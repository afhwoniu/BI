<template>
  <div class="portal-view">
    <!-- 需要密码 -->
    <div v-if="needPassword" class="password-form">
      <el-card style="width: 400px;">
        <template #header>访问验证</template>
        <el-form @submit.prevent="submitPassword">
          <el-form-item label="访问密码">
            <el-input v-model="password" type="password" placeholder="请输入访问密码" />
          </el-form-item>
          <el-form-item>
            <el-button type="primary" @click="submitPassword" :loading="loading">确认</el-button>
          </el-form-item>
        </el-form>
      </el-card>
    </div>

    <!-- 错误信息 -->
    <div v-else-if="errorMsg" class="error-view">
      <el-result icon="error" :title="errorMsg">
        <template #extra>
          <el-button @click="$router.push('/portal')">返回门户</el-button>
        </template>
      </el-result>
    </div>

    <!-- 内容展示 -->
    <div v-else-if="content" class="content-view">
      <div class="view-header">
        <h2>{{ content.title }}</h2>
      </div>
      <div class="view-body">
        <!-- 报表 -->
        <div v-if="content.objectType === 'report'" class="report-content">
          <div v-for="page in content.content?.pages" :key="page.id" class="report-page">
            <h3>{{ page.title }}</h3>
            <div class="page-items">
              <div v-for="item in page.items" :key="item.id" class="report-item">
                <div v-if="item.itemType === 'text'" v-html="item.textContent"></div>
                <div v-else-if="item.itemType === 'image'"><img :src="item.imageUrl" style="max-width: 100%;" /></div>
                <div v-else class="chart-placeholder">图表 #{{ item.chartId }}</div>
              </div>
            </div>
          </div>
        </div>
        <!-- 面板 -->
        <div v-else-if="content.objectType === 'panel'" class="panel-content">
          <div class="panel-grid">
            <el-card v-for="item in content.content?.items" :key="item.id" class="panel-card">
              <template #header>{{ item.chartName }}</template>
              <div class="chart-placeholder">{{ item.chartType }} 图表</div>
            </el-card>
          </div>
        </div>
        <!-- 图表 -->
        <div v-else-if="content.objectType === 'chart'" class="chart-content">
          <el-card>
            <template #header>{{ content.content?.name }}</template>
            <div class="chart-placeholder large">{{ content.content?.chartType }} 图表</div>
          </el-card>
        </div>
      </div>
    </div>

    <!-- 加载中 -->
    <div v-else class="loading-view" v-loading="loading">
      <div style="height: 300px;"></div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { viewByToken } from '@/api/menu'

const route = useRoute()
const token = route.params.token as string

const loading = ref(false)
const needPassword = ref(false)
const password = ref('')
const errorMsg = ref('')
const content = ref<any>(null)

onMounted(() => loadContent())

async function loadContent() {
  loading.value = true
  errorMsg.value = ''
  try {
    const res = await viewByToken(token, password.value || undefined)
    content.value = res.data
    needPassword.value = false
  } catch (e: any) {
    if (e.response?.data?.needPassword) {
      needPassword.value = true
    } else {
      errorMsg.value = e.response?.data?.message || '加载失败'
    }
  } finally {
    loading.value = false
  }
}

function submitPassword() {
  loadContent()
}
</script>

<style scoped>
.portal-view { min-height: 100vh; background: #f5f7fa; }
.password-form, .error-view, .loading-view { display: flex; align-items: center; justify-content: center; min-height: 100vh; }
.content-view { padding: 20px; }
.view-header { background: #fff; padding: 20px; margin-bottom: 20px; border-radius: 4px; }
.view-header h2 { margin: 0; }
.view-body { background: #fff; padding: 20px; border-radius: 4px; }
.report-page { margin-bottom: 30px; }
.report-page h3 { margin-bottom: 15px; }
.page-items { display: flex; flex-wrap: wrap; gap: 16px; }
.report-item { padding: 10px; background: #fafafa; border: 1px solid #e0e0e0; border-radius: 4px; }
.panel-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(350px, 1fr)); gap: 20px; }
.chart-placeholder { height: 200px; display: flex; align-items: center; justify-content: center; background: #f5f5f5; color: #999; }
.chart-placeholder.large { height: 400px; }
</style>

