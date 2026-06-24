<template>
  <div class="knowledge-test">
    <div class="test-layout">
      <!-- 左侧主区域 -->
      <div class="main-area">
        <!-- 页面标题 -->
        <div class="page-header">
          <h2>知识库检索</h2>
        </div>

        <!-- 搜索框 -->
        <div class="search-box">
          <el-input
            v-model="searchQuery"
            placeholder="请输入查询内容"
            size="large"
            clearable
            @keyup.enter="doSearch"
          >
            <template #append>
              <el-button :icon="Search" @click="doSearch" :loading="searching" />
            </template>
          </el-input>
        </div>

        <!-- 检索统计 -->
        <div class="search-stats" v-if="searchResult">
          <span><strong>检索结果</strong></span>
          <span class="stat-item">耗时: <strong>{{ searchResult.latencyMs }}ms</strong></span>
          <span class="stat-item">召回: <strong>{{ searchResult.results.length }}</strong> 条</span>
        </div>

        <!-- 检索结果列表 -->
        <div class="results-area" v-if="searchResult">
          <div
            v-for="(item, index) in searchResult.results"
            :key="item.chunkId"
            class="result-card"
          >
            <div class="result-header">
              <span class="rank">#{{ index + 1 }}</span>
              <span class="doc-title">{{ item.documentTitle }}</span>
              <el-tag type="success" size="small">{{ (item.score * 100).toFixed(1) }}%</el-tag>
              <div class="result-actions">
                <el-button link type="primary" size="small" @click="downloadDocument(item)">
                  <el-icon><Download /></el-icon> 下载
                </el-button>
              </div>
            </div>
            <div class="result-content">{{ item.content }}</div>
            <div class="result-meta">
              <span v-if="item.fileName" class="file-info">
                <el-icon><Document /></el-icon> {{ item.fileName }}
              </span>
              <span v-if="item.pageNumber">页码: {{ item.pageNumber }}</span>
              <span v-if="item.sectionTitle">章节: {{ item.sectionTitle }}</span>
            </div>
          </div>

          <el-empty v-if="searchResult.results.length === 0" description="未找到相关内容" />
        </div>

        <!-- 初始状态 -->
        <div class="empty-state" v-else>
          <el-empty description="输入关键词开始检索" />
        </div>
      </div>

      <!-- 右侧参数面板 -->
      <div class="params-panel">
        <div class="panel-header">
          <span>检索参数设置</span>
          <el-button link type="primary" @click="resetParams">重置</el-button>
        </div>

        <el-form label-position="top" size="small">
          <el-form-item label="召回数量">
            <el-slider v-model="searchParams.topK" :min="1" :max="20" show-input :show-input-controls="false" />
          </el-form-item>

          <el-form-item label="最小相似度">
            <el-slider v-model="searchParams.minScore" :min="0" :max="1" :step="0.05" show-input :show-input-controls="false" />
          </el-form-item>

          <el-divider />

          <el-form-item label="文档分类">
            <el-select v-model="searchParams.categoryId" placeholder="不限" clearable style="width: 100%">
              <el-option
                v-for="cat in categories"
                :key="cat.id"
                :label="cat.name"
                :value="cat.id"
              />
            </el-select>
          </el-form-item>

          <el-divider />

          <div class="history-section">
            <div class="section-title">历史查询</div>
            <div class="history-list">
              <div
                v-for="(item, idx) in searchHistory"
                :key="idx"
                class="history-item"
                @click="useHistoryQuery(item)"
              >
                {{ item }}
              </div>
              <div v-if="searchHistory.length === 0" class="no-history">暂无历史记录</div>
            </div>
            <el-button v-if="searchHistory.length > 0" link type="danger" @click="clearHistory" size="small">
              清空历史
            </el-button>
          </div>
        </el-form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { Search, Download, Document } from '@element-plus/icons-vue'
import { searchKnowledge, getCategories, type KnowledgeSearchResult, type KnowledgeCategory } from '@/api/knowledge'

// API基础路径
const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'

// 搜索相关
const searchQuery = ref('')
const searching = ref(false)
const searchResult = ref<{ results: KnowledgeSearchResult[]; latencyMs: number } | null>(null)

// 参数设置
const searchParams = ref({
  topK: 8,
  minScore: 0.3,
  categoryId: null as number | null
})

// 分类列表
const categories = ref<KnowledgeCategory[]>([])

// 搜索历史
const HISTORY_KEY = 'knowledge_search_history'
const searchHistory = ref<string[]>([])

onMounted(() => {
  loadCategories()
  loadHistory()
})

async function loadCategories() {
  try {
    const res = await getCategories()
    categories.value = res.data || []
  } catch (e) {
    console.error('加载分类失败', e)
  }
}

function loadHistory() {
  const saved = localStorage.getItem(HISTORY_KEY)
  if (saved) {
    try {
      searchHistory.value = JSON.parse(saved)
    } catch { searchHistory.value = [] }
  }
}

function saveHistory(query: string) {
  if (!query.trim()) return
  const history = searchHistory.value.filter(h => h !== query)
  history.unshift(query)
  searchHistory.value = history.slice(0, 10)
  localStorage.setItem(HISTORY_KEY, JSON.stringify(searchHistory.value))
}

function clearHistory() {
  searchHistory.value = []
  localStorage.removeItem(HISTORY_KEY)
}

