const state = {
  connectionOk: false,
  dashboardTimer: null,
  diagnosticsTimer: null
};

const RESET_CONFIRMATION_TEXT = "Bu işlem yerel Agent kimlik bilgilerini silecek. Merkezi STYS kaydı silinmeyecektir. Agent yeniden enrollment gerektirecektir.";

function $(id) {
  return document.getElementById(id);
}

function $$(selector) {
  return Array.from(document.querySelectorAll(selector));
}

function setText(id, value) {
  const el = $(id);
  if (el) el.textContent = value ?? "-";
}

function setBadge(id, value, kind = "muted") {
  const el = $(id);
  if (!el) return;
  el.textContent = value ?? "-";
  el.className = `badge ${kind}`;
}

function setStatus(id, value, kind = "muted") {
  const el = $(id);
  if (!el) return;
  el.textContent = value ?? "-";
  el.className = `status ${kind}`;
}

function setHidden(id, hidden) {
  const el = $(id);
  if (!el) return;
  el.classList.toggle("hidden", !!hidden);
}

function normalizeBaseUrl(value) {
  const input = (value || "").trim();
  if (!input) return "";
  return input.endsWith("/") ? input.slice(0, -1) : input;
}

async function getJson(url, options = {}) {
  const response = await fetch(url, {
    headers: {
      "Content-Type": "application/json",
      ...(options.headers || {})
    },
    ...options
  });

  const text = await response.text();
  const payload = text ? JSON.parse(text) : null;

  if (!response.ok) {
    const error = new Error(payload?.message || response.statusText || "İstek başarısız.");
    error.status = response.status;
    error.payload = payload;
    throw error;
  }

  return payload;
}

function renderPills(id, values) {
  const el = $(id);
  if (!el) return;

  const items = Array.isArray(values) ? values.filter(Boolean) : [];
  el.innerHTML = items.length
    ? items.map((item) => `<span class="pill">${escapeHtml(String(item))}</span>`).join("")
    : "<span class=\"muted\">-</span>";
}

