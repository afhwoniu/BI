<template>
  <div class="ai-config-panel">
    <!-- LLM配置 -->
    <el-divider content-position="left">LLM大模型配置</el-divider>

    <!-- 服务商卡片列表 -->
    <div class="provider-cards">
      <div
        v-for="p in providers"
        :key="p.key"
        class="provider-card"
        :class="{ 'is-active': llmConfig.provider === p.key }"
        @click="onProviderCardClick(p.key)"
      >
        <div class="provider-header">
          <span class="provider-name">{{ p.name }}</span>
          <el-tag v-if="llmConfig.provider === p.key" type="success" size="small">当前使用</el-tag>
        </div>
        <div class="provider-desc">{{ p.description }}</div>
        <div class="provider-status">
          <el-tag v-if="providerApiKeys[p.key]" type="success" size="small">已配置</el-tag>
          <el-tag v-else type="info" size="small">未配置</el-tag>
        </div>
      </div>
    </div>

    <el-form label-width="120px" :model="llmConfig" style="margin-top: 16px;">
      <el-row :gutter="20">
        <el-col :span="12">
          <el-form-item label="模型">
            <el-select v-model="llmConfig.model" style="width: 100%">
              <el-option v-for="m in currentModels" :key="m.value" :label="m.label" :value="m.value">
                <span>{{ m.label }}</span>
                <span style="color: #999; font-size: 12px; margin-left: 8px">{{ m.description }}</span>
              </el-option>
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="API Key">
            <el-input
              v-model="providerApiKeys[llmConfig.provider]"
              type="password"
              show-password
              placeholder="输入API密钥"
              @input="onApiKeyInput"
            />
          </el-form-item>
        </el-col>
      </el-row>

      <el-row :gutter="20">
        <el-col :span="12">
          <el-form-item label="API地址">
            <el-input v-model="llmConfig.baseUrl" disabled>
              <template #suffix>
                <el-tooltip content="根据服务商自动设置">
                  <el-icon><InfoFilled /></el-icon>
                </el-tooltip>
              </template>
            </el-input>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label=" ">
            <el-button type="success" :loading="testingLlm" @click="testLlm">
              <el-icon><Connection /></el-icon>
              测试LLM连接
            </el-button>
          </el-form-item>
        </el-col>
      </el-row>


    </el-form>
    
    <!-- Embedding配置 -->
    <el-divider content-position="left">Embedding向量化配置</el-divider>
    
    <el-form label-width="120px" :model="embeddingConfig">
      <el-row :gutter="20">
        <el-col :span="12">
          <el-form-item label="服务商">
            <el-select v-model="embeddingConfig.provider" @change="(val: string) => onEmbeddingProviderChange(val, previousEmbeddingProvider)" style="width: 100%">
              <el-option v-for="p in embeddingProviders" :key="p.key" :label="p.name" :value="p.key" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="模型">
            <el-select v-model="embeddingConfig.model" style="width: 100%">
              <el-option v-for="m in currentEmbeddingModels" :key="m.value" :label="m.label" :value="m.value" />
            </el-select>
          </el-form-item>
        </el-col>
      </el-row>
      
      <el-row :gutter="20">
        <el-col :span="12">
          <el-form-item label="API地址">
            <el-input v-model="embeddingConfig.baseUrl" disabled />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="API Key">
            <el-input v-model="embeddingConfig.apiKey" type="password" show-password placeholder="输入API密钥" />
          </el-form-item>
        </el-col>
      </el-row>
      
      <el-row>
        <el-col :span="24">
          <el-form-item label=" ">
            <el-button type="success" :loading="testingEmbedding" @click="testEmbedding">
              <el-icon><Connection /></el-icon>
              测试Embedding连接
            </el-button>
          </el-form-item>
        </el-col>
      </el-row>
    </el-form>

    <!-- RAG检索增强配置 -->
    <el-divider content-position="left">RAG检索增强配置</el-divider>

    <el-form label-width="140px" :model="ragConfig">
      <el-row :gutter="20">
        <el-col :span="8">
          <el-form-item label="启用RAG检索">
            <el-switch
              v-model="ragConfig.enabled"
              active-text="开启"
              inactive-text="关闭"
            />
            <el-tooltip content="开启后，智能分析会先检索知识库获取相关上下文，增强AI回答的准确性" placement="top">
              <el-icon style="margin-left: 8px; color: #909399; cursor: help;"><QuestionFilled /></el-icon>
            </el-tooltip>
          </el-form-item>
        </el-col>
        <el-col :span="8">
          <el-form-item label="检索数量(TopK)">
            <el-input-number
              v-model="ragConfig.topK"
              :min="1"
              :max="20"
              :disabled="!ragConfig.enabled"
              style="width: 100%"
            />
          </el-form-item>
        </el-col>
        <el-col :span="8">
          <el-form-item label="最低相似度">
            <el-slider
              v-model="ragConfig.minScore"
              :min="0"
              :max="1"
              :step="0.1"
              :disabled="!ragConfig.enabled"
              show-input
              :show-input-controls="false"
            />
          </el-form-item>
        </el-col>
      </el-row>
      <el-row>
        <el-col :span="24">
          <el-alert
            v-if="ragConfig.enabled"
            title="RAG检索增强已启用"
            type="success"
            description="智能分析时会自动检索指标知识库和文档知识库，将相关内容注入AI提示词中，提高回答的准确性和专业性。"
            :closable="false"
            show-icon
          />
          <el-alert
            v-else
            title="RAG检索增强已关闭"
            type="info"
            description="关闭后智能分析将仅使用数据库Schema信息，不会检索知识库内容。适用于知识库尚未建设或不需要知识增强的场景。"
            :closable="false"
            show-icon
          />
        </el-col>
      </el-row>
    </el-form>

    <!-- SQL字段验证配置 -->
    <el-divider content-position="left">SQL字段验证配置</el-divider>

    <el-form label-width="160px" :model="sqlValidationConfig">
      <el-row :gutter="20">
        <el-col :span="8">
          <el-form-item label="启用SQL字段验证">
            <el-switch
              v-model="sqlValidationConfig.enabled"
              active-text="开启"
              inactive-text="关闭"
            />
            <el-tooltip content="开启后会验证AI生成SQL中的字段是否存在于表结构中，如发现不存在的字段会自动让AI修正" placement="top">
              <el-icon style="margin-left: 8px; color: #909399; cursor: help;"><QuestionFilled /></el-icon>
            </el-tooltip>
          </el-form-item>
        </el-col>
        <el-col :span="8">
          <el-form-item label="最大重试次数">
            <el-input-number
              v-model="sqlValidationConfig.maxRetry"
              :min="1"
              :max="5"
              :disabled="!sqlValidationConfig.enabled"
              style="width: 100%"
            />
          </el-form-item>
        </el-col>
      </el-row>
      <el-row>
        <el-col :span="24">
          <el-alert
            v-if="sqlValidationConfig.enabled"
            title="SQL字段验证已启用"
            type="success"
            description="AI生成SQL后会自动检查字段是否存在于表结构中。如发现不存在的字段，会构建修正提示让AI重新生成正确的SQL，最多重试指定次数。"
            :closable="false"
            show-icon
          />
          <el-alert
            v-else
            title="SQL字段验证已关闭"
            type="info"
            description="关闭后AI生成的SQL将直接执行，如果包含不存在的字段会导致SQL执行错误。建议保持开启以提高AI生成SQL的准确性。"
            :closable="false"
            show-icon
          />
        </el-col>
      </el-row>
    </el-form>

    <!-- ASR语音识别配置 -->
    <el-divider content-position="left">语音识别(ASR)配置</el-divider>

    <el-form label-width="140px" :model="asrConfig">
      <el-row :gutter="20">
        <el-col :span="8">
          <el-form-item label="启用语音识别">
            <el-switch
              v-model="asrConfig.enabled"
              active-text="开启"
              inactive-text="关闭"
            />
            <el-tooltip content="开启后可在智能分析页面使用语音输入" placement="top">
              <el-icon style="margin-left: 8px; color: #909399; cursor: help;"><QuestionFilled /></el-icon>
            </el-tooltip>
          </el-form-item>
        </el-col>
        <el-col :span="8">
          <el-form-item label="服务商">
            <el-select v-model="asrConfig.provider" :disabled="!asrConfig.enabled" style="width: 100%">
              <el-option label="智谱AI" value="zhipu" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="8">
          <el-form-item label="模型">
            <el-select v-model="asrConfig.model" :disabled="!asrConfig.enabled" style="width: 100%">
              <el-option label="GLM-4-Voice" value="glm-4-voice" />
              <el-option label="GLM-ASR-2512" value="glm-asr-2512" />
            </el-select>
          </el-form-item>
        </el-col>
      </el-row>
      <el-row :gutter="20">
        <el-col :span="12">
          <el-form-item label="API Key">
            <el-input
              v-model="asrConfig.apiKey"
              type="password"
              show-password
              :disabled="!asrConfig.enabled"
              placeholder="留空则复用智谱LLM的API Key"
            />
          </el-form-item>
        </el-col>
        <el-col :span="6">
          <el-form-item label="流式识别">
            <el-switch
              v-model="asrConfig.streamEnabled"
              :disabled="!asrConfig.enabled"
              active-text="开启"
              inactive-text="关闭"
            />
          </el-form-item>
        </el-col>
        <el-col :span="6">
          <el-form-item label="识别语言">
            <el-select v-model="asrConfig.language" :disabled="!asrConfig.enabled" style="width: 100%">
              <el-option label="中文" value="zh" />
              <el-option label="英文" value="en" />
            </el-select>
          </el-form-item>
        </el-col>
      </el-row>
      <el-row :gutter="20">
        <el-col :span="12">
          <el-form-item label=" ">
            <el-button type="success" :loading="testingAsr" :disabled="!asrConfig.enabled" @click="testAsr">
              <el-icon><Connection /></el-icon>
              测试ASR连接
            </el-button>
          </el-form-item>
        </el-col>
      </el-row>
      <el-row>
        <el-col :span="24">
          <el-alert
            v-if="asrConfig.enabled"
            title="语音识别已启用"
            type="success"
            description="智能分析页面将显示语音输入按钮，点击后可通过麦克风录音并自动转为文字。"
            :closable="false"
            show-icon
          />
          <el-alert
            v-else
            title="语音识别已关闭"
            type="info"
            description="关闭后智能分析页面不显示语音输入按钮。"
            :closable="false"
            show-icon
          />
        </el-col>
      </el-row>
    </el-form>

    <!-- 语音唤醒配置 -->
    <el-divider content-position="left">语音唤醒配置</el-divider>

    <el-form label-width="140px" :model="voiceWakeupConfig">
      <el-row :gutter="20">
        <el-col :span="8">
          <el-form-item label="启用语音唤醒">
            <el-switch
              v-model="voiceWakeupConfig.enabled"
              :disabled="!asrConfig.enabled"
              active-text="开启"
              inactive-text="关闭"
            />
            <el-tooltip content="开启后可通过唤醒词激活语音输入，需先启用语音识别" placement="top">
              <el-icon style="margin-left: 8px; color: #909399; cursor: help;"><QuestionFilled /></el-icon>
            </el-tooltip>
          </el-form-item>
        </el-col>
        <el-col :span="8">
          <el-form-item label="唤醒词">
            <el-input
              v-model="voiceWakeupConfig.wakeupWordsText"
              :disabled="!voiceWakeupConfig.enabled"
              placeholder="多个用逗号分隔，如：你好助手,小助手"
            />
          </el-form-item>
        </el-col>
        <el-col :span="8">
          <el-form-item label="指令词">
            <el-input
              v-model="voiceWakeupConfig.commandWordsText"
              :disabled="!voiceWakeupConfig.enabled"
              placeholder="说出指令词自动发送，如：执行,查询"
            />
          </el-form-item>
        </el-col>
      </el-row>
    </el-form>

    <!-- 业务AI模型配置 -->
    <el-divider content-position="left">业务AI模型配置</el-divider>
    <el-alert
      type="info"
      :closable="false"
      show-icon
      style="margin-bottom: 16px"
    >
      <template #default>
        不同业务场景可使用不同的AI模型。关闭"使用默认配置"后可为该业务单独配置服务商和模型。
      </template>
    </el-alert>

    <el-table :data="bizConfigs" border style="width: 100%">
      <el-table-column prop="label" label="业务场景" width="180" />
      <el-table-column label="使用默认配置" width="140">
        <template #default="{ row }">
          <el-switch v-model="row.useDefault" @change="syncToConfigs" />
        </template>
      </el-table-column>
      <el-table-column label="服务商" width="160">
        <template #default="{ row }">
          <el-select v-model="row.provider" :disabled="row.useDefault" style="width: 100%" @change="onBizProviderChange(row)">
            <el-option v-for="p in providers" :key="p.key" :label="p.name" :value="p.key" />
          </el-select>
        </template>
      </el-table-column>
      <el-table-column label="模型">
        <template #default="{ row }">
          <el-select v-model="row.model" :disabled="row.useDefault" style="width: 100%" @change="syncToConfigs">
            <el-option v-for="m in (modelsByProvider[row.provider] || [])" :key="m.value" :label="m.label" :value="m.value" />
          </el-select>
        </template>
      </el-table-column>
      <el-table-column label="说明" width="200">
        <template #default="{ row }">
          <span style="color: #909399; font-size: 12px">{{ row.description }}</span>
        </template>
      </el-table-column>
    </el-table>

    <!-- 自定义提示词配置 -->
    <div class="section-title" style="margin-top: 20px; color: #409eff; font-weight: bold;">自定义AI提示词</div>
    <el-form label-width="100px">
      <el-row>
        <el-col :span="24">
          <el-form-item label="提示词">
            <el-input
              v-model="customPrompt"
              type="textarea"
              :rows="4"
              placeholder="输入自定义提示词，将在智能分析时追加到系统提示词中。&#10;例如：&#10;- 本系统主要用于医院数据分析&#10;- 涉及金额的字段单位为元&#10;- 日期格式为YYYY-MM-DD"
            />
          </el-form-item>
        </el-col>
      </el-row>
    </el-form>

    <!-- 测试结果弹窗 -->
    <el-dialog v-model="showTestResult" title="测试结果" width="500px">
      <el-result :icon="testResult.success ? 'success' : 'error'" :title="testResult.title">
        <template #sub-title>
          <p>{{ testResult.message }}</p>
          <p v-if="testResult.response" style="margin-top: 10px; color: #666; font-size: 12px;">
            响应: {{ testResult.response }}
          </p>
        </template>
      </el-result>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { InfoFilled, Connection, QuestionFilled } from '@element-plus/icons-vue'
