using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ottoman.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkGalleryAndSalonProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "SalonSettings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoKey",
                table: "SalonSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MapsUrl",
                table: "SalonSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HaircutCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HaircutCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalonOpeningHours",
                columns: table => new
                {
                    Day = table.Column<int>(type: "int", nullable: false),
                    OpenMinutes = table.Column<int>(type: "int", nullable: true),
                    CloseMinutes = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalonOpeningHours", x => x.Day);
                    table.CheckConstraint("CK_SalonOpeningHours_Range", "([OpenMinutes] IS NULL AND [CloseMinutes] IS NULL) OR ([OpenMinutes] >= 0 AND [CloseMinutes] <= 1440 AND [OpenMinutes] < [CloseMinutes])");
                });

            migrationBuilder.CreateTable(
                name: "GalleryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Caption = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GalleryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GalleryItems_EmployeeProfiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "EmployeeProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GalleryItems_HaircutCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "HaircutCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GalleryItems_CategoryId_CreatedAtUtc",
                table: "GalleryItems",
                columns: new[] { "CategoryId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GalleryItems_ContentHash",
                table: "GalleryItems",
                column: "ContentHash",
                unique: true,
                filter: "[ContentHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GalleryItems_EmployeeId",
                table: "GalleryItems",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_GalleryItems_IsFeatured",
                table: "GalleryItems",
                column: "IsFeatured");

            migrationBuilder.CreateIndex(
                name: "IX_HaircutCategories_Name",
                table: "HaircutCategories",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GalleryItems");

            migrationBuilder.DropTable(
                name: "SalonOpeningHours");

            migrationBuilder.DropTable(
                name: "HaircutCategories");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "SalonSettings");

            migrationBuilder.DropColumn(
                name: "LogoKey",
                table: "SalonSettings");

            migrationBuilder.DropColumn(
                name: "MapsUrl",
                table: "SalonSettings");
        }
    }
}
