using Bi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bi.Application.Services;

public interface IAlertSchemaInitializer
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
}

/// <summary>
/// 预警模块数据库初始化器（幂等执行）
/// </summary>
public class AlertSchemaInitializer : IAlertSchemaInitializer
{
    private readonly BiDbContext _db;
    private readonly ILogger<AlertSchemaInitializer> _logger;

    public AlertSchemaInitializer(BiDbContext db, ILogger<AlertSchemaInitializer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        var sqlList = new[]
        {
            """
            CREATE TABLE IF NOT EXISTS bi_alert_rule (
                id BIGSERIAL PRIMARY KEY,
                rule_code VARCHAR(50) NOT NULL UNIQUE,
                rule_name VARCHAR(200) NOT NULL,
                rule_type VARCHAR(30) NOT NULL DEFAULT 'threshold',
                severity_level VARCHAR(20) NOT NULL DEFAULT 'warning',
                rule_status VARCHAR(20) NOT NULL DEFAULT 'enabled',
                datasource_id BIGINT NULL,
                dataset_id BIGINT NULL,
                chart_id BIGINT NULL,
                kpi_id BIGINT NULL,
                metric_field VARCHAR(100) NULL,
                dimension_field VARCHAR(100) NULL,
                time_field VARCHAR(100) NULL,
                stat_granularity VARCHAR(20) NOT NULL DEFAULT 'day',
                condition_json JSONB NOT NULL DEFAULT '{}'::jsonb,
                calc_sql TEXT NULL,
                schedule_type VARCHAR(20) NOT NULL DEFAULT 'interval',
                cron_expr VARCHAR(100) NULL,
                interval_seconds INT NOT NULL DEFAULT 300,
                timezone VARCHAR(50) NOT NULL DEFAULT 'Asia/Shanghai',
                dedup_minutes INT NOT NULL DEFAULT 60,
                cooldown_minutes INT NOT NULL DEFAULT 30,
                owner_user_id BIGINT NULL,
                notify_channels JSONB NOT NULL DEFAULT '[]'::jsonb,
                notify_template TEXT NULL,
                last_check_at TIMESTAMP NULL,
                next_check_at TIMESTAMP NULL,
                remark VARCHAR(500) NULL,
                created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS bi_alert_event (
                id BIGSERIAL PRIMARY KEY,
                event_no VARCHAR(64) NOT NULL UNIQUE,
                rule_id BIGINT NOT NULL,
                rule_snapshot_json JSONB NOT NULL DEFAULT '{}'::jsonb,
                event_status VARCHAR(20) NOT NULL DEFAULT 'open',
                severity_level VARCHAR(20) NOT NULL,
                trigger_time TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                first_triggered_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                last_triggered_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                trigger_count INT NOT NULL DEFAULT 1,
                current_value NUMERIC(20,4) NULL,
                baseline_value NUMERIC(20,4) NULL,
                compare_value NUMERIC(20,4) NULL,
                change_pct NUMERIC(10,4) NULL,
                threshold_desc VARCHAR(500) NULL,
                dimension_value_json JSONB NOT NULL DEFAULT '{}'::jsonb,
                evidence_json JSONB NOT NULL DEFAULT '{}'::jsonb,
                suggestion_text TEXT NULL,
                ack_by BIGINT NULL,
                ack_at TIMESTAMP NULL,
                resolved_by BIGINT NULL,
                resolved_at TIMESTAMP NULL,
                resolution_note VARCHAR(1000) NULL,
                is_notified BOOLEAN NOT NULL DEFAULT FALSE,
                notified_at TIMESTAMP NULL,
                created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS bi_alert_event_action (
                id BIGSERIAL PRIMARY KEY,
                event_id BIGINT NOT NULL,
                action_type VARCHAR(30) NOT NULL,
                action_user_id BIGINT NULL,
                action_note VARCHAR(1000) NULL,
                action_payload JSONB NOT NULL DEFAULT '{}'::jsonb,
                created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS bi_alert_subscription (
                id BIGSERIAL PRIMARY KEY,
                rule_id BIGINT NULL,
                subscriber_type VARCHAR(20) NOT NULL,
                subscriber_id BIGINT NULL,
                channel_type VARCHAR(20) NOT NULL,
                channel_target VARCHAR(200) NULL,
                severity_filter JSONB NOT NULL DEFAULT '[]'::jsonb,
                is_enabled BOOLEAN NOT NULL DEFAULT TRUE,
                created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS bi_alert_notification_log (
                id BIGSERIAL PRIMARY KEY,
                event_id BIGINT NOT NULL,
                rule_id BIGINT NOT NULL,
                subscription_id BIGINT NULL,
                channel_type VARCHAR(20) NOT NULL,
                send_to VARCHAR(200) NULL,
                send_status VARCHAR(20) NOT NULL DEFAULT 'pending',
                send_content TEXT NULL,
                response_text TEXT NULL,
                retry_count INT NOT NULL DEFAULT 0,
                sent_at TIMESTAMP NULL,
                created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS bi_alert_metric_snapshot (
                id BIGSERIAL PRIMARY KEY,
                rule_id BIGINT NOT NULL,
                snapshot_time TIMESTAMP NOT NULL,
                current_value NUMERIC(20,4) NULL,
                baseline_value NUMERIC(20,4) NULL,
                compare_value NUMERIC(20,4) NULL,
                change_pct NUMERIC(10,4) NULL,
                dimension_value_json JSONB NOT NULL DEFAULT '{}'::jsonb,
                calc_context_json JSONB NOT NULL DEFAULT '{}'::jsonb,
                created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            """,

            // 外键约束（存在即忽略）
            """
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bi_alert_rule_datasource') THEN
                    ALTER TABLE bi_alert_rule ADD CONSTRAINT fk_bi_alert_rule_datasource FOREIGN KEY (datasource_id) REFERENCES bi_datasource(id);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bi_alert_rule_dataset') THEN
                    ALTER TABLE bi_alert_rule ADD CONSTRAINT fk_bi_alert_rule_dataset FOREIGN KEY (dataset_id) REFERENCES bi_dataset(id);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bi_alert_rule_chart') THEN
                    ALTER TABLE bi_alert_rule ADD CONSTRAINT fk_bi_alert_rule_chart FOREIGN KEY (chart_id) REFERENCES bi_chart(id);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bi_alert_rule_kpi') THEN
                    ALTER TABLE bi_alert_rule ADD CONSTRAINT fk_bi_alert_rule_kpi FOREIGN KEY (kpi_id) REFERENCES bi_kpi_definition(id);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bi_alert_rule_owner') THEN
                    ALTER TABLE bi_alert_rule ADD CONSTRAINT fk_bi_alert_rule_owner FOREIGN KEY (owner_user_id) REFERENCES sys_user(id);
                END IF;

                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bi_alert_event_rule') THEN
                    ALTER TABLE bi_alert_event ADD CONSTRAINT fk_bi_alert_event_rule FOREIGN KEY (rule_id) REFERENCES bi_alert_rule(id) ON DELETE CASCADE;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bi_alert_event_ack_user') THEN
                    ALTER TABLE bi_alert_event ADD CONSTRAINT fk_bi_alert_event_ack_user FOREIGN KEY (ack_by) REFERENCES sys_user(id);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bi_alert_event_resolve_user') THEN
                    ALTER TABLE bi_alert_event ADD CONSTRAINT fk_bi_alert_event_resolve_user FOREIGN KEY (resolved_by) REFERENCES sys_user(id);
                END IF;

                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bi_alert_event_action_event') THEN
                    ALTER TABLE bi_alert_event_action ADD CONSTRAINT fk_bi_alert_event_action_event FOREIGN KEY (event_id) REFERENCES bi_alert_event(id) ON DELETE CASCADE;
                END IF;

                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bi_alert_subscription_rule') THEN
                    ALTER TABLE bi_alert_subscription ADD CONSTRAINT fk_bi_alert_subscription_rule FOREIGN KEY (rule_id) REFERENCES bi_alert_rule(id) ON DELETE CASCADE;
                END IF;

                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bi_alert_notification_event') THEN
                    ALTER TABLE bi_alert_notification_log ADD CONSTRAINT fk_bi_alert_notification_event FOREIGN KEY (event_id) REFERENCES bi_alert_event(id) ON DELETE CASCADE;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bi_alert_notification_rule') THEN
                    ALTER TABLE bi_alert_notification_log ADD CONSTRAINT fk_bi_alert_notification_rule FOREIGN KEY (rule_id) REFERENCES bi_alert_rule(id);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bi_alert_notification_subscription') THEN
                    ALTER TABLE bi_alert_notification_log ADD CONSTRAINT fk_bi_alert_notification_subscription FOREIGN KEY (subscription_id) REFERENCES bi_alert_subscription(id);
                END IF;

                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bi_alert_metric_snapshot_rule') THEN
                    ALTER TABLE bi_alert_metric_snapshot ADD CONSTRAINT fk_bi_alert_metric_snapshot_rule FOREIGN KEY (rule_id) REFERENCES bi_alert_rule(id) ON DELETE CASCADE;
                END IF;
            END $$;
            """,

            // 索引
            "CREATE INDEX IF NOT EXISTS idx_bi_alert_rule_status ON bi_alert_rule(rule_status);",
            "CREATE INDEX IF NOT EXISTS idx_bi_alert_rule_next_check ON bi_alert_rule(next_check_at);",
            "CREATE INDEX IF NOT EXISTS idx_bi_alert_event_rule ON bi_alert_event(rule_id);",
            "CREATE INDEX IF NOT EXISTS idx_bi_alert_event_status ON bi_alert_event(event_status);",
            "CREATE INDEX IF NOT EXISTS idx_bi_alert_event_trigger_time ON bi_alert_event(trigger_time DESC);",
            "CREATE INDEX IF NOT EXISTS idx_bi_alert_event_action_event ON bi_alert_event_action(event_id);",
            "CREATE INDEX IF NOT EXISTS idx_bi_alert_subscription_rule ON bi_alert_subscription(rule_id);",
            "CREATE INDEX IF NOT EXISTS idx_bi_alert_notification_event ON bi_alert_notification_log(event_id);",
            "CREATE INDEX IF NOT EXISTS idx_bi_alert_notification_status ON bi_alert_notification_log(send_status);",
            "CREATE INDEX IF NOT EXISTS idx_bi_alert_metric_snapshot_rule_time ON bi_alert_metric_snapshot(rule_id, snapshot_time DESC);"
        };

        foreach (var sql in sqlList)
        {
            // ExecuteSqlRawAsync内部会做string.Format，SQL中的{}需要先转义
            var escapedSql = sql.Replace("{", "{{").Replace("}", "}}");
            await _db.Database.ExecuteSqlRawAsync(escapedSql, Array.Empty<object>(), ct);
        }

        _logger.LogInformation("预警模块数据库对象检查完成");
    }
}
