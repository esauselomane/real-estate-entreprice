import { useCallback, useEffect, useRef, useState } from 'react';
import { loadGoogleMaps } from './GoogleMapsLoader.js';
import { DEFAULT_PROPERTY_MAP_CENTER, getPropertyTypeColor } from './googlePropertyMapUtils.js';

const MAP_STYLES = [
  { elementType: 'geometry', stylers: [{ color: '#0f172a' }] },
  { elementType: 'labels.text.stroke', stylers: [{ color: '#0f172a' }] },
  { elementType: 'labels.text.fill', stylers: [{ color: '#cbd5e1' }] },
  {
    featureType: 'road',
    elementType: 'geometry',
    stylers: [{ color: '#334155' }, { lightness: 10 }],
  },
  {
    featureType: 'water',
    elementType: 'geometry',
    stylers: [{ color: '#1e293b' }],
  },
];

const MARKER_SVG = (color) => `
  <svg xmlns="http://www.w3.org/2000/svg" width="28" height="40" viewBox="0 0 28 40">
    <path d="M14 1C7.9 1 3 5.9 3 12c0 8.2 9.6 15.4 10 15.7l1 1.3 1-1.3c.4-.3 10-7.5 10-15.7 0-6.1-4.9-11-11-11z" fill="${color}" stroke="#ffffff" stroke-width="1.5"/>
    <circle cx="14" cy="12" r="4.5" fill="#ffffff"/>
  </svg>`;

function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function createMarkerIcon(propertyTypeName, isSelected) {
  const width = isSelected ? 34 : 28;
  const height = isSelected ? 48 : 40;
  const color = getPropertyTypeColor(propertyTypeName);

  return {
    url: `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(MARKER_SVG(color))}`,
    scaledSize: new window.google.maps.Size(width, height),
    anchor: new window.google.maps.Point(width / 2, height - 1),
  };
}

function createInfoWindowContent(property) {
  const address = escapeHtml(property.completeAddress || 'Address unavailable');
  const propertyType = escapeHtml(property.propertyTypeDetails?.name || property.portfolioZoning || 'Property');
  const propertyId = escapeHtml(property.propertyId);
  const listingLabels = [];

  if (property.isForSale) listingLabels.push('<span style="color:#34d399;margin-right:6px">For Sale</span>');
  if (property.isForRent) listingLabels.push('<span style="color:#22d3ee">For Rent</span>');

  return `<div style="font-family:Arial,sans-serif;font-size:13px;line-height:1.45;min-width:220px;color:#e2e8f0">
    <div style="font-weight:700;color:#f8fafc;margin-bottom:5px">${address}</div>
    <div style="color:#94a3b8;margin-bottom:7px">#${propertyId} · ${propertyType}</div>
    <div>${listingLabels.join('')}</div>
  </div>`;
}

function hasValidCoordinates(property) {
  const latitude = property.latitude;
  const longitude = property.longitude;
  if (latitude == null || longitude == null || String(latitude).trim() === '' || String(longitude).trim() === '') {
    return false;
  }

  const numericLatitude = Number(latitude);
  const numericLongitude = Number(longitude);
  return Number.isFinite(numericLatitude) && Number.isFinite(numericLongitude) &&
    Math.abs(numericLatitude) <= 90 && Math.abs(numericLongitude) <= 180;
}

