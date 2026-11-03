using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ottoman.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalonSocialLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "SalonSettings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "SalonSettings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "SalonSettings");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "SalonSettings");
        }
    }
}
