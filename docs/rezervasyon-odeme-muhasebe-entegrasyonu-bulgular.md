# Rezervasyon Ödeme → Muhasebe Entegrasyonu — Bulgular ve Uygulama Notları

Bu doküman, rezervasyon ödeme ekranının muhasebe modülüne (`TahsilatOdemeBelgesi`) entegre edilmesi
sırasında yapılan analiz ve uygulama çalışmasının bulgularını özetler.

## 1. Mevcut yapıda tespit edilen gerçek boşluklar

- **`Rezervasyon` entity'sinde hiçbir cari kart bağlantısı yoktu.** Ne `CariKartId`, ne `KurumId`, ne
  de `MusteriId` alanı mevcuttu. Konaklama faturalama akışı (`RezervasyonSatisBelgesiService`) da
  rezervasyondan bir cari kart türetmiyordu — misafir bilgisi (ad/TCKN/telefon) sadece fatura taslağına
  request üzerinden aktarılıyordu, kalıcı bir cari kart eşlemesi yoktu.
- **`TahsilatOdemeBelgesi` → `MuhasebeFis` üretimi hiç yoktu.** `MuhasebeKaynakModulleri.TahsilatOdemeBelgesi`
  sabiti sadece `CariHareket.KaynakModul` için kullanılıyordu; fiş üretimi için kullanılmıyordu.
- **`TahsilatOdemeBelgesi.AddAsync` zaten istenen davranışı sağlıyordu:** `KapatilacakCariHareketId`
  `null` bırakılırsa hiçbir `CariHareket` doğmuyor — bu, "ödeme = tahsilat, gelir değil" ayrımının
  üzerine oturduğu mevcut mekanizma oldu, yeniden icat edilmedi.
- **`Rezervasyonlar.OdemeTipleri` ile `Muhasebe.OdemeYontemleri` iki ayrı sabit sınıfıydı** ve
  senkron değildi (`HavaleEft` rezervasyon tarafında yoktu). Hizalandı.

## 2. Uygulanan mimari kararlar

