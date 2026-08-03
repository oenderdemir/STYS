# E-Belge UBL/PDF/E-Posta Renderer Hazırlık Raporu (Faz 2B.5 — Üçüncü Düzeltme)

Bu rapor, `2002536` sürümünün mimari ve sözleşmesel eksikler nedeniyle kabul edilmemesi üzerine
güncellenmiştir. Resmî UBL-TR1.2.1 paketi, e-Arşiv raporu/faturası ayrımı, UserList, `Adet → C62`,
standart KDV kodu ve dar satış faturası kapsamına ilişkin doğrulanmış araştırmalar kabul edilmiş
sayılmakta ve tekrar edilmemektedir. Bu turda yalnızca aşağıdaki başlıklar düzeltilmiştir:
renderer sözleşmesi, V1/V2 reader geriye uyumluluğu, eşleme matrisinin atomikliği, indirim/parasal
toplam eşlemesi, imza sınırı, determinizm sözleşmesi, 14.09.2026 yürürlük kararı ve kesim öncesi
kapı sözleşmesi.

## Kesin ürün kararı: devreye alma tarihi

Fatura işlemleri **14.09.2026 tarihinden önce hiçbir ortamda canlı kullanıma alınmayacaktır.**
Canlıya geçiş, 14.09.2026'da yürürlüğe giren yeni GİB paketleri ve kuralları yürürlüğe girdikten
sonra yapılacaktır. Bu karar bu raporun tamamına uygulanmıştır:

- 14.09.2026 öncesinde geçerli **eski GİB paketleri için renderer desteği geliştirilmeyecektir.**
- Eski ve yeni rule-set arasında **tarih bazlı seçim yapılmayacaktır**; tarih bazlı rule-set
  registry önerisi bu rapordan kaldırılmıştır.
- Eski paketlerin bulunması veya hash'lerinin çıkarılması bu faz için gerekli değildir ve açık
  konu olarak bırakılmamıştır.
- Renderer **yalnız 14.09.2026'da yürürlüğe giren yeni GİB kural setini** destekleyecektir.
- Belge tarihi veya fatura kesim tarihi 14.09.2026'dan önce olan belgeler, **resmî numara
  verilmeden** kesim öncesi kapıda reddedilecektir (§9).
- Özellik, canlıya geçiş tarihine kadar konfigürasyon/feature flag ile **kapalı** tutulacaktır.
- Runtime sırasında GİB sitesinden **paket indirilmeyecektir**; paket build/deployment artifact'ı
  olarak sabitlenecektir.

## 1. İncelenen GİB kaynakları ve sürümleri

Renderer'ın destekleyeceği **tek** kural seti, 27.07.2026 tarihinde yayımlanan ve 14.09.2026
tarihinde yürürlüğe giren settir. Bu setin kimliği ve sabitlenecek dosyaları:

**Rule-set kimliği:** `GIB-UBL-TR-1.2.1/2026-09-14`

| Paket | Boyut (byte) | SHA-256 |
| --- | --- | --- |
| `UBL-TR1.2.1_Paketi.zip` | 1052004 | `cb583941b8a8a239c59902c6bc455c0f75d48f2bb81d7d3fbe1ae827f981f7db` |
| `e-FaturaPaketi.zip` | 678554 | `e0fd9136cadbb79bd29f286c7ff80c6f2202ce1a0354338d0bf0739dd88dc29e` |
| `UBLTR_1.2.1_Kilavuzlar.zip` | 5868477 | `0f7c720da5d9f0e9d25ef929f03d1ecd04871bda924ecb1a6b71b5e8fba0710a` |
| `earsiv_paket_v1.1_8.zip` | 18701 | `07a00ddaf98a2b3ec1ef9beb8a90d19133b211045f25d2e67279bd509be9f75f` |

Rule-set içinde renderer tarafından fiilen kullanılacak dosyalar:

- `UBL-TR1.2.1_Paketi.zip` → `xsdrt/maindoc/UBL-Invoice-2.1.xsd`,
  `xsdrt/common/UBL-CommonAggregateComponents-2.1.xsd`,
  `xsdrt/common/UBL-CommonBasicComponents-2.1.xsd`,
  `xsdrt/common/UBL-CommonExtensionComponents-2.1.xsd` ve bunların transitive import'ları
- `e-FaturaPaketi.zip` → `schematron/UBL-TR_Main_Schematron.xml`,
  `schematron/UBL-TR_Common_Schematron.xml`, `schematron/UBL-TR_Codelist.xml`
- `earsiv_paket_v1.1_8.zip` → yalnız e-Arşiv **raporlama** akışı için (`eArsiv.xsd`); tekil e-Arşiv
  faturası UBL-TR'dir ve bu paketin fatura ile ilgisi yoktur

**Sabitleme kuralı:** Bu dosyalar uygulama repository'sine/deployment artifact'ına gömülecek,
build sırasında SHA-256 değerleri doğrulanacak ve runtime'da GİB sitesine hiçbir çağrı
yapılmayacaktır. Rule-set, renderer implementasyonuna immutable teknik konfigürasyon olarak
enjekte edilir; canlı internet veya veritabanından okunmaz.

## 2. Repository'deki mevcut durum

Ticari otorite `SatisBelgesi`, immutable snapshot `EBelgeSnapshot`, kanonik okuyucu
`EBelgeCanonicalSnapshotReader`, kesim akışı `SatisBelgesiService.FaturaKesAsync`.

**Kesim öncesi kapı zaten mevcuttur — yeni bir kavram değildir.** Önceki raporlar bunu sıfırdan
eklenecek bir bileşen gibi sunmuştu; bu yanlıştır. `SatisBelgesiService.FaturaKesAsync` içinde
`EnsureUblHazirlikKaynaklari(belge, tesis.Kurum)` çağrısı `SatisBelgesiService.cs:1116`
satırındadır ve tam olarak doğru noktadadır:

