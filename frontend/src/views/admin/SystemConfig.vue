<template>
  <div class="system-config">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>系统配置</span>
          <div>
            <el-button type="primary" :loading="saving" @click="handleSave">
              <el-icon><Check /></el-icon>
              保存配置
            </el-button>
            <el-button @click="handleRefresh">
              <el-icon><Refresh /></el-icon>
              刷新缓存
            </el-button>
          </div>
        </div>
      </template>

      <el-tabs v-model="activeGroup" @tab-change="loadGroupConfig">
        <el-tab-pane
          v-for="group in groups"
          :key="group.key"
          :label="group.name"
          :name="group.key"
        >
          <template #label>
            <span>
              <el-icon><component :is="group.icon" /></el-icon>
              {{ group.name }}
            </span>
          </template>
        </el-tab-pane>
      </el-tabs>

      <!-- AI配置使用专用面板 -->
      <AiConfigPanel
        v-if="activeGroup === 'ai'"
        ref="aiConfigPanelRef"
        :configs="configs"
        @update:configs="onAiConfigUpdate"
        v-loading="loading"
      />

      <!-- 其他配置使用通用表单 -->
      <el-form
        v-else
        :model="configForm"
        label-width="180px"
        class="config-form"
        v-loading="loading"
      >
        <el-form-item
          v-for="config in configs"
          :key="config.configKey"
          :label="config.displayName || config.configKey"
        >
          <!-- 字符串类型 -->
          <el-input
            v-if="config.configType === 'string'"
            v-model="config.configValue"
            :placeholder="config.remark || ''"
            clearable
          />

          <!-- 密码类型 -->
          <el-input
            v-else-if="config.configType === 'password'"
            v-model="config.configValue"
            type="password"
            :placeholder="config.isEncrypted ? '已加密存储，留空保持不变' : ''"
            show-password
            clearable
          />

          <!-- 数字类型 -->
          <el-input-number
            v-else-if="config.configType === 'number'"
            v-model.number="config.configValue"
            :min="0"
            :precision="config.configKey.includes('temperature') ? 2 : 0"
            :step="config.configKey.includes('temperature') ? 0.1 : 1"
          />

          <!-- 布尔类型 -->
          <el-switch
            v-else-if="config.configType === 'boolean'"
            :model-value="config.configValue === 'true'"
            @update:model-value="(val: any) => config.configValue = val ? 'true' : 'false'"
          />

          <!-- 默认字符串 -->
          <el-input
            v-else
            v-model="config.configValue"
            :placeholder="config.remark || ''"
          />

          <template #error>
            <span class="config-hint">{{ config.remark }}</span>
          </template>
        </el-form-item>
      </el-form>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { Check, Refresh } from '@element-plus/icons-vue'
import {
  getConfigGroups,
  getConfigByGroup,
  saveConfigBatch,
  refreshConfigCache,
  type ConfigItem,
  type ConfigGroup
} from '@/api/config'
import AiConfigPanel from '@/components/config/AiConfigPanel.vue'

const groups = ref<ConfigGroup[]>([])
const activeGroup = ref('basic')
const configs = ref<ConfigItem[]>([])
const configForm = ref({})
const loading = ref(false)
const saving = ref(false)

// 加载配置分组
const loadGroups = async () => {
  try {
    const res = await getConfigGroups()
    groups.value = res.data || []
    if (groups.value.length > 0) {
      activeGroup.value = groups.value[0].key
      await loadGroupConfig(activeGroup.value)
    }
  } catch (error) {
    console.error('加载配置分组失败:', error)
  }
}

// 加载分组配置
const loadGroupConfig = async (group: string | number) => {
  loading.value = true
  try {
    const res = await getConfigByGroup(String(group))
    configs.value = res.data || []
  } catch (error) {
    console.error('加载配置失败:', error)
  } finally {
    loading.value = false
  }
}

// AI配置更新
const onAiConfigUpdate = (newConfigs: ConfigItem[]) => {
  configs.value = newConfigs
}

// 保存配置
const handleSave = async () => {
  saving.value = true
  try {
    await saveConfigBatch(configs.value)
    ElMessage.success('配置保存成功')
    // 刷新缓存
    await refreshConfigCache()
  } catch (error: any) {
    ElMessage.error(error.message || '保存失败')
  } finally {
    saving.value = false
  }
}

// 刷新缓存
const handleRefresh = async () => {
  try {
    await refreshConfigCache()
    ElMessage.success('配置缓存已刷新')
    // 重新加载当前分组
    await loadGroupConfig(activeGroup.value)
  } catch (error: any) {
    ElMessage.error(error.message || '刷新失败')
  }
}

onMounted(() => {
  loadGroups()
})
</script>

<style scoped>
.system-config {
  padding: 20px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.config-form {
  max-width: 800px;
  margin-top: 20px;
}

.config-hint {
  font-size: 12px;
  color: #909399;
}

:deep(.el-tabs__item) {
  display: flex;
  align-items: center;
  gap: 4px;
}

:deep(.el-form-item) {
  margin-bottom: 22px;
}
</style>

