# Schematron XPath 2.0 POC — sonuç kanıtları

İzole, üretim koduna dokunmayan proof-of-concept. Gerçek GİB `UBL-TR_Main_Schematron.xml` /
`UBL-TR_Common_Schematron.xml` dosyaları (backend'deki vendored kopyalardan aynen kopyalandı,
DEĞİŞTİRİLMEDİ) ve resmi ISO Schematron skeleton XSLT1 dosyaları, Java Saxon-HE 10.9
(`net.sf.saxon:Saxon-HE:10.9`, Maven Central'dan indirildi, MPL-2.0) ile derlendi ve çalıştırıldı.

## Ortam

- Saxon-HE 10.9.jar (SHA aşağıda), JDK 17 (AdoptOpenJDK 17.0.0.20) ile çalıştırıldı.
- Komutlar: `java -cp Saxon-HE-10.9.jar net.sf.saxon.Transform ...`

## Adımlar ve gerçek çıktı

1. **Stage 1** (`iso_dsdl_include.xsl` ile `sch:include` çözümü): `rules/UBL-TR_Main_Schematron.xml` → `out/stage1.xml` (162 KB). Başarılı, ~4.6 sn.
2. **Stage 2** (`iso_abstract_expand.xsl` ile `sch:extends` genişletme): → `out/stage2.xml` (157 KB). Başarılı, ~1.7 sn.
3. **Stage 3** (`iso_svrl_for_xslt1.xsl` + `iso_schematron_skeleton_for_xslt1.xsl` ile SVRL-üretici XSLT derlemesi): → `out/validator.xsl` (632 KB). Başarılı, ~2.5 sn. Üretilen dosyada **12 adet gerçek `exists(` çağrısı** var (grep ile doğrulandı) — .NET `XslCompiledTransform`'un yükleyemediği TAM OLARAK bu ifadeler.
4. **Bilinen ek bulgu (POC'a özgü, GİB dosyası DEĞİŞTİRİLMEDİ)**: derlenen `validator.xsl`, `xs:date(...)` gibi XPath 2.0 yerleşik tip döküm ifadeleri kullanıyor ama GİB kaynak dosyası hiçbir yerde `xmlns:xs`/`sch:ns prefix="xs"` bildirmiyor. Saxon bunu (haklı olarak) `XPST0081 Namespace prefix 'xs' has not been declared` hatasıyla reddetti. POC'ta bu, YALNIZ derlenmiş ÇIKTI dosyasına (`out/validator.xsl`, bizim ürettiğimiz bir ARA ARTEFAKT, GİB kaynak dosyası değil) kök `xsl:stylesheet` elemanına standart `xmlns:xs="http://www.w3.org/2001/XMLSchema"` bildirimi eklenerek (tek satır, semantik değişiklik YOK, yalnız eksik ad alanı bağlamı) çözüldü. Gerçek entegrasyonda bu, derleme adımının (Stage 3) çıktısını post-process eden küçük bir adım veya schematron dosyasına resmi bir `<sch:ns prefix="xs" uri="http://www.w3.org/2001/XMLSchema"/>` eklenmesi (GİB dosyasını DEĞİL, bizim include-zinciri başına eklenen bir wrapper dosyasını) ile kalıcı çözülebilir.
5. **Stage 4a — kurala uygun örnek** (`samples/valid.xml`): `out/valid-svrl.xml`. Sonuç: **2 `svrl:failed-assert`**, hiçbiri `exists()` tabanlı DEĞİL (biri "abstracts" pattern açıklaması, biri ayrı bir bulgu — bkz. aşağıdaki "Yan bulgu"). **`exists(cac:WithholdingTaxTotal)` tabanlı kural bu örnekte HİÇ tetiklenmedi** — beklenen davranış.
6. **Stage 4b — bilinçli ihlal örneği** (`samples/ihlalli.xml`, `InvoiceTypeCode=SATIS` iken `cac:WithholdingTaxTotal` eklendi): `out/ihlalli-svrl.xml`. Sonuç: **10 `svrl:failed-assert`**, içlerinde GERÇEK, tam metniyle şu assertion var:
   ```
   <svrl:failed-assert test="not(cbc:UBLVersionID ='2.1') or not(exists(cac:WithholdingTaxTotal)) or cbc:InvoiceTypeCode = 'TEVKIFAT' or ...">
     <svrl:text>Uyumsuz fatura tipi: 'SATIS'. cac:WithholdingTaxTotal elamanı varken fatura tipi TEVKIFAT,YTBTEVKIFAT,IADE,YTBIADE,SGK,SARJ ve SARJANLIK olabilir.</svrl:text>
   </svrl:failed-assert>
   ```
   Bu, `exists()`'in SAHTE değil GERÇEKTEN yürütüldüğünün doğrudan kanıtıdır — sonuç metni GİB'in kendi Türkçe hata mesajıdır, taklit/regex değildir.
7. **Determinizm**: `ihlalli.xml` iki kez bağımsız çalıştırıldı (`det-run1.xml`, `det-run2.xml`); `diff` SIFIR fark verdi — byte-birebir aynı SVRL çıktısı.
8. **Güvenlik — DTD/XXE**: `samples/xxe-deneme.xml` (yerel `win.ini` dosyasını sızdırmayı deneyen klasik XXE payload'ı) hem `-dtd:off` bayrağıyla hem varsayılan ayarla çalıştırıldı — **hiçbir durumda dosya içeriği sızmadı** (SVRL çıktısında `win.ini` içeriği YOK). Gerçek entegrasyonda `-dtd:off` (CLI) veya `Feature.XML_VERSION`/güvenli `SAXParserFactory` + özel `URIResolver` (API) ile bu kilitlenmeli.
9. **Paralel çalışma**: `valid.xml` ve `ihlalli.xml` aynı anda (arka planda) doğrulandı — sonuçlar birbirini etkilemedi (2 vs 10 `failed-assert`, beklenen değerlerle birebir).
10. **Performans**: CLI üzerinden tek seferlik JVM başlatmalı çalıştırma ~3 sn (JVM soğuk başlatma dahil — asıl doğrulama işlemi bunun küçük bir kısmı). Bu, JVM'in HER doğrulama için yeniden başlatılmaması gerektiğini gösteriyor — kalıcı bir sidecar/servis süreci (JVM bir kez ayağa kalkar, derlenmiş `XsltExecutable` bellekte tutulur) gerçek üretim gecikmesini milisaniye seviyesine indirir (Saxon s9api dokümantasyonu `XsltExecutable`'ın immutable ve thread-safe olduğunu, birden çok `Xslt30Transformer`'ın ondan paralel türetilebileceğini belirtir).

## Yan bulgu (exists() dışı, ayrıca not edilmeli)

`valid.xml` örneğinde `ProfileID=EARSIVFATURA` için "Geçersiz cbc:ProfileID... ProfileIDType listesine bakınız" bulgusu çıktı. Kök neden: `UBL-TR_Main_Schematron.xml` içinde `<let name="type" value="efatura"/>` SABİT KODLANMIŞ (dışarıdan parametre olarak geçilebilir bir `sch:param`/`xsl:param` DEĞİL); `EARSIVFATURA` yalnız `$type='earchive'` iken kontrol edilen `$ProfileIDTypeEarchive` listesindedir. Bu, GİB'in resmi dosyasının varsayılan modunun "efatura" olduğu ve e-Arşiv senaryoları için ya dosyanın `type` değişkeninin değiştirilmesi ya da ayrı bir giriş noktası/parametreleştirme gerektiği anlamına gelir — Faz 2B.5/sonrası için ayrı bir açık konu (bu POC'un kapsamı dışında, GİB dosyası DEĞİŞTİRİLMEDİ).

## Dosyalar

- `rules/` — GİB schematron dosyalarının AYNEN kopyası (kaynak: `backend/Muhasebe/SatisBelgeleri/EBelgeUblKuralSeti/schematron/`)
- `skeleton/` — ISO Schematron skeleton XSLT1 dosyalarının AYNEN kopyası
- `samples/` — POC için üretilen örnek XML'ler (üretim verisi DEĞİL)
- `out/` — üretilen ara/nihai artefaktlar ve SVRL sonuçları (kanıt)
- `Saxon-HE-10.9.jar` — Maven Central'dan indirilen resmi, değiştirilmemiş jar
