"use client";

import { X } from "lucide-react";
import { useEffect } from "react";

interface VideoModalProps {
  open: boolean;
  onClose: () => void;
}

export default function VideoModal({ open, onClose }: VideoModalProps) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    const prev = document.documentElement.style.overflow;
    window.addEventListener("keydown", onKey);
    document.documentElement.style.overflow = "hidden";
    return () => {
      window.removeEventListener("keydown", onKey);
      document.documentElement.style.overflow = prev;
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-slate-950/60 p-4 outline-none"
      role="dialog"
      aria-modal="true"
      aria-label="Watch overview"
      onClick={onClose}
    >
      <div
        className="relative w-full max-w-4xl rounded-2xl bg-black shadow-xl ring-1 ring-slate-700"
        onClick={(e) => e.stopPropagation()}
      >
        <button
          type="button"
          className="absolute top-3 right-3 rounded-xl bg-slate-800 p-2 text-slate-200 hover:bg-slate-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
          aria-label="Close video"
          onClick={onClose}
        >
          <X className="h-5 w-5" />
        </button>
        <video
          src="/demo.mp4"
          controls
          autoPlay
          muted
          playsInline
          className="h-full w-full rounded-2xl object-contain"
        >
          Your browser does not support the video element.
        </video>
      </div>
    </div>
  );
}
