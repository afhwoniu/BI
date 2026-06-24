<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { Document } from '@element-plus/icons-vue'
import DevNotesDialog from '@/components/DevNotesDialog.vue'

const router = useRouter()
const route = useRoute()
const userStore = useUserStore()

const isCollapse = ref(false)
const showDevNotes = ref(false) // 开发说明弹窗

const menuItems = [
  { path: '/dashboard', title: '工作台', icon: 'HomeFilled' },
  { path: '/datasource', title: '数据源管理', icon: 'Connection' },
  { path: '/dataset', title: '数据集管理', icon: 'Grid' },
  { path: '/chart', title: '图表管理', icon: 'PieChart' },
  { path: '/panel', title: '分析面板', icon: 'DataBoard' },
  { path: '/report', title: '报表管理', icon: 'Document' },
  { path: '/ai-analysis', title: '智能分析', icon: 'ChatDotRound' },
  { path: '/kpi', title: '指标知识库', icon: 'Collection' },
  { path: '/alert/rules', title: '预警规则', icon: 'Bell' },
  { path: '/alert/events', title: '预警事件', icon: 'Warning' },
  { path: '/knowledge', title: '文档知识库', icon: 'Folder' },
  { path: '/admin/knowledge-test', title: '知识库检索', icon: 'Aim' },
  { path: '/admin/menu', title: '菜单管理', icon: 'Menu' },
  { path: '/admin/publish', title: '发布管理', icon: 'Share' },
  { path: '/system-config', title: '系统配置', icon: 'Setting' }
]

const activeMenu = computed(() => route.path)

function handleLogout() {
  userStore.logout()
  router.push('/login')
}
</script>

<template>
  <el-container class="layout-container">
    <!-- 侧边栏 -->
    <el-aside :width="isCollapse ? '64px' : '220px'" class="sidebar">
      <div class="logo">
        <span v-if="!isCollapse">智能BI平台</span>
        <span v-else>BI</span>
      </div>
      <el-menu
        :default-active="activeMenu"
        :collapse="isCollapse"
        router
        background-color="#304156"
        text-color="#bfcbd9"
        active-text-color="#409eff"
      >
        <el-menu-item v-for="item in menuItems" :key="item.path" :index="item.path">
          <el-icon><component :is="item.icon" /></el-icon>
          <template #title>{{ item.title }}</template>
        </el-menu-item>
      </el-menu>
    </el-aside>

    <!-- 主内容区 -->
    <el-container>
      <el-header class="header">
        <el-icon class="collapse-btn" @click="isCollapse = !isCollapse">
          <component :is="isCollapse ? 'Expand' : 'Fold'" />
        </el-icon>
        <div class="header-right">
          <!-- 开发说明按钮 -->
          <el-tooltip content="开发说明" placement="bottom">
            <el-button
              class="dev-notes-btn"
              :icon="Document"
              circle
              @click="showDevNotes = true"
            />
          </el-tooltip>

          <el-dropdown @command="handleLogout">
            <span class="user-info">
              <el-avatar :size="32" :src="userStore.avatar || undefined">
                {{ userStore.realName?.charAt(0) || 'U' }}
              </el-avatar>
              <span class="username">{{ userStore.realName || userStore.username }}</span>
            </span>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item command="logout">退出登录</el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </el-header>
      <el-main class="main-content">
        <router-view />
      </el-main>
    </el-container>
  </el-container>

  <!-- 开发说明弹窗 -->
  <DevNotesDialog v-model="showDevNotes" />
</template>

<style scoped lang="scss">
.layout-container {
  height: 100%;
}
.sidebar {
  background: #304156;
  transition: width 0.3s;
  .logo {
    height: 60px; display: flex; align-items: center; justify-content: center;
    color: #fff; font-size: 18px; font-weight: bold; background: #263445;
  }
  .el-menu { border-right: none; }
}
.header {
  display: flex; align-items: center; justify-content: space-between;
  background: #fff; border-bottom: 1px solid #e6e6e6; padding: 0 20px;
  .collapse-btn { font-size: 20px; cursor: pointer; }
  .header-right { display: flex; align-items: center; gap: 16px; }
  .dev-notes-btn { margin-right: 8px; }
  .user-info { display: flex; align-items: center; cursor: pointer; }
  .username { margin-left: 8px; }
}
.main-content { background: #f5f7fa; padding: 20px; }
</style>
