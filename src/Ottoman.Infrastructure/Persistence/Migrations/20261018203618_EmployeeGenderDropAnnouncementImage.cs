using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ottoman.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeGenderDropAnnouncementImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageKey",
                table: "Announcements");

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "EmployeeProfiles",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            // The three demo barbers are men; everybody else is set in the employee form.
            migrationBuilder.Sql(@"
UPDATE p SET p.Gender = N'Male'
FROM EmployeeProfiles p
JOIN AspNetUsers u ON u.Id = p.UserId
WHERE p.Gender IS NULL AND u.Email IN (N'kerim@ottoman.local', N'tarik@ottoman.local', N'adin@ottoman.local');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gender",
                table: "EmployeeProfiles");

            migrationBuilder.AddColumn<string>(
                name: "ImageKey",
                table: "Announcements",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
