// Uses an existing Puppeteer installation and Chromium browser. All listings are isolated QA fixtures.
const assert = require('node:assert/strict');
const http = require('node:http');
const fs = require('node:fs/promises');
const path = require('node:path');
const { once } = require('node:events');
const puppeteer = require(process.env.QA_PUPPETEER_PATH || 'puppeteer');

async function start(server) { server.listen(0, '127.0.0.1'); await once(server, 'listening'); return `http://127.0.0.1:${server.address().port}`; }
async function close(server) { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
async function run() {
  const { createServer } = await import('../server.mjs');
  const token = 'b'.repeat(64);
  let submission = null;
  let publishedForWithdrawal = false;
  let withdrawn = false;
  let lastSearch = '';
  const property = {
    id: 7, address: 'QA preview · Rosebank, Johannesburg', description: 'An isolated test listing for browser verification.', propertyType: 'Residential', erfSize: '240 m²',
    isForSale: true, isForRent: true, salePrice: 2400000, rentalPrice: 12000, latitude: -26.14, longitude: 28.04, isHot: true, images: [],
    amenities: [{ name: 'SwimmingPool', label: 'Swimming pool', booleanValue: true }, { name: 'Parking', label: 'Parking', numberValue: 2 }],
    owner: { name: 'QA Owner', email: 'owner@example.test', phone: '+27821234567' }, units: [{ id: 1, advertisedDepositAmount: 12000, amenities: [{ name: 'PetsAllowed', label: 'Pets allowed', booleanValue: false }], number: 'Unit 1', description: 'House', areaSqM: 180, rentalPrice: 12000, salePrice: null, isVacant: true, images: [] }],
  };
  const upstream = http.createServer(async function handle(request, response) {
    response.setHeader('Content-Type', 'application/json');
    const url = new URL(request.url, 'http://localhost');
    if (url.pathname.endsWith('/properties/locations')) return response.end(JSON.stringify([{ locationId: 'gauteng-rosebank', suburb: 'Rosebank', city: 'Johannesburg', region: 'Gauteng', country: 'South Africa' }, { locationId: 'cape-rosebank', suburb: 'Rosebank', city: 'Cape Town', region: 'Western Cape', country: 'South Africa' }]));
    if (url.pathname.endsWith('/properties/types')) return response.end(JSON.stringify(['Residential', 'Commercial', 'Industrial', 'Retail', 'Land', 'Room share/let']));
    if (url.pathname.endsWith('/owners/register') || url.pathname.endsWith('/owners/login')) return response.end(JSON.stringify({ token, name: 'QA Owner' }));
    if (url.pathname.endsWith('/submissions') && request.method === 'POST') {
      const chunks = []; for await (const chunk of request) chunks.push(chunk);
      submission = JSON.parse(Buffer.concat(chunks).toString());
      return response.end(JSON.stringify({ propertyId: 8, status: 'Pending review' }));
    }
    if (url.pathname.endsWith('/submissions/8/withdraw') && request.method === 'POST') { withdrawn = true; return response.end(JSON.stringify({ isPublicListing: false, publicReviewStatus: 'Unpublished' })); }
    if (url.pathname.endsWith('/submissions')) {
      if (request.headers['x-owner-token'] !== token) { response.statusCode = 401; return response.end('{}'); }
      return response.end(JSON.stringify(submission ? [{ ...property, ...submission, id: 8, owner: property.owner, isHot: false, reviewStatus: withdrawn ? 'Unpublished' : publishedForWithdrawal ? 'Approved' : 'Pending review', isPublicListing: publishedForWithdrawal && !withdrawn }] : []));
    }
    if (url.pathname.endsWith('/properties/7')) return response.end(JSON.stringify(property));
    lastSearch = url.search;
    response.end(JSON.stringify({ items: [property], total: 1, page: 1, pageSize: 12, nearbyAvailable: true }));
  });
  const upstreamUrl = await start(upstream);
  const site = createServer({ apiUrl: `${upstreamUrl}/api/developer/`, apiKey: 'qa-key', agencyId: 'AGENCY-A' });
  const siteUrl = await start(site);
  // The runtime origin is allocated dynamically for this isolated test server.
  const originSite = createServer({ apiUrl: `${upstreamUrl}/api/developer/`, apiKey: 'qa-key', agencyId: 'AGENCY-A', origin: siteUrl });
  await close(site);
  const port = Number(new URL(siteUrl).port); originSite.listen(port, '127.0.0.1'); await once(originSite, 'listening');
  let browser;
  try { browser = await puppeteer.launch({ executablePath: process.env.QA_BROWSER_PATH || '/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge', headless: true }); }
  catch (error) { await close(originSite); await close(upstream); throw error; }
  const errors = [];
  const output = path.resolve(__dirname, '../../artifacts/property-public-site'); await fs.mkdir(output, { recursive: true });
  try {
    const page = await browser.newPage(); page.on('pageerror', error => errors.push(error.message));
    await page.setViewport({ width: 1440, height: 1100 });
    await page.goto(siteUrl, { waitUntil: 'networkidle0' });
    await page.waitForSelector('#latest-properties .property-card');
    assert.equal(await page.$eval('[name=propertyType]', element => element.value), 'Room share/let');
    assert.equal(await page.$eval('[name=listingType]', element => element.value), 'All');
    assert.equal(await page.$$('[data-listing-type=Room]').then(items => items.length), 0);
    assert.equal(await page.$eval('h1', element => element.textContent.includes('next chapter')), true);
    assert.equal(await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--accent').trim()), '#d0ad68');
    await page.screenshot({ path: path.join(output, 'home-desktop.png'), fullPage: true });
    for (const width of [390, 768, 1440]) {
      await page.setViewport({ width, height: 900 });
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, `No overflow at ${width}px`);
    }
    await page.setViewport({ width: 390, height: 844 });
    await page.screenshot({ path: path.join(output, 'home-mobile.png'), fullPage: true });
    await page.select('select[name=propertyType]', 'Land');
    await page.type('input[name=location]', 'Rosebank');
    await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('#property-search [type=submit]')]);
    assert.match(page.url(), /propertyType=Land/); assert.match(lastSearch, /location=Rosebank/);
    await page.goto(`${siteUrl}/properties/7`, { waitUntil: 'networkidle0' });
    assert.equal(await page.$eval('.contact-panel', element => element.textContent.includes('owner@example.test')), true);
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await page.screenshot({ path: path.join(output, 'detail-mobile.png'), fullPage: true });
    await page.goto(siteUrl, { waitUntil: 'networkidle0' });
    await page.type('[name=location]', 'Rosebank');
    await page.waitForSelector('#location-suggestions option');
    assert.equal(await page.$$eval('#location-suggestions option', values => values.length), 2);
    await page.$eval('[name=location]', input => { input.value = 'Rosebank, Johannesburg, Gauteng, South Africa'; input.dispatchEvent(new Event('input', { bubbles: true })); });
    await page.waitForFunction(() => document.querySelector('[name=locationId]').value === 'gauteng-rosebank');
    await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('#property-search [type=submit]')]);
    assert.match(page.url(), /locationId=gauteng-rosebank/);
    await page.goto(`${siteUrl}/properties/7`, { waitUntil: 'networkidle0' });
    await page.waitForSelector('.amenity-list .amenity-icon');
    assert.equal(await page.$$eval('.amenity-list li', items => items.length), 3);
    assert.match(await page.$eval('.unit', unit => unit.textContent), /Not allowed/);
    assert.match(await page.$eval('.deposit-detail', item => item.textContent), /12.?000/);
    await page.goto(`${siteUrl}/sell`, { waitUntil: 'networkidle0' });
    await page.click('#toggle-auth');
    await page.type('[name=name]', 'QA Owner'); await page.type('[name=phone]', '+27821234567');
    await page.type('[name=email]', 'owner@example.test'); await page.type('[name=password]', 'qa-password-12345');
    await page.click('#owner-auth [type=submit]'); await page.waitForSelector('#submission-form');
    await page.type('[name=address]', 'QA owner submission, Rosebank'); await page.select('[name=propertyType]', 'Residential');
    await page.type('[name=erfSize]', '240 m²'); await page.type('[name=salePrice]', '2400000'); await page.type('textarea[name=description]', 'QA owner submission description.');
    await page.type('[name=location_countryCode]', 'ZA'); await page.type('[name=location_country]', 'South Africa');
    await page.type('[name=location_region]', 'Gauteng'); await page.type('[name=location_city]', 'Johannesburg'); await page.type('[name=location_suburb]', 'Rosebank');
    await page.type('[name=latitude]', '-26.14'); await page.type('[name=longitude]', '28.04');
    await page.type('[data-field=number]', 'Unit 1'); await page.type('[data-field=description]', 'Whole house'); await page.type('[data-field=areaSqM]', '180');
    await page.click('[name=publishContactConsent]');
    await page.screenshot({ path: path.join(output, 'owner-submission-mobile.png'), fullPage: true });
    await page.click('#submission-form [type=submit]'); await page.waitForFunction(() => document.querySelector('h1')?.textContent.includes('one step closer'));
    assert.equal(submission.address, 'QA owner submission, Rosebank'); assert.equal(submission.publishContactConsent, true);
    assert.equal(submission.units.length, 1); assert.equal(submission.location.region, 'Gauteng'); assert.equal(submission.location.suburb, 'Rosebank');
    await page.goto(`${siteUrl}/owner`, { waitUntil: 'networkidle0' }); await page.waitForSelector('.edit-submission');
    assert.equal(await page.$eval('.status-line', element => element.textContent.includes('Pending review')), true);
    await page.click('.edit-submission'); await page.waitForSelector('#submission-form');
    assert.equal(await page.$eval('[name=address]', element => element.value), submission.address);
    publishedForWithdrawal = true;
    await page.goto(`${siteUrl}/owner`, { waitUntil: 'networkidle0' });
    await page.waitForSelector('.withdraw-submission');
    await page.click('.withdraw-submission');
    await page.waitForFunction(() => document.querySelector('#owner-action-message')?.textContent.includes('Only the agency'));
    assert.equal(withdrawn, true);
    assert.equal(await page.$$eval('.property-card', values => values.length), 1);
    assert.equal(await page.$('.withdraw-submission'), null);
    assert.match(await page.$eval('.status-line', value => value.textContent), /Unpublished/);
    assert.deepEqual(errors, []);
    console.log('PASS: Desktop/mobile layouts, search filters, owner contacts, registration, submission, pending status and edit form. No browser runtime errors.');
  } finally { await browser.close(); await close(originSite); await close(upstream); }
}
run().catch(error => { console.error(error); process.exitCode = 1; });
