<template>
  <!-- 语音唤醒组件 - 持续监听麦克风，检测唤醒词 -->
  <div class="voice-wakeup" v-if="enabled">
    <!-- 唤醒状态指示器 -->
    <el-tooltip :content="tooltipContent" placement="top">
      <div 
        class="wakeup-indicator" 
        :class="{ listening: isListening, activated: isActivated }"
        @click="toggleListening"
      >
        <el-icon :size="16">
          <Microphone v-if="!isListening" />
          <Headset v-else />
        </el-icon>
        <span v-if="isActivated" class="activated-text">已唤醒</span>
      </div>
    </el-tooltip>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { Microphone, Headset } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import api from '@/api'

// Props
const props = defineProps<{
  /** 唤醒词列表（外部传入时覆盖后端配置） */
  wakeupWords?: string[]
  /** 是否启用组件显示（不影响自动启动逻辑） */
  enabled?: boolean
}>()

// Emits
const emit = defineEmits<{
  (e: 'wakeup'): void
  (e: 'listeningChange', listening: boolean): void
  (e: 'command', word: string): void
}>()

// 状态
const isListening = ref(false)
const isActivated = ref(false)
const wakeupEnabled = ref(false)   // 后端配置的唤醒开关
const configWakeupWords = ref<string[]>([])  // 后端配置的唤醒词
const configCommandWords = ref<string[]>([]) // 后端配置的指令词

// Web Speech API 实例
let recognition: any = null

// 实际使用的唤醒词（优先 props，其次后端配置，最后默认）
const activeWakeupWords = computed(() =>
  props.wakeupWords?.length ? props.wakeupWords :
  configWakeupWords.value.length ? configWakeupWords.value :
  ['你好助手', '嘿助手', '小助手', '助手']
)

const tooltipContent = computed(() => {
  if (!wakeupEnabled.value) return '语音唤醒未启用'
  if (isActivated.value) return '已唤醒，请说出您的需求'
  if (isListening.value) return `正在监听唤醒词：${activeWakeupWords.value.join('、')}`
  return '点击重新开启语音唤醒'
})

// 从后端获取 ASR + 唤醒配置，如唤醒开启则自动启动
async function initFromConfig() {
  try {
    const res = await api.get('/ai/asr/config')
    if (res.code === 0 && res.data) {
      const data = res.data
      // 读取语音唤醒开关和唤醒词
      wakeupEnabled.value = data.wakeupEnabled ?? false
      if (data.wakeupWords?.length) configWakeupWords.value = data.wakeupWords
      if (data.commandWords?.length) configCommandWords.value = data.commandWords

      console.log('[VoiceWakeup] 配置加载完成:', {
        wakeupEnabled: wakeupEnabled.value,
        wakeupWords: configWakeupWords.value,
        commandWords: configCommandWords.value
      })

      // 如果语音唤醒已启用，自动开始监听
      if (wakeupEnabled.value) {
        await startListening()
      }
    }
  } catch (e) {
    console.warn('[VoiceWakeup] 获取配置失败', e)
  }
}

// 切换监听状态（手动点击）
async function toggleListening() {
  if (isListening.value) {
    stopListening()
  } else {
    await startListening()
  }
}

// 检查浏览器是否支持 Web Speech API
function isSpeechRecognitionSupported(): boolean {
  return !!(window as any).SpeechRecognition || !!(window as any).webkitSpeechRecognition
}

// 开始监听 - 优先使用 Web Speech API
async function startListening() {
  if (isListening.value) return

  if (isSpeechRecognitionSupported()) {
    startWebSpeechListening()
  } else {
    console.warn('[VoiceWakeup] 浏览器不支持 Web Speech API，无法使用语音唤醒')
    ElMessage.warning('当前浏览器不支持语音唤醒，请使用 Chrome 浏览器')
  }
}

// 使用 Web Speech API 实时监听唤醒词
function startWebSpeechListening() {
  const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition
  recognition = new SpeechRecognition()

  recognition.lang = 'zh-CN'
  recognition.continuous = true      // 持续监听，不自动停止
  recognition.interimResults = true  // 返回中间结果，实时响应

  recognition.onstart = () => {
    isListening.value = true
    emit('listeningChange', true)
    console.log('[VoiceWakeup] Web Speech 开始监听，唤醒词:', activeWakeupWords.value)
  }

  recognition.onresult = (event: any) => {
    const results = event.results
    for (let i = event.resultIndex; i < results.length; i++) {
      const transcript = results[i][0].transcript.toLowerCase().replace(/\s/g, '')
      console.log('[VoiceWakeup] 识别到:', transcript, '(finalResult:', results[i].isFinal, ')')

      // 检测唤醒词
      if (!isActivated.value) {
        for (const word of activeWakeupWords.value) {
          if (transcript.includes(word.toLowerCase())) {
            console.log('[VoiceWakeup] 唤醒成功! 触发词:', word)
            isActivated.value = true
            ElMessage.success(`🎤 已唤醒！请说出您的需求`)
            emit('wakeup')
            // 5秒后重置唤醒状态
            setTimeout(() => { isActivated.value = false }, 5000)
            break
          }
        }
      }

      // 已唤醒状态下检测指令词（自动发送）
      if (isActivated.value && results[i].isFinal) {
        for (const cmd of configCommandWords.value) {
          if (transcript.includes(cmd.toLowerCase())) {
            console.log('[VoiceWakeup] 检测到指令词:', cmd)
            emit('command', cmd)
            break
          }
        }
      }
    }
  }

  recognition.onerror = (event: any) => {
    console.warn('[VoiceWakeup] 识别错误:', event.error)
    if (event.error === 'not-allowed') {
      ElMessage.error('麦克风权限被拒绝，请允许麦克风访问')
      isListening.value = false
    } else if (event.error === 'network') {
      // 网络错误时自动重试
      setTimeout(() => { if (wakeupEnabled.value) recognition?.start() }, 2000)
    }
  }

  recognition.onend = () => {
    // 持续监听：结束后自动重启（除非主动停止）
    if (isListening.value && wakeupEnabled.value) {
      setTimeout(() => {
        try { recognition?.start() } catch (e) { /* 忽略已启动的错误 */ }
      }, 300)
    } else {
      isListening.value = false
      emit('listeningChange', false)
    }
  }

  try {
    recognition.start()
  } catch (e) {
    console.error('[VoiceWakeup] 启动失败', e)
  }
}

// 停止监听
function stopListening() {
  if (recognition) {
    recognition.onend = null // 取消自动重启
    recognition.stop()
    recognition = null
  }
  isListening.value = false
  isActivated.value = false
  emit('listeningChange', false)
}

// 暴露方法
defineExpose({
  startListening,
  stopListening,
  isListening,
  isActivated
})

// 生命周期
onMounted(() => {
  initFromConfig()
})

onUnmounted(() => {
  stopListening()
})
</script>

<style scoped lang="scss">
.voice-wakeup {
  display: inline-flex;
  align-items: center;
}

.wakeup-indicator {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 8px;
  border-radius: 12px;
  cursor: pointer;
  transition: all 0.3s;
  background: #f0f0f0;
  color: #666;

  &:hover {
    background: #e0e0e0;
  }

  &.listening {
    background: rgba(64, 158, 255, 0.1);
    color: #409eff;
    animation: pulse 2s infinite;
  }

  &.activated {
    background: rgba(103, 194, 58, 0.2);
    color: #67c23a;
  }
}

.activated-text {
  font-size: 12px;
  font-weight: 500;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.6; }
}
</style>

