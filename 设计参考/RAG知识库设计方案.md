# RAG知识库设计方案

## 一、项目背景

在智能BI分析中，AI需要理解业务指标的口径定义才能生成准确的SQL。目前通过手工在指标知识库录入，效率较低。本方案设计一个本地RAG（检索增强生成）知识库系统，支持：

1. 自动解析PDF/Word/Excel等文档
2. 智能分块并向量化存储
3. 在AI分析时检索相关知识，提供给LLM参考

## 二、技术选型

| 环节 | 技术方案 | 说明 |
|------|---------|------|
| **向量模型** | Ollama + BGE-M3 | 本地部署，1024维，中文效果最佳 |
| **文档解析** | PdfPig + OpenXml + NPOI | .NET原生库，无外部依赖 |
| **分块策略** | 递归分块 + 50字重叠 | 500字/块，保留语义完整性 |
| **向量存储** | PostgreSQL + pgvector | HNSW索引，高性能检索 |
| **检索方式** | 统一检索 | 指标库 + 文档库合并检索 |

## 三、系统架构

```
┌─────────────────────────────────────────────────────────────────────┐
│                      RAG知识库系统架构                               │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  前端 (Vue3)                                                        │
│  ├── 文档上传管理                                                    │
│  ├── 知识库浏览                                                      │
│  └── 智能分析（RAG增强）                                             │
│                           ↓                                         │
│  后端 (.NET 9)                                                      │
│  ├── DocumentParserService   # 文档解析                             │
│  ├── ChunkingService         # 文本分块                             │
│  ├── OllamaEmbeddingService  # 向量生成                             │
│  ├── KnowledgeService        # 知识库管理                           │
│  └── UnifiedRetrieverService # 统一RAG检索                          │
│                           ↓                                         │
│  ┌─────────────────┐    ┌─────────────────────────────────────────┐ │
│  │ Ollama (本地)   │    │ PostgreSQL + pgvector                   │ │
│  │ └── BGE-M3模型  │    │ ├── knowledge_documents (文档表)        │ │
│  └─────────────────┘    │ ├── knowledge_chunks (分块+向量)        │ │
│                         │ └── kpi_definitions (指标+向量)         │ │
│                         └─────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

## 四、数据库设计

### 4.1 启用pgvector扩展

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

### 4.2 知识库分类表

```sql
CREATE TABLE knowledge_categories (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,              -- 分类名称
    parent_id BIGINT REFERENCES knowledge_categories(id),
    sort_order INT DEFAULT 0,
    description TEXT,
    created_at TIMESTAMP DEFAULT NOW()
);
COMMENT ON TABLE knowledge_categories IS '知识库分类';
```

### 4.3 知识文档表

```sql
CREATE TABLE knowledge_documents (
    id BIGSERIAL PRIMARY KEY,
    category_id BIGINT REFERENCES knowledge_categories(id),
    title VARCHAR(500) NOT NULL,             -- 文档标题
    file_name VARCHAR(255),                  -- 原始文件名
    file_type VARCHAR(50),                   -- pdf/docx/xlsx/txt/md
    file_size BIGINT,                        -- 文件大小(bytes)
    file_path VARCHAR(500),                  -- 存储路径
    content_hash VARCHAR(64),                -- 内容hash（去重）
    status VARCHAR(50) DEFAULT 'pending',    -- pending/processing/completed/failed
    error_message TEXT,
    chunk_count INT DEFAULT 0,
    datasource_id BIGINT,
    metadata JSONB,
    created_by BIGINT,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);
COMMENT ON TABLE knowledge_documents IS '知识库文档';
```

### 4.4 知识分块表（核心）

```sql
CREATE TABLE knowledge_chunks (
    id BIGSERIAL PRIMARY KEY,
    document_id BIGINT NOT NULL REFERENCES knowledge_documents(id) ON DELETE CASCADE,
    chunk_index INT NOT NULL,                -- 块序号
    content TEXT NOT NULL,                   -- 块内容
    content_length INT,
    embedding vector(1024),                  -- BGE-M3向量
    page_number INT,                         -- 页码
    section_title VARCHAR(500),              -- 章节标题
    metadata JSONB,
    created_at TIMESTAMP DEFAULT NOW()
);


## 五、核心服务接口设计

### 5.1 Ollama Embedding服务

```csharp
public interface IOllamaEmbeddingService : IEmbeddingService
{
    // 继承自IEmbeddingService
    // Task<float[]> GetEmbeddingAsync(string text);
    // Task<List<float[]>> GetEmbeddingsAsync(List<string> texts);
    // int Dimensions { get; }  // 1024
}
```

