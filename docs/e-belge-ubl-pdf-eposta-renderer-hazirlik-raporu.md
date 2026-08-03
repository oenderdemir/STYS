# E-Belge UBL/PDF/E-Posta Renderer Hazırlık Raporu (Faz 2B.5 — İkinci Düzeltme)

Bu rapor, önceki sürümün (`6e6fd9a`) Faz 2B.5 ön incelemesinin tekrar kabul edilmemesi üzerine,
resmî GİB'in asıl UBL-TR1.2.1 paketi de dahil edilerek yeniden hazırlanmıştır. Önceki iki rapor
(`21a81b5`, `6e6fd9a`) yalnız e-Fatura paketi, e-Arşiv raporlama paketi ve UBL-TR kılavuz paketini
incelemiş; GİB'in ayrıca yayımladığı asıl `UBL-TR1.2.1_Paketi.zip`'i atlamıştı. Bu tur için o paket
de dahil olmak üzere aşağıdaki kaynaklar bizzat indirilip incelenmiştir:

- `https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/UBL-TR1.2.1_Paketi.zip` (yeni indirildi)
- `https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/UserList_(Kullanici_Listeleri)_Kilavuzu_V.1.0.pdf`
- `https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-Arsiv_Teknik_Kilavuzu_V.1.18.pdf`
- `UBL-TR Kod Listeleri - V 1.43.pdf` (PDF metni tam çıkarılıp tablo hizalaması doğrulandı)
- `e-FaturaPaketi.zip`, `earsiv_paket_v1.1_8.zip`, `UBLTR_1.2.1_Kilavuzlar.zip` (önceki turlardan)

Önceki iki raporun "asıl XSD bulunamadı" sonucu **yanlıştı** — bu paket incelenmeden verilmiş bir
sonuçtu. `UBL-TR1.2.1_Paketi.zip` içinde asıl UBL gövde şemaları (`UBL-Invoice-2.1.xsd`,
`UBL-CommonAggregateComponents-2.1.xsd`, `UBL-CommonBasicComponents-2.1.xsd`) fiilen mevcuttur ve
bu rapor artık bunlara dayanmaktadır.

## 1. İncelenen GİB kaynakları ve sürümleri

**`UBL-TR1.2.1_Paketi.zip`** (yeni indirilen asıl paket) içeriği:

- `xsdrt/maindoc/UBL-Invoice-2.1.xsd`, `UBL-CreditNote-2.1.xsd`, `UBL-DespatchAdvice-2.1.xsd`,
  `UBL-ApplicationResponse-2.1.xsd`, `UBL-ReceiptAdvice-2.1.xsd`
- `xsdrt/common/UBL-CommonAggregateComponents-2.1.xsd` (3296 satır — `PartyType`, `AddressType`,
  `PersonType`, `TaxTotalType`, `BillingReferenceType`, `MonetaryTotalType`, `InvoiceLineType`
  burada tanımlı), `UBL-CommonBasicComponents-2.1.xsd`, `UBL-CommonExtensionComponents-2.1.xsd`,
  `UBL-CoreComponentParameters-2.1.xsd`, `UBL-QualifiedDataTypes-2.1.xsd`,
  `UBL-UnqualifiedDataTypes-2.1.xsd`, `UBL-XAdESv132/v141-2.1.xsd`, `UBL-xmldsig-core-schema-2.1.xsd`
- `xml/` altında 40'tan fazla senaryo örneği: `TemelFaturaOrnegi.xml`, `TicariFaturaOrnegi.xml`,
  `IadeFaturasiOrnegi.xml`, `TEVKIFAT.xml`, `OTV.xml`, `OZELMATRAH.xml`, `ISTISNA-1.xml`,
  `ISTISNA-2.xml`, `IHRACAT.xml`, `SARJ.xml`, `SARJANLIK.xml`, `YOLCUBERABER.xml`, `YTB_*`
  (Yatırım Teşvik) varyantları, `IDIS_Fatura.xml`, `HKS-Ornek1/2.xml`, `HASTANE.xml`,
  `Irsaliye-*.xml`, `IrsaliyeYaniti-*.xml`, XSLT'ler (`general.xslt`, `irsaliye.xslt`,
  `appResponse.xslt`)
