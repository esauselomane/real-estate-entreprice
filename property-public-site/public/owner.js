import { bindAddressFields, collectAddress } from './locations.js';
import { main, api, escapeHtml, imageSource, propertyCard, propertyTypes } from './app.js';

let ownedProperties = [];
let editingProperty = null;
let propertyImages = [];
let unitImages = new Map();
let agencyName = 'the agency';

export async function renderOwnerPage() {
  try {
    const configuration = await fetch('/site-config').then(response => response.json());
    agencyName = configuration.agencyName;
  } catch { /* The form can still identify the destination as the agency. */ }
  main.innerHTML = '<div class="wrap loading" role="status">Checking your owner account…</div>';
  try {
    ownedProperties = await api('submissions');
    if (location.pathname === '/sell') renderSubmissionForm();
    else renderDashboard();
  } catch (error) {
    if (error.status === 401) renderAuthentication(false);
    else renderConnectionError(error);
  }
}
function renderConnectionError(error) {
  main.innerHTML = `<div class="wrap section"><div class="notice error" role="alert">${escapeHtml(error.message)} <button type="button" class="plain-button" id="retry-owner">Try again</button></div></div>`;
  document.querySelector('#retry-owner').addEventListener('click', renderOwnerPage);
}

function renderAuthentication(registering) {
  main.innerHTML = `<div class="wrap auth-layout"><section class="auth-intro"><div class="eyebrow">Sell or rent through us</div><h1>Your property.<br>Your next opportunity.</h1><p>Create your owner account, add your property and submit it to ${escapeHtml(agencyName)}. The agency reviews your listing before it goes live.</p><p>Once approved, buyers and tenants can contact you directly. You stay in control of your property details, photos and availability.</p><img src="/property-scene.svg" alt="A home ready for its next chapter"></section><section class="panel"><div class="form-heading"><h2>${registering ? 'Create your owner account' : 'Welcome back'}</h2></div><p>${registering ? 'Already have an account?' : 'New to Biscove Properties?'} <button type="button" class="plain-button" id="toggle-auth">${registering ? 'Sign in' : 'Create an account'}</button></p><form id="owner-auth" class="form-stack">${registering ? '<label>Your full name<input required name="name" autocomplete="name" maxlength="200"></label><label>Contact number<input required name="phone" type="tel" autocomplete="tel" maxlength="40" placeholder="e.g. +27 82 123 4567"></label>' : ''}<label>Email address<input required name="email" type="email" autocomplete="email" maxlength="254"></label><label>Password<input required name="password" type="password" minlength="12" maxlength="128" autocomplete="${registering ? 'new-password' : 'current-password'}"></label><div class="form-help">Use at least 12 characters. Your account is specific to ${escapeHtml(agencyName)}.</div><button class="button primary" type="submit">${registering ? 'Create account' : 'Sign in'} ↗</button><div id="auth-message" role="alert" class="form-help"></div></form></section></div>`;
  document.querySelector('#toggle-auth').addEventListener('click', toggle);
  document.querySelector('#owner-auth').addEventListener('submit', authenticate);
  function toggle() { renderAuthentication(!registering); }
  async function authenticate(event) {
    event.preventDefault(); const form = event.currentTarget; const button = form.querySelector('[type=submit]');
    button.disabled = true;
    const message = document.querySelector('#auth-message'); message.textContent = '';
    try {
      const data = Object.fromEntries(new FormData(form));
      await api(`owners/${registering ? 'register' : 'login'}`, { method: 'POST', body: JSON.stringify(data) });
      await renderOwnerPage();
    } catch (error) { message.textContent = error.message; }
    finally { button.disabled = false; }
  }
}