function renderTesisList(id, tesisler) {
  const el = $(id);
  if (!el) return;

  const items = Array.isArray(tesisler) ? tesisler : [];
  el.innerHTML = items.length
    ? items.map((item) => `<span class="pill">${escapeHtml(item?.ad || item?.Ad || String(item?.id ?? item?.Id ?? "-"))}</span>`).join("")
    : "<span class=\"muted\">-</span>";
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function formatDateTime(value) {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  return new Intl.DateTimeFormat("tr-TR", {
    dateStyle: "medium",
    timeStyle: "medium"
  }).format(date);
}

function formatDuration(startValue) {
  if (!startValue) return "-";
  const start = new Date(startValue);
  if (Number.isNaN(start.getTime())) return String(startValue);
  const ms = Math.max(0, Date.now() - start.getTime());
  const totalSeconds = Math.floor(ms / 1000);
  const days = Math.floor(totalSeconds / 86400);
  const hours = Math.floor((totalSeconds % 86400) / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return `${days}g ${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
}

function mapConnectionBadge(result) {
  if (!result) return { text: "-", kind: "muted" };
  if (result.success) return { text: "Bağlantı başarılı", kind: "ok" };
  if (result.status === "timeout") return { text: "Timeout", kind: "warn" };
  if (result.status === "tls-error") return { text: "TLS hata", kind: "error" };
  if (result.status === "dns-error") return { text: "DNS hata", kind: "error" };
  if (result.status?.startsWith("http-")) return { text: result.status.toUpperCase(), kind: "warn" };
  return { text: result.status || "Hata", kind: "error" };
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

function updateEnrollmentButtonState() {
  const enrollBtn = $("enroll-btn");
  if (!enrollBtn) return;
  enrollBtn.disabled = !state.connectionOk;
}

function deriveDashboardMessage(data) {
  if (data?.runtime?.requiresReEnrollment) return "Yeniden enrollment gerekiyor.";
  if (!data?.credentialMevcutMu) return "Agent kaydı bekleniyor.";
  if (!data?.runtime?.authenticationReady) return "Kimlik doğrulama bekleniyor.";
  if (data?.agent?.onlineMi) return "Agent online.";
  return "Agent kayıtlı fakat online değil.";
}

async function loadConfig() {
  const cfg = await getJson("/api/bootstrap/config");

  if ($("stys-base-url")) $("stys-base-url").value = cfg.stysBaseUrl ?? "";
  if ($("agent-display-name")) $("agent-display-name").value = cfg.agentDisplayName ?? "";
  if ($("http-timeout-seconds")) $("http-timeout-seconds").value = cfg.httpTimeoutSeconds ?? 30;
  if ($("local-ui-port")) $("local-ui-port").value = cfg.localUiPort ?? 5180;
  state.connectionOk = false;
  setBadge("connection-badge", "Bağlantı testi gerekli", "warn");
  updateEnrollmentButtonState();

  return cfg;
}

async function saveConfig(event) {
  event.preventDefault();

  const payload = {
    stysBaseUrl: normalizeBaseUrl($("stys-base-url").value),
    agentDisplayName: $("agent-display-name").value,
    httpTimeoutSeconds: Number($("http-timeout-seconds").value),
    localUiPort: Number($("local-ui-port").value)
  };

  const result = await getJson("/api/bootstrap/config", {
    method: "POST",
    body: JSON.stringify(payload)
  });

  const config = result.configuration || {};
  if ($("stys-base-url")) $("stys-base-url").value = config.stysBaseUrl ?? "";
  if ($("agent-display-name")) $("agent-display-name").value = config.agentDisplayName ?? "";
  if ($("http-timeout-seconds")) $("http-timeout-seconds").value = config.httpTimeoutSeconds ?? 30;
  if ($("local-ui-port")) $("local-ui-port").value = config.localUiPort ?? 5180;

  const kind = result.reEnrollmentRequired ? "warn" : "ok";
  setStatus("save-status", result.message || "Kaydedildi.", kind);
  setBadge("restart-required-badge", result.restartRequired ? "Restart gerekli" : "Restart gerekmez", result.restartRequired ? "warn" : "ok");
  setBadge("reenrollment-required-badge", result.reEnrollmentRequired ? "Re-enrollment gerekli" : "Re-enrollment gerekmez", result.reEnrollmentRequired ? "warn" : "ok");

  if (result.reEnrollmentRequired) {
    setStatus("config-warning", "Bu STYS adresi için mevcut local credential geçerli değil. Yeniden enrollment gerekir.", "warn");
  } else {
    setStatus("config-warning", "Bootstrap ayarları kaydedildi.", "ok");
  }

  await refreshAll();
}

async function testConnection() {
  const payload = {
    stysBaseUrl: normalizeBaseUrl($("stys-base-url").value),
    agentDisplayName: $("agent-display-name").value,
    httpTimeoutSeconds: Number($("http-timeout-seconds").value),
    localUiPort: Number($("local-ui-port").value)
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

  if (!state.connectionOk) {
    setStatus("enroll-status", "Önce bağlantı testi başarılı olmalı.", "warn");
    return;
  }

  const payload = {
    stysBaseUrl: normalizeBaseUrl($("stys-base-url").value),
    agentDisplayName: $("agent-display-name").value.trim(),
    enrollmentCode: $("enrollment-code").value.trim(),
    httpTimeoutSeconds: Number($("http-timeout-seconds").value),
    localUiPort: Number($("local-ui-port").value),
    capabilities: []
  };

  const enrollBtn = $("enroll-btn");
  if (enrollBtn) enrollBtn.disabled = true;

  try {
    const result = await getJson("/api/bootstrap/enroll", {
      method: "POST",
      body: JSON.stringify(payload)
    });

    if ($("enrollment-code")) $("enrollment-code").value = "";
    setStatus("enroll-status", result.message || "✓ STYS'e kayıt başarılı", "ok");
    state.connectionOk = false;
    setBadge("connection-badge", "Bağlantı testi gerekli", "warn");
    updateEnrollmentButtonState();
    await refreshAll();
  } catch (error) {
    setStatus("enroll-status", mapEnrollmentError(error), "error");
    updateEnrollmentButtonState();
  }
}

async function loadDashboard() {
  const data = await getJson("/api/bootstrap/dashboard");

  setStatus("dashboard-status", deriveDashboardMessage(data), data?.runtime?.requiresReEnrollment ? "warn" : (data?.credentialMevcutMu ? "ok" : "muted"));
  setText("agent-status", data?.agentDurumu || "-");
  setText("agent-id", data?.agent?.agentId ?? data?.agent?.AgentId ?? "-");
  setText("agent-name", data?.agent?.agentAd || data?.agent?.AgentAd || data?.agentDisplayName || "-");
  setText("kurum-name", data?.agent?.kurumAd || data?.agent?.KurumAd || "-");
  renderTesisList("tesis-list", data?.agent?.tesisler || data?.agent?.Tesisler || []);
  renderPills("scope-list", data?.agent?.scopes || data?.agent?.Scopes || []);
  renderPills("capability-list", data?.agent?.capabilities || data?.agent?.Capabilities || []);
  setText("credential-present", data?.credentialMevcutMu ? "Evet" : "Hayır");
  setText("auth-ready", data?.runtime?.authenticationReady ? "Evet" : "Hayır");
  setText("stys-address", data?.stysAdresi || "-");
  setText("stys-server-version", data?.stysServerVersion || data?.sonBaglantiTesti?.version || "-");
  setText("agent-version", data?.agentVersion || "-");
  setText("local-ui-version", data?.localUiVersion || "-");
  setText("last-heartbeat", formatDateTime(data?.agent?.lastHeartbeatAt || data?.runtime?.lastHeartbeatSuccessAt));
  setText("last-connection-test", data?.sonBaglantiTesti?.message || "-");
  setText("heartbeat-worker-status", data?.heartbeatWorkerDurumu || "-");
  setText("command-worker-status", data?.commandWorkerDurumu || "-");
  setText("last-reset-at", formatDateTime(data?.runtime?.lastResetAt));
  setText("re-enrollment-note", data?.reEnrollmentNotu || data?.runtime?.requiresReEnrollmentReason || "-");

  const badge = mapConnectionBadge(data?.sonBaglantiTesti);
  setBadge("last-connection-status", badge.text, badge.kind);
  setText("last-connection-server-time", data?.sonBaglantiTesti?.serverTime || "-");
  setText("last-connection-version", data?.sonBaglantiTesti?.version || "-");
  setText("stys-connection-status", data?.stysConnectionDurumu || "-");

  const wizardVisible = !data?.credentialMevcutMu || !!data?.runtime?.requiresReEnrollment;
  setHidden("enrollment-wizard-card", !wizardVisible);
  setStatus("wizard-status", wizardVisible
    ? (data?.runtime?.requiresReEnrollment
      ? "Mevcut credential bu STYS adresi için geçerli değil. Yeni enrollment yapabilirsiniz."
      : "Bağlantı testi başarılı olduktan sonra enrollment başlatılabilir.")
    : "Agent kayıtlı.", wizardVisible ? (data?.runtime?.requiresReEnrollment ? "warn" : "muted") : "ok");

  setHidden("reset-card", !data?.credentialMevcutMu && !data?.runtime?.requiresReEnrollment);
  if (data?.runtime?.requiresReEnrollment) {
    setStatus("reset-status", "Bu agent için controlled reset veya yeni enrollment gerekir.", "warn");
  }

  return data;
}

async function loadDiagnostics() {
  const data = await getJson("/api/bootstrap/diagnostics");

  setStatus("diagnostics-status", "Diagnostics güncellendi.", "ok");
  setText("diag-process-id", data.processId || "-");
  setText("diag-process-start", formatDateTime(data.processStartTimeUtc));
  setText("diag-uptime", data.uptime || formatDuration(data.processStartTimeUtc));
  setText("diag-machine", data.machineName || "-");
  setText("diag-os", data.operatingSystem || "-");
  setText("diag-framework", data.frameworkDescription || "-");
  setText("diag-agent-version", data.agentVersion || "-");
  setText("diag-local-version", data.localUiVersion || "-");
  setText("diag-data-dir", data.dataDirectory || "-");
  setText("diag-bootstrap-path", data.bootstrapConfigurationPath || "-");
  setText("diag-credential-path", data.credentialStorePath || "-");
  setText("diag-stys-base-url", data.stysBaseUrl || "-");
  setText("diag-credential-present", data.credentialPresent ? "Evet" : "Hayır");
  setText("diag-auth-ready", data.authenticationReady ? "Evet" : "Hayır");
  setText("diag-reenrollment", data.requiresReEnrollment ? (data.requiresReEnrollmentReason || "Evet") : "Hayır");
  setText("diag-last-connection", formatDateTime(data.lastSuccessfulStysConnectionAt));
  setText("diag-last-heartbeat", formatDateTime(data.lastHeartbeatSuccessAt));
  setText("diag-last-heartbeat-error", data.lastHeartbeatError || "-");
  setText("diag-last-command", formatDateTime(data.lastCommandPollSuccessAt));
  setText("diag-last-command-error", data.lastCommandPollError || "-");
  setText("diag-last-reset", formatDateTime(data.lastResetAt));

  renderLogTable(data.recentLogs || []);
  return data;
}

function renderLogTable(entries) {
  const body = $("diagnostics-log-body");
  if (!body) return;

  const rows = Array.isArray(entries) ? entries : [];
  body.innerHTML = rows.length
    ? rows.map((entry) => `
        <tr>
          <td class="mono">${escapeHtml(formatDateTime(entry.timestampUtc))}</td>
          <td><span class="badge ${logLevelKind(entry.level)}">${escapeHtml(entry.level || "-")}</span></td>
          <td class="mono">${escapeHtml(entry.category || "-")}</td>
          <td>${escapeHtml(entry.message || "-")}</td>
        </tr>`).join("")
    : `<tr><td colspan="4" class="muted">Log bulunamadı.</td></tr>`;
}

function logLevelKind(level) {
  const normalized = String(level || "").toLowerCase();
  if (normalized.includes("critical") || normalized.includes("error")) return "error";
  if (normalized.includes("warning")) return "warn";
  if (normalized.includes("information") || normalized.includes("info")) return "ok";
  return "muted";
}

async function resetEnrollment(event) {
  event.preventDefault();

  const confirmation = $("reset-confirmation")?.value || "";
  if (confirmation.trim() !== RESET_CONFIRMATION_TEXT) {
    setStatus("reset-status", "Onay metni tam olarak eşleşmiyor.", "error");
    return;
  }

  const result = await getJson("/api/bootstrap/reset", {
    method: "POST",
    body: JSON.stringify({ confirmationText: confirmation })
  });

  if ($("reset-confirmation")) $("reset-confirmation").value = "";
  setStatus("reset-status", result.message || "Sıfırlandı.", "ok");
  await refreshAll();
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

function bindDashboardPage() {
  $("enrollment-form")?.addEventListener("submit", submitEnrollment);
  $("test-connection-btn")?.addEventListener("click", (event) => {
    event.preventDefault();
    testConnection().catch((error) => {
      setBadge("connection-badge", "hata", "error");
      setStatus("connection-status", mapEnrollmentError(error), "error");
      state.connectionOk = false;
      updateEnrollmentButtonState();
    });
  });
  $("reset-form")?.addEventListener("submit", resetEnrollment);
}

function bindSetupPage() {
  $("bootstrap-form")?.addEventListener("submit", saveConfig);
  $("test-connection-btn")?.addEventListener("click", (event) => {
    event.preventDefault();
    testConnection().catch((error) => {
      setStatus("connection-status", mapEnrollmentError(error), "error");
    });
  });
}

function startRefreshers() {
  stopRefreshers();

  if ($("dashboard-root")) {
    state.dashboardTimer = window.setInterval(() => {
      loadDashboard().catch(() => {});
    }, 5000);
  }

  if ($("diagnostics-root")) {
    state.diagnosticsTimer = window.setInterval(() => {
      loadDiagnostics().catch(() => {});
    }, 5000);
  }
}

function stopRefreshers() {
  if (state.dashboardTimer) {
    clearInterval(state.dashboardTimer);
    state.dashboardTimer = null;
  }

  if (state.diagnosticsTimer) {
    clearInterval(state.diagnosticsTimer);
    state.diagnosticsTimer = null;
  }
}

async function refreshAll() {
  const tasks = [];

  if ($("dashboard-root")) {
    tasks.push(loadDashboard().catch((error) => {
      setStatus("dashboard-status", error.message, "error");
    }));
  }

  if ($("diagnostics-root")) {
    tasks.push(loadDiagnostics().catch((error) => {
      setStatus("diagnostics-status", error.message, "error");
    }));
  }

  if ($("bootstrap-form")) {
    tasks.push(loadConfig().catch((error) => {
      setStatus("save-status", error.message, "error");
    }));
  }

  await Promise.all(tasks);
}

async function bootstrap() {
  initNavigation();

  if ($("bootstrap-form")) {
    bindSetupPage();
    await loadConfig().catch((error) => {
      setStatus("save-status", error.message, "error");
    });
  }

  if ($("dashboard-root")) {
    bindDashboardPage();
    await loadConfig().catch(() => {});
    await loadDashboard().catch((error) => {
      setStatus("dashboard-status", error.message, "error");
    });
  }

  if ($("diagnostics-root")) {
    await loadDiagnostics().catch((error) => {
      setStatus("diagnostics-status", error.message, "error");
    });
  }

  startRefreshers();
}

window.addEventListener("DOMContentLoaded", bootstrap);
