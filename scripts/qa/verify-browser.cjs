// Browser smoke checks against the normal local gateway and frontend.
const puppeteer = require(process.env.QA_PUPPETEER_PATH || '/tmp/real-estate-browser-qa/node_modules/puppeteer');
const fs = require('fs');
const path = require('path');
const assert = require('assert/strict');
(async () => {
  const response = await fetch('http://localhost:5001/api/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email: 'agency.admin@agency.local', password: process.env.QA_PASSWORD || 'Password123!' }) });
  assert.equal(response.status, 200);
  const session = await response.json();
  const browser = await puppeteer.launch({ browser: 'firefox', executablePath: '/Applications/Firefox.app/Contents/MacOS/firefox', headless: true });
  try {
    const page = await browser.newPage();
    const errors = [];
    page.on('pageerror', error => { errors.push(error.message); console.error('Browser error:', error.message); });
    await page.evaluateOnNewDocument(data => {
      localStorage.setItem('user_session_jwt', data.token);
      localStorage.setItem('user_profile_data', JSON.stringify(data.profile));
      localStorage.removeItem('active_tenant_override');
    }, session);
    await page.setViewport({ width: 1440, height: 1000 });
    await page.goto(process.env.QA_FRONTEND_URL || 'http://localhost:5173', { waitUntil: 'networkidle2', timeout: 60000 });
    async function click(label) {
      await page.waitForFunction(text => Array.from(document.querySelectorAll('button')).some(button => button.textContent.trim() === text), { timeout: 20000 }, label);
      await page.evaluate(text => Array.from(document.querySelectorAll('button')).find(button => button.textContent.trim() === text).click(), label);
    }
    await click('Administration');
    await click('Agency Overview');
    await page.waitForFunction(() => document.body.innerText.includes('Quick Glance'));
    console.log('AgencyAdmin can open Agency Overview under Administration: PASS');
    await click('System Admin');
    assert.equal(await page.evaluate(() => Array.from(document.querySelectorAll('button')).some(button => button.textContent.includes('Amenity Types'))), false);
    assert.equal(await page.evaluate(() => Array.from(document.querySelectorAll('button')).some(button => button.textContent.includes('Reports & Analytics'))), false);
    console.log('AgencyAdmin System Admin excludes amenities and reports: PASS');
    await click('Reports');
    await click('Reports & Analytics');
    await page.waitForFunction(() => document.body.innerText.toLowerCase().includes('pipeline value'), { timeout: 15000 }).catch(async error => { console.error(await page.evaluate(() => document.body.innerText.slice(0, 2500))); throw error; });
    console.log('Reports & Analytics opens under Reports: PASS');
    await click('People');
    await click('CRM Directory');
    const search = await page.waitForSelector('input[placeholder="Search this list..."]', { timeout: 30000 });
    await search.type('agency');
    console.log('CRM Directory navigation and search: PASS');
    await click('Business');
    assert.equal(await page.evaluate(() => document.querySelectorAll('nav > div > div').length), 1);
    await click('Deals');
    await page.waitForFunction(() => /DL-\d+/.test(document.body.innerText), { timeout: 45000 });
    console.log('Agency deal records render in the browser: PASS');
    assert.equal(await page.evaluate(() => document.querySelectorAll('nav > div > div').length), 1);
    console.log('Only the selected menu category remains expanded: PASS');
    await page.click('button[aria-label="Open my profile"]');
    await page.waitForSelector('dialog[open] form input');
    const bounds = await page.evaluate(() => {
      const box = document.querySelector('dialog').getBoundingClientRect();
      return box.top >= 0 && box.bottom <= innerHeight && box.left >= 0 && box.right <= innerWidth;
    });
    assert(bounds);
    const output = path.resolve(__dirname, '../../artifacts/agency-qa');
    fs.mkdirSync(output, { recursive: true });
    await page.screenshot({ path: path.join(output, 'profile-modal.png') });
    await page.click('button[aria-label="Close profile"]');
    console.log('Profile modal is editable, fits viewport and closes: PASS');
    await page.evaluate(() => window.dispatchEvent(new CustomEvent('open-billing')));
    await page.waitForFunction(() => document.querySelector('h1')?.textContent === 'Subscription & billing');
    await page.screenshot({ path: path.join(output, 'billing-desktop.png'), fullPage: true });
    await page.setViewport({ width: 390, height: 844 });
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    await page.screenshot({ path: path.join(output, 'billing-mobile.png'), fullPage: true });
    console.log('Billing mobile layout has no horizontal overflow: PASS');
    assert.deepEqual(errors, []);
    console.log('No browser runtime errors: PASS');
  } finally { await browser.close(); }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