function renderDashboard() {
  main.innerHTML = `<div class="wrap page-heading"><div class="breadcrumb"><a href="/">Home</a> / Owner workspace</div><div class="eyebrow">Your owner workspace</div><h1>Your properties, in one place.</h1><p>Track submissions to ${escapeHtml(agencyName)} and keep your listing details up to date. Changes to a listing return it to review and temporarily remove it from public search.</p><div class="form-heading"><a class="button primary" href="/sell">Add a property ↗</a><button class="plain-button" type="button" id="owner-logout">Sign out</button></div></div><section class="wrap results-section"><p id="owner-action-message" role="status" class="form-help"></p><div class="property-grid">${ownedProperties.length ? ownedProperties.map(property => propertyCard(property, true)).join('') : '<div class="empty"><h2>Your first listing starts here.</h2><p>Add a property to submit it to the agency for review.</p><a class="button secondary" href="/sell">Add your property</a></div>'}</div></section>`;
  document.querySelector('#owner-logout').addEventListener('click', logout);
  document.querySelectorAll('.edit-submission').forEach(bindEditButton);
  document.querySelectorAll('.withdraw-submission').forEach(bindWithdrawButton);
}
function bindWithdrawButton(button) { button.addEventListener('click', withdrawProperty); }
async function withdrawProperty(event) {
  const button = event.currentTarget;
  const identity = Number(button.dataset.id);
  button.disabled = true;
  const message = document.querySelector('#owner-action-message');
  message.textContent = 'Removing your listing from the website…';
  try {
    await api(`submissions/${identity}/withdraw`, { method: 'POST', body: '{}' });
    await renderOwnerPage();
    const status = document.querySelector('#owner-action-message');
    if (status) status.textContent = 'Removed from the website. Your property remains here and with the agency. Only the agency can publish it again.';
  } catch (error) { message.textContent = error.message; button.disabled = false; }
}
function bindEditButton(button) { button.addEventListener('click', editProperty); }
function editProperty(event) {
  editingProperty = ownedProperties.find(property => property.id === Number(event.currentTarget.dataset.id));
  renderSubmissionForm(editingProperty);
  window.scrollTo({ top: 0, behavior: 'instant' });
}
async function logout() {
  try { await api('owners/logout', { method: 'POST', body: '{}' }); location.href = '/owner'; }
  catch (error) { renderConnectionError(error); }
}