export default function GooglePropertyMap({
  properties = [],
  selectedPropertyId = null,
  onSelectProperty,
  onMapClick,
  className = '',
}) {
  const mapElementRef = useRef(null);
  const mapRef = useRef(null);
  const markersRef = useRef(new Map());
  const infoWindowRef = useRef(null);
  const mapClickListenerRef = useRef(null);
  const onSelectPropertyRef = useRef(onSelectProperty);
  const onMapClickRef = useRef(onMapClick);
  const [isMapReady, setIsMapReady] = useState(false);
  const [mapError, setMapError] = useState('');

  useEffect(() => {
    onSelectPropertyRef.current = onSelectProperty;
  }, [onSelectProperty]);

  useEffect(() => {
    onMapClickRef.current = onMapClick;
  }, [onMapClick]);

  const handleMapClick = useCallback(() => {
    onMapClickRef.current?.();
  }, []);

  useEffect(() => {
    let disposed = false;
    const markers = markersRef.current;

    async function initializePropertyMap() {
      try {
        const importLibrary = await loadGoogleMaps();
        const mapsLibrary = await importLibrary('maps');

        if (disposed || !mapElementRef.current) return;

        const map = new mapsLibrary.Map(mapElementRef.current, {
          center: DEFAULT_PROPERTY_MAP_CENTER,
          zoom: 10,
          mapTypeId: 'roadmap',
          disableDefaultUI: false,
          fullscreenControl: true,
          mapTypeControl: false,
          streetViewControl: false,
          rotateControl: false,
          styles: MAP_STYLES,
        });

        mapRef.current = map;
        infoWindowRef.current = new mapsLibrary.InfoWindow();
        mapClickListenerRef.current = map.addListener('click', handleMapClick);
        setIsMapReady(true);
      } catch (error) {
        if (!disposed) {
          setMapError(error instanceof Error ? error.message : 'Google Maps could not load.');
        }
      }
    }

    initializePropertyMap();

    return () => {
      disposed = true;
      mapClickListenerRef.current?.remove();
      markers.forEach((marker) => marker.setMap(null));
      markers.clear();
      infoWindowRef.current?.close();
      mapRef.current = null;
      infoWindowRef.current = null;
    };
  }, [handleMapClick]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !window.google?.maps) return;

    const visiblePropertyIds = new Set();
    const bounds = new window.google.maps.LatLngBounds();
    let validPropertyCount = 0;

    properties.forEach((property) => {
      if (!hasValidCoordinates(property)) return;

      const propertyId = String(property.propertyId);
      const latitude = Number(property.latitude);
      const longitude = Number(property.longitude);
      const propertyType = property.propertyTypeDetails?.name;
      const isSelected = String(selectedPropertyId) === propertyId;
      visiblePropertyIds.add(propertyId);
      validPropertyCount += 1;
      bounds.extend({ lat: latitude, lng: longitude });

      let marker = markersRef.current.get(propertyId);
      if (!marker) {
        marker = new window.google.maps.Marker({
          map,
          position: { lat: latitude, lng: longitude },
          icon: createMarkerIcon(propertyType, isSelected),
          title: property.completeAddress,
          zIndex: isSelected ? 2 : 1,
        });
        marker.propertyData = property;
        marker.addListener('click', () => {
          const currentProperty = marker.propertyData;
          if (!currentProperty) return;
          onSelectPropertyRef.current?.(currentProperty.propertyId);
          const infoWindow = infoWindowRef.current;
          if (infoWindow) {
            infoWindow.setContent(createInfoWindowContent(currentProperty));
            infoWindow.open({ map, anchor: marker });
          }
        });
        markersRef.current.set(propertyId, marker);
      } else {
        marker.setPosition({ lat: latitude, lng: longitude });
        marker.setIcon(createMarkerIcon(propertyType, isSelected));
        marker.setZIndex(isSelected ? 2 : 1);
        marker.setTitle(property.completeAddress);
        marker.propertyData = property;
      }
    });

    markersRef.current.forEach((marker, propertyId) => {
      if (!visiblePropertyIds.has(propertyId)) {
        marker.setMap(null);
        markersRef.current.delete(propertyId);
      }
    });

    if (validPropertyCount === 0) {
      map.setCenter(DEFAULT_PROPERTY_MAP_CENTER);
      map.setZoom(10);
    } else if (validPropertyCount === 1) {
      map.setCenter(bounds.getCenter());
      map.setZoom(15);
    } else {
      map.fitBounds(bounds, 56);
    }
  }, [properties, selectedPropertyId, isMapReady]);

  if (mapError) {
    return (
      <div className={`flex h-full min-h-[420px] items-center justify-center rounded-xl border border-red-900/60 bg-red-950/20 p-6 text-center text-xs text-red-300 ${className}`}>
        <div>
          <div className="mb-2 text-sm font-bold">Google Maps is unavailable</div>
          <div>{mapError}</div>
        </div>
      </div>
    );
  }

  return (
    <div className={`relative h-full min-h-[420px] overflow-hidden rounded-xl border border-slate-800 bg-slate-950 ${className}`}>
      <div ref={mapElementRef} className="h-full w-full" aria-label="Property map" />
      {!isMapReady && (
        <div className="absolute inset-0 z-10 flex items-center justify-center bg-slate-950/80 text-xs font-semibold text-slate-300">
          Loading Google Maps…
        </div>
      )}
    </div>
  );
}
