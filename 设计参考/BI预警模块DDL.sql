-- BI预警模块 DDL（PostgreSQL）
-- 说明：表名、字段名采用现有系统 snake_case 风格，全部附中文备注

-- 1) 预警规则主表
CREATE TABLE IF NOT EXISTS bi_alert_rule (
    id                  BIGSERIAL PRIMARY KEY,
    rule_code           VARCHAR(50) NOT NULL,
    rule_name           VARCHAR(200) NOT NULL,
    rule_type           VARCHAR(30) NOT NULL DEFAULT 'threshold',
    severity_level      VARCHAR(20) NOT NULL DEFAULT 'warning',
    rule_status         VARCHAR(20) NOT NULL DEFAULT 'enabled',
    datasource_id       BIGINT NULL,
    dataset_id          BIGINT NULL,
    chart_id            BIGINT NULL,
    kpi_id              BIGINT NULL,
    metric_field        VARCHAR(100) NULL,
    dimension_field     VARCHAR(100) NULL,
    time_field          VARCHAR(100) NULL,
    stat_granularity    VARCHAR(20) NOT NULL DEFAULT 'day',
    condition_json      JSONB NOT NULL DEFAULT '{}'::jsonb,
    calc_sql            TEXT NULL,
    schedule_type       VARCHAR(20) NOT NULL DEFAULT 'interval',
    cron_expr           VARCHAR(100) NULL,
    interval_seconds    INT NOT NULL DEFAULT 300,
    timezone            VARCHAR(50) NOT NULL DEFAULT 'Asia/Shanghai',
    dedup_minutes       INT NOT NULL DEFAULT 60,
    cooldown_minutes    INT NOT NULL DEFAULT 30,
    owner_user_id       BIGINT NULL,
    notify_channels     JSONB NOT NULL DEFAULT '[]'::jsonb,
    notify_template     TEXT NULL,
    last_check_at       TIMESTAMP NULL,
    next_check_at       TIMESTAMP NULL,
    remark              VARCHAR(500) NULL,
    created_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uk_bi_alert_rule_code UNIQUE (rule_code),
    CONSTRAINT fk_bi_alert_rule_datasource FOREIGN KEY (datasource_id) REFERENCES bi_datasource(id),
    CONSTRAINT fk_bi_alert_rule_dataset FOREIGN KEY (dataset_id) REFERENCES bi_dataset(id),
    CONSTRAINT fk_bi_alert_rule_chart FOREIGN KEY (chart_id) REFERENCES bi_chart(id),
    CONSTRAINT fk_bi_alert_rule_kpi FOREIGN KEY (kpi_id) REFERENCES bi_kpi_definition(id),
    CONSTRAINT fk_bi_alert_rule_owner FOREIGN KEY (owner_user_id) REFERENCES sys_user(id)
);

