// Read-only browser verification against the real local sample databases.
const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs/promises');
const puppeteer = require(process.env.QA_PUPPETEER_PATH || 'puppeteer');

async function run() {
  const browser = await puppeteer.launch({ executablePath: process.env.QA_BROWSER_PATH || '/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge', headless: true });
  const output = path.resolve(__dirname, '../../artifacts/property-public-site');
  await fs.mkdir(output, { recursive: true });
  try {
    const page = await browser.newPage();
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    await page.setViewport({ width: 1440, height: 1000 });
    await page.goto('http://localhost:3010/', { waitUntil: 'networkidle0' });
    await page.waitForSelector('#hot-properties .property-card');
    assert.ok(await page.$$eval('#hot-properties .property-card', cards => cards.length > 0 && cards.length <= 5));
    await page.$eval('#latest-properties', element => element.scrollIntoView());
    await page.waitForFunction(() => [...document.querySelectorAll('#latest-properties .card-media img')].every(image => image.complete && image.naturalWidth > 0));
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(output, 'live-home-desktop.png'), fullPage: true });
    await page.goto('http://localhost:3010/properties?listingType=All', { waitUntil: 'networkidle0' });
    await page.waitForSelector('#search-results .property-card');
    assert.equal(await page.$$eval('#search-results .property-card', cards => cards.length), 12);
    await page.setViewport({ width: 390, height: 844 });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await page.screenshot({ path: path.join(output, 'live-results-mobile.png'), fullPage: true });
    await page.goto('http://localhost:3010/properties?listingType=Rent&propertyType=Room%20share%2Flet', { waitUntil: 'networkidle0' });
    await page.waitForSelector('#search-results .property-card');
    assert.ok(await page.$$eval('#search-results .property-card', cards => cards.length >= 2));
    await page.select('[name=roomLettingType]', 'PrivateRoom');
    await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('#property-search [type=submit]')]);
    assert.ok(await page.$$eval('#search-results .property-card', cards => cards.length >= 1));
    assert.equal(await page.$eval('#search-results', element => element.textContent.includes('Private room')), true);
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await page.screenshot({ path: path.join(output, 'live-rooms-mobile.png'), fullPage: true });
    await page.goto('http://localhost:3010/properties?listingType=Both', { waitUntil: 'networkidle0' });
    await page.waitForSelector('#search-results .property-card');
    assert.ok(await page.$$eval('#search-results .property-card', cards => cards.length >= 2));
    await page.goto('http://localhost:3010/properties?location=Sea%20Point&listingType=All&includeNearby=false', { waitUntil: 'networkidle0' });
    await page.waitForSelector('#nearby-area-filter input');
    assert.equal(await page.$eval('#apply-nearby', button => button.hidden), true);
    const initialCount = await page.$eval('#result-count', element => element.textContent);
    const originalUrl = page.url();
    await page.click('#nearby-area-filter input');
    assert.equal(await page.$eval('#apply-nearby', button => button.hidden), false);
    assert.equal(page.url(), originalUrl);
    assert.equal(await page.$eval('#result-count', element => element.textContent), initialCount);
    await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('#apply-nearby')]);
    assert.match(page.url(), /selectedNearbyAreas=/);
    assert.equal(await page.$$eval('#nearby-area-filter input:checked', inputs => inputs.length), 1);
    assert.equal(await page.$eval('#apply-nearby', button => button.hidden), true);
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await page.screenshot({ path: path.join(output, 'live-nearby-areas-mobile.png'), fullPage: true });
    await page.goto('http://localhost:3010/owner', { waitUntil: 'networkidle0' });
    await page.waitForSelector('#owner-auth');
    await page.type('[name=email]', 'owner.public-e2e@example.test');
    await page.type('[name=password]', 'BiscoveSampleOwner2026!');
    await page.click('#owner-auth [type=submit]');
    await page.waitForSelector('.edit-submission');
    assert.equal(await page.$$('.edit-submission').then(buttons => buttons.length), 50);
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await page.screenshot({ path: path.join(output, 'live-owner-mobile.png'), fullPage: true });
    assert.deepEqual(errors, []);
    console.log('PASS: Real local sample listings, gallery PNGs, owner sign-in, fifty owned listings, desktop/mobile layout and no browser errors.');
  } finally { await browser.close(); }
}
run().catch(error => { console.error(error); process.exitCode = 1; });
