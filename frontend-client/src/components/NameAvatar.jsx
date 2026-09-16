import { useState } from 'react';

export default function NameAvatar({ name, src }) {
  const [failedSource, setFailedSource] = useState(null);
  const initials = (name || '?').trim().split(/\s+/).map(part => part[0]).join('').slice(0, 2).toUpperCase();
  return <span className="inline-flex h-8 w-8 shrink-0 items-center justify-center overflow-hidden rounded-full bg-slate-700 text-xs text-white" aria-hidden="true">
    {src && src !== failedSource ? <img src={src} alt="" className="h-full w-full object-cover" onError={() => setFailedSource(src)} /> : initials}
  </span>;
}