import { testLlmConnection, testEmbeddingConnection, testAsrConnection } from '@/api/config'

// Props
const props = defineProps<{
  configs: any[]
}>()

// Emits
const emit = defineEmits<{
  (e: 'update:configs', configs: any[]): void
}>()

// 服务商配置（添加 Kimi/Moonshot）
const providers = [
  { key: 'deepseek', name: 'DeepSeek', description: '深度求索', baseUrl: 'https://api.deepseek.com' },
  { key: 'qwen', name: '通义千问', description: '阿里云', baseUrl: 'https://dashscope.aliyuncs.com/compatible-mode/v1' },
  { key: 'zhipu', name: '智谱AI', description: '智谱清言', baseUrl: 'https://open.bigmodel.cn/api/paas/v4' },
  { key: 'kimi', name: 'Kimi', description: '月之暗面', baseUrl: 'https://api.moonshot.cn/v1' },
]

// 模型列表（添加 Kimi K2.5 系列）
const modelsByProvider: Record<string, { value: string; label: string; description: string }[]> = {
  deepseek: [
    { value: 'deepseek-chat', label: 'DeepSeek Chat', description: '通用对话' },
    { value: 'deepseek-coder', label: 'DeepSeek Coder', description: '代码生成' },
    { value: 'deepseek-reasoner', label: 'DeepSeek R1', description: '推理增强' },
  ],
  qwen: [
    { value: 'qwen3.5-plus', label: 'Qwen3.5 Plus', description: '思考模式·原生多模态旗舰' },
    { value: 'qwen3.5-plus-instant', label: 'Qwen3.5 Plus Instant', description: '非思考·快速响应' },
    { value: 'qwen3-max', label: 'Qwen3 Max', description: '最新旗舰' },
    { value: 'qwen-max', label: 'Qwen Max', description: '强能力' },
    { value: 'qwen-plus', label: 'Qwen Plus', description: '均衡性能' },
    { value: 'qwen-turbo', label: 'Qwen Turbo', description: '快速响应' },
  ],
  zhipu: [
    { value: 'glm-4.7', label: 'GLM-4.7', description: '最新模型' },
    { value: 'glm-4-plus', label: 'GLM-4 Plus', description: '旗舰模型' },
    { value: 'glm-4', label: 'GLM-4', description: '标准模型' },
    { value: 'glm-4-flash', label: 'GLM-4 Flash', description: '快速响应' },
  ],
  // Kimi/Moonshot 模型列表（月之暗面）
  // kimi-k2.5 支持通过 enable_thinking 参数切换思考/非思考模式
  // kimi-k2.5-instant 为虚拟名称，后端识别后转为 kimi-k2.5 + enable_thinking:false（快速响应）
  kimi: [
    { value: 'kimi-k2.5', label: 'Kimi K2.5 Thinking', description: '深度思考·复杂推理' },
    { value: 'kimi-k2.5-instant', label: 'Kimi K2.5 Instant', description: '非思考·快速响应' },
    { value: 'kimi-k2-thinking', label: 'Kimi K2 Thinking', description: 'K2长思考 256k' },
    { value: 'kimi-k2-thinking-turbo', label: 'Kimi K2 Thinking Turbo', description: 'K2长思考高速版' },
    { value: 'kimi-k2-turbo-preview', label: 'Kimi K2 Turbo', description: '高速版' },
    { value: 'moonshot-v1-128k', label: 'Moonshot 128K', description: '超长上下文' },
    { value: 'moonshot-v1-32k', label: 'Moonshot 32K', description: '长上下文' },
    { value: 'moonshot-v1-8k', label: 'Moonshot 8K', description: '标准版' },
  ],
}

