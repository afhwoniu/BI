<template>
  <div class="alert-event-center">
    <el-card>
      <template #header>
        <div class="card-header">
          <div class="filter-group">
            <el-input v-model="filters.keyword" placeholder="事件号/规则名" clearable style="width: 220px" @keyup.enter="loadEvents" />
            <el-select v-model="filters.status" placeholder="状态" clearable style="width: 140px">
              <el-option label="待处理" value="open" />
              <el-option label="已确认" value="acknowledged" />
              <el-option label="已解决" value="resolved" />
              <el-option label="已忽略" value="ignored" />
              <el-option label="已关闭" value="closed" />
            </el-select>
            <el-select v-model="filters.severity" placeholder="级别" clearable style="width: 120px">
              <el-option label="提示" value="info" />
              <el-option label="预警" value="warning" />
              <el-option label="严重" value="critical" />
            </el-select>
            <el-input-number v-model="filters.ruleId" :min="1" :step="1" placeholder="规则ID" style="width: 130px" />
            <el-button @click="loadEvents">查询</el-button>
          </div>
        </div>
      </template>

      <el-table :data="events" v-loading="loading">
        <el-table-column prop="eventNo" label="事件号" min-width="210" />
        <el-table-column prop="ruleName" label="规则名称" min-width="180" />
        <el-table-column label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="getStatusTag(row.eventStatus)" size="small">{{ getStatusText(row.eventStatus) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="级别" width="90">
          <template #default="{ row }">
            <el-tag :type="getSeverityTag(row.severityLevel)" size="small">{{ getSeverityText(row.severityLevel) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="thresholdDesc" label="命中条件" min-width="200" />
        <el-table-column prop="currentValue" label="当前值" min-width="100" />
        <el-table-column prop="triggerCount" label="触发次数" width="90" />
        <el-table-column label="触发时间" min-width="170">
          <template #default="{ row }">{{ formatDate(row.triggerTime) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="360" fixed="right">
          <template #default="{ row }">
            <el-button link @click="showDetail(row)">详情</el-button>
            <el-button link type="primary" @click="handleAck(row)" :disabled="row.eventStatus !== 'open'">确认</el-button>
            <el-button link type="success" @click="handleResolve(row)" :disabled="!canResolve(row.eventStatus)">解决</el-button>
            <el-button link type="warning" @click="handleIgnore(row)" :disabled="!canResolve(row.eventStatus)">忽略</el-button>
            <el-button link type="danger" @click="handleClose(row)" :disabled="row.eventStatus === 'closed'">关闭</el-button>
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
        @change="loadEvents"
      />
    </el-card>

    <el-drawer v-model="detailVisible" title="预警事件详情" size="50%">
      <template v-if="currentDetail">
        <el-descriptions :column="2" border>
          <el-descriptions-item label="事件号">{{ currentDetail.eventNo }}</el-descriptions-item>
          <el-descriptions-item label="规则">{{ currentDetail.ruleName }}</el-descriptions-item>
          <el-descriptions-item label="状态">{{ getStatusText(currentDetail.eventStatus) }}</el-descriptions-item>
          <el-descriptions-item label="级别">{{ getSeverityText(currentDetail.severityLevel) }}</el-descriptions-item>
          <el-descriptions-item label="触发时间">{{ formatDate(currentDetail.triggerTime) }}</el-descriptions-item>
          <el-descriptions-item label="触发次数">{{ currentDetail.triggerCount }}</el-descriptions-item>
          <el-descriptions-item label="当前值">{{ currentDetail.currentValue ?? '-' }}</el-descriptions-item>
          <el-descriptions-item label="变化率">{{ formatPct(currentDetail.changePct) }}</el-descriptions-item>
          <el-descriptions-item label="命中条件" :span="2">{{ currentDetail.thresholdDesc || '-' }}</el-descriptions-item>
          <el-descriptions-item label="处理建议" :span="2">{{ currentDetail.suggestionText || '-' }}</el-descriptions-item>
          <el-descriptions-item label="处置备注" :span="2">{{ currentDetail.resolutionNote || '-' }}</el-descriptions-item>
        </el-descriptions>

        <div class="json-block">
          <div class="json-title">证据快照</div>
          <pre>{{ prettyJson(currentDetail.evidenceJson) }}</pre>
        </div>

        <div class="json-block">
          <div class="json-title">动作轨迹</div>
          <div class="action-toolbar">
            <el-button size="small" @click="handleAddComment">追加备注</el-button>
          </div>
          <el-table :data="actions" size="small" style="margin-top: 10px">
            <el-table-column prop="actionType" label="动作类型" width="120" />
            <el-table-column prop="actionUserId" label="操作人ID" width="100" />
            <el-table-column prop="actionNote" label="备注" min-width="180" />
            <el-table-column label="时间" min-width="170">
              <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
            </el-table-column>
          </el-table>
        </div>
      </template>
    </el-drawer>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { AlertEventActionItem, AlertEventDetail, AlertEventListItem } from '@/api/alert'
import * as alertApi from '@/api/alert'

const loading = ref(false)
const events = ref<AlertEventListItem[]>([])
const pagination = ref({
  page: 1,
  pageSize: 20,
  total: 0
})
const filters = ref({
  keyword: '',
  status: '',
  severity: '',
  ruleId: undefined as number | undefined
})

const detailVisible = ref(false)
const currentDetail = ref<AlertEventDetail | null>(null)
const actions = ref<AlertEventActionItem[]>([])

onMounted(() => {
  loadEvents()
})

async function loadEvents() {
  loading.value = true
  try {
    const res = await alertApi.getAlertEvents({
      keyword: filters.value.keyword || undefined,
      status: filters.value.status || undefined,
      severity: filters.value.severity || undefined,
      ruleId: filters.value.ruleId,
      page: pagination.value.page,
      pageSize: pagination.value.pageSize
    })
    if (res.code === 0 && res.data) {
      events.value = res.data.items
      pagination.value.total = res.data.total
    }
  } finally {
    loading.value = false
  }
}

async function showDetail(row: AlertEventListItem) {
  const [detailRes, actionRes] = await Promise.all([
    alertApi.getAlertEvent(row.id),
    alertApi.getAlertEventActions(row.id)
  ])

  if (detailRes.code !== 0 || !detailRes.data) {
    ElMessage.error(detailRes.message || '读取事件详情失败')
    return
  }

  currentDetail.value = detailRes.data
  actions.value = actionRes.code === 0 && actionRes.data ? actionRes.data : []
  detailVisible.value = true
}

async function handleAck(row: AlertEventListItem) {
  const note = await askNote('确认事件')
  if (note === null) return
  const res = await alertApi.ackAlertEvent(row.id, { note })
  handleActionResult(res.code, res.message, '已确认')
}

async function handleResolve(row: AlertEventListItem) {
  const note = await askNote('解决事件')
  if (note === null) return
  const res = await alertApi.resolveAlertEvent(row.id, { note })
  handleActionResult(res.code, res.message, '已解决')
}

async function handleIgnore(row: AlertEventListItem) {
  const note = await askNote('忽略事件')
  if (note === null) return
  const res = await alertApi.ignoreAlertEvent(row.id, { note })
  handleActionResult(res.code, res.message, '已忽略')
}

async function handleClose(row: AlertEventListItem) {
  const note = await askNote('关闭事件')
  if (note === null) return
  const res = await alertApi.closeAlertEvent(row.id, { note })
  handleActionResult(res.code, res.message, '已关闭')
}

async function handleAddComment() {
  if (!currentDetail.value) return
  const note = await askNote('追加备注')
  if (note === null) return

  const res = await alertApi.addAlertEventAction(currentDetail.value.id, {
    actionType: 'comment',
    actionNote: note,
    actionPayload: { source: 'ui' }
  })

  if (res.code === 0) {
    ElMessage.success('备注已追加')
    const actionRes = await alertApi.getAlertEventActions(currentDetail.value.id)
    actions.value = actionRes.code === 0 && actionRes.data ? actionRes.data : []
  } else {
    ElMessage.error(res.message)
  }
}

function handleActionResult(code: number, message: string, successText: string) {
  if (code === 0) {
    ElMessage.success(successText)
    loadEvents()
    if (currentDetail.value) {
      showDetail({ ...currentDetail.value })
    }
  } else {
    ElMessage.error(message)
  }
}

async function askNote(title: string): Promise<string | null> {
  try {
    const result = await ElMessageBox.prompt('请输入处理备注（可留空）', title, {
      inputPlaceholder: '例如：已通知责任科室复核数据',
      confirmButtonText: '确定',
      cancelButtonText: '取消'
    })
    return result.value?.trim() || ''
  } catch {
    return null
  }
}

function canResolve(status: string) {
  return status === 'open' || status === 'acknowledged'
}

function formatDate(value: string | null) {
  if (!value) return '-'
  return new Date(value).toLocaleString('zh-CN', { hour12: false })
}

function formatPct(value: number | null) {
  if (value === null || value === undefined) return '-'
  return `${(value * 100).toFixed(2)}%`
}

function getStatusTag(status: string) {
  if (status === 'open') return 'danger'
  if (status === 'acknowledged') return 'warning'
  if (status === 'resolved') return 'success'
  if (status === 'ignored') return 'info'
  return undefined
}

function getStatusText(status: string) {
  if (status === 'open') return '待处理'
  if (status === 'acknowledged') return '已确认'
  if (status === 'resolved') return '已解决'
  if (status === 'ignored') return '已忽略'
  if (status === 'closed') return '已关闭'
  return status
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

function prettyJson(data: Record<string, unknown>) {
  return JSON.stringify(data || {}, null, 2)
}
</script>

<style scoped>
.alert-event-center {
  padding: 16px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.filter-group {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.json-block {
  margin-top: 16px;
}

.json-title {
  font-weight: 600;
  margin-bottom: 8px;
}

.json-block pre {
  background: #f6f8fa;
  border: 1px solid #ebeef5;
  border-radius: 6px;
  padding: 10px;
  overflow-x: auto;
}

.action-toolbar {
  display: flex;
  justify-content: flex-end;
}
</style>
