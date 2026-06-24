<template>
  <div class="report-design" v-loading="loading">
    <!-- 顶部工具栏 -->
    <div class="toolbar">
      <div class="left">
        <el-button @click="$router.push('/report')">
          <el-icon><ArrowLeft /></el-icon>返回
        </el-button>
        <el-input v-model="report.name" style="width: 200px; margin-left: 16px;" placeholder="报表名称" />
      </div>
      <div class="center">
        <el-button-group>
          <el-button :type="currentPageIndex === i ? 'primary' : 'default'" 
                     v-for="(page, i) in report.pages" :key="page.id || i"
                     @click="currentPageIndex = i">
            {{ page.title || `第${i + 1}页` }}
          </el-button>
        </el-button-group>
        <el-button @click="addPage" style="margin-left: 8px;">
          <el-icon><Plus /></el-icon>
        </el-button>
      </div>
      <div class="right">
        <el-button type="primary" @click="handleSave" :loading="saving">保存</el-button>
      </div>
    </div>

    <!-- 主编辑区 -->
    <div class="main-area">
      <!-- 左侧素材库 -->
      <div class="sidebar">
        <h4>添加元素</h4>
        <div class="element-list">
          <div class="element-item" @click="showChartPicker = true">
            <el-icon><PieChart /></el-icon>
            <span>图表</span>
          </div>
          <div class="element-item" @click="addTextElement">
            <el-icon><Document /></el-icon>
            <span>文本</span>
          </div>
          <div class="element-item" @click="addImageElement">
            <el-icon><Picture /></el-icon>
            <span>图片</span>
          </div>
        </div>
      </div>

      <!-- 中间画布 -->
      <div class="canvas-area">
        <div class="canvas" ref="canvasRef">
          <div v-for="(item, idx) in currentPageItems" :key="item.id || idx"
               class="canvas-item" 
               :class="{ selected: selectedItemIndex === idx }"
               :style="getItemStyle(item)"
               @click="selectedItemIndex = idx">
            <div v-if="item.itemType === 'chart'" class="chart-placeholder">
              <el-icon><PieChart /></el-icon>
              <span>{{ item.chartName || `图表#${item.chartId}` }}</span>
            </div>
            <div v-else-if="item.itemType === 'text'" class="text-content" v-html="item.textContent"></div>
            <div v-else-if="item.itemType === 'image'" class="image-content">
              <img :src="item.imageUrl" v-if="item.imageUrl" />
              <span v-else>图片</span>
            </div>
            <div class="item-actions">
              <el-button size="small" type="danger" @click.stop="removeItem(idx)">删除</el-button>
            </div>
          </div>
        </div>
      </div>

      <!-- 右侧属性面板 -->
      <div class="property-panel">
        <template v-if="selectedItem">
          <h4>元素属性</h4>
          <el-form label-width="60px" size="small">
            <el-form-item label="宽度">
              <el-input-number v-model="selectedLayout.width" :min="50" :max="1200" />
            </el-form-item>
            <el-form-item label="高度">
              <el-input-number v-model="selectedLayout.height" :min="50" :max="800" />
            </el-form-item>
            <el-form-item label="X">
              <el-input-number v-model="selectedLayout.x" :min="0" />
            </el-form-item>
            <el-form-item label="Y">
              <el-input-number v-model="selectedLayout.y" :min="0" />
            </el-form-item>
            <template v-if="selectedItem.itemType === 'text'">
              <el-form-item label="内容">
                <el-input v-model="selectedItem.textContent" type="textarea" :rows="4" />
              </el-form-item>
            </template>
            <template v-if="selectedItem.itemType === 'image'">
              <el-form-item label="URL">
                <el-input v-model="selectedItem.imageUrl" placeholder="图片地址" />
              </el-form-item>
            </template>
          </el-form>
        </template>
        <div v-else class="no-selection">
          <p>点击画布元素进行编辑</p>
        </div>
      </div>
    </div>

    <!-- 图表选择器弹窗 -->
    <el-dialog v-model="showChartPicker" title="选择图表" width="600px">
      <el-table :data="chartList" @row-click="handleSelectChart" style="cursor: pointer;">
        <el-table-column prop="id" label="ID" width="80" />
        <el-table-column prop="name" label="图表名称" />
        <el-table-column prop="chartType" label="类型" width="100" />
      </el-table>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft, Plus, PieChart, Document, Picture } from '@element-plus/icons-vue'
