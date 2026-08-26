import React, { useState, useEffect } from 'react';
import { 
  ArrowLeft, MapPin, FileText, User, Home, CheckCircle2, 
  ExternalLink, Image, Plus, Trash2, Layers, DollarSign, 
  Building, Check, Loader2, AlertCircle 
} from 'lucide-react';
import { propertyService } from '../services/ propertyService.js';

export default function PropertyDetails({ propertyId, onBack, onNavigateToClient }) {
  const [property, setProperty] = useState(null);
  const [subUnits, setSubUnits] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState('');
  const [platforms, setPlatforms] = useState([]);
  const [imagePreview, setImagePreview] = useState(null);
  const [isUploadingImage, setIsUploadingImage] = useState(false);
  const [actionSuccessMessage, setActionSuccessMessage] = useState('');

  // New Sub-Unit form state
  const [showAddUnitForm, setShowAddUnitForm] = useState(false);
  const [isSubmittingUnit, setIsSubmittingUnit] = useState(false);
  const [newUnit, setNewUnit] = useState({
    unitNumber: '',
    unitDescription: '',
    netRentableAreaSqM: 50,
    specificUnitRentalPrice: '',
    specificUnitSalePrice: '',
    isCurrentlyVacant: true
  });

  // Load live property and its linked units
  const loadPropertyData = async () => {
    setIsLoading(true);
    setErrorMessage('');
    try {
      const data = await propertyService.getPropertyById(propertyId);
      setProperty(data);
      setSubUnits(data.subUnits || []);
      
      // Load platforms lookup
      try {
        const platList = await propertyService.getPlatforms();
        setPlatforms(platList || []);
      } catch {
        setPlatforms([
          { platformId: 1, platformName: 'Property24', baseUrlAddress: 'https://property24.com' },
          { platformId: 2, platformName: 'PrivateProperty', baseUrlAddress: 'https://privateproperty.co.za' },
          { platformId: 3, platformName: 'Gumtree', baseUrlAddress: 'https://gumtree.co.za' }
        ]);
      }
    } catch (err) {
      setErrorMessage(typeof err === 'string' ? err : 'Failed to load property details from database.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (propertyId) {
      loadPropertyData();
    }
  }, [propertyId]);

  // Handle Image Upload
  const handleImageUpload = async (e) => {
    const file = e.target.files[0];
    if (!file) return;

    setImagePreview(URL.createObjectURL(file));
    setIsUploadingImage(true);
    setActionSuccessMessage('');

    try {
      await propertyService.uploadPropertyImage(propertyId, file);
      setActionSuccessMessage('Image dispatched to Kafka pipeline for background optimization.');
      setTimeout(() => setActionSuccessMessage(''), 5000);
    } catch (err) {
      alert(`Image upload error: ${err}`);
    } finally {
      setIsUploadingImage(false);
    }
  };

  // Handle Adding a new Sub-Unit
  const handleAddSubUnit = async (e) => {
    e.preventDefault();
    if (!newUnit.unitNumber.trim()) {
      alert('Please provide a unit number / name.');
      return;
    }

    setIsSubmittingUnit(true);
    try {
      const createdUnit = await propertyService.addPropertyUnit(propertyId, {
        unitNumber: newUnit.unitNumber.trim(),
        unitDescription: newUnit.unitDescription.trim(),
        netRentableAreaSqM: parseFloat(newUnit.netRentableAreaSqM) || 0,
        specificUnitRentalPrice: newUnit.specificUnitRentalPrice ? parseFloat(newUnit.specificUnitRentalPrice) : null,
        specificUnitSalePrice: newUnit.specificUnitSalePrice ? parseFloat(newUnit.specificUnitSalePrice) : null,
        isCurrentlyVacant: newUnit.isCurrentlyVacant
      });

      setSubUnits(prev => [...prev, createdUnit]);
      setNewUnit({
        unitNumber: '',
        unitDescription: '',
        netRentableAreaSqM: 50,
        specificUnitRentalPrice: '',
        specificUnitSalePrice: '',
        isCurrentlyVacant: true
      });
      setShowAddUnitForm(false);
      setActionSuccessMessage(`Successfully linked ${createdUnit.unitNumber} to this property.`);
      setTimeout(() => setActionSuccessMessage(''), 4000);
    } catch (err) {
      alert(`Failed to add unit: ${err}`);
    } finally {
      setIsSubmittingUnit(false);
    }
  };

  // Handle Deleting a Sub-Unit
  const handleDeleteSubUnit = async (unitId, unitNumber) => {
    if (!window.confirm(`Are you sure you want to remove ${unitNumber} from this property?`)) return;

    try {
      await propertyService.deletePropertyUnit(propertyId, unitId);
      setSubUnits(prev => prev.filter(u => u.propertyUnitId !== unitId));
      setActionSuccessMessage(`Sub-unit ${unitNumber} unlinked successfully.`);
      setTimeout(() => setActionSuccessMessage(''), 4000);
    } catch (err) {
      alert(`Failed to delete unit: ${err}`);
    }
  };

  // Handle Syndication Toggle
  const handleToggleSyndication = async (platformId, currentStatus) => {
    const newStatus = !currentStatus;
    try {
      await propertyService.togglePortalSyndication(propertyId, platformId, newStatus);
      const updated = await propertyService.getPropertyById(propertyId);
      setProperty(updated);
    } catch (err) {
      alert(`Syndication update failed: ${err}`);
    }
  };

  if (isLoading) {
    return (
      <div className="py-24 flex flex-col items-center justify-center text-slate-500 gap-3">
        <Loader2 className="w-8 h-8 text-blue-500 animate-spin" />
        <span className="text-xs font-medium tracking-wide">Loading property asset and linked sub-units from SQL database...</span>
      </div>
    );
  }

  if (errorMessage || !property) {
    return (
      <div className="p-8 bg-red-950/30 border border-red-900/60 rounded-2xl text-center space-y-4">
        <AlertCircle className="w-10 h-10 text-red-400 mx-auto" />
        <h3 className="text-base font-bold text-red-300">Property Retrieval Failed</h3>
        <p className="text-xs text-red-400 max-w-md mx-auto">{errorMessage || 'The requested property could not be found.'}</p>
        <button 
          onClick={onBack}
          className="inline-flex items-center gap-2 text-xs font-semibold text-white bg-slate-800 hover:bg-slate-700 px-4 py-2 rounded-xl transition-colors cursor-pointer"
        >
          <ArrowLeft size={14} /> Return to Search
        </button>
      </div>
    );
  }

  const isSyndicatedTo = (platformId) => {
    return property.syndicationLinks?.some(s => s.platformId === platformId && s.isSyndicatedActive);
  };

  const getSyndicationDetails = (platformId) => {
    return property.syndicationLinks?.find(s => s.platformId === platformId);
  };

  const totalNRA = subUnits.reduce((acc, u) => acc + (parseFloat(u.netRentableAreaSqM) || 0), 0);
  const vacantUnitsCount = subUnits.filter(u => u.isCurrentlyVacant).length;

  return (
    <div className="space-y-6 animate-fade-in">
      
      {/* 🧭 Top Navigation & Status Bar */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <button 
          onClick={onBack}
          className="inline-flex items-center gap-2 text-xs font-semibold text-slate-400 hover:text-white bg-slate-900 border border-slate-800 px-4 py-2 rounded-xl transition-colors cursor-pointer shadow-sm"
        >
          <ArrowLeft size={14} /> Back to Asset Registry
        </button>
        <div className="flex items-center gap-2">
          <span className="text-xs font-mono text-slate-400 bg-slate-900 px-3 py-1.5 rounded-lg border border-slate-800">
            Property ID: <strong className="text-white">#{property.propertyId}</strong>
          </span>
          <span className="text-xs font-mono text-slate-400 bg-slate-900 px-3 py-1.5 rounded-lg border border-slate-800">
            Tenant Sandbox: <strong className="text-blue-400">{property.tenantId}</strong>
          </span>
        </div>
      </div>

      {actionSuccessMessage && (
        <div className="p-3.5 bg-emerald-950/40 border border-emerald-800/80 text-emerald-300 rounded-xl text-xs flex items-center gap-2 animate-fade-in">
          <CheckCircle2 size={16} className="text-emerald-400 shrink-0" />
          <span>{actionSuccessMessage}</span>
        </div>
      )}

      {/* Main Multi-Column Split Workspace Frame Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        
        {/* 🏢 LEFT & CENTER: Core Attributes, Map & Multi-Tenant Units */}
        <div className="lg:col-span-2 space-y-6">
          
          {/* Core Property Attributes Layout Box */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
            <div className="flex justify-between items-start flex-wrap gap-2 mb-2">
              <div>
                <h3 className="text-xl font-black text-white tracking-tight">{property.completeAddress}</h3>
                <p className="text-xs text-blue-400 font-medium uppercase tracking-wider mt-0.5">
                  {property.portfolioZoning} &bull; {property.propertyTypeDetails?.name || 'Commercial'} Sector
                </p>
              </div>
              <span className={`text-xs font-bold uppercase px-3 py-1 rounded-lg border ${
                property.marketClass === 'Sale' 
                  ? 'bg-emerald-950 text-emerald-400 border-emerald-900' 
                  : 'bg-blue-950 text-blue-400 border-blue-900'
              }`}>
                For {property.marketClass}
              </span>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 p-4 bg-slate-950/60 rounded-xl border border-slate-800/80 my-4">
              <div>
                <span className="block text-[10px] text-slate-500 font-bold uppercase">Erf Spatial Size</span>
                <span className="text-sm font-bold text-white mt-0.5 block">{property.erfSpatialSize}</span>
              </div>
              <div>
                <span className="block text-[10px] text-slate-500 font-bold uppercase">Title Deed Type</span>
                <span className="text-sm font-bold text-white mt-0.5 block">{property.titleDeedType}</span>
              </div>
              <div>
                <span className="block text-[10px] text-slate-500 font-bold uppercase">Appraisal Valuation</span>
                <span className="text-sm font-bold text-emerald-400 mt-0.5 block">
                  R {property.valuationPrice ? property.valuationPrice.toLocaleString() : 'N/A'}
                </span>
              </div>
              <div>
                <span className="block text-[10px] text-slate-500 font-bold uppercase">
                  {property.marketClass === 'Rental' ? 'Monthly Base Rental' : 'Target Sale Price'}
                </span>
                <span className="text-sm font-bold text-white mt-0.5 block">
                  {property.marketClass === 'Rental' 
                    ? `R ${(property.baseRentalRate || 0).toLocaleString()}/mo`
                    : `R ${(property.salePrice || property.valuationPrice || 0).toLocaleString()}`}
                </span>
              </div>
            </div>

            {property.marketClass === 'Rental' && (
              <div className="grid grid-cols-3 gap-3 p-3 bg-slate-950/40 rounded-xl border border-slate-800 text-xs mb-4">
                <div>
                  <span className="text-slate-500 block text-[10px] uppercase font-bold">Annual Escalation</span>
                  <span className="text-slate-200 font-semibold">{property.rentEscalationPercentage || 7.5}% / yr</span>
                </div>
                <div>
                  <span className="text-slate-500 block text-[10px] uppercase font-bold">Mandated Term</span>
                  <span className="text-slate-200 font-semibold">{property.leaseTermMonths || 12} Months</span>
                </div>
                <div>
                  <span className="text-slate-500 block text-[10px] uppercase font-bold">TI Fit-out Allowance</span>
                  <span className="text-amber-400 font-semibold">R {(property.tenantInstallationAllowance || 0).toLocaleString()}</span>
                </div>
              </div>
            )}

            {/* 🗺️ Geospatial Coordinates Container */}
            <div className="h-44 w-full bg-slate-950 border border-slate-800 rounded-xl flex flex-col items-center justify-center p-4 text-center relative overflow-hidden">
              <MapPin size={28} className="text-blue-500 mb-1 animate-bounce" />
              <span className="text-xs font-bold text-slate-300">Geospatial Positioning Coordinate Register</span>
              <span className="text-[11px] font-mono text-slate-400 mt-1">
                Latitude: {property.latitude} &bull; Longitude: {property.longitude}
              </span>
              <div className="absolute inset-0 bg-gradient-to-t from-slate-950 via-transparent to-transparent pointer-events-none" />
            </div>
          </div>

          {/* 🏢 MULTI-TENANT SUB-UNITS LEDGER (LINKED UNITS SECTION) */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
            <div className="flex items-center justify-between flex-wrap gap-2">
              <div className="flex items-center gap-2">
                <Layers size={18} className="text-blue-500" />
                <h4 className="text-sm font-bold text-white uppercase tracking-wider">
                  Linked Property Units ({subUnits.length})
                </h4>
              </div>
              <div className="flex items-center gap-2">
                <span className="text-xs text-slate-400 bg-slate-950 px-2.5 py-1 rounded-lg border border-slate-800">
                  Total NRA: <strong className="text-white">{totalNRA.toFixed(1)} m²</strong>
                </span>
                <span className="text-xs text-slate-400 bg-slate-950 px-2.5 py-1 rounded-lg border border-slate-800">
                  Vacant: <strong className="text-emerald-400">{vacantUnitsCount}</strong> / {subUnits.length}
                </span>
                <button
                  type="button"
                  onClick={() => setShowAddUnitForm(!showAddUnitForm)}
                  className="inline-flex items-center gap-1.5 text-xs font-semibold text-white bg-blue-600 hover:bg-blue-500 px-3 py-1.5 rounded-lg transition-colors cursor-pointer shadow-sm"
                >
                  <Plus size={14} /> {showAddUnitForm ? 'Cancel' : 'Link New Unit'}
                </button>
              </div>
            </div>

            {/* Inline Add New Sub-Unit Form */}
            {showAddUnitForm && (
              <form onSubmit={handleAddSubUnit} className="p-4 bg-slate-950 border border-blue-900/60 rounded-xl space-y-3 animate-fade-in">
                <h5 className="text-xs font-bold text-blue-400 uppercase tracking-wider flex items-center gap-1.5">
                  <Building size={14} /> Link New Sub-Unit to #{property.propertyId}
                </h5>
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                  <div>
                    <label className="block text-[10px] font-bold text-slate-400 uppercase mb-1">Unit / Suite Number *</label>
                    <input 
                      type="text" 
                      placeholder="e.g. Suite 201, Bay B" 
                      value={newUnit.unitNumber}
                      onChange={e => setNewUnit({ ...newUnit, unitNumber: e.target.value })}
                      required
                      className="w-full px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500"
                    />
                  </div>
                  <div>
                    <label className="block text-[10px] font-bold text-slate-400 uppercase mb-1">Net Rentable Area (m²) *</label>
                    <input 
                      type="number" 
                      placeholder="e.g. 120" 
                      value={newUnit.netRentableAreaSqM}
                      onChange={e => setNewUnit({ ...newUnit, netRentableAreaSqM: e.target.value })}
                      required
                      className="w-full px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500"
                    />
                  </div>
                  <div>
                    <label className="block text-[10px] font-bold text-slate-400 uppercase mb-1">
                      {property.marketClass === 'Rental' ? 'Specific Monthly Rental (R)' : 'Specific Sale Price (R)'}
                    </label>
                    <input 
                      type="number" 
                      placeholder="e.g. 18500" 
                      value={property.marketClass === 'Rental' ? newUnit.specificUnitRentalPrice : newUnit.specificUnitSalePrice}
                      onChange={e => property.marketClass === 'Rental' 
                        ? setNewUnit({ ...newUnit, specificUnitRentalPrice: e.target.value })
                        : setNewUnit({ ...newUnit, specificUnitSalePrice: e.target.value })
                      }
                      className="w-full px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-4 gap-3 items-end">
                  <div className="sm:col-span-3">
                    <label className="block text-[10px] font-bold text-slate-400 uppercase mb-1">Unit Description / Specification</label>
                    <input 
                      type="text" 
                      placeholder="e.g. Corner office suite with double glass facade and cold storage" 
                      value={newUnit.unitDescription}
                      onChange={e => setNewUnit({ ...newUnit, unitDescription: e.target.value })}
                      className="w-full px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500"
                    />
                  </div>
                  <div>
                    <button
                      type="submit"
                      disabled={isSubmittingUnit}
                      className="w-full inline-flex items-center justify-center gap-1.5 text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-500 py-2 rounded-lg transition-colors cursor-pointer disabled:opacity-50"
                    >
                      {isSubmittingUnit ? <Loader2 size={14} className="animate-spin" /> : <Plus size={14} />} Save & Link
                    </button>
                  </div>
                </div>
              </form>
            )}

            {/* List of Linked Units */}
            <div className="space-y-2.5">
              {subUnits.length === 0 ? (
                <div className="p-6 bg-slate-950/40 border border-dashed border-slate-800 rounded-xl text-center text-xs text-slate-500">
                  No sub-units currently linked to this property. Click "Link New Unit" to add one.
                </div>
              ) : (
                subUnits.map((unit) => (
                  <div 
                    key={unit.propertyUnitId || unit.unitNumber} 
                    className="p-3.5 bg-slate-950/70 border border-slate-800 rounded-xl flex items-center justify-between gap-4 hover:border-slate-700 transition-colors"
                  >
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <span className="text-xs font-bold text-white tracking-tight">{unit.unitNumber}</span>
                        <span className={`text-[10px] font-bold uppercase px-2 py-0.5 rounded ${
                          unit.isCurrentlyVacant 
                            ? 'bg-emerald-950 text-emerald-400 border border-emerald-900' 
                            : 'bg-slate-800 text-slate-400 border border-slate-700'
                        }`}>
                          {unit.isCurrentlyVacant ? 'Vacant' : 'Occupied'}
                        </span>
                        <span className="text-xs font-semibold text-blue-400 bg-blue-950/60 px-2 py-0.5 rounded border border-blue-900">
                          {unit.netRentableAreaSqM} m² NRA
                        </span>
                      </div>
                      <p className="text-xs text-slate-400">{unit.unitDescription || 'Standard commercial unit profile'}</p>
                    </div>

                    <div className="flex items-center gap-4">
                      {(unit.specificUnitRentalPrice || unit.specificUnitSalePrice) && (
                        <div className="text-right">
                          <span className="block text-[9px] text-slate-500 uppercase font-bold">Unit Price</span>
                          <span className="text-xs font-bold text-emerald-400">
                            R {(unit.specificUnitRentalPrice || unit.specificUnitSalePrice).toLocaleString()}
                            {unit.specificUnitRentalPrice ? '/mo' : ''}
                          </span>
                        </div>
                      )}
                      <button 
                        type="button"
                        onClick={() => handleDeleteSubUnit(unit.propertyUnitId, unit.unitNumber)}
                        title="Remove Sub-unit"
                        className="text-slate-500 hover:text-red-400 p-1.5 rounded-lg hover:bg-slate-800 transition-colors cursor-pointer"
                      >
                        <Trash2 size={14} />
                      </button>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>

          {/* 🖼️ Media Vault & Image Uploader */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
            <h4 className="text-sm font-bold text-white uppercase tracking-wider mb-4 flex items-center gap-2">
              <Image size={16} className="text-blue-500" /> Asset Image Vault Gallery
            </h4>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="border-2 border-dashed border-slate-800 hover:border-slate-700 rounded-xl p-6 flex flex-col items-center justify-center bg-slate-950/40 text-center relative min-h-[128px]">
                <input 
                  type="file" 
                  accept="image/*"
                  onChange={handleImageUpload}
                  disabled={isUploadingImage}
                  className="absolute inset-0 opacity-0 cursor-pointer w-full h-full" 
                />
                {isUploadingImage ? (
                  <Loader2 className="w-6 h-6 text-blue-500 animate-spin" />
                ) : (
                  <>
                    <span className="text-xs text-slate-400 font-medium">Click or Drag to Upload Picture</span>
                    <span className="text-[10px] text-slate-600 mt-1">Dispatches to Kafka Image Worker stream</span>
                  </>
                )}
              </div>

              {imagePreview ? (
                <div className="bg-slate-950 border border-slate-800 rounded-xl p-2 flex items-center justify-center h-32 relative group">
                  <img src={imagePreview} alt="Upload preview" className="h-full w-full object-cover rounded-lg" />
                  <span className="absolute bottom-2 right-2 bg-slate-900/90 text-[9px] px-1.5 py-0.5 rounded text-blue-400 border border-slate-700">Uploaded</span>
                </div>
              ) : (
                <div className="bg-slate-950/20 border border-slate-800/80 rounded-xl flex items-center justify-center text-xs text-slate-600 italic">
                  No images uploaded yet
                </div>
              )}
            </div>
          </div>

        </div>

        {/* 📋 RIGHT: Entities Links & Platform Syndication */}
        <div className="space-y-6">
          
          {/* Linked Legal Entities Card */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
            <h4 className="text-sm font-bold text-white uppercase tracking-wider mb-4 flex items-center gap-2">
              <User size={16} className="text-blue-500" /> Linked Legal Entities
            </h4>
            <div className="space-y-3">
              
              <div className="p-3 bg-slate-950/60 border border-slate-800 rounded-xl">
                <span className="text-[9px] text-amber-500 font-bold uppercase tracking-wider block">Registered Landowner</span>
                <button 
                  type="button"
                  onClick={() => onNavigateToClient(property.linkedOwnerId || 'OT-0001', 'Owner', `Owner ${property.linkedOwnerId || '0001'}`)}
                  className="text-sm font-bold text-white hover:text-blue-400 text-left mt-0.5 cursor-pointer underline decoration-dotted transition-colors block w-full"
                >
                  {property.linkedOwnerId || 'Registered Landowner'}
                </button>
              </div>

              <div className="p-3 bg-slate-950/60 border border-slate-800 rounded-xl">
                <span className="text-[9px] text-blue-400 font-bold uppercase tracking-wider block">Operating Tenant Agency</span>
                <button 
                  type="button"
                  onClick={() => onNavigateToClient(property.tenantId, 'Tenant', `Tenant Agency ${property.tenantId}`)}
                  className="text-sm font-bold text-white hover:text-blue-400 text-left mt-0.5 cursor-pointer underline decoration-dotted transition-colors block w-full"
                >
                  {property.tenantId}
                </button>
              </div>

            </div>
          </div>

          {/* Verification Legal File Attachments */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
            <h4 className="text-sm font-bold text-white uppercase tracking-wider mb-4 flex items-center gap-2">
              <FileText size={16} className="text-blue-500" /> Deeds & Verification Vault
            </h4>
            <div className="space-y-2">
              {['Title_Deed_Verification.pdf', 'Municipal_Zoning_Certificate.pdf'].map((doc, idx) => (
                <div key={idx} className="flex items-center gap-3 p-2.5 bg-slate-950 border border-slate-800 rounded-xl text-xs text-slate-300 group hover:border-slate-700 transition-colors">
                  <FileText size={14} className="text-slate-500 group-hover:text-blue-400" />
                  <span className="truncate flex-1 font-medium">{doc}</span>
                </div>
              ))}
            </div>
          </div>

          {/* Portal Syndication Tracker */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
            <h4 className="text-sm font-bold text-white uppercase tracking-wider mb-4 flex items-center gap-2">
              <Home size={16} className="text-blue-500" /> Portal Syndication Tracker
            </h4>
            <div className="space-y-3">
              {platforms.map((plat) => {
                const active = isSyndicatedTo(plat.platformId);
                const syncDetails = getSyndicationDetails(plat.platformId);

                return (
                  <div key={plat.platformId} className="flex items-center justify-between p-3 bg-slate-950/60 border border-slate-800 rounded-xl">
                    <div className="flex items-center gap-3">
                      <button
                        type="button"
                        onClick={() => handleToggleSyndication(plat.platformId, active)}
                        className={`w-5 h-5 rounded flex items-center justify-center border transition-colors cursor-pointer ${
                          active ? 'bg-blue-600 border-blue-500 text-white' : 'border-slate-700 bg-slate-950 hover:border-slate-600'
                        }`}
                      >
                        {active && <Check size={12} />}
                      </button>
                      <div>
                        <span className="text-xs font-semibold text-slate-300 block">{plat.platformName}</span>
                        {syncDetails?.externalReferenceIndex && (
                          <span className="text-[10px] text-slate-500 font-mono">Ref: {syncDetails.externalReferenceIndex}</span>
                        )}
                      </div>
                    </div>

                    {active && syncDetails?.livePortalListingUrl ? (
                      <a 
                        href={syncDetails.livePortalListingUrl} 
                        target="_blank" 
                        rel="noreferrer"
                        className="inline-flex items-center gap-1 text-[10px] text-blue-400 hover:text-blue-300 font-medium transition-colors"
                      >
                        Portal View <ExternalLink size={10} />
                      </a>
                    ) : (
                      <span className="text-[10px] text-slate-600 italic">{active ? 'Syncing...' : 'Unlisted'}</span>
                    )}
                  </div>
                );
              })}
            </div>
          </div>

        </div>
      </div>
    </div>
  );
}
