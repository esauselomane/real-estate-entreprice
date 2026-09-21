import GooglePropertyMapPicker from './maps/GooglePropertyMapPicker.jsx';
import GooglePropertyMap from './maps/GooglePropertyMap.jsx';
import { getPropertyTypeColor } from './maps/googlePropertyMapUtils.js';
import { useState, useEffect, useMemo } from 'react';
import { Building2, SlidersHorizontal, Loader2, Layers, ListCollapse, Search, Plus, X, Save } from 'lucide-react';
import { propertyService } from '../services/propertyService.js';
import { useTenant } from '../contexts/TenantContext.jsx';
import Can from '../components/Can.jsx';

function hasValidCoordinates(latitude, longitude) {
  if (latitude == null || longitude == null || String(latitude).trim() === '' || String(longitude).trim() === '') {
    return false;
  }

  const numericLatitude = Number(latitude);
  const numericLongitude = Number(longitude);
  return Number.isFinite(numericLatitude) && Number.isFinite(numericLongitude) &&
    Math.abs(numericLatitude) <= 90 && Math.abs(numericLongitude) <= 180;
}

export default function PropertyMapWorkspace({ onSelectProperty }) {
  const { activeTenantId, isResolvingSubtree } = useTenant();
  const [properties, setProperties] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [selectedMapMarkerId, setSelectedMapMarkerId] = useState(null);
  const [activeTypeFilter, setActiveTypeFilter] = useState('All');
  const [searchTerm, setSearchTerm] = useState('');
  const [showAddModal, setShowAddModal] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState('');
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [hasSelectedGoogleLocation, setHasSelectedGoogleLocation] = useState(false);

  const [propertyForm, setPropertyForm] = useState({
    completeAddress: '',
    erfSpatialSize: '',
    titleDeedType: 'Freehold',
    portfolioZoning: 'Commercial Zone',
    marketClass: 'Sale',
    propertyTypeId: 1,
    valuationPrice: '',
    salePrice: '',
    tenantInstallationAllowance: 0,
    latitude: '',
    longitude: '',
    linkedOwnerId: 'OT-0001',
    subUnits: []
  });

  useEffect(() => {
    const fetchSpatialAssetsForMapping = async () => {
      setIsLoading(true);
      setErrorMessage('');
      try {
        const assets = await propertyService.searchProperties([], 'All', null, null, 'All', 1, 10000);
        setProperties(assets || []);
      } catch (err) {
        setErrorMessage(err || 'Failed to populate spatial markers onto map canvases.');
      } finally {
        setIsLoading(false);
      }
    };
    fetchSpatialAssetsForMapping();
  }, [activeTenantId]);

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
  const filteredMapAssets = useMemo(() => properties.filter((property) => {
    if (activeTypeFilter !== 'All' && property.propertyTypeDetails?.name !== activeTypeFilter) return false;
    if (search) {
      return (
        String(property.completeAddress || '').toLowerCase().includes(search) ||
        String(property.portfolioZoning || '').toLowerCase().includes(search) ||
        String(property.propertyId || '').toLowerCase().includes(search)
      );
    }
    return true;
  }), [properties, activeTypeFilter, search]);

  const mappableAssets = useMemo(() => filteredMapAssets.filter((property) =>
    hasValidCoordinates(property.latitude, property.longitude)
  ), [filteredMapAssets]);

  // 🏷️ Legend data: count of markers per property type, plus a count of
  // For-Sale / For-Rent / Both so the legend stays accurate when the
  // tenant filter shrinks the visible set.
  const legend = useMemo(() => {
    const byType = {};
    let forSale = 0;
    let forRent = 0;
    let both = 0;
    let neither = 0;
    mappableAssets.forEach((p) => {
      const name = p.propertyTypeDetails?.name || 'Other';
      byType[name] = (byType[name] || 0) + 1;
      if (p.isForSale && p.isForRent) both++;
      else if (p.isForSale) forSale++;
      else if (p.isForRent) forRent++;
      else neither++;
    });
    return {
      byType: Object.entries(byType)
        .sort((a, b) => b[1] - a[1])
        .map(([name, count]) => ({ name, count, color: getPropertyTypeColor(name) })),
      forSale,
      forRent,
      both,
      neither,
    };
  }, [mappableAssets]);


  const openProperty = (propertyId) => {
    setSelectedMapMarkerId(propertyId);
    onSelectProperty && onSelectProperty(propertyId);
  };

  const handleOpenAddModal = () => {
    setPropertyForm({
      completeAddress: '', erfSpatialSize: '', titleDeedType: 'Freehold',
      portfolioZoning: 'Commercial Zone', marketClass: 'Sale', propertyTypeId: 1,
      valuationPrice: '', salePrice: '', tenantInstallationAllowance: 0,
      latitude: '', longitude: '', linkedOwnerId: 'OT-0001',
      isForSale: true, isForRent: false, subUnits: []
    });
    setSaveError('');
    setSaveSuccess(false);
    setHasSelectedGoogleLocation(false);
    setShowAddModal(true);
  };

  const handleFormChange = (field, value) => {
    if (field === 'latitude' || field === 'longitude') {
      setHasSelectedGoogleLocation(false);
    }
    setPropertyForm((prev) => {
      const next = { ...prev, [field]: value };
      // 🧬 Derive the legacy MarketClass string from the two independent
      // listing flags so backend / search callers that still read it see a
      // consistent value.
      if (field === 'isForSale' || field === 'isForRent') {
        const sale = field === 'isForSale' ? value : prev.isForSale;
        const rent = field === 'isForRent' ? value : prev.isForRent;
        next.marketClass = sale && rent ? 'Both' : sale ? 'Sale' : rent ? 'Rental' : 'Off Market';
      }
      return next;
    });
  };

  const handleMapClick = () => {
    setSelectedMapMarkerId(null);
  };

  const handlePropertyLocationChange = (location) => {
    setPropertyForm((previous) => ({ ...previous, ...location }));
    setHasSelectedGoogleLocation(hasValidCoordinates(location.latitude, location.longitude));
  };

  const handleSaveProperty = async (event) => {
    event.preventDefault();
    setSaveError('');
    setSaveSuccess(false);

    const latitude = Number(propertyForm.latitude);
    const longitude = Number(propertyForm.longitude);
    if (!propertyForm.completeAddress.trim()) {
      setSaveError('Property address is required.');
      return;
    }
    if (!hasSelectedGoogleLocation || !hasValidCoordinates(latitude, longitude)) {
      setSaveError('Select a Google address or drop a pin on the map before creating the property.');
      return;
    }
    if (!propertyForm.isForSale && !propertyForm.isForRent) {
      setSaveError('Select at least one listing type (For Sale or For Rent).');
      return;
    }

    setIsSaving(true);
    try {
      await propertyService.createProperty({
        ...propertyForm,
        valuationPrice: Number(propertyForm.valuationPrice) || 0,
        salePrice: propertyForm.salePrice ? Number(propertyForm.salePrice) : null,
        tenantInstallationAllowance: Number(propertyForm.tenantInstallationAllowance) || 0,
        latitude,
        longitude,
        propertyTypeId: Number(propertyForm.propertyTypeId) || 1,
        isForSale: !!propertyForm.isForSale,
        isForRent: !!propertyForm.isForRent
      });
      setSaveSuccess(true);
      setShowAddModal(false);
      const assets = await propertyService.searchProperties([], 'All', null, null);
      setProperties(assets || []);
    } catch (error) {
      setSaveError(error || 'Failed to create property.');
    } finally {
      setIsSaving(false);
    }
  };

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

        {/* Address / id search + Add Property button */}
        <div className="relative flex items-center gap-2">
          <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500" />
          <input
            type="text"
            placeholder="Search address or property #"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full pl-9 pr-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500"
          />
          <Can permission="properties:create">
            <button
              type="button"
              onClick={handleOpenAddModal}
              className="shrink-0 px-3 py-2 bg-blue-600 hover:bg-blue-500 rounded-xl text-xs font-bold text-white flex items-center gap-1"
            >
              <Plus size={14} /> Add Property
            </button>
          </Can>
        </div>

        {/* Dynamic Asset Record Cards Scrollbox */}
        <div className="flex-1 overflow-y-auto pr-1 space-y-3 max-h-[60vh] lg:max-h-[calc(100vh-22rem)]">
          {errorMessage && (
            <div className="p-4 bg-red-950/30 border border-red-900/60 text-red-400 rounded-2xl text-xs font-medium text-center">
              {errorMessage}
            </div>
          )}

          {isResolvingSubtree && (
            <div className="p-3 bg-blue-950/30 border border-blue-900/60 text-blue-200 rounded-2xl text-[11px] font-medium text-center flex items-center justify-center gap-2">
              <Loader2 className="w-3 h-3 animate-spin" /> Loading new tenant scope…
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

      {/* 🗺️ RIGHT SIDE PANEL: GOOGLE MAP */}
      <div className="lg:col-span-2 bg-slate-900 border border-slate-800 rounded-2xl shadow-xl flex flex-col h-[50vh] lg:h-full relative overflow-hidden p-3 min-h-[450px]">
        <div className="absolute top-5 left-5 z-10 bg-slate-950/80 backdrop-blur-md px-3 py-1.5 rounded-xl border border-slate-800 text-[11px] font-semibold text-slate-300 flex items-center gap-2 shadow-lg shadow-black/40">
          <Layers size={12} className="text-blue-500 animate-pulse" /> Live Multi-Tenant Mapping Canvas
        </div>

        {mappableAssets.length > 0 && (
          <div className="absolute bottom-5 right-5 z-10 bg-slate-950/85 backdrop-blur-md border border-slate-800 rounded-xl shadow-lg shadow-black/40 p-3 w-56 text-[11px] text-slate-200 max-h-[60%] overflow-y-auto">
            <div className="text-[10px] uppercase tracking-widest text-slate-500 font-bold mb-2">Marker Legend</div>

            <div className="space-y-1.5 mb-3">
              {legend.byType.map(({ name, count, color }) => (
                <div key={name} className="flex items-center justify-between gap-2">
                  <div className="flex items-center gap-2 min-w-0">
                    <span
                      className="inline-block w-3 h-3 rounded-full shrink-0 border border-white/30"
                      style={{ backgroundColor: color }}
                    />
                    <span className="truncate">{name}</span>
                  </div>
                  <span className="text-slate-400 font-semibold tabular-nums">{count}</span>
                </div>
              ))}
            </div>

            <div className="border-t border-slate-800 pt-2 space-y-1">
              <div className="text-[10px] uppercase tracking-widest text-slate-500 font-bold mb-1">Listing</div>
              <div className="flex items-center justify-between"><span>For Sale</span><span className="text-emerald-300 font-semibold tabular-nums">{legend.forSale}</span></div>
              <div className="flex items-center justify-between"><span>For Rent</span><span className="text-cyan-300 font-semibold tabular-nums">{legend.forRent}</span></div>
              <div className="flex items-center justify-between"><span>Sale + Rent</span><span className="text-amber-300 font-semibold tabular-nums">{legend.both}</span></div>
              {legend.neither > 0 && (
                <div className="flex items-center justify-between"><span>Off-market</span><span className="text-slate-400 font-semibold tabular-nums">{legend.neither}</span></div>
              )}
            </div>
          </div>
        )}

        <GooglePropertyMap
          properties={mappableAssets}
          selectedPropertyId={selectedMapMarkerId}
          onSelectProperty={openProperty}
          onMapClick={handleMapClick}
          className="h-full"
        />
      </div>

      {/* 🏗️ ADD PROPERTY MODAL */}
      {showAddModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 w-full max-w-3xl max-h-[85vh] overflow-y-auto shadow-2xl">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-white font-bold uppercase tracking-[0.1em] text-[11px] flex items-center gap-2">
                <Plus size={14} className="text-blue-500" /> Add Property
              </h3>
              <button
                type="button"
                onClick={() => setShowAddModal(false)}
                className="text-slate-400 hover:text-white"
              >
                <X size={16} />
              </button>
            </div>

            {saveError && <div className="mb-3 p-2 bg-red-950/30 border border-red-900 rounded-lg text-red-300 text-xs">{saveError}</div>}
            {saveSuccess && <div className="mb-3 p-2 bg-emerald-950/30 border border-emerald-900 rounded-lg text-emerald-300 text-xs">Property created successfully!</div>}

            <form onSubmit={handleSaveProperty} className="grid grid-cols-2 gap-3">
              <GooglePropertyMapPicker
                address={propertyForm.completeAddress}
                latitude={propertyForm.latitude}
                longitude={propertyForm.longitude}
                onChange={handlePropertyLocationChange}
                required
                height={300}
              />

              <div>
                <label className="block text-[10px] text-slate-400 uppercase mb-1">Erf / Spatial Size</label>
                <input
                  type="text"
                  value={propertyForm.erfSpatialSize}
                  onChange={(e) => handleFormChange('erfSpatialSize', e.target.value)}
                  placeholder="e.g. 850 sqm"
                  className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white"
                />
              </div>
              <div>
                <label className="block text-[10px] text-slate-400 uppercase mb-1">Valuation Price</label>
                <input
                  type="number" step="0.01"
                  value={propertyForm.valuationPrice}
                  onChange={(e) => handleFormChange('valuationPrice', e.target.value)}
                  placeholder="0.00"
                  className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white"
                />
              </div>

              <div>
                <label className="block text-[10px] text-slate-400 uppercase mb-1">Title Deed Type</label>
                <select
                  value={propertyForm.titleDeedType}
                  onChange={(e) => handleFormChange('titleDeedType', e.target.value)}
                  className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white"
                >
                  <option value="Freehold">Freehold</option>
                  <option value="Leasehold">Leasehold</option>
                  <option value="Sectional Title">Sectional Title</option>
                </select>
              </div>
              <div>
                <label className="block text-[10px] text-slate-400 uppercase mb-1">Portfolio Zoning</label>
                <input
                  type="text"
                  value={propertyForm.portfolioZoning}
                  onChange={(e) => handleFormChange('portfolioZoning', e.target.value)}
                  className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white"
                />
              </div>

              <div>
                <label className="block text-[10px] text-slate-400 uppercase mb-2">Listing Type</label>
                <div className="flex flex-col gap-2 px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl">
                  <label className="flex items-center gap-2 text-xs text-white cursor-pointer">
                    <input
                      type="checkbox"
                      checked={!!propertyForm.isForSale}
                      onChange={(e) => handleFormChange('isForSale', e.target.checked)}
                      className="rounded border-slate-600 bg-slate-900 text-emerald-500 focus:ring-emerald-500"
                    />
                    <span>For Sale</span>
                  </label>
                  <label className="flex items-center gap-2 text-xs text-white cursor-pointer">
                    <input
                      type="checkbox"
                      checked={!!propertyForm.isForRent}
                      onChange={(e) => handleFormChange('isForRent', e.target.checked)}
                      className="rounded border-slate-600 bg-slate-900 text-blue-500 focus:ring-blue-500"
                    />
                    <span>To Rent</span>
                  </label>
                </div>
                <p className="text-[10px] text-slate-500 mt-1">Tick both to market the property as For Sale and To Rent.</p>
              </div>
              <div>
                <label className="block text-[10px] text-slate-400 uppercase mb-1">Property Type ID</label>
                <input
                  type="number"
                  value={propertyForm.propertyTypeId}
                  onChange={(e) => handleFormChange('propertyTypeId', e.target.value)}
                  className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white"
                />
              </div>

              <div>
                <label className="block text-[10px] text-slate-400 uppercase mb-1">Sale Price</label>
                <input
                  type="number" step="0.01"
                  value={propertyForm.salePrice}
                  onChange={(e) => handleFormChange('salePrice', e.target.value)}
                  placeholder="0.00"
                  className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white"
                />
              </div>
              <div>
                <label className="block text-[10px] text-slate-400 uppercase mb-1">Tenant Install Allowance</label>
                <input
                  type="number" step="0.01"
                  value={propertyForm.tenantInstallationAllowance}
                  onChange={(e) => handleFormChange('tenantInstallationAllowance', e.target.value)}
                  className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white"
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-[10px] text-slate-400 uppercase mb-1">Latitude</label>
                  <input
                    type="number" step="0.000001"
                    value={propertyForm.latitude}
                    onChange={(e) => handleFormChange('latitude', e.target.value)}
                    className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white"
                  />
                </div>
                <div>
                  <label className="block text-[10px] text-slate-400 uppercase mb-1">Longitude</label>
                  <input
                    type="number" step="0.000001"
                    value={propertyForm.longitude}
                    onChange={(e) => handleFormChange('longitude', e.target.value)}
                    className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white"
                  />
                </div>
              </div>
              <div className="col-span-2">
                <label className="block text-[10px] text-slate-400 uppercase mb-1">Linked Owner ID</label>
                <input
                  type="text"
                  value={propertyForm.linkedOwnerId}
                  onChange={(e) => handleFormChange('linkedOwnerId', e.target.value)}
                  className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white"
                />
              </div>

              <div className="col-span-2 flex justify-end gap-2 mt-4 pt-4 border-t border-slate-800">
                <button
                  type="button"
                  onClick={() => setShowAddModal(false)}
                  className="px-4 py-2 bg-slate-800 text-slate-300 rounded-xl text-xs font-bold hover:bg-slate-700"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isSaving}
                  className="px-4 py-2 bg-blue-600 text-white rounded-xl text-xs font-bold hover:bg-blue-500 disabled:opacity-50 flex items-center gap-1"
                >
                  {isSaving ? <Loader2 size={14} className="animate-spin" /> : <Save size={14} />}
                  {isSaving ? 'Saving...' : 'Create Property'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

    </div>
  );
}
