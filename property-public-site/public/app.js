import { amenityIcon, renderAmenities } from './amenities.js';
import { bindLocationSearch } from './locations.js';
import { renderOwnerPage } from './owner.js';

export const main = document.querySelector('#main');
export let propertyTypes = ['Residential', 'Commercial', 'Industrial', 'Retail', 'Land', 'Room share/let'];
const currentUrl = new URL(location.href);
let currentSearchController;

export function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, replaceHtmlCharacter);
}
function replaceHtmlCharacter(character) {
  return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[character];
}

export async function api(route, options = {}) {
  const response = await fetch(`/api/${route}`, { ...options, credentials: 'same-origin', headers: { 'Content-Type': 'application/json', ...options.headers } });
  if (response.status === 204) return null;
  let data;
  try { data = await response.json(); }
  catch { throw new Error('The service returned an unexpected response. Please try again.'); }
  if (!response.ok) {
    const validationErrors = Object.values(data.errors || {}).flat().join(' ');
    const error = new Error(data.message || validationErrors || data.title || 'Unable to complete this request. Please try again.');
    error.status = response.status;
    throw error;
  }
  return data;
}

export function money(value) {
  if (value == null) return 'Price on request';
  return new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR', maximumFractionDigits: 0 }).format(value);
}

export function imageSource(value) {
  if (typeof value !== 'string') return '/property-scene.svg';
  if (/^https:\/\//i.test(value) || /^data:image\/(jpeg|png|webp);base64,/i.test(value)) return escapeHtml(value);
  return '/property-scene.svg';
}

export function listingLabel(property) {
  const labels = [];
  if (property.isForSale) labels.push('For sale');
  if (property.isForRent) labels.push('To let');
  if (property.roomLettingType === 'PrivateRoom') labels.push('Private room');
  if (property.roomLettingType === 'SharedRoom') labels.push('Room share');
  return labels.join(' · ');
}

export function propertyCard(property, management = false) {
  const showRental = currentUrl.searchParams.get('listingType') === 'Rent' || !property.isForSale;
  const price = showRental ? property.rentalPrice : property.salePrice;
  const classification = listingLabel(property);
  const href = management ? '#' : `/properties/${property.id}`;
  const tag = management ? 'div' : 'a';
  const image = property.thumb || property.images?.[0];
  return `<article class="property-card"><${tag} ${management ? '' : `href="${href}"`}>
    <div class="card-media"><img ${image ? '' : 'class="placeholder"'} src="${imageSource(image)}" alt="${image ? escapeHtml(property.address) : 'Property photo not supplied'}" loading="lazy" referrerpolicy="no-referrer"><div class="badges"><span class="badge">${classification}</span>${property.isHot ? '<span class="badge hot">↗ Hot property</span>' : ''}${property.isNearby ? `<span class="badge nearby">Nearby · ${property.distanceKm} km</span>` : ''}</div></div>
    <div class="card-content"><div class="price">${money(price)}${showRental && price != null ? '<small> / month</small>' : ''}</div>${secondaryPrice(property, showRental)}<h3>${escapeHtml(property.address)}</h3><p>${escapeHtml(property.propertyType)} ${classification.toLowerCase()}</p><div class="card-facts"><span>▧ ${escapeHtml(property.erfSize || 'Size on request')}</span><span>▤ ${property.units.length} unit${property.units.length === 1 ? '' : 's'}</span>${property.distanceKm != null && !property.isNearby ? `<span>${property.distanceKm} km away</span>` : ''}</div></div>
    </${tag}>${management ? `<div class="status-line"><span>${escapeHtml(property.reviewStatus)}</span><button type="button" class="plain-button edit-submission" data-id="${property.id}">Edit listing</button>${property.isPublicListing ? `<button type="button" class="plain-button withdraw-submission" data-id="${property.id}">Remove from website</button>` : ''}</div>` : ''}</article>`;
}

function secondaryPrice(property, showRental) {
  if (!property.isForSale || !property.isForRent) return '';
  if (showRental) return `<p>Also for sale: ${money(property.salePrice)}</p>`;
  return `<p>Also to let: ${money(property.rentalPrice)} / month</p>`;
}

function typeOptions(selected = '') {
  return '<option value="">All property types</option>' + propertyTypes.map(type => `<option ${selected === type ? 'selected' : ''}>${escapeHtml(type)}</option>`).join('');
}

function searchForm(parameters = new URLSearchParams()) {
  const listingType = parameters.get('listingType') || 'Sale';
  return `<form id="property-search" class="search-panel" action="/properties">
    <div class="tabs" role="group" aria-label="Listing purpose"><button type="button" class="tab ${listingType === 'Sale' ? 'active' : ''}" data-listing-type="Sale" aria-pressed="${listingType === 'Sale'}">For sale</button><button type="button" class="tab ${listingType === 'Rent' ? 'active' : ''}" data-listing-type="Rent" aria-pressed="${listingType === 'Rent'}">To let</button><button type="button" class="tab ${listingType === 'Both' ? 'active' : ''}" data-listing-type="Both" aria-pressed="${listingType === 'Both'}">Sale &amp; to let</button><button type="button" class="tab ${listingType === 'All' ? 'active' : ''}" data-listing-type="All" aria-pressed="${listingType === 'All'}">All listings</button></div>
    <input type="hidden" name="locationId" value="${escapeHtml(parameters.get('locationId') || '')}">
    <input type="hidden" name="listingType" value="${escapeHtml(listingType)}">
    <div class="search-fields"><label>Where would you like to look?<input name="location" type="search" maxlength="200" placeholder="Select suburb, city, region and country" value="${escapeHtml(parameters.get('location') || '')}"></label>
    <label>Property type<select name="propertyType">${typeOptions(parameters.get('propertyType'))}</select></label>
    <label>Min price (R)<input name="minPrice" type="number" min="0" max="999999999999" placeholder="No minimum" value="${escapeHtml(parameters.get('minPrice') || '')}"></label>
    <label>Max price (R)<input name="maxPrice" type="number" min="0" max="999999999999" placeholder="No maximum" value="${escapeHtml(parameters.get('maxPrice') || '')}"></label>
    <button class="button primary" type="submit">⌕ &nbsp; Search</button></div>
    <div class="search-options"><label class="radius-select">Rooms <select name="roomLettingType"><option value="All">All</option><option value="PrivateRoom" ${parameters.get('roomLettingType') === 'PrivateRoom' ? 'selected' : ''}>Private room</option><option value="SharedRoom" ${parameters.get('roomLettingType') === 'SharedRoom' ? 'selected' : ''}>Room share</option></select></label><span class="form-help">Choose nearby areas on the results page.</span><label class="radius-select">Within <select name="radiusKm"><option value="5">5 km</option><option value="15" ${!parameters.has('radiusKm') || parameters.get('radiusKm') === '15' ? 'selected' : ''}>15 km</option><option value="30" ${parameters.get('radiusKm') === '30' ? 'selected' : ''}>30 km</option><option value="50" ${parameters.get('radiusKm') === '50' ? 'selected' : ''}>50 km</option></select></label><label class="check-label"><input type="checkbox" name="hotOnly" ${parameters.get('hotOnly') === 'true' ? 'checked' : ''}>Hot properties only</label></div>
  </form>`;
}

function bindSearch() {
  const form = document.querySelector('#property-search');
  bindLocationSearch(form);
  form.addEventListener('submit', submitSearch);
  form.querySelectorAll('[data-listing-type]').forEach(bindPurposeButton);
  function bindPurposeButton(button) { button.addEventListener('click', selectPurpose); }
  function selectPurpose(event) {
    form.elements.listingType.value = event.currentTarget.dataset.listingType;
    form.querySelectorAll('[data-listing-type]').forEach(updatePurposeButton);
  }
  function updatePurposeButton(button) {
    const selected = button.dataset.listingType === form.elements.listingType.value;
    button.classList.toggle('active', selected); button.setAttribute('aria-pressed', String(selected));
  }
}

function submitSearch(event) {
  event.preventDefault();
  const form = event.currentTarget;
  if (form.elements.minPrice.value && form.elements.maxPrice.value && Number(form.elements.minPrice.value) > Number(form.elements.maxPrice.value)) {
    form.elements.maxPrice.setCustomValidity('Maximum price must be greater than or equal to minimum price.');
    form.elements.maxPrice.reportValidity();
    form.elements.maxPrice.addEventListener('input', clearPriceValidity, { once: true });
    return;
  }
  const parameters = new URLSearchParams(new FormData(form));
  const sameLocation = parameters.get('location') === currentUrl.searchParams.get('location') && parameters.get('locationId') === (currentUrl.searchParams.get('locationId') || '');
  const selectedAreas = sameLocation ? currentUrl.searchParams.getAll('selectedNearbyAreas') : [];
  selectedAreas.forEach(appendSelectedArea);
  const selectedIds = sameLocation ? currentUrl.searchParams.getAll('selectedNearbyLocationIds') : [];
  selectedIds.forEach(id => parameters.append('selectedNearbyLocationIds', id));
  parameters.set('includeNearby', String(selectedAreas.length > 0 || selectedIds.length > 0));
  function appendSelectedArea(area) { parameters.append('selectedNearbyAreas', area); }
  parameters.set('hotOnly', String(form.elements.hotOnly.checked));
  for (const [key, value] of [...parameters]) { if (!value) parameters.delete(key); }
  location.href = `/properties?${parameters}`;
}
function clearPriceValidity(event) { event.currentTarget.setCustomValidity(''); }

async function renderHome() {
  main.innerHTML = `<section class="hero"><div class="wrap hero-layout"><div><div class="eyebrow">A place for every possibility</div><h1>Space for your<br><em>next chapter.</em></h1><p>From a first home to your next business address. Find a property that fits, and connect directly with its owner.</p></div><div class="hero-art"><img src="/property-scene.svg" alt="An illustrated home surrounded by greenery"><div class="art-note">YOUR NEXT MOVE STARTS HERE<strong>Find a place. Make it yours.</strong></div></div></div></section>
    <div class="wrap search-overlap">${searchForm(new URLSearchParams({ listingType: 'All', propertyType: 'Room share/let' }))}</div>
    <section class="wrap section"><div class="section-heading"><div><div class="eyebrow">Find your kind of space</div><h2>What are you looking for?</h2></div></div><div class="type-grid">${propertyTypes.map(renderTypeCard).join('')}</div></section>
    <section class="wrap section"><div class="section-heading"><div><div class="eyebrow">Worth a closer look</div><h2>Hot rooms</h2><p>Room listings selected by the agency’s administrators.</p></div><a class="text-link" href="/properties?hotOnly=true&amp;propertyType=Room%20share%2Flet&amp;listingType=All">View all hot rooms ↗</a></div><div id="hot-properties" class="property-grid" aria-live="polite"><p class="empty">Finding the latest hot properties…</p></div></section>
    <section class="wrap section"><div class="section-heading"><div><div class="eyebrow">Your search starts here</div><h2>Find your next room</h2><p>Explore the latest approved private rooms and room shares.</p></div><a class="text-link" href="/properties?listingType=All">Browse all properties ↗</a></div><div id="latest-properties" class="property-grid" aria-live="polite"><p class="empty">Loading the latest properties…</p></div></section>
    <div class="wrap"><section class="owner-cta"><div><div class="eyebrow">Your property. Your next opportunity.</div><h2>Have a space someone<br>will call their own?</h2><p>List your home, land or commercial space with us. Submit your property for agency review, then connect directly with interested buyers and tenants.</p></div><div class="cta-actions"><a class="button" href="/sell">Sell or rent through us ↗</a></div></section></div>
    <section class="wrap trust-row"><div class="trust-item"><span class="trust-icon">✓</span><strong>Reviewed before it goes live</strong><p>Every listing is approved by the agency before it appears here.</p></div><div class="trust-item"><span class="trust-icon">⌖</span><strong>A little further, a little more choice</strong><p>Explore nearby areas alongside the location you searched for.</p></div><div class="trust-item"><span class="trust-icon">↗</span><strong>Speak directly to the owner</strong><p>Find the details you need and start the conversation yourself.</p></div></section>`;
  bindSearch();
  await Promise.allSettled([loadCards('properties/hot?pageSize=5&randomize=true&propertyType=Room%20share%2Flet&listingType=All', '#hot-properties', 'No hot properties just yet. Explore the latest listings below.'), loadCards('properties?pageSize=6&propertyType=Room%20share%2Flet&listingType=All', '#latest-properties', 'New properties are on their way. Check back soon.')]);
}
function renderTypeCard(type) {
  const symbols = { Residential: '⌂', Commercial: '▥', Industrial: '▤', Retail: '▱', Land: '⌖', 'Room share/let': '⌂' };
  const subtitles = { Residential: 'Find a home', Commercial: 'Room to grow', Industrial: 'Built for business', Retail: 'Open new doors', Land: 'Start something new', 'Room share/let': 'Find a room' };
  return `<a class="type-card" href="/properties?propertyType=${encodeURIComponent(type)}&listingType=All"><span class="type-symbol" aria-hidden="true">${symbols[type] || '▧'}</span><div><strong>${escapeHtml(type)}</strong><span>${subtitles[type] || 'Explore properties'}</span></div></a>`;
}
async function loadCards(route, selector, emptyMessage) {
  const target = document.querySelector(selector);
  try {
    const data = await api(route);
    target.innerHTML = data.items.length ? data.items.map(item => propertyCard(item)).join('') : `<div class="empty">${escapeHtml(emptyMessage)}</div>`;
  } catch (error) { target.innerHTML = `<div class="notice error full-width" role="alert">${escapeHtml(error.message)} <button type="button" class="plain-button retry-cards">Try again</button></div>`; target.querySelector('button').addEventListener('click', retry); }
  function retry() { loadCards(route, selector, emptyMessage); }
}

async function renderResults() {
  const parameters = currentUrl.searchParams;
  const purpose = { Sale: 'for sale', Rent: 'to let', Both: 'for sale & to let' }[parameters.get('listingType')] || 'for sale & to let';
  main.innerHTML = `<div class="wrap page-heading"><div class="breadcrumb"><a href="/">Home</a> / Find a property</div><div class="eyebrow">Make your next move</div><h1>Properties ${purpose}</h1><p>Find your space. Explore your area and the neighbourhoods around it.</p>${searchForm(parameters)}</div><section class="wrap results-section"><div class="results-toolbar"><span id="result-count" role="status">Finding properties…</span><a href="/properties?listingType=All" class="text-link">Clear filters</a></div><div id="nearby-note" class="form-help"></div><div id="nearby-areas"></div><div id="search-results" class="property-grid"></div><div id="pagination" class="pagination"></div></section>`;
  bindSearch();
  await loadResults();
}
async function loadResults() {
  currentSearchController?.abort(); currentSearchController = new AbortController();
  const target = document.querySelector('#search-results');
  try {
    const parameters = new URLSearchParams(currentUrl.searchParams); parameters.set('pageSize', '12');
    parameters.set('includeNearby', String(currentUrl.searchParams.get('includeNearby') === 'true'));
    const data = await api(`properties?${parameters}`, { signal: currentSearchController.signal });
    target.innerHTML = data.items.length ? data.items.map(item => propertyCard(item)).join('') : '<div class="empty"><h2>No properties found</h2><p>Try another area, a wider radius or fewer filters.</p><a class="button secondary" href="/properties?listingType=All">Explore all properties</a></div>';
    document.querySelector('#result-count').textContent = `${data.total} propert${data.total === 1 ? 'y' : 'ies'} found`;
    const nearby = currentUrl.searchParams.get('includeNearby') !== 'false' && currentUrl.searchParams.get('location');
    document.querySelector('#nearby-note').textContent = nearby ? data.nearbyAvailable ? 'Nearby distances are measured from a listed property in your searched area. Exact area matches appear first.' : 'We could not locate an area anchor for this search. Showing address matches only; try a nearby town or suburb.' : '';
    renderNearbyAreas(data.nearbyAreas || []);
    const pages = Math.ceil(data.total / data.pageSize);
    document.querySelector('#pagination').innerHTML = pages > 1 ? `${data.page > 1 ? pageLink(data.page - 1, '← Previous') : ''}<span>Page ${data.page} of ${pages}</span>${data.page < pages ? pageLink(data.page + 1, 'Next →') : ''}` : '';
  } catch (error) {
    if (error.name === 'AbortError') return;
    document.querySelector('#result-count').textContent = 'Unable to load properties';
    target.innerHTML = `<div class="notice error full-width" role="alert">${escapeHtml(error.message)} <button class="plain-button" id="retry-search">Try again</button></div>`;
    document.querySelector('#retry-search').addEventListener('click', loadResults);
  }
}
function renderNearbyAreas(areas) {
  const container = document.querySelector('#nearby-areas');
  if (!currentUrl.searchParams.get('location') || !areas.length) { container.innerHTML = ''; return; }
  const selected = new Set(currentUrl.searchParams.getAll('selectedNearbyAreas'));
  const selectedIds = new Set(currentUrl.searchParams.getAll('selectedNearbyLocationIds'));
  const includeAll = currentUrl.searchParams.get('includeNearby') === 'true' && selected.size === 0 && selectedIds.size === 0;
  container.innerHTML = `<form id="nearby-area-filter" class="panel"><h2>Include nearby areas</h2><p class="form-help">Select areas, then apply filters to add their properties to your search.</p><div class="nearby-area-options">${areas.map(renderOption).join('')}</div><button type="submit" class="button primary small" id="apply-nearby" hidden>Apply filters</button></form>`;
  const form = container.querySelector('form');
  const inputs = [...form.querySelectorAll('input')];
  const original = inputs.map(input => input.checked);
  form.addEventListener('change', showApplyButton);
  form.addEventListener('submit', applyNearbyAreas);
  function renderOption(area) {
    return `<label class="check-label"><input type="checkbox" name="area" data-location-id="${escapeHtml(area.locationId || '')}" value="${escapeHtml(area.name)}" ${includeAll || selected.has(area.name) || selectedIds.has(area.locationId) ? 'checked' : ''}><span>${escapeHtml(area.name)} <small>(${area.propertyCount}) · ${area.distanceKm} km</small></span></label>`;
  }
  function showApplyButton() {
    document.querySelector('#apply-nearby').hidden = !inputs.some(hasChanged);
  }
  function hasChanged(input, index) { return input.checked !== original[index]; }
  function applyNearbyAreas(event) {
    event.preventDefault();
    const parameters = new URLSearchParams(currentUrl.searchParams);
    parameters.delete('selectedNearbyAreas'); parameters.delete('selectedNearbyLocationIds'); parameters.delete('page');
    let checkedCount = 0;
    for (const input of inputs) {
      if (input.checked) { parameters.append('selectedNearbyAreas', input.value); if (input.dataset.locationId) parameters.append('selectedNearbyLocationIds', input.dataset.locationId); checkedCount++; }
    }
    parameters.set('includeNearby', String(checkedCount > 0));
    location.href = `/properties?${parameters}`;
  }
}

function pageLink(page, label) {
  const parameters = new URLSearchParams(currentUrl.searchParams); parameters.set('page', String(page));
  return `<a class="button secondary small" href="/properties?${escapeHtml(parameters)}">${label}</a>`;
}

async function renderDetail(id) {
  try {
    const property = await api(`properties/${id}`);
    // The server supplies the canonical title and social metadata for this listing.
    const phone = property.owner.phone.replace(/[^\d+]/g, '');
    const email = encodeURIComponent(property.owner.email);
    main.innerHTML = `<div class="wrap page-heading"><div class="breadcrumb"><a href="/">Home</a> / <a href="/properties?listingType=All">Properties</a> / ${escapeHtml(property.propertyType)}</div><div class="eyebrow">${escapeHtml(property.propertyType)} · ${escapeHtml(listingLabel(property))}${property.isHot ? ' · Hot property' : ''}</div><h1>${escapeHtml(property.address)}</h1></div>
      <div class="wrap detail-layout"><div><div class="gallery">${property.images.length ? property.images.map(renderGalleryImage).join('') : '<img src="/property-scene.svg" alt="No property photographs supplied">'}</div><section class="panel"><div class="price">${property.isForSale ? money(property.salePrice) : `${money(property.rentalPrice)} <small>/ month</small>`}</div>${property.isForSale && property.isForRent ? `<p>Also available to let from ${money(property.rentalPrice)} / month</p>` : ''}<div class="facts"><div>Property type<strong>${escapeHtml(property.propertyType)}</strong></div><div>Property size<strong>${escapeHtml(property.erfSize)}</strong></div><div>Units<strong>${property.units.length}</strong></div></div><h2>Amenities &amp; features</h2>${renderAmenities(property.amenities, escapeHtml)}<h2>About this property</h2><p>${escapeHtml(property.description)}</p></section><section class="panel"><h2>Location</h2><p>${escapeHtml(property.address)}</p><a class="button secondary small" href="https://www.google.com/maps/search/?api=1&query=${property.latitude},${property.longitude}" target="_blank" rel="noopener noreferrer">View on map ↗</a> <a class="text-link" href="/properties?latitude=${property.latitude}&longitude=${property.longitude}&radiusKm=15&listingType=All">Find properties within 15 km →</a></section><section class="panel"><h2>Units & availability</h2>${property.units.length ? property.units.map(unit => renderUnit(unit, property.isForRent)).join('') : '<p>No individual units are listed. Contact the owner for details.</p>'}</section></div><aside class="panel contact-panel"><div class="owner-avatar" aria-hidden="true">${escapeHtml(property.owner.name.charAt(0))}</div><div class="eyebrow">Contact the property owner</div><h2>${escapeHtml(property.owner.name)}</h2><p>Interested in this space? Speak directly with the owner to arrange a viewing or discuss your next steps.</p><a class="contact" href="tel:${escapeHtml(phone)}">${escapeHtml(property.owner.phone)}</a><a class="contact" href="mailto:${email}">${escapeHtml(property.owner.email)}</a><a class="button primary" href="tel:${escapeHtml(phone)}">Call the owner ↗</a><a class="button secondary" href="mailto:${email}?subject=${encodeURIComponent(`Property enquiry: ${property.address}`)}">Email about this property</a></aside></div>`;
  } catch (error) {
    main.innerHTML = `<div class="wrap section"><div class="empty"><h1>${error.status === 404 ? 'This property is no longer available' : 'Unable to load this property'}</h1><p>${error.status === 404 ? 'It may have been withdrawn or is awaiting review.' : escapeHtml(error.message)}</p><a class="button primary" href="/properties?listingType=All">Explore available properties</a></div></div>`;
  }
}
function renderGalleryImage(image, index) {
  return `<img src="${imageSource(image)}" alt="Property photograph ${index + 1}" ${index ? 'loading="lazy"' : ''} referrerpolicy="no-referrer">`;
}
function renderUnit(unit, isForRent) {
  return `<div class="unit"><h3>${escapeHtml(unit.number)} · ${escapeHtml(unit.description)}</h3><p>${unit.areaSqM} m² · ${unit.isVacant ? 'Available' : 'Occupied'}${unit.rentalPrice != null ? ` · ${money(unit.rentalPrice)} / month` : ''}${unit.salePrice != null ? ` · Sale: ${money(unit.salePrice)}` : ''}</p>${isForRent ? `<p class="deposit-detail">${amenityIcon('deposit')}<span>Rental deposit: ${unit.advertisedDepositAmount == null ? 'On request' : unit.advertisedDepositAmount === 0 ? 'No deposit required' : money(unit.advertisedDepositAmount)}</span></p>` : ''}${renderAmenities(unit.amenities, escapeHtml)}<div class="unit-images">${(unit.images || []).map(renderGalleryImage).join('')}</div></div>`;
}

async function initialize() {
  document.querySelector('#year').textContent = String(new Date().getFullYear());
  try {
    const types = await api('properties/types');
    if (types.length) propertyTypes = types;
  } catch { /* Keep the supported categories visible while the service is unavailable. */ }
  const detail = /^\/properties\/(\d+)$/.exec(location.pathname);
  if (detail) await renderDetail(detail[1]);
  else if (location.pathname === '/properties') await renderResults();
  else if (location.pathname === '/sell' || location.pathname === '/owner') await renderOwnerPage();
  else await renderHome();
}
initialize();
