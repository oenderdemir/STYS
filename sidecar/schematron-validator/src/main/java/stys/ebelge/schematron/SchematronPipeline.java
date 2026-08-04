package stys.ebelge.schematron;

import net.sf.saxon.s9api.*;
import org.xml.sax.InputSource;
import org.xml.sax.XMLReader;

import javax.xml.XMLConstants;
import javax.xml.parsers.SAXParserFactory;
import javax.xml.transform.stream.StreamSource;
import javax.xml.transform.sax.SAXSource;
import java.io.ByteArrayInputStream;
import java.io.StringReader;
import java.io.StringWriter;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

/**
 * ISO Schematron skeleton'ın 3 aşamalı derleme hattını (sch:include çöz -> sch:extends genişlet
 * -> SVRL üretici XSLT'ye derle) Java Saxon-HE 13.0 (s9api) ile GERÇEKTEN çalıştırır ve nihai
 * derlenmiş {@link XsltExecutable}'ı BİR KEZ üretip saklar (bkz. görev md.4 - "her request'te
 * yeniden compile edilmemeli"). {@link XsltExecutable} Saxon dokümantasyonuna göre immutable ve
 * thread-safe'tir; her istek kendi {@link Xslt30Transformer}'ını ondan türetir (paralel
 * kullanım güvenlidir, pool gerekmez).
 */
final class SchematronPipeline {

    private static final String SVRL_NS = "http://purl.oclc.org/dsdl/svrl";
    private static final int MAX_VIOLATIONS = 200;

    private final Processor processor;
    private final XsltExecutable compiledValidator;
    private final XPathCompiler xpathCompiler;

    private SchematronPipeline(Processor processor, XsltExecutable compiledValidator, XPathCompiler xpathCompiler) {
        this.processor = processor;
        this.compiledValidator = compiledValidator;
        this.xpathCompiler = xpathCompiler;
    }

    /** Başlangıçta BİR KEZ çağrılır. Herhangi bir aşama başarısız olursa fırlatır (sidecar ready olmaz). */
    static SchematronPipeline compile(ArtifactManifest manifest) throws SaxonApiException {
        Processor processor = new Processor(false);
        SandboxUriResolver sandboxResolver = new SandboxUriResolver(manifest.resolve("."));

        XsltCompiler compiler = processor.newXsltCompiler();
        compiler.setURIResolver(sandboxResolver);

        XsltExecutable includeStage = compiler.compile(new StreamSource(manifest.resolve("skeleton/iso_dsdl_include.xsl").toFile()));
        XsltExecutable abstractExpandStage = compiler.compile(new StreamSource(manifest.resolve("skeleton/iso_abstract_expand.xsl").toFile()));
        XsltExecutable svrlStage = compiler.compile(new StreamSource(manifest.resolve("skeleton/iso_svrl_for_xslt1.xsl").toFile()));

        XdmDestination stage1 = new XdmDestination();
        Xslt30Transformer t1 = includeStage.load30();
        t1.setURIResolver(sandboxResolver);
        t1.transform(new StreamSource(manifest.resolve(manifest.schematronEntry).toFile()), stage1);

        XdmDestination stage2 = new XdmDestination();
        Xslt30Transformer t2 = abstractExpandStage.load30();
        t2.setURIResolver(sandboxResolver);
        t2.transform(stage1.getXdmNode().asSource(), stage2);

        XdmDestination stage3 = new XdmDestination();
        Xslt30Transformer t3 = svrlStage.load30();
        t3.setURIResolver(sandboxResolver);
        t3.transform(stage2.getXdmNode().asSource(), stage3);

        // Derlenen validator XSLT, GİB kaynak dosyasının hiç bildirmediği "xs:" (XML Schema)
        // ad alanı önekini kullanıyor (xs:date(...) tip dökümleri). Bu, YALNIZ bizim ürettiğimiz
        // ARA ARTEFAKTA (GİB kaynak dosyasına DEĞİL) standart xmlns:xs bildirimi eklenerek
        // çözülür - semantik değişiklik yoktur, yalnız eksik ad alanı bağlamı tamamlanır (bkz.
        // poc/schematron-xpath2-poc/SONUC.md, "yan bulgu").
        String validatorXsltText = serialize(processor, stage3.getXdmNode());
        validatorXsltText = validatorXsltText.replaceFirst(
                "<xsl:stylesheet ",
                "<xsl:stylesheet xmlns:xs=\"http://www.w3.org/2001/XMLSchema\" ");

        XsltCompiler finalCompiler = processor.newXsltCompiler();
        finalCompiler.setURIResolver(new DenyAllUriResolver());
        XsltExecutable compiledValidator = finalCompiler.compile(new StreamSource(new StringReader(validatorXsltText)));

        XPathCompiler xpathCompiler = processor.newXPathCompiler();
        xpathCompiler.declareNamespace("svrl", SVRL_NS);

        return new SchematronPipeline(processor, compiledValidator, xpathCompiler);
    }

