using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ottoman.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropDeliveryFulfilment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delivery was never offered (the API refused it), but any row that still says so must stay readable now
            // that Pickup is the only value: it becomes a pickup, and its address (never used) goes with the column.
            migrationBuilder.Sql("UPDATE [Orders] SET [Fulfilment] = N'Pickup' WHERE [Fulfilment] <> N'Pickup';");
            migrationBuilder.DropColumn(
                name: "DeliveryAddress",
                table: "Orders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddress",
                table: "Orders",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }
    }
}