COMMENT ON TABLE bi_alert_rule IS '预警规则主表';
COMMENT ON COLUMN bi_alert_rule.id IS '主键ID';
COMMENT ON COLUMN bi_alert_rule.rule_code IS '规则编码（唯一）';
COMMENT ON COLUMN bi_alert_rule.rule_name IS '规则名称';
COMMENT ON COLUMN bi_alert_rule.rule_type IS '规则类型：threshold/mom_change/yoy_change/continuous/custom_sql';
COMMENT ON COLUMN bi_alert_rule.severity_level IS '默认严重级别：info/warning/critical/emergency';
COMMENT ON COLUMN bi_alert_rule.rule_status IS '规则状态：enabled/disabled';
COMMENT ON COLUMN bi_alert_rule.datasource_id IS '关联数据源ID';
COMMENT ON COLUMN bi_alert_rule.dataset_id IS '关联数据集ID';
COMMENT ON COLUMN bi_alert_rule.chart_id IS '关联图表ID';
COMMENT ON COLUMN bi_alert_rule.kpi_id IS '关联指标定义ID';
COMMENT ON COLUMN bi_alert_rule.metric_field IS '指标字段名（用于计算当前值）';
COMMENT ON COLUMN bi_alert_rule.dimension_field IS '维度字段名（用于分组预警）';
COMMENT ON COLUMN bi_alert_rule.time_field IS '时间字段名';
COMMENT ON COLUMN bi_alert_rule.stat_granularity IS '统计粒度：minute/hour/day/week/month';
COMMENT ON COLUMN bi_alert_rule.condition_json IS '触发条件JSON（阈值、波动率、连续次数等）';
COMMENT ON COLUMN bi_alert_rule.calc_sql IS '计算SQL（custom_sql规则必填）';
COMMENT ON COLUMN bi_alert_rule.schedule_type IS '调度类型：interval/cron';
COMMENT ON COLUMN bi_alert_rule.cron_expr IS 'Cron表达式（schedule_type=cron时使用）';
COMMENT ON COLUMN bi_alert_rule.interval_seconds IS '执行间隔秒数（schedule_type=interval时使用）';
COMMENT ON COLUMN bi_alert_rule.timezone IS '规则执行时区';
COMMENT ON COLUMN bi_alert_rule.dedup_minutes IS '去重窗口（分钟），同规则同维度不重复发事件';
COMMENT ON COLUMN bi_alert_rule.cooldown_minutes IS '冷却时间（分钟），命中后抑制重复通知';
COMMENT ON COLUMN bi_alert_rule.owner_user_id IS '规则负责人用户ID';
COMMENT ON COLUMN bi_alert_rule.notify_channels IS '通知渠道JSON数组（inapp/wecom/sms/email/webhook）';
COMMENT ON COLUMN bi_alert_rule.notify_template IS '通知模板内容';
COMMENT ON COLUMN bi_alert_rule.last_check_at IS '上次检测时间';
COMMENT ON COLUMN bi_alert_rule.next_check_at IS '下次检测时间';
COMMENT ON COLUMN bi_alert_rule.remark IS '备注';
COMMENT ON COLUMN bi_alert_rule.created_at IS '创建时间';
COMMENT ON COLUMN bi_alert_rule.updated_at IS '更新时间';

CREATE INDEX IF NOT EXISTS idx_bi_alert_rule_status ON bi_alert_rule(rule_status);
CREATE INDEX IF NOT EXISTS idx_bi_alert_rule_next_check ON bi_alert_rule(next_check_at);
CREATE INDEX IF NOT EXISTS idx_bi_alert_rule_owner ON bi_alert_rule(owner_user_id);


-- 2) 预警事件表
CREATE TABLE IF NOT EXISTS bi_alert_event (
    id                      BIGSERIAL PRIMARY KEY,
    event_no                VARCHAR(64) NOT NULL,
    rule_id                 BIGINT NOT NULL,
    rule_snapshot_json      JSONB NOT NULL DEFAULT '{}'::jsonb,
    event_status            VARCHAR(20) NOT NULL DEFAULT 'open',
    severity_level          VARCHAR(20) NOT NULL,
    trigger_time            TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    first_triggered_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_triggered_at       TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    trigger_count           INT NOT NULL DEFAULT 1,
    current_value           NUMERIC(20,4) NULL,
    baseline_value          NUMERIC(20,4) NULL,
    threshold_desc          VARCHAR(500) NULL,
    compare_value           NUMERIC(20,4) NULL,
    change_pct              NUMERIC(10,4) NULL,
    dimension_value_json    JSONB NOT NULL DEFAULT '{}'::jsonb,
    evidence_json           JSONB NOT NULL DEFAULT '{}'::jsonb,
    suggestion_text         TEXT NULL,
    ack_by                  BIGINT NULL,
    ack_at                  TIMESTAMP NULL,
    resolved_by             BIGINT NULL,
    resolved_at             TIMESTAMP NULL,
    resolution_note         VARCHAR(1000) NULL,
    is_notified             BOOLEAN NOT NULL DEFAULT FALSE,
    notified_at             TIMESTAMP NULL,
    created_at              TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at              TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uk_bi_alert_event_no UNIQUE (event_no),
    CONSTRAINT fk_bi_alert_event_rule FOREIGN KEY (rule_id) REFERENCES bi_alert_rule(id),
    CONSTRAINT fk_bi_alert_event_ack_user FOREIGN KEY (ack_by) REFERENCES sys_user(id),
    CONSTRAINT fk_bi_alert_event_resolve_user FOREIGN KEY (resolved_by) REFERENCES sys_user(id)
);

