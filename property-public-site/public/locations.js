function label(location) {
  return [...new Set([location.suburb, location.city, location.district, location.region, location.country || location.countryCode].filter(Boolean))].join(', ');
}

export function bindLocationSearch(form) {
  const input = form.elements.location;
  const identity = form.elements.locationId;
  const options = document.createElement('datalist');
  options.id = 'location-suggestions';
  input.setAttribute('list', options.id);
  input.setAttribute('autocomplete', 'off');
  input.after(options);
  let timer;
  let requestVersion = 0;
  let matches = [];
  input.addEventListener('input', scheduleSuggestions);
  function synchronizeIdentity() {
    identity.value = matches.find(item => label(item) === input.value)?.locationId || '';
  }
  function scheduleSuggestions() {
    identity.value = '';
    synchronizeIdentity();
    clearTimeout(timer);
    const version = ++requestVersion;
    // A selected qualified label need not be searched as though it were an area name.
    if (identity.value) return;
    timer = setTimeout(() => loadSuggestions(version), 200);
  }
  async function loadSuggestions(version) {
    try {
      const response = await fetch(`/api/properties/locations?q=${encodeURIComponent(input.value)}`);
      if (!response.ok) return;
      const values = await response.json();
      if (version !== requestVersion || !input.isConnected) return;
      matches = values;
      options.replaceChildren(...matches.map(createOption));
      synchronizeIdentity();
    } catch { /* Existing free-text searches remain available for unmigrated listings. */ }
  }
  function createOption(location) {
    const option = document.createElement('option');
    option.value = label(location);
    return option;
  }
}

function addressParts(place) {
  function get(types, short = false) {
    for (const type of types) {
      const component = place.addressComponents?.find(item => item.types.includes(type));
      if (component) return short ? component.shortText : component.longText;
    }
    return '';
  }
  return { countryCode: get(['country'], true), country: get(['country']), region: get(['administrative_area_level_1']),
    district: get(['administrative_area_level_2']), city: get(['locality', 'postal_town', 'administrative_area_level_3']),
    suburb: get(['sublocality_level_1', 'sublocality', 'neighborhood']) };
}

let googleLoader;
async function loadGooglePlaces(key) {
  if (!googleLoader) googleLoader = new Promise((resolve, reject) => {
    const script = document.createElement('script');
    window.biscoveGoogleReady = () => resolve(window.google.maps.importLibrary('places'));
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(key)}&loading=async&libraries=places&callback=biscoveGoogleReady`;
    script.onerror = () => reject(new Error('Google address search could not load.'));
    document.head.append(script);
  });
  return googleLoader;
}

export function collectAddress(form) {
  const names = ['countryCode', 'country', 'region', 'district', 'city', 'suburb'];
  const location = Object.fromEntries(names.map(name => [name, form.elements[`location_${name}`].value.trim()]));
  return { location: location.countryCode ? location : null, googlePlaceId: form.dataset.googlePlaceId || null, postalCode: form.elements.postalCode.value.trim() };
}

export async function bindAddressFields(form, property) {
  const container = document.createElement('div');
  container.className = 'form-grid';
  const heading = document.createElement('p');
  heading.className = 'form-help full-width';
  heading.textContent = 'Confirm the address details below. Province/state and suburb are optional where they do not apply. Country codes use two letters, e.g. ZA, GB or US.';
  container.append(heading);
  const fields = [['countryCode', 'Country code', 2], ['country', 'Country', 150], ['region', 'Province / state / region', 150], ['district', 'District / county', 150], ['city', 'City / town', 150], ['suburb', 'Suburb / neighbourhood', 150]];
  for (const [name, text, maxLength] of fields) {
    const label = document.createElement('label'); label.textContent = text;
    const input = document.createElement('input'); input.name = `location_${name}`; input.maxLength = maxLength;
    input.value = property?.location?.[name] || '';
    if (name === 'countryCode') { input.required = true; input.pattern = '[A-Za-z]{2}'; }
    label.append(input); container.append(label);
  }
  const postalLabel = document.createElement('label'); postalLabel.textContent = 'Postal code (optional)';
  const postal = document.createElement('input'); postal.name = 'postalCode'; postal.maxLength = 40; postal.value = property?.postalCode || '';
  postalLabel.append(postal); container.append(postalLabel);
  const address = form.elements.address;
  address.closest('label').after(container);
  form.dataset.googlePlaceId = property?.googlePlaceId || '';
  address.addEventListener('input', clearResolvedAddress);
  function clearResolvedAddress() {
    form.dataset.googlePlaceId = '';
    for (const [name] of fields) form.elements[`location_${name}`].value = '';
    postal.value = '';
  }
  try {
    const config = await fetch('/site-config').then(response => response.json());
    if (!config.googleMapsBrowserKey || !form.isConnected) return;
    const { PlaceAutocompleteElement } = await loadGooglePlaces(config.googleMapsBrowserKey);
    if (!form.isConnected) return;
    const picker = new PlaceAutocompleteElement({});
    picker.setAttribute('aria-label', 'Find a property address worldwide with Google Maps');
    address.closest('label').before(picker);
    picker.addEventListener('gmp-select', selectAddress);
    async function selectAddress(event) {
      try {
        const place = event.placePrediction.toPlace();
        await place.fetchFields({ fields: ['id', 'formattedAddress', 'location', 'addressComponents'] });
        if (!form.isConnected || !place.location) return;
        address.value = place.formattedAddress;
        form.elements.latitude.value = place.location.lat(); form.elements.longitude.value = place.location.lng();
        const parts = addressParts(place);
        for (const [name] of fields) form.elements[`location_${name}`].value = parts[name] || '';
        postal.value = place.addressComponents?.find(item => item.types.includes('postal_code'))?.longText || '';
        form.dataset.googlePlaceId = place.id;
        heading.textContent = 'Address found. Confirm the city, region, country and map coordinates before submitting.';
      } catch { heading.textContent = 'Could not retrieve this address. Try another suggestion or complete the address details below.'; }
    }
  } catch { heading.textContent = 'Google address search is unavailable. Complete and confirm the address details below.'; }
}
