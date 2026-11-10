<script setup lang="ts">
// 主界面：顶部栏 + 统计卡片 + 违规列表
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import StatsCards from './StatsCards.vue'
import ViolationTable from './ViolationTable.vue'

const emit = defineEmits<{ (e: 'logout'): void }>()          // 退出事件，交给 App.vue 处理
const refreshKey = ref(0)                                    // 变了就重新挂载 StatsCards，触发重新加载
const role = sessionStorage.getItem('role') ?? ''            // 当前角色，只用于显示

function logout() {                                          // 退出登录
  emit('logout')                                             // 父组件会清 token
  ElMessage.info('已退出')
}

function onChanged() {                                       // 处理违规后触发
  refreshKey.value++                                         // key 变化 → StatsCards 重新加载
}
</script>

<template>
  <div class="page">
    <header class="topbar">
      <h2>口罩合规检测系统</h2>
      <div class="right">
        <span class="role">{{ role === 'Admin' ? '管理员' : '普通用户' }}</span>
        <el-button size="small" @click="logout">退出登录</el-button>
      </div>
    </header>

    <!-- key 变化时 Vue 会重建组件，从而重新拉统计数据 -->
    <StatsCards :key="refreshKey" />

    <!-- 处理成功后发 changed，让上面的统计跟着刷新 -->
    <ViolationTable @changed="onChanged" />
  </div>
</template>

<style scoped>
.page { padding: 16px 24px; background: #f5f7fa; min-height: 100vh; }
.topbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}
.topbar h2 { margin: 0; font-size: 20px; }
.right { display: flex; align-items: center; gap: 12px; }
.role { color: #909399; font-size: 13px; }
</style>
