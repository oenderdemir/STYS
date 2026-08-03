# E-Belge UBL/PDF/E-Posta Renderer Hazırlık Raporu (Faz 2B.5 — Düzeltilmiş)

Bu rapor, önceki sürümün (`21a81b5`) Faz 2B.5 ön incelemesinin kabul edilmemesi üzerine, mevcut
sonuçlar otorite kabul edilmeden, repository kodu ve indirilmiş resmî GİB paketleri üzerinden
yeniden hazırlanmıştır.

İnceleme tarihi: 03.08.2026

İncelenen resmi GİB kaynakları (indirilip yerel olarak açılmıştır):

- https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-FaturaPaketi.zip
- https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/earsiv_paket_v1.1_8.zip
- https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/UBLTR_1.2.1_Kilavuzlar.zip

Genel bulgu: **`UBLTR-Invoice-2.1.xsd`, `CommonAggregateComponents-2` ve `CommonBasicComponents-2`
— örnek XML'lerin `xsi:schemaLocation` ile referans verdiği ve `ProfileIDCheck` kuralının adıyla
andığı asıl UBL gövde şemaları — indirilen iki pakette de fiziksel olarak yok.** XSD-seviyesi
zorunluluk iddiaları bu nedenle schematron kanıtına indirgenmiştir; XSD'ye dayandırılan hiçbir
"zorunlu/opsiyonel" iddiası bu raporda öne sürülmemektedir, aksine bu eksiklik ayrı bir açık madde
olarak işaretlenmiştir (bkz. §6, §10).

## 1. Tek renderer girdisi kuralı

Renderer'ın tek iş girdisi `IEBelgeCanonicalSnapshotReader.Oku(...)` çıktısı olan
`EBelgeCanonicalSnapshotV1`'dir (`backend/Muhasebe/SatisBelgeleri/EBelgeCanonicalSnapshotReader.cs:33-91`).
Bu tip, `SnapshotSchemaVersion`, hash ve alan-bazlı `ValidateSnapshot` kontrolleriyle
doğrulanıyor; deserialize edilen JSON'un yeniden serialize edilmiş hali `talep.CanonicalJson` ile
birebir (ordinal) eşleşmezse `EBelgeCanonicalSnapshotException` fırlatılıyor. Bu, canonical alan
setinin dışına çıkan hiçbir ek/gölge alanın sessizce kabul edilemeyeceği anlamına gelir
(`UnmappedMemberHandling = Disallow`).

Belge tipi, belge/kesim tarihi, tenant/kurum bağlamı, taraf bilgileri, vergi bilgileri,
`ProfileID`, `InvoiceTypeCode`, birim kodları için ikinci bir otoriter girdi
**önerilmemektedir**. Aşağıdaki bölümlerde tespit edilen eksik alanların tamamı, canlı entity
okuma veya sonradan üretilen sidecar/yayın snapshot'ı ile değil, **kesim anında canonical
snapshot'a yazılacak yeni alanlarla (V2)** kapatılmalıdır. V1 kayıtları değiştirilmeyecek (§8).

Renderer sürümü ile GİB paket/kod listesi sürümü ayrımı: Tarih bazlı registry yalnızca
"bu `BelgeTarihi`/`FaturaKesimTarihi` için hangi schematron+codelist versiyonu yürürlükteydi"
sorusuna cevap vermeli (örn. 27.07.2026 öncesi V1.42, sonrası V1.43 — bkz. §10). Bu registry
**hiçbir zaman** `ProfileID`, `InvoiceTypeCode`, birim kodu gibi belgeye özgü iş kararı
üretmemeli; bu kararlar yalnızca canonical snapshot'taki alanlardan gelmelidir.

## 2. Eşleme matrisi

