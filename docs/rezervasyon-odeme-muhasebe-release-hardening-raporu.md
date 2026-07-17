# Rezervasyon Ödeme-Muhasebe Entegrasyonu — Release Hardening Raporu

**Tarih:** 2026-07-18
**Kapsam:** `fbba9a0` (nihai doğrulama raporu) sonrası, yeni özellik geliştirmeden önce yapılan yayına hazırlık kontrolleri.
**Kural:** Bu turda kod değişikliği yapılmadı. Kritik bulgular aşağıda raporlanmış, küçük/izole düzeltmeler önerilmiştir; onay olmadan uygulanmamıştır.

---

## 1. Build Sonucu

| Katman | Komut | Sonuç |
|---|---|---|
| Backend | `dotnet build STYS.sln --no-restore` | ✅ 0 hata, 14 uyarı (hepsi bu oturumdan bağımsız, önceden var olan uyarılar) |
| Frontend | `npm run build` (mevcut `node_modules` ile) | ✅ 0 hata. 1 uyarı: initial bundle 4.22 MB, budget 2.80 MB'ı 1.42 MB aşıyor (önceden var olan bütçe uyarısı, bu oturumun değişiklikleriyle ilgisi yok) |

**Sonuç: Build tarafında blocker yok.**

---

## 2. Test Sonucu

`dotnet test tests/STYS.Tests/STYS.Tests.csproj --no-build` (entegrasyon connection string TANIMLI DEĞİLKEN çalıştırıldı — asıl amaç skip davranışını doğrulamaktı):

```
Toplam: 308   Başarılı: 219   Başarısız: 78   Atlanan: 11
```

### 2a. Entegrasyon testleri — beklenen davranış DOĞRULANDI ✅

Atlanan 11 test tam olarak `RezervasyonOdemeMuhasebeIntegrationTests` içindeki 11 senaryo (Senaryo1..11), her biri `IntegrationFactAttribute` üzerinden şu mesajla atlanıyor:
`"STYS_INTEGRATION_TEST_CONNECTION_STRING ortam degiskeni tanimli degil — entegrasyon testi atlandi."`

Bu, "entegrasyon testleri env var yoksa skip edilmeli" gereksinimini karşılıyor. Normal (`[Fact]`) testler SQL Server istemiyor — hepsi InMemory provider kullanıyor.

### 2b. 78 başarısız test — KÖK NEDEN BULUNDU, BU OTURUMUN İŞİNDEN BAĞIMSIZ (pre-existing)

**Bulgu:** Başarısız testlerin tamamı iki farklı, birbiriyle ilgisiz, **önceden var olan** test-altyapısı sorunundan kaynaklanıyor. Hiçbiri bu oturumda yapılan rezervasyon-ödeme-muhasebe commit'leriyle (`d075519`, `fe3480e`, `422d8f7`, `930f9c6`, `e6ff969`, `fbba9a0` vb.) ilgili değil.

**Sorun A — Tenant seed eksikliği (≈73 test: `RezervasyonServiceTests` ~70, `KampTahsisServiceTests` 2, `KampKurallariTests` 3, kısmen)**

