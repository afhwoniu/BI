<template>
  <div class="screen-container" ref="screenRef" @dblclick="toggleFullscreen">
    <!-- 标题栏 -->
    <div class="screen-header" v-if="panel">
      <h1 class="screen-title">{{ panel.name }}</h1>
      <div class="screen-time">{{ currentTime }}</div>
    </div>

    <!-- 图表区域 -->
    <div class="screen-content">
      <div
        v-for="item in panelItems"
        :key="item.id"
        class="screen-chart-item"
        :style="getItemStyle(item)"
      >
        <div class="chart-title">{{ item.chart?.name }}</div>
        <div class="chart-wrapper" :ref="el => setChartRef(item.id, el)"></div>
      </div>
    </div>

    <!-- 全屏提示 -->
    <div class="fullscreen-tip" v-if="showTip">双击进入/退出全屏</div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, nextTick } from 'vue'
import { useRoute } from 'vue-router'
import * as echarts from 'echarts'
import api from '@/api'

const route = useRoute()
const screenRef = ref<HTMLElement>()
const panel = ref<any>(null)
const panelItems = ref<any[]>([])
const currentTime = ref('')
const showTip = ref(true)
const chartInstances = new Map<number, echarts.ECharts>()
const chartRefs = new Map<number, HTMLElement>()

let timeInterval: number
let refreshInterval: number

// 设置图表容器引用
const setChartRef = (id: number, el: any) => {
  if (el) chartRefs.set(id, el as HTMLElement)
}

// 获取子项样式（基于大屏布局）
const getItemStyle = (item: any) => {
  const layout = item.screenLayoutJson ? JSON.parse(item.screenLayoutJson) : JSON.parse(item.layoutJson || '{}')
  return {
    left: `${(layout.x || 0) * 100 / 12}%`,
    top: `${(layout.y || 0) * 60}px`,
    width: `${(layout.w || 4) * 100 / 12}%`,
    height: `${(layout.h || 4) * 60}px`
  }
}

// 加载面板数据
const loadPanel = async () => {
  const id = route.params.id
  const res = await api.get(`/panels/${id}`)
  if (res.data.code === 0) {
    panel.value = res.data.data
    panelItems.value = panel.value.items || []
    await nextTick()
    initCharts()
  }
}

// 初始化图表
const initCharts = async () => {
  for (const item of panelItems.value) {
    if (!item.chartId) continue
    const el = chartRefs.get(item.id)
    if (!el) continue

    // 销毁旧实例
    const oldChart = chartInstances.get(item.id)
    if (oldChart) oldChart.dispose()

    // 创建新实例（深色主题）
    const chart = echarts.init(el, 'dark')
    chartInstances.set(item.id, chart)

    // 查询数据
    try {
      const queryRes = await api.post(`/charts/${item.chartId}/query`)
      if (queryRes.data.code === 0) {
        const option = buildChartOption(item.chart, queryRes.data.data)
        chart.setOption(option)
      }
    } catch (e) {
      console.error('图表查询失败', e)
    }
  }
}

// 构建图表配置
const buildChartOption = (chart: any, data: any[]) => {
  const config = JSON.parse(chart.configJson || '{}')
  const xField = config.xField || 'name'
  const yField = config.yField || 'value'

  const xData = data.map(d => d[xField])
  const yData = data.map(d => d[yField])

  const baseOption: any = {
    backgroundColor: 'transparent',
    tooltip: { trigger: 'axis' },
    grid: { left: 60, right: 20, top: 40, bottom: 40 },
    xAxis: { type: 'category', data: xData, axisLabel: { color: '#aaa' } },
    yAxis: { type: 'value', axisLabel: { color: '#aaa' } },
    series: [{ type: chart.chartType || 'bar', data: yData, itemStyle: { color: '#00d4ff' } }]
  }

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

  return baseOption
}

// 切换全屏
const toggleFullscreen = () => {
  if (!document.fullscreenElement) {
    screenRef.value?.requestFullscreen()
  } else {
    document.exitFullscreen()
  }
}

// 更新时间
const updateTime = () => {
  const now = new Date()
  currentTime.value = now.toLocaleString('zh-CN', { hour12: false })
}

// 窗口大小变化时重绘图表
const handleResize = () => {
  chartInstances.forEach(chart => chart.resize())
}

onMounted(() => {
  loadPanel()
  updateTime()
  timeInterval = window.setInterval(updateTime, 1000)
  setTimeout(() => showTip.value = false, 3000)
  window.addEventListener('resize', handleResize)

  // 自动刷新（5分钟）
  const config = panel.value?.configJson ? JSON.parse(panel.value.configJson) : {}
  const interval = (config.refreshInterval || 300) * 1000
  refreshInterval = window.setInterval(initCharts, interval)
})

onUnmounted(() => {
  clearInterval(timeInterval)
  clearInterval(refreshInterval)
  window.removeEventListener('resize', handleResize)
  chartInstances.forEach(chart => chart.dispose())
})
</script>

<style scoped>
.screen-container {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background: linear-gradient(135deg, #0a1628 0%, #1a2a4a 50%, #0a1628 100%);
  overflow: hidden;
  font-family: 'Microsoft YaHei', sans-serif;
}

.screen-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 40px;
  background: linear-gradient(180deg, rgba(0, 100, 200, 0.3) 0%, transparent 100%);
  border-bottom: 1px solid rgba(0, 200, 255, 0.3);
}

.screen-title {
  font-size: 32px;
  font-weight: bold;
  color: #00d4ff;
  text-shadow: 0 0 20px rgba(0, 212, 255, 0.5);
  margin: 0;
}

.screen-time {
  font-size: 20px;
  color: #aaa;
}

.screen-content {
  position: relative;
  width: 100%;
  height: calc(100vh - 80px);
  padding: 20px;
  box-sizing: border-box;
}

.screen-chart-item {
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

.fullscreen-tip {
  position: fixed;
  bottom: 20px;
  left: 50%;
  transform: translateX(-50%);
  background: rgba(0, 0, 0, 0.7);
  color: #fff;
  padding: 10px 20px;
  border-radius: 20px;
  font-size: 14px;
  animation: fadeOut 3s forwards;
}

@keyframes fadeOut {
  0%, 70% { opacity: 1; }
  100% { opacity: 0; }
}
</style>

