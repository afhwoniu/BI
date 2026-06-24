<script setup lang="ts">
import { ref, onMounted } from 'vue'

const stats = ref([
  { title: '数据源', count: 0, icon: 'Connection', color: '#409eff' },
  { title: '数据集', count: 0, icon: 'Grid', color: '#67c23a' },
  { title: '图表', count: 0, icon: 'PieChart', color: '#e6a23c' },
  { title: '分析面板', count: 0, icon: 'DataBoard', color: '#f56c6c' }
])

onMounted(() => {
  // 模拟加载统计数据
  setTimeout(() => {
    stats.value[0].count = 3
    stats.value[1].count = 12
    stats.value[2].count = 28
    stats.value[3].count = 5
  }, 500)
})
</script>

<template>
  <div class="dashboard">
    <h2>欢迎使用智能BI可视化平台</h2>
    <el-row :gutter="20" class="stats-row">
      <el-col :span="6" v-for="item in stats" :key="item.title">
        <el-card shadow="hover" class="stat-card">
          <div class="stat-icon" :style="{ background: item.color }">
            <el-icon :size="28"><component :is="item.icon" /></el-icon>
          </div>
          <div class="stat-info">
            <div class="count">{{ item.count }}</div>
            <div class="title">{{ item.title }}</div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <el-row :gutter="20" style="margin-top: 20px;">
      <el-col :span="16">
        <el-card>
          <template #header>
            <span>快速开始</span>
          </template>
          <el-steps :active="0" align-center>
            <el-step title="配置数据源" description="连接SQL Server/PostgreSQL/MySQL" />
            <el-step title="创建数据集" description="编写SQL定义数据集" />
            <el-step title="设计图表" description="拖拽字段生成图表" />
            <el-step title="发布面板" description="组合图表创建分析面板" />
          </el-steps>
        </el-card>
      </el-col>
      <el-col :span="8">
        <el-card>
          <template #header>
            <span>系统信息</span>
          </template>
          <el-descriptions :column="1" border size="small">
            <el-descriptions-item label="系统版本">V5.0.0</el-descriptions-item>
            <el-descriptions-item label="后端框架">.NET 9 + PostgreSQL</el-descriptions-item>
            <el-descriptions-item label="前端框架">Vue3 + Element Plus</el-descriptions-item>
          </el-descriptions>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<style scoped lang="scss">
.dashboard {
  h2 { margin-bottom: 20px; color: #333; }
  .stat-card {
    display: flex; align-items: center; padding: 10px;
    .stat-icon {
      width: 60px; height: 60px; border-radius: 8px; display: flex;
      align-items: center; justify-content: center; color: #fff; margin-right: 15px;
    }
    .stat-info {
      .count { font-size: 28px; font-weight: bold; color: #333; }
      .title { color: #999; font-size: 14px; }
    }
  }
}
</style>

