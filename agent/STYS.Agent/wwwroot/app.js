const $ = (selector, root = document) => root.querySelector(selector);
const $$ = (selector, root = document) => Array.from(root.querySelectorAll(selector));

const state = {
  connectionOk: false,
  dashboardLoaded: false
};

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

function setStatus(id, value, kind = "muted") {
  const el = document.getElementById(id);
  if (!el) return;
  el.textContent = value ?? "";
  el.className = `status ${kind}`.trim();
}

function setHidden(id, hidden) {
  const el = document.getElementById(id);
  if (!el) return;
  el.classList.toggle("hidden", hidden);
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
  const payload = contentType.includes("application/json")
    ? await response.json()
    : await response.text();

  if (!response.ok) {
    const message = typeof payload === "string"
      ? payload
      : payload?.message || payload?.Message || `HTTP ${response.status}`;
    const error = new Error(message);
    error.status = response.status;
    error.body = payload;
    throw error;
  }

  return payload;
}

function renderPills(id, values) {
  const el = document.getElementById(id);
  if (!el) return;
  el.innerHTML = "";

  const items = (values ?? []).filter(Boolean);
  if (items.length === 0) {
    el.textContent = "-";
    return;
  }

  for (const value of items) {
    const pill = document.createElement("span");
    pill.className = "pill";
    pill.textContent = value;
    el.appendChild(pill);
  }
}

function renderTesisList(id, tesisler) {
  const labels = (tesisler ?? []).map((x) => {
    if (typeof x === "string") return x;
    return x?.ad || x?.Ad || String(x?.id ?? x?.Id ?? "-");
  });
  renderPills(id, labels);
}

function updateEnrollmentButtonState() {
  const enrollBtn = $("#enroll-btn");
  if (!enrollBtn) return;
  const baseUrl = normalizeBaseUrl($("#stys-base-url")?.value);
  const agentName = ($("#agent-display-name")?.value ?? "").trim();
  const enrollmentCode = ($("#enrollment-code")?.value ?? "").trim();
  enrollBtn.disabled = !state.connectionOk || !baseUrl || !agentName || !enrollmentCode;
}

function bindEnrollmentFormState() {
  const inputs = ["#stys-base-url", "#agent-display-name", "#enrollment-code", "#http-timeout-seconds", "#local-ui-port"];
  for (const selector of inputs) {
    const input = $(selector);
    if (!input) continue;
    input.addEventListener("input", () => {
      if (selector === "#stys-base-url" || selector === "#agent-display-name" || selector === "#http-timeout-seconds" || selector === "#local-ui-port") {
        state.connectionOk = false;
        setBadge("connection-badge", "Bağlantı testi gerekli", "warn");
        setStatus("connection-status", "Alan değişti; tekrar bağlantı testi yapın.", "warn");
      }
      updateEnrollmentButtonState();
    });
  }
}

function mapConnectionBadge(result) {
  return result.success
    ? { text: "Bağlantı başarılı", kind: "ok" }
    : { text: result.status || "hata", kind: result.status === "timeout" ? "warn" : "error" };
}

function mapEnrollmentError(error) {
  const message = (error?.message || "").toLowerCase();
  const status = error?.status;

  if (status === 401 || status === 403) return "Enrollment reddedildi.";
  if (message.includes("geçersiz enrollment") || message.includes("invalid enrollment")) return "Enrollment code geçersiz.";
  if (message.includes("kullanım sayısına ulaştı") || message.includes("kullanılmış") || message.includes("used")) return "Enrollment code kullanılmış.";
  if (message.includes("süresi dolmuş") || message.includes("expired")) return "Enrollment code süresi dolmuş.";
  if (message.includes("tls") || message.includes("certificate")) return "TLS hatası.";
  if (message.includes("erişilemiyor") || message.includes("dns") || message.includes("timeout") || message.includes("connection")) return "STYS erişilemiyor.";
  if (message.includes("credential") || message.includes("secret")) return "Credential kaydedilemedi.";
  if (message.includes("token")) return "Token alınamadı.";
  if (message.includes("geçersiz stys adresi") || message.includes("invalid url")) return "Geçersiz STYS adresi.";
  return error?.message || "İşlem başarısız.";
}

