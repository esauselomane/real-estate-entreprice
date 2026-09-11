import { Loader2 } from 'lucide-react';

export default function LoadingOverlay({ label = 'Loading…', subtitle = '' }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/80 backdrop-blur-sm">
      <div className="flex flex-col items-center gap-4">
        <Loader2 className="w-10 h-10 text-blue-500 animate-spin" />
        <div className="text-center">
          <p className="text-sm font-medium text-slate-300">{label}</p>
          {subtitle && <p className="text-xs text-slate-500 mt-1">{subtitle}</p>}
        </div>
      </div>
    </div>
  );
}