- Bu paket **schematron dosyası içermiyor** — schematron kuralları ayrı olarak `e-FaturaPaketi.zip`
  içinde (`schematron/UBL-TR_Main_Schematron.xml`, `UBL-TR_Common_Schematron.xml`,
  `UBL-TR_Codelist.xml`, mtime 27.07.2026, V1.43 ile tutarlı).

**Önemli çapraz-doğrulama bulgusu:** Paket içindeki `IadeFaturasiOrnegi.xml` şu değerleri taşıyor:
`CustomizationID=TR1.2`, `ProfileID=TICARIFATURA`, `InvoiceTypeCode=IADE`, `IssueDate=2009-01-09`.
Bu, e-FaturaPaketi.zip'teki schematron kuralı `InvoiceTypeCodeCheck` (Common_Schematron.xml:172-177)
ile **doğrudan çelişiyor** — o kural `InvoiceTypeCode=IADE` iken `ProfileID`'nin yalnızca
`{TEMELFATURA, EARSIVFATURA, ILAC_TIBBICIHAZ, YATIRIMTESVIK, IDIS, KAMU}` olabileceğini söylüyor,
`TICARIFATURA` bu kümede yok. Örnek dosyanın tarihi (`2009-01-09`) ve içinde `cbc:DocumentTypeCode`
elemanının (schematron'un `IADEInvioceCheck` kuralının şart koştuğu) hiç bulunmaması, bu örneğin
**eski/güncellenmemiş bir demo verisi** olduğunu gösteriyor — `CustomizationID=TR1.2` taşımasına
rağmen. Bu raporun geri kalanında **schematron kuralı otoriter kabul edilmiştir**, örnek XML değil;
ancak bu, iki resmi kaynak arasında çözülmemiş bir tutarsızlıktır ve ürün sahibine açık soru olarak
taşınmalıdır (§10).

**UserList Kılavuzu V1.0 (Nisan 2026):** Tam olarak incelendi (13 sayfa). İçerik: `User`
(Identifier, Title, Type=Kamu/Özel, FirstCreationTime, AccountType, Documents/Alias — e-Fatura/
e-İrsaliye posta kutusu takma adları). **Belgede `ProfileID`, `TEMELFATURA`, `TICARIFATURA` veya
bir "profil" alanı hiç geçmiyor.** Bu, önceki raporun `CariKart.AliciEFaturaProfili`/"GİB mükellef
sorgusu" önerisinin resmi dayanağı olmadığını doğrular (bkz. §3).

**e-Arşiv Teknik Kılavuzu V1.18 (Ağustos 2025):** Tam olarak incelendi. "§6 e-Arşiv Fatura
Standardı" bölümü (satır 2135-2178) şunu açıkça yazıyor: *"Fatura formatı olarak UBL-TR fatura
formatı genel olarak kullanılacak yöntemdir. UBL-TR olarak hazırlanan faturalarda ProfileId alanı
EARSIVFATURA olarak yazılmalıdır."* Ayrıca: *"Fatura formatı olarak özel izinle PDF kullanılıyorsa...
ProfileId alanı EARSIVFATURA olarak yazılmalıdır. CopyIndicator alanı true yazılmalıdır."* — yani
`CopyIndicator=true` yalnızca **özel izinli PDF+ekli-XML** senaryosunda zorunlu, genel elektronik
UBL-TR iletiminde belirtilmiyor (bu durumda `false` esas alınır). Ayrıca aynı kılavuzun web servis
bölümünde (satır 2095-2101), `sendDocumentFile` metodunun gönderdiği `eArsiv.xsd`'ye uygun dosyanın
**"eArsivRaporu belgesi"** olduğu açıkça yazıyor — yani `EArsiv.xsd`, tekil fatura XML'i değil,
**periyodik raporlama** (batch report) şemasıdır. Bu, §2'nin kesin dayanağıdır.

**UBL-TR Kod Listeleri V1.43 (Temmuz 2026):** PDF metni tam çıkarılıp tablo hizalaması kod↔açıklama
eşleşmesi doğrulanarak okundu (bkz. §4, §5).

## 2. Repository'deki mevcut durum

Değişiklik yok (önceki raporlarla aynı): `SatisBelgesi` ticari otorite, `EBelgeSnapshot` immutable
snapshot, `EBelgeCanonicalSnapshotReader` kanonik okuyucu, `SatisBelgesiService.FaturaKesAsync`
kesim akışı.

**e-Arşiv raporu ile e-Arşiv faturası ayrımı (netleştirildi):**

- **Tekil e-Arşiv fatura XML'i:** UBL-TR formatı — aynı `UBL-Invoice-2.1.xsd` şeması,
  `ProfileID=EARSIVFATURA`. e-Fatura ile **aynı XSD/schematron altyapısını** kullanır, yalnızca
  `ProfileID` değeri ve gönderim kanalı farklıdır.
- **e-Arşiv raporu:** `EArsiv.xsd` şemasına uygun, `sendDocumentFile` web servisi ile GİB'e
  gönderilen periyodik özet/rapor — bu, tekil faturanın kendisi değil, kesilen faturaların toplu
  bildirimidir.
- `CopyIndicator=true`, yalnızca özel izinli PDF+ekli-XML senaryosunda zorunlu; genel elektronik
  UBL-TR iletiminde bu şart belirtilmiyor.

Bu ayrım artık resmi kılavuz alıntısıyla kesinleşmiştir: **"e-Fatura ve e-Arşiv faturaları için iki
bağımsız fatura XSD modeli gerekir" sonucu resmi dayanaksızdır ve kaldırılmıştır.** Renderer,
e-Fatura ve e-Arşiv çıktısı için **aynı `UBL-Invoice-2.1.xsd`/schematron temelini** kullanabilir;
fark yalnızca `ProfileID` (ve varsa `CopyIndicator`) seçiminde ortaya çıkar.

## 3. Snapshot → UBL eşleme matrisi

| Snapshot alanı | Hedef UBL elemanı/attribute | Zorunluluk | Dönüşüm/kod listesi | Durum |
| --- | --- | --- | --- | --- |
| — (renderer sabiti) | `cbc:UBLVersionID` | Zorunlu (`UBL-Invoice-2.1.xsd:10`) | Sabit `2.1` | Doğrudan kullanılabilir |
| — (renderer sabiti) | `cbc:CustomizationID` | Zorunlu (`:11`) | Sabit `TR1.2` veya `TR1.2.1` (`CustomizationIDCheck`) | Doğrudan kullanılabilir |
| Belge tipi + (yeni) `EFaturaSenaryosu` | `cbc:ProfileID` | Zorunlu (`:12`) | `ProfileIDCheck`, `InvoiceTypeCodeCheck` | Otoriter kaynak eksik (bkz. §4/§10) |
| `Belge.ResmiFaturaNo` | `cbc:ID` | Zorunlu (`:13`), format `^[A-Z0-9]{3}20YY[0-9]{9}$` (`InvoiceIDCheck`) | 16 hane | Deterministik eşlenebilir |
| — (renderer sabiti) | `cbc:CopyIndicator` | Zorunlu (`:14`) | Sabit `false` (dar kapsamda özel-izinli PDF senaryosu yok) | Doğrudan kullanılabilir |
| `Belge.EBelgeUuid` | `cbc:UUID` | Zorunlu (`:15`) | — | Doğrudan kullanılabilir |
| `Belge.FaturaKesimTarihi`/`BelgeTarihi` | `cbc:IssueDate` | Zorunlu (`:16`) | ISO tarih | Deterministik eşlenebilir |
| `Belge.FaturaKesimTarihi` (saat kısmı) | `cbc:IssueTime` | Opsiyonel (`:17`) | ISO saat | Deterministik eşlenebilir |
| Belge tipi + KDV senaryosu | `cbc:InvoiceTypeCode` | Zorunlu (`:18`) | `InvoiceTypeCodeCheck` | Deterministik eşlenebilir (dar kapsamda sabit `SATIS`) |
| `Odeme.ParaBirimi` | `cbc:DocumentCurrencyCode` | Zorunlu (`:20`) | ISO 4217 | Deterministik eşlenebilir (dar kapsamda sabit `TRY`) |
| Satır listesi | `cbc:LineCountNumeric` | Zorunlu (`:26`) | Snapshot'ta hazır alan **değil** — satır sayısından deterministik hesaplanır | Deterministik eşlenebilir |
| — (dar kapsamda yok) | `cac:OrderReference` | Opsiyonel (`:28`) | — | İlk sürümde destek dışı bırakılmalı |
| `Iade.*` | `cac:BillingReference/cac:InvoiceDocumentReference` | Opsiyonel, `InvoiceTypeCode=IADE` iken zorunlu (`:29`, `BillingReferenceType`) | 16 hane `ID` + `DocumentTypeCode='IADE'` | İlk sürümde destek dışı bırakılmalı (dar kapsamda iade yok) |
| — | `cac:Signature` | Zorunlu, en az 1 (`:35`) | `SignatureCheck` (`schemeID='VKN_TCKN'`) | Renderer/imza altyapısı sorumluluğu — bu hazırlık fazının kapsamı dışında ayrı bir imzalama fazı gerektirir |
| `Kurum.*` | `cac:AccountingSupplierParty` | Zorunlu (`:36`) | `SupplierPartyType` → `cac:Party` | Doğrudan kullanılabilir (kapsayıcı eleman) |
| `Alici.*` | `cac:AccountingCustomerParty` | Zorunlu (`:37`) | `CustomerPartyType` → `cac:Party` | Doğrudan kullanılabilir (kapsayıcı eleman) |
| `Kurum.VergiNo`/`Alici.MusteriVergiNo`/`MusteriTcKimlikNo` | Her bir `cac:PartyIdentification` | Zorunlu, unbounded (`PartyType`, `CAC.xsd:2135`) | `schemeID`∈{VKN,TCKN,...} (28 değer), `PartyIdentificationTCKNVKNCheck` | Deterministik eşlenebilir |
| `Kurum.KurumUnvani`/`Alici.MusteriUnvan` | `cac:PartyName/cbc:Name` | Opsiyonel XSD'de (`:2136`), `schemeID=VKN` iken schematron'a göre zorunlu | — | Deterministik eşlenebilir |
| `Kurum.Adres`/`Alici.MusteriAdres` (yapısı) | `cac:PostalAddress` | **Zorunlu** (`PartyType:2137`, minOccurs yok) | `AddressType` — `CitySubdivisionName` ve `CityName` zorunlu, `Country` zorunlu, `StreetName`/`BuildingNumber`/`PostalZone`/`Region`/`District` opsiyonel; **serbest metin `AddressLine/Line` bu tipte hiç yok** | Otoriter kaynak eksik — mevcut tek-string `Adres` alanı bu yapıyı dolduramaz (bkz. §6) |
| `Kurum.VergiDairesi`/`Alici.MusteriVergiDairesi` | `cac:PartyTaxScheme/cac:TaxScheme/cbc:Name` | Opsiyonel XSD'de (`:2139`); yalnız `ProfileID=IHRACAT` iken schematron'a göre zorunlu | — | Dar kapsamda (ihracat yok) doğrudan kullanılabilir; ihracat kapsamı ayrıca destek dışı |
| `Alici.MusteriTcKimlikNo` + gerçek kişi | `cac:Person` | Opsiyonel XSD'de (`:2142`); `schemeID=TCKN` iken zorunlu | `PersonType`: `FirstName` zorunlu, `FamilyName` zorunlu | Otoriter kaynak eksik — `MusteriAdSoyad` tek string bölünmemeli (bkz. §6) |
| — (dar kapsamda yok) | `cac:PaymentMeans` | Opsiyonel (`:42`) | — | İlk sürümde destek dışı bırakılmalı |
| `Odeme.VadeTarihi` | `cac:PaymentTerms/cbc:PaymentDueDate` | Opsiyonel (`:43`) | — | Doğrudan kullanılabilir (null ise hiç eklenmez) |
| `Satir.IndirimTutari` | `cac:AllowanceCharge` | Opsiyonel, unbounded (`:44`) | `ChargeIndicator=false`, `Amount` | Deterministik eşlenebilir |
| Belge toplamları | `cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory` | `TaxTotal` zorunlu (`:49`), `TaxSubtotal` en az 1 (`TaxTotalType`) | `TaxTypeCheck`, `$TaxType` | Deterministik eşlenebilir (dar kapsamda tek kod `0015`) |
| — (`cac:TaxCategory/cac:TaxScheme`) | `TaxScheme/TaxTypeCode` | Opsiyonel XSD'de (`TaxSchemeType`), pratikte zorunlu | `0015`=Gerçek Usulde KDV (V1.43 Kod Listeleri, §1.9, sayfa 12-13) | Deterministik eşlenebilir |
| — (dar kapsamda yok) | `cac:WithholdingTaxTotal` | Opsiyonel (`:50`) | Tevkifat kod+oran listesi | İlk sürümde destek dışı bırakılmalı |
| `ToplamMatrah`/`ToplamKdv`/`GenelToplam` | `cac:LegalMonetaryTotal` | Zorunlu (`:51`); `LineExtensionAmount`, `TaxExclusiveAmount`, `TaxInclusiveAmount`, `PayableAmount` zorunlu alt elemanlar (`MonetaryTotalType`) | Satır toplamlarından hesaplanır | Deterministik eşlenebilir |
| Satır listesi | `cac:InvoiceLine` | Zorunlu, en az 1 (`:52`) | — | Doğrudan kullanılabilir (kapsayıcı eleman) |
| `Satir.Miktar` + (yeni) `BirimKodu` | `cbc:InvoicedQuantity/@unitCode` | Zorunlu attribute (`InvoicedQuantityCheck`) | `UnitCodeList`; **`Adet`→`C62` doğrulandı** (§5) | Deterministik eşlenebilir yalnız "Adet" için; diğer birimler otoriter kaynak eksik |
| `Satir.BirimFiyat` | `cac:Price/cbc:PriceAmount` | Zorunlu (`PriceType`) | — | Deterministik eşlenebilir |
| `Satir.Aciklama` | `cac:Item/cbc:Name` | Zorunlu (`ItemType`) | — | Deterministik eşlenebilir |
| — | XSLT/imza/karekod `AdditionalDocumentReference` | Opsiyonel, unbounded (`:34`) | — | Renderer/görüntüleme altyapısı sorumluluğu — iş verisi değil, bu hazırlık fazının kapsamı dışında |

## 4. Otoriter kaynak eksikleri

- `ProfileID` seçimi hâlâ belge düzeyinde otoriter bir alana bağlanmamış (bkz. §3, düzeltilmiş
  model).
- Satır birim kodu: yalnızca "Adet" için resmi karşılık (`C62`) doğrulandı; Gün/Saat/Gece/Kişi/
  Porsiyon için V1.43 Kod Listeleri'nde kesin Türkçe karşılık bulunamadı (bkz. §5).
- `MusteriAdSoyad` tek string; `Person/FirstName`+`FamilyName` için ayrı otoriter alan yok.
- `Kurum.Adres`/`Alici.MusteriAdres` tek serbest metin; XSD artık kesin olarak
  `CitySubdivisionName`+`CityName`+`Country` alanlarının yapısal ve zorunlu olduğunu gösteriyor —
  mevcut model bunu karşılamıyor.
- `Kurum.Adres` (yasal adres) ile `Tesis.Adres` (fiziksel tesis) arasında hangi UBL Party'sine
  gideceği hâlâ netleşmemiş.
- ÖTV liste seçimi (I/II/III[A/B/C]/IV) — dar kapsamda gerekmiyor ama ileride gerekecek.
- Tevkifat kodu (601-627/801-825) — dar kapsamda gerekmiyor.
- Konaklama vergisi için ayrı bir `TaxTypeCode` (`0059` gibi) V1.43 Kod Listeleri'nde
  **doğrulanamadı** — belge yalnızca `InvoiceTypeCode=KONAKLAMAVERGISI` ve ayrı bir "Konaklama
  Vergisi İstisna Kodları Listesi" (istisna sebebi, `TaxTypeCode` değil) içeriyor; dar kapsamda
  zaten destek dışı.
- `ProfileID=TICARIFATURA`+`InvoiceTypeCode=IADE` konusunda schematron ile resmi örnek arasındaki
  çelişki (bkz. §1) çözülmeden iade senaryoları modellenmemeli.

## 5. İlk renderer için destek matrisi

| Belge tipi | İlk renderer kapsamı | Gerekçe |
| --- | --- | --- |
| `SatisFaturasi` | Evet | Dar kapsamın tek senaryosu; `InvoiceTypeCode=SATIS` deterministik üretilebilir |
| `AlisIadeFaturasi` | Hayır | `InvoiceTypeCode=IADE` gerektirir; hem profil kısıtı hem schematron/örnek çelişkisi (§1, §3) çözülmeden eklenemez |
| `IadeFaturasi` | Hayır | Aynı gerekçe |
| `SatisIadeFaturasi` | Hayır | Gelen belge; ayrıca iade kapsamı dar sürümde yok |
| `AlisFaturasi` | Hayır | Gelen belge; STYS sadece tüketir |
| `Proforma`, `FaturaTaslagi` | Hayır | Resmi e-belge değil |

İlk renderer kapsamı kesin olarak:

- `SatisBelgesiTipi.SatisFaturasi`
- `ParaBirimi=TRY`, `Kur=1`
- Tüm satırlar `KdvUygulamaTipi.Kdvli`
- Yalnız standart KDV (`TaxTypeCode=0015`)
- Tevkifat yok, istisna yok, ÖTV yok, ÖİV yok, konaklama vergisi yok, iade yok, özel matrah yok,
  ihracat yok

## 6. Snapshot V1/V2 kararı

- Mevcut `EBelgeSnapshot` V1 immutable kanonik snapshot olarak korunur; V1 kayıtlarının JSON/hash
  şekli **değiştirilmez**.
- Dar kapsam dahi V1'de bulunmayan alanlar gerektiriyor (`EFaturaSenaryosu`, `BirimKodu`, yapısal
  adres alanları, `Ad`/`Soyad`); bu nedenle **en dar senaryo bile V2 gerektirir.**
