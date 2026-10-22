import { createChatHandler } from './chat.mjs';
import { renderSeo, crawlDocument, escapeMarkup } from './seo.mjs';
import http from 'node:http';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const publicDirectory = fileURLToPath(new URL('./public/', import.meta.url));
const allowedFiles = new Map([
  ['/chat.js', 'text/javascript; charset=utf-8'], ['/amenities.js', 'text/javascript; charset=utf-8'], ['/locations.js', 'text/javascript; charset=utf-8'], ['/app.js', 'text/javascript; charset=utf-8'], ['/owner.js', 'text/javascript; charset=utf-8'],
  ['/styles.css', 'text/css; charset=utf-8'], ['/property-scene.svg', 'image/svg+xml'],
  ['/logo.jpg', 'image/jpeg'], ['/favicon.svg', 'image/svg+xml'],
]);
const ownerCookieName = 'biscove_owner';
const maximumBodyBytes = 30_000_000;

export function upstreamRoute(pathname, method) {
  const route = pathname.replace(/^\/api\//, '');
  if (method === 'GET' && /^properties(?:\/(?:types|hot|locations|\d+))?$/.test(route)) return route;
  if (method === 'POST' && /^owners\/(?:register|login|logout)$/.test(route)) return route;
  if (method === 'GET' && route === 'submissions') return route;
  if (method === 'POST' && route === 'submissions') return route;
  if (method === 'POST' && /^submissions\/\d+\/withdraw$/.test(route)) return route;
  if (method === 'PUT' && /^submissions\/\d+$/.test(route)) return route;
  return null;
}

function replyJson(response, status, value) {
  response.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
  response.end(JSON.stringify(value));
}

async function readBody(request) {
  let size = 0;
  const chunks = [];
  for await (const chunk of request) {
    size += chunk.length;
    if (size > maximumBodyBytes) throw Object.assign(new Error('Images exceed the 30 MB submission limit.'), { status: 413 });
    chunks.push(chunk);
  }
  return Buffer.concat(chunks);
}

function readOwnerCookie(request) {
  const cookies = request.headers.cookie?.split(';') || [];
  const cookie = cookies.find(item => item.trim().startsWith(`${ownerCookieName}=`));
  return cookie?.trim().slice(ownerCookieName.length + 1) || '';
}

function setOwnerCookie(response, token, production) {
  const age = token ? 8 * 60 * 60 : 0;
  response.setHeader('Set-Cookie', `${ownerCookieName}=${token}; HttpOnly; SameSite=Strict; Path=/api/; Max-Age=${age}${production ? '; Secure' : ''}`);
}

async function proxy(request, response, url, configuration) {
  const route = upstreamRoute(url.pathname, request.method);
  if (!route) return replyJson(response, 404, { message: 'Endpoint not found.' });
  if (!configuration.apiUrl || !configuration.apiKey || !configuration.agencyId) return replyJson(response, 503, { message: 'The agency website connection has not been configured yet.' });
  if (request.method !== 'GET') {
    if (request.headers.origin !== configuration.origin) return replyJson(response, 403, { message: 'This request must come from the property website.' });
    if (!request.headers['content-type']?.startsWith('application/json')) return replyJson(response, 415, { message: 'JSON is required.' });
  }
  const target = new URL(route, configuration.apiUrl.endsWith('/') ? configuration.apiUrl : `${configuration.apiUrl}/`);
  target.search = url.search;
  const headers = { 'X-Api-Key': configuration.apiKey, 'X-Expected-Agency-Id': configuration.agencyId, Accept: 'application/json' };
  const ownerToken = readOwnerCookie(request);
  if (ownerToken) headers['X-Owner-Token'] = ownerToken;
  let body;
  if (request.method !== 'GET') {
    body = await readBody(request);
    headers['Content-Type'] = 'application/json';
  }
  let upstream;
  try {
    upstream = await fetch(target, { method: request.method, headers, body, signal: AbortSignal.timeout(30000), redirect: 'error' });
  } catch {
    return replyJson(response, 502, { message: 'The property service is unavailable. Please try again shortly.' });
  }
  if (upstream.status === 429) return replyJson(response, 429, { message: 'Too many sign-in attempts. Please wait a minute and try again.' });
  if (route === 'owners/logout') setOwnerCookie(response, '', configuration.production);
  if (upstream.status === 204) { response.writeHead(204); return response.end(); }
  let data;
  try { data = await upstream.json(); }
  catch { return replyJson(response, 502, { message: 'The property service returned an unexpected response.' }); }
  if (route === 'owners/login' || route === 'owners/register') {
    if (upstream.ok && typeof data.token === 'string' && /^[a-fA-F0-9]{64}$/.test(data.token)) {
      setOwnerCookie(response, data.token, configuration.production);
      delete data.token;
    }
  }
  if (!upstream.ok && upstream.status >= 500) data = { message: 'The property service is unavailable. Please try again shortly.' };
  replyJson(response, upstream.status, data);
}

function brandingAsset(value, fallback) {
  if (!value) return fallback;
  if (value.startsWith('/') && !value.startsWith('//') && !value.includes('\\')) return value;
  try { const url = new URL(value); if (url.protocol === 'https:') return url.href; } catch { /* Use bundled branding for invalid values. */ }
  return fallback;
}

export function createServer(options = {}) {
  const configuration = {
    openaiApiKey: options.openaiApiKey ?? process.env.OPENAI_API_KEY ?? '',
    chatModel: options.chatModel ?? process.env.OPENAI_CHAT_MODEL ?? 'gpt-4.1-mini',
    chatFetch: options.chatFetch,
    apiUrl: options.apiUrl ?? process.env.DEVELOPER_API_URL,
    apiKey: options.apiKey ?? process.env.AGENCY_API_KEY,
    agencyId: options.agencyId ?? process.env.AGENCY_ID,
    origin: options.origin ?? process.env.PUBLIC_SITE_ORIGIN ?? 'http://localhost:3010',
    production: options.production ?? process.env.NODE_ENV === 'production',
    googleVerification: options.googleVerification ?? process.env.GOOGLE_SITE_VERIFICATION ?? '',
    bingVerification: options.bingVerification ?? process.env.BING_SITE_VERIFICATION ?? '',
    logoUrl: brandingAsset(options.logoUrl ?? process.env.PUBLIC_LOGO_URL, '/logo.jpg'),
    faviconUrl: brandingAsset(options.faviconUrl ?? process.env.PUBLIC_FAVICON_URL, '/favicon.svg'),
    socialImage: options.socialImage ?? process.env.SEO_SOCIAL_IMAGE ?? '',
    currency: options.currency ?? process.env.SITE_CURRENCY ?? 'ZAR',
    googleMapsBrowserKey: options.googleMapsBrowserKey ?? process.env.GOOGLE_MAPS_BROWSER_KEY ?? '',
    agencyName: options.agencyName ?? process.env.AGENCY_DISPLAY_NAME ?? 'Biscove Properties',
  };
  const handleChat = createChatHandler(configuration);
  return http.createServer(async function handleRequest(request, response) {
    response.setHeader('X-Content-Type-Options', 'nosniff');
    response.setHeader('Referrer-Policy', 'strict-origin-when-cross-origin');
    response.setHeader('Cache-Control', 'no-store');
    response.setHeader('Content-Security-Policy', "default-src 'self'; script-src 'self' https://maps.googleapis.com https://maps.gstatic.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data: https:; connect-src 'self' https://maps.googleapis.com https://places.googleapis.com https://maps.gstatic.com; base-uri 'self'; frame-ancestors 'none'; form-action 'self'");
    try {
      const url = new URL(request.url, 'http://localhost');
      if (url.pathname === '/api/chat' || url.pathname.startsWith('/api/chat/')) return await handleChat(request, response, url.pathname);
      if (url.pathname.startsWith('/api/')) return await proxy(request, response, url, configuration);
      if (url.pathname === '/health') return replyJson(response, 200, { status: 'healthy', configured: Boolean(configuration.apiUrl && configuration.apiKey && configuration.agencyId) });
      if (url.pathname === '/site-config') return replyJson(response, 200, { agencyName: configuration.agencyName, logoUrl: configuration.logoUrl, faviconUrl: configuration.faviconUrl, googleMapsBrowserKey: configuration.googleMapsBrowserKey, currency: configuration.currency, chatEnabled: Boolean(configuration.openaiApiKey && configuration.apiUrl && configuration.apiKey && configuration.agencyId) });
      if (request.method !== 'GET' && request.method !== 'HEAD') return replyJson(response, 405, { message: 'Method not allowed.' });
      if (url.pathname === '/robots.txt' || /^\/sitemap(?:-pages|-properties-[1-9]\d*)?\.xml$/.test(url.pathname)) {
        const document = await crawlDocument(url.pathname, configuration);
        response.writeHead(200, { 'Content-Type': document.type });
        return response.end(request.method === 'HEAD' ? undefined : document.body);
      }
      if (url.pathname === '/properties' && url.searchParams.get('listingType') === 'Room') {
        url.searchParams.set('listingType', 'Rent');
        url.searchParams.set('propertyType', 'Room share/let');
        response.writeHead(301, { Location: url.pathname + url.search });
        return response.end();
      }
      let contentType = allowedFiles.get(url.pathname);
      const brandingFile = /^\/branding\/[a-zA-Z0-9][a-zA-Z0-9._-]*\.(png|jpg|jpeg|webp|svg|ico)$/.exec(url.pathname);
      if (brandingFile) contentType = { png: 'image/png', jpg: 'image/jpeg', jpeg: 'image/jpeg', webp: 'image/webp', svg: 'image/svg+xml', ico: 'image/x-icon' }[brandingFile[1]];
      const isPage = /^\/(?:properties(?:\/\d+)?|sell|owner)?$/.test(url.pathname);
      if (!contentType && !isPage) return replyJson(response, 404, { message: 'Page not found.' });
      const file = contentType ? url.pathname.slice(1) : 'index.html';
      const content = await readFile(path.join(publicDirectory, file));
      if (isPage) {
        const referral = url.searchParams.get('ref');
        if (referral && /^[a-zA-Z0-9_-]{1,80}$/.test(referral))
          response.setHeader('Set-Cookie', `biscove_referral=${referral}; HttpOnly; SameSite=Lax; Path=/; Max-Age=2592000${configuration.production ? '; Secure' : ''}`);
        const brandedTemplate = content.toString()
          .replaceAll('{{AGENCY_NAME}}', escapeMarkup(configuration.agencyName))
          .replace('src="/logo.jpg" alt="Biscove"', `src="${escapeMarkup(configuration.logoUrl)}" alt="${escapeMarkup(configuration.agencyName)}"`)
          .replace('href="/favicon.svg" type="image/svg+xml"', `href="${escapeMarkup(configuration.faviconUrl)}"`);
        const page = await renderSeo(brandedTemplate, url, configuration);
        response.setHeader('Content-Security-Policy', response.getHeader('Content-Security-Policy').replace("script-src 'self'", `script-src 'self' 'nonce-${page.nonce}'`));
        response.setHeader('X-Robots-Tag', page.robots);
        if (page.status === 503) response.setHeader('Retry-After', '60');
        response.writeHead(page.status, { 'Content-Type': 'text/html; charset=utf-8' });
        return response.end(request.method === 'HEAD' ? undefined : page.html);
      }
      response.writeHead(200, { 'Content-Type': contentType || 'text/html; charset=utf-8' });
      response.end(request.method === 'HEAD' ? undefined : content);
    } catch (error) {
      if (!response.headersSent) replyJson(response, error.code === 'ENOENT' ? 404 : error.status || 500, { message: error.code === 'ENOENT' ? 'File not found.' : error.status ? error.message : 'Unable to complete this request.' });
      else response.end();
    }
  });
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const port = Number(process.env.PORT || 3010);
  createServer().listen(port, '0.0.0.0', function started() { console.log(`Biscove Properties is running on http://localhost:${port}`); });
}
