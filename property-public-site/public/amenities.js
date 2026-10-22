const icons = {
  petsallowed: '<circle cx="8" cy="6" r="2"/><circle cx="16" cy="6" r="2"/><circle cx="4" cy="11" r="2"/><circle cx="20" cy="11" r="2"/><path d="M7 20c-4-2 1-10 5-10s9 8 5 10c-2 1-3-1-5-1s-3 2-5 1Z"/>',
  swimmingpool: '<path d="M3 20c3-3 3 3 6 0s3 3 6 0 3 3 6 0M7 16V5a2 2 0 0 1 4 0m4 11V5a2 2 0 0 1 4 0M7 9h8M7 14h8"/>',
  parking: '<rect x="3" y="3" width="18" height="18" rx="3"/><path d="M10 17V7h4a3 3 0 0 1 0 6h-4"/>',
  security: '<path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6Z"/><path d="m8 12 3 3 5-6"/>',
  gym: '<path d="M6 5v14M3 8v8M18 5v14M21 8v8M6 12h12"/>',
  deposit: '<rect x="3" y="6" width="18" height="14" rx="2"/><path d="M7 6V4h10v2M3 11h18M14 15h4"/>',
  default: '<path d="m5 12 4 4L19 6"/>',
};

export function amenityIcon(name) {
  const key = String(name || '').toLowerCase().replace(/[^a-z]/g, '');
  const icon = icons[key] || (key.includes('pool') ? icons.swimmingpool : key.includes('pet') ? icons.petsallowed : key.includes('parking') ? icons.parking : icons.default);
  return `<svg class="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${icon}</svg>`;
}

export function renderAmenities(amenities, escapeHtml) {
  if (!amenities?.length) return '<p class="form-help">Amenity details on request.</p>';
  function renderAmenity(item) {
    let value = 'Details on request';
    if (item.booleanValue != null) value = item.booleanValue ? 'Yes' : 'No';
    if (item.name === 'PetsAllowed' && item.booleanValue != null) value = item.booleanValue ? 'Allowed' : 'Not allowed';
    if (item.numberValue != null) value = `${item.numberValue}${item.name === 'Parking' ? (item.numberValue === 1 ? ' bay' : ' bays') : ''}`;
    return `<li>${amenityIcon(item.name)}<span><strong>${escapeHtml(item.label || item.name)}</strong><small>${escapeHtml(value)}</small></span></li>`;
  }
  return `<ul class="amenity-list">${amenities.map(renderAmenity).join('')}</ul>`;
}
