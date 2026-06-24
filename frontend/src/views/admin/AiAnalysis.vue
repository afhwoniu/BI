<template>
  <div class="ai-analysis">
    <!-- 左侧：对话历史（标签页分类） -->
    <div class="left-panel">
      <!-- 会话历史 -->
      <el-card class="history-card">
        <template #header>
          <div class="history-header">
            <span>对话历史</span>
            <el-button link size="small" @click="loadSessionList" :loading="loadingHistory">
              <el-icon><Refresh /></el-icon>
            </el-button>
          </div>
        </template>
        <!-- 标签页切换 -->
        <el-tabs v-model="historyTab" class="history-tabs" @tab-change="onHistoryTabChange">
          <el-tab-pane name="bi">
            <template #label>
              <span class="tab-label">
                <el-icon><DataLine /></el-icon>
                <span>BI统计</span>
                <el-badge v-if="groupedSessions.bi.length > 0" :value="groupedSessions.bi.length" type="primary" class="tab-badge" />
              </span>
            </template>
          </el-tab-pane>
          <el-tab-pane name="hz360">
            <template #label>
              <span class="tab-label">
                <el-icon><User /></el-icon>
                <span>患者360</span>
                <el-badge v-if="groupedSessions.hz360.length > 0" :value="groupedSessions.hz360.length" type="success" class="tab-badge" />
              </span>
            </template>
          </el-tab-pane>
          <el-tab-pane name="internetsearch">
            <template #label>
              <span class="tab-label">
                <el-icon><Search /></el-icon>
                <span>AI检索</span>
                <el-badge v-if="groupedSessions.internetsearch.length > 0" :value="groupedSessions.internetsearch.length" type="warning" class="tab-badge" />
              </span>
            </template>
          </el-tab-pane>
          <el-tab-pane name="report">
            <template #label>
              <span class="tab-label">
                <el-icon><Grid /></el-icon>
                <span>报表</span>
                <el-badge v-if="groupedSessions.report.length > 0" :value="groupedSessions.report.length" type="info" class="tab-badge" />
              </span>
            </template>
          </el-tab-pane>
        </el-tabs>
        <!-- 搜索框 -->
        <el-input
          v-model="historySearch"
          placeholder="搜索历史..."
          size="small"
          clearable
          style="margin-bottom: 8px"
        >
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>
        <div class="history-list">
          <!-- 当前标签页的会话列表 -->
          <div
            v-for="session in currentTabSessions"
            :key="session.id"
            class="history-item session-item"
            :class="{ active: sessionId === session.sessionKey, disabled: !session.lastMessageId }"
            @click="replaySession(session)"
          >
            <div class="session-content">
              <div class="session-header">
                <span class="session-id">#{{ session.id }}</span>
                <el-button
                  class="session-edit-btn"
                  :icon="Edit"
                  size="small"
                  link
                  @click.stop="editSessionTitle(session)"
                  title="编辑名称"
                />
              </div>
              <div class="session-title">{{ session.title }}</div>
              <div class="session-meta">
                <span class="session-time">{{ formatTimeAgo(session.createdAt) }}</span>
                <!-- 图片数量显示 -->
                <span v-if="session.imageCount && session.imageCount > 0"
                      class="session-images"
                      @click.stop="previewSessionImages(session)"
                      title="点击预览已保存的图片">
                  <el-icon><Picture /></el-icon> {{ session.imageCount }}张
                </span>
              </div>
            </div>
            <el-button
              class="session-delete-btn"
              :icon="Delete"
              size="small"
              type="danger"
              text
              @click.stop="deleteSession(session)"
              title="删除此会话"
            />
          </div>

          <el-empty v-if="currentTabSessions.length === 0" :description="historySearch ? '未找到匹配项' : '暂无历史'" :image-size="60" />
        </div>
        <!-- 报告生成按钮 -->
        <div class="report-generate-btns">
          <el-button type="success" :disabled="groupedSessions.bi.length === 0" @click="showPptGenerator = true">
            <el-icon><Document /></el-icon>
            生成PPT
          </el-button>
          <el-button type="primary" :disabled="groupedSessions.bi.length === 0" @click="showWordGenerator = true">
            <el-icon><Document /></el-icon>
            生成Word
          </el-button>
        </div>
      </el-card>
    </div>
    
    <!-- 右侧：对话和结果 -->
    <div class="right-panel">
      <!-- 输入区 -->
      <el-card class="input-card">
        <div class="input-area">
          <el-input v-model="question" type="textarea" :rows="2" placeholder="请输入您的分析需求，例如：统计各科室的门诊人次（支持语音输入）" @keyup.enter.ctrl="sendQuestion" />
          <!-- 语音唤醒按钮（自动从后端配置读取唤醒词，无需手动传入） -->
          <VoiceWakeup
            ref="voiceWakeupRef"
            :enabled="true"
            @wakeup="onVoiceWakeup"
            @command="onVoiceCommand"
            style="margin-left: 8px"
          />
          <!-- 语音输入按钮 -->
          <VoiceInput
            ref="voiceInputRef"
            :disabled="loading"
            @transcribed="onVoiceTranscribed"
            @error="onVoiceError"
            style="margin-left: 4px"
          />
          <el-button type="primary" :loading="loading" @click="sendQuestion" style="margin-left: 8px">
            <el-icon><Promotion /></el-icon>
            发送
          </el-button>
        </div>
        <!-- 时间范围选择标签 -->
        <div class="time-range-bar">
          <span class="time-range-label">时间范围：</span>
          <div class="time-range-tags">
            <el-tag
              v-for="opt in timeRangeOptions"
              :key="opt.value"
              :type="timeRangeType === opt.value ? 'primary' : 'info'"
              :effect="timeRangeType === opt.value ? 'dark' : 'plain'"
              class="time-tag"
              @click="onTimeRangeSelect(opt.value)"
            >
              {{ opt.label }}
            </el-tag>
          </div>
          <!-- 自定义日期选择器 -->
          <el-date-picker
            v-if="timeRangeType === 'custom'"
            v-model="dateRange"
            type="daterange"
            range-separator="至"
            start-placeholder="开始"
            end-placeholder="结束"
            format="YYYY-MM-DD"
            value-format="YYYY-MM-DD"
            size="small"
            style="margin-left: 8px; width: 240px;"
            @change="onCustomDateRangeChange"
          />
        </div>
      </el-card>
      
      <!-- 结果区 -->
      <el-card class="result-card" v-if="currentResult">
        <template #header>
          <div class="result-header">
            <span>分析结果</span>
            <div class="result-actions">
              <el-button size="small" @click="showPromptDialog = true" v-if="currentResult.prompts?.length || currentResult.promptText">查看提示词</el-button>
              <el-button size="small" @click="showSqlDialog = true">查看SQL</el-button>
            </div>
          </div>
        </template>
        
        <!-- 下钻面包屑导航（仅BI模式显示） -->
        <div class="drill-breadcrumbs" v-if="currentResult.mode === 'bi' && drillBreadcrumbs.length > 1">
          <el-breadcrumb separator="/">
            <el-breadcrumb-item
              v-for="(crumb, idx) in drillBreadcrumbs"
              :key="idx"
              @click="idx < drillBreadcrumbs.length - 1 && navigateBreadcrumb(idx)"
              :class="{ 'clickable': idx < drillBreadcrumbs.length - 1, 'current': idx === drillBreadcrumbs.length - 1 }"
            >
              {{ crumb.label }}
            </el-breadcrumb-item>
          </el-breadcrumb>
          <el-button size="small" type="warning" plain @click="resetDrill" style="margin-left: 12px">
            <el-icon><RefreshLeft /></el-icon> 返回初始
          </el-button>
        </div>

        <!-- 模式标签 -->
        <div class="mode-badge" v-if="currentResult.mode">
          <el-tag :type="getModeTagType(currentResult.mode)" size="small">
            {{ getModeLabel(currentResult.mode) }}
          </el-tag>
          <span class="mode-reason" v-if="currentResult.modeReason">{{ currentResult.modeReason }}</span>
          <!-- 知识问答模式添加查看提示词按钮 -->
          <el-button
            v-if="currentResult.mode === 'internetsearch' && currentResult.prompts?.length"
            size="small"
            type="info"
            plain
            @click="showPromptDialog = true"
            style="margin-left: 12px"
          >
            <el-icon><Document /></el-icon> 查看提示词
          </el-button>
        </div>

        <!-- AI回答 -->
        <div class="answer-section" v-if="currentResult.answer || streamingAnswer">
          <!-- 通用问答模式用Markdown渲染（支持流式显示） -->
          <div v-if="currentResult.mode === 'internetsearch'" class="markdown-answer">
            <div v-html="renderMarkdown(streamingAnswer || currentResult.answer || '')"></div>
            <span v-if="isStreaming" class="streaming-cursor">▌</span>
          </div>
          <!-- 其他模式用普通alert -->
          <el-alert v-else :title="currentResult.answer" type="info" :closable="false" show-icon />
        </div>

        <!-- 报表模式 - 多页签表格 -->
        <template v-if="currentResult.mode === 'report' && currentResult.reportSheets && currentResult.reportSheets.length > 0">
          <!-- 报表工具栏 -->
          <div class="report-toolbar">
            <div class="report-toolbar-left">
              <el-button size="small" type="success" @click="exportReportExcel">
                <el-icon><Download /></el-icon> 导出Excel
              </el-button>
              <el-button size="small" @click="printReport">
                <el-icon><Document /></el-icon> 打印
              </el-button>
            </div>
            <div class="report-toolbar-right">
              <span class="report-row-count">共 {{ currentReportSheet?.rows?.length || 0 }} 条数据</span>
            </div>
          </div>

          <!-- 多页签 -->
          <el-tabs v-model="activeReportSheet" type="border-card" class="report-tabs" v-if="currentResult.reportSheets.length > 1">
            <el-tab-pane v-for="(sheet, sIdx) in currentResult.reportSheets" :key="sIdx" :name="String(sIdx)" :label="sheet.title" />
          </el-tabs>

          <!-- 报表表格 -->
          <div class="report-table-wrapper" v-if="currentReportSheet">
            <el-table
              :data="currentReportSheet.rows"
              border
              stripe
              :max-height="reportTableMaxHeight"
              style="width: 100%"
              size="small"
              :header-cell-style="{ background: '#f5f7fa', color: '#333', fontWeight: 'bold' }"
              show-summary
              :summary-method="getReportSummary"
            >
              <el-table-column type="index" label="#" width="50" fixed="left" align="center" />
              <el-table-column
                v-for="col in currentReportSheet.columns"
                :key="col.field"
                :prop="col.field"
                :label="col.title"
                :width="col.width || undefined"
                :min-width="col.width ? undefined : 100"
                :align="(col.align as 'left' | 'center' | 'right') || 'left'"
                :fixed="col.field === currentReportSheet.columns[0]?.field ? 'left' : undefined"
              >
                <template #default="{ row }">
                  <span v-if="col.dataType === 'number'" class="report-number">{{ formatReportNumber(row[col.field]) }}</span>
                  <span v-else>{{ row[col.field] }}</span>
                </template>
              </el-table-column>
            </el-table>
          </div>
        </template>

        <!-- 患者360模式 - 显示患者卡片列表 -->
        <template v-if="currentResult.mode === 'hz360' && currentResult.patients && currentResult.patients.length > 0">
          <div class="patient-cards">
            <div class="patient-card" v-for="patient in currentResult.patients" :key="patient.patientId" @click="openPatient360(patient)">
              <div class="patient-header">
                <span class="patient-name">{{ patient.patientName }}</span>
                <el-tag size="small" :type="patient.gender === '男' ? 'primary' : 'danger'">{{ patient.gender || '未知' }}</el-tag>
                <span class="patient-age" v-if="patient.age">{{ patient.age }}岁</span>
              </div>
              <div class="patient-info">
                <div class="info-row" v-if="patient.birthDate">
                  <span class="info-label">出生日期：</span>
                  <span class="info-value">{{ formatDate(patient.birthDate) }}</span>
                </div>
                <div class="info-row" v-if="patient.idCard">
                  <span class="info-label">身份证：</span>
                  <span class="info-value">{{ patient.idCard }}</span>
                </div>
                <div class="info-row" v-if="patient.phone">
                  <span class="info-label">电话：</span>
                  <span class="info-value">{{ patient.phone }}</span>
                </div>
                <div class="info-row" v-if="patient.lastVisitDate">
                  <span class="info-label">最近就诊：</span>
                  <span class="info-value">{{ formatDate(patient.lastVisitDate) }}</span>
                </div>
                <div class="info-row" v-if="patient.lastDepartment">
                  <span class="info-label">就诊科室：</span>
                  <span class="info-value">{{ patient.lastDepartment }}</span>
                </div>
                <div class="info-row" v-if="patient.lastDiagnosis">
                  <span class="info-label">诊断：</span>
                  <span class="info-value diagnosis">{{ patient.lastDiagnosis }}</span>
                </div>
              </div>
              <div class="patient-action">
                <el-button type="primary" size="small" link>查看详情 →</el-button>
              </div>
            </div>
          </div>
        </template>

        <!-- BI模式 - 多查询仪表板模式 -->
        <template v-else-if="currentResult.mode !== 'hz360' && currentResult.mode !== 'internetsearch' && currentResult.mode !== 'report' && currentResult.queries && currentResult.queries.length > 0">
          <!-- 下钻控制区：医院筛选 + 维度切换 -->
          <div class="drill-control-bar" v-if="currentResult.detailSql">
            <!-- 医院筛选（医共体多院区）- 优先用后端返回的hospitals，否则从维度数据中获取 -->
            <div class="hospital-filter" v-if="hospitalOptions.length > 0">
              <span class="filter-label">医院筛选：</span>
              <el-select v-model="selectedHospital" placeholder="全部医院" size="small" style="width: 180px" @change="onHospitalChange">
                <el-option label="全部医院" value="" />
                <el-option v-for="h in hospitalOptions" :key="h" :label="h" :value="h" />
              </el-select>
            </div>
            <!-- 维度切换器 -->
            <div class="dimension-switcher" v-if="currentResult.dimensions && currentResult.dimensions.length > 0">
              <span class="filter-label">分析维度：</span>
              <el-radio-group v-model="selectedDimension" size="small" @change="onDimensionChange">
                <!-- 初始维度按钮：恢复原始图表 -->
                <el-radio-button value="__initial__">初始</el-radio-button>
                <el-radio-button v-for="dim in currentResult.dimensions" :key="dim" :value="dim">{{ dim }}</el-radio-button>
              </el-radio-group>
            </div>
            <!-- 度量选择器 -->
            <div class="measure-selector" v-if="currentResult.measures && currentResult.measures.length > 1">
              <span class="filter-label">统计指标：</span>
              <el-select v-model="selectedMeasureIdx" size="small" style="width: 140px" @change="onMeasureChange">
                <el-option v-for="(m, idx) in currentResult.measures" :key="idx" :label="m.alias" :value="idx" />
              </el-select>
            </div>
          </div>

          <!-- KPI卡片区 -->
          <div class="kpi-cards" v-if="kpiQueries.length > 0">
            <div class="kpi-card" v-for="(kpi, idx) in kpiQueries" :key="'kpi-'+idx"
                 :ref="el => setKpiRef(idx, el as HTMLElement)"
                 :class="{ 'kpi-error': kpi.error, 'kpi-clickable': !!currentResult?.detailSql }">
              <!-- 保存按钮（右上角） -->
               <div class="kpi-save-btns">
                 <el-icon class="kpi-save-btn" @click.stop="saveChartToManage(-1 - idx, kpi)" title="保存到图表管理"><FolderAdd /></el-icon>
                 <el-icon class="kpi-save-btn" @click.stop="saveKpiAsImage(idx, kpi.title)" title="保存为图片"><Picture /></el-icon>
               </div>
              <div class="kpi-content" @click="handleKpiClick(kpi, idx)">
                <div class="kpi-value">{{ getKpiValue(kpi) }}</div>
                <div class="kpi-title">{{ kpi.title }}</div>
                <!-- 同比环比展示 -->
                <div class="kpi-compare" v-if="kpi.yoyRate !== undefined || kpi.momRate !== undefined">
                  <span v-if="kpi.yoyRate !== undefined" class="compare-item" :class="getCompareClass(kpi.yoyRate)">
                    同比 <span class="compare-arrow">{{ kpi.yoyRate > 0 ? '↑' : (kpi.yoyRate < 0 ? '↓' : '→') }}</span>{{ Math.abs(kpi.yoyRate) }}%
                  </span>
                  <span v-if="kpi.momRate !== undefined" class="compare-item" :class="getCompareClass(kpi.momRate)">
                    环比 <span class="compare-arrow">{{ kpi.momRate > 0 ? '↑' : (kpi.momRate < 0 ? '↓' : '→') }}</span>{{ Math.abs(kpi.momRate) }}%
                  </span>
                </div>
                <div class="kpi-click-hint" v-if="currentResult?.detailSql">点击查看明细</div>
              </div>
            </div>
          </div>

          <!-- 图表区 -->
          <div class="charts-grid">
            <div v-for="(chart, idx) in chartQueries" :key="'chart-'+idx" class="chart-item">
              <!-- 图表头部工具栏 -->
              <div class="chart-header">
                <!-- 当有下钻数据且是第一个图表时，显示下钻维度标题 -->
                <div class="chart-title">{{ (drillData && idx === 0) ? `按${selectedDimension}分析` : chart.title }}</div>
                <div class="chart-tools">
                  <el-select v-model="chartLimits[idx]" size="small" style="width: 85px" @change="renderCharts">
                    <el-option :value="10" label="前10条" />
                    <el-option :value="20" label="前20条" />
                    <el-option :value="50" label="前50条" />
                    <el-option :value="-1" label="全部" />
                  </el-select>
                  <el-select v-model="chartSortModes[idx]" size="small" style="width: 80px" @change="renderCharts">
                    <el-option value="none" label="不排序" />
                    <el-option value="asc" label="升序" />
                    <el-option value="desc" label="降序" />
                  </el-select>
                  <el-button-group size="small">
                    <el-button :type="chartViewModes[idx] === 'chart' ? 'primary' : 'default'" @click="chartViewModes[idx] = 'chart'">
                      <el-icon><DataLine /></el-icon>
                    </el-button>
                    <el-button :type="chartViewModes[idx] === 'table' ? 'primary' : 'default'" @click="chartViewModes[idx] = 'table'">
                      <el-icon><Grid /></el-icon>
                    </el-button>
                  </el-button-group>
                  <el-button size="small" @click="openFullscreen(idx)"><el-icon><FullScreen /></el-icon></el-button>
                  <el-button size="small" @click="exportTableData(chart, idx)"><el-icon><Download /></el-icon></el-button>
                  <el-button size="small" type="success" @click="saveChartAsImage(idx, chart.title)" title="保存为图片">
                    <el-icon><Picture /></el-icon>
                  </el-button>
                  <el-button size="small" type="primary" :loading="savingChartIdx === idx" @click="saveChartToManage(idx, chart)" title="保存到图表管理">
                    <el-icon><FolderAdd /></el-icon>
                  </el-button>
                </div>
              </div>
              <div v-if="chart.error" class="chart-error">{{ chart.error }}</div>
              <template v-else>
                <!-- 图表视图 -->
                <div v-show="chartViewModes[idx] === 'chart'" :ref="el => setChartRef(idx, el as HTMLElement)" class="chart-container"></div>
                <!-- 表格视图: 当有下钻数据且是第一个图表时，显示下钻数据 -->
                <div v-show="chartViewModes[idx] === 'table'" class="table-container">
                  <el-table :data="getProcessedData((drillData && idx === 0) ? drillData : chart.data, chartLimits[idx], chartSortModes[idx])" border size="small" max-height="320">
                    <el-table-column type="index" label="序号" width="60" />
                    <el-table-column v-for="col in getTableColumns((drillData && idx === 0) ? drillData : chart.data)" :key="col" :prop="col" :label="col" />
                  </el-table>
                </div>
              </template>
            </div>
          </div>
        </template>

        <!-- 单查询模式（兼容旧版） -->
        <template v-else-if="currentResult.data && currentResult.data.length > 0">
          <div class="chart-section">
            <div ref="singleChartRef" class="chart-container"></div>
          </div>
          <div class="table-section">
            <el-table :data="currentResult.data" border size="small" max-height="300">
              <el-table-column v-for="col in dataColumns" :key="col" :prop="col" :label="col" />
            </el-table>
          </div>
        </template>

        <!-- 错误信息 -->
        <el-alert v-if="currentResult.error" :title="currentResult.error" type="error" :closable="false" show-icon />
      </el-card>
    </div>
    
    <!-- SQL预览弹窗 -->
    <el-dialog v-model="showSqlDialog" title="SQL查询语句" width="700px" top="5vh">
      <!-- 报表模式：显示每个页签的SQL -->
      <div class="sql-list" v-if="currentResult?.reportSheets?.length">
        <div v-for="(sheet, idx) in currentResult.reportSheets" :key="idx" class="sql-item">
          <div class="sql-title">{{ sheet.title }}</div>
          <el-input :model-value="sheet.sql || ''" type="textarea" :rows="6" readonly />
        </div>
      </div>
      <!-- BI模式：显示queries的SQL -->
      <div class="sql-list" v-else-if="currentResult?.queries?.length">
        <div v-for="(q, idx) in currentResult.queries" :key="idx" class="sql-item">
          <div class="sql-title">{{ q.title }} ({{ q.type }})</div>
          <el-input :model-value="q.sql" type="textarea" :rows="3" readonly />
        </div>
      </div>
      <!-- 兼容旧版单SQL -->
      <el-input v-else :model-value="currentResult?.sql || ''" type="textarea" :rows="10" readonly />
      <template #footer>
        <el-button @click="showSqlDialog = false">关闭</el-button>
        <el-button type="primary" @click="copySql">复制SQL</el-button>
      </template>
    </el-dialog>

    <!-- 提示词预览弹窗 -->
    <el-dialog v-model="showPromptDialog" title="AI提示词详情" width="900px" top="5vh">
      <div class="prompt-dialog-content">
        <!-- 实时提示词列表（新对话时显示，按阶段分开） -->
        <template v-if="displayPrompts.length > 0">
          <div v-for="(prompt, index) in displayPrompts" :key="index" class="prompt-phase">
            <div class="phase-header">
              <el-tag type="primary">{{ prompt.phase }}</el-tag>
            </div>
            <div class="phase-content">
              <div class="prompt-section">
                <div class="section-title">输入提示词：</div>
                <el-input :model-value="prompt.content" type="textarea" :autosize="{ minRows: 5, maxRows: 15 }" readonly />
              </div>
              <div class="prompt-section" v-if="prompt.response">
                <div class="section-title">AI响应：</div>
                <el-input :model-value="prompt.response" type="textarea" :autosize="{ minRows: 3, maxRows: 10 }" readonly />
              </div>
            </div>
          </div>
        </template>
        <!-- 无内容提示 -->
        <el-empty v-else description="暂无提示词记录" />
      </div>
      <template #footer>
        <el-button @click="showPromptDialog = false">关闭</el-button>
      </template>
    </el-dialog>

    <!-- 全屏图表弹窗 -->
    <el-dialog v-model="showFullscreen" :title="fullscreenChart?.title || '图表详情'" width="95%" top="2vh" class="fullscreen-dialog" destroy-on-close>
      <template v-if="fullscreenChart">
        <div class="fullscreen-tools">
          <el-select v-model="fullscreenLimit" size="small" style="width: 100px" @change="renderFullscreenChart">
            <el-option :value="10" label="前10条" />
            <el-option :value="20" label="前20条" />
            <el-option :value="50" label="前50条" />
            <el-option :value="100" label="前100条" />
            <el-option :value="-1" label="全部" />
          </el-select>
          <el-select v-model="fullscreenSortMode" size="small" style="width: 80px" @change="renderFullscreenChart">
            <el-option value="none" label="不排序" />
            <el-option value="asc" label="升序" />
            <el-option value="desc" label="降序" />
          </el-select>
          <el-button-group size="small">
            <el-button :type="fullscreenViewMode === 'chart' ? 'primary' : 'default'" @click="switchToFullscreenChart()">
              <el-icon><DataLine /></el-icon> 图表
            </el-button>
            <el-button :type="fullscreenViewMode === 'table' ? 'primary' : 'default'" @click="fullscreenViewMode = 'table'">
              <el-icon><Grid /></el-icon> 表格
            </el-button>
          </el-button-group>
          <el-button size="small" type="success" @click="exportFullscreenData"><el-icon><Download /></el-icon> 导出Excel</el-button>
        </div>
        <!-- 图表视图 -->
        <div v-show="fullscreenViewMode === 'chart'" ref="fullscreenChartRef" class="fullscreen-chart-container"></div>
        <!-- 表格视图 -->
        <div v-show="fullscreenViewMode === 'table'" class="fullscreen-table-container">
          <el-table :data="getProcessedData(fullscreenChart.data, fullscreenLimit, fullscreenSortMode)" border stripe max-height="65vh">
            <el-table-column type="index" label="序号" width="60" />
            <el-table-column v-for="col in getTableColumns(fullscreenChart.data)" :key="col" :prop="col" :label="col" />
          </el-table>
        </div>
      </template>
      <template #footer>
        <el-button @click="showFullscreen = false">关闭</el-button>
      </template>
    </el-dialog>

    <!-- 明细数据弹窗（点击图表下钻） -->
    <el-dialog v-model="showDetailDialog" :title="detailTitle" width="90%" top="3vh" class="detail-dialog" destroy-on-close>
      <div class="detail-content" v-loading="detailLoading">
        <!-- 筛选条件显示 -->
        <div class="detail-filter-bar">
          <div class="filter-info">
            <el-tag v-if="detailData?.filterDescription" type="info" size="small">
              下钻条件: {{ detailData.filterDescription }}
            </el-tag>
            <span class="detail-total">
              共 {{ filteredDetailData.length }} 条记录
              <template v-if="filteredDetailData.length !== (detailData?.data?.length || 0)">
                （已筛选，原 {{ detailData?.data?.length || 0 }} 条）
              </template>
            </span>
          </div>
          <!-- 字段快速筛选区域 -->
          <div class="column-filters" v-if="detailData?.columns?.length">
            <el-popover placement="bottom" :width="300" trigger="click">
              <template #reference>
                <el-button size="small" :type="hasColumnFilters ? 'primary' : 'default'">
                  <el-icon><Filter /></el-icon>
                  字段筛选
                  <el-badge v-if="activeFilterCount > 0" :value="activeFilterCount" class="filter-badge" />
                </el-button>
              </template>
              <div class="filter-popover">
                <div class="filter-header">
                  <span>字段筛选</span>
                  <el-button link type="primary" size="small" @click="clearAllColumnFilters">清除全部</el-button>
                </div>
                <el-scrollbar max-height="300px">
                  <div v-for="col in detailData.columns" :key="col" class="filter-item">
                    <div class="filter-label">{{ col }}</div>
                    <el-input
                      v-model="columnFilterValues[col]"
                      size="small"
                      :placeholder="`筛选 ${col}`"
                      clearable
                      @input="onColumnFilterChange"
                    />
                  </div>
                </el-scrollbar>
              </div>
            </el-popover>
          </div>
        </div>

        <!-- 明细表格（带列筛选功能） -->
        <el-table
          :data="paginatedDetailData"
          border
          stripe
          size="small"
          max-height="55vh"
          style="width: 100%"
          @filter-change="handleTableFilterChange"
        >
          <el-table-column type="index" label="序号" width="60" fixed="left" />
          <el-table-column
            v-for="col in detailData?.columns || []"
            :key="col"
            :prop="col"
            :label="col"
            min-width="120"
            show-overflow-tooltip
            sortable
          >
            <template #header>
              <div class="column-header-with-filter">
                <span>{{ col }}</span>
                <el-icon
                  v-if="columnFilterValues[col]"
                  class="filter-active-icon"
                  @click.stop="clearColumnFilter(col)"
                >
                  <CircleClose />
                </el-icon>
              </div>
            </template>
          </el-table-column>
        </el-table>

        <!-- 分页 -->
        <div class="detail-pagination">
          <el-pagination
            v-model:current-page="detailLocalPage"
            v-model:page-size="detailLocalPageSize"
            :page-sizes="[20, 50, 100, 200]"
            :total="filteredDetailData.length"
            layout="total, sizes, prev, pager, next, jumper"
            @size-change="onDetailLocalSizeChange"
            @current-change="onDetailLocalPageChange"
          />
        </div>
      </div>
      <template #footer>
        <el-button @click="showDetailDialog = false">关闭</el-button>
        <el-button type="primary" @click="exportDetailData">导出Excel</el-button>
      </template>
    </el-dialog>

    <!-- 下钻菜单（悬浮式） -->
    <Teleport to="body">
      <div
        v-if="showDrillMenu"
        class="drill-menu-overlay"
        @click="closeDrillMenu"
      >
        <div
          class="drill-menu"
          :style="{ left: drillMenuPosition.x + 'px', top: drillMenuPosition.y + 'px' }"
          @click.stop
        >
          <div class="drill-menu-header">
            <span class="drill-target">{{ drillContext?.dimensionValue }}</span>
            <span class="drill-hint">选择下钻维度</span>
          </div>
          <div class="drill-menu-options">
            <div
              v-for="opt in drillDownOptions"
              :key="opt.key"
              class="drill-option"
              @click="executeDrill(opt)"
            >
              <span class="drill-icon">{{ opt.icon }}</span>
              <span class="drill-label">{{ opt.label }}</span>
            </div>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- PPT生成器弹窗 -->
    <PptGenerator v-model="showPptGenerator" :sessions="groupedSessions.bi" />

    <!-- Word生成器弹窗 -->
    <WordGenerator v-model:visible="showWordGenerator" :sessions="groupedSessions.bi" :datasource-id="selectedDatasourceId || undefined" />

    <!-- 图片预览弹窗 -->
    <el-dialog v-model="showImagePreview" :title="`已保存的图片 - ${previewSessionTitle}`" width="80%" top="5vh">
      <div class="image-preview-container">
        <div v-for="(img, idx) in previewImages" :key="idx" class="image-preview-item">
          <el-image :src="img" :preview-src-list="previewImages" :initial-index="idx" fit="contain" />
          <div class="image-info">
            <div class="image-filename">{{ img.split('/').pop() }}</div>
            <el-popconfirm title="确定删除这张图片吗？" @confirm="handleDeleteImage(img, idx)">
              <template #reference>
                <el-button type="danger" size="small" :icon="Delete" circle />
              </template>
            </el-popconfirm>
          </div>
        </div>
        <el-empty v-if="previewImages.length === 0" description="暂无图片" />
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick, reactive, Teleport, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Promotion, FullScreen, Download, DataLine, Grid, RefreshLeft, Refresh, Search, User, Filter, CircleClose, Document, Delete, Edit, Picture, FolderAdd } from '@element-plus/icons-vue'
import * as echarts from 'echarts'
import html2canvas from 'html2canvas'
import { getDatasourceList } from '@/api/datasource'
import { getConfig } from '@/api/config'
import { aiChat, aiDetail, aiDrill, aiRefresh, getAiTables, getAiSessions, aiReplay, uploadChartImages, deleteAiSession, updateAiSessionTitle, getSessionImages, deleteSessionImage, saveAsChart as saveAsChartApi, type AiChatResponse, type TableInfo, type DrillFilter, type DetailRequest, type DetailResponse, type DrillRequest, type MeasureField, type RefreshRequest, type SessionListItem, type QueryItem } from '@/api/ai'
import VoiceInput from '@/components/VoiceInput.vue'
import VoiceWakeup from '@/components/VoiceWakeup.vue'
import PptGenerator from '@/components/PptGenerator.vue'
import WordGenerator from '@/components/WordGenerator.vue'

