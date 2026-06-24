<template>
  <div class="mobile-home">
    <!-- 顶部标题 -->
    <div class="mobile-header">
      <h1>数据分析</h1>
    </div>

    <!-- 搜索栏 -->
    <div class="search-bar">
      <input v-model="searchText" placeholder="搜索面板..." class="search-input" />
    </div>

    <!-- 面板列表 -->
    <div class="panel-list">
      <div
        v-for="panel in filteredPanels"
        :key="panel.id"
        class="panel-card"
        @click="goToPanel(panel.id)"
      >
        <div class="panel-icon">📊</div>
        <div class="panel-info">
          <div class="panel-name">{{ panel.name }}</div>
          <div class="panel-desc">{{ panel.remark || '暂无描述' }}</div>
        </div>
        <div class="panel-arrow">›</div>
      </div>

      <div v-if="filteredPanels.length === 0" class="empty-tip">
        暂无可用面板
      </div>
    </div>

    <!-- 底部导航 -->
    <div class="mobile-nav">
      <div class="nav-item active">
        <span class="nav-icon">📊</span>
        <span class="nav-text">面板</span>
      </div>
      <div class="nav-item" @click="goToCharts">
        <span class="nav-icon">📈</span>
        <span class="nav-text">图表</span>
      </div>
      <div class="nav-item" @click="goToAi">
        <span class="nav-icon">🤖</span>
        <span class="nav-text">智能分析</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import api from '@/api'

const router = useRouter()
const panels = ref<any[]>([])
const searchText = ref('')

const filteredPanels = computed(() => {
  if (!searchText.value) return panels.value
  return panels.value.filter(p => 
    p.name.toLowerCase().includes(searchText.value.toLowerCase())
  )
})

const loadPanels = async () => {
  try {
    const res = await api.get('/panels')
    if (res.data.code === 0) {
      // 优先显示移动端面板
      panels.value = res.data.data.sort((a: any, b: any) => {
        if (a.panelType === 'mobile' && b.panelType !== 'mobile') return -1
        if (a.panelType !== 'mobile' && b.panelType === 'mobile') return 1
        return 0
      })
    }
  } catch (e) {
    console.error('加载面板失败', e)
  }
}

const goToPanel = (id: number) => {
  router.push(`/m/panel/${id}`)
}

const goToCharts = () => {
  router.push('/m/charts')
}

const goToAi = () => {
  router.push('/m/ai')
}

onMounted(() => {
  loadPanels()
})
</script>

<style scoped>
.mobile-home {
  min-height: 100vh;
  background: #f5f5f5;
  padding-bottom: 60px;
}

.mobile-header {
  background: linear-gradient(135deg, #409eff, #67c23a);
  padding: 20px;
  color: #fff;
}

.mobile-header h1 {
  margin: 0;
  font-size: 24px;
}

.search-bar {
  padding: 10px 15px;
  background: #fff;
}

.search-input {
  width: 100%;
  padding: 10px 15px;
  border: 1px solid #ddd;
  border-radius: 20px;
  font-size: 14px;
  box-sizing: border-box;
}

.panel-list {
  padding: 10px 15px;
}

.panel-card {
  display: flex;
  align-items: center;
  background: #fff;
  padding: 15px;
  border-radius: 10px;
  margin-bottom: 10px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.05);
}

.panel-icon {
  font-size: 32px;
  margin-right: 15px;
}

.panel-info {
  flex: 1;
}

.panel-name {
  font-size: 16px;
  font-weight: bold;
  color: #333;
}

.panel-desc {
  font-size: 12px;
  color: #999;
  margin-top: 5px;
}

.panel-arrow {
  font-size: 24px;
  color: #ccc;
}

.empty-tip {
  text-align: center;
  color: #999;
  padding: 40px;
}

.mobile-nav {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  display: flex;
  background: #fff;
  border-top: 1px solid #eee;
  padding: 8px 0;
}

.nav-item {
  flex: 1;
  text-align: center;
  color: #999;
}

.nav-item.active {
  color: #409eff;
}

.nav-icon {
  display: block;
  font-size: 20px;
}

.nav-text {
  font-size: 12px;
}
</style>

