using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakeUpServiceApi.Migrations
{
    /// <inheritdoc />
    public partial class updatebooking_unit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Bookings");
        }
    }
}
