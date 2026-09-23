import { useCallback, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import apiClient from '../services/apiClient.js';
import ListingMappingPreview from './ListingMappingPreview.jsx';
import { usePermission } from '../contexts/PermissionContext.jsx';

const statusLabels = {
  NotPublished: 'Not published', Queued: 'Queued', Submitted: 'Submitted — awaiting confirmation',
  Live: 'Live', WithdrawalPending: 'Withdrawal pending', Withdrawn: 'Withdrawn',
  RetryPending: 'Retry scheduled', NeedsAttention: 'Needs attention',
};

export default function ListingPortalsPanel({ propertyId }) {
  const [data, setData] = useState(null);
  const [error, setError] = useState('');
  const [environment, setEnvironment] = useState('Sandbox');
  const [selectedProviderKey, setSelectedProviderKey] = useState(null);
  const { hasPermission } = usePermission();
  const canEdit = hasPermission('properties:edit');
  const refresh = useCallback(async (signal) => {
    try {
      const response = await apiClient.get(`/properties/syndication/${propertyId}`, { signal });
      if (!signal?.aborted) { setData(response.data); setError(''); }
    } catch (failure) {
      if (!signal?.aborted) setError(failure.message || 'Could not load listing portals.');
    }
  }, [propertyId]);

  useEffect(() => {
    const controller = new AbortController();
    // eslint-disable-next-line react-hooks/set-state-in-effect -- Updates occur only after the network request settles.
    refresh(controller.signal);
    const timer = setInterval(() => refresh(controller.signal), 10000);
    return () => { controller.abort(); clearInterval(timer); };
  }, [refresh]);

  const availableProviders = (data?.providers || []).filter(provider => data.connections.some(connection =>
    connection.providerKey === provider.key && connection.environment === environment &&
    (connection.hasApiKey || connection.branchId?.trim())));
  const selectedProvider = availableProviders.find(provider => provider.key === selectedProviderKey);

  return <section className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
    <div className="flex items-center justify-between gap-3 mb-3">
      <h4 className="text-sm font-bold text-white">Listing portals</h4>
      <select aria-label="Portal environment" value={environment} onChange={event => { setEnvironment(event.target.value); setSelectedProviderKey(null); }} className="bg-slate-950 text-slate-200 border border-slate-700 rounded p-2 text-xs">
        <option>Sandbox</option><option>Production</option>
      </select>
    </div>
    <p className="text-xs text-slate-400 mb-4">Select a platform to review its listing mapping and publication status.</p>
    {environment === 'Sandbox' && <p className="text-xs text-amber-300 mb-3">Sandbox listings are for testing and are separate from production.</p>}
    {error && <p role="alert" className="text-xs text-red-300 mb-3">{error} <button onClick={() => refresh()}>Retry</button></p>}
    {!data && !error && <p role="status" className="text-xs text-slate-400">Loading portals…</p>}
    <ul className="space-y-2">
      {availableProviders.map(provider => {
        const connection = data.connections.find(item => item.providerKey === provider.key && item.environment === environment);
        const publication = data.publications.find(item => item.connectionId === connection.id);
        return <li key={provider.key}><button type="button" onClick={() => setSelectedProviderKey(provider.key)}
          aria-haspopup="dialog" className="w-full flex items-center justify-between gap-3 rounded-xl border border-slate-700 bg-slate-950/40 p-4 text-left hover:border-blue-500 hover:bg-slate-800 focus-visible:outline-2 focus-visible:outline-blue-400">
          <span className="min-w-0"><span className="block text-sm font-semibold text-white">{provider.name}</span>
            <span className="block mt-1 text-xs text-slate-400">{statusLabels[publication?.state] || 'Not published'}{!provider.ready || !connection.hasApiKey ? ' · Preview available' : ''}</span></span>
          <span className="shrink-0 text-sm text-blue-300" aria-hidden="true">View mapping →</span>
        </button></li>;
      })}
    </ul>
    {data && !availableProviders.length && <p className="text-xs text-slate-400">No platforms configured for this agency in {environment}. Add a platform’s API key or branch ID in Agency at a Glance.</p>}
    {selectedProvider && <MappingModal provider={selectedProvider} environment={environment} onClose={() => setSelectedProviderKey(null)}>
      <PortalConnection key={`${propertyId}:${selectedProvider.key}:${environment}`} provider={selectedProvider}
        environment={environment} propertyId={propertyId}
        connection={data.connections.find(item => item.providerKey === selectedProvider.key && item.environment === environment)}
        publications={data.publications} canEdit={canEdit} listing={data.listing} onRefresh={refresh} />
    </MappingModal>}
  </section>;
}

function PortalConnection({ provider, environment, propertyId, connection, publications, canEdit, listing, onRefresh }) {
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const publication = publications.find(item => item.connectionId === connection?.id);
  const state = publication?.state || 'NotPublished';
  const pending = ['Queued', 'Submitted', 'WithdrawalPending', 'RetryPending'].includes(state);

  async function perform(operation) {
    setBusy(true); setMessage('');
    try { await operation(); await onRefresh(); }
    catch (failure) { setMessage(failure.response?.data?.message || failure.message); }
    finally { setBusy(false); }
  }

  async function publish() {
    await apiClient.post(`/properties/syndication/${propertyId}/connections/${connection.id}/publish`, { publish: true });
    setMessage('Publication queued. Status will update after the provider responds.');
  }

  async function reconcile() {
    await apiClient.post(`/properties/syndication/${propertyId}/connections/${connection.id}/reconcile`);
    setMessage('Portal status check queued.');
  }

  async function withdraw() {
    await apiClient.post(`/properties/syndication/${propertyId}/connections/${connection.id}/publish`, { publish: false });
    setMessage('Withdrawal queued. The listing may remain visible until the provider confirms removal.');
  }

  return <article className="space-y-4">
    <div className="flex justify-between gap-3 text-sm"><strong className="text-white">{provider.name}</strong><span className={state === 'Live' ? 'text-emerald-300' : 'text-slate-300'}>{statusLabels[state] || state}</span></div>
    {!provider.ready && <p className="text-xs text-amber-200">{provider.setupMessage}</p>}
    {publication?.confirmedPublished && state !== 'Live' && <p className="text-xs text-amber-200">Last confirmed live; this request has not changed the confirmed portal status yet.</p>}
    {publication?.error && <p className="text-xs text-red-300">{publication.error}</p>}
    {publication?.lastConfirmedAt && <p className="text-xs text-slate-400">Last confirmed: {new Date(publication.lastConfirmedAt).toLocaleString()}</p>}
    {publication?.externalId && <p className="text-xs text-slate-400">Portal reference: {publication.externalId}</p>}
    {publication?.confirmedPublished && publication.publicUrl?.startsWith('https://') && <a className="text-sm text-blue-300" href={publication.publicUrl} target="_blank" rel="noreferrer">View portal listing</a>}
    {!connection?.hasApiKey && <p className="text-xs text-slate-400">Preview only. Add the agency API key in Agency at a Glance to configure this portal.</p>}
    <ListingMappingPreview provider={provider} environment={environment} connection={connection} listing={listing} />
    {canEdit && <div className="flex gap-4 text-xs">
      <button disabled={busy || pending || state === 'NeedsAttention' || !provider.ready || !connection?.hasApiKey || !connection?.enabled} onClick={() => perform(publish)} className="text-blue-300 disabled:opacity-40">{publication?.confirmedPublished ? 'Publish changes' : 'Publish'}</button>
      {provider.supportsStatusChecks && publication && <button disabled={busy || pending || !provider.ready} onClick={() => perform(reconcile)} className="text-blue-300 disabled:opacity-40">Check portal status</button>}
      {provider.supportsWithdrawal && (publication?.desiredPublished || publication?.confirmedPublished) && <button disabled={busy || state === 'WithdrawalPending'} onClick={() => perform(withdraw)} className="text-amber-300 disabled:opacity-40">Withdraw</button>}
    </div>}
    {message && <p role="status" className="text-xs text-slate-200">{message}</p>}
  </article>;
}

function MappingModal({ provider, environment, onClose, children }) {
  const dialogRef = useRef(null);
  useEffect(() => {
    const dialog = dialogRef.current;
    const previousFocus = document.activeElement;
    const previousOverflow = document.body.style.overflow;
    dialog.showModal();
    document.body.style.overflow = 'hidden';
    return () => {
      dialog.close();
      document.body.style.overflow = previousOverflow;
      previousFocus?.focus();
    };
  }, []);

  function handleDialogClose(event) {
    // StrictMode's cleanup queues a native close event, then reopens the dialog.
    // Only a dialog that is still closed should dismiss the React modal.
    if (!event.currentTarget.open) onClose();
  }

  return createPortal(<dialog ref={dialogRef} onClose={handleDialogClose} aria-labelledby="syndication-mapping-title"
    className="fixed inset-0 m-auto w-[calc(100%_-_2rem)] max-w-5xl max-h-[90dvh] rounded-2xl border border-slate-700 bg-slate-900 p-0 text-slate-200 shadow-2xl backdrop:bg-black/70">
    <div className="flex max-h-[90dvh] flex-col">
      <header className="flex shrink-0 items-center justify-between gap-4 border-b border-slate-700 px-6 py-4">
        <div><h2 id="syndication-mapping-title" className="text-lg font-semibold text-white">{provider.name} · Listing mapping</h2><p className="mt-1 text-xs text-slate-400">{environment}</p></div>
        <button type="button" autoFocus onClick={onClose} aria-label="Close listing mapping" className="rounded-lg px-3 py-2 text-sm text-slate-300 hover:bg-slate-800 focus-visible:outline-2 focus-visible:outline-blue-400">Close ✕</button>
      </header>
      <div className="min-h-0 overflow-y-auto p-4 sm:p-6">{children}</div>
    </div>
  </dialog>, document.body);
}
