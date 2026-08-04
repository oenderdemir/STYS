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

### Frontend hâlâ yapılmadı

Faz 2B.4.2'den beri açık: otoriter satıcı/alıcı yapısal adres (sokak, bina no, ilçe, il, posta
kodu) ve gerçek kişi alıcılar için ayrı ad/soyad alanları hâlâ UI'da girilemiyor. Renderer artık
bu alanları DOĞRUDAN TÜKETTİĞİNDEN (bkz. `EBelgeUblRenderer.ValidateAuthoritativeFields`), bu
eksik olmadan `EBelgeUblOptions.Enabled=true` hiçbir üretim ortamında pratikte kullanılamaz.
Gereken ekranlar: Kurum ayarları (satıcı yapısal adres), CariKart/Müşteri formu (alıcı yapısal
adres + gerçek kişi ad/soyad ayrımı, kurumsal/gerçek kişi seçimine göre koşullu alanlar).
