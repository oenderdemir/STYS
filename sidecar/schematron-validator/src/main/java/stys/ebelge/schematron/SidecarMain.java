package stys.ebelge.schematron;

import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;
import com.sun.net.httpserver.HttpServer;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.nio.file.Path;
import java.time.Instant;
import java.util.List;
import java.util.Locale;
import java.util.concurrent.*;
import java.util.logging.Level;
import java.util.logging.Logger;

/**
 * G\u0130B UBL-TR Schematron do\u011frulama sidecar'\u0131. Yaln\u0131z bilinen, whitelist edilmi\u015f rule-set
 * kimli\u011fini \u00e7al\u0131\u015ft\u0131r\u0131r; runtime stylesheet y\u00fckleme, kullan\u0131c\u0131 taraf\u0131ndan sa\u011flanan
 * path/URL veya keyfi XPath KABUL ETMEZ (bkz. g\u00f6rev md.1). Yaln\u0131z internal network \u00fczerinden
 * eri\u015filebilir olmal\u0131d\u0131r - public port olarak YAY\u0130NLANMAMALIDIR (bkz. docker-compose.yml,
 * expose kullan\u0131l\u0131r, ports kullan\u0131lmaz).
 */
public final class SidecarMain {

    private static final Logger LOG = Logger.getLogger("schematron-sidecar");
    private static final int MAX_BODY_BYTES = 5_000_000;
    private static final long REQUEST_TIMEOUT_MS = 10_000;

