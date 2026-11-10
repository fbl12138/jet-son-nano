<script setup lang="ts">
// 违规列表：状态筛选 + 分页 + 处理按钮
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { fetchViolations, handleViolation } from '../api/violations'
import type { Violation } from '../api/types'

const emit = defineEmits<{ (e: 'changed'): void }>()          // 处理成功后通知父组件刷新统计

const status = ref('Open')                                   // 默认只看待处理
const page = ref(1)                                          // 当前页
const pageSize = ref(10)                                     // 每页条数
const total = ref(0)                                         // 总条数
const rows = ref<Violation[]>([])                            // 当前页数据
const loading = ref(false)                                   // 表格 loading

async function load() {                                      // 拉数据
  loading.value = true
  try {
    const data = await fetchViolations(status.value, page.value, pageSize.value)
    rows.value = data.items
    total.value = data.total
  } catch (e: any) {
    ElMessage.error(e?.response?.status === 401 ? '登录已过期，请重新登录' : '加载列表失败')
  } finally {
    loading.value = false
  }
}

function onFilterChange() {                                  // 切换状态下拉
  page.value = 1                                             // 回到第一页，否则可能停在空页
  load()
}

function onPageChange(p: number) {                           // 点分页
  page.value = p
  load()
}

async function onHandle(row: Violation) {                    // 点“处理”
  try {
    const { value } = await ElMessageBox.prompt('填写处理备注', `处理违规 #${row.id}`, {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      inputPlaceholder: '例如：已提醒并补发口罩'
    })

    await handleViolation(row.id, value ?? '')               // body 只传 remark
    ElMessage.success('已处理')
    await load()                                             // 重新拉，状态会变成 Handled
    emit('changed')                                          // 让父组件刷新统计卡片
  } catch (e: any) {
    if (e === 'cancel' || e === 'close') return              // 用户点了取消，不算错误
    if (e?.response?.status === 403) {
      ElMessage.error('没有权限，需要 Admin 账号')
    } else if (e?.response?.status === 409) {
      ElMessage.warning('这条已被处理过')
      await load()
    } else {
      ElMessage.error('处理失败')
    }
  }
}

function fmt(t: string) {                                    // UTC → 北京时间
  return new Date(t).toLocaleString('zh-CN', {
    timeZone: 'Asia/Shanghai',
    hour12: false
  })
}

onMounted(load)                                              // 首次进入加载
</script>

<template>
  <el-card class="table-card">
    <template #header>
      <div class="header">
        <span>违规记录</span>
        <el-select v-model="status" style="width: 140px" @change="onFilterChange">
          <el-option label="待处理（Open）" value="Open" />
          <el-option label="已处理（Handled）" value="Handled" />
          <el-option label="全部" value="" />
        </el-select>
      </div>
    </template>

    <el-table :data="rows" v-loading="loading" size="small" border>
      <el-table-column prop="id" label="Id" width="70" />
      <el-table-column prop="deviceName" label="设备" width="140" />
      <el-table-column prop="violationTypes" label="类型" width="120" />
      <el-table-column label="时间" width="180">
        <template #default="{ row }">{{ fmt(row.detectedAt) }}</template>
      </el-table-column>
      <el-table-column prop="trackId" label="TrackId" width="180" />
      <el-table-column label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="row.status === 'Open' ? 'danger' : 'success'" size="small">
            {{ row.status }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="100">
        <template #default="{ row }">
          <el-button
            v-if="row.status === 'Open'"
            type="primary"
            size="small"
            @click="onHandle(row)"
          >处理</el-button>
          <span v-else class="muted">—</span>
        </template>
      </el-table-column>
    </el-table>

    <div class="pager">
      <el-pagination
        :current-page="page"
        :page-size="pageSize"
        :total="total"
        layout="prev, pager, next, total"
        @current-change="onPageChange"
      />
    </div>
  </el-card>
</template>

<style scoped>
.table-card { margin-top: 12px; }
.header { display: flex; justify-content: space-between; align-items: center; }
.pager { display: flex; justify-content: flex-end; margin-top: 12px; }
.muted { color: #c0c4cc; }
</style>
