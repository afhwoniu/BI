# 智能BI模块设计方案

> 版本：v2.0（RAG增强版）
> 更新日期：2025-12-26
> 模块定位：基于AI + RAG的自然语言查询与智能可视化

## 目录

1. [模块概述](#1-模块概述)
2. [系统架构](#2-系统架构)
3. [核心组件设计](#3-核心组件设计)
   - 3.1 Schema元数据服务
   - 3.2 LLM服务接口
   - 3.3 Embedding向量服务
   - 3.4 指标检索服务
   - 3.5 AI分析响应DSL
   - 3.6 RAG增强Prompt工程模板
4. [数据库扩展设计](#4-数据库扩展设计)
   - 4.1 pgvector扩展安装
   - 4.2 指标知识库表（核心）
   - 4.3 指标使用示例数据
   - 4.4 对话历史表
   - 4.5 向量检索SQL示例
5. [API接口设计](#5-api接口设计)
6. [前端设计](#6-前端设计)
7. [安全设计](#7-安全设计)
8. [图表智能推荐](#8-图表智能推荐)
9. [成本与性能优化](#9-成本与性能优化)
10. [实施计划](#10-实施计划)
11. [风险与应对](#11-风险与应对)
12. [与现有系统集成](#12-与现有系统集成)
13. [附录：指标知识库最佳实践](#13-附录指标知识库最佳实践)

---

## 1. 模块概述

### 1.1 背景与目标

传统BI工具需要用户具备一定的技术背景（编写SQL、配置图表），学习成本较高。本模块引入AI能力，让业务人员通过**自然语言对话**的方式完成数据查询与可视化，降低使用门槛。

**医疗场景特殊需求**：
- 医疗指标有严格的**计算口径**定义（如：门诊人次、出院人数、床位使用率等）
- 不同医院/区域可能有不同的指标定义版本
- AI 需要理解业务术语并按标准口径生成 SQL

为解决上述问题，本模块引入 **RAG（检索增强生成）** 架构，通过向量检索匹配相关指标定义，再让 AI 基于标准口径生成 SQL。

### 1.2 核心能力

| 能力 | 说明 |
|------|------|
| **指标知识库** | 存储医疗指标定义、计算公式、SQL模板，支持向量检索 |
| **RAG增强NL2SQL** | 先检索相关指标定义，再结合Schema生成SQL |
| **智能图表推荐** | 根据数据特征自动推荐最佳图表类型 |
| **对话式分析** | 支持多轮对话，逐步深入分析 |
| **SQL安全验证** | 确保AI生成的SQL安全可执行 |

### 1.3 技术选型

| 组件 | 选择 | 说明 |
|------|------|------|
| LLM服务 | DeepSeek V3 API | 可插拔，支持OpenAI/本地模型 |
| 向量数据库 | PostgreSQL + pgvector | 复用管理库，无需额外部署 |
| Embedding模型 | text-embedding-3-small 或 BGE-M3 | 用于指标文本向量化 |
| 前端框架 | Vue 3 + Element Plus + ECharts | - |
| 前端对话组件 | 自定义Chat组件（基于Vue 3） | - |

---

## 2. 系统架构

### 2.1 架构图

```
┌──────────────────────────────────────────────────────────────────────────┐
│                        智能BI模块架构（RAG增强版）                         │
├──────────────────────────────────────────────────────────────────────────┤
│  前端层                                                                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌────────────┐  │
│  │ 对话输入框   │  │ 图表渲染区   │  │ SQL预览/编辑 │  │ 指标管理   │  │
│  │ (ChatInput)  │  │ (ChartView)  │  │ (SqlEditor)  │  │(KpiManager)│  │
│  └──────────────┘  └──────────────┘  └──────────────┘  └────────────┘  │
├──────────────────────────────────────────────────────────────────────────┤
│  API层                                                                    │
│  POST /api/v1/ai/chat            - 对话式查询（含RAG检索）                 │
│  GET  /api/v1/ai/schema/{dsId}   - 获取数据源Schema                       │
│  POST /api/v1/ai/execute         - 执行AI生成的查询                       │
│  GET  /api/v1/ai/history         - 获取对话历史                           │
│  ────────────────────────────────────────────────────────────────────    │
│  GET  /api/v1/kpi/list           - 指标列表                               │
│  POST /api/v1/kpi                - 创建指标                               │
│  POST /api/v1/kpi/search         - 向量检索指标                           │
│  POST /api/v1/kpi/embedding      - 批量生成向量                           │
├──────────────────────────────────────────────────────────────────────────┤
│  应用服务层                                                                │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │                      AiAnalysisService                              │  │
│  │  ┌────────────┐ ┌────────────┐ ┌──────────────┐ ┌──────────────┐  │  │
│  │  │KpiRetriever│ │SchemaProvider│ │PromptBuilder │ │ChartRecommend│  │  │
│  │  │(指标检索)   │ │(获取表结构)  │ │(构建Prompt)  │ │(推荐图表)    │  │  │
│  │  └────────────┘ └────────────┘ └──────────────┘ └──────────────┘  │  │
│  └────────────────────────────────────────────────────────────────────┘  │
├──────────────────────────────────────────────────────────────────────────┤
│  AI/向量层                                                                │
│  ┌─────────────────────────────┐  ┌─────────────────────────────────┐   │
│  │   LlmService (可插拔接口)    │  │   EmbeddingService              │   │
│  │  ├── DeepSeekProvider       │  │  ├── OpenAIEmbedding            │   │
│  │  ├── OpenAIProvider         │  │  ├── BGEEmbedding (本地)        │   │
│  │  └── LocalLlamaProvider     │  │  └── 向量维度: 1536/1024        │   │
│  └─────────────────────────────┘  └─────────────────────────────────┘   │
├──────────────────────────────────────────────────────────────────────────┤
│  数据层                                                                   │
│  ┌─────────────────────────────┐  ┌─────────────────────────────────┐   │
│  │   PostgreSQL + pgvector     │  │   业务数据源                     │   │
│  │  ├── bi_kpi_definition      │  │  ├── MySQL/PostgreSQL           │   │
│  │  ├── bi_kpi_category        │  │  ├── SQL Server                 │   │
│  │  └── bi_ai_session/message  │  │  └── 其他数据库                  │   │
│  └─────────────────────────────┘  └─────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────┘
```

### 2.2 RAG增强数据流程

```
用户输入自然语言（如："查看本月门诊人次"）
       ↓
  ┌─────────────────────────────────────┐
  │  1. 向量检索指标知识库（pgvector）   │
  │     - 将用户问题转为向量             │
  │     - 检索相似指标定义（Top 3-5）    │
  └─────────────────────────────────────┘
       ↓
  ┌─────────────────────────────────────┐
  │  2. 获取数据源Schema                 │
  │     - 相关表结构                     │
  │     - 字段说明和类型                 │
  └─────────────────────────────────────┘
       ↓
  ┌─────────────────────────────────────┐
  │  3. 构建增强Prompt                   │
  │     - 注入检索到的指标定义           │
  │     - 注入Schema信息                 │
  │     - 用户问题                       │
  └─────────────────────────────────────┘
       ↓
  ┌─────────────────────────────────────┐
  │  4. LLM生成SQL                       │
  │     - 基于指标口径生成准确SQL         │
  │     - 推荐图表类型                   │
  └─────────────────────────────────────┘
       ↓
   SQL安全验证
       ↓
   执行SQL查询
       ↓
 返回数据 + 图表配置 + 引用的指标定义
       ↓
  前端渲染图表（显示数据来源和计算口径）
```

---

## 3. 核心组件设计

### 3.1 Schema元数据服务

负责获取数据源的表结构信息，供AI理解数据库结构。

```csharp
public interface ISchemaService
{
    /// <summary>获取数据源的所有表</summary>
    Task<List<TableInfo>> GetTablesAsync(long datasourceId);
    
    /// <summary>获取表的字段信息</summary>
    Task<List<ColumnInfo>> GetColumnsAsync(long datasourceId, string tableName);
    
    /// <summary>生成Schema描述文本（用于Prompt）</summary>
    Task<string> BuildSchemaDescriptionAsync(long datasourceId, List<string>? tableNames = null);
}

public class TableInfo
{
    public string TableName { get; set; }     // 表名
    public string? Comment { get; set; }       // 表注释
    public int ColumnCount { get; set; }       // 字段数
}

public class ColumnInfo
{
    public string ColumnName { get; set; }     // 字段名
    public string DataType { get; set; }       // 数据类型
    public string? Comment { get; set; }        // 字段注释
    public bool IsNullable { get; set; }       // 是否可空
    public bool IsPrimaryKey { get; set; }     // 是否主键
}
```

### 3.2 LLM服务接口

可插拔的LLM服务设计，支持多种AI提供商。

```csharp
public interface ILlmService
{
    /// <summary>发送对话请求</summary>
    Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default);
}

public class LlmRequest
{
    public string SystemPrompt { get; set; }        // 系统提示词
    public List<ChatMessage> Messages { get; set; } // 对话历史
    public double Temperature { get; set; } = 0.1;  // 生成温度（SQL生成建议低温）
    public int MaxTokens { get; set; } = 2000;      // 最大token数
}

public class LlmResponse
{
    public bool Success { get; set; }
    public string Content { get; set; }             // AI回复内容
    public int PromptTokens { get; set; }           // 输入token数
    public int CompletionTokens { get; set; }       // 输出token数
    public string? ErrorMessage { get; set; }
}
```

### 3.3 Embedding向量服务

负责将文本转换为向量，支持多种Embedding模型。

```csharp
public interface IEmbeddingService
{
    /// <summary>生成文本向量</summary>
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default);

    /// <summary>批量生成向量</summary>
    Task<List<float[]>> GetEmbeddingsAsync(List<string> texts, CancellationToken ct = default);

    /// <summary>向量维度</summary>
    int Dimension { get; }
}

// OpenAI Embedding实现
public class OpenAIEmbeddingService : IEmbeddingService
{
    public int Dimension => 1536;  // text-embedding-3-small
    // ...
}

// BGE本地Embedding实现（可选）
public class BGEEmbeddingService : IEmbeddingService
{
    public int Dimension => 1024;  // BGE-M3
    // ...
}
```

### 3.4 指标检索服务

基于pgvector的向量相似度检索服务。

```csharp
public interface IKpiRetrieverService
{
    /// <summary>向量检索相关指标</summary>
    Task<List<KpiDefinition>> SearchAsync(string query, int topK = 5, long? datasourceId = null);

    /// <summary>为指标生成并保存向量</summary>
    Task UpdateEmbeddingAsync(long kpiId);

    /// <summary>批量更新所有指标向量</summary>
    Task RebuildAllEmbeddingsAsync();
}

public class KpiDefinition
{
    public long Id { get; set; }
    public string Name { get; set; }              // 指标名称，如"门诊人次"
    public string Description { get; set; }        // 指标说明
    public string Formula { get; set; }            // 计算公式/口径
    public string SqlTemplate { get; set; }        // SQL模板
    public string Category { get; set; }           // 分类
    public float Similarity { get; set; }          // 检索相似度
}
```

### 3.5 AI分析响应DSL

AI输出的结构化响应，增加引用指标信息。

```json
{
  "intent": "分析各科室本月门诊收入排名",
  "referencedKpis": [
    {"id": 12, "name": "门诊收入", "formula": "SUM(门诊费用)"}
  ],
  "sql": "SELECT department_name as 科室, SUM(total_fee) as 收入 FROM outpatient_records WHERE visit_date >= '2024-12-01' GROUP BY department_name ORDER BY 收入 DESC",
  "chartType": "bar",
  "config": {
    "title": "各科室本月门诊收入排名",
    "dimensions": ["科室"],
    "measures": [{"field": "收入", "aggType": "none", "alias": "收入(元)"}],
    "sortBy": [{"field": "收入", "order": "desc"}],
    "limit": 10
  },
  "explanation": "该查询统计了本月各科室的门诊总收入（计算口径：门诊就诊产生的所有费用合计），并按收入从高到低排序"
}
```

### 3.6 RAG增强Prompt工程模板

```
你是医院数据分析专家，帮助用户分析医疗数据。请严格按照以下规则工作：

## 规则
1. 仅生成 SELECT 查询语句，禁止 INSERT/UPDATE/DELETE/DROP 等操作
2. 必须使用提供的表和字段，不要假设不存在的表
3. **优先使用下方提供的指标定义和SQL模板**，确保计算口径一致
4. 日期过滤使用参数化查询格式
5. 输出必须为有效的JSON格式

## 相关指标定义（请严格按照这些口径计算）
{kpi_definitions}

## 可用数据表
{schema_info}

## 输出格式
```json
{
  "intent": "用户意图描述",
  "referencedKpis": [{"id": 指标ID, "name": "指标名称", "formula": "计算公式"}],
  "sql": "生成的SQL语句",
  "chartType": "bar|line|pie|table|kpi",
  "config": {
    "title": "图表标题",
    "dimensions": ["维度字段"],
    "measures": [{"field": "字段名", "aggType": "sum|avg|count|max|min|none", "alias": "显示名"}],
    "sortBy": [{"field": "字段名", "order": "asc|desc"}],
    "limit": 10
  },
  "explanation": "结果解释说明（包含使用的计算口径）"
}
```

## 用户问题
{user_question}
```

**Prompt中注入的指标定义示例**：

```
## 相关指标定义（请严格按照这些口径计算）

### 指标1：门诊人次
- ID: 5
- 分类: 医疗服务/门诊
- 说明: 门诊挂号就诊的人次数，同一患者多次就诊算多次
- 计算公式: COUNT(DISTINCT visit_id)
- SQL模板: SELECT COUNT(DISTINCT visit_id) as 门诊人次 FROM outpatient_records WHERE visit_date BETWEEN @start AND @end
- 相关表: outpatient_records

### 指标2：门诊收入
- ID: 12
- 分类: 财务/收入
- 说明: 门诊产生的所有医疗费用合计
- 计算公式: SUM(total_fee)
- SQL模板: SELECT SUM(total_fee) as 门诊收入 FROM outpatient_billing WHERE billing_date BETWEEN @start AND @end
- 相关表: outpatient_billing
```

---

## 4. 数据库扩展设计

### 4.1 pgvector扩展安装

```sql
-- 在PostgreSQL中启用pgvector扩展
CREATE EXTENSION IF NOT EXISTS vector;
```

### 4.2 指标知识库表（核心）

```sql
-- 指标分类表
CREATE TABLE bi_kpi_category (
    id              BIGSERIAL PRIMARY KEY,           -- 主键
    parent_id       BIGINT DEFAULT 0,                -- 父分类ID，0表示顶级
    name            VARCHAR(100) NOT NULL,           -- 分类名称
    code            VARCHAR(50),                     -- 分类编码
    sort_order      INT DEFAULT 0,                   -- 排序顺序
    remark          VARCHAR(500),                    -- 说明
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
COMMENT ON TABLE bi_kpi_category IS '指标分类表';
COMMENT ON COLUMN bi_kpi_category.name IS '分类名称，如：医疗服务、财务收入、运营效率';

-- 指标定义表（核心知识库）
CREATE TABLE bi_kpi_definition (
    id              BIGSERIAL PRIMARY KEY,           -- 主键
    category_id     BIGINT,                          -- 所属分类ID
    datasource_id   BIGINT,                          -- 关联数据源ID（可选，null表示通用）
    code            VARCHAR(50) UNIQUE,              -- 指标编码，如 KPI_001
    name            VARCHAR(100) NOT NULL,           -- 指标名称，如"门诊人次"
    alias           VARCHAR(200),                    -- 别名/同义词，逗号分隔，如"门诊量,门诊就诊人次"
    description     TEXT,                            -- 指标说明（详细解释口径）
    formula         VARCHAR(500),                    -- 计算公式，如 COUNT(DISTINCT visit_id)
    unit            VARCHAR(50),                     -- 单位，如"人次"、"元"
    sql_template    TEXT,                            -- SQL模板（带参数占位符）
    related_tables  VARCHAR(500),                    -- 相关表名，逗号分隔
    related_fields  VARCHAR(500),                    -- 相关字段，逗号分隔
    dimension_hint  VARCHAR(200),                    -- 常用维度提示，如"时间,科室,医生"
    data_type       VARCHAR(20) DEFAULT 'number',    -- 数据类型：number/percent/currency
    precision_num   INT DEFAULT 2,                   -- 小数精度
    is_enabled      BOOLEAN DEFAULT TRUE,            -- 是否启用
    version         VARCHAR(20) DEFAULT '1.0',       -- 版本号
    embedding       vector(1536),                    -- 向量字段（1536维，适配OpenAI）
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (category_id) REFERENCES bi_kpi_category(id),
    FOREIGN KEY (datasource_id) REFERENCES bi_datasource(id)
);
COMMENT ON TABLE bi_kpi_definition IS '指标定义知识库表';
COMMENT ON COLUMN bi_kpi_definition.name IS '指标名称，用于向量检索匹配';
COMMENT ON COLUMN bi_kpi_definition.alias IS '别名/同义词，提高检索召回率';
COMMENT ON COLUMN bi_kpi_definition.formula IS '标准计算公式，确保口径一致';
COMMENT ON COLUMN bi_kpi_definition.sql_template IS 'SQL模板，@start @end 等参数占位符';
COMMENT ON COLUMN bi_kpi_definition.embedding IS '指标描述的向量表示，用于相似度检索';

-- 创建向量索引（使用IVFFlat或HNSW算法）
CREATE INDEX idx_kpi_embedding ON bi_kpi_definition
USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);

-- 或使用HNSW索引（查询更快，占用更多内存）
-- CREATE INDEX idx_kpi_embedding ON bi_kpi_definition
-- USING hnsw (embedding vector_cosine_ops) WITH (m = 16, ef_construction = 64);
```

### 4.3 指标使用示例数据

```sql
-- 插入分类示例
INSERT INTO bi_kpi_category (name, code, sort_order) VALUES
('医疗服务', 'medical_service', 1),
('财务收入', 'finance_income', 2),
('运营效率', 'operation', 3),
('质量安全', 'quality', 4);

-- 插入指标定义示例
INSERT INTO bi_kpi_definition (category_id, code, name, alias, description, formula, unit, sql_template, related_tables, dimension_hint) VALUES
(1, 'KPI_001', '门诊人次', '门诊量,门诊就诊人次,门诊人数',
 '统计门诊挂号就诊的人次数。同一患者在同一天挂多个号算多次，不同天就诊也算多次。',
 'COUNT(DISTINCT visit_id)', '人次',
 'SELECT COUNT(DISTINCT visit_id) as 门诊人次 FROM outpatient_records WHERE visit_date BETWEEN @start_date AND @end_date',
 'outpatient_records', '时间,科室,医生'),

(1, 'KPI_002', '出院人数', '出院人次,出院量',
 '统计住院患者出院的人数。以出院日期为准，同一患者多次住院出院算多次。',
 'COUNT(DISTINCT admission_id)', '人',
 'SELECT COUNT(DISTINCT admission_id) as 出院人数 FROM inpatient_records WHERE discharge_date BETWEEN @start_date AND @end_date',
 'inpatient_records', '时间,科室,病区'),

(2, 'KPI_010', '门诊收入', '门诊医疗收入,门诊总收入',
 '门诊产生的所有医疗费用合计，包括挂号费、诊查费、检查费、药品费等。',
 'SUM(total_fee)', '元',
 'SELECT SUM(total_fee) as 门诊收入 FROM outpatient_billing WHERE billing_date BETWEEN @start_date AND @end_date',
 'outpatient_billing', '时间,科室,费用类型'),

(3, 'KPI_020', '床位使用率', '床位占用率,病床使用率',
 '实际占用床日数与开放床日数的比值。开放床日数=开放床位数×统计天数。',
 'SUM(occupied_bed_days) / SUM(open_bed_days) * 100', '%',
 'SELECT ROUND(SUM(occupied_bed_days)::numeric / NULLIF(SUM(open_bed_days), 0) * 100, 2) as 床位使用率 FROM bed_statistics WHERE stat_date BETWEEN @start_date AND @end_date',
 'bed_statistics', '时间,科室,病区'),

(3, 'KPI_021', '平均住院日', '平均住院天数',
 '出院患者的平均住院天数。计算公式：总住院天数/出院人数。',
 'SUM(length_of_stay) / COUNT(DISTINCT admission_id)', '天',
 'SELECT ROUND(SUM(length_of_stay)::numeric / NULLIF(COUNT(DISTINCT admission_id), 0), 1) as 平均住院日 FROM inpatient_records WHERE discharge_date BETWEEN @start_date AND @end_date',
 'inpatient_records', '时间,科室,病种');
```

### 4.4 对话历史表

```sql
-- AI对话会话表
CREATE TABLE bi_ai_session (
    id              BIGSERIAL PRIMARY KEY,           -- 主键
    user_id         BIGINT,                          -- 用户ID
    datasource_id   BIGINT NOT NULL,                 -- 关联数据源
    title           VARCHAR(200),                    -- 会话标题
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (datasource_id) REFERENCES bi_datasource(id)
);
COMMENT ON TABLE bi_ai_session IS 'AI对话会话表';

-- AI对话消息表
CREATE TABLE bi_ai_message (
    id              BIGSERIAL PRIMARY KEY,           -- 主键
    session_id      BIGINT NOT NULL,                 -- 所属会话ID
    role            VARCHAR(20) NOT NULL,            -- user/assistant/system
    content         TEXT NOT NULL,                   -- 消息内容
    sql_text        TEXT,                            -- 生成的SQL（仅assistant）
    chart_type      VARCHAR(50),                     -- 推荐的图表类型
    config_json     JSONB,                           -- 图表配置
    referenced_kpis JSONB,                           -- 引用的指标ID列表
    token_count     INT DEFAULT 0,                   -- token消耗
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (session_id) REFERENCES bi_ai_session(id) ON DELETE CASCADE
);
COMMENT ON TABLE bi_ai_message IS 'AI对话消息表';
COMMENT ON COLUMN bi_ai_message.referenced_kpis IS '引用的指标定义，如 [{"id":1,"name":"门诊人次"}]';

-- AI查询收藏表（可选）
CREATE TABLE bi_ai_favorite (
    id              BIGSERIAL PRIMARY KEY,           -- 主键
    user_id         BIGINT,                          -- 用户ID
    message_id      BIGINT NOT NULL,                 -- 关联消息ID
    name            VARCHAR(200),                    -- 收藏名称
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (message_id) REFERENCES bi_ai_message(id) ON DELETE CASCADE
);
COMMENT ON TABLE bi_ai_favorite IS 'AI查询收藏表';
```

### 4.5 向量检索SQL示例

```sql
-- 根据用户问题检索相似指标（余弦相似度）
-- 假设 @query_embedding 是用户问题的向量
SELECT
    id, name, alias, description, formula, sql_template,
    1 - (embedding <=> @query_embedding) as similarity  -- 余弦相似度
FROM bi_kpi_definition
WHERE is_enabled = TRUE
  AND (datasource_id IS NULL OR datasource_id = @datasource_id)
ORDER BY embedding <=> @query_embedding  -- 距离越小越相似
LIMIT 5;

-- 混合检索：向量相似度 + 关键词匹配
SELECT
    id, name, alias, description, formula, sql_template,
    1 - (embedding <=> @query_embedding) as vector_score,
    ts_rank(to_tsvector('simple', name || ' ' || COALESCE(alias, '')),
            plainto_tsquery('simple', @keyword)) as keyword_score
FROM bi_kpi_definition
WHERE is_enabled = TRUE
ORDER BY
    (1 - (embedding <=> @query_embedding)) * 0.7 +  -- 向量权重70%
    ts_rank(to_tsvector('simple', name || ' ' || COALESCE(alias, '')),
            plainto_tsquery('simple', @keyword)) * 0.3  -- 关键词权重30%
    DESC
LIMIT 5;
```

---

## 5. API接口设计

### 5.1 接口列表

#### AI对话接口

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/v1/ai/chat` | 发送对话消息，返回AI分析结果（含RAG检索） |
| POST | `/api/v1/ai/execute` | 执行AI生成的SQL并返回数据 |
| GET | `/api/v1/ai/sessions` | 获取用户的会话列表 |
| GET | `/api/v1/ai/sessions/{id}` | 获取会话详情（含消息历史） |
| DELETE | `/api/v1/ai/sessions/{id}` | 删除会话 |
| GET | `/api/v1/ai/schema/{datasourceId}` | 获取数据源Schema信息 |
| POST | `/api/v1/ai/favorites` | 收藏查询 |
| GET | `/api/v1/ai/favorites` | 获取收藏列表 |

#### 指标知识库接口

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/v1/kpi/categories` | 获取指标分类树 |
| POST | `/api/v1/kpi/categories` | 创建指标分类 |
| PUT | `/api/v1/kpi/categories/{id}` | 更新指标分类 |
| DELETE | `/api/v1/kpi/categories/{id}` | 删除指标分类 |
| GET | `/api/v1/kpi` | 获取指标定义列表（分页） |
| POST | `/api/v1/kpi` | 创建指标定义 |
| GET | `/api/v1/kpi/{id}` | 获取指标详情 |
| PUT | `/api/v1/kpi/{id}` | 更新指标定义 |
| DELETE | `/api/v1/kpi/{id}` | 删除指标定义 |
| POST | `/api/v1/kpi/search` | 向量检索相关指标 |
| POST | `/api/v1/kpi/{id}/embedding` | 为单个指标生成向量 |
| POST | `/api/v1/kpi/embedding/rebuild` | 批量重建所有指标向量 |
| POST | `/api/v1/kpi/import` | 批量导入指标定义（Excel/JSON） |
| GET | `/api/v1/kpi/export` | 导出指标定义 |

### 5.2 核心接口定义

#### 对话接口（RAG增强版）

```
POST /api/v1/ai/chat

Request:
{
  "sessionId": 123,              // 可选，不传则创建新会话
  "datasourceId": 1,             // 数据源ID
  "message": "本月各科室门诊量是多少？",
  "enableRag": true              // 是否启用RAG检索，默认true
}

Response:
{
  "code": 0,
  "message": "success",
  "data": {
    "sessionId": 123,
    "messageId": 456,
    "intent": "查询本月各科室门诊量",
    "referencedKpis": [
      {
        "id": 1,
        "name": "门诊人次",
        "formula": "COUNT(DISTINCT visit_id)",
        "similarity": 0.92
      }
    ],
    "sql": "SELECT department_name as 科室, COUNT(DISTINCT visit_id) as 门诊人次 FROM outpatient_records WHERE visit_date >= '2024-12-01' GROUP BY department_name",
    "chartType": "bar",
    "config": { ... },
    "explanation": "该查询统计了本月各科室的门诊人次（口径：按挂号就诊去重统计）",
    "tokenUsage": {
      "prompt": 800,
      "completion": 250,
      "total": 1050
    }
  }
}
```

#### 指标向量检索接口

```
POST /api/v1/kpi/search

Request:
{
  "query": "门诊收入",           // 搜索关键词/问题
  "datasourceId": 1,             // 可选，限定数据源
  "categoryId": null,            // 可选，限定分类
  "topK": 5                      // 返回数量
}

Response:
{
  "code": 0,
  "data": {
    "items": [
      {
        "id": 12,
        "code": "KPI_010",
        "name": "门诊收入",
        "alias": "门诊医疗收入,门诊总收入",
        "description": "门诊产生的所有医疗费用合计...",
        "formula": "SUM(total_fee)",
        "sqlTemplate": "SELECT SUM(total_fee) as 门诊收入 FROM ...",
        "similarity": 0.95
      },
      {
        "id": 13,
        "code": "KPI_011",
        "name": "门诊药品收入",
        "similarity": 0.78
      }
    ]
  }
}
```

#### 创建指标定义接口

```
POST /api/v1/kpi

Request:
{
  "categoryId": 2,
  "code": "KPI_025",
  "name": "药占比",
  "alias": "药品费用占比,药品收入占比",
  "description": "药品费用占医疗总收入的比例，是医院控费的重要指标",
  "formula": "SUM(药品费用) / SUM(医疗总收入) * 100",
  "unit": "%",
  "sqlTemplate": "SELECT ROUND(SUM(drug_fee)::numeric / NULLIF(SUM(total_fee), 0) * 100, 2) as 药占比 FROM billing WHERE ...",
  "relatedTables": "billing,drug_detail",
  "relatedFields": "drug_fee,total_fee",
  "dimensionHint": "时间,科室,医生",
  "dataType": "percent",
  "precisionNum": 2,
  "datasourceId": null           // null表示通用指标
}

Response:
{
  "code": 0,
  "data": {
    "id": 25,
    "embeddingStatus": "pending"  // 向量生成状态
  }
}
```

#### 执行接口

```
POST /api/v1/ai/execute

Request:
{
  "datasourceId": 1,
  "sql": "SELECT department_name as 科室, COUNT(DISTINCT visit_id) as 门诊人次 ...",
  "limit": 1000
}

Response:
{
  "code": 0,
  "data": {
    "columns": [
      {"name": "科室", "dataType": "String"},
      {"name": "门诊人次", "dataType": "Int64"}
    ],
    "rows": [
      {"科室": "内科", "门诊人次": 1234},
      {"科室": "外科", "门诊人次": 890}
    ],
    "totalRows": 15
  }
}
```

---

## 6. 前端设计

### 6.1 智能分析页面布局

```
┌────────────────────────────────────────────────────────────────────┐
│  智能分析                                              [新建会话]  │
├──────────────┬─────────────────────────────────────────────────────┤
│              │                                                     │
│  会话历史    │            图表/数据展示区                           │
│  ┌────────┐  │     ┌─────────────────────────────────────────┐    │
│  │ 会话1  │  │     │                                         │    │
│  ├────────┤  │     │            ECharts 图表                 │    │
│  │ 会话2  │  │     │                                         │    │
│  ├────────┤  │     └─────────────────────────────────────────┘    │
│  │ 会话3  │  │                                                     │
│  └────────┘  │     ┌─────────────────────────────────────────┐    │
│              │     │  📊 引用指标：门诊人次（相似度92%）      │    │
│              │     │  📝 口径：按挂号就诊去重统计             │    │
│              │     └─────────────────────────────────────────┘    │
│              │                                                     │
│              │     ┌─────────────────────────────────────────┐    │
│              │     │  SQL预览（可编辑）                       │    │
│              │     └─────────────────────────────────────────┘    │
│              ├─────────────────────────────────────────────────────┤
│              │            对话消息区                               │
│              │  ┌─────────────────────────────────────────────┐   │
│              │  │ 👤 本月各科室门诊量是多少？                 │   │
│              │  ├─────────────────────────────────────────────┤   │
│              │  │ 🤖 根据您的问题，我查询了本月各科室的门诊   │   │
│              │  │    人次。使用指标【门诊人次】的标准口径...  │   │
│              │  └─────────────────────────────────────────────┘   │
│              ├─────────────────────────────────────────────────────┤
│              │  ┌─────────────────────────────────────────────┐   │
│              │  │ 请输入您的问题...                   [发送]  │   │
│              │  └─────────────────────────────────────────────┘   │
└──────────────┴─────────────────────────────────────────────────────┘
```

### 6.2 指标知识库管理页面

```
┌────────────────────────────────────────────────────────────────────┐
│  指标知识库                    [导入] [导出] [重建向量] [新建指标]  │
├──────────────┬─────────────────────────────────────────────────────┤
│              │                                                     │
│  指标分类    │  🔍 搜索指标...                                     │
│  ┌────────┐  │  ─────────────────────────────────────────────────  │
│  │▼医疗服务│  │                                                     │
│  │  门诊   │  │  ┌─────────────────────────────────────────────┐   │
│  │  住院   │  │  │ KPI_001 门诊人次                            │   │
│  │  手术   │  │  │ 别名：门诊量,门诊就诊人次                   │   │
│  ├────────┤  │  │ 公式：COUNT(DISTINCT visit_id)              │   │
│  │▼财务收入│  │  │ 说明：统计门诊挂号就诊的人次数...           │   │
│  │  门诊   │  │  │ 向量状态：✅ 已生成                         │   │
│  │  住院   │  │  │                        [编辑] [删除] [测试]  │   │
│  ├────────┤  │  └─────────────────────────────────────────────┘   │
│  │▶运营效率│  │                                                     │
│  ├────────┤  │  ┌─────────────────────────────────────────────┐   │
│  │▶质量安全│  │  │ KPI_002 出院人数                            │   │
│  └────────┘  │  │ ...                                          │   │
│              │  └─────────────────────────────────────────────┘   │
│  [新建分类]  │                                                     │
│              │                              [上一页] 1/5 [下一页]  │
└──────────────┴─────────────────────────────────────────────────────┘
```

### 6.3 指标编辑弹窗

```
┌─────────────────────────────────────────────────────────────┐
│  编辑指标定义                                          [×]  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  基本信息                                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ 指标编码：[KPI_001        ]  分类：[医疗服务/门诊▼] │   │
│  │ 指标名称：[门诊人次       ]  单位：[人次          ] │   │
│  │ 别名：    [门诊量,门诊就诊人次                    ] │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  计算口径                                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ 说明：                                               │   │
│  │ [统计门诊挂号就诊的人次数。同一患者在同一天挂多个  ]│   │
│  │ [号算多次，不同天就诊也算多次。                    ]│   │
│  │                                                      │   │
│  │ 计算公式：[COUNT(DISTINCT visit_id)                ]│   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  SQL模板                                                    │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ SELECT COUNT(DISTINCT visit_id) as 门诊人次         │   │
│  │ FROM outpatient_records                              │   │
│  │ WHERE visit_date BETWEEN @start_date AND @end_date   │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  关联信息                                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ 相关表：  [outpatient_records                      ]│   │
│  │ 相关字段：[visit_id,visit_date                     ]│   │
│  │ 常用维度：[时间,科室,医生                          ]│   │
│  │ 数据类型：[数值▼]  精度：[0]位小数                  │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│                              [取消] [保存] [保存并生成向量] │
└─────────────────────────────────────────────────────────────┘
```

### 6.4 交互流程

#### 智能分析流程
1. 用户选择数据源，输入自然语言问题
2. 后端进行RAG检索，找到相关指标定义
3. 后端调用AI生成SQL和图表配置（注入指标口径）
4. 前端显示AI解释说明和引用的指标
5. 用户确认后执行SQL，展示图表
6. 用户可修改SQL重新执行
7. 用户可将结果保存为正式图表/收藏

#### 指标管理流程
1. 管理员维护指标分类和定义
2. 创建/编辑指标时填写完整的口径说明和SQL模板
3. 保存后自动生成向量（或手动触发）
4. 可批量导入/导出指标定义
5. 定期重建向量索引以优化检索效果

---

## 7. 安全设计

### 7.1 SQL安全验证

```csharp
public class SqlSecurityValidator
{
    private static readonly string[] ForbiddenKeywords =
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "TRUNCATE",
        "ALTER", "CREATE", "EXEC", "EXECUTE", "GRANT", "REVOKE"
    };

    public ValidationResult Validate(string sql)
    {
        // 1. 检查禁止关键字
        foreach (var keyword in ForbiddenKeywords)
        {
            if (Regex.IsMatch(sql, $@"\b{keyword}\b", RegexOptions.IgnoreCase))
            {
                return ValidationResult.Fail($"禁止使用 {keyword} 语句");
            }
        }

        // 2. 必须以SELECT开头
        if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Fail("仅支持SELECT查询");
        }

        // 3. 检查注释和分号（防止SQL注入）
        if (sql.Contains("--") || sql.Contains("/*") || sql.Count(c => c == ';') > 1)
        {
            return ValidationResult.Fail("SQL格式不合法");
        }

        return ValidationResult.Success();
    }
}
```

### 7.2 权限控制

- 用户只能查询有权限的数据源
- AI生成的SQL受数据源连接账号权限限制
- 敏感字段可在Schema服务中过滤

---

## 8. 图表类型推荐规则

### 8.1 规则引擎（辅助AI决策）

| 数据特征 | 推荐图表 | 说明 |
|----------|----------|------|
| 时间维度 + 单数值 | line | 趋势分析 |
| 时间维度 + 多数值 | line(多线) | 多指标趋势对比 |
| 分类维度 + 单数值 | bar | 对比分析 |
| 分类维度 + 单数值（<8项） | pie | 占比分析 |
| 两个数值维度 | scatter | 相关性分析 |
| 单个汇总数值 | kpi | 指标卡片 |
| 多维明细数据 | table | 明细展示 |

### 8.2 推荐服务实现

```csharp
public class ChartRecommender
{
    public string Recommend(List<ColumnInfo> columns, string sql)
    {
        var dims = columns.Where(c => IsStringOrDate(c.DataType)).ToList();
        var measures = columns.Where(c => IsNumeric(c.DataType)).ToList();

        // 时间维度 + 数值 → 折线图
        if (dims.Any(d => IsDateType(d.DataType)) && measures.Count >= 1)
            return "line";

        // 单个数值（汇总） → KPI卡片
        if (dims.Count == 0 && measures.Count == 1)
            return "kpi";

        // 分类维度 + 单数值，数量少 → 饼图
        if (dims.Count == 1 && measures.Count == 1)
        {
            // 通过SQL判断分组数量（可选）
            return "bar"; // 默认柱状图，前端可切换饼图
        }

        // 多维度多度量 → 表格
        if (dims.Count > 2 || measures.Count > 3)
            return "table";

        return "bar"; // 默认柱状图
    }
}
```

---

## 9. 成本与性能优化

### 9.1 Token成本控制

| 策略 | 说明 |
|------|------|
| Schema裁剪 | 只发送相关表的结构，不发全库 |
| 低温度生成 | SQL生成使用 temperature=0.1 |
| 缓存Schema | Schema信息缓存10分钟 |
| 限制对话历史 | 多轮对话只保留最近5轮 |

### 9.2 响应优化

| 策略 | 说明 |
|------|------|
| 流式输出 | 支持SSE流式返回AI响应 |
| SQL预检 | EXPLAIN检查SQL性能 |
| 结果缓存 | 相同SQL结果缓存5分钟 |
| 并发限制 | 单用户并发AI请求限制 |

### 9.3 DeepSeek API成本参考

| 模型 | 输入价格 | 输出价格 |
|------|----------|----------|
| DeepSeek-V3 | ¥1/M tokens | ¥2/M tokens |
| DeepSeek-R1（推理） | ¥4/M tokens | ¥16/M tokens |

> 典型单次查询消耗约500-1000 tokens，成本约 ¥0.001-0.003

### 9.4 Embedding成本参考

| 服务 | 价格 | 说明 |
|------|------|------|
| OpenAI text-embedding-3-small | $0.02/M tokens | 1536维，推荐 |
| OpenAI text-embedding-3-large | $0.13/M tokens | 3072维，更精确 |
| BGE-M3（本地部署） | 免费 | 1024维，需GPU |

> 指标定义向量化一次性成本很低，100条指标约消耗5000 tokens，成本约 $0.0001

---

## 10. 实施计划

### 10.1 Phase 1 - MVP（预估2周）

| 任务 | 说明 | 预估工时 |
|------|------|----------|
| 5-1 | Schema服务实现 | 2天 |
| 5-2 | LLM服务封装（DeepSeek） | 2天 |
| 5-3 | AI对话API开发 | 3天 |
| 5-4 | 前端SmartBI页面 | 3天 |
| 5-5 | 集成测试与调优 | 2天 |

**验收标准**：
- [ ] 能选择数据源，输入自然语言问题
- [ ] AI能正确生成SQL并推荐图表类型
- [ ] 能执行SQL并展示图表
- [ ] SQL安全验证正常工作

### 10.2 Phase 2 - 指标知识库（预估2周）

| 任务 | 说明 | 预估工时 |
|------|------|----------|
| 5-6 | pgvector扩展安装与配置 | 0.5天 |
| 5-7 | 指标分类/定义表创建 | 0.5天 |
| 5-8 | Embedding服务封装（OpenAI） | 1天 |
| 5-9 | 指标检索服务实现 | 2天 |
| 5-10 | 指标管理API开发 | 2天 |
| 5-11 | 前端指标管理页面 | 3天 |
| 5-12 | RAG增强Prompt集成 | 1天 |
| 5-13 | 测试与调优 | 2天 |

**验收标准**：
- [ ] 能创建/编辑/删除指标定义
- [ ] 指标保存后自动生成向量
- [ ] 用户提问时能检索到相关指标
- [ ] AI生成的SQL使用正确的指标口径
- [ ] 前端显示引用的指标信息

### 10.3 Phase 3 - 对话增强（预估2周）

| 任务 | 说明 | 预估工时 |
|------|------|----------|
| 5-14 | 对话会话管理 | 2天 |
| 5-15 | 多轮对话上下文 | 2天 |
| 5-16 | 查询收藏功能 | 1天 |
| 5-17 | 将AI结果保存为正式图表 | 2天 |
| 5-18 | Few-shot示例库 | 2天 |
| 5-19 | 错误处理与自纠正 | 2天 |

**验收标准**：
- [ ] 支持多轮对话追问
- [ ] 能保存对话历史
- [ ] 能将查询结果保存为正式图表
- [ ] AI生成错误时能自动重试

### 10.4 Phase 4 - 优化（持续迭代）

| 任务 | 说明 |
|------|------|
| 5-20 | 混合检索优化（向量+关键词） |
| 5-21 | 流式输出支持 |
| 5-22 | 本地Embedding模型支持（可选） |
| 5-23 | 智能推荐优化 |
| 5-24 | 使用统计与分析 |
| 5-25 | 指标批量导入/导出 |

---

## 11. 风险与应对

| 风险 | 影响 | 应对措施 |
|------|------|----------|
| AI生成SQL不准确 | 查询结果错误 | RAG注入指标口径 + Few-shot示例 + 自纠正机制 |
| 指标检索不准确 | 使用错误口径 | 优化向量索引 + 混合检索 + 别名扩展 |
| API调用超时 | 用户体验差 | 超时重试 + 友好提示 |
| Token成本过高 | 运营成本 | Schema裁剪 + 缓存 + 指标检索减少上下文 |
| SQL注入风险 | 安全问题 | 严格的安全验证 |
| 复杂查询生成失败 | 功能受限 | 提供SQL编辑器让用户修改 |
| pgvector性能问题 | 检索慢 | 使用HNSW索引 + 限制向量数量 |

---

## 12. 与现有系统集成

### 12.1 复用现有组件

| 现有组件 | 复用方式 |
|----------|----------|
| `DatabaseConnectionService` | 执行AI生成的SQL |
| `ChartQueryService` | 解析图表配置 |
| `BiDbContext` | 存储会话历史和指标定义 |
| vue-echarts 组件 | 渲染图表 |
| `ChartConfig` 类型定义 | DSL格式兼容 |

### 12.2 新增组件

| 新组件 | 说明 |
|--------|------|
| `IEmbeddingService` | 向量生成服务接口 |
| `OpenAIEmbeddingService` | OpenAI Embedding实现 |
| `IKpiRetrieverService` | 指标向量检索服务 |
| `KpiDefinition` | 指标定义实体 |
| `KpiCategory` | 指标分类实体 |

### 12.3 菜单集成

在系统菜单中添加入口：

```json
[
  {
    "name": "智能分析",
    "icon": "RobotOutlined",
    "path": "/smart-bi",
    "component": "SmartBI"
  },
  {
    "name": "指标知识库",
    "icon": "DatabaseOutlined",
    "path": "/kpi-management",
    "component": "KpiManagement",
    "permission": "admin"
  }
]
```

---

## 13. 附录：指标知识库最佳实践

### 13.1 指标定义规范

1. **名称规范**：使用业务通用名称，如"门诊人次"而非"op_visit_count"
2. **别名完整**：添加常见的同义词和简称，提高检索召回率
3. **口径清晰**：详细说明计算逻辑、包含/排除条件、时间范围等
4. **SQL模板**：使用参数占位符（@start_date, @end_date），便于动态替换
5. **维度提示**：列出常用的分析维度，帮助AI理解可能的分组方式

### 13.2 向量检索优化

1. **定期重建索引**：指标数量变化较大时重建向量索引
2. **混合检索**：结合向量相似度和关键词匹配，提高准确率
3. **相似度阈值**：设置最低相似度阈值（如0.7），过滤不相关结果
4. **Top-K调优**：根据实际效果调整返回数量，通常3-5个效果最佳

### 13.3 指标维护流程

```
新业务需求 → 定义指标口径 → 编写SQL模板 → 录入系统 → 生成向量 → 测试验证
                ↑                                              ↓
                └──────────── 口径调整 ←─────────────── 发现问题
```

---

> 文档版本：v2.0（RAG增强版）
> 创建日期：2025-12-26
> 更新日期：2025-12-26
> 作者：AI Agent
>
> 更新记录：
> - v2.0: 新增指标知识库设计、RAG检索增强、pgvector向量存储

