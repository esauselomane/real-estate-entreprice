export const PROPERTY_TYPE_COLORS = Object.freeze({
  Residential: '#3b82f6',
  Commercial: '#f59e0b',
  Retail: '#10b981',
  Industrial: '#ef4444',
  Default: '#6474a5',
});

export const DEFAULT_PROPERTY_MAP_CENTER = Object.freeze({
  lat: -33.9249,
  lng: 18.4207,
});

export function getPropertyTypeColor(propertyTypeName) {
  return PROPERTY_TYPE_COLORS[propertyTypeName] || PROPERTY_TYPE_COLORS.Default;
}