- Rezervasyon ödemesi kaydedilirken **sadece `TahsilatOdemeBelgesi` oluşturulur**, `MuhasebeFis`
  otomatik üretilmez (revizyon isteği #1/#2). Fiş üretimi `ITahsilatOdemeBelgesiMuhasebeFisService`
  üzerinden ayrı, manuel/isteğe bağlı bir aksiyon olarak tasarlandı; kaynak modülden bağımsızdır.
- Cari kart çözümleme sırası: **rezervasyonda önbelleklenmiş → kullanıcının seçtiği → TCKN/VKN veya
  (telefon + ad-soyad birlikte) eşleşmesi → tesisin konfigüre edilmiş varsayılan "Rezervasyon
  Misafirleri" cari kartı → hiçbiri yoksa HTTP 422 ile kullanıcıdan seçim istenir.** Otomatik cari
  kart **oluşturulmaz** (revizyon isteği #4/#5 gereği bilinçli olarak).
- Tahsilat fişinde alacak hesabı hard-code değil: `Tesis.RezervasyonTahsilatAlacakHesapTipi`
  (`Cari` | `AlinanAvans`) ile konfigüre edilebilir.
- `RezervasyonOdeme` ve `TahsilatOdemeBelgesi` aynı transaction içinde oluşur; bildirim commit'ten
  sonra gönderilir (muhasebe adımı başarısız olursa kullanıcı yanlışlıkla "ödeme alındı" bildirimi
  görmesin diye).
- Ödeme iptali fiziksel silme değildir: `RezervasyonOdeme.Durum = Iptal` + bağlı `TahsilatOdemeBelgesi`
  iptali (mevcut `TahsilatOdemeBelgesiService.IptalEtAsync` / `CariHareketKapamaService.GeriAlAsync`)
  + fiş onaylanmışsa `MuhasebeFisService.IptalEtAsync` (ters kayıt) yeniden kullanılır.

## 3. Kritik olay: `dotnet ef migrations add` tehlikeli bir migration üretti

İlk migration üretim denemesinde `dotnet ef migrations add` **102 tabloyu `DropTable`/`CreateTable`
ile yeniden oluşturan** bir migration dosyası üretti (6300+ satır). Bu, veritabanına uygulansaydı
tüm veriyi silip şemayı sıfırdan kurmaya çalışacaktı.

**Kök neden:** `dotnet ef migrations remove` komutu, snapshot dosyasını (`StysAppDbContextModelSnapshot.cs`)
önceki doğru duruma geri almak yerine **neredeyse boş bir duruma düşürdü** (9086 satırdan 21 satıra).
Bu bozuk snapshot üzerinden alınan bir sonraki `migrations add`, EF Core'un modeli "sıfırdan" olarak
yorumlamasına yol açtı.

**Alınan önlem:**
1. Migration hemen uygulanmadan fark edildi (Up() içeriği incelenerek — `CreateTable`/`DropTable`
   sayısı 0 olmalıyken 102/102 çıktı).
2. Bozulan snapshot dosyası `git checkout -- ...StysAppDbContextModelSnapshot.cs` ile geri yüklendi.
3. Migration, temiz snapshot durumundan yeniden üretildi — bu sefer sadece `AddColumn`/`CreateIndex`/
   `AddForeignKey` içeren, 314 satırlık, güvenli/ek nitelikli bir migration çıktı
   (`20260713155404_AddRezervasyonOdemeMuhasebeEntegrasyonu`).
4. Migration **veritabanına uygulanmadı** (`dotnet ef database update` çalıştırılmadı) — geri alınması
   zor bir işlem olduğu için kullanıcı onayı bekleniyor.

**Öneri:** Bu ortamda `dotnet ef migrations remove` komutuna güvenilmemeli. Bir migration'ın yanlış
üretildiği tespit edilirse, `migrations remove` çalıştırmak yerine önce `git status` ile snapshot
dosyasının durumu kontrol edilmeli; gerekirse migration `.cs`/`.Designer.cs` dosyaları elle silinip
snapshot `git checkout` ile geri yüklenmelidir.

## 4. Yetkilendirme kapsamı notu

Mevcut `GET /ui/muhasebe/kasa-banka-hesaplari/tip/{tip}` ve `GET /ui/muhasebe/cari-kartlar` uç
noktaları `KasaBankaHesapYonetimi.View` / `CariKartYonetimi.View` (muhasebe modülü) izni gerektiriyor.
Resepsiyon kullanıcılarının bu izinlere sahip olmama ihtimali yüksek olduğundan, ödeme dialogundaki
kasa/banka ve cari kart seçimleri için **rezervasyon modülü kapsamında, `RezervasyonYonetimi.Manage`
izniyle çalışan iki dar kapsamlı proxy uç noktası** eklendi:
- `GET /ui/rezervasyon/kayitlar/{id}/kasa-banka-hesap-secenekleri?odemeTipi=...`
- `GET /ui/rezervasyon/kayitlar/{id}/cari-kart-secenekleri?arama=...`

İkisi de rezervasyonun kendi `TesisId`'sine göre filtrelenir ve minimal alan döner (muhasebe hesap
planı detaylarını göstermez).

## 5. Değişen/eklenen dosyalar

Ayrıntılı liste için `changes.md` içindeki ilgili tur girdisine bakınız.

## 6. İkinci tur — kod inceleme düzeltmeleri

İlk uygulamanın kod incelemesinde 6 madde tespit edildi ve düzeltildi:

1. **Nested transaction riski:** `RezervasyonService.IptalOdemeAsync` transaction açıyor, içeride
   çağrılan `TahsilatOdemeBelgesiService.IptalEtAsync` da koşulsuz `BeginTransactionAsync` açıyordu.
   `IptalEtAsync` artık `CariHareketKapamaService.GeriAlAsync` ile aynı `ownsTransaction` desenini
   kullanıyor — ambient transaction varsa kendi transaction'ını açmıyor/kapatmıyor.
2. **Atlanan validasyonlar:** `RezervasyonOdemeMuhasebeService.TahsilatOlusturAsync`,
   `TahsilatOdemeBelgesi`'ni `TahsilatOdemeBelgesiService.AddAsync` yerine doğrudan `DbContext` ile
   eklediği için `ValidateAsync`/`EnsureOpenPeriodAsync`/`ValidateKapatilacakCariHareketAsync`
   atlanıyordu. Bu üçü `ITahsilatOdemeBelgesiService.ValidateOlusturmaAsync(...)` adıyla ortak,
   public bir metoda çıkarıldı; hem `AddAsync` hem `RezervasyonOdemeMuhasebeService` aynı metodu
   çağırıyor — tek doğrulama kaynağı korunuyor.
3. **Yetersiz duplicate koruması:** `RezervasyonOdemeler.TahsilatOdemeBelgesiId` üzerindeki unique
   index tek başına yeterli değildi (uygulama dışı/elle DB erişiminde aynı kaynaktan ikinci belge
   üretilebilirdi). `TahsilatOdemeBelgeleri` üzerinde `(KaynakModul, KaynakId)` için
   `IsDeleted = 0 AND KaynakId IS NOT NULL` filtreli unique index eklendi — iki katmanlı savunma.
4. **BelgeNo race condition:** `GenerateBelgeNoAsync` MAX+1 sorgusuna dayandığından yarış durumuna
   açıktı. `BelgeNo` üzerindeki mevcut unique index korunarak, ambient transaction içinde
   `IDbContextTransaction.CreateSavepointAsync`/`RollbackToSavepointAsync` ile 3 denemelik güvenli
   retry eklendi (`SatisBelgesiMuhasebeFisService`'teki retry desenine benzer, ancak burada iç içe
   transaction yerine savepoint kullanıldı çünkü çağrı zaten ambient transaction içinde çalışıyor).
5. **Koşulsuz cari kart hesap planı zorunluluğu:** `TahsilatOdemeBelgesiMuhasebeFisService`,
   `Tesis.RezervasyonTahsilatAlacakHesapTipi = AlinanAvans` olsa bile `CariKart.MuhasebeHesapPlaniId`
   zorunlu tutuyordu (ve `ResolveAlacakHesabiAsync` içinde null-forgiving operatörle potansiyel
   `NullReferenceException` riski vardı). Kontrol artık yalnızca `Cari` modunda, `ResolveAlacakHesabiAsync`
   içinde koşullu olarak çalışıyor.
6. **Normalize edilmemiş odemeTipi:** `GetKasaBankaHesapSecenekleriAsync`, `KaydetOdemeAsync`'in
   kullandığı `NormalizeOdemeTipi` metodunu kullanmıyordu; büyük/küçük harf farkı olan bir değer
   sessizce boş liste veya "Gecersiz odeme tipi" hatası dönebiliyordu. Artık aynı normalizasyon
   uygulanıyor.

**Migration güncellemesi:** Fix #3'teki yeni index, henüz veritabanına uygulanmamış olan
`20260713155404_AddRezervasyonOdemeMuhasebeEntegrasyonu.cs` migration dosyasına elle eklendi (aracı
tekrar riske atmamak için `dotnet ef migrations add` çalıştırılmadı — snapshot dosyası da elle
güncellendi). Doğrulama için geçici bir `dotnet ef migrations add ZZZ_CheckNoPendingChanges` denemesi
yapıldı; **boş** (0 operasyon) migration üretmesi, elle yapılan snapshot düzenlemesinin modelle tam
örtüştüğünü doğruladı. Bu geçici dosyalar `dotnet ef migrations remove` KULLANILMADAN elle silindi.

## 7. Üçüncü tur — Fix #5'in eksik kalan yarısı

İkinci tur düzeltmesinde `TahsilatOdemeBelgesiMuhasebeFisService.ResolveAlacakHesabiAsync`'teki
koşulsuz `CariKart.MuhasebeHesapPlaniId` zorunluluğu giderilmişti, ancak **fiş üretiminden önceki**
`TahsilatOdemeBelgesiService.ValidateAsync` (AddAsync/`ValidateOlusturmaAsync` üzerinden) hâlâ
koşulsuzdu — yani `AlinanAvans` modunda dahi `RezervasyonOdemeMuhasebeService.TahsilatOlusturAsync`,
cari kartın `MuhasebeHesapPlaniId`'si boşsa `TahsilatOdemeBelgesi` oluşturma aşamasında (fiş üretiminden
çok önce) hatayla karşılaşıyordu.

