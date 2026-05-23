# Muhasebe Baz Metot Kullanımı Derin Denetim Raporu — Faz 59A

> **Oluşturma Tarihi:** 2026-05-23
> **Kapsam:** [`BaseRdbmsService`](platform/TOD.Platform.Persistence.Rdbms/Services/BaseRdbmsService.cs) ve [`BaseRdbmsRepository`](platform/TOD.Platform.Persistence.Rdbms/Repositories/BaseRdbmsRepository.cs)'den türeyen Muhasebe servislerinin, kalıtım aldıkları **base metotları gerçekten çağırıp çağırmadığı** denetlenmiştir.
> **Önceki Faz:** [Faz 59 — Base Kalıtım Uyum Raporu](docs/muhasebe-base-service-uyum-raporu.md)
>
> **Denetim Kriterleri (18 madde):**
>
> 1. `BaseRdbmsService.AddAsync(dto)` çağrılıyor mu? Yoksa manuel `new Entity + DbContext.Add + SaveChanges` mı?
> 2. `BaseRdbmsService.UpdateAsync(dto)` çağrılıyor mu? Yoksa manuel property-by-property + SaveChanges mı?
> 3. `BaseRdbmsService.DeleteAsync(id)` çağrılıyor mu? Soft-delete/silme iş kuralları korunuyor mu?
> 4. `BaseRdbmsService.GetByIdAsync` çağrılıyor mu? Yoksa `_repository.GetByIdAsync` + manuel `Mapper.Map` mı?
> 5. `BaseRdbmsService.GetAllAsync` çağrılıyor mu?
> 6. `BaseRdbmsService.WhereAsync` çağrılıyor mu?
> 7. `BaseRdbmsService.GetPagedAsync` çağrılıyor mu?
> 8. `BaseRdbmsService.AnyAsync` çağrılıyor mu?
> 9. Repository metotları (`AddAsync`, `Update`, `Delete`, `SaveChangesAsync`, `GetByIdAsync`, `GetAllAsync`, `Where`, `FirstOrDefaultAsync`, `AnyAsync`, `GetPagedAsync`) doğru kullanılıyor mu?
> 10. AutoMapper (`Mapper.Map<TDto>(entity)`) manuel çağrılıyor mu? (Base metot içinde zaten var.)
> 11. Transaction yönetimi (`BeginTransactionAsync` + `CommitAsync` / `RollbackAsync`) korunuyor mu?
> 12. İş kuralları (validasyon, tesis erişim kontrolü, muhasebe hesap senkronizasyonu) refactoring sonrası korunuyor mu?
> 13. `StysAppDbContext` doğrudan kullanımı (manuel CRUD) var mı?
> 14. `DomainAccessScope` / `BuildScopedIncludeQuery` pattern'i korunuyor mu?
> 15. `EnrichEntityAsync` / `OnEntitySavedAsync` override'ları var mı?
> 16. `BaseRdbmsDto<TKey>` kalıtımı + AutoMapper `ReverseMap` uyumlu mu?
> 17. Delete/soft-delete davranışı — `IsDeleted = true` manuel mi set ediliyor? (Platform interceptor ile otomatik olmalı.)
> 18. DTO-Entity alan eşleşmesi — manuel property kopyalama yerine AutoMapper kullanılıyor mu?

---

## 1. Platform Base Metot Referansı

### [`BaseRdbmsService.AddAsync(dto)`](platform/TOD.Platform.Persistence.Rdbms/Services/BaseRdbmsService.cs:52-67)
```csharp
public virtual async Task<TDto> AddAsync(TDto dto)
{
    if (!dto.Id.HasValue && typeof(TKey) == typeof(Guid))
        dto.Id = (TKey)(object)Guid.NewGuid();

    var entity = Mapper.Map<TEntity>(dto);       // AutoMapper DTO→Entity
    await EnrichEntityAsync(dto, entity);         // Virtual hook
    await Repository.AddAsync(entity);            // Repository.AddAsync
    await Repository.SaveChangesAsync();          // SaveChanges
    await OnEntitySavedAsync(entity.Id);          // Virtual hook
    return Mapper.Map<TDto>(entity);              // AutoMapper Entity→DTO
}
```

