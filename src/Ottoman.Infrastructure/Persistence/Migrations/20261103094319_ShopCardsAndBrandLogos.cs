using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ottoman.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShopCardsAndBrandLogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageKey",
                table: "ShopSections",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImageProductImageId",
                table: "ShopSections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowAsCard",
                table: "ShopSections",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LogoKey",
                table: "Brands",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageKey",
                table: "ShopSections");

            migrationBuilder.DropColumn(
                name: "ImageProductImageId",
                table: "ShopSections");

            migrationBuilder.DropColumn(
                name: "ShowAsCard",
                table: "ShopSections");

            migrationBuilder.DropColumn(
                name: "LogoKey",
                table: "Brands");
        }
    }
}
