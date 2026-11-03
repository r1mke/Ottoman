using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ottoman.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShopSectionsAndSalePrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SalePrice",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShopSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    MaxItems = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopSections_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShopSectionProducts",
                columns: table => new
                {
                    ShopSectionId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopSectionProducts", x => new { x.ShopSectionId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_ShopSectionProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShopSectionProducts_ShopSections_ShopSectionId",
                        column: x => x.ShopSectionId,
                        principalTable: "ShopSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Product_SalePrice",
                table: "Products",
                sql: "[SalePrice] IS NULL OR ([SalePrice] > 0 AND [SalePrice] < [Price])");

            migrationBuilder.CreateIndex(
                name: "IX_ShopSectionProducts_ProductId",
                table: "ShopSectionProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopSections_CategoryId",
                table: "ShopSections",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopSections_SortOrder",
                table: "ShopSections",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopSectionProducts");

            migrationBuilder.DropTable(
                name: "ShopSections");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Product_SalePrice",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SalePrice",
                table: "Products");
        }
    }
}
