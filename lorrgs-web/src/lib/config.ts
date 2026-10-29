const requestTimeoutValue = import.meta.env.VITE_REQUEST_TIMEOUT_MS;
const requestTimeoutMs = requestTimeoutValue === undefined ? 120000 : Number(requestTimeoutValue);

if (!Number.isSafeInteger(requestTimeoutMs) || requestTimeoutMs <= 0) {
  throw new Error('VITE_REQUEST_TIMEOUT_MS must be a positive integer in milliseconds.');
}

export const config = {
  requestTimeoutMs
};
