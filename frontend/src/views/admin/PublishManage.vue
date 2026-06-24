<template>
  <div class="publish-manage">
    <div class="page-header">
      <h2>发布管理</h2>
      <el-button type="primary" @click="handleCreate">
        <el-icon><Plus /></el-icon>新建发布
      </el-button>
    </div>

    <el-table :data="list" v-loading="loading" stripe>
      <el-table-column prop="id" label="ID" width="80" />
      <el-table-column prop="title" label="标题" min-width="150" />
      <el-table-column prop="objectType" label="类型" width="100">
        <template #default="{ row }">
          <el-tag :type="getTypeTag(row.objectType)" size="small">{{ getTypeText(row.objectType) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="accessScope" label="访问范围" width="100">
        <template #default="{ row }">
          <el-tag :type="getScopeTag(row.accessScope)" size="small">{{ getScopeText(row.accessScope) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="viewCount" label="访问次数" width="100" align="center" />
      <el-table-column prop="isEnabled" label="状态" width="80">
        <template #default="{ row }">
          <el-switch v-model="row.isEnabled" @change="handleToggle(row)" />
        </template>
      </el-table-column>
      <el-table-column prop="accessToken" label="访问链接" min-width="200">
        <template #default="{ row }">
          <el-input :model-value="getShareUrl(row.accessToken)" readonly size="small">
            <template #append>
              <el-button @click="copyLink(row.accessToken)">复制</el-button>
            </template>
          </el-input>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="{ row }">
          <el-button type="primary" link @click="handleEdit(row)">编辑</el-button>
          <el-button type="danger" link @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 编辑弹窗 -->
    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑发布' : '新建发布'" width="500px">
      <el-form :model="form" label-width="80px">
        <el-form-item label="标题" required>
          <el-input v-model="form.title" placeholder="发布标题" />
        </el-form-item>
        <el-form-item label="类型" required>
          <el-select v-model="form.objectType" style="width: 100%;">
            <el-option label="报表" value="report" />
            <el-option label="面板" value="panel" />
            <el-option label="图表" value="chart" />
          </el-select>
        </el-form-item>
        <el-form-item label="对象ID" required>
          <el-input-number v-model="form.objectId" :min="1" style="width: 100%;" />
        </el-form-item>
        <el-form-item label="访问范围">
          <el-radio-group v-model="form.accessScope">
            <el-radio value="public">公开</el-radio>
            <el-radio value="private">私有</el-radio>
            <el-radio value="role">角色</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="访问密码">
          <el-input v-model="form.accessPassword" placeholder="可选，留空则无需密码" />
        </el-form-item>
        <el-form-item label="过期时间">
          <el-date-picker v-model="form.expireAt" type="datetime" placeholder="可选" style="width: 100%;" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.remark" type="textarea" :rows="2" />
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
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import { getPublishList, createPublish, updatePublish, deletePublish, togglePublish, type PublishItem } from '@/api/menu'

const loading = ref(false)
const saving = ref(false)
const list = ref<PublishItem[]>([])
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)

const form = ref({
  title: '', objectType: 'report', objectId: 0, accessScope: 'public',
  accessPassword: '', expireAt: '', remark: ''
})

onMounted(() => loadList())

async function loadList() {
  loading.value = true
  try {
    const res = await getPublishList()
    list.value = res.data || []
  } finally { loading.value = false }
}

function handleCreate() {
  editingId.value = null
  form.value = { title: '', objectType: 'report', objectId: 0, accessScope: 'public', accessPassword: '', expireAt: '', remark: '' }
  dialogVisible.value = true
}

function handleEdit(row: PublishItem) {
  editingId.value = row.id
  form.value = { title: row.title, objectType: row.objectType, objectId: row.objectId, accessScope: row.accessScope, accessPassword: '', expireAt: row.expireAt || '', remark: row.remark || '' }
  dialogVisible.value = true
}

async function handleSave() {
  if (!form.value.title || !form.value.objectId) { ElMessage.warning('请填写必填项'); return }
  saving.value = true
  try {
    if (editingId.value) {
      await updatePublish(editingId.value, form.value)
      ElMessage.success('更新成功')
    } else {
      await createPublish(form.value)
      ElMessage.success('发布成功')
    }
    dialogVisible.value = false
    await loadList()
  } finally { saving.value = false }
}

async function handleDelete(row: PublishItem) {
  await ElMessageBox.confirm('确定取消发布吗？', '确认', { type: 'warning' })
  await deletePublish(row.id)
  ElMessage.success('已取消发布')
  await loadList()
}

async function handleToggle(row: PublishItem) {
  await togglePublish(row.id)
}

function getShareUrl(token: string) { return `${location.origin}/portal/view/${token}` }
function copyLink(token: string) {
  navigator.clipboard.writeText(getShareUrl(token))
  ElMessage.success('链接已复制')
}
function getTypeTag(t: string) { return t === 'report' ? 'primary' : t === 'panel' ? 'success' : 'warning' }
function getTypeText(t: string) { return t === 'report' ? '报表' : t === 'panel' ? '面板' : '图表' }
function getScopeTag(s: string) { return s === 'public' ? 'success' : s === 'private' ? 'info' : 'warning' }
function getScopeText(s: string) { return s === 'public' ? '公开' : s === 'private' ? '私有' : '角色' }
</script>

<style scoped>
.publish-manage { padding: 20px; }
.page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
</style>

