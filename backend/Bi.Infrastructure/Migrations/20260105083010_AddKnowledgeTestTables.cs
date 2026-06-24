using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeTestTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bi_knowledge_test_case",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    query = table.Column<string>(type: "text", nullable: false),
                    expected_document_ids = table.Column<string>(type: "jsonb", nullable: true),
                    expected_chunk_ids = table.Column<string>(type: "jsonb", nullable: true),
                    expected_keywords = table.Column<string>(type: "jsonb", nullable: true),
                    category_id = table.Column<long>(type: "bigint", nullable: true),
                    remark = table.Column<string>(type: "text", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_knowledge_test_case", x => x.id);
                },
                comment: "知识库测试用例表");

            migrationBuilder.CreateTable(
                name: "bi_knowledge_test_run",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    total_cases = table.Column<int>(type: "integer", nullable: false),
                    completed_cases = table.Column<int>(type: "integer", nullable: false),
                    top_k = table.Column<int>(type: "integer", nullable: false),
                    min_score = table.Column<float>(type: "real", nullable: false),
                    hit_rate = table.Column<float>(type: "real", nullable: false),
                    mrr = table.Column<float>(type: "real", nullable: false),
                    avg_precision = table.Column<float>(type: "real", nullable: false),
                    avg_recall = table.Column<float>(type: "real", nullable: false),
                    avg_latency_ms = table.Column<float>(type: "real", nullable: false),
                    detail_results = table.Column<string>(type: "jsonb", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_knowledge_test_run", x => x.id);
                },
                comment: "知识库测试运行记录表");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bi_knowledge_test_case");

            migrationBuilder.DropTable(
                name: "bi_knowledge_test_run");
        }
    }
}
