import { useEffect, useState } from 'react';
import apiClient from '../services/apiClient.js';
import GooglePropertyMapPicker from './maps/GooglePropertyMapPicker.jsx';

export default function AgencySampleDataProvisioning({ activeTenantId }) {
  const [requests, setRequests] = useState([]);
  const [selectedTenant, setSelectedTenant] = useState('');
  const [locations, setLocations] = useState({});
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [refresh, setRefresh] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    async function load() {
      try {
        const response = await apiClient.get('/auth/sample-data', { signal: controller.signal });
        setRequests(response.data);
        setError('');
      } catch (failure) {
        if (!controller.signal.aborted) setError(failure.message || 'Could not load sample data requests.');
      }
    }
    load();
    const interval = setInterval(load, 10000);
    return () => { controller.abort(); clearInterval(interval); };
  }, [refresh]);

  const agency = requests.find(item => item.tenantId === selectedTenant)
    || requests.find(item => item.tenantId === activeTenantId) || requests[0];
  const location = locations[agency?.tenantId] ?? null;
  function setLocation(update) {
    setLocations(current => ({
      ...current,
      [agency.tenantId]: typeof update === 'function' ? update(current[agency.tenantId]) : update,
    }));
  }

  async function provision() {
    setBusy(true);
    setError('');
    try {
      await apiClient.post(`/auth/sample-data/${encodeURIComponent(agency.tenantId)}/provision`, {
        latitude: location?.latitude === '' ? null : location?.latitude ?? null,
        longitude: location?.longitude === '' ? null : location?.longitude ?? null,
      });
      setRefresh(value => value + 1);
    } catch (failure) {
      setError(failure.message || 'Could not start provisioning.');
    } finally { setBusy(false); }
  }

  const canProvision = agency && ['Requested', 'Failed'].includes(agency.state);
  const coordinatesReady = location?.latitude !== '' && location?.longitude !== ''
    && location?.latitude != null && location?.longitude != null;
  return <section className="rounded-2xl border border-slate-700 bg-slate-900 p-5 space-y-4">
    <div><h2 className="text-lg font-semibold text-white">Trial sample data requests</h2>
      <p className="text-sm text-slate-400">Platform administrators provision requested demonstration data here. The agency administrator receives an email when it is ready.</p></div>
    {error && <p role="alert" className="text-sm text-red-400">{error}</p>}
    {!agency ? <p className="text-sm text-slate-400">No agencies have requested sample data.</p> : <>
      <label className="block text-sm text-slate-300">Requesting agency
        <select value={agency.tenantId} disabled={busy} onChange={event => { setLocation(null); setSelectedTenant(event.target.value); }}
          className="mt-1 block w-full rounded-lg border border-slate-700 bg-slate-950 p-3 text-white">
          {requests.map(item => <option key={item.tenantId} value={item.tenantId}>{item.agencyName} ({item.tenantId}) — {item.state}</option>)}
        </select></label>
      <p className="text-sm text-slate-300">Agency address: {agency.headquartersAddress}</p>
      <p className="text-sm text-slate-400">5 contacts · 5 brokers · 5 properties · 1 company · 1 legal entity · 5 leads · 5 deals. All linked and labelled as samples.</p>
      {canProvision && !agency.hasPlan && <div className="space-y-2">
        <p className="text-sm text-slate-300">Confirm the location of the agency address. Sample properties will be placed nearby.</p>
        <GooglePropertyMapPicker key={agency.tenantId} address={location?.completeAddress ?? agency.headquartersAddress}
          latitude={location?.latitude ?? ''} longitude={location?.longitude ?? ''} onChange={setLocation} height={240} />
        <div className="flex gap-3">
          <label className="text-sm text-slate-300">Latitude<input aria-label="Agency latitude" type="number" step="any" min="-85" max="85"
            value={location?.latitude ?? ''} onChange={event => setLocation(value => ({ ...value, latitude: event.target.value }))}
            className="block w-full rounded-lg border border-slate-700 bg-slate-950 p-2" /></label>
          <label className="text-sm text-slate-300">Longitude<input aria-label="Agency longitude" type="number" step="any" min="-179" max="179"
            value={location?.longitude ?? ''} onChange={event => setLocation(value => ({ ...value, longitude: event.target.value }))}
            className="block w-full rounded-lg border border-slate-700 bg-slate-950 p-2" /></label>
        </div>
      </div>}
      <p role="status" className="text-sm text-slate-300">{agency.state === 'Completed'
        ? 'Sample data is ready and the notification email has been sent.'
        : agency.provisionedAt ? 'Sample data is ready. Notification email pending.'
          : agency.state === 'Requested' ? 'Awaiting platform administrator approval.'
            : agency.state === 'Failed' ? 'Provisioning needs attention. You can safely retry.' : 'Provisioning is queued or in progress.'}</p>
      {agency.error && <p className="text-sm text-amber-300">{agency.error}</p>}
      {canProvision && <button type="button" onClick={provision} disabled={busy || (!agency.hasPlan && !coordinatesReady)}
        className="rounded-lg bg-blue-600 px-4 py-2 font-semibold text-white disabled:opacity-50 disabled:cursor-not-allowed">
        {busy ? 'Queuing…' : agency.provisionedAt ? 'Retry notification email' : agency.state === 'Failed' ? 'Retry provisioning' : 'Provision sample data'}
      </button>}
    </>}
  </section>;
}
