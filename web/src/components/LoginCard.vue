<script setup lang="ts">
// 登录卡片：输入账号密码，调后端 /api/auth/login，成功后通知父组件
import { ref } from 'vue'                    // Vue 的响应式 API
import { ElMessage } from 'element-plus'     // Element Plus 的全局提示
import { login } from '../api/auth'          // 我们自己封装的登录函数

const emit = defineEmits<{ (e: 'logged'): void }>()   // 声明会向父组件发 logged 事件

const username = ref('admin')                // 用户名；演示先预填 admin
const password = ref('Admin@123')            // 密码；演示先预填密码
const loading = ref(false)                   // 按钮转圈状态
const errorMsg = ref('')                     // 登录失败时的提示文案

async function onSubmit() {                  // 点“登录”或回车时触发
  if (!username.value || !password.value) {  // 前端先做非空校验
    errorMsg.value = '请输入用户名和密码'
    return
  }

  loading.value = true                       // 开始转圈
  errorMsg.value = ''                        // 清空上一次的错误

  try {
    await login(username.value, password.value)   // 调后端登录接口
    ElMessage.success('登录成功')                  // 成功提示
    emit('logged')                                // 通知父组件切换界面
  } catch (e: any) {                              // 失败（401 或网络问题）
    errorMsg.value = e?.response?.status === 401
      ? '用户名或密码错误'                          // 401 说明账号密码错
      : '登录失败，请确认后端已启动'                 // 其它情况多半是后端没开
  } finally {
    loading.value = false                     // 无论成功失败都停止转圈
  }
}
</script>

<template>
  <div class="login-wrap">
    <el-card class="login-card">
      <h2>口罩检测系统</h2>

      <!-- label-width 固定住左侧标签宽度，两个输入框才会一样长 -->
      <el-form label-width="72px" @submit.prevent="onSubmit">
        <el-form-item label="用户名">
          <el-input v-model="username" />
        </el-form-item>

        <el-form-item label="密码">
          <!-- 密码框：type=password 显示圆点，show-password 提供“眼睛”按钮，回车也能登录 -->
          <el-input
            v-model="password"
            type="password"
            show-password
            @keyup.enter="onSubmit"
          />
        </el-form-item>

        <!-- 只有有错误时才显示红色提示 -->
        <el-alert
          v-if="errorMsg"
          :title="errorMsg"
          type="error"
          :closable="false"
        />

        <el-button
          type="primary"
          :loading="loading"
          style="width: 100%"
          @click="onSubmit"
        >登录</el-button>
      </el-form>
    </el-card>
  </div>
</template>

<style scoped>
.login-wrap {
  display: flex;              /* 用 flex 布局 */
  justify-content: center;    /* 水平居中 */
  align-items: center;        /* 垂直居中 */
  min-height: 100vh;          /* 至少占满一屏高度 */
  background: #f5f7fa;        /* 浅灰背景 */
}
.login-card {
  width: 360px;               /* 卡片宽度 */
}
</style>
