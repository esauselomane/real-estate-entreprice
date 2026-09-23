const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

function createClient() {
  let calls = 0;
  const fakeClient = {
    interceptors: { request: { use() {} }, response: { use() {} } },
    get(url, config) {
      calls++;
      return new Promise((resolve, reject) => {
        if (config?.signal?.aborted) return reject(new Error('cancelled'));
        config?.signal?.addEventListener('abort', () => reject(new Error('cancelled')), { once: true });
        setImmediate(() => resolve({ data: url }));
      });
    },
  };
  const source = fs.readFileSync(require.resolve('../src/services/apiClient.js'), 'utf8')
    .replace("import axios from 'axios';", '')
    .replace('import.meta.env.VITE_API_BASE_URL', 'undefined')
    .replace('export default apiClient;', 'globalThis.client = apiClient;');
  const context = { axios: { create: () => fakeClient }, localStorage: { getItem: () => 'test-session' } };
  vm.runInNewContext(source, context);
  return { client: context.client, callCount: () => calls };
}

test('effect remount gets a fresh request after the first effect aborts', async () => {
  const { client, callCount } = createClient();
  const first = new AbortController();
  const second = new AbortController();
  const cancelled = client.get('/platforms', { signal: first.signal });
  const rejected = assert.rejects(cancelled, /cancelled/);
  first.abort();
  const next = client.get('/platforms', { signal: second.signal });
  await rejected;
  assert.equal((await next).data, '/platforms');
  assert.equal(callCount(), 2);
});

test('requests without caller-owned cancellation still coalesce', async () => {
  const { client, callCount } = createClient();
  const first = client.get('/platforms');
  const second = client.get('/platforms');
  assert.equal(first, second);
  await Promise.all([first, second]);
  assert.equal(callCount(), 1);
});