import { 
  getReportDetail, 
  updateReport, 
  saveReportPage, 
  saveReportItem,
  deleteReportItem,
  type ReportDetail, 
  type ReportPage, 
  type ReportItem 
} from '@/api/report'
import { getChartList, type Chart } from '@/api/chart'

const route = useRoute()
const reportId = Number(route.params.id)

const loading = ref(false)
const saving = ref(false)
const report = ref<ReportDetail>({
  id: 0, name: '', reportType: 'report', configJson: '{}', 
  isPublished: false, createdAt: '', pages: []
})
const currentPageIndex = ref(0)
const selectedItemIndex = ref(-1)
const showChartPicker = ref(false)
const chartList = ref<Chart[]>([])

const currentPage = computed(() => report.value.pages[currentPageIndex.value])
const currentPageItems = computed(() => currentPage.value?.items || [])
const selectedItem = computed(() => currentPageItems.value[selectedItemIndex.value])
const selectedLayout = computed(() => {
  if (!selectedItem.value) return { x: 0, y: 0, width: 300, height: 200 }
  try {
    return JSON.parse(selectedItem.value.layoutJson || '{}')
  } catch { return { x: 0, y: 0, width: 300, height: 200 } }
})

// 同步布局变化
watch(selectedLayout, (val) => {
  if (selectedItem.value) {
    selectedItem.value.layoutJson = JSON.stringify(val)
  }
}, { deep: true })

onMounted(async () => {
  await loadReport()
  await loadCharts()
})

