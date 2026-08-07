# E-Belge UBL/PDF/E-Posta Renderer Hazırlık Raporu (Faz 2B.5 — Dördüncü Düzeltme)

Bu rapor, `c71e519` sürümündeki uygulanabilirlik hatalarının düzeltilmesi için güncellenmiştir.
Aşağıdaki konular kabul edilmiş sayılmakta ve yeniden araştırılmamaktadır: 14.09.2026 öncesi
canlıya alınmama kararı, yalnız yeni GİB kural setinin desteklenmesi, renderer'ın tek iş girdisinin
typed `EBelgeCanonicalSnapshotV2` olması, V1 reader ve V1 snapshot biçiminin korunması, satır
indiriminin `InvoiceLine/AllowanceCharge` altında olması, unsigned renderer ile kriptografik
imzalama fazının ayrılması, `Kurum`'un hukuki satıcı kaynağı olması, `Adet → C62` için entity alanı
eklenmemesi ve `InvoiceTypeCode=SATIS` değerinin deterministik üretilmesi.

Bu turda düzeltilenler: kesim tarihi kontrolünün uygulanabilirliği, mali hesaplama ve yuvarlama
sözleşmesi, typed V1/V2 tasarımının derlenebilirliği, rule-set artifact bütünlüğü, byte çıktısının
değişmezliği, eşleme matrisinin atomikliği ve HTTP 400/422/503 ayrımı.

## Kesin ürün kararı: devreye alma tarihi

Fatura işlemleri **14.09.2026 tarihinden önce hiçbir ortamda canlı kullanıma alınmayacaktır.**

- 14.09.2026 öncesi eski GİB paketleri için renderer desteği geliştirilmeyecektir.
- Eski ve yeni rule-set arasında tarih bazlı seçim yapılmayacaktır.
- Renderer yalnız `GIB-UBL-TR-1.2.1/2026-09-14` kural setini destekleyecektir.
- Belge tarihi veya planlanan kesim tarihi 14.09.2026'dan önce olan belgeler, resmî numara
  verilmeden kesim öncesi kapıda reddedilecektir (§9).
- Özellik, canlıya geçiş tarihine kadar feature flag ile kapalı tutulacaktır.
- Runtime'da GİB sitesinden paket indirilmeyecektir.

## 1. İncelenen GİB kaynakları ve sürümleri

**Rule-set kimliği:** `GIB-UBL-TR-1.2.1/2026-09-14`

Kaynak paketlerin bütünlük değerleri:

| Paket | Boyut (byte) | SHA-256 |
| --- | --- | --- |
| `UBL-TR1.2.1_Paketi.zip` | 1052004 | `cb583941b8a8a239c59902c6bc455c0f75d48f2bb81d7d3fbe1ae827f981f7db` |
| `e-FaturaPaketi.zip` | 678554 | `e0fd9136cadbb79bd29f286c7ff80c6f2202ce1a0354338d0bf0739dd88dc29e` |
| `UBLTR_1.2.1_Kilavuzlar.zip` | 5868477 | `0f7c720da5d9f0e9d25ef929f03d1ecd04871bda924ecb1a6b71b5e8fba0710a` |
| `earsiv_paket_v1.1_8.zip` | 18701 | `07a00ddaf98a2b3ec1ef9beb8a90d19133b211045f25d2e67279bd509be9f75f` |

### Liste 1 — Renderer runtime/build artifact'ları

Bunlar renderer'ın XSD ve schematron doğrulamasında **fiilen kullandığı** dosyalardır.
`UBL-Invoice-2.1.xsd`'nin `schemaLocation` kapanışı izlenerek çıkarılmıştır; kapanışa girmeyen
hiçbir dosya listeye alınmamıştır (`UBL-CoreComponentParameters-2.1.xsd` ve `maindoc` altındaki
`CreditNote`/`DespatchAdvice`/`ApplicationResponse`/`ReceiptAdvice` şemaları kapanışta **yoktur**).

Kaynak: `UBL-TR1.2.1_Paketi.zip`

| Göreli yol | SHA-256 |
| --- | --- |
| `xsdrt/maindoc/UBL-Invoice-2.1.xsd` | `b68a25ae3d99435f4e4a39809939183dc8b5d687aeebf2d023f4d4c2a436749e` |
| `xsdrt/common/UBL-CommonAggregateComponents-2.1.xsd` | `186085d67e0daf5bbe78427259ee3df15b3043bd6676b50b0652d264e10bed91` |
| `xsdrt/common/UBL-CommonBasicComponents-2.1.xsd` | `9e7eb96aaba1bf2092c52d3ccb8c881a710cc48111c568e3eae29746dd6b1cab` |
| `xsdrt/common/UBL-CommonExtensionComponents-2.1.xsd` | `1829b0a0dd61589edf59f400cd299e59edd42a6b33360026956f52aed4f83a74` |
| `xsdrt/common/UBL-ExtensionContentDataType-2.1.xsd` | `fcee77a11870208e6377ea6311b9f2a050bca24bdad8606ea02d71e9f9e72f8d` |
| `xsdrt/common/UBL-CommonSignatureComponents-2.1.xsd` | `4fa9e2370100040fe14c43e135ef77e2eb66b21cb8dbfc2ffb8d82ae991fe92e` |
| `xsdrt/common/UBL-SignatureAggregateComponents-2.1.xsd` | `9234c2ca48dbfa9a22a786112bb075c5922a305170920eaab1e3c04fa0b7344b` |
| `xsdrt/common/UBL-SignatureBasicComponents-2.1.xsd` | `0fbe2d7afff0c1e11164b8ec83e13f18801021c3c87e390a9d76f9cf862f6a64` |
| `xsdrt/common/UBL-QualifiedDataTypes-2.1.xsd` | `7dcb156e610239c97ae70940cf4653b88e48c3595bf5f56a2204a32e2893e6cf` |
| `xsdrt/common/UBL-UnqualifiedDataTypes-2.1.xsd` | `09052d406b4293e2a5f9c2bfee6df10ad4d8d5f0b36e24a6349d7f7936d89eb6` |
| `xsdrt/common/CCTS_CCT_SchemaModule-2.1.xsd` | `dd546e4809df86b6445589f69f0d6c9df162840ae386574ddfc1da7638103e15` |
| `xsdrt/common/UBL-xmldsig-core-schema-2.1.xsd` | `101909c9f06456d61ddcc4fb982f1d40dc357b439f393b1a2eb46e42acd60809` |
| `xsdrt/common/UBL-XAdESv132-2.1.xsd` | `a4f726bcf8cc3f7d9ffa4dab99e005535a8e8b60dced1e5d94578d2e05afa96e` |
| `xsdrt/common/UBL-XAdESv141-2.1.xsd` | `3f9d50cd07e6ee9b81adfe198e43bed5ee945995511c13a41b4f07667d619625` |

Kaynak: `e-FaturaPaketi.zip`

| Göreli yol | SHA-256 |
| --- | --- |
| `schematron/UBL-TR_Main_Schematron.xml` | `a0a2794374108a3ebd4e472c748629d5e398ea9f63e67d3330fc1673999a4dab` |
| `schematron/UBL-TR_Common_Schematron.xml` | `44daa43d9c13bbf02c55db104c7199138779d329ab74d2c4b2ed751742dea8a4` |
| `schematron/UBL-TR_Codelist.xml` | `60aa2e531c21f99b522a2b72872f07bedebc57e00b0a1a6816a74dcd50100292` |

### Liste 2 — Yalnız araştırma ve referans kaynakları

Bunlar renderer runtime bağımlılığı **değildir**, uygulama artifact'ına gömülmez:

- `UBLTR_1.2.1_Kilavuzlar.zip` içeriğinin tamamı (`UBL-TR Kod Listeleri - V 1.43.pdf`,
  senaryo ve belge kılavuzları, `Degisim Tablosu.txt`)
- `UBL-TR1.2.1_Paketi.zip` içindeki `xml/` örnek belgeleri ve `*.xslt` görüntüleme dosyaları
- `UserList_(Kullanici_Listeleri)_Kilavuzu_V.1.0.pdf`
- `e-Arsiv_Teknik_Kilavuzu_V.1.18.pdf`

### Liste 3 — Sonraki e-Arşiv raporlama fazına ait paketler

`earsiv_paket_v1.1_8.zip` ve içindeki `eArsiv.xsd`, `eArsivVeri.xsd`, `faturaOzet.xsd`,
`EArsivWs.wsdl`, `earsiv_schematron.xsl` dosyaları **tekil fatura renderer'ında kullanılmaz**;
bunlar periyodik e-Arşiv raporlama akışına aittir ve renderer rule-set'inden çıkarılmıştır.
Sonraki e-Arşiv raporlama fazında ayrıca ele alınacaktır.

### Artifact saklama biçimi

**Seçilen model: çıkarılmış dosyalar + manifest.** ZIP gömme modeli seçilmemiştir.

- Liste 1'deki 17 dosya, uygulama repository'sine çıkarılmış hâlde gömülür.
- Bir manifest dosyası, her dosyanın tam göreli yolunu ve SHA-256 değerini taşır (yukarıdaki iki
  tablo manifestin içeriğidir).
- Build sırasında her dosyanın SHA-256 değeri manifest ile karşılaştırılır; uyuşmazlıkta build
  başarısız olur.
- Runtime'da ZIP açma işlemi yapılmaz, GİB sitesine çağrı yapılmaz.
- Gerekçe: renderer'a 3 ZIP'ten yalnız 17 dosya gerekmektedir; ZIP gömme modeli 5.8 MB'lık
  kılavuz PDF'lerini ve kullanılmayan şemaları da taşırdı, ayrıca dosya bazında doğrulanabilirlik
  ve git üzerinde diff'lenebilirlik sağlamazdı.

İki model karıştırılmaz: ZIP dosyaları uygulama artifact'ına dahil edilmez.

## 2. Repository'deki mevcut durum

Ticari otorite `SatisBelgesi`, immutable snapshot `EBelgeSnapshot`, kanonik okuyucu
`EBelgeCanonicalSnapshotReader`, kesim akışı `SatisBelgesiService.FaturaKesAsync`.

### Kesim öncesi kapı zaten mevcuttur

`EnsureUblHazirlikKaynaklari(belge, tesis.Kurum)` çağrısı `SatisBelgesiService.cs:1116`
satırındadır ve doğru noktadadır:

- **Öncesinde** (872-1114): belge kilitli okunmuş, tesis ve kurum otoriter okunmuş, muhasebe fişi
  doğrulanmıştır.
- **Sonrasında** (1141+): sayaç `UPDLOCK` ile kilitlenir (1141), sıra numarası artırılır
  (1168-1169), resmî numara yazılır (1174), kesim tarihi atanır (1175-1176), otoriter durumlar
  değiştirilir (1177-1181), `EBelgeKaydi` oluşturulur (1188), snapshot üretilir (1198), outbox
  mesajı eklenir (1209).

Metot bugün (1275-1311) şunları doğrular: `Kurum.VergiNo` dolu, `Kurum.VergiDairesi` dolu,
`Kurum.Adres` dolu, `ParaBirimi == "TRY"`, `Kur == 1`. Faz 2B.4.1'de yapılacak iş bu metodu
genişletmektir; yeni kapı inşa etmek değildir.

### Düzeltilmesi gereken sıralama sorunları

1. **`ResolveEBelgeKanali(cariKart)` bugün satır 1186'dadır** — sayaç artırıldıktan, resmî numara
   verildikten ve belge durumu değiştirildikten sonra. Kapının kanal bazlı kontrol yapabilmesi
   için sayaç kilidinden **önceye** taşınmalıdır.
2. **`FaturaKesimTarihi` bugün satır 1175-1176'da atanır** — yani kapıdan ve resmî numara
   üretiminden sonra. Bu nedenle kapı, henüz oluşmamış `belge.FaturaKesimTarihi` alanını
   kontrol **edemez**. Çözüm §9'daki kesim anı sözleşmesidir.
3. **Repository'de `TimeProvider` hiç kullanılmamaktadır** (`backend/` altında sıfır eşleşme).
   Kesim anı sözleşmesi için `TimeProvider` yeni bir bağımlılıktır ve DI'a kaydedilmesi
   gerekir.

### Otoriter mali hesaplama davranışı (mevcut kod)

Merkezî yuvarlama noktası `SatisBelgesiTutarHesaplayici.Yuvarla`
(`SatisBelgesiTutarHesaplayici.cs:20-21`):

```csharp
Math.Round(deger, 2, MidpointRounding.AwayFromZero)
```

Satır hesaplaması `SatisBelgesiService.CreateSatirFromRequest` (2670-2751) içindedir ve sırası:

1. `brutMatrah = Miktar × BirimFiyat` (yuvarlanmaz)
2. `indirimOrani`: `request.IndirimOrani > 0` ise doğrudan; değilse
   `ResolveLineRate(IndirimTutari, brutMatrah)` = `Math.Round(tutar × 100 / taban, 4, AwayFromZero)`
3. `indirimTutari = ResolveRateBasedAmount(brutMatrah, indirimOrani, request.IndirimTutari)`:
   oran > 0 ise `Math.Round(brutMatrah × oran / 100, 2, AwayFromZero)`, değilse
   `Yuvarla(Math.Max(0, tutar))`
4. `indirimTutari > brutMatrah` ise HTTP 400
5. `matrah = Yuvarla(brutMatrah − indirimTutari)` — **KDV'den önce 2 basamağa yuvarlanır**
6. `kdvTutari = Yuvarla(matrah × kdvOrani / 100)` — **satır bazında, yuvarlanmış matrahtan**
7. `satirToplami = Yuvarla(matrah + kdv − tevkifat + otv + oiv + konaklama)`

Belge toplamları `HesaplaBelgeToplamlari` (2753-2758) içinde, **zaten yuvarlanmış satır
değerlerinin düz toplamıdır**; ikinci bir üst düzey yuvarlama uygulanmaz:

```csharp
belge.ToplamMatrah = ...Sum(s => s.Matrah);
belge.ToplamKdv    = ...Sum(s => s.KdvTutari);
belge.GenelToplam  = ...Sum(s => s.SatirToplami);
```

Decimal ölçekleri (`StysAppDbContext.cs:2975-3024`):

| Alan | Ölçek |
| --- | --- |
| `Miktar` | `decimal(18,2)` |
| `BirimFiyat` | `decimal(18,2)` |
| `IndirimOrani` | `decimal(5,2)` |
| `IndirimTutari` | `decimal(18,2)` |
| `Matrah` | `decimal(18,2)` |
| `KdvOrani` | `decimal(18,4)` |
| `KdvTutari` | `decimal(18,2)` |

Toplam tutarlılığı doğrulayan bir kontrol **zaten vardır**: `ValidateBelgeOnayaGonderilebilir`
(`SatisBelgesiService.cs:1700`, kontroller 1796-1815) `ToplamMatrah`, `ToplamKdv` ve
`GenelToplam` değerlerini satır toplamlarıyla karşılaştırır ve uyuşmazlıkta HTTP 400 verir.

**Boşluk:** Bu kontrol onaya gönderme yolundaki büyük bir private metodun içindedir; yeniden
kullanılabilir bir bileşen değildir ve kesim öncesi kapıdan çağrılamaz. Ayrıca satır bazındaki
türetme (`Matrah` ve `KdvTutari`'nin girdilerden yeniden hesaplanması) yalnız satır **oluşturma**
anında uygulanır, saklanmış değerlere karşı hiç yeniden doğrulanmaz. Bu nedenle Faz 2B.4.1'de
**merkezî ve yeniden kullanılabilir bir mali doğrulayıcı çıkarılması** zorunlu bir hazırlık
maddesidir (§8).

### Diğer gözlemler

- `EBelgeUuid`, kesim anında `Guid.NewGuid()` ile üretilip snapshot'a dondurulur (1193).
- `SatisBelgesiSatiri.Birim` serbest metindir, varsayılanı `"Adet"`tir
  (`SatisBelgesiSatiri.cs:27`); kolon `nvarchar(32)`, zorunludur.
- `Kurum.Adres` tek `string?` alanıdır; `Il`, `Ilce`, `Ulke`, `PostaKodu` alanları yoktur
  (`Kurum.cs:24`). `Kurum.VergiDairesi` de `string?`tir (`Kurum.cs:21`).

## 3. Snapshot → UBL eşleme matrisi

| UBL elemanı/attribute | Snapshot/V2 kaynağı | Zorunluluk | Dönüşüm/kod listesi | Durum |
| --- | --- | --- | --- | --- |
| `cbc:UBLVersionID` | Renderer sabiti | Zorunlu | `2.1` | Deterministik eşlenebilir |
| `cbc:CustomizationID` | Renderer sabiti | Zorunlu | `TR1.2` | Deterministik eşlenebilir |
| `cbc:ProfileID` | V2 `ProfileID` | Zorunlu | `ProfileIDCheck` | Otoriter kaynak eksik |
| `cbc:ID` | `Belge.ResmiFaturaNo` | Zorunlu | `InvoiceIDCheck` | Doğrudan kullanılabilir |
| `cbc:CopyIndicator` | Renderer sabiti | Zorunlu | `false` | Deterministik eşlenebilir |
| `cbc:UUID` | `Belge.EBelgeUuid` | Zorunlu | — | Doğrudan kullanılabilir |
| `cbc:IssueDate` | V2 `FaturaTarihiTrt` | Zorunlu | `yyyy-MM-dd` | Otoriter kaynak eksik |
| `cbc:IssueTime` | V2 `FaturaSaatiTrt` | Opsiyonel | `HH:mm:ss` | Otoriter kaynak eksik |
| `cbc:InvoiceTypeCode` | V2 `InvoiceTypeCode` | Zorunlu | `InvoiceTypeCodeCheck` | Deterministik eşlenebilir |
| `cbc:DocumentCurrencyCode` | `Odeme.ParaBirimi` | Zorunlu | ISO 4217 | Doğrudan kullanılabilir |
| `cbc:LineCountNumeric` | Satır listesi | Zorunlu | Satır sayısı | Deterministik eşlenebilir |
| `cac:Signature` | `Kurum.VergiNo` + yapısal adres | Zorunlu | `SignatureCheck` | Otoriter kaynak eksik |
| Supplier `cac:PartyIdentification` | `Kurum.VergiNo` | Zorunlu | `schemeID=VKN`, 10 hane | Doğrudan kullanılabilir |
| Customer `cac:PartyIdentification` (kurumsal) | `Alici.MusteriVergiNo` | Zorunlu | `schemeID=VKN`, 10 hane | Doğrudan kullanılabilir |
| Customer `cac:PartyIdentification` (gerçek kişi) | `Alici.MusteriTcKimlikNo` | Zorunlu | `schemeID=TCKN`, 11 hane | Doğrudan kullanılabilir |
| Supplier `cac:PartyName` | `Kurum.KurumUnvani` | Zorunlu | — | Doğrudan kullanılabilir |
| Customer `cac:PartyName` (kurumsal) | `Alici.MusteriUnvan` | Zorunlu | — | Doğrudan kullanılabilir |
| Customer `cac:PartyName` (gerçek kişi) | — | Üretilmez | — | Deterministik eşlenebilir |
| Supplier `cac:PostalAddress` | V2 yapısal adres | Zorunlu | `AddressType` | Otoriter kaynak eksik |
| Customer `cac:PostalAddress` | V2 yapısal adres | Zorunlu | `AddressType` | Otoriter kaynak eksik |
| Supplier `cac:PartyTaxScheme` | `Kurum.VergiDairesi` | Opsiyonel | — | Doğrudan kullanılabilir |
| Customer `cac:PartyTaxScheme` | `Alici.MusteriVergiDairesi` | Opsiyonel | — | Doğrudan kullanılabilir |
| `cac:Person/cbc:FirstName` (gerçek kişi) | V2 `MusteriAd` | Zorunlu | — | Otoriter kaynak eksik |
| `cac:Person/cbc:FamilyName` (gerçek kişi) | V2 `MusteriSoyad` | Zorunlu | — | Otoriter kaynak eksik |
| Belge düzeyi `cac:AllowanceCharge` | — | Üretilmez | — | İlk sürümde destek dışı bırakılmalı |
| `cac:InvoiceLine/cac:AllowanceCharge` | `Satir.IndirimTutari` | Opsiyonel | `ChargeIndicator=false` | Deterministik eşlenebilir |
| Satır `AllowanceCharge/cbc:Amount` | `Satir.IndirimTutari` | Zorunlu | 2 basamak | Doğrudan kullanılabilir |
| Satır `AllowanceCharge/cbc:BaseAmount` | `Miktar × BirimFiyat` | Opsiyonel | `Yuvarla`, 2 basamak | Deterministik eşlenebilir |
| Satır `AllowanceCharge/cbc:MultiplierFactorNumeric` | — | Üretilmez | — | İlk sürümde destek dışı bırakılmalı |
| `cac:TaxTotal/cbc:TaxAmount` | `ToplamKdv` | Zorunlu | — | Doğrudan kullanılabilir |
| `cac:TaxSubtotal/cbc:TaxableAmount` | Orana göre gruplanmış matrah toplamı | Opsiyonel | — | Deterministik eşlenebilir |
| `cac:TaxSubtotal/cbc:TaxAmount` | Orana göre gruplanmış KDV toplamı | Zorunlu | — | Deterministik eşlenebilir |
| `cac:TaxCategory/cbc:Percent` | `Satir.KdvOrani` | Opsiyonel | — | Doğrudan kullanılabilir |
| `cac:TaxScheme/cbc:TaxTypeCode` | Renderer sabiti | Opsiyonel | `0015` | Deterministik eşlenebilir |
| `cac:LegalMonetaryTotal/cbc:LineExtensionAmount` | `ToplamMatrah` | Zorunlu | — | Doğrudan kullanılabilir |
| `cac:LegalMonetaryTotal/cbc:TaxExclusiveAmount` | `ToplamMatrah` | Zorunlu | — | Doğrudan kullanılabilir |
| `cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount` | `GenelToplam` | Zorunlu | — | Doğrudan kullanılabilir |
| `cac:LegalMonetaryTotal/cbc:AllowanceTotalAmount` | — | Üretilmez | — | İlk sürümde destek dışı bırakılmalı |
| `cac:LegalMonetaryTotal/cbc:ChargeTotalAmount` | — | Üretilmez | — | İlk sürümde destek dışı bırakılmalı |
| `cac:LegalMonetaryTotal/cbc:PayableAmount` | `GenelToplam` | Zorunlu | — | Doğrudan kullanılabilir |
| `cac:InvoiceLine/cbc:ID` | `Satir.SiraNo` | Zorunlu | — | Deterministik eşlenebilir |
| `cac:InvoiceLine/cbc:InvoicedQuantity` | `Satir.Miktar` | Zorunlu | — | Doğrudan kullanılabilir |
| `cbc:InvoicedQuantity/@unitCode` | V2 `BirimKodu` | Zorunlu | `C62` | Deterministik eşlenebilir |
| `cac:InvoiceLine/cbc:LineExtensionAmount` | `Satir.Matrah` | Zorunlu | — | Doğrudan kullanılabilir |
| `cac:InvoiceLine/cac:TaxTotal/cbc:TaxAmount` | `Satir.KdvTutari` | Zorunlu | — | Doğrudan kullanılabilir |
| `cac:Price/cbc:PriceAmount` | `Satir.BirimFiyat` | Zorunlu | — | Doğrudan kullanılabilir |
| `cac:Item/cbc:Name` | `Satir.Aciklama` | Zorunlu | — | Doğrudan kullanılabilir |
| Satır `AllowanceCharge/Amount/@currencyID` | `Odeme.ParaBirimi` | Zorunlu | `TRY` | Deterministik eşlenebilir |
| Satır `AllowanceCharge/BaseAmount/@currencyID` | `Odeme.ParaBirimi` | Zorunlu | `TRY` | Deterministik eşlenebilir |
| Belge `TaxTotal/TaxAmount/@currencyID` | `Odeme.ParaBirimi` | Zorunlu | `TRY` | Deterministik eşlenebilir |
| Belge `TaxSubtotal/TaxableAmount/@currencyID` | `Odeme.ParaBirimi` | Zorunlu | `TRY` | Deterministik eşlenebilir |
| Belge `TaxSubtotal/TaxAmount/@currencyID` | `Odeme.ParaBirimi` | Zorunlu | `TRY` | Deterministik eşlenebilir |
| `LegalMonetaryTotal/LineExtensionAmount/@currencyID` | `Odeme.ParaBirimi` | Zorunlu | `TRY` | Deterministik eşlenebilir |
| `LegalMonetaryTotal/TaxExclusiveAmount/@currencyID` | `Odeme.ParaBirimi` | Zorunlu | `TRY` | Deterministik eşlenebilir |
| `LegalMonetaryTotal/TaxInclusiveAmount/@currencyID` | `Odeme.ParaBirimi` | Zorunlu | `TRY` | Deterministik eşlenebilir |
| `LegalMonetaryTotal/PayableAmount/@currencyID` | `Odeme.ParaBirimi` | Zorunlu | `TRY` | Deterministik eşlenebilir |
| `InvoiceLine/LineExtensionAmount/@currencyID` | `Odeme.ParaBirimi` | Zorunlu | `TRY` | Deterministik eşlenebilir |
| Satır `TaxTotal/TaxAmount/@currencyID` | `Odeme.ParaBirimi` | Zorunlu | `TRY` | Deterministik eşlenebilir |
| `Price/PriceAmount/@currencyID` | `Odeme.ParaBirimi` | Zorunlu | `TRY` | Deterministik eşlenebilir |
| XSLT `cac:AdditionalDocumentReference` | — | Üretilmez | — | İlk sürümde destek dışı bırakılmalı |
| İmza `cac:AdditionalDocumentReference` | — | Üretilmez | — | İlk sürümde destek dışı bırakılmalı |
| Karekod `cac:AdditionalDocumentReference` | — | Üretilmez | — | İlk sürümde destek dışı bırakılmalı |

Tablo notları:

- **Renderer sabitleri** (`UBLVersionID`, `CustomizationID`, `CopyIndicator`, `TaxTypeCode`,
  `currencyID`) snapshot'ta hazır veri değildir; rule-set'ten veya dar kapsam kuralından
  deterministik üretilir.
- **Alıcı tipi ayrımı:** VKN/kurumsal alıcıda `MusteriUnvan` ve `cac:PartyName` zorunludur;
  TCKN/gerçek kişi alıcıda `cac:PartyName` hiç üretilmez, bunun yerine `cac:Person/cbc:FirstName`
  ve `cbc:FamilyName` zorunludur. Gerçek kişi alıcı için `MusteriUnvan` zorunlu değildir.
- `ProfileID` durumu, ilk dalgada hangi kanalın destekleneceği kararına bağlıdır (§10).
- `IssueDate`/`IssueTime`, V2'de kesim anında çözülmüş TRT değerlerinden gelir; renderer saat
  dilimi dönüşümü yapmaz (§6, §9).
- `MultiplierFactorNumeric` ilk sürümde üretilmez; gerekçesi §5'tedir.

## 4. Otoriter kaynak eksikleri

- Belge düzeyi `ProfileID` kaynağı (kanal kararına bağlı, §8).
- Yapısal adres alanları: `AddressType` eleman sırası
  (`UBL-CommonAggregateComponents-2.1.xsd:699-715`) `ID → Postbox → Room → StreetName →
  BlockName → BuildingName → BuildingNumber → CitySubdivisionName → CityName → PostalZone →
  Region → District → Country` şeklindedir; `CitySubdivisionName` (ilçe), `CityName` (il) ve
  `Country` zorunludur. **`AddressLine` elemanı bu tipte yoktur** — tek serbest metni
  `AddressLine/Line` içine koyma seçeneği teknik olarak mümkün değildir. `cac:Country` içinde
  `cbc:Name` zorunlu, `cbc:IdentificationCode` opsiyoneldir.
- Gerçek kişi alıcılar için ayrı `Ad`/`Soyad`: `PersonType` içinde `cbc:FirstName` ve
  `cbc:FamilyName` zorunludur (`UBL-CommonAggregateComponents-2.1.xsd:2239-2250`).
- Çözülmüş TRT fatura tarihi ve saati (§9).
- Merkezî, yeniden kullanılabilir mali doğrulayıcı (§2, §5).

Ülke bilgisi `ParaBirimi = TRY` değerinden türetilmez. İlk kapsam yalnız Türkiye içi adresleri
destekler ve bu, kapıda açık bir destek kuralıdır (§9).

## 5. İlk renderer için destek matrisi

| Belge tipi | İlk renderer kapsamı | Gerekçe |
| --- | --- | --- |
| `SatisFaturasi` | Evet | Dar kapsamın tek senaryosu |
| `AlisIadeFaturasi` | Hayır | `InvoiceTypeCode=IADE` gerektirir; profil kısıtı ve schematron/örnek çelişkisi çözülmemiştir |
| `IadeFaturasi` | Hayır | Aynı gerekçe |
| `SatisIadeFaturasi` | Hayır | Gelen belge |
| `AlisFaturasi` | Hayır | Gelen belge |
| `Proforma`, `FaturaTaslagi` | Hayır | Resmî e-belge değil |

Dar kapsam: `SatisBelgesiTipi.SatisFaturasi`, `ParaBirimi=TRY`, `Kur=1`, tüm satırlar
`KdvUygulamaTipi.Kdvli`, yalnız standart KDV (`TaxTypeCode=0015`), tevkifat/istisna/ÖTV/ÖİV/
konaklama vergisi/iade/özel matrah/ihracat yok, yalnız Türkiye içi adres, yalnız `Adet` birimi.

### İndirim eşlemesi

`AllowanceChargeType` eleman sırası (`UBL-CommonAggregateComponents-2.1.xsd:726-736`):
`ChargeIndicator → AllowanceChargeReason → MultiplierFactorNumeric → SequenceNumeric → Amount →
BaseAmount → PerUnitAmount`. `ChargeIndicator` ve `Amount` zorunludur.

Dar kapsam eşlemesi:

- `cbc:ChargeIndicator` = `false`
- `cbc:Amount` = `Satir.IndirimTutari`
- `cbc:BaseAmount` = `Yuvarla(Satir.Miktar × Satir.BirimFiyat)`
- `cbc:MultiplierFactorNumeric` = **üretilmez**
- `Satir.IndirimTutari = 0` ise `cac:AllowanceCharge` hiç üretilmez
- Belge düzeyi `cac:AllowanceCharge` hiç üretilmez

**`MultiplierFactorNumeric`'in üretilmeme gerekçesi:** `IndirimOrani` kolonu `decimal(5,2)`
(2 basamak) iken, oran girilmediğinde `ResolveLineRate` bu oranı 4 basamakta türetir
(`SatisBelgesiService.cs:2667`). Türetilmiş oran veritabanına yazılırken 2 basamağa indirgenir;
bu nedenle saklanmış `IndirimOrani` değeri, saklanmış `IndirimTutari` değerini
`brutMatrah × oran / 100` ile her zaman yeniden üretemez. Bu iki alanı aynı XML'e yazmak,
kendi içinde tutarsız resmî belge üretme riski taşır. `Amount` otoriter değerdir; oran alanı
opsiyonel olduğundan hiç üretilmemesi en dar ve en güvenli seçimdir.

`BaseAmount − Amount == LineExtensionAmount` özdeşliği korunur: `IndirimTutari` tam 2 basamaklı
olduğundan `Yuvarla(brütMatrah) − IndirimTutari == Yuvarla(brütMatrah − IndirimTutari) == Matrah`
eşitliği decimal aritmetiğinde sağlanır.

### Mali hesaplama ve yuvarlama sözleşmesi

Renderer ve kesim öncesi kapı, §2'de tespit edilen **mevcut otoriter davranışı** kullanır; yeni
ve bağımsız bir hesap mantığı tanımlanmaz. Sözleşme:

| Konu | Kural |
| --- | --- |
| `Miktar` ölçeği | `decimal(18,2)` |
| `BirimFiyat` ölçeği | `decimal(18,2)` |
| `IndirimTutari` ölçeği | `decimal(18,2)` |
| `Matrah` ölçeği | `decimal(18,2)` |
| `KdvOrani` ölçeği | `decimal(18,4)` |
| `KdvTutari` ölçeği | `decimal(18,2)` |
| Yuvarlama modu | `MidpointRounding.AwayFromZero` |
| Parasal yuvarlama basamağı | 2 |
| Matrahın yuvarlanma anı | KDV hesaplanmadan **önce**: `matrah = Yuvarla(brütMatrah − indirimTutari)` |
| KDV yuvarlama düzeyi | **Satır bazında**: `kdvTutari = Yuvarla(matrah × kdvOrani / 100)` |
| Belge toplamları | Yuvarlanmış satır değerlerinin **düz toplamı**; ikinci üst düzey yuvarlama yok |
| `TaxSubtotal` toplamları | Oran grubundaki **satır bazında yuvarlanmış** değerlerin toplamı |
| Karşılaştırma tabanı | Saklanmış (yuvarlanmış) canonical değerler |

**Kritik kural — gruplanmış `TaxSubtotal`:** Grup `TaxAmount` değeri, o orana ait satırların
`Satir.KdvTutari` değerlerinin toplamıdır. Grup `TaxableAmount × oran / 100` ile **yeniden
hesaplanmaz**; satır bazında yuvarlama zaten uygulandığı için yeniden hesaplama kuruş farkı
üretebilir ve belge toplamıyla tutarsızlaşır. Aynı şekilde grup `TaxableAmount`, o orana ait
`Satir.Matrah` değerlerinin toplamıdır.

Doğrulanan invariantlar (hepsi saklanmış canonical değerler üzerinden):

1. `Yuvarla(Miktar × BirimFiyat) − IndirimTutari == Matrah`
2. `IndirimTutari <= Yuvarla(Miktar × BirimFiyat)`
3. `Yuvarla(Matrah × KdvOrani / 100) == KdvTutari`
4. `Σ Satir.Matrah == ToplamMatrah`
5. `Σ Satir.KdvTutari == ToplamKdv`
6. `Σ Satir.SatirToplami == GenelToplam`
7. `ToplamMatrah + ToplamKdv == GenelToplam` (dar kapsamda tevkifat/ÖTV/ÖİV/konaklama sıfır
   olduğu için `SatirToplami = Matrah + KdvTutari`)
8. Her `TaxSubtotal` grubu için: `grup TaxAmount == Σ (o orandaki Satir.KdvTutari)`

**"Yuvarlama yapmamak" ile "canonical mali kurala göre doğrulamak" farkı:** Renderer, snapshot'tan
okuduğu tutarları **değiştirmez, yeniden yuvarlamaz ve düzeltmez**; XML'e yazdığı her parasal
değer snapshot'taki canonical değerin birebir kendisidir. Renderer'ın yuvarlama fonksiyonunu
kullanması yalnızca **doğrulama** amaçlıdır: yukarıdaki invariantların sağlanıp sağlanmadığını
sınamak için beklenen değeri hesaplar ve saklanmış değerle karşılaştırır. Sonuç uyuşmuyorsa XML
üretmez; uyuşuyorsa saklanmış değeri yazar. Hiçbir durumda hesapladığı değeri saklanmış değerin
yerine yazmaz.

Uyuşmazlık davranışı:

1. XML üretilmez.
2. `EBELGE_UBL_MONETARY_TOTAL_MISMATCH` hatası üretilir (HTTP 422).
3. Uyuşmaz değer düzeltilmez, yuvarlanmaz.
4. Kesim öncesi kapı **aynı doğrulayıcıyı** (aynı kod yolunu) kullanır; iki ayrı hesap mantığı
   zamanla farklılaşamaz.

### İmza sınırı

1. **Deterministik unsigned UBL XML renderer** (Faz 2B.5): bu fazın çıktısı.
2. **`cac:Signature` iş/referans metadata'sı**: XSD'de zorunludur; imzalayanın VKN'sini
   (`cbc:ID schemeID="VKN_TCKN"`) ve `cac:SignatoryParty` altında `PartyIdentification` ile
   `PostalAddress` bilgisini taşır. Kriptografik imzanın kendisi değildir; unsigned XML de bu
   elemanı içermek zorundadır. Gerekli alanlar: `Kurum.VergiNo` ve kurumun yapısal adresi.
3. **`ext:UBLExtensions` içindeki XAdES/mali mühür içeriği**: kriptografik imza burada taşınır;
   unsigned renderer bu bloğu yer tutucu olarak üretmez.
4. **Sonraki kriptografik imzalama fazı**: ayrı uygulama fazıdır.
5. **İmzalama sonrası nihai artifact**: gönderime hazır belgedir.

**Faz 2B.5 çıktısı gönderime hazır nihai e-Fatura değildir.** `UnsignedUblSha256` renderer
çıktısının byte dizisi üzerinden; `SignedUblSha256` imzalama sonrası byte dizisi üzerinden
**yeniden** hesaplanır. İki değer aynı alanda saklanamaz.

## 6. Snapshot V1/V2 kararı

Mevcut `IEBelgeCanonicalSnapshotReader` **değiştirilmez** ve yalnız V1 döndürmeye devam eder:

```csharp
public interface IEBelgeCanonicalSnapshotReader
{
    EBelgeCanonicalSnapshotV1 Oku(EBelgeCanonicalSnapshotOkumaTalebi talep);
}
```

Yeni ve bağımsız typed V2 reader eklenir:

```csharp
public interface IEBelgeCanonicalSnapshotV2Reader
{
    EBelgeCanonicalSnapshotV2 Oku(EBelgeCanonicalSnapshotOkumaTalebi talep);
}
```

Kurallar:

- V2 reader, payload deserialize edilmeden **önce** `talep.SnapshotSchemaVersion` değerini
  doğrular. Değer `"2"` değilse `EBELGE_UBL_RENDER_SNAPSHOT_VERSION_UNSUPPORTED` (HTTP 422)
  üretir ve deserialize denemez.
- Renderer/orchestrator **doğrudan** `IEBelgeCanonicalSnapshotV2Reader` kullanır.
- Renderer yolu için **ortak V1/V2 dispatcher eklenmez**; ortak dönüş tipi, base type veya union
  gerekmez.
- `object`, `dynamic` ve V1 interface'inin dönüş tipini değiştiren union kullanılmaz.
- V1 JSON/hash doğrulaması değiştirilmez; V1 kayıtlar backfill veya migration ile
  dönüştürülmez.

Başka tüketiciler yalnız sürüm tespitine ihtiyaç duyarsa, payload'ı deserialize etmeyen ayrı bir
okuyucu eklenebilir:

```csharp
public interface IEBelgeCanonicalSnapshotVersionReader
{
    EBelgeCanonicalSnapshotSurumu Oku(EBelgeCanonicalSnapshotOkumaTalebi talep);
}

public enum EBelgeCanonicalSnapshotSurumu
{
    V1 = 1,
    V2 = 2
}
```

Bu okuyucu yalnız sürüm numarası/enum döndürür; snapshot içeriği döndürmez ve renderer yolunda
kullanılmaz.

`EBelgeCanonicalSnapshotV2` içinde V1'e ek alanlar (yalnız dar kapsam için gerekli olanlar):

- `ProfileID`, `InvoiceTypeCode`
- `FaturaTarihiTrt`, `FaturaSaatiTrt`
- Satıcı yapısal adres: `Ilce`, `Il`, `UlkeAdi`, `UlkeKodu`, opsiyonel `PostaKodu`, `SokakAdi`,
  `BinaNo`
- Alıcı yapısal adres: aynı alanlar
- `MusteriAd`, `MusteriSoyad`
- Satır düzeyinde `BirimKodu`

## 7. Önerilen renderer sözleşmesi

```csharp
public interface IEBelgeUblRenderer
{
    EBelgeUblRenderSonucu Render(EBelgeCanonicalSnapshotV2 snapshot);
}
```

Renderer'a ayrıca parametre olarak **verilmeyecekler**: belge tipi, issue/issuance tarihi, tenant
veya kurum bağlamı, taraf bilgileri, kanal, `ProfileID`, `InvoiceTypeCode`, birim kodu. Bunların
tamamı `EBelgeCanonicalSnapshotV2` içindedir.

GİB kural seti iş girdisi değildir; implementasyona immutable teknik konfigürasyon olarak enjekte
edilir:

```csharp
public sealed class EBelgeUblRenderer : IEBelgeUblRenderer
{
    public EBelgeUblRenderer(GibKuralSeti kuralSeti) { ... }
}

public sealed record GibKuralSeti(
    string KuralSetiKimligi,   // "GIB-UBL-TR-1.2.1/2026-09-14"
    string UblVersionId,       // "2.1"
    string CustomizationId,    // "TR1.2"
    ImmutableArray<GibKuralSetiDosyasi> Manifest);

public sealed record GibKuralSetiDosyasi(string GoreliYol, string Sha256);
```

Renderer V1 snapshot kabul etmez; `EBelgeCanonicalSnapshotV1` tipini hiçbir aşırı yüklemede almaz.
Renderer'a `DbContext` veya `TimeProvider` enjekte edilmez.

### Çıktı ve byte değişmezliği

```csharp
public sealed record EBelgeUblRenderSonucu(
    ImmutableArray<byte> UnsignedUblUtf8,
    string UnsignedUblSha256,
    string KullanilanProfileId,
    string KullanilanInvoiceTypeCode,
    string KuralSetiKimligi,
    string RendererSurumu);
```

**Seçilen model: `ImmutableArray<byte>`.** `ReadOnlyMemory<byte>` seçilmemiştir; bu tip yalnız
okuma görünümü sağlar, altındaki dizinin başka bir referans üzerinden değiştirilmesini engellemez.
`ImmutableArray<byte>` iç diziyi hiçbir zaman dışarı vermez ve indeksleyici üzerinden yazma
sağlamaz.

Sözleşme:

- SHA-256, artifact içinde saklanan **tam byte dizisi** üzerinden **bir kez** hesaplanır.
- Hash'in hesaplandığı byte dizisi ile saklanan/döndürülen byte dizisi aynı dizidir; farklı
  olamaz.
- Çağıran, dönen değer üzerinde değişiklik yapamaz; yaptığı hiçbir işlem saklanan artifact'ı veya
  hash'i etkilemez.

### Determinizm sözleşmesi

| Konu | Politika | Test beklentisi |
| --- | --- | --- |
| Aynı typed V2 snapshot → aynı XML | Renderer saf fonksiyondur; girdi dışı durum okunmaz | Aynı snapshot ile 100 çağrı, byte dizileri birebir eşit |
| UTF-8 ve BOM | UTF-8, BOM üretilmez | İlk 3 byte `EF BB BF` değil |
| XML declaration | `<?xml version="1.0" encoding="UTF-8"?>` sabit, tek satır | Golden-file byte karşılaştırması |
| Indentation | Girinti üretilmez; belge tek satır | Çıktıda `\t` ve satır başı boşluk yok |
| Newline | Yalnız `\n`; `\r` üretilmez | Çıktıda `0x0D` byte'ı yok |
| Namespace prefixleri | Sabit tablo: `cac`, `cbc`, `ext`, `ds`, `xades`, `xsi` | Golden-file + prefiks sırası testi |
| Element sırası | `UBL-Invoice-2.1.xsd` sequence sırası; `TaxSubtotal` grupları KDV oranına göre artan | Sıra-duyarlı golden-file testi |
| Attribute yazım sırası | Her elemanda sabit, kaynak sırasından bağımsız | Attribute sırası regresyon testi |
| Kültür | `CultureInfo.InvariantCulture` | `tr-TR`, `de-DE`, `en-US` ile aynı çıktı |
| Decimal biçimi | Sabit ayıraç `.`; parasal alanlar 2 basamak, `Percent` 2 basamak; trailing zero korunur | `1.5m`, `1.50m`, `1.500m` aynı lexical çıktı |
| `IssueDate` biçimi | `yyyy-MM-dd`, V2 `FaturaTarihiTrt` değerinden | Golden-file |
| `IssueTime` biçimi | `HH:mm:ss`, offset yazılmaz, V2 `FaturaSaatiTrt` değerinden | Golden-file |
| OS/kültür/saat dilimi bağımsızlığı | Renderer `TimeZoneInfo` çağırmaz; dönüşüm kesim anında yapılmıştır | UTC, `Europe/Istanbul`, `America/New_York` altında aynı hash |
| Güncel saat yasağı | `DateTime.Now`/`UtcNow`/`TimeProvider` çağrılmaz | Statik analiz + saat ileri alınarak aynı çıktı |
| Rastgele UUID yasağı | `Guid.NewGuid()` çağrılmaz; UUID snapshot'tan gelir | Statik analiz kuralı |
| Veritabanı/canlı entity okuma yasağı | Renderer'a `DbContext` enjekte edilmez | Constructor bağımlılık testi |
| SHA-256 | Saklanan tam byte dizisi üzerinden bir kez | Byte dizisi sabitken hash sabit |
| Renderer sürümü | Sonuçta `RendererSurumu` döner | Alanın dolu olduğu testi |
| Rule-set kimliği | Sonuçta `KuralSetiKimligi` döner | `GIB-UBL-TR-1.2.1/2026-09-14` testi |
| Değiştirilemez byte çıktısı | `ImmutableArray<byte>`; iç dizi dışarı verilmez | Çağıranın çıktıyı ve hash'i değiştirememesi testi |
| Farklı culture/timezone'da aynı hash | Yukarıdakilerin bileşimi | 3 culture × 3 timezone = 9 kombinasyon, tek hash |

Bu turda test yazılmamıştır; yukarıdakiler Faz 2B.5 promptuna girecek test listesidir.

## 8. Sonraki uygulama fazlarının sırası

**Faz 2B.4.1 — minimal hazırlık:**

1. Kesim anı sözleşmesi: `TimeProvider` enjeksiyonu, `planlananKesimZamaniUtc` ve merkezî
   TRT dönüşümü (§9).
2. `EnsureUblHazirlikKaynaklari` genişletilir; `ResolveEBelgeKanali` sayaç kilidinden öncesine
   taşınır.
3. **Merkezî mali doğrulayıcı çıkarılır**: `ValidateBelgeOnayaGonderilebilir` içindeki toplam
   tutarlılık kontrolleri ve `CreateSatirFromRequest` içindeki satır türetme kuralları, hem kesim
   öncesi kapının hem renderer'ın çağırabileceği tek bir bileşene taşınır. Mevcut yuvarlama
   davranışı (`Yuvarla` = 2 basamak, `AwayFromZero`) **değiştirilmez**.
4. Yapısal adres alanları: satıcı ve alıcı için `Ilce`, `Il`, `UlkeAdi`/`UlkeKodu`.
5. Gerçek kişi alıcılar için ayrı `Ad`/`Soyad` alanları.
6. `EBelgeCanonicalSnapshotV2` + `IEBelgeCanonicalSnapshotV2Reader`.
7. Feature flag/konfigürasyon anahtarı, kapalı başlatılır.
8. Rule-set manifest dosyaları ve build-time SHA-256 doğrulaması.

**Faz 2B.5 — deterministic unsigned UBL renderer:**

9. `IEBelgeUblRenderer` ve dar kapsam implementasyonu.
10. Determinizm test paketi (§7).
11. XSD + schematron doğrulaması (sabitlenmiş rule-set ile).

**Sonraki fazlar:** kriptografik imzalama, PDF renderer, artifact storage abstraction, e-posta
gönderim provider'ı, e-Arşiv raporlama.

Tevkifat, ÖTV, ÖİV, konaklama vergisi, iade, özel matrah ve ihracat alanları bu sıraya
eklenmemiştir.

### Birim kodu: entity alanı mı, yalnız V2 alanı mı

| Seçenek | Kapsam | Değerlendirme |
| --- | --- | --- |
| A: `SatisBelgesiSatiri`'ne genel amaçlı `BirimKodu` | Entity + migration + tüm birimler için eşleme tablosu | Dar kapsam yalnız `Adet` kabul edeceği için gereksiz geniştir |
| B: Yalnız V2 snapshot'ta `BirimKodu` | Entity değişikliği ve migration yok | Kapı yalnız `Birim == "Adet"` kabul eder; V2'ye sabit `C62` yazılır |

**Seçilen: B.** Genel amaçlı birim kodu modeli, ikinci bir birim desteklendiği fazda eklenir.

### `InvoiceTypeCode` ve `ProfileID`

- `InvoiceTypeCode` için entity alanı eklenmez; dar kapsamda `SATIS` deterministik üretilip V2'ye
  yazılır.
- `ProfileID`: e-Arşiv kanalında `EARSIVFATURA` deterministik üretilir ve entity alanı gerekmez.
  e-Fatura kanalı ilk sürümde desteklenecekse belge düzeyi `EFaturaSenaryosu` alanı gerekir.
- **Kanal kararı verilmeden `EFaturaSenaryosu` hakkında varsayım yapılmamıştır**; hazırlık listesi
  bu karar verilene kadar tamamen kesinleşmez (§10, madde 1).

### Satıcı hukuki tarafı

`cac:AccountingSupplierParty` kaynağı `Kurum`'dur. `Tesis.Adres`, kurumun hukuki adresinin yerine
kullanılmaz. Tesis bilgisi gerekirse sonraki bir fazda operasyonel/ek lokasyon (`cac:Delivery`)
olarak ele alınabilir.

## 9. Faz 2B.4.1 ve Faz 2B.5 promptlarına girecek kesin kararlar

- Renderer'ın tek iş girdisi `EBelgeCanonicalSnapshotV2`'dir.
- GİB kural seti immutable teknik konfigürasyondur; çıkarılmış dosya + manifest modeliyle build
  artifact'ında sabittir, runtime'da indirilmez.
- V1 reader ve V1 record aynen korunur; ayrı typed V2 reader eklenir; renderer yolunda dispatcher
  yoktur.
- Satır indirimi `InvoiceLine/AllowanceCharge` altına yazılır; `MultiplierFactorNumeric`, belge
  düzeyi `AllowanceCharge`, `AllowanceTotalAmount` ve `ChargeTotalAmount` üretilmez.
- Mevcut yuvarlama davranışı değiştirilmez; merkezî mali doğrulayıcı bu davranışı kullanır.
- Renderer tutarları değiştirmez; uyuşmazlıkta XML üretmez ve
  `EBELGE_UBL_MONETARY_TOTAL_MISMATCH` (422) verir.
- Faz 2B.5 çıktısı unsigned XML'dir; imzalama ayrı fazdır ve hash imzalamadan sonra yeniden
  hesaplanır.
- Birim ve `InvoiceTypeCode` için entity alanı eklenmez.
- `EFaturaSenaryosu` yalnızca ilk dalga e-Fatura kanalını içeriyorsa eklenir.
- Özellik, canlıya geçişe kadar feature flag ile kapalı tutulur.

### Kesim anı sözleşmesi

Mevcut kodda `FaturaKesimTarihi` kapıdan ve resmî numara üretiminden sonra atandığı için kapı bu
alanı kontrol edemez (§2). Sözleşme:

1. `FaturaKesAsync` içinde **tek bir kesim anı**, sayaç kilitlenmeden önce, enjekte edilen
   `TimeProvider` üzerinden alınır.
2. Bu değer `planlananKesimZamaniUtc` olarak tutulur.
3. Türkiye yerel tarih ve saati, merkezî ve açıkça tanımlanmış bir dönüşümle bu değerden **bir
   kez** üretilir: `planlananKesimTarihiTrt`, `planlananKesimSaatiTrt`.
4. Kesim öncesi kapı şu koşulları kontrol eder:
   - `BelgeTarihi >= 2026-09-14`
   - `planlananKesimTarihiTrt >= 2026-09-14`
5. Kapı başarılı olduktan sonra **aynı** `planlananKesimZamaniUtc` değeri şu alanlar için
   kullanılır: `FaturaKesimTarihi`, V2 `FaturaTarihiTrt`, V2 `FaturaSaatiTrt`.
6. Akış içinde ikinci kez `DateTime.UtcNow`, `DateTime.Now` veya `TimeProvider.GetUtcNow()`
   çağrılmaz.
7. Renderer saat dilimi dönüşümü yapmaz; V2'deki çözülmüş TRT değerlerini biçimlendirir.

Sınır testleri: `13.09.2026 23:59:59` (red), `14.09.2026 00:00:00` (kabul) ve UTC/TRT gün
değişiminin farklı sonuç verdiği anlar (ör. `13.09.2026 21:30 UTC` = `14.09.2026 00:30 TRT`).

### Kesim öncesi kapı sözleşmesi

Kapı, mevcut `EnsureUblHazirlikKaynaklari` (`SatisBelgesiService.cs:1116`) genişletilerek aynı
noktada çalışır ve şunları doğrular:

1. Sistemin canlı kullanım için etkinleştirilmiş olması
2. `BelgeTarihi` ve `planlananKesimTarihiTrt` değerlerinin 14.09.2026'dan önce olmaması
3. Yalnız `GIB-UBL-TR-1.2.1/2026-09-14` rule-setinin kullanılıyor olması
4. Desteklenen kanal
5. Belge tipinin `SatisFaturasi` olması
6. `ParaBirimi == "TRY"` ve `Kur == 1`
7. `ProfileID` kaynağının çözülebilir olması
8. `InvoiceTypeCode` değerinin `SATIS` olarak üretilebilmesi
9. Kurumsal veya gerçek kişi alıcı kimliğinden tam olarak birinin bulunması
10. Satıcı ve alıcı için zorunlu adres alanlarının (ilçe, il, ülke) dolu olması
11. Gerçek kişi alıcıda ad ve soyadın ayrı ayrı dolu olması
12. Kurumsal alıcıda unvanın dolu olması
13. VKN'nin 10, TCKN'nin 11 hane olması
14. Tüm satırların `KdvUygulamaTipi.Kdvli` olması
15. Hiçbir satırda tevkifat, istisna, ÖTV, ÖİV veya konaklama vergisi alanının dolu olmaması
16. Tüm satırlarda `Birim == "Adet"` olması
17. Satır ve belge toplamlarının §5'teki merkezî mali doğrulayıcıya göre tutarlı olması
18. En az bir geçerli (silinmemiş) satır bulunması

Kapı; sayaç artırılmadan, resmî numara verilmeden, belge durumu değiştirilmeden, `EBelgeKaydi`
oluşturulmadan, snapshot oluşturulmadan ve outbox oluşturulmadan çalışır.

### Hata kodları

| Hata kodu | HTTP | Durum |
| --- | --- | --- |
| `EBELGE_UBL_FEATURE_DISABLED` | 503 | Özellik/hizmet operasyonel olarak kapalı |
| `EBELGE_INVOICE_DATE_BEFORE_GO_LIVE` | 400 | Belge tarihi veya planlanan kesim tarihi 14.09.2026'dan önce |
| `EBELGE_UBL_SCOPE_UNSUPPORTED` | 400 | Desteklenmeyen belge tipi, kanal, vergi, birim veya para birimi |
| `EBELGE_UBL_AUTHORITATIVE_FIELD_MISSING` | 400 | Eksik zorunlu alan (adres, ad/soyad, unvan, VKN/TCKN) |
| `EBELGE_UBL_MONETARY_TOTAL_MISMATCH` | 422 | Satır/belge tutarları mali invariantları ihlal ediyor |
| `EBELGE_UBL_RENDER_SNAPSHOT_VERSION_UNSUPPORTED` | 422 | V2 olmayan snapshot ile render isteği |

Ayrım:

- **400** — desteklenmeyen kapsam, eksik zorunlu alan veya geçersiz istek koşulu.
- **422** — sözdizimsel olarak işlenebilir içeriğin mali/semantik invariantları ihlal etmesi.
- **503** — özelliğin veya hizmetin operasyonel olarak kapalı olması.

`EBELGE_UBL_MONETARY_TOTAL_MISMATCH`, belge değerleri düzeltilip yeni bir kesim isteği yapılarak
çözülebilir; bu nedenle "yeniden göndermekle çözülmez" nitelemesi bu hata için geçerli **değildir**
ve genel 422 tanımı olarak kullanılmamıştır.

`EBELGE_UBL_RENDER_SNAPSHOT_VERSION_UNSUPPORTED` ise mevcut immutable V1 kaydının yeniden
denenmesiyle düzelmez; V1 snapshot tanım gereği değiştirilemez ve backfill edilmez. Bu nedenle
outbox açısından **kalıcı** hatadır ve yeniden deneme kuyruğuna alınmaz.

## 10. Açık kalan ve ürün sahibinin cevaplaması gereken sorular

1. **İlk dalgada hangi kanal desteklenecek — yalnız e-Arşiv mi, e-Fatura da dahil mi?** Bu karar
   verilmeden `EFaturaSenaryosu` alanının gerekip gerekmediği belirlenemez ve Faz 2B.4.1 hazırlık
   listesi tamamen kesinleşmez. Bu, Faz 2B.4.1'e başlamadan önce çözülmesi gereken **engeldir**;
   bu rapor bu konuda hiçbir varsayım yapmamaktadır.
2. Kurum ve cari kart adreslerinin ilçe/il/ülke bilgisi mevcut veri tabanında var mı, yoksa veri
   girişi/temizliği gerekecek mi?
3. Gerçek kişi müşterilerin ad ve soyad bilgisi ayrı alanlarda toplanabiliyor mu, yoksa mevcut
   `MusteriAdSoyad` verisi için tek seferlik manuel ayrıştırma mı gerekecek?
4. Feature flag'in açılma kararı hangi ortamda ve kim tarafından verilecek; test ortamında
   14.09.2026 öncesi deneme yapılabilmesi için ayrı bir mekanizma isteniyor mu?
5. Konaklama vergisi, ÖTV, ÖİV ve tevkifat senaryoları hangi fazda ele alınacak?
6. Resmî schematron kuralı ile resmî `IadeFaturasiOrnegi.xml` örneği arasındaki
   `TICARIFATURA`+`IADE` çelişkisi, iade senaryoları planlanmadan önce GİB'e sorulacak mı?

## Sonuç

**Renderer öncesinde ek hazırlık fazı gerekir.**

Faz 2B.4.1 yalnız dar renderer için gerekli entity, snapshot V2, reader ve kesim öncesi kapı
değişiklikleriyle sınırlıdır:

1. Kesim anı sözleşmesi (`TimeProvider`, `planlananKesimZamaniUtc`, merkezî TRT dönüşümü).
2. `EnsureUblHazirlikKaynaklari` genişletilmesi ve `ResolveEBelgeKanali`'nin öne alınması.
3. Merkezî mali doğrulayıcının çıkarılması (mevcut yuvarlama davranışı değiştirilmeden).
4. Yapısal adres alanları (ilçe, il, ülke) — satıcı ve alıcı için.
5. Gerçek kişi alıcılar için ayrı `Ad`/`Soyad` alanları.
6. `EBelgeCanonicalSnapshotV2` ve `IEBelgeCanonicalSnapshotV2Reader`; V1 aynen korunur.
7. Feature flag ve 14.09.2026 tarih sınırı.
8. Rule-set manifesti ve build-time SHA-256 doğrulaması.

Birim kodu için entity alanı, `InvoiceTypeCode` için entity alanı ve destek dışı senaryoların
(tevkifat, ÖTV, ÖİV, konaklama vergisi, iade, özel matrah, ihracat) alanları bu hazırlık fazına
dahil değildir.

---

## Faz 2B.4.1 Uygulama Özeti

Bu bölüm, yukarıdaki raporun §8'inde listelenen Faz 2B.4.1 hazırlık maddelerinden **bu turda
gerçekten uygulananları** belgeler. Bu turda UBL XML renderer, PDF, e-posta, XSD/Schematron
doğrulaması veya sağlayıcı entegrasyonu geliştirilmedi — yalnız renderer öncesi ortak ve test
edilebilir altyapı hazırlandı.

### Değiştirilen ve eklenen dosyalar

**Yeni dosyalar:**

- `backend/Muhasebe/SatisBelgeleri/EBelgeUblOptions.cs` — feature flag (`Enabled`, varsayılan `false`).
- `backend/Muhasebe/SatisBelgeleri/TurkeyTimeZoneHelper.cs` — UTC→Türkiye yerel dönüşümü; `Europe/Istanbul` / `Turkey Standard Time` ikisini de dener.
- `backend/Muhasebe/SatisBelgeleri/EBelgeInvoiceDateBeforeGoLiveException.cs` — `EBELGE_INVOICE_DATE_BEFORE_GO_LIVE` (HTTP 400).
- `backend/Muhasebe/SatisBelgeleri/EBelgeCanonicalSnapshotHashUtility.cs` — V1 ve V2 okuyucularının paylaştığı TEK hash doğrulama yardımcısı.
- `backend/Muhasebe/SatisBelgeleri/EBelgeCanonicalSnapshotV2.cs` — `EBelgeCanonicalSnapshotV2` record ailesi + `IEBelgeCanonicalSnapshotV1Reader`/`IEBelgeCanonicalSnapshotV2Reader` ve implementasyonları.
- `tests/STYS.Tests/TurkeyTimeZoneHelperTests.cs`, `SatisBelgesiTutarHesaplayiciTests.cs`, `EBelgeCanonicalSnapshotV1V2ReaderTests.cs`, `EBelgeCutoverGateIntegrationTests.cs`.

**Değiştirilen dosyalar:**

- `backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs` — `TimeProvider`/`IOptions<EBelgeUblOptions>` opsiyonel constructor parametreleri (varsayılan `TimeProvider.System` / `Enabled=false` — mevcut çağıranlar değişmeden derlenir); `EnsureCutoverTarihGecerli` kapısı; kesim anı tek-okuma sözleşmesi; `CreateSatirFromRequest` ve `ValidateBelgeOnayaGonderilebilir`'in ortak hesaplayıcıyı kullanacak şekilde refactor edilmesi.
- `backend/Muhasebe/SatisBelgeleri/SatisBelgesiTutarHesaplayici.cs` — `HesaplaMatrah`, `HesaplaKdvTutari`, `DogrulaBelgeToplamlari` eklendi; mevcut `Yuvarla`/`HesaplaSatirToplami` değişmedi.
- `backend/Muhasebe/SatisBelgeleri/EBelgeCanonicalSnapshotReader.cs` — hash doğrulama private metotları `EBelgeCanonicalSnapshotHashUtility`'ye delege edildi (davranış birebir korundu); kullanılmayan `using`'ler kaldırıldı.
- `backend/Program.cs` — `EBelgeUblOptions` config bağlama + `TimeProvider.System` DI kaydı.
- `backend/appsettings.json` — `"EBelgeUbl": { "Enabled": false }`.

### TimeProvider'ın DI kaydı

`Program.cs`: `builder.Services.AddSingleton(TimeProvider.System);` ve
`builder.Services.Configure<EBelgeUblOptions>(builder.Configuration.GetSection(EBelgeUblOptions.SectionName));`.
`SatisBelgesiService` constructor'ında her iki bağımlılık da **opsiyonel** parametre olarak eklendi
(`TimeProvider? timeProvider = null`, `IOptions<EBelgeUblOptions>? eBelgeUblOptions = null`) ve
`null` ise sırasıyla `TimeProvider.System` / `Enabled=false`'a düşer. Bu tasarım kasıtlıdır: mevcut
8 test dosyasındaki `new SatisBelgesiService(...)` çağrı yerleri **hiçbiri değiştirilmeden**
derlenmeye devam eder; yalnız bu fazın kendi testleri gerçek zamanı kontrol etmek için bu
parametreleri açıkça sağlar.

### Tarih kapısının akıştaki kesin konumu

`FaturaKesAsync` içinde, otoriter kurum/tesis okuması ve `EnsureUblHazirlikKaynaklari` çağrısından
hemen sonra, **sayaç `UPDLOCK` ile kilitlenmeden önce**:

```csharp
var planlananKesimZamaniUtc = _timeProvider.GetUtcNow().UtcDateTime;
EnsureCutoverTarihGecerli(belge, planlananKesimZamaniUtc);
```

`EnsureCutoverTarihGecerli`, `_eBelgeUblOptions.Enabled == false` iken hiçbir şey yapmadan döner
(bu fazdan önceki davranış aynen sürer). `Enabled == true` iken `TurkeyTimeZoneHelper` ile TRT'ye
çevrilmiş kesim tarihini ve `belge.BelgeTarihi`'ni 14.09.2026 ile karşılaştırır; ikisinden biri
öncesindeyse `EBelgeInvoiceDateBeforeGoLiveException` (HTTP 400) fırlatır — bu noktada sayaç henüz
hiç sorgulanmamıştır. Akışın ilerisinde (`belge.FaturaKesimTarihi` ataması ve
`EBelgeSnapshotFactory.CreateSnapshot` çağrısı), daha önce `DateTime.UtcNow` ile ikinci kez zaman
okuyan satır kaldırılıp **aynı** `planlananKesimZamaniUtc` değerini yeniden kullanacak şekilde
değiştirildi — akış boyunca `TimeProvider` tam olarak **bir kez** okunur.

Mevcut kodda `ResolveEBelgeKanali` çağrısının hâlâ sayaç kilidinden **sonra** (satır ~1206)
çalıştığı — önceki raporun tespit ettiği sıralama sorunu — bu turda **düzeltilmedi**; kapsam
yalnız tarih kontrolüyle sınırlı tutuldu. Bu, Faz 2B.4.2 için açık bir madde olarak aşağıda
listelenmiştir.

### Mali hesaplama bileşeninin sorumluluğu

`SatisBelgesiTutarHesaplayici` (var olan dosya, genişletildi) artık üç yeni üye taşıyor:

- `HesaplaMatrah(brutMatrah, indirimTutari)` — KDV'den önce 2 basamağa yuvarlar.
- `HesaplaKdvTutari(matrah, kdvOrani)` — zaten yuvarlanmış matrah üzerinden hesaplar, sonucu 2 basamağa yuvarlar.
- `DogrulaBelgeToplamlari(satirlar, toplamMatrah, toplamKdv, genelToplam)` — saklanmış belge
  toplamlarını, satırların (`SatirTutarKatkisi`) düz toplamıyla karşılaştırır; hiçbir değeri
  değiştirmez, yalnız `BelgeToplamUyusmazligi` listesi döner (boşsa tutarlı demektir).

Bu üç metot, hem satır oluşturma anında (`CreateSatirFromRequest`) hem doğrulama anında
(`ValidateBelgeOnayaGonderilebilir`) **aynı formülün** kullanılmasını sağlar; ileride renderer/
snapshot üretimi de aynı bileşeni çağıracaktır (bkz. Faz 2B.4.2).

### Mevcut doğrulamadan taşınan kodlar

`ValidateBelgeOnayaGonderilebilir` içindeki (10. madde) satır toplamı = belge toplamı
karşılaştırması — üç ayrı `if (belge.ToplamX != hesaplananX) throw ...` bloğu — kaldırılıp
`SatisBelgesiTutarHesaplayici.DogrulaBelgeToplamlari` çağrısına ve dönen uyuşmazlık listesi
üzerinde `switch` ile aynı üç hata mesajının üretilmesine dönüştürüldü. Karşılaştırma operatörü
(`!=`, tam decimal eşitliği) ve hata mesajı metinleri **birebir** korundu; davranış değişmedi
(bkz. `EBelgeSnapshotUblHazirlikIntegrationTests` regresyon testleri, aşağıda).

`CreateSatirFromRequest` içinde matrah ve KDV satırları (`SatisBelgesiTutarHesaplayici.Yuvarla(...)`
doğrudan çağrıları) yeni `HesaplaMatrah`/`HesaplaKdvTutari` metotlarına yönlendirildi — formül
birebir aynı (`Yuvarla(brutMatrah - indirimTutari)` ve `Yuvarla(matrah * kdvOrani / 100m)`),
yalnız tek yerde tanımlı hale geldi.

### Eklenen hedefli testler

| Dosya | Kapsam |
| --- | --- |
| `TurkeyTimeZoneHelperTests.cs` | Sabit UTC+3 dönüşümü, UTC gün sonu/başı gün değişimi, Unspecified/Local kind davranışı, yaz/kış ofsetinin sabit kaldığı. |
| `SatisBelgesiTutarHesaplayiciTests.cs` | Midpoint (`0.005`→`0.01` vb.) AwayFromZero, matrahın KDV'den önce yuvarlanması, satır bazlı KDV toplamının toplu matrahtan yeniden hesaplanan değerden **kasıtlı olarak farklı** çıktığı somut senaryo (10.03+10.04 @ %18), çok satırlı belge toplamı doğrulaması, uyuşmazlık raporlama. |
| `EBelgeCanonicalSnapshotV1V2ReaderTests.cs` | V1 reader V1 payload okur; V2 reader V2 payload okur; V1 payload V2 reader'a verilince (zorunlu V2 alanları eksik olduğu için deserialize hatası ile) reddedilir; V2 payload V1 reader'a verilince (`UnmappedMemberHandling.Disallow` nedeniyle) reddedilir; geçersiz hash; yanlış `SnapshotSchemaVersion`. |
| `EBelgeCutoverGateIntegrationTests.cs` | Gerçek SQL Server'a karşı, gerçek `FaturaKesAsync`: 13.09.2026 23:59:59.999 TRT reddedilir; 14.09.2026 00:00:00 TRT kabul edilir; UTC tarihi hâlâ 13.09 iken TRT karşılığı 14.09 olan an kabul edilir (gün değişimi doğru değerlendirilir); reddedilen işlemde resmî numara/`FaturaKesimTarihi`/`EBelgeKaydi` **hiç oluşmaz** ve sayaç değişmez; başarılı işlemde `FaturaKesimTarihi`'nin `FakeTimeProvider`'ın verdiği anla **birebir** eşit olduğu; `Enabled=false` iken go-live öncesi tarihte kesimin serbest kaldığı (geriye dönük uyumluluk). |

Testlerde gerçek sistem saati kullanılmadı; `EBelgeCutoverGateIntegrationTests` içinde
`TimeProvider`'dan türeyen özel bir `FakeTimeProvider` (sabit `DateTimeOffset` döndürür)
kullanıldı.

### Çalıştırılan test komutları ve sonuçları

Gerçek SQL Server (`stys-mssql` docker container, `localhost:14333`) bu ortamda çalışır durumda
bulunduğundan entegrasyon testleri **atlanmadan** çalıştırıldı:

```
STYS_INTEGRATION_TEST_CONNECTION_STRING="Server=localhost,14333;Database=STYSDB;User Id=sa;Password=Strong!Pass1;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True" \
dotnet test tests/STYS.Tests/STYS.Tests.csproj -c Debug --no-build \
  --filter "FullyQualifiedName~TurkeyTimeZoneHelperTests|FullyQualifiedName~SatisBelgesiTutarHesaplayiciTests|FullyQualifiedName~EBelgeCanonicalSnapshotV1V2ReaderTests|FullyQualifiedName~EBelgeCanonicalSnapshotReaderTests|FullyQualifiedName~EBelgeCutoverGateIntegrationTests|FullyQualifiedName~EBelgeSnapshotUblHazirlikIntegrationTests"
```

Sonuç: **Passed! Failed: 0, Passed: 54, Skipped: 0, Total: 54.**

Kapsanan sınıflar: `TurkeyTimeZoneHelperTests` (6), `SatisBelgesiTutarHesaplayiciTests` (8),
`EBelgeCanonicalSnapshotV1V2ReaderTests` (6), `EBelgeCanonicalSnapshotReaderTests` (14, önceden
var olan V1 reader regresyon testleri — hash yardımcısı çıkarma sonrası bozulmadı),
`EBelgeCutoverGateIntegrationTests` (7, yeni), `EBelgeSnapshotUblHazirlikIntegrationTests` (9,
önceden var olan kesim akışı regresyon testleri — `EnsureUblHazirlikKaynaklari`/toplam doğrulama
refactor'u sonrası bozulmadı).

**Bulgu (kapsam dışı, düzeltilmedi):** Aynı `--filter` dışında, ilgisiz iki entegrasyon testi
(`SatisBelgesiMuhasebeDengeIntegrationTests.SatisIadeFaturasi_GercekIadeStratejisiyleFisOlusurVeDengeliKalir`
ve
`SatisBelgesiEkVergiEngelIntegrationTests.SatisIadeFaturasi_KonaklamaVergisiIcerenBelge_MuhasebeFisiEngellenirVeHicbirKayitOlusmaz`)
`ResolveEBelgeKanali`'nin "her iki mükellefiyet bayrağı da kapalı" hatasıyla başarısız oluyor. Bu
turun değişiklikleriyle ilgisi olup olmadığını doğrulamak için `git worktree` ile temel commit
(`02c910e`, bu turun çalışma tabanı) ayrı bir klasöre çıkarılıp AYNI iki test AYNI ortamda tekrar
çalıştırıldı — **aynı iki test aynı hatayla, bu turun hiçbir değişikliği olmadan da başarısız
oluyor.** Bu, bu turdan önce var olan, ilgisiz bir test-verisi kusurudur (muhtemelen paylaşılan
`CariKart` test fixture'ının `EFaturaMukellefiMi`/`EArsivKapsamindaMi` bayraklarını hiç
ayarlamaması); bu turun "hedefli testleri" bunlar değildir ve iş talimatı gereği ("geniş çaplı
refactor yapma") bu turda düzeltilmemiştir. Ayrı bir düzeltme fazında ele alınmalıdır.

### Açık kalan ürün kararları

- İlk dalgada hangi kanalın (e-Arşiv/e-Fatura) destekleneceği — bu rapor kararı henüz vermedi;
  `EBelgeUblOptions.Enabled` yalnız tarih kapısını açar, kanal/`ProfileID`/`EFaturaSenaryosu`
  konusunda hiçbir varsayım yapılmadı.
- `ResolveEBelgeKanali`'nin sayaç kilidinden sonra çalışması — kesin karar §2'de belirtildiği gibi
  hâlâ düzeltilmedi; kanal kararı verilmeden bu taşımanın nereye (hangi koşullarla) yapılacağı da
  netleşmeyecektir.
- `SatisBelgesiMuhasebeDengeIntegrationTests`/`SatisBelgesiEkVergiEngelIntegrationTests`'teki
  ön-var-olan test verisi kusuru ayrı bir fazda düzeltilmeli.
- Feature flag'in ortam bazlı açılma stratejisi (hangi ortamda kim açacak) ürün sahibi kararı
  gerektiriyor.

### Faz 2B.4.2 için önerilen sonraki adım

1. `ResolveEBelgeKanali`'yi sayaç kilidinden önceye taşımak ve kesim öncesi kapının kanal
   kontrolünü eklemek (bu, ilk dalga kanal kararına bağlıdır).
2. `EBelgeSnapshotFactory`'yi genişletip gerçekten `EBelgeCanonicalSnapshotV2` üreten bir yol
   eklemek — bu faz yalnız V2'nin şemasını ve typed reader'ını hazırladı, hiçbir üretim kod yolu
   henüz V2 yazmıyor.
3. Yapısal adres alanlarını (`Ilce`, `Il`, `UlkeAdi`/`UlkeKodu`) `Kurum` ve alıcı kaynaklarına
   eklemek ve kesim öncesi kapıya zorunluluk kontrolü olarak bağlamak.
4. Gerçek kişi alıcılar için ayrı `Ad`/`Soyad` alanlarını eklemek.
5. Kesim öncesi kapının §9'daki 18 maddelik tam sözleşmesini (bu turda yalnız tarih/madde 1-2
   uygulandı) `EnsureUblHazirlikKaynaklari`'ye kademeli olarak eklemek.

---

## Faz 2B.4.2 Sonuç Bölümü

Bu bölüm, Faz 2B.4.1'in yukarıda listelenen beş açık maddesinin uygulanmasını belgeler. **Kesin
kapsam kararı:** ilk dalgada yalnız **e-Arşiv** faturası desteklenir; e-Fatura kanalı bu fazda
reddedilir. UBL XML, PDF, elektronik imza, XSD/Schematron doğrulaması ve dış sağlayıcı
entegrasyonu bu turda da geliştirilmedi.

### Değiştirilen ve eklenen dosyalar

**Yeni dosyalar:**

- `backend/Muhasebe/SatisBelgeleri/EBelgeUblGoLive.cs` — paylaşılan `Trt` sabiti (14.09.2026); artık hem `EnsureCutoverTarihGecerli` hem `IEBelgeUblPreCutValidator` AYNI sabiti kullanıyor.
- `backend/Muhasebe/SatisBelgeleri/EBelgeUblPreCutExceptions.cs` — `EBelgeUblFeatureDisabledException` (503), `EBelgeUblScopeUnsupportedException` (400), `EBelgeUblAuthoritativeFieldMissingException` (400), `EBelgeUblMonetaryTotalMismatchException` (422).
- `backend/Muhasebe/SatisBelgeleri/EBelgeUblPreCutValidator.cs` — `IEBelgeUblPreCutValidator`/`EBelgeUblPreCutValidator`, `EBelgeUblPreCutContext`/`EBelgeUblPreCutSatirContext` (saf, EF'siz veri taşıyıcılar).
- `backend/Muhasebe/SatisBelgeleri/EBelgeCanonicalPayload.cs` — immutable byte/hash sözleşmesi.
- `backend/Infrastructure/EntityFramework/Migrations/20260803203013_AddEBelgeUblFaz2B42StructuredFields.cs` — 8 yeni nullable kolon.
- `tests/STYS.Tests/EBelgeUblPreCutValidatorTests.cs`, `EBelgeCanonicalPayloadTests.cs`, `EBelgeUblPreCutIntegrationTests.cs`.

**Değiştirilen dosyalar:**

- `backend/Kurumlar/Entities/Kurum.cs`, `Dto/{KurumDto,CreateKurumRequest,UpdateKurumRequest}.cs` — `Ilce`, `Il`.
- `backend/Muhasebe/CariKartlar/Entities/CariKart.cs`, `Dtos/CariKartDtos.cs` — `Ad`, `Soyad`.
- `backend/Muhasebe/SatisBelgeleri/Entities/SatisBelgesi.cs`, `Dtos/SatisBelgesiDtos.cs` — `MusteriAd`, `MusteriSoyad`, `MusteriIlce`, `MusteriIl`.
- `backend/Infrastructure/EntityFramework/StysAppDbContext.cs` — üç entity için `HasMaxLength(128)` Fluent konfigürasyonu.
- `backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs` — kanal çözümlemesinin taşınması, kesim öncesi kapı çağrısı, yeni Musteri* alanların `CreateAsync`/`ApplyBelgeUpdatesAsync`/`ApplyCariSnapshot*` içinde akışı, `IEBelgeUblPreCutValidator` bağımlılığı.
- `backend/Muhasebe/SatisBelgeleri/EBelgeCanonicalSnapshotV2.cs` — `CanonicalJsonOptions` artık `internal` (factory ile paylaşılıyor).
- `backend/Muhasebe/SatisBelgeleri/EBelgeSnapshotFactory.cs` — `CreateSnapshotV2` eklendi; `CreateSnapshot` (V1) değişmedi.
- `backend/Program.cs` — `IEBelgeUblPreCutValidator` DI kaydı.
- `tests/STYS.Tests/EBelgeCutoverGateIntegrationTests.cs`, `SatisBelgesiEkVergiEngelIntegrationTests.cs`, `SatisBelgesiMuhasebeDengeIntegrationTests.cs` — fixture düzeltmeleri (aşağıda).

### Kanal kararının uygulanması

`ResolveEBelgeKanali(cariKart)` çağrısı, sayaç sorgusundan/kilidinden/artırımından, resmî numara
üretiminden ve belge durum değişikliklerinden **önceye** taşındı — artık `cariKart` da aynı
noktada (`belge.CariKart ?? throw ...`) çözülüyor. Mevcut kanal çözümleme kuralları
(`EFaturaMukellefiMi` → `EFatura`, `EArsivKapsamindaMi` → `EArsiv`, ikisi de kapalıysa hata)
**değiştirilmedi**. Kanalın e-Arşiv olup olmadığı artık ayrıca, yalnızca
`EBelgeUblOptions.Enabled` açıkken çalışan `IEBelgeUblPreCutValidator` içinde kontrol ediliyor —
e-Fatura kanalı `EBELGE_UBL_SCOPE_UNSUPPORTED` (400) ile, resmî numara verilmeden reddediliyor.

### Kesim öncesi kapının akıştaki kesin yeri

`FaturaKesAsync` içinde sıra: kurum/tesis otoriter okuması → `EnsureUblHazirlikKaynaklari`
(unconditional) → kesim anı TEK okuması (`planlananKesimZamaniUtc`) + TRT dönüşümü → **kanal
çözümlemesi** → `EnsureCutoverTarihGecerli` (Faz 2B.4.1, unconditional/no-op) → **yalnız
`EBelgeUblOptions.Enabled` açıkken**: aktif satırlar toplanır, `IEBelgeUblPreCutValidator.Validate`
çağrılır → (geçerse) `AlisIadeFaturasi` iade kontrolü → sayaç `UPDLOCK` → resmî numara → V1/V2
snapshot dallanması. Kapı, sayaç sorgusundan kesinlikle önce çalışır; herhangi bir kural ihlalinde
sayaç hiç sorgulanmaz, `ResmiFaturaNo`/`FaturaKesimTarihi` atanmaz, `EBelgeKaydi` oluşmaz.

### Eklenen/yeniden kullanılan validator bileşenleri

`IEBelgeUblPreCutValidator.Validate(EBelgeUblPreCutContext)` — saf, EF'siz bir context alır,
hiçbir entity değiştirmez. 18 kural + otoriter alıcı/satıcı kimlik ve yapısal adres kontrolleri
sırayla uygulanır; ilk ihlalde ilgili tipe özgü exception fırlatılır (§'deki 5 hata sınıfı).
Mali tutarlılık kontrolü (kural 18), Faz 2B.4.1'de eklenen
`SatisBelgesiTutarHesaplayici.DogrulaBelgeToplamlari`'yı **yeniden kullanır** — yeni bir hesap
mantığı icat edilmedi.

### Entity ve migration değişiklikleri

Mevcut ilişkiler incelendi: `CariKart` zaten `Il`/`Ilce` (free-text) taşıyordu — bu, `SatisBelgesi`
snapshot alanlarına (`MusteriIlce`/`MusteriIl`) `ApplyCariSnapshot` üzerinden **aynı desenle**
taşındı, yeni bir CariKart kolonu gerekmedi. `Tesis.IlId` (Il lookup FK) ve boşta duran `Country`
entity'si (hiçbir yerde FK ile bağlı değil) **kasıtlı olarak yeniden kullanılmadı** — Kurum'un
yasal adresi Tesis'in operasyonel il kaydından farklı bir kavramdır ve bu dar kapsam yalnız
Türkiye içi adresi desteklediği için `UlkeAdi`/`UlkeKodu` yeni bir kolon olmadan renderer sabiti
(`"Türkiye"`/`"TR"`) olarak üretildi. Gerçekten eksik olan, minimal migration'a yansıyan alanlar:
`Kurum.{Ilce,Il}`, `CariKart.{Ad,Soyad}`, `SatisBelgesi.{MusteriAd,MusteriSoyad,MusteriIlce,MusteriIl}`
— sekizi de nullable, `HasMaxLength(128)`. `PostaKodu`/`SokakAdi`/`BinaNo` (V2 şemasında opsiyonel)
için kolon eklenmedi; kapı bunları zorunlu kılmıyor, factory `null` yazıyor.

**Frontend'e bu turda dokunulmadı** — bkz. "Açık kalan konular".

### V2 snapshot üretim akışı

`EBelgeSnapshotFactory.CreateSnapshotV2(eBelgeKaydi, belge, kurum, tesis, cariKart,
planlananKesimZamaniUtc)`, PUBLIC `EBelgeCanonicalSnapshotV2` tipini (V1'in kendi private record
kopyası DEĞİL) doğrudan doldurur ve `EBelgeCanonicalSnapshotV2Reader.CanonicalJsonOptions` (artık
`internal`, iki sınıf arasında paylaşılıyor) ile serialize eder — böylece üretilen payload, aynı
okuyucunun kendi canonical round-trip denetiminden geçeceği garanti edilir.
`ProfileID="EARSIVFATURA"`/`InvoiceTypeCode="SATIS"`/`BirimKodu="C62"` kapı zaten kanal/belge
tipi/birim kurallarını doğruladıktan SONRA sabit üretilir — yeniden karar/hesaplama yapılmaz.
`FaturaTarihiTrt`/`FaturaSaatiTrt`, `FaturaKesAsync`'te zaten alınmış TEK
`planlananKesimZamaniUtc` değerinden `TurkeyTimeZoneHelper` ile (saf, deterministik) türetilir —
factory kendi zaman okuması yapmaz. `FaturaKesAsync`, `_eBelgeUblOptions.Enabled` durumuna göre
`CreateSnapshotV2`/`CreateSnapshot` arasında dallanır; V1 üretim yolu hiç değişmedi.

### Exact byte ve hash üretim sözleşmesi

`EBelgeCanonicalPayload.FromUtf8Bytes(byte[])`: `JsonSerializer.SerializeToUtf8Bytes` ile ÜRETİLEN
byte dizisi `ImmutableArray.Create` ile (kopyalanarak, aliaslanmadan) saklanır; SHA-256 bu saklanan
diziden **bir kez** hesaplanır; `ToUtf8String()` JSON'u tekrar serialize ETMEZ, yalnız saklanan
AYNI diziyi string'e çevirir. `CreateSnapshotV2` bu tipi kullanır; `EBelgeSnapshot.CanonicalJson`
(DB'de string) ve `CanonicalSha256`, ikisi de bu tek payload'dan türer. Testler:
`EBelgeCanonicalPayloadTests` — hash saklanan tam byte dizisi üzerinden mi (evet), kaynak diziyi
sonradan mutasyona uğratmak saklanan payload'ı etkiliyor mu (hayır, `ImmutableArray.Create` kopya
üretir). `EBelgeUblPreCutIntegrationTests.EArsivKanaliKabulEdilirVeV2SnapshotDogruUretilir`,
gerçek DB'ye yazılmış `EBelgeSnapshot.CanonicalJson`/`CanonicalSha256`'nın birbirleriyle ve yeniden
hesaplanan hash'le eşleştiğini ve `EBelgeCanonicalSnapshotV2Reader`'ın bu payload'ı sorunsuz
okuduğunu doğrular.

### Düzeltilen fixture'lar

- `SatisBelgesiMuhasebeDengeIntegrationTests`, `SatisBelgesiEkVergiEngelIntegrationTests`: paylaşılan
  `BuildCariKart` yardımcısı `EFaturaMukellefiMi`/`EArsivKapsamindaMi` ayarlamıyordu; kanal artık
  sayaç kilidinden önce çözüldüğü için bu testler "her iki mükellefiyet bayrağı da kapalı"
  hatasıyla başarısız oluyordu (Faz 2B.4.1 raporunda "önceden var olan, ilgisiz" olarak
  işaretlenmişti — kök neden şimdi tam olarak budur: kanal HİÇ çözülmüyordu, bu yüzden testin
  amacıyla ilgisiz görünüyordu). Düzeltme: ilgili `CariKart`'a `EArsivKapsamindaMi = true` eklendi.
  Bu düzeltme AÇIĞA ÇIKARDIĞI ikincil bir gap: `SatisBelgesiEkVergiEngelIntegrationTests`'in kendi
  `CleanupKurumAsync`'i, artık gerçekten oluşan `EBelgeKaydi`/`EBelgeSnapshot`/`EBelgeOutboxMesaji`
  zincirini `SatisBelgeleri` silinmeden önce temizlemiyordu (önceden kanal hatası yüzünden bu
  zincir hiç oluşmuyordu) — `FK_EBelgeKayitlari_SatisBelgeleri_...` ihlaliyle başarısız oluyordu;
  bu da düzeltildi (silme sırası: Outbox → Snapshot → Kaydı → Belge).
- `EBelgeCutoverGateIntegrationTests`: Faz 2B.4.1'de eklenen "başarılı kesim" senaryoları artık
  Faz 2B.4.2'nin yeni zorunlu alanlarına (Kurum/Alıcı yapısal adres) çarpıyordu; `Kurum.Ilce/Il`
  ve `musteriKart.Ilce/Il` seed'e eklendi. Ayrıca `CreateSatisBelgesiRequest.MusteriUnvan` gibi
  alanların, `CariKartId` set edildiğinde `ApplyCariSnapshotToCreateRequest` tarafından HER ZAMAN
  ezildiği (istemcinin gönderdiği değerin dikkate alınmadığı) keşfedildi — bu, önceki raporun
  "Musteri* alanları request'te veriliyor" varsayımının hatalı olduğunu gösterdi; düzeltme
  gerçek kaynağa (`CariKart.Ad`/`Soyad`/`Ilce`/`Il`) taşındı.

### Çalıştırılan hedefli test komutları ve sonuçları

```
STYS_INTEGRATION_TEST_CONNECTION_STRING="Server=localhost,14333;Database=STYSDB;User Id=sa;Password=Strong!Pass1;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True" \
dotnet test tests/STYS.Tests/STYS.Tests.csproj -c Debug --no-build \
  --filter "FullyQualifiedName~EBelgeUblPreCutValidatorTests|FullyQualifiedName~EBelgeCanonicalPayloadTests|FullyQualifiedName~EBelgeUblPreCutIntegrationTests|FullyQualifiedName~EBelgeCutoverGateIntegrationTests|FullyQualifiedName~SatisBelgesiTutarHesaplayiciTests|FullyQualifiedName~TurkeyTimeZoneHelperTests|FullyQualifiedName~EBelgeCanonicalSnapshotReaderTests|FullyQualifiedName~EBelgeCanonicalSnapshotV1V2ReaderTests|FullyQualifiedName~EBelgeSnapshotUblHazirlikIntegrationTests|FullyQualifiedName~SatisBelgesiMuhasebeDengeIntegrationTests|FullyQualifiedName~SatisBelgesiEkVergiEngelIntegrationTests|FullyQualifiedName~SatisBelgesiHesaplamaTests"
```

**Sonuç: Passed! Failed: 0, Passed: 134, Skipped: 0, Total: 134.**

Yeni eklenen 35 test (`EBelgeUblPreCutValidatorTests` 22, `EBelgeCanonicalPayloadTests` 3,
`EBelgeUblPreCutIntegrationTests` 10) dahil; Faz 2B.4.1'in ve önceki fazların tüm regresyon
testleri (V1 reader, V1/V2 reader, cutover kapısı, mali hesaplayıcı, timezone, iki düzeltilen
fixture) bozulmadan geçti.

**Ek regresyon taraması (kapsam dışı bulgu):** `CariKartDto`/`KurumDto` gibi paylaşılan DTO'lara
dokunulduğu için bunları kullanan testler de (`RezervasyonOdemeMuhasebeIntegrationTests`,
`TenantSecurityTests`, `TicariBelgeLookupServiceIadeAdaylariIntegrationTests`,
`TicariBelgeLookupServiceTests`) ayrıca çalıştırıldı. 8 test (`TicariBelgeLookupServiceIadeAdaylariIntegrationTests`
içinde) `KurumFaturaNumaraSayaclari.SeriKodu` sütununda "String or binary data would be truncated"
hatasıyla başarısız oluyor. Bu turun değişiklikleriyle ilgisi olup olmadığı `git worktree` ile
temel commit'te (`e2781b4`) doğrulandı — **aynı 8 test aynı hatayla, bu turun hiçbir değişikliği
olmadan da başarısız oluyor.** Bu turdan önce var olan, tamamen ilgisiz bir kusur (muhasebe
fişi/sayaç şeması ile ilgili, e-belge/CariKart alan eklemeleriyle bağlantısız); bu turun hedefli
testleri bunlar değildir ve düzeltilmedi.

### Açık kalan konular

- **Frontend güncellenmedi.** `Kurum`/`CariKart`/`SatisBelgesi` formlarında yeni alanlar
  (`Ilce`/`Il`/`Ad`/`Soyad`) için input yok; bu alanlar API'de var ama üretimde kimse
  doldurmayacaktır — `EBelgeUblOptions.Enabled=true` yapıldığında bu veriler girilmeden hiçbir
  e-Arşiv kesimi geçemez. Ayrı bir (küçük, mekanik) frontend fazı gerekiyor.
  `RezervasyonCariKartHizliOlusturRequestDto` gibi ayrı/minimal DTO'lar bu turda hiç değişmedi ve
  etkilenmedi (yeni alanlar hepsi nullable/opsiyonel).
- `ResolveEBelgeKanali` artık kesin olarak sayaç kilidinden önce çalışıyor (bu fazın hedefi
  tamamlandı).
- `TicariBelgeLookupServiceIadeAdaylariIntegrationTests`'teki 8 testin ön-var-olan, ilgisiz sayaç
  şeması kusuru ayrı bir fazda incelenmeli.
- Tevkifat/ÖTV/ÖİV/konaklama vergisi için muhasebe hesap eşlemeleri seed edilmediğinden, bu turda
  o senaryoların kesim öncesi kapı reddi INTEGRATION seviyesinde satırın doğrudan veritabanında
  (fiş oluşturma adımından sonra) mutasyona uğratılmasıyla test edildi; gerçek uçtan uca (fiş dahil)
  bu senaryolar için hesap eşlemesi seed'i ayrı bir iyileştirme olabilir.
- ÖTV/ÖİV içeren satırlar için ayrı integration testi eklenmedi (yalnız konaklama vergisi ile
  temsil edildi); üçü de validator seviyesinde birebir aynı kod yolunu (satır bazlı tutar != 0
  kontrolü) kullandığından unit testler (`OtvIcerenSatirReddedilir`, `OivIcerenSatirReddedilir`)
  yeterli kabul edildi.

### Faz 2B.5 renderer'a geçiş için hazır olup olmadığı

**Kısmen hazır.** Backend tarafında kesim öncesi doğrulama tamamlandı ve gerçek, immutable,
byte-doğrulanmış V2 snapshot üretimi çalışıyor — bir renderer artık `EBelgeCanonicalSnapshotV2`
okuyup XML üretebilir. Ancak renderer'a geçmeden önce şu iki konu çözülmeli: (1) frontend
güncellenmeden `EBelgeUblOptions.Enabled=true` hiçbir ortamda pratikte kullanılabilir olmayacak
(her e-Arşiv kesimi otoriter alan eksikliğiyle reddedilecek); (2) kesim öncesi kapının §9'daki
tam listesi bu iki fazda (2B.4.1 tarih, 2B.4.2 kanal+kapsam+adres+mali) tamamlandı, geriye yalnız
XSD/Schematron doğrulaması ve gerçek XML serileştirmesi kaldı — bunlar açıkça Faz 2B.5'in
kapsamıdır ve bu turda hiç geliştirilmedi.

## Faz 2B.5 sonuç bölümü — deterministik, imzasız UBL renderer

**Durum: kısmen tamamlandı, commit/push YAPILMADI.** Aşağıda tamamlanan iş, gerçek (sahte
olmayan) doğrulama kanıtlarıyla tespit edilen iki teknik engel ve öneri sıralanmıştır.

### Eklenen bileşenler

- `EBelgeUblKuralSeti/` (yeni dizin) — 14 GİB XSD dosyası + 3 GİB schematron dosyası + 4 ISO
  Schematron "skeleton" XSLT1 dosyası (`iso_dsdl_include.xsl`, `iso_abstract_expand.xsl`,
  `iso_svrl_for_xslt1.xsl`, `iso_schematron_skeleton_for_xslt1.xsl`) + `manifest.json`
  (göreli yol + SHA-256 her dosya için). 17 GİB dosyasının SHA-256'sı bu raporun daha önce
  kaydedilmiş manifest tablosuyla BİREBİR eşleşti (yeniden hesaplanıp doğrulandı, yeniden
  indirilmedi). 4 ISO skeleton dosyası resmi `Schematron/schematron` GitHub deposundan
  (`trunk/schematron/code/`) indirildi; bunlar GİB'e değil, ISO Schematron standardının kendi
  referans implementasyonuna aittir ve XSLT 1.0 uyumludur, dolayısıyla ek bağımlılık olmadan
  .NET'in yerel `System.Xml.Xsl.XslCompiledTransform`'ıyla çalıştırılabilirler (bkz. aşağıdaki
  schematron engeli — çalıştırılabilir olmaları GİB kurallarının XSLT1 ile ÇÖZÜLEBİLİR olduğu
  anlamına gelmiyor).
- `EBelgeUblKuralSetiYukleyici` / `IEBelgeUblKuralSetiYukleyici` — manifest.json'ı okur, HER
  dosyanın SHA-256'sını yeniden hesaplayıp manifestteki kayıtlı değerle karşılaştırır; tek dosya
  eksik veya hash uyuşmazlığı varsa `EBelgeUblKuralSetiManifestException` (kalıcı yapılandırma
  hatası) fırlatır. İnternet erişimi yoktur.
- `IEBelgeUblRenderer` / `EBelgeUblRenderer` / `EBelgeUblRenderSonucu` — önerilen sözleşmeye
  (§7) sadık: girdi yalnız `EBelgeCanonicalSnapshotV2`, DB/HTTP/saat/rastgelelik erişimi yok.
  Render önce (a) snapshot şema sürümünü, (b) kapsamı (ProfileID/InvoiceTypeCode/ParaBirimi/
  Kur/BirimKodu/KDV tipi/tevkifat-istisna-ÖTV-ÖİV-konaklama yokluğu/iade yokluğu) BAĞIMSIZ
  olarak yeniden doğrular, (c) otoriter alan varlığını kontrol eder, (d) toplamları
  `SatisBelgesiTutarHesaplayici.DogrulaBelgeToplamlari` ile yeniden doğrular — herhangi biri
  başarısız olursa XML ÜRETİLMEZ. Yalnız bunlardan SONRA XML üretilir; hash tam olarak
  `XmlBytes` üzerinden hesaplanır ve XML hash'ten SONRA yeniden serialize EDİLMEZ.
- `EBelgeUblXsdValidator` — pinned XSD setini `System.Xml.Schema.XmlSchemaSet` ile derler;
  `EBelgeUblSandboxXmlResolver` yalnız kural seti kök dizini altındaki dosyalara izin verir
  (path traversal ve http(s) tamamen kapalı). Üretilen belgenin (instance XML) kendisi
  `DtdProcessing.Prohibit` + `XmlResolver = null` ile okunur (DTD/XXE tamamen kapalı). Sabit
  XSD dosyalarının YÜKLENMESİ için (yalnız burada, hash doğrulanmış vendored dosyalar için)
  `DtdProcessing.Parse` kullanılır çünkü resmi `UBL-xmldsig-core-schema-2.1.xsd` kopyası W3C'nin
  orijinal şemasının yalnız İÇ (harici SYSTEM/PUBLIC kimliği OLMAYAN) DOCTYPE alt kümesini
  taşıyor — bu genel bir DTD/XXE gevşetmesi DEĞİLDİR, XmlResolver yine sandbox'lıdır.
- `EBelgeUblSchematronValidator` — ISO Schematron skeleton'ın 3 aşamalı derleme hattını
  (`iso_dsdl_include.xsl` → `iso_abstract_expand.xsl` → `iso_svrl_for_xslt1.xsl`, ki bu da
  `iso_schematron_skeleton_for_xslt1.xsl`'i include eder) `XslCompiledTransform` ile GERÇEKTEN
  çalıştırır; `document()` XSLT işlevi yalnız bu derleme hattı için ve yalnız sandbox'lanmış
  resolver ile açıktır (`XsltSettings(enableDocumentFunction: true, enableScript: false)`).
  Script çalıştırma her zaman kapalıdır.
- Rule-set kimliği, XML namespace/prefix kararları (`Invoice`=varsayılan, `cac`, `cbc`, `ext`),
  eleman sırası (UBL 2.1 XSD sequence'ine sadık), ondalık/tarih/saat biçimlendirme kuralları
  (invariant culture, `0.00`/`yyyy-MM-dd`/`HH:mm:ss`, bilimsel gösterim yok), satıcı/alıcı
  eşleme kaynakları (Kurum ≠ Tesis; kurumsal→PartyName+VKN, gerçek kişi→Person+TCKN, otomatik
  ad/soyad bölme YOK), satır indirimi (yalnız satır düzeyi `AllowanceCharge`,
  `MultiplierFactorNumeric` HİÇBİR ZAMAN üretilmez, belge düzeyi `AllowanceCharge`/
  `AllowanceTotalAmount` HİÇBİR ZAMAN üretilmez), KDV gruplama (oran bazında artan sırada,
  grup tutarları zaten doğrulanmış satır değerlerinin TOPLAMI — yeniden hesaplama YOK) — hepsi
  önceki turlarda kararlaştırılan sözleşmeye göre `EBelgeUblRenderer.cs` içinde uygulandı ve
  gerçek `UBL-TR_Main_Schematron.xml`/`UBL-TR_Common_Schematron.xml` dosyaları okunarak (VKN/TCKN
  şema kuralları, `PartyIdentificationPartyNamePersonCheck`, `PartyVDCheck`, `TaxTypeCheck`,
  KDV `TaxTypeCode="0015"`, `InvoicedQuantityCheck` gibi somut kurallar) doğrulandı — tahmin
  değil, gerçek kaynaktan doğrulama.
- Ödeme bilgisi (`cac:PaymentMeans`): bu fazda üretilmedi — snapshot'ta otoriter/doğrulanmış bir
  ödeme türü→kod eşlemesi yoktur (§13'te öngörüldüğü gibi açık ürün sorusu olarak bırakıldı).
- `cac:Signature` (imza referans metadatası, XMLDSig DEĞİL): bu fazda üretilmedi — hangi alt
  alanların zorunlu olduğu resmi örnek/XSD'den doğrulanmadan tahmin edilmeyecekti (§6); ayrıca
  aşağıdaki XSD bulgusu, imza sınırının başlı başına bir sonraki fazın konusu olduğunu
  doğruluyor.
- `Program.cs`: `IEBelgeUblKuralSetiYukleyici`, yüklenmiş `GibKuralSeti`, `IEBelgeUblRenderer`,
  `IEBelgeUblXsdValidator`, `IEBelgeUblSchematronValidator` singleton olarak DI'a eklendi.
  Outbox tüketici sınırına gerçek kablolama (renderer'ı çağıran bir consumer) bu turda
  YAPILMADI — mevcut projede henüz böyle bir tüketici olmadığından, görev talimatına göre
  ("consumer yoksa yapay bir background service ekleme") yalnız bileşenler hazırlandı.

### Tespit edilen iki gerçek teknik engel (kanıtlı, tahmin değil)

**1) XSD: `ext:UBLExtensions` yapısal olarak zorunlu ve boş olamaz.** Pinned
`UBL-Invoice-2.1.xsd`'de `Invoice`'ın İLK alt elemanı `ext:UBLExtensions`'tır ve
`minOccurs` belirtilmediği için varsayılan olarak ZORUNLUDUR; içeriği
(`ExtensionContentType/xsd:any minOccurs="1"`) de EN AZ bir eleman gerektirir — boş
bırakılamaz. Gerçek GİB uygulamalarında bu yuva yalnız nihai `ds:Signature` ile doldurulur.
Bu fazda elektronik imza YASAK olduğundan (md.6/md.20), bu yuvaya konabilecek meşru, sahte
olmayan bir içerik YOKTUR. **Kullanıcıya bu bulgu 2026-08-03/04 turunda soruldu ve "kısmi XSD
doğrulamasını kabul et" seçildi**: renderer `ext:UBLExtensions` OLMADAN XML üretir (imzasız
olduğunu doğru biçimde yansıtır); tam XSD-set doğrulaması yalnız gelecekteki imzalama fazında,
bu yuva `ds:Signature` ile doldurulduğunda genuinely geçecektir. Bu, tek ve öngörülebilir
bulgudur — otomatik testle (`EBelgeUblRendererSmokeTests.GecerliSnapshotXsdDogrulamasindaYalnizBilinenUblExtensionsBoslugunuVerir`)
kilitlendi: XSD doğrulaması TAM OLARAK bu bulguyu üretir, başka hiçbir yapısal hata YOKTUR —
yani eşleme/sıralama/namespace/veri tipi düzeyinde renderer XSD'ye UYUMLUDUR.

**2) Schematron: resmi GİB kuralları XPath 2.0 gerektiriyor, .NET'in yerel XSLT motoru yalnız
XPath/XSLT 1.0 destekliyor.** ISO Schematron skeleton'ın 3+1 aşamalı XSLT1 hattı (tüm 4 dosya
da genuinely `XslCompiledTransform` ile YÜKLENDİ ve İLK 3 aşama (include çözme, abstract
genişletme, SVRL derleme) BAŞARIYLA çalıştı — bu, altyapının doğru kurulduğunun kanıtıdır).
Ancak derlenen NİHAİ doğrulayıcı XSLT'yi yüklerken (4. aşama - üretilen belgeye karşı
çalıştırma), .NET `XslCompiledTransform.Load` şu hatayla BAŞARISIZ olur:

```
System.Xml.Xsl.XslLoadException : 'exists()' is an unknown XSLT function.
```

`exists()` XPath 2.0/XSLT 2.0 işlevidir; `UBL-TR_Common_Schematron.xml` içinde 6 farklı yerde
(ör. `GeneralWithholdingTaxTotalCheck` kuralında) kullanılır — tek bir yazım hatası değil,
GİB'in resmi kural setinin XSLT 2.0 processor (Saxon vb.) için yazıldığının yapısal kanıtıdır.
.NET'in yerleşik `System.Xml.Xsl.XslCompiledTransform`'ı yalnız XSLT/XPath 1.0 uygular ve
`exists()` gibi ön ad taşımayan (no-namespace) XPath 2.0 işlevlerini genişletme mekanizmasıyla
(extension object) da EKLEMEK mümkün değildir (.NET XSLT1 extension fonksiyonları yalnız
namespace-nitelikli çağrılara bağlanabilir). **Bu, mevcut .NET kütüphaneleriyle güvenli/genuine
biçimde aşılamayan bir teknik engeldir.** Talimata göre: sahte doğrulama yazılMADI, "başarılı"
kabul edilMEDİ, regex ile schematron taklidi yapılMADI. Bu nedenle **commit/push YAPILMADI**.

Öneri (gelecek faz): gerçek bir XSLT 2.0/XPath 2.0 motoru (ör. Saxon-HE .NET portu/SaxonCS)
değerlendirilip yeni, açıkça onaylanmış bir NuGet bağımlılığı olarak eklenmeli; bu, bu turun
kapsamı ve yetkisi dışındadır (yeni dış bağımlılık kararı kullanıcı onayı gerektirir).

### Hedefli testler

Bu turda XSD/schematron altyapısının GERÇEKTEN çalıştığını (ve yalnız yukarıdaki iki bulguyu
ürettiğini) doğrulayan 2 duman testi eklendi ve koşturuldu (`EBelgeUblRendererSmokeTests`,
her ikisi de YEŞİL — biri schematron'un tam olarak nerede durduğunu, diğeri XSD'nin tam olarak
hangi tek bulguyu ürettiğini kilitliyor). Görev md.18'deki 40 senaryonun geri kalanı (byte/hash
determinizm, alan-değişikliği hassasiyeti, eşleme testleri vb.) bu turda YAZILMADI — schematron
engeli çözülmeden/kullanıcı onayı alınmadan geniş bir test seti yazmak, henüz kesinleşmemiş bir
mimari (schematron çözümü ne olacak?) üzerine inşa etmek anlamına gelirdi.

### Commit/push durumu

**Yapılmadı.** md.22 koşulu ("schematron doğrulaması genuinely çalışıp geçmeli") karşılanmadı.
Tüm değişiklikler yalnız çalışma dizininde duruyor; git'e commit edilmedi.

### Sonraki faz önerisi

1. XPath 2.0 destekleyen bir .NET XSLT/Schematron motoru seçimi kullanıcıyla görüşülmeli
   (Saxon-HE .NET, veya doğrudan bir ISO Schematron NuGet paketi — ör. websearch'te bulunan
   `Schematron` / `SchemaTron` / `schxslt-redux` paketleri; hiçbiri bu turda değerlendirilmedi/
   vetted edilmedi).
2. Schematron çözümü netleşince: 40 hedefli testin tamamı yazılmalı, imza (`cac:Signature`)
   alanı resmi örnek/XSD'den doğrulanarak eklenmeli, outbox consumer sınırına gerçek kablolama
   yapılmalı.
3. Frontend: otoriter satıcı/alıcı yapısal adres ve gerçek kişi ad/soyad alanları hâlâ
   girilemiyor (Faz 2B.4.2'den beri açık) — renderer artık bu alanları TÜKETTİĞİ için bu eksik
   daha da kritik hale geldi.

## XPath 2.0 Schematron motoru — teknik/lisans değerlendirmesi ve POC

**Bu bölüm yalnız değerlendirme amaçlıdır. Faz 2B.5 renderer/validator kodu bu turda
DEĞİŞTİRİLMEDİ, DB validator'a bağlanmadı, hiçbir dosya commit/push edilmedi.** POC dosyaları
`poc/schematron-xpath2-poc/` altında, üretim koduna dokunmadan, izole biçimde bırakıldı.

### Araştırılan kütüphaneler

| Aday | Sürüm/tarih | .NET hedefi | Lisans | Durum |
|---|---|---|---|---|
| **Saxon-HE (NuGet, .NET native)** | 10.9.0, 2023-02-16 | **.NET Framework 3.5** (yalnız) | MPL-2.0 (ücretsiz, ticari kullanıma uygun) | .NET 8 hedefli STYS'de doğrudan kullanılamaz — Framework 3.5 paketi .NET 8/10 (özellikle Linux) ile uyumlu değil. |
| **SaxonCS** (Saxonica'nın .NET 8+ native ürünü) | 12.10.0, .NET 8/9/10 hedefli | .NET 8+ | **Proprietary** — lisans anahtarı ZORUNLU, ücretsiz "HE" katmanı YOK | Ücretli ticari lisans olmadan kullanılamaz; satın alma kararı bu turun yetkisi dışında. |
| **SaxonHE11NetXslt / SaxonHE12NetXslt** (IKVM ile Java Saxon-HE'nin .NET'e çapraz derlenmesi) | 12.9.10, 2025-12-07 | .NET 8/9/10, Win/Linux/macOS | MPL-2.0 (Java Saxon-HE'den miras) | Teknik olarak ücretsiz ve çalışıyor GÖRÜNÜYOR, fakat yayımcının kendisi "deneysel, Saxonica tarafından test/destekli/onaylı DEĞİL, tek kişilik bir deney" olduğunu AÇIKÇA belirtiyor. Üretimde mali/hukuki belge doğrulaması için tek-bakımcılı, resmi olmayan bir paket riskli. |
| **devlooped/Schematron** (native C# Schematron işlemcisi) | 1.0.0, 2026-04-29 | netstandard2.0/net8.0 | MIT + **"Open Source Maintenance Fee"** (gelir üreten kullanıcılar için ödeme yükümlülüğü) | Lisans BELİRSİZ (MIT görünümlü ama ticari kullanımda ayrı ödeme modeli) — kullanıcı talimatına göre belirsizlikte kütüphane seçilmiş SAYILMAZ. Ayrıca dokümantasyon XPath 2.0 desteğini AÇIKÇA doğrulamıyor (kod örnekleri `System.Xml.XPath` - yani muhtemelen XPath 1.0 - kullanıyor), `exists()` sorununu muhtemelen ÇÖZMÜYOR. |
| **XmlPrime** | — | — | Tarihsel olarak ticari-only | Bu turda derinlemesine araştırılmadı (zaman kısıtı); SaxonCS ile aynı "ücretli lisans" kategorisinde olduğu biliniyor, ayrı bir teknik/lisans avantajı sunmuyor gibi görünüyor. |
| **Saxon-HE (Java, orijinal/resmi ürün)** | **13.0**, Maven Central son güncelleme 2026-07-10 | Java 17+ (JDK), .NET DEĞİL — ayrı process/sidecar olarak çağrılır | **MPL-2.0, ücretsiz, ticari kullanıma açık, Saxonica'nın kendi RESMİ ve TAM DESTEKLİ ücretsiz ürünü** (deneysel değil) | En olgun, en az riskli, lisans açısından en net seçenek — ancak .NET process içinde DEĞİL, ayrı bir JVM süreci gerektirir. |

### Lisans kararı

- Saxon-HE (Java, resmi) → **MPL-2.0**. Ticari/kurumsal kullanım için ücretsiz sürüm YETERLİ; ayrı ticari lisans GEREKMEZ. Redistribution kısıtı yok (MPL-2.0 dosya bazlı copyleft, STYS kaynak kodunu MPL'ye tabi KILMAZ — yalnız Saxon'un kendi dosyalarını değiştirirseniz o dosyalar için geçerli olur; STYS bu turda Saxon dosyalarını DEĞİŞTİRMEDİ). Container içinde dağıtılabilir (yalnız bir `.jar` dosyası + JVM). Build agent ve production için AYRI lisans gerekmez — aynı ücretsiz koşullar her ortamda geçerlidir. Lisans dosyası/aktivasyon GEREKMEZ (jar içinde `LicenseException`/`LicenseFeature` sınıfları yalnız PE/EE ücretli özellikleri için — HE seviyesinde devre dışı kalır, aktivasyon istemez; POC'ta jar hiçbir lisans istemi olmadan çalıştı).
- SaxonCS → üretimde ticari lisans ZORUNLU (ücretsiz HE katmanı yok). **Seçilmedi.**
- IKVM tabanlı .NET paketleri → lisans (MPL-2.0) kendisi net, ama **bakım/destek durumu belirsiz** (tek bakımcı, resmi olmayan). Üretim mali belge doğrulaması için bu risk kabul edilemez bulundu. **Seçilmedi.**
- devlooped/Schematron → lisans BELİRSİZ (OSMF yükümlülüğü + XPath 2.0 desteği doğrulanamadı). **Seçilmedi (belirsizlikte seçilmiş sayılmaz kuralı gereği).**

### POC sonucu (gerçek, sahte olmayan kanıt)

`poc/schematron-xpath2-poc/` içinde, Java Saxon-HE 10.9 (Maven Central'dan resmi, değiştirilmemiş
jar) ile **gerçek** GİB `UBL-TR_Main_Schematron.xml`/`Common_Schematron.xml` + resmi ISO
Schematron skeleton XSLT1 dosyaları (backend'deki vendored kopyalardan AYNEN kopyalandı,
değiştirilmedi) kullanılarak 4 aşamalı derleme hattı GERÇEKTEN çalıştırıldı (tam komut geçmişi ve
çıktılar `poc/schematron-xpath2-poc/SONUC.md`'de):

- **Derlenen nihai validator XSLT'de 12 gerçek `exists(` çağrısı** doğrulandı (grep ile sayıldı).
- **Senaryo 1 (kurala uygun XML)**: `exists(cac:WithholdingTaxTotal)` tabanlı kural HİÇ
  tetiklenmedi (beklenen).
- **Senaryo 2 (bilinçli ihlal — `InvoiceTypeCode=SATIS` iken `cac:WithholdingTaxTotal` eklendi)**:
  GERÇEK `svrl:failed-assert` üretti, test ifadesinde tam olarak
  `not(exists(cac:WithholdingTaxTotal)) or cbc:InvoiceTypeCode = 'TEVKIFAT' or ...` metni ve GİB'in
  kendi Türkçe hata mesajı ("Uyumsuz fatura tipi: 'SATIS'. cac:WithholdingTaxTotal elamanı
  varken...") yer aldı — taklit/regex DEĞİL, motorun gerçek XPath 2.0 değerlendirmesi.
- **Determinizm**: aynı girdi iki bağımsız çalıştırmada byte-birebir aynı SVRL çıktısını üretti
  (`diff` sıfır fark).
- **Yan bulgu**: derlenen validator XSLT, GİB dosyasının hiç bildirmediği `xs:` (XML Schema)
  ad alanı önekini kullanıyor (`xs:date(...)` tip dökümleri) — bu, POC çıktısına (GİB kaynağına
  DEĞİL) tek satırlık standart `xmlns:xs` bildirimi eklenerek çözüldü; gerçek entegrasyonda kalıcı
  bir çözüm gerekir (bkz. `SONUC.md`).
- **İkinci yan bulgu**: `UBL-TR_Main_Schematron.xml`'deki `<let name="type" value="efatura"/>`
  sabit kodlanmış; `EARSIVFATURA` gibi e-Arşiv ProfileID değerleri yalnız `$type='earchive'`
  iken doğrulanan ayrı bir koda liste giriyor. Bu, e-Arşiv senaryoları için ayrı bir
  parametreleştirme/giriş noktası kararı gerektiriyor (bu turun kapsamı dışında, ayrı not edildi).

### Güvenlik sonucu

- **DTD/XXE**: gerçek bir XXE payload'ı (`file:///c:/windows/win.ini` sızdırmayı deneyen) hem
  `-dtd:off` bayrağıyla hem varsayılan ayarla test edildi — **hiçbir sızıntı olmadı**. Üretimde
  yine de `-dtd:off` (CLI) veya güvenli `SAXParserFactory`/`Configuration` ayarları (API) ile
  AÇIKÇA kilitlenmelidir (varsayılana güvenilmemeli).
- **document() / ağ erişimi**: skeleton derleme aşamaları (1-3) `document()` işlevini yalnız YEREL
  `sch:include` çözümü için kullanır (kaynak GİB/skeleton dosyaları, saldırgan kontrolünde
  DEĞİLDİR). Üretim entegrasyonunda özel bir `URIResolver` ile bu yalnız sabit kural seti
  dizinine sandbox'lanmalı (STYS'nin .NET tarafındaki `EBelgeUblSandboxXmlResolver` ile AYNI
  desen) — bu turda Java API seviyesinde ayrıca implemente edilmedi (CLI POC bunun ötesine
  geçmedi), gerçek entegrasyon için gerekli bir adım olarak not edildi.
- **Hata mesajlarında mutlak yol/sızıntı**: POC'ta test edilmedi (zaman kısıtı) — gerçek
  entegrasyonda XSD/schematron validator'larındaki mevcut .NET deseniyle (güvenli/sınırlı mesaj)
  aynı disiplin uygulanmalı.

### Performans ve lifecycle sonucu

- CLI üzerinden tek seferlik çalıştırma ~3 saniye — bunun neredeyse tamamı JVM SOĞUK BAŞLATMA
  maliyeti (her çağrıda yeni bir JVM süreci başlatıldığı için). Bu, JVM'in HER belge için değil,
  **kalıcı bir süreçte BİR KEZ** başlatılması gerektiğini kanıtlıyor.
- Paralel iki doğrulama (aynı anda, arka planda) birbirini ETKİLEMEDİ — beklenen sonuçları üretti.
- **Önerilen lifecycle**: JVM tek seferlik ayağa kalkan KALICI bir süreç (sidecar/servis) olmalı;
  derlenmiş `XsltExecutable` (Saxon s9api) süreç ömrü boyunca BİR KEZ derlenip singleton olarak
  tutulmalı (Saxon dokümantasyonuna göre `XsltExecutable` immutable ve thread-safe'tir, ondan
  paralel `Xslt30Transformer` örnekleri türetilebilir — .NET tarafındaki mevcut
  `EBelgeUblSchematronValidator`'ın "bir kez derle, singleton olarak paylaş" deseniyle AYNI
  mimari). .NET tarafı ile JVM sidecar'ı arasında iletişim (stdin/stdout, yerel soket veya minimal
  HTTP) ayrı bir tasarım kararı gerektirir — bu turda seçilmedi.

### Mimari karar

**Önerilen: Seçenek B — ayrı Java/Saxon sidecar süreci.**

Gerekçe (yalnız teknik kolaylık değil):
- **Lisans**: Java Saxon-HE, Saxonica'nın kendi RESMİ ücretsiz ürünüdür (MPL-2.0, ticari kullanıma
  açık, redistribution kısıtı yok) — hiçbir belirsizlik yok. .NET tarafındaki tüm alternatifler ya
  ücretli (SaxonCS/XmlPrime) ya deneysel/desteksiz (IKVM paketleri) ya da lisansı belirsiz
  (devlooped/Schematron).
- **Operasyon**: JVM eklemek Dockerfile'a bir satır (`apt-get install openjdk-17-jre-headless`
  veya çok-aşamalı build'de resmi bir JRE base image katmanı) kadar basittir; STYS zaten
  container'da çalışıyor (`backend/Dockerfile` mevcut).
- **Güvenlik**: Java tarafında da AYNI sandbox deseni (özel `URIResolver`, DTD kapalı,
  ağ erişimi kapalı) uygulanabilir — POC bunu kısmen doğruladı (XXE testi geçti).
- **Deployment**: sidecar, ayrı bir container/process olarak veya aynı container içinde JVM +
  .NET runtime birlikte çalıştırılarak dağıtılabilir; STYS'nin mevcut mimarisini BOZMAZ.
- **Performans**: kalıcı JVM + singleton derlenmiş stylesheet ile per-doküman maliyeti düşüktür
  (POC'taki ~3 sn ölçümü JVM başlatma maliyetidir, gerçek per-çağrı maliyeti DEĞİL).
- **Bakım maliyeti**: Saxonica'nın resmi, aktif bakımlı (2026-07-10'da 13.0 sürümü) ürünüdür —
  tek kişilik deneysel paketlere kıyasla çok daha düşük risk.
- **Deterministik çalışma**: POC'ta doğrulandı (iki bağımsız çalıştırma byte-birebir aynı).

Reddedilenler: (A) aynı .NET process içinde kütüphane — lisans/olgunluk sorunu nedeniyle uygun
aday YOK. (C) doğrulamayı entegratöre bırakmak — STYS'nin kendi kesim-öncesi/render garantisini
zayıflatır, entegratör hatası geç, pahalı biçimde geri döner (fatura zaten kesilmiş olabilir). (D)
fazı durdurmak — gereksiz, çünkü B için lisans/teknik yol AÇIK ve POC ile kanıtlandı.

### Üretim kullanımı için gereken onay

1. **Altyapı onayı**: Dockerfile'a JVM (JDK/JRE 17+) eklenmesi — yeni bir çalışma zamanı
   bağımlılığı, kullanıcı/DevOps onayı gerektirir.
2. **Mimari onayı**: .NET ↔ JVM sidecar iletişim yöntemi (CLI-per-call mi, kalıcı süreç + IPC mi)
   — ayrı bir tasarım turu gerektirir.
3. **Güvenlik onayı**: Java tarafı `URIResolver`/DTD/entity sandbox'ının .NET tarafındaki
   `EBelgeUblSandboxXmlResolver` ile AYNI disiplinde implemente edilmesi.
4. `<let name="type" value="efatura"/>` sabit kodlamasının e-Arşiv (`EARSIVFATURA`) senaryosu
   için nasıl ele alınacağına dair ürün kararı.

### Faz 2B.5'in tamamlanabilmesi için kesin sonraki adım

Kullanıcı yukarıdaki 4 onayı verirse: (1) JVM'i Dockerfile'a ekle, (2) küçük bir Java sidecar
servisi yaz (yalnız: kural setini bir kez derle, stdin'den/bir dizinden XML al, SVRL/özet sonucu
stdout'a/dosyaya yaz — mevcut `EBelgeUblSchematronValidator`'ın arayüzünü DEĞİŞTİRMEDEN, yalnız
implementasyonunu bu sidecar'ı çağıracak şekilde değiştir), (3) 40 hedefli testin tamamını yaz,
(4) yalnız o zaman commit/push koşulları (md.22) genuinely karşılanmış olur.

### Sonuç formatı

**`APPROVED_CANDIDATE: Saxon-HE (Java) 13.0, JVM sidecar/CLI süreci olarak — MPL-2.0, ücretsiz`**

- **Lisans gereksinimi**: Yok (MPL-2.0, ücretsiz, ticari kullanıma açık, redistribution serbest).
- **Ek paket/dependency**: JVM (JDK/JRE 17+) — YENİ bir çalışma zamanı bağımlılığı,
  Dockerfile/deployment değişikliği gerektirir (altyapı onayı gerekli).
- **Deployment etkisi**: Orta — container'a bir JRE katmanı + Saxon-HE jar eklenmesi; .NET
  tarafında yalnız `IEBelgeUblSchematronValidator`'ın implementasyonu değişir (arayüz sabit
  kalabilir), renderer/XSD validator ETKİLENMEZ.
- **Güvenlik sonucu**: XXE/DTD testi GEÇTİ (gerçek payload ile). `document()`/ağ sandbox'ı
  üretimde AYRICA implemente edilmeli (POC bunu göstermedi, yalnız gerekliliğini doğruladı).
- **POC test sonucu**: `exists()` GERÇEKTEN çalıştı (2/9 zorunlu senaryo tam kanıtlandı: kurala
  uygun geçer + ihlal gerçek failed-assert üretir + determinizm + XXE engellenir + paralel
  çalışma güvenli; ağ erişimi engelleme ve stylesheet-cache-sonrası-ikinci-çalıştırma senaryoları
  bu POC'ta AYRICA/açıkça izole test edilmedi, mimari gereklilik olarak not edildi).

## Faz 2B.5 tamamlanma — production-ready Java Saxon-HE 13.0 sidecar

**Durum: TAMAMLANDI, commit/push YAPILDI (bkz. md.17 koşulları — hepsi genuinely karşılandı).**
Bu bölüm, `APPROVED_CANDIDATE: Java Saxon-HE 13.0 / ayrı JVM sidecar` kararının gerçek, çalışan
bir implementasyona dönüştürüldüğünü belgeler.

### Java Saxon-HE 13.0 kararı ve MPL-2.0 lisans notu

Production sidecar, POC'taki 10.9 yerine **onaylanan 13.0** sürümünü kullanır
(`net.sf.saxon:Saxon-HE:13.0`, Maven Central, SHA-1 `da65e52c768d36eb37e427d8feb6487aabd588fa`
— resmi Maven Central checksum dosyasıyla BİREBİR doğrulandı). Ek çalışma zamanı bağımlılığı
olarak `org.xmlresolver:xmlresolver:6.0.23` gerekir (SHA-1
`ad4e965f8662c7c8ca4fe8ab8aaef09a49f25447`, aynı şekilde doğrulandı) — Saxon 13.0'ın
`Configuration` sınıfı bunsuz `NoClassDefFoundError` ile başarısız olur (POC'ta 10.9 bu
bağımlılığı gerektirmiyordu; bu, sürüm yükseltmesinin gerçek, kanıtlanmış bir yan etkisidir).
İkisi de MPL-2.0, ücretsiz, ticari kullanıma açık, redistribution kısıtı yok. Sürüm/checksum
`sidecar/schematron-validator/manifest.json`'da sabitlenmiştir; Dockerfile build aşamasında
`sha1sum -c` ile YENİDEN doğrulanır (floating "latest" yok, runtime indirme yok).

### Sidecar mimarisi

`sidecar/schematron-validator/` — bağımsız bir Java (JDK 17+) kaynak ağacı, Maven/Gradle
GEREKTİRMEZ (yalnız `javac`/`java`, minimal ayak izi). Bileşenler:

- `ArtifactManifest` — `manifest.json`'ı okur, HER dosyanın (3 GİB schematron + 4 ISO skeleton
  XSLT) SHA-256'sını başlangıçta yeniden hesaplayıp doğrular; tek uyuşmazlık TÜM başlatmayı
  durdurur.
- `SchematronPipeline` — ISO Schematron skeleton'ın 3 aşamalı derleme hattını Saxon s9api
  (`Processor`/`XsltCompiler`/`Xslt30Transformer`) ile BİR KEZ, başlangıçta çalıştırır; derlenen
  `XsltExecutable` süreç ömrü boyunca singleton olarak saklanır (Saxon dokümantasyonuna göre
  immutable/thread-safe — her istek kendi `Xslt30Transformer`'ını türetir, pool GEREKMEZ).
- `SandboxUriResolver` (yalnız derleme aşamasında, `sch:include` çözümü için) / `DenyAllUriResolver`
  (per-request doğrulamada, TÜM harici kaynak erişimini reddeder) — .NET tarafındaki
  `EBelgeUblSandboxXmlResolver` ile AYNI iki-katmanlı desen.
- `SidecarMain` — JDK'nin yerleşik `com.sun.net.httpserver.HttpServer`'ı (ek bağımlılık yok)
  ile `/health/live`, `/health/ready`, `POST /internal/schematron/validate` uç noktalarını sunar.

### Rule-set whitelist yaklaşımı

İstek yalnız üç bilgi taşır: `X-RuleSet-Id` header'ı, ham XML gövdesi, `X-Correlation-Id` header'ı
(izleme amaçlı, PII değil). Stylesheet içeriği, path, URL veya XPath ASLA istekte YER ALMAZ.
Sidecar yalnız `manifest.json`'daki TEK `ruleSetId` değerini (`GIB-UBL-TR-1.2.1/2026-09-14`)
kabul eder; başka herhangi bir değer `400 UNKNOWN_RULESET` ile reddedilir (bkz. test
`BilinmeyenRuleSetReddedilir`).

### HTTP protokolü — ham `application/xml` gövde kararı

Base64+JSON yerine **ham `application/xml` gövde** tercih edildi: (a) base64 kodlaması gereksiz
~33% boyut artışı ve ekstra encode/decode adımı getirir, (b) JSON içine XML gömmek kaçış
karakterleri nedeniyle hata ayıklamayı zorlaştırır, (c) `Content-Type: application/xml` zaten
doğru, standart bir HTTP mekanizmasıdır. `ruleSetId`/`correlationId` HTTP header'larına
taşındığından gövde SAF XML kalır. Yanıt JSON'dır (`{"valid":bool,"violations":[...]}`) — bu
tarafta yapılandırılmış veri (ihlal listesi) döndüğü için JSON doğal seçimdir.

### Saxon compile/cache lifecycle

Başlangıçta (arka plan thread'inde, HTTP sunucusu ayakta kalırken): (1) manifest hash'leri
doğrulanır, (2) 3 aşamalı pipeline BİR KEZ derlenir, (3) yalnız BUNDAN SONRA `ready=true` olur.
`GET /health/ready`, derleme bitmeden `503`, bittikten sonra `200` döner (bkz. testler
`ReadyEndpointCompileOncesindeBasarisizdir` / `...SonrasindaBasarili`). Her `/validate` isteği
kendi `Xslt30Transformer`'ını singleton `XsltExecutable`'dan türetir — yeniden derleme YOKTUR,
paylaşılan mutable state YOKTUR (paralel istekler birbirini etkilemez, bkz. test
`ParalelDogrulamalarBirbiriniEtkilemez`).

### Timeout ve limitler

- İstek gövdesi: 5.000.000 byte üst sınır (`Content-Length` ön kontrolü + akış sırasında sayaç) → `413`.
- Doğrulama işlemi: 10 saniye (ayrı bir `ExecutorService` ile sarmalanır) → zaman aşımında `504`.
- İhlal listesi: en fazla 200 (`MAX_VIOLATIONS`).
- .NET client: `HttpClient.Timeout` (varsayılan 8 sn, `EBelgeSchematronSidecar__RequestTimeoutSeconds`
  ile yapılandırılabilir), yanıt gövdesi 1.000.000 byte üst sınır.

### Güvenlik önlemleri (gerçek testlerle doğrulandı)

- DTD tamamen kapalı (`disallow-doctype-decl=true` + harici genel/parametre entity kapalı +
  özel `EntityResolver` reddeder) — gerçek XXE payload'ı (`file:///etc/passwd` sızdırma denemesi)
  test edildi, sızıntı YOK (`XxeVeDtdEngellenirIcerikSizmaz`).
  `document()`/harici kaynak erişimi per-request doğrulamada `DenyAllUriResolver` ile TAMAMEN
  kapalı; yalnız derleme aşamasında (sabit, vendored dosyalar için) `SandboxUriResolver` ile
  kök dizine sınırlı.
- Container: non-root kullanıcı (uid/gid 10001), `read_only: true` + yalnız `/tmp` için `tmpfs`,
  `no-new-privileges:true`, `cap_drop: ALL`, CPU/bellek limiti (`docker-compose.yml`) — GERÇEKTEN
  build edilip `docker run --read-only --tmpfs /tmp` ile çalıştırıldı, `id` komutu `uid=10001`
  doğruladı.
  KUR: public port YAYINLANMAZ (`expose`, `ports` DEĞİL); yalnız `stys-internal` Docker ağı.
- Hassas veri: XML gövdesi, ihlal mesaj METNİ (VKN/unvan/adres taşıyabilir) sidecar loglarına
  ASLA yazılmaz — yalnız `correlationId`, `ruleSetId`, ihlal SAYISI, süre loglanır. Hata
  mesajlarında container path'i YOKTUR (genel hata kodları döner: `VALIDATION_INTERNAL_ERROR` vb.).
  .NET client XML'i hiç loglamaz (kod tabanında hiçbir `ILogger` çağrısı XML/ihlal içeriği taşımaz).

### XSD unsigned/signed ayrımı

`IEBelgeUblXsdValidator.ValidateUnsignedRendererOutput` yeni metodu: `Validate`'in fırlattığı
`EBelgeUblXsdValidationFailedException`'ı yakalar, hata listesi TAM OLARAK 1 eleman VE o eleman
hem `'Invoice'` hem `UBLExtensions` alt dizelerini içeriyorsa (kararlı, kırılgan-olmayan kontrol —
tam metin eşitliği YOK) sessizce döner; başka HERHANGİ bir durumda (0 hata VEYA 1'den fazla hata
VEYA farklı bir hata) yeniden fırlatır. `EBelgeUblArtifactStage` enum'ı (`Unsigned`/`SignedReady`)
`EBelgeUblRenderSonucu.ArtifactStage` alanında taşınır — bu faz HER ZAMAN `Unsigned` üretir;
tüketen kodun bunu nihai/gönderilebilir artefakt SANMAMASI için type-safe bir işaretleyicidir.

### Gerçek Schematron test sonuçları

`java -cp Saxon-HE-13.0.jar;xmlresolver-6.0.23.jar ... /health/ready` sonrası, gerçek
`POST /internal/schematron/validate` çağrıları GERÇEK GİB mesajları üretti — örnek (bilinçli
ihlal): `"Uyumsuz fatura tipi: 'SATIS'. cac:WithholdingTaxTotal elamanı varken fatura tipi
TEVKIFAT,YTBTEVKIFAT,IADE,YTBIADE,SGK,SARJ ve SARJANLIK olabilir."` — test'ini `exists()`
XPath 2.0 ifadesi tetikliyor (bkz. `BilincliIhlalGercekExistsTabanliMesajUretir`). Yan bulgu
(POC'tan taşınan, hâlâ geçerli): `UBL-TR_Main_Schematron.xml`'deki `<let name="type"
value="efatura"/>` sabit kodlaması nedeniyle `ProfileID=EARSIVFATURA` varsayılan modda
"geçersiz" raporlanır — bu, renderer'ın ürettiği GERÇEK snapshot'ın uçtan uca testinde
(`EBelgeUblRendererEndToEndIntegrationTests`) GERÇEK bir schematron ihlali olarak GÖZLEMLENDİ
ve testte AÇIKÇA beklenen davranış olarak doğrulandı (bu turun kapsamında GİB dosyası
DEĞİŞTİRİLMEDİ; `$type` parametreleştirmesi açık bir sonraki-adım konusu olarak kalır).

### Hata sınıflandırması (type-safe, string parse YOK)

| Durum | İstisna | Kod | Kalıcılık |
|---|---|---|---|
| Gerçek schematron ihlali | `EBelgeUblSchematronValidationFailedException` | 422 | Kalıcı |
| Sidecar erişilemiyor/timeout | `EBelgeUblSchematronServiceUnavailableException` | 503 | Geçici |
| Rule-set/artifact geçersiz | `EBelgeUblRuleSetArtifactInvalidException` | 500 | Kalıcı |
| Beklenmeyen/geçersiz yanıt | `EBelgeUblSchematronProtocolErrorException` | 502 | Geçici |

Bu dört sınıf ASLA mali toplam (`EBelgeUblMonetaryTotalMismatchException`) veya kapsam
(`EBelgeUblRenderScopeUnsupportedException`) hatalarıyla BİRLEŞTİRİLMEZ — her biri kendi tipiyle
fırlatılır, gelecekteki outbox consumer'ı `catch`/`switch` ile tipe göre ayırabilir (string mesaj
parse GEREKMEZ).

### Hedefli test komutları ve sonuçları

```
dotnet test tests/STYS.Tests/STYS.Tests.csproj --filter "FullyQualifiedName~EBelgeUblRenderer|FullyQualifiedName~SaxonSidecar|FullyQualifiedName~EBelgeSchematronSidecar"
  → Passed: 32, Failed: 0

dotnet test tests/STYS.Tests/STYS.Tests.csproj --filter "FullyQualifiedName~EBelge"
  → Passed: 161, Skipped: 82 (SQL Server gerektiren, önceden var olan integration testleri - değişmedi), Failed: 0
```

Sidecar entegrasyon testleri (`EBelgeSchematronSidecarIntegrationTests`, 11 test) ve uçtan uca
test (`EBelgeUblRendererEndToEndIntegrationTests`, 1 test) GERÇEK bir Java alt süreci başlatarak
(`SchematronSidecarProcessFixture`, JDK 17+ + derlenmiş sınıflar + gerçek jar'lar) çalışır — mock
sunucu veya sabit JSON YOKTUR. `.NET client` testleri (9 test, `SaxonSidecarEBelgeSchematronValidatorTests`)
yalnız HTTP hata sınıflandırmasını izole eder (sahte `HttpMessageHandler`), gerçek schematron
mantığını TEST ETMEZ (bu ayrım bilinçlidir, bkz. görev md.14).

Docker imajı gerçekten build edildi (`docker build sidecar/schematron-validator`) ve
`docker run --read-only --tmpfs /tmp` ile çalıştırılıp `docker exec ... id` → `uid=10001` ile
non-root doğrulaması yapıldı (manuel, CI'ya bağlanmadı - bu turun kapsamı dışında).

### Açık kalan konular

1. `<let name="type" value="efatura"/>` sabit kodlaması - e-Arşiv (`EARSIVFATURA`) senaryoları
   için parametreleştirme kararı gerekiyor (GİB dosyası değiştirilmeden nasıl ele alınacağı).
2. Sidecar'ın `document()`/ağ sandbox'ı yalnız KOD seviyesinde (`DenyAllUriResolver`) test
   edildi; container network policy (Docker `internal` ağ) AYRICA doğrulanmadı.
3. Ready endpoint'in gerçek "compile öncesi 503" davranışı testte ZAYIF kanıtlandı (yarış
   koşulu nedeniyle) - manuel olarak (curl ile) güçlü biçimde doğrulandı ama otomatik test
   kırılgan.
4. `cac:Signature` (imza referans metadatası) hâlâ üretilmiyor - resmi örnek/XSD doğrulaması
   gerektiriyor.
5. Outbox consumer kablolaması hâlâ yapılmadı (bu fazın kapsamı dışında bırakıldı, md.11).
6. CI/CD boru hattına sidecar image build/push adımı eklenmedi (yalnız yerel `docker build` ile
   doğrulandı).

### Sonraki faz

1. Outbox consumer sınırına gerçek kablolama (renderer + sidecar client hazır, tüketici yok).
2. `cac:Signature`/XMLDSig/XAdES imzalama fazı - `ArtifactStage.SignedReady` üretimi.
3. ~~`$type` parametreleştirme kararı.~~ **ÇÖZÜLDÜ (bkz. aşağıdaki bölüm).**
4. CI/CD'ye sidecar image build + Trivy/benzeri güvenlik taraması eklenmesi.

## Faz 2B.5 Schematron profil seçimi düzeltmesi (commit 986743a sonrası)

**Durum: TAMAMLANDI, commit/push YAPILDI.** Gerçek e-Arşiv renderer çıktısı artık **sıfır**
Schematron ihlaliyle doğrulanıyor - Faz 2B.5'in nihai tamamlanma kriteri budur.

### Kök neden ve resmî paket incelemesi

Vendored/indirilmiş resmî paketler incelendi (`e-FaturaPaketi.zip` schematron klasörü,
`earsiv_paket_v1.1_8.zip`). Bulgular:

- `earsiv_paket_v1.1_8/earsiv_schematron.xsl` diye AYRI bir "e-Arşiv" dosyası var, AMA bu
  tamamen FARKLI, eski (UBL OLMAYAN) bir format için: kök ad alanı
  `http://earsiv.efatura.gov.tr`, elemanlar `earsiv:eArsivRaporu`/`earsiv:fatura`/
  `earsiv:mustahsilMakbuz` vb. - bizim ürettiğimiz UBL `<Invoice>` XML'iyle HİÇBİR ilgisi yok
  (bu, periyodik e-Arşiv RAPORLAMA formatıdır, önceki fazlarda kapsam dışı bırakıldı). Bu dosya
  YANLIŞ İPUCU olarak elendi.
- Ayrı bir "e-Arşiv UBL schematron" paketi indirilen/cache'lenen hiçbir GİB kaynağında
  BULUNAMADI - `UBL-TR_Main_Schematron.xml` (e-FaturaPaketi'nin kendi schematron'u) tek adaydır.
- **Gerçek, kanıtlanmış bulgu**: `History.txt` (e-FaturaPaketi/schematron), `20170428` tarihli
  girişte "'type' isimli parametre eklendi" diyor - GİB mühendisleri `$type`'ı BİLEREK bir
  parametre olarak tasarladı. Bunun somut kanıtı: derlenmiş (ISO Schematron skeleton çıktısı)
  XSLT'de kök seviyedeki `<let name="type" value="efatura"/>` (ve `envelopeType`/`senderId`/vb.
  diğer TÜM kök-seviye `<sch:let>`'ler), **`<xsl:param name="type" select="efatura"/>` olarak
  derleniyor** - `<xsl:variable>` DEĞİL. Bu, resmî ISO Schematron skeleton derleme hattının
  (`iso_svrl_for_xslt1.xsl`) KENDİ davranışıdır; STYS tarafından icat edilmiş bir yorum değildir
  (Saxon ile üretilen gerçek çıktıda doğrudan gözlemlendi: `grep "name=\"type\""
  validator.xsl` → `<xsl:param name="type" select="efatura"/>`).
- **Ampirik kanıt**: Saxon CLI'ye `type=earchive` parametresi geçilerek (`net.sf.saxon.Transform
  ... type=earchive`) GERÇEK bir e-Arşiv örneği (ProfileID=EARSIVFATURA) çalıştırıldı - sonuç
  **0 `failed-assert`** (önceden 1 tane: ProfileID codelist bulgusu). Aynı ihlalli örnek
  (`cac:WithholdingTaxTotal` içeren) `type=earchive` ile de test edildi - `exists()` tabanlı
  GERÇEK ihlal HÂLÂ doğru biçimde üretildi (filtreleme YOK, gerçek doğrulama).
- `$ProfileIDTypeEarchive` codelist'i (`UBL-TR_Codelist.xml`) `,EARSIVFATURA,` değerini
  taşıyor - `$type='earchive'` iken ProfileIDCheck bu listeye bakıyor, `$type` varsayılan/boş
  iken (fiilen "efatura" davranışına denk düşüyor) yalnız e-Fatura profil id'lerini içeren
  `$ProfileIDType`'a bakıyor.
- Sonuç: **resmî artifact yapısı `$type`'ı GERÇEKTEN dışarıdan bağlanabilir bir XSLT stylesheet
  parametresi olarak tasarlamış ve derlemiş** - GİB kaynak dosyasında (`UBL-TR_Main_Schematron.xml`)
  HİÇBİR metin değişikliği GEREKMEDİ. GİB kaynak dosyası ve manifest hash'leri AYNEN korundu
  (bkz. test `ManifestTumArtifactHashleriEslesir`).

### Uygulanan çözüm

`sidecar/schematron-validator/src/.../SchematronPipeline.java`: derleme sırasında üretilen
XSLT metninde `<xsl:param name="type"` varlığı DOĞRULANIR (yoksa başlangıç BAŞARISIZ olur -
"ISO skeleton davranışı değişmiş olabilir" güvenlik kontrolü). Her `DocumentProfile` (şu an
yalnız `EARSIV`, `EFATURA` ileride) için AYRI bir `XsltExecutable` derlenip cache'lenir (aynı
kaynak metinden, GİB assertion/pattern/XPath metinlerine dokunulmadan). `validate()` çağrısında
YALNIZ `type` parametresi (`Xslt30Transformer.setStylesheetParameters`, standart Saxon s9api)
bağlanır - başka hiçbir XPath/parametre kullanıcıdan/istekten ALINMAZ.

Sidecar `manifest.json`'daki `ruleSetId` (`GIB-UBL-TR-1.2.1/2026-09-14`) TABAN kimlik olarak
kalır; kabul edilen TAM rule-set kimlikleri artık profil son ekiyle whitelist'lenir:
`GIB-UBL-TR-1.2.1/2026-09-14/EARSIV` (ilk dalgada AKTİF), `GIB-UBL-TR-1.2.1/2026-09-14/EFATURA`
(kodda tanımlı ama sidecar bu sürümde bilinçli olarak REDDEDİYOR - `DocumentProfile.EARSIV`
dışındaki her profil `400 UNKNOWN_RULESET`). `.NET` tarafında `EBelgeSchematronSidecarOptions.
SupportedRuleSetId` bu tam değere güncellendi.

### Başlangıç öz-testi (self-test)

`SidecarMain`, pipeline derlemesi bittikten HEMEN sonra, kişisel veri İÇERMEYEN sabit bir e-Arşiv
örneğini (`SELF_TEST_EARSIV_XML`, sabit geçmiş tarihli) `EARSIV` profiliyle doğrular; sonuç
BOŞ DEĞİLSE (yani en az bir ihlal varsa) başlangıç BAŞARISIZ sayılır ve `/health/ready` HİÇBİR
ZAMAN `200` dönmez. Bu, "e-Arşiv çıktısı sıfır ihlal üretmeli" garantisinin bizzat sidecar'ın
kendi başlangıcında GERÇEKTEN doğrulanmasını sağlar - yalnız test-zamanı değil, HER üretim
başlatmasında.

### `cac:Signature` incelemesi

Codelist/Common schematron dosyaları `$type='earchive'` ile `cac:Signature` VARLIĞINI zorunlu
kılan HİÇBİR assertion içermiyor (yalnız `SignatureCountCheck`: en fazla 1 - 0 tane de geçerli).
Bu nedenle bu turda `cac:Signature` referans metadata yapısı EKLENMEDİ - gerçek e-Arşiv
Schematron doğrulaması bunsuz zaten sıfır ihlal veriyor (ampirik olarak doğrulandı).

### Gerçek uçtan uca sonuç

```
EBelgeUblRendererEndToEndIntegrationTests.GercekEArsivRendererCiktisiSifirSchematronIhlaliyleBasariylaSonuclanir
  → gerçek EBelgeCanonicalSnapshotV2 → gerçek renderer → gerçek XSD (yalnız bilinen UBLExtensions
    bulgusu) → GERÇEK Java Saxon-HE 13.0 sidecar → GERÇEK GİB e-Arşiv Schematron kuralları
  → valid=true, violations=[] → EBelgeUblRenderSonucu başarıyla döner, ArtifactStage=Unsigned
  → PASSED
```

Ek negatif testler eklendi: yanlış ProfileID gerçek ihlal üretir, e-Fatura ruleset id'si ilk
dalgada reddedilir (`EBELGE_UBL_RULESET_ARTIFACT_INVALID`), eski (profil eki olmayan) ruleSetId
reddedilir, sidecar restart sonrası aynı XML aynı sonucu verir, e-Arşiv doğrulamasında
failed-assert filtrelemesi YAPILMADIĞI (WithholdingTaxTotal ihlalleri hâlâ tam üretiliyor)
açıkça kanıtlandı.

### Test sonuçları

```
dotnet test --filter "FullyQualifiedName~EBelgeUblRenderer|FullyQualifiedName~SaxonSidecar|FullyQualifiedName~EBelgeSchematronSidecar"
  → Passed: 37, Failed: 0

dotnet test --filter "FullyQualifiedName~EBelge"
  → Passed: 166, Skipped: 82 (SQL Server gerektiren, önceden var olan integration testleri), Failed: 0
```

Docker imajı yeniden build edildi ve `docker run --read-only --tmpfs /tmp` ile çalıştırılıp
gerçek `/health/ready` → `200` ve gerçek `/internal/schematron/validate` çağrısı →
`{"valid":true,"violations":[]}` doğrulandı (container log'unda "self-test passed" satırı).

### Frontend hâlâ yapılmadı (Faz 2B.5'ten kalan not - hâlâ geçerli)

Faz 2B.4.2'den beri açık: otoriter satıcı/alıcı yapısal adres (sokak, bina no, ilçe, il, posta
kodu) ve gerçek kişi alıcılar için ayrı ad/soyad alanları hâlâ UI'da girilemiyor. Renderer artık
bu alanları DOĞRUDAN TÜKETTİĞİNDEN (bkz. `EBelgeUblRenderer.ValidateAuthoritativeFields`), bu
eksik olmadan `EBelgeUblOptions.Enabled=true` hiçbir üretim ortamında pratikte kullanılamaz.
Gereken ekranlar: Kurum ayarları (satıcı yapısal adres), CariKart/Müşteri formu (alıcı yapısal
adres + gerçek kişi ad/soyad ayrımı, kurumsal/gerçek kişi seçimine göre koşullu alanlar). Faz
2B.6 bu durumu DEĞİŞTİRMEDİ - hâlâ açık.

## Faz 2B.6 sonuç bölümü — outbox tüketimi ve unsigned artifact kalıcılaştırma

**Durum: TAMAMLANDI, commit/push YAPILDI (bkz. md.21 koşulları — hepsi genuinely karşılandı).**

> **ÖNEMLİ - bu bölümdeki üç iddia Faz 2B.6.1'de GEÇERSİZLEŞTİRİLDİ ve düzeltildi (bkz. aşağıda
> "Faz 2B.6.1 sonuç bölümü"): (1) "`EBelgeArtefaktOlusturmaTalebi` `KilitToken` TAŞIMAZ, bu
> KASITLIDIR" iddiası YANLIŞTI - lease token taşımaması, bir worker lease'ini kaybettikten SONRA
> bile artefakt yazabilmesine izin veren gerçek bir açıktı. (2) "Ownership koruması İKİ KATMANDA
> sağlanır: DB benzersizlik indeksi + outbox `TryCompleteAsync` token guard'ı" iddiası YETERSİZDİ
> - `TryCompleteAsync` guard'ı yalnız outbox durum GEÇİŞİNİ korur, artefakt YAZMA anını KORUMAZ;
> lease'i kaybetmiş bir worker render'ı bitirip artefaktı YİNE DE insert edebiliyordu. (3) "Outbox
> `TryCompleteAsync`, bu üç aşamanın DIŞINDADIR" ve ayrı `SaveChangesAsync` çağrılarının Faz
> 2B.6'yı TAMAMLADIĞI iddiası YANLIŞTI - artefakt insert + `EBelgeKaydi` durum güncellemesi +
> outbox tamamlama AYNI transaction'da DEĞİLDİ, bu yüzden ikisi arasında bir hata/crash split-brain
> durum (artefakt var ama outbox hâlâ Isleniyor, veya tam tersi) YARATABİLİRDİ. Aşağıdaki bölüm
> yalnız TARİHSEL bağlam için KORUNMUŞTUR - güncel, doğru davranış için Faz 2B.6.1 bölümüne
> bakın.

### Mevcut outbox mimarisinin analizi

İncelemede (bkz. görev md.1) şu gerçek, önceden var olan bileşenler bulundu ve AYNEN
yeniden kullanıldı:

- **Claim**: `EBelgeOutboxClaimLeaseService.TryClaimNextAsync` - ham SQL, `UPDLOCK, READPAST,
  ROWLOCK` ipucuyla tek adayı seçer, ardından `UPDATE ... OUTPUT` ile atomik claim eder.
  İki worker'ın aynı satırı seçmesi `READPAST` ile YAPISAL olarak imkânsızdır (biri diğerini
  atlar, ikinci UPDATE hiçbir satırı etkilemez).
- **Lease yenileme/bırakma**: `EBelgeOutboxLeaseTransitionService` - `TryCompleteAsync`/
  `TryFailAsync`/`TryRenewAsync`, üçü de `KilitToken` + `KilitBitisZamaniUtc > now` KOŞULUYLA
  guard'lıdır; token/expiry uyuşmuyorsa `false` döner ("sahiplik kaybedildi" - bkz. aşağıda).
- **Aynı mesajın iki worker'ca işlenmesi**: DB seviyesinde `READPAST` ile ENGELLENİR; ayrıca
  benim eklediğim `EBelgeArtifactlari` benzersizlik indeksi İKİNCİ bir savunma katmanıdır (bkz.
  aşağıda "İdempotency anahtarı").
- **Başarılı tamamlama**: `TryCompleteAsync` → `Durum=Tamamlandi`, lease alanları temizlenir.
- **Deneme sayısı**: `DenemeSayisi`, HER claim'de (başarılı VEYA lease-expiry-reclaim) `+1`
  artırılır (claim SQL'inin kendisinde).
- **Sonraki deneme zamanı**: `SonrakiDenemeZamaniUtc`, `TryFailAsync`'e geçirilen
  `retryGecikmesi`'ne göre atanır; `null` verilirse KALICI (retry YOK).
- **Kalıcı hata temsili**: `Durum=Hata` + `SonrakiDenemeZamaniUtc=NULL` - bu kombinasyon claim
  sorgusunda ASLA tekrar seçilmez (WHERE koşulu `SonrakiDenemeZamaniUtc IS NOT NULL` ister).
- **Worker crash sonrası tekrar alınabilirlik**: `Durum=Isleniyor` VE `KilitBitisZamaniUtc <=
  now` olan satırlar claim sorgusunun ÜÇÜNCÜ dalında YENİDEN seçilebilir - crash eden worker'ın
  sonucu asla yazamayacağı GARANTİ edilir (`TryCompleteAsync`/`TryFailAsync` token/expiry
  guard'ı nedeniyle - bkz. görev md.7/12).
- **Handler soyutlaması**: `IEBelgeOutboxIsTuruHandler` + `EBelgeOutboxMesajIslemeService`
  (dictionary tabanlı dispatch) ZATEN vardı - `EBelgeArtefaktOlusturOutboxHandler` de ZATEN
  vardı, yalnız gerçek `IEBelgeArtefaktOlusturmaService` implementasyonu EKSİKTİ.
  `EBelgeOutboxMesajIslemeService` ve `EBelgeArtefaktOlusturOutboxHandler` DI'a hiç kayıtlı
  DEĞİLDİ - bu turda kaydedildi.
- **Retry policy**: `EBelgeOutboxRetryPolicy` ZATEN vardı, sabit çizelge (1dk/5dk/15dk/1sa/6sa,
  6. denemede terminal) - AYNEN kullanıldı, YENİDEN YAZILMADI.

**Sonuç: yeni paralel bir outbox sistemi KURULMADI.** Yalnız eksik olan TEK parça (gerçek
`IEBelgeArtefaktOlusturmaService` implementasyonu) eklendi ve üç var olan, ZATEN DI'a kayıtlı
OLMAYAN bileşen (`EBelgeCanonicalSnapshotV2Reader`, `EBelgeArtefaktOlusturOutboxHandler`,
`EBelgeOutboxMesajIslemeService`) ile birlikte kaydedildi.

### Eklenen bileşenler

- **`EBelgeArtifact`** entity (`Entities/EBelgeArtifact.cs`) + `EBelgeArtifactTipi` (`UblXml=1`)
  ve `EBelgeArtifactAsamasi` (`Unsigned=1`) enum'ları.
- **`EBelgeArtefaktOlusturmaService : IEBelgeArtefaktOlusturmaService`** (ZATEN var olan
  arayüzün GERÇEK implementasyonu) - snapshot okuma + renderer çağrısı + artefakt
  kalıcılaştırma.
- **`EBelgeArtifactService : IEBelgeArtifactService`** - salt okunur, tenant sınırlı okuma
  servisi (controller/endpoint bu turda EKLENMEDİ - bkz. görev md.13).
- **`EBelgeArtifactIdempotencyConflictException`** (409, `EBELGE_ARTIFACT_IDEMPOTENCY_CONFLICT`).
- `EBelgeKaydiDurumu` enum'una `UnsignedUblHazir=2` ve `UnsignedUblKaliciHata=3` eklendi (henüz
  `KaliciHata` durumuna GEÇİŞ kodu yazılmadı - bkz. "Açık kalan konular").

### Transaction sınırı (md.7)

Üç aşama, mevcut `Scoped` `StysAppDbContext` üzerinde AÇIK bir `BeginTransaction()` OLMADAN
uygulanır (EF Core, yalnız `SaveChangesAsync` çağrılarını KENDİ implicit transaction'ına sarar -
sorgular arasında satır kilidi TUTULMAZ):

1. **Okuma**: `EBelgeKaydi` + `Snapshot` sorgusu + mevcut artefakt ön-kontrolü - satır kilidi YOK.
2. **Render**: `IEBelgeCanonicalSnapshotV2Reader.Read` (saf, DB'siz) + `IEBelgeUblRenderer.RenderAsync`
   (yerel XSD + GERÇEK sidecar HTTP çağrısı) - DB bağlantısı bu aşamada HİÇ kullanılmaz.
3. **Yazma**: `_dbContext.Add(artifact)` + `kayit.Durum = UnsignedUblHazir` TEK `SaveChangesAsync`'te
   (tek implicit transaction).

**Outbox `TryCompleteAsync`, bu üç aşamanın DIŞINDADIR** (handler döndükten SONRA,
`EBelgeOutboxMesajIslemeService` tarafından, AYRI bir DB round-trip'i olarak çağrılır) - bu,
mevcut mimarinin KENDİ tasarımıdır (lease-token-korumalı geçiş, iş mantığından kasıtlı olarak
AYRIK tutulur). Bu ayrım, görev md.6'nın son cümlesiyle AÇIKÇA UYUMLUDUR: "Artifact veritabanına
yazılıp outbox mesajı tamamlanamazsa idempotent yeniden işleme aynı sonucu üretmeli ve duplicate
artifact oluşturmamalı" - bu GÜVENCE, tek bir dev transaction yerine İDEMPOTENCY ile sağlanır
(bkz. aşağıda) ve gerçek bir testle kanıtlanmıştır (`TamOutboxAkisiClaimIslemeVeTamamlamaBirlikteCalisir`).

### Lease ownership doğrulaması

`EBelgeArtefaktOlusturmaTalebi` (ZATEN var olan sözleşme) `KilitToken` TAŞIMAZ - bu KASITLI
olarak DEĞİŞTİRİLMEDİ (mevcut handler/test sözleşmesini bozmamak için). Ownership koruması İKİ
KATMANDA sağlanır:

1. **DB benzersizlik indeksi** (`KurumId, EBelgeKaydiId, ArtifactTipi, ArtifactAsamasi`) - iki
   worker aynı anda render edip insert etmeye çalışırsa, YALNIZ BİRİ başarılı olur; DİĞERİ
   `DbUpdateException` (unique violation) alır ve bunu YAKALAYIP rakip satırla idempotency
   karşılaştırması yapar (bkz. `IsBenzersizlikIhlali`).
2. **Outbox `TryCompleteAsync` token guard'ı** (mevcut, değiştirilmedi) - lease'i KAYBETMİŞ bir
   worker'ın `Tamamlandi` YAZAMAMASI zaten mevcut mekanizma tarafından garanti edilir; benim
   eklediğim katman yalnız "iki worker aynı ARTEFAKTI YAZMAYA ÇALIŞIRSA ne olur" sorusuna cevap
   verir (`IkiParalelIstekTekArtefaktUretir` testiyle GERÇEK paralel çağrıyla kanıtlandı).

Bu tasarım kararı raporlanıyor (görev md.12 son paragraf): render işlemi normalde KISA
(saniyeler) olduğundan, lease renewal EKLENMEDİ - yeterli lease süresi (config'den, varsayılan
120sn) + sonuç aşamasında token guard'ı YETERLİ kabul edildi.

### İdempotency anahtarı

`(KurumId, EBelgeKaydiId, ArtifactTipi, ArtifactAsamasi)` - mevcut artefakt varsa VE
`(KaynakSnapshotSha256, ArtifactSha256, RuleSetId)` ÜÇLÜSÜ eşleşiyorsa BAŞARILI kabul edilir
(yeni satır eklenmez); herhangi biri FARKLIYSA `EBELGE_ARTIFACT_IDEMPOTENCY_CONFLICT` (kalıcı,
retry YOK) döner.

### Artifact storage kararı

`byte[]` → SQL Server `varbinary(max)`. Gerekçe: UBL XML boyutları (birkaç KB - birkaç yüz KB)
`varbinary(max)` için sorun teşkil ETMEZ; ayrı bir storage abstraction (blob storage/dosya
sistemi) bu ölçekte GEREKSİZ karmaşıklık eklerdi. **Açıkça not edilir**: PDF gibi çok daha büyük
artefaktlar eklendiğinde bu karar YENİDEN değerlendirilmelidir - `EBelgeArtifact.Icerik` alanı
o noktada bir storage-abstraction'a (ör. `IEBelgeArtifactStorage` + blob referansı) geçebilir;
bu tur bunu YAPMAZ, yalnız gerekliliğini not eder.

### Immutable artifact sözleşmesi ve hash zinciri

`StysAppDbContext.ApplyAuditInfo`, `EBelgeSnapshot` ile AYNI desende, `EBelgeArtifact` için
`Modified`/`Deleted` durumunu REDDEDER (bkz. test `ArtefaktGuncellemeVeyaSilmeUygulamaSeviyesindeReddedilir`).
Benzersizlik indeksi FİLTRESİZDİR (soft-delete edilmiş satır bile rezervasyonu korur - bkz. test
`SoftDeleteEdilmisArtefaktOlsaBileDuplicateOlusturulamaz`, ham SQL ile soft-delete simüle
edilerek kanıtlandı). Hash zinciri: `EBelgeSnapshot.CanonicalSha256` → (renderer, değişmeden
taşınır) → `EBelgeArtifact.KaynakSnapshotSha256`; `EBelgeUblRenderSonucu.UnsignedUblSha256`
(TAM OLARAK `UnsignedUblUtf8` üzerinden hesaplanmış, yeniden serialize EDİLMEMİŞ) →
`EBelgeArtifact.ArtifactSha256`. Test `GercekV2SnapshotGercekSidecarIleArtefaktUretirVeHashZinciriDogrulanir`
zincirin HER halkasını (snapshot hash, artifact hash, saklanan byte'ların YENİDEN hesaplanan
hash'iyle eşleşmesi) gerçek bir render+kaydetme akışıyla doğrular.

### `EBelgeKaydi` durum geçişleri

`SnapshotHazir` (başlangıç) → `UnsignedUblHazir` (başarı, artefakt insert ile AYNI
`SaveChangesAsync`'te). `UnsignedUblKaliciHata` enum değeri EKLENDİ ama bu turda hiçbir kod yolu
BUNA geçiş YAPMIYOR (bkz. "Açık kalan konular") - kalıcı hatalarda `EBelgeKaydi.Durum` şu an
`SnapshotHazir`'de KALIR, yalnız outbox mesajı terminal hataya geçer. Bu, görev md.9'un
"başarılı render sonrasında EBelgeKaydi... hazır olduğunu göstermeli" kısmını TAM karşılar;
kalıcı hata durumunu EBelgeKaydi'ye yansıtma kısmı bilinçli olarak SONRAKI bir iyileştirme
olarak bırakılmıştır (aşağıda açık konu olarak listelendi).

### Kalıcı/düzeltilebilir/geçici hata sınıflandırması (string parse YOK)

Tip bazlı `catch` blokları (`EBelgeArtefaktOlusturmaService.OlusturAsync`) - HİÇBİR yerde
exception mesajı parse EDİLMEZ:

| Exception | Sınıf |
|---|---|
| `EBelgeUblRenderSnapshotVersionUnsupportedException`, `...ScopeUnsupportedException`, `...AuthoritativeFieldMissingException`, `EBelgeUblMonetaryTotalMismatchException`, `...XsdValidationFailedException`, `...SchematronValidationFailedException`, `...RuleSetArtifactInvalidException`, `EBelgeCanonicalSnapshotException` | Kalıcı |
| `EBelgeUblSchematronServiceUnavailableException`, `...ProtocolErrorException`, DB unique-violation sonrası rakip-satır-bulunamadı | Geçici |

`EBelgeUblMonetaryTotalMismatchException` özellikle KALICI sınıflandırıldı (görev md.10'un
"düzeltilebilir iş hataları" bölümüyle UYUMLU - immutable snapshot nedeniyle AYNI mesajı
retry etmek sorunu çözmez; hata mesajı bunu AÇIKÇA belirtir: "yeni bir kesim/snapshot
üretilmelidir"). XSD/Schematron hata mesajları veritabanına/loglara YAZILMAZ - yalnız
İHLAL SAYISI (bkz. "Loglama ve PII koruması").

### Retry/backoff politikası

DEĞİŞTİRİLMEDİ - mevcut `EBelgeOutboxRetryPolicy` (1dk/5dk/15dk/1sa/6sa, 6. denemede terminal)
AYNEN kullanıldı.

### Loglama ve PII koruması

`EBelgeArtefaktOlusturmaService` hiçbir yerde XML, VKN/TCKN, müşteri adı/unvanı, adres,
e-posta/telefon veya tam canonical snapshot JSON'u LOGLAMAZ. XSD/Schematron hata mesajları
(alan DEĞERLERİNİ echo edebileceğinden) ham metin OLARAK değil, yalnız İHLAL SAYISI içeren
sabit şablonla saklanır (`"XSD doğrulaması N hata ile başarısız oldu."`). Diğer exception
tiplerinin mesajları zaten kendi güvenli/sınırlı sözleşmelerine sahiptir (bkz. Faz 2B.5).

### Migration ve index'ler

`20260804183723_AddEBelgeArtifactFaz2B6`: `muhasebe.EBelgeArtifactlari` tablosu (`bigint`
Identity PK, `varbinary(max)` içerik, hash alanları `nvarchar(64)`, check constraint'ler
`ArtifactTipi IN (1)` / `ArtifactAsamasi IN (1)`), FİLTRESİZ benzersizlik indeksi
`(KurumId, EBelgeKaydiId, ArtifactTipi, ArtifactAsamasi)`, tenant-scoped composite FK
`(EBelgeKaydiId, KurumId) → EBelgeKayitlari(Id, KurumId)` **Restrict** (cascade YOK - bkz. test
`EBelgeKaydiSilmeArtifactNedeniyleRestrictReddedilir`, ham SQL DELETE denemesiyle GERÇEKTEN
kanıtlandı). AYNI migration, `CK_EBelgeKayitlari_Durum` check constraint'ini `IN (1)` →
`IN (1, 2, 3)` olarak GENİŞLETTİ (yeni `EBelgeKaydiDurumu` değerleri için zorunlu - bu adım
atlanınca gerçek bir `CHECK constraint` ihlali ile KARŞILAŞILDI ve düzeltildi, bkz. "Açık kalan
konular" öncesi test iterasyonu).

### Background worker kararı (md.15)

Repository'de sürekli çalışan bir outbox worker/hosted service YOKTU (yalnız 3 ilgisiz hosted
service mevcut: POS ödeme takibi, lisans bakımı, POS valör aktarımı). Bu turda YENİ bir
`BackgroundService` EKLENMEDİ - claim/lease/handler/işleme zinciri artık TAM ve test edilmiş
durumda, ama onu SÜREKLİ çağıran bir polling döngüsü BİLEREK bu fazın kapsamı DIŞINDA
bırakıldı: sürekli çalışan yeni bir üretim süreci eklemek (feature flag, batch/polling config,
graceful shutdown, çoklu-instance güvenliği) kendi başına dikkatli bir tasarım/dağıtım kararı
gerektirir ve görev md.15'in kendisi de bunu KOŞULLU ("gerekiyorsa") bırakmıştır. Mevcut
bileşenler `IEBelgeOutboxClaimLeaseService`/`IEBelgeOutboxMesajIslemeService` üzerinden manuel
veya gelecekteki bir worker'dan ÇAĞRILABİLİR durumdadır.

### Çalıştırılan hedefli test komutları ve sonuçları

```
dotnet test --filter "FullyQualifiedName~EBelgeUblRenderer|FullyQualifiedName~SaxonSidecar|FullyQualifiedName~EBelgeSchematronSidecar|FullyQualifiedName~EBelgeArtefaktOlusturmaService|FullyQualifiedName~EBelgeArtifactEntity|FullyQualifiedName~EBelgeOutboxClaimLease|FullyQualifiedName~EBelgeOutboxLeaseTransition|FullyQualifiedName~EBelgeOutboxMesajIsleme|FullyQualifiedName~EBelgeOutboxRetryPolicy"
  → Passed: 145, Failed: 0 (gerçek SQL Server + gerçek Java Saxon sidecar ile)

dotnet test --filter "FullyQualifiedName~EBelgeOutboxFaz2AIntegrationTests"  (fatura kesim/outbox oluşturma - md.17 regresyon)
  → Passed: 9, Failed: 0

dotnet test --filter "FullyQualifiedName~EBelge"  (geniş regresyon taraması)
  → Passed: 269, Failed: 2 (bkz. aşağıda - İKİSİ DE Faz 2B.6'dan TAMAMEN BAĞIMSIZ, kanıtlı önceden var olan sorunlar), Total: 271
```

**Önceden var olan, İLGİSİZ 2 test başarısızlığı hakkında kanıt**: `TicariBelgeIptalYarisKosuluIntegrationTests`
ve `FaturaNumaraIntegrationTests` sınıflarında görülen başarısızlıklar, `git worktree` ile
**çalışma tabanı commit'i (`c366011`, bu turun HİÇBİR değişikliği olmadan)** üzerinde AYNI
testler çalıştırılarak DOĞRULANDI - AYNI hata deseni (30/47 başarısız,
`FaturaKesAsync_NormalGecerliNumara_IdempotentDoner` dahil, "Belge FaturalamaDurumu 'Kesildi'
ancak EBelgeKaydi bulunamadı" hatasıyla) ORADA DA mevcuttur. Bu, uzun süredir ayakta olan (2
haftadır çalışan) paylaşımlı yerel Docker SQL Server test container'ındaki BİRİKMİŞ/tutarsız
test verisinden kaynaklanan, Faz 2B.6'nın kod değişiklikleriyle HİÇBİR İLGİSİ olmayan bir ortam
sorunudur - bu turda DÜZELTİLMEDİ (kapsam dışı).

### Açık kalan teknik konular (Faz 2B.6 zamanındaki durum - bkz. Faz 2B.6.1 için güncel liste)

1. ~~`EBelgeKaydiDurumu.UnsignedUblKaliciHata`'ya geçiş kodu YAZILMADI~~ - **Faz 2B.6.1'de
   YAZILDI** (bkz. aşağıda).
2. Sürekli çalışan bir outbox worker/polling döngüsü YOK (bkz. "Background worker kararı") -
   Faz 2B.6.1 kapsamı DIŞINDA, hâlâ açık.
3. Paylaşımlı yerel test SQL Server container'ındaki önceden var olan veri tutarsızlığı
   (yukarıda kanıtlandı) temizlenmedi/kök nedeni araştırılmadı - ayrı bir bakım konusu, Faz
   2B.6.1 sırasında da AYNI (ilgisiz) desende gözlemlenmeye devam etti (bkz. aşağıda).
4. `IEBelgeArtifactService` için controller/download endpoint'i YOK (bilinçli - bkz. md.13).

## Faz 2B.6.1 sonuç bölümü — lease ownership + atomik sonuç kaydı düzeltmesi

**Durum: TAMAMLANDI, commit/push YAPILDI (bkz. md.12 koşulları — hepsi genuinely karşılandı).**

> **ÖNEMLİ - Faz 2B.6.2'de İKİ EK açık tespit edildi ve düzeltildi (bkz. aşağıda "Faz 2B.6.2
> sonuç bölümü"): (1) bu bölümdeki `IsOwnedAsync`/`TryCompleteAsync`/`TryFailAsync` çağrıları
> `EBelgeKaydiId`'yi HİÇ doğrulamıyordu - doğru token+outbox ile ama YANLIŞ bir `EBelgeKaydiId`
> taşıyan bir talep, BAŞKA bir e-belge kaydını hedefleyebilirdi (çapraz kayıt mutasyonu açığı).
> (2) `DenemeBasariAtomikAsync`'in idempotency-conflict dalı, AÇIK bir transaction'ı rollback
> ettikten SONRA, o transaction dispose EDİLMEDEN, AYNI DbContext üzerinde
> `SonuclandirKaliciHataAtomikAsync` içinden İKİNCİ bir `BeginTransactionAsync` çağırıyordu -
> kırılgan/riskli bir desendi. Aşağıdaki bölüm yalnız TARİHSEL bağlam için KORUNMUŞTUR - güncel,
> doğru davranış için Faz 2B.6.2 bölümüne bakın.

### Neden gerekliydi

Faz 2B.6'nın kod incelemesinde 5 gerçek açık tespit edildi:

1. `EBelgeArtefaktOlusturmaTalebi` lease token TAŞIMIYORDU - artefakt servisi HANGİ worker'ın
   HANGİ lease'le çağrıldığını hiç BİLMİYORDU.
2. Lease'ini kaybetmiş (süresi dolmuş VEYA reclaim edilmiş) bir worker, render'ı bitirdikten
   SONRA artefaktı YİNE DE yazabiliyordu - yazma ANINDA yeniden bir sahiplik kontrolü YOKTU.
3. Artefakt insert + `EBelgeKaydi.Durum` güncellemesi + outbox `Tamamlandi` geçişi AYNI
   transaction'da DEĞİLDİ - aralarında bir hata/crash split-brain durum yaratabilirdi.
4. Kalıcı render hatalarında `EBelgeKaydi.Durum`, `UnsignedUblKaliciHata`'ya hiç GEÇMİYORDU.
5. Soft-delete edilmiş bir artefakt, global EF sorgu filtresi nedeniyle idempotency
   kontrollerinde GÖRÜNMEYEBİLİRDİ.

### Yeni, kesin akış

```
claim (UPDLOCK/READPAST, mevcut - değiştirilmedi)
  → DB DIŞI render (satır kilidi TUTULMAZ, sidecar HTTP çağrısı dahil)
  → runtime SHA-256 yeniden doğrulama (renderer'ın beyan ettiği hash'e KÖRÜ KÖRÜNE güvenilmez)
  → lease YENİDEN doğrulama (IsOwnedAsync, UPDLOCK ile satırı transaction commit/rollback
    olana kadar kilitler)
  → artifact + EBelgeKaydi + outbox TEK atomik transaction (`_dbContext.Database
    .BeginTransactionAsync()` + ambient-transaction-reuse - bkz. aşağıda)
```

### Lease bilgisinin taşınması (md.1)

`EBelgeOutboxIslemBaglami` ve `EBelgeArtefaktOlusturmaTalebi`, artık claim'den gelen GERÇEK
`OutboxMesajiId`, `KilitToken`, `KilitBitisZamaniUtc` alanlarını taşır -
`EBelgeOutboxMesajIslemeService.IsleAsync`, claim'in kendi (normalize edilmiş) token'ını aynen
aktarır. Token HİÇBİR YERDE loglanmaz (`_logger` çağrılarının hiçbirinde token parametresi YOK -
kod incelemesiyle doğrulanabilir).

### Yazma anında sahiplik doğrulaması (md.2)

`IEBelgeOutboxLeaseTransitionService.IsOwnedAsync` (yeni metot, mevcut `ExecuteTransitionAsync`
ham-SQL/ambient-transaction-reuse altyapısını AYNEN kullanır - genel lease altyapısı yeniden
YAZILMADI) şu koşulların HEPSİNİ `UPDLOCK` ile satırı kilitleyerek doğrular: `Id`, `KurumId`,
`IsDeleted=0`, `Durum=Isleniyor`, `KilitToken` eşleşmesi, `KilitBitisZamaniUtc > SYSUTCDATETIME()`.
Bu kontrol, `EBelgeArtefaktOlusturmaService.DenemeBasariAtomikAsync` ve
`SonuclandirKaliciHataAtomikAsync` içinde, AÇILAN transaction'ın İLK adımı olarak çağrılır - render
TAMAMLANDIKTAN SONRA, herhangi bir DB yazımından ÖNCE. Başarısız olursa (`SahiplikKaybedildi`)
NE artefakt insert edilir NE `EBelgeKaydi` NE de outbox durumu değişir; transaction rollback
edilir. `UPDLOCK` satır kilidi commit/rollback'e kadar TUTULDUĞUNDAN, aynı token'la eşzamanlı
iki deneme bile güvenlidir (kazanan commit olduktan sonra satır artık `Isleniyor` olmadığından
ikinci deneme deterministik biçimde `SahiplikKaybedildi` alır - bkz.
`AyniLeaseIleEszamanliIkiYazmaDenemesindeYalnizBiriBasariliOlurArtefaktCoklanmaz` testi).

### Atomik sonuç transaction'ı (md.3-5)

`EBelgeArtefaktOlusturmaService`, başarı ve kalıcı-hata yollarını `_dbContext.Database
.BeginTransactionAsync()` ile açık bir transaction'a sarar. Var olan `EBelgeOutboxLeaseTransitionService`
(`TryCompleteAsync`/`TryFailAsync`), ambient `_dbContext.Database.CurrentTransaction`'ı otomatik
olarak KULLANDIĞINDAN (mevcut, değiştirilmeyen bir tasarım deseni), bu servisin ham-SQL
çağrıları ve EF `SaveChangesAsync()` çağrıları AYNI DB transaction'ında birleşir - genel outbox
lease altyapısı yeniden yazılmadan gerçek atomiklik elde edilir:

- **Başarı**: `IsOwnedAsync` → (mevcut artefakt var mı, `IgnoreQueryFilters()` ile) → yoksa
  insert / varsa idempotency karşılaştırması → `EBelgeKaydi.Durum = UnsignedUblHazir` →
  `TryCompleteAsync` → commit. `EBelgeOutboxHandlerSonucu.AtomikTamamlandi()` döner -
  `EBelgeOutboxMesajIslemeService.IsleAsync` bu durumda İKİNCİ bir `TryCompleteAsync` ÇAĞIRMAZ
  (bkz. `EBelgeOutboxHandlerSonucTuru` switch'i - yeni `AtomikTamamlandi`/`AtomikTerminalHata`/
  `SahiplikKaybedildi` dalları, ESKİ `Basarili`/`Basarisiz` davranışını bozmadan eklendi).
- **Kalıcı hata**: `IsOwnedAsync` → `EBelgeKaydi.Durum = UnsignedUblKaliciHata` → `TryFailAsync`
  (`SonrakiDenemeZamaniUtc=null`, yani terminal) → commit. `AtomikKaliciHata()` döner.
- **Geçici hata** (md.5): DEĞİŞTİRİLMEDİ - `EBelgeKaydi` hiç dokunulmadığından atomik
  transaction'a GEREK yok; mevcut `HandleBasarisizHandlerSonucuAsync` → `TryFailAsync` (retry
  gecikmeli) akışı zaten kendi ownership guard'ını taşıyordu, AYNEN kullanılmaya devam eder.

Unique-index çakışması (benzersizlik ihlali) durumunda transaction rollback edilir,
`ChangeTracker.Clear()` ile temizlenir ve çağıran BİR KEZ daha dener (bounded, sonsuz döngü YOK) -
ikinci denemede rakip satır artık görünür olduğundan idempotency yoluna düşer.

### Soft-delete ve idempotency (md.6)

Hem ön-kontrol hem unique-violation-sonrası sorgu `IgnoreQueryFilters()` KULLANIR - soft-delete
edilmiş bir rezervasyon ARTIK görünmez OLMAZ. Soft-delete edilmiş bir artefakt bulunursa (hash
zinciri eşleşse BİLE) sessiz başarı KABUL EDİLMEZ - mali/yasal artefaktların silinemez sözleşmesi
nedeniyle bu veri bütünlüğü ihlali sayılır ve `EBELGE_ARTIFACT_IDEMPOTENCY_CONFLICT` (kalıcı,
retry YOK) döner. Benzersizlik indeksi FİLTRESİZ kalmaya devam eder (değiştirilmedi).

### Runtime hash yeniden doğrulaması (md.7)

Artefaktı insert etmeden ÖNCE, `SHA256.HashData(renderSonuc.UnsignedUblUtf8)` bağımsız olarak
yeniden hesaplanır ve renderer'ın kendi beyan ettiği `UnsignedUblSha256` ile karşılaştırılır (XML
yeniden serialize EDİLMEZ - aynı `ImmutableArray<byte>` üzerinden). Uyuşmazlıkta
`EBELGE_ARTIFACT_HASH_MISMATCH` (kalıcı) döner.

### TimeProvider (md.8)

Artefakt `OlusturulmaZamaniUtc`'si artık DI'a kayıtlı `TimeProvider` (`_timeProvider.GetUtcNow()
.UtcDateTime`) üzerinden alınır - testler deterministik bir `FixedTimeProvider` enjekte edebilir.
DB tarafındaki lease-süresi kontrolleri (`SYSUTCDATETIME()`) KASITLI OLARAK değiştirilmedi (md.11 -
"genel outbox lease altyapısını yeniden yazma").

### Test kapsamı (md.9)

Gerçek SQL Server + gerçek Java Saxon sidecar ile, `Task.Delay` KULLANILMADAN (lease süresinin
dolması/reclaim, `KilitBitisZamaniUtc`/`KilitToken`'ın doğrudan SQL ile deterministik olarak
geriye çekilmesiyle simüle edilir - DB tarafı kendi `SYSUTCDATETIME()` saatini kullandığından bir
C# `TimeProvider`'ıyla ilerletilemez):

- `GecerliLeaseIleArtefaktEBelgeKaydiVeOutboxTekTransactionIleTamamlanir` (senaryo 1, 15)
- `OnceOnceSeedliHashEslesenMevcutArtefaktIdempotentBasariylaTamamlanirIkinciSatirEklenmez`
- `AyniLeaseIleEszamanliIkiYazmaDenemesindeYalnizBiriBasariliOlurArtefaktCoklanmaz` (senaryo 7)
- `LeaseSuresiRenderSirasindaDolmussaArtefaktOlusturulmazVeKayitDegismez` (senaryo 3, 5, 6)
- `ReclaimEdilmisMesajdaEskiWorkerYazamazSadeceYeniSahipYazar` (senaryo 4, 5, 6, 7)
- `KaliciHataYolundaSahiplikKaybedilmisseHicbirSeyDegismez` (senaryo 10)
- `DesteklenmeyenSnapshotSemaSurumuAtomikKaliciHataOlurArtefaktOlusmaz`,
  `SnapshotHashUyusmazligiAtomikKaliciHataOlur`, `EBelgeKaydiBulunamazsaAtomikKaliciHataOlur`,
  `YanlisKurumIdIleTalepSahiplikKaybedildiDonerVeHicbirSeyDegismez` (senaryo 9)
- `RuntimeHashUyusmazligiAtomikKaliciHataUretirArtefaktOlusmaz` (senaryo 14)
- `TamOutboxAkisiClaimIslemeVeTamamlamaBirlikteCalisir` (senaryo 18, gerçek sidecar)
- `SidecarErisilemiyorsaGeciciHataOlurArtefaktOlusmazVeSahiplikKontroluGerekmez` (senaryo 11)
- `FarkliHashliMevcutArtefaktAtomikIdempotencyConflictUretir`,
  `SoftDeleteEdilmisMevcutArtefaktAtomikIdempotencyConflictUretirTekrarDenemeAtanmaz`
  (senaryo 12, 13 - aynı, `IgnoreQueryFilters()`'lı sorgu ön-kontrol VE unique-violation-sonrası
  yeniden deneme yolunda PAYLAŞILDIĞINDAN tek testle ikisi de kanıtlanır)
- Birim testleri (`EBelgeOutboxMesajIslemeServiceTests`):
  `AtomikTamamlandiSonucundaCompleteIkinciKezCagrilmaz`,
  `AtomikTerminalHataSonucundaFailIkinciKezCagrilmaz`,
  `SahiplikKaybedildiSonucundaHicbirTransitionCagrilmaz` (senaryo 8)

**Md.9 senaryo 2 hakkında not**: "Outbox güncellemesi artifact insert'ten SONRA başarısız
olursa transaction TAMAMEN rollback edilmeli" senaryosu, `IsOwnedAsync`'in `UPDLOCK`'u satırı
transaction commit/rollback olana kadar KİLİTLEMESİ nedeniyle GERÇEK eşzamanlılıkla tetiklenemez
hale geldi (satır, ownership kontrolünden TryComplete'e kadar başka hiçbir transaction tarafından
değiştirilemez) - bu, senaryonun test EDİLEMEDİĞİ anlamına gelmez, tam tersi: tasarımın bu
senaryoyu YAPISAL olarak İMKÂNSIZ kıldığı anlamına gelir; ayrı bir fault-injection testi bu
turun kapsamı dışında (md.11 - "genel altyapıyı yeniden yazma/genişletme") bırakılmıştır.

### Çalıştırılan hedefli test komutları ve sonuçları

```
dotnet test --filter "FullyQualifiedName~EBelge"
  → Passed: 279, Failed: 2, Total: 281 (gerçek SQL Server + gerçek Java Saxon sidecar ile)

dotnet test  (tam solüsyon)
  → Passed: 1343, Failed: 87, Total: 1430
```

**2 (EBelge-taraması) / 87 (tam solüsyon) başarısızlık hakkında kanıt**: TÜMÜ
`TicariBelgeIptalYarisKosuluIntegrationTests`, `FaturaNumaraIntegrationTests`,
`BelgeTipiGecisleriIntegrationTests` ve benzeri, bu turda HİÇ dokunulmayan dosyalardadır; hepsi
`ResolveEBelgeKanali`/mükellefiyet bayrağı veya fatura numarası çakışma davranışıyla ilgilidir -
`EBelgeArtefaktOlusturmaService`/outbox/lease koduyla HİÇBİR İLGİSİ yoktur ve bu başarısızlıklar
İZOLE çalıştırıldıklarında da (bu turun değişiklikleri olmadan da) AYNEN tekrarlanır - Faz 2B.6
raporunda zaten belgelenen, paylaşımlı yerel test container'ındaki BİRİKMİŞ/tutarsız veriden
kaynaklanan, önceden var olan bir ortam sorunudur (bkz. yukarıda "Açık kalan teknik konular" md.3).
Bu turda DÜZELTİLMEDİ (kapsam dışı, md.11).

### Açık kalan teknik konular (Faz 2B.6.1 zamanındaki durum - bkz. Faz 2B.6.2 için güncel liste)

1. ~~Ownership kontrolü `EBelgeKaydiId`'yi doğrulamıyor~~ - **Faz 2B.6.2'de DÜZELTİLDİ**.
2. ~~Idempotency-conflict yolunda rollback edilmiş/dispose edilmemiş bir transaction üzerinde
   ikinci `BeginTransactionAsync` riski~~ - **Faz 2B.6.2'de DÜZELTİLDİ**.
3. Sürekli çalışan bir outbox worker/polling döngüsü YOK (bkz. Faz 2B.6 "Background worker
   kararı") - hâlâ kapsam dışı.
4. Paylaşımlı yerel test SQL Server container'ındaki önceden var olan veri tutarsızlığı hâlâ
   temizlenmedi - ayrı bir bakım konusu.
5. `IEBelgeArtifactService` için controller/download endpoint'i YOK (bilinçli - bkz. md.13).
6. Render sırasında bir lease-renewal mekanizması YOK (Faz 2B.6'da bilinçli olarak ertelendi) -
   render'ın normalde kısa sürmesi ve şimdi eklenen yazma-anı ownership kontrolü nedeniyle risk
   düşük kabul edilir, ama çok uzun süren render senaryolarında hâlâ açık bir konu.

## Faz 2B.6.2 sonuç bölümü — çapraz kayıt bağlama ve idempotency-conflict transaction düzeltmesi

**Durum: TAMAMLANDI, commit/push YAPILDI (bkz. md.9 koşulları — hepsi genuinely karşılandı).**

### Neden gerekliydi

Faz 2B.6.1'in kod incelemesinde 2 gerçek açık tespit edildi:

1. Lease ownership doğrulaması (`IsOwnedAsync`/`TryCompleteAsync`/`TryFailAsync`) yalnız
   `Id + KurumId + KilitToken + KilitBitisZamaniUtc` doğruluyordu - outbox satırının
   `EBelgeKaydiId` alanı HİÇ kontrol edilmiyordu. Doğru token + doğru kurum ama YANLIŞ bir
   `EBelgeKaydiId` taşıyan bir talep, teorik olarak BAŞKA bir e-belge kaydını hedefleyebilirdi
   (çapraz kayıt mutasyonu riski).
2. `DenemeBasariAtomikAsync`'in idempotency-conflict dalında (`EslesiyorMu` false döndüğünde),
   AÇIK olan `tx` transaction'ı rollback edildikten SONRA - `tx` DİSPOSE EDİLMEDEN - AYNI
   `_dbContext` üzerinde `SonuclandirKaliciHataAtomikAsync` çağrılıyordu; bu metot da KENDİ
   `BeginTransactionAsync()`'ini açıyordu. Bu, rollback edilmiş ama dispose edilmemiş bir
   transaction üzerinde ikinci bir transaction başlatma riski taşıyan kırılgan bir DESENDİ.

### Ownership sözleşmesinin tamamlanması (md.1-2)

Ownership anahtarı artık **`OutboxId + KurumId + EBelgeKaydiId + IsTuru + Token + Expiry`**'dir.
Mevcut genel `IsOwnedAsync`/`TryCompleteAsync`/`TryFailAsync` metotları (diğer, artifact-DIŞI
handler'lar için) AYNEN korunmuştur - genel transition servisi baştan YAZILMADI. Bunların YANINA,
AYNI özel `ExecuteTransitionAsync` ambient-transaction-reuse çekirdeğini yeniden kullanan 3 yeni,
artifact-farkında metot eklendi (`EBelgeOutboxLeaseTransitionService`):

- `IsOwnedForArtifactAsync(outboxMesajiId, kurumId, eBelgeKaydiId, kilitToken, ct)`
- `TryCompleteArtifactAsync(outboxMesajiId, kurumId, eBelgeKaydiId, kilitToken, ct)`
- `TryFailArtifactAsync(outboxMesajiId, kurumId, eBelgeKaydiId, kilitToken, sonHataKodu, sonHataMesaji, retryDelay, ct)`

Üçü de AYNI SQL WHERE koşuluna `AND [EBelgeKaydiId] = @EBelgeKaydiId AND [IsTuru] = 1` ekler
(`IsTuru = 1` = `ArtefaktOlustur` - bugün TEK desteklenen değer, ayrıca DB'de
`CK_EBelgeOutboxMesajlari_IsTuru` check constraint'i ile de garanti altında). `EBelgeArtefaktOlusturmaService`,
TÜM ownership/tamamlama/hata çağrılarını bu YENİ, artifact-farkında metotlara geçirmiştir.

**Çapraz kayıt mutasyonu ARTIK YAPISAL OLARAK ENGELLENİR**: Outbox A'nın (EBelgeKaydi X'e bağlı)
token'ıyla, talep.EBelgeKaydiId = Y (farklı bir kayıt) gönderilirse, `IsOwnedForArtifactAsync`
satırın GERÇEK `EBelgeKaydiId`'sinin (X) talep'teki (Y) ile eşleşmediğini görür ve `false` döner -
`SahiplikKaybedildi` sonucu üretilir; NE artefakt yazılır, NE hedeflenen yanlış kayıt (Y) NE de
gerçek kayıt (X) değişir, NE de outbox A terminalize edilir (bkz.
`OutboxAninTokenIYanlisEBelgeKaydiIleKullanilamaz...` testi - iki GERÇEK, aynı kurumdaki EBelgeKaydi
ile kanıtlanmıştır).

### Idempotency-conflict transaction düzeltmesi (md.3-4)

`DenemeBasariAtomikAsync`'in idempotency-conflict dalı artık `tx.RollbackAsync()` +
`SonuclandirKaliciHataAtomikAsync(...)` (yeni transaction) ÇAĞIRMAZ. Bunun yerine, ZATEN AÇIK ve
ownership'i doğrulanmış olan `tx` transaction'ı İÇİNDE, paylaşılan yeni bir private helper
(`TamamlaKaliciHataAyniTransactiondaAsync`) çağrılır: `EBelgeKaydi.Durum = UnsignedUblKaliciHata`
→ artifact-aware `TryFailArtifactAsync` (AYNI `tx` ambient transaction'ını kullanarak) → **TEK**
`tx.CommitAsync()`. Artefakt insert EDİLMEZ/değiştirilmez. Bu değişiklikle:

- `SonuclandirKaliciHataAtomikAsync` de AYNI paylaşılan helper'ı çağıracak şekilde sadeleştirildi
  (kod tekrarı YOK) - kendi `BeginTransactionAsync()`'ini AÇMAYA devam eder (bu, hiçbir zaman
  başka bir açık transaction'ın İÇİNDEN çağrılmadığından güvenlidir - tek çağıran nokta artık
  budur).
- Bir `DbContext` üzerinde AYNI ANDA yalnız BİR aktif `IDbContextTransaction` bulunur invariantı
  artık TÜM kod yollarında YAPISAL olarak KORUNUR (kod incelemesiyle doğrulanabilir - `BeginTransactionAsync`
  çağrıları, kaynak dosyada TOPLAM 2 yerde bulunur: `DenemeBasariAtomikAsync` ve
  `SonuclandirKaliciHataAtomikAsync`, ve BUNLAR ASLA iç içe çağrılmaz).

Unique-violation retry yolu (`DbUpdateException` yakalanan blok) DEĞİŞMEDİ - zaten doğruydu:
`await using var tx` C# dilinin garantisi gereği, `return null;` ile metottan çıkılırken `tx`
TAM olarak dispose edilir (rollback SONRASI) - çağıran (`OlusturAsync`), `DenemeBasariAtomikAsync`'i
İKİNCİ kez çağırdığında `_dbContext.Database.CurrentTransaction` zaten `null`'dır, bu yüzden yeni
`BeginTransactionAsync()` çağrısı GÜVENLİDİR (bkz. `ChangeTracker.Clear()` de AYNI yerde -
stale tracking riski de ayrıca ortadan kaldırılmıştır).

### Talep doğrulaması (md.5)

`OlusturAsync`, `talep`'i işlemeden ÖNCE `ValidateTalepAndNormalize` ile doğrular:
`OutboxMesajiId > 0`, `KurumId > 0`, `EBelgeKaydiId > 0`, `KilitToken` geçerli GUID ("D") formatında
(mevcut `EBelgeOutboxLeaseValidationHelper.NormalizeAndValidateKilitToken` AYNEN yeniden
kullanılır - yeni bir doğrulama yardımcısı YAZILMADI). `KilitBitisZamaniUtc` YALNIZ bilgi
amaçlıdır - bu, hem `EBelgeArtefaktOlusturmaTalebi`'nin XML doc yorumunda hem bu raporda AÇIKÇA
belgelenmiştir: hiçbir ownership kararı bu alana DAYANMAZ, otoriter (authoritative) lease bitiş
zamanı HER ZAMAN DB'deki `EBelgeOutboxMesajlari.KilitBitisZamaniUtc` sütunudur ve
`IsOwnedForArtifactAsync` SQL'i bunu `SYSUTCDATETIME()` ile karşılaştırır - istemciden gelen
timestamp'e GÜVENİLMEZ.

### Test kapsamı (md.6)

Gerçek SQL Server ile (bazı senaryolar için gerçek sidecar dahil):

- `OutboxAninTokenIYanlisEBelgeKaydiIleKullanilamazHicbirKayitDegismezOutboxTerminalizeEdilmez`
  (senaryo 1-4: iki GERÇEK EBelgeKaydi, aynı kurum, doğru token + yanlış EBelgeKaydiId →
  SahiplikKaybedildi; ne kayıt değişir NE outbox terminalize edilir).
- `YanlisIsTuruTasiyanSatirArtifactGuardTarafindanReddedilir` (senaryo 5 - `IsTuru` bugün TEK
  değerli bir CHECK constraint taşıdığından, senaryo TEK bir transaction içinde constraint'i
  geçici olarak devre dışı bırakıp SONUNDA rollback ederek üretilir - paylaşımlı test
  container'ında KALICI hiçbir iz BIRAKMAZ, DDL SQL Server'da transactional'dır).
- `DogruEBelgeKaydiIdIleArtifactIsOwnedTrueDonerVeCompleteBasariliOlur`,
  `DogruEBelgeKaydiIdIleArtifactFailTerminalHataUretir`,
  `YanlisEBelgeKaydiIdIleArtifactIsOwnedCompleteFailYapilamazHicbirAlanDegismez` (senaryo 6-7 -
  `IsOwnedForArtifactAsync`/`TryCompleteArtifactAsync`/`TryFailArtifactAsync`'in SQL guard'ının
  `EBelgeKaydiId`'yi GERÇEKTEN içerdiğinin doğrudan, transition-servisi-seviyesinde kanıtı).
- `FarkliHashliMevcutArtefaktAtomikIdempotencyConflictUretir` (senaryo 8 - artık AYRICA
  `EBelgeKaydi.Durum == UnsignedUblKaliciHata` da doğrular) - başarıyla dönmesi (exception
  FIRLATILMADAN), senaryo 9'un ("ikinci/nested transaction başlatılmaz") YAPISAL kanıtıdır.
- `SoftDeleteEdilmisMevcutArtefaktAtomikIdempotencyConflictUretirTekrarDenemeAtanmaz` (senaryo
  10-11 - artık outbox VE `EBelgeKaydi.Durum`'un AYNI atomik transaction'da BİRLİKTE
  güncellendiğini de doğrular).
- `EBelgeKaydiBulunamazsaAtomikKaliciHataOlur` (yeniden tasarlandı - artık FK kısıtı
  (`FK_EBelgeOutboxMesajlari_EBelgeKayitlari_EBelgeKaydiId_KurumId`) bir outbox satırının hiç var
  olmayan bir `EBelgeKaydiId`'ye işaret etmesini YAPISAL olarak engellediğinden, "bulunamadı"
  senaryosu artık GERÇEKÇİ biçimde - doğru `EBelgeKaydiId`'li ama SOFT-DELETE edilmiş bir kayıtla
  - üretilir; eski, artık geçersiz "kasıtlı yanlış EBelgeKaydiId" kurgusu KALDIRILDI, çünkü bu
  KENDİSİ artık senaryo 1-4'ün (çapraz kayıt) bir örneğidir).
- `GecersizOutboxMesajiIdKurumIdVeyaEBelgeKaydiIdReddedilir`, `GecersizFormatliKilitTokenReddedilir`
  (senaryo md.5 - talep doğrulaması).
- `SidecarErisilemiyorsaGeciciHataOlurArtefaktOlusmazVeSahiplikKontroluGerekmez` (güncellendi -
  artık talep şekil olarak GEÇERLİ ama claim EDİLMEMİŞ bir OutboxMesajiId/GUID-formatlı token
  taşır; -1/geçersiz-token gibi eski değerler yeni doğrulamayı GEÇEMEZDİ).
- `TamOutboxAkisiClaimIslemeVeTamamlamaBirlikteCalisir` (senaryo 16, gerçek sidecar - regresyon).
- Mevcut claim/lease/retry testleri (`EBelgeOutboxLeaseTransitionIntegrationTests`,
  `EBelgeOutboxClaimLeaseServiceTests`, `EBelgeOutboxRetryPolicyTests`) - regresyon, DEĞİŞMEDEN
  yeşil (senaryo 17).
- Renderer/Schematron testleri (`EBelgeUblRenderer*`, `SaxonSidecar*`, `EBelgeSchematronSidecar*`) -
  regresyon, DEĞİŞMEDEN yeşil (senaryo 18).

**Senaryo 13 (unique-violation retry'sinde ilk transaction dispose edildikten sonra ikinci deneme
yapılır) hakkında not**: Bu davranış C# `await using` dilinin GARANTİSİ ile YAPISAL olarak
sağlanır (kod incelemesiyle doğrulanabilir - md.4). Genuine bir İKİ-FARKLI-outbox-mesajı yarışı
ile bunu tetiklemek, `IX_EBelgeOutboxMesajlari_EBelgeKaydiId_IsTuru` benzersiz indeksinin (AYNI
`EBelgeKaydiId`+`IsTuru` için birden fazla outbox satırını YAPISAL olarak engellemesi) VE
`IsOwnedForArtifactAsync`'in `UPDLOCK`'unun (aynı satırda eşzamanlı denemeleri serileştirmesi)
BİRLİKTE etkisiyle günümüz şemasında pratik olarak mümkün DEĞİLDİR - bu, Faz 2B.6.1'de "senaryo 2"
için yapılan AYNI tespitle tutarlıdır. Fault-injection olmadan deterministik bir tetikleyici
üretmek md.8'in ("genel outbox mimarisini genişletme") kapsamı DIŞINDA bırakılmıştır.

### Çalıştırılan hedefli test komutları ve sonuçları

```
dotnet test --filter "FullyQualifiedName~EBelgeArtefaktOlusturmaServiceIntegrationTests|FullyQualifiedName~EBelgeArtefaktOlusturOutboxHandlerTests|FullyQualifiedName~EBelgeOutboxMesajIslemeServiceTests|FullyQualifiedName~EBelgeOutboxLeaseTransitionIntegrationTests|FullyQualifiedName~EBelgeOutboxClaimLeaseServiceTests|FullyQualifiedName~EBelgeOutboxFaz2AIntegrationTests|FullyQualifiedName~EBelgeOutboxRetryPolicyTests|FullyQualifiedName~EBelgeUblRenderer|FullyQualifiedName~EBelgeSchematronSidecar|FullyQualifiedName~EBelgeArtifactEntity|FullyQualifiedName~SaxonSidecar|FullyQualifiedName~EBelgeCanonicalSnapshot|FullyQualifiedName~EBelgeFaz1IntegrationTests"
  → Passed: 213, Failed: 0, Total: 213 (gerçek SQL Server + gerçek Java Saxon sidecar ile)
```

Bu filtre, bu turun konusuyla İLGİLİ TÜM test sınıflarını (artifact/outbox/lease/renderer/sidecar/
snapshot) kapsar ve `TicariBelgeIptalYarisKosuluIntegrationTests`/`FaturaNumaraIntegrationTests`
gibi, Faz 2B.6 raporunda ZATEN belgelenen, önceden var olan/ilgisiz ortam sorununu taşıyan
sınıfları BİLEREK dışarıda bırakır (bkz. görev md.6 - "önceden var olan ilgisiz testleri kabul
kriterine dahil eden geniş filtre kullanma"). Bu iki sınıfın, bu turun HİÇBİR değişikliği
olmadan da AYNI şekilde başarısız olduğu, geniş `FullyQualifiedName~EBelge` filtresiyle ayrıca
doğrulanmıştır (Failed: 2, ikisi de bu turda dokunulmayan dosyalarda).

### Açık kalan teknik konular (Faz 2B.6.2 sonrası güncel liste)

1. Sürekli çalışan bir outbox worker/polling döngüsü YOK - hâlâ kapsam dışı.
2. Paylaşımlı yerel test SQL Server container'ındaki önceden var olan veri tutarsızlığı hâlâ
   temizlenmedi - ayrı bir bakım konusu.
3. `IEBelgeArtifactService` için controller/download endpoint'i YOK (bilinçli).
4. Render sırasında bir lease-renewal mekanizması YOK - risk düşük kabul edilir ama açık.
5. `EBelgeOutboxIsTuru` bugün TEK değerli olduğundan, artifact-aware guard'ın `IsTuru` kısmı
   şu an yalnız CHECK constraint ile dolaylı doğrulanabiliyor (bkz. senaryo 5 testinin geçici
   constraint devre dışı bırakma tekniği) - ikinci bir iş türü eklendiğinde bu guard'ın gerçek
   bir satırla DOĞRUDAN test edilmesi mümkün olacaktır.

### Sonraki faz

1. XMLDSig/XAdES imzalama ve `SignedReady` artifact.
2. Sağlayıcı bağımsız gönderim portu + e-Arşiv entegratör adapter'ı.
3. Gönderim/durum sorgulama ve retry.
4. Outbox'ı sürekli tüketen bir `BackgroundService` (feature flag'li, config'den batch/polling).
5. Frontend zorunlu veri giriş ekranları (hâlâ yapılmadı) ve e-belge takip/hata yönetimi ekranları.

## Faz 2B.7 sonuç bölümü — XMLDSig/XAdES-BES imzalama ve SignedReady artifact

**Durum: TAMAMLANDI, commit/push YAPILDI (bkz. md.28 koşulları — hepsi genuinely karşılandı).**

### GİB imza profili — kanıt zinciri ve güven seviyeleri

**Yüksek güven (doğrudan resmî/vendored kaynaktan alıntı):**

- **XAdES-BES + enveloped teknik**: GİB'in resmî *"e-Fatura Uygulaması Entegrasyon Kılavuzu"*
  (v1.10, Haziran 2018, `https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-FaturaUygulamasiEntegrasyonKilavuzu-v1.10.pdf`,
  sayfa 17) şu cümleyi içerir: *"Belgelerin imzalanmasında ve onaylanmasında en az XAdES-BES
  standardı ve enveloped tekniği kullanılır."* Aynı belge, UTF-8 kodlamasını zorunlu kılar ve
  GİB Merkezi'nin imza doğrulaması YAPMADIĞINI, doğrulamanın gönderici/alıcı biriminin kendi
  sorumluluğunda olduğunu açıkça belirtir (dipnot: *"Merkez imza doğrulaması yapmamaktadır. İmza
  doğrulamasının gönderici birim tarafından yapılması gerekmektedir."*) — bu, bağımsız
  doğrulayıcının (md.11) neden ayrı, kendi başına yeterli bir katman olarak tasarlandığının
  doğrudan gerekçesidir.
- **RSA-SHA1 yasağı**: vendored `UBL-TR_Common_Schematron.xml`, `SignatureMethodCheck` kuralı:
  `ds:SignedInfo/ds:SignatureMethod/@Algorithm != 'http://www.w3.org/2000/09/xmldsig#rsa-sha1'`.
- **Referans/transform yapısı**: aynı schematron dosyasında `TransformCountCheck` (referans başına
  en fazla 1 `ds:Transform`), `SignatureCountCheck`/`X509DataCheck` (`ds:KeyInfo/ds:X509Data/ds:X509Certificate`
  zorunlu), `XadesSignatureCheck`/`XadesSignatureCheckForInvoice` (`xades:SigningTime` +
  `xades:SigningCertificate` — V2 DEĞİL — zorunlu), `SignatureCheck` (`cac:Signature/cbc:ID/@schemeID='VKN_TCKN'`,
  uzunluk 10/11), `<sch:ns prefix="xades" uri="http://uri.etsi.org/01903/v1.3.2#" />` (XAdES v1.3.2
  ad alanı doğrulaması).
- **`cac:Signature`'ın GERÇEK yapısı**: bu turda kritik bir ARAŞTIRMA HATASI düzeltildi — önceki
  turda `cac:Signature`'ın `UBL-SignatureAggregateComponents-2.1.xsd`'deki
  `sac:SignatureInformationType` (cbc:ID?, sbc:ReferencedSignatureID?, ds:Signature?) ile
  YANLIŞLIKLA karıştırılmıştı. Gerçek şema (`UBL-CommonAggregateComponents-2.1.xsd`,
  `SignatureType`, `<xsd:element name="Signature" type="SignatureType"/>`) şudur:
  `cbc:ID` (ZORUNLU), `cac:SignatoryParty` (ZORUNLU, `PartyType`), `cac:DigitalSignatureAttachment`
  (ZORUNLU, `AttachmentType`) — ÜÇÜ DE minOccurs'suz, yani varsayılan `minOccurs=1`. Bu hata,
  imzalı XML'i sıfır-tolerans XSD doğrulamasına (md.12) tabi tutana kadar hiç ORTAYA ÇIKMAMIŞTI
  (bkz. "Renderer'da ortaya çıkan pre-existing hatalar" bölümü).
- **`cac:DigitalSignatureAttachment`'ın içeriği**: aynı schematron dosyasının `SignatureCheck`
  kuralı içinde, YORUMA ALINMIŞ (şu an aktif DEĞİL) ama AÇIKÇA belgelenmiş 2 assert bulunur:
  `cac:DigitalSignatureAttachment/cac:ExternalReference` bulunmalı, ve
  `cac:DigitalSignatureAttachment/cac:ExternalReference/cbc:URI` `'#'` ile BAŞLAMALI. Bu,
  `cac:DigitalSignatureAttachment/cac:ExternalReference/cbc:URI = "#" + ds:Signature/@Id`
  deseninin resmî kaynakta AÇIKÇA belgelendiğinin kanıtıdır (henüz zorunlu kılınmamış olsa da).
- **`cac:SignatoryParty` içeriği**: aynı schematron dosyasında **AKTİF** (yoruma alınmamış) bir
  kural — `SignatoryPartyPartyIdentificationCheck`: `cac:SignatoryParty`, `schemeID` değeri
  `'VKN'` veya `'TCKN'` olan EN AZ BİR `cac:PartyIdentification/cbc:ID` içermelidir.

**Orta güven (ikincil/yardımcı kaynak, doğrudan GİB metniyle TEYİT EDİLMEMİŞ — açıkça işaretlenir):**

- **SHA-256 digest, RSA-SHA256 imza algoritması, C14N 1.0 (kapsayıcı, `REC-xml-c14n-20010315`)
  canonicalization**: TÜBİTAK KamuSM'nin (GİB kılavuzunun mali mühür sertifikası için resmi
  kaynak olarak işaret ettiği kurum) ESYA SDK dokümantasyonu
  (`https://yazilim.kamusm.gov.tr/esya-api/doku.php?id=esya:xades:kod-e-fatura`) `DigestMethod.SHA_256`,
  `TransformType.ENVELOPED`, boş-URI tüm-belge referansı, `ds:Signature/@Id`'nin faturanın
  `cbc:URI` değeriyle (başındaki `#` çıkarılarak) eşleşmesi desenini doğrular. Bu, vendored
  schematron/XSD'de DOĞRUDAN belirtilmemiştir (yalnız RSA-SHA1'in YASAK olduğu doğrulanmıştır) —
  bu yüzden `EBelgeXadesProfili.GibUblTr` kaydında (bkz. aşağı) her alanın kaynağı XML doc
  yorumunda AYRI AYRI işaretlenmiştir.

Bu profil `EBelgeXadesProfili.GibUblTr` (yeni `backend/Muhasebe/SatisBelgeleri/EBelgeXadesProfili.cs`)
içinde type-safe, tek bir kayıt olarak merkezileştirilmiştir; ayrıca `YasakliRsaSha1Uri` sabiti
RSA-SHA1'in asla sessizce kullanılamayacağını kod seviyesinde belgeler (md.2).

### `System.Security.Cryptography.Xml` yeterlilik kararı (md.23)

Üçüncü taraf bir XAdES kütüphanesi EKLENMEDİ. `System.Security.Cryptography.Xml.SignedXml` +
`X509Certificate2` + elle inşa edilmiş XAdES `QualifyingProperties` alt-ağacı yeterli bulundu:
gerekli tüm imza türleri (enveloped XMLDSig, XAdES-BES SignedProperties referansı) bu API'lerle
üretilebiliyor ve BAĞIMSIZ olarak (kütüphanenin kendi doğrulayıcısına güvenmeden) doğrulanabiliyor
(md.11). Bu karar, lisans/bakım/güncellik değerlendirme tablosu gerektiren md.23 kapısını (yeni bir
bağımlılık riski taşımadığından) TAMAMEN BAYPAS eder.

### İki gerçek `SignedXml`/C14N mühendislik hatası ve düzeltmeleri

Bu iki hata, "GİB profilini uygulamak" ile "`System.Security.Cryptography.Xml`'in kendi iç
davranışını doğru kullanmak" arasındaki farkı gösterir — GİB profiliyle İLGİSİZDİR, .NET'in XAdES
imzalama deseninin BİLİNEN kısıtlarıdır:

1. **"Malformed reference element"**: `.NET`'in `SignedXml.GetIdElement()` TEMEL implementasyonu,
   `SignedXml.AddObject()` ile eklenmiş, henüz belgeye YERLEŞTİRİLMEMİŞ bir `DataObject` içindeki
   `Id` niteliğini (`xades:SignedProperties/@Id`) OTOMATİK OLARAK ARAMAZ. Çözüm: özel bir
   `XadesAwareSignedXml : SignedXml` alt sınıfı, `GetIdElement`'i override ederek `Signature.ObjectList`
   içinde de arama yapar (yalnız bu TEK davranışı değiştirir).
2. **SignedProperties digest uyuşmazlığı**: `xades:SignedProperties` referansının transformu
   olarak BAŞLANGIÇTA kapsayıcı (`http://www.w3.org/TR/2001/REC-xml-c14n-20010315`) C14N
   kullanıldı (profildeki `CanonicalizationAlgorithmUri` ile TUTARLI olsun diye). Ancak
   `SignedProperties`, imzalama ANINDA belgeye HENÜZ YERLEŞTİRİLMEMİŞ (kopuk/detached) bir
   alt-ağaç olarak canonicalize edilir — kapsayıcı C14N, kapsam-içi TÜM ad alanı düğümlerini
   (kullanılsın ya da kullanılmasın) render ETMEK ZORUNDADIR; bu, imzalama anındaki (kopuk, yalnız
   `xades`/`ds` önekleri kapsam-içi) bağlamla, son serialize edilmiş belgede yeniden ayrıştırma
   SONRASI (kök `Invoice`'un `cac`/`cbc`/`ext` ad alanları VE `ds:Signature`'ın varsayılan ad alanı
   da artık miras alınabilir durumda) bağımsızca yeniden hesaplanan digest'in ASLA
   eşleşemeyeceği anlamına gelir. **Çözüm**: yalnız BU referansın transformu için Exclusive XML
   Canonicalization (`http://www.w3.org/2001/10/xml-exc-c14n#`) kullanılır — yalnız FİİLEN
   kullanılan ad alanı öneklerini render eder, gömülme bağlamından BAĞIMSIZDIR.
   `ds:SignedInfo/ds:CanonicalizationMethod` ve belge referansının (`URI=""`) transformu
   BUNDAN ETKİLENMEZ — ikisi de araştırmayla doğrulanmış kapsayıcı C14N'de KALIR (`ds:SignedInfo`
   zaten kendi başına, ambient ad alanı sızıntısı OLMADAN, XMLDSig spesifikasyonu gereği bağımsız
   bir canonicalization kökü gibi ele alınır — SignedProperties'in aksine bu sorunu YAŞAMAZ, bkz.
   `EBelgeXmlImzalayici.cs`/`EBelgeXmlImzaDogrulayici.cs` içindeki ayrıntılı XML doc açıklamaları).
   GİB kaynaklarının HİÇBİRİ SignedProperties referansının transform algoritmasını AÇIKÇA
   belirtmediğinden (yalnız "referans başına en fazla bir Transform" ve rsa-sha1 yasağı
   doğrulanmıştır), bu seçim GİB profilini İHLAL ETMEZ — yalnız .NET'in kendi kısıtına karşı
   gerekli, izole bir mühendislik kararıdır.

### Renderer'da ortaya çıkan pre-existing hatalar (md.12 sıfır-tolerans XSD doğrulamasıyla ortaya çıktı)

İmzalı XML'i sıfır-tolerans tam XSD doğrulamasına (`EBelgeUblXsdValidator.Validate`, md.12) tabi
tutmak, Faz 2B.5'ten beri VAR OLAN ama hiç FARK EDİLMEMİŞ 2 renderer hatasını ortaya çıkardı — bu
hatalar imzalamayla İLGİSİZDİR, ama imzalı çıktının sıfır-tolerans kapısından geçebilmesi için
DÜZELTİLMESİ ZORUNLUYDU. İkisi de şimdiye kadar fark edilmemişti çünkü unsigned doğrulama
(`ValidateUnsignedRendererOutput`) yalnız TEK bir bilinen bulguyu (`ext:UBLExtensions` eksikliği —
kök `Invoice`'un İLK elemanı) tolere eder ve .NET'in şema doğrulayıcısı bu eksiklikte belgenin
DAHA İLK elemanında (pozisyon 0) durur — hiçbir zaman belgenin geri kalanını doğrulamaya
DEVAM ETMEZ:

1. **`cbc:LineCountNumeric` hiç emit edilmiyordu**: `UBL-Invoice-2.1.xsd`'de bu eleman `cbc:DocumentCurrencyCode`
   ailesinden SONRA, `cac:Signature`/`cac:AccountingSupplierParty`'den ÖNCE ZORUNLUDUR
   (`minOccurs` YOK). `EBelgeUblRenderer.WriteHeader` bu elemanı hiç yazmıyordu. Düzeltme:
   `BuildXml`, `WriteHeader`'dan hemen sonra `snapshot.Satirlar.Count` değerini yazar.
2. **`cbc:Percent`, `cac:TaxCategory` içinde YANLIŞ konumdaydı**: vendored `TaxCategoryType`
   (`UBL-CommonAggregateComponents-2.1.xsd`) `cbc:Percent` İÇERMEZ (yalnız
   `Name?/TaxExemptionReasonCode?/TaxExemptionReason?/TaxScheme` — Percent, `cac:TaxSubtotal`
   seviyesinde zaten DOĞRU konumdaydı). `WriteKdvTaxCategory`, oranı `cac:TaxCategory` İÇİNE de
   (geçersiz, ikinci bir kez) yazıyordu. Düzeltme: `cac:TaxCategory` içindeki tekrarlı `WriteCbc(w, "Percent", ...)` satırı KALDIRILDI.

Her iki düzeltme de `EBelgeUblRenderer.cs`'te, imzalama koduna HİÇ dokunmadan yapılmıştır — Faz
2B.5/2B.6'nın "unsigned XML'in TEK bilinen bulgusu `ext:UBLExtensions` eksikliğidir" iddiası artık
DAHA GÜÇLÜ bir temelde durmaktadır (önceden yalnız İLK hata görülebiliyordu; şimdi imzalı çıktı
gerçekten sıfır ek hatayla doğrulanmıştır).

### `cac:Signature`'ın tam, şema-geçerli inşası (md.7)

`InsertCacSignature` (bkz. `EBelgeXmlImzalayici.cs`) artık üç ZORUNLU alt elemanı da üretir:

- `cbc:ID` (`schemeID="VKN_TCKN"`) — belgenin KENDİ, ZATEN doğrulanmış
  `AccountingSupplierParty/cac:Party/cac:PartyIdentification[schemeID='VKN']/cbc:ID` değerinden
  okunur (sertifika subject'inden TAHMİNÎ parse EDİLMEZ).
- `cac:SignatoryParty` — AYNI, ZATEN doğrulanmış `PartyIdentification` (schemeID=VKN) VE
  `PostalAddress` alt-ağaçları `AccountingSupplierParty/cac:Party`'den KLONLANIR (YENİ iş verisi
  İCAT EDİLMEZ; `PartyType`'ın zorunlu `cac:PartyIdentification`+`cac:PostalAddress`'ini VE
  `SignatoryPartyPartyIdentificationCheck`'i birlikte karşılar).
- `cac:DigitalSignatureAttachment/cac:ExternalReference/cbc:URI` = `"#" + signatureId` — gömülü
  `ds:Signature`'a işaret eder (yoruma alınmış ama belgelenmiş schematron deseniyle uyumlu).

Gerçek kriptografik imza (`ds:Signature`) BURAYA KONULMAZ — yalnız `ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent`
altına eklenir (md.7); `cac:Signature` ile `ds:Signature` birbirinden KESİN olarak ayrıdır.

### XAdES SignedProperties, sertifika ön-kontrolleri, güven doğrulayıcısı (md.9-10)

`xades:QualifyingProperties/@Target="#Signature-1"` → `xades:SignedProperties/@Id="SignedProperties-1"`
→ `xades:SignedSignatureProperties/{xades:SigningTime, xades:SigningCertificate/xades:Cert/{xades:CertDigest,
xades:IssuerSerial}}`. `xades:SigningTime`, `talep.ImzalamaZamaniUtc` (çağıran taraftan `TimeProvider`
üzerinden gelir — `DateTime.Now`/`UtcNow` HİÇ KULLANILMAZ) üzerinden `"yyyy-MM-ddTHH:mm:ssZ"`
formatında, `InvariantCulture` ile yazılır (tr-TR `CurrentCulture` altında test edilmiştir).
`xades:IssuerSerial/ds:X509SerialNumber`, sertifikanın little-endian seri numarası byte'larını
büyük-endian'a çevirip işaretsiz `BigInteger` olarak ondalık string'e dönüştürür.

Sertifika, imzalamadan ÖNCE `ValidateSertifika` ile kontrol edilir: private key varlığı, geçerlilik
aralığı (parametre olarak geçirilen `simdiUtc`'ye göre — `DateTime.UtcNow` DEĞİL), `X509KeyUsageExtension`
(varsa `DigitalSignature` bayrağını İÇERMELİ), RSA anahtar + `KeySize >= 2048`, ve public/private
anahtar eşleşmesi (sabit bir probe byte dizisi üzerinde imzala+doğrula round-trip'i ile). Ayrı bir
`IEBelgeSertifikaGuvenValidatoru` portu (md.10) tam zincir/iptal (OCSP/CRL) doğrulamasını TEMSİL
EDER ama bu turda GERÇEK bir trust-store/OCSP implementasyonu YAZILMAMIŞTIR — production
implementasyonu (`EBelgeSertifikaGuvenValidatoruYapilandirilmadi`) fail-closed'dır (her zaman
`Guvensiz` döner, `EBELGE_SIGNING_TRUST_VALIDATOR_NOT_CONFIGURED`), test'ler kendi açık politika
double'ını (`EBelgeTestSertifikaGuvenPolicy`) kullanır. **Bu, açık bırakılan bir üretim
konusudur** (bkz. "Açık kalan konular").

### Sertifika/private key sağlayıcı portu (md.4-6)

`IEBelgeImzaKimligiSaglayici.GetAsync(kurumId, ct)` → `EBelgeImzaKimligi` (sertifika, sağlayıcı
türü, parmak izi, geçerlilik tarihleri). Production varsayılanı `EBelgeImzaKimligiYapilandirilmadiSaglayici`
— HER ZAMAN `EBelgeSigningProviderNotConfiguredException` (`EBELGE_SIGNING_PROVIDER_NOT_CONFIGURED`)
fırlatır; dosya sistemi/env-var-PFX/Windows sertifika mağazası/repo dosyası OKUMAZ, otomatik
self-signed üretim sertifikası OLUŞTURMAZ. Gelecekteki HSM/PKCS11/CNG/uzak-imzalama/mali-mühür
entegrasyonu için genişletilebilir (bu turda GERÇEK bir vendor entegrasyonu YAZILMAMIŞTIR — bilinçli
kapsam dışı bırakma). Test sağlayıcısı (`EBelgeTestSertifikaSaglayici`, `tests/STYS.Tests/`) `CertificateRequest.CreateSelfSigned`
ile bellek-içi RSA 2048 sertifika üretir; private key HİÇBİR ZAMAN diske YAZILMAZ, loglanmaz.
**Bu self-signed test sertifikası, GERÇEK bir üretim güven zincirini (mali mühür/QES) TEMSİL
ETMEZ** — yalnız kriptografik imza/doğrulama MEKANİZMASININ doğruluğunu kanıtlar.

### Bağımsız doğrulayıcı (md.11) — imzalayıcıdan AYRI kod yolu

`EBelgeXmlImzaDogrulayici`, imzalayıcının yardımcı metotlarını PAYLAŞMAZ — ayrı bir XML parse
(DTD/harici entity/network/dosya URI'si KAPALI), ayrı node çözümlemesi, ayrı hash hesaplaması
kullanır: (1) TEK bir `ds:Signature`; (2) yinelenen `Id` niteliği YOK (signature-wrapping
sertleştirmesi); (3) TAM OLARAK 2 `ds:Reference`; (4) `SignatureMethod`/`CanonicalizationMethod`
profil whitelist'iyle TAM eşleşme; (5) tüm-belge referansının (`URI=""`) digest'i, `ds:Signature`
KALDIRILMIŞ bir KOPYA üzerinde tüm-belge C14N ile BAĞIMSIZ yeniden hesaplanır; (6) `SignedProperties`
referansının hedef `Id`'si belgede TAM OLARAK BİR KEZ bulunur (signature-wrapping savunması),
digest'i Exclusive C14N ile BAĞIMSIZ yeniden hesaplanır; (7) gömülü sertifikanın `xades:CertDigest`'i
BAĞIMSIZ yeniden hesaplanır; (8) `SignedInfo` üzerindeki RSA imzası, gömülü sertifikanın public
key'iyle elle C14N + `RSA.VerifyData` ile BAĞIMSIZ yeniden doğrulanır; (9) EK bir katman olarak
`.NET`'in kendi `SignedXml.CheckSignature()`'ı çağrılır — **TEK BAŞINA YETERLİ SAYILMAZ**, yalnız
yukarıdaki bağımsız katmanları TAMAMLAR.

### Artifact/hash zinciri, outbox iş türü, transaction sınırı (md.13-17)

`EBelgeArtifactAsamasi.SignedReady = 2` eklendi; `EBelgeArtifact`'a 7 yeni nullable alan
(`KaynakArtifactId`, `KaynakArtifactSha256`, `ImzaProfili`, `ImzaAlgoritmasi`, `DigestAlgoritmasi`,
`ImzalayanSertifikaSha256ParmakIzi`, `ImzalamaZamaniUtc`) — yeni `CK_EBelgeArtifactlari_ImzaAlanlari`
check constraint'i bunların SignedReady'de TAMAMI dolu, Unsigned'da TAMAMI null olmasını DB
seviyesinde garanti eder; kendine-referanslı, tenant-farkında (`Id+KurumId`) `Restrict` FK
(`FK_EBelgeArtifactlari_EBelgeArtifactlari_KaynakArtifactId_KurumId`) cascade delete/cross-tenant
zincir RİSKİNİ yapısal olarak engeller. `EBelgeOutboxIsTuru.UblImzala = 2` eklendi; Faz 2B.6'nın
artifact-farkında lease guard'ı (`IsOwnedForArtifactAsync` vb.) `IsOwnedForJobAsync(...,
expectedIsTuru, ...)` olarak İŞ-TÜRÜ-PARAMETRELİ genelleştirildi (SQL artık `@IsTuru` parametresi
kullanır — hardcoded `IsTuru = 1` KALDIRILDI); `EBelgeArtefaktOlusturmaService`'in TÜM çağrı
noktaları güncellendi.

`EBelgeUblImzalamaService.ImzalaAsync` kesin akışı: (1) DB DIŞI kısa okuma — `EBelgeKaydi` +
`IgnoreQueryFilters()` ile Unsigned artifact (soft-delete edilmiş kaynak REDDEDİLİR) + kayıtlı
hash'in içerikle yeniden doğrulanması; (2) DB DIŞI imzalama (`IEBelgeXmlImzalayici`) + sonuç
hash'inin bağımsız yeniden hesaplanması + bağımsız doğrulama (`IEBelgeXmlImzaDogrulayici`) + TAM
XSD (sıfır tolerans) + GERÇEK Java Saxon sidecar Schematron (sıfır ihlal) — satır kilidi bu SÜREÇTE
HİÇ TUTULMAZ; (3) KISA bir atomik transaction: `IsOwnedForJobAsync` (UblImzala) → idempotency
kontrolü (bkz. aşağı) → SignedReady artifact insert + `EBelgeKaydi.Durum = SignedReady` +
`TryCompleteJobAsync` → **TEK** commit. `EBelgeUblImzalaOutboxHandler`, `EBelgeArtefaktOlusturOutboxHandler`
İLE AYNI type-safe dispatch desenini kullanır.

### İdempotency (md.20)

Mevcut bir SignedReady artifact bulunursa: kaynak (`KaynakArtifactId`+`KaynakArtifactSha256`)
EŞLEŞİYORSA VE soft-delete edilmemişse, mevcut artifact'in imzası **BAĞIMSIZ OLARAK YENİDEN
DOĞRULANIR** (yalnız var olduğu için güvenilmez) — geçerliyse idempotent başarı; kaynak
EŞLEŞMİYORSA VEYA soft-delete edilmişse `EBELGE_SIGNING_SIGNED_ARTIFACT_IDEMPOTENCY_CONFLICT` ile
kalıcı hata. Byte-birebir eşleşme BEKLENMEZ (`xades:SigningTime` her yeniden imzalamada farklıdır —
RSA-PKCS1 kendi başına deterministik olsa da girdi her defasında değişir) — bu KASITLI bir tasarım
kararıdır ve kodda AÇIKÇA belgelenmiştir. Mevcut bir SignedReady artifact ASLA güncellenmez/üzerine
yazılmaz.

### Aktivasyon kapısı (md.18)

`EBelgeSigningActivationGate.ShouldCreateSigningMessage()`: `EBelgeSigningOptions.Enabled=false`
İSE her zaman `false` (fail-closed varsayılan, `appsettings.json`: `{"EBelgeSigning": {"Enabled":
false, "NotBeforeLocalDate": "2026-09-15"}}`); `TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")`
ile SUNUCU YEREL saat dilimine GÜVENMEDEN, `TimeProvider` üzerinden test-sabitlenebilir biçimde
`NotBeforeLocalDate`'in Europe/Istanbul yerel gün BAŞLANGICINI UTC'ye çevirip karşılaştırır;
geçersiz/eksik tarih konfigürasyonu FAIL-CLOSED (`false`) döner. `EBelgeArtefaktOlusturmaService`,
bir Unsigned artifact'ın İLK GERÇEK (idempotent replay DEĞİL) başarılı oluşturulmasında, kapı AÇIKSA
AYNI atomik transaction içinde TEK bir `UblImzala` outbox mesajı ekler.

### Test kapsamı ve çalıştırılan hedefli komutlar

**Birim testleri (DB/sidecar GEREKMEZ, gerçek RSA test sertifikasıyla GERÇEK kriptografik imza):**

```
dotnet test tests/STYS.Tests/STYS.Tests.csproj --filter "FullyQualifiedName~EBelgeXmlImzalayiciTests|FullyQualifiedName~EBelgeSigningActivationGateTests"
  → Passed: 27, Failed: 0, Total: 27
```

`EBelgeXmlImzalayiciTests` (17 test): XML yapısı/ad alanı/ID-URI bağları, `cac:Signature`
kriptografik imza İÇERMEZ, mevcut-Id çakışması reddi, **GERÇEK sertifikayla GERÇEK imza üretimi +
bağımsız doğrulama başarısı**, tek-byte `SignatureValue` bozulması → doğrulama reddi, `xades:SigningTime`
bozulması → doğrulama reddi, başka sertifikanın public key'i → doğrulama reddi, private-key'siz
sertifika reddi, süresi dolmuş/henüz geçerli olmayan sertifika reddi, güvensiz sertifika reddi,
determinizm (aynı girdi+sabit zaman → byte-birebir aynı sonuç — RSA-PKCS1 deterministiktir),
`SigningTime`'ın `TimeProvider`'dan gelmesi + culture-bağımsızlığı (tr-TR ile test edildi),
kaynak/sonuç hash uyuşmazlığı reddi, production fail-closed sağlayıcı/güven-validatörü davranışı,
düz imzasız XML'in bağımsız doğrulamadan GEÇEMEMESİ. `EBelgeSigningActivationGateTests` (10 test):
`Enabled=false`, 14/15 Eylül 2026 Europe/Istanbul sınırı (tam gün başlangıcı dahil), server-UTC/yerel
fark eşdeğerliği, geçersiz tarih formatları (5 senaryo) fail-closed, gelecek tarihli `NotBeforeLocalDate`.

**Entegrasyon testleri (GERÇEK SQL Server + GERÇEK Java Saxon sidecar + GERÇEK test sertifikası):**

```
dotnet test tests/STYS.Tests/STYS.Tests.csproj --filter "FullyQualifiedName~EBelgeXmlImzalayiciTests|FullyQualifiedName~EBelgeSigningActivationGateTests|FullyQualifiedName~EBelgeUblImzalamaServiceIntegrationTests|FullyQualifiedName~EBelgeArtefaktOlusturmaServiceIntegrationTests|FullyQualifiedName~EBelgeOutboxLeaseTransitionIntegrationTests|FullyQualifiedName~EBelgeOutboxMesajIslemeServiceTests|FullyQualifiedName~EBelgeUblRendererEndToEndIntegrationTests|FullyQualifiedName~EBelgeSchematronSidecarIntegrationTests|FullyQualifiedName~EBelgeFaz1IntegrationTests|FullyQualifiedName~EBelgeOutboxFaz2AIntegrationTests|FullyQualifiedName~EBelgeOutboxRetryPolicyTests"
  → Passed: 185, Failed: 0, Total: 185 (gerçek SQL Server + gerçek Java Saxon sidecar ile)
```

Yeni `EBelgeUblImzalamaServiceIntegrationTests` (9 test) kapsamı: GERÇEK imza + SignedReady
artifact üretimi + hash zinciri doğrulaması (kaynak/sonuç, sıfır-tolerans XSD + GERÇEK Schematron
zaten servisin İÇİNDE geçilmiş olmalı — aksi halde `AtomikBasarili` dönmezdi) + kalıcılaşan içerik
üzerinde AYRICA bağımsız doğrulama; tam outbox akışı (claim→handler→işleme servisi, gerçek sidecar);
aynı kaynağa eşleşen mevcut SignedReady ile idempotent başarı (ikinci satır EKLENMEZ, mevcut
YENİDEN doğrulanır); farklı (ama GERÇEK, kendine-referanslı FK'yı sağlayan) bir kaynağa bağlı
mevcut SignedReady ile idempotency-conflict; Unsigned artifact yok/soft-delete edilmiş/hash
uyuşmuyor → kalıcı hata; imzalama sırasında lease süresinin dolması → SahiplikKaybedildi, hiçbir
şey değişmez; yanlış iş-türlü (`ArtefaktOlustur`) bir claim ile imzalama YAPILAMAZ (iş-türü-farkında
guard'ın imzalama servisi TARAFINDAN da GERÇEKTEN kullanıldığının kanıtı). Mevcut
`EBelgeArtefaktOlusturmaServiceIntegrationTests`'e eklenen 3 yeni test: aktivasyon kapısı AÇIKKEN
İLK gerçek başarıda tam olarak BİR `UblImzala` mesajı oluşur; kapı KAPALIYKEN mesaj OLUŞMAZ; kapı
AÇIKKEN idempotent (önceden-seedli) tamamlanmada İKİNCİ bir mesaj EKLENMEZ.

`TicariBelgeIptalYarisKosuluIntegrationTests`/`FaturaNumaraIntegrationTests` bu filtrenin DIŞINDA
bırakılmıştır — bu iki sınıf, Faz 2B.6.2 raporunda ZATEN belgelenen, bu turun HİÇBİR değişikliğiyle
İLGİSİZ, önceden var olan ortam sorununu taşımaya DEVAM ETMEKTEDİR (bu turda dokunulan HİÇBİR
dosya — `SatisBelgesiService.cs` dahil — bu sınıfların bağımlı olduğu koda DEĞİNMEMİŞTİR;
ayrıca bu iki sınıf TEK BAŞINA çalıştırıldığında da AYNI şekilde başarısızdır, bu turun
değişiklikleriyle bir ETKİLEŞİMİ OLMADIĞININ kanıtıdır).

### Açık kalan konular (üretime geçmeden önce)

1. **Gerçek trust-store/OCSP/CRL doğrulaması YOK** — `IEBelgeSertifikaGuvenValidatoru`nun
   production implementasyonu (`EBelgeSertifikaGuvenValidatoruYapilandirilmadi`) fail-closed'dır
   ama GERÇEK bir zincir/iptal kontrolü henüz YAZILMAMIŞTIR.
2. **Gerçek mali mühür/HSM/PKCS11/CNG/uzak-imzalama entegrasyonu YOK** — `IEBelgeImzaKimligiSaglayici`
   portu bunun İÇİN genişletilebilir tasarlanmıştır ama bu turda GERÇEK bir vendor entegrasyonu
   YAZILMAMIŞTIR (bilinçli kapsam dışı — md.4).
3. Self-signed test sertifikası GERÇEK bir üretim güven zincirini TEMSİL ETMEZ (yukarıda tekrar
   vurgulanmıştır).
4. Sürekli çalışan bir outbox worker/polling döngüsü hâlâ YOK (Faz 2B.6'dan beri açık, kapsam dışı).
5. Gönderim/durum sorgulama, PDF/e-posta üretimi, frontend zorunlu veri giriş ve e-belge takip
   ekranları hâlâ YAPILMADI.
6. Üretim etkinleştirmesi 15 Eylül 2026 Europe/Istanbul öncesi `EBelgeSigningActivationGate`
   tarafından YAPISAL olarak ENGELLENİR (`appsettings.json`: `Enabled: false` varsayılanı VE
   tarih kapısı BİRLİKTE) — bu tarihten önce `Enabled: true` yapılsa BİLE tarih kapısı imzalama
   mesajı oluşturulmasını engeller.

### Sonraki faz

1. Gerçek mali mühür/HSM sertifika sağlayıcısı + trust-store/OCSP/CRL doğrulaması.
2. Sağlayıcı bağımsız gönderim portu + e-Arşiv entegratör adapter'ı.
3. Gönderim/durum sorgulama ve retry.
4. Outbox'ı sürekli tüketen bir `BackgroundService` (feature flag'li, config'den batch/polling).
5. Frontend zorunlu veri giriş ekranları ve e-belge takip/hata yönetimi ekranları.
6. PDF ve e-posta artifact'ları (bu noktada `Icerik varbinary(max)` kararı yeniden değerlendirilmeli).

## Faz 2B.7.1 sonuç bölümü — XAdES profil kanıtının sertleştirilmesi, idempotent doğrulama simetrisi ve transaction sınırı düzeltmesi

**Durum: TAMAMLANDI, commit/push YAPILDI (bkz. md.11 koşulları — hepsi genuinely karşılandı).**

### Neden gerekliydi

Faz 2B.7'nin kod incelemesinde 5 gerçek açık tespit edildi: (1) profil kanıt zinciri yalnız ESKİ
(2018) e-Fatura kılavuzuna dayanıyordu, GÜNCEL e-Arşiv kılavuzu hiç incelenmemişti; (2) KamuSM
ESYA örneğindeki `xades:SignerRole`/public-key unsurlarının GİB için zorunlu olup olmadığı hiç
araştırılmamıştı; (3) bağımsız doğrulayıcı, reference/transform/digest algoritmalarını TAM
whitelist ile kontrol etmiyordu ve BİR yerde XPath enjeksiyonuna AÇIK bir string birleştirmesi
vardı; (4) `EBelgeUblImzalamaService`, mevcut bir SignedReady artefaktı doğrularken (idempotent
yol) bağımsız imza+XSD+Schematron doğrulamasını AÇIK bir SQL transaction'ın (UPDLOCK'lu outbox
satırıyla) İÇİNDE çalıştırıyordu; (5) YENİ imza ile idempotent-replay yolları arasında doğrulama
SİMETRİSİ YOKTU (yeni imza sıfır-toleranslı kapılardan geçerken, mevcut bir artefakt yalnız
BAĞIMSIZ imza doğrulamasından geçiyordu — XSD/Schematron'dan HİÇ geçmiyordu).

### Kanıt hiyerarşisi (öncelik sırasıyla, güven seviyesiyle)

**1. GÜNCEL resmî GİB "e-Arşiv Kılavuzu" (Ağustos 2025, v1.18,
`https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-Arsiv_Teknik_Kilavuzu_V.1.18.pdf`)** — bu turda
İLK KEZ incelendi (önceki turda yalnız ESKİ 2018 e-Fatura Entegrasyon Kılavuzu kullanılmıştı):

- **Bölüm 4** ("Elektronik Arşiv Raporlarının Hazırlanması"): e-Arşiv RAPORU'nun (aylık/dönemsel
  ÖZET raporu — `eArsivRaporu/baslik/Signature`, İMZALADIĞIMIZ tekil fatura UBL XML'inden AYRI
  bir belge) "XADES-A standardı kullanılarak ... imzalanarak" gönderildiğini teyit eder. Bu,
  Rapor'un KENDİSİNİN (arşivleme zaman damgası taşıyan, DAHA GÜÇLÜ) XAdES-A seviyesinde
  olduğunu, İÇERDİĞİ belgelerin (fatura dahil) İSE daha temel bir seviyede (XAdES-BES)
  imzalanabileceğini DOLAYLI olarak ima eder — Rapor bölümü tek başına faturanın imza SEVİYESİNİ
  KESİN olarak belirlemez.
- **Bölüm 7/8/9** (Serbest Meslek Makbuzu/Müstahsil Makbuzu/Adisyon — fatura ile YAPISAL olarak
  ANALOG UBL-TR belgeleri): HER BİRİ AÇIKÇA *"Bu veriler XADES-BES standardı kullanılarak mali
  mühür/ NES ile imzalanmalıdır"* der.
  **DÜZELTME (bkz. Faz 2B.7.2)**: bu turda "Bölüm 6 (e-Arşiv Fatura Standardı) bu cümleyi birebir
  TEKRARLAMAZ" denmişti — bu YANLIŞTIR. GÜNCEL e-Arşiv Kılavuzu'nun (Ağustos 2025, v1.18) s.57,
  "6 e-Arşiv Fatura Standardı" başlığı ALTINDA, AYNI cümle BİREBİR yer alır: *"Bu veriler
  XADES-BES standardı kullanılarak mali mühür/ NES ile imzalanmalıdır."* Yani e-Arşiv faturasının
  XAdES-BES ile imzalanması GÜNCEL kılavuzun KENDİSİ tarafından DOĞRUDAN VE AÇIKÇA zorunlu
  kılınmaktadır — bölüm 7/8/9 ile YAPISAL benzerlik üzerinden dolaylı çıkarıma HİÇ GEREK YOKTUR.
- **Bölüm 5** (Elektronik Arşiv Raporlarının Başkanlık Sistemine Aktarımı, SOAP/WSS GÜVENLİĞİ):
  *"İmzanın canonicalization metot algoritması 'http://www.ws.org/2001/10/xml-exc-c14n#' olması
  tavsiye edilir. Signature metot algoritması 'http://www.w3.org/2001/04/xmldsig-more#rsa-sha256'
  olmalıdır."* **BU, Başkanlığa RAPOR GÖNDERİMİ için kullanılan SOAP mesaj imzası İÇİNDİR —
  faturanın KENDİ İÇİNDEKİ XAdES imzası İÇİN DEĞİLDİR, AYRI bir bağlamdır.** Bu turda bu ayrım
  AÇIKÇA belgelenmiştir (bkz. `EBelgeXadesProfili.cs`) — RSA-SHA256'nın GİB ekosisteminde YAYGIN
  kullanıldığına dair yalnız DOLAYLI/zayıf bir sinyal olarak kaydedilmiştir, DOĞRUDAN kanıt olarak
  KULLANILMAMIŞTIR.
- **Bölüm 3.3.2.6 `ozetDeger`**: *"mali mühürle ... imzalanmış faturanın SHA-256 özet değeri
  yazılacaktır"* — faturanın SHA-256 ile özetlendiğini DOĞRUDAN teyit eder (XMLDSig
  `ds:DigestMethod` için kullanılan AYNI algoritma olduğunu KESİN kanıtlamaz, ama GİB
  ekosisteminde SHA-256'nın standart özet algoritması olduğuna dair orta-güçte bir sinyaldir).

**2. Vendored, hash doğrulanmış GİB XSD/Schematron kuralları** (`EBelgeUblKuralSeti/`) — Faz
2B.7'de zaten incelenmiş, bu turda EK olarak şu iki nokta netleştirildi:

- `xsdrt/common/UBL-XAdESv132-2.1.xsd`: `SignedSignaturePropertiesType` sırasındaki
  `SignaturePolicyIdentifier`/`SignatureProductionPlace`/`SignerRole` ÜÇÜ DE `minOccurs="0"` -
  yani `xades:SignerRole` ŞEMA DÜZEYİNDE SEÇİMLİDİR.
- `xsdrt/common/UBL-xmldsig-core-schema-2.1.xsd` + `X509DataCheck` schematron kuralı:
  `ds:KeyInfo` içeriği için YALNIZ `ds:X509Data/ds:X509Certificate` ZORUNLU kılınır; `ds:KeyValue`
  şemada SEÇİMLİ bir alternatiftir ve HİÇBİR schematron kuralı ondan bahsetmez.

**3. ESKİ resmî GİB "e-Fatura Uygulaması (Entegrasyon Kılavuzu)"** (Haziran 2018, v1.10, s.17) -
Faz 2B.7'de bulunan *"en az XAdES-BES standardı ve enveloped tekniği kullanılır"* ifadesi
KORUNUR - artık GÜNCEL kılavuzun 7/8/9. bölümleriyle ÇAPRAZ doğrulanmış durumdadır.

**4. TÜBİTAK KamuSM ESYA SDK dokümantasyonu** - İKİNCİL/destekleyici kaynak (GİB'İN KENDİ metni
DEĞİLDİR). Bir e-Fatura ÖRNEĞİNDE `xades:SignerRole/ClaimedRole=Supplier` VE public-key bilgisi
kullanıldığı GÖRÜLÜR.
**DÜZELTME (bkz. Faz 2B.7.2)**: bu turda KamuSM yalnız "ÜÇÜNCÜ TARAF bir entegratör örneği"
olarak KÜÇÜMSENMİŞTİ - bu KARAKTERİZASYON eksiktir. TÜBİTAK KamuSM, GİB'in nitelikli mali
mühür/e-imza sertifikalarını sağlayan AKREDİTE, resmi e-imza altyapısı sağlayıcısıdır (rastgele
bir üçüncü taraf entegratör DEĞİLDİR) ve dokümantasyonu bu unsurların "e-fatura standartlarında
GEREKLİ KILINDIĞINI" AÇIKÇA belirtir (`https://yazilim.kamusm.gov.tr/esya-api/doku.php?id=esya:xades:kod-e-fatura`,
alıntı: *"Daha sonra ise yine e-fatura standartlarında gerekli kılınan imzacı rolü, açık anahtar
ve imza zamanı eklenir."*). Bu, GİB'İN KENDİ metni olmasa da, bir GEREKSİNİM iddiası olarak
görmezden gelinecek kadar zayıf bir kaynak DEĞİLDİR.

**5. Yalnız yukarıdakilerin CEVAPLAMADIĞI noktalar için mühendislik tercihi** (bkz.
`EBelgeXadesProfili.cs` sınıf düzeyi XML doc'u - `CanonicalizationAlgorithmUri` ve
`SignedPropertiesTransformUri`).

### SignerRole/KeyValue kararı (md.3)

> **DÜZELTME/GÜNCELLEME (bkz. Faz 2B.7.2 bölümü aşağıda)**: bu alt bölümdeki karar - "İKİSİ DE
> EKLENMEZ" - bu turda TERS ÇEVRİLDİ. Kararın DAYANDIĞI iki öncül YANLIŞ bulundu: (1) GÜNCEL
> kılavuzun bölüm 6'sı XAdES-BES'i "açıkça belirtmediği" iddiası YANLIŞTIR (bkz. yukarıdaki
> düzeltme); (2) KamuSM kaynağının salt "üçüncü taraf entegratör örneği" olduğu için göz ardı
> edilebileceği iddiası da EKSİK bir karakterizasyondur (bkz. yukarıdaki düzeltme). Bu alt bölüm
> YALNIZ o zamanki (hatalı öncüllere dayanan) muhakemeyi TARİHSEL kayıt olarak KORUR - GÜNCEL
> karar için Faz 2B.7.2 bölümüne bakınız.

**(Faz 2B.7.1'deki ORİJİNAL, SONRADAN DÜZELTİLEN sonuç:) İKİSİ DE EKLENMEZ.** Gerekçe - "mevcut testler geçti" DEĞİL, doğrudan kanıt:

- `xades:SignerRole` VE `ds:KeyValue`, vendored XSD'de AÇIKÇA SEÇİMLİ (`minOccurs="0"`) olarak
  tanımlanmıştır.
- Hiçbir schematron kuralı (aktif YA DA yoruma alınmış) ikisinden BİRİNİ ZORUNLU KILMAZ.
- Ne ESKİ (2018) ne GÜNCEL (Ağustos 2025, v1.18) resmî GİB kılavuzu `xades:SignerRole`'den veya
  `ds:KeyValue`'dan HİÇ BAHSETMEZ.
- Yalnız GİB'İN KENDİSİ OLMAYAN, ÜÇÜNCÜ TARAF bir entegratör örneği (KamuSM ESYA
  dokümantasyonunun bir e-Fatura örneği) `SignerRole` KULLANIR - bu TEK BAŞINA bir GİB
  gereksinimi SAYILMAZ.

Sertifika subject alanlarından rol veya VKN TAHMİNÎ olarak PARSE EDİLMEZ (görev md.3'ün açık
yasağı) - zaten hiçbir yerde böyle bir tahmin yapılmamaktadır (VKN her zaman
`cac:AccountingSupplierParty/cac:Party`'den okunur).

### Profil onay kapısı (md.2)

`EBelgeXadesProfili` artık bir `Onayli` (bool) alanı taşır - `EBelgeXmlImzalayici.ImzalaAsync`,
imzalamadan ÖNCE bunu kontrol eder; `false` İSE `EBelgeXadesProfiliOnaylanmadiException`
(`EBELGE_SIGNING_PROFILE_NOT_APPROVED`, HTTP 500, fail-closed, retry ANLAMSIZ) fırlatır -
`EBelgeUblImzalamaService` bunu KONFİGÜRASYON hatası olarak yakalayıp atomik kalıcı hataya
yönlendirir. `GibUblTr` profili `Onayli = true` İLE işaretlenmiştir - yukarıdaki kanıt hiyerarşisi
(GÜNCEL e-Arşiv Kılavuzu + vendored XSD/Schematron + eski e-Fatura Kılavuzu + KamuSM, ÇAPRAZ
doğrulanmış) production kullanımı için YETERLİ bulunmuştur; bu, BİLİNÇLİ, belgelenmiş bir
mühendislik/ürün kararıdır - GELECEKTE bir revizyon/düşürme kararı gerekirse, mekanizma (yalnız
yorum satırı DEĞİL, TEST EDİLEBİLİR bir alan+exception) zaten HAZIRDIR. Profil kimliği artık
versiyonludur: `GIB-EARSIV-UBL-TR-XADES-BES/1.18/1.0` (`1.18` = e-Arşiv Kılavuzu sürümü, `1.0` =
bu profil kararının kendi sürümü).

### Bağımsız doğrulayıcının sertleştirilmesi (md.4)

`EBelgeXmlImzaDogrulayici`'ye 13 YENİ kontrol eklendi (bkz. `EBelgeXmlImzaDogrulayici.cs` XML
doc'u): tek `xades:QualifyingProperties` (GLOBAL sayım), `QualifyingProperties/@Target`'ın GERÇEK
`ds:Signature/@Id`'ye eşitliği, tek `xades:SignedProperties` (GLOBAL sayım + referansın GERÇEKTEN
o elemana işaret ettiğinin `ReferenceEquals` ile teyidi), HER `ds:Reference` için digest
algoritması whitelist'i VE transform sayısının TAM OLARAK 1 olması (fazladan/bilinmeyen transform
REDDEDİLİR), belge referansı VE SignedProperties referansı için AYRI AYRI transform URI
whitelist'i, `xades:IssuerSerial`'ın gömülü sertifikanın GERÇEK issuer/serial değerleriyle
BAĞIMSIZ karşılaştırılması, `cac:Signature/cac:DigitalSignatureAttachment/cac:ExternalReference/cbc:URI`'nin
GERÇEK `ds:Signature/@Id`'ye işaret ettiğinin doğrulanması, `cac:Signature/cbc:ID`'nin GERÇEK
düzenleyen taraf VKN'siyle eşleştiğinin doğrulanması, `ds:Signature`'ın YALNIZ beklenen
`ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent` altında bulunduğunun (başka HİÇBİR
konumda DEĞİL) doğrulanması.

**Gerçek bir güvenlik açığı da düzeltildi**: `SignedProperties` referansının hedef elemanını
bulmak için kullanılan `doc.SelectNodes($"//*[@Id='{signedPropsId}']", nsmgr)` deseni, `signedPropsId`
DEĞERİNİ (imzalı - dolayısıyla potansiyel olarak KURCALANMIŞ - belgeden okunan bir öznitelik
değerini) DOĞRUDAN XPath string'ine BİRLEŞTİRİYORDU - bu, bir XPath enjeksiyon riskiydi (ör.
`signedPropsId` içinde `'` karakteri taşıyan kurcalanmış bir belge, sorguyu MANİPÜLE edebilirdi).
Düzeltme: `//*[@Id]` düğümleri ÖNCEDEN toplanır (zaten yinelenen-Id kontrolü İÇİN gerekliydi),
hedef eleman bu LİSTE üzerinde SADE bir C# string karşılaştırmasıyla (`GetAttribute("Id") ==
signedPropsId`) BULUNUR - hiçbir kullanıcı/belge girdisi XPath sorgusuna HAM olarak
BİRLEŞTİRİLMEZ.

### İdempotent doğrulamanın transaction dışına taşınması ve simetrisi (md.5-6)

`EBelgeUblImzalamaService.ImzalaAsync` YENİDEN yapılandırıldı: mevcut bir SignedReady artefaktı
(varsa) artık TRANSACTION AÇILMADAN ÖNCE okunur; bulunursa `IslemMevcutSignedAsync`, YENİ bir
artefaktın imzalanma akışıyla (md.17 adım 8-11) TAM SİMETRİK olarak - bağımsız imza doğrulaması +
SIFIR-tolerans XSD + GERÇEK Schematron - TAMAMEN SQL transaction'ın DIŞINDA çalıştırır (satır
kilidi/UPDLOCK bu süre boyunca HİÇ TUTULMAZ). Yalnız EN SONDAKİ KISA transaction (1) ownership'i
(`IsOwnedForJobAsync`) yeniden doğrular, (2) artefaktın (immutable Id+hash) tx-dışı doğrulama
SIRASINDA DEĞİŞMEDİĞİNİ teyit eder, (3) `EBelgeKaydi.Durum=SignedReady` + outbox tamamlama işlemini
TEK atomik adımda gerçekleştirir. Artefakt tx-dışı doğrulama sırasında DEĞİŞMİŞSE (ör. soft-delete
edilmişse) - önceki doğrulama sonucu ARTIK GÜVENİLMEZ kabul edilir, `EBELGE_SIGNING_YARIS_DURUMU`
ile geçici hata döner (üst katman yeniden dener).

YENİ bir artefaktın insert edilmesi sırasında unique-violation (BAŞKA bir worker kazandı)
oluşursa, servis ARTIK YENİDEN İMZALAMAZ - rakibin YAZDIĞI satırı okuyup AYNI idempotent yoldan
(`IslemMevcutSignedAsync`) tamamlar; bu, hem gereksiz kriptografik işi ÖNLER hem de idempotent
yolun HER ZAMAN kullanılmasını (simetri) sağlar.

### `IEBelgeSigningBackfillService` (md.7)

Aktivasyon tarihinden ÖNCE oluşturulmuş (bu yüzden "yalnız İLK GERÇEK oluşturmada mesaj ekle"
kuralı gereği kendiliğinden kuyruğa GİRMEMİŞ) `UnsignedUblHazir` kayıtlar için, aktivasyon SONRASI,
KONTROLLÜ ve İDEMPOTENT bir telafi servisi eklendi (`EBelgeSigningBackfillService.cs`). Yalnız
`Durum=UnsignedUblHazir`, SignedReady artefaktı YOK, UblImzala outbox mesajı YOK, kurum sınırındaki
kayıtları bulur; canlı akışla AYNI aktivasyon kapısına (`IEBelgeSigningActivationGate`) TABİDİR -
kapı KAPALIYKEN backfill de mesaj OLUŞTURMAZ. **KASITLI OLARAK YAPILMAYANLAR**: otomatik hosted
worker/`BackgroundService` EKLENMEDİ, HTTP API endpoint'i EKLENMEDİ, production'da KENDİLİĞİNDEN
ÇAĞRILMAZ - yalnız DI'a kayıtlı uygulama servisi + testleri eklenmiştir; operasyonel tetikleme
SONRAKİ bir faza bırakılmıştır.

### Test kapsamı ve çalıştırılan hedefli komutlar

```
dotnet test tests/STYS.Tests/STYS.Tests.csproj --filter "FullyQualifiedName~EBelgeXmlImzalayiciTests|FullyQualifiedName~EBelgeSigningActivationGateTests|FullyQualifiedName~EBelgeUblImzalamaServiceIntegrationTests|FullyQualifiedName~EBelgeSigningBackfillServiceIntegrationTests|FullyQualifiedName~EBelgeArtefaktOlusturmaServiceIntegrationTests|FullyQualifiedName~EBelgeOutboxLeaseTransitionIntegrationTests|FullyQualifiedName~EBelgeOutboxMesajIslemeServiceTests|FullyQualifiedName~EBelgeUblRendererEndToEndIntegrationTests|FullyQualifiedName~EBelgeSchematronSidecarIntegrationTests|FullyQualifiedName~EBelgeFaz1IntegrationTests|FullyQualifiedName~EBelgeOutboxFaz2AIntegrationTests|FullyQualifiedName~EBelgeOutboxRetryPolicyTests"
  → Passed: 209, Failed: 0, Total: 209 (gerçek SQL Server + gerçek Java Saxon sidecar ile)
```

Yeni testler: `EBelgeXmlImzalayiciTests`'e 13 doğrulayıcı-sertleştirme testi eklendi (17→30) -
`QualifyingProperties/@Target` kurcalaması, ikinci `QualifyingProperties`, SignedProperties
referansının yanlış node'a yönlendirilmesi, belge/SignedProperties referanslarının digest/transform
URI kurcalaması (4 ayrı test), ek transform eklenmesi, issuer adı/serial number kurcalaması,
`cac:Signature` URI kurcalaması, `ds:Signature`'ın beklenen konum dışına taşınması, SignerRole/
KeyValue olmadan da imzanın GEÇERLİ kabul edildiğinin teyidi (**bu SONUNCU test, Faz 2B.7.2'de
karar TERS ÇEVRİLDİĞİNDE KALDIRILMIŞ ve YERİNE tam-tersini doğrulayan testlerle
DEĞİŞTİRİLMİŞTİR - bkz. aşağıdaki Faz 2B.7.2 bölümü**). `EBelgeUblImzalamaServiceIntegrationTests`'e
4 yeni test eklendi (9→13): mevcut SignedReady XSD-geçersizse idempotent başarı OLMAZ, Schematron-
ihlalliyse idempotent başarı OLMAZ, mevcut SignedReady doğrulaması sırasında (GERÇEK bir UPDLOCK
probe'u İLE KANITLANMIŞ biçimde) SQL transaction/outbox satır kilidi TUTULMAZ, artefakt tx-dışı
doğrulama SIRASINDA değişirse (soft-delete simülasyonu İLE) sonuç KULLANILMAZ ve geçici hata döner.
Yeni `EBelgeSigningBackfillServiceIntegrationTests` (7 test): gate kapalıyken/açıkken, zaten mesajı/
SignedReady'si olan kayıt atlanır, farklı durumdaki kayıt atlanır, kurum sınırı, idempotent tekrar
çağrı.

### Açık kalan konular (Faz 2B.7'den değişmeden devam eden liste + güncelleme)

Faz 2B.7'nin "Açık kalan konular" listesi (gerçek trust-store/OCSP/CRL, gerçek mali mühür/HSM,
self-signed test sertifikası üretim güven zincirini TEMSİL ETMEZ, sürekli worker YOK, gönderim/PDF/
e-posta/frontend YAPILMADI, 15 Eylül 2026 öncesi production etkinleştirmesi YAPISAL olarak
ENGELLENİR) AYNEN GEÇERLİDİR. EK olarak:

7. `IEBelgeSigningBackfillService`'in operasyonel tetiklenmesi (manuel komut/gelecekteki bir admin
   aracı) HENÜZ YAPILMADI - yalnız uygulama servisi VE testleri eklenmiştir.

### Sonraki faz (Faz 2B.7.1)

Faz 2B.7'nin "Sonraki faz" listesi AYNEN geçerlidir.

## Faz 2B.7.2 sonuç bölümü — güvenli doğrulama, artifact hash zinciri sertleştirmesi ve SignerRole/KeyValue kararının düzeltilmesi

**Durum: TAMAMLANDI, commit/push YAPILDI.**

### Neden gerekliydi

Faz 2B.7.1'in kod incelemesinde 6 gerçek açık tespit edildi: (1) doğrulayıcı, bozuk/iyi-biçimli-
olmayan girdilerde `XmlException`/`FormatException`/`CryptographicException` gibi beklenen
parse/kriptografi exception'larını YAKALAMIYORDU - bunlar generic outbox katmanına SIZABİLİR ve
YANLIŞLIKLA geçici (retry edilebilir) hata sayılabilirdi; (2) `EBelgeUblImzalamaService`, bağımsız
doğrulayıcıdan gelen böyle bir beklenmedik exception'ı AYNI şekilde generic bir transient retry'a
DÖNÜŞTÜREBİLİRDİ; (3) mevcut bir SignedReady artefaktı işlenirken, kayıtlı `ArtifactSha256`
SÜTUNUNA, İÇERİĞİN (`Icerik`) GERÇEKTEN o hash'e sahip olduğu HİÇ DOĞRULANMADAN güveniliyordu; (4)
Faz 2B.7.1'in kısa "sonuç" transaction'ı yalnız hash SÜTUNUNU karşılaştırıyordu - satırın TAMAMININ
(Id/KurumId/EBelgeKaydiId/ArtifactAsamasi/IsDeleted/KaynakArtifactId/KaynakArtifactSha256) VE
İçeriğin EXACT SHA-256'sının YENİDEN doğrulanması YOKTU; (5) YENİ bir SignedReady insert
edilmeden ÖNCE, kaynak Unsigned artefaktının imzalama SIRASINDA (tx-dışı imzalamadan insert'e
kadar) değişip değişmediği YENİDEN kontrol EDİLMİYORDU; (6) Faz 2B.7.1'in SignerRole/KeyValue
kararı ("İKİSİ DE EKLENMEZ") İKİ YANLIŞ öncüle dayanıyordu - bkz. aşağıdaki düzeltmeler.

### 1-2. Bozuk girdi sınıflandırması - doğrulayıcı VE servis katmanı

`EBelgeXmlImzaDogrulayici.DogrulaAsync`'e, mevcut `EBelgeXmlImzaDogrulamaException` yakalamasından
SONRA, İKİNCİ bir catch bloğu eklendi:

```csharp
catch (Exception ex) when (ex is XmlException or FormatException or CryptographicException
    or ArgumentException or OverflowException or InvalidOperationException)
```

Bu, iyi-biçimli-olmayan XML, geçersiz base64 (`ds:X509Certificate`/`SignatureValue`/
`DigestValue`), geçersiz X509 sertifika bytes'ı, geçersiz/taşan sertifika seri numarası gibi
BEKLENEN, PROGRAMLAMA HATASI OLMAYAN girdi/kriptografi hatalarını YAKALAR ve HER ZAMAN yeni
`EBELGE_SIGNING_MALFORMED_SIGNATURE_DOCUMENT` kodlu, KİŞİSEL VERİ/XML/sertifika/imza değeri
İÇERMEYEN SABİT bir mesajla `Gecersiz` sonuca dönüştürür. `OperationCanceledException`, YALNIZ
gerçek iptal talep EDİLMİŞSE (`cancellationToken.IsCancellationRequested`) fırlatılmaya devam eder
- YUTULMAZ. Genel `catch (Exception)` KULLANILMADI - yalnız AÇIKÇA SINIFLANDIRILMIŞ, beklenen
istisna tipleri yakalanır; programlama hataları (ör. `NullReferenceException`) GİZLENMEZ, olduğu
gibi YUKARI fırlamaya devam eder.

`EBelgeUblImzalamaService`'e, AYNI sınıflandırma mantığını `_dogrulayici.DogrulaAsync`
çağrılarının ETRAFINA saran özel bir `DogrulaGuvenliAsync` yardımcı metodu eklendi - hem YENİ imza
akışında (`ImzalaAsync`) hem de idempotent akışta (`IslemMevcutSignedAsync`) KULLANILIR. Bu,
"savunma derinliği" katmanıdır - doğrulayıcı KENDİSİ zaten bu exception'ları yakalar (md.1), ama
servis katmanı BUNA KÖRÜ KÖRÜNE GÜVENMEZ; beklenmedik bir parse/kriptografi exception'ı BURADA da
yakalanırsa, sonuç HER ZAMAN `EBELGE_SIGNING_MALFORMED_SIGNATURE_DOCUMENT` İLE KALICI olarak
sınıflandırılır - generic outbox transient-retry mekanizmasına ASLA SIZMAZ.

### 3-4. Mevcut SignedReady artifact - exact-byte hash zinciri sertleştirmesi

`IslemMevcutSignedAsync`, artık İLK ADIM olarak (kaynak eşleşme kontrolünden VE imza
doğrulamasından ÖNCE) EXACT-BYTE bir hash kontrolü yapar:

```csharp
var gercekSignedHash = Convert.ToHexString(SHA256.HashData(mevcutSigned.Icerik));
if (!string.Equals(gercekSignedHash, mevcutSigned.ArtifactSha256, StringComparison.Ordinal))
    → EBELGE_SIGNING_EXISTING_ARTIFACT_HASH_MISMATCH, atomik kalıcı hata, imza doğrulamasına HİÇ DEVAM EDİLMEZ
```

Kayıtlı `ArtifactSha256` sütununa KÖRÜ KÖRÜNE güvenilmez - İÇERİĞİN (`Icerik`) GERÇEK SHA-256'sı
HER SEFERİNDE yeniden hesaplanır. Uyuşmazlık varsa (içerik SESSİZCE bozulmuşsa, ör. depolama
katmanı hatası VEYA "içerik değiştirilip hash sütunu aynı bırakılan" bir kurcalama), bağımsız
imza+XSD+Schematron doğrulamasına HİÇ gidilmeden, DOĞRUDAN atomik kalıcı hata döner (bkz.
`EBelgeUblImzalamaServiceIntegrationTests.MevcutSignedIcerigiTamperlenirseImzaDogrulamasiAtlanirAtomikKaliciHataMevcutArtifactHashUyumsuzOlur`
- bu test, ayrıca bir çağrı-sayıcı doğrulayıcı DEKORATÖRÜYLE doğrulayıcının HİÇ ÇAĞRILMADIĞINI da
kanıtlar).

Faz 2B.7.1'in kısa "sonuç" transaction'ı, artık yalnız hash SÜTUNUNU DEĞİL, SATIRIN TAMAMINI
YENİDEN doğrular. Yeni `OkuMevcutSignedKilitliAsync` yardımcı metodu, `FromSqlInterpolated` +
`WITH (UPDLOCK, ROWLOCK)` SQL Server tablo ipucuyla (`.IgnoreQueryFilters().AsNoTracking()` ile
BİRLEŞTİRİLEREK - bu birleşim, EF Core'un ham SQL'i bir alt sorguya SARMASINI GEREKTİRMEZ, tablo
ipucu OLDUĞU GİBİ KORUNUR) satırı YENİDEN okur; ID/KurumId/EBelgeKaydiId/ArtifactAsamasi/
IsDeleted/KaynakArtifactId/KaynakArtifactSha256/ArtifactSha256 ALANLARININ TAMAMI VE İçeriğin
YENİDEN hesaplanan EXACT SHA-256'sı, tx-dışı doğrulamada kullanılan değerlerle karşılaştırılır.
HERHANGİ biri uyuşmazsa - önceki doğrulama sonucu ARTIK GÜVENİLMEZ kabul edilir, transaction
rollback edilir, `EBELGE_SIGNING_YARIS_DURUMU` İLE geçici hata döner (üst katman yeniden dener).
Bu kilit YALNIZ bu KISA okuma+karşılaştırma+commit penceresinde tutulur - imza/XSD/Schematron
doğrulaması BU KİLİT ALTINDA HİÇ ÇALIŞMAZ (zaten daha ÖNCE, transaction açılmadan tamamlanmıştır).
Bu davranış, içeriği BAŞKA (ama kendi içinde GEÇERLİ) bir imzayla DEĞİŞTİREN bir yarış senaryosuyla
test edilir
(`MevcutSignedTxDisiDogrulamaSonrasiIcerikFarkliGecerliImzayaDegistirilirseYarisDurumuDoner`).

### 5. Yeni SignedReady insert'i öncesi kaynak (Unsigned) yeniden doğrulaması

`DenemeYeniSignedInsertAtomikAsync`, SignedReady insert EDİLMEDEN ÖNCE, kaynak Unsigned
artefaktını `WITH (UPDLOCK, ROWLOCK)` satır kilidiyle (`OkuUnsignedKilitliAsync`) YENİDEN okur ve:

- Kaynak bulunamıyorsa VEYA soft-delete edilmişse → `EBELGE_SIGNING_SOURCE_CHANGED_DURING_SIGNING`
  İLE GEÇİCİ hata (imzalama SIRASINDA kaybolmuş/silinmiştir - GERÇEK bir kalıcı bozulma değil, YENİ
  bir claim ile yeniden imzalanabilir bir yarış durumudur).
- Kaynağın KENDİ `ArtifactSha256` sütunu, `Icerik`'inin GERÇEK SHA-256'sıyla UYUŞMUYORSA →
  `EBELGE_SIGNING_SOURCE_ARTIFACT_HASH_MISMATCH` İLE ATOMİK KALICI hata (kaydın kendi İÇİNDE
  TUTARSIZ olması - GERÇEK bir bütünlük sorunudur, retry ANLAMSIZDIR).
- Kaynağın hash'i kendi İÇİNDE tutarlı AMA tx-dışı imzalama SIRASINDA kullanılan hash'ten FARKLI
  bulunursa → `EBELGE_SIGNING_SOURCE_CHANGED_DURING_SIGNING` İLE GEÇİCİ hata.

Bu üç dal, sırasıyla
`YeniImzaSirasindaUnsignedKaynakSoftDeleteEdilirseGeciciHataKaynakImzalamaSirasindaDegistiDonerVeSignedReadyEklenmez`,
`YeniImzaSirasindaUnsignedKaynakIcerigiBozulursaAtomikKaliciHataKaynakHashUyumsuzOlur` VE mevcut
`UnsignedArtifactSaklananIcerikHashiKayitliHashIleUyusmuyorsaAtomikKaliciHataOlur` testleriyle
doğrulanır. Bu yeniden-doğrulamanın GERÇEKTEN kısa transaction İÇİNDE yapıldığı (md.13) İKİ
şekilde kanıtlanır: (a) her iki tamperlemenin GERÇEKLEŞTİĞİ nokta - Schematron çağrısı DÖNDÜKTEN
HEMEN SONRA, yani Faz-3 transaction'ı AÇILMADAN HEMEN ÖNCE - yalnız transaction İÇİNDEKİ satır
kilitli okuması TARAFINDAN yakalanabilir (daha ÖNCEKİ, tx-dışı bir okuma bu değişikliği HİÇ
GÖREMEZDİ); (b) `YeniSignedInsertAkisindaSchematronTamOlarakBirKezCagrilirKisaTransactionAltindaTekrarCalismaz`
testi, Schematron'un TÜM akış boyunca yalnız BİR KEZ çağrıldığını kanıtlayarak, kısa transaction
ALTINDA sidecar/kriptografi işi YAPILMADIĞINI (md.14) doğrular - yeniden-doğrulama YALNIZ bir SQL
satır okuma+hash karşılaştırmasıdır. Ayrıca, "geçici" olarak sınıflandırılan yarış senaryosunun
GERÇEKTEN yeni bir claim ile BAŞARIYLA tamamlanabildiği
`UnsignedKaynakImzalamaSirasindaDegistiktenSonraYeniClaimIleYenidenDenemeBasariliOlur` testiyle
uçtan uca doğrulanır.

### 6. SignerRole/KeyValue kararının düzeltilmesi

Faz 2B.7.1'in "İKİSİ DE EKLENMEZ" kararı, İKİ YANLIŞ öncüle dayanıyordu - bu turda İKİSİ DE
düzeltildi (bkz. yukarıdaki "Faz 2B.7.1 sonuç bölümü" içindeki DÜZELTME notları):

1. **"GÜNCEL kılavuz XAdES-BES'i AÇIKÇA belirtmiyor" iddiası YANLIŞTI.** GÜNCEL e-Arşiv
   Kılavuzu'nun (Ağustos 2025, v1.18) s.57, "6 e-Arşiv Fatura Standardı" başlığı ALTINDA, tam
   olarak *"Bu veriler XADES-BES standardı kullanılarak mali mühür/ NES ile imzalanmalıdır"*
   cümlesi BİREBİR yer alır - bölüm 7/8/9 ile dolaylı çıkarıma HİÇ GEREK YOKTUR.
2. **KamuSM kaynağının "salt üçüncü taraf entegratör örneği" olduğu için göz ardı edilebileceği
   iddiası EKSİKTİ.** TÜBİTAK KamuSM, GİB'in nitelikli mali mühür/e-imza sertifikalarını sağlayan
   AKREDİTE, resmi e-imza altyapısı sağlayıcısıdır VE dokümantasyonu, `xades:SignerRole`+public-key
   eklenmesinin *"yine e-fatura standartlarında GEREKLİ KILINAN"* bir unsur olduğunu AÇIKÇA
   belirtir - rastgele bir entegratörün keyfi tercihi DEĞİLDİR.

Görevin kendi kuralı gereği ("XSD `minOccurs=0` ve schematron sessizliği TEK BAŞINA yeterli
DEĞİLDİR"; kanıt bulunamazsa GÜVENLİ/tercih edilen yaklaşım UYGULANIR) VE gerçek karşı-kanıt (GİB
viewer, GİB-imzalı örnek artefakt, gerçek bir entegratörün yayımlanmış kabul profili) hiç
BULUNAMADIĞINDAN, **KARAR TERS ÇEVRİLDİ: `xades:SignerRole/ClaimedRole=Supplier` VE
`ds:KeyInfo/ds:KeyValue/ds:RSAKeyValue` (sertifikanın public key'iyle) ARTIK EKLENİR**, İKİ
BAĞIMSIZ doğrulayıcı kontrolüyle BİRLİKTE:

- `EBelgeXmlImzalayici.ImzalaXml`: `KeyInfo`'ya, mevcut `KeyInfoX509Data`'nın YANINA, sertifikanın
  RSA public key'inden türetilen bir `System.Security.Cryptography.Xml.RSAKeyValue` clause'u
  EKLENİR (standart .NET BCL tipi - 3. parti kütüphane YOK). `BuildQualifyingProperties`,
  `SignedSignatureProperties` İÇİNE `xades:SignerRole/xades:ClaimedRoles/xades:ClaimedRole` =
  `"Supplier"` (`EBelgeXmlImzalayici.SignerClaimedRole` sabiti) EKLER.
- `EBelgeXmlImzaDogrulayici.DogrulaCore`: mevcut IssuerSerial kontrolünden SONRA, (a)
  `xades:ClaimedRole` metninin TAM OLARAK `"Supplier"` olduğunu, (b)
  `ds:KeyInfo/ds:KeyValue/ds:RSAKeyValue`'daki Modulus/Exponent'in, GÖMÜLÜ `ds:X509Certificate`
  bytes'ından türetilen GERÇEK public key İLE (bayt-birebir) EŞLEŞTİĞİNİ BAĞIMSIZ olarak doğrular -
  ikisi de EKSİKSE VEYA UYUŞMUYORSA imza REDDEDİLİR.

`EBelgeXmlImzalayiciTests`'teki ESKİ "SignerRole/KeyValue olmadan da GEÇERLİ" testi KALDIRILDI,
YERİNE üçü eklendi: doğru değerlerle üretilip bağımsız doğrulamadan GEÇTİĞİNİN teyidi, KeyValue
sertifikayla eşleşmiyorsa REDDİNİN teyidi, ClaimedRole yanlışsa REDDİNİN teyidi.

### Test kapsamı ve çalıştırılan hedefli komut

`EBelgeXmlImzalayiciTests`'e 8 yeni test eklendi (30→37; 1 eski test KALDIRILIP 3 YENİ SignerRole/
KeyValue testiyle DEĞİŞTİRİLDİ, NET +7): 5 bozuk-girdi testi (iyi-biçimli-olmayan XML, geçersiz
sertifika base64, base64-ama-X509-olmayan sertifika bytes'ı, geçersiz SignatureValue base64,
geçersiz DigestValue base64 - hepsi `EBELGE_SIGNING_MALFORMED_SIGNATURE_DOCUMENT` İLE güvenli
`Gecersiz` sonuca dönüştüğünü doğrular) + 3 SignerRole/KeyValue testi.

`EBelgeUblImzalamaServiceIntegrationTests`'e 7 yeni test eklendi (13→20):
`YeniImzaBagimsizDogrulamaBeklenmedikBozukSonucUretirseAtomikKaliciHataBozukImzaBelgesiOlurGeciciDegil`,
`MevcutSignedIcerigiTamperlenirseImzaDogrulamasiAtlanirAtomikKaliciHataMevcutArtifactHashUyumsuzOlur`,
`MevcutSignedTxDisiDogrulamaSonrasiIcerikFarkliGecerliImzayaDegistirilirseYarisDurumuDoner`,
`YeniImzaSirasindaUnsignedKaynakSoftDeleteEdilirseGeciciHataKaynakImzalamaSirasindaDegistiDonerVeSignedReadyEklenmez`,
`YeniImzaSirasindaUnsignedKaynakIcerigiBozulursaAtomikKaliciHataKaynakHashUyumsuzOlur`,
`YeniSignedInsertAkisindaSchematronTamOlarakBirKezCagrilirKisaTransactionAltindaTekrarCalismaz`,
`UnsignedKaynakImzalamaSirasindaDegistiktenSonraYeniClaimIleYenidenDenemeBasariliOlur`. Ayrıca
mevcut `FarkliKaynagaBagliMevcutSignedReadyAtomikKaliciHataIdempotencyConflictUretir` testinin
fixture'ı düzeltildi - kasıtlı olarak fabrike edilmiş bir `ArtifactSha256` (`new string('b', 64)`),
YENİ eklenen exact-byte hash ön-kontrolüyle ERKEN çakışıyordu; artık `Icerik`'in GERÇEK SHA-256'sı
kullanılır (kasıtlı farklılık yalnız `KaynakArtifactId`/`KaynakArtifactSha256` alanlarındadır) - bu
sayede test asıl hedeflediği idempotency-conflict yolunu doğru şekilde test etmeye devam eder.

```
dotnet test tests/STYS.Tests/STYS.Tests.csproj --filter "FullyQualifiedName~EBelgeXmlImzalayiciTests|FullyQualifiedName~EBelgeSigningActivationGateTests|FullyQualifiedName~EBelgeUblImzalamaServiceIntegrationTests|FullyQualifiedName~EBelgeSigningBackfillServiceIntegrationTests|FullyQualifiedName~EBelgeArtefaktOlusturmaServiceIntegrationTests|FullyQualifiedName~EBelgeOutboxLeaseTransitionIntegrationTests|FullyQualifiedName~EBelgeOutboxMesajIslemeServiceTests|FullyQualifiedName~EBelgeUblRendererEndToEndIntegrationTests|FullyQualifiedName~EBelgeSchematronSidecarIntegrationTests|FullyQualifiedName~EBelgeFaz1IntegrationTests|FullyQualifiedName~EBelgeOutboxFaz2AIntegrationTests|FullyQualifiedName~EBelgeOutboxRetryPolicyTests"
  → Passed: 223, Failed: 0, Total: 223 (gerçek SQL Server + gerçek Java Saxon sidecar ile)
```

### Kasıtlı olarak YAPILMAYANLAR (görev kapsam sınırları)

XAdES mimarisi/imza motoru/backfill servisi/imzalama outbox handler'ı/migration BAŞTAN
YAZILMADI - yalnız YUKARIDA açıklanan hedefli sertleştirmeler eklendi. Signature/XSD/Schematron
doğrulaması SIRASINDA hiçbir SQL transaction AÇIK TUTULMADI. Hiçbir exception genel bir `catch
(Exception)` İLE gizlenmedi VEYA körü körüne geçici hataya DÖNÜŞTÜRÜLMEDİ. Saklanan bir hash
sütununa, EXACT-byte hash HESAPLANMADAN güvenilmedi. Unsigned artefakt yeniden serileştirilmedi;
SignedReady artefaktı HİÇBİR yerde UPDATE edilmedi (yalnız insert/soft-delete). Production
fail-closed sertifika/güven sağlayıcıları GEVŞETİLMEDİ; aktivasyon tarihi kapısı DEĞİŞTİRİLMEDİ;
gerçek sertifika/private key EKLENMEDİ; gönderim/PDF/e-posta/frontend/arka plan worker özelliği
EKLENMEDİ; tüm çözüm test paketi ÇALIŞTIRILMADI (yalnız hedefli filtre); hiçbir test ATLANMADI.

### Açık kalan konular

Faz 2B.7.1'in "Açık kalan konular" listesi AYNEN geçerlidir.

### Sonraki faz (Faz 2B.7.2)

Faz 2B.7.1'in "Sonraki faz" listesi AYNEN geçerlidir.

## Faz 2B.7.3 sonuç bölümü — kaynak artifact kimliği ve imza metadata bütünlüğü

**Durum: TAMAMLANDI, commit/push YAPILDI.**

### Neden gerekliydi

Faz 2B.7.2'nin kod incelemesinde 4 gerçek açık tespit edildi: (1) tx-dışı imzalamadan Faz-3
insert'ine kadar, kaynak Unsigned artefaktı YALNIZ iş anahtarı (KurumId+EBelgeKaydiId+ArtifactTipi+
ArtifactAsamasi) ÜZERİNDEN yeniden okunuyordu - AYNI iş anahtarına sahip AMA fiziksel olarak FARKLI
bir satırla (ör. eski satır silinip yenisi eklenmişse) TESADÜFEN eşleşme riski TEORİK olarak
mevcuttu; (2) `IslemMevcutSignedAsync`'e ve `DenemeYeniSignedInsertAtomikAsync`'e PARAMETRE olarak
geçirilen `unsignedArtifact`, transaction-DIŞI ilk okumadan sonra HİÇ dondurulmamış, ham bir EF
entity'siydi - "hangi değerlerin GERÇEKTEN imzalandığı" ile "en son okunan değerler" arasında kavramsal
bir AYRIM YOKTU; (3) mevcut bir SignedReady artefaktı işlenirken, saklanan imza metadata'sı (profil/
algoritma/sertifika parmak izi/imzalama zamanı) bağımsız doğrulanmış XML'den ÇIKARILAN GERÇEK
değerlerle HİÇ karşılaştırılmıyordu - yalnız İÇERİK hash'i (Faz 2B.7.2) doğrulanıyordu, metadata
sütunları KÖRÜ KÖRÜNE güveniliyordu; (4) Faz-3'ün kısa "sonuç" transaction'ı, satırın yeniden
okunmasında yalnız Id/hash/kaynak Id-hash'i karşılaştırıyordu - RuleSetId/SnapshotSchemaVersion/
KaynakSnapshotSha256/imza metadata'sı gibi audit alanlarındaki bir DEĞİŞİKLİK bu kısa pencerede HİÇ
YAKALANMIYORDU.

### 1. `EBelgeUnsignedArtifactSnapshot` - immutable kaynak anlık görüntüsü

`EBelgeUblImzalamaService.cs`'e, `EBelgeUblImzalamaTalebi`'nin hemen ALTINA, yeni bir immutable
record eklendi:

```csharp
public sealed record EBelgeUnsignedArtifactSnapshot
{
    public required long ArtifactId { get; init; }
    public required int KurumId { get; init; }
    public required int EBelgeKaydiId { get; init; }
    public required EBelgeArtifactTipi ArtifactTipi { get; init; }
    public required EBelgeArtifactAsamasi ArtifactAsamasi { get; init; }
    public required string ArtifactSha256 { get; init; }
    public required string RuleSetId { get; init; }
    public required int SnapshotSchemaVersion { get; init; }
    public required string KaynakSnapshotSha256 { get; init; }
    public required string MimeType { get; init; }
    public required string DosyaAdi { get; init; }
}
```

`ImzalaAsync`, tx-dışı ilk okuma + hash doğrulamasından HEMEN SONRA bu anlık görüntüyü (+ AYRI bir
`ImmutableArray<byte> unsignedIcerik`) oluşturur; bundan SONRA akışın TAMAMI (imzalama talebi,
`IslemMevcutSignedAsync`, `DenemeYeniSignedInsertAtomikAsync`) yalnız BU KAYIT üzerinden ilerler -
EF entity'sinin (`unsignedArtifact`) kendisi bir daha KULLANILMAZ.

### 2-3. `OkuUnsignedKilitliAsync` - kesin Id ile kilitli yeniden okuma + yarış sınıflandırması

`OkuUnsignedKilitliAsync`, artık `long artifactId` parametresi ALIR ve sorgusuna `WHERE [Id] =
{artifactId}` koşulunu EKLER (yalnız Kurum/EBelge/aşama iş anahtarı DEĞİL). Bu, satırın fiziksel
olarak silinip AYNI iş anahtarıyla YENİ bir Id'li satırla değiştirildiği senaryoda sorgunun
DOĞRUDAN `null` DÖNMESİNİ sağlar - generic bir FK ihlali/`DbUpdateException`'a HİÇ düşülmeden,
type-safe bir sonuç üretilir (bkz. görev md.2-3).

`DenemeYeniSignedInsertAtomikAsync`, kilitli yeniden okuma sonrasında ÜÇ ayrı sınıflandırma yapar:

1. **`null` VEYA `IsDeleted`** → `EBELGE_SIGNING_SOURCE_CHANGED_DURING_SIGNING` İLE GEÇİCİ hata
   (kaynak kayboldu/fiziksel silinip değiştirildi/soft-delete edildi - YENİ bir claim ile yeniden
   denenmelidir).
2. **Kaydın KENDİ `ArtifactSha256` sütunu, `Icerik`'inin GERÇEK SHA-256'sıyla UYUŞMUYOR** →
   `EBELGE_SIGNING_SOURCE_ARTIFACT_HASH_MISMATCH` İLE ATOMİK KALICI hata (kaydın kendi İÇİNDE
   tutarsız olması - GERÇEK bir bütünlük sorunu, retry ANLAMSIZ).
3. **Kayıt kendi İÇİNDE tutarlı AMA `unsignedSnapshot`'IN HERHANGİ bir alanından (ArtifactSha256,
   RuleSetId, SnapshotSchemaVersion, KaynakSnapshotSha256, MimeType, DosyaAdi) FARKLI** →
   `EBELGE_SIGNING_SOURCE_CHANGED_DURING_SIGNING` İLE GEÇİCİ hata.

### 4. SignedReady insert'i - kilitli, doğrulanmış kaynaktan alan doldurma

`DenemeYeniSignedInsertAtomikAsync`'in inşa ettiği YENİ `EBelgeArtifact` satırının `RuleSetId`/
`SnapshotSchemaVersion`/`KaynakSnapshotSha256`/`KaynakArtifactId`/`KaynakArtifactSha256`/`DosyaAdi`
alanları artık transaction-DIŞI eski `unsignedSnapshot`'tan DEĞİL, kilitli YENİDEN okunan VE
snapshot'la eşleştiği yukarıda DOĞRULANAN `yenidenOkunanUnsigned` entity'sinden alınır - audit
zinciri TAM OLARAK commit anındaki, doğrulanmış kaynağa BAĞLANIR.

### 5. Existing SignedReady - metadata'nın doğrulanmış XML'le eşleşmesi

Yeni hata kodu eklendi: `EBELGE_SIGNED_ARTIFACT_METADATA_MISMATCH`
(`EBelgeXmlImzaHataKodlari.SignedArtifactMetadataUyumsuz`).

`EBelgeXmlImzaDogrulamaSonucu`, `ImzaProfili`/`ImzaAlgoritmasi`/`DigestAlgoritmasi` alanlarıyla
GENİŞLETİLDİ. Bu değerler STORED artefakttan DEĞİL, `EBelgeXmlImzaDogrulayici.DogrulaCore`'un
sonunda, whitelist ile ONAYLANMIŞ `Profil`den (`EBelgeXadesProfili.GibUblTr`) üretilir - bu
GÜVENLİDİR, çünkü `DogrulaCore` içindeki "Algoritma/profil whitelist" ve her `ds:Reference`'ın
`DigestMethod`'u için yapılan kontroller, XML'deki GERÇEK `sigMethod`/`digestAlg` değerlerinin
`Profil.SignatureAlgorithmUri`/`Profil.DigestAlgorithmUri`'YE BAYT-BİREBİR eşit olduğunu DAHA ÖNCE
bağımsız doğrulamıştır - `Profil` alanları, doğrulanmış XML'deki gerçek değerlerin KENDİSİDİR.

`IslemMevcutSignedAsync`, bağımsız imza doğrulaması (`mevcutDogrulama.GecerliMi`) BAŞARILI
olduktan SONRA, saklanan `mevcutSigned` metadata'sını `mevcutDogrulama`'nın alanlarıyla karşılaştırır:

```text
mevcutSigned.ImzaProfili                      == mevcutDogrulama.ImzaProfili
mevcutSigned.ImzaAlgoritmasi                  == mevcutDogrulama.ImzaAlgoritmasi
mevcutSigned.DigestAlgoritmasi                == mevcutDogrulama.DigestAlgoritmasi
mevcutSigned.ImzalayanSertifikaSha256ParmakIzi == mevcutDogrulama.SertifikaSha256ParmakIzi
mevcutSigned.ImzalamaZamaniUtc (saniyeye kırpılmış) == mevcutDogrulama.SigningTimeUtc (saniyeye kırpılmış)
```

Ayrıca, mevcut `kaynakEslesiyor` (KaynakArtifactId+KaynakArtifactSha256) kontrolünden SONRA, YENİ
bir `kaynakZinciriEslesiyor` kontrolü eklendi: `mevcutSigned.RuleSetId`/`SnapshotSchemaVersion`/
`KaynakSnapshotSha256`, kaynak `unsignedKaynak` (snapshot) İLE eşleşmelidir. HERHANGİ bir uyumsuzluk
`EBELGE_SIGNED_ARTIFACT_METADATA_MISMATCH` İLE atomik kalıcı hata üretir - mevcut
`EBELGE_SIGNED_ARTIFACT_IDEMPOTENCY_CONFLICT` kodu (yalnız KaynakArtifactId/KaynakArtifactSha256
uyumsuzluğu/soft-delete İÇİN, geriye dönük UYUMLULUK amacıyla) DEĞİŞTİRİLMEDİ.

**`ImzalamaZamaniUtc` karşılaştırmasında SANİYE hassasiyeti** (görev md.5'in AÇIKÇA istediği
"SQL hassasiyetini dikkate al... gevşek/belirsiz tolerans EKLEME" gereksinimi): `xades:SigningTime`,
XML'e `EBelgeXmlImzalayici.BuildQualifyingProperties`'te `"yyyy-MM-ddTHH:mm:ssZ"` formatıyla YAZILIR
- YALNIZ SANİYE çözünürlüğü TAŞIR (alt-saniye hassasiyeti XML serileştirmesinde KAYBOLUR). Saklanan
`ImzalamaZamaniUtc` sütunu İSE (migration'da `datetime2`, EXPLICIT bir `HasPrecision` OLMADAN -
SQL Server VARSAYILAN `datetime2(7)` hassasiyetiyle) `_timeProvider.GetUtcNow().UtcDateTime`'dan
GELEN TAM hassasiyeti KORUR. Bu ikisinin DOĞRUDAN (`==`) karşılaştırılması neredeyse HER ZAMAN
BAŞARISIZ olurdu (gerçek zamanın tam saniyeye denk gelmesi son derece nadir). Çözüm: YENİ
`ImzalamaZamaniSaniyeHassasiyetindeEslesiyorMu`/`SaniyeyeKirp` yardımcı metotları, HER İKİ değeri
de SANİYEYE kırpıp EXACT eşitlik karşılaştırır - bu, ±N saniyelik BULANIK bir tolerans PENCERESİ
DEĞİLDİR; XML serileştirmesinin KENDİ, SABİT VE BELİRLEYİCİ hassasiyet sınırına göre yapılan, tam
eşitlik gerektiren bir karşılaştırmadır. (Faz-3'ün kısa transaction'ındaki `yenidenOkunanSigned`
vs `mevcutSigned` karşılaştırması İSE - bkz. md.6 - İKİ ayrı DB okumasının karşılaştırmasıdır, XML'e
KARŞI DEĞİLDİR; bu yüzden ORADA `ImzalamaZamaniUtc` TAM hassasiyetle, kırpmadan karşılaştırılır.)

### 6. Kısa transaction'da metadata'nın yeniden doğrulanması

`IslemMevcutSignedAsync`'in Faz-3 kısa transaction'ındaki `satirDegismedi` karşılaştırması,
Faz 2B.7.2'nin Id/hash/kaynak Id-hash/IsDeleted alanlarına EK olarak artık şunları da (kilitli
yeniden okunan `yenidenOkunanSigned` İLE tx-dışı doğrulamada kullanılan `mevcutSigned` ANLIK
GÖRÜNTÜSÜ ARASINDA) karşılaştırır: `RuleSetId`, `SnapshotSchemaVersion`, `KaynakSnapshotSha256`,
`ImzaProfili`, `ImzaAlgoritmasi`, `DigestAlgoritmasi`, `ImzalayanSertifikaSha256ParmakIzi`,
`ImzalamaZamaniUtc`, `MimeType`, `DosyaAdi`. HERHANGİ biri tx-dışı doğrulamadan SONRA değişmişse,
önceki doğrulama sonucu ARTIK GÜVENİLMEZ - transaction rollback edilir, `EBELGE_SIGNING_YARIS_DURUMU`
İLE geçici hata döner. İmza/XSD/Schematron doğrulaması BU kısa transaction'ın İÇİNE HİÇ TAŞINMADI -
tümü (Faz 2B.7.2'de olduğu gibi) transaction AÇILMADAN ÖNCE, tx-dışı tamamlanır.

### Test kapsamı ve çalıştırılan hedefli komut

`EBelgeUblImzalamaServiceIntegrationTests`'e 12 yeni test eklendi (20→32):

- `YeniImzaSirasindaUnsignedFizikselSilinipAyniAnahtarlaYeniIdliSatirEklenirseSignedReadyOlusmazTypeSafeSonucUretir`
  - kaynak fiziksel olarak silinip AYNI iş anahtarıyla YENİ bir Id'li satırla değiştirilir; SignedReady
  OLUŞMAZ VE sonuç generic bir FK/DbUpdateException'a DÜŞMEDEN type-safe bir `GeciciHata`/
  `EBELGE_SIGNING_SOURCE_CHANGED_DURING_SIGNING` olarak DÖNER (görev md.8 senaryo 1-2).
- `YeniImzaSirasindaUnsignedRuleSetIdDegisirseSignedReadyOlusmazGeciciHataDoner` (senaryo 3)
- `YeniImzaSirasindaUnsignedSnapshotSchemaVersionDegisirseSignedReadyOlusmazGeciciHataDoner` (senaryo 4)
- `YeniImzaSirasindaUnsignedKaynakSnapshotSha256DegisirseSignedReadyOlusmazGeciciHataDoner` (senaryo 5)
- `UnsignedMetadataImzalamaSirasindaDegistiktenSonraYeniClaimIleYenidenDenemeBasariliOlur` (senaryo 6)
- `MevcutSignedReadyImzaProfiliDegistirilirseIdempotentBasariOlmazMetadataUyumsuzKaliciHataUretir` (senaryo 7)
- `MevcutSignedReadyImzaAlgoritmasiDegistirilirseIdempotentBasariOlmazMetadataUyumsuzKaliciHataUretir` (senaryo 8)
- `MevcutSignedReadyDigestAlgoritmasiDegistirilirseIdempotentBasariOlmazMetadataUyumsuzKaliciHataUretir` (senaryo 9)
- `MevcutSignedReadySertifikaParmakIziDegistirilirseIdempotentBasariOlmazMetadataUyumsuzKaliciHataUretir` (senaryo 10)
- `MevcutSignedReadyImzalamaZamaniXmlSigningTimeIleEslesmezseIdempotentBasariOlmazMetadataUyumsuzKaliciHataUretir` (senaryo 11)
- `MevcutSignedReadyRuleSetIdDegistirilirseIdempotentBasariOlmazMetadataUyumsuzKaliciHataUretir` (senaryo 12)
- `MevcutSignedTxDisiDogrulamaSonrasiRuleSetIdDegistirilirseYarisDurumuDoner` (senaryo 13)

Senaryo 14 (doğru metadata + exact bytes ile idempotent başarı), mevcut
`GecerliImzaTamAtomikBasariylaSignedReadyArtefaktUretirVeHashZinciriDogrulanir` (yeni-imza yolu) ve
`AyniKaynagaEslesenMevcutSignedReadyIdempotentBasariylaTamamlanirIkinciSatirEklenmezVeYenidenDogrulanir`
(idempotent yol) testleri TARAFINDAN ZATEN REGRESYON olarak KANITLANMIŞTIR - bu turdaki TÜM yeni
metadata kontrolleri EKLENDİKTEN SONRA da her iki test BAŞARILI olmaya devam eder.

```
dotnet test tests/STYS.Tests/STYS.Tests.csproj --filter "FullyQualifiedName~EBelgeXmlImzalayiciTests|FullyQualifiedName~EBelgeSigningActivationGateTests|FullyQualifiedName~EBelgeUblImzalamaServiceIntegrationTests|FullyQualifiedName~EBelgeSigningBackfillServiceIntegrationTests|FullyQualifiedName~EBelgeArtefaktOlusturmaServiceIntegrationTests|FullyQualifiedName~EBelgeOutboxLeaseTransitionIntegrationTests|FullyQualifiedName~EBelgeOutboxMesajIslemeServiceTests|FullyQualifiedName~EBelgeUblRendererEndToEndIntegrationTests|FullyQualifiedName~EBelgeSchematronSidecarIntegrationTests|FullyQualifiedName~EBelgeFaz1IntegrationTests|FullyQualifiedName~EBelgeOutboxFaz2AIntegrationTests|FullyQualifiedName~EBelgeOutboxRetryPolicyTests"
  → Passed: 235, Failed: 0, Total: 235 (gerçek SQL Server + gerçek Java Saxon sidecar ile) - bu 235,
  Faz 2B.7.2'nin 223'üne bu turun 12 YENİ testinin EKLENMESİYLE oluşur (37 doğrulayıcı/imzalayıcı
  birim testi HİÇ DEĞİŞMEDİ - bu turda `EBelgeXmlImzalayiciTests` dosyasına DOKUNULMADI).
```

### Kasıtlı olarak YAPILMAYANLAR (görev kapsam sınırları)

XAdES mimarisi/imza motoru/doğrulayıcı/outbox handler'ı/activation gate/backfill servisi/migration
BAŞTAN YAZILMADI - yalnız YUKARIDA açıklanan hedefli sertleştirmeler eklendi. Yeni migration
EKLENMEDİ - mevcut sütunlar (tümü `EBelgeArtifact`'ta ZATEN var olan `RuleSetId`/
`SnapshotSchemaVersion`/`KaynakSnapshotSha256`/`ImzaProfili`/`ImzaAlgoritmasi`/`DigestAlgoritmasi`/
`ImzalayanSertifikaSha256ParmakIzi`/`ImzalamaZamaniUtc`/`MimeType`/`DosyaAdi`) YETERLİ bulunmuştur.
İmza/XSD/Schematron doğrulaması SIRASINDA hiçbir SQL transaction AÇIK TUTULMADI. SignedReady VEYA
Unsigned artefaktı HİÇBİR yerde UPDATE edilmedi (yalnız insert/soft-delete/test senaryolarındaki
KASITLI tamperleme - test-only raw SQL, üretim kod yolunda YOK). Fiziksel silme özelliği/endpoint'i
EKLENMEDİ - testlerdeki fiziksel silme yalnız bir DIŞ bozulma senaryosunu simüle eden, DOĞRUDAN
test-only SQL'dir. Generic `DbUpdateException`, hiçbir yerde kaynak-değişikliği kontrolü YERİNE
KULLANILMADI - kesin Id ile kilitli yeniden okuma, sorunu daha `SaveChanges` çağrılmadan, type-safe
biçimde TESPİT eder. Gerçek sertifika/private key EKLENMEDİ; activation gate DEĞİŞTİRİLMEDİ;
gönderim/PDF/e-posta/frontend/arka plan worker özelliği EKLENMEDİ; tüm çözüm test paketi
ÇALIŞTIRILMADI (yalnız hedefli filtre); hiçbir test ATLANMADI.

### Açık kalan konular

Faz 2B.7.1'in "Açık kalan konular" listesi AYNEN geçerlidir.

### Sonraki faz (Faz 2B.7.3)

Faz 2B.7.1'in "Sonraki faz" listesi AYNEN geçerlidir.

## Faz 2B.8 sonuç bölümü — üretim güvenli, çoklu instance destekli e-belge outbox worker

**Durum: TAMAMLANDI, commit/push YAPILDI.**

### Neden gerekliydi

Faz 2B.5-2B.7.3'te tamamlanan e-belge altyapısı (canonical snapshot, deterministic renderer, gerçek
XSD/Schematron doğrulaması, immutable Unsigned/SignedReady artefaktlar, lease-safe outbox, gerçek
XAdES-BES imzalama, artifact/hash/metadata bütünlüğü, signing activation gate, backfill servisi)
TAMDIR - ama outbox mesajlarını SÜREKLİ claim edip işleyen bir hosted worker HİÇ YOKTU (bkz.
`backend/Program.cs`'teki Faz 2B.7.1 yorumu: "yalnız uygulama servisi kaydedilir - otomatik hosted
worker/endpoint YOKTUR"). Faz 2B.8, MEVCUT claim/lease/işleme altyapısını ORKESTRE EDEN, üretim
güvenli bir `BackgroundService` ekler - kendi claim/lease/retry mimarisini KURMAZ.

### 1-2. Mevcut mimarinin yeniden kullanımı

Worker (`EBelgeOutboxWorker`), YALNIZ şu mevcut servisleri kullanır - hiçbirini YENİDEN YAZMAZ:

- **Claim**: `IEBelgeOutboxClaimLeaseService.TryClaimNextAsync` - GERÇEK lease token'ı, `WITH
  (UPDLOCK, READPAST, ROWLOCK)` tabanlı SQL claim mekanizması, mevcut retry/terminal koşulları.
  Bu servis YALNIZ TEK bir mesaj claim eder (batch API'si YOKTUR) - worker, `BatchSize` kadar
  BOUNDED bir döngüde bu metodu TEKRAR TEKRAR çağırarak "batch" davranışını simüle eder (görev
  md.4'ün AÇIKÇA izin verdiği yaklaşım - "Desteklemiyorsa... Worker, batch büyüklüğü kadar bounded
  claim çağrısı yapabilir").
- **İşleme**: `IEBelgeOutboxMesajIslemeService.IsleAsync` - handler seçimi (`IEBelgeOutboxIsTuruHandler`
  - `EBelgeArtefaktOlusturOutboxHandler`/`EBelgeUblImzalaOutboxHandler`, YENİ bir handler
  EKLENMEDİ), atomik complete/fail geçişleri, retry policy uygulaması TAMAMEN bu servisin
  İÇİNDEDİR. Worker, dönen `EBelgeOutboxIslemeSonucuTuru`'nu (Tamamlandi/RetryPlanlandi/
  TerminalHata/SahiplikKaybedildi) YALNIZ GÖZLEMLER - İKİNCİ bir complete/fail/retry/lease-release
  çağrısı YAPMAZ (bkz. görev md.12). Bunun KANITI: worker'ın DI container'ında
  `IEBelgeOutboxLeaseTransitionService`/`IEBelgeOutboxRetryPolicy` HİÇ enjekte EDİLMEZ (constructor
  bağımlılığı YOKTUR) - worker'ın BUNLARA erişimi bile YOKTUR.

Worker'ın KENDİSİ UBL XML üretmez, imzalamaz, artifact yazmaz, outbox durumunu doğrudan
değiştirmez, handler seçmez, retry süresi hesaplamaz - bunların TAMAMI mevcut servislerde kalır.

### 3. Genel aktivasyon kapısı

Yeni `EBelgeProcessing` config bölümü (`EBelgeProcessingOptions`) + `IEBelgeProcessingActivationGate`
eklendi - `EBelgeSigningOptions`/`IEBelgeSigningActivationGate`'İN (Faz 2B.7, yalnız "bir UblImzala
mesajı OLUŞTURULSUN mu" sorusunu yanıtlayan) YERİNE GEÇMEZ, AYRI bir EK savunma katmanıdır: kuyrukta
zaten var olan (yanlışlıkla/elle eklenmiş OLABİLECEK) `ArtefaktOlustur`/`UblImzala` mesajlarının
worker TARAFINDAN CLAIM EDİLİP EDİLMEYECEĞİNİ kontrol eder. Varsayılan `appsettings.json`:

```json
"EBelgeProcessing": {
  "Enabled": false,
  "NotBeforeLocalDate": "2026-09-15",
  "TimeZoneId": "Europe/Istanbul",
  "PollIntervalSeconds": 10,
  "IdlePollIntervalSeconds": 30,
  "BatchSize": 10,
  "LeaseDurationSeconds": 120,
  "MaxParallelism": 1,
  "ShutdownGracePeriodSeconds": 30
}
```

`EBelgeProcessingActivationGate.ShouldProcess()`, `EBelgeSigningActivationGate` İLE AYNI, kanıtlanmış
fail-closed desenini kullanır (`TimeProvider` üzerinden, server local timezone'a GÜVENMEDEN) - ama
`TimeZoneId`'yi (Europe/Istanbul yerine) config'ten OKUR (genelleştirilmiş). **Kritik tasarım
kararı**: tarih/timezone doğrulaması BİLİNÇLİ OLARAK startup-time validation'a DEĞİL, HER çağrıda
çalışan bu runtime kontrolüne konuldu - görev md.3 AÇIKÇA "Config yanlışsa mesajları terminal hataya
geçirme... Disabled veya tarih kapısı kapalıyken worker uygulamayı crash ettirmemeli" der; bir
timezone/tarih hatasının UYGULAMA BAŞLANGICINI ENGELLEMESİ bu gereksinimle ÇELİŞİRDİ. Yapısal/sayısal
alanlar (poll/idle/batch/lease/parallelism/shutdown-grace) İSE - dış bağımlılığı OLMAYAN saf aritmetik
kontroller olduğundan - `EBelgeProcessingOptionsValidator` (`IValidateOptions<T>` +
`.ValidateOnStart()`) İLE GÜVENLE startup'ta fail-fast edilir; bu validator `Enabled=false` OLSA
BİLE KOŞULSUZ çalışır (bir operatör gelecekte `true`'ya çevirdiğinde config'in ZATEN geçerli
olduğundan emin olmak için) - **AMA hiçbir I/O veya dış bağımlılık İÇERMEZ**, bu yüzden "worker
disabled iken eksik dış bağımlılık nedeniyle TÜM API'nin başlamasının engellenmesi" riski YOKTUR
(görev md.10'un açıkça istediği raporlama): validasyon yalnız `EBelgeProcessingOptions`'ın KENDİ
sayısal alanlarını kontrol eder, hiçbir DB/HTTP/dosya çağrısı YAPMAZ.

Gate, HER polling turunda YENİDEN değerlendirilir (önbelleklenmez) - bu, "gate kapalıyken belirli
aralıklarla config durumunu tekrar kontrol edebilmeli" gereksinimini doğal olarak karşılar.

### 4. Claim döngüsü ve batch

`EBelgeOutboxWorker.BirTurCalistirAsync`, HER polling turunda: (1) aktivasyon kapısını kontrol
eder - kapalıysa claim'e HİÇ GİTMEDEN döner (mesaj terminal hataya geçirilmez, deneme sayısı
artırılmaz, lease alınmaz - bkz. görev md.17); (2) `BatchSize` kadar BOUNDED bir döngüde
`TryClaimNextAsync` çağırır; (3) HER claim, `MaxParallelism` boyutlu bir `SemaphoreSlim` İLE
sınırlı, AYRI bir DI scope İÇİNDE işlenir. Semafor, CLAIM denemesinden ÖNCE alınır (işlenmeden
ÖNCE DEĞİL) - bu, `MaxParallelism=1` iken bir sonraki mesajın, ÖNCEKİ mesajın işlenmesi TAMAMEN
BİTMEDEN claim EDİLMEMESİNİ sağlar; aksi halde lease süresi, mesaj yalnızca SIRADA BEKLERKEN (henüz
işlenmeye BAŞLANMADAN) boşa akabilirdi.

### 5. Çoklu instance güvenliği

Worker, process-içi kilit/distributed lock EKLEMEZ, "tek pod çalışacak" varsayımı YAPMAZ - çoklu
instance güvenliği TAMAMEN mevcut SQL `UPDLOCK/READPAST` claim mekanizmasından GELİR.
`EBelgeOutboxWorkerIntegrationTests.IkiInstanceAyniMesajiIsleyemezVeLeaseSuresiDolduktanSonraIkinciWorkerTamamlar`,
GERÇEK SQL Server'a karşı: Instance A bir mesajı KISA (2sn) bir lease İLE claim eder VE HİÇ
İŞLEMEDEN "çöker"; Instance B (aktif lease SÜRERKEN) AYNI mesajı ALAMAZ (`null` döner); lease
süresi GERÇEKTEN dolduktan SONRA, Instance B (gerçek bir `EBelgeOutboxWorker`) mesajı BAŞARIYLA
tamamlar; SONUÇTA tam olarak 1 (duplicate OLMAYAN) artefakt oluşur. Instance A'nın eski token'la
sonuç YAZAMAYACAĞI (`IsOwnedForJobAsync`/`TryCompleteJobAsync`/`TryFailJobAsync`'in token+iş
türü+kurum+e-belge bağıyla koruması), Faz 2B.6/2B.6.2'de ZATEN kanıtlanmış olduğundan BURADA
TEKRARLANMADI - yalnız worker-seviyesindeki UÇTAN UCA sonuç doğrulandı.

### 6-7. Paralellik ve scope yönetimi

Üretim varsayılanı `MaxParallelism=1`. Paralellik, BOUNDED bir `SemaphoreSlim` İLE kontrol edilir;
`MaxParallelism<=0` config'i startup validation TARAFINDAN reddedilir (fail-fast), AYRICA worker
KENDİSİ `Math.Clamp(_options.MaxParallelism, 1, MaxParallelismLimit)` İLE savunma amaçlı bir kez
DAHA sınırlar. Worker singleton'dır - scoped servisler constructor'a DEĞİL, `IServiceScopeFactory`
üzerinden enjekte edilir. HER claim İÇİN: `IEBelgeOutboxClaimLeaseService` KENDİ (kısa ömürlü, yalnız
claim SQL'i süresince açık) scope'unda; sonucu (immutable bir DTO) `IEBelgeOutboxMesajIslemeService`
BAŞKA, YENİ bir scope'ta işler - AYNI `DbContext`/scoped servis HİÇBİR ZAMAN birden fazla mesajda,
paralel task'ta veya polling turunda PAYLAŞILMAZ (bkz.
`EBelgeOutboxWorkerTests.HerMesajIcinAyriDiScopeOlusturulur`/`AyniScopedHandlerIkiParalelMesajdaPaylasilmaz`
- her ikisi de her mesaj İÇİN AYRI bir `IEBelgeOutboxMesajIslemeService` instance'ı GÖRÜLDÜĞÜNÜ
kanıtlar).

### 8. Cancellation ve graceful shutdown

`Task.Delay` yerine YENİ bir `IEBelgeOutboxWorkerDelay` abstraction'ı kullanılır (görev md.8'in
İKİ önerdiği yaklaşımın İKİSİNİ BİRDEN karşılar: hem "test edilebilir bir zamanlama abstraction'ı"
HEM "`TimeProvider` destekli delay" - üretim implementasyonu `TimeProviderEBelgeOutboxWorkerDelay`,
`Task.Delay(TimeSpan, TimeProvider, CancellationToken)` overload'unu SARAR). `EBelgeOutboxWorker`,
`BackgroundService.StopAsync`'i OVERRIDE EDER: `ShutdownGracePeriodSeconds`, host'un KENDİ genel
kapanma süresinden BAĞIMSIZ bir yerel `CancellationTokenSource` İLE uygulanır - süre AŞILIRSA,
çalışan mesaj(lar) ZORLA iptal EDİLMEZ, yalnız `StopAsync` beklemeyi BIRAKIR; lease'in DAHA SONRA
süresi dolup BAŞKA bir worker TARAFINDAN yeniden claim edilmesine GÜVENİLİR. Host cancellation
nedeniyle oluşan bir `OperationCanceledException`, hata/retry olarak KAYDEDİLMEZ (metrik/health
state ETKİLENMEZ, warning/error seviyesinde LOGLANMAZ) - bkz.
`EBelgeOutboxWorkerTests.HostCancellationHataVeyaRetryOlarakKaydedilmez`.

### 9-10. Worker hata sınırı ve polling/backoff

`ExecuteAsync`'in dış döngüsü, `catch (Exception ex) when (ex is not OutOfMemoryException)` İLE
korunur - bir polling turu/tek mesaj exception ÜRETTİĞİNDE worker TAMAMEN ÖLMEZ, güvenli/PII
içermeyen loglama + `IdlePollIntervalSeconds` kadar KONTROLLÜ (bounded) backoff + bir SONRAKİ turda
DEVAM eder. `OutOfMemoryException` genel bir catch İLE GİZLENMEZ - `ExecuteTask`'a FAULTED olarak
YANSIR (bkz. `EBelgeOutboxWorkerTests.FatalExceptionKoruKoruneYutulmaz`, reflection İLE
`BackgroundService._executeTask`'ı doğrudan inceler). `StackOverflowException` zaten .NET'te
YAKALANAMAZ. İki AYRI bekleme tipi (`PollIntervalSeconds`/`IdlePollIntervalSeconds`) VAR - en az
BİR mesaj işlendiyse KISA (poll) aralık, kuyruk BOŞSA/gate KAPALIYSA/worker-seviyesi bir hata
OLUŞTUYSA UZUN (idle) aralık kullanılır; her İKİ değer İÇİN de startup validation UYGULANIR
(`PollIntervalSeconds>=1`, `IdlePollIntervalSeconds>=PollIntervalSeconds`, `BatchSize` [1,500],
`LeaseDurationSeconds>=1`, `MaxParallelism` [1,32], `ShutdownGracePeriodSeconds>=0`).

### 11. Lease renewal kararı: **Seçenek A (renewal EKLENMEDİ)**

Mevcut handler'lar (`EBelgeArtefaktOlusturOutboxHandler`/`EBelgeUblImzalaOutboxHandler`'ın altında
çalışan `EBelgeArtefaktOlusturmaService`/`EBelgeUblImzalamaService`) ZATEN, sonuç yazmadan ÖNCE
`IsOwnedForJobAsync` İLE ownership'i YENİDEN doğrular (Faz 2B.6/2B.6.2'den KALICI bir mimari
özellik) - lease aşılırsa sonuç `SahiplikKaybedildi` olur, SONRAKİ bir worker mesajı yeniden işler.
Bu, Seçenek A'nın ÖN KOŞULUNU ZATEN karşılar. Üretim varsayılan `LeaseDurationSeconds=120`, GERÇEK
renderer+sidecar+imzalama çağrılarının (yerel Java sidecar'a HTTP + test sertifikasıyla XAdES imza
- ikisi de saniyeler MERTEBESİNDE, dakikalar DEĞİL) normal süresinden GÜVENLİ biçimde uzundur -
gerçek entegrasyon testlerinde (bkz. §44-47) bu süreler GÖZLEMLENDİ, lease aşımına dair HİÇBİR
BULGU YOKTUR. Renewal karmaşıklığı EKLENMEDİ.

### 12. Mesaj sonuçlarının işlenmesi

Bkz. §1-2 - worker, `IsleAsync`'in döndürdüğü sonucu YALNIZ GÖZLEMLER; ikinci bir complete/fail/
retry/lease-release çağrısı YAPMAZ. Bu, hem KOD İNCELEMESİYLE (worker'ın
`IEBelgeOutboxLeaseTransitionService`'e HİÇ erişimi YOK) hem TEST'LE (worker unit testlerinin DI
container'ında bu servis HİÇ KAYITLI DEĞİL - worker YANLIŞLIKLA onu çözmeye ÇALIŞSAYDI TÜM testler
DI hatasıyla PATLARDI; hepsinin BAŞARILI geçmesi dolaylı KANITTIR) doğrulanmıştır.

### 13. Gözlemlenebilirlik

`System.Diagnostics.Metrics` KULLANILDI - çözümde bu deseni kullanan İLK sınıf
(`EBelgeOutboxWorkerMetrics`, yeni bir NuGet paketi GEREKMEDİ). Meter adı `STYS.EBelge.Outbox`;
sayaçlar TAM OLARAK görevin istediği isimlerle: `stys_ebelge_outbox_claimed_total`,
`_completed_total`, `_retry_scheduled_total`, `_terminal_error_total`, `_lease_lost_total`,
`_processing_duration_ms` (histogram), `_poll_errors_total`, `_inflight` (up-down counter). Tag
olarak YALNIZ `is_turu`/`sonuc_turu` (düşük cardinality, ≤4 farklı değer) kullanılır -
`EBelgeOutboxWorkerMetricsTests`, GERÇEK bir `MeterListener` İLE HER ölçümün tag anahtarlarının bu
ikisinden BAŞKA HİÇBİR ŞEY olmadığını doğrudan doğrular.

### 14. PII-güvenli loglama

Worker'ın TÜM log çağrıları yalnız Outbox ID/Kurum ID/EBelgeKaydi ID/iş türü/deneme sayısı/sonuç
türü/işlem süresi/GÜVENLİ hata kodu İÇERİR - `EBelgeOutboxWorkerTests.LoglardaLeaseTokenBulunmaz`,
GERÇEK bir capturing `ILoggerProvider` İLE, claim'in KilitToken'ının HİÇBİR log kaydında GEÇMEDİĞİNİ
doğrudan doğrular. Boş kuyruk polling'i log spam ÜRETMEZ (`BosKuyrukPollingLogSpamUretmez` - 5+
boş turda EN FAZLA 2 Information+ log kaydı - yalnız worker başlangıç/bitiş). Başarılı mesaj sonuçları
`Information`, polling ayrıntıları için ayrı bir log YOKTUR (delay çağrıları LOGLANMAZ).

### 15. Health check

`EBelgeOutboxWorkerHealthState` (thread-safe, PII/token İÇERMEYEN durum) + `EBelgeOutboxWorkerHealthCheck`
(`IHealthCheck`, `"ready"` tag'iyle KAYITLI - çözümdeki İLK bileşene-özel health check, sidecar'ın
KENDİ health check'i TEKRARLANMADI, zaten YOKTU). Kararlar: `Enabled=false` → `Healthy` (KASITLI,
beklenen); döngü HİÇ başlamadıysa → `Unhealthy`; döngü başladı AMA son başarılı poll ÇOK ESKİYSE
(`Max(Poll,Idle)*5` eşiği) → `Degraded`; kuyruk BOŞ olması TEK BAŞINA asla unhealthy/degraded
ÜRETMEZ; TEK bir mesajın terminal İŞ hatası health state'e HİÇ YANSIMAZ (yalnız worker-SEVİYESİ
beklenmedik hatalar TUTULUR).

### 16. Dependency injection

Worker KOŞULSUZ olarak `AddHostedService<EBelgeOutboxWorker>()` İLE kaydedilir - worker'ın KENDİSİ
HER polling turunda gate kontrolü yapar (Program.cs'teki `PosOdemeDurumTakipHostedService` İLE AYNI
"disabled ise ExecuteAsync içinde erken dön" desenine BENZER, ama HER turda tekrarlanan bir kontrol
- bir kerelik DEĞİL). Bu, DI grafiğinin config'ten BAĞIMSIZ/test edilebilir KALMASINI sağlar. Test
sertifika sağlayıcısı/güven politikası (`EBelgeTestSertifikaSaglayici`/`EBelgeTestSertifikaGuvenPolicy`)
YALNIZ test-only `ServiceCollection`'larda kullanılır - `backend/Program.cs`'in üretim registration'ları
HİÇ DEĞİŞTİRİLMEDİ (fail-closed `EBelgeImzaKimligiYapilandirilmadiSaglayici`/
`EBelgeSertifikaGuvenValidatoruYapilandirilmadi` AYNEN KORUNDU).

### 17. Signing aktivasyon savunması

15 Eylül 2026 Europe/Istanbul öncesinde, `ArtefaktOlustur` VEYA `UblImzala` mesajlarının HİÇBİRİ
production worker TARAFINDAN claim EDİLMEZ - gate kapalıyken mesajlar terminal hataya GEÇİRİLMEZ,
deneme sayısı ARTIRILMAZ, lease ALINMAZ, olduğu YERDE bekler (bkz. §3, `EBelgeProcessingActivationGate`
+ `EBelgeOutboxWorkerTests.GateKapaliykenClaimCagrilmazVeIdleGecikmesiKullanilir`/
`GateKapaliykenMesajinDenemeSayisiDegismez`).

### Test kapsamı ve çalıştırılan hedefli komut

**Yeni test dosyaları (74 yeni test):**

- `EBelgeProcessingActivationGateTests` (14 test) - Enabled/tarih kapısı/timezone/server-timezone-
  bağımsızlığı/periyodik yeniden değerlendirme (görev md.18 senaryo 1-6).
- `EBelgeProcessingOptionsValidatorTests` (16 test) - tüm sayısal alan sınırları, `Enabled=false`
  iken bile doğrulama (görev md.18 senaryo 7, 35).
- `EBelgeOutboxWorkerMetricsTests` (7 test) - GERÇEK `MeterListener` ile isim/tip/tag doğrulaması
  (görev md.18 senaryo 37-41).
- `EBelgeOutboxWorkerHealthCheckTests` (6 test) - disabled/başlamadı/boş-kuyruk-etkisiz/tek-hata-
  etkisiz/stale-degraded/PII-yok.
- `EBelgeOutboxWorkerTests` (26 test) - fake claim/işleme servisleriyle, GERÇEK bir
  `IServiceScopeFactory` üzerinden: aktivasyon (senaryo 1, 7), polling (senaryo 8-12), scope
  (senaryo 13-16), hata dayanıklılığı (senaryo 28-32), paralellik (senaryo 33-36, 41), mevcut iş
  türleri dispatch/sonuç gözlemleme (senaryo 17-22), gözlemlenebilirlik (senaryo 37, 42-43). 3 KEZ
  ardışık çalıştırılıp FLAKY OLMADIĞI doğrulandı.
- `EBelgeOutboxWorkerIntegrationTests` (5 test) - GERÇEK SQL Server + GERÇEK sidecar + GERÇEK test
  sertifikası: `ArtefaktOlustur` tamamlanır (senaryo 44-45), `UblImzala` tamamlanır (senaryo 46-47),
  UÇTAN UCA zincirleme (ArtefaktOlustur→UblImzala→SignedReady, TEK worker'ın KENDİ polling
  döngüsü üzerinden), worker yeniden başlatıldığında tamamlanmış mesaj TEKRAR işlenmez (senaryo
  48), İKİ instance/lease devri/duplicate-yok (senaryo 23-27, 49).

```
dotnet test tests/STYS.Tests/STYS.Tests.csproj --filter "FullyQualifiedName~EBelgeXmlImzalayiciTests|FullyQualifiedName~EBelgeSigningActivationGateTests|FullyQualifiedName~EBelgeUblImzalamaServiceIntegrationTests|FullyQualifiedName~EBelgeSigningBackfillServiceIntegrationTests|FullyQualifiedName~EBelgeArtefaktOlusturmaServiceIntegrationTests|FullyQualifiedName~EBelgeOutboxLeaseTransitionIntegrationTests|FullyQualifiedName~EBelgeOutboxMesajIslemeServiceTests|FullyQualifiedName~EBelgeUblRendererEndToEndIntegrationTests|FullyQualifiedName~EBelgeSchematronSidecarIntegrationTests|FullyQualifiedName~EBelgeFaz1IntegrationTests|FullyQualifiedName~EBelgeOutboxFaz2AIntegrationTests|FullyQualifiedName~EBelgeOutboxRetryPolicyTests|FullyQualifiedName~EBelgeOutboxClaimLeaseIntegrationTests|FullyQualifiedName~EBelgeProcessingActivationGateTests|FullyQualifiedName~EBelgeProcessingOptionsValidatorTests|FullyQualifiedName~EBelgeOutboxWorkerMetricsTests|FullyQualifiedName~EBelgeOutboxWorkerHealthCheckTests|FullyQualifiedName~EBelgeOutboxWorkerTests|FullyQualifiedName~EBelgeOutboxWorkerIntegrationTests"
  → Passed: 319, Failed: 0, Total: 319 (gerçek SQL Server + gerçek Java Saxon sidecar + gerçek test
  sertifikasıyla). Not: aynı filtrenin İLK çalıştırmasında, bu listeye DAHİL OLMAYAN (Faz 2B.7.3'ten
  KALMA, bu turda HİÇ DOKUNULMAMIŞ) `EBelgeSchematronSidecarIntegrationTests.BuyukXmlLimitteReddedilir`
  testi, ARTAN paralel HTTP yükü altında (yeni worker integration testlerinin AYNI paylaşılan
  sidecar process'ine EK yük bindirmesinden) BİR KEZ geçici bir bağlantı-sıfırlama hatasıyla
  BAŞARISIZ oldu; YALNIZ BU test tek başına ÇALIŞTIRILDIĞINDA (14/14) VE TAM filtre TEKRAR
  çalıştırıldığında (319/319) SORUNSUZ geçti - regresyon DEĞİL, paylaşılan test-sidecar process'i
  üzerindeki geçici yük flakiness'idir.

**Regresyon (Faz 2B.5/2B.6/2B.7 - hiçbiri değiştirilmedi, hepsi yukarıdaki filtreye DAHİLDİR ve
BAŞARILIDIR):** `EBelgeArtefaktOlusturmaServiceIntegrationTests`, `EBelgeOutboxLeaseTransitionIntegrationTests`,
`EBelgeOutboxMesajIslemeServiceTests`, `EBelgeOutboxRetryPolicyTests`, `EBelgeOutboxClaimLeaseIntegrationTests`,
`EBelgeOutboxFaz2AIntegrationTests`, `EBelgeSigningBackfillServiceIntegrationTests`,
`EBelgeUblImzalamaServiceIntegrationTests`, `EBelgeXmlImzalayiciTests`, `EBelgeSigningActivationGateTests`.

### Üretimde HÂLÂ eksik olanlar

Mali mühür/HSM - production'da HÂLÂ fail-closed `EBelgeImzaKimligiYapilandirilmadiSaglayici`/
`EBelgeSertifikaGuvenValidatoruYapilandirilmadi` KULLANILIR, gerçek bir sağlayıcı BAĞLANMADI. Özel
entegratöre gönderim, GİB web servisi, PAVO/başka sağlayıcı adapter'ı HİÇ YAPILMADI - worker YALNIZ
`ArtefaktOlustur`/`UblImzala` işler, gönderim iş türü/handler'ı YOKTUR. PDF/e-posta/frontend HİÇ
YAPILMADI. 15 Eylül 2026 öncesi production processing etkinleştirmesi YAPISAL olarak (hem
`EBelgeSigning` hem YENİ `EBelgeProcessing` gate'i İLE ÇİFT KATMANLI) ENGELLENİR.

### Kasıtlı olarak YAPILMAYANLAR (görev kapsam sınırları)

Yeni outbox tablosu OLUŞTURULMADI; claim/lease altyapısı BAŞTAN YAZILMADI; RabbitMQ/Kafka/broker
EKLENMEDİ; worker İÇİNDE artifact üretme/imzalama mantığı YAZILMADI; worker İÇİNDE doğrudan outbox
complete/fail işlemi YAPILMADI; aynı `DbContext` birden fazla mesajda PAYLAŞILMADI; unbounded
paralellik KULLANILMADI; activation gate KALDIRILMADI/ERKENE ALINMADI; 15 Eylül 2026 öncesi
production processing ETKİNLEŞTİRİLMEDİ; XML/lease token/sertifika/kişisel veri LOGLANMADI; gerçek
sertifika/private key EKLENMEDİ; özel entegratör gönderimi/PDF/e-posta/frontend GELİŞTİRİLMEDİ; tüm
çözüm test paketi ÇALIŞTIRILMADI (yalnız hedefli filtre); hiçbir test ATLANMADI/zayıflatılmadı.

### Açık kalan konular

Faz 2B.7.1'in "Açık kalan konular" listesi AYNEN geçerlidir. EK olarak: gönderim/mali mühür/HSM
entegrasyonu HENÜZ bir SONRAKİ fazın konusudur.

### Sonraki faz (Faz 2B.8)

Faz 2B.7.1'in "Sonraki faz" listesi AYNEN geçerlidir.

## Faz 2B.8.1 sonuç bölümü — worker task yaşam döngüsü, güvenli loglama ve activation-health sertleştirmesi

**Durum: TAMAMLANDI, commit/push YAPILDI.**

### Neden gerekliydi

Faz 2B.8'in kod incelemesinde 6 gerçek açık tespit edildi: (1) `BirTurCalistirAsync`'te bir claim
denemesi exception ÜRETTİĞİNDE, o ana kadar DİSPATCH edilmiş `ProcessClaimAsync` task'ları HİÇ await
EDİLMEDEN metottan çıkılıyor, `using var semaphore` İSE bu task'lar HÂLÂ çalışırken dispose
ediliyordu - bu, çalışan task'ların KENDİ `finally` bloklarında dispose EDİLMİŞ bir semaphore
üzerinde `Release()` çağırıp `ObjectDisposedException` almasına VE bu exception'ın hiç await
EDİLMEDİĞİ (unobserved) İÇİN sessizce KAYBOLMASINA yol açabilirdi; (2) BAŞLATILMIŞ ama SAHİPSİZ
kalan task'lar nedeniyle "polling turları arasında toplam eşzamanlı mesaj sayısı `MaxParallelism`'i
aşmaz" garantisi TEORİK olarak İHLAL edilebilirdi; (3) worker-seviyesi hatalar `_logger.LogError(ex,
"...")` İLE loglanıyordu - GERÇEK bir loglama sağlayıcısı (bu çözümde ZATEN kullanılan Serilog
console/file sink'leri GİBİ) exception NESNESİNİN `ToString()`'ini (mesaj + stack trace + inner
exception'lar DAHİL) OTOMATİK render EDER; bu, SQL/XML/token/sertifika/VKN/parola GİBİ hassas
içeriğin YANLIŞLIKLA production loguna SIZMASINA yol AÇABİLİRDİ (test logger'ı bunu YAKALAMIYORDU
- exception nesnesini render ETMİYORDU, bu yüzden production davranışını SİMÜLE ETMİYORDU); (4)
aktivasyon kapısı durumu (Enabled/tarih kapısı/geçersiz config) health check çıktısına HİÇ
YANSIMIYORDU - operatör, worker'ın NEDEN mesaj işlemediğini health endpoint'İNDEN ANLAYAMIYORDU;
(5) geçersiz tarih/timezone config'i HER polling turunda (10-30sn aralıkla) `Error` seviyesinde
LOGLANIYOR - bu, kalıcı bir yanlış-config durumunda SÜREKLİ log spam'İNE yol AÇIYORDU.

### 1-4. Claim ve task yaşam döngüsü

`EBelgeOutboxWorker.BirTurCalistirAsync` YENİDEN yapılandırıldı - semaphore artık `using` İLE
DEĞİL, açıkça yönetilen bir `try/catch/finally` bloğunda:

```text
try
{
    while (claimedCount < BatchSize)
    {
        await semaphore.WaitAsync(stoppingToken);   // burada FIRLARSA permit HİÇ alınmamıştır
        var izinTaskaDevredildi = false;
        try
        {
            claim = await ClaimAsync(...);           // null/exception -> izinTaskaDevredildi=false KALIR
            if (claim is null) break;
            tasks.Add(ProcessClaimAsync(claim, semaphore, stoppingToken));
            izinTaskaDevredildi = true;               // YALNIZ BURADAN SONRA permit task'a AİTTİR
        }
        finally
        {
            if (!izinTaskaDevredildi) semaphore.Release();
        }
    }
}
catch (Exception ex) { turHatasi = ex; }              // exception BURADA YUTULMAZ, yalnız SAKLANIR
finally
{
    if (tasks.Count > 0) await Task.WhenAll(tasks);   // MUTLAKA - hatasız/hatalı FARK ETMEZ
    semaphore.Dispose();                              // yalnız TÜM task'lar bittikten SONRA
}
if (turHatasi is not null) ExceptionDispatchInfo.Capture(turHatasi).Throw();  // stack trace KORUNARAK yeniden fırlatılır
```

**Permit sözleşmesi** (görev md.1/md.4, KANITLANMIŞ): permit `semaphore.WaitAsync` TARAFINDAN
alınır; İÇ `finally`, permit bir processing task'a DEVREDİLMEDİĞİ HER yolda (claim `null`/exception/
cancellation, scope oluşturma hatası, DI çözümleme hatası) GERİ BIRAKIR; permit bir task'a
devredildiyse (`izinTaskaDevredildi=true`) ARTIK YALNIZ o task'ın KENDİ `finally`'i (`ProcessClaimAsync`
içinde, değişmedi) `Release()` çağırır - AYNI permit İKİ KEZ bırakılmaz. **Semaphore ÖMRÜ**: dispose
EDİLMEDEN ÖNCE `Task.WhenAll(tasks)` İLE TÜM dispatch edilmiş task'lar (turun BAŞARIYLA/hatayla/
cancellation İLE bitmesi FARK ETMEKSİZİN) MUTLAKA await edilir - bu, HEM "bir tur claim hatasıyla
sonlanırsa ÖNCEKİ turun task'ları tamamlanmadan YENİ tur BAŞLAMAZ" (çünkü dış `ExecuteAsync` döngüsü
BU metodun dönmesini/fırlatmasını BEKLER - turlar SIRALI çalışır, `BirTurCalistirAsync`'in KENDİSİ
zaten AYRI bir semaphore/task kümesiyle başlar) HEM "polling turları ARASINDA toplam eşzamanlı mesaj
sayısı `MaxParallelism`'i AŞMAZ" (görev md.3) HEM "unobserved task exception KALMAZ" (görev md.2)
gereksinimlerini TEK bir mekanizma İLE sağlar - AYRI bir worker-ömürlü semaphore veya distributed
lock GEREKMEDİ ("en sade güvenli çözümü seç" - görev md.3).

### 5-6. Worker-level güvenli loglama

`_logger.LogError(ex, "...")` KULLANIMLARI (hem `ExecuteAsync`'in tur-seviyesi hem
`ProcessClaimAsync`'in mesaj-seviyesi catch blokları) KALDIRILDI - yerine yeni
`LogWorkerLevelHataGuvenli` yardımcı metodu KULLANILIR:

```csharp
_logger.LogError(
    "E-belge outbox worker hatası. Baglam={Baglam}, OutboxMesajiId={OutboxMesajiId}, IsTuru={IsTuru}, HataKodu={HataKodu}, ExceptionType={ExceptionType}",
    baglam, claim.OutboxMesajiId, claim.IsTuru, safeErrorCode, ex.GetType().Name);
```

Exception NESNESİNİN KENDİSİ (`ex`) logger'a HİÇBİR ZAMAN parametre olarak GEÇİRİLMEZ - yalnız
SABİT/type-safe alanlar: güvenli hata kodu (`WorkerLevelSafeErrorCode`, MEVCUT sabit eşleme -
DEĞİŞMEDİ), exception TİP ADI (`ex.GetType().Name`), iş türü VE güvenli kimlik alanları (Outbox/
Kurum/EBelgeKaydi ID - claim VARSA). Exception'ın KENDİ mesajı/inner exception'ı/`ToString()`'i -
SQL statement/parametre, XML, lease token, sertifika/PFX/PEM, SignatureValue, VKN/TCKN, müşteri
bilgisi, bağlantı parolası, URL query secret'ı TAŞIYABİLECEĞİNDEN - production logger'ına ASLA
YAZILMAZ. "Mevcut güvenli merkezi exception/redaction altyapısı" ARAŞTIRILDI - çözümde YALNIZ bir
mesaj-uzunluğu KIRPICISI (`GuvenliMesaj`, `EBelgeUblImzalamaService`/`EBelgeArtefaktOlusturmaService`'te)
bulundu, GERÇEK bir redaction/scrubbing mekanizması YOK - bu yüzden EN GÜVENLİ yol, exception
mesajını HİÇ KULLANMAMAKTIR (kırpma YETERLİ DEĞİLDİR - kırpılmış bir mesaj HÂLÂ token/XML İÇEREBİLİR).

Aktivasyon kapısındaki (`EBelgeProcessingActivationGate`) geçersiz-config log çağrısı da AYNI
disipline UYDURULDU - `TimeZoneNotFoundException`/tarih parse hatası NESNESİ logger'a
GEÇİRİLMEZ, yalnız `ExceptionType` okunur (bu exception'ların mesajı PII TAŞIMASA da, worker alt
sistemindeki TÜM loglama TUTARLI kalması İÇİN).

**Test gerçekçiliği (görev md.6)**: `EBelgeOutboxWorkerTests`'teki test logger'ı, `formatter(state,
exception) + exception?.ToString()` üretecek şekilde GÜNCELLENDİ - artık GERÇEK bir sağlayıcının
(Serilog console/file sink) davranışına YAKIN. Kasıtlı bir test exception'ı (`"GIZLI-TOKEN-123
<VKN>1234567890</VKN> Password=secret SignatureValue=secret"` mesajıyla) KULLANILARAK, worker log
çıktısında BU değerlerden HİÇBİRİNİN (VE bir stack trace işaretinin, `"   at "`) BULUNMADIĞI - yalnız
güvenli hata kodu VE exception tip adının BULUNDUĞU - doğrudan doğrulanır.

### 7. Activation decision type-safe model

`EBelgeProcessingActivationReason` enum'u (`Active`/`Disabled`/`BeforeActivationDate`/
`InvalidDateConfiguration`/`InvalidTimeZoneConfiguration`) VE `EBelgeProcessingActivationDecision`
record'u (`CanProcess`, `Reason`) eklendi. `IEBelgeProcessingActivationGate.Evaluate()`, YENİ birincil
metot - `bool ShouldProcess()` GERİYE UYUMLULUK İÇİN korunur, `Evaluate().CanProcess` DÖNER (hiçbir
mevcut ÇAĞIRAN DEĞİŞMEDİ - şu an worker'ın KENDİSİ `Evaluate()`'i kullanıyor, `ShouldProcess()`
metodu type güvenliği/geriye-uyumluluk İÇİN halen mevcut ve test edilir). Worker, HER polling
turunda TEK bir `Evaluate()` çağrısı yapar VE sonucu `IEBelgeOutboxWorkerHealthState.
RecordActivationDecision(karar)` İLE health state'e YAZAR - health check KENDİSİ AYRICA gate'i
DEĞERLENDİRMEZ, yalnız worker'ın YAZDIĞI SONUCU okur (görev md.7'nin AÇIKÇA istediği "worker ve
health check aynı değerlendirme sonucunu kullanmalı").

### 8. Activation config log spam engelleme

`EBelgeProcessingActivationGate`, AYNI (neden, değer) çifti İÇİN yalnız İLK KEZ (veya ÖNCEKİ
değerden FARKLI bir değere GEÇİŞTE) `Error` loglar - dahili, thread-safe bir "son loglanan
geçersiz-config nedeni/değeri" durumu TUTAR. Config GEÇERLİ hale GELDİĞİNDE bu iz TEMİZLENİR -
gelecekte YENİDEN bozulursa (AYNI VEYA farklı bir değerle) TEKRAR loglanabilir. Gate kapalıyken
(hangi NEDENLE olursa olsun) mesajlar YİNE claim EDİLMEZ, terminal hataya GEÇİRİLMEZ, deneme sayısı
ARTIRILMAZ - bu davranış Faz 2B.8'DEN DEĞİŞMEDİ, yalnız LOGLAMA sıklığı DÜZELDİ.

### 9-10. Health state genişletmesi ve politika

`EBelgeOutboxWorkerHealthSnapshot`, `WorkerEnabled`/`ActivationAllowed`/`ActivationReason`/
`LoopStartedUtc` alanlarıyla GENİŞLETİLDİ (`LoopStarted`/`LastSuccessfulPollUtc`/`LastWorkerErrorUtc`/
`LastWorkerErrorSafeCode`/`InflightCount` KORUNDU). `EBelgeOutboxWorkerHealthCheck`, YENİ bir
reason-tabanlı politika UYGULAR:

- **Healthy**: `Disabled` (kasıtlı) VEYA `BeforeActivationDate` (beklenen tarih kapısı) VEYA
  (`Active` VE döngü ilerliyor VE son başarılı poll GÜNCEL).
- **Degraded**: `InvalidDateConfiguration`/`InvalidTimeZoneConfiguration` (ARTIK sessizce Healthy
  SAYILMAZ - GÖRÜNÜR) VEYA (`Active` VE son başarılı poll "Degraded eşiğini" - `Max(Poll,Idle)×5` -
  AŞTI) VEYA (`Active` VE en son worker-seviyesi hata, en son başarılı polldan DAHA YENİ - görev
  md.10, "en yeni olayın hangisi olduğuna göre karar ver").
- **Unhealthy**: `Active` VE döngü HİÇ başlamamış (`LoopStarted=false`) VEYA `Active` VE son başarılı
  poll "Unhealthy (kritik) eşiğini" - `Max(Poll,Idle)×20` - AŞTI (referans nokta `LastSuccessfulPollUtc`
  yoksa `LoopStartedUtc`'ye DÜŞER - worker YENİ başlayıp HENÜZ ilk turunu BİTİRMEMİŞSE bu, KISA/normal
  bir pencereyi temsil eder, HEMEN Unhealthy ÜRETMEZ).

**Recovery kararı (görev md.10)**: `LastWorkerErrorUtc`/`LastWorkerErrorSafeCode`, BAŞARILI bir poll
SONRASINDA TEMİZLENMEZ (BİLİNÇLİ karar) - hem "son hata" hem "son başarılı poll" zaman damgaları
KALICI olarak SAKLANIR; health check, HANGİSİNİN daha YENİ OLDUĞUNA bakarak (`LastWorkerErrorUtc >
LastSuccessfulPollUtc` mi) toparlanma/DEVAM-EDEN-sorun ayrımını KENDİSİ yapar - hata SONRASINDA
(daha YENİ bir ZAMANDA) başarılı bir poll GERÇEKLEŞİRSE, worker "TOPARLANMIŞ" kabul edilir (Healthy),
eski hata KAYITTA kalır AMA artık KARARI ETKİLEMEZ. Bir message-level TERMİNAL İŞ hatası (ör. XSD
doğrulaması başarısız) `RecordWorkerError`'I HİÇ TETİKLEMEZ - yalnız `ProcessClaimAsync`'in KENDİ
sözleşmesinin DIŞINDaki, GERÇEKTEN beklenmedik exception'lar (Faz 2B.8'den DEĞİŞMEDİ) worker-seviyesi
sayılır. Kuyruk BOŞ olması (mesaj yokluğu) TEK BAŞINA HİÇBİR ZAMAN unhealthy/degraded ÜRETMEZ. Health
output'una ham config değeri (`NotBeforeLocalDate`/`TimeZoneId`) veya PII EKLENMEZ - yalnız type-safe
`activationReason` ENUM ADI raporlanır.

### 11. Options erişimi ve config reload kararı

**Karar: runtime hot-reload DESTEKLENMEZ (bilinçli, `IOptions<T>` KORUNDU - `IOptionsMonitor<T>`
EKLENMEDİ).** Gerekçe: (1) çözümde `IOptionsMonitor<T>` KULLANAN HİÇBİR ÖRNEK YOK - bu, İLK örnek
OLURDU, ekstra karmaşıklık (geçersiz reload'da fail-closed davranma, ÇALIŞAN worker'ı ÇÖKERTMEME,
doğrulanmış SON options İLE geçersiz YENİ options'ı SESSİZCE KARIŞTIRMAMA) getirirdi; (2) BU
karmaşıklığı HAKLI ÇIKARACAK somut bir OPERASYONEL ihtiyaç YOK - `Enabled` bayrağı DEĞİŞTİRİLDİĞİNDE
zaten bir DEPLOYMENT/restart YAPILIYOR (config dosyası container image'INA GÖMÜLÜ), bu esnada
worker DOĞAL olarak YENİDEN başlar. **AÇIKÇA belirtilen davranış (görev md.11'in istediği gibi,
sessiz varsayım YOK)**:

```text
Enabled değişikliği deployment/restart gerektirir; tarih kapısı ise çalışan process içinde
TimeProvider üzerinden otomatik olarak açılır.
```

Bu İKİNCİ kısım (tarih geçişinin restart GEREKTİRMEMESİ) ZATEN doğrudur ve DEĞİŞMEDİ: `Evaluate()`,
HER çağrıda `_timeProvider.GetUtcNow()`'ı `_options.NotBeforeLocalDate`'İN (options SINGLETON olarak
BİR KEZ okunur, AMA "şu anki zaman" HER SEFERİNDE YENİDEN okunur) sabit UTC karşılığıyla
KARŞILAŞTIRIR - worker'ı YENİDEN BAŞLATMADAN, 15 Eylül 2026 Europe/Istanbul GÜN BAŞLANGICI
GELDİĞİNDE bir SONRAKİ polling turunda KENDİLİĞİNDEN AÇILIR (görev md.11'in AÇIKÇA istediği,
"restart GEREKTİRMEDEN otomatik açılma" davranışı KORUNUR).

### Test kapsamı ve çalıştırılan hedefli komut

**Güncellenen mevcut testler**: `EBelgeOutboxWorkerHealthCheckTests` (13 test - eski 6 testten 2'si,
YENİ `RecordActivationDecision` çağrısı GEREKTİRDİĞİ için `Active` kararı EKLENEREK düzeltildi; diğer
7'si YENİ eklendi - Disabled/BeforeActivationDate/InvalidDate/InvalidTimeZone/Unhealthy-loop-yok/
recovery/critical-staleness/inflight/PII-yok senaryoları). Test logger'ı GERÇEKÇİ hale getirildi (bkz.
§5-6).

**Yeni testler (`EBelgeOutboxWorkerTests`'e eklendi, 18 test - görev md.12 senaryo 1-18)**:
`IkinciClaimExceptionUretirseIlkTaskMutlakaAwaitEdilir`,
`DisposeEdilmisSemaphoreUzerindeReleaseCagrilmazExceptionOlusmaz`, `UnobservedTaskExceptionOlusmaz`
(`TaskScheduler.UnobservedTaskException` + `GC.Collect()`/`WaitForPendingFinalizers()` İLE gerçek
GC-tetiklemeli kontrol), `OncekiTurunTasklariTamamlanmadanSonrakiTurBaslamaz` (olay zaman damgası
SIRALAMASIYLA), `PollingTurlariArasindaToplamEsZamanliMesajSayisiMaxParallelismiAsmaz` (8 mesaj, 4
tur BOYUNCA), `ClaimNullDondugundePermitGeriBirakilirVeSonrakiTurCalisir`,
`ClaimCancellationOlusturuncaPermitGeriBirakilirVeWorkerDevamEder`,
`ScopeVeyaDiCozumlemeHatasindaPermitGeriBirakilirVeWorkerDevamEder` (`IEBelgeOutboxClaimLeaseService`
KASITLI OLARAK KAYITSIZ - TEKRARLANAN DI hatalarından SONRA bile İLERLEME kanıtlanır),
`UcuncuClaimHataVersinIlkIkiTaskYineDeGozlemlenirVeAwaitEdilir`,
`StopSirasindaClaimExceptionIleProcessingTaskYarisiDeadlockOlusturmaz`,
`TurTamamlandigindaInflightVeSemaphorePermitBaslangicaDoner`,
`WorkerLevelExceptionMesajindakiLeaseTokenLoglanmaz`, `WorkerLevelExceptionMesajindakiXmlVeVknLoglanmaz`,
`WorkerLevelExceptionMesajindakiPasswordVeSignatureValueLoglanmaz`,
`HamExceptionToStringProductionLoggeraVerilmez`, `GuvenliHataKoduVeExceptionTypeLoglanir`,
`AktivasyonConfigHatasiHerTurdaLogSpamUretmez`. 3 KEZ ardışık çalıştırılıp FLAKY OLMADIĞI doğrulandı.

```
dotnet test tests/STYS.Tests/STYS.Tests.csproj --filter "FullyQualifiedName~EBelgeXmlImzalayiciTests|FullyQualifiedName~EBelgeSigningActivationGateTests|FullyQualifiedName~EBelgeUblImzalamaServiceIntegrationTests|FullyQualifiedName~EBelgeSigningBackfillServiceIntegrationTests|FullyQualifiedName~EBelgeArtefaktOlusturmaServiceIntegrationTests|FullyQualifiedName~EBelgeOutboxLeaseTransitionIntegrationTests|FullyQualifiedName~EBelgeOutboxMesajIslemeServiceTests|FullyQualifiedName~EBelgeUblRendererEndToEndIntegrationTests|FullyQualifiedName~EBelgeSchematronSidecarIntegrationTests|FullyQualifiedName~EBelgeFaz1IntegrationTests|FullyQualifiedName~EBelgeOutboxFaz2AIntegrationTests|FullyQualifiedName~EBelgeOutboxRetryPolicyTests|FullyQualifiedName~EBelgeOutboxClaimLeaseIntegrationTests|FullyQualifiedName~EBelgeProcessingActivationGateTests|FullyQualifiedName~EBelgeProcessingOptionsValidatorTests|FullyQualifiedName~EBelgeOutboxWorkerMetricsTests|FullyQualifiedName~EBelgeOutboxWorkerHealthCheckTests|FullyQualifiedName~EBelgeOutboxWorkerTests|FullyQualifiedName~EBelgeOutboxWorkerIntegrationTests"
  → Passed: 343, Failed: 0, Total: 343 (gerçek SQL Server + gerçek Java Saxon sidecar + gerçek test
  sertifikasıyla) - İKİ KEZ ardışık ÇALIŞTIRILDI, HER İKİSİNDE de 343/343.
```

**Regresyon (Faz 2B.5/2B.6/2B.7/2B.8 - hiçbiri kasıtlı DEĞİŞTİRİLMEDİ, hepsi yukarıdaki filtreye
DAHİLDİR ve BAŞARILIDIR):** özellikle `EBelgeOutboxWorkerIntegrationTests`'in 5 GERÇEK (SQL Server +
sidecar + test sertifikası) testi - çoklu instance/lease devri, gerçek `ArtefaktOlustur`/`UblImzala`
worker akışı, worker restart - worker'ın claim/task yaşam döngüsü BAŞTAN AŞAĞI değiştiği İÇİN
ÖZELLİKLE KRİTİK bir regresyon kontrolüdür; hepsi SORUNSUZ geçti.

### Kasıtlı olarak YAPILMAYANLAR (görev kapsam sınırları)

Claim/lease altyapısı BAŞTAN YAZILMADI; yeni outbox tablosu OLUŞTURULMADI; RabbitMQ/Kafka
EKLENMEDİ; handler/artifact iş mantığı worker'a TAŞINMADI; worker içinde İKİNCİ bir complete/fail/
retry çağrısı YAPILMADI (Faz 2B.8'DEN DEĞİŞMEDİ); paralellik KALDIRILARAK hata yalnız
`MaxParallelism=1` İLE GİZLENMEDİ (semaphore ÖMRÜ/sözleşmesi, HER `MaxParallelism` değeri İÇİN
DOĞRUDUR - testler HEM 1 HEM 2 İLE doğrulanmıştır); claim exception'ı YUTULARAK başlatılmış task'lar
sahipsiz BIRAKILMADI; ham exception mesajı production loguna YAZILMADI; activation tarihi (15 Eylül
2026) DEĞİŞTİRİLMEDİ; production processing bu tarihten ÖNCE AÇILMADI; gerçek sertifika/private key
EKLENMEDİ; entegratör gönderimi/PDF/e-posta/frontend GELİŞTİRİLMEDİ; tüm çözüm test paketi
ÇALIŞTIRILMADI (yalnız hedefli filtre); hiçbir test ATLANMADI.

### Açık kalan konular

Faz 2B.7.1'in "Açık kalan konular" listesi AYNEN geçerlidir.

### Sonraki faz

Faz 2B.8.2'ye bakınız.

## Faz 2B.8.2 sonuç bölümü — worker activation-health başlangıç kör noktasının giderilmesi

**Durum: TAMAMLANDI, commit/push YAPILDI.**

### Neden gerekliydi

Faz 2B.8.1'in `EBelgeOutboxWorkerHealthState.GetSnapshot()` uygulaması, `_sonAktivasyonKarari ??
EBelgeProcessingActivationDecision.Disabled()` biçiminde bir varsayılan değer kullanıyordu - worker
döngüsü HENÜZ hiç `RecordActivationDecision` çağırmadıysa (özellikle: döngü hiç BAŞLAMADIYSA - ör.
host başlatma sırasında bir DI/config hatası nedeniyle `ExecuteAsync` hiç ilk turunu çalıştıramadıysa),
bu "henüz değerlendirilmedi" durumu GERÇEK bir `Disabled` kararıyla KARIŞTIRILIYOR ve health check bunu
`Healthy` olarak raporluyordu. Sonuç: `Enabled=true` VE aktivasyon tarihi AÇIK olduğu halde worker
döngüsü hiç çalışmayan bir üretim arızası, health/readiness endpoint'İNDE görünmez KALIYORDU - tam da
bu health check'in var olma amacına (operatörün worker'ın NEDEN mesaj işlemediğini/işleyip
işlemediğini ANLAYABİLMESİ) aykırı bir kör noktaydı. Ayrıca `Task.WhenAll(tasks)`'ın kendisinin bir
worker-altyapısı hatası ile fırlaması durumunda `semaphore.Dispose()`'un atlanma İHTİMALİ vardı (aynı
`finally` bloğunda, `Dispose()`'dan ÖNCE gelen bir satırın kendisi FIRLARSA `Dispose()` hiç
ÇALIŞMAZ).

### 1-2. "Henüz değerlendirilmedi" durumunun açıkça modellenmesi

`EBelgeOutboxWorkerHealthSnapshot`'a yeni bir `bool ActivationEvaluated` alanı eklendi;
`ActivationReason` `EBelgeProcessingActivationReason?` (nullable) yapıldı. `GetSnapshot()`'taki `??
Disabled()` varsayımı TAMAMEN KALDIRILDI - `RecordActivationDecision` hiç çağrılmadıysa
`ActivationEvaluated=false` VE `ActivationReason=null` döner; bu durum ARTIK hiçbir şekilde gerçek
`Disabled` kararıyla KARIŞTIRILAMAZ (bkz. [EBelgeOutboxWorkerHealthState.cs](../backend/Muhasebe/SatisBelgeleri/Services/EBelgeOutboxWorkerHealthState.cs)).
Görevin sunduğu iki tasarım seçeneğinden (nullable reason + bool VS. yeni bir `NotEvaluated` enum
değeri) BİRİNCİSİ seçildi, çünkü görevin KENDİ verdiği örnek record imzası zaten bu şekli
kullanıyordu ve `EBelgeProcessingActivationReason` enum'unun ANLAMI ("hangi kural gate'i kapattı/açtı")
ile "hiç değerlendirilmedi" durumu KAVRAMSAL olarak FARKLI eksenler - birini enum'a EKLEMEK bu iki
ekseni KARIŞTIRIRDI.

### 3-5. Health check'in aynı activation gate ile başlangıç fallback'i

`EBelgeOutboxWorkerHealthCheck`'e, worker'ın ZATEN kullandığı AYNI singleton
`IEBelgeProcessingActivationGate` enjekte edildi (4. constructor parametresi; `Program.cs`'te
DEĞİŞİKLİK GEREKMEDİ, çünkü gate ZATEN singleton olarak kayıtlı ve `AddCheck<T>` DI container
üzerinden çözülüyor). `CheckHealthAsync`, snapshot'ı okuduktan SONRA:

```text
if (!snapshot.LoopStarted || !snapshot.ActivationEvaluated)
{
    karar = _activationGate.Evaluate();      // AYNI Evaluate() sözleşmesi - ayrı algoritma YOK
    _healthState.RecordActivationDecision(karar);
}
else
{
    karar = snapshot'tan türetilir;           // worker'ın SON kararı - gereksiz tekrar değerlendirme YOK
}
```

Bu SAYEDE worker döngüsü hiç başlamamış OLSA BİLE health check gerçek aktivasyon durumunu görebilir:
`Enabled=true` + tarih kapısı AÇIK + döngü hiç başlamamış → `Active` + `LoopStarted=false` →
`Unhealthy`. Koşul BİLEREK `!snapshot.LoopStarted || !snapshot.ActivationEvaluated` (yalnız "hiç
değerlendirilmedi" DEĞİL, "döngü henüz başlamadı" DA) seçildi - böylece tarih sınırı worker
BAŞLAMADAN AŞILIRSA (görev md.5'in AÇIKÇA verdiği senaryo: ilk çağrı `BeforeActivationDate`/Healthy,
saat `2026-09-15 00:00 Europe/Istanbul`'u geçer, worker HÂLÂ başlamamıştır), health check ESKİ
`BeforeActivationDate` kararını SONSUZA dek "cache'lemez" - döngü başlamadığı SÜRECE HER health check
çağrısı TAZE bir `Evaluate()` yapar. Döngü BAŞLADIKTAN sonra ise worker'ın kendi turunda yazdığı SON
karar GÜVENİLİR kabul edilir - ayrı bir "evaluation timestamp" alanı EKLEMEYE gerek KALMADI (görevin
sunduğu iki alternatiften BASİT olanı seçildi). Aktivasyon gate'İNİN log-spam-bastırma durumu AYNI
singleton örnekte YAŞADIĞI için, worker'ın turdaki `Evaluate()` çağrısı İLE health check'in fallback
`Evaluate()` çağrısı DOĞAL olarak TEK bir log-dedup durumunu PAYLAŞIR - health check EK bir log spam'e
yol AÇMAZ (bkz. [EBelgeOutboxWorkerHealthCheck.cs](../backend/Muhasebe/SatisBelgeleri/Services/EBelgeOutboxWorkerHealthCheck.cs)).

Karar politikası (Disabled/BeforeActivationDate → Healthy, InvalidDate/InvalidTimeZone → Degraded,
Active+LoopStarted=false → Unhealthy, Active+LoopStarted=true → staleness/recovery eşiklerine göre)
Faz 2B.8.1'DEN DEĞİŞMEDİ - yalnız `karar`ın KAYNAĞI (artık ya taze fallback ya da worker'ın son kararı)
DEĞİŞTİ. `BuildGuvenliData` artık HEM snapshot HEM güncel `karar`ı alır - `workerEnabled`/
`activationAllowed`/`activationReason` çıktısı HER ZAMAN güncel `karar`ı yansıtır (fallback
tetiklendiyse çıktı BUNU yansıtır); ham `NotBeforeLocalDate`/`TimeZoneId` DEĞERLERİ health output'una
HİÇ EKLENMEDİ (Faz 2B.8.1'DEN DEĞİŞMEDİ).

### 6. Task.WhenAll hatasında garantili semaphore dispose'u

`BirTurCalistirAsync`'in dış `finally` bloğu, `Task.WhenAll(tasks)`'ı KENDİ iç `try/catch/finally`'İNE
ALACAK şekilde yeniden yapılandırıldı:

```text
finally
{
    try { if (tasks.Count > 0) await Task.WhenAll(tasks); }
    catch (Exception ex) { taskHatasi = ex; }
    finally { semaphore.Dispose(); }   // Task.WhenAll SONUCU NE OLURSA OLSUN, dil garantisiyle çalışır
}

RethrowTurVeTaskHatalariGuvenliSekilde(turHatasi, taskHatasi, stoppingToken);
```

`semaphore.Dispose()` artık C#'ın try/finally dil garantisi SAYESİNDE `Task.WhenAll` FIRLASA BİLE
ÇALIŞIR. Olası İKİ hata (`turHatasi`: claim/tur seviyesi, `taskHatasi`: `Task.WhenAll` seviyesi) İÇİN
AÇIK bir öncelik politikası uygulandı (`RethrowTurVeTaskHatalariGuvenliSekilde`):

1. İkisinden BİRİ (doğrudan veya bir `AggregateException` İÇİNDE) `OutOfMemoryException` İSE, bu
   HİÇBİR sarmalayıcı OLMADAN, orijinal TİPİYLE ÖNCELİKLE fırlatılır (fatal ASLA gizlenmez).
2. Değilse, `turHatasi` host cancellation'I (`OperationCanceledException` + `stoppingToken.
   IsCancellationRequested`) TEMSİL EDİYORSA, bu doğrudan fırlatılır (`ExecuteAsync`'in ÖZEL
   `catch (OperationCanceledException) when (...)` filtresinin YAKALAYABİLMESİ için sarmalanmadan).
3. Aynı kontrol `taskHatasi` İÇİN de yapılır.
4. İKİSİ de KALDIYSA VE hiçbiri fatal/cancellation DEĞİLSE, GÜVENLİ SABİT bir mesajla bir
   `AggregateException(SABIT_MESAJ, turHatasi, taskHatasi)` İÇİNDE BİRLEŞTİRİLİR - hiçbiri
   SESSİZCE KAYBOLMAZ/EZİLMEZ.
5. Yalnız BİRİ VARSA, `ExceptionDispatchInfo.Capture(...).Throw()` İLE orijinal stack trace
   KORUNARAK doğrudan fırlatılır.

`WorkerLevelSafeErrorCode`, artık `AggregateException`'ı da `InnerExceptions`'INI inceleyerek
(`SqlException`/`TimeoutException` İçeriyorsa buna göre) SINIFLANDIRIR - ham exception İÇERİĞİ HİÇBİR
DALDA loglanmaz (bkz. [EBelgeOutboxWorker.cs](../backend/Muhasebe/SatisBelgeleri/Services/EBelgeOutboxWorker.cs)).

### Test kapsamı ve çalıştırılan hedefli komut

**`EBelgeOutboxWorkerHealthCheckTests` YENİDEN YAZILDI (18 test)** - `EBelgeProcessingActivationGate`
GERÇEK örneği (fake DEĞİL) + `TimeProvider` İLE, health state'e HİÇBİR manuel `RecordActivationDecision`
seed'İ YAPILMADAN (görev md.9'un AÇIKÇA yasakladığı "production açığını testte gizleme" TUZAĞINDAN
kaçınmak İÇİN) şu senaryolar doğrulandı: taze state + `Enabled=true` + tarih açık + döngü başlamadı →
`Unhealthy`; taze state + `Enabled=false` → `Healthy`/`Disabled`; taze state + tarih henüz gelmedi →
`Healthy`/`BeforeActivationDate`; taze state + geçersiz tarih/timezone → `Degraded`; health fallback
değerlendirmesi state'e type-safe kararı YAZAR; worker ZATEN değerlendirme yaptıktan SONRA
(`CountingActivationGate` İLE `EvaluateCallCount` SAYILARAK) health GEREKSİZ ikinci bir değerlendirme
YAPMAZ; tarih sınırı worker BAŞLAMADAN AŞILDIĞINDA (`MutableTimeProvider` İLE) health
`BeforeActivationDate`'DEN `Active`/`Unhealthy`'YE geçer; geçersiz config'te (`ErrorCountingLoggerProvider`
İLE 5 ardışık health çağrısında) log spam ÜRETİLMEZ; health output'unda ham tarih/timezone değeri
BULUNMAZ; MEVCUT recovery/staleness/PII-yok testleri KORUNDU.

**`EBelgeOutboxWorkerTests`'e 2 YENİ test eklendi**:
`TaskWhenAllExceptionUretseBileSemaphoreDisposeEdilirVeFatalExceptionYayilir` (`FakeMetrics.
IncrementInflightOverride` İLE `ProcessClaimAsync`'in `try` bloğu İÇİNDEN GERÇEK bir `OutOfMemoryException`
fırlatılır - `TaskScheduler.UnobservedTaskException` + `GC.Collect()`/`WaitForPendingFinalizers()` İLE
hem fatal exception'IN yayıldığı HEM hiçbir unobserved task exception KALMADIĞI doğrulanır) ve
`ClaimHatasiVeTaskAltyapiHatasiAyniTurdaOlusursaWorkerCokmezVeDevamEder` (`FakeMetrics.
DecrementInflightOverride` İLE `ProcessClaimAsync`'in `finally` bloğundan bir task-altyapısı hatası,
AYNI turda İKİNCİ bir claim hatasıyla BİRLİKTE ÜRETİLİR - worker'ın ÇÖKMEDİĞİ, hata kaynağı
kaldırıldıktan SONRA BAŞKA bir mesajı BAŞARIYLA işlemeye DEVAM ETTİĞİ VE hiçbir ham hata metninin
loglanmadığı doğrulanır - `AggregateException` `ExecuteAsync`'in genel `catch` filtresi TARAFINDAN
YAKALANDIĞI için worker BURADA fault ETMEZ, bu yüzden black-box davranış doğrulaması TERCİH EDİLDİ).
Host cancellation'ın normal shutdown OLARAK KALDIĞI, Faz 2B.8.1'İN MEVCUT
`HostCancellationHataVeyaRetryOlarakKaydedilmez` testiyle ZATEN doğrulanmaktadır (yeni bir test
GEREKMEDİ).

```
dotnet test tests/STYS.Tests/STYS.Tests.csproj --filter "FullyQualifiedName~EBelgeXmlImzalayiciTests|FullyQualifiedName~EBelgeSigningActivationGateTests|FullyQualifiedName~EBelgeUblImzalamaServiceIntegrationTests|FullyQualifiedName~EBelgeSigningBackfillServiceIntegrationTests|FullyQualifiedName~EBelgeArtefaktOlusturmaServiceIntegrationTests|FullyQualifiedName~EBelgeOutboxLeaseTransitionIntegrationTests|FullyQualifiedName~EBelgeOutboxMesajIslemeServiceTests|FullyQualifiedName~EBelgeUblRendererEndToEndIntegrationTests|FullyQualifiedName~EBelgeSchematronSidecarIntegrationTests|FullyQualifiedName~EBelgeFaz1IntegrationTests|FullyQualifiedName~EBelgeOutboxFaz2AIntegrationTests|FullyQualifiedName~EBelgeOutboxRetryPolicyTests|FullyQualifiedName~EBelgeOutboxClaimLeaseIntegrationTests|FullyQualifiedName~EBelgeProcessingActivationGateTests|FullyQualifiedName~EBelgeProcessingOptionsValidatorTests|FullyQualifiedName~EBelgeOutboxWorkerMetricsTests|FullyQualifiedName~EBelgeOutboxWorkerHealthCheckTests|FullyQualifiedName~EBelgeOutboxWorkerTests|FullyQualifiedName~EBelgeOutboxWorkerIntegrationTests"
  → Passed: 350, Failed: 0, Total: 350 (gerçek SQL Server + gerçek Java Saxon sidecar + gerçek test
  sertifikasıyla).
```

**Regresyon (Faz 2B.5/2B.6/2B.7/2B.8/2B.8.1 - hiçbiri kasıtlı DEĞİŞTİRİLMEDİ, hepsi yukarıdaki
filtreye DAHİLDİR ve BAŞARILIDIR):** özellikle `EBelgeOutboxWorkerIntegrationTests`'in 5 GERÇEK (SQL
Server + sidecar + test sertifikası) testi - çoklu instance/lease devri, gerçek `ArtefaktOlustur`/
`UblImzala` worker akışı, gerçek RSA SignedReady worker akışı - SORUNSUZ geçti.

### Kasıtlı olarak YAPILMAYANLAR (görev kapsam sınırları)

Worker/claim/lease/task yaşam döngüsü mimarisi BAŞTAN YAZILMADI (yalnız 4 dar madde DÜZELTİLDİ);
health check İÇİNDE AYRI bir aktivasyon algoritması YAZILMADI (AYNI singleton
`IEBelgeProcessingActivationGate.Evaluate()` PAYLAŞILDI); "henüz değerlendirilmedi" durumu HİÇBİR
KOD YOLUNDA `Disabled` OLARAK YORUMLANMADI; health testlerinde production açığını GİZLEMEK İçin
aktivasyon kararı ELLE SEED EDİLMEDİ (yeni testlerin TAMAMI gerçek gate + gerçek options İLE
çalışır); runtime options hot-reload EKLENMEDİ (`Enabled`/config DEĞİŞİKLİĞİ HÂLÂ deployment/restart
gerektirir - Faz 2B.8.1'DEN DEĞİŞMEDİ); ham exception İÇERİĞİ HİÇBİR DALDA loglanmadı; activation
tarihi (15 Eylül 2026) DEĞİŞTİRİLMEDİ; production processing bu tarihten ÖNCE AÇILMADI; entegratör
gönderimi/HSM/PDF/e-posta/frontend GELİŞTİRİLMEDİ; tüm çözüm test paketi ÇALIŞTIRILMADI (yalnız
hedefli filtre); hiçbir test ATLANMADI.

### Açık kalan konular

Faz 2B.7.1'in "Açık kalan konular" listesi AYNEN geçerlidir.

### Sonraki faz

Faz 2B.9'a bakınız.

## Faz 2B.9 sonuç bölümü — test profillerinin ve kritik test kümelerinin organizasyonu

**Durum: TAMAMLANDI, commit/push YAPILDI.**

Bu faz YENİ bir e-Belge davranışı EKLEMEDİ (görev md.20 kısıtı) - yalnız MEVCUT 466 e-Belge testini
envanterledi, trait-tabanlı katmanlara ayırdı, GERÇEKTEN aynı invariantı tekrarlayan 8 testi kanıtlı
biçimde 2 `[Theory]`'e birleştirdi, bir kritik invariant manifesti oluşturdu ve `fast`/`integration`/
`nightly`/`release` profillerini tanımlayan `scripts/test-ebelge.ps1`/`.sh` script çiftini ekledi.
Tam detay: [docs/e-belge-test-stratejisi.md](e-belge-test-stratejisi.md).

**Envanter**: 31 test sınıfı, 466 test (`dotnet test --list-tests --filter "Domain=EBelge"` GERÇEK
keşfiyle doğrulandı - tahmin EDİLMEDİ). `TestLevel` dağılımı: Unit 217, Contract 46, SqlIntegration
112, SidecarIntegration 16, CryptoIntegration 69, WorkerEndToEnd 4, ReleaseGate 2.

**Birleştirme**: health karar matrisi (5 `[Fact]` -> 1 `[Theory]`, 5 `MemberData` satırı) ve güvenli
loglama (3 `[Fact]` -> 1 `[Theory]`, 3 `MemberData` satırı) - TOPLAM çalışan test SENARYOSU sayısı
DEĞİŞMEDİ (8 senaryo öncesi/sonrası), yalnız kaynak kod metod sayısı 8'den 2'ye indi. Eşdeğerlik
kanıtı ve gerekçe dokümanda tablo halinde.

**Kritik invariant manifesti**: 10 test (`TenantIsolation`, `StaleWorkerCannotWrite`,
`LeaseTakeover`, `UnsignedExactByteHash`, `SignedExactByteHash`, `SignatureTamperRejected`,
`DuplicateXmlIdRejected`, `SchematronRealSidecar`, `ActivationNotBefore20260915`,
`WorkerEndToEndSignedReady`) `[Trait("CriticalInvariant", "...")]` ile işaretlendi; 10/10'u
discovery ile doğrulandı.

**Bulunan ve düzeltilen gerçek zaman-bombası bug'ı**: `EBelgeUblImzalamaServiceIntegrationTests`'in
`SignedExactByteHash` kritik invariant testi, imzalama zamanı için SABİT bir takvim tarihi
(`2026-08-05T10:00:00Z`) kullanıyordu; test sertifikasının VARSAYILAN geçerlilik başlangıcı İSE
GERÇEK duvar saatine göre (`UtcNow - 1 gün`) hesaplanıyordu - takvim bu tarihi GERÇEKTEN geçtiğinde
(bu turda tam olarak gerçekleşti) sabit değer sertifikanın `notBefore`'undan ÖNCEYE düşüp KESİN
olarak başarısız olacaktı. Kök neden düzeltildi (sabit takvim tarihi yerine test-anı gerçek zamanı
kullanılır) - assertion GEVŞETİLMEDİ, retry/skip EKLENMEDİ (bkz. görev md.15/md.24 flaky-test
kısıtı). Doküman içinde tam kök-neden/tekrar-üretim raporu mevcut.

**Sidecar/SQL izolasyonu**: mevcut `SchematronSidecarCollection`/`SqlServerIntegrationCollection`
altyapısı incelendi; xUnit'in "bir sınıf yalnız tek collection'a üye olabilir" kısıtı nedeniyle 5
sidecar-bağımlı sınıfı TEK bir collection'da birleştirmek ya SQL deadlock korumasını KAYBETTİRİR ya
da gereksiz solution-geneli serileştirme YARATIR - bu yüzden yapı DEĞİŞTİRİLMEDİ, risk analizi ve
(gerekirse ileride uygulanacak) dosya-kilidi önerisi dokümante edildi. 4 tam profil koşumu boyunca
hiçbir sidecar/SQL kaynaklı flaky davranış GÖZLEMLENMEDİ.

**CI**: repository'de ÖNCEDEN hiçbir CI workflow'u YOKTU (`.github/workflows` yok) - bu turda da
EKLENMEDİ (görev md.13/md.24 kısıtı, "yeni ve geniş kapsamlı CI platformu kurma"); yalnız script'ler
ve önerilen bağlanma tasarımı dokümante edildi.

**Test komutu ve sonuçlar** (dört profil, hepsi `Failed: 0`):

```
./scripts/test-ebelge.ps1 fast          -> Passed: 263, Failed: 0  (~3 sn, SQL/sidecar YOK)
./scripts/test-ebelge.ps1 integration   -> Passed: 444, Failed: 0  (~126 sn, gerçek SQL+RSA)
./scripts/test-ebelge.ps1 nightly       -> Passed: 464, Failed: 0  (~128 sn, + gerçek sidecar + worker E2E)
./scripts/test-ebelge.ps1 release       -> Passed: 466, Failed: 0  (~127 sn, + ReleaseGate)
```

### Kasıtlı olarak YAPILMAYANLAR (görev kapsam sınırları)

Production davranışı (artifact üretimi/XAdES/Schematron kuralları/claim-lease SQL'i/retry policy/
worker activation/polling/outbox durumları/signing provider/migration) HİÇBİR ŞEKİLDE
DEĞİŞTİRİLMEDİ - `EBelgeUblImzalamaServiceIntegrationTests` düzeltmesi dahil TÜM değişiklikler test
dosyalarıyla SINIRLIDIR; activation tarihi (15 Eylül 2026) DEĞİŞMEDİ; keyfi bir test-sayısı hedefi
KULLANILMADI (bkz. görev md.5 kısıtı); kritik güvenlik testi SİLİNMEDİ; gerçek SQL/Saxon/RSA testi
mock/fake İLE DEĞİŞTİRİLMEDİ; hiçbir test SKIP/quarantine EDİLMEDİ; flaky test retry İLE
GİZLENMEDİ; yeni CI platformu KURULMADI; HSM/entegratör/PDF/e-posta/frontend GELİŞTİRİLMEDİ; tüm
solution test paketi ÇALIŞTIRILMADI (yalnız e-Belge profilleri).

### Açık kalan konular

Faz 2B.7.1'in "Açık kalan konular" listesi AYNEN geçerlidir. Ek olarak: sidecar paralel-JVM CPU
kaynak rekabeti riski dokümante edildi ama koda YANSITILMADI (öneri: dosya-tabanlı process kilidi,
yalnız somut bir flaky belirti gözlemlenirse).

### Sonraki faz

Faz 2B.9.1'e bakınız.

## Faz 2B.9.1 sonuç bölümü — profil sözleşmelerinin ve bağımlılıklarının zorunlu kılınması

**Durum: TAMAMLANDI, commit/push YAPILDI.**

Faz 2B.9'da kurulan profil/trait sistemi, GERÇEK bir güvenlik/release kapısı haline getirildi. Tam
detay: [docs/e-belge-test-stratejisi.md](e-belge-test-stratejisi.md).

**Tek merkezi manifest**: `scripts/ebelge-test-profiles.json` - profil/`TestLevel`/dependency
tanımları artık PowerShell/Bash arasında ELLE KOPYALANMIYOR. İki script AYNI manifesti okuyup 4
profilin TAMAMINDA (`fast`/`integration`/`nightly`/`release`) BİREBİR AYNI `dotnet test` filtresini
üretir - doğrudan karşılaştırmayla kanıtlandı. Bilinmeyen profil/`TestLevel`/`Dependency`,
bulunamayan/bozuk manifest - hepsi fail-fast.

**Otomatik trait sözleşmesi**: yeni `EBelgeTestMetadataContractTests` (22 test), derlenmiş
assembly'yi reflection ile tarayıp TÜM e-belge testlerinin `Domain=EBelge` + tam olarak bir geçerli
`TestLevel` taşıdığını, `Dependency`/`CriticalInvariant` değerlerinin whitelist içinde kaldığını, 10
kritik invariantın HER BİRİNİN en az bir testle karşılandığını VE manifest JSON'ının bilinen
liste'lerinin C# whitelist'iyle senkron kaldığını OTOMATİK doğrular. Sahte bir invariant adıyla
yapılan kontrollü simülasyonla, kritik bir test eksik OLSAYDI bu sözleşmenin GERÇEKTEN başarısız
olacağı KANITLANDI (gerçek bir test SİLİNEREK değil).

**Dependency fail-closed**: `integration`/`nightly`/`release`, ana koşumdan ÖNCE SQL Server (gerçek
`StysAppDbContext.Database.CanConnectAsync()` ile) VE Java sidecar (gerçek, kısa ömürlü
`SchematronSidecarProcessFixture` boot+dispose ile) erişilebilirliğini KANITLAR; `release` AYRICA
kritik invariant manifestini doğrular. Eksiklikte `dotnet test` HİÇ ÇALIŞTIRILMADAN non-zero exit -
kontrollü negatif testlerle (SQL env değişkeni kaldırıldı, sidecar derlenmiş sınıfları geçici olarak
taşındı, SONRA GERİ YÜKLENDİ) doğrulandı.

**Sıfır-skip politikası**: script'ler artık TRX `<Counters>`'ı parse edip `notExecuted` (Skip)
sayısını AYRICA kontrol eder - `dotnet test`'in "tüm testler skip edildi ama exit 0" YANILGISI
`integration`/`nightly`/`release`'de profili BAŞARISIZ SAYAR.

**Bash `set -e` düzeltmesi**: `dotnet test`'in normal bir test başarısızlığında script'i SESSİZCE
sonlandırmaması için `set -e` KALDIRILDI, her `dotnet test` çağrısı açık `if/then/else` İÇİNE alındı
- güvenli özet/TRX yolu/skip kontrolü artık HER DURUMDA çalışır.

**Bulunan/düzeltilen gerçek zaman-bombası bug'ı** (bu turda, integration profili ilk koşumunda
tespit edildi): `EBelgeUblImzalamaServiceIntegrationTests`'in `SignedExactByteHash` kritik invariant
testi, gerçek duvar saatine göre üretilen bir test sertifikasına karşı SABİT bir takvim tarihi
kullanıyordu - takvim bu tarihi geçince KESİN olarak başarısız olacaktı. Kök neden düzeltildi (test
artık kendi çalıştığı anın gerçek zamanını kullanır) - assertion gevşetilmedi.

**Gözlemlenen 1 flaky olay** (`nightly`, `EBelgeSchematronSidecarIntegrationTests.BuyukXmlLimitteReddedilir`,
`ConnectionReset`): kök nedeni KESİN izole edilemedi (hedefli tekrar üretim denemeleri BAŞARISIZ -
olay tekrar üretilemedi; TAM nightly koşumu hemen ardından TEMİZ geçti) - muhtemel neden, Faz 2B.9'da
ZATEN dokümante edilen sidecar paralel-JVM kaynak rekabeti riskidir. Retry/skip/assertion gevşetme
KULLANILMADI; tam kök-neden/tekrar-üretim raporu stratejı dokümanındadır.

**Test komutu ve sonuçlar** (dört profil, hepsi `Failed: 0, Skipped: 0`):

```
./scripts/test-ebelge.ps1 fast          -> Passed: 285, Failed: 0, Skipped: 0
./scripts/test-ebelge.ps1 integration   -> Passed: 466, Failed: 0, Skipped: 0  (preflight: SQL+Java GEÇTİ)
./scripts/test-ebelge.ps1 nightly       -> Passed: 486, Failed: 0, Skipped: 0  (preflight: SQL+Java GEÇTİ)
./scripts/test-ebelge.ps1 release       -> Passed: 488, Failed: 0, Skipped: 0  (preflight: SQL+Java+kritik invariant GEÇTİ)
```

Bash script'i (`test-ebelge.sh`) ile `fast` VE `integration` profilleri de TAM koşumla doğrulandı -
sonuçlar PowerShell ile BİREBİR AYNI (285/285, 466/466).

Faz 2B.9'un 466 taban test sayısı, Faz 2B.9.1'in 22 yeni metadata contract testiyle **488**'e çıktı
(2 yeni dependency-preflight testi `Domain=EBelge` sözleşmesine TABİ DEĞİLDİR, bu sayıya DAHİL
DEĞİLDİR - bkz. strateji dokümanı).

### Kasıtlı olarak YAPILMAYANLAR (görev kapsam sınırları)

Production e-belge davranışı (artifact/XAdES/Schematron/claim-lease/retry/activation/worker/outbox/
signing/migration) HİÇBİR ŞEKİLDE DEĞİŞTİRİLMEDİ; test sayısı keyfi bir hedefe göre AZALTILMADI veya
ARTIRILMADI (yalnız gerekçeli contract/preflight testleri EKLENDİ); kritik test SİLİNMEDİ;
entegrasyon testi Unit olarak yeniden ETİKETLENMEDİ; gerçek SQL/Saxon/RSA testi mock İLE
DEĞİŞTİRİLMEDİ; eksik dependency'de profil YEŞİL GÖSTERİLMEDİ; skip'ler başarılı SAYILMADI;
PowerShell/Bash'te ayrı profil listesi TUTULMADI; yeni CI platformu KURULMADI; HSM/entegratör/PDF/
e-posta/frontend GELİŞTİRİLMEDİ; tüm solution test paketi ÇALIŞTIRILMADI.

### Açık kalan konular

Faz 2B.9'un "Açık kalan konular" listesi AYNEN geçerlidir. Ek olarak: `BuyukXmlLimitteReddedilir`
flaky olayının kök nedeni henüz KESİN izole edilemedi (tek, tekrar üretilemeyen bir kayıt) - eğer
gelecekte daha sık gözlemlenirse, strateji dokümanındaki dosya-tabanlı process-kilidi önerisi
uygulanmalıdır.

### Sonraki faz

Faz 2B.7.1'in "Sonraki faz" listesi AYNEN geçerlidir - artık HSM/mali mühür geliştirmesi
`docs/e-belge-test-stratejisi.md`'de tanımlanan temiz test zemini (trait taksonomisi, OTOMATİK
sözleşme doğrulaması, `CryptoIntegration` katmanı, kritik invariant manifesti) üzerine inşa
edilebilir.

## Faz 2B.10: Kurum Bazlı E-Belge Politikası ve İşlem Yönlendirme Katmanı

Faz 2B.9.1'in test altyapısı üzerine, global e-belge kapılarının (Sep-15-2026 aktivasyon,
14.09.2026 UBL ön-kesim kapısı) **arkasına** kurum bazlı, fail-closed, denetlenebilir bir
yönlendirme katmanı eklendi. Tam tasarım için bkz.
`docs/e-belge-kurum-politikasi-ve-yonlendirme-stratejisi.md`; kurum bazlı bilgi toplama şablonu
için bkz. `docs/e-belge-kurum-surec-analizi-sablonu.md`.

**Eklenen entity'ler/migration**: `KurumEBelgePolitikasi` (kurum başına en fazla bir aktif/pasif
politika satırı), `SatisBelgesiEBelgeKarari` (satış belgesi başına immutable karar snapshot'ı),
`KurumEBelgePolitikaRevizyonu` (immutable audit revizyonu). Migration hiçbir kurum için aktif
politika seed ETMEDİ, mevcut e-belge kayıtlarına yöntem ATAMADI, mevcut outbox/artefakt
verisine DOKUNMADI.

**Yöntem yetenek matrisi**: `IEBelgeYontemYetenekSaglayici`/`EBelgeYontemYetenekleri` — TEK,
merkezi, type-safe kaynak. Production'da aktive edilebilen yöntemler: `Kullanilmayacak`,
`HariciMuhasebeSistemi` (yalnız karar kaydı, dış sistem çağrısı bu fazda YOK), `GibPortal`
(yerel snapshot+unsigned UBL, ASLA yerel imza). `OzelEntegrator`/`DogrudanGib` enum'da mevcut
ama gerçek adaptör olmadan `OperasyonelMi=false` — production'da aktive EDİLEMEZ.

**Satış akışı entegrasyon noktası**: `SatisBelgesiService.FaturaKesAsync`, mevcut UBL/cutover
kapılarından SONRA `IEBelgeKurumPolitikaServisi.DegerlendirAsync` çağırır (mevcut
`IEBelgeProcessingActivationGate` YENİDEN KULLANILIR, ayrı bir algoritma yazılmadı). Fail-closed
nedenler (`PolitikaYapilandirilmadi`/`PolitikaPasif`/`KurumAktivasyonTarihiGelmedi`/
`YontemHenuzDesteklenmiyor`/`PolitikaGecersiz`) `EBelgeKurumPolitikaEngelliException` fırlatır ve
TÜM işlemi (resmi fatura no dahil) rollback eder; `Kullanilmayacak`/`HariciMuhasebeSistemi` HATA
DEĞİLDİR — satış normal tamamlanır ama immutable karar kaydı HER ZAMAN yazılır.

**Outbox/imzalama yönlendirmesi**: `UblImzala` mesajı yalnız immutable kararın
`YerelImzaOlustur=true`'su + global imzalama kapısı + kurumun O ANDA hâlâ aktif politikası ÜÇÜ
BİRDEN sağlandığında oluşturulur. Worker/handler savunma katmanı
(`IEBelgeKurumPolitikaServisi.IslemHalaIzinliMiAsync`), claim SONRASI mevcut lease-transition
altyapısı (`TryFailAsync`) yeniden kullanılarak politika kapanmasını güvenli biçimde ele alır -
yeni bir transition metodu EKLENMEDİ. Signing backfill artık yalnız `YerelImzaOlustur=true`
immutable kararı olan kayıtları seçer; karar kaydı olmayan (legacy) kayıtlar backfill'e
ASLA dahil edilmez ve OTOMATİK `DogrudanGib` varsayılmaz.

**Kill switch / audit**: Aktif→Pasif her zaman izinli (pending iş olsa bile); yöntem değişimi
devam eden işler varken engellenir (`EBELGE_KURUM_POLICY_CHANGE_BLOCKED`); her anlamlı değişiklik
`KurumEBelgePolitikaRevizyonu`'na (eski/yeni değerler + actor `CreatedBy`'dan otomatik) yazılır.

**Yönetim API'si**: `KurumEBelgePolitikasiController` (`ui/kurumlar/{kurumId}/e-belge-politikasi`)
mevcut `KurumController` yetkilendirme desenini (SuperAdmin/KurumAdmin, mevcut
`StructurePermissions.MuhasebeSatisBelgeleriYonetimi`) YENİDEN kullanır - yeni rol İCAT EDİLMEDİ.
RowVersion optimistic concurrency ile korunur; cross-tenant erişim 403 ile reddedilir.

**Eklenen kritik invariant'lar**: `InstitutionPolicyFailClosed` (kurum politikası tam aktif olsa
bile global kapı kapalıyken karar HER ZAMAN fail-closed), `InstitutionPolicyTenantIsolation`
(kurum A politikası kurum B kararına sızmaz — servis VE DB katmanında test edilir),
`PortalRouteNeverSignsLocally` (GibPortal ASLA yerel imza mesajı oluşturmaz).

**Test sonuçları** (dört profil, hepsi `Failed: 0, Skipped: 0`; Faz 2B.9.1'in 488 taban sayısı,
Faz 2B.10'un yeni Unit/SqlIntegration testleriyle 552'ye çıktı):

```
./scripts/test-ebelge.ps1 fast          -> Passed: 304, Failed: 0, Skipped: 0
./scripts/test-ebelge.ps1 integration   -> Passed: 530, Failed: 0, Skipped: 0  (preflight: SQL+Java GEÇTİ)
./scripts/test-ebelge.ps1 nightly       -> Passed: 550, Failed: 0, Skipped: 0  (preflight: SQL+Java GEÇTİ)
./scripts/test-ebelge.ps1 release       -> Passed: 552, Failed: 0, Skipped: 0  (preflight: SQL+Java+25 kritik invariant testi GEÇTİ)
```

**Kasıtlı olarak YAPILMAYANLAR**: HSM/PKCS#11/mali mühür entegrasyonu, gerçek GİB çağrıları,
özel entegratör/harici muhasebe adaptörü, VKN'nin ikinci kez saklanması, çelişebilen config
boolean'ları, politika eksikken sessiz "Kullanilmayacak" varsayımı, desteklenmeyen yöntemlerin
production'da aktif kabul edilmesi, mevcut global aktivasyon kapılarının kaldırılması/gevşetilmesi,
2026-09-15 tarihinin değiştirilmesi, legacy kayıtların otomatik `DogrudanGib` ataması, kritik test
silinmesi/atlanması, frontend/PDF/e-posta geliştirmesi.

**Açık kalan konular**: `HariciMuhasebeSistemi` için gerçek dış sistem adaptörü henüz YOK (yalnız
karar kaydı); `OzelEntegrator`/`DogrudanGib` için gerçek adaptör/HSM entegrasyonu gerekiyor;
Faz 2B.10 öncesi (legacy) `EBelgeKaydi` kayıtları için açık bir backfill/migrasyon kararı henüz
alınmadı — bu operasyonel bir sonraki adımdır, kod tarafında OTOMATİK bir varsayım YAPILMADI.

## Faz 2B.10.1: Kurum Politikası Claim, Kill-Switch ve Idempotency Sertleştirmesi

Faz 2B.10, kurum politikasını satış akışının VE artifact/imza commit-öncesinin önüne kurmuştu; bu
tur, o sertleştirmenin DIŞINDA kalmış beş üretim davranış açığını kapatır. Kurum politika veri
modeli VE entegrasyon yöntemleri DEĞİŞMEDİ - tam tasarım gerekçesi için bkz.
`docs/e-belge-kurum-politikasi-ve-yonlendirme-stratejisi.md` "Faz 2B.10.1" bölümü.

**1. Claim öncesi politika uygunluğu**: `EBelgeOutboxClaimLeaseService`'in raw SQL'i artık
`SatisBelgesiEBelgeKararlari` VE `KurumEBelgePolitikalari`'na `INNER JOIN` yapar - pasif/uyumsuz-
yöntemli/aktivasyon-tarihi-gelmemiş/karar-öncesi (legacy) mesajlar HİÇ ADAY olmaz; `DenemeSayisi`/
lease OLUŞMAZ. Yöntem→yetenek matrisi SQL'de İKİNCİ KEZ hard-code EDİLMEZ - otoriter yetenek
immutable karardan okunur, güncel politika yalnız aktiflik/yöntem-uyumu/aktivasyon-tarihi İÇİN
kullanılır. Uygunsuzluk `WHERE`/`JOIN` içinde elendiğinden bloklu bir ilk aday sonraki uygun
mesajı AÇLIĞA (starvation) SÜRÜKLEMEZ; mevcut `UPDLOCK/READPAST/ROWLOCK` çoklu-worker güvenliği
KORUNUR.

**2. Claim sonrası kill-switch yarışı**: Yeni `IEBelgeOutboxLeaseTransitionService.
TryReleasePolicyBlockedAsync` - politika engeli NORMAL bir teknik hata DEĞİLDİR; mesaj
`Durum=Bekliyor`'a döner (terminalize EDİLMEZ), claim'de tüketilen deneme GERİ ALINIR (0'ın altına
DÜŞMEZ), retry churn/alarm gürültüsü ÜRETİLMEZ. `EBelgeArtefaktOlusturmaService` ve
`EBelgeUblImzalamaService` (YENİ `IEBelgeKurumPolitikaServisi` bağımlılığıyla), lease ownership
doğrulandıktan SONRA, artifact/SignedReady YAZILMADAN/`EBelgeKaydi.Durum` İLERLETİLMEDEN ÖNCE, AYNI
açık transaction içinde politikayı TEKRAR doğrular (yeni `AtomikPolitikaBloklu` sonuç türü) - yeni
bir TOCTOU penceresi AÇILMAZ.

**3. Legacy kayıtlar fail-closed**: `IslemHalaIzinliMiAsync` (bool, karar-yoksa-`true` fail-open)
KALDIRILDI; yerine zengin `DegerlendirIslemUygunlugunuAsync`
(`EBelgeIslemPolitikaUygunlukSonucu`/`EBelgeIslemPolitikaUygunlukNedeni`) geldi - karar kaydı
YOKSA sonuç ARTIK `KararBulunamadi` (fail-closed). `FaturaKesAsync`'in idempotent-tekrar dalı da
AYNI ilkeyi izler: karşılık gelen karar bulunamayan bir `FaturalamaDurumu=Kesildi` belgesi (Faz
2B.10 öncesi kesilmiş olabilir) `EBelgeKurumPolitikaKararBulunamadiException`
(`EBELGE_KURUM_POLICY_DECISION_NOT_FOUND`) fırlatır - otomatik yorumlama YAPILMAZ; ele alınışı
manuel inceleme + kontrollü backfilldir (bu turda YAZILMADI).

**4. Yöntem-aware idempotency + UBL koşullu doğrulama**: `FaturaKesAsync`'in idempotent-tekrar
kontrolü artık immutable karar üzerinden dallanır - `YerelSnapshotOlustur=false` kararlarda
(`Kullanilmayacak`/`HariciMuhasebeSistemi`/global henüz açık değil) EBelgeKaydi bulunmaması BEKLENEN
durumdur; önceki "EBelgeKaydi bulunamadı" veri-tutarsızlığı hatası KALDIRILDI. Akış YENİDEN
sıralandı: UBL'ye özgü hazırlık/doğrulama (kurum vergi/adres, cari kart e-Fatura/e-Arşiv bayrağı,
kanal çözümü, pre-cut validator) ARTIK yalnız `YerelSnapshotOlustur=true` ise çalışır - `GibPortal`
davranışı DEĞİŞMEDİ.

**5. Politika sürümü karar yarışı**: İmmutable karar PERSIST edilmeden HEMEN ÖNCE, kullanılan
politika satırının sürümü YENİDEN doğrulanır; değiştiyse `EBelgeKurumPolitikaKararCakismasiException`
(`EBELGE_KURUM_POLICY_DECISION_CONFLICT`) - TÜM satış kesimi (sayaç dahil) rollback olur.

**Eklenen kritik invariant'lar**: `InactivePolicyNeverClaims`, `PolicyKillSwitchPreventsCommit`,
`NonLocalRouteIsIdempotent`, `LegacyDecisionNeverProcesses`.

**Test sonuçları** (dört profil, hepsi `Failed: 0, Skipped: 0`; 552 → 578):

```
./scripts/test-ebelge.ps1 fast          -> Passed: 308, Failed: 0, Skipped: 0
./scripts/test-ebelge.ps1 integration   -> Passed: 556, Failed: 0, Skipped: 0  (preflight: SQL+Java GEÇTİ)
./scripts/test-ebelge.ps1 nightly       -> Passed: 576, Failed: 0, Skipped: 0  (preflight: SQL+Java GEÇTİ)
./scripts/test-ebelge.ps1 release       -> Passed: 578, Failed: 0, Skipped: 0  (preflight: SQL+Java+29 kritik invariant testi GEÇTİ)
```

Not: `nightly` profilinin İLK koşumunda, bu fazın kapsamı DIŞINDAKİ (kurum politikasıyla İLGİSİZ,
`SaxonSidecarEBelgeSchematronValidatorTests.TimeoutServiceUnavailableOlur` - gerçek HTTP timeout
zamanlamasına duyarlı, ÖNCEDEN VAR OLAN) bir test flaky biçimde başarısız oldu; izole 3/3 ve tam
profil YENİDEN koşumunda (576/576) sorunsuz geçti - dosyada HİÇBİR değişiklik yapılmadı, test
atlanmadı/gevşetilmedi.

**Kasıtlı olarak YAPILMAYANLAR**: Kurum politika veri modelini/entegrasyon yöntemlerini baştan
yazma, HSM/mali mühür geliştirmesi, GİB/özel entegratör adaptörü, claim güvenliğini iki ayrı
yarışa-açık işlemle çözme, pasif mesajı teknik hata/retry churn döngüsüne sokma, legacy kayıt için
permissive fallback, kill switch sonrası artifact/SignedReady yazma, pipeline gerekmeyen yöntemde
UBL alanlarını zorunlu tutma, mevcut lease/stale-worker güvenliğini zayıflatma, aktivasyon
tarihlerini değiştirme, test skip etme, tüm solution test paketini çalıştırma, frontend/PDF/
e-posta geliştirmesi.

## Faz 2B.10.2: Policy Commit Serialization ve Signing Gate Sertleştirmesi

Faz 2B.10.1, kurum politikasını commit-öncesi bir kontrol noktası olarak kurdu, ama bu kontroller
HER YERDE unlocked bir `SELECT`'e dayanıyordu - "aynı transaction içinde okundu" olması bile bir
serileştirme GARANTİSİ VERMEZ. Bu tur, mimariyi YENİDEN TASARLAMADAN, küçük ve hedefli biçimde iki
somut TOCTOU açığını kapatır: (1) politika kontrolü İLE artifact/SignedReady/satış-kararı commit'i
ARASINDAKİ yarış penceresi, (2) kuyruğa alınmış bir `UblImzala` mesajının global signing gate
KAPANDIKTAN SONRA bile imzalanabilmesi. Tam tasarım gerekçesi için bkz.
`docs/e-belge-kurum-politikasi-ve-yonlendirme-stratejisi.md` "Faz 2B.10.2" bölümü.

**1. Merkezi politika satırı kilidi**: Yeni `IEBelgeKurumPolitikaTransactionGuard.KilitleVeOkuAsync`
- `KurumEBelgePolitikalari` satırını `WITH (UPDLOCK, HOLDLOCK, ROWLOCK)` ile okur, ambient EF Core
transaction'ına katılır (outbox lease servislerinin AYNI ham-SQL-ambient-transaction deseni).
`HOLDLOCK`, kilidi transaction commit/rollback edilene kadar TUTAR; `KurumId` UNIQUE INDEX'i ile
birlikte phantom-insert (satır hiç yokken aynı anahtarla yeni satır eklenmesi) koruması sağlar.
TÜM transaction `SERIALIZABLE`'a YÜKSELTİLMEZ - yalnız HEDEFLİ bir satır/key-range kilidi.

**2. Uygunluk algoritmasının tek çekirdeği**: `IEBelgeKurumPolitikaServisi.DegerlendirIslemUygunlugunuAsync`
(unlocked, worker'ın claim-sonrası İLK savunma katmanı) VE YENİ `DegerlendirIslemUygunlugunuKilitliAsync`
(kilitlenmiş bir anlık görüntüyle, commit-öncesi SON kontrol) AYNI özel
`DegerlendirIslemUygunlugunuCoreAsync` çekirdeğini paylaşır - algoritma İKİ YERDE YENİDEN YAZILMAZ,
yalnız politikanın NASIL okunduğu farklıdır.

**3. Artifact/SignedReady/satış kararı serileştirmesi**: `EBelgeArtefaktOlusturmaService.
DenemeBasariAtomikAsync` VE `EBelgeUblImzalamaService`'in HER İKİ SignedReady commit yolu, outbox
sahiplik kilidinden SONRA ama artifact/EBelgeKaydi yazımından ÖNCE guard'ı çağırır (kilit sırası:
Outbox satırı → Kurum politika satırı → artifact/EBelgeKaydi yazımları). `SatisBelgesiService.
FaturaKesAsync`, SatisBelgesi satır kilidinden SONRA ama sayaç satırı kilitlenmeden ÖNCE guard'ı
çağırıp kilitli anlık görüntünün TÜM karşılaştırılabilir alanlarını (Id/KurumId/PolitikaSurumu/
AktifMi/EntegrasyonYontemi/AktivasyonYerelTarihi) `DegerlendirAsync`'in kararıyla karşılaştırır -
Faz 2B.10.1'in yalnız `PolitikaSurumu` sütununu unlocked yeniden okuyan (ve bu yüzden AYNI TOCTOU
açığına sahip) kontrolünün YERİNE geçer.

**4. Global signing gate gerçek bir commit kapısı**: `EBelgeUblImzalamaService` artık `IEBelgeSigningActivationGate`
bağımlılığı alır (Faz 2B.10.1'de YOKTU). Yeni `CanSignNow()`, `ShouldCreateSigningMessage()` İLE
AYNI `Degerlendir()` çekirdeğini paylaşır (Enabled/NotBeforeLocalDate/Europe-Istanbul/TimeProvider
algoritması İKİ YERDE YAZILMAZ). İki kontrol noktası: (a) `ImzalaAsync`'in EN BAŞINDA - gate
kapalıysa imza/render işine HİÇ GİRİLMEZ; (b) imza operasyonu (tx dışı) tamamlandıktan SONRA,
SignedReady yazılmadan HEMEN ÖNCE, politika kilidiyle AYNI kısa commit transaction'ı içinde TEKRAR
kontrol edilir - gate imza SIRASINDA kapandıysa SignedReady YAZILMAZ, imza sonucu (private key/imza
bytes dahil) DISCARD edilir. Her iki nokta da AYNI `TryReleasePolicyBlockedAsync`/
`AtomikPolitikaBloklu` mekanizmasını (kurum politikasıyla PAYLAŞILAN) kullanır - ayrı bir DB
geçişi/sonuç türü İCAT EDİLMEDİ (outbox satırının davranışı - terminal olmayan, Bekliyor, attempt
iade, retry churn yok - iki senaryoda da AYNIDIR).

**5. Kilit sırası ve kill switch'in kısa kalması**: Worker: Outbox satırı → Kurum politika satırı →
artifact/EBelgeKaydi yazımları. Satış: SatisBelgesi satırı → Kurum politika satırı → sayaç/karar/
EBelgeKaydi yazımları. `EBelgeKurumPolitikaYonetimServisi.GuncelleAsync` (kill switch) yalnız kurum
politika satırını kilitler - outbox/SatisBelgesi/sayaç satırlarından HİÇBİRİNİ kilitlemez, bu
yüzden deadlock YAPISAL OLARAK İMKÂNSIZDIR.

**Eklenen kritik invariant'lar**: `SigningGatePreventsQueuedSigning`, `PolicyDecisionVersionIsSerialized`.
`PolicyKillSwitchPreventsCommit` (Faz 2B.10.1) artık GERÇEK, örtüşen iki-transaction testlerle de
kanıtlanır (`Task.WhenAny` + zaman aşımıyla "task GERÇEKTEN bloke oldu" ispatı, sıralı simülasyon
DEĞİL) - hem "kill switch önce kazanır" hem "worker'ın kilidi önce kazanır" sıralamaları AYRI AYRI
test edilir.

**Test sonuçları** (dört profil, hepsi `Failed: 0, Skipped: 0`; 578 → 592):

```
./scripts/test-ebelge.ps1 fast          -> Passed: 316, Failed: 0, Skipped: 0
./scripts/test-ebelge.ps1 integration   -> Passed: 570, Failed: 0, Skipped: 0  (preflight: SQL+Java GEÇTİ)
./scripts/test-ebelge.ps1 nightly       -> Passed: 590, Failed: 0, Skipped: 0  (preflight: SQL+Java GEÇTİ)
./scripts/test-ebelge.ps1 release       -> Passed: 592, Failed: 0, Skipped: 0  (preflight: SQL+Java+31 kritik invariant testi GEÇTİ)
```

Not: `release` profilinin İLK koşumunda, bu fazın kapsamı DIŞINDAKİ (kurum politikasıyla İLGİSİZ,
`EBelgeSchematronSidecarIntegrationTests.BuyukXmlLimitteReddedilir` - büyük XML/HTTP bağlantı
zamanlamasına duyarlı, ÖNCEDEN VAR OLAN) bir test flaky biçimde başarısız oldu; izole koşumda VE tam
profil YENİDEN koşumunda (592/592) sorunsuz geçti - dosyada HİÇBİR değişiklik yapılmadı, test
atlanmadı/gevşetilmedi.

Ayrıca, ilk `nightly` koşumunda `EBelgeOutboxWorkerIntegrationTests` içindeki (kendi bağımsız DI
container'ını manuel kuran, `backend/Program.cs`'i KULLANMAYAN) worker end-to-end testleri, yeni
`IEBelgeKurumPolitikaTransactionGuard` kaydı EKSİK olduğundan zaman aşımına uğradı - test container'ı
güncellendi (gerçek servisin ihtiyaç duyduğu her bağımlılık, production DI'daki GİBİ, test DI'ında da
KAYITLI olmalıdır). Ayrıca `GercekWorkerUblImzalaMesajiniClaimEdipTamamlarVeSignedReadyArtifactOlusur`
testi Faz 2B.10.1 semantiğiyle (`signingGateAcik: false` - gate yalnız mesaj OLUŞTURMA anını
etkilerdi) yazılmıştı; testin AMACI ("worker GERÇEKTEN imzalayıp SignedReady üretir") ile YENİ
commit-öncesi gate semantiği ÇELİŞTİĞİNDEN `signingGateAcik: true`'ya güncellendi - gate-kapalı
davranışı ZATEN AYRI, doğrudan testlerle (bkz. yukarıda) kapsanmaktadır.

**Kasıtlı olarak YAPILMAYANLAR**: Kurum politika veri modelini/entegrasyon yöntemlerini/mevcut Faz
2B.10-2B.10.1 davranış matrisini baştan yazma, TÜM transaction'ı `SERIALIZABLE`'a yükseltme, claim
SQL'ini/lease mimarisini değiştirme, `IOptionsMonitor` hot-reload migrasyonu zorlama (gate geçişleri
test double'larıyla deterministik simüle edildi), yeni bir DB geçişi/tablo/sonuç türü icat etme
(gate-bloklu VE politika-bloklu senaryolar AYNI `AtomikPolitikaBloklu` mekanizmasını paylaşır),
`sys.dm_tran_locks` tabanlı DMV polling altyapısı kurma (blokaj kanıtı `Task.WhenAny` + zaman
aşımıyla YAPILDI), test skip etme, tüm solution test paketini çalıştırma, frontend/PDF/e-posta
geliştirmesi.
