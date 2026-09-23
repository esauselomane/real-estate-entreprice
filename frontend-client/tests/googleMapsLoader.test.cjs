const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

function fixture() {
  const scripts = [];
  const context = { window: {}, document: {
    scripts,
    createElement: () => ({ dataset: {}, remove() { scripts.splice(scripts.indexOf(this), 1); } }),
    querySelector: () => scripts[0],
    head: { appendChild(script) { scripts.push(script); } },
  } };
  const source = fs.readFileSync(require.resolve('../src/components/maps/GoogleMapsLoader.js'), 'utf8')
    .replace('export function', 'function').replace('import.meta.env.VITE_GOOGLE_MAPS_API_KEY', "'test-key'");
  vm.runInNewContext(source + '\nglobalThis.load = loadGoogleMaps;', context);
  return { context, scripts };
}

test('first load waits for API callback and concurrent callers share one script', async () => {
  const { context, scripts } = fixture();
  const first = context.load();
  const second = context.load();
  assert.equal(first, second);
  assert.equal(scripts.length, 1);
  let ready = false;
  first.then(() => { ready = true; });
  scripts[0].onload?.();
  await Promise.resolve();
  assert.equal(ready, false);
  const importer = async name => ({ name });
  context.window.google = { maps: { importLibrary: importer } };
  context.window.propertyMapsReady();
  assert.equal(await first, importer);
  assert.equal(await second, importer);
});

test('failed script is removed and subsequent load can retry', async () => {
  const { context, scripts } = fixture();
  const failed = context.load();
  const rejection = assert.rejects(failed, /could not load/);
  scripts[0].onerror();
  await rejection;
  assert.equal(scripts.length, 0);
  const retry = context.load();
  assert.equal(scripts.length, 1);
  const importer = async () => ({});
  context.window.google = { maps: { importLibrary: importer } };
  context.window.propertyMapsReady();
  assert.equal(await retry, importer);
});
