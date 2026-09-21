using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Microservice.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokerMetricsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrokerMonthlyTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BrokerId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    TargetCommission = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerMonthlyTargets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrokerPerformanceMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BrokerId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MetricDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeadToDealConversionRate = table.Column<decimal>(type: "decimal(7,2)", nullable: false),
                    AvgDaysToClose = table.Column<decimal>(type: "decimal(7,1)", nullable: false),
                    AvgDealValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AvgCommissionGenerated = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StageVelocityScore = table.Column<decimal>(type: "decimal(7,2)", nullable: false),
                    GeoSpecializationScore = table.Column<decimal>(type: "decimal(7,2)", nullable: false),
                    PropertyTypeConversionScore = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PriceBandScore = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CrmHygieneScore = table.Column<decimal>(type: "decimal(7,2)", nullable: false),
                    OverallScore = table.Column<decimal>(type: "decimal(7,2)", nullable: false),
                    TotalLeadsAssigned = table.Column<int>(type: "int", nullable: false),
                    TotalDealsWon = table.Column<int>(type: "int", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerPerformanceMetrics", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrokerMonthlyTargets");

            migrationBuilder.DropTable(
                name: "BrokerPerformanceMetrics");
        }
    }
}
