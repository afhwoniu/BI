using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentProcessingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProcessProgress",
                table: "bi_knowledge_document",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProcessedChunkCount",
                table: "bi_knowledge_document",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RawContent",
                table: "bi_knowledge_document",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessProgress",
                table: "bi_knowledge_document");

            migrationBuilder.DropColumn(
                name: "ProcessedChunkCount",
                table: "bi_knowledge_document");

            migrationBuilder.DropColumn(
                name: "RawContent",
                table: "bi_knowledge_document");
        }
    }
}
