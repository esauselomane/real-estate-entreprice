using CRM.Microservice.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CRM.Microservice.Migrations;

/// <summary>Repairs legacy databases whose brochure table predates the branding columns in the initial snapshot.</summary>
[DbContext(typeof(CRMDbContext))]
[Migration("20260915000100_RepairBrochureBrandingColumns")]
public sealed class RepairBrochureBrandingColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('BrochureCards', 'CompanyName') IS NULL
                ALTER TABLE BrochureCards ADD CompanyName nvarchar(max) NOT NULL DEFAULT N'';
            IF COL_LENGTH('BrochureCards', 'CompanyLogoUrl') IS NULL
                ALTER TABLE BrochureCards ADD CompanyLogoUrl nvarchar(max) NOT NULL DEFAULT N'';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Retain branding data: these columns also belong to the initial model.
    }
}
