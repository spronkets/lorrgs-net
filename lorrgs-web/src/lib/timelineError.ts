export function timelineErrorSummary(message: string): string {
  const status = /^API Error:\s*\d{3}\s+(.+?)(?:\s+-\s+|$)/.exec(message);
  if (status) return status[1].trim();
  const summary = message.split(/\s+-\s+\{|[\r\n]/, 1)[0].trim();
  return summary && !summary.startsWith('{') ? summary : 'Unable to load timeline';
}
