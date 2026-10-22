import { useState } from 'react';
import apiClient from '../services/apiClient.js';
import { propertyService } from '../services/propertyService.js';

export default function PublicListingExpiryReport() {
  const today = new Date().toISOString().slice(0, 10);
  const [from, setFrom] = useState(today);
  const [to, setTo] = useState(new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 10));
  const [rows, setRows] = useState(null);
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  async function load(event) {
    event.preventDefault(); setBusy(true); setMessage('');
    try {
      const response = await apiClient.get('/properties/public-listings/expiring', { params: { fromUtc: `${from}T00:00:00Z`, toUtc: `${to}T23:59:59Z`, ...propertyService._tenantQueryParams() } });
      setRows(response.data);
    } catch (error) { setMessage(error.response?.data?.message || 'Could not load the expiry report.'); }
    finally { setBusy(false); }
  }
  function download() {
    function quote(value) { return '"' + String(value ?? '').replace(/^[=+@-]/, "'$&").replaceAll('"', '""') + '"'; }
    const lines = [['Property ID', 'Agency', 'Address', 'Letting expires (UTC)', 'Sale remains available', 'Review status'], ...rows.map(row => [row.propertyId, row.tenantId, row.completeAddress, row.publicLettingExpiresAt, row.isForSale ? 'Yes' : 'No', row.publicReviewStatus])];
    const csv = lines.map(line => line.map(quote).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a'); link.href = url; link.download = 'letting-listings-expiry.csv'; link.click(); URL.revokeObjectURL(url);
  }
  return <details className="rounded-xl border border-slate-700 bg-slate-900 p-4 mb-4">
    <summary className="cursor-pointer font-semibold text-white">Letting listings — expiry report</summary>
    <form onSubmit={load} className="mt-4 flex flex-wrap items-end gap-3 text-sm text-slate-300">
      <label>From (UTC)<input type="date" required value={from} onChange={event => setFrom(event.target.value)} className="block bg-slate-950 p-2 rounded" /></label>
      <label>Through (UTC)<input type="date" required min={from} value={to} onChange={event => setTo(event.target.value)} className="block bg-slate-950 p-2 rounded" /></label>
      <button disabled={busy} className="rounded bg-blue-600 px-4 py-2 text-white">{busy ? 'Loading…' : 'Run report'}</button>
      {rows && <button type="button" onClick={download} className="rounded border border-slate-600 px-4 py-2">Download CSV</button>}
    </form>
    <p role="status" className="mt-2 text-sm text-slate-400">{message || (rows ? `${rows.length} listing(s) in this date range.` : 'Terms run for two calendar months. Choose past dates to include expired terms.')}</p>
    {rows && <div className="mt-3 overflow-x-auto"><table className="w-full text-left text-xs text-slate-300"><thead><tr><th>Property</th><th>Agency</th><th>Letting ends (UTC)</th><th>Status</th></tr></thead><tbody>{rows.map(row => <tr key={row.propertyId} className="border-t border-slate-700"><td className="py-3">#{row.propertyId} · {row.completeAddress}</td><td>{row.tenantId}</td><td>{new Date(row.publicLettingExpiresAt).toISOString().slice(0, 10)}</td><td>{row.publicReviewStatus}{row.isForSale ? ' · Sale retained' : ''}</td></tr>)}</tbody></table></div>}
  </details>;
}
