// Browser smoke checks against the normal local gateway and frontend.
const puppeteer = require(process.env.QA_PUPPETEER_PATH || '/tmp/real-estate-browser-qa/node_modules/puppeteer');
const fs = require('fs');
const path = require('path');
const assert = require('assert/strict');
(async () => {
  const response = await fetch('http://localhost:5001/api/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email: 'platform.admin@agency.local', password: process.env.QA_PASSWORD || 'Password123!' }) });
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
      localStorage.setItem('active_tenant_override', JSON.stringify({ __source: 'view-override', agencyId: 'AG-0001', tenantId: 'TEN-0001' }));
    }, session);
    await page.setViewport({ width: 1440, height: 1000 });
    await page.goto(process.env.QA_FRONTEND_URL || 'http://localhost:5173', { waitUntil: 'networkidle2', timeout: 60000 });
    async function click(label) {
      await page.waitForFunction(text => Array.from(document.querySelectorAll('button')).some(button => button.textContent.trim() === text), { timeout: 20000 }, label);
      await page.evaluate(text => Array.from(document.querySelectorAll('button')).find(button => button.textContent.trim() === text).click(), label);
    }
    await page.click('button[title="Switch active tenant for reports and admin views"]');
    await page.waitForFunction(() => document.body.innerText.includes('TEN-001'));
    const optionCount = await page.evaluate(() => Array.from(document.querySelectorAll('button')).filter(button => /AG-|TEN-/.test(button.textContent)).length);
    assert(optionCount > 1000, 'Full tenant roster remains available despite persisted selection');
    await click('All TenantsAggregate across every visible agency').catch(async () => {
      await page.evaluate(() => Array.from(document.querySelectorAll('button')).find(button => button.textContent.includes('Aggregate across every visible agency')).click());
    });
    assert.equal(await page.evaluate(() => JSON.parse(localStorage.getItem('active_tenant_override')).__all__), true);
    assert.equal(errors.length, 0);
    console.log('PlatformAdmin full roster and All Tenants selection: PASS');
  } finally { await browser.close(); }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
