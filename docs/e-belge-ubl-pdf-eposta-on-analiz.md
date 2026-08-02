# e-Belge / UBL-TR / PDF / e-Posta Aşaması Ön Teknik Analiz

Tarih: 2026-08-02  
Kapsam: STYS kod tabanı içindeki satış belgesi modeli, faturalama akışı, dosya saklama altyapısı ve e-belge üretim/gönderim sınırları.  
Not: Bu analizde yalnız resmî GİB kaynakları kullanıldı. Erişim tarihi 2026-08-02’dir.

## Resmî kaynaklar

- GİB e-Belge ana sayfa: https://ebelge.gib.gov.tr/anasayfa.html
- GİB e-Fatura mevzuat ve kılavuzlar: https://ebelge.gib.gov.tr/efaturamevzuat.html
- e-Fatura Uygulaması Özel Entegrasyon Kılavuzu: https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-Fatura_Uygulamasi_Ozel_Entegrasyon_Kilavuzu_v1.14.pdf
- e-Fatura Uygulaması Saklama Kılavuzu: https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-FaturaUygulamasiSaklamaKilavuzu.pdf

GİB ana sayfasında 27.07.2026 tarihli duyuruda “UBL-TR (Kod Listeleri) Kılavuzu”nun güncellendiği ve değişikliklerin 14.09.2026 itibarıyla devreye alınacağı görülüyor. Bu nedenle kod listeleri ve resmî paket içerikleri için güncel GİB dokümanı tek otorite olarak alınmalıdır.

## Mevcut durum

### Satış belgesi modeli

İncelenen ana model `SatisBelgesi` ve `SatisBelgesiSatiri`.

`SatisBelgesi` içinde şu kritik alanlar mevcut:

- kurum / tesis / cari kart bağları: `KurumId`, `TesisId`, `CariKartId`
- belge yönü ve kaynak izleri: `BelgeTipi`, `KaynakModul`, `KaynakTipi`, `KaynakId`
- resmi belge alanları: `ResmiFaturaNo`, `EBelgeUuid`
- iade referansı: `IadeEdilenBelgeId`
- muhasebe ve faturalama durumları: `TicariDurum`, `MuhasebeDurumu`, `FaturalamaDurumu`
- belge zamanları: `FaturaKesimTarihi`, `MuhasebeOnayinaGonderilmeTarihi`, `MuhasebeOnayTarihi`, `MusteriyeGonderimTarihi`
- müşteri snapshot alanları: unvan, ad-soyad, vergi no, TCKN, vergi dairesi, adres, e-posta, telefon, kurumsal mı
- muhasebe fişi bağlantıları: `MuhasebeFisId`, `MuhasebeFisOlusturmaTarihi`
- satır snapshot alanları: miktar, birim fiyat, indirim, KDV, istisna, tevkifat, ÖTV, ÖİV, konaklama vergisi

`SatisBelgesiSatiri` satır bazında önemli bir snapshot katmanı sağlıyor. Özellikle:

- `KdvUygulamaTipi`
- `KdvIstisnaKodu`, `KdvIstisnaAciklamasi`
- `TevkifatPay`, `TevkifatPayda`, `TevkifatTutari`
- `KaynakSatirId`

### Faturalama durumu

`TicariBelgeFaturalamaDurumu` şu anda bir orkestrasyon/projeksiyon alanı gibi davranıyor:

- `Uygulanamaz`
- `Baslatilmadi`
- `KesimBekliyor`
- `Kesildi`
- `MusteriyeGonderildi`
- `IptalEdildi`

Bu alan, entegratör yanıtları veya retry geçmişi için yeterli değil; sadece üst seviye durum projeksiyonu.

### Belge yönü

`SatisBelgesiTipiExtensions` tarafında yön otoriter şekilde ayrılmış durumda:

- STYS tarafından düzenlenen giden belgeler:
  - `SatisFaturasi`
  - `AlisIadeFaturasi`
- karşı taraf tarafından düzenlenen gelen belgeler:
  - `AlisFaturasi`
  - `SatisIadeFaturasi`

Yalnız giden belgeler için resmi numara üretimi, UBL/PDF üretimi ve gönderim süreci tasarlanmalı.
Gelen belgeler yanlışlıkla “yeniden düzenlenebilir e-belge” gibi modellenmemeli.

