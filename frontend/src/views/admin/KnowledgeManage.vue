<template>
  <div class="knowledge-manage">
    <el-row :gutter="16">
      <!-- 左侧分类树 -->
      <el-col :span="6">
        <el-card class="category-card">
          <template #header>
            <div class="card-header">
              <span>知识库分类</span>
              <el-button type="primary" size="small" @click="showCategoryDialog()">新增</el-button>
            </div>
          </template>
          <el-tree
            ref="treeRef"
            :data="categories"
            :props="{ label: 'name', children: 'children' }"
            node-key="id"
            highlight-current
            default-expand-all
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
      
      <!-- 右侧文档列表 -->
      <el-col :span="18">
        <el-card>
          <template #header>
            <div class="card-header">
              <div class="search-bar">
                <el-input v-model="keyword" placeholder="搜索文档" style="width: 200px" @keyup.enter="loadDocuments" clearable />
                <el-select v-model="statusFilter" placeholder="状态" clearable style="width: 120px; margin-left: 8px">
                  <el-option label="待处理" value="pending" />
                  <el-option label="处理中" value="processing" />
                  <el-option label="已完成" value="completed" />
                  <el-option label="失败" value="failed" />
                </el-select>
              </div>
              <div>
                <el-button type="primary" @click="showUploadDialog">
                  <el-icon><Upload /></el-icon> 上传文档
                </el-button>
              </div>
            </div>
          </template>
          
          <el-table :data="documents" v-loading="loading">
            <el-table-column prop="title" label="标题" min-width="200" />
            <el-table-column prop="fileName" label="文件名" width="150" show-overflow-tooltip />
            <el-table-column prop="fileType" label="类型" width="80" />
            <el-table-column label="大小" width="100">
              <template #default="{ row }">
                {{ formatFileSize(row.fileSize) }}
              </template>
            </el-table-column>
            <el-table-column prop="chunkCount" label="分块数" width="80" />
            <el-table-column label="状态" width="140">
              <template #default="{ row }">
                <div v-if="row.status === 'processing' || row.status === 'pending'" class="status-progress">
                  <el-progress
                    :percentage="row.processProgress || 0"
                    :stroke-width="12"
                    :format="(p: number) => p + '%'"
                    style="width: 100px"
                  />
                  <span class="progress-text">{{ row.status === 'pending' ? '等待中' : '处理中' }}</span>
                </div>
                <el-tag v-else :type="getStatusType(row.status)" size="small">
                  {{ getStatusText(row.status) }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="createdAt" label="上传时间" width="160">
              <template #default="{ row }">
                {{ formatDate(row.createdAt) }}
              </template>
            </el-table-column>
            <el-table-column label="操作" width="180" fixed="right">
              <template #default="{ row }">
                <el-button link size="small" @click="showDocumentDetail(row)">详情</el-button>
                <el-button link size="small" @click="handleReprocess(row)" :disabled="row.status === 'processing'">
                  重新处理
                </el-button>
                <el-button link size="small" type="danger" @click="handleDeleteDocument(row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
          
          <el-pagination
            v-model:current-page="page"
            v-model:page-size="pageSize"
            :total="total"
            layout="total, sizes, prev, pager, next"
            :page-sizes="[10, 20, 50]"
            @change="loadDocuments"
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
      </el-form>
      <template #footer>
        <el-button @click="categoryDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSaveCategory">保存</el-button>
      </template>
    </el-dialog>
    
    <!-- 上传文档弹窗 -->
    <el-dialog v-model="uploadDialogVisible" title="上传文档" width="500px">
      <el-form :model="uploadForm" label-width="80px">
        <el-form-item label="标题" required>
          <el-input v-model="uploadForm.title" placeholder="文档标题" />
        </el-form-item>
        <el-form-item label="分类">
          <el-tree-select
            v-model="uploadForm.categoryId"
            :data="categories"
            :props="{ label: 'name', children: 'children' }"
            node-key="id"
            value-key="id"
            check-strictly
            clearable
            placeholder="选择分类"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="文件" required>
          <el-upload
            ref="uploadRef"
            :auto-upload="false"
            :limit="1"
            :on-change="handleFileChange"
            accept=".txt,.md,.pdf,.docx,.xls,.xlsx"
          >
            <el-button type="primary">选择文件</el-button>
            <template #tip>
              <div class="el-upload__tip">支持 txt、md、pdf、docx、xls、xlsx 格式</div>
            </template>
          </el-upload>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="uploadDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleUpload" :loading="uploading">上传</el-button>
      </template>
    </el-dialog>

    <!-- 文档详情弹窗 -->
    <el-dialog v-model="detailDialogVisible" title="文档详情" width="900px">
      <el-descriptions v-if="currentDocument" :column="2" border>
        <el-descriptions-item label="标题" :span="2">{{ currentDocument.title }}</el-descriptions-item>
        <el-descriptions-item label="文件名">{{ currentDocument.fileName }}</el-descriptions-item>
        <el-descriptions-item label="文件类型">{{ currentDocument.fileType }}</el-descriptions-item>
        <el-descriptions-item label="文件大小">{{ formatFileSize(currentDocument.fileSize) }}</el-descriptions-item>
        <el-descriptions-item label="分块数量">{{ currentDocument.chunkCount }}</el-descriptions-item>
        <el-descriptions-item label="状态">
          <el-tag :type="getStatusType(currentDocument.status)">{{ getStatusText(currentDocument.status) }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="上传时间">{{ formatDate(currentDocument.createdAt) }}</el-descriptions-item>
        <el-descriptions-item v-if="currentDocument.errorMessage" label="错误信息" :span="2">
          <el-text type="danger">{{ currentDocument.errorMessage }}</el-text>
        </el-descriptions-item>
      </el-descriptions>

      <!-- 分块列表 -->
      <div v-if="currentDocument && currentDocument.chunkCount > 0" style="margin-top: 20px">
        <h4 style="margin-bottom: 12px">分块内容预览</h4>
        <el-table :data="documentChunks" v-loading="loadingChunks" max-height="400" border>
          <el-table-column prop="chunkIndex" label="序号" width="70" align="center" />
          <el-table-column prop="content" label="内容" min-width="400">
            <template #default="{ row }">
              <el-tooltip :content="row.content" placement="top" :show-after="500">
                <span class="chunk-content">{{ row.content }}</span>
              </el-tooltip>
            </template>
          </el-table-column>
          <el-table-column prop="contentLength" label="字符数" width="80" align="center" />
          <el-table-column prop="pageNumber" label="页码" width="70" align="center">
            <template #default="{ row }">{{ row.pageNumber || '-' }}</template>
          </el-table-column>
          <el-table-column prop="sectionTitle" label="章节" width="120">
            <template #default="{ row }">{{ row.sectionTitle || '-' }}</template>
          </el-table-column>
        </el-table>
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Upload } from '@element-plus/icons-vue'
import type { UploadFile } from 'element-plus'
import * as knowledgeApi from '@/api/knowledge'
import type { KnowledgeCategory, KnowledgeDocument, KnowledgeChunk } from '@/api/knowledge'

// 状态轮询定时器
let statusPollingTimer: ReturnType<typeof setInterval> | null = null

// 分类相关
const categories = ref<KnowledgeCategory[]>([])
const categoryDialogVisible = ref(false)
const editingCategory = ref<KnowledgeCategory | null>(null)
const categoryForm = ref({ name: '', parentId: null as number | null, description: '' })

// 文档相关
const documents = ref<KnowledgeDocument[]>([])
const loading = ref(false)
const keyword = ref('')
const statusFilter = ref('')
const selectedCategoryId = ref<number | null>(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)

// 上传相关
const uploadDialogVisible = ref(false)
const uploading = ref(false)
const uploadForm = ref({ title: '', categoryId: null as number | null })
const selectedFile = ref<File | null>(null)

// 详情相关
const detailDialogVisible = ref(false)
const currentDocument = ref<KnowledgeDocument | null>(null)
const documentChunks = ref<KnowledgeChunk[]>([])
const loadingChunks = ref(false)

onMounted(() => {
  loadCategories()
  loadDocuments()
  // 启动状态轮询
  startStatusPolling()
})

onUnmounted(() => {
  // 清理轮询定时器
  stopStatusPolling()
})

watch([statusFilter], () => {
  page.value = 1
  loadDocuments()
})

// 启动状态轮询（每3秒检查处理中的文档）
const startStatusPolling = () => {
  if (statusPollingTimer) return
  statusPollingTimer = setInterval(async () => {
    // 找出正在处理的文档
    const processingDocs = documents.value.filter(
      d => d.status === 'pending' || d.status === 'processing'
    )
    if (processingDocs.length === 0) return

    // 逐个更新状态
    for (const doc of processingDocs) {
      try {
        const res = await knowledgeApi.getDocumentStatus(doc.id)
        if (res.code === 0) {
          // 更新文档状态
          const idx = documents.value.findIndex(d => d.id === doc.id)
          if (idx >= 0) {
            documents.value[idx].status = res.data.status
            documents.value[idx].processProgress = res.data.progress
            documents.value[idx].chunkCount = res.data.chunkCount
            documents.value[idx].processedChunkCount = res.data.processedChunkCount
            documents.value[idx].errorMessage = res.data.errorMessage
          }
        }
      } catch (e) {
        console.error('轮询状态失败', e)
      }
    }
  }, 3000)
}

const stopStatusPolling = () => {
  if (statusPollingTimer) {
    clearInterval(statusPollingTimer)
    statusPollingTimer = null
  }
}

const loadCategories = async () => {
  const res = await knowledgeApi.getCategories()
  if (res.code === 0) {
    categories.value = res.data
  }
}

const loadDocuments = async () => {
  loading.value = true
  try {
    const res = await knowledgeApi.getDocuments({
      categoryId: selectedCategoryId.value ?? undefined,
      status: statusFilter.value || undefined,
      keyword: keyword.value || undefined,
      page: page.value,
      pageSize: pageSize.value
    })
    if (res.code === 0) {
      documents.value = res.data.items
      total.value = res.data.total
    }
  } finally {
    loading.value = false
  }
}

const handleCategoryClick = (data: KnowledgeCategory) => {
  selectedCategoryId.value = data.id
  page.value = 1
  loadDocuments()
}

const showCategoryDialog = (category?: KnowledgeCategory) => {
  editingCategory.value = category || null
  categoryForm.value = category
    ? { name: category.name, parentId: category.parentId ?? null, description: category.description ?? '' }
    : { name: '', parentId: null, description: '' }
  categoryDialogVisible.value = true
}

const handleSaveCategory = async () => {
  if (!categoryForm.value.name) {
    ElMessage.warning('请输入分类名称')
    return
  }
  const data = {
    name: categoryForm.value.name,
    parentId: categoryForm.value.parentId || undefined,
    description: categoryForm.value.description
  }
  const res = editingCategory.value
    ? await knowledgeApi.updateCategory(editingCategory.value.id, data)
    : await knowledgeApi.createCategory(data)
  if (res.code === 0) {
    ElMessage.success('保存成功')
    categoryDialogVisible.value = false
    loadCategories()
  } else {
    ElMessage.error(res.message)
  }
}

const handleDeleteCategory = async (category: KnowledgeCategory) => {
  await ElMessageBox.confirm('确定删除该分类吗？', '提示', { type: 'warning' })
  const res = await knowledgeApi.deleteCategory(category.id)
  if (res.code === 0) {
    ElMessage.success('删除成功')
    loadCategories()
  } else {
    ElMessage.error(res.message)
  }
}

// 上传相关
const showUploadDialog = () => {
  uploadForm.value = { title: '', categoryId: selectedCategoryId.value }
  selectedFile.value = null
  uploadDialogVisible.value = true
}

const handleFileChange = (file: UploadFile) => {
  selectedFile.value = file.raw as File
  if (!uploadForm.value.title) {
    uploadForm.value.title = file.name.replace(/\.[^.]+$/, '')
  }
}

const handleUpload = async () => {
  if (!uploadForm.value.title) {
    ElMessage.warning('请输入文档标题')
    return
  }
  if (!selectedFile.value) {
    ElMessage.warning('请选择文件')
    return
  }

  uploading.value = true
  try {
    const formData = new FormData()
    formData.append('title', uploadForm.value.title)
    formData.append('file', selectedFile.value)
    if (uploadForm.value.categoryId) {
      formData.append('categoryId', String(uploadForm.value.categoryId))
    }

    const res = await knowledgeApi.uploadDocument(formData)
    if (res.code === 0) {
      ElMessage.success('上传成功，正在处理中...')
      uploadDialogVisible.value = false
      loadDocuments()
    } else {
      ElMessage.error(res.message)
    }
  } finally {
    uploading.value = false
  }
}

const handleDeleteDocument = async (doc: KnowledgeDocument) => {
  await ElMessageBox.confirm('确定删除该文档吗？相关分块和向量也会被删除。', '提示', { type: 'warning' })
  const res = await knowledgeApi.deleteDocument(doc.id)
  if (res.code === 0) {
    ElMessage.success('删除成功')
    loadDocuments()
  } else {
    ElMessage.error(res.message)
  }
}

const handleReprocess = async (doc: KnowledgeDocument) => {
  await ElMessageBox.confirm('确定重新处理该文档吗？', '提示', { type: 'info' })
  const res = await knowledgeApi.reprocessDocument(doc.id)
  if (res.code === 0) {
    ElMessage.success('已开始重新处理')
    loadDocuments()
  } else {
    ElMessage.error(res.message)
  }
}

const showDocumentDetail = async (doc: KnowledgeDocument) => {
  currentDocument.value = doc
  detailDialogVisible.value = true
  documentChunks.value = []

  // 加载分块列表
  if (doc.chunkCount > 0) {
    loadingChunks.value = true
    try {
      const res = await knowledgeApi.getDocumentChunks(doc.id)
      if (res.code === 0) {
        documentChunks.value = res.data || []
      }
    } catch (e) {
      console.error('加载分块失败', e)
    } finally {
      loadingChunks.value = false
    }
  }
}

// 工具方法
const formatFileSize = (size?: number) => {
  if (!size) return '-'
  if (size < 1024) return size + ' B'
  if (size < 1024 * 1024) return (size / 1024).toFixed(1) + ' KB'
  return (size / 1024 / 1024).toFixed(1) + ' MB'
}

const formatDate = (dateStr: string) => {
  return new Date(dateStr).toLocaleString('zh-CN')
}

const getStatusType = (status: string): 'info' | 'warning' | 'success' | 'danger' => {
  const map: Record<string, 'info' | 'warning' | 'success' | 'danger'> = {
    pending: 'info',
    processing: 'warning',
    completed: 'success',
    failed: 'danger'
  }
  return map[status] || 'info'
}

const getStatusText = (status: string) => {
  const map: Record<string, string> = {
    pending: '待处理',
    processing: '处理中',
    completed: '已完成',
    failed: '失败'
  }
  return map[status] || status
}
</script>

<style scoped>
.knowledge-manage {
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
.search-bar {
  display: flex;
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
.chunk-content {
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
  text-overflow: ellipsis;
  line-height: 1.5;
  max-height: 4.5em;
}
.status-progress {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 2px;
}
.status-progress .progress-text {
  font-size: 12px;
  color: #909399;
}
</style>

