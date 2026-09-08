import { useEffect, useState } from 'react';
import { propertyService } from '../services/propertyService.js';
import { useAuth } from '../contexts/AuthContext.jsx';
import Can from '../components/Can.jsx';
import { Plus, Pencil, Trash2, Save, X, Search, Tag } from 'lucide-react';

export default function AmenityTypesWorkspace() {
  const { userRole } = useAuth();
  const isSuperAdmin = userRole === 'PlatformAdmin';
  const [types, setTypes] = useState([]);
  const [search, setSearch] = useState('');
  const [form, setForm] = useState({ name: '', label: '', requiresBoolean: false, requiresNumber: false, isResidentialOnly: false, isActive: true });
  const [editing, setEditing] = useState(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const data = await propertyService.getAllAmenityTypes();
      setTypes(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || 'Failed to load amenity types.');
      setTypes([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const resetForm = () => setForm({ name: '', label: '', requiresBoolean: false, requiresNumber: false, isResidentialOnly: false, isActive: true });

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    setError('');
    try {
      if (editing) {
        await propertyService.updateAmenityType(editing.amenityTypeId, form);
      } else {
        await propertyService.createAmenityType(form);
      }
      resetForm();
      setEditing(null);
      await load();
    } catch (err) {
      setError(err.message || 'Save failed.');
    } finally {
      setSaving(false);
    }
  };

  const handleEdit = (row) => {
    setEditing(row);
    setForm({
      name: row.name || '',
      label: row.label || '',
      requiresBoolean: row.requiresBoolean,
      requiresNumber: row.requiresNumber,
      isResidentialOnly: row.isResidentialOnly,
      isActive: row.isActive
    });
  };

  const handleDelete = async (id) => {
    if (!confirm('Delete this amenity type? This cannot be undone.')) return;
    try {
      await propertyService.deleteAmenityType(id);
      await load();
    } catch (err) {
      alert(err.message || 'Delete failed.');
    }
  };

  const filtered = types.filter(t => (t.name || '').toLowerCase().includes(search.toLowerCase()) || (t.label || '').toLowerCase().includes(search.toLowerCase()));

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px]">
          <Tag size={14} className="text-emerald-500" /> Amenity Catalogue
        </div>
        <Can permission="amenities:create">
          <button type="button" onClick={() => { resetForm(); setEditing(null); }} className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold cursor-pointer">
            <Plus size={12} /> New Amenity
          </button>
        </Can>
      </div>

      <form onSubmit={handleSubmit} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-4">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          <div className="space-y-1">
            <label className="block text-[10px] text-slate-400 uppercase font-bold mb-1">Name (key)</label>
            <input required type="text" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="e.g. Parking" className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" />
          </div>
          <div className="space-y-1">
            <label className="block text-[10px] text-slate-400 uppercase font-bold mb-1">Label</label>
            <input type="text" value={form.label} onChange={(e) => setForm({ ...form, label: e.target.value })} placeholder="e.g. Secure Parking" className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" />
          </div>
          <div className="space-y-1">
            <label className="block text-[10px] text-slate-400 uppercase font-bold mb-1">Value type</label>
            <div className="flex items-center gap-4 mt-1">
              <label className="inline-flex items-center gap-1.5 text-xs text-slate-300 cursor-pointer whitespace-nowrap">
                <input type="checkbox" checked={form.requiresBoolean} onChange={(e) => setForm({ ...form, requiresBoolean: e.target.checked })} className="rounded border-slate-700 bg-slate-950" /> Boolean
              </label>
              <label className="inline-flex items-center gap-1.5 text-xs text-slate-300 cursor-pointer whitespace-nowrap">
                <input type="checkbox" checked={form.requiresNumber} onChange={(e) => setForm({ ...form, requiresNumber: e.target.checked })} className="rounded border-slate-700 bg-slate-950" /> Number
              </label>
            </div>
          </div>
          <div className="space-y-1">
            <label className="block text-[10px] text-slate-400 uppercase font-bold mb-1">Scope</label>
            <label className="inline-flex items-center gap-1.5 text-xs text-slate-300 cursor-pointer whitespace-nowrap mt-1">
              <input type="checkbox" checked={form.isResidentialOnly} onChange={(e) => setForm({ ...form, isResidentialOnly: e.target.checked })} className="rounded border-slate-700 bg-slate-950" /> Residential only
            </label>
          </div>
        </div>
        <div className="flex items-center gap-3">
          {editing ? (
            <>
              <Can permission="amenities:edit">
                <button type="submit" disabled={saving} className="inline-flex items-center gap-1.5 px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold rounded-xl cursor-pointer disabled:opacity-40">
                  <Save size={12} /> Update
                </button>
              </Can>
              <button type="button" onClick={() => { resetForm(); setEditing(null); }} className="inline-flex items-center gap-1.5 px-4 py-2 bg-slate-950 hover:bg-slate-800 text-slate-400 text-xs font-bold rounded-xl border border-slate-800 cursor-pointer">
                <X size={12} /> Cancel
              </button>
            </>
          ) : (
            <Can permission="amenities:create">
              <button type="submit" disabled={saving} className="inline-flex items-center gap-1.5 px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold rounded-xl cursor-pointer disabled:opacity-40">
                <Save size={12} /> Create
              </button>
            </Can>
          )}
        </div>
        {error && <p className="text-[10px] text-red-400 bg-red-950/40 px-2 py-1 rounded border border-red-900">{error}</p>}
      </form>

      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
        <div className="flex items-center gap-2 mb-4">
          <Search size={14} className="text-slate-500" />
          <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search amenity types..." className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-blue-500" />
        </div>
        {loading ? (
          <p className="text-xs text-slate-500 text-center py-4">Loading amenity types...</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-[11px]">
              <thead>
                <tr className="text-slate-500 uppercase text-[9px]">
                  <th className="text-left py-1">Name</th>
                  <th className="text-left py-1">Label</th>
                  <th className="text-left py-1">Boolean</th>
                  <th className="text-left py-1">Number</th>
                  <th className="text-left py-1">Residential</th>
                  <th className="text-left py-1">Active</th>
                  <th className="text-right py-1">Actions</th>
                </tr>
              </thead>
              <tbody>
                {filtered.length === 0 && (
                  <tr><td colSpan="7" className="py-3 text-center text-slate-500">No amenity types found.</td></tr>
                )}
                {filtered.map((t) => (
                  <tr key={t.amenityTypeId} className="border-t border-slate-800 hover:bg-slate-800/20">
                    <td className="py-1.5 text-white font-medium">{t.name}</td>
                    <td className="py-1.5 text-slate-300">{t.label || '—'}</td>
                    <td className="py-1.5 text-slate-400">{t.requiresBoolean ? 'Yes' : 'No'}</td>
                    <td className="py-1.5 text-slate-400">{t.requiresNumber ? 'Yes' : 'No'}</td>
                    <td className="py-1.5 text-slate-400">{t.isResidentialOnly ? 'Yes' : 'No'}</td>
                    <td className="py-1.5 text-slate-400">{t.isActive ? 'Active' : 'Inactive'}</td>
                    <td className="py-1.5 text-right">
                      <Can permission="amenities:edit">
                        <button type="button" onClick={() => handleEdit(t)} className="p-1.5 text-slate-400 hover:text-white"><Pencil size={13} /></button>
                      </Can>
                      <Can permission="amenities:delete">
                        <button type="button" onClick={() => handleDelete(t.amenityTypeId)} className="p-1.5 text-red-400 hover:text-red-300"><Trash2 size={13} /></button>
                      </Can>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
      <p className="text-[10px] text-slate-500">Amenity types drive the property and unit amenity editors. Deactivated types are hidden from dropdowns but retained for historical records.</p>
    </div>
  );
}