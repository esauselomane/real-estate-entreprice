import { useEffect, useState } from 'react';
import { ReceiptText, ClipboardCheck, MessageSquare, Gavel, Scale, Pencil, Loader2 } from 'lucide-react';
import { crmService } from '../services/crmService.js';

const modules = [
  { id: 'financials', label: 'Financials', icon: ReceiptText, title: 'Invoice / arrears item', contact: 'Owner / tenant', amount: 'Amount', details: 'Invoice, payment reference or arrears notes' },
  { id: 'inspections', label: 'Inspections', icon: ClipboardCheck, title: 'Inspection', contact: 'Inspector / tenant', amount: 'Cost', details: 'Condition, findings, follow-up and evidence location' },
  { id: 'portal', label: 'Tenant Portal', icon: MessageSquare, title: 'Tenant request', contact: 'Tenant', amount: 'Charge', details: 'Request, communication and response details' },
  { id: 'auctions', label: 'Auctions', icon: Gavel, title: 'Auction listing / bid', contact: 'Bidder', amount: 'Bid / reserve value', details: 'Reserve, bidder KYC, deposit and bid history' },
  { id: 'conveyancing', label: 'Conveyancing', icon: Scale, title: 'Conveyancing milestone', contact: 'Attorney / buyer', amount: 'Legal cost', details: 'OTP, bond, deeds office milestone and outstanding documents' }
];

const blank = (module) => ({ module, title: '', propertyReference: '', contactName: '', status: 'Open', dueDate: '', amount: 0, details: '' });

export default function OperationsWorkspace() {
  const [active, setActive] = useState(modules[0]);
  const [records, setRecords] = useState([]);
  const [form, setForm] = useState(blank(modules[0].id));
  const [editing, setEditing] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const load = async (module = active.id) => {
    setLoading(true); setError('');
    try { setRecords(await crmService.getOperationalRecords(module)); }
    catch (err) { setError(err || 'Could not load operational records.'); }
    finally { setLoading(false); }
  };
  useEffect(() => { load(active.id); setForm(blank(active.id)); setEditing(null); }, [active.id]);

  const selectModule = (module) => setActive(module);
  const save = async (event) => {
    event.preventDefault(); setError('');
    const payload = { ...form, module: active.id, amount: Number(form.amount || 0), dueDate: form.dueDate || null };
    try {
      if (editing) await crmService.updateOperationalRecord(editing.operationalRecordId, { ...editing, ...payload });
      else await crmService.createOperationalRecord(payload);
      setForm(blank(active.id)); setEditing(null); await load();
    } catch (err) { setError(err || 'Could not save this record.'); }
  };

  return <div className="space-y-5 animate-fade-in">
    <div><p className="text-[10px] uppercase tracking-[.2em] text-slate-500">Operations</p><h3 className="text-xl font-bold text-white">Property Management Workspace</h3></div>
    <div className="flex gap-2 overflow-x-auto pb-1">{modules.map((module) => { const Icon = module.icon; return <button key={module.id} type="button" onClick={() => selectModule(module)} className={`shrink-0 flex items-center gap-2 px-3 py-2 rounded-xl text-xs font-bold border ${active.id === module.id ? 'bg-blue-600 border-blue-500 text-white' : 'bg-slate-900 border-slate-800 text-slate-400 hover:text-white'}`}><Icon size={14} />{module.label}</button>; })}</div>
    {error && <div className="p-3 text-xs rounded-xl bg-red-950/40 border border-red-900 text-red-300">{error}</div>}
    <div className="grid xl:grid-cols-[390px_1fr] gap-6">
      <form onSubmit={save} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-3">
        <h4 className="text-sm text-white font-bold">{editing ? `Edit ${active.label}` : `Add ${active.label} record`}</h4>
        <label className="label">{active.title}<input required value={form.title} onChange={e => setForm({ ...form, title: e.target.value })} className="field" /></label>
        <label className="label">Property reference<input value={form.propertyReference} onChange={e => setForm({ ...form, propertyReference: e.target.value })} placeholder="Property ID or address" className="field" /></label>
        <label className="label">{active.contact}<input value={form.contactName} onChange={e => setForm({ ...form, contactName: e.target.value })} className="field" /></label>
        <div className="grid grid-cols-2 gap-3"><label className="label">Status<select value={form.status} onChange={e => setForm({ ...form, status: e.target.value })} className="field"><option>Open</option><option>In Progress</option><option>Awaiting Information</option><option>Completed</option><option>Cancelled</option></select></label><label className="label">Due date<input type="date" value={form.dueDate || ''} onChange={e => setForm({ ...form, dueDate: e.target.value })} className="field" /></label></div>
        <label className="label">{active.amount}<input type="number" min="0" value={form.amount ?? 0} onChange={e => setForm({ ...form, amount: e.target.value })} className="field" /></label>
        <label className="label">Details<textarea rows={4} value={form.details} onChange={e => setForm({ ...form, details: e.target.value })} placeholder={active.details} className="field" /></label>
        <div className="flex gap-3"><button type="button" onClick={() => { setEditing(null); setForm(blank(active.id)); }} className="flex-1 py-2 border border-slate-700 rounded-xl text-xs">Clear</button><button className="flex-1 py-2 bg-blue-600 text-white rounded-xl text-xs font-bold">{editing ? 'Save changes' : 'Save record'}</button></div>
      </form>
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5"><div className="flex justify-between mb-4"><h4 className="text-sm text-white font-bold">Saved {active.label} records</h4>{loading && <Loader2 className="animate-spin text-blue-500" size={16} />}</div><div className="space-y-3">{records.map((record) => <div key={record.operationalRecordId} className="p-4 rounded-xl bg-slate-950 border border-slate-800 flex justify-between gap-4"><div><div className="flex gap-2 items-center"><p className="text-sm font-bold text-white">{record.title}</p><span className="text-[10px] px-2 py-0.5 rounded bg-blue-950 text-blue-300">{record.status}</span></div><p className="text-xs text-slate-400 mt-1">{record.propertyReference || 'No property linked'} · {record.contactName || 'No contact'}</p><p className="text-xs text-slate-500 mt-2 whitespace-pre-wrap">{record.details}</p><p className="text-xs text-emerald-400 mt-2">R {Number(record.amount || 0).toLocaleString()} {record.dueDate ? `· Due ${new Date(record.dueDate).toLocaleDateString()}` : ''}</p></div><button type="button" onClick={() => { setEditing(record); setForm({ ...record, dueDate: record.dueDate ? record.dueDate.slice(0, 10) : '' }); }} className="h-fit p-2 text-slate-400 hover:text-white"><Pencil size={15} /></button></div>)}{!loading && !records.length && <p className="py-10 text-center text-xs text-slate-500">No {active.label.toLowerCase()} records have been saved.</p>}</div></div>
    </div>
  </div>;
}
