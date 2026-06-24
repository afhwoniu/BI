<script setup lang="ts">
import { ref, onMounted, nextTick, onUnmounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'
import * as echarts from 'echarts'
import { getPanelDetail, type PanelItem } from '@/api/panel'
import { queryChart, type FilterCondition } from '@/api/chart'

// 使用 grid-layout-plus (Vue 3 兼容版)
import { GridLayout, GridItem } from 'grid-layout-plus'

const route = useRoute()
const router = useRouter()
const loading = ref(true)
const panelName = ref('')
const panelType = ref('pc_dashboard')
const chartInstances = ref<Map<string, echarts.ECharts>>(new Map())

interface CanvasItem {
  i: string
  chartId: number
  chartName: string
  chartType: string
  x: number
  y: number
  w: number
  h: number
}
const canvasItems = ref<CanvasItem[]>([])

const gridConfig = computed(() => {
  const configs: Record<string, { colNum: number; rowHeight: number; margin: [number, number] }> = {
    pc_dashboard: { colNum: 12, rowHeight: 30, margin: [10, 10] },
    big_screen: { colNum: 24, rowHeight: 20, margin: [8, 8] },
    mobile: { colNum: 4, rowHeight: 40, margin: [8, 8] }
  }
  return configs[panelType.value] || configs.pc_dashboard
})

const urlFilters = computed<FilterCondition[]>(() => {
  const filters: FilterCondition[] = []
  const query = route.query
  for (const key in query) {
    if (key.startsWith('filter_')) {
      const rest = key.substring(7)
      const parts = rest.split('_')
      let field = rest
      let operator = '='

      const lastPart = parts[parts.length - 1]?.toLowerCase()
      const opMap: Record<string, string> = {
        'eq': '=', 'ne': '!=', 'gt': '>', 'lt': '<',
        'gte': '>=', 'lte': '<=', 'like': 'like', 'in': 'in'
      }
      if (opMap[lastPart]) {
        operator = opMap[lastPart]
        field = parts.slice(0, -1).join('_')
      }

      let value: any = query[key]
      if (operator === 'in' && typeof value === 'string') {
        value = value.split(',')
      }

      filters.push({ field, operator, value })
    }
  }
  return filters
})

onMounted(async () => {
  const id = Number(route.params.id)
  if (!id) {
    ElMessage.error('面板ID无效')
    return
  }
  await loadPanel(id)
})

onUnmounted(() => {
  chartInstances.value.forEach(c => c.dispose())
  chartInstances.value.clear()
})

interface GlobalFilter {
  id: string
  label: string
  field: string
  type: 'input' | 'select' | 'date' | 'daterange'
  options?: string | string[]
  defaultValue?: any
}
const globalFilters = ref<GlobalFilter[]>([])

const filterValues = ref<Record<string, any>>({})

async function loadPanel(id: number) {
  loading.value = true
  try {
    const res = await getPanelDetail(id)
    if (res.code === 0 && res.data) {
      panelName.value = res.data.name
      panelType.value = res.data.panelType
      try {
        const config = JSON.parse(res.data.configJson || '{}')
        globalFilters.value = config.globalFilters || []
        globalFilters.value.forEach(f => {
          filterValues.value[f.field] = f.defaultValue || ''
        })
      } catch {
        globalFilters.value = []
      }
      canvasItems.value = res.data.items.map((item: PanelItem, index: number) => {
        const layout = JSON.parse(item.layoutJson || '{}')
        return {
          i: `item-${index}`,
          chartId: item.chartId || 0,
          chartName: item.chartName || '',
          chartType: item.chartType || 'bar',
          x: layout.x || 0,
          y: layout.y || 0,
          w: layout.w || 6,
          h: layout.h || 8
        }
      })
      await nextTick()
      for (const item of canvasItems.value) {
        await renderItemChart(item.i)
      }
    } else {
      ElMessage.error(res.message || '加载面板失败')
    }
  } catch (e) {
    console.error(e)
    ElMessage.error('加载面板失败')
  } finally {
    loading.value = false
  }
}

function getAllFilters(): FilterCondition[] {
  const filters = [...urlFilters.value]
  for (const gf of globalFilters.value) {
    const val = filterValues.value[gf.field]
    if (val !== undefined && val !== '' && val !== null) {
      if (gf.type === 'daterange' && Array.isArray(val) && val.length === 2) {
        filters.push({ field: gf.field, operator: 'between', value: val })
      } else {
        filters.push({ field: gf.field, operator: '=', value: val })
      }
    }
  }
  return filters
}

async function applyFilters() {
  for (const item of canvasItems.value) {
    await renderItemChart(item.i)
  }
}

const drillFilters = ref<FilterCondition[]>([])
const drillSourceIndex = ref<string | null>(null)

async function renderItemChart(itemKey: string) {
  const item = canvasItems.value.find(i => i.i === itemKey)
  if (!item?.chartId) return
  const el = document.getElementById(`preview-chart-${itemKey}`)
  if (!el) return

  try {
    const filters = [...getAllFilters(), ...drillFilters.value]
    const res = await queryChart(item.chartId, { filters, skipCache: true })
    if (res.code === 0 && res.data) {
      let chart = chartInstances.value.get(itemKey)
      if (!chart) {
        chart = echarts.init(el)
        chartInstances.value.set(itemKey, chart)
        chart.on('click', (params: any) => handleChartClick(itemKey, params, res.data!.categories))
      }
      const colors = ['#5470c6', '#91cc75', '#fac858', '#ee6666', '#73c0de', '#3ba272']
      const option: echarts.EChartsOption = {
        color: colors,
        tooltip: { trigger: item.chartType === 'pie' ? 'item' : 'axis' },
        legend: { bottom: 0, type: 'scroll' },
        grid: { left: '3%', right: '4%', bottom: '15%', top: '10%', containLabel: true },
        xAxis: item.chartType === 'pie' ? undefined : { type: 'category', data: res.data.categories },
        yAxis: item.chartType === 'pie' ? undefined : { type: 'value' },
        series: res.data.series.map((s: any) => ({
          name: s.name,
          type: item.chartType as 'bar' | 'line' | 'pie',
          data: item.chartType === 'pie'
            ? res.data!.categories.map((c: string, i: number) => ({ name: c, value: s.data[i] }))
            : s.data
        }))
      }
      chart.setOption(option, true)
    }
  } catch (e) {
    console.error(e)
  }
}

function handleChartClick(itemKey: string, params: any, categories: string[]) {
  const item = canvasItems.value.find(i => i.i === itemKey)
  if (!item) return
  
  let dimValue = ''
  if (item.chartType === 'pie') {
    dimValue = params.name
  } else {
    dimValue = categories[params.dataIndex] || params.name
  }

  if (!dimValue) return

  if (drillSourceIndex.value === itemKey &&
      drillFilters.value.length > 0 &&
      drillFilters.value[0].value === dimValue) {
    clearDrillFilter()
    return
  }

  drillFilters.value = [{ field: '_drill_dim_', operator: '=', value: dimValue }]
  drillSourceIndex.value = itemKey

  ElMessage.info(`已选择: ${dimValue}，其他图表将筛选此条件`)
  refreshOtherCharts(itemKey)
}

function clearDrillFilter() {
  drillFilters.value = []
  drillSourceIndex.value = null
  applyFilters()
}

async function refreshOtherCharts(sourceKey: string) {
  for (const item of canvasItems.value) {
    if (item.i !== sourceKey) {
      await renderItemChart(item.i)
    }
  }
}

function goBack() {
  router.push({ name: 'Panel' })
}

function handleResize() {
  chartInstances.value.forEach(c => c.resize())
}

onMounted(() => {
  window.addEventListener('resize', handleResize)
})
onUnmounted(() => {
  window.removeEventListener('resize', handleResize)
})
</script>

<template>
  <div class="panel-preview" v-loading="loading">
    <div class="preview-header">
      <el-button :icon="ArrowLeft" @click="goBack">返回</el-button>
      <span class="panel-name">{{ panelName }}</span>
    </div>

    <div v-if="globalFilters.length > 0" class="filter-bar">
      <template v-for="filter in globalFilters" :key="filter.id">
        <div class="filter-item">
          <span class="filter-label">{{ filter.label }}:</span>
          <el-input
            v-if="filter.type === 'input'"
            v-model="filterValues[filter.field]"
            placeholder="请输入"
            size="small"
            style="width: 150px"
            clearable
          />
          <el-select
            v-else-if="filter.type === 'select'"
            v-model="filterValues[filter.field]"
            placeholder="请选择"
            size="small"
            style="width: 150px"
            clearable
          >
            <el-option
              v-for="opt in (typeof filter.options === 'string' ? filter.options.split(',') : filter.options || [])"
              :key="opt"
              :label="opt.trim()"
              :value="opt.trim()"
            />
          </el-select>
          <el-date-picker
            v-else-if="filter.type === 'date'"
            v-model="filterValues[filter.field]"
            type="date"
            placeholder="选择日期"
            size="small"
            style="width: 150px"
            value-format="YYYY-MM-DD"
          />
          <el-date-picker
            v-else-if="filter.type === 'daterange'"
            v-model="filterValues[filter.field]"
            type="daterange"
            start-placeholder="开始"
            end-placeholder="结束"
            size="small"
            style="width: 240px"
            value-format="YYYY-MM-DD"
          />
        </div>
      </template>
      <el-button type="primary" size="small" @click="applyFilters">查询</el-button>
      <template v-if="drillFilters.length > 0">
        <el-divider direction="vertical" />
        <el-tag type="warning" closable @close="clearDrillFilter">
          联动筛选: {{ drillFilters[0]?.value }}
        </el-tag>
      </template>
    </div>

    <div v-else-if="drillFilters.length > 0" class="filter-bar">
      <el-tag type="warning" closable @close="clearDrillFilter">
        联动筛选: {{ drillFilters[0]?.value }}
      </el-tag>
    </div>

    <div class="preview-canvas">
      <div v-if="canvasItems.length === 0 && !loading" class="empty-tip">
        <el-empty description="该面板暂无图表" />
      </div>
      
      <GridLayout
        v-if="canvasItems.length > 0"
        :layout="canvasItems"
        :col-num="gridConfig.colNum"
        :row-height="gridConfig.rowHeight"
        :margin="gridConfig.margin"
        :is-draggable="false"
        :is-resizable="false"
        :use-css-transforms="true"
        :vertical-compact="true"
      >
        <GridItem
          v-for="item in canvasItems"
          :key="item.i"
          :x="item.x"
          :y="item.y"
          :w="item.w"
          :h="item.h"
          :i="item.i"
          :static="true"
        >
          <div class="chart-card">
            <div class="chart-header">{{ item.chartName }}</div>
            <div :id="`preview-chart-${item.i}`" class="chart-body"></div>
          </div>
        </GridItem>
      </GridLayout>
    </div>
  </div>
</template>

<style scoped lang="scss">
.panel-preview {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: #f0f2f5;
}

.preview-header {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px 20px;
  background: #fff;
  border-bottom: 1px solid #e4e7ed;
  flex-shrink: 0;
}

.panel-name {
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.filter-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 16px;
  padding: 12px 20px;
  background: #fff;
  border-bottom: 1px solid #e4e7ed;
  flex-shrink: 0;
}

.filter-item {
  display: flex;
  align-items: center;
  gap: 8px;
}

.filter-label {
  font-size: 13px;
  color: #606266;
  white-space: nowrap;
}

.preview-canvas {
  flex: 1;
  padding: 16px;
  overflow: auto;
  
  :deep(.vgl-layout) {
    min-height: 100%;
  }
  
  :deep(.vgl-item) {
    touch-action: none;
  }
}

.empty-tip {
  width: 100%;
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 300px;
}

.chart-card {
  height: 100%;
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  transition: box-shadow 0.2s;
  
  &:hover {
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.12);
  }
}

.chart-header {
  padding: 12px 16px;
  font-size: 15px;
  font-weight: 600;
  color: #303133;
  border-bottom: 1px solid #ebeef5;
  background: #fafafa;
}

.chart-body {
  flex: 1;
  min-height: 0;
  padding: 8px;
}
</style>
