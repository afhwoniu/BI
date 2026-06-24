<template>
  <div class="voice-input" v-if="asrEnabled">
    <!-- 语音按钮 -->
    <el-tooltip :content="isRecording ? '点击停止' : '语音输入'" placement="top">
      <el-button
        :type="isRecording ? 'danger' : 'default'"
        :icon="isRecording ? VideoPause : Microphone"
        circle
        @click="toggleRecording"
        :loading="isProcessing"
        :class="{ 'recording': isRecording, 'pulse': isRecording }"
      />
    </el-tooltip>

    <!-- 录音状态指示 -->
    <div v-if="isRecording" class="recording-indicator">
      <div class="wave-container">
        <div class="wave" v-for="i in 5" :key="i"></div>
      </div>
      <span class="duration">{{ formatDuration(recordingDuration) }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { Microphone, VideoPause } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import api from '@/api'

// Props
const props = defineProps<{
  disabled?: boolean
  /** 是否为连续对话模式（识别完后自动继续录音） */
  continuousMode?: boolean
}>()

// Emits
const emit = defineEmits<{
  (e: 'transcribed', text: string): void
  (e: 'error', message: string): void
  /** 录音开始事件 */
  (e: 'recordingStart'): void
  /** 录音结束事件 */
  (e: 'recordingStop'): void
}>()

// 状态
const asrEnabled = ref(false)
const isRecording = ref(false)
const isProcessing = ref(false)
const recordingDuration = ref(0)

// 录音相关
let mediaRecorder: MediaRecorder | null = null
let audioChunks: Blob[] = []
let recordingTimer: number | null = null
let stream: MediaStream | null = null

// 格式化时长
const formatDuration = (seconds: number) => {
  const mins = Math.floor(seconds / 60)
  const secs = seconds % 60
  return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`
}

// 检查ASR是否启用
const checkAsrEnabled = async () => {
  try {
    // 注意：axios响应拦截器已返回response.data，所以res直接就是{code, data, message}
    const res = await api.get('/ai/asr/config')
    console.log('[VoiceInput] ASR配置返回:', res)

    if (res.code === 0) {
      asrEnabled.value = res.data?.enabled ?? false
      console.log('[VoiceInput] ASR启用状态:', asrEnabled.value)
    }
  } catch (e) {
    console.error('[VoiceInput] 获取ASR配置失败', e)
    asrEnabled.value = false
  }
}

// 切换录音状态
const toggleRecording = async () => {
  if (props.disabled) return

  if (isRecording.value) {
    stopRecording()
  } else {
    await startRecording()
  }
}

// 开始录音
const startRecording = async () => {
  try {
    // 请求麦克风权限
    stream = await navigator.mediaDevices.getUserMedia({ audio: true })

    // 确定支持的音频格式
    const mimeType = getSupportedMimeType()

    mediaRecorder = new MediaRecorder(stream, { mimeType })
    audioChunks = []

    mediaRecorder.ondataavailable = (event) => {
      if (event.data.size > 0) {
        audioChunks.push(event.data)
      }
    }

    mediaRecorder.onstop = async () => {
      // 停止所有音轨
      stream?.getTracks().forEach(track => track.stop())

      if (audioChunks.length > 0) {
        await processAudio()
      }
    }

    mediaRecorder.start(100) // 每100ms收集一次数据
    isRecording.value = true
    recordingDuration.value = 0
    emit('recordingStart')

    // 开始计时
    recordingTimer = window.setInterval(() => {
      recordingDuration.value++
      // 最长录音60秒
      if (recordingDuration.value >= 60) {
        stopRecording()
        ElMessage.warning('录音时长已达上限')
      }
    }, 1000)

  } catch (e: any) {
    console.error('开始录音失败', e)
    if (e.name === 'NotAllowedError') {
      ElMessage.error('请允许使用麦克风')
    } else {
      ElMessage.error('无法访问麦克风: ' + e.message)
    }
    emit('error', e.message)
  }
}

// 停止录音
const stopRecording = () => {
  if (mediaRecorder && mediaRecorder.state !== 'inactive') {
    mediaRecorder.stop()
  }
  isRecording.value = false
  emit('recordingStop')

  if (recordingTimer) {
    clearInterval(recordingTimer)
    recordingTimer = null
  }
}

// 获取支持的MIME类型
const getSupportedMimeType = () => {
  const types = ['audio/webm', 'audio/ogg', 'audio/mp4', 'audio/wav']
  for (const type of types) {
    if (MediaRecorder.isTypeSupported(type)) {
      return type
    }
  }
  return 'audio/webm' // 默认
}

// 将WebM/其他格式转换为WAV格式 (智谱ASR仅支持wav/mp3等格式)
const convertToWav = async (audioBlob: Blob): Promise<Blob> => {
  return new Promise((resolve) => {
    const audioContext = new AudioContext()
    const reader = new FileReader()

    reader.onload = async () => {
      try {
        const arrayBuffer = reader.result as ArrayBuffer
        const audioBuffer = await audioContext.decodeAudioData(arrayBuffer)

        // 创建WAV文件
        const wavBlob = audioBufferToWav(audioBuffer)
        resolve(wavBlob)
      } catch (err) {
        console.error('音频解码失败，使用原始格式', err)
        resolve(audioBlob) // 解码失败则使用原始blob
      } finally {
        audioContext.close()
      }
    }

    reader.onerror = () => {
      console.error('读取音频文件失败')
      resolve(audioBlob)
    }

    reader.readAsArrayBuffer(audioBlob)
  })
}

// AudioBuffer转WAV Blob
const audioBufferToWav = (buffer: AudioBuffer): Blob => {
  const numChannels = buffer.numberOfChannels
  const sampleRate = buffer.sampleRate
  const format = 1 // PCM
  const bitDepth = 16

  // 合并所有通道数据
  const length = buffer.length * numChannels * (bitDepth / 8)
  const arrayBuffer = new ArrayBuffer(44 + length)
  const view = new DataView(arrayBuffer)

  // WAV文件头
  const writeString = (offset: number, str: string) => {
    for (let i = 0; i < str.length; i++) {
      view.setUint8(offset + i, str.charCodeAt(i))
    }
  }

  writeString(0, 'RIFF')
  view.setUint32(4, 36 + length, true)
  writeString(8, 'WAVE')
  writeString(12, 'fmt ')
  view.setUint32(16, 16, true) // fmt chunk size
  view.setUint16(20, format, true)
  view.setUint16(22, numChannels, true)
  view.setUint32(24, sampleRate, true)
  view.setUint32(28, sampleRate * numChannels * (bitDepth / 8), true)
  view.setUint16(32, numChannels * (bitDepth / 8), true)
  view.setUint16(34, bitDepth, true)
  writeString(36, 'data')
  view.setUint32(40, length, true)

  // 写入音频数据 (交错多通道)
  let offset = 44
  for (let i = 0; i < buffer.length; i++) {
    for (let ch = 0; ch < numChannels; ch++) {
      const sample = Math.max(-1, Math.min(1, buffer.getChannelData(ch)[i]))
      const intSample = sample < 0 ? sample * 0x8000 : sample * 0x7FFF
      view.setInt16(offset, intSample, true)
      offset += 2
    }
  }

  return new Blob([arrayBuffer], { type: 'audio/wav' })
}

// 处理录音数据
const processAudio = async () => {
  isProcessing.value = true

  try {
    const mimeType = mediaRecorder?.mimeType || 'audio/webm'
    const audioBlob = new Blob(audioChunks, { type: mimeType })

    // 转换为WAV格式 (智谱ASR支持的格式)
    console.log('原始音频格式:', mimeType, '大小:', audioBlob.size)
    const wavBlob = await convertToWav(audioBlob)
    console.log('转换后WAV大小:', wavBlob.size)

    // 创建FormData上传
    const formData = new FormData()
    formData.append('audio', wavBlob, 'recording.wav')

    const res = await api.post('/ai/asr/transcribe', formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })

    // 注意：axios响应拦截器已返回response.data
    if (res.code === 0 && res.data?.success) {
      const text = res.data.text
      if (text) {
        emit('transcribed', text)
        ElMessage.success('语音识别成功')
      } else {
        ElMessage.warning('未识别到语音内容')
      }
    } else {
      const error = res.data?.error || res.message || '识别失败'
      ElMessage.error(error)
      emit('error', error)
    }
  } catch (e: any) {
    console.error('语音识别失败', e)
    ElMessage.error('语音识别失败: ' + e.message)
    emit('error', e.message)
  } finally {
    isProcessing.value = false
    audioChunks = []

    // 连续对话模式：识别完成后自动继续录音
    if (props.continuousMode) {
      setTimeout(() => {
        startRecording()
      }, 500)
    }
  }
}

// 暴露方法给父组件调用
defineExpose({
  /** 开始录音 */
  startRecording,
  /** 停止录音 */
  stopRecording,
  /** 切换录音状态 */
  toggleRecording,
  /** 是否正在录音 */
  isRecording,
  /** 是否正在处理 */
  isProcessing,
  /** ASR是否启用 */
  asrEnabled
})

// 生命周期
onMounted(() => {
  checkAsrEnabled()
})

onUnmounted(() => {
  if (isRecording.value) {
    stopRecording()
  }
})
</script>

<style scoped lang="scss">
.voice-input {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.recording {
  animation: pulse 1.5s infinite;
}

@keyframes pulse {
  0%, 100% {
    box-shadow: 0 0 0 0 rgba(245, 108, 108, 0.7);
  }
  50% {
    box-shadow: 0 0 0 10px rgba(245, 108, 108, 0);
  }
}

.recording-indicator {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 12px;
  background: rgba(245, 108, 108, 0.1);
  border-radius: 16px;
}

.wave-container {
  display: flex;
  align-items: center;
  gap: 2px;
  height: 20px;
}

.wave {
  width: 3px;
  height: 100%;
  background: #f56c6c;
  border-radius: 2px;
  animation: wave 0.5s ease-in-out infinite;

  &:nth-child(1) { animation-delay: 0s; }
  &:nth-child(2) { animation-delay: 0.1s; }
  &:nth-child(3) { animation-delay: 0.2s; }
  &:nth-child(4) { animation-delay: 0.3s; }
  &:nth-child(5) { animation-delay: 0.4s; }
}

@keyframes wave {
  0%, 100% {
    transform: scaleY(0.3);
  }
  50% {
    transform: scaleY(1);
  }
}

.duration {
  font-size: 12px;
  color: #f56c6c;
  font-family: monospace;
}
</style>