- **Öncesinde** (satır 872-1114): belge kilitli okunmuş, tesis ve kurum otoriter olarak okunmuş,
  muhasebe fişi doğrulanmıştır.
- **Sonrasında** (satır 1141+): sayaç `UPDLOCK` ile kilitlenir (1141), sıra numarası artırılır
  (1168-1169), resmî numara yazılır (1174), kesim tarihi atanır (1175-1176), otoriter durumlar
  değiştirilir (1177-1181), `EBelgeKaydi` oluşturulur (1188), snapshot üretilir (1198), outbox
  mesajı eklenir (1209).

`EnsureUblHazirlikKaynaklari` (satır 1275-1311) bugün şunları doğrulamaktadır: `Kurum.VergiNo`
dolu, `Kurum.VergiDairesi` dolu, `Kurum.Adres` dolu, `ParaBirimi == "TRY"`, `Kur == 1`. Yani dar
kapsamın para birimi ve kur kısıtı **zaten yürürlüktedir**. Faz 2B.4.1'de yapılacak iş, bu mevcut
metodu genişletmektir; yeni bir kapı inşa etmek değildir.

**Düzeltilmesi gereken sıralama hatası:** `ResolveEBelgeKanali(cariKart)` çağrısı bugün satır
1186'dadır — yani sayaç artırıldıktan, resmî numara verildikten ve belge durumu değiştirildikten
**sonra**. Kapının kanal bazlı kontrol yapabilmesi için (`ProfileID` kaynağı, e-Arşiv/e-Fatura
ayrımı) kanal çözümlemesi sayaç kilidinden **önceye**, `EnsureUblHazirlikKaynaklari` ile aynı
bloğa taşınmalıdır. Aksi halde kanalı desteklenmeyen bir belge önce resmî numara tüketir, sonra
reddedilir.

Diğer ilgili gözlemler:

- `EBelgeUuid`, kesim anında `Guid.NewGuid()` ile üretilip snapshot'a dondurulur (satır 1193);
  renderer UUID üretmez.
- `FaturaKesimTarihi`, kesim anında `DateTime.UtcNow` ile dondurulur (satır 1175); renderer
  güncel saat okumaz.
- `SatisBelgesiSatiri.Birim` alanı serbest metindir ve varsayılan değeri literal `"Adet"`tir
  (`SatisBelgesiSatiri.cs:27`).
- `Kurum.Adres` tek `string?` alanıdır; `Il`, `Ilce`, `Ulke`, `PostaKodu` alanları **yoktur**
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
| Customer `cac:PartyIdentification` | `Alici.MusteriVergiNo` veya `Alici.MusteriTcKimlikNo` | Zorunlu | `schemeID=VKN`/`TCKN` | Doğrudan kullanılabilir |
| Supplier `cac:PartyName` | `Kurum.KurumUnvani` | Zorunlu | — | Doğrudan kullanılabilir |
| Customer `cac:PartyName` | `Alici.MusteriUnvan` | Zorunlu | — | Doğrudan kullanılabilir |
| Supplier `cac:PostalAddress` | V2 yapısal adres | Zorunlu | `AddressType` | Otoriter kaynak eksik |
| Customer `cac:PostalAddress` | V2 yapısal adres | Zorunlu | `AddressType` | Otoriter kaynak eksik |
| Supplier `cac:PartyTaxScheme` | `Kurum.VergiDairesi` | Opsiyonel | — | Doğrudan kullanılabilir |
| Customer `cac:PartyTaxScheme` | `Alici.MusteriVergiDairesi` | Opsiyonel | — | Doğrudan kullanılabilir |
| `cac:Person/cbc:FirstName` | V2 `MusteriAd` | Zorunlu | — | Otoriter kaynak eksik |
| `cac:Person/cbc:FamilyName` | V2 `MusteriSoyad` | Zorunlu | — | Otoriter kaynak eksik |
| Belge düzeyi `cac:AllowanceCharge` | — | Opsiyonel | — | İlk sürümde destek dışı bırakılmalı |
| `cac:InvoiceLine/cac:AllowanceCharge` | `Satir.IndirimTutari` | Opsiyonel | `ChargeIndicator=false` | Deterministik eşlenebilir |
| `cac:TaxTotal/cbc:TaxAmount` | `ToplamKdv` | Zorunlu | — | Deterministik eşlenebilir |
| `cac:TaxSubtotal/cbc:TaxableAmount` | Orana göre gruplanmış matrah | Opsiyonel | — | Deterministik eşlenebilir |
| `cac:TaxSubtotal/cbc:TaxAmount` | Orana göre gruplanmış KDV | Zorunlu | — | Deterministik eşlenebilir |
| `cac:TaxCategory/cbc:Percent` | `Satir.KdvOrani` | Opsiyonel | — | Doğrudan kullanılabilir |
| `cac:TaxScheme/cbc:TaxTypeCode` | Renderer sabiti | Opsiyonel | `0015` | Deterministik eşlenebilir |
| `cac:LegalMonetaryTotal/cbc:LineExtensionAmount` | `ToplamMatrah` | Zorunlu | — | Deterministik eşlenebilir |
| `cac:LegalMonetaryTotal/cbc:TaxExclusiveAmount` | `ToplamMatrah` | Zorunlu | — | Deterministik eşlenebilir |
| `cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount` | `GenelToplam` | Zorunlu | — | Deterministik eşlenebilir |
| `cac:LegalMonetaryTotal/cbc:AllowanceTotalAmount` | — | Opsiyonel | — | İlk sürümde destek dışı bırakılmalı |
| `cac:LegalMonetaryTotal/cbc:ChargeTotalAmount` | — | Opsiyonel | — | İlk sürümde destek dışı bırakılmalı |
| `cac:LegalMonetaryTotal/cbc:PayableAmount` | `GenelToplam` | Zorunlu | — | Deterministik eşlenebilir |
| `cac:InvoiceLine/cbc:ID` | `Satir.SiraNo` | Zorunlu | — | Deterministik eşlenebilir |
| `cac:InvoiceLine/cbc:InvoicedQuantity` | `Satir.Miktar` | Zorunlu | — | Doğrudan kullanılabilir |
| `cbc:InvoicedQuantity/@unitCode` | V2 `BirimKodu` | Zorunlu | `C62` | Deterministik eşlenebilir |
| `cac:InvoiceLine/cbc:LineExtensionAmount` | `Satir.Matrah` | Zorunlu | — | Doğrudan kullanılabilir |
| `cac:InvoiceLine/cac:TaxTotal` | `Satir.KdvTutari` | Opsiyonel | — | Deterministik eşlenebilir |
| `cac:Price/cbc:PriceAmount` | `Satir.BirimFiyat` | Zorunlu | — | Doğrudan kullanılabilir |
| `cac:Item/cbc:Name` | `Satir.Aciklama` | Zorunlu | — | Doğrudan kullanılabilir |
| Parasal alanların `@currencyID` | `Odeme.ParaBirimi` | Zorunlu | `TRY` | Deterministik eşlenebilir |
| XSLT `cac:AdditionalDocumentReference` | — | Opsiyonel | — | İlk sürümde destek dışı bırakılmalı |
| İmza `cac:AdditionalDocumentReference` | — | Opsiyonel | — | İlk sürümde destek dışı bırakılmalı |
| Karekod `cac:AdditionalDocumentReference` | — | Opsiyonel | — | İlk sürümde destek dışı bırakılmalı |

