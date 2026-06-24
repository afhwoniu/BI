<template>
  <div class="portal-layout">
    <!-- 侧边菜单 -->
    <div class="portal-sidebar">
      <div class="portal-logo">
        <h2>数据门户</h2>
      </div>
      <el-menu
        :default-active="activeMenu"
        background-color="#1f2d3d"
        text-color="#bfcbd9"
        active-text-color="#409eff"
        @select="handleMenuSelect"
      >
        <template v-for="menu in menuTree" :key="menu.id">
          <el-sub-menu v-if="menu.children?.length" :index="String(menu.id)">
            <template #title>
              <el-icon v-if="menu.icon"><component :is="menu.icon" /></el-icon>
              <span>{{ menu.name }}</span>
            </template>
            <el-menu-item v-for="child in menu.children" :key="child.id" :index="String(child.id)">
              {{ child.name }}
            </el-menu-item>
          </el-sub-menu>
          <el-menu-item v-else :index="String(menu.id)">
            <el-icon v-if="menu.icon"><component :is="menu.icon" /></el-icon>
            <span>{{ menu.name }}</span>
          </el-menu-item>
        </template>
      </el-menu>
    </div>

    <!-- 主内容区 -->
    <div class="portal-main">
      <div class="portal-header">
        <h3>{{ currentTitle }}</h3>
      </div>
      <div class="portal-content" v-loading="loading">
        <div v-if="!currentContent" class="welcome">
          <el-empty description="请从左侧菜单选择内容" />
        </div>
        <div v-else>
          <!-- 报表内容 -->
          <div v-if="currentType === 'report'" class="report-view">
            <div v-for="page in currentContent.pages" :key="page.id" class="report-page">
              <h4>{{ page.title }}</h4>
              <div class="page-items">
                <div v-for="item in page.items" :key="item.id" class="report-item" :style="getItemStyle(item)">
                  <div v-if="item.itemType === 'text'" v-html="item.textContent"></div>
                  <div v-else-if="item.itemType === 'image'"><img :src="item.imageUrl" /></div>
                  <div v-else class="chart-placeholder">图表 #{{ item.chartId }}</div>
                </div>
              </div>
            </div>
          </div>
          <!-- 面板内容 -->
          <div v-else-if="currentType === 'panel'" class="panel-view">
            <div v-for="item in currentContent.items" :key="item.id" class="panel-item">
              <el-card>
                <template #header>{{ item.chartName }}</template>
                <div class="chart-placeholder">{{ item.chartType }} 图表</div>
              </el-card>
            </div>
          </div>
          <!-- 图表内容 -->
          <div v-else-if="currentType === 'chart'" class="chart-view">
            <el-card>
              <template #header>{{ currentContent.name }}</template>
              <div class="chart-placeholder">{{ currentContent.chartType }} 图表</div>
            </el-card>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { getPortalMenus, viewByToken, type MenuItem } from '@/api/menu'

const menuTree = ref<MenuItem[]>([])
const activeMenu = ref('')
const currentTitle = ref('欢迎')
const currentType = ref('')
const currentContent = ref<any>(null)
const loading = ref(false)

// 扁平化菜单用于查找
const menuMap = ref<Map<number, MenuItem>>(new Map())

onMounted(async () => {
  const res = await getPortalMenus()
  menuTree.value = res.data || []
  buildMenuMap(menuTree.value)
})

function buildMenuMap(menus: MenuItem[]) {
  for (const m of menus) {
    menuMap.value.set(m.id, m)
    if (m.children) buildMenuMap(m.children)
  }
}

async function handleMenuSelect(index: string) {
  const menu = menuMap.value.get(Number(index))
  if (!menu) return

  activeMenu.value = index
  currentTitle.value = menu.name

  if (menu.menuType === 'link' && menu.linkUrl) {
    window.open(menu.linkUrl, '_blank')
    return
  }

  if (menu.menuType === 'publish' && (menu as any).publishToken) {
    loading.value = true
    try {
      const res = await viewByToken((menu as any).publishToken)
      currentType.value = res.data.objectType
      currentContent.value = res.data.content
    } catch (e) {
      console.error(e)
    } finally {
      loading.value = false
    }
  }
}

function getItemStyle(item: any): Record<string, string> {
  try {
    const layout = JSON.parse(item.layoutJson || '{}')
    return { left: `${layout.x}px`, top: `${layout.y}px`, width: `${layout.width}px`, height: `${layout.height}px`, position: 'absolute' }
  } catch { return {} }
}
</script>

<style scoped>
.portal-layout { display: flex; height: 100vh; }
.portal-sidebar { width: 240px; background: #1f2d3d; }
.portal-logo { height: 60px; display: flex; align-items: center; justify-content: center; color: #fff; border-bottom: 1px solid #304156; }
.portal-logo h2 { margin: 0; font-size: 18px; }
.portal-main { flex: 1; display: flex; flex-direction: column; background: #f5f7fa; }
.portal-header { height: 50px; background: #fff; border-bottom: 1px solid #e6e6e6; display: flex; align-items: center; padding: 0 20px; }
.portal-header h3 { margin: 0; font-size: 16px; }
.portal-content { flex: 1; padding: 20px; overflow: auto; }
.welcome { height: 100%; display: flex; align-items: center; justify-content: center; }
.report-page { margin-bottom: 20px; }
.page-items { position: relative; min-height: 400px; background: #fff; border: 1px solid #e0e0e0; }
.report-item { background: #fafafa; border: 1px solid #ddd; }
.panel-view { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 16px; }
.chart-placeholder { height: 200px; display: flex; align-items: center; justify-content: center; color: #999; background: #f5f5f5; }
</style>

