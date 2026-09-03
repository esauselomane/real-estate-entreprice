using System;
using System.Collections.Generic;
using System.Linq;
using CRM.Microservice.Models;
using Microsoft.EntityFrameworkCore;

namespace CRM.Microservice.Data
{
    public static class CRMSeedData
    {
        private static void EnsureSchemaBackfill(CRMDbContext context)
        {
            BackfillColumn(context, "CRMContacts", "LinkedDealIds", "nvarchar(max) NULL DEFAULT ''");
            BackfillColumn(context, "CRMContacts", "Area", "nvarchar(max) NULL DEFAULT ''");
            BackfillColumn(context, "CRMContacts", "Budget", "nvarchar(max) NULL DEFAULT ''");
            BackfillColumn(context, "OwnerTenantRecords", "PropertyTypeInterests", "nvarchar(max) NULL DEFAULT ''");
            BackfillColumn(context, "OwnerTenantRecords", "AreaInterestIds", "nvarchar(max) NULL DEFAULT ''");
        }

        private static void BackfillColumn(CRMDbContext context, string tableName, string columnName, string definition)
        {
            var columnExists = context.Database.SqlQueryRaw<int>(
                $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N'{tableName}' AND COLUMN_NAME = N'{columnName}'")
                .AsEnumerable().FirstOrDefault();
            if (columnExists == 0)
            {
                context.Database.ExecuteSqlRaw($"ALTER TABLE [{tableName}] ADD [{columnName}] {definition};");
            }
        }

        public static void EnsureSeeded(CRMDbContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Database.EnsureCreated();

            EnsureSchemaBackfill(context);

            if (context.AgencyRecords.Count() < 1000)
            {
                var agencies = new List<AgencyRecord>();
                for (var i = context.AgencyRecords.Count() + 1; i <= 1000; i++)
                {
                    agencies.Add(new AgencyRecord
                    {
                        AgencyId = $"AG-{i:0000}",
                        TenantId = "TEN-001",
                        Name = $"Agency {i}",
                        ContactEmail = $"agency{i}@realestate.local",
                        HeadquartersAddress = $"{i} Harbour Road, Cape Town, South Africa",
                        OperationalStatus = i % 4 == 0 ? "Review" : "Active",
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                context.AgencyRecords.AddRange(agencies);
            }

            if (context.ContactPersons.Count() == 0)
            {
                var contactPersons = new List<ContactPerson>
                {
                    new() { ContactPersonId = "CPT-00001", AgencyId = "AG-0001", FullName = "Liam Nel", Email = "liam.nel@agency1.local", Phone = "+27821000001", Position = "Managing Director", Role = "Manager", IsAgencyAdmin = true,  CreatedAt = DateTime.UtcNow },
                    new() { ContactPersonId = "CPT-00002", AgencyId = "AG-0001", FullName = "Sarah du Toit", Email = "sarah.d@agency1.local", Phone = "+27821000002", Position = "Leasing Broker", Role = "Broker", IsAgencyAdmin = false, CreatedAt = DateTime.UtcNow },
                    new() { ContactPersonId = "CPT-00003", AgencyId = "AG-0002", FullName = "Marco Botha", Email = "marco.b@agency2.local", Phone = "+27821000003", Position = "Property Manager", Role = "Owner", IsAgencyAdmin = false, CreatedAt = DateTime.UtcNow }
                };
                context.ContactPersons.AddRange(contactPersons);
            }

            if (context.AgencyRecords.Any(a => string.IsNullOrWhiteSpace(a.TradingName)))
            {
                foreach (var a in context.AgencyRecords.Where(a => string.IsNullOrWhiteSpace(a.TradingName)))
                {
                    a.TradingName = a.Name;
                    a.RegistrationNumber = $"REG-{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}";
                    a.CompanyLogoUrl = "https://ui-avatars.com/api/?background=0ea5e9&color=fff&name=" + Uri.EscapeDataString(a.Name ?? "Agency");
                }
            }

            context.SaveChanges();

            if (context.BrokerRecords.Count() < 1000)
            {
                var brokers = new List<BrokerRecord>();
                var agencyIds = context.AgencyRecords.Select(a => a.AgencyId).ToList();
                var brokerIndex = context.BrokerRecords.Count() + 1;

                for (var i = brokerIndex; i <= 1000; i++)
                {
                    var agencyId = agencyIds[(i - 1) % agencyIds.Count];
                    brokers.Add(new BrokerRecord
                    {
                        BrokerId = $"BRK-{i:0000}",
                        TenantId = "TEN-001",
                        AgencyId = agencyId,
                        FullName = $"Broker {i}",
                        Email = $"broker{i}@realestate.local",
                        Phone = $"+27{(100000000 + i).ToString().Substring(0, 9)}",
                        Role = i % 3 == 0 ? "Manager" : i % 2 == 0 ? "Senior Broker" : "Broker",
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                context.BrokerRecords.AddRange(brokers);
            }

            context.SaveChanges();

            if (context.OwnerTenantRecords.Count() < 1000)
            {
                var ownersTenants = new List<OwnerTenantRecord>();
                var ownerCount = context.OwnerTenantRecords.Count() + 1;
                for (var i = ownerCount; i <= 1000; i++)
                {
                    ownersTenants.Add(new OwnerTenantRecord
                    {
                        TenantId = "TEN-001",
                        RecordType = i % 2 == 0 ? "Owner" : "Tenant",
                        FullName = i % 2 == 0 ? $"Owner {i}" : $"Tenant {i}",
                        CompanyName = i % 2 == 0 ? $"Capital Holdings {i}" : $"Tenant Group {i}",
                        EmailAddress = i % 2 == 0 ? $"owner{i}@invest.local" : $"tenant{i}@occupy.local",
                        PhoneNumber = $"+27{(200000000 + i).ToString().Substring(0, 9)}",
                        PrimaryLocation = i % 3 == 0 ? "Johannesburg" : i % 3 == 1 ? "Cape Town" : "Durban",
                        BudgetLimit = 250000 + (i * 4000),
                        Notes = i % 2 == 0 ? "Owner relationship ready for portfolio expansion." : "Tenant relationship active with renewal cycle.",
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                context.OwnerTenantRecords.AddRange(ownersTenants);
            }

            context.SaveChanges();

            if (context.CRMCompanies.Count() < 1000)
            {
                var companies = new List<CRMCompany>();
                for (var i = context.CRMCompanies.Count() + 1; i <= 1000; i++)
                {
                    companies.Add(new CRMCompany
                    {
                        CompanyId = $"COM-{i:0000}",
                        TenantId = "TEN-001",
                        BusinessName = $"Company {i}",
                        RegistrationNumber = $"REG-{i:000000}",
                        VatNumber = $"VAT-{i:000000}",
                        CorporateWebsite = $"https://company{i}.example.com",
                        HeadOfficeAddress = $"{i} Main Road, Johannesburg, South Africa",
                        CorporateCreditLimit = 500000m + i * 5000,
                        IndustryVertical = i % 4 == 0 ? "Commercial Leasing" : i % 3 == 0 ? "Residential Sales" : "Property Investment",
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                context.CRMCompanies.AddRange(companies);
            }

            context.SaveChanges();

            if (context.CRMLegalEntities.Count() < 1000)
            {
                var legalEntities = new List<CRMLegalEntity>();
                var companyIds = context.CRMCompanies.Select(c => c.CompanyId).ToList();
                for (var i = context.CRMLegalEntities.Count() + 1; i <= 1000; i++)
                {
                    legalEntities.Add(new CRMLegalEntity
                    {
                        LegalEntityId = $"LGE-{i:0000}",
                        TenantId = "TEN-001",
                        RegisteredLegalName = $"Legal Entity {i}",
                        AssociatedCompanyId = companyIds[(i - 1) % companyIds.Count],
                        EntityTypeClassifier = i % 2 == 0 ? "Special Purpose Vehicle" : "Private Company",
                        TaxRegistrationIndex = $"TAX-{i:000000}",
                        DeedsOfficeIdentifier = $"DO-{i:000000}",
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                context.CRMLegalEntities.AddRange(legalEntities);
            }

            context.SaveChanges();

            if (context.CRMContacts.Count() < 1000)
            {
                var contacts = new List<CRMContact>();
                var companyIds = context.CRMCompanies.Select(c => c.CompanyId).ToList();
                var legalIds = context.CRMLegalEntities.Select(l => l.LegalEntityId).ToList();
                for (var i = context.CRMContacts.Count() + 1; i <= 1000; i++)
                {
                    contacts.Add(new CRMContact
                    {
                        ContactId = $"CON-{i:0000}",
                        TenantId = "TEN-001",
                        FullName = $"Contact {i}",
                        EmailAddress = $"contact{i}@pipeline.local",
                        PhoneNumber = $"+27{(300000000 + i).ToString().Substring(0, 9)}",
                        PositionJobTitle = i % 2 == 0 ? "Director" : "Relationship Manager",
                        ContactRoleType = i % 3 == 0 ? "Seller" : i % 2 == 0 ? "Buyer" : "Broker",
                        AssociatedCompanyId = companyIds[(i - 1) % companyIds.Count],
                        AssociatedLegalEntityId = legalIds[(i - 1) % legalIds.Count],
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                context.CRMContacts.AddRange(contacts);
            }

            context.SaveChanges();

            if (context.LeadRecords.Count() < 1000)
            {
                var leads = new List<LeadRecord>();
                var agents = context.BrokerRecords.Select(b => b.FullName).Distinct().ToList();
                for (var i = context.LeadRecords.Count() + 1; i <= 1000; i++)
                {
                    leads.Add(new LeadRecord
                    {
                        TenantId = "TEN-001",
                        ClientName = $"Lead Client {i}",
                        ContactEmail = $"lead{i}@opportunity.local",
                        ContactPhone = $"+27{(400000000 + i).ToString().Substring(0, 9)}",
                        AllocatedToAgent = agents[(i - 1) % agents.Count],
                        LeadStage = (LeadWorkflowStage)(i % 5),
                        PropertyInterestClass = i % 3 == 0 ? "Residential Sale" : i % 3 == 1 ? "Commercial Lease" : "Industrial Investment",
                        PreferredArea = i % 4 == 0 ? "Cape Town" : i % 4 == 1 ? "Johannesburg" : i % 4 == 2 ? "Durban" : "Pretoria",
                        EstimatedBudget = 150000m + (i * 12500),
                        LeadNotes = "Prospect reviewed location, pricing and due diligence analytics.",
                        IsConvertedToDeal = i % 6 == 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                context.LeadRecords.AddRange(leads);
            }

            context.SaveChanges();

            if (context.DealRecords.Count() < 1000)
            {
                var deals = new List<DealRecord>();
                var buyerIds = context.CRMContacts.Select(c => c.ContactId).ToList();
                var sellerIds = context.CRMContacts.Select(c => c.ContactId).ToList();
                var agents = context.BrokerRecords.Select(b => b.FullName).Distinct().ToList();
                for (var i = context.DealRecords.Count() + 1; i <= 1000; i++)
                {
                    deals.Add(new DealRecord
                    {
                        TenantId = "TEN-001",
                        AllocatedToAgent = agents[(i - 1) % agents.Count],
                        DealStage = (DealWorkflowStage)(i % 6),
                        LinkedPropertyId = i % 200 == 0 ? 200 + (i % 250) : i % 900,
                        BuyerClientId = buyerIds[(i - 1) % buyerIds.Count],
                        SellerClientId = sellerIds[(i - 1) % sellerIds.Count],
                        TransactionValue = 500000m + (i * 15000),
                        DealNotes = "Deal advanced through underwriting and legal review.",
                        LastStageUpdatedAt = DateTime.UtcNow.AddDays(-i),
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                context.DealRecords.AddRange(deals);
            }

            if (context.GeographyRecords.Count() < 1)
            {
                context.GeographyRecords.AddRange(new List<GeographyRecord>
                {
                    new() { Country = "South Africa", Province = "Western Cape", City = "Cape Town", Suburb = "City Bowl", Latitude = -33.9249m, Longitude = 18.4207m },
                    new() { Country = "South Africa", Province = "Western Cape", City = "Cape Town", Suburb = "Rondebosch", Latitude = -33.9322m, Longitude = 18.4672m },
                    new() { Country = "South Africa", Province = "Western Cape", City = "Stellenbosch", Suburb = "Stellenbosch Central", Latitude = -33.9348m, Longitude = 18.8659m },
                    new() { Country = "South Africa", Province = "Gauteng", City = "Johannesburg", Suburb = "Sandton", Latitude = -26.1027m, Longitude = 28.0330m },
                    new() { Country = "South Africa", Province = "Gauteng", City = "Pretoria", Suburb = "Hatfield", Latitude = -25.7546m, Longitude = 28.2567m },
                    new() { Country = "South Africa", Province = "KwaZulu-Natal", City = "Durban", Suburb = "Umhlanga", Latitude = -29.9558m, Longitude = 30.9834m }
                });
            }

            // Backfill a representative interest profile onto existing owner/tenant rows.
            if (context.OwnerTenantRecords.Any())
            {
                var ots = context.OwnerTenantRecords.ToList();
                var types = new[] { "Residential", "Commercial", "Retail", "Industrial" };
                for (int i = 0; i < ots.Count; i++)
                {
                    var rec = ots[i];
                    if (string.IsNullOrWhiteSpace(rec.PropertyTypeInterests))
                        rec.PropertyTypeInterests = types[(i % types.Length)];
                    if (string.IsNullOrWhiteSpace(rec.AreaInterestIds))
                        rec.AreaInterestIds = ((1 + (i % 6)).ToString());
                }
            }

            context.SaveChanges();
        }
    }
}
