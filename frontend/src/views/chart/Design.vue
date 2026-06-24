<script setup lang="ts">
import { ref, onMounted, watch, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Close } from '@element-plus/icons-vue'
import * as echarts from 'echarts'
import { getDatasetList, getDatasetDetail, type Dataset, type DatasetField } from '@/api/dataset'
import { getChartDetail, createChart, updateChart, queryChart } from '@/api/chart'

const route = useRoute()
const router = useRouter()
const chartId = ref<number | null>(null)
const chartRef = ref<HTMLDivElement>()
let chartInstance: echarts.ECharts | null = null

const datasetList = ref<Dataset[]>([])
const fields = ref<DatasetField[]>([])
const saving = ref(false)
const querying = ref(false)
const hasData = ref(false) // 标记是否有图表数据

// 保存上次查询结果，用于切换图表类型时重新渲染
const lastQueryResult = ref<{ categories: string[]; series: { name: string; data: any[] }[] } | null>(null)

// 表单：图表配置
const form = ref({
  name: '',
  datasetId: 0,
  chartType: 'bar' as string,
  remark: ''
})

// 维度列表
const dimensions = ref<{ field: string; alias: string }[]>([])
// 度量列表（带聚合方式）
const measures = ref<{ field: string; alias: string; aggType: string }[]>([])
// 预筛选列表
const preFilters = ref<{ field: string; operator: string; value: string; enabled: boolean }[]>([])

// 同环比配置
const compareConfig = ref({
  yoyEnabled: false,  // 同比
  momEnabled: false,  // 环比
  dateField: '',      // 日期字段
  dateGranularity: 'month' // 日期粒度
})

// 聚合类型选项
const aggOptions = [
  { label: 'SUM', value: 'sum' },
  { label: 'COUNT', value: 'count' },
  { label: 'AVG', value: 'avg' },
  { label: 'MAX', value: 'max' },
  { label: 'MIN', value: 'min' }
]

// 筛选操作符选项
const operatorOptions = [
  { label: '等于 =', value: '=' },
  { label: '不等于 !=', value: '!=' },
  { label: '大于 >', value: '>' },
  { label: '小于 <', value: '<' },
  { label: '大于等于 >=', value: '>=' },
  { label: '小于等于 <=', value: '<=' },
  { label: '包含 LIKE', value: 'like' },
  { label: '在列表中 IN', value: 'in' },
  { label: '非空', value: 'notnull' },
  { label: '为空', value: 'isnull' }
]

onMounted(async () => {
  await loadDatasets()
  if (route.params.id) {
    chartId.value = Number(route.params.id)
    await loadChart(chartId.value)
  }
})

watch(() => form.value.datasetId, async (newVal, oldVal) => {
  if (newVal) {
    await loadDatasetFields(newVal)
    // 只在切换数据集时清空（编辑时不清空）
    if (oldVal && oldVal !== newVal) {
      dimensions.value = []
      measures.value = []
      preFilters.value = []
      hasData.value = false
      clearChart()
    }
  } else {
    fields.value = []
  }
})

// 监听图表类型变化，自动重新渲染（无需重新查询）
watch(() => form.value.chartType, () => {
  if (lastQueryResult.value) {
    nextTick(() => {
      if (form.value.chartType !== 'kpi' && chartInstance) {
        chartInstance.clear()
        chartInstance.dispose()
        chartInstance = null
      }
      renderChart(lastQueryResult.value!.categories, lastQueryResult.value!.series)
    })
  }
})

async function loadDatasets() {
  try {
    const res = await getDatasetList()
    if (res.code === 0) {
      datasetList.value = res.data || []
    }
  } catch (e) {
    console.error(e)
  }
}

async function loadDatasetFields(datasetId: number) {
  try {
    const res = await getDatasetDetail(datasetId)
    if (res.code === 0 && res.data) {
      fields.value = res.data.fields || []
    }
  } catch (e) {
    console.error(e)
  }
}