// 每个服务商独立存储的API Key（添加 Kimi）
const providerApiKeys = ref<Record<string, string>>({
  deepseek: '',
  qwen: '',
  zhipu: '',
  kimi: '',
})

// Embedding服务商的API Key
const embeddingProviderApiKeys = ref<Record<string, string>>({
  openai: '',
  zhipu: '',
  qwen: '',
})

// Embedding服务商
const embeddingProviders = [
  { key: 'openai', name: 'OpenAI', baseUrl: 'https://api.openai.com/v1' },
  { key: 'zhipu', name: '智谱AI', baseUrl: 'https://open.bigmodel.cn/api/paas/v4' },
  { key: 'qwen', name: '通义千问', baseUrl: 'https://dashscope.aliyuncs.com/compatible-mode/v1' },
]

const embeddingModelsByProvider: Record<string, { value: string; label: string }[]> = {
  openai: [
    { value: 'text-embedding-3-small', label: 'text-embedding-3-small' },
    { value: 'text-embedding-3-large', label: 'text-embedding-3-large' },
  ],
  zhipu: [
    { value: 'embedding-3', label: 'Embedding-3' },
  ],
  qwen: [
    { value: 'text-embedding-v3', label: 'text-embedding-v3' },
  ],
}

// LLM配置
const llmConfig = ref({
  provider: 'deepseek',
  model: 'deepseek-chat',
  baseUrl: 'https://api.deepseek.com',
  apiKey: '',
  temperature: 0.3,
  maxTokens: 2000,
})