// 语音组件引用
const voiceInputRef = ref<InstanceType<typeof VoiceInput> | null>(null)
const voiceWakeupRef = ref<InstanceType<typeof VoiceWakeup> | null>(null)
void voiceWakeupRef // 防止未使用警告（模板中使用）

// PPT/Word生成器弹窗控制
const showPptGenerator = ref(false)
const showWordGenerator = ref(false)

// 数据源相关（从系统配置获取默认数据源）
const datasources = ref<any[]>([])
const selectedDatasourceId = ref<number | null>(null)
const tables = ref<TableInfo[]>([])
const loadingTables = ref(false)

// 时间范围选择
const timeRangeType = ref('thisMonth')  // 默认本月
const dateRange = ref<[string, string] | null>(null)

// 时间范围选项（用于标签显示）
const timeRangeOptions = [
  { value: 'none', label: '不限' },
  { value: 'today', label: '今日' },
  { value: 'yesterday', label: '昨日' },
  { value: 'thisMonth', label: '本月' },
  { value: 'lastMonth', label: '上月' },
  { value: 'thisQuarter', label: '本季' },
  { value: 'lastQuarter', label: '上季' },
  { value: 'thisYear', label: '今年' },
  { value: 'lastYear', label: '去年' },
  { value: 'custom', label: '自定义' }
]

