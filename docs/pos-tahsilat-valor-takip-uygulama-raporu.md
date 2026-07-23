# Kredi Kartı/POS Valör Takibi ve Banka Hesabına Aktarım — Uygulama Raporu

Tarih: 2026-07-23 (ilk uygulama, commit 903fa6a) — güncelleme 1: 2026-07-23 (kod incelemesi
düzeltmeleri, commit c7ceecb) — güncelleme 2: 2026-07-23 (FisNo sayaç migration/concurrency
düzeltmeleri, commit 43d0c83) — güncelleme 3: 2026-07-23 (backfill migration, Adım B
transaction/retry sertleştirmesi, gerçek DB-seviyeli eşzamanlılık testleri, commit 43d0c83 sonrası)

## Güncelleme 3 — Backfill migration, Adım B transaction/retry sertleştirmesi, gerçek eşzamanlılık testleri (commit 43d0c83 sonrası)

`43d0c83` üzerindeki üçüncü bir kod incelemesinde 6 madde tespit edildi ve düzeltildi:

1. **`AddPosValorFisNoSayaci` migrationı geriye dönük değiştirilmedi.** Önceki turda o migrationın
   `Up()`'ına eklenen seed SQL bloğu **geri alındı** (migration artık yalnızca tabloyu/index'i
   oluşturuyor — c7ceecb'deki orijinal haline döndü). Bunun yerine **yeni, bağımsız bir migration**
   (`BackfillPosValorFisNoSayaclari`) eklendi: mevcut POS valör transfer fişlerinden (1) eksik sayaç
   satırlarını oluşturur, (2) var olan ama gerçek veriden **geri kalmış** (`SonNumara` < gerçek
   maksimum) sayaçları yükseltir — **asla azaltmaz**. İki adım da idempotent (`NOT EXISTS`/`<`
   koşullu), tekrar tekrar çalıştırılabilir. `Down()` kasıtlı olarak no-op (bu bir veri onarımıdır,
   geri alınacak bir şema yoktur).
   **Gerçek SQL Server'a karşı doğrulandı**: veritabanı önce `AddPosTahsilatValorMenuVeYetki`'ye
   (bu migrationdan önceki noktaya) geri alındı, sonra yalnızca `AddPosValorFisNoSayaci`'nin GÜNCEL
   (seed'siz, c7ceecb-dönemi) hali uygulanarak "eski bir veritabanı" simüle edildi. Üç senaryo elle
   test edildi: (a) legacy fiş var, sayaç YOK → backfill sayacı `SonNumara=3` ile oluşturdu; (b)
   legacy fiş var (gerçek maks=7), sayaç VAR ama geride (`SonNumara=2`) → backfill `7`'ye yükseltti;
   (c) sayaç zaten gerçek maksimumun (10) ÖNÜNDE (`SonNumara=50`) → backfill **azaltmadı**, `50`
   olarak kaldı. Migration ikinci kez çalıştırıldığında (idempotency kontrolü) hiçbir değer
   değişmedi. Test verileri temizlendi, veritabanı tekrar tüm migrationların başına (`Backfill...`
   dahil) güncellendi.
2. **FisNo unique-index çakışmasında artık aynı transaction/context'te devam edilmiyor.**
   `ValidateVeFisOlusturAsync` ve `GenerateFisNoAsync`'in kendi iç retry döngüleri kaldırıldı; bunun
   yerine bu iki yer, çakışma (FisNo unique index veya sayaç satırının ilk oluşturulmasındaki unique
   çakışma) tespit ettiğinde yeni bir `PosValorAdimBYenidenDenemeException` fırlatıyor.
   `HesabaAktarAsync`'in Adım B döngüsü bu istisnayı yakalayıp: (a) **tüm Adım B transaction'ını**
   rollback ediyor, (b) `ChangeTracker.Clear()` ile önceki denemenin tüm izlenen entity'lerini
   temizliyor, (c) sayacı **ayrı, yeni bir transaction'da** (rollback sonrası ambient transaction
   kalmadığı için `CorrectFisNoSayaciAsync`'in kendi `SaveChangesAsync`'i kendi otonom transaction'ını
   açıyor) gerçek maksimuma yükseltiyor, (d) Adım B'yi **sınırlı sayıda (3)** baştan deniyor. Önceki
   (hatalı) tasarımda bu düzeltme AYNI, birazdan rollback edilecek transaction içinde yapılıyordu —
   rollback ile düzeltme de anlamsız hale geliyordu. (Not: bu servis, projedeki genel `DbContext`
   kullanım deseniyle tutarlı olarak TEK bir enjekte edilmiş `StysAppDbContext` üzerinden çalışır —
   projede hiçbir yerde `IDbContextFactory` kullanılmıyor; "yeni transaction" burada rollback+
   `ChangeTracker.Clear()` sonrası tamamen bağımsız/temiz bir transaction anlamına gelir, ayrı bir
   DbContext örneği değil — bu, mevcut mimariyi genişletme ilkesiyle tutarlıdır.)
