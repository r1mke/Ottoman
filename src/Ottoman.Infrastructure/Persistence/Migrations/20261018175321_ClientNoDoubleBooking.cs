using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ottoman.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClientNoDoubleBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "ReservationSlots",
                type: "nvarchar(450)",
                nullable: true);

            // Rows from before this rule get their client from the reservation. If the data already holds two live
            // reservations of one client at the same time, only the earliest keeps the client on its steps, so the
            // unique index can be created; the later one stays as it was and is not checked retroactively.
            migrationBuilder.Sql(@"
UPDATE s SET s.ClientId = r.ClientId
FROM ReservationSlots s
JOIN Reservations r ON r.Id = s.ReservationId
WHERE r.Id = (
    SELECT MIN(r2.Id)
    FROM ReservationSlots s2
    JOIN Reservations r2 ON r2.Id = s2.ReservationId
    WHERE r2.ClientId = r.ClientId AND s2.SlotStartUtc = s.SlotStartUtc);");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationSlots_ClientId_SlotStartUtc",
                table: "ReservationSlots",
                columns: new[] { "ClientId", "SlotStartUtc" },
                unique: true,
                filter: "[ClientId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReservationSlots_ClientId_SlotStartUtc",
                table: "ReservationSlots");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "ReservationSlots");
        }
    }
}
