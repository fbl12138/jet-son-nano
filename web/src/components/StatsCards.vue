<script setup lang="ts">
// 统计卡片：今日合规情况 + 最近 7 天趋势
import { onMounted, ref } from 'vue'
import { fetchCompliance, fetchDaily } from '../api/stats'

const stats = ref<{
  personDetections: number
  violations: number
  compliant: number
  complianceRate: number
} | null>(null)                                              // 今日统计，null 表示还没加载

const daily = ref<Array<{                                    // 最近 7 天
  date: string
  personDetections: number
  violations: number
  complianceRate: number
}>>([])

const loading = ref(false)                                   // 加载状态

async function load() {                                      // 拉两份统计
  loading.value = true
  try {
    // 并行请求，两个接口互不依赖，比串行快
    const [today, week] = await Promise.all([fetchCompliance(), fetchDaily(7)])
    stats.value = today
    daily.value = week
  } catch (e) {
    console.error('加载统计失败', e)
  } finally {
    loading.value = false
  }
}

function pct(v: number) {                                    // 0.2043 → "20.4%"
  return (v * 100).toFixed(1) + '%'
}

onMounted(load)                                              // 组件挂载后拉一次
</script>

<template>
  <div v-loading="loading">
    <el-row :gutter="12">
      <el-col :span="8">
        <el-card>
          <div class="label">今日检测人次</div>
          <div class="value">{{ stats?.personDetections ?? 0 }}</div>
        </el-card>
      </el-col>
      <el-col :span="8">
        <el-card>
          <div class="label">今日违规</div>
          <div class="value red">{{ stats?.violations ?? 0 }}</div>
        </el-card>
      </el-col>
      <el-col :span="8">
        <el-card>
          <div class="label">今日合规率</div>
          <div class="value green">{{ pct(stats?.complianceRate ?? 0) }}</div>
        </el-card>
      </el-col>
    </el-row>

    <el-card class="trend">
      <template #header>最近 7 天</template>
      <el-table :data="daily" size="small">
        <el-table-column prop="date" label="日期" width="120" />
        <el-table-column prop="personDetections" label="人次" width="100" />
        <el-table-column prop="violations" label="违规" width="100" />
        <el-table-column label="合规率">
          <template #default="{ row }">{{ pct(row.complianceRate) }}</template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<style scoped>
.label { color: #909399; font-size: 13px; }
.value { font-size: 28px; font-weight: 600; margin-top: 6px; }
.red { color: #f56c6c; }
.green { color: #67c23a; }
.trend { margin-top: 12px; }
</style>