// Embedding配置
const embeddingConfig = ref({
  provider: 'openai',
  model: 'text-embedding-3-small',
  baseUrl: 'https://api.openai.com/v1',
  apiKey: '',
})

// RAG检索配置
const ragConfig = ref({
  enabled: true,
  topK: 5,
  minScore: 0.5,
})

// SQL字段验证配置
const sqlValidationConfig = ref({
  enabled: true,
  maxRetry: 2,
})

// ASR语音识别配置
const asrConfig = ref({
  enabled: true,
  provider: 'zhipu',
  model: 'glm-4-voice',
  apiKey: '',
  streamEnabled: true,
  language: 'zh',
})

// 语音唤醒配置
const voiceWakeupConfig = ref({
  enabled: false,
  wakeupWordsText: '你好助手,嘿助手,小助手',
  commandWordsText: '执行,发送,查询,开始,分析',
})

// 业务AI配置列表
const bizConfigs = ref([
  { key: 'bi', label: '智能BI分析', useDefault: true, provider: 'deepseek', model: 'deepseek-chat', description: 'SQL生成、图表推荐' },
  { key: 'hz360', label: '患者360', useDefault: true, provider: 'deepseek', model: 'deepseek-chat', description: '患者信息检索分析' },
  { key: 'search', label: 'AI检索增强', useDefault: true, provider: 'deepseek', model: 'deepseek-chat', description: '知识库检索问答' },
  { key: 'docgen', label: 'PPT/Word生成', useDefault: true, provider: 'deepseek', model: 'deepseek-chat', description: '文档大纲和内容生成' },
])