Çözüm: `ValidateAsync`/`ValidateOlusturmaAsync`/`ITahsilatOdemeBelgesiService.ValidateOlusturmaAsync`
imzasına `bool requireCariMuhasebeHesabi` parametresi eklendi:
- Genel `TahsilatOdemeBelgesi` ekranı (`AddAsync`, `UpdateAsync`) her zaman `true` geçiriyor — bu ekran
  her durumda cari hesap üzerinden çalışır.
- `RezervasyonOdemeMuhasebeService.TahsilatOlusturAsync`, çağrıdan önce `Tesis.RezervasyonTahsilatAlacakHesapTipi`'ni
  okuyup `requireCariMuhasebeHesabi = (tipi != AlinanAvans)` olarak hesaplıyor ve bunu geçiriyor —
  böylece `AlinanAvans` modundaki tesislerde, muhasebe hesap planı bağlantısı olmayan bir misafir cari
  kartıyla da rezervasyon tahsilatı sorunsuz oluşturulabiliyor.

## 8. Dördüncü tur — uçtan uca test (gerçek SQL Server test DB'sine karşı)

Üç commit (`1a93b5d`, `e4942fa`, `82cc2a4`) merge öncesi test edildi: `dotnet clean/restore/build`,
migration/snapshot tutarlılığı (`ZZZ_CheckNoPendingChanges` boş diff verdi), tam migration script
üretimi ve 12 alan/index'in script içinde doğrulanması, yerel docker test DB'sine (`stys-mssql`,
`localhost:14333/STYSDB`) migration uygulaması ve 8 senaryonun tamamının gerçek DB'ye karşı çalıştırılması.

