import { useState, useEffect, useRef } from 'react';
import { Search, User, X, Loader2, Check } from 'lucide-react';
import { crmService } from '../services/crmService.js';

/**
 * Reusable broker picker — single or multi-select, no inline create.
 *
 * The data source is crmService.getBrokers() (small list, no debounced
 * search). Always stores broker ids upstream; names are resolved from the
 * picked broker's `fullName` for display.
 *
 * Props:
 *   - value     : null | Broker | Broker[]   (Broker = { brokerId, fullName, email })
 *   - onChange  : (next) => void
 *   - placeholder
 *   - multi     : default false
 *   - disabled
 */
export default function BrokerPicker({
  value = null,
  onChange,
  placeholder = 'Search brokers by name or email...',
  multi = false,
  disabled = false
}) {
  const isMulti = !!multi;
  const selectedList = isMulti
    ? (Array.isArray(value) ? value : [])
    : (value ? [value] : []);
  const selectedIds = new Set(selectedList.map((b) => b.brokerId));

  const [query, setQuery] = useState('');
  const [allBrokers, setAllBrokers] = useState([]);
  const [showDropdown, setShowDropdown] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [loadError, setLoadError] = useState('');
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

  // Load the broker roster once. The list is small (a handful per tenant)
  // so we skip pagination and use the first page.
  useEffect(() => {
    let cancelled = false;
    setIsLoading(true);
    setLoadError('');
    crmService
      .getBrokers(1)
      .then((rows) => {
        if (cancelled) return;
        const list = Array.isArray(rows) ? rows : (rows?.items || []);
        setAllBrokers(list);
      })
      .catch((err) => {
        if (cancelled) return;
        setLoadError(err?.message || 'Failed to load brokers.');
      })
      .finally(() => { if (!cancelled) setIsLoading(false); });
    return () => { cancelled = true; };
  }, []);

  // Single-mode: mirror the picked broker's name into the visible query
  useEffect(() => {
    if (!isMulti && value && typeof value === 'object') {
      setQuery(value.fullName || '');
    }
    if (isMulti && (!value || value.length === 0)) {
      setQuery('');
    }
  }, [isMulti, value?.brokerId, value?.fullName, value?.length]);

  const matches = (() => {
    const q = query.trim().toLowerCase();
    if (!q) {
      return isMulti ? allBrokers.filter((b) => !selectedIds.has(b.brokerId)) : allBrokers;
    }
    return allBrokers.filter((b) => {
      if (isMulti && selectedIds.has(b.brokerId)) return false;
      return (
        (b.fullName || '').toLowerCase().includes(q) ||
        (b.email || '').toLowerCase().includes(q)
      );
    });
  })();

  const pick = (broker) => {
    if (isMulti) {
      onChange([...selectedList, broker]);
    } else {
      onChange(broker);
      setQuery(broker.fullName || '');
    }
    setShowDropdown(false);
  };

  const removeOne = (brokerId) => {
    if (isMulti) {
      onChange(selectedList.filter((b) => b.brokerId !== brokerId));
    } else {
      onChange(null);
      setQuery('');
    }
  };

  const clearAll = () => {
    onChange(isMulti ? [] : null);
    setQuery('');
  };

  return (
    <div ref={wrapperRef} className="space-y-2">
      {isMulti && selectedList.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selectedList.map((b) => (
            <span key={b.brokerId} className="inline-flex items-center gap-1.5 bg-purple-950/60 border border-purple-800 text-purple-300 text-xs px-2.5 py-1 rounded-lg">
              <User size={11} />
              {b.fullName || b.brokerId}
              <button type="button" onClick={() => removeOne(b.brokerId)} className="hover:text-red-400 cursor-pointer"><X size={11} /></button>
            </span>
          ))}
          <button type="button" onClick={clearAll} className="text-[10px] text-slate-500 hover:text-slate-300 underline">Clear all</button>
        </div>
      )}

      {!isMulti && value && (
        <div className="flex items-center gap-2 p-2 bg-purple-950/30 border border-purple-900 rounded-lg">
          <Check size={12} className="text-purple-400" />
          <span className="text-xs text-purple-300">Assigned: {value.fullName || value.brokerId}</span>
          <button type="button" onClick={clearAll} className="ml-auto text-slate-400 hover:text-white"><X size={12} /></button>
        </div>
      )}

      <div className="relative">
        <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500" />
        <input
          type="text"
          value={query}
          onChange={(e) => { setQuery(e.target.value); setShowDropdown(true); }}
          onFocus={() => setShowDropdown(true)}
          disabled={disabled}
          placeholder={placeholder}
          className="w-full pl-9 pr-9 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 disabled:opacity-60"
        />
        {isLoading && <Loader2 size={14} className="absolute right-3 top-1/2 -translate-y-1/2 text-blue-500 animate-spin" />}
        {showDropdown && (
          <div className="absolute z-50 w-full mt-1 bg-slate-900 border border-slate-800 rounded-xl shadow-xl max-h-48 overflow-y-auto">
            {loadError && <div className="px-3 py-2 text-xs text-red-400">{loadError}</div>}
            {!loadError && matches.length === 0 && (
              <div className="px-3 py-2 text-xs text-slate-500">No brokers match.</div>
            )}
            {matches.map((b) => (
              <button
                key={b.brokerId}
                type="button"
                onClick={() => pick(b)}
                className="w-full text-left px-3 py-2 hover:bg-slate-800 text-xs text-white flex items-center gap-2"
              >
                <User size={12} className="text-slate-400" />
                <div>
                  <div className="font-semibold">{b.fullName || b.brokerId}</div>
                  <div className="text-[10px] text-slate-500">{b.email} {b.role ? `• ${b.role}` : ''}</div>
                </div>
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
