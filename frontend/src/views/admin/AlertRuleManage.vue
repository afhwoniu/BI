<template>
  <div class="alert-rule-manage">
    <el-card>
      <template #header>
        <div class="card-header">
          <div class="filter-group">
            <el-input v-model="filters.keyword" placeholder="规则名称/编码" clearable style="width: 220px" @keyup.enter="loadRules" />
            <el-select v-model="filters.ruleStatus" placeholder="状态" clearable style="width: 140px">
              <el-option label="启用" value="enabled" />
              <el-option label="禁用" value="disabled" />
            </el-select>
            <el-select v-model="filters.ruleType" placeholder="规则类型" clearable style="width: 160px">
              <el-option label="阈值" value="threshold" />
              <el-option label="环比变化" value="mom_change" />
              <el-option label="同比变化" value="yoy_change" />
              <el-option label="连续触发" value="continuous" />
              <el-option label="自定义SQL" value="custom_sql" />
            </el-select>
            <el-button @click="loadRules">查询</el-button>
          </div>
          <el-button type="primary" @click="openCreateDialog">新增规则</el-button>
        </div>
      </template>

      <el-table :data="rules" v-loading="loading">
        <el-table-column prop="ruleCode" label="规则编码" min-width="180" />
        <el-table-column prop="ruleName" label="规则名称" min-width="180" />
        <el-table-column label="规则类型" width="110">
          <template #default="{ row }">{{ getRuleTypeText(row.ruleType) }}</template>
        </el-table-column>
        <el-table-column label="严重级别" width="100">
          <template #default="{ row }">
            <el-tag :type="getSeverityTag(row.severityLevel)" size="small">{{ getSeverityText(row.severityLevel) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="状态" width="90">
          <template #default="{ row }">
            <el-tag :type="row.ruleStatus === 'enabled' ? 'success' : 'info'" size="small">
              {{ row.ruleStatus === 'enabled' ? '启用' : '禁用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="nextCheckAt" label="下次检查" min-width="170">
          <template #default="{ row }">{{ formatDate(row.nextCheckAt) }}</template>
        </el-table-column>
        <el-table-column prop="updatedAt" label="更新时间" min-width="170">
          <template #default="{ row }">{{ formatDate(row.updatedAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="340" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click="handleRunNow(row)">执行</el-button>
            <el-button v-if="row.ruleStatus === 'enabled'" link @click="handleDisable(row)">禁用</el-button>
            <el-button v-else link type="success" @click="handleEnable(row)">启用</el-button>
            <el-button link @click="openEditDialog(row)">编辑</el-button>
            <el-button link type="danger" @click="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-pagination
        v-model:current-page="pagination.page"
        v-model:page-size="pagination.pageSize"
        :total="pagination.total"
        layout="total, sizes, prev, pager, next"
        :page-sizes="[10, 20, 50]"
        style="margin-top: 16px; justify-content: flex-end"
        @change="loadRules"
      />
    </el-card>

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑预警规则' : '新增预警规则'" width="780px">
      <el-form :model="form" label-width="110px">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="规则名称" required>
              <el-input v-model="form.ruleName" placeholder="例如：门诊量异常升高" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="规则编码">
              <el-input v-model="form.ruleCode" placeholder="可留空自动生成" />
            </el-form-item>
          </el-col>
        </el-row>

        <el-row :gutter="16">
          <el-col :span="8">
            <el-form-item label="规则类型">
              <el-select v-model="form.ruleType" style="width: 100%">
                <el-option label="阈值" value="threshold" />
                <el-option label="环比变化" value="mom_change" />
                <el-option label="同比变化" value="yoy_change" />
                <el-option label="连续触发" value="continuous" />
                <el-option label="自定义SQL" value="custom_sql" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="严重级别">
              <el-select v-model="form.severityLevel" style="width: 100%">
                <el-option label="提示" value="info" />
                <el-option label="预警" value="warning" />
                <el-option label="严重" value="critical" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="规则状态">
              <el-select v-model="form.ruleStatus" style="width: 100%">
                <el-option label="启用" value="enabled" />
                <el-option label="禁用" value="disabled" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>

        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="图表">
              <el-select v-model="form.chartId" clearable filterable style="width: 100%" placeholder="可选，优先用图表指标值">
                <el-option v-for="item in chartOptions" :key="item.id" :label="item.name" :value="item.id" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="数据源">
              <el-select v-model="form.datasourceId" clearable filterable style="width: 100%" placeholder="calcSql 时建议指定">
                <el-option v-for="item in datasourceOptions" :key="item.id" :label="`${item.name} (${item.type})`" :value="item.id" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>

        <el-row :gutter="16">
          <el-col :span="8">
            <el-form-item label="间隔(秒)">
              <el-input-number v-model="form.intervalSeconds" :min="30" :step="30" style="width: 100%" />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="去重(分钟)">
              <el-input-number v-model="form.dedupMinutes" :min="0" :step="5" style="width: 100%" />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="冷却(分钟)">
              <el-input-number v-model="form.cooldownMinutes" :min="0" :step="5" style="width: 100%" />
            </el-form-item>
          </el-col>
        </el-row>

        <div class="condition-box">
          <div class="condition-title">触发条件</div>
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="操作符" label-width="70px">
                <el-select v-model="conditionForm.operator" style="width: 100%">
                  <el-option label=">" value=">" />
                  <el-option label=">=" value=">=" />
                  <el-option label="<" value="<" />
                  <el-option label="<=" value="<=" />
                  <el-option label="=" value="==" />
                  <el-option label="!=" value="!=" />
                </el-select>
              </el-form-item>
            </el-col>

            <el-col v-if="isThresholdType" :span="8">
              <el-form-item label="阈值" label-width="70px">
                <el-input-number v-model="conditionForm.threshold" :step="1" style="width: 100%" />
              </el-form-item>
            </el-col>

            <el-col v-if="isChangeType" :span="8">
              <el-form-item label="变化率%" label-width="70px">
                <el-input-number v-model="conditionForm.changePct" :step="1" style="width: 100%" />
              </el-form-item>
            </el-col>

            <el-col v-if="isContinuousType" :span="8">
              <el-form-item label="阈值" label-width="70px">
                <el-input-number v-model="conditionForm.threshold" :step="1" style="width: 100%" />
              </el-form-item>
            </el-col>

            <el-col v-if="isContinuousType" :span="8">
              <el-form-item label="连续次数" label-width="70px">
                <el-input-number v-model="conditionForm.consecutiveCount" :min="2" :step="1" style="width: 100%" />
              </el-form-item>
            </el-col>
          </el-row>
        </div>

        <el-form-item label="计算SQL">
          <el-input v-model="form.calcSql" type="textarea" :rows="4" placeholder="可填SELECT语句，优先于图表计算" />
        </el-form-item>

        <el-form-item label="通知渠道">
          <el-checkbox-group v-model="notifyChannels">
            <el-checkbox value="inapp">站内</el-checkbox>
            <el-checkbox value="wecom">企微机器人</el-checkbox>
            <el-checkbox value="webhook">Webhook</el-checkbox>
          </el-checkbox-group>
        </el-form-item>

        <el-form-item label="备注">
          <el-input v-model="form.remark" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { AlertRuleDetail, AlertRuleListItem, AlertRuleUpsertPayload } from '@/api/alert'
import * as alertApi from '@/api/alert'
import * as datasourceApi from '@/api/datasource'
import * as chartApi from '@/api/chart'

const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)

const rules = ref<AlertRuleListItem[]>([])
const filters = ref({
  keyword: '',
  ruleStatus: '',
  ruleType: ''
})
const pagination = ref({
  page: 1,
  pageSize: 20,
  total: 0
})

const datasourceOptions = ref<Array<{ id: number; name: string; type: string }>>([])
const chartOptions = ref<Array<{ id: number; name: string }>>([])

const form = ref({
  ruleCode: '',
  ruleName: '',
  ruleType: 'threshold',
  severityLevel: 'warning',
  ruleStatus: 'enabled',
  datasourceId: undefined as number | undefined,
  datasetId: undefined as number | undefined,
  chartId: undefined as number | undefined,
  kpiId: undefined as number | undefined,
  metricField: '',
  dimensionField: '',
  timeField: '',
  statGranularity: 'day',
  calcSql: '',
  scheduleType: 'interval',
  cronExpr: '',
  intervalSeconds: 300,
  timezone: 'Asia/Shanghai',
  dedupMinutes: 60,
  cooldownMinutes: 30,
  ownerUserId: undefined as number | undefined,
  notifyTemplate: '',
  remark: ''
})

const conditionForm = ref({
  operator: '>=',
  threshold: 0,
  changePct: 20,
  consecutiveCount: 3
})

const notifyChannels = ref<string[]>(['inapp'])

const isChangeType = computed(() => form.value.ruleType === 'mom_change' || form.value.ruleType === 'yoy_change')
const isContinuousType = computed(() => form.value.ruleType === 'continuous')
const isThresholdType = computed(() => !isChangeType.value && !isContinuousType.value)

onMounted(() => {
  loadRules()
  loadSelectOptions()
})

async function loadSelectOptions() {
  const [dsRes, chartRes] = await Promise.all([datasourceApi.getDatasourceList(), chartApi.getChartList()])
  if (dsRes.code === 0 && dsRes.data) {
    datasourceOptions.value = dsRes.data.filter(x => x.isEnabled).map(x => ({ id: x.id, name: x.name, type: x.type }))
  }
  if (chartRes.code === 0 && chartRes.data) {
    chartOptions.value = chartRes.data.map(x => ({ id: x.id, name: x.name }))
  }
}

async function loadRules() {
  loading.value = true
  try {
    const res = await alertApi.getAlertRules({
      keyword: filters.value.keyword || undefined,
      ruleStatus: filters.value.ruleStatus || undefined,
      ruleType: filters.value.ruleType || undefined,
      page: pagination.value.page,
      pageSize: pagination.value.pageSize
    })
    if (res.code === 0 && res.data) {
      rules.value = res.data.items
      pagination.value.total = res.data.total
    }
  } finally {
    loading.value = false
  }
}

function resetForm() {
  form.value = {
    ruleCode: '',
    ruleName: '',
    ruleType: 'threshold',
    severityLevel: 'warning',
    ruleStatus: 'enabled',
    datasourceId: undefined,
    datasetId: undefined,
    chartId: undefined,
    kpiId: undefined,
    metricField: '',
    dimensionField: '',
    timeField: '',
    statGranularity: 'day',
    calcSql: '',
    scheduleType: 'interval',
    cronExpr: '',
    intervalSeconds: 300,
    timezone: 'Asia/Shanghai',
    dedupMinutes: 60,
    cooldownMinutes: 30,
    ownerUserId: undefined,
    notifyTemplate: '',
    remark: ''
  }
  conditionForm.value = { operator: '>=', threshold: 0, changePct: 20, consecutiveCount: 3 }
  notifyChannels.value = ['inapp']
}

function openCreateDialog() {
  editingId.value = null
  resetForm()
  dialogVisible.value = true
}

async function openEditDialog(row: AlertRuleListItem) {
  const res = await alertApi.getAlertRule(row.id)
  if (res.code !== 0 || !res.data) {
    ElMessage.error(res.message || '读取规则详情失败')
    return
  }
  fillFormByDetail(res.data)
  editingId.value = row.id
  dialogVisible.value = true
}

function fillFormByDetail(detail: AlertRuleDetail) {
  form.value = {
    ruleCode: detail.ruleCode,
    ruleName: detail.ruleName,
    ruleType: detail.ruleType,
    severityLevel: detail.severityLevel,
    ruleStatus: detail.ruleStatus,
    datasourceId: detail.datasourceId ?? undefined,
    datasetId: detail.datasetId ?? undefined,
    chartId: detail.chartId ?? undefined,
    kpiId: detail.kpiId ?? undefined,
    metricField: detail.metricField ?? '',
    dimensionField: detail.dimensionField ?? '',
    timeField: detail.timeField ?? '',
    statGranularity: detail.statGranularity,
    calcSql: detail.calcSql ?? '',
    scheduleType: detail.scheduleType,
    cronExpr: detail.cronExpr ?? '',
    intervalSeconds: detail.intervalSeconds,
    timezone: detail.timezone,
    dedupMinutes: detail.dedupMinutes,
    cooldownMinutes: detail.cooldownMinutes,
    ownerUserId: detail.ownerUserId ?? undefined,
    notifyTemplate: detail.notifyTemplate ?? '',
    remark: detail.remark ?? ''
  }

  const condition = (detail.conditionJson || {}) as Record<string, unknown>
  conditionForm.value.operator = toStringOr(condition.operator, '>=')
  conditionForm.value.threshold = toNumberOr(condition.threshold, 0)
  conditionForm.value.changePct = toNumberOr(condition.changePct, 20)
  conditionForm.value.consecutiveCount = Math.max(2, toNumberOr(condition.consecutiveCount, 3))

  notifyChannels.value = Array.isArray(detail.notifyChannels) && detail.notifyChannels.length > 0
    ? detail.notifyChannels
    : ['inapp']
}

function buildConditionJson() {
  const base: Record<string, unknown> = {
    operator: conditionForm.value.operator
  }

  if (isChangeType.value) {
    base.changePct = conditionForm.value.changePct
  } else if (isContinuousType.value) {
    base.threshold = conditionForm.value.threshold
    base.consecutiveCount = Math.max(2, conditionForm.value.consecutiveCount)
  } else {
    base.threshold = conditionForm.value.threshold
  }

  return base
}

function parsePayload(): AlertRuleUpsertPayload | null {
  if (!form.value.ruleName.trim()) {
    ElMessage.warning('请填写规则名称')
    return null
  }

  if (!form.value.chartId && !form.value.calcSql.trim()) {
    ElMessage.warning('请至少配置图表或计算SQL')
    return null
  }

  const channels = notifyChannels.value.filter(Boolean)
  if (channels.length === 0) {
    channels.push('inapp')
  }

  return {
    ruleCode: form.value.ruleCode.trim() || undefined,
    ruleName: form.value.ruleName.trim(),
    ruleType: form.value.ruleType,
    severityLevel: form.value.severityLevel,
    ruleStatus: form.value.ruleStatus,
    datasourceId: form.value.datasourceId ?? null,
    datasetId: form.value.datasetId ?? null,
    chartId: form.value.chartId ?? null,
    kpiId: form.value.kpiId ?? null,
    metricField: form.value.metricField.trim() || null,
    dimensionField: form.value.dimensionField.trim() || null,
    timeField: form.value.timeField.trim() || null,
    statGranularity: form.value.statGranularity,
    conditionJson: buildConditionJson(),
    calcSql: form.value.calcSql.trim() || null,
    scheduleType: form.value.scheduleType,
    cronExpr: form.value.cronExpr.trim() || null,
    intervalSeconds: form.value.intervalSeconds,
    timezone: form.value.timezone,
    dedupMinutes: form.value.dedupMinutes,
    cooldownMinutes: form.value.cooldownMinutes,
    ownerUserId: form.value.ownerUserId ?? null,
    notifyChannels: channels,
    notifyTemplate: form.value.notifyTemplate.trim() || null,
    remark: form.value.remark.trim() || null
  }
}

async function handleSave() {
  const payload = parsePayload()
  if (!payload) return

  saving.value = true
  try {
    const res = editingId.value
      ? await alertApi.updateAlertRule(editingId.value, payload)
      : await alertApi.createAlertRule(payload)
    if (res.code === 0) {
      ElMessage.success('保存成功')
      dialogVisible.value = false
      await loadRules()
    } else {
      ElMessage.error(res.message)
    }
  } finally {
    saving.value = false
  }
}

async function handleDelete(row: AlertRuleListItem) {
  await ElMessageBox.confirm(`确认删除规则【${row.ruleName}】？`, '删除确认', { type: 'warning' })
  const res = await alertApi.deleteAlertRule(row.id)
  if (res.code === 0) {
    ElMessage.success('删除成功')
    await loadRules()
  } else {
    ElMessage.error(res.message)
  }
}

async function handleEnable(row: AlertRuleListItem) {
  const res = await alertApi.enableAlertRule(row.id)
  if (res.code === 0) {
    ElMessage.success('规则已启用')
    await loadRules()
  } else {
    ElMessage.error(res.message)
  }
}

async function handleDisable(row: AlertRuleListItem) {
  const res = await alertApi.disableAlertRule(row.id)
  if (res.code === 0) {
    ElMessage.success('规则已禁用')
    await loadRules()
  } else {
    ElMessage.error(res.message)
  }
}

async function handleRunNow(row: AlertRuleListItem) {
  const res = await alertApi.runAlertRuleNow(row.id)
  if (res.code === 0 && res.data) {
    const resultText = res.data.triggered ? '已命中并生成事件' : '未命中'
    ElMessage.success(`${resultText}：${res.data.message}`)
  } else {
    ElMessage.error(res.message)
  }
}

function toStringOr(value: unknown, fallback: string) {
  return typeof value === 'string' && value ? value : fallback
}

function toNumberOr(value: unknown, fallback: number) {
  if (typeof value === 'number' && Number.isFinite(value)) return value
  if (typeof value === 'string') {
    const n = Number(value)
    if (Number.isFinite(n)) return n
  }
  return fallback
}

function formatDate(value: string | null) {
  if (!value) return '-'
  return new Date(value).toLocaleString('zh-CN', { hour12: false })
}

function getRuleTypeText(type: string) {
  if (type === 'threshold') return '阈值'
  if (type === 'mom_change') return '环比'
  if (type === 'yoy_change') return '同比'
  if (type === 'continuous') return '连续'
  if (type === 'custom_sql') return 'SQL'
  return type
}

function getSeverityTag(level: string) {
  if (level === 'critical') return 'danger'
  if (level === 'warning') return 'warning'
  return 'info'
}

function getSeverityText(level: string) {
  if (level === 'critical') return '严重'
  if (level === 'warning') return '预警'
  return '提示'
}
</script>

<style scoped>
.alert-rule-manage {
  padding: 16px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.filter-group {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.condition-box {
  padding: 10px 12px;
  border: 1px solid #ebeef5;
  border-radius: 6px;
  margin-bottom: 14px;
}

.condition-title {
  font-size: 13px;
  color: #606266;
  margin-bottom: 8px;
}
</style>
