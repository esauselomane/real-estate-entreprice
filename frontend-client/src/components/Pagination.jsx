import { ChevronLeft, ChevronRight } from 'lucide-react';

const buildPageWindow = (current, totalPages) => {
  if (totalPages <= 0) return [];
  const windowSize = 5;
  if (totalPages <= windowSize) {
    return Array.from({ length: totalPages }, (_, i) => i + 1);
  }
  const half = Math.floor(windowSize / 2);
  let start = current - half;
  let end = current + half;
  if (start < 1) { start = 1; end = windowSize; }
  if (end > totalPages) { end = totalPages; start = totalPages - windowSize + 1; }
  const window = [];
  for (let i = start; i <= end; i++) window.push(i);
  return window;
};

export default function Pagination({ page, pageSize, totalCount, onPageChange, className = '' }) {
  const safeTotal = Math.max(0, Number(totalCount) || 0);
  const safeSize = Math.max(1, Number(pageSize) || 1);
  const totalPages = Math.max(1, Math.ceil(safeTotal / safeSize));
  const safePage = Math.min(Math.max(1, Number(page) || 1), totalPages);
  const startIndex = safeTotal === 0 ? 0 : (safePage - 1) * safeSize + 1;
  const endIndex = Math.min(safePage * safeSize, safeTotal);

  const pageWindow = buildPageWindow(safePage, totalPages);
  const showStartEllipsis = pageWindow.length > 0 && pageWindow[0] > 1;
  const showEndEllipsis = pageWindow.length > 0 && pageWindow[pageWindow.length - 1] < totalPages;

  if (safeTotal === 0) {
    return (
      <div className={`flex items-center justify-between gap-3 text-xs text-slate-500 ${className}`}>
        <span>0 results</span>
      </div>
    );
  }

  return (
    <div className={`flex flex-col sm:flex-row items-center justify-between gap-3 text-xs text-slate-400 ${className}`}>
      <span>
        Showing <span className="text-slate-200 font-semibold">{startIndex}</span>–<span className="text-slate-200 font-semibold">{endIndex}</span> of <span className="text-slate-200 font-semibold">{safeTotal}</span>
      </span>
      <div className="flex items-center gap-1">
        <button
          type="button"
          onClick={() => onPageChange(safePage - 1)}
          disabled={safePage <= 1}
          className="px-2.5 py-1.5 rounded-md border border-slate-700 bg-slate-900 hover:bg-slate-800 disabled:opacity-40 disabled:cursor-not-allowed flex items-center gap-1"
        >
          <ChevronLeft size={14} /> Prev
        </button>
        {showStartEllipsis && (
          <>
            <button
              type="button"
              onClick={() => onPageChange(1)}
              className="min-w-[2rem] px-2 py-1.5 rounded-md border border-slate-700 bg-slate-900 hover:bg-slate-800 text-slate-200"
            >
              1
            </button>
            <span className="px-1 text-slate-600">…</span>
          </>
        )}
        {pageWindow.map((p) => (
          <button
            key={p}
            type="button"
            onClick={() => onPageChange(p)}
            className={`min-w-[2rem] px-2 py-1.5 rounded-md border ${p === safePage ? 'border-amber-500 bg-amber-500/10 text-amber-300' : 'border-slate-700 bg-slate-900 hover:bg-slate-800 text-slate-200'}`}
          >
            {p}
          </button>
        ))}
        {showEndEllipsis && (
          <>
            <span className="px-1 text-slate-600">…</span>
            <button
              type="button"
              onClick={() => onPageChange(totalPages)}
              className="min-w-[2rem] px-2 py-1.5 rounded-md border border-slate-700 bg-slate-900 hover:bg-slate-800 text-slate-200"
            >
              {totalPages}
            </button>
          </>
        )}
        <button
          type="button"
          onClick={() => onPageChange(safePage + 1)}
          disabled={safePage >= totalPages}
          className="px-2.5 py-1.5 rounded-md border border-slate-700 bg-slate-900 hover:bg-slate-800 disabled:opacity-40 disabled:cursor-not-allowed flex items-center gap-1"
        >
          Next <ChevronRight size={14} />
        </button>
      </div>
    </div>
  );
}
