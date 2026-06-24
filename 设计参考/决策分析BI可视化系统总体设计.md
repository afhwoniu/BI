# 决策分析 BI 可视化系统总体设计

## 1. 项目背景与建设目标

### 1.1 背景

在医疗机构/区域医共体场景下，存在大量分散在 HIS、LIS、EMR、医保、财务等系统中的业务数据。传统报表多为固定格式，难以灵活支持临时分析、跨维度钻取、多终端展示的需求。

本项目参考《DataInsight 操作手册》的理念，建设一套**通用的决策分析 BI 可视化平台**，由业务人员通过拖拽、配置即可完成大部分数据分析与可视化工作，减少对开发人员的依赖。

### 1.2 建设目标

- 通过 **SQL 定义数据集**，屏蔽底层复杂表结构，支持多种数据源；
- 在数据集之上，以 **图表、报表、分析面板** 等形式对数据进行可视化与分析；
- 支持：
  - 维度/度量配置、拖拽行列、筛选器；
  - 外链参数（URL 参数控制筛选）；
  - 下钻联动、全局筛选；
  - 同环比分析、计算列；
- 同一数据集可复用在多个图表 / 报表 / 分析中；
- 覆盖 **大屏展示、PC Web 端、移动端** 多终端访问场景；
- 支持 **统一菜单与权限控制、集中展示平台、备份与还原**。

### 1.3 技术栈约束

| 类别 | 技术选型 |
|------|----------|
| 后端 | ASP.NET Core 9（RESTful API） |
| 前端 | Vue 3 + TypeScript + Element Plus + ECharts |
| 管理数据库 | PostgreSQL，库名：`bi_management_v4` |
| 菜单形式 | 侧边菜单导航 |

---

## 2. 整体架构设计

### 2.1 总体架构视图

系统采用 **前后端分离 + 元数据驱动** 的架构：

- 前端 Vue 3 + Element Plus + ECharts 提供管理端、PC 展示端、大屏端、移动端四类 UI；
- 后端 ASP.NET Core 9 暴露统一 RESTful API；
- PostgreSQL `bi_management_v4` 存储平台自身的元数据和配置；
- 各业务数据源（SQLServer / PostgreSQL / Analysis Server / Excel 等）通过"数据源配置"接入。

可以抽象为四层：

1. **表示层（UI 层）**：Vue 3 + Element Plus + ECharts（管理端/展示端/大屏/移动端）
2. **接口层（API 层）**：ASP.NET Core Controllers
3. **业务层（Domain & Application）**：领域模型 + 应用服务
4. **数据访问层（Infrastructure）**：管理库 + 业务库 + 缓存

### 2.2 后端架构（ASP.NET Core）

#### 2.2.1 分层设计

- **API 层**
  - 各模块控制器：`DatasourcesController`、`DatasetsController`、`ChartsController`、`PanelsController`、`ReportsController`、`MenusController` 等；
  - 统一返回规范（统一响应模型）、统一异常处理与日志拦截；
  - 鉴权：JWT 中间件，支持角色/组织上下文。

- **应用层**
  - 按业务用例组织服务，如：
    - `DatasetAppService`：新建/编辑/预览数据集；
    - `ChartAppService`：生成图表 SQL、执行图表查询；
    - `PanelAppService`：面板布局保存、面板渲染；
    - `ReportAppService`：报表分页配置与渲染；
  - 负责参数校验、权限判断、事务控制。

- **领域层**
  - 领域对象：
    - `Datasource`（数据源）
    - `Dataset` / `DatasetField`（数据集与字段元数据）
    - `Chart`（图表）
    - `Panel` / `PanelItem` / `PanelLink`（分析面板）
    - `Report` / `ReportPage` / `ReportItem`（报表/报告）
    - `Menu`（菜单）
    - `User` / `Role` / `Org`（用户/角色/组织）
  - 核心业务规则：
    - SQL 语句约束：仅 SELECT，不允许 LIMIT/TOP、GROUP BY、ORDER BY；
    - 维度/度量与聚合规则；
    - 下钻维度映射规则；
    - 行级权限规则（组织维度绑定）。