### Mevcut altyapı işaretleri

Koddaki mevcut işaretler şunları gösteriyor:

- dosya saklama için bir altyapı zaten var
- PDF üretimi / saklama için var olan ama burada ayrıntısı henüz tamamlanmamış bir katman bulunuyor
- e-posta gönderimi için altyapı işaretleri var, fakat gönderim geçmişi ve idempotency seviyesi e-belge kalitesi için henüz yetersiz görünüyor
- `FaturaKesAsync` akışı sadece giden, STYS kaynaklı belgeler için resmi numara üretmeye yönelmiş durumda

## Model yeterlilik değerlendirmesi

| İhtiyaç | Mevcut durum | Değerlendirme | Not |
|---|---:|---|---|
| Düzenleyen kurumun unvan, VKN, vergi dairesi ve adres bilgileri | Kısmi | Yetersiz | `Kurum` modelinde vergi no var; unvan, vergi dairesi ve tam adres snapshot’ı e-belge için kalıcı olarak saklanmalı. |
| Alıcı / tedarikçi kimlik ve adres bilgileri | Kısmi | Yetersiz | `CariKart` alanları var; ancak belgenin kesildiği andaki immutable snapshot ayrı tutulmalı. |
| `EBelgeUuid` ve `ResmiFaturaNo` tekillik / idempotency | Var | Kısmi yeterli | Tekillik yalnız DB constraint ile değil, durum geçişleri ve yeniden üretim kurallarıyla desteklenmeli. |
| e-Fatura / e-Arşiv ayrımı ve cari mükellefiyet bilgisi | Var | Kısmi yeterli | `EFaturaMukellefiMi`, `EArsivKapsamindaMi` var; karar anındaki snapshot gerekir. |
| Para birimi ve ödeme bilgileri | Kısmi | Yetersiz | Para birimi ve ödeme snapshot’ı UBL üretiminde bağımsız saklanmalı. |
| KDV, istisna ve tevkifat kodları | Var | Kısmi yeterli | Satır seviyesinde alanlar var; kod listesi doğrulaması ve versiyon etkisi ayrıca yönetilmeli. |
| İade faturasında asıl belge referansı | Var | Kısmi yeterli | `IadeEdilenBelgeId` var; UBL referans snapshot’ı ayrıca tutulmalı. |
| Belge kesildiği andaki değişmez satıcı / alıcı / satır snapshot’ı | Kısmi | Yetersiz | Canlı tablolardan yeniden üretim yerine immutable snapshot gerekir. |
| UBL XML ve PDF saklama yeri, SHA-256 özeti ve sürümü | Kısmi | Yetersiz | Saklama kılavuzuna uygun ayrı artefact kayıtları gerekir. |
| Aynı belgenin tekrar üretilmesi kuralları | Kısmi | Yetersiz | “Tekrar üretim” ile “aynı içeriği yeniden render etme” ayrılmalı. |
| Entegratör bağımsız servis sınırı | Hayır | Yetersiz | Şu an açık ve net bir boundary yok; eklenmeli. |
| Gönderim denemeleri, hata, retry ve idempotency | Kısmi | Yetersiz | Ayrı delivery attempt modeli gerekli. |
| E-posta alıcısı, ekler ve gönderim geçmişi | Kısmi | Yetersiz | Alıcı, ek referansı ve deneme geçmişi ayrı tabloda tutulmalı. |
| İptal edilmiş belgeye ait çıktının korunması | Kısmi | Yetersiz | İptal sonrası artefact’lar korunmalı, immutable kalmalı. |

## Önerilen veri ve servis sınırları

### Önerilen entity kümeleri

1. `EBelgeBelge`
   - iş akışının otoriter kök kaydı
   - belge tipi, yön, durum, tenant, kurum, tesis, cari, kaynak referansları

2. `EBelgeSnapshot`
   - kurum snapshot
   - alıcı / tedarikçi snapshot
   - satır snapshot
   - ödeme / para birimi / vergi snapshot
   - belge kesim anı sabit veriler

