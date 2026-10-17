// Apply test-only settings without overwriting apps/web/config/local.json.
const config = require("../../apps/web/config.js");
const load = config.load;
config.load = (environment) => {
  const settings = load(environment);
  const backend = new URL(process.env.E2E_SERVER_URL).origin;
  const web = new URL(process.env.E2E_WEB_URL || "https://localhost:18080").origin;
  const services = ["api", "identity", "notifications", "events", "icons", "scim"];
  return {
    ...settings,
    urls: {
      base: web,
      webVault: web,
      ...Object.fromEntries(services.map((service) => [service, `${web}/${service}`])),
    },
    dev: {
      ...settings.dev,
      port: Number(new URL(web).port || 443),
      proxyApi: `${backend}/api`,
      proxyIdentity: `${backend}/identity`,
      proxyNotifications: `${backend}/notifications`,
      proxyEvents: `${backend}/events`,
      proxyIcons: `${backend}/icons`,
      wsConnectSrc: web.replace("https:", "wss:"),
    },
  };
};

const build = require("../../bitwarden_license/bit-web/webpack.config.js");
module.exports = (...args) => {
  const result = build(...args);
  if (process.env.E2E_WEB_BUILD_STATUS) {
    result.plugins.push({
      apply(compiler) {
        compiler.hooks.done.tap("E2ECompilationStatus", (stats) => {
          const fs = require("node:fs");
          const status = process.env.E2E_WEB_BUILD_STATUS;
          fs.writeFileSync(`${status}.tmp`, JSON.stringify({ hasErrors: stats.hasErrors() }));
          fs.renameSync(`${status}.tmp`, status);
        });
      },
    });
  }
  return result;
};
