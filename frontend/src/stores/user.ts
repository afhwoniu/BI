import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { login as apiLogin, getUserInfo } from '@/api/auth'

export const useUserStore = defineStore('user', () => {
  const token = ref<string>(localStorage.getItem('token') || '')
  const userId = ref<number>(0)
  const username = ref<string>('')
  const realName = ref<string>('')
  const avatar = ref<string>('')

  const isLoggedIn = computed(() => !!token.value)

  async function login(userName: string, password: string) {
    const res = await apiLogin(userName, password)
    if (res.code === 0 && res.data) {
      token.value = res.data.token
      userId.value = res.data.userId
      username.value = res.data.username
      realName.value = res.data.realName || ''
      avatar.value = res.data.avatar || ''
      localStorage.setItem('token', res.data.token)
      return true
    }
    return false
  }

  async function fetchUserInfo() {
    const res = await getUserInfo()
    if (res.code === 0 && res.data) {
      userId.value = res.data.userId
      username.value = res.data.username
      realName.value = res.data.realName || ''
      avatar.value = res.data.avatar || ''
    }
  }

  function logout() {
    token.value = ''
    userId.value = 0
    username.value = ''
    realName.value = ''
    avatar.value = ''
    localStorage.removeItem('token')
  }

  return {
    token, userId, username, realName, avatar,
    isLoggedIn, login, logout, fetchUserInfo
  }
})

