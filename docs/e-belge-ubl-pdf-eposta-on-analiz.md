# e-Belge / UBL-TR / PDF / e-Posta Aşaması Ön Teknik Analiz

Tarih: 2026-08-02  
Kapsam: STYS kod tabanında satış belgesi, e-belge issuance, snapshot, render, artefakt saklama ve teslimat sınırları.  
Not: Bu analizde yalnız resmî GİB kaynakları kullanıldı. Erişim tarihi 2026-08-02’dir.

## Resmî kaynaklar

- GİB e-Belge ana sayfa: https://ebelge.gib.gov.tr/anasayfa.html
- GİB e-Fatura mevzuat ve teknik mimari: https://ebelge.gib.gov.tr/efaturamevzuat.html
- e-Fatura Uygulaması Özel Entegrasyon Kılavuzu: https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-Fatura_Uygulamasi_Ozel_Entegrasyon_Kilavuzu_v1.14.pdf
- e-Fatura Uygulaması Saklama Kılavuzu: https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-FaturaUygulamasiSaklamaKilavuzu.pdf
- e-Arşiv Teknik Kılavuzu 1.18: https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-Arsiv_Teknik_Kilavuzu_V.1.18.pdf
- 27.07.2026 duyurusundaki güncel UBL-TR paketi: https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/UBLTR_1.2.1_Kilavuzlar.zip

GİB ana sayfasındaki 27.07.2026 duyurusuna göre UBL-TR (Kod Listeleri) Kılavuzu güncellenmiş ve değişikliklerin 14.09.2026 itibarıyla devreye alınacağı ilan edilmiştir. Bu nedenle kod listeleri ve paket seçimi için tek otorite güncel GİB duyurusudur.

## 1) Mevcut durum

### Satış belgesi modeli

Ana ticari kök `SatisBelgesi` ve satırları `SatisBelgesiSatiri`’dır. Bu yapı ticari ve muhasebesel belgenin otoriter aggregate root’u olarak kalmalıdır.

`SatisBelgesi` üzerinde görülen kritik alanlar:

- kurum / tesis / cari bağları: `KurumId`, `TesisId`, `CariKartId`
- belge yönü ve kaynak izleri: `BelgeTipi`, `KaynakModul`, `KaynakTipi`, `KaynakId`
- resmi belge alanları: `ResmiFaturaNo`, `EBelgeUuid`
- iade referansı: `IadeEdilenBelgeId`
- muhasebe ve faturalama durumları: `TicariDurum`, `MuhasebeDurumu`, `FaturalamaDurumu`
- zaman alanları: `FaturaKesimTarihi`, `MuhasebeOnayinaGonderilmeTarihi`, `MuhasebeOnayTarihi`, `MusteriyeGonderimTarihi`
- müşteri snapshot alanları: unvan / ad-soyad, vergi no, TCKN, vergi dairesi, adres, e-posta, telefon, kurumsal mı
- muhasebe fişi bağlantıları: `MuhasebeFisId`, `MuhasebeFisOlusturmaTarihi`

`SatisBelgesiSatiri` satır seviyesinde zaten güçlü bir ticari snapshot sağlar:

- `KdvUygulamaTipi`
- `KdvIstisnaKodu`, `KdvIstisnaAciklamasi`
- `TevkifatPay`, `TevkifatPayda`, `TevkifatTutari`
- `SatirToplami`
- `KaynakSatirId`

### Belge yönü

`SatisBelgesiTipiExtensions` yönü otoriter biçimde ayırıyor:

- STYS tarafından düzenlenen giden belgeler:
  - `SatisFaturasi`
  - `AlisIadeFaturasi`
- karşı taraf tarafından düzenlenen gelen belgeler:
  - `AlisFaturasi`
  - `SatisIadeFaturasi`

Yalnız giden belgeler için resmî numara, UBL, PDF ve gönderim akışı tasarlanmalıdır. Gelen belgeler yanlışlıkla yeniden düzenlenebilir e-belge gibi modellenmemelidir.

### Faturalama durumu

`TicariBelgeFaturalamaDurumu` bir ayrıntılı e-belge yaşam döngüsünden türetilen üst seviye projection olarak değerlendirilmelidir:

- `Uygulanamaz`
- `Baslatilmadi`
- `KesimBekliyor`
- `Kesildi`
- `MusteriyeGonderildi`
- `IptalEdildi`

Bu alan çift yönlü ve çelişebilen bağımsız bir durum alanı olmamalıdır. Ayrıntılı issuance / delivery state `EBelgeKaydi` altında tutulmalı, `TicariBelgeFaturalamaDurumu` buradan üretilmelidir.

