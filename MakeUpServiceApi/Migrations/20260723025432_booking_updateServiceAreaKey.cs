using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakeUpServiceApi.Migrations
{
    /// <inheritdoc />
    public partial class booking_updateServiceAreaKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_ServiceAreas_ServiceAreaAreaID",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ServiceAreaAreaID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ServiceAreaAreaID",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_AreaID",
                table: "Bookings",
                column: "AreaID");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_ServiceAreas_AreaID",
                table: "Bookings",
                column: "AreaID",
                principalTable: "ServiceAreas",
                principalColumn: "AreaID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_ServiceAreas_AreaID",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_AreaID",
                table: "Bookings");

            migrationBuilder.AddColumn<int>(
                name: "ServiceAreaAreaID",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ServiceAreaAreaID",
                table: "Bookings",
                column: "ServiceAreaAreaID");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_ServiceAreas_ServiceAreaAreaID",
                table: "Bookings",
                column: "ServiceAreaAreaID",
                principalTable: "ServiceAreas",
                principalColumn: "AreaID");
        }
    }
}
