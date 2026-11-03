using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ottoman.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeTitles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeTitles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeTitles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTitles_Name",
                table: "EmployeeTitles",
                column: "Name",
                unique: true);

            // The pick list starts with the usual titles plus whatever the barbers already have.
            migrationBuilder.Sql(@"
INSERT INTO EmployeeTitles (Name)
SELECT v.Name FROM (VALUES (N'Master Barber'), (N'Senior Barber'), (N'Barber'), (N'Junior Barber')) AS v(Name)
UNION
SELECT DISTINCT LTRIM(RTRIM(Title)) FROM EmployeeProfiles WHERE LTRIM(RTRIM(Title)) <> N'';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeTitles");
        }
    }
}
