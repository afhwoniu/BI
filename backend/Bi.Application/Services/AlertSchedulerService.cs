using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bi.Application.Services;

/// <summary>
/// 预警规则调度后台服务
/// </summary>
public class AlertSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertSchedulerService> _logger;

    public AlertSchedulerService(IServiceScopeFactory scopeFactory, ILogger<AlertSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("预警调度服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var engine = scope.ServiceProvider.GetRequiredService<IAlertEngineService>();
                var runCount = await engine.RunDueRulesAsync(stoppingToken);
                if (runCount > 0)
                {
                    _logger.LogInformation("预警调度执行完成，本轮执行规则数: {RunCount}", runCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "预警调度执行失败");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 忽略取消异常，按框架生命周期退出
            }
        }

        _logger.LogInformation("预警调度服务已停止");
    }
}