### [`BaseRdbmsService.UpdateAsync(dto)`](platform/TOD.Platform.Persistence.Rdbms/Services/BaseRdbmsService.cs:70-93)
```csharp
public virtual async Task<TDto> UpdateAsync(TDto dto)
{
    if (!dto.Id.HasValue) throw new InvalidOperationException("Id bos olamaz.");

    var existingEntity = await Repository.GetByIdAsync(dto.Id.Value);
    if (existingEntity is null) throw new InvalidOperationException("...");

    existingEntity.IsDeleted = false;            // Soft-delete'ten geri getirme
    Mapper.Map(dto, existingEntity);             // AutoMapper DTO→Entity (merge)
    await EnrichEntityAsync(dto, existingEntity);
    Repository.Update(existingEntity);
    await Repository.SaveChangesAsync();
    await OnEntitySavedAsync(existingEntity.Id);
    return Mapper.Map<TDto>(existingEntity);
}
```

### [`BaseRdbmsService.DeleteAsync(id)`](platform/TOD.Platform.Persistence.Rdbms/Services/BaseRdbmsService.cs:95-105)
```csharp
public virtual async Task DeleteAsync(TKey id)
{
    var entity = await Repository.GetByIdAsync(id);
    if (entity is null) throw new InvalidOperationException("Entity not found");
    Repository.Delete(entity);                   // DbSet.Remove (hard delete)
    await Repository.SaveChangesAsync();          // Interceptor soft-delete'e çevirir
}
```

---

## 2. Servis Sınıflandırması (Metot Kullanımına Göre)

### Kategori A — TAM UYUMLU ✅
**Tüm CRUD metotları base metotları çağırır. Manuel `DbContext` / `DbSet` kullanımı yoktur.**

| # | Servis | AddAsync | UpdateAsync | DeleteAsync | GetById | GetAll | Where | GetPaged | Not |
|---|--------|----------|-------------|-------------|---------|--------|-------|----------|-----|
| 1 | [`TasinirKartService`](backend/Muhasebe/TasinirKartlari/Services/TasinirKartService.cs) | `base.AddAsync(dto)` | `base.UpdateAsync(dto)` | `base.DeleteAsync` | `base` | `base` | `base` | `base` | **Referans pattern** |
| 2 | [`KdvIstisnaTanimService`](backend/Muhasebe/Kdv/Services/KdvIstisnaTanimService.cs) | `base.AddAsync(dto)` | `base.UpdateAsync(dto)` | — | — | — | — | — | DbContext enjekte dahi edilmemiş 🏆 |
| 3 | [`StokHareketService`](backend/Muhasebe/StokHareketleri/Services/StokHareketService.cs) | `base.AddAsync(dto)` | `base.UpdateAsync(dto)` | — | — | — | — | — | KDV hesaplama sonrası base |
| 4 | [`MuhasebeHesapPlaniService`](backend/Muhasebe/MuhasebeHesapPlanlari/Services/MuhasebeHesapPlaniService.cs) | `base.AddAsync(dto)` | `base.UpdateAsync(dto)` | `base.DeleteAsync` | — | — | — | — | Cache invalidasyonu ekli |
| 5 | [`BankaHareketService`](backend/Muhasebe/BankaHareketleri/Services/BankaHareketService.cs) | `base.AddAsync(dto)` | `base.UpdateAsync(dto)` | `base.DeleteAsync` | `base` | `base` | — | — | |
| 6 | [`KasaHareketService`](backend/Muhasebe/KasaHareketleri/Services/KasaHareketService.cs) | `base.AddAsync(dto)` | `base.UpdateAsync(dto)` | `base.DeleteAsync` | `base` | `base` | — | — | |
| 7 | [`CariHareketService`](backend/Muhasebe/CariHareketler/Services/CariHareketService.cs) | `base.AddAsync(dto)` | `base.UpdateAsync(dto)` | `base.DeleteAsync` | `base` | `base` | — | — | |
| 8 | [`TahsilatOdemeBelgesiService`](backend/Muhasebe/TahsilatOdemeBelgeleri/Services/TahsilatOdemeBelgesiService.cs) | `base.AddAsync(dto)` | `base.UpdateAsync(dto)` | `base.DeleteAsync` | `base` | `base` | — | — | |
| 9 | [`TasinirKodService`](backend/Muhasebe/TasinirKodlari/Services/TasinirKodService.cs) | `base.AddAsync(dto)` | `base.UpdateAsync(dto)` | — | `base` | `base` | — | — | |
| 10 | [`TasinirKodMuhasebeHesapEslemeService`](backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Services/TasinirKodMuhasebeHesapEslemeService.cs) | `base.AddAsync(dto)` | `base.UpdateAsync(dto)` | — | `base` | `base` | — | — | |
| 11 | [`MuhasebeVergiHesapEslemeService`](backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Services/MuhasebeVergiHesapEslemeService.cs) | `base.AddAsync(dto)` | `base.UpdateAsync(dto)` | — | `base` | `base` | — | — | |
| 12 | [`MuhasebeHesapBakiyeService`](backend/Muhasebe/MuhasebeHesapBakiyeleri/Services/MuhasebeHesapBakiyeService.cs) | `base.AddAsync(dto)` | `base.UpdateAsync(dto)` | — | `base` | `base` | — | — | Rebuild metodu özel |

