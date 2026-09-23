let mapsPromise;

export function loadGoogleMaps() {
  if (typeof window === 'undefined') {
    return Promise.reject(new Error('Google Maps is only available in the browser.'));
  }

  if (window.google?.maps?.importLibrary) {
    return Promise.resolve(window.google.maps.importLibrary);
  }

  if (mapsPromise) {
    return mapsPromise;
  }

  mapsPromise = new Promise((resolve, reject) => {
    const apiKey = import.meta.env.VITE_GOOGLE_MAPS_API_KEY;

    if (!apiKey) {
      mapsPromise = null;
      reject(new Error('Google Maps is not configured. Check VITE_GOOGLE_MAPS_API_KEY.'));
      return;
    }

    const callbackName = 'propertyMapsReady';
    const scriptUrl = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&libraries=maps,places,geocoding&v=weekly&loading=async&callback=${callbackName}`;
    const existingScript = Array.from(document.scripts).find((script) => script.src === scriptUrl);

    const resolveMaps = () => {
      if (window.google?.maps?.importLibrary) {
        resolve(window.google.maps.importLibrary);
        return;
      }

      reject(new Error('Google Maps loaded without the Maps JavaScript API.'));
    };

    const rejectMapsLoad = () => {
      document.querySelector('script[data-google-maps-library="property-map"]')?.remove();
      reject(new Error('Google Maps could not load. Check the API key and network connection.'));
    };

    window[callbackName] = resolveMaps;

    if (existingScript) {
      existingScript.addEventListener('error', rejectMapsLoad, { once: true });
      if (window.google?.maps?.importLibrary) {
        resolveMaps();
      }
      return;
    }

    const script = document.createElement('script');
    script.src = scriptUrl;
    script.async = true;
    script.dataset.googleMapsLibrary = 'property-map';
    // With loading=async, only the API callback signals readiness.
    script.onerror = rejectMapsLoad;
    document.head.appendChild(script);
  });

  mapsPromise = mapsPromise.catch(error => {
    mapsPromise = null;
    throw error;
  });
  return mapsPromise;
}
