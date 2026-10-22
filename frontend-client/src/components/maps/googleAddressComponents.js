export function extractGoogleAddress(place) {
  const components = place.addressComponents || place.address_components || [];
  function component(types, short = false) {
    for (const type of types) {
      const match = components.find(item => item.types.includes(type));
      if (match) return short ? (match.shortText || match.short_name || '') : (match.longText || match.long_name || '');
    }
    return '';
  }
  const countryCode = component(['country'], true);
  return {
    googlePlaceId: place.id || place.place_id || null,
    postalCode: component(['postal_code']),
    location: countryCode ? {
      countryCode, country: component(['country']), region: component(['administrative_area_level_1']),
      district: component(['administrative_area_level_2']),
      city: component(['locality', 'postal_town', 'administrative_area_level_3']),
      suburb: component(['sublocality_level_1', 'sublocality', 'neighborhood']),
    } : null,
  };
}
