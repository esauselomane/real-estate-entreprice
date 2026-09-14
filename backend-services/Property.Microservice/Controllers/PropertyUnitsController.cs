using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Services;
using Property.Microservice.Services.Abstractions;
using Property.Microservice.Services.BackgroundJobs;
using System.Security.Claims;
using System.Text.Json;

namespace Property.Microservice.Controllers;

/// <summary>
/// Handles unit-level operations: sub-unit CRUD, document management,
/// maintenance calls, leases, and sole mandates.
/// </summary>
    [ApiController]
    [Authorize]
    [Route("api/properties")]
public class PropertyUnitsController : PropertyControllerBase
{
    private readonly IRepository<UnitLease> _unitLeaseRepository;
    private readonly IRepository<MaintenanceCall> _maintenanceCallRepository;
    private readonly IRepository<SoleMandate> _soleMandateRepository;

    public PropertyUnitsController(
        PropertyDbContext dbContext,
        PermissionCheckService permissionCheck,
        IPropertyRepository propertyRepository,
        IPropertyUnitRepository propertyUnitRepository,
        ILogger<PropertyUnitsController> logger,
        IRepository<UnitLease> unitLeaseRepository,
        IRepository<MaintenanceCall> maintenanceCallRepository,
        IRepository<SoleMandate> soleMandateRepository,
        IJobQueue<ImageProcessingJob> imageProcessingQueue,
        IJobQueue<SyndicationJob> syndicationQueue)
        : base(dbContext, permissionCheck, propertyRepository, propertyUnitRepository, logger, imageProcessingQueue, syndicationQueue)
    {
        _unitLeaseRepository = unitLeaseRepository;
        _maintenanceCallRepository = maintenanceCallRepository;
        _soleMandateRepository = soleMandateRepository;
    }

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

            var propertyQuery = PropertyRepository.Query().Where(p => p.PropertyId == propertyId);
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

            var query = PropertyUnitRepository.Query().Where(u => u.PropertyId == propertyId);
            if (role == "Tenant" && !string.IsNullOrWhiteSpace(userId))
            {
                var leasedUnitIds = await GetLeasedUnitIdsAsync(userId, userTenantId!);
                query = query.Where(u => leasedUnitIds.Contains(u.PropertyUnitId));
            }

