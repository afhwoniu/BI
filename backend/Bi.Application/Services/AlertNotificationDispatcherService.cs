using System.Text;
using Bi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bi.Application.Services;

/// <summary>
/// 预警通知派发后台服务（V1）
/// </summary>
public class AlertNotificationDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertNotificationDispatcherService> _logger;

    private const int BatchSize = 50;
    private const int MaxRetryCount = 3;

    public AlertNotificationDispatcherService(
        IServiceScopeFactory scopeFactory,
        ILogger<AlertNotificationDispatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("预警通知派发服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BiDbContext>();

                var pendingLogs = await db.AlertNotificationLogs
                    .Where(x => x.SendStatus == "pending")
                    .Where(x => x.RetryCount < MaxRetryCount)
                    .OrderBy(x => x.CreatedAt)
                    .Take(BatchSize)
                    .ToListAsync(stoppingToken);

                if (pendingLogs.Count > 0)
                {
                    foreach (var log in pendingLogs)
                    {
                        await DispatchOneAsync(log, db, stoppingToken);
                    }

                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("预警通知派发完成，本轮处理 {Count} 条", pendingLogs.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "预警通知派发异常");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 按生命周期退出
            }
        }

        _logger.LogInformation("预警通知派发服务已停止");
    }

    private async Task DispatchOneAsync(Domain.Entities.AlertNotificationLog log, BiDbContext db, CancellationToken ct)
    {
        try
        {
            var channel = (log.ChannelType ?? string.Empty).Trim().ToLowerInvariant();

            if (channel == "inapp")
            {
                log.SendStatus = "success";
                log.ResponseText = "站内消息派发成功";
                log.SentAt = DateTime.UtcNow;
                log.UpdatedAt = DateTime.UtcNow;
                return;
            }

            if (channel is not ("webhook" or "wecom"))
            {
                MarkFailed(log, $"暂不支持的通知渠道: {log.ChannelType}");
                return;
            }

            if (string.IsNullOrWhiteSpace(log.SendTo) || !Uri.TryCreate(log.SendTo, UriKind.Absolute, out var uri))
            {
                MarkFailed(log, "通知地址无效");
                return;
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            var payload = BuildWebhookPayload(log, db);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await http.SendAsync(request, ct);
            var responseText = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                log.SendStatus = "success";
                log.ResponseText = Truncate(responseText, 500);
                log.SentAt = DateTime.UtcNow;
                log.UpdatedAt = DateTime.UtcNow;
                return;
            }

            MarkFailed(log, $"HTTP {(int)response.StatusCode}: {Truncate(responseText, 500)}");
        }
        catch (Exception ex)
        {
            MarkFailed(log, $"发送异常: {ex.Message}");
        }
    }

    private static string BuildWebhookPayload(Domain.Entities.AlertNotificationLog log, BiDbContext db)
    {
        var evt = db.AlertEvents.FirstOrDefault(x => x.Id == log.EventId);
        var rule = db.AlertRules.FirstOrDefault(x => x.Id == log.RuleId);

        var body = new
        {
            eventId = log.EventId,
            eventNo = evt?.EventNo,
            ruleId = log.RuleId,
            ruleName = rule?.RuleName,
            channel = log.ChannelType,
            severity = evt?.SeverityLevel,
            content = log.SendContent,
            triggerTime = evt?.TriggerTime,
            sentAt = DateTime.UtcNow
        };
        return System.Text.Json.JsonSerializer.Serialize(body);
    }

    private static void MarkFailed(Domain.Entities.AlertNotificationLog log, string reason)
    {
        log.RetryCount += 1;
        log.ResponseText = reason;
        log.SendStatus = log.RetryCount >= MaxRetryCount ? "failed" : "pending";
        log.UpdatedAt = DateTime.UtcNow;
    }

    private static string Truncate(string? text, int maxLen)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        return text.Length <= maxLen ? text : text[..maxLen];
    }
}