// 选择时间范围
async function onTimeRangeSelect(value: string) {
  timeRangeType.value = value
  if (value !== 'custom') {
    dateRange.value = null
  }

  // ★ 如果当前有结果（历史会话或当前查询），自动刷新数据
  if (currentResult.value?.messageId && currentResult.value?.mode === 'bi') {
    await refreshWithNewTimeRange()
  }
}

// 使用新时间范围刷新当前结果
async function refreshWithNewTimeRange() {
  if (!currentResult.value?.messageId || !selectedDatasourceId.value) return

  const effectiveDateRange = getEffectiveDateRange()
  if (!effectiveDateRange) {
    ElMessage.warning('请选择时间范围')
    return
  }

  loading.value = true
  try {
    const res = await aiReplay({
      messageId: currentResult.value.messageId,
      datasourceId: selectedDatasourceId.value,
      startDate: effectiveDateRange[0],
      endDate: effectiveDateRange[1]
    })

    if (res.code === 0 && res.data) {
      // 更新结果，保持其他状态
      currentResult.value = res.data
      await nextTick()
      renderCharts()
      ElMessage.success(`已刷新为 ${effectiveDateRange[0]} 至 ${effectiveDateRange[1]} 的数据`)
    } else {
      ElMessage.error(res.message || '刷新失败')
    }
  } catch (e: any) {
    console.error('刷新时间范围失败', e)
    ElMessage.error('刷新时间范围失败')
  } finally {
    loading.value = false
  }
}

// 自定义日期范围变化时刷新
async function onCustomDateRangeChange() {
  // 仅当有选择日期且有当前结果时刷新
  if (dateRange.value && dateRange.value[0] && dateRange.value[1]) {
    if (currentResult.value?.messageId && currentResult.value?.mode === 'bi') {
      await refreshWithNewTimeRange()
    }
  }
}

// 根据时间范围类型计算实际日期
function getDateRangeByType(type: string): [string, string] | null {
  const now = new Date()
  // ★ 修复：使用本地日期格式化，避免toISOString导致的UTC时区偏移
  const formatDate = (d: Date) => {
    const year = d.getFullYear()
    const month = String(d.getMonth() + 1).padStart(2, '0')
    const day = String(d.getDate()).padStart(2, '0')
    return `${year}-${month}-${day}`
  }

  switch (type) {
    case 'none':
      return null
    case 'today': {
      return [formatDate(now), formatDate(now)]
    }
    case 'yesterday': {
      const yesterday = new Date(now)
      yesterday.setDate(yesterday.getDate() - 1)
      return [formatDate(yesterday), formatDate(yesterday)]
    }
    case 'thisMonth': {
      const start = new Date(now.getFullYear(), now.getMonth(), 1)
      const end = new Date(now.getFullYear(), now.getMonth() + 1, 0)
      return [formatDate(start), formatDate(end)]
    }
    case 'lastMonth': {
      const start = new Date(now.getFullYear(), now.getMonth() - 1, 1)
      const end = new Date(now.getFullYear(), now.getMonth(), 0)
      return [formatDate(start), formatDate(end)]
    }
    case 'thisQuarter': {
      const quarter = Math.floor(now.getMonth() / 3)
      const start = new Date(now.getFullYear(), quarter * 3, 1)
      const end = new Date(now.getFullYear(), quarter * 3 + 3, 0)
      return [formatDate(start), formatDate(end)]
    }
    case 'lastQuarter': {
      const quarter = Math.floor(now.getMonth() / 3) - 1
      const year = quarter < 0 ? now.getFullYear() - 1 : now.getFullYear()
      const q = quarter < 0 ? 3 : quarter
      const start = new Date(year, q * 3, 1)
      const end = new Date(year, q * 3 + 3, 0)
      return [formatDate(start), formatDate(end)]
    }
    case 'thisYear': {
      const start = new Date(now.getFullYear(), 0, 1)
      const end = new Date(now.getFullYear(), 11, 31)
      return [formatDate(start), formatDate(end)]
    }
    case 'lastYear': {
      const start = new Date(now.getFullYear() - 1, 0, 1)
      const end = new Date(now.getFullYear() - 1, 11, 31)
      return [formatDate(start), formatDate(end)]
    }
    case 'custom':
      return dateRange.value
    default:
      return null
  }
}

// 时间范围类型变化时重置自定义日期（保留以备将来使用）
function _onTimeRangeTypeChange() {
  if (timeRangeType.value !== 'custom') {
    dateRange.value = null
  }
}
void _onTimeRangeTypeChange // 防止未使用警告

// 获取当前有效的时间范围
function getEffectiveDateRange(): [string, string] | null {
  return getDateRangeByType(timeRangeType.value)
}

// 对话相关
const question = ref('')
const loading = ref(false)
const chatHistory = ref<{ role: 'user' | 'assistant'; content: string }[]>([])
const currentResult = ref<AiChatResponse | null>(null)
const sessionId = ref<string>('')

// 会话历史列表
const sessionList = ref<SessionListItem[]>([])
const loadingHistory = ref(false)
const historySearch = ref('')  // 历史搜索关键词
const historyTab = ref('bi')  // 当前选中的历史标签页

// 过滤后的会话列表
const filteredSessionList = computed(() => {
  if (!historySearch.value.trim()) {
    return sessionList.value
  }
  const keyword = historySearch.value.trim().toLowerCase()
  return sessionList.value.filter(s => s.title.toLowerCase().includes(keyword))
})

// 按模式分组的会话列表
const groupedSessions = computed(() => {
  const filtered = filteredSessionList.value
  return {
    bi: filtered.filter(s => s.mode === 'bi' || !s.mode),
    hz360: filtered.filter(s => s.mode === 'hz360'),
    internetsearch: filtered.filter(s => s.mode === 'internetsearch'),
    report: filtered.filter(s => s.mode === 'report')
  }
})

const currentTabSessions = computed(() => {
  const tab = historyTab.value as 'bi' | 'hz360' | 'internetsearch' | 'report'
  return groupedSessions.value[tab] || []
})

// 标签页切换
function onHistoryTabChange(_tab: string | number) {
  // 可以在这里添加切换逻辑，比如记录用户偏好
}

// 图表相关
const singleChartRef = ref<HTMLElement | null>(null)  // 单查询模式的图表
const chartRefs: Record<number, HTMLElement> = {}  // 多查询模式的图表refs
const kpiRefs: Record<number, HTMLElement> = {}  // KPI卡片的refs（用于html2canvas截图）
let chartInstances: echarts.ECharts[] = []  // 多图表实例

// 图表显示控制（每个图表的显示条数限制）
const chartLimits = reactive<Record<number, number>>({})  // 每个图表的条数限制
const chartViewModes = reactive<Record<number, 'chart' | 'table'>>({})  // 每个图表的视图模式
const chartSortModes = reactive<Record<number, 'none' | 'asc' | 'desc'>>({})  // 每个图表的排序模式

// 全屏弹窗
const showFullscreen = ref(false)
const fullscreenChart = ref<any>(null)
const fullscreenChartIdx = ref<number>(-1)  // 全屏图表的原始索引，用于下钻
const fullscreenChartRef = ref<HTMLElement | null>(null)
const fullscreenLimit = ref(-1)  // 全屏时的条数限制
const fullscreenViewMode = ref<'chart' | 'table'>('chart')
const fullscreenSortMode = ref<'none' | 'asc' | 'desc'>('none')  // 全屏排序模式
let fullscreenChartInstance: echarts.ECharts | null = null

// 弹窗控制
const showSqlDialog = ref(false)
const showPromptDialog = ref(false)

// 图片预览相关
const showImagePreview = ref(false)  // 图片预览弹窗
const previewImages = ref<string[]>([])  // 预览的图片URL列表
const previewSessionTitle = ref('')  // 预览的会话标题

// 流式输出相关
const isStreaming = ref(false)  // 是否正在流式接收
const streamingAnswer = ref('')  // 流式接收的回答内容

// 下钻功能相关
interface DrillContext {
  chartIdx: number       // 当前图表索引
  chartTitle: string     // 图表标题
  dimension: string      // 当前点击的维度字段名
  dimensionValue: string // 当前点击的维度值
  measureField: string   // 度量字段
}
const showDrillMenu = ref(false)  // 下钻菜单显示
const drillMenuPosition = ref({ x: 0, y: 0 })  // 菜单位置
const drillContext = ref<DrillContext | null>(null)  // 当前下钻上下文
const drillBreadcrumbs = ref<{ label: string; question: string; filters?: DrillFilter[] }[]>([])  // 面包屑导航
const originalQuestion = ref('')  // 原始问题

// 报表模式相关状态
const activeReportSheet = ref('0')  // 当前激活的报表页签索引
const reportTableMaxHeight = ref(600)  // 报表表格最大高度

// 当前报表页签数据
const currentReportSheet = computed(() => {
  if (!currentResult.value?.reportSheets?.length) return null
  const idx = parseInt(activeReportSheet.value)
  return currentResult.value.reportSheets[idx] || currentResult.value.reportSheets[0]
})

// 报表合计行方法
function getReportSummary({ columns, data }: { columns: any[], data: Record<string, any>[] }) {
  if (!currentReportSheet.value?.summaryRow) {
    const sums: string[] = []
    columns.forEach((col, index) => {
      if (index === 0) { sums.push('合计'); return }
      const field = col.property
      const colDef = currentReportSheet.value?.columns.find(c => c.field === field)
      if (colDef?.dataType === 'number') {
        const total = data.reduce((sum, row) => {
          const val = Number(row[field])
          return isNaN(val) ? sum : sum + val
        }, 0)
        sums.push(formatReportNumber(total))
      } else {
        sums.push('')
      }
    })
    return sums
  }
  const summary = currentReportSheet.value!.summaryRow!
  const sums: string[] = []
  columns.forEach((col, index) => {
    if (index === 0) { sums.push('合计'); return }
    const field = col.property
    const val = summary[field]
    const colDef = currentReportSheet.value?.columns.find(c => c.field === field)
    if (colDef?.dataType === 'number') {
      sums.push(formatReportNumber(val))
    } else {
      sums.push(val != null ? String(val) : '')
    }
  })
  return sums
}

