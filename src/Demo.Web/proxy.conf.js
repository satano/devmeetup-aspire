// Dev-server proxy: forward /api to the gateway. Under Aspire the AppHost injects GATEWAY_URL;
// when running `npm start` standalone it falls back to the Phase 0 gateway address.
const target = process.env.GATEWAY_URL || 'https://localhost:7000';

module.exports = {
  '/api': {
    target,
    secure: false,
    changeOrigin: true,
  },
};
