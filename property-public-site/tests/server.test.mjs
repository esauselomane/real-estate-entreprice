import test from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import { once } from 'node:events';
import { createServer, upstreamRoute } from '../server.mjs';

async function start(server) { server.listen(0, '127.0.0.1'); await once(server, 'listening'); return `http://127.0.0.1:${server.address().port}`; }
async function close(server) { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }

await test('Proxy keeps agency keys and owner tokens server-side and prevents cross-origin writes', async function proxyBoundary() {
  const received = [];
  const token = 'a'.repeat(64);
  const upstream = http.createServer(async function handle(request, response) {
    const chunks = []; for await (const chunk of request) chunks.push(chunk);
    received.push({ path: request.url, key: request.headers['x-api-key'], agency: request.headers['x-expected-agency-id'], owner: request.headers['x-owner-token'], body: Buffer.concat(chunks).toString() });
    response.setHeader('Content-Type', 'application/json');
    response.end(JSON.stringify(request.url === '/api/developer/owners/login' ? { token, name: 'Owner' } : { items: [], total: 0 }));
  });
  const upstreamUrl = await start(upstream);
  const site = createServer({ apiUrl: `${upstreamUrl}/api/developer/`, apiKey: 'private-agency-key', agencyId: 'AGENCY-A', origin: 'https://property.example', production: true });
  const siteUrl = await start(site);
  try {
    let response = await fetch(`${siteUrl}/api/owners/login`, { method: 'POST', headers: { Origin: 'https://other.example', 'Content-Type': 'application/json' }, body: '{}' });
    assert.equal(response.status, 403); assert.equal(received.length, 0);
    response = await fetch(`${siteUrl}/api/owners/login`, { method: 'POST', headers: { Origin: 'https://property.example', 'Content-Type': 'application/json', 'X-Api-Key': 'attacker-key' }, body: '{}' });
    assert.equal(response.status, 200);
    assert.deepEqual(await response.json(), { name: 'Owner' });
    const cookie = response.headers.get('set-cookie');
    assert.match(cookie, /HttpOnly/); assert.match(cookie, /SameSite=Strict/); assert.match(cookie, /Secure/);
    assert.equal(received[0].key, 'private-agency-key'); assert.equal(received[0].agency, 'AGENCY-A');
    response = await fetch(`${siteUrl}/api/submissions`, { headers: { Cookie: cookie.split(';')[0] } });
    assert.equal(response.status, 200); assert.equal(received[1].owner, token);
    response = await fetch(`${siteUrl}/api/developer/keys`);
    assert.equal(response.status, 404); assert.equal(received.length, 2);
    response = await fetch(`${siteUrl}/server.mjs`); assert.equal(response.status, 404);
    response = await fetch(`${siteUrl}/.env.local`); assert.equal(response.status, 404);
    response = await fetch(`${siteUrl}/`); assert.equal(response.status, 200);
    assert.doesNotMatch(await response.text(), /private-agency-key/);
    assert.match(response.headers.get('content-security-policy'), /frame-ancestors 'none'/);
  } finally { await close(site); await close(upstream); }
});

await test('Unconfigured sites show an explicit service state and still render all pages', async function configurationState() {
  const site = createServer({ apiUrl: '', apiKey: '' }); const siteUrl = await start(site);
  try {
    const response = await fetch(`${siteUrl}/api/properties`); assert.equal(response.status, 503);
    assert.match((await response.json()).message, /not been configured/);
    for (const route of ['/', '/properties', '/sell', '/owner']) assert.equal((await fetch(`${siteUrl}${route}`)).status, 200);
  } finally { await close(site); }
});

await test('Only the public API contract can be reached through the website', function routeAllowlist() {
  for (const path of ['/api/properties/1/leases', '/api/properties/1/documents', '/api/crm/contacts', '/api/keys', '/api/submissions/1/delete']) assert.equal(upstreamRoute(path, 'GET'), null);
  assert.equal(upstreamRoute('/api/properties/1', 'PUT'), null);
  assert.equal(upstreamRoute('/api/submissions/1', 'PUT'), 'submissions/1');
  assert.equal(upstreamRoute('/api/submissions/1/withdraw', 'POST'), 'submissions/1/withdraw');
  assert.equal(upstreamRoute('/api/submissions/1/publish', 'POST'), null);
  assert.equal(upstreamRoute('/api/properties/hot', 'GET'), 'properties/hot');
});