- **基础设施层**
  - EF Core + Npgsql 访问 `bi_management_v4`；
  - Dapper / ADO.NET 动态访问业务数据库（根据数据源类型动态选择驱动）；
  - 缓存（内存/Redis）：图表结果缓存、元数据缓存；
  - 日志与审计（Serilog/NLog）。

#### 2.2.2 数据访问策略

- 管理库部分使用 EF Core（强类型 + 迁移）；
- 对于动态 SQL 执行，使用 Dapper/ADO.NET：
  - 避免 ORM 对复杂聚合 SQL 的约束；
  - 统一封装"参数化 SQL 执行、超时、异常转换"。

### 2.3 前端架构（Vue 3）

#### 2.3.1 模块划分

| 路由前缀 | 说明 |
|----------|------|
| `/` | 管理端：数据源、数据集、图表、面板、报表、菜单、权限、系统设置 |
| `/portal` | PC 展示端：按菜单展示已发布的面板/报表/图表 |
| `/screen` | 大屏展示：全屏显示指定面板 |
| `/m` | 移动端：简化布局的关键面板/图表展示 |

#### 2.3.2 前端技术选型

- Vue 3 + TypeScript + Vite 6
- UI：Element Plus（管理端 + Portal），可选 Vant 4（移动端）
- 图表：Apache ECharts（vue-echarts）
- 布局拖拽：vue-grid-layout
- 状态管理：Pinia
- 路由：Vue Router 4（按模块懒加载）

### 2.4 数据架构（管理库 + 业务库）

#### 2.4.1 管理库（bi_management_v4）

- 责任：只存"配置"和"元数据"，不存业务事实数据；
- 主要表（仅列出核心）：

| 分类 | 表名 | 说明 |
|------|------|------|
| 数据源 | `bi_datasource` | 数据源连接配置 |
| 数据集 | `bi_dataset` | SQL 数据集定义 |
| 数据集字段 | `bi_dataset_field` | 字段元数据（维度/度量） |
| 图表 | `bi_chart` | 图表配置 |
| 分析面板 | `bi_panel` / `bi_panel_item` / `bi_panel_link` | 面板、子项、联动 |
| 报表 | `bi_report` / `bi_report_page` / `bi_report_item` | 报表、页、元素 |
| 菜单 | `sys_menu` | 菜单树 |
| 发布 | `bi_publish` | 发布记录 |
| 用户权限 | `sys_user` / `sys_role` / `sys_org` / `sys_role_menu` | 用户、角色、组织、权限 |

字段规范：
- 命名统一使用小写 + 下划线，便于前后端一致；
- 每个表和字段添加中文注释，标明业务含义。

#### 2.4.2 业务库

- 通过 `bi_datasource` 配置连接各业务数据库；
- 只做只读访问；
- 数据集 SQL 由业务方/数据开发者编写，平台做基本语法/安全校验。

### 2.5 部署架构（简要）

- 应用服务：ASP.NET Core 容器部署 / Windows 服务部署；
- 数据库：PostgreSQL 独立实例或集群；
- 前端：Vue 应用构建后部署到 Nginx / 静态服务器；
- 推荐：通过反向代理统一访问入口（如 `/api` 代理到后端，`/` 返回前端）。

---

## 3. 核心业务模型设计

### 3.1 概念模型总览

核心概念对照《DataInsight操作手册》如下：

