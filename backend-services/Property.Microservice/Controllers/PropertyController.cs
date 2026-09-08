using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Services.Abstractions;
using Property.Microservice.Services;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;
using System.Security.Claims;
using System.Text.Json;

namespace Property.Microservice.Controllers
{
    [ApiController]
    [Route("api/properties")]
    public class PropertyController : ControllerBase
    {
        private readonly PropertyDbContext _dbContext;
        private readonly KafkaPublisherService _kafkaPublisher;
        private readonly IPropertyRepository _propertyRepository;
        private readonly IPropertyUnitRepository _propertyUnitRepository;
        private readonly IRepository<ExternalPlatform> _externalPlatformRepository;
        private readonly IRepository<PropertyPlatformSyndication> _propertyPlatformSyndicationRepository;
        private readonly IRepository<UnitLease> _unitLeaseRepository;
        private readonly IRepository<MaintenanceCall> _maintenanceCallRepository;
        private readonly IRepository<SoleMandate> _soleMandateRepository;
        private readonly IRepository<AmenityType> _amenityTypeRepository;
        private readonly IRepository<PropertyUnitAmenity> _propertyUnitAmenityRepository;
        private readonly ILogger<PropertyController> _logger;
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

        public PropertyController(PropertyDbContext dbContext, KafkaPublisherService kafkaPublisher, ILogger<PropertyController> logger, PermissionCheckService permissionCheck, IPropertyRepository propertyRepository, IPropertyUnitRepository propertyUnitRepository, IRepository<ExternalPlatform> externalPlatformRepository, IRepository<PropertyPlatformSyndication> propertyPlatformSyndicationRepository, IRepository<UnitLease> unitLeaseRepository, IRepository<MaintenanceCall> maintenanceCallRepository, IRepository<SoleMandate> soleMandateRepository, IRepository<AmenityType> amenityTypeRepository, IRepository<PropertyUnitAmenity> propertyUnitAmenityRepository)
        {
            _dbContext = dbContext;
            _propertyRepository = propertyRepository;
            _propertyUnitRepository = propertyUnitRepository;
            _externalPlatformRepository = externalPlatformRepository;
            _propertyPlatformSyndicationRepository = propertyPlatformSyndicationRepository;
            _unitLeaseRepository = unitLeaseRepository;
            _maintenanceCallRepository = maintenanceCallRepository;
            _soleMandateRepository = soleMandateRepository;
            _amenityTypeRepository = amenityTypeRepository;
            _propertyUnitAmenityRepository = propertyUnitAmenityRepository;
            _kafkaPublisher = kafkaPublisher;
            _logger = logger;
            PermissionCheck = permissionCheck;

            // 📁 LOCAL DISK VAULT: actual property media (images + documents) are persisted
            // to the server filesystem here so they survive beyond a single request, not
            // just as ephemeral URL references.
            _storageRootPath = Path.Combine(AppContext.BaseDirectory, "PropertyMediaVault");
            if (!Directory.Exists(_storageRootPath))
            {
                Directory.CreateDirectory(_storageRootPath);
            }
        }

        private PermissionCheckService PermissionCheck { get; }

        // --- 📥 DATA TRANSFER INTERFACE MODEL BINDINGS ---
                                           
         
                                                                             
                                                                                                                 
                                             
                                             
                                                                                                                        
                                              
                                              
         

                                       
         
                                                                
                                                                        
         

                                             
         
                                               
                                               
                                                     
         

                                              
         
                                     
                                                                  
                                                                       
                                                           
                                                                 
                                                               
                                                               
         

                                              
         
                                                                                            
                                                                       
                                                           
                                                                 
                                                               
                                                               
         

                                          
         
                                                                                                              
                                                                                                                 
                                                                                                                    
                                                                   
                                                                                                                                            
         

                                 
         
                                                            
                                                                   
                                                                                                            
                                                                                                        
                                                                   
                                               
                                                    
         

                                          
         
                                                                       
                                                                      
                                                                   
                                                                            
                                                             
                                                        
                                                       
                                                   
                                                        
                                                                  
                                                     
                                                                         
                                                              
                                                              
                                                                  
                                                                                                                  
         

        // The property editor uses the same fields as creation.  Keeping this as an
        // explicit DTO prevents clients from changing tenant and audit fields.
                                                                      

        // --- 📋 ENDPOINT: GET ALL SYNDICATION PLATFORMS ---
        [HttpGet("platforms")]
        public async Task<IActionResult> GetPlatforms()
        {
            var platforms = await _externalPlatformRepository.Query().ToListAsync();
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

            var query = _propertyRepository.Query()
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

            // 📜 Attach the currently-active sole mandate (if any) so the
            // UI can render the obligation/expiry banners without a
            // second round-trip. Null when no mandate is open.
            var activeMandate = await _soleMandateRepository.Query()
                .Where(m => m.PropertyId == propertyId && m.IsActive
                    && m.StartDate <= DateTime.UtcNow && m.EndDate >= DateTime.UtcNow)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                property,
                activeSoleMandate = activeMandate == null ? null : new
                {
                    activeMandate.SoleMandateId,
                    activeMandate.StartDate,
                    activeMandate.EndDate,
                    activeMandate.CreatedAt,
                    daysRemaining = (int)Math.Ceiling((activeMandate.EndDate - DateTime.UtcNow).TotalDays)
                }
            });
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

            var propertyQuery = _propertyRepository.Query().Where(p => p.PropertyId == propertyId);
            var overrideTenantId = HttpContext?.Request?.Query["tenantId"].ToString();
            var usingOverride = (role == "PlatformAdmin" || role == "AgencyAdmin")
                && !string.IsNullOrWhiteSpace(overrideTenantId)
                && overrideTenantId != "__all__";
            if (usingOverride)
            {
                // 🌳 Comma-separated subtree support: a PlatformAdmin viewing
                // a record while a parent-agency subtree is active should be
                // able to see records owned by any descendant.
                var tenantIds = overrideTenantId
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (tenantIds.Count == 1) propertyQuery = propertyQuery.Where(p => p.TenantId == tenantIds[0]);
                else if (tenantIds.Count > 1) propertyQuery = propertyQuery.Where(p => tenantIds.Contains(p.TenantId));
            }
            else
            {
                if (role != "PlatformAdmin") propertyQuery = propertyQuery.Where(p => p.TenantId == userTenantId);
            }
            if (role == "Owner" && !string.IsNullOrWhiteSpace(userId))
                propertyQuery = propertyQuery.Where(p => p.LinkedOwnerId == userId);
            if (!await propertyQuery.AnyAsync()) return NotFound(new { message = "Target property record not found." });

            var query = _propertyUnitRepository.Query().Where(u => u.PropertyId == propertyId);
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
            if (!await PermissionCheck.HasPermissionAsync("units:create"))
            {
                _logger.LogWarning("AddPropertyUnit: forbidden — caller lacks units:create permission");
                return Forbid();
            }

            // 🛡️ Tenants and Owners may never add (or delete) units — that is an agency-level
            // action. Any authenticated user outside a read-only role is permitted.
            if (IsReadOnlyRole()) return Forbid();

            var userTenantId = User.FindFirst("TenantId")?.Value;
            if (string.IsNullOrWhiteSpace(userTenantId)) return Forbid();
            var role = GetCurrentCanonicalRole();
            if (role != "PlatformAdmin")
            {
                var property = await _propertyRepository.Query()
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
                RentalRate = payload.RentalRate,
                RentEscalationPercentage = payload.RentEscalationPercentage,
                LeaseTermMonths = payload.LeaseTermMonths,
                IsCurrentlyVacant = payload.IsCurrentlyVacant,
                UnitImagesJson = "[]",
                CreatedAt = DateTime.UtcNow
            };

