import React, { useEffect, useState } from 'react';
import { Users, Plus, UserCheck, Building2, Search, Pencil } from 'lucide-react';
import { crmService } from '../services/ crmService.js';

export default function OwnersTenantsWorkspace() {
  const [records, setRecords] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [query, setQuery] = useState('');
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({
    recordType: 'Owner',
    fullName: '',
    companyName: '',
    emailAddress: '',
    phoneNumber: '',
    primaryLocation: '',
    budgetLimit: 0,
    notes: ''
  });

  const load = async () => {
    setIsLoading(true);
    setError('');
    try {
      const data = await crmService.getOwnerTenantRecords();
      setRecords(data || []);
    } catch (err) {
      setError(err || 'Unable to load owner/tenant records.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const filtered = records.filter((row) => {
    const haystack = `${row?.fullName || ''} ${row?.companyName || ''} ${row?.emailAddress || ''} ${row?.primaryLocation || ''}`.toLowerCase();
    return haystack.includes((query || '').toLowerCase());
  });

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      if (editing) {
        await crmService.updateOwnerTenantRecord({ ...editing, ...form });
      } else {
        await crmService.createOwnerTenantRecord(form);
      }
      setForm({ recordType: 'Owner', fullName: '', companyName: '', emailAddress: '', phoneNumber: '', primaryLocation: '', budgetLimit: 0, notes: '' });
      setEditing(null);
      await load();
    } catch (err) {
      alert(err || 'Save failed');
    }
  };

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="grid gap-6 xl:grid-cols-[400px_1fr]">
        <form onSubmit={handleSubmit} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-4">
          <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px]">
            <Plus className="text-blue-500" size={14} /> {editing ? 'Edit' : 'Add'} Owner/Tenant
          </div>

          <div className="grid grid-cols-2 gap-3">
            <select value={form.recordType} onChange={(e) => setForm({ ...form, recordType: e.target.value })} className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white">
              <option value="Owner">Owner</option>
              <option value="Tenant">Tenant</option>
            </select>
            <input value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} placeholder="Full name" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
            <input value={form.companyName} onChange={(e) => setForm({ ...form, companyName: e.target.value })} placeholder="Company name" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
            <input value={form.emailAddress} type="email" onChange={(e) => setForm({ ...form, emailAddress: e.target.value })} placeholder="Email address" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
            <input value={form.phoneNumber} onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })} placeholder="Phone" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
            <input value={form.primaryLocation} onChange={(e) => setForm({ ...form, primaryLocation: e.target.value })} placeholder="Primary location" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
            <input value={form.budgetLimit} type="number" onChange={(e) => setForm({ ...form, budgetLimit: Number(e.target.value || 0) })} placeholder="Budget limit" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
            <textarea value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} placeholder="Notes" rows={3} className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
          </div>

          <div className="flex gap-3">
            <button type="button" onClick={() => { setEditing(null); setForm({ recordType: 'Owner', fullName: '', companyName: '', emailAddress: '', phoneNumber: '', primaryLocation: '', budgetLimit: 0, notes: '' }); }} className="flex-1 py-2 rounded-xl border border-slate-700 text-slate-300 text-xs font-bold">Clear</button>
            <button type="submit" className="flex-1 py-2 rounded-xl bg-blue-600 text-white text-xs font-bold">Save</button>
          </div>
        </form>

        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
          <div className="flex items-center justify-between gap-3 mb-4">
            <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px]"><Users size={14} className="text-blue-500" /> Registry</div>
            <div className="flex items-center gap-2 bg-slate-950 border border-slate-700 rounded-xl px-2 py-1.5">
              <Search size={12} className="text-slate-500" />
              <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search" className="bg-transparent text-xs text-white outline-none w-28" />
            </div>
          </div>

          {error && <div className="p-2 rounded-lg border border-red-900 bg-red-950/40 text-red-300 text-xs mb-3">{error}</div>}
          {isLoading ? <div className="py-8 text-center text-slate-400 text-sm">Loading records...</div> : (
            <div className="space-y-3">
              {filtered.map((record) => (
                <div key={record.ownerTenantId} className="rounded-xl border border-slate-800 bg-slate-950/60 p-4 flex items-center justify-between gap-4">
                  <div>
                    <div className="flex items-center gap-2">
                      <span className={`rounded-md border px-2 py-1 text-[10px] font-bold ${record.recordType === 'Owner' ? 'border-amber-900 bg-amber-950/40 text-amber-300' : 'border-blue-900 bg-blue-950/40 text-blue-300'}`}>{record.recordType}</span>
                      <h4 className="text-white font-bold">{record.fullName}</h4>
                    </div>
                    <p className="text-xs text-slate-400 mt-1">{record.companyName || 'Individual'} • {record.emailAddress || 'No email'} • {record.phoneNumber || 'No phone'}</p>
                    <p className="text-[11px] text-slate-500 mt-1">{record.primaryLocation || 'Location not provided'} • Budget limit: R {Number(record.budgetLimit || 0).toLocaleString()}</p>
                  </div>
                  <button type="button" onClick={() => { setEditing(record); setForm({ ...record }); }} className="p-2 rounded-lg border border-slate-700 text-slate-300 hover:text-white hover:border-slate-600"><Pencil size={14} /></button>
                </div>
              ))}
              {!filtered.length && !isLoading && <div className="py-10 text-center text-slate-500 text-xs">No matching owner or tenant records.</div>}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
