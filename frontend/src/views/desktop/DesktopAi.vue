<template>
  <!-- 桌面端专用智能分析界面 - 直接复用Web端AiAnalysis组件 -->
  <div class="desktop-ai-wrapper">
    <AiAnalysis />
  </div>
</template>

<script setup lang="ts">
// 直接引入Web端的AiAnalysis组件
import AiAnalysis from '@/views/admin/AiAnalysis.vue'
</script>

<style scoped lang="scss">
.desktop-ai-wrapper {
  width: 100%;
  height: 100vh;
  overflow: hidden;
}
</style>

