<template>
  <div class="kpi-manage">
    <el-row :gutter="16">
      <!-- 左侧分类树 -->
      <el-col :span="6">
        <el-card class="category-card">
          <template #header>
            <div class="card-header">
              <span>指标分类</span>
              <el-button type="primary" size="small" @click="showCategoryDialog()">新增</el-button>
            </div>
          </template>
          <el-tree
            ref="treeRef"
            :data="categories"
            :props="{ label: 'name', children: 'children' }"
            node-key="id"
            highlight-current
            @node-click="handleCategoryClick"
          >
            <template #default="{ node, data }">
              <span class="tree-node">
                <span>{{ node.label }}</span>
                <span class="tree-actions">
                  <el-button link size="small" @click.stop="showCategoryDialog(data)">编辑</el-button>
                  <el-button link size="small" type="danger" @click.stop="handleDeleteCategory(data)">删除</el-button>
                </span>
              </span>
            </template>
          </el-tree>
        </el-card>
      </el-col>
      
      <!-- 右侧指标列表 -->
      <el-col :span="18">
        <el-card>
          <template #header>
            <div class="card-header">
              <el-input v-model="keyword" placeholder="搜索指标" style="width: 200px" @keyup.enter="loadDefinitions" clearable />
              <div>
                <el-button type="primary" @click="showDefinitionDialog()">新增指标</el-button>
                <el-button @click="handleBatchGenerateEmbedding" :disabled="!selectedIds.length">批量生成向量</el-button>
              </div>
            </div>
          </template>
          
          <el-table :data="definitions" v-loading="loading" @selection-change="handleSelectionChange">
            <el-table-column type="selection" width="50" />
            <el-table-column prop="code" label="编码" width="120" />
            <el-table-column prop="name" label="名称" min-width="150" />
            <el-table-column prop="categoryName" label="分类" width="100" />
            <el-table-column prop="unit" label="单位" width="80" />
            <el-table-column label="向量状态" width="100">
              <template #default="{ row }">
                <el-tag v-if="row.hasEmbedding" type="success" size="small">已生成</el-tag>
                <el-tag v-else type="info" size="small">未生成</el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="isEnabled" label="状态" width="80">
              <template #default="{ row }">
                <el-tag :type="row.isEnabled ? 'success' : 'danger'" size="small">
                  {{ row.isEnabled ? '启用' : '禁用' }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="200" fixed="right">
              <template #default="{ row }">
                <el-button link size="small" @click="showDefinitionDialog(row)">编辑</el-button>
                <el-button link size="small" @click="handleGenerateEmbedding(row)" :disabled="row.hasEmbedding">生成向量</el-button>
                <el-button link size="small" type="danger" @click="handleDeleteDefinition(row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
          
          <el-pagination
            v-model:current-page="page"
            v-model:page-size="pageSize"
            :total="total"
            layout="total, sizes, prev, pager, next"
            :page-sizes="[10, 20, 50]"
            @change="loadDefinitions"
            style="margin-top: 16px; justify-content: flex-end"
          />
        </el-card>
      </el-col>
    </el-row>
    
    <!-- 分类编辑弹窗 -->
    <el-dialog v-model="categoryDialogVisible" :title="editingCategory ? '编辑分类' : '新增分类'" width="400px">
      <el-form :model="categoryForm" label-width="80px">
        <el-form-item label="名称" required>
          <el-input v-model="categoryForm.name" />
        </el-form-item>
        <el-form-item label="上级分类">
          <el-tree-select
            v-model="categoryForm.parentId"
            :data="categories"
            :props="{ label: 'name', children: 'children' }"
            node-key="id"
            value-key="id"
            check-strictly
            clearable
            placeholder="选择上级分类"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="categoryForm.description" type="textarea" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="categoryForm.sortOrder" :min="0" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="categoryDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSaveCategory">保存</el-button>
      </template>
    </el-dialog>
    
    <!-- 指标编辑弹窗 -->
    <el-dialog v-model="definitionDialogVisible" :title="editingDefinition ? '编辑指标' : '新增指标'" width="600px">
      <el-form :model="definitionForm" label-width="100px">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="编码" required>
              <el-input v-model="definitionForm.code" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="名称" required>
              <el-input v-model="definitionForm.name" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="分类" required>
              <el-tree-select
                v-model="definitionForm.categoryId"
                :data="categories"
                :props="{ label: 'name', children: 'children' }"
                node-key="id"
                value-key="id"
                check-strictly
                placeholder="选择分类"
                style="width: 100%"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="单位">
              <el-input v-model="definitionForm.unit" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="指标口径">
          <el-input v-model="definitionForm.definition" type="textarea" :rows="3" placeholder="指标的业务定义说明" />
        </el-form-item>
        <el-form-item label="计算公式">
          <el-input v-model="definitionForm.formula" type="textarea" :rows="2" placeholder="如：门诊人次 / 门诊收入" />
        </el-form-item>
        <el-form-item label="SQL模板">
          <el-input v-model="definitionForm.sqlTemplate" type="textarea" :rows="4" placeholder="可包含参数占位符，如 {{start_date}}" />
        </el-form-item>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="数据类型">
              <el-select v-model="definitionForm.dataType" style="width: 100%">
                <el-option label="数值" value="number" />
                <el-option label="百分比" value="percent" />
                <el-option label="金额" value="currency" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="启用状态">
              <el-switch v-model="definitionForm.isEnabled" />
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>
      <template #footer>
        <el-button @click="definitionDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSaveDefinition">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { KpiCategory, KpiCategoryForm, KpiDefinition, KpiDefinitionForm } from '@/api/kpi'
import * as kpiApi from '@/api/kpi'

// 分类相关
const categories = ref<KpiCategory[]>([])
const categoryDialogVisible = ref(false)
const editingCategory = ref<KpiCategory | null>(null)
const categoryForm = ref<KpiCategoryForm>({
  name: '',
  parentId: null,
  description: null,
  sortOrder: 0
})

// 指标相关
const definitions = ref<KpiDefinition[]>([])
const loading = ref(false)
const keyword = ref('')
const selectedCategoryId = ref<number | null>(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const selectedIds = ref<number[]>([])
const definitionDialogVisible = ref(false)
const editingDefinition = ref<KpiDefinition | null>(null)
const definitionForm = ref<KpiDefinitionForm>({
  code: '',
  name: '',
  categoryId: 0,
  definition: null,
  formula: null,
  sqlTemplate: null,
  datasourceId: null,
  unit: null,
  dataType: 'number',
  isEnabled: true
})

onMounted(() => {
  loadCategories()
  loadDefinitions()
})

const loadCategories = async () => {
  const res = await kpiApi.getCategories()
  if (res.code === 0) {
    categories.value = res.data
  }
}

const loadDefinitions = async () => {
  loading.value = true
  try {
    const res = await kpiApi.getDefinitions({
      categoryId: selectedCategoryId.value ?? undefined,
      keyword: keyword.value || undefined,
      page: page.value,
      pageSize: pageSize.value
    })
    if (res.code === 0) {
      definitions.value = res.data.items
      total.value = res.data.total
    }
  } finally {
    loading.value = false
  }
}

const handleCategoryClick = (data: KpiCategory) => {
  selectedCategoryId.value = data.id
  page.value = 1
  loadDefinitions()
}

const showCategoryDialog = (category?: KpiCategory) => {
  editingCategory.value = category || null
  categoryForm.value = category
    ? { name: category.name, parentId: category.parentId, description: category.description, sortOrder: category.sortOrder }
    : { name: '', parentId: null, description: null, sortOrder: 0 }
  categoryDialogVisible.value = true
}

const handleSaveCategory = async () => {
  if (!categoryForm.value.name) {
    ElMessage.warning('请输入分类名称')
    return
  }
  const res = editingCategory.value
    ? await kpiApi.updateCategory(editingCategory.value.id, categoryForm.value)
    : await kpiApi.createCategory(categoryForm.value)
  if (res.code === 0) {
    ElMessage.success('保存成功')
    categoryDialogVisible.value = false
    loadCategories()
  } else {
    ElMessage.error(res.message)
  }
}

const handleDeleteCategory = async (category: KpiCategory) => {
  await ElMessageBox.confirm('确定删除该分类吗？', '提示', { type: 'warning' })
  const res = await kpiApi.deleteCategory(category.id)
  if (res.code === 0) {
    ElMessage.success('删除成功')
    loadCategories()
  } else {
    ElMessage.error(res.message)
  }
}

const showDefinitionDialog = (definition?: KpiDefinition) => {
  editingDefinition.value = definition || null
  definitionForm.value = definition
    ? {
        code: definition.code,
        name: definition.name,
        categoryId: definition.categoryId,
        definition: definition.definition,
        formula: definition.formula,
        sqlTemplate: definition.sqlTemplate,
        datasourceId: definition.datasourceId,
        unit: definition.unit,
        dataType: definition.dataType,
        isEnabled: definition.isEnabled
      }
    : {
        code: '',
        name: '',
        categoryId: selectedCategoryId.value || 0,
        definition: null,
        formula: null,
        sqlTemplate: null,
        datasourceId: null,
        unit: null,
        dataType: 'number',
        isEnabled: true
      }
  definitionDialogVisible.value = true
}

const handleSaveDefinition = async () => {
  if (!definitionForm.value.code || !definitionForm.value.name) {
    ElMessage.warning('请填写编码和名称')
    return
  }
  if (!definitionForm.value.categoryId) {
    ElMessage.warning('请选择分类')
    return
  }
  const res = editingDefinition.value
    ? await kpiApi.updateDefinition(editingDefinition.value.id, definitionForm.value)
    : await kpiApi.createDefinition(definitionForm.value)
  if (res.code === 0) {
    ElMessage.success('保存成功')
    definitionDialogVisible.value = false
    loadDefinitions()
  } else {
    ElMessage.error(res.message)
  }
}

const handleDeleteDefinition = async (definition: KpiDefinition) => {
  await ElMessageBox.confirm('确定删除该指标吗？', '提示', { type: 'warning' })
  const res = await kpiApi.deleteDefinition(definition.id)
  if (res.code === 0) {
    ElMessage.success('删除成功')
    loadDefinitions()
  } else {
    ElMessage.error(res.message)
  }
}

const handleSelectionChange = (rows: KpiDefinition[]) => {
  selectedIds.value = rows.map(r => r.id)
}

const handleGenerateEmbedding = async (definition: KpiDefinition) => {
  const res = await kpiApi.generateEmbedding(definition.id)
  if (res.code === 0) {
    ElMessage.success('向量生成成功')
    loadDefinitions()
  } else {
    ElMessage.error(res.message)
  }
}

const handleBatchGenerateEmbedding = async () => {
  const res = await kpiApi.generateEmbeddings(selectedIds.value)
  if (res.code === 0) {
    ElMessage.success('批量生成成功')
    loadDefinitions()
  } else {
    ElMessage.error(res.message)
  }
}
</script>

<style scoped>
.kpi-manage {
  padding: 16px;
}
.category-card {
  height: calc(100vh - 140px);
}
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.tree-node {
  flex: 1;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-right: 8px;
}
.tree-actions {
  opacity: 0;
  transition: opacity 0.2s;
}
.tree-node:hover .tree-actions {
  opacity: 1;
}
</style>

