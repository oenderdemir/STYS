# Ödeme İzleme Çapraz-Kaynak Araştırması + Fiş Doğrulama — Uygulama Raporu

Tarih: 2026-07-26
Esas alınan commit: `85954e6ff9e9d6c790d653b32e7bbbed169516be`

Bu tur, önceki committe eklenen finansal güvenlik kontrollerini **koruyarak** (geçmiş tarihli POS
hesaplamama, durum allowlist'i, tesis bazlı gruplama, para birimi kontrolleri, uyarılı tutar ayrımı,
birleşik Nakit–Banka Pozisyonu endpoint'i) aşağıdaki eksikleri tamamlar. Veri modeli değişikliği
yapılmamıştır; **migration gerekmedi**.

---

## 1. Analiz — gerçek veri modeli (varsayım yok)

### Ödeme zinciri kaynakları ve tesis kapsamı

| Kaynak | Tesis kapsamı nasıl uygulanır | Ödeme bağı |
|---|---|---|
| `TahsilatOdemeBelgesi` | **Kendi `TesisId`'si YOK** → `CariKart.TesisId` üzerinden | `KaynakModul`/`KaynakId`, `KapatilacakCariHareketId`, `MuhasebeFisId`, `KasaBankaHesapId` |
| `CariHareket` | `CariKart.TesisId` | `KaynakModul=TahsilatOdemeBelgesi` + `KaynakId`, `IliskiliCariHareketId` (kapama) |
| `PosTahsilatValor` | **Kendi `TesisId`'si VAR** | `TahsilatOdemeBelgesiId`, `BagliBankaHesapId`, `MuhasebeFisId`, `TersKayitMuhasebeFisId` |
| `KasaHareket` / `BankaHareket` | `KasaBankaHesap.TesisId` | `KaynakModul`/`KaynakId`, `CariKartId` |
| `MuhasebeFis` | **Kendi `TesisId` + `MaliYil` + `Donem`** | `KaynakModul`/`KaynakId` |
| `MuhasebeFisSatir` | fişten devralır | **`KasaBankaHesapId` ve `CariKartId` alanları VAR** |
| `Rezervasyon` | kendi `TesisId` | `RezervasyonOdeme.TahsilatOdemeBelgesiId` (**ters yön FK**) → `ReferansNo` |

### Kritik bulgular

- **`MuhasebeFisSatir.KasaBankaHesapId` gerçekten var** → "fiş satırında doğru banka/kasa hesabı
  etkilenmiş mi" kontrolü uydurma değil, gerçekten yapılabiliyor. Bu tur bu kontrol eklendi.
- **`MuhasebeHesapPlani.TesisId` NULLABLE** → hesap planı tesise özel de olabilir, paylaşılan da.
  (Önceki turdaki "global tablodur" yorumu yanlıştı; **yorum düzeltildi**, gruplama mantığı zaten
  doğruydu ve korundu.)
- **`Fatura` / `Tahakkuk` / `Konaklama` entity'si YOK.** En yakın gerçek karşılıklar
  `Rezervasyon.ReferansNo` ve `SatisBelgesi.BelgeNo`. Bu nedenle "fatura/tahakkuk numarası" filtresi
  **uydurulmadı**; rezervasyon referans no filtresi eklendi.
- **POS/banka işlem referansı alanı YOK.** `TahsilatOdemeBelgesi` üzerinde `BelgeNo` dışında
  referans/dekont alanı bulunmuyor → bu filtre eklenemedi, sınırlama olarak raporlanıyor.
- `MuhasebeFisDurumlari`: `Taslak` / `Onayli` / `Iptal` / `TersKayit`. Bakiyeye yalnızca
  **Onayli + TersKayit** yansır (Hızlı Mizan kuralıyla aynı).
- `MuhasebeDonem`: `TesisId` + `MaliYil` + `DonemNo` + `BaslangicTarihi`/`BitisTarihi` + `KapaliMi`.

---

## 2. Ödeme İzleme artık ödeme belgesi merkezli değil (madde 2)

Yeni bileşen: **`OdemeCaprazAramaService`** (`GET ui/muhasebe/odeme-izleme/capraz-arama`).

Her kaynak için **ayrı ve dar bir aday sağlayıcı** çalışır; sonuçlar ortak bir tekilleştirme
anahtarında birleşir. Tek devasa sorgu yoktur.

### Araştırılan kaynaklar

`TahsilatOdemeBelgesi` · `CariHareket` · `PosTahsilatValor` · `KasaHareket` · `BankaHareket` ·
`MuhasebeFis`

### Tespit edilen kopukluklar

| Kod | Anlamı |
|---|---|
| `OdemeBaglantisiOlmayanMuhasebeFisi` | Fiş tahsilat kaynaklı ama kaynak belge yok |
| `OdemeBelgesiOlmayanCariHareket` | Cari hareket belgeden doğmuş ama belge yok |
| `MuhasebeFisiOlmayanOdemeBelgesi` | Nakit/banka/POS ödemesi ama fiş yok |
| `CariHareketEtkisiOlmayanOdemeBelgesi` | Borç kapatma işaretli ama kapama hareketi yok |
| `ValorKaydiOlmayanPosTahsilati` | Kredi kartı tahsilatı ama valör kaydı yok |
| `HedefBankaHesabiOlmayanValor` | Valör hedef hesabı yok/pasif **veya tesisi uyuşmuyor** |
| `OdemeBelgesiOlmayanKasaHareketi` / `...BankaHareketi` | Kasa/banka hareketi belgeden doğmuş ama belge yok |
| `SoftDeleteIliskiNedeniyleGorunmeyen` | Kaynak belge soft-delete edilmiş |

### Tekilleştirme

Ödeme belgesi id'si bilinen her kaynak **aynı** `BELGE:{id}` anahtarını üretir. Böylece aynı mali
işlem belge + cari hareket + valör + fiş kayıtlarında bulunsa da **tek aday** olur;
`BulunduguKaynaklar` listesi hangi kaynaklarda göründüğünü gösterir. Belge id'si bilinmeyen kayıtlar
kendi kaynak-özel anahtarlarını (`CH:`, `KH:`, `BH:`, `FIS:`) kullanır.

Sayfalama: server-side, **maksimum 200** page size, kararlı sıralama (tarih ↓, sonra tekilleştirme
anahtarı), her sağlayıcıda `AsNoTracking` + projection. Kopukluk tespitleri EF'in `EXISTS` alt
sorgusuna çevirdiği `Any(...)` ile yapılır — **N+1 yok**.

---

## 3. `BakiyeyeDahilMi` artık fişi gerçekten doğruluyor (madde 3)

Yeni paylaşılan bileşen: **`MuhasebeFisDogrulama`** (saf, DB'siz) + `DogrulanmisFis` projeksiyonu.

`MuhasebeFisId` dolu olması **tek başına yeterli değil**. Doğrulananlar:

| Kontrol | Neden kodu |
|---|---|
| Fiş gerçekten var mı | `FisBulunamadi` |
| Soft-delete edilmiş mi | `FisSoftDeleteEdilmis` |
| Durumu mali etki oluşturuyor mu (Onaylı/TersKayit) | `FisDurumuMaliEtkiOlusturmuyor` |
| Doğru tesise mi ait | `FisFarkliTesiseAit` |
| Fiş tarihi beklenen dönem aralığında mı | `FisDonemiUyumsuz` |
| Fiş **satırlarında** beklenen kasa/banka hesabı etkilenmiş mi | `FisSatirindaBeklenenHesapYok` |

Fiş `IgnoreQueryFilters()` ile aranır — böylece "bulunamadı" ile "soft-delete edilmiş" ayırt edilir.

### Response alanları

`BakiyeyeDahilMi` · `BakiyeyeDahilEdilmeDurumu` (`TamamenDahil`/`KismenDahil`/`DahilDegil`) ·
`BakiyeyeDahilEdilmemeNedenKodlari` · `BakiyeyeDahilEdilmemeAciklamalari` · `EtkiledigiCariVeyaBorc` ·
`EtkiledigiTutar` · `EtkiledigiParaBirimi`

Kurallar: cari hareket yoksa `true` üretilmez; fiş zorunluyken geçersizse `TamamenDahil` üretilmez;
aktif belge tek başına mali etki kanıtı sayılmaz.

---

## 4. POS–valör–fiş doğrulaması güçlendirildi (madde 4)

`PosValorFinansalSiniflandirici` artık sorgu katmanından **doğrulanmış fiş** ve **tesis** bilgisi
alıyor:

- `Aktarildi` + fiş doğrulanamadı → `AktarimFisiDogrulanamadi` (normal kabul **edilmez**)
- `AktarimFisiIptalEdildi` + ters kayıt fişi doğrulanamadı → aynı şekilde
- Fiş satırında hedef banka hesabı etkilenmemişse geçersiz sayılır

Servis, ilgili tüm fişleri **tek sorguda** (`IgnoreQueryFilters`) ve fiş-satırı hesap etkilerini
**tek gruplu sorguda** yükler — N+1 yok.

---

## 5. Valör–banka hesabı tesis uyumu (madde 5)

`ValorProjeksiyon`'a `TesisId` eklendi. Bir valör, yalnızca `BagliBankaHesapId` eşleşti diye başka
tesisin hesabına dahil edilemez:

- Valör tesisi ≠ banka hesabı tesisi → `ValorBankaHesabiTesisUyumsuz`
- Normal toplamdan çıkarılır, uyarılı tutara eklenir
- Yetkisiz tesis verisi zaten sorguya girmez (kapsam `tesisIds` ile uygulanır)

---

## 6. Cari döküm: tarih kapsamı + gerçek devreden bakiye (madde 6)

- **Devreden bakiye** = açılış bakiyesi + tarih başlangıcından **önceki aktif** hareketlerin net
  etkisi (para birimi bazında, ayrı gruplu sorgu). Artık başlangıç öncesi hareketler yok sayılmıyor.
- Dönem içi hareketler ayrı; `AciklananKalanBakiye` = devreden + dönem içi net.
- **POS kayıtları cari hareketlerle aynı tarih kapsamını** kullanır. Kullanılan tarih alanı:
  kaynak `TahsilatOdemeBelgesi.BelgeTarihi` — cari hareket de bu belgeden doğduğu için tutarlıdır.
  `OdemeTarihi`/`BeklenenValorTarihi`/`AktarimTarihi` birbirinin yerine **kullanılmaz**.
- Belge tarihi tanımsız POS kayıtları döneme **sessizce katılmaz**;
  `DonemeKatilmayanBelirsizTarihliPos` alanında ve `Uyarilar` listesinde raporlanır.

---

## 7. Boş para birimi artık TRY sayılmıyor (madde 7)

`string.IsNullOrWhiteSpace(pb) ? "TRY" : pb` davranışı **her iki serviste de kaldırıldı**.

- Boş/tanımsız para birimi → `"Bilinmiyor"` etiketiyle **ayrı** grupta
- Sınıflandırıcıda ayrı kod: `ParaBirimiTanimsiz` (≠ `ParaBirimiUyusmuyor`)
- Normal mali toplama girmez, uyarı üretir
- Kur çevrimi olmadan farklı para birimleri hiçbir yerde birleştirilmez

---

## 8. Eşleşme güven seviyeleri (madde 8)

| Seviye | Koşul |
|---|---|
| `Kesin` | Normalize referans **birebir** + **tekil** (yetki kapsamında tek aday) + **çelişki yok** |
| `YuksekOlasilik` | Referans eşleşiyor ama tekil değil **veya** çelişki var; ya da tutar+PB+dar tarih+yöntem/hesap uyumlu |
| `IncelenmesiGereken` | Yalnızca zayıf sinyaller (tutar+tarih) veya yöntem/hesap çelişkisi |
| `EslesmeYok` | Yeterli kanıt yok |

- `Contains`/kısmi referans **kesin üretmez**
- Minimum referans uzunluğu (4) backend validation
- Aynı normalize referansa birden fazla aday → **kesin üretilmez** (`ReferansTekilMi=false`)
- Tolerans­lı tarih "birebir" olarak raporlanmaz (`TarihBirebirMi`, `TarihFarkiGun`)
- Her adayda: `EslesenAlanlar`, `UyusmayanAlanlar`, `KullanilanNormalizasyon`, gerekçe

---

## 9. Uyarılı tutar özeti tamamlandı (madde 9)

Hiçbir banka DTO'suna bağlanamayan kayıtların (hedef hesap yok/pasif/silinmiş,
`BagliBankaHesapId` null) tutarları artık yalnızca uyarı metninde kalmıyor —
`Ozet.UyariliTutarlar` özetine de giriyor. Gruplama `(UyariTipi, ParaBirimi)` bazında; uyarılı
kayıtlar normal toplamda yer almıyor.

---

## 10. Ödeme İzleme filtreleri (madde 10)

**Eklenen ve gerçekten sorguya uygulanan** filtreler: muhasebe fiş no, rezervasyon referans no,
IBAN (kısmi), oluşturulma tarihi aralığı, işlemi yapan kullanıcı, mali yıl, dönem, iptal durumu.

**Eklenmeyenler (veri modeli desteklemiyor):** POS/banka işlem referansı, fatura/tahakkuk/konaklama
numarası — karşılık gelen alan/entity yok (bkz. bölüm 1).

Tesis seçimi mevcut global muhasebe bağlamından gelmeye devam ediyor; ikinci mekanizma
oluşturulmadı. IBAN listelerde maskeli.

---

## 11. Yetkilendirme ve gizlilik (madde 11)

- Tesis kapsamı **her sağlayıcının kendi sorgusunda** uygulanıyor; istemci ID'sine güvenilmiyor
- `EnsureCanAccessTesisAsync` yetkisiz `TesisId` için 403
- Yetkisiz tesis verisi `totalCount`'a da sızmıyor (test edildi)
- `IgnoreQueryFilters` yalnızca (a) soft-delete edilmiş fişi "bulunamadı"dan ayırmak, (b) kopuk
  ilişkinin nedenini açıklamak için kullanılıyor; tesis filtresi bu sorgularda korunuyor

---

## 12. Değişen dosyalar

### Backend

| Dosya | Amaç |
|---|---|
| `Muhasebe/Common/Services/MuhasebeFisDogrulama.cs` | **yeni** — paylaşılan, saf fiş geçerlilik değerlendirmesi + `DogrulanmisFis` |
| `Muhasebe/OdemeIzleme/Services/OdemeCaprazAramaService.cs` | **yeni** — çapraz-kaynak aday üretimi + tekilleştirme |
| `Muhasebe/OdemeIzleme/Services/IOdemeCaprazAramaService.cs` | **yeni** — arayüz |
| `Muhasebe/NakitBankaPozisyonu/Services/PosValorFinansalSiniflandirici.cs` | doğrulanmış fiş + tesis uyumu + `ParaBirimiTanimsiz` |
| `Muhasebe/NakitBankaPozisyonu/Services/NakitBankaPozisyonuService.cs` | fiş/fiş-satırı doğrulama sorguları, bağlanamayan uyarılı tutarlar, boş PB düzeltmesi, mükerrerlik yorumu |
| `Muhasebe/NakitBankaPozisyonu/Dtos/NakitBankaPozisyonuDtos.cs` | yeni uyarı kodları, uyarıda `ParaBirimi` |
| `Muhasebe/OdemeIzleme/Services/OdemeIzlemeService.cs` | gerçek fiş doğrulaması, devreden bakiye, POS tarih kapsamı, eşleşme tekilliği, yeni filtreler |
| `Muhasebe/OdemeIzleme/Dtos/OdemeIzlemeDtos.cs` | çapraz arama DTO'ları, bakiye neden kodları, devreden bakiye, eşleşme açıklanabilirliği |
| `Muhasebe/OdemeIzleme/Controllers/OdemeIzlemeController.cs` | `capraz-arama` endpoint'i |
| `Program.cs` | `IOdemeCaprazAramaService` kaydı |

### Frontend

| Dosya | Amaç |
|---|---|
| `odeme-izleme.dto.ts` | çapraz arama modelleri, kopukluk/kaynak etiketleri, yeni filtre alanları |
| `odeme-izleme.service.ts` | `caprazAra()` + yeni filtre parametreleri |
| `odeme-izleme.ts` | çapraz arama state/sayfalama, yeni filtreler, etiket yardımcıları |
| `odeme-izleme.html` | çapraz arama dialogu (sayfalı tablo, kopukluk rozetleri), yeni filtre alanları |

**Migration:** gerekmedi (veri modeli değişmedi).

---

## 13. Doğrulama sonuçları

| # | Komut | Toplam | Başarılı | Başarısız | Atlanan | Uyarı | Veritabanı |
|---|---|---|---|---|---|---|---|
| 1 | `dotnet build STYS.sln` | — | ✅ | 0 error | — | **0 warning** | — |
| 2 | `dotnet test tests/STYS.Tests/STYS.Tests.csproj` | **465** | **465** | **0** | **0** | — | **Gerçek SQL Server** (`localhost,14333` / `STYSDB`) |
| 3 | `npx ng test --watch=false --browsers=ChromeHeadless` | **37** | **37** | **0** | 0 | — | — |
| 4 | `npx ng build` | — | ✅ | — | — | 1 (bundle budget, **önceden var olan**) | — |

InMemory provider finansal doğruluk kanıtı olarak kullanılmadı. Test sonrası fiziksel kalıntı
kontrolü: Kurumlar/Tesisler/HesapPlanlari/Iller = **0**.

### Ara koşuda görülen ve giderilen durum (dürüst kayıt)

İlk tam koşuda 2, ikinci koşuda 9 test **SQL Server deadlock'u** nedeniyle başarısız oldu
(paralel test sınıfları aynı anda `MuhasebeHesapPlanlari` siliyor). Bunlar üretim kodu hatası
değil, test altyapısı çakışmasıdır. Önemli nokta: **cleanup altyapısı doğru davrandı** — hatayı
yutmak yerine testi başarısız kıldı ve kalıntıyı raporladı. Biriken kalıntı FK sırasıyla
temizlendikten sonra tam koşu **465/465 başarılı** oldu. Bu koşuda `MuhasebeHesapPlanlari.TesisId`
ve `MuhasebeHesapKoduSayaclari` FK'leri de keşfedilip temizlik kapsamına alındı.

---

## 14. Bilinen sınırlamalar

1. **POS/banka işlem referansı ile arama yok** — `TahsilatOdemeBelgesi`'nde `BelgeNo` dışında
   referans/dekont alanı bulunmuyor.
2. **Fatura / tahakkuk / konaklama numarası filtresi yok** — bu entity'ler projede mevcut değil;
   en yakın gerçek karşılık rezervasyon referans no (eklendi) ve `SatisBelgesi.BelgeNo`.
3. **Geçmiş tarihli POS pozisyonu hâlâ hesaplanmıyor** (önceki turdan korunan bilinçli karar) —
   iptal zamanı ve durum geçiş tarihçesi veri modelinde yok.
4. **Hesapların geçmişteki aktiflik durumu bilinmiyor** — bugünkü `AktifMi` kullanılıyor.
5. **Kurum bazlı ayrı filtre eklenmedi** — tesis kapsamı zaten kurum sınırını kapsıyor
   (`Tesis.KurumId`); ikinci ve çelişkili bir mekanizma kurulmadı.
6. **Yetki dışı tesis/kurum için "olası kayıt var" uyarısı üretilmiyor** — böyle bir uyarı kaydın
   varlığını ifşa etme riski taşıdığından, dikkatli bir yetki tasarımı yapılmadan eklenmedi.
7. **Paralel test koşusunda deadlock riski** — `MuhasebeHesapPlanlari` üzerinde eşzamanlı silme
   yapan test sınıfları ara sıra deadlock'a düşebiliyor; cleanup bunu testi başarısız kılarak
   raporluyor (sessiz kalıntı bırakmıyor).

---

## 15. Kapsam sınırı (korundu)

Yapılmayanlar: banka API entegrasyonu · otomatik ödeme taşıma/mahsup · fiş/ödeme/cari hareket/valör
kaydı oluşturma-değiştirme · valör durumu değiştirme · varsayımsal geçmiş durum üretme · varsayımsal
döviz kuru · yalnızca tutar benzerliğinden ödeme sahipliği çıkarma · yetki dışı finansal ayrıntı
gösterme · ilgisiz genel refactoring · gereksiz migration.

Her iki ekran da **salt-okunur** araştırma/raporlama aracı olarak kalmıştır.

---

## 16. İkinci tur (2026-07-27) — SQL-taraflı sayfalama, gerçekten bağımsız aday keşfi, ters kayıt doğrulama, tesis-sızıntı kapatma

Bu tur, önceki turdaki uygulamanın **bellek-içi sayfalama** yaptığını, bağımsız kaynak aramasının
`KaynakModul==TahsilatOdemeBelgesi` şartına gizlice bağımlı kaldığını, `MuhasebeFisDogrulama`'nın
`MaliYil`/`Donem` alanlarını taşıyıp hiç karşılaştırmadığını, ters kayıt doğrulamasının yalnızca
`TersKayitMuhasebeFisId` doluluğuna baktığını ve bağlı hesap/fiş kaydı başka bir tesise ait olduğunda
bunun sızabildiğini tespit eden yeni bir talimat üzerine, **mevcut kodu (yeni modül eklemeden)
düzelterek** bu sorunları giderir.

### 16.1 Değişen dosyalar ve amaçları

| Dosya | Amaç |
|---|---|
| `backend/Muhasebe/OdemeIzleme/Services/OdemeCaprazAramaService.cs` | Tamamen yeniden yazıldı: 6 kaynağın ortak `AdayHam` projeksiyonu `Concat` (→ SQL `UNION ALL`) ile birleşir; tekilleştirme/sıralama/sayfalama `GroupBy`+`OrderBy`+`Skip`+`Take` ile **SQL Server tarafında** (`OFFSET/FETCH`, `COUNT`, `GROUP BY`) yapılır. `KaynakModul` gate'i kaldırıldı — cari/kasa/banka hareketi ve muhasebe fişi kaynakları artık **bağımsız (belgeye bağlı olmayan)** kayıtları da bulur. |
| `backend/Muhasebe/OdemeIzleme/Dtos/OdemeIzlemeDtos.cs` | `OdemeErisimKisitiNedenKodlari` (4 kod), `OdemeDetayDto.BagliHesapErisimKisitliMi/BagliFisErisimKisitliMi/ErisimKisitiNedenKodlari`, `OdemeAdayiDto.BagimsizKayitMi/GuvenSeviyesi/GuvenGerekcesi`, `OdemeCaprazAramaFilterDto` genişletmesi (`ParaBirimi/BelgeNo/MuhasebeFisNo/KasaBankaHesapId/MaliYil/Donem/SadeceIptalEdilmisOlanlar`). |
| `backend/Muhasebe/Common/Services/MuhasebeFisDogrulama.cs` | `MaliYil`/`Donem` artık gerçekten karşılaştırılıyor; "beklenen tesis dolu, fiş tesisi null" artık **reddediliyor** (önceden `HasValue` guard'ı nedeniyle sessizce geçiyordu); hesap kontrolü `null` sonucunu **ayrı bir "doğrulanamadı" kod** (`FisHesapKontroluYapilamadi`) ile işaretliyor, `== false` ile karıştırmıyor; dönem-tarih aralığı verildiğinde `FisTarihi` null ise de reddediyor (`FisTarihiYok`). |
| `backend/Muhasebe/Common/Services/TersKayitIliskisiDogrulama.cs` (yeni) | Ters kaydın asıl fişi **gerçekten** terslediğini `IptalEdilenFisId` (otoriter ilişki) üzerinden doğrular; tesis uyumu, tutar uyumu, mükerrer ters kayıt sayısı kontrol edilir. Kanıtlanamıyorsa `TERS_KAYIT_ILISKISI_DOGRULANAMADI`. |
| `backend/Muhasebe/NakitBankaPozisyonu/Services/PosValorFinansalSiniflandirici.cs`, `NakitBankaPozisyonuService.cs` | Ters kayıt sınıflandırması artık `TersKayitIliskisiDogrulama` sonucuna bağlı; kanıtlanamayan ters kayıt `VeriKalitesiUyarisi`'na düşer (önceden salt `Durum` alanına göre otomatik `TersKayitSurecinde` sayılıyordu). `ParaBirimi ?? "TRY"` kalıntıları `NormalizeParaBirimi`'ye çevrildi. |
| `backend/Muhasebe/OdemeIzleme/Services/OdemeIzlemeService.cs` | `GetDetayAsync`'te `KasaBankaHesaplari`/`MuhasebeFisler` sorguları artık **yetkili tesis id listesini WHERE koşuluna dahil ediyor**; başka tesise ait kayıt bulunduğunda (veya kayıt hiç yoksa — bilerek ayrım yapılmaz) hesap/fiş DETAYI hiç dönmüyor, yalnızca güvenli bayrak + neden kodu + kullanıcıya gösterilecek genel uyarı dönüyor. |
| `tests/STYS.Tests/OdemeCaprazAramaSqlKanitiTests.cs` | (Bu turda doğrulandı, değişmedi) SQL-taraflı sayfalama kanıtı — bkz. §16.3. |
| `tests/STYS.Tests/OdemeCaprazAramaBagimsizAdayVeYetkiTests.cs` (yeni) | Bağımsız cari/kasa/banka/fiş adayı keşfi, tesis-yetki sızıntısı yokluğu, filtre-uygulanamayan-kaynağın-sessizce-dışlanması, aynı ödeme belgesine bağlı cari hareketin TEK adaya birleşmesi — 6 test. |
| `tests/STYS.Tests/TersKayitIliskisiDogrulamaTests.cs` (yeni) | `TersKayitIliskisiDogrulama` saf mantık testleri — 9 test. |
| `tests/STYS.Tests/MuhasebeFisDogrulamaTests.cs` | 6 yeni test: mali yıl/dönem uyumsuzluğu, tesis-null-reddi, hesap-kontrolü-null-reddi, dönem aralığı verilip fiş tarihi yoksa red. |
| `tests/STYS.Tests/OdemeIzlemeServiceTests.cs` | 2 yeni test: bağlı kasa/banka hesabı başka tesiste → sızmaz; bağlı muhasebe fişi başka tesiste → sızmaz. Ayrıca 7 mevcut çapraz-arama testi, yeni "daraltıcı alan zorunlu" kuralına uyacak şekilde güncellendi (narrowing field eklendi). |
| `tests/STYS.Tests/PosValorFinansalSiniflandiriciTests.cs` | Test fixture'ı, `AktarimFisiIptalEdildi` senaryosu için geçerli bir `TersKayitIliskisi` üretecek şekilde güncellendi (yeni zorunlu doğrulama nedeniyle). |
| Frontend: `odeme-izleme.dto.ts`, `odeme-izleme.service.ts`, `odeme-izleme.ts`, `odeme-izleme.html`, `odeme-izleme.spec.ts` | Çapraz arama dialoguna daraltıcı-alan filtreleri (cari, belge no, fiş no, kasa/banka hesabı, tutar/tarih aralığı) eklendi; alan girilmeden arama YAPILMIYOR, backend kuralıyla birebir aynı istemci-taraflı uyarı gösteriliyor; sonuç satırlarına güven seviyesi + "Bağımsız kayıt" etiketi eklendi; detay dialogunda yetki-dışı hesap/fiş bağlantısı için güvenli uyarı (silinen ayrıntı yerine) gösteriliyor. |

### 16.2 Filtre × kaynak uygulanabilirlik tablosu

| Filtre | Ödeme Belgesi | Cari Hareket | POS/Valör | Banka H. | Kasa H. | Muhasebe Fişi |
|---|---|---|---|---|---|---|
| Tarih aralığı | ✅ doğrudan | ✅ doğrudan | ✅ doğrudan | ✅ doğrudan | ✅ doğrudan | ✅ doğrudan |
| Tutar aralığı | ✅ doğrudan | ✅ doğrudan (borç/alacak) | ✅ (net tutar) | ✅ doğrudan | ✅ doğrudan | ✅ (toplam borç) |
| Cari hesap | ✅ doğrudan | ✅ doğrudan | ✅ (belge üzerinden EXISTS) | ✅ doğrudan | ✅ doğrudan | ✅ (fiş satırı EXISTS) |
| Para birimi | ✅ doğrudan | ✅ doğrudan | ✅ doğrudan | ✅ doğrudan | ✅ doğrudan | ✅ (satır EXISTS — başlıkta yok) |
| Belge no | ✅ doğrudan | ✅ doğrudan | ✅ (belge EXISTS) | ✅ doğrudan | ✅ doğrudan | ✅ (fiş no ile) |
| Muhasebe fiş no | ✅ (fiş EXISTS) | ❌ **kaynak dışı bırakılır** | ✅ (fiş EXISTS) | ❌ **kaynak dışı bırakılır** | ❌ **kaynak dışı bırakılır** | ✅ doğrudan |
| Kasa/banka hesabı | ✅ doğrudan | ❌ **kaynak dışı bırakılır** | ✅ doğrudan | ✅ doğrudan | ✅ doğrudan | ✅ (satır EXISTS) |
| Mali yıl / dönem | ✅ (fiş EXISTS) | ❌ **kaynak dışı bırakılır** | ✅ (fiş EXISTS) | ❌ **kaynak dışı bırakılır** | ❌ **kaynak dışı bırakılır** | ✅ doğrudan (fiş başlığı) |
| Sadece iptal edilmiş | ✅ (Durum) | ✅ (Durum) | ✅ (Durum) | — (uygulanmadı) | — (uygulanmadı) | — (uygulanmadı) |

"❌ kaynak dışı bırakılır": ilgili filtre bu kaynakta güvenilir bir alan/ilişki üzerinden
uygulanamadığı için, bu filtre verildiğinde **o kaynak sorgudan tamamen çıkarılır**
(`Where(_ => false)`) — filtre sessizce yok sayılıp yanıltıcı "eşleşme yok" sonucu üretilmez.

### 16.3 SQL-taraflı sayfalama kanıtı (gerçek, bu turda tekrar doğrulandı)

`OdemeCaprazAramaSqlKanitiTests` içindeki `DbCommandInterceptor`, gerçek SQL Server'a giden komut
metnini yakalıyor. Bu turun koşusunda `C:\Users\...\Temp\capraz-arama-sql.txt` dosyasına yazılan
SQL'de somut olarak şunlar sayıldı:

- `UNION ALL`: **15** kez (6 kaynağın `Concat` zinciri)
- `OFFSET`: **1**, `FETCH NEXT`: **1** (sayfa sorgusu SQL Server'da `OFFSET/FETCH` ile yapılıyor)
- `GROUP BY`: **2** (tekilleştirme + sayaç sorgusu)
- `COUNT(`: **1** (post-filtre+dedup gerçek `totalElements`)

Sayfa başına materyalize edilen satır sayısı = sayfa boyutu (test: `PageSize=5` → `Items.Count<=5`,
gerçek `TotalCount=25`); kaynak tabloların tamamı asla `ToListAsync()` ile belleğe alınmıyor.

### 16.4 Tekilleştirme anahtarı/öncelik tablosu

| Durum | Anahtar |
|---|---|
| Kayıt bir `TahsilatOdemeBelgesi`'ne (`KaynakModul`/`KaynakId` ile) bağlı | `BELGE:{belgeId}` — tüm kaynaklar aynı anahtarda birleşir |
| Bağımsız cari hareket | `CARIHAREKET:{id}` |
| Bağımsız kasa hareketi | `KASAHAREKET:{id}` |
| Bağımsız banka hareketi | `BANKAHAREKET:{id}` |
| Bağımsız muhasebe fişi | `FIS:{id}` |

Bağımsız anahtarlar `BagimsizKayitMi=true` + `GuvenSeviyesi=IncelenmesiGereken` ile işaretlenir;
belge-bağlantılı anahtarlar `YuksekOlasilik` alır. Hiçbir aday yalnızca tutar eşleşmesiyle "Kesin"
sayılmaz.

### 16.5 Yetki/tesis-sızıntı kapatma — nasıl uygulandı

`OdemeIzlemeService.GetDetayAsync`'te `KasaBankaHesaplari`/`MuhasebeFisler` sorguları artık
`yetkiliTesisIds.Contains(...)` koşulunu **doğrudan WHERE'e** ekliyor (önce oku-sonra-sil deseni
DEĞİL). Kayıt bulunamadığında (gerçekten yok / var ama yetki dışı — **kasıtlı olarak ayrım
yapılmıyor**), `BagliHesapErisimKisitliMi`/`BagliFisErisimKisitliMi=true` + ilgili neden kodu
(`YETKI_KAPSAMI_DISINDA_HESAP_BAGLANTISI`/`YETKI_KAPSAMI_DISINDA_FIS_BAGLANTISI`) set edilir; hesap
adı/IBAN/kod/fiş no/tarih/durum alanları boş kalır. `totalElements`/aday sayılarına bu kayıtlar
sızmaz çünkü kaynak sorgu zaten tesis kapsamıyla filtrelenmiştir.

### 16.6 Bu turda çalıştırılan komutlar ve gerçek sonuçlar

| # | Komut | Toplam | Başarılı | Başarısız | Veritabanı |
|---|---|---|---|---|---|
| 1 | `dotnet build STYS.sln` | — | ✅ 0 error, 0 warning | — | — |
| 2 | `dotnet test tests/STYS.Tests/STYS.Tests.csproj` (tam koşu) | **491** | **491** | **0** | Gerçek SQL Server (`localhost,14333`/`STYSDB`) |
| 3 | `npx ng build --configuration development` | — | ✅ 0 error | — | — |
| 4 | `npx ng test --watch=false --browsers=ChromeHeadless` | **38** | **38** | **0** | — |

**Dürüst not:** tam test koşusu ilk iki denemede sırasıyla 8 ve 5 testin **paralel test sınıfları
arasındaki SQL Server deadlock/duplicate-key/ FK çakışması** nedeniyle başarısız olduğunu gösterdi
(`BackfillMissingPosTahsilatValorSnapshotsMigrationTests`, `RezervasyonOdemeMuhasebeIntegrationTests`,
`PosTahsilatValorIntegrationTests` gibi bu turda **hiç dokunulmayan** sınıflarda). Bu testler tek
başına/izole çalıştırıldığında sorunsuz geçti; üçüncü tam koşu **491/491** başarılı sonuçlandı. Bu,
üretim kodunda bir regresyon değil, aynı SQL Server örneğine karşı yüksek paralellikle çalışan
bağımsız test sınıfları arasındaki bilinen bir kaynak çakışmasıdır (önceki turun raporunda da §13'te
aynı sınıf deadlock'ları not edilmişti).

### 16.7 Bilinen sınırlamalar (bu turda eklenen)

- `MuhasebeFisSatir.MuhasebeHesapPlaniId` zorunlu FK olduğu için cross-source aramada "hiç var
  olmayan bir kasa/banka hesabına bağlı ödeme belgesi" senaryosu DB seviyesinde zaten imkânsız
  (FK constraint) — yalnızca "var ama başka tesise ait" senaryosu gerçekçi ve test edildi.
- Migration gerekmedi; veri modeli değişmedi.

---

**Sonuç:** Sunucu-taraflı sayfalama, bağımsız kaynak keşfi ve tesis-arası veri gizliliği artık gerçek
kodla (prose değil) kanıtlanmış; 33 yeni/güncellenen test dahil toplam 491 backend + 38 frontend testi
gerçek SQL Server'a karşı yeşil.

---

## 18. Üçüncü tur (2026-07-27) — Beklenen/Bulunan ayrımı, deterministik birleştirme, ters kayıt kesinleştirme

Esas alınan başlangıç commiti: `35b36688a1b18745c13f8b3e862e25e3861c22a4`.

Bu tur, bir önceki turun çapraz aramasının `CariKartId`/`KasaBankaHesapId`/`MaliYil`/`Donem`
alanlarını **yalnızca sonuç filtresi** olarak kullandığını — yani "başka hesaba/cariye/döneme
yanlışlıkla işlenmiş" tam da aranan kayıtların bu filtreler yüzünden SESSİZCE elendiğini tespit eden
yeni bir talimat üzerine, mevcut kodu (yeni modül eklemeden) baştan kurar.

### 18.1 Değişen dosyalar ve amaçları

| Dosya | Amaç |
|---|---|
| `backend/Muhasebe/OdemeIzleme/Dtos/OdemeIzlemeDtos.cs` | `OdemeCaprazAramaFilterDto`'dan `CariKartId`/`KasaBankaHesapId`/`MaliYil`/`Donem` kaldırıldı; yerine **karşılaştırma-amaçlı, SONUCU DARALTMAYAN** `Beklenen*` alanları (`BeklenenCariKartId`, `BeklenenBankaHesapId`, `BeklenenKasaHesapId`, `BeklenenMuhasebeHesapPlaniId`, `BeklenenMaliYil`, `BeklenenDonem`, `BeklenenTesisId`, `BeklenenKurumId`, `BeklenenTutar`, `BeklenenParaBirimi`) eklendi. Yeni `RezervasyonReferansNo` gerçek bir narrowing alanı olarak eklendi. `OdemeAdayiDto`'ya `EslesenAlanlar`/`CelisenAlanlar`/`VeriKalitesiUyarilari`/`AyrintiYetkiNedeniyleGizliMi`/`GuvenliNedenKodlari`/`KaynakTutarlari` (madde 3 - kaynak bazlı tutarlar) eklendi. Yeni `OdemeCeliskiKodlari` sabit sınıfı (10 çelişki kodu). |
| `backend/Muhasebe/OdemeIzleme/Services/OdemeCaprazAramaService.cs` | Tamamen yeniden yazıldı: (1) altı sağlayıcının WHERE'inden cari/hesap/dönem filtreleri kaldırıldı — artık yalnızca gerçek narrowing alanları (tarih, tutar, para birimi, belge no, fiş no, rezervasyon no) filtreler; (2) `KarsilastirBeklenenVeBulunan` — bulunan adayın gerçek cari/hesap/hesap-türü/dönem/tesis/kurum/tutar/para birimi değerlerini `Beklenen*` ile karşılaştırıp **ELEMEDEN** `EslesenAlanlar`/`CelisenAlanlar` üretir; (3) `AdayHam`'a `KaynakOncelik` (1=Belge, 2=Banka/Kasa Hareketi, 3=POS/Valör, 4=Cari Hareket, 5=Muhasebe Fişi) + `MuhasebeHesapPlaniId`/`MaliYil`/`Donem`/`KurumId`/`KasaBankaHesapTipi`/`TutarTuru` eklendi; sayfa-zenginleştirme sorgusu artık `OrderBy(KaynakOncelik).ThenBy(KaynakId)` ile açıkça sıralanarak **deterministik birleştirme** sağlanıyor (fiziksel SQL satır sırasına bağlı değil); (4) `DogrulaSorguSinirlari` (eski `DogrulaDaralticiAlanVarligi`'nin yerine) — güçlü referans (belge/fiş/rezervasyon no) YOKSA tutar aralığının hem alt hem üst sınırı + dar tarih aralığı (≤92 gün) birlikte zorunlu; tek taraflı tutar/tarih reddedilir. |
| `backend/Muhasebe/Common/Services/TersKayitIliskisiDogrulama.cs` | `AsilFisId` null iken `IptalEdilenFisId` dolu olsa bile artık **ASIL_FIS_BILINMIYOR** ile reddediliyor (önceki turda bu boşluk vardı — null AsilFisId kontrolü atlanıyordu). Para birimi (`TersKayitParaBirimi`/`AsilFisParaBirimi`) modele taşındı ve karşılaştırılıyor (`TERS_KAYIT_PARA_BIRIMI_UYUSMAZLIGI`). Kurum karşılaştırması eklendi (`TERS_KAYIT_KURUM_UYUSMAZLIGI`). Yeni `TersYonluHesapEtkisiUyumluMu` (bool?) alanı — `null` (doğrulanamadı) VEYA `false` iken asla "doğrulandı" üretilmiyor (`TERS_YONLU_HESAP_ETKISI_DOGRULANAMADI`). |
| `backend/Muhasebe/NakitBankaPozisyonu/Services/NakitBankaPozisyonuService.cs` | Yeni `FisOzeti` record'u (KurumId + ParaBirimi eklendi). Fiş-satırı sorgusu artık (FisId,HesapId)→net etki (Borç-Alacak) sözlüğü de üretiyor; `CozumleTersKayitIliskisi` bu sözlükten **asıl fişin BU spesifik hesaptaki net etkisi ile ters kaydın net etkisinin toplamda sıfıra yaklaşıp yaklaşmadığını** (`Math.Abs(asilNet + tersNet) <= 0.01`) hesaplayarak `TersYonluHesapEtkisiUyumluMu`'yu GERÇEKTEN dolduruyor (varsayım değil). |
| `backend/Muhasebe/OdemeIzleme/Services/OdemeIzlemeService.cs` | **Madde 9 hatası düzeltildi**: `GetDetayAsync`'te `dto.Uyarilar = await BuildUyarilarAsync(...)` satırı, önceden eklenen yetki/erişim-kısıtı uyarılarını (`BagliHesapErisimKisitliMi`/`BagliFisErisimKisitliMi`) SESSİZCE siliyordu — artık birleştiriliyor (`UyariTipi`'ne göre tekilleştirilerek). **Madde 8 hatası düzeltildi**: `RaporlamaParaBirimi = "TRY"` sabiti (cari kart açılış bakiyesini varsayılan TRY sayan kod) tamamen kaldırıldı; açılış bakiyesi artık güvenilir bir para birimi kaynağı (kurum/tesis bazlı ayar) OLMADIĞI için `Bilinmiyor` grubuna yazılıyor, normal/TRY toplamlarına katılmıyor, veri kalitesi uyarısı üretiyor. |
| `tests/STYS.Tests/TersKayitIliskisiDogrulamaTests.cs` | 6 yeni test: `AsilFisBilinmiyor`, `FarkliKurum`, `ParaBirimiUyumsuzlugu`, `TersYonluHesapEtkisiDogrulanamadi` (null), `TersYonluHesapEtkisiUyumsuz` (false) + mevcutlar güncellendi (yeni kayıt alanları). |
| `tests/STYS.Tests/PosValorFinansalSiniflandiriciTests.cs` | `GecerliTersKayitIliskisi()` fixture'ı yeni alanlarla (kurum, para birimi, ters yönlü hesap etkisi=true) güncellendi. |
| `tests/STYS.Tests/OdemeCaprazAramaBagimsizAdayVeYetkiTests.cs` | 4 yeni test (bkz. §18.3) + mevcut testler yeni sözleşmeye (Beklenen* alanları artık daraltmıyor) uyacak şekilde güncellendi. |
| `tests/STYS.Tests/OdemeCaprazAramaSqlKanitiTests.cs`, `tests/STYS.Tests/OdemeIzlemeServiceTests.cs` | Mevcut testler yeni filtre sözleşmesine (CariKartId→BeklenenCariKartId + gerçek narrowing alanı) taşındı; açılış bakiyesi testleri (2 adet) yeni "Bilinmiyor" davranışına göre güncellendi. |
| `docs/odeme-izleme-capraz-kaynak-ve-fis-dogrulama-raporu.md` | Bu bölüm (§18). |

### 18.2 Arama alanları ile beklenen karşılaştırma alanlarının ayrımı

| Kategori | Alanlar | Davranış |
|---|---|---|
| **Aramayı daraltan** (gerçek SQL filtresi) | `BelgeNo`, `MuhasebeFisNo`, `RezervasyonReferansNo`, `TutarMin`/`TutarMax`, `TarihBaslangic`/`TarihBitis`, `ParaBirimi`, `SadeceIptalEdilmisOlanlar` | WHERE'e girer; adayı SQL seviyesinde bulur/eler |
| **Beklenen** (yalnızca karşılaştırma) | `BeklenenCariKartId`, `BeklenenBankaHesapId`, `BeklenenKasaHesapId`, `BeklenenMuhasebeHesapPlaniId`, `BeklenenMaliYil`, `BeklenenDonem`, `BeklenenTesisId`, `BeklenenKurumId`, `BeklenenTutar`, `BeklenenParaBirimi` | WHERE'e HİÇ girmez; bulunan adayın gerçek değeriyle karşılaştırılır, uyuşmazlık `CelisenAlanlar`'a yazılır — aday ELENMEZ |

Not: `KasaBankaHesap` modelde tek bir tablo olup `Tip` alanıyla (Banka/NakitKasa/KrediKarti/DovizHesabi)
ayrışır; bu yüzden "beklenen banka hesabı" ve "beklenen kasa hesabı" iki ayrı giriş alanı olarak
sunulur ama karşılaştırma, bulunan hesabın gerçek `Tip`'ine göre doğru `Beklenen*` alanıyla eşlenir.

### 18.3 Üç zorunlu kanıt — gerçek kod + test

**1) Beklenen hesap/cari farklı yerdeki güçlü adayı elemiyor:**
`OdemeCaprazAramaBagimsizAdayVeYetkiTests.BeklenenCariFarkliOlanGuclu_Aday_ELENMEZ_CeliskiIleDoner`
— `BeklenenCariKartId=10` verilirken gerçek kayıt cari 20'ye ait; test adayın **DÖNDÜĞÜNÜ**,
`CariKartId==20`, `CelisenAlanlar` içinde `CARI_HESAP_UYUSMAZLIGI` ve `EslesenAlanlar`'da "Cari Kart"
OLMADIĞINI doğrular. Aynı desen banka hesabı (`BeklenenBankaHesabiFarkliOlanAday_...`) ve mali
yıl/dönem (`BeklenenMaliYilVeDonemFarkliOlanFis_...`) için de ayrı testlerle kanıtlanmıştır.

**2) Tesisler arası yanlış bağlantı geçerli/yüksek-olasılık sayılmıyor:** (önceki turdan korunan,
bu turda da regresyonsuz doğrulanan) `OdemeIzlemeServiceTests.Detay_BagliKasaBankaHesabiBaskaTesisteyse_...`
ve `Detay_BagliMuhasebeFisiBaskaTesisteyse_...` — yetki dışı tesise ait hesap/fiş asla `YuksekOlasilik`
üretmiyor, yalnızca güvenli neden kodu dönüyor, IBAN maskeli dahi gösterilmiyor.

**3) Deterministik birleştirme eklenirken SQL sayfalaması bozulmadı:**
`FarkliKaynaklarinCelisenTutarlari_KaynakOnceligineGoreDeterministikBirlesir` — aynı mali işlemin
belge (öncelik 1, tutar 500) ve cari hareket (öncelik 4, işaretli tutar 500 ama farklı anlam)
kaynaklarından geldiği, `AraAsync`'in İKİ KEZ çağrılmasının HER İKİSİNDE de özet `Tutar`'ın belge
kaynağından (`"Belge Tutarı"`) geldiğini ve `KaynakTutarlari` listesinin her iki kaynağı da ayrı
ayrı (kendi `TutarTuru`'yla) taşıdığını kanıtlar. `OdemeCaprazAramaSqlKanitiTests` (önceki turdan,
bu turda da yeşil) `UNION ALL`/`OFFSET`/`FETCH NEXT`/`GROUP BY`/`COUNT(` varlığını gerçek SQL
metninde doğrulamaya devam ediyor — deterministik `OrderBy(KaynakOncelik)` eklenmesi bu SQL yapısını
bozmadı.