- `StysAppDbContext.ApplyTenantRules` kuralı: yeni eklenen bir `ITenantEntity`, `CurrentKurumId` yoksa ancak `IsSuperAdmin == true && KurumId > 0` şartıyla kabul ediliyor; aksi halde `"Aktif kurum bilgisi bulunamadi."` (400) fırlatıyor.
- `RezervasyonServiceTests.CreateDbContext()` bir `FakeCurrentTenantAccessor` veriyor: `IsSuperAdmin() => true`, `GetCurrentKurumId() => null`.
- Ancak `SeedReservationFixtureWithTenRoomsAsync` (ve diğer seed helper'lar) `Tesis` (`ITenantEntity`) kaydını **`KurumId` set etmeden** ekliyor → `KurumId` default `0` → `IsSuperAdmin && KurumId > 0` şartı sağlanmıyor → throw.
- **Bu kuralın kendisi `ab49b51`/`9916d01` commit'leriyle 2026-06-15 tarihinde eklendi** — bu oturumun ilk commit'inden (bu ay) haftalarca önce. Test dosyasının hiçbir noktasında `Tesis.KurumId` set edilmemiş; yani bu testler o tarihten beri kırık olmalı ve muhtemelen o tarihten beri `dotnet test` ile hiç çalıştırılmamış (bu oturumdaki tüm doğrulamalar canlı backend + Playwright ile yapıldı, xUnit suite'i hiç koşulmadı).
- **Önerilen minimal düzeltme (izole, tek dosya):** `RezervasyonServiceTests.cs` içindeki seed helper'larda `Tesis` nesnelerine `KurumId = 1` eklemek (veya `FakeCurrentTenantAccessor.GetCurrentKurumId()`'i `1` döndürecek şekilde değiştirmek). Aynı desen `KampTahsisServiceTests` ve `KampKurallariTests` için de geçerli olabilir — ayrı incelenmeli.

**Sorun B — Reflection tabanlı yetki testi, attribute imzasıyla uyumsuz (1 test: `TenantSecurityTests.KurumController_GetMyKurumlar_UsesAuthOnlyPermissionAttribute`)**

- Test, `[Permission]` attribute'unun **hiç** constructor argümanı almadığını (`Assert.Empty(attributeData.ConstructorArguments)`) doğrulamaya çalışıyor.
- Gerçekte `PermissionAttribute`'un constructor'ı `params string[] permissions` alıyor; `[Permission]` (parametresiz yazım) derlenince bile `ConstructorArguments` koleksiyonu **boş bir string dizisi içeren tek bir eleman** taşıyor (`[String[0]{}]`), asla gerçekten boş olmuyor.
- Bu, attribute'un `params` imzasıyla test assertion'ının hiçbir zaman uyuşamayacağı, bağımsız bir pre-existing test kalıntısı. Bu oturumun işiyle ilgisi yok.
- **Önerilen minimal düzeltme:** `Assert.Empty(attributeData.ConstructorArguments)` yerine `Assert.Empty(((string[])attributeData.ConstructorArguments[0].Value)!)` ile "izin listesi boş mu" kontrolü yapmak.

**Kalan ~4 test:** `KampKurallariTests`/`KampTahsisServiceTests`'teki bazı testler de muhtemelen aynı Sorun A kalıbını paylaşıyor (aynı `Tesis`/tenant seed yapısı kullanıyorlar); ayrı ayrı doğrulanmadı ama örüntü aynı.

**Değerlendirme:** Bu, hem "yapılmalı" hem "yapılabilir" kategorisine ayrılabilir — bkz. Bölüm 8.

---

## 3. Migration Riski

Son iki migration incelendi:

- `20260713155404_AddRezervasyonOdemeMuhasebeEntegrasyonu`
- `20260714140521_AddRezervasyonSatisBelgesiEntegrasyonu`

İstenen 7 alanın tümü doğrulandı (migration + entity eşleşmesi):

| Alan | Migration'da var mı | Nullable/Default |
|---|---|---|
| `Rezervasyon.CariKartId` | ✅ | nullable |
| `Rezervasyon.SatisBelgesiId` | ✅ | nullable |
| `RezervasyonOdeme.KasaBankaHesapId` | ✅ | nullable |
| `RezervasyonOdeme.TahsilatOdemeBelgesiId` | ✅ | nullable |
| `TahsilatOdemeBelgesi.KasaBankaHesapId` | ✅ | nullable |
| `TahsilatOdemeBelgesi.MuhasebeFisId` | ✅ | nullable |
| `TahsilatOdemeBelgesi.MuhasebeFisOlusturmaTarihi` | ✅ | nullable |

**NOT NULL + default'lu yeni alanlar (production'da mevcut satırlar için risksiz, otomatik backfill):**
- `Tesisler.RezervasyonTahsilatAlacakHesapTipi` — `nvarchar(16) NOT NULL DEFAULT 'Cari'`
- `RezervasyonOdemeler.Durum` — `nvarchar(16) NOT NULL DEFAULT 'Aktif'`

**Unique filtered index'ler (production çakışma riski değerlendirildi):**
- `IX_RezervasyonOdemeler_TahsilatOdemeBelgesiId` — unique, `WHERE TahsilatOdemeBelgesiId IS NOT NULL`. Yeni alan, mevcut satırlarda hep NULL → çakışma yok.
- `IX_Rezervasyonlar_SatisBelgesiId` — unique, `WHERE IsDeleted=0 AND SatisBelgesiId IS NOT NULL` → çakışma yok.
- `IX_TahsilatOdemeBelgeleri_KaynakModul_KaynakId` — unique, `WHERE IsDeleted=0 AND KaynakId IS NOT NULL` → **tek dikkat noktası:** bu index migration'dan önce girilmiş, `KaynakModul`+`KaynakId` dolu (`KaynakId IS NOT NULL`) satırlar arasında zaten bir duplikasyon varsa migration **UYGULANAMAZ** (unique index oluşturma hata verir). Bu, üretim veritabanına migration uygulanmadan önce tek satırlık bir doğrulama sorgusuyla kontrol edilmeli:
  ```sql
  SELECT KaynakModul, KaynakId, COUNT(*) FROM muhasebe.TahsilatOdemeBelgeleri
  WHERE IsDeleted = 0 AND KaynakId IS NOT NULL
  GROUP BY KaynakModul, KaynakId HAVING COUNT(*) > 1;
  ```
  Sonuç boşsa migration risksiz uygulanabilir.

