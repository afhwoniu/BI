<template>
  <el-dialog
    v-model="visible"
    title="生成Word报告"
    width="950px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <!-- 步骤条 -->
    <el-steps :active="currentStep" finish-status="success" simple style="margin-bottom: 20px">
      <el-step title="选择会话" />
      <el-step title="编辑大纲" />
      <el-step title="预览导出" />
    </el-steps>

    <!-- 步骤1：选择会话 -->
    <div v-if="currentStep === 0" class="step-content">
      <el-form label-width="100px">
        <el-form-item label="报告标题" required>
          <el-input v-model="reportTitle" placeholder="请输入报告标题" />
        </el-form-item>
        <el-form-item label="报告要求">
          <el-input v-model="reportIdea" type="textarea" :rows="2" placeholder="例如：请突出分析数据趋势变化，给出改进建议" />
        </el-form-item>
        <el-form-item label="选择会话" required>
          <div class="session-select-area">
            <el-checkbox-group v-model="selectedSessionIds">
              <el-checkbox
                v-for="session in sessions"
                :key="session.id"
                :label="session.id"
                class="session-checkbox"
              >
                <div class="session-info">
                  <span class="session-title">{{ session.title }}</span>
                  <span class="session-time">{{ formatTime(session.lastActiveAt) }}</span>
                </div>
              </el-checkbox>
            </el-checkbox-group>
            <el-empty v-if="sessions.length === 0" description="暂无可用会话" :image-size="60" />
          </div>
        </el-form-item>
      </el-form>
    </div>

    <!-- 步骤2：编辑大纲 -->
    <div v-else-if="currentStep === 1" class="step-content">
      <div class="outline-editor">
        <!-- 章节列表 -->
        <div class="chapters-list">
          <div class="chapters-header">
            <span>章节列表</span>
            <el-button link type="primary" @click="addChapter">
              <el-icon><Plus /></el-icon>
            </el-button>
          </div>
          <draggable
            v-model="outline.chapters"
            item-key="title"
            handle=".drag-handle"
            animation="200"
            ghost-class="chapter-ghost"
          >
            <template #item="{ element: chapter, index }">
              <div
                class="chapter-item"
                :class="{ active: selectedChapterIndex === index }"
                @click="selectChapter(index)"
              >
                <div class="chapter-header">
                  <el-icon class="drag-handle"><Rank /></el-icon>
                  <span class="chapter-order">{{ index + 1 }}</span>
                  <el-tag :type="getChapterTypeTag(chapter.type)" size="small">{{ getChapterTypeLabel(chapter.type) }}</el-tag>
                  <el-button link type="danger" class="delete-btn" @click.stop="removeChapter(index)" :disabled="outline.chapters.length <= 1">
                    <el-icon><Delete /></el-icon>
                  </el-button>
                </div>
                <div class="chapter-title">{{ chapter.title || '(未命名)' }}</div>
              </div>
            </template>
          </draggable>
        </div>

        <!-- 章节编辑区 -->
        <div class="chapter-editor" v-if="selectedChapter">
          <el-form label-width="80px">
            <el-form-item label="章节标题">
              <el-input v-model="selectedChapter.title" placeholder="请输入章节标题" />
            </el-form-item>
            <el-form-item label="章节类型">
              <el-radio-group v-model="selectedChapter.type">
                <el-radio-button value="text">纯文本</el-radio-button>
                <el-radio-button value="table">表格</el-radio-button>
                <el-radio-button value="chart">图表</el-radio-button>
                <el-radio-button value="conclusion">总结</el-radio-button>
              </el-radio-group>
            </el-form-item>
            <el-form-item label="正文内容">
              <el-input
                v-model="selectedChapter.content"
                type="textarea"
                :rows="8"
                placeholder="请输入章节正文内容"
              />
            </el-form-item>
            <!-- 图表/表格类型时显示图片预览 -->
            <el-form-item v-if="(selectedChapter.type === 'chart' || selectedChapter.type === 'table') && (selectedChapter.chartImageUrls?.length ?? 0) > 0" label="图表预览">
              <div class="chart-preview-area">
                <img
                  v-for="(url, idx) in selectedChapter.chartImageUrls"
                  :key="idx"
                  :src="getChartImageUrl(url)"
                  class="chart-preview-image"
                  @error="handleImageError"
                />
              </div>
            </el-form-item>
            <!-- 表格数据预览 -->
            <el-form-item v-if="(selectedChapter.type === 'table' || selectedChapter.type === 'chart') && (selectedChapter.tableData?.length ?? 0) > 0" label="数据预览">
              <div class="table-preview-hint">
                共 {{ selectedChapter.tableData?.length ?? 0 }} 条数据，将在导出时生成表格
              </div>
            </el-form-item>
          </el-form>
        </div>
      </div>
      <!-- 查看AI提示词 -->
      <div class="prompt-section" v-if="outline.systemPrompt || outline.userPrompt">
        <div class="prompt-header" style="cursor: pointer;" @click="showAiPrompt = !showAiPrompt">
          <el-icon><View /></el-icon>
          <span>查看AI提示词</span>
          <el-icon style="margin-left: auto; transition: transform 0.3s;" :style="{ transform: showAiPrompt ? 'rotate(180deg)' : '' }"><ArrowDown /></el-icon>
        </div>
        <div v-show="showAiPrompt" class="ai-prompt-content">
          <div v-if="outline.systemPrompt" class="prompt-block">
            <div class="prompt-block-title">系统提示词 (System Prompt)</div>
            <pre class="prompt-text">{{ outline.systemPrompt }}</pre>
          </div>
          <div v-if="outline.userPrompt" class="prompt-block">
            <div class="prompt-block-title">用户提示词 (User Prompt)</div>
            <pre class="prompt-text">{{ outline.userPrompt }}</pre>
          </div>
        </div>
      </div>
    </div>

    <!-- 步骤3：预览导出 -->
    <div v-else-if="currentStep === 2" class="step-content">
      <div class="template-selector">
        <span class="template-label">选择模板：</span>
        <div class="template-options">
          <div
            v-for="tpl in templates"
            :key="tpl.value"
            class="template-option"
            :class="{ active: selectedTemplate === tpl.value }"
            @click="selectedTemplate = tpl.value"
          >
            <div class="template-preview" :style="{ backgroundColor: tpl.color }"></div>
            <span class="template-name">{{ tpl.label }}</span>
          </div>
        </div>
        <span class="chapter-count">共 {{ outline.chapters.length }} 个章节</span>
      </div>

      <!-- 报告预览 -->
      <div class="report-preview">
        <div class="report-header">
          <h1 class="report-title">{{ outline.title || reportTitle }}</h1>
          <p v-if="outline.subtitle" class="report-subtitle">{{ outline.subtitle }}</p>
        </div>
        <div v-if="outline.abstract" class="report-abstract">
          <h3>摘 要</h3>
          <p>{{ outline.abstract }}</p>
        </div>
        <div class="report-toc">
          <h3>目 录</h3>
          <div v-for="(chapter, idx) in outline.chapters" :key="idx" class="toc-item">
            <div class="toc-title">{{ idx + 1 }}. {{ chapter.title }} <span class="toc-type">[{{ chapter.type === 'chart' ? '图表' : chapter.type === 'table' ? '表格' : chapter.type === 'conclusion' ? '结论' : '文本' }}]</span></div>
            <div v-if="chapter.content" class="toc-summary">{{ chapter.content.substring(0, 120) }}{{ chapter.content.length > 120 ? '...' : '' }}</div>
          </div>
        </div>
      </div>
    </div>

    <!-- 底部按钮 -->
    <template #footer>
      <el-button @click="handleClose">取消</el-button>
      <el-button v-if="currentStep > 0" @click="prevStep">上一步</el-button>
      <el-button v-if="currentStep < 2" type="primary" @click="nextStep" :loading="generating">
        {{ currentStep === 0 ? '生成大纲' : '下一步' }}
      </el-button>
      <el-button v-if="currentStep === 2" type="primary" @click="exportWord" :loading="exporting">
        <el-icon><Download /></el-icon>
        导出Word
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Plus, Delete, Rank, Download, View, ArrowDown } from '@element-plus/icons-vue'
import draggable from 'vuedraggable'
import type { SessionListItem, WordChapter, WordOutlineResponse } from '@/api/ai'
import { generateWordOutline, generateWordFile } from '@/api/ai'

