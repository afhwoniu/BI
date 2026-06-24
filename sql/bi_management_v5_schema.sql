-- ============================================================
-- 智能BI可视化系统V5 - 数据库建表脚本 (PostgreSQL)
-- 数据库: bi_management_v5
-- 自动导出时间: 2026-06-24 11:17:08
-- 说明: 含表中文备注、字段中文备注、主键、索引、外键约束
-- 备注: 字段中文注释来源于实体类 XML 注释 + 常用列名词库推断
-- ============================================================

CREATE EXTENSION IF NOT EXISTS "pgcrypto";  -- 用于 gen_random_uuid()

-- ------------------------------------------------------------
-- 表: __EFMigrationsHistory
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "__EFMigrationsHistory" CASCADE;
CREATE TABLE "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "pk___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);


CREATE UNIQUE INDEX "PK___EFMigrationsHistory" ON public."__EFMigrationsHistory" USING btree ("MigrationId");

-- ------------------------------------------------------------
-- 表: bi_ai_favorite  备注: AI查询收藏表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_ai_favorite" CASCADE;
CREATE TABLE "bi_ai_favorite" (
    "id" bigint NOT NULL,
    "user_id" bigint NOT NULL,
    "title" character varying(200) NOT NULL,
    "question" text NOT NULL,
    "sql" text,
    "chart_type" character varying(50),
    "chart_config" text,
    "datasource_id" bigint,
    "remark" character varying(500),
    "created_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_ai_favorite" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_ai_favorite" IS 'AI查询收藏表';
COMMENT ON COLUMN "bi_ai_favorite"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_ai_favorite"."user_id" IS '用户ID';
COMMENT ON COLUMN "bi_ai_favorite"."title" IS '收藏标题';
COMMENT ON COLUMN "bi_ai_favorite"."question" IS '原始问题';
COMMENT ON COLUMN "bi_ai_favorite"."sql" IS 'SQL语句';
COMMENT ON COLUMN "bi_ai_favorite"."chart_type" IS '图表类型';
COMMENT ON COLUMN "bi_ai_favorite"."chart_config" IS '图表配置JSON';
COMMENT ON COLUMN "bi_ai_favorite"."datasource_id" IS '关联的数据源ID';
COMMENT ON COLUMN "bi_ai_favorite"."remark" IS '备注';
COMMENT ON COLUMN "bi_ai_favorite"."created_at" IS '创建时间';

CREATE UNIQUE INDEX "PK_bi_ai_favorite" ON public.bi_ai_favorite USING btree (id);
CREATE INDEX "IX_bi_ai_favorite_datasource_id" ON public.bi_ai_favorite USING btree (datasource_id);
CREATE INDEX "IX_bi_ai_favorite_user_id" ON public.bi_ai_favorite USING btree (user_id);

-- ------------------------------------------------------------
-- 表: bi_ai_message  备注: AI消息表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_ai_message" CASCADE;
CREATE TABLE "bi_ai_message" (
    "id" bigint NOT NULL,
    "session_id" bigint NOT NULL,
    "role" character varying(20) NOT NULL,
    "content" text NOT NULL,
    "sql" text,
    "chart_type" character varying(50),
    "chart_config" text,
    "tokens_used" integer NOT NULL,
    "created_at" timestamp with time zone NOT NULL,
    "detail_sql" text,
    "dimension_fields" text,
    "hospital_field" character varying(100),
    "kpi_config" text,
    "measure_fields" text,
    "prompt_text" text,
    "default_charts_config" text,
    "mode" character varying(20),
    "ChartImages" text,
    "date_field" character varying(100),
    "prompts_json" text,
    CONSTRAINT "pk_bi_ai_message" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_ai_message" IS 'AI消息表';
COMMENT ON COLUMN "bi_ai_message"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_ai_message"."session_id" IS '所属会话ID';
COMMENT ON COLUMN "bi_ai_message"."role" IS '消息角色（user/assistant）';
COMMENT ON COLUMN "bi_ai_message"."content" IS '消息内容';
COMMENT ON COLUMN "bi_ai_message"."sql" IS 'SQL语句';
COMMENT ON COLUMN "bi_ai_message"."chart_type" IS '图表类型';
COMMENT ON COLUMN "bi_ai_message"."chart_config" IS '图表配置JSON';
COMMENT ON COLUMN "bi_ai_message"."tokens_used" IS 'Token消耗';
COMMENT ON COLUMN "bi_ai_message"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_ai_message"."detail_sql" IS '明细SQL（核心！用于下钻聚合）';
COMMENT ON COLUMN "bi_ai_message"."dimension_fields" IS '可用维度字段JSON（如：["就诊日期","科室名称","医生"]）';
COMMENT ON COLUMN "bi_ai_message"."hospital_field" IS '医院字段名（用于医共体筛选）';
COMMENT ON COLUMN "bi_ai_message"."kpi_config" IS 'KPI配置JSON';
COMMENT ON COLUMN "bi_ai_message"."measure_fields" IS '可用度量字段JSON（如：[{"field":"费用","alias":"总费用","agg":"SUM"}]）';
COMMENT ON COLUMN "bi_ai_message"."prompt_text" IS '完整提示词（用于调试和复现，旧版兼容）';
COMMENT ON COLUMN "bi_ai_message"."default_charts_config" IS '原始图表配置JSON（用于刷新时恢复图表结构）';
COMMENT ON COLUMN "bi_ai_message"."mode" IS '对话模式：bi-指标统计, hz360-患者360, internetsearch-通用问答';
COMMENT ON COLUMN "bi_ai_message"."ChartImages" IS '图表截图路径JSON（如：["/uploads/charts/123_0.png", "/uploads/charts/123_1.png"]）';
COMMENT ON COLUMN "bi_ai_message"."date_field" IS '日期字段名（用于同比环比计算和时间参数替换）';
COMMENT ON COLUMN "bi_ai_message"."prompts_json" IS '分阶段提示词JSON（用于保存完整的prompts列表）';

CREATE UNIQUE INDEX "PK_bi_ai_message" ON public.bi_ai_message USING btree (id);
CREATE INDEX "IX_bi_ai_message_session_id" ON public.bi_ai_message USING btree (session_id);

-- ------------------------------------------------------------
-- 表: bi_ai_session  备注: AI会话表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_ai_session" CASCADE;
CREATE TABLE "bi_ai_session" (
    "id" bigint NOT NULL,
    "session_key" character varying(50) NOT NULL,
    "title" character varying(200),
    "datasource_id" bigint,
    "user_id" bigint NOT NULL,
    "created_at" timestamp with time zone NOT NULL,
    "last_active_at" timestamp with time zone NOT NULL,
    "mode" character varying(20) NOT NULL DEFAULT 'bi'::character varying,
    CONSTRAINT "pk_bi_ai_session" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_ai_session" IS 'AI会话表';
COMMENT ON COLUMN "bi_ai_session"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_ai_session"."session_key" IS '会话唯一标识（用于前端）';
COMMENT ON COLUMN "bi_ai_session"."title" IS '收藏标题';
COMMENT ON COLUMN "bi_ai_session"."datasource_id" IS '关联的数据源ID';
COMMENT ON COLUMN "bi_ai_session"."user_id" IS '用户ID';
COMMENT ON COLUMN "bi_ai_session"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_ai_session"."last_active_at" IS '最后活动时间';
COMMENT ON COLUMN "bi_ai_session"."mode" IS '对话模式：bi-指标统计, hz360-患者360, internetsearch-通用问答';

CREATE UNIQUE INDEX "PK_bi_ai_session" ON public.bi_ai_session USING btree (id);
CREATE INDEX "IX_bi_ai_session_datasource_id" ON public.bi_ai_session USING btree (datasource_id);
CREATE UNIQUE INDEX "IX_bi_ai_session_session_key" ON public.bi_ai_session USING btree (session_key);
CREATE INDEX "IX_bi_ai_session_user_id" ON public.bi_ai_session USING btree (user_id);

-- ------------------------------------------------------------
-- 表: bi_alert_event  备注: 规则编码（唯一） </summary> public string RuleCode { get; set; } = string.Empty; <summary> 规则名称 </summary> public string RuleName { get; set; } = string.Empty; <summary> 规则类型：threshold/mom_change/yoy_change/continuous/custom_sql </summary> public string RuleType { get; set; } = AlertRuleTypes.Threshold; <summary> 默认严重级别：info/warning/critical/emergency </summary> public string SeverityLevel { get; set; } = AlertSeverityLevels.Warning; <summary> 规则状态：enabled/disabled </summary> public string RuleStatus { get; set; } = AlertRuleStatuses.Enabled; <summary> 关联数据源ID </summary> public long? DatasourceId { get; set; } <summary> 关联数据集ID </summary> public long? DatasetId { get; set; } <summary> 关联图表ID </summary> public long? ChartId { get; set; } <summary> 关联指标定义ID </summary> public long? KpiId { get; set; } <summary> 指标字段名 </summary> public string? MetricField { get; set; } <summary> 维度字段名 </summary> public string? DimensionField { get; set; } <summary> 时间字段名 </summary> public string? TimeField { get; set; } <summary> 统计粒度：minute/hour/day/week/month </summary> public string StatGranularity { get; set; } = "day"; <summary> 触发条件JSON </summary> public string ConditionJson { get; set; } = "{}"; <summary> 计算SQL（自定义规则时使用） </summary> public string? CalcSql { get; set; } <summary> 调度类型：interval/cron </summary> public string ScheduleType { get; set; } = "interval"; <summary> Cron表达式 </summary> public string? CronExpr { get; set; } <summary> 执行间隔（秒） </summary> public int IntervalSeconds { get; set; } = 300; <summary> 执行时区 </summary> public string Timezone { get; set; } = "Asia/Shanghai"; <summary> 去重窗口（分钟） </summary> public int DedupMinutes { get; set; } = 60; <summary> 冷却时间（分钟） </summary> public int CooldownMinutes { get; set; } = 30; <summary> 规则负责人 </summary> public long? OwnerUserId { get; set; } <summary> 通知渠道JSON数组 </summary> public string NotifyChannels { get; set; } = "[]"; <summary> 通知模板 </summary> public string? NotifyTemplate { get; set; } <summary> 上次检测时间 </summary> public DateTime? LastCheckAt { get; set; } <summary> 下次检测时间 </summary> public DateTime? NextCheckAt { get; set; } <summary> 备注 </summary> public string? Remark { get; set; } public virtual Datasource? Datasource { get; set; } public virtual Dataset? Dataset { get; set; } public virtual Chart? Chart { get; set; } public virtual KpiDefinition? Kpi { get; set; } public virtual SysUser? OwnerUser { get; set; } public virtual ICollection<AlertEvent> Events { get; set; } = new List<AlertEvent>(); public virtual ICollection<AlertSubscription> Subscriptions { get; set; } = new List<AlertSubscription>(); public virtual ICollection<AlertMetricSnapshot> MetricSnapshots { get; set; } = new List<AlertMetricSnapshot>(); } <summary> 预警事件实体
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_alert_event" CASCADE;
CREATE TABLE "bi_alert_event" (
    "id" bigint NOT NULL DEFAULT nextval('bi_alert_event_id_seq'::regclass),
    "event_no" character varying(64) NOT NULL,
    "rule_id" bigint NOT NULL,
    "rule_snapshot_json" jsonb NOT NULL DEFAULT '{}'::jsonb,
    "event_status" character varying(20) NOT NULL DEFAULT 'open'::character varying,
    "severity_level" character varying(20) NOT NULL,
    "trigger_time" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "first_triggered_at" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "last_triggered_at" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "trigger_count" integer NOT NULL DEFAULT 1,
    "current_value" numeric(20,4),
    "baseline_value" numeric(20,4),
    "compare_value" numeric(20,4),
    "change_pct" numeric(10,4),
    "threshold_desc" character varying(500),
    "dimension_value_json" jsonb NOT NULL DEFAULT '{}'::jsonb,
    "evidence_json" jsonb NOT NULL DEFAULT '{}'::jsonb,
    "suggestion_text" text,
    "ack_by" bigint,
    "ack_at" timestamp without time zone,
    "resolved_by" bigint,
    "resolved_at" timestamp without time zone,
    "resolution_note" character varying(1000),
    "is_notified" boolean NOT NULL DEFAULT false,
    "notified_at" timestamp without time zone,
    "created_at" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "pk_bi_alert_event" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_alert_event" IS '规则编码（唯一） </summary> public string RuleCode { get; set; } = string.Empty; <summary> 规则名称 </summary> public string RuleName { get; set; } = string.Empty; <summary> 规则类型：threshold/mom_change/yoy_change/continuous/custom_sql </summary> public string RuleType { get; set; } = AlertRuleTypes.Threshold; <summary> 默认严重级别：info/warning/critical/emergency </summary> public string SeverityLevel { get; set; } = AlertSeverityLevels.Warning; <summary> 规则状态：enabled/disabled </summary> public string RuleStatus { get; set; } = AlertRuleStatuses.Enabled; <summary> 关联数据源ID </summary> public long? DatasourceId { get; set; } <summary> 关联数据集ID </summary> public long? DatasetId { get; set; } <summary> 关联图表ID </summary> public long? ChartId { get; set; } <summary> 关联指标定义ID </summary> public long? KpiId { get; set; } <summary> 指标字段名 </summary> public string? MetricField { get; set; } <summary> 维度字段名 </summary> public string? DimensionField { get; set; } <summary> 时间字段名 </summary> public string? TimeField { get; set; } <summary> 统计粒度：minute/hour/day/week/month </summary> public string StatGranularity { get; set; } = "day"; <summary> 触发条件JSON </summary> public string ConditionJson { get; set; } = "{}"; <summary> 计算SQL（自定义规则时使用） </summary> public string? CalcSql { get; set; } <summary> 调度类型：interval/cron </summary> public string ScheduleType { get; set; } = "interval"; <summary> Cron表达式 </summary> public string? CronExpr { get; set; } <summary> 执行间隔（秒） </summary> public int IntervalSeconds { get; set; } = 300; <summary> 执行时区 </summary> public string Timezone { get; set; } = "Asia/Shanghai"; <summary> 去重窗口（分钟） </summary> public int DedupMinutes { get; set; } = 60; <summary> 冷却时间（分钟） </summary> public int CooldownMinutes { get; set; } = 30; <summary> 规则负责人 </summary> public long? OwnerUserId { get; set; } <summary> 通知渠道JSON数组 </summary> public string NotifyChannels { get; set; } = "[]"; <summary> 通知模板 </summary> public string? NotifyTemplate { get; set; } <summary> 上次检测时间 </summary> public DateTime? LastCheckAt { get; set; } <summary> 下次检测时间 </summary> public DateTime? NextCheckAt { get; set; } <summary> 备注 </summary> public string? Remark { get; set; } public virtual Datasource? Datasource { get; set; } public virtual Dataset? Dataset { get; set; } public virtual Chart? Chart { get; set; } public virtual KpiDefinition? Kpi { get; set; } public virtual SysUser? OwnerUser { get; set; } public virtual ICollection<AlertEvent> Events { get; set; } = new List<AlertEvent>(); public virtual ICollection<AlertSubscription> Subscriptions { get; set; } = new List<AlertSubscription>(); public virtual ICollection<AlertMetricSnapshot> MetricSnapshots { get; set; } = new List<AlertMetricSnapshot>(); } <summary> 预警事件实体';
COMMENT ON COLUMN "bi_alert_event"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_alert_event"."event_no" IS '预警事件实体 </summary> public class AlertEvent : BaseEntity { <summary> 事件编号';
COMMENT ON COLUMN "bi_alert_event"."rule_id" IS '规则ID';
COMMENT ON COLUMN "bi_alert_event"."rule_snapshot_json" IS '规则快照JSON';
COMMENT ON COLUMN "bi_alert_event"."event_status" IS '事件状态：open/acknowledged/resolved/ignored/closed';
COMMENT ON COLUMN "bi_alert_event"."severity_level" IS '默认严重级别：info/warning/critical/emergency';
COMMENT ON COLUMN "bi_alert_event"."trigger_time" IS '本次触发时间';
COMMENT ON COLUMN "bi_alert_event"."first_triggered_at" IS '首次触发时间';
COMMENT ON COLUMN "bi_alert_event"."last_triggered_at" IS '最近触发时间';
COMMENT ON COLUMN "bi_alert_event"."trigger_count" IS '累计触发次数';
COMMENT ON COLUMN "bi_alert_event"."current_value" IS '当前值';
COMMENT ON COLUMN "bi_alert_event"."baseline_value" IS '基线值';
COMMENT ON COLUMN "bi_alert_event"."compare_value" IS '对比值';
COMMENT ON COLUMN "bi_alert_event"."change_pct" IS '变化百分比';
COMMENT ON COLUMN "bi_alert_event"."threshold_desc" IS '阈值说明';
COMMENT ON COLUMN "bi_alert_event"."dimension_value_json" IS '维度值JSON';
COMMENT ON COLUMN "bi_alert_event"."evidence_json" IS '证据JSON';
COMMENT ON COLUMN "bi_alert_event"."suggestion_text" IS '处置建议';
COMMENT ON COLUMN "bi_alert_event"."ack_by" IS '确认人';
COMMENT ON COLUMN "bi_alert_event"."ack_at" IS '确认时间';
COMMENT ON COLUMN "bi_alert_event"."resolved_by" IS '解决人';
COMMENT ON COLUMN "bi_alert_event"."resolved_at" IS '解决时间';
COMMENT ON COLUMN "bi_alert_event"."resolution_note" IS '处理说明';
COMMENT ON COLUMN "bi_alert_event"."is_notified" IS '是否已通知';
COMMENT ON COLUMN "bi_alert_event"."notified_at" IS '通知时间';
COMMENT ON COLUMN "bi_alert_event"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_alert_event"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX bi_alert_event_event_no_key ON public.bi_alert_event USING btree (event_no);
CREATE INDEX idx_bi_alert_event_rule ON public.bi_alert_event USING btree (rule_id);
CREATE INDEX idx_bi_alert_event_status ON public.bi_alert_event USING btree (event_status);
CREATE INDEX idx_bi_alert_event_trigger_time ON public.bi_alert_event USING btree (trigger_time DESC);

-- ------------------------------------------------------------
-- 表: bi_alert_event_action  备注: 事件编号 </summary> public string EventNo { get; set; } = string.Empty; <summary> 规则ID </summary> public long RuleId { get; set; } <summary> 规则快照JSON </summary> public string RuleSnapshotJson { get; set; } = "{}"; <summary> 事件状态：open/acknowledged/resolved/ignored/closed </summary> public string EventStatus { get; set; } = AlertEventStatuses.Open; <summary> 严重级别 </summary> public string SeverityLevel { get; set; } = AlertSeverityLevels.Warning; <summary> 本次触发时间 </summary> public DateTime TriggerTime { get; set; } = DateTime.UtcNow; <summary> 首次触发时间 </summary> public DateTime FirstTriggeredAt { get; set; } = DateTime.UtcNow; <summary> 最近触发时间 </summary> public DateTime LastTriggeredAt { get; set; } = DateTime.UtcNow; <summary> 累计触发次数 </summary> public int TriggerCount { get; set; } = 1; <summary> 当前值 </summary> public decimal? CurrentValue { get; set; } <summary> 基线值 </summary> public decimal? BaselineValue { get; set; } <summary> 对比值 </summary> public decimal? CompareValue { get; set; } <summary> 变化百分比 </summary> public decimal? ChangePct { get; set; } <summary> 阈值说明 </summary> public string? ThresholdDesc { get; set; } <summary> 维度值JSON </summary> public string DimensionValueJson { get; set; } = "{}"; <summary> 证据JSON </summary> public string EvidenceJson { get; set; } = "{}"; <summary> 处置建议 </summary> public string? SuggestionText { get; set; } <summary> 确认人 </summary> public long? AckBy { get; set; } <summary> 确认时间 </summary> public DateTime? AckAt { get; set; } <summary> 解决人 </summary> public long? ResolvedBy { get; set; } <summary> 解决时间 </summary> public DateTime? ResolvedAt { get; set; } <summary> 处理说明 </summary> public string? ResolutionNote { get; set; } <summary> 是否已通知 </summary> public bool IsNotified { get; set; } <summary> 通知时间 </summary> public DateTime? NotifiedAt { get; set; } public virtual AlertRule? Rule { get; set; } public virtual SysUser? AckUser { get; set; } public virtual SysUser? ResolvedUser { get; set; } public virtual ICollection<AlertEventAction> Actions { get; set; } = new List<AlertEventAction>(); public virtual ICollection<AlertNotificationLog> NotificationLogs { get; set; } = new List<AlertNotificationLog>(); } <summary> 预警事件动作实体
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_alert_event_action" CASCADE;
CREATE TABLE "bi_alert_event_action" (
    "id" bigint NOT NULL DEFAULT nextval('bi_alert_event_action_id_seq'::regclass),
    "event_id" bigint NOT NULL,
    "action_type" character varying(30) NOT NULL,
    "action_user_id" bigint,
    "action_note" character varying(1000),
    "action_payload" jsonb NOT NULL DEFAULT '{}'::jsonb,
    "created_at" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "pk_bi_alert_event_action" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_alert_event_action" IS '事件编号 </summary> public string EventNo { get; set; } = string.Empty; <summary> 规则ID </summary> public long RuleId { get; set; } <summary> 规则快照JSON </summary> public string RuleSnapshotJson { get; set; } = "{}"; <summary> 事件状态：open/acknowledged/resolved/ignored/closed </summary> public string EventStatus { get; set; } = AlertEventStatuses.Open; <summary> 严重级别 </summary> public string SeverityLevel { get; set; } = AlertSeverityLevels.Warning; <summary> 本次触发时间 </summary> public DateTime TriggerTime { get; set; } = DateTime.UtcNow; <summary> 首次触发时间 </summary> public DateTime FirstTriggeredAt { get; set; } = DateTime.UtcNow; <summary> 最近触发时间 </summary> public DateTime LastTriggeredAt { get; set; } = DateTime.UtcNow; <summary> 累计触发次数 </summary> public int TriggerCount { get; set; } = 1; <summary> 当前值 </summary> public decimal? CurrentValue { get; set; } <summary> 基线值 </summary> public decimal? BaselineValue { get; set; } <summary> 对比值 </summary> public decimal? CompareValue { get; set; } <summary> 变化百分比 </summary> public decimal? ChangePct { get; set; } <summary> 阈值说明 </summary> public string? ThresholdDesc { get; set; } <summary> 维度值JSON </summary> public string DimensionValueJson { get; set; } = "{}"; <summary> 证据JSON </summary> public string EvidenceJson { get; set; } = "{}"; <summary> 处置建议 </summary> public string? SuggestionText { get; set; } <summary> 确认人 </summary> public long? AckBy { get; set; } <summary> 确认时间 </summary> public DateTime? AckAt { get; set; } <summary> 解决人 </summary> public long? ResolvedBy { get; set; } <summary> 解决时间 </summary> public DateTime? ResolvedAt { get; set; } <summary> 处理说明 </summary> public string? ResolutionNote { get; set; } <summary> 是否已通知 </summary> public bool IsNotified { get; set; } <summary> 通知时间 </summary> public DateTime? NotifiedAt { get; set; } public virtual AlertRule? Rule { get; set; } public virtual SysUser? AckUser { get; set; } public virtual SysUser? ResolvedUser { get; set; } public virtual ICollection<AlertEventAction> Actions { get; set; } = new List<AlertEventAction>(); public virtual ICollection<AlertNotificationLog> NotificationLogs { get; set; } = new List<AlertNotificationLog>(); } <summary> 预警事件动作实体';
COMMENT ON COLUMN "bi_alert_event_action"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_alert_event_action"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_alert_event_action"."updated_at" IS '更新时间';

CREATE INDEX idx_bi_alert_event_action_event ON public.bi_alert_event_action USING btree (event_id);

-- ------------------------------------------------------------
-- 表: bi_alert_metric_snapshot  备注: 指标快照实体
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_alert_metric_snapshot" CASCADE;
CREATE TABLE "bi_alert_metric_snapshot" (
    "id" bigint NOT NULL DEFAULT nextval('bi_alert_metric_snapshot_id_seq'::regclass),
    "rule_id" bigint NOT NULL,
    "snapshot_time" timestamp without time zone NOT NULL,
    "current_value" numeric(20,4),
    "baseline_value" numeric(20,4),
    "compare_value" numeric(20,4),
    "change_pct" numeric(10,4),
    "dimension_value_json" jsonb NOT NULL DEFAULT '{}'::jsonb,
    "calc_context_json" jsonb NOT NULL DEFAULT '{}'::jsonb,
    "created_at" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "pk_bi_alert_metric_snapshot" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_alert_metric_snapshot" IS '指标快照实体';
COMMENT ON COLUMN "bi_alert_metric_snapshot"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_alert_metric_snapshot"."rule_id" IS '规则ID';
COMMENT ON COLUMN "bi_alert_metric_snapshot"."current_value" IS '当前值';
COMMENT ON COLUMN "bi_alert_metric_snapshot"."baseline_value" IS '基线值';
COMMENT ON COLUMN "bi_alert_metric_snapshot"."compare_value" IS '对比值';
COMMENT ON COLUMN "bi_alert_metric_snapshot"."change_pct" IS '变化百分比';
COMMENT ON COLUMN "bi_alert_metric_snapshot"."dimension_value_json" IS '维度值JSON';
COMMENT ON COLUMN "bi_alert_metric_snapshot"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_alert_metric_snapshot"."updated_at" IS '更新时间';

CREATE INDEX idx_bi_alert_metric_snapshot_rule_time ON public.bi_alert_metric_snapshot USING btree (rule_id, snapshot_time DESC);

-- ------------------------------------------------------------
-- 表: bi_alert_notification_log  备注: 通知发送日志实体
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_alert_notification_log" CASCADE;
CREATE TABLE "bi_alert_notification_log" (
    "id" bigint NOT NULL DEFAULT nextval('bi_alert_notification_log_id_seq'::regclass),
    "event_id" bigint NOT NULL,
    "rule_id" bigint NOT NULL,
    "subscription_id" bigint,
    "channel_type" character varying(20) NOT NULL,
    "send_to" character varying(200),
    "send_status" character varying(20) NOT NULL DEFAULT 'pending'::character varying,
    "send_content" text,
    "response_text" text,
    "retry_count" integer NOT NULL DEFAULT 0,
    "sent_at" timestamp without time zone,
    "created_at" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "pk_bi_alert_notification_log" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_alert_notification_log" IS '通知发送日志实体';
COMMENT ON COLUMN "bi_alert_notification_log"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_alert_notification_log"."rule_id" IS '规则ID';
COMMENT ON COLUMN "bi_alert_notification_log"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_alert_notification_log"."updated_at" IS '更新时间';

CREATE INDEX idx_bi_alert_notification_event ON public.bi_alert_notification_log USING btree (event_id);
CREATE INDEX idx_bi_alert_notification_status ON public.bi_alert_notification_log USING btree (send_status);

-- ------------------------------------------------------------
-- 表: bi_alert_rule  备注: 预警规则实体
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_alert_rule" CASCADE;
CREATE TABLE "bi_alert_rule" (
    "id" bigint NOT NULL DEFAULT nextval('bi_alert_rule_id_seq'::regclass),
    "rule_code" character varying(50) NOT NULL,
    "rule_name" character varying(200) NOT NULL,
    "rule_type" character varying(30) NOT NULL DEFAULT 'threshold'::character varying,
    "severity_level" character varying(20) NOT NULL DEFAULT 'warning'::character varying,
    "rule_status" character varying(20) NOT NULL DEFAULT 'enabled'::character varying,
    "datasource_id" bigint,
    "dataset_id" bigint,
    "chart_id" bigint,
    "kpi_id" bigint,
    "metric_field" character varying(100),
    "dimension_field" character varying(100),
    "time_field" character varying(100),
    "stat_granularity" character varying(20) NOT NULL DEFAULT 'day'::character varying,
    "condition_json" jsonb NOT NULL DEFAULT '{}'::jsonb,
    "calc_sql" text,
    "schedule_type" character varying(20) NOT NULL DEFAULT 'interval'::character varying,
    "cron_expr" character varying(100),
    "interval_seconds" integer NOT NULL DEFAULT 300,
    "timezone" character varying(50) NOT NULL DEFAULT 'Asia/Shanghai'::character varying,
    "dedup_minutes" integer NOT NULL DEFAULT 60,
    "cooldown_minutes" integer NOT NULL DEFAULT 30,
    "owner_user_id" bigint,
    "notify_channels" jsonb NOT NULL DEFAULT '[]'::jsonb,
    "notify_template" text,
    "last_check_at" timestamp without time zone,
    "next_check_at" timestamp without time zone,
    "remark" character varying(500),
    "created_at" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "pk_bi_alert_rule" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_alert_rule" IS '预警规则实体';
COMMENT ON COLUMN "bi_alert_rule"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_alert_rule"."rule_code" IS '预警规则实体 </summary> public class AlertRule : BaseEntity { <summary> 规则编码（唯一）';
COMMENT ON COLUMN "bi_alert_rule"."rule_name" IS '规则名称';
COMMENT ON COLUMN "bi_alert_rule"."rule_type" IS '规则类型：threshold/mom_change/yoy_change/continuous/custom_sql';
COMMENT ON COLUMN "bi_alert_rule"."severity_level" IS '默认严重级别：info/warning/critical/emergency';
COMMENT ON COLUMN "bi_alert_rule"."rule_status" IS '规则状态：enabled/disabled';
COMMENT ON COLUMN "bi_alert_rule"."datasource_id" IS '关联的数据源ID';
COMMENT ON COLUMN "bi_alert_rule"."dataset_id" IS '关联数据集ID';
COMMENT ON COLUMN "bi_alert_rule"."chart_id" IS '关联图表ID';
COMMENT ON COLUMN "bi_alert_rule"."kpi_id" IS '关联指标定义ID';
COMMENT ON COLUMN "bi_alert_rule"."metric_field" IS '指标字段名';
COMMENT ON COLUMN "bi_alert_rule"."dimension_field" IS '维度字段名';
COMMENT ON COLUMN "bi_alert_rule"."time_field" IS '时间字段名';
COMMENT ON COLUMN "bi_alert_rule"."stat_granularity" IS '统计粒度：minute/hour/day/week/month';
COMMENT ON COLUMN "bi_alert_rule"."condition_json" IS '触发条件JSON';
COMMENT ON COLUMN "bi_alert_rule"."calc_sql" IS '计算SQL（自定义规则时使用）';
COMMENT ON COLUMN "bi_alert_rule"."schedule_type" IS '调度类型：interval/cron';
COMMENT ON COLUMN "bi_alert_rule"."cron_expr" IS 'Cron表达式';
COMMENT ON COLUMN "bi_alert_rule"."interval_seconds" IS '执行间隔（秒）';
COMMENT ON COLUMN "bi_alert_rule"."timezone" IS '执行时区';
COMMENT ON COLUMN "bi_alert_rule"."dedup_minutes" IS '去重窗口（分钟）';
COMMENT ON COLUMN "bi_alert_rule"."cooldown_minutes" IS '冷却时间（分钟）';
COMMENT ON COLUMN "bi_alert_rule"."owner_user_id" IS '规则负责人';
COMMENT ON COLUMN "bi_alert_rule"."notify_channels" IS '通知渠道JSON数组';
COMMENT ON COLUMN "bi_alert_rule"."notify_template" IS '通知模板';
COMMENT ON COLUMN "bi_alert_rule"."last_check_at" IS '上次检测时间';
COMMENT ON COLUMN "bi_alert_rule"."next_check_at" IS '下次检测时间';
COMMENT ON COLUMN "bi_alert_rule"."remark" IS '备注';
COMMENT ON COLUMN "bi_alert_rule"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_alert_rule"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX bi_alert_rule_rule_code_key ON public.bi_alert_rule USING btree (rule_code);
CREATE INDEX idx_bi_alert_rule_status ON public.bi_alert_rule USING btree (rule_status);
CREATE INDEX idx_bi_alert_rule_next_check ON public.bi_alert_rule USING btree (next_check_at);

-- ------------------------------------------------------------
-- 表: bi_alert_subscription  备注: 预警订阅实体
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_alert_subscription" CASCADE;
CREATE TABLE "bi_alert_subscription" (
    "id" bigint NOT NULL DEFAULT nextval('bi_alert_subscription_id_seq'::regclass),
    "rule_id" bigint,
    "subscriber_type" character varying(20) NOT NULL,
    "subscriber_id" bigint,
    "channel_type" character varying(20) NOT NULL,
    "channel_target" character varying(200),
    "severity_filter" jsonb NOT NULL DEFAULT '[]'::jsonb,
    "is_enabled" boolean NOT NULL DEFAULT true,
    "created_at" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "pk_bi_alert_subscription" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_alert_subscription" IS '预警订阅实体';
COMMENT ON COLUMN "bi_alert_subscription"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_alert_subscription"."rule_id" IS '规则ID';
COMMENT ON COLUMN "bi_alert_subscription"."is_enabled" IS '是否启用';
COMMENT ON COLUMN "bi_alert_subscription"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_alert_subscription"."updated_at" IS '更新时间';

CREATE INDEX idx_bi_alert_subscription_rule ON public.bi_alert_subscription USING btree (rule_id);

-- ------------------------------------------------------------
-- 表: bi_chart  备注: 图表配置表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_chart" CASCADE;
CREATE TABLE "bi_chart" (
    "id" bigint NOT NULL,
    "name" character varying(100) NOT NULL,
    "dataset_id" bigint NOT NULL,
    "chart_type" character varying(50) NOT NULL,
    "config_json" jsonb NOT NULL,
    "remark" character varying(500),
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_chart" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_chart" IS '图表配置表';
COMMENT ON COLUMN "bi_chart"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_chart"."name" IS '图表实体 - 对应bi_chart表 </summary> public class Chart : BaseEntity { <summary> 图表名称';
COMMENT ON COLUMN "bi_chart"."dataset_id" IS '关联数据集ID';
COMMENT ON COLUMN "bi_chart"."chart_type" IS '图表类型';
COMMENT ON COLUMN "bi_chart"."config_json" IS '图表配置JSON';
COMMENT ON COLUMN "bi_chart"."remark" IS '备注';
COMMENT ON COLUMN "bi_chart"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_chart"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_bi_chart" ON public.bi_chart USING btree (id);
CREATE INDEX "IX_bi_chart_dataset_id" ON public.bi_chart USING btree (dataset_id);

-- ------------------------------------------------------------
-- 表: bi_dataset  备注: SQL数据集定义表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_dataset" CASCADE;
CREATE TABLE "bi_dataset" (
    "id" bigint NOT NULL,
    "name" character varying(100) NOT NULL,
    "datasource_id" bigint NOT NULL,
    "sql_text" text NOT NULL,
    "param_schema" jsonb,
    "remark" character varying(500),
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_dataset" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_dataset" IS 'SQL数据集定义表';
COMMENT ON COLUMN "bi_dataset"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_dataset"."name" IS '图表实体 - 对应bi_chart表 </summary> public class Chart : BaseEntity { <summary> 图表名称';
COMMENT ON COLUMN "bi_dataset"."datasource_id" IS '关联的数据源ID';
COMMENT ON COLUMN "bi_dataset"."sql_text" IS 'SQL语句';
COMMENT ON COLUMN "bi_dataset"."param_schema" IS '参数定义JSON';
COMMENT ON COLUMN "bi_dataset"."remark" IS '备注';
COMMENT ON COLUMN "bi_dataset"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_dataset"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_bi_dataset" ON public.bi_dataset USING btree (id);
CREATE INDEX "IX_bi_dataset_datasource_id" ON public.bi_dataset USING btree (datasource_id);

-- ------------------------------------------------------------
-- 表: bi_dataset_field  备注: 数据集字段元数据表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_dataset_field" CASCADE;
CREATE TABLE "bi_dataset_field" (
    "id" bigint NOT NULL,
    "dataset_id" bigint NOT NULL,
    "field_name" character varying(100) NOT NULL,
    "field_alias" character varying(100),
    "data_type" character varying(50) NOT NULL,
    "role" character varying(20) NOT NULL,
    "agg_type" character varying(20) NOT NULL DEFAULT 'none'::character varying,
    "sort_order" integer NOT NULL DEFAULT 0,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_dataset_field" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_dataset_field" IS '数据集字段元数据表';
COMMENT ON COLUMN "bi_dataset_field"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_dataset_field"."dataset_id" IS '关联数据集ID';
COMMENT ON COLUMN "bi_dataset_field"."field_name" IS '字段名';
COMMENT ON COLUMN "bi_dataset_field"."field_alias" IS '字段别名（显示名）';
COMMENT ON COLUMN "bi_dataset_field"."data_type" IS '数据类型';
COMMENT ON COLUMN "bi_dataset_field"."role" IS '消息角色（user/assistant）';
COMMENT ON COLUMN "bi_dataset_field"."agg_type" IS '聚合类型：sum/count/avg/max/min/none';
COMMENT ON COLUMN "bi_dataset_field"."sort_order" IS '排序顺序';
COMMENT ON COLUMN "bi_dataset_field"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_dataset_field"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_bi_dataset_field" ON public.bi_dataset_field USING btree (id);
CREATE INDEX "IX_bi_dataset_field_dataset_id" ON public.bi_dataset_field USING btree (dataset_id);

-- ------------------------------------------------------------
-- 表: bi_datasource  备注: 数据源配置表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_datasource" CASCADE;
CREATE TABLE "bi_datasource" (
    "id" bigint NOT NULL,
    "name" character varying(100) NOT NULL,
    "type" character varying(50) NOT NULL,
    "conn_string" text NOT NULL,
    "remark" character varying(500),
    "is_enabled" boolean NOT NULL DEFAULT true,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_datasource" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_datasource" IS '数据源配置表';
COMMENT ON COLUMN "bi_datasource"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_datasource"."name" IS '图表实体 - 对应bi_chart表 </summary> public class Chart : BaseEntity { <summary> 图表名称';
COMMENT ON COLUMN "bi_datasource"."type" IS '数据源类型：sqlserver/postgres/mysql';
COMMENT ON COLUMN "bi_datasource"."conn_string" IS '连接字符串（加密存储）';
COMMENT ON COLUMN "bi_datasource"."remark" IS '备注';
COMMENT ON COLUMN "bi_datasource"."is_enabled" IS '是否启用';
COMMENT ON COLUMN "bi_datasource"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_datasource"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_bi_datasource" ON public.bi_datasource USING btree (id);

-- ------------------------------------------------------------
-- 表: bi_knowledge_category  备注: 知识库分类表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_knowledge_category" CASCADE;
CREATE TABLE "bi_knowledge_category" (
    "id" bigint NOT NULL,
    "name" character varying(100) NOT NULL,
    "parent_id" bigint,
    "sort_order" integer NOT NULL DEFAULT 0,
    "description" character varying(500),
    "created_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_knowledge_category" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_knowledge_category" IS '知识库分类表';
COMMENT ON COLUMN "bi_knowledge_category"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_knowledge_category"."name" IS '图表实体 - 对应bi_chart表 </summary> public class Chart : BaseEntity { <summary> 图表名称';
COMMENT ON COLUMN "bi_knowledge_category"."parent_id" IS '父分类ID';
COMMENT ON COLUMN "bi_knowledge_category"."sort_order" IS '排序顺序';
COMMENT ON COLUMN "bi_knowledge_category"."description" IS '描述';
COMMENT ON COLUMN "bi_knowledge_category"."created_at" IS '创建时间';

CREATE UNIQUE INDEX "PK_bi_knowledge_category" ON public.bi_knowledge_category USING btree (id);
CREATE INDEX "IX_bi_knowledge_category_parent_id" ON public.bi_knowledge_category USING btree (parent_id);

-- ------------------------------------------------------------
-- 表: bi_knowledge_chunk  备注: 知识库分块表（含向量嵌入）
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_knowledge_chunk" CASCADE;
CREATE TABLE "bi_knowledge_chunk" (
    "id" bigint NOT NULL,
    "document_id" bigint NOT NULL,
    "chunk_index" integer NOT NULL,
    "content" text NOT NULL,
    "content_length" integer,
    "embedding" vector(1024),
    "page_number" integer,
    "section_title" character varying(500),
    "metadata" jsonb,
    "created_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_knowledge_chunk" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_knowledge_chunk" IS '知识库分块表（含向量嵌入）';
COMMENT ON COLUMN "bi_knowledge_chunk"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_knowledge_chunk"."document_id" IS '所属文档ID';
COMMENT ON COLUMN "bi_knowledge_chunk"."chunk_index" IS '块在文档中的序号';
COMMENT ON COLUMN "bi_knowledge_chunk"."content" IS '消息内容';
COMMENT ON COLUMN "bi_knowledge_chunk"."content_length" IS '内容长度';
COMMENT ON COLUMN "bi_knowledge_chunk"."embedding" IS '向量嵌入（pgvector类型，1024维 for BGE-M3）';
COMMENT ON COLUMN "bi_knowledge_chunk"."page_number" IS '页码（PDF文档）';
COMMENT ON COLUMN "bi_knowledge_chunk"."section_title" IS '章节标题';
COMMENT ON COLUMN "bi_knowledge_chunk"."metadata" IS '扩展元信息（JSON格式）';
COMMENT ON COLUMN "bi_knowledge_chunk"."created_at" IS '创建时间';

CREATE UNIQUE INDEX "PK_bi_knowledge_chunk" ON public.bi_knowledge_chunk USING btree (id);
CREATE INDEX "IX_bi_knowledge_chunk_document_id" ON public.bi_knowledge_chunk USING btree (document_id);

-- ------------------------------------------------------------
-- 表: bi_knowledge_document  备注: 知识库文档表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_knowledge_document" CASCADE;
CREATE TABLE "bi_knowledge_document" (
    "id" bigint NOT NULL,
    "category_id" bigint,
    "title" character varying(500) NOT NULL,
    "file_name" character varying(255),
    "file_type" character varying(50),
    "file_size" bigint,
    "file_path" character varying(500),
    "content_hash" character varying(64),
    "status" character varying(50) NOT NULL DEFAULT 'pending'::character varying,
    "error_message" text,
    "chunk_count" integer NOT NULL DEFAULT 0,
    "datasource_id" bigint,
    "metadata" jsonb,
    "created_by" bigint,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    "ProcessProgress" integer NOT NULL DEFAULT 0,
    "ProcessedChunkCount" integer NOT NULL DEFAULT 0,
    "RawContent" text,
    CONSTRAINT "pk_bi_knowledge_document" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_knowledge_document" IS '知识库文档表';
COMMENT ON COLUMN "bi_knowledge_document"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_knowledge_document"."category_id" IS '分类ID';
COMMENT ON COLUMN "bi_knowledge_document"."title" IS '收藏标题';
COMMENT ON COLUMN "bi_knowledge_document"."file_name" IS '原始文件名';
COMMENT ON COLUMN "bi_knowledge_document"."file_type" IS '文件类型 (pdf/docx/xlsx/txt/md)';
COMMENT ON COLUMN "bi_knowledge_document"."file_size" IS '文件大小(bytes)';
COMMENT ON COLUMN "bi_knowledge_document"."file_path" IS '文件存储路径';
COMMENT ON COLUMN "bi_knowledge_document"."content_hash" IS '内容Hash（用于去重）';
COMMENT ON COLUMN "bi_knowledge_document"."status" IS '处理状态：pending-待处理, processing-处理中, completed-已完成, failed-失败';
COMMENT ON COLUMN "bi_knowledge_document"."error_message" IS '错误信息';
COMMENT ON COLUMN "bi_knowledge_document"."chunk_count" IS '分块数量';
COMMENT ON COLUMN "bi_knowledge_document"."datasource_id" IS '关联的数据源ID';
COMMENT ON COLUMN "bi_knowledge_document"."metadata" IS '扩展元信息（JSON格式）';
COMMENT ON COLUMN "bi_knowledge_document"."created_by" IS '创建人ID';
COMMENT ON COLUMN "bi_knowledge_document"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_knowledge_document"."updated_at" IS '更新时间';
COMMENT ON COLUMN "bi_knowledge_document"."ProcessProgress" IS '处理进度百分比（0-100）';
COMMENT ON COLUMN "bi_knowledge_document"."ProcessedChunkCount" IS '已处理分块数（用于显示进度）';
COMMENT ON COLUMN "bi_knowledge_document"."RawContent" IS '原始文档内容（临时存储，处理完后可清空）';

CREATE UNIQUE INDEX "PK_bi_knowledge_document" ON public.bi_knowledge_document USING btree (id);
CREATE INDEX "IX_bi_knowledge_document_category_id" ON public.bi_knowledge_document USING btree (category_id);
CREATE INDEX "IX_bi_knowledge_document_status" ON public.bi_knowledge_document USING btree (status);

-- ------------------------------------------------------------
-- 表: bi_knowledge_test_case  备注: 知识库测试用例表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_knowledge_test_case" CASCADE;
CREATE TABLE "bi_knowledge_test_case" (
    "id" bigint NOT NULL,
    "name" character varying(200) NOT NULL,
    "query" text NOT NULL,
    "expected_document_ids" jsonb,
    "expected_chunk_ids" jsonb,
    "expected_keywords" jsonb,
    "category_id" bigint,
    "remark" text,
    "is_enabled" boolean NOT NULL,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_knowledge_test_case" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_knowledge_test_case" IS '知识库测试用例表';
COMMENT ON COLUMN "bi_knowledge_test_case"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_knowledge_test_case"."name" IS '图表实体 - 对应bi_chart表 </summary> public class Chart : BaseEntity { <summary> 图表名称';
COMMENT ON COLUMN "bi_knowledge_test_case"."query" IS '测试查询文本';
COMMENT ON COLUMN "bi_knowledge_test_case"."expected_document_ids" IS '期望命中的文档ID列表（JSON数组）';
COMMENT ON COLUMN "bi_knowledge_test_case"."expected_chunk_ids" IS '期望命中的分块ID列表（JSON数组，更精确的评估）';
COMMENT ON COLUMN "bi_knowledge_test_case"."expected_keywords" IS '期望命中的关键词列表（JSON数组，用于模糊匹配评估）';
COMMENT ON COLUMN "bi_knowledge_test_case"."category_id" IS '分类ID';
COMMENT ON COLUMN "bi_knowledge_test_case"."remark" IS '备注';
COMMENT ON COLUMN "bi_knowledge_test_case"."is_enabled" IS '是否启用';
COMMENT ON COLUMN "bi_knowledge_test_case"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_knowledge_test_case"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_bi_knowledge_test_case" ON public.bi_knowledge_test_case USING btree (id);

-- ------------------------------------------------------------
-- 表: bi_knowledge_test_run  备注: 知识库测试运行记录表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_knowledge_test_run" CASCADE;
CREATE TABLE "bi_knowledge_test_run" (
    "id" bigint NOT NULL,
    "name" character varying(200),
    "status" character varying(50) NOT NULL,
    "total_cases" integer NOT NULL,
    "completed_cases" integer NOT NULL,
    "top_k" integer NOT NULL,
    "min_score" real NOT NULL,
    "hit_rate" real NOT NULL,
    "mrr" real NOT NULL,
    "avg_precision" real NOT NULL,
    "avg_recall" real NOT NULL,
    "avg_latency_ms" real NOT NULL,
    "detail_results" jsonb,
    "error_message" text,
    "started_at" timestamp with time zone,
    "completed_at" timestamp with time zone,
    "created_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_knowledge_test_run" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_knowledge_test_run" IS '知识库测试运行记录表';
COMMENT ON COLUMN "bi_knowledge_test_run"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_knowledge_test_run"."name" IS '图表实体 - 对应bi_chart表 </summary> public class Chart : BaseEntity { <summary> 图表名称';
COMMENT ON COLUMN "bi_knowledge_test_run"."status" IS '处理状态：pending-待处理, processing-处理中, completed-已完成, failed-失败';
COMMENT ON COLUMN "bi_knowledge_test_run"."total_cases" IS '测试用例总数';
COMMENT ON COLUMN "bi_knowledge_test_run"."completed_cases" IS '已完成用例数';
COMMENT ON COLUMN "bi_knowledge_test_run"."top_k" IS 'TopK参数（检索返回的最大结果数）';
COMMENT ON COLUMN "bi_knowledge_test_run"."min_score" IS '最小相似度阈值';
COMMENT ON COLUMN "bi_knowledge_test_run"."hit_rate" IS '命中率（Hit Rate）：至少命中一个期望结果的比例';
COMMENT ON COLUMN "bi_knowledge_test_run"."mrr" IS '平均倒数排名（MRR - Mean Reciprocal Rank）';
COMMENT ON COLUMN "bi_knowledge_test_run"."avg_precision" IS '平均精确率（Precision@K）';
COMMENT ON COLUMN "bi_knowledge_test_run"."avg_recall" IS '平均召回率（Recall@K）';
COMMENT ON COLUMN "bi_knowledge_test_run"."avg_latency_ms" IS '平均检索耗时（毫秒）';
COMMENT ON COLUMN "bi_knowledge_test_run"."detail_results" IS '详细结果（JSON数组，每个用例的结果）';
COMMENT ON COLUMN "bi_knowledge_test_run"."error_message" IS '错误信息';
COMMENT ON COLUMN "bi_knowledge_test_run"."started_at" IS '开始时间';
COMMENT ON COLUMN "bi_knowledge_test_run"."completed_at" IS '结束时间';
COMMENT ON COLUMN "bi_knowledge_test_run"."created_at" IS '创建时间';

CREATE UNIQUE INDEX "PK_bi_knowledge_test_run" ON public.bi_knowledge_test_run USING btree (id);

-- ------------------------------------------------------------
-- 表: bi_kpi_category  备注: 指标分类表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_kpi_category" CASCADE;
CREATE TABLE "bi_kpi_category" (
    "id" bigint NOT NULL,
    "name" character varying(100) NOT NULL,
    "parent_id" bigint,
    "description" character varying(500),
    "sort_order" integer NOT NULL DEFAULT 0,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_kpi_category" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_kpi_category" IS '指标分类表';
COMMENT ON COLUMN "bi_kpi_category"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_kpi_category"."name" IS '图表实体 - 对应bi_chart表 </summary> public class Chart : BaseEntity { <summary> 图表名称';
COMMENT ON COLUMN "bi_kpi_category"."parent_id" IS '父分类ID';
COMMENT ON COLUMN "bi_kpi_category"."description" IS '描述';
COMMENT ON COLUMN "bi_kpi_category"."sort_order" IS '排序顺序';
COMMENT ON COLUMN "bi_kpi_category"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_kpi_category"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_bi_kpi_category" ON public.bi_kpi_category USING btree (id);
CREATE INDEX "IX_bi_kpi_category_parent_id" ON public.bi_kpi_category USING btree (parent_id);

-- ------------------------------------------------------------
-- 表: bi_kpi_definition  备注: 指标定义表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_kpi_definition" CASCADE;
CREATE TABLE "bi_kpi_definition" (
    "id" bigint NOT NULL,
    "code" character varying(50) NOT NULL,
    "name" character varying(200) NOT NULL,
    "category_id" bigint NOT NULL,
    "definition" character varying(2000),
    "formula" character varying(1000),
    "sql_template" text,
    "datasource_id" bigint,
    "unit" character varying(50),
    "data_type" character varying(50) NOT NULL DEFAULT 'number'::character varying,
    "embedding_json" text,
    "embedding_updated_at" timestamp with time zone,
    "is_enabled" boolean NOT NULL DEFAULT true,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_kpi_definition" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_kpi_definition" IS '指标定义表';
COMMENT ON COLUMN "bi_kpi_definition"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_kpi_definition"."code" IS '指标编码（唯一标识）';
COMMENT ON COLUMN "bi_kpi_definition"."name" IS '图表实体 - 对应bi_chart表 </summary> public class Chart : BaseEntity { <summary> 图表名称';
COMMENT ON COLUMN "bi_kpi_definition"."category_id" IS '分类ID';
COMMENT ON COLUMN "bi_kpi_definition"."definition" IS '指标口径/定义说明';
COMMENT ON COLUMN "bi_kpi_definition"."formula" IS '计算公式说明';
COMMENT ON COLUMN "bi_kpi_definition"."sql_template" IS 'SQL模板（可包含参数占位符）';
COMMENT ON COLUMN "bi_kpi_definition"."datasource_id" IS '关联的数据源ID';
COMMENT ON COLUMN "bi_kpi_definition"."unit" IS '单位';
COMMENT ON COLUMN "bi_kpi_definition"."data_type" IS '数据类型';
COMMENT ON COLUMN "bi_kpi_definition"."embedding_json" IS '向量嵌入JSON（用于语义检索，存储float数组的JSON格式） 注意：如果数据库支持pgvector，可以改用vector类型以获得更好的性能';
COMMENT ON COLUMN "bi_kpi_definition"."embedding_updated_at" IS '向量生成时间';
COMMENT ON COLUMN "bi_kpi_definition"."is_enabled" IS '是否启用';
COMMENT ON COLUMN "bi_kpi_definition"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_kpi_definition"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_bi_kpi_definition" ON public.bi_kpi_definition USING btree (id);
CREATE INDEX "IX_bi_kpi_definition_category_id" ON public.bi_kpi_definition USING btree (category_id);
CREATE UNIQUE INDEX "IX_bi_kpi_definition_code" ON public.bi_kpi_definition USING btree (code);
CREATE INDEX "IX_bi_kpi_definition_datasource_id" ON public.bi_kpi_definition USING btree (datasource_id);

-- ------------------------------------------------------------
-- 表: bi_panel  备注: 分析面板表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_panel" CASCADE;
CREATE TABLE "bi_panel" (
    "id" bigint NOT NULL,
    "name" character varying(100) NOT NULL,
    "panel_type" character varying(50) NOT NULL DEFAULT 'pc_dashboard'::character varying,
    "config_json" jsonb NOT NULL,
    "remark" character varying(500),
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_panel" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_panel" IS '分析面板表';
COMMENT ON COLUMN "bi_panel"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_panel"."name" IS '图表实体 - 对应bi_chart表 </summary> public class Chart : BaseEntity { <summary> 图表名称';
COMMENT ON COLUMN "bi_panel"."panel_type" IS '面板类型：pc_dashboard/big_screen/mobile';
COMMENT ON COLUMN "bi_panel"."config_json" IS '图表配置JSON';
COMMENT ON COLUMN "bi_panel"."remark" IS '备注';
COMMENT ON COLUMN "bi_panel"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_panel"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_bi_panel" ON public.bi_panel USING btree (id);

-- ------------------------------------------------------------
-- 表: bi_panel_item  备注: 面板子项表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_panel_item" CASCADE;
CREATE TABLE "bi_panel_item" (
    "id" bigint NOT NULL,
    "panel_id" bigint NOT NULL,
    "chart_id" bigint,
    "layout_json" jsonb NOT NULL,
    "sort_order" integer NOT NULL DEFAULT 0,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    "mobile_layout_json" jsonb,
    "screen_layout_json" jsonb,
    CONSTRAINT "pk_bi_panel_item" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_panel_item" IS '面板子项表';
COMMENT ON COLUMN "bi_panel_item"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_panel_item"."panel_id" IS '面板子项实体 - 对应bi_panel_item表 </summary> public class PanelItem : BaseEntity { <summary> 所属面板ID';
COMMENT ON COLUMN "bi_panel_item"."chart_id" IS '关联图表ID';
COMMENT ON COLUMN "bi_panel_item"."layout_json" IS 'PC端布局信息JSON {x, y, w, h}';
COMMENT ON COLUMN "bi_panel_item"."sort_order" IS '排序顺序';
COMMENT ON COLUMN "bi_panel_item"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_panel_item"."updated_at" IS '更新时间';
COMMENT ON COLUMN "bi_panel_item"."mobile_layout_json" IS '移动端布局信息JSON {x, y, w, h}（可选，为空时使用LayoutJson）';
COMMENT ON COLUMN "bi_panel_item"."screen_layout_json" IS '大屏端布局信息JSON {x, y, w, h}（可选，为空时使用LayoutJson）';

CREATE UNIQUE INDEX "PK_bi_panel_item" ON public.bi_panel_item USING btree (id);
CREATE INDEX "IX_bi_panel_item_chart_id" ON public.bi_panel_item USING btree (chart_id);
CREATE INDEX "IX_bi_panel_item_panel_id" ON public.bi_panel_item USING btree (panel_id);

-- ------------------------------------------------------------
-- 表: bi_publish  备注: 发布记录表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_publish" CASCADE;
CREATE TABLE "bi_publish" (
    "id" bigint NOT NULL,
    "title" character varying(200) NOT NULL,
    "object_type" character varying(50) NOT NULL,
    "object_id" bigint NOT NULL,
    "access_scope" character varying(50) NOT NULL DEFAULT 'private'::character varying,
    "access_token" character varying(100),
    "access_password" character varying(100),
    "expire_at" timestamp with time zone,
    "is_enabled" boolean NOT NULL DEFAULT true,
    "view_count" integer NOT NULL DEFAULT 0,
    "last_viewed_at" timestamp with time zone,
    "published_by" bigint NOT NULL,
    "remark" character varying(500),
    "allowed_roles" jsonb,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_publish" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_publish" IS '发布记录表';
COMMENT ON COLUMN "bi_publish"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_publish"."title" IS '收藏标题';
COMMENT ON COLUMN "bi_publish"."object_type" IS '对象类型：report/panel/chart';
COMMENT ON COLUMN "bi_publish"."object_id" IS '对象ID';
COMMENT ON COLUMN "bi_publish"."access_scope" IS '访问范围：public(公开)/private(私有)/role(角色)';
COMMENT ON COLUMN "bi_publish"."access_token" IS '访问Token（用于分享链接）';
COMMENT ON COLUMN "bi_publish"."access_password" IS '访问密码（可选）';
COMMENT ON COLUMN "bi_publish"."expire_at" IS '过期时间';
COMMENT ON COLUMN "bi_publish"."is_enabled" IS '是否启用';
COMMENT ON COLUMN "bi_publish"."view_count" IS '访问次数统计';
COMMENT ON COLUMN "bi_publish"."last_viewed_at" IS '最后访问时间';
COMMENT ON COLUMN "bi_publish"."published_by" IS '发布人ID';
COMMENT ON COLUMN "bi_publish"."remark" IS '备注';
COMMENT ON COLUMN "bi_publish"."allowed_roles" IS '允许访问的角色ID列表（JSON数组，当AccessScope=role时）';
COMMENT ON COLUMN "bi_publish"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_publish"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_bi_publish" ON public.bi_publish USING btree (id);
CREATE UNIQUE INDEX "IX_bi_publish_access_token" ON public.bi_publish USING btree (access_token);

-- ------------------------------------------------------------
-- 表: bi_report  备注: 报表/报告主表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_report" CASCADE;
CREATE TABLE "bi_report" (
    "id" bigint NOT NULL,
    "name" character varying(200) NOT NULL,
    "report_type" character varying(50) NOT NULL DEFAULT 'report'::character varying,
    "cover_image" character varying(500),
    "config_json" jsonb NOT NULL,
    "remark" character varying(500),
    "is_published" boolean NOT NULL DEFAULT false,
    "published_at" timestamp with time zone,
    "created_by" bigint NOT NULL,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_report" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_report" IS '报表/报告主表';
COMMENT ON COLUMN "bi_report"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_report"."name" IS '图表实体 - 对应bi_chart表 </summary> public class Chart : BaseEntity { <summary> 图表名称';
COMMENT ON COLUMN "bi_report"."report_type" IS '报表类型：report(报告)/dashboard(仪表板)';
COMMENT ON COLUMN "bi_report"."cover_image" IS '封面图URL';
COMMENT ON COLUMN "bi_report"."config_json" IS '图表配置JSON';
COMMENT ON COLUMN "bi_report"."remark" IS '备注';
COMMENT ON COLUMN "bi_report"."is_published" IS '是否已发布';
COMMENT ON COLUMN "bi_report"."published_at" IS '发布时间';
COMMENT ON COLUMN "bi_report"."created_by" IS '创建人ID';
COMMENT ON COLUMN "bi_report"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_report"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_bi_report" ON public.bi_report USING btree (id);

-- ------------------------------------------------------------
-- 表: bi_report_item  备注: 报表元素表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_report_item" CASCADE;
CREATE TABLE "bi_report_item" (
    "id" bigint NOT NULL,
    "page_id" bigint NOT NULL,
    "item_type" character varying(50) NOT NULL,
    "chart_id" bigint,
    "panel_id" bigint,
    "text_content" text,
    "image_url" character varying(500),
    "layout_json" jsonb NOT NULL,
    "style_json" jsonb NOT NULL,
    "sort_order" integer NOT NULL DEFAULT 0,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_report_item" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_report_item" IS '报表元素表';
COMMENT ON COLUMN "bi_report_item"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_report_item"."page_id" IS '报表元素/组件 支持多种类型：图表、面板、文本、图片、表格等 </summary> public class BiReportItem : BaseEntity { <summary> 所属页面ID';
COMMENT ON COLUMN "bi_report_item"."item_type" IS '元素类型：chart/panel/text/image/table/shape';
COMMENT ON COLUMN "bi_report_item"."chart_id" IS '关联图表ID';
COMMENT ON COLUMN "bi_report_item"."panel_id" IS '面板子项实体 - 对应bi_panel_item表 </summary> public class PanelItem : BaseEntity { <summary> 所属面板ID';
COMMENT ON COLUMN "bi_report_item"."text_content" IS '文本内容（当ItemType=text时）';
COMMENT ON COLUMN "bi_report_item"."image_url" IS '图片URL（当ItemType=image时）';
COMMENT ON COLUMN "bi_report_item"."layout_json" IS 'PC端布局信息JSON {x, y, w, h}';
COMMENT ON COLUMN "bi_report_item"."style_json" IS '样式配置JSON（字体、颜色、边框等）';
COMMENT ON COLUMN "bi_report_item"."sort_order" IS '排序顺序';
COMMENT ON COLUMN "bi_report_item"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_report_item"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_bi_report_item" ON public.bi_report_item USING btree (id);
CREATE INDEX "IX_bi_report_item_chart_id" ON public.bi_report_item USING btree (chart_id);
CREATE INDEX "IX_bi_report_item_page_id" ON public.bi_report_item USING btree (page_id);
CREATE INDEX "IX_bi_report_item_panel_id" ON public.bi_report_item USING btree (panel_id);

-- ------------------------------------------------------------
-- 表: bi_report_page  备注: 报表页面表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_report_page" CASCADE;
CREATE TABLE "bi_report_page" (
    "id" bigint NOT NULL,
    "report_id" bigint NOT NULL,
    "title" character varying(200) NOT NULL,
    "sort_order" integer NOT NULL DEFAULT 0,
    "config_json" jsonb NOT NULL,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_report_page" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_report_page" IS '报表页面表';
COMMENT ON COLUMN "bi_report_page"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_report_page"."report_id" IS '报表页面 每个报表可包含多个页面，类似PPT结构 </summary> public class BiReportPage : BaseEntity { <summary> 所属报表ID';
COMMENT ON COLUMN "bi_report_page"."title" IS '收藏标题';
COMMENT ON COLUMN "bi_report_page"."sort_order" IS '排序顺序';
COMMENT ON COLUMN "bi_report_page"."config_json" IS '图表配置JSON';
COMMENT ON COLUMN "bi_report_page"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_report_page"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_bi_report_page" ON public.bi_report_page USING btree (id);
CREATE INDEX "IX_bi_report_page_report_id" ON public.bi_report_page USING btree (report_id);

-- ------------------------------------------------------------
-- 表: bi_slow_query_log  备注: 慢查询日志表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "bi_slow_query_log" CASCADE;
CREATE TABLE "bi_slow_query_log" (
    "id" bigint NOT NULL,
    "datasource_id" bigint NOT NULL,
    "chart_id" bigint,
    "sql_text" text NOT NULL,
    "execution_time_ms" bigint NOT NULL,
    "threshold_ms" bigint NOT NULL,
    "executed_by" text,
    "executed_at" timestamp with time zone NOT NULL,
    "explain_result" text,
    "suggestion" text,
    "is_resolved" boolean NOT NULL,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_bi_slow_query_log" PRIMARY KEY (id)
);

COMMENT ON TABLE "bi_slow_query_log" IS '慢查询日志表';
COMMENT ON COLUMN "bi_slow_query_log"."id" IS '收藏ID';
COMMENT ON COLUMN "bi_slow_query_log"."datasource_id" IS '关联的数据源ID';
COMMENT ON COLUMN "bi_slow_query_log"."chart_id" IS '关联图表ID';
COMMENT ON COLUMN "bi_slow_query_log"."sql_text" IS 'SQL语句';
COMMENT ON COLUMN "bi_slow_query_log"."execution_time_ms" IS '执行时间（毫秒）';
COMMENT ON COLUMN "bi_slow_query_log"."threshold_ms" IS '慢查询阈值（毫秒）';
COMMENT ON COLUMN "bi_slow_query_log"."executed_by" IS '执行用户';
COMMENT ON COLUMN "bi_slow_query_log"."executed_at" IS '执行时间';
COMMENT ON COLUMN "bi_slow_query_log"."explain_result" IS 'EXPLAIN分析结果（JSON）';
COMMENT ON COLUMN "bi_slow_query_log"."suggestion" IS '优化建议';
COMMENT ON COLUMN "bi_slow_query_log"."is_resolved" IS '是否已处理';
COMMENT ON COLUMN "bi_slow_query_log"."created_at" IS '创建时间';
COMMENT ON COLUMN "bi_slow_query_log"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_bi_slow_query_log" ON public.bi_slow_query_log USING btree (id);
CREATE INDEX "IX_bi_slow_query_log_chart_id" ON public.bi_slow_query_log USING btree (chart_id);
CREATE INDEX "IX_bi_slow_query_log_datasource_id" ON public.bi_slow_query_log USING btree (datasource_id);

-- ------------------------------------------------------------
-- 表: sys_config  备注: 系统配置表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "sys_config" CASCADE;
CREATE TABLE "sys_config" (
    "id" bigint NOT NULL,
    "config_key" character varying(100) NOT NULL,
    "config_value" text,
    "config_group" character varying(50) NOT NULL,
    "config_type" character varying(50) NOT NULL DEFAULT 'string'::character varying,
    "is_encrypted" boolean NOT NULL DEFAULT false,
    "display_name" character varying(100),
    "remark" character varying(500),
    "sort_order" integer NOT NULL DEFAULT 0,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone,
    CONSTRAINT "pk_sys_config" PRIMARY KEY (id)
);

COMMENT ON TABLE "sys_config" IS '系统配置表';
COMMENT ON COLUMN "sys_config"."id" IS '收藏ID';
COMMENT ON COLUMN "sys_config"."config_key" IS '配置键（唯一标识）';
COMMENT ON COLUMN "sys_config"."config_value" IS '配置值';
COMMENT ON COLUMN "sys_config"."config_group" IS '配置分组：basic-基础设置, ai-AI服务, security-安全配置, cache-缓存配置, data-数据配置';
COMMENT ON COLUMN "sys_config"."config_type" IS '配置类型：string, number, boolean, json, password';
COMMENT ON COLUMN "sys_config"."is_encrypted" IS '是否加密存储（敏感配置如API Key）';
COMMENT ON COLUMN "sys_config"."display_name" IS '配置名称（显示用）';
COMMENT ON COLUMN "sys_config"."remark" IS '备注';
COMMENT ON COLUMN "sys_config"."sort_order" IS '排序顺序';
COMMENT ON COLUMN "sys_config"."created_at" IS '创建时间';
COMMENT ON COLUMN "sys_config"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_sys_config" ON public.sys_config USING btree (id);
CREATE INDEX "IX_sys_config_config_group" ON public.sys_config USING btree (config_group);
CREATE UNIQUE INDEX "IX_sys_config_config_key" ON public.sys_config USING btree (config_key);

-- ------------------------------------------------------------
-- 表: sys_menu  备注: 系统菜单表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "sys_menu" CASCADE;
CREATE TABLE "sys_menu" (
    "id" bigint NOT NULL,
    "name" character varying(100) NOT NULL,
    "parent_id" bigint NOT NULL DEFAULT 0,
    "menu_type" character varying(50) NOT NULL DEFAULT 'folder'::character varying,
    "icon" character varying(100),
    "link_url" character varying(500),
    "publish_id" bigint,
    "sort_order" integer NOT NULL DEFAULT 0,
    "is_visible" boolean NOT NULL DEFAULT true,
    "remark" character varying(500),
    "SysMenuId" bigint,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_sys_menu" PRIMARY KEY (id)
);

COMMENT ON TABLE "sys_menu" IS '系统菜单表';
COMMENT ON COLUMN "sys_menu"."id" IS '收藏ID';
COMMENT ON COLUMN "sys_menu"."name" IS '图表实体 - 对应bi_chart表 </summary> public class Chart : BaseEntity { <summary> 图表名称';
COMMENT ON COLUMN "sys_menu"."parent_id" IS '父分类ID';
COMMENT ON COLUMN "sys_menu"."menu_type" IS '菜单类型：folder(目录)/link(链接)/publish(发布对象)';
COMMENT ON COLUMN "sys_menu"."icon" IS '图标名称';
COMMENT ON COLUMN "sys_menu"."link_url" IS '链接地址（当MenuType=link时）';
COMMENT ON COLUMN "sys_menu"."publish_id" IS '关联发布ID（当MenuType=publish时）';
COMMENT ON COLUMN "sys_menu"."sort_order" IS '排序顺序';
COMMENT ON COLUMN "sys_menu"."is_visible" IS '是否可见';
COMMENT ON COLUMN "sys_menu"."remark" IS '备注';
COMMENT ON COLUMN "sys_menu"."created_at" IS '创建时间';
COMMENT ON COLUMN "sys_menu"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_sys_menu" ON public.sys_menu USING btree (id);
CREATE INDEX "IX_sys_menu_publish_id" ON public.sys_menu USING btree (publish_id);
CREATE INDEX "IX_sys_menu_SysMenuId" ON public.sys_menu USING btree ("SysMenuId");

-- ------------------------------------------------------------
-- 表: sys_org  备注: 系统组织表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "sys_org" CASCADE;
CREATE TABLE "sys_org" (
    "id" bigint NOT NULL,
    "org_code" character varying(50) NOT NULL,
    "org_name" character varying(100) NOT NULL,
    "parent_id" bigint NOT NULL DEFAULT 0,
    "org_type" character varying(50) NOT NULL DEFAULT 'dept'::character varying,
    "sort_order" integer NOT NULL DEFAULT 0,
    "is_enabled" boolean NOT NULL DEFAULT true,
    "remark" character varying(500),
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_sys_org" PRIMARY KEY (id)
);

COMMENT ON TABLE "sys_org" IS '系统组织表';
COMMENT ON COLUMN "sys_org"."id" IS '收藏ID';
COMMENT ON COLUMN "sys_org"."org_code" IS '系统组织架构表 - 对应sys_org表 </summary> public class SysOrg : BaseEntity { <summary> 组织编码';
COMMENT ON COLUMN "sys_org"."org_name" IS '组织名称';
COMMENT ON COLUMN "sys_org"."parent_id" IS '父分类ID';
COMMENT ON COLUMN "sys_org"."org_type" IS '组织类型：company(公司)/dept(部门)/group(小组)';
COMMENT ON COLUMN "sys_org"."sort_order" IS '排序顺序';
COMMENT ON COLUMN "sys_org"."is_enabled" IS '是否启用';
COMMENT ON COLUMN "sys_org"."remark" IS '备注';
COMMENT ON COLUMN "sys_org"."created_at" IS '创建时间';
COMMENT ON COLUMN "sys_org"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_sys_org" ON public.sys_org USING btree (id);
CREATE UNIQUE INDEX "IX_sys_org_org_code" ON public.sys_org USING btree (org_code);

-- ------------------------------------------------------------
-- 表: sys_role  备注: 系统角色表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "sys_role" CASCADE;
CREATE TABLE "sys_role" (
    "id" bigint NOT NULL,
    "role_code" character varying(50) NOT NULL,
    "role_name" character varying(100) NOT NULL,
    "remark" character varying(500),
    "sort_order" integer NOT NULL DEFAULT 0,
    "is_enabled" boolean NOT NULL DEFAULT true,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    CONSTRAINT "pk_sys_role" PRIMARY KEY (id)
);

COMMENT ON TABLE "sys_role" IS '系统角色表';
COMMENT ON COLUMN "sys_role"."id" IS '收藏ID';
COMMENT ON COLUMN "sys_role"."role_code" IS '系统角色表 - 对应sys_role表 </summary> public class SysRole : BaseEntity { <summary> 角色编码（唯一标识）';
COMMENT ON COLUMN "sys_role"."role_name" IS '角色名称';
COMMENT ON COLUMN "sys_role"."remark" IS '备注';
COMMENT ON COLUMN "sys_role"."sort_order" IS '排序顺序';
COMMENT ON COLUMN "sys_role"."is_enabled" IS '是否启用';
COMMENT ON COLUMN "sys_role"."created_at" IS '创建时间';
COMMENT ON COLUMN "sys_role"."updated_at" IS '更新时间';

CREATE UNIQUE INDEX "PK_sys_role" ON public.sys_role USING btree (id);
CREATE UNIQUE INDEX "IX_sys_role_role_code" ON public.sys_role USING btree (role_code);

-- ------------------------------------------------------------
-- 表: sys_role_menu  备注: 角色菜单关联表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "sys_role_menu" CASCADE;
CREATE TABLE "sys_role_menu" (
    "id" bigint NOT NULL,
    "role_id" bigint NOT NULL,
    "menu_id" bigint NOT NULL,
    CONSTRAINT "pk_sys_role_menu" PRIMARY KEY (id)
);

COMMENT ON TABLE "sys_role_menu" IS '角色菜单关联表';
COMMENT ON COLUMN "sys_role_menu"."id" IS '收藏ID';
COMMENT ON COLUMN "sys_role_menu"."role_id" IS '角色ID';
COMMENT ON COLUMN "sys_role_menu"."menu_id" IS '菜单ID';

CREATE UNIQUE INDEX "PK_sys_role_menu" ON public.sys_role_menu USING btree (id);
CREATE INDEX "IX_sys_role_menu_menu_id" ON public.sys_role_menu USING btree (menu_id);
CREATE UNIQUE INDEX "IX_sys_role_menu_role_id_menu_id" ON public.sys_role_menu USING btree (role_id, menu_id);

-- ------------------------------------------------------------
-- 表: sys_user  备注: 系统用户表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "sys_user" CASCADE;
CREATE TABLE "sys_user" (
    "id" bigint NOT NULL,
    "username" character varying(50) NOT NULL,
    "password_hash" character varying(256) NOT NULL,
    "real_name" character varying(50),
    "email" character varying(100),
    "phone" character varying(20),
    "avatar" character varying(500),
    "is_enabled" boolean NOT NULL DEFAULT true,
    "last_login_at" timestamp with time zone,
    "created_at" timestamp with time zone NOT NULL,
    "updated_at" timestamp with time zone NOT NULL,
    "org_id" bigint,
    CONSTRAINT "pk_sys_user" PRIMARY KEY (id)
);

COMMENT ON TABLE "sys_user" IS '系统用户表';
COMMENT ON COLUMN "sys_user"."id" IS '收藏ID';
COMMENT ON COLUMN "sys_user"."username" IS '系统用户实体 - 对应sys_user表 </summary> public class SysUser : BaseEntity { <summary> 用户名';
COMMENT ON COLUMN "sys_user"."password_hash" IS '密码哈希';
COMMENT ON COLUMN "sys_user"."real_name" IS '真实姓名';
COMMENT ON COLUMN "sys_user"."email" IS '邮箱';
COMMENT ON COLUMN "sys_user"."phone" IS '手机号';
COMMENT ON COLUMN "sys_user"."avatar" IS '头像URL';
COMMENT ON COLUMN "sys_user"."is_enabled" IS '是否启用';
COMMENT ON COLUMN "sys_user"."last_login_at" IS '最后登录时间';
COMMENT ON COLUMN "sys_user"."created_at" IS '创建时间';
COMMENT ON COLUMN "sys_user"."updated_at" IS '更新时间';
COMMENT ON COLUMN "sys_user"."org_id" IS '所属组织ID';

CREATE UNIQUE INDEX "PK_sys_user" ON public.sys_user USING btree (id);
CREATE UNIQUE INDEX "IX_sys_user_username" ON public.sys_user USING btree (username);
CREATE INDEX "IX_sys_user_org_id" ON public.sys_user USING btree (org_id);

-- ------------------------------------------------------------
-- 表: sys_user_role  备注: 用户角色关联表
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "sys_user_role" CASCADE;
CREATE TABLE "sys_user_role" (
    "id" bigint NOT NULL,
    "user_id" bigint NOT NULL,
    "role_id" bigint NOT NULL,
    CONSTRAINT "pk_sys_user_role" PRIMARY KEY (id)
);

COMMENT ON TABLE "sys_user_role" IS '用户角色关联表';
COMMENT ON COLUMN "sys_user_role"."id" IS '收藏ID';
COMMENT ON COLUMN "sys_user_role"."user_id" IS '用户ID';
COMMENT ON COLUMN "sys_user_role"."role_id" IS '角色ID';

CREATE UNIQUE INDEX "PK_sys_user_role" ON public.sys_user_role USING btree (id);
CREATE INDEX "IX_sys_user_role_role_id" ON public.sys_user_role USING btree (role_id);
CREATE UNIQUE INDEX "IX_sys_user_role_user_id_role_id" ON public.sys_user_role USING btree (user_id, role_id);

-- ============================================================
-- 外键约束 (Foreign Keys)
-- ============================================================
ALTER TABLE "bi_ai_favorite" ADD CONSTRAINT "FK_bi_ai_favorite_bi_datasource_datasource_id" FOREIGN KEY (datasource_id) REFERENCES bi_datasource(id);
ALTER TABLE "bi_ai_favorite" ADD CONSTRAINT "FK_bi_ai_favorite_sys_user_user_id" FOREIGN KEY (user_id) REFERENCES sys_user(id) ON DELETE CASCADE;
ALTER TABLE "bi_ai_message" ADD CONSTRAINT "FK_bi_ai_message_bi_ai_session_session_id" FOREIGN KEY (session_id) REFERENCES bi_ai_session(id) ON DELETE CASCADE;
ALTER TABLE "bi_ai_session" ADD CONSTRAINT "FK_bi_ai_session_bi_datasource_datasource_id" FOREIGN KEY (datasource_id) REFERENCES bi_datasource(id);
ALTER TABLE "bi_ai_session" ADD CONSTRAINT "FK_bi_ai_session_sys_user_user_id" FOREIGN KEY (user_id) REFERENCES sys_user(id) ON DELETE CASCADE;
ALTER TABLE "bi_alert_event" ADD CONSTRAINT "fk_bi_alert_event_ack_user" FOREIGN KEY (ack_by) REFERENCES sys_user(id);
ALTER TABLE "bi_alert_event" ADD CONSTRAINT "fk_bi_alert_event_resolve_user" FOREIGN KEY (resolved_by) REFERENCES sys_user(id);
ALTER TABLE "bi_alert_event" ADD CONSTRAINT "fk_bi_alert_event_rule" FOREIGN KEY (rule_id) REFERENCES bi_alert_rule(id) ON DELETE CASCADE;
ALTER TABLE "bi_alert_event_action" ADD CONSTRAINT "fk_bi_alert_event_action_event" FOREIGN KEY (event_id) REFERENCES bi_alert_event(id) ON DELETE CASCADE;
ALTER TABLE "bi_alert_metric_snapshot" ADD CONSTRAINT "fk_bi_alert_metric_snapshot_rule" FOREIGN KEY (rule_id) REFERENCES bi_alert_rule(id) ON DELETE CASCADE;
ALTER TABLE "bi_alert_notification_log" ADD CONSTRAINT "fk_bi_alert_notification_event" FOREIGN KEY (event_id) REFERENCES bi_alert_event(id) ON DELETE CASCADE;
ALTER TABLE "bi_alert_notification_log" ADD CONSTRAINT "fk_bi_alert_notification_rule" FOREIGN KEY (rule_id) REFERENCES bi_alert_rule(id);
ALTER TABLE "bi_alert_notification_log" ADD CONSTRAINT "fk_bi_alert_notification_subscription" FOREIGN KEY (subscription_id) REFERENCES bi_alert_subscription(id);
ALTER TABLE "bi_alert_rule" ADD CONSTRAINT "fk_bi_alert_rule_chart" FOREIGN KEY (chart_id) REFERENCES bi_chart(id);
ALTER TABLE "bi_alert_rule" ADD CONSTRAINT "fk_bi_alert_rule_dataset" FOREIGN KEY (dataset_id) REFERENCES bi_dataset(id);
ALTER TABLE "bi_alert_rule" ADD CONSTRAINT "fk_bi_alert_rule_datasource" FOREIGN KEY (datasource_id) REFERENCES bi_datasource(id);
ALTER TABLE "bi_alert_rule" ADD CONSTRAINT "fk_bi_alert_rule_kpi" FOREIGN KEY (kpi_id) REFERENCES bi_kpi_definition(id);
ALTER TABLE "bi_alert_rule" ADD CONSTRAINT "fk_bi_alert_rule_owner" FOREIGN KEY (owner_user_id) REFERENCES sys_user(id);
ALTER TABLE "bi_alert_subscription" ADD CONSTRAINT "fk_bi_alert_subscription_rule" FOREIGN KEY (rule_id) REFERENCES bi_alert_rule(id) ON DELETE CASCADE;
ALTER TABLE "bi_chart" ADD CONSTRAINT "FK_bi_chart_bi_dataset_dataset_id" FOREIGN KEY (dataset_id) REFERENCES bi_dataset(id) ON DELETE CASCADE;
ALTER TABLE "bi_dataset" ADD CONSTRAINT "FK_bi_dataset_bi_datasource_datasource_id" FOREIGN KEY (datasource_id) REFERENCES bi_datasource(id) ON DELETE CASCADE;
ALTER TABLE "bi_dataset_field" ADD CONSTRAINT "FK_bi_dataset_field_bi_dataset_dataset_id" FOREIGN KEY (dataset_id) REFERENCES bi_dataset(id) ON DELETE CASCADE;
ALTER TABLE "bi_knowledge_category" ADD CONSTRAINT "FK_bi_knowledge_category_bi_knowledge_category_parent_id" FOREIGN KEY (parent_id) REFERENCES bi_knowledge_category(id);
ALTER TABLE "bi_knowledge_chunk" ADD CONSTRAINT "FK_bi_knowledge_chunk_bi_knowledge_document_document_id" FOREIGN KEY (document_id) REFERENCES bi_knowledge_document(id) ON DELETE CASCADE;
ALTER TABLE "bi_knowledge_document" ADD CONSTRAINT "FK_bi_knowledge_document_bi_knowledge_category_category_id" FOREIGN KEY (category_id) REFERENCES bi_knowledge_category(id);
ALTER TABLE "bi_kpi_category" ADD CONSTRAINT "FK_bi_kpi_category_bi_kpi_category_parent_id" FOREIGN KEY (parent_id) REFERENCES bi_kpi_category(id);
ALTER TABLE "bi_kpi_definition" ADD CONSTRAINT "FK_bi_kpi_definition_bi_datasource_datasource_id" FOREIGN KEY (datasource_id) REFERENCES bi_datasource(id);
ALTER TABLE "bi_kpi_definition" ADD CONSTRAINT "FK_bi_kpi_definition_bi_kpi_category_category_id" FOREIGN KEY (category_id) REFERENCES bi_kpi_category(id) ON DELETE CASCADE;
ALTER TABLE "bi_panel_item" ADD CONSTRAINT "FK_bi_panel_item_bi_chart_chart_id" FOREIGN KEY (chart_id) REFERENCES bi_chart(id);
ALTER TABLE "bi_panel_item" ADD CONSTRAINT "FK_bi_panel_item_bi_panel_panel_id" FOREIGN KEY (panel_id) REFERENCES bi_panel(id) ON DELETE CASCADE;
ALTER TABLE "bi_report_item" ADD CONSTRAINT "FK_bi_report_item_bi_chart_chart_id" FOREIGN KEY (chart_id) REFERENCES bi_chart(id);
ALTER TABLE "bi_report_item" ADD CONSTRAINT "FK_bi_report_item_bi_panel_panel_id" FOREIGN KEY (panel_id) REFERENCES bi_panel(id);
ALTER TABLE "bi_report_item" ADD CONSTRAINT "FK_bi_report_item_bi_report_page_page_id" FOREIGN KEY (page_id) REFERENCES bi_report_page(id) ON DELETE CASCADE;
ALTER TABLE "bi_report_page" ADD CONSTRAINT "FK_bi_report_page_bi_report_report_id" FOREIGN KEY (report_id) REFERENCES bi_report(id) ON DELETE CASCADE;
ALTER TABLE "bi_slow_query_log" ADD CONSTRAINT "FK_bi_slow_query_log_bi_chart_chart_id" FOREIGN KEY (chart_id) REFERENCES bi_chart(id);
ALTER TABLE "bi_slow_query_log" ADD CONSTRAINT "FK_bi_slow_query_log_bi_datasource_datasource_id" FOREIGN KEY (datasource_id) REFERENCES bi_datasource(id) ON DELETE CASCADE;
ALTER TABLE "sys_menu" ADD CONSTRAINT "FK_sys_menu_bi_publish_publish_id" FOREIGN KEY (publish_id) REFERENCES bi_publish(id);
ALTER TABLE "sys_menu" ADD CONSTRAINT "FK_sys_menu_sys_menu_SysMenuId" FOREIGN KEY ("SysMenuId") REFERENCES sys_menu(id);
ALTER TABLE "sys_role_menu" ADD CONSTRAINT "FK_sys_role_menu_sys_menu_menu_id" FOREIGN KEY (menu_id) REFERENCES sys_menu(id) ON DELETE CASCADE;
ALTER TABLE "sys_role_menu" ADD CONSTRAINT "FK_sys_role_menu_sys_role_role_id" FOREIGN KEY (role_id) REFERENCES sys_role(id) ON DELETE CASCADE;
ALTER TABLE "sys_user" ADD CONSTRAINT "FK_sys_user_sys_org_org_id" FOREIGN KEY (org_id) REFERENCES sys_org(id);
ALTER TABLE "sys_user_role" ADD CONSTRAINT "FK_sys_user_role_sys_role_role_id" FOREIGN KEY (role_id) REFERENCES sys_role(id) ON DELETE CASCADE;
ALTER TABLE "sys_user_role" ADD CONSTRAINT "FK_sys_user_role_sys_user_user_id" FOREIGN KEY (user_id) REFERENCES sys_user(id) ON DELETE CASCADE;