// 业务服务商变更
const onBizProviderChange = (row: any) => {
  const models = modelsByProvider[row.provider]
  if (models && models.length > 0) {
    row.model = models[0].value
  }
  syncToConfigs()
}

// 自定义提示词
const customPrompt = ref('')

// 当前可选模型
const currentModels = computed(() => modelsByProvider[llmConfig.value.provider] || [])
const currentEmbeddingModels = computed(() => embeddingModelsByProvider[embeddingConfig.value.provider] || [])

// 记录上一个服务商，用于切换时保存API Key
// previousProvider已移除，使用卡片点击模式

// 测试状态
const testingLlm = ref(false)
const testingEmbedding = ref(false)
const testingAsr = ref(false)
const showTestResult = ref(false)
const testResult = ref({ success: false, title: '', message: '', response: '' })

// 点击服务商卡片 - 简化的切换逻辑
const onProviderCardClick = (provider: string) => {
  if (llmConfig.value.provider === provider) return

  llmConfig.value.provider = provider
  const p = providers.find(x => x.key === provider)
  if (p) {
    llmConfig.value.baseUrl = p.baseUrl
    const models = modelsByProvider[provider]
    if (models && models.length > 0) {
      llmConfig.value.model = models[0].value
    }
  }
  syncToConfigs()
}

// API Key输入时同步
const onApiKeyInput = () => {
  syncToConfigs()
}

// Embedding服务商变更
const previousEmbeddingProvider = ref('zhipu')
const onEmbeddingProviderChange = (provider: string, oldProvider?: string) => {
  // 保存当前服务商的API Key
  if (oldProvider && embeddingConfig.value.apiKey) {
    embeddingProviderApiKeys.value[oldProvider] = embeddingConfig.value.apiKey
  }

  const p = embeddingProviders.find(x => x.key === provider)
  if (p) {
    embeddingConfig.value.baseUrl = p.baseUrl
    const models = embeddingModelsByProvider[provider]
    if (models && models.length > 0) {
      embeddingConfig.value.model = models[0].value
    }
    // 加载该服务商的API Key
    embeddingConfig.value.apiKey = embeddingProviderApiKeys.value[provider] || ''
  }
  previousEmbeddingProvider.value = provider
  syncToConfigs()
}

