import { useAuth } from '../contexts/AuthContext.jsx';
import { useState } from 'react';
import apiClient from '../services/apiClient.js';

export default function PublicListingSettings({ property }) {
  const { activeProfile } = useAuth();
  const canManageHot = ['AgencyAdmin', 'PlatformAdmin'].includes(activeProfile?.role) && activeProfile?.tenantId === property.tenantId;
  const [visible, setVisible] = useState(property.isPublicListing || false);
  const [hot, setHot] = useState(property.isHotProperty || false);
  const [ownerName, setOwnerName] = useState(property.publicOwnerName || '');
  const [ownerEmail, setOwnerEmail] = useState(property.publicOwnerEmail || '');
  const [ownerPhone, setOwnerPhone] = useState(property.publicOwnerPhone || '');
  const [roomLettingType, setRoomLettingType] = useState(property.roomLettingType || 'None');
  const [renewLetting, setRenewLetting] = useState(false);
  const [expiresAt, setExpiresAt] = useState(property.publicLettingExpiresAt);
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  async function save(event) {
    event.preventDefault(); setBusy(true); setMessage('');
    try {
      const response = await apiClient.put(`/properties/${property.propertyId}/public-listing`, {
        isPublicListing: visible, ...(canManageHot ? { isHotProperty: visible && hot } : {}), ownerName, ownerEmail, ownerPhone, roomLettingType, renewLetting,
      });
      setHot(response.data.isHotProperty);
      setExpiresAt(response.data.publicLettingExpiresAt); setRenewLetting(false);
      setVisible(response.data.isPublicListing);
      setMessage(`Saved: ${response.data.publicReviewStatus}.`);
    } catch (error) { setMessage(error.message); }
    finally { setBusy(false); }
  }
  return <form onSubmit={save} className="bg-slate-900 border border-slate-800 rounded-2xl p-6 space-y-3">
    <h4 className="font-bold text-white">Public website</h4>
    <p className="text-xs text-slate-400">Review status: {property.publicReviewStatus || 'Draft'}. Check the owner’s contact details before publishing. Owner edits return a listing to review.</p>
    <label className="flex gap-2 text-sm text-white"><input type="checkbox" checked={visible} onChange={event => setVisible(event.target.checked)} />Visible on public website</label>
    <label className="flex gap-2 text-sm text-white"><input type="checkbox" disabled={!visible || !canManageHot} checked={hot} onChange={event => setHot(event.target.checked)} />Hot property</label>
    {!canManageHot && <p className="text-xs text-slate-400">Only administrators belonging to this agency can change hot status.</p>}
    {property.isForRent && <div className="text-xs text-slate-400"><p>Letting term ends: {expiresAt ? new Date(expiresAt).toLocaleDateString() : 'Two calendar months after first publication'}</p><label className="flex gap-2 mt-2"><input type="checkbox" checked={renewLetting} onChange={event => setRenewLetting(event.target.checked)} />Explicitly renew the letting term for two calendar months when saving a published listing</label></div>}
    <label className="block text-xs text-slate-400">Room share / let<select value={roomLettingType} onChange={event => setRoomLettingType(event.target.value)} className="block w-full mt-1 p-2 rounded bg-slate-950 border border-slate-700 text-white"><option value="None">Whole property / no room letting</option><option value="PrivateRoom">Private room to let</option><option value="SharedRoom">Shared room / room share</option></select></label>
    <label className="block text-xs text-slate-400">Public owner name<input required maxLength={200} value={ownerName} onChange={event => setOwnerName(event.target.value)} className="block w-full mt-1 p-2 rounded bg-slate-950 border border-slate-700 text-white" /></label>
    <label className="block text-xs text-slate-400">Public owner email<input required type="email" maxLength={254} value={ownerEmail} onChange={event => setOwnerEmail(event.target.value)} className="block w-full mt-1 p-2 rounded bg-slate-950 border border-slate-700 text-white" /></label>
    <label className="block text-xs text-slate-400">Public owner phone<input required type="tel" maxLength={40} value={ownerPhone} onChange={event => setOwnerPhone(event.target.value)} className="block w-full mt-1 p-2 rounded bg-slate-950 border border-slate-700 text-white" /></label>
    <p className="text-xs text-slate-400">Saving a visible listing approves publication of these contact details.</p>
    <button disabled={busy} className="bg-blue-600 text-white rounded px-4 py-2 text-sm">{busy ? 'Saving…' : 'Save publication settings'}</button>
    <p role="status" className="text-sm text-blue-300">{message}</p>
  </form>;
}