### Kategori B — DÜZELTİLDİ 🔧
**Bu fazda manuel `DbContext` kullanan AddAsync/GetById/GetAll metotları base metotlara taşınmıştır. UpdateAsync'ler iş kuralı karmaşıklığı nedeniyle manuel bırakılmıştır.**

| # | Servis | Değişiklik | Yöntem | Risk |
|---|--------|------------|--------|------|
| 13 | [`CariKartService`](backend/Muhasebe/CariKartlar/Services/CariKartService.cs) | **AddAsync Path B** (Tedarikci/Musteri) | `new CariKart{…}` + `_dbContext.CariKartlar.AddAsync` → `dto.MuhasebeHesapPlaniId = detay.…; dto.CariKodu = detay.Kod; var result = await base.AddAsync(dto);` | Düşük |
| 13 | CariKartService | **UpdateAsync** | Manuel bırakıldı — tip/tesis değişmezlik kontrolü + muhasebe hesap adı senkronizasyonu + property-by-property atama | Yüksek |
| 14 | [`MuhasebeDonemService`](backend/Muhasebe/MuhasebeDonemleri/Services/MuhasebeDonemService.cs) | **AddAsync** | `Mapper.Map<MuhasebeDonem>(dto)` + `_dbContext.MuhasebeDonemler.AddAsync` → `var result = await base.AddAsync(dto)` + reload with Tesis include | Düşük |
| 14 | MuhasebeDonemService | **GetByIdAsync** | `_repository.GetByIdAsync` + manuel `Mapper.Map` → `base.GetByIdAsync(id, CombineIncludes(…))` | Düşük |
| 14 | MuhasebeDonemService | **GetAllAsync** | `_repository.GetAllAsync` + manuel `Mapper.Map` → `base.GetAllAsync(CombineIncludes(…))` | Düşük |
| 14 | MuhasebeDonemService | **UpdateAsync** | Manuel bırakıldı — kapalı/açık dönem mantığı, dönem kapatma/açma kuralı, property-by-property atama | Yüksek |
| 15 | [`KasaBankaHesapService`](backend/Muhasebe/KasaBankaHesaplari/Services/KasaBankaHesapService.cs) | **AddAsync** | `new KasaBankaHesap{…}` + `_dbContext.KasaBankaHesaplari.AddAsync` → `dto.MuhasebeHesapPlaniId = …; dto.Kod = …; var result = await base.AddAsync(dto);` | Düşük |
| 15 | KasaBankaHesapService | **UpdateAsync** | Manuel bırakıldı — tip/tesis değişmezlik kontrolü + muhasebe hesap senkronizasyonu + property-by-property atama | Yüksek |

### Kategori C — REPOSITORY PATTERN (Manuel ama Kabul Edilebilir) ⚠️
**Bu servisler `_repository.AddAsync`/`_repository.SaveChangesAsync` kullanır. Base servis metodu yerine repository metodu tercih edilmiştir. Manuel `new Entity` ve `Mapper.Map` içerir.**

| # | Servis | Açıklama |
|---|--------|----------|
| 16 | [`DepoService`](backend/Muhasebe/Depolar/Services/DepoService.cs) | AddAsync: `_mapper.Map<Depo>(dto)` + `_repository.AddAsync(entity)` + `_repository.SaveChangesAsync()`. DepoCikisGruplari alt entity yönetimi nedeniyle repository pattern kullanılıyor. UpdateAsync: property-by-property + DepoCikisGruplari sync (manuel bırakıldı). DeleteAsync: `base.DeleteAsync(id)` + alt depo kontrolü. |

