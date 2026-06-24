<template>
  <div class="mobile-charts">
    <div class="page-header">
      <span class="back-btn" @click="goBack">‹</span>
      <span class="page-title">图表列表</span>
      <span></span>
    </div>

    <div class="chart-list">
      <div
        v-for="chart in charts"
        :key="chart.id"
        class="chart-card"
      >
        <div class="chart-name">{{ chart.name }}</div>
        <div class="chart-type">{{ getChartTypeName(chart.chartType) }}</div>
        <div class="chart-container" :ref="el => setChartRef(chart.id, el)"></div>
      </div>
    </div>

    <div v-if="loading" class="loading">加载中...</div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import * as echarts from 'echarts'
import api from '@/api'

const router = useRouter()
const charts = ref<any[]>([])
const loading = ref(true)
const chartInstances = new Map<number, echarts.ECharts>()
const chartRefs = new Map<number, HTMLElement>()

const setChartRef = (id: number, el: any) => {
  if (el) chartRefs.set(id, el as HTMLElement)
}

const getChartTypeName = (type: string) => {
  const map: Record<string, string> = { bar: '柱状图', line: '折线图', pie: '饼图' }
  return map[type] || type
}

const loadCharts = async () => {
  try {
    const res = await api.get('/charts')
    if (res.data.code === 0) {
      charts.value = res.data.data
      await nextTick()
      initCharts()
    }
  } catch (e) {
    console.error('加载图表失败', e)
  } finally {
    loading.value = false
  }
}

const initCharts = async () => {
  for (const chart of charts.value) {
    const el = chartRefs.get(chart.id)
    if (!el) continue

    const instance = echarts.init(el)
    chartInstances.set(chart.id, instance)

    try {
      const res = await api.post(`/charts/${chart.id}/query`)
      if (res.data.code === 0) {
        const option = buildOption(chart, res.data.data)
        instance.setOption(option)
      }
    } catch (e) {
      console.error('查询失败', e)
    }
  }
}

const buildOption = (chart: any, data: any[]) => {
  const config = JSON.parse(chart.configJson || '{}')
  const xField = config.xField || 'name'
  const yField = config.yField || 'value'

  if (chart.chartType === 'pie') {
    return {
      series: [{
        type: 'pie',
        radius: '60%',
        data: data.map(d => ({ name: d[xField], value: d[yField] }))
      }]
    }
  }

  return {
    grid: { left: 40, right: 10, top: 20, bottom: 30 },
    xAxis: { type: 'category', data: data.map(d => d[xField]), axisLabel: { fontSize: 10 } },
    yAxis: { type: 'value' },
    series: [{ type: chart.chartType || 'bar', data: data.map(d => d[yField]) }]
  }
}

const goBack = () => router.back()

onMounted(() => loadCharts())
onUnmounted(() => chartInstances.forEach(c => c.dispose()))
</script>

<style scoped>
.mobile-charts {
  min-height: 100vh;
  background: #f5f5f5;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #409eff;
  color: #fff;
  padding: 15px;
}

.back-btn {
  font-size: 24px;
  padding: 5px 10px;
}

.page-title {
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
  padding: 15px;
}

.chart-name {
  font-size: 16px;
  font-weight: bold;
}

.chart-type {
  font-size: 12px;
  color: #999;
  margin-top: 5px;
}

.chart-container {
  height: 200px;
  margin-top: 10px;
}

.loading {
  text-align: center;
  padding: 20px;
  color: #999;
}
</style>