### Mevcut altyapı tespiti

Kod tabanındaki gözlem şu şekilde:

- `KurumLogoStorageOptions` kurum logosuna özel yerel dosya saklama içindir; genel artefakt storage abstraction değildir.
- `OdaDolulukRaporPdfService` rapor PDF’i üretir; kalıcı e-belge PDF saklama altyapısı değildir.
- genel bir e-posta gönderim servisi / provider kaydı görünmüyor.
- e-belge için storage abstraction, PDF renderer ve e-posta provider altyapısı yeni geliştirilmelidir.

## 2) Otorite sınırı ve kayıt modeli

### Kesin karar

- `SatisBelgesi` ticari ve muhasebesel belgenin otoriter aggregate root’u olarak kalır.
- Yeni e-belge kaydı ikinci bir ticari belge kökü olmaz.
- Önerilen ad `EBelgeKaydi`dır; `EBelgeBelge` yerine daha açıktır.
- `EBelgeKaydi`, `SatisBelgesiId` üzerinden `SatisBelgesi`’ne bire bir bağlı olmalıdır.
- `EBelgeKaydi` yalnız issuance, snapshot, render, artefakt ve delivery yaşam döngüsünün otoritesi olmalıdır.

### Otorite tablosu

| Veri / karar | Otoriter kayıt | Not |
|---|---|---|
| Belge tipi | `SatisBelgesi` | Ticari sınıf burada kalır. |
| Kurum / tesis / cari ilişkisi | `SatisBelgesi` | Ticari bağın kökü burada kalır. |
| Tutarlar ve satır hesapları | `SatisBelgesi` + `SatisBelgesiSatiri` | Muhasebesel iş kuralı burada otoriterdir. |
| Issuance anı snapshot | `EBelgeKaydi` + `EBelgeSnapshot` | Immutable çıktıdır. |
| Resmî numara | `EBelgeKaydi` | Giden belgelerde üretilir. |
| `EBelgeUuid` | `EBelgeKaydi` | UUID otoritesi burada olur. |
| UBL / PDF artefaktları | `EBelgeKaydi` + `EBelgeArtefakt` | Hash, yol, sürüm burada tutulur. |
| Delivery / retry state | `EBelgeKaydi` + `EBelgeDeliveryAttempt` | Entegratör ve worker state burada olur. |
| `TicariBelgeFaturalamaDurumu` | Projection | `EBelgeKaydi` üzerinden üretilir. |

İki tarafta bağımsız ve çelişebilecek durum yazımları tasarlanmamalıdır.

## 3) Tekillik ve DB kuralları

Mevcut durumun doğru kaydı:

- `ResmiFaturaNo` tekilliği mevcutta `KurumId + ResmiFaturaNo` üzerindedir ve yalnız aktif kayıtlar için geçerlidir.
- `EBelgeUuid` alanı vardır; fakat benzersiz indeks ve üretim akışı görünmüyor.

Yeni model için öneri:

- `EBelgeKaydi.SatisBelgesiId` zorunlu ve benzersiz olmalıdır.
- `EBelgeUuid` null değilse benzersiz olmalıdır.
- `BelgeVersiyonu` ile snapshot / artefakt tekillikleri açıkça tanımlanmalıdır.
- kesilmiş veya iptal edilmiş e-belge kayıtları soft-delete edilerek kimliklerin yeniden kullanımına izin verilmemelidir.
- uygulama seviyesindeki idempotency, DB unique constraint’lerinin yerine geçmez; sadece onları tamamlar.

## 4) Transaction sonrası güvenilir yürütme

Önerilen yürütme zinciri:

1. `FaturaKesAsync` içinde kilitli belge oku.
2. resmî numara gerekiyorsa aynı transaction içinde ata.
3. immutable snapshot oluştur.
4. `EBelgeKaydi` satırını atomik oluştur.
5. gerekiyorsa transactional outbox kaydını atomik oluştur.
6. transaction commit olsun.
7. worker, UBL/PDF render ve entegratör çağrısını transaction dışında yürütsün.

Bu modelde UBL/PDF üretimi ve entegratör çağrısı DB transaction’ının dışında kalır. Transaction commit’i ile worker’ın başlaması arasındaki kayıp pencere transactional outbox ile kapatılır.

### Worker ve retry davranışı

- worker lease / claim ile tekil çalışmalıdır.
- claim edilen iş için retry sayacı, hata kodu, hata metni ve deneme zamanı ayrı kaydedilmelidir.
- aynı iş tekrar işlendiğinde ikinci UUID, ikinci resmî numara veya yeni snapshot versiyonu oluşmamalıdır.
- aynı işin tekrar çalışması sadece aynı issuance kimliğinin devamı olmalıdır.

