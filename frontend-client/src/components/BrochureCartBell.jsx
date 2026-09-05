import { useEffect, useRef, useState } from 'react';
import { Bell, X, Loader2 } from 'lucide-react';
import { crmService } from '../services/crmService.js';
import { useAppUi } from '../contexts/AppUiContext.jsx';

/**
 * Top-bar notification bell that lists every active brochure cart for the
 * current tenant. Clicking a cart sets the brochure target and navigates
 * the user to the crm view (where BrochureBuilderWorkspace mounts).
 *
 * Active = `IsActive && !IsDeleted` (the backend already filters those out
 * for us, so we just trust the response).
 */
export default function BrochureCartBell() {
  const [carts, setCarts] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const wrapperRef = useRef(null);
  const { setBrochureDealTarget, navigateTo } = useAppUi();

  // 🛑 Mount-guard: when App crashes and remounts in a tight loop, this bell
  // remounts each tick and would fire loadCarts() instantly every time,
  // producing a request storm. The guard enforces a minimum cooldown between
  // mount-triggered fetches AND a single in-flight slot so we can never fire
  // two requests concurrently.
  const lastFetchRef = useRef(0);
  const inFlightRef = useRef(false);

  // Load the active-cart list. Refresh when the dropdown opens and on a
  // slow polling interval (every 30s) so the badge stays roughly current
  // while the user is in the app.
  const loadCarts = async () => {
    if (inFlightRef.current) return; // de-dup concurrent mounts
    const now = Date.now();
    if (now - lastFetchRef.current < 2_000) return; // 2s mount-cooldown
    inFlightRef.current = true;
    lastFetchRef.current = now;
    setIsLoading(true);
    setError('');
    try {
      const list = await crmService.getActiveBrochureCards();
      setCarts(Array.isArray(list) ? list : []);
    } catch (err) {
      setError(err?.message || 'Failed to load brochure carts.');
    } finally {
      inFlightRef.current = false;
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadCarts();
    const handle = setInterval(loadCarts, 30_000);
    return () => clearInterval(handle);
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (isOpen) loadCarts();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen]);

  // Close on outside click
  useEffect(() => {
    const onDocClick = (e) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target)) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, []);

  const openCart = (cart) => {
    // The brochure workspace expects a deal-shaped object; we have a card
    // with a `dealId`. Pass the card through with dealId intact and let
    // BrochureBuilderWorkspace resolve the deal via getBrochureCardByDeal.
    const dealShape = { dealId: cart.dealId, linkedPropertyId: cart.propertyId };
    setBrochureDealTarget(dealShape);
    navigateTo('crm');
    setIsOpen(false);
  };

  const badgeCount = carts.length;

  return (
    <div ref={wrapperRef} className="relative">
      <button
        type="button"
        onClick={() => setIsOpen((v) => !v)}
        className="relative p-2 rounded-xl border border-slate-800 bg-slate-900/60 hover:bg-slate-800 hover:border-blue-700 text-slate-300 transition-colors"
        title="Brochure carts"
        aria-label={`Open brochure carts (${badgeCount})`}
      >
        <Bell size={16} />
        {badgeCount > 0 && (
          <span className="absolute -top-1 -right-1 min-w-[18px] h-[18px] px-1 rounded-full bg-blue-600 text-white text-[10px] font-bold flex items-center justify-center border border-slate-900">
            {badgeCount > 99 ? '99+' : badgeCount}
          </span>
        )}
      </button>

      {isOpen && (
        <div className="absolute right-0 mt-2 w-80 bg-slate-900 border border-slate-800 rounded-2xl shadow-2xl z-50 overflow-hidden">
          <header className="px-4 py-3 border-b border-slate-800 flex items-center justify-between">
            <div>
              <h4 className="text-xs font-bold text-white uppercase tracking-wider">Brochure Carts</h4>
              <p className="text-[10px] text-slate-500 mt-0.5">Active carts for this tenant</p>
            </div>
            <button type="button" onClick={() => setIsOpen(false)} className="text-slate-400 hover:text-white"><X size={14} /></button>
          </header>

          {error && (
            <div className="px-4 py-3 text-xs text-red-400 border-b border-slate-800">{error}</div>
          )}

          {isLoading && carts.length === 0 ? (
            <div className="px-4 py-6 flex items-center justify-center gap-2 text-xs text-slate-400">
              <Loader2 size={14} className="animate-spin" /> Loading carts…
            </div>
          ) : carts.length === 0 ? (
            <div className="px-4 py-6 text-xs text-slate-500 text-center">No active brochure carts.</div>
          ) : (
            <ul className="max-h-80 overflow-y-auto divide-y divide-slate-800">
              {carts.map((cart) => (
                <li key={cart.cardId}>
                  <button
                    type="button"
                    onClick={() => openCart(cart)}
                    className="w-full px-4 py-3 hover:bg-slate-800/60 transition-colors text-left flex flex-col gap-1"
                  >
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-bold text-white">Cart #{cart.cardId} • Deal DL-{cart.dealId}</span>
                      <span className="text-[10px] text-slate-500">{cart.status || 'Open'}</span>
                    </div>
                    {cart.propertyReference && (
                      <span className="text-[10px] text-slate-400 truncate">{cart.propertyReference}</span>
                    )}
                    {cart.clientName && (
                      <span className="text-[10px] text-blue-300 truncate">For: {cart.clientName}</span>
                    )}
                  </button>
                </li>
              ))}
            </ul>
          )}

          <footer className="px-4 py-2 border-t border-slate-800 text-[10px] text-slate-500 text-center">
            Polls every 30s
          </footer>
        </div>
      )}
    </div>
  );
}
