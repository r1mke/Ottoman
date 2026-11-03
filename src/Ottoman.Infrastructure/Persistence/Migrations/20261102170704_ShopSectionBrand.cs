using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ottoman.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShopSectionBrand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BrandId",
                table: "ShopSections",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopSections_BrandId",
                table: "ShopSections",
                column: "BrandId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShopSections_Brands_BrandId",
                table: "ShopSections",
                column: "BrandId",
                principalTable: "Brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShopSections_Brands_BrandId",
                table: "ShopSections");

            migrationBuilder.DropIndex(
                name: "IX_ShopSections_BrandId",
                table: "ShopSections");

            migrationBuilder.DropColumn(
                name: "BrandId",
                table: "ShopSections");
        }
    }
}
