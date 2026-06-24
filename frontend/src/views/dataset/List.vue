<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { formatDateTime } from '@/utils/format'
import {
  getDatasetList,
  getDatasetDetail,
  createDataset,
  updateDataset,
  deleteDataset,
  previewDataset,
  type Dataset,
  type DatasetField,
  type ColumnInfo
} from '@/api/dataset'
import { getDatasourceList, type Datasource } from '@/api/datasource'
const loading = ref(false)
const list = ref<Dataset[]>([])
const dialogVisible = ref(false)
const saving = ref(false)
const previewing = ref(false)
const datasourceList = ref<Datasource[]>([])
const previewColumns = ref<ColumnInfo[]>([])
const previewRows = ref<Record<string, any>[]>([])

const form = ref({
  id: 0,
  name: '',
  datasourceId: 0,
  sqlText: '',
  paramSchema: '',
  remark: '',
  fields: [] as DatasetField[]
})

onMounted(async () => {
  await loadDatasources()
  await loadList()
})

async function loadDatasources() {
  try {
    const res = await getDatasourceList()
    if (res.code === 0) {
      datasourceList.value = res.data?.filter((d: Datasource) => d.isEnabled) || []
    }
  } catch (e) {
    console.error(e)
  }
}

async function loadList() {
  loading.value = true
  try {
    const res = await getDatasetList()
    if (res.code === 0) {
      list.value = res.data || []
    }
  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

function showAdd() {
  form.value = { id: 0, name: '', datasourceId: 0, sqlText: '', paramSchema: '', remark: '', fields: [] }
  previewColumns.value = []
  previewRows.value = []
  dialogVisible.value = true
}

async function handleEdit(row: Dataset) {
  try {
    const res = await getDatasetDetail(row.id)
    if (res.code === 0 && res.data) {
      form.value = {
        id: res.data.id,
        name: res.data.name,
        datasourceId: res.data.datasourceId,
        sqlText: res.data.sqlText,
        paramSchema: res.data.paramSchema || '',
        remark: res.data.remark || '',
        fields: res.data.fields || []
      }
      previewColumns.value = []
      previewRows.value = []
      dialogVisible.value = true
    }
  } catch (e) {
    console.error(e)
  }
}

async function handleDelete(row: Dataset) {
  await ElMessageBox.confirm('确定删除该数据集吗？', '提示')
  try {
    const res = await deleteDataset(row.id)
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

async function handlePreview() {
  if (!form.value.datasourceId || !form.value.sqlText) {
    ElMessage.warning('请选择数据源并输入SQL')
    return
  }
  previewing.value = true
  try {
    const res = await previewDataset({
      datasourceId: form.value.datasourceId,
      sqlText: form.value.sqlText,
      maxRows: 50
    })
    if (res.code === 0 && res.data) {
      previewColumns.value = res.data.columns
      previewRows.value = res.data.rows
      // 自动生成字段 - 包含完整信息
      form.value.fields = res.data.columns.map((col, i) => ({
        fieldName: col.name,
        fieldAlias: col.name, // 默认别名与字段名相同
        dataType: col.dataType,
        role: 'dim' as const, // 默认为维度
        aggType: 'none', // 默认无聚合
        sortOrder: i
      }))
      ElMessage.success(`预览成功，共${res.data.totalRows}行`)
    } else {
      ElMessage.error(res.message || '预览失败')
    }
  } catch (e) {
    console.error(e)
  } finally {
    previewing.value = false
  }
}

async function handleSave() {
  if (!form.value.name || !form.value.datasourceId || !form.value.sqlText) {
    ElMessage.warning('请填写名称、选择数据源并输入SQL')
    return
  }
  saving.value = true
  try {
    const payload = {
      name: form.value.name,
      datasourceId: form.value.datasourceId,
      sqlText: form.value.sqlText,
      paramSchema: form.value.paramSchema || undefined,
      remark: form.value.remark || undefined,
      fields: form.value.fields
    }
    if (form.value.id) {
      const res = await updateDataset(form.value.id, payload)
      if (res.code === 0) {
        ElMessage.success('保存成功')
        dialogVisible.value = false
        loadList()
      } else {
        ElMessage.error(res.message || '保存失败')
      }
    } else {
      const res = await createDataset(payload)
      if (res.code === 0) {
        ElMessage.success('创建成功')
        dialogVisible.value = false
        loadList()
      } else {
        ElMessage.error(res.message || '创建失败')
      }
    }
  } catch (e) {
    console.error(e)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="page-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>数据集管理</span>
          <el-button type="primary" @click="showAdd">新建数据集</el-button>
        </div>
      </template>
      <el-table :data="list" v-loading="loading" stripe>
        <el-table-column prop="name" label="名称" />
        <el-table-column prop="datasourceName" label="所属数据源" />
        <el-table-column prop="remark" label="备注" />
        <el-table-column prop="createdAt" label="创建时间" width="180">
          <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="150" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click="handleEdit(row)">编辑</el-button>
            <el-button link type="danger" @click="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑数据集' : '新建数据集'" width="900px" top="5vh">
      <el-form :model="form" label-width="100px">
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="名称" required>
              <el-input v-model="form.name" placeholder="数据集名称" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="数据源" required>
              <el-select v-model="form.datasourceId" placeholder="选择数据源" style="width: 100%">
                <el-option v-for="ds in datasourceList" :key="ds.id" :label="ds.name" :value="ds.id" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="SQL语句" required>
          <el-input v-model="form.sqlText" type="textarea" :rows="6" placeholder="SELECT * FROM ..." />
          <div style="margin-top: 8px;">
            <el-button size="small" :loading="previewing" @click="handlePreview">预览数据</el-button>
          </div>
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.remark" placeholder="备注说明" />
        </el-form-item>
      </el-form>

      <!-- 字段列表 - 只定义字段名、别名、类型 -->
      <div v-if="form.fields.length > 0" style="margin-top: 16px;">
        <h4>字段列表</h4>
        <p class="field-hint">提示：维度/度量/聚合方式将在图表设计器中配置</p>
        <el-table :data="form.fields" size="small" max-height="200">
          <el-table-column prop="fieldName" label="字段名" width="180" />
          <el-table-column prop="fieldAlias" label="显示名称" min-width="180">
            <template #default="{ row }">
              <el-input v-model="row.fieldAlias" size="small" placeholder="设置显示名称" />
            </template>
          </el-table-column>
          <el-table-column prop="dataType" label="数据类型" width="150">
            <template #default="{ row }">
              <el-tag size="small" type="info">{{ row.dataType }}</el-tag>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <!-- 预览数据 -->
      <div v-if="previewRows.length > 0" style="margin-top: 16px;">
        <h4>数据预览 (前50行)</h4>
        <el-table :data="previewRows" size="small" max-height="200">
          <el-table-column v-for="col in previewColumns" :key="col.name" :prop="col.name" :label="col.name" min-width="100" />
        </el-table>
      </div>

      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.card-header { display: flex; justify-content: space-between; align-items: center; }
h4 { margin: 0 0 8px 0; font-size: 14px; color: #606266; }
.field-hint { font-size: 12px; color: #909399; margin: 0 0 8px 0; }
</style>

