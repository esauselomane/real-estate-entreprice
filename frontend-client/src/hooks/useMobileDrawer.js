import { useEffect, useState } from "react";
const mobileQuery = "(max-width: 767px)";
export default function useMobileDrawer(drawerRef, open, onClose) {
  const [mobile, setMobile] = useState(
    () => window.matchMedia(mobileQuery).matches,
  );
  useEffect(() => {
    const query = window.matchMedia(mobileQuery);
    function updateViewport(event) {
      setMobile(event.matches);
      if (!event.matches) onClose(false);
    }
    query.addEventListener("change", updateViewport);
    return () => query.removeEventListener("change", updateViewport);
  }, [onClose]);
  useEffect(() => {
    if (!mobile || !open) return;
    const drawer = drawerRef.current;
    const previouslyFocused = document.activeElement;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const focusableSelector =
      'button:not([disabled]), a[href], input:not([disabled]), select:not([disabled]), [tabindex="0"]';
    function visibleControls() {
      return [...drawer.querySelectorAll(focusableSelector)].filter(
        (element) => element.getClientRects().length > 0,
      );
    }
    visibleControls()[0]?.focus();
    function handleKeyDown(event) {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose(false);
      }
      if (event.key !== "Tab") return;
      const controls = visibleControls();
      const first = controls[0];
      const last = controls[controls.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last?.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first?.focus();
      }
    }
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener("keydown", handleKeyDown);
      if (previouslyFocused?.isConnected) previouslyFocused.focus();
    };
  }, [drawerRef, mobile, open, onClose]);
  return mobile;
}
