using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ottoman.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeTitleForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. The new column, empty for now.
            migrationBuilder.AddColumn<int>(
                name: "TitleId",
                table: "EmployeeProfiles",
                type: "int",
                nullable: true);

            // 2. Every title a barber has is on the list (blank ones become "Barber"), then each barber points at it.
            //    The default collation is case-insensitive, like the unique index on the list.
            migrationBuilder.Sql(@"
INSERT INTO EmployeeTitles (Name)
SELECT DISTINCT LTRIM(RTRIM(p.Title)) FROM EmployeeProfiles p
WHERE LTRIM(RTRIM(p.Title)) <> N'' AND NOT EXISTS (SELECT 1 FROM EmployeeTitles t WHERE t.Name = LTRIM(RTRIM(p.Title)));
IF EXISTS (SELECT 1 FROM EmployeeProfiles WHERE LTRIM(RTRIM(Title)) = N'') AND NOT EXISTS (SELECT 1 FROM EmployeeTitles WHERE Name = N'Barber')
    INSERT INTO EmployeeTitles (Name) VALUES (N'Barber');
UPDATE p SET TitleId = t.Id
FROM EmployeeProfiles p
JOIN EmployeeTitles t ON t.Name = CASE WHEN LTRIM(RTRIM(p.Title)) = N'' THEN N'Barber' ELSE LTRIM(RTRIM(p.Title)) END;");

            // 3. Now every barber has one: make it required, index it and let the database guard it.
            migrationBuilder.AlterColumn<int>(
                name: "TitleId",
                table: "EmployeeProfiles",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeProfiles_TitleId",
                table: "EmployeeProfiles",
                column: "TitleId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeProfiles_EmployeeTitles_TitleId",
                table: "EmployeeProfiles",
                column: "TitleId",
                principalTable: "EmployeeTitles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 4. The old text column goes last, once its data lives in the foreign key.
            migrationBuilder.DropColumn(
                name: "Title",
                table: "EmployeeProfiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "EmployeeProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
UPDATE p SET Title = t.Name FROM EmployeeProfiles p JOIN EmployeeTitles t ON t.Id = p.TitleId;");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeProfiles_EmployeeTitles_TitleId",
                table: "EmployeeProfiles");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeProfiles_TitleId",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "TitleId",
                table: "EmployeeProfiles");
        }
    }
}