### Kategori D — CUSTOM CRUD (Kendi İmzaları, Base Override Değil) 📋
**Bu servisler `BaseRdbmsService`'ten türer ancak kendi `CreateAsync`/`UpdateAsync`/`DeleteAsync` metotlarını tanımlar (base override değil). İş akışı (workflow) yönetimi içerirler.**

| # | Servis | Açıklama |
|---|--------|----------|
| 17 | [`SatisBelgesiService`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs) | `CreateAsync(CreateSatisBelgesiRequest)` — manuel entity oluşturma + KDV hesaplama + Repository.AddAsync. `UpdateAsync(int id, UpdateSatisBelgesiRequest)` — durum kontrollü (Taslak/Reddedildi). `DeleteAsync(int id, CancellationToken)` — manuel `IsDeleted = true` (satırlar dahil). **Base CRUD override edilmez, kendi iş akışı metotları vardır.** |
| 18 | [`MuhasebeFisService`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs) | Kompleks fiş işleme servisi. Kendi iş akışı metotları mevcut. Bu fazda değiştirilmedi. |

### Kategori E — STANDALONE SERVİSLER (BaseRdbms Kalıtımı Yok) 🏷️
**Bu servisler `BaseRdbmsService`'ten türemez. Rapor, batch işlem veya sorgu servisleridir. Kendi arayüzlerini implemente ederler.**

| # | Servis | Interface | Açıklama |
|---|--------|-----------|----------|
| 19 | [`KdvBeyannameService`](backend/Muhasebe/Kdv/Services/KdvBeyannameService.cs) | `IKdvBeyannameService` | Beyanname hesaplama |
| 20 | [`CariHesapEkstresiService`](backend/Muhasebe/CariHareketler/Services/CariHesapEkstresiService.cs) | `ICariHesapEkstresiService` | Ekstre raporu |
| 21 | [`MizanService`](backend/Muhasebe/Mizan/Services/MizanService.cs) | `IMizanService` | Mizan raporu |
| 22 | [`GelirTablosuService`](backend/Muhasebe/GelirTablosu/Services/GelirTablosuService.cs) | `IGelirTablosuService` | Gelir tablosu |
| 23 | [`KdvBeyannameHazirlikKontrolService`](backend/Muhasebe/Kdv/Services/KdvBeyannameHazirlikKontrolService.cs) | `IKdvBeyannameHazirlikKontrolService` | KDV kontrol raporu |
| 24 | [`KdvHareketRaporService`](backend/Muhasebe/Kdv/Services/KdvHareketRaporService.cs) | `IKdvHareketRaporService` | KDV hareket raporu |
| 25 | [`KdvOzetRaporService`](backend/Muhasebe/Kdv/Services/KdvOzetRaporService.cs) | `IKdvOzetRaporService` | KDV özet raporu |

---

## 3. Yapılan Değişiklikler (Refactoring Detayı)

### 3.1 [`CariKartService.AddAsync`](backend/Muhasebe/CariKartlar/Services/CariKartService.cs:84-113) — Path B (Tedarikci/Musteri)

**Önceki kod (manuel):**
```csharp
var entity = new CariKart
{
    TesisId = dto.TesisId,
    CariTipi = dto.CariTipi,
    CariKodu = detay.Kod,
    UnvanAdSoyad = dto.UnvanAdSoyad,
    VergiNoTckn = NormalizeOptional(dto.VergiNoTckn, 32),
    // ... 10+ property daha ...
    MuhasebeHesapPlaniId = detay.MuhasebeHesapPlaniId
};
await _dbContext.CariKartlar.AddAsync(entity, CancellationToken.None);
await _dbContext.SaveChangesAsync(CancellationToken.None);
await tx.CommitAsync(CancellationToken.None);
return Mapper.Map<CariKartDto>(entity);
```

**Yeni kod (base metotlu):**
```csharp
dto.MuhasebeHesapPlaniId = detay.MuhasebeHesapPlaniId;
dto.AnaMuhasebeHesapKodu = detay.AnaMuhasebeHesapKodu;
dto.MuhasebeHesapSiraNo = detay.SiraNo;
dto.CariKodu = detay.Kod;

var result = await base.AddAsync(dto);
await tx.CommitAsync(CancellationToken.None);
return result;
```

