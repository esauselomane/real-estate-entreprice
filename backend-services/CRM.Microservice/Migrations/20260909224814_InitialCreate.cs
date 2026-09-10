using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Microservice.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgencyRecords",
                columns: table => new
                {
                    AgencyId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TradingName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyLogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HeadquartersAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperationalStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BrokerageLicense = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParentAgencyId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsTrial = table.Column<bool>(type: "bit", nullable: false),
                    TrialStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrialEndDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgencyRecords", x => x.AgencyId);
                    table.ForeignKey(
                        name: "FK_AgencyRecords_AgencyRecords_ParentAgencyId",
                        column: x => x.ParentAgencyId,
                        principalTable: "AgencyRecords",
                        principalColumn: "AgencyId");
                });

            migrationBuilder.CreateTable(
                name: "BrochureCards",
                columns: table => new
                {
                    CardId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DealId = table.Column<int>(type: "int", nullable: false),
                    BrokerAgentId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClientEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClientPhone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyLogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    GeneratedFilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrochureCards", x => x.CardId);
                });

            migrationBuilder.CreateTable(
                name: "BrokerRecords",
                columns: table => new
                {
                    BrokerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AgencyId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerRecords", x => x.BrokerId);
                });

            migrationBuilder.CreateTable(
                name: "DealRecords",
                columns: table => new
                {
                    DealId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllocatedBrokerIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DealStage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LinkedPropertyId = table.Column<int>(type: "int", nullable: true),
                    BuyerClientId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SellerClientId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DealNotes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastStageUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CommissionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CommissionValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RealisedCommission = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RealisedCommissionIsAuto = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealRecords", x => x.DealId);
                });

            migrationBuilder.CreateTable(
                name: "GeographyRecords",
                columns: table => new
                {
                    GeographyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Province = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Suburb = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,6)", nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(10,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeographyRecords", x => x.GeographyId);
                });

            migrationBuilder.CreateTable(
                name: "LeadRecords",
                columns: table => new
                {
                    LeadId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LinkedContactId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AllocatedBrokerIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeadStage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PropertyInterestClass = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreferredArea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeadType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstimatedBudget = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LeadNotes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsConvertedToDeal = table.Column<bool>(type: "bit", nullable: false),
                    CommissionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CommissionValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadRecords", x => x.LeadId);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LinkType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LinkId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                });

            migrationBuilder.CreateTable(
                name: "OperationalRecords",
                columns: table => new
                {
                    OperationalRecordId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PropertyReference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: true),
                    PropertyUnitId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalRecords", x => x.OperationalRecordId);
                });

            migrationBuilder.CreateTable(
                name: "OwnerTenantRecords",
                columns: table => new
                {
                    OwnerTenantId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecordType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrimaryLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BudgetLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PropertyTypeInterests = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AreaInterestIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerTenantRecords", x => x.OwnerTenantId);
                });

            migrationBuilder.CreateTable(
                name: "ContactPersons",
                columns: table => new
                {
                    ContactPersonId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AgencyId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Position = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsAgencyAdmin = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactPersons", x => x.ContactPersonId);
                    table.ForeignKey(
                        name: "FK_ContactPersons_AgencyRecords_AgencyId",
                        column: x => x.AgencyId,
                        principalTable: "AgencyRecords",
                        principalColumn: "AgencyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BrochureCardLines",
                columns: table => new
                {
                    LineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CardId = table.Column<int>(type: "int", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    UnitNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UnitDescription = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PropertyDescription = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    NetRentableAreaSqM = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    SelectedImageRefs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrochureCardLines", x => x.LineId);
                    table.ForeignKey(
                        name: "FK_BrochureCardLines_BrochureCards_CardId",
                        column: x => x.CardId,
                        principalTable: "BrochureCards",
                        principalColumn: "CardId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DealBrokerSplits",
                columns: table => new
                {
                    SplitId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealId = table.Column<int>(type: "int", nullable: false),
                    BrokerId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BrokerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SplitPercentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SplitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealBrokerSplits", x => x.SplitId);
                    table.ForeignKey(
                        name: "FK_DealBrokerSplits_DealRecords_DealId",
                        column: x => x.DealId,
                        principalTable: "DealRecords",
                        principalColumn: "DealId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    ContactId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PositionJobTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrimaryLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BudgetLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PropertyTypeInterests = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AreaInterestIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssociatedCompanyId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    AssociatedLegalEntityId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    LinkedDealIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgencyId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsAgencyAdmin = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.ContactId);
                    table.ForeignKey(
                        name: "FK_Contacts_AgencyRecords_AgencyId",
                        column: x => x.AgencyId,
                        principalTable: "AgencyRecords",
                        principalColumn: "AgencyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CRMCompanies",
                columns: table => new
                {
                    CompanyId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VatNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorporateWebsite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HeadOfficeAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorporateCreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IndustryVertical = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssociatedLegalEntityId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRMCompanies", x => x.CompanyId);
                });

            migrationBuilder.CreateTable(
                name: "CRMLegalEntities",
                columns: table => new
                {
                    LegalEntityId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegisteredLegalName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssociatedCompanyId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EntityTypeClassifier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TaxRegistrationIndex = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeedsOfficeIdentifier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRMLegalEntities", x => x.LegalEntityId);
                    table.ForeignKey(
                        name: "FK_CRMLegalEntities_CRMCompanies_AssociatedCompanyId",
                        column: x => x.AssociatedCompanyId,
                        principalTable: "CRMCompanies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CRMContacts",
                columns: table => new
                {
                    ContactId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PositionJobTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactRoleType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssociatedCompanyId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    AssociatedLegalEntityId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    LinkedDealIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRMContacts", x => x.ContactId);
                    table.ForeignKey(
                        name: "FK_CRMContacts_CRMCompanies_AssociatedCompanyId",
                        column: x => x.AssociatedCompanyId,
                        principalTable: "CRMCompanies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMContacts_CRMLegalEntities_AssociatedLegalEntityId",
                        column: x => x.AssociatedLegalEntityId,
                        principalTable: "CRMLegalEntities",
                        principalColumn: "LegalEntityId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgencyRecords_ParentAgencyId",
                table: "AgencyRecords",
                column: "ParentAgencyId");

            migrationBuilder.CreateIndex(
                name: "IX_BrochureCardLines_CardId",
                table: "BrochureCardLines",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_AgencyId",
                table: "ContactPersons",
                column: "AgencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_AgencyId",
                table: "Contacts",
                column: "AgencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_AssociatedCompanyId",
                table: "Contacts",
                column: "AssociatedCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_AssociatedLegalEntityId",
                table: "Contacts",
                column: "AssociatedLegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CRMCompanies_AssociatedLegalEntityId",
                table: "CRMCompanies",
                column: "AssociatedLegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CRMContacts_AssociatedCompanyId",
                table: "CRMContacts",
                column: "AssociatedCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CRMContacts_AssociatedLegalEntityId",
                table: "CRMContacts",
                column: "AssociatedLegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CRMLegalEntities_AssociatedCompanyId",
                table: "CRMLegalEntities",
                column: "AssociatedCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_DealBrokerSplits_DealId",
                table: "DealBrokerSplits",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId_CreatedAt",
                table: "Notifications",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Contacts_CRMCompanies_AssociatedCompanyId",
                table: "Contacts",
                column: "AssociatedCompanyId",
                principalTable: "CRMCompanies",
                principalColumn: "CompanyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contacts_CRMLegalEntities_AssociatedLegalEntityId",
                table: "Contacts",
                column: "AssociatedLegalEntityId",
                principalTable: "CRMLegalEntities",
                principalColumn: "LegalEntityId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CRMCompanies_CRMLegalEntities_AssociatedLegalEntityId",
                table: "CRMCompanies",
                column: "AssociatedLegalEntityId",
                principalTable: "CRMLegalEntities",
                principalColumn: "LegalEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CRMLegalEntities_CRMCompanies_AssociatedCompanyId",
                table: "CRMLegalEntities");

            migrationBuilder.DropTable(
                name: "BrochureCardLines");

            migrationBuilder.DropTable(
                name: "BrokerRecords");

            migrationBuilder.DropTable(
                name: "ContactPersons");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropTable(
                name: "CRMContacts");

            migrationBuilder.DropTable(
                name: "DealBrokerSplits");

            migrationBuilder.DropTable(
                name: "GeographyRecords");

            migrationBuilder.DropTable(
                name: "LeadRecords");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "OperationalRecords");

            migrationBuilder.DropTable(
                name: "OwnerTenantRecords");

            migrationBuilder.DropTable(
                name: "BrochureCards");

            migrationBuilder.DropTable(
                name: "AgencyRecords");

            migrationBuilder.DropTable(
                name: "DealRecords");

            migrationBuilder.DropTable(
                name: "CRMCompanies");

            migrationBuilder.DropTable(
                name: "CRMLegalEntities");
        }
    }
}
