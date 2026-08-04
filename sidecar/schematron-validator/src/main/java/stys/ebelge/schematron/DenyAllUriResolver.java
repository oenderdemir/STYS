package stys.ebelge.schematron;

import javax.xml.transform.Source;
import javax.xml.transform.TransformerException;
import javax.xml.transform.URIResolver;

/**
 * Üretilen belge (instance XML) doğrulaması sırasında document()/harici kaynak erişimini
 * TAMAMEN reddeder. Derlenmiş validator'ın çalışma zamanında herhangi bir dış kaynağa
 * ihtiyacı YOKTUR - bu resolver yalnız savunma derinliği (defense in depth) amaçlıdır.
 */
final class DenyAllUriResolver implements URIResolver {
    @Override
    public Source resolve(String href, String base) throws TransformerException {
        throw new TransformerException("Doğrulama sırasında harici kaynak erişimi kapalıdır.");
    }
}
