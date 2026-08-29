import React, { useState, useEffect, useMemo, useRef } from 'react';
import { MapPin, Building2, SlidersHorizontal, Loader2, Layers, ListCollapse, Search } from 'lucide-react';
import { MapContainer, TileLayer, Marker, Popup, useMapEvents } from 'react-leaflet';
import L from 'leaflet';
import { propertyService } from '../services/propertyService.js';

// 🗺️ Vite + Leaflet default-marker icon patch — the bundler can't resolve the PNG
// asset URLs otherwise, which leaves markers invisible. Resolved once per load.
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl:   'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl:         'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl:       'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png'
});

// 🎨 Item 1: one marker colour per property type so sector types are
// instantly scannable on the canvas (Residential=blue, Commercial=amber, …).
const TYPE_COLORS = {
  Residential: '#3b82f6',
  Commercial:  '#f59e0b',
  Retail:      '#10b981',
  Industrial:  '#ef4444',
  Default:     '#6474a5'
};

const coloredMarkerIcon = (color) => L.divIcon({
  className: 'property-type-marker',
  html: `<svg width="26" height="38" viewBox="0 0 26 38" fill="none" xmlns="http://www.w3.org/2000/svg">` +
    `<path d="M13 0C5.8 0 0 5.8 0 13c0 10.5 11.5 18.5 11.9 18.7.1.1 1.5 1.3 3.2 2.7.3.2.5.2.7 0 1.7-1.4 3.1-2.6 3.2-2.7C26 23.5 26 13 26 13 26 5.8 20.2 0 13 0z" fill="${color}" stroke="white" stroke-width="1.5"/>` +
    `<circle cx="13" cy="14" r="5" fill="white"/>` +
    `</svg>`,
  iconSize: [26, 38],
  iconAnchor: [13, 38],
  popupAnchor: [0, -38],
  shadowUrl: ''
});

const DEFAULT_CENTER = [-33.9249, 18.4207]; // Cape Town fallback

