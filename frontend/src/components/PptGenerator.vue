<template>
  <el-dialog
    v-model="visible"
    title="生成PPT报告"
    width="900px"
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
        <el-form-item label="PPT标题" required>
          <el-input v-model="pptTitle" placeholder="请输入PPT标题" />
        </el-form-item>
        <el-form-item label="目标受众">
          <el-input v-model="audience" placeholder="例如：医院管理层、科室负责人" />
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
        <div class="slides-list">
          <div class="slides-header">
            <span>幻灯片列表</span>
            <el-button link type="primary" @click="addSlide">
              <el-icon><Plus /></el-icon>
            </el-button>
          </div>
          <draggable
            v-model="outline.slides"
            item-key="title"
            handle=".drag-handle"
            animation="200"
            ghost-class="slide-ghost"
          >
            <template #item="{ element: slide, index }">
              <div
                class="slide-item"
                :class="{ active: selectedSlideIndex === index }"
                @click="selectSlide(index)"
              >
                <div class="slide-header">
                  <el-icon class="drag-handle"><Rank /></el-icon>
                  <span class="slide-order">{{ index + 1 }}</span>
                  <el-tag :type="getSlideTypeTag(slide.type)" size="small">{{ getSlideTypeLabel(slide.type) }}</el-tag>
                  <el-button link type="danger" class="delete-btn" @click.stop="removeSlide(index)" :disabled="outline.slides.length <= 1">
                    <el-icon><Delete /></el-icon>
                  </el-button>
                </div>
                <div class="slide-title">{{ slide.title || '(未命名)' }}</div>
              </div>
            </template>
          </draggable>
        </div>
        <div class="slide-editor" v-if="selectedSlide">
          <el-form label-width="80px">
            <el-form-item label="类型">
              <el-select v-model="selectedSlide.type" style="width: 100%" @change="handleSlideTypeChange">
                <el-option label="封面页" value="title" />
                <el-option label="内容页" value="content" />
                <el-option label="图表页" value="chart" />
                <el-option label="指标页" value="kpi" />
                <el-option label="总结页" value="summary" />
              </el-select>
            </el-form-item>
            <el-form-item label="布局">
              <el-select v-model="selectedSlide.layout" style="width: 100%" placeholder="选择布局样式">
                <el-option
                  v-for="layout in getAvailableLayouts(selectedSlide.type)"
                  :key="layout.id"
                  :label="layout.name"
                  :value="layout.id"
                />
              </el-select>
            </el-form-item>
            <el-form-item label="标题">
              <el-input v-model="selectedSlide.title" placeholder="输入幻灯片标题" />
            </el-form-item>
            <el-form-item label="要点">
              <draggable
                v-model="selectedSlide.points"
                item-key="index"
                handle=".point-drag-handle"
                animation="150"
              >
                <template #item="{ index: i }">
                  <div class="point-row">
                    <el-icon class="point-drag-handle"><Rank /></el-icon>
                    <el-input v-model="selectedSlide.points[i]" placeholder="输入要点内容" />
                    <el-button link type="danger" @click="removePoint(i)" :disabled="selectedSlide.points.length <= 1">
                      <el-icon><Delete /></el-icon>
                    </el-button>
                  </div>
                </template>
              </draggable>
              <el-button link type="primary" @click="addPoint" style="margin-top: 8px;">
                <el-icon><Plus /></el-icon> 添加要点
              </el-button>
            </el-form-item>
            <el-form-item label="备注">
              <el-input v-model="selectedSlide.notes" type="textarea" :rows="3" placeholder="演讲备注（可选）" />
            </el-form-item>
          </el-form>
          <!-- 单页AI编辑区域 -->
          <div class="slide-ai-edit">
            <div class="ai-edit-header">
              <el-icon><Promotion /></el-icon>
              <span>AI智能编辑</span>
            </div>
            <div class="ai-quick-actions">
              <el-button size="small" @click="quickOptimizeSlide('simplify')" :loading="slideOptimizing">
                <el-icon><Minus /></el-icon> 精简
              </el-button>
              <el-button size="small" @click="quickOptimizeSlide('expand')" :loading="slideOptimizing">
                <el-icon><Plus /></el-icon> 扩展
              </el-button>
              <el-button size="small" @click="quickOptimizeSlide('reorder')" :loading="slideOptimizing">
                <el-icon><Sort /></el-icon> 调序
              </el-button>
              <el-button size="small" @click="quickOptimizeSlide('changeLayout')" :loading="slideOptimizing">
                <el-icon><Grid /></el-icon> 换布局
              </el-button>
              <el-button size="small" @click="quickOptimizeSlide('addNotes')" :loading="slideOptimizing">
                <el-icon><Document /></el-icon> 加备注
              </el-button>
            </div>
            <div class="ai-custom-edit">
              <el-input
                v-model="slideOptimizePrompt"
                placeholder="输入自定义修改要求..."
                size="small"
                clearable
              >
                <template #append>
                  <el-button
                    :loading="slideOptimizing"
                    :disabled="!slideOptimizePrompt.trim()"
                    @click="customOptimizeSlide"
                  >
                    <el-icon><Promotion /></el-icon>
                  </el-button>
                </template>
              </el-input>
            </div>
          </div>
        </div>
        <el-empty v-else description="请选择一个幻灯片进行编辑" :image-size="60" />
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
      <!-- 提示词输入区域 -->
      <div class="prompt-section">
        <div class="prompt-header">
          <el-icon><Edit /></el-icon>
          <span>AI优化提示词（可选）</span>
        </div>
        <el-input
          v-model="optimizePrompt"
          type="textarea"
          :rows="2"
          placeholder="输入额外的指导，如：请突出数据对比、增加图表说明、调整内容顺序等..."
        />
        <el-button
          type="primary"
          size="small"
          :loading="optimizing"
          :disabled="!optimizePrompt.trim()"
          @click="optimizeOutline"
          style="margin-top: 8px;"
        >
          <el-icon><Promotion /></el-icon> AI优化大纲
        </el-button>
      </div>
    </div>

    <!-- 步骤3：预览导出 -->
    <div v-else-if="currentStep === 2" class="step-content step-preview">
      <div class="preview-header">
        <el-form :inline="true">
          <el-form-item label="选择模板">
            <el-radio-group v-model="selectedTemplate" size="small">
              <el-radio-button label="business">
                <div class="template-option">
                  <div class="template-preview business"></div>
                  <span>商务蓝</span>
                </div>
              </el-radio-button>
              <el-radio-button label="medical">
                <div class="template-option">
                  <div class="template-preview medical"></div>
                  <span>医疗绿</span>
                </div>
              </el-radio-button>
              <el-radio-button label="simple">
                <div class="template-option">
                  <div class="template-preview simple"></div>
                  <span>简约白</span>
                </div>
              </el-radio-button>
              <el-radio-button label="tech">
                <div class="template-option">
                  <div class="template-preview tech"></div>
                  <span>科技紫</span>
                </div>
              </el-radio-button>
              <el-radio-button label="warm">
                <div class="template-option">
                  <div class="template-preview warm"></div>
                  <span>暖橙</span>
                </div>
              </el-radio-button>
              <el-radio-button label="dark">
                <div class="template-option">
                  <div class="template-preview dark"></div>
                  <span>深灰</span>
                </div>
              </el-radio-button>
            </el-radio-group>
          </el-form-item>
        </el-form>
        <div class="preview-info">
          <el-text type="info">共 {{ outline.slides.length }} 张幻灯片</el-text>
        </div>
      </div>
      <div class="preview-container">
        <!-- 左侧缩略图列表 -->
        <div class="preview-thumbnails">
          <div
            v-for="(slide, index) in outline.slides"
            :key="index"
            class="thumbnail-item"
            :class="{ active: previewSlideIndex === index }"
            @click="previewSlideIndex = index"
          >
            <div class="thumbnail-number">{{ index + 1 }}</div>
            <div class="thumbnail-preview" :class="selectedTemplate">
              <div class="thumbnail-title">{{ slide.title }}</div>
            </div>
          </div>
        </div>
        <!-- 右侧大预览 -->
        <div class="preview-main">
          <div class="preview-slide-large" :class="[selectedTemplate, 'layout-' + (previewingSlide.layout || '')]" v-if="previewingSlide">
            <!-- 封面页样式 - 根据layout显示不同布局 -->
            <template v-if="previewingSlide.type === 'title'">
              <!-- 左对齐封面布局 -->
              <div v-if="previewingSlide.layout === 'left-title'" class="slide-cover layout-left">
                <h1 class="slide-main-title">{{ pptTitle }}</h1>
                <h2 class="slide-sub-title">{{ previewingSlide.title }}</h2>
                <div class="slide-meta" v-if="audience">受众：{{ audience }}</div>
              </div>
              <!-- 默认居中封面布局 -->
              <div v-else class="slide-cover layout-centered">
                <h1 class="slide-main-title">{{ pptTitle }}</h1>
                <h2 class="slide-sub-title">{{ previewingSlide.title }}</h2>
                <div class="slide-meta" v-if="audience">受众：{{ audience }}</div>
              </div>
            </template>
            <!-- 总结页样式 - 根据layout显示不同布局 -->
            <template v-else-if="previewingSlide.type === 'summary'">
              <!-- 居中总结布局 -->
              <div v-if="previewingSlide.layout === 'summary-centered'" class="slide-summary layout-centered">
                <h2 class="slide-title">{{ previewingSlide.title }}</h2>
                <div class="summary-content-centered">
                  <p v-for="(point, i) in previewingSlide.points" :key="i">{{ point }}</p>
                </div>
              </div>
              <!-- 默认总结要点布局 -->
              <div v-else class="slide-summary">
                <h2 class="slide-title">{{ previewingSlide.title }}</h2>
                <div class="summary-points">
                  <div class="summary-point" v-for="(point, i) in previewingSlide.points" :key="i">
                    <span class="point-icon">✓</span>
                    <span>{{ point }}</span>
                  </div>
                </div>
              </div>
            </template>
            <!-- KPI指标页样式 - 根据layout显示不同布局 -->
            <template v-else-if="previewingSlide.type === 'kpi'">
              <div class="slide-kpi-page" :class="'layout-' + (previewingSlide.layout || 'three-kpi')">
                <h2 class="slide-title">{{ previewingSlide.title }}</h2>
                <!-- 优先显示已保存的图片（只取第一张） -->
                <div class="kpi-images-container" v-if="previewingSlide.chartImageUrls && previewingSlide.chartImageUrls.length > 0">
                  <div class="kpi-image-wrapper">
                    <img :src="getChartImageUrl(previewingSlide.chartImageUrls[0])" alt="KPI图片" class="kpi-saved-image" @click="openImagePreview(getChartImageUrl(previewingSlide.chartImageUrls[0]))" />
                  </div>
                </div>
                <!-- 如果没有图片，显示KPI卡片 -->
                <template v-else-if="previewingSlide.kpiCards && previewingSlide.kpiCards.length > 0">
                  <!-- 四指标网格布局 -->
                  <div v-if="previewingSlide.layout === 'four-kpi'" class="kpi-cards-grid">
                    <div class="kpi-card" v-for="(kpi, kpiIdx) in previewingSlide.kpiCards.slice(0, 4)" :key="kpiIdx">
                      <div class="kpi-title">{{ kpi.title }}</div>
                      <div class="kpi-value">{{ kpi.value }}<span class="kpi-unit" v-if="kpi.unit">{{ kpi.unit }}</span></div>
                      <div class="kpi-changes" v-if="kpi.yoyChange || kpi.momChange">
                        <span v-if="kpi.yoyChange" :class="getChangeClass(kpi.yoyChange)">同比: {{ kpi.yoyChange }}</span>
                        <span v-if="kpi.momChange" :class="getChangeClass(kpi.momChange)">环比: {{ kpi.momChange }}</span>
                      </div>
                    </div>
                  </div>
                  <!-- 指标+图表布局 -->
                  <div v-else-if="previewingSlide.layout === 'kpi-with-chart'" class="kpi-with-chart-layout">
                    <div class="kpi-cards-row">
                      <div class="kpi-card-small" v-for="(kpi, kpiIdx) in previewingSlide.kpiCards.slice(0, 4)" :key="kpiIdx">
                        <div class="kpi-title">{{ kpi.title }}</div>
                        <div class="kpi-value">{{ kpi.value }}<span class="kpi-unit" v-if="kpi.unit">{{ kpi.unit }}</span></div>
                      </div>
                    </div>
                    <div class="kpi-chart-area">
                      <el-icon size="24"><DataLine /></el-icon>
                      <span>图表区域</span>
                    </div>
                  </div>
                  <!-- 默认三指标横排布局 -->
                  <div v-else class="kpi-cards-container">
                    <div class="kpi-card" v-for="(kpi, kpiIdx) in previewingSlide.kpiCards" :key="kpiIdx">
                      <div class="kpi-title">{{ kpi.title }}</div>
                      <div class="kpi-value">{{ kpi.value }}<span class="kpi-unit" v-if="kpi.unit">{{ kpi.unit }}</span></div>
                      <div class="kpi-changes" v-if="kpi.yoyChange || kpi.momChange">
                        <span v-if="kpi.yoyChange" :class="getChangeClass(kpi.yoyChange)">同比: {{ kpi.yoyChange }}</span>
                        <span v-if="kpi.momChange" :class="getChangeClass(kpi.momChange)">环比: {{ kpi.momChange }}</span>
                      </div>
                    </div>
                  </div>
                </template>
                <div v-else class="kpi-placeholder">
                  <el-icon size="32"><DataLine /></el-icon>
                  <span>暂无指标数据或图片</span>
                </div>
                <!-- 要点说明 -->
                <ul class="slide-points kpi-notes" v-if="previewingSlide.points && previewingSlide.points.length > 0">
                  <li v-for="(point, i) in previewingSlide.points" :key="i">
                    <span class="bullet">●</span>
                    <span>{{ point }}</span>
                  </li>
                </ul>
              </div>
            </template>
            <!-- 内容页样式 - 根据layout显示不同布局 -->
            <template v-else-if="previewingSlide.type === 'content'">
              <div class="slide-content-page" :class="'layout-' + (previewingSlide.layout || 'bullets-left')">
                <h2 class="slide-title">{{ previewingSlide.title }}</h2>
                <!-- 双栏布局 -->
                <div v-if="previewingSlide.layout === 'two-column'" class="two-column-layout">
                  <ul class="slide-points column-left">
                    <li v-for="(point, i) in getLeftColumnPoints(previewingSlide.points)" :key="'l'+i">
                      <span class="bullet">●</span>
                      <span>{{ point }}</span>
                    </li>
                  </ul>
                  <ul class="slide-points column-right">
                    <li v-for="(point, i) in getRightColumnPoints(previewingSlide.points)" :key="'r'+i">
                      <span class="bullet">●</span>
                      <span>{{ point }}</span>
                    </li>
                  </ul>
                </div>
                <!-- 居中要点布局 -->
                <ul v-else-if="previewingSlide.layout === 'bullets-centered'" class="slide-points centered-points">
                  <li v-for="(point, i) in previewingSlide.points" :key="i">
                    <span class="bullet">●</span>
                    <span>{{ point }}</span>
                  </li>
                </ul>
                <!-- 默认左侧要点布局 -->
                <ul v-else class="slide-points">
                  <li v-for="(point, i) in previewingSlide.points" :key="i">
                    <span class="bullet">●</span>
                    <span>{{ point }}</span>
                  </li>
                </ul>
              </div>
            </template>
            <!-- 图表页样式 - 根据layout显示不同布局 -->
            <template v-else-if="previewingSlide.type === 'chart'">
              <div class="slide-chart-page" :class="'layout-' + (previewingSlide.layout || 'full-image')">
                <h2 class="slide-title">{{ previewingSlide.title }}</h2>
                <!-- 左图右文布局 -->
                <div v-if="previewingSlide.layout === 'image-left-text-right'" class="chart-layout-lr">
                  <div class="chart-area">
                    <template v-if="previewingSlide.chartImageUrls && previewingSlide.chartImageUrls.length > 0">
                      <img :src="getChartImageUrl(previewingSlide.chartImageUrls[0])" alt="图表" class="chart-preview-image" @click="openImagePreview(getChartImageUrl(previewingSlide.chartImageUrls[0]))" />
                    </template>
                    <div v-else class="chart-placeholder-small"><el-icon size="24"><DataLine /></el-icon></div>
                  </div>
                  <ul class="slide-points text-area">
                    <li v-for="(point, i) in previewingSlide.points" :key="i">
                      <span class="bullet">●</span>
                      <span>{{ point }}</span>
                    </li>
                  </ul>
                </div>
                <!-- 左文右图布局 -->
                <div v-else-if="previewingSlide.layout === 'image-right-text-left'" class="chart-layout-rl">
                  <ul class="slide-points text-area">
                    <li v-for="(point, i) in previewingSlide.points" :key="i">
                      <span class="bullet">●</span>
                      <span>{{ point }}</span>
                    </li>
                  </ul>
                  <div class="chart-area">
                    <template v-if="previewingSlide.chartImageUrls && previewingSlide.chartImageUrls.length > 0">
                      <img :src="getChartImageUrl(previewingSlide.chartImageUrls[0])" alt="图表" class="chart-preview-image" @click="openImagePreview(getChartImageUrl(previewingSlide.chartImageUrls[0]))" />
                    </template>
                    <div v-else class="chart-placeholder-small"><el-icon size="24"><DataLine /></el-icon></div>
                  </div>
                </div>
                <!-- 上图下文布局 -->
                <div v-else-if="previewingSlide.layout === 'image-top-text-bottom'" class="chart-layout-tb">
                  <div class="chart-area-top">
                    <template v-if="previewingSlide.chartImageUrls && previewingSlide.chartImageUrls.length > 0">
                      <img :src="getChartImageUrl(previewingSlide.chartImageUrls[0])" alt="图表" class="chart-preview-image" @click="openImagePreview(getChartImageUrl(previewingSlide.chartImageUrls[0]))" />
                    </template>
                    <div v-else class="chart-placeholder-small"><el-icon size="24"><DataLine /></el-icon></div>
                  </div>
                  <ul class="slide-points text-area-bottom">
                    <li v-for="(point, i) in previewingSlide.points" :key="i">
                      <span class="bullet">●</span>
                      <span>{{ point }}</span>
                    </li>
                  </ul>
                </div>
                <!-- 默认全幅图表布局 -->
                <div v-else class="chart-layout-full">
                  <div v-if="previewingSlide.chartImageUrls && previewingSlide.chartImageUrls.length > 0" class="slide-chart-single">
                    <img :src="getChartImageUrl(previewingSlide.chartImageUrls[0])" alt="图表" class="chart-preview-image" @click="openImagePreview(getChartImageUrl(previewingSlide.chartImageUrls[0]))" />
                  </div>
                  <div v-else class="slide-chart-placeholder">
                    <el-icon size="32"><DataLine /></el-icon>
                    <span>暂无图表截图</span>
                    <span class="chart-tip">（请在AI分析页面先生成图表）</span>
                  </div>
                  <ul v-if="previewingSlide.points && previewingSlide.points.length > 0" class="slide-points chart-notes">
                    <li v-for="(point, i) in previewingSlide.points" :key="i">
                      <span class="bullet">●</span>
                      <span>{{ point }}</span>
                    </li>
                  </ul>
                </div>
              </div>
            </template>
            <!-- 页码 -->
            <div class="slide-page-number">{{ previewSlideIndex + 1 }} / {{ outline.slides.length }}</div>
          </div>
          <!-- 导航按钮 -->
          <div class="preview-nav">
            <el-button :disabled="previewSlideIndex === 0" @click="previewSlideIndex--">
              <el-icon><ArrowLeft /></el-icon> 上一页
            </el-button>
            <el-button :disabled="previewSlideIndex === outline.slides.length - 1" @click="previewSlideIndex++">
              下一页 <el-icon><ArrowRight /></el-icon>
            </el-button>
          </div>
        </div>
      </div>
    </div>

    <!-- 底部按钮 -->
    <template #footer>
      <el-button @click="handleClose">取消</el-button>
      <el-button v-if="currentStep > 0" @click="prevStep">上一步</el-button>
      <el-button v-if="currentStep < 2" type="primary" :loading="generating" @click="nextStep">
        {{ currentStep === 0 ? '生成大纲' : '下一步' }}
      </el-button>
      <el-button v-if="currentStep === 2" type="success" :loading="exporting" @click="exportPpt">
        <el-icon><Download /></el-icon> 导出PPT
      </el-button>
    </template>
  </el-dialog>

  <!-- 图片预览对话框 -->
  <el-dialog
    v-model="imagePreviewVisible"
    title="图表预览"
    width="80%"
    :close-on-click-modal="true"
    class="image-preview-dialog"
  >
    <div class="image-preview-container">
      <img :src="previewImageUrl" alt="图表预览" class="preview-full-image" />
    </div>
  </el-dialog>

  <!-- AI优化预览对话框 -->
  <el-dialog
    v-model="showOptimizePreview"
    title="AI优化预览 - 确认修改"
    width="800px"
    :close-on-click-modal="false"
    class="optimize-preview-dialog"
  >
    <div class="optimize-preview-content">
      <div class="preview-compare">
        <!-- 修改前 -->
        <div class="preview-column original">
          <div class="column-header">
            <el-tag type="info">修改前</el-tag>
          </div>
          <div class="preview-card" v-if="optimizePreviewOriginal">
            <h4>{{ optimizePreviewOriginal.title || '(无标题)' }}</h4>
            <div class="preview-layout">
              <el-tag size="small">{{ optimizePreviewOriginal.layout || '默认布局' }}</el-tag>
            </div>
            <ul class="preview-points">
              <li v-for="(point, idx) in optimizePreviewOriginal.points" :key="idx">{{ point }}</li>
            </ul>
            <div class="preview-notes" v-if="optimizePreviewOriginal.notes">
              <strong>备注：</strong>{{ optimizePreviewOriginal.notes }}
            </div>
          </div>
        </div>
        <!-- 箭头 -->
        <div class="preview-arrow">
          <el-icon :size="24"><ArrowRight /></el-icon>
        </div>
        <!-- 修改后 -->
        <div class="preview-column result">
          <div class="column-header">
            <el-tag type="success">修改后</el-tag>
          </div>
          <div class="preview-card" v-if="optimizePreviewResult">
            <h4 :class="{ 'changed': optimizePreviewResult.title !== optimizePreviewOriginal?.title }">
              {{ optimizePreviewResult.title || '(无标题)' }}
            </h4>
            <div class="preview-layout">
              <el-tag size="small" :type="optimizePreviewResult.layout !== optimizePreviewOriginal?.layout ? 'warning' : 'info'">
                {{ optimizePreviewResult.layout || '默认布局' }}
              </el-tag>
            </div>
            <ul class="preview-points">
              <li v-for="(point, idx) in optimizePreviewResult.points" :key="idx"
                  :class="{ 'changed': !optimizePreviewOriginal?.points?.includes(point) }">
                {{ point }}
              </li>
            </ul>
            <div class="preview-notes" v-if="optimizePreviewResult.notes"
                 :class="{ 'changed': optimizePreviewResult.notes !== optimizePreviewOriginal?.notes }">
              <strong>备注：</strong>{{ optimizePreviewResult.notes }}
            </div>
          </div>
        </div>
      </div>
    </div>
    <template #footer>
      <div class="dialog-footer">
        <el-button @click="cancelOptimize">取消</el-button>
        <el-button type="primary" @click="confirmOptimize">
          <el-icon><Promotion /></el-icon> 应用修改
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Delete, Plus, Download, Rank, ArrowLeft, ArrowRight, DataLine, Edit, Promotion, Minus, Sort, Grid, Document, View, ArrowDown } from '@element-plus/icons-vue'
import draggable from 'vuedraggable'
import type { SessionListItem, PptOutlineResponse, PptSlide } from '@/api/ai'
import { generatePptOutline, generatePptFile, optimizePptOutline, OptimizeCommands } from '@/api/ai'