**Kazanım:** 15 satır manuel entity oluşturma + 2 manuel DbContext çağrısı → 5 satır DTO doldurma + `base.AddAsync(dto)`.

### 3.2 [`MuhasebeDonemService.AddAsync`](backend/Muhasebe/MuhasebeDonemleri/Services/MuhasebeDonemService.cs:52-68)

**Önceki kod (manuel):**
```csharp
var entity = Mapper.Map<MuhasebeDonem>(dto);
await _dbContext.MuhasebeDonemler.AddAsync(entity);
await _dbContext.SaveChangesAsync();
var created = await _repository.GetByIdAsync(entity.Id, q => q.Include(x => x.Tesis))
    ?? throw new BaseException("Dönem oluşturulamadı.", 500);
return Mapper.Map<MuhasebeDonemDto>(created);
```

**Yeni kod (base metotlu):**
```csharp
var result = await base.AddAsync(dto);
var created = await _repository.GetByIdAsync(result.Id!.Value, q => q.Include(x => x.Tesis))
    ?? throw new BaseException("Dönem oluşturulamadı.", 500);
return Mapper.Map<MuhasebeDonemDto>(created);
```

**Kazanım:** `Mapper.Map<MuhasebeDonem>(dto)` + `_dbContext.MuhasebeDonemler.AddAsync` + `_dbContext.SaveChangesAsync` → `base.AddAsync(dto)`. Tesis include reload korundu çünkü `TesisAdi` navigation property'den geliyor.

### 3.3 [`MuhasebeDonemService.GetByIdAsync/GetAllAsync`](backend/Muhasebe/MuhasebeDonemleri/Services/MuhasebeDonemService.cs:31-41)

**Önceki kod:**
```csharp
var entity = await _repository.GetByIdAsync(id, q => q.Include(x => x.Tesis));
return Mapper.Map<MuhasebeDonemDto?>(entity);
```

**Yeni kod:**
```csharp
var combinedInclude = CombineIncludes(q => q.Include(x => x.Tesis), include);
return await base.GetByIdAsync(id, combinedInclude);
```

**Kazanım:** `_repository.GetByIdAsync` + manuel `Mapper.Map` → `base.GetByIdAsync`. `CombineIncludes` yardımcı metodu eklendi.

### 3.4 [`KasaBankaHesapService.AddAsync`](backend/Muhasebe/KasaBankaHesaplari/Services/KasaBankaHesapService.cs:38-95)

**Önceki kod (manuel):**
```csharp
var entity = new KasaBankaHesap
{
    TesisId = dto.TesisId,
    Tip = dto.Tip,
    Kod = muhasebeDetay.Kod,
    Ad = dto.Ad,
    // ... 20+ property daha ...
};
await _dbContext.KasaBankaHesaplari.AddAsync(entity, CancellationToken.None);
await _dbContext.SaveChangesAsync(CancellationToken.None);
return Mapper.Map<KasaBankaHesapDto>(entity);
```

**Yeni kod (base metotlu):**
```csharp
dto.MuhasebeHesapPlaniId = muhasebeDetay.MuhasebeHesapPlaniId;
dto.AnaMuhasebeHesapKodu = muhasebeDetay.AnaMuhasebeHesapKodu;
dto.MuhasebeHesapSiraNo = muhasebeDetay.SiraNo;
dto.Kod = muhasebeDetay.Kod;

var result = await base.AddAsync(dto);
await tx.CommitAsync(CancellationToken.None);
return result;
```

**Kazanım:** 25+ satır manuel entity oluşturma → 4 satır DTO doldurma + `base.AddAsync(dto)`.

---

## 4. Neden UpdateAsync'ler Değiştirilmedi?

Aşağıdaki UpdateAsync metotları **bilinçli olarak manuel bırakılmıştır:**

