using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ottoman.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnnouncementManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Announcements",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "News");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "Announcements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Announcements",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Announcements",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Announcements");
        }
    }
}