export default function PropertyMapWorkspace({ onSelectProperty }) {
  const [properties, setProperties] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [selectedMapMarkerId, setSelectedMapMarkerId] = useState(null);
  const [activeTypeFilter, setActiveTypeFilter] = useState('All');
  const [searchTerm, setSearchTerm] = useState('');

  useEffect(() => {
    const fetchSpatialAssetsForMapping = async () => {
      setIsLoading(true);
      setErrorMessage('');
      try {
        const assets = await propertyService.searchProperties([], 'All', null, null);
        setProperties(assets || []);
      } catch (err) {
        setErrorMessage(err || 'Failed to populate spatial markers onto map canvases.');
      } finally {
        setIsLoading(false);
      }
    };
    fetchSpatialAssetsForMapping();
  }, []);

  // 🧭 Distinct property-type filters derived from the loaded data so the cockpit
  // matches the actual seed values (Residential / Commercial / Retail / Industrial).
  const typeOptions = useMemo(() => {
    const set = new Set(['All']);
    properties.forEach((p) => {
      const name = p.propertyTypeDetails?.name;
      if (name) set.add(name);
    });
    return Array.from(set);
  }, [properties]);

  const search = searchTerm.trim().toLowerCase();
  const filteredMapAssets = properties.filter((p) => {
    if (activeTypeFilter !== 'All' && p.propertyTypeDetails?.name !== activeTypeFilter) return false;
    if (search) {
      return (
        String(p.completeAddress || '').toLowerCase().includes(search) ||
        String(p.portfolioZoning || '').toLowerCase().includes(search) ||
        String(p.propertyId || '').toLowerCase().includes(search)
      );
    }
    return true;
  });

  const mappableAssets = filteredMapAssets.filter(
    (p) => p.latitude != null && p.longitude != null &&
      !isNaN(Number(p.latitude)) && !isNaN(Number(p.longitude))
  );

  const { center, bounds } = useMemo(() => {
    if (!mappableAssets.length) return { center: DEFAULT_CENTER, bounds: null };
    const lats = mappableAssets.map((p) => Number(p.latitude));
    const lngs = mappableAssets.map((p) => Number(p.longitude));
    const b = L.latLngBounds(
      [Math.min(...lats), Math.min(...lngs)],
      [Math.max(...lats), Math.max(...lngs)]
    );
    const c = [lats.reduce((a, b2) => a + b2, 0) / lats.length, lngs.reduce((a, b2) => a + b2, 0) / lngs.length];
    return { center: c, bounds: b.isValid() && mappableAssets.length > 1 ? b : null };
  }, [mappableAssets]);

  const openProperty = (propertyId) => {
    setSelectedMapMarkerId(propertyId);
    onSelectProperty && onSelectProperty(propertyId);
  };

  function MapClickHandler() {
    useMapEvents({ click: () => setSelectedMapMarkerId(null) });
    return null;
  }

  return (
    <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 min-h-[calc(100vh-12rem)] animate-fade-in">

      {/* 📋 LEFT SIDE PANEL: GEOLOCATION CONTROLS & PROPERTIES SIDEBAR LIST */}
      <div className="lg:col-span-1 space-y-4 flex flex-col h-full overflow-hidden">

        {/* Spatial Filter Cockpit Bar */}
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl space-y-3">
          <span className="text-[10px] text-slate-500 font-bold uppercase tracking-widest flex items-center gap-1">
            <SlidersHorizontal size={10} className="text-blue-500" /> Sector Type Filter
          </span>
          <div className="flex flex-wrap gap-1">
            {typeOptions.map((zoning) => (
              <button
                key={zoning}
                type="button"
                onClick={() => setActiveTypeFilter(zoning)}
                className={`text-[11px] font-bold px-2.5 py-1.5 rounded-lg border transition-colors cursor-pointer ${
                  activeTypeFilter === zoning
                    ? 'bg-blue-600 text-white border-blue-500 shadow-md'
                    : 'bg-slate-950 text-slate-400 border-slate-800 hover:text-white'
                }`}
              >
                {zoning}
              </button>
            ))}
          </div>
        </div>

        {/* Address / id search */}
        <div className="relative">
          <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500" />
          <input
            type="text"
            placeholder="Search address or property #"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full pl-9 pr-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500"
          />
        </div>

        {/* Dynamic Asset Record Cards Scrollbox */}
        <div className="flex-1 overflow-y-auto pr-1 space-y-3 max-h-[60vh] lg:max-h-[calc(100vh-22rem)]">
          {errorMessage && (
            <div className="p-4 bg-red-950/30 border border-red-900/60 text-red-400 rounded-2xl text-xs font-medium text-center">
              {errorMessage}
            </div>
          )}

          {isLoading ? (
            <div className="py-12 flex flex-col items-center justify-center text-slate-500 gap-3">
              <Loader2 className="w-6 h-6 text-blue-500 animate-spin" />
              <span className="text-xs font-medium">Resolving coordinate indices...</span>
            </div>
          ) : (
            filteredMapAssets.map((prop) => (
              <div
                key={prop.propertyId}
                onClick={() => openProperty(prop.propertyId)}
                className={`p-4 rounded-2xl border transition-all cursor-pointer shadow-md select-none ${
                  selectedMapMarkerId === prop.propertyId
                    ? 'bg-slate-900 border-blue-500 ring-1 ring-blue-500/30'
                    : 'bg-slate-900/60 border-slate-800 hover:border-slate-700'
                }`}
              >
                <div className="flex justify-between items-start mb-1.5">
                  <h4 className="text-white font-bold text-sm truncate tracking-tight">{prop.completeAddress}</h4>
                  <span className="text-[10px] font-mono text-slate-500">#{prop.propertyId}</span>
                </div>
                <div className="flex justify-between items-center text-[11px] text-slate-400 font-medium">
                  <span className="flex items-center gap-1"><Building2 size={12} className="text-slate-500" /> {prop.propertyTypeDetails?.name || prop.portfolioZoning || '—'}</span>
                  <div className="flex items-center gap-1.5">
                    {prop.isForSale && <span className="text-[9px] font-bold px-1.5 py-0.5 rounded bg-emerald-950/40 text-emerald-300 border border-emerald-900">Sale</span>}
                    {prop.isForRent && <span className="text-[9px] font-bold px-1.5 py-0.5 rounded bg-cyan-950/40 text-cyan-300 border border-cyan-900">Rent</span>}
                    <span className="text-white font-bold">R {prop.valuationPrice != null ? prop.valuationPrice.toLocaleString() : 'N/A'}</span>
                  </div>
                </div>
                <div className="mt-2 pt-2 border-t border-slate-800/60 text-[10px] text-slate-500 font-mono flex gap-3">
                  <span>Lat: {prop.latitude != null ? Number(prop.latitude).toFixed(4) : '—'}</span>
                  <span>Lng: {prop.longitude != null ? Number(prop.longitude).toFixed(4) : '—'}</span>
                </div>
                <div className="mt-2 text-[10px] text-blue-400 font-semibold flex items-center gap-1.5">
                  <ListCollapse size={10} /> Click to open asset details
                </div>
              </div>
            ))
          )}

          {!isLoading && filteredMapAssets.length === 0 && !errorMessage && (
            <div className="py-12 text-center border border-dashed border-slate-800 rounded-2xl text-slate-500 text-xs">
              No mapped assets found matching the selected sector and search.
            </div>
          )}
        </div>
      </div>

      {/* 🗺️ RIGHT SIDE PANEL: INTERACTIVE LEAFLET MAP */}
      <div className="lg:col-span-2 bg-slate-900 border border-slate-800 rounded-2xl shadow-xl flex flex-col h-[50vh] lg:h-full relative overflow-hidden p-3 min-h-[450px]">
        <div className="absolute top-5 left-5 z-10 bg-slate-950/80 backdrop-blur-md px-3 py-1.5 rounded-xl border border-slate-800 text-[11px] font-semibold text-slate-300 flex items-center gap-2 shadow-lg shadow-black/40">
          <Layers size={12} className="text-blue-500 animate-pulse" /> Live Multi-Tenant Mapping Canvas
        </div>

        {mappableAssets.length === 0 ? (
          <div className="w-full h-full bg-slate-950 rounded-xl border border-slate-800/80 flex flex-col items-center justify-center text-center p-6 relative">
            <div className="absolute inset-0 bg-radial-gradient from-blue-500/5 via-transparent to-transparent pointer-events-none"></div>
            <div className="max-w-md space-y-4">
              <div className="w-12 h-12 rounded-2xl bg-blue-950 border border-blue-900 flex items-center justify-center text-blue-400 mx-auto shadow-md">
                <MapPin size={22} />
              </div>
              <div>
                <h3 className="text-white font-bold text-base tracking-tight">No mappable assets</h3>
                <p className="text-slate-400 text-xs mt-1 leading-relaxed">
                  Displaying <span className="text-blue-400 font-bold">0</span> real estate assets on the map canvas.
                  Adjust the sector filter or search term to reveal coordinate pairs.
                </p>
              </div>
            </div>
          </div>
        ) : (
          <MapContainer
            key={mappableAssets.length}
            center={center}
            bounds={bounds || undefined}
            zoom={bounds ? undefined : 11}
            className="w-full h-full rounded-xl z-0"
          >
            <MapClickHandler />
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
             {mappableAssets.map((prop) => (
               <Marker
                 key={prop.propertyId}
                 position={[Number(prop.latitude), Number(prop.longitude)]}
                 icon={coloredMarkerIcon(TYPE_COLORS[prop.propertyTypeDetails?.name] || TYPE_COLORS.Default)}
                 eventHandlers={{
                  click: () => {
                    setSelectedMapMarkerId(prop.propertyId);
                  }
                }}
              >
                <Popup>
                  <div className="text-xs w-56">
                    <div className="font-bold text-slate-100">{prop.completeAddress}</div>
                    <div className="text-slate-300 mt-0.5">#{prop.propertyId} · {prop.propertyTypeDetails?.name || '—'} · {prop.portfolioZoning || '—'}</div>
                    <div className="mt-1 flex gap-1.5">
                      {prop.isForSale && <span className="text-[9px] font-bold px-1.5 py-0.5 rounded bg-emerald-950/40 text-emerald-300 border border-emerald-900">For Sale</span>}
                      {prop.isForRent && <span className="text-[9px] font-bold px-1.5 py-0.5 rounded bg-cyan-950/40 text-cyan-300 border border-cyan-900">For Rent</span>}
                    </div>
                    <button
                      onClick={() => openProperty(prop.propertyId)}
                      className="mt-1.5 text-blue-400 font-semibold underline"
                    >
                      Open asset details →
                    </button>
                  </div>
                </Popup>
              </Marker>
            ))}
          </MapContainer>
        )}
      </div>

    </div>
  );
}
