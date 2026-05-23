# Muhasebe Modülü — BaseService / BaseRepository Uyum Denetim Raporu

**Faz:** 59  
**Tarih:** 2026-05-24  
**Kapsam:** `backend/Muhasebe` altındaki tüm servis, repository, DTO ve AutoMapper sınıfları  
**Amaç:** BaseRdbmsService / BaseRdbmsRepository / BaseRdbmsDto uyumluluğunu denetlemek, sınıflandırmak ve düşük riskli düzeltmeleri belirlemek

---

## 1. Platform Base Sınıfları (Referans)

| Dosya | Açıklama |
|---|---|
| [`platform/TOD.Platform.Persistence.Rdbms/Services/IBaseRdbmsService.cs`](../platform/TOD.Platform.Persistence.Rdbms/Services/IBaseRdbmsService.cs) | Generic CRUD servis arayüzü: `GetByIdAsync`, `GetAllAsync`, `GetPagedAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `WhereAsync`, `AnyAsync` |
| [`platform/TOD.Platform.Persistence.Rdbms/Services/BaseRdbmsService.cs`](../platform/TOD.Platform.Persistence.Rdbms/Services/BaseRdbmsService.cs) | Temel CRUD implementasyonu. `Delete` → `Repository.Delete(entity)` + `SaveChangesAsync`. `AddAsync` Guid anahtarlarda otomatik ID üretir. `UpdateAsync` `dto.Id.HasValue` kontrolü yapar, mevcut entity'yi yükler ve `IsDeleted = false` set eder. |
| [`platform/TOD.Platform.Persistence.Rdbms/Repositories/IBaseRdbmsRepository.cs`](../platform/TOD.Platform.Persistence.Rdbms/Repositories/IBaseRdbmsRepository.cs) | Generic repository arayüzü |
| [`platform/TOD.Platform.Persistence.Rdbms/Repositories/BaseRdbmsRepository.cs`](../platform/TOD.Platform.Persistence.Rdbms/Repositories/BaseRdbmsRepository.cs) | `Delete` → `DbSet.Remove(entity)` (hard delete). Soft delete DbContext SaveChanges interceptor'ı tarafından gerçekleştirilir. `UndoDelete` → `IsDeleted = false` |
| [`platform/TOD.Platform.Persistence.Rdbms/Dto/BaseRdbmsDto.cs`](../platform/TOD.Platform.Persistence.Rdbms/Dto/BaseRdbmsDto.cs) | `TKey? Id` soyut property |

---

## 2. Servis Sınıflandırması

### Sınıflandırma Anahtarı

| Kategori | Açıklama | Eylem |
|---|---|---|
| **A. CRUD/Tanım Servisi** | Sadece validation override'ları olan BaseRdbmsService türevleri | ✅ Uyumlu, değişiklik yok |
| **B. İş Kurallı CRUD** | BaseRdbmsService kullanan ama önemli iş mantığı override'ları (transaction, hesap oluşturma, bakiye, vb.) olan servisler | ✅ Uyumlu, değişiklik yok |
| **C. Rapor/Aggregate** | Doğrudan DbContext kullanan, rapor/aggregate/Excel çıktısı üreten özel servisler | ⛔ DOKUNMA — Base'e taşınamaz |
| **D. Batch/Rebuild/Sync** | Toplu işlem, rebuild, senkronizasyon amaçlı özel servisler | ⛔ DOKUNMA — Base'e taşınamaz |
| **E. Workflow/Domain Motoru** | İş akışı, domain mantığı, validasyon motoru gibi özel servisler | ⛔ DOKUNMA — Base'e taşınamaz |

---

### 2.1 A Kategorisi — CRUD/Tanım Servisleri (4 servis)

| # | Servis | Dosya | Base | Override'lar | Durum |
|---|---|---|---|---|---|
| 1 | `KdvIstisnaTanimService` | [`backend/Muhasebe/Kdv/Services/KdvIstisnaTanimService.cs`](../backend/Muhasebe/Kdv/Services/KdvIstisnaTanimService.cs) | `BaseRdbmsService<KdvIstisnaTanimDto, KdvIstisnaTanim, int>` | `AddAsync` (validasyon), `UpdateAsync` (validasyon + duplicate check), `FilterAsync` (özel filtre) | ✅ UYUMLU |
| 2 | `PaketTuruService` | [`backend/Muhasebe/PaketTurleri/Services/PaketTuruService.cs`](../backend/Muhasebe/PaketTurleri/Services/PaketTuruService.cs) | `BaseRdbmsService<PaketTuruDto, PaketTuru, int>` | `AddAsync` (isim validasyonu), `UpdateAsync` (ID check + validasyon) | ✅ UYUMLU |
| 3 | `BankaHareketService` | [`backend/Muhasebe/BankaHareketleri/Services/BankaHareketService.cs`](../backend/Muhasebe/BankaHareketleri/Services/BankaHareketService.cs) | `BaseRdbmsService<BankaHareketDto, BankaHareket, int>` | `AddAsync`/`UpdateAsync` (validasyon), `GetByIdAsync`/`GetAllAsync`/`WhereAsync`/`GetPagedAsync` (access scope) | ✅ UYUMLU |
| 4 | `KasaHareketService` | [`backend/Muhasebe/KasaHareketleri/Services/KasaHareketService.cs`](../backend/Muhasebe/KasaHareketleri/Services/KasaHareketService.cs) | `BaseRdbmsService<KasaHareketDto, KasaHareket, int>` | `AddAsync`/`UpdateAsync` (validasyon), `GetByIdAsync`/`GetAllAsync`/`WhereAsync`/`GetPagedAsync` (access scope) | ✅ UYUMLU |

---

### 2.2 B Kategorisi — İş Kurallı CRUD (16 servis)

| # | Servis | Dosya | Base | Öne Çıkan İş Kuralları | Durum |
|---|---|---|---|---|---|
| 5 | `DepoService` | [`backend/Muhasebe/Depolar/Services/DepoService.cs`](../backend/Muhasebe/Depolar/Services/DepoService.cs) | `BaseRdbmsService<DepoDto, Depo, int>` | Transaction + muhasebe hesabı oluşturma, child sync, access scope | ✅ UYUMLU |
| 6 | `CariKartService` | [`backend/Muhasebe/CariKartlar/Services/CariKartService.cs`](../backend/Muhasebe/CariKartlar/Services/CariKartService.cs) | `BaseRdbmsService<CariKartDto, CariKart, int>` | Transaction + muhasebe detay hesap oluşturma, bakiye, access scope | ✅ UYUMLU |
| 7 | `HesapService` | [`backend/Muhasebe/Hesaplar/Services/HesapService.cs`](../backend/Muhasebe/Hesaplar/Services/HesapService.cs) | `BaseRdbmsService<HesapDto, Hesap, int>` | Link yönetimi (depo/kasa-banka), lookup endpoint'leri, access scope | ✅ UYUMLU |
| 8 | `KasaBankaHesapService` | [`backend/Muhasebe/KasaBankaHesaplari/Services/KasaBankaHesapService.cs`](../backend/Muhasebe/KasaBankaHesaplari/Services/KasaBankaHesapService.cs) | `BaseRdbmsService<KasaBankaHesapDto, KasaBankaHesap, int>` | Transaction + muhasebe detay hesap, access scope, tip bazlı sorgular | ✅ UYUMLU |
| 9 | `StokHareketService` | [`backend/Muhasebe/StokHareketleri/Services/StokHareketService.cs`](../backend/Muhasebe/StokHareketleri/Services/StokHareketService.cs) | `BaseRdbmsService<StokHareketDto, StokHareket, int>` | KDV hesaplama, tutar hesaplama, bakiye, access scope (Depo üzerinden) | ✅ UYUMLU |
| 10 | `CariHareketService` | [`backend/Muhasebe/CariHareketler/Services/CariHareketService.cs`](../backend/Muhasebe/CariHareketler/Services/CariHareketService.cs) | `BaseRdbmsService<CariHareketDto, CariHareket, int>` | Validasyon, `GetEkstreAsync` (cari ekstre), access scope (CariKart üzerinden) | ✅ UYUMLU |
| 11 | `TahsilatOdemeBelgesiService` | [`backend/Muhasebe/TahsilatOdemeBelgeleri/Services/TahsilatOdemeBelgesiService.cs`](../backend/Muhasebe/TahsilatOdemeBelgeleri/Services/TahsilatOdemeBelgesiService.cs) | `BaseRdbmsService<TahsilatOdemeBelgesiDto, TahsilatOdemeBelgesi, int>` | Validasyon, `GetGunlukOzetAsync`, access scope (CariKart üzerinden) | ✅ UYUMLU |
| 12 | `MuhasebeDonemService` | [`backend/Muhasebe/MuhasebeDonemleri/Services/MuhasebeDonemService.cs`](../backend/Muhasebe/MuhasebeDonemleri/Services/MuhasebeDonemService.cs) | `BaseRdbmsService<MuhasebeDonemDto, MuhasebeDonem, int>` | `DonemKapatAsync`, `DonemAcAsync`, `GetAktifDonemAsync`, kapalı dönem lock | ✅ UYUMLU |
| 13 | `MuhasebeFisService` | [`backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs`](../backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs) | `BaseRdbmsService<MuhasebeFisDto, MuhasebeFis, int>` | **2367 satır.** `OnaylaAsync`, `IptalEtAsync`, `GetYevmiyeDefteriAsync`, `GetMuavinDefterAsync`, `GetMizanAsync`, `GetMizanBakiyeAsync`, `KarsilastirMizanAsync`, `TasinirMuhasebeFisiTaslagiOlusturAsync`, Excel export'lar, FisNo retry + UniqueConstraint handling | ⚠️ DOKUNMA |
| 14 | `SatisBelgesiService` | [`backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs`](../backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs) | `BaseRdbmsService<SatisBelgesiDto, SatisBelgesi, int>` | Faz 58A'da BaseRdbmsService'e taşındı. Access scope, iş kuralları, KDV fix. | ✅ UYUMLU |
| 15 | `MuhasebeHesapPlaniService` | [`backend/Muhasebe/MuhasebeHesapPlanlari/Services/MuhasebeHesapPlaniService.cs`](../backend/Muhasebe/MuhasebeHesapPlanlari/Services/MuhasebeHesapPlaniService.cs) | `BaseRdbmsService<MuhasebeHesapPlaniDto, MuhasebeHesapPlani, int>` | Distributed cache (Redis), tree operasyonları (`GetTreeAsync`, `GetTreeRootsAsync`, `GetTreeChildrenAsync`), cache invalidation | ✅ UYUMLU |
| 16 | `TasinirKartService` | [`backend/Muhasebe/TasinirKartlari/Services/TasinirKartService.cs`](../backend/Muhasebe/TasinirKartlari/Services/TasinirKartService.cs) | `BaseRdbmsService<TasinirKartDto, TasinirKart, int>` | Transaction + muhasebe detay hesap oluşturma, taşınır kod eşleme, access scope | ✅ UYUMLU |
| 17 | `TasinirKodService` | [`backend/Muhasebe/TasinirKodlari/Services/TasinirKodService.cs`](../backend/Muhasebe/TasinirKodlari/Services/TasinirKodService.cs) | `BaseRdbmsService<TasinirKodDto, TasinirKod, int>` | Import (grup güncelleme + pasifleştirme), tree operasyonları, distributed cache, lisans kontrolü | ✅ UYUMLU |
| 18 | `TasinirKodMuhasebeHesapEslemeService` | [`backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Services/TasinirKodMuhasebeHesapEslemeService.cs`](../backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Services/TasinirKodMuhasebeHesapEslemeService.cs) | `BaseRdbmsService<TasinirKodMuhasebeHesapEslemeDto, TasinirKodMuhasebeHesapEsleme, int>` | Validasyon, `GetVarsayilanAsync`, `GetByTasinirKodIdAsync`, IslemTuru ↔ HareketTipi uyumluluğu | ✅ UYUMLU |
| 19 | `MuhasebeVergiHesapEslemeService` | [`backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Services/MuhasebeVergiHesapEslemeService.cs`](../backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Services/MuhasebeVergiHesapEslemeService.cs) | `BaseRdbmsService<MuhasebeVergiHesapEslemeDto, MuhasebeVergiHesapEsleme, int>` | Validasyon (KDV hesap tipi kontrolü), `GetAktifEslemeAsync`, include override'ları | ✅ UYUMLU |
| 20 | `MuhasebeHesapBakiyeService` | [`backend/Muhasebe/MuhasebeHesapBakiyeleri/Services/MuhasebeHesapBakiyeService.cs`](../backend/Muhasebe/MuhasebeHesapBakiyeleri/Services/MuhasebeHesapBakiyeService.cs) | `BaseRdbmsService<MuhasebeHesapBakiyeDto, MuhasebeHesapBakiye, int>` | `RebuildAsync` (toplu bakiye yeniden hesaplama: transaction + soft delete + aggregate + AddRange), `GetFilteredAsync`, `GetByTesisYilDonemAsync` | ✅ UYUMLU |

---

### 2.3 C Kategorisi — Rapor/Aggregate (2 servis) ⛔ DOKUNMA

| # | Servis | Dosya | Bağımlılık | Metotlar | Durum |
|---|---|---|---|---|---|
| 21 | `KdvHareketRaporService` | [`backend/Muhasebe/Kdv/Services/KdvHareketRaporService.cs`](../backend/Muhasebe/Kdv/Services/KdvHareketRaporService.cs) | `StysAppDbContext` + `IUserAccessScopeService` | `GetRaporAsync` (stok hareket + muhasebe fiş join + aggregate, 1000 satır limit), `ExportExcelAsync` (ClosedXML, 50K limit) | ⛔ Base'e taşınamaz |
| 22 | `KdvOzetRaporService` | [`backend/Muhasebe/Kdv/Services/KdvOzetRaporService.cs`](../backend/Muhasebe/Kdv/Services/KdvOzetRaporService.cs) | `StysAppDbContext` | `GetOzetRaporAsync` (mali yıl/dönem çözümleme, stok sorgu, fiş join, özet hesaplama, uyarı tespiti), `ExportExcelAsync` | ⛔ Base'e taşınamaz |

---

### 2.4 C/E Kategorisi — Workflow/Rapor (2 servis) ⛔ DOKUNMA

| # | Servis | Dosya | Bağımlılık | Metotlar | Durum |
|---|---|---|---|---|---|
| 23 | `KdvBeyannameHazirlikKontrolService` | [`backend/Muhasebe/Kdv/Services/KdvBeyannameHazirlikKontrolService.cs`](../backend/Muhasebe/Kdv/Services/KdvBeyannameHazirlikKontrolService.cs) | `StysAppDbContext` | 10 kontrol metodu: `KDV_HAREKET_VAR_MI`, `MUHASEBE_FISI_EKSIK`, `KDV_TUTARI_EKSIK`, `ISTISNA_KODU_EKSIK`, `TEVKIFATLI_HAREKET_VAR`, `KDV_HESAP_UYUMU`, `FIS_DENGE_KONTROLU`, `TASLAK_FIS_VAR`, `KDV_OZET_TUTARLILIK`, `ISTISNA_AYRIMI_KONTROLU` | ⛔ Base'e taşınamaz |
| 24 | `DonemKapanisKontrolService` | [`backend/Muhasebe/DonemKapanis/Services/DonemKapanisKontrolService.cs`](../backend/Muhasebe/DonemKapanis/Services/DonemKapanisKontrolService.cs) | `StysAppDbContext` | `KontrolEtAsync` — 7 kontrol: Dönem varlığı, dönem kapalı mı, taslak fiş, dengesiz taslak, dengesiz onaylı, yevmiye no eksik, dönem toplam dengesizliği | ⛔ Base'e taşınamaz |

---

### 2.5 D Kategorisi — Batch/Rebuild/Sync (2 servis) ⛔ DOKUNMA

| # | Servis | Dosya | Bağımlılık | Metotlar | Durum |
|---|---|---|---|---|---|
| 25 | `MuhasebeDetayHesapService` | [`backend/Muhasebe/Common/Services/MuhasebeDetayHesapService.cs`](../backend/Muhasebe/Common/Services/MuhasebeDetayHesapService.cs) | `StysAppDbContext` | `CreateOrResolveDetayHesapAsync` — UPDLOCK + ROWLOCK + HOLDLOCK ile sayaç yönetimi, retry (max 5), transaction yönetimi | ⛔ Base'e taşınamaz |
| 26 | `MuhasebeHesapBakiyeGuncellemeService` | [`backend/Muhasebe/MuhasebeHesapBakiyeleri/Services/MuhasebeHesapBakiyeGuncellemeService.cs`](../backend/Muhasebe/MuhasebeHesapBakiyeleri/Services/MuhasebeHesapBakiyeGuncellemeService.cs) | `StysAppDbContext` | `FisBakiyeleriniIsleAsync` — Fiş onay/iptal akışında bakiye güncelleme, üst hesap konsolidasyonu, local entity tracking | ⛔ Base'e taşınamaz |

---

### 2.6 E Kategorisi — Domain Motoru (1 servis) ⛔ DOKUNMA

| # | Servis | Dosya | Bağımlılık | Metotlar | Durum |
|---|---|---|---|---|---|
| 27 | `KdvUygulamaService` | [`backend/Muhasebe/Kdv/Services/KdvUygulamaService.cs`](../backend/Muhasebe/Kdv/Services/KdvUygulamaService.cs) | `StysAppDbContext` | `ValidateAndSnapshotAsync` — KDV hesaplama + istisna validasyonu + geçerlilik kontrolü, snapshot mantığı | ⛔ Base'e taşınamaz |

---

### 2.7 C Kategorisi — Dashboard (1 servis) ⛔ DOKUNMA

| # | Servis | Dosya | Bağımlılık | Metotlar | Durum |
|---|---|---|---|---|---|
| 28 | `MuhasebeDashboardService` | [`backend/Muhasebe/Dashboard/Services/MuhasebeDashboardService.cs`](../backend/Muhasebe/Dashboard/Services/MuhasebeDashboardService.cs) | `StysAppDbContext` | `GetDashboardAsync` — Aggregate sorgular: dönem sayıları, fiş sayıları, dengesiz taslak, toplam borç/alacak, son 10 fiş, uyarılar | ⛔ Base'e taşınamaz |

---

## 3. Repository Uyum Denetimi

**Sonuç:** Tüm repository sınıfları [`BaseRdbmsRepository<TEntity, int>`](../platform/TOD.Platform.Persistence.Rdbms/Repositories/BaseRdbmsRepository.cs) kalıtımını doğru şekilde almaktadır.

| # | Repository | Entity | Dosya |
|---|---|---|---|
| 1 | `DepoRepository` | `Depo` | [`backend/Muhasebe/Depolar/Repositories/DepoRepository.cs`](../backend/Muhasebe/Depolar/Repositories/DepoRepository.cs) |
| 2 | `CariKartRepository` | `CariKart` | [`backend/Muhasebe/CariKartlar/Repositories/CariKartRepository.cs`](../backend/Muhasebe/CariKartlar/Repositories/CariKartRepository.cs) |
| 3 | `MuhasebeHesapKoduSayacRepository` | `MuhasebeHesapKoduSayac` | [`backend/Muhasebe/CariKartlar/Repositories/MuhasebeHesapKoduSayacRepository.cs`](../backend/Muhasebe/CariKartlar/Repositories/MuhasebeHesapKoduSayacRepository.cs) |
| 4 | `HesapRepository` | `Hesap` | [`backend/Muhasebe/Hesaplar/Repositories/HesapRepository.cs`](../backend/Muhasebe/Hesaplar/Repositories/HesapRepository.cs) |
| 5 | `KasaBankaHesapRepository` | `KasaBankaHesap` | [`backend/Muhasebe/KasaBankaHesaplari/Repositories/KasaBankaHesapRepository.cs`](../backend/Muhasebe/KasaBankaHesaplari/Repositories/KasaBankaHesapRepository.cs) |
| 6 | `BankaHareketRepository` | `BankaHareket` | [`backend/Muhasebe/BankaHareketleri/Repositories/BankaHareketRepository.cs`](../backend/Muhasebe/BankaHareketleri/Repositories/BankaHareketRepository.cs) |
| 7 | `KasaHareketRepository` | `KasaHareket` | [`backend/Muhasebe/KasaHareketleri/Repositories/KasaHareketRepository.cs`](../backend/Muhasebe/KasaHareketleri/Repositories/KasaHareketRepository.cs) |
| 8 | `CariHareketRepository` | `CariHareket` | [`backend/Muhasebe/CariHareketler/Repositories/CariHareketRepository.cs`](../backend/Muhasebe/CariHareketler/Repositories/CariHareketRepository.cs) |
| 9 | `StokHareketRepository` | `StokHareket` | [`backend/Muhasebe/StokHareketleri/Repositories/StokHareketRepository.cs`](../backend/Muhasebe/StokHareketleri/Repositories/StokHareketRepository.cs) |
| 10 | `TahsilatOdemeBelgesiRepository` | `TahsilatOdemeBelgesi` | [`backend/Muhasebe/TahsilatOdemeBelgeleri/Repositories/TahsilatOdemeBelgesiRepository.cs`](../backend/Muhasebe/TahsilatOdemeBelgeleri/Repositories/TahsilatOdemeBelgesiRepository.cs) |
| 11 | `MuhasebeDonemRepository` | `MuhasebeDonem` | [`backend/Muhasebe/MuhasebeDonemleri/Repositories/MuhasebeDonemRepository.cs`](../backend/Muhasebe/MuhasebeDonemleri/Repositories/MuhasebeDonemRepository.cs) |
| 12 | `MuhasebeFisRepository` | `MuhasebeFis` | [`backend/Muhasebe/MuhasebeFisleri/Repositories/MuhasebeFisRepository.cs`](../backend/Muhasebe/MuhasebeFisleri/Repositories/MuhasebeFisRepository.cs) |
| 13 | `MuhasebeHesapPlaniRepository` | `MuhasebeHesapPlani` | [`backend/Muhasebe/MuhasebeHesapPlanlari/Repositories/MuhasebeHesapPlaniRepository.cs`](../backend/Muhasebe/MuhasebeHesapPlanlari/Repositories/MuhasebeHesapPlaniRepository.cs) |
| 14 | `TasinirKartRepository` | `TasinirKart` | [`backend/Muhasebe/TasinirKartlari/Repositories/TasinirKartRepository.cs`](../backend/Muhasebe/TasinirKartlari/Repositories/TasinirKartRepository.cs) |
| 15 | `TasinirKodRepository` | `TasinirKod` | [`backend/Muhasebe/TasinirKodlari/Repositories/TasinirKodRepository.cs`](../backend/Muhasebe/TasinirKodlari/Repositories/TasinirKodRepository.cs) |
| 16 | `MuhasebeHesapBakiyeRepository` | `MuhasebeHesapBakiye` | [`backend/Muhasebe/MuhasebeHesapBakiyeleri/Repositories/MuhasebeHesapBakiyeRepository.cs`](../backend/Muhasebe/MuhasebeHesapBakiyeleri/Repositories/MuhasebeHesapBakiyeRepository.cs) |
| 17 | `MuhasebeVergiHesapEslemeRepository` | `MuhasebeVergiHesapEsleme` | [`backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Repositories/MuhasebeVergiHesapEslemeRepository.cs`](../backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Repositories/MuhasebeVergiHesapEslemeRepository.cs) |
| 18 | `TasinirKodMuhasebeHesapEslemeRepository` | `TasinirKodMuhasebeHesapEsleme` | [`backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Repositories/TasinirKodMuhasebeHesapEslemeRepository.cs`](../backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Repositories/TasinirKodMuhasebeHesapEslemeRepository.cs) |
| 19 | `SatisBelgesiRepository` | `SatisBelgesi` | [`backend/Muhasebe/SatisBelgeleri/Repositories/SatisBelgesiRepository.cs`](../backend/Muhasebe/SatisBelgeleri/Repositories/SatisBelgesiRepository.cs) |
| 20 | `PaketTuruRepository` | `PaketTuru` | [`backend/Muhasebe/PaketTurleri/Repositories/PaketTuruRepository.cs`](../backend/Muhasebe/PaketTurleri/Repositories/PaketTuruRepository.cs) |
| 21 | `KdvIstisnaTanimRepository` | `KdvIstisnaTanim` | [`backend/Muhasebe/Kdv/Repositories/KdvIstisnaTanimRepository.cs`](../backend/Muhasebe/Kdv/Repositories/KdvIstisnaTanimRepository.cs) |

**Repository olmayan modüller (bilinçli tasarım):**
- `MuhasebeHesapPlanlari` — yalnızca repository'si olan CRUD + tree işlemleri (servis `IMuhasebeHesapPlaniRepository` kullanıyor)
- `Dashboard`, `DonemKapanis`, `Kdv/*` rapor servisleri — doğrudan `StysAppDbContext` kullanır (rapor/aggregate pattern)
- `Common/MuhasebeDetayHesapService` — doğrudan `StysAppDbContext` kullanır (shared utility)

---

## 4. DTO Uyum Denetimi

**Sonuç:** Tüm birincil entity DTO'ları [`BaseRdbmsDto<int>`](../platform/TOD.Platform.Persistence.Rdbms/Dto/BaseRdbmsDto.cs) kalıtımını doğru şekilde almaktadır.

| # | DTO | Kalıtım | Dosya |
|---|---|---|---|
| 1 | `DepoDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/Depolar/Dtos/DepoDtos.cs`](../backend/Muhasebe/Depolar/Dtos/DepoDtos.cs) |
| 2 | `CariKartDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/CariKartlar/Dtos/CariKartDtos.cs`](../backend/Muhasebe/CariKartlar/Dtos/CariKartDtos.cs) |
| 3 | `HesapDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/Hesaplar/Dtos/HesapDtos.cs`](../backend/Muhasebe/Hesaplar/Dtos/HesapDtos.cs) |
| 4 | `KasaBankaHesapDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/KasaBankaHesaplari/Dtos/KasaBankaHesapDtos.cs`](../backend/Muhasebe/KasaBankaHesaplari/Dtos/KasaBankaHesapDtos.cs) |
| 5 | `BankaHareketDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/BankaHareketleri/Dtos/BankaHareketDtos.cs`](../backend/Muhasebe/BankaHareketleri/Dtos/BankaHareketDtos.cs) |
| 6 | `KasaHareketDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/KasaHareketleri/Dtos/KasaHareketDtos.cs`](../backend/Muhasebe/KasaHareketleri/Dtos/KasaHareketDtos.cs) |
| 7 | `CariHareketDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/CariHareketler/Dtos/CariHareketDtos.cs`](../backend/Muhasebe/CariHareketler/Dtos/CariHareketDtos.cs) |
| 8 | `StokHareketDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/StokHareketleri/Dtos/StokHareketDtos.cs`](../backend/Muhasebe/StokHareketleri/Dtos/StokHareketDtos.cs) |
| 9 | `TahsilatOdemeBelgesiDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/TahsilatOdemeBelgeleri/Dtos/TahsilatOdemeBelgesiDtos.cs`](../backend/Muhasebe/TahsilatOdemeBelgeleri/Dtos/TahsilatOdemeBelgesiDtos.cs) |
| 10 | `MuhasebeDonemDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/MuhasebeDonemleri/Dtos/MuhasebeDonemDtos.cs`](../backend/Muhasebe/MuhasebeDonemleri/Dtos/MuhasebeDonemDtos.cs) |
| 11 | `MuhasebeFisDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/MuhasebeFisleri/Dtos/MuhasebeFisDtos.cs`](../backend/Muhasebe/MuhasebeFisleri/Dtos/MuhasebeFisDtos.cs) |
| 12 | `MuhasebeHesapPlaniDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/MuhasebeHesapPlanlari/Dtos/MuhasebeHesapPlaniDtos.cs`](../backend/Muhasebe/MuhasebeHesapPlanlari/Dtos/MuhasebeHesapPlaniDtos.cs) |
| 13 | `TasinirKartDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/TasinirKartlari/Dtos/TasinirKartDtos.cs`](../backend/Muhasebe/TasinirKartlari/Dtos/TasinirKartDtos.cs) |
| 14 | `TasinirKodDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/TasinirKodlari/Dtos/TasinirKodDtos.cs`](../backend/Muhasebe/TasinirKodlari/Dtos/TasinirKodDtos.cs) |
| 15 | `MuhasebeHesapBakiyeDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/MuhasebeHesapBakiyeleri/Dtos/MuhasebeHesapBakiyeDtos.cs`](../backend/Muhasebe/MuhasebeHesapBakiyeleri/Dtos/MuhasebeHesapBakiyeDtos.cs) |
| 16 | `MuhasebeVergiHesapEslemeDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Dtos/MuhasebeVergiHesapEslemeDtos.cs`](../backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Dtos/MuhasebeVergiHesapEslemeDtos.cs) |
| 17 | `TasinirKodMuhasebeHesapEslemeDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Dtos/TasinirKodMuhasebeHesapEslemeDtos.cs`](../backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Dtos/TasinirKodMuhasebeHesapEslemeDtos.cs) |
| 18 | `SatisBelgesiDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/SatisBelgeleri/Dtos/SatisBelgesiDtos.cs`](../backend/Muhasebe/SatisBelgeleri/Dtos/SatisBelgesiDtos.cs) |
| 19 | `PaketTuruDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/PaketTurleri/Dtos/PaketTuruDtos.cs`](../backend/Muhasebe/PaketTurleri/Dtos/PaketTuruDtos.cs) |
| 20 | `KdvIstisnaTanimDto` | `BaseRdbmsDto<int>` | [`backend/Muhasebe/Kdv/Dtos/KdvIstisnaTanimDtos.cs`](../backend/Muhasebe/Kdv/Dtos/KdvIstisnaTanimDtos.cs) |

**BaseRdbmsDto<int> KALITIMI ALMAYAN DTO'lar (bilinçli tasarım):**
- Rapor DTO'ları (`KdvHareketRaporDto`, `KdvOzetRaporDto`, vb.) — entity değil, view model
- Filter DTO'ları (`MuhasebeFisFilterDto`, `MizanFilterDto`, vb.) — sorgu parametreleri
- Sonuç DTO'ları (`TasinirKodImportSonucDto`, `MuhasebeHesapBakiyeRebuildResultDto`, vb.) — işlem sonuçları
- Lookup/Ozet DTO'ları (`CariBakiyeDto`, `StokBakiyeDto`, `HesapLookupDto`, vb.) — yardımcı veri transfer nesneleri

---

## 5. AutoMapper Profil Denetimi

**Sonuç:** Tüm AutoMapper profilleri `AutoMapper.Profile` kalıtımını doğru şekilde almaktadır. Her modül kendi Entity ↔ DTO eşlemelerini içerir.

| # | Profil | Dosya |
|---|---|---|
| 1 | `DepoProfile` | [`backend/Muhasebe/Depolar/Mapping/DepoProfile.cs`](../backend/Muhasebe/Depolar/Mapping/DepoProfile.cs) |
| 2 | `CariKartProfile` | [`backend/Muhasebe/CariKartlar/Mapping/CariKartProfile.cs`](../backend/Muhasebe/CariKartlar/Mapping/CariKartProfile.cs) |
| 3 | `HesapProfile` | [`backend/Muhasebe/Hesaplar/Mapping/HesapProfile.cs`](../backend/Muhasebe/Hesaplar/Mapping/HesapProfile.cs) |
| 4 | `KasaBankaHesapProfile` | [`backend/Muhasebe/KasaBankaHesaplari/Mapping/KasaBankaHesapProfile.cs`](../backend/Muhasebe/KasaBankaHesaplari/Mapping/KasaBankaHesapProfile.cs) |
| 5 | `BankaHareketProfile` | [`backend/Muhasebe/BankaHareketleri/Mapping/BankaHareketProfile.cs`](../backend/Muhasebe/BankaHareketleri/Mapping/BankaHareketProfile.cs) |
| 6 | `KasaHareketProfile` | [`backend/Muhasebe/KasaHareketleri/Mapping/KasaHareketProfile.cs`](../backend/Muhasebe/KasaHareketleri/Mapping/KasaHareketProfile.cs) |
| 7 | `CariHareketProfile` | [`backend/Muhasebe/CariHareketler/Mapping/CariHareketProfile.cs`](../backend/Muhasebe/CariHareketler/Mapping/CariHareketProfile.cs) |
| 8 | `StokHareketProfile` | [`backend/Muhasebe/StokHareketleri/Mapping/StokHareketProfile.cs`](../backend/Muhasebe/StokHareketleri/Mapping/StokHareketProfile.cs) |
| 9 | `TahsilatOdemeBelgesiProfile` | [`backend/Muhasebe/TahsilatOdemeBelgeleri/Mapping/TahsilatOdemeBelgesiProfile.cs`](../backend/Muhasebe/TahsilatOdemeBelgeleri/Mapping/TahsilatOdemeBelgesiProfile.cs) |
| 10 | `MuhasebeDonemProfile` | [`backend/Muhasebe/MuhasebeDonemleri/Mapping/MuhasebeDonemProfile.cs`](../backend/Muhasebe/MuhasebeDonemleri/Mapping/MuhasebeDonemProfile.cs) |
| 11 | `MuhasebeFisProfile` | [`backend/Muhasebe/MuhasebeFisleri/Mapping/MuhasebeFisProfile.cs`](../backend/Muhasebe/MuhasebeFisleri/Mapping/MuhasebeFisProfile.cs) |
| 12 | `MuhasebeHesapPlaniProfile` | [`backend/Muhasebe/MuhasebeHesapPlanlari/Mapping/MuhasebeHesapPlaniProfile.cs`](../backend/Muhasebe/MuhasebeHesapPlanlari/Mapping/MuhasebeHesapPlaniProfile.cs) |
| 13 | `TasinirKartProfile` | [`backend/Muhasebe/TasinirKartlari/Mapping/TasinirKartProfile.cs`](../backend/Muhasebe/TasinirKartlari/Mapping/TasinirKartProfile.cs) |
| 14 | `TasinirKodProfile` | [`backend/Muhasebe/TasinirKodlari/Mapping/TasinirKodProfile.cs`](../backend/Muhasebe/TasinirKodlari/Mapping/TasinirKodProfile.cs) |
| 15 | `MuhasebeHesapBakiyeProfile` | [`backend/Muhasebe/MuhasebeHesapBakiyeleri/Mapping/MuhasebeHesapBakiyeProfile.cs`](../backend/Muhasebe/MuhasebeHesapBakiyeleri/Mapping/MuhasebeHesapBakiyeProfile.cs) |
| 16 | `MuhasebeVergiHesapEslemeProfile` | [`backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Mapping/MuhasebeVergiHesapEslemeProfile.cs`](../backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Mapping/MuhasebeVergiHesapEslemeProfile.cs) |
| 17 | `TasinirKodMuhasebeHesapEslemeProfile` | [`backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Mapping/TasinirKodMuhasebeHesapEslemeProfile.cs`](../backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Mapping/TasinirKodMuhasebeHesapEslemeProfile.cs) |
| 18 | `SatisBelgesiProfile` | [`backend/Muhasebe/SatisBelgeleri/Mapping/SatisBelgesiProfile.cs`](../backend/Muhasebe/SatisBelgeleri/Mapping/SatisBelgesiProfile.cs) |
| 19 | `PaketTuruProfile` | [`backend/Muhasebe/PaketTurleri/Mapping/PaketTuruProfile.cs`](../backend/Muhasebe/PaketTurleri/Mapping/PaketTuruProfile.cs) |
| 20 | `KdvIstisnaTanimProfile` | [`backend/Muhasebe/Kdv/Mapping/KdvIstisnaTanimProfile.cs`](../backend/Muhasebe/Kdv/Mapping/KdvIstisnaTanimProfile.cs) |

---

## 6. Delete / Soft-Delete Davranış Denetimi

| Katman | Delete Davranışı | Soft-Delete Mekanizması |
|---|---|---|
| [`BaseRdbmsRepository.Delete()`](../platform/TOD.Platform.Persistence.Rdbms/Repositories/BaseRdbmsRepository.cs) | `DbSet.Remove(entity)` — hard delete çağrısı | — |
| [`StysAppDbContext.SaveChangesAsync()`](../backend/Infrastructure/EntityFramework/StysAppDbContext.cs) | SaveChanges interceptor'ı `EntityState.Deleted` olan entity'leri yakalar, `IsDeleted = true` + `DeletedAt = now` set eder, state'i `Modified` yapar | ✅ Soft delete burada gerçekleşir |
| [`BaseRdbmsRepository.UndoDelete()`](../platform/TOD.Platform.Persistence.Rdbms/Repositories/BaseRdbmsRepository.cs) | `IsDeleted = false` set eder | ✅ Geri alma |
| `BaseRdbmsService.DeleteAsync()` | `Repository.Delete(entity)` → `SaveChangesAsync()` | ✅ Tutarlı |

**Sonuç:** Tüm servisler aynı soft-delete mekanizmasını kullanmaktadır (DbContext interceptor tabanlı). Hiçbir servis manuel `IsDeleted = true` yapmamaktadır. Bu tutarlı ve doğru bir yaklaşımdır.

---

## 7. Düşük Riskli Düzeltmeler

**Düzeltme gerektiren bir bulgu yoktur.** Muhasebe modülü, BaseRdbmsService/BaseRdbmsRepository/BaseRdbmsDto pattern'lerine eksiksiz uyum göstermektedir:

- ✅ 20 CRUD servisi → hepsi `BaseRdbmsService<TDto, TEntity, int>` türevi
- ✅ 21 repository → hepsi `BaseRdbmsRepository<TEntity, int>` türevi
- ✅ 20 birincil DTO → hepsi `BaseRdbmsDto<int>` türevi
- ✅ 20 AutoMapper profili → hepsi `Profile` türevi
- ✅ Delete/soft-delete mekanizması → DbContext interceptor ile tutarlı
- ✅ 8 özel servis (rapor/batch/workflow) → bilinçli olarak Base dışında, doğru tasarım

---

## 8. Özet İstatistikler

| Metrik | Değer |
|---|---|
| Toplam servis sayısı | 28 |
| BaseRdbmsService kullanan | 20 (%71) |
| Özel servis (Base dışı) | 8 (%29) |
| Toplam repository sayısı | 21 |
| BaseRdbmsRepository kullanan | 21 (%100) |
| BaseRdbmsDto<int> kullanan DTO | 20 (birincil entity DTO'larının tamamı) |
| AutoMapper profili | 20 |
| Düzeltme gerektiren bulgu | **0** |

---

## 9. Sonuç

Muhasebe modülü, platform BaseRdbms pattern'lerine **tam uyumlu** durumdadır. Tüm CRUD servisleri [`BaseRdbmsService`](../platform/TOD.Platform.Persistence.Rdbms/Services/BaseRdbmsService.cs) üzerine inşa edilmiş, tüm repository'ler [`BaseRdbmsRepository`](../platform/TOD.Platform.Persistence.Rdbms/Repositories/BaseRdbmsRepository.cs) kalıtımı almış, tüm entity DTO'ları [`BaseRdbmsDto<int>`](../platform/TOD.Platform.Persistence.Rdbms/Dto/BaseRdbmsDto.cs) türevidir. Rapor, batch, workflow ve domain motoru servisleri bilinçli olarak Base dışında bırakılmıştır — bu doğru bir mimari karardır.

**Bu fazda herhangi bir kod değişikliği yapılmamıştır.** Denetim sonucunda düzeltme gerektiren bir uyumsuzluk tespit edilmemiştir.
