<template>
  <div class="mobile-panel">
    <!-- 顶部导航 -->
    <div class="panel-header">
      <span class="back-btn" @click="goBack">‹</span>
      <span class="panel-title">{{ panel?.name }}</span>
      <span class="refresh-btn" @click="refreshCharts">↻</span>
    </div>

    <!-- 图表卡片列表 -->
    <div class="chart-list">
      <div
        v-for="item in panelItems"
        :key="item.id"
        class="chart-card"
      >
        <div class="chart-card-title">{{ item.chart?.name }}</div>
        <div class="chart-container" :ref="el => setChartRef(item.id, el)"></div>
      </div>
    </div>

    <!-- 加载中 -->
    <div v-if="loading" class="loading-mask">
      <div class="loading-spinner"></div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as echarts from 'echarts'
import api from '@/api'

const route = useRoute()
const router = useRouter()
const panel = ref<any>(null)
const panelItems = ref<any[]>([])
const loading = ref(true)
const chartInstances = new Map<number, echarts.ECharts>()
const chartRefs = new Map<number, HTMLElement>()

const setChartRef = (id: number, el: any) => {
  if (el) chartRefs.set(id, el as HTMLElement)
}

const loadPanel = async () => {
  loading.value = true
  try {
    const id = route.params.id
    const res = await api.get(`/panels/${id}`)
    if (res.data.code === 0) {
      panel.value = res.data.data
      panelItems.value = panel.value.items || []
      await nextTick()
      initCharts()
    }
  } catch (e) {
    console.error('加载面板失败', e)
  } finally {
    loading.value = false
  }
}

const initCharts = async () => {
  for (const item of panelItems.value) {
    if (!item.chartId) continue
    const el = chartRefs.get(item.id)
    if (!el) continue

    const oldChart = chartInstances.get(item.id)
    if (oldChart) oldChart.dispose()

    const chart = echarts.init(el)
    chartInstances.set(item.id, chart)

    try {
      const res = await api.post(`/charts/${item.chartId}/query`)
      if (res.data.code === 0) {
        const option = buildChartOption(item.chart, res.data.data)
        chart.setOption(option)
      }
    } catch (e) {
      console.error('图表查询失败', e)
    }
  }
}

const buildChartOption = (chart: any, data: any[]) => {
  const config = JSON.parse(chart.configJson || '{}')
  const xField = config.xField || 'name'
  const yField = config.yField || 'value'

  const xData = data.map(d => d[xField])
  const yData = data.map(d => d[yField])

  if (chart.chartType === 'pie') {
    return {
      tooltip: { trigger: 'item' },
      series: [{
        type: 'pie',
        radius: ['40%', '70%'],
        data: data.map(d => ({ name: d[xField], value: d[yField] }))
      }]
    }
  }

  return {
    tooltip: { trigger: 'axis' },
    grid: { left: 50, right: 20, top: 30, bottom: 40 },
    xAxis: { type: 'category', data: xData, axisLabel: { rotate: 45, fontSize: 10 } },
    yAxis: { type: 'value' },
    series: [{ type: chart.chartType || 'bar', data: yData }]
  }
}

const refreshCharts = () => {
  initCharts()
}

const goBack = () => {
  router.back()
}

const handleResize = () => {
  chartInstances.forEach(chart => chart.resize())
}

onMounted(() => {
  loadPanel()
  window.addEventListener('resize', handleResize)
})

onUnmounted(() => {
  window.removeEventListener('resize', handleResize)
  chartInstances.forEach(chart => chart.dispose())
})
</script>

<style scoped>
.mobile-panel {
  min-height: 100vh;
  background: #f5f5f5;
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #409eff;
  color: #fff;
  padding: 15px;
  position: sticky;
  top: 0;
  z-index: 100;
}

.back-btn, .refresh-btn {
  font-size: 24px;
  cursor: pointer;
  padding: 5px 10px;
}

.panel-title {
  font-size: 18px;
  font-weight: bold;
}

.chart-list {
  padding: 10px;
}

.chart-card {
  background: #fff;
  border-radius: 10px;
  margin-bottom: 15px;
  overflow: hidden;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.05);
}

.chart-card-title {
  padding: 12px 15px;
  font-size: 14px;
  font-weight: bold;
  color: #333;
  border-bottom: 1px solid #eee;
}

.chart-container {
  height: 250px;
  padding: 10px;
}

.loading-mask {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(255, 255, 255, 0.8);
  display: flex;
  align-items: center;
  justify-content: center;
}

.loading-spinner {
  width: 40px;
  height: 40px;
  border: 3px solid #eee;
  border-top-color: #409eff;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>