Tablo notları:

- **Renderer sabitleri** (`UBLVersionID`, `CustomizationID`, `CopyIndicator`, `TaxTypeCode`,
  `currencyID`) snapshot'ta hazır veri değildir; rule-set'ten veya dar kapsam kuralından
  deterministik olarak üretilir. Bu nedenle "Doğrudan kullanılabilir" değil, "Deterministik
  eşlenebilir" işaretlenmiştir.
- `ProfileID` durumu, ilk dalgada hangi kanalın destekleneceği kararına bağlıdır (§8). e-Arşiv
  kanalında `EARSIVFATURA` deterministik türetilir; e-Fatura kanalı ilk dalgaya girerse belge
  düzeyi `EFaturaSenaryosu` alanı gerekir ve bu alan bugün yoktur.
- `IssueDate`/`IssueTime`, bugünkü `FaturaKesimTarihi` (UTC) alanından **doğrudan
  türetilmemelidir** — UTC→yerel dönüşüm işletim sistemi zaman dilimi veritabanına bağımlılık
  yaratır ve determinizm sözleşmesini bozar (§6). V2, kesim anında çözülmüş yerel (TRT) tarih ve
  saat değerlerini ayrı alanlar olarak dondurmalıdır; renderer yalnız biçimlendirme yapar.
- `cac:Signature` XSD'de zorunludur (`UBL-Invoice-2.1.xsd:35`, `minOccurs` yok) ve içinde
  `cac:SignatoryParty/cac:PostalAddress` taşır; bu nedenle yapısal adres eksikliğine bağımlıdır
  (§5).
- `AllowanceTotalAmount` ve `ChargeTotalAmount`, UBL semantiğinde **belge düzeyi**
  indirim/artırım toplamlarıdır. Dar kapsamda belge düzeyi indirim bulunmadığı ve satır indirimi
  satır `LineExtensionAmount` içinde netleştirildiği için bu iki eleman hiç üretilmez (§4).

## 4. Otoriter kaynak eksikleri

- Belge düzeyi `ProfileID` kaynağı (kanal kararına bağlı, §8).
- Yapısal adres alanları: `Kurum.Adres` ve `Alici.MusteriAdres` tek serbest metindir. GİB'in
  değiştirilmiş `AddressType` tipinde (`UBL-CommonAggregateComponents-2.1.xsd:699-715`) eleman
  sırası `ID → Postbox → Room → StreetName → BlockName → BuildingName → BuildingNumber →
  CitySubdivisionName → CityName → PostalZone → Region → District → Country` şeklindedir;
  `CitySubdivisionName` (ilçe), `CityName` (il) ve `Country` **zorunludur**, `PostalZone`
  opsiyoneldir. **`AddressLine` elemanı bu tipte hiç yoktur** — dolayısıyla "tek serbest metni
  `AddressLine/Line` içine koymak" seçeneği teknik olarak mümkün değildir. `cac:Country` içinde
  `cbc:Name` zorunlu, `cbc:IdentificationCode` opsiyoneldir
  (`UBL-CommonAggregateComponents-2.1.xsd:1222`).
- Gerçek kişi alıcılar için ayrı `Ad`/`Soyad`: `PersonType` içinde `cbc:FirstName` ve
  `cbc:FamilyName` **her ikisi de zorunludur** (`UBL-CommonAggregateComponents-2.1.xsd:2239-2250`).
  `MusteriAdSoyad` tek string'i tahminle bölünemez.
- Yerel (TRT) fatura tarihi ve saati: bugünkü `FaturaKesimTarihi` UTC'dir; determinizm için
  çözülmüş yerel değerler gerekir.

