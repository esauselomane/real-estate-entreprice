import { useState, useEffect } from 'react';
import { 
   ArrowLeft, MapPin, FileText, User, Home, CheckCircle2, 
   ExternalLink, Image, Plus, Trash2, Layers, 
   Building, Check, Loader2, AlertCircle, FileSignature, Wrench, FolderLock, Pencil,
   X, Save, Upload, Download
  } from 'lucide-react';
import { propertyService } from '../services/propertyService.js';
import { crmService } from '../services/crmService.js';
import { canManageUnits } from '../services/authService.js';
import { usePermission } from '../contexts/PermissionContext.jsx';
import Can from '../components/Can.jsx';

export default function PropertyDetails({ propertyId, onBack, onNavigateToClient }) {
  const { hasPermission } = usePermission();
  const [property, setProperty] = useState(null);
  const [subUnits, setSubUnits] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState('');
  const [platforms, setPlatforms] = useState([]);
  const [imagePreview, setImagePreview] = useState(null);
  const [isUploadingImage, setIsUploadingImage] = useState(false);
  const [actionSuccessMessage, setActionSuccessMessage] = useState('');
  const [isEditingProperty, setIsEditingProperty] = useState(false);
  const [propertyForm, setPropertyForm] = useState(null);
  // 🧩 PROPERTY-LEVEL OPERATIONS — operational records bound to this property (consolidated
  // from the standalone OperationsWorkspace so all property operations live on the property page).
  const [propertyOps, setPropertyOps] = useState([]);
  const [propertyOpsLoading, setPropertyOpsLoading] = useState(false);
  const [propertyOpsError, setPropertyOpsError] = useState('');

  // 🔗 UNIT WORKSPACE MODAL: editable details + leases/maintenance + unit document vault
  const OPS_TABS = [
    { id: 'leases', label: 'Leases', Icon: FileSignature, amount: 'Monthly rent', detailsPlaceholder: 'Term, escalation, deposit and notice details' },
    { id: 'maintenance', label: 'Maintenance', Icon: Wrench, amount: 'Estimated cost', detailsPlaceholder: 'Issue, access instructions, photos or resolution notes' }
  ];
  const [unitModalUnit, setUnitModalUnit] = useState(null);
  const [modalTab, setModalTab] = useState('details');
  const [unitOpsRecords, setUnitOpsRecords] = useState([]);
  const [unitOpsLoading, setUnitOpsLoading] = useState(false);
  const [unitOpsError, setUnitOpsError] = useState('');
  const [unitOpsSuccess, setUnitOpsSuccess] = useState('');
  const [unitOpsForm, setUnitOpsForm] = useState({ title: '', contactName: '', status: 'Open', dueDate: '', amount: '', details: '' });
  const [unitOpsEditing, setUnitOpsEditing] = useState(null);
  const activeOpsTab = OPS_TABS.find(t => t.id === (unitModalUnit ? modalTab : 'leases')) || OPS_TABS[0];

  // Editable unit details form (pre-filled when the modal opens on a unit)
  const [unitEditForm, setUnitEditForm] = useState({ unitNumber: '', unitDescription: '', netRentableAreaSqM: '', specificUnitRentalPrice: '', specificUnitSalePrice: '', rentalRate: '', rentEscalationPercentage: '', leaseTermMonths: '', isCurrentlyVacant: true });
  const [unitEditError, setUnitEditError] = useState('');
  const [unitEditSuccess, setUnitEditSuccess] = useState('');

  // Unit-level document vault (name + description + actual file saved to server disk)
  const [unitDocForm, setUnitDocForm] = useState({ name: '', description: '', file: null });
  const [unitDocUploading, setUnitDocUploading] = useState(false);
  const [unitDocError, setUnitDocError] = useState('');
  const [unitDocSuccess, setUnitDocSuccess] = useState('');

   // New Sub-Unit form state
   const [showAddUnitForm, setShowAddUnitForm] = useState(false);
   const [isSubmittingUnit, setIsSubmittingUnit] = useState(false);
   // 🛡️ Role-derived capability flags (mirror the backend Tenant/Owner read-only rule).
   // Tenants and Owners can view + edit their scoped units/docs but may never add or
   // delete units, documents, or maintenance calls — so those controls are hidden.
   const canManage = canManageUnits();
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

      // Load the property-level operations ledger (all modules bound to this property)
      try {
        setPropertyOpsLoading(true);
        const ops = await crmService.getOperationalRecordsForUnit('', propertyId, null);
        setPropertyOps(ops || []);
      } catch (err) {
        setPropertyOpsError(err || 'Could not load property operations.');
        setPropertyOps([]);
      } finally {
        setPropertyOpsLoading(false);
      }

      // Load platforms lookup
      try {
        const platList = await propertyService.getPlatforms();
        setPlatforms(platList || []);
      } catch (platformError) {
        setPlatforms([]);
        setErrorMessage(platformError || 'Failed to load syndication platforms.');
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

  // --- 🔗 UNIT WORKSPACE MODAL ---
  const openUnitModal = (unit) => {
    setUnitModalUnit(unit);
    setModalTab('details');
    setUnitOpsRecords([]);
    setUnitOpsForm({ title: '', contactName: '', status: 'Open', dueDate: '', amount: '', details: '' });
    setUnitOpsEditing(null);
    setUnitOpsError(''); setUnitOpsSuccess('');
    setUnitEditForm({
      unitNumber: unit.unitNumber || '',
      unitDescription: unit.unitDescription || '',
      netRentableAreaSqM: unit.netRentableAreaSqM || '',
      specificUnitRentalPrice: unit.specificUnitRentalPrice ?? '',
      specificUnitSalePrice: unit.specificUnitSalePrice ?? '',
      rentalRate: unit.rentalRate ?? '',
      rentEscalationPercentage: unit.rentEscalationPercentage ?? '',
      leaseTermMonths: unit.leaseTermMonths ?? '',
      isCurrentlyVacant: !!unit.isCurrentlyVacant
    });
    setUnitEditError(''); setUnitEditSuccess('');
    setUnitDocForm({ name: '', description: '', file: null });
    setUnitDocError(''); setUnitDocSuccess('');
  };

  const closeUnitModal = () => setUnitModalUnit(null);

  // Load leases/maintenance operational records when the modal is open on those tabs
  useEffect(() => {
    const loadModalOps = async () => {
      if (!unitModalUnit || !OPS_TABS.some(t => t.id === modalTab)) return;
      setUnitOpsLoading(true);
      setUnitOpsError('');
      try {
        const recs = await crmService.getOperationalRecordsForUnit(
          modalTab,
          property.propertyId,
          unitModalUnit.propertyUnitId
        );
        setUnitOpsRecords(recs || []);
      } catch (err) {
        setUnitOpsError(err || 'Could not load unit records.');
      } finally {
        setUnitOpsLoading(false);
      }
    };
    loadModalOps();
  }, [unitModalUnit, modalTab]);

  const resetUnitOpForm = () => {
    setUnitOpsForm({ title: '', contactName: '', status: 'Open', dueDate: '', amount: '', details: '' });
    setUnitOpsEditing(null);
  };

  const saveUnitOp = async (event) => {
    event.preventDefault();
    if (!unitModalUnit || !unitOpsForm.title.trim()) {
      setUnitOpsError('A title is required.');
      return;
    }
    setUnitOpsError(''); setUnitOpsSuccess('');
    const payload = {
      tenantId: property.tenantId,
      module: modalTab,
      title: unitOpsForm.title,
      propertyReference: `${property.completeAddress} · Unit ${unitModalUnit.unitNumber}`,
      contactName: unitOpsForm.contactName,
      status: unitOpsForm.status,
      dueDate: unitOpsForm.dueDate || null,
      amount: Number(unitOpsForm.amount || 0),
      details: unitOpsForm.details,
      propertyId: property.propertyId,
      propertyUnitId: unitModalUnit.propertyUnitId
    };
    try {
      if (unitOpsEditing) {
        await crmService.updateOperationalRecord(unitOpsEditing.operationalRecordId, { ...unitOpsEditing, ...payload });
      } else {
        await crmService.createOperationalRecord(payload);
      }
      setUnitOpsSuccess('Record saved.');
      setTimeout(() => setUnitOpsSuccess(''), 2000);
      resetUnitOpForm();
      const recs = await crmService.getOperationalRecordsForUnit(
        modalTab, property.propertyId, unitModalUnit.propertyUnitId
      );
      setUnitOpsRecords(recs || []);
    } catch (err) {
      setUnitOpsError(err || 'Could not save this record.');
    }
  };

  const editUnitOp = (record) => {
    setUnitOpsEditing(record);
    setUnitOpsForm({
      title: record.title || '',
      contactName: record.contactName || '',
      status: record.status || 'Open',
      dueDate: record.dueDate ? new Date(record.dueDate).toISOString().slice(0, 10) : '',
      amount: record.amount || '',
      details: record.details || ''
    });
    setUnitOpsError(''); setUnitOpsSuccess('');
  };

  // --- EDITABLE UNIT DETAILS ---
  const saveUnitEdit = async (event) => {
    event.preventDefault();
    if (!unitModalUnit || !unitEditForm.unitNumber.trim()) {
      setUnitEditError('Unit number is required.');
      return;
    }
    setUnitEditError(''); setUnitEditSuccess('');
    try {
      const saved = await propertyService.updatePropertyUnit(property.propertyId, unitModalUnit.propertyUnitId, {
        unitNumber: unitEditForm.unitNumber.trim(),
        unitDescription: unitEditForm.unitDescription,
        netRentableAreaSqM: Number(unitEditForm.netRentableAreaSqM || 0),
        specificUnitRentalPrice: unitEditForm.specificUnitRentalPrice === '' ? null : Number(unitEditForm.specificUnitRentalPrice),
        specificUnitSalePrice: unitEditForm.specificUnitSalePrice === '' ? null : Number(unitEditForm.specificUnitSalePrice),
        rentalRate: unitEditForm.rentalRate === '' ? null : Number(unitEditForm.rentalRate),
        rentEscalationPercentage: unitEditForm.rentEscalationPercentage === '' ? null : Number(unitEditForm.rentEscalationPercentage),
        leaseTermMonths: unitEditForm.leaseTermMonths === '' ? null : Number(unitEditForm.leaseTermMonths),
        isCurrentlyVacant: unitEditForm.isCurrentlyVacant
      });
      // Reflect edits into the open modal + the master property list
      setUnitModalUnit(saved);
      setSubUnits(prev => prev.map(u => u.propertyUnitId === saved.propertyUnitId ? saved : u));
      setUnitEditSuccess('Unit details updated.');
      setTimeout(() => setUnitEditSuccess(''), 2000);
    } catch (err) {
      setUnitEditError(err || 'Could not update unit details.');
    }
  };

  // --- UNIT DOCUMENT VAULT (name + description + actual file saved to server disk) ---
  const parseUnitDocs = (unit = unitModalUnit) => {
    if (!unit || !unit.unitDocumentsJson) return [];
    try { return JSON.parse(unit.unitDocumentsJson) || []; }
    catch { return []; }
  };
  const [unitDocs, setUnitDocs] = useState([]);

  // Refresh the unit document list from the role-scoped list endpoint whenever the
  // unit modal is (re)opened. Falls back to the embedded field if the list call is blocked.
  const loadUnitDocs = async () => {
    if (!unitModalUnit) { setUnitDocs([]); return; }
    try {
      const docs = await propertyService.getUnitDocuments(property.propertyId, unitModalUnit.propertyUnitId);
      setUnitDocs(docs && docs.length ? docs : parseUnitDocs(unitModalUnit));
    } catch {
      setUnitDocs(parseUnitDocs(unitModalUnit));
    }
  };

  useEffect(() => { loadUnitDocs(); }, [unitModalUnit]);

  const resetDocForm = () => setUnitDocForm({ name: '', description: '', file: null });

  const uploadUnitDocument = async (event) => {
    event.preventDefault();
    if (!unitModalUnit || !unitDocForm.file || !unitDocForm.name.trim()) {
      setUnitDocError('A document name and file are required.');
      return;
    }
    setUnitDocUploading(true); setUnitDocError(''); setUnitDocSuccess('');
    const base64 = await new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result.split(',')[1]);
      reader.onerror = reject;
      reader.readAsDataURL(unitDocForm.file);
    });
      try {
        await propertyService.uploadPropertyUnitDocument(property.propertyId, unitModalUnit.propertyUnitId, unitDocForm.file, {
          name: unitDocForm.name.trim(),
          description: unitDocForm.description.trim()
        });
        // Refresh the unit document list (server persists UnitDocumentsJson)
        await loadUnitDocs();
        setUnitDocSuccess('Document uploaded to server vault.');
        setTimeout(() => setUnitDocSuccess(''), 2000);
        resetDocForm();
      } catch (err) {
      setUnitDocError(err || 'Could not upload this document.');
    } finally {
      setUnitDocUploading(false);
    }
  };

  const deleteUnitDocument = async (doc) => {
    if (!window.confirm(`Are you sure you want to delete "${doc.name}" from this unit's document vault?`)) return;
    try {
      await propertyService.deletePropertyUnitDocument(property.propertyId, unitModalUnit.propertyUnitId, doc.storedName);
      await loadUnitDocs();
      setUnitDocSuccess('Document removed.');
      setTimeout(() => setUnitDocSuccess(''), 1500);
    } catch (err) {
      setUnitDocError(err || 'Could not remove this document.');
    }
  };

  const downloadUnitDocument = async (doc) => {
    try {
      const blob = await propertyService.downloadPropertyUnitDocument(property.propertyId, unitModalUnit.propertyUnitId, doc.storedName, doc.fileName);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url; a.download = doc.fileName;
      a.click(); URL.revokeObjectURL(url);
    } catch (err) {
      alert(`Could not download document: ${err}`);
    }
  };

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

  const handleSaveProperty = async (event) => {
    event.preventDefault();
    try {
      const saved = await propertyService.updateProperty(propertyId, {
        ...propertyForm,
        propertyTypeId: Number(propertyForm.propertyTypeId || property.propertyTypeId),
        valuationPrice: Number(propertyForm.valuationPrice || 0),
        salePrice: propertyForm.salePrice === '' ? null : Number(propertyForm.salePrice),
        tenantInstallationAllowance: Number(propertyForm.tenantInstallationAllowance || 0),
        latitude: Number(propertyForm.latitude || 0), longitude: Number(propertyForm.longitude || 0)
      });
      setProperty(saved); setSubUnits(saved.subUnits || subUnits); setIsEditingProperty(false);
      setActionSuccessMessage('Property details saved to the backend.');
    } catch (err) { setErrorMessage(err || 'Could not save property details.'); }
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

            <Can permission="properties:edit">
              <button type="button" onClick={() => { setPropertyForm({ ...property }); setIsEditingProperty(true); }} className="mb-4 text-xs font-bold text-blue-300 border border-blue-900 rounded-lg px-3 py-1.5 hover:bg-blue-950">Edit property details</button>
            </Can>

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
            </div>

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

          {/* 🧩 PROPERTY-LEVEL OPERATIONS LEDGER (consolidated from the property operations workspace) */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
            <div className="flex items-center gap-2">
              <Wrench size={18} className="text-violet-500" />
              <h4 className="text-sm font-bold text-white uppercase tracking-wider">Property Operations ({propertyOps.length})</h4>
            </div>
            {propertyOpsLoading ? (
              <div className="text-xs text-slate-500">Loading property operations ledger...</div>
            ) : propertyOpsError ? (
              <div className="text-xs text-red-400">{propertyOpsError}</div>
            ) : propertyOps.length === 0 ? (
              <p className="text-xs text-slate-500">No operational records are currently bound to this property.</p>
            ) : (
              <div className="space-y-3">
                {propertyOps.map((r) => (
                  <div key={r.operationalRecordId} className="p-3 bg-slate-950/60 border border-slate-800 rounded-xl flex justify-between gap-4">
                    <div className="space-y-1">
                      <div className="flex items-center gap-2"><span className="text-xs font-bold text-white">{r.title}</span><span className="text-[10px] px-2 py-0.5 rounded bg-blue-950 text-blue-300">{r.module}</span></div>
                      <p className="text-xs text-slate-400">{r.propertyReference || 'No property reference'} · {r.contactName || 'No contact'}</p>
                      <p className="text-xs text-slate-500 whitespace-pre-wrap">{r.details}</p>
                    </div>
                    <div className="text-right text-xs text-slate-400">
                      <span className="px-2 py-0.5 rounded bg-slate-800 text-slate-300">{r.status}</span>
                      {r.amount > 0 && <span className="text-emerald-400 block mt-1">R {Number(r.amount).toLocaleString()}</span>}
                      {r.dueDate && <span className="text-slate-500 block mt-1">Due {new Date(r.dueDate).toLocaleDateString()}</span>}
                    </div>
                  </div>
                ))}
              </div>
            )}
            <p className="text-[10px] text-slate-500">Records are managed from the unit workspace (maintenance / lease tabs). All entries are tenant-scoped to this property.</p>
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
                 {canManage && (
                   <button
                     type="button"
                     onClick={() => setShowAddUnitForm(!showAddUnitForm)}
                     className="inline-flex items-center gap-1.5 text-xs font-semibold text-white bg-blue-600 hover:bg-blue-500 px-3 py-1.5 rounded-lg transition-colors cursor-pointer shadow-sm"
                   >
                     <Plus size={14} /> {showAddUnitForm ? 'Cancel' : 'Link New Unit'}
                   </button>
                 )}
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
                      placeholder="e.g. Corner office suite" 
                      value={newUnit.unitDescription}
                      onChange={e => setNewUnit({ ...newUnit, unitDescription: e.target.value })}
                      className="w-full px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500"
                    />
                  </div>
                  <div>
                    <Can permission="units:create">
                      <button
                        type="submit"
                        disabled={isSubmittingUnit}
                        className="w-full inline-flex items-center justify-center gap-1.5 text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-500 py-2 rounded-lg transition-colors cursor-pointer disabled:opacity-50"
                      >
                        {isSubmittingUnit ? <Loader2 size={14} className="animate-spin" /> : <Plus size={14} />} Save & Link
                      </button>
                    </Can>
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
                          onClick={() => openUnitModal(unit)}
                          title="Open unit workspace (details / leases / maintenance / documents)"
                          className="text-slate-500 hover:text-blue-400 p-1.5 rounded-lg hover:bg-slate-800 transition-colors cursor-pointer"
                        >
                          <Wrench size={14} />
                        </button>
                        {canManage && (
                          <Can permission="units:delete">
                            <button 
                              type="button"
                              onClick={() => handleDeleteSubUnit(unit.propertyUnitId, unit.unitNumber)}
                              title="Remove Sub-unit"
                              className="text-slate-500 hover:text-red-400 p-1.5 rounded-lg hover:bg-slate-800 transition-colors cursor-pointer"
                            >
                              <Trash2 size={14} />
                            </button>
                          </Can>
                        )}
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>

          {/* 🧩 UNIT WORKSPACE MODAL: editable details + leases/maintenance + document vault */}
          {unitModalUnit && (
            <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4">
              <div className="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-5xl max-h-[90vh] flex flex-col shadow-xl">
                <header className="flex items-center justify-between p-4 border-b border-slate-800">
                  <h3 className="text-sm font-bold text-white uppercase">Unit {unitModalUnit.unitNumber} — Property #{property.propertyId}</h3>
                  <button type="button" onClick={closeUnitModal} className="p-1.5 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800"><X size={16} /></button>
                </header>

                <nav className="flex items-end gap-1 px-4 border-b border-slate-800 bg-slate-950/40">
                  <button type="button" onClick={() => setModalTab('details')} className={`px-3 py-2 text-xs font-bold ${modalTab === 'details' ? 'text-blue-300 border-b-2 border-blue-500' : 'text-slate-400'}`}>Details</button>
                  {OPS_TABS.map((tab) => { const Icon = tab.Icon; return (
                    <button key={tab.id} type="button" onClick={() => setModalTab(tab.id)} className={`flex items-center gap-1 px-3 py-2 text-xs font-bold ${modalTab === tab.id ? 'text-blue-300 border-b-2 border-blue-500' : 'text-slate-400'}`}>
                      <Icon size={13} />{tab.label}
                    </button>
                  ); })}
                  <button type="button" onClick={() => setModalTab('documents')} className={`flex items-center gap-1 px-3 py-2 text-xs font-bold ${modalTab === 'documents' ? 'text-blue-300 border-b-2 border-blue-500' : 'text-slate-400'}`}>
                    <FolderLock size={13} />Document Vault
                  </button>
                </nav>

                <div className="overflow-y-auto p-4 space-y-4">
                  {/* ✏️ UNIT DETAILS (editable) */}
                  {modalTab === 'details' && (
                    <form onSubmit={saveUnitEdit} className="grid sm:grid-cols-2 gap-4">
                      {unitEditError && <div className="p-3 rounded-xl text-xs bg-red-950/40 border border-red-900 text-red-300 sm:col-span-2">{unitEditError}</div>}
                      {unitEditSuccess && <div className="p-3 rounded-xl text-xs bg-emerald-950/40 border border-emerald-900 text-emerald-300 sm:col-span-2">{unitEditSuccess}</div>}
                      <label className="label">Unit number<input required value={unitEditForm.unitNumber} onChange={e => setUnitEditForm({ ...unitEditForm, unitNumber: e.target.value })} className="field" /></label>
                      <label className="label">NRA (m²)<input type="number" min="0" value={unitEditForm.netRentableAreaSqM} onChange={e => setUnitEditForm({ ...unitEditForm, netRentableAreaSqM: e.target.value })} className="field" /></label>
                      <label className="label sm:col-span-2">Unit description<textarea rows={2} value={unitEditForm.unitDescription} onChange={e => setUnitEditForm({ ...unitEditForm, unitDescription: e.target.value })} className="field" placeholder="e.g. Ground floor retail shell" /></label>
                      <label className="label">Rental price<input type="number" min="0" value={unitEditForm.specificUnitRentalPrice ?? ''} onChange={e => setUnitEditForm({ ...unitEditForm, specificUnitRentalPrice: e.target.value || '' })} className="field" /></label>
                      <label className="label">Sale price<input type="number" min="0" value={unitEditForm.specificUnitSalePrice ?? ''} onChange={e => setUnitEditForm({ ...unitEditForm, specificUnitSalePrice: e.target.value || '' })} className="field" /></label>
                      <label className="label">Rental rate (R/mo)<input type="number" min="0" value={unitEditForm.rentalRate ?? ''} onChange={e => setUnitEditForm({ ...unitEditForm, rentalRate: e.target.value || '' })} className="field" /></label>
                      <label className="label">Escalation %<input type="number" min="0" step="0.01" value={unitEditForm.rentEscalationPercentage ?? ''} onChange={e => setUnitEditForm({ ...unitEditForm, rentEscalationPercentage: e.target.value || '' })} className="field" /></label>
                      <label className="label">Lease term (months)<input type="number" min="0" value={unitEditForm.leaseTermMonths ?? ''} onChange={e => setUnitEditForm({ ...unitEditForm, leaseTermMonths: e.target.value || '' })} className="field" /></label>
                      <label className="flex items-center gap-2 text-xs mt-1"><input type="checkbox" checked={unitEditForm.isCurrentlyVacant} onChange={e => setUnitEditForm({ ...unitEditForm, isCurrentlyVacant: e.target.checked })} /><span className="text-slate-300">Currently vacant</span></label>
                      <div className="sm:col-span-2">
                        <Can permission="units:edit">
                          <button type="submit" className="inline-flex items-center gap-1.5 px-4 py-2 bg-blue-600 text-white rounded-xl text-xs font-bold"><Save size={13} />Save unit details</button>
                        </Can>
                      </div>
                    </form>
                  )}

                  {/* 📋 LEASES / MAINTENANCE (CRM records scoped to this unit; property/unit auto-bound) */}
                  {(modalTab === 'leases' || modalTab === 'maintenance') && (
                    <div className="space-y-4">
                      {unitOpsError && <div className="p-3 rounded-xl text-xs bg-red-950/40 border border-red-900 text-red-300">{unitOpsError}</div>}
                      {unitOpsSuccess && <div className="p-3 rounded-xl text-xs bg-emerald-950/40 border border-emerald-900 text-emerald-300">{unitOpsSuccess}</div>}
                      <div className="grid xl:grid-cols-[320px_1fr] gap-6">
                        <form onSubmit={saveUnitOp} className="space-y-3">
                          <h5 className="text-xs font-bold text-blue-400 uppercase">
                            {unitOpsEditing ? `Edit ${activeOpsTab.label}` : `Add ${activeOpsTab.label}`}
                          </h5>
                          <label className="label">Title<input required value={unitOpsForm.title} onChange={e => setUnitOpsForm({ ...unitOpsForm, title: e.target.value })} className="field" placeholder={`${activeOpsTab.label.slice(0, -1)} title`} /></label>
                          <label className="label">Contact<input value={unitOpsForm.contactName} onChange={e => setUnitOpsForm({ ...unitOpsForm, contactName: e.target.value })} className="field" placeholder="Contractor, tenant, vendor..." /></label>
                          <div className="grid grid-cols-2 gap-3">
                            <label className="label">Status<select value={unitOpsForm.status} onChange={e => setUnitOpsForm({ ...unitOpsForm, status: e.target.value })} className="field"><option>Open</option><option>In Progress</option><option>Awaiting Information</option><option>Completed</option><option>Cancelled</option></select></label>
                            <label className="label">Due date<input type="date" value={unitOpsForm.dueDate || ''} onChange={e => setUnitOpsForm({ ...unitOpsForm, dueDate: e.target.value })} className="field" /></label>
                          </div>
                          <label className="label">{activeOpsTab.amount}<input type="number" min="0" value={unitOpsForm.amount ?? ''} onChange={e => setUnitOpsForm({ ...unitOpsForm, amount: e.target.value })} className="field" /></label>
                          <label className="label">Details<textarea rows={4} value={unitOpsForm.details} onChange={e => setUnitOpsForm({ ...unitOpsForm, details: e.target.value })} className="field" placeholder={activeOpsTab.detailsPlaceholder} /></label>
                           <div className="flex gap-3">
                             <button type="button" onClick={resetUnitOpForm} className="flex-1 py-2 border border-slate-700 rounded-xl text-xs">Clear</button>
                             <Can permission="units:edit">
                               <button className="flex-1 py-2 bg-blue-600 text-white rounded-xl text-xs font-bold">{unitOpsEditing ? 'Save changes' : 'Save record'}</button>
                             </Can>
                           </div>
                        </form>

                        <div>
                          {unitOpsLoading ? (
                            <div className="flex items-center gap-2 text-xs text-slate-400"><Loader2 className="animate-spin" size={14} />Loading {activeOpsTab.label.toLowerCase()}…</div>
                          ) : (
                            <div className="space-y-3">
                              {unitOpsRecords.map((r) => (
                                <div key={r.operationalRecordId} className="p-3.5 bg-slate-950/60 border border-slate-800 rounded-xl flex justify-between gap-4">
                                  <div className="space-y-0.5">
                                    <div className="flex items-center gap-2"><p className="text-xs font-bold text-white">{r.title}</p><span className="text-[10px] px-1.5 py-0.5 rounded bg-slate-800 text-slate-300">{r.status}</span></div>
                                    <p className="text-[10px] text-slate-400">{r.contactName || 'No contact'}{r.dueDate ? ` · Due ${new Date(r.dueDate).toLocaleDateString()}` : ''}</p>
                                    {r.details && <p className="text-[10px] text-slate-500 whitespace-pre-wrap">{r.details}</p>}
                                    {Number(r.amount || 0) !== 0 && <p className="text-[10px] text-emerald-400">R {Number(r.amount).toLocaleString()}</p>}
                                  </div>
                                   <Can permission="units:edit">
                                     <button type="button" onClick={() => editUnitOp(r)} className="h-fit p-1.5 text-slate-400 hover:text-white"><Pencil size={13} /></button>
                                   </Can>
                                </div>
                              ))}
                              {!unitOpsRecords.length && <p className="text-[10px] text-slate-500">No {activeOpsTab.label.toLowerCase()} records for this unit yet.</p>}
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  )}

                  {/* 📎 DOCUMENT VAULT (name + description + actual file saved to server disk) */}
                  {modalTab === 'documents' && (
                    <div className="space-y-4">
                      {unitDocError && <div className="p-3 rounded-xl text-xs bg-red-950/40 border border-red-900 text-red-300">{unitDocError}</div>}
                      {unitDocSuccess && <div className="p-3 rounded-xl text-xs bg-emerald-950/40 border border-emerald-900 text-emerald-300">{unitDocSuccess}</div>}
                      <form onSubmit={uploadUnitDocument} className="grid sm:grid-cols-2 gap-3 items-end">
                        <label className="label">Document name<input required value={unitDocForm.name} onChange={e => setUnitDocForm({ ...unitDocForm, name: e.target.value })} className="field" placeholder="e.g. Maintenance Report Q3" /></label>
                        <label className="label">Description<textarea rows={2} value={unitDocForm.description} onChange={e => setUnitDocForm({ ...unitDocForm, description: e.target.value })} className="field" placeholder="What is this document?" /></label>
                        <label className="label sm:col-span-2">Actual document
                          <input required type="file" onChange={e => setUnitDocForm({ ...unitDocForm, file: e.target.files?.[0] || null })} className="text-xs text-slate-400 file:mr-3 file:py-2 file:px-4 file:rounded-xl file:border-0 file:text-xs file:font-bold file:bg-blue-600 file:text-white hover:file:bg-blue-700" />
                          <span className="text-[10px] text-slate-500 block mt-1">{unitDocForm.file?.name || 'No file chosen'}</span>
                        </label>
                         <div className="sm:col-span-2">
                           <Can permission="units:edit">
                             <button type="submit" disabled={unitDocUploading} className="inline-flex items-center gap-1.5 px-4 py-2 bg-blue-600 disabled:opacity-60 text-white rounded-xl text-xs font-bold"><Upload size={13} />{unitDocUploading ? 'Uploading…' : 'Save to vault'}</button>
                           </Can>
                         </div>
                      </form>
                      <div className="border-t border-slate-800 pt-3 space-y-2">
                        <h6 className="text-[10px] font-bold uppercase text-slate-400">Stored documents</h6>
                        {unitDocs.length === 0
                          ? <p className="text-[10px] text-slate-500">No documents linked to this unit yet.</p>
                          : <div className="space-y-2">{unitDocs.map((d) => (
                              <div key={d.storedName} className="p-3 bg-slate-950/60 border border-slate-800 rounded-xl flex items-center justify-between gap-3">
                                <div className="min-w-0">
                                  <p className="text-xs font-bold text-white truncate">{d.name || d.fileName}</p>
                                  {d.description && <p className="text-[10px] text-slate-400 truncate">{d.description}</p>}
                                  <p className="text-[10px] text-slate-500">{d.contentType || 'file'} · {(d.sizeBytes / 1024).toFixed(1)} KB · {new Date(d.uploadedAt).toLocaleDateString()}</p>
                                </div>
                                 <div className="flex items-center gap-1.5">
                                   <button type="button" onClick={() => downloadUnitDocument(d)} title="Download" className="p-1.5 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800"><Download size={13} /></button>
                                   {canManage && (
                                     <Can permission="units:delete">
                                       <button type="button" onClick={() => deleteUnitDocument(d)} title="Delete document" className="p-1.5 text-slate-400 hover:text-red-400 rounded-lg hover:bg-slate-800"><Trash2 size={13} /></button>
                                     </Can>
                                   )}
                                 </div>
                              </div>
                            ))}</div>}
                      </div>
                    </div>
                  )}
                </div>
              </div>
            </div>
          )}

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
                <span className="text-sm font-bold text-white mt-0.5 block">{property.tenantId}</span>
              </div>

            </div>
          </div>

          {/* Verification Legal File Attachments */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
            <h4 className="text-sm font-bold text-white uppercase tracking-wider mb-4 flex items-center gap-2">
              <FileText size={16} className="text-blue-500" /> Deeds & Verification Vault
            </h4>
            <div className="space-y-2">
              {(JSON.parse(property.verificationDocumentsJson || '[]')).map((doc, idx) => (
                <div key={idx} className="flex items-center gap-3 p-2.5 bg-slate-950 border border-slate-800 rounded-xl text-xs text-slate-300 group hover:border-slate-700 transition-colors">
                  <FileText size={14} className="text-slate-500 group-hover:text-blue-400" />
                  <span className="truncate flex-1 font-medium">{doc}</span>
                </div>
              ))}
              {!JSON.parse(property.verificationDocumentsJson || '[]').length && <p className="text-xs text-slate-500">No verification documents are stored for this property.</p>}
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
                      <Can permission="properties:edit">
                        <button
                          type="button"
                          onClick={() => handleToggleSyndication(plat.platformId, active)}
                          className={`w-5 h-5 rounded flex items-center justify-center border transition-colors cursor-pointer ${
                            active ? 'bg-blue-600 border-blue-500 text-white' : 'border-slate-700 bg-slate-950 hover:border-slate-600'
                          }`}
                        >
                          {active && <Check size={12} />}
                        </button>
                      </Can>
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
      {isEditingProperty && propertyForm && (
        <div className="fixed inset-0 z-50 p-4 bg-black/70 overflow-y-auto">
          <form onSubmit={handleSaveProperty} className="max-w-3xl mx-auto my-8 bg-slate-900 border border-slate-700 rounded-2xl p-6 space-y-4">
            <h3 className="text-white font-bold">Edit property details</h3>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              {[
                ['completeAddress', 'Address'], ['erfSpatialSize', 'Erf size'], ['titleDeedType', 'Title deed type'], ['portfolioZoning', 'Zoning'], ['linkedOwnerId', 'Owner record ID'], ['valuationPrice', 'Valuation'], ['salePrice', 'Sale price'], ['tenantInstallationAllowance', 'TI allowance'], ['latitude', 'Latitude'], ['longitude', 'Longitude']
              ].map(([key, label]) => <label key={key} className="text-[10px] text-slate-400 uppercase">{label}<input value={propertyForm[key] ?? ''} onChange={e => setPropertyForm({ ...propertyForm, [key]: e.target.value })} className="mt-1 w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-lg text-sm text-white normal-case" /></label>)}
              <label className="text-[10px] text-slate-400 uppercase">Market class<select value={propertyForm.marketClass || 'Sale'} onChange={e => setPropertyForm({ ...propertyForm, marketClass: e.target.value })} className="mt-1 w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-lg text-sm text-white normal-case"><option>Sale</option><option>Rental</option></select></label>
            </div>
             <div className="flex gap-3"><button type="button" onClick={() => setIsEditingProperty(false)} className="flex-1 py-2 border border-slate-700 rounded-xl text-xs">Cancel</button><Can permission="properties:edit"><button type="submit" className="flex-1 py-2 bg-blue-600 text-white rounded-xl text-xs font-bold">Save property</button></Can></div>
          </form>
        </div>
      )}
    </div>
  );
}
