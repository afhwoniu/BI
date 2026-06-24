using System.Text;
using Bi.Api.Middlewares;
using Bi.Api.Services;
using Bi.Application.Services;
using Bi.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

// 配置Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/bi-platform-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// 添加数据库上下文
builder.Services.AddDbContext<BiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BiManagement"),
        npgsqlOptions => npgsqlOptions.UseVector()));

// 注册系统配置服务
builder.Services.AddScoped<IConfigService, ConfigService>();

// 注册报表服务
builder.Services.AddScoped<IReportService, ReportService>();

// 注册图表缓存服务
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IChartCacheService, MemoryChartCacheService>();

// 注册慢查询服务
builder.Services.AddScoped<ISlowQueryService, SlowQueryService>();

// 注册备份还原服务
builder.Services.AddScoped<IBackupService, BackupService>();

// 注册智能BI服务
builder.Services.AddScoped<ISchemaService, SchemaService>();
builder.Services.AddHttpClient<ILlmService, DeepSeekLlmService>();
builder.Services.AddScoped<PptGeneratorService>(); // PPT生成服务（使用ShapeCrawler）

// Embedding服务：根据配置选择OpenAI或Ollama
var embeddingProvider = builder.Configuration["Embedding:Provider"] ?? "openai";
if (embeddingProvider.Equals("ollama", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>();
}
else
{
    builder.Services.AddHttpClient<IEmbeddingService, OpenAiEmbeddingService>();
}

builder.Services.AddScoped<IKpiRetrieverService, KpiRetrieverService>();

// 知识库相关服务
builder.Services.AddScoped<IDocumentParserService, DocumentParserService>();
builder.Services.AddSingleton<ITextChunkerService, TextChunkerService>();
builder.Services.AddScoped<IKnowledgeService, KnowledgeService>();

// 文档处理后台服务（异步处理分块和向量化）
builder.Services.AddHostedService<DocumentProcessingService>();

// 统一检索服务（RAG）
builder.Services.AddScoped<IUnifiedSearchService, UnifiedSearchService>();

// 知识库测试服务
builder.Services.AddScoped<IKnowledgeTestService, KnowledgeTestService>();

// ASR语音识别服务
builder.Services.AddHttpClient<IAsrService, ZhipuAsrService>();

// 预警模块服务
builder.Services.AddScoped<IAlertEngineService, AlertEngineService>();
builder.Services.AddScoped<IAlertSchemaInitializer, AlertSchemaInitializer>();
builder.Services.AddHostedService<AlertSchedulerService>();
builder.Services.AddHostedService<AlertNotificationDispatcherService>();

// 添加JWT认证
var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSection["Secret"] ?? "DefaultSecretKey123456789012345678901234";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

// 添加CORS
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// 添加控制器
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// 添加Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "智能BI可视化平台 API",
        Version = "v1",
        Description = "基于ASP.NET Core 9的智能BI可视化系统后端API"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT授权，请输入: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 配置HTTP管道
app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "BI Platform API v1"));
}

app.UseCors("AllowFrontend");

// 启用静态文件服务（用于访问上传的图片等资源）
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 自动迁移数据库（开发环境）
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BiDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("PendingModelChangesWarning", StringComparison.OrdinalIgnoreCase))
    {
        Log.Warning(ex, "检测到EF模型与迁移存在差异，已跳过自动迁移以保证服务可启动");
    }

    // 初始化默认管理员账号
    if (!db.SysUsers.Any())
    {
        db.SysUsers.Add(new Bi.Domain.Entities.SysUser
        {
            Username = "admin",
            PasswordHash = Bi.Api.Controllers.AuthController.HashPassword("admin123"),
            RealName = "系统管理员",
            IsEnabled = true
        });
        db.SaveChanges();
        Log.Information("已创建默认管理员账号: admin / admin123");
    }

    // 初始化默认系统配置
    var configService = scope.ServiceProvider.GetRequiredService<IConfigService>();
    configService.InitializeDefaultsAsync().GetAwaiter().GetResult();
}

// 初始化预警模块数据库对象（幂等）
using (var alertScope = app.Services.CreateScope())
{
    try
    {
        var alertSchemaInitializer = alertScope.ServiceProvider.GetRequiredService<IAlertSchemaInitializer>();
        alertSchemaInitializer.EnsureSchemaAsync().GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "预警模块数据库初始化失败，系统将继续启动");
    }
}

Log.Information("智能BI可视化平台启动成功，监听端口: 5000");
app.Run();
