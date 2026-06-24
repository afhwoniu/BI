<template>
  <div class="report-view" v-loading="loading">
    <div class="report-header">
      <h1>{{ report?.name }}</h1>
      <div class="page-nav" v-if="report?.pages?.length > 1">
        <el-button :disabled="currentPageIndex === 0" @click="currentPageIndex--">上一页</el-button>
        <span>{{ currentPageIndex + 1 }} / {{ report.pages.length }}</span>
        <el-button :disabled="currentPageIndex >= report.pages.length - 1" @click="currentPageIndex++">下一页</el-button>
      </div>
    </div>

    <div class="report-content">
      <div class="page-canvas">
        <div v-for="item in currentPageItems" :key="item.id" 
             class="report-item" :style="getItemStyle(item)">
          <!-- 图表 -->
          <div v-if="item.itemType === 'chart'" class="chart-container" :ref="el => chartRefs[item.id] = el">
          </div>
          <!-- 文本 -->
          <div v-else-if="item.itemType === 'text'" class="text-container" v-html="item.textContent"></div>
          <!-- 图片 -->
          <div v-else-if="item.itemType === 'image'" class="image-container">
            <img :src="item.imageUrl" v-if="item.imageUrl" />
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick } from 'vue'
import { useRoute } from 'vue-router'
import * as echarts from 'echarts'
import { getReportRenderData } from '@/api/report'
import { queryChart } from '@/api/chart'

const route = useRoute()
const reportId = Number(route.params.id)

const loading = ref(false)
const report = ref<any>(null)
const currentPageIndex = ref(0)
const chartRefs = ref<Record<number, any>>({})
const chartInstances = ref<Record<number, echarts.ECharts>>({})

const currentPage = computed(() => report.value?.pages?.[currentPageIndex.value])
const currentPageItems = computed(() => currentPage.value?.items || [])

onMounted(async () => {
  await loadReport()
})

watch(currentPageIndex, async () => {
  await nextTick()
  await renderCharts()
})

async function loadReport() {
  loading.value = true
  try {
    const res = await getReportRenderData(reportId)
    report.value = res.data
    await nextTick()
    await renderCharts()
  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

async function renderCharts() {
  for (const item of currentPageItems.value) {
    if (item.itemType === 'chart' && item.chartId) {
      await renderChart(item)
    }
  }
}

async function renderChart(item: any) {
  const container = chartRefs.value[item.id]
  if (!container) return

  try {
    // 获取图表数据
    const res = await queryChart(item.chartId)
    const queryResult = res.data
    if (!queryResult) return

    // 初始化或获取 ECharts 实例
    let chart = chartInstances.value[item.id]
    if (!chart) {
      chart = echarts.init(container)
      chartInstances.value[item.id] = chart
    }

    // 解析配置
    const chartType = item.chartType || 'bar'

    // 构建 ECharts 配置
    const option: echarts.EChartsOption = {
      tooltip: { trigger: chartType === 'pie' ? 'item' : 'axis' },
      legend: { bottom: 10 },
      grid: { left: '3%', right: '4%', bottom: '15%', containLabel: true },
      xAxis: chartType === 'pie' ? undefined : { type: 'category', data: queryResult.categories },
      yAxis: chartType === 'pie' ? undefined : { type: 'value' },
      series: queryResult.series.map((s: any) => ({
        name: s.name,
        type: chartType,
        data: chartType === 'pie'
          ? queryResult.categories.map((c: string, i: number) => ({ name: c, value: s.data[i] }))
          : s.data
      }))
    }

    chart.setOption(option, true)
  } catch (e) {
    console.error('渲染图表失败:', e)
  }
}

function getItemStyle(item: any): Record<string, string> {
  try {
    const layout = JSON.parse(item.layoutJson || '{}')
    return {
      position: 'absolute',
      left: `${layout.x || 0}px`,
      top: `${layout.y || 0}px`,
      width: `${layout.width || 300}px`,
      height: `${layout.height || 200}px`
    }
  } catch {
    return { position: 'absolute', left: '0px', top: '0px', width: '300px', height: '200px' }
  }
}
</script>

<style scoped>
.report-view { min-height: 100vh; background: #f0f2f5; padding: 20px; }
.report-header { text-align: center; margin-bottom: 20px; }
.report-header h1 { margin: 0 0 16px 0; font-size: 24px; }
.page-nav { display: flex; justify-content: center; align-items: center; gap: 16px; }
.report-content { display: flex; justify-content: center; }
.page-canvas { position: relative; width: 960px; min-height: 600px; background: #fff; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
.report-item { overflow: hidden; }
.chart-container { width: 100%; height: 100%; }
.text-container { padding: 12px; height: 100%; overflow: auto; }
.image-container { width: 100%; height: 100%; display: flex; align-items: center; justify-content: center; }
.image-container img { max-width: 100%; max-height: 100%; object-fit: contain; }
</style>

