import axios from 'axios';

// 🔀 Establish central pipeline connections mapping to your Ocelot API Gateway
const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/gateway',
  headers: {
    'Content-Type': 'application/json',
  },
});

// 🔐 AUTOMATED INTERCEPTOR: Injects secure identity and tenant metadata into outbound requests
apiClient.interceptors.request.use(
  (config) => {
    const sessionToken = localStorage.getItem('user_session_jwt');
    if (sessionToken) {
      config.headers.Authorization = `Bearer ${sessionToken}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// 🚨 RESPONSE INTERCEPTOR: Normalises backend errors so the UI always
//   receives a consistent { message } shape. Gateway timeouts, 500s,
//   and 401/403s are caught here — the UI shows a red banner rather
//   than crashing the entire page.
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error.response?.status;
    const backendMessage = error.response?.data?.message;
    if (backendMessage) error.message = backendMessage;
    if (status === 401) error.message = 'Your session has expired. Please sign in again.';
    if (status === 403) error.message = 'You do not have permission to view this resource.';
    if (status === 500 || status === 502 || status === 503 || status === 504)
      error.message = 'Service temporarily unavailable. Please try again.';
    if (!error.message || error.message === 'Network Error')
      error.message = 'Network error — the server is not responding.';
    return Promise.reject(error);
  }
);


// 🔁 GET request coalescing.
// Collapses *concurrent* duplicate GETs into a single HTTP round-trip so that:
//   • React 18 StrictMode's dev double-invoke of mount effects doesn't fire
//     each request twice, and
//   • two components fetching the same resource at the same time (e.g. the
//     deals page and the persistent brochure-cart bell both hitting
//     /crm/brochure-cards/active) share one network call.
// Keyed on method + url + sorted params + bearer token, so different tenants
// or pages are never incorrectly merged. Only in-flight requests are shared;
// periodic/30s polls start fresh once the prior call has settled.
const pendingGetRequests = new Map();

const requestSignature = (url, config) => {
  const token =
    config?.headers?.Authorization || `Bearer ${localStorage.getItem('user_session_jwt') || ''}`;
  const params = config?.params;
  let qs = '';
  if (typeof params === 'string') {
    qs = params;
  } else if (params && typeof params === 'object') {
    qs = Object.keys(params)
      .sort()
      .map((k) => `${k}=${encodeURIComponent(params[k] == null ? '' : params[k])}`)
      .join('&');
  }
  return `GET|${url}|${qs}|${token || ''}`;
};

const originalGet = apiClient.get.bind(apiClient);
apiClient.get = (url, config) => {
  const key = requestSignature(url, config || {});
  const cached = pendingGetRequests.get(key);
  if (cached) return cached;
  const promise = originalGet(url, config).finally(() => {
    pendingGetRequests.delete(key);
  });
  pendingGetRequests.set(key, promise);
  return promise;
};

export default apiClient;
