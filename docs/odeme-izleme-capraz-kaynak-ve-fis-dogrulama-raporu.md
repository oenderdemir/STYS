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