// 格式化报表数字
function formatReportNumber(val: any): string {
  if (val == null || val === '') return '-'
  const num = Number(val)
  if (isNaN(num)) return String(val)
  if (Number.isInteger(num)) return num.toLocaleString('zh-CN')
  return num.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// 导出报表为Excel（CSV格式）
function exportReportExcel() {
  const sheet = currentReportSheet.value
  if (!sheet) return
  const cols = sheet.columns
  const rows = sheet.rows
  const BOM = '\uFEFF'
  const header = cols.map(c => c.title).join(',')
  const dataLines = rows.map(row => cols.map(c => {
    const val = row[c.field]
    if (val == null) return ''
    const str = String(val)
    return str.includes(',') || str.includes('"') || str.includes('\n')
      ? `"${str.replace(/"/g, '""')}"`
      : str
  }).join(','))
  let csv = BOM + header + '\n' + dataLines.join('\n')
  if (sheet.summaryRow) {
    const summaryLine = cols.map(c => {
      const val = sheet.summaryRow![c.field]
      if (val == null) return ''
      return String(val)
    }).join(',')
    csv += '\n' + summaryLine
  }
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const link = document.createElement('a')
  link.href = URL.createObjectURL(blob)
  link.download = `${sheet.title || '报表'}_${new Date().toISOString().slice(0, 10)}.csv`
  link.click()
  URL.revokeObjectURL(link.href)
}

// 打印报表
function printReport() {
  const sheet = currentReportSheet.value
  if (!sheet) return
  const cols = sheet.columns
  const rows = sheet.rows
  let html = `<html><head><title>${sheet.title}</title>
    <style>
      body { font-family: "Microsoft YaHei", sans-serif; padding: 20px; }
      h2 { text-align: center; }
      table { border-collapse: collapse; width: 100%; }
      th, td { border: 1px solid #333; padding: 6px 10px; text-align: left; font-size: 13px; }
      th { background: #f0f0f0; font-weight: bold; }
      .number { text-align: right; }
      .summary-row { font-weight: bold; background: #f5f7fa; }
    </style></head><body><h2>${sheet.title}</h2><table>`
  html += '<tr>' + cols.map(c => `<th>${c.title}</th>`).join('') + '</tr>'
  rows.forEach(row => {
    html += '<tr>' + cols.map(c => {
      const cls = c.dataType === 'number' ? ' class="number"' : ''
      return `<td${cls}>${formatReportNumber(row[c.field])}</td>`
    }).join('') + '</tr>'
  })
  if (sheet.summaryRow) {
    html += '<tr class="summary-row">' + cols.map(c => {
      const val = sheet.summaryRow![c.field]
      const cls = c.dataType === 'number' ? ' class="number"' : ''
      return `<td${cls}>${formatReportNumber(val)}</td>`
    }).join('') + '</tr>'
  }
  html += '</table></body></html>'
  const win = window.open('', '_blank')
  if (win) {
    win.document.write(html)
    win.document.close()
    win.print()
  }
}

// 新版下钻控制状态
const selectedHospital = ref<string>('')  // 选中的医院（医共体筛选）
const selectedDimension = ref<string>('')  // 选中的下钻维度
const selectedMeasureIdx = ref<number>(0)  // 选中的度量指标索引
const drillFilters = ref<DrillFilter[]>([])  // 当前下钻过滤条件
const drillLoading = ref(false)  // 下钻加载状态
const drillData = ref<Record<string, any>[] | null>(null)  // 下钻结果数据
const originalQueries = ref<any[] | null>(null)  // 保存原始queries，用于恢复

// 明细数据弹窗状态
const showDetailDialog = ref(false)  // 明细弹窗显示
const detailLoading = ref(false)  // 明细加载状态
const detailData = ref<DetailResponse | null>(null)  // 明细数据
const detailFilters = ref<DrillFilter[]>([])  // 当前明细筛选条件（后端筛选）
const detailPage = ref(1)  // 后端分页-当前页
const detailPageSize = ref(100000)  // 后端分页-每次获取条数（放大到10万条，支持大数据量）
const detailTitle = ref('')  // 明细弹窗标题

// 明细表格前端筛选和分页状态
const columnFilterValues = ref<Record<string, string>>({})  // 各列的筛选值
const detailLocalPage = ref(1)  // 前端分页-当前页
const detailLocalPageSize = ref(50)  // 前端分页-每页条数

// 计算：是否有列筛选条件
const hasColumnFilters = computed(() => {
  return Object.values(columnFilterValues.value).some(v => v && v.trim())
})

// 计算：激活的筛选条件数量
const activeFilterCount = computed(() => {
  return Object.values(columnFilterValues.value).filter(v => v && v.trim()).length
})

// 计算：根据列筛选条件过滤后的数据
const filteredDetailData = computed(() => {
  if (!detailData.value?.data) return []
  const data = detailData.value.data

  // 如果没有筛选条件，返回全部数据
  if (!hasColumnFilters.value) return data

  // 应用所有列筛选条件
  return data.filter(row => {
    return Object.entries(columnFilterValues.value).every(([col, filterValue]) => {
      if (!filterValue || !filterValue.trim()) return true
      const cellValue = row[col]
      if (cellValue === null || cellValue === undefined) return false
      // 不区分大小写的包含匹配
      return String(cellValue).toLowerCase().includes(filterValue.toLowerCase().trim())
    })
  })
})

// 计算：前端分页后的数据
const paginatedDetailData = computed(() => {
  const start = (detailLocalPage.value - 1) * detailLocalPageSize.value
  const end = start + detailLocalPageSize.value
  return filteredDetailData.value.slice(start, end)
})

// 下钻维度选项（根据业务定义）
const drillDownOptions = [
  { key: 'time', label: '按时间趋势', icon: '📅', prompt: '按月份的时间趋势' },
  { key: 'doctor', label: '按医生分析', icon: '👨‍⚕️', prompt: '按医生' },
  { key: 'department', label: '按科室分析', icon: '🏥', prompt: '按科室' },
  { key: 'diagnosis', label: '按诊断分析', icon: '📋', prompt: '按诊断' },
  { key: 'patient', label: '按患者分析', icon: '👤', prompt: '按患者' },
]

// 计算数据列（单查询模式用）
const dataColumns = computed(() => {
  if (!currentResult.value?.data || currentResult.value.data.length === 0) return []
  return Object.keys(currentResult.value.data[0])
})

// 过滤出KPI查询
const kpiQueries = computed(() => {
  return currentResult.value?.queries?.filter(q => q.type === 'kpi') || []
})

// 过滤出图表查询
const chartQueries = computed(() => {
  return currentResult.value?.queries?.filter(q => q.type !== 'kpi') || []
})

// ★ 识别医院相关的维度字段（从dimensions中查找）
const hospitalDimension = computed(() => {
  const dims = currentResult.value?.dimensions || []
  // 常见的医院字段名模式
  const hospitalPatterns = ['医院', '机构', '院区', 'hospital', 'org', 'institution']
  for (const dim of dims) {
    const lowerDim = dim.toLowerCase()
    if (hospitalPatterns.some(p => lowerDim.includes(p))) {
      return dim
    }
  }
  return null
})

// ★ 从维度图表数据中获取医院列表（如果没有hospitals但有医院维度）
const hospitalOptions = computed(() => {
  // 调试日志
  console.log('[医院筛选] 检查医院选项:', {
    hospitals: currentResult.value?.hospitals,
    hospitalField: currentResult.value?.hospitalField,
    dimensions: currentResult.value?.dimensions,
    hospitalDimension: hospitalDimension.value,
    detailSql: currentResult.value?.detailSql ? '有' : '无'
  })

  // 优先使用后端返回的hospitals
  if (currentResult.value?.hospitals && currentResult.value.hospitals.length > 0) {
    console.log('[医院筛选] 使用后端返回的hospitals:', currentResult.value.hospitals)
    return currentResult.value.hospitals
  }
  // 如果有医院维度，从图表数据中获取值
  if (hospitalDimension.value) {
    const dimField = hospitalDimension.value
    // 从所有图表数据中收集医院值
    const hospitals = new Set<string>()
    for (const query of chartQueries.value) {
      if (query.data && query.data.length > 0) {
        for (const row of query.data) {
          if (row[dimField]) {
            hospitals.add(String(row[dimField]))
          }
        }
      }
    }
    const result = Array.from(hospitals).sort()
    console.log('[医院筛选] 从图表数据中提取:', result, '字段名:', dimField)
    return result
  }
  console.log('[医院筛选] 无法获取医院列表')
  return []
})

// ★ 有效的医院字段名（用于筛选）
const effectiveHospitalField = computed(() => {
  // 优先使用后端返回的hospitalField
  if (currentResult.value?.hospitalField) {
    return currentResult.value.hospitalField
  }
  // 否则使用识别出的医院维度
  return hospitalDimension.value
})

// 计算显示的提示词列表（将promptText解析成分阶段格式）
const displayPrompts = computed(() => {
  // 如果有prompts数组，直接使用
  if (currentResult.value?.prompts?.length) {
    return currentResult.value.prompts
  }

  // 如果只有promptText，解析成分阶段格式
  if (currentResult.value?.promptText) {
    const text = currentResult.value.promptText
    const prompts: { phase: string; content: string; response?: string }[] = []

    // 解析格式：[System]\n{systemPrompt}\n\n[User]\n{question}
    const systemMatch = text.match(/\[System\]\s*([\s\S]*?)(?=\n\n\[User\]|\n\[User\]|$)/i)
    const userMatch = text.match(/\[User\]\s*([\s\S]*?)$/i)

    if (systemMatch || userMatch) {
      prompts.push({
        phase: 'SQL生成（历史记录）',
        content: text,
        response: currentResult.value.answer || ''
      })
    } else {
      // 无法解析，作为完整提示词显示
      prompts.push({
        phase: '完整提示词（历史记录）',
        content: text,
        response: currentResult.value.answer || ''
      })
    }

    return prompts
  }

  return []
})

// 设置图表ref
function setChartRef(idx: number, el: HTMLElement | null) {
  if (el) {
    chartRefs[idx] = el
  }
}

// 设置KPI卡片ref
function setKpiRef(idx: number, el: HTMLElement | null) {
  if (el) {
    kpiRefs[idx] = el
  }
}

// 获取表格列名
function getTableColumns(data: any[] | undefined): string[] {
  if (!data || data.length === 0) return []
  return Object.keys(data[0])
}

// 获取限制后的数据
function getLimitedData(data: any[] | undefined, limit: number): any[] {
  if (!data) return []
  if (limit === -1 || limit >= data.length) return data
  return data.slice(0, limit)
}

// 初始化图表显示设置
function initChartSettings() {
  chartQueries.value.forEach((_, idx) => {
    if (chartLimits[idx] === undefined) chartLimits[idx] = 20
    if (chartViewModes[idx] === undefined) chartViewModes[idx] = 'chart'
    if (chartSortModes[idx] === undefined) chartSortModes[idx] = 'none'
  })
}

// 判断字段是否为日期/时间类型
function isDateField(fieldName: string, sampleValue: any): boolean {
  // 根据字段名判断
  const dateKeywords = ['日期', '时间', 'date', 'time', '年', '月', '周', 'year', 'month', 'week', 'day']
  if (dateKeywords.some(kw => fieldName.toLowerCase().includes(kw))) {
    return true
  }
  // 根据值格式判断（ISO日期格式）
  if (typeof sampleValue === 'string') {
    // 匹配 YYYY-MM-DD 或 YYYY-MM-DDTHH:mm:ss 格式
    const datePattern = /^\d{4}-\d{2}-\d{2}/
    if (datePattern.test(sampleValue)) return true
  }
  return false
}

// 格式化X轴值（去掉日期时间中的T00:00:00部分）
function formatXAxisValue(value: any): string {
  const str = String(value)
  // 匹配 YYYY-MM-DDTHH:mm:ss 格式，只保留日期部分
  if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(str)) {
    return str.split('T')[0]  // 只保留 YYYY-MM-DD
  }
  // 匹配 YYYY-MM-DD HH:mm:ss 格式（带空格）
  if (/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}/.test(str)) {
    return str.split(' ')[0]  // 只保留 YYYY-MM-DD
  }
  return str
}

// 对数据进行排序（按数值字段或时间字段）
function getSortedData(data: any[] | undefined, sortMode: 'none' | 'asc' | 'desc'): any[] {
  if (!data || data.length === 0 || sortMode === 'none') return data || []

  const keys = Object.keys(data[0])
  const dimensionField = keys[0]  // 第一列是维度
  const valueField = keys.length > 1 ? keys[1] : keys[0]  // 第二列是数值

  // 检测是否是日期类型字段 - 日期字段按日期排序
  const isDateDimension = isDateField(dimensionField, data[0][dimensionField])

  if (isDateDimension) {
    // 日期类型按时间顺序排序
    const sorted = [...data].sort((a, b) => {
      const da = new Date(a[dimensionField]).getTime() || 0
      const db = new Date(b[dimensionField]).getTime() || 0
      return sortMode === 'asc' ? da - db : db - da
    })
    return sorted
  } else {
    // 非日期类型按数值排序
    const sorted = [...data].sort((a, b) => {
      const va = Number(a[valueField]) || 0
      const vb = Number(b[valueField]) || 0
      return sortMode === 'asc' ? va - vb : vb - va
    })
    return sorted
  }
}

// 获取处理后的数据（日期类型始终按时间顺序，非日期类型可按数值排序）
function getProcessedData(data: any[] | undefined, limit: number, sortMode: 'none' | 'asc' | 'desc'): any[] {
  if (!data || data.length === 0) return []

  const keys = Object.keys(data[0])
  const dimensionField = keys[0]
  const isDateDimension = isDateField(dimensionField, data[0][dimensionField])

  // 日期类型：即使sortMode是none也按时间顺序排序
  if (isDateDimension && sortMode === 'none') {
    const sorted = [...data].sort((a, b) => {
      const da = new Date(a[dimensionField]).getTime() || 0
      const db = new Date(b[dimensionField]).getTime() || 0
      return da - db  // 默认升序（从早到晚）
    })
    return getLimitedData(sorted, limit)
  }

  const sorted = getSortedData(data, sortMode)
  return getLimitedData(sorted, limit)
}

// 打开全屏弹窗
function openFullscreen(idx: number) {
  const originalChart = chartQueries.value[idx]

  // 保存图表索引，用于下钻时使用
  fullscreenChartIdx.value = idx

  // 当有下钻数据且是第一个图表时，使用下钻数据
  if (drillData.value && idx === 0) {
    fullscreenChart.value = {
      ...originalChart,
      title: `按${selectedDimension.value}分析`,  // 使用下钻维度标题
      data: drillData.value,  // 使用下钻数据
      type: isDateField(selectedDimension.value, drillData.value[0]?.[selectedDimension.value]) ? 'line' : 'bar'
    }
  } else {
    fullscreenChart.value = originalChart
  }

  fullscreenLimit.value = chartLimits[idx] || -1
  fullscreenSortMode.value = chartSortModes[idx] || 'none'
  fullscreenViewMode.value = 'chart'
  showFullscreen.value = true
  nextTick(() => {
    renderFullscreenChart()
  })
}

// 渲染全屏图表
function renderFullscreenChart() {
  if (!fullscreenChartRef.value || !fullscreenChart.value) return

  if (fullscreenChartInstance) {
    fullscreenChartInstance.dispose()
  }

  const chart = fullscreenChart.value
  const data = getProcessedData(chart.data, fullscreenLimit.value, fullscreenSortMode.value)
  if (!data || data.length === 0) return

  fullscreenChartInstance = echarts.init(fullscreenChartRef.value)

  const chartType = chart.type as string
  const keys = Object.keys(data[0])
  const xField = keys[0]
  const yField = keys.length > 1 ? keys[1] : keys[0]

  // 使用formatXAxisValue格式化X轴数据（去掉时间部分）
  const xData = data.map((row: any) => formatXAxisValue(row[xField]))
  const yData = data.map((row: any) => Number(row[yField]) || 0)

  // 医疗蓝色系配色
  const medicalBlueColors = ['#1e88e5', '#1565c0', '#0097a7', '#3949ab', '#039be5', '#00838f', '#283593']

  const option: echarts.EChartsOption = {
    title: { text: chart.title, left: 'center', textStyle: { fontSize: 18 } },
    color: medicalBlueColors,
    tooltip: { trigger: chartType === 'pie' ? 'item' : 'axis' },
    grid: { top: 60, bottom: xData.some((x: string) => x.length > 4) ? 80 : 40, left: 60, right: 40 },
    xAxis: chartType === 'pie' ? undefined : {
      type: 'category',
      data: xData,
      axisLabel: { rotate: xData.length > 10 ? 45 : 0, fontSize: 12 }
    },
    yAxis: chartType === 'pie' ? undefined : { type: 'value' },
    series: [{
      type: chartType as any,
      data: chartType === 'pie'
        ? data.map((_: any, i: number) => ({ name: xData[i], value: yData[i] }))
        : yData,
      label: chartType === 'pie' ? { show: true, formatter: '{b}: {d}%' } : undefined,
      itemStyle: {
        borderRadius: chartType === 'bar' ? [4, 4, 0, 0] : undefined,
        color: chartType !== 'pie' ? '#1e88e5' : undefined  // 医疗蓝
      }
    }]
  }

  fullscreenChartInstance.setOption(option)

  // ★ 绑定点击事件，支持全屏图表下钻
  fullscreenChartInstance.off('click')  // 先移除旧的监听
  fullscreenChartInstance.on('click', (params) => {
    handleChartClick(params, fullscreenChartIdx.value, fullscreenChart.value)
  })
}

// 切换到全屏图表视图（需要等待DOM渲染后再绘制图表）
function switchToFullscreenChart() {
  fullscreenViewMode.value = 'chart'
  nextTick(() => {
    // 延迟一点确保v-show切换完成
    setTimeout(() => {
      renderFullscreenChart()
    }, 50)
  })
}

// 导出表格数据为Excel
function exportTableData(chart: any, idx: number) {
  const data = getLimitedData(chart.data, chartLimits[idx] || -1)
  exportToExcel(data, chart.title)
}

// 导出全屏数据
function exportFullscreenData() {
  if (!fullscreenChart.value) return
  const data = getLimitedData(fullscreenChart.value.data, fullscreenLimit.value)
  exportToExcel(data, fullscreenChart.value.title)
}

// 导出明细数据
function exportDetailData() {
  if (!detailData.value?.data || detailData.value.data.length === 0) {
    ElMessage.warning('没有可导出的数据')
    return
  }
  exportToExcel(detailData.value.data, detailTitle.value || '明细数据')
}

// 通用导出Excel函数
function exportToExcel(data: any[], title: string) {
  if (!data || data.length === 0) {
    ElMessage.warning('没有数据可导出')
    return
  }

  const columns = Object.keys(data[0])
  const header = columns.join(',')
  const rows = data.map(row => columns.map(col => {
    const val = row[col]
    // 处理包含逗号或换行的值
    if (typeof val === 'string' && (val.includes(',') || val.includes('\n') || val.includes('"'))) {
      return `"${val.replace(/"/g, '""')}"`
    }
    return val ?? ''
  }).join(','))

  const csvContent = '\uFEFF' + header + '\n' + rows.join('\n')  // BOM for UTF-8
  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' })
  const link = document.createElement('a')
  link.href = URL.createObjectURL(blob)
  link.download = `${title}_${new Date().toLocaleDateString()}.csv`
  link.click()
  URL.revokeObjectURL(link.href)
  ElMessage.success('导出成功')
}

// ==================== 下钻功能 ====================

// 处理图表点击事件 - 直接弹出明细数据
function handleChartClick(params: any, _chartIdx: number, query: any) {
  if (!params.name || !currentResult.value) return

  const data = query.data
  if (!data || data.length === 0) return

  // 检查是否支持明细查询
  if (!currentResult.value.detailSql) {
    ElMessage.info('该查询不支持下钻到明细数据')
    return
  }

  const keys = Object.keys(data[0])
  const dimensionField = keys[0]  // 第一个字段是维度

  // 构建筛选条件
  const filters: DrillFilter[] = []

  // 添加当前点击的维度筛选
  filters.push({
    field: dimensionField,
    op: '=',
    value: String(params.name)
  })

  // 如果有医院筛选，也添加进去
  if (selectedHospital.value && effectiveHospitalField.value) {
    filters.push({
      field: effectiveHospitalField.value,
      op: '=',
      value: selectedHospital.value
    })
  }

  // 设置弹窗标题
  detailTitle.value = `${dimensionField}: ${params.name} 的明细数据`
  detailFilters.value = filters
  detailPage.value = 1

  // 打开弹窗并加载数据
  showDetailDialog.value = true
  loadDetailData()
}

// 处理KPI卡片点击 - 弹出明细数据
function handleKpiClick(kpi: any, _idx: number) {
  if (!currentResult.value?.detailSql) {
    ElMessage.info('该查询不支持下钻到明细数据')
    return
  }

  // KPI卡片点击时，不加维度筛选，只加医院筛选（如果有的话）
  const filters: DrillFilter[] = []

  // 如果有医院筛选，添加进去
  if (selectedHospital.value && effectiveHospitalField.value) {
    filters.push({
      field: effectiveHospitalField.value,
      op: '=',
      value: selectedHospital.value
    })
  }

  // 设置弹窗标题
  detailTitle.value = `${kpi.title} - 明细数据`
  detailFilters.value = filters
  detailPage.value = 1

  // 打开弹窗并加载数据
  showDetailDialog.value = true
  loadDetailData()
}

// 执行下钻
async function executeDrill(drillOption: typeof drillDownOptions[0]) {
  if (!drillContext.value) return

  const ctx = drillContext.value
  showDrillMenu.value = false

  // 保存原始问题（如果是第一次下钻）
  if (drillBreadcrumbs.value.length === 0 && originalQuestion.value === '') {
    originalQuestion.value = chatHistory.value.filter(m => m.role === 'user').pop()?.content || ''
    drillBreadcrumbs.value.push({ label: '全部', question: originalQuestion.value })
  }

  // 构造下钻问题
  const drillQuestion = buildDrillQuestion(ctx, drillOption)

  // 添加面包屑
  drillBreadcrumbs.value.push({
    label: `${ctx.dimensionValue} - ${drillOption.label}`,
    question: drillQuestion
  })

  // 发送下钻查询
  question.value = drillQuestion
  await sendQuestion()
}

