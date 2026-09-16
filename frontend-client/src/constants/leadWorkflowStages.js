// Keep names and numeric order aligned with CRM.Models.LeadWorkflowStage.
export const LEAD_STAGES = Object.freeze(['Contacted', 'AttemptedContact', 'Nurturing', 'Disqualified', 'Qualified']);
export const LEAD_STAGE_COLORS = { Contacted: '#3b82f6', AttemptedContact: '#f59e0b', Nurturing: '#a78bfa', Disqualified: '#ef4444', Qualified: '#14b8a6' };
export function leadStageName(value) {
  if (typeof value === 'number') return LEAD_STAGES[value] || '';
  return LEAD_STAGES.includes(value) ? value : '';
}
export function leadStageChartData(aggregates = {}) {
  return LEAD_STAGES.map((stage, index) => ({
    name: stage.replace(/([A-Z])/g, ' $1').trim(),
    value: Number(aggregates?.[stage] ?? aggregates?.[index] ?? 0),
    color: LEAD_STAGE_COLORS[stage],
  }));
}
