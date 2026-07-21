using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakeUpServiceApi.Migrations
{
    /// <inheritdoc />
    public partial class updateBooking_newpax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Pax",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Pax",
                table: "Bookings");
        }
    }
}
