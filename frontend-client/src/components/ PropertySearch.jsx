import React, { useState, useEffect } from 'react';
import { Search, MapPin, X, SlidersHorizontal, Building2, Loader2 } from 'lucide-react';
import { propertyService } from '../services/ propertyService.js';

export default function PropertySearch({ onSelectProperty }) {
  // Search parameters states
  const [selectedLocations, setSelectedLocations] = useState([]);
  const [locationInput, setLocationInput] = useState('');
  const [propertyType, setPropertyType] = useState('All');
  const [minSize, setMinSize] = useState('');
  const [maxSize, setMaxSize] = useState('');

  // Live data fetching lifecycle states
  const [properties, setProperties] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  // 🔄 LIVE BACKEND SYNCHRONIZATION PIPELINE: Fetches filtered rows directly from SQL Server
  useEffect(() => {
    const fetchLiveProperties = async () => {
      setIsLoading(true);
      setErrorMessage('');
      try {
        const liveInventory = await propertyService.searchProperties(
          selectedLocations,
          propertyType,
          minSize,
          maxSize
        );
        setProperties(liveInventory);
      } catch (err) {
        setErrorMessage(err || 'Failed to sync spatial data metrics from backend clusters.');
      } finally {
        setIsLoading(false);
      }
    };

    fetchLiveProperties();
  }, [selectedLocations, propertyType, minSize, maxSize]); // Re-fires instantly on layout modifications

  const handleAddLocation = (e) => {
    e.preventDefault();
    if (locationInput.trim() && !selectedLocations.includes(locationInput.trim())) {
      setSelectedLocations([...selectedLocations, locationInput.trim()]);
      setLocationInput('');
    }
  };

  const handleRemoveLocation = (locToRemove) => {
    setSelectedLocations(selectedLocations.filter(loc => loc !== locToRemove));
  };
  return (
    <div className="space-y-6 animate-fade-in">
      
      {/* 🔍 Interactive Search Control Panel */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
        <h3 className="text-lg font-bold text-white mb-4 flex items-center gap-2">
          <SlidersHorizontal size={18} className="text-blue-500" />
          Multi-Criteria Filter Engine
        </h3>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          
          {/* 📍 Multi-Location Dynamic Chip Array Entry */}
          <div className="space-y-2">
            <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider">Target Locations</label>
            <form onSubmit={handleAddLocation} className="flex gap-2">
              <div className="relative flex-1">
                <MapPin size={16} className="absolute left-3 top-3.5 text-slate-500" />
                <input 
                  type="text"
                  value={locationInput}
                  onChange={(e) => setLocationInput(e.target.value)}
                  placeholder="Type area & press Enter..."
                  className="w-full pl-9 pr-4 py-2.5 bg-slate-950 border border-slate-700 rounded-xl text-sm text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 transition-colors"
                />
              </div>
            </form>
            
            {/* Location Chips Display Canvas */}
            <div className="flex flex-wrap gap-1.5 pt-1">
              {selectedLocations.map((loc, idx) => (
                <span key={idx} className="inline-flex items-center gap-1.5 bg-blue-950/60 border border-blue-800 text-blue-300 text-xs px-2.5 py-1 rounded-lg">
                  {loc}
                  <button type="button" onClick={() => handleRemoveLocation(loc)} className="hover:text-red-400 cursor-pointer">
                    <X size={12} />
                  </button>
                </span>
              ))}
              {selectedLocations.length === 0 && <span className="text-xs text-slate-600 italic">Searching all regions globally</span>}
            </div>
          </div>

          {/* 🏢 Sector Property Type Category Dropdown */}
          <div className="space-y-2">
            <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider">Property Type Sector</label>
            <div className="relative">
              <Building2 size={16} className="absolute left-3 top-3.5 text-slate-500" />
              <select 
                value={propertyType}
                onChange={(e) => setPropertyType(e.target.value)}
                className="w-full pl-9 pr-4 py-2.5 bg-slate-950 border border-slate-700 rounded-xl text-sm text-white focus:outline-none focus:border-blue-500 appearance-none cursor-pointer"
              >
                <option value="All">All Property Types</option>
                <option value="Residential">Residential Sector</option>
                <option value="Commercial">Commercial Offices</option>
                <option value="Retail">Retail Storefronts</option>
              </select>
            </div>
          </div>

          {/* 📐 Size Threshold Area Fields (Erf/Floor size in m²) */}
          <div className="space-y-2">
            <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider">Size Boundary Extent (m²)</label>
            <div className="flex gap-2">
              <input 
                type="number" 
                placeholder="Min m²"
                value={minSize}
                onChange={(e) => setMinSize(e.target.value)}
                className="w-full px-3 py-2.5 bg-slate-950 border border-slate-700 rounded-xl text-sm text-white placeholder-slate-600 focus:outline-none focus:border-blue-500"
              />
              <input 
                type="number" 
                placeholder="Max m²"
                value={maxSize}
                onChange={(e) => setMaxSize(e.target.value)}
                className="w-full px-3 py-2.5 bg-slate-950 border border-slate-700 rounded-xl text-sm text-white placeholder-slate-600 focus:outline-none focus:border-blue-500"
              />
            </div>
          </div>

        </div>
      </div>

      {/* 📊 Matching Results Output Grid */}
      <div className="space-y-3">
        <div className="flex justify-between items-center px-1">
          <span className="text-xs font-semibold text-slate-400 uppercase tracking-widest">Matched System Assets ({properties.length})</span>
        </div>

        {/* ❌ Network Error Messages Alert Row */}
        {errorMessage && (
          <div className="p-4 bg-red-950/30 border border-red-900/60 text-red-400 rounded-2xl text-xs font-medium text-center">
            {errorMessage}
          </div>
        )}

        {/* ⏳ Async Loading Spinner Engine Mask */}
        {isLoading ? (
          <div className="py-20 flex flex-col items-center justify-center text-slate-500 gap-3">
            <Loader2 className="w-8 h-8 text-blue-500 animate-spin" />
            <span className="text-xs font-medium tracking-wide">Querying microservices registry indices...</span>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {properties.map((prop) => (
              <div 
                key={prop.propertyId} 
                onClick={() => onSelectProperty(prop.propertyId)}
                className="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 hover:border-slate-700 transition-all flex flex-col justify-between group cursor-pointer"
              >
                <div>
                  <div className="flex justify-between items-start mb-2">
                    <span className={`text-xs font-bold uppercase px-2 py-0.5 rounded-md ${
                      prop.marketClass === 'Sale' ? 'bg-emerald-950 text-emerald-400 border border-emerald-900' :
                      prop.marketClass === 'Rental' ? 'bg-blue-950 text-blue-400 border border-blue-900' :
                      'bg-amber-950 text-amber-400 border border-amber-900'
                    }`}>
                      {prop.marketClass}
                    </span>
                    <span className="text-sm font-bold text-slate-300">{prop.erfSpatialSize}</span>
                  </div>
                  
                  <h4 className="text-white font-bold text-base mb-1 tracking-tight group-hover:text-blue-400 transition-colors">
                    {prop.completeAddress}
                  </h4>
                  <div className="flex items-center gap-2 mb-4">
                    <p className="text-xs text-slate-500 font-medium">{prop.portfolioZoning}</p>
                    {prop.subUnits && prop.subUnits.length > 0 && (
                      <span className="text-[10px] font-semibold text-blue-400 bg-blue-950/80 px-2 py-0.5 rounded-full border border-blue-900">
                        {prop.subUnits.length} {prop.subUnits.length === 1 ? 'Unit' : 'Units'}
                      </span>
                    )}
                  </div>
                </div>

                <div className="pt-4 border-t border-slate-800/80 flex items-center justify-between">
                  <div>
                    <span className="block text-[10px] text-slate-500 uppercase font-bold tracking-wider">Valuation Cost Structure</span>
                    <span className="text-lg font-black text-white">
                      {prop.marketClass === 'Rental' ? `R${prop.valuationPrice.toLocaleString()}/mo` : `R${prop.valuationPrice.toLocaleString()}`}
                    </span>
                  </div>
                  <span className="text-xs text-blue-400 font-bold opacity-0 group-hover:opacity-100 transition-opacity">Drill Down &rarr;</span>
                </div>
              </div>
            ))}

            {properties.length === 0 && !errorMessage && (
              <div className="col-span-full py-12 text-center border border-dashed border-slate-800 rounded-2xl text-slate-500 text-sm">
                No active property assets found matching the selected search criteria inside database records.
              </div>
            )}
          </div>
        )}
      </div>

    </div>
  );
}
