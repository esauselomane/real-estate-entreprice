import { useState } from 'react';

export default function ListingMappingPreview({ provider, environment, connection, listing }) {
  const [listingPurpose, setListingPurpose] = useState(listing?.forSale ? 'Sale' : 'Rental');
  const [agentCode, setAgentCode] = useState('');
  const [locationReference, setLocationReference] = useState('');
  if (!listing) return null;
  const price = listingPurpose === 'Sale' && listing.salePrice != null
    ? new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' }).format(listing.salePrice) : '';
  const rows = [
    ['Property reference', String(listing.propertyId), 'From this property'],
    ['Agency branch', connection?.branchId, 'Agency connection'],
    ['Listing purpose', listingPurpose, 'Preview selection'],
    ['Address', listing.address, 'From this property'],
    ['Description', listing.description, 'From this property'],
    ['Asking price', price, listingPurpose === 'Rental' ? 'Rental pricing mapping pending' : 'From this property'],
    ['Photographs', listing.photoUrls?.length ? `${listing.photoUrls.length} photos` : '', 'Public photo URLs must be verified'],
    ['AgentCode', agentCode, 'Preview only · saved codes belong to broker profiles'],
    ['Portal location reference', locationReference, 'Provider location lookup pending'],
  ];
  return <section className="rounded-xl border border-slate-700 bg-slate-950/50">
    <h3 className="px-4 py-3 text-sm font-semibold text-white">{provider.name} · Listing mapping preview</h3>
    <div className="px-4 pb-4 space-y-4">
      <div className="flex flex-wrap gap-2 text-[11px]"><span className="rounded-full bg-blue-500/10 text-blue-300 px-2 py-1">{environment}</span><span className="rounded-full bg-amber-500/10 text-amber-200 px-2 py-1">Draft preview · Nothing sent</span></div>
      <p className="text-xs text-slate-400">Review how your listing could map to this portal. Preview selections are not saved. The final provider field mappings and location lookup still require integration verification.</p>
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="text-xs text-slate-300">Listing purpose<select value={listingPurpose} onChange={event => setListingPurpose(event.target.value)} className="block mt-1 w-full bg-slate-900 border border-slate-700 rounded-lg p-2"><option disabled={!listing.forSale}>Sale</option><option disabled={!listing.forRent}>Rental</option></select></label>
        <label className="text-xs text-slate-300">AgentCode preview<input value={agentCode} onChange={event => setAgentCode(event.target.value)} maxLength={200} placeholder="Try a broker’s portal AgentCode" className="block mt-1 w-full bg-slate-900 border border-slate-700 rounded-lg p-2" /></label>
        <label className="text-xs text-slate-300 sm:col-span-2">Portal location reference preview<input value={locationReference} onChange={event => setLocationReference(event.target.value)} maxLength={200} placeholder="Provider suburb / area reference" className="block mt-1 w-full bg-slate-900 border border-slate-700 rounded-lg p-2" /></label>
      </div>
      <div className="overflow-x-auto"><table className="w-full text-left text-xs"><thead className="text-slate-400"><tr><th className="py-2 pr-3">Portal value</th><th className="py-2 pr-3">Listing / mapping</th><th className="py-2">Source / readiness</th></tr></thead>
        <tbody>{rows.map(([label, value, source]) => <tr key={label} className="border-t border-slate-800"><th scope="row" className="py-3 pr-3 font-medium text-slate-300 align-top">{label}</th><td className="py-3 pr-3 text-slate-200 max-w-xs break-words whitespace-pre-wrap">{value || <span className="text-amber-300">Needs mapping</span>}</td><td className="py-3 text-slate-500 align-top">{source}</td></tr>)}</tbody>
      </table></div>
      <div className="rounded-lg bg-slate-900 p-3 text-xs text-slate-400 space-y-1">
        <p className={connection?.hasApiKey ? 'text-emerald-300' : 'text-amber-300'}>{connection?.hasApiKey ? '✓ Agency API key saved' : '○ Agency API key missing'}</p>
        <p>{connection?.branchId ? '✓ Branch reference entered' : '○ Agency branch mapping needed'}</p>
        <p>○ Broker assignment and portal taxonomy need verification before publishing</p>
        {!provider.ready && <p>○ Provider integration is not yet ready for submission</p>}
      </div>
      <button type="button" disabled className="w-full rounded-lg bg-blue-600 px-4 py-2.5 text-xs font-semibold text-white opacity-40 cursor-not-allowed">Submit preview to {provider.name} · Unavailable</button>
    </div>
  </section>;
}
