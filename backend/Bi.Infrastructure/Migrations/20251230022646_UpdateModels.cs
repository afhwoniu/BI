using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bi_slow_query_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    datasource_id = table.Column<long>(type: "bigint", nullable: false),
                    chart_id = table.Column<long>(type: "bigint", nullable: true),
                    sql_text = table.Column<string>(type: "text", nullable: false),
                    execution_time_ms = table.Column<long>(type: "bigint", nullable: false),
                    threshold_ms = table.Column<long>(type: "bigint", nullable: false),
                    executed_by = table.Column<string>(type: "text", nullable: true),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    explain_result = table.Column<string>(type: "text", nullable: true),
                    suggestion = table.Column<string>(type: "text", nullable: true),
                    is_resolved = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_slow_query_log", x => x.id);
                    table.ForeignKey(
                        name: "FK_bi_slow_query_log_bi_chart_chart_id",
                        column: x => x.chart_id,
                        principalTable: "bi_chart",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_bi_slow_query_log_bi_datasource_datasource_id",
                        column: x => x.datasource_id,
                        principalTable: "bi_datasource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "慢查询日志表");

            migrationBuilder.CreateIndex(
                name: "IX_bi_slow_query_log_chart_id",
                table: "bi_slow_query_log",
                column: "chart_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_slow_query_log_datasource_id",
                table: "bi_slow_query_log",
                column: "datasource_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bi_slow_query_log");
        }
    }
}