// 从props.configs初始化（使用后端的configKey格式）
const initFromConfigs = () => {
  const getConfigValue = (key: string) => {
    const c = props.configs.find(x => x.configKey === key)
    return c?.configValue || ''
  }

  // 加载各服务商的API Key（从后端存储的独立配置）
  providerApiKeys.value.deepseek = getConfigValue('ai.llm.apiKey.deepseek')
  providerApiKeys.value.qwen = getConfigValue('ai.llm.apiKey.qwen')
  providerApiKeys.value.zhipu = getConfigValue('ai.llm.apiKey.zhipu')
  providerApiKeys.value.kimi = getConfigValue('ai.llm.apiKey.kimi')

  // LLM配置
  const savedProvider = getConfigValue('ai.llm.provider') || 'deepseek'
  llmConfig.value.provider = savedProvider
  llmConfig.value.model = getConfigValue('ai.llm.model') || 'deepseek-chat'
  llmConfig.value.temperature = parseFloat(getConfigValue('ai.llm.temperature')) || 0.3
  llmConfig.value.maxTokens = parseInt(getConfigValue('ai.llm.maxTokens')) || 2000

  // 设置baseUrl
  const p = providers.find(x => x.key === savedProvider)
  if (p) {
    llmConfig.value.baseUrl = p.baseUrl
  }

  // Embedding配置
  const savedEmbeddingProvider = getConfigValue('ai.embedding.provider') || 'zhipu'
  embeddingConfig.value.provider = savedEmbeddingProvider
  embeddingConfig.value.model = getConfigValue('ai.embedding.model') || 'embedding-3'

  // 加载Embedding的API Key
  embeddingProviderApiKeys.value[savedEmbeddingProvider] = getConfigValue('ai.embedding.apiKey')
  embeddingConfig.value.apiKey = embeddingProviderApiKeys.value[savedEmbeddingProvider] || ''
  previousEmbeddingProvider.value = savedEmbeddingProvider

  const ep = embeddingProviders.find(x => x.key === savedEmbeddingProvider)
  if (ep) {
    embeddingConfig.value.baseUrl = ep.baseUrl
  }

  // 自定义提示词
  customPrompt.value = getConfigValue('ai.customPrompt') || ''

  // RAG检索配置
  const ragEnabledStr = getConfigValue('ai.rag.enabled')
  ragConfig.value.enabled = ragEnabledStr === '' || ragEnabledStr === 'true' // 默认开启
  ragConfig.value.topK = parseInt(getConfigValue('ai.rag.topK')) || 5
  ragConfig.value.minScore = parseFloat(getConfigValue('ai.rag.minScore')) || 0.5

  // SQL字段验证配置
  const sqlValidationEnabledStr = getConfigValue('ai.biz.bi.sqlValidation.enabled')
  sqlValidationConfig.value.enabled = sqlValidationEnabledStr === '' || sqlValidationEnabledStr === 'true' // 默认开启
  sqlValidationConfig.value.maxRetry = parseInt(getConfigValue('ai.biz.bi.sqlValidation.maxRetry')) || 2

  // ASR语音识别配置
  const asrEnabledStr = getConfigValue('ai.asr.enabled')
  asrConfig.value.enabled = asrEnabledStr === '' || asrEnabledStr === 'true' // 默认开启
  asrConfig.value.provider = getConfigValue('ai.asr.provider') || 'zhipu'
  asrConfig.value.model = getConfigValue('ai.asr.model') || 'glm-4-voice'
  asrConfig.value.apiKey = getConfigValue('ai.asr.apiKey') || ''
  const streamEnabledStr = getConfigValue('ai.asr.streamEnabled')
  asrConfig.value.streamEnabled = streamEnabledStr === '' || streamEnabledStr === 'true' // 默认开启
  asrConfig.value.language = getConfigValue('ai.asr.language') || 'zh'

  // 语音唤醒配置
  voiceWakeupConfig.value.enabled = getConfigValue('ai.voice.wakeup.enabled') === 'true'
  const wakeupWordsJson = getConfigValue('ai.voice.wakeup.words')
  if (wakeupWordsJson) {
    try {
      const words = JSON.parse(wakeupWordsJson)
      voiceWakeupConfig.value.wakeupWordsText = words.join(',')
    } catch { /* ignore */ }
  }
  const commandWordsJson = getConfigValue('ai.voice.command.words')
  if (commandWordsJson) {
    try {
      const words = JSON.parse(commandWordsJson)
      voiceWakeupConfig.value.commandWordsText = words.join(',')
    } catch { /* ignore */ }
  }

  // 业务AI配置
  const loadBizConfig = (key: string, bizItem: any) => {
    const useDefaultStr = getConfigValue(`ai.biz.${key}.useDefault`)
    bizItem.useDefault = useDefaultStr === '' || useDefaultStr === 'true'
    bizItem.provider = getConfigValue(`ai.biz.${key}.provider`) || 'deepseek'
    bizItem.model = getConfigValue(`ai.biz.${key}.model`) || 'deepseek-chat'
  }
  bizConfigs.value.forEach(item => loadBizConfig(item.key, item))
}

