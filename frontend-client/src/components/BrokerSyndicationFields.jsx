import { useEffect, useState } from 'react';
import apiClient from '../services/apiClient.js';

export default function BrokerSyndicationFields({ brokerId }) {
  const [data, setData] = useState(null);
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    const options = { signal: controller.signal };
    Promise.all([
      apiClient.get('/properties/syndication/providers', options),
      brokerId ? apiClient.get(`/crm/brokers/${encodeURIComponent(brokerId)}/syndication-codes`, options) : Promise.resolve({ data: {} }),
    ]).then(([providers, codes]) => setData({ providers: providers.data, codes: codes.data }))
      .catch(() => { if (!controller.signal.aborted) setError('Could not load portal AgentCodes. Reopen this form to retry. Existing codes will be preserved.'); });
    return () => controller.abort();
  }, [brokerId]);
  return <fieldset className="border border-slate-700 rounded-xl p-3 space-y-3">
    <legend className="text-xs text-slate-300 px-2">Listing portal AgentCodes</legend>
    <p className="text-xs text-slate-400">Enter this broker’s code for each platform. You can prepare codes before the agency connects a platform.</p>
    {error && <p role="alert" className="text-xs text-red-300">{error}</p>}
    {!data && !error && <p className="text-xs text-slate-400">Loading platforms…</p>}
    {data?.providers.map(provider => <div key={provider.key} className="space-y-2"><strong className="text-xs text-white">{provider.name}</strong>
      <div className="grid grid-cols-2 gap-3">{['Sandbox', 'Production'].map(environment => <label key={environment} className="text-xs text-slate-400">{environment} AgentCode
        <input name={`portalCode:${provider.key}:${environment}`} maxLength={200} defaultValue={data.codes[`${provider.key}:${environment}`] || ''} className="mt-1 w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-white" />
      </label>)}</div>
    </div>)}
  </fieldset>;
}
