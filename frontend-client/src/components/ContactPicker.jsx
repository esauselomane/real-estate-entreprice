import { useState, useEffect, useRef } from 'react';
import { Search, User, X, Plus, Loader2, Check } from 'lucide-react';
import { crmService } from '../services/crmService.js';

/**
 * Reusable contact picker — single or multi-select with search-then-pick and
 * optional inline "create new". The component always stores ids upstream; the
 * display name is rendered from the picked contact's `fullName`.
 *
 * Modes:
 *   - single (default): value is a contact object or null
 *   - multi           : value is an array of contact objects (order preserved)
 *
 * Props:
 *   - value           : current selection (null | Contact | Contact[])
 *   - onChange        : (next) => void — receives the new value in the same shape as `value`
 *   - placeholder     : input placeholder
 *   - allowCreate     : show the "Create new" affordance (single only — inline create in multi is out of scope)
 *   - multi           : enable multi-select mode
 *   - disabled        : lock the picker
   *   - defaultRole     : contactType for inline-created contacts (single mode)
 */
export default function ContactPicker({
  value = null,
  onChange,
  placeholder = 'Search contacts by name, email or phone...',
  allowCreate = true,
  multi = false,
  disabled = false,
  defaultRole = 'Tenant'
}) {
  const isMulti = !!multi;
  const selectedList = isMulti
    ? (Array.isArray(value) ? value : [])
    : (value ? [value] : []);
  const selectedIds = new Set(selectedList.map((c) => c.contactId));

  const [query, setQuery] = useState('');
  const [matches, setMatches] = useState([]);
  const [showDropdown, setShowDropdown] = useState(false);
  const [isSearching, setIsSearching] = useState(false);
  const [isCreating, setIsCreating] = useState(false);
  const [newContact, setNewContact] = useState({ fullName: '', emailAddress: '', phoneNumber: '' });
  const [createError, setCreateError] = useState('');
  const debounceRef = useRef(null);
  const wrapperRef = useRef(null);

  // Close dropdown on outside click
  useEffect(() => {
    const onDocClick = (e) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target)) {
        setShowDropdown(false);
      }
    };
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, []);

  // Single-mode: mirror the selected value's name into the visible query when
  // the parent swaps it (e.g. loading an existing lease). For multi we keep
  // the query clean since the chips show the selections.
  useEffect(() => {
    if (!isMulti && value && typeof value === 'object') {
      setQuery(value.fullName || '');
    }
    if (isMulti && (!value || value.length === 0)) {
      setQuery('');
    }
  }, [isMulti, value?.contactId, value?.fullName, value?.length]);

  const handleSearch = (raw) => {
    setQuery(raw);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (raw.trim().length < 2) {
      setMatches([]);
      setShowDropdown(false);
      return;
    }
    debounceRef.current = setTimeout(async () => {
      setIsSearching(true);
      try {
        const results = await crmService.searchContacts(raw);
        // Filter out already-selected in multi mode
        const filtered = isMulti
          ? (results || []).filter((c) => !selectedIds.has(c.contactId))
          : (results || []);
        setMatches(filtered);
        setShowDropdown(true);
      } catch {
        setMatches([]);
      } finally {
        setIsSearching(false);
      }
    }, 250);
  };

  const pick = (contact) => {
    if (isMulti) {
      onChange([...selectedList, contact]);
    } else {
      onChange(contact);
      setQuery(contact.fullName || '');
    }
    setShowDropdown(false);
  };

  const removeOne = (contactId) => {
    if (isMulti) {
      onChange(selectedList.filter((c) => c.contactId !== contactId));
    } else {
      onChange(null);
      setQuery('');
      setMatches([]);
    }
  };

  const clearAll = () => {
    onChange(isMulti ? [] : null);
    setQuery('');
    setMatches([]);
    setShowDropdown(false);
    setIsCreating(false);
  };

  const submitNew = async (e) => {
    e.preventDefault();
    e.stopPropagation();
    setCreateError('');
    if (!newContact.fullName.trim()) {
      setCreateError('Full name is required.');
      return;
    }
    try {
      const created = await crmService.createContact({
        fullName: newContact.fullName.trim(),
        emailAddress: newContact.emailAddress.trim(),
        phoneNumber: newContact.phoneNumber.trim(),
        contactType: defaultRole
      });
      pick(created);
      setNewContact({ fullName: '', emailAddress: '', phoneNumber: '' });
    } catch (err) {
      setCreateError(err || 'Failed to create contact.');
    }
  };

  return (
    <div ref={wrapperRef} className="space-y-2">
      {/* 🏷️ Selected chips (multi-mode shows them above the input) */}
      {isMulti && selectedList.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selectedList.map((c) => (
            <span key={c.contactId} className="inline-flex items-center gap-1.5 bg-blue-950/60 border border-blue-800 text-blue-300 text-xs px-2.5 py-1 rounded-lg">
              <User size={11} />
              {c.fullName || c.contactId}
              <button type="button" onClick={() => removeOne(c.contactId)} className="hover:text-red-400 cursor-pointer"><X size={11} /></button>
            </span>
          ))}
          <button type="button" onClick={clearAll} className="text-[10px] text-slate-500 hover:text-slate-300 underline">Clear all</button>
        </div>
      )}

      {/* Single-mode: show linked pill above the input */}
      {!isMulti && value && (
        <div className="flex items-center gap-2 p-2 bg-blue-950/30 border border-blue-900 rounded-lg">
          <Check size={12} className="text-blue-400" />
          <span className="text-xs text-blue-300">Linked: {value.fullName || value.contactId}</span>
          <button type="button" onClick={clearAll} className="ml-auto text-slate-400 hover:text-white"><X size={12} /></button>
        </div>
      )}

      <div className="relative">
        <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500" />
        <input
          type="text"
          value={query}
          onChange={(e) => handleSearch(e.target.value)}
          onFocus={() => matches.length > 0 && setShowDropdown(true)}
          disabled={disabled}
          placeholder={placeholder}
          className="w-full pl-9 pr-9 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 disabled:opacity-60"
        />
        {(isSearching || isCreating) && (
          <Loader2 size={14} className="absolute right-3 top-1/2 -translate-y-1/2 text-blue-500 animate-spin" />
        )}
        {showDropdown && matches.length > 0 && (
          <div className="absolute z-50 w-full mt-1 bg-slate-900 border border-slate-800 rounded-xl shadow-xl max-h-48 overflow-y-auto">
            {matches.map((c) => (
              <button
                key={c.contactId}
                type="button"
                onClick={() => pick(c)}
                className="w-full text-left px-3 py-2 hover:bg-slate-800 text-xs text-white flex items-center gap-2"
              >
                <User size={12} className="text-slate-400" />
                <div>
                  <div className="font-semibold">{c.fullName}</div>
                  <div className="text-[10px] text-slate-500">{c.emailAddress} • {c.phoneNumber}</div>
                </div>
              </button>
            ))}
            {!isMulti && allowCreate && (
              <button
                type="button"
                onClick={() => { setIsCreating(true); setShowDropdown(false); }}
                className="w-full text-left px-3 py-2 hover:bg-slate-800 text-xs text-blue-300 flex items-center gap-2 border-t border-slate-800"
              >
                <Plus size={12} />
                Create new contact
              </button>
            )}
          </div>
        )}
        {showDropdown && matches.length === 0 && query.trim().length >= 2 && !isSearching && !isMulti && allowCreate && (
          <div className="absolute z-50 w-full mt-1 bg-slate-900 border border-slate-800 rounded-xl shadow-xl p-2">
            <button
              type="button"
              onClick={() => { setIsCreating(true); setShowDropdown(false); }}
              className="w-full text-left px-2 py-1.5 hover:bg-slate-800 text-xs text-blue-300 flex items-center gap-2"
            >
              <Plus size={12} /> No match — create new contact "{query.trim()}"
            </button>
          </div>
        )}
      </div>

      {isCreating && (
        <form onSubmit={submitNew} className="p-3 bg-slate-950/60 border border-slate-800 rounded-xl space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-[10px] font-bold uppercase text-slate-400">New contact</span>
            <button type="button" onClick={() => setIsCreating(false)} className="text-slate-400 hover:text-white"><X size={12} /></button>
          </div>
          {createError && <div className="text-[10px] text-red-400">{createError}</div>}
          <input
            required
            placeholder="Full name *"
            value={newContact.fullName}
            onChange={(e) => setNewContact({ ...newContact, fullName: e.target.value })}
            className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-lg text-xs text-white focus:outline-none focus:border-blue-500"
          />
          <div className="grid grid-cols-2 gap-2">
            <input
              type="email"
              placeholder="Email"
              value={newContact.emailAddress}
              onChange={(e) => setNewContact({ ...newContact, emailAddress: e.target.value })}
              className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-lg text-xs text-white focus:outline-none focus:border-blue-500"
            />
            <input
              type="text"
              placeholder="Phone"
              value={newContact.phoneNumber}
              onChange={(e) => setNewContact({ ...newContact, phoneNumber: e.target.value })}
              className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-lg text-xs text-white focus:outline-none focus:border-blue-500"
            />
          </div>
          <button type="submit" className="w-full py-2 bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold rounded-lg">Create & link</button>
        </form>
      )}
    </div>
  );
}