### Deterministik artefakt yazımı

Önerilen sıra:

1. snapshot canonical hash’i ve `EBelgeUuid` ile deterministik storage key üret.
2. UBL XML / PDF byte’larını üret.
3. storage’a yaz.
4. yazılan dosyanın SHA-256 özetini hesapla.
5. DB’de artefakt metadata’yı upsert et.

Bu sırada:

- storage’a yazılıp DB kaydı oluşmaması halinde aynı deterministik key ile retry edilir; orphan blob taraması gerekir.
- DB kaydı oluşup dosya yazılmaması halinde metadata “missing blob” olarak işaretlenir; worker yeniden render / yazma yapar.

### Outbox penceresi

Commit sonrası worker başlamadan işin kaybolmaması için transactional outbox gerekir. Böylece:

- DB commit tamamlanır.
- outbox satırı kalıcıdır.
- worker bu satırı claim ederek render / delivery adımını sürdürür.

## 5) Snapshot kapsamı

`EBelgeSnapshot` aşağıdaki verileri issuance anında immutable olarak taşımalıdır:

- düzenleyen kurum unvanı
- VKN
- vergi dairesi
- adres
- iletişim bilgileri
- tesis bilgileri
- alıcı / tedarikçi kimlik bilgileri
- adres ve iletişim bilgileri
- e-Fatura / e-Arşiv karar anındaki mükellefiyet bilgisi
- belge tarihi
- belge saati
- para birimi
- döviz kuru
- ödeme bilgileri
- tüm satır alanları
- tüm indirim alanları
- tüm vergi alanları
- iade edilen belgenin numara snapshot’ı
- iade edilen belgenin UUID snapshot’ı
- iade edilen belgenin tarih snapshot’ı
- snapshot şema sürümü
- canonical hash

### Mevcut alanların yeniden kullanımı

`SatisBelgesi` üzerindeki müşteri alanları ile `SatisBelgesiSatiri` satırları, snapshot üretiminde kaynak olarak yeniden kullanılmalıdır. Ancak bu alanların kendisi ikinci bir writable mali veri deposu haline getirilmemelidir.

Öneri:

- `SatisBelgesi` ticari gerçeğin kaydıdır.
- `EBelgeSnapshot` bu gerçeğin issuance anındaki immutable izdüşümüdür.
- aynı veri iki yerde editable olarak tutulmaz.

Eksik olup `EBelgeSnapshot` içinde saklanması gerekenler:

- düzenleyen kurumun tam unvan / VKN / vergi dairesi / adres / iletişim snapshot’ı
- tesisin issuance anı tam iletişim snapshot’ı
- alıcı / tedarikçi karar anı kimlik ve adres snapshot’ı
- e-Fatura / e-Arşiv kararı
- belge tarihi-saat bilgisi
- döviz kuru ve ödeme bilgileri
- iade referansının numara / UUID / tarih üçlüsü
- snapshot şema sürümü
- canonical hash

## 6) Durum makinesi

Önerilen ana akış:

`Taslak -> Hazir -> KesimBekliyor -> Kesildi -> Entegratorde -> Alindi / Hata -> Gonderildi`

İptal hattı:

`Kesildi / Gonderildi -> IptalTalebi -> IptalEdildi`

Kurallar:

- resmî numara yalnız giden belgelerde üretilir.
- UBL / PDF üretimi `Kesildi` state’ine bağlıdır.
- delivery retry’ları belgeyi yeniden düzenlenebilir hale getirmez.
- iptal edilmiş belgenin artefaktları korunur.

## 7) Güvenlik ve tenant izolasyonu

- her belge tenant sınırıyla okunmalı ve yazılmalıdır.
- snapshot içinde tenant dışı referans taşınmamalıdır.
- storage key tenant / kurum / belge hiyerarşisine göre ayrılmalıdır.
- e-posta alıcıları ve entegratör kimlikleri tenant bazında filtrelenmelidir.
- UBL XML ve PDF, başka tenant verisi çekilerek yeniden hesaplanmamalıdır.

## 8) Migration etkisi

Muhtemel şema etkileri:

- `EBelgeKaydi` tablosu
- `EBelgeSnapshot` tablosu
- `EBelgeArtefakt` tablosu
- `EBelgeDeliveryAttempt` tablosu
- `EBelgeEmailDelivery` tablosu
- belge üzerinde snapshot / artefakt referans kolonları
- hash, sürüm, storage key, mime type alanları

