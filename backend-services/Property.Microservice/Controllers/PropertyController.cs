using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.Models;
using Property.Microservice.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace Property.Microservice.Controllers
{
    [ApiController]
    [Route("api/properties")]
    public class PropertyController : ControllerBase
    {
        private readonly PropertyDbContext _context;
        private readonly KafkaPublisherService _kafkaPublisher;
        private readonly string _storageRootPath;

        // 🧷 Unit document metadata is persisted as JSON in UnitDocumentsJson. ASP.NET Core
        // serializes the API surface in camelCase, so we serialize this column with camelCase
        // too — otherwise the frontend would receive PascalCase keys (Name/StoredName) that
        // don't match its camel-typed bindings (name/storedName).
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public PropertyController(PropertyDbContext context, KafkaPublisherService kafkaPublisher)
        {
            _context = context;
            _kafkaPublisher = kafkaPublisher;

            // 📁 LOCAL DISK VAULT: actual property media (images + documents) are persisted
            // to the server filesystem here so they survive beyond a single request, not
            // just as ephemeral URL references.
            _storageRootPath = Path.Combine(AppContext.BaseDirectory, "PropertyMediaVault");
            if (!Directory.Exists(_storageRootPath))
            {
                Directory.CreateDirectory(_storageRootPath);
            }
        }

        // --- 📥 DATA TRANSFER INTERFACE MODEL BINDINGS ---
                                           
         
                                                                             
                                                                                                                 
                                             
                                             
                                                                                                                        
                                              
                                              
         

                                       
         
                                                                
                                                                        
         

                                             
         
                                               
                                               
                                                     
         

                                              
         
                                     
                                                                  
                                                                       
                                                           
                                                                 
                                                               
                                                               
         

                                              
         
                                                                                            
                                                                       
                                                           
                                                                 
                                                               
                                                               
         

                                          
         
                                                                                                              
                                                                                                                 
                                                                                                                    
                                                                   
                                                                                                                                            
         

                                 
         
                                                            
                                                                   
                                                                                                            
                                                                                                        
                                                                   
                                               
                                                    
         

                                          
         
                                                                       
                                                                      
                                                                   
                                                                            
                                                             
                                                        
                                                       
                                                   
                                                        
                                                                  
                                                     
                                                                         
                                                              
                                                              
                                                                  
                                                                                                                  
         

        // The property editor uses the same fields as creation.  Keeping this as an
        // explicit DTO prevents clients from changing tenant and audit fields.
                                                                      

        // --- 📋 ENDPOINT: GET ALL SYNDICATION PLATFORMS ---
        [HttpGet("platforms")]
        public async Task<IActionResult> GetPlatforms()
        {
            var platforms = await _context.ExternalPlatforms.ToListAsync();
            return Ok(platforms);
        }

        // --- 🔍 ENDPOINT: GET SINGLE PROPERTY WITH LINKED SUB-UNITS ---
        [Authorize]
        [HttpGet("{propertyId}")]
        public async Task<IActionResult> GetPropertyById(int propertyId)
        {
            var role = GetCurrentCanonicalRole();
            var userTenantId = User.FindFirst("TenantId")?.Value;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userTenantId)) return Forbid();

            var query = _context.PropertyAssets
                .Include(p => p.PropertyTypeDetails)
                .Include(p => p.SubUnits)
                .Include(p => p.SyndicationLinks)
                    .ThenInclude(s => s.PlatformDetails)
                .Where(p => p.PropertyId == propertyId);

            query = ApplyPropertyVisibility(query);

            var property = await query.FirstOrDefaultAsync();
            if (property == null) return NotFound(new { message = "Target property record not found." });

            // A Tenant only sees their leased unit, never sibling units of the same property.
            if (role == "Tenant" && !string.IsNullOrWhiteSpace(userId))
            {
                var leasedUnitIds = await GetLeasedUnitIdsAsync(userId, userTenantId);
                property.SubUnits = property.SubUnits
                    .Where(u => leasedUnitIds.Contains(u.PropertyUnitId))
                    .ToList();
                if (!property.SubUnits.Any())
                    return NotFound(new { message = "Target property record not found." });
            }

            return Ok(property);
        }

        // --- 🏢 ENDPOINT: GET SUB-UNITS FOR A PROPERTY ---
        [Authorize]
        [HttpGet("{propertyId}/units")]
        public async Task<IActionResult> GetPropertyUnits(int propertyId)
        {
            // 🛡️ Reuse the unit resolver so only units the role may see are returned
            // (PlatformAdmin/AgencyAdmin/Broker => all tenant units; Owner => owned property;
            //  Tenant => only their leased unit).
            var role = GetCurrentCanonicalRole();
            var userTenantId = User.FindFirst("TenantId")?.Value;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userTenantId)) return Forbid();

            var propertyQuery = _context.PropertyAssets.Where(p => p.PropertyId == propertyId);
            if (role != "PlatformAdmin") propertyQuery = propertyQuery.Where(p => p.TenantId == userTenantId);
            if (role == "Owner" && !string.IsNullOrWhiteSpace(userId))
                propertyQuery = propertyQuery.Where(p => p.LinkedOwnerId == userId);
            if (!await propertyQuery.AnyAsync()) return NotFound(new { message = "Target property record not found." });

            var query = _context.PropertyUnits.Where(u => u.PropertyId == propertyId);
            if (role == "Tenant" && !string.IsNullOrWhiteSpace(userId))
            {
                var leasedUnitIds = await GetLeasedUnitIdsAsync(userId, userTenantId!);
                query = query.Where(u => leasedUnitIds.Contains(u.PropertyUnitId));
            }

            var units = await query.OrderBy(u => u.UnitNumber).ToListAsync();
            return Ok(units);
        }

        // --- ➕ ENDPOINT: ADD SUB-UNIT TO PROPERTY ---
        [Authorize]
        [HttpPost("{propertyId}/units")]
        public async Task<IActionResult> AddPropertyUnit(int propertyId, [FromBody] CreatePropertyUnitPayload payload)
        {
            // 🛡️ Tenants and Owners may never add (or delete) units — that is an agency-level
            // action. Any authenticated user outside a read-only role is permitted.
            if (IsReadOnlyRole()) return Forbid();

            var userTenantId = User.FindFirst("TenantId")?.Value;
            if (string.IsNullOrWhiteSpace(userTenantId)) return Forbid();
            var role = GetCurrentCanonicalRole();
            if (role != "PlatformAdmin")
            {
                var property = await _context.PropertyAssets
                    .FirstOrDefaultAsync(p => p.PropertyId == propertyId && p.TenantId == userTenantId);
                if (property == null) return NotFound(new { message = "Target property record not found." });
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.UnitNumber))
            {
                return BadRequest(new { message = "Unit number is required." });
            }

            var unit = new PropertyUnit
            {
                PropertyId = propertyId,
                UnitNumber = payload.UnitNumber,
                UnitDescription = payload.UnitDescription ?? string.Empty,
                NetRentableAreaSqM = payload.NetRentableAreaSqM,
                SpecificUnitRentalPrice = payload.SpecificUnitRentalPrice,
                SpecificUnitSalePrice = payload.SpecificUnitSalePrice,
                IsCurrentlyVacant = payload.IsCurrentlyVacant,
                UnitImagesJson = "[]",
                CreatedAt = DateTime.UtcNow
            };

            _context.PropertyUnits.Add(unit);
            await _context.SaveChangesAsync();

            return Ok(unit);
        }

        // --- ❌ ENDPOINT: DELETE SUB-UNIT FROM PROPERTY ---
        [Authorize]
        [HttpDelete("{propertyId}/units/{unitId}")]
        public async Task<IActionResult> DeletePropertyUnit(int propertyId, int unitId)
        {
            // 🛡️ Tenants and Owners may never delete anything.
            if (IsReadOnlyRole()) return Forbid();

            var unit = await GetAccessibleUnitAsync(propertyId, unitId);
            if (unit == null)
            {
                if (string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value)) return Forbid();
                return NotFound(new { message = "Property unit not found." });
            }

            _context.PropertyUnits.Remove(unit);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Unit successfully unlinked and deleted from property." });
        }

        // --- ✏️ ENDPOINT: UPDATE UNIT DETAILS (editable unit on property page) ---
        [Authorize]
        [HttpPut("{propertyId}/units/{unitId:int}")]
        public async Task<IActionResult> UpdatePropertyUnit(int propertyId, int unitId, [FromBody] UpdatePropertyUnitPayload payload)
        {
            var unit = await GetAccessibleUnitAsync(propertyId, unitId);
            if (unit == null)
            {
                if (string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value)) return Forbid();
                return NotFound(new { message = "Target property or unit not found." });
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.UnitNumber))
                return BadRequest(new { message = "Unit number is required." });

            unit.UnitNumber = payload.UnitNumber;
            unit.UnitDescription = payload.UnitDescription ?? string.Empty;
            unit.NetRentableAreaSqM = payload.NetRentableAreaSqM;
            unit.SpecificUnitRentalPrice = payload.SpecificUnitRentalPrice;
            unit.SpecificUnitSalePrice = payload.SpecificUnitSalePrice;
            unit.IsCurrentlyVacant = payload.IsCurrentlyVacant;

            await _context.SaveChangesAsync();
            return Ok(unit);
        }

        // --- 📎 ENDPOINT: UPLOAD A DOCUMENT TO A UNIT (saved to server disk + metadata persisted) ---
        [Authorize]
        [HttpPost("{propertyId}/units/{unitId:int}/documents")]
        public async Task<IActionResult> UploadPropertyUnitDocument(int propertyId, int unitId, [FromBody] DocumentUploadPayload payload)
        {
            var unit = await GetAccessibleUnitAsync(propertyId, unitId);
            if (unit == null)
            {
                if (string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value)) return Forbid();
                return NotFound(new { message = "Target property or unit not found." });
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.Name) || string.IsNullOrEmpty(payload.Base64DataString))
                return BadRequest(new { message = "Document name and file data are required." });

            byte[] docBytes;
            try { docBytes = Convert.FromBase64String(payload.Base64DataString); }
            catch { return BadRequest(new { message = "Corrupt document payload could not be decoded." }); }
            if (docBytes.Length == 0) return BadRequest(new { message = "Uploaded document is empty." });

            // Sanitize the original name and generate a unique on-disk name to avoid collisions / traversal.
            string safeOrigName = Path.GetFileName(payload.FileName ?? payload.Name) ?? "document";
            string ext = Path.GetExtension(safeOrigName);
            string storedName = $"{Guid.NewGuid():N}{ext}";

            string unitFolder = Path.Combine(_storageRootPath, $"Property_{propertyId}", $"Unit_{unitId}", "Documents");
            Directory.CreateDirectory(unitFolder);
            string finalPath = Path.Combine(unitFolder, storedName);
            await System.IO.File.WriteAllBytesAsync(finalPath, docBytes);

            var docs = ParseUnitDocuments(unit.UnitDocumentsJson);
            var meta = new DocumentMeta
            {
                Name = payload.Name,
                Description = payload.Description,
                FileName = safeOrigName,
                StoredName = storedName,
                ContentType = payload.ContentType,
                SizeBytes = docBytes.Length,
                UploadedAt = DateTime.UtcNow
            };
            docs.Add(meta);
            unit.UnitDocumentsJson = JsonSerializer.Serialize(docs, JsonOptions);
            await _context.SaveChangesAsync();

            return Ok(meta);
        }

        // --- 📄 ENDPOINT: DOWNLOAD / VIEW A UNIT DOCUMENT ---
        [Authorize]
        [HttpGet("{propertyId}/units/{unitId:int}/documents/{storedName}")]
        public async Task<IActionResult> DownloadPropertyUnitDocument(int propertyId, int unitId, string storedName)
        {
            var unit = await GetAccessibleUnitAsync(propertyId, unitId);
            if (unit == null)
            {
                if (string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value)) return Forbid();
                return NotFound(new { message = "Target property or unit not found." });
            }

            var docs = ParseUnitDocuments(unit.UnitDocumentsJson);
            var doc = docs.FirstOrDefault(d => d.StoredName == storedName);
            if (doc == null) return NotFound(new { message = "Document record not found." });

            string filePath = Path.Combine(_storageRootPath, $"Property_{propertyId}", $"Unit_{unitId}", "Documents", storedName);
            if (!System.IO.File.Exists(filePath)) return NotFound(new { message = "Document file is missing from server disk." });

            byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            // Serve with the original file name so downloads keep a friendly name.
            return File(fileBytes, string.IsNullOrEmpty(doc.ContentType) ? "application/octet-stream" : doc.ContentType, doc.FileName);
        }

        // --- 📋 ENDPOINT: LIST A UNIT'S STORED DOCUMENTS (read-only; role-scoped via unit resolver) ---
        [Authorize]
        [HttpGet("{propertyId}/units/{unitId:int}/documents")]
        public async Task<IActionResult> ListUnitDocuments(int propertyId, int unitId)
        {
            var unit = await GetAccessibleUnitAsync(propertyId, unitId);
            if (unit == null)
            {
                if (string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value)) return Forbid();
                return NotFound(new { message = "Target property or unit not found." });
            }

            var docs = ParseUnitDocuments(unit.UnitDocumentsJson);
            return Ok(docs);
        }

        // --- ❌ ENDPOINT: DELETE A UNIT DOCUMENT FROM THE VAULT ---
        [Authorize]
        [HttpDelete("{propertyId}/units/{unitId:int}/documents/{storedName}")]
        public async Task<IActionResult> DeletePropertyUnitDocument(int propertyId, int unitId, string storedName)
        {
            // 🛡️ Tenants and Owners may never delete anything (documents included).
            if (IsReadOnlyRole()) return Forbid();

            var unit = await GetAccessibleUnitAsync(propertyId, unitId);
            if (unit == null)
            {
                if (string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value)) return Forbid();
                return NotFound(new { message = "Target property or unit not found." });
            }

            var docs = ParseUnitDocuments(unit.UnitDocumentsJson);
            var doc = docs.FirstOrDefault(d => d.StoredName == storedName);
            if (doc == null) return NotFound(new { message = "Document record not found." });

            string filePath = Path.Combine(_storageRootPath, $"Property_{propertyId}", $"Unit_{unitId}", "Documents", storedName);
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);

            docs.Remove(doc);
            unit.UnitDocumentsJson = JsonSerializer.Serialize(docs);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Document successfully removed from unit vault." });
        }

        // --- 🛡️ SHARED: role/tenant/lease-scoped unit resolver ---
        // Enforces the visibility matrix end-to-end:
        //   • PlatformAdmin       -> any unit, any tenant
        //   • AgencyAdmin / Broker-> any unit within the caller's tenant
        //   • Owner               -> only units belonging to a property they own (LinkedOwnerId)
        //   • Tenant              -> only the unit they currently have an ACTIVE lease on
        private async Task<PropertyUnit?> GetAccessibleUnitAsync(int propertyId, int unitId)
        {
            var role = GetCurrentCanonicalRole();
            var userTenantId = User.FindFirst("TenantId")?.Value;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userTenantId)) return null;

            var isPlatformAdmin = role == "PlatformAdmin";
            var propertyQuery = _context.PropertyAssets.Where(p => p.PropertyId == propertyId);
            if (!isPlatformAdmin) propertyQuery = propertyQuery.Where(p => p.TenantId == userTenantId);

            // Owner may only touch units under a property they actually own.
            if (role == "Owner" && !string.IsNullOrWhiteSpace(userId))
                propertyQuery = propertyQuery.Where(p => p.LinkedOwnerId == userId);

            var propertyExists = await propertyQuery.AnyAsync();
            if (!propertyExists) return null;

            var unit = await _context.PropertyUnits.FirstOrDefaultAsync(u => u.PropertyUnitId == unitId && u.PropertyId == propertyId);
            if (unit == null) return null;

            // A Tenant may only address the specific unit they currently lease.
            if (role == "Tenant")
            {
                var isLeased = await _context.UnitLeases.AnyAsync(l =>
                    l.UserId == userId && l.TenantId == userTenantId && l.PropertyUnitId == unitId &&
                    l.PropertyId == propertyId && l.IsActive && l.LeaseEnd >= DateTime.UtcNow);
                if (!isLeased) return null;
            }

            return unit;
        }

        // Resolve the caller's canonical role, normalizing legacy seed aliases
        // (SuperAdmin/Manager/Agent/Admin) to the advertised 5-role contract.
        private string? GetCurrentCanonicalRole()
        {
            var role = User.FindFirst(c =>
                c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
                ?? User.FindFirst(ClaimTypes.Role)?.Value;
            return role switch
            {
                "SuperAdmin" => "PlatformAdmin",
                "Manager" => "AgencyAdmin",
                "Admin" => "AgencyAdmin",
                "Agent" => "Broker",
                _ => role
            };
        }

        private static readonly System.Collections.Generic.HashSet<string> ReadOnlyRoles =
            new(System.StringComparer.OrdinalIgnoreCase) { "Tenant", "Owner" };

        // Tenants and Owners may never delete anything (units, documents, maintenance calls).
        private bool IsReadOnlyRole() => ReadOnlyRoles.Contains(GetCurrentCanonicalRole() ?? string.Empty);

        // Apply role + tenant scoping to a PropertyAsset queryable so search / look-ups
        // only surface properties a role is entitled to see.
        private IQueryable<PropertyAsset> ApplyPropertyVisibility(IQueryable<PropertyAsset> query)
        {
            var role = GetCurrentCanonicalRole();
            if (role == "PlatformAdmin") return query;

            var tenantId = User.FindFirst("TenantId")?.Value;
            if (string.IsNullOrWhiteSpace(tenantId)) return query.Where(p => false);
            query = query.Where(p => p.TenantId == tenantId);

            if (role == "Owner")
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(p => p.LinkedOwnerId == userId);
            }
            else if (role == "Tenant")
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var leasedPropertyIds = _context.UnitLeases
                    .Where(l => l.UserId == userId && l.TenantId == tenantId && l.IsActive && l.LeaseEnd >= DateTime.UtcNow)
                    .Select(l => l.PropertyId).ToList();
                query = query.Where(p => leasedPropertyIds.Contains(p.PropertyId));
            }
            return query;
        }

        private async Task<HashSet<int>> GetLeasedUnitIdsAsync(string userId, string tenantId)
        {
            return await _context.UnitLeases
                .Where(l => l.UserId == userId && l.TenantId == tenantId && l.IsActive && l.LeaseEnd >= DateTime.UtcNow)
                .Select(l => l.PropertyUnitId)
                .ToHashSetAsync();
        }

    private static List<DocumentMeta> ParseUnitDocuments(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]") return new List<DocumentMeta>();
        try { return JsonSerializer.Deserialize<List<DocumentMeta>>(json, JsonOptions) ?? new List<DocumentMeta>(); }
        catch { return new List<DocumentMeta>(); }
    }

        // --- 🏠 ENDPOINT: CREATE PROPERTY WITH OPTIONAL SUB-UNITS ---
        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateProperty([FromBody] CreatePropertyPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.CompleteAddress))
            {
                return BadRequest(new { message = "Property address is required." });
            }

            // 🛡️ Multi-tenant isolation: the owning TenantId is taken from the JWT, never
            // from the client payload, so a user cannot mint a property into another sandbox.
            var userTenantId = User.FindFirst("TenantId")?.Value;
            if (string.IsNullOrWhiteSpace(userTenantId)) return Forbid();
            if (GetCurrentCanonicalRole() == "Tenant" || GetCurrentCanonicalRole() == "Owner")
                return Forbid();

            var newProp = new PropertyAsset
            {
                TenantId = userTenantId,
                CompleteAddress = payload.CompleteAddress,
                ErfSpatialSize = payload.ErfSpatialSize ?? "200 m²",
                TitleDeedType = payload.TitleDeedType ?? "Freehold",
                PortfolioZoning = payload.PortfolioZoning ?? "Commercial Zone",
                MarketClass = payload.MarketClass ?? "Sale",
                PropertyTypeId = payload.PropertyTypeId > 0 ? payload.PropertyTypeId : 1,
                ValuationPrice = payload.ValuationPrice,
                SalePrice = payload.SalePrice,
                BaseRentalRate = payload.BaseRentalRate,
                RentEscalationPercentage = payload.RentEscalationPercentage,
                LeaseTermMonths = payload.LeaseTermMonths,
                TenantInstallationAllowance = payload.TenantInstallationAllowance,
                Latitude = payload.Latitude != 0 ? payload.Latitude : -33.9249m,
                Longitude = payload.Longitude != 0 ? payload.Longitude : 18.4241m,
                LinkedOwnerId = payload.LinkedOwnerId ?? "OT-0001",
                VerificationDocumentsJson = "[]",
                CreatedAt = DateTime.UtcNow
            };

            if (payload.SubUnits != null && payload.SubUnits.Any())
            {
                foreach (var unitPayload in payload.SubUnits)
                {
                    newProp.SubUnits.Add(new PropertyUnit
                    {
                        UnitNumber = unitPayload.UnitNumber,
                        UnitDescription = unitPayload.UnitDescription ?? string.Empty,
                        NetRentableAreaSqM = unitPayload.NetRentableAreaSqM,
                        SpecificUnitRentalPrice = unitPayload.SpecificUnitRentalPrice,
                        SpecificUnitSalePrice = unitPayload.SpecificUnitSalePrice,
                        IsCurrentlyVacant = unitPayload.IsCurrentlyVacant,
                        UnitImagesJson = "[]",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            _context.PropertyAssets.Add(newProp);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPropertyById), new { propertyId = newProp.PropertyId }, newProp);
        }

        [Authorize]
        [HttpPut("{propertyId}")]
        public async Task<IActionResult> UpdateProperty(int propertyId, [FromBody] UpdatePropertyPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.CompleteAddress))
                return BadRequest(new { message = "Property address is required." });

            // 🛡️ Resolve the caller's multi-tenant scope from the JWT claims.
            // The "TenantId" claim is injected by Identity.Microservice on every issued token.
            var userTenantId = User.FindFirst("TenantId")?.Value;
            if (string.IsNullOrWhiteSpace(userTenantId))
                return Forbid();

            // Platform administrators operate across every tenant sandbox, so they
            // bypass the tenant filter. Both role aliases are honored because the
            // Identity microservice seeds the root admin as "SuperAdmin" while the
            // model/register contract advertises the registered role as "PlatformAdmin".
            var isPlatformAdmin = User.IsInRole("PlatformAdmin") || User.IsInRole("SuperAdmin");

            // 🧭 Tenant scoping guard: scope the lookup to the caller's tenant so an
            // authenticated user from tenant A can never read or mutate a property
            // that belongs to tenant B. A non-owner request resolves to "not found"
            // rather than revealing cross-tenant record existence.
            IQueryable<PropertyAsset> propertyQuery = _context.PropertyAssets
                .Include(p => p.PropertyTypeDetails)
                .Include(p => p.SubUnits);

            propertyQuery = isPlatformAdmin
                ? propertyQuery.Where(p => p.PropertyId == propertyId)
                : propertyQuery.Where(p => p.PropertyId == propertyId && p.TenantId == userTenantId);

            var property = await propertyQuery.FirstOrDefaultAsync();
            if (property == null)
                return NotFound(new { message = "Target property record not found." });

            property.CompleteAddress = payload.CompleteAddress;
            property.ErfSpatialSize = payload.ErfSpatialSize ?? string.Empty;
            property.TitleDeedType = payload.TitleDeedType ?? "Freehold";
            property.PortfolioZoning = payload.PortfolioZoning ?? string.Empty;
            property.MarketClass = payload.MarketClass ?? "Sale";
            property.PropertyTypeId = payload.PropertyTypeId > 0 ? payload.PropertyTypeId : property.PropertyTypeId;
            property.ValuationPrice = payload.ValuationPrice;
            property.SalePrice = payload.SalePrice;
            property.BaseRentalRate = payload.BaseRentalRate;
            property.RentEscalationPercentage = payload.RentEscalationPercentage;
            property.LeaseTermMonths = payload.LeaseTermMonths;
            property.TenantInstallationAllowance = payload.TenantInstallationAllowance;
            property.Latitude = payload.Latitude;
            property.Longitude = payload.Longitude;
            property.LinkedOwnerId = payload.LinkedOwnerId ?? string.Empty;

            // Re-assert the tenant scope on the entity so a malicious payload cannot
            // override or clear the TenantId isolation key during a cross-tenant attempt.
            property.TenantId = isPlatformAdmin
                ? property.TenantId
                : userTenantId;

            await _context.SaveChangesAsync();
            return Ok(await _context.PropertyAssets
                .Include(p => p.PropertyTypeDetails)
                .Include(p => p.SubUnits)
                .FirstAsync(p => p.PropertyId == propertyId));
        }
        // --- 🔍 ENDPOINT A: MULTI-CRITERIA REAL ESTATE SEARCH PIPELINE ---
        [Authorize]
        [HttpPost("search")]
        public async Task<IActionResult> SearchProperties([FromBody] PropertySearchCriteria criteria, [FromQuery] int page = 1)
        {
            if (page < 1) page = 1;
            const int PAGE_SIZE = 20;

            // Eagerly load relational structures (Property Type lookups and sub-units)
            var query = _context.PropertyAssets
                .Include(p => p.PropertyTypeDetails)
                .Include(p => p.SubUnits)
                .AsQueryable();

            // 🛡️ Role + tenant visibility scoping: a PlatformAdmin sees every property,
            // an Owner only their owned properties, a Tenant only active-leased properties,
            // and AgencyAdmin/Broker only their own tenant's properties.
            query = ApplyPropertyVisibility(query);

            // 1. Filter by target locations array matching text boundaries
            if (criteria.Locations != null && criteria.Locations.Any())
            {
                query = query.Where(p => criteria.Locations.Any(loc => p.CompleteAddress.Contains(loc)));
            }

            // 2. Filter by normalized Property Type lookups
            if (!string.Equals(criteria.PropertyType, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.PropertyTypeDetails != null && 
                                         p.PropertyTypeDetails.Name == criteria.PropertyType);
            }

            // 📈 Listing eligibility filter (a property may be listed for SALE and/or RENT).
            if (!string.Equals(criteria.ListingType, "All", StringComparison.OrdinalIgnoreCase))
            {
                if (criteria.ListingType.Equals("Sale", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => p.IsForSale);
                else if (criteria.ListingType.Equals("Rent", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => p.IsForRent);
            }

            // Price range filter (uses the active listing price; sale→SalePrice, rent→BaseRentalRate)
            if (criteria.MinPrice.HasValue)
                query = query.Where(p => (criteria.ListingType == "Rent" ? p.BaseRentalRate : p.SalePrice) >= criteria.MinPrice.Value);
            if (criteria.MaxPrice.HasValue)
                query = query.Where(p => (criteria.ListingType == "Rent" ? p.BaseRentalRate : p.SalePrice) <= criteria.MaxPrice.Value);

            // The UI submits area bounds in square metres.  Use linked unit area
            // where available, falling back to the text field only for display.
            if (criteria.MinSize.HasValue)
                query = query.Where(p => p.SubUnits.Any(u => u.NetRentableAreaSqM >= criteria.MinSize.Value));
            if (criteria.MaxSize.HasValue)
                query = query.Where(p => p.SubUnits.Any(u => u.NetRentableAreaSqM <= criteria.MaxSize.Value));

            var total = await query.CountAsync();
            var results = await query.OrderBy(p => p.PropertyId)
                .Skip((page - 1) * PAGE_SIZE).Take(PAGE_SIZE).ToListAsync();
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            return Ok(results);
        }

        // --- 🖼️ ENDPOINT B: KAFKA-BACKED IMAGE STREAMING UPLOADER ---
        [HttpPost("{propertyId}/upload-image")]
        public async Task<IActionResult> UploadPropertyImage(int propertyId, [FromBody] ImageUploadPayload payload)
        {
            var property = await _context.PropertyAssets.FindAsync(propertyId);
            if (property == null) return NotFound(new { message = "Target property record not found." });

            if (payload == null || string.IsNullOrEmpty(payload.Base64DataString))
            {
                return BadRequest(new { message = "Invalid picture data payload." });
            }

            // Construct transactional message payload envelope
            string messageValue = $"{{\"PropertyId\":{propertyId},\"FileName\":\"{payload.FileName}\",\"Base64Data\":\"{payload.Base64DataString}\"}}";
            
            // Dispatch asynchronously directly onto your Kafka clusters media pipeline
            await _kafkaPublisher.PublishMessageAsync("property-images", propertyId.ToString(), messageValue);

            return Ok(new { message = "Media payload successfully dispatched to Kafka event streams for optimization." });
        }
        // --- 🔗 ENDPOINT C: PORTAL SYNDICATION CHECKBOX TRACKER TOGGLE ---
        [HttpPost("syndicate")]
        public async Task<IActionResult> ToggleSyndication([FromBody] SyndicationTogglePayload payload)
        {
            if (payload == null) return BadRequest(new { message = "Invalid syndication data envelope." });

            var property = await _context.PropertyAssets
                .Include(p => p.PropertyTypeDetails)
                .FirstOrDefaultAsync(p => p.PropertyId == payload.PropertyId);

            if (property == null) return NotFound(new { message = "Target property record not found." });

            var platform = await _context.ExternalPlatforms.FindAsync(payload.PlatformId);
            if (platform == null) return NotFound(new { message = "Target external marketing channel portal not found." });

            // Fetch or create the portal links ledger row matching current identifiers
            var syndicationRecord = await _context.PropertyPlatformSyndications
                .FirstOrDefaultAsync(s => s.PropertyId == payload.PropertyId && s.PlatformId == payload.PlatformId);

            if (syndicationRecord == null)
            {
                syndicationRecord = new PropertyPlatformSyndication
                {
                    PropertyId = payload.PropertyId,
                    PlatformId = payload.PlatformId,
                    IsSyndicatedActive = payload.SyndicateStatus,
                    ExternalReferenceIndex = "EXT-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
                    LivePortalListingUrl = $"{platform.BaseUrlAddress}/listing/prop-{payload.PropertyId}",
                    LastSynchronizedAt = DateTime.UtcNow
                };
                _context.PropertyPlatformSyndications.Add(syndicationRecord);
            }
            else
            {
                syndicationRecord.IsSyndicatedActive = payload.SyndicateStatus;
                syndicationRecord.LastSynchronizedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Dispatch message payload back onto your active Kafka data synchronization topic channels
            string activeState = payload.SyndicateStatus ? "true" : "false";
            string kafkaSyncValue = $"{{\"PropertyId\":{payload.PropertyId},\"PlatformId\":{payload.PlatformId},\"IsActive\":{activeState}}}";
            
            await _kafkaPublisher.PublishMessageAsync("platform-syndication", payload.PropertyId.ToString(), kafkaSyncValue);

            return Ok(new
            {
                message = payload.SyndicateStatus 
                    ? $"Asset successfully pushed out to {platform.PlatformName} tracking registers." 
                    : $"Asset successfully de-listed from {platform.PlatformName} tracking networks.",
                externalRef = syndicationRecord.ExternalReferenceIndex,
                // Maps directly back to the property used in legacy integrations to satisfy compilation requirements
                directPropertyUrl = syndicationRecord.LivePortalListingUrl 
            });
        }

        [HttpPost("brochure/generate")]
        public async Task<IActionResult> GenerateBrochure([FromBody] GeneratedBrochurePayload payload)
        {
            if (payload == null || payload.PropertyId <= 0 || payload.DealId <= 0)
                return BadRequest(new { message = "A deal and property are required to generate a brochure." });

            var propertyExists = await _context.PropertyAssets.AnyAsync(p => p.PropertyId == payload.PropertyId);
            if (!propertyExists) return NotFound(new { message = "Target property record not found." });

            await _kafkaPublisher.PublishMessageAsync(
                "brochure-generation-requests",
                payload.DealId.ToString(),
                System.Text.Json.JsonSerializer.Serialize(payload));
            return Accepted(new { message = "Brochure generation request queued." });
        }

    // --- 🛠️ ENDPOINTS: PROPERTY-UNIT MAINTENANCE CALLS (tenant-logged) ---
    // A Tenant may create a maintenance call only against a unit they currently lease;
    // an Owner/Broker/AgencyAdmin may create one for units in their scope. Tenants and
    // Owners may never delete anything. A dedicated payload DTO is used so the
    // server-derived TenantId/RequestedByUserId are never validated against missing
    // client input.
                                       
     
                                                         
                                                               
                                              
     

    [Authorize]
    [HttpGet("{propertyId}/units/{unitId:int}/maintenance")]
    public async Task<IActionResult> GetUnitMaintenanceCalls(int propertyId, int unitId)
    {
        var unit = await GetAccessibleUnitAsync(propertyId, unitId);
        if (unit == null)
        {
            if (string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value)) return Forbid();
            return NotFound(new { message = "Target property or unit not found." });
        }

        var calls = await _context.MaintenanceCalls
            .Where(m => m.PropertyId == propertyId && m.PropertyUnitId == unitId)
            .OrderByDescending(m => m.UpdatedAt)
            .ToListAsync();
        return Ok(calls);
    }

    [Authorize]
    [HttpPost("{propertyId}/units/{unitId:int}/maintenance")]
    public async Task<IActionResult> AddUnitMaintenanceCall(int propertyId, int unitId, [FromBody] MaintenanceCallPayload payload)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.Title))
            return BadRequest(new { message = "A maintenance call title is required." });

        var unit = await GetAccessibleUnitAsync(propertyId, unitId);
        if (unit == null)
        {
            if (string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value)) return Forbid();
            return NotFound(new { message = "Target property or unit not found." });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantId = User.FindFirst("TenantId")?.Value;

        var call = new MaintenanceCall
        {
            TenantId = tenantId ?? string.Empty,
            RequestedByUserId = userId ?? string.Empty,
            PropertyId = propertyId,
            PropertyUnitId = unitId,
            Title = payload.Title,
            Description = payload.Description ?? string.Empty,
            Status = "Open",
            DueDate = payload.DueDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.MaintenanceCalls.Add(call);
        await _context.SaveChangesAsync();
        return Ok(call);
    }

    [Authorize]
    [HttpDelete("{propertyId}/units/{unitId:int}/maintenance/{callId:int}")]
    public async Task<IActionResult> DeleteMaintenanceCall(int callId)
    {
        // Tenants and Owners may never delete anything.
        if (IsReadOnlyRole()) return Forbid();

        var call = await _context.MaintenanceCalls.FindAsync(callId);
        if (call == null) return NotFound(new { message = "Maintenance call not found." });

        // Tenant isolation: admins only act within their tenant.
        var tenantId = User.FindFirst("TenantId")?.Value;
        if (GetCurrentCanonicalRole() != "PlatformAdmin" && call.TenantId != tenantId)
            return Forbid();

            _context.MaintenanceCalls.Remove(call);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Maintenance call resolved and removed." });
        }
    }
}