COMMENT ON TABLE bi_alert_event IS '预警事件表';
COMMENT ON COLUMN bi_alert_event.id IS '主键ID';
COMMENT ON COLUMN bi_alert_event.event_no IS '事件编号（唯一）';
COMMENT ON COLUMN bi_alert_event.rule_id IS '关联规则ID';
COMMENT ON COLUMN bi_alert_event.rule_snapshot_json IS '规则快照JSON（触发时保存，便于追溯）';
COMMENT ON COLUMN bi_alert_event.event_status IS '事件状态：open/acknowledged/resolved/ignored/closed';
COMMENT ON COLUMN bi_alert_event.severity_level IS '事件严重级别：info/warning/critical/emergency';
COMMENT ON COLUMN bi_alert_event.trigger_time IS '本次触发时间';
COMMENT ON COLUMN bi_alert_event.first_triggered_at IS '首次触发时间';
COMMENT ON COLUMN bi_alert_event.last_triggered_at IS '最近触发时间';
COMMENT ON COLUMN bi_alert_event.trigger_count IS '累计触发次数';
COMMENT ON COLUMN bi_alert_event.current_value IS '当前指标值';
COMMENT ON COLUMN bi_alert_event.baseline_value IS '基线值（同比/环比或历史均值）';
COMMENT ON COLUMN bi_alert_event.threshold_desc IS '触发阈值描述';
COMMENT ON COLUMN bi_alert_event.compare_value IS '对比值（上期/去年同期）';
COMMENT ON COLUMN bi_alert_event.change_pct IS '变化百分比（小数，如0.2356）';
COMMENT ON COLUMN bi_alert_event.dimension_value_json IS '维度值JSON（如科室/病区）';
COMMENT ON COLUMN bi_alert_event.evidence_json IS '证据数据JSON（TopN明细、SQL结果摘要）';
COMMENT ON COLUMN bi_alert_event.suggestion_text IS '处置建议文本';
COMMENT ON COLUMN bi_alert_event.ack_by IS '确认人用户ID';
COMMENT ON COLUMN bi_alert_event.ack_at IS '确认时间';
COMMENT ON COLUMN bi_alert_event.resolved_by IS '解决人用户ID';
COMMENT ON COLUMN bi_alert_event.resolved_at IS '解决时间';
COMMENT ON COLUMN bi_alert_event.resolution_note IS '处理说明';
COMMENT ON COLUMN bi_alert_event.is_notified IS '是否已通知';
COMMENT ON COLUMN bi_alert_event.notified_at IS '通知完成时间';
COMMENT ON COLUMN bi_alert_event.created_at IS '创建时间';
COMMENT ON COLUMN bi_alert_event.updated_at IS '更新时间';

CREATE INDEX IF NOT EXISTS idx_bi_alert_event_rule ON bi_alert_event(rule_id);
CREATE INDEX IF NOT EXISTS idx_bi_alert_event_status ON bi_alert_event(event_status);
CREATE INDEX IF NOT EXISTS idx_bi_alert_event_trigger_time ON bi_alert_event(trigger_time DESC);
CREATE INDEX IF NOT EXISTS idx_bi_alert_event_severity ON bi_alert_event(severity_level);


-- 3) 事件处置动作表
CREATE TABLE IF NOT EXISTS bi_alert_event_action (
    id                  BIGSERIAL PRIMARY KEY,
    event_id            BIGINT NOT NULL,
    action_type         VARCHAR(30) NOT NULL,
    action_user_id      BIGINT NULL,
    action_note         VARCHAR(1000) NULL,
    action_payload      JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_bi_alert_event_action_event FOREIGN KEY (event_id) REFERENCES bi_alert_event(id) ON DELETE CASCADE,
    CONSTRAINT fk_bi_alert_event_action_user FOREIGN KEY (action_user_id) REFERENCES sys_user(id)
);