// 定义tag类型
type TagType = 'primary' | 'success' | 'warning' | 'info' | 'danger'

// 布局模板配置（与后端SlideLayouts保持一致）
const layoutOptions: Record<string, { id: string; name: string }[]> = {
  title: [
    { id: 'centered-title', name: '居中封面' },
    { id: 'left-title', name: '左对齐封面' }
  ],
  content: [
    { id: 'bullets-left', name: '左侧要点' },
    { id: 'two-column', name: '双栏布局' },
    { id: 'bullets-centered', name: '居中要点' }
  ],
  chart: [
    { id: 'full-image', name: '全幅图表' },
    { id: 'image-left-text-right', name: '左图右文' },
    { id: 'image-right-text-left', name: '左文右图' },
    { id: 'image-top-text-bottom', name: '上图下文' }
  ],
  kpi: [
    { id: 'three-kpi', name: '三指标卡片' },
    { id: 'four-kpi', name: '四指标网格' },
    { id: 'kpi-with-chart', name: '指标+图表' }
  ],
  summary: [
    { id: 'summary-points', name: '总结要点' },
    { id: 'summary-centered', name: '居中总结' }
  ]
}

// 获取当前幻灯片类型的可用布局
function getAvailableLayouts(type: string) {
  return layoutOptions[type] || layoutOptions.content
}