Ülke bilgisi, `ParaBirimi = TRY` değerinden **türetilmeyecektir**; para birimi ile adres ülkesi
farklı kavramlardır. İlk kapsam yalnız Türkiye içi adresleri destekleyecektir ve bu, kapıda açık
bir destek kuralıdır (§9).

## 5. İlk renderer için destek matrisi

| Belge tipi | İlk renderer kapsamı | Gerekçe |
| --- | --- | --- |
| `SatisFaturasi` | Evet | Dar kapsamın tek senaryosu; `InvoiceTypeCode=SATIS` deterministik üretilir |
| `AlisIadeFaturasi` | Hayır | `InvoiceTypeCode=IADE` gerektirir; profil kısıtı ve schematron/örnek çelişkisi çözülmemiştir |
| `IadeFaturasi` | Hayır | Aynı gerekçe |
| `SatisIadeFaturasi` | Hayır | Gelen belge |
| `AlisFaturasi` | Hayır | Gelen belge |
| `Proforma`, `FaturaTaslagi` | Hayır | Resmî e-belge değil |

Dar kapsam kesin olarak: `SatisBelgesiTipi.SatisFaturasi`, `ParaBirimi=TRY`, `Kur=1`, tüm satırlar
`KdvUygulamaTipi.Kdvli`, yalnız standart KDV (`TaxTypeCode=0015`), tevkifat yok, istisna yok, ÖTV
yok, ÖİV yok, konaklama vergisi yok, iade yok, özel matrah yok, ihracat yok, yalnız Türkiye içi
adres, yalnız `Adet` birimi.

### İndirim ve parasal toplam eşlemesi

`Satir.IndirimTutari`, belge düzeyi `Invoice/AllowanceCharge` olarak eşlenmez. Doğru konum
`cac:InvoiceLine/cac:AllowanceCharge`'dır. `AllowanceChargeType` eleman sırası
(`UBL-CommonAggregateComponents-2.1.xsd:726-736`):
`ChargeIndicator → AllowanceChargeReason → MultiplierFactorNumeric → SequenceNumeric → Amount →
BaseAmount → PerUnitAmount`. Bunlardan `ChargeIndicator` ve `Amount` zorunludur.

Dar kapsam eşlemesi:

- `cbc:ChargeIndicator` = `false` (indirim, artırım değil)
- `cbc:MultiplierFactorNumeric` = `Satir.IndirimOrani / 100` — yalnız `IndirimOrani > 0` ise üretilir
- `cbc:Amount` = `Satir.IndirimTutari`
- `cbc:BaseAmount` = `Satir.Miktar × Satir.BirimFiyat` (indirim öncesi brüt tutar)
- `Satir.IndirimTutari = 0` ise `cac:AllowanceCharge` elemanı **hiç üretilmez**
- Belge düzeyi indirim bulunmadığı için root `cac:AllowanceCharge` **hiç üretilmez**

`cac:InvoiceLine/cbc:LineExtensionAmount`, UBL semantiğinde satır indirimi **düşülmüş** net
tutardır; `Satir.Matrah` alanına karşılık gelir. Brüt tutar yalnız `BaseAmount` içinde görünür.

Standart KDV'li dar kapsam için parasal eşleme:

| Kavram | UBL hedefi | Hesap |
| --- | --- | --- |
| Satır matrahı | `InvoiceLine/LineExtensionAmount` | `Satir.Matrah` |
| KDV oranı | `InvoiceLine/TaxTotal/TaxSubtotal/TaxCategory/Percent` | `Satir.KdvOrani` |
| Satır KDV tutarı | `InvoiceLine/TaxTotal/TaxAmount` | `Satir.KdvTutari` |
| Gruplanmış alt toplam | `TaxTotal/TaxSubtotal` | KDV oranına göre gruplanır, oran artan sırada |
| Toplam matrah | `LegalMonetaryTotal/LineExtensionAmount` | `ToplamMatrah` |
| Toplam KDV | `TaxTotal/TaxAmount` | `ToplamKdv` |
| Vergi hariç toplam | `LegalMonetaryTotal/TaxExclusiveAmount` | `ToplamMatrah` |
| Vergi dahil toplam | `LegalMonetaryTotal/TaxInclusiveAmount` | `GenelToplam` |
| Ödenecek toplam | `LegalMonetaryTotal/PayableAmount` | `GenelToplam` |

Belge düzeyi `TaxSubtotal` grupları, KDV oranına göre gruplanır ve **oran değerine göre artan
sırada** yazılır; bu, determinizm sözleşmesinin bir parçasıdır (§6).

**Renderer snapshot toplamlarını sessizce değiştirmez.** Davranış sırası:

1. Snapshot satırları ve toplamları aynı deterministik mali kurallarla doğrulanır:
   `Σ Satir.Matrah == ToplamMatrah`, `Σ Satir.KdvTutari == ToplamKdv`,
   `ToplamMatrah + ToplamKdv == GenelToplam` ve her satır için
   `Miktar × BirimFiyat − IndirimTutari == Matrah`.
2. Uyuşmazlık varsa XML **üretilmez**.
3. Kalıcı `EBELGE_UBL_MONETARY_TOTAL_MISMATCH` hatası üretilir (HTTP 422).
4. Uyuşmaz değer renderer tarafından **düzeltilmez**, yuvarlanmaz.
5. Doğrulama başarılıysa canonical snapshot değerleri XML'e **olduğu gibi** yazılır.

Kesim öncesi kapı, aynı mali doğrulayıcıyı (aynı kod yolunu) kullanır; iki ayrı hesap mantığı
zamanla farklılaşamaz.

### İmza sınırı

Aşağıdaki kavramlar birbirinden ayrıdır:

