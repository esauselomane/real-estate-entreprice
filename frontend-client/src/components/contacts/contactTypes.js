const flags = { Buyer: 1, Seller: 2, Broker: 4, Owner: 8, Tenant: 16, Staff: 32, Manager: 64 };
export function normalizeContactTypes(value) {
  if (Array.isArray(value)) return [...new Set(value.flatMap(normalizeContactTypes))];
  if (typeof value === 'number' && Number.isInteger(value)) return Object.keys(flags).filter(name => (value & flags[name]) !== 0);
  if (typeof value !== 'string') return [];
  if (/^\d+$/.test(value.trim())) return normalizeContactTypes(Number(value));
  return [...new Set(value.split(',').map(name => name.trim()).flatMap(name => name === 'All' ? Object.keys(flags) : name in flags ? [name] : []))];
}