// 获取默认布局
function getDefaultLayout(type: string): string {
  const defaults: Record<string, string> = {
    title: 'centered-title',
    content: 'bullets-left',
    chart: 'full-image',
    kpi: 'three-kpi',
    summary: 'summary-points'
  }
  return defaults[type] || 'bullets-left'
}

// 双栏布局辅助函数 - 获取左列要点
function getLeftColumnPoints(points: string[]): string[] {
  if (!points || points.length === 0) return []
  const halfCount = Math.ceil(points.length / 2)
  return points.slice(0, halfCount)
}

// 双栏布局辅助函数 - 获取右列要点
function getRightColumnPoints(points: string[]): string[] {
  if (!points || points.length === 0) return []
  const halfCount = Math.ceil(points.length / 2)
  return points.slice(halfCount)
}

const props = defineProps<{
  modelValue: boolean
  sessions: SessionListItem[]
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
}>()

// 控制弹窗显示
const visible = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

// 步骤控制
const currentStep = ref(0)
const generating = ref(false)
const exporting = ref(false)

// 步骤1数据
const pptTitle = ref('')
const audience = ref('')
const selectedSessionIds = ref<number[]>([])

// 步骤2数据
const outline = ref<PptOutlineResponse>({ slides: [] })
const selectedSlideIndex = ref(0)
const selectedSlide = computed(() => outline.value.slides[selectedSlideIndex.value])
const optimizePrompt = ref('')  // AI优化提示词
const optimizing = ref(false)   // 优化中状态
const slideOptimizing = ref(false)     // 单页优化中状态
const slideOptimizePrompt = ref('')    // 单页优化提示词
const showAiPrompt = ref(false)        // 查看AI提示词展开状态

