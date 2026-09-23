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
            // Older local installations created these tables before migration tracking.
            // Adopt existing tables without deleting their data; create missing tables normally.
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.Sql("""
                    IF OBJECT_ID(N'[dbo].[BrokerMonthlyTargets]', N'U') IS NULL
                        CREATE TABLE [dbo].[BrokerMonthlyTargets] (
                            [Id] int IDENTITY PRIMARY KEY,
                            [TenantId] nvarchar(max) NOT NULL,
                            [BrokerId] nvarchar(max) NOT NULL,
                            [Year] int NOT NULL,
                            [Month] int NOT NULL,
                            [TargetCommission] decimal(18,2) NOT NULL,
                            [CreatedAt] datetime2 NOT NULL,
                            [UpdatedAt] datetime2 NOT NULL
                        );
                    IF OBJECT_ID(N'[dbo].[BrokerPerformanceMetrics]', N'U') IS NULL
                        CREATE TABLE [dbo].[BrokerPerformanceMetrics] (
                            [Id] int IDENTITY PRIMARY KEY,
                            [TenantId] nvarchar(max) NOT NULL,
                            [BrokerId] nvarchar(max) NOT NULL,
                            [MetricDate] datetime2 NOT NULL,
                            [LeadToDealConversionRate] decimal(7,2) NOT NULL,
                            [AvgDaysToClose] decimal(7,1) NOT NULL,
                            [AvgDealValue] decimal(18,2) NOT NULL,
                            [AvgCommissionGenerated] decimal(18,2) NOT NULL,
                            [StageVelocityScore] decimal(7,2) NOT NULL,
                            [GeoSpecializationScore] decimal(7,2) NOT NULL,
                            [PropertyTypeConversionScore] nvarchar(max) NOT NULL,
                            [PriceBandScore] nvarchar(max) NOT NULL,
                            [CrmHygieneScore] decimal(7,2) NOT NULL,
                            [OverallScore] decimal(7,2) NOT NULL,
                            [TotalLeadsAssigned] int NOT NULL,
                            [TotalDealsWon] int NOT NULL,
                            [ComputedAt] datetime2 NOT NULL
                        );
                    """);
                return;
            }

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
