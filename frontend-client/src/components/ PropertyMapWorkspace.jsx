import React, { useState, useEffect } from 'react';
import { MapPin, Building2, SlidersHorizontal, Loader2, DollarSign, Layers } from 'lucide-react';
import { propertyService } from '../services/ propertyService.js';

export default function PropertyMapWorkspace() {
  const [properties, setProperties] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [selectedMapMarkerId, setSelectedMapMarkerId] = useState(null);
  
  // Localized view filtering anchors matching layout models
  const [activeZoningFilter, setActiveZoningFilter] = useState('All');

  useEffect(() => {
    const fetchSpatialAssetsForMapping = async () => {
      setIsLoading(true);
      setErrorMessage('');
      try {
        // Query the Ocelot Gateway using default broad criteria to gather map indicators
        const assets = await propertyService.searchProperties(['Green Point', 'Sandton'], 'All', null, null);
        setProperties(assets || []);
      } catch (err) {
        setErrorMessage(err || 'Failed to populate spatial markers onto map canvases.');
      } finally {
        setIsLoading(false);
      }
    };

    fetchSpatialAssetsForMapping();
  }, []);

  const filteredMapAssets = properties.filter(p => 
    activeZoningFilter === 'All' || p.portfolioZoning === activeZoningFilter
  );
  return (
    <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 min-h-[calc(100vh-12rem)] animate-fade-in">
      
      {/* 📋 LEFT SIDE PANEL: GEOLOCATION CONTROLS & PROPERTIES SIDEBAR LIST */}
      <div className="lg:col-span-1 space-y-4 flex flex-col h-full overflow-hidden">
        
        {/* Spatial Filter Cockpit Bar */}
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl space-y-3">
          <span className="text-[10px] text-slate-500 font-bold uppercase tracking-widest flex items-center gap-1">
            <SlidersHorizontal size={10} className="text-blue-500" /> Sector Mapping Filters
          </span>
          <div className="flex flex-wrap gap-1">
            {['All', 'Residential', 'Commercial', 'Retail'].map((zoning) => (
              <button
                key={zoning}
                type="button"
                onClick={() => setActiveZoningFilter(zoning)}
                className={`text-[11px] font-bold px-2.5 py-1.5 rounded-lg border transition-colors cursor-pointer ${
                  activeZoningFilter === zoning
                    ? 'bg-blue-600 text-white border-blue-500 shadow-md'
                    : 'bg-slate-950 text-slate-400 border-slate-800 hover:text-white'
                }`}
              >
                {zoning}
              </button>
            ))}
          </div>
        </div>

        {/* Dynamic Asset Record Cards Scrollbox */}
        <div className="flex-1 overflow-y-auto pr-1 space-y-3 max-h-[60vh] lg:max-h-[calc(100vh-20rem)]">
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
                onClick={() => setSelectedMapMarkerId(prop.propertyId)}
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
                  <span className="flex items-center gap-1"><Building2 size={12} className="text-slate-500" /> {prop.portfolioZoning}</span>
                  <span className="text-white font-bold">R {prop.valuationPrice.toLocaleString()}</span>
                </div>
                <div className="mt-2 pt-2 border-t border-slate-800/60 text-[10px] text-slate-500 font-mono flex gap-3">
                  <span>Lat: {parseFloat(prop.latitude).toFixed(4)}</span>
                  <span>Lng: {parseFloat(prop.longitude).toFixed(4)}</span>
                </div>
              </div>
            ))
          )}

          {filteredMapAssets.length === 0 && !isLoading && !errorMessage && (
            <div className="py-12 text-center border border-dashed border-slate-800 rounded-2xl text-slate-500 text-xs">
              No mapped assets found matching parameters.
            </div>
          )}
        </div>
      </div>

      {/* 🗺️ RIGHT SIDE PANEL: INTUITIVE REAL ESTATE GRAPHICAL MAP VISUALIZATION CONTAINER */}
      <div className="lg:col-span-2 bg-slate-900 border border-slate-800 rounded-2xl shadow-xl flex flex-col h-[50vh] lg:h-full relative overflow-hidden p-3 min-h-[450px]">
        <div className="absolute top-5 left-5 z-10 bg-slate-950/80 backdrop-blur-md px-3 py-1.5 rounded-xl border border-slate-800 text-[11px] font-semibold text-slate-300 flex items-center gap-2 shadow-lg shadow-black/40">
          <Layers size={12} className="text-blue-500 animate-pulse" /> Live Multi-Tenant Mapping Canvas
        </div>
        
        {/* Fallback Graphic / Real-time Map Representation Panel */}
        <div className="w-full h-full bg-slate-950 rounded-xl border border-slate-800/80 flex flex-col items-center justify-center text-center p-6 relative">
          <div className="absolute inset-0 bg-radial-gradient from-blue-500/5 via-transparent to-transparent pointer-events-none"></div>
          
          <div className="max-w-md space-y-4">
            <div className="w-12 h-12 rounded-2xl bg-blue-950 border border-blue-900 flex items-center justify-center text-blue-400 mx-auto shadow-md">
              <MapPin size={22} className="animate-bounce" />
            </div>
            <div>
              <h3 className="text-white font-bold text-base tracking-tight">Interactive Map Active Workspace</h3>
              <p className="text-slate-400 text-xs mt-1 leading-relaxed">
                Displaying <span className="text-blue-400 font-bold">{filteredMapAssets.length}</span> active real estate assets mapped from database coordinate pairs. Click on sidebar address indices to center focus blocks.
              </p>
            </div>
          </div>
        </div>
      </div>

    </div>
  );
}