### 18.4 Ters kayıt ilişkisi — bu turda eklenen kesinleştirmeler

- `AsilFisId` bilinmiyorsa (`null`), ters fişte `IptalEdilenFisId` dolu olsa bile ilişki
  KANITLANMAMIŞ sayılır (`ASIL_FIS_BILINMIYOR`).
- Para birimi artık gerçekten karşılaştırılıyor (fiş satırlarından okunan temsili para birimi).
- Kurum karşılaştırması eklendi (Tesis→Kurum çözümlemesi üzerinden).
- **Ters yönlü hesap etkisi** artık varsayım değil: `NakitBankaPozisyonuService`, asıl fiş ile ters
  kaydın AYNI banka/kasa hesabındaki net (Borç−Alacak) etkilerini fiş satırlarından hesaplayıp
  toplamının sıfıra yakın olup olmadığını (`±0.01`) doğruluyor; doğrulanamıyorsa (ilgili hesap
  satırı yoksa) veya uyumsuzsa `TERS_YONLU_HESAP_ETKISI_DOGRULANAMADI` ile "doğrulandı" ASLA
  üretilmiyor.

### 18.5 Bu turda çalıştırılan komutlar ve gerçek sonuçlar

| # | Komut | Toplam | Başarılı | Başarısız | Veritabanı |
|---|---|---|---|---|---|
| 1 | `dotnet build STYS.sln` | — | ✅ 0 error, 0 warning | — | — |
| 2 | `dotnet test tests/STYS.Tests/STYS.Tests.csproj -- xUnit.MaxParallelThreads=1` | **500** | **500** | **0** | Gerçek SQL Server (`localhost,14333`/`STYSDB`) |
| 3 | `npx ng build --configuration development` | — | ✅ 0 error | — | — |
| 4 | `npx ng test --watch=false --browsers=ChromeHeadless` | **38** | **38** | **0** | — |

