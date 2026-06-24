<script setup lang="ts">
import { ref, onMounted, nextTick, onBeforeUnmount, reactive, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { PieChart, Close, Plus, Rank, FullScreen, RefreshRight, Lock, Unlock } from '@element-plus/icons-vue'
import * as echarts from 'echarts'
import { getChartList, queryChart, type Chart } from '@/api/chart'
import { getPanelDetail, createPanel, updatePanel, type PanelItem, type PanelItemUpdate } from '@/api/panel'

// 使用 grid-layout-plus (Vue 3 兼容版)
import { GridLayout, GridItem } from 'grid-layout-plus'

const route = useRoute()
const router = useRouter()
const panelId = ref<number | null>(null)
const saving = ref(false)
const chartList = ref<Chart[]>([])
const chartInstances = ref<Map<string, echarts.ECharts>>(new Map())

const form = ref({
  name: '',
  panelType: 'pc_dashboard',
  remark: ''
})

// 全局筛选器配置
interface GlobalFilter {
  id: string
  label: string
  field: string
  type: 'input' | 'select' | 'date' | 'daterange'
  options?: string[]
  defaultValue?: any
}
const globalFilters = ref<GlobalFilter[]>([])

// 画布上的图表项
interface CanvasItem {
  i: string
  chartId: number
  chartName: string
  chartType: string
  x: number
  y: number
  w: number
  h: number
  static?: boolean
}
const canvasItems = ref<CanvasItem[]>([])

// 画布配置
const canvasConfig = reactive({
  colNum: 12,
  rowHeight: 30,
  margin: [10, 10] as [number, number],
  isDraggable: true,
  isResizable: true,
  useCssTransforms: true,
  verticalCompact: true,
  preventCollision: false
})

// 根据面板类型调整网格配置
const panelTypeConfigs: Record<string, { colNum: number; rowHeight: number }> = {
  pc_dashboard: { colNum: 12, rowHeight: 30 },
  big_screen: { colNum: 24, rowHeight: 20 },
  mobile: { colNum: 4, rowHeight: 40 }
}

watch(() => form.value.panelType, (type) => {
  const config = panelTypeConfigs[type]
  if (config) {
    canvasConfig.colNum = config.colNum
    canvasConfig.rowHeight = config.rowHeight
  }
})



onMounted(async () => {
  await loadCharts()
  if (route.params.id) {
    panelId.value = Number(route.params.id)
    await loadPanel(panelId.value)
  }
})

onBeforeUnmount(() => {
  chartInstances.value.forEach(chart => chart.dispose())
  chartInstances.value.clear()
})

async function loadCharts() {
  try {
    const res = await getChartList()
    if (res.code === 0) {
      chartList.value = res.data || []
    }
  } catch (e) {
    console.error(e)
  }
}

async function loadPanel(id: number) {
  try {
    const res = await getPanelDetail(id)
    if (res.code === 0 && res.data) {
      form.value = {
        name: res.data.name,
        panelType: res.data.panelType,
        remark: res.data.remark || ''
      }
      try {
        const config = JSON.parse(res.data.configJson || '{}')
        globalFilters.value = config.globalFilters || []
      } catch {
        globalFilters.value = []
      }
      canvasItems.value = res.data.items.map((item: PanelItem, index: number) => {
        const layout = JSON.parse(item.layoutJson || '{}')
        return {
          i: `item-${item.chartId}-${index}`,
          chartId: item.chartId || 0,
          chartName: item.chartName || '',
          chartType: item.chartType || 'bar',
          x: layout.x || 0,
          y: layout.y || (index * 4),
          w: layout.w || 6,
          h: layout.h || 8,
          static: false
        }
      })
      await nextTick()
      for (const item of canvasItems.value) {
        await renderItemChart(item.i)
      }
    }
  } catch (e) {
    console.error(e)
  }
}

function addChartToCanvas(chart: Chart) {
  console.log('添加图表:', chart)
  const newItem: CanvasItem = {
    i: `item-${chart.id}-${Date.now()}`,
    chartId: chart.id,
    chartName: chart.name,
    chartType: chart.chartType,
    x: 0,
    y: 1000,
    w: 6,
    h: 8,
    static: false
  }
  // 使用 push 后强制触发更新
  canvasItems.value.push(newItem)
  console.log('当前画布项数量:', canvasItems.value.length)
  console.log('当前画布项:', canvasItems.value)
  
  nextTick(() => {
    console.log('nextTick 渲染图表:', newItem.i)
    renderItemChart(newItem.i)
  })
}

function removeItem(itemKey: string) {
  const index = canvasItems.value.findIndex(i => i.i === itemKey)
  if (index > -1) {
    const instance = chartInstances.value.get(itemKey)
    if (instance) {
      instance.dispose()
      chartInstances.value.delete(itemKey)
    }
    canvasItems.value.splice(index, 1)
  }
}

function toggleItemLock(itemKey: string) {
  const item = canvasItems.value.find(i => i.i === itemKey)
  if (item) {
    item.static = !item.static
    ElMessage.success(item.static ? '已锁定' : '已解锁')
  }
}

async function renderItemChart(itemKey: string) {
  const item = canvasItems.value.find(i => i.i === itemKey)
  if (!item?.chartId) return
  
  const el = document.getElementById(`chart-${itemKey}`)
  if (!el) {
    console.warn('图表元素未找到:', `chart-${itemKey}`)
    return
  }

  const oldInstance = chartInstances.value.get(itemKey)
  if (oldInstance) {
    oldInstance.dispose()
  }

  try {
    const res = await queryChart(item.chartId, { skipCache: true })
    if (res.code === 0 && res.data) {
      const chart = echarts.init(el)
      const option: echarts.EChartsOption = {
        tooltip: { trigger: item.chartType === 'pie' ? 'item' : 'axis' },
        legend: { bottom: 0, type: 'scroll' },
        grid: { left: '3%', right: '4%', bottom: '15%', top: '10%', containLabel: true },
        xAxis: item.chartType === 'pie' ? undefined : { type: 'category', data: res.data.categories },
        yAxis: item.chartType === 'pie' ? undefined : { type: 'value' },
        series: res.data.series.map((s: any) => ({
          name: s.name,
          type: item.chartType as 'bar' | 'line' | 'pie',
          data: item.chartType === 'pie'
            ? res.data!.categories.map((c: string, i: number) => ({ name: c, value: s.data[i] }))
            : s.data
        }))
      }
      chart.setOption(option)
      // 新增图表时容器尺寸可能尚未稳定，下一帧强制自适应一次
      requestAnimationFrame(() => chart.resize())
      chartInstances.value.set(itemKey, chart)
    }
  } catch (e) {
    console.error(e)
  }
}

function onLayoutUpdated() {
  nextTick(() => {
    chartInstances.value.forEach((chart) => {
      chart.resize()
    })
  })
}

function refreshAllCharts() {
  chartInstances.value.forEach((chart) => {
    chart.resize()
  })
}

function addGlobalFilter() {
  globalFilters.value.push({
    id: `gf_${Date.now()}`,
    label: '新筛选器',
    field: '',
    type: 'input',
    options: [],
    defaultValue: ''
  })
}

function removeGlobalFilter(index: number) {
  globalFilters.value.splice(index, 1)
}

async function handleSave() {
  if (!form.value.name) {
    ElMessage.warning('请填写面板名称')
    return
  }
  saving.value = true
  try {
    const items: PanelItemUpdate[] = canvasItems.value.map((item, i) => ({
      chartId: item.chartId,
      layoutJson: JSON.stringify({ x: item.x, y: item.y, w: item.w, h: item.h }),
      sortOrder: i
    }))
    const configJson = JSON.stringify({
      globalFilters: globalFilters.value.filter(f => f.field)
    })
    const payload = {
      name: form.value.name,
      panelType: form.value.panelType,
      configJson,
      remark: form.value.remark,
      items
    }
    if (panelId.value) {
      const res = await updatePanel(panelId.value, payload)
      if (res.code === 0) {
        ElMessage.success('保存成功')
      } else {
        ElMessage.error(res.message || '保存失败')
      }
    } else {
      const res = await createPanel({ name: form.value.name, panelType: form.value.panelType, remark: form.value.remark })
      if (res.code === 0) {
        panelId.value = res.data as number
        await updatePanel(panelId.value, payload)
        ElMessage.success('创建成功')
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

function goBack() {
  router.push({ name: 'Panel' })
}
</script>

<template>
  <div class="panel-design">
    <el-page-header @back="goBack">
      <template #content>
        <span>{{ panelId ? '编辑面板' : '新建面板' }}</span>
      </template>
      <template #extra>
        <el-space>
          <el-tag type="info" effect="plain">
            <el-icon><Rank /></el-icon>
            拖拽移动 / 拖拽右下角调整大小
          </el-tag>
          <el-button type="primary" :loading="saving" @click="handleSave">
            保存
          </el-button>
        </el-space>
      </template>
    </el-page-header>

    <div class="design-container">
      <!-- 左栏：图表库 -->
      <div class="left-panel">
        <el-card>
          <template #header>图表库 ({{ chartList.length }})</template>
          <div class="chart-list">
            <div v-if="!chartList.length" class="empty-tip">暂无可用图表</div>
            <div
              v-for="chart in chartList"
              :key="chart.id"
              class="chart-item"
              @click="addChartToCanvas(chart)"
            >
              <el-icon><PieChart /></el-icon>
              <span class="chart-name">{{ chart.name }}</span>
              <el-icon class="add-icon"><Plus /></el-icon>
            </div>
          </div>
        </el-card>
      </div>

      <!-- 中间：画布 -->
      <div class="center-panel">
        <el-card class="canvas-card">
          <!-- 工具栏 -->
          <div class="canvas-toolbar">
            <el-space>
              <el-switch
                v-model="canvasConfig.isDraggable"
                active-text="可拖拽"
                inline-prompt
              />
              <el-switch
                v-model="canvasConfig.isResizable"
                active-text="可调整"
                inline-prompt
              />
              <el-divider direction="vertical" />
              <el-text type="info">网格: {{ canvasConfig.colNum }}列</el-text>
              <el-button
                size="small"
                :icon="RefreshRight"
                circle
                title="刷新图表"
                @click="refreshAllCharts"
              />
            </el-space>
          </div>

          <!-- 画布区域 -->
          <div class="canvas-area">
            <div v-if="!canvasItems.length" class="empty-canvas">
              <el-empty description="点击左侧图表添加到画布" />
            </div>
            <GridLayout
              v-else
              v-model:layout="canvasItems"
              :col-num="canvasConfig.colNum"
              :row-height="canvasConfig.rowHeight"
              :margin="canvasConfig.margin"
              :is-draggable="canvasConfig.isDraggable"
              :is-resizable="canvasConfig.isResizable"
              :use-css-transforms="canvasConfig.useCssTransforms"
              :vertical-compact="canvasConfig.verticalCompact"
              @layout-updated="onLayoutUpdated"
            >
              <GridItem
                v-for="item in canvasItems"
                :key="item.i"
                :x="item.x"
                :y="item.y"
                :w="item.w"
                :h="item.h"
                :i="item.i"
                :static="item.static"
                drag-allow-from=".item-header"
                drag-ignore-from=".no-drag"
              >
                  <div class="grid-item-content" :class="{ locked: item.static }">
                    <div class="item-header">
                      <div class="header-left">
                        <el-icon class="drag-handle"><Rank /></el-icon>
                        <span class="item-title">{{ item.chartName }}</span>
                      </div>
                      <div class="item-actions no-drag">
                        <el-tooltip content="锁定/解锁">
                          <el-button
                            link
                            size="small"
                            :type="item.static ? 'warning' : 'info'"
                            @click="toggleItemLock(item.i)"
                          >
                            <el-icon>
                              <Lock v-if="item.static" />
                              <Unlock v-else />
                            </el-icon>
                          </el-button>
                        </el-tooltip>
                        <el-tooltip :content="`${item.w}列 × ${item.h}行`">
                          <el-icon class="size-icon"><FullScreen /></el-icon>
                        </el-tooltip>
                        <el-button
                          link
                          type="danger"
                          size="small"
                          @click="removeItem(item.i)"
                        >
                          <el-icon><Close /></el-icon>
                        </el-button>
                      </div>
                    </div>
                    <div :id="`chart-${item.i}`" class="item-chart no-drag"></div>
                  </div>
              </GridItem>
            </GridLayout>
          </div>
        </el-card>
      </div>

      <!-- 右栏：配置 -->
      <div class="right-panel">
        <el-card>
          <template #header>面板配置</template>
          <el-form label-position="top" size="small">
            <el-form-item label="面板名称" required>
              <el-input v-model="form.name" placeholder="输入面板名称" />
            </el-form-item>
            <el-form-item label="面板类型">
              <el-select v-model="form.panelType" style="width: 100%">
                <el-option label="PC仪表盘" value="pc_dashboard" />
                <el-option label="大屏" value="big_screen" />
                <el-option label="移动端" value="mobile" />
              </el-select>
            </el-form-item>
            <el-form-item label="网格列数">
              <el-slider v-model="canvasConfig.colNum" :min="4" :max="24" :step="2" show-stops />
            </el-form-item>
            <el-form-item label="行高(px)">
              <el-slider v-model="canvasConfig.rowHeight" :min="20" :max="60" />
            </el-form-item>
            <el-form-item label="备注">
              <el-input v-model="form.remark" type="textarea" :rows="2" placeholder="备注说明" />
            </el-form-item>
          </el-form>

          <el-divider content-position="left">全局筛选器</el-divider>
          <div class="global-filters">
            <div v-for="(filter, idx) in globalFilters" :key="filter.id" class="filter-config">
              <div class="filter-row">
                <el-input v-model="filter.label" placeholder="显示标签" size="small" style="width: 100%" />
                <el-icon class="remove-btn" @click="removeGlobalFilter(idx)"><Close /></el-icon>
              </div>
              <el-input v-model="filter.field" placeholder="字段名" size="small" style="margin-top: 4px" />
              <el-select v-model="filter.type" size="small" style="width: 100%; margin-top: 4px">
                <el-option label="输入框" value="input" />
                <el-option label="下拉选择" value="select" />
                <el-option label="日期" value="date" />
                <el-option label="日期范围" value="daterange" />
              </el-select>
              <el-input
                v-if="filter.type === 'select'"
                :model-value="Array.isArray(filter.options) ? filter.options.join(',') : (filter.options || '')"
                @update:model-value="(v: string) => filter.options = v.split(',')"
                placeholder="选项，逗号分隔"
                size="small"
                style="margin-top: 4px"
              />
            </div>
            <el-button type="primary" link size="small" :icon="Plus" @click="addGlobalFilter">添加筛选器</el-button>
          </div>
        </el-card>
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
.panel-design {
  height: 100vh;
  display: flex;
  flex-direction: column;
  padding: 16px;
  box-sizing: border-box;
  background: #f0f2f5;

  .design-container {
    flex: 1;
    display: flex;
    gap: 16px;
    margin-top: 16px;
    overflow: hidden;

    .left-panel {
      width: 260px;
      flex-shrink: 0;
      overflow-y: auto;
      
      .chart-list {
        max-height: calc(100vh - 200px);
        overflow-y: auto;
        
        .chart-item {
          display: flex;
          align-items: center;
          gap: 8px;
          padding: 12px;
          margin-bottom: 8px;
          background: #f5f7fa;
          border-radius: 6px;
          cursor: pointer;
          transition: all 0.2s;
          border: 1px solid transparent;
          
          &:hover {
            background: #e6f2ff;
            border-color: #409eff;
            transform: translateX(4px);
          }
          
          .chart-name {
            flex: 1;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
          }
          
          .add-icon {
            color: #409eff;
            opacity: 0;
            transition: opacity 0.2s;
          }
          
          &:hover .add-icon {
            opacity: 1;
          }
        }
      }
      
      .empty-tip {
        color: #909399;
        text-align: center;
        padding: 40px 0;
      }
    }

    .center-panel {
      flex: 1;
      min-width: 0;
      
      .canvas-card {
        height: 100%;
        display: flex;
        flex-direction: column;
        
        :deep(.el-card__body) {
          flex: 1;
          display: flex;
          flex-direction: column;
          padding: 0;
          overflow: hidden;
        }
      }
      
      .canvas-toolbar {
        padding: 12px 16px;
        border-bottom: 1px solid #e4e7ed;
        background: #fff;
        flex-shrink: 0;
      }
      
      .canvas-area {
        flex: 1;
        background: #f5f7fa;
        overflow: auto;
        position: relative;
        padding: 16px;
        
        .empty-canvas,
        .loading-layout,
        .error-layout {
          position: absolute;
          top: 50%;
          left: 50%;
          transform: translate(-50%, -50%);
        }
      }
    }

    .right-panel {
      width: 300px;
      flex-shrink: 0;
      overflow-y: auto;
    }
  }
}

// grid-layout-plus 样式
:deep(.vgl-layout) {
  min-height: 100%;
}

:deep(.vgl-item) {
  touch-action: none;
  
  .grid-item-content {
    height: 100%;
    background: #fff;
    border-radius: 8px;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
    display: flex;
    flex-direction: column;
    overflow: hidden;
    transition: box-shadow 0.2s;
    position: relative;
    
    &:hover {
      box-shadow: 0 4px 16px rgba(0, 0, 0, 0.12);
    }
    
    &.locked {
      .item-header {
        background: #fdf6ec;
      }
    }
    
    .item-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 10px 12px;
      border-bottom: 1px solid #e4e7ed;
      background: #fafafa;
      cursor: move;
      user-select: none;
      
      .header-left {
        display: flex;
        align-items: center;
        gap: 8px;
        flex: 1;
        min-width: 0;
        
        .drag-handle {
          color: #909399;
          cursor: move;
        }
        
        .item-title {
          font-weight: 500;
          font-size: 14px;
          overflow: hidden;
          text-overflow: ellipsis;
          white-space: nowrap;
        }
      }
      
      .item-actions {
        display: flex;
        align-items: center;
        gap: 4px;
        
        .size-icon {
          color: #909399;
          font-size: 14px;
          margin: 0 4px;
        }
      }
    }
    
    .item-chart {
      flex: 1;
      min-height: 0;
      padding: 8px;
    }
  }
  
  .vgl-item__resizer {
    position: absolute;
    width: 20px;
    height: 20px;
    bottom: 0;
    right: 0;
    cursor: se-resize;
    background: linear-gradient(135deg, transparent 50%, #409eff 50%);
    border-radius: 0 0 8px 0;
    
    &:hover {
      background: linear-gradient(135deg, transparent 50%, #66b1ff 50%);
    }
  }
  
  &.vgl-item--dragging .grid-item-content {
    opacity: 0.9;
    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.2);
    z-index: 100;
  }
  
  &.vgl-item--resizing .grid-item-content {
    opacity: 0.9;
    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.2);
  }
}

.global-filters {
  margin-top: 8px;
  
  .filter-config {
    background: #f5f7fa;
    border-radius: 6px;
    padding: 10px;
    margin-bottom: 10px;
    border: 1px solid #e4e7ed;
    
    .filter-row {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    
    .remove-btn {
      cursor: pointer;
      color: #909399;
      flex-shrink: 0;
      
      &:hover {
        color: #f56c6c;
      }
    }
  }
}
</style>
