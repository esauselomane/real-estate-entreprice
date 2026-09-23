using CRM.Microservice.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CRM.Microservice.Migrations;

[DbContext(typeof(CRMDbContext))]
[Migration("20260922000100_AddBrokerSyndicationCodes")]
public sealed class AddBrokerSyndicationCodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "SyndicationAgentCodesJson", table: "BrokerRecords", type: "nvarchar(max)", nullable: false, defaultValue: "{}");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "SyndicationAgentCodesJson", table: "BrokerRecords");
}