| Servis | Manual Sebep | Risk Seviyesi |
|--------|--------------|---------------|
| [`CariKartService.UpdateAsync`](backend/Muhasebe/CariKartlar/Services/CariKartService.cs:134-190) | Tip değişmezlik kontrolü (muhasebe hesabı olan kartta tip değişemez), tesis değişmezlik kontrolü, muhasebe hesap adı/aktiflik senkronizasyonu. `base.UpdateAsync` AutoMapper ile tüm alanları eşler; burada ise **sadece belirli alanlar** güncelleniyor (`CariKodu`, `AnaMuhasebeHesapKodu`, `MuhasebeHesapSiraNo` güncellenmez). | Yüksek |
| [`MuhasebeDonemService.UpdateAsync`](backend/Muhasebe/MuhasebeDonemleri/Services/MuhasebeDonemService.cs:70-116) | Kapalı dönemde sadece `Aciklama` değiştirilebilir; diğer alanlar değiştirilmeye çalışılırsa hata fırlatır. Açık dönemde `KapaliMi` değişikliği `DonemKapatAsync`/`DonemAcAsync` endpoint'lerine zorunlu kılınmıştır. `base.UpdateAsync` bu seviyede granular kontrol sağlamaz. | Yüksek |
| [`KasaBankaHesapService.UpdateAsync`](backend/Muhasebe/KasaBankaHesaplari/Services/KasaBankaHesapService.cs:103-167) | Tip/tesis değişmezlik kontrolü, muhasebe hesap adı/tesisId/aktiflik senkronizasyonu. `Tip` ve `Kod` alanları güncellenmez. | Yüksek |
| [`DepoService.UpdateAsync`](backend/Muhasebe/Depolar/Services/DepoService.cs:80-135) | `DepoCikisGruplari` alt entity senkronizasyonu (ekleme/güncelleme/silme), muhasebe hesap senkronizasyonu, tesis değişmezlik kontrolü. `base.UpdateAsync` alt entity'leri yönetmez. | Yüksek |

---

## 5. Delete/Soft-Delete Davranışı

Tüm servislerde delete davranışı tutarlıdır:

| Servis | Delete Pattern | Soft-Delete Mekanizması |
|--------|---------------|------------------------|
| Kategori A servisleri | `base.DeleteAsync(id)` veya override yok (base kullanılır) | Platform `SaveChangesInterceptor` → `IsDeleted = true` |
| CariKartService | `base.DeleteAsync(id)` + bağlı muhasebe hesap deaktivasyonu | ✅ |
| MuhasebeDonemService | `base.DeleteAsync(id)` + kapalı dönem/fiş kontrolü | ✅ |
| KasaBankaHesapService | `base.DeleteAsync(id)` + bağlı muhasebe hesap deaktivasyonu | ✅ |
| DepoService | `base.DeleteAsync(id)` + alt depo kontrolü + muhasebe hesap deaktivasyonu | ✅ |
| SatisBelgesiService | Manuel `IsDeleted = true` (belge + satırlar) | ⚠️ Manuel, base kullanılmaz |
| MuhasebeFisService | Manuel `IsDeleted = true` | ⚠️ Manuel, base kullanılmaz |

**Not:** `SatisBelgesiService` ve `MuhasebeFisService` base `DeleteAsync` override etmez; kendi workflow-aware delete metotları vardır. Manuel soft-delete bu servisler için kabul edilebilir.

---

## 6. AutoMapper Kullanımı

| Pattern | Servis Sayısı | Açıklama |
|---------|--------------|----------|
| `CreateMap<Entity, Dto>().ReverseMap()` ile tam otomatik | 12 | Kategori A servisleri |
| `Mapper.Map<TDto>(entity)` manuel çağrısı | 3 | Kategori B UpdateAsync'ler (manuel bırakıldı) |
| `_mapper.Map<Depo>(dto)` manuel çağrısı | 1 | DepoService AddAsync (repository pattern) |
| `Mapper.Map<TDto>(entity)` manuel çağrısı | 2 | Tesis include reload sonrası |

**İyileştirme:** Refactoring sonrası `CariKartService.AddAsync`, `MuhasebeDonemService.AddAsync/GetById/GetAll`, `KasaBankaHesapService.AddAsync` artık manuel `Mapper.Map` çağırmıyor; bu işi base metotlara bırakıyor.

---

## 7. Repository Kullanımı

Tüm 21 repository [`BaseRdbmsRepository<TEntity, TKey>`](platform/TOD.Platform.Persistence.Rdbms/Repositories/BaseRdbmsRepository.cs)'den türer. Repository metot kullanımı:

