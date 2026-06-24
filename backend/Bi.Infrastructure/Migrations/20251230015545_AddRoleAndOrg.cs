using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleAndOrg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "org_id",
                table: "sys_user",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bi_publish",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    object_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    object_id = table.Column<long>(type: "bigint", nullable: false),
                    access_scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "private"),
                    access_token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    access_password = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    expire_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    view_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_viewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    published_by = table.Column<long>(type: "bigint", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    allowed_roles = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_publish", x => x.id);
                },
                comment: "发布记录表");

            migrationBuilder.CreateTable(
                name: "bi_report",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    report_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "report"),
                    cover_image = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    config_json = table.Column<string>(type: "jsonb", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_report", x => x.id);
                },
                comment: "报表/报告主表");

            migrationBuilder.CreateTable(
                name: "sys_org",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    org_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    org_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    org_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "dept"),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_org", x => x.id);
                },
                comment: "系统组织表");

            migrationBuilder.CreateTable(
                name: "sys_role",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    role_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_role", x => x.id);
                },
                comment: "系统角色表");

            migrationBuilder.CreateTable(
                name: "sys_menu",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    menu_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "folder"),
                    icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    link_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    publish_id = table.Column<long>(type: "bigint", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SysMenuId = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_menu", x => x.id);
                    table.ForeignKey(
                        name: "FK_sys_menu_bi_publish_publish_id",
                        column: x => x.publish_id,
                        principalTable: "bi_publish",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_sys_menu_sys_menu_SysMenuId",
                        column: x => x.SysMenuId,
                        principalTable: "sys_menu",
                        principalColumn: "id");
                },
                comment: "系统菜单表");

            migrationBuilder.CreateTable(
                name: "bi_report_page",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    report_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    config_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_report_page", x => x.id);
                    table.ForeignKey(
                        name: "FK_bi_report_page_bi_report_report_id",
                        column: x => x.report_id,
                        principalTable: "bi_report",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "报表页面表");

            migrationBuilder.CreateTable(
                name: "sys_user_role",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    role_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_user_role", x => x.id);
                    table.ForeignKey(
                        name: "FK_sys_user_role_sys_role_role_id",
                        column: x => x.role_id,
                        principalTable: "sys_role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sys_user_role_sys_user_user_id",
                        column: x => x.user_id,
                        principalTable: "sys_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "用户角色关联表");

            migrationBuilder.CreateTable(
                name: "sys_role_menu",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    menu_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_role_menu", x => x.id);
                    table.ForeignKey(
                        name: "FK_sys_role_menu_sys_menu_menu_id",
                        column: x => x.menu_id,
                        principalTable: "sys_menu",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sys_role_menu_sys_role_role_id",
                        column: x => x.role_id,
                        principalTable: "sys_role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "角色菜单关联表");

            migrationBuilder.CreateTable(
                name: "bi_report_item",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    page_id = table.Column<long>(type: "bigint", nullable: false),
                    item_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    chart_id = table.Column<long>(type: "bigint", nullable: true),
                    panel_id = table.Column<long>(type: "bigint", nullable: true),
                    text_content = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    layout_json = table.Column<string>(type: "jsonb", nullable: false),
                    style_json = table.Column<string>(type: "jsonb", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bi_report_item", x => x.id);
                    table.ForeignKey(
                        name: "FK_bi_report_item_bi_chart_chart_id",
                        column: x => x.chart_id,
                        principalTable: "bi_chart",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_bi_report_item_bi_panel_panel_id",
                        column: x => x.panel_id,
                        principalTable: "bi_panel",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_bi_report_item_bi_report_page_page_id",
                        column: x => x.page_id,
                        principalTable: "bi_report_page",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "报表元素表");

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_org_id",
                table: "sys_user",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_publish_access_token",
                table: "bi_publish",
                column: "access_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bi_report_item_chart_id",
                table: "bi_report_item",
                column: "chart_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_report_item_page_id",
                table: "bi_report_item",
                column: "page_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_report_item_panel_id",
                table: "bi_report_item",
                column: "panel_id");

            migrationBuilder.CreateIndex(
                name: "IX_bi_report_page_report_id",
                table: "bi_report_page",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "IX_sys_menu_publish_id",
                table: "sys_menu",
                column: "publish_id");

            migrationBuilder.CreateIndex(
                name: "IX_sys_menu_SysMenuId",
                table: "sys_menu",
                column: "SysMenuId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_org_org_code",
                table: "sys_org",
                column: "org_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_role_role_code",
                table: "sys_role",
                column: "role_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_role_menu_menu_id",
                table: "sys_role_menu",
                column: "menu_id");

            migrationBuilder.CreateIndex(
                name: "IX_sys_role_menu_role_id_menu_id",
                table: "sys_role_menu",
                columns: new[] { "role_id", "menu_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_role_role_id",
                table: "sys_user_role",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_role_user_id_role_id",
                table: "sys_user_role",
                columns: new[] { "user_id", "role_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_sys_user_sys_org_org_id",
                table: "sys_user",
                column: "org_id",
                principalTable: "sys_org",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sys_user_sys_org_org_id",
                table: "sys_user");

            migrationBuilder.DropTable(
                name: "bi_report_item");

            migrationBuilder.DropTable(
                name: "sys_org");

            migrationBuilder.DropTable(
                name: "sys_role_menu");

            migrationBuilder.DropTable(
                name: "sys_user_role");

            migrationBuilder.DropTable(
                name: "bi_report_page");

            migrationBuilder.DropTable(
                name: "sys_menu");

            migrationBuilder.DropTable(
                name: "sys_role");

            migrationBuilder.DropTable(
                name: "bi_report");

            migrationBuilder.DropTable(
                name: "bi_publish");

            migrationBuilder.DropIndex(
                name: "IX_sys_user_org_id",
                table: "sys_user");

            migrationBuilder.DropColumn(
                name: "org_id",
                table: "sys_user");
        }
    }
}