// 构造下钻问题
function buildDrillQuestion(ctx: DrillContext, drillOption: typeof drillDownOptions[0]): string {
  // 基于当前图表标题和点击的维度值构造新问题
  const baseAnalysis = ctx.chartTitle.replace(/^按.*?的/, '').replace(/统计/, '').replace(/分析/, '').trim()

  let drillQuestion = ''

  // 根据下钻类型构造问题
  switch (drillOption.key) {
    case 'time':
      drillQuestion = `统计${ctx.dimensionValue}${drillOption.prompt}的${baseAnalysis || '数据'}`
      break
    case 'doctor':
      drillQuestion = `统计${ctx.dimensionValue}${drillOption.prompt}的${baseAnalysis || '数据'}`
      break
    case 'department':
      drillQuestion = `统计${ctx.dimensionValue}${drillOption.prompt}的${baseAnalysis || '数据'}`
      break
    case 'diagnosis':
      drillQuestion = `统计${ctx.dimensionValue}${drillOption.prompt}的${baseAnalysis || '数据'}`
      break
    case 'patient':
      drillQuestion = `统计${ctx.dimensionValue}${drillOption.prompt}的${baseAnalysis || '详细数据'}`
      break
    default:
      drillQuestion = `分析${ctx.dimensionValue}的详细${baseAnalysis || '数据'}`
  }

  // 如果有时间范围，追加
  const effectiveDateRange = getEffectiveDateRange()
  if (effectiveDateRange && effectiveDateRange[0] && effectiveDateRange[1]) {
    drillQuestion += `（时间范围：${effectiveDateRange[0]} 至 ${effectiveDateRange[1]}）`
  }

  return drillQuestion
}

// 面包屑导航跳转
async function navigateBreadcrumb(index: number) {
  if (index < 0 || index >= drillBreadcrumbs.value.length - 1) return

  // 截断面包屑到指定位置
  const targetBreadcrumb = drillBreadcrumbs.value[index]
  drillBreadcrumbs.value = drillBreadcrumbs.value.slice(0, index + 1)

  // 发送对应的问题
  question.value = targetBreadcrumb.question
  await sendQuestion()
}

// 重置下钻（返回最初状态）
function resetDrill() {
  if (originalQuestion.value) {
    question.value = originalQuestion.value
    drillBreadcrumbs.value = []
    originalQuestion.value = ''
    sendQuestion()
  }
}

// 关闭下钻菜单
function closeDrillMenu() {
  showDrillMenu.value = false
  drillContext.value = null
}

// ==================== 新版下钻功能（基于明细SQL） ====================

// 医院筛选变化 - 刷新所有KPI和图表
async function onHospitalChange() {
  // 如果没有选择医院（选择"全部"），恢复原始数据
  if (!selectedHospital.value) {
    // 恢复原始queries数据
    if (currentResult.value && originalQueries.value) {
      currentResult.value.queries = [...originalQueries.value]
    }
    await nextTick()
    renderCharts()
    return
  }

  // 调用刷新API，带医院筛选条件
  await refreshQueriesWithFilter()
}

// 刷新KPI和图表（带筛选条件）
async function refreshQueriesWithFilter() {
  if (!currentResult.value?.messageId || !selectedDatasourceId.value) return

  loading.value = true
  try {
    // 构建筛选条件
    const filters: DrillFilter[] = []
    if (selectedHospital.value && effectiveHospitalField.value) {
      filters.push({
        field: effectiveHospitalField.value,
        op: '=',
        value: selectedHospital.value
      })
    }

    // 获取当前时间范围（用于计算同比环比）
    const effectiveDateRange = getEffectiveDateRange()

    const req: RefreshRequest = {
      messageId: currentResult.value.messageId,
      datasourceId: selectedDatasourceId.value,
      filters: filters.length > 0 ? filters : undefined,
      // 传递时间范围用于同比环比计算
      startDate: effectiveDateRange?.[0] || undefined,
      endDate: effectiveDateRange?.[1] || undefined
    }

    const res = await aiRefresh(req)
    if (res.code === 0 && res.data?.queries) {
      // 保存原始queries（如果还没保存的话）
      if (!originalQueries.value && currentResult.value.queries) {
        originalQueries.value = [...currentResult.value.queries]
      }
      // 更新当前queries
      currentResult.value.queries = res.data.queries
      await nextTick()
      renderCharts()
    } else {
      ElMessage.error(res.message || '刷新查询失败')
    }
  } catch (e: any) {
    ElMessage.error(e.message || '刷新查询失败')
  } finally {
    loading.value = false
  }
}

// 静默刷新KPI计算同比环比（不显示loading，不显示错误）
async function refreshQueriesForYoYMoM() {
  if (!currentResult.value?.messageId || !selectedDatasourceId.value) {
    console.log('[YoYMoM] 缺少messageId或datasourceId，跳过')
    return
  }

  try {
    const effectiveDateRange = getEffectiveDateRange()
    if (!effectiveDateRange?.[0] || !effectiveDateRange?.[1]) {
      console.log('[YoYMoM] 无有效时间范围，跳过')
      return
    }

    console.log('[YoYMoM] 开始计算同比环比，时间范围:', effectiveDateRange)

    const req: RefreshRequest = {
      messageId: currentResult.value.messageId,
      datasourceId: selectedDatasourceId.value,
      startDate: effectiveDateRange[0],
      endDate: effectiveDateRange[1]
    }

    const res = await aiRefresh(req)
    console.log('[YoYMoM] 刷新响应:', res)

    if (res.code === 0 && res.data?.queries) {
      // 只更新KPI的同比环比数据，不替换整个queries
      let updatedCount = 0
      res.data.queries.forEach(newQuery => {
        if (newQuery.type === 'kpi' && currentResult.value?.queries) {
          const existingKpi = currentResult.value.queries.find(
            q => q.type === 'kpi' && q.title === newQuery.title
          )
          if (existingKpi) {
            console.log(`[YoYMoM] 更新KPI "${newQuery.title}":`, {
              yoy: newQuery.yoy,
              yoyRate: newQuery.yoyRate,
              mom: newQuery.mom,
              momRate: newQuery.momRate
            })
            existingKpi.yoy = newQuery.yoy
            existingKpi.yoyRate = newQuery.yoyRate
            existingKpi.mom = newQuery.mom
            existingKpi.momRate = newQuery.momRate
            updatedCount++
          }
        }
      })
      console.log(`[YoYMoM] 共更新 ${updatedCount} 个KPI`)
    }
  } catch (e) {
    // 静默失败，不显示错误
    console.warn('[YoYMoM] 计算同比环比失败', e)
  }
}

// 维度切换变化
async function onDimensionChange() {
  // 如果选择了"初始"，清除下钻数据，恢复原始图表
  if (selectedDimension.value === '__initial__') {
    drillData.value = null
    drillBreadcrumbs.value = []
    await nextTick()
    renderCharts()  // 重新渲染原始图表
    return
  }

  await executeDrillQuery()
}

// 度量切换变化
async function onMeasureChange() {
  await executeDrillQuery()
}

// 执行下钻查询（基于后端drill API）
async function executeDrillQuery() {
  if (!currentResult.value?.messageId || !selectedDatasourceId.value || !selectedDimension.value) {
    return
  }

  drillLoading.value = true

  try {
    // 构建过滤条件
    const filters: DrillFilter[] = [...drillFilters.value]

    // 添加医院筛选
    if (selectedHospital.value && effectiveHospitalField.value) {
      filters.push({
        field: effectiveHospitalField.value,
        op: '=',
        value: selectedHospital.value
      })
    }

    // 获取选中的度量
    const measures: MeasureField[] = []
    if (currentResult.value.measures && currentResult.value.measures.length > 0) {
      measures.push(currentResult.value.measures[selectedMeasureIdx.value])
    }

    // 获取当前图表的条数限制（默认使用第一个图表的设置）
    const currentLimit = chartLimits[0] || 20
    // 如果设置为-1（全部），使用一个较大的值
    const effectiveLimit = currentLimit === -1 ? 1000 : currentLimit

    const request: DrillRequest = {
      messageId: currentResult.value.messageId,
      datasourceId: selectedDatasourceId.value,
      groupBy: selectedDimension.value,
      filters: filters.length > 0 ? filters : undefined,
      measures: measures.length > 0 ? measures : undefined,
      orderBy: 'desc',
      limit: effectiveLimit
    }

    const res = await aiDrill(request)

    if (res.code === 0 && res.data) {
      drillData.value = res.data.data || null

      // 更新面包屑
      updateDrillBreadcrumb()

      // 重新渲染图表
      await nextTick()
      renderDrillChart()
    } else {
      ElMessage.error(res.message || '下钻查询失败')
    }
  } catch (e: any) {
    ElMessage.error(e.message || '下钻查询失败')
  } finally {
    drillLoading.value = false
  }
}

// 更新下钻面包屑（注意：仅在真正下钻时由executeDrillFromChart调用）
// executeDrillQuery只是切换维度，不应追加面包屑
function updateDrillBreadcrumb() {
  // 当前实现已移至 executeDrillFromChart 中处理
  // 此函数不再自动追加面包屑，避免切换维度时重复添加
}

// 渲染下钻结果图表
function renderDrillChart() {
  if (!drillData.value || drillData.value.length === 0) return

  // 找到第一个图表容器并渲染
  const chartQueriesArr = currentResult.value?.queries?.filter(q => q.type !== 'kpi') || []
  if (chartQueriesArr.length === 0) return

  const el = chartRefs[0]
  if (!el) return

  // 销毁旧图表
  const existingIdx = chartInstances.findIndex(c => c.getDom() === el)
  if (existingIdx >= 0) {
    chartInstances[existingIdx].dispose()
    chartInstances.splice(existingIdx, 1)
  }

  const instance = echarts.init(el)
  chartInstances.push(instance)

  let data = [...drillData.value]
  const keys = Object.keys(data[0])
  const xField = keys[0]
  const yField = keys.length > 1 ? keys[1] : keys[0]

  // 检测是否是日期维度
  const isDateDimension = isDateField(xField, data[0][xField])

  // 日期维度：按时间顺序排序
  if (isDateDimension) {
    data = data.sort((a, b) => {
      const da = new Date(a[xField]).getTime() || 0
      const db = new Date(b[xField]).getTime() || 0
      return da - db  // 升序（从早到晚）
    })
  }

  // 使用formatXAxisValue格式化X轴数据（去掉时间部分）
  const xData = data.map(row => formatXAxisValue(row[xField]))
  const yData = data.map(row => Number(row[yField]) || 0)

  // 日期维度用折线图，其他用柱状图
  const chartType = isDateDimension ? 'line' : 'bar'

  // 医疗蓝色系配色
  const medicalBlueColors = ['#1e88e5', '#1565c0', '#0097a7', '#3949ab', '#039be5', '#00838f', '#283593']

  const option: echarts.EChartsOption = {
    title: { text: `按${selectedDimension.value}分析`, left: 'center', textStyle: { fontSize: 14 } },
    color: medicalBlueColors,
    tooltip: { trigger: 'axis' },
    grid: { top: 40, bottom: xData.some(x => x.length > 4) ? 60 : 30, left: 50, right: 20 },
    xAxis: {
      type: 'category',
      data: xData,
      axisLabel: { rotate: xData.length > 8 ? 45 : 0, fontSize: 10, interval: 0 }
    },
    yAxis: { type: 'value' },
    series: [{
      type: chartType,
      data: yData,
      itemStyle: {
        borderRadius: chartType === 'bar' ? [3, 3, 0, 0] : undefined,
        color: '#1e88e5'  // 医疗蓝
      },
      smooth: chartType === 'line' ? true : undefined,
      emphasis: { itemStyle: { shadowBlur: 10, shadowColor: 'rgba(0,0,0,0.3)' } }
    }]
  }

  instance.setOption(option)

  // 添加点击事件（下钻到明细）
  instance.off('click')
  instance.on('click', (params) => handleDrillChartClick(params, xField))
}

// 处理下钻图表的点击事件
function handleDrillChartClick(params: any, dimensionField: string) {
  if (!params.name || !currentResult.value) return
  if (!currentResult.value.detailSql) {
    ElMessage.info('该查询不支持下钻到明细数据')
    return
  }

  // 构建筛选条件
  const filters: DrillFilter[] = []

  // 添加当前点击的维度筛选
  filters.push({
    field: dimensionField,
    op: '=',
    value: String(params.name)
  })

  // 如果有医院筛选，也添加进去
  if (selectedHospital.value && effectiveHospitalField.value) {
    filters.push({
      field: effectiveHospitalField.value,
      op: '=',
      value: selectedHospital.value
    })
  }

  // 设置弹窗标题
  detailTitle.value = `${dimensionField}: ${params.name} 的明细数据`
  detailFilters.value = filters
  detailPage.value = 1

  // 重置前端筛选和分页状态
  columnFilterValues.value = {}
  detailLocalPage.value = 1
  detailLocalPageSize.value = 20

  // 打开弹窗并加载数据
  showDetailDialog.value = true
  loadDetailData()
}

// 加载明细数据
async function loadDetailData() {
  if (!currentResult.value?.messageId || !selectedDatasourceId.value) return

  detailLoading.value = true
  try {
    // 获取当前时间范围（用于替换明细SQL中的时间参数）
    const effectiveDateRange = getEffectiveDateRange()

    const req: DetailRequest = {
      messageId: currentResult.value.messageId,
      datasourceId: selectedDatasourceId.value,
      filters: detailFilters.value,
      page: detailPage.value,
      pageSize: detailPageSize.value,
      startDate: effectiveDateRange?.[0] || undefined,  // ★ 传递时间范围
      endDate: effectiveDateRange?.[1] || undefined
    }

    const res = await aiDetail(req)
    if (res.code === 0 && res.data) {
      detailData.value = res.data
    } else {
      ElMessage.error(res.message || '查询明细失败')
    }
  } catch (e: any) {
    ElMessage.error(e.message || '查询明细失败')
  } finally {
    detailLoading.value = false
  }
}

// 前端分页-页码变化
function onDetailLocalPageChange(page: number) {
  detailLocalPage.value = page
}

// 前端分页-每页条数变化
function onDetailLocalSizeChange(size: number) {
  detailLocalPageSize.value = size
  detailLocalPage.value = 1
}

// 列筛选值变化时重置分页
function onColumnFilterChange() {
  detailLocalPage.value = 1
}

// 清除单个列的筛选
function clearColumnFilter(col: string) {
  columnFilterValues.value[col] = ''
  detailLocalPage.value = 1
}

// 清除所有列筛选
function clearAllColumnFilters() {
  columnFilterValues.value = {}
  detailLocalPage.value = 1
}

// 表格筛选变化处理（Element Plus内置筛选）
function handleTableFilterChange(filters: Record<string, any>) {
  console.log('Table filter changed:', filters)
}

// 重置下钻状态
function resetDrillState() {
  selectedHospital.value = ''
  selectedDimension.value = ''
  selectedMeasureIdx.value = 0
  drillFilters.value = []
  drillData.value = null
  drillBreadcrumbs.value = []
  originalQueries.value = null  // 清除原始queries
}

// 获取KPI值
function getKpiValue(kpi: any): string {
  if (kpi.error) return '错误'
  if (!kpi.data || kpi.data.length === 0) return '-'

  const row = kpi.data[0]
  const field = kpi.field || Object.keys(row)[0]
  const value = Number(row[field]) || 0

  return formatKpiValue(value)
}

// 获取同比环比的CSS类（增长用绿色，下降用红色，持平用白色）
function getCompareClass(rate: number | undefined): string {
  if (rate === undefined) return ''
  if (rate === 0) return 'compare-flat'
  return rate > 0 ? 'compare-up' : 'compare-down'
}

// 格式化KPI显示值
function formatKpiValue(value: number): string {
  if (value >= 100000000) {
    return (value / 100000000).toFixed(2) + '亿'
  } else if (value >= 10000) {
    return (value / 10000).toFixed(2) + '万'
  } else if (Number.isInteger(value)) {
    return value.toLocaleString()
  } else {
    return value.toFixed(2)
  }
}

