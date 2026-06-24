<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { ElMessage } from 'element-plus'
import { Edit, View, Document, FullScreen, Close, Warning } from '@element-plus/icons-vue'
import { getDevNotes, saveDevNotes } from '@/api/config'

// Markdown渲染（简单实现）
function renderMarkdown(text: string): string {
  if (!text) return ''
  let html = text
    // 标题
    .replace(/^### (.+)$/gm, '<h3>$1</h3>')
    .replace(/^## (.+)$/gm, '<h2>$1</h2>')
    .replace(/^# (.+)$/gm, '<h1>$1</h1>')
    // 粗体/斜体
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/\*(.+?)\*/g, '<em>$1</em>')
    // 代码块
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    // 列表
    .replace(/^- (.+)$/gm, '<li>$1</li>')
    .replace(/^(\d+)\. (.+)$/gm, '<li>$2</li>')
    // 分隔线
    .replace(/^---$/gm, '<hr/>')
    // 换行
    .replace(/\n\n/g, '</p><p>')
    .replace(/\n/g, '<br/>')
  return `<p>${html}</p>`
}

// 定义Props
const props = defineProps<{
  modelValue: boolean
}>()

// 定义Emits
const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
}>()

// 响应式状态
const content = ref('')           // 编辑内容
const originalContent = ref('')   // 原始内容，用于取消时恢复
const loading = ref(false)        // 加载状态
const saving = ref(false)         // 保存状态
const isEditMode = ref(false)     // 是否编辑模式
const isFullscreen = ref(false)   // 是否全屏

// 计算属性：是否有修改
const hasChanges = computed(() => content.value !== originalContent.value)

// 监听对话框打开
watch(() => props.modelValue, async (visible) => {
  if (visible) {
    await loadNotes()
    isEditMode.value = false
  }
})

// 加载开发说明
async function loadNotes() {
  loading.value = true
  try {
    const res = await getDevNotes()
    content.value = res.data || getDefaultContent()
    originalContent.value = content.value
  } catch (error: any) {
    ElMessage.error(error.message || '加载开发说明失败')
    content.value = getDefaultContent()
  } finally {
    loading.value = false
  }
}

// 默认内容模板
function getDefaultContent(): string {
  return `# 智能BI可视化系统 - 开发说明

## 系统概述
这是一个基于AI的商业智能可视化分析系统。

## 技术架构
- **前端**: Vue 3 + Element Plus + TypeScript
- **后端**: .NET 8 + EF Core
- **数据库**: PostgreSQL
- **AI服务**: 支持多种LLM和向量嵌入模型

## 开发要点
1. 添加新功能时请更新此说明
2. 重要的配置变更请记录在这里
3. 已知问题和待办事项也可以记录

## 版本历史
- v1.0.0: 初始版本

---
*请根据实际开发情况更新此文档*
`
}

// 保存开发说明
async function handleSave() {
  saving.value = true
  try {
    await saveDevNotes(content.value)
    originalContent.value = content.value
    ElMessage.success('保存成功')
    isEditMode.value = false
  } catch (error: any) {
    ElMessage.error(error.message || '保存失败')
  } finally {
    saving.value = false
  }
}

// 取消编辑
function handleCancel() {
  if (hasChanges.value) {
    content.value = originalContent.value
  }
  isEditMode.value = false
}

// 关闭对话框
function handleClose() {
  if (hasChanges.value && isEditMode.value) {
    ElMessage.warning('请先保存或取消修改')
    return
  }
  emit('update:modelValue', false)
}

// 切换全屏
function toggleFullscreen() {
  isFullscreen.value = !isFullscreen.value
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    title="开发说明"
    :width="isFullscreen ? '100%' : '900px'"
    :fullscreen="isFullscreen"
    :close-on-click-modal="false"
    :before-close="handleClose"
    class="dev-notes-dialog"
  >
    <!-- 工具栏 -->
    <template #header>
      <div class="dialog-header">
        <span class="title">
          <el-icon><Document /></el-icon>
          开发说明
        </span>
        <div class="toolbar">
          <el-button-group>
            <el-button
              :type="!isEditMode ? 'primary' : 'default'"
              :icon="View"
              @click="isEditMode = false"
            >预览</el-button>
            <el-button
              :type="isEditMode ? 'primary' : 'default'"
              :icon="Edit"
              @click="isEditMode = true"
            >编辑</el-button>
          </el-button-group>
          <el-button :icon="isFullscreen ? Close : FullScreen" @click="toggleFullscreen" />
        </div>
      </div>
    </template>

    <!-- 内容区域 -->
    <div v-loading="loading" class="content-wrapper" :class="{ fullscreen: isFullscreen }">
      <!-- 编辑模式 -->
      <el-input
        v-if="isEditMode"
        v-model="content"
        type="textarea"
        :rows="isFullscreen ? 30 : 20"
        placeholder="请输入开发说明（支持Markdown格式）..."
        class="editor"
      />
      <!-- 预览模式 -->
      <div v-else class="preview markdown-body" v-html="renderMarkdown(content)" />
    </div>

    <!-- 底部按钮 -->
    <template #footer>
      <div class="footer">
        <span v-if="hasChanges && isEditMode" class="unsaved-hint">
          <el-icon color="#E6A23C"><Warning /></el-icon>
          有未保存的修改
        </span>
        <div class="actions">
          <el-button v-if="isEditMode" @click="handleCancel">取消</el-button>
          <el-button v-if="isEditMode" type="primary" :loading="saving" @click="handleSave">
            保存
          </el-button>
          <el-button v-else @click="handleClose">关闭</el-button>
        </div>
      </div>
    </template>
  </el-dialog>
</template>

<style scoped lang="scss">
.dev-notes-dialog {
  :deep(.el-dialog__header) {
    padding: 16px 20px;
    border-bottom: 1px solid #e4e7ed;
    margin-right: 0;
  }
}

.dialog-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  padding-right: 30px;
  
  .title {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 16px;
    font-weight: 600;
  }
  
  .toolbar {
    display: flex;
    gap: 12px;
  }
}

.content-wrapper {
  min-height: 400px;
  max-height: 60vh;
  overflow-y: auto;
  
  &.fullscreen {
    min-height: calc(100vh - 200px);
    max-height: calc(100vh - 200px);
  }
}

.editor {
  :deep(.el-textarea__inner) {
    font-family: 'Consolas', 'Monaco', monospace;
    font-size: 14px;
    line-height: 1.6;
  }
}

.preview {
  padding: 16px;
  background: #fafafa;
  border-radius: 4px;
  min-height: 400px;
  
  :deep(h1) { font-size: 24px; margin: 16px 0 12px; border-bottom: 1px solid #eee; padding-bottom: 8px; }
  :deep(h2) { font-size: 20px; margin: 14px 0 10px; }
  :deep(h3) { font-size: 16px; margin: 12px 0 8px; }
  :deep(code) { background: #f0f0f0; padding: 2px 6px; border-radius: 3px; font-size: 13px; }
  :deep(li) { margin: 4px 0; margin-left: 20px; }
  :deep(hr) { border: none; border-top: 1px solid #ddd; margin: 16px 0; }
  :deep(strong) { font-weight: 600; }
  :deep(em) { font-style: italic; }
}

.footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  
  .unsaved-hint {
    display: flex;
    align-items: center;
    gap: 4px;
    color: #E6A23C;
    font-size: 13px;
  }
  
  .actions {
    display: flex;
    gap: 8px;
  }
}
</style>