Geriye uyumluluk:

- mevcut `SatisBelgesi` kayıtları için ilk geçişte geriye dönük snapshot çıkarma gerekebilir.
- eski belgeler read-only historical document olarak ele alınmalıdır.

## 9) Küçük ve sıralı uygulama fazları

### Faz 1

Yalnız şu kapsam:

- `EBelgeKaydi`
- immutable `EBelgeSnapshot`
- `SatisBelgesiId` ile bire bir bağlantı
- kesin DB tekillikleri
- `FaturaKesAsync` içinde atomik ve idempotent snapshot oluşturma

Faz 1 dışında tutulacaklar:

- UBL üretimi
- PDF üretimi
- entegratör çağrısı
- e-posta gönderimi
- frontend

Dar hedefli test önerileri:

- bir `SatisBelgesi` için ikinci `EBelgeKaydi` oluşamadığını doğrula
- aynı belgenin snapshot’ının canlı master data değişikliklerinden etkilenmediğini doğrula
- resmî numara ve UUID tekilliğini doğrula

### Faz 2

Yalnız:

- transactional outbox
- worker claim / lease
- UBL / PDF render
- artefakt saklama
- delivery retry ve hata kaydı

Dar hedefli test önerileri:

- aynı işin ikinci UUID veya ikinci resmî numara üretmediğini doğrula
- storage miss ve DB miss toparlama senaryolarını doğrula
- retry sonrası aynı deterministic key ile devam edildiğini doğrula

### Faz 3

Yalnız:

- entegratör provider uyarlaması
- e-posta provider uyarlaması
- raporlama / saklama ayrıntıları

Dar hedefli test önerileri:

- provider response mapping’in normalize edildiğini doğrula
- e-posta eklerinin doğru artefaktlardan üretildiğini doğrula

## 10) Açık kararlar ve entegratöre bağlı noktalar

Varsayım üretilmemesi gereken noktalar:

- UBL-TR sürümünün kesin devreye giriş tarihi
- profil / alt profil seçimi
- zorunlu kod listelerinin güncel kapsamı
- e-Fatura / e-Arşiv ayrımının entegratör tarafında nasıl normalize edileceği
- provider response kodları
- PDF oluşturma formatı ve saklama süresi
- e-posta eklerinin zip / tekil / çoklu dosya politikası

Karar:

- 14.09.2026 öncesi, resmî GİB duyurusundaki güncel paket ve kod listeleri uygulanmalıdır.
- 14.09.2026 sonrası için yeni paket / kod listesi ancak güncel GİB yayınları doğrulandıktan sonra devreye alınmalıdır.
- entegratör seçilmeden profil ve provider yanıt kodları hakkında varsayım yapılmamalıdır.

## 11) Kritik karar: immutable snapshot

Kesilmiş bir belgenin UBL / PDF içeriği, daha sonra `Kurum`, `Tesis` veya `CariKart` bilgileri değişse bile değişmemelidir.

Bu nedenle:

- canlı tablolardan yeniden üretim: önerilmez
- immutable snapshot: önerilir

### Neden immutable snapshot?

- belge kesim anındaki hukuki ve operasyonel gerçeği korur.
- sonradan yapılan master data değişiklikleri geçmiş belgeyi bozmaz.
- UBL, PDF, e-posta eki ve entegratör payload’ı arasında tutarlılık sağlar.
- saklama kılavuzu ve audit ihtiyacıyla uyumludur.

### Canlı tablolardan yeniden üretimin riski

- cari kart unvanı veya adresi sonradan değişirse eski UBL / PDF değişebilir.
- tesis adresi veya kurum bilgisi düzeltmesi geçmiş belgeyi yanlış hale getirir.
- tekrar üretimde hash ve arşiv bütünlüğü bozulur.

Sonuç: belge kesim anında snapshot alınmalı; UBL / PDF ve gönderim artefaktları bu snapshot’tan üretilmelidir.

## 12) Sonuç

Mevcut STYS modeli belge yönü, resmî numara akışı ve satır vergi alanları açısından iyi bir temel sağlıyor. Ancak e-belge için gerekli hukuki sabitlik, artifact saklama ve delivery history ayrıştırması henüz tamamlanmış değil.

Önerilen güvenli tasarım:

1. giden belgelerde resmî numara ve immutable snapshot
2. `EBelgeKaydi` üzerinden issuance otoritesi
3. transactional outbox ile güvenilir handoff
4. UBL / PDF / entegratör / e-posta fazlarının ayrılması
5. entegratör bağımsız servis sınırı