**Bulunan ve düzeltilen hata:** `tests/STYS.Tests/RezervasyonServiceTests.cs` derlenmiyordu —
`CreateService` helper'ı, `RezervasyonService` constructor'ına eklenen `IRezervasyonOdemeMuhasebeService`
parametresini geçmiyordu. Düzeltme: `FakeRezervasyonOdemeMuhasebeService` eklendi; bu Fake gerçek
servisin gözlemlenebilir davranışını (KasaBankaHesapId zorunluluğu) yansıtır ama InMemory provider'ın
desteklemediği TahsilatOdemeBelgesi/unique-index/transaction zincirini üretmez — o yüzden asıl 8
senaryo doğrulaması ayrı, gerçek SQL Server'a karşı çalışan `RezervasyonOdemeMuhasebeIntegrationTests.cs`
dosyasında yapıldı (InMemory provider unique index/FK/savepoint semantiğini desteklemediğinden
mevcut InMemory tabanlı test konvansiyonu bu senaryolar için yetersizdi).

**Pre-existing, bu değişikliklerle ilgisiz bulgu:** `RezervasyonServiceTests.cs` içindeki 72/144 test
`"Aktif kurum bilgisi bulunamadi"` hatasıyla başarısız oluyor (seed helper'ları `ApplyTenantRules`
için tenant context sağlamıyor). Bu değişikliklerden önceki commit'te (`a0a2ba5`) izole bir
`git worktree` ile dogrulandi: aynı 72/72 sonuç. Regresyon değil, ayrı bir iş kalemi.

**8 senaryo sonucu:** Tümü başarılı, iki ayrı çalıştırmada tutarlı, test sonrası DB'de kalıntı kalmadı.
Ayrıntı ve senaryo bazlı sonuç tablosu bu konuşmanın test raporunda mevcuttur.

## 9. Beşinci tur — entegrasyon testlerinin izolasyonu (merge öncesi düzeltme)

İlk halinde `RezervasyonOdemeMuhasebeIntegrationTests.cs` bağlantı dizesini kod içinde sabit (hard-coded,
şifre dahil) tutuyordu ve testler düz `[Fact]` olduğu için normal `dotnet test` akışında da
çalışmaya çalışıp yerel SQL Server olmadan başarısız oluyordu. Düzeltildi:

- Bağlantı dizesi artık **`STYS_INTEGRATION_TEST_CONNECTION_STRING`** ortam değişkeninden okunuyor;
  kod içinde hiçbir sabit/şifre yok.
- Özel bir `IntegrationFactAttribute : FactAttribute` eklendi: bu değişken tanımlı değilse xUnit'in
  `Skip` mekanizmasıyla testi **çalıştırmadan** "Skipped" olarak işaretler. `InitializeAsync`,
  `DisposeAsync` ve `CreateDbContext` içinde de aynı kontrol savunma amaçlı tekrarlandı (xUnit
  sürümleri arasında `IAsyncLifetime` çağrılma zamanlaması farklılaşabildiği için).
- Sınıfa `[Trait("Category", "Integration")]` eklendi.
- Doğrulandı: değişken tanımsızken `dotnet test` → **8/8 Skipped, 21ms, hiç DB bağlantısı yok**;
  değişken tanımlıyken `dotnet test --filter Category=Integration` → **8/8 Passed**.

### Çalıştırma komutları

```bash
# Normal test (yerel SQL Server GEREKMEZ — entegrasyon testleri otomatik atlanır)
dotnet test

# Entegrasyon testleri (gercek/test SQL Server'a karsi calisir)
STYS_INTEGRATION_TEST_CONNECTION_STRING="Server=<host>,<port>;Database=<db>;User Id=<user>;Password=<password>;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True" \
  dotnet test --filter Category=Integration
```

Yerel docker test ortamı için örnek (gerçek şifre burada **verilmez** — kendi ortamınızdaki değeri
kullanın, örn. `docker-compose.yml` içindeki `SA_PASSWORD` veya ekip içi paylaşılan secret):

```
STYS_INTEGRATION_TEST_CONNECTION_STRING="Server=localhost,14333;Database=STYSDB;User Id=sa;Password=<yerel-test-sifreniz>;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

PowerShell'de: `$env:STYS_INTEGRATION_TEST_CONNECTION_STRING = "..."` şeklinde tanımlanıp aynı
`dotnet test --filter Category=Integration` komutu çalıştırılır.
