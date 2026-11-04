using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ottoman.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReservationConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoConfirmReservations",
                table: "SalonSettings",
                type: "bit",
                nullable: false,
                defaultValue: true); // keep today's behaviour: bookings are confirmed at once

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "Reservations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletedBy",
                table: "Reservations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAtUtc",
                table: "Reservations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedBy",
                table: "Reservations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Declined",
                table: "Reservations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "AuditEntries",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            // Bookings made before this change were confirmed the moment they were made: say so, and keep their status.
            migrationBuilder.Sql(
                "UPDATE [Reservations] SET [ConfirmedAtUtc] = [CreatedAtUtc], [ConfirmedBy] = N'System' " +
                "WHERE [Status] IN (N'Confirmed', N'Completed', N'NoShow') AND [ConfirmedAtUtc] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoConfirmReservations",
                table: "SalonSettings");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "CompletedBy",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ConfirmedAtUtc",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ConfirmedBy",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Declined",
                table: "Reservations");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "AuditEntries",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);
        }
    }
}
