<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Connection } from '@element-plus/icons-vue'
import { formatDateTime } from '@/utils/format'
import {
  getDatasourceList,
  getDatasourceDetail,
  createDatasource,
  updateDatasource,
  deleteDatasource,
  testDatasourceConnection,
  type Datasource
} from '@/api/datasource'

const loading = ref(false)
const list = ref<Datasource[]>([])
const dialogVisible = ref(false)
const saving = ref(false)
const testing = ref(false)
const testingRow = ref<number | null>(null)

// 表单数据 - 拆分连接参数
const form = ref({
  id: 0,
  name: '',
  type: 'PostgreSQL',
  host: '',
  port: 5432,
  database: '',
  username: '',
  password: '',
  isEnabled: true,
  remark: ''
})

// 数据库类型配置
const dbTypes = [
  { label: 'PostgreSQL', value: 'PostgreSQL', defaultPort: 5432 },
  { label: 'SQL Server', value: 'SQLServer', defaultPort: 1433 },
  { label: 'MySQL', value: 'MySQL', defaultPort: 3306 },
  { label: 'Doris', value: 'Doris', defaultPort: 9030 }  // Doris使用MySQL协议，默认FE端口9030
]

// 监听类型变化，自动更新默认端口
watch(() => form.value.type, (newType) => {
  const config = dbTypes.find(d => d.value === newType)
  if (config && !form.value.id) {
    form.value.port = config.defaultPort
  }
})

// 根据表单生成连接字符串
const connString = computed(() => {
  const { type, host, port, database, username, password } = form.value
  if (!host || !database) return ''

  switch (type) {
    case 'PostgreSQL':
      return `Host=${host};Port=${port};Database=${database};Username=${username};Password=${password}`
    case 'SQLServer':
      return `Server=${host},${port};Database=${database};User Id=${username};Password=${password};TrustServerCertificate=true`
    case 'MySQL':
      // 添加ConnectionReset=false避免COM_RESET_CONNECTION错误；AllowUserVariables=true支持中文别名
      return `Server=${host};Port=${port};Database=${database};Uid=${username};Pwd=${password};ConnectionReset=false;AllowUserVariables=true;CharSet=utf8mb4`
    case 'Doris':
      // Doris使用MySQL协议连接
      return `Server=${host};Port=${port};Database=${database};Uid=${username};Pwd=${password};ConnectionReset=false;AllowUserVariables=true;CharSet=utf8mb4`
    default:
      return ''
  }
})

// 解析连接字符串到表单
function parseConnString(connStr: string, type: string) {
  const params: Record<string, string> = {}
  connStr.split(';').forEach(part => {
    const [key, ...vals] = part.split('=')
    if (key) params[key.toLowerCase().trim()] = vals.join('=')
  })

  switch (type) {
    case 'PostgreSQL':
      return {
        host: params['host'] || '',
        port: parseInt(params['port']) || 5432,
        database: params['database'] || '',
        username: params['username'] || '',
        password: params['password'] || ''
      }
    case 'SQLServer':
      const serverParts = (params['server'] || '').split(',')
      return {
        host: serverParts[0] || '',
        port: parseInt(serverParts[1]) || 1433,
        database: params['database'] || '',
        username: params['user id'] || '',
        password: params['password'] || ''
      }
    case 'MySQL':
      return {
        host: params['server'] || '',
        port: parseInt(params['port']) || 3306,
        database: params['database'] || '',
        username: params['uid'] || '',
        password: params['pwd'] || ''
      }
    case 'Doris':
      // Doris使用MySQL协议，解析方式相同
      return {
        host: params['server'] || '',
        port: parseInt(params['port']) || 9030,
        database: params['database'] || '',
        username: params['uid'] || '',
        password: params['pwd'] || ''
      }
    default:
      return { host: '', port: 5432, database: '', username: '', password: '' }
  }
}

onMounted(() => {
  loadList()
})

