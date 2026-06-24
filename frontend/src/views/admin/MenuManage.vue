<template>
  <div class="menu-manage">
    <el-row :gutter="20">
      <!-- 左侧菜单树 -->
      <el-col :span="10">
        <el-card>
          <template #header>
            <div class="card-header">
              <span>菜单结构</span>
              <el-button type="primary" size="small" @click="handleAddRoot">添加根菜单</el-button>
            </div>
          </template>
          <el-tree
            :data="menuTree"
            :props="{ label: 'name', children: 'children' }"
            node-key="id"
            default-expand-all
            highlight-current
            @node-click="handleNodeClick"
          >
            <template #default="{ node, data }">
              <span class="tree-node">
                <el-icon v-if="data.icon"><component :is="data.icon" /></el-icon>
                <span>{{ node.label }}</span>
                <el-tag size="small" :type="getMenuTypeTag(data.menuType)">{{ getMenuTypeText(data.menuType) }}</el-tag>
              </span>
            </template>
          </el-tree>
        </el-card>
      </el-col>

      <!-- 右侧编辑区 -->
      <el-col :span="14">
        <el-card>
          <template #header>
            <div class="card-header">
              <span>{{ editingMenu ? '编辑菜单' : '新建菜单' }}</span>
              <div>
                <el-button v-if="editingMenu" type="success" size="small" @click="handleAddChild">添加子菜单</el-button>
                <el-button v-if="editingMenu" type="danger" size="small" @click="handleDelete">删除</el-button>
              </div>
            </div>
          </template>
          <el-form :model="form" label-width="80px">
            <el-form-item label="名称" required>
              <el-input v-model="form.name" placeholder="菜单名称" />
            </el-form-item>
            <el-form-item label="类型">
              <el-radio-group v-model="form.menuType">
                <el-radio value="folder">目录</el-radio>
                <el-radio value="link">链接</el-radio>
                <el-radio value="publish">发布对象</el-radio>
              </el-radio-group>
            </el-form-item>
            <el-form-item label="图标">
              <el-input v-model="form.icon" placeholder="图标名称" />
            </el-form-item>
            <el-form-item v-if="form.menuType === 'link'" label="链接">
              <el-input v-model="form.linkUrl" placeholder="链接地址" />
            </el-form-item>
            <el-form-item v-if="form.menuType === 'publish'" label="发布对象">
              <el-select v-model="form.publishId" placeholder="选择发布对象" style="width: 100%;">
                <el-option v-for="p in publishList" :key="p.id" :label="p.title" :value="p.id">
                  <span>{{ p.title }}</span>
                  <el-tag size="small" style="margin-left: 8px;">{{ p.objectType }}</el-tag>
                </el-option>
              </el-select>
            </el-form-item>
            <el-form-item label="排序">
              <el-input-number v-model="form.sortOrder" :min="0" />
            </el-form-item>
            <el-form-item label="可见">
              <el-switch v-model="form.isVisible" />
            </el-form-item>
            <el-form-item label="备注">
              <el-input v-model="form.remark" type="textarea" :rows="2" />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" @click="handleSave" :loading="saving">保存</el-button>
              <el-button @click="resetForm">重置</el-button>
            </el-form-item>
          </el-form>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { 
  getMenuTree, createMenu, updateMenu, deleteMenu, getPublishList,
  type MenuItem, type PublishItem 
} from '@/api/menu'

const menuTree = ref<MenuItem[]>([])
const publishList = ref<PublishItem[]>([])
const editingMenu = ref<MenuItem | null>(null)
const saving = ref(false)

const form = ref({
  name: '',
  parentId: 0,
  menuType: 'folder',
  icon: '',
  linkUrl: '',
  publishId: undefined as number | undefined,
  sortOrder: 0,
  isVisible: true,
  remark: ''
})

onMounted(async () => {
  await loadData()
})

async function loadData() {
  const [menuRes, publishRes] = await Promise.all([getMenuTree(), getPublishList()])
  menuTree.value = menuRes.data || []
  publishList.value = publishRes.data || []
}

function handleNodeClick(data: MenuItem) {
  editingMenu.value = data
  form.value = {
    name: data.name,
    parentId: data.parentId,
    menuType: data.menuType,
    icon: data.icon || '',
    linkUrl: data.linkUrl || '',
    publishId: data.publishId,
    sortOrder: data.sortOrder,
    isVisible: data.isVisible,
    remark: data.remark || ''
  }
}

function handleAddRoot() {
  editingMenu.value = null
  form.value = { name: '', parentId: 0, menuType: 'folder', icon: '', linkUrl: '', publishId: undefined, sortOrder: 0, isVisible: true, remark: '' }
}

function handleAddChild() {
  if (!editingMenu.value) return
  const parentId = editingMenu.value.id
  editingMenu.value = null
  form.value = { name: '', parentId, menuType: 'folder', icon: '', linkUrl: '', publishId: undefined, sortOrder: 0, isVisible: true, remark: '' }
}

async function handleSave() {
  if (!form.value.name) { ElMessage.warning('请输入菜单名称'); return }
  saving.value = true
  try {
    if (editingMenu.value) {
      await updateMenu(editingMenu.value.id, form.value)
      ElMessage.success('更新成功')
    } else {
      await createMenu(form.value)
      ElMessage.success('创建成功')
    }
    await loadData()
  } finally { saving.value = false }
}

async function handleDelete() {
  if (!editingMenu.value) return
  await ElMessageBox.confirm('确定删除该菜单吗？', '确认删除', { type: 'warning' })
  await deleteMenu(editingMenu.value.id)
  ElMessage.success('删除成功')
  resetForm()
  await loadData()
}

function resetForm() {
  editingMenu.value = null
  form.value = { name: '', parentId: 0, menuType: 'folder', icon: '', linkUrl: '', publishId: undefined, sortOrder: 0, isVisible: true, remark: '' }
}

function getMenuTypeTag(type: string) {
  return type === 'folder' ? 'info' : type === 'link' ? 'warning' : 'success'
}
function getMenuTypeText(type: string) {
  return type === 'folder' ? '目录' : type === 'link' ? '链接' : '发布'
}
</script>

<style scoped>
.menu-manage { padding: 20px; }
.card-header { display: flex; justify-content: space-between; align-items: center; }
.tree-node { display: flex; align-items: center; gap: 8px; }
</style>

