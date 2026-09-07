import { useEffect, useState } from 'react';
import { ArrowLeft, User, Phone, Mail, Banknote, MapPin, Pencil, Save, Loader2, AlertCircle } from 'lucide-react';
import { crmService } from '../services/crmService.js';

const emptyRecord = { recordType: 'Owner', fullName: '', companyName: '', emailAddress: '', phoneNumber: '', primaryLocation: '', budgetLimit: 0, notes: '' };

export default function ClientDetails({ clientId, onBack }) {
  const [record, setRecord] = useState(null);
  const [form, setForm] = useState(emptyRecord);
  const [editing, setEditing] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    const load = async () => {
      setLoading(true); setError('');
      try {
        const id = Number(String(clientId || '').replace(/\D/g, ''));
        if (!id) throw new Error('This link does not reference an owner or tenant record.');
        const data = await crmService.getOwnerTenantRecord(id);
        setRecord(data); setForm(data);
      } catch (err) { setError(err || 'Client record could not be loaded.'); }
      finally { setLoading(false); }
    };
    load();
  }, [clientId]);

  const save = async (event) => {
    event.preventDefault(); setSaving(true); setError('');
    try {
      const saved = await crmService.updateOwnerTenantRecord({ ...record, ...form, budgetLimit: Number(form.budgetLimit || 0) });
      setRecord(saved); setForm(saved); setEditing(false);
    } catch (err) { setError(err || 'Client changes could not be saved.'); }
    finally { setSaving(false); }
  };

  if (loading) return <div className="py-20 text-center text-slate-400"><Loader2 className="inline animate-spin mr-2" size={16} />Loading client record…</div>;

  return <div className="space-y-6 animate-fade-in">
    <div className="flex justify-between items-center gap-3">
      <button onClick={onBack} className="inline-flex items-center gap-2 text-xs font-semibold text-slate-400 hover:text-white bg-slate-900 border border-slate-800 px-4 py-2 rounded-xl"><ArrowLeft size={14} /> Back to Asset Details</button>
      {record && !editing && <button onClick={() => setEditing(true)} className="inline-flex items-center gap-2 text-xs font-bold bg-blue-600 text-white px-4 py-2 rounded-xl"><Pencil size={14} /> Edit client</button>}
    </div>
    {error && <div className="p-3 rounded-xl bg-red-950/40 border border-red-900 text-red-300 text-xs flex gap-2"><AlertCircle size={15} />{error}</div>}
    {!record ? <div className="p-8 bg-slate-900 border border-slate-800 rounded-2xl text-slate-400 text-sm">No saved owner or tenant profile is linked to this property.</div> : editing ? (
      <form onSubmit={save} className="bg-slate-900 border border-slate-800 rounded-2xl p-6 space-y-4">
        <h3 className="text-white font-bold">Edit {record.recordType} profile</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          <input required value={form.fullName || ''} onChange={e => setForm({ ...form, fullName: e.target.value })} placeholder="Full name" className="field" />
          <input value={form.companyName || ''} onChange={e => setForm({ ...form, companyName: e.target.value })} placeholder="Company name" className="field" />
          <input type="email" value={form.emailAddress || ''} onChange={e => setForm({ ...form, emailAddress: e.target.value })} placeholder="Email" className="field" />
          <input value={form.phoneNumber || ''} onChange={e => setForm({ ...form, phoneNumber: e.target.value })} placeholder="Phone" className="field" />
          <input value={form.primaryLocation || ''} onChange={e => setForm({ ...form, primaryLocation: e.target.value })} placeholder="Location" className="field" />
          <input type="number" aria-label="Budget limit" value={form.budgetLimit ?? 0} onChange={e => setForm({ ...form, budgetLimit: e.target.value })} placeholder="Budget" className="field" />
          <textarea value={form.notes || ''} onChange={e => setForm({ ...form, notes: e.target.value })} placeholder="Notes" className="field md:col-span-2" rows={3} />
        </div>
        <div className="flex gap-3"><button type="button" onClick={() => { setForm(record); setEditing(false); }} className="flex-1 py-2 border border-slate-700 rounded-xl text-xs">Cancel</button><button disabled={saving} className="flex-1 py-2 bg-blue-600 rounded-xl text-xs font-bold text-white">{saving ? 'Saving…' : <><Save size={13} className="inline mr-1" />Save changes</>}</button></div>
      </form>
    ) : <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-6">
      <div className="flex items-center gap-4"><div className="w-14 h-14 rounded-full bg-blue-600 flex items-center justify-center"><User /></div><div><h3 className="text-xl font-bold text-white">{record.fullName}</h3><span className="text-[10px] uppercase text-blue-300">{record.recordType}</span></div></div>
      <div className="grid md:grid-cols-2 gap-5 text-sm text-slate-300 border-t border-slate-800 pt-5"><p><Mail size={14} className="inline mr-2 text-slate-500" />{record.emailAddress || 'No email saved'}</p><p><Phone size={14} className="inline mr-2 text-slate-500" />{record.phoneNumber || 'No phone saved'}</p><p><MapPin size={14} className="inline mr-2 text-slate-500" />{record.primaryLocation || 'No location saved'}</p><p><Banknote size={14} className="inline mr-2 text-emerald-500" />R {Number(record.budgetLimit || 0).toLocaleString()}</p></div>
      {record.notes && <p className="text-xs text-slate-400 bg-slate-950 p-3 rounded-xl">{record.notes}</p>}
    </div>}
  </div>;
}
