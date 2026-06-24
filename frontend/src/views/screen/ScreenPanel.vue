<template>
  <div class="screen-panel">
    <!-- 标题栏 -->
    <div class="panel-header">
      <h1 class="panel-title">{{ panel.name }}</h1>
      <div class="panel-time">{{ currentTime }}</div>
    </div>

    <!-- 图表区域 -->
    <div class="panel-content">
      <div
        v-for="item in panel.items"
        :key="item.id"
        class="chart-item"
        :style="getItemStyle(item)"
      >
        <div class="chart-title">{{ item.chart?.name }}</div>
        <div class="chart-wrapper" :ref="el => setChartRef(item.id, el)"></div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, nextTick, watch } from 'vue'
import * as echarts from 'echarts'
import api from '@/api'

const props = defineProps<{ panel: any }>()

const currentTime = ref('')
const chartInstances = new Map<number, echarts.ECharts>()
const chartRefs = new Map<number, HTMLElement>()

let timeInterval: number

const setChartRef = (id: number, el: any) => {
  if (el) chartRefs.set(id, el as HTMLElement)
}

const getItemStyle = (item: any) => {
  const layout = item.screenLayoutJson ? JSON.parse(item.screenLayoutJson) : JSON.parse(item.layoutJson || '{}')
  return {
    left: `${(layout.x || 0) * 100 / 12}%`,
    top: `${(layout.y || 0) * 60}px`,
    width: `${(layout.w || 4) * 100 / 12}%`,
    height: `${(layout.h || 4) * 60}px`
  }
}

const initCharts = async () => {
  await nextTick()
  for (const item of props.panel.items || []) {
    if (!item.chartId) continue
    const el = chartRefs.get(item.id)
    if (!el) continue

    const oldChart = chartInstances.get(item.id)
    if (oldChart) oldChart.dispose()

    const chart = echarts.init(el, 'dark')
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
      backgroundColor: 'transparent',
      tooltip: { trigger: 'item' },
      series: [{
        type: 'pie',
        radius: ['40%', '70%'],
        data: data.map(d => ({ name: d[xField], value: d[yField] })),
        label: { color: '#fff' }
      }]
    }
  }

  return {
    backgroundColor: 'transparent',
    tooltip: { trigger: 'axis' },
    grid: { left: 60, right: 20, top: 40, bottom: 40 },
    xAxis: { type: 'category', data: xData, axisLabel: { color: '#aaa' } },
    yAxis: { type: 'value', axisLabel: { color: '#aaa' } },
    series: [{ type: chart.chartType || 'bar', data: yData, itemStyle: { color: '#00d4ff' } }]
  }
}

const updateTime = () => {
  currentTime.value = new Date().toLocaleString('zh-CN', { hour12: false })
}

const handleResize = () => {
  chartInstances.forEach(chart => chart.resize())
}

watch(() => props.panel, () => {
  initCharts()
}, { immediate: true })

onMounted(() => {
  updateTime()
  timeInterval = window.setInterval(updateTime, 1000)
  window.addEventListener('resize', handleResize)
})

onUnmounted(() => {
  clearInterval(timeInterval)
  window.removeEventListener('resize', handleResize)
  chartInstances.forEach(chart => chart.dispose())
})
</script>

<style scoped>
.screen-panel {
  width: 100%;
  height: 100%;
  background: linear-gradient(135deg, #0a1628 0%, #1a2a4a 50%, #0a1628 100%);
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 40px;
  background: linear-gradient(180deg, rgba(0, 100, 200, 0.3) 0%, transparent 100%);
  border-bottom: 1px solid rgba(0, 200, 255, 0.3);
}

.panel-title {
  font-size: 32px;
  font-weight: bold;
  color: #00d4ff;
  text-shadow: 0 0 20px rgba(0, 212, 255, 0.5);
  margin: 0;
}

.panel-time {
  font-size: 20px;
  color: #aaa;
}

.panel-content {
  position: relative;
  width: 100%;
  height: calc(100vh - 80px);
  padding: 20px;
  box-sizing: border-box;
}

.chart-item {
  position: absolute;
  background: rgba(0, 50, 100, 0.3);
  border: 1px solid rgba(0, 200, 255, 0.2);
  border-radius: 8px;
  padding: 10px;
  box-sizing: border-box;
}

.chart-title {
  font-size: 16px;
  color: #00d4ff;
  margin-bottom: 10px;
  text-align: center;
}

.chart-wrapper {
  width: 100%;
  height: calc(100% - 30px);
}
</style>

