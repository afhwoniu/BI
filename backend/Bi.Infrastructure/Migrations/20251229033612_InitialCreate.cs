using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bi_datasource",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    conn_string = table.Column<string>(type: "text", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_datasource", x => x.id);
                },
                comment: "数据源配置表");

            migrationBuilder.CreateTable(
                name: "bi_panel",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    panel_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "pc_dashboard"),
                    config_json = table.Column<string>(type: "jsonb", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_panel", x => x.id);
                },
                comment: "分析面板表");

            migrationBuilder.CreateTable(
                name: "sys_user",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    real_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    avatar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_user", x => x.id);
                },
                comment: "系统用户表");

            migrationBuilder.CreateTable(
                name: "bi_dataset",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    datasource_id = table.Column<long>(type: "bigint", nullable: false),
                    sql_text = table.Column<string>(type: "text", nullable: false),
                    param_schema = table.Column<string>(type: "jsonb", nullable: true),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_dataset", x => x.id);
                    table.ForeignKey(
                        name: "FK_bi_dataset_bi_datasource_datasource_id",
                        column: x => x.datasource_id,
                        principalTable: "bi_datasource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "SQL数据集定义表");

            migrationBuilder.CreateTable(
                name: "bi_chart",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    dataset_id = table.Column<long>(type: "bigint", nullable: false),
                    chart_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    config_json = table.Column<string>(type: "jsonb", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_chart", x => x.id);
                    table.ForeignKey(
                        name: "FK_bi_chart_bi_dataset_dataset_id",
                        column: x => x.dataset_id,
                        principalTable: "bi_dataset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "图表配置表");

            migrationBuilder.CreateTable(
                name: "bi_dataset_field",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    dataset_id = table.Column<long>(type: "bigint", nullable: false),
                    field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    field_alias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    data_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    agg_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "none"),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_dataset_field", x => x.id);
                    table.ForeignKey(
                        name: "FK_bi_dataset_field_bi_dataset_dataset_id",
                        column: x => x.dataset_id,
                        principalTable: "bi_dataset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "数据集字段元数据表");

            migrationBuilder.CreateTable(
                name: "bi_panel_item",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    panel_id = table.Column<long>(type: "bigint", nullable: false),
                    chart_id = table.Column<long>(type: "bigint", nullable: true),
                    layout_json = table.Column<string>(type: "jsonb", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_panel_item", x => x.id);
                    table.ForeignKey(
                        name: "FK_bi_panel_item_bi_chart_chart_id",
                        column: x => x.chart_id,
                        principalTable: "bi_chart",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_bi_panel_item_bi_panel_panel_id",
                        column: x => x.panel_id,
                        principalTable: "bi_panel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "面板子项表");

            migrationBuilder.CreateIndex(
                name: "IX_bi_chart_dataset_id",
                table: "bi_chart",
                column: "dataset_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_dataset_datasource_id",
                table: "bi_dataset",
                column: "datasource_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_dataset_field_dataset_id",
                table: "bi_dataset_field",
                column: "dataset_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_panel_item_chart_id",
                table: "bi_panel_item",
                column: "chart_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_panel_item_panel_id",
                table: "bi_panel_item",
                column: "panel_id");

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_username",
                table: "sys_user",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bi_dataset_field");

            migrationBuilder.DropTable(
                name: "bi_panel_item");

            migrationBuilder.DropTable(
                name: "sys_user");

            migrationBuilder.DropTable(
                name: "bi_chart");

            migrationBuilder.DropTable(
                name: "bi_panel");

            migrationBuilder.DropTable(
                name: "bi_dataset");

            migrationBuilder.DropTable(
                name: "bi_datasource");
        }
    }
}
