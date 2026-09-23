using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Microservice.Migrations
{
    /// <inheritdoc />
    public partial class AddAgencySampleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
        IF COL_LENGTH('DealRecords', 'SourceLeadId') IS NULL
            ALTER TABLE DealRecords ADD SourceLeadId int NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DealRecords_LeadRecords_SourceLeadId')
            ALTER TABLE DealRecords ADD CONSTRAINT FK_DealRecords_LeadRecords_SourceLeadId
                FOREIGN KEY (SourceLeadId) REFERENCES LeadRecords(LeadId);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DealRecords_SourceLeadId' AND object_id = OBJECT_ID('DealRecords'))
            CREATE INDEX IX_DealRecords_SourceLeadId ON DealRecords(SourceLeadId);
        IF OBJECT_ID('SampleDataReceipts') IS NULL
            CREATE TABLE SampleDataReceipts (TenantId nvarchar(64) PRIMARY KEY, ResultJson nvarchar(max) NOT NULL);
        """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DealRecords_LeadRecords_SourceLeadId",
                table: "DealRecords");

            migrationBuilder.DropTable(
                name: "SampleDataReceipts");

            migrationBuilder.DropIndex(
                name: "IX_DealRecords_SourceLeadId",
                table: "DealRecords");

            migrationBuilder.DropColumn(
                name: "SourceLeadId",
                table: "DealRecords");
        }
    }
}
