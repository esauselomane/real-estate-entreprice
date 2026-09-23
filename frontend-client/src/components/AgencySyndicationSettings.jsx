import { useEffect, useState } from 'react';
import apiClient from '../services/apiClient.js';
import { usePermission } from '../contexts/PermissionContext.jsx';

export default function AgencySyndicationSettings({ tenantId }) {
  const [data, setData] = useState(null);
  const [error, setError] = useState('');
  const [retryCount, setRetryCount] = useState(0);
  const [environment, setEnvironment] = useState('Sandbox');
  const { hasPermission } = usePermission();
  useEffect(() => {
    if (!tenantId || tenantId === '__all__') return;
    const controller = new AbortController();
    apiClient.get(`/properties/syndication/agencies/${encodeURIComponent(tenantId)}`, { signal: controller.signal })
      .then(response => { if (!controller.signal.aborted) { setData(response.data); setError(''); } })
      .catch(failure => { if (!controller.signal.aborted) setError(failure.response?.data?.message || failure.message || 'Could not load listing platforms.'); });
    return () => controller.abort();
  }, [tenantId, retryCount]);
  return <section className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-4">
    <div className="flex justify-between items-center gap-3">
      <div><h3 className="text-white font-semibold">Listing platforms</h3><p className="text-xs text-slate-400 mt-1">Connect this agency to your listing portals.</p></div>
      <select aria-label="Connection environment" value={environment} onChange={event => setEnvironment(event.target.value)} className="bg-slate-950 border border-slate-700 rounded-lg p-2 text-sm text-white"><option>Sandbox</option><option>Production</option></select>
    </div>
    {(!tenantId || tenantId === '__all__') ? <p className="text-sm text-slate-400">Select one agency to manage its listing platforms.</p> : <>
      {error && <p role="alert" className="text-red-300 text-sm">{error} <button type="button" onClick={() => { setError(''); setRetryCount(count => count + 1); }} className="ml-2 underline text-blue-300">Retry</button></p>}
      {!data && !error && <p className="text-slate-400 text-sm">Loading platforms…</p>}
      {data?.providers.map(provider => <AgencyPortalEditor key={`${tenantId}:${provider.key}:${environment}`} tenantId={tenantId} provider={provider} environment={environment}
        connection={data.connections.find(item => item.providerKey === provider.key && item.environment === environment)} canEdit={hasPermission('agencies:edit')}
        onSaved={connection => setData(previous => ({ ...previous, connections: [...previous.connections.filter(item => item.id !== connection.id), connection] }))} />)}
    </>}
  </section>;
}

function AgencyPortalEditor({ tenantId, provider, environment, connection, canEdit, onSaved }) {
  const [editing, setEditing] = useState(false);
  const [apiKey, setApiKey] = useState('');
  const [branchId, setBranchId] = useState(connection?.branchId || '');
  const [settings, setSettings] = useState(() => JSON.parse(connection?.settingsJson || '{}'));
  const [removeApiKey, setRemoveApiKey] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  async function save(event) {
    event.preventDefault(); setBusy(true); setMessage('');
    try {
      const response = await apiClient.put(`/properties/syndication/agencies/${encodeURIComponent(tenantId)}/${provider.key}`, { environment, branchId, apiKey: apiKey || null, removeApiKey, settings });
      onSaved(response.data); setApiKey(''); setRemoveApiKey(false); setEditing(false); setMessage('Agency connection saved.');
    } catch (failure) { setMessage(failure.response?.data?.message || 'Could not save the connection.'); }
    finally { setBusy(false); }
  }
  function cancel() { setEditing(false); setApiKey(''); setRemoveApiKey(false); setBranchId(connection?.branchId || ''); setSettings(JSON.parse(connection?.settingsJson || '{}')); setMessage(''); }
  return <article className="border border-slate-700 rounded-xl p-4 space-y-3">
    <div className="flex justify-between gap-3"><strong className="text-white">{provider.name}</strong><span className={`text-xs ${connection?.hasApiKey ? 'text-emerald-300' : 'text-amber-300'}`}>{connection?.hasApiKey ? 'API key saved' : 'Preview · No API key'}</span></div>
    <p className="text-xs text-slate-400">{environment} connection. Broker AgentCodes are managed when adding or editing a broker.</p>
    {!provider.ready && <p className="text-xs text-amber-200">{provider.setupMessage}</p>}
    {!editing && canEdit && <button type="button" onClick={() => setEditing(true)} className="rounded-lg bg-blue-600 px-3 py-2 text-xs font-semibold text-white">{connection?.hasApiKey ? 'Edit connection' : 'Add connection'}</button>}
    {editing && <form onSubmit={save} className="space-y-3">
      <label className="block text-xs text-slate-300">API key<input type="password" autoComplete="new-password" maxLength={4096} disabled={busy || removeApiKey} value={apiKey} onChange={event => setApiKey(event.target.value)} placeholder={connection?.hasApiKey ? 'Leave blank to keep the saved key' : 'Enter agency API key'} className="mt-1 w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-white" /></label>
      <label className="block text-xs text-slate-300">Portal branch ID<input maxLength={200} disabled={busy} value={branchId} onChange={event => setBranchId(event.target.value)} className="mt-1 w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-white" /></label>
      {(provider.settings || []).map(field => <label key={field.key} className="block text-xs text-slate-300">{field.label}<input maxLength={500} disabled={busy} value={settings[field.key] || ''} onChange={event => setSettings(previous => ({ ...previous, [field.key]: event.target.value }))} className="mt-1 w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-white" /></label>)}
      {connection?.hasApiKey && <label className="flex gap-2 text-xs text-slate-300"><input type="checkbox" disabled={busy} checked={removeApiKey} onChange={event => { setRemoveApiKey(event.target.checked); setApiKey(''); }} />Remove saved key and disable new publishing</label>}
      <p className="text-xs text-slate-500">Keys are encrypted and are never displayed after saving.</p>
      <div className="flex gap-3"><button disabled={busy} className="bg-blue-600 rounded-lg px-3 py-2 text-xs text-white disabled:opacity-40">{busy ? 'Saving…' : 'Save connection'}</button><button type="button" disabled={busy} onClick={cancel} className="text-xs text-slate-300">Cancel</button></div>
    </form>}
    {message && <p role="status" className="text-xs text-slate-300">{message}</p>}
  </article>;
}
