import { useState } from 'react';

export default function PropertyStaticMap({ latitude, longitude, address }) {
  const [failedUrl, setFailedUrl] = useState(null);
  const key = import.meta.env.VITE_GOOGLE_MAPS_API_KEY;
  const lat = Number(latitude), lng = Number(longitude);
  const valid = latitude != null && longitude != null && Number.isFinite(lat) && Number.isFinite(lng)
    && Math.abs(lat) <= 85 && Math.abs(lng) <= 180 && (lat !== 0 || lng !== 0);
  const params = new URLSearchParams({ size: '640x320', scale: '2', zoom: '15', maptype: 'roadmap', markers: `${lat},${lng}`, key: key || '' });
  const url = `https://maps.googleapis.com/maps/api/staticmap?${params}`;
  if (!valid || !key || failedUrl === url) return <div className="p-6 text-sm text-slate-400">Map unavailable. {address}</div>;
  return <a href={`https://www.google.com/maps/search/?api=1&query=${lat},${lng}`} target="_blank" rel="noreferrer">
    <img src={url} alt={`Map showing ${address || 'property location'}`} className="w-full h-auto rounded-xl" onError={() => setFailedUrl(url)} />
  </a>;
}
