import { useEffect, useState } from 'react';
import apiClient from '../services/apiClient.js';

export default function DeveloperApiKeys({ agencyId }) {
  const [keys, setKeys] = useState([]);
  const [name, setName] = useState('Public property website');
  const [newKey, setNewKey] = useState('');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  useEffect(function loadKeys() {
    if (!agencyId || agencyId === '__all__') return;
    const controller = new AbortController();
    async function load() {
      try { const response = await apiClient.get('/developer/keys', { params: { agencyId }, signal: controller.signal }); setKeys(response.data); }
      catch (error) { if (!controller.signal.aborted) setMessage(error.message); }
    }
    load();
    return () => controller.abort();
  }, [agencyId]);
  async function refresh() {
    const response = await apiClient.get('/developer/keys', { params: { agencyId } }); setKeys(response.data);
  }
  async function generate(event) {
    event.preventDefault(); setBusy(true); setMessage(''); setNewKey('');
    try {
      const response = await apiClient.post('/developer/keys/generate', { agencyId, name });
      setNewKey(response.data.apiKey); await refresh();
    } catch (error) { setMessage(error.message); }
    finally { setBusy(false); }
  }
  async function revoke(event) {
    const id = event.currentTarget.dataset.keyId; setBusy(true); setMessage('');
    try { await apiClient.post(`/developer/keys/${id}/revoke`); setNewKey(''); await refresh(); }
    catch (error) { setMessage(error.message); }
    finally { setBusy(false); }
  }
  if (!agencyId || agencyId === '__all__') return <p className="text-sm text-slate-400">Select one agency to manage its website API keys.</p>;
  return <section className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-3">
    <h3 className="font-semibold text-white">Developer API · Public website</h3>
    <p className="text-xs text-slate-400">Agency: {agencyId}. Install the key in the website server’s environment. Keys grant public listing access and owner submissions for this agency.</p>
    <form onSubmit={generate} className="flex flex-wrap gap-2"><input aria-label="API key name" required maxLength={100} value={name} onChange={event => setName(event.target.value)} className="p-2 bg-slate-950 text-white border border-slate-700 rounded" /><button disabled={busy} className="bg-blue-600 text-white rounded px-4 py-2">Generate key</button></form>
    {newKey && <div className="p-3 border border-amber-500 rounded"><p className="text-sm text-amber-300">Copy now. This key is shown once.</p><code className="text-sm text-white break-all select-all">{newKey}</code><button type="button" onClick={() => setNewKey('')} className="block text-sm text-blue-300 mt-2">Hide key</button></div>}
    <ul className="space-y-2">{keys.map(key => <li key={key.id} className="flex gap-3 text-sm text-slate-300"><span className="flex-1">{key.name} · {key.keyPrefix}… · {key.isRevoked ? 'Revoked' : 'Active'}</span>{!key.isRevoked && <button disabled={busy} type="button" data-key-id={key.id} onClick={revoke} className="text-red-400">Revoke</button>}</li>)}</ul>
    <p role="status" className="text-sm text-blue-300">{message}</p>
  </section>;
}
