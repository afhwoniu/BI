using Bi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace Bi.Infrastructure.Data;

/// <summary>
/// BI平台数据库上下文
/// </summary>
public class BiDbContext : DbContext
{
    public BiDbContext(DbContextOptions<BiDbContext> options) : base(options) { }

    // 数据源相关
    public DbSet<Datasource> Datasources => Set<Datasource>();
    public DbSet<Dataset> Datasets => Set<Dataset>();
    public DbSet<DatasetField> DatasetFields => Set<DatasetField>();

    // 图表相关
    public DbSet<Chart> Charts => Set<Chart>();

    // 面板相关
    public DbSet<Panel> Panels => Set<Panel>();
    public DbSet<PanelItem> PanelItems => Set<PanelItem>();

    // 系统用户
    public DbSet<SysUser> SysUsers => Set<SysUser>();

    // 指标知识库
    public DbSet<KpiCategory> KpiCategories => Set<KpiCategory>();
    public DbSet<KpiDefinition> KpiDefinitions => Set<KpiDefinition>();

    // AI会话
    public DbSet<AiSession> AiSessions => Set<AiSession>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<AiFavorite> AiFavorites => Set<AiFavorite>();

    // 系统配置
    public DbSet<SysConfig> SysConfigs => Set<SysConfig>();

    // 报表相关
    public DbSet<BiReport> Reports => Set<BiReport>();
    public DbSet<BiReportPage> ReportPages => Set<BiReportPage>();
    public DbSet<BiReportItem> ReportItems => Set<BiReportItem>();

    // 菜单与发布
    public DbSet<SysMenu> Menus => Set<SysMenu>();
    public DbSet<BiPublish> Publishes => Set<BiPublish>();

    // 角色与组织
    public DbSet<SysRole> Roles => Set<SysRole>();
    public DbSet<SysOrg> Orgs => Set<SysOrg>();
    public DbSet<SysRoleMenu> RoleMenus => Set<SysRoleMenu>();
    public DbSet<SysUserRole> UserRoles => Set<SysUserRole>();

    // 慢查询日志
    public DbSet<SlowQueryLog> SlowQueryLogs => Set<SlowQueryLog>();

    // 预警模块
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();
    public DbSet<AlertEventAction> AlertEventActions => Set<AlertEventAction>();
    public DbSet<AlertSubscription> AlertSubscriptions => Set<AlertSubscription>();
    public DbSet<AlertNotificationLog> AlertNotificationLogs => Set<AlertNotificationLog>();
    public DbSet<AlertMetricSnapshot> AlertMetricSnapshots => Set<AlertMetricSnapshot>();

    // RAG知识库
    public DbSet<KnowledgeCategory> KnowledgeCategories => Set<KnowledgeCategory>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    // 知识库测试
    public DbSet<KnowledgeTestCase> KnowledgeTestCases => Set<KnowledgeTestCase>();
    public DbSet<KnowledgeTestRun> KnowledgeTestRuns => Set<KnowledgeTestRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 数据源表配置
        modelBuilder.Entity<Datasource>(entity =>
        {
            entity.ToTable("bi_datasource");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Type).HasColumnName("type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ConnString).HasColumnName("conn_string").IsRequired();
            entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasComment("数据源配置表");
        });

