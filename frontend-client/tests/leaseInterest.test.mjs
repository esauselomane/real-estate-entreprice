import { test } from 'node:test';
import assert from 'node:assert/strict';
import { leaseTerminationAmount } from '../src/utils/leaseInterest.js';

test('deposit compounds monthly over four calendar years', () => {
  const expected = Math.round(10000 * (1 + 0.12 / 12) ** 48 * 100) / 100;
  assert.equal(leaseTerminationAmount(10000, 12, '2024-01-01', '2028-01-01'), expected);
});
test('zero interest retains principal; missing or invalid inputs have no estimate', () => {
  assert.equal(leaseTerminationAmount(10000, 0, '2024-01-01', '2025-01-01'), 10000);
  for (const rate of [null, '', -1, 'bad']) assert.equal(leaseTerminationAmount(10000, rate, '2024-01-01', '2025-01-01'), null);
  assert.equal(leaseTerminationAmount(10000, 12, 'invalid', '2025-01-01'), null);
  assert.equal(leaseTerminationAmount(10000, 12, '2025-01-01', '2024-01-01'), null);
});
