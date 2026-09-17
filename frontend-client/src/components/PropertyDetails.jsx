import GoogleAddressPicker from './maps/GoogleAddressPicker.jsx';
import PropertyStaticMap from './maps/PropertyStaticMap.jsx';
import { useState, useEffect } from 'react';
import {
   ArrowLeft, MapPin, FileText, User, Home, CheckCircle2,
   ExternalLink, Image, Plus, Trash2, Layers,
    Building, Check, Loader2, AlertCircle, FileSignature, Wrench, FolderLock, Pencil,
    X, Save, Upload, Download, History, ChevronDown, ChevronUp, Calendar,
    ParkingMeter, Ship, Wind, Heart, Wifi, Zap, Shield, FileUp, Eye, EyeOff
  } from 'lucide-react';
import { propertyService } from '../services/propertyService.js';
import { crmService } from '../services/crmService.js';
import { canManageUnits } from '../services/authService.js';
import { usePermission } from '../contexts/PermissionContext.jsx';
import Can from '../components/Can.jsx';
import ContactPicker from '../components/ContactPicker.jsx';

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

  // 🏠/🏢 AMENITIES
  const [amenitiesData, setAmenitiesData] = useState(null);
  const [amenitiesLoading, setAmenitiesLoading] = useState(false);
  const [amenitySaving, setAmenitySaving] = useState(false);
  const [amenityError, setAmenityError] = useState('');
  const [amenityForm, setAmenityForm] = useState({}); // { amenityTypeId, scope:'property'|'unit', unitId?, booleanValue, numberValue, notes }
  const [amenityEditing, setAmenityEditing] = useState(null); // existing row being edited
  const [amenityTypes, setAmenityTypes] = useState([]); // catalogue for the add-form dropdown

  const handleSaveAmenity = async (e) => {
    e.preventDefault();
    if (!amenityForm.amenityTypeId) return;
    setAmenitySaving(true);
    try {
      const unitId = amenityForm.scope === 'unit' ? amenityForm.unitId : null;
      const payload = {
        amenityTypeId: Number(amenityForm.amenityTypeId),
        booleanValue: amenityForm.booleanValue,
        numberValue: amenityForm.numberValue,
        notes: amenityForm.notes,
      };
      await propertyService.upsertAmenity(property.propertyId, payload, unitId);
      const resp = await propertyService.getAmenities(propertyId);
      setAmenitiesData(resp);
      setAmenityForm({});
      setAmenityEditing(null);
      setAmenityError('');
    } catch (err) {
      setAmenityError((err && err.message) || 'Failed to save amenity.');
    } finally {
      setAmenitySaving(false);
    }
  };

  // Load the amenity catalogue once when the property has loaded (admins get
  // the active types; non-admins just see the types already attached).
  useEffect(() => {
    if (!property) return;
    let cancelled = false;
    (async () => {
      try {
        const list = await propertyService.getAmenityTypes();
        if (!cancelled) setAmenityTypes(Array.isArray(list) ? list : []);
      } catch {
        if (!cancelled) setAmenityTypes([]);
      }
    })();
    return () => { cancelled = true; };
  }, [property]);

  // 🔗 UNIT WORKSPACE MODAL: editable details + lease/history + maintenance + document vault
  // The legacy OperationalRecord-backed "Leases" tab has been replaced by a real
  // UnitLeases-backed flow: a Lease tab (current editable lease) and a History
  // tab (read-only list of every past lease for this unit).
  const OPS_TABS = [
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
  const activeOpsTab = OPS_TABS.find(t => t.id === (unitModalUnit ? modalTab : 'maintenance')) || OPS_TABS[0];

  // 📜 Lease tab state — current editable lease + the full history list.
  const [unitLeases, setUnitLeases] = useState([]);
  const [unitLeasesLoading, setUnitLeasesLoading] = useState(false);
  const [unitLeasesError, setUnitLeasesError] = useState('');
  const [unitLeasesSuccess, setUnitLeasesSuccess] = useState('');
  const [leaseForm, setLeaseForm] = useState({
    contactId: null,
    contactDisplay: null, // full contact object (for ContactPicker)
    tenantName: '',       // snapshot of full name from picked contact
    leaseStart: '',
    leaseEnd: '',
    monthlyRent: '',
    depositAmount: '',
    accruedInterestPercent: '',
    notes: ''
  });
  const [leaseEditingId, setLeaseEditingId] = useState(null);
  // 🕒 History drill-down: when set, render the read-only lease detail + maintenance
  // summary instead of the history list. Cleared by the Back button.
  const [historyDetail, setHistoryDetail] = useState(null);
  const [historyMaintCalls, setHistoryMaintCalls] = useState([]);
  const [historyMaintOpen, setHistoryMaintOpen] = useState(false);
  const [historyLoading, setHistoryLoading] = useState(false);

  // Editable unit details form (pre-filled when the modal opens on a unit).
  // `leaseTermMonths` was removed — lease dates are now managed on the Lease tab.
  const [unitEditForm, setUnitEditForm] = useState({ unitNumber: '', unitDescription: '', netRentableAreaSqM: '', specificUnitRentalPrice: '', specificUnitSalePrice: '', rentalRate: '', rentEscalationPercentage: '', isCurrentlyVacant: true });
  const [unitEditError, setUnitEditError] = useState('');
  const [unitEditSuccess, setUnitEditSuccess] = useState('');

  // Unit-level document vault (name + description + actual file saved to server disk)
  const [unitDocForm, setUnitDocForm] = useState({ name: '', description: '', file: null });
  const [unitDocUploading, setUnitDocUploading] = useState(false);
  const [unitDocError, setUnitDocError] = useState('');
  const [unitDocSuccess, setUnitDocSuccess] = useState('');

  // 📜 SOLE MANDATE EDITING (Task 7) — expose the Sole Mandate toggle / field
  // in a highly visible "Listing Details" editing panel in the right sidebar so
  // users can open / close / renew the mandate without hunting through menus.
  const [mandateForm, setMandateForm] = useState({ startDate: '', endDate: '' });
  const [mandateError, setMandateError] = useState('');
  const [mandateSuccess, setMandateSuccess] = useState('');
  const [mandateBusy, setMandateBusy] = useState(false);
  const [showMandatePanel, setShowMandatePanel] = useState(false);

  const loadMandates = async () => {
    try {
      const list = await propertyService.listSoleMandates(property.propertyId);
      return Array.isArray(list) ? list : [];
    } catch {
      return [];
    }
  };

  const openSoleMandate = async (event) => {
    event.preventDefault();
    if (!mandateForm.startDate || !mandateForm.endDate) {
      setMandateError('Start and end dates are required.');
      return;
    }
    if (new Date(mandateForm.endDate) <= new Date(mandateForm.startDate)) {
      setMandateError('End date must be after the start date.');
      return;
    }
    setMandateBusy(true);
    setMandateError('');
    setMandateSuccess('');
    try {
      await propertyService.createSoleMandate(property.propertyId, {
        startDate: new Date(mandateForm.startDate).toISOString(),
        endDate: new Date(mandateForm.endDate).toISOString()
      });
      setMandateForm({ startDate: '', endDate: '' });
      setMandateSuccess('Sole mandate opened.');
      setTimeout(() => setMandateSuccess(''), 2500);
      // Refresh the property row so the active-mandate banner updates.
      const updated = await propertyService.getPropertyById(propertyId);
      setProperty(updated);
    } catch (err) {
      setMandateError((err && err.message) || 'Could not open sole mandate.');
    } finally {
      setMandateBusy(false);
    }
  };

  const closeSoleMandate = async () => {
    const mandates = await loadMandates();
    const active = mandates.find(m => m.isActive);
    if (!active) {
      setMandateError('No active sole mandate to close.');
      return;
    }
    if (!window.confirm('Close the active sole mandate? The property will no longer be under exclusive mandate.')) return;
    setMandateBusy(true);
    setMandateError('');
    setMandateSuccess('');
    try {
      await propertyService.closeSoleMandate(property.propertyId, active.mandateId);
      setMandateSuccess('Sole mandate closed.');
      setTimeout(() => setMandateSuccess(''), 2500);
      const updated = await propertyService.getPropertyById(propertyId);
      setProperty(updated);
    } catch (err) {
      setMandateError((err && err.message) || 'Could not close sole mandate.');
    } finally {
      setMandateBusy(false);
    }
  };

  // 📄 PROPERTY-LEVEL DEEDS & VERIFICATION VAULT — upload / delete verification
  // documents directly on the property page (Task 1). Mirrors the unit document
  // vault pattern so the UI is consistent.
  const [verificationDocForm, setVerificationDocForm] = useState({
    name: '',
    description: '',
    file: null,
    isPasswordProtected: false,
    documentPassword: ''
  });
  const [showVerificationPassword, setShowVerificationPassword] = useState(false);
  const [verificationDocUploading, setVerificationDocUploading] = useState(false);
  const [verificationDocDeleting, setVerificationDocDeleting] = useState(null);
  const [verificationDocError, setVerificationDocError] = useState('');
  const [verificationDocSuccess, setVerificationDocSuccess] = useState('');

  // Parse the verification documents array stored as JSON on the property row.
  const parseVerificationDocs = () => {
    if (!property || !property.verificationDocumentsJson) return [];
    try { const documents = JSON.parse(property.verificationDocumentsJson); return Array.isArray(documents) ? documents : []; }
    catch { return []; }
  };

  const parseVerificationDocumentMetadata = () => {
    if (!property || !property.verificationDocumentMetadataJson) return [];
    try {
      const metadata = JSON.parse(property.verificationDocumentMetadataJson);
      return Array.isArray(metadata) ? metadata : [];
    }
    catch { return []; }
  };

  const resetVerificationDocForm = () => {
    setVerificationDocForm({
      name: '',
      description: '',
      file: null,
      isPasswordProtected: false,
      documentPassword: ''
    });
    setShowVerificationPassword(false);
  };

  const handleVerificationPasswordToggle = (event) => {
    const isPasswordProtected = event.target.checked;
    setVerificationDocForm((currentForm) => ({
      ...currentForm,
      isPasswordProtected,
      documentPassword: isPasswordProtected ? currentForm.documentPassword : ''
    }));
    setShowVerificationPassword(false);
  };

  const handleVerificationFileChange = (event) => {
    setVerificationDocForm((currentForm) => ({
      ...currentForm,
      file: event.target.files?.[0] || null
    }));
  };

  const handleVerificationPasswordVisibilityToggle = () => {
    setShowVerificationPassword((isVisible) => !isVisible);
  };

  const uploadVerificationDocument = async (event) => {
    event.preventDefault();
    if (!verificationDocForm.file || !verificationDocForm.name.trim()) {
      setVerificationDocError('A document name and file are required.');
      return;
    }
    if (verificationDocForm.file.size > 10 * 1024 * 1024 || !/\.(pdf|png|jpe?g|tiff?|gif|docx?|txt)$/i.test(verificationDocForm.file.name)) {
      setVerificationDocError('Choose a supported document no larger than 10 MB.');
      return;
    }
    if (verificationDocForm.isPasswordProtected && !verificationDocForm.documentPassword.trim()) {
      setVerificationDocError('Enter a password to protect this document.');
      return;
    }
    setVerificationDocUploading(true);
    setVerificationDocError('');
    setVerificationDocSuccess('');
    try {
      await propertyService.uploadPropertyDocument(property.propertyId, verificationDocForm.file, {
        name: verificationDocForm.name.trim(),
        description: verificationDocForm.description.trim(),
        isPasswordProtected: verificationDocForm.isPasswordProtected,
        documentPassword: verificationDocForm.documentPassword
      });
      // Refresh the property row so the new document appears in the vault.
      const updated = await propertyService.getPropertyById(propertyId);
      setProperty(updated);
      setVerificationDocSuccess('Verification document uploaded to the deeds vault.');
      setTimeout(() => setVerificationDocSuccess(''), 2500);
      resetVerificationDocForm();
    } catch (err) {
      setVerificationDocError((err && err.message) || 'Could not upload this verification document.');
    } finally {
      setVerificationDocUploading(false);
    }
  };

  const downloadVerificationDocument = async (index, name) => {
    try {
      const blob = await propertyService.downloadPropertyDocument(property.propertyId, index);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = name;
      anchor.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch {
      setVerificationDocError('Could not download this verification document. Please retry.');
    }
  };

  const deleteVerificationDocument = async (index) => {
    const docs = parseVerificationDocs();
    const target = docs[index];
    const label = (typeof target === 'string' && target.startsWith('data:'))
      ? `document #${index + 1}`
      : (target || `document #${index + 1}`);
    if (!window.confirm(`Are you sure you want to delete "${label}" from the property's deeds vault?`)) return;
    setVerificationDocDeleting(index);
    setVerificationDocError('');
    setVerificationDocSuccess('');
    try {
      await propertyService.deletePropertyDocument(property.propertyId, index);
      const updated = await propertyService.getPropertyById(propertyId);
      setProperty(updated);
      setVerificationDocSuccess('Verification document removed.');
      setTimeout(() => setVerificationDocSuccess(''), 1500);
    } catch (err) {
      setVerificationDocError((err && err.message) || 'Could not remove this verification document.');
    } finally {
      setVerificationDocDeleting(null);
    }
  };

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
         setPropertyOpsError((err && err.message) || 'Could not load property operations.');
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
         setErrorMessage((platformError && platformError.message) || 'Failed to load syndication platforms.');
       }

       // Load amenities (both scopes) + whether unit-level add is permitted.
       try {
         setAmenitiesLoading(true);
         const amen = await propertyService.getAmenities(propertyId);
         setAmenitiesData(amen || { items: [] });
       } catch (err) {
         setAmenityError((err && err.message) || 'Failed to load amenities.');
         setAmenitiesData({ isResidential: false, canAddUnitAmenities: true, items: [] });
       } finally {
         setAmenitiesLoading(false);
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
      isCurrentlyVacant: !!unit.isCurrentlyVacant
    });
    setUnitEditError(''); setUnitEditSuccess('');
    setUnitDocForm({ name: '', description: '', file: null });
    setUnitDocError(''); setUnitDocSuccess('');
  };

  const closeUnitModal = () => setUnitModalUnit(null);

  // Load maintenance operational records when the modal is open on that tab.
  // (The legacy "leases" module of the OperationalRecord ledger has been
  // superseded by the dedicated Lease/History tabs above.)
  useEffect(() => {
    const loadModalOps = async () => {
      if (!unitModalUnit || modalTab !== 'maintenance') return;
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
        setUnitOpsError((err && err.message) || 'Could not load unit records.');
      } finally {
        setUnitOpsLoading(false);
      }
    };
    loadModalOps();
  }, [unitModalUnit, modalTab]);

  // 📜 Load the unit lease ledger whenever the Lease or History tab opens.
  // The backend returns the full sorted list (newest first) with IsActive
  // re-derived from the dates. We also surface the most recent current
  // (active) lease into the editable Lease form so the user can update it
  // in place rather than create a new one.
  const loadUnitLeases = async () => {
    if (!unitModalUnit || !property) return;
    setUnitLeasesLoading(true);
    setUnitLeasesError('');
    try {
      const list = await propertyService.getUnitLeases(property.propertyId, unitModalUnit.propertyUnitId);
      setUnitLeases(Array.isArray(list) ? list : []);
      const active = (list || []).find((l) => l.isCurrent) || null;
      hydrateLeaseForm(active);
    } catch (err) {
      setUnitLeasesError((err && err.message) || 'Could not load unit leases.');
    } finally {
      setUnitLeasesLoading(false);
    }
  };

  useEffect(() => {
    if (!unitModalUnit) return;
    if (modalTab === 'lease' || modalTab === 'history') {
      loadUnitLeases();
      setHistoryDetail(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [unitModalUnit, modalTab]);

  // Populate the editable Lease form from a lease record. `leaseEditingId`
  // distinguishes "edit existing" from "create new".
  const hydrateLeaseForm = (lease) => {
    if (lease) {
      const display = lease.contactId
        ? { contactId: lease.contactId, fullName: lease.tenantNameSnapshot || '(linked contact)' }
        : null;
      setLeaseForm({
        contactId: lease.contactId || null,
        contactDisplay: display,
        tenantName: lease.tenantNameSnapshot || '',
        leaseStart: lease.leaseStart ? new Date(lease.leaseStart).toISOString().slice(0, 10) : '',
        leaseEnd: lease.leaseEnd ? new Date(lease.leaseEnd).toISOString().slice(0, 10) : '',
        monthlyRent: lease.monthlyRent ?? '',
        depositAmount: lease.depositAmount ?? '',
        accruedInterestPercent: lease.accruedInterestPercent ?? '',
        notes: lease.notes || ''
      });
      setLeaseEditingId(lease.unitLeaseId);
    } else {
      setLeaseForm({ contactId: null, contactDisplay: null, tenantName: '', leaseStart: '', leaseEnd: '', monthlyRent: '', depositAmount: '', accruedInterestPercent: '', notes: '' });
      setLeaseEditingId(null);
    }
  };

  const saveLease = async (e) => {
    e.preventDefault();
    if (!unitModalUnit || !property) return;
    if (!leaseForm.leaseStart || !leaseForm.leaseEnd) {
      setUnitLeasesError('Start and end dates are required.');
      return;
    }
    if (new Date(leaseForm.leaseEnd) <= new Date(leaseForm.leaseStart)) {
      setUnitLeasesError('Lease end date must be after the start date.');
      return;
    }
    setUnitLeasesError(''); setUnitLeasesSuccess('');
    const payload = {
      leaseStart: new Date(leaseForm.leaseStart).toISOString(),
      leaseEnd: new Date(leaseForm.leaseEnd).toISOString(),
      contactId: leaseForm.contactDisplay?.contactId || null,
      tenantNameSnapshot: leaseForm.tenantName || null,
      monthlyRent: leaseForm.monthlyRent === '' ? null : Number(leaseForm.monthlyRent),
      depositAmount: leaseForm.depositAmount === '' ? null : Number(leaseForm.depositAmount),
      accruedInterestPercent: leaseForm.accruedInterestPercent === '' ? null : Number(leaseForm.accruedInterestPercent),
      notes: leaseForm.notes || null
    };
    try {
      if (leaseEditingId) {
        await propertyService.updateUnitLease(property.propertyId, unitModalUnit.propertyUnitId, leaseEditingId, payload);
      } else {
        await propertyService.createUnitLease(property.propertyId, unitModalUnit.propertyUnitId, payload);
      }
      setUnitLeasesSuccess('Lease saved.');
      setTimeout(() => setUnitLeasesSuccess(''), 2000);
      await loadUnitLeases();
    } catch (err) {
      setUnitLeasesError((err && err.message) || 'Could not save lease.');
    }
  };

  const endLeaseNow = async (lease) => {
    if (!unitModalUnit || !property) return;
    if (!window.confirm('End this lease now? The end date will be set to today and the unit will become vacant.')) return;
    try {
      await propertyService.updateUnitLease(property.propertyId, unitModalUnit.propertyUnitId, lease.unitLeaseId, { endNow: true });
      setUnitLeasesSuccess('Lease ended.');
      setTimeout(() => setUnitLeasesSuccess(''), 2000);
      await loadUnitLeases();
    } catch (err) {
      setUnitLeasesError((err && err.message) || 'Could not end lease.');
    }
  };

  const deleteLease = async (lease) => {
    if (!unitModalUnit || !property) return;
    if (!window.confirm('Delete this lease record permanently? This cannot be undone.')) return;
    try {
      await propertyService.deleteUnitLease(property.propertyId, unitModalUnit.propertyUnitId, lease.unitLeaseId);
      setUnitLeasesSuccess('Lease removed.');
      setTimeout(() => setUnitLeasesSuccess(''), 2000);
      if (leaseEditingId === lease.unitLeaseId) hydrateLeaseForm(null);
      await loadUnitLeases();
    } catch (err) {
      setUnitLeasesError((err && err.message) || 'Could not delete lease.');
    }
  };

  // 🕒 Open a past lease in the read-only History detail view. Loads the
  // maintenance summary for the unit (all calls, since we don't link to leases).
  const openHistoryDetail = async (lease) => {
    setHistoryDetail(lease);
    setHistoryMaintOpen(false);
    setHistoryLoading(true);
    try {
      const calls = await propertyService.getUnitMaintenanceCalls(property.propertyId, unitModalUnit.propertyUnitId);
      setHistoryMaintCalls(calls || []);
    } catch {
      setHistoryMaintCalls([]);
    } finally {
      setHistoryLoading(false);
    }
  };

  // Computes the lease term in months for display on history rows.
  const leaseTermLabel = (start, end) => {
    if (!start || !end) return '—';
    const s = new Date(start);
    const e = new Date(end);
    if (Number.isNaN(s) || Number.isNaN(e) || e <= s) return '—';
    const months = Math.round(((e - s) / (1000 * 60 * 60 * 24 * 30.4375)) * 10) / 10;
    return `${months} month${months === 1 ? '' : 's'}`;
  };

  // 💰 Amount due to tenant on lease termination.
  //   total = deposit × (1 + rate × (months / 12))
  // `rate` is a percentage per year (e.g. 5 means 5% p.a.).
  // Returns null if any input is missing/invalid.
  const leaseTerminationAmount = (deposit, ratePct, start, end) => {
    const d = Number(deposit);
    const r = Number(ratePct);
    if (!Number.isFinite(d) || d <= 0) return null;
    if (!Number.isFinite(r) || r < 0) return null;
    if (!start || !end) return null;
    const s = new Date(start);
    const e = new Date(end);
    if (Number.isNaN(s) || Number.isNaN(e) || e <= s) return null;
    const months = (e - s) / (1000 * 60 * 60 * 24 * 30.4375);
    const years = months / 12;
    return d * (1 + (r / 100) * years);
  };

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
      setUnitOpsError((err && err.message) || 'Could not save this record.');
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
        isCurrentlyVacant: unitEditForm.isCurrentlyVacant
      });
      // Reflect edits into the open modal + the master property list
      setUnitModalUnit(saved);
      setSubUnits(prev => prev.map(u => u.propertyUnitId === saved.propertyUnitId ? saved : u));
      setUnitEditSuccess('Unit details updated.');
      setTimeout(() => setUnitEditSuccess(''), 2000);
    } catch (err) {
      setUnitEditError((err && err.message) || 'Could not update unit details.');
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
      setUnitDocError((err && err.message) || 'Could not upload this document.');
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
      setUnitDocError((err && err.message) || 'Could not remove this document.');
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
        latitude: Number(propertyForm.latitude || 0), longitude: Number(propertyForm.longitude || 0),
        // 🧬 Listing flags — preserve the user's checkbox selection through
        // the save call. Coerce to strict booleans for the backend.
        isForSale: !!propertyForm.isForSale,
        isForRent: !!propertyForm.isForRent,
        // 🧬 Derive the legacy MarketClass string for any consumer that still
        // reads it (search criteria, syndication rules, etc).
        marketClass: (() => {
          const s = !!propertyForm.isForSale, r = !!propertyForm.isForRent;
          return s && r ? 'Both' : s ? 'Sale' : r ? 'Rental' : 'Off Market';
        })()
      });
      setProperty(saved); setSubUnits(saved.subUnits || subUnits); setIsEditingProperty(false);
      setActionSuccessMessage('Property details saved to the backend.');
    } catch (err) { setErrorMessage((err && err.message) || 'Could not save property details.'); }
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

      {/* 📜 SOLE MANDATE BANNERS — visible to ALL roles. The yellow obligation
          banner is shown whenever a property has an active sole mandate, to
          remind every viewer of the legal-document requirement. The orange/red
          14-day expiration banner fires when the mandate is about to lapse. */}
      {property.activeSoleMandate && (
        <div className="space-y-2">
          <div className="flex items-start gap-3 p-3.5 rounded-2xl border border-amber-700/50 bg-gradient-to-br from-amber-950/50 to-slate-900/40 text-amber-100 text-xs leading-relaxed">
            <span className="text-base leading-none shrink-0">📜</span>
            <div className="flex-1">
              <strong className="font-bold uppercase tracking-[0.1em] text-[10px] text-amber-300">Sole Mandate Active</strong>
              <p className="mt-1">
                This property is under a sole mandate from <strong>{property.activeSoleMandate.startDate?.slice(0, 10)}</strong> to <strong>{property.activeSoleMandate.endDate?.slice(0, 10)}</strong>.
                The agent of record is obligated to keep the signed mandate legal documents on file in this property's documents tab before any marketing activity.
              </p>
            </div>
          </div>
          {Number.isFinite(property.activeSoleMandate.daysRemaining) && property.activeSoleMandate.daysRemaining <= 14 && (
            <div className={`flex items-start gap-3 p-3.5 rounded-2xl border text-xs leading-relaxed ${property.activeSoleMandate.daysRemaining <= 0 ? 'bg-red-950/60 border-red-700/60 text-red-200' : property.activeSoleMandate.daysRemaining <= 7 ? 'bg-red-950/40 border-red-800/60 text-red-200' : 'bg-orange-950/40 border-orange-700/50 text-orange-200'}`}>
              <span className="text-base leading-none shrink-0">⏰</span>
              <div className="flex-1">
                <strong className="font-bold uppercase tracking-[0.1em] text-[10px]">
                  {property.activeSoleMandate.daysRemaining <= 0 ? 'Mandate Expired' : 'Mandate Expiring Soon'}
                </strong>
                <p className="mt-1">
                  {property.activeSoleMandate.daysRemaining <= 0
                    ? `The sole mandate expired ${Math.abs(property.activeSoleMandate.daysRemaining)} day${Math.abs(property.activeSoleMandate.daysRemaining) === 1 ? '' : 's'} ago. Renew or open a new mandate to continue marketing this property.`
                    : `The sole mandate expires in ${property.activeSoleMandate.daysRemaining} day${property.activeSoleMandate.daysRemaining === 1 ? '' : 's'} (${property.activeSoleMandate.endDate?.slice(0, 10)}). Renew or open a new mandate before it lapses.`}
                </p>
              </div>
            </div>
          )}
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
                  {[property.portfolioZoning, property.propertyTypeDetails?.name].filter(Boolean).join(' • ') || 'Property classification not specified'}
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

            <PropertyStaticMap latitude={property.latitude} longitude={property.longitude} address={property.completeAddress} />
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

          {/* 🏠/🏢 PROPERTY & UNIT AMENITIES */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
            <div className="flex items-center justify-between flex-wrap gap-2">
              <div className="flex items-center gap-2">
                <Layers size={18} className="text-emerald-400" />
                <h4 className="text-sm font-bold text-white uppercase tracking-wider">Amenities</h4>
              </div>
              {amenityError && (
                <span className="text-[10px] text-red-400 bg-red-950/40 px-2 py-1 rounded border border-red-900">{amenityError}</span>
              )}
            </div>

            {amenitiesLoading ? (
              <div className="text-xs text-slate-500 flex items-center gap-2"><Loader2 size={12} className="animate-spin" /> Loading amenities…</div>
            ) : (
              (() => {
                const items = amenitiesData?.items || [];
                const isResidential = !!amenitiesData?.isResidential;
                const canAddUnit = !!amenitiesData?.canAddUnitAmenities;
                const propertyAmenities = items.filter((a) => !a.propertyUnitId);
                const unitAmenities = items.filter((a) => !!a.propertyUnitId);

                const renderAmenityRow = (a) => (
                  <div key={a.amenityId} className="p-3 bg-slate-950/60 border border-slate-800 rounded-xl flex items-center justify-between gap-3">
                    <div className="flex items-center gap-2.5 min-w-0">
                      {a.amenityName === 'Parking' ? <ParkingMeter size={14} className="text-slate-400" /> : null}
                      {a.amenityName === 'PetsAllowed' ? <Heart size={14} className="text-red-400" /> : null}
                      {a.amenityName === 'Gym' ? <Wind size={14} className="text-indigo-400" /> : null}
                      {a.amenityName === 'SwimmingPool' ? <Layers size={14} className="text-cyan-400" /> : null}
                      {a.amenityName === 'Security' ? <Shield size={14} className="text-amber-400" /> : null}
                      <div className="min-w-0">
                        <span className="text-xs font-semibold text-white truncate">{a.label || a.amenityName}</span>
                        <span className="block text-[10px] text-slate-500 truncate">{isResidential && a.isResidentialOnly ? 'Residential-only' : ''} {a.propertyUnitId ? `· Unit #${a.propertyUnitId}` : '· Property-level'}</span>
                      </div>
                    </div>
                    <div className="text-right">
                      {a.requiresBoolean !== undefined && a.requiresBoolean !== null && a.requiresBoolean
                        ? (a.booleanValue
                          ? <span className="text-xs font-bold text-emerald-400">Allowed</span>
                          : <span className="text-xs font-semibold text-slate-400">No</span>)
                        : a.requiresNumber
                          ? <span className="text-xs font-bold text-white">{a.numberValue ?? 0} {a.numberValue === 1 ? 'bay' : 'bays'}</span>
                          : <span className="text-[10px] text-slate-500">—</span>}
                    </div>
                  </div>
                );

                return (
                  <div className="space-y-3.5">
                    {/* Add form (property-level always; unit-level only for non-residential) */}
                    <form onSubmit={handleSaveAmenity} className="p-3.5 bg-slate-950/50 border border-slate-800 rounded-xl grid grid-cols-1 md:grid-cols-6 gap-3 items-end">
                      <div className="md:col-span-2">
                        <label className="block text-[10px] uppercase text-slate-500 font-bold mb-1">Amenity</label>
                        <select
                          value={amenityForm.amenityTypeId || ''}
                          onChange={(e) => setAmenityForm({ ...amenityForm, amenityTypeId: Number(e.target.value), scope: amenityForm.scope || 'property' })}
                          className="w-full text-xs bg-slate-900 border border-slate-700 rounded-lg text-white px-2 py-1.5 focus:border-blue-500"
                        >
                          <option value="">— select amenity —</option>
                          {amenityTypes.map((t) => (
                            <option key={t.amenityTypeId} value={t.amenityTypeId}>{t.label || t.name}</option>
                          ))}
                        </select>
                      </div>

                      <div className="md:col-span-2">
                        <label className="block text-[10px] uppercase text-slate-500 font-bold mb-1">Scope</label>
                        <select
                          value={amenityForm.scope || 'property'}
                          onChange={(e) => setAmenityForm({ ...amenityForm, scope: e.target.value, unitId: undefined })}
                          className="w-full text-xs bg-slate-900 border border-slate-700 rounded-lg text-white px-2 py-1.5 focus:border-blue-500"
                          disabled={isResidential} // residential = property-level only
                        >
                          <option value="property">Property-level</option>
                          <option value="unit" disabled={!canAddUnit}>Unit-level</option>
                        </select>
                        {isResidential && (
                          <p className="text-[9px] text-amber-400 mt-1">Locked: residential properties can't add unit-level amenities.</p>
                        )}
                      </div>

                      {amenityForm.scope === 'unit' && canAddUnit && (
                        <div className="md:col-span-2">
                          <label className="block text-[10px] uppercase text-slate-500 font-bold mb-1">Unit</label>
                          <select
                            value={amenityForm.unitId || ''}
                            onChange={(e) => setAmenityForm({ ...amenityForm, unitId: Number(e.target.value) })}
                            className="w-full text-xs bg-slate-900 border border-slate-700 rounded-lg text-white px-2 py-1.5 focus:border-blue-500"
                          >
                            {subUnits.map((u) => (
                              <option key={u.propertyUnitId} value={u.propertyUnitId}>#{u.propertyUnitId} — {u.unitNumber}</option>
                            ))}
                          </select>
                        </div>
                      )}

                      <div className="md:col-span-2">
                        {amenityForm.amenityTypeId && amenityTypes.find((t) => t.amenityTypeId === amenityForm.amenityTypeId)?.requiresBoolean ? (
                          <label className="flex items-center gap-2 text-xs">
                            <input
                              type="checkbox"
                              checked={!!amenityForm.booleanValue}
                              onChange={(e) => setAmenityForm({ ...amenityForm, booleanValue: e.target.checked })}
                              className="w-4 h-4 rounded bg-slate-900 border-slate-600 text-blue-500 focus:ring-blue-500"
                            />
                            <span className="text-slate-300">Enabled (pets allowed / feature available)</span>
                          </label>
                        ) : amenityForm.amenityTypeId && amenityTypes.find((t) => t.amenityTypeId === amenityForm.amenityTypeId)?.requiresNumber ? (
                          <label className="block text-[10px] uppercase text-slate-500 font-bold mb-1">
                            Quantity
                            <input
                              type="number" min={0}
                              value={amenityForm.numberValue ?? ''}
                              onChange={(e) => setAmenityForm({ ...amenityForm, numberValue: Number(e.target.value) })}
                              className="w-full mt-1 text-xs bg-slate-900 border border-slate-700 rounded-lg text-white px-2 py-1.5 focus:border-blue-500"
                            />
                          </label>
                        ) : <label className="block text-[10px] uppercase text-slate-500 font-bold mb-1">Value — select an amenity that carries a quantity or toggle</label>}
                      </div>

                      <div className="md:col-span-2">
                        <label className="block text-[10px] uppercase text-slate-500 font-bold mb-1">Note</label>
                        <input
                          type="text"
                          value={amenityForm.notes || ''}
                          onChange={(e) => setAmenityForm({ ...amenityForm, notes: e.target.value })}
                          placeholder="e.g. visitor bays reserved"
                          className="w-full text-xs bg-slate-900 border border-slate-700 rounded-lg text-white px-2 py-1.5 focus:border-blue-500"
                        />
                      </div>

                      {amenitySaving ? null : (
                        <div className="md:col-span-full flex justify-end">
                          <button type="submit" disabled={amenitySaving || !amenityForm.amenityTypeId} className="inline-flex items-center gap-1 text-xs font-semibold text-white bg-emerald-600 hover:bg-emerald-500 px-3 py-1.5 rounded-lg transition-colors cursor-pointer disabled:opacity-40">
                            <Save size={12} /> {amenityForm.amenityTypeId ? 'Save amenity' : 'Choose an amenity first'}
                          </button>
                        </div>
                      )}
                    </form>

                    {propertyAmenities.length > 0 && (
                      <div className="space-y-1">
                        <div className="text-[10px] uppercase text-slate-500 font-bold">Property-level amenities</div>
                        {propertyAmenities.map(renderAmenityRow)}
                      </div>
                    )}
                    {unitAmenities.length > 0 && (
                      <div className="space-y-1">
                        <div className="text-[10px] uppercase text-slate-500 font-bold">Unit-level amenities</div>
                        {unitAmenities.map(renderAmenityRow)}
                      </div>
                    )}
                    {items.length === 0 && (
                      <p className="text-xs text-slate-500">No amenities attached yet. Parking (number of bays) and Pets allowed (residential toggle) are supported.</p>
                    )}
                  </div>
                );
              })()
            )}
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
                  <button type="button" onClick={() => setModalTab('lease')} className={`flex items-center gap-1 px-3 py-2 text-xs font-bold ${modalTab === 'lease' ? 'text-blue-300 border-b-2 border-blue-500' : 'text-slate-400'}`}>
                    <FileSignature size={13} />Lease
                  </button>
                  <button type="button" onClick={() => setModalTab('history')} className={`flex items-center gap-1 px-3 py-2 text-xs font-bold ${modalTab === 'history' ? 'text-blue-300 border-b-2 border-blue-500' : 'text-slate-400'}`}>
                    <History size={13} />History
                  </button>
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
                      <label className="flex items-center gap-2 text-xs mt-1"><input type="checkbox" checked={unitEditForm.isCurrentlyVacant} onChange={e => setUnitEditForm({ ...unitEditForm, isCurrentlyVacant: e.target.checked })} /><span className="text-slate-300">Currently vacant</span></label>
                      <div className="sm:col-span-2">
                        <Can permission="units:edit">
                          <button type="submit" className="inline-flex items-center gap-1.5 px-4 py-2 bg-blue-600 text-white rounded-xl text-xs font-bold"><Save size={13} />Save unit details</button>
                        </Can>
                      </div>
                    </form>
                  )}

                  {/* 📜 LEASE TAB — editable current lease (or new lease) backed by UnitLeases */}
                  {modalTab === 'lease' && (
                    <div className="space-y-4">
                      {unitLeasesError && <div className="p-3 rounded-xl text-xs bg-red-950/40 border border-red-900 text-red-300">{unitLeasesError}</div>}
                      {unitLeasesSuccess && <div className="p-3 rounded-xl text-xs bg-emerald-950/40 border border-emerald-900 text-emerald-300">{unitLeasesSuccess}</div>}

                      <div className="grid xl:grid-cols-[1fr_320px] gap-6">
                        <form onSubmit={saveLease} className="space-y-3">
                          <div className="flex items-center justify-between">
                            <h5 className="text-xs font-bold text-blue-400 uppercase">
                              {leaseEditingId ? 'Edit Current Lease' : 'New Lease'}
                            </h5>
                            {leaseEditingId && (
                              <span className="text-[10px] text-slate-500">Lease #{leaseEditingId}</span>
                            )}
                          </div>

                          <div>
                            <label className="text-[10px] text-slate-400 uppercase">Tenant (contact)</label>
                            <ContactPicker
                              value={leaseForm.contactDisplay}
                              onChange={(c) => setLeaseForm({ ...leaseForm, contactDisplay: c, contactId: c?.contactId || null, tenantName: c?.fullName || '' })}
                              allowCreate
                              placeholder="Search tenant or create new..."
                            />
                          </div>

                          <div className="grid grid-cols-2 gap-3">
                            <label className="label">Start date<input required type="date" value={leaseForm.leaseStart} onChange={(e) => setLeaseForm({ ...leaseForm, leaseStart: e.target.value })} className="field" /></label>
                            <label className="label">End date<input required type="date" value={leaseForm.leaseEnd} onChange={(e) => setLeaseForm({ ...leaseForm, leaseEnd: e.target.value })} className="field" /></label>
                          </div>
                          {leaseForm.leaseStart && leaseForm.leaseEnd && new Date(leaseForm.leaseEnd) > new Date(leaseForm.leaseStart) && (
                            <div className="flex items-center gap-1.5 text-[10px] text-slate-400">
                              <Calendar size={11} />Term: {leaseTermLabel(leaseForm.leaseStart, leaseForm.leaseEnd)}
                            </div>
                          )}

                          <div className="grid grid-cols-2 gap-3">
                            <label className="label">Monthly rent (R)<input type="number" min="0" value={leaseForm.monthlyRent ?? ''} onChange={(e) => setLeaseForm({ ...leaseForm, monthlyRent: e.target.value })} className="field" /></label>
                            <label className="label">Deposit (R)<input type="number" min="0" value={leaseForm.depositAmount ?? ''} onChange={(e) => setLeaseForm({ ...leaseForm, depositAmount: e.target.value })} className="field" /></label>
                          </div>

                          <label className="label">Accrued interest on deposit (% per year)<input type="number" min="0" step="0.01" value={leaseForm.accruedInterestPercent ?? ''} onChange={(e) => setLeaseForm({ ...leaseForm, accruedInterestPercent: e.target.value })} className="field" placeholder="e.g. 5.00" /></label>

                          {/* 💰 Live read-only label — recomputed on every keystroke so the
                              user can see the termination value update as they edit deposit /
                              rate / dates. Wording flips to "Amount paid" when the lease
                              window is already in the past. Same formula is also used in
                              the History detail view. */}
                          {(() => {
                            const total = leaseTerminationAmount(
                              leaseForm.depositAmount,
                              leaseForm.accruedInterestPercent,
                              leaseForm.leaseStart,
                              leaseForm.leaseEnd
                            );
                            if (total == null) {
                              return (
                                <div className="p-2.5 rounded-xl bg-slate-950/40 border border-dashed border-slate-800 text-[10px] text-slate-500">
                                  Amount due to tenant on lease termination: enter deposit, rate, and both dates to preview.
                                </div>
                              );
                            }
                            const previewExpired = leaseForm.leaseEnd && new Date(leaseForm.leaseEnd) < new Date();
                            const label = previewExpired
                              ? 'Amount paid to tenant on lease termination'
                              : 'Amount due to tenant on lease termination';
                            return (
                              <div className="p-2.5 rounded-xl bg-emerald-950/30 border border-emerald-900">
                                <span className="block text-[10px] text-emerald-400 uppercase font-bold tracking-wider">{label}</span>
                                <span className="text-lg font-black text-emerald-200">R {Math.round(total).toLocaleString()}</span>
                                <span className="block text-[10px] text-emerald-400/70 mt-0.5">
                                  Deposit {Number(leaseForm.depositAmount).toLocaleString()} + interest at {Number(leaseForm.accruedInterestPercent)}% p.a. over {leaseTermLabel(leaseForm.leaseStart, leaseForm.leaseEnd)}
                                </span>
                              </div>
                            );
                          })()}

                          <label className="label">Notes<textarea rows={3} value={leaseForm.notes} onChange={(e) => setLeaseForm({ ...leaseForm, notes: e.target.value })} className="field" placeholder="Notice period, escalation notes, special conditions..." /></label>

                          <div className="flex gap-2">
                            {leaseEditingId && (
                              <button type="button" onClick={() => hydrateLeaseForm(null)} className="py-2 px-4 border border-slate-700 rounded-xl text-xs">New</button>
                            )}
                            <Can permission="units:edit">
                              <button className="flex-1 py-2 bg-blue-600 hover:bg-blue-500 text-white rounded-xl text-xs font-bold">{leaseEditingId ? 'Save changes' : 'Create lease'}</button>
                            </Can>
                          </div>
                        </form>

                        <div className="space-y-2">
                          <h5 className="text-xs font-bold text-blue-400 uppercase">Current Tenant</h5>
                          {unitLeasesLoading ? (
                            <div className="flex items-center gap-2 text-xs text-slate-400"><Loader2 className="animate-spin" size={14} />Loading…</div>
                          ) : (() => {
                            const current = unitLeases.find((l) => l.isCurrent);
                            const upcoming = unitLeases.find((l) => l.isFuture);
                            if (!current) {
                              return (
                                <div className="p-3 bg-slate-950/60 border border-slate-800 rounded-xl space-y-2">
                                  <p className="text-xs text-slate-500">No active tenant for this unit right now.</p>
                                  {upcoming && (
                                    <p className="text-[10px] text-blue-300">Upcoming: starts {new Date(upcoming.leaseStart).toLocaleDateString()}</p>
                                  )}
                                </div>
                              );
                            }
                            return (
                              <div className="p-3 bg-emerald-950/30 border border-emerald-900 rounded-xl space-y-2">
                                <div className="flex items-center justify-between">
                                  <span className="text-xs font-bold text-emerald-300 uppercase">Active</span>
                                  <span className="text-[10px] text-slate-400">Lease #{current.unitLeaseId}</span>
                                </div>
                                <p className="text-sm font-semibold text-white">{current.tenantNameSnapshot || (current.contactId ? `Tenant ${current.contactId}` : 'Unlinked lease')}</p>
                                <p className="text-[10px] text-slate-400">
                                  {new Date(current.leaseStart).toLocaleDateString()} → {new Date(current.leaseEnd).toLocaleDateString()} ({leaseTermLabel(current.leaseStart, current.leaseEnd)})
                                </p>
                                {current.monthlyRent != null && <p className="text-[10px] text-emerald-300">R {Number(current.monthlyRent).toLocaleString()}/mo</p>}
                                <div className="flex gap-1.5 pt-1">
                                  <Can permission="units:edit">
                                    <button type="button" onClick={() => hydrateLeaseForm(current)} className="flex-1 text-[10px] py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg"><Pencil size={11} className="inline" /> Edit</button>
                                    <button type="button" onClick={() => endLeaseNow(current)} className="flex-1 text-[10px] py-1.5 bg-amber-900/60 hover:bg-amber-900 text-amber-200 rounded-lg">End now</button>
                                  </Can>
                                </div>
                              </div>
                            );
                          })()}
                        </div>
                      </div>
                    </div>
                  )}

                  {/* 🕒 HISTORY TAB — list of every lease for this unit. Click a row for read-only detail + maintenance summary. */}
                  {modalTab === 'history' && (
                    <div className="space-y-3">
                      {unitLeasesError && <div className="p-3 rounded-xl text-xs bg-red-950/40 border border-red-900 text-red-300">{unitLeasesError}</div>}

                      {historyDetail ? (
                        // ─── read-only lease detail + collapsible maintenance summary ───
                        <div className="space-y-3">
                          <button type="button" onClick={() => setHistoryDetail(null)} className="inline-flex items-center gap-1 text-[10px] text-blue-300 hover:text-white"><ArrowLeft size={11} />Back to history</button>
                          <div className="p-4 bg-slate-950/60 border border-slate-800 rounded-xl space-y-2">
                            <div className="flex items-center justify-between">
                              <h5 className="text-sm font-bold text-white">Lease #{historyDetail.unitLeaseId}</h5>
                              {historyDetail.isCurrent ? (
                                <span className="text-[10px] px-2 py-0.5 rounded bg-emerald-900/60 text-emerald-300 uppercase">Active</span>
                              ) : historyDetail.isFuture ? (
                                <span className="text-[10px] px-2 py-0.5 rounded bg-blue-900/60 text-blue-300 uppercase">Upcoming</span>
                              ) : (
                                <span className="text-[10px] px-2 py-0.5 rounded bg-slate-800 text-slate-400 uppercase">Expired</span>
                              )}
                            </div>
                            <div className="grid sm:grid-cols-2 gap-2 text-xs text-slate-300">
                              <div><span className="text-slate-500">Start:</span> {new Date(historyDetail.leaseStart).toLocaleDateString()}</div>
                              <div><span className="text-slate-500">End:</span> {new Date(historyDetail.leaseEnd).toLocaleDateString()}</div>
                              <div><span className="text-slate-500">Term:</span> {leaseTermLabel(historyDetail.leaseStart, historyDetail.leaseEnd)}</div>
                              <div><span className="text-slate-500">Monthly rent:</span> {historyDetail.monthlyRent != null ? `R ${Number(historyDetail.monthlyRent).toLocaleString()}` : '—'}</div>
                              <div><span className="text-slate-500">Deposit:</span> {historyDetail.depositAmount != null ? `R ${Number(historyDetail.depositAmount).toLocaleString()}` : '—'}</div>
                              <div><span className="text-slate-500">Interest rate:</span> {historyDetail.accruedInterestPercent != null ? `${Number(historyDetail.accruedInterestPercent)}% p.a.` : '—'}</div>
                              <div className="sm:col-span-2">
                                <span className="text-slate-500">Tenant:</span>{' '}
                                <span className="text-white font-semibold">
                                  {historyDetail.tenantNameSnapshot || (historyDetail.contactId ? historyDetail.contactId : 'Unlinked')}
                                </span>
                                {historyDetail.contactId && historyDetail.tenantNameSnapshot && (
                                  <span className="text-slate-500 text-[10px] ml-2">({historyDetail.contactId})</span>
                                )}
                              </div>
                            </div>
                            {(() => {
                              const total = leaseTerminationAmount(
                                historyDetail.depositAmount,
                                historyDetail.accruedInterestPercent,
                                historyDetail.leaseStart,
                                historyDetail.leaseEnd
                              );
                              if (total == null) return null;
                              const label = historyDetail.isExpired
                                ? 'Amount paid to tenant on lease termination'
                                : 'Amount due to tenant on lease termination';
                              return (
                                <div className="p-2.5 rounded-xl bg-emerald-950/30 border border-emerald-900">
                                  <span className="block text-[10px] text-emerald-400 uppercase font-bold tracking-wider">{label}</span>
                                  <span className="text-lg font-black text-emerald-200">R {Math.round(total).toLocaleString()}</span>
                                </div>
                              );
                            })()}
                            {historyDetail.notes && (
                              <div className="pt-2 border-t border-slate-800 text-xs text-slate-300 whitespace-pre-wrap">{historyDetail.notes}</div>
                            )}
                          </div>

                          {/* Collapsible maintenance summary — all unit calls, since we don't link to leases. */}
                          <button
                            type="button"
                            onClick={() => setHistoryMaintOpen(!historyMaintOpen)}
                            className="w-full flex items-center justify-between p-3 bg-slate-950/60 border border-slate-800 rounded-xl hover:bg-slate-900"
                          >
                            <span className="flex items-center gap-2 text-xs font-bold text-slate-200">
                              <Wrench size={13} className="text-blue-400" />
                              Maintenance Calls ({historyMaintCalls.length})
                            </span>
                            {historyMaintOpen ? <ChevronUp size={14} className="text-slate-400" /> : <ChevronDown size={14} className="text-slate-400" />}
                          </button>
                          {historyMaintOpen && (
                            <div className="space-y-2 pl-2">
                              {historyLoading ? (
                                <div className="flex items-center gap-2 text-xs text-slate-400 pl-2"><Loader2 className="animate-spin" size={14} />Loading maintenance…</div>
                              ) : historyMaintCalls.length === 0 ? (
                                <p className="text-[10px] text-slate-500 pl-2">No maintenance calls logged for this unit.</p>
                              ) : (
                                historyMaintCalls.map((c) => (
                                  <div key={c.maintenanceCallId} className="p-2.5 bg-slate-950/40 border border-slate-800 rounded-lg flex items-center justify-between gap-2">
                                    <div className="min-w-0">
                                      <p className="text-xs font-semibold text-white truncate">{c.title}</p>
                                      <p className="text-[10px] text-slate-500">{new Date(c.createdAt).toLocaleDateString()} • {c.status}</p>
                                    </div>
                                  </div>
                                ))
                              )}
                            </div>
                          )}

                          <Can permission="units:delete">
                            <button type="button" onClick={() => deleteLease(historyDetail).then(() => setHistoryDetail(null))} className="text-[10px] text-red-400 hover:text-red-300 inline-flex items-center gap-1"><Trash2 size={11} />Delete this lease</button>
                          </Can>
                        </div>
                      ) : (
                        // ─── history list ───
                        <>
                          {unitLeasesLoading ? (
                            <div className="flex items-center gap-2 text-xs text-slate-400"><Loader2 className="animate-spin" size={14} />Loading history…</div>
                          ) : unitLeases.length === 0 ? (
                            <p className="text-[10px] text-slate-500 text-center py-8">No leases recorded for this unit yet. Use the Lease tab to add one.</p>
                          ) : (
                            <div className="space-y-2">
                              {unitLeases.map((l) => (
                                <button
                                  type="button"
                                  key={l.unitLeaseId}
                                  onClick={() => openHistoryDetail(l)}
                                  className="w-full p-3 bg-slate-950/60 border border-slate-800 hover:border-blue-700 rounded-xl text-left flex items-center justify-between gap-3 transition-colors"
                                >
                                  <div className="min-w-0 space-y-0.5">
                                    <div className="flex items-center gap-2 flex-wrap">
                                      <p className="text-xs font-bold text-white">
                                        Tenant: {l.tenantNameSnapshot || (l.contactId ? `(${l.contactId})` : 'Unlinked')}
                                      </p>
                                      {l.isCurrent ? (
                                        <span className="text-[10px] px-1.5 py-0.5 rounded bg-emerald-900/60 text-emerald-300 uppercase">Active</span>
                                      ) : l.isFuture ? (
                                        <span className="text-[10px] px-1.5 py-0.5 rounded bg-blue-900/60 text-blue-300 uppercase">Upcoming</span>
                                      ) : (
                                        <span className="text-[10px] px-1.5 py-0.5 rounded bg-slate-800 text-slate-400 uppercase">Expired</span>
                                      )}
                                    </div>
                                    <p className="text-[10px] text-slate-400">
                                      {new Date(l.leaseStart).toLocaleDateString()} → {new Date(l.leaseEnd).toLocaleDateString()} ({leaseTermLabel(l.leaseStart, l.leaseEnd)})
                                    </p>
                                    {l.monthlyRent != null && <p className="text-[10px] text-emerald-400">R {Number(l.monthlyRent).toLocaleString()}/mo</p>}
                                  </div>
                                  <span className="text-[10px] text-blue-400 font-semibold shrink-0">View →</span>
                                </button>
                              ))}
                            </div>
                          )}
                        </>
                      )}
                    </div>
                  )}

                  {/* 📋 MAINTENANCE (CRM records scoped to this unit; property/unit auto-bound) */}
                  {modalTab === 'maintenance' && (
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

               {/* Show all gallery images */}
               {(JSON.parse(property.galleryImageJson || '[]')).map((doc, idx) => {
                 const isDataUrl = doc.startsWith('data:');
                 const href = isDataUrl ? doc : `/api/properties/${property.propertyId}/documents/${idx}`;
                 return (
                   <div key={idx} className="bg-slate-950 border border-slate-800 rounded-xl p-2 flex items-center justify-center h-32 relative group">
                     <img src={href} alt={`Property ${idx + 1}`} className="h-full w-full object-cover rounded-lg" />
                     <span className="absolute bottom-2 right-2 bg-slate-900/90 text-[9px] px-1.5 py-0.5 rounded text-blue-400 border border-slate-700">Image {idx + 1}</span>
                   </div>
                 );
               })}
               {!JSON.parse(property.galleryImageJson || '[]').length && (
                 <div className="bg-slate-950/20 border border-slate-800/80 rounded-xl flex items-center justify-center text-xs text-slate-600 italic">
                   No images in gallery yet
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

            {/* 📜 SOLE MANDATE EDITING PANEL (Task 7) — visible, intuitive location
                inside the Listing Details / Status editing panel so users can
                open / close / renew the mandate without hunting through menus. */}
            <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl">
              <div className="flex items-center justify-between mb-3">
                <h4 className="text-sm font-bold text-white uppercase tracking-wider flex items-center gap-2">
                  <FileSignature size={15} className="text-amber-400" /> Sole Mandate
                </h4>
                <span className={`text-[10px] font-bold uppercase px-2 py-0.5 rounded border ${
                  property.activeSoleMandate
                    ? 'bg-amber-950 text-amber-400 border-amber-900'
                    : 'bg-slate-800 text-slate-400 border-slate-700'
                }`}>
                  {property.activeSoleMandate ? 'Active' : 'Inactive'}
                </span>
              </div>

              {property.activeSoleMandate && (
                <div className="mb-3 p-2.5 bg-amber-950/30 border border-amber-900/60 rounded-xl text-[11px] text-amber-200">
                  Active mandate from <strong>{property.activeSoleMandate.startDate?.slice(0, 10)}</strong> to <strong>{property.activeSoleMandate.endDate?.slice(0, 10)}</strong>.
                </div>
              )}

              {mandateSuccess && (
                <div className="p-2 mb-2 rounded-lg text-[11px] bg-emerald-950/40 border border-emerald-900 text-emerald-300">{mandateSuccess}</div>
              )}
              {mandateError && (
                <div className="p-2 mb-2 rounded-lg text-[11px] bg-red-950/40 border border-red-900 text-red-300">{mandateError}</div>
              )}

              <Can permission="properties:edit">
                <div className="space-y-3">
                  <button
                    type="button"
                    onClick={() => setShowMandatePanel(v => !v)}
                    className="text-xs font-bold text-amber-300 border border-amber-900 rounded-lg px-3 py-1.5 hover:bg-amber-950 transition-colors cursor-pointer"
                  >
                    {showMandatePanel ? 'Collapse' : (property.activeSoleMandate ? 'Renew / Open new mandate' : 'Open sole mandate')}
                  </button>

                  {showMandatePanel && (
                    <form onSubmit={openSoleMandate} className="space-y-2.5 animate-fade-in">
                      <div className="grid grid-cols-2 gap-2">
                        <div>
                          <label className="block text-[10px] uppercase text-slate-500 font-bold mb-1">Start date</label>
                          <input
                            type="date"
                            value={mandateForm.startDate}
                            onChange={e => setMandateForm({ ...mandateForm, startDate: e.target.value })}
                            className="w-full px-2 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs text-white focus:outline-none focus:border-amber-500"
                          />
                        </div>
                        <div>
                          <label className="block text-[10px] uppercase text-slate-500 font-bold mb-1">End date</label>
                          <input
                            type="date"
                            value={mandateForm.endDate}
                            onChange={e => setMandateForm({ ...mandateForm, endDate: e.target.value })}
                            className="w-full px-2 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs text-white focus:outline-none focus:border-amber-500"
                          />
                        </div>
                      </div>
                      <div className="flex gap-2">
                        <button
                          type="submit"
                          disabled={mandateBusy}
                          className="flex-1 py-2 bg-amber-600 disabled:opacity-60 text-white rounded-lg text-xs font-bold"
                        >
                          {mandateBusy ? 'Working…' : 'Open mandate'}
                        </button>
                        {property.activeSoleMandate && (
                          <button
                            type="button"
                            onClick={closeSoleMandate}
                            disabled={mandateBusy}
                            className="flex-1 py-2 bg-red-700 disabled:opacity-60 text-white rounded-lg text-xs font-bold"
                          >
                            Close mandate
                          </button>
                        )}
                      </div>
                    </form>
                  )}
                </div>
              </Can>
            </div>
          </div>

          {/* Verification Legal File Attachments — with Add / Delete actions (Task 1) */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
            <h4 className="text-sm font-bold text-white uppercase tracking-wider mb-4 flex items-center gap-2">
              <FileText size={16} className="text-blue-500" /> Deeds & Verification Vault
            </h4>

            {/* Success / error banners */}
            {verificationDocSuccess && (
              <div className="p-2.5 mb-3 rounded-xl text-xs bg-emerald-950/40 border border-emerald-900 text-emerald-300">
                {verificationDocSuccess}
              </div>
            )}
            {verificationDocError && (
              <div className="p-2.5 mb-3 rounded-xl text-xs bg-red-950/40 border border-red-900 text-red-300">
                {verificationDocError}
              </div>
            )}

            {/* Add-document upload form — gated by properties:edit so tenants/owners can't mutate deeds files */}
            <Can permission="properties:edit">
              <form onSubmit={uploadVerificationDocument} className="mb-4 p-3.5 bg-slate-950/60 border border-slate-800 rounded-xl space-y-3">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                  <div>
                    <label className="block text-[10px] uppercase text-slate-500 font-bold mb-1">Document name</label>
                    <input
                      required
                      type="text"
                      value={verificationDocForm.name}
                      onChange={e => setVerificationDocForm({ ...verificationDocForm, name: e.target.value })}
                      placeholder="e.g. Title Deed - Erf 123"
                      className="w-full px-3 py-2 bg-slate-900 border border-slate-700 rounded-lg text-xs text-white focus:outline-none focus:border-blue-500"
                    />
                  </div>
                  <div>
                    <label className="block text-[10px] uppercase text-slate-500 font-bold mb-1">Description</label>
                    <input
                      type="text"
                      value={verificationDocForm.description}
                      onChange={e => setVerificationDocForm({ ...verificationDocForm, description: e.target.value })}
                      placeholder="Optional note"
                      className="w-full px-3 py-2 bg-slate-900 border border-slate-700 rounded-lg text-xs text-white focus:outline-none focus:border-blue-500"
                    />
                  </div>
                </div>
                <label className="block text-[10px] uppercase text-slate-500 font-bold mb-1">
                  Actual document
                  <input
                    required
                    type="file"
                    accept=".pdf,.png,.jpg,.jpeg,.tif,.tiff,.gif,.doc,.docx,.txt"
                    onChange={handleVerificationFileChange}
                    className="mt-1 w-full text-xs text-slate-400 file:mr-3 file:py-2 file:px-4 file:rounded-xl file:border-0 file:text-xs file:font-bold file:bg-blue-600 file:text-white hover:file:bg-blue-700"
                  />
                  <span className="text-[10px] text-slate-500 block mt-1">
                    {verificationDocForm.file?.name || 'No file chosen'} · Max 10 MB · PDF / image / doc
                  </span>
                </label>
                <div className="flex items-center justify-between gap-3 rounded-lg border border-slate-800 bg-slate-900 px-3 py-2.5">
                  <label htmlFor="verification-password-protection" className="text-xs font-semibold text-slate-300 cursor-pointer">
                    Password Protect Document?
                  </label>
                  <input
                    id="verification-password-protection"
                    type="checkbox"
                    checked={verificationDocForm.isPasswordProtected}
                    onChange={handleVerificationPasswordToggle}
                    className="h-4 w-4 rounded border-slate-600 bg-slate-800 text-blue-600 focus:ring-blue-500"
                  />
                </div>
                {verificationDocForm.isPasswordProtected && (
                  <div className="relative">
                    <label htmlFor="verification-document-password" className="block text-[10px] uppercase text-slate-500 font-bold mb-1">
                      Document password
                    </label>
                    <input
                      id="verification-document-password"
                      required={verificationDocForm.isPasswordProtected}
                      type={showVerificationPassword ? 'text' : 'password'}
                      value={verificationDocForm.documentPassword}
                      onChange={e => setVerificationDocForm({ ...verificationDocForm, documentPassword: e.target.value })}
                      autoComplete="new-password"
                      placeholder="Enter a password to lock this file"
                      className="w-full px-3 py-2 pr-10 bg-slate-900 border border-slate-700 rounded-lg text-xs text-white focus:outline-none focus:border-blue-500"
                    />
                    <button
                      type="button"
                      onClick={handleVerificationPasswordVisibilityToggle}
                      aria-label={showVerificationPassword ? 'Hide document password' : 'Show document password'}
                      className="absolute right-2 top-[26px] text-slate-500 hover:text-slate-300"
                    >
                      {showVerificationPassword ? <EyeOff size={15} /> : <Eye size={15} />}
                    </button>
                  </div>
                )}
                <div className="flex justify-end">
                  <button
                    type="submit"
                    disabled={verificationDocUploading || verificationDocDeleting !== null}
                    className="inline-flex items-center gap-1.5 px-4 py-2 bg-blue-600 disabled:opacity-60 text-white rounded-xl text-xs font-bold"
                  >
                    <FileUp size={13} />{verificationDocUploading ? 'Uploading…' : 'Save to vault'}
                  </button>
                </div>
              </form>
            </Can>

            {/* Stored documents list — with delete actions (Task 1) */}
            <div className="space-y-2">
              {parseVerificationDocs().length === 0 ? (
                <p className="text-xs text-slate-500">No verification documents are stored for this property.</p>
              ) : (
                parseVerificationDocs().map((doc, idx) => {
                  const documentMetadata = parseVerificationDocumentMetadata()[idx] || {};
                  const isPasswordProtected = documentMetadata.isPasswordProtected === true;
                  const isDataUrl = typeof doc === 'string' && doc.startsWith('data:');
                  let displayName = documentMetadata.fileName || (typeof doc === "string" ? doc : `Verification document ${idx + 1}`);
                  if (isDataUrl && !documentMetadata.fileName) {
                    const mimeMatch = doc.match(/data:([^;]+);/);
                    const ext = mimeMatch ? mimeMatch[1].split('/').pop()?.toUpperCase() : 'FILE';
                    displayName = `Verification document ${idx + 1}.${ext}`;
                  } else if (typeof doc === 'string' && !documentMetadata.fileName) {
                    displayName = doc.split('/').pop()?.split('\\').pop() || doc;
                  }
                  return (
                    <div key={idx} className="flex items-center gap-3 p-2.5 bg-slate-950 border border-slate-800 rounded-xl text-xs text-slate-300 group hover:border-slate-700 transition-colors">
                      <FileText size={14} className="text-slate-500 group-hover:text-blue-400 transition-colors" />
                      <button type="button" onClick={() => downloadVerificationDocument(idx, displayName)} className="truncate text-left flex-1 font-medium text-blue-400 hover:text-blue-300 underline decoration-dotted underline-offset-2">
                        {displayName}
                      </button>
                      {isPasswordProtected && <FolderLock size={13} className="text-amber-400 shrink-0" title="Password protected" />}
                      <Can permission="properties:edit">
                        <button
                          type="button"
                          onClick={() => deleteVerificationDocument(idx)}
                          disabled={verificationDocUploading || verificationDocDeleting !== null}
                          title="Delete document"
                          className="p-1.5 text-slate-500 hover:text-red-400 rounded-lg hover:bg-slate-800 transition-colors cursor-pointer"
                        >
                          {verificationDocDeleting === idx ? <Loader2 size={13} className="animate-spin" /> : <Trash2 size={13} />}
                        </button>
                      </Can>
                    </div>
                  );
                })
              )}
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
<GoogleAddressPicker onSelect={address => setPropertyForm(previous => ({ ...previous, ...address }))} />
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              {[
                ['completeAddress', 'Address'], ['erfSpatialSize', 'Erf size'], ['titleDeedType', 'Title deed type'], ['portfolioZoning', 'Zoning'], ['linkedOwnerId', 'Owner record ID'], ['valuationPrice', 'Valuation'], ['salePrice', 'Sale price'], ['tenantInstallationAllowance', 'TI allowance'], ['latitude', 'Latitude'], ['longitude', 'Longitude']
              ].map(([key, label]) => <label key={key} className="text-[10px] text-slate-400 uppercase">{label}<input value={propertyForm[key] ?? ''} onChange={e => setPropertyForm({ ...propertyForm, [key]: e.target.value, ...(key === 'completeAddress' ? { latitude: '', longitude: '' } : {}) })} className="mt-1 w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-lg text-sm text-white normal-case" /></label>)}
              <div className="md:col-span-2 text-[10px] text-slate-400 uppercase">
                Listing Type
                <div className="mt-1 flex gap-4 p-3 bg-slate-950 border border-slate-700 rounded-lg normal-case">
                  <label className="flex items-center gap-2 text-sm text-white cursor-pointer">
                    <input
                      type="checkbox"
                      checked={!!propertyForm.isForSale}
                      onChange={(e) => setPropertyForm({ ...propertyForm, isForSale: e.target.checked })}
                      className="rounded border-slate-600 bg-slate-900 text-emerald-500 focus:ring-emerald-500"
                    />
                    For Sale
                  </label>
                  <label className="flex items-center gap-2 text-sm text-white cursor-pointer">
                    <input
                      type="checkbox"
                      checked={!!propertyForm.isForRent}
                      onChange={(e) => setPropertyForm({ ...propertyForm, isForRent: e.target.checked })}
                      className="rounded border-slate-600 bg-slate-900 text-blue-500 focus:ring-blue-500"
                    />
                    To Rent
                  </label>
                </div>
                <p className="text-[10px] text-slate-500 mt-1">Tick both to market the property as For Sale and To Rent.</p>
              </div>
            </div>
             <div className="flex gap-3"><button type="button" onClick={() => setIsEditingProperty(false)} className="flex-1 py-2 border border-slate-700 rounded-xl text-xs">Cancel</button><Can permission="properties:edit"><button type="submit" className="flex-1 py-2 bg-blue-600 text-white rounded-xl text-xs font-bold">Save property</button></Can></div>
          </form>
        </div>
      )}
    </div>
  );
}