COMMENT ON TABLE bi_alert_event_action IS '预警事件处置动作表';
COMMENT ON COLUMN bi_alert_event_action.id IS '主键ID';
COMMENT ON COLUMN bi_alert_event_action.event_id IS '关联事件ID';
COMMENT ON COLUMN bi_alert_event_action.action_type IS '动作类型：ack/assign/comment/resolve/reopen/ignore/close';
COMMENT ON COLUMN bi_alert_event_action.action_user_id IS '动作执行人用户ID';
COMMENT ON COLUMN bi_alert_event_action.action_note IS '动作说明';
COMMENT ON COLUMN bi_alert_event_action.action_payload IS '动作扩展数据JSON';
COMMENT ON COLUMN bi_alert_event_action.created_at IS '创建时间';
COMMENT ON COLUMN bi_alert_event_action.updated_at IS '更新时间';

CREATE INDEX IF NOT EXISTS idx_bi_alert_event_action_event ON bi_alert_event_action(event_id);
CREATE INDEX IF NOT EXISTS idx_bi_alert_event_action_created ON bi_alert_event_action(created_at DESC);


-- 4) 预警订阅表
CREATE TABLE IF NOT EXISTS bi_alert_subscription (
    id                  BIGSERIAL PRIMARY KEY,
    rule_id             BIGINT NULL,
    subscriber_type     VARCHAR(20) NOT NULL,
    subscriber_id       BIGINT NULL,
    channel_type        VARCHAR(20) NOT NULL,
    channel_target      VARCHAR(200) NULL,
    severity_filter     JSONB NOT NULL DEFAULT '[]'::jsonb,
    is_enabled          BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_bi_alert_subscription_rule FOREIGN KEY (rule_id) REFERENCES bi_alert_rule(id) ON DELETE CASCADE
);

COMMENT ON TABLE bi_alert_subscription IS '预警订阅表';
COMMENT ON COLUMN bi_alert_subscription.id IS '主键ID';
COMMENT ON COLUMN bi_alert_subscription.rule_id IS '关联规则ID，为空表示全局订阅';
COMMENT ON COLUMN bi_alert_subscription.subscriber_type IS '订阅对象类型：user/role/webhook';
COMMENT ON COLUMN bi_alert_subscription.subscriber_id IS '订阅对象ID（用户ID或角色ID）';
COMMENT ON COLUMN bi_alert_subscription.channel_type IS '通知渠道：inapp/wecom/sms/email/webhook';
COMMENT ON COLUMN bi_alert_subscription.channel_target IS '通知目标（邮箱/手机号/Webhook地址等）';
COMMENT ON COLUMN bi_alert_subscription.severity_filter IS '级别过滤JSON数组';
COMMENT ON COLUMN bi_alert_subscription.is_enabled IS '是否启用';
COMMENT ON COLUMN bi_alert_subscription.created_at IS '创建时间';
COMMENT ON COLUMN bi_alert_subscription.updated_at IS '更新时间';

CREATE INDEX IF NOT EXISTS idx_bi_alert_subscription_rule ON bi_alert_subscription(rule_id);
CREATE INDEX IF NOT EXISTS idx_bi_alert_subscription_channel ON bi_alert_subscription(channel_type);


-- 5) 通知发送日志表
CREATE TABLE IF NOT EXISTS bi_alert_notification_log (
    id                  BIGSERIAL PRIMARY KEY,
    event_id            BIGINT NOT NULL,
    rule_id             BIGINT NOT NULL,
    subscription_id     BIGINT NULL,
    channel_type        VARCHAR(20) NOT NULL,
    send_to             VARCHAR(200) NULL,
    send_status         VARCHAR(20) NOT NULL DEFAULT 'pending',
    send_content        TEXT NULL,
    response_text       TEXT NULL,
    retry_count         INT NOT NULL DEFAULT 0,
    sent_at             TIMESTAMP NULL,
    created_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_bi_alert_notification_event FOREIGN KEY (event_id) REFERENCES bi_alert_event(id) ON DELETE CASCADE,
    CONSTRAINT fk_bi_alert_notification_rule FOREIGN KEY (rule_id) REFERENCES bi_alert_rule(id),
    CONSTRAINT fk_bi_alert_notification_subscription FOREIGN KEY (subscription_id) REFERENCES bi_alert_subscription(id)
);

