<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getPanelList, deletePanel, type Panel } from '@/api/panel'
import { formatDateTime } from '@/utils/format'

const router = useRouter()
const loading = ref(false)
const list = ref<Panel[]>([])

const panelTypes: Record<string, string> = {
  pc_dashboard: 'PC仪表盘',
  big_screen: '大屏',
  mobile: '移动端'
}

onMounted(() => {
  loadList()
})

async function loadList() {
  loading.value = true
  try {
    const res = await getPanelList()
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
  router.push({ name: 'PanelDesign', params: id ? { id } : {} })
}

function goPreview(id: number) {
  router.push({ name: 'PanelPreview', params: { id } })
}

async function handleDelete(row: Panel) {
  await ElMessageBox.confirm('确定删除该面板吗？', '提示')
  try {
    const res = await deletePanel(row.id)
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
          <span>分析面板</span>
          <el-button type="primary" @click="goDesign()">新建面板</el-button>
        </div>
      </template>
      <el-table :data="list" v-loading="loading" stripe>
        <el-table-column prop="name" label="名称" />
        <el-table-column prop="panelType" label="类型" width="120">
          <template #default="{ row }">{{ panelTypes[row.panelType] || row.panelType }}</template>
        </el-table-column>
        <el-table-column prop="remark" label="备注" />
        <el-table-column prop="createdAt" label="创建时间" width="180">
          <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="180" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click="goDesign(row.id)">设计</el-button>
            <el-button link type="success" @click="goPreview(row.id)">预览</el-button>
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