async function loadChart(id: number) {
  try {
    const res = await getChartDetail(id)
    if (res.code === 0 && res.data) {
      const config = JSON.parse(res.data.configJson || '{}')
      form.value = {
        name: res.data.name,
        datasetId: res.data.datasetId,
        chartType: res.data.chartType,
        remark: res.data.remark || ''
      }
      dimensions.value = config.dimensions || []
      measures.value = config.measures || []
      preFilters.value = config.preFilters || []
      // 加载同环比配置
      if (config.compare) {
        compareConfig.value = {
          yoyEnabled: config.compare.yoyEnabled || false,
          momEnabled: config.compare.momEnabled || false,
          dateField: config.compare.dateField || '',
          dateGranularity: config.compare.dateGranularity || 'month'
        }
      }
      if (res.data.datasetId) {
        await loadDatasetFields(res.data.datasetId)
      }
      // 加载完成后自动查询
      await nextTick()
      setTimeout(() => handleQuery(false), 300)
    }
  } catch (e) {
    console.error(e)
  }
}

// 添加到维度
function addToDimension(field: DatasetField) {
  if (!dimensions.value.find(d => d.field === field.fieldName)) {
    dimensions.value.push({
      field: field.fieldName,
      alias: field.fieldAlias || field.fieldName
    })
  }
}

// 添加到度量
function addToMeasure(field: DatasetField) {
  if (!measures.value.find(m => m.field === field.fieldName)) {
    measures.value.push({
      field: field.fieldName,
      alias: field.fieldAlias || field.fieldName,
      aggType: 'sum'
    })
  }
}

function removeDimension(index: number) {
  dimensions.value.splice(index, 1)
}

function removeMeasure(index: number) {
  measures.value.splice(index, 1)
}

// 添加预筛选条件
function addPreFilter() {
  preFilters.value.push({
    field: '',
    operator: '=',
    value: '',
    enabled: true
  })
}

// 删除预筛选条件
function removePreFilter(index: number) {
  preFilters.value.splice(index, 1)
}

// 刷新预览 - showSaveMsg控制是否显示保存提示
async function handleQuery(showSaveMsg = true) {
  if (!form.value.datasetId) {
    ElMessage.warning('请先选择数据集')
    return
  }
  if (dimensions.value.length === 0 || measures.value.length === 0) {
    ElMessage.warning('请添加至少一个维度和度量')
    return
  }
  if (!form.value.name) {
    ElMessage.warning('请填写图表名称')
    return
  }

  // 保存配置
  await doSave(showSaveMsg)
  if (!chartId.value) return

  querying.value = true
  try {
    const res = await queryChart(chartId.value)
    if (res.code === 0 && res.data) {
      hasData.value = true
      // 保存查询结果以便切换图表类型时重新渲染
      lastQueryResult.value = { categories: res.data.categories, series: res.data.series }
      await nextTick()
      renderChart(res.data.categories, res.data.series)
    } else {
      ElMessage.error(res.message || '查询失败')
    }
  } catch (e) {
    console.error(e)
  } finally {
    querying.value = false
  }
}

function clearChart() {
  if (chartInstance) {
    chartInstance.clear()
  }
}