| Snapshot alanı | Hedef UBL elemanı/attribute | Zorunluluk | Dönüşüm/kod listesi | Durum |
| --- | --- | --- | --- | --- |
| `Belge.EBelgeUuid` | `cbc:UUID` | Zorunlu | — | Doğrudan kullanılabilir |
| `Belge.ResmiFaturaNo` | `cbc:ID` | Zorunlu, format `^[A-Z0-9]{3}20YY[0-9]{9}$` (`InvoiceIDCheck`, Common_Schematron.xml:153-155) | 16 karakter format kontrolü | Deterministik eşlenebilir (format doğrulaması eklenmeli) |
| `Belge.FaturaKesimTarihi`/`BelgeTarihi` | `cbc:IssueDate` | Zorunlu | ISO tarih | Deterministik eşlenebilir — kaynak alan (`FaturaKesimTarihi` vs `BelgeTarihi`) karara bağlanmalı |
| — (yok) | `cbc:UBLVersionID` | Zorunlu, sabit `2.1` hedeflenmeli | — | Otoriter kaynak eksik: renderer sabiti olmalı, snapshot alanı değil |
| — (yok) | `cbc:CustomizationID` | Zorunlu, `TR1.2` veya `TR1.2.1` (`CustomizationIDCheck`, Common_Schematron.xml:141-143) | — | Otoriter kaynak eksik: renderer sabiti olmalı |
| — (yok, `EBelgeKanali`'ndan kısmen türer) | `cbc:ProfileID` | Zorunlu | `ProfileIDCheck` + `InvoiceTypeCodeCheck` (Common_Schematron.xml:146-150, 172-177) | Otoriter kaynak eksik (bkz. §3) |
| — (yok) | `cbc:InvoiceTypeCode` | Zorunlu | `InvoiceTypeCodeCheck` (Common_Schematron.xml:172-177) | Otoriter kaynak eksik (bkz. §4) |
| `Odeme.ParaBirimi` | `cbc:DocumentCurrencyCode` | Zorunlu | ISO 4217 (`CurrencyCodeList`, Codelist.xml:48) | Deterministik eşlenebilir |
| Satır sayısı | `cbc:LineCountNumeric` | Zorunlu | — | Doğrudan kullanılabilir |
| `Kurum.KurumUnvani` + `Kurum.VergiNo` | `AccountingSupplierParty/Party/PartyIdentification[@schemeID='VKN']` + `PartyName/Name` | Zorunlu (VKN 10 hane; `PartyIdentificationTCKNVKNCheck`, `PartyIdentificationPartyNamePersonCheck`, Common_Schematron.xml:255-258, 281-286) | — | Deterministik eşlenebilir |
| `Kurum.VergiDairesi` | `PartyTaxScheme/TaxScheme/Name` | Sadece `ProfileID='IHRACAT'` iken şart (`PartyVDCheck`, Common_Schematron.xml:439-441) | Kod yok, sadece isim | İlk sürümde destek dışı bırakılmalı (ihracat kapsam dışıysa) |
| `Kurum.Adres` | `AccountingSupplierParty/Party/PostalAddress` | XSD ile teyit edilemiyor (asıl XSD yok); Invoice tarafı için schematron zorunluluğu yok | — | Otoriter kaynak eksik — alan yapısı ve zorunluluk netleşmeli (bkz. §6). **`Tesis.Adres` ile karıştırılmamalı** |
| `Tesis.Adres`/`Tesis.Telefon` | Şube/teslimat bağlamı (`cac:Delivery` veya not alanı — Party adresi değil) | Belirsiz | — | Otoriter kaynak eksik: Kurum/Tesis adresinin hangi UBL yapısına gideceği ayrı karar gerektirir |
| `Alici.MusteriVergiNo` + `KurumsalMi=true` | `AccountingCustomerParty/Party/PartyIdentification[@schemeID='VKN']` + `PartyName/Name` (`MusteriUnvan`) | Zorunlu, VKN 10 hane | `PartyIdentificationPartyNamePersonCheck` | Deterministik eşlenebilir |
| `Alici.MusteriTcKimlikNo` + `KurumsalMi=false` | `PartyIdentification[@schemeID='TCKN']` + `Person/FirstName` + `Person/FamilyName` | Zorunlu, TCKN 11 hane, FirstName/FamilyName ikisi de boş olamaz (`PartyIdentificationPartyNamePersonCheck`, Common_Schematron.xml:281-286) | — | Otoriter kaynak eksik: `MusteriAdSoyad` tek string; ayrı `Ad`/`Soyad` alanları yoksa tahmini bölme yasak (bkz. §6) |
| `Alici.MusteriVergiDairesi` | `PartyTaxScheme/TaxScheme/Name` | Belirsiz (genel kural yok; e-Arşiv XSD'sinde ayrı `TaxOfficeCode` de var) | — | Otoriter kaynak eksik |
| `Alici.MusteriAdres` | `PostalAddress` | Belirsiz (bkz. §6) | — | Otoriter kaynak eksik |
| `Odeme.VadeTarihi` | `cac:PaymentTerms`/`PaymentDueDate` | Opsiyonel | — | Doğrudan kullanılabilir |
| `Satir.Miktar` | `cbc:InvoicedQuantity` | Zorunlu | — | Doğrudan kullanılabilir |
| `Satir.Birim` (serbest metin) | `cbc:InvoicedQuantity/@unitCode` | Zorunlu attribute (`InvoicedQuantityCheck`, Common_Schematron.xml:437-439); UBLVersionID=2.1 iken kod-listesi kontrolü de var (`GeneralUnitCodeCheck`, :208-211) | `UnitCodeList` (Codelist.xml:56) | Otoriter kaynak eksik — serbest metinden asla türetilemez (bkz. §5) |
| `Satir.BirimFiyat`, `IndirimOrani`, `IndirimTutari` | `cac:Price/PriceAmount`, `AllowanceCharge` | Zorunlu/opsiyonel | — | Deterministik eşlenebilir |
| `Satir.Matrah` | `LineExtensionAmount` | Zorunlu | — | Deterministik eşlenebilir |
| `Satir.KdvUygulamaTipi` + `KdvOrani` + `KdvTutari` | `TaxTotal/TaxSubtotal/TaxCategory/Percent` + `TaxScheme/TaxTypeCode` | Zorunlu | `TaxTypeCheck` (Common_Schematron.xml:399-401), `$TaxType` | Kısmen deterministik — kod `0015` (KDV) sabit varsayılabilir ama istisna/tevkifat dallanması ayrı karar gerektirir |
| `Satir.KdvIstisnaKodu` + `KdvIstisnaAciklamasi` | `TaxCategory/TaxExemptionReasonCode` + `TaxExemptionReason` | `TaxExemptionReason` varsa kod zorunlu (`TaxExemptionReasonCodeCheck`, Common_Schematron.xml:378-386) | `TaxExemptionReasonCodeType` (Codelist.xml:21) | Deterministik eşlenebilir (kod snapshot'ta zaten var) |
| `Satir.TevkifatPay`/`Payda`/`Tutari` | `WithholdingTaxTotal/TaxSubtotal/TaxCategory/Percent` + `TaxTypeCode` | `TaxTypeCode`+`Percent` kombinasyonu `$WithholdingTaxTypeWithPercent` içinde olmalı (`WithholdingTaxTotalCheck`, Common_Schematron.xml:307-312) | `WithholdingTaxType` (601-627, 801-825) | Otoriter kaynak eksik: snapshot'ta sadece pay/payda oranı var, tevkifat *kodu* yok (bkz. §4) |
| `Satir.OtvOrani`/`OtvTutari` | `TaxTotal/TaxSubtotal/TaxCategory/TaxScheme/TaxTypeCode` (ÖTV'ye özgü ayrı kural yok) | Belirsiz | `TaxType` (Codelist.xml:15), Türkçe etiket yok | Otoriter kaynak eksik (bkz. §4) |
| `Satir.OivOrani`/`OivTutari` | Aynı generic `TaxTypeCode` mekanizması | Belirsiz | Aynı `TaxType` listesi, etiketsiz | Otoriter kaynak eksik |
| `Satir.KonaklamaVergisiOrani`/`Tutari` | Belge düzeyinde `InvoiceTypeCode='KONAKLAMAVERGISI'` (Codelist.xml:10) + satır düzeyinde vergi kodu | Belge tipi + satır kodu ikisi de gerekli | `InvoiceTypeCodeList` | Kısmen deterministik (tip kodu net, satır vergi kodu net değil) |
| `Iade.IadeEdilenBelgeNo`/`EBelgeUuid`/`BelgeTarihi` | `cac:BillingReference/cac:InvoiceDocumentReference` (`cbc:ID` 16 hane, `cbc:DocumentTypeCode='IADE'`) — **`cac:AdditionalDocumentReference` değil** (`IADEInvioceCheck`, Common_Schematron.xml:491-493) | `InvoiceTypeCode` ∈ {TEVKIFATIADE, IADE, YTBIADE, YTBTEVKIFATIADE} iken zorunlu | — | Deterministik eşlenebilir ama hedef eleman `BillingReference`'dır |
| `Metadata.SnapshotSchemaVersion` + `CanonicalSha256` | Renderer'ın dahili audit/log alanı (UBL'e yazılmaz) | — | — | Doğrudan kullanılabilir |

## 3. ProfileID kararı

**`EBelgeKanali.EArsiv` → `EARSIVFATURA` kesin türetilebilir mi?** Evet. `ProfileIDCheck` kuralı,
`type='earchive'` durumunda `$ProfileIDTypeEarchive` listesini tek değerle (`EARSIVFATURA`,
Codelist.xml:6) sınırlıyor. Bu, kanaldan doğrudan ve güvenle türetilebilecek tek durumdur.

**`EFatura` kanalından `TEMELFATURA` veya `TICARIFATURA` seçilebilir mi?** Hayır.
`EBelgeKanali.EFatura` sadece "bu belge e-Fatura sistemine gidiyor" bilgisini taşır;
`TEMELFATURA`/`TICARIFATURA` ayrımı GİB nezdinde **alıcının e-Fatura portalında kayıtlı profil
türüne** bağlıdır — bu, `SatisBelgesiTipi`, `KdvUygulamaTipi` veya mevcut `CariKart` alanlarından
(`EFaturaMukellefiMi`, `EArsivKapsamindaMi`) türetilemez; bunlar sadece "e-Fatura mükellefi mi"
bilgisini taşır, "hangi profile kayıtlı" bilgisini taşımaz.

Ayrıca schematron'dan çıkan bağlayıcı ek kural: `InvoiceTypeCodeCheck` assert 2
(Common_Schematron.xml:172-177), `InvoiceTypeCode='IADE'` iken `ProfileID`'nin yalnızca
`{TEMELFATURA, EARSIVFATURA, ILAC_TIBBICIHAZ, YATIRIMTESVIK, IDIS, KAMU}` kümesinden olabileceğini,
**`TICARIFATURA`'nın bu kümede olmadığını** söylüyor. Yani alıcı normalde `TICARIFATURA` profiline
kayıtlı olsa bile, bir iade belgesi bu profille asla düzenlenemez.

**Seçilemiyorsa yeni otoriter alan hangi iş modelinde tutulmalı?** Alıcının GİB'e kayıtlı e-Fatura
profili mükellef ilişkisine ait bir veridir; en doğal yeri `CariKart` (zaten `EFaturaMukellefiMi`/
`EArsivKapsamindaMi` tutulan yer) üzerinde yeni bir alan (örn. `AliciEFaturaProfili`: Temel/Ticari)
olmalıdır. Bu alan GİB mükellef sorgulama servisinden veya manuel kayıttan beslenmeli; repository
genelinde `ProfileID`/`TEMELFATURA`/`TICARIFATURA` string'i sıfır `.cs` dosyasında geçiyor — bu
tamamen modellenmemiş bir alan.

**Bu alan canonical V2 snapshot'a nasıl alınmalı?** `EBelgeCanonicalCariKartV1` bölümüne yeni bir
alan eklenmeli ve kesim anında `CariKart`'tan okunup dondurulmalı; nihai `ProfileID` kararı
resolver'da bu ham profil ile belge tipinin (satış/iade) birleşiminden üretilmeli — snapshot ham
alıcı profilini taşır, resolver iade istisnasını (yukarıda) uygular.

**Sessiz `TEMELFATURA` varsayımının riski nedir?** İade belgelerinde bu varsayım schematron'a göre
zaten doğru sonuca denk gelir, fakat **normal satış faturalarında** alıcı `TICARIFATURA`'ya
kayıtlıysa yanlıştır: Ticari Fatura profili GİB tarafında "Uygulama Yanıtı" (kabul/red) iş akışını
ve itiraz süresi farklarını tetikler; sessizce Temel Fatura seçmek, alıcının beklediği kabul/red
bildirim akışının hiç oluşmamasına, farklı hukuki-ticari nitelikte bir belge üretilmesine yol açar.
Bu risk XML doğrulama hatası olarak görünmez; sessizce yanlış belge türü üretme riskidir.
`ProfileID` seçimi tarih bazlı registry'ye bırakılmamalıdır.

## 4. InvoiceTypeCode ve vergi senaryoları

`SatisBelgesiTipi` (`FaturaTaslagi, SatisFaturasi, IadeFaturasi, Proforma, AlisFaturasi,
SatisIadeFaturasi, AlisIadeFaturasi`) ve `KdvUygulamaTipi` (`Kdvli, TamIstisna, KismiIstisna,
KdvKapsamDisi, Tevkifatli`) satır düzeyinde tutuluyor; `InvoiceTypeCode` ise belge düzeyinde tek
bir değer. Bu asimetri aşağıda kod bazında değerlendirilmiştir.

**SATIS** — `Kdvli` satırlardan oluşan, iade olmayan bir `SatisFaturasi` için doğrudan
türetilebilir; en düşük risk barındıran durum.

**IADE** — `IadeEdilenBelgeId` dolu ve belge iade tipinde olduğunda seçilebilir, ancak bu seçim
`ProfileID`'yi `TICARIFATURA` dışına zorlar (§3) ve `IADEInvioceCheck` kuralı gereği
`cac:BillingReference/cac:InvoiceDocumentReference` altında 16 haneli `ID` + `DocumentTypeCode='IADE'`
üretilmesini şart koşar. Mevcut `EBelgeCanonicalIadeV1` alanları bu referansı kurmaya yeterli
görünüyor, ancak 16 hane format garantisi ve kaynak alan seçimi (`IadeEdilenBelgeNo` vs
`IadeEdilenFaturaNo`) netleşmemiştir.

**TEVKIFAT** — `KdvUygulamaTipi=Tevkifatli` satırların varlığı `InvoiceTypeCode=TEVKIFAT` seçimini
işaret eder; ancak `WithholdingTaxTotalCheck` kuralı `TaxTypeCode`+`Percent` kombinasyonunun
`$WithholdingTaxTypeWithPercent` listesinde birebir olmasını istiyor. Snapshot'ta yalnızca
`TevkifatPay`/`TevkifatPayda` (kesir) ve `TevkifatTutari` var; **tevkifat kodu (601-627/801-825)
alanı hiç yok.**

**ISTISNA** — `KdvIstisnaKodu` zaten satırda mevcut ve `TaxExemptionReasonCodeCheck` kuralına göre
`InvoiceTypeCode` bu kodun ait olduğu alt-listeye göre seçilmelidir. Kod alanı deterministik
eşlenebilir durumda; ancak belge düzeyinde `InvoiceTypeCode` tek değerdir — bir satır `Kdvli`,
başka bir satır `TamIstisna` ise, belge için tek bir `InvoiceTypeCode` seçmenin genel bir kuralı
schematron'da tanımlı değildir (aşağıya bakınız).

**OZELMATRAH** — `TaxExemptionReasonCodeCheck` içinde ayrı bir `$ozelMatrahTaxExemptionReasonCodeType`
alt-listesi var, ancak bu senaryonun snapshot'ta ayrı bir işaretleyicisi yok (`KdvUygulamaTipi`
enum'unda `OzelMatrah` değeri yok) — bu senaryo bugünkü modelde hiç ayırt edilemez.

**Karışık KDV/istisna/tevkifat satırlarında resmi kural nedir?** İncelenen schematron
dosyalarında, tek bir belgede farklı satır türlerinin birlikte bulunması durumunda belge düzeyinde
hangi tek `InvoiceTypeCode`'un seçileceğine dair genel bir kural bulunamadı — yalnızca özel
profiller (`ILAC_TIBBICIHAZ`, `YATIRIMTESVIK`, `IDIS`) için bazı kodların birlikte izinli olduğu
belirtiliyor. Genel `TEMELFATURA`/`TICARIFATURA` profilinde karışık satır senaryosu için resmi bir
kural bulunamadı; bu, iş modeli tarafından açıkça çözülmesi gereken bir karardır (örn. "aynı
belgede tevkifat ve istisna satırı birlikte varsa v1'de reddet"), tahmini bir kural
uydurulmamıştır.

**Belge düzeyinde yeni otoriter `InvoiceTypeCode` alanı gerekli mi?** Evet.

**Tevkifat kodu ayrıca gerekli mi?** Evet, ayrı ve zorunlu.

**ÖTV için liste bilgisi gerekli mi?** ÖTV'ye özgü ayrı, isimli bir schematron kuralı ya da
codelist bulunamadı; `OtvOrani`/`OtvTutari` generic `$TaxType` (34 kodluk, Türkçe etiketsiz)
mekanizmasına giriyor olmalı. Hangi bare kodun ÖTV'ye karşılık geldiği bu paketlerden
doğrulanamadı — liste/tarife bilgisi olmadan bu alan güvenle kodlanamaz.

**ÖİV ve konaklama vergisi kodları güvenle sabit eşlenebilir mi?** Konaklama vergisi belge
düzeyinde ayrı bir `InvoiceTypeCode` değeri (`KONAKLAMAVERGISI`) olarak zaten kod listesinde var —
bu kısım güvenle sabitlenebilir. Ancak satır düzeyinde ÖİV/konaklama vergisi `TaxTypeCode`'unun
hangi bare koda karşılık geldiği etiketsiz olduğu için doğrulanamadı; sabit eşleme önerilemez.

**Snapshot toplamlarıyla hesaplanan UBL toplamları uyuşmazsa renderer belge üretmeyi reddetmeli
mi?** Evet, kesin: uyuşmazlık durumunda belge üretimi reddedilmeli ve kalıcı hata üretilmelidir.
Otomatik düzeltme veya yuvarlama yapılmamalıdır.

## 5. Birim kodları

Serbest metin `Birim` alanı (`SatisBelgesiSatiri.Birim`, varsayılan değer literal `"Adet"`)
doğrudan `unitCode` olarak kullanılamaz — bu hem `InvoicedQuantityCheck` (unitCode attribute
zorunlu) hem `GeneralUnitCodeCheck` (UBLVersionID=2.1 için kod-listesi doğrulaması) tarafından
teyit ediliyor.

**"Adet" için resmî kod:** `UBL-TR_Codelist.xml` dosyasındaki `$UnitCodeList` yalnızca bare
UN/ECE kodlarını (`C62`, `NIU`, `DAY`, `HUR`, `MON`, `ANN`, `WEE`, `LBR` vb.) içeriyor; Türkçe
etiket sütunu (örn. "Adet"→hangi kod) bu dosyada yok. GİB'in örnek XML'inde (`1_TEMEL_FATURA.xml`)
bir adet sayımı için `unitCode="NIU"` kullanılmış, ancak bu tek bir demo örneği — genel/resmi bir
kural olarak sunulamaz. **"Adet" için dahi bu paketlerden kesin/otoriter bir kod teyit edilemedi.**

**Gece, gün, saat, kişi, porsiyon:** Aynı gerekçeyle hiçbiri için kesin karşılık teyit edilemedi.
Bu raporun sonucu: birim kodu eşlemesinin tamamı otoriter kaynak eksikliği olarak
işaretlenmelidir.

**Otoriter `BirimKodu` alanı gerekli mi?** Evet — hem `SatisBelgesiSatiri` entity'sinde hem
canonical V2 snapshot'ta zorunlu yeni bir alan olmalı; serbest metin `Birim` alanı kullanıcı
arayüzü için kalabilir ama UBL üretimi bu yeni koddan beslenmeli.

## 6. Taraf ve adres yapıları

**Kurumsal alıcı (VKN):** `PartyIdentificationPartyNamePersonCheck` kuralı, `schemeID='VKN'`
olduğunda `PartyName/Name`'in dolu olmasını şart koşuyor — `MusteriUnvan` bu ihtiyacı karşılıyor,
VKN 10 hane kontrolüyle birlikte deterministik eşlenebilir.

**Gerçek kişi alıcı (TCKN):** Aynı kural, `schemeID='TCKN'` olduğunda `cac:Person` altında hem
`FirstName` hem `FamilyName`'in ayrı ayrı boş olmamasını şart koşuyor. Mevcut model tek bir
`MusteriAdSoyad` string alanı taşıyor. **`MusteriAdSoyad` alanı boşluğa göre tahmini
bölünmemelidir** — bileşik soyadlar, göbek adları gibi durumlarda yanlış bölünme, hatalı `Person`
bilgisiyle geçersiz veya yanlış kişiye ait resmi belge üretme riskini doğurur. Bu alan şu haliyle
uygun değil, otoriter kaynak eksiktir: ayrı `Ad`/`Soyad` alanları kesim anında ayrı ayrı toplanıp
canonical V2'ye eklenmelidir.

**Vergi dairesi:** Schematron'da genel bir zorunluluk yok (sadece `IHRACAT` profili için
`TaxScheme/Name` boş olamaz kuralı var); ancak e-Arşiv XSD'sinde (`EArsiv.xsd`, farklı/
basitleştirilmiş bir şema) vergi dairesi `TaxOfficeName`+`TaxOfficeCode` olarak ikili ve zorunlu
modellenmiş. STYS'in mevcut `VergiDairesi` alanı yalnızca isim (string) taşıyor, kod taşımıyor. Bu,
e-Fatura ve e-Arşiv kanalları arasında potansiyel bir tutarsızlık kaynağıdır ve iki kanal için
ayrı ayrı netleştirilmelidir.

**Adres yapıları — genel bulgu:** Ne e-Fatura schematron dosyalarında (Invoice tarafı için) ne de
mevcut paketlerdeki XSD'lerde, `AccountingSupplierParty`/`AccountingCustomerParty` altındaki
`PostalAddress` için kesin bir zorunluluk teyit edilebildi (asıl UBL Invoice XSD'si elde
bulunmuyor). Schematron'da yalnızca DespatchAdvice teslimat adresi için somut kurallar var
(`CitySubdivisionName`, `CityName`, `Country/Name` boş olamaz + `PostalZone` regex
`^((0[1-9])|([1-7][0-9])|(8[0-1]))[0-9]{3}$`) — bu kurallar Invoice tarafına genellenemez.
**Tek serbest metnin yeterli olup olmadığı, ilçe/il/posta kodu/ülke alanlarının zorunlu olup
olmadığı, asıl `UBLTR-Invoice-2.1.xsd` indirilip incelenmeden kesin karara bağlanamaz.**

**`Kurum.Adres` ile `Tesis.Adres` karıştırılmamalı:** Model, `Kurum.Adres`'i nullable (opsiyonel),
`Tesis.Adres`'i zorunlu, dolu string olarak tutuyor. UBL'de `AccountingSupplierParty/PostalAddress`,
mükellefin (Kurum'un) yasal/kayıtlı adresini temsil etmesi beklenen bir alandır; `Tesis` ise
fiziksel şube/tesis bilgisidir ve aynı Kurum'a bağlı birden fazla Tesis'te farklılaşabilir. Eğer UBL
çıktısı için Kurum'un yasal adresi zorunluysa ve `Kurum.Adres` null olabiliyorsa, bu başlı başına
bir eksikliktir; sessizce `Tesis.Adres`'e düşülmesi kabul edilemez çünkü hukuki taraf adresini fiili
hizmet adresiyle karıştırmış olur. Bu karar açıkça ve ayrı verilmelidir.

## 7. İlk renderer kapsamı

**Seçenek A — Yalnız standart TRY satış faturası, standart KDV, ek vergi/tevkifat/istisna yok:**

- Eksik otoriter alanlar: alıcı e-Fatura profili (Temel/Ticari, §3), birim kodu (§5), yapısal adres
  kararı (§6), (varsa) vergi dairesi kodu.
- Model değişiklikleri: `CariKart`'a profil alanı, `SatisBelgesiSatiri`'ne `BirimKodu`, gerekirse
  yapısal adres alanları.
- Snapshot V2 etkisi: orta — az sayıda yeni zorunlu alan, satır bazlı vergi karmaşıklığı yok.
- Test yükü: düşük — tek `InvoiceTypeCode=SATIS`, tek profil dallanması.
- Hatalı resmi belge üretme riski: düşük.

**Seçenek B — Tüm satış, iade, tevkifat, istisna, ÖTV, ÖİV, konaklama vergisi senaryoları:**

- Eksik otoriter alanlar: A'daki her şeye ek olarak tevkifat kodu, ÖTV liste/tarife bilgisi,
  ÖİV/konaklama vergisi kod teyidi, karışık satır senaryosunda belge-düzeyi `InvoiceTypeCode`
  seçim politikası (resmi kural bulunamadı), IADE senaryosunda `BillingReference` yapısı ve
  profil-düşürme kuralı.
- Model değişiklikleri: kapsamlı — vergi kodu resolver'ı, tevkifat kod tablosu, ÖTV tarife verisi,
  belge-tipi/profile resolver, karışık-satır politikası.
- Snapshot V2 etkisi: büyük.
- Test yükü: yüksek — çok sayıda profil × tip × vergi kombinasyonunun schematron çapraz
  kontrolleriyle test edilmesi gerekir.
- Hatalı resmi belge üretme riski: yüksek, resolver kapsamlı test edilmeden.

`AlisIadeFaturasi`, kod tarafında `StysTarafindanDuzenlenirMi()` içinde (STYS tarafından
düzenlenen giden belge) yer alıyor. Ancak bu senaryo `InvoiceTypeCode=IADE` gerektirir, bu da hem
profil-düşürme kuralını hem `BillingReference/InvoiceDocumentReference` yapısını devreye sokar —
bunların ikisi de bugün doğrulanmış/test edilmiş değil. `AlisIadeFaturasi`'nin ilk sürüme kanıt
sunmadan alınması önerilmez.

**Önerilen en küçük güvenli ve üretilebilir kapsam:** Seçenek A, yalnızca `SatisFaturasi`,
`InvoiceTypeCode=SATIS`, tüm satırlar `KdvUygulamaTipi=Kdvli`, `ParaBirimi=TRY`.
`AlisIadeFaturasi` ve diğer tüm iade/istisna/tevkifat/ÖTV/ÖİV/konaklama senaryoları sonraki
fazlara bırakılmalıdır.

## 8. V1/V2 stratejisi

**V1 ile desteklenebilecek en dar senaryo var mı?** Hayır. Seçenek A'daki en dar kapsam bile alıcı
profili, birim kodu ve adres yapısı kararı gibi V1'de bulunmayan alanlar gerektiriyor. En dar
kapsam dahi V2 gerektirir.

**Yeni canonical alanlar nedeniyle V2 gerekli mi?** Evet, kesin gerekli.

**Reader aynı interface altında V1 ve V2'yi nasıl ayrıştırmalı?** Mevcut
`EBelgeCanonicalSnapshotReader`, `ValidateTalep` içinde `talep.SnapshotSchemaVersion`'ı tek sabit
değere (`SupportedSnapshotSchemaVersion = "1"`) eşitlik kontrolüyle sınırlıyor
(`EBelgeCanonicalSnapshotReader.cs:100-103`). V2 eklenirken bu kontrol, desteklenen versiyon
kümesine (`{"1","2"}`) genişletilmeli ve `Oku` metodu versiyona göre farklı record tipini
(`EBelgeCanonicalSnapshotV1` sabit kalır, yeni `EBelgeCanonicalSnapshotV2` eklenir) deserialize
edip aynı arayüz (`IEBelgeCanonicalSnapshotReader`) üzerinden döndürmelidir — V1'in mevcut alan
seti/hash doğrulama davranışı değiştirilmemeli, çünkü `ValidateHashMatchesJson` tam JSON string
eşitliğine dayanıyor; V1 record şeklinde yapılacak herhangi bir alan ekleme/çıkarma, geçmişte
hash'lenmiş V1 snapshot'ların doğrulamasını bozar.

**Eski V1 snapshot'lar render edilemiyorsa hangi güvenli kalıcı hata koduyla durmalı?** Mevcut
`EBelgeCanonicalSnapshotException` deseniyle tutarlı, ayrı bir hata sabiti tanımlanmalı (örn.
`EBELGE_UBL_RENDER_SNAPSHOT_VERSION_UNSUPPORTED`, HTTP 422) — "canonical snapshot geçersiz"
(mevcut `EBELGE_CANONICAL_SNAPSHOT_INVALID`) ile "snapshot geçerli ama render için yeterli/
desteklenen versiyon değil" durumları karıştırılmamalı; bu ikinci durum kalıcı ve tekrar denemeyle
çözülmeyecek bir hata olarak işaretlenmeli.

**Eski snapshot'ların migration ile değiştirilmemesi nasıl sağlanmalı?** V1 satırları hiçbir zaman
UPDATE edilmemeli; yeni alanlar yalnızca kesim anından itibaren `SnapshotSchemaVersion="2"` ile
oluşturulan yeni `EBelgeSnapshot` satırlarında bulunur. Var olan V1 satırları render edilemez
durumda kalıp yukarıdaki kalıcı hata koduyla işaretlenir — geriye dönük zenginleştirme veya
backfill yapılmaz. Ayrı bir "UBL input/yayın snapshot'ı" kavramı önerilmiyor; V2, aynı
`EBelgeSnapshot` mekanizmasının yeni şema versiyonudur.

## 9. Determinizm sözleşmesi

| Konu | Politika | Test beklentisi |
| --- | --- | --- |
| Aynı typed snapshot → aynı çıktı | Renderer saf fonksiyon olmalı; girdi dışı hiçbir durum (saat, ortam, DB) okunmamalı | Aynı snapshot ile N kez çağrı → byte-birebir aynı çıktı testi |
| UTF-8 / BOM | UTF-8, BOM'suz çıktı sabitlenmeli | İlk 3 byte'ın `EF BB BF` olmadığı testi |
| XML declaration | `<?xml version="1.0" encoding="UTF-8"?>` sabit, tek satır, tek boşluk düzeni | Golden-file karşılaştırması |
| Indentation | Sabit (girintisiz veya sabit 2-boşluk), seçim yapılıp sonradan değişmemeli | Golden-file |
| Newline | `\n` (LF) sabit, `\r\n` üretilmemeli | Byte-seviye newline testi |
| Namespace prefixleri | Sabit prefiks tablosu (`cac:`, `cbc:` vb.) | Golden-file + şema prefiks testi |
| Element/attribute sırası | Sabit sıra, veri sırasına bağlı olmayan serileştirme | Sıra-duyarlı golden-file testi |
| Decimal lexical format | Sabit ondalık ayıraç (`.`), sabit basamak kuralı, `InvariantCulture` | Kültür değiştirilerek aynı çıktı testi |
| Tarih/saat biçimi | ISO 8601 (`yyyy-MM-dd`), saat dilimi bilgisi olmadan | Farklı sistem saat dilimlerinde aynı çıktı testi |
| Kültür/saat dilimi/OS bağımsızlığı | Tüm formatlama `InvariantCulture` ile | CI'da en az iki farklı culture/saat dilimiyle test |
| Rastgele UUID / güncel saat yasağı | Renderer `Guid.NewGuid()`/`DateTime.Now` çağırmamalı | Statik analiz + zaman/rastgelelik enjekte edilip aynı çıktı beklentisi |
| SHA-256 (exact UTF-8 bytes) | Üretilen XML'in tam UTF-8 byte dizisi üzerinden hesaplanmalı | Byte dizisi değişmeden hash'in de değişmediği regresyon testi |
| Renderer sürümü | Her çıktıya hangi renderer sürümü/GİB paket sürümüyle üretildiği damgalanmalı | Sürüm damgasının snapshot ile saklandığı entegrasyon testi |
| Dışarıdan değiştirilemeyen byte çıktısı | Üretilen XML immutable saklanmalı; hiçbir kod yolu içeriği güncellemez | Depolama katmanı geldiğinde "overwrite yasak" testi |

## 10. Resmî paket sürümleri

**e-FaturaPaketi.zip:**

- Kullanılan yollar: `xml/1_TEMEL_FATURA.xml`, `xml/2_TICARI_FATURA.xml`, `xml/3_TICARI_FATURA.xml`,
  `xml/6_TEMEL_FATURA_KDV_SIFIR.xml`, `xml/7_TEMEL_FATURA_IADE.xml`, `xml/7_TICARI_FATURA.xml`,
  `schematron/UBL-TR_Main_Schematron.xml`, `schematron/UBL-TR_Common_Schematron.xml`,
  `schematron/UBL-TR_Codelist.xml`.
- Örnek XML'ler `CustomizationID=TR1.0` taşıyor (2013 tarihli demo veri) — schematron'un talep
  ettiği `TR1.2`/`TR1.2.1` ile uyumsuz; bu örnekler yalnızca element sırası/isimlendirme referansı
  için kullanılabilir, doğrulama/uygunluk referansı olarak kullanılamaz.
- Schematron dosyalarının mtime'ı 27.07.2026 — kod listesi V1.43 ile tutarlı.
- Yürürlük tarihi: bu zip içinde ayrı bir "yürürlük tarihi" alanı yok.

**earsiv_paket_v1.1_8.zip:** e-Arşiv'e özgü, UBL namespace'i kullanmayan, farklı ve
basitleştirilmiş bir şema seti (`EArsiv.xsd`, `eArsivVeri.xsd`, `EArsivWs.wsdl`,
`earsiv_schematron.xsl`). Bu paketin `PartyIdentification`/`TaxOffice`/`PostalAddress` tanımları
e-Fatura tarafındakiyle birebir örtüşmüyor (örn. `schemeID` enum'u 7 değerle sınırlı, e-Fatura
schematron'undaki 28 değerlik listeden çok daha dar). Renderer'ın e-Fatura ve e-Arşiv çıktısı için
ayrı doğrulama/alan setleri taşıması gerekir; ikisi tek bir ortak şema varsayımıyla ele alınamaz.

**UBLTR_1.2.1_Kilavuzlar.zip:** PDF/Word kılavuz seti + `Degisim Tablosu.txt`. Bu txt dosyası
sadece tarih/versiyon log'u — versiyon bazlı alan/kural farkı içermiyor. Son giriş:
`27.07.2026 - UBL-TR Kod Listeleri - V 1.43 yayınlandı.` Önceki versiyon: `16.03.2026 - V 1.42`.

**14.09.2026 öncesi/sonrası farklar:** Bu zip'lerin hiçbirinde 14.09.2026 tarihine referans veren
bir "yürürlük tarihi" alanı yok. Bu tarih, `docs/e-belge-ubl-pdf-eposta-on-analiz.md` dosyasının
GİB duyuru sayfasından aktardığı bir bilgidir, paket içeriğinden değil. Alan-bazlı bir
"V1.42→V1.43 farkı" listesi hiçbir dosyada bulunamadı — bu fark uydurulmamalı, ayrı bir açık madde
olarak bırakılmalıdır: hazırlık fazında V1.42 ile V1.43 codelist'lerinin fiili diff'i çıkarılmalı.

**Renderer'ın hangi sürümü neden sabitlemesi gerektiği:** Renderer, gömülü codelist/schematron
kurallarını belirli bir GİB sürümüne (örn. "V1.43 / 27.07.2026") sabitlemeli ve bu sabitleme kod
içinde açıkça sürüm etiketiyle işaretlenmelidir — çünkü bu listeler zamanla değişiyor (V1.18'den
V1.43'e 8 yılda 26 sürüm). Tarih bazlı registry, yalnızca "bu `BelgeTarihi`/`FaturaKesimTarihi`
için hangi gömülü kural seti kullanılacak" sorusuna cevap verir; `ProfileID` gibi belgeye özgü iş
kararı üretmez (§1, §3 ile tutarlı). 14.09.2026 sonrası kesilen belgeler için V1.43 kuralları,
öncesi için V1.42 kuralları kullanılmalıdır — bu ayrım renderer'ın versiyon-seçim mantığının test
edilmesi gereken açık bir dalıdır.

## Sonuç

**Renderer öncesinde ek hazırlık fazı gerekir.**

Hazırlık fazı yalnızca gerçekten zorunlu model/snapshot değişiklikleriyle sınırlanmalıdır:

1. `CariKart`'a alıcının e-Fatura profil bilgisini (Temel/Ticari) taşıyan yeni alan + kesim anında
   bunu belge tipiyle (satış/iade) birleştirip nihai `ProfileID`'yi üreten karar mantığı.
2. Belge düzeyinde otoriter `InvoiceTypeCode` alanı + karışık satır senaryosu için açık bir iş
   politikası.
3. Satır düzeyinde tevkifat kodu (601-627/801-825) alanı.
4. `SatisBelgesiSatiri`/canonical snapshot'a otoriter `BirimKodu` alanı — "Adet" dahil hiçbir
   Türkçe birim adının bu paketlerden güvenle kod eşleşmesi çıkarılamadığı için, ayrı ve etiketli
   bir GİB birim kodu referansı temin edilmeden bu alan doldurulamaz.
5. Gerçek kişi alıcılar için ayrı `Ad`/`Soyad` alanları (mevcut `MusteriAdSoyad` tahmini
   bölünmeyecek).
6. `Kurum.Adres` (yasal adres) ile `Tesis.Adres` (fiziksel tesis) arasındaki UBL hedefinin
   netleştirilmesi ve gerekirse yapısal adres alanları — bu karar, asıl `UBLTR-Invoice-2.1.xsd`
   dosyası temin edilip incelenmeden nihai hale getirilmemelidir.
7. V1'i bozmadan V2 şema versiyonu ekleyen reader/exception tasarımı.

ÖTV/ÖİV/tevkifat liste bilgisi, ihracat profili vergi dairesi zorunluluğu ve `AlisIadeFaturasi`
desteği gibi konular, en dar güvenli kapsamın (yalnız standart TRY `SatisFaturasi`,
`InvoiceTypeCode=SATIS`, tüm satırlar `Kdvli`) dışında tutularak sonraki fazlara ertelenmelidir.
