import { expect, it } from 'vitest';
import { RATE_LIMIT_MESSAGE, timelineErrorSummary } from './timelineError';

it('shows only the HTTP error summary without response bodies or trace IDs', () => {
  expect(
    timelineErrorSummary(
      'API Error: 429 Too Many Requests - {"status":429,"detail":"Retry later","traceId":"private"}'
    )
  ).toBe(RATE_LIMIT_MESSAGE);
  expect(timelineErrorSummary(RATE_LIMIT_MESSAGE)).toBe(RATE_LIMIT_MESSAGE);
  expect(timelineErrorSummary('API Error: 502 Bad Gateway - Failed to load')).toBe('Bad Gateway');
  expect(timelineErrorSummary('API Error: 404 Not Found')).toBe('Not Found');
  expect(timelineErrorSummary('Rate limited')).toBe('Rate limited');
  expect(timelineErrorSummary('{"traceId":"private"}')).toBe('Unable to load timeline');
});
