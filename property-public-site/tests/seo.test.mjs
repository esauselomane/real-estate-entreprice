import test from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import { once } from 'node:events';
import { createServer } from '../server.mjs';

async function start(server) { server.listen(0, '127.0.0.1'); await once(server, 'listening'); return `http://127.0.0.1:${server.address().port}`; }
async function close(server) { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }

test('SEO is server rendered, escaped and scoped to approved inventory; sitemaps contain canonical URLs', async () => {
  const property = { id: 7, address: 'Rosebank <script>alert(1)</script>', description: 'A home with a pool.', propertyType: 'Residential', isForSale: true, salePrice: 1500000, latitude: -26.14, longitude: 28.04, images: ['https://images.example/house.jpg'], units: [], location: { city: 'Johannesburg', region: 'Gauteng', countryCode: 'ZA' } };
  const upstream = http.createServer(function handle(request, response) {
    assert.equal(request.headers['x-api-key'], 'secret-key');
    response.setHeader('Content-Type', 'application/json');
    if (request.url.includes('/properties/404')) { response.statusCode = 404; return response.end('{}'); }
    response.end(JSON.stringify(request.url.includes('/properties/7') ? property : { total: 49, items: [property] }));
  });
  const api = await start(upstream);
  const site = createServer({ apiUrl: `${api}/`, apiKey: 'secret-key', agencyId: 'AGENCY-A', origin: 'https://property.example', production: true, agencyName: 'Test Agency' });
  const base = await start(site);
  try {
    let response = await fetch(`${base}/properties/7?ref=someone`); const html = await response.text();
    assert.match(html, /rel="canonical" href="https:\/\/property.example\/properties\/7"/);
    assert.match(html, /property="og:image" content="https:\/\/images.example\/house.jpg"/);
    assert.match(html, /name="twitter:card"/); assert.match(html, /"@type":"RealEstateListing"/);
    assert.match(html, /<article/); assert.doesNotMatch(html, /<script>alert/); assert.doesNotMatch(html, /secret-key/);
    const schema = JSON.parse(html.match(/<script type="application\/ld\+json"[^>]*>(.*?)<\/script>/s)[1]);
    assert.equal(schema.find(item => item['@type'] === 'RealEstateListing').offers[0].price, 1500000);
    assert.match(response.headers.get('content-security-policy'), /nonce-/);
    for (const route of ['/owner', '/sell', '/properties?location=Rosebank']) {
      response = await fetch(base + route); assert.match(response.headers.get('x-robots-tag'), /noindex/);
    }
    response = await fetch(`${base}/properties/404`); assert.equal(response.status, 404); assert.match(response.headers.get('x-robots-tag'), /noindex/);
    response = await fetch(`${base}/robots.txt`); assert.match(await response.text(), /Sitemap: https:\/\/property.example\/sitemap.xml/);
    response = await fetch(`${base}/sitemap.xml`); assert.match(await response.text(), /sitemap-properties-2.xml/);
    response = await fetch(`${base}/sitemap-properties-1.xml`); const xml = await response.text(); assert.match(xml, /https:\/\/property.example\/properties\/7/); assert.doesNotMatch(xml, /owner|sell|secret-key/);
    response = await fetch(`${base}/sitemap-pages.xml`); assert.match(await response.text(), /listingType=Rent/);
  } finally { await close(site); await close(upstream); }
});

test('Development sites discourage indexing and unavailable listings return retryable status', async () => {
  const site = createServer({ apiUrl: '', apiKey: '', production: false }); const base = await start(site);
  try {
    let response = await fetch(base); assert.match(response.headers.get('x-robots-tag'), /noindex/);
    response = await fetch(`${base}/robots.txt`); assert.match(await response.text(), /Disallow: \/\n/);
    response = await fetch(`${base}/properties/1`); assert.equal(response.status, 503); assert.equal(response.headers.get('retry-after'), '60');
  } finally { await close(site); }
});

test('Branding comes from configuration and old room-purpose URLs redirect to the room property type', async () => {
  const site = createServer({ logoUrl: 'https://brand.example/agency-logo.png', faviconUrl: 'https://brand.example/agency-icon.ico', agencyName: 'Another Agency', production: false });
  const base = await start(site);
  try {
    let response = await fetch(base); const html = await response.text();
    assert.match(html, /src="https:\/\/brand.example\/agency-logo.png" alt="Another Agency"/);
    assert.match(html, /rel="icon" href="https:\/\/brand.example\/agency-icon.ico"/);
    response = await fetch(`${base}/site-config`); const config = await response.json();
    assert.equal(config.logoUrl, 'https://brand.example/agency-logo.png');
    response = await fetch(`${base}/properties?listingType=Room&location=Rosebank`, { redirect: 'manual' });
    assert.equal(response.status, 301);
    const redirect = new URL(response.headers.get('location'), base);
    assert.equal(redirect.searchParams.get('listingType'), 'Rent');
    assert.equal(redirect.searchParams.get('propertyType'), 'Room share/let');
    assert.equal(redirect.searchParams.get('location'), 'Rosebank');
  } finally { await close(site); }
});