3. `EBelgeArtefakt`
   - UBL XML
   - PDF
   - hash
   - sürüm
   - saklama yolu
   - üretim zamanı

4. `EBelgeDeliveryAttempt`
   - entegratör gönderim denemesi
   - request / response metadata
   - hata kodu
   - retry sayısı
   - idempotency key

5. `EBelgeEmailDelivery`
   - e-posta alıcısı
   - ekler
   - gönderim geçmişi
   - mail provider yanıtları

6. `EBelgeCancellationOrReversal`
   - iptal / red / düzeltme ilişkisinin haritalanması
   - iptal edilen belgenin artefaktlarının korunması

### Önerilen servis sınırları

- `EBelgeIssuanceService`
  - yalnız giden belgelerde resmi numara üretir
  - snapshot oluşturur
  - UBL/PDF üretimini tetikler

- `EBelgeRenderService`
  - snapshot’tan UBL XML ve PDF üretir
  - canlı tablolara bağımlı olmamalıdır

- `EBelgeDeliveryService`
  - entegratör bağımsız teslimat orkestrasyonu
  - retry / idempotency / durum geçişi yönetimi

- `EBelgeStorageService`
  - artefakt saklama
  - hash doğrulama
  - versiyonlama

- `EBelgeEmailService`
  - ekli e-posta gönderimi
  - alıcı doğrulama
  - gönderim geçmişi

## Durum makinesi

Önerilen ana akış:

`Taslak -> Hazir -> KesimBekliyor -> Kesildi -> Entegratorde -> Alindi / Hata -> Gonderildi`

İptal hattı:

`Kesildi / Gonderildi -> IptalTalebi -> IptalEdildi`

Önemli kurallar:

- resmi numara yalnız `KesimBekliyor -> Kesildi` geçişinde ve yalnız giden belgeler için
- UBL/PDF üretimi `Kesildi` durumuna bağlanmalı
- gönderim retry’ları belgeyi yeniden düzenlenebilir hale getirmemeli
- iptal edilmiş belgenin artefaktları saklanmalı, ancak yeni revizyon üretimi ayrı kimlikte yapılmalı

## İdempotency ve transaction yaklaşımı

Öneri:

1. belgeyi kilitli oku
2. güncellenebilirlik / silinebilirlik / muhasebe fişi engeli kontrollerini kilit aldıktan sonra yap
3. resmi numara gerekiyorsa aynı transaction içinde ata
4. snapshot üret
5. UBL/PDF artefakt meta kayıtlarını oluştur
6. transaction commit
7. dış sistem gönderimini transaction dışına çıkar

Bu yaklaşım, dış entegratör çağrılarının DB transaction’ını uzatmasını engeller.

İdempotency anahtarları:

- belge id
- belge versiyonu
- artefakt türü
- entegratör gönderim id’si

Tekrar denemede:

- aynı belge için aynı resmi numara yeniden üretilemez
- aynı snapshot hash’i ile aynı artefakt yeniden yazılabilir ama yeni revizyon açmamalıdır
- gönderim denemesi başarısız olduysa hata kaydı korunmalı, eski durum değerleri belgeye geri yazılmamalı

## Güvenlik ve tenant izolasyonu

- her belge tenant sınırıyla okunmalı ve yazılmalı
- snapshot içinde tenant dışı referans taşınmamalı
- dosya saklama yolu tenant / kurum / belge hiyerarşisiyle ayrılmalı
- e-posta alıcıları ve entegratör kimlikleri tenant bazında filtrelenmeli
- UBL/XML ve PDF içeriği, başka tenant’tan veri çekerek yeniden hesaplanmamalı

## Migration etkisi

Olası şema etkileri:

- `EBelgeSnapshot` tablosu veya JSON kolonları
- `EBelgeArtefakt` tablosu
- `EBelgeDeliveryAttempt` tablosu
- `EBelgeEmailDelivery` tablosu
- belge üzerinde snapshot ve artefakt referans kolonları
- hash, sürüm, saklama yolu, mime type alanları

Geriye uyumluluk:

- mevcut `SatisBelgesi` kayıtları için ilk geçişte geriye dönük snapshot oluşturma gerekebilir
- eski belgeler “read-only historical document” olarak işlenmelidir

