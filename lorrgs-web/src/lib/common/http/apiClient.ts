import type { ApiOptions } from './ApiOptions';
import { RATE_LIMIT_MESSAGE } from '../../timelineError';
import { config } from '../../config';

const API_BASE: string =
  (typeof import.meta !== 'undefined' && import.meta.env?.VITE_API_BASE) || '/api';
const HEALTH_READY_URL: string | undefined =
  typeof import.meta !== 'undefined' ? import.meta.env?.VITE_HEALTH_URL : undefined;

export function buildApiUrl(endpoint: string): string {
  return `${API_BASE}${endpoint}`;
}

function deriveHealthUrls(): string[] {
  if (HEALTH_READY_URL) {
    return [HEALTH_READY_URL];
  }

  const trimmedBase = API_BASE.replace(/\/+$/, '');
  const defaultUrls = ['/health'];

  if (/^https?:\/\//i.test(trimmedBase)) {
    const baseWithoutApi = trimmedBase.replace(/\/api$/i, '');
    return [`${baseWithoutApi}/health`];
  }

  return defaultUrls;
}

async function probeHealth(url: string): Promise<boolean> {
  try {
    const response = await fetch(url, {
      method: 'GET',
      cache: 'no-store'
    });

    return response.ok;
  } catch {
    return false;
  }
}

export async function waitForHealthReady(timeoutMs = 30000, intervalMs = 500): Promise<boolean> {
  const urls = deriveHealthUrls();
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    for (const url of urls) {
      if (await probeHealth(url)) {
        return true;
      }
    }

    await new Promise((resolve) => setTimeout(resolve, intervalMs));
  }

  return false;
}

export function normalizeApiPayload(payload: unknown): unknown[] {
  if (Array.isArray(payload)) {
    return payload;
  }

  if (payload && typeof payload === 'object') {
    return Object.values(payload as Record<string, unknown>);
  }

  return [];
}

export function normalizeApiCollection<T = unknown>(payload: unknown, fallbackKey = ''): T[] {
  if (Array.isArray(payload)) {
    return payload;
  }

  if (payload && typeof payload === 'object') {
    const record = payload as Record<string, unknown>;

    if (fallbackKey && Array.isArray(record[fallbackKey])) {
      return record[fallbackKey] as T[];
    }

    if (Object.values(record).every((value) => value && typeof value === 'object')) {
      return Object.values(record) as T[];
    }
  }

  return [];
}

async function buildApiError(response: Response): Promise<Error> {
  if (response.status === 429) return new Error(RATE_LIMIT_MESSAGE);
  let detail = '';

  try {
    const contentType = response.headers.get('content-type') || '';

    if (
      contentType.includes('application/json') ||
      contentType.includes('application/problem+json')
    ) {
      const payload = (await response.json()) as Record<string, unknown>;
      if (typeof payload.error === 'string' && payload.error.trim()) {
        detail = payload.error.trim();
      } else if (typeof payload.message === 'string' && payload.message.trim()) {
        detail = payload.message.trim();
      } else if (typeof payload.detail === 'string' && payload.detail.trim()) {
        detail = payload.detail.trim();
      }
    } else {
      const text = (await response.text()).trim();
      if (text) {
        detail = text;
      }
    }
  } catch (error: unknown) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error;
    // Fallback to status text below.
  }

  const statusSummary = `API Error: ${response.status} ${response.statusText}`.trim();
  return new Error(detail ? `${statusSummary} - ${detail}` : statusSummary);
}

async function request<T>(endpoint: string, options: ApiOptions, body?: string): Promise<T> {
  const { timeoutMs = config.requestTimeoutMs, ...fetchOptions } = options;
  const url = buildApiUrl(endpoint);
  const controller = new AbortController();
  const cancel = () => controller.abort();
  options.signal?.addEventListener('abort', cancel, { once: true });
  if (options.signal?.aborted) controller.abort();
  const timeoutHandle = setTimeout(() => controller.abort(), timeoutMs);

  try {
    const response = await fetch(url, {
      method: 'GET',
      headers: { 'Content-Type': 'application/json', ...options.headers },
      body,
      ...fetchOptions,
      signal: controller.signal
    });
    if (!response.ok) {
      throw await buildApiError(response);
    }

    return (await response.json()) as T;
  } catch (error: unknown) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      if (options.signal?.aborted) throw error;
      throw new Error(`API Error: request timed out after ${timeoutMs}ms`);
    }

    throw error;
  } finally {
    clearTimeout(timeoutHandle);
    options.signal?.removeEventListener('abort', cancel);
  }
}

export function get<T = unknown>(endpoint: string, options: ApiOptions = {}): Promise<T> {
  return request<T>(endpoint, options);
}

export function send(endpoint: string, body: unknown, options: ApiOptions = {}): Promise<unknown> {
  return request(
    endpoint,
    {
      ...options,
      method: options.method || 'POST'
    },
    JSON.stringify(body ?? {})
  );
}
