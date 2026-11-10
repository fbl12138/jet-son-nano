<script setup lang="ts">
import { ref } from 'vue'                                   // 响应式 API
import LoginCard from './components/LoginCard.vue'          // 登录组件
import Dashboard from './components/Dashboard.vue'          // 主界面（临时占位）

const token = ref(sessionStorage.getItem('token'))          // 初始值：刷新页面时从会话存储读
// ↑ 有 token 说明之前登录过，直接进主界面；没有就显示登录卡

function onLogged() {                                       // 登录成功后由子组件触发
  token.value = sessionStorage.getItem('token')             // 同步最新 token，界面切到主界面
}

function onLogout() {                                       // 退出登录
  sessionStorage.clear()                                    // 清掉 token / role
  token.value = null                                        // 界面切回登录
}
</script>

<template>
  <LoginCard v-if="!token" @logged="onLogged" />             <!-- 没 token：显示登录 -->
  <Dashboard v-else @logout="onLogout" />                    <!-- 有 token：显示主界面 -->
</template>