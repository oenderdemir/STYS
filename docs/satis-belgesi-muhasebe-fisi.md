# Satış Belgesi Muhasebe Fişi Üretimi (Faz 65C)

## Genel Bakış

`ISatisBelgesiMuhasebeFisService` / `SatisBelgesiMuhasebeFisService`, `MuhasebeOnaylandi` durumundaki bir satış belgesinden 120 / 600 / 391 hesap kurgusuyla muhasebe fişi oluşturur.

### Kapsam (Faz 65C)

| Özellik | Destek | Açıklama |
|---------|--------|----------|
| Muhasebe fişi oluşturma | ✅ | 120 / 600 / 391 kurgusu |
| KDV'li satış belgesi | ✅ | ToplamKdv > 0 ise 391 satırı eklenir |
| KDV'siz satış belgesi | ✅ | ToplamKdv = 0 ise 391 satırı atlanır |
| FaturaTaslagi | ✅ | |
| Proforma | ❌ | Hata döner |
| İade faturası | ❌ | Hata döner (Faz 65D'de) |
| Tevkifatlı satır | ❌ | Hata döner |
| Controller endpoint | ✅ | Faz 65D'de eklendi |
| Frontend buton/aksiyon | ✅ | Faz 65D'de eklendi |
| MuhasebeOnaylaAsync entegrasyonu | ❌ | Faz 65E'de |
| e-Fatura entegrasyonu | ❌ | Sonraki faz |
| İptal / ters kayıt | ❌ | Sonraki faz |

## Mimari Kararlar

### Neden BaseRdbmsService Türetilmedi?

`SatisBelgesiMuhasebeFisService`, [`BaseRdbmsService`](platform/TOD.Platform.Persistence.Rdbms/Services/BaseRdbmsService.cs) sınıfından türetilmemiştir. Gerekçeler:

1. **Çapraz-agrega transaction**: Satış belgesi (`SatisBelgeleri` agrega) ve muhasebe fişi (`MuhasebeFisler` agrega) iki farklı agrega köküdür. Aynı transaction içinde her ikisine de yazma yapılması gerekir. `BaseRdbmsService` tek bir agrega için tasarlanmıştır.
2. **Çoklu repository kullanımı**: Hem `ISatisBelgesiRepository` hem `IMuhasebeFisRepository` hem de `StysAppDbContext` doğrudan kullanılmaktadır. `BaseRdbmsService` yalnızca tek bir repository tipiyle çalışır.
3. **Özel private helper'lar**: `MuhasebeFisService` içindeki `GenerateFisNoAsync`, `GetKdvHesabiAsync`, `GetFisTipiKodu`, `IsUniqueConflict` gibi yardımcılar private olduğu için bu serviste kopyalanmıştır (aşağıdaki tabloya bakınız).

### DbContext Doğrudan Kullanımı

[`StysAppDbContext`](backend/Infrastructure/EntityFramework/StysAppDbContext.cs), transaction yönetimi (`BeginTransactionAsync` / `CommitAsync`) ve `MuhasebeFisler` DbSet'ine doğrudan erişim için kullanılır. Bu, çapraz-agrega transaction'ı mümkün kılar.

## Hesap Kurgusu (120 / 600 / 391)

| Hesap | Ad | Borç/Alacak | Tutar | Koşul |
|-------|-----|-------------|-------|-------|
| 120 | Alıcılar | Borç | GenelToplam | Her zaman |
| 600 | Yurtiçi Satışlar | Alacak | ToplamMatrah | Her zaman |
| 391 | Hesaplanan KDV | Alacak | ToplamKdv | Sadece ToplamKdv > 0 |

### Hesap Bulma Stratejisi

Hesaplar `GetHesapPlaniAsync` ve `GetKdvHesabiAsync` ile bulunur:

- **GetHesapPlaniAsync** (120, 600): `HesapPlani` tablosunda `TamKod` alanı `anaKod` ile başlayan kayıtları arar. Tesis ID'ye özel olanları önceliklendirir. Tam eşleşme varsa onu, yoksa prefix eşleşmesini döner.
- **GetKdvHesabiAsync** (391): `MuhasebeFisService`'teki aynı pattern kopyalanmıştır. KDV hesapları için tesis bazlı önceliklendirme yapar.

Bulunamayan her hesap için `BaseException` fırlatılır.

## Tekrarlanmış (Replicated) Private Yardımcılar

Aşağıdaki metodlar [`MuhasebeFisService`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs) içinde `private` olduğu için bu serviste kopyalanmıştır:

| Metod | Orijinal Konum | Kopyalanma Gerekçesi |
|-------|---------------|---------------------|
| `GenerateFisNoAsync` | MuhasebeFisService (private) | Fiş no formatı: `{MaliYil}-{FisTipiKodu}-{6 haneli sıra}` |
| `GetFisTipiKodu` | MuhasebeFisService (private) | SatisBelgesi için "STB" döner |
| `IsUniqueConflict` | MuhasebeFisService (private) | SQL Server 2601/2627 hata kontrolü |
| `GetKdvHesabiAsync` | MuhasebeFisService (private) | 391 hesabı bulma |
| `GetHesapPlaniAsync` | Yeni (bu servis) | 120/600 hesap bulma (tesis öncelikli) |

## İş Akışı

```
MuhasebeFisiOlusturAsync(satisBelgesiId)
│
├── 1. Validasyonlar (transaction dışı)
│   ├── ID > 0 kontrolü
│   ├── Belge mevcut mu?
│   ├── IsDeleted kontrolü
│   ├── Durum == MuhasebeOnaylandi mi?
│   ├── MuhasebeFisId == null mi?
│   ├── TesisId mevcut mu?
│   ├── BelgeTipi: Proforma/IadeFaturasi red
│   ├── ToplamMatrah > 0
│   ├── GenelToplam > 0
│   ├── ToplamMatrah + ToplamKdv ≈ GenelToplam (0.01m tolerans)
│   └── GetAktifDonemAsync → açık dönem kontrolü
│
├── 2. Transaction loop (max 3 deneme)
│   └── for (int attempt = 0; attempt < 3; attempt++)
│       │
│       ├── Transaction başlat
│       ├── Belgeyi satırlarıyla birlikte yeniden oku
│       ├── Duplicate kontrol (MuhasebeFisler tablosunda)
│       ├── Satır validasyonları
│       │   ├── Aktif satır var mı?
│       │   ├── Tevkifatlı satır kontrolü
│       │   └── Satır toplamları = Belge toplamları (0.01m)
│       ├── Hesap planından 120, 600, (391) bul
│       ├── MuhasebeFisSatir'ları oluştur
│       ├── Borç/alacak dengesi kontrolü (0.01m)
│       ├── FisNo üret (GenerateFisNoAsync)
│       ├── MuhasebeFis entity oluştur ve kaydet
│       ├── SatisBelgesi.MuhasebeFisId ata ve kaydet
│       ├── Transaction commit
│       └── DbUpdateException → IsUniqueConflict → retry
│
└── 3. Güncel DTO dön
    ├── Belgeyi yeniden oku
    ├── Satırları manuel yükle
    └── Map → SatisBelgesiDto
```

## Transaction Retry Mekanizması

`FisNo` çakışması durumunda (SQL Server unique constraint hatası 2601/2627), transaction 3 defaya kadar tekrarlanır. Her denemede yeni `FisNo` üretilir. 3 deneme de başarısız olursa hata fırlatılır.

```csharp
catch (DbUpdateException ex) when (IsUniqueConflict(ex))
{
    if (attempt < 2)
    {
        _logger.LogWarning("Fiş no çakışması, tekrar deneniyor. Deneme: {Attempt}", attempt + 2);
        continue;
    }
    throw;
}
```

## DI Kaydı

[`Program.cs`](backend/Program.cs:129):

```csharp
builder.Services.AddScoped<ISatisBelgesiMuhasebeFisService, SatisBelgesiMuhasebeFisService>();
```

## MuhasebeKaynakModulleri Sabiti

[`MuhasebeKaynakModulleri`](backend/Muhasebe/Common/Constants/MuhasebeKaynakModulleri.cs:12):

```csharp
public const string SatisBelgesi = "SatisBelgesi";
```

`Hepsi` dizisine de eklenmiştir.

## Dosya Listesi

| Dosya | Durum | Açıklama |
|-------|-------|----------|
| [`ISatisBelgesiMuhasebeFisService.cs`](backend/Muhasebe/SatisBelgeleri/Services/ISatisBelgesiMuhasebeFisService.cs) | Yeni | Interface |
| [`SatisBelgesiMuhasebeFisService.cs`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiMuhasebeFisService.cs) | Yeni | Implementasyon (~350 satır) |
| [`MuhasebeKaynakModulleri.cs`](backend/Muhasebe/Common/Constants/MuhasebeKaynakModulleri.cs) | Değişiklik | SatisBelgesi sabiti eklendi |
| [`Program.cs`](backend/Program.cs) | Değişiklik | DI kaydı eklendi |

## Bağımlılıklar

- `ISatisBelgesiRepository` — Satış belgesi okuma/güncelleme
- `IMuhasebeFisRepository` — Muhasebe fişi ekleme (potansiyel kullanım)
- `StysAppDbContext` — Transaction yönetimi, MuhasebeFisler DbSet, SatisBelgeleri DbSet
- `IMapper` — Entity → DTO dönüşümü
- `IMuhasebeDonemService` — Açık dönem kontrolü
- `ILogger<SatisBelgesiMuhasebeFisService>` — Loglama

## Hata Kodları

| Kod | Durum |
|-----|-------|
| 400 | Validasyon hatası (geçersiz ID, durum, tutar, vb.) |
| 404 | Belge veya hesap bulunamadı |
| 409 | Zaten fiş oluşturulmuş (duplicate) |
| 500 | Beklenmeyen hata (güncel belge okunamadı) |

## Faz 65D: Controller Endpoint ve Frontend Aksiyonu

### Controller Endpoint

[`SatisBelgeleriController.cs`](backend/Muhasebe/SatisBelgeleri/Controllers/SatisBelgeleriController.cs):

```csharp
[HttpPost("{id:int}/muhasebe-fisi-olustur")]
[Permission(StructurePermissions.MuhasebeFisYonetimi.Manage)]
public async Task<IActionResult> MuhasebeFisiOlustur(int id, CancellationToken cancellationToken)
{
    var result = await _muhasebeFisService.MuhasebeFisiOlusturAsync(id, cancellationToken);
    return Ok(result);
}
```

- Route: `POST /ui/muhasebe/satis-belgeleri/{id:int}/muhasebe-fisi-olustur`
- Yetki: `MuhasebeFisYonetimi.Manage` (diğer mutasyon endpoint'leriyle aynı)
- Dönüş: `SatisBelgesiDto` (güncellenmiş belge, `muhasebeFisId` ve `muhasebeFisOlusturmaTarihi` dolu)
- Constructor'a `ISatisBelgesiMuhasebeFisService` bağımlılığı eklendi

### Frontend Service

[`satis-belgesi.service.ts`](frontend/src/app/pages/muhasebe/services/satis-belgesi.service.ts):

```typescript
muhasebeFisiOlustur(id: number): Observable<SatisBelgesiDto> {
    return this.http
        .post<ApiResponse<SatisBelgesiDto>>(`${this.base}/${id}/muhasebe-fisi-olustur`, {})
        .pipe(map(envelope => this.unwrap(envelope)));
}
```

### Frontend Buton

[`satis-belgeleri.component.ts`](frontend/src/app/pages/muhasebe/satis-belgeleri/satis-belgeleri.component.ts):

```typescript
canFisOlustur(belge: SatisBelgesiDto): boolean {
    return belge.durum === SatisBelgesiDurumu.MuhasebeOnaylandi && !belge.muhasebeFisId;
}

muhasebeFisiOlustur(belge: SatisBelgesiDto): void {
    this.confirmationService.confirm({
        message: `"${belge.belgeNo}" için muhasebe fişi oluşturmak istediğinize emin misiniz?`,
        header: 'Fiş Oluşturma Onayı',
        icon: 'pi pi-file',
        accept: () => {
            this.service.muhasebeFisiOlustur(belge.id).subscribe({
                next: () => {
                    this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Muhasebe fişi oluşturuldu.' });
                    this.loadBelgeler();
                },
                error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
            });
        }
    });
}
```

### Görünürlük Kuralları

Buton yalnızca aşağıdaki koşulların tümü sağlandığında görünür:

| Koşul | Açıklama |
|-------|----------|
| `belge.durum === SatisBelgesiDurumu.MuhasebeOnaylandi` | Belge muhasebe onayından geçmiş olmalı |
| `!belge.muhasebeFisId` | Daha önce fiş oluşturulmamış olmalı (null/undefined) |

### Kullanıcı Deneyimi

1. Kullanıcı "Muhasebe Fişi Oluştur" butonuna tıklar
2. Onay dialog'u görüntülenir (`pi pi-file` ikonu ile)
3. Kullanıcı onaylarsa endpoint çağrılır
4. Başarılı olursa: "Muhasebe fişi oluşturuldu." toast mesajı gösterilir, tablo yenilenir (buton kaybolur çünkü `muhasebeFisId` artık dolu)
5. Başarısız olursa: Hata mesajı toast ile gösterilir

### Değişen Dosyalar (Faz 65D)

| Dosya | Durum | Açıklama |
|-------|-------|----------|
| [`SatisBelgeleriController.cs`](backend/Muhasebe/SatisBelgeleri/Controllers/SatisBelgeleriController.cs) | Değişiklik | `MuhasebeFisiOlustur` endpoint'i ve DI eklendi |
| [`satis-belgesi.service.ts`](frontend/src/app/pages/muhasebe/services/satis-belgesi.service.ts) | Değişiklik | `muhasebeFisiOlustur` HTTP metodu eklendi |
| [`satis-belgeleri.component.ts`](frontend/src/app/pages/muhasebe/satis-belgeleri/satis-belgeleri.component.ts) | Değişiklik | `canFisOlustur` helper + `muhasebeFisiOlustur` aksiyon |
| [`satis-belgeleri.component.html`](frontend/src/app/pages/muhasebe/satis-belgeleri/satis-belgeleri.component.html) | Değişiklik | "Muhasebe Fişi Oluştur" butonu eklendi |