## Küçük ve sıralı uygulama fazları

### Faz 1: Snapshot temeli

Amaç:

- kurum / tesis / cari snapshot modelini eklemek
- belge satır snapshot’ını netleştirmek

Dar hedefli test önerileri:

- belge kesim anında snapshot değerlerinin canlı kayıttan bağımsız kaldığını doğrula
- cari kart değişince eski belgenin etkilenmediğini doğrula

### Faz 2: Artefakt modeli

Amaç:

- UBL XML ve PDF artefakt kayıtlarını eklemek
- hash ve sürüm saklamak

Dar hedefli test önerileri:

- aynı snapshot’tan üretilen artefakt hash’inin sabit kaldığını doğrula
- artefakt yolunun tenant izolasyonuna uyduğunu doğrula

### Faz 3: Issuance / delivery ayrımı

Amaç:

- resmi numara üretimini yalnız giden belgelerle sınırla
- entegratör gönderimini ayrı servis olarak ayır

Dar hedefli test önerileri:

- gelen belge türlerinde resmi numara üretilemediğini doğrula
- aynı belge için çift resmi numara üretiminin engellendiğini doğrula

### Faz 4: Retry ve geçmiş

Amaç:

- gönderim denemelerini kaydetmek
- hata ve retry davranışını standartlaştırmak

Dar hedefli test önerileri:

- başarısız entegratör çağrısında attempt kaydı oluştuğunu doğrula
- retry sonrası önceki hata kaydının korunduğunu doğrula

### Faz 5: E-posta katmanı

Amaç:

- alıcı, ek ve geçmiş kayıtlarını oluşturmak

Dar hedefli test önerileri:

- e-posta alıcısının snapshot’tan geldiğini doğrula
- eklerin UBL/PDF artefakt referansına dayandığını doğrula

## Açık kararlar ve entegratöre bağlı noktalar

Bu alanlarda varsayım üretmek doğru değil; entegratör ve güncel GİB dokümanı ile netleştirilmeli:

- UBL-TR sürümünün kesin versiyonu
- profil / alt profil seçimi
- zorunlu kod listelerinin güncel kapsamı
- e-Fatura / e-Arşiv ayrımının entegratör tarafında nasıl temsil edileceği
- gönderim yanıtlarının hangi alanlarla normalize edileceği
- PDF oluşturma formatı ve saklama süresi
- e-posta eklerinin zip / tekil / çoklu dosya politikası

## Kritik karar

Kesilmiş bir belgenin UBL/PDF içeriği, daha sonra `Kurum`, `Tesis` veya `CariKart` bilgileri değişse bile değişmemelidir.

Bu nedenle önerilen model:

- canlı tablolardan yeniden üretim: önerilmez
- immutable snapshot: önerilir

### Neden immutable snapshot?

- belge kesim anındaki hukuki ve operasyonel gerçeği korur
- sonradan yapılan master data değişiklikleri eski belgenin içeriğini bozmaz
- UBL, PDF, e-posta eki ve entegratör payload’ı arasında tutarlılık sağlar
- audit ve saklama kılavuzu ile daha uyumludur

### Canlı tablolardan yeniden üretimin riski

- cari kart unvanı veya adresi sonradan değişirse eski UBL/PDF içeriği de değişebilir
- tesis adresi veya kurum bilgisi düzeltmeleri geçmiş belgeyi yanlış hale getirir
- tekrar üretimde hash ve arşiv bütünlüğü bozulur

Sonuç: belge kesim anında snapshot alınmalı; UBL/PDF ve gönderim artefaktları bu snapshot’tan üretilmelidir.

## Sonuç

Mevcut STYS modeli belge yönü, resmi numara akışı ve temel vergi satırları açısından iyi bir başlangıç sağlıyor; ancak e-belge, UBL-TR, PDF ve e-posta katmanı için gereken hukuki sabitlik, artefakt saklama ve delivery geçmişi henüz ayrıştırılmış değil.

En güvenli tasarım:

1. giden belgelerde resmi numara ve snapshot
2. snapshot’tan UBL/PDF üretimi
3. ayrı delivery attempt ve email history tabloları
4. immutable saklama
5. entegratör bağımsız servis sınırı