COMMENT ON TABLE bi_alert_notification_log IS '预警通知发送日志表';
COMMENT ON COLUMN bi_alert_notification_log.id IS '主键ID';
COMMENT ON COLUMN bi_alert_notification_log.event_id IS '关联事件ID';
COMMENT ON COLUMN bi_alert_notification_log.rule_id IS '关联规则ID';
COMMENT ON COLUMN bi_alert_notification_log.subscription_id IS '关联订阅ID';
COMMENT ON COLUMN bi_alert_notification_log.channel_type IS '通知渠道：inapp/wecom/sms/email/webhook';
COMMENT ON COLUMN bi_alert_notification_log.send_to IS '发送目标';
COMMENT ON COLUMN bi_alert_notification_log.send_status IS '发送状态：pending/success/failed';
COMMENT ON COLUMN bi_alert_notification_log.send_content IS '发送内容';
COMMENT ON COLUMN bi_alert_notification_log.response_text IS '通道响应内容';
COMMENT ON COLUMN bi_alert_notification_log.retry_count IS '重试次数';
COMMENT ON COLUMN bi_alert_notification_log.sent_at IS '发送时间';
COMMENT ON COLUMN bi_alert_notification_log.created_at IS '创建时间';
COMMENT ON COLUMN bi_alert_notification_log.updated_at IS '更新时间';

CREATE INDEX IF NOT EXISTS idx_bi_alert_notification_event ON bi_alert_notification_log(event_id);
CREATE INDEX IF NOT EXISTS idx_bi_alert_notification_status ON bi_alert_notification_log(send_status);
CREATE INDEX IF NOT EXISTS idx_bi_alert_notification_sent_at ON bi_alert_notification_log(sent_at DESC);


-- 6) 指标快照表（用于趋势追踪与后续预测扩展）
CREATE TABLE IF NOT EXISTS bi_alert_metric_snapshot (
    id                      BIGSERIAL PRIMARY KEY,
    rule_id                 BIGINT NOT NULL,
    snapshot_time           TIMESTAMP NOT NULL,
    current_value           NUMERIC(20,4) NULL,
    baseline_value          NUMERIC(20,4) NULL,
    compare_value           NUMERIC(20,4) NULL,
    change_pct              NUMERIC(10,4) NULL,
    dimension_value_json    JSONB NOT NULL DEFAULT '{}'::jsonb,
    calc_context_json       JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at              TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at              TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_bi_alert_metric_snapshot_rule FOREIGN KEY (rule_id) REFERENCES bi_alert_rule(id) ON DELETE CASCADE
);

COMMENT ON TABLE bi_alert_metric_snapshot IS '预警指标快照表';
COMMENT ON COLUMN bi_alert_metric_snapshot.id IS '主键ID';
COMMENT ON COLUMN bi_alert_metric_snapshot.rule_id IS '关联规则ID';
COMMENT ON COLUMN bi_alert_metric_snapshot.snapshot_time IS '快照时间';
COMMENT ON COLUMN bi_alert_metric_snapshot.current_value IS '当前值';
COMMENT ON COLUMN bi_alert_metric_snapshot.baseline_value IS '基线值';
COMMENT ON COLUMN bi_alert_metric_snapshot.compare_value IS '对比值';
COMMENT ON COLUMN bi_alert_metric_snapshot.change_pct IS '变化百分比（小数）';
COMMENT ON COLUMN bi_alert_metric_snapshot.dimension_value_json IS '维度值JSON';
COMMENT ON COLUMN bi_alert_metric_snapshot.calc_context_json IS '计算上下文JSON（SQL、参数、耗时等）';
COMMENT ON COLUMN bi_alert_metric_snapshot.created_at IS '创建时间';
COMMENT ON COLUMN bi_alert_metric_snapshot.updated_at IS '更新时间';

CREATE INDEX IF NOT EXISTS idx_bi_alert_metric_snapshot_rule_time
    ON bi_alert_metric_snapshot(rule_id, snapshot_time DESC);

