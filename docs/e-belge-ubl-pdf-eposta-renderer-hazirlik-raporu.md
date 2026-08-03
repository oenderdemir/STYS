# E-Belge UBL/PDF/E-Posta Renderer Hazırlık Raporu

Bu rapor, mevcut çalışma ağacındaki e-belge snapshot yapısının UBL üretimi için yeterli olup olmadığını
ve renderer öncesinde ek hazırlık fazı gerekip gerekmediğini değerlendirir.

İnceleme tarihi: 03.08.2026

İncelenen resmi GİB kaynakları:

- https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-FaturaPaketi.zip
- https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/earsiv_paket_v1.1_8.zip
- https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/UBLTR_1.2.1_Kilavuzlar.zip

## 1. İncelenen GİB kaynakları ve sürümleri

### e-Fatura paketi

Paket, UBL-TR örnek XML'ler, XSD'ler ve schematron kuralları içeriyor. İçeriğinde:

- `xml/1_TEMEL_FATURA.xml`
- `xml/2_TICARI_FATURA.xml`
- `xml/7_TEMEL_FATURA_IADE.xml`
- `xml/7_TICARI_FATURA.xml`
- `schematron/UBL-TR_Main_Schematron.xml`
- `schematron/UBL-TR_Common_Schematron.xml`
- `schematron/UBL-TR_Codelist.xml`

Paket, doğrulama ve kural kaynağı olarak kullanılabilir; doğrudan renderer sözleşmesi değildir.

### e-Arşiv paketi

Paket, e-Arşiv XSD/WSDL ve schematron altyapısını içeriyor:

- `EArsiv.xsd`
- `eArsivVeri.xsd`
- `EArsivWs.wsdl`
- `earsiv_schematron.xsl`

### UBLTR_1.2.1 kılavuz paketi

Paket içinde belge, senaryo ve kod listeleri dokümanları var:

- `UBL-TR Fatura - V 1.0.pdf`
- `UBL-TR Uygulama Yanıtı - V 0.2.pdf`
- `UBL-TR Temel Fatura Senaryosu - V 0.2.pdf`
- `UBL-TR Ticari Fatura Senaryosu - V 0.3.pdf`
- `UBL-TR Kod Listeleri - V 1.43.pdf`

`Degisim Tablosu.txt` içinde `27.07.2026 - UBL-TR Kod Listeleri - V 1.43` satırı bulunuyor. Bu, kod listesi değişimlerinin tarihe bağlı yönetilmesi gerektiğini gösteriyor.

## 2. Repository’deki mevcut durum

Mevcut kodda şu yapı var:

- Ticari/muhasebesel otorite: `SatisBelgesi`
- Immutable snapshot: `EBelgeSnapshot`
- Kanonik snapshot okuyucu: `EBelgeCanonicalSnapshotReader`
- Fatura kesim akışı: `SatisBelgesiService.FaturaKesAsync`

Snapshot ve iş modeli açısından önemli gözlemler:

- Kurum tarafında `VergiDairesi` ve `Adres` alanları artık mevcut.
- Belge tarafında `ParaBirimi` ve `Kur` mevcut.
- Müşteri snapshot alanları mevcut:
  - `MusteriUnvan`
  - `MusteriAdSoyad`
  - `MusteriVergiNo`
  - `MusteriTcKimlikNo`
  - `MusteriVergiDairesi`
  - `MusteriAdres`
  - `MusteriEposta`
  - `MusteriTelefon`
  - `KurumsalMi`
- Satır sıralaması deterministik: önce `SiraNo`, eşitlik halinde `Id`.

Repo’da henüz şu bileşenler yok:

- UBL XML renderer
- PDF renderer
- storage abstraction
- e-posta provider / sender

## 3. Snapshot → UBL eşleme matrisi

| Snapshot alanı | UBL karşılığı | Durum |
|---|---|---|
| Kurum.Ad | Supplier unvanı | Uygun |
| Kurum.VergiNo | VKN / party ID | Uygun |
| Kurum.VergiDairesi | Vergi dairesi / kayıt bağlamı | Uygun, ama yerleşim netleştirilmeli |
| Kurum.Adres | PostalAddress | Kısmi; tek serbest metin alanı olabilir, fakat yapılandırma kararı gerekir |
| Tesis bilgileri | İşletme/şube bağlamı | Kısmi |
| MusteriUnvan / AdSoyad / VergiNo / TcKimlikNo / VergiDairesi / Adres / Eposta / Telefon / KurumsalMi | CustomerParty | Uygun |
| CariKartId / CariKodu / EFaturaMukellefiMi / EArsivKapsamindaMi | Kanal ve taraf metadata | Uygun |
| ParaBirimi | DocumentCurrencyCode | Uygun |
| Kur | ExchangeRate / monetary context | Uygun |
| VadeTarihi | PaymentDueDate | Uygun |
| Satır vergi alanları | TaxTotal / TaxSubtotal / Withholding | Kısmi; kod sözlüğü gerekir |
| Satır.Birim | UnitCode | Eksik; serbest metin yeterli değil |
| İade edilen belge no/UUID/tarih | BillingReference | Uygun |
| Snapshot schema version | Renderer contract version | Uygun |
| Canonical SHA-256 | Integrity hash | Uygun |