// 加载数据源列表和默认数据源配置
const loadDatasources = async () => {
  try {
    // 1. 加载数据源列表
    const res = await getDatasourceList()
    if (res.code === 0) {
      datasources.value = res.data || []
    }

    // 2. 从系统配置获取默认AI数据源
    try {
      const configRes = await getConfig('ai.default.datasource')
      if (configRes.code === 0 && configRes.data) {
        const defaultDsId = Number(configRes.data)
        if (defaultDsId && datasources.value.some(ds => ds.id === defaultDsId)) {
          selectedDatasourceId.value = defaultDsId
        }
      }
    } catch (configErr) {
      console.warn('获取默认数据源配置失败，将使用第一个数据源', configErr)
    }

    // 3. 如果没有配置默认数据源，使用第一个数据源
    if (!selectedDatasourceId.value && datasources.value.length > 0) {
      selectedDatasourceId.value = datasources.value[0].id
    }
  } catch (e) {
    console.error('加载数据源失败', e)
  }
}

// 数据源变化时清空表列表（虽然不再显示选择表，但保留加载以便将来使用）
const _onDatasourceChange = async () => {
  tables.value = []
  if (!selectedDatasourceId.value) return

  loadingTables.value = true
  try {
    const res = await getAiTables(selectedDatasourceId.value)
    if (res.code === 0) {
      tables.value = res.data || []
    }
  } catch (e) {
    console.error('加载表列表失败', e)
  } finally {
    loadingTables.value = false
  }
}
void _onDatasourceChange // 防止未使用警告

// 语音指令词配置
const VOICE_SEND_KEYWORDS = ['执行', '发送', '查询', '开始', '分析', '搜索']

// 语音识别结果处理
const onVoiceTranscribed = (text: string) => {
  // 检测是否包含指令词
  let cleanText = text.trim()
  let shouldAutoSend = false

  // 检查文本末尾是否有指令词
  for (const keyword of VOICE_SEND_KEYWORDS) {
    if (cleanText.endsWith(keyword)) {
      // 移除指令词
      cleanText = cleanText.slice(0, -keyword.length).trim()
      shouldAutoSend = true
      console.log(`[语音] 检测到指令词"${keyword}"，将自动发送`)
      break
    }
  }

  // 将识别的文本设置到输入框（如果已有内容则追加，否则直接设置）
  if (question.value.trim()) {
    question.value = question.value.trim() + ' ' + cleanText
  } else {
    question.value = cleanText
  }

  // 如果检测到指令词，自动发送
  if (shouldAutoSend && question.value.trim()) {
    ElMessage.info('🎤 正在执行语音指令...')
    // 稍微延迟发送，让用户看到文字
    setTimeout(() => {
      sendQuestion()
    }, 300)
  }
}

// 语音识别错误处理
const onVoiceError = (message: string) => {
  console.error('语音识别错误:', message)
}

// 语音唤醒事件处理：唤醒后自动激活 VoiceInput 开始录音
const onVoiceWakeup = () => {
  console.log('[语音唤醒] 检测到唤醒词，自动开始录音')
  if (voiceInputRef.value && !voiceInputRef.value.isRecording) {
    voiceInputRef.value.startRecording()
  }
}

// 语音指令词事件处理：检测到"发送"、"执行"等词，自动提交
const onVoiceCommand = (word: string) => {
  console.log('[语音唤醒] 检测到指令词:', word)
  if (question.value?.trim()) {
    sendQuestion()
  }
}

// 发送问题
const sendQuestion = async () => {
  if (!question.value.trim()) {
    ElMessage.warning('请输入问题')
    return
  }
  if (!selectedDatasourceId.value) {
    ElMessage.warning('请先选择数据源')
    return
  }

  loading.value = true

  // ★ 关键修复：每次发送新问题都创建新会话，清空旧的sessionId
  // 防止新问题被错误地追加到之前点击过的历史会话中
  sessionId.value = ''
  currentResult.value = null
  chatHistory.value = []

  chatHistory.value.push({ role: 'user', content: question.value })

  // 重置流式状态
  isStreaming.value = false
  streamingAnswer.value = ''

  // 保存原始问题（不带时间范围）
  const originalQuestion = question.value

  // 获取有效的时间范围（仅用于BI模式）
  const effectiveDateRange = getEffectiveDateRange()

  // 构造带时间范围的问题（仅用于BI模式）
  let questionWithTimeRange = question.value
  if (effectiveDateRange && effectiveDateRange[0] && effectiveDateRange[1]) {
    questionWithTimeRange += `（时间范围：${effectiveDateRange[0]} 至 ${effectiveDateRange[1]}）`
  }

  try {
    // 先尝试流式接口（知识问答模式会使用流式，不带时间范围）
    // 如果是其他模式会redirect到普通请求，那时再加时间范围
    await sendQuestionStream(originalQuestion, questionWithTimeRange)
  } catch (e: any) {
    console.error('流式请求失败，回退到普通请求', e)
    // 如果流式失败，回退到普通请求（带时间范围，用于BI模式）
    await sendQuestionNormal(questionWithTimeRange)
  } finally {
    loading.value = false
    isStreaming.value = false
    question.value = ''
  }
}

// 流式发送问题（用于知识问答模式）
// originalQuestion: 原始问题（不带时间范围，用于知识问答）
// questionWithTimeRange: 带时间范围的问题（用于BI模式redirect时）
const sendQuestionStream = async (originalQuestion: string, questionWithTimeRange: string) => {
  const baseUrl = import.meta.env.VITE_API_BASE_URL || ''
  const url = `${baseUrl}/api/v1/ai/chat/stream`

  const response = await fetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${localStorage.getItem('token') || ''}`
    },
    body: JSON.stringify({
      question: originalQuestion,  // 流式接口用原始问题（不带时间范围）
      datasourceId: selectedDatasourceId.value,
      sessionId: sessionId.value || undefined
    })
  })

  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`)
  }

  const reader = response.body?.getReader()
  if (!reader) {
    throw new Error('无法读取响应流')
  }

  const decoder = new TextDecoder()
  let buffer = ''
  let redirectMode = ''

  // 初始化currentResult
  currentResult.value = {
    mode: '',
    answer: '',
    sessionId: sessionId.value || '',
    messageId: 0,
    prompts: []
  }

  while (true) {
    const { done, value } = await reader.read()
    if (done) break

    buffer += decoder.decode(value, { stream: true })

    // SSE格式: event: xxx\ndata: yyy\n\n
    // 按双换行分割事件
    const events = buffer.split('\n\n')
    buffer = events.pop() || ''  // 最后一个可能不完整，保留

    for (const event of events) {
      if (!event.trim()) continue

      const lines = event.split('\n')
      let eventType = ''
      const dataLines: string[] = []

      for (const line of lines) {
        if (line.startsWith('event: ')) {
          eventType = line.slice(7).trim()
        } else if (line.startsWith('data: ')) {
          // SSE协议：多行data需要拼接，每个data:行代表一行内容
          dataLines.push(line.slice(6))
        }
      }
      // 将多个data行用换行符拼接还原原始内容
      const data = dataLines.join('\n')

      if (!eventType) continue

      switch (eventType) {
        case 'init':
          try {
            const initData = JSON.parse(data)
            currentResult.value!.sessionId = initData.sessionId
            currentResult.value!.mode = initData.mode
            currentResult.value!.modeReason = initData.modeReason
            sessionId.value = initData.sessionId
          } catch (e) { console.error('解析init失败', e) }
          break
        case 'redirect':
          // 非internetsearch模式，需要回退到普通请求
          redirectMode = data
          break
        case 'prompt':
          try {
            const promptInfo = JSON.parse(data)
            // 构造prompts格式
            currentResult.value!.prompts = [{
              phase: promptInfo.phase || '知识问答',
              content: `[System]\n${promptInfo.systemPrompt}\n\n[User]\n${promptInfo.userQuestion}`,
              response: ''
            }]
          } catch (e) { console.error('解析prompt失败', e) }
          break
        case 'content':
          isStreaming.value = true
          streamingAnswer.value += data
          // 实时更新prompts的response
          if (currentResult.value?.prompts?.length) {
            currentResult.value.prompts[0].response = streamingAnswer.value
          }
          break
        case 'error':
          ElMessage.error(data || '请求失败')
          break
        case 'done':
          // 完成后将流式内容赋值给currentResult
          if (streamingAnswer.value) {
            currentResult.value!.answer = streamingAnswer.value
            chatHistory.value.push({ role: 'assistant', content: streamingAnswer.value })
          }
          loadSessionList()
          break
      }
    }

    // 如果需要重定向到普通请求（使用带时间范围的问题）
    if (redirectMode) {
      reader.cancel()
      await sendQuestionNormal(questionWithTimeRange)
      return
    }
  }
}

// 普通发送问题（用于BI和患者360模式）
const sendQuestionNormal = async (finalQuestion: string) => {
  const res = await aiChat({
    question: finalQuestion,
    datasourceId: selectedDatasourceId.value!,
    sessionId: sessionId.value || undefined
  })

  if (res.code === 0 && res.data) {
    currentResult.value = res.data
    sessionId.value = res.data.sessionId
    // 报表模式重置页签
    if (res.data.mode === 'report') {
      activeReportSheet.value = '0'
    }

    if (res.data.answer) {
      chatHistory.value.push({ role: 'assistant', content: res.data.answer })
    }

    // ★ 立即添加或更新会话到列表（不等待API刷新）
    const newSessionKey = res.data.sessionId
    const existingIdx = sessionList.value.findIndex(s => s.sessionKey === newSessionKey)
    if (existingIdx >= 0) {
      // 已存在，更新时间和messageId
      sessionList.value[existingIdx].lastActiveAt = new Date().toISOString()
      sessionList.value[existingIdx].lastMessageId = res.data.messageId
    } else {
      // 新会话，添加到列表最前面
      sessionList.value.unshift({
        id: res.data.messageId || 0,  // 临时使用messageId
        sessionKey: newSessionKey,
        title: originalQuestion.value || finalQuestion.substring(0, 50),
        datasourceId: selectedDatasourceId.value ?? undefined,  // null转为undefined
        createdAt: new Date().toISOString(),
        lastActiveAt: new Date().toISOString(),
        lastMessageId: res.data.messageId,
        mode: res.data.mode || 'bi'
      })
    }

    // 重置下钻状态
    resetDrillState()

    // 初始化下钻维度（默认选中"初始"，显示原始图表）
    if (res.data.dimensions && res.data.dimensions.length > 0) {
      selectedDimension.value = '__initial__'  // 默认显示原始图表
    }

    // 初始化图表设置并渲染
    await nextTick()
    initChartSettings()
    renderCharts()

    // 异步刷新历史会话列表（获取准确的id和更新信息）
    loadSessionList()

    // 如果有KPI且有时间范围，自动刷新一次以计算同比环比
    const effectiveDateRange = getEffectiveDateRange()
    const hasKpi = res.data.queries?.some(q => q.type === 'kpi')
    if (hasKpi && effectiveDateRange && effectiveDateRange[0] && effectiveDateRange[1]) {
      // 静默刷新计算同比环比
      await refreshQueriesForYoYMoM()
    }
  } else {
    ElMessage.error(res.message || '请求失败')
  }
}

// 渲染所有图表
const renderCharts = () => {
  // 清理旧图表
  chartInstances.forEach(c => c.dispose())
  chartInstances = []

  // 多查询模式
  if (chartQueries.value.length > 0) {
    // 稍微延迟以确保DOM已更新
    setTimeout(() => {
      chartQueries.value.forEach((query, idx) => {
        const el = chartRefs[idx]
        if (!el || !query.data || query.data.length === 0) return

        // 如果已经有实例，先销毁
        const existingIdx = chartInstances.findIndex(c => c.getDom() === el)
        if (existingIdx >= 0) {
          chartInstances[existingIdx].dispose()
          chartInstances.splice(existingIdx, 1)
        }

        const instance = echarts.init(el)
        chartInstances.push(instance)

        const chartType = query.type as string
        // 使用排序+限制后的数据
        const limit = chartLimits[idx] || -1
        const sortMode = chartSortModes[idx] || 'none'
        const data = getProcessedData(query.data, limit, sortMode)
        if (!data || data.length === 0) return

        const keys = Object.keys(data[0])
        const xField = keys[0]
        const yField = keys.length > 1 ? keys[1] : keys[0]

        // 使用formatXAxisValue格式化X轴数据（去掉时间部分）
        const xData = data.map(row => formatXAxisValue(row[xField]))
        const yData = data.map(row => Number(row[yField]) || 0)

        // 医疗蓝色系配色
        const medicalBlueColors = ['#1e88e5', '#1565c0', '#0097a7', '#3949ab', '#039be5', '#00838f', '#283593']

        const option: echarts.EChartsOption = {
          title: { text: '', left: 'center', textStyle: { fontSize: 14 } },  // 标题已在header显示
          color: medicalBlueColors,
          tooltip: {
            trigger: chartType === 'pie' ? 'item' : 'axis',
            formatter: chartType === 'pie' ? '{b}: {c} ({d}%)' : undefined
          },
          grid: { top: 20, bottom: xData.some(x => x.length > 4) ? 60 : 30, left: 50, right: 20 },
          xAxis: chartType === 'pie' ? undefined : {
            type: 'category',
            data: xData,
            axisLabel: { rotate: xData.length > 8 ? 45 : 0, fontSize: 10, interval: 0 }
          },
          yAxis: chartType === 'pie' ? undefined : { type: 'value' },
          series: [{
            type: chartType as any,
            data: chartType === 'pie'
              ? data.map((_, i) => ({ name: xData[i], value: yData[i] }))
              : yData,
            label: chartType === 'pie' ? { show: true, formatter: '{b}: {d}%' } : undefined,
            itemStyle: {
              borderRadius: chartType === 'bar' ? [3, 3, 0, 0] : undefined,
              color: chartType !== 'pie' ? '#1e88e5' : undefined  // 柱状图和折线图统一用医疗蓝
            },
            smooth: chartType === 'line' ? true : undefined,  // 折线图平滑曲线
            emphasis: { itemStyle: { shadowBlur: 10, shadowColor: 'rgba(0,0,0,0.3)' } }  // 点击高亮
          }]
        }

        instance.setOption(option)

        // 添加点击事件监听（下钻到明细）
        instance.off('click')  // 先移除旧的监听
        instance.on('click', (params) => handleChartClick(params, idx, query))
      })
      // 注：已移除自动截图，改为手动点击"保存为图片"按钮
    }, 100)
  }
  // 单查询模式
  else if (singleChartRef.value && currentResult.value?.data?.length) {
    const instance = echarts.init(singleChartRef.value)
    chartInstances.push(instance)

    const data = currentResult.value.data
    const chartType = currentResult.value.chartType || 'bar'
    const config = currentResult.value.chartConfig

    // 使用formatXAxisValue格式化X轴数据（去掉时间部分）
    const xData = data.map(row => {
      const dim = config?.dimensions?.[0] || Object.keys(row)[0]
      return formatXAxisValue(row[dim])
    })
    const measureField = config?.measures?.[0]?.field || Object.keys(data[0])[1] || Object.keys(data[0])[0]
    const yData = data.map(row => row[measureField])

    const option: echarts.EChartsOption = {
      title: { text: config?.title || '分析结果', left: 'center' },
      tooltip: { trigger: chartType === 'pie' ? 'item' : 'axis' },
      xAxis: chartType === 'pie' ? undefined : { type: 'category', data: xData },
      yAxis: chartType === 'pie' ? undefined : { type: 'value' },
      series: [{
        type: chartType as any,
        data: chartType === 'pie'
          ? data.map((_, i) => ({ name: xData[i], value: yData[i] }))
          : yData
      }]
    }

    instance.setOption(option)
    // 注：已移除自动截图，改为手动点击"保存为图片"按钮
  }
}

