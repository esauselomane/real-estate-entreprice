using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Microservice.Migrations
{
    /// <inheritdoc />
    public partial class AddDealRecordStageTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviousStage",
                table: "DealRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PropertyAddress",
                table: "DealRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousStage",
                table: "DealRecords");

            migrationBuilder.DropColumn(
                name: "PropertyAddress",
                table: "DealRecords");
        }
    }
}