1. **Deterministik unsigned UBL XML renderer** (Faz 2B.5): bu fazın çıktısı. XSD ve schematron'a
   uygun, ancak kriptografik imza taşımayan XML.
2. **`cac:Signature` iş/referans metadata'sı**: XSD'de zorunlu bir elemandır; imzalayanın
   VKN'sini (`cbc:ID schemeID="VKN_TCKN"`), `cac:SignatoryParty` altında `PartyIdentification` ve
   `PostalAddress` bilgisini taşır. Bu **kriptografik imzanın kendisi değildir**; unsigned XML de
   bu elemanı içermek zorundadır. Gerekli snapshot alanları: `Kurum.VergiNo` ve kurumun yapısal
   adresi (§4'teki eksikliğe bağımlıdır).
3. **`ext:UBLExtensions` içindeki XAdES/mali mühür içeriği**: kriptografik imza burada taşınır.
   Unsigned renderer bu bloğu **boş/yer tutucu olarak üretmez**; imzalama fazı ekler.
4. **Sonraki kriptografik imzalama fazı**: ayrı bir uygulama fazıdır, bu hazırlık fazının ve Faz
   2B.5'in kapsamı dışındadır.
5. **İmzalama sonrası nihai artifact**: gönderime hazır e-Fatura/e-Arşiv belgesidir.

**Faz 2B.5 çıktısı gönderime hazır nihai e-Fatura değildir.** Hash ayrımı:

- `UnsignedUblSha256`: renderer çıktısının tam UTF-8 byte dizisi üzerinden hesaplanır; determinizm
  testlerinin dayanağıdır.
- `SignedUblSha256`: imzalama fazından sonra, imzalanmış byte dizisi üzerinden **yeniden**
  hesaplanır. İmzalama byte'ları değiştirdiği için bu iki değer birbirinin yerine kullanılamaz ve
  aynı alanda saklanamaz.

## 6. Snapshot V1/V2 kararı

Mevcut `IEBelgeCanonicalSnapshotReader.Oku(...)` imzası **`EBelgeCanonicalSnapshotV1`
döndürmeye devam eder**. Önceki raporun bu imzanın dönüş tipini bir union'a çevirme önerisi
geri alınmıştır: dönüş tipini değiştirmek interface'i korumak değil, kırıcı değişiklik yapmaktır.

En küçük güvenli tasarım:

- Mevcut `EBelgeCanonicalSnapshotReader` ve `EBelgeCanonicalSnapshotV1` record'u **aynen korunur**;
  alan seti, JSON şekli ve hash doğrulaması değiştirilmez.
- Ayrı ve typed bir `IEBelgeCanonicalSnapshotV2Reader` eklenir:
  `EBelgeCanonicalSnapshotV2 Oku(EBelgeCanonicalSnapshotOkumaTalebi talep)`.
- Sürümü okuyup doğru typed reader'a yönlendiren ayrı bir dispatcher bulunur; dispatcher
  `SnapshotSchemaVersion` değerine bakar ve desteklenmeyen sürümde açık hata üretir.
- Renderer **yalnız** `IEBelgeCanonicalSnapshotV2Reader` çıktısı olan `EBelgeCanonicalSnapshotV2`
  değerini tüketir; V1 kabul etmez.
- `object` ve `dynamic` kullanılmaz.
- V1 kayıtlar backfill veya migration ile dönüştürülmez.
- V1 snapshot için render isteği gelirse kalıcı `EBELGE_UBL_RENDER_SNAPSHOT_VERSION_UNSUPPORTED`
  hatası, **HTTP 422** ile üretilir. Bu hata tekrar denemeyle çözülmez; outbox tarafında kalıcı
  hata olarak işaretlenir.

`EBelgeCanonicalSnapshotV2` içinde V1'e ek olarak bulunacak alanlar (dar kapsam için gerekli
olanlar, fazlası değil):

- `ProfileID` (nihai değer, kesim anında çözülmüş)
- `InvoiceTypeCode` (dar kapsamda `SATIS`)
- `FaturaTarihiTrt`, `FaturaSaatiTrt` (çözülmüş yerel değerler)
- Satıcı yapısal adres: `Ilce`, `Il`, `UlkeAdi`, `UlkeKodu`, opsiyonel `PostaKodu`, `SokakAdi`,
  `BinaNo`
- Alıcı yapısal adres: aynı alanlar
- `MusteriAd`, `MusteriSoyad` (gerçek kişi alıcılarda)
- Satır düzeyinde `BirimKodu` (dar kapsamda `C62`)

## 7. Önerilen renderer sözleşmesi

Renderer'ın **tek iş girdisi** doğrulanmış typed V2 snapshot'tır:

```csharp
public interface IEBelgeUblRenderer
{
    EBelgeUblRenderSonucu Render(EBelgeCanonicalSnapshotV2 snapshot);
}
```

Renderer'a **ayrıca parametre olarak verilmeyecekler**: belge tipi, issue/issuance tarihi, tenant
veya kurum bağlamı, taraf bilgileri, kanal, `ProfileID`, `InvoiceTypeCode`, birim kodu. Bunların
tamamı `EBelgeCanonicalSnapshotV2` içinde bulunur.

GİB kural seti **iş girdisi değildir**; implementasyona immutable teknik konfigürasyon olarak
enjekte edilir:

```csharp
public sealed class EBelgeUblRenderer : IEBelgeUblRenderer
{
    public EBelgeUblRenderer(GibKuralSeti kuralSeti) { ... }
}

public sealed record GibKuralSeti(
    string KuralSetiKimligi,          // "GIB-UBL-TR-1.2.1/2026-09-14"
    string UblVersionId,              // "2.1"
    string CustomizationId,           // "TR1.2"
    IReadOnlyDictionary<string, string> PaketSha256);
```

