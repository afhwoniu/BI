<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const route = useRoute()
const userStore = useUserStore()

const loading = ref(false)
const form = reactive({
  username: '',
  password: ''
})

async function handleLogin() {
  if (!form.username || !form.password) {
    ElMessage.warning('请输入用户名和密码')
    return
  }
  loading.value = true
  try {
    const success = await userStore.login(form.username, form.password)
    if (success) {
      ElMessage.success('登录成功')
      const redirect = route.query.redirect as string || '/dashboard'
      router.push(redirect)
    } else {
      ElMessage.error('用户名或密码错误')
    }
  } catch (e) {
    ElMessage.error('登录失败')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-container">
    <div class="login-box">
      <h2>智能BI可视化平台</h2>
      <el-form :model="form" @submit.prevent="handleLogin">
        <el-form-item>
          <el-input v-model="form.username" placeholder="用户名" prefix-icon="User" size="large" />
        </el-form-item>
        <el-form-item>
          <el-input v-model="form.password" type="password" placeholder="密码" prefix-icon="Lock" size="large" show-password />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :loading="loading" size="large" style="width: 100%" native-type="submit">
            登 录
          </el-button>
        </el-form-item>
      </el-form>
      <p class="hint">默认账号: admin / admin123</p>
    </div>
  </div>
</template>

<style scoped lang="scss">
.login-container {
  height: 100%; display: flex; align-items: center; justify-content: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}
.login-box {
  width: 400px; padding: 40px; background: #fff; border-radius: 8px; box-shadow: 0 4px 20px rgba(0,0,0,0.15);
  h2 { text-align: center; margin-bottom: 30px; color: #333; }
  .hint { text-align: center; color: #999; font-size: 12px; margin-top: 20px; }
}
</style>

