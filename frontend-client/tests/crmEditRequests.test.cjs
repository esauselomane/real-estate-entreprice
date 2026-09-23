const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

function createService() {
  const requests = [];
  const source = fs.readFileSync(require.resolve('../src/services/crmService.js'), 'utf8')
    .replace(/^import .*;$/gm, '')
    .replace('export const crmService =', 'const crmService =') + '\nglobalThis.service = crmService;';
  const context = { apiClient: { put: async (url, body) => { requests.push({ url, body }); return { data: body }; } } };
  vm.runInNewContext(source, context);
  return { service: context.service, requests };
}

test('agency edits send null for empty trial dates', async () => {
  const { service, requests } = createService();
  await service.updateAgency('AG-1', { name: 'Updated', trialStartDate: '', trialEndDate: '' });
  assert.equal(requests[0].body.trialStartDate, null);
  assert.equal(requests[0].body.trialEndDate, null);
  assert.equal(requests[0].url, '/crm/agencies/AG-1');
});

test('contact edits retain agency and use the contact identifier', async () => {
  const { service, requests } = createService();
  await service.updateContact({ contactId: 'CON-1', agencyId: 'AG-1', fullName: 'Updated contact' });
  assert.equal(requests[0].url, '/crm/contacts/CON-1');
  assert.equal(requests[0].body.agencyId, 'AG-1');
  assert.equal(requests[0].body.fullName, 'Updated contact');
});