// 同步到configs
const syncToConfigs = () => {
  const updateOrAddConfig = (key: string, value: string, displayName?: string) => {
    const c = props.configs.find(x => x.configKey === key)
    if (c) {
      c.configValue = value
    } else {
      props.configs.push({
        configKey: key,
        configValue: value,
        configGroup: 'ai',
        configType: key.includes('apiKey') ? 'password' : 'string',
        displayName: displayName || key,
        remark: '',
        isEncrypted: key.includes('apiKey'),
      })
    }
  }

  // 保存Embedding当前服务商的API Key
  if (embeddingConfig.value.apiKey) {
    embeddingProviderApiKeys.value[embeddingConfig.value.provider] = embeddingConfig.value.apiKey
  }

  // LLM配置
  updateOrAddConfig('ai.llm.provider', llmConfig.value.provider, 'LLM服务商')
  updateOrAddConfig('ai.llm.model', llmConfig.value.model, 'LLM模型')
  updateOrAddConfig('ai.llm.baseUrl', llmConfig.value.baseUrl, 'LLM API地址')
  updateOrAddConfig('ai.llm.temperature', String(llmConfig.value.temperature), '温度参数')
  updateOrAddConfig('ai.llm.maxTokens', String(llmConfig.value.maxTokens), '最大Token数')

  // 保存各服务商的API Key（直接从providerApiKeys获取）
  updateOrAddConfig('ai.llm.apiKey.deepseek', providerApiKeys.value.deepseek || '', 'DeepSeek API Key')
  updateOrAddConfig('ai.llm.apiKey.qwen', providerApiKeys.value.qwen || '', '通义千问 API Key')
  updateOrAddConfig('ai.llm.apiKey.zhipu', providerApiKeys.value.zhipu || '', '智谱AI API Key')
  updateOrAddConfig('ai.llm.apiKey.kimi', providerApiKeys.value.kimi || '', 'Kimi/Moonshot API Key')

  // 保留兼容性：当前选中服务商的API Key也存到ai.llm.apiKey
  const currentApiKey = providerApiKeys.value[llmConfig.value.provider] || ''
  updateOrAddConfig('ai.llm.apiKey', currentApiKey, 'LLM API密钥')

  // Embedding配置
  updateOrAddConfig('ai.embedding.provider', embeddingConfig.value.provider, 'Embedding服务商')
  updateOrAddConfig('ai.embedding.model', embeddingConfig.value.model, 'Embedding模型')
  updateOrAddConfig('ai.embedding.baseUrl', embeddingConfig.value.baseUrl, 'Embedding API地址')
  updateOrAddConfig('ai.embedding.apiKey', embeddingConfig.value.apiKey, 'Embedding API密钥')

  // 自定义提示词
  updateOrAddConfig('ai.customPrompt', customPrompt.value, 'AI自定义提示词')

  // RAG检索配置
  updateOrAddConfig('ai.rag.enabled', ragConfig.value.enabled ? 'true' : 'false', '启用RAG检索增强')
  updateOrAddConfig('ai.rag.topK', String(ragConfig.value.topK), 'RAG检索数量')
  updateOrAddConfig('ai.rag.minScore', String(ragConfig.value.minScore), 'RAG最低相似度')

  // SQL字段验证配置
  updateOrAddConfig('ai.biz.bi.sqlValidation.enabled', sqlValidationConfig.value.enabled ? 'true' : 'false', '启用SQL字段验证')
  updateOrAddConfig('ai.biz.bi.sqlValidation.maxRetry', String(sqlValidationConfig.value.maxRetry), 'SQL验证最大重试次数')

  // ASR语音识别配置
  updateOrAddConfig('ai.asr.enabled', asrConfig.value.enabled ? 'true' : 'false', '启用语音识别')
  updateOrAddConfig('ai.asr.provider', asrConfig.value.provider, 'ASR服务商')
  updateOrAddConfig('ai.asr.model', asrConfig.value.model, 'ASR模型')
  updateOrAddConfig('ai.asr.apiKey', asrConfig.value.apiKey, 'ASR API密钥')
  updateOrAddConfig('ai.asr.streamEnabled', asrConfig.value.streamEnabled ? 'true' : 'false', '启用流式识别')
  updateOrAddConfig('ai.asr.language', asrConfig.value.language, '识别语言')

  // 语音唤醒配置
  updateOrAddConfig('ai.voice.wakeup.enabled', voiceWakeupConfig.value.enabled ? 'true' : 'false', '启用语音唤醒')
  const wakeupWords = voiceWakeupConfig.value.wakeupWordsText.split(',').map(w => w.trim()).filter(w => w)
  updateOrAddConfig('ai.voice.wakeup.words', JSON.stringify(wakeupWords), '唤醒词')
  const commandWords = voiceWakeupConfig.value.commandWordsText.split(',').map(w => w.trim()).filter(w => w)
  updateOrAddConfig('ai.voice.command.words', JSON.stringify(commandWords), '指令词')

  // 业务AI配置
  bizConfigs.value.forEach(item => {
    updateOrAddConfig(`ai.biz.${item.key}.useDefault`, item.useDefault ? 'true' : 'false', `${item.label}-使用默认配置`)
    updateOrAddConfig(`ai.biz.${item.key}.provider`, item.provider, `${item.label}-服务商`)
    updateOrAddConfig(`ai.biz.${item.key}.model`, item.model, `${item.label}-模型`)
  })

  emit('update:configs', props.configs)
}