`GibKuralSeti`, build/deployment artifact'ından yüklenir; canlı internet veya veritabanından
okunmaz. Bu projede yalnız `GIB-UBL-TR-1.2.1/2026-09-14` desteklenir; tarih bazlı seçim yoktur.

Çıktı:

```csharp
public sealed record EBelgeUblRenderSonucu(
    ReadOnlyMemory<byte> UnsignedUblUtf8,   // dışarıdan değiştirilemez
    string UnsignedUblSha256,
    string KullanilanProfileId,
    string KullanilanInvoiceTypeCode,
    string KuralSetiKimligi,
    string RendererSurumu);
```

Renderer V1 snapshot kabul etmez; `IEBelgeCanonicalSnapshotV1` tipini hiçbir aşırı yüklemede
almaz.

### Determinizm sözleşmesi

| Konu | Politika | Test beklentisi |
| --- | --- | --- |
| Aynı typed V2 snapshot → aynı XML | Renderer saf fonksiyondur; girdi dışı hiçbir durum okunmaz | Aynı snapshot ile 100 kez çağrı, byte dizileri birebir eşit |
| UTF-8 ve BOM | UTF-8, BOM üretilmez | Çıktının ilk 3 byte'ı `EF BB BF` değil |
| XML declaration | `<?xml version="1.0" encoding="UTF-8"?>` sabit, tek satır | Golden-file byte karşılaştırması |
| Indentation | Girinti üretilmez; tüm belge tek satır | Çıktıda `\t` ve satır başı boşluk bulunmaz |
| Newline | Yalnız `\n`; `\r` hiç üretilmez | Çıktıda `0x0D` byte'ı bulunmaz |
| Namespace prefixleri | Sabit tablo: `cac`, `cbc`, `ext`, `ds`, `xades`, `xsi` | Golden-file + prefiks sırası testi |
| Element sırası | `UBL-Invoice-2.1.xsd` sequence sırası; `TaxSubtotal` grupları KDV oranına göre artan | Sıra-duyarlı golden-file testi |
| Attribute yazım sırası | Her elemanda sabit, kaynak sırasından bağımsız | Attribute sırası regresyon testi |
| Kültür | Tüm biçimlendirme `CultureInfo.InvariantCulture` | `tr-TR`, `de-DE`, `en-US` ile aynı çıktı |
| Decimal biçimi | Sabit ondalık ayıraç `.`; parasal alanlar 2 basamak, oran alanları 2 basamak; trailing zero korunur | `1.5m`, `1.50m`, `1.500m` girdileri aynı lexical çıktıyı verir |
| `IssueDate` biçimi | `yyyy-MM-dd`, V2'deki çözülmüş TRT değerinden | Golden-file |
| `IssueTime` biçimi | `HH:mm:ss`, offset yazılmaz, V2'deki çözülmüş TRT değerinden | Golden-file |
| OS/kültür/yerel saat dilimi bağımsızlığı | Renderer `TimeZoneInfo` çağırmaz; dönüşüm kesim anında yapılmıştır | UTC, `Europe/Istanbul`, `America/New_York` altında aynı hash |
| Güncel saat yasağı | `DateTime.Now`/`UtcNow` çağrılmaz | Statik analiz kuralı + saat ileri alınarak aynı çıktı testi |
| Rastgele UUID yasağı | `Guid.NewGuid()` çağrılmaz; UUID snapshot'tan gelir | Statik analiz kuralı |
| Veritabanı/canlı entity okuma yasağı | Renderer'a `DbContext` enjekte edilmez | Constructor bağımlılık testi |
| SHA-256 | Çıktının tam UTF-8 byte dizisi üzerinden | Byte dizisi sabitken hash sabit |
| Renderer sürümü | Sonuçta `RendererSurumu` olarak döner | Sürüm alanının dolu olduğu testi |
| Rule-set kimliği | Sonuçta `KuralSetiKimligi` olarak döner | `GIB-UBL-TR-1.2.1/2026-09-14` değeri testi |
| Değiştirilemez byte çıktısı | `ReadOnlyMemory<byte>` döner; iç buffer dışarı sızdırılmaz veya defensive copy yapılır | Çağıranın çıktıyı değiştirememesi testi |
| Farklı culture/timezone'da aynı hash | Yukarıdakilerin bileşimi | Matris testi: 3 culture × 3 timezone = 9 kombinasyon, tek hash |

Bu turda test yazılmamıştır; yukarıdaki beklentiler Faz 2B.5 promptuna girecek test listesidir.

## 8. Sonraki uygulama fazlarının sırası

**Faz 2B.4.1 — minimal hazırlık (yalnız dar renderer için gerekli olan):**

1. `EnsureUblHazirlikKaynaklari` genişletilir ve kanal çözümlemesi sayaç kilidinden öncesine
   taşınır (§9).
2. Yapısal adres alanları: `Kurum` ve alıcı adres kaynağına `Ilce`, `Il`, `UlkeAdi`/`UlkeKodu`
   eklenir.
3. Gerçek kişi alıcılar için ayrı `Ad`/`Soyad` alanları eklenir.
4. `EBelgeCanonicalSnapshotV2` + `IEBelgeCanonicalSnapshotV2Reader` + dispatcher eklenir.
5. Feature flag/konfigürasyon anahtarı eklenir ve kapalı başlatılır.

**Faz 2B.5 — deterministic unsigned UBL renderer:**

6. `IEBelgeUblRenderer` ve dar kapsam implementasyonu.
7. Determinizm test paketi (§7 tablosu).
8. XSD + schematron doğrulaması (sabitlenmiş rule-set ile).