// 修改预览相关
const showOptimizePreview = ref(false)          // 显示预览对话框
const optimizePreviewOriginal = ref<PptSlide | null>(null)   // 原始幻灯片
const optimizePreviewResult = ref<PptSlide | null>(null)     // 优化后幻灯片
const optimizePreviewOutline = ref<PptOutlineResponse | null>(null)  // 优化后完整大纲

// 步骤3数据
const selectedTemplate = ref('business')
const previewSlideIndex = ref(0)
const previewingSlide = computed(() => outline.value.slides[previewSlideIndex.value])

// 图片预览数据
const imagePreviewVisible = ref(false)
const previewImageUrl = ref('')

// 打开图片预览
function openImagePreview(url: string) {
  previewImageUrl.value = url
  imagePreviewVisible.value = true
}

// 格式化时间
function formatTime(dateStr: string): string {
  if (!dateStr) return ''
  const date = new Date(dateStr)
  return date.toLocaleDateString('zh-CN', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}

// 获取幻灯片类型标签
function getSlideTypeLabel(type: string): string {
  const labels: Record<string, string> = { title: '封面', content: '内容', chart: '图表', kpi: '指标', summary: '总结' }
  return labels[type] || '内容'
}

function getSlideTypeTag(type: string): TagType {
  const tags: Record<string, TagType> = { title: 'primary', content: 'info', chart: 'warning', kpi: 'danger', summary: 'success' }
  return tags[type] || 'info'
}

// 选择幻灯片
function selectSlide(index: number) {
  selectedSlideIndex.value = index
}

// 类型变更时自动设置默认布局
function handleSlideTypeChange(newType: string) {
  if (selectedSlide.value) {
    selectedSlide.value.layout = getDefaultLayout(newType)
  }
}

// 添加幻灯片
function addSlide() {
  const newSlide: PptSlide = {
    order: outline.value.slides.length + 1,
    type: 'content',
    title: '新幻灯片',
    points: [''],
    notes: '',
    layout: 'bullets-left'  // 默认布局
  }
  outline.value.slides.push(newSlide)
  selectedSlideIndex.value = outline.value.slides.length - 1
}

// 删除幻灯片
function removeSlide(index: number) {
  if (outline.value.slides.length <= 1) {
    ElMessage.warning('至少保留一张幻灯片')
    return
  }
  outline.value.slides.splice(index, 1)
  // 调整选中索引
  if (selectedSlideIndex.value >= outline.value.slides.length) {
    selectedSlideIndex.value = outline.value.slides.length - 1
  }
}

// 添加要点
function addPoint() {
  if (selectedSlide.value) {
    selectedSlide.value.points.push('')
  }
}

// 删除要点
function removePoint(index: number) {
  if (selectedSlide.value && selectedSlide.value.points.length > 1) {
    selectedSlide.value.points.splice(index, 1)
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

// 获取KPI变化值的CSS类（正值为增长绿色，负值为下降红色）
function getChangeClass(change: string | undefined): string {
  if (!change) return ''
  if (change.startsWith('+') || change.startsWith('↑')) return 'change-up'
  if (change.startsWith('-') || change.startsWith('↓')) return 'change-down'
  // 判断数值是否为正
  const numMatch = change.match(/[\d.]+/)
  if (numMatch && parseFloat(numMatch[0]) > 0 && !change.includes('-')) return 'change-up'
  if (numMatch && change.includes('-')) return 'change-down'
  return ''
}

// 上一步
function prevStep() {
  if (currentStep.value > 0) currentStep.value--
}

// 下一步
async function nextStep() {
  if (currentStep.value === 0) {
    // 验证
    if (!pptTitle.value.trim()) {
      ElMessage.warning('请输入PPT标题')
      return
    }
    if (selectedSessionIds.value.length === 0) {
      ElMessage.warning('请至少选择一个会话')
      return
    }
    // 调用API生成大纲
    generating.value = true
    try {
      const res = await generatePptOutline({
        sessionIds: selectedSessionIds.value,
        title: pptTitle.value,
        audience: audience.value
      })
      if (res.code === 0 && res.data) {
        outline.value = res.data
        selectedSlideIndex.value = 0
        previewSlideIndex.value = 0
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

// AI优化大纲
async function optimizeOutline() {
  if (!optimizePrompt.value.trim()) {
    ElMessage.warning('请输入优化提示词')
    return
  }

  optimizing.value = true
  try {
    const res = await optimizePptOutline({
      outline: outline.value,
      prompt: optimizePrompt.value
    })
    if (res.code === 0 && res.data) {
      outline.value = res.data
      optimizePrompt.value = ''  // 清空提示词
      ElMessage.success('大纲优化成功')
    } else {
      ElMessage.error(res.message || '优化失败')
    }
  } catch (err: any) {
    ElMessage.error(err.message || '优化失败')
  } finally {
    optimizing.value = false
  }
}

// 单页快捷优化（使用预定义指令）- 先显示预览
async function quickOptimizeSlide(command: keyof typeof OptimizeCommands) {
  if (!selectedSlide.value || slideOptimizing.value) return

  slideOptimizing.value = true
  try {
    // 保存原始幻灯片（深拷贝）
    optimizePreviewOriginal.value = JSON.parse(JSON.stringify(selectedSlide.value))

    const res = await optimizePptOutline({
      outline: outline.value,
      prompt: OptimizeCommands[command],
      slideIndex: selectedSlideIndex.value,
      mode: 'single'
    })
    if (res.code === 0 && res.data) {
      // 显示预览对话框，等待用户确认
      optimizePreviewOutline.value = res.data
      optimizePreviewResult.value = res.data.slides[selectedSlideIndex.value]
      showOptimizePreview.value = true
    } else {
      ElMessage.error(res.message || '优化失败')
    }
  } catch (err: any) {
    ElMessage.error(err.message || '优化请求失败')
  } finally {
    slideOptimizing.value = false
  }
}

// 单页自定义优化 - 先显示预览
async function customOptimizeSlide() {
  if (!selectedSlide.value || slideOptimizing.value || !slideOptimizePrompt.value.trim()) return

  slideOptimizing.value = true
  try {
    // 保存原始幻灯片（深拷贝）
    optimizePreviewOriginal.value = JSON.parse(JSON.stringify(selectedSlide.value))

    const res = await optimizePptOutline({
      outline: outline.value,
      prompt: slideOptimizePrompt.value,
      slideIndex: selectedSlideIndex.value,
      mode: 'single'
    })
    if (res.code === 0 && res.data) {
      // 显示预览对话框，等待用户确认
      optimizePreviewOutline.value = res.data
      optimizePreviewResult.value = res.data.slides[selectedSlideIndex.value]
      showOptimizePreview.value = true
      slideOptimizePrompt.value = ''  // 清空输入
    } else {
      ElMessage.error(res.message || '优化失败')
    }
  } catch (err: any) {
    ElMessage.error(err.message || '优化请求失败')
  } finally {
    slideOptimizing.value = false
  }
}

// 确认应用优化结果
function confirmOptimize() {
  if (optimizePreviewOutline.value) {
    outline.value = optimizePreviewOutline.value
    ElMessage.success('已应用优化结果')
  }
  closeOptimizePreview()
}

// 取消优化
function cancelOptimize() {
  ElMessage.info('已取消优化')
  closeOptimizePreview()
}

// 关闭预览对话框
function closeOptimizePreview() {
  showOptimizePreview.value = false
  optimizePreviewOriginal.value = null
  optimizePreviewResult.value = null
  optimizePreviewOutline.value = null
}

// 导出PPT（原生方案）
async function exportPpt() {
  exporting.value = true
  try {
    const res = await generatePptFile({
      outline: outline.value,
      template: selectedTemplate.value,
      pptTitle: pptTitle.value,
      audience: audience.value
    })
    if (res.code === 0 && res.data) {
      // 下载PPT文件
      const byteCharacters = atob(res.data)
      const byteNumbers = new Array(byteCharacters.length)
      for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i)
      }
      const byteArray = new Uint8Array(byteNumbers)
      const blob = new Blob([byteArray], { type: 'application/vnd.openxmlformats-officedocument.presentationml.presentation' })
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `${pptTitle.value || 'PPT报告'}.pptx`
      link.click()
      URL.revokeObjectURL(url)
      ElMessage.success('PPT导出成功，您可以继续调整或关闭窗口')
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
  // 重置状态
  currentStep.value = 0
  pptTitle.value = ''
  audience.value = ''
  selectedSessionIds.value = []
  outline.value = { slides: [] }
  selectedSlideIndex.value = 0
  selectedTemplate.value = 'business'
}

// 监听弹窗打开
watch(visible, (val) => {
  if (val && props.sessions.length > 0) {
    // 默认选中第一个会话
    selectedSessionIds.value = [props.sessions[0].id]
  }
})
</script>

<style scoped lang="scss">
.step-content {
  min-height: 400px;
}

.session-select-area {
  max-height: 300px;
  overflow-y: auto;
  border: 1px solid var(--el-border-color-light);
  border-radius: 4px;
  padding: 12px;
}

.session-checkbox {
  display: flex;
  width: 100%;
  margin-bottom: 8px;

  :deep(.el-checkbox__label) {
    flex: 1;
  }
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
  gap: 16px;
  min-height: 400px;
  flex: 1;
}

/* 提示词输入区域 */
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

.slides-list {
  width: 220px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 4px;
  padding: 8px;
  overflow-y: auto;
  max-height: 400px;
}

.slides-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 4px 8px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  margin-bottom: 8px;
  font-weight: 500;
}

.slide-item {
  padding: 8px;
  border-radius: 4px;
  margin-bottom: 8px;
  cursor: pointer;
  border: 1px solid transparent;
  background: var(--el-fill-color-blank);
  transition: all 0.2s;

  &:hover {
    background: var(--el-fill-color-light);

    .delete-btn {
      opacity: 1;
    }
  }

  &.active {
    border-color: var(--el-color-primary);
    background: var(--el-color-primary-light-9);
  }
}

.slide-ghost {
  opacity: 0.5;
  background: var(--el-color-primary-light-8);
}

.slide-header {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 4px;

  .delete-btn {
    opacity: 0;
    transition: opacity 0.2s;
    margin-left: auto;
  }
}

.drag-handle {
  cursor: grab;
  color: var(--el-text-color-secondary);

  &:active {
    cursor: grabbing;
  }
}

.slide-order {
  width: 18px;
  height: 18px;
  border-radius: 50%;
  background: var(--el-color-primary);
  color: white;
  font-size: 11px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.slide-title {
  font-size: 12px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--el-text-color-regular);
}

.slide-editor {
  flex: 1;
  border: 1px solid var(--el-border-color-light);
  border-radius: 4px;
  padding: 16px;
  overflow-y: auto;
  max-height: 450px;
}

/* 单页AI编辑区域 */
.slide-ai-edit {
  margin-top: 16px;
  padding: 12px;
  background: linear-gradient(135deg, rgba(64, 158, 255, 0.05), rgba(103, 194, 58, 0.05));
  border-radius: 8px;
  border: 1px dashed var(--el-color-primary-light-5);
}

.ai-edit-header {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  font-weight: 500;
  color: var(--el-color-primary);
  margin-bottom: 10px;
}

.ai-quick-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 10px;

  .el-button {
    font-size: 12px;
    padding: 4px 8px;
  }
}

.ai-custom-edit {
  .el-input {
    font-size: 12px;
  }
}

.point-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;

  .point-drag-handle {
    cursor: grab;
    color: var(--el-text-color-secondary);
    flex-shrink: 0;

    &:active {
      cursor: grabbing;
    }
  }

  .el-input {
    flex: 1;
  }
}

.template-option {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}

.template-preview {
  width: 60px;
  height: 40px;
  border-radius: 4px;
  border: 1px solid var(--el-border-color);

  &.business {
    background: linear-gradient(135deg, #003366 0%, #006699 100%);
  }

  &.medical {
    background: linear-gradient(135deg, #006633 0%, #009966 100%);
  }

  &.simple {
    background: linear-gradient(135deg, #f5f5f5 0%, #ffffff 100%);
  }

  &.tech {
    background: linear-gradient(135deg, #2D1B4E 0%, #9B59B6 100%);
  }

  &.warm {
    background: linear-gradient(135deg, #8B4513 0%, #FF8C00 100%);
  }

  &.dark {
    background: linear-gradient(135deg, #2C3E50 0%, #3498DB 100%);
  }
}

.preview-area {
  margin-top: 16px;
}

.preview-slides {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
}

.preview-slide {
  width: 160px;
  height: 100px;
  border-radius: 4px;
  padding: 8px;
  font-size: 10px;
  overflow: hidden;

  &.business {
    background: linear-gradient(135deg, #003366 0%, #004488 100%);
    color: white;
  }

  &.medical {
    background: linear-gradient(135deg, #006633 0%, #008855 100%);
    color: white;
  }

  &.simple {
    background: #f8f8f8;
    color: #333;
    border: 1px solid #ddd;
  }

  &.tech {
    background: linear-gradient(135deg, #2D1B4E 0%, #5B3A8C 100%);
    color: white;
  }

  &.warm {
    background: linear-gradient(135deg, #8B4513 0%, #C66922 100%);
    color: white;
  }

  &.dark {
    background: linear-gradient(135deg, #2C3E50 0%, #3D5166 100%);
    color: white;
  }
}

.preview-slide-type {
  font-size: 8px;
  opacity: 0.7;
  margin-bottom: 4px;
}

.preview-slide-title {
  font-weight: bold;
  font-size: 11px;
  margin-bottom: 6px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.preview-slide-points {
  font-size: 9px;
  margin: 0;
  padding-left: 12px;

  li {
    margin-bottom: 2px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
}

// 步骤3预览增强样式
.step-preview {
  display: flex;
  flex-direction: column;
}

.preview-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.preview-container {
  display: flex;
  gap: 16px;
  flex: 1;
  min-height: 350px;
}

.preview-thumbnails {
  width: 120px;
  max-height: 350px;
  overflow-y: auto;
  border: 1px solid var(--el-border-color-light);
  border-radius: 4px;
  padding: 8px;
}

.thumbnail-item {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 8px;
  cursor: pointer;
  padding: 4px;
  border-radius: 4px;

  &:hover {
    background: var(--el-fill-color-light);
  }

  &.active {
    background: var(--el-color-primary-light-9);

    .thumbnail-preview {
      border-color: var(--el-color-primary);
    }
  }
}

.thumbnail-number {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  width: 16px;
}

.thumbnail-preview {
  width: 70px;
  height: 45px;
  border-radius: 2px;
  padding: 4px;
  border: 1px solid var(--el-border-color);

  &.business {
    background: linear-gradient(135deg, #003366 0%, #006699 100%);
    color: white;
  }

  &.medical {
    background: linear-gradient(135deg, #006633 0%, #009966 100%);
    color: white;
  }

  &.simple {
    background: #f8f8f8;
    color: #333;
  }

  &.tech {
    background: linear-gradient(135deg, #2D1B4E 0%, #9B59B6 100%);
    color: white;
  }

  &.warm {
    background: linear-gradient(135deg, #8B4513 0%, #FF8C00 100%);
    color: white;
  }

  &.dark {
    background: linear-gradient(135deg, #2C3E50 0%, #3498DB 100%);
    color: white;
  }
}

.thumbnail-title {
  font-size: 8px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.preview-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.preview-slide-large {
  flex: 1;
  border-radius: 8px;
  padding: 24px;
  position: relative;
  min-height: 280px;

  &.business {
    background: linear-gradient(135deg, #003366 0%, #004488 100%);
    color: white;
  }

  &.medical {
    background: linear-gradient(135deg, #006633 0%, #008855 100%);
    color: white;
  }

  &.simple {
    background: #f8f8f8;
    color: #333;
    border: 1px solid #ddd;
  }

  &.tech {
    background: linear-gradient(135deg, #2D1B4E 0%, #5B3A8C 100%);
    color: white;
  }

  &.warm {
    background: linear-gradient(135deg, #8B4513 0%, #C66922 100%);
    color: white;
  }

  &.dark {
    background: linear-gradient(135deg, #2C3E50 0%, #3D5166 100%);
    color: white;
  }
}

.slide-cover {
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  height: 100%;
  text-align: center;
}

.slide-main-title {
  font-size: 28px;
  margin-bottom: 16px;
}

.slide-sub-title {
  font-size: 18px;
  opacity: 0.9;
  margin-bottom: 12px;
}

.slide-meta {
  font-size: 14px;
  opacity: 0.7;
}

.slide-summary, .slide-content-page {
  height: 100%;
}

.slide-title {
  font-size: 22px;
  margin-bottom: 20px;
  padding-bottom: 8px;
  border-bottom: 2px solid currentColor;
  opacity: 0.9;
}

.slide-points {
  list-style: none;
  padding: 0;
  margin: 0;

  li {
    display: flex;
    align-items: flex-start;
    gap: 12px;
    margin-bottom: 12px;
    font-size: 16px;

    .bullet {
      opacity: 0.7;
    }
  }
}

.summary-points {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.summary-point {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 16px;

  .point-icon {
    width: 24px;
    height: 24px;
    border-radius: 50%;
    background: rgba(255, 255, 255, 0.2);
    display: flex;
    align-items: center;
    justify-content: center;
  }
}

.slide-chart-placeholder {
  margin-top: 20px;
  padding: 40px;
  border: 2px dashed rgba(255, 255, 255, 0.3);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  opacity: 0.6;

  .chart-tip {
    font-size: 12px;
    opacity: 0.7;
  }
}

/* 图表截图预览区域 - 单张大图 */
.slide-chart-single {
  margin-top: 10px;
  text-align: center;
}

.slide-chart-single .chart-preview-image {
  width: 100%;
  max-height: 280px;
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.2);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
  object-fit: contain;
  background: rgba(255, 255, 255, 0.95);
  cursor: pointer;
  transition: transform 0.2s, box-shadow 0.2s;
}

.slide-chart-single .chart-preview-image:hover {
  transform: scale(1.02);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

.chart-image-wrapper {
  position: relative;
  display: inline-block;
}

.chart-preview-image {
  max-width: 100%;
  max-height: 200px;
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.2);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
  object-fit: contain;
  background: rgba(255, 255, 255, 0.95);
  cursor: pointer;
}

.slide-page-number {
  position: absolute;
  bottom: 12px;
  right: 16px;
  font-size: 12px;
  opacity: 0.5;
}

.preview-nav {
  display: flex;
  justify-content: center;
  gap: 12px;
}

/* 图片预览对话框 */
.image-preview-dialog .el-dialog__body {
  padding: 0;
}

.image-preview-container {
  display: flex;
  justify-content: center;
  align-items: center;
  background: #f5f5f5;
  min-height: 400px;
  max-height: 80vh;
  overflow: auto;
}

.preview-full-image {
  max-width: 100%;
  max-height: 80vh;
  object-fit: contain;
}

/* KPI指标页样式 */
.slide-kpi-page {
  padding: 24px;
  height: 100%;
  display: flex;
  flex-direction: column;
}

/* KPI图片容器样式 - 用于显示已保存的指标图片 */
.kpi-images-container {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  justify-content: center;
  align-items: center;
  margin: 16px 0;
  flex: 1;
}

.kpi-image-wrapper {
  background: rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  padding: 8px;
  overflow: hidden;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
}

.kpi-saved-image {
  max-width: 100%;
  max-height: 280px;
  object-fit: contain;
  border-radius: 4px;
}

.kpi-cards-container {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  justify-content: center;
  margin: 20px 0;
  flex: 1;
}

.kpi-card {
  background: rgba(255, 255, 255, 0.15);
  border-radius: 12px;
  padding: 20px;
  min-width: 160px;
  max-width: 200px;
  flex: 1;
  text-align: center;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 8px;
  backdrop-filter: blur(10px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  transition: transform 0.2s;
}

.kpi-card:hover {
  transform: translateY(-2px);
}

.kpi-title {
  font-size: 14px;
  opacity: 0.9;
  font-weight: 500;
}

.kpi-value {
  font-size: 28px;
  font-weight: bold;
  color: #fff;
  text-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
}

.kpi-unit {
  font-size: 14px;
  font-weight: normal;
  opacity: 0.7;
  margin-left: 4px;
}

.kpi-changes {
  display: flex;
  justify-content: center;
  gap: 12px;
  font-size: 12px;
  margin-top: 4px;
}

.change-up {
  color: #67c23a;
}

.change-down {
  color: #f56c6c;
}

.kpi-placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  flex: 1;
  opacity: 0.6;
}

.kpi-notes {
  margin-top: auto;
  padding-top: 16px;
  border-top: 1px solid rgba(255, 255, 255, 0.2);
}

/* AI优化预览对话框 */
.optimize-preview-dialog .el-dialog__body {
  padding: 20px;
}

.optimize-preview-content {
  min-height: 200px;
}

.preview-compare {
  display: flex;
  align-items: flex-start;
  gap: 16px;
}

.preview-column {
  flex: 1;
  min-width: 0;
}

.column-header {
  margin-bottom: 12px;
  text-align: center;
}

.preview-arrow {
  display: flex;
  align-items: center;
  justify-content: center;
  padding-top: 80px;
  color: var(--el-color-primary);
}

.preview-card {
  background: var(--el-fill-color-light);
  border-radius: 8px;
  padding: 16px;
  border: 1px solid var(--el-border-color-light);
  min-height: 180px;

  h4 {
    margin: 0 0 8px;
    font-size: 16px;
    color: var(--el-text-color-primary);

    &.changed {
      color: var(--el-color-warning);
      font-weight: bold;
    }
  }
}

.preview-layout {
  margin-bottom: 12px;
}

.preview-points {
  margin: 0;
  padding-left: 20px;

  li {
    margin-bottom: 6px;
    color: var(--el-text-color-regular);

    &.changed {
      color: var(--el-color-success);
      font-weight: 500;
    }
  }
}

.preview-notes {
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px dashed var(--el-border-color);
  font-size: 13px;
  color: var(--el-text-color-secondary);

  &.changed {
    color: var(--el-color-success);
  }
}

.preview-column.result .preview-card {
  border-color: var(--el-color-success-light-5);
  background: linear-gradient(135deg, rgba(103, 194, 58, 0.02), rgba(103, 194, 58, 0.05));
}

/* ========== 智能布局预览样式 ========== */

/* 封面页布局样式 */
.slide-cover.layout-left {
  align-items: flex-start;
  text-align: left;
  padding-left: 40px;
}

.slide-cover.layout-centered {
  align-items: center;
  text-align: center;
}

/* 内容页 - 双栏布局 */
.two-column-layout {
  display: flex;
  gap: 20px;
  margin-top: 16px;
}

.column-left, .column-right {
  flex: 1;
}

/* 内容页 - 居中要点 */
.centered-points {
  text-align: center;
  li {
    justify-content: center;
  }
}

/* 图表页布局样式 */
.slide-chart-page {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.chart-layout-lr, .chart-layout-rl {
  display: flex;
  gap: 16px;
  flex: 1;
  margin-top: 16px;
}

.chart-area {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 120px;
}

.text-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
}

/* 上图下文布局 */
.chart-layout-tb {
  display: flex;
  flex-direction: column;
  gap: 12px;
  flex: 1;
  margin-top: 16px;
}

.chart-area-top {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100px;
}

.text-area-bottom {
  font-size: 14px;
}

/* 全幅图表布局 */
.chart-layout-full {
  flex: 1;
  display: flex;
  flex-direction: column;
}

/* 图表占位符小尺寸 */
.chart-placeholder-small {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border: 1px dashed rgba(255,255,255,0.3);
  border-radius: 8px;
  opacity: 0.6;
}

/* 图表备注 */
.chart-notes {
  margin-top: 12px;
  font-size: 13px;
  opacity: 0.8;
}

/* KPI页布局样式 */
.slide-kpi-page {
  height: 100%;
  display: flex;
  flex-direction: column;
}

/* KPI网格布局（2x2） */
.kpi-cards-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
  flex: 1;
  margin-top: 16px;
}

/* KPI+图表布局 */
.kpi-with-chart-layout {
  display: flex;
  flex-direction: column;
  gap: 16px;
  flex: 1;
  margin-top: 16px;
}

.kpi-cards-row {
  display: flex;
  gap: 12px;
}

.kpi-card-small {
  flex: 1;
  padding: 12px;
  background: rgba(255,255,255,0.1);
  border-radius: 8px;
  text-align: center;

  .kpi-title {
    font-size: 12px;
    opacity: 0.8;
    margin-bottom: 4px;
  }

  .kpi-value {
    font-size: 18px;
    font-weight: bold;
  }
}

.kpi-chart-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border: 1px dashed rgba(255,255,255,0.3);
  border-radius: 8px;
  opacity: 0.6;
}

/* 总结页居中布局 */
.summary-content-centered {
  text-align: center;
  margin-top: 20px;

  p {
    margin-bottom: 16px;
    font-size: 16px;
    line-height: 1.8;
  }
}

.slide-summary.layout-centered {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
}

</style>

