using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakeUpServiceApi.Migrations
{
    /// <inheritdoc />
    public partial class Update_Booking_ServicePrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ServiceFee",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServiceFee",
                table: "Bookings");
        }
    }
}
