using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace Bi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeBaseTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 启用pgvector扩展（需要数据库支持）
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            migrationBuilder.CreateTable(
                name: "bi_knowledge_category",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_knowledge_category", x => x.id);
                    table.ForeignKey(
                        name: "FK_bi_knowledge_category_bi_knowledge_category_parent_id",
                        column: x => x.parent_id,
                        principalTable: "bi_knowledge_category",
                        principalColumn: "id");
                },
                comment: "知识库分类表");

            migrationBuilder.CreateTable(
                name: "bi_knowledge_document",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category_id = table.Column<long>(type: "bigint", nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    file_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    file_size = table.Column<long>(type: "bigint", nullable: true),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    chunk_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    datasource_id = table.Column<long>(type: "bigint", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_knowledge_document", x => x.id);
                    table.ForeignKey(
                        name: "FK_bi_knowledge_document_bi_knowledge_category_category_id",
                        column: x => x.category_id,
                        principalTable: "bi_knowledge_category",
                        principalColumn: "id");
                },
                comment: "知识库文档表");

            migrationBuilder.CreateTable(
                name: "bi_knowledge_chunk",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_id = table.Column<long>(type: "bigint", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    content_length = table.Column<int>(type: "integer", nullable: true),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: true),
                    page_number = table.Column<int>(type: "integer", nullable: true),
                    section_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_knowledge_chunk", x => x.id);
                    table.ForeignKey(
                        name: "FK_bi_knowledge_chunk_bi_knowledge_document_document_id",
                        column: x => x.document_id,
                        principalTable: "bi_knowledge_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "知识库分块表（含向量嵌入）");

            migrationBuilder.CreateIndex(
                name: "IX_bi_knowledge_category_parent_id",
                table: "bi_knowledge_category",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_knowledge_chunk_document_id",
                table: "bi_knowledge_chunk",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_knowledge_document_category_id",
                table: "bi_knowledge_document",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_knowledge_document_status",
                table: "bi_knowledge_document",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bi_knowledge_chunk");

            migrationBuilder.DropTable(
                name: "bi_knowledge_document");

            migrationBuilder.DropTable(
                name: "bi_knowledge_category");
        }
    }
}
