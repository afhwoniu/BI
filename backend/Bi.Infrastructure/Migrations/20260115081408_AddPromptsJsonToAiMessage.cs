using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptsJsonToAiMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "prompts_json",
                table: "bi_ai_message",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "prompts_json",
                table: "bi_ai_message");
        }
    }
}
