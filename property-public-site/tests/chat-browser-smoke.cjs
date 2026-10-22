// Isolated browser check: no paid model calls and no real enquiries or property fixtures.
const assert = require('node:assert/strict');
const http = require('node:http');
const { once } = require('node:events');
const puppeteer = require(process.env.QA_PUPPETEER_PATH || 'puppeteer');
async function start(server) { server.listen(0, '127.0.0.1'); await once(server, 'listening'); return `http://127.0.0.1:${server.address().port}`; }
async function close(server) { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
async function run() {
  const { createServer } = await import('../server.mjs');
  const property = { id: 7, address: 'QA room · Rosebank, Gauteng', description: 'A QA room', propertyType: 'Room share/let', isForRent: true, rentalPrice: 3500, owner: { name: 'QA Owner', email: 'owner@example.test', phone: '+27821234567' }, images: [], units: [], amenities: [], latitude: -26.14, longitude: 28.04 };
  let writes = 0; const errors = [];
  const upstream = http.createServer(function handle(request, response) {
    response.setHeader('Content-Type', 'application/json');
    if (request.method === 'POST') { writes++; return response.end(JSON.stringify({ leadId: 91 })); }
    if (request.url.includes('properties/types')) return response.end(JSON.stringify(['Room share/let']));
    if (request.url.endsWith('properties/7')) return response.end(JSON.stringify(property));
    response.end(JSON.stringify({ total: 1, items: [property] }));
  });
  const api = await start(upstream);
  const configuration = { apiUrl: api + '/', apiKey: 'qa-key', agencyId: 'AGENCY-QA', agencyName: 'QA Agency', openaiApiKey: 'qa-provider-key', production: false,
    async chatFetch(url, options) {
      const input = JSON.parse(options.body).input;
      let output;
      if (input.at(-1).type === 'function_call_output') output = [{ type: 'message', role: 'assistant', content: [{ type: 'output_text', text: 'Here are the details. <img src=x onerror=alert(1)> Please review before sending.' }] }];
      else {
        const enquiring = input.at(-1).content.includes('enquire');
        const args = enquiring ? { propertyId: 7, listingType: 'Rent', name: 'QA Visitor', surname: 'Example', email: null, phone: '+27821234567', message: 'I would like a viewing.' } : { listingType: 'Rent', propertyType: 'Room share/let', location: 'Rosebank', locationId: null, minPrice: null, maxPrice: 4000, includeNearby: false, radiusKm: 15 };
        output = [{ type: 'function_call', call_id: 'qa-call', name: enquiring ? 'prepare_enquiry' : 'search_properties', arguments: JSON.stringify(args) }];
      }
      return Response.json({ output });
    }
  };
  const placeholder = createServer(configuration); const base = await start(placeholder); await close(placeholder);
  const site = createServer({ ...configuration, origin: base }); site.listen(Number(new URL(base).port), '127.0.0.1'); await once(site, 'listening');
  const browser = await puppeteer.launch({ executablePath: process.env.QA_BROWSER_PATH || '/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge', headless: true });
  try {
    const page = await browser.newPage(); page.on('pageerror', error => errors.push(error.message));
    await page.setViewport({ width: 1440, height: 1000 });
    await page.goto(base, { waitUntil: 'networkidle0' });
    await page.waitForSelector('.chat-launcher'); await page.click('.chat-launcher');
    await page.type('#chat-question', 'Find rooms in Rosebank under 4000'); await page.click('.chat-compose button');
    await page.waitForSelector('.chat-property');
    assert.equal(await page.$eval('.chat-property', node => node.getAttribute('href')), '/properties/7');
    assert.equal(await page.$$('.chat-message img[src=x]').then(nodes => nodes.length), 0);
    assert.equal(writes, 0);
    await page.type('#chat-question', 'Please enquire about room 7 with my QA contact details'); await page.click('.chat-compose button');
    await page.waitForSelector('.chat-enquiry');
    assert.equal(writes, 0);
    assert.ok(await page.$eval('.chat-enquiry', node => node.textContent.includes('+27821234567')));
    await page.click('.chat-enquiry .button');
    await page.waitForFunction(() => document.querySelector('.chat-enquiry').textContent.includes('Reference: 91'));
    assert.equal(writes, 1);
    await page.setViewport({ width: 390, height: 844 });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    const bounds = await page.$eval('.chat-panel', node => { const r = node.getBoundingClientRect(); return { left: r.left, right: r.right, top: r.top, bottom: r.bottom }; });
    assert.ok(bounds.left >= 0 && bounds.right <= 390 && bounds.top >= 0 && bounds.bottom <= 844);
    await page.focus('#chat-question'); await page.keyboard.press('Escape');
    assert.equal(await page.$eval('.chat-panel', node => node.hidden), true);
    assert.deepEqual(errors, []);
    console.log('PASS chatbot desktop/mobile, listing cards, literal text safety, draft review, confirmed delivery, Escape and no browser errors.');
  } finally { await browser.close(); await close(site); await close(upstream); }
}
run().catch(error => { console.error(error); process.exitCode = 1; });
