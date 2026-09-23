// Deposit estimate: nominal annual percentage rate compounded monthly.
// Fractional periods use actual elapsed days / (365.25 / 12).
export function leaseTerminationAmount(deposit, annualRatePercent, start, end) {
  if (deposit == null || deposit === '' || annualRatePercent == null || annualRatePercent === '') return null;
  const principal = Number(deposit);
  const rate = Number(annualRatePercent);
  if (!Number.isFinite(principal) || principal <= 0 || !Number.isFinite(rate) || rate < 0 || !start || !end) return null;
  const startDate = new Date(start);
  const endDate = new Date(end);
  if (!Number.isFinite(startDate.getTime()) || !Number.isFinite(endDate.getTime()) || endDate <= startDate) return null;
  const months = (endDate - startDate) / (86400000 * 365.25 / 12);
  const total = principal * Math.pow(1 + rate / 1200, months);
  return Number.isFinite(total) ? Math.round((total + Number.EPSILON) * 100) / 100 : null;
}