    /**
     * Başlangıç öz-testi (self-test) için kullanılan, GERÇEK kişisel veri İÇERMEYEN, sabit
     * (deterministik) e-Arşiv örnek XML'i. Ready endpoint yalnız bu örnek sıfır ihlal
     * ÜRETİRSE 200 döner (bkz. görev md.6). Tarih kasıtlı olarak sabit ve geçmişte - ileride
     * "günün tarihinden ileri olamaz" kuralını asla ihlal etmeyecek şekilde seçildi.
     */
    private static final String SELF_TEST_EARSIV_XML = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                 xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
                 xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
          <cbc:UBLVersionID>2.1</cbc:UBLVersionID>
          <cbc:CustomizationID>TR1.2</cbc:CustomizationID>
          <cbc:ProfileID>EARSIVFATURA</cbc:ProfileID>
          <cbc:ID>SLF2020000000001</cbc:ID>
          <cbc:CopyIndicator>false</cbc:CopyIndicator>
          <cbc:UUID>00000000-0000-4000-8000-000000000000</cbc:UUID>
          <cbc:IssueDate>2020-06-15</cbc:IssueDate>
          <cbc:IssueTime>10:00:00</cbc:IssueTime>
          <cbc:InvoiceTypeCode>SATIS</cbc:InvoiceTypeCode>
          <cbc:DocumentCurrencyCode>TRY</cbc:DocumentCurrencyCode>
        </Invoice>
        """;

    private volatile SchematronPipeline pipeline;
    private volatile boolean ready = false;
    private volatile String startupError = null;
    private final String baseRuleSetId;
    private final ExecutorService transformExecutor = Executors.newCachedThreadPool(SidecarMain::daemonThread);

    private SidecarMain(String baseRuleSetId) {
        this.baseRuleSetId = baseRuleSetId;
    }

    public static void main(String[] args) throws Exception {
        Path rootDir = Path.of(System.getProperty("schematron.rootDir", "."));
        int port = Integer.parseInt(System.getProperty("schematron.port", "8081"));

        ArtifactManifest manifest = ArtifactManifest.load(rootDir);
        SidecarMain app = new SidecarMain(manifest.ruleSetId);
        app.initializeAsync(manifest);

        HttpServer server = HttpServer.create(new InetSocketAddress("0.0.0.0", port), 0);
        server.createContext("/health/live", app::handleLive);
        server.createContext("/health/ready", app::handleReady);
        server.createContext("/internal/schematron/validate", app::handleValidate);
        server.setExecutor(Executors.newFixedThreadPool(8, SidecarMain::daemonThread));
        server.start();

        LOG.info(() -> "schematron-sidecar started on port " + port);
    }

    private void initializeAsync(ArtifactManifest manifest) {
        Thread t = new Thread(() -> {
            try {
                manifest.verifyOrThrow();
                SchematronPipeline compiled = SchematronPipeline.compile(manifest);

                // Öz-test: e-Arşiv profili GERÇEKTEN sıfır ihlal üretmiyorsa sidecar ready OLMAZ
                // (bkz. görev md.6 - "küçük bir embedded self-test e-Arşiv örneği beklenen
                // sonucu vermiş" olmalı). Kişisel veri İÇERMEZ.
                List<Violation> selfTestViolations = compiled.validate(
                        SELF_TEST_EARSIV_XML.getBytes(StandardCharsets.UTF_8), DocumentProfile.EARSIV);
                if (!selfTestViolations.isEmpty()) {
                    throw new IllegalStateException(
                            "Öz-test (self-test) başarısız: e-Arşiv örneği " + selfTestViolations.size() + " ihlal üretti.");
                }

                this.pipeline = compiled;
                this.ready = true;
                LOG.info("schematron pipeline compiled, self-test passed, baseRuleSetId=" + baseRuleSetId + ", sidecar ready");
            } catch (Exception e) {
                this.startupError = e.getClass().getSimpleName();
                LOG.log(Level.SEVERE, "sidecar startup failed: " + e.getClass().getSimpleName());
            }
        }, "schematron-init");
        t.setDaemon(true);
        t.start();
    }

    private void handleLive(HttpExchange exchange) throws IOException {
        writeJson(exchange, 200, "{\"status\":\"live\"}");
    }

    private void handleReady(HttpExchange exchange) throws IOException {
        if (ready) {
            writeJson(exchange, 200, "{\"status\":\"ready\"}");
        } else {
            writeJson(exchange, 503, "{\"status\":\"not-ready\"}");
        }
    }

    private void handleValidate(HttpExchange exchange) throws IOException {
        String correlationId = firstHeader(exchange, "X-Correlation-Id");
        try {
            if (!"POST".equalsIgnoreCase(exchange.getRequestMethod())) {
                writeJson(exchange, 405, "{\"error\":\"METHOD_NOT_ALLOWED\"}");
                return;
            }

            if (!ready) {
                writeJson(exchange, 503, "{\"error\":\"SERVICE_NOT_READY\"}");
                return;
            }

            String ruleSetId = firstHeader(exchange, "X-RuleSet-Id");
            DocumentProfile profile = DocumentProfile.fromRuleSetId(ruleSetId, baseRuleSetId);
            // İlk dalgada yalnız e-Arşiv aktif (bkz. görev md.5) - EFATURA whitelist'te
            // tanımlı olsa da bu sidecar sürümünde bilinçli olarak REDDEDİLİR.
            if (profile != DocumentProfile.EARSIV)
            {
                writeJson(exchange, 400, "{\"error\":\"UNKNOWN_RULESET\"}");
                return;
            }

            byte[] xmlBytes = readBounded(exchange.getRequestBody(), MAX_BODY_BYTES);
            if (xmlBytes == null) {
                writeJson(exchange, 413, "{\"error\":\"PAYLOAD_TOO_LARGE\"}");
                return;
            }

            Instant started = Instant.now();
            Future<List<Violation>> future = transformExecutor.submit(() -> pipeline.validate(xmlBytes, profile));

            List<Violation> violations;
            try {
                violations = future.get(REQUEST_TIMEOUT_MS, TimeUnit.MILLISECONDS);
            } catch (TimeoutException e) {
                future.cancel(true);
                LOG.warning(() -> "validate timeout correlationId=" + safeCid(correlationId));
                writeJson(exchange, 504, "{\"error\":\"VALIDATION_TIMEOUT\"}");
                return;
            } catch (ExecutionException e) {
                LOG.log(Level.WARNING, "validate failed correlationId=" + safeCid(correlationId)
                        + " cause=" + e.getCause().getClass().getSimpleName());
                writeJson(exchange, 500, "{\"error\":\"VALIDATION_INTERNAL_ERROR\"}");
                return;
            }

            long elapsedMs = java.time.Duration.between(started, Instant.now()).toMillis();
            // Bilinçli olarak: XML içeriği, ihlal mesaj METNİ (VKN/unvan/adres taşıyabilir) veya
            // correlationId dışındaki kişisel veri ASLA loglanmaz - yalnız yapısal metadata.
            LOG.info(() -> "validate correlationId=" + safeCid(correlationId)
                    + " violationCount=" + violations.size() + " elapsedMs=" + elapsedMs);

            writeJson(exchange, 200, toResponseJson(violations));
        } catch (Exception e) {
            LOG.log(Level.SEVERE, "unexpected error correlationId=" + safeCid(correlationId)
                    + " type=" + e.getClass().getSimpleName());
            writeJson(exchange, 500, "{\"error\":\"INTERNAL_ERROR\"}");
        }
    }

    private static byte[] readBounded(InputStream in, int maxBytes) throws IOException {
        java.io.ByteArrayOutputStream buffer = new java.io.ByteArrayOutputStream();
        byte[] chunk = new byte[8192];
        int total = 0;
        int n;
        while ((n = in.read(chunk)) != -1) {
            total += n;
            if (total > maxBytes) {
                return null;
            }
            buffer.write(chunk, 0, n);
        }
        return buffer.toByteArray();
    }

    private static String toResponseJson(List<Violation> violations) {
        StringBuilder sb = new StringBuilder();
        sb.append("{\"valid\":").append(violations.isEmpty()).append(",\"violations\":[");
        for (int i = 0; i < violations.size(); i++) {
            if (i > 0) sb.append(",");
            Violation v = violations.get(i);
            sb.append("{\"ruleId\":\"").append(jsonEscape(v.ruleId())).append("\",")
              .append("\"location\":\"").append(jsonEscape(v.location())).append("\",")
              .append("\"message\":\"").append(jsonEscape(v.message())).append("\",")
              .append("\"severity\":\"").append(jsonEscape(v.severity())).append("\"}");
        }
        sb.append("]}");
        return sb.toString();
    }

    private static String jsonEscape(String s) {
        return s.replace("\\", "\\\\").replace("\"", "\\\"")
                .replace("\n", "\\n").replace("\r", "\\r").replace("\t", "\\t");
    }

    private static void writeJson(HttpExchange exchange, int status, String json) throws IOException {
        byte[] bytes = json.getBytes(StandardCharsets.UTF_8);
        exchange.getResponseHeaders().set("Content-Type", "application/json; charset=utf-8");
        exchange.sendResponseHeaders(status, bytes.length);
        try (OutputStream os = exchange.getResponseBody()) {
            os.write(bytes);
        }
    }

    private static String firstHeader(HttpExchange exchange, String name) {
        List<String> values = exchange.getRequestHeaders().get(name);
        return values == null || values.isEmpty() ? null : values.get(0);
    }

    private static String safeCid(String correlationId) {
        return correlationId == null ? "-" : correlationId.replaceAll("[^a-zA-Z0-9-]", "").toLowerCase(Locale.ROOT);
    }

    private static Thread daemonThread(Runnable r) {
        Thread t = new Thread(r);
        t.setDaemon(true);
        return t;
    }
}