// 保存单个图表为图片（手动触发）
const saveChartAsImage = async (chartIndex: number, chartTitle: string) => {
  if (!currentResult.value?.messageId) {
    ElMessage.warning('请先进行数据分析')
    return
  }

  // 获取对应的图表实例
  const instance = chartInstances[chartIndex]
  if (!instance) {
    ElMessage.warning('图表未渲染完成，请稍后再试')
    return
  }

  try {
    // 截取图表
    const dataUrl = instance.getDataURL({
      type: 'png',
      pixelRatio: 2,  // 高清截图
      backgroundColor: '#fff'  // 白色背景
    })

    if (!dataUrl) {
      ElMessage.error('截图失败')
      return
    }

    // 上传到服务器
    const title = chartTitle || `图表${chartIndex + 1}`
    await uploadChartImages({
      messageId: currentResult.value.messageId,
      images: [dataUrl],
      chartImages: [{ image: dataUrl, title }]
    })

    ElMessage.success(`图表"${title}"已保存`)

    // 刷新会话列表以更新图片数量显示
    await loadSessionList()
  } catch (e) {
    console.error('保存图表图片失败', e)
    ElMessage.error('保存失败，请重试')
  }
}

// 保存单个KPI卡片为图片（使用html2canvas截图HTML元素）
const saveKpiAsImage = async (kpiIndex: number, kpiTitle: string) => {
  if (!currentResult.value?.messageId) {
    ElMessage.warning('请先进行数据分析')
    return
  }

  // 获取对应的KPI卡片元素
  const kpiElement = kpiRefs[kpiIndex]
  if (!kpiElement) {
    ElMessage.warning('KPI卡片未渲染完成，请稍后再试')
    return
  }

  try {
    // 使用 html2canvas 截取 KPI 卡片
    const canvas = await html2canvas(kpiElement, {
      backgroundColor: null,  // 透明背景，保留渐变
      scale: 2,  // 高清截图
      useCORS: true,
      logging: false
    })

    const dataUrl = canvas.toDataURL('image/png')
    if (!dataUrl) {
      ElMessage.error('截图失败')
      return
    }

    // 上传到服务器
    const title = kpiTitle || `指标${kpiIndex + 1}`
    await uploadChartImages({
      messageId: currentResult.value.messageId,
      images: [dataUrl],
      chartImages: [{ image: dataUrl, title }]
    })

    ElMessage.success(`指标"${title}"已保存`)

    // 刷新会话列表以更新图片数量显示
    await loadSessionList()
  } catch (e) {
    console.error('保存KPI图片失败', e)
    ElMessage.error('保存失败，请重试')
  }
}

// 复制SQL
const copySql = () => {
  const queries = currentResult.value?.queries
  if (queries && queries.length > 0) {
    const allSql = queries.map(q => `-- ${q.title}\n${q.sql}`).join('\n\n')
    navigator.clipboard.writeText(allSql)
    ElMessage.success('已复制所有SQL到剪贴板')
  } else if (currentResult.value?.sql) {
    navigator.clipboard.writeText(currentResult.value.sql)
    ElMessage.success('已复制到剪贴板')
  }
}

// 保存单个图表到图表管理（创建数据集+图表实体）
const savingChartIdx = ref<number | null>(null)
const saveChartToManage = async (chartIndex: number, chart: QueryItem) => {
  if (!selectedDatasourceId.value) {
    ElMessage.warning('请先选择数据源')
    return
  }
  if (!chart.sql) {
    ElMessage.warning('该图表缺少SQL，无法保存')
    return
  }

  savingChartIdx.value = chartIndex
  try {
    const res = await saveAsChartApi({
      title: chart.title || (chartIndex < 0 ? `指标${Math.abs(chartIndex)}` : `图表${chartIndex + 1}`),
      question: question.value,
      sql: chart.sql,
      datasourceId: selectedDatasourceId.value,
      chartType: chart.type === 'kpi' ? 'kpi' : (chart.type || 'bar')
    })
    if (res.code === 0) {
      ElMessage.success(`"${chart.title}"已保存到图表管理`)
    } else {
      ElMessage.error(res.message || '保存失败')
    }
  } catch (e: any) {
    ElMessage.error(e.message || '保存失败')
  } finally {
    savingChartIdx.value = null
  }
}

// ==================== 模式相关功能 ====================

// 获取模式标签类型
function getModeTagType(mode: string): 'primary' | 'success' | 'warning' | 'info' | 'danger' {
  switch (mode) {
    case 'bi': return 'success'
    case 'hz360': return 'warning'
    case 'internetsearch': return 'info'
    case 'report': return 'danger'
    default: return 'primary'
  }
}

function getModeLabel(mode: string): string {
  switch (mode) {
    case 'bi': return '📊 指标统计'
    case 'hz360': return '👤 患者360'
    case 'internetsearch': return '💬 知识问答'
    case 'report': return '📋 智能报表'
    default: return mode
  }
}