function renderSubmissionForm(property = null) {
  editingProperty = property;
  propertyImages = [...(property?.images || [])]; unitImages = new Map();
  main.innerHTML = `<div class="wrap submission-page"><div class="page-heading"><div class="breadcrumb"><a href="/">Home</a> / <a href="/owner">My properties</a> / ${property ? 'Edit listing' : 'Add a property'}</div><div class="eyebrow">Sell or rent through us</div><h1>${property ? 'Keep your property up to date.' : 'Tell us about your property.'}</h1><p>Your property will be submitted to ${escapeHtml(agencyName)} for review. Your name, email address and phone number will appear with the approved listing.</p></div><form id="submission-form">
    <section class="panel"><h2>01 · The essentials</h2><div class="form-grid"><label class="full-width">Property address<input name="address" required maxlength="500" autocomplete="street-address" placeholder="Street address, suburb, city" value="${escapeHtml(property?.address || '')}"></label><label>Property type<select required name="propertyType"><option value="">Choose a type</option>${propertyTypes.map(type => `<option ${property?.propertyType === type ? 'selected' : ''}>${escapeHtml(type)}</option>`).join('')}</select></label><label>Property size<input name="erfSize" required maxlength="100" placeholder="e.g. 450 m² or 2 hectares" value="${escapeHtml(property?.erfSize || '')}"></label><label class="check-label"><input type="checkbox" name="isForSale" ${property?.isForSale || !property ? 'checked' : ''}>Available for sale</label><label class="check-label"><input type="checkbox" name="isForRent" ${property?.isForRent ? 'checked' : ''}>Available to let</label><label>Room share / let<select name="roomLettingType"><option value="None">Whole property / no room letting</option><option value="PrivateRoom" ${property?.roomLettingType === 'PrivateRoom' ? 'selected' : ''}>Private room to let</option><option value="SharedRoom" ${property?.roomLettingType === 'SharedRoom' ? 'selected' : ''}>Shared room / room share</option></select></label><label>Sale price (R)<input type="number" name="salePrice" min="1" max="999999999999" step="0.01" placeholder="If selling" value="${property?.salePrice ?? ''}"></label><label class="full-width">Description<textarea required name="description" rows="6" maxlength="10000" placeholder="What makes this property special? Describe the space, amenities and surrounding area.">${escapeHtml(property?.description || '')}</textarea></label></div></section>
    <section class="panel"><h2>02 · Pin the location</h2><p class="form-help">Enter the property’s map coordinates so it appears accurately in nearby searches. You can copy these from Google Maps by right-clicking the property location.</p><div class="form-grid"><label>Latitude<input required name="latitude" type="number" min="-90" max="90" step="any" placeholder="e.g. -26.2041" value="${property?.latitude ?? ''}"></label><label>Longitude<input required name="longitude" type="number" min="-180" max="180" step="any" placeholder="e.g. 28.0473" value="${property?.longitude ?? ''}"></label></div></section>
    <section class="panel"><h2>03 · Make a good first impression</h2><p class="form-help">Add up to 10 property photos. JPEG, PNG or WebP, up to 2 MB each. The first photo is the cover image.</p><label>Property photographs<input id="property-photos" type="file" accept="image/jpeg,image/png,image/webp" multiple></label><div id="property-photo-preview" class="image-preview"></div><button class="plain-button" id="clear-photos" type="button">Clear property photos</button></section>
    <section class="panel"><div class="form-heading"><h2>04 · Units & availability</h2><button type="button" class="button secondary small" id="add-unit">+ Add unit</button></div><p class="form-help">For rentals, add at least one unit with its monthly rent. A whole house can be listed as a single unit. Existing units stay linked to agency records.</p><div id="unit-forms"></div></section>
    <section class="panel"><label class="check-label"><input name="publishContactConsent" type="checkbox" required>I am authorised to list this property. I agree that my name, email and phone number will be publicly visible when the agency approves this listing.</label><p class="form-help">${property ? 'Saving these changes withdraws the current listing and sends it back to the agency for review.' : 'Submitting creates an unpublished property in the agency’s portal. Only the agency can approve publication or mark it as a hot property.'}</p><div class="form-heading"><button type="submit" class="button primary">${property ? 'Save & resubmit for review' : 'Submit property for review'} ↗</button><a class="text-link" href="/owner">Back to my properties</a></div><div id="submission-message" role="alert" class="form-help"></div></section>
  </form></div>`;
  bindAddressFields(document.querySelector('#submission-form'), property);
  document.querySelector('[name=roomLettingType]').addEventListener('change', synchronizeRoomType);
  document.querySelector('[name=propertyType]').addEventListener('change', synchronizePropertyType);
  document.querySelector('#property-photos').addEventListener('change', addPropertyPhotos);
  document.querySelector('#clear-photos').addEventListener('click', clearPropertyPhotos);
  document.querySelector('#add-unit').addEventListener('click', addEmptyUnit);
  document.querySelector('#submission-form').addEventListener('submit', submitProperty);
  renderPropertyImages();
  const units = property?.units || [];
  units.forEach(addExistingUnit);
  if (!units.length) addUnitForm();
}
function synchronizeRoomType(event) {
  const form = event.currentTarget.form;
  if (event.currentTarget.value !== 'None') form.elements.propertyType.value = 'Room share/let';
}
function synchronizePropertyType(event) {
  const form = event.currentTarget.form;
  if (event.currentTarget.value === 'Room share/let') {
    if (form.elements.roomLettingType.value === 'None') form.elements.roomLettingType.value = 'PrivateRoom';
  } else form.elements.roomLettingType.value = 'None';
}
function addExistingUnit(unit) { addUnitForm(unit); }
function addEmptyUnit() { addUnitForm(); }
function addUnitForm(unit = null) {
  const container = document.querySelector('#unit-forms');
  if (container.children.length >= 100) return;
  const id = crypto.randomUUID(); unitImages.set(id, [...(unit?.images || [])]);
  const section = document.createElement('section'); section.className = 'unit unit-form'; section.dataset.unitKey = id;
  section.innerHTML = `<div class="form-heading"><h3>${unit ? escapeHtml(unit.number) : 'Unit details'}</h3>${unit ? '' : '<button type="button" class="plain-button remove-unit">Remove</button>'}</div><div class="form-grid"><label>Unit name / number<input required data-field="number" maxlength="50" placeholder="e.g. Whole property or Unit 1" value="${escapeHtml(unit?.number || '')}"></label><label>Description<input required data-field="description" maxlength="100" placeholder="e.g. Three-bedroom house" value="${escapeHtml(unit?.description || '')}"></label><label>Area (m²)<input required type="number" data-field="areaSqM" min="0" max="99999999" step="0.01" value="${unit?.areaSqM ?? ''}"></label><label>Monthly rent (R)<input type="number" data-field="rentalPrice" min="1" max="999999999999" step="0.01" value="${unit?.rentalPrice ?? ''}"></label><label>Advertised rental deposit (R)<input type="number" data-field="advertisedDepositAmount" min="0" max="999999999999" step="0.01" placeholder="On request" value="${unit?.advertisedDepositAmount ?? ''}"></label><label>Unit sale price (R)<input type="number" data-field="salePrice" min="1" max="999999999999" step="0.01" value="${unit?.salePrice ?? ''}"></label><label class="check-label"><input type="checkbox" data-field="isVacant" ${unit?.isVacant !== false ? 'checked' : ''}>Currently available / vacant</label><label class="full-width">Unit photos · within the 10-photo property limit<input type="file" accept="image/jpeg,image/png,image/webp" multiple data-field="photos"></label></div><div class="image-preview unit-photo-preview"></div><button type="button" class="plain-button clear-unit-photos">Clear unit photos</button>`;
  section.querySelector('.remove-unit')?.addEventListener('click', removeUnit);
  section.querySelector('[data-field=photos]').addEventListener('change', addUnitPhotos);
  section.querySelector('.clear-unit-photos').addEventListener('click', clearUnitPhotos);
  container.append(section); renderUnitImages(section);
  function removeUnit() { unitImages.delete(id); section.remove(); }
}
function totalPictureCount() {
  let total = propertyImages.length;
  for (const images of unitImages.values()) total += images.length;
  return total;
}
async function readImageFiles(files) {
  const results = [];
  for (const file of files) {
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) throw new Error('Use JPEG, PNG or WebP photographs.');
    if (file.size > 2 * 1024 * 1024) throw new Error(`${file.name} is larger than 2 MB.`);
    const buffer = await file.arrayBuffer();
    const bytes = new Uint8Array(buffer);
    // Chunk conversion to avoid overflowing the call stack on a large photo.
    let binary = '';
    for (let offset = 0; offset < bytes.length; offset += 8192) binary += String.fromCharCode(...bytes.subarray(offset, offset + 8192));
    results.push(`data:${file.type};base64,${btoa(binary)}`);
  }
  return results;
}
async function addPropertyPhotos(event) {
  try {
    if (totalPictureCount() + event.currentTarget.files.length > 10) throw new Error('Use at most 10 photographs across the property and its units.');
    propertyImages.push(...await readImageFiles(event.currentTarget.files)); renderPropertyImages(); setSubmissionMessage('');
  } catch (error) { setSubmissionMessage(error.message); }
  finally { event.target.value = ''; }
}
function renderPropertyImages() { document.querySelector('#property-photo-preview').innerHTML = propertyImages.map(renderPhoto).join(''); }
function clearPropertyPhotos() { propertyImages = []; renderPropertyImages(); }
function renderPhoto(image, index) { return `<img src="${imageSource(image)}" alt="Uploaded photograph ${index + 1}" referrerpolicy="no-referrer">`; }
function renderUnitImages(section) { section.querySelector('.unit-photo-preview').innerHTML = (unitImages.get(section.dataset.unitKey) || []).map(renderPhoto).join(''); }
async function addUnitPhotos(event) {
  const section = event.currentTarget.closest('.unit-form'); const id = section.dataset.unitKey;
  try {
    const images = unitImages.get(id);
    if (totalPictureCount() + event.currentTarget.files.length > 10) throw new Error('Use at most 10 photographs across the property and its units.');
    images.push(...await readImageFiles(event.currentTarget.files)); renderUnitImages(section); setSubmissionMessage('');
  } catch (error) { setSubmissionMessage(error.message); }
  finally { event.target.value = ''; }
}
function clearUnitPhotos(event) {
  const section = event.currentTarget.closest('.unit-form'); unitImages.set(section.dataset.unitKey, []); renderUnitImages(section);
}
function setSubmissionMessage(value) { document.querySelector('#submission-message').textContent = value; }
function collectUnit(section) {
  function value(name) { return section.querySelector(`[data-field=${name}]`).value; }
  return { number: value('number'), description: value('description'), areaSqM: Number(value('areaSqM')),
    advertisedDepositAmount: value('advertisedDepositAmount') ? Number(value('advertisedDepositAmount')) : null,
    rentalPrice: value('rentalPrice') ? Number(value('rentalPrice')) : null,
    salePrice: value('salePrice') ? Number(value('salePrice')) : null,
    isVacant: section.querySelector('[data-field=isVacant]').checked, images: unitImages.get(section.dataset.unitKey) || [] };
}
async function submitProperty(event) {
  event.preventDefault(); const form = event.currentTarget; const button = form.querySelector('[type=submit]');
  const data = Object.fromEntries(new FormData(form));
  const payload = { ...collectAddress(form), address: data.address, description: data.description, propertyType: data.propertyType,
    erfSize: data.erfSize, isForSale: form.elements.isForSale.checked, isForRent: form.elements.isForRent.checked,
    salePrice: data.salePrice ? Number(data.salePrice) : null, latitude: Number(data.latitude), longitude: Number(data.longitude),
    roomLettingType: data.roomLettingType, images: propertyImages, units: [...document.querySelectorAll('.unit-form')].map(collectUnit), publishContactConsent: form.elements.publishContactConsent.checked };
  button.disabled = true; setSubmissionMessage('Submitting your property…');
  try {
    await api(editingProperty ? `submissions/${editingProperty.id}` : 'submissions', { method: editingProperty ? 'PUT' : 'POST', body: JSON.stringify(payload) });
    main.innerHTML = `<div class="wrap section"><section class="empty"><div class="eyebrow">Property submitted</div><h1>You’re one step closer.</h1><p>Your listing is now in ${escapeHtml(agencyName)}’s portal, awaiting review.<br>It will appear on the website once the agency approves it.</p><a class="button primary" href="/owner">View my properties ↗</a></section></div>`;
    window.scrollTo({ top: 0, behavior: 'instant' });
  } catch (error) { setSubmissionMessage(error.status === 401 ? 'Your session expired. Sign in again from My properties; keep this page open to retain your draft.' : error.message); }
  finally { button.disabled = false; }
}
