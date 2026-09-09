import { useEffect, useState } from 'react';
import { Users, Plus, Search, Pencil, X, Tag, MapPin } from 'lucide-react';
import { crmService } from '../services/crmService.js';
import { usePermission } from '../contexts/PermissionContext.jsx';
import { useTenant } from '../contexts/TenantContext.jsx';
import Can from '../components/Can.jsx';
import Pagination from '../components/Pagination.jsx';

const PROPERTY_TYPES = ['Residential', 'Commercial', 'Retail', 'Industrial'];
const OT_PAGE_SIZE = 20;

export default function OwnersTenantsWorkspace() {
  const { hasPermission } = usePermission();
  const { activeTenantId } = useTenant();
  const [records, setRecords] = useState([]);
  const [recordsTotal, setRecordsTotal] = useState(0);
  const [recordsPage, setRecordsPage] = useState(1);
  const [geography, setGeography] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [query, setQuery] = useState('');
  const [editing, setEditing] = useState(null);
  const [areaSearch, setAreaSearch] = useState('');
  const [filters, setFilters] = useState({ contactType: 'Any', budgetMin: '', budgetMax: '', areaIds: [] });
  const [filterAreaSearch, setFilterAreaSearch] = useState('');
  const [showModal, setShowModal] = useState(false);

  const CONTACT_TYPES = ['Any', 'Owner', 'Tenant', 'Broker', 'Buyer', 'Seller', 'Staff', 'Manager'];

  const [form, setForm] = useState({
    contactType: 'Owner',
    fullName: '',
    companyName: '',
    emailAddress: '',
    phoneNumber: '',
    primaryLocation: '',
    budgetLimit: 0,
    propertyTypeInterests: [],
    areaInterestIds: [],
    notes: ''
  });

  const load = async (pageToLoad = 1, filterOverrides = null) => {
    setIsLoading(true);
    setError('');
    try {
      const activeFilters = filterOverrides !== null ? filterOverrides : filters;
      const [data, geo] = await Promise.all([
        activeFilters.contactType === 'Any' && !activeFilters.budgetMin && !activeFilters.budgetMax && !activeFilters.areaIds?.length
          ? crmService.getOwnerTenantRecords(pageToLoad)
          : crmService.postOwnerTenantRecordsFiltered(activeFilters),
        crmService.searchGeography('')
      ]);
      setRecords(data || []);
      setRecordsTotal(Number(data?.totalCount || 0));
      setRecordsPage(pageToLoad);
      setGeography(geo || []);
    } catch (err) {
      setError(err || 'Unable to load owner/tenant records.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => { load(1); }, [activeTenantId]);

  const handleOTPageChange = (newPage) => {
    if (newPage < 1) return;
    const maxPage = Math.max(1, Math.ceil(recordsTotal / OT_PAGE_SIZE));
    if (newPage > maxPage) return;
    load(newPage);
  };

  const handleSearch = () => {
    load(1);
  };

  const handleFilterChange = (field, value) => {
    setFilters(prev => ({ ...prev, [field]: value }));
  };

  const toggleFilterArea = (geo) => {
    const id = geo.geographyId;
    const exists = filters.areaIds.includes(id);
    const next = exists
      ? filters.areaIds.filter((a) => a !== id)
      : [...filters.areaIds, id];
    handleFilterChange('areaIds', next);
    setFilterAreaSearch('');
  };

  const removeFilterArea = (id) => {
    handleFilterChange('areaIds', filters.areaIds.filter((a) => a !== id));
  };

  const toggleType = (type) => {
    const next = form.propertyTypeInterests.includes(type)
      ? form.propertyTypeInterests.filter((t) => t !== type)
      : [...form.propertyTypeInterests, type];
    setForm({ ...form, propertyTypeInterests: next });
  };

  const addArea = (geo) => {
    if (!form.areaInterestIds.includes(geo.geographyId)) {
      setForm({ ...form, areaInterestIds: [...form.areaInterestIds, geo.geographyId] });
    }
    setAreaSearch('');
  };

  const removeArea = (id) => setForm({ ...form, areaInterestIds: form.areaInterestIds.filter((a) => a !== id) });

  const areaLabel = (id) => {
    const g = geography.find((x) => x.geographyId === id);
    return g ? `${g.province || g.country}, ${g.city}${g.suburb ? ' (' + g.suburb + ')' : ''}` : '';
  };

  const filtered = records.filter((row) => {
    const haystack = `${row?.fullName || ''} ${row?.companyName || ''} ${row?.emailAddress || ''} ${row?.primaryLocation || ''}`.toLowerCase();
    return haystack.includes((query || '').toLowerCase());
  });

  const parseInterests = (val) => {
    if (Array.isArray(val)) return val;
    if (!val) return [];
    try { return JSON.parse(val); } catch { return String(val).split(',').map((s) => s.trim()).filter(Boolean); }
  };
  const parseAreaIds = (val) => {
    if (Array.isArray(val)) return val.map(Number);
    if (!val) return [];
    try { return JSON.parse(val).map(Number); } catch { return String(val).split(',').map((s) => s.trim()).filter(Boolean).map(Number); }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      if (editing) {
        await crmService.updateOwnerTenantRecord({ ...editing, ...form });
      } else {
        await crmService.createOwnerTenantRecord(form);
      }
      setForm({
        contactType: 'Owner', fullName: '', companyName: '', emailAddress: '', phoneNumber: '',
        primaryLocation: '', budgetLimit: 0, propertyTypeInterests: [], areaInterestIds: [], notes: ''
      });
      setEditing(null);
      setShowModal(false);
      await load(recordsPage);
    } catch (err) {
      alert(err || 'Save failed');
    }
  };

  const openModal = (record = null) => {
    setEditing(record);
    setForm(record
      ? {
          contactType: record.contactType || 'Owner',
          fullName: record.fullName || '',
          companyName: record.companyName || '',
          emailAddress: record.emailAddress || '',
          phoneNumber: record.phoneNumber || '',
          primaryLocation: record.primaryLocation || '',
          budgetLimit: Number(record.budgetLimit || 0),
          propertyTypeInterests: parseInterests(record.propertyTypeInterests),
          areaInterestIds: parseAreaIds(record.areaInterestIds),
          notes: record.notes || ''
        }
      : {
          contactType: 'Owner', fullName: '', companyName: '', emailAddress: '', phoneNumber: '',
          primaryLocation: '', budgetLimit: 0, propertyTypeInterests: [], areaInterestIds: [], notes: ''
        });
    setShowModal(true);
  };

  const closeModal = () => {
    setEditing(null);
    setForm({
      contactType: 'Owner', fullName: '', companyName: '', emailAddress: '', phoneNumber: '',
      primaryLocation: '', budgetLimit: 0, propertyTypeInterests: [], areaInterestIds: [], notes: ''
    });
    setShowModal(false);
  };

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex justify-between items-center">
        <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px]"><Users size={14} className="text-blue-500" /> Registry</div>
        <Can permission="users:create">
          <button type="button" onClick={() => openModal()} className="flex items-center gap-1 bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold px-3 py-1.5 rounded-xl">
            <Plus size={12} /> Add Contact
          </button>
        </Can>
      </div>

      {/* 🎯 Filter bar */}
      <div className="flex flex-wrap gap-3 mb-4">
        <select
          value={filters.contactType}
          onChange={(e) => handleFilterChange('contactType', e.target.value)}
          className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-1.5 text-xs text-white"
        >
          {CONTACT_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
        </select>
        <input
          type="number"
          min="0"
          placeholder="Min budget"
          value={filters.budgetMin}
          onChange={(e) => handleFilterChange('budgetMin', e.target.value)}
          className="w-28 bg-slate-950 border border-slate-700 rounded-xl px-3 py-1.5 text-xs text-white"
        />
        <input
          type="number"
          min="0"
          placeholder="Max budget"
          value={filters.budgetMax}
          onChange={(e) => handleFilterChange('budgetMax', e.target.value)}
          className="w-28 bg-slate-950 border border-slate-700 rounded-xl px-3 py-1.5 text-xs text-white"
        />
        {/* 🌍 Area autocomplete (multi-select) */}
        <div className="relative w-48">
          <MapPin size={12} className="absolute left-2 top-1/2 -translate-y-1/2 text-slate-500" />
          <input
            type="text"
            placeholder="Search area..."
            value={filterAreaSearch}
            onChange={(e) => setFilterAreaSearch(e.target.value)}
            onFocus={() => setFilterAreaSearch('')}
            className="w-full pl-7 pr-3 py-1.5 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white placeholder-slate-500"
          />
          {filterAreaSearch && (
            <div className="absolute top-full mt-1 w-full max-h-36 overflow-y-auto bg-slate-950 border border-slate-700 rounded-xl py-1 shadow-xl z-10">
              {geography.filter((g) => {
                const hay = `${g.country || ''} ${g.province || ''} ${g.city || ''} ${g.suburb || ''}`.toLowerCase();
                return hay.includes(filterAreaSearch.toLowerCase());
              }).slice(0, 15).map((g) => (
                <div
                  key={g.geographyId}
                  onClick={() => toggleFilterArea(g)}
                  className="px-3 py-1.5 text-xs text-slate-300 hover:bg-slate-800 cursor-pointer"
                >
                  {g.province}, {g.city}{g.suburb ? ` (${g.suburb})` : ''}
                </div>
              ))}
            </div>
          )}
        </div>
        <button
          type="button"
          onClick={handleSearch}
          className="bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold px-4 py-1.5 rounded-xl"
        >
          Search
        </button>
        <button
          type="button"
          onClick={() => { setFilters({ contactType: 'Any', budgetMin: '', budgetMax: '', areaIds: [] }); setFilterAreaSearch(''); load(1); }}
          className="border border-slate-700 text-slate-300 hover:text-white text-xs font-bold px-4 py-1.5 rounded-xl"
        >
          Clear
        </button>
      </div>

      {/* Selected filter areas */}
      {filters.areaIds.length > 0 && (
        <div className="flex flex-wrap gap-1.5 mb-2">
          {filters.areaIds.map((id) => (
            <span key={id} className="inline-flex items-center gap-1 text-[10px] px-2 py-1 rounded-md bg-slate-800 border border-slate-700 text-slate-300">
              <MapPin size={10} className="text-blue-400" />
              {areaLabel(id)}
              <button type="button" onClick={() => removeFilterArea(id)} className="hover:text-white"><X size={10} /></button>
            </span>
          ))}
        </div>
      )}

      {error && <div className="p-2 rounded-lg border border-red-900 bg-red-950/40 text-red-300 text-xs mb-3">{error}</div>}
      {isLoading ? <div className="py-8 text-center text-slate-400 text-sm">Loading records...</div> : (
        <div className="space-y-3">
          {filtered.map((record) => (
            <div key={record.contactId} className="rounded-xl border border-slate-800 bg-slate-950/60 p-4 flex items-center justify-between gap-4">
              <div>
                <div className="flex items-center gap-2 flex-wrap">
                  <span className={`rounded-md border px-2 py-1 text-[10px] font-bold ${record.contactType === 'Owner' ? 'border-amber-900 bg-amber-950/40 text-amber-300' : record.contactType === 'Tenant' ? 'border-blue-900 bg-blue-950/40 text-blue-300' : 'border-slate-700 bg-slate-800 text-slate-300'}`}>{record.contactType}</span>
                  <h4 className="text-white font-bold">{record.fullName}</h4>
                </div>
                <p className="text-xs text-slate-400 mt-1">{record.companyName || 'Individual'} • {record.emailAddress || 'No email'} • {record.phoneNumber || 'No phone'}</p>
                <p className="text-[11px] text-slate-500 mt-1">{record.primaryLocation || 'Location not provided'} • Budget: R {Number(record.budgetLimit || 0).toLocaleString()}</p>
                {parseInterests(record.propertyTypeInterests).length > 0 && (
                  <div className="flex flex-wrap gap-1 mt-1.5">
                    {parseInterests(record.propertyTypeInterests).map((t) => (
                      <Tag key={t} size={10} className="text-[9px]" />
                    ))}
                    <span className="text-[10px] text-slate-400">{parseInterests(record.propertyTypeInterests).join(', ')}</span>
                  </div>
                )}
                {parseAreaIds(record.areaInterestIds).length > 0 && (
                  <div className="flex flex-wrap gap-1 mt-1">
                    {parseAreaIds(record.areaInterestIds).map((id) => (
                      <span key={id} className="text-[10px] text-slate-400 bg-slate-800/60 px-1.5 py-0.5 rounded">{areaLabel(id)}</span>
                    ))}
                  </div>
                )}
              </div>
              <Can permission="users:edit">
                <button type="button"
                  onClick={() => openModal(record)}
                  className="p-2 rounded-lg border border-slate-700 text-slate-300 hover:text-white hover:border-slate-600">
                  <Pencil size={14} />
                </button>
              </Can>
            </div>
          ))}
          {!filtered.length && !isLoading && (
            <div className="py-10 text-center text-slate-500 text-xs">No matching owner or tenant records.</div>
          )}
        </div>
      )}

      <Pagination
        page={recordsPage}
        pageSize={OT_PAGE_SIZE}
        totalCount={recordsTotal}
        onPageChange={handleOTPageChange}
        className="pt-2"
      />

      {/* ✕ Add / Edit Contact Modal */}
      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 w-full max-w-2xl max-h-[85vh] overflow-y-auto shadow-2xl">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-white font-bold uppercase tracking-[0.1em] text-[11px] flex items-center gap-2">
                <Plus size={14} className="text-blue-500" /> {editing ? 'Edit' : 'Add'} Owner/Tenant
              </h3>
              <button
                type="button"
                onClick={closeModal}
                className="text-slate-400 hover:text-white"
              >
                <X size={16} />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="grid grid-cols-2 gap-3">
              <select value={form.contactType} onChange={(e) => setForm({ ...form, contactType: e.target.value })} className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white">
                <option value="Owner">Owner</option>
                <option value="Tenant">Tenant</option>
              </select>
              <input value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} placeholder="Full name" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
              <input value={form.companyName} onChange={(e) => setForm({ ...form, companyName: e.target.value })} placeholder="Company name" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
              <input value={form.emailAddress} type="email" onChange={(e) => setForm({ ...form, emailAddress: e.target.value })} placeholder="Email" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
              <input value={form.phoneNumber} onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })} placeholder="Phone" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
              <input value={form.primaryLocation} onChange={(e) => setForm({ ...form, primaryLocation: e.target.value })} placeholder="Primary location" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
              <input value={form.budgetLimit} type="number" aria-label="Budget limit" min="0" onChange={(e) => setForm({ ...form, budgetLimit: Number(e.target.value || 0) })} placeholder="Budget limit (R)" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />

              {/* 📋 Property-type interests (multi-select) */}
              <div className="col-span-2">
                <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-1.5">Property type interests</label>
                <div className="flex flex-wrap gap-1.5">
                  {PROPERTY_TYPES.map((t) => {
                    const on = form.propertyTypeInterests.includes(t);
                    return (
                      <button type="button" key={t} onClick={() => toggleType(t)}
                        className={`text-[10px] font-bold px-2.5 py-1.5 rounded-lg border transition-colors cursor-pointer ${
                          on ? 'bg-blue-600 text-white border-blue-500' : 'bg-slate-950 text-slate-400 border-slate-700 hover:text-white'
                        }`}>
                        {t}
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* 🌍 Area interest auto-fill (geography lookup) */}
              <div className="col-span-2">
                <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-1.5">Area interests</label>
                <div className="relative">
                  <MapPin size={12} className="absolute left-2 top-1/2 -translate-y-1/2 text-slate-500" />
                  <input
                    value={areaSearch}
                    onChange={(e) => setAreaSearch(e.target.value)}
                    placeholder="Type to search locations..."
                    className="w-full pl-7 pr-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500"
                  />
                </div>
                {areaSearch.trim() && (
                  <div className="mt-1 max-h-36 overflow-y-auto bg-slate-950 border border-slate-700 rounded-xl py-1 shadow-xl">
                    {geography.filter((g) => {
                      const hay = `${g.country || ''} ${g.province || ''} ${g.city || ''} ${g.suburb || ''}`.toLowerCase();
                      return hay.includes(areaSearch.toLowerCase()) && !form.areaInterestIds.includes(g.geographyId);
                    }).map((g) => (
                      <div key={g.geographyId}
                        onClick={() => addArea(g)}
                        className="px-3 py-1.5 text-xs text-slate-300 hover:bg-slate-800 cursor-pointer">
                        {g.province}, {g.city}{g.suburb ? ` (${g.suburb})` : ''}
                      </div>
                    ))}
                  </div>
                )}
                {form.areaInterestIds.length > 0 && (
                  <div className="mt-2 flex flex-wrap gap-1.5">
                    {form.areaInterestIds.map((id) => (
                      <span key={id} className="inline-flex items-center gap-1 text-[10px] px-2 py-1 rounded-md bg-slate-800 border border-slate-700 text-slate-300">
                        <MapPin size={10} className="text-blue-400" />
                        {areaLabel(id)}
                        <button type="button" onClick={() => removeArea(id)} className="hover:text-white"><X size={10} /></button>
                      </span>
                    ))}
                  </div>
                )}
              </div>

              <textarea value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} placeholder="Notes" rows={3} className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />

              <div className="col-span-2 flex gap-3">
                <button type="button" onClick={closeModal} className="flex-1 py-2 rounded-xl border border-slate-700 text-slate-300 text-xs font-bold">Cancel</button>
                <Can permission={editing ? 'users:edit' : 'users:create'}>
                  <button type="submit" className="flex-1 py-2 rounded-xl bg-blue-600 text-white text-xs font-bold">Save</button>
                </Can>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
