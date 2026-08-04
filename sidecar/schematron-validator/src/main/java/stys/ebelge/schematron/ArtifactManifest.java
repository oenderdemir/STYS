package stys.ebelge.schematron;

import java.io.IOException;
import java.io.UncheckedIOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.ArrayList;
import java.util.HexFormat;
import java.util.List;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * Sabit, yalnız bu sidecar'a ait manifest.json'ı okur ve HER dosyanın SHA-256'sını doğrular.
 * Genel amaçlı bir JSON ayrıştırıcı DEĞİLDİR - yalnız bizim ürettiğimiz, sabit şemalı
 * manifest.json'ı okur (dış/kullanıcı girdisi değil, imaja gömülü, salt-okunur bir yapı taşır).
 */
final class ArtifactManifest {

    record FileEntry(String path, String sha256) {}

    private static final Pattern RULE_SET_ID = Pattern.compile("\"ruleSetId\"\\s*:\\s*\"([^\"]+)\"");
    private static final Pattern SCHEMATRON_ENTRY = Pattern.compile("\"schematronEntry\"\\s*:\\s*\"([^\"]+)\"");
    private static final Pattern FILE_ENTRY = Pattern.compile(
            "\\{\\s*\"path\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"sha256\"\\s*:\\s*\"([^\"]+)\"\\s*\\}");

    final String ruleSetId;
    final String schematronEntry;
    final List<FileEntry> files;
    private final Path rootDir;

    private ArtifactManifest(String ruleSetId, String schematronEntry, List<FileEntry> files, Path rootDir) {
        this.ruleSetId = ruleSetId;
        this.schematronEntry = schematronEntry;
        this.files = files;
        this.rootDir = rootDir;
    }

    static ArtifactManifest load(Path rootDir) {
        Path manifestPath = rootDir.resolve("manifest.json");
        String json;
        try {
            json = Files.readString(manifestPath, StandardCharsets.UTF_8);
        } catch (IOException e) {
            throw new UncheckedIOException("manifest.json okunamadı", e);
        }

        Matcher ruleSetMatcher = RULE_SET_ID.matcher(json);
        if (!ruleSetMatcher.find()) {
            throw new IllegalStateException("manifest.json içinde ruleSetId bulunamadı");
        }

        Matcher entryMatcher = SCHEMATRON_ENTRY.matcher(json);
        if (!entryMatcher.find()) {
            throw new IllegalStateException("manifest.json içinde schematronEntry bulunamadı");
        }

        List<FileEntry> fileEntries = new ArrayList<>();
        Matcher fileMatcher = FILE_ENTRY.matcher(json);
        while (fileMatcher.find()) {
            fileEntries.add(new FileEntry(fileMatcher.group(1), fileMatcher.group(2)));
        }

        if (fileEntries.isEmpty()) {
            throw new IllegalStateException("manifest.json içinde dosya listesi boş");
        }

        return new ArtifactManifest(ruleSetMatcher.group(1), entryMatcher.group(1), List.copyOf(fileEntries), rootDir);
    }

    /** Her dosyanın SHA-256'sını yeniden hesaplayıp manifestteki kayıtlı değerle karşılaştırır. Tek uyuşmazlık TÜMÜNÜ reddeder. */
    void verifyOrThrow() {
        for (FileEntry entry : files) {
            Path filePath = rootDir.resolve(entry.path());
            if (!Files.exists(filePath)) {
                throw new IllegalStateException("Sabit artefakt dosyası eksik: " + entry.path());
            }

            String actual = sha256Hex(filePath);
            if (!actual.equalsIgnoreCase(entry.sha256())) {
                throw new IllegalStateException("Sabit artefakt dosyası SHA-256 uyuşmuyor: " + entry.path());
            }
        }
    }

    private static String sha256Hex(Path path) {
        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            byte[] bytes = Files.readAllBytes(path);
            return HexFormat.of().formatHex(digest.digest(bytes));
        } catch (NoSuchAlgorithmException e) {
            throw new IllegalStateException("SHA-256 algoritması bulunamadı", e);
        } catch (IOException e) {
            throw new UncheckedIOException(e);
        }
    }

    Path resolve(String relativePath) {
        return rootDir.resolve(relativePath);
    }
}