// 监听配置变化
watch([llmConfig, embeddingConfig, customPrompt, ragConfig, sqlValidationConfig, asrConfig, voiceWakeupConfig, bizConfigs], syncToConfigs, { deep: true })

// 测试LLM
const testLlm = async () => {
  const currentApiKey = providerApiKeys.value[llmConfig.value.provider]
  if (!currentApiKey) {
    ElMessage.warning('请先输入API Key')
    return
  }
  testingLlm.value = true
  try {
    const res = await testLlmConnection({
      provider: llmConfig.value.provider,
      model: llmConfig.value.model,
      baseUrl: llmConfig.value.baseUrl,
      apiKey: currentApiKey,
    })
    if (res.code === 0 && res.data) {
      testResult.value = {
        success: res.data.success,
        title: res.data.success ? 'LLM连接成功' : 'LLM连接失败',
        message: res.data.message,
        response: res.data.response || '',
      }
    } else {
      testResult.value = { success: false, title: '测试失败', message: res.message, response: '' }
    }
    showTestResult.value = true
  } catch (e: any) {
    ElMessage.error(e.message || '测试失败')
  } finally {
    testingLlm.value = false
  }
}

// 测试Embedding
const testEmbedding = async () => {
  if (!embeddingConfig.value.apiKey) {
    ElMessage.warning('请先输入API Key')
    return
  }
  testingEmbedding.value = true
  try {
    const res = await testEmbeddingConnection({
      provider: embeddingConfig.value.provider,
      model: embeddingConfig.value.model,
      baseUrl: embeddingConfig.value.baseUrl,
      apiKey: embeddingConfig.value.apiKey,
    })
    if (res.code === 0 && res.data) {
      testResult.value = {
        success: res.data.success,
        title: res.data.success ? 'Embedding连接成功' : 'Embedding连接失败',
        message: res.data.message,
        response: res.data.dimensions ? `向量维度: ${res.data.dimensions}` : '',
      }
    } else {
      testResult.value = { success: false, title: '测试失败', message: res.message, response: '' }
    }
    showTestResult.value = true
  } catch (e: any) {
    ElMessage.error(e.message || '测试失败')
  } finally {
    testingEmbedding.value = false
  }
}

// 测试ASR连接
const testAsr = async () => {
  testingAsr.value = true
  try {
    // 获取ASR API Key，如果为空则尝试使用智谱LLM的Key
    let apiKey = asrConfig.value.apiKey
    if (!apiKey) {
      apiKey = providerApiKeys.value.zhipu || ''
    }
    if (!apiKey) {
      ElMessage.warning('请先配置ASR API Key或智谱LLM API Key')
      testingAsr.value = false
      return
    }

    const res = await testAsrConnection({
      provider: asrConfig.value.provider,
      model: asrConfig.value.model,
      apiKey: apiKey,
    })
    if (res.code === 0 && res.data) {
      testResult.value = {
        success: res.data.success,
        title: res.data.success ? 'ASR连接成功' : 'ASR连接失败',
        message: res.data.message,
        response: '',
      }
    } else {
      testResult.value = { success: false, title: '测试失败', message: res.message, response: '' }
    }
    showTestResult.value = true
  } catch (e: any) {
    ElMessage.error(e.message || '测试失败')
  } finally {
    testingAsr.value = false
  }
}

// 暴露初始化方法
defineExpose({ initFromConfigs })

onMounted(() => {
  if (props.configs.length > 0) {
    initFromConfigs()
  }
})

watch(() => props.configs, (newVal) => {
  if (newVal.length > 0) {
    initFromConfigs()
  }
}, { immediate: true })
</script>

<style scoped>
.ai-config-panel {
  padding: 10px 0;
}

:deep(.el-divider__text) {
  font-weight: bold;
  color: #409eff;
}

/* 服务商卡片样式 - 响应式网格布局，支持更多服务商 */
.provider-cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 12px;
  margin-bottom: 16px;
}

.provider-card {
  padding: 12px 16px;
  border: 2px solid #e4e7ed;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.3s;
  background: #fff;
  min-height: 80px;
}

.provider-card:hover {
  border-color: #409eff;
  box-shadow: 0 2px 8px rgba(64, 158, 255, 0.2);
}

.provider-card.is-active {
  border-color: #67c23a;
  background: linear-gradient(135deg, #f0f9eb 0%, #fff 100%);
  box-shadow: 0 2px 12px rgba(103, 194, 58, 0.3);
}

.provider-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
}

.provider-name {
  font-size: 15px;
  font-weight: bold;
  color: #303133;
}

.provider-desc {
  font-size: 12px;
  color: #909399;
  margin-bottom: 8px;
}

.provider-status {
  display: flex;
  justify-content: flex-end;
}
</style>

