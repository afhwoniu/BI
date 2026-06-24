using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiMessageDrillFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "detail_sql",
                table: "bi_ai_message",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dimension_fields",
                table: "bi_ai_message",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hospital_field",
                table: "bi_ai_message",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kpi_config",
                table: "bi_ai_message",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "measure_fields",
                table: "bi_ai_message",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prompt_text",
                table: "bi_ai_message",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "detail_sql",
                table: "bi_ai_message");

            migrationBuilder.DropColumn(
                name: "dimension_fields",
                table: "bi_ai_message");

            migrationBuilder.DropColumn(
                name: "hospital_field",
                table: "bi_ai_message");

            migrationBuilder.DropColumn(
                name: "kpi_config",
                table: "bi_ai_message");

            migrationBuilder.DropColumn(
                name: "measure_fields",
                table: "bi_ai_message");

            migrationBuilder.DropColumn(
                name: "prompt_text",
                table: "bi_ai_message");
        }
    }
}
