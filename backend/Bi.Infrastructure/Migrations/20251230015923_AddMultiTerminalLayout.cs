using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTerminalLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mobile_layout_json",
                table: "bi_panel_item",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "screen_layout_json",
                table: "bi_panel_item",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mobile_layout_json",
                table: "bi_panel_item");

            migrationBuilder.DropColumn(
                name: "screen_layout_json",
                table: "bi_panel_item");
        }
    }
}
