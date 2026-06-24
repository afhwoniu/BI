<template>
  <div class="mobile-ai">
    <div class="page-header">
      <span class="back-btn" @click="goBack">‹</span>
      <span class="page-title">智能分析</span>
      <span></span>
    </div>

    <!-- 消息列表 -->
    <div class="message-list" ref="messageListRef">
      <div
        v-for="(msg, idx) in messages"
        :key="idx"
        class="message-item"
        :class="msg.role"
      >
        <div class="message-content">{{ msg.content }}</div>
        <div v-if="msg.chartData" class="message-chart" :ref="el => setChartRef(idx, el)"></div>
      </div>
    </div>

    <!-- 输入区 -->
    <div class="input-area">
      <input
        v-model="inputText"
        placeholder="输入您的问题..."
        class="input-box"
        @keyup.enter="sendMessage"
      />
      <button class="send-btn" @click="sendMessage" :disabled="loading">
        {{ loading ? '...' : '发送' }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, nextTick, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import * as echarts from 'echarts'
import api from '@/api'

const router = useRouter()
const messages = ref<any[]>([
  { role: 'assistant', content: '您好！我是智能分析助手，请问有什么可以帮您？' }
])
const inputText = ref('')
const loading = ref(false)
const messageListRef = ref<HTMLElement>()
const chartInstances = new Map<number, echarts.ECharts>()
const chartRefs = new Map<number, HTMLElement>()

const setChartRef = (idx: number, el: any) => {
  if (el) chartRefs.set(idx, el as HTMLElement)
}

const sendMessage = async () => {
  if (!inputText.value.trim() || loading.value) return

  const question = inputText.value.trim()
  messages.value.push({ role: 'user', content: question })
  inputText.value = ''
  loading.value = true

  await nextTick()
  scrollToBottom()

  try {
    const res = await api.post('/ai/chat', { question, datasourceId: 1 })
    if (res.data.code === 0) {
      const data = res.data.data
      const msg: any = { role: 'assistant', content: data.answer || '分析完成' }
      
      if (data.data && data.data.length > 0) {
        msg.chartData = data.data
        msg.chartType = data.chartType
        msg.chartConfig = data.chartConfig
      }
      
      messages.value.push(msg)
      await nextTick()
      
      if (msg.chartData) {
        renderChart(messages.value.length - 1, msg)
      }
    } else {
      messages.value.push({ role: 'assistant', content: res.data.message || '分析失败' })
    }
  } catch (e: any) {
    messages.value.push({ role: 'assistant', content: '请求失败：' + (e.message || '未知错误') })
  } finally {
    loading.value = false
    scrollToBottom()
  }
}

const renderChart = (idx: number, msg: any) => {
  const el = chartRefs.get(idx)
  if (!el) return

  const chart = echarts.init(el)
  chartInstances.set(idx, chart)

  const config = msg.chartConfig || {}
  const xField = config.xField || 'name'
  const yField = config.yField || 'value'
  const data = msg.chartData

  let option: any
  if (msg.chartType === 'pie') {
    option = {
      series: [{ type: 'pie', radius: '60%', data: data.map((d: any) => ({ name: d[xField], value: d[yField] })) }]
    }
  } else {
    option = {
      grid: { left: 40, right: 10, top: 20, bottom: 30 },
      xAxis: { type: 'category', data: data.map((d: any) => d[xField]) },
      yAxis: { type: 'value' },
      series: [{ type: msg.chartType || 'bar', data: data.map((d: any) => d[yField]) }]
    }
  }
  chart.setOption(option)
}

const scrollToBottom = () => {
  if (messageListRef.value) {
    messageListRef.value.scrollTop = messageListRef.value.scrollHeight
  }
}

const goBack = () => router.back()

onUnmounted(() => chartInstances.forEach(c => c.dispose()))
</script>

<style scoped>
.mobile-ai {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background: #f5f5f5;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #409eff;
  color: #fff;
  padding: 15px;
  flex-shrink: 0;
}

.back-btn { font-size: 24px; padding: 5px 10px; }
.page-title { font-size: 18px; font-weight: bold; }

.message-list {
  flex: 1;
  overflow-y: auto;
  padding: 10px;
}

.message-item {
  margin-bottom: 15px;
}

.message-item.user .message-content {
  background: #409eff;
  color: #fff;
  margin-left: 50px;
}

.message-item.assistant .message-content {
  background: #fff;
  margin-right: 50px;
}

.message-content {
  padding: 12px 15px;
  border-radius: 10px;
  font-size: 14px;
  line-height: 1.5;
}

.message-chart {
  height: 200px;
  background: #fff;
  border-radius: 10px;
  margin-top: 10px;
  margin-right: 50px;
}

.input-area {
  display: flex;
  padding: 10px;
  background: #fff;
  border-top: 1px solid #eee;
  flex-shrink: 0;
}

.input-box {
  flex: 1;
  padding: 10px 15px;
  border: 1px solid #ddd;
  border-radius: 20px;
  font-size: 14px;
}

.send-btn {
  margin-left: 10px;
  padding: 10px 20px;
  background: #409eff;
  color: #fff;
  border: none;
  border-radius: 20px;
  font-size: 14px;
}

.send-btn:disabled {
  background: #ccc;
}
</style>

