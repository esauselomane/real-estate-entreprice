import GooglePropertyMapPicker from './maps/GooglePropertyMapPicker.jsx';
import { useState, useEffect, useCallback } from 'react';
import { MapPin, X, SlidersHorizontal, Building2, Loader2, Pencil } from 'lucide-react';
import { propertyService } from '../services/propertyService.js';
import Can from '../components/Can.jsx';
import Pagination from '../components/Pagination.jsx';

const LOAD_ALL_PAGE_SIZE = 100000;

function hasValidCoordinates(latitude, longitude) {
  if (latitude == null || longitude == null || String(latitude).trim() === '' || String(longitude).trim() === '') {
    return false;
  }

  const numericLatitude = Number(latitude);
  const numericLongitude = Number(longitude);
  return Number.isFinite(numericLatitude) && Number.isFinite(numericLongitude) &&
    Math.abs(numericLatitude) <= 90 && Math.abs(numericLongitude) <= 180;
}

function getListingFlags(marketClass) {
  const normalizedMarketClass = String(marketClass || '').toLowerCase();
  return {
    isForSale: normalizedMarketClass === 'sale' || normalizedMarketClass === 'both',
    isForRent: normalizedMarketClass === 'rent' || normalizedMarketClass === 'rental' || normalizedMarketClass === 'both'
  };
}

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
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [totalCount, setTotalCount] = useState(0);
  const [errorMessage, setErrorMessage] = useState('');
  const [editingProperty, setEditingProperty] = useState(null);
  const [editErrorMessage, setEditErrorMessage] = useState('');
  const [hasSelectedGoogleLocation, setHasSelectedGoogleLocation] = useState(false);
  const [propertyForm, setPropertyForm] = useState({ completeAddress: '', erfSpatialSize: '', titleDeedType: 'Freehold', portfolioZoning: '', marketClass: 'Sale', propertyTypeId: '', valuationPrice: '', salePrice: '', latitude: '', longitude: '', linkedOwnerId: '', isForSale: false, isForRent: false, soleMandate: false, soleMandateStart: '', soleMandateEnd: '' });

  // 📜 Single-shot fetch — every filter change requests page 1 with a
  // deliberately huge pageSize so the full set returns in one round-trip.
  // Replaces the previous infinite-scroll + IntersectionObserver pattern
  // (which was unreliable because the observer lived in a conditional
  // effect and silently never fired).
  const fetchAll = useCallback(async () => {
    setIsLoading(true);
    setErrorMessage('');
    try {
      const liveInventory = await propertyService.searchProperties(
        selectedLocations,
        propertyType,
        minSize,
        maxSize,
        listingType,
        1,
        LOAD_ALL_PAGE_SIZE
      );
      const incoming = Array.isArray(liveInventory) ? liveInventory : [];
      setProperties(incoming);
      const total = Number(liveInventory?.totalCount || incoming.length);
      setTotalCount(total);
      setPage(1);
    } catch (err) {
      setErrorMessage(err || 'Failed to sync spatial data metrics from backend clusters.');
    } finally {
      setIsLoading(false);
    }
  }, [selectedLocations, propertyType, minSize, maxSize, listingType]);

  // 🔄 LIVE BACKEND SYNCHRONIZATION PIPELINE: re-fetch on any filter change.
  useEffect(() => {
    fetchAll();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedLocations, propertyType, minSize, maxSize, listingType]);



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

  const handleEditLocationChange = (location) => {
    setPropertyForm((previous) => ({ ...previous, ...location }));
    setHasSelectedGoogleLocation(hasValidCoordinates(location.latitude, location.longitude));
  };

  const handleEditCoordinateChange = (field, value) => {
    setPropertyForm((previous) => ({ ...previous, [field]: value }));
    setHasSelectedGoogleLocation(false);
  };

  const openEditProperty = async (propertyId) => {
    try {
      const prop = await propertyService.getPropertyById(propertyId);
      setEditingProperty(prop);
      const mandate = prop.activeSoleMandate;
      setPropertyForm({
        completeAddress: prop.completeAddress || '',
        erfSpatialSize: prop.erfSpatialSize || '',
        titleDeedType: prop.titleDeedType || 'Freehold',
        portfolioZoning: prop.portfolioZoning || '',
        marketClass: prop.marketClass || 'Sale',
        propertyTypeId: String(prop.propertyTypeId || ''),
        valuationPrice: String(prop.valuationPrice || ''),
        salePrice: String(prop.salePrice || ''),
        latitude: String(prop.latitude ?? ''),
        longitude: String(prop.longitude ?? ''),
        linkedOwnerId: prop.linkedOwnerId || '',
        isForSale: !!prop.isForSale,
        isForRent: !!prop.isForRent,
        soleMandate: !!mandate,
        soleMandateStart: mandate ? mandate.startDate?.slice(0, 10) : '',
        soleMandateEnd: mandate ? mandate.endDate?.slice(0, 10) : ''
      });
      setHasSelectedGoogleLocation(hasValidCoordinates(prop.latitude, prop.longitude));
      setEditErrorMessage('');

    } catch (err) { alert(err || 'Failed to load property.'); }
  };

  const handleUpdateProperty = async (e) => {
    e.preventDefault();
    if (!editingProperty) return;

    const latitude = Number(propertyForm.latitude);
    const longitude = Number(propertyForm.longitude);
    if (!propertyForm.completeAddress.trim() || !hasSelectedGoogleLocation || !hasValidCoordinates(latitude, longitude)) {
      setEditErrorMessage('Select a Google address or drag the pin on the map before saving. Manual coordinates are not accepted.');
      return;
    }

    const listingFlags = getListingFlags(propertyForm.marketClass);
    try {
      await propertyService.updateProperty(editingProperty.propertyId, {
        ...propertyForm,
        propertyTypeId: Number(propertyForm.propertyTypeId) || 0,
        valuationPrice: Number(propertyForm.valuationPrice) || 0,
        salePrice: propertyForm.salePrice === '' ? null : Number(propertyForm.salePrice),
        latitude,
        longitude,
        linkedOwnerId: propertyForm.linkedOwnerId,
        isForSale: listingFlags.isForSale,
        isForRent: listingFlags.isForRent
      });
      // 📜 Sole-mandate: separate from the property PUT. Ticked = open a
      // new active mandate (server auto-closes any previous one). Unticked
      // = close the currently-active mandate, if any.
      const existingMandate = editingProperty.activeSoleMandate;
      if (propertyForm.soleMandate && !existingMandate) {
        const today = new Date();
        const fallbackEnd = new Date(today.getTime() + 365 * 24 * 60 * 60 * 1000);
        const start = propertyForm.soleMandateStart || today.toISOString().slice(0, 10);
        const end = propertyForm.soleMandateEnd || fallbackEnd.toISOString().slice(0, 10);
        await propertyService.createSoleMandate(editingProperty.propertyId, { startDate: start, endDate: end });
      } else if (!propertyForm.soleMandate && existingMandate) {
        await propertyService.closeSoleMandate(editingProperty.propertyId, existingMandate.soleMandateId);
      } else if (propertyForm.soleMandate && existingMandate) {
        // Re-saving with same dates is a no-op; the user can change dates
        // and we'll close+reopen (server enforces one-active constraint).
        const newStart = propertyForm.soleMandateStart;
        const newEnd = propertyForm.soleMandateEnd;
        if (newStart && newEnd && (newStart !== existingMandate.startDate?.slice(0, 10) || newEnd !== existingMandate.endDate?.slice(0, 10))) {
          await propertyService.closeSoleMandate(editingProperty.propertyId, existingMandate.soleMandateId);
          await propertyService.createSoleMandate(editingProperty.propertyId, { startDate: newStart, endDate: newEnd });
        }
      }
      setEditingProperty(null);
      setEditErrorMessage('');
    } catch (err) {
      setEditErrorMessage(err?.message || err || 'Failed to update property.');
    }
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
          <span className="text-xs font-semibold text-slate-400 uppercase tracking-widest">
            Matched System Assets ({properties.length} of {totalCount || properties.length})
          </span>
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
                        <Can permission="properties:edit">
                          <button type="button" onClick={(e) => { e.stopPropagation(); openEditProperty(prop.propertyId); }} className="p-1.5 bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-white rounded-lg transition-colors" title="Edit property"><Pencil size={14} /></button>
                        </Can>
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

                     {/* 📜 Sole-mandate expiration banner (14-day warning). Shown
                         on every list card so the agent can see at a glance which
                         mandates are about to lapse. */}
                     {Number.isFinite(prop.soleMandateDaysRemaining) && prop.soleMandateDaysRemaining != null && prop.soleMandateDaysRemaining <= 14 && (
                       <div className={`mt-2 flex items-start gap-2 p-2.5 rounded-xl border text-[10px] leading-snug ${prop.soleMandateDaysRemaining <= 0 ? 'bg-red-950/50 border-red-700/50 text-red-200' : prop.soleMandateDaysRemaining <= 7 ? 'bg-red-950/30 border-red-800/50 text-red-300' : 'bg-amber-950/40 border-amber-700/50 text-amber-200'}`}>
                         <span className="text-sm leading-none">📜</span>
                         <span className="font-semibold">
                           {prop.soleMandateDaysRemaining <= 0
                             ? 'Sole mandate has expired — renew to continue marketing.'
                             : `Sole mandate expires in ${prop.soleMandateDaysRemaining} day${prop.soleMandateDaysRemaining === 1 ? '' : 's'}.`}
                         </span>
                       </div>
                     )}

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

        {/* 📑 Pagination footer — list is fetched in one round-trip so this
            just exposes the total count via the shared <Pagination /> component. */}
        {!isLoading && properties.length > 0 && (
          <Pagination
            page={page}
            pageSize={Math.max(1, properties.length)}
            totalCount={totalCount || properties.length}
            onPageChange={() => {}}
            className="pt-2"
          />
        )}
      </div>

      {/* Edit Property Modal */}
      {editingProperty && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-md flex items-center justify-center p-4 z-50 animate-fade-in">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 max-w-2xl w-full space-y-4 shadow-2xl max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-white uppercase tracking-wider">Edit Property</h3>
              <button type="button" onClick={() => { setEditingProperty(null);   }} className="text-slate-400 hover:text-white"><X size={14} /></button>
            </div>
            <form onSubmit={handleUpdateProperty} className="space-y-3">
              {editErrorMessage && (
                <div className="p-2 bg-red-950/30 border border-red-900 rounded-lg text-red-300 text-xs">
                  {editErrorMessage}
                </div>
              )}
              <GooglePropertyMapPicker
                address={propertyForm.completeAddress}
                latitude={propertyForm.latitude}
                longitude={propertyForm.longitude}
                onChange={handleEditLocationChange}
                required
                height={260}
              />
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">ERF Size</label><input type="text" value={propertyForm.erfSpatialSize} onChange={(e) => setPropertyForm({ ...propertyForm, erfSpatialSize: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Title Deed Type</label><select value={propertyForm.titleDeedType} onChange={(e) => setPropertyForm({ ...propertyForm, titleDeedType: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"><option>Freehold</option><option>Sectional Title</option><option>Leasehold</option></select></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Zoning</label><input type="text" value={propertyForm.portfolioZoning} onChange={(e) => setPropertyForm({ ...propertyForm, portfolioZoning: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Market Class</label><select value={propertyForm.marketClass} onChange={(e) => setPropertyForm({ ...propertyForm, marketClass: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"><option>Sale</option><option>Rent</option><option>Both</option></select></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Property Type ID</label><input type="number" value={propertyForm.propertyTypeId} onChange={(e) => setPropertyForm({ ...propertyForm, propertyTypeId: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Valuation Price (R)</label><input type="number" value={propertyForm.valuationPrice} onChange={(e) => setPropertyForm({ ...propertyForm, valuationPrice: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Sale Price (R)</label><input type="number" value={propertyForm.salePrice} onChange={(e) => setPropertyForm({ ...propertyForm, salePrice: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Latitude</label><input type="number" step="any" value={propertyForm.latitude} onChange={(e) => handleEditCoordinateChange('latitude', e.target.value)} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Longitude</label><input type="number" step="any" value={propertyForm.longitude} onChange={(e) => handleEditCoordinateChange('longitude', e.target.value)} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1 md:col-span-2"><label className="text-[10px] text-slate-400 uppercase">Linked Owner ID</label><input type="text" value={propertyForm.linkedOwnerId} onChange={(e) => setPropertyForm({ ...propertyForm, linkedOwnerId: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
              </div>

              {/* 📜 SOLE MANDATE — stylish toggle + date pickers + obligation reminder */}
              <div className="md:col-span-2 p-4 rounded-2xl border border-amber-800/40 bg-gradient-to-br from-amber-950/30 to-slate-900/40 space-y-3">
                <label className="flex items-center gap-3 cursor-pointer select-none">
                  <span className="relative inline-flex">
                    <input
                      type="checkbox"
                      checked={!!propertyForm.soleMandate}
                      onChange={(e) => setPropertyForm({ ...propertyForm, soleMandate: e.target.checked })}
                      className="peer sr-only"
                    />
                    <span className={`w-11 h-6 rounded-full transition-colors ${propertyForm.soleMandate ? 'bg-amber-500' : 'bg-slate-700'}`}></span>
                    <span className={`absolute top-0.5 left-0.5 w-5 h-5 rounded-full bg-white shadow transition-transform ${propertyForm.soleMandate ? 'translate-x-5' : ''}`}></span>
                  </span>
                  <span className="text-xs font-bold text-amber-200 uppercase tracking-[0.12em]">📜 Property is under sole mandate</span>
                </label>

                {propertyForm.soleMandate && (
                  <>
                    <div className="grid grid-cols-2 gap-3">
                      <div className="space-y-1">
                        <label className="text-[10px] text-slate-400 uppercase">Start Date</label>
                        <input type="date" value={propertyForm.soleMandateStart} onChange={(e) => setPropertyForm({ ...propertyForm, soleMandateStart: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-amber-500" />
                      </div>
                      <div className="space-y-1">
                        <label className="text-[10px] text-slate-400 uppercase">End Date</label>
                        <input type="date" value={propertyForm.soleMandateEnd} onChange={(e) => setPropertyForm({ ...propertyForm, soleMandateEnd: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-amber-500" />
                      </div>
                    </div>
                    <div className="flex items-start gap-2 p-3 rounded-xl bg-amber-500/10 border border-amber-600/40 text-amber-200 text-[11px] leading-relaxed">
                      <span className="text-base leading-none">⚠️</span>
                      <span>
                        <strong className="font-bold">Obligation:</strong> the signed mandate legal documents must be uploaded under this property's documents tab before any marketing or show-day activity begins. Mark the date range above and save to open the mandate.
                      </span>
                    </div>
                  </>
                )}
              </div>
              <div className="flex gap-2">
                <button type="submit" className="flex-1 py-2.5 rounded-xl bg-blue-600 text-white text-xs font-bold">Save Changes</button>
                <button type="button" onClick={() => { setEditingProperty(null);   }} className="py-2.5 px-4 rounded-xl text-xs font-bold bg-slate-800 text-slate-300">Cancel</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
