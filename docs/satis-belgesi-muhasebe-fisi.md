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

## Faz 65E: Fiş Bilgisi ve Fişe Git Aksiyonu

### Route Analizi

Mevcut muhasebe fişleri listesi route'u: `/muhasebe/fisler`

[`MuhasebeFislerComponent`](frontend/src/app/pages/muhasebe/fisler/muhasebe-fisler.component.ts) içinde `goToFis(row)` metodu `queryParams: { id: row.id }` ile aynı rotaya yönlenir ve ilgili fişi filtreler+vurgular. Detay route'u mevcut değildir; fiş detayı dialog üzerinden [`openDetay(row)`](frontend/src/app/pages/muhasebe/fisler/muhasebe-fisler.component.ts:279) ile açılır.

### "Fişe Git" Butonu

[`satis-belgeleri.component.ts`](frontend/src/app/pages/muhasebe/satis-belgeleri/satis-belgeleri.component.ts):

```typescript
hasMuhasebeFisi(belge: SatisBelgesiDto): boolean {
    return !!belge.muhasebeFisId;
}

muhasebeFisineGit(belge: SatisBelgesiDto): void {
    if (!belge.muhasebeFisId) {
        return;
    }
    this.router.navigate(['/muhasebe/fisler'], { queryParams: { id: belge.muhasebeFisId } });
}
```

- Buton ikonu: `pi pi-external-link`
- Tooltip: "Muhasebe Fişine Git"
- Constructor'a `Router` inject edildi

### Detay Dialog'da Fiş Bilgisi

[`satis-belgeleri.component.html`](frontend/src/app/pages/muhasebe/satis-belgeleri/satis-belgeleri.component.html) detay panelinde:

- Fiş varsa: Tıklanabilir link "Fiş # {muhasebeFisId}" + oluşturma tarihi (`dd.MM.yyyy HH:mm`)
- Fiş yoksa: "Henüz oluşturulmadı"

### Görünürlük Kuralları (Güncel)

| Buton | Görünme Koşulu |
|-------|----------------|
| Muhasebe Fişi Oluştur | `durum === MuhasebeOnaylandi && !muhasebeFisId` |
| Muhasebe Fişine Git | `!!muhasebeFisId` |

İki buton birbirini dışlar: Fiş oluşturulmamışsa "Oluştur", oluşturulmuşsa "Fişe Git" görünür.

### Backend

Bu fazda backend değişikliği yapılmamıştır. Mevcut `SatisBelgesiDto` zaten `muhasebeFisId` ve `muhasebeFisOlusturmaTarihi` alanlarını içermektedir (Faz 65B).

### Migration

Bu fazda migration gerekmemiştir.

### Değişen Dosyalar (Faz 65E)

| Dosya | Durum | Açıklama |
|-------|-------|----------|
| [`satis-belgeleri.component.ts`](frontend/src/app/pages/muhasebe/satis-belgeleri/satis-belgeleri.component.ts) | Değişiklik | `Router` inject, `hasMuhasebeFisi` + `muhasebeFisineGit` metotları |
| [`satis-belgeleri.component.html`](frontend/src/app/pages/muhasebe/satis-belgeleri/satis-belgeleri.component.html) | Değişiklik | "Fişe Git" butonu + detay dialog'da fiş bilgisi |

## Faz 66 — Bağlı Muhasebe Fişi Olan Satış Belgelerinde Koruma Kuralları

### Amaç

Satış belgesi ile muhasebe fişi bağlantısı kurulduktan sonra (`MuhasebeFisId` dolu), satış belgesi üzerinde yapılabilecek değişiklik/silme/iptal davranışlarını muhasebe fişi bütünlüğünü koruyacak şekilde netleştirir.

### Temel İlke

`MuhasebeFisId` dolu bir satış belgesi **muhasebe etkisi doğurmuştur**. Bu nedenle:

- **Belge güncellenemez.**
- **Belge silinemez.**
- **Belge doğrudan iptal edilemez.**
- **Belge reddedilemez.**
- **Belge tekrar muhasebe onayına gönderilemez.**
- **Belge tekrar muhasebe onaylanamaz.**

