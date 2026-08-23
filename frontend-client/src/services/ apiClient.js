import axios from 'axios';

// 🔀 Establish central pipeline connections mapping to your Ocelot API Gateway
const apiClient = axios.create({
  baseURL: 'http://localhost:5000/gateway',
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

export default apiClient;
