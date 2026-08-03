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