Değişiklik ancak **bağlı muhasebe fişi için iptal/ters kayıt prosedürü** işletildikten sonra yapılabilir.

### Backend Koruması

[`SatisBelgesiService.cs`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs) içerisinde `ThrowIfMuhasebeFisiOlusmus` private helper'ı eklendi:

```csharp
private static void ThrowIfMuhasebeFisiOlusmus(SatisBelgesi belge, string islemAdi)
{
    if (belge.MuhasebeFisId.HasValue)
    {
        throw new BaseException(
            $"Bu satış belgesi için muhasebe fişi oluşturulduğundan {islemAdi} işlemi yapılamaz. " +
            "Önce bağlı muhasebe fişi için iptal/ters kayıt süreci işletilmelidir.",
            errorCode: 400);
    }
}
```

Bu helper aşağıdaki tüm mutasyon metotlarında belge bulunduktan hemen sonra çağrılır:

| Metot | Kontrol | Hata Mesajı |
|-------|---------|-------------|
| `UpdateAsync` | `ThrowIfMuhasebeFisiOlusmus(belge, "güncelleme")` | güncelleme işlemi yapılamaz |
| `DeleteAsync` | `ThrowIfMuhasebeFisiOlusmus(belge, "silme")` | silme işlemi yapılamaz |
| `IptalEtAsync` | `ThrowIfMuhasebeFisiOlusmus(belge, "iptal")` | iptal işlemi yapılamaz |
| `ReddetAsync` | `ThrowIfMuhasebeFisiOlusmus(belge, "reddetme")` | reddetme işlemi yapılamaz |
| `MuhasebeOnayinaGonderAsync` | `ThrowIfMuhasebeFisiOlusmus(belge, "muhasebe onayına gönderme")` | muhasebe onayına gönderme işlemi yapılamaz |
| `MuhasebeOnaylaAsync` | `ThrowIfMuhasebeFisiOlusmus(belge, "muhasebe onaylama")` | muhasebe onaylama işlemi yapılamaz |

### Frontend Görünürlük Kuralları (Güncel)

[`satis-belgeleri.component.ts`](frontend/src/app/pages/muhasebe/satis-belgeleri/satis-belgeleri.component.ts) içindeki `can*` helper'larına `muhasebeFisId` kontrolü eklendi. Bağlı fişi olan belgelerde tüm mutasyon butonları gizlenir.

| Helper | Ek Kontrol | Sonuç |
|--------|-----------|-------|
| `canEdit` | `if (belge.muhasebeFisId) return false` | Düzenle butonu gizlenir |
| `canDelete` | `if (belge.muhasebeFisId) return false` | Sil butonu gizlenir |
| `canGonder` | `if (belge.muhasebeFisId) return false` | Onaya Gönder butonu gizlenir |
| `canOnayla` | `if (belge.muhasebeFisId) return false` | Onayla butonu gizlenir |
| `canReddet` | `if (belge.muhasebeFisId) return false` | Reddet butonu gizlenir |
| `canIptal` | `if (belge.muhasebeFisId) return false` | İptal butonu gizlenir |
| `canFisOlustur` | (zaten `!belge.muhasebeFisId` kontrolü var) | Değişiklik yok |
| `hasMuhasebeFisi` | (zaten `!!belge.muhasebeFisId` kontrolü var) | Değişiklik yok |

**Not:** Frontend görünürlük kuralları kullanıcı deneyimi içindir. Nihai güvenlik backend validasyonları ile sağlanır.

### Bu Fazda Yapılmayanlar

- Otomatik ters muhasebe fişi oluşturulmaz.
- Muhasebe fişi otomatik iptal edilmez.
- Bağlı fişi iptal edilmiş satış belgesini iptal etme akışı bu fazda ele alınmaz.
- e-Fatura/e-Arşiv iptali yapılmaz.

### Bağlı Fiş İptal/Ters Kayıt Süreci (Sonraki Faz)

Bağlı muhasebe fişi olan bir satış belgesini iptal etmek gerektiğinde:

