import { useEffect, useState } from 'react';
import apiClient from '../../services/apiClient.js';

const blank = { code: '', name: '', description: '', monthlyPrice: '', annualPrice: '', features: '', sortOrder: 0, isActive: true };

export default function SubscriptionPackagesAdmin() {
  const [packages, setPackages] = useState([]);
  const [form, setForm] = useState(blank);
  const [editingId, setEditingId] = useState(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    apiClient.get('/system/pricing-packages').then(response => setPackages(response.data)).catch(error => setError(error.message));
  }, []);
  function change(event) {
    const { name, value, checked, type } = event.target;
    setForm(previous => ({ ...previous, [name]: type === 'checkbox' ? checked : value }));
  }
  function edit(packageItem) {
    setEditingId(packageItem.id);
    setForm({ ...packageItem, monthlyPrice: packageItem.monthlyPrice ?? '', annualPrice: packageItem.annualPrice ?? '', features: packageItem.features.join('\n') });
  }
  async function save(event) {
    event.preventDefault(); setBusy(true); setError('');
    const payload = { ...form, code: form.code.trim().toUpperCase(), monthlyPrice: form.monthlyPrice === '' ? null : Number(form.monthlyPrice), annualPrice: form.annualPrice === '' ? null : Number(form.annualPrice), sortOrder: Number(form.sortOrder), features: form.features.split('\n').map(value => value.trim()).filter(Boolean) };
    try {
      if (editingId) await apiClient.put(`/system/pricing-packages/${editingId}`, payload);
      else await apiClient.post('/system/pricing-packages', payload);
      const response = await apiClient.get('/system/pricing-packages');
      setPackages(response.data); setEditingId(null); setForm(blank);
    } catch (error) { setError(error.message); }
    finally { setBusy(false); }
  }
  return <section className="space-y-5 text-slate-100">
    <h2 className="text-xl font-semibold">Subscription packages</h2>
    <p className="text-sm text-slate-400">Changes apply to new checkouts. Existing subscriptions keep their agreed price. Deactivate a package to remove it from sale.</p>
    {error && <p role="alert" className="text-red-300">{error}</p>}
    <div className="flex flex-wrap gap-3">{packages.map(item => <button key={item.id} onClick={() => edit(item)} className="border border-slate-700 rounded-xl p-3">{item.name} · {item.isActive ? 'Active' : 'Inactive'}</button>)}</div>
    <form onSubmit={save} className="grid md:grid-cols-2 gap-4 max-w-3xl">
      {['code', 'name', 'description', 'monthlyPrice', 'annualPrice', 'sortOrder'].map(name => <label key={name} className="flex flex-col gap-1">{({ code: 'Code', name: 'Name', description: 'Description', monthlyPrice: 'Monthly price (ZAR)', annualPrice: 'Annual price (ZAR)', sortOrder: 'Display order' })[name]}
        <input name={name} value={form[name] ?? ''} onChange={change} disabled={busy || (name === 'code' && !!editingId)} required={['code', 'name'].includes(name)} type={['monthlyPrice', 'annualPrice', 'sortOrder'].includes(name) ? 'number' : 'text'} min={name === 'sortOrder' ? undefined : 0} step={name === 'sortOrder' ? 1 : '0.01'} className="bg-slate-900 border border-slate-700 rounded p-2" />
      </label>)}
      <label className="md:col-span-2">Features (one per line)<textarea name="features" value={form.features} onChange={change} rows={5} className="block w-full bg-slate-900 border border-slate-700 rounded p-2" /></label>
      <label><input type="checkbox" name="isActive" checked={form.isActive} onChange={change} /> Available for new subscriptions</label>
      <div className="flex gap-4"><button disabled={busy} className="bg-blue-600 rounded px-4 py-2">{busy ? 'Saving…' : editingId ? 'Save changes' : 'Create package'}</button><button type="button" onClick={() => { setEditingId(null); setForm(blank); }}>New package</button></div>
    </form>
  </section>;
}