| 概念 | 说明 |
|------|------|
| 数据源（Datasource） | 连接到某个数据库或数据仓库 |
| 数据集（Dataset） | 以 SQL 语句形式定义的数据集合，是后续分析基础 |
| 维度（Dimension） | 分类字段，如日期、科室、病区等 |
| 度量（Measure） | 数值字段，如费用、数量、次数等 |
| 图表（Chart） | 对数据集依据配置进行的可视化呈现（柱图、折线、饼图等） |
| 分析面板（Panel/Analysis） | 在一个面板中呈现多张图表/报表，支持联动 |
| 报表/报告（Report） | 分页、说明文本、图表/分析组合成的"讲故事"式报告 |
| 集中展示平台（Portal） | 按菜单分类组织分析和报告，面向最终用户 |

### 3.2 数据源模型（bi_datasource）

```sql
-- 数据源表
CREATE TABLE bi_datasource (
    id              BIGSERIAL PRIMARY KEY,           -- 主键
    name            VARCHAR(100) NOT NULL,           -- 数据源名称
    type            VARCHAR(50) NOT NULL,            -- 类型：sqlserver/postgres/analysis/excel
    conn_string     TEXT NOT NULL,                   -- 连接字符串（加密存储）
    remark          VARCHAR(500),                    -- 说明
    is_enabled      BOOLEAN DEFAULT TRUE,            -- 是否启用
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,  -- 创建时间
    updated_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP   -- 更新时间
);
COMMENT ON TABLE bi_datasource IS '数据源配置表';
```

### 3.3 数据集与字段模型（bi_dataset / bi_dataset_field）

#### 3.3.1 SQL 约束（对齐 DataInsight 语构模式）

- 仅允许 `SELECT` 语句；
- 不允许：`ORDER BY`、`GROUP BY`、`TOP` / `LIMIT` 等行数限制；
- 聚合逻辑由图表层配置生成。

#### 3.3.2 数据库表设计

```sql
-- 数据集表
CREATE TABLE bi_dataset (
    id              BIGSERIAL PRIMARY KEY,           -- 主键
    name            VARCHAR(100) NOT NULL,           -- 数据集名称
    datasource_id   BIGINT NOT NULL,                 -- 所属数据源ID
    sql_text        TEXT NOT NULL,                   -- 原始SQL语句
    param_schema    JSONB DEFAULT '[]',              -- 参数定义JSON
    remark          VARCHAR(500),                    -- 说明
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (datasource_id) REFERENCES bi_datasource(id)
);
COMMENT ON TABLE bi_dataset IS 'SQL数据集定义表';

-- 数据集字段表
CREATE TABLE bi_dataset_field (
    id              BIGSERIAL PRIMARY KEY,           -- 主键
    dataset_id      BIGINT NOT NULL,                 -- 所属数据集ID
    field_name      VARCHAR(100) NOT NULL,           -- 字段名
    field_alias     VARCHAR(100),                    -- 字段别名（显示名）
    data_type       VARCHAR(50) NOT NULL,            -- 数据类型
    role            VARCHAR(20) NOT NULL,            -- dim=维度, measure=度量
    agg_type        VARCHAR(20) DEFAULT 'none',      -- 聚合类型：sum/count/avg/max/min/none
    sort_order      INT DEFAULT 0,                   -- 排序顺序
    FOREIGN KEY (dataset_id) REFERENCES bi_dataset(id) ON DELETE CASCADE
);
COMMENT ON TABLE bi_dataset_field IS '数据集字段元数据表';
```

#### 3.3.3 参数定义 JSON 结构示例

```json
[
  {
    "name": "startDate",
    "type": "date",
    "required": true,
    "defaultValue": null,
    "description": "开始日期"
  },
  {
    "name": "orgCode",
    "type": "string",
    "required": false,
    "defaultValue": "",
    "description": "组织编码"
  }
]
```

### 3.4 图表模型（bi_chart）

