using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAndKpiTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bi_ai_favorite",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    question = table.Column<string>(type: "text", nullable: false),
                    sql = table.Column<string>(type: "text", nullable: true),
                    chart_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    chart_config = table.Column<string>(type: "text", nullable: true),
                    datasource_id = table.Column<long>(type: "bigint", nullable: true),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_ai_favorite", x => x.id);
                    table.ForeignKey(
                        name: "FK_bi_ai_favorite_bi_datasource_datasource_id",
                        column: x => x.datasource_id,
                        principalTable: "bi_datasource",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_bi_ai_favorite_sys_user_user_id",
                        column: x => x.user_id,
                        principalTable: "sys_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "AI查询收藏表");

            migrationBuilder.CreateTable(
                name: "bi_ai_session",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    datasource_id = table.Column<long>(type: "bigint", nullable: true),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_active_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_ai_session", x => x.id);
                    table.ForeignKey(
                        name: "FK_bi_ai_session_bi_datasource_datasource_id",
                        column: x => x.datasource_id,
                        principalTable: "bi_datasource",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_bi_ai_session_sys_user_user_id",
                        column: x => x.user_id,
                        principalTable: "sys_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "AI会话表");

            migrationBuilder.CreateTable(
                name: "bi_kpi_category",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_kpi_category", x => x.id);
                    table.ForeignKey(
                        name: "FK_bi_kpi_category_bi_kpi_category_parent_id",
                        column: x => x.parent_id,
                        principalTable: "bi_kpi_category",
                        principalColumn: "id");
                },
                comment: "指标分类表");

            migrationBuilder.CreateTable(
                name: "bi_ai_message",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<long>(type: "bigint", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    sql = table.Column<string>(type: "text", nullable: true),
                    chart_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    chart_config = table.Column<string>(type: "text", nullable: true),
                    tokens_used = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_ai_message", x => x.id);
                    table.ForeignKey(
                        name: "FK_bi_ai_message_bi_ai_session_session_id",
                        column: x => x.session_id,
                        principalTable: "bi_ai_session",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "AI消息表");

            migrationBuilder.CreateTable(
                name: "bi_kpi_definition",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category_id = table.Column<long>(type: "bigint", nullable: false),
                    definition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    formula = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sql_template = table.Column<string>(type: "text", nullable: true),
                    datasource_id = table.Column<long>(type: "bigint", nullable: true),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    data_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "number"),
                    embedding_json = table.Column<string>(type: "text", nullable: true),
                    embedding_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_kpi_definition", x => x.id);
                    table.ForeignKey(
                        name: "FK_bi_kpi_definition_bi_datasource_datasource_id",
                        column: x => x.datasource_id,
                        principalTable: "bi_datasource",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_bi_kpi_definition_bi_kpi_category_category_id",
                        column: x => x.category_id,
                        principalTable: "bi_kpi_category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "指标定义表");

            migrationBuilder.CreateIndex(
                name: "IX_bi_ai_favorite_datasource_id",
                table: "bi_ai_favorite",
                column: "datasource_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_ai_favorite_user_id",
                table: "bi_ai_favorite",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_ai_message_session_id",
                table: "bi_ai_message",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_ai_session_datasource_id",
                table: "bi_ai_session",
                column: "datasource_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_ai_session_session_key",
                table: "bi_ai_session",
                column: "session_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bi_ai_session_user_id",
                table: "bi_ai_session",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_kpi_category_parent_id",
                table: "bi_kpi_category",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_kpi_definition_category_id",
                table: "bi_kpi_definition",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_kpi_definition_code",
                table: "bi_kpi_definition",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bi_kpi_definition_datasource_id",
                table: "bi_kpi_definition",
                column: "datasource_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bi_ai_favorite");

            migrationBuilder.DropTable(
                name: "bi_ai_message");

            migrationBuilder.DropTable(
                name: "bi_kpi_definition");

            migrationBuilder.DropTable(
                name: "bi_ai_session");

            migrationBuilder.DropTable(
                name: "bi_kpi_category");
        }
    }
}