**Sonraki fazlar:** kriptografik imzalama, PDF renderer, artifact storage abstraction, e-posta
gönderim provider'ı.

Tevkifat, ÖTV, ÖİV, konaklama vergisi, iade, özel matrah ve ihracat alanları bu sıraya
**eklenmemiştir**; destek dışı oldukları için model değişikliği gerektirmezler.

### Birim kodu: entity alanı mı, yalnız V2 alanı mı

| Seçenek | Kapsam | Değerlendirme |
| --- | --- | --- |
| A: `SatisBelgesiSatiri`'ne genel amaçlı `BirimKodu` alanı | Entity değişikliği + migration + tüm birim değerleri için eşleme tablosu | Dar kapsam yalnız `Adet` kabul edeceği için gereksiz geniştir; eşleme tablosu bugün doğrulanamayan birimleri de zorunlu kılar |
| B: Yalnız V2 snapshot'ta `BirimKodu` | Entity değişikliği yok, migration yok | Kapı yalnız `Birim == "Adet"` kabul eder; V2'ye sabit `C62` yazılır |

**Seçilen: B.** Entity'ye genel amaçlı `BirimKodu` eklemek zorunlu değildir. Kesim öncesi kapı
yalnız desteklenen canonical `"Adet"` değerini kabul eder, V2 snapshot'a nihai `BirimKodu=C62`
yazılır, diğer tüm birimler destek dışı kalır. Genel amaçlı birim kodu modeli, ikinci bir birim
desteklendiği fazda eklenir.

### `InvoiceTypeCode` ve `ProfileID`

- `InvoiceTypeCode` için **entity alanı eklenmez.** Dar kapsamda `SATIS` deterministik üretilir ve
  V2 snapshot'a yazılır.
- `ProfileID`: e-Arşiv kanalında `EARSIVFATURA` deterministik üretilir ve entity alanı gerekmez.
  e-Fatura kanalı ilk sürümde desteklenecekse belge düzeyi `EFaturaSenaryosu` alanı **gerekir**;
  ilk sürüm yalnız e-Arşiv olacaksa bu alan hazırlık fazına **eklenmemelidir**.
- Bu nedenle **hazırlık listesi, ilk dalgada desteklenecek kanal kesinleşmeden tamamen
  kesinleşmez.** Kanal kararı §10'daki ilk sorudur.

### Satıcı hukuki tarafı

`cac:AccountingSupplierParty` kaynağı **`Kurum`** olmalıdır. `Tesis.Adres`, kurumun hukuki
adresinin yerine kullanılmamalıdır; bu, hukuki taraf adresi ile fiili hizmet adresini karıştırır.
`Kurum.Adres` bugün `string?` olduğu ve boş olabildiği için kapı bunu zorunlu kılmalıdır (mevcut
`EnsureUblHazirlikKaynaklari` bunu zaten kontrol etmektedir). Tesis bilgisi gerekirse daha sonra
operasyonel/ek lokasyon (`cac:Delivery`) olarak ayrı bir fazda ele alınabilir.

## 9. Faz 2B.4.1 ve Faz 2B.5 promptlarına girecek kesin kararlar

- Renderer'ın tek iş girdisi `EBelgeCanonicalSnapshotV2`'dir; belge tipi, tarih, tenant, kanal,
  `ProfileID`, `InvoiceTypeCode`, birim kodu ayrıca parametre olarak verilmez.
- GİB kural seti immutable teknik konfigürasyondur, build artifact'ında sabittir, runtime'da
  indirilmez; kimliği `GIB-UBL-TR-1.2.1/2026-09-14`'tür ve tarih bazlı seçim yoktur.
- V1 reader ve V1 record aynen korunur; ayrı typed V2 reader ve dispatcher eklenir; renderer V1
  kabul etmez.
- Satır indirimi `InvoiceLine/AllowanceCharge` altına yazılır; belge düzeyi `AllowanceCharge`,
  `AllowanceTotalAmount` ve `ChargeTotalAmount` üretilmez.
- Renderer snapshot toplamlarını değiştirmez; uyuşmazlıkta XML üretmez ve
  `EBELGE_UBL_MONETARY_TOTAL_MISMATCH` (422) verir.
- Faz 2B.5 çıktısı unsigned XML'dir ve gönderime hazır nihai e-Fatura değildir; imzalama ayrı
  fazdır ve artifact hash'i imzalamadan sonra yeniden hesaplanır.
- Birim için entity alanı eklenmez; kapı yalnız `Adet` kabul eder, V2'ye `C62` yazılır.
- `InvoiceTypeCode` için entity alanı eklenmez; dar kapsamda `SATIS` üretilip V2'ye yazılır.
- `EFaturaSenaryosu` alanı yalnızca ilk dalga e-Fatura kanalını içeriyorsa eklenir.
- Özellik, 14.09.2026 canlıya geçişine kadar feature flag ile kapalı tutulur.

### Kesim öncesi kapı sözleşmesi

Kapı, mevcut `EnsureUblHazirlikKaynaklari` (`SatisBelgesiService.cs:1116`) genişletilerek
oluşturulur ve **aynı noktada** çalışır. Kanal çözümlemesi (`ResolveEBelgeKanali`) bu noktaya
taşınır. Kapı şunları doğrular:

