import { useEffect, useRef, useState } from 'react';

import { loadGoogleMaps } from './GoogleMapsLoader.js';

async function loadPlaces() {
  const importLibrary = await loadGoogleMaps();
  return importLibrary('places');
}

export default function GoogleAddressPicker({ onSelect }) {
  const container = useRef(null);
  const selectionHandler = useRef(onSelect);
  selectionHandler.current = onSelect;
  const [message, setMessage] = useState('Loading Google address search…');
  useEffect(() => {
    let disposed = false;
    let widget;
    async function selectPlace(event) {
      setMessage('Retrieving coordinates…');
      try {
        const place = event.placePrediction.toPlace();
        await place.fetchFields({ fields: ['formattedAddress', 'location'] });
        if (disposed) return;
        if (!place.location) throw new Error('This address has no coordinates. Select another result.');
        selectionHandler.current({ completeAddress: place.formattedAddress, latitude: place.location.lat(), longitude: place.location.lng() });
        setMessage('Address and coordinates updated.');
      } catch { if (!disposed) setMessage('Could not retrieve coordinates. Select another result or enter them manually.'); }
    }
    loadPlaces().then(({ PlaceAutocompleteElement }) => {
      if (disposed) return;
      widget = new PlaceAutocompleteElement({ includedRegionCodes: ['za'] });
      widget.setAttribute('aria-label', 'Search property address with Google');
      widget.addEventListener('gmp-select', selectPlace);
      widget.addEventListener('gmp-error', () => setMessage('Google address search is unavailable. Check API access or enter coordinates manually.'));
      container.current.appendChild(widget);
      setMessage('Select a Google result to fill the address and coordinates.');
    }).catch(error => { if (!disposed) setMessage(error.message); });
    return () => { disposed = true; widget?.remove(); };
  }, []);
  return <div className="col-span-full space-y-1"><div ref={container} /><p role="status" className="text-xs text-slate-400">{message}</p></div>;
}
