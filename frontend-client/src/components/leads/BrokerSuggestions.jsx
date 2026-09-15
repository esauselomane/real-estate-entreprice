import { useEffect, useState } from 'react';
import { crmService } from '../../services/crmService.js';

export default function BrokerSuggestions({ tenantId, onSelect }) {
  const [snapshot, setSnapshot] = useState(null);
  useEffect(() => {
    let cancelled = false;
    crmService.getSuggestedBrokers({ tenantId }).then(brokers => {
      if (!cancelled) setSnapshot({ tenantId, brokers });
    }).catch(() => { if (!cancelled) setSnapshot({ tenantId, error: true }); });
    return () => { cancelled = true; };
  }, [tenantId]);
  if (!snapshot || snapshot.tenantId !== tenantId) return <p className="text-xs text-slate-400">Finding the highest-scoring brokers…</p>;
  if (snapshot.error) return <p className="text-xs text-amber-400">Suggestions unavailable. You can still choose a broker below.</p>;
  if (!snapshot.brokers.length) return <p className="text-xs text-slate-400">No active brokers available for this agency.</p>;
  return <div className="my-2 space-y-2">
    <p className="text-xs text-slate-400">Suggested by current performance score. Select a broker to assign them.</p>
    {snapshot.brokers.map((broker, index) => <button type="button" key={broker.brokerId}
      onClick={() => onSelect({ brokerId: broker.brokerId, fullName: broker.brokerName })}
      className="block w-full rounded-lg border border-slate-700 p-3 text-left text-xs text-white hover:border-blue-400">
      {broker.brokerName} — {Number(broker.matchScore).toFixed(1)}/100 {index === 0 ? '(Highest score)' : ''}
      <span className="block text-slate-400">{broker.label} · Lead conversion: {Number(broker.conversionRate).toFixed(1)}%</span>
    </button>)}
  </div>;
}