1. Sistemin canlı kullanım için etkinleştirilmiş olması (feature flag açık)
2. Belge tarihinin ve fatura kesim tarihinin 14.09.2026'dan önce olmaması
3. Yalnız `GIB-UBL-TR-1.2.1/2026-09-14` rule-setinin kullanılıyor olması
4. Desteklenen kanal (kanal kararına göre e-Arşiv ve/veya e-Fatura)
5. Belge tipinin `SatisFaturasi` olması
6. `ParaBirimi == "TRY"` ve `Kur == 1`
7. `ProfileID` kaynağının çözülebilir olması
8. `InvoiceTypeCode` değerinin `SATIS` olarak üretilebilmesi
9. Kurumsal veya gerçek kişi alıcı kimliğinin tam olarak birinin bulunması
10. Satıcı ve alıcı için zorunlu adres alanlarının (ilçe, il, ülke) dolu olması
11. Gerçek kişi alıcıda ad ve soyadın ayrı ayrı dolu olması
12. Kurumsal alıcıda unvanın dolu olması
13. VKN'nin 10, TCKN'nin 11 hane olması
14. Tüm satırların `KdvUygulamaTipi.Kdvli` olması
15. Hiçbir satırda tevkifat, istisna, ÖTV, ÖİV veya konaklama vergisi alanının dolu olmaması
16. Tüm satırlarda `Birim == "Adet"` olması
17. Satır ve belge toplamlarının §5'teki mali doğrulayıcıya göre tutarlı olması
18. En az bir geçerli (silinmemiş) satır bulunması

Kapı; sayaç artırılmadan, resmî numara verilmeden, belge durumu değiştirilmeden, `EBelgeKaydi`
oluşturulmadan, snapshot oluşturulmadan ve outbox oluşturulmadan çalışır. Bugünkü kod bu sıralamayı
zaten sağlamaktadır (§2); yalnız kanal çözümlemesinin öne alınması gerekir.

### Hata kodları

| Hata kodu | HTTP | Durum |
| --- | --- | --- |
| `EBELGE_UBL_FEATURE_DISABLED` | 503 | Özellik henüz etkinleştirilmemiş |
| `EBELGE_INVOICE_DATE_BEFORE_GO_LIVE` | 400 | Fatura/belge tarihi 14.09.2026'dan önce |
| `EBELGE_UBL_SCOPE_UNSUPPORTED` | 400 | Destek dışı belge tipi, kanal, vergi, birim veya para birimi |
| `EBELGE_UBL_AUTHORITATIVE_FIELD_MISSING` | 400 | Eksik otoriter metadata (adres, ad/soyad, unvan, VKN/TCKN) |
| `EBELGE_UBL_MONETARY_TOTAL_MISMATCH` | 422 | Satır/belge toplamları tutarsız |
| `EBELGE_UBL_RENDER_SNAPSHOT_VERSION_UNSUPPORTED` | 422 | V1 snapshot ile render isteği |

**400 ile 422 ayrımı:** HTTP 400, çağıranın belgeyi düzelterek yeniden gönderebileceği kapsam ve
eksik veri hatalarıdır. HTTP 422, belge yapısal olarak geçerli ve kapsam içi olmasına rağmen
semantik/tutarlılık invariantının ihlal edildiği, yeniden göndermekle çözülmeyen durumlardır;
mevcut `EBelgeCanonicalSnapshotException` da bu nedenle 422 kullanmaktadır. `EBELGE_UBL_FEATURE_DISABLED`
bilinçli olarak 503'tür — belge veya istek hatalı değildir, hizmet henüz açılmamıştır.

## 10. Açık kalan ve ürün sahibinin cevaplaması gereken sorular

1. **İlk dalgada hangi kanal desteklenecek — yalnız e-Arşiv mi, e-Fatura da dahil mi?** Bu karar
   verilmeden `EFaturaSenaryosu` alanının gerekip gerekmediği ve dolayısıyla Faz 2B.4.1 hazırlık
   listesi tamamen kesinleşmez.
2. Kurum ve cari kart adreslerinin ilçe/il/ülke bilgisi mevcut veri tabanında var mı, yoksa veri
   girişi/veri temizliği gerekecek mi?
3. Gerçek kişi müşterilerin ad ve soyad bilgisi ayrı alanlarda toplanabiliyor mu, yoksa mevcut
   `MusteriAdSoyad` verisi için tek seferlik manuel ayrıştırma mı gerekecek?
4. Feature flag'in açılma kararı hangi ortamda ve kim tarafından verilecek; test ortamında
   14.09.2026 öncesi deneme yapılabilmesi için ayrı bir mekanizma isteniyor mu?
5. Konaklama vergisi, ÖTV, ÖİV ve tevkifat senaryoları hangi fazda ele alınacak?
6. Resmî schematron kuralı ile resmî `IadeFaturasiOrnegi.xml` örneği arasındaki
   `TICARIFATURA`+`IADE` çelişkisi, iade senaryoları planlanmadan önce GİB'e sorulacak mı?

## Sonuç

**Renderer öncesinde ek hazırlık fazı gerekir.**

Hazırlık fazı (Faz 2B.4.1) yalnız dar renderer için gerekli entity, snapshot V2, reader ve kesim
öncesi kapı değişiklikleriyle sınırlıdır:

1. `EnsureUblHazirlikKaynaklari` genişletilmesi ve kanal çözümlemesinin sayaç kilidinden öncesine
   taşınması.
2. Yapısal adres alanları (ilçe, il, ülke) — satıcı ve alıcı için.
3. Gerçek kişi alıcılar için ayrı `Ad`/`Soyad` alanları.
4. `EBelgeCanonicalSnapshotV2`, `IEBelgeCanonicalSnapshotV2Reader` ve dispatcher; V1 aynen
   korunur.
5. Feature flag ve 14.09.2026 tarih sınırı.

Birim kodu için entity alanı, `InvoiceTypeCode` için entity alanı ve destek dışı senaryoların
(tevkifat, ÖTV, ÖİV, konaklama vergisi, iade, özel matrah, ihracat) alanları bu hazırlık fazına
**dahil değildir**.
