import { createRouter, createWebHistory, RouteRecordRaw } from 'vue-router'
import { useUserStore } from '@/stores/user'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/Login.vue'),
    meta: { requiresAuth: false }
  },
  {
    path: '/',
    component: () => import('@/layouts/MainLayout.vue'),
    redirect: '/dashboard',
    meta: { requiresAuth: true },
    children: [
      {
        path: 'dashboard',
        name: 'Dashboard',
        component: () => import('@/views/Dashboard.vue'),
        meta: { title: '工作台' }
      },
      {
        path: 'datasource',
        name: 'Datasource',
        component: () => import('@/views/datasource/List.vue'),
        meta: { title: '数据源管理' }
      },
      {
        path: 'dataset',
        name: 'Dataset',
        component: () => import('@/views/dataset/List.vue'),
        meta: { title: '数据集管理' }
      },
      {
        path: 'chart',
        name: 'Chart',
        component: () => import('@/views/chart/List.vue'),
        meta: { title: '图表管理' }
      },
      {
        path: 'chart/design/:id?',
        name: 'ChartDesign',
        component: () => import('@/views/chart/Design.vue'),
        meta: { title: '图表设计' }
      },
      {
        path: 'panel',
        name: 'Panel',
        component: () => import('@/views/panel/List.vue'),
        meta: { title: '分析面板' }
      },
      {
        path: 'panel/design/:id?',
        name: 'PanelDesign',
        component: () => import('@/views/panel/Design.vue'),
        meta: { title: '面板设计' }
      },
      {
        path: 'panel/preview/:id',
        name: 'PanelPreview',
        component: () => import('@/views/panel/Preview.vue'),
        meta: { title: '面板预览' }
      },
      {
        path: 'ai-analysis',
        name: 'AiAnalysis',
        component: () => import('@/views/admin/AiAnalysis.vue'),
        meta: { title: '智能分析' }
      },
      {
        path: 'kpi',
        name: 'KpiManage',
        component: () => import('@/views/admin/KpiManage.vue'),
        meta: { title: '指标知识库' }
      },
      {
        path: 'alert/rules',
        name: 'AlertRuleManage',
        component: () => import('@/views/admin/AlertRuleManage.vue'),
        meta: { title: '预警规则管理' }
      },
      {
        path: 'alert/events',
        name: 'AlertEventCenter',
        component: () => import('@/views/admin/AlertEventCenter.vue'),
        meta: { title: '预警事件中心' }
      },
      {
        path: 'knowledge',
        name: 'KnowledgeManage',
        component: () => import('@/views/admin/KnowledgeManage.vue'),
        meta: { title: '文档知识库' }
      },
      {
        path: 'system-config',
        name: 'SystemConfig',
        component: () => import('@/views/admin/SystemConfig.vue'),
        meta: { title: '系统配置' }
      },
      {
        path: 'report',
        name: 'Report',
        component: () => import('@/views/report/List.vue'),
        meta: { title: '报表管理' }
      },
      {
        path: 'report/design/:id',
        name: 'ReportDesign',
        component: () => import('@/views/report/Design.vue'),
        meta: { title: '报表设计' }
      },
      {
        path: 'admin/menu',
        name: 'MenuManage',
        component: () => import('@/views/admin/MenuManage.vue'),
        meta: { title: '菜单管理' }
      },
      {
        path: 'admin/publish',
        name: 'PublishManage',
        component: () => import('@/views/admin/PublishManage.vue'),
        meta: { title: '发布管理' }
      },
      {
        path: 'admin/knowledge-test',
        name: 'KnowledgeTest',
        component: () => import('@/views/admin/KnowledgeTest.vue'),
        meta: { title: '知识库检索' }
      }
    ]
  },
  // 报表预览（独立页面）
  {
    path: '/report/view/:id',
    name: 'ReportView',
    component: () => import('@/views/report/View.vue'),
    meta: { title: '报表预览', requiresAuth: false }
  },
  // 门户（公开访问）
  {
    path: '/portal',
    name: 'Portal',
    component: () => import('@/views/portal/Portal.vue'),
    meta: { title: '数据门户', requiresAuth: false }
  },
  {
    path: '/portal/view/:token',
    name: 'PortalView',
    component: () => import('@/views/portal/PortalView.vue'),
    meta: { title: '内容查看', requiresAuth: false }
  },
  // 大屏展示路由
  {
    path: '/screen/:id',
    name: 'Screen',
    component: () => import('@/views/screen/ScreenView.vue'),
    meta: { title: '大屏展示', requiresAuth: false }
  },
  // 大屏轮播路由 /screen/carousel?ids=1,2,3&interval=30
  {
    path: '/screen/carousel',
    name: 'ScreenCarousel',
    component: () => import('@/views/screen/ScreenCarousel.vue'),
    meta: { title: '大屏轮播', requiresAuth: false }
  },
  // 移动端路由
  {
    path: '/m',
    name: 'MobileHome',
    component: () => import('@/views/mobile/MobileHome.vue'),
    meta: { title: '移动端首页', requiresAuth: false }
  },
  {
    path: '/m/panel/:id',
    name: 'MobilePanel',
    component: () => import('@/views/mobile/MobilePanel.vue'),
    meta: { title: '面板详情', requiresAuth: false }
  },
  {
    path: '/m/charts',
    name: 'MobileCharts',
    component: () => import('@/views/mobile/MobileCharts.vue'),
    meta: { title: '图表列表', requiresAuth: false }
  },
  {
    path: '/m/ai',
    name: 'MobileAi',
    component: () => import('@/views/mobile/MobileAi.vue'),
    meta: { title: '智能分析', requiresAuth: false }
  },
  // 桌面悬浮窗专用智能分析页面（需要登录，但只显示智能分析界面）
  {
    path: '/desktop/ai',
    name: 'DesktopAi',
    component: () => import('@/views/desktop/DesktopAi.vue'),
    meta: { title: '智能分析助手', requiresAuth: true, isDesktopMode: true }
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

// 路由守卫
router.beforeEach((to, from, next) => {
  const userStore = useUserStore()
  const requiresAuth = to.meta.requiresAuth !== false

  if (requiresAuth && !userStore.isLoggedIn) {
    // 未登录，跳转到登录页，并记录原目标地址
    next({ name: 'Login', query: { redirect: to.fullPath } })
  } else if (to.name === 'Login' && userStore.isLoggedIn) {
    // 已登录访问登录页，根据来源决定跳转目标
    const redirect = to.query.redirect as string
    if (redirect) {
      next(redirect)
    } else if (from.meta.isDesktopMode) {
      // 如果来自桌面模式，返回桌面模式
      next({ name: 'DesktopAi' })
    } else {
      next({ name: 'Dashboard' })
    }
  } else {
    next()
  }
})

export default router