### 5.2 文档解析服务

```csharp
public interface IDocumentParserService
{
    Task<DocumentParseResult> ParseAsync(string filePath, string fileType);
}

public class DocumentParseResult
{
    public bool Success { get; set; }
    public string Content { get; set; }           // 全文
    public List<PageContent> Pages { get; set; }  // 按页（PDF）
    public Dictionary<string, string> Metadata { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### 5.3 文本分块服务

```csharp
public interface IChunkingService
{
    List<TextChunk> ChunkText(string text, ChunkingOptions options);
}

public class ChunkingOptions
{
    public int MaxChunkSize { get; set; } = 500;      // 最大块大小
    public int ChunkOverlap { get; set; } = 50;       // 重叠大小
    public string[] Separators { get; set; } = { "\n\n", "\n", "。", "；", " " };
}
```

### 5.4 知识库管理服务

```csharp
public interface IKnowledgeService
{
    // 文档管理
    Task<long> UploadDocumentAsync(IFormFile file, long? categoryId);
    Task<bool> DeleteDocumentAsync(long documentId);
    Task<List<KnowledgeDocumentDto>> GetDocumentsAsync(long? categoryId, string? status);

    // 处理文档
    Task ProcessDocumentAsync(long documentId);

    // 分类管理
    Task<List<KnowledgeCategoryDto>> GetCategoriesAsync();
    Task<long> CreateCategoryAsync(string name, long? parentId);
}
```

### 5.5 统一RAG检索服务

```csharp
public interface IUnifiedRetrieverService
{
    Task<RagContext> RetrieveAsync(string query, RetrieveOptions options);
}

public class RetrieveOptions
{
    public int TopK { get; set; } = 5;
    public double MinScore { get; set; } = 0.6;
    public bool IncludeKpi { get; set; } = true;
    public bool IncludeDocument { get; set; } = true;
    public long? DatasourceId { get; set; }
}

public class RagContext
{
    public List<KpiMatch> KpiMatches { get; set; }
    public List<ChunkMatch> DocumentMatches { get; set; }
    public string BuildContextText();  // 构建给LLM的上下文
}
```

## 六、向量检索SQL

```sql
-- 检索文档块（使用pgvector）
SELECT
    c.id, c.content, c.section_title,
    d.title as document_title,
    1 - (c.embedding <=> $1::vector) as score
FROM knowledge_chunks c
JOIN knowledge_documents d ON c.document_id = d.id
WHERE d.status = 'completed'
  AND 1 - (c.embedding <=> $1::vector) >= $2
ORDER BY c.embedding <=> $1::vector
LIMIT $3;

-- 检索指标
SELECT
    id, name, definition, formula, sql_template,
    1 - (embedding <=> $1::vector) as score
FROM kpi_definitions
WHERE is_enabled = true AND embedding IS NOT NULL
  AND 1 - (embedding <=> $1::vector) >= $2
ORDER BY embedding <=> $1::vector
LIMIT $3;
```

## 七、Ollama部署

```powershell
# 1. 安装Ollama（Windows）
# 下载：https://ollama.com/download

# 2. 启动服务
ollama serve

# 3. 拉取BGE-M3模型
ollama pull bge-m3

# 4. 测试
curl http://localhost:11434/api/embeddings -d '{"model":"bge-m3","prompt":"测试文本"}'
```

## 八、实施计划

### 阶段一：基础设施（1-2天）
- 部署Ollama + BGE-M3模型
- PostgreSQL启用pgvector扩展
- 创建数据库表结构
- 实现OllamaEmbeddingService

### 阶段二：文档处理Pipeline（2-3天）
- 实现DocumentParserService（PDF/Word/Excel解析）
- 实现ChunkingService（递归分块）
- 实现KnowledgeService（文档上传、管理）
- 实现后台处理任务

### 阶段三：RAG检索集成（2天）
- 实现UnifiedRetrieverService
- 修改AiController集成RAG上下文
- 优化Prompt模板
- 迁移现有KPI向量

### 阶段四：前端界面（2天）
- 知识库管理页面
- 文档上传功能
- 文档详情预览
- 智能分析显示引用来源

## 九、预期效果

用户提问"统计今年门诊量"时：

1. RAG检索找到相关知识：
   - [指标库] 门诊量定义、公式、SQL模板
   - [文档库] 《门诊统计规范.pdf》相关段落

2. 构建增强Prompt给LLM，生成准确SQL