// 标签类型定义
type TagType = 'primary' | 'success' | 'info' | 'warning' | 'danger'

// Props
interface Props {
  sessions: SessionListItem[]
  datasourceId?: number
}
const props = defineProps<Props>()

// 弹窗控制
const visible = defineModel<boolean>('visible', { default: false })

// 步骤控制
const currentStep = ref(0)
const generating = ref(false)
const exporting = ref(false)

// 表单数据
const reportTitle = ref('')
const reportIdea = ref('')
const selectedSessionIds = ref<number[]>([])

// 大纲数据
const outline = ref<WordOutlineResponse>({ title: '', chapters: [] })
const selectedChapterIndex = ref(0)
const showAiPrompt = ref(false)

// 模板选择
const selectedTemplate = ref('formal')
const templates = [
  { value: 'formal', label: '正式报告', color: '#003366' },
  { value: 'simple', label: '简约版', color: '#666666' },
  { value: 'academic', label: '学术版', color: '#000000' }
]

// 当前选中的章节
const selectedChapter = computed(() => {
  if (outline.value.chapters.length === 0) return null
  return outline.value.chapters[selectedChapterIndex.value]
})

// 格式化时间
function formatTime(date: string) {
  if (!date) return ''
  return new Date(date).toLocaleString('zh-CN', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
}

// 获取章节类型标签
function getChapterTypeLabel(type: string): string {
  const labels: Record<string, string> = { text: '文本', table: '表格', chart: '图表', conclusion: '总结' }
  return labels[type] || '文本'
}

function getChapterTypeTag(type: string): TagType {
  const tags: Record<string, TagType> = { text: 'info', table: 'warning', chart: 'primary', conclusion: 'success' }
  return tags[type] || 'info'
}

// 选择章节
function selectChapter(index: number) {
  selectedChapterIndex.value = index
}

// 添加章节
function addChapter() {
  const newChapter: WordChapter = {
    order: outline.value.chapters.length + 1,
    type: 'text',
    title: '新章节',
    content: ''
  }
  outline.value.chapters.push(newChapter)
  selectedChapterIndex.value = outline.value.chapters.length - 1
}

// 删除章节
function removeChapter(index: number) {
  if (outline.value.chapters.length <= 1) {
    ElMessage.warning('至少保留一个章节')
    return
  }
  outline.value.chapters.splice(index, 1)
  if (selectedChapterIndex.value >= outline.value.chapters.length) {
    selectedChapterIndex.value = outline.value.chapters.length - 1
  }
}

// 上一步
function prevStep() {
  if (currentStep.value > 0) currentStep.value--
}

// 图片加载失败处理
function handleImageError(e: Event) {
  const img = e.target as HTMLImageElement
  if (img) {
    img.style.display = 'none'
  }
}

// 获取图表截图完整URL（拼接后端地址）
function getChartImageUrl(imgUrl: string): string {
  if (!imgUrl) return ''
  // 如果已经是完整URL直接返回
  if (imgUrl.startsWith('http://') || imgUrl.startsWith('https://')) {
    return imgUrl
  }
  // 拼接后端API地址
  const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'
  return `${baseUrl}${imgUrl}`
}

// 下一步
async function nextStep() {
  if (currentStep.value === 0) {
    if (!reportTitle.value.trim()) {
      ElMessage.warning('请输入报告标题')
      return
    }
    if (selectedSessionIds.value.length === 0) {
      ElMessage.warning('请至少选择一个会话')
      return
    }
    generating.value = true
    try {
      const res = await generateWordOutline({
        sessionIds: selectedSessionIds.value,
        title: reportTitle.value,
        idea: reportIdea.value,
        datasourceId: props.datasourceId
      })
      if (res.code === 0 && res.data) {
        outline.value = res.data
        selectedChapterIndex.value = 0
        currentStep.value = 1
        ElMessage.success('大纲生成成功')
      } else {
        ElMessage.error(res.message || '生成大纲失败')
      }
    } catch (err: any) {
      ElMessage.error(err.message || '生成大纲失败')
    } finally {
      generating.value = false
    }
  } else {
    currentStep.value++
  }
}

// 导出Word
async function exportWord() {
  exporting.value = true
  try {
    const res = await generateWordFile({
      outline: outline.value,
      template: selectedTemplate.value,
      datasourceId: props.datasourceId
    })
    if (res.code === 0 && res.data) {
      // 解码base64并下载
      const byteCharacters = atob(res.data)
      const byteNumbers = new Array(byteCharacters.length)
      for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i)
      }
      const byteArray = new Uint8Array(byteNumbers)
      const blob = new Blob([byteArray], { type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' })
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `${reportTitle.value || 'Word报告'}.docx`
      link.click()
      URL.revokeObjectURL(url)
      ElMessage.success('Word导出成功，您可以继续调整或关闭窗口')
    } else {
      ElMessage.error(res.message || '导出失败')
    }
  } catch (err: any) {
    ElMessage.error(err.message || '导出失败')
  } finally {
    exporting.value = false
  }
}

// 关闭弹窗
function handleClose() {
  visible.value = false
  currentStep.value = 0
  reportTitle.value = ''
  reportIdea.value = ''
  selectedSessionIds.value = []
  outline.value = { title: '', chapters: [] }
  selectedChapterIndex.value = 0
  selectedTemplate.value = 'formal'
}

// 监听弹窗打开
watch(visible, (val) => {
  if (val && props.sessions.length > 0) {
    selectedSessionIds.value = [props.sessions[0].id]
  }
})
</script>

<style scoped>
.step-content {
  min-height: 400px;
}

.session-select-area {
  max-height: 200px;
  overflow-y: auto;
  border: 1px solid var(--el-border-color);
  border-radius: 4px;
  padding: 10px;
}

.session-checkbox {
  display: block;
  margin-bottom: 8px;
}

.session-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.session-title {
  font-weight: 500;
}

.session-time {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.outline-editor {
  display: flex;
  gap: 20px;
  min-height: 400px;
}

.prompt-section {
  margin-top: 16px;
  padding: 12px;
  background: var(--el-fill-color-light);
  border-radius: 8px;
  border: 1px dashed var(--el-border-color);
}

.prompt-header {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 8px;
  font-size: 14px;
  color: var(--el-text-color-secondary);
}

.ai-prompt-content {
  margin-top: 8px;
}

.prompt-block {
  margin-bottom: 12px;
}

.prompt-block-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin-bottom: 4px;
  padding: 2px 8px;
  background: var(--el-color-primary-light-9);
  border-radius: 4px;
  display: inline-block;
}

.prompt-text {
  font-size: 12px;
  line-height: 1.6;
  color: var(--el-text-color-regular);
  background: var(--el-fill-color);
  padding: 8px 12px;
  border-radius: 6px;
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 300px;
  overflow-y: auto;
  margin: 4px 0 0 0;
}

.chapters-list {
  width: 200px;
  border: 1px solid var(--el-border-color);
  border-radius: 4px;
  overflow: hidden;
}

.chapters-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px;
  background: var(--el-fill-color-light);
  border-bottom: 1px solid var(--el-border-color);
  font-weight: 500;
}