function renderChart(categories: string[], series: { name: string; data: any[] }[]) {
  if (!chartRef.value) return

  // KPI卡片模式：显示数值指标
  if (form.value.chartType === 'kpi') {
    if (chartInstance) { chartInstance.clear(); chartInstance.dispose(); chartInstance = null }
    const kpiHtml = series.length === 0 && categories.length === 0
      ? '<div style="text-align:center;color:#999;padding:40px">暂无数据</div>'
      : `<div style="display:flex;flex-wrap:wrap;gap:16px;justify-content:center;padding:20px">
          ${series.map((s, i) => {
            const val = s.data.length > 0 ? s.data[0] : 0
            return `<div style="text-align:center;min-width:120px;padding:16px 24px;background:linear-gradient(135deg,#${['667eea','764ba2','43e97b','fa709a','fee140'][i%5]},#${['764ba2','667eea','38f9d7','fee140','fa709a'][i%5]});border-radius:8px;color:#fff">
              <div style="font-size:28px;font-weight:bold">${Number(val).toLocaleString()}</div>
              <div style="font-size:13px;margin-top:4px;opacity:0.9">${s.name}</div>
            </div>`
          }).join('')}
        </div>`
    chartRef.value.innerHTML = kpiHtml
    return
  }

  if (!chartInstance) {
    chartInstance = echarts.init(chartRef.value)
  }

  const colors = ['#5470c6', '#91cc75', '#fac858', '#ee6666', '#73c0de', '#3ba272', '#fc8452', '#9a60b4']
  const option: echarts.EChartsOption = {
    color: colors,
    tooltip: { trigger: form.value.chartType === 'pie' ? 'item' : 'axis' },
    legend: { bottom: 10, type: 'scroll' },
    grid: { left: '3%', right: '4%', bottom: '15%', top: '10%', containLabel: true },
    xAxis: form.value.chartType === 'pie' ? undefined : {
      type: 'category',
      data: categories,
      axisLabel: { rotate: categories.length > 8 ? 30 : 0 }
    },
    yAxis: form.value.chartType === 'pie' ? undefined : { type: 'value' },
    series: series.map((s, idx) => ({
      name: s.name,
      type: form.value.chartType as 'bar' | 'line' | 'pie',
      data: form.value.chartType === 'pie'
        ? categories.map((c, i) => ({ name: c, value: s.data[i] }))
        : s.data,
      itemStyle: { color: colors[idx % colors.length] },
      label: form.value.chartType === 'pie' ? { show: true, formatter: '{b}: {d}%' } : undefined
    }))
  }
  chartInstance.setOption(option, true)
}

async function doSave(showMsg = true) {
  if (!form.value.name || !form.value.datasetId) {
    ElMessage.warning('请填写图表名称并选择数据集')
    return
  }
  saving.value = true
  try {
    // 构建配置JSON，包含同环比配置
    const configObj: any = {
      dimensions: dimensions.value,
      measures: measures.value,
      preFilters: preFilters.value.filter(f => f.field) // 只保存有字段的筛选
    }
    // 如果启用了同环比，添加配置
    if (compareConfig.value.yoyEnabled || compareConfig.value.momEnabled) {
      configObj.compare = {
        yoyEnabled: compareConfig.value.yoyEnabled,
        momEnabled: compareConfig.value.momEnabled,
        dateField: compareConfig.value.dateField,
        dateGranularity: compareConfig.value.dateGranularity
      }
    }
    const configJson = JSON.stringify(configObj)
    const payload = {
      name: form.value.name,
      datasetId: form.value.datasetId,
      chartType: form.value.chartType,
      configJson,
      remark: form.value.remark
    }
    if (chartId.value) {
      const res = await updateChart(chartId.value, payload)
      if (res.code === 0) {
        if (showMsg) ElMessage.success('保存成功')
      } else {
        ElMessage.error(res.message || '保存失败')
      }
    } else {
      const res = await createChart(payload)
      if (res.code === 0) {
        chartId.value = res.data as number
        if (showMsg) ElMessage.success('创建成功')
      } else {
        ElMessage.error(res.message || '创建失败')
      }
    }
  } catch (e) {
    console.error(e)
  } finally {
    saving.value = false
  }
}

function goBack() {
  router.push({ name: 'Chart' })
}
</script>

