import { randomBytes } from 'node:crypto';

export function escapeMarkup(value) {
  return String(value ?? '').replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character]);
}

function absoluteImage(value, origin) {
  try { const url = new URL(value, origin); return ['http:', 'https:'].includes(url.protocol) ? url.href : null; } catch { return null; }
}

async function listingData(configuration, route) {
  if (!configuration.apiUrl || !configuration.apiKey || !configuration.agencyId) throw Object.assign(new Error('Listings are not configured.'), { status: 503 });
  const base = configuration.apiUrl.endsWith('/') ? configuration.apiUrl : `${configuration.apiUrl}/`;
  const response = await fetch(new URL(route, base), { headers: { 'X-Api-Key': configuration.apiKey, 'X-Expected-Agency-Id': configuration.agencyId }, signal: AbortSignal.timeout(10000), redirect: 'error' });
  if (!response.ok) throw Object.assign(new Error('Listing information is unavailable.'), { status: response.status === 404 ? 404 : 503 });
  return response.json();
}

function purpose(property) {
  if (property.isForSale && property.isForRent) return 'For sale and to let';
  return property.isForSale ? 'For sale' : 'To let';
}

function pageSchema(configuration, canonical, title, description) {
  const origin = new URL(configuration.origin).origin;
  return [{ '@context': 'https://schema.org', '@type': 'WebSite', '@id': `${origin}/#website`, name: configuration.agencyName, url: `${origin}/`, inLanguage: 'en' },
    { '@context': 'https://schema.org', '@type': 'RealEstateAgent', '@id': `${origin}/#agency`, name: configuration.agencyName, url: `${origin}/` },
    { '@context': 'https://schema.org', '@type': 'WebPage', name: title, description, url: canonical, isPartOf: { '@id': `${origin}/#website` } }];
}

