import { useEffect, useRef, useState } from 'react';
import { Bell, Loader2, ChevronRight } from 'lucide-react';
import { crmService } from '../services/crmService.js';
import { useAppUi } from '../contexts/AppUiContext.jsx';

/**
 * Robust date formatter for notification timestamps. Returns a human-readable
 * relative label ("Just now", "5 min ago", "Yesterday", "2 days ago") and
 * falls back gracefully when the raw value is null, undefined, or an
 * unparseable ISO string — instead of rendering the literal "Invalid Date".
 */
const formatNotificationTime = (value) => {
  if (!value) return 'Just now';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'Just now';

  const now = new Date();
  const diffMs = now - date;
  const diffMin = Math.floor(diffMs / 60_000);
  const diffHr = Math.floor(diffMs / 3_600_000);
  const diffDay = Math.floor(diffMs / 86_400_000);

  if (diffMin < 1) return 'Just now';
  if (diffMin < 60) return `${diffMin} min ago`;
  if (diffHr < 24) return `${diffHr} hr ago`;
  if (diffDay === 1) return 'Yesterday';
  if (diffDay < 7) return `${diffDay} days ago`;
  return date.toLocaleDateString('en-ZA', { month: 'short', day: 'numeric' });
};

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
      const newItems = Array.isArray(data?.items) ? data.items.filter(Boolean) : [];

      // Cache seen IDs so notifications that were just shown (but not yet
      // explicitly dismissed) are not re-flashed on the next poll cycle.
      newItems.forEach((n) => {
        if (n.isRead) {
          seenIdsRef.current.add(n.notificationId);
        }
      });

      setItems(newItems.filter(item => !item.isRead && !seenIdsRef.current.has(item.notificationId)));
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
    if (isOpen) {
      // Mark all as read the moment the dropdown opens so the badge
      // disappears and the list reflects the consumed state.
      load();
    }
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
    if (!n.isRead) {
      try {
        await crmService.markNotificationRead(n.notificationId);
        seenIdsRef.current.add(n.notificationId);
        setItems(previous => previous.filter(item => item.notificationId !== n.notificationId));
        setUnreadCount(previous => Math.max(0, previous - 1));
      } catch {
        return; // Leave it unread when persistence fails, so it can be retried.
      }
    }
    if (n.linkType === 'deal') {
      navigateTo('crm');
      setBrochureDealTarget({ dealId: Number(n.linkId), autoOpen: true });
    } else if (n.linkType === 'brochure') {
      navigateTo('deals');
    } else if (n.linkType === 'property') {
      navigateTo('search');
    }
    // 🛎️ Close the dropdown immediately on click so the notification is
    // visually consumed — it should not linger as "still open".
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
        <div className="workspace-header-popover absolute right-0 mt-2 w-80 max-h-96 overflow-y-auto bg-slate-900 border border-slate-800 rounded-xl shadow-2xl z-50">
          <div className="p-3 border-b border-slate-800 flex items-center justify-between">
            <h4 className="text-sm font-semibold text-white">Notifications</h4>
            {isLoading && <Loader2 size={12} className="animate-spin text-blue-400" />}
          </div>
          {items.length === 0 ? (
            <div className="p-4 text-xs text-slate-500 text-center">No notifications yet.</div>
          ) : (
            <ul className="divide-y divide-slate-800">
              {items.map((n) => (
                <li key={n.notificationId}>
                  <button
                    type="button"
                    onClick={() => handleClick(n)}
                    className={`w-full text-left p-3 hover:bg-slate-800/60 cursor-pointer flex items-start gap-2 ${!n.isRead ? 'bg-slate-800/30' : ''}`}
                  >
                    <div className={`w-2 h-2 mt-1.5 rounded-full ${!n.isRead ? 'bg-blue-400' : 'bg-slate-700'}`} />
                    <div className="flex-1 min-w-0">
                      <div className="text-xs font-semibold text-white truncate">{n.title}</div>
                      {n.body && <div className="text-[11px] text-slate-400 truncate">{n.body}</div>}
                      <div className="text-[10px] text-slate-500 mt-0.5">{formatNotificationTime(n.createdAt)}</div>
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
