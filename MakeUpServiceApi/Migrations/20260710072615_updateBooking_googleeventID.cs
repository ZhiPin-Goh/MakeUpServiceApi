using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakeUpServiceApi.Migrations
{
    /// <inheritdoc />
    public partial class updateBooking_googleeventID : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleEventID",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleEventID",
                table: "Bookings");
        }
    }
}
