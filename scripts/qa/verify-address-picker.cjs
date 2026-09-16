// Deterministic component check; does not call or bill Google APIs.
const puppeteer = require(process.env.QA_PUPPETEER_PATH || '/tmp/real-estate-browser-qa/node_modules/puppeteer');
const assert = require('assert/strict');
(async () => {
  const browser = await puppeteer.launch({ browser: 'firefox', executablePath: '/Applications/Firefox.app/Contents/MacOS/firefox', headless: true });
  try {
    const page = await browser.newPage();
    await page.goto('http://localhost:5173', { waitUntil: 'networkidle2' });
    await page.evaluate(async () => {
      class FakePlaces extends HTMLElement {}
      customElements.define('qa-google-places', FakePlaces);
      window.google = { maps: { importLibrary: async () => ({ PlaceAutocompleteElement: FakePlaces }) } };
      const React = await import('/node_modules/.vite/deps/react.js');
      const clientModule = await import('/node_modules/.vite/deps/react-dom_client.js');
      const { createRoot } = clientModule.default || clientModule;
      const { default: Picker } = await import('/src/components/maps/GoogleAddressPicker.jsx');
      const host = document.createElement('div');
      document.body.appendChild(host);
      createRoot(host).render((React.default || React).createElement(Picker, { onSelect: value => { window.qaAddress = value; } }));
    });
    await page.waitForSelector('qa-google-places');
    await page.evaluate(() => {
      const event = new Event('gmp-select');
      event.placePrediction = { toPlace: () => ({ fetchFields: async () => {}, formattedAddress: 'Sandton, South Africa', location: { lat: () => -26.1076, lng: () => 28.0567 } }) };
      document.querySelector('qa-google-places').dispatchEvent(event);
    });
    await page.waitForFunction(() => window.qaAddress);
    assert.deepEqual(await page.evaluate(() => window.qaAddress), { completeAddress: 'Sandton, South Africa', latitude: -26.1076, longitude: 28.0567 });
    console.log('Google selection maps address and coordinates: PASS (simulated provider)');
    await page.evaluate(() => {
      const event = new Event('gmp-select');
      event.placePrediction = { toPlace: () => ({ fetchFields: async () => { throw new Error('Unavailable'); } }) };
      document.querySelector('qa-google-places').dispatchEvent(event);
    });
    await page.waitForFunction(() => document.body.innerText.includes('Could not retrieve coordinates'));
    console.log('Place-detail failure displays actionable feedback: PASS');
  } finally { await browser.close(); }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
