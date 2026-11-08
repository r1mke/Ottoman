using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ottoman.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReviewTypedTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployeeProfileId",
                table: "Reviews",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductId",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceId",
                table: "Reviews",
                type: "int",
                nullable: true);

            // Move the existing reviews to the typed keys. A review whose target no longer exists (or never did)
            // has nothing to point at; it is removed rather than left breaking the new foreign keys.
            migrationBuilder.Sql(@"
UPDATE r SET ProductId = p.Id FROM Reviews r JOIN Products p ON p.Id = TRY_CAST(r.TargetKey AS int) WHERE r.TargetType = 'Product';
UPDATE r SET ServiceId = s.Id FROM Reviews r JOIN Services s ON s.Id = TRY_CAST(r.TargetKey AS int) WHERE r.TargetType = 'Service';
UPDATE r SET EmployeeProfileId = e.UserId FROM Reviews r JOIN EmployeeProfiles e ON e.UserId = r.TargetKey WHERE r.TargetType = 'Employee';
DELETE FROM Reviews WHERE ProductId IS NULL AND ServiceId IS NULL AND EmployeeProfileId IS NULL;");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ClientId_TargetType_TargetKey",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_TargetType_TargetKey_IsHidden",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "TargetKey",
                table: "Reviews");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ClientId_EmployeeProfileId",
                table: "Reviews",
                columns: new[] { "ClientId", "EmployeeProfileId" },
                unique: true,
                filter: "[EmployeeProfileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ClientId_ProductId",
                table: "Reviews",
                columns: new[] { "ClientId", "ProductId" },
                unique: true,
                filter: "[ProductId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ClientId_ServiceId",
                table: "Reviews",
                columns: new[] { "ClientId", "ServiceId" },
                unique: true,
                filter: "[ServiceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_EmployeeProfileId_IsHidden",
                table: "Reviews",
                columns: new[] { "EmployeeProfileId", "IsHidden" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ProductId_IsHidden",
                table: "Reviews",
                columns: new[] { "ProductId", "IsHidden" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ServiceId_IsHidden",
                table: "Reviews",
                columns: new[] { "ServiceId", "IsHidden" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Review_OneTarget",
                table: "Reviews",
                sql: "([TargetType] = 'Product' AND [ProductId] IS NOT NULL AND [ServiceId] IS NULL AND [EmployeeProfileId] IS NULL) OR ([TargetType] = 'Service' AND [ServiceId] IS NOT NULL AND [ProductId] IS NULL AND [EmployeeProfileId] IS NULL) OR ([TargetType] = 'Employee' AND [EmployeeProfileId] IS NOT NULL AND [ProductId] IS NULL AND [ServiceId] IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_EmployeeProfiles_EmployeeProfileId",
                table: "Reviews",
                column: "EmployeeProfileId",
                principalTable: "EmployeeProfiles",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Products_ProductId",
                table: "Reviews",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Services_ServiceId",
                table: "Reviews",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetKey",
                table: "Reviews",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE Reviews SET TargetKey = COALESCE(CAST(ProductId AS nvarchar(450)), CAST(ServiceId AS nvarchar(450)), EmployeeProfileId);
ALTER TABLE Reviews ALTER COLUMN TargetKey nvarchar(450) NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_EmployeeProfiles_EmployeeProfileId",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Products_ProductId",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Services_ServiceId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ClientId_EmployeeProfileId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ClientId_ProductId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ClientId_ServiceId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_EmployeeProfileId_IsHidden",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ProductId_IsHidden",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ServiceId_IsHidden",
                table: "Reviews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Review_OneTarget",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "EmployeeProfileId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ServiceId",
                table: "Reviews");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ClientId_TargetType_TargetKey",
                table: "Reviews",
                columns: new[] { "ClientId", "TargetType", "TargetKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_TargetType_TargetKey_IsHidden",
                table: "Reviews",
                columns: new[] { "TargetType", "TargetKey", "IsHidden" });
        }
    }
}