.chapter-item {
  padding: 10px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  cursor: pointer;
  transition: background 0.2s;
}

.chapter-item:hover {
  background: var(--el-fill-color-lighter);
}

.chapter-item.active {
  background: var(--el-color-primary-light-9);
  border-left: 3px solid var(--el-color-primary);
}

.chapter-header {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 4px;
}

.drag-handle {
  cursor: move;
  color: var(--el-text-color-secondary);
}

.chapter-order {
  font-weight: 600;
  color: var(--el-text-color-secondary);
}

.delete-btn {
  margin-left: auto;
}

.chapter-title {
  font-size: 13px;
  color: var(--el-text-color-regular);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.chapter-ghost {
  opacity: 0.5;
  background: var(--el-color-primary-light-8);
}

.chapter-editor {
  flex: 1;
  padding: 10px;
  border: 1px solid var(--el-border-color);
  border-radius: 4px;
}

.chart-preview-area {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.chart-preview-image {
  max-width: 100%;
  max-height: 200px;
  border: 1px solid var(--el-border-color);
  border-radius: 4px;
  object-fit: contain;
}

.table-preview-hint {
  color: var(--el-text-color-secondary);
  font-size: 13px;
  padding: 8px 12px;
  background: var(--el-fill-color-light);
  border-radius: 4px;
}

.template-selector {
  display: flex;
  align-items: center;
  gap: 20px;
  margin-bottom: 20px;
}

.template-label {
  font-weight: 500;
}

.template-options {
  display: flex;
  gap: 12px;
}

.template-option {
  cursor: pointer;
  text-align: center;
  padding: 8px;
  border: 2px solid transparent;
  border-radius: 6px;
  transition: all 0.2s;
}

.template-option:hover {
  border-color: var(--el-border-color);
}

.template-option.active {
  border-color: var(--el-color-primary);
}

.template-preview {
  width: 60px;
  height: 40px;
  border-radius: 4px;
  margin-bottom: 4px;
}

.template-name {
  font-size: 12px;
}

.chapter-count {
  margin-left: auto;
  color: var(--el-text-color-secondary);
}

.report-preview {
  border: 1px solid var(--el-border-color);
  border-radius: 4px;
  padding: 30px;
  background: #fff;
  max-height: 350px;
  overflow-y: auto;
}

.report-header {
  text-align: center;
  margin-bottom: 20px;
  padding-bottom: 20px;
  border-bottom: 2px solid #eee;
}

.report-title {
  font-size: 24px;
  color: #003366;
  margin-bottom: 10px;
}

.report-subtitle {
  font-size: 16px;
  color: #666;
}

.report-abstract {
  margin-bottom: 20px;
  padding: 15px;
  background: #f9f9f9;
  border-radius: 4px;
}

.report-abstract h3 {
  margin-bottom: 10px;
  color: #333;
}

.report-toc {
  padding: 15px;
  background: #fafafa;
  border-radius: 4px;
}

.report-toc h3 {
  margin-bottom: 10px;
  color: #333;
}

.toc-item {
  padding: 8px 0;
  color: #333;
  border-bottom: 1px dashed #eee;
}

.toc-title {
  font-weight: 500;
}

.toc-type {
  font-size: 12px;
  color: #999;
  margin-left: 4px;
}

.toc-summary {
  font-size: 13px;
  color: #888;
  margin-top: 4px;
  line-height: 1.4;
}
</style>