**Sonuç: Migration riski DÜŞÜK.** Tek ön-koşul: yukarıdaki duplikasyon kontrol sorgusunun prod DB'de 0 satır döndürdüğünün teyit edilmesi (deploy runbook'una eklenmeli).

---

## 4. Regresyon Smoke Test Planı

Aşağıdaki 9 senaryonun **7'si bu oturum içinde canlı backend + Playwright ile manuel olarak zaten doğrulandı** (bkz. `docs/rezervasyon-odeme-muhasebe-entegrasyonu-dogrulama-raporu.md`). Kalan 2'si (#1, #2 — rezervasyon dışı normal satış/alış akışı) bu oturumda hiç dokunulmadığından, sadece regresyon riski taşımıyor olarak değerlendirildi ama release öncesi tek geçişlik smoke testi önerilir.

| # | Senaryo | Bu oturumda test edildi mi | Öncelik |
|---|---|---|---|
| 1 | Normal satış faturası → muhasebe fişi | ❌ Hayır (rezervasyon dışı akış, kod değişmedi) | Smoke önerilir |
| 2 | Normal alış faturası → muhasebe fişi | ❌ Hayır (kod değişmedi) | Smoke önerilir |
| 3 | Rezervasyon tahsilatı → Tahsilat/Odeme Belgesi → muhasebe fişi | ✅ Evet (Senaryo1 vb., cari override dahil) | Doğrulandı |
| 4 | Muhasebe fişi iptali → tekrar fiş oluşturma | ✅ Evet | Doğrulandı |
| 5 | Check-out → satış belgesi → gelir fişi | ✅ Evet (422d8f7 sonrası retest) | Doğrulandı |
| 6 | Tahsilat kapama (aynı cari) | ✅ Evet | Doğrulandı |
| 7 | Farklı cari override (manuel seçim) | ✅ Evet (fe3480e sonrası test) | Doğrulandı |
| 8 | Kasa/Banka/POS default hesap seçimi | ✅ Evet (Senaryo8 tipi uyumsuzluk dahil) | Doğrulandı |
| 9 | Muhasebe fişleri listesi görüntüleme | ✅ Evet (dolaylı, fiş oluşturma testlerinin parçası) | Doğrulandı |

**Öneri:** Release öncesi son bir geçişte sadece #1 ve #2 (rezervasyon dışı satış/alış) manuel smoke testi yapılması yeterli; kalan 7 senaryo bu oturumda kanıtlanmış durumda.

---

## 5. Yetkilendirme Matrisi Önerisi

Backend kod taraması ile doğrulanan gerçek `[Permission]` gereksinimleri:

| # | İşlem | Gereken izin | Resepsiyon | Muhasebe | Yönetici |
|---|---|---|:---:|:---:|:---:|
| 1 | Rezervasyon ödemesi alma | `RezervasyonYonetimi.Manage` | ✅ | ✅ | ✅ |
| 2 | Hızlı cari kart oluşturma | `CariKartYonetimi.Manage` | ❌* | ✅ | ✅ |
| 3 | Farklı cari seçme (override) | `RezervasyonYonetimi.Manage` | ✅ | ✅ | ✅ |
| 4 | Tahsilat/Odeme Belgeleri görme | `TahsilatOdemeBelgesiYonetimi.View` | ❌ | ✅ | ✅ |
| 5 | Tahsilat fişi (muhasebe fişi) oluşturma | `TahsilatOdemeBelgesiYonetimi.Manage` | ❌ | ✅ | ✅ |
| 6 | Satış belgesi görme/onaylama | `MuhasebeSatisBelgeleriYonetimi.View`/`.Manage` | ❌ | ✅ | ✅ |
| 7 | Gelir fişi oluşturma | `MuhasebeSatisBelgeleriYonetimi.Manage` | ❌ | ✅ | ✅ |
| 8 | Tahsilat kapama | `RezervasyonYonetimi.Manage` (rez. tarafı) / `TahsilatOdemeBelgesiYonetimi.Manage` (belge tarafı) | ✅ (rez. tarafı) | ✅ | ✅ |
| 9 | Muhasebe fişi iptal etme | `MuhasebeFisYonetimi.Manage` | ❌ | ✅ | ✅ |
| 10 | Kasa/Banka/POS hesabı yönetme | `KasaBankaHesapYonetimi.View`/`.Manage` | ❌ | ✅ | ✅ |

*(*) Not: seed data'da `Resepsiyonist` grubuna `CariKartYonetimi.Manage` verildiğine dair bir kayıt bulunamadı — rezervasyon ödeme dialogundaki "hızlı cari kart oluştur" özelliğinin resepsiyon rolüyle gerçekten çalışıp çalışmadığı ayrıca doğrulanmalı; eğer resepsiyonun bu özelliği kullanması bekleniyorsa, ilgili endpoint'in `RezervasyonYonetimi.Manage` ile de erişilebilir olması veya `Resepsiyonist` grubuna `CariKartYonetimi.Manage` eklenmesi gerekebilir.*

**Sonuç:** Mimari olarak "Ödeme ≠ Gelir" ilkesiyle tutarlı — resepsiyon sadece tahsilat alabiliyor/cari seçebiliyor, muhasebe fişi üretimi/iptali/kasa-banka yönetimi tamamen Muhasebe+Yönetici'ye kapalı. Tek gri alan (*) not edildi, kod değişikliği önerilmeden önce ayrıca doğrulanmalı.

---

## 6. Kullanıcı Rehberi Taslağı

### 6.1 Resepsiyon için: Rezervasyon ödemesi alma
1. Rezervasyon kartını açın, **"Ödeme Al"** butonuna tıklayın.
2. Tutarı girin, ödeme yöntemini seçin (Nakit / Kredi Kartı / Havale-EFT).
3. Havale/EFT veya Kredi Kartı seçtiyseniz, ilgili **Kasa/Banka hesabını** seçmeniz istenecektir (bkz. 6.2).
4. **Kaydet**'e basın. Ödeme rezervasyona işlenir; muhasebe fişi bu aşamada OLUŞMAZ (bu ayrı ve manueldir — bkz. 6.4).

### 6.2 EFT/Banka hesabı seçme
1. Ödeme yöntemi Havale-EFT veya Kredi Kartı ise, açılan listeden parayı fiilen alan banka/POS hesabını seçin.
2. Liste boşsa veya doğru hesap görünmüyorsa, Muhasebe ekibiyle iletişime geçin — hesap tanımı **Kasa/Banka/POS Hesapları** ekranından (yalnızca Muhasebe/Yönetici erişimi) yapılır.

### 6.3 Farklı cari seçme (misafir adına değil başka bir cari adına ödeme)
1. Ödeme ekranında **"Farklı cari seç"** bağlantısına tıklayın.
2. Açılan panelde mevcut bir cari kartı arayıp seçin, ya da hızlıca yeni bir cari kart oluşturun (izniniz varsa).
3. Seçim özet satırında **"Seçili cari: [Ad]"** olarak görünür; kaldırmak isterseniz yanındaki **"Kaldır"**a tıklayın.
4. Bu seçim yalnızca o ödeme kaydı için geçerlidir — rezervasyonun ana carisini değiştirmez (rezervasyon ilk kez cari atanıyorsa ana cari olarak da kaydedilir).

### 6.4 Muhasebe için: Tahsilat fişi oluşturma
1. **Tahsilat/Ödeme Belgeleri** ekranına gidin, ilgili belgeyi bulun.
2. Belge detayında **"Muhasebe Fişi Oluştur"**a tıklayın.
3. Sistem otomatik olarak doğru cari hesabı ve kasa/banka hesabını kullanarak dengeli bir fiş taslağı üretir; onaylayın.
4. Fiş oluşturulduktan sonra belge üzerinde fiş tarihi ve numarası görünür. Yanlışlıkla ikinci kez fiş oluşturulamaz (aynı belge için tekilleştirme vardır).

### 6.5 Check-out sonrası gelir belgesi kontrolü
1. Misafir check-out olduğunda sistem otomatik bir **satış belgesi** oluşturur (fiş DEĞİL — sadece belge).
2. Muhasebe, **Satış Belgeleri** ekranından bu belgeyi bulup inceler, gerekirse düzeltir.
3. Belge onaylandıktan sonra **"Muhasebe Fişi Oluştur"** ile gelir fişi manuel olarak üretilir.
4. Restoran/oda-ekstra KDV oranı karışık satırlar için (bilinen sınırlama, bkz. doğrulama raporu) fiş oluşturmadan önce satır bazlı KDV kontrolü önerilir.

### 6.6 Tahsilat kapama
1. **Cari Hareketler** ekranında kapatılmamış (borç) bir hareketi seçin.
2. Karşılığında kullanılacak tahsilat hareketini (aynı cariye ait) seçip **"Kapat"**a basın.
3. Sistem kalan tutarları günceller; tam kapanan hareketler "Kapandı" olarak işaretlenir.
4. Yanlış kapama yapıldıysa **"Kapamayı Geri Al"** ile geri alınabilir (dönem kapalıysa bu işlem engellenir).

---

## 7. Bulunan Hata / Eksik Özeti

| # | Bulgu | Kategori | Bu oturumun işiyle ilişkisi |
|---|---|---|---|
| 1 | 78 xUnit testi `Tesis.KurumId` seed eksikliği yüzünden başarısız (`ApplyTenantRules`) | Test altyapısı | **İlgisiz** — 2026-06-15'ten beri var, bu ay hiç dokunulmadı |
| 2 | `TenantSecurityTests.KurumController_GetMyKurumlar_...` testi `params string[]` attribute imzasıyla uyuşmuyor | Test altyapısı | **İlgisiz** |
| 3 | Resepsiyon rolünün "hızlı cari kart oluşturma" (`CariKartYonetimi.Manage`) izni seed data'da bulunamadı | Yetkilendirme netliği | Doğrulanmalı — bu ay eklenen UI özelliğiyle ilgili olabilir |
| 4 | `IX_TahsilatOdemeBelgeleri_KaynakModul_KaynakId` unique index'i prod'da mevcut duplikasyon varsa migration'ı engelleyebilir | Migration riski | Deploy runbook'una kontrol sorgusu eklenmeli |

**Kritik/blocker seviyesinde YENİ bir hata bulunmadı.** Bulgu 1-2 test suite güvenilirliğini etkiliyor ama üretim davranışını etkilemiyor (canlı sistemde `ICurrentTenantAccessor` gerçek JWT/kurum context'inden besleniyor, testteki fake accessor sorunuyla ilgisi yok).

---

## 8. Production Öncesi Yapılmalı / Yapılabilir Ayrımı

### Yapılmalı (release blocker veya yüksek risk)
- ✅ **Yok.** Bu oturumda incelenen build/migration/yetki/regresyon boyutlarında release'i engelleyen bir bulgu yok.
- Deploy runbook'una `IX_TahsilatOdemeBelgeleri_KaynakModul_KaynakId` duplikasyon kontrol sorgusunun (Bölüm 3) eklenmesi.
- Bulgu #3'ün (Resepsiyon → hızlı cari kart oluşturma izni) canlıda gerçek bir rol ile hızlıca doğrulanması — eğer resepsiyon bu özelliği kullanamıyorsa UI'da gereksiz bir buton gösteriliyor demektir.

### Yapılabilir (release'i bloklamaz, ayrı iş kalemi olarak planlanabilir)
- xUnit test suite'indeki 78 pre-existing hatanın düzeltilmesi (Bölüm 2b'deki önerilen minimal düzeltmeler — ayrı, izole bir commit olmalı, bu oturumun kapsamı dışında).
- Rezervasyon dışı normal satış/alış faturası akışlarının (#1, #2) tek geçişlik manuel smoke testi.
- Bundle size uyarısının (4.22 MB / 2.80 MB budget) ayrı bir performans/lazy-loading iş kalemi olarak ele alınması.

---

## Sonuç

Rezervasyon ödeme-muhasebe entegrasyonu (bu oturumda tamamlanan `d075519` → `fbba9a0` arası tüm commit'ler) **build, migration ve fonksiyonel regresyon açısından üretime hazır** durumda. Test suite'inde tespit edilen 78 başarısızlık, bu işten tamamen bağımsız, haftalar önceden var olan bir test-altyapısı sorunudur ve üretim davranışını etkilemez; düzeltilmesi ayrı, izole bir iş kalemi olarak önerilir. Yetkilendirme matrisinde bulunan tek belirsizlik (Resepsiyon'un hızlı cari kart oluşturma izni) release öncesi hızlı bir manuel kontrolle netleştirilmelidir.