// 简易Markdown渲染（支持基本格式）
function renderMarkdown(text: string): string {
  if (!text) return ''

  // 先规范化换行符
  let html = text.replace(/\r\n/g, '\n').replace(/\r/g, '\n')

  // 处理多个连续换行为段落分隔
  const paragraphs = html.split(/\n{2,}/)

  // 内联格式处理函数（粗体、斜体、代码）
  const processInlineFormats = (str: string): string => {
    return str
      .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
      .replace(/\*(.+?)\*/g, '<em>$1</em>')
      .replace(/__(.+?)__/g, '<strong>$1</strong>')
      .replace(/_(.+?)_/g, '<em>$1</em>')
      .replace(/`([^`]+)`/g, '<code>$1</code>')
  }

  const processedParagraphs = paragraphs.map(para => {
    let processed = para.trim()
    if (!processed) return ''

    // 转义HTML特殊字符（但保留换行符处理）
    processed = processed
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')

    // 标题处理（必须在行首）
    if (processed.match(/^#{1,4}\s/)) {
      processed = processed
        .replace(/^#### (.+)$/gm, (_, content) => `<h5>${processInlineFormats(content)}</h5>`)
        .replace(/^### (.+)$/gm, (_, content) => `<h4>${processInlineFormats(content)}</h4>`)
        .replace(/^## (.+)$/gm, (_, content) => `<h3>${processInlineFormats(content)}</h3>`)
        .replace(/^# (.+)$/gm, (_, content) => `<h2>${processInlineFormats(content)}</h2>`)
      return processed
    }

    // 无序列表处理
    if (processed.match(/^[-*]\s/m)) {
      const listItems = processed.split('\n')
        .map(line => {
          const match = line.match(/^[-*]\s+(.+)$/)
          return match ? `<li>${processInlineFormats(match[1])}</li>` : processInlineFormats(line)
        })
        .join('')
      return `<ul>${listItems}</ul>`
    }

    // 有序列表处理
    if (processed.match(/^\d+\.\s/m)) {
      const listItems = processed.split('\n')
        .map(line => {
          const match = line.match(/^\d+\.\s+(.+)$/)
          return match ? `<li>${processInlineFormats(match[1])}</li>` : processInlineFormats(line)
        })
        .join('')
      return `<ol>${listItems}</ol>`
    }

    // 普通段落处理
    processed = processInlineFormats(processed)

    // 单个换行转换为<br>
    processed = processed.replace(/\n/g, '<br>')

    return `<p>${processed}</p>`
  })

  return processedParagraphs.filter(p => p).join('')
}

// 格式化日期
function formatDate(dateStr: string): string {
  if (!dateStr) return ''
  const date = new Date(dateStr)
  return date.toLocaleDateString('zh-CN')
}

// 打开患者360详情页
function openPatient360(patient: any) {
  if (patient.detailUrl) {
    // 在新浏览器窗口打开患者360详情链接
    window.open(patient.detailUrl, '_blank')
  } else if (patient.patientId) {
    // 如果没有detailUrl，提示用户
    ElMessage.warning(`患者 ${patient.patientName} 暂无360详情链接`)
  }
}

// ==================== 会话历史功能 ====================

// 格式化时间（相对时间）
function formatTimeAgo(dateStr: string) {
  const date = new Date(dateStr)
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()
  const diffMins = Math.floor(diffMs / 60000)
  const diffHours = Math.floor(diffMs / 3600000)
  const diffDays = Math.floor(diffMs / 86400000)

  if (diffMins < 1) return '刚刚'
  if (diffMins < 60) return `${diffMins}分钟前`
  if (diffHours < 24) return `${diffHours}小时前`
  if (diffDays < 7) return `${diffDays}天前`
  return date.toLocaleDateString()
}

// 加载会话历史列表
async function loadSessionList() {
  if (!selectedDatasourceId.value) return

  loadingHistory.value = true
  try {
    const res = await getAiSessions(selectedDatasourceId.value)
    if (res.code === 0 && res.data) {
      // ★ 保留本地添加的会话（如果后端列表中还没有）
      // 这样可以避免竞态条件导致新会话消失
      const localSessions = sessionList.value.filter(local => {
        // 检查本地会话是否在后端列表中存在
        const existsInBackend = res.data!.some(backend =>
          backend.sessionKey === local.sessionKey
        )
        // 如果不存在于后端，且是最近创建的（5分钟内），保留它
        if (!existsInBackend) {
          const createdAt = new Date(local.createdAt).getTime()
          const now = Date.now()
          return (now - createdAt) < 5 * 60 * 1000  // 5分钟内的本地会话
        }
        return false
      })

      // 合并：后端列表 + 本地新增的会话
      sessionList.value = [...localSessions, ...res.data]
    }
  } catch (e) {
    console.error('加载会话历史失败', e)
  } finally {
    loadingHistory.value = false
  }
}

// 删除会话
async function deleteSession(session: SessionListItem) {
  try {
    await ElMessageBox.confirm(
      `确定要删除会话 "${session.title}" 吗？删除后无法恢复。`,
      '删除确认',
      {
        confirmButtonText: '删除',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )

    const res = await deleteAiSession(session.id)
    if (res.code === 0) {
      ElMessage.success('删除成功')
      // 如果删除的是当前会话，清空当前显示
      if (sessionId.value === session.sessionKey) {
        sessionId.value = ''
        currentResult.value = null
        chatHistory.value = []
      }
      // 刷新列表
      await loadSessionList()
    } else {
      ElMessage.error(res.message || '删除失败')
    }
  } catch (e: any) {
    // 用户取消
    if (e !== 'cancel' && e?.message !== 'cancel') {
      console.error('删除会话失败', e)
      ElMessage.error('删除会话失败')
    }
  }
}

// 编辑会话标题
async function editSessionTitle(session: SessionListItem) {
  try {
    const { value: newTitle } = await ElMessageBox.prompt(
      '请输入新的会话名称',
      '编辑会话名称',
      {
        confirmButtonText: '保存',
        cancelButtonText: '取消',
        inputValue: session.title,
        inputPattern: /^.{1,100}$/,
        inputErrorMessage: '名称长度应在1-100个字符之间'
      }
    )

    if (newTitle && newTitle !== session.title) {
      const res = await updateAiSessionTitle(session.id, newTitle)
      if (res.code === 0) {
        ElMessage.success('修改成功')
        // 更新本地列表中的标题
        const idx = sessionList.value.findIndex(s => s.id === session.id)
        if (idx >= 0) {
          sessionList.value[idx].title = newTitle
        }
      } else {
        ElMessage.error(res.message || '修改失败')
      }
    }
  } catch (e: any) {
    // 用户取消
    if (e !== 'cancel' && e?.message !== 'cancel') {
      console.error('编辑会话标题失败', e)
    }
  }
}

// 获取图片完整URL（拼接后端地址）
function getFullImageUrl(imgPath: string): string {
  if (!imgPath) return ''
  // 如果已经是完整URL直接返回
  if (imgPath.startsWith('http://') || imgPath.startsWith('https://')) {
    return imgPath
  }
  // 拼接后端API地址
  const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'
  return `${baseUrl}${imgPath}`
}

// 当前预览的会话ID（用于删除图片）
const previewSessionId = ref<number>(0)

// 预览会话的已保存图片
async function previewSessionImages(session: SessionListItem) {
  try {
    const res = await getSessionImages(session.id)
    if (res.code === 0 && res.data && res.data.length > 0) {
      // 将相对路径转换为完整URL
      previewImages.value = res.data.map(img => getFullImageUrl(img))
      previewSessionTitle.value = session.title
      previewSessionId.value = session.id
      showImagePreview.value = true
    } else {
      ElMessage.warning('暂无已保存的图片')
    }
  } catch (e) {
    console.error('获取会话图片失败', e)
    ElMessage.error('获取图片失败')
  }
}

// 删除会话中的指定图片
async function handleDeleteImage(imgUrl: string, index: number) {
  try {
    const res = await deleteSessionImage(previewSessionId.value, imgUrl)
    if (res.code === 0) {
      // 从预览列表中移除
      previewImages.value.splice(index, 1)
      ElMessage.success('图片已删除')
      // 刷新会话列表以更新图片数量
      await loadSessionList()
      // 如果没有图片了，关闭弹窗
      if (previewImages.value.length === 0) {
        showImagePreview.value = false
      }
    } else {
      ElMessage.error(res.message || '删除失败')
    }
  } catch (e) {
    console.error('删除图片失败', e)
    ElMessage.error('删除失败')
  }
}

// 重放历史会话
async function replaySession(session: SessionListItem) {
  if (!selectedDatasourceId.value) return

  // 没有可重放消息的会话，提示用户
  if (!session.lastMessageId) {
    ElMessage.warning('该历史会话无法重放（旧版数据）')
    return
  }

  loading.value = true
  try {
    // ★ 获取当前时间范围，用于历史会话重放时使用新时间段
    const effectiveDateRange = getEffectiveDateRange()

    const res = await aiReplay({
      messageId: session.lastMessageId,
      datasourceId: selectedDatasourceId.value,
      startDate: effectiveDateRange?.[0] || undefined,
      endDate: effectiveDateRange?.[1] || undefined
    })
    if (res.code === 0 && res.data) {
      currentResult.value = res.data
      sessionId.value = res.data.sessionId

      // 清空当前对话历史，显示历史会话内容
      chatHistory.value = [
        { role: 'user', content: session.title },
        { role: 'assistant', content: res.data.answer || '已加载历史查询' }
      ]

      // ★ 根据模式决定后续处理
      const mode = res.data.mode || 'bi'

      if (mode === 'bi') {
        // BI模式：重置下钻状态，初始化图表
        resetDrillState()

        // 初始化下钻维度（默认选中"初始"）
        if (res.data.dimensions && res.data.dimensions.length > 0) {
          selectedDimension.value = '__initial__'  // 默认显示原始图表
        }

        // 渲染图表
        await nextTick()
        initChartSettings()  // 初始化图表设置
        renderCharts()       // 使用统一的渲染方法
      } else if (mode === 'hz360') {
        // 患者360模式：前端已经通过模板渲染患者列表
        // 不需要额外处理
      } else if (mode === 'internetsearch') {
        // 知识问答模式：前端已经通过模板渲染Markdown内容
      } else if (mode === 'report') {
        // 报表模式：重置页签
        activeReportSheet.value = '0'
      }

      ElMessage.success('已加载历史查询结果')
    } else {
      ElMessage.error(res.message || '加载失败')
    }
  } catch (e: any) {
    ElMessage.error(e.message || '加载历史查询失败')
  } finally {
    loading.value = false
  }
}

// 监听数据源变化，加载会话历史
watch(selectedDatasourceId, () => {
  loadSessionList()
})

onMounted(() => {
  loadDatasources()
})

// 监听窗口大小变化
window.addEventListener('resize', () => {
  chartInstances.forEach(c => c.resize())
})
</script>

<style scoped>
.ai-analysis {
  display: flex;
  height: 100%;
  gap: 16px;
  padding: 16px;
  background: #f5f7fa;
}

.left-panel {
  width: 300px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.right-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.config-card, .history-card {
  flex-shrink: 0;
}

.history-card {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

/* 让el-card的body使用flex布局 */
.history-card :deep(.el-card__body) {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  padding-bottom: 12px;
}

.history-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

/* 历史标签页样式 */
.history-tabs {
  flex-shrink: 0;
}

.history-list {
  flex: 1;
  overflow-y: auto;
  scrollbar-width: thin;
  scrollbar-color: #c0c4cc #f5f7fa;
  min-height: 0; /* 重要：让flex子元素可以收缩 */
}

.report-generate-btns {
  flex-shrink: 0;
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid var(--el-border-color-lighter);
  display: flex;
  gap: 8px;

  .el-button {
    flex: 1;
  }
}

.history-list::-webkit-scrollbar {
  width: 6px;
}

.history-list::-webkit-scrollbar-track {
  background: #f5f7fa;
  border-radius: 3px;
}

.history-list::-webkit-scrollbar-thumb {
  background: #c0c4cc;
  border-radius: 3px;
}

.history-list::-webkit-scrollbar-thumb:hover {
  background: #909399;
}

.history-item {
  padding: 8px 12px;
  margin-bottom: 8px;
  border-radius: 8px;
}

.history-item.user {
  background: #e6f7ff;
  text-align: right;
}

.history-item.assistant {
  background: #f6ffed;
}

/* 会话历史样式 */
.session-item {
  background: #fafafa;
  cursor: pointer;
  transition: all 0.2s;
  border: 1px solid transparent;
}

.session-item:hover {
  background: #e6f7ff;
  border-color: #91d5ff;
}

.session-item.active {
  background: #bae7ff;
  border-color: #1890ff;
}

.session-item.disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.session-item.disabled:hover {
  background: #fafafa;
  border-color: transparent;
}

.session-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.session-content {
  flex: 1;
  min-width: 0;
}

.session-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 2px;
}

.session-id {
  font-size: 10px;
  color: #999;
  font-family: monospace;
}

.session-edit-btn {
  opacity: 0;
  transition: opacity 0.2s;
  padding: 2px 4px !important;
  height: auto !important;
  color: #409eff;
}

.session-item:hover .session-edit-btn {
  opacity: 1;
}

.session-title {
  font-size: 13px;
  color: #333;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.session-time {
  font-size: 11px;
  color: #999;
}

.session-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 2px;
}

.session-images {
  font-size: 11px;
  color: #409eff;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 2px;
}

.session-images:hover {
  color: #66b1ff;
  text-decoration: underline;
}

/* 图片预览弹窗样式 */
.image-preview-container {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  justify-content: flex-start;
  max-height: 70vh;
  overflow-y: auto;
}

.image-preview-item {
  width: calc(33.33% - 12px);
  border: 1px solid #eee;
  border-radius: 8px;
  overflow: hidden;
  background: #fafafa;
}

.image-preview-item .el-image {
  width: 100%;
  height: 200px;
}

.image-info {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px;
  background: #f5f5f5;
  gap: 8px;
}

.image-filename {
  flex: 1;
  font-size: 12px;
  color: #666;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.session-delete-btn {
  opacity: 0;
  transition: opacity 0.2s;
  flex-shrink: 0;
  margin-left: 4px;
}

.session-item:hover .session-delete-btn {
  opacity: 1;
}

.input-card {
  flex-shrink: 0;
}

.input-area {
  display: flex;
  align-items: flex-start;
}

/* 时间范围选择条 */
.time-range-bar {
  display: flex;
  align-items: center;
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid #ebeef5;
  flex-wrap: wrap;
  gap: 8px;
}

.time-range-label {
  font-size: 13px;
  color: #606266;
  white-space: nowrap;
}

.time-range-tags {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.time-tag {
  cursor: pointer;
  transition: all 0.2s;
}

.time-tag:hover {
  transform: translateY(-1px);
}

/* 历史分组样式 */
.history-group {
  margin-bottom: 12px;
}

/* 历史标签页样式 */
.history-tabs {
  margin-bottom: 8px;
}

.history-tabs :deep(.el-tabs__header) {
  margin: 0;
}

.history-tabs :deep(.el-tabs__nav-wrap) {
  padding: 0;
}

.history-tabs :deep(.el-tabs__item) {
  padding: 0 8px;
  height: 32px;
  font-size: 12px;
}

.tab-label {
  display: flex;
  align-items: center;
  gap: 4px;
}

.tab-label .el-icon {
  font-size: 14px;
}

.tab-badge {
  margin-left: 4px;
}

.tab-badge :deep(.el-badge__content) {
  font-size: 10px;
  padding: 0 4px;
  height: 14px;
  line-height: 14px;
}

.result-card {
  flex: 1;
  overflow: auto;
}

.result-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.answer-section {
  margin-bottom: 16px;
}

.chart-section {
  margin-bottom: 16px;
}

.chart-container {
  height: 350px;
  width: 100%;
}

.table-section {
  margin-top: 16px;
}

/* 下钻控制区样式 */
.drill-control-bar {
  display: flex;
  align-items: center;
  gap: 24px;
  padding: 12px 16px;
  background: linear-gradient(135deg, #f5f7fa 0%, #e8ecf1 100%);
  border-radius: 8px;
  margin-bottom: 16px;
  flex-wrap: wrap;
}

.hospital-filter,
.dimension-switcher,
.measure-selector {
  display: flex;
  align-items: center;
  gap: 8px;
}

.filter-label {
  color: #606266;
  font-size: 13px;
  font-weight: 500;
  white-space: nowrap;
}

/* KPI卡片样式 */
.kpi-cards {
  display: flex;
  gap: 16px;
  margin-bottom: 20px;
  flex-wrap: wrap;
}

.kpi-card {
  background: linear-gradient(135deg, #1e88e5 0%, #42a5f5 100%); /* 医疗蓝渐变 */
  border-radius: 12px;
  padding: 20px 24px;
  min-width: 150px;
  flex: 1;
  color: #fff;
  text-align: center;
  box-shadow: 0 4px 12px rgba(30, 136, 229, 0.3);
  position: relative;  /* 用于定位保存按钮 */
}

/* KPI卡片保存按钮（右上角） */
.kpi-save-btns {
  position: absolute;
  top: 6px;
  right: 6px;
  display: flex;
  gap: 4px;
  opacity: 0;
  transition: opacity 0.2s;
}

.kpi-card:hover .kpi-save-btns {
  opacity: 1;
}

.kpi-save-btns .kpi-save-btn {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.2);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: background 0.2s;
}

.kpi-save-btns .kpi-save-btn:hover {
  background: rgba(255, 255, 255, 0.4);
}

.kpi-save-btns .kpi-save-btn .el-icon {
  font-size: 14px;
  color: #fff;
}

/* KPI内容区域已在模板中使用，不需要额外样式 */

/* 蓝色系配色：第2个卡片（深蓝） */
.kpi-card:nth-child(2) {
  background: linear-gradient(135deg, #1565c0 0%, #1e88e5 100%); /* 深蓝渐变 */
  box-shadow: 0 4px 12px rgba(21, 101, 192, 0.3);
}

/* 蓝色系配色：第3个卡片（青蓝） */
.kpi-card:nth-child(3) {
  background: linear-gradient(135deg, #0097a7 0%, #26c6da 100%); /* 青蓝渐变 */
  box-shadow: 0 4px 12px rgba(0, 151, 167, 0.3);
}

/* 蓝色系配色：第4个卡片（靛蓝） */
.kpi-card:nth-child(4) {
  background: linear-gradient(135deg, #3949ab 0%, #5c6bc0 100%); /* 靛蓝渐变 */
  box-shadow: 0 4px 12px rgba(57, 73, 171, 0.3);
}

/* 蓝色系配色：第5个及更多卡片（天蓝） */
.kpi-card:nth-child(n+5) {
  background: linear-gradient(135deg, #039be5 0%, #4fc3f7 100%); /* 天蓝渐变 */
  box-shadow: 0 4px 12px rgba(3, 155, 229, 0.3);
}

.kpi-value {
  font-size: 28px;
  font-weight: bold;
  margin-bottom: 8px;
}

.kpi-title {
  font-size: 14px;
  opacity: 0.9;
}

.kpi-clickable {
  cursor: pointer;
  transition: transform 0.2s, box-shadow 0.2s;
}

.kpi-clickable:hover {
  transform: translateY(-3px);
  box-shadow: 0 6px 16px rgba(30, 136, 229, 0.5);
}

.kpi-click-hint {
  font-size: 11px;
  opacity: 0.7;
  margin-top: 6px;
}

/* 同比环比样式 */
.kpi-compare {
  display: flex;
  justify-content: center;
  gap: 12px;
  margin-top: 8px;
  font-size: 13px;
  font-weight: bold;
}

.compare-item {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  padding: 3px 8px;
  border-radius: 4px;
  background: rgba(255, 255, 255, 0.85);
  color: #333333; /* 黑色文字 */
}

.compare-arrow {
  font-weight: bold;
  font-size: 14px;
}

.compare-up {
  color: #2e7d32; /* 深绿色表示增长 */
}

.compare-down {
  color: #c62828; /* 深红色表示下降 */
}

.compare-flat {
  color: #666666; /* 灰色表示持平 */
}

/* 维度切换栏 */
.dimension-bar {
  display: flex;
  align-items: center;
  margin-bottom: 16px;
  padding: 12px 16px;
  background: #f5f7fa;
  border-radius: 8px;
}

.bar-label {
  color: #606266;
  font-size: 14px;
  margin-right: 8px;
}

/* 图表网格 */
.charts-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
  gap: 16px;
  margin-top: 16px;
}

.chart-item {
  background: #fff;
  border: 1px solid #ebeef5;
  border-radius: 8px;
  padding: 12px;
}

/* 图表头部工具栏 */
.chart-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
  flex-wrap: wrap;
  gap: 8px;
}

.chart-tools {
  display: flex;
  align-items: center;
  gap: 8px;
}

.chart-title {
  font-size: 14px;
  font-weight: 500;
  color: #303133;
}

.table-container {
  max-height: 320px;
  overflow: auto;
}

.chart-error {
  color: #f56c6c;
  text-align: center;
  padding: 40px;
}

.kpi-error {
  background: linear-gradient(135deg, #f56c6c 0%, #c45656 100%) !important;
}

/* 全屏弹窗样式 */
.fullscreen-tools {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  padding: 12px;
  background: #f5f7fa;
  border-radius: 8px;
}

.fullscreen-chart-container {
  height: 65vh;
  width: 100%;
}

.fullscreen-table-container {
  max-height: 65vh;
  overflow: auto;
}

/* SQL列表样式 */
.sql-list {
  max-height: 60vh;
  overflow-y: auto;
}

.sql-item {
  margin-bottom: 16px;
}

.sql-title {
  font-weight: bold;
  margin-bottom: 8px;
  color: #409eff;
}

/* 提示词弹窗样式 */
.prompt-dialog-content {
  max-height: 70vh;
  overflow-y: auto;
}

.prompt-phase {
  margin-bottom: 20px;
  padding: 16px;
  background: #f5f7fa;
  border-radius: 8px;
}

.phase-header {
  margin-bottom: 12px;
}

.phase-content {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.prompt-section {
  background: #fff;
  padding: 12px;
  border-radius: 4px;
}

.section-title {
  font-weight: bold;
  margin-bottom: 8px;
  color: #303133;
}

/* ==================== 下钻功能样式 ==================== */

/* 面包屑导航 */
.drill-breadcrumbs {
  display: flex;
  align-items: center;
  padding: 12px 16px;
  background: linear-gradient(90deg, #e6f7ff 0%, #f0f9ff 100%);
  border-radius: 8px;
  margin-bottom: 16px;
  border-left: 4px solid #1890ff;
}

.drill-breadcrumbs .el-breadcrumb {
  flex: 1;
}

.drill-breadcrumbs .el-breadcrumb__item.clickable {
  cursor: pointer;
}

.drill-breadcrumbs .el-breadcrumb__item.clickable:hover .el-breadcrumb__inner {
  color: #1890ff;
  text-decoration: underline;
}

.drill-breadcrumbs .el-breadcrumb__item.current .el-breadcrumb__inner {
  font-weight: bold;
  color: #1890ff;
}

/* 下钻菜单遮罩 */
.drill-menu-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 3000;
  background: rgba(0, 0, 0, 0.1);
}

/* 下钻菜单 */
.drill-menu {
  position: fixed;
  min-width: 200px;
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.15);
  overflow: hidden;
  animation: drillMenuFadeIn 0.2s ease;
  transform: translate(-50%, 10px);
}

@keyframes drillMenuFadeIn {
  from {
    opacity: 0;
    transform: translate(-50%, 20px);
  }
  to {
    opacity: 1;
    transform: translate(-50%, 10px);
  }
}

.drill-menu-header {
  padding: 14px 16px;
  background: linear-gradient(135deg, #1890ff 0%, #096dd9 100%);
  color: #fff;
}

.drill-target {
  display: block;
  font-size: 16px;
  font-weight: bold;
  margin-bottom: 4px;
}

.drill-hint {
  font-size: 12px;
  opacity: 0.85;
}

.drill-menu-options {
  padding: 8px 0;
}

.drill-option {
  display: flex;
  align-items: center;
  padding: 12px 16px;
  cursor: pointer;
  transition: all 0.2s;
}

.drill-option:hover {
  background: #f0f9ff;
  color: #1890ff;
}

.drill-icon {
  font-size: 18px;
  margin-right: 12px;
}

.drill-label {
  font-size: 14px;
}

/* 明细数据弹窗样式 */
.detail-dialog .el-dialog__body {
  padding: 16px 20px;
}

.detail-content {
  min-height: 400px;
}

.detail-filter-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 16px;
  padding: 12px 16px;
  background: #f5f7fa;
  border-radius: 6px;
}

.filter-info {
  display: flex;
  align-items: center;
  gap: 12px;
}

.detail-total {
  color: #666;
  font-size: 13px;
}

.column-filters {
  display: flex;
  align-items: center;
  gap: 8px;
}

.filter-badge {
  margin-left: 4px;
}

.filter-popover {
  max-height: 350px;
}

.filter-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-bottom: 12px;
  margin-bottom: 12px;
  border-bottom: 1px solid #ebeef5;
  font-weight: 500;
}

.filter-item {
  margin-bottom: 12px;
}

.filter-label {
  font-size: 12px;
  color: #606266;
  margin-bottom: 4px;
}

.column-header-with-filter {
  display: flex;
  align-items: center;
  gap: 4px;
}

.filter-active-icon {
  color: #409eff;
  cursor: pointer;
  font-size: 14px;
}

.filter-active-icon:hover {
  color: #f56c6c;
}

.detail-pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid #ebeef5;
}

/* 模式标签样式 */
.mode-badge {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  padding: 8px 12px;
  background: #f5f7fa;
  border-radius: 6px;
}

.mode-reason {
  color: #666;
  font-size: 13px;
}

/* Markdown内容样式 */
.markdown-answer {
  padding: 16px;
  background: #fff;
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  line-height: 1.8;
}

.markdown-answer h2, .markdown-answer h3, .markdown-answer h4 {
  margin: 16px 0 8px;
  color: #303133;
}

.markdown-answer p {
  margin: 8px 0;
}

.markdown-answer code {
  padding: 2px 6px;
  background: #f5f5f5;
  border-radius: 4px;
  font-family: 'Consolas', monospace;
  font-size: 13px;
}

.markdown-answer li {
  margin: 4px 0;
  list-style: disc;
  margin-left: 20px;
}

/* 流式输出光标动画 */
.streaming-cursor {
  display: inline-block;
  color: #409eff;
  animation: blink 1s infinite;
  font-weight: bold;
}

@keyframes blink {
  0%, 50% { opacity: 1; }
  51%, 100% { opacity: 0; }
}

/* 患者卡片列表样式 */
.patient-cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 16px;
  margin-top: 16px;
}

.patient-card {
  background: #fff;
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  padding: 16px;
  cursor: pointer;
  transition: all 0.3s;
}

.patient-card:hover {
  border-color: #409eff;
  box-shadow: 0 4px 12px rgba(64, 158, 255, 0.15);
  transform: translateY(-2px);
}

.patient-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
  padding-bottom: 12px;
  border-bottom: 1px solid #f0f0f0;
}

.patient-name {
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.patient-age {
  color: #666;
  font-size: 14px;
}

.patient-info {
  margin-bottom: 12px;
}

.info-row {
  display: flex;
  margin-bottom: 8px;
  font-size: 14px;
}

.info-label {
  color: #909399;
  width: 80px;
  flex-shrink: 0;
}

.info-value {
  color: #303133;
}

.info-value.diagnosis {
  color: #e6a23c;
}

.patient-action {
  text-align: right;
  padding-top: 8px;
  border-top: 1px solid #f0f0f0;
}

/* ============ 报表模式样式 ============ */
.report-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  padding: 8px 12px;
  background: #fff;
  border-radius: 6px;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.06);
}
.report-toolbar-left {
  display: flex;
  gap: 8px;
}
.report-row-count {
  color: #909399;
  font-size: 13px;
}
.report-tabs {
  margin-bottom: 0;
}
.report-tabs .el-tabs__content {
  display: none;
}
.report-table-wrapper {
  background: #fff;
  border-radius: 0 0 6px 6px;
  overflow: hidden;
}
.report-table-wrapper .el-table {
  font-size: 13px;
}
.report-number {
  font-variant-numeric: tabular-nums;
}
</style>

