using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ottoman.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShopBlocksAndLook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Background",
                table: "ShopSections",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "CardSpan",
                table: "ShopSections",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Half");

            migrationBuilder.AddColumn<string>(
                name: "HeadingAlign",
                table: "ShopSections",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Left");

            migrationBuilder.AddColumn<bool>(
                name: "ShowSeeAll",
                table: "ShopSections",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TileSize",
                table: "ShopSections",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.CreateTable(
                name: "ShopSectionEntries",
                columns: table => new
                {
                    ShopSectionId = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopSectionEntries", x => new { x.ShopSectionId, x.EntityId });
                    table.ForeignKey(
                        name: "FK_ShopSectionEntries_ShopSections_ShopSectionId",
                        column: x => x.ShopSectionId,
                        principalTable: "ShopSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopSectionEntries");

            migrationBuilder.DropColumn(
                name: "Background",
                table: "ShopSections");

            migrationBuilder.DropColumn(
                name: "CardSpan",
                table: "ShopSections");

            migrationBuilder.DropColumn(
                name: "HeadingAlign",
                table: "ShopSections");

            migrationBuilder.DropColumn(
                name: "ShowSeeAll",
                table: "ShopSections");

            migrationBuilder.DropColumn(
                name: "TileSize",
                table: "ShopSections");
        }
    }
}