<template>
  <div class="chart-design">
    <!-- 顶部导航 -->
    <div class="design-header">
      <div class="header-left">
        <el-button text @click="goBack">
          <span>← 返回</span>
        </el-button>
        <el-divider direction="vertical" />
        <span class="header-title">{{ chartId ? '编辑图表' : '新建图表' }}</span>
      </div>
    </div>

    <div class="design-container">
      <!-- 左栏：可用字段 -->
      <div class="left-panel">
        <el-card shadow="never">
          <template #header>
            <span class="panel-title">可用字段</span>
          </template>
          <div v-if="!form.datasetId" class="field-tip">
            <el-icon :size="32" color="#dcdfe6"><svg viewBox="0 0 1024 1024"><path fill="currentColor" d="M512 64a448 448 0 1 1 0 896 448 448 0 0 1 0-896zm0 832a384 384 0 1 0 0-768 384 384 0 0 0 0 768zm48-176a48 48 0 1 1-96 0 48 48 0 0 1 96 0zm-48-464a32 32 0 0 1 32 32v288a32 32 0 0 1-64 0V288a32 32 0 0 1 32-32z"/></svg></el-icon>
            <p>请先在右侧选择数据集</p>
          </div>
          <div v-else-if="fields.length === 0" class="field-tip">
            <p>该数据集暂无字段</p>
          </div>
          <div v-else class="field-list">
            <div v-for="f in fields" :key="f.fieldName" class="field-row">
              <div class="field-left">
                <span class="field-icon">📁</span>
                <span class="field-name">{{ f.fieldName }}</span>
                <el-tag size="small" type="info" effect="plain">{{ f.dataType }}</el-tag>
              </div>
              <div class="field-btns">
                <span class="field-btn dim-btn" @click="addToDimension(f)">维度</span>
                <span class="field-btn measure-btn" @click="addToMeasure(f)">度量</span>
              </div>
            </div>
          </div>
        </el-card>
      </div>

      <!-- 中间：图表预览 -->
      <div class="center-panel">
        <el-card shadow="never" class="preview-card">
          <template #header>
            <div class="preview-header">
              <span class="panel-title">图表预览</span>
              <div class="preview-actions">
                <el-button :loading="querying" @click="handleQuery()">刷新预览</el-button>
                <el-button type="primary" :loading="saving" @click="doSave()">保存</el-button>
              </div>
            </div>
          </template>

          <!-- 维度/度量配置区 -->
          <div class="config-area">
            <!-- 维度 -->
            <div class="config-row">
              <div class="config-label">维度 (X轴/分类)</div>
              <div class="config-content">
                <span v-if="dimensions.length === 0" class="config-hint">点击左侧字段的"维度"添加</span>
                <el-tag
                  v-for="(dim, i) in dimensions"
                  :key="dim.field"
                  closable
                  type="primary"
                  effect="light"
                  @close="removeDimension(i)"
                >
                  {{ dim.alias }}
                </el-tag>
              </div>
            </div>

            <!-- 度量 -->
            <div class="config-row">
              <div class="config-label">度量 (Y轴/数值)</div>
              <div class="config-content">
                <span v-if="measures.length === 0" class="config-hint">点击左侧字段的"度量"添加</span>
                <div v-for="(m, i) in measures" :key="m.field" class="measure-item">
                  <el-tag type="success" effect="light" class="measure-name">{{ m.alias }}</el-tag>
                  <el-select v-model="m.aggType" size="small" class="agg-select">
                    <el-option v-for="opt in aggOptions" :key="opt.value" :label="opt.label" :value="opt.value" />
                  </el-select>
                  <el-icon class="remove-icon" @click="removeMeasure(i)"><Close /></el-icon>
                </div>
              </div>
            </div>
          </div>

          <!-- 图表区域 -->
          <div class="chart-area">
            <div v-if="!hasData && (dimensions.length === 0 || measures.length === 0)" class="chart-empty">
              <img src="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 150'%3E%3Crect fill='%23f5f7fa' width='200' height='150'/%3E%3Crect x='30' y='100' width='20' height='40' fill='%23dcdfe6'/%3E%3Crect x='60' y='70' width='20' height='70' fill='%23dcdfe6'/%3E%3Crect x='90' y='50' width='20' height='90' fill='%23dcdfe6'/%3E%3Crect x='120' y='80' width='20' height='60' fill='%23dcdfe6'/%3E%3Crect x='150' y='60' width='20' height='80' fill='%23dcdfe6'/%3E%3C/svg%3E" alt="chart placeholder" />
              <p>添加维度和度量后点击刷新预览</p>
            </div>
            <div v-show="hasData || (dimensions.length > 0 && measures.length > 0)" ref="chartRef" class="chart-canvas"></div>
          </div>
        </el-card>
      </div>

      <!-- 右栏：图表配置 -->
      <div class="right-panel">
        <el-card shadow="never">
          <template #header>
            <span class="panel-title">图表配置</span>
          </template>
          <el-form label-position="top" size="default">
            <el-form-item label="* 图表名称">
              <el-input v-model="form.name" placeholder="输入图表名称" />
            </el-form-item>
            <el-form-item label="* 数据集">
              <el-select v-model="form.datasetId" placeholder="选择数据集" style="width: 100%">
                <el-option v-for="ds in datasetList" :key="ds.id" :label="ds.name" :value="ds.id" />
              </el-select>
            </el-form-item>
            <el-form-item label="图表类型">
              <el-radio-group v-model="form.chartType" class="chart-type-group">
                <el-radio-button value="bar">柱状图</el-radio-button>
                <el-radio-button value="line">折线图</el-radio-button>
                <el-radio-button value="pie">饼图</el-radio-button>
                <el-radio-button value="kpi">KPI卡片</el-radio-button>
              </el-radio-group>
            </el-form-item>
            <el-form-item label="备注">
              <el-input v-model="form.remark" type="textarea" :rows="2" placeholder="备注说明" />
            </el-form-item>
          </el-form>

          <!-- 预筛选配置 -->
          <el-divider content-position="left">预筛选条件</el-divider>
          <div class="pre-filter-list">
            <div v-for="(filter, idx) in preFilters" :key="idx" class="pre-filter-item">
              <el-select v-model="filter.field" placeholder="字段" size="small" style="width: 100%; margin-bottom: 4px">
                <el-option v-for="f in fields" :key="f.fieldName" :label="f.fieldAlias || f.fieldName" :value="f.fieldName" />
              </el-select>
              <div class="filter-row">
                <el-select v-model="filter.operator" size="small" style="width: 90px">
                  <el-option v-for="op in operatorOptions" :key="op.value" :label="op.label" :value="op.value" />
                </el-select>
                <el-input
                  v-if="!['notnull', 'isnull'].includes(filter.operator)"
                  v-model="filter.value"
                  placeholder="值"
                  size="small"
                  style="flex: 1; margin-left: 4px"
                />
                <el-switch v-model="filter.enabled" size="small" style="margin-left: 4px" />
                <el-icon class="remove-filter" @click="removePreFilter(idx)"><Close /></el-icon>
              </div>
            </div>
            <el-button type="primary" link size="small" @click="addPreFilter">+ 添加筛选条件</el-button>
          </div>

          <!-- 同环比配置 -->
          <el-divider content-position="left">同环比分析</el-divider>
          <div class="compare-config">
            <el-form-item label="日期字段" size="small">
              <el-select v-model="compareConfig.dateField" placeholder="选择日期字段" style="width: 100%" clearable>
                <el-option v-for="f in fields" :key="f.fieldName" :label="f.fieldAlias || f.fieldName" :value="f.fieldName" />
              </el-select>
            </el-form-item>
            <el-form-item label="日期粒度" size="small">
              <el-select v-model="compareConfig.dateGranularity" style="width: 100%">
                <el-option label="日" value="day" />
                <el-option label="周" value="week" />
                <el-option label="月" value="month" />
                <el-option label="季度" value="quarter" />
                <el-option label="年" value="year" />
              </el-select>
            </el-form-item>
            <div class="compare-switches">
              <el-checkbox v-model="compareConfig.yoyEnabled" :disabled="!compareConfig.dateField">同比（去年同期）</el-checkbox>
              <el-checkbox v-model="compareConfig.momEnabled" :disabled="!compareConfig.dateField">环比（上期）</el-checkbox>
            </div>
          </div>
        </el-card>
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
.chart-design {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: #f0f2f5;
}