async function loadDashboard() {
  const data = await getJson("/api/bootstrap/dashboard");
  state.dashboardLoaded = true;

  setStatus("dashboard-status", data.credentialMevcutMu ? "Kayıtlı agent bilgileri yüklendi." : "Agent kaydı bekleniyor.", data.credentialMevcutMu ? "ok" : "muted");
  setText("agent-status", data.agent?.onlineMi ? "Online" : (data.agentDurumu || "-"));
  setText("agent-id", data.agent?.agentId ?? data.agent?.AgentId ?? "-");
  setText("agent-name", data.agent?.agentAd || data.agent?.AgentAd || data.agentDisplayName || "-");
  setText("kurum-name", data.agent?.kurumAd || data.agent?.KurumAd || "-");
  renderTesisList("tesis-list", data.agent?.tesisler || data.agent?.Tesisler || []);
  renderPills("scope-list", data.agent?.scopes || data.agent?.Scopes || []);
  renderPills("capability-list", data.agent?.capabilities || data.agent?.Capabilities || []);
  setText("credential-present", data.credentialMevcutMu ? "Evet" : "Hayır");
  setText("stys-address", data.stysAdresi || "-");
  setText("agent-version", data.agentVersion || "-");
  setText("local-ui-version", data.localUiVersion || "-");
  setText("last-heartbeat", data.agent?.lastHeartbeatAt || data.agent?.LastHeartbeatAt || "-");

  if (data.sonBaglantiTesti) {
    const result = data.sonBaglantiTesti;
    const badge = mapConnectionBadge(result);
    setBadge("last-connection-status", badge.text, badge.kind);
    setText("last-connection-server-time", result.serverTime || "-");
    setText("last-connection-version", result.version || "-");
  }

  const wizardVisible = !data.credentialMevcutMu;
  setHidden("enrollment-wizard-card", !wizardVisible ? true : false);
  if (wizardVisible) {
    setStatus("wizard-status", "Bağlantı testi sonrası STYS'e kayıt yapabilirsiniz.", "muted");
  } else {
    setStatus("wizard-status", "Agent kayıtlı. Yeniden enrollment bu fazda kapalı.", "ok");
  }

  updateEnrollmentButtonState();
}

async function loadConfig() {
  const cfg = await getJson("/api/bootstrap/config");
  const baseUrlInput = $("#stys-base-url");
  const agentNameInput = $("#agent-display-name");
  const timeoutInput = $("#http-timeout-seconds");
  const portInput = $("#local-ui-port");

  if (baseUrlInput) baseUrlInput.value = cfg.stysBaseUrl ?? "";
  if (agentNameInput) agentNameInput.value = cfg.agentDisplayName ?? "";
  if (timeoutInput) timeoutInput.value = cfg.httpTimeoutSeconds ?? 30;
  if (portInput) portInput.value = cfg.localUiPort ?? 5180;

  state.connectionOk = false;
  setBadge("connection-badge", "Bağlantı testi gerekli", "warn");
  setStatus("connection-status", "Hazır.", "muted");
  updateEnrollmentButtonState();
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

  setStatus("save-status", "Kaydedildi. Port değişikliği için Agent yeniden başlatılmalı.", "ok");
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

  const badge = mapConnectionBadge(result);
  setBadge("connection-badge", badge.text, badge.kind);
  setStatus("connection-status", result.message || "Bağlantı testi tamamlandı.", result.success ? "ok" : (result.status === "timeout" ? "warn" : "error"));
  setText("connection-server-time", result.serverTime || "-");
  setText("connection-version", result.version || "-");
  state.connectionOk = !!result.success;
  updateEnrollmentButtonState();
}

async function submitEnrollment(event) {
  event.preventDefault();

  const payload = {
    stysBaseUrl: normalizeBaseUrl($("#stys-base-url").value),
    agentDisplayName: $("#agent-display-name").value.trim(),
    enrollmentCode: $("#enrollment-code").value.trim(),
    httpTimeoutSeconds: Number($("#http-timeout-seconds").value),
    localUiPort: Number($("#local-ui-port").value),
    capabilities: []
  };

  if (!state.connectionOk) {
    setStatus("enroll-status", "Önce bağlantı testi başarılı olmalı.", "warn");
    return;
  }

  const enrollBtn = $("#enroll-btn");
  if (enrollBtn) enrollBtn.disabled = true;

  try {
    const result = await getJson("/api/bootstrap/enroll", {
      method: "POST",
      body: JSON.stringify(payload)
    });

    $("#enrollment-code").value = "";
    setStatus("enroll-status", result.message || "✓ STYS'e kayıt başarılı", "ok");
    state.connectionOk = false;
    setBadge("connection-badge", "Bağlantı testi gerekli", "warn");
    updateEnrollmentButtonState();
    await loadDashboard();
  } catch (error) {
    setStatus("enroll-status", mapEnrollmentError(error), "error");
    updateEnrollmentButtonState();
  }
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

function initEnrollmentPage() {
  const form = $("#enrollment-form");
  if (form) form.addEventListener("submit", submitEnrollment);

  const testButton = $("#test-connection-btn");
  if (testButton) {
    testButton.addEventListener("click", (event) => {
      event.preventDefault();
      testConnection().catch((error) => {
        setBadge("connection-badge", "hata", "error");
        setStatus("connection-status", mapEnrollmentError(error), "error");
        state.connectionOk = false;
        updateEnrollmentButtonState();
      });
    });
  }

  bindEnrollmentFormState();
}

async function bootstrap() {
  initNavigation();

  if ($("#bootstrap-form")) {
    $("#bootstrap-form").addEventListener("submit", saveConfig);
    $("#test-connection-btn")?.addEventListener("click", (event) => {
      event.preventDefault();
      testConnection().catch((error) => {
        setStatus("connection-status", mapEnrollmentError(error), "error");
      });
    });
    await loadConfig().catch((error) => {
      setStatus("save-status", error.message, "error");
    });
  }

  if ($("#dashboard-root")) {
    initEnrollmentPage();
    await Promise.all([
      loadConfig().catch(() => {}),
      loadDashboard().catch((error) => {
        setStatus("dashboard-status", error.message, "error");
      })
    ]);
  }
}

window.addEventListener("DOMContentLoaded", bootstrap);