1. Önce bağlı muhasebe fişi için ters kayıt fişi oluşturulmalıdır.
2. Veya bağlı fiş iptal edilmelidir (`Durum = Iptal`).
3. Ardından satış belgesi iptal edilebilir.

Bu akış ayrı bir fazda (Faz 67 veya sonrası) değerlendirilecektir.

### Değişen Dosyalar (Faz 66)

| Dosya | Durum | Açıklama |
|-------|-------|----------|
| [`SatisBelgesiService.cs`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs) | Değişiklik | `ThrowIfMuhasebeFisiOlusmus` helper + 6 mutasyon metoduna koruma eklendi |
| [`satis-belgeleri.component.ts`](frontend/src/app/pages/muhasebe/satis-belgeleri/satis-belgeleri.component.ts) | Değişiklik | `can*` helper'larına `muhasebeFisId` kontrolü eklendi |
| [`satis-belgesi-muhasebe-fisi.md`](docs/satis-belgesi-muhasebe-fisi.md) | Değişiklik | Faz 66 dokümantasyonu eklendi |

### Migration

Bu fazda model değişikliği yapılmadığından migration gerekmemiştir.

---

## Faz 68 — Bağlı Fiş Durumuna Göre Akıllı Koruma

### Amaç

Faz 66'daki salt `HasValue` kontrolü yerine, bağlı muhasebe fişinin durumuna göre akıllı karar veren bir koruma katmanı oluşturulmuştur. Ayrıca taslak fiş silindiğinde `SatisBelgesi.MuhasebeFisId` referansı otomatik temizlenir.

### Unique Index Düzeltmesi

Faz 67 analizinde tespit edilen ters kayıt çakışma riski için migration oluşturulmuştur:

| Önceki Filter | Yeni Filter |
|---------------|-------------|
| `[Durum] <> 'Iptal'` | `[Durum] NOT IN ('Iptal', 'TersKayit')` |

**Migration:** [`20260524211826_FixMuhasebeFisKaynakUniqueIndexForTersKayit`](backend/Infrastructure/EntityFramework/Migrations/20260524211826_FixMuhasebeFisKaynakUniqueIndexForTersKayit.cs)

Bu sayede aynı kaynaktan (TesisId + KaynakModul + KaynakId) hem onaylı fiş hem de ters kayıt fişi oluşabilir.

### Durum-Bazlı Helper: `ThrowIfMuhasebeFisiIslemiEngellerAsync`

[`SatisBelgesiService.cs`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs) içinde eski statik `ThrowIfMuhasebeFisiOlusmus` kaldırılmış, yerine async durum-bazlı helper eklenmiştir.

| Bağlı Fiş Durumu | Karar | Hata Mesajı |
|---|---|---|
| `MuhasebeFisId` null | ✅ Serbest | — |
| Fiş bulunamadı | ❌ Hata + log warning | "Satış belgesine bağlı muhasebe fişi bulunamadı. Sistem yöneticinize başvurun." |
| `IsDeleted = true` | ❌ Hata + log warning | "Satış belgesine bağlı muhasebe fişi silinmiş görünüyor. Sistem yöneticinize başvurun." |
| `Taslak` | ❌ Hata | "Bu satış belgesine bağlı muhasebe fişi taslak durumunda. Önce bağlı fişi silmeniz gerekir." |
| `Onayli` | ❌ Hata | "Bu satış belgesine bağlı muhasebe fişi onaylı durumdadır. Önce bağlı fiş için iptal/ters kayıt süreci işletilmelidir." |
| `Iptal` | ✅ Serbest | Ters kayıt oluşturulmuş, muhasebe etkisi sıfırlanmış |
| `TersKayit` | ❌ Hata + log warning | Veri tutarsızlığı — MuhasebeFisId TersKayit fişine işaret etmemeli |
| Bilinmeyen durum | ❌ Hata | "Bağlı muhasebe fişinin durumu nedeniyle işlem yapılamaz: {Durum}" |

### Etkilenen Metotlar

6 mutasyon metodu da async helper'a geçirilmiştir:

- [`UpdateAsync`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs:239)
- [`DeleteAsync`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs:294)
- [`MuhasebeOnayinaGonderAsync`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs:327)
- [`MuhasebeOnaylaAsync`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs:356)
- [`ReddetAsync`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs:390)
- [`IptalEtAsync`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs:415)

### Taslak Fiş Silindiğinde Referans Temizliği

[`MuhasebeFisService.DeleteAsync`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs:1438) içine, fişin kaynağı `SatisBelgesi` ise ve durumu `Taslak` ise `SatisBelgesi.MuhasebeFisId` ve `MuhasebeFisOlusturmaTarihi` alanlarını temizleyen cross-aggregate güncelleme eklenmiştir.

**Gerekçe:** Taslak fiş henüz muhasebe etkisi doğurmamıştır. Silindiğinde satış belgesi yeniden düzenlenebilir/iptal edilebilir hale gelmelidir. `DbContext` üzerinden `SatisBelgeleri` set'ine doğrudan erişim, cross-aggregate transaction gerektiği için kabul edilmiştir.

### Onaylı Fiş İptal Edildiğinde Davranış

[`MuhasebeFisService.IptalEtAsync`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs:211) sonrası `SatisBelgesi.MuhasebeFisId` **korunur**. Bunun yerine helper `Durum = Iptal` için satış belgesi iptaline izin verir (Seçenek B).

### Frontend

Frontend değişikliği yapılmamıştır (Seçenek A — güvenli tarafta kal). `MuhasebeFisId` doluysa tüm mutasyon butonları gizli kalır. Iptal fişli satış belgesini UI'dan iptal edebilme aksiyonu ayrı fazda (Faz 69) ele alınacaktır.

### Yeni Bağımlılıklar

[`SatisBelgesiService`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs) constructor'ına:
- `IMuhasebeFisRepository` — bağlı fiş durumunu okumak için (base repository `FirstOrDefaultAsync` kullanılır)
- `ILogger<SatisBelgesiService>` — veri tutarsızlık durumlarında warning loglamak için

### Değişen Dosyalar (Faz 68)

| Dosya | Durum | Açıklama |
|-------|-------|----------|
| [`SatisBelgesiService.cs`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs) | Değişiklik | `ThrowIfMuhasebeFisiOlusmus` → `ThrowIfMuhasebeFisiIslemiEngellerAsync`, DI genişletildi |
| [`MuhasebeFisService.cs`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs) | Değişiklik | `DeleteAsync` içine cross-aggregate `SatisBelgesi.MuhasebeFisId` temizliği eklendi |
| [`StysAppDbContext.cs`](backend/Infrastructure/EntityFramework/StysAppDbContext.cs) | Değişiklik | Unique index filter: `<> 'Iptal'` → `NOT IN ('Iptal', 'TersKayit')` |
| [`20260524211826_FixMuhasebeFisKaynakUniqueIndexForTersKayit.cs`](backend/Infrastructure/EntityFramework/Migrations/20260524211826_FixMuhasebeFisKaynakUniqueIndexForTersKayit.cs) | Yeni | Migration |
| [`satis-belgesi-muhasebe-fisi.md`](docs/satis-belgesi-muhasebe-fisi.md) | Değişiklik | Faz 68 dokümantasyonu eklendi |

### Bu Fazda Yapılmayanlar

- Satış belgesinden tek tuşla bağlı fişi iptal etme akışı (Faz 69)
- Entegre iptal zinciri (Faz 69)
- e-Fatura/e-Arşiv iptali (Faz 70+)
- Frontend `can*` helper'ları güncellemesi (güvenli tarafta kal)
- `IptalEtAsync` sonrası `MuhasebeFisId` temizliği (Seçenek B uygulandı — referans korunur, helper izin verir)
## Faz 73B Notu

Satış faturası muhasebe fişi üretimi belge tipi bazlı strateji mimarisine taşınmıştır. Mevcut davranış korunmuştur; alış faturası, iade faturaları ve tevkifatlı belgeler için otomatik fiş üretimi bu fazda açılmamıştır.