```sql
-- 图表表
CREATE TABLE bi_chart (
    id              BIGSERIAL PRIMARY KEY,           -- 主键
    name            VARCHAR(100) NOT NULL,           -- 图表名称
    dataset_id      BIGINT NOT NULL,                 -- 关联数据集ID
    chart_type      VARCHAR(50) NOT NULL,            -- 图表类型：bar/line/pie/table/kpi等
    config_json     JSONB NOT NULL,                  -- 图表配置JSON
    remark          VARCHAR(500),                    -- 说明
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (dataset_id) REFERENCES bi_dataset(id)
);
COMMENT ON TABLE bi_chart IS '图表配置表';
```

#### 图表配置 JSON 结构示例

```json
{
  "dimensions": ["order_date:month", "department"],
  "measures": [
    {"field": "amount", "agg": "sum", "alias": "金额合计"},
    {"field": "quantity", "agg": "count", "alias": "数量"}
  ],
  "filters": [
    {"field": "region", "op": "in", "values": ["北区", "南区"]}
  ],
  "yoy": {"enabled": false, "dateField": "order_date"},
  "style": {
    "theme": "light",
    "stack": false,
    "showLabel": true
  }
}
```

### 3.5 分析面板模型（bi_panel 等）

```sql
-- 分析面板表
CREATE TABLE bi_panel (
    id              BIGSERIAL PRIMARY KEY,           -- 主键
    name            VARCHAR(100) NOT NULL,           -- 面板名称
    panel_type      VARCHAR(50) DEFAULT 'pc_dashboard', -- pc_dashboard/big_screen/mobile
    config_json     JSONB DEFAULT '{}',              -- 面板级配置（主题、全局筛选等）
    remark          VARCHAR(500),
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
COMMENT ON TABLE bi_panel IS '分析面板表';

-- 面板子项表
CREATE TABLE bi_panel_item (
    id              BIGSERIAL PRIMARY KEY,           -- 主键
    panel_id        BIGINT NOT NULL,                 -- 所属面板ID
    chart_id        BIGINT,                          -- 关联图表ID
    report_id       BIGINT,                          -- 关联报表ID（二选一）
    layout_json     JSONB NOT NULL,                  -- 布局信息 {x, y, w, h}
    sort_order      INT DEFAULT 0,                   -- 排序
    FOREIGN KEY (panel_id) REFERENCES bi_panel(id) ON DELETE CASCADE,
    FOREIGN KEY (chart_id) REFERENCES bi_chart(id)
);
COMMENT ON TABLE bi_panel_item IS '面板子项表';

-- 面板联动关系表
CREATE TABLE bi_panel_link (
    id              BIGSERIAL PRIMARY KEY,           -- 主键
    panel_id        BIGINT NOT NULL,                 -- 所属面板
    source_item_id  BIGINT NOT NULL,                 -- 源图表面板项ID
    target_item_id  BIGINT NOT NULL,                 -- 目标图表面板项ID
    field_mapping   JSONB NOT NULL,                  -- 维度映射关系
    FOREIGN KEY (panel_id) REFERENCES bi_panel(id) ON DELETE CASCADE
);
COMMENT ON TABLE bi_panel_link IS '面板下钻联动关系表';
```

### 3.6 报表/报告模型（bi_report 等）

```sql
-- 报表主表
CREATE TABLE bi_report (
    id              BIGSERIAL PRIMARY KEY,
    name            VARCHAR(100) NOT NULL,           -- 报表名称
    description     TEXT,                            -- 描述
    cover_url       VARCHAR(500),                    -- 封面图URL
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
COMMENT ON TABLE bi_report IS '报表/报告主表';

-- 报表页表
CREATE TABLE bi_report_page (
    id              BIGSERIAL PRIMARY KEY,
    report_id       BIGINT NOT NULL,
    page_title      VARCHAR(200),                    -- 页标题
    page_order      INT DEFAULT 0,                   -- 页顺序
    note_text       TEXT,                            -- 说明文本
    FOREIGN KEY (report_id) REFERENCES bi_report(id) ON DELETE CASCADE
);
COMMENT ON TABLE bi_report_page IS '报表页表';

-- 报表页元素表
CREATE TABLE bi_report_item (
    id              BIGSERIAL PRIMARY KEY,
    page_id         BIGINT NOT NULL,
    item_type       VARCHAR(50) NOT NULL,            -- chart/panel/text/image
    ref_id          BIGINT,                          -- 引用ID（图表或面板）
    content         TEXT,                            -- 文本内容（item_type=text时）
    layout_json     JSONB NOT NULL,                  -- 布局 {x, y, w, h}
    FOREIGN KEY (page_id) REFERENCES bi_report_page(id) ON DELETE CASCADE
);
COMMENT ON TABLE bi_report_item IS '报表页元素表';
```