            var units = await query.OrderBy(u => u.UnitNumber).ToListAsync();
            return Ok(units);
        }
        [HttpPost("{propertyId}/units")]
        public async Task<IActionResult> AddPropertyUnit(int propertyId, [FromBody] CreatePropertyUnitPayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("units:create"))
            {
                Logger.LogWarning("AddPropertyUnit: forbidden — caller lacks units:create permission");
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
                var property = await PropertyRepository.Query()
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

            PropertyUnitRepository.Add(unit);
            await DbContext.SaveChangesAsync();

            return Ok(unit);
        }
        [HttpDelete("{propertyId}/units/{unitId}")]
        public async Task<IActionResult> DeletePropertyUnit(int propertyId, int unitId)
        {
            if (!await PermissionCheck.HasPermissionAsync("units:delete"))
            {
                Logger.LogWarning("DeletePropertyUnit: forbidden — caller lacks units:delete permission");
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

            PropertyUnitRepository.Remove(unit);
            await DbContext.SaveChangesAsync();

            return Ok(new { message = "Unit successfully unlinked and deleted from property." });
        }
        [HttpPut("{propertyId}/units/{unitId:int}")]
        public async Task<IActionResult> UpdatePropertyUnit(int propertyId, int unitId, [FromBody] UpdatePropertyUnitPayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("units:edit"))
            {
                Logger.LogWarning("UpdatePropertyUnit: forbidden — caller lacks units:edit permission");
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

            await DbContext.SaveChangesAsync();
            return Ok(unit);
        }
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

            string unitFolder = Path.Combine(StorageRootPath, $"Property_{propertyId}", $"Unit_{unitId}", "Documents");
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
            await DbContext.SaveChangesAsync();

            return Ok(meta);
        }
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

            string filePath = Path.Combine(StorageRootPath, $"Property_{propertyId}", $"Unit_{unitId}", "Documents", storedName);
            if (!System.IO.File.Exists(filePath)) return NotFound(new { message = "Document file is missing from server disk." });

            byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            // Serve with the original file name so downloads keep a friendly name.
            return File(fileBytes, string.IsNullOrEmpty(doc.ContentType) ? "application/octet-stream" : doc.ContentType, doc.FileName);
        }
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

            string filePath = Path.Combine(StorageRootPath, $"Property_{propertyId}", $"Unit_{unitId}", "Documents", storedName);
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);

            docs.Remove(doc);
            unit.UnitDocumentsJson = JsonSerializer.Serialize(docs);
            await DbContext.SaveChangesAsync();

            return Ok(new { message = "Document successfully removed from unit vault." });
        }
    [HttpGet("{propertyId}/units/{unitId:int}/maintenance")]
    public async Task<IActionResult> GetUnitMaintenanceCalls(int propertyId, int unitId)
    {
        if (!await PermissionCheck.HasPermissionAsync("units:view"))
        {
            Logger.LogWarning("GetUnitMaintenanceCalls: forbidden — caller lacks units:view permission");
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
    [HttpPost("{propertyId}/units/{unitId:int}/maintenance")]
    public async Task<IActionResult> AddUnitMaintenanceCall(int propertyId, int unitId, [FromBody] MaintenanceCallPayload payload)
    {
        if (!await PermissionCheck.HasPermissionAsync("units:edit"))
        {
            Logger.LogWarning("AddUnitMaintenanceCall: forbidden — caller lacks units:edit permission");
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
        await DbContext.SaveChangesAsync();
        return Ok(call);
    }
    [HttpDelete("{propertyId}/units/{unitId:int}/maintenance/{callId:int}")]
    public async Task<IActionResult> DeleteMaintenanceCall(int callId)
    {
        if (!await PermissionCheck.HasPermissionAsync("units:delete"))
        {
            Logger.LogWarning("DeleteMaintenanceCall: forbidden — caller lacks units:delete permission");
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
            await DbContext.SaveChangesAsync();
            return Ok(new { message = "Maintenance call resolved and removed." });
        }
    [HttpGet("{propertyId}/units/{unitId:int}/leases")]
    public async Task<IActionResult> GetUnitLeases(int propertyId, int unitId)
    {
        if (!await PermissionCheck.HasPermissionAsync("units:view"))
        {
            Logger.LogWarning("GetUnitLeases: forbidden — caller lacks units:view permission");
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
    [HttpGet("{propertyId}/units/{unitId:int}/leases/active")]
    public async Task<IActionResult> GetUnitActiveLease(int propertyId, int unitId)
    {
        if (!await PermissionCheck.HasPermissionAsync("units:view"))
        {
            Logger.LogWarning("GetUnitActiveLease: forbidden — caller lacks units:view permission");
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
    [HttpPost("{propertyId}/units/{unitId:int}/leases")]
    public async Task<IActionResult> CreateUnitLease(int propertyId, int unitId, [FromBody] CreateUnitLeasePayload payload)
    {
        if (!await PermissionCheck.HasPermissionAsync("units:edit"))
        {
            Logger.LogWarning("CreateUnitLease: forbidden — caller lacks units:edit permission");
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
        await DbContext.SaveChangesAsync();
        return Ok(lease);
    }
    [HttpPut("{propertyId}/units/{unitId:int}/leases/{leaseId:int}")]
    public async Task<IActionResult> UpdateUnitLease(int propertyId, int unitId, int leaseId, [FromBody] UpdateUnitLeasePayload payload)
    {
        if (!await PermissionCheck.HasPermissionAsync("units:edit"))
        {
            Logger.LogWarning("UpdateUnitLease: forbidden — caller lacks units:edit permission");
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

        await DbContext.SaveChangesAsync();
        return Ok(lease);
    }
    [HttpDelete("{propertyId}/units/{unitId:int}/leases/{leaseId:int}")]
    public async Task<IActionResult> DeleteUnitLease(int propertyId, int unitId, int leaseId)
    {
        if (!await PermissionCheck.HasPermissionAsync("units:delete"))
        {
            Logger.LogWarning("DeleteUnitLease: forbidden — caller lacks units:delete permission");
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
        await DbContext.SaveChangesAsync();
        return Ok(new { message = "Lease removed." });
    }
    [HttpGet("{propertyId:int}/mandates")]
    public async Task<IActionResult> GetSoleMandates(int propertyId)
    {
        var userTenantId = User.FindFirst("TenantId")?.Value;
        if (string.IsNullOrWhiteSpace(userTenantId)) return Forbid();

        var property = await PropertyRepository.Query()
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
    [HttpPost("{propertyId:int}/mandates")]
    public async Task<IActionResult> CreateSoleMandate(int propertyId, [FromBody] CreateSoleMandatePayload payload)
    {
        if (!await PermissionCheck.HasPermissionAsync("properties:edit"))
        {
            Logger.LogWarning("CreateSoleMandate: forbidden — caller lacks properties:edit permission");
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
        var propertyQuery = PropertyRepository.Query().Where(p => p.PropertyId == propertyId);
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
        await DbContext.SaveChangesAsync();

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
    [HttpDelete("{propertyId:int}/mandates/{mandateId:int}")]
    public async Task<IActionResult> CloseSoleMandate(int propertyId, int mandateId)
    {
        if (!await PermissionCheck.HasPermissionAsync("properties:edit"))
        {
            Logger.LogWarning("CloseSoleMandate: forbidden — caller lacks properties:edit permission");
            return Forbid();
        }
        var mandate = await _soleMandateRepository.Query()
            .FirstOrDefaultAsync(m => m.PropertyId == propertyId && m.SoleMandateId == mandateId);
        if (mandate == null) return NotFound(new { message = "Mandate not found." });
        if (!mandate.IsActive) return Ok(new { message = "Mandate is already closed." });

        mandate.IsActive = false;
        mandate.ClosedAt = DateTime.UtcNow;
        await DbContext.SaveChangesAsync();
        return Ok(new { message = "Mandate closed." });
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
}