.design-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 20px;
  background: #fff;
  border-bottom: 1px solid #e4e7ed;
  .header-left {
    display: flex;
    align-items: center;
  }
  .header-title {
    font-size: 16px;
    font-weight: 500;
    color: #303133;
  }
}

.design-container {
  flex: 1;
  display: flex;
  gap: 12px;
  padding: 12px;
  overflow: hidden;
}

.panel-title {
  font-weight: 600;
  font-size: 14px;
  color: #303133;
}

.left-panel {
  width: 300px;
  overflow-y: auto;
  :deep(.el-card__body) { padding: 12px; }
}

.center-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  .preview-card {
    height: 100%;
    display: flex;
    flex-direction: column;
    :deep(.el-card__body) { flex: 1; display: flex; flex-direction: column; padding: 16px; }
  }
}

.right-panel {
  width: 280px;
  :deep(.el-card__body) { padding: 16px; }
}

.preview-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.preview-actions {
  display: flex;
  gap: 8px;
}

/* 字段列表样式 */
.field-tip {
  text-align: center;
  padding: 30px 10px;
  color: #909399;
  p { margin: 8px 0 0 0; font-size: 13px; }
}

.field-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.field-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 10px;
  background: #fafafa;
  border-radius: 6px;
  transition: background 0.2s;
  &:hover { background: #f0f2f5; }
}