### 3.7 菜单与发布模型

```sql
-- 系统菜单表
CREATE TABLE sys_menu (
    id              BIGSERIAL PRIMARY KEY,
    parent_id       BIGINT DEFAULT 0,                -- 父菜单ID，0表示顶级
    name            VARCHAR(100) NOT NULL,           -- 菜单名称
    icon            VARCHAR(100),                    -- 图标
    sort_order      INT DEFAULT 0,                   -- 排序
    link_type       VARCHAR(50),                     -- panel/report/chart/external
    link_target_id  BIGINT,                          -- 内部对象ID
    link_url        VARCHAR(500),                    -- 外部链接URL
    is_enabled      BOOLEAN DEFAULT TRUE
);
COMMENT ON TABLE sys_menu IS '系统菜单表';

-- 发布记录表
CREATE TABLE bi_publish (
    id              BIGSERIAL PRIMARY KEY,
    object_type     VARCHAR(50) NOT NULL,            -- panel/report/chart
    object_id       BIGINT NOT NULL,                 -- 对象ID
    access_token    VARCHAR(100) UNIQUE,             -- 访问令牌
    access_scope    VARCHAR(50) DEFAULT 'private',   -- public/private/role
    allowed_roles   JSONB DEFAULT '[]',              -- 允许的角色列表
    published_at    TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
COMMENT ON TABLE bi_publish IS '发布记录表';
```

---

## 4. 多终端支持设计

### 4.1 PC 管理端

- 左侧侧边菜单分模块：数据源 / 数据集 / 图表 / 分析 / 报表 / 菜单 / 权限 / 系统设置；
- 页面设计上注重：列表 + 编辑器两栏布局，大量使用弹窗/抽屉减少页面跳转。

### 4.2 PC 展示端（Portal）

- 侧边菜单 + 顶部面包屑；
- 菜单点击后加载对应已发布对象（面板/报表/图表）；
- 支持 URL 外链参数用于深度链接。

### 4.3 大屏端（Screen）

- 专用路由 `/screen/:panelId` 或附带 token；
- 默认全屏、隐藏菜单、深色主题；
- 支持定时刷新、跨页面轮播（可选）。

### 4.4 移动端（Mobile）

- 响应式 / 独立路由 `/m`；
- 简化布局：单列/双列卡片、折叠式筛选区；
- 重点展示关键指标、趋势类图表。

---

## 5. RESTful API 规范

### 5.1 命名规范

- 使用小写 + 复数资源名；
- 使用版本前缀 `/api/v1`；
- 遵循 HTTP 动词语义：GET（查询）、POST（创建/动作）、PUT（全更新）、PATCH（局部更新）、DELETE（删除）。

### 5.2 典型接口列表

