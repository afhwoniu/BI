import request from './request'

export interface AlertRuleListItem {
  id: number
  ruleCode: string
  ruleName: string
  ruleType: string
  severityLevel: string
  ruleStatus: string
  datasourceId: number | null
  chartId: number | null
  kpiId: number | null
  lastCheckAt: string | null
  nextCheckAt: string | null
  createdAt: string
  updatedAt: string
}

export interface AlertRuleDetail extends AlertRuleListItem {
  datasetId: number | null
  metricField: string | null
  dimensionField: string | null
  timeField: string | null
  statGranularity: string
  conditionJson: Record<string, unknown>
  calcSql: string | null
  scheduleType: string
  cronExpr: string | null
  intervalSeconds: number
  timezone: string
  dedupMinutes: number
  cooldownMinutes: number
  ownerUserId: number | null
  notifyChannels: string[]
  notifyTemplate: string | null
  remark: string | null
}

export interface AlertRuleUpsertPayload {
  ruleCode?: string
  ruleName: string
  ruleType: string
  severityLevel: string
  ruleStatus: string
  datasourceId?: number | null
  datasetId?: number | null
  chartId?: number | null
  kpiId?: number | null
  metricField?: string | null
  dimensionField?: string | null
  timeField?: string | null
  statGranularity?: string
  conditionJson?: Record<string, unknown>
  calcSql?: string | null
  scheduleType?: string
  cronExpr?: string | null
  intervalSeconds?: number
  timezone?: string
  dedupMinutes?: number
  cooldownMinutes?: number
  ownerUserId?: number | null
  notifyChannels?: string[]
  notifyTemplate?: string | null
  remark?: string | null
}

export interface AlertRuleRunResult {
  triggered: boolean
  eventId: number | null
  message: string
  currentValue: number | null
  baselineValue: number | null
  compareValue: number | null
  changePct: number | null
}

export interface AlertEventListItem {
  id: number
  eventNo: string
  ruleId: number
  ruleName: string
  eventStatus: string
  severityLevel: string
  triggerTime: string
  firstTriggeredAt: string
  lastTriggeredAt: string
  triggerCount: number
  currentValue: number | null
  baselineValue: number | null
  compareValue: number | null
  changePct: number | null
  thresholdDesc: string | null
  dimensionValueJson: Record<string, unknown>
  suggestionText: string | null
  ackBy: number | null
  ackAt: string | null
  resolvedBy: number | null
  resolvedAt: string | null
}

export interface AlertEventDetail extends AlertEventListItem {
  ruleSnapshotJson: Record<string, unknown>
  evidenceJson: Record<string, unknown>
  resolutionNote: string | null
  isNotified: boolean
  notifiedAt: string | null
}

export interface AlertEventActionItem {
  id: number
  eventId: number
  actionType: string
  actionUserId: number | null
  actionNote: string | null
  actionPayload: Record<string, unknown>
  createdAt: string
}

export interface AlertEventHandlePayload {
  note?: string
}

export interface AlertEventActionCreatePayload {
  actionType: string
  actionNote?: string
  actionPayload?: Record<string, unknown>
}

export const getAlertRules = (params: {
  keyword?: string
  ruleStatus?: string
  ruleType?: string
  page?: number
  pageSize?: number
}) => request.get<{ items: AlertRuleListItem[]; total: number; page: number; pageSize: number }>('/alert-rules', { params })

export const getAlertRule = (id: number) => request.get<AlertRuleDetail>(`/alert-rules/${id}`)
export const createAlertRule = (data: AlertRuleUpsertPayload) => request.post<number>('/alert-rules', data)
export const updateAlertRule = (id: number, data: AlertRuleUpsertPayload) => request.put<boolean>(`/alert-rules/${id}`, data)
export const deleteAlertRule = (id: number) => request.delete<boolean>(`/alert-rules/${id}`)
export const enableAlertRule = (id: number) => request.post<boolean>(`/alert-rules/${id}/enable`)
export const disableAlertRule = (id: number) => request.post<boolean>(`/alert-rules/${id}/disable`)
export const runAlertRuleNow = (id: number) => request.post<AlertRuleRunResult>(`/alert-rules/${id}/run`)

export const getAlertEvents = (params: {
  status?: string
  severity?: string
  ruleId?: number
  keyword?: string
  page?: number
  pageSize?: number
}) => request.get<{ items: AlertEventListItem[]; total: number; page: number; pageSize: number }>('/alert-events', { params })

export const getAlertEvent = (id: number) => request.get<AlertEventDetail>(`/alert-events/${id}`)
export const ackAlertEvent = (id: number, data?: AlertEventHandlePayload) => request.post<boolean>(`/alert-events/${id}/ack`, data)
export const resolveAlertEvent = (id: number, data?: AlertEventHandlePayload) => request.post<boolean>(`/alert-events/${id}/resolve`, data)
export const ignoreAlertEvent = (id: number, data?: AlertEventHandlePayload) => request.post<boolean>(`/alert-events/${id}/ignore`, data)
export const closeAlertEvent = (id: number, data?: AlertEventHandlePayload) => request.post<boolean>(`/alert-events/${id}/close`, data)
export const getAlertEventActions = (id: number) => request.get<AlertEventActionItem[]>(`/alert-events/${id}/actions`)
export const addAlertEventAction = (id: number, data: AlertEventActionCreatePayload) => request.post<boolean>(`/alert-events/${id}/actions`, data)