.field-left {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
  min-width: 0;
}

.field-icon { font-size: 14px; }
.field-name {
  font-size: 13px;
  color: #303133;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.field-btns {
  display: flex;
  gap: 6px;
  flex-shrink: 0;
}

.field-btn {
  font-size: 12px;
  padding: 2px 8px;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
}

.dim-btn {
  color: #409eff;
  &:hover { background: #409eff20; }
}

.measure-btn {
  color: #67c23a;
  &:hover { background: #67c23a20; }
}

/* 配置区样式 */
.config-area {
  background: #fafafa;
  border-radius: 8px;
  padding: 16px;
  margin-bottom: 16px;
}

.config-row {
  margin-bottom: 12px;
  &:last-child { margin-bottom: 0; }
}

.config-label {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
  margin-bottom: 8px;
}

.config-content {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  min-height: 32px;
  align-items: center;
}

.config-hint {
  color: #c0c4cc;
  font-size: 13px;
}

.measure-item {
  display: flex;
  align-items: center;
  gap: 6px;
  background: #67c23a10;
  padding: 4px 8px;
  border-radius: 6px;
}

.measure-name {
  border: none;
}

.agg-select {
  width: 80px;
  :deep(.el-input__wrapper) {
    box-shadow: none;
    background: transparent;
  }
}

.remove-icon {
  cursor: pointer;
  color: #909399;
  font-size: 14px;
  &:hover { color: #f56c6c; }
}

/* 图表区域 */
.chart-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 300px;
  background: #fff;
  border-radius: 8px;
  border: 1px solid #ebeef5;
}

.chart-empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  color: #909399;
  img { width: 200px; opacity: 0.6; margin-bottom: 16px; }
  p { margin: 0; font-size: 14px; }
}

.chart-canvas {
  flex: 1;
  min-height: 300px;
}

.chart-type-group {
  display: flex;
  flex-wrap: wrap;
}

/* 预筛选样式 */
.pre-filter-list {
  margin-top: 8px;
}

.pre-filter-item {
  background: #f5f7fa;
  border-radius: 6px;
  padding: 8px;
  margin-bottom: 8px;
}

.filter-row {
  display: flex;
  align-items: center;
  gap: 4px;
}

.remove-filter {
  cursor: pointer;
  color: #909399;
  margin-left: 4px;
  &:hover { color: #f56c6c; }
}

.compare-config {
  .compare-switches {
    display: flex;
    flex-direction: column;
    gap: 8px;
    margin-top: 8px;
  }
}
</style>

