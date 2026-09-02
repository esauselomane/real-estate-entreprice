import React, { useState, useEffect, useRef } from 'react';
import { Search, MapPin, X, SlidersHorizontal, Building2, Loader2, Pencil } from 'lucide-react';
import { propertyService } from '../services/propertyService.js';

export default function PropertySearch({ onSelectProperty }) {
  // Search parameters states
  const [selectedLocations, setSelectedLocations] = useState([]);
  const [locationInput, setLocationInput] = useState('');
  const [propertyType, setPropertyType] = useState('All');
  const [listingType, setListingType] = useState('All');
  const [minSize, setMinSize] = useState('');
  const [maxSize, setMaxSize] = useState('');

  // Live data fetching lifecycle states
  const [properties, setProperties] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [editingProperty, setEditingProperty] = useState(null);
  const [propertyForm, setPropertyForm] = useState({ completeAddress: '', erfSpatialSize: '', titleDeedType: 'Freehold', portfolioZoning: '', marketClass: 'Sale', propertyTypeId: '', valuationPrice: '', salePrice: '', latitude: '', longitude: '', linkedOwnerId: '' });
  const [addressQuery, setAddressQuery] = useState('');
  const [addressResults, setAddressResults] = useState([]);
  const [isSearchingAddress, setIsSearchingAddress] = useState(false);
  const [showAddressDropdown, setShowAddressDropdown] = useState(false);
  const addressDebounceRef = useRef(null);

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
          maxSize,
          listingType
        );
        setProperties(liveInventory);
      } catch (err) {
        setErrorMessage(err || 'Failed to sync spatial data metrics from backend clusters.');
      } finally {
        setIsLoading(false);
      }
    };

    fetchLiveProperties();
    }, [selectedLocations, propertyType, minSize, maxSize, listingType]); // Re-fires instantly on layout modifications

  useEffect(() => () => { if (addressDebounceRef.current) clearTimeout(addressDebounceRef.current); }, []);


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

  const searchAddress = async (query) => {
    setAddressQuery(query);
    if (addressDebounceRef.current) clearTimeout(addressDebounceRef.current);
    if (query.trim().length < 3) { setAddressResults([]); setShowAddressDropdown(false); return; }
    addressDebounceRef.current = setTimeout(async () => {
      setIsSearchingAddress(true);
      try {
        const res = await fetch(`https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}&limit=5`);
        if (!res.ok) throw new Error(`Geocoding failed: ${res.status}`);
        const data = await res.json();
        setAddressResults(data || []);
        setShowAddressDropdown(true);
      } catch {
        setAddressResults([]);
      } finally {
        setIsSearchingAddress(false);
      }
    }, 350);
  };

  const selectAddress = (result) => {
    setPropertyForm((prev) => ({ ...prev, completeAddress: result.display_name, latitude: result.lat, longitude: result.lon }));
    setAddressQuery(result.display_name);
    setShowAddressDropdown(false);
    setAddressResults([]);
  };

  const openEditProperty = async (propertyId) => {
    try {
      const prop = await propertyService.getPropertyById(propertyId);
      setEditingProperty(prop);
      setPropertyForm({
        completeAddress: prop.completeAddress || '',
        erfSpatialSize: prop.erfSpatialSize || '',
        titleDeedType: prop.titleDeedType || 'Freehold',
        portfolioZoning: prop.portfolioZoning || '',
        marketClass: prop.marketClass || 'Sale',
        propertyTypeId: String(prop.propertyTypeId || ''),
        valuationPrice: String(prop.valuationPrice || ''),
        salePrice: String(prop.salePrice || ''),
        latitude: String(prop.latitude || ''),
        longitude: String(prop.longitude || ''),
        linkedOwnerId: prop.linkedOwnerId || ''
      });
      setAddressQuery(prop.completeAddress || '');
    } catch (err) { alert(err || 'Failed to load property.'); }
  };

  const handleUpdateProperty = async (e) => {
    e.preventDefault();
    if (!editingProperty) return;
    try {
      await propertyService.updateProperty(editingProperty.propertyId, {
        ...propertyForm,
        propertyTypeId: parseInt(propertyForm.propertyTypeId) || 0,
        valuationPrice: parseFloat(propertyForm.valuationPrice) || 0,
        salePrice: parseFloat(propertyForm.salePrice) || 0,
        latitude: parseFloat(propertyForm.latitude) || 0,
        longitude: parseFloat(propertyForm.longitude) || 0,
        linkedOwnerId: propertyForm.linkedOwnerId
      });
      setEditingProperty(null);
      setAddressQuery('');
      setAddressResults([]);
      setShowAddressDropdown(false);
    } catch (err) { alert(err || 'Failed to update property.'); }
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

          {/* 📈 Listing Eligibility Toggle — a property may be marketed for SALE and/or RENT. */}
          <div className="space-y-2">
            <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider">Listing Mode</label>
            <div className="grid grid-cols-3 gap-1 h-10">
              {[{ v: 'All', label: 'Both' }, { v: 'Sale', label: 'For Sale' }, { v: 'Rent', label: 'For Rent' }].map((o) => (
                <button
                  key={o.v} type="button"
                  onClick={() => setListingType(o.v)}
                  className={`text-xs font-bold rounded-lg border transition-all cursor-pointer ${
                    listingType === o.v
                      ? 'bg-blue-600 border-blue-600 text-white'
                      : 'bg-slate-950 border-slate-700 text-slate-400 hover:text-white hover:border-blue-800'
                  }`}
                >{o.label}</button>
              ))}
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
                     <span className="text-xs font-bold uppercase px-2 py-0.5 rounded-md bg-slate-800 text-slate-300 border border-slate-700">
                       {prop.isForSale && prop.isForRent ? 'SALE + RENT' : prop.isForSale ? 'FOR SALE' : prop.isForRent ? 'FOR RENT' : 'OFF MARKET'}
                     </span>
                     <div className="flex items-center gap-2">
                       <button type="button" onClick={(e) => { e.stopPropagation(); openEditProperty(prop.propertyId); }} className="p-1.5 bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-white rounded-lg transition-colors" title="Edit property"><Pencil size={14} /></button>
                       <span className="text-sm font-bold text-slate-300">{prop.erfSpatialSize}</span>
                     </div>
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
                          {prop.isForRent ? `R${(prop.subUnits?.[0]?.rentalRate || prop.valuationPrice).toLocaleString()}/mo` : `R${prop.salePrice?.toLocaleString() || prop.valuationPrice.toLocaleString()}`}
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

      {/* Edit Property Modal */}
      {editingProperty && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-md flex items-center justify-center p-4 z-50 animate-fade-in">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 max-w-2xl w-full space-y-4 shadow-2xl max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-white uppercase tracking-wider">Edit Property</h3>
              <button type="button" onClick={() => { setEditingProperty(null); setAddressQuery(''); setAddressResults([]); }} className="text-slate-400 hover:text-white"><X size={14} /></button>
            </div>
            <form onSubmit={handleUpdateProperty} className="space-y-3">
              <div className="space-y-1 relative">
                <label className="text-[10px] text-slate-400 uppercase">Address Search</label>
                <input type="text" value={addressQuery} onChange={(e) => searchAddress(e.target.value)} onFocus={() => addressResults.length > 0 && setShowAddressDropdown(true)} placeholder="Search address..." className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" />
                {isSearchingAddress && <Loader2 className="absolute right-3 top-7 text-blue-500 animate-spin" size={14} />}
                {showAddressDropdown && addressResults.length > 0 && (
                  <div className="absolute z-50 w-full mt-1 bg-slate-900 border border-slate-800 rounded-xl shadow-xl max-h-48 overflow-y-auto">
                    {addressResults.map((r) => (
                      <button key={r.place_id} type="button" onClick={() => selectAddress(r)} className="w-full text-left px-3 py-2 hover:bg-slate-800 text-xs text-white">
                        {r.display_name}
                      </button>
                    ))}
                  </div>
                )}
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div className="space-y-1 md:col-span-2"><label className="text-[10px] text-slate-400 uppercase">Complete Address</label><input required type="text" value={propertyForm.completeAddress} onChange={(e) => setPropertyForm({ ...propertyForm, completeAddress: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">ERF Size</label><input type="text" value={propertyForm.erfSpatialSize} onChange={(e) => setPropertyForm({ ...propertyForm, erfSpatialSize: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Title Deed Type</label><select value={propertyForm.titleDeedType} onChange={(e) => setPropertyForm({ ...propertyForm, titleDeedType: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"><option>Freehold</option><option>Sectional Title</option><option>Leasehold</option></select></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Zoning</label><input type="text" value={propertyForm.portfolioZoning} onChange={(e) => setPropertyForm({ ...propertyForm, portfolioZoning: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Market Class</label><select value={propertyForm.marketClass} onChange={(e) => setPropertyForm({ ...propertyForm, marketClass: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"><option>Sale</option><option>Rent</option><option>Both</option></select></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Property Type ID</label><input type="number" value={propertyForm.propertyTypeId} onChange={(e) => setPropertyForm({ ...propertyForm, propertyTypeId: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Valuation Price (R)</label><input type="number" value={propertyForm.valuationPrice} onChange={(e) => setPropertyForm({ ...propertyForm, valuationPrice: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Sale Price (R)</label><input type="number" value={propertyForm.salePrice} onChange={(e) => setPropertyForm({ ...propertyForm, salePrice: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Latitude</label><input type="number" step="any" value={propertyForm.latitude} onChange={(e) => setPropertyForm({ ...propertyForm, latitude: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Longitude</label><input type="number" step="any" value={propertyForm.longitude} onChange={(e) => setPropertyForm({ ...propertyForm, longitude: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1 md:col-span-2"><label className="text-[10px] text-slate-400 uppercase">Linked Owner ID</label><input type="text" value={propertyForm.linkedOwnerId} onChange={(e) => setPropertyForm({ ...propertyForm, linkedOwnerId: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
              </div>
              <div className="flex gap-2">
                <button type="submit" className="flex-1 py-2.5 rounded-xl bg-blue-600 text-white text-xs font-bold">Save Changes</button>
                <button type="button" onClick={() => { setEditingProperty(null); setAddressQuery(''); setAddressResults([]); }} className="py-2.5 px-4 rounded-xl text-xs font-bold bg-slate-800 text-slate-300">Cancel</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