| 模块 | 方法 | 路径 | 说明 |
|------|------|------|------|
| 数据源 | GET | `/api/v1/datasources` | 获取列表 |
| 数据源 | POST | `/api/v1/datasources` | 创建 |
| 数据源 | GET | `/api/v1/datasources/{id}` | 获取详情 |
| 数据源 | PUT | `/api/v1/datasources/{id}` | 更新 |
| 数据源 | DELETE | `/api/v1/datasources/{id}` | 删除 |
| 数据源 | POST | `/api/v1/datasources/{id}/test` | 测试连接 |
| 数据集 | GET | `/api/v1/datasets` | 获取列表 |
| 数据集 | POST | `/api/v1/datasets` | 创建 |
| 数据集 | POST | `/api/v1/datasets/{id}/preview` | 预览数据 |
| 图表 | GET | `/api/v1/charts` | 获取列表 |
| 图表 | POST | `/api/v1/charts` | 创建 |
| 图表 | POST | `/api/v1/charts/{id}/query` | 执行查询 |
| 面板 | GET | `/api/v1/panels/{id}` | 获取面板详情 |
| 面板 | POST | `/api/v1/panels` | 创建面板 |
| 发布 | POST | `/api/v1/publish` | 发布对象 |
| 发布 | GET | `/api/v1/view/{token}` | 访问已发布内容 |

---

## 6. 安全性与性能设计

### 6.1 安全性

- **认证授权**：使用 JWT 或 OAuth2.0，Token 中承载用户 ID、角色、组织编码等；
- **SQL 安全**：
  - 对用户输入的 SQL 做 AST 解析或关键字检查；
  - 只允许 SELECT 语句；
  - 禁止写操作和管理语句（如 DROP/ALTER/INSERT/UPDATE/DELETE）；
- **数据源安全**：连接字符串 AES 加密存储，后端运行时解密连接。

### 6.2 性能与扩展性

- **图表结果缓存**：按"图表ID + 参数"作为缓存键，设置合理过期时间；
- **分页与行数限制**：所有表格类图表默认启用分页，限制最大返回行数；
- **异步查询**：对于特别大的报表或复杂图表，支持异步任务 + 轮询获取结果。

---

## 7. 运维与备份

- 提供管理库元数据的"备份/还原"功能：
  - 备份内容：数据源配置（不含明文密码）、数据集、字段、图表、面板、报表、菜单、发布；
  - 排除内容：文件型数据源、图片、日志、大对象；
- 日志与监控：
  - 接口访问日志（用户、IP、耗时）；
  - 关键错误告警（如 SQL 执行失败、数据源连接失败）。

---

## 8. 智能BI模块（AI增强）

### 8.1 模块定位

智能BI模块是系统的AI增强能力层，让业务人员通过**自然语言对话**完成数据查询与可视化，降低使用门槛。

### 8.2 核心能力

| 能力 | 说明 |
|------|------|
| **NL2SQL** | 自然语言转SQL，AI理解用户意图生成查询语句 |
| **智能图表推荐** | 根据数据特征自动推荐最佳图表类型 |
| **对话式分析** | 支持多轮对话，逐步深入分析 |
| **SQL安全验证** | 确保AI生成的SQL安全可执行 |

### 8.3 技术选型

- **LLM服务**：DeepSeek V3 API（可插拔，支持OpenAI等）
- **Schema获取**：动态获取数据源表结构供AI参考
- **安全验证**：SQL白名单机制，仅允许SELECT查询

### 8.4 数据流程

```
用户自然语言 → AI意图解析 → 获取Schema → 生成SQL → 安全验证 → 推荐图表 → 执行查询 → 渲染展示
```

### 8.5 关键表结构

| 表名 | 说明 |
|------|------|
| `bi_ai_session` | AI对话会话表 |
| `bi_ai_message` | AI对话消息表 |
| `bi_ai_favorite` | AI查询收藏表 |

> 详细设计见《智能BI模块设计方案.md》

---

## 9. 未来扩展方向

- 增加更多图表类型与高级分析能力（如漏斗图、桑基图、组合图）；
- 支持指标体系（KPI 指标树）管理；
- 支持简单数据准备（字段映射、简单 ETL）；
- 接入统一用户中心 / 单点登录（SSO），与现有业务系统深度集成；
- 智能BI能力持续增强（Schema RAG、本地模型支持、智能推荐优化）。

---

> 文档版本：v1.1
> 更新日期：2025-12-26
> 变更说明：新增第8章智能BI模块设计概要

