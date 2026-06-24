<template>
  <div class="carousel-container" ref="containerRef">
    <!-- 当前面板 -->
    <div class="carousel-panel" :class="{ 'fade-in': fadeIn }">
      <ScreenPanel v-if="currentPanel" :panel="currentPanel" :key="currentPanel.id" />
    </div>

    <!-- 控制栏 -->
    <div class="carousel-controls" v-if="panels.length > 1">
      <div class="carousel-dots">
        <span
          v-for="(p, idx) in panels"
          :key="p.id"
          class="dot"
          :class="{ active: idx === currentIndex }"
          @click="goToPanel(idx)"
        ></span>
      </div>
      <div class="carousel-info">
        {{ currentIndex + 1 }} / {{ panels.length }}
        <span class="pause-btn" @click="togglePause">{{ isPaused ? '▶' : '⏸' }}</span>
      </div>
    </div>

    <!-- 进度条 -->
    <div class="progress-bar" v-if="!isPaused && panels.length > 1">
      <div class="progress" :style="{ width: progress + '%' }"></div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useRoute } from 'vue-router'
import api from '@/api'
import ScreenPanel from './ScreenPanel.vue'

const route = useRoute()
const containerRef = ref<HTMLElement>()
const panels = ref<any[]>([])
const currentIndex = ref(0)
const isPaused = ref(false)
const fadeIn = ref(true)
const progress = ref(0)

// 轮播间隔（秒）
const interval = computed(() => {
  return parseInt(route.query.interval as string) || 30
})

// 当前面板
const currentPanel = computed(() => panels.value[currentIndex.value])

let carouselTimer: number
let progressTimer: number

// 加载面板列表
const loadPanels = async () => {
  const ids = route.query.ids as string
  if (!ids) return

  const idList = ids.split(',').map(id => parseInt(id))
  for (const id of idList) {
    try {
      const res = await api.get(`/panels/${id}`)
      if (res.data.code === 0) {
        panels.value.push(res.data.data)
      }
    } catch (e) {
      console.error('加载面板失败', id, e)
    }
  }
}

// 切换到下一个面板
const nextPanel = () => {
  fadeIn.value = false
  setTimeout(() => {
    currentIndex.value = (currentIndex.value + 1) % panels.value.length
    fadeIn.value = true
    progress.value = 0
  }, 300)
}

// 跳转到指定面板
const goToPanel = (idx: number) => {
  fadeIn.value = false
  setTimeout(() => {
    currentIndex.value = idx
    fadeIn.value = true
    progress.value = 0
  }, 300)
}

// 暂停/继续
const togglePause = () => {
  isPaused.value = !isPaused.value
}

// 启动轮播
const startCarousel = () => {
  carouselTimer = window.setInterval(() => {
    if (!isPaused.value && panels.value.length > 1) {
      nextPanel()
    }
  }, interval.value * 1000)

  // 进度条更新
  progressTimer = window.setInterval(() => {
    if (!isPaused.value && panels.value.length > 1) {
      progress.value += 100 / (interval.value * 10)
      if (progress.value >= 100) progress.value = 0
    }
  }, 100)
}

// 全屏
const enterFullscreen = () => {
  containerRef.value?.requestFullscreen?.()
}

onMounted(async () => {
  await loadPanels()
  startCarousel()
  // 自动全屏
  if (route.query.fullscreen !== 'false') {
    setTimeout(enterFullscreen, 500)
  }
})

onUnmounted(() => {
  clearInterval(carouselTimer)
  clearInterval(progressTimer)
})
</script>

<style scoped>
.carousel-container {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background: #0a1628;
  overflow: hidden;
}

.carousel-panel {
  width: 100%;
  height: 100%;
  opacity: 0;
  transition: opacity 0.3s ease;
}

.carousel-panel.fade-in {
  opacity: 1;
}

.carousel-controls {
  position: fixed;
  bottom: 20px;
  left: 50%;
  transform: translateX(-50%);
  display: flex;
  align-items: center;
  gap: 20px;
  background: rgba(0, 0, 0, 0.5);
  padding: 10px 20px;
  border-radius: 20px;
}

.carousel-dots {
  display: flex;
  gap: 8px;
}

.dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.3);
  cursor: pointer;
  transition: all 0.3s;
}

.dot.active {
  background: #00d4ff;
  transform: scale(1.2);
}

.carousel-info {
  color: #aaa;
  font-size: 14px;
}

.pause-btn {
  margin-left: 10px;
  cursor: pointer;
}

.progress-bar {
  position: fixed;
  bottom: 0;
  left: 0;
  width: 100%;
  height: 3px;
  background: rgba(255, 255, 255, 0.1);
}

.progress {
  height: 100%;
  background: linear-gradient(90deg, #00d4ff, #00ff88);
  transition: width 0.1s linear;
}
</style>