        // 数据集表配置
        modelBuilder.Entity<Dataset>(entity =>
        {
            entity.ToTable("bi_dataset");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.DatasourceId).HasColumnName("datasource_id");
            entity.Property(e => e.SqlText).HasColumnName("sql_text").IsRequired();
            entity.Property(e => e.ParamSchema).HasColumnName("param_schema").HasColumnType("jsonb");
            entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(e => e.Datasource).WithMany(d => d.Datasets).HasForeignKey(e => e.DatasourceId);
            entity.HasComment("SQL数据集定义表");
        });

        // 数据集字段表配置
        modelBuilder.Entity<DatasetField>(entity =>
        {
            entity.ToTable("bi_dataset_field");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DatasetId).HasColumnName("dataset_id");
            entity.Property(e => e.FieldName).HasColumnName("field_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.FieldAlias).HasColumnName("field_alias").HasMaxLength(100);
            entity.Property(e => e.DataType).HasColumnName("data_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(20).IsRequired();
            entity.Property(e => e.AggType).HasColumnName("agg_type").HasMaxLength(20).HasDefaultValue("none");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(e => e.Dataset).WithMany(d => d.Fields).HasForeignKey(e => e.DatasetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasComment("数据集字段元数据表");
        });

        // 图表表配置
        modelBuilder.Entity<Chart>(entity =>
        {
            entity.ToTable("bi_chart");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.DatasetId).HasColumnName("dataset_id");
            entity.Property(e => e.ChartType).HasColumnName("chart_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ConfigJson).HasColumnName("config_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(e => e.Dataset).WithMany(d => d.Charts).HasForeignKey(e => e.DatasetId);
            entity.HasComment("图表配置表");
        });

        // 面板表配置
        modelBuilder.Entity<Panel>(entity =>
        {
            entity.ToTable("bi_panel");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.PanelType).HasColumnName("panel_type").HasMaxLength(50).HasDefaultValue("pc_dashboard");
            entity.Property(e => e.ConfigJson).HasColumnName("config_json").HasColumnType("jsonb");
            entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasComment("分析面板表");
        });

        // 面板子项表配置
        modelBuilder.Entity<PanelItem>(entity =>
        {
            entity.ToTable("bi_panel_item");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PanelId).HasColumnName("panel_id");
            entity.Property(e => e.ChartId).HasColumnName("chart_id");
            entity.Property(e => e.LayoutJson).HasColumnName("layout_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.ScreenLayoutJson).HasColumnName("screen_layout_json").HasColumnType("jsonb");
            entity.Property(e => e.MobileLayoutJson).HasColumnName("mobile_layout_json").HasColumnType("jsonb");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(e => e.Panel).WithMany(p => p.Items).HasForeignKey(e => e.PanelId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Chart).WithMany(c => c.PanelItems).HasForeignKey(e => e.ChartId);
            entity.HasComment("面板子项表");
        });

        // 系统用户表配置
        modelBuilder.Entity<SysUser>(entity =>
        {
            entity.ToTable("sys_user");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(256).IsRequired();
            entity.Property(e => e.RealName).HasColumnName("real_name").HasMaxLength(50);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100);
            entity.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(20);
            entity.Property(e => e.Avatar).HasColumnName("avatar").HasMaxLength(500);
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasOne(e => e.Org).WithMany().HasForeignKey(e => e.OrgId);
            entity.HasComment("系统用户表");
        });

        // 指标分类表配置
        modelBuilder.Entity<KpiCategory>(entity =>
        {
            entity.ToTable("bi_kpi_category");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.ParentId).HasColumnName("parent_id");
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(e => e.Parent).WithMany(e => e.Children).HasForeignKey(e => e.ParentId);
            entity.HasComment("指标分类表");
        });

        // 指标定义表配置
        modelBuilder.Entity<KpiDefinition>(entity =>
        {
            entity.ToTable("bi_kpi_definition");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.Definition).HasColumnName("definition").HasMaxLength(2000);
            entity.Property(e => e.Formula).HasColumnName("formula").HasMaxLength(1000);
            entity.Property(e => e.SqlTemplate).HasColumnName("sql_template");
            entity.Property(e => e.DatasourceId).HasColumnName("datasource_id");
            entity.Property(e => e.Unit).HasColumnName("unit").HasMaxLength(50);
            entity.Property(e => e.DataType).HasColumnName("data_type").HasMaxLength(50).HasDefaultValue("number");
            // 向量字段使用JSON格式存储（兼容无pgvector的环境）
            entity.Property(e => e.EmbeddingJson).HasColumnName("embedding_json");
            entity.Property(e => e.EmbeddingUpdatedAt).HasColumnName("embedding_updated_at");
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(e => e.Category).WithMany(e => e.Kpis).HasForeignKey(e => e.CategoryId);
            entity.HasOne(e => e.Datasource).WithMany().HasForeignKey(e => e.DatasourceId);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasComment("指标定义表");
        });

        // AI会话表配置
        modelBuilder.Entity<AiSession>(entity =>
        {
            entity.ToTable("bi_ai_session");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SessionKey).HasColumnName("session_key").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(200);
            entity.Property(e => e.Mode).HasColumnName("mode").HasMaxLength(20).HasDefaultValue("bi");  // 对话模式：bi/hz360/internetsearch
            entity.Property(e => e.DatasourceId).HasColumnName("datasource_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.LastActiveAt).HasColumnName("last_active_at");
            entity.HasOne(e => e.Datasource).WithMany().HasForeignKey(e => e.DatasourceId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            entity.HasIndex(e => e.SessionKey).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasComment("AI会话表");
        });

        // AI消息表配置
        modelBuilder.Entity<AiMessage>(entity =>
        {
            entity.ToTable("bi_ai_message");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Mode).HasColumnName("mode").HasMaxLength(20);  // 对话模式：bi/hz360/internetsearch
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.Sql).HasColumnName("sql");
            entity.Property(e => e.DetailSql).HasColumnName("detail_sql");  // 明细SQL
            entity.Property(e => e.HospitalField).HasColumnName("hospital_field").HasMaxLength(100);  // 医院字段
            entity.Property(e => e.DateField).HasColumnName("date_field").HasMaxLength(100);  // 日期字段（用于同比环比）
            entity.Property(e => e.DimensionFields).HasColumnName("dimension_fields");  // 维度字段JSON
            entity.Property(e => e.MeasureFields).HasColumnName("measure_fields");  // 度量字段JSON
            entity.Property(e => e.KpiConfig).HasColumnName("kpi_config");  // KPI配置JSON
            entity.Property(e => e.DefaultChartsConfig).HasColumnName("default_charts_config");  // 原始图表配置JSON
            entity.Property(e => e.PromptText).HasColumnName("prompt_text");  // 完整提示词（旧版兼容）
            entity.Property(e => e.PromptsJson).HasColumnName("prompts_json");  // 分阶段提示词JSON
            entity.Property(e => e.ChartType).HasColumnName("chart_type").HasMaxLength(50);
            entity.Property(e => e.ChartConfig).HasColumnName("chart_config");
            entity.Property(e => e.TokensUsed).HasColumnName("tokens_used");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasOne(e => e.Session).WithMany(e => e.Messages).HasForeignKey(e => e.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.SessionId);
            entity.HasComment("AI消息表");
        });

        // AI收藏表配置
        modelBuilder.Entity<AiFavorite>(entity =>
        {
            entity.ToTable("bi_ai_favorite");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Question).HasColumnName("question");
            entity.Property(e => e.Sql).HasColumnName("sql");
            entity.Property(e => e.ChartType).HasColumnName("chart_type").HasMaxLength(50);
            entity.Property(e => e.ChartConfig).HasColumnName("chart_config");
            entity.Property(e => e.DatasourceId).HasColumnName("datasource_id");
            entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.Datasource).WithMany().HasForeignKey(e => e.DatasourceId);
            entity.HasIndex(e => e.UserId);
            entity.HasComment("AI查询收藏表");
        });

        // 系统配置表配置
        modelBuilder.Entity<SysConfig>(entity =>
        {
            entity.ToTable("sys_config");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ConfigKey).HasColumnName("config_key").HasMaxLength(100).IsRequired();
            entity.Property(e => e.ConfigValue).HasColumnName("config_value");
            entity.Property(e => e.ConfigGroup).HasColumnName("config_group").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ConfigType).HasColumnName("config_type").HasMaxLength(50).HasDefaultValue("string");
            entity.Property(e => e.IsEncrypted).HasColumnName("is_encrypted").HasDefaultValue(false);
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(100);
            entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
            entity.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => e.ConfigKey).IsUnique();
            entity.HasIndex(e => e.ConfigGroup);
            entity.HasComment("系统配置表");
        });

        // 报表主表配置
        modelBuilder.Entity<BiReport>(entity =>
        {
            entity.ToTable("bi_report");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.ReportType).HasColumnName("report_type").HasMaxLength(50).HasDefaultValue("report");
            entity.Property(e => e.CoverImage).HasColumnName("cover_image").HasMaxLength(500);
            entity.Property(e => e.ConfigJson).HasColumnName("config_json").HasColumnType("jsonb");
            entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
            entity.Property(e => e.IsPublished).HasColumnName("is_published").HasDefaultValue(false);
            entity.Property(e => e.PublishedAt).HasColumnName("published_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasComment("报表/报告主表");
        });

        // 报表页面配置
        modelBuilder.Entity<BiReportPage>(entity =>
        {
            entity.ToTable("bi_report_page");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(200);
            entity.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(e => e.ConfigJson).HasColumnName("config_json").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(e => e.Report).WithMany(r => r.Pages).HasForeignKey(e => e.ReportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasComment("报表页面表");
        });

        // 报表元素配置
        modelBuilder.Entity<BiReportItem>(entity =>
        {
            entity.ToTable("bi_report_item");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PageId).HasColumnName("page_id");
            entity.Property(e => e.ItemType).HasColumnName("item_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ChartId).HasColumnName("chart_id");
            entity.Property(e => e.PanelId).HasColumnName("panel_id");
            entity.Property(e => e.TextContent).HasColumnName("text_content");
            entity.Property(e => e.ImageUrl).HasColumnName("image_url").HasMaxLength(500);
            entity.Property(e => e.LayoutJson).HasColumnName("layout_json").HasColumnType("jsonb");
            entity.Property(e => e.StyleJson).HasColumnName("style_json").HasColumnType("jsonb");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(e => e.Page).WithMany(p => p.Items).HasForeignKey(e => e.PageId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Chart).WithMany().HasForeignKey(e => e.ChartId);
            entity.HasOne(e => e.Panel).WithMany().HasForeignKey(e => e.PanelId);
            entity.HasComment("报表元素表");
        });

        // 系统菜单配置
        modelBuilder.Entity<SysMenu>(entity =>
        {
            entity.ToTable("sys_menu");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.ParentId).HasColumnName("parent_id").HasDefaultValue(0);
            entity.Property(e => e.MenuType).HasColumnName("menu_type").HasMaxLength(50).HasDefaultValue("folder");
            entity.Property(e => e.Icon).HasColumnName("icon").HasMaxLength(100);
            entity.Property(e => e.LinkUrl).HasColumnName("link_url").HasMaxLength(500);
            entity.Property(e => e.PublishId).HasColumnName("publish_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(e => e.IsVisible).HasColumnName("is_visible").HasDefaultValue(true);
            entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(e => e.Publish).WithMany().HasForeignKey(e => e.PublishId);
            entity.HasComment("系统菜单表");
        });

        // 发布记录配置
        modelBuilder.Entity<BiPublish>(entity =>
        {
            entity.ToTable("bi_publish");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            entity.Property(e => e.ObjectType).HasColumnName("object_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ObjectId).HasColumnName("object_id");
            entity.Property(e => e.AccessScope).HasColumnName("access_scope").HasMaxLength(50).HasDefaultValue("private");
            entity.Property(e => e.AccessToken).HasColumnName("access_token").HasMaxLength(100);
            entity.Property(e => e.AccessPassword).HasColumnName("access_password").HasMaxLength(100);
            entity.Property(e => e.ExpireAt).HasColumnName("expire_at");
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
            entity.Property(e => e.ViewCount).HasColumnName("view_count").HasDefaultValue(0);
            entity.Property(e => e.LastViewedAt).HasColumnName("last_viewed_at");
            entity.Property(e => e.PublishedBy).HasColumnName("published_by");
            entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
            entity.Property(e => e.AllowedRoles).HasColumnName("allowed_roles").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => e.AccessToken).IsUnique();
            entity.HasComment("发布记录表");
        });

        // 系统角色配置
        modelBuilder.Entity<SysRole>(entity =>
        {
            entity.ToTable("sys_role");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RoleCode).HasColumnName("role_code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.RoleName).HasColumnName("role_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
            entity.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => e.RoleCode).IsUnique();
            entity.HasComment("系统角色表");
        });

        // 系统组织配置
        modelBuilder.Entity<SysOrg>(entity =>
        {
            entity.ToTable("sys_org");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrgCode).HasColumnName("org_code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.OrgName).HasColumnName("org_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.ParentId).HasColumnName("parent_id").HasDefaultValue(0);
            entity.Property(e => e.OrgType).HasColumnName("org_type").HasMaxLength(50).HasDefaultValue("dept");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
            entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => e.OrgCode).IsUnique();
            entity.Ignore(e => e.Children);
            entity.HasComment("系统组织表");
        });

        // 角色菜单关联配置
        modelBuilder.Entity<SysRoleMenu>(entity =>
        {
            entity.ToTable("sys_role_menu");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.MenuId).HasColumnName("menu_id");
            entity.HasOne(e => e.Role).WithMany(r => r.RoleMenus).HasForeignKey(e => e.RoleId);
            entity.HasOne(e => e.Menu).WithMany().HasForeignKey(e => e.MenuId);
            entity.HasIndex(e => new { e.RoleId, e.MenuId }).IsUnique();
            entity.HasComment("角色菜单关联表");
        });

        // 用户角色关联配置
        modelBuilder.Entity<SysUserRole>(entity =>
        {
            entity.ToTable("sys_user_role");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.HasOne(e => e.User).WithMany(u => u.UserRoles).HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.Role).WithMany(r => r.UserRoles).HasForeignKey(e => e.RoleId);
            entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();
            entity.HasComment("用户角色关联表");
        });

        // 慢查询日志配置
        modelBuilder.Entity<SlowQueryLog>(entity =>
        {
            entity.ToTable("bi_slow_query_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DatasourceId).HasColumnName("datasource_id");
            entity.Property(e => e.ChartId).HasColumnName("chart_id");
            entity.Property(e => e.SqlText).HasColumnName("sql_text");
            entity.Property(e => e.ExecutionTimeMs).HasColumnName("execution_time_ms");
            entity.Property(e => e.ThresholdMs).HasColumnName("threshold_ms");
            entity.Property(e => e.ExecutedBy).HasColumnName("executed_by");
            entity.Property(e => e.ExecutedAt).HasColumnName("executed_at");
            entity.Property(e => e.ExplainResult).HasColumnName("explain_result");
            entity.Property(e => e.Suggestion).HasColumnName("suggestion");
            entity.Property(e => e.IsResolved).HasColumnName("is_resolved");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(e => e.Datasource).WithMany().HasForeignKey(e => e.DatasourceId);
            entity.HasOne(e => e.Chart).WithMany().HasForeignKey(e => e.ChartId);
            entity.HasComment("慢查询日志表");
        });

        // 预警规则表配置
        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.ToTable("bi_alert_rule");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RuleCode).HasColumnName("rule_code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.RuleName).HasColumnName("rule_name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.RuleType).HasColumnName("rule_type").HasMaxLength(30).HasDefaultValue(AlertRuleTypes.Threshold);
            entity.Property(e => e.SeverityLevel).HasColumnName("severity_level").HasMaxLength(20).HasDefaultValue(AlertSeverityLevels.Warning);
            entity.Property(e => e.RuleStatus).HasColumnName("rule_status").HasMaxLength(20).HasDefaultValue(AlertRuleStatuses.Enabled);
            entity.Property(e => e.DatasourceId).HasColumnName("datasource_id");
            entity.Property(e => e.DatasetId).HasColumnName("dataset_id");
            entity.Property(e => e.ChartId).HasColumnName("chart_id");
            entity.Property(e => e.KpiId).HasColumnName("kpi_id");
            entity.Property(e => e.MetricField).HasColumnName("metric_field").HasMaxLength(100);
            entity.Property(e => e.DimensionField).HasColumnName("dimension_field").HasMaxLength(100);
            entity.Property(e => e.TimeField).HasColumnName("time_field").HasMaxLength(100);
            entity.Property(e => e.StatGranularity).HasColumnName("stat_granularity").HasMaxLength(20).HasDefaultValue("day");
            entity.Property(e => e.ConditionJson).HasColumnName("condition_json").HasColumnType("jsonb").HasDefaultValue("{}");
            entity.Property(e => e.CalcSql).HasColumnName("calc_sql");
            entity.Property(e => e.ScheduleType).HasColumnName("schedule_type").HasMaxLength(20).HasDefaultValue("interval");
            entity.Property(e => e.CronExpr).HasColumnName("cron_expr").HasMaxLength(100);
            entity.Property(e => e.IntervalSeconds).HasColumnName("interval_seconds").HasDefaultValue(300);
            entity.Property(e => e.Timezone).HasColumnName("timezone").HasMaxLength(50).HasDefaultValue("Asia/Shanghai");
            entity.Property(e => e.DedupMinutes).HasColumnName("dedup_minutes").HasDefaultValue(60);
            entity.Property(e => e.CooldownMinutes).HasColumnName("cooldown_minutes").HasDefaultValue(30);
            entity.Property(e => e.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(e => e.NotifyChannels).HasColumnName("notify_channels").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.NotifyTemplate).HasColumnName("notify_template");
            entity.Property(e => e.LastCheckAt).HasColumnName("last_check_at");
            entity.Property(e => e.NextCheckAt).HasColumnName("next_check_at");
            entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Datasource).WithMany().HasForeignKey(e => e.DatasourceId);
            entity.HasOne(e => e.Dataset).WithMany().HasForeignKey(e => e.DatasetId);
            entity.HasOne(e => e.Chart).WithMany().HasForeignKey(e => e.ChartId);
            entity.HasOne(e => e.Kpi).WithMany().HasForeignKey(e => e.KpiId);
            entity.HasOne(e => e.OwnerUser).WithMany().HasForeignKey(e => e.OwnerUserId);

            entity.HasIndex(e => e.RuleCode).IsUnique();
            entity.HasIndex(e => e.RuleStatus);
            entity.HasIndex(e => e.NextCheckAt);
            entity.HasComment("预警规则主表");
        });

        // 预警事件表配置
        modelBuilder.Entity<AlertEvent>(entity =>
        {
            entity.ToTable("bi_alert_event");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EventNo).HasColumnName("event_no").HasMaxLength(64).IsRequired();
            entity.Property(e => e.RuleId).HasColumnName("rule_id");
            entity.Property(e => e.RuleSnapshotJson).HasColumnName("rule_snapshot_json").HasColumnType("jsonb").HasDefaultValue("{}");
            entity.Property(e => e.EventStatus).HasColumnName("event_status").HasMaxLength(20).HasDefaultValue(AlertEventStatuses.Open);
            entity.Property(e => e.SeverityLevel).HasColumnName("severity_level").HasMaxLength(20).IsRequired();
            entity.Property(e => e.TriggerTime).HasColumnName("trigger_time");
            entity.Property(e => e.FirstTriggeredAt).HasColumnName("first_triggered_at");
            entity.Property(e => e.LastTriggeredAt).HasColumnName("last_triggered_at");
            entity.Property(e => e.TriggerCount).HasColumnName("trigger_count").HasDefaultValue(1);
            entity.Property(e => e.CurrentValue).HasColumnName("current_value").HasPrecision(20, 4);
            entity.Property(e => e.BaselineValue).HasColumnName("baseline_value").HasPrecision(20, 4);
            entity.Property(e => e.CompareValue).HasColumnName("compare_value").HasPrecision(20, 4);
            entity.Property(e => e.ChangePct).HasColumnName("change_pct").HasPrecision(10, 4);
            entity.Property(e => e.ThresholdDesc).HasColumnName("threshold_desc").HasMaxLength(500);
            entity.Property(e => e.DimensionValueJson).HasColumnName("dimension_value_json").HasColumnType("jsonb").HasDefaultValue("{}");
            entity.Property(e => e.EvidenceJson).HasColumnName("evidence_json").HasColumnType("jsonb").HasDefaultValue("{}");
            entity.Property(e => e.SuggestionText).HasColumnName("suggestion_text");
            entity.Property(e => e.AckBy).HasColumnName("ack_by");
            entity.Property(e => e.AckAt).HasColumnName("ack_at");
            entity.Property(e => e.ResolvedBy).HasColumnName("resolved_by");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.ResolutionNote).HasColumnName("resolution_note").HasMaxLength(1000);
            entity.Property(e => e.IsNotified).HasColumnName("is_notified").HasDefaultValue(false);
            entity.Property(e => e.NotifiedAt).HasColumnName("notified_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Rule).WithMany(r => r.Events).HasForeignKey(e => e.RuleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AckUser).WithMany().HasForeignKey(e => e.AckBy);
            entity.HasOne(e => e.ResolvedUser).WithMany().HasForeignKey(e => e.ResolvedBy);

            entity.HasIndex(e => e.EventNo).IsUnique();
            entity.HasIndex(e => e.RuleId);
            entity.HasIndex(e => e.EventStatus);
            entity.HasIndex(e => e.TriggerTime);
            entity.HasComment("预警事件表");
        });

        // 预警事件动作表配置
        modelBuilder.Entity<AlertEventAction>(entity =>
        {
            entity.ToTable("bi_alert_event_action");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.ActionType).HasColumnName("action_type").HasMaxLength(30).IsRequired();
            entity.Property(e => e.ActionUserId).HasColumnName("action_user_id");
            entity.Property(e => e.ActionNote).HasColumnName("action_note").HasMaxLength(1000);
            entity.Property(e => e.ActionPayload).HasColumnName("action_payload").HasColumnType("jsonb").HasDefaultValue("{}");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Event).WithMany(e => e.Actions).HasForeignKey(e => e.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ActionUser).WithMany().HasForeignKey(e => e.ActionUserId);

            entity.HasIndex(e => e.EventId);
            entity.HasComment("预警事件处置动作表");
        });

        // 预警订阅表配置
        modelBuilder.Entity<AlertSubscription>(entity =>
        {
            entity.ToTable("bi_alert_subscription");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RuleId).HasColumnName("rule_id");
            entity.Property(e => e.SubscriberType).HasColumnName("subscriber_type").HasMaxLength(20).IsRequired();
            entity.Property(e => e.SubscriberId).HasColumnName("subscriber_id");
            entity.Property(e => e.ChannelType).HasColumnName("channel_type").HasMaxLength(20).IsRequired();
            entity.Property(e => e.ChannelTarget).HasColumnName("channel_target").HasMaxLength(200);
            entity.Property(e => e.SeverityFilter).HasColumnName("severity_filter").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Rule).WithMany(r => r.Subscriptions).HasForeignKey(e => e.RuleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.RuleId);
            entity.HasComment("预警订阅表");
        });

        // 预警通知日志表配置
        modelBuilder.Entity<AlertNotificationLog>(entity =>
        {
            entity.ToTable("bi_alert_notification_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.RuleId).HasColumnName("rule_id");
            entity.Property(e => e.SubscriptionId).HasColumnName("subscription_id");
            entity.Property(e => e.ChannelType).HasColumnName("channel_type").HasMaxLength(20).IsRequired();
            entity.Property(e => e.SendTo).HasColumnName("send_to").HasMaxLength(200);
            entity.Property(e => e.SendStatus).HasColumnName("send_status").HasMaxLength(20).HasDefaultValue("pending");
            entity.Property(e => e.SendContent).HasColumnName("send_content");
            entity.Property(e => e.ResponseText).HasColumnName("response_text");
            entity.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
            entity.Property(e => e.SentAt).HasColumnName("sent_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Event).WithMany(e => e.NotificationLogs).HasForeignKey(e => e.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Rule).WithMany().HasForeignKey(e => e.RuleId);
            entity.HasOne(e => e.Subscription).WithMany().HasForeignKey(e => e.SubscriptionId);

            entity.HasIndex(e => e.EventId);
            entity.HasIndex(e => e.SendStatus);
            entity.HasComment("预警通知发送日志表");
        });

        // 预警指标快照表配置
        modelBuilder.Entity<AlertMetricSnapshot>(entity =>
        {
            entity.ToTable("bi_alert_metric_snapshot");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RuleId).HasColumnName("rule_id");
            entity.Property(e => e.SnapshotTime).HasColumnName("snapshot_time");
            entity.Property(e => e.CurrentValue).HasColumnName("current_value").HasPrecision(20, 4);
            entity.Property(e => e.BaselineValue).HasColumnName("baseline_value").HasPrecision(20, 4);
            entity.Property(e => e.CompareValue).HasColumnName("compare_value").HasPrecision(20, 4);
            entity.Property(e => e.ChangePct).HasColumnName("change_pct").HasPrecision(10, 4);
            entity.Property(e => e.DimensionValueJson).HasColumnName("dimension_value_json").HasColumnType("jsonb").HasDefaultValue("{}");
            entity.Property(e => e.CalcContextJson).HasColumnName("calc_context_json").HasColumnType("jsonb").HasDefaultValue("{}");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Rule).WithMany(r => r.MetricSnapshots).HasForeignKey(e => e.RuleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.RuleId, e.SnapshotTime });
            entity.HasComment("预警指标快照表");
        });

        // 知识库分类表配置
        modelBuilder.Entity<KnowledgeCategory>(entity =>
        {
            entity.ToTable("bi_knowledge_category");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.ParentId).HasColumnName("parent_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasOne(e => e.Parent).WithMany(e => e.Children).HasForeignKey(e => e.ParentId);
            entity.HasComment("知识库分类表");
        });

        // 知识库文档表配置
        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.ToTable("bi_knowledge_document");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
            entity.Property(e => e.FileName).HasColumnName("file_name").HasMaxLength(255);
            entity.Property(e => e.FileType).HasColumnName("file_type").HasMaxLength(50);
            entity.Property(e => e.FileSize).HasColumnName("file_size");
            entity.Property(e => e.FilePath).HasColumnName("file_path").HasMaxLength(500);
            entity.Property(e => e.ContentHash).HasColumnName("content_hash").HasMaxLength(64);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("pending");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.ChunkCount).HasColumnName("chunk_count").HasDefaultValue(0);
            entity.Property(e => e.DatasourceId).HasColumnName("datasource_id");
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(e => e.Category).WithMany(c => c.Documents).HasForeignKey(e => e.CategoryId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CategoryId);
            entity.HasComment("知识库文档表");
        });

        // 知识库分块表配置（含向量）
        modelBuilder.Entity<KnowledgeChunk>(entity =>
        {
            entity.ToTable("bi_knowledge_chunk");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.ChunkIndex).HasColumnName("chunk_index");
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.ContentLength).HasColumnName("content_length");
            // pgvector 向量字段配置（1024维 for BGE-M3）
            entity.Property(e => e.Embedding).HasColumnName("embedding").HasColumnType("vector(1024)");
            entity.Property(e => e.PageNumber).HasColumnName("page_number");
            entity.Property(e => e.SectionTitle).HasColumnName("section_title").HasMaxLength(500);
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasOne(e => e.Document).WithMany(d => d.Chunks).HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.DocumentId);
            entity.HasComment("知识库分块表（含向量嵌入）");
        });

        // 知识库测试用例表配置
        modelBuilder.Entity<KnowledgeTestCase>(entity =>
        {
            entity.ToTable("bi_knowledge_test_case");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Query).HasColumnName("query").IsRequired();
            entity.Property(e => e.ExpectedDocumentIds).HasColumnName("expected_document_ids").HasColumnType("jsonb");
            entity.Property(e => e.ExpectedChunkIds).HasColumnName("expected_chunk_ids").HasColumnType("jsonb");
            entity.Property(e => e.ExpectedKeywords).HasColumnName("expected_keywords").HasColumnType("jsonb");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.Remark).HasColumnName("remark");
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasComment("知识库测试用例表");
        });

        // 知识库测试运行记录表配置
        modelBuilder.Entity<KnowledgeTestRun>(entity =>
        {
            entity.ToTable("bi_knowledge_test_run");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.TotalCases).HasColumnName("total_cases");
            entity.Property(e => e.CompletedCases).HasColumnName("completed_cases");
            entity.Property(e => e.TopK).HasColumnName("top_k");
            entity.Property(e => e.MinScore).HasColumnName("min_score");
            entity.Property(e => e.HitRate).HasColumnName("hit_rate");
            entity.Property(e => e.Mrr).HasColumnName("mrr");
            entity.Property(e => e.AvgPrecision).HasColumnName("avg_precision");
            entity.Property(e => e.AvgRecall).HasColumnName("avg_recall");
            entity.Property(e => e.AvgLatencyMs).HasColumnName("avg_latency_ms");
            entity.Property(e => e.DetailResults).HasColumnName("detail_results").HasColumnType("jsonb");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasComment("知识库测试运行记录表");
        });
    }
}