3. **Adım B'nin `BeginTransactionAsync` çağrısı artık hata/cleanup kapsamında.** Daha önce bu çağrı
   try/catch DIŞINDAYDI — claim (Adım A, ayrı/commit edilmiş bir transaction'da) alındıktan sonra
   transaction açma işlemi (bağlantı hatası vb.) başarısız olursa istisna doğrudan yukarı fırlıyor ve
   kayıt `Aktariliyor` durumunda, `ClaimToken` dolu şekilde kalıyordu — yalnızca 15 dakikalık "stuck"
   eşiği geçtikten sonra kurtarılabiliyordu. Artık `BeginTransactionAsync` try/catch içine alındı:
   normal bir hata durumunda kayıt `ClaimToken` koşuluyla kontrollü bir `Hata` durumuna hemen
   getiriliyor; `OperationCanceledException` durumunda ise (önceki durum `Aktariliyor` ise `Hata`'ya,
   değilse önceki duruma) aynı mantıkla geri alınıyor — hiçbir durumda 15 dakika beklemek
   gerekmiyor.
4. **Senaryo 2 artık gerçek bir DB-seviyeli bariyerle senkronize ediliyor.** Önceki turda kullanılan
   "çağrı ÖNCESİNDEKİ gate" yaklaşımının bunu GERÇEKTEN garanti ETMEDİĞİ kabul edildi — iki task'ın
   `HesabaAktarAsync` içinde hangi noktada olduğu belirsizdi. Yerine gerçek bir `DbCommandInterceptor`
   (`SayacSelectBarrierInterceptor`) eklendi: SQL komut metni incelenip, sayaç üzerindeki kilitli
   SELECT (`WITH UPDLOCK/ROWLOCK/HOLDLOCK`) **SQL Server'a gönderilmeden hemen önce**
   (`ReaderExecutingAsync`) her iki taraf da bir bariyerde durduruluyor, ikisi de hazır olunca AYNI
   ANDA serbest bırakılıyor. **Önemli bir kendi-kendine-deadlock bulundu ve düzeltildi**: bariyer ilk
   denemede SELECT TAMAMLANDIKTAN SONRA (`ReaderExecutedAsync`) konulmuştu — ama `UPDLOCK`, başka bir
   `UPDLOCK` ile UYUMSUZ olduğundan, ilk tamamlanan taraf transaction'ını açık tutup (kilidi elinde)
   bariyerde beklerken, ikinci taraf AYNI kilidi beklemek zorunda kalıp SQL Server seviyesinde bloke
   oluyor ve interceptor'a hiç ulaşamıyordu — iki taraf da asla "hazır" sinyali veremiyordu. Bariyer
   komut gönderilmeden ÖNCEYE taşınarak bu kendi-kendine-deadlock giderildi.
5. **`SafeDuzeltmeAsync` artık yalnızca beklenen 409'u yutuyor.** Önceden tüm `BaseException`
   türlerini koşulsuz yutuyordu; artık `when (ex.ErrorCode == 409)` koşuluyla sınırlandı — 500/403/422
   gibi beklenmeyen bir hata kodu gelirse test başarısız olur (koşulsuz yutma kaldırıldı).
6. **Senaryo 8 artık `TahsilatOdemeBelgesiService.IptalEtAsync`'i çağırıyor.** Önceden doğrudan
   `PosTahsilatValorSnapshotService.IptalEtAsync` çağrılıyordu — bu, production'da tahsilat iptalinin
   ambient transaction'ını yöneten gerçek giriş noktasını (cari hareket kapamasının geri alınması +
   POS valör snapshot iptalinin AYNI transaction'da yürütülmesi) atlıyordu. Artık gerçek
   `TahsilatOdemeBelgesiService` (gerçek `CariHareketKapamaService`, gerçek `MuhasebeFisService` ile)
   kuruluyor ve test, tahsilat belgesinin kendi `Durum`'unun da (valör iptal olduğunda `Iptal`)
   tutarlı olduğunu ayrıca doğruluyor.

### Değişen/eklenen dosyalar (bu güncellemede)
- `backend/Infrastructure/EntityFramework/Migrations/20260723075626_AddPosValorFisNoSayaci.cs`
  (seed SQL bloğu GERİ ALINDI — migration c7ceecb'deki orijinal haline döndü)
- `backend/Infrastructure/EntityFramework/Migrations/20260723111420_BackfillPosValorFisNoSayaclari.cs`
  (yeni, bağımsız backfill migrationı)
- `backend/Muhasebe/PosTahsilatValorleri/Services/PosTahsilatValorAktarimService.cs`
  (`PosValorAdimBYenidenDenemeException`, Adım B'nin birleşik deadlock/FisNo-çakışması retry
  döngüsü, `BeginTransactionAsync` hata/cleanup kapsamı, `GenerateFisNoAsync`/
  `ValidateVeFisOlusturAsync`'in iç retry döngülerinin kaldırılması)
- `tests/STYS.Tests/PosTahsilatValorIntegrationTests.cs` (`SayacSelectBarrierInterceptor` ile
  Senaryo 2'nin gerçek DB-seviyeli bariyere taşınması, `CreateTahsilatOdemeBelgesiService` yardımcısı,
  Senaryo 8'in gerçek `TahsilatOdemeBelgesiService.IptalEtAsync` çağırması, `SafeDuzeltmeAsync`'in
  yalnızca 409 yutması)

### Çalıştırılan komutlar ve sonuçlar
- **Backfill migration manuel doğrulaması** (`localhost,14333/STYSDB`): veritabanı
  `AddPosTahsilatValorMenuVeYetki`'ye geri alındı → yalnızca güncel (seed'siz)
  `AddPosValorFisNoSayaci` uygulandı ("eski veritabanı" simülasyonu) → 3 farklı senaryoda (eksik
  sayaç / geride kalmış sayaç / önde olan sayaç) test verisi eklendi → `BackfillPosValorFisNoSayaclari`
  uygulandı → sonuçlar doğrulandı (sırasıyla `3`, `7`, `50` — hiçbiri azaltılmadı) → migration ikinci
  kez çalıştırılıp idempotency doğrulandı (değer değişmedi) → test verisi temizlendi → veritabanı
  tekrar migration head'ine güncellendi.
- `dotnet build backend/STYS.csproj` → **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet build tests/STYS.Tests/STYS.Tests.csproj` → **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet test --filter FullyQualifiedName~PosTahsilatValor` (gerçek SQL Server'a karşı, 9 senaryo,
  Senaryo 2'nin yeni DB-seviyeli bariyeri ve Senaryo 8'in yeni `TahsilatOdemeBelgesiService` çağrısı
  dahil) → **9/9 Passed, 0 Failed, 0 Skipped**.
- `dotnet test tests/STYS.Tests` (tüm proje, env var ayarlı) → **324 Passed, 0 Failed, 0 Skipped**.
- `ng build` (frontend) → **başarılı** (yalnızca ön var olan bundle-size uyarısı, hata değil).

---

## Güncelleme 2 — FisNo sayaç migration ve eşzamanlılık düzeltmeleri (commit c7ceecb sonrası)

`c7ceecb` üzerinde yapılan ikinci bir kod incelemesinde, `AddPosValorFisNoSayaci` migration'ı ve
FisNo üretiminin eşzamanlılık doğruluğu hakkında 5 madde tespit edildi ve düzeltildi:

1. **Migration artık boş bir sayaç tablosu bırakmıyor.** `Up()` içine, mevcut `MuhasebeFisler`
   tablosunda `KaynakModul=PosTahsilatValorTransferi` ve `FisNo` formatı `YYYY-VLR-NNNNNN` olan
   kayıtları tesis+mali yıl bazında tarayıp **gerçek en büyük numarayı** `PosValorFisNoSayaclari`'na
   yazan bir `migrationBuilder.Sql()` bloğu eklendi. Sorgu `NOT EXISTS` ile korunuyor — migration
   birden fazla kez çalıştırılsa (örn. script olarak yeniden uygulansa) mevcut sayaç satırlarının
   üzerine yazmıyor (idempotent). **Gerçek bir SQL Server'a karşı manuel doğrulandı**: migration geri
   alındı, `TesisId=999999, FisNo='2026-VLR-000001'` ile elle bir legacy fiş eklendi, migration tekrar
   uygulandı, `PosValorFisNoSayaclari`'nda o tesis/yıl için `SonNumara=1` satırının doğru şekilde
   oluştuğu doğrulandı, ardından test verisi temizlendi.
2. **Sayaç yokken artık `SonNumara=1` varsayılmıyor.** `GenerateFisNoAsync`, sayaç satırı yoksa artık
   `ComputeGercekMaksimumNumaraAsync` ile `MuhasebeFisler` üzerinde gerçek maksimum numarayı hesaplayıp
   sayaç satırını `SonNumara = gerçekMaks + 1` ile oluşturuyor — legacy/migration-öncesi fişlerle
   çakışma riski ortadan kalktı. FisNo unique index çakışmasında (aynı numara başka bir fiş tarafından
   zaten kullanılmış), aynı transaction'da sonsuz döngüye girmek yerine rollback sonrası **yeni bir
   transaction'da** `CorrectFisNoSayaciAsync` ile sayaç gerçek maksimuma göre düzeltiliyor, ardından
   en fazla 3 denemeye kadar sınırlı şekilde tekrar deneniyor.
3. **Deadlock (SQL Server 1205) artık aynı transaction'da devam etmiyor.** `HesabaAktarAsync`'in Adım
   B'si, 1205 hatası (`IsDeadlock` ile sınıflandırılıyor) alırsa **tüm transaction'ı** (yeni context/
   transaction ile) en fazla 3 kez baştan deniyor — kısmi/eski bir transaction içinde ilerlemiyor. Bu,
   sayaç satırının ilk kez oluşturulması için yarışan iki eşzamanlı sürecin deterministik bir
   `SemaphoreSlim`+`CountdownEvent` bariyeriyle senkronize edildiği **Senaryo 2** testiyle (yeniden
   yazıldı) doğrulandı: her iki taraf da başarıyla tamamlanıyor, sayaç tablosunda bu tesis/yıl için
   **tam olarak bir** satır oluşuyor ve `SonNumara=2` (iki başarılı aktarımı yansıtıyor).
4. **Senaryo 8 (aktarım/tahsilat-iptali yarışı) güçlendirildi.** Artık valör `Iptal` sonuçlanırsa aktif/
   Onaylı hiçbir transfer fişinin kalmadığı (fiş hiç oluşmamışsa 0 fiş; oluşup sonra geri alınmışsa
   orijinal fiş `Durum=Iptal`, karşı fiş `Durum=TersKayit`, `IptalEdilenFisId`/`TersKayitFisId`
   ilişkisi doğru, tam olarak bir ters kayıt) ve **bakiye etkisinin gerçekten sıfırlandığı**
   (`MuhasebeHesapBakiyeleri.NetBakiye` POS/Banka hesapları için toplamda 0) doğrulanıyor. Test
   yardımcıları (`SafeCallAsync`/`TryAktarAsync`) artık **tüm** `BaseException`'ları koşulsuz
   yutmuyor — yalnızca beklenen yarış-kaybı hata kodları (404/409/422 bağlama göre) yutuluyor, başka
   bir kod (örn. 500, 403) test hatası olarak yukarı fırlatılıyor.
5. **Yeni Senaryo 9 eklendi**: bir tesis için `MuhasebeFisler`'da legacy `2026-VLR-000001` fişi
   varken `PosValorFisNoSayaclari`'nda o tesis/yıl için HİÇ satır olmadığı (migration-öncesi/geçiş
   durumu) senaryoda, gerçek bir `HesabaAktarAsync` çağrısının başarıyla tamamlandığı, üretilen FisNo
   numarasının `2026-VLR-000001` ile ÇAKIŞMADIĞI ve `>=000002` olduğu, sayaç tablosunun bu değeri
   doğru yansıttığı doğrulanıyor.

### Değişen/eklenen dosyalar (bu güncellemede)
- `backend/Infrastructure/EntityFramework/Migrations/20260723075626_AddPosValorFisNoSayaci.cs`
  (mevcut fişlerden sayaç seed eden `migrationBuilder.Sql()` bloğu eklendi)
- `backend/Muhasebe/PosTahsilatValorleri/Services/PosTahsilatValorAktarimService.cs`
  (`ComputeGercekMaksimumNumaraAsync`, `CorrectFisNoSayaciAsync`, `IsDeadlock`, deadlock-safe
  transaction-retry döngüsü, FisNo çakışması retry döngüsü)
- `tests/STYS.Tests/PosTahsilatValorIntegrationTests.cs` (Senaryo 2 barrier ile yeniden yazıldı,
  Senaryo 8 güçlendirildi, yeni Senaryo 9 eklendi, `SafeCallAsync` ile koşulsuz exception yutma
  kaldırıldı)

### Çalıştırılan komutlar ve sonuçlar
- **Migration manuel doğrulaması** (`localhost,14333/STYSDB`): migration geri alındı → legacy fiş
  (`2026-VLR-000001`, `TesisId=999999`) elle eklendi → migration yeniden uygulandı → sayaç satırının
  `SonNumara=1` ile doğru oluştuğu doğrulandı → test verisi temizlendi.
- `dotnet ef database update` (güncel migration seti, `AddPosValorFisNoSayaci` dahil) → **başarılı**,
  `dotnet ef migrations list` ile son migration'ın uygulandığı doğrulandı.
- `dotnet build backend/STYS.csproj` → **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet build tests/STYS.Tests/STYS.Tests.csproj` → **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet test --filter FullyQualifiedName~PosTahsilatValor` (gerçek SQL Server'a karşı, 9 senaryo) →
  **9/9 Passed, 0 Failed, 0 Skipped**.
- `dotnet test tests/STYS.Tests` (tüm proje, env var ayarlı) → **324 Passed, 0 Failed, 0 Skipped**.
- `ng build` (frontend) → **başarılı** (yalnızca ön var olan bundle-size uyarısı, hata değil).

---

## Güncelleme 1 — Kod incelemesi düzeltmeleri (commit 903fa6a sonrası)

İlk uygulamanın kod incelemesinde 8 madde tespit edildi ve düzeltildi:

1. **Fiş artık Taslak bırakılmıyor.** Aktarımın muhasebe etkisi hemen doğduğu için, transfer fişi
   oluşturulduktan hemen sonra **aynı transaction içinde** onaylanıyor (`MuhasebeFisService.OnaylaAsync`
   çağrılıyor — YevmiyeNo üretiliyor, hesap bakiyeleri işleniyor, `Durum=Onayli`). Bunu mümkün kılmak
   için `OnaylaAsync`, `IptalEtAsync` ile aynı `ownsTransaction` (ambient transaction) desenine
   taşındı — artık kendi transaction'ını zorla açmıyor, çağıranın açık transaction'ı içinde çalışabiliyor.
   Bu, önceden var olan gizli bir hatayı da düzeltti: fiş Taslak kaldığı için sonradan
   `PosValorTransferFisiniIptalEtAsync` (`Durum==Onayli` şartı arar) hiçbir zaman çalışamazdı.
2. **Tesis yetki kontrolü** `PosTahsilatValorAktarimService` (`HesabaAktarAsync`,
   `DuzeltmeTersKayitAsync`, `ValoruGelenleriHesabaAktarAsync`) ve `PosTahsilatValorService`
   (`GetByIdAsync`, `GetOzetAsync`, `GetTopluOnayBilgisiAsync`) içine eklendi —
   `IUserAccessScopeService` ile kaydın **gerçek** `TesisId`'si (istemcinin gönderdiği değer değil)
   doğrulanıyor; kapsam dışı tesise erişim 403 ile reddediliyor. Arka plan job'u (hosted service)
   sistem akışı olduğundan kapsam dışı bırakıldı (tüm tesisleri tarar, tasarım gereği).
3. **Manuel tutar doğrulaması** sıkılaştırıldı: Brüt>0, 0≤Komisyon≤Brüt, 0≤Net≤Brüt, Brüt=Net+Komisyon
   (`ValidateTutarlar`), ayrıca fiş insert'inden hemen önce gerçek satır toplamlarında
   ToplamBorç=ToplamAlacak ve negatif satır tutarı kontrolü eklendi.
4. **FisNo üretimi** artık `Max(FisNo)+1` yerine `MuhasebeYevmiyeNoSayac` ile aynı
   `WITH (UPDLOCK, ROWLOCK, HOLDLOCK)` + retry desenini kullanan yeni bir sayaç tablosu
   (`PosValorFisNoSayaclari`) üzerinden üretiliyor. `DbUpdateException`, hangi unique index'in
   çakıştığını (`IX_MuhasebeFisler_TesisId_KaynakModul_KaynakId` / `IX_MuhasebeFisler_TesisId_FisNo`)
   ayırt edecek şekilde sınıflandırılıyor; bu iki çakışma türü "mükerrer fiş" olarak ele alınıp
   **DenemeSayisi tüketilmiyor** (özel 499 hata kodu ile işaretlenip dış katmanda ayrı yönetiliyor).
5. **Audit trail eklendi.** `KomisyonTutari`/`NetTutar`/`KomisyonGiderHesapPlaniId` gerçekten
   değiştiğinde (no-op değilse) `PosTahsilatValorDegisiklikGecmisleri` tablosuna eski/yeni değer +
   zorunlu açıklama ile satır yazılıyor (aynı transaction, `BaseEntity.CreatedBy/CreatedAt` ile
   kullanıcı/tarih otomatik damgalanıyor). Değişiklik yoksa audit satırı oluşturulmuyor.
6. **Ters kayıt idempotent lookup düzeltildi.** Orijinal fiş `Iptal` durumundaysa, `IptalEdilenFisId`
   ile bulunan kayıt artık yalnızca var olduğu için değil, `Durum=TersKayit` VE `TesisId` eşleşmesi
   doğrulandıktan sonra geçerli sayılıyor; aksi halde veri tutarsızlığı hatası dönüyor.
7. **Migration öncesi mükerrer kontrolü çalıştırıldı** (yerel dev SQL Server'a karşı,
   `localhost,14333/STYSDB`): 0 satır — mevcut veride sorun yok, migration güvenle uygulandı.
8. **SQL Server entegrasyon testleri eklendi** (`tests/STYS.Tests/PosTahsilatValorIntegrationTests.cs`,
   8 senaryo) — bkz. aşağıdaki "Çalıştırılan komutlar ve sonuçlar" bölümü. Bu testler yazılırken
   gerçek iki hata daha bulundu ve düzeltildi: (a) 422 kullanıcı-hatası yolunda claim'in önceki
   duruma geri döndürülmemesi (kayıt "Aktariliyor"da takılı kalıyordu), (b) test cleanup'ının
   FK sırası hatası (üretim koduyla ilgisiz, yalnızca test temizliği).

### Değişen/eklenen dosyalar (bu güncellemede)
- `backend/Muhasebe/PosTahsilatValorleri/Services/{PosTahsilatValorAktarimService,PosTahsilatValorService}.cs`
- `backend/Muhasebe/PosTahsilatValorleri/Entities/PosValorFisNoSayac.cs` (yeni)
- `backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs` (`OnaylaAsync` ambient transaction,
  `PosValorTransferFisiniIptalEtAsync` idempotent lookup düzeltmesi)
- `backend/Infrastructure/EntityFramework/StysAppDbContext.cs` (yeni DbSet + tablo config)
- Migration: `AddPosValorFisNoSayaci`
- `tests/STYS.Tests/PosTahsilatValorIntegrationTests.cs` (yeni, 8 SQL Server entegrasyon testi)

### Çalıştırılan komutlar ve sonuçlar
- **Migration öncesi mükerrer kontrolü** (`localhost,14333/STYSDB`, salt-okunur):
  `SELECT IptalEdilenFisId, COUNT(*) ... HAVING COUNT(*)>1` → **0 satır** (sorun yok).
- `dotnet build` → **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet ef database update` (yerel dev SQL Server'a, 3 migration: `AddPosTahsilatValorTakip`,
  `AddPosTahsilatValorMenuVeYetki`, `AddPosValorFisNoSayaci`) → **başarılı**.
- `dotnet test tests/STYS.Tests` (env var **ayarlı** — hem InMemory hem gerçek SQL Server entegrasyon
  testleri dahil) → **323 Passed, 0 Failed, 0 Skipped** (önceki turda env var yokken skip edilen 18
  entegrasyon testi de dahil bu kez tamamı çalıştı ve geçti).
- `tests/STYS.Tests/PosTahsilatValorIntegrationTests.cs` (8 yeni senaryo, gerçek SQL Server'a karşı) →
  **8/8 Passed**: eşzamanlı iki aktarım (yalnızca biri başarılı), iki farklı valör kaydı için eşzamanlı
  FisNo üretimi (çakışma yok), negatif komisyon reddi, Net>Brüt reddi, farklı tesis erişimi (403),
  transfer fişi onay/ters-kayıt yaşam döngüsü, aynı kayda iki düzeltme-ters-kayıt isteği (tek ters
  kayıt), aktarım/tahsilat-iptali yarışı (tutarlı sonlanma).
- `ng build` (frontend) → **başarılı** (yalnızca ön var olan bundle-size uyarısı, hata değil).

### Kalan notlar
- FisNo/DenemeSayisi sınıflandırması SQL hata mesajı metni üzerinden index adı eşleştirmesiyle
  yapılıyor (`ClassifyFisConflict`) — SQL Server'a özgü, taşınabilir değil (proje zaten yalnızca SQL
  Server hedefliyor, bu kabul edilebilir).
- Yeni entegrasyon testleri gerçek SQL Server gerektirir ve `STYS_INTEGRATION_TEST_CONNECTION_STRING`
  ortam değişkeni tanımlı değilse otomatik atlanır (CI'da varsayılan davranışı bozmaz).

---

## İlk uygulama (commit 903fa6a)

## Amaç

Kredi kartı/POS ile alınan tahsilatlarda para, alındığı anda banka hesabına geçmiş sayılmamalı;
gerçek dünyada olduğu gibi bankaya "valör" (settlement) günü, komisyon kesintisiyle geçmelidir. Bu
iş, POS tahsilatı için ayrı bir valör takip kaydı ekler ve valör günü geldiğinde (manuel veya
otomatik) POS alacağını (109) bağlı banka hesabına (102) aktaran **ikinci, bağımsız bir muhasebe
fişi** üretir. İlk tahsilat fişinin mevcut mantığı değişmemiştir.

Bu rapor, plan onayından sonra yapılan uygulamayı, tasarım kararlarını, değişen/eklenen dosyaları,
build/test sonuçlarını ve bilinen riskleri özetler.

## Mevcut sistemde tespit edilen yapı

- `KasaBankaHesap` zaten `ValorGunSayisi` ve `BagliBankaHesapId` alanlarını taşıyordu; POS→banka
  valör transferi mantığı hiç yoktu.
- `TahsilatOdemeBelgesiMuhasebeFisService`, ilk tahsilat fişini (Borç=109, Alacak=Cari/Avans) her
  zaman **ayrı, manuel tetiklenen** bir aksiyonla üretiyor — belge oluşurken otomatik değil.
- `MuhasebeFis` tablosunda `(TesisId, KaynakModul, KaynakId)` üzerinde unique filtered index zaten
  vardı, ancak `IptalEdilenFisId` üzerinde unique index **yoktu** — aynı orijinal fişe ikinci bir
  ters kayıt oluşturulmasını DB seviyesinde engelleyen bir mekanizma eksikti (bu işte eklendi).
- `TahsilatOdemeBelgesiService.IptalEtAsync`, bugün **hiçbir ödeme yönteminde** belgeye bağlı ilk
  muhasebe fişini iptal/ters-kayıt etmiyor (yalnızca cari hareket kapamasını geri alıyor). Bu,
  mevcut sistemde önceden var olan, bu işten bağımsız bir tasarım boşluğudur — **kapsam dışı
  bırakılmıştır**, düzeltilmesi istenirse ayrı bir iş kalemi ve nakit/banka/kredi kartı tüm ödeme
  yöntemleri için regresyon testi gerektirir.

## Önemli tasarım kararları

- **Valör tarihi `DateOnly`**: valör bir an değil bir takvim günü olduğundan `DateTime` yerine
  `DateOnly` kullanıldı; "bugün" karşılaştırması Europe/Istanbul saat dilimine göre yapılır.
- **Komisyon hesaplama**: `KasaBankaHesap.KomisyonOrani` (yüzde, sabit oran) tanımlıysa komisyon/net
  otomatik ve kesin hesaplanır; tanımsızsa ve otomatik aktarım açıksa kayıt `MutabakatBekliyor`
  durumuna düşer ve **job bu durumu asla otomatik işlemez** — yalnızca manuel aktarımda komisyon
  bilgisi açıkça girilerek ilerletilebilir. Yuvarlama: `Math.Round(x, 2, MidpointRounding.AwayFromZero)`.
- **Claim/lease + concurrency token**: Aktarım işlemi iki aşamalıdır (kısa "claim" transaction'ı +
  ayrı "iş" transaction'ı). `PosTahsilatValor.ClaimToken` EF Core concurrency token olarak
  işaretlenmiştir; `WITH (UPDLOCK, ROWLOCK)` satır kilidiyle birlikte, bir fişin oluşup kaydın
  "Aktarıldı" durumuna geçmeden kalması (orphan fiş) yapısal olarak imkânsız hale getirilmiştir.
- **`MutabakatBekliyor` durumu**: komisyon oranı bilinmeyen otomatik hesaplarda güvenlik amaçlı ara
  durum; brüt tutarın yanlışlıkla net olarak bankaya yazılmasını engeller.
- **`TersKayitOlusturuluyor` durumu**: "düzeltme/ters kayıt" işleminin kendi ayrı ara-durumu; normal
  POS aktarım claim'i ve arka plan job'u bu durumu hiç görmez — düzeltme işleminin yanlışlıkla normal
  bir aktarım gibi kurtarılması engellenmiştir.
- **`IptalEdilenFisId` unique index**: `IsDeleted` filtresi bilinçli olarak eklenmedi — bir ters
  kayıt fişi soft-delete edilse bile aynı orijinal fişe ikinci bir ters kayıt açılmasına izin
  verilmemesi gerektiği için.
- **Basitleştirme**: plan taslağında önerilen özel exception sınıfları (`PosValorKullaniciHatasiException`
  vb.) yerine, projenin mevcut `BaseException(mesaj, errorCode)` deseni kullanıldı — 422 kullanıcı
  hatası (kayda dokunulmaz), 409 kalıcı yapılandırma hatası (DenemeSayisi anında tüketilir, job bir
  daha otomatik denemez), diğer istisnalar geçici teknik hata (sınırlı otomatik yeniden deneme).
- Fiş tipi kararı: komisyonlu/komisyonsuz fark etmeksizin `MuhasebeFisTipleri.Mahsup` kullanıldı
  (projede "Virman" tipi yok, eklemek kapsam dışı bir denetim gerektirirdi).

## Değiştirilen / eklenen dosyalar

### Backend — yeni dosyalar
- `backend/Muhasebe/Common/Constants/ValorGunTurleri.cs`
- `backend/Muhasebe/Common/Services/ParaTutarYuvarlamaHelper.cs`
- `backend/Muhasebe/Common/Services/IResmiTatilGunuProvider.cs`
- `backend/Muhasebe/Common/Services/ValorTarihHesaplamaService.cs`
- `backend/Muhasebe/MuhasebeFisleri/Dtos/MuhasebeFisIptalSonucDto.cs`
- `backend/Muhasebe/PosTahsilatValorleri/**` (yeni modül: Entities, Dtos, Repositories, Services, Controllers, Mapping)
- Migrationlar: `20260722230838_AddPosTahsilatValorTakip`, `20260722231404_AddPosTahsilatValorMenuVeYetki`

### Backend — değiştirilen dosyalar
- `backend/Muhasebe/Common/Constants/MuhasebeKaynakModulleri.cs` — `PosTahsilatValorTransferi` eklendi
- `backend/Muhasebe/KasaBankaHesaplari/{Entities,Dtos,Services,Controllers}/*` — valör/komisyon alanları ve doğrulamaları
- `backend/Muhasebe/MuhasebeFisleri/Services/{IMuhasebeFisService,MuhasebeFisService}.cs` — `PosValorTransferFisiniIptalEtAsync`
- `backend/Muhasebe/TahsilatOdemeBelgeleri/Services/TahsilatOdemeBelgesiService.cs` — snapshot servis entegrasyonu
- `backend/Infrastructure/EntityFramework/StysAppDbContext.cs` — yeni DbSet'ler, index'ler, concurrency token
- `backend/Program.cs`, `backend/StructurePermissions.cs`

### Frontend
- `frontend/src/app/pages/muhasebe/kasa-banka-hesaplari/*` — checkbox, valör gün türü, komisyon alanları
- `frontend/src/app/pages/muhasebe/pos-tahsilat-valor/**` (yeni ekran: dto, service, component, template)
- `frontend/src/app.routes.ts`

### Testler
- `tests/STYS.Tests/{RezervasyonGelirTahakkukuTests,RezervasyonOdemeMuhasebeIntegrationTests}.cs` —
  yeni arayüz üyesi için sahte (fake) servis güncellemesi; mevcut testler kırılmadı.

## Endpoint listesi

`ui/muhasebe/pos-tahsilat-valor`:
`GET paged`, `GET {id}`, `GET ozet`, `POST toplu-onay-bilgisi`, `POST {id}/hesaba-aktar`,
`POST secili-hesaplara-aktar`, `POST valoru-gelenleri-hesaba-aktar`, `POST {id}/yeniden-dene`,
`POST {id}/duzeltme-ters-kayit`.

Ayrıca: `ui/muhasebe/kasa-banka-hesaplari/komisyon-gider-hesap-secimleri`.

## Ekran kullanım akışı

1. Muhasebe > Finansal Hesaplar ekranında bir Kredi Kartı/POS hesabı tanımlanırken artık **Bağlı
   Banka Hesabı zorunlu**; "Valör gününde otomatik olarak banka hesabına aktar" checkbox'ı, Valör
   Gün Türü, Komisyon Oranı ve Komisyon Gider Hesabı seçilebilir.
2. Kredi kartıyla yapılan her tahsilatta arka planda bir valör kaydı oluşur (nakit/banka tahsilatlarına dokunulmaz).
3. Muhasebe > POS Valör Takibi ekranında özet kartları, filtrelenebilir liste, tekil/seçili/tüm
   "valörü gelenler" toplu aktarım, hatalı kayıtları yeniden deneme ve (aktarılmış kayıtlar için)
   açıklama zorunlu "Düzeltme/Ters Kayıt" aksiyonu bulunur.
4. Otomatik aktarım açık hesaplarda arka plan job'u (15 dk aralıklı) valör günü gelen kayıtları
   otomatik işler; bu işlem **bankaya fiziksel para transferi yapmaz**, yalnızca STYS içi muhasebe
   kaydı oluşturur.

## Otomatik job davranışı

`PosValorAktarimHostedService`, her turda üç aday kümesini tarar: valör günü gelmiş bekleyen
kayıtlar, yeniden-deneme uygun hatalı kayıtlar (limit/backoff dahilinde), ve takılı kalmış
("Aktarılıyor" durumunda donmuş) kayıtlar. `MutabakatBekliyor` ve `TersKayitOlusturuluyor` durumları
job tarafından hiçbir zaman seçilmez.

## Çalıştırılan build/test komutları ve sonuçları

- `dotnet build` → **Build succeeded, 0 Warning(s), 0 Error(s)**
- `dotnet test tests/STYS.Tests/STYS.Tests.csproj` → **297 Passed, 0 Failed, 18 Skipped** (skip
  edilenler gerçek SQL Server gerektiren mevcut entegrasyon testleri; env var yokken zaten
  atlanıyorlar — regresyon yok)
- `dotnet ef migrations add` (x2) → başarıyla üretildi, migration içerikleri gözden geçirildi
- `npx ng build` (frontend) → **başarılı** (yalnızca ön var olan bundle-size bütçe uyarısı, hata değil)

## Kalan riskler / yapılmayanlar

- **Yeni özellik için otomatik test yazılmadı.** Kullanıcının istediği 20+ senaryo (mükerrer aktarım,
  eşzamanlı istekler, kısmi toplu hata, açık/kapalı dönem, iptal zincirleri vb.) için test
  eklenmedi. Mevcut test paketi kırılmadı ancak yeni claim/lock/idempotent akışlar
  doğrulanmadı — üretime almadan önce en azından kritik eşzamanlılık senaryoları gerçek SQL
  Server'a karşı test edilmelidir.
- **Migration öncesi veri kontrolü çalıştırılmadı**: `IptalEdilenFisId` üzerindeki yeni unique
  index'in mevcut prod verisiyle çakışıp çakışmayacağı kontrol edilmedi (aşağıdaki sorgu migration
  uygulanmadan önce mutlaka çalıştırılmalı):
  ```sql
  SELECT IptalEdilenFisId, COUNT(*) FROM Muhasebe.MuhasebeFisler
  WHERE IptalEdilenFisId IS NOT NULL GROUP BY IptalEdilenFisId HAVING COUNT(*) > 1;
  ```
- **Backfill script'i yazılmadı** (plana göre zaten kapsam dışı, kasıtlı) — geçmiş tahsilatlar için
  valör kaydı/fiş otomatik üretilmez.
- Stuck-eşiği/backoff/azami-deneme gibi sabitler appsettings yerine kod içi sabit olarak
  bırakıldı (basitleştirme, kolayca konfigüre edilebilir hale getirilebilir).
- **Manuel/tarayıcı doğrulaması yapılmadı** — yalnızca derleme ve otomatik test sonuçları
  doğrulandı, ekranlar canlı olarak denenmedi.
