using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Microservice.Migrations
{
    /// <inheritdoc />
    public partial class AddEpilepsySafeModeToUserAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EpilepsySafeMode",
                table: "UserAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EpilepsySafeMode",
                table: "UserAccounts");
        }
    }
}