async function loadReport() {
  loading.value = true
  try {
    const res = await getReportDetail(reportId)
    if (res.data) {
      report.value = res.data
      if (res.data.pages.length === 0) {
        report.value.pages = [{ id: 0, title: '第1页', sortOrder: 1, configJson: '{}', items: [] }]
      }
    }
  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

async function loadCharts() {
  try {
    const res = await getChartList()
    chartList.value = res.data || []
  } catch (e) {
    console.error(e)
  }
}

function addPage() {
  const newPage: ReportPage = {
    id: 0,
    title: `第${report.value.pages.length + 1}页`,
    sortOrder: report.value.pages.length + 1,
    configJson: '{}',
    items: []
  }
  report.value.pages.push(newPage)
  currentPageIndex.value = report.value.pages.length - 1
}

function addTextElement() {
  const newItem: ReportItem = {
    id: 0, itemType: 'text', sortOrder: currentPageItems.value.length,
    textContent: '<p>请输入文本内容</p>',
    layoutJson: JSON.stringify({ x: 50, y: 50, width: 300, height: 100 }),
    styleJson: '{}'
  }
  currentPage.value.items.push(newItem)
  selectedItemIndex.value = currentPage.value.items.length - 1
}

function addImageElement() {
  const newItem: ReportItem = {
    id: 0, itemType: 'image', sortOrder: currentPageItems.value.length,
    imageUrl: '',
    layoutJson: JSON.stringify({ x: 50, y: 50, width: 300, height: 200 }),
    styleJson: '{}'
  }
  currentPage.value.items.push(newItem)
  selectedItemIndex.value = currentPage.value.items.length - 1
}

function handleSelectChart(row: Chart) {
  const newItem: ReportItem = {
    id: 0, itemType: 'chart', chartId: row.id, chartName: row.name,
    sortOrder: currentPageItems.value.length,
    layoutJson: JSON.stringify({ x: 50, y: 50, width: 400, height: 300 }),
    styleJson: '{}'
  }
  currentPage.value.items.push(newItem)
  selectedItemIndex.value = currentPage.value.items.length - 1
  showChartPicker.value = false
  ElMessage.success(`已添加图表: ${row.name}`)
}

async function removeItem(idx: number) {
  const item = currentPageItems.value[idx]
  if (item.id) {
    await deleteReportItem(item.id)
  }
  currentPage.value.items.splice(idx, 1)
  selectedItemIndex.value = -1
}

function getItemStyle(item: ReportItem) {
  try {
    const layout = JSON.parse(item.layoutJson || '{}')
    return {
      left: `${layout.x || 0}px`,
      top: `${layout.y || 0}px`,
      width: `${layout.width || 300}px`,
      height: `${layout.height || 200}px`
    }
  } catch {
    return { left: '0px', top: '0px', width: '300px', height: '200px' }
  }
}

async function handleSave() {
  saving.value = true
  try {
    // 保存报表基本信息
    await updateReport(reportId, {
      name: report.value.name,
      reportType: report.value.reportType,
      configJson: report.value.configJson,
      remark: report.value.remark
    })
    // 保存各页面和元素
    for (const page of report.value.pages) {
      const pageRes = await saveReportPage(reportId, {
        id: page.id || undefined,
        title: page.title,
        sortOrder: page.sortOrder,
        configJson: page.configJson
      })
      page.id = pageRes.data.id
      for (const item of page.items) {
        const itemRes = await saveReportItem(page.id, {
          id: item.id || undefined,
          itemType: item.itemType,
          chartId: item.chartId,
          panelId: item.panelId,
          textContent: item.textContent,
          imageUrl: item.imageUrl,
          layoutJson: item.layoutJson,
          styleJson: item.styleJson,
          sortOrder: item.sortOrder
        })
        item.id = itemRes.data.id
      }
    }
    ElMessage.success('保存成功')
  } catch (e) {
    console.error(e)
    ElMessage.error('保存失败')
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.report-design { height: 100vh; display: flex; flex-direction: column; background: #f5f5f5; }
.toolbar { display: flex; justify-content: space-between; align-items: center; padding: 12px 20px; background: #fff; border-bottom: 1px solid #e0e0e0; }
.toolbar .left, .toolbar .right { display: flex; align-items: center; }
.main-area { flex: 1; display: flex; overflow: hidden; }
.sidebar { width: 180px; background: #fff; border-right: 1px solid #e0e0e0; padding: 16px; }
.sidebar h4 { margin: 0 0 16px 0; font-size: 14px; color: #333; }
.element-list { display: flex; flex-direction: column; gap: 8px; }
.element-item { display: flex; align-items: center; gap: 8px; padding: 10px; border: 1px solid #ddd; border-radius: 4px; cursor: pointer; transition: all 0.2s; }
.element-item:hover { border-color: #409eff; background: #ecf5ff; }
.canvas-area { flex: 1; padding: 20px; overflow: auto; display: flex; justify-content: center; }
.canvas { position: relative; width: 960px; min-height: 600px; background: #fff; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
.canvas-item { position: absolute; border: 2px solid transparent; border-radius: 4px; cursor: move; transition: border-color 0.2s; background: #fafafa; }
.canvas-item:hover { border-color: #409eff; }
.canvas-item.selected { border-color: #409eff; box-shadow: 0 0 0 2px rgba(64,158,255,0.3); }
.chart-placeholder { display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100%; color: #666; }
.chart-placeholder .el-icon { font-size: 48px; margin-bottom: 8px; }
.text-content { padding: 12px; height: 100%; overflow: auto; }
.image-content { height: 100%; display: flex; align-items: center; justify-content: center; }
.image-content img { max-width: 100%; max-height: 100%; object-fit: contain; }
.item-actions { position: absolute; top: -30px; right: 0; display: none; }
.canvas-item:hover .item-actions { display: block; }
.property-panel { width: 260px; background: #fff; border-left: 1px solid #e0e0e0; padding: 16px; overflow-y: auto; }
.property-panel h4 { margin: 0 0 16px 0; font-size: 14px; color: #333; }
.no-selection { color: #999; text-align: center; padding-top: 40px; }
</style>

