package stys.ebelge.schematron;

import javax.xml.transform.Source;
import javax.xml.transform.TransformerException;
import javax.xml.transform.URIResolver;
import javax.xml.transform.stream.StreamSource;
import java.io.File;
import java.net.URI;
import java.nio.file.Path;

/**
 * Yalnız sabit kural seti kök dizini ALTINDAKİ dosyalara izin verir - path traversal ve
 * http(s)/ftp gibi uzak kaynaklara izin VERMEZ. Yalnız derleme aşamalarında (sch:include
 * çözümü) kullanılır; üretilen belge (instance XML) doğrulaması {@link DenyAllUriResolver}
 * kullanır (bkz. md.1/md.3 - document() ile keyfi ağ/dosya erişimi kapalı).
 */
final class SandboxUriResolver implements URIResolver {

    private final Path rootDirReal;

    SandboxUriResolver(Path rootDir) {
        this.rootDirReal = rootDir.toAbsolutePath().normalize();
    }

    @Override
    public Source resolve(String href, String base) throws TransformerException {
        try {
            URI baseUri = base == null || base.isEmpty() ? null : new URI(base);
            URI resolved = baseUri == null ? new URI(href) : baseUri.resolve(href);

            if (!"file".equalsIgnoreCase(resolved.getScheme())) {
                throw new TransformerException("Yerel olmayan (uzak) kaynak referansına izin verilmiyor: " + safeDescribe(resolved));
            }

            Path resolvedPath = Path.of(new File(resolved).toURI()).toAbsolutePath().normalize();
            if (!resolvedPath.startsWith(rootDirReal)) {
                throw new TransformerException("Kural seti kök dizini dışına referansa izin verilmiyor");
            }

            return new StreamSource(resolvedPath.toFile());
        } catch (Exception e) {
            if (e instanceof TransformerException te) {
                throw te;
            }
            throw new TransformerException("Kaynak çözümlenemedi", e);
        }
    }

    private static String safeDescribe(URI uri) {
        return uri.getScheme() == null ? "(şema yok)" : uri.getScheme() + "://...";
    }
}