**Dürüst not:** varsayılan (paralel) test koşusunda, bu turda dokunulmayan sınıflarda (ör.
`BackfillMissingPosTahsilatValorSnapshotsMigrationTests`, `RezervasyonOdemeMuhasebeIntegrationTests`,
`NakitBankaPozisyonuServiceTests`) ara sıra SQL Server deadlock/duplicate-key kaynaklı geçici hatalar
gözlendi (izole çalıştırıldıklarında sorunsuz geçiyorlar) — bu, `xUnit.MaxParallelThreads=1` ile
tekrar çalıştırılarak **500/500 başarılı** olarak doğrulandı. Bu, aynı SQL Server örneğine karşı
yüksek paralellikle çalışan bağımsız test sınıfları arasındaki bilinen bir kaynak çakışmasıdır
(önceki iki turun raporlarında da not edilmişti), üretim kodunda regresyon değildir.

### 18.6 Bilinen sınırlamalar (bu turda eklenen)

- `Beklenen*` alanlarının çözümlenmesi için her sağlayıcıya `KasaBankaHesaplari`/`Tesisler`/
  `MuhasebeFisler` üzerine ek korele alt sorgular eklendi (MuhasebeHesapPlaniId, KurumId, MaliYil/
  Donem, KasaBankaHesapTipi) — bu, sorgu karmaşıklığını artırır; veri hacmi büyüdükçe bu alt
  sorguların indekslenmesi ayrıca değerlendirilmelidir (bu turun kapsamı dışında, refactor değil).
- Tarih aralığı üst sınırı (92 gün) proje veri hacmine göre keyfi seçilmiştir; gerçek kullanım
  verisiyle yeniden değerlendirilebilir.
- Ters yönlü hesap etkisi doğrulaması, valörün bağlı olduğu SPESİFİK banka hesabı üzerinden yapılır
  (fişin tüm satırları değil) — bu, gerçek kullanım senaryosuna (tek hesaba etki) uygundur ancak
  çoklu-hesap etkileyen fişlerin diğer hesaplarının ters yönlülüğünü doğrulamaz.
- Migration gerekmedi; veri modeli değişmedi.

---

**Sonuç (üçüncü tur):** Beklenen/bulunan ayrımı, tesisler-arası yanlış bağlantı reddi ve deterministik
kaynak-öncelikli birleştirme artık üç ayrı, isimlendirilmiş entegrasyon testiyle kanıtlanmıştır;
toplam 500 backend + 38 frontend testi gerçek SQL Server'a karşı yeşil. Başlangıç commiti
`35b36688a1b18745c13f8b3e862e25e3861c22a4`.