/// Metadata and useful listing text are present in the initial HTML, including for bots without JavaScript.
export async function renderSeo(template, url, configuration) {
  const origin = new URL(configuration.origin).origin;
  const nonce = randomBytes(18).toString('base64');
  const categoryNames = { Sale: 'Properties for sale', Rent: 'Properties to let', Both: 'Properties for sale and to let', All: 'Properties' };
  const category = categoryNames[url.searchParams.get('listingType')];
  let canonical = new URL(url.pathname, origin);
  if (url.pathname === '/properties' && category) canonical.searchParams.set('listingType', url.searchParams.get('listingType'));
  if (url.pathname === '/properties' && url.searchParams.get('propertyType') === 'Room share/let') canonical.searchParams.set('propertyType', 'Room share/let');
  let title = `${configuration.agencyName} | Rooms to let and share`;
  let description = `Find private rooms and room shares, plus homes, commercial property and land with ${configuration.agencyName}. Browse approved listings, explore nearby areas and contact property owners.`;
  let content = `<section class="wrap page-heading"><h1>${escapeMarkup(configuration.agencyName)}</h1><p>${escapeMarkup(description)}</p><a href="/properties">Browse properties</a></section>`;
  let status = 200;
  let indexable = configuration.production && !['/owner', '/sell'].includes(url.pathname);
  let image = absoluteImage(configuration.socialImage || configuration.logoUrl || '/logo.jpg', origin);
  let schema;
  if (url.pathname === '/properties') {
    title = `${url.searchParams.get('propertyType') === 'Room share/let' ? 'Room share/let' : category || 'Find properties'} | ${configuration.agencyName}`;
    description = `${category || 'Search homes, commercial property, land and rooms'} with ${configuration.agencyName}. View photographs, amenities and owner contact details.`;
    for (const key of url.searchParams.keys()) {
      if (key === 'propertyType' && url.searchParams.get(key) === 'Room share/let') continue;
      if (key !== 'listingType' && !['utm_source', 'utm_medium', 'utm_campaign', 'ref'].includes(key)) indexable = false;
    }
    if (url.searchParams.has('listingType') && !category) indexable = false;
    content = `<section class="wrap page-heading"><h1>${escapeMarkup(category || 'Find properties')}</h1><p>${escapeMarkup(description)}</p></section>`;
  }
  if (url.pathname === '/sell' || url.pathname === '/owner') {
    title = `${url.pathname === '/sell' ? 'Sell or let your property' : 'Owner account'} | ${configuration.agencyName}`;
    description = `Submit and manage your property listings with ${configuration.agencyName}. Agency review is required before publication.`;
    content = `<section class="wrap page-heading"><h1>${escapeMarkup(title)}</h1><p>${escapeMarkup(description)}</p></section>`;
  }
  if (/^\/properties\/\d+$/.test(url.pathname)) {
    try {
      const property = await listingData(configuration, url.pathname.slice(1));
      title = `${property.propertyType} ${purpose(property).toLowerCase()} in ${property.address} | ${configuration.agencyName}`;
      description = `${purpose(property)}: ${property.address}. ${property.description || `Explore this ${property.propertyType.toLowerCase()} listing and contact the owner.`}`.replace(/\s+/g, ' ').slice(0, 180);
      image = absoluteImage(property.images?.find(value => /^https:\/\//i.test(value)) || configuration.socialImage || configuration.logoUrl || '/logo.jpg', origin);
      const address = property.location;
      schema = pageSchema(configuration, canonical.href, title, description);
      const accommodation = { '@type': property.propertyType === 'Residential' ? 'Accommodation' : 'Place', name: property.address, description: property.description, address: { '@type': 'PostalAddress', streetAddress: property.address, addressLocality: address?.city, addressRegion: address?.region, addressCountry: address?.countryCode, postalCode: property.postalCode }, geo: { '@type': 'GeoCoordinates', latitude: property.latitude, longitude: property.longitude } };
      const offers = [];
      if (property.isForSale && property.salePrice != null) offers.push({ '@type': 'Offer', price: property.salePrice, priceCurrency: configuration.currency, businessFunction: 'http://purl.org/goodrelations/v1#Sell' });
      if (property.isForRent && property.rentalPrice != null) offers.push({ '@type': 'Offer', priceCurrency: configuration.currency, businessFunction: 'http://purl.org/goodrelations/v1#LeaseOut', priceSpecification: { '@type': 'UnitPriceSpecification', price: property.rentalPrice, priceCurrency: configuration.currency, unitText: 'month' } });
      schema.push({ '@context': 'https://schema.org', '@type': 'RealEstateListing', url: canonical.href, name: title, description: property.description, image, mainEntity: accommodation, offers });
      schema.push({ '@context': 'https://schema.org', '@type': 'BreadcrumbList', itemListElement: [{ '@type': 'ListItem', position: 1, name: 'Home', item: `${origin}/` }, { '@type': 'ListItem', position: 2, name: 'Properties', item: `${origin}/properties` }, { '@type': 'ListItem', position: 3, name: property.address, item: canonical.href }] });
      content = `<article class="wrap page-heading"><h1>${escapeMarkup(property.address)}</h1><p>${escapeMarkup(property.propertyType)} · ${escapeMarkup(purpose(property))}</p><p>${escapeMarkup(property.description)}</p><h2>Units</h2><ul>${(property.units || []).map(unit => `<li>${escapeMarkup(unit.number)}: ${escapeMarkup(unit.description)}</li>`).join('')}</ul><a href="/properties">Browse more properties</a></article>`;
    } catch (error) {
      status = error.status === 404 ? 404 : 503;
      indexable = false;
      title = `${status === 404 ? 'Property unavailable' : 'Property service unavailable'} | ${configuration.agencyName}`;
      description = 'This property cannot currently be displayed. Browse other available listings or try again later.';
      content = `<section class="wrap page-heading"><h1>${escapeMarkup(title)}</h1><p>${escapeMarkup(description)}</p><a href="/properties">Browse properties</a></section>`;
    }
  }
  const robots = indexable ? 'index, follow, max-image-preview:large' : 'noindex, follow';
  const tags = [`<title>${escapeMarkup(title)}</title>`, `<meta name="description" content="${escapeMarkup(description)}">`, `<meta name="robots" content="${robots}">`, `<meta name="author" content="${escapeMarkup(configuration.agencyName)}">`, `<link rel="canonical" href="${escapeMarkup(canonical.href)}">`, '<meta property="og:type" content="website">', '<meta property="og:locale" content="en_ZA">', `<meta property="og:site_name" content="${escapeMarkup(configuration.agencyName)}">`, `<meta property="og:title" content="${escapeMarkup(title)}">`, `<meta property="og:description" content="${escapeMarkup(description)}">`, `<meta property="og:url" content="${escapeMarkup(canonical.href)}">`, '<meta name="twitter:card" content="summary_large_image">', `<meta name="twitter:title" content="${escapeMarkup(title)}">`, `<meta name="twitter:description" content="${escapeMarkup(description)}">`];
  if (image) tags.push(`<meta property="og:image" content="${escapeMarkup(image)}">`, `<meta property="og:image:alt" content="${escapeMarkup(title)}">`, `<meta name="twitter:image" content="${escapeMarkup(image)}">`, `<meta name="twitter:image:alt" content="${escapeMarkup(title)}">`);
  if (configuration.googleVerification) tags.push(`<meta name="google-site-verification" content="${escapeMarkup(configuration.googleVerification)}">`);
  if (configuration.bingVerification) tags.push(`<meta name="msvalidate.01" content="${escapeMarkup(configuration.bingVerification)}">`);
  const json = JSON.stringify(schema || pageSchema(configuration, canonical.href, title, description)).replace(/</g, '\\u003c');
  tags.push(`<script type="application/ld+json" nonce="${nonce}">${json}</script>`);
  let html = template.replace(/\s*<title>.*?<\/title>/s, '').replace(/\s*<meta name="description"[^>]*>/, '').replace('</head>', `${tags.join('\n')}\n</head>`);
  html = html.replace('<html lang="en-ZA">', '<html lang="en">').replace('<div class="wrap loading" role="status">Loading properties…</div>', content);
  return { html, status, robots, nonce };
}

function xml(locations, type) {
  const tag = type === 'sitemapindex' ? 'sitemap' : 'url';
  return `<?xml version="1.0" encoding="UTF-8"?><${type} xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">${locations.map(location => `<${tag}><loc>${escapeMarkup(location)}</loc></${tag}>`).join('')}</${type}>`;
}

export async function crawlDocument(pathname, configuration) {
  const origin = new URL(configuration.origin).origin;
  if (pathname === '/robots.txt') return { type: 'text/plain; charset=utf-8', body: configuration.production ? `User-agent: *\nDisallow: /api/\nDisallow: /site-config\nDisallow: /health\nSitemap: ${origin}/sitemap.xml\n` : 'User-agent: *\nDisallow: /\n' };
  if (!configuration.production) return { type: 'application/xml; charset=utf-8', body: xml([], 'urlset') };
  const fixed = ['/', '/properties', '/properties?listingType=Rent&propertyType=Room+share%2Flet', ...['Sale', 'Rent', 'Both'].map(value => `/properties?listingType=${value}`)];
  if (pathname === '/sitemap-pages.xml') return { type: 'application/xml; charset=utf-8', body: xml(fixed.map(route => origin + route), 'urlset') };
  const page = /^\/sitemap-properties-([1-9]\d*)\.xml$/.exec(pathname);
  if (page) {
    const data = await listingData(configuration, `properties?page=${page[1]}&pageSize=48&listingType=All`);
    return { type: 'application/xml; charset=utf-8', body: xml(data.items.map(item => `${origin}/properties/${item.id}`), 'urlset') };
  }
  const data = await listingData(configuration, 'properties?page=1&pageSize=48&listingType=All');
  const count = Math.ceil(data.total / 48);
  if (count > 49999) throw Object.assign(new Error('Sitemap index exceeds supported size.'), { status: 503 });
  return { type: 'application/xml; charset=utf-8', body: xml([`${origin}/sitemap-pages.xml`, ...Array.from({ length: count }, (_, index) => `${origin}/sitemap-properties-${index + 1}.xml`)], 'sitemapindex') };
}
