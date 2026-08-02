# e-Belge / UBL-TR / PDF / e-Posta Aşaması Ön Teknik Analiz

Tarih: 2026-08-02  
Kapsam: STYS kod tabanında satış belgesi, e-belge issuance, snapshot, render, artefakt saklama ve teslimat sınırları.  
Not: Bu analizde yalnız resmî GİB kaynakları kullanıldı. Erişim tarihi 2026-08-02’dir.

## Resmî kaynaklar

- GİB e-Belge ana sayfa: https://ebelge.gib.gov.tr/anasayfa.html
- GİB e-Fatura mevzuat ve teknik mimari: https://ebelge.gib.gov.tr/efaturamevzuat.html
- e-Fatura Uygulaması Özel Entegrasyon Kılavuzu: https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-Fatura_Uygulamasi_Ozel_Entegrasyon_Kilavuzu_v1.14.pdf
- e-Fatura Paketi: https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-FaturaPaketi.zip
- e-Fatura Uygulaması Saklama Kılavuzu: https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-FaturaUygulamasiSaklamaKilavuzu.pdf
- e-Arşiv Teknik Kılavuzu 1.18: https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-Arsiv_Teknik_Kilavuzu_V.1.18.pdf
- e-Arşiv Fatura Paketi: https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/earsiv_paket_v1.1_8.zip
- 27.07.2026 duyurusundaki güncel UBL-TR paketi: https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/UBLTR_1.2.1_Kilavuzlar.zip

GİB ana sayfasındaki 27.07.2026 duyurusuna göre güncellemeler 14.09.2026 tarihinde devreye alınacaktır. 13.09.2026 sonuna kadar o tarihte yürürlükte olan önceki paket / kod listesi seti kullanılmalı, 14.09.2026 itibarıyla duyuruda yayımlanan yeni paketler kullanılmalıdır. Paket seçimi issuance tarihi ve effective-date registry üzerinden yapılmalıdır.

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

Bu alan çift yönlü ve çelişebilen bağımsız bir durum alanı olmamalıdır. `TicariBelgeFaturalamaDurumu` mevcut `SatisBelgesi` yaşam döngüsünün otoriter projection’ıdır; `EBelgeKaydi`'ndan türetilmesi sonraki fazlarda ayrıca kararlaştırılabilir.

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
- `EBelgeKaydi` ayrıca e-Fatura / e-Arşiv kararının otoritesidir; snapshot şema sürümü, belge versiyonu, canonical JSON ve canonical hash ise yalnız `EBelgeSnapshot` üzerinde otoritedir. Aynı alanların `EBelgeKaydi` üzerinde ikinci writable kopyası oluşturulmaz.
- `SatisBelgesi.EBelgeUuid` alanı varsa onun kaderi nettir: Faz 1 geçişinde legacy uyumluluk için yalnız okunur tutulur, writable otorite `EBelgeKaydi.EBelgeUuid` olur. İki tabloda bağımsız writable UUID bırakılmaz; gerekirse migration ile mevcut değerler `EBelgeKaydi`’na backfill edilir ve sonra `SatisBelgesi` tarafı read-only compatibility alanına düşürülür.

### Otorite tablosu

| Veri / karar | Otoriter kayıt | Not |
|---|---|---|
| Belge tipi | `SatisBelgesi` | Ticari sınıf burada kalır. |
| Kurum / tesis / cari ilişkisi | `SatisBelgesi` | Ticari bağın kökü burada kalır. |
| Tutarlar ve satır hesapları | `SatisBelgesi` + `SatisBelgesiSatiri` | Muhasebesel iş kuralı burada otoriterdir. |
| Issuance anı snapshot | `EBelgeKaydi` + `EBelgeSnapshot` | Immutable çıktıdır; değiştirilebilir kaynak değildir. |
| Resmî numara | `SatisBelgesi` | Faz 1’de tek otorite buradadır; snapshot’a kopyalanabilir ama snapshot otoriter değildir. |
| e-Fatura / e-Arşiv kararı | `EBelgeKaydi` | İssuance kararı burada otoriterdir. |
| `EBelgeUuid` | `EBelgeKaydi` | UUID otoritesi burada olur; `SatisBelgesi` tarafı legacy uyumluluk dışında writable kalmaz. |
| Snapshot şema sürümü / belge versiyonu / canonical JSON / canonical hash | `EBelgeSnapshot` | Bu alanlar snapshot’ta otoriterdir; `EBelgeKaydi` üzerinde writable kopya tutulmaz. |
| UBL / PDF artefaktları | `EBelgeKaydi` + `EBelgeArtefakt` | Hash, yol, sürüm burada tutulur. |
| Delivery / retry state | `EBelgeKaydi` + `EBelgeDeliveryAttempt` | Entegratör ve worker state burada olur. |
| `TicariBelgeFaturalamaDurumu` | Projection | Faz 1’de `SatisBelgesi` üzerinde otoriter kalır; `EBelgeKaydi` üzerinden projection üretimi sonraki faz kararıdır. |

İki tarafta bağımsız ve çelişebilecek durum yazımları tasarlanmamalıdır.

## 3) Tekillik ve DB kuralları

Mevcut durumun doğru kaydı:

- `ResmiFaturaNo` tekilliği mevcutta `KurumId + ResmiFaturaNo` üzerindedir ve yalnız aktif kayıtlar için geçerlidir.
- `EBelgeUuid` alanı vardır; fakat benzersiz indeks ve üretim akışı görünmüyor.

Yeni model için öneri:

