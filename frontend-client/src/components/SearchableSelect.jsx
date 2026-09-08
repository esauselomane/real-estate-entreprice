import { useState, useEffect, useRef } from 'react';
import { Search, User, X, Plus, Loader2, Check } from 'lucide-react';

/**
 * Generic searchable select — single or multi-select with debounced type-ahead.
 * Always stores the full selected object(s) upstream; display text comes from
 * `itemLabel`. Mirrors the BrokerPicker / ContactPicker UX so the CRM
 * Directory's company + legal-entity pickers feel consistent.
 *
 * Props:
 *   - value        : null | item | item[]      (single or multi, same shape as onChange)
 *   - onChange     : (next) => void
 *   - onSearch     : async (query) => item[]   (debounced on text input)
 *   - itemKey      : (item) => string          (key + uniqueness, default i => i.id)
 *   - itemLabel    : (item) => string          (required primary text)
 *   - itemSubtext  : (item) => string          (optional secondary line)
 *   - placeholder
 *   - multi        : default false
 *   - disabled     : default false
 *   - allowCreate  : show "create new" affordance (single only)
 *   - onCreate     : async (query) => item     (required when allowCreate)
 *   - emptyMessage
 */
export default function SearchableSelect({
  value = null,
  onChange,
  onSearch,
  itemKey = (i) => i?.id,
  itemLabel,
  itemSubtext,
  placeholder = 'Search...',
  multi = false,
  disabled = false,
  allowCreate = false,
  onCreate,
  emptyMessage = 'No results.',
}) {
  const isMulti = !!multi;
  const selectedList = isMulti
    ? (Array.isArray(value) ? value : [])
    : (value ? [value] : []);
  const selectedIds = new Set(selectedList.map((item) => itemKey(item)));

  const [query, setQuery] = useState('');
  const [matches, setMatches] = useState([]);
  const [showDropdown, setShowDropdown] = useState(false);
  const [isSearching, setIsSearching] = useState(false);
  const [isCreating, setIsCreating] = useState(false);
  const [createError, setCreateError] = useState('');
  const debounceRef = useRef(null);
  const wrapperRef = useRef(null);

  useEffect(() => {
    const onDocClick = (e) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target)) {
        setShowDropdown(false);
      }
    };
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, []);

  useEffect(() => {
    if (!isMulti && value && typeof value === 'object') {
      setQuery(itemLabel(value));
    }
    if (isMulti && (!value || value.length === 0)) {
      setQuery('');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isMulti, value, itemLabel]);

  const handleSearch = (raw) => {
    setQuery(raw);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (raw.trim().length < 2) {
      setMatches([]);
      setShowDropdown(false);
      return;
    }
    debounceRef.current = setTimeout(async () => {
      if (typeof onSearch !== 'function') { setMatches([]); return; }
      setIsSearching(true);
      try {
        const results = await onSearch(raw);
        const list = Array.isArray(results) ? results : [];
        const filtered = isMulti
          ? list.filter((item) => !selectedIds.has(itemKey(item)))
          : list;
        setMatches(filtered);
        setShowDropdown(true);
      } catch {
        setMatches([]);
      } finally {
        setIsSearching(false);
      }
    }, 250);
  };

  const pick = (item) => {
    if (isMulti) {
      onChange([...selectedList, item]);
    } else {
      onChange(item);
      setQuery(itemLabel(item));
    }
    setShowDropdown(false);
  };

  const removeOne = (id) => {
    if (isMulti) {
      onChange(selectedList.filter((item) => itemKey(item) !== id));
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
    if (typeof onCreate !== 'function') return;
    if (!query.trim()) {
      setCreateError('A name is required.');
      return;
    }
    setIsCreating(true);
    try {
      const created = await onCreate(query.trim());
      pick(created);
      setQuery('');
    } catch (err) {
      setCreateError(err || 'Could not create.');
    } finally {
      setIsCreating(false);
    }
  };

  const showCreateAffordance = !isMulti && allowCreate && !isCreating &&
    query.trim().length > 0 && matches.length === 0 && !isSearching;

  return (
    <div ref={wrapperRef} className="space-y-2">
      {isMulti && selectedList.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selectedList.map((item) => (
            <span key={itemKey(item)} className="inline-flex items-center gap-1.5 bg-blue-950/60 border border-blue-800 text-blue-300 text-xs px-2.5 py-1 rounded-lg">
              <User size={11} />
              {itemLabel(item)}
              <button type="button" onClick={() => removeOne(itemKey(item))} className="hover:text-red-400 cursor-pointer"><X size={11} /></button>
            </span>
          ))}
          <button type="button" onClick={clearAll} className="text-[10px] text-slate-500 hover:text-slate-300 underline">Clear all</button>
        </div>
      )}

      {!isMulti && value && (
        <div className="flex items-center gap-2 p-2 bg-blue-950/30 border border-blue-900 rounded-lg">
          <Check size={12} className="text-blue-400" />
          <span className="text-xs text-blue-300 truncate">Linked: {itemLabel(value)}</span>
          <button type="button" onClick={clearAll} className="ml-auto text-slate-400 hover:text-white"><X size={12} /></button>
        </div>
      )}

      <div className="relative">
        <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500" />
        <input
          type="text"
          value={query}
          onChange={(e) => handleSearch(e.target.value)}
          onFocus={() => { if (matches.length > 0) setShowDropdown(true); }}
          disabled={disabled}
          placeholder={placeholder}
          className="w-full pl-9 pr-9 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 disabled:opacity-60"
        />
        {(isSearching || isCreating) && (
          <Loader2 size={14} className="absolute right-3 top-1/2 -translate-y-1/2 text-blue-500 animate-spin" />
        )}
        {showDropdown && (
          <div className="absolute z-50 w-full mt-1 bg-slate-900 border border-slate-800 rounded-xl shadow-xl max-h-48 overflow-y-auto">
            {matches.map((item) => (
              <button
                key={itemKey(item)}
                type="button"
                onClick={() => pick(item)}
                className="w-full text-left px-3 py-2 hover:bg-slate-800 text-xs text-white flex items-center gap-2"
              >
                <User size={12} className="text-slate-400" />
                <div className="truncate">
                  <div className="font-semibold truncate">{itemLabel(item)}</div>
                  {itemSubtext && itemSubtext(item) && (
                    <div className="text-[10px] text-slate-500 truncate">{itemSubtext(item)}</div>
                  )}
                </div>
              </button>
            ))}
            {!isSearching && matches.length === 0 && query.trim().length >= 2 && (
              <div className="px-3 py-2 text-xs text-slate-500">{emptyMessage}</div>
            )}
          </div>
        )}
        {showCreateAffordance && (
          <form
            onSubmit={submitNew}
            className="absolute z-50 w-full mt-1 bg-slate-900 border border-slate-800 rounded-xl shadow-xl p-2"
          >
            <button type="submit" className="w-full text-left px-2 py-1.5 hover:bg-slate-800 text-xs text-blue-300 flex items-center gap-2">
              <Plus size={12} /> Create new "{query.trim()}"
            </button>
            {createError && <div className="px-2 text-[10px] text-red-400">{createError}</div>}
          </form>
        )}
      </div>
    </div>
  );
}
