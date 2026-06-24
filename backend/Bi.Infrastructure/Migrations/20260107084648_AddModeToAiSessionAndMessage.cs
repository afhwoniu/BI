using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModeToAiSessionAndMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mode",
                table: "bi_ai_session",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "bi");

            migrationBuilder.AddColumn<string>(
                name: "mode",
                table: "bi_ai_message",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mode",
                table: "bi_ai_session");

            migrationBuilder.DropColumn(
                name: "mode",
                table: "bi_ai_message");
        }
    }
}
