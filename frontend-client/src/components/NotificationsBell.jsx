import { useEffect, useRef, useState } from 'react';
import { Bell, Loader2, ChevronRight } from 'lucide-react';
import { crmService } from '../services/crmService.js';
import { useAppUi } from '../contexts/AppUiContext.jsx';

/**
 * Top-bar notification bell for per-user notifications (created by the
 * "Remind Agent" action on the broker dashboard, or by system events).
 * Polls the CRM service every 30s and shows the unread count as a badge.
 * Clicking an item navigates to the deal/property it links to.
 */
export default function NotificationsBell() {
  const { navigateTo, setBrochureDealTarget } = useAppUi();
  const [items, setItems] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [isOpen, setIsOpen] = useState(false);
  const wrapperRef = useRef(null);
  const seenIdsRef = useRef(new Set());

  const load = async () => {
    setIsLoading(true);
    try {
      const data = await crmService.getNotifications({ limit: 25 });
      const newItems = data.items || [];

      // Cache seen IDs so notifications that were just shown (but not yet
      // explicitly dismissed) are not re-flashed on the next poll cycle.
      newItems.forEach((n) => {
        if (n.IsRead) {
          seenIdsRef.current.add(n.NotificationId);
        }
      });

      setItems(newItems);
      setUnreadCount(data.unreadCount || 0);
    } catch {
      // soft-fail — bell badge is non-critical
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    load();
    const handle = setInterval(load, 30_000);
    return () => clearInterval(handle);
  }, []);

  useEffect(() => {
    if (isOpen) load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen]);

  useEffect(() => {
    const onDoc = (e) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target)) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, []);

   const handleClick = async (n) => {
    // Optimistically mark as seen/ read locally to avoid re-flash on next poll.
    seenIdsRef.current.add(n.NotificationId);
    setItems((prev) => prev.map((x) => x.NotificationId === n.NotificationId ? { ...x, IsRead: true } : x));
    setUnreadCount((prev) => Math.max(0, prev - 1));

    if (!n.IsRead) {
      try {
        await crmService.markNotificationRead(n.NotificationId);
      } catch { /* ignore */ }
    }
    if (n.LinkType === 'deal') {
      navigateTo('crm');
      setBrochureDealTarget({ dealId: Number(n.LinkId), autoOpen: true });
    } else if (n.LinkType === 'property') {
      navigateTo('search');
    }
    setIsOpen(false);
  };

  return (
    <div className="relative" ref={wrapperRef}>
      <button
        type="button"
        onClick={() => setIsOpen((v) => !v)}
        className="relative w-10 h-10 rounded-xl bg-slate-900 border border-slate-800 hover:border-slate-700 text-slate-300 hover:text-white flex items-center justify-center transition-colors cursor-pointer"
        title="Notifications"
      >
        <Bell size={18} />
        {unreadCount > 0 && (
          <span className="absolute -top-1 -right-1 min-w-[18px] h-[18px] px-1 rounded-full bg-red-500 text-white text-[10px] font-bold flex items-center justify-center">
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      {isOpen && (
        <div className="absolute right-0 mt-2 w-80 max-h-96 overflow-y-auto bg-slate-900 border border-slate-800 rounded-xl shadow-2xl z-50">
          <div className="p-3 border-b border-slate-800 flex items-center justify-between">
            <h4 className="text-sm font-semibold text-white">Notifications</h4>
            {isLoading && <Loader2 size={12} className="animate-spin text-blue-400" />}
          </div>
          {items.length === 0 ? (
            <div className="p-4 text-xs text-slate-500 text-center">No notifications yet.</div>
          ) : (
            <ul className="divide-y divide-slate-800">
              {items.map((n) => (
                <li key={n.NotificationId}>
                  <button
                    type="button"
                    onClick={() => handleClick(n)}
                    className={`w-full text-left p-3 hover:bg-slate-800/60 cursor-pointer flex items-start gap-2 ${!n.IsRead ? 'bg-slate-800/30' : ''}`}
                  >
                    <div className={`w-2 h-2 mt-1.5 rounded-full ${!n.IsRead ? 'bg-blue-400' : 'bg-slate-700'}`} />
                    <div className="flex-1 min-w-0">
                      <div className="text-xs font-semibold text-white truncate">{n.Title}</div>
                      {n.Body && <div className="text-[11px] text-slate-400 truncate">{n.Body}</div>}
                      <div className="text-[10px] text-slate-500 mt-0.5">{new Date(n.CreatedAt).toLocaleString('en-ZA')}</div>
                    </div>
                    <ChevronRight size={12} className="text-slate-600 mt-1" />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
