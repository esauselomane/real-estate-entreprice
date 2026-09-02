import apiClient from './apiClient.js';

export const propertyService = {

  // --- 🔍 MULTI-CRITERIA REAL ESTATE SEARCH PIPELINE ---
  searchProperties: async (locationsArray, propertyType, minSize, maxSize, listingType = 'All', page = 1) => {
    try {
      const criteriaPayload = {
        locations: locationsArray,
        propertyType: propertyType,
        minSize: minSize ? parseInt(minSize, 10) : null,
        maxSize: maxSize ? parseInt(maxSize, 10) : null,
        listingType: listingType
      };

      const response = await apiClient.post('/properties/search?page=' + page, criteriaPayload);
      const data = response.data;
      return Object.assign(data, { totalCount: Number(response.headers['x-total-count'] || 0) });
    } catch (error) {
      throw error.response?.data?.message || 'Search execution failed on the database engine.';
    }
  },

  // --- 🔍 GET SINGLE PROPERTY WITH LINKED UNITS ---
  getPropertyById: async (propertyId) => {
    try {
      const response = await apiClient.get(`/properties/${propertyId}`);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Failed to fetch property details.';
    }
  },

  updateProperty: async (propertyId, propertyData) => {
    try {
      const response = await apiClient.put(`/properties/${propertyId}`, propertyData);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Failed to save property details.';
    }
  },

  // --- 🏢 GET SUB-UNITS LINKED TO PROPERTY ---
  getPropertyUnits: async (propertyId) => {
    try {
      const response = await apiClient.get(`/properties/${propertyId}/units`);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Failed to fetch property units.';
    }
  },

  // --- 📋 LIST A UNIT'S STORED DOCUMENTS (read-only; role-scoped) ---
  getUnitDocuments: async (propertyId, unitId) => {
    try {
      const response = await apiClient.get(`/properties/${propertyId}/units/${unitId}/documents`);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Failed to load unit documents.';
    }
  },

  // --- ➕ ADD NEW SUB-UNIT TO PROPERTY ---
  addPropertyUnit: async (propertyId, unitData) => {
    try {
      const response = await apiClient.post(`/properties/${propertyId}/units`, unitData);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Failed to add unit to property.';
    }
  },

   // --- ❌ DELETE SUB-UNIT FROM PROPERTY ---
   deletePropertyUnit: async (propertyId, unitId) => {
     try {
       const response = await apiClient.delete(`/properties/${propertyId}/units/${unitId}`);
       return response.data;
     } catch (error) {
       throw error.response?.data?.message || 'Failed to delete unit.';
     }
   },

   // --- ✏️ UPDATE EXISTING SUB-UNIT (editable unit details on the property page) ---
   updatePropertyUnit: async (propertyId, unitId, unitData) => {
     try {
       const response = await apiClient.put(`/properties/${propertyId}/units/${unitId}`, unitData);
       return response.data;
     } catch (error) {
       throw error.response?.data?.message || 'Failed to update unit details.';
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
       throw error.response?.data?.message || 'Failed to upload unit document.';
     }
   },

   // --- 📄 DOWNLOAD A UNIT DOCUMENT (returns a Blob for a client-side save) ---
   downloadPropertyUnitDocument: async (propertyId, unitId, storedName) => {
     try {
       const response = await apiClient.get(`/properties/${propertyId}/units/${unitId}/documents/${encodeURIComponent(storedName)}`, { responseType: 'blob' });
       return response.data;
     } catch (error) {
       throw error.response?.data?.message || 'Failed to download unit document.';
     }
   },

    // --- ❌ DELETE A UNIT DOCUMENT FROM THE VAULT ---
    deletePropertyUnitDocument: async (propertyId, unitId, storedName) => {
      try {
        const response = await apiClient.delete(`/properties/${propertyId}/units/${unitId}/documents/${encodeURIComponent(storedName)}`);
        return response.data;
      } catch (error) {
        throw error.response?.data?.message || 'Failed to delete unit document.';
      }
    },

    // --- 🛠️ MAINTENANCE CALLS ON A UNIT (tenant-logged) ---
    getUnitMaintenanceCalls: async (propertyId, unitId) => {
      try {
        const response = await apiClient.get(`/properties/${propertyId}/units/${unitId}/maintenance`);
        return response.data;
      } catch (error) {
        throw error.response?.data?.message || 'Failed to load maintenance calls.';
      }
    },

    addUnitMaintenanceCall: async (propertyId, unitId, payload) => {
      try {
        const response = await apiClient.post(`/properties/${propertyId}/units/${unitId}/maintenance`, payload);
        return response.data;
      } catch (error) {
        throw error.response?.data?.message || 'Failed to log maintenance call.';
      }
    },

    deleteUnitMaintenanceCall: async (propertyId, unitId, callId) => {
      try {
        const response = await apiClient.delete(`/properties/${propertyId}/units/${unitId}/maintenance/${callId}`);
        return response.data;
      } catch (error) {
        throw error.response?.data?.message || 'Failed to remove maintenance call.';
      }
    },


  // --- 📋 GET SYNDICATION PLATFORMS ---
  getPlatforms: async () => {
    try {
      const response = await apiClient.get('/properties/platforms');
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Failed to fetch platforms.';
    }
  },

  // --- 🏠 REGISTER NEW PROPERTY INVENTORY RECORD ---
  createProperty: async (propertyData) => {
    try {
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      
      const payload = {
        ...propertyData,
        tenantId: userProfile.tenantId // Enforce parent multi-tenant sandbox boundary
      };

      const response = await apiClient.post('/properties/create', payload);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Failed to register the property asset record.';
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
      throw error || 'Failed to dispatch image file binary stream to Kafka pipeline.';
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
      throw error.response?.data?.message || 'Portal syndication tracker check out of sync.';
    }
  }
  ,
  dispatchBrochureGenerationTask: async (payload) => {
    try {
      const response = await apiClient.post('/properties/brochure/generate', payload);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Brochure generation request failed.';
    }
  },

  updateProperty: async (propertyId, payload) => {
    try {
      const response = await apiClient.put(`/properties/${propertyId}`, payload);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Failed to update property.';
    }
  }
};