## 4. Otoriter kaynak eksikleri

Renderer öncesinde hâlâ netleşmesi gereken noktalar:

- `ProfileID` seçimi belge tipine göre otoriter değil.
- Satır birimi kodu yok; `Birim` serbest metin.
- KDV / istisna / tevkifat / ÖTV / ÖİV / konaklama vergisi kodları merkezi çözülmüyor.
- Belge tipi → UBL belge tipi eşlemesi açık değil.
- Adres modelinin UBL açısından yeterli olup olmadığı ayrı karar gerektiriyor.
- UBL paket seçimi için tarih bazlı effective-date registry yok.
- Renderer çıktı sözleşmesi ayrı bir artifact contract olarak tanımlı değil.

## 5. İlk renderer için destek matrisi

| Belge tipi | İlk renderer kapsamı | Gerekçe |
|---|---|---|
| SatisFaturasi | Evet | Giden belgenin ana senaryosu |
| AlisIadeFaturasi | Evet | STYS tarafından düzenlenen giden iade senaryosu |
| AlisFaturasi | Hayır | Gelen belge; STYS sadece tüketir |
| SatisIadeFaturasi | Hayır | Gelen belge; yeniden düzenleme modeli olmamalı |

İlk renderer yalnızca giden belgeler için açılmalı.

## 6. Snapshot V1/V2 kararı

Karar:

- Mevcut `EBelgeSnapshot` V1, iş verisi için immutable kanonik snapshot olarak korunmalı.
- Renderer doğrudan canlı tablolardan değil snapshot'tan beslenmeli.
- Ancak UBL için gereken tüm metadata V1 içinde yeterince otoriter değil.

Bu nedenle:

- V1 bozulmamalı.
- Renderer için ayrı bir UBL odaklı input sözleşmesi tanımlanmalı.
- V1 iş snapshot'ı, renderer input'u ise yayın snapshot'ı olarak ayrıştırılmalı.

## 7. Önerilen renderer sözleşmesi

Önerilen giriş:

- immutable snapshot
- belge tipi
- issuance tarihi
- etkin GİB paket/kod listesi versiyonu
- tenant / kurum bağlamı

Önerilen çıktı:

- UBL XML
- kullanılan profile/senaryo kodu
- SHA-256
- validation sonucu
- renderer uyarıları

Kurallar:

- Canlı DB’den yeniden okuma yapılmamalı.
- Snapshot dışında sessiz tamamlama yapılmamalı.
- Eksik UBL metadata açık hata üretmeli.
- Kod listesi seçimi tarih bazlı registry’den gelmeli.

## 8. Sonraki uygulama fazlarının sırası

Önerilen sıra:

1. Authoritative UBL metadata registry
2. Satır birim kodu eşlemesi
3. Vergi kodu çözücüsü
4. Belge tipi / profile resolver
5. Renderer input contract ayrıştırması
6. UBL XML renderer
7. UBL doğrulama / schematron
8. PDF renderer
9. Artifact storage abstraction
10. E-posta gönderim provider'ı

## 9. Faz 2B.5 uygulama promptuna girecek kesin kararlar

- Yalnız giden belgeler için ilk renderer açılacak.
- `SatisBelgesi` iş otoritesi olarak kalacak.
- `EBelgeSnapshot` immutable iş özeti olacak.
- Renderer canlı tabloları değil snapshot'ı okuyacak.
- `ProfileID`, belge tipi ve kod listesi seçimi tarih bazlı registry’den gelecek.
- Satır birimi kod tabanlı modele taşınacak; serbest metin yeterli sayılmayacak.
- Vergi kodları merkezi resolver ile çözülecek.
- PDF ve e-posta, UBL XML’den sonra ayrı fazlarda ele alınacak.
- Gelen belgeler yeniden düzenlenebilir e-belge gibi modellenmeyecek.
- Eksik UBL metadata sessiz varsayımla doldurulmayacak; açık hata üretilecek.

## 10. Açık kalan ve ürün sahibinin cevaplaması gereken sorular

- `ProfileID` belge tipine göre nasıl seçilecek?
- Satır birimi için hangi resmi kod listesi kullanılacak?
- KDV / tevkifat / ÖTV / ÖİV / konaklama vergisi için hangi kodlar otoriter?
- Tek satır adres yeterli mi, yoksa yapılandırılmış adres alanları mı gerekli?
- İlk renderer yalnız e-Fatura mı destekleyecek, e-Arşiv de aynı dalgada mı gelecek?
- PDF, UBL’den türeyen bir sunum katmanı mı olacak?
- UBL paket seçimi issuance tarihiyle mi, effective-date registry ile mi yönetilecek?

## Sonuç

Renderer öncesinde ek hazırlık fazı gerekir.

En dar hazırlık fazı:

- authoritative UBL metadata registry
- satır birim kodu eşlemesi
- vergi kodu çözümleyicisi
- belge tipi / profile resolver
- renderer input contract ayrıştırması

