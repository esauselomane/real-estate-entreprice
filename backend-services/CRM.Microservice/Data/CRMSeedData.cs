using System.Linq.Expressions;
using System.Linq;
using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Services.Abstractions;
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

        private static void EnsureAgencyParentColumn(CRMDbContext context)
        {
            BackfillColumn(context, "AgencyRecords", "ParentAgencyId", "nvarchar(64) NULL");
            BackfillColumn(context, "AgencyRecords", "IsTrial", "bit NOT NULL DEFAULT 0");
            BackfillColumn(context, "AgencyRecords", "TrialStartDate", "datetime2 NULL");
            BackfillColumn(context, "AgencyRecords", "TrialEndDate", "datetime2 NULL");
        }

        public static void EnsureSeeded(CRMDbContext context, IAgencyRepository agencyRepository, IBrokerRepository brokerRepository, ILeadRepository leadRepository, IDealRepository dealRepository, ICompanyRepository companyRepository, ILegalEntityRepository legalEntityRepository, IContactRepository contactRepository, IOwnerTenantRepository ownerTenantRepository, IGeographyRepository geographyRepository, IContactPersonRepository contactPersonRepository, IBrochureCardRepository brochureCardRepository, IDealBrokerSplitRepository dealBrokerSplitRepository, INotificationRepository notificationRepository)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Database.EnsureCreated();

            EnsureSchemaBackfill(context);

            EnsureAgencyParentColumn(context);

            if (agencyRepository.Query().Count() < 1000)
            {
                var agencies = new List<AgencyRecord>();
                // 🧬 Each agency gets its OWN tenant so the TenantSwitcher can
                // actually filter the universe. The first 8 agencies are
                // top-level (parent = null); the rest roll up under AG-0001
                // to demonstrate the parent/child hierarchy.
                for (var i = agencyRepository.Query().Count() + 1; i <= 1000; i++)
                {
                    string? parentAgencyId = i <= 8 ? null : (i % 50 == 0 ? $"AG-0001" : $"AG-{(i % 8) + 1:0000}");
                    agencies.Add(new AgencyRecord
                    {
                        AgencyId = $"AG-{i:0000}",
                        TenantId = $"TEN-{i:0000}",
                        ParentAgencyId = parentAgencyId,
                        Name = $"Agency {i}",
                        ContactEmail = $"agency{i}@realestate.local",
                        HeadquartersAddress = $"{i} Harbour Road, Cape Town, South Africa",
                        OperationalStatus = i % 7 == 0 ? "Review" : i % 11 == 0 ? "TrialPending" : "Active",
                        IsTrial = i % 11 == 0,
                        TrialStartDate = i % 11 == 0 ? DateTime.UtcNow.AddDays(-7) : (DateTime?)null,
                        TrialEndDate = i % 11 == 0 ? DateTime.UtcNow.AddDays(7) : (DateTime?)null,
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                agencyRepository.AddRange(agencies);
            }

            if (contactPersonRepository.Query().Count() == 0)
            {
                var contactPersons = new List<ContactPerson>
                {
                    new() { ContactPersonId = "CPT-00001", AgencyId = "AG-0001", FullName = "Liam Nel", Email = "liam.nel@agency1.local", Phone = "+27821000001", Position = "Managing Director", Role = "Manager", IsAgencyAdmin = true,  CreatedAt = DateTime.UtcNow },
                    new() { ContactPersonId = "CPT-00002", AgencyId = "AG-0001", FullName = "Sarah du Toit", Email = "sarah.d@agency1.local", Phone = "+27821000002", Position = "Leasing Broker", Role = "Broker", IsAgencyAdmin = false, CreatedAt = DateTime.UtcNow },
                    new() { ContactPersonId = "CPT-00003", AgencyId = "AG-0002", FullName = "Marco Botha", Email = "marco.b@agency2.local", Phone = "+27821000003", Position = "Property Manager", Role = "Owner", IsAgencyAdmin = false, CreatedAt = DateTime.UtcNow }
                };
                contactPersonRepository.AddRange(contactPersons);
            }

            if (agencyRepository.Query().Any(a => string.IsNullOrWhiteSpace(a.TradingName)))
            {
                foreach (var a in agencyRepository.Query().Where(a => string.IsNullOrWhiteSpace(a.TradingName)))
                {
                    a.TradingName = a.Name;
                    a.RegistrationNumber = $"REG-{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}";
                    a.CompanyLogoUrl = "https://ui-avatars.com/api/?background=0ea5e9&color=fff&name=" + Uri.EscapeDataString(a.Name ?? "Agency");
                }
            }

            context.SaveChanges();

            if (brokerRepository.Query().Count() < 1000)
            {
                var brokers = new List<BrokerRecord>();
                // 🧬 Brokers inherit TenantId from their parent agency so the
                // TenantSwitcher can scope the broker list to one agency.
                var agencyTenantLookup = agencyRepository.Query()
                    .Select(a => new { a.AgencyId, a.TenantId })
                    .ToDictionary(a => a.AgencyId, a => a.TenantId);
                var agencyIds = agencyTenantLookup.Keys.ToList();
                var brokerIndex = brokerRepository.Query().Count() + 1;

                for (var i = brokerIndex; i <= 1000; i++)
                {
                    var agencyId = agencyIds[(i - 1) % agencyIds.Count];
                    var tenantId = agencyTenantLookup.TryGetValue(agencyId, out var t) ? t : "TEN-001";
                    brokers.Add(new BrokerRecord
                    {
                        BrokerId = $"BRK-{i:0000}",
                        TenantId = tenantId,
                        AgencyId = agencyId,
                        FullName = $"Broker {i}",
                        Email = $"broker{i}@realestate.local",
                        Phone = $"+27{(100000000 + i).ToString().Substring(0, 9)}",
                        Role = i % 3 == 0 ? "Manager" : i % 2 == 0 ? "Senior Broker" : "Broker",
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                brokerRepository.AddRange(brokers);
            }

            context.SaveChanges();

            if (ownerTenantRepository.Query().Count() < 1000)
            {
                var ownersTenants = new List<OwnerTenantRecord>();
                var ownerCount = ownerTenantRepository.Query().Count() + 1;
                for (var i = ownerCount; i <= 1000; i++)
                {
                    // 🧬 Each row is stamped with the tenant of an agency
                    // chosen deterministically by index, so picking "Agency 42"
                    // in the dropdown surfaces only the owners/tenants that
                    // rolled into that tenant.
                    ownersTenants.Add(new OwnerTenantRecord
                    {
                        TenantId = $"TEN-{(i % 1000) + 1:0000}",
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

                ownerTenantRepository.AddRange(ownersTenants);
            }

            context.SaveChanges();

            if (companyRepository.Query().Count() < 1000)
            {
                var companies = new List<CRMCompany>();
                for (var i = companyRepository.Query().Count() + 1; i <= 1000; i++)
                {
                    companies.Add(new CRMCompany
                    {
                        CompanyId = $"COM-{i:0000}",
                        TenantId = $"TEN-{(i % 1000) + 1:0000}",
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

                companyRepository.AddRange(companies);
            }

            context.SaveChanges();

            if (legalEntityRepository.Query().Count() < 1000)
            {
                var legalEntities = new List<CRMLegalEntity>();
                var companyIds = companyRepository.Query().Select(c => c.CompanyId).ToList();
                for (var i = legalEntityRepository.Query().Count() + 1; i <= 1000; i++)
                {
                    legalEntities.Add(new CRMLegalEntity
                    {
                        LegalEntityId = $"LGE-{i:0000}",
                        TenantId = $"TEN-{(i % 1000) + 1:0000}",
                        RegisteredLegalName = $"Legal Entity {i}",
                        AssociatedCompanyId = companyIds[(i - 1) % companyIds.Count],
                        EntityTypeClassifier = i % 2 == 0 ? "Special Purpose Vehicle" : "Private Company",
                        TaxRegistrationIndex = $"TAX-{i:000000}",
                        DeedsOfficeIdentifier = $"DO-{i:000000}",
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                legalEntityRepository.AddRange(legalEntities);
            }

            context.SaveChanges();

            if (contactRepository.Query().Count() < 1000)
            {
                var contacts = new List<CRMContact>();
                var companyIds = companyRepository.Query().Select(c => c.CompanyId).ToList();
                var legalIds = legalEntityRepository.Query().Select(l => l.LegalEntityId).ToList();
                for (var i = contactRepository.Query().Count() + 1; i <= 1000; i++)
                {
                    contacts.Add(new CRMContact
                    {
                        ContactId = $"CON-{i:0000}",
                        TenantId = $"TEN-{(i % 1000) + 1:0000}",
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

                contactRepository.AddRange(contacts);
            }

            context.SaveChanges();

            if (leadRepository.Query().Count() < 1000)
            {
                var leads = new List<LeadRecord>();
                // 🧬 Leads inherit TenantId from their allocated broker so the
                // TenantSwitcher scopes the lead list to a single agency.
                // The seed now stores AllocatedBrokerIds (csv of broker user
                // ids) instead of the legacy free-text AllocatedToAgent name.
                var brokerTenantLookup = brokerRepository.Query()
                    .Select(b => new { b.BrokerId, b.FullName, b.TenantId })
                    .GroupBy(x => x.BrokerId)
                    .Select(g => g.First())
                    .ToDictionary(x => x.BrokerId, x => new { x.FullName, x.TenantId });
                var agents = brokerTenantLookup.Keys.ToList();
                for (var i = leadRepository.Query().Count() + 1; i <= 1000; i++)
                {
                    var agentId = agents[(i - 1) % agents.Count];
                    var agent = brokerTenantLookup[agentId];
                    var tenantId = agent.TenantId;
                    leads.Add(new LeadRecord
                    {
                        TenantId = tenantId,
                        ClientName = $"Lead Client {i}",
                        ContactEmail = $"lead{i}@opportunity.local",
                        ContactPhone = $"+27{(400000000 + i).ToString().Substring(0, 9)}",
                        AllocatedBrokerIds = agentId,
                        LeadStage = (LeadWorkflowStage)(i % 5),
                        PropertyInterestClass = i % 3 == 0 ? "Residential Sale" : i % 3 == 1 ? "Commercial Lease" : "Industrial Investment",
                        PreferredArea = i % 4 == 0 ? "Cape Town" : i % 4 == 1 ? "Johannesburg" : i % 4 == 2 ? "Durban" : "Pretoria",
                        EstimatedBudget = 150000m + (i * 12500),
                        // 🧮 Carry commission on the lead so promoted-to-deal
                        // has a commission to inherit.
                        CommissionType = i % 10 < 7 ? CommissionTypes.Percent : CommissionTypes.Amount,
                        CommissionValue = i % 10 < 7
                            ? 3m + (decimal)(i % 5)
                            : 25000m + (i * 750m),
                        LeadNotes = "Prospect reviewed location, pricing and due diligence analytics.",
                        IsConvertedToDeal = i % 6 == 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                leadRepository.AddRange(leads);
            }

            context.SaveChanges();

            if (dealRepository.Query().Count() < 1000)
            {
                var deals = new List<DealRecord>();
                var buyerIds = contactRepository.Query().Select(c => c.ContactId).ToList();
                var sellerIds = contactRepository.Query().Select(c => c.ContactId).ToList();
                // 🧬 Deals inherit TenantId from the allocated broker's agency
                // so the TenantSwitcher scopes the deal list to one agency.
                // AllocatedBrokerIds stores the broker user id (matches the
                // updated DealRecord schema).
                var brokerTenantLookup = brokerRepository.Query()
                    .Select(b => new { b.BrokerId, b.FullName, b.TenantId })
                    .GroupBy(x => x.BrokerId)
                    .Select(g => g.First())
                    .ToDictionary(x => x.BrokerId, x => new { x.FullName, x.TenantId });
                var agents = brokerTenantLookup.Keys.ToList();
                for (var i = dealRepository.Query().Count() + 1; i <= 1000; i++)
                {
                    var agentId = agents[(i - 1) % agents.Count];
                    var agent = brokerTenantLookup[agentId];
                    var tenantId = agent.TenantId;
                    deals.Add(new DealRecord
                    {
                        TenantId = tenantId,
                        AllocatedBrokerIds = agentId,
                        DealStage = (DealWorkflowStage)(i % 6),
                        LinkedPropertyId = i % 200 == 0 ? 200 + (i % 250) : i % 900,
                        BuyerClientId = buyerIds[(i - 1) % buyerIds.Count],
                        SellerClientId = sellerIds[(i - 1) % sellerIds.Count],
                        TransactionValue = 500000m + (i * 15000),
                        // 🧮 Every deal carries a commission so the reports
                        // (commission-by-stage, broker leaderboard, closed-won
                        // commission) actually surface numbers. ~70% are
                        // percentage-based (3-7% of the deal value), the rest
                        // are flat Rand amounts.
                        CommissionType = i % 10 < 7 ? CommissionTypes.Percent : CommissionTypes.Amount,
                        CommissionValue = i % 10 < 7
                            ? 3m + (decimal)((i % 5)) // 3%–7% bands
                            : 25000m + (i * 750m),    // flat Rand amount
                        DealNotes = "Deal advanced through underwriting and legal review.",
                        LastStageUpdatedAt = DateTime.UtcNow.AddDays(-i),
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }

                dealRepository.AddRange(deals);
            }

            if (geographyRepository.Query().Count() < 1)
            {
                geographyRepository.AddRange(new List<GeographyRecord>
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
            if (ownerTenantRepository.Query().Any())
            {
                var ots = ownerTenantRepository.Query().ToList();
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

            // 🧬 One-shot tenant-distribution backfill. Earlier seed data
            // stamped every record with the single legacy "TEN-001" so the
            // TenantSwitcher had no per-agency data to filter. When this
            // legacy tenant is still present, re-stamp every table so each
            // row lands in a per-agency tenant (mirrors the seeder's
            // `i % 1000 + 1` distribution). Idempotent: once no rows carry
            // TEN-001, the UPDATE has nothing to do.
            BackfillLegacySingleTenantDistribution(context, agencyRepository, brokerRepository, ownerTenantRepository, companyRepository, legalEntityRepository, contactRepository, leadRepository, dealRepository);

            context.SaveChanges();
        }

        private static void BackfillLegacySingleTenantDistribution(CRMDbContext context, IAgencyRepository agencyRepository, IBrokerRepository brokerRepository, IOwnerTenantRepository ownerTenantRepository, ICompanyRepository companyRepository, ILegalEntityRepository legalEntityRepository, IContactRepository contactRepository, ILeadRepository leadRepository, IDealRepository dealRepository)
        {
            var legacyTenants = new[] { "TEN-001", "" };
            // Agencies: each agency gets a unique tenant + (if i > 8) a parent.
            // Only well-formed AG-#### ids are eligible; legacy rows that
            // somehow got stamped with a TEN-* or random AgencyId are left
            // for manual cleanup.
            var staleAgencies = agencyRepository.Query()
                .Where(a => legacyTenants.Contains(a.TenantId) && a.AgencyId.StartsWith("AG-"))
                .OrderBy(a => a.AgencyId)
                .ToList();
            for (int idx = 0; idx < staleAgencies.Count; idx++)
            {
                var a = staleAgencies[idx];
                var numeric = int.Parse(a.AgencyId.Replace("AG-", ""));
                a.TenantId = $"TEN-{numeric:0000}";
                if (numeric > 8)
                {
                    a.ParentAgencyId = numeric % 50 == 0 ? "AG-0001" : $"AG-{(numeric % 8) + 1:0000}";
                    a.IsTrial = numeric % 11 == 0;
                    if (a.IsTrial)
                    {
                        a.TrialStartDate = DateTime.UtcNow.AddDays(-7);
                        a.TrialEndDate = DateTime.UtcNow.AddDays(7);
                    }
                }
                if (string.IsNullOrWhiteSpace(a.OperationalStatus) || a.OperationalStatus == "Active")
                {
                    a.OperationalStatus = numeric % 7 == 0 ? "Review" : numeric % 11 == 0 ? "TrialPending" : "Active";
                }
            }
            context.SaveChanges();

            // Brokers: re-stamp by joining to their parent agency.
            var staleBrokers = brokerRepository.Query()
                .Where(b => legacyTenants.Contains(b.TenantId))
                .ToList();
            var agencyTenantLookup = agencyRepository.Query()
                .Select(a => new { a.AgencyId, a.TenantId })
                .ToDictionary(a => a.AgencyId, a => a.TenantId);
            foreach (var b in staleBrokers)
            {
                if (agencyTenantLookup.TryGetValue(b.AgencyId, out var t))
                    b.TenantId = t;
            }
            context.SaveChanges();

            // OwnerTenantRecords.PrimaryKey is OwnerTenantId (int). Spread
            // legacy TEN-001 rows across the new tenant distribution.
            var staleOwners = ownerTenantRepository.Query().Where(r => legacyTenants.Contains(r.TenantId)).ToList();
            foreach (var rec in staleOwners)
            {
                var slot = (Math.Abs(rec.OwnerTenantId) % 1000) + 1;
                rec.TenantId = $"TEN-{slot:0000}";
            }
            context.SaveChanges();

            // CRMCompany / LegalEntity / Contact use string PKs. Parse trailing
            // digits from the id (COM-0042 → 42) and assign the matching tenant.
            int ParseTrailingInt(string id)
            {
                if (string.IsNullOrEmpty(id)) return 0;
                int n = 0;
                for (int k = id.Length - 1; k >= 0 && k > id.Length - 8; k--)
                {
                    if (char.IsDigit(id[k])) n = n * 10 + (id[k] - '0');
                    else break;
                }
                return n == 0 ? Math.Abs(id.GetHashCode()) : n;
            }
            void DistributeByStringPk<TEntity>(System.Collections.Generic.IEnumerable<TEntity> rows, System.Func<TEntity, string> idSelector, System.Action<TEntity, string> setTenant) where TEntity : class
            {
                foreach (var row in rows)
                {
                    var id = idSelector(row) ?? "";
                    if (string.IsNullOrEmpty(id)) continue;
                    var n = ParseTrailingInt(id);
                    var slot = (n % 1000) + 1;
                    setTenant(row, $"TEN-{slot:0000}");
                }
            }
            var staleCompanies = companyRepository.Query().Where(c => legacyTenants.Contains(c.TenantId)).ToList();
            DistributeByStringPk(staleCompanies, c => c.CompanyId, (c, t) => c.TenantId = t);
            context.SaveChanges();
            var staleLegal = legalEntityRepository.Query().Where(l => legacyTenants.Contains(l.TenantId)).ToList();
            DistributeByStringPk(staleLegal, l => l.LegalEntityId, (l, t) => l.TenantId = t);
            context.SaveChanges();
            var staleContacts = contactRepository.Query().Where(c => legacyTenants.Contains(c.TenantId)).ToList();
            DistributeByStringPk(staleContacts, c => c.ContactId, (c, t) => c.TenantId = t);
            context.SaveChanges();

            // Leads + Deals inherit the broker's tenant. (Legacy AllocatedToAgent
            // field was removed; the brokerTenantLookup is retained for any
            // pre-migration rows that still need tenant stamping.)
            var brokerTenantLookup2 = brokerRepository.Query()
                .Where(b => !string.IsNullOrEmpty(b.FullName))
                .Select(b => new { b.FullName, b.TenantId })
                .GroupBy(x => x.FullName)
                .Select(g => g.First())
                .ToDictionary(x => x.FullName, x => x.TenantId);
            var staleLeads = leadRepository.Query().Where(l => legacyTenants.Contains(l.TenantId)).ToList();
            foreach (var l in staleLeads)
            {
                l.TenantId = $"TEN-{(Math.Abs(l.LeadId) % 1000) + 1:0000}";
            }
            context.SaveChanges();
            var staleDeals = dealRepository.Query().Where(d => legacyTenants.Contains(d.TenantId)).ToList();
            foreach (var d in staleDeals)
            {
                d.TenantId = $"TEN-{(Math.Abs(d.DealId) % 1000) + 1:0000}";
            }
            context.SaveChanges();

            // 🧬 One-shot backfill: deals and leads that pre-date the
            // commission-on-every-row policy need CommissionType/Value
            // stamped now so reports actually surface numbers. Idempotent —
            // any row that already has a non-zero CommissionValue is left
            // untouched.
            var bareDeals = dealRepository.Query()
                .Where(d => d.CommissionValue == 0m || d.CommissionType == string.Empty)
                .ToList();
            foreach (var d in bareDeals)
            {
                var i = d.DealId;
                d.CommissionType = i % 10 < 7 ? CommissionTypes.Percent : CommissionTypes.Amount;
                d.CommissionValue = i % 10 < 7
                    ? 3m + (decimal)(i % 5)
                    : 25000m + (i * 750m);
            }
            if (bareDeals.Count > 0) context.SaveChanges();

            var bareLeads = leadRepository.Query()
                .Where(l => l.CommissionValue == 0m || l.CommissionType == string.Empty)
                .ToList();
            foreach (var l in bareLeads)
            {
                var i = l.LeadId;
                l.CommissionType = i % 10 < 7 ? CommissionTypes.Percent : CommissionTypes.Amount;
                l.CommissionValue = i % 10 < 7
                    ? 3m + (decimal)(i % 5)
                    : 25000m + (i * 750m);
            }
            if (bareLeads.Count > 0) context.SaveChanges();
        }
    }
}
