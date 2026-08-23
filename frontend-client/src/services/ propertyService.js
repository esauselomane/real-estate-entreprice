import apiClient from './ apiClient.js';

export const propertyService = {

  // --- 🔍 MULTI-CRITERIA REAL ESTATE SEARCH PIPELINE ---
  searchProperties: async (locationsArray, propertyType, minSize, maxSize) => {
    try {
      const criteriaPayload = {
        locations: locationsArray, // E.g., ['Green Point', 'Sandton']
        propertyType: propertyType, // E.g., 'Residential', 'Commercial', 'Retail', 'All'
        minSize: minSize ? parseInt(minSize, 10) : null,
        maxSize: maxSize ? parseInt(maxSize, 10) : null
      };

      const response = await apiClient.post('/properties/search', criteriaPayload);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Search execution failed on the database engine.';
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
};
