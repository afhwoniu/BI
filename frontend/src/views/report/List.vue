<template>
  <div class="report-list">
    <div class="page-header">
      <h2>报表管理</h2>
      <el-button type="primary" @click="handleCreate">
        <el-icon><Plus /></el-icon>新建报表
      </el-button>
    </div>

    <el-table :data="list" v-loading="loading" stripe>
      <el-table-column prop="id" label="ID" width="80" />
      <el-table-column prop="name" label="报表名称" min-width="200" />
      <el-table-column prop="reportType" label="类型" width="100">
        <template #default="{ row }">
          <el-tag :type="row.reportType === 'report' ? 'primary' : 'success'" size="small">
            {{ row.reportType === 'report' ? '报告' : '仪表板' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="pageCount" label="页数" width="80" align="center" />
      <el-table-column prop="isPublished" label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="row.isPublished ? 'success' : 'info'" size="small">
            {{ row.isPublished ? '已发布' : '未发布' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="200" fixed="right">
        <template #default="{ row }">
          <el-button type="primary" link @click="handleEdit(row)">编辑</el-button>
          <el-button type="success" link @click="handlePreview(row)">预览</el-button>
          <el-button type="danger" link @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 创建/编辑弹窗 -->
    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑报表' : '新建报表'" width="500px">
      <el-form :model="form" label-width="80px">
        <el-form-item label="名称" required>
          <el-input v-model="form.name" placeholder="请输入报表名称" />
        </el-form-item>
        <el-form-item label="类型">
          <el-radio-group v-model="form.reportType">
            <el-radio value="report">报告</el-radio>
            <el-radio value="dashboard">仪表板</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.remark" type="textarea" :rows="3" placeholder="可选备注" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave" :loading="saving">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import { 
  getReportList, 
  createReport, 
  updateReport, 
  deleteReport,
  type ReportListItem 
} from '@/api/report'

const router = useRouter()
const loading = ref(false)
const saving = ref(false)
const list = ref<ReportListItem[]>([])
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)

const form = ref({
  name: '',
  reportType: 'report',
  remark: ''
})

onMounted(() => {
  loadList()
})

async function loadList() {
  loading.value = true
  try {
    const res = await getReportList()
    list.value = res.data || []
  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

function handleCreate() {
  editingId.value = null
  form.value = { name: '', reportType: 'report', remark: '' }
  dialogVisible.value = true
}

function handleEdit(row: ReportListItem) {
  router.push(`/report/design/${row.id}`)
}

function handlePreview(row: ReportListItem) {
  window.open(`/report/view/${row.id}`, '_blank')
}

async function handleDelete(row: ReportListItem) {
  await ElMessageBox.confirm(`确定删除报表"${row.name}"吗？`, '确认删除', { type: 'warning' })
  await deleteReport(row.id)
  ElMessage.success('删除成功')
  loadList()
}

async function handleSave() {
  if (!form.value.name) {
    ElMessage.warning('请输入报表名称')
    return
  }
  saving.value = true
  try {
    if (editingId.value) {
      await updateReport(editingId.value, form.value)
      ElMessage.success('更新成功')
    } else {
      const res = await createReport(form.value)
      ElMessage.success('创建成功')
      // 跳转到编辑页
      router.push(`/report/design/${res.data.id}`)
    }
    dialogVisible.value = false
    loadList()
  } catch (e) {
    console.error(e)
  } finally {
    saving.value = false
  }
}

function formatDate(dateStr: string) {
  if (!dateStr) return ''
  return new Date(dateStr).toLocaleString('zh-CN')
}
</script>

<style scoped>
.report-list {
  padding: 20px;
}
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}
</style>