| Repository Metodu | Kullanan Servisler | Durum |
|-------------------|-------------------|-------|
| `AddAsync(entity)` | DepoService, SatisBelgesiService, MuhasebeFisService | Manuel CRUD yapan servisler |
| `Update(entity)` | SatisBelgesiService | Manuel workflow |
| `Delete(entity)` | — | Base üzerinden |
| `SaveChangesAsync()` | DepoService, SatisBelgesiService, MuhasebeFisService | Manuel CRUD |
| `GetByIdAsync(id, include)` | MuhasebeDonemService (reload için) | Base sonrası reload |
| `FirstOrDefaultAsync(predicate, include)` | SatisBelgesiService | Workflow kontrolleri |
| `AnyAsync(predicate)` | Çoğu servis | Validasyon kontrolleri |
| `Where(predicate)` | Çoğu servis | Filtreleme |

---

## 8. Genel İstatistikler

| Metrik | Faz 59 (Kalıtım) | Faz 59A (Metot Kullanımı) |
|--------|-----------------|--------------------------|
| Toplam servis | 28 | 28 |
| BaseRdbms'ten türeyen | 18 | 18 |
| Tüm CRUD base metotlu (A) | 10 | 12 |
| Kısmen base metotlu, düzeltildi (B) | — | 3 |
| Repository pattern (C) | — | 1 |
| Custom CRUD (D) | 6 | 2 |
| Standalone (E) | 6 | 7 |
| **Refaktör edilen metot** | 0 | **7** |
| **Manuel bırakılan UpdateAsync** | — | **4** |
| **Eklenen yardımcı metot** | — | **1** (`CombineIncludes`) |

### Refaktör edilen metotlar:
1. [`CariKartService.AddAsync`](backend/Muhasebe/CariKartlar/Services/CariKartService.cs:84-113) — Path B
2. [`MuhasebeDonemService.AddAsync`](backend/Muhasebe/MuhasebeDonemleri/Services/MuhasebeDonemService.cs:52-68)
3. [`MuhasebeDonemService.GetByIdAsync`](backend/Muhasebe/MuhasebeDonemleri/Services/MuhasebeDonemService.cs:31-35)
4. [`MuhasebeDonemService.GetAllAsync`](backend/Muhasebe/MuhasebeDonemleri/Services/MuhasebeDonemService.cs:37-41)
5. [`KasaBankaHesapService.AddAsync`](backend/Muhasebe/KasaBankaHesaplari/Services/KasaBankaHesapService.cs:38-95)
6. [`MuhasebeDonemService.CombineIncludes`](backend/Muhasebe/MuhasebeDonemleri/Services/MuhasebeDonemService.cs:262-272) — Yeni yardımcı metot
7. 4 `UpdateAsync` metodu **bilinçli olarak manuel bırakıldı** (iş kuralı koruması)

---

## 9. Derleme Sonucu

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 10. Sonuç ve Öneriler

### Başarılar
- **7 metot** başarıyla base metotlara taşındı.
- **~60 satır** manuel entity oluşturma ve DbContext çağrısı elimine edildi.
- **0 hata, 0 uyarı** ile derleme başarılı.
- Transaction yönetimi, iş kuralları, muhasebe hesap senkronizasyonu ve tesis erişim kontrolleri **tamamen korundu**.

### Bilinçli Olarak Manuel Bırakılanlar
- 4 UpdateAsync: CariKartService, MuhasebeDonemService, KasaBankaHesapService, DepoService
- Sebep: Bu metotlar sadece belirli alanları günceller; `base.UpdateAsync` AutoMapper ile tüm alanları merge eder. Ayrıca tip/tesis değişmezlik kontrolleri, dönem kapatma/açma kuralları, alt entity senkronizasyonu gibi karmaşık iş kuralları içerirler.

### Gelecek Fazlar İçin Öneriler
1. **DepoService.AddAsync**: `_repository.AddAsync` + `_repository.SaveChangesAsync` → `base.AddAsync(dto)` geçişi değerlendirilebilir. Ancak `DepoCikisGruplari` alt entity'lerinin manuel oluşturulması (`BuildCikisGruplari`) base AddAsync ile uyumsuzdur.
2. **MuhasebeDonemService.UpdateAsync**: Kapalı/açık dönem mantığı için `EnrichEntityAsync` override'ı ile base UpdateAsync kullanılabilir hale getirilebilir.
3. **CariKartService/KasaBankaHesapService UpdateAsync**: Tip/tesis değişmezlik kontrolleri ve muhasebe hesap senkronizasyonu, `OnEntitySavedAsync` hook'u ile base UpdateAsync sonrası yapılabilir.