- V1 render edilemeyen eski kayıtlar için ayrı, kalıcı bir hata kodu tanımlanmalı
  (`EBELGE_UBL_RENDER_SNAPSHOT_VERSION_UNSUPPORTED`, HTTP 422); V1 satırları migration/backfill ile
  güncellenmez.
- Ayrı bir "UBL input/yayın snapshot'ı" kavramı önerilmiyor — V2, aynı `EBelgeSnapshot`
  mekanizmasının yeni şema versiyonudur.

**Reader tasarımı (üç seçenekten biri açıkça seçildi):** *Versiyona göre ayrıştırılmış sonuç/
discriminated union* seçildi. Gerekçe: `object`/`dynamic` yasak; ortak bir taban tip (V1 ve V2'nin
alan setleri kökten farklı olduğu için) V1'i yapay şekilde bir arayüze zorlar ve gereksiz bir
soyutlama getirir; ayrı iki tam bağımsız reader ise üst seviyede hangi reader'ın çağrılacağını
belirleyecek bir dispatcher'ı yine gerektirir. Bunun yerine:

- Tek bir `IEBelgeCanonicalSnapshotReader.Oku(talep)` arayüzü korunur, ancak dönüş tipi
  `sealed abstract record EBelgeCanonicalSnapshotSonucu` olur; bunun iki alt tipi vardır:
  `V1Sonucu(EBelgeCanonicalSnapshotV1 Snapshot)` ve `V2Sonucu(EBelgeCanonicalSnapshotV2 Snapshot)`.
