const state = {
  connectionOk: false,
  dashboardTimer: null,
  diagnosticsTimer: null,
  localDevicesTimer: null,
  localDeviceEditingId: null,
  selectedLocalDeviceId: null,
  selectedLocalDeviceTerminals: [],
  agentSelf: null,
  selectedProvisioningTesisId: null,
  selectedProvisioningCandidate: null,
  selectedPaymentTerminalId: null
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

function localDeviceTypeLabel(value) {
  return Number(value) === 1 ? "Printer" : "POS";
}

function localDeviceProviderLabel(value) {
  return "PAVO";
}

function localDeviceProtocolLabel(value) {
  return Number(value) === 1 ? "HTTPS" : "HTTP";
}

function localDeviceStatusBadge(status) {
  const value = Number(status);
  if (value === 1) return { text: "Connected", kind: "ok" };
  if (value === 2) return { text: "Unreachable", kind: "warn" };
  if (value === 3) return { text: "Timeout", kind: "warn" };
  if (value === 4) return { text: "TlsError", kind: "error" };
  if (value === 5) return { text: "ProtocolError", kind: "error" };
  return { text: "Unknown", kind: "muted" };
}

function localDevicePairingBadge(status) {
  const value = Number(status);
  if (value === 1) return { text: "Paired", kind: "ok" };
  if (value === 2) return { text: "Failed", kind: "error" };
  return { text: "NotPaired", kind: "muted" };
}

function localDeviceProvisioningBadge(status) {
  const value = Number(status);
  if (value === 1) return { text: "STYS'e kaydedildi", kind: "ok" };
  if (value === 2) return { text: "Yeniden eşitle gerekli", kind: "warn" };
  if (value === 3) return { text: "Çakışma", kind: "error" };
  if (value === 4) return { text: "Devre dışı", kind: "error" };
  if (value === 5) return { text: "Kayıt başarısız", kind: "error" };
  return { text: "Kaydedilmedi", kind: "muted" };
}

function localDeviceStysReconciliationBadge(status) {
  const value = Number(status);
  if (value === 0) return { text: "InSync", kind: "ok" };
  if (value === 1) return { text: "ReProvisionRequired", kind: "warn" };
  if (value === 2) return { text: "CentralMissing", kind: "muted" };
  if (value === 3) return { text: "OwnershipConflict", kind: "error" };
  if (value === 4) return { text: "Disabled", kind: "error" };
  return { text: "-", kind: "muted" };
}

function formatDeviceAddress(device) {
  if (!device?.host) return "-";
  const protocol = Number(device.protocol) === 1 ? "https" : "http";
  const port = Number(device.protocol) === 1
    ? Number(device.httpsPort || 4568)
    : Number(device.httpPort || 4567);
  return `${protocol}://${device.host}:${port}`;
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

function mapLocalDeviceError(error) {
  const message = (error?.message || "").toLowerCase();
  if (message.includes("host")) return "Geçersiz host/IP değeri.";
  if (message.includes("protocol")) return "Geçersiz protocol.";
  if (message.includes("port")) return "Port değeri geçersiz.";
  if (message.includes("tester")) return "Bu provider için connection tester yok.";
  if (message.includes("bulunamadı")) return "Cihaz bulunamadı.";
  if (message.includes("eşleştirilmiş")) return "Bu cihaz zaten eşleştirilmiş.";
  if (message.includes("önce cihaz bilgisini getir") || message.includes("cihaz bilgisi alınmalıdır")) return "Önce cihaz bilgisini alın.";
  if (message.includes("önce pavo cihazı ile pairing yapılmalıdır")) return "Önce PAVO cihazı ile pairing yapılmalıdır.";
  if (message.includes("ödeme testi") && message.includes("hazır değil")) return "Cihaz ödeme için hazır değil.";
  if (message.includes("ödeme testi") && message.includes("aktif terminal")) return "Ödeme testi için aktif terminal gerekli.";
  if (message.includes("ödeme testi") && message.includes("bağlantı testi")) return "Cihazla iletişim kurulamadı.";
  if (message.includes("tesis seçimi zorunludur")) return "Tesis seçimi zorunludur.";
  if (message.includes("agent kapsamı")) return "Seçilen tesis agent kapsamı dışında.";
  if (message.includes("başka agent'a bağlı") || message.includes("başka agent yerel cihazına bağlı")) return "Bu cihaz başka Agent'a bağlı.";
  if (message.includes("başka tesise bağlı")) return "Bu cihaz başka tesise kayıtlı.";
  if (message.includes("devre dışı")) return "Merkezi cihaz devre dışı.";
  if (message.includes("çakışma")) return "Merkezi cihaz çakışması var.";
  return error?.message || "İşlem başarısız.";
}

function localDeviceOperationMessage(error) {
  if (!error) return "İşlem tamamlandı.";
  return mapLocalDeviceError(error);
}

function updateEnrollmentButtonState() {
  const enrollBtn = $("enroll-btn");
  if (!enrollBtn) return;
  enrollBtn.disabled = !state.connectionOk;
}

function setLocalDeviceFormMode(device) {
  const title = $("local-device-form-title");
  const saveBtn = $("local-device-save-btn");
  const cancelBtn = $("local-device-cancel-btn");
  const idField = $("local-device-id");

  state.localDeviceEditingId = device?.id || null;
  if (idField) idField.value = device?.id || "";
  if (title) title.textContent = device ? "Cihaz Düzenle" : "Yeni Cihaz";
  if (saveBtn) saveBtn.textContent = device ? "Güncelle" : "Kaydet";
  setHidden("local-device-cancel-btn", !device);
  if (cancelBtn) {
    cancelBtn.disabled = false;
  }
}

function resetLocalDeviceForm() {
  if ($("local-device-form")) {
    $("local-device-form").reset();
  }

  if ($("local-device-type")) $("local-device-type").value = "0";
  if ($("local-device-provider")) $("local-device-provider").value = "0";
  if ($("local-device-protocol")) $("local-device-protocol").value = "0";
  if ($("local-device-http-port")) $("local-device-http-port").value = 4567;
  if ($("local-device-https-port")) $("local-device-https-port").value = 4568;
  if ($("local-device-serial-number")) $("local-device-serial-number").value = "";
  if ($("local-device-host")) $("local-device-host").value = "";
  if ($("local-device-display-name")) $("local-device-display-name").value = "";
  setLocalDeviceFormMode(null);
  setStatus("local-device-form-status", "Yeni cihaz girilebilir.", "muted");
}

function syncLocalDevicePortDefaults() {
  const protocol = Number($("local-device-protocol")?.value || 0);
  const httpPort = $("local-device-http-port");
  const httpsPort = $("local-device-https-port");

  if (protocol === 1) {
    if (httpPort && (!httpPort.value || Number(httpPort.value) === 0)) {
      httpPort.value = 4567;
    }
    if (httpsPort && (!httpsPort.value || Number(httpsPort.value) === 0)) {
      httpsPort.value = 4568;
    }
  } else {
    if (httpPort && (!httpPort.value || Number(httpPort.value) === 0)) {
      httpPort.value = 4567;
    }
    if (httpsPort && (!httpsPort.value || Number(httpsPort.value) === 0)) {
      httpsPort.value = 4568;
    }
  }
}

function collectLocalDeviceFormPayload() {
  return {
    id: $("local-device-id")?.value?.trim() || null,
    displayName: $("local-device-display-name")?.value?.trim() || "",
    deviceType: Number($("local-device-type")?.value || 0),
    provider: Number($("local-device-provider")?.value || 0),
    host: $("local-device-host")?.value?.trim() || "",
    protocol: Number($("local-device-protocol")?.value || 0),
    httpPort: Number($("local-device-http-port")?.value || 4567),
    httpsPort: Number($("local-device-https-port")?.value || 4568),
    serialNumber: $("local-device-serial-number")?.value?.trim() || null
  };
}

function formatLocalDeviceAddress(device) {
  if (!device?.host) return "-";
  const protocol = Number(device.protocol) === 1 ? "https" : "http";
  const port = Number(device.protocol) === 1
    ? Number(device.httpsPort || 4568)
    : Number(device.httpPort || 4567);
  return `${protocol}://${device.host}:${port}`;
}

function localDeviceTitle(device) {
  return device?.displayName || "-";
}

function localDeviceModelText(device) {
  return device?.deviceName || device?.serialNumber || "-";
}

function renderLocalDeviceDetail(device) {
  const card = $("local-device-detail-card");
  if (!card) return;

  if (!device) {
    card.classList.add("hidden");
    return;
  }

  card.classList.remove("hidden");
  const isPaired = Number(device.pairingStatus) === 1;
  setText("local-device-detail-title", device.displayName || "Cihaz Detayı");
  setText("local-device-detail-subtitle", `${localDeviceTitle(device)} • ${formatLocalDeviceAddress(device)}`);
  setText("local-device-detail-connection", localDeviceStatusBadge(device.status).text);
  setText("local-device-detail-pairing", localDevicePairingBadge(device.pairingStatus).text);
  setText("local-device-detail-serial", device.serialNumber || "-");
  setText("local-device-detail-device-name", localDeviceModelText(device));
  setText("local-device-detail-device-info-at", formatDateTime(device.lastDeviceInfoAt));
  setText("local-device-detail-pairing-at", formatDateTime(device.lastPairingAt));
  setText("local-device-detail-address", formatLocalDeviceAddress(device));
  setText("local-device-detail-central-id", device.centralPosCihaziId ?? device.CentralPosCihaziId ?? "-");
  setText("local-device-detail-provisioning-at", formatDateTime(device.lastProvisionedAt || device.LastProvisionedAt));
  const provisioningBadge = localDeviceProvisioningBadge(device.provisioningStatus ?? device.ProvisioningStatus);
  setText("local-device-detail-provisioning-status", provisioningBadge.text);
  const provisioningStatusEl = $("local-device-detail-provisioning-status");
  if (provisioningStatusEl) provisioningStatusEl.className = `value small-value badge ${provisioningBadge.kind}`;
  const stysStatusBadge = localDeviceStysReconciliationBadge(device.stysReconciliationStatus ?? device.StysReconciliationStatus);
  setText("local-device-detail-stys-status", stysStatusBadge.text);
  const stysStatusEl = $("local-device-detail-stys-status");
  if (stysStatusEl) stysStatusEl.className = `value small-value badge ${stysStatusBadge.kind}`;
  setStatus("local-device-detail-stys-message", device.stysReconciliationMessage || device.StysReconciliationMessage || "-", stysStatusBadge.kind);
  setText("local-device-detail-last-test", formatDateTime(device.lastConnectionTestAt));
  setStatus("local-device-detail-last-result", device.lastError || "Hazır.", device.lastError ? "error" : "ok");
  setStatus("local-device-detail-last-pairing-error", device.lastPairingError || "-", device.lastPairingError ? "error" : "muted");

  const warning = $("local-device-detail-warning");
  if (warning) {
    warning.textContent = isPaired
      ? ""
      : "Bu cihaz eşleşmiş değil. Cihaz bilgisi alındıktan sonra pairing başlatılabilir.";
    warning.className = isPaired ? "status warn hidden" : "status warn";
  }

  const pairBtn = $("local-device-detail-pair-btn");
  if (pairBtn) {
    pairBtn.textContent = isPaired ? "Yeniden Pairing" : "Pairing Başlat";
    pairBtn.dataset.forceRePair = isPaired ? "true" : "false";
    pairBtn.disabled = false;
  }

  const stysBtn = $("local-device-detail-stys-btn");
  if (stysBtn) {
    stysBtn.disabled = false;
  }

  const discoverBtn = $("local-device-detail-discover-btn");
  if (discoverBtn) {
    discoverBtn.disabled = !isPaired;
  }

  const candidateBtn = $("local-device-provisioning-preview-btn");
  if (candidateBtn) {
    candidateBtn.disabled = !isPaired;
  }

  const paymentSubmitBtn = $("local-device-payment-submit-btn");
  if (paymentSubmitBtn) {
    paymentSubmitBtn.disabled = !isPaired;
  }

  const paymentTerminalSelect = $("local-device-payment-terminal");
  if (paymentTerminalSelect) {
    paymentTerminalSelect.disabled = !isPaired || state.selectedLocalDeviceTerminals.length === 0;
  }

  const saveBtn = $("provisioning-save-btn");
  if (saveBtn) {
    saveBtn.disabled = !state.selectedProvisioningCandidate;
    const hasCentralRecord = Boolean(device.centralPosCihaziId ?? device.CentralPosCihaziId);
    saveBtn.textContent = hasCentralRecord ? "STYS ile Yeniden Eşitle" : "STYS'e Kaydet";
  }
}

function localDeviceTerminalStatusBadge(terminal) {
  if (!terminal) return { text: "-", kind: "muted" };
  return terminal.active ? { text: "Aktif", kind: "ok" } : { text: "Pasif", kind: "muted" };
}

function renderLocalDeviceTerminalRows(terminals) {
  const body = $("local-device-terminals-table-body");
  if (!body) return;

  const items = Array.isArray(terminals) ? terminals : [];
  state.selectedLocalDeviceTerminals = items;
  renderPaymentTerminalOptions(items);
  body.innerHTML = items.length
    ? items.map((terminal) => {
        const badge = localDeviceTerminalStatusBadge(terminal);
        return `
          <tr>
            <td>${escapeHtml(terminal.acquirerName || terminal.acquirerId || "-")}</td>
            <td class="mono">${escapeHtml(terminal.terminalId || "-")}</td>
            <td class="mono">${escapeHtml(terminal.merchantId || "-")}</td>
            <td><span class="badge ${badge.kind}">${escapeHtml(badge.text)}</span></td>
            <td>${escapeHtml(formatDateTime(terminal.lastDiscoveredAt))}</td>
            <td>${terminal.active ? "Evet" : "Hayır"}</td>
          </tr>`;
      }).join("")
    : `<tr><td colspan="6" class="muted">Keşfedilmiş terminal yok.</td></tr>`;
}

function renderPaymentTerminalOptions(terminals) {
  const select = $("local-device-payment-terminal");
  if (!select) return;

  const activeTerminals = (Array.isArray(terminals) ? terminals : []).filter((terminal) => terminal?.active);
  const previous = select.value;
  select.innerHTML = activeTerminals.length
    ? activeTerminals.map((terminal) => `<option value="${escapeHtml(terminal.terminalId || "")}">${escapeHtml(`${terminal.acquirerName || terminal.acquirerId || "-"} • ${terminal.terminalId || "-"}`)}</option>`).join("")
    : '<option value="">Aktif terminal yok</option>';

  if (activeTerminals.some((terminal) => String(terminal.terminalId) === String(previous))) {
    select.value = previous;
  } else if (activeTerminals.length > 0) {
    select.value = String(activeTerminals[0].terminalId || "");
  }

  state.selectedPaymentTerminalId = select.value || null;
  ensureLocalPaymentSaleReference();
}

function generateLocalPaymentSaleReference() {
  const now = new Date();
  const stamp = [
    now.getFullYear(),
    String(now.getMonth() + 1).padStart(2, "0"),
    String(now.getDate()).padStart(2, "0"),
    String(now.getHours()).padStart(2, "0"),
    String(now.getMinutes()).padStart(2, "0"),
    String(now.getSeconds()).padStart(2, "0")
  ].join("");
  const devicePart = String(state.selectedLocalDeviceId || "LOCAL")
    .replace(/[^a-zA-Z0-9]/g, "")
    .slice(0, 8)
    .toUpperCase();
  return `STYS-PAY-${stamp}-${devicePart}`;
}

function ensureLocalPaymentSaleReference() {
  const input = $("local-device-payment-sale-ref");
  if (!input || input.value.trim()) return;
  input.value = generateLocalPaymentSaleReference();
}

function renderProvisioningTesisOptions() {
  const select = $("provisioning-tesis-id");
  if (!select) return;

  const tesisler = Array.isArray(state.agentSelf?.tesisler || state.agentSelf?.Tesisler)
    ? (state.agentSelf?.tesisler || state.agentSelf?.Tesisler)
    : [];

  const previous = select.value;
  select.innerHTML = tesisler.length
    ? tesisler.map((tesis) => `<option value="${escapeHtml(String(tesis.id ?? tesis.Id))}">${escapeHtml(tesis.ad || tesis.Ad || String(tesis.id ?? tesis.Id))}</option>`).join("")
    : '<option value="">Tesis yok</option>';

  const validPrevious = tesisler.some((tesis) => String(tesis.id ?? tesis.Id) === String(previous));
  if (validPrevious) {
    select.value = previous;
  } else if (tesisler.length > 0) {
    select.value = String(tesisler[0].id ?? tesisler[0].Id);
  } else {
    select.value = "";
  }

  state.selectedProvisioningTesisId = select.value ? Number(select.value) : null;
}

function renderProvisioningCandidate(candidate) {
  const pre = $("provisioning-preview-json");
  if (!pre) return;

  if (!candidate) {
    state.selectedProvisioningCandidate = null;
    pre.textContent = "Henüz önizleme oluşturulmadı.";
    const saveBtn = $("provisioning-save-btn");
    if (saveBtn) saveBtn.disabled = true;
    return;
  }

  state.selectedProvisioningCandidate = candidate;
  pre.textContent = JSON.stringify(candidate, null, 2);
  const saveBtn = $("provisioning-save-btn");
  if (saveBtn) saveBtn.disabled = false;
}

function collectLocalPaymentTestPayload() {
  const amount = Number($("local-device-payment-amount")?.value || 0);
  if (!Number.isFinite(amount) || amount <= 0) {
    throw new Error("Tutar sıfırdan büyük olmalıdır.");
  }

  return {
    amount,
    currencyCode: ($("local-device-payment-currency")?.value || "TRY").trim() || "TRY",
    saleReference: $("local-device-payment-sale-ref")?.value?.trim() || null,
    description: $("local-device-payment-desc")?.value?.trim() || null,
    installmentCount: Number($("local-device-payment-installment")?.value || 0),
    selectedTerminalId: $("local-device-payment-terminal")?.value || null,
    selectedSlots: ["rf", "icc", "magneticStripe", "qr", "manual"],
    cardReadTimeout: 60,
    allowDismissCardRead: true,
    pinEntryTimeout: 30,
    printReceipt: false,
    responseBeforePrintEnabled: false,
    customerReceiptPrintEnabled: true,
    merchantReceiptPrintEnabled: true,
    receiptImage: false,
    customerReceiptImageEnabled: false,
    merchantReceiptImageEnabled: false,
    receiptWidth: "58mm",
    headUnmaskLength: 0,
    tailUnmaskLength: 4,
    receiptJsonEnabled: false,
    customerReceiptJsonEnabled: false,
    merchantReceiptJsonEnabled: false,
    receiptTextEnabled: true,
    receiptTextWidth: "40",
    customerReceiptTextEnabled: true,
    customerReceiptTextWidth: "40",
    merchantReceiptTextEnabled: true,
    merchantReceiptTextWidth: "40"
  };
}

function renderPaymentTestResult(result) {
  const pre = $("local-device-payment-result");
  if (!pre) return;
  pre.textContent = JSON.stringify(result, null, 2);
}

async function loadLocalDeviceDetail(id) {
  if (!id) {
    state.selectedLocalDeviceId = null;
    renderLocalDeviceDetail(null);
    renderLocalDeviceTerminalRows([]);
    renderProvisioningCandidate(null);
    setStatus("provisioning-preview-status", "Önizleme bekleniyor.", "muted");
    return null;
  }

  const device = await getJson(`/api/local-devices/${encodeURIComponent(id)}`);
  state.selectedLocalDeviceId = device.id || id;
  renderLocalDeviceDetail(device);
  renderProvisioningCandidate(null);
  setStatus("provisioning-preview-status", "Önizleme bekleniyor.", "muted");
  await loadLocalDeviceTerminals(state.selectedLocalDeviceId).catch(() => {});
  ensureLocalPaymentSaleReference();
  return device;
}

async function selectLocalDevice(id) {
  state.selectedLocalDeviceId = id;
  return await loadLocalDeviceDetail(id);
}

async function loadAgentSelf() {
  const self = await getJson("/api/agent/me");
  state.agentSelf = self;
  renderProvisioningTesisOptions();
  const status = $("provisioning-agent-status");
  if (status) {
    status.textContent = self?.kurumAd || self?.KurumAd ? `Agent kapsamı hazır: ${self?.kurumAd || self?.KurumAd}` : "Agent kapsamı hazır.";
    status.className = "status ok";
  }
  return self;
}

async function loadLocalDeviceTerminals(id) {
  if (!id) {
    state.selectedLocalDeviceTerminals = [];
    renderLocalDeviceTerminalRows([]);
    renderProvisioningCandidate(null);
    setStatus("local-device-terminals-status", "Keşif bekleniyor.", "muted");
    return [];
  }

  const terminals = await getJson(`/api/local-devices/${encodeURIComponent(id)}/terminals`);
  renderLocalDeviceTerminalRows(terminals || []);
  setStatus("local-device-terminals-status", terminals?.length ? "Terminal listesi güncellendi." : "Keşfedilmiş terminal yok.", terminals?.length ? "ok" : "muted");
  return terminals || [];
}

async function checkSelectedLocalDeviceStysStatus() {
  if (!state.selectedLocalDeviceId) {
    setStatus("local-devices-status", "Önce bir cihaz seçin.", "warn");
    return null;
  }

  setStatus("local-devices-status", "STYS durumu kontrol ediliyor...", "muted");
  const result = await getJson(`/api/local-devices/${encodeURIComponent(state.selectedLocalDeviceId)}/stys-status`, {
    method: "POST"
  });

  const badge = localDeviceStysReconciliationBadge(result?.status ?? result?.Status);
  setText("local-device-detail-stys-status", badge.text);
  const stysStatusEl = $("local-device-detail-stys-status");
  if (stysStatusEl) stysStatusEl.className = `value small-value badge ${badge.kind}`;
  setStatus("local-device-detail-stys-message", result?.message || result?.Message || "-", badge.kind);
  setText("local-device-detail-last-result", result?.message || result?.Message || "STYS durumu kontrol edildi.");
  setStatus("local-devices-status", result?.message || result?.Message || "STYS durumu kontrol edildi.", badge.kind);
  await loadLocalDevices();
  await selectLocalDevice(state.selectedLocalDeviceId).catch(() => {});
  return result;
}

async function discoverLocalDeviceTerminals(id) {
  if (!id) {
    throw new Error("Önce bir cihaz seçin.");
  }

  setStatus("local-devices-status", "Terminaller keşfediliyor...", "muted");
  const terminals = await getJson(`/api/local-devices/${encodeURIComponent(id)}/terminals/discover`, {
    method: "POST"
  });
  setStatus("local-devices-status", "Terminal discovery tamamlandı.", "ok");
  setStatus("local-device-terminals-status", terminals?.length ? "Terminal discovery tamamlandı." : "Keşfedilmiş terminal yok.", terminals?.length ? "ok" : "muted");
  setText("local-device-last-action", `Terminal discovery: ${id}`);
  await loadLocalDevices();
  await loadLocalDeviceTerminals(id);
  await selectLocalDevice(id).catch(() => {});
  return terminals;
}

async function loadProvisioningCandidateForSelectedDevice() {
  if (!state.selectedLocalDeviceId) {
    throw new Error("Önce bir cihaz seçin.");
  }

  const tesisId = state.selectedProvisioningTesisId || Number($("provisioning-tesis-id")?.value || 0);
  if (!tesisId) {
    throw new Error("Tesis seçimi zorunludur.");
  }

  if (!state.agentSelf) {
    await loadAgentSelf();
  }

  setStatus("provisioning-preview-status", "Önizleme hazırlanıyor...", "muted");
  const candidate = await getJson(`/api/local-devices/${encodeURIComponent(state.selectedLocalDeviceId)}/provisioning-candidate?tesisId=${encodeURIComponent(String(tesisId))}`);
  renderProvisioningCandidate(candidate);
  setStatus("provisioning-preview-status", "Provisioning önizlemesi hazır.", "ok");
  return candidate;
}

async function submitLocalPaymentTest() {
  if (!state.selectedLocalDeviceId) {
    throw new Error("Önce bir cihaz seçin.");
  }

  const payload = collectLocalPaymentTestPayload();
  setStatus("local-device-payment-status", "Ödeme testi gönderiliyor...", "muted");
  const result = await getJson(`/api/local-devices/${encodeURIComponent(state.selectedLocalDeviceId)}/payment-test`, {
    method: "POST",
    body: JSON.stringify(payload)
  });

  renderPaymentTestResult(result);
  const badge = result?.success ? { text: "Ödeme testi tamamlandı.", kind: "ok" } : { text: result?.message || "Ödeme testi başarısız.", kind: "warn" };
  setStatus("local-device-payment-status", badge.text, badge.kind);
  setText("local-device-last-action", `Payment test: ${state.selectedLocalDeviceId}`);
  setText("local-device-last-result", result?.message || badge.text);
  return result;
}

async function registerSelectedLocalDevice() {
  if (!state.selectedProvisioningCandidate) {
    await loadProvisioningCandidateForSelectedDevice();
  }

  if (!state.selectedProvisioningCandidate) {
    throw new Error("Önce provisioning önizlemesi oluşturulmalıdır.");
  }

  setStatus("provisioning-preview-status", "STYS'e kayıt gönderiliyor...", "muted");
  const result = await getJson("/api/agent/pos-devices/register", {
    method: "POST",
    body: JSON.stringify(state.selectedProvisioningCandidate)
  });

  const message = result?.message || "✓ STYS'e kaydedildi";
  setStatus("provisioning-preview-status", `${message} Central PosCihazi ID: ${result.centralPosCihaziId ?? result.CentralPosCihaziId ?? "-"}`, "ok");
  setText("local-device-last-action", `Provisioned: ${state.selectedLocalDeviceId}`);
  setText("local-device-last-result", result.provisioningStatus || result.ProvisioningStatus || "Provisioned");
  await loadLocalDevices();
  await selectLocalDevice(state.selectedLocalDeviceId).catch(() => {});
  return result;
}

function renderLocalDeviceRows(devices) {
  const body = $("local-devices-table-body");
  if (!body) return;

  const items = Array.isArray(devices) ? devices : [];
  body.innerHTML = items.length
    ? items.map((device) => {
        const badge = localDeviceStatusBadge(device.status);
        const pairingBadge = localDevicePairingBadge(device.pairingStatus);
        const lastTest = formatDateTime(device.lastConnectionTestAt);
        const lastError = device.lastError ? escapeHtml(device.lastError) : "-";
        return `
          <tr>
            <td>
              <div class="table-title">${escapeHtml(device.displayName || "-")}</div>
              <div class="help mono">${escapeHtml(lastError)}</div>
            </td>
            <td>${escapeHtml(localDeviceTypeLabel(device.deviceType))}</td>
            <td>${escapeHtml(localDeviceProviderLabel(device.provider))}</td>
            <td class="mono">
              <div>${escapeHtml(formatDeviceAddress(device))}</div>
              <div class="help">Serial: ${escapeHtml(device.serialNumber || "yok")}</div>
            </td>
            <td><span class="badge ${pairingBadge.kind}">${escapeHtml(pairingBadge.text)}</span></td>
            <td><span class="badge ${badge.kind}">${escapeHtml(badge.text)}</span></td>
            <td>${escapeHtml(lastTest)}</td>
            <td>
              <div class="table-actions">
                <button type="button" class="secondary" data-local-device-action="details" data-local-device-id="${escapeHtml(device.id)}">Detay</button>
                <button type="button" class="secondary" data-local-device-action="edit" data-local-device-id="${escapeHtml(device.id)}">Düzenle</button>
                <button type="button" class="danger" data-local-device-action="delete" data-local-device-id="${escapeHtml(device.id)}">Sil</button>
              </div>
            </td>
          </tr>`;
      }).join("")
    : `<tr><td colspan="8" class="muted">Kayıtlı cihaz yok.</td></tr>`;

  body.querySelectorAll("[data-local-device-action]").forEach((button) => {
    button.addEventListener("click", () => {
      const id = button.getAttribute("data-local-device-id");
      const action = button.getAttribute("data-local-device-action");
      const device = items.find((item) => String(item.id) === String(id));
      if (!device && action !== "delete") return;

      if (action === "details") {
        selectLocalDevice(id).catch((error) => {
          setStatus("local-devices-status", mapLocalDeviceError(error), "error");
        });
      } else if (action === "edit") {
        fillLocalDeviceForm(device);
      } else if (action === "delete") {
        deleteLocalDevice(id).catch((error) => {
          setStatus("local-devices-status", mapLocalDeviceError(error), "error");
        });
      }
    });
  });
}

function fillLocalDeviceForm(device) {
  if (!device) return;
  if ($("local-device-id")) $("local-device-id").value = device.id || "";
  if ($("local-device-display-name")) $("local-device-display-name").value = device.displayName || "";
  if ($("local-device-type")) $("local-device-type").value = String(device.deviceType ?? 0);
  if ($("local-device-provider")) $("local-device-provider").value = String(device.provider ?? 0);
  if ($("local-device-host")) $("local-device-host").value = device.host || "";
  if ($("local-device-protocol")) $("local-device-protocol").value = String(device.protocol ?? 0);
  if ($("local-device-http-port")) $("local-device-http-port").value = Number(device.httpPort || 4567);
  if ($("local-device-https-port")) $("local-device-https-port").value = Number(device.httpsPort || 4568);
  if ($("local-device-serial-number")) $("local-device-serial-number").value = device.serialNumber || "";
  setLocalDeviceFormMode(device);
  setStatus("local-device-form-status", "Cihaz düzenleme modunda.", "warn");
}

async function loadLocalDevices() {
  const data = await getJson("/api/local-devices");
  renderLocalDeviceRows(data || []);
  setStatus("local-devices-status", "Cihaz listesi güncellendi.", "ok");

  if (state.selectedLocalDeviceId) {
    const selected = (Array.isArray(data) ? data : []).find((item) => String(item.id) === String(state.selectedLocalDeviceId));
    if (selected) {
      renderLocalDeviceDetail(selected);
      await loadLocalDeviceTerminals(state.selectedLocalDeviceId).catch(() => {});
    } else {
      state.selectedLocalDeviceId = null;
      renderLocalDeviceDetail(null);
      renderLocalDeviceTerminalRows([]);
      renderProvisioningCandidate(null);
      setStatus("provisioning-preview-status", "Önizleme bekleniyor.", "muted");
    }
  }

  return data;
}

async function saveLocalDevice(event) {
  event.preventDefault();

  const payload = collectLocalDeviceFormPayload();
  const isEdit = !!payload.id;
  const url = isEdit ? `/api/local-devices/${encodeURIComponent(payload.id)}` : "/api/local-devices";
  const method = isEdit ? "PUT" : "POST";

  setStatus("local-device-form-status", "Kaydediliyor...", "muted");
  const result = await getJson(url, {
    method,
    body: JSON.stringify(payload)
  });

  setStatus("local-device-form-status", "Cihaz kaydedildi.", "ok");
  setText("local-device-last-action", `${result.displayName || payload.displayName} kaydedildi`);
  setText("local-device-last-result", result.lastError || "Kayıt tamamlandı");
  resetLocalDeviceForm();
  await loadLocalDevices();
  await selectLocalDevice(result.id || result.Id || payload.id || null).catch(() => {});
}

async function deleteLocalDevice(id) {
  const ok = window.confirm("Bu yerel cihaz kaydı silinsin mi?");
  if (!ok) return;

  await getJson(`/api/local-devices/${encodeURIComponent(id)}`, {
    method: "DELETE"
  });

  setStatus("local-devices-status", "Cihaz silindi.", "ok");
  setText("local-device-last-action", `Silindi: ${id}`);
  setText("local-device-last-result", "-");
  if (state.localDeviceEditingId && String(state.localDeviceEditingId) === String(id)) {
    resetLocalDeviceForm();
  }
  if (state.selectedLocalDeviceId && String(state.selectedLocalDeviceId) === String(id)) {
    state.selectedLocalDeviceId = null;
    renderLocalDeviceDetail(null);
  }
  await loadLocalDevices();
}

async function loadSelectedLocalDeviceInfo() {
  if (!state.selectedLocalDeviceId) {
    setStatus("local-devices-status", "Önce bir cihaz seçin.", "warn");
    return null;
  }

  const device = await getJson(`/api/local-devices/${encodeURIComponent(state.selectedLocalDeviceId)}/device-info`, {
    method: "POST"
  });

  setText("local-device-detail-last-result", "Cihaz bilgisi alındı.");
  setStatus("local-devices-status", "Cihaz bilgisi alındı.", "ok");
  await loadLocalDevices();
  await selectLocalDevice(device.id || state.selectedLocalDeviceId).catch(() => {});
  await loadLocalDeviceTerminals(state.selectedLocalDeviceId).catch(() => {});
  return device;
}

async function pairSelectedLocalDevice() {
  if (!state.selectedLocalDeviceId) {
    setStatus("local-devices-status", "Önce bir cihaz seçin.", "warn");
    return null;
  }

  const current = await getJson(`/api/local-devices/${encodeURIComponent(state.selectedLocalDeviceId)}`);
  const forceRePair = Number(current?.pairingStatus) === 1;
  if (forceRePair) {
    const ok = window.confirm("Bu cihaz zaten eşleştirilmiş. Yeniden pairing mevcut pairing bilgisini değiştirebilir.");
    if (!ok) {
      return null;
    }
  }

  const device = await getJson(`/api/local-devices/${encodeURIComponent(state.selectedLocalDeviceId)}/pairing`, {
    method: "POST",
    body: JSON.stringify({ forceRePair })
  });

  setText("local-device-detail-last-result", forceRePair ? "Yeniden pairing tamamlandı." : "Pairing tamamlandı.");
  setStatus("local-devices-status", "Pairing tamamlandı.", "ok");
  await loadLocalDevices();
  await selectLocalDevice(device.id || state.selectedLocalDeviceId).catch(() => {});
  await loadLocalDeviceTerminals(state.selectedLocalDeviceId).catch(() => {});
  return device;
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
  setText("diag-stys-base-url", data.stysBaseUrl || "-");
  setText("diag-credential-present", data.credentialPresent ? "Evet" : "Hayır");
  setText("diag-auth-ready", data.authenticationReady ? "Evet" : "Hayır");
  setText("diag-reenrollment", data.requiresReEnrollment ? (data.requiresReEnrollmentReason || "Evet") : "Hayır");
  setText("diag-last-connection", formatDateTime(data.lastSuccessfulStysConnectionAt));
  setText("diag-last-connection-error", data.lastStysConnectionError || "-");
  setText("diag-last-heartbeat", formatDateTime(data.lastHeartbeatSuccessAt));
  setText("diag-last-heartbeat-error", data.lastHeartbeatError || "-");
  setText("diag-last-command", formatDateTime(data.lastCommandPollSuccessAt));
  setText("diag-last-command-error", data.lastCommandPollError || "-");
  setText("diag-last-reset", formatDateTime(data.lastResetAt));

  renderLogTable(data.recentLogs || []);
  return data;
}

function bindLocalDevicesPage() {
  $("local-device-form")?.addEventListener("submit", saveLocalDevice);
  $("local-device-new-btn")?.addEventListener("click", () => resetLocalDeviceForm());
  $("local-device-refresh-btn")?.addEventListener("click", () => {
    loadLocalDevices().catch((error) => {
      setStatus("local-devices-status", error.message, "error");
    });
  });
  $("local-device-terminals-refresh-btn")?.addEventListener("click", () => {
    loadLocalDeviceTerminals(state.selectedLocalDeviceId).catch((error) => {
      setStatus("local-device-terminals-status", mapLocalDeviceError(error), "error");
    });
  });
  $("local-device-cancel-btn")?.addEventListener("click", (event) => {
    event.preventDefault();
    resetLocalDeviceForm();
  });
  $("local-device-protocol")?.addEventListener("change", () => syncLocalDevicePortDefaults());
  $("local-device-payment-terminal")?.addEventListener("change", () => {
    state.selectedPaymentTerminalId = $("local-device-payment-terminal")?.value || null;
  });
  $("local-device-payment-form")?.addEventListener("submit", (event) => {
    event.preventDefault();
    submitLocalPaymentTest().catch((error) => {
      setStatus("local-device-payment-status", mapLocalDeviceError(error), "error");
      setText("local-device-payment-result", error?.message || String(error));
    });
  });
  $("local-device-detail-info-btn")?.addEventListener("click", () => {
    loadSelectedLocalDeviceInfo().catch((error) => {
      setStatus("local-devices-status", mapLocalDeviceError(error), "error");
    });
  });
  $("local-device-detail-pair-btn")?.addEventListener("click", () => {
    pairSelectedLocalDevice().catch((error) => {
      setStatus("local-devices-status", mapLocalDeviceError(error), "error");
      setText("local-device-detail-last-result", localDeviceOperationMessage(error));
    });
  });
  $("local-device-detail-discover-btn")?.addEventListener("click", () => {
    discoverLocalDeviceTerminals(state.selectedLocalDeviceId).catch((error) => {
      setStatus("local-devices-status", mapLocalDeviceError(error), "error");
    });
  });
  $("local-device-detail-stys-btn")?.addEventListener("click", () => {
    checkSelectedLocalDeviceStysStatus().catch((error) => {
      setStatus("local-devices-status", mapLocalDeviceError(error), "error");
      setText("local-device-detail-last-result", localDeviceOperationMessage(error));
    });
  });
  $("provisioning-tesis-id")?.addEventListener("change", () => {
    state.selectedProvisioningTesisId = Number($("provisioning-tesis-id")?.value || 0) || null;
  });
  $("provisioning-preview-btn")?.addEventListener("click", () => {
    loadProvisioningCandidateForSelectedDevice().catch((error) => {
      setStatus("provisioning-preview-status", mapLocalDeviceError(error), "error");
    });
  });
  $("provisioning-save-btn")?.addEventListener("click", () => {
    registerSelectedLocalDevice().catch((error) => {
      setStatus("provisioning-preview-status", error.status === 409 ? error.message : mapLocalDeviceError(error), "error");
    });
  });
  resetLocalDeviceForm();
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

  if ($("local-devices-root")) {
    state.localDevicesTimer = window.setInterval(() => {
      loadLocalDevices().catch(() => {});
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

  if (state.localDevicesTimer) {
    clearInterval(state.localDevicesTimer);
    state.localDevicesTimer = null;
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

  if ($("local-devices-root")) {
    tasks.push(loadAgentSelf().catch((error) => {
      setStatus("provisioning-agent-status", error.message, "error");
    }));
    tasks.push(loadLocalDevices().catch((error) => {
      setStatus("local-devices-status", error.message, "error");
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

  if ($("local-devices-root")) {
    await loadAgentSelf().catch((error) => {
      setStatus("provisioning-agent-status", error.message, "error");
    });
    bindLocalDevicesPage();
    await loadLocalDevices().catch((error) => {
      setStatus("local-devices-status", error.message, "error");
    });
  }

  startRefreshers();
}

window.addEventListener("DOMContentLoaded", bootstrap);
