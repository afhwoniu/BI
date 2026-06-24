# BI预警模块设计方案（V1）

> 适配系统：智能BI可视化系统V5  
> 目标：先实现“可配置规则 + 自动触发 + 通知 + 闭环处置”

## 1. 模块范围

V1先做“预警”闭环，不把复杂预测模型绑死到第一期：

1. 规则管理：阈值、同比/环比波动、连续异常规则。
2. 定时检测：按周期执行规则，生成预警事件。
3. 消息通知：站内/企业微信/短信/邮件（可扩展）。
4. 事件闭环：确认、指派、处理、关闭、复盘。
5. 运行留痕：通知日志、处置日志、指标快照。

## 2. 业务流程

1. 管理员创建预警规则并绑定数据对象（数据源/指标/图表）。
2. 调度任务按规则周期执行（每5分钟、每小时、每天等）。
3. 引擎计算当前值与基线值，判断是否触发。
4. 触发后生成事件，按订阅关系发送通知。
5. 业务人员在预警中心做确认与处置，系统记录处置轨迹。
6. 关闭事件后可回看规则快照与处理时效。

## 3. 表设计（命名与现有系统一致）

预警模块新增6张表：

1. `bi_alert_rule`：预警规则主表  
2. `bi_alert_event`：预警事件表  
3. `bi_alert_event_action`：事件处置动作表  
4. `bi_alert_subscription`：预警订阅表  
5. `bi_alert_notification_log`：通知发送日志表  
6. `bi_alert_metric_snapshot`：指标快照表

说明：

1. 所有表均使用 `id/created_at/updated_at`，与现有实体基类一致。
2. 规则支持JSON条件配置，便于后续扩展异常检测与预测预警。
3. 事件表保留 `rule_snapshot_json`，避免规则修改后历史不可追溯。

## 4. 关键字段说明

### 4.1 规则类型（`rule_type`）

1. `threshold`：静态阈值预警（如床位使用率 > 95%）。
2. `mom_change`：环比波动预警（如较上期增长 > 20%）。
3. `yoy_change`：同比波动预警。
4. `continuous`：连续N期异常预警。
5. `custom_sql`：自定义SQL返回单值后判断。

### 4.2 事件状态（`event_status`）

1. `open`：待处理
2. `acknowledged`：已确认
3. `resolved`：已解决
4. `ignored`：已忽略
5. `closed`：已关闭

### 4.3 严重级别（`severity_level`）

1. `info`
2. `warning`
3. `critical`
4. `emergency`

## 5. 条件JSON建议结构

`bi_alert_rule.condition_json` 示例：

```json
{
  "operator": ">=",
  "threshold": 95,
  "unit": "%",
  "compare": {
    "type": "none",
    "window": "day"
  },
  "dedup": {
    "dedupMinutes": 60,
    "cooldownMinutes": 30
  }
}
```

`mom_change` 示例：

```json
{
  "operator": ">=",
  "changePct": 20,
  "basePeriod": "previous_day",
  "minBaseValue": 10
}
```

## 6. 接口草案

1. `GET /api/v1/alert-rules`：分页查询规则
2. `POST /api/v1/alert-rules`：创建规则
3. `PUT /api/v1/alert-rules/{id}`：更新规则
4. `POST /api/v1/alert-rules/{id}/enable`：启用规则
5. `POST /api/v1/alert-rules/{id}/disable`：停用规则
6. `POST /api/v1/alert-rules/{id}/run`：手工执行规则
7. `GET /api/v1/alert-events`：分页查询事件
8. `POST /api/v1/alert-events/{id}/ack`：确认事件
9. `POST /api/v1/alert-events/{id}/resolve`：处理完成
10. `POST /api/v1/alert-events/{id}/ignore`：忽略事件
11. `GET /api/v1/alert-events/{id}/actions`：处置轨迹
12. `GET /api/v1/alert-dashboard/summary`：预警概览

## 7. 与现有模块关联

1. `datasource_id` 关联 `bi_datasource.id`
2. `dataset_id` 关联 `bi_dataset.id`（可选）
3. `chart_id` 关联 `bi_chart.id`（可选）
4. `kpi_id` 关联 `bi_kpi_definition.id`（可选）
5. `owner_user_id`、`ack_by`、`resolved_by` 关联 `sys_user.id`

## 8. 调度与执行建议

1. 使用后台定时服务扫描 `bi_alert_rule` 中启用规则。
2. 规则执行后写入 `bi_alert_metric_snapshot`。
3. 命中后生成/更新 `bi_alert_event`，并写 `bi_alert_notification_log`。
4. 同规则同维度在去重窗口内只保留1条活跃事件（防告警风暴）。

## 9. 下一步（V2）

V2可在本设计上扩展“预知预判”：

1. 增加 `bi_alert_forecast_result`（预测结果表）。
2. 在事件中记录“预计越界时间”和“风险概率”。
3. 引入建议动作模板（资源调度、成本控制、重点科室排班）。

---

附：完整DDL见同目录文件 `BI预警模块DDL.sql`。
