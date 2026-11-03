using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ottoman.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductImageContentHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages");

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "ProductImages",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId_ContentHash",
                table: "ProductImages",
                columns: new[] { "ProductId", "ContentHash" },
                unique: true,
                filter: "[ContentHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductImages_ProductId_ContentHash",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "ProductImages");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId");
        }
    }
}
