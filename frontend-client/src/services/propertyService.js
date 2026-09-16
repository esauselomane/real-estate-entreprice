const toMessage = (err, fallback) => { if (!err) return fallback; if (typeof err === 'string') return err; if (err.message) return err.message; try { return JSON.stringify(err); } catch { return fallback; } };

import apiClient from './apiClient.js';

const ACTIVE_TENANT_STORAGE_KEY = 'active_tenant_override';
const ACTIVE_TENANT_STORAGE_MARKER = 'view-override';
const USER_PROFILE_STORAGE_KEY = 'user_profile_data';

function resolveActiveTenantOverride() {
  try {
    const raw = localStorage.getItem(ACTIVE_TENANT_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    if (!parsed || typeof parsed !== 'object') return null;
    if (parsed.__source !== ACTIVE_TENANT_STORAGE_MARKER) return null;
    if (parsed.__all__) return '__all__';
    return parsed.tenantId || null;
  } catch { return null; }
}

function resolveLoggedInTenantId() {
  try {
    const raw = localStorage.getItem(USER_PROFILE_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    return parsed?.tenantId || null;
  } catch { return null; }
}

// 🌳 The property service is partition-by-TenantId but doesn't know about
// the agency hierarchy. The CRM expands the active agency's subtree
// (parent + descendants for a top-level, just itself for a child) and
// TenantContext caches the resulting TenantId list keyed by agencyId. We
// read that cache here so the property search can be sent a
// comma-separated ?tenantId= list. Returns null when the override is
// "__all__" / unset (PlatformAdmin unfiltered) or the cache hasn't been
// populated yet.
function resolvePropertyTenantIdList() {
  try {
    const overrideRaw = localStorage.getItem(ACTIVE_TENANT_STORAGE_KEY);
    if (!overrideRaw) return null;
    const override = JSON.parse(overrideRaw);
    if (!override || override.__source !== ACTIVE_TENANT_STORAGE_MARKER) return null;
    if (override.__all__) return null;
    if (override.agencyId == null) return null;
    const cachedRaw = localStorage.getItem('property_subtree_cache');
    if (!cachedRaw) return null;
    const cached = JSON.parse(cachedRaw) || {};
    const list = cached[override.agencyId];
    if (!Array.isArray(list) || list.length === 0) return null;
    return list.join(',');
  } catch { return null; }
}

export const propertyService = {
  // 🌐 Read params: prefer the active-tenant override; fall back to the
  // logged-in user's tenant so backend filters always see a tenant id.
  // When the override is a specific agency, the property service receives
  // a comma-separated list of TenantIds covering the agency's full
  // subtree (parent + descendants for a top-level, just itself for a
  // child) so dashboard aggregates reflect the right scope.
  _tenantQueryParams: () => {
    const override = resolveActiveTenantOverride();
    if (override === '__all__') return {};
    const subtreeList = resolvePropertyTenantIdList();
    if (subtreeList) return { tenantId: subtreeList };
    if (override) return { tenantId: override };
    const profile = JSON.parse(localStorage.getItem('user_profile_data') || 'null');
      if (['PlatformAdmin', 'SuperAdmin'].includes(profile?.role)) return {};
      const fallback = resolveLoggedInTenantId();
    return fallback ? { tenantId: fallback } : {};
  },

  // 🛰️ Resolve the tenant id a write should be stamped with. "__all__" is
  // never a valid stamp — that mode is read-only.
  _resolveWriteTenantId: () => {
    const override = resolveActiveTenantOverride();
    if (override && override !== '__all__') return override;
    return resolveLoggedInTenantId();
  },

  // --- 🔍 MULTI-CRITERIA REAL ESTATE SEARCH PIPELINE ---
  searchProperties: async (locationsArray, propertyType, minSize, maxSize, listingType = 'All', page = 1, pageSize = null) => {
    try {
      const criteriaPayload = {
        locations: locationsArray,
        propertyType: propertyType,
        minSize: minSize ? parseInt(minSize, 10) : null,
        maxSize: maxSize ? parseInt(maxSize, 10) : null,
        listingType: listingType
      };

      // 🛰️ TenantSwitcher override: the comma-separated subtree list (or
      // single TenantId) belongs on the URL as a query string, not in the
      // body. The backend reads it from HttpContext.Request.Query so the
      // URL is the only place it can be read. If this is empty, the
      // property service unfiltered-queries (PlatformAdmin sees every
      // property, which is why the map never appeared to change).
      const overrideParams = propertyService._tenantQueryParams();
      const queryParams = { page, ...overrideParams };
      if (pageSize && pageSize > 0) queryParams.pageSize = pageSize;
      const queryString = new URLSearchParams(queryParams).toString();
      // 🪵 DEBUG — leave in for the next round of troubleshooting. Will
      // show: (1) the raw override from localStorage, (2) the subtree
      // cache, (3) the final URL we're POSTing to.
      console.debug('[propertyService.searchProperties] override=', resolveActiveTenantOverride(),
        'subtreeCache=', localStorage.getItem('property_subtree_cache'),
        'finalUrl=', `/properties/search?${queryString}`,
        'body=', criteriaPayload);

      const response = await apiClient.post(`/properties/search?${queryString}`, criteriaPayload);
      const data = response.data;
      return Object.assign(data, { totalCount: Number(response.headers['x-total-count'] || 0) });
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Search execution failed on the database engine.'));
    }
  },

  // --- 🔍 GET SINGLE PROPERTY WITH LINKED UNITS ---
  getPropertyById: async (propertyId) => {
    try {
      // 🎯 Single-record fetch — no tenant override. The list view
      // (searchProperties) is the one that respects the active tenant
      // filter; once a user clicks a property they should be able to
      // open it regardless of the current switcher state. The backend
      // still applies the role-based visibility rules (a Tenant can
      // only open properties they have a lease on, etc.), so this is
      // safe — the override was only ever used as a *scope-narrowing*
      // signal, not an *authorization* signal.
      const response = await apiClient.get(`/properties/${propertyId}`);
      // The backend now wraps the property in { property, activeSoleMandate }
      // so consumers don't need a second round-trip to render the
      // obligation/expiry banners. Flatten the property back out so
      // existing call-sites keep working, and tack the mandate on as
      // an extra field.
      const payload = response.data || {};
      const property = payload.property || payload;
      return Object.assign(property, { activeSoleMandate: payload.activeSoleMandate || null });
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Failed to load the property asset.'));
    }
  },

  // 📜 Sole-mandate helpers. One property can have many SoleMandates
  // over time but only one is active at any moment (enforced on the
  // server). The active mandate is included on getPropertyById so the
  // UI doesn't have to call listMandates separately for banner logic.
  listSoleMandates: async (propertyId) => {
    try {
      const response = await apiClient.get(`/properties/${propertyId}/mandates`);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Failed to load sole mandates.'));
    }
  },

  createSoleMandate: async (propertyId, { startDate, endDate }) => {
    try {
      const response = await apiClient.post(`/properties/${propertyId}/mandates`, { startDate, endDate });
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Failed to open sole mandate.'));
    }
  },

  closeSoleMandate: async (propertyId, mandateId) => {
    try {
      const response = await apiClient.delete(`/properties/${propertyId}/mandates/${mandateId}`);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Failed to close sole mandate.'));
    }
  },

  updateProperty: async (propertyId, propertyData) => {
    try {
      const response = await apiClient.put(`/properties/${propertyId}`, propertyData);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Failed to save property details.'));
    }
  },

  // --- 🏢 GET SUB-UNITS LINKED TO PROPERTY ---
  getPropertyUnits: async (propertyId) => {
    try {
      // No override here either — once a property is open the user
      // should be able to see its sub-units, period. The backend
      // already enforces the per-unit visibility rules (Tenants only
      // see units they have an active lease on, etc.).
      const response = await apiClient.get(`/properties/${propertyId}/units`);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Failed to fetch property units.'));
    }
  },

   // --- 📦 GET PROPERTY AMENITIES ---
   getPropertyAmenities: async (propertyId) => {
     try {
       const response = await apiClient.get(`/properties/${propertyId}/amenities`);
       return response.data;
     } catch (error) {
       throw new Error(toMessage(error.response?.data?.message, 'Failed to load property amenities.'));
     }
   },

   // --- 📋 LIST A UNIT'S STORED DOCUMENTS (read-only; role-scoped) ---
   getUnitDocuments: async (propertyId, unitId) => {
    try {
      // See getPropertyById — once a property/unit is open the user
      // should be able to see its records.
      const response = await apiClient.get(`/properties/${propertyId}/units/${unitId}/documents`);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Failed to load unit documents.'));
    }
  },

  // --- ➕ ADD NEW SUB-UNIT TO PROPERTY ---
  addPropertyUnit: async (propertyId, unitData) => {
    try {
      const response = await apiClient.post(`/properties/${propertyId}/units`, unitData);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Failed to add unit to property.'));
    }
  },

   // --- ❌ DELETE SUB-UNIT FROM PROPERTY ---
   deletePropertyUnit: async (propertyId, unitId) => {
     try {
       const response = await apiClient.delete(`/properties/${propertyId}/units/${unitId}`);
       return response.data;
     } catch (error) {
       throw new Error(toMessage(error.response?.data?.message, 'Failed to delete unit.'));
     }
   },

   // --- ✏️ UPDATE EXISTING SUB-UNIT (editable unit details on the property page) ---
   updatePropertyUnit: async (propertyId, unitId, unitData) => {
     try {
       const response = await apiClient.put(`/properties/${propertyId}/units/${unitId}`, unitData);
       return response.data;
     } catch (error) {
       throw new Error(toMessage(error.response?.data?.message, 'Failed to update unit details.'));
     }
   },

   // --- 📎 UPLOAD A UNIT DOCUMENT (name + description + actual file saved to server disk; auto-bound to unit) ---
   uploadPropertyUnitDocument: async (propertyId, unitId, file, { name, description }) => {
     try {
       const convertToBase64 = (file) => new Promise((resolve, reject) => {
         const reader = new FileReader();
         reader.readAsDataURL(file);
         reader.onload = () => resolve(reader.result.split(',')[1]);
         reader.onerror = (error) => reject(error);
       });
       const base64Data = await convertToBase64(file);
       const payload = {
         name: name,
         description: description || '',
         fileName: file.name,
         contentType: file.type || '',
         base64DataString: base64Data
       };
       const response = await apiClient.post(`/properties/${propertyId}/units/${unitId}/documents`, payload);
       return response.data;
     } catch (error) {
       throw new Error(toMessage(error.response?.data?.message, 'Failed to upload unit document.'));
     }
   },

   // --- 📄 DOWNLOAD A UNIT DOCUMENT (returns a Blob for a client-side save) ---
   downloadPropertyUnitDocument: async (propertyId, unitId, storedName) => {
     try {
       const response = await apiClient.get(`/properties/${propertyId}/units/${unitId}/documents/${encodeURIComponent(storedName)}`, { responseType: 'blob' });
       return response.data;
     } catch (error) {
       throw new Error(toMessage(error.response?.data?.message, 'Failed to download unit document.'));
     }
   },

    // --- ❌ DELETE A UNIT DOCUMENT FROM THE VAULT ---
    deletePropertyUnitDocument: async (propertyId, unitId, storedName) => {
      try {
        const response = await apiClient.delete(`/properties/${propertyId}/units/${unitId}/documents/${encodeURIComponent(storedName)}`);
        return response.data;
      } catch (error) {
        throw new Error(toMessage(error.response?.data?.message, 'Failed to delete unit document.'));
      }
    },

    // --- 🛠️ MAINTENANCE CALLS ON A UNIT (tenant-logged) ---
    getUnitMaintenanceCalls: async (propertyId, unitId) => {
      try {
        // See getPropertyById — single-record fetches don't carry the
        // tenant override so the user isn't blocked by switcher state.
        const response = await apiClient.get(`/properties/${propertyId}/units/${unitId}/maintenance`);
        return response.data;
      } catch (error) {
        throw new Error(toMessage(error.response?.data?.message, 'Failed to load maintenance calls.'));
      }
    },

    addUnitMaintenanceCall: async (propertyId, unitId, payload) => {
      try {
        const response = await apiClient.post(`/properties/${propertyId}/units/${unitId}/maintenance`, payload);
        return response.data;
      } catch (error) {
        throw new Error(toMessage(error.response?.data?.message, 'Failed to log maintenance call.'));
      }
    },

    deleteUnitMaintenanceCall: async (propertyId, unitId, callId) => {
      try {
        const response = await apiClient.delete(`/properties/${propertyId}/units/${unitId}/maintenance/${callId}`);
        return response.data;
      } catch (error) {
        throw new Error(toMessage(error.response?.data?.message, 'Failed to remove maintenance call.'));
      }
    },


  // --- 📜 UNIT LEASE LEDGER (Lease + History tabs) ---
  // All endpoints are role-scoped on the backend; the frontend never
  // appends the tenant override because the backend resolves visibility
  // from the JWT + the unit's propertyId (the same model used for
  // single-record maintenance/document reads).
  getUnitLeases: async (propertyId, unitId) => {
    try {
      const response = await apiClient.get(`/properties/${propertyId}/units/${unitId}/leases`);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Failed to load lease history.'));
    }
  },

  getUnitActiveLease: async (propertyId, unitId) => {
    try {
      const response = await apiClient.get(`/properties/${propertyId}/units/${unitId}/leases/active`);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Failed to load active lease.'));
    }
  },

  createUnitLease: async (propertyId, unitId, payload) => {
    try {
      const response = await apiClient.post(`/properties/${propertyId}/units/${unitId}/leases`, payload);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Failed to create lease.'));
    }
  },

  updateUnitLease: async (propertyId, unitId, leaseId, payload) => {
    try {
      const response = await apiClient.put(`/properties/${propertyId}/units/${unitId}/leases/${leaseId}`, payload);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Failed to update lease.'));
    }
  },

  deleteUnitLease: async (propertyId, unitId, leaseId) => {
    try {
      const response = await apiClient.delete(`/properties/${propertyId}/units/${unitId}/leases/${leaseId}`);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Failed to remove lease.'));
    }
  },


  // --- 📋 GET SYNDICATION PLATFORMS ---
  getPlatforms: async () => {
    try {
      const response = await apiClient.get('/properties/platforms', { params: propertyService._tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Failed to fetch platforms.'));
    }
  },

  // --- 🏠 REGISTER NEW PROPERTY INVENTORY RECORD ---
  createProperty: async (propertyData) => {
    try {
      const payload = {
        ...propertyData,
        // Enforce parent multi-tenant sandbox boundary: active override wins
        // over the logged-in user's tenantId, "__all__" mode is read-only.
        tenantId: propertyService._resolveWriteTenantId() || propertyData.tenantId
      };

      const response = await apiClient.post('/properties/create', payload);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Failed to register the property asset record.'));
    }
  },
  // --- 🖼️ KAFKA-BACKED IMAGE STREAMING FILE UPLOADER ---
  uploadPropertyImage: async (propertyId, fileObject) => {
    try {
      // Helper function to transform physical files into Base64 strings for network streaming
      const convertToBase64 = (file) => {
        return new Promise((resolve, reject) => {
          const fileReader = new FileReader();
          fileReader.readAsDataURL(file);
          fileReader.onload = () => {
            // Strip out the data url metadata prefix block (e.g. "data:image/jpeg;base64,")
            const base64String = fileReader.result.split(',')[1];
            resolve(base64String);
          };
          fileReader.onerror = (error) => reject(error);
        });
      };

      const base64Data = await convertToBase64(fileObject);

      const payload = {
        fileName: fileObject.name,
        base64DataString: base64Data
      };

      const response = await apiClient.post(`/properties/${propertyId}/upload-image`, payload);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error, 'Failed to dispatch image file binary stream to Kafka pipeline.'));
    }
  },

  // --- 📄 DEEDS & VERIFICATION DOCUMENT VAULT (property-level) ---

    // Upload a new verification document into the property's deeds vault.
    // The file is sent as multipart/form-data so the backend can enforce
    // size and format limits server-side.
    downloadPropertyDocument: async (propertyId, index) => {
      const response = await apiClient.get(`/properties/${propertyId}/documents/${index}`, { responseType: 'blob' });
      return response.data;
    },

    uploadPropertyDocument: async (propertyId, file, { name, description } = {}) => {
      try {
        const formData = new FormData();
        formData.append('File', file);
        if (name) formData.append('Name', name);
        if (description) formData.append('Description', description);
        const response = await apiClient.post(`/properties/${propertyId}/documents`, formData, {
          headers: { 'Content-Type': 'multipart/form-data' }
        });
        return response.data;
      } catch (error) {
        throw new Error(toMessage(error.response?.data?.message, 'Failed to upload verification document.'));
      }
    },

    // Delete a verification document from the property's deeds vault.
    deletePropertyDocument: async (propertyId, index) => {
      try {
        const response = await apiClient.delete(`/properties/${propertyId}/documents/${index}`);
        return response.data;
      } catch (error) {
        throw new Error(toMessage(error.response?.data?.message, 'Failed to delete verification document.'));
      }
    },

  // --- 🔗 EXTERNAL PORTAL SYNDICATION CHECKBOX TRACKER TOGGLE ---
  togglePortalSyndication: async (propertyId, platformId, syndicateStatus) => {
    try {
      const payload = {
        propertyId: parseInt(propertyId, 10),
        platformId: parseInt(platformId, 10),
        syndicateStatus: syndicateStatus // Boolean value mapping to UI checkboxes
      };

      const response = await apiClient.post('/properties/syndicate', payload);
      return response.data; // Returns external reference numbers and live portal links
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Portal syndication tracker check out of sync.'));
    }
  }
  ,
  dispatchBrochureGenerationTask: async (payload) => {
    try {
      const response = await apiClient.post('/properties/brochure/generate', payload);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Brochure generation request failed.'));
    }
  },

  // 🏠/🏢 PROPERTY + UNIT AMENITIES
  getAmenities: async (propertyId) => {
    try {
      const response = await apiClient.get(`/properties/${propertyId}/amenities`, { params: propertyService._tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Amenities sync failed.'));
    }
  },

  upsertAmenity: async (propertyId, payload, unitId = null) => {
    try {
      const params = { ...propertyService._tenantQueryParams() };
      if (unitId) params.unitId = unitId;
      const response = await apiClient.post(`/properties/${propertyId}/amenities`, payload, { params });
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Amenity save failed.'));
    }
  },

  deleteAmenity: async (propertyId, amenityId) => {
    try {
      const response = await apiClient.delete(`/properties/${propertyId}/amenities/${amenityId}`);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Amenity delete failed.'));
    }
  },

  // 📚 SuperAdmin amenity catalogue
  getAmenityTypes: async () => {
    try {
      const response = await apiClient.get('/properties/amenity-types', { params: propertyService._tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Catalogue sync failed.'));
    }
  },

  getAllAmenityTypes: async () => {
    try {
      const response = await apiClient.get('/properties/amenity-types/all', { params: propertyService._tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Catalogue sync failed.'));
    }
  },

  createAmenityType: async (payload) => {
    try {
      const response = await apiClient.post('/properties/amenity-types', payload);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Amenity type create failed.'));
    }
  },

  updateAmenityType: async (id, payload) => {
    try {
      const response = await apiClient.put(`/properties/amenity-types/${id}`, payload);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Amenity type update failed.'));
    }
  },

  deleteAmenityType: async (id) => {
    try {
      const response = await apiClient.delete(`/properties/amenity-types/${id}`);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Amenity type delete failed.'));
    }
  }
};