async function loadList() {
  loading.value = true
  try {
    const res = await getDatasourceList()
    if (res.code === 0) {
      list.value = res.data || []
    } else {
      ElMessage.error(res.message || '加载失败')
    }
  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

function showAdd() {
  form.value = { id: 0, name: '', type: 'PostgreSQL', host: '', port: 5432, database: '', username: '', password: '', isEnabled: true, remark: '' }
  dialogVisible.value = true
}

async function handleEdit(row: Datasource) {
  try {
    const res = await getDatasourceDetail(row.id)
    if (res.code === 0 && res.data) {
      const parsed = parseConnString(res.data.connString, res.data.type)
      form.value = {
        id: res.data.id,
        name: res.data.name,
        type: res.data.type,
        host: parsed.host,
        port: parsed.port,
        database: parsed.database,
        username: parsed.username,
        password: parsed.password,
        isEnabled: res.data.isEnabled,
        remark: res.data.remark || ''
      }
      dialogVisible.value = true
    }
  } catch (e) {
    console.error(e)
  }
}

async function handleDelete(row: Datasource) {
  await ElMessageBox.confirm('确定删除该数据源吗？', '提示')
  try {
    const res = await deleteDatasource(row.id)
    if (res.code === 0) {
      ElMessage.success('删除成功')
      loadList()
    } else {
      ElMessage.error(res.message || '删除失败')
    }
  } catch (e) {
    console.error(e)
  }
}

async function handleSave() {
  if (!form.value.name || !form.value.host || !form.value.database) {
    ElMessage.warning('请填写名称、主机和数据库名')
    return
  }
  saving.value = true
  try {
    const payload = {
      name: form.value.name,
      type: form.value.type,
      connString: connString.value,
      isEnabled: form.value.isEnabled,
      remark: form.value.remark
    }
    if (form.value.id) {
      const res = await updateDatasource(form.value.id, payload)
      if (res.code === 0) {
        ElMessage.success('保存成功')
        dialogVisible.value = false
        loadList()
      } else {
        ElMessage.error(res.message || '保存失败')
      }
    } else {
      const res = await createDatasource(payload)
      if (res.code === 0) {
        ElMessage.success('创建成功')
        dialogVisible.value = false
        loadList()
      } else {
        ElMessage.error(res.message || '创建失败')
      }
    }
  } catch (e) {
    console.error(e)
  } finally {
    saving.value = false
  }
}

async function handleTestConnection() {
  if (!form.value.host || !form.value.database) {
    ElMessage.warning('请填写主机和数据库名')
    return
  }
  testing.value = true
  try {
    const res = await testDatasourceConnection({
      type: form.value.type,
      connString: connString.value
    })
    if (res.code === 0) {
      ElMessage.success('连接成功！')
    } else {
      ElMessage.error(res.message || '连接失败')
    }
  } catch (e) {
    console.error(e)
  } finally {
    testing.value = false
  }
}

// 列表中的测试连接 - 需要先获取详情拿到connString
async function handleTestRow(row: Datasource) {
  testingRow.value = row.id
  try {
    // 先获取详情（包含connString）
    const detailRes = await getDatasourceDetail(row.id)
    if (detailRes.code !== 0 || !detailRes.data) {
      ElMessage.error('获取数据源信息失败')
      return
    }
    const res = await testDatasourceConnection({
      type: detailRes.data.type,
      connString: detailRes.data.connString
    })
    if (res.code === 0) {
      ElMessage.success(`${row.name} 连接成功！`)
    } else {
      ElMessage.error(res.message || '连接失败')
    }
  } catch (e) {
    console.error(e)
  } finally {
    testingRow.value = null
  }
}
</script>

<template>
  <div class="page-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>数据源管理</span>
          <el-button type="primary" @click="showAdd">新建数据源</el-button>
        </div>
      </template>
      <el-table :data="list" v-loading="loading" stripe>
        <el-table-column prop="name" label="名称" min-width="120" />
        <el-table-column prop="type" label="类型" width="120" />
        <el-table-column prop="isEnabled" label="状态" width="80">
          <template #default="{ row }">
            <el-tag :type="row.isEnabled ? 'success' : 'info'" size="small">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="remark" label="备注" min-width="150" />
        <el-table-column prop="createdAt" label="创建时间" width="180">
          <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="200" fixed="right">
          <template #default="{ row }">
            <el-button link type="success" :loading="testingRow === row.id" @click="handleTestRow(row)">测试</el-button>
            <el-button link type="primary" @click="handleEdit(row)">编辑</el-button>
            <el-button link type="danger" @click="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 编辑对话框 - 友好的表单 -->
    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑数据源' : '新建数据源'" width="600px">
      <el-form :model="form" label-width="90px">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="名称" required>
              <el-input v-model="form.name" placeholder="数据源名称" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="类型" required>
              <el-select v-model="form.type" style="width: 100%">
                <el-option v-for="db in dbTypes" :key="db.value" :label="db.label" :value="db.value" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>

        <el-divider content-position="left">连接信息</el-divider>

        <el-row :gutter="16">
          <el-col :span="16">
            <el-form-item label="主机地址" required>
              <el-input v-model="form.host" placeholder="如：localhost 或 192.168.1.100" />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="端口" required>
              <el-input v-model.number="form.port" placeholder="端口号" type="number" style="width: 100%" />
            </el-form-item>
          </el-col>
        </el-row>

        <el-form-item label="数据库名" required>
          <el-input v-model="form.database" placeholder="数据库名称" />
        </el-form-item>

        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="用户名">
              <el-input v-model="form.username" placeholder="数据库用户名" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="密码">
              <el-input v-model="form.password" type="password" show-password placeholder="数据库密码" />
            </el-form-item>
          </el-col>
        </el-row>

        <el-form-item>
          <el-button :loading="testing" @click="handleTestConnection">
            <el-icon><Connection /></el-icon>
            <span style="margin-left: 4px">测试连接</span>
          </el-button>
        </el-form-item>

        <el-divider />

        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="状态">
              <el-switch v-model="form.isEnabled" active-text="启用" inactive-text="禁用" />
            </el-form-item>
          </el-col>
        </el-row>

        <el-form-item label="备注">
          <el-input v-model="form.remark" placeholder="备注说明（可选）" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.card-header { display: flex; justify-content: space-between; align-items: center; }
</style>

