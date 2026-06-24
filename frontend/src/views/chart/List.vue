<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getChartList, deleteChart, type Chart } from '@/api/chart'
import { formatDateTime } from '@/utils/format'

const router = useRouter()
const loading = ref(false)
const list = ref<Chart[]>([])

const chartTypeLabels: Record<string, string> = {
  bar: '柱状图',
  line: '折线图',
  pie: '饼图',
  table: '表格',
  kpi: 'KPI卡片'
}

onMounted(() => {
  loadList()
})

async function loadList() {
  loading.value = true
  try {
    const res = await getChartList()
    if (res.code === 0) {
      list.value = res.data || []
    }
  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

function goDesign(id?: number) {
  router.push({ name: 'ChartDesign', params: id ? { id } : {} })
}

async function handleDelete(row: Chart) {
  await ElMessageBox.confirm('确定删除该图表吗？', '提示')
  try {
    const res = await deleteChart(row.id)
    if (res.code === 0) {
      ElMessage.success('删除成功')
      loadList()
    } else {
      ElMessage.error(res.message || '删除失败')
    }
  } catch (e) {
    console.error(e)
  }
}
</script>

<template>
  <div class="page-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>图表管理</span>
          <el-button type="primary" @click="goDesign()">新建图表</el-button>
        </div>
      </template>
      <el-table :data="list" v-loading="loading" stripe>
        <el-table-column prop="name" label="名称" />
        <el-table-column prop="chartType" label="类型" width="120">
          <template #default="{ row }">
            {{ chartTypeLabels[row.chartType] || row.chartType }}
          </template>
        </el-table-column>
        <el-table-column prop="datasetName" label="数据集" />
        <el-table-column prop="createdAt" label="创建时间" width="180">
          <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="150" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click="goDesign(row.id)">设计</el-button>
            <el-button link type="danger" @click="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<style scoped>
.card-header { display: flex; justify-content: space-between; align-items: center; }
</style>

