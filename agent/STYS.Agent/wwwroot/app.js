const $ = (selector, root = document) => root.querySelector(selector);
const $$ = (selector, root = document) => Array.from(root.querySelectorAll(selector));

function setText(id, value) {
  const el = document.getElementById(id);
  if (el) el.textContent = value ?? "";
}

function setBadge(id, value, kind = "") {
  const el = document.getElementById(id);
  if (!el) return;
  el.textContent = value ?? "";
  el.className = `badge ${kind}`.trim();
}

function normalizeBaseUrl(value) {
  return (value ?? "").trim().replace(/\/+$/, "");
}

async function getJson(url, options = {}) {
  const response = await fetch(url, {
    headers: { "Content-Type": "application/json" },
    ...options
  });
  const contentType = response.headers.get("content-type") || "";
  const payload = contentType.includes("application/json") ? await response.json() : await response.text();
  if (!response.ok) {
    const message = typeof payload === "string" ? payload : payload?.message || `HTTP ${response.status}`;
    throw new Error(message);
  }
  return payload;
}

async function loadDashboard() {
  const data = await getJson("/api/bootstrap/dashboard");
  setText("agent-status", data.agentDurumu);
  setText("stys-address", data.stysAdresi);
  setText("enrollment-status", data.enrollmentDurumu);
  setText("agent-display-name", data.agentDisplayName);
  setText("agent-version", data.agentVersion);
  setText("local-ui-version", data.localUiVersion);
  setText("credential-present", data.credentialMevcutMu ? "Evet" : "Hayır");

  if (data.sonBaglantiTesti) {
    const result = data.sonBaglantiTesti;
    const kind = result.success ? "ok" : result.status === "timeout" ? "warn" : "error";
    setBadge("last-connection-status", result.message || result.status, kind);
    setText("last-connection-server-time", result.serverTime || "-");
    setText("last-connection-version", result.version || "-");
  }
}

async function loadConfig() {
  const cfg = await getJson("/api/bootstrap/config");
  $("#stys-base-url").value = cfg.stysBaseUrl ?? "";
  $("#agent-display-name").value = cfg.agentDisplayName ?? "";
  $("#http-timeout-seconds").value = cfg.httpTimeoutSeconds ?? 30;
  $("#local-ui-port").value = cfg.localUiPort ?? 5180;
}

async function saveConfig(event) {
  event.preventDefault();
  const payload = {
    stysBaseUrl: normalizeBaseUrl($("#stys-base-url").value),
    agentDisplayName: $("#agent-display-name").value,
    httpTimeoutSeconds: Number($("#http-timeout-seconds").value),
    localUiPort: Number($("#local-ui-port").value)
  };

  const saved = await getJson("/api/bootstrap/config", {
    method: "POST",
    body: JSON.stringify(payload)
  });

  $("#save-status").textContent = `Kaydedildi. Port değişikliği için Agent yeniden başlatılmalı.`;
  $("#save-status").className = "status ok";
  if (saved) {
    $("#stys-base-url").value = saved.stysBaseUrl ?? "";
    $("#agent-display-name").value = saved.agentDisplayName ?? "";
    $("#http-timeout-seconds").value = saved.httpTimeoutSeconds ?? 30;
    $("#local-ui-port").value = saved.localUiPort ?? 5180;
  }
}

async function testConnection() {
  const payload = {
    stysBaseUrl: normalizeBaseUrl($("#stys-base-url").value),
    agentDisplayName: $("#agent-display-name").value,
    httpTimeoutSeconds: Number($("#http-timeout-seconds").value),
    localUiPort: Number($("#local-ui-port").value)
  };
  const result = await getJson("/api/bootstrap/test-connection", {
    method: "POST",
    body: JSON.stringify(payload)
  });

  const statusEl = $("#connection-status");
  statusEl.textContent = result.message || result.status;
  statusEl.className = `status ${result.success ? "ok" : (result.status === "timeout" ? "warn" : "error")}`;
  setBadge("connection-badge", result.success ? "Bağlantı başarılı" : result.status, result.success ? "ok" : "error");
  setText("connection-server-time", result.serverTime || "-");
  setText("connection-version", result.version || "-");
}

function initNavigation() {
  const path = window.location.pathname.replace(/\/+$/, "") || "/";
  $$("[data-nav]").forEach((link) => {
    const href = link.getAttribute("href");
    if (href === path) {
      link.classList.add("active");
    }
  });
}

function initSetupPage() {
  const form = $("#bootstrap-form");
  if (form) form.addEventListener("submit", saveConfig);
  const testButton = $("#test-connection-btn");
  if (testButton) testButton.addEventListener("click", (event) => { event.preventDefault(); testConnection(); });
  loadConfig().catch((err) => {
    const status = $("#save-status");
    if (status) {
      status.textContent = err.message;
      status.className = "status error";
    }
  });
}

async function bootstrap() {
  initNavigation();
  if ($("#dashboard-root")) {
    await loadDashboard().catch((err) => {
      const status = $("#dashboard-status");
      if (status) {
        status.textContent = err.message;
        status.className = "status error";
      }
    });
  }
  if ($("#bootstrap-form")) {
    initSetupPage();
  }
}

window.addEventListener("DOMContentLoaded", bootstrap);
