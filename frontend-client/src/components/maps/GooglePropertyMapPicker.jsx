import { useCallback, useEffect, useRef, useState } from 'react';
import { Loader2, MapPin, Search } from 'lucide-react';
import { loadGoogleMaps } from './GoogleMapsLoader.js';

const DEFAULT_CENTER = { lat: -33.9249, lng: 18.4207 };

function hasValidCoordinates(latitude, longitude) {
  if (latitude == null || longitude == null || String(latitude).trim() === '' || String(longitude).trim() === '') {
    return false;
  }

  const numericLatitude = Number(latitude);
  const numericLongitude = Number(longitude);
  return Number.isFinite(numericLatitude) && Number.isFinite(numericLongitude) &&
    Math.abs(numericLatitude) <= 90 && Math.abs(numericLongitude) <= 180;
}

function toCoordinateString(value) {
  return value === null || value === undefined || value === '' ? '' : String(value);
}

export default function GooglePropertyMapPicker({
  address = '',
  latitude = '',
  longitude = '',
  onChange,
  required = false,
  height = 320,
}) {
  const inputRef = useRef(null);
  const mapElementRef = useRef(null);
  const mapRef = useRef(null);
  const markerRef = useRef(null);
  const autocompleteRef = useRef(null);
  const geocoderRef = useRef(null);
  const listenerRefs = useRef([]);
  const onChangeRef = useRef(onChange);
  const addressRef = useRef(address);
  const pendingLocationRef = useRef(null);
  const reverseGeocodeRequestRef = useRef(0);
  const disposedRef = useRef(false);
  const initialLocationRef = useRef({ address, latitude, longitude });
  const [statusMessage, setStatusMessage] = useState('Loading Google Maps…');
  const [isLocating, setIsLocating] = useState(false);

  const publishLocation = useCallback((nextAddress, nextLatitude, nextLongitude, message) => {
    if (disposedRef.current) return;

    const normalizedAddress = nextAddress || '';
    const normalizedLatitude = toCoordinateString(nextLatitude);
    const normalizedLongitude = toCoordinateString(nextLongitude);

    addressRef.current = normalizedAddress;
    onChangeRef.current?.({
      completeAddress: normalizedAddress,
      latitude: normalizedLatitude,
      longitude: normalizedLongitude,
    });
    setStatusMessage(message);
  }, []);

  const reverseGeocodeLocation = useCallback((nextLatitude, nextLongitude) => {
    const geocoder = geocoderRef.current;
    const requestId = reverseGeocodeRequestRef.current + 1;
    reverseGeocodeRequestRef.current = requestId;

    if (!geocoder) {
      publishLocation(addressRef.current, nextLatitude, nextLongitude, 'Pin moved. Address resolution is unavailable.');
      return;
    }

    setIsLocating(true);
    setStatusMessage('Resolving the dropped pin…');
    geocoder.geocode({ location: { lat: Number(nextLatitude), lng: Number(nextLongitude) } }, (results) => {
      if (disposedRef.current || reverseGeocodeRequestRef.current !== requestId) return;
      setIsLocating(false);

      const selectedResult = results?.find((result) => result.formatted_address);
      const resolvedAddress = selectedResult?.formatted_address || addressRef.current;
      publishLocation(
        resolvedAddress,
        nextLatitude,
        nextLongitude,
        selectedResult ? 'Address and coordinates updated from the dropped pin.' : 'Pin coordinates updated. Address resolution returned no result.'
      );
    });
  }, [publishLocation]);

  const handleMarkerDragEnd = useCallback(() => {
    const marker = markerRef.current;
    const position = marker?.getPosition();
    if (!position) return;
    reverseGeocodeLocation(position.lat(), position.lng());
  }, [reverseGeocodeLocation]);

  const moveMarker = useCallback((nextLatitude, nextLongitude, shouldZoom) => {
    const map = mapRef.current;
    const marker = markerRef.current;
    const position = { lat: Number(nextLatitude), lng: Number(nextLongitude) };

    if (!map || !window.google?.maps) return false;

    if (marker) {
      marker.setPosition(position);
      marker.setTitle(addressRef.current || 'Selected property location');
    } else {
      markerRef.current = new window.google.maps.Marker({
        map,
        position,
        draggable: true,
        title: addressRef.current || 'Selected property location',
      });
      listenerRefs.current.push(markerRef.current.addListener('dragend', handleMarkerDragEnd));
    }

    map.panTo(position);
    if (shouldZoom) map.setZoom(16);
    return true;
  }, [handleMarkerDragEnd]);

  const handleMapClick = useCallback((event) => {
    const position = event.latLng;
    if (!position) return;
    const nextLatitude = position.lat();
    const nextLongitude = position.lng();
    moveMarker(nextLatitude, nextLongitude, false);
    reverseGeocodeLocation(nextLatitude, nextLongitude);
  }, [moveMarker, reverseGeocodeLocation]);

  const handlePlaceChanged = useCallback(() => {
    const autocomplete = autocompleteRef.current;
    const place = autocomplete?.getPlace();
    const position = place?.geometry?.location;

    if (!position) {
      setStatusMessage('Select a complete Google address result.');
      return;
    }

    const nextAddress = place.formatted_address || place.name || addressRef.current;
    publishLocation(nextAddress, position.lat(), position.lng(), 'Address and coordinates updated from Google.');
    moveMarker(position.lat(), position.lng(), true);
  }, [moveMarker, publishLocation]);

  const handleAddressLookup = useCallback(() => {
    const geocoder = geocoderRef.current;
    const query = inputRef.current?.value.trim() || '';

    if (!query) {
      setStatusMessage('Enter an address before searching.');
      inputRef.current?.focus();
      return;
    }

    if (!geocoder) {
      setStatusMessage('Google address lookup is still loading.');
      return;
    }

    setIsLocating(true);
    setStatusMessage('Searching for the address…');
    geocoder.geocode({ address: query }, (results, geocodeStatus) => {
      if (disposedRef.current) return;
      setIsLocating(false);
      const selectedResult = results?.find((result) => result.geometry?.location);
      const position = selectedResult?.geometry?.location;

      if (!position) {
        setStatusMessage(geocodeStatus === 'ZERO_RESULTS' ? 'No Google address matched that search.' : 'Google could not resolve that address.');
        return;
      }

      const resolvedAddress = selectedResult.formatted_address || query;
      publishLocation(resolvedAddress, position.lat(), position.lng(), 'Address and coordinates updated from Google.');
      moveMarker(position.lat(), position.lng(), true);
    });
  }, [moveMarker, publishLocation]);

  const handleInputChange = useCallback((event) => {
    publishLocation(event.target.value, '', '', '');
  }, [publishLocation]);

  const handleInputKeyDown = useCallback((event) => {
    if (event.key === 'Enter') {
      event.preventDefault();
      handleAddressLookup();
    }
  }, [handleAddressLookup]);

  useEffect(() => {
    onChangeRef.current = onChange;
  }, [onChange]);

  useEffect(() => {
    addressRef.current = address || '';
  }, [address]);

  useEffect(() => {
    let disposed = false;
    disposedRef.current = false;

    async function initializePicker() {
      try {
        const importLibrary = await loadGoogleMaps();
        const [mapsLibrary, placesLibrary, geocodingLibrary] = await Promise.all([
          importLibrary('maps'),
          importLibrary('places'),
          importLibrary('geocoding'),
        ]);

        if (disposed || disposedRef.current || !mapElementRef.current || !inputRef.current) return;

        const initialLocation = initialLocationRef.current;
        const initialLatitude = Number(initialLocation.latitude);
        const initialLongitude = Number(initialLocation.longitude);
        const hasInitialLocation = hasValidCoordinates(initialLocation.latitude, initialLocation.longitude);
        const map = new mapsLibrary.Map(mapElementRef.current, {
          center: hasInitialLocation ? { lat: initialLatitude, lng: initialLongitude } : DEFAULT_CENTER,
          zoom: hasInitialLocation ? 15 : 10,
          disableDefaultUI: false,
          fullscreenControl: true,
          mapTypeControl: false,
          streetViewControl: false,
          rotateControl: false,
        });
        const geocoder = new geocodingLibrary.Geocoder();
        const autocomplete = new placesLibrary.Autocomplete(inputRef.current, {
          componentRestrictions: { country: 'za' },
          fields: ['formatted_address', 'geometry', 'name'],
        });

        mapRef.current = map;
        geocoderRef.current = geocoder;
        autocompleteRef.current = autocomplete;
        listenerRefs.current = [
          autocomplete.addListener('place_changed', handlePlaceChanged),
          map.addListener('click', handleMapClick),
        ];

        if (hasInitialLocation) {
          moveMarker(initialLatitude, initialLongitude, false);
        } else if (pendingLocationRef.current) {
          moveMarker(pendingLocationRef.current.latitude, pendingLocationRef.current.longitude, false);
          pendingLocationRef.current = null;
        }

        setStatusMessage(hasInitialLocation ? 'Drag the pin or search for another address.' : 'Search for an address or click the map to drop a pin.');
      } catch (error) {
        if (!disposed && !disposedRef.current) {
          setStatusMessage(error instanceof Error ? error.message : 'Google Maps could not load.');
        }
      }
    }

    initializePicker();

    return () => {
      disposed = true;
      disposedRef.current = true;
      listenerRefs.current.forEach((listener) => listener.remove());
      listenerRefs.current = [];
      markerRef.current?.setMap(null);
      markerRef.current = null;
      autocompleteRef.current = null;
      geocoderRef.current = null;
      mapRef.current = null;
    };
  }, [handleMapClick, handlePlaceChanged, moveMarker]);

  useEffect(() => {
    const nextAddress = address || '';
    const nextLatitude = Number(latitude);
    const nextLongitude = Number(longitude);
    const nextHasValidLocation = hasValidCoordinates(latitude, longitude);

    addressRef.current = nextAddress;

    if (!mapRef.current) {
      pendingLocationRef.current = nextHasValidLocation ? { latitude: nextLatitude, longitude: nextLongitude } : null;
      return;
    }

    if (nextHasValidLocation) {
      moveMarker(nextLatitude, nextLongitude, false);
    } else if (markerRef.current) {
      markerRef.current.setMap(null);
      markerRef.current = null;
    }
  }, [address, latitude, longitude, moveMarker]);

  return (
    <div className="col-span-full space-y-2">
      <label className="block text-[10px] font-bold uppercase tracking-wider text-slate-400">
        Google Address Search {required ? '*' : ''}
      </label>
      <div className="flex gap-2">
        <div className="relative min-w-0 flex-1">
          <MapPin size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500" />
          <input
            ref={inputRef}
            type="text"
            required={required}
            value={address || ''}
            onChange={handleInputChange}
            onKeyDown={handleInputKeyDown}
            placeholder="Type an address, then select a Google result"
            className="w-full pl-9 pr-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500"
          />
        </div>
        <button
          type="button"
          onClick={handleAddressLookup}
          disabled={isLocating}
          className="shrink-0 px-3 py-2 bg-blue-600 hover:bg-blue-500 disabled:opacity-50 rounded-xl text-xs font-bold text-white flex items-center gap-1"
        >
          {isLocating ? <Loader2 size={14} className="animate-spin" /> : <Search size={14} />}
          {isLocating ? 'Searching…' : 'Search'}
        </button>
      </div>
      <div
        ref={mapElementRef}
        className="w-full rounded-xl border border-slate-800"
        style={{ height: `${Number(height) || 320}px` }}
        aria-label="Property location map"
      />
      <p role="status" className="text-[11px] leading-relaxed text-slate-400">
        {statusMessage}
      </p>
    </div>
  );
}