function useHistoryQuery(query: string) {
  searchQuery.value = query
  doSearch()
}

function resetParams() {
  searchParams.value = { topK: 8, minScore: 0.3, categoryId: null }
}

async function doSearch() {
  if (!searchQuery.value.trim()) {
    ElMessage.warning('请输入查询内容')
    return
  }
  searching.value = true
  const startTime = Date.now()
  try {
    const res = await searchKnowledge({
      query: searchQuery.value,
      topK: searchParams.value.topK,
      minScore: searchParams.value.minScore,
      categoryId: searchParams.value.categoryId ?? undefined
    })
    const latencyMs = Date.now() - startTime
    searchResult.value = {
      results: res.data || [],
      latencyMs
    }
    saveHistory(searchQuery.value)
  } catch (e: unknown) {
    const errMsg = e instanceof Error ? e.message : '检索失败'
    ElMessage.error(errMsg)
  } finally {
    searching.value = false
  }
}

// 下载文档 - 使用fetch + blob方式确保正确下载
async function downloadDocument(item: KnowledgeSearchResult) {
  try {
    const url = `${API_BASE}/api/v1/knowledge/documents/${item.documentId}/download`
    const response = await fetch(url)

    if (!response.ok) {
      throw new Error(`下载失败: ${response.status}`)
    }

    // 从Content-Disposition获取文件名，或使用文档标题
    const contentDisposition = response.headers.get('Content-Disposition')
    let filename = item.documentTitle || `document_${item.documentId}`
    if (contentDisposition) {
      // 尝试解析filename*=UTF-8''xxx 或 filename="xxx"
      const utf8Match = contentDisposition.match(/filename\*=UTF-8''(.+)/i)
      const normalMatch = contentDisposition.match(/filename="?([^";\n]+)"?/i)
      if (utf8Match) {
        filename = decodeURIComponent(utf8Match[1])
      } else if (normalMatch) {
        filename = normalMatch[1]
      }
    }

    // 创建blob并下载
    const blob = await response.blob()
    const blobUrl = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = blobUrl
    link.download = filename
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(blobUrl)

    ElMessage.success('文档下载成功')
  } catch (error: any) {
    console.error('下载文档失败:', error)
    ElMessage.error(error.message || '下载文档失败')
  }
}
</script>

<style scoped>
.knowledge-test {
  height: calc(100vh - 100px);
  padding: 20px;
  box-sizing: border-box;
}

.test-layout {
  display: flex;
  gap: 20px;
  height: 100%;
}

.main-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.page-header {
  margin-bottom: 20px;
}

.page-header h2 {
  margin: 0 0 5px 0;
  font-size: 22px;
  color: #303133;
}

.page-header .subtitle {
  color: #909399;
  font-size: 14px;
}

.search-box {
  margin-bottom: 15px;
}

.search-stats {
  display: flex;
  align-items: center;
  gap: 20px;
  padding: 10px 15px;
  background: #f5f7fa;
  border-radius: 6px;
  margin-bottom: 15px;
  font-size: 14px;
  color: #606266;
}

.stat-item strong {
  color: #409eff;
}

.results-area {
  flex: 1;
  overflow-y: auto;
}

.result-card {
  background: #fff;
  border: 1px solid #ebeef5;
  border-radius: 8px;
  padding: 15px;
  margin-bottom: 12px;
  transition: box-shadow 0.2s;
}

.result-card:hover {
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.result-header {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 10px;
}

.result-header .rank {
  font-weight: bold;
  color: #409eff;
  font-size: 16px;
}

.result-header .doc-title {
  flex: 1;
  font-weight: 500;
  color: #303133;
}

.result-actions {
  display: flex;
  gap: 8px;
  margin-left: 10px;
}

.result-content {
  color: #606266;
  font-size: 14px;
  line-height: 1.6;
  max-height: 120px;
  overflow: hidden;
  text-overflow: ellipsis;
  background: #f9f9f9;
  padding: 10px;
  border-radius: 6px;
}

.result-meta {
  margin-top: 10px;
  font-size: 12px;
  color: #909399;
  display: flex;
  gap: 15px;
  align-items: center;
}

.file-info {
  display: flex;
  align-items: center;
  gap: 4px;
  color: #606266;
  background: #ecf5ff;
  padding: 2px 8px;
  border-radius: 4px;
}

.empty-state {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* 右侧参数面板 */
.params-panel {
  width: 280px;
  background: #fff;
  border: 1px solid #ebeef5;
  border-radius: 8px;
  padding: 15px;
  flex-shrink: 0;
  overflow-y: auto;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-weight: 600;
  color: #303133;
  margin-bottom: 15px;
  padding-bottom: 10px;
  border-bottom: 1px solid #ebeef5;
}

.history-section {
  margin-top: 10px;
}

.section-title {
  font-size: 14px;
  font-weight: 500;
  color: #303133;
  margin-bottom: 10px;
}

.history-list {
  max-height: 200px;
  overflow-y: auto;
}

.history-item {
  padding: 8px 10px;
  background: #f5f7fa;
  border-radius: 4px;
  margin-bottom: 6px;
  font-size: 13px;
  color: #606266;
  cursor: pointer;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  transition: background 0.2s;
}

.history-item:hover {
  background: #ecf5ff;
  color: #409eff;
}

.no-history {
  color: #909399;
  font-size: 13px;
  text-align: center;
  padding: 20px 0;
}
</style>