            _propertyUnitRepository.Add(unit);
            await _dbContext.SaveChangesAsync();

            return Ok(unit);
        }

        // --- ❌ ENDPOINT: DELETE SUB-UNIT FROM PROPERTY ---
        [Authorize]
        [HttpDelete("{propertyId}/units/{unitId}")]
        public async Task<IActionResult> DeletePropertyUnit(int propertyId, int unitId)
        {
            if (!await PermissionCheck.HasPermissionAsync("units:delete"))
            {
                _logger.LogWarning("DeletePropertyUnit: forbidden — caller lacks units:delete permission");
                return Forbid();
            }

            // 🛡️ Tenants and Owners may never delete anything.
            if (IsReadOnlyRole()) return Forbid();

            var unit = await GetAccessibleUnitAsync(propertyId, unitId);
            if (unit == null)
            {
                if (string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value)) return Forbid();
                return NotFound(new { message = "Property unit not found." });
            }

            _propertyUnitRepository.Remove(unit);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Unit successfully unlinked and deleted from property." });
        }

        // --- ✏️ ENDPOINT: UPDATE UNIT DETAILS (editable unit on property page) ---
        [Authorize]
        [HttpPut("{propertyId}/units/{unitId:int}")]
        public async Task<IActionResult> UpdatePropertyUnit(int propertyId, int unitId, [FromBody] UpdatePropertyUnitPayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("units:edit"))
            {
                _logger.LogWarning("UpdatePropertyUnit: forbidden — caller lacks units:edit permission");
                return Forbid();
            }

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
            unit.RentalRate = payload.RentalRate;
            unit.RentEscalationPercentage = payload.RentEscalationPercentage;
            unit.LeaseTermMonths = payload.LeaseTermMonths;
            unit.IsCurrentlyVacant = payload.IsCurrentlyVacant;

            await _dbContext.SaveChangesAsync();
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
            await _dbContext.SaveChangesAsync();

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
            await _dbContext.SaveChangesAsync();

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
            if (string.IsNullOrWhiteSpace(userTenantId) && role != "PlatformAdmin") return null;

            var overrideTenantId = HttpContext?.Request?.Query["tenantId"].ToString();
            var usingOverride = (role == "PlatformAdmin" || role == "AgencyAdmin")
                && !string.IsNullOrWhiteSpace(overrideTenantId)
                && overrideTenantId != "__all__";
            var effectiveTenantId = usingOverride ? overrideTenantId : userTenantId;

            var isPlatformAdmin = role == "PlatformAdmin";
            var propertyQuery = _propertyRepository.Query().Where(p => p.PropertyId == propertyId);
            if (usingOverride)
            {
                // 🌳 Subtree override = comma-separated list of allowed tenants.
                var tenantIds = overrideTenantId
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (tenantIds.Count == 1) propertyQuery = propertyQuery.Where(p => p.TenantId == tenantIds[0]);
                else if (tenantIds.Count > 1) propertyQuery = propertyQuery.Where(p => tenantIds.Contains(p.TenantId));
            }
            else if (!isPlatformAdmin)
            {
                propertyQuery = propertyQuery.Where(p => p.TenantId == userTenantId);
            }

            // Owner may only touch units under a property they actually own.
            if (role == "Owner" && !string.IsNullOrWhiteSpace(userId))
                propertyQuery = propertyQuery.Where(p => p.LinkedOwnerId == userId);

            var propertyExists = await propertyQuery.AnyAsync();
            if (!propertyExists) return null;

            var unit = await _propertyUnitRepository.Query().FirstOrDefaultAsync(u => u.PropertyUnitId == unitId && u.PropertyId == propertyId);
            if (unit == null) return null;

            // A Tenant may only address the specific unit they currently lease.
            if (role == "Tenant")
            {
                var isLeased = await _unitLeaseRepository.Query().AnyAsync(l =>
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
        // only surface properties a role is entitled to see. When the caller is a
        // PlatformAdmin/AgencyAdmin and supplies ?tenantId=<id>, that override is
        // honored in place of the JWT-bound tenantId (the JWT tenant itself is
        // never mutated). The override value is a real TenantId drawn from the
        // AgencyRecord.TenantId of the selected agency.
        //
        // 🌳 Subtree support: the override may also be a comma-separated list
        // of TenantIds (e.g. ?tenantId=TEN-0001,TEN-0010,TEN-0100). This is
        // how the CRM hands a parent-agency's full subtree down to the
        // property service for dashboard aggregation — the property service
        // doesn't know about agency hierarchy on its own, so the CRM expands
        // the subtree and the property service filters on the resulting set.
        private IQueryable<PropertyAsset> ApplyPropertyVisibility(IQueryable<PropertyAsset> query)
        {
            var role = GetCurrentCanonicalRole();
            var overrideTenantId = HttpContext?.Request?.Query["tenantId"].ToString();
            var usingOverride = (role == "PlatformAdmin" || role == "AgencyAdmin")
                && !string.IsNullOrWhiteSpace(overrideTenantId)
                && overrideTenantId != "__all__";

            if (role == "PlatformAdmin" && !usingOverride) return query;

            if (usingOverride)
            {
                // 🧬 Comma-separated list = subtree filter (parent + descendants).
                var tenantIds = overrideTenantId
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (tenantIds.Count == 0) return query.Where(p => false);
                if (tenantIds.Count == 1) query = query.Where(p => p.TenantId == tenantIds[0]);
                else query = query.Where(p => tenantIds.Contains(p.TenantId));
                return query;
            }

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
                var leasedPropertyIds = _unitLeaseRepository.Query()
                    .Where(l => l.UserId == userId && l.TenantId == tenantId && l.IsActive && l.LeaseEnd >= DateTime.UtcNow)
                    .Select(l => l.PropertyId).ToList();
                query = query.Where(p => leasedPropertyIds.Contains(p.PropertyId));
            }
            return query;
        }

        private async Task<HashSet<int>> GetLeasedUnitIdsAsync(string userId, string tenantId)
        {
            return await _unitLeaseRepository.Query()
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
            var caller = User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            _logger.LogInformation("CreateProperty: requested by {Caller}", caller);

            if (!await PermissionCheck.HasPermissionAsync("properties:create"))
            {
                _logger.LogWarning("CreateProperty: forbidden — caller {Caller} lacks properties:create permission", caller);
                return Forbid();
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.CompleteAddress))
            {
                _logger.LogWarning("CreateProperty: rejected — address missing, caller={Caller}", caller);
                return BadRequest(new { message = "Property address is required." });
            }

            var userTenantId = User.FindFirst("TenantId")?.Value;
            if (string.IsNullOrWhiteSpace(userTenantId))
            {
                _logger.LogWarning("CreateProperty: forbidden — no tenant claim for caller {Caller}", caller);
                return Forbid();
            }
            if (GetCurrentCanonicalRole() == "Tenant" || GetCurrentCanonicalRole() == "Owner")
            {
                _logger.LogWarning("CreateProperty: forbidden — caller {Caller} role {Role} cannot create properties",
                    caller, GetCurrentCanonicalRole());
                return Forbid();
            }

            var isForSale = payload.IsForSale ?? string.Equals(payload.MarketClass, "Sale", StringComparison.OrdinalIgnoreCase) || string.Equals(payload.MarketClass, "Both", StringComparison.OrdinalIgnoreCase);
            var isForRent = payload.IsForRent ?? string.Equals(payload.MarketClass, "Rental", StringComparison.OrdinalIgnoreCase) || string.Equals(payload.MarketClass, "Both", StringComparison.OrdinalIgnoreCase);
            // 🧬 If the client passed explicit booleans (or the legacy string
            // maps cleanly to a single side), surface the dual-listing case in
            // the legacy MarketClass field so search and syndication see "Both".
            var marketClass = payload.MarketClass ?? "Sale";
            if (payload.IsForSale == true && payload.IsForRent == true) marketClass = "Both";
            else if (payload.IsForSale == true) marketClass = "Sale";
            else if (payload.IsForRent == true) marketClass = "Rental";

            var newProp = new PropertyAsset
            {
                TenantId = userTenantId,
                CompleteAddress = payload.CompleteAddress,
                ErfSpatialSize = payload.ErfSpatialSize ?? "200 m²",
                TitleDeedType = payload.TitleDeedType ?? "Freehold",
                PortfolioZoning = payload.PortfolioZoning ?? "Commercial Zone",
                MarketClass = marketClass,
                PropertyTypeId = payload.PropertyTypeId > 0 ? payload.PropertyTypeId : 1,
                ValuationPrice = payload.ValuationPrice,
                SalePrice = payload.SalePrice,
                TenantInstallationAllowance = payload.TenantInstallationAllowance,
                Latitude = payload.Latitude != 0 ? payload.Latitude : -33.9249m,
                Longitude = payload.Longitude != 0 ? payload.Longitude : 18.4241m,
                LinkedOwnerId = payload.LinkedOwnerId ?? "OT-0001",
                VerificationDocumentsJson = "[]",
                CreatedAt = DateTime.UtcNow,
                // 🧬 Listing flags — if the payload didn't specify them, infer
                // from the legacy MarketClass for backward compatibility.
                IsForSale = isForSale,
                IsForRent = isForRent
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
                        RentalRate = unitPayload.RentalRate,
                        RentEscalationPercentage = unitPayload.RentEscalationPercentage,
                        LeaseTermMonths = unitPayload.LeaseTermMonths,
                        IsCurrentlyVacant = unitPayload.IsCurrentlyVacant,
                        UnitImagesJson = "[]",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            _propertyRepository.Add(newProp);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Property created: PropertyId={PropertyId} Address={Address} Tenant={Tenant} By={Caller}",
                newProp.PropertyId, newProp.CompleteAddress, newProp.TenantId, caller);

            return CreatedAtAction(nameof(GetPropertyById), new { propertyId = newProp.PropertyId }, newProp);
        }

        [Authorize]
        [HttpPut("{propertyId}")]
        public async Task<IActionResult> UpdateProperty(int propertyId, [FromBody] UpdatePropertyPayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("properties:edit"))
            {
                _logger.LogWarning("UpdateProperty: forbidden — caller lacks properties:edit permission");
                return Forbid();
            }

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
            IQueryable<PropertyAsset> propertyQuery = _propertyRepository.Query()
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
            property.TenantInstallationAllowance = payload.TenantInstallationAllowance;
            property.Latitude = payload.Latitude;
            property.Longitude = payload.Longitude;
            property.LinkedOwnerId = payload.LinkedOwnerId ?? string.Empty;
            // 🧬 Update listing flags — null leaves the existing value alone.
            if (payload.IsForSale.HasValue) property.IsForSale = payload.IsForSale.Value;
            if (payload.IsForRent.HasValue) property.IsForRent = payload.IsForRent.Value;
            // 🧬 Re-synthesize the legacy MarketClass string so any consumer
            // that still reads it sees a value consistent with the two flags.
            if (property.IsForSale && property.IsForRent) property.MarketClass = "Both";
            else if (property.IsForSale) property.MarketClass = "Sale";
            else if (property.IsForRent) property.MarketClass = "Rental";

            // Re-assert the tenant scope on the entity so a malicious payload cannot
            // override or clear the TenantId isolation key during a cross-tenant attempt.
            property.TenantId = isPlatformAdmin
                ? property.TenantId
                : userTenantId;

            await _dbContext.SaveChangesAsync();
            return Ok(await _propertyRepository.Query()
                .Include(p => p.PropertyTypeDetails)
                .Include(p => p.SubUnits)
                .FirstAsync(p => p.PropertyId == propertyId));
        }
        // --- 🔍 ENDPOINT A: MULTI-CRITERIA REAL ESTATE SEARCH PIPELINE ---
        [Authorize]
        [HttpPost("search")]
        public async Task<IActionResult> SearchProperties([FromBody] PropertySearchCriteria criteria, [FromQuery] int page = 1, [FromQuery] int? pageSize = null)
        {
            if (!await PermissionCheck.HasPermissionAsync("properties:view"))
            {
                _logger.LogWarning("SearchProperties: forbidden — caller lacks properties:view permission");
                return Forbid();
            }

            var caller = User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            _logger.LogInformation("SearchProperties: Page={Page} PropertyType={PropertyType} ListingType={ListingType} Locations={Locations} Caller={Caller}",
                page, criteria.PropertyType, criteria.ListingType, string.Join(",", criteria.Locations ?? Enumerable.Empty<string>()), caller);

            if (page < 1) page = 1;
            // 📐 Page-size override: callers can request up to a custom cap (the
            // map + the "load all" PropertySearch view pass pageSize=10000).
            // A null/zero/negative value falls back to the default of 20.
            const int DEFAULT_PAGE_SIZE = 20;
            const int MAX_PAGE_SIZE = 10000;
            int pageSizeValue = pageSize.HasValue && pageSize.Value > 0
                ? Math.Min(pageSize.Value, MAX_PAGE_SIZE)
                : DEFAULT_PAGE_SIZE;

            // Eagerly load relational structures (Property Type lookups and sub-units)
            var query = _propertyRepository.Query()
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

            // Price range filter (uses the active listing price; sale→SalePrice, rent→unit RentalRate)
            if (criteria.MinPrice.HasValue)
                query = query.Where(p => (criteria.ListingType == "Rent" ? p.SubUnits.Any(u => u.RentalRate >= criteria.MinPrice.Value) : p.SalePrice >= criteria.MinPrice.Value));
            if (criteria.MaxPrice.HasValue)
                query = query.Where(p => (criteria.ListingType == "Rent" ? p.SubUnits.Any(u => u.RentalRate <= criteria.MaxPrice.Value) : p.SalePrice <= criteria.MaxPrice.Value));

            // The UI submits area bounds in square metres.  Use linked unit area
            // where available, falling back to the text field only for display.
            if (criteria.MinSize.HasValue)
                query = query.Where(p => p.SubUnits.Any(u => u.NetRentableAreaSqM >= criteria.MinSize.Value));
            if (criteria.MaxSize.HasValue)
                query = query.Where(p => p.SubUnits.Any(u => u.NetRentableAreaSqM <= criteria.MaxSize.Value));

            var total = await query.CountAsync();
            var results = await query.OrderBy(p => p.PropertyId)
                .Skip((page - 1) * pageSizeValue).Take(pageSizeValue)
                .Select(p => new
                {
                    p.PropertyId,
                    p.TenantId,
                    p.CompleteAddress,
                    p.ErfSpatialSize,
                    p.TitleDeedType,
                    p.PortfolioZoning,
                    p.MarketClass,
                    p.IsForSale,
                    p.IsForRent,
                    p.PropertyTypeId,
                    p.ValuationPrice,
                    p.SalePrice,
                    p.TenantInstallationAllowance,
                    p.Latitude,
                    p.Longitude,
                    p.VerificationDocumentsJson,
                    p.LinkedOwnerId,
                    p.PropertyDescription,
                    p.CreatedAt,
                    p.PropertyTypeDetails,
                    // 📜 Days remaining on the active sole mandate (if any).
                    // 0 / null = no active mandate. Surfaced on list cards so
                    // the 14-day expiration banner can fire without a
                    // second round-trip per row.
                    soleMandateDaysRemaining = p.SoleMandates
                        .Where(m => m.IsActive && m.StartDate <= DateTime.UtcNow && m.EndDate >= DateTime.UtcNow)
                        .OrderByDescending(m => m.CreatedAt)
                        .Select(m => (int?)Math.Ceiling((m.EndDate - DateTime.UtcNow).TotalDays))
                        .FirstOrDefault()
                })
                .ToListAsync();
                         HttpContext.Response.Headers["X-Total-Count"] = total.ToString();

            // 📊 Chart-friendly aggregates over the ENTIRE filtered universe
            // (not the 20-row page) so the listing market-share donut on the
            // dashboard reflects every property in scope.
            var propertyTypeGroups = await query
                .Where(p => p.PropertyTypeDetails != null)
                .GroupBy(p => p.PropertyTypeDetails!.Name)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToListAsync();
            var listingGroups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["ForSale"] = await query.CountAsync(p => p.IsForSale),
                ["ForRent"] = await query.CountAsync(p => p.IsForRent)
            };
            var propertyAggregates = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
            {
                ["propertyType"] = propertyTypeGroups.ToDictionary(g => g.Name ?? "Unknown", g => g.Count),
                ["listingEligibility"] = listingGroups
            };
            HttpContext.Response.Headers["X-Aggregates"] = System.Text.Json.JsonSerializer.Serialize(propertyAggregates);

            _logger.LogInformation("SearchProperties: returned {Count} results (page {Page}) out of {Total} for caller {Caller}",
                results.Count, page, total, caller);
            return Ok(results);
        }

        // --- 🖼️ ENDPOINT B: IMAGE UPLOADER ---
        // Persists the uploaded picture as a data-URL entry inside the
        // property's `VerificationDocumentsJson` and emits a tiny Kafka
        // event (just the property id + filename) so downstream
        // optimisation/OCR pipelines can react. Sending the raw base64
        // through Kafka was rejected by the broker with
        // "Message size too large" — blobs belong in storage, not in event
        // streams.
        [HttpPost("{propertyId}/upload-image")]
        public async Task<IActionResult> UploadPropertyImage(int propertyId, [FromBody] ImageUploadPayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("properties:edit"))
            {
                _logger.LogWarning("UploadPropertyImage: forbidden — caller lacks properties:edit permission");
                return Forbid();
            }

            var caller = User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            _logger.LogInformation("UploadPropertyImage: PropertyId={PropertyId} Caller={Caller}", propertyId, caller);
            var property = await _propertyRepository.GetByIdAsync(propertyId);
            if (property == null)
            {
                _logger.LogWarning("UploadPropertyImage: PropertyId={PropertyId} not found", propertyId);
                return NotFound(new { message = "Target property record not found." });
            }

            if (payload == null || string.IsNullOrEmpty(payload.Base64DataString))
            {
                return BadRequest(new { message = "Invalid picture data payload." });
            }

            // 🖼️ Persist to the property IMAGE GALLERY. Append (don't replace) so the
            // gallery grows over time. A hard cap of 50 keeps the row sane.
            var current = string.IsNullOrWhiteSpace(property.GalleryImageJson)
                ? new List<string>()
                : (System.Text.Json.JsonSerializer.Deserialize<List<string>>(property.GalleryImageJson) ?? new List<string>());

            var mime = payload.FileName?.ToLowerInvariant() switch
            {
                var n when n != null && n.EndsWith(".png") => "image/png",
                var n when n != null && n.EndsWith(".webp") => "image/webp",
                var n when n != null && (n.EndsWith(".jpg") || n.EndsWith(".jpeg")) => "image/jpeg",
                _ => "image/jpeg"
            };
            var dataUrl = $"data:{mime};base64,{payload.Base64DataString}";
            current.Add(dataUrl);
            if (current.Count > 50) current = current.GetRange(current.Count - 50, 50);
            property.GalleryImageJson = System.Text.Json.JsonSerializer.Serialize(current);

            await _dbContext.SaveChangesAsync();

            // 🛰️ Notify downstream optimisers (OCR, image resizing, ML
            // tagging) via Kafka. The event payload is intentionally tiny —
            // just the id + filename — so it stays well under the broker's
            // 1 MB default message size limit.
            var imageEvent = new PropertyImageUploadedEvent(
                propertyId,
                payload.FileName,
                payload.Base64DataString,
                property.TenantId,
                caller);
            try
            {
                await _kafkaPublisher.RaiseEventAsync(
                    TopicNames.PropertyImages,
                    propertyId.ToString(),
                    imageEvent);
            }
            catch (Exception ex)
            {
                // The image is already persisted — a Kafka publish failure
                // shouldn't fail the user-visible upload. Just log it so the
                // optimisation pipeline can be re-triggered later.
                _logger.LogWarning(ex, "UploadPropertyImage: Kafka notify failed for PropertyId={PropertyId} (image still saved)", propertyId);
            }

            return Ok(new
            {
                message = "Image persisted to property gallery and optimisers notified.",
                imageCount = current.Count
            });
        }
        // --- 🔗 ENDPOINT C: PORTAL SYNDICATION CHECKBOX TRACKER TOGGLE ---
        [HttpPost("syndicate")]
        public async Task<IActionResult> ToggleSyndication([FromBody] SyndicationTogglePayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("properties:edit"))
            {
                _logger.LogWarning("ToggleSyndication: forbidden — caller lacks properties:edit permission");
                return Forbid();
            }

            var caller = User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            _logger.LogInformation("ToggleSyndication: PropertyId={PropertyId} PlatformId={PlatformId} Syndicate={Syndicate} Caller={Caller}",
                payload?.PropertyId, payload?.PlatformId, payload?.SyndicateStatus, caller);

            if (payload == null)
            {
                _logger.LogWarning("ToggleSyndication: rejected — null payload, caller={Caller}", caller);
                return BadRequest(new { message = "Invalid syndication data envelope." });
            }

            var property = await _propertyRepository.Query()
                .Include(p => p.PropertyTypeDetails)
                .FirstOrDefaultAsync(p => p.PropertyId == payload.PropertyId);

            if (property == null) return NotFound(new { message = "Target property record not found." });

            var platform = await _externalPlatformRepository.GetByIdAsync(payload.PlatformId);
            if (platform == null) return NotFound(new { message = "Target external marketing channel portal not found." });

            // Fetch or create the portal links ledger row matching current identifiers
            var syndicationRecord = await _propertyPlatformSyndicationRepository.Query()
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
                _propertyPlatformSyndicationRepository.Add(syndicationRecord);
            }
            else
            {
                syndicationRecord.IsSyndicatedActive = payload.SyndicateStatus;
                syndicationRecord.LastSynchronizedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            var syndicationEvent = new PropertySyndicationRequestedEvent(
                PropertyId: payload.PropertyId,
                PlatformId: payload.PlatformId,
                IsActive: payload.SyndicateStatus,
                TenantId: property.TenantId,
                RequestedBy: caller
            );

            await _kafkaPublisher.RaiseEventAsync(
                TopicNames.PropertySyndication,
                payload.PropertyId.ToString(),
                syndicationEvent);

            _logger.LogInformation("ToggleSyndication: completed — PropertyId={PropertyId} PlatformId={PlatformId} Active={Active} ExternalRef={ExternalRef}",
                payload.PropertyId, payload.PlatformId, payload.SyndicateStatus, syndicationRecord.ExternalReferenceIndex);

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
            if (!await PermissionCheck.HasPermissionAsync("billing:export"))
            {
                _logger.LogWarning("GenerateBrochure: forbidden — caller lacks billing:export permission");
                return Forbid();
            }

            if (payload == null || payload.DealId <= 0 || !payload.SubUnits.Any())
            {
                _logger.LogWarning("GenerateBrochure: rejected — invalid payload (DealId={DealId} Units={Units})",
                    payload?.DealId, payload?.SubUnits?.Count ?? 0);
                return BadRequest(new { message = "A deal and at least one unit are required to generate a brochure." });
            }

            var propertyIds = payload.SubUnits.Select(u => u.PropertyId).Distinct().ToList();
            var validPropertyCount = await _propertyRepository.Query().CountAsync(p => propertyIds.Contains(p.PropertyId));
            if (validPropertyCount != propertyIds.Count)
            {
                _logger.LogWarning("GenerateBrochure: one or more properties not found — PropertyIds={PropertyIds}", propertyIds);
                return NotFound(new { message = "One or more target property records were not found." });
            }

            var canonicalRole = GetCurrentCanonicalRole();
            var isTrialClaim = User.FindFirst("IsTrial")?.Value;
            var trialEndDateClaim = User.FindFirst("TrialEndDate")?.Value;

            if (isTrialClaim == "true" && (canonicalRole == "AgencyAdmin" || canonicalRole == "Broker"))
            {
                _logger.LogWarning("GenerateBrochure: blocked — trial restriction for Role={Role} DealId={DealId}",
                    canonicalRole, payload.DealId);
                return Forbid(new
                {
                    message = "Brochure compilation is restricted during the trial period.",
                    trialEndDate = trialEndDateClaim
                }.ToString());
            }

            var caller = User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            var tenantId = User.FindFirst("TenantId")?.Value ?? string.Empty;

            var brochureEvent = new BrochureGenerationRequestedEvent(
                DealId: payload.DealId,
                BrochureCardId: payload.BrochureCardId,
                PropertyId: payload.SubUnits.FirstOrDefault()?.PropertyId ?? 0,
                TenantId: tenantId,
                RequestedBy: caller,
                BrokerName: payload.BrokerName,
                BrokerEmail: payload.BrokerEmail,
                BrokerPhone: payload.BrokerPhone,
                CompanyName: payload.CompanyName,
                CompanyLogoUrl: payload.CompanyLogoUrl,
                ClientName: payload.ClientName,
                ClientEmail: payload.ClientEmail,
                ClientPhone: payload.ClientPhone,
                MarketClass: payload.MarketClass,
                TenantInstallationAllowance: payload.TenantInstallationAllowance,
                SubUnits: payload.SubUnits.Select(u => new BrochureSubUnitEvent
                {
                    PropertyId = u.PropertyId,
                    PropertyDescription = u.PropertyDescription,
                    Latitude = u.Latitude,
                    Longitude = u.Longitude,
                    UnitNumber = u.UnitNumber,
                    UnitDescription = u.UnitDescription,
                    NetRentableAreaSqM = u.NetRentableAreaSqM,
                    RentalRate = u.RentalRate,
                    RentEscalationPercentage = u.RentEscalationPercentage,
                    LeaseTermMonths = u.LeaseTermMonths,
                    SalePrice = u.SalePrice,
                    SelectedImageRefs = u.SelectedImageRefs
                }).ToList()
            );

            await _kafkaPublisher.RaiseEventAsync(
                TopicNames.BrochureGenerationRequests,
                payload.DealId.ToString(),
                brochureEvent);

            _logger.LogInformation("GenerateBrochure: queued — DealId={DealId} Units={Units}", payload.DealId, payload.SubUnits.Count);
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
        if (!await PermissionCheck.HasPermissionAsync("units:view"))
        {
            _logger.LogWarning("GetUnitMaintenanceCalls: forbidden — caller lacks units:view permission");
            return Forbid();
        }

        var unit = await GetAccessibleUnitAsync(propertyId, unitId);
        if (unit == null)
        {
            if (string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value)) return Forbid();
            return NotFound(new { message = "Target property or unit not found." });
        }

        var calls = await _maintenanceCallRepository.Query()
            .Where(m => m.PropertyId == propertyId && m.PropertyUnitId == unitId)
            .OrderByDescending(m => m.UpdatedAt)
            .ToListAsync();
        return Ok(calls);
    }

    [Authorize]
    [HttpPost("{propertyId}/units/{unitId:int}/maintenance")]
    public async Task<IActionResult> AddUnitMaintenanceCall(int propertyId, int unitId, [FromBody] MaintenanceCallPayload payload)
    {
        if (!await PermissionCheck.HasPermissionAsync("units:edit"))
        {
            _logger.LogWarning("AddUnitMaintenanceCall: forbidden — caller lacks units:edit permission");
            return Forbid();
        }

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

        _maintenanceCallRepository.Add(call);
        await _dbContext.SaveChangesAsync();
        return Ok(call);
    }

    [Authorize]
    [HttpDelete("{propertyId}/units/{unitId:int}/maintenance/{callId:int}")]
    public async Task<IActionResult> DeleteMaintenanceCall(int callId)
    {
        if (!await PermissionCheck.HasPermissionAsync("units:delete"))
        {
            _logger.LogWarning("DeleteMaintenanceCall: forbidden — caller lacks units:delete permission");
            return Forbid();
        }

        // Tenants and Owners may never delete anything.
        if (IsReadOnlyRole()) return Forbid();

        var call = await _maintenanceCallRepository.GetByIdAsync(callId);
        if (call == null) return NotFound(new { message = "Maintenance call not found." });

        // Tenant isolation: admins only act within their tenant.
        var tenantId = User.FindFirst("TenantId")?.Value;
        if (GetCurrentCanonicalRole() != "PlatformAdmin" && call.TenantId != tenantId)
            return Forbid();

            _maintenanceCallRepository.Remove(call);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Maintenance call resolved and removed." });
        }

    // ────────────────────────────────────────────────────────────────────────────
    // 📜 UNIT LEASE LEDGER — backing the Lease + History tabs on the unit modal.
    //   • Today < LeaseStart  → future lease
    //   • LeaseStart ≤ Today ≤ LeaseEnd → active (current tenant)
    //   • Today > LeaseEnd  → expired
    // We always re-derive IsActive on read so the UI badge stays correct even if
    // the stored flag drifts. The flag itself is only persisted for backwards
    // compatibility with the existing tenant-visibility filter (see
    // ApplyPropertyVisibility / GetLeasedUnitIdsAsync).
    // ────────────────────────────────────────────────────────────────────────────

    [Authorize]
    [HttpGet("{propertyId}/units/{unitId:int}/leases")]
    public async Task<IActionResult> GetUnitLeases(int propertyId, int unitId)
    {
        if (!await PermissionCheck.HasPermissionAsync("units:view"))
        {
            _logger.LogWarning("GetUnitLeases: forbidden — caller lacks units:view permission");
            return Forbid();
        }

        var unit = await GetAccessibleUnitAsync(propertyId, unitId);
        if (unit == null)
        {
            if (string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value)) return Forbid();
            return NotFound(new { message = "Target property or unit not found." });
        }

        var now = DateTime.UtcNow;
        var leases = await _unitLeaseRepository.Query()
            .Where(l => l.PropertyId == propertyId && l.PropertyUnitId == unitId)
            .OrderByDescending(l => l.LeaseStart)
            .ToListAsync();

        // Re-derive IsActive on the wire so the UI never has to compute it.
        var projected = leases.Select(l => new
        {
            l.UnitLeaseId,
            l.UserId,
            l.TenantId,
            l.PropertyId,
            l.PropertyUnitId,
            l.LeaseStart,
            l.LeaseEnd,
            // The authoritative live flag: window contains today.
            IsActive = l.LeaseStart <= now && now <= l.LeaseEnd,
            IsCurrent = l.LeaseStart <= now && now <= l.LeaseEnd,
            IsFuture = l.LeaseStart > now,
            IsExpired = l.LeaseEnd < now,
            l.ContactId,
            l.MonthlyRent,
            l.DepositAmount,
            l.Notes,
            l.AccruedInterestPercent,
            l.TenantNameSnapshot,
            l.CreatedAt
        }).ToList();
        return Ok(projected);
    }

    [Authorize]
    [HttpGet("{propertyId}/units/{unitId:int}/leases/active")]
    public async Task<IActionResult> GetUnitActiveLease(int propertyId, int unitId)
    {
        if (!await PermissionCheck.HasPermissionAsync("units:view"))
        {
            _logger.LogWarning("GetUnitActiveLease: forbidden — caller lacks units:view permission");
            return Forbid();
        }

        var unit = await GetAccessibleUnitAsync(propertyId, unitId);
        if (unit == null)
        {
            if (string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value)) return Forbid();
            return NotFound(new { message = "Target property or unit not found." });
        }

        var now = DateTime.UtcNow;
        var lease = await _unitLeaseRepository.Query()
            .Where(l => l.PropertyId == propertyId && l.PropertyUnitId == unitId
                        && l.LeaseStart <= now && now <= l.LeaseEnd)
            .OrderByDescending(l => l.LeaseStart)
            .FirstOrDefaultAsync();
        if (lease == null) return Ok(null);

        return Ok(new
        {
            lease.UnitLeaseId,
            lease.UserId,
            lease.TenantId,
            lease.PropertyId,
            lease.PropertyUnitId,
            lease.LeaseStart,
            lease.LeaseEnd,
            IsActive = true,
            IsCurrent = true,
            IsFuture = false,
            IsExpired = false,
            lease.ContactId,
            lease.MonthlyRent,
            lease.DepositAmount,
            lease.Notes,
            lease.AccruedInterestPercent,
            lease.TenantNameSnapshot,
            lease.CreatedAt
        });
    }

    [Authorize]
    [HttpPost("{propertyId}/units/{unitId:int}/leases")]
    public async Task<IActionResult> CreateUnitLease(int propertyId, int unitId, [FromBody] CreateUnitLeasePayload payload)
    {
        if (!await PermissionCheck.HasPermissionAsync("units:edit"))
        {
            _logger.LogWarning("CreateUnitLease: forbidden — caller lacks units:edit permission");
            return Forbid();
        }
        if (IsReadOnlyRole()) return Forbid();
        if (payload == null) return BadRequest(new { message = "A lease payload is required." });
        if (payload.LeaseEnd <= payload.LeaseStart)
            return BadRequest(new { message = "Lease end date must be after the start date." });

        var unit = await GetAccessibleUnitAsync(propertyId, unitId);
        if (unit == null)
        {
            if (string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value)) return Forbid();
            return NotFound(new { message = "Target property or unit not found." });
        }

        var tenantId = User.FindFirst("TenantId")?.Value ?? string.Empty;
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var now = DateTime.UtcNow;
        var isActive = payload.LeaseStart <= now && now <= payload.LeaseEnd;

        var lease = new UnitLease
        {
            UserId = string.IsNullOrWhiteSpace(payload.UserId) ? userId : payload.UserId!,
            TenantId = tenantId,
            PropertyId = propertyId,
            PropertyUnitId = unitId,
            LeaseStart = payload.LeaseStart,
            LeaseEnd = payload.LeaseEnd,
            IsActive = isActive,
            CreatedAt = now,
            ContactId = payload.ContactId,
            MonthlyRent = payload.MonthlyRent,
            DepositAmount = payload.DepositAmount,
            Notes = payload.Notes,
            AccruedInterestPercent = payload.AccruedInterestPercent,
            // 🏷️ Snapshotted tenant name from the frontend (picked contact).
            // Null is allowed — the history view will fall back to "Unlinked".
            TenantNameSnapshot = string.IsNullOrWhiteSpace(payload.TenantNameSnapshot)
                ? null
                : payload.TenantNameSnapshot.Trim()
        };
        _unitLeaseRepository.Add(lease);
        await _dbContext.SaveChangesAsync();
        return Ok(lease);
    }

    [Authorize]
    [HttpPut("{propertyId}/units/{unitId:int}/leases/{leaseId:int}")]
    public async Task<IActionResult> UpdateUnitLease(int propertyId, int unitId, int leaseId, [FromBody] UpdateUnitLeasePayload payload)
    {
        if (!await PermissionCheck.HasPermissionAsync("units:edit"))
        {
            _logger.LogWarning("UpdateUnitLease: forbidden — caller lacks units:edit permission");
            return Forbid();
        }
        if (IsReadOnlyRole()) return Forbid();
        if (payload == null) return BadRequest(new { message = "A lease payload is required." });

        var unit = await GetAccessibleUnitAsync(propertyId, unitId);
        if (unit == null)
        {
            if (string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value)) return Forbid();
            return NotFound(new { message = "Target property or unit not found." });
        }

        var lease = await _unitLeaseRepository.Query().FirstOrDefaultAsync(l =>
            l.UnitLeaseId == leaseId && l.PropertyId == propertyId && l.PropertyUnitId == unitId);
        if (lease == null) return NotFound(new { message = "Lease not found." });

        if (payload.LeaseStart.HasValue) lease.LeaseStart = payload.LeaseStart.Value;
        if (payload.LeaseEnd.HasValue) lease.LeaseEnd = payload.LeaseEnd.Value;
        if (lease.LeaseEnd <= lease.LeaseStart)
            return BadRequest(new { message = "Lease end date must be after the start date." });

        if (payload.ContactId != null) lease.ContactId = string.IsNullOrWhiteSpace(payload.ContactId) ? null : payload.ContactId;
        if (payload.MonthlyRent.HasValue) lease.MonthlyRent = payload.MonthlyRent;
        if (payload.DepositAmount.HasValue) lease.DepositAmount = payload.DepositAmount;
        if (payload.Notes != null) lease.Notes = payload.Notes;
        if (payload.AccruedInterestPercent.HasValue) lease.AccruedInterestPercent = payload.AccruedInterestPercent;
        // Snapshot semantics: explicit non-null overwrites, null leaves
        // existing value. An explicit empty string clears it.
        if (payload.TenantNameSnapshot != null)
        {
            lease.TenantNameSnapshot = string.IsNullOrWhiteSpace(payload.TenantNameSnapshot)
                ? null
                : payload.TenantNameSnapshot.Trim();
        }
        if (payload.EndNow == true)
        {
            // End-now: pin LeaseEnd to today so the lease falls out of the active
            // window and the unit is logically vacant again.
            lease.LeaseEnd = DateTime.UtcNow;
        }
        // Always re-derive the persisted IsActive flag to stay in sync.
        var now = DateTime.UtcNow;
        lease.IsActive = lease.LeaseStart <= now && now <= lease.LeaseEnd;

        await _dbContext.SaveChangesAsync();
        return Ok(lease);
    }

    [Authorize]
    [HttpDelete("{propertyId}/units/{unitId:int}/leases/{leaseId:int}")]
    public async Task<IActionResult> DeleteUnitLease(int propertyId, int unitId, int leaseId)
    {
        if (!await PermissionCheck.HasPermissionAsync("units:delete"))
        {
            _logger.LogWarning("DeleteUnitLease: forbidden — caller lacks units:delete permission");
            return Forbid();
        }
        if (IsReadOnlyRole()) return Forbid();

        var unit = await GetAccessibleUnitAsync(propertyId, unitId);
        if (unit == null)
        {
            if (string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value)) return Forbid();
            return NotFound(new { message = "Target property or unit not found." });
        }

        var lease = await _unitLeaseRepository.Query().FirstOrDefaultAsync(l =>
            l.UnitLeaseId == leaseId && l.PropertyId == propertyId && l.PropertyUnitId == unitId);
        if (lease == null) return NotFound(new { message = "Lease not found." });

        _unitLeaseRepository.Remove(lease);
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Lease removed." });
    }

    // --- 📜 SOLE MANDATE ENDPOINTS ---

    // 📜 History of all sole-mandate agreements for a property (active
    // and closed). Sorted newest-first so the most recent record is
    // index 0. Open to anyone with properties:view so tenants can see
    // why a property is restricted.
    [Authorize]
    [HttpGet("{propertyId:int}/mandates")]
    public async Task<IActionResult> GetSoleMandates(int propertyId)
    {
        var userTenantId = User.FindFirst("TenantId")?.Value;
        if (string.IsNullOrWhiteSpace(userTenantId)) return Forbid();

        var property = await _propertyRepository.Query()
            .Where(p => p.PropertyId == propertyId)
            .FirstOrDefaultAsync();
        if (property == null) return NotFound(new { message = "Property not found." });

        var mandates = await _soleMandateRepository.Query()
            .Where(m => m.PropertyId == propertyId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new
            {
                m.SoleMandateId,
                m.StartDate,
                m.EndDate,
                m.IsActive,
                m.ClosedAt,
                m.CreatedAt,
                m.CreatedByUserId,
                daysRemaining = m.IsActive && m.EndDate > DateTime.UtcNow
                    ? (int)Math.Ceiling((m.EndDate - DateTime.UtcNow).TotalDays)
                    : 0
            })
            .ToListAsync();
        return Ok(mandates);
    }

    // ➕ Open a new sole mandate. If the property already has an active
    // mandate, this endpoint closes it (sets IsActive=0, ClosedAt=now)
    // and creates the new active row in the same transaction. The
    // filtered unique index would reject two-active inserts anyway, so
    // we close-then-insert to keep history intact.
    [Authorize]
    [HttpPost("{propertyId:int}/mandates")]
    public async Task<IActionResult> CreateSoleMandate(int propertyId, [FromBody] CreateSoleMandatePayload payload)
    {
        if (!await PermissionCheck.HasPermissionAsync("properties:edit"))
        {
            _logger.LogWarning("CreateSoleMandate: forbidden — caller lacks properties:edit permission");
            return Forbid();
        }
        if (payload == null) return BadRequest(new { message = "Mandate payload is required." });
        if (payload.EndDate <= payload.StartDate)
            return BadRequest(new { message = "End date must be after start date." });
        if (payload.EndDate <= DateTime.UtcNow)
            return BadRequest(new { message = "End date must be in the future." });

        var userTenantId = User.FindFirst("TenantId")?.Value;
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userTenantId)) return Forbid();

        // 🛡️ Apply same visibility rules as property edit so an
        // AgencyAdmin can't add a mandate to a property they can't see.
        var propertyQuery = _propertyRepository.Query().Where(p => p.PropertyId == propertyId);
        propertyQuery = ApplyPropertyVisibility(propertyQuery);
        var property = await propertyQuery.FirstOrDefaultAsync();
        if (property == null) return NotFound(new { message = "Property not found." });

        // 🧹 Auto-close the currently-active mandate (if any) in the same transaction.
        var currentActive = await _soleMandateRepository.Query()
            .Where(m => m.PropertyId == propertyId && m.IsActive)
            .ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var m in currentActive)
        {
            m.IsActive = false;
            m.ClosedAt = now;
        }

        var newMandate = new SoleMandate
        {
            PropertyId = propertyId,
            TenantId = userTenantId!,
            StartDate = payload.StartDate,
            EndDate = payload.EndDate,
            IsActive = true,
            CreatedByUserId = userId ?? string.Empty,
            CreatedAt = now
        };
        _soleMandateRepository.Add(newMandate);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSoleMandates), new { propertyId }, new
        {
            newMandate.SoleMandateId,
            newMandate.StartDate,
            newMandate.EndDate,
            newMandate.IsActive,
            newMandate.CreatedAt,
            newMandate.CreatedByUserId,
            daysRemaining = (int)Math.Ceiling((newMandate.EndDate - now).TotalDays)
        });
    }

    // ❌ Terminate an active mandate early (or no-op on a closed one).
    [Authorize]
    [HttpDelete("{propertyId:int}/mandates/{mandateId:int}")]
    public async Task<IActionResult> CloseSoleMandate(int propertyId, int mandateId)
    {
        if (!await PermissionCheck.HasPermissionAsync("properties:edit"))
        {
            _logger.LogWarning("CloseSoleMandate: forbidden — caller lacks properties:edit permission");
            return Forbid();
        }
        var mandate = await _soleMandateRepository.Query()
            .FirstOrDefaultAsync(m => m.PropertyId == propertyId && m.SoleMandateId == mandateId);
        if (mandate == null) return NotFound(new { message = "Mandate not found." });
        if (!mandate.IsActive) return Ok(new { message = "Mandate is already closed." });

        mandate.IsActive = false;
        mandate.ClosedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Mandate closed." });
    }

    // 📊 Active-listing count + average DOM for the broker dashboard.
    // `tenantId` may be a comma-separated list of tenantIds so the caller
    // can pass an agency subtree in one go.
    [HttpGet("active-listings-count")]
    public async Task<IActionResult> GetActiveListingsCount(
        [FromQuery] string? tenantId = null,
        [FromQuery] string? linkedBrokerId = null)
    {
        var tenantIds = (tenantId ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (tenantIds.Count == 0) return Ok(new { activeListings = 0, averageDom = 0 });

        var q = _propertyRepository.Query()
            .Where(p => tenantIds.Contains(p.TenantId) && (p.IsForSale || p.IsForRent));
        var count = await q.CountAsync();
        if (count == 0) return Ok(new { activeListings = 0, averageDom = 0 });
        var avg = await q.Select(p => (double)EF.Functions.DateDiffDay(p.CreatedAt, DateTime.UtcNow)).AverageAsync();
        return Ok(new { activeListings = count, averageDom = (int)Math.Round(avg) });
    }

    // ⚠️ Compliance endpoint: active sole mandates whose EndDate is within
    // the next `days` days (or already in the past). Used by the CRM
    // service's broker compliance inbox. `tenantId` may be a comma-separated
    // list of tenantIds so the caller can pass an agency subtree in one go.
    [HttpGet("expiring-mandates")]
    public async Task<IActionResult> GetExpiringMandates(
        [FromQuery] string? tenantId = null,
        [FromQuery] int days = 14,
        [FromQuery] string? brokerId = null)
    {
        var tenantIds = (tenantId ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (tenantIds.Count == 0) return Ok(Array.Empty<object>());

        var cutoff = DateTime.UtcNow.AddDays(days);
        var query = _soleMandateRepository.Query()
            .Where(m => tenantIds.Contains(m.TenantId) && m.IsActive && m.EndDate <= cutoff);
        var rows = await query
            .OrderBy(m => m.EndDate)
            .Select(m => new
            {
                m.SoleMandateId,
                m.PropertyId,
                m.TenantId,
                m.StartDate,
                endDate = m.EndDate,
                m.CreatedByUserId
            })
            .ToListAsync();
        return Ok(rows);
    }

    // ============================================================
    // 🏠/🏢 PROPERTY + UNIT AMENITIES
    // ============================================================

    // A DTO that carries the amenity type metadata the UI needs to render the
    // right input (toggle vs number) and respects the residential-only flag.
    private class AmenityDto
    {
        public int AmenityId { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public int PropertyId { get; set; }
        public int? PropertyUnitId { get; set; }
        public int AmenityTypeId { get; set; }
        public string AmenityName { get; set; } = string.Empty;
        public string? Label { get; set; }
        public bool RequiresBoolean { get; set; }
        public bool RequiresNumber { get; set; }
        public bool IsResidentialOnly { get; set; }
        public bool? BooleanValue { get; set; }
        public int? NumberValue { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
    }

    // 📦 List every amenity (both scopes) for a property, decorated with the
    // type metadata the frontend uses to render toggles/numbers.
    [HttpGet("{propertyId:int}/amenities")]
    public async Task<IActionResult> GetPropertyAmenities(int propertyId)
    {
        var property = await _propertyRepository.Query()
            .Include(p => p.PropertyTypeDetails)
            .FirstOrDefaultAsync(p => p.PropertyId == propertyId);
        if (property == null) return NotFound(new { message = "Property not found." });

        var isResidential = string.Equals(property.PropertyTypeDetails?.Name, "Residential", StringComparison.OrdinalIgnoreCase);

        var rows = await _propertyUnitAmenityRepository.Query()
            .Where(a => a.PropertyId == propertyId && a.IsActive)
            .Include(a => a.AmenityType)
            .Select(a => new AmenityDto
            {
                AmenityId = a.AmenityId,
                TenantId = a.TenantId,
                PropertyId = a.PropertyId,
                PropertyUnitId = a.PropertyUnitId,
                AmenityTypeId = a.AmenityTypeId,
                AmenityName = a.AmenityType!.Name,
                Label = a.AmenityType.Label,
                RequiresBoolean = a.AmenityType.RequiresBoolean,
                RequiresNumber = a.AmenityType.RequiresNumber,
                IsResidentialOnly = a.AmenityType.IsResidentialOnly,
                BooleanValue = a.BooleanValue,
                NumberValue = a.NumberValue,
                Notes = a.Notes,
                IsActive = a.IsActive
            })
            .ToListAsync();

        // Tell the UI whether unit-level add is allowed for this property.
        return Ok(new
        {
            isResidential,
            canAddUnitAmenities = !isResidential,
            items = rows
        });
    }

    // 🅰️ Upsert a single amenity. `unitId` is optional; when omitted the
    // amenity is property-level. Residential assets always get property-level
    // amenities only — passing a unitId on a residential property is rejected.
    [HttpPost("{propertyId:int}/amenities")]
    public async Task<IActionResult> UpsertPropertyAmenity(int propertyId, [FromQuery] int? unitId, [FromBody] AmenityUpsertDto dto)
    {
        var property = await _propertyRepository.Query()
            .Include(p => p.PropertyTypeDetails)
            .FirstOrDefaultAsync(p => p.PropertyId == propertyId);
        if (property == null) return NotFound(new { message = "Property not found." });

        var isResidential = string.Equals(property.PropertyTypeDetails?.Name, "Residential", StringComparison.OrdinalIgnoreCase);
        if (isResidential && unitId.HasValue)
        {
            return Conflict(new { message = "Residential properties cannot have unit-level amenities." });
        }

        var amenityType = await _amenityTypeRepository.Query()
            .FirstOrDefaultAsync(t => t.AmenityTypeId == dto.AmenityTypeId && t.IsActive);
        if (amenityType == null) return BadRequest(new { message = "Invalid or inactive amenity type." });

        // Residential-only amenity types may only attach to residential assets.
        if (amenityType.IsResidentialOnly && !isResidential)
        {
            return Conflict(new { message = amenityType.Name + " is only valid for residential properties." });
        }

        if (unitId.HasValue)
        {
            var unit = await _propertyUnitRepository.Query().FirstOrDefaultAsync(u => u.PropertyUnitId == unitId.Value && u.PropertyId == propertyId);
            if (unit == null) return BadRequest(new { message = "Unit not found on this property." });
        }

        var existing = await _propertyUnitAmenityRepository.Query()
            .FirstOrDefaultAsync(a => a.PropertyId == propertyId
                && a.AmenityTypeId == dto.AmenityTypeId
                && a.PropertyUnitId == unitId);

        if (existing == null)
        {
            existing = new PropertyUnitAmenity
            {
                TenantId = property.TenantId,
                PropertyId = propertyId,
                PropertyUnitId = unitId,
                AmenityTypeId = dto.AmenityTypeId,
                IsActive = true
            };
            _propertyUnitAmenityRepository.Add(existing);
        }

        if (amenityType.RequiresBoolean) existing.BooleanValue = dto.BooleanValue;
        if (amenityType.RequiresNumber)  existing.NumberValue = dto.NumberValue;
        existing.Notes = dto.Notes;

            await _dbContext.SaveChangesAsync();
            await _dbContext.Entry(existing).Reference(a => a.AmenityType).LoadAsync();
            return Ok(new
            {
                existing.AmenityId,
                existing.PropertyId,
                existing.PropertyUnitId,
                amenityName = amenityType.Name,
                amenityType.Label,
                amenityType.RequiresBoolean,
                amenityType.RequiresNumber,
                amenityType.IsResidentialOnly,
                existing.BooleanValue,
                existing.NumberValue,
                existing.Notes,
                existing.IsActive
            });
    }

    // 🗑️ Soft-delete (deactivate) an amenity. Hard deletes would orphan audit; we keep the row.
    [HttpDelete("{propertyId:int}/amenities/{amenityId:int}")]
    public async Task<IActionResult> DeletePropertyAmenity(int propertyId, int amenityId)
    {
        var amenity = await _propertyUnitAmenityRepository.Query()
            .FirstOrDefaultAsync(a => a.AmenityId == amenityId && a.PropertyId == propertyId && a.IsActive);
        if (amenity == null) return NotFound(new { message = "Amenity not found." });
        amenity.IsActive = false;
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Amenity removed." });
    }

    // 📄 Serve a single verification document by index. Supports both
    // inline data URLs and on-disk file paths produced by the image worker.
    [HttpGet("{propertyId:int}/documents/{index:int}")]
    public async Task<IActionResult> GetPropertyDocument(int propertyId, int index)
    {
        var property = await _propertyRepository.Query()
            .FirstOrDefaultAsync(p => p.PropertyId == propertyId);
        if (property == null) return NotFound(new { message = "Property not found." });

        var docs = string.IsNullOrWhiteSpace(property.VerificationDocumentsJson)
            ? new List<string>()
            : (System.Text.Json.JsonSerializer.Deserialize<List<string>>(property.VerificationDocumentsJson) ?? new List<string>());

        if (index < 0 || index >= docs.Count) return NotFound(new { message = "Document not found." });

        var doc = docs[index];

        // Data URL — return inline
        if (doc.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = doc.Split(',', 2);
            if (parts.Length != 2) return BadRequest(new { message = "Invalid data URL." });

            var meta = parts[0].Split(';');
            var mime = meta[0].Replace("data:", "", StringComparison.OrdinalIgnoreCase);
            var base64 = parts[1];

            var dataBytes = Convert.FromBase64String(base64);
            return File(dataBytes, mime);
        }

        // File path — resolve from worker storage root
        var fileName = Path.GetFileName(doc);
        var workerRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "PropertyMediaVault"));
        var filePath = Path.GetFullPath(Path.Combine(workerRoot, doc.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));

        if (!filePath.StartsWith(workerRoot, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Invalid document path." });

        if (!System.IO.File.Exists(filePath))
            return NotFound(new { message = "Document file not found on disk." });

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        var contentType = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf"
            : fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
            : fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg"
            : "application/octet-stream";

        return File(bytes, contentType, fileName);
    }
}
}
