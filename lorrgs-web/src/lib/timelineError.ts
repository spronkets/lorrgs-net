export const RATE_LIMIT_MESSAGE =
  "Warcraft Logs needs a breather. We've hit its request limit, so please try again later.";

export function isArchivedReportError(message: string): boolean {
  return /^API Error:\s*403\b/.test(message) && /report is archived\b/i.test(message);
}

export function timelineErrorSummary(message: string): string {
  if (/^API Error:\s*429\b/.test(message)) return RATE_LIMIT_MESSAGE;
  if (isArchivedReportError(message))
    return 'Archived report — timeline requires Warcraft Logs subscriber access.';
  const status = /^API Error:\s*\d{3}\s+(.+?)(?:\s+-\s+|$)/.exec(message);
  if (status) return status[1].trim();
  const summary = message.split(/\s+-\s+\{|[\r\n]/, 1)[0].trim();
  return summary && !summary.startsWith('{') ? summary : 'Unable to load timeline';
}