    /**
     * Üretilen belgeyi (instance XML) derlenmiş validator'a karşı doğrular. Her çağrı KENDİ
     * {@link Xslt30Transformer}'ını türetir (paylaşılan mutable state yoktur - paralel çağrılar
     * birbirini etkilemez). DTD/harici entity TAMAMEN kapalıdır (XXE korumalı ayrıştırma).
     */
    List<Violation> validate(byte[] xmlBytes) throws Exception {
        XMLReader secureReader = createSecureXmlReader();
        InputSource inputSource = new InputSource(new ByteArrayInputStream(xmlBytes));
        SAXSource saxSource = new SAXSource(secureReader, inputSource);

        Xslt30Transformer transformer = compiledValidator.load30();
        transformer.setURIResolver(new DenyAllUriResolver());

        XdmDestination result = new XdmDestination();
        transformer.transform(saxSource, result);

        XPathSelector selector = xpathCompiler.compile("//svrl:failed-assert").load();
        selector.setContextItem(result.getXdmNode());

        List<Violation> violations = new ArrayList<>();
        for (XdmItem item : selector) {
            XdmNode node = (XdmNode) item;
            String location = attr(node, "location");
            String test = attr(node, "test");
            String text = childText(node, "text");
            violations.add(new Violation(stableRuleId(test), location, text, "error"));
            if (violations.size() >= MAX_VIOLATIONS) {
                break;
            }
        }

        return violations;
    }

    private static XMLReader createSecureXmlReader() throws Exception {
        SAXParserFactory factory = SAXParserFactory.newInstance();
        factory.setFeature(XMLConstants.FEATURE_SECURE_PROCESSING, true);
        factory.setFeature("http://apache.org/xml/features/disallow-doctype-decl", true);
        factory.setFeature("http://xml.org/sax/features/external-general-entities", false);
        factory.setFeature("http://xml.org/sax/features/external-parameter-entities", false);
        factory.setNamespaceAware(true);
        XMLReader reader = factory.newSAXParser().getXMLReader();
        reader.setEntityResolver((publicId, systemId) -> {
            throw new org.xml.sax.SAXException("Harici entity çözümlemesi kapalıdır.");
        });
        return reader;
    }

    private static String attr(XdmNode node, String name) {
        String value = node.getAttributeValue(new QName(name));
        return value == null ? "" : value;
    }

    private static String childText(XdmNode node, String localName) {
        for (XdmNode child : node.children(localName)) {
            return child.getStringValue().trim();
        }
        return "";
    }

    /** svrl:failed-assert öğeleri ayrı bir rule id taşımaz - test ifadesinin kararlı bir özetinden türetilir. */
    private static String stableRuleId(String testExpression) {
        java.util.zip.CRC32 crc = new java.util.zip.CRC32();
        crc.update(testExpression.getBytes(StandardCharsets.UTF_8));
        return "rule-" + Long.toHexString(crc.getValue());
    }

    private static String serialize(Processor processor, XdmNode node) throws SaxonApiException {
        Serializer serializer = processor.newSerializer();
        StringWriter writer = new StringWriter();
        serializer.setOutputWriter(writer);
        processor.writeXdmValue(node, serializer);
        return writer.toString();
    }
}