- `EBelgeKaydi.SatisBelgesiId` zorunlu ve benzersiz olmalıdır; unique indeks IsDeleted filtresi olmadan tanımlanmalıdır.
- `EBelgeKaydi.EBelgeUuid` null olmayan kayıtlar arasında benzersiz olmalıdır; unique indeks IsDeleted filtresi olmadan tanımlanmalıdır.
- böylece soft-delete edilmiş kaydın `SatisBelgesiId` veya UUID’si yeniden kullanılamaz.
- `BelgeVersiyonu` ile snapshot / artefakt tekillikleri açıkça tanımlanmalıdır.
- Faz 1’de `BelgeVersiyonu = 1` olmalıdır; idempotent retry yeni versiyon oluşturmaz.
- canonical hash global unique yapılmamalıdır; aynı içerikli farklı belgeler bulunabilir.
- kesilmiş veya iptal edilmiş e-belge kayıtları soft-delete edilse bile kimliklerin yeniden kullanımına izin verilmemelidir.
- uygulama seviyesindeki idempotency, DB unique constraint’lerinin yerine geçmez; sadece onları tamamlar.
- `SatisBelgesi` tarafındaki `KurumId + ResmiFaturaNo` unique indeksinden IsDeleted filtresinin kaldırılması önerilir; ancak bu geçişten önce tarihsel mükerrer kayıt kontrolü ve düzeltme planı yapılmalıdır.

## 4) Transaction sonrası güvenilir yürütme

Faz 1 için atomik sıra:

1. satış belgesini transaction içinde kilitli ve güncel oku.
2. giden belge, muhasebe durumu ve idempotency kurallarını doğrula.
3. mevcut sayaçtan `ResmiFaturaNo` üret.
4. `SatisBelgesi` üzerinde `ResmiFaturaNo`, `FaturaKesimTarihi` ve `Kesildi` durumunu ata.
5. aynı transaction içinde `EBelgeKaydi` ve immutable `EBelgeSnapshot` oluştur.
6. tek `SaveChanges` / commit ile tamamla.
7. hata halinde sayaç dahil her şeyi rollback yap.

Faz 1’de UBL / PDF üretimi ve entegratör çağrısı DB transaction’ının dışında tutulur; bu adımlar sonraki fazların konusudur. Aynı çağrı tekrarlandığında ikinci numara, UUID, `EBelgeKaydi` veya snapshot oluşturulmaz.

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
- geçmiş belgeler için canlı `Kurum` / `Tesis` / `CariKart` verilerinden geriye dönük hukuki snapshot üretimi yapılmaz.
- legacy belgeler read-only tutulur; yalnız güvenilir tarihsel veri varsa ayrıca veri envanteri/migration aşamasında ele alınır.

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

`SatisBelgesi` üzerindeki kesim öncesi durumlar:

`Taslak -> Hazir -> KesimBekliyor`

`FaturaKesAsync` sırasında:

`KesimBekliyor -> Kesildi`

ve aynı anda `EBelgeKaydi` + snapshot oluşturulur.

`EBelgeKaydi`, Faz 1’de doğrudan `SnapshotHazir` benzeri bir başlangıç durumuyla oluşmalıdır. `Taslak / Hazir / KesimBekliyor` durumları henüz var olmayan `EBelgeKaydi` altında gösterilmemelidir.

Faz 2 sonrasındaki e-belge durumu örneği:

`SnapshotHazir -> RenderBekliyor -> ArtefaktHazir -> GonderimBekliyor -> Gonderiliyor -> Gonderildi / Hata -> IptalEdildi`

Faz 1’de mevcut `SatisBelgesiDurumProjection` ve otoriter `FaturalamaDurumu` yapısı değiştirilmemelidir. İleride projection yapılacaksa bunun `SatisBelgesi + opsiyonel EBelgeKaydi` üzerinden üretileceği ayrı karar olarak not edilmelidir; Faz 1’de bu projection'ın otoritesi hâlâ `SatisBelgesi`'dir.

İptal hattı:

`Kesildi / Gonderildi -> IptalTalebi -> IptalEdildi`

Kurallar:

- resmî numara yalnız giden belgelerde üretilir.
- UBL / PDF üretimi sonraki fazların konusudur; Faz 1’de yoktur.
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
- migration
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

### Faz 2A

Yalnız:

- transactional outbox
- worker claim / lease

Dar hedefli test önerileri:

- aynı outbox işinin tek worker tarafından claim edildiğini doğrula
- retry sonrası aynı işin ikinci kez sahiplenilmediğini doğrula

### Faz 2B

Yalnız:

- UBL / PDF render
- artefakt saklama

Dar hedefli test önerileri:

- aynı snapshot’tan üretilen artefakt hash’inin sabit kaldığını doğrula
- storage miss ve DB miss toparlama senaryolarını doğrula

### Faz 2C

Yalnız:

- delivery retry ve hata kaydı

Dar hedefli test önerileri:

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

- 13.09.2026 sonuna kadar o tarihte yürürlükte olan önceki paket / kod listesi seti kullanılmalıdır.
- 14.09.2026 itibarıyla duyuruda yayımlanan yeni paketler kullanılmalıdır.
- paket seçimi issuance tarihi ve effective-date registry üzerinden yapılmalıdır.
- eski paket bağlantısı veya içeriği resmî kaynaktan doğrulanamıyorsa tahmin üretilmemelidir; bu durum saklanması gereken sürümlü artefakt / checksum kararı olarak işaretlenmelidir.
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