- `ValidateTalep`, tek sabit değer yerine desteklenen versiyon kümesine (`{"1","2"}`) bakar; içeride
  versiyona göre ayrı doğrulama/deserialize mantığı çalışır (bu, iki ayrı reader'ın yaptığı işi
  yapar, fakat dışa tek sözleşme sunar).
- Çağıran kod (renderer), `switch` ifadesiyle (derleyici tarafından tüketicilik denetimi yapılan
  `sealed` hiyerarşi) hangi versiyonla çalıştığını açıkça ayırt eder.
- V1'in mevcut alan seti, hash doğrulaması ve `EBelgeCanonicalSnapshotV1` record tipi **hiç
  değiştirilmez** — sadece dışa dönen sonuç bir union'ın parçası haline gelir.

## 7. Önerilen renderer sözleşmesi

Önerilen giriş: immutable snapshot (V1 veya V2, dispatcher'dan), belge tipi, issuance tarihi, etkin
GİB paket/kod listesi versiyonu (tarih bazlı registry'den, yalnızca kural seti seçimi için),
tenant/kurum bağlamı.

Önerilen çıktı: UBL XML, kullanılan `ProfileID`/`InvoiceTypeCode`, SHA-256, validation sonucu,
renderer uyarıları.

Kurallar: canlı DB'den yeniden okuma yapılmaz; snapshot dışında sessiz tamamlama yapılmaz; eksik
UBL metadata açık hata üretir; kod listesi seçimi tarih bazlı registry'den gelir ama iş kararı
(`ProfileID`, `InvoiceTypeCode`, birim kodu) üretmez.

**Kesim öncesi destek-kapsamı kapısı:** `FaturaKesAsync` akışında, otoriter kurum/tesis/belge
okumasından **sonra**, ama sayaç artırılmadan, resmi numara verilmeden, belge durumu
değiştirilmeden, `EBelgeKaydi` oluşturulmadan, snapshot oluşturulmadan ve outbox oluşturulmadan
**önce**, renderer destek-kapsamı doğrulanmalıdır: belge tipi dar kapsam dışında ise, herhangi bir
vergi/tevkifat/istisna alanı doluysa, veya V2'nin zorunlu kıldığı yeni alanlardan
(`EFaturaSenaryosu`, `BirimKodu`, yapısal adres, `Ad`/`Soyad`) biri eksikse, işlem **atomik olarak
HTTP 400 ile reddedilmelidir**. Hiçbir sayaç, resmi numara, snapshot veya outbox yan etkisi
oluşmamalıdır — desteklenmeyen bir belgenin önce resmi numara alıp sonra kalıcı render hatasına
düşmesine izin verilmez.

## 8. Sonraki uygulama fazlarının sırası

1. Belge düzeyi `EFaturaSenaryosu` (Temel/Ticari) alanı + `ProfileID`/`InvoiceTypeCode` resolver
   (dar kapsam: sabit `SATIS`)
2. `BirimKodu` alanı (dar kapsam: yalnız `Adet`→`C62`)
3. Yapısal adres alanları (İlçe/İl/Ülke zorunlu, Sokak/Bina No/Posta Kodu opsiyonel)
4. Gerçek kişi alıcılar için ayrı `Ad`/`Soyad` alanları
5. V1'i bozmadan V2 dispatcher/discriminated-union reader tasarımı
6. Kesim öncesi destek-kapsamı kapısı (`FaturaKesAsync` içinde)
7. UBL XML renderer (dar kapsam)
8. UBL doğrulama / schematron (dar kapsam kuralları)
9. PDF renderer
10. Artifact storage abstraction, e-posta gönderim provider'ı

Tevkifat, ÖTV, ÖİV, konaklama vergisi, iade, özel matrah, ihracat alanları bu sıraya **eklenmedi**
— bunlar destek dışı kaldığı için model değişikliği bu fazda yapılmayacak.

## 9. Faz 2B.5 uygulama promptuna girecek kesin kararlar

- İlk renderer kapsamı kesin olarak: `SatisFaturasi`, TRY, Kur=1, tüm satırlar Kdvli, yalnız
  standart KDV; tevkifat/istisna/ÖTV/ÖİV/konaklama/iade/özel matrah/ihracat yok.
- `ProfileID`, GİB mükellef sorgusundan veya `CariKart`'tan değil, belge düzeyinde otoriter bir
  alandan (`SatisBelgesi.EFaturaSenaryosu` benzeri) gelecek; `EARSIVFATURA` kanaldan
  (`EBelgeKanali.EArsiv`) deterministik türetilecek.
- Nihai `ProfileID`, `InvoiceTypeCode` ve satır `BirimKodu` değerleri kesim anında canonical V2
  snapshot'a yazılacak.
- Kesim öncesi destek-kapsamı kapısı zorunlu: desteklenmeyen tip/vergi/eksik V2 alanı, hiçbir yan
  etki (sayaç/numara/snapshot/outbox) oluşmadan HTTP 400 ile reddedilecek.
- V1 kayıtları değiştirilmeyecek; reader, discriminated-union sonuç tipiyle V1/V2'yi
  ayrıştıracak.
- `MusteriAdSoyad` tahmini bölünmeyecek; ayrı `Ad`/`Soyad` alanları eklenecek.
- Adres, tek serbest metinden yapısal (İlçe/İl/Ülke zorunlu) modele taşınacak.
- `ProfileID=TICARIFATURA`+`InvoiceTypeCode=IADE` çelişkisi çözülmeden hiçbir iade senaryosu
  (dahil `AlisIadeFaturasi`) modellenmeyecek.

## 10. Açık kalan ve ürün sahibinin cevaplaması gereken sorular

- `SatisBelgesi.EFaturaSenaryosu` (Temel/Ticari) alanı hangi iş sürecinde/ne zaman belirlenecek —
  alıcıyla yapılan ticari anlaşmaya mı, yoksa başka bir kritere mi bağlı?
- Resmi schematron kuralı ile resmi `IadeFaturasiOrnegi.xml` örneği arasındaki `TICARIFATURA`+
  `IADE` çelişkisi GİB'e sorulup netleştirilmeli mi?
- Gün/Saat/Gece/Kişi/Porsiyon birimleri için GİB'in ayrıca yayımladığı UN/ECE Rec. 20 tam listesi
  veya Ticaret Bakanlığı ölçü kodları listesi (V1.43 Kod Listeleri'nin atıfta bulunduğu harici
  kaynaklar) STYS tarafından ayrıca temin edilip mi kullanılacak?
- Kurum/Tesis adreslerinin ilçe/il/ülke bilgisi mevcut veri tabanında var mı, yoksa manuel veri
  girişi mi gerekecek?
- Konaklama vergisi, ÖTV, ÖİV, tevkifat senaryoları hangi fazda ele alınacak — bunlar için ayrı bir
  "Faz 2C" mi planlanmalı?
- İlk renderer yalnız e-Fatura mı destekleyecek, e-Arşiv aynı dalgada mı (aynı XSD temeli üzerinden
  yalnızca `ProfileID=EARSIVFATURA` farkıyla) mı gelecek?

## Sonuç

**Mevcut snapshot ile renderer geliştirilemez; renderer öncesinde ek hazırlık fazı gerekir.**

Ancak bu ikinci düzeltmede hazırlık fazının kapsamı önemli ölçüde **daralmıştır**: resmi paket
incelemesi, önceki raporların "otoriter kaynak tamamen eksik" dediği birçok noktayı (Adet→C62,
ÖTV/ÖİV kodları, e-Arşiv/e-Fatura XSD birliği, adres yapısının XSD'de zorunlu olduğu) kesin
biçimde çözmüştür. Geriye kalan, gerçekten zorunlu hazırlık maddeleri:

1. Belge düzeyi `EFaturaSenaryosu`/`ProfileID` alanı (dar kapsamda sabit değer üretilebilir, ama
   alan model olarak eklenmeli).
2. `BirimKodu` alanı (dar kapsamda yalnız `Adet`→`C62` yeterli).
3. Yapısal adres alanları (İlçe/İl/Ülke zorunlu).
4. Gerçek kişi alıcılar için ayrı `Ad`/`Soyad` alanları.
5. Kesim öncesi destek-kapsamı kapısı.
6. V1'i bozmadan V2 discriminated-union reader tasarımı.

Tevkifat, ÖTV, ÖİV, konaklama vergisi ve iade alanları bu fazın modeline **eklenmemiştir** —
bunlar destek dışı bırakıldığı için hazırlık fazı kapsamında değildir.
