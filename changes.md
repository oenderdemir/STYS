# Proje Mimari Kurallari

Bu kurallara uyulmali, mevcut patterni bozmamali:
Her turda yapılan işlemlerin özeti changes.md dosyasına append edilmeli.

## Backend (.NET 10 + EF Core + SQL Server)
- **Repository pattern:** Tum entity erisimi `IBaseRdbmsRepository<TEntity, TKey>` / `BaseRdbmsRepository<TEntity, TKey>` uzerinden yapilir (kaynak: `TOD.Platform.Persistence.Rdbms`). Servisler dogrudan DbContext kullanmaz — yalnizca projection/cross-entity sorgulari icin DbContext kullanilabilir.
- **Servis pattern:** CRUD islemleri icin `BaseRdbmsService<TDto, TEntity, TKey>` kullanilir. Ozel is mantigi olan servisler (ornegin KampTahsisService) repository + DbContext kullanir.
- **Auto-registration:** Repository'ler `builder.Services.AddBaseRdbmsServicesAndRepositoriesScoped(typeof(Program).Assembly)` ile otomatik kayit olur. Custom servisler (IKampXxxService) `Program.cs`'de manuel `AddScoped` ile kayit edilir.
- **Controller:** Tum UI controllerlari `UIController` base class'indan turetilir. Yetkilendirme `[Permission(StructurePermissions.Xxx.View)]` attribute ile yapilir.
- **Entity:** Tum entityler `BaseEntity<T>` uzerinden soft-delete destekler (IsDeleted, DeletedAt, DeletedBy).
- **Migration:** EF Core migration'lari elle yazilir (scaffold/auto-generate kullanilmaz). `StysAppDbContextModelSnapshot.cs` de elle guncellenir.
- **Permission seed:** Yeni modul/menu/rol ekleme SQL migration ile yapilir — TODBase semasi (`TODBase.Roles`, `TODBase.RoleGroupRoles`, `TODBase.Menus`) uzerinden INSERT/UPDATE.
- **Sabit degerler:** Durum, tip gibi sabitler static class + const string pattern ile tanimlanir (ornegin `KampBasvuruDurumlari`, `KampKatilimciTipleri`).

## Frontend (Angular + PrimeNG)
- **Standalone component** pattern kullanilir (NgModule yok).
- **Servis:** Tek bir `KampYonetimiService` tum kamp API cagrilarini icerir. `ApiResponse<T>` envelope uzerinden map edilir.
- **DTO:** Tum tipler `kamp-yonetimi.dto.ts` dosyasinda tanimlanir.
- **Route:** `app.routes.ts`'de lazy load ile eklenir, breadcrumb tanimlanir.
- **Stil:** Her sayfa icin ayri `.scss` dosyasi, PrimeNG componentleri kullanilir.

## Genel Kurallar
- Minimum mudahale: Yalnizca gereken degisiklik yapilir, gereksiz refactor/iyilestirme yapilmaz.
- Mevcut dosya ve pattern ornegi olarak `backend/Countries/` klasoru referans alinabilir.
- Her tur sonunda backend ve frontend build alinir, sonuc raporlanir.
- Talimat dokumani: `2025Kamp_Talimati.pdf` (proje kokunde) is kurallari icin referans kaynaktir.

---

# Kamp Modulu Degisiklik Kaydi



## Tur 1 - Kamp Rezervasyon Modulu (tahsisten rezervasyon uretme)

### Yeni Dosyalar
- backend/Kamp/Entities/KampRezervasyon.cs — yeni entity
- backend/Kamp/Repositories/IKampRezervasyonRepository.cs
- backend/Kamp/Repositories/KampRezervasyonRepository.cs
- backend/Kamp/Services/IKampRezervasyonService.cs
- backend/Kamp/Services/KampRezervasyonService.cs
- backend/Kamp/Controllers/KampRezervasyonController.cs
- backend/Kamp/Dto/KampRezervasyonDto.cs
- backend/Infrastructure/EntityFramework/Migrations/20260405150000_AddKampRezervasyonlari.cs (+Designer)
- backend/Infrastructure/EntityFramework/Migrations/20260405151000_AddKampRezervasyonPermissionsAndMenu.cs
- frontend/src/app/pages/kamp-yonetimi/kamp-rezervasyonlari.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-rezervasyonlari.html
- frontend/src/app/pages/kamp-yonetimi/kamp-rezervasyonlari.scss

### Guncellenen Dosyalar
- backend/Kamp/KampBasvuruKurallari.cs — KampRezervasyonDurumlari sabitleri eklendi
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs — DbSet + model builder
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- backend/StructurePermissions.cs — KampRezervasyonYonetimi eklendi
- backend/ErisimTeshis/ErisimTeshisModulTanimlari.cs — modul tanimi eklendi
- backend/Program.cs — servis kaydi
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts — rezervasyon DTOlari
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.service.ts — 4 yeni metod
- frontend/src/app/pages/kamp-yonetimi/kamp-tahsis-yonetimi.ts — uretRezervasyon()
- frontend/src/app/pages/kamp-yonetimi/kamp-tahsis-yonetimi.html — "Rezervasyon Uret" butonu
- frontend/src/app.routes.ts — /kamp-rezervasyonlari route

### Build
- Backend: BASARILI
- Frontend: BASARILI

---

## Tur 2 - Talimat Uyumluluk Duzeltmeleri

### 2a. Seed data basvuru tarihlerini duzelt
- Sorun: Her donem icin farkli basvuru bitis tarihi hesaplaniyordu
- Talimat: Tum donemler icin tek tarih: 02 Mayis 2025
- Dosya: backend/Infrastructure/EntityFramework/Migrations/20260405160000_FixKampBasvuruTarihleri.cs (YENI)

### 2b. Basvuru tarih validasyonu
- Sorun: BasvuruBaslangicTarihi/BasvuruBitisTarihi kontrolu yoktu, donem aktifse her zaman basvuru yapilabiliyordu
- Duzeltme: ValidateRequest icine tarih araligi kontrolu eklendi
- Dosya: backend/Kamp/Services/KampBasvuruService.cs

### 2c. Sehit/gazi/malul tarife istisnasi
- Sorun: Talimat "sehit yakinlari ile gaziler, harp ve vazife malulleri icin Bakanlik mensuplari tarifesi uygulanir" diyordu, sistemde bu tip yoktu
- Duzeltme:
  - KampKatilimciTipleri'ne SehitGaziMalul eklendi + KamuTarifesiUygulanirMi() metodu
  - KampUcretHesaplamaService'de hard-coded Kamu kontrolu yerine KamuTarifesiUygulanirMi() kullanildi
  - Frontend kamp-basvuru.ts'de katilimciTipleri listesine secenek eklendi
- Dosyalar:
  - backend/Kamp/KampBasvuruKurallari.cs
  - backend/Kamp/Services/KampUcretHesaplamaService.cs
  - frontend/src/app/pages/kamp-yonetimi/kamp-basvuru.ts

### 2d. Katilimci iptali + tek kisi yatak ucreti kurali
- Sorun: Talimat "basvuru sahibi haric istirakçilerin iptali dilekce ile yapilir" ve "tek kisi kalirsa yatak ucreti tahsil edilir" diyordu, sistemde katilimci bazinda iptal yoktu
- Duzeltme:
  - IKampBasvuruService'e KatilimciIptalEtAsync eklendi
  - KampBasvuruService'de implementasyon: basvuru sahibi iptal edilemez, soft-delete ile katilimci cikarilir, tek kisi kalirsa uyari mesaji doner
  - KampBasvuruController'a DELETE endpoint eklendi (KampTahsisYonetimi.Manage yetkisi gerekir)
  - KampKatilimciIptalSonucDto eklendi
- Dosyalar:
  - backend/Kamp/Services/IKampBasvuruService.cs
  - backend/Kamp/Services/KampBasvuruService.cs
  - backend/Kamp/Controllers/KampBasvuruController.cs
  - backend/Kamp/Dto/KampBasvuruDto.cs

### 2e. No-show iptali (donem basladiktan 2 gun sonra kampa katilmayanlarin tahsisini iptal)
- Talimat: "Donemin baslamasindan itibaren ikinci gunun sonuna kadar kampa katilmayanlarin tahsisi iptal edilecektir"
- Backend:
  - IKampTahsisService'e NoShowIptalUygulaAsync eklendi
  - KampTahsisService'de implementasyon: donem baslangicindan 2 gun gecmis mi kontrol eder, TahsisEdildi durumundaki ama aktif rezervasyonu olmayan basvurulari IptalEdildi yapar
  - KampTahsisController'a POST {kampDonemiId}/noshow-iptal endpoint eklendi
  - KampNoShowIptalSonucDto eklendi
- Frontend:
  - KampNoShowIptalSonucDto DTO eklendi (kamp-yonetimi.dto.ts)
  - noShowIptalUygula() metodu eklendi (kamp-yonetimi.service.ts)
  - Tahsis yonetimi sayfasina "No-Show Iptal" butonu eklendi (donem secili oldugunda aktif, severity=danger, outlined)
  - kamp-tahsis-yonetimi.ts'de uygulaNoShowIptal() metodu ve canRunNoShowIptal getter eklendi
- Dosyalar:
  - backend/Kamp/Services/IKampTahsisService.cs
  - backend/Kamp/Services/KampTahsisService.cs
  - backend/Kamp/Controllers/KampTahsisController.cs
  - backend/Kamp/Dto/KampNoShowIptalSonucDto.cs
  - frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts
  - frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.service.ts
  - frontend/src/app/pages/kamp-yonetimi/kamp-tahsis-yonetimi.ts
  - frontend/src/app/pages/kamp-yonetimi/kamp-tahsis-yonetimi.html

### Build Sonuclari (Tur 2)
- Backend: BASARILI (0 hata, 0 uyari)
- Frontend: BASARILI

---

## Tur 3 - UI Tamamlama (katilimci iptali, basvuru detay, iade sayfasi, benim basvurularim)

### 3a. Admin basvuru detay dialog'u
- Backend:
  - IKampBasvuruService'e `GetByIdAsync` eklendi
  - KampBasvuruController'a `GET /ui/kampbasvuru/{id}` endpointi eklendi
  - Endpoint, `KampTahsisYonetimi.View` veya `KampRezervasyonYonetimi.View` yetkisi ile erisilebilir
  - KampBasvuruDto'ya konaklama tarihleri ve createdAt alanlari eklendi
- Frontend:
  - Reusable `kamp-basvuru-detay-dialog` componenti eklendi
  - Tahsis ve rezervasyon ekranlarina "Detay" aksiyonu baglandi

### 3b. Katilimci iptali UI
- Mevcut backend endpointi (`DELETE /ui/kampbasvuru/{kampBasvuruId}/katilimci/{katilimciId}`) frontend'de kullanilmaya baslandi
- KampYonetimiService'e `katilimciIptalEt` metodu eklendi
- Admin detay dialog'unda, basvuru sahibi haric katilimcilar icin "Katilimci Iptal" butonu eklendi
- Iptal sonrasi detay yeniden yukleniyor, tahsis/rezervasyon listeleri refresh ediliyor

### 3c. Benim Basvurularim sayfasi
- Yeni sayfa eklendi:
  - `frontend/src/app/pages/kamp-yonetimi/kamp-benim-basvurularim.ts`
  - `...html`
  - `...scss`
- Route eklendi: `/kamp-basvurularim`
- Kamp basvuru formu ustune bu sayfaya giden hizli erisim butonu eklendi
- Sayfada kullanici kendi basvurularini listeleyip detay dialog'u olmadan katilimci listesini gorebiliyor

### 3d. Iade yonetimi sayfasi
- Yeni bagimsiz admin sayfasi eklendi:
  - `frontend/src/app/pages/kamp-yonetimi/kamp-iade-yonetimi.ts`
  - `...html`
  - `...scss`
- Route eklendi: `/kamp-iade-yonetimi`
- Sayfa, mevcut `iade-karari` backend hesaplama endpointini kullaniyor
- Tahsis listesinden basvuru secilip vazgecme tarihi, odenen toplam tutar, kullanilmayan gun sayisi ve mazeret bilgisi ile iade/kesinti sonucu hesaplanabiliyor
- Tahsis ve rezervasyon ekranlarina Iade Yonetimi kisayolu eklendi

### Guncellenen Dosyalar
- backend/Kamp/Dto/KampBasvuruDto.cs
- backend/Kamp/Services/IKampBasvuruService.cs
- backend/Kamp/Services/KampBasvuruService.cs
- backend/Kamp/Controllers/KampBasvuruController.cs
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.service.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-tahsis-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-tahsis-yonetimi.html
- frontend/src/app/pages/kamp-yonetimi/kamp-rezervasyonlari.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-rezervasyonlari.html
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru.html
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru.scss
- frontend/src/app.routes.ts

### Yeni Dosyalar
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru-detay-dialog.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru-detay-dialog.html
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru-detay-dialog.scss
- frontend/src/app/pages/kamp-yonetimi/kamp-benim-basvurularim.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-benim-basvurularim.html
- frontend/src/app/pages/kamp-yonetimi/kamp-benim-basvurularim.scss
- frontend/src/app/pages/kamp-yonetimi/kamp-iade-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-iade-yonetimi.html
- frontend/src/app/pages/kamp-yonetimi/kamp-iade-yonetimi.scss

### Build Sonuclari (Tur 3)
- Backend: BASARILI
- Frontend: BASARILI

## Codex - EF PendingModelChanges duzeltmesi (2026-04-05)
- `RunDatabaseMigrationsOnStartup` sirasinda alinan `PendingModelChangesWarning` hatasi incelendi.
- EF teshis komutu ile farkin `KampRezervasyonlari.TesisId` indeksi tarafinda oldugu netlestirildi.
- Sorunun kaynagi, kamp rezervasyon migration zincirindeki bozuk/eksik metadata idi:
  - `backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs` guncellendi.
  - `backend/Infrastructure/EntityFramework/Migrations/20260405150000_AddKampRezervasyonlari.Designer.cs` dogru hedef model ile yeniden olusturuldu.
- Yanlis output path'e dusen gecici EF teshis migration'i temizlendi.

### Dogrulama
- `dotnet build backend/STYS.csproj /p:NoWarn=NU1903` : BASARILI
- `dotnet ef migrations has-pending-model-changes --project backend/STYS.csproj --startup-project backend/STYS.csproj --context StysAppDbContext` : `No changes have been made to the model since the last migration.`

---

## Tur 4 - Kamp Menu Reorganizasyonu

### Yapilan Degisiklikler
- "Kamp Yonetimi" ana menusu Isletme altindan cikarilip top-level yapildi (ParentId=NULL)
- Eksik 3 ekranin menusu eklendi:
  - **Iade Yonetimi** (MenuOrder=5, route: kamp-iade-yonetimi, rol kisitlamali)
  - **Basvuru Yap** (MenuOrder=6, route: kamp-basvurusu, herkese acik)
  - **Basvurularim** (MenuOrder=7, route: kamp-basvurularim, herkese acik)
- KampIadeYonetimi yetki sinifi eklendi (Menu/View/Manage)
- Iade rolleri Admin ve TesisManager gruplarina atandi
- ErisimTeshis modul tanimi eklendi

### Menu Yapisi (son durum)
```
Kamp Yonetimi (top-level, fa-campground)
  ├─ 0: Programlar
  ├─ 1: Donemler
  ├─ 2: Tesis Atamalari
  ├─ 3: Tahsisler
  ├─ 4: Rezervasyonlar
  ├─ 5: Iade Yonetimi
  ├─ 6: Basvuru Yap
  └─ 7: Basvurularim
```

### Degisen Dosyalar
- backend/StructurePermissions.cs — KampIadeYonetimi eklendi
- backend/ErisimTeshis/ErisimTeshisModulTanimlari.cs — kamp-iade-yonetimi modulu eklendi
- backend/Infrastructure/EntityFramework/Migrations/20260405170000_ReorganizeKampMenus.cs (YENI)

### Build Sonuclari (Tur 4)
- Backend: BASARILI (0 hata, 0 uyari)
- Frontend: Degisiklik yok (menu DB tarafinda)

---

## Tur 5 - Magic Number/String Parametrizasyonu ve 2026 Yil Guncellemesi

### 5a. KampParametre tablosu — magic numberlari DB'ye tasima
- Yeni `KampParametreleri` tablosu (Kod/Deger/Aciklama, unique index on Kod)
- Yeni `KampParametre` entity + `IKampParametreService` / `KampParametreService` (scoped, lazy-load cache)
- `KampParametreKodlari` static class — tum parametre kodlari
- Seed data ile mevcut tum sabitler DB'ye yazildi (41 parametre)

#### DB'ye tasinan sabitler:
| Parametre | Eski Yer | Deger |
|---|---|---|
| KamuAvansKisiBasi | KampBasvuruKurallari.cs | 1700 |
| DigerAvansKisiBasi | KampBasvuruKurallari.cs | 2550 |
| YemekOrani | KampBasvuruKurallari.cs | 0.50 |
| UcretsizCocukSiniri | KampBasvuruKurallari.cs | 2023-01-01 (2026 kampi icin) |
| YarimUcretliCocukSiniri | KampBasvuruKurallari.cs | 2020-01-01 (2026 kampi icin) |
| EmekliBonusPuan | KampPuanlamaService.cs | 30 |
| KatilimciBasinaPuan | KampPuanlamaService.cs | 10 |
| OncekiYilKatilimPenalti | KampPuanlamaService.cs | 20 |
| TabanPuan.* (5 adet) | KampBasvuruKurallari.GetTabanPuan() | 40/20/15/10/5 |
| Konaklama.*.* (28 adet) | KampBasvuruKurallari.ResolveKonaklama() | Tesis bazli ucretler ve kisi sinirlari |

- KampPuanlamaService ve KampUcretHesaplamaService artik IKampParametreService'ten okuyor
- Kod icindeki eski sabitler fallback default olarak korunuyor

### 5b. Yil guncellemesi (2025 → 2026)
- **Entity**: `Kamp2023tenFaydalandiMi` kaldirildi, `Kamp2025tenFaydalandiMi` eklendi (Kamp2024 korundu)
- **DB migration**: `Kamp2023tenFaydalandiMi` column drop, `Kamp2025tenFaydalandiMi` column add
- **DTO**: Backend + frontend'de 2023→2024, 2024→2025 olarak guncellendi
- **Servisler**: KampBasvuruService, KampPuanlamaService property referanslari guncellendi
- **Frontend**: Checkbox labellari "2024 kampindan faydalandi" / "2025 kampindan faydalandi" olarak guncellendi
- **Frontend**: Basvuru sayfasi baslik metni "2026 yaz kampi" olarak guncellendi
- **KampBasvuruKurallari.cs**: Cocuk yas sinirlari 2026 yilina gore guncellendi (2022→2023, 2019→2020)

### 5c. 2026 Yaz Kampi seed data
- 17 donem eklendi (2026-YAZ-01..17, Haziran-Eylul 2026)
- Basvuru tarihleri: 01 Mart 2026 - 02 Mayis 2026
- Alata (52 kontenjan) ve Foca (61 kontenjan) tesis atamalari
- Mevcut YAZ_KAMPI programi kullanildi

### Yeni Dosyalar
- backend/Kamp/Entities/KampParametre.cs
- backend/Kamp/Services/IKampParametreService.cs
- backend/Kamp/Services/KampParametreService.cs
- backend/Kamp/KampParametreKodlari.cs
- backend/Infrastructure/EntityFramework/Migrations/20260405180000_AddKampParametreAndUpdateYears.cs

### Degisen Dosyalar
- backend/Kamp/Entities/KampBasvuru.cs — column rename
- backend/Kamp/Dto/KampBasvuruDto.cs — property rename
- backend/Kamp/Dto/KampBasvuruRequestDto.cs — property rename
- backend/Kamp/KampBasvuruKurallari.cs — cocuk yas sinir defaults guncellendi
- backend/Kamp/Services/KampBasvuruService.cs — yil referanslari + parametreService inject
- backend/Kamp/Services/KampPuanlamaService.cs — DB parametreleri kullanir
- backend/Kamp/Services/KampUcretHesaplamaService.cs — DB parametreleri kullanir
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs — KampParametreleri DbSet + model config
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs — KampParametre eklendi + column degisikligi
- backend/Program.cs — IKampParametreService kaydi
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts — property rename
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru.ts — form control rename
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru.html — label + baslik 2026

### Riskler
- `ResolveKonaklama()` hala code-based (tesis adi + birim tipi match). Konaklama parametreleri DB'ye seed edildi ama henuz servisten okunmuyor — bu sabitler nadir degisir, gelecekte ihtiyac olursa baglanti kurulabilir.
- Mevcut 2025 donemi basvurulari icin `Kamp2023tenFaydalandiMi` datasi drop ediliyor — bunlar artik 2025 yili icin tarihsel olarak irrelevant.
- KampParametreService cache'i scoped (request bazli). Cok sik parametre degisikligi olursa singleton + invalidation gerekebilir.

### Build Sonuclari (Tur 5)
- Backend: BASARILI (0 hata, 0 uyari)
- Frontend: BASARILI

---

## Tur 6 - Kamp Basvuru Kurallarinin Dinamiklestirilmesi

### Yapilan Degisiklikler
- `KampBasvuru` icindeki statik `Kamp2024tenFaydalandiMi` / `Kamp2025tenFaydalandiMi` alanlari kaldirildi.
- Yeni `KampBasvuruSahibi` yapisi eklendi; kamp basvurulari artik `KampBasvuruSahibiId` ile baglaniyor.
- Gecmis kamp katilimlari icin dinamik `KampBasvuruGecmisKatilimlari` tablosu eklendi.
- Kamp yilina gore puan kirma penceresini belirleyen `KampKuralSetleri` tablosu eklendi.
- Basvuru sahibi tipi, katilimci tipi ve akrabalik tipi lookup’lari DB’ye tasindi:
  - `KampBasvuruSahibiTipleri`
  - `KampKatilimciTipleri`
  - `KampAkrabalikTipleri`
- `KampPuanlamaService` artik:
  - aktif `KampKuralSeti` kaydini okur
  - basvuru sahibi tipini DB lookup’tan bulur
  - basvuru yilina gore gecmis yil penceresini hesaplar
  - kullanicinin secimi + ayni TC’ye ait mevcut gecmis katilimlarini birlestirerek puan kirar
- `KampUcretHesaplamaService` icindeki kamu tarife karari DB lookup’tan okunur.
- `KampBasvuruService` icinde:
  - basvuru sahibinin TC’si ile `KampBasvuruSahibi` resolve edilir
  - secili/bulunan gecmis katilim yillari birlestirilir
  - create sirasinda eksik gecmis yil kayitlari dinamik tabloya yazilir
- Kamp basvuru baglami genisletildi:
  - `basvuruSahibiTipleri`
  - `katilimciTipleri`
  - `akrabalikTipleri`
  - donem bazli dinamik `gecmisKatilimYillari`
- Frontend kamp basvuru ekrani artik:
  - tip listelerini backend baglamindan alir
  - statik 2024/2025 checkbox’lari yerine doneme gore dinamik gecmis yil checkbox’lari gosterir
  - preview sonucundaki birlesik gecmis yil bilgisini forma yansitir

### Yeni Dosyalar
- backend/Kamp/Entities/KampBasvuruSahibi.cs
- backend/Kamp/Entities/KampBasvuruGecmisKatilim.cs
- backend/Kamp/Entities/KampKuralSeti.cs
- backend/Kamp/Entities/KampBasvuruSahibiTipi.cs
- backend/Kamp/Entities/KampKatilimciTipi.cs
- backend/Kamp/Entities/KampAkrabalikTipi.cs
- backend/Infrastructure/EntityFramework/Migrations/20260405193000_DynamicKampBasvuruKurallari.cs

### Degisen Dosyalar
- backend/Kamp/Entities/KampBasvuru.cs
- backend/Kamp/Entities/KampBasvuruKatilimci.cs
- backend/Kamp/Dto/KampBasvuruBaglamDto.cs
- backend/Kamp/Dto/KampBasvuruDto.cs
- backend/Kamp/Dto/KampBasvuruKatilimciDto.cs
- backend/Kamp/Dto/KampBasvuruOnizlemeDto.cs
- backend/Kamp/Dto/KampBasvuruRequestDto.cs
- backend/Kamp/KampBasvuruKurallari.cs
- backend/Kamp/Services/IKampPuanlamaService.cs
- backend/Kamp/Services/IKampUcretHesaplamaService.cs
- backend/Kamp/Services/KampBasvuruService.cs
- backend/Kamp/Services/KampPuanlamaService.cs
- backend/Kamp/Services/KampUcretHesaplamaService.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru.html
- changes.md

### Build Sonuclari (Tur 6)
- Backend: BASARILI
- Frontend: BASARILI

### Notlar / Riskler
- `dotnet ef migrations has-pending-model-changes` denemesi sandbox’taki `dotnet ef` tasarim-zamani build davranisi nedeniyle dogrulanamadi; runtime build basarili.
- Bu turda lookup tablolarina yonetim CRUD eklenmedi; seed + runtime okuma modeli kullaniliyor.
- Gecmis katilimlar su an kullanici beyani + mevcut sahibin kayitlari uzerinden tutuluyor; gercek kamp kullanim/veri dogrulamasi baglantisi ileride ayrica guclendirilebilir.

---

## Tur 7 - KampBasvuru / KampBasvuruSahibi Ayrimini Netlestirme

### Yapilan Degisiklikler
- `KampBasvuru` icindeki kisi profiline donuk alanlar acik snapshot alanlarina donusturuldu:
  - `BasvuruSahibiAdiSoyadi` -> `BasvuruSahibiAdiSoyadiSnapshot`
  - `BasvuruSahibiTipi` -> `BasvuruSahibiTipiSnapshot`
  - `HizmetYili` -> `HizmetYiliSnapshot`
- `KampBasvuruSahibi` profiline tasinan alanlar:
  - `BasvuruSahibiTipi`
  - `HizmetYili`
- `AdSoyad` zaten sahip entity’sinde oldugu icin ayni isimle profil kaynagi olarak korunuyor.
- `KampBasvuruService` icinde:
  - sahip resolve edilirken profil alanlari artik `KampBasvuruSahibi` uzerinde guncelleniyor
  - yeni basvuru olusturulurken ayni veriler snapshot alanlarina da yaziliyor
  - DTO map’leri snapshot alanlardan donmeye devam ediyor, bu nedenle mevcut UI kirilmadi
- Tahsis ve rezervasyon akislari, basvuru icindeki yeni snapshot alanlarini kullanacak sekilde guncellendi.
- Yeni migration ile mevcut veride:
  - sahip tablosuna yeni profil kolonlari ekleniyor
  - mevcut basvurulardan sahip profili backfill ediliyor
  - basvuru kolonlari snapshot adlarina rename ediliyor

### Yeni Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260406090000_SeparateKampBasvuruSahibiProfile.cs

### Degisen Dosyalar
- backend/Kamp/Entities/KampBasvuru.cs
- backend/Kamp/Entities/KampBasvuruSahibi.cs
- backend/Kamp/Services/KampBasvuruService.cs
- backend/Kamp/Services/KampTahsisService.cs
- backend/Kamp/Services/KampRezervasyonService.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- tests/STYS.Tests/KampTahsisServiceTests.cs
- changes.md

### Build Sonuclari (Tur 7)
- Backend: BASARILI
- Frontend: BASARILI

### Notlar / Riskler
- DTO adlari geriye donuk uyumluluk icin korunuyor; acik snapshot adlandirmasi entity/migration katmaninda yapildi.
- Sahip profili backfill’i, mevcut basvurular arasindan en guncel kayda gore dolduruluyor. Farkli tarihsel basvurularda tip/hizmet yili degismisse sahip profili son durumu temsil edecek.

## Tur 7.1 - Kamp Migration Metadata Tamamlama

### Yapilanlar
- Manuel eklenen kamp migration’lari icin eksik `.Designer.cs` dosyalari olusturuldu.
- `DbContext` ve `Migration` attribute tanimlari, `BuildTargetModel` govdeleriyle birlikte migration metadata’si tamamlandi.
- `StysAppDbContextModelSnapshot.cs` icindeki kamp rezervasyon alan adlari mevcut `DbContext` ile hizalandi.

### Yeni Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260405193000_DynamicKampBasvuruKurallari.Designer.cs
- backend/Infrastructure/EntityFramework/Migrations/20260406090000_SeparateKampBasvuruSahibiProfile.Designer.cs

### Degisen Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- changes.md

### Build Sonuclari (Tur 7.1)
- Backend: BASARILI
- Frontend: BASARILI

### Notlar / Riskler
- Bu turda sadece migration metadata ve snapshot uyumu duzeltildi; runtime davranisinda yeni is kurali degisikligi yok.

## Tur 7.2 - KampBasvuru User Shortcut Sadelestirme

### Yapilanlar
- `KampBasvuru` icindeki `BasvuruSahibiUserId` alani kaldirildi.
- "Benim basvurularim" ve ayni aile tekrar basvuru kontrolleri `KampBasvuruSahibi.UserId` uzerinden calisacak sekilde tasindi.
- Basvuru olusturma akisi login zorunlulugundan cikarildi; anonim kullanici da kamp basvurusu olusturabilir hale geldi.
- `KampBasvuruSahibi.UserId` geri dolumu icin migration eklendi, mevcut `KampBasvurulari.BasvuruSahibiUserId` verisi dusurulmeden once sahip profiline tasiniyor.

### Yeni Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260406094500_RemoveKampBasvuruUserShortcut.cs
- backend/Infrastructure/EntityFramework/Migrations/20260406094500_RemoveKampBasvuruUserShortcut.Designer.cs

### Degisen Dosyalar
- backend/Kamp/Entities/KampBasvuru.cs
- backend/Kamp/Services/KampBasvuruService.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs

### Build Sonuclari (Tur 7.2)
- Backend: BASARILI
- Frontend: CALISTIRILMADI

### Notlar / Riskler
- Anonim ve TC'siz basvurularda "ayni aile tek basvuru" kontrolu ancak kayitli sahip baglami kurulabildigi durumda calisir; en guclu esleme halen TC kimlik no uzerinden saglaniyor.

## Tur 7.3 - Kamp Basvuru No ve Public Sorgu

### Yapilanlar
- Her kamp basvurusu icin benzersiz `BasvuruNo` uretilmeye baslandi.
- `KampBasvuruDto` ve frontend modellerine `basvuruNo` eklendi; basvuru kaydi sonrasi kullaniciya bu numara donduruluyor.
- `KampBasvuruController` icinde `baglam`, `onizleme`, `basvuru olusturma` ve `basvuru-no ile sorgulama` endpoint'leri anonim kullanima acildi.
- `kamp-basvurusu` route'u auth guard disina alindi; ekran public erisilebilir hale geldi.
- Kamp basvuru ekranina "Basvuru Sorgula" alani eklendi. Kullanici login olmadan basvuru numarasi ile durumunu kontrol edebiliyor.
- Eski kayitlar icin `BasvuruNo` backfill eden migration eklendi ve benzersiz index tanimlandi.

### Yeni Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260406103000_AddKampBasvuruNoAndPublicTracking.cs
- backend/Infrastructure/EntityFramework/Migrations/20260406103000_AddKampBasvuruNoAndPublicTracking.Designer.cs

### Degisen Dosyalar
- backend/Kamp/Entities/KampBasvuru.cs
- backend/Kamp/Dto/KampBasvuruDto.cs
- backend/Kamp/Services/IKampBasvuruService.cs
- backend/Kamp/Services/KampBasvuruService.cs
- backend/Kamp/Controllers/KampBasvuruController.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- frontend/src/app.routes.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.service.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru.html
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru.scss
- changes.md

### Build Sonuclari (Tur 7.3)
- Backend: BASARILI
- Frontend: BASARILI

### Notlar / Riskler
- `BasvuruNo` ile public sorgu, numarayi bilen kisiye basvuru detayini gosterebilir. Daha siki mahremiyet istenirse ikinci bir dogrulama alanı (ornegin TC son 4 hane veya dogum tarihi) eklenmeli.

## Tur 8 - Alata/Foca Ozel Akislarin Genericlestirilmesi

### Yapilanlar
- Kamp konaklama birimi kurali tesis adina bagli `contains("alata"/"foca")` kontrolunden cikarildi.
- Konaklama konfigurasyonu artik `KampParametreleri` icindeki `Konaklama.<BirimKodu>.*` kayitlarindan dinamik okunuyor.
- Kamp basvuru baglami icindeki birim listesi artik parametre tablosundan otomatik uretiliyor; yeni tesis/birim eklemede kod degisikligi gerekmiyor.
- `IKampParametreService` genisletildi:
  - `GetString(kod, defaultValue)`
  - `GetByPrefix(prefix)`
- Kamp migration'larindaki tesis adi bazli Alata/Foca atama SQL'i generic hale getirildi; aktif tum tesisler donemlere otomatik atanacak sekilde duzenlendi.
- `20260405180000_AddKampParametreAndUpdateYears` icindeki konaklama parametre seed anahtarlari Alata/Foca adlarindan bagimsiz birim kodlarina cekildi (`Standart34`, `Prefabrik45`, `Otel45`, `Betonarme45`).
- Kamp testlerindeki Alata/Foca bagimli birim kodlari ve fixture adlari genericlestirildi.

### Degisen Dosyalar
- backend/Kamp/KampBasvuruKurallari.cs
- backend/Kamp/KampParametreKodlari.cs
- backend/Kamp/Services/IKampParametreService.cs
- backend/Kamp/Services/KampParametreService.cs
- backend/Kamp/Services/KampBasvuruService.cs
- backend/Kamp/Services/KampUcretHesaplamaService.cs
- backend/Infrastructure/EntityFramework/Migrations/20260404195000_Seed2025YazKampiData.cs
- backend/Infrastructure/EntityFramework/Migrations/20260405180000_AddKampParametreAndUpdateYears.cs
- tests/STYS.Tests/KampKurallariTests.cs
- tests/STYS.Tests/KampTahsisServiceTests.cs
- changes.md

### Build Sonuclari (Tur 8)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

### Test Notu
- `dotnet test tests/STYS.Tests/STYS.Tests.csproj --no-build` calistirildi; mevcut projede kamp disi rezervasyon testlerinde halihazirda bulunan hatalar nedeniyle genel test paketi basarisiz.
- Kamp odakli filtreli test calistirma denemesinde ortam kaynakli `NU1900` (nuget vulnerability index erisim) uyarisi nedeniyle test kosumu tamamlanamadi.

## Tur 9 - Sidebar Ana Basliklari Acilir/Kapanir Yapisi

### Yapilanlar
- Sol menude root seviye ana basliklar (`ANA MENU`, `TESIS`, `ISLETME`, `KAMP YONETIMI` vb.) acilir/kapanir hale getirildi.
- Varsayilan davranis: root basliklar kapali gelir.
- Aktif route'un bulundugu grup otomatik acik kalir (route gorunurlugu korunur).
- Root baslik satiri tiklanabilir toggle butonuna cevrildi; acik/kapali durum icin yukari/asagi ok gosterimi eklendi.
- Root grup genisleme durumu layout state icine alindi (`expandedRootPaths`) ve servis seviyesinde yonetildi.

### Degisen Dosyalar
- frontend/src/app/layout/service/layout.service.ts
- frontend/src/app/layout/component/app.menuitem.ts
- frontend/src/app/layout/component/app.menuitem.scss
- changes.md

### Build Sonuclari (Tur 9)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

### Notlar
- Backend build'de bu turdan bagimsiz mevcut warningler korunuyor (`20260406091119_duzeltme` isimlendirme warning).

## Tur 10 - Kamp Puan Kurali Yonetim Ekranlari

### Yapilanlar
- Kamp puan hesaplama kurallarini yonetmek icin yeni backend API eklendi:
  - `GET /ui/kamppuankurali/yonetim-baglam`
  - `PUT /ui/kamppuankurali/yonetim-baglam`
- API kapsaminda su alanlar yonetilebilir hale getirildi:
  - `KatilimciBasinaPuan` (`KampParametreleri`)
  - `KampKuralSetleri` (kamp yili, onceki yil sayisi, katilim ceza puani, aktiflik)
  - `KampBasvuruSahibiTipleri` (kod, ad, oncelik, taban puan, hizmet yili puani aktifligi, emekli bonus, varsayilan katilimci tipi, aktiflik)
- Yeni frontend sayfasi eklendi: `kamp-puan-kurallari`
  - Inline duzenleme ile kural seti ve basvuru sahibi tipi ekleme/guncelleme/satir silme
  - `KatilimciBasinaPuan` parametresi duzenleme
  - Tek adimda kaydetme
- Kamp Programlari ve Kamp Donemleri ekranlarina "Puan Kurallari" hizli gecis butonu eklendi.
- Yeni route eklendi:
  - `/kamp-puan-kurallari` (breadcrumb: Isletme > Kamp Yonetimi > Puan Kurallari)

### Yeni Dosyalar
- backend/Kamp/Dto/KampPuanKuraliYonetimDto.cs
- backend/Kamp/Services/IKampPuanKuraliYonetimService.cs
- backend/Kamp/Services/KampPuanKuraliYonetimService.cs
- backend/Kamp/Controllers/KampPuanKuraliController.cs
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.html
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.scss

### Degisen Dosyalar
- backend/Program.cs
- frontend/src/app.routes.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.service.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-programi-tanim-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-programi-tanim-yonetimi.html
- frontend/src/app/pages/kamp-yonetimi/kamp-donemi-tanim-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-donemi-tanim-yonetimi.html
- changes.md

### Build Sonuclari (Tur 10)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

### Notlar
- Yetkilendirme icin mevcut kamp tanim yonetimi yetkileri kullanildi (yeni permission/migration eklenmedi).

## Tur 11 - Menu Bazli Ayrik Yetkilendirme (Kamp Puan Kurallari)

### Yapilanlar
- `Kamp Puan Kurallari` ekrani icin paylasimli kamp tanim yetkileri birakildi; menuye ozel yeni yetki grubu tanimlandi:
  - `KampPuanKuraliYonetimi.Menu`
  - `KampPuanKuraliYonetimi.View`
  - `KampPuanKuraliYonetimi.Manage`
- Erisim tesisi modullerine `/kamp-puan-kurallari` route'u menuye ozel yetki seti ile eklendi.
- API endpoint yetkilendirmeleri menuye ozel izinlere cekildi:
  - `GET /ui/kamppuankurali/yonetim-baglam` -> `KampPuanKuraliYonetimi.View`
  - `PUT /ui/kamppuankurali/yonetim-baglam` -> `KampPuanKuraliYonetimi.Manage`
- Frontend tarafinda sayfa ve hizli erisim butonlari yeni menuye ozel izin adlari ile guncellendi; manage yetkisi olmayan kullanicida duzenleme kontrolleri read-only oldu.
- Yeni migration eklendi:
  - `20260406120000_AddKampPuanKuraliPermissionsAndMenu`
  - TODBase tarafinda role/menu/menu-role seedleri eklendi.

### Degisen Dosyalar
- backend/StructurePermissions.cs
- backend/ErisimTeshis/ErisimTeshisModulTanimlari.cs
- backend/Kamp/Controllers/KampPuanKuraliController.cs
- backend/Infrastructure/EntityFramework/Migrations/20260406120000_AddKampPuanKuraliPermissionsAndMenu.cs
- frontend/src/app/pages/kamp-yonetimi/kamp-programi-tanim-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-donemi-tanim-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.html
- changes.md

### Build Sonuclari (Tur 11)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

### Notlar
- `ErisimTeshisModulTanimlari` icin yapilan kontrolde modul satiri sayisi ve tekil permission grubu sayisi esit (27/27); yani mevcut modul/menu yapisi genel olarak menu-bazli ayrik izin prensibine uygun.

## Tur 12 - Kamp Puan Kurallari Program Bazli Hale Getirildi

### Yapilanlar
- Kamp puanlama kural seti secimi `kamp yili` yerine `kamp programi + kamp yili` kombinasyonuna cevrildi.
- `KampKuralSeti` modeline `KampProgramiId` eklendi ve kural seti benzersizlik kuralı `(KampProgramiId, KampYili)` olacak sekilde guncellendi.
- Basvuru onizleme/validasyon akisinda ilgili donemin programina ait aktif kural seti kullanilmaya baslandi.
- Puan kurali yonetim baglamina `Programlar` listesi eklendi.
- Puan kurali ekraninda kural seti satirlarina `Kamp Programi` kolonu eklendi; yeni satirlar program secimi ile olusturuluyor.
- Yeni migration ile mevcut yil bazli kural setleri program bazina tasindi:
  - Aktif programlar icin mevcut satirlar program bazinda cogaltildi,
  - eski (programsiz) satirlar temizlendi,
  - FK + unique index eklendi.

### Yeni Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260406170000_MakeKampKuralSetleriProgramScoped.cs

### Degisen Dosyalar
- backend/Kamp/Entities/KampKuralSeti.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- backend/Kamp/Services/IKampPuanlamaService.cs
- backend/Kamp/Services/KampPuanlamaService.cs
- backend/Kamp/Services/KampBasvuruService.cs
- backend/Kamp/Dto/KampPuanKuraliYonetimDto.cs
- backend/Kamp/Services/KampPuanKuraliYonetimService.cs
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.html
- tests/STYS.Tests/KampKurallariTests.cs
- changes.md

### Build Sonuclari (Tur 12)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

### Test Notu
- `dotnet test tests/STYS.Tests/STYS.Tests.csproj --no-build --filter "FullyQualifiedName~KampKurallariTests"` kosuldu.
- Sonuc: 3 testten 2'si basarili, 1'i basarisiz (`UcretHesaplama_CocukKurallariVeEkHizmetleriUygular` beklenen/gerceklesen tutar farki; bu turdaki program bazli kural seti degisikliginden bagimsiz gorunuyor).

## Tur 13 - Global Baslik + Programa Bagli Puanlama Kurallari (Minimum Mudahale)

### Hedef
- Basvuru sahibi tipleri global sozluk olarak kalsin.
- Her kamp programi icin bu global tiplerden secilerek farkli puanlama kurali tanimlanabilsin.
- Katilimci basina puan degeri global tek parametre yerine program/yil kural setinde tanimlanabilsin.

### Yapilanlar
- Yeni entity/tablo eklendi: `KampProgramiBasvuruSahibiTipKurallari`
  - Program + global basvuru sahibi tipi bazinda puanlama alanlari tutuluyor:
    - OncelikSirasi, TabanPuan, HizmetYiliPuaniAktifMi, EmekliBonusPuani, VarsayilanKatilimciTipiKodu, AktifMi
- `KampKuralSeti` genisletildi:
  - `KatilimciBasinaPuan` alani eklendi (artik kural seti satirinda)
- Puanlama servisi guncellendi:
  - Basvuru tipi once global sozlukten bulunuyor (kod ile),
  - Ardindan secili programa ait tip-kural kaydindan puanlama degerleri aliniyor,
  - Katilimci basina puan kural setinin kendi alanindan aliniyor.
- Kamp puan kurali yonetim baglami/saklama akisi guncellendi:
  - Global tip listesi ayri donuyor,
  - Program + tip kural satirlari ayri yonetiliyor,
  - Kural setinde `KatilimciBasinaPuan` kolon bazinda kaydediliyor.
- Frontend puan kurali ekrani guncellendi:
  - Basvuru sahibi tipi kurallarinda serbest kod/ad girisi kaldirildi,
  - Program secimi + global tip secimi ile kural satiri olusturma modeline gecildi,
  - Kural seti tablosuna `Katilimci Basina Puan` kolonu eklendi.

### Yeni Dosyalar
- backend/Kamp/Entities/KampProgramiBasvuruSahibiTipKurali.cs
- backend/Infrastructure/EntityFramework/Migrations/20260406190000_AddProgramScopedBasvuruSahibiTipKurallari.cs

### Degisen Dosyalar
- backend/Kamp/Entities/KampKuralSeti.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- backend/Kamp/Services/KampPuanlamaService.cs
- backend/Kamp/Services/KampPuanKuraliYonetimService.cs
- backend/Kamp/Dto/KampPuanKuraliYonetimDto.cs
- backend/Kamp/Dto/KampBasvuruBaglamDto.cs
- backend/Kamp/Services/KampBasvuruService.cs
- tests/STYS.Tests/KampKurallariTests.cs
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.html
- changes.md

### Build Sonuclari (Tur 13)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

### Test Notu
- Hedefli test: `KampKurallariTests.Puanlama_TarimOrmanPersoneliIcinTalimatPuanlariniUygular` BASARILI.

## Tur 14 - Kamp Puan Kurali Ekrani Dropdown Gorunumu Duzeltmesi

### Yapilanlar
- Ekrandaki native/uyumsuz dropdown gorunumu duzeltildi.
- `kamp-puan-kurali-yonetimi` sayfasindaki secimler `p-dropdown` yerine PrimeNG `p-select` ile standart hale getirildi.
- Program, Global Tip ve Varsayilan Katilimci Tipi alanlarina `appendTo='body'` eklendi (tablo icinde overlay kesilmesini onlemek icin).
- Global tip secenegi icin okunabilir etiket formatina gecildi: `Ad (Kod)`.
- Tablo icinde `p-select` icin genislik/min-width stili eklendi.

### Degisen Dosyalar
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.html
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.scss
- changes.md

### Build Sonuclari (Tur 14)
- Frontend: BASARILI (`npm run build`)

## Tur 15 - Donem Etiketlerini Program / Donem Formatina Getirme

### Yapilanlar
- Kamp baglam DTO'larina donem secenekleri icin `KampProgramiAd` alani eklendi:
  - Basvuru, Tahsis, Rezervasyon baglamlari.
- Backend'de ilgili baglam sorgulari program adini da donecek sekilde guncellendi.
- Frontend'de donem dropdown etiketleri `Program / Donem` formatina cekildi:
  - Kamp Basvuru
  - Kamp Tahsis
  - Kamp Rezervasyon
  - Kamp Iade

### Degisen Dosyalar
- backend/Kamp/Dto/KampBasvuruBaglamDto.cs
- backend/Kamp/Dto/KampTahsisBaglamDto.cs
- backend/Kamp/Dto/KampRezervasyonDto.cs
- backend/Kamp/Services/KampBasvuruService.cs
- backend/Kamp/Services/KampTahsisService.cs
- backend/Kamp/Services/KampRezervasyonService.cs
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru.html
- frontend/src/app/pages/kamp-yonetimi/kamp-tahsis-yonetimi.html
- frontend/src/app/pages/kamp-yonetimi/kamp-rezervasyonlari.html
- frontend/src/app/pages/kamp-yonetimi/kamp-iade-yonetimi.html
- changes.md

### Build Sonuclari (Tur 15)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur 16 - Kamp Donemi Tesis Atamasi Ekraninda Toplu Donem Uygulamasi

### Yapilanlar
- `Kamp Donemi Tesis Atamasi` ekranina toplu uygulama akisi eklendi.
- Secili donemin mevcut tesis atama konfigurasyonu, birden fazla doneme tek seferde uygulanabilir hale getirildi.
- Yeni UI alanlari:
  - `Toplu Uygulama Donemleri` (coklu secim)
  - `Ayni Program+Yil Donemlerini Sec` hizli secim butonu
  - `Secili Donemlere Toplu Uygula` kaydetme butonu
- Is kurali:
  - Toplu aday listesi, secili donem ile ayni `program + yil` kapsamindaki donemlerden otomatik uretilir.
  - Toplu kayit, secili hedef donemlerin tamamina ayni tesis atama payload'ini gonderir.

### Degisen Dosyalar
- frontend/src/app/pages/kamp-yonetimi/kamp-donemi-atama-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-donemi-atama-yonetimi.html
- frontend/src/app/pages/kamp-yonetimi/kamp-donemi-atama-yonetimi.scss
- changes.md

### Build Sonuclari (Tur 16)
- Frontend: BASARILI (`npm run build`)

## Tur 17 - Kamp Sezonu Bazli Filtreleme (Donem Atama Ekrani)

### Yapilanlar
- `Kamp Donemi Tesis Atamasi` ekranina `Kamp Sezonu` secimi eklendi.
- Ekran akisina sezon -> donem hiyerarsisi getirildi:
  - Sezon secilince donem dropdown'i otomatik filtrelenir.
  - Toplu uygulama donem listesi de ayni sezon kapsamiyla filtrelenir.
  - Sezon degisince secili donem ve atama tablosu yeni filtreye gore otomatik yenilenir.
- Toplu uygulama ozelligi korunup sezona baglandi.

### Degisen Dosyalar
- frontend/src/app/pages/kamp-yonetimi/kamp-donemi-atama-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-donemi-atama-yonetimi.html
- frontend/src/app/pages/kamp-yonetimi/kamp-donemi-atama-yonetimi.scss
- changes.md

### Build Sonuclari (Tur 17)
- Frontend: BASARILI (`npm run build`)

## Tur 18 - Donem Atama Ekraninda Tek Donem Dropdown'ina Gecis

### Yapilanlar
- `Kamp Donemi` tekli dropdown kaldirildi.
- Ekranda sadece bir adet donem secim kontrolu birakildi: `Kamp Donemleri (Coklu Secim)`.
- Bu coklu secim artik hem:
  - duzenlenecek kaynak donemi (ilk secili donem),
  - hem de toplu uygulama hedef donemlerini
  belirliyor.
- Coklu secim bosalsa bile alan gizlenmiyor (yetkisi olan kullanici tekrar secim yapabiliyor).

### Degisen Dosyalar
- frontend/src/app/pages/kamp-yonetimi/kamp-donemi-atama-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-donemi-atama-yonetimi.html
- frontend/src/app/pages/kamp-yonetimi/kamp-donemi-atama-yonetimi.scss
- changes.md

### Build Sonuclari (Tur 18)
- Frontend: BASARILI (`npm run build`)

## Tur 19 - Cocuk Ucretlendirmesinde Parametreden Yas Kurali Tablosuna Gecis

### Yapilanlar
- Cocuk ucretlendirme kurali, sabit tarih parametresi yerine kamp tarihindeki yasi esas alacak sekilde guncellendi.
- Yeni tablo eklendi: `KampYasUcretKurallari`
  - `UcretsizCocukMaxYas`
  - `YarimUcretliCocukMaxYas`
  - `YemekOrani`
  - `AktifMi`
- Ucret hesaplama akisinda:
  - aktif yas-kurali kaydi veritabanindan okunuyor,
  - katilimci yasi `kampDonemi.KonaklamaBaslangicTarihi` referans alinarak hesaplaniyor,
  - kural yoksa fallback olarak kod tarafindaki varsayilan degerler kullaniliyor.
- Migration ile varsayilan bir aktif kural kaydi seed edildi (`2 / 6 / 0.50`).

### Degisen Dosyalar
- backend/Kamp/Entities/KampYasUcretKurali.cs
- backend/Kamp/KampBasvuruKurallari.cs
- backend/Kamp/Services/KampUcretHesaplamaService.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Infrastructure/EntityFramework/Migrations/20260406200416_AddKampYasUcretKurallari.cs
- backend/Infrastructure/EntityFramework/Migrations/20260406200416_AddKampYasUcretKurallari.Designer.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- changes.md

### Build Sonuclari (Tur 19)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)

## Tur 20 - Yas/Ucret Kurallarini Kamp Puan Kurallari Ekranina Ekleme

### Yapilanlar
- `Kamp Puan Kurallari` ekranina yeni bir global bolum eklendi:
  - `Ucretsiz Cocuk Max Yas`
  - `Yarim Ucretli Cocuk Max Yas`
  - `Yemek Orani (0-1)`
  - `Aktif`
- Bu alanlar mevcut `yonetim-baglam` GET/PUT akisi icine dahil edildi:
  - baglam okunurken aktif yas/ucret kurali ekrana geliyor,
  - kaydetmede ayni istekle yas/ucret kurali da guncelleniyor.
- Backend validasyonlari eklendi:
  - yas alanlari `0-18`,
  - `yarim max yas >= ucretsiz max yas`,
  - `yemek orani 0.00-1.00`.
- Kayit mantigi:
  - mevcut kayit varsa update,
  - yoksa yeni kayit olustur.

### Degisen Dosyalar
- backend/Kamp/Dto/KampPuanKuraliYonetimDto.cs
- backend/Kamp/Services/KampPuanKuraliYonetimService.cs
- backend/Kamp/KampValidasyonKurallari.cs
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.html
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.scss
- changes.md

### Build Sonuclari (Tur 20)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur 21 - Konaklama Birimlerini Tesis Binalarindan Uretme

### Yapilanlar
- Kamp basvuru baglaminda `Konaklama Birimi` listesi, artik global parametre listesi yerine secili tesisin aktif binalarindan uretiliyor.
- Her bina icin aktif odalarin oda tipi kapasiteleri okunup min/max kapasite hesaplandi.
- Hesaplanan kapasite araligina gore mevcut konaklama ucret parametrelerinden uygun konfigurasyon eslestirildi.
- Basvuru dogrulama ve ucret hesaplama akisinda:
  - once geriye donuk destek icin direkt parametre kodu cozumleniyor,
  - olmazsa secili tesis + bina adi uzerinden kapasite esleme ile konaklama konfigurasyonu bulunuyor.
- Bu sayede ekrandaki secim kaynagi bina bazli hale geldi; mevcut fiyat hesaplama akisi da korunmus oldu.

### Degisen Dosyalar
- backend/Kamp/Services/KampBasvuruService.cs
- backend/Kamp/Services/KampUcretHesaplamaService.cs
- changes.md

### Build Sonuclari (Tur 21)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)

## Tur 22 - Konaklama Parametrelerini Tabloya Tasima (Ilk Faz)

### Yapilanlar
- `Konaklama.*` parametreleri yerine yeni global tablo eklendi: `KampKonaklamaTarifeleri`.
- Yeni tablo alani:
  - `Kod`, `Ad`
  - `MinimumKisi`, `MaksimumKisi`
  - `KamuGunlukUcret`, `DigerGunlukUcret`
  - `BuzdolabiGunlukUcret`, `TelevizyonGunlukUcret`, `KlimaGunlukUcret`
  - `AktifMi`
- Migration icinde otomatik veri tasima eklendi:
  - mevcut `KampParametreleri` icindeki `Konaklama.<Kod>.<Alan>` kayitlari parse edilerek yeni tabloya insert ediliyor.
- Kamp basvuru baglami ve ucret/cozumleme akislari yeni tabloyu kullanacak sekilde guncellendi.
- Geriye donuk uyumluluk:
  - Eski kayitlarda `KonaklamaBirimiTipi` alaninda tarifenin eski kodu varsa (`Standart34` vb.), yeni tabloda `Kod` alanindan cozumlenmeye devam ediyor.

### Degisen Dosyalar
- backend/Kamp/Entities/KampKonaklamaTarifesi.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Infrastructure/EntityFramework/Migrations/20260406203256_AddKampKonaklamaTarifeleri.cs
- backend/Infrastructure/EntityFramework/Migrations/20260406203256_AddKampKonaklamaTarifeleri.Designer.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- backend/Kamp/Services/KampBasvuruService.cs
- backend/Kamp/Services/KampUcretHesaplamaService.cs
- changes.md

### Build Sonuclari (Tur 22)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)

## Tur 23 - Kamp Donemleri Bos Gelme Hotfix'i

### Yapilanlar
- `Kamp donemleri dolmuyor` problemi icin gecis fallback'i tamamlandi.
- Neden:
  - `KampKonaklamaTarifeleri` tablosu henuz dolu degilse baglam uretilirken konaklama birimleri bos kaliyor ve donem listesi filtreleniyordu.
- Cozum:
  - `KampBasvuruService` icindeki fallback'e ek olarak,
  - `KampUcretHesaplamaService` icinde de ayni fallback eklendi.
  - Boyeclikle yeni tablo bos olsa bile eski `Konaklama.*` parametrelerinden gecici olarak konaklama tarifeleri okunuyor.

### Degisen Dosyalar
- backend/Kamp/Services/KampUcretHesaplamaService.cs
- changes.md

### Build Sonuclari (Tur 23)
- Backend build bu turda dogrulanamadi (calisan `STYS` process'i DLL lock nedeniyle `dotnet build` kopyalama asamasinda durdu).

## Tur 24 - Kamp Donemleri Bos Gelme Icin Ek Fallback

### Yapilanlar
- Donem listesinin hala bos gelmesi senaryosu icin ikinci fallback eklendi.
- `KampBasvuruService.BuildBirimler`:
  - Tesisin bina/oda eslesmesinden birim cikmazsa, gecis doneminde aktif konaklama tarifelerini direkt secenek olarak dondurur.
- Bu sayede tesis tarafi henuz tam modellenmemis ortamlarda da donem/birim secimleri bos kalmaz.

### Degisen Dosyalar
- backend/Kamp/Services/KampBasvuruService.cs
- changes.md

### Build Sonuclari (Tur 24)
- Backend build bu turda da dogrulanamadi (calisan `STYS` process'i DLL lock nedeniyle `dotnet build` kopyalama asamasinda durdu).

## Tur 25 - Kamp Basvurusunda Konaklama Birimini Dogrudan Bina Adindan Gosterme

### Yapilanlar
- `Kamp Basvurusu` ekranindaki `Konaklama Birimi` secenekleri icin eski tarife/fallback adlari kaldirildi.
- `KampBasvuruService.BuildBirimler` artik secenekleri sadece secili tesisin aktif bina adlarindan uretiyor.
- Boyeclikle dropdown'da `Alata/Foca` gibi eski parametre kaynakli adlar yerine dogrudan tesisin bina adlari listelenir.

### Degisen Dosyalar
- backend/Kamp/Services/KampBasvuruService.cs
- changes.md

### Build Sonuclari (Tur 25)
- Backend build bu turda yeniden kosulmadi (ortamda calisan `STYS` process lock riski var).

## Tur 26 - Kamp Parametrelerini Program Bazinda Ayrisma (Avans/Iade/No-Show)

### Yapilanlar
- Program bazli ayar tablosu eklendi: `KampProgramiParametreAyarlari`
  - `KamuAvansKisiBasi`
  - `DigerAvansKisiBasi`
  - `VazgecmeIadeGunSayisi`
  - `GecBildirimGunlukKesintiyUzdesi`
  - `NoShowSuresiGun`
- Migration ile aktif kamp programlari icin baslangic kayitlari otomatik olusturuldu.
  - Degerler mevcut global `KampParametreleri` kayitlarindan alinir.
- Ucret hesaplama servisinde avans tutarlari artik `kampProgramiId` bazli okunuyor.
  - Sira: `program ayari` -> `global parametre` -> `kod default`.
- Tahsis servisinde no-show gunu artik `kampProgramiId` bazli okunuyor.
  - Sira: `program ayari` -> `global parametre`.
- Iade servisinde vazgecme gunu ve gec bildirim kesintisi program bazli okunuyor.
  - `KampIadeHesaplamaRequestDto` icine `KampDonemiId` eklendi.
  - Frontend iade hesaplama cagrisi bu alanı gonderir hale getirildi.
  - Sira: `program ayari` -> `global parametre`.

### Degisen Dosyalar
- backend/Kamp/Entities/KampProgramiParametreAyari.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Infrastructure/EntityFramework/Migrations/20260407083248_AddKampProgramiParametreAyarlari.cs
- backend/Infrastructure/EntityFramework/Migrations/20260407083248_AddKampProgramiParametreAyarlari.Designer.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- backend/Kamp/Services/KampUcretHesaplamaService.cs
- backend/Kamp/Services/KampTahsisService.cs
- backend/Kamp/Services/KampIadeService.cs
- backend/Kamp/Dto/KampIadeHesaplamaRequestDto.cs
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-iade-yonetimi.ts
- changes.md

### Build Sonuclari (Tur 26)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur 27 - Kamp Programina Gore Kural Duzenleme Alani Eklendi

### Yapilanlar
- `Kamp Puan Kurallari` yonetim ekranina yeni bolum eklendi: **Program Bazli Parametreler**.
- Bu bolumde her kamp programi icin su kurallar ayri ayri duzenlenebilir hale getirildi:
  - `KamuAvansKisiBasi`
  - `DigerAvansKisiBasi`
  - `VazgecmeIadeGunSayisi`
  - `GecBildirimGunlukKesintiyUzdesi`
  - `NoShowSuresiGun`
  - `AktifMi`
- Backend `yonetim-baglam` GET/PUT akisina `ProgramParametreAyarlari` dahil edildi.
- Kaydetme sirasinda program parametreleri icin upsert eklendi (mevcut satir update, yeni satir insert).
- Validasyonlar eklendi:
  - Program secimi zorunlu
  - Program bazli tekillik (bir programa bir satir)
  - Avans tutarlari, iade/no-show gunleri ve kesinti orani icin min/max kontrolu
- Tabloda kayit yoksa ekranin bos gelmemesi icin, aktif programlardan varsayilan satirlar uretilir hale getirildi.
- Frontend DTO/ekran kaydet payload'i yeni alanla guncellendi.

### Degisen Dosyalar
- backend/Kamp/Dto/KampPuanKuraliYonetimDto.cs
- backend/Kamp/Services/KampPuanKuraliYonetimService.cs
- backend/Kamp/KampValidasyonKurallari.cs
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.html
- changes.md

### Build Sonuclari (Tur 27)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)
- Not: Backend build'de mevcut eski migration dosyasi nedeniyle 2 adet `CS8981` warning goruldu (bu tur degisikliginden bagimsiz).

## Tur 28 - Aktif Olmayan Kamp Programlarini Operasyon Ekranlarindan Filtreleme

### Yapilanlar
- Aktif olmayan kamp programlarinin diger ekranlarda gorunmesini engellemek icin operasyonel baglam sorgularina `KampProgrami.AktifMi` filtresi eklendi.
- Kamp Basvuru baglami:
  - Donem listesi artik sadece aktif programlara ait aktif donemleri getiriyor.
  - Basvuru onizleme validasyonuna, secili donemin programi aktif degilse hata ekleyen kontrol eklendi.
- Kamp Tahsis baglami ve listeleme:
  - Donem dropdown listesi aktif programlarla sinirlandi.
  - Basvuru listesi sorgusu aktif programli donemler ile sinirlandi.
- Kamp Rezervasyon baglami ve listeleme:
  - Donem dropdown listesi aktif programlarla sinirlandi.
  - Rezervasyon listesi sorgusu aktif programli donemler ile sinirlandi.
- Kamp Donemi yonetim baglami:
  - Program secenekleri sadece aktif programlardan getiriliyor.
  - Donem ekleme/guncellemede secilen programin aktif olmasi zorunlu hale getirildi.

### Degisen Dosyalar
- backend/Kamp/Services/KampBasvuruService.cs
- backend/Kamp/Services/KampTahsisService.cs
- backend/Kamp/Services/KampRezervasyonService.cs
- backend/Kamp/Services/KampDonemiService.cs
- changes.md

### Build Sonuclari (Tur 28)
- Backend: Bu turda build lock nedeniyle dogrulanamadi (`STYS` ve `Visual Studio` processleri DLL dosyalarini kilitliyor).

## Tur 29 - Donemler Ekraninda da Inaktif Program Donemlerini Gizleme

### Yapilanlar
- Kullanici talebine uygun olarak `Donemler` ekranini besleyen sorgulara da aktif program filtresi uygulandi.
- `KampDonemiService` icindeki temel include/liste sorgusu guncellendi:
  - artik sadece `KampProgrami.AktifMi = true` olan donemler donuyor.
- Bu degisiklikle:
  - `Kamp Donemleri` listesi,
  - `Kamp Donemi Tesis Atama` ekraninin kullandigi donem kaynagi
  inaktif programa bagli donemleri gostermez.

### Degisen Dosyalar
- backend/Kamp/Services/KampDonemiService.cs
- changes.md

### Build Sonuclari (Tur 29)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Not: Mevcut eski migration dosyasi nedeniyle 2 adet `CS8981` warning devam ediyor.

## Tur 30 - Konaklama Ucret Konfigurasyonu Yonetimi Eklendi + Kapasite Esleme Fallback

### Yapilanlar
- `KampUcretHesaplamaService` ve `KampBasvuruService` icindeki konaklama tarife esleme akisi iyilestirildi.
  - Esleme sirasi: `birebir min-max` -> `araligi kapsayan en uygun tarife` -> `ortusen en yakin tarife`.
  - Bu sayede bina kapasitesi birebir esit degilse de "uygun ucret konfigurasyonu bulunamadi" hatasi azaltildi.
- Kullanici talebiyle ucret konfigurasyonu giris/guncelleme ekrani eklendi:
  - Mevcut `Kamp Puan Kurallari` yonetim ekranina yeni bolum: **Konaklama Ucret Konfigurasyonu**.
  - Alanlar:
    - `Kod`, `Ad`
    - `MinimumKisi`, `MaksimumKisi`
    - `KamuGunlukUcret`, `DigerGunlukUcret`
    - `BuzdolabiGunlukUcret`, `TelevizyonGunlukUcret`, `KlimaGunlukUcret`
    - `AktifMi`
- Backend `yonetim-baglam` ve `kaydet` akisi genisletildi:
  - `KonaklamaTarifeleri` listesi GET/PUT akisina dahil edildi.
  - Upsert (insert/update) mantigi eklendi.
  - Validasyonlar eklendi:
    - en az bir tarife zorunlu
    - kod/ad zorunlu
    - kod tekil olmalı
    - kisi araligi gecerlilik kontrolu
    - ucretlerde negatif deger engeli

### Degisen Dosyalar
- backend/Kamp/Services/KampUcretHesaplamaService.cs
- backend/Kamp/Services/KampBasvuruService.cs
- backend/Kamp/Dto/KampPuanKuraliYonetimDto.cs
- backend/Kamp/Services/KampPuanKuraliYonetimService.cs
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi.html
- changes.md

### Build Sonuclari (Tur 30)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)
- Not: Mevcut eski migration dosyasi nedeniyle backend buildde 2 adet `CS8981` warning devam ediyor.

## Tur 31 - Kamp Rezervasyonu ile Normal Rezervasyonu Ilk Fazda Baglama

### Yapilanlar
- Kamp ve normal rezervasyon akislari arasinda ilk faz entegrasyon eklendi.
- `KampRezervasyonService.UretAsync` guncellendi:
  - Kamp rezervasyonu olusturulurken ayni referans numarasi (`KAMP-YYYY-XXXX`) ile `Rezervasyonlar` tablosuna da otomatik kayit aciliyor.
  - Islemler tek transaction icinde yapiliyor (kamp + normal rezervasyon birlikte commit).
- Normal rezervasyon kaydi icin kamp verilerinden temel alanlar map edildi:
  - `TesisId`, `KisiSayisi`, `GirisTarihi`, `CikisTarihi`, `ToplamBazUcret`, `ToplamUcret`, `MisafirAdiSoyadi`, `TcKimlikNo`
  - `RezervasyonDurumu = Onayli`
  - `Notlar` alanina kamp kaynak bilgisi yazildi.
- `KampRezervasyonService.IptalEtAsync` guncellendi:
  - Kamp rezervasyonu iptal edilince, ayni referans numarasina sahip normal rezervasyon da `Iptal` durumuna cekiliyor.

### Degisen Dosyalar
- backend/Kamp/Services/KampRezervasyonService.cs
- changes.md

### Build Sonuclari (Tur 31)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)
- Not: Backend build'de mevcut eski migration dosyasi nedeniyle 2 adet `CS8981` warning devam ediyor.

## Tur 32 - Faz 2 Baslangici: Kamp Rezervasyonundan Normal Rezervasyona Otomatik Segment/Oda Atamasi

### Yapilanlar
- Kamp ve normal rezervasyon entegrasyonu bir adim daha ileri tasindi.
- `KampRezervasyonService.UretAsync` icinde normal rezervasyon olusurken artik:
  - tek segment otomatik uretiliyor,
  - bu segmente otomatik oda atamalari yapiliyor.

### Oda Atama Kurali
- Secili `KonaklamaBirimiTipi` (artik bina adi) once hedeflenir.
- O binada uygun aktif odalar yoksa, tesis genelindeki aktif odalara fallback yapilir.
- Oda doluluk hesabi mevcut normal rezervasyon segment/oda atamalarindan alinır.
  - Cakisan tarih araliginda `Iptal` disi rezervasyonlar dikkate alinir.
- Kapasite dagitimi:
  - Paylasimsiz odada doluluk varsa oda kullanilmaz.
  - Paylasimli odada kalan kapasite kadar kisi atanir.
  - Kisi sayisi birden fazla odaya bolunerek atanabilir.
- Yeterli kapasite yoksa kamp rezervasyonu olusturma islemi hata ile durur.

### Ek Iyilestirme
- Kamp rezervasyon numarasi uretiminde, ayni referansin normal rezervasyon tablosunda da tekil olmasi garanti edildi.

### Degisen Dosyalar
- backend/Kamp/Services/KampRezervasyonService.cs
- changes.md

### Build Sonuclari (Tur 32)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)
- Not: Backend build'de mevcut eski migration dosyasi nedeniyle 2 adet `CS8981` warning devam ediyor.

## Tur 33 - Normal Rezervasyon Listesinde Kaynak (Kamp/Normal) Gorunurlugu

### Yapilanlar
- Kullanici talebine gore `Kayitli Rezervasyonlar` tablosuna rezervasyon kaynagini gosteren yeni alan eklendi.
- Backend rezervasyon liste DTO'su genisletildi:
  - yeni alan: `Kaynak`
  - degerleme kurali: `ReferansNo` `KAMP-` ile basliyorsa `Kamp`, degilse `Normal`.
- Frontend rezervasyon liste DTO'su guncellendi (`kaynak` alanı eklendi).
- `Rezervasyon Yonetimi` ekranindaki `Kayitli Rezervasyonlar` tablosuna `Kaynak` sutunu eklendi.
  - `Kamp` kayitlari `info` etiketi ile,
  - `Normal` kayitlar `secondary` etiketi ile gosteriliyor.
- Tablo genisletme satiri (`expanded row`) yeni sutun sayisina gore guncellendi.

### Degisen Dosyalar
- backend/Rezervasyonlar/Dto/RezervasyonListeDto.cs
- backend/Rezervasyonlar/Services/RezervasyonService.cs
- frontend/src/app/pages/rezervasyon-yonetimi/rezervasyon-yonetimi.dto.ts
- frontend/src/app/pages/rezervasyon-yonetimi/rezervasyon-yonetimi.html
- changes.md

### Build Sonuclari (Tur 33)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)
- Not: Backend build'de mevcut eski migration dosyasi nedeniyle 2 adet `CS8981` warning devam ediyor.

## Tur 34 - Restoran Modulu Ornek Seed Verisi (Migration)

### Yapilanlar
- Restoran modulu frontend testlerini hizlandirmak icin yeni seed migration eklendi.
- Migration, idempotent calisir:
  - daha once ayni seed tag ile veri varsa yeniden ekleme yapmaz.
- Seed kapsami:
  - aktif ilk tesis icin 1 restoran (`Ana Restoran`)
  - 5 masa (Musait/Rezerve/Kapali kombinasyonu)
  - 3 menu kategorisi + 6 urun
  - 2 siparis:
    - 1 acik siparis (`Hazirlaniyor`, odemesiz)
    - 1 tamamlanmis siparis (`Tamamlandi`, tam odenmis)
  - 1 nakit odeme kaydi
- `Down` migration, seed tag ile olusan restoran verilerini iliski sirasina gore temizler.

### Degisen Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260410211500_SeedRestaurantModuleData.cs
- changes.md

## Tur 35 - Restoran Yetkilendirmesini Controller Bazinda Ayrisma

### Yapilanlar
- Restoran modulu icin tek bir `RezervasyonYonetimi.*` yetkisine bagli kalan yapi ayrildi.
- Yeni permission domainleri eklendi:
  - `RestoranYonetimi` (RestoranlarController)
  - `RestoranMasaYonetimi` (RestoranMasalariController)
  - `RestoranMenuYonetimi` (RestoranMenuKategorileriController + RestoranMenuUrunleriController)
  - `RestoranSiparisYonetimi` (RestoranSiparisleriController)
  - `RestoranOdemeYonetimi` (RestoranOdemeleriController)
- Backend controller `[Permission(...)]` attributeleri yeni domainlere cekildi.
- Frontend tarafinda restoran ekranlarinin `canManage` kontrolleri yeni permission adlariyla guncellendi.
- Restoran runtime menusu role mappingleri yeni menu permission'larina cekildi:
  - `RestoranYonetimi.Menu`
  - `RestoranMasaYonetimi.Menu`
  - `RestoranMenuYonetimi.Menu`
  - `RestoranSiparisYonetimi.Menu`
- Siparis ekraninda odeme aksiyonlari icin ayri `RestoranOdemeYonetimi.Manage` kontrolu eklendi.
- Yeni migration eklendi:
  - `20260410214500_AddRestaurantControllerScopedPermissions`
  - 5 domain x 3 role (`Menu/View/Manage`) olusturur
  - Admin ve TesisManager gruplarina bu rolleri atar
  - `Down` tarafinda ilgili role/grup baglantilarini temizler.

### Degisen Dosyalar
- backend/StructurePermissions.cs
- backend/RestoranYonetimi/Restoranlar/Controllers/RestoranlarController.cs
- backend/RestoranYonetimi/RestoranMasalari/Controllers/RestoranMasalariController.cs
- backend/RestoranYonetimi/RestoranMenuKategorileri/Controllers/RestoranMenuKategorileriController.cs
- backend/RestoranYonetimi/RestoranMenuUrunleri/Controllers/RestoranMenuUrunleriController.cs
- backend/RestoranYonetimi/RestoranSiparisleri/Controllers/RestoranSiparisleriController.cs
- backend/RestoranYonetimi/RestoranOdemeleri/Controllers/RestoranOdemeleriController.cs
- backend/Infrastructure/EntityFramework/Migrations/20260410214500_AddRestaurantControllerScopedPermissions.cs
- frontend/src/app/core/menu/menu-runtime.service.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-masa-yonetimi.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-menu-yonetimi.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-siparis-yonetimi.ts
- changes.md

## Tur 36 - Restoran Menusunu Top-Level Yapma + Tum Tesislere Restoran ve Ortak Kategori/Urun Seed

### Yapilanlar
- Restoran menusu, Isletme altina enjekte edilmek yerine dogrudan top-level (`Restoran`) olarak sabitlendi.
- Eger top-level `Restoran` menusu zaten varsa duplicate olusturulmadan child itemlar merge edilir.
- Yeni seed migration eklendi:
  - Tum aktif tesislerde `Ana Restoran` yoksa otomatik olusturur.
  - Her aktif restoran icin standart masa seti seed eder.
  - Ortak kategori sablonunu her restorana uygular (tekrar kullanilabilir kategori seti):
    - Corbalar
    - Ana Yemekler
    - Tatlilar
    - Icecekler
  - Her kategoriye urun sablonu seed eder.
  - Islem idempotenttir (var olan kayitlari tekrar eklemez).

### Degisen Dosyalar
- frontend/src/app/core/menu/menu-runtime.service.ts
- backend/Infrastructure/EntityFramework/Migrations/20260410223000_SeedRestaurantsForAllTesisWithSharedMenuTemplates.cs
- changes.md

## Tur 37 - Global Kategori Havuzu + Restoran Bazli Kategori Atama Ekrani

### Yapilanlar
- Restoran urun kategorilerini ust seviyeden yonetmek icin global kategori API'leri eklendi.
- Mimariyi bozmamak icin mevcut `RestoranMenuKategorileri` tablosu uzerinden isim-bazli ortak kategori havuzu kullanildi.
- Backend yeni endpointler:
  - `GET /api/restoran-menu-kategorileri/global`
  - `POST /api/restoran-menu-kategorileri/global`
  - `PUT /api/restoran-menu-kategorileri/global/{id}`
  - `DELETE /api/restoran-menu-kategorileri/global/{id}`
  - `GET /api/restoran-menu-kategorileri/atama-baglam?restoranId=...`
  - `PUT /api/restoran-menu-kategorileri/atamalar`
- Atama davranisi:
  - Restoran icin secilen global kategoriler aktif edilir (yoksa olusturulur),
  - secilmeyen global kategoriler restoran tarafinda pasiflenir.
- Frontend yeni ekran eklendi:
  - `restoran-kategori-havuzu`
  - Sol tarafta global kategori CRUD, sag tarafta restoran secip coklu kategori atama.
- Route ve menu entegrasyonu tamamlandi:
  - yeni route: `/restoran-kategori-havuzu`
  - Restoran top-level menusu altina `Kategori Havuzu` eklendi.

### Degisen Dosyalar
- backend/RestoranYonetimi/RestoranMenuKategorileri/Dtos/RestoranMenuKategoriDtos.cs
- backend/RestoranYonetimi/RestoranMenuKategorileri/Services/IRestoranMenuKategoriService.cs
- backend/RestoranYonetimi/RestoranMenuKategorileri/Services/RestoranMenuKategoriService.cs
- backend/RestoranYonetimi/RestoranMenuKategorileri/Controllers/RestoranMenuKategorileriController.cs
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.dto.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-menu-yonetimi.service.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-kategori-havuzu-yonetimi.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-kategori-havuzu-yonetimi.html
- frontend/src/app/app.routes.ts
- frontend/src/app/core/menu/menu-runtime.service.ts
- changes.md

### Build Sonuclari (Tur 37)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur 38 - Global Menu Kategori Tablosu + LINQ Translation Hata Duzeltmesi

### Yapilanlar
- Uretimde alinan hata giderildi:
  - `RestoranMenuKategoriService` icindeki `GroupBy + projection` sorgusu SQL'e cevrilemedigi icin `InvalidOperationException` aliyordu.
  - Global kategori listeleme akisinda EF GroupBy projection kaldirildi; SQL tabanli okuma kullanildi.
- Talebe uygun sekilde ayri bir global kategori tablosu eklendi:
  - yeni tablo: `restoran.MenuKategoriTanimlari`
  - alanlar: `Ad`, `SiraNo`, `AktifMi` + audit/soft-delete kolonlari
  - `Ad` uzerinde unique index (`IsDeleted = 0` filtreli)
  - migration sirasinda mevcut `RestoranMenuKategorileri` verilerinden backfill yapildi.
- Global kategori CRUD ve restoran bazli atama akisi bu yeni tabloya baglandi:
  - global kategori listesi/artirma/guncelleme/pasifleme artik `MenuKategoriTanimlari` tablosunu esas aliyor.
  - restoran kategori atamalarinda secilen global kategoriler restoran tarafinda aktifleniyor/olusturuluyor,
    secilmeyenler pasifleniyor.

### Degisen Dosyalar
- backend/RestoranYonetimi/RestoranMenuKategorileri/Services/RestoranMenuKategoriService.cs
- backend/Infrastructure/EntityFramework/Migrations/20260410235500_AddRestoranMenuKategoriTanimlariTable.cs
- changes.md

### Build Sonuclari (Tur 38)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur 39 - Restoran Menusunun Alt Menulerde Tekrar Etmesini Duzeltme

### Yapilanlar
- `menu-runtime.service.ts` icindeki restoran menu enjeksiyonu sadece kok menu seviyesinde calisacak sekilde duzeltildi.
- Alt menu agaclarinda tekrar tekrar `Restoran` root eklenmesi engellendi.

### Degisen Dosyalar
- frontend/src/app/core/menu/menu-runtime.service.ts
- changes.md

## Tur 40 - Restoran ile Isletme Alani (RESTORAN) Iliskisi

### Yapilanlar
- `Restoran` varligina `IsletmeAlaniId` (nullable) iliskisi eklendi.
- Sadece ayni tesis altindaki ve `IsletmeAlaniSinifi.Kod = RESTORAN` olan aktif isletme alanlarinin secilebilmesi backend tarafinda zorunlu kilindi.
- Yeni endpoint eklendi:
  - `GET /api/restoranlar/isletme-alanlari?tesisId=...`
- Restoran listesi/detay DTO'su isletme alani adini da donecek sekilde genislendi.
- Restoran yonetimi ekranina tesis secimine bagli `Isletme Alani (RESTORAN)` dropdown'u eklendi.
- Migration eklendi:
  - `20260412105205_AddRestoranIsletmeAlaniIliskisi`
  - `restoran.Restoranlar` tablosuna `IsletmeAlaniId` kolonu + FK + index
  - Mevcut restoranlar icin ayni tesis altinda uygun RESTORAN isletme alani backfill SQL'i eklendi.

### Degisen Dosyalar
- backend/RestoranYonetimi/Restoranlar/Entities/Restoran.cs
- backend/RestoranYonetimi/Restoranlar/Dtos/RestoranDtos.cs
- backend/RestoranYonetimi/Restoranlar/Services/IRestoranService.cs
- backend/RestoranYonetimi/Restoranlar/Services/RestoranService.cs
- backend/RestoranYonetimi/Restoranlar/Controllers/RestoranlarController.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Infrastructure/EntityFramework/Migrations/20260412105205_AddRestoranIsletmeAlaniIliskisi.cs
- backend/Infrastructure/EntityFramework/Migrations/20260412105205_AddRestoranIsletmeAlaniIliskisi.Designer.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.dto.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.service.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.html
- changes.md

### Build Sonuclari (Tur 40)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur 41 - RESTORAN Isletme Alani Secenek Sorgusu LINQ Translation Duzeltmesi

### Yapilanlar
- `RestoranService.GetIsletmeAlaniSecenekleriAsync` icindeki `OrderBy` tarafinda SQL'e cevrilemeyen string formatlama kaldirildi.
- Sorgu SQL tarafinda yalniz ham alanlari cekiyor, ad olusturma ve siralama bellek tarafinda yapiliyor.
- `string.Format` kaynakli `could not be translated` hatasi giderildi.

### Degisen Dosyalar
- backend/RestoranYonetimi/Restoranlar/Services/RestoranService.cs
- changes.md

## Tur 42 - Restoran Yoneticisi Atama Altyapisi

### Yapilanlar
- Restoran icin yonetici atama iliskisi eklendi:
  - yeni entity: `RestoranYonetici` (`RestoranId`, `UserId`)
  - `Restoran` ile bire-cok iliski
  - unique kisit: ayni restorana ayni kullanici ikinci kez atanamaz
- `Restoran` DTO/request modelleri yonetici listesi destekleyecek sekilde genislendi:
  - `YoneticiUserIds`
- Restoran servisinde yonetici atama kurallari eklendi:
  - create/update isteklerinde kullanici id'leri normalize edilir
  - var olmayan kullanici id'leri engellenir
  - restoran yonetici listesi senkronize edilir (ekle/sil)
- Restoran yonetim ekraninda yonetici secimi eklendi:
  - dialog icinde `Restoran Yoneticileri` coklu secim alani
  - listede `Yonetici Sayisi` kolonu
- Yonetici adaylari icin restoran yetkili endpoint eklendi:
  - `GET /ui/yoneticiaday/restoran-yoneticileri`
- Migration eklendi:
  - `20260412111830_AddRestoranYoneticileri`
  - yeni tablo: `[restoran].[RestoranYoneticileri]`

### Degisen Dosyalar
- backend/RestoranYonetimi/Restoranlar/Entities/RestoranYonetici.cs
- backend/RestoranYonetimi/Restoranlar/Entities/Restoran.cs
- backend/RestoranYonetimi/Restoranlar/Dtos/RestoranDtos.cs
- backend/RestoranYonetimi/Restoranlar/Services/RestoranService.cs
- backend/YoneticiAdaylari/Services/IYoneticiAdayService.cs
- backend/YoneticiAdaylari/Services/YoneticiAdayService.cs
- backend/YoneticiAdaylari/Controllers/YoneticiAdayController.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Infrastructure/EntityFramework/Migrations/20260412111830_AddRestoranYoneticileri.cs
- backend/Infrastructure/EntityFramework/Migrations/20260412111830_AddRestoranYoneticileri.Designer.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.dto.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.service.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.html
- changes.md

### Build Sonuclari (Tur 42)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur 43 - Restoran Yoneticisi Atanabilir/Atayabilir Yetkileri

### Yapilanlar
- `KullaniciAtama` altina yeni izinler eklendi:
  - `KullaniciAtama.RestoranYoneticisiAtanabilir`
  - `KullaniciAtama.RestoranYoneticisiAtayabilir`
- Restoran yonetici aday endpointi yeni atama iznine baglandi:
  - `GET /ui/yoneticiaday/restoran-yoneticileri` artik `RestoranYoneticisiAtayabilir` ister.
- Restoran yonetici aday listesi artik marker role'e gore filtreleniyor:
  - sadece `RestoranYoneticisiAtanabilir` rolune sahip, scope icinde gorunebilen ve block olmayan kullanicilar listelenir.
- Restoran create/update servisinde backend kurali eklendi:
  - `yoneticiUserIds` gonderiliyorsa islem yapan kullanicida `RestoranYoneticisiAtayabilir` olmali
  - secilen kullanicilar `RestoranYoneticisiAtanabilir` marker rolune sahip olmali
- Frontend restoran yonetim ekraninda yonetici atama alani:
  - sadece `RestoranYoneticisiAtayabilir` varsa gorunur
  - bu yetki yoksa payload'a yonetici listesi gonderilmez (mevcut atamalar korunur)
- Yeni migration eklendi:
  - `20260412114000_AddRestaurantManagerAssignmentPermissions`
  - yeni roller olusturuldu, grup atamalari yapildi:
    - `Atanabilir`: Admin, TesisYonetici, BinaYonetici, Resepsiyonist
    - `Atayabilir`: Admin, TesisYonetici

### Degisen Dosyalar
- backend/StructurePermissions.cs
- backend/YoneticiAdaylari/Controllers/YoneticiAdayController.cs
- backend/YoneticiAdaylari/Services/YoneticiAdayService.cs
- backend/AccessScope/AccessScopeProvider.cs
- backend/RestoranYonetimi/Restoranlar/Services/RestoranService.cs
- backend/Infrastructure/EntityFramework/Migrations/20260412114000_AddRestaurantManagerAssignmentPermissions.cs
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.html
- changes.md

### Build Sonuclari (Tur 43)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur 44 - Restoran Menu Yonetimi Urun Formu Duzeltmeleri

### Yapilanlar
- Urun dialoguna kategori secimi eklendi (`Kategori` dropdown).
- Urun guncellemede hata veren ana neden giderildi:
  - menu response map'lenirken `restoranMenuKategoriId` urun modeline set edilmiyordu.
  - artik her urun kaydi kendi kategori id'si ile mapleniyor.
- Urun validasyon mesaji netlestirildi:
  - `Kategori ve urun adi zorunludur.`
- Edit modunda urun kaydinda kategori id bos gelirse secili kategoriye fallback eklendi.

### Degisen Dosyalar
- frontend/src/app/pages/restoran-yonetimi/restoran-menu-yonetimi.service.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-menu-yonetimi.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-menu-yonetimi.html
- changes.md

### Build Sonuclari (Tur 44)
- Frontend: BASARILI (`npm run build`)

## Tur 45 - Restoran Menu Ekrani Sadelestirme (Kategori CRUD Kaldirildi)

### Yapilanlar
- `Restoran Menu Yonetimi` ekranindan kategori ekle/duzenle/sil alanlari kaldirildi.
- Ekran artik sadece urun yonetimi odakli:
  - urunler kategori bazli gruplanmis kartlar altinda listeleniyor.
  - her kategori icin ayri "Bu Kategoriye Urun Ekle" aksiyonu eklendi.
  - ustte genel "Urun Ekle" butonu korunarak kategori secimi urun dialogunda yapiliyor.
- Urun dialogundaki kategori secimi, olusturma/guncelleme akisiyla uyumlu halde korundu.
- Silinen component dosyasi yeniden olusturularak route baglantisi tekrar calisir hale getirildi.

### Degisen Dosyalar
- frontend/src/app/pages/restoran-yonetimi/restoran-menu-yonetimi.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-menu-yonetimi.html
- changes.md

### Build Sonuclari (Tur 45)
- Frontend: BASARILI (`npm run build`)

## Tur 46 - Pasif Urun Gorunurlugu ve Hazirlama Suresi UI Iyilestirme

### Yapilanlar
- Restoran menu yonetimi ekraninda urun verisi aktif menu endpointinden degil, yonetim endpointlerinden alinacak sekilde guncellendi.
  - Boyunca kullanilan yeni servis metodu: `getYonetimMenuByRestoranId(restoranId)`
  - Kategoriler: `GET /api/restoran-menu-kategorileri?restoranId=...`
  - Urunler: `GET /api/restoran-menu-urunleri`
  - Sonuc: pasif urunler artik listede kalir; tekrar aktif edilebilir.
- Siparis ekranlarinin kullandigi mevcut `getMenuByRestoranId` (aktif menu) davranisi degistirilmedi.
- Urun dialogundaki `Hazirlama (dk)` alani iyilestirildi:
  - `p-inputgroup` + `dk` addon kullanildi.
  - Spinner gorunumu sadeletildi (`showButtons` kaldirildi, tam sayi girisi korundu).
  - Dialog genisligi bir miktar arttirildi (`40rem`) ve alan tasmasi azaltildi.

### Degisen Dosyalar
- frontend/src/app/pages/restoran-yonetimi/restoran-menu-yonetimi.service.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-menu-yonetimi.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-menu-yonetimi.html
- changes.md

### Build Sonuclari (Tur 46)
- Frontend: BASARILI (`npm run build`)

## Tur 47 - Restoran Menu Ekraninda Kategori UI Temizligi

### Yapilanlar
- `restoran-menu-yonetimi` urun dialogundan kategori dropdown kaldirildi.
- Kategoriye ozel aksiyon metinleri sadeletildi:
  - "Bu Kategoriye Urun Ekle" -> "Urun Ekle"
- Ustteki "Urun Ekle" butonu kategori secimi gostermeden ilk kategoriye urun olusturacak sekilde ayarlandi.
- Validasyon mesajlari sadeletildi:
  - "Kategori ve urun adi zorunludur." yerine "Urun adi zorunludur."
  - kategori id yoksa ayri teknik uyari: "Urun kategorisi bulunamadi."

### Degisen Dosyalar
- frontend/src/app/pages/restoran-yonetimi/restoran-menu-yonetimi.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-menu-yonetimi.html
- changes.md

### Build Sonuclari (Tur 47)
- Frontend: BASARILI (`npm run build`)

## Tur 48 - Restoran Menu Ekrani UX Iyilestirmeleri

### Yapilanlar
- Kategori bloklari acilir/kapanir panele cevrildi (`p-panel [toggleable]=true`).
- Uste urun arama alani eklendi:
  - ad ve aciklama alanlarinda filtreleme yapiyor.
  - arama sonucu kategori basliginda adet guncel gorunuyor.
- Kategori tablolarinda baslik/kolon hizasi sabitlendi:
  - `table-layout: fixed` ve ortak `colgroup` genislikleri eklendi.
  - tum panellerde `Ad/Fiyat/Sure/Durum/Islem` kolonlari ayni hizada.

### Degisen Dosyalar
- frontend/src/app/pages/restoran-yonetimi/restoran-menu-yonetimi.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-menu-yonetimi.html
- changes.md

### Build Sonuclari (Tur 48)
- Frontend: BASARILI (`npm run build`)

## Tur 49 - Urun Dialogunda Kategori Secimi Geri Eklendi

### Yapilanlar
- `restoran-menu-yonetimi` ekraninda urun olusturma/duzenleme dialoguna kategori dropdown tekrar eklendi.
- Boylece yeni urun eklerken kategori manuel secilebilir hale geldi.
- Kategori yonetim paneli geri getirilmedi; sadece urun dialogundaki secim alani acildi.

### Degisen Dosyalar
- frontend/src/app/pages/restoran-yonetimi/restoran-menu-yonetimi.html
- changes.md

### Build Sonuclari (Tur 49)
- Frontend: BASARILI (`npm run build`)

## Tur 50 - Musteri Dijital Menu Ekrani Eklendi

### Yapilanlar
- Musteri odakli sade dijital menu endpointi eklendi:
  - `GET /api/musteri-menu/{restoranId}`
  - sadece aktif restoran, aktif kategori ve aktif urunler doner.
  - kategori siralamasi `SiraNo` + `Ad`.
  - urun siralamasi `Ad`.
- Yeni backend DTO/service/controller eklendi (minimum kapsam).
- Yeni frontend ekran eklendi:
  - route: `/musteri-menu/:restoranId`
  - restoran ozet bilgi alani
  - yatay kategori chip filtresi
  - ad/aciklama bazli arama
  - kart bazli urun listesi
  - urun detay popup (kategori, fiyat, hazirlama suresi)
  - mobil uyumlu stil
- Mevcut admin/siparis akislari degistirilmedi.

### Degisen Dosyalar
- backend/RestoranYonetimi/MusteriMenu/Dtos/MusteriMenuDtos.cs
- backend/RestoranYonetimi/MusteriMenu/Services/IMusteriMenuService.cs
- backend/RestoranYonetimi/MusteriMenu/Services/MusteriMenuService.cs
- backend/RestoranYonetimi/MusteriMenu/Controllers/MusteriMenuController.cs
- backend/Program.cs
- frontend/src/app/pages/musteri-menu/musteri-menu.model.ts
- frontend/src/app/pages/musteri-menu/musteri-menu.service.ts
- frontend/src/app/pages/musteri-menu/musteri-menu.ts
- frontend/src/app/pages/musteri-menu/musteri-menu.html
- frontend/src/app/pages/musteri-menu/musteri-menu.scss
- frontend/src/app.routes.ts
- changes.md

### Build Sonuclari (Tur 50)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur 51 - Musteri Menu Route Dogrudan Sayfaya Alindi

### Yapilanlar
- `musteri-menu` route'u `AppLayout` cocuk route yapisindan cikarilip dogrudan component route yapildi:
  - `/musteri-menu/:restoranId` artik direkt `MusteriMenuPage` render eder.
- Amac: URL ile direkt acilista sayfanin bos kalmasini/on yukleme sorunlarini engellemek.

### Degisen Dosyalar
- frontend/src/app.routes.ts
- changes.md

### Build Sonuclari (Tur 51)
- Frontend: BASARILI (`npm run build`)

## Tur 52 - Musteri Menu Ekrani Gorsel Sadelestirme ve Mobil Sıkılastirma

### Yapilanlar
- Musteri menu ekraninin genel bosluklari azaltildi, container merkezlenip max-genislik verildi.
- Hero ve toolbar kartlari daha kompakt ve daha temiz gorunume alindi.
- Kategori chip satiri ve kart araliklari sikilastirildi.
- Urun karti ic padding ve tipografi PrimeNG card override ile optimize edildi.
- Grid davranisi iyilestirildi:
  - Desktop: daha dengeli kart dagilimi (`minmax(260px, 1fr)`)
  - Tablet: 2 kolon
  - Mobil: 1 kolon
- Mobil breakpointlerde baslik/padding/section olculeri ayrica optimize edildi.

### Degisen Dosyalar
- frontend/src/app/pages/musteri-menu/musteri-menu.scss
- changes.md

### Build Sonuclari (Tur 52)
- Frontend: BASARILI (`npm run build`)

## Tur 53 - Musteri Menu Urun Kartlarina Kucuk Gorsel Eklendi

### Yapilanlar
- Urun kartlarina kucuk thumbnail gorsel alani eklendi.
- Urun detay dialoguna da buyukce gorsel alani eklendi.
- Gorseller `public/demo/images/product` altindaki mevcut gorsellerden urun id bazli otomatik seciliyor.
- Gorsel yuklenemezse fallback olarak `product-placeholder.svg` kullaniliyor.

### Degisen Dosyalar
- frontend/src/app/pages/musteri-menu/musteri-menu.ts
- frontend/src/app/pages/musteri-menu/musteri-menu.html
- frontend/src/app/pages/musteri-menu/musteri-menu.scss
- changes.md

### Build Sonuclari (Tur 53)
- Frontend: BASARILI (`npm run build`)

## Tur 54 - Musteri Menu Icin Login Zorunlulugu Kaldirildi (Frontend Interceptor)

### Yapilanlar
- `auth-token.interceptor` anonim istek allowlist'i genisletildi.
- `GET /api/musteri-menu/{restoranId}` cagrilari token zorunlulugundan muaf tutuldu.
- Helper isimlendirmesi genellestirildi:
  - `isAnonymousKampBasvuruRequest` -> `isAnonymousPublicRequest`
- Sonuc: `/musteri-menu/:restoranId` sayfasi login olmadan acildiginda API istegi client tarafinda 401'e dusmeden backend'e gider.

### Degisen Dosyalar
- frontend/src/app/pages/auth/auth-token.interceptor.ts
- changes.md

## Tur 55 - Garson Servis Ekrani (Masa Oturumu) Eklendi

### Yapilanlar
- Restoran modulu icin hizli operasyon odakli yeni backend API yuzeyi eklendi:
  - `api/garson/restoranlar/{restoranId}/masalar`
  - `api/garson/masalar/{masaId}/oturum` (GET)
  - `api/garson/masalar/{masaId}/oturum` (POST: acik oturum yoksa olustur)
  - `api/garson/oturumlar/{oturumId}/kalemler` (POST)
  - `api/garson/oturumlar/{oturumId}/kalemler/{kalemId}` (PUT/DELETE)
  - `api/garson/oturumlar/{oturumId}/not` (PUT)
  - `api/garson/oturumlar/{oturumId}/durum` (PUT)
  - `api/garson/restoranlar/{restoranId}/menu`
- Mevcut `RestoranSiparis` entity'si korunarak garson akisinda teknik olarak "Masa Oturumu" kavrami saglandi.
- Is kurallari uygulandi:
  - ayni masada tek acik oturum
  - pasif/kapali masa icin oturum acmama
  - pasif urun/kategori engeli
  - kapali oturumda kalem degisiklik engeli
  - kalem eklemede ayni urun+not icin miktar arttirma
  - toplamlarin backend tarafinda yeniden hesaplanmasi
- Frontend'e yeni `garson-servis` operasyon sayfasi eklendi:
  - sol panel: restoran secimi + masa kart gridi
  - sag panel: secili masa oturumu, kalemler, notlar, hizli urun ekleme
  - kategori sekmeli urun secimi + urun arama
  - miktar +/- , kalem notu, kalem silme
  - oturum notu kaydetme
  - "Servise Al", "Hesaba Devret", "Oturumu Kapat" aksiyonlari
  - dokunmatik kullanima uygun kart/buton yogun layout
- Route eklendi: `/garson-servis`
- Restoran menu altina `Garson Servis` menusu eklendi.

### Degisen Dosyalar
- backend/RestoranYonetimi/GarsonServis/Dtos/GarsonServisDtos.cs
- backend/RestoranYonetimi/GarsonServis/Services/IGarsonServisService.cs
- backend/RestoranYonetimi/GarsonServis/Services/GarsonServisService.cs
- backend/RestoranYonetimi/GarsonServis/Controllers/GarsonServisController.cs
- backend/Program.cs
- frontend/src/app/pages/restoran-yonetimi/garson-servis.dto.ts
- frontend/src/app/pages/restoran-yonetimi/garson-servis.service.ts
- frontend/src/app/pages/restoran-yonetimi/garson-servis.ts
- frontend/src/app/pages/restoran-yonetimi/garson-servis.html
- frontend/src/app/pages/restoran-yonetimi/garson-servis.scss
- frontend/src/app.routes.ts
- frontend/src/app/core/menu/menu-runtime.service.ts
- changes.md

### Build Sonuclari (Tur 55)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur 56 - Garson Ekraninda Kalem Bazli Durum Takibi (Masa Oturumu)

### Yapilanlar
- Masa oturumu kalemlerine durum alani eklendi:
  - `Beklemede`, `Hazirlaniyor`, `Hazir`, `ServisEdildi`, `Iptal`
- Backend entity/model:
  - `RestoranSiparisKalemi.Durum` alani eklendi (default: `Beklemede`).
  - yeni sabit sinifi: `RestoranSiparisKalemDurumlari`.
- Garson API guncellendi:
  - kalem DTO'su artik `Durum` donuyor.
  - kalem update isteginde `Durum` gonderilebiliyor.
  - ayni urun ekleme birlestirmesi, `ServisEdildi/Iptal` kalemleriyle birlesmeyecek sekilde daraltildi.
- Frontend garson servis ekrani guncellendi:
  - her kalem satirinda durum tag'i gosterimi eklendi.
  - hizli durum aksiyonlari eklendi:
    - Beklemede
    - Hazirlaniyor
    - Hazir
    - Servis Edildi
  - kalem not/miktar guncelleme istekleri durum bilgisini de koruyacak sekilde guncellendi.

### Migration
- yeni migration: `20260412190000_AddRestoranSiparisKalemiDurumu`
  - `[restoran].[RestoranSiparisKalemleri]` tablosuna `Durum` kolonu eklendi (`nvarchar(32)`, not null, default `Beklemede`)
  - mevcut kayitlar icin backfill SQL eklendi.
- `StysAppDbContextModelSnapshot` guncellendi.

### Degisen Dosyalar
- backend/RestoranYonetimi/RestoranSiparisleri/Entities/RestoranSiparisKalemi.cs
- backend/RestoranYonetimi/RestoranSiparisleri/Entities/RestoranSiparisKalemDurumlari.cs
- backend/RestoranYonetimi/RestoranSiparisleri/Dtos/RestoranSiparisDtos.cs
- backend/RestoranYonetimi/RestoranSiparisleri/Services/RestoranSiparisService.cs
- backend/RestoranYonetimi/GarsonServis/Dtos/GarsonServisDtos.cs
- backend/RestoranYonetimi/GarsonServis/Services/GarsonServisService.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Infrastructure/EntityFramework/Migrations/20260412190000_AddRestoranSiparisKalemiDurumu.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- frontend/src/app/pages/restoran-yonetimi/garson-servis.dto.ts
- frontend/src/app/pages/restoran-yonetimi/garson-servis.ts
- frontend/src/app/pages/restoran-yonetimi/garson-servis.html
- changes.md

### Build Sonuclari (Tur 56)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur 57 - Restoran Yoneticisi Erisim Kapsami (Sadece Kendi Restoranlari)

### Yapilanlar
- Restoran modulu icin ortak erisim servisi eklendi:
  - `IRestoranErisimService`
  - `RestoranErisimService`
- Kural:
  - Kullanici `RestoranYoneticileri` tablosunda restoran(lar)a atanmis ise,
  - ve `KullaniciAtama.RestoranYoneticisiAtayabilir` yetkisi yoksa,
  - restoran modulunde yalnizca atandigi restoranlarin verisini gorebilir/isleyebilir.
- Admin ve restoran yonetici atama yetkisi olan kullanicilarin mevcut genis gorunumu korunur.

### Scope Uygulanan Servisler
- `RestoranService`
  - listeleme ve detay erisim filtrelendi
  - update/delete icin restoran erisim kontrolu eklendi
- `RestoranMasaService`
  - listeleme, detay, create/update/delete restoran erisim kontrolu eklendi
- `RestoranMenuKategoriService`
  - listeleme, detay, menu getirme, atama baglami/atama kayit restoran erisim kontrolu eklendi
- `RestoranMenuUrunService`
  - kategori/restoran bagina gore listeleme, detay, create/update/delete restoran erisim kontrolu eklendi
- `RestoranSiparisService`
  - listeleme, detay, restoran bazli cagrilar, create/update/durum guncelleme restoran erisim kontrolu eklendi
- `RestoranOdemeService`
  - odeme liste/ozet ve odeme olusturma akislarinda siparisin restoranina erisim kontrolu eklendi
- `GarsonServisService`
  - restoran bazli masa/menu endpointleri ve masa/oturum bazli islemlerde restoran erisim kontrolu eklendi

### DI
- `Program.cs` icine `IRestoranErisimService -> RestoranErisimService` kaydi eklendi.

### Degisen Dosyalar
- backend/RestoranYonetimi/Services/IRestoranErisimService.cs
- backend/RestoranYonetimi/Services/RestoranErisimService.cs
- backend/Program.cs
- backend/RestoranYonetimi/Restoranlar/Services/RestoranService.cs
- backend/RestoranYonetimi/RestoranMasalari/Services/RestoranMasaService.cs
- backend/RestoranYonetimi/RestoranMenuKategorileri/Services/RestoranMenuKategoriService.cs
- backend/RestoranYonetimi/RestoranMenuUrunleri/Services/RestoranMenuUrunService.cs
- backend/RestoranYonetimi/RestoranSiparisleri/Services/RestoranSiparisService.cs
- backend/RestoranYonetimi/RestoranOdemeleri/Services/RestoranOdemeService.cs
- backend/RestoranYonetimi/GarsonServis/Services/GarsonServisService.cs
- changes.md

### Build Sonuclari (Tur 57)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)

## Tur 58 - Tesis Yoneticisi Restoran Garsonu Olusturma/Atama

### Yapilanlar
- Yeni rol/yetkiler eklendi:
  - `KullaniciAtama.RestoranGarsonuAtanabilir`
  - `KullaniciAtama.RestoranGarsonuAtayabilir`
- Restoran-garson iliskisi eklendi:
  - yeni tablo/entity: `RestoranGarsonlari` (`RestoranId`, `UserId`)
- Restoran CRUD akisina garson atama eklendi:
  - `CreateRestoranRequest` ve `UpdateRestoranRequest` icine `GarsonUserIds`
  - restoran detay/liste DTO'suna `GarsonUserIds`
- Yonetici aday endpointi genisletildi:
  - `GET /ui/yoneticiaday/restoran-garsonlari`
- Restoran erisim kapsaminda garson atamalari da dikkate alindi:
  - restoran yoneticisi + garson atamasi olan kullanicilar yalnizca kendi restoranlarini gorur.
- Frontend restoran yonetimi ekranina garson atama alani eklendi:
  - aday cekme + coklu secim + liste kolonu (garson sayisi)

### Migration
- yeni migration: `20260412193000_AddRestoranGarsonlariAndPermissions`
  - `[restoran].[RestoranGarsonlari]` tablosu olusturuldu
  - unique/index/FK tanimlari eklendi
  - yeni roller TODBase'e seed edildi
  - rol-atama map seed SQL'i eklendi

### Degisen Dosyalar
- backend/StructurePermissions.cs
- backend/AccessScope/AccessScopeProvider.cs
- backend/RestoranYonetimi/Restoranlar/Entities/RestoranGarson.cs
- backend/RestoranYonetimi/Restoranlar/Entities/Restoran.cs
- backend/RestoranYonetimi/Restoranlar/Dtos/RestoranDtos.cs
- backend/RestoranYonetimi/Restoranlar/Services/RestoranService.cs
- backend/RestoranYonetimi/Services/RestoranErisimService.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/YoneticiAdaylari/Services/IYoneticiAdayService.cs
- backend/YoneticiAdaylari/Services/YoneticiAdayService.cs
- backend/YoneticiAdaylari/Controllers/YoneticiAdayController.cs
- backend/Infrastructure/EntityFramework/Migrations/20260412193000_AddRestoranGarsonlariAndPermissions.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.dto.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.service.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.ts
- frontend/src/app/pages/restoran-yonetimi/restoran-yonetimi.html
- changes.md

### Build Sonuclari (Tur 58)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur 59 - Restoran Yonetici/Garson Gruplari ve Yetki Atamalari

### Yapilanlar
- Veritabani icin iki yeni kullanici grubu seed edildi:
  - `RestoranYoneticiGrubu`
  - `GarsonGrubu`
- Gruplara restoran modulu icin gerekli roller otomatik atandi.

### Yetki Setleri
- `RestoranYoneticiGrubu`:
  - `RestoranYonetimi`: `Menu`, `View`, `Manage`
  - `RestoranMasaYonetimi`: `Menu`, `View`, `Manage`
  - `RestoranMenuYonetimi`: `Menu`, `View`, `Manage`
  - `RestoranSiparisYonetimi`: `Menu`, `View`, `Manage`
  - `RestoranOdemeYonetimi`: `Menu`, `View`, `Manage`
  - `KullaniciAtama`: `RestoranYoneticisiAtanabilir`, `RestoranGarsonuAtayabilir`
- `GarsonGrubu`:
  - `RestoranYonetimi`: `Menu`, `View`
  - `RestoranSiparisYonetimi`: `Menu`, `View`, `Manage`
  - `RestoranMenuYonetimi`: `View`
  - `KullaniciAtama`: `RestoranGarsonuAtanabilir`

### Migration
- yeni migration: `20260413102000_AddRestaurantManagerAndWaiterUserGroups`
  - grup olusturma + role seed/atama SQL'i eklendi
  - idempotent (mevcut kayitlari tekrar eklemez) olacak sekilde yazildi

### DB Uygulama
- Migration veritabanina uygulandi:
  - `dotnet ef database update --context StysAppDbContext --project backend/STYS.csproj --startup-project backend/STYS.csproj`
  - Sonuc: `Done.`

### Degisen Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260413102000_AddRestaurantManagerAndWaiterUserGroups.cs
- changes.md

## Tur 60 - Restoran Erisim Kapsami Sertlestirme (Tesis Yoneticisi + Restoran Yoneticisi + Garson)

### Yapilanlar
- `RestoranErisimService` kapsam kurali guncellendi.
- Admin disinda su roller icin scope zorunlu hale getirildi:
  - `KullaniciAtama.TesisYoneticisiAtanabilir/Atayabilir`
  - `KullaniciAtama.RestoranYoneticisiAtanabilir/Atayabilir`
  - `KullaniciAtama.RestoranGarsonuAtanabilir/Atayabilir`
- Tesis yoneticisi icin erisebilir restoranlar:
  - yonettigi tesislerdeki restoranlar
  - + varsa dogrudan restoran yoneticisi/garson atandigi restoranlar
- Restoran yoneticisi ve garson icin erisebilir restoranlar:
  - yalnizca atandigi restoranlar
- Bu rollerde scope bos ise artik genis gorunum acilmiyor (liste bos doner).

### Degisen Dosyalar
- backend/RestoranYonetimi/Services/RestoranErisimService.cs
- changes.md

### Build Sonuclari (Tur 60)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)

## Tur 61 - Tesis Yoneticisi ile Restoran Yoneticisi/Garson Kullanici Olusturma

### Yapilanlar
- Tesis bazli kullanici olusturma akisina iki yeni endpoint eklendi:
  - `POST /ui/tesis/{tesisId}/restoran-yonetici-kullanici`
  - `POST /ui/tesis/{tesisId}/garson-kullanici`
- Servis tarafinda yeni olusturma metodlari eklendi:
  - `CreateRestoranYoneticisiUserAsync`
  - `CreateRestoranGarsonuUserAsync`
- Bu akislarda:
  - tesis erisim yetkisi kontrolu yapiliyor
  - gerekli atama yetkisi kontrolu yapiliyor (`RestoranYoneticisiAtayabilir`, `RestoranGarsonuAtayabilir`)
  - uygun marker grubu otomatik atanarak kullanici olusturuluyor
  - olusan kullanicinin `KullaniciTesisSahiplikleri` kaydi secilen tesis ile esleniyor

### Kullanici Yonetimi UI
- Scoped tesis yoneticisi hizli olusturma butonlari genisletildi:
  - `Restoran Yoneticisi Olustur`
  - `Garson Olustur`
- `kullanici-yonetimi` akisina yeni scoped create tipleri eklendi.
- Frontend servisine yeni cagrilar eklendi:
  - `createRestoranYoneticisiUserForTesis`
  - `createGarsonUserForTesis`

### Ek Backend API (Restoran Bazli)
- Restoran bazli olusturma endpointleri de eklendi:
  - `POST /api/restoranlar/{restoranId}/yonetici-kullanici`
  - `POST /api/restoranlar/{restoranId}/garson-kullanici`
- Bu endpointler olusturulan kullaniciyi ilgili restorana otomatik atar.

### Degisen Dosyalar
- backend/Tesisler/Services/ITesisService.cs
- backend/Tesisler/Services/TesisService.cs
- backend/Tesisler/Controllers/TesisController.cs
- backend/RestoranYonetimi/Restoranlar/Services/IRestoranService.cs
- backend/RestoranYonetimi/Restoranlar/Services/RestoranService.cs
- backend/RestoranYonetimi/Restoranlar/Controllers/RestoranlarController.cs
- frontend/src/app/pages/kullanici-yonetimi/kullanici-yonetimi.service.ts
- frontend/src/app/pages/kullanici-yonetimi/kullanici-yonetimi.ts
- frontend/src/app/pages/kullanici-yonetimi/kullanici-yonetimi.html
- changes.md

### Build Sonuclari (Tur 61)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur 62 - Restoran/Garson Olusturma Butonlarinda Grup Atamasi Duzeltmesi

### Duzeltme
- Yeni hizli olusturma akislarinda grup secimi marker-role'a gore yapildigi icin yanlislikla `TesisYoneticiGrubu` gibi gruplara dusme riski vardi.
- Restoran yoneticisi ve garson olusturma akislari artik **grup adi bazli kesin atama** yapiyor:
  - `RestoranYoneticiGrubu`
  - `GarsonGrubu`

### Guncellenen Noktalar
- `TesisService`:
  - `CreateRestoranYoneticisiUserAsync` -> `RestoranYoneticiGrubu`
  - `CreateRestoranGarsonuUserAsync` -> `GarsonGrubu`
- `RestoranService`:
  - `CreateRestoranYoneticisiUserAsync` -> `RestoranYoneticiGrubu`
  - `CreateRestoranGarsonuUserAsync` -> `GarsonGrubu`
- Bu amacla her iki servise de `GetGroupIdByNameAsync` eklendi.

### Degisen Dosyalar
- backend/Tesisler/Services/TesisService.cs
- backend/RestoranYonetimi/Restoranlar/Services/RestoranService.cs
- changes.md

### Build Sonuclari (Tur 62)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)

## Tur 63 - Garson Olusturda Fazla Rol Sorunu Duzeltmesi

### Sorun
- `Garson Olustur` (ve benzer marker bazli akislar) birden fazla grupta ayni marker oldugu icin yanlis grubu secebiliyordu.
- Bu da olusturma dialogunda beklenenden fazla rol gorunmesine neden oluyordu.

### Duzeltme
- `GetGroupIdByMarkerAsync` kullanimi korunarak secim daraltildi.
- Backend'de marker'a gore grup secerken hedef grup onceliklendirildi:
  - `RestoranYoneticisiAtanabilir` -> `RestoranYoneticiGrubu`
  - `RestoranGarsonuAtanabilir` -> `GarsonGrubu`
- Frontend `kullanici-yonetimi` scoped hizli olusturma akisi da ayni sekilde hedef grup adini once ariyor, bulamazsa marker fallback yapiyor.

### Degisen Dosyalar
- backend/Tesisler/Services/TesisService.cs
- backend/RestoranYonetimi/Restoranlar/Services/RestoranService.cs
- frontend/src/app/pages/kullanici-yonetimi/kullanici-yonetimi.ts
- changes.md

### Build Sonuclari (Tur 63)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur 64 - Scoped Yonetici Grup Validasyonu (Restoran Yonetici/Garson)

### Sorun
- `StysScopedUserService` scoped grup validasyonunda sadece tesis/bina/resepsiyonist markerlarini yonetim grubu olarak kabul ediyordu.
- Bu nedenle restoran yoneticisi/garson olusturma akisinda:
  - `Scoped yonetici yalnizca yonetim grubu tipindeki kullanici gruplarina atama yapabilir.`
  hatasi alinabiliyordu.

### Duzeltme
- Scoped validasyon whitelist'i genisletildi:
  - `KullaniciAtama.RestoranYoneticisiAtanabilir`
  - `KullaniciAtama.RestoranGarsonuAtanabilir`
- Marker -> atayabilir mapping'i genisletildi:
  - `RestoranYoneticisiAtanabilir -> RestoranYoneticisiAtayabilir`
  - `RestoranGarsonuAtanabilir -> RestoranGarsonuAtayabilir`
- Scoped yoneticinin yonetebilecegi marker listesine restoran markerlari eklendi.

### Degisen Dosyalar
- backend/Kullanicilar/Services/StysScopedUserService.cs
- changes.md

### Build Sonuclari (Tur 64)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)

## Tur 65 - Kullanici Seed (Gruplara Gore)

### Yapilanlar
- Sistemi hizli kullanilabilir hale getirmek icin cesitli yonetim gruplari icin demo kullanicilar seed edildi.
- Seed edilen kullanicilar (idempotent):
  - `tesisyoneticisi.demo`
  - `binayoneticisi.demo`
  - `resepsiyonist.demo`
  - `restoranyoneticisi.demo`
  - `garson.demo`
- Ortak parola hash'i migration ile eklendi (tum demo kullanicilar icin ayni).

### Atamalar
- `tesisyoneticisi.demo`
  - `TesisYoneticiGrubu`
  - aktif bir tesise `TesisYoneticileri` atamasi
- `binayoneticisi.demo`
  - `BinaYoneticiGrubu`
  - aktif bir binaya `BinaYoneticileri` atamasi
- `resepsiyonist.demo`
  - `ResepsiyonistGrubu`
  - aktif bir tesise `TesisResepsiyonistleri` atamasi
- `restoranyoneticisi.demo`
  - `RestoranYoneticiGrubu`
  - aktif bir restorana `restoran.RestoranYoneticileri` atamasi
- `garson.demo`
  - `GarsonGrubu`
  - aktif bir restorana `restoran.RestoranGarsonlari` atamasi
- Tum kullanicilar icin uygun `KullaniciTesisSahiplikleri` kayitlari da seed edildi.

### Migration
- yeni migration: `20260413113000_SeedScopedUsersForManagementGroups`

### DB Uygulama
- `dotnet ef database update --context StysAppDbContext --project backend/STYS.csproj --startup-project backend/STYS.csproj`
- Sonuc: `Done.`

### Degisen Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260413113000_SeedScopedUsersForManagementGroups.cs
- changes.md

### Build Sonuclari (Tur 65)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)

## Tur 66 - Seed Demo Kullanici Parolasi Guncelleme

### Yapilanlar
- Seed edilen demo kullanicilarin ortak parolasi `1` olacak sekilde guncellendi.
- Etkilenen kullanicilar:
  - `tesisyoneticisi.demo`
  - `binayoneticisi.demo`
  - `resepsiyonist.demo`
  - `restoranyoneticisi.demo`
  - `garson.demo`

### Migration
- yeni migration: `20260413121000_UpdateSeededDemoUsersPasswordToOne`
  - Up: parola hash'i `1` icin gunceller
  - Down: onceki demo hash'ine geri alir

### DB Uygulama
- `dotnet ef database update --context StysAppDbContext --project backend/STYS.csproj --startup-project backend/STYS.csproj`
- Sonuc: `Done.`

### Degisen Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260413121000_UpdateSeededDemoUsersPasswordToOne.cs
- changes.md

## Tur 67 - Garson Login 403 (UIPolicy/UIUser) Duzeltmesi

### Sorun
- Garson login oldugunda `restoranlar`, `bildirim`, `tree`, `negotiate` gibi tum `UIController`/hub cagrilarinda 403 aliyordu.
- Kök neden: `UIPolicy` icin gereken `KullaniciTipi.UIUser` claim'i `GarsonGrubu` (ve restoran yonetici grubu) tarafinda yoktu.

### Duzeltme
- `RestoranYoneticiGrubu` ve `GarsonGrubu` gruplarina `KullaniciTipi.UIUser` rol baglantisi eklendi.
- Rol yoksa once `TODBase.Roles` icinde idempotent sekilde olusturuluyor, sonra grup-role baglantisi yapiliyor.

### Migration
- yeni migration: `20260413124000_AddUiUserRoleToRestaurantGroups`

### DB Uygulama
- `dotnet ef database update --context StysAppDbContext --project backend/STYS.csproj --startup-project backend/STYS.csproj`
- Sonuc: `Done.`

### Degisen Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260413124000_AddUiUserRoleToRestaurantGroups.cs
- changes.md

## Tur 68 - Kamp Basvurularim ve Restoran Menu Yetkilendirme Sertlestirmesi

### Sorun
- `Kamp Donemi Tesis Atamalari` menusu bazi ortamlarda rol baglantisi eksik oldugu icin herkese gorunebiliyordu.
- `Basvurularim` menusu ve `benim-basvurularim` endpoint'i ayri bir yetki ile korunmuyordu.
- Restoran menu itemleri runtime tarafinda enjekte edildigi icin `Yetkilendirme > Menuler` ekraninda kalici kayit olarak gorunmuyordu.

### Yapilanlar
- Yeni permission domain eklendi:
  - `KampBasvuruYonetimi.Menu`
  - `KampBasvuruYonetimi.View`
  - `KampBasvuruYonetimi.Manage`
- `KampBasvuruController.GetBenimBasvurularim` endpoint'i `KampBasvuruYonetimi.View` ile korundu.
- Yeni migration ile:
  - `KampBasvuruYonetimi` rolleri idempotent olarak olusturuldu.
  - Admin, Tesis Yonetici ve Resepsiyonist gruplarina uygun kamp basvuru rolleri baglandi.
  - `Basvurularim` menu item'ine `KampBasvuruYonetimi.Menu` rol baglantisi eklendi.
  - `Kamp Donemi Tesis Atamalari` menu item'i icin `KampDonemiTesisAtamaYonetimi.Menu` baglantisi zorunlu olarak idempotent sekilde tamamlandi.
  - Restoran ana menu ve alt menu itemleri `TODBase.MenuItems`/`TODBase.MenuItemRoles` tarafina kalici olarak seed edildi.
- Frontend'de runtime restoran menu enjeksiyonu kaldirildi; menu sadece backend menu agacindan geliyor.

### Migration
- yeni migration: `20260413143000_FixKampBasvurularimAndRestaurantMenuAuthorizations`
- Not: Migration olusturuldu, veritabanina `update` calistirilmadi.

### Degisen Dosyalar
- backend/StructurePermissions.cs
- backend/Kamp/Controllers/KampBasvuruController.cs
- backend/Infrastructure/EntityFramework/Migrations/20260413143000_FixKampBasvurularimAndRestaurantMenuAuthorizations.cs
- frontend/src/app/core/menu/menu-runtime.service.ts
- changes.md

### Build Sonuclari (Tur 68)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur 69 - Restoran MenuItems Kaynagini Kalici ve Generic Hale Getirme

### Tespit
- Sol menu kaynagi `GET /ui/menuitem/tree` uzerinden dogrudan `TODBase.MenuItems` tablosu.
- Runtime tarafinda manuel menu uretimi kaldirildigi icin restoran menulerinin de `MenuItems` + `MenuItemRoles` tarafinda bulunmasi gerekiyor.

### Duzeltme
- Eski yaklasima uygun idempotent bir migration eklendi:
  - `Restoran` ana menusu (top-level) yoksa olusturuyor, varsa normalize ediyor.
  - Alt menu route'larini (`restoran-yonetimi`, `restoran-masa-yonetimi`, `restoran-menu-yonetimi`, `restoran-kategori-havuzu`, `restoran-siparis-yonetimi`, `garson-servis`) route bazli bulup yoksa olusturuyor, varsa parent/order/label olarak normalize ediyor.
  - Ilgili `*.Menu` rolleri yoksa olusturuyor ve `MenuItemRoles` baglantilarini idempotent sekilde tamamliyor.
- Bu sayede restoran menuleri hem uygulama menusunde hem de `Yetkilendirme > Menuler` ekraninda kalici kayit olarak gorunur.

### Migration
- yeni migration: `20260413151000_EnsureRestaurantMenusInMenuItems`
- Not: Migration olusturuldu, veritabanina `update` calistirilmadi.

### Degisen Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260413151000_EnsureRestaurantMenusInMenuItems.cs
- changes.md

### Build Sonuclari (Tur 69)
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)

## Tur 70 - Garson/Restoran Yonetici Icin Kamp Menu Gorunurlugu Kisitlama

### Sorun
- `garson.01` kullanicisinin `Kamp Yonetimi > Tesis Atamalari` menusunu gormesi beklenen davranis degildi.
- Kök neden ortamlar arasi veri farkliliginda:
  - restoran gruplarina kamp domain rolleri verilmis olabilmesi
  - `kamp-donemi-atamalari` menu-role baginin eksik kalabilmesi

### Duzeltme
- Yeni migration eklendi:
  - `GarsonGrubu` ve `RestoranYoneticiGrubu` uzerindeki tum kamp domain rolleri temizleniyor.
  - `kamp-donemi-atamalari` menu item'i icin `KampDonemiTesisAtamaYonetimi.Menu` baglantisi route bazli idempotent olarak zorunlu hale getiriliyor.

### Migration
- yeni migration: `20260413154000_RestrictKampMenusForRestaurantGroups`
- Not: Migration olusturuldu, veritabanina `update` calistirilmadi.

### Degisen Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260413154000_RestrictKampMenusForRestaurantGroups.cs
- changes.md

## Tur 71 - Duplicate Grup Kayitlarinda Garson/Restoran Yonetici Rol Normalizasyonu

### Sorun
- `garson.01` hala kamp menusu gorebiliyor ve restoran menulerini goremiyordu.
- Muhtemel neden: ayni isimli birden fazla `GarsonGrubu`/`RestoranYoneticiGrubu` kaydi olan ortamlarda onceki migrationlar tek grup ID uzerinden calisiyordu.

### Duzeltme
- Yeni migration eklendi ve grup adina sahip tum group ID'leri hedeflenerek normalize edildi:
  - Tum `GarsonGrubu` kayitlarina gerekli restoran + `KullaniciTipi.UIUser` rolleri eklendi.
  - Tum `RestoranYoneticiGrubu` kayitlarina gerekli restoran + `KullaniciTipi.UIUser` rolleri eklendi.
  - Bu iki grup tipinden tum kamp domain rolleri temizlendi.
  - `kamp-donemi-atamalari` menu item'i icin `KampDonemiTesisAtamaYonetimi.Menu` bagi route bazli tekrar zorunlu kilindi.

### Migration
- yeni migration: `20260413162000_NormalizeRestaurantGroupRoleAssignments`
- Not: Migration olusturuldu, veritabanina `update` calistirilmadi.

### Degisen Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260413162000_NormalizeRestaurantGroupRoleAssignments.cs
- changes.md

## Tur 72 - Restoran Yetkilerini Micro Seviyeye Ayirma

### Talep
- Restoran yetkilendirmelerinde ayni rollerin tekrar kullanilmasi yerine daha ince taneli yonetim istendi.

### Yapilanlar
- Yeni permission domain'leri eklendi:
  - `RestoranKategoriHavuzuYonetimi.Menu/View/Manage`
  - `GarsonServisYonetimi.Menu/View/Manage`
- Controller permission ayrimi yapildi:
  - `GarsonServisController` endpointleri `GarsonServisYonetimi` alanina tasindi.
  - `RestoranMenuKategorileriController` icindeki `global` + `atama` endpointleri `RestoranKategoriHavuzuYonetimi` alanina tasindi.
- Menu-role ayrimi icin yeni migration:
  - `restoran-kategori-havuzu` artik `RestoranKategoriHavuzuYonetimi.Menu` ile korunuyor.
  - `garson-servis` artik `GarsonServisYonetimi.Menu` ile korunuyor.
  - Eski baglar (`RestoranMenuYonetimi.Menu` / `RestoranSiparisYonetimi.Menu`) bu iki menu item icin temizleniyor.
- Grup bazli rol dagitimi idempotent eklendi:
  - Admin + Tesis Yonetici: yeni iki domainin tum rolleri
  - Restoran Yonetici: yeni iki domainin tum rolleri
  - Garson: sadece `GarsonServisYonetimi` tum rolleri

### Migration
- yeni migration: `20260413170000_SplitRestaurantPermissionsForMicroAuthorization`
- Not: Migration olusturuldu, veritabanina `update` calistirilmadi.

### Degisen Dosyalar
- backend/StructurePermissions.cs
- backend/RestoranYonetimi/GarsonServis/Controllers/GarsonServisController.cs
- backend/RestoranYonetimi/RestoranMenuKategorileri/Controllers/RestoranMenuKategorileriController.cs
- backend/Infrastructure/EntityFramework/Migrations/20260413170000_SplitRestaurantPermissionsForMicroAuthorization.cs
- changes.md

## Tur 73 - Garson Servis Menusu Parent Yetki Duzeltmesi

### Sorun
- Garson garson-servis route'una gidebiliyordu ancak sol menu'de goremiyordu.
- Neden: Restoran parent menu item'i icin gerekli role bagi yoksa cocuk menu gizleniyor.

### Duzeltme
- Restoran ana menu item'ine GarsonServisYonetimi.Menu role bagi idempotent olarak eklendi.

### Migration
- yeni migration: 20260413173000_AddGarsonServisMenuRoleToRestoranRoot 
- Not: Migration olusturuldu, veritabanina update calistirilmadi.

### Degisen Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260413173000_AddGarsonServisMenuRoleToRestoranRoot.cs
- changes.md

## Tur 74 - Garson Grubuna Odeme Ozeti Gorme Yetkisi

### Talep
- `GarsonGrubu` kullanicilari odeme ozeti akisini goruntuleyebilsin.

### Duzeltme
- `GarsonGrubu`na `RestoranOdemeYonetimi.View` rolu idempotent migration ile eklendi.
- Rol sistemde yoksa migration tarafinda olusturulup sonrasinda grup-role baglantisi kurulur.
- Tum `GarsonGrubu` kayitlari (duplicate grup durumlari dahil) hedeflenir.

### Migration
- yeni migration: `20260413174500_GrantGarsonOdemeOzetiViewPermission`
- Not: Migration olusturuldu, veritabanina `update` calistirilmadi.

### Degisen Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260413174500_GrantGarsonOdemeOzetiViewPermission.cs
- changes.md

## Tur 75 - RestoranYoneticiGrubu Marker Yetkisi Ekleme

### Talep
- `RestoranYoneticiGrubu` icin `KullaniciAtama.RestoranYoneticisiAtanabilir` marker rolunun eklenmesi istendi.

### Duzeltme
- Idempotent migration eklendi:
  - Marker rolu yoksa olusturur.
  - `RestoranYoneticiGrubu` adina sahip tum aktif grup kayitlarina rol baglar.

### Migration
- yeni migration: `20260413180000_AddRestoranYoneticisiAtanabilirToRestoranYoneticiGrubu`
- Not: Migration olusturuldu, veritabanina `update` calistirilmadi.

### Degisen Dosyalar
- backend/Infrastructure/EntityFramework/Migrations/20260413180000_AddRestoranYoneticisiAtanabilirToRestoranYoneticiGrubu.cs
- changes.md

## Tur 76 - Restoran Yonetici Icin Rezervasyon Tesisler Endpoint 403 Duzeltmesi

### Sorun
- `restoranyonetici` kullanicisi `GET /api/ui/rezervasyon/tesisler` cagrısında 403 aliyordu.

### Duzeltme
- Endpoint permission'i OR seklinde genisletildi:
  - Once: `RezervasyonYonetimi.View`
  - Simdi: `RezervasyonYonetimi.View` veya `RestoranYonetimi.View`
- Boylesiyle restoran yoneticisi gereksiz tam rezervasyon rolune sahip olmadan tesis listesini cekebiliyor.

### Degisen Dosyalar
- backend/Rezervasyonlar/Controllers/RezervasyonController.cs
- changes.md

## Tur 77 - Aktif Rezervasyon Arama Endpoint 404 Duzeltmesi

### Sorun
- `GET /api/restoran-siparisleri/aktif-rezervasyonlar?tesisId=...` cagrisi 404 donuyordu.
- Neden: frontend cagrisi vardi ancak backend controller'da bu route tanimli degildi.

### Duzeltme
- `RestoranOdemeleriController` icine endpoint eklendi:
  - `GET /api/restoran-siparisleri/aktif-rezervasyonlar`
  - Parametreler: `tesisId`, `q`
  - Permission: `RestoranOdemeYonetimi.View`
- Servis katmanindaki mevcut `SearchAktifRezervasyonlarAsync` metodu endpoint'e baglandi.

### Degisen Dosyalar
- backend/RestoranYonetimi/RestoranOdemeleri/Controllers/RestoranOdemeleriController.cs
- changes.md

## Tur 78 - Odaya Ekle Rezervasyon Arama Filtresi Duzeltmesi

### Sorun
- Akif konaklama rezervasyonu olmasina ragmen `aktif-rezervasyonlar` aramasinda sonuc donmuyordu.
- Saat bazli `UtcNow` karsilastirmasi, ozellikle gun icindeki kayitlarda filtreyi fazla daraltiyordu.

### Duzeltme
- Rezervasyon arama filtresi gun bazli hale getirildi:
  - `DateTime.Today` kullanildi.
  - `GirisTarihi.Date <= today && CikisTarihi.Date >= today`
- Odaya ekleme kuraliyla uyumlu olacak sekilde sadece `CheckInTamamlandi` durumundaki rezervasyonlar listelenir hale getirildi.
- Oda no filtrelemesinde null-guvenli kosul eklendi.

### Degisen Dosyalar
- backend/RestoranYonetimi/RestoranOdemeleri/Repositories/RestoranOdemeRepository.cs
- changes.md

## Tur 79 - Odaya Ekle Rezervasyon Aramasinda Tarih Filtresi Gevsetildi

### Tespit
- `RZV-20260413163903-56CFBE` kaydi veritabaninda mevcut:
  - `TesisId=4`
  - `RezervasyonDurumu=CheckInTamamlandi`
  - `GirisTarihi=14/04/2026 14:00`
  - `CikisTarihi=15/04/2026 10:00`
- Bugun `13/04/2026` oldugu icin onceki gun filtresi nedeniyle aramada listelenmiyordu.

### Duzeltme
- `aktif-rezervasyonlar` aramasinda tarih kisiti kaldirildi.
- Arama artik odaya-ekle is kurali ile uyumlu sekilde sadece su kosullari uygular:
  - ayni tesis
  - aktif rezervasyon
  - `RezervasyonDurumu == CheckInTamamlandi`
  - `Iptal` olmayan

### Degisen Dosyalar
- backend/RestoranYonetimi/RestoranOdemeleri/Repositories/RestoranOdemeRepository.cs
- changes.md

## Tur 80 - Rezervasyon Odeme Ekraninda Restoran Ucretlendirmesi Ayrimi

### Talep
- `OdayaEkle` kaynakli hareketlerin `-85,00` gibi negatif tahsilat gorunmesi yerine,
- ek hizmetlerden ayri bir `Restoran Ucretlendirmeleri` panelinde daha anlasilir gosterilmesi istendi.

### Duzeltme
- Rezervasyon odeme dialogunda hesaplama ve gorunum ayrildi:
  - `Restoran Ucretlendirmeleri` (OdayaEkle/negatif kayitlar) ayri panelde listelenir.
  - `Odeme Islemleri` tablosu yalnizca tahsilat kayitlarini gosterir.
  - Ozet kartlari gorunumde su sekilde hesaplanir:
    - `Toplam = Konaklama + Ek Hizmet + Restoran Ucretlendirmeleri`
    - `Odenen = Tahsilat Toplami`
    - `Kalan = Toplam - Odenen`
- Odeme kaydet butonu ve odeme tutari varsayilani da ayni gorunum hesaplarini kullanacak sekilde guncellendi.

### Degisen Dosyalar
- frontend/src/app/pages/rezervasyon-yonetimi/rezervasyon-yonetimi.ts
- frontend/src/app/pages/rezervasyon-yonetimi/rezervasyon-yonetimi.html
- changes.md

## Tur 81 - Restoran Ucretlendirmeleri Paneli Acilir/Kapanir Yapildi

### Talep
- Rezervasyon odeme dialogundaki yeni `Restoran Ucretlendirmeleri` panelinin acilir/kapanir olmasi istendi.

### Duzeltme
- Panel icin ayri state eklendi: `odemeRestoranUcretPanelExpanded`.
- Baslik alanina `Goster/Gizle` butonu eklendi.
- Liste alani sadece panel acik oldugunda render edilir hale getirildi.
- Dialog acilis/kapanisinda panel state'i resetlenir.

### Degisen Dosyalar
- frontend/src/app/pages/rezervasyon-yonetimi/rezervasyon-yonetimi.ts
- frontend/src/app/pages/rezervasyon-yonetimi/rezervasyon-yonetimi.html
- changes.md

## Tur 82 - Erisim Teshis Modullerine Kamp + Restoran Kapsami Eklendi

### Talep
- Kamp ve restoran isleri icin erisim teshislerinde modul secilebilir olmasi istendi.

### Duzeltme
- `ErisimTeshisModulTanimlari` listesi genisletildi.
- Kamp tarafina eklenen teshis modulleri:
  - `kamp-tarife-yonetimi` (`/kamp-tarifeleri`)
  - `kamp-basvuru-yonetimi` (`/kamp-basvurularim`)
- Restoran tarafina eklenen teshis modulleri:
  - `restoran-yonetimi`
  - `restoran-kategori-havuzu-yonetimi`
  - `restoran-masa-yonetimi`
  - `restoran-menu-yonetimi`
  - `restoran-siparis-yonetimi`
  - `garson-servis-yonetimi`

### Degisen Dosyalar
- backend/ErisimTeshis/ErisimTeshisModulTanimlari.cs
- changes.md

## Tur 83 - Kamp Programi Basvuru Limit Parametresi ve Basvuru Kurali

### Talep
- Kamp programi icine kisi basi en fazla basvuru adedi parametresi eklensin.
- Kamp basvuru akisinda bu limite gore kontrol calissin.

### Duzeltme
- `KampProgrami` modeline `MaksimumBasvuruSayisi` alani eklendi (varsayilan: 1).
- Kamp programi yonetim ekranina yeni alan eklendi:
  - Dialog input: `Maks. Basvuru (Kisi Basi)`
  - Liste kolonunda gosterim eklendi.
- Servis validasyonu:
  - Kamp programi kayit/guncellemede `MaksimumBasvuruSayisi` araligi `1-20`.
- Basvuru kurali:
  - Basvuru olusturmada, ayni `KampProgrami` icin ayni basvuru sahibi adina
    `IptalEdildi/Reddedildi` disindaki basvuru sayisi limite ulasmissa islem engellenir.
  - Onizleme asamasinda da ayni kontrol hata listesine yansitilir.
- `LoadContextAsync` kamp donemini `KampProgrami` ile include edecek sekilde guncellendi.

### Migration
- yeni migration: `20260414083750_AddKampProgramiMaksimumBasvuruSayisi`
- Not: Migration olusturuldu, veritabanina update calistirilmadi.

### Degisen Dosyalar
- backend/Kamp/Entities/KampProgrami.cs
- backend/Kamp/Dto/KampProgramiDto.cs
- backend/Kamp/Services/KampProgramiService.cs
- backend/Kamp/Services/KampBasvuruService.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Infrastructure/EntityFramework/Migrations/20260414083750_AddKampProgramiMaksimumBasvuruSayisi.cs
- backend/Infrastructure/EntityFramework/Migrations/20260414083750_AddKampProgramiMaksimumBasvuruSayisi.Designer.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-programi-dialog.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-programi-tanim-yonetimi.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-programi-tanim-yonetimi.html
- changes.md

## Tur 84 - Kamp Basvurusunda Coklu Tesis + Donem Tercihleri

### Talep
- Tek basvuru icinde birden fazla tesis ve birden fazla kamp donemi tercihi girilebilsin.
- Basvuru ekraninda tesisler alt alta listelensin; her tesis icin 5 tercih dropdownu bulunsun.
- Kabul secimi ileride puanlama/kural motoruna birakilsin.

### Duzeltme
- Backend:
  - `KampBasvuru` icine tercih koleksiyonu eklendi.
  - Yeni entity: `KampBasvuruTercih` (`KampBasvuruId`, `TercihSirasi`, `KampDonemiId`, `TesisId`, `KonaklamaBirimiTipi`).
  - `KampBasvuruRequestDto` ve `KampBasvuruDto` icine `Tercihler` alani eklendi.
  - `KampBasvuruService`:
    - istek icindeki tercihler normalize edilip tekilleştiriliyor,
    - en az bir tercih zorunlu hale getirildi,
    - tercihlerin donem/tesis atama ve basvuruya aciklik kontrolleri eklendi,
    - basvuru kaydinda tum tercihler `KampBasvuruTercihleri` tablosuna yaziliyor,
    - ilk tercih mevcut hesaplama/puanlama akisi icin birincil secim olarak kullaniliyor.
  - `StysAppDbContext`:
    - `DbSet<KampBasvuruTercih>` eklendi,
    - tablo/index/iliski konfigurasyonlari eklendi.
- Frontend:
  - Kamp basvuru DTO’larina `tercihler` modeli eklendi.
  - `kamp-basvuru` ekraninda tesis bazli satir + 5 tercih dropdown yapisi eklendi.
  - Secilen tercihlerden request payload icin `tercihler[]` listesi uretiliyor.
  - Ilk tercih otomatik olarak birincil donem/tesis secimi olarak senkronize ediliyor.

### Migration
- yeni migration: `20260414090803_AddKampBasvuruCokluTercih`
- Not: Migration olusturuldu, veritabanina update calistirilmadi.

### Degisen Dosyalar
- backend/Kamp/Entities/KampBasvuru.cs
- backend/Kamp/Entities/KampBasvuruTercih.cs
- backend/Kamp/Dto/KampBasvuruRequestDto.cs
- backend/Kamp/Dto/KampBasvuruDto.cs
- backend/Kamp/Services/KampBasvuruService.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Infrastructure/EntityFramework/Migrations/20260414090803_AddKampBasvuruCokluTercih.cs
- backend/Infrastructure/EntityFramework/Migrations/20260414090803_AddKampBasvuruCokluTercih.Designer.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- frontend/src/app/pages/kamp-yonetimi/kamp-yonetimi.dto.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru.ts
- frontend/src/app/pages/kamp-yonetimi/kamp-basvuru.html
- changes.md


---

## Tur - Muhasebe Faz 1 (Cari/Kasa/Banka/TahsilatOdeme)

### Yeni Backend Dosyalari
- `backend/Muhasebe/CariKartlar/*` (Entities, Dtos, Repositories, Services, Controllers, Mapping)
- `backend/Muhasebe/CariHareketler/*`
- `backend/Muhasebe/KasaHareketleri/*`
- `backend/Muhasebe/BankaHareketleri/*`
- `backend/Muhasebe/TahsilatOdemeBelgeleri/*`
- `backend/Infrastructure/EntityFramework/Migrations/20260414184421_AddAccountingPhase1Core.cs` (+ Designer)

### Guncellenen Backend Dosyalari
- `backend/Infrastructure/EntityFramework/StysAppDbContext.cs`
  - Muhasebe DbSet'leri eklendi
  - `muhasebe` schema tablo/iliski/index/precision config'leri eklendi
- `backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs`
- `backend/Program.cs`
  - repository/service DI kayitlari eklendi
- `backend/StructurePermissions.cs`
  - Faz 1 muhasebe permission domainleri eklendi

### Migration Icerigi
- Muhasebe Faz 1 tablolari:
  - `muhasebe.CariKartlar`
  - `muhasebe.CariHareketler`
  - `muhasebe.KasaHareketleri`
  - `muhasebe.BankaHareketleri`
  - `muhasebe.TahsilatOdemeBelgeleri`
- FK/index/unique/precision
- TODBase tarafinda Faz 1 role + menu + menuItemRole + admin/tesis manager rol atama seed SQL'i

### Yeni Frontend Dosyalari
- `frontend/src/app/pages/muhasebe/cari-kartlar/*`
- `frontend/src/app/pages/muhasebe/cari-hareketler/*`
- `frontend/src/app/pages/muhasebe/kasa-hareketleri/*`
- `frontend/src/app/pages/muhasebe/banka-hareketleri/*`
- `frontend/src/app/pages/muhasebe/tahsilat-odeme-belgeleri/*`

### Guncellenen Frontend Dosyalari
- `frontend/src/app.routes.ts`
  - Faz 1 muhasebe route'lari eklendi:
    - `/muhasebe/cari-kartlar`
    - `/muhasebe/cari-hareketler`
    - `/muhasebe/kasa-hareketleri`
    - `/muhasebe/banka-hareketleri`
    - `/muhasebe/tahsilat-odeme-belgeleri`

### Build
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur - Muhasebe Faz 1 Seed Verisi

### Yeni Dosya
- `backend/Infrastructure/EntityFramework/Migrations/20260414190500_SeedAccountingPhase1Data.cs`

### Icerik
- `muhasebe` faz1 tablolari icin idempotent seed SQL eklendi:
  - Cari kartlar
  - Cari hareketler
  - Kasa hareketleri
  - Banka hareketleri
  - Tahsilat/odeme belgeleri
- `CreatedBy = migration_seed_accounting_phase1_v1` etiketi ile izlendi.
- `Down` icinde sadece bu etiketli seed kayitlari geri alinir.

### Build
- Backend: BASARILI

## Tur - Muhasebe Faz 2 (Tasinir/Depo/Stok)

### Yeni Backend Dosyalari
- `backend/Muhasebe/TasinirKodlari/*` (Entities, Dtos, Repositories, Services, Controllers, Mapping)
- `backend/Muhasebe/TasinirKartlari/*`
- `backend/Muhasebe/Depolar/*`
- `backend/Muhasebe/StokHareketleri/*`
- `backend/Infrastructure/EntityFramework/Migrations/20260415073101_AddAccountingPhase2Inventory.cs` (+ Designer)

### Guncellenen Backend Dosyalari
- `backend/Infrastructure/EntityFramework/StysAppDbContext.cs`
  - Faz 2 DbSet'leri eklendi
  - `muhasebe` schema Faz 2 tablo/iliski/index/precision config'leri eklendi
- `backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs`
- `backend/Program.cs`
  - Faz 2 repository/service DI kayitlari eklendi
- `backend/StructurePermissions.cs`
  - Faz 2 permission domainleri eklendi:
    - `TasinirKodYonetimi`
    - `TasinirKartYonetimi`
    - `DepoYonetimi`
    - `StokHareketYonetimi`

### Faz 2 Migration Icerigi
- Muhasebe Faz 2 tablolari:
  - `muhasebe.TasinirKodlar`
  - `muhasebe.TasinirKartlar`
  - `muhasebe.Depolar`
  - `muhasebe.StokHareketleri`
- FK/index/unique/precision tanimlari
- TODBase tarafinda Faz 2 role + menu + menuItemRole + admin/tesis manager rol atama seed SQL'i

### Import Yaklasimi (Excel Referans Zemini)
- Tasinir kodlari icin backend import API eklendi:
  - `POST /api/muhasebe/tasinir-kodlari/import`
- Request modeli: `ImportTasinirKodlariRequest`
  - satir bazli kod aktarimi
  - mevcut kodlari guncelleme opsiyonu
  - import disindakileri pasife cekme opsiyonu
- Duplicate engeli: `TamKod` bazli benzersizlik ve idempotent guncelleme mantigi.

### Yeni Frontend Dosyalari
- `frontend/src/app/pages/muhasebe/tasinir-kodlari/*`
- `frontend/src/app/pages/muhasebe/tasinir-kartlari/*`
- `frontend/src/app/pages/muhasebe/depolar/*`
- `frontend/src/app/pages/muhasebe/stok-hareketleri/*`

### Guncellenen Frontend Dosyalari
- `frontend/src/app.routes.ts`
  - Faz 2 route'lari eklendi:
    - `/muhasebe/tasinir-kodlari`
    - `/muhasebe/tasinir-kartlari`
    - `/muhasebe/depolar`
    - `/muhasebe/stok-hareketleri`

### Build
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur - Tasinir Kodlari Resmi XLS Referansi

### Yapilanlar
- `Guncel-Tasinir-Kod-Listesi-15.06.2023-.xls` dosyasi parse edilerek resmi liste dikkate alindi.
- Filtreleme kurali:
  - `Hesap Kodu` numerik olan satirlar
  - Aciklama/Ad alani dolu olan satirlar
- Sonuc:
  - 3270 benzersiz `TamKod` uretildi.

### Yeni Migration
- `backend/Infrastructure/EntityFramework/Migrations/20260415095000_SeedOfficialTasinirKodlariFromXls.cs`

### Migration Davranisi
- Resmi XLS kaynakli 3270 kayit `muhasebe.TasinirKodlar` tablosuna upsert edilir.
- `TamKod` bazli eslesme yapilir.
- Duzey/ad alanlari guncellenir.
- UstKod iliskisi `UstTamKod` uzerinden tekrar kurulur.
- Seed etiketi: `migration_seed_accounting_phase2_xls_v1`.

### Not
- Excelde 6 segmentli kodlar bulundugu icin (`150.13.04.01.01.01` gibi),
  entity'deki `Duzey1..Duzey5` alanlarina ilk 5 segment yazilir; tam hiyerarsi `TamKod` ve `UstTamKod` uzerinden korunur.

## Tur - Muhasebe Listelerinde Paging

### Backend
- Muhasebe Faz 1 ve Faz 2 controller list endpointlerine `paged` endpointleri eklendi:
  - `GET /api/muhasebe/cari-kartlar/paged`
  - `GET /api/muhasebe/cari-hareketler/paged`
  - `GET /api/muhasebe/kasa-hareketleri/paged`
  - `GET /api/muhasebe/banka-hareketleri/paged`
  - `GET /api/muhasebe/tahsilat-odeme-belgeleri/paged`
  - `GET /api/muhasebe/tasinir-kodlari/paged`
  - `GET /api/muhasebe/tasinir-kartlari/paged`
  - `GET /api/muhasebe/depolar/paged`
  - `GET /api/muhasebe/stok-hareketleri/paged`
- Filtreli endpointlerde mevcut filtreler korundu:
  - `cariKartId`
  - `depoId`

### Frontend
- Muhasebe ekranlarinda servis katmanina `getPaged(pageNumber, pageSize, ...)` metotlari eklendi.
- Asagidaki sayfalarda PrimeNG lazy paginator aktif edildi:
  - `cari-kartlar`
  - `cari-hareketler`
  - `kasa-hareketleri`
  - `banka-hareketleri`
  - `tahsilat-odeme-belgeleri`
  - `tasinir-kodlari`
  - `tasinir-kartlari`
  - `depolar`
  - `stok-hareketleri`
- Sayfalara `pageNumber`, `pageSize`, `totalRecords`, `onLazyLoad` eklendi.

### Build
- Backend: BASARILI (`dotnet build backend/STYS.csproj`)
- Frontend: BASARILI (`npm run build`)

## Tur - Tasinir Kodlari Full Cache

### Yapilanlar
- `TasinirKodService` icin full-cache lookup modeli eklendi.
- Arama/sayfalama icin her sorguda DB'ye gitmek yerine tum tasinir kodlari bellekte tutulup memory uzerinden filtreleme yapiliyor.
- Yeni servis metodu eklendi:
  - `GetPagedForLookupAsync(PagedRequest request, string? query, CancellationToken cancellationToken)`
- `TasinirKodlariController` `GET /api/muhasebe/tasinir-kodlari/paged` endpoint'i bu yeni cache'li metoda baglandi.
- Cache invalidation eklendi:
  - `AddAsync`
  - `UpdateAsync`
  - `DeleteAsync`
  - `ImportAsync`
- `Program.cs` icine `AddMemoryCache()` eklendi.

### Teknik Not
- Kod sorgusunda (`sadece rakam + nokta`) eslesme:
  - Tam kod
  - Alt kirilim prefix (`150.01.01.07.*`)
- Metin sorgusunda `TamKod/Ad` case-insensitive contains devam ediyor.

## Tur - Redis ve Distributed Cache Entegrasyonu

### Altyapi
- `docker-compose.yml` icine `redis` servisi eklendi (`redis:7-alpine`).
- Redis kaliciligi icin `redis_data` volume eklendi.
- Backend servisine Redis env degiskenleri eklendi:
  - `Redis__Configuration`
  - `Redis__InstanceName`
- Backend `depends_on` listesine `redis` (healthcheck) eklendi.

### Konfigurasyon
- `backend/appsettings.json` ve `backend/appsettings.Development.json` dosyalarina `Redis` bolumu eklendi:
  - `Configuration`
  - `InstanceName`
- `.env` ve `.env.example` dosyalarina eklendi:
  - `STYS_REDIS_CONFIGURATION`
  - `STYS_REDIS_INSTANCE_NAME`
  - `STYS_REDIS_HOST_PORT`

### Backend Cache Guncellemesi
- `TasinirKodService` cache katmani `IMemoryCache` yerine `IDistributedCache` ile Redis tabanli hale getirildi.
- Arama/paging icin tum tasinir kodlari Redis'te tek liste olarak cacheleniyor; filtreleme memory uzerinde yapiliyor.
- Invalidasyonlar eklendi:
  - `AddAsync`
  - `UpdateAsync`
  - `DeleteAsync`
  - `ImportAsync`
- `Program.cs` icinde `AddStackExchangeRedisCache` kaydi eklendi.
- `backend/STYS.csproj` icine `Microsoft.Extensions.Caching.StackExchangeRedis` paketi eklendi.

### Not
- Build denemesinde kod hatasi degil, calisan `STYS` prosesi ve Visual Studio dosya kilidi nedeniyle kopyalama hatasi alindi.

## Tur - Restoran Servislerinde BaseService ve Repository Uyumlamasi

### Yapilanlar
- Restoran modulu servis interface'leri IBaseRdbmsService<Dto, Entity, int> cizgisine alindi:
  - IRestoranService`r
  - IRestoranMasaService`r
  - IRestoranMenuKategoriService`r
  - IRestoranMenuUrunService`r
  - IRestoranSiparisService`r
  - IRestoranOdemeService`r
- Servis implementasyonlari BaseRdbmsService<Dto, Entity, int> uzerinden turetildi.
- Restoran VSA servislerinde ana entity CRUD akislari kendi repository'leri uzerinden calisacak sekilde guncellendi.
- RestoranService, RestoranMasaService, RestoranMenuKategoriService, RestoranMenuUrunService, RestoranSiparisService, RestoranOdemeService icinde dogrudan entity DbSet kullanimi azaltildi; ilgili repository'ler aktif kullanildi.

### Dogrulama
- Backend derleme: BASARILI (dotnet build backend/STYS.csproj).

## Tur - Restoran Servislerinde Redundant CRUD Temizligi

- RestoranMasaService, RestoranMenuKategoriService, RestoranMenuUrunService icinde GetList/GetById/Create/Update/Delete tekrar eden metotlar BaseService akisina tasindi.
- Bu servislerde CRUD artik GetAllAsync/WhereAsync/GetByIdAsync/AddAsync/UpdateAsync/DeleteAsync override'lari ile yuruyor.
- Controllerlar request modellerini DTO'ya mapleyip BaseService metotlarini cagiracak sekilde guncellendi.
- Gereksiz servis arayuzu metotlari kaldirildi (IBaseRdbmsService ile cakisanlar).
- Build dogrulamasi: BASARILI (dotnet build backend/STYS.csproj).

## Tur - BaseService Redundant Imza Temizligi (Proje Geneli)

- IBaseRdbmsService'ten tureyen interface'lerde base CRUD ile cakisan redundant imzalar temizlendi.
- Temizlenen alanlar:
  - ackend/RestoranYonetimi/Restoranlar/Services/IRestoranService.cs`r
  - ackend/RestoranYonetimi/RestoranSiparisleri/Services/IRestoranSiparisService.cs`r
  - ackend/RestoranYonetimi/RestoranMenuUrunleri/Services/IRestoranMenuUrunService.cs`r
  - ackend/Kamp/Services/IKampDonemiService.cs`r
- Restoran tarafinda controllerlar ve servisler base metot akisini kullanacak sekilde duzenlendi (GetAll/Where/GetById/Add/Update/Delete).
- Kamp donemi controller cagrilari base imzalarla uyumlu hale getirildi (GetByIdAsync, GetPagedAsync).
- Dogrulama: dotnet build backend/STYS.csproj BASARILI.


## Tur - License Fingerprint Mismatch Duzeltmesi

- 	ools/Tod.LicenseGenerator/Program.cs icindeki fingerprint hesaplama algoritmasi backend runtime ile birebir uyumlu hale getirildi.
- Onceki generator sirasi (ENV|INSTANCE|MACHINE|OS|CUSTOMER|MARKER) backend ile farkliydi; yeni algoritma backend ile ayni:
  - PROFILE:<PROFILE>|ENV|INSTANCE|CUSTOMER|MARKER (+ PhysicalServer ise |MACHINE|OS).
- Generator'a fingerprint profili secimi eklendi (PhysicalServer/Container).
- Tool build dogrulamasi: BASARILI (dotnet build tools/Tod.LicenseGenerator/Tod.LicenseGenerator.csproj).


## Tur - Tod.LicenseGenerator Mini Arayuz

- 	ools/Tod.LicenseGenerator icine gui komutu ile acilan kucuk WinForms arayuz eklendi.
- Arayuzde su islemler var:
  - Lisans alanlarini girme (ProductCode, CustomerCode, Environment, Instance, profil, marker, moduller, bitis tarihi)
  - Fingerprint Hesapla`r
  - Lisans Uret (JSON dosyasina kaydet)
  - Public Key Goster`r
- Fingerprint ve imzalama mantigi CLI ile ortak LicenseGeneratorCore sinifina tasindi.
- Tod.LicenseGenerator.csproj WinForms icin guncellendi (
et10.0-windows, UseWindowsForms=true).
- Build dogrulamasi: BASARILI (dotnet build tools/Tod.LicenseGenerator/Tod.LicenseGenerator.csproj).

## Tur - Lisanslama Sertlestirme (Kontrollu Kilit + Production Hardening)

### Startup / Middleware
- `platform/TOD.Platform.Licensing.AspNetCore/LicenseApplicationBuilderExtensions.cs`
  - Startup validasyonu ortam+config bazli hale getirildi.
  - `Licensing:FailFastOnStartupInProduction=false` varsayilaninda Production'da uygulama ayakta kalir (kontrollu kilit).
  - Gecersiz lisans durumda business endpoint'leri middleware kapatir, lisans yenileme endpoint'leri acik kalir.
- `platform/TOD.Platform.Licensing.AspNetCore/LicenseGuardMiddleware.cs`
  - ExcludedPaths eslesmesi daraltildi:
    - Varsayilan: exact match
    - Prefix: sadece `/*` ile biten tanimlarda segment-aware

### Lisans Yenileme Endpointleri
- `backend/Licensing/Controllers/ApiLicenseController.cs` eklendi.
  - `GET /api/license/status`
  - `POST /api/license/upload`
- Mevcut `ui/license` akisi bozulmadan korundu.

### Config Sertlestirme
- `backend/appsettings.json` ve `backend/appsettings.Development.json` guncellendi:
  - `ExcludedPaths` daraltildi:
    - `/health/*`
    - `/auth/*`
    - `/ui/license/*`
    - `/api/license/status`
    - `/api/license/upload`
  - `FailFastOnStartupInProduction` eklendi (default `false`).
  - `RequireIntegrityHashesInProduction` ve `IntegrityHashes` eklendi.

### Service-Layer Enforcement (Ensure...)
- `backend/Licensing/StysLicensedModules.cs` eklendi (Kamp/Rezervasyon/Restoran/OdaTemizlik/Bildirim).
- Asagidaki servislerde bool kontrol yerine `EnsureModuleLicensedAsync(...)` ile zorlayici kontrol eklendi:
  - `RezervasyonService` (kaydet, odeme kaydet, odeme raporu)
  - `RestoranSiparisService` (add/update/durum)
  - `RestoranOdemeService` (odeme olusturma, aktif rezervasyon arama)
  - `GarsonServisService` (tum operasyonel metotlar)
  - `OdaTemizlikService` (baslat/tamamla)
  - `BildirimService` (okuma/yazma/publish akislari)

### Controller-Level Modul Lisanslama
- `RequiresLicensedModule` uygulamasi STYS modullerine yayildi:
  - `KampBasvuruController` -> Kamp
  - `KampTarifeYonetimController` -> Kamp (const'a cekildi)
  - `RezervasyonController` -> Rezervasyon
  - `RestoranlarController` -> Restoran
  - `OdaTemizlikController` -> OdaTemizlik
  - `BildirimController` -> Bildirim

### Middleware Disi Akislar
- `backend/Bildirimler/Hubs/BildirimHub.cs`
  - `OnConnectedAsync` icinde lisans + modul zorlamasi eklendi.
  - Method-level ornek icin `PingAsync()` eklendi.
- `backend/Licensing/Services/LicenseAwareMaintenanceHostedService.cs` eklendi.
  - Periyodik worker akisinda lisans/modul kontrolu zorunlu.

### Public Key / Integrity Hardening
- `platform/TOD.Platform.Licensing/EcdsaLicenseSignatureVerifier.cs`
  - Production ortaminda public key override runtime'da da bloklandi (defense-in-depth).
- `platform/TOD.Platform.Licensing/AssemblyIntegrityChecker.cs`
  - `IntegrityHashes` config'inden hash alabilen efektif kontrol eklendi.
  - Production'da `RequireIntegrityHashesInProduction=true` iken hash listesi bos ise integrity fail olur.

### Dogrulama
- `dotnet build backend/STYS.csproj -o backend/.tmp-build` BASARILI.
- Not: normal output path'e build denemesinde calisan `STYS` prosesi nedeniyle file lock alinabiliyor.

## Tur - Muhasebe Hesap Plani + Tasinir Kod Refactor (Kod + Parent)

### Yapilanlar
- `TasinirKod` modeli `Duzey1Kod..Duzey5Kod` alanlarindan cikarilip `Kod` + `UstKodId` hiyerarsisine alindi.
- `TasinirKod` backend DTO/request/import modelleri yeni yapıya uyarlandi.
- `TasinirKodService` normalize/validasyon/import ve lookup filtreleri `Kod` alanini baz alacak sekilde guncellendi.
- Muhasebe icin yeni modül eklendi:
  - `MuhasebeHesapPlanlari` (entity/dto/repository/service/controller/mapping)
  - API: `/api/muhasebe/hesap-plani` (list/tree/paged/getById/create/update/delete)
- Yeni permission alani eklendi:
  - `StructurePermissions.MuhasebeHesapPlaniYonetimi` (Menu/View/Manage)
- Erisim teshis modul listesine muhasebe hesap plani modulu eklendi.

### Frontend
- `tasinir-kodlari` sayfasi `Kod` alanina gore guncellendi:
  - tablo kolonu
  - dialog form alani
  - create/update payload
  - ornek import payload
- Yeni sayfa eklendi: `muhasebe-hesap-plani`
  - dosyalar:
    - `frontend/src/app/pages/muhasebe/muhasebe-hesap-plani/muhasebe-hesap-plani.dto.ts`
    - `.../muhasebe-hesap-plani.service.ts`
    - `.../muhasebe-hesap-plani.ts`
    - `.../muhasebe-hesap-plani.html`
  - route eklendi: `/muhasebe/hesap-plani`

### Migration
- Yeni migration:
  - `20260418173002_AddMuhasebeHesapPlaniAndRefactorTasinirKod`
- Icerik:
  - `muhasebe.TasinirKodlar` tablosuna `Kod` kolon gecisi ve eski `Duzey1..5` kolonlarinin kaldirilmasi
  - `IX_TasinirKodlar_UstKodId_Kod` unique index
  - yeni tablo: `muhasebe.MuhasebeHesapPlanlari` + self FK + unique/indexler
  - TODBase role/menu/menu-item-role seed:
    - domain: `MuhasebeHesapPlaniYonetimi`
    - menu route: `muhasebe/hesap-plani`

### Build
- Backend: BASARILI (`dotnet build backend/STYS.csproj -o backend/.tmp-build`)
- Frontend: BASARILI (`npm run build`)
- Not: Migration DB'ye uygulanmadi (istek uzerine).

## Tur - PendingModelChangesWarning Duzeltmesi (2026-04-18)

- Sorun: `StysAppDbContext` icin `PendingModelChangesWarning` aliyordu.
- Kök neden: `TasinirKod` refactor ve `MuhasebeHesapPlani` model degisikligi snapshot/migration zincirine tam yansimamis durumdaydi.
- Yapilanlar:
  - Eski hatali migration dosyalari temizlendi.
  - Migration yeniden scaffold edildi: `20260418182444_AddMuhasebeHesapPlaniAndRefactorTasinirKod`.
  - Migration icinde `TasinirKod.Kod` kolonu icin veri backfill SQL eklendi (`TamKod` son segmentinden doldurma), sonra `NOT NULL`'a cekildi.
- Dogrulama:
  - `dotnet ef migrations has-pending-model-changes` sonucu: **No changes have been made to the model since the last migration.**


## Tur - Tasinir Kodlari FE Agac Gorunumu + Parent Secimi

- `muhasebe/tasinir-kodlari` ekrani liste yerine `p-treeTable` ile agac yapida gosterilecek sekilde guncellendi.
- Dialog formuna `Parent Tasinir Kod` secimi eklendi (`p-select`, filtrelenebilir).
- Duzenleme modunda mevcut parent otomatik secili geliyor, create modunda temiz basliyor.
- Parent secimi sonrasinda secili parent bilgisi ekranda ozet olarak gosteriliyor.
- Kendi kendini parent secmeyi engellemek icin edit modunda aktif kayit parent seceneklerinden cikarildi.
- Build dogrulama: `npm run build` BASARILI.

## Tur - TasinirKod Unique Index Cakisma Duzeltmesi

- Hata: `IX_TasinirKodlar_UstKodId_Kod` index olusurken duplicate `(UstKodId, Kod)` degeri nedeniyle migration fail oluyordu.
- Duzeltme: `20260418182444_AddMuhasebeHesapPlaniAndRefactorTasinirKod` migration'ina index oncesi veri duzeltme SQL'i eklendi.
  - `UstKodId` alanlari `TamKod` hiyerarsisinden yeniden hesaplanir (parent = son noktadan onceki segment).
  - Ayni parent altinda kalan duplicate `Kod` degerleri `-2`, `-3` ... suffix ile benzersizlestirilir.
- Sonuc: migration index olusturma adiminda duplicate key hatasina dusmez.

## Tur - Tasinir Kod Agacinda Yetim Gorunum Duzeltmesi

- Sorun: Parent'i olmayan (veya UstKodId'si bos/yanlis olan) kodlar agacta basi bos/yetim gorunuyordu.
- Duzeltme: FE tree builder'a fallback parent cozumu eklendi.
  - Once `UstKodId` ile parent aranir.
  - Parent bulunamazsa `TamKod` kirilimindan en yakin mevcut ust kod bulunup parent atanir.
- Sonuc: `150.12.9.1.02` gibi kayitlar parent hiyerarsisine dogru yerlestirilir; basi bos gorunum azalir.
- Dogrulama: `npm run build` BASARILI.

## Tur - Tasinir Kod Agacinda Ara Kirilim Sanal Dugumleri

- `muhasebe/tasinir-kodlari` agac kurulumunda `UstKodId` olmayan kayitlar icin `TamKod` kirilimlari kullanilarak ara seviyeler sanal dugum olarak uretilir hale getirildi.
- Boylece `150.12.9.1.02` gibi kayitlar dogrudan ust kok dugume yigilmak yerine `150 -> 150.12 -> 150.12.9 -> ...` zinciriyle gosteriliyor.
- Sanal dugumler sadece gorunum amacli: duzenle/sil aksiyonlari disable edildi.
- Frontend dogrulama: `npm run build` BASARILI (mevcut bundle budget uyarilari haric).
## Tur - Muhasebe Hesap Plani UI + Excel Seed + Redis Cache

### Backend
- `MuhasebeHesapPlaniService` Redis/distributed cache ile guncellendi.
  - Tree verisi cache key versiyonlama ile tutuluyor.
  - Add/Update/Delete sonrasi cache invalidation yapiliyor.
- `MuhasebeHesapPlaniController` list endpointi cacheli tree akisini kullanacak sekilde guncellendi.
- Yeni migration eklendi:
  - `20260419113000_SeedMuhasebeHesapPlaniFromExcel.cs`
  - Kaynak dosya: `C:\Users\cuce\Desktop\stys\TEK DÜZEN HESAP PLANI Muhasebe kodları.xlsx`
  - 319 satir hesap plani `muhasebe.MuhasebeHesapPlanlari` tablosuna upsert edilir.
  - `TamKod` kirilimina gore `UstHesapId` iliskisi migration icinde yeniden kurulur.

### Frontend
- `muhasebe-hesap-plani` ekrani agac odakli hale getirildi (`p-treeTable`).
- Ustte hizli arama eklendi (kod/tam kod/ad).
- Dialog icinde parent hesap secimi filtreli dropdown ile tum kayitlardan yapilir hale getirildi.

### Dogrulama
- Backend build: `dotnet build backend/STYS.csproj -o backend/.tmp-build` BASARILI.
- Frontend build: `npm run build` BASARILI.
## Tur - Muhasebe Hesap Plani Menu ve Yetkilendirme

- `Muhasebe Hesap Plani` ekraninin menude gorunmesi icin yeni migration eklendi:
  - `20260419124500_AddMuhasebeHesapPlaniMenuAndPermissions.cs`
- Migration ile:
  - `TODBase.Roles` icinde `MuhasebeHesapPlaniYonetimi` icin `Menu/View/Manage` rolleri idempotent olarak olusturulur.
  - Admin ve TesisYonetici gruplarina bu roller atanir (`TODBase.UserGroupRoles`).
  - `TODBase.MenuItems` icinde `muhasebe/hesap-plani` menusu `Muhasebe` koku altina eklenir/guncellenir.
  - `TODBase.MenuItemRoles` ile menu gorunurlugu `MuhasebeHesapPlaniYonetimi.Menu` rolune baglanir.
- Backend build dogrulamasi: `dotnet build backend/STYS.csproj -o backend/.tmp-build` BASARILI.
## Tur - Muhasebe Hesap Plani Menu Gorunurlugu Duzeltmesi

- Sorun analizi: menu runtime'da parent item yetkisi yoksa child yetkili olsa bile tum dal gizleniyordu.
- Duzeltme: `frontend/src/app/core/menu/menu-runtime.service.ts` icindeki `filterMenuItems` mantigi guncellendi.
  - Parent item kendi rolune sahip degilse ama yetkili child varsa parent konteyner olarak tutulur.
  - Yetkisiz parent icin dogrudan aksiyon (routerLink/url/command) temizlenir, sadece childlar gosterilir.
- Bu sayede `Muhasebe Hesap Plani` gibi yeni eklenen child menuler, parent role zincirindeki gecici eksikliklerden etkilenmeden gorunur.
- Frontend build dogrulamasi: `npm run build` BASARILI.
## Tur - Muhasebe Hesap Plani Menu Gorunmeme Garantili Duzeltme

- Yeni migration eklendi: `20260419133000_EnsureMuhasebeHesapPlaniMenuVisible.cs`
- Migration, `muhasebe/hesap-plani` menu item gorunurlugunu garanti altina alir:
  - `MuhasebeHesapPlaniYonetimi` Menu/View/Manage rollerini bulur veya olusturur.
  - Soft-delete olmus role/menu kayitlarini tekrar aktif eder.
  - `Muhasebe` root menusu ve `muhasebe/hesap-plani` child menusu yoksa olusturur, varsa parent/order/route degerlerini normalize eder.
  - Admin (`YoneticiGrubu`) ve `TesisYoneticiGrubu` icin role atamalarini garanti eder.
  - `MenuItemRoles` baglarini root ve child icin tekrar garanti eder.
- Backend build: BASARILI (`dotnet build backend/STYS.csproj -o backend/.tmp-build`).
## Tur - Muhasebe Hesap Plani Migration Kesfi Duzeltmesi

- Sorun: Otomatik migration calismasina ragmen son eklenen 3 migration uygulanmiyordu.
- Kök neden: Elle eklenen migration dosyalarinda EF migration attribute yoktu; EF migration assembly kesfinde atlanma riski olusuyordu.
- Duzeltme:
  - `20260419113000_SeedMuhasebeHesapPlaniFromExcel.cs` dosyasina `[Migration("20260419113000_SeedMuhasebeHesapPlaniFromExcel")]` eklendi.
  - `20260419124500_AddMuhasebeHesapPlaniMenuAndPermissions.cs` dosyasina `[Migration("20260419124500_AddMuhasebeHesapPlaniMenuAndPermissions")]` eklendi.
  - `20260419133000_EnsureMuhasebeHesapPlaniMenuVisible.cs` dosyasina `[Migration("20260419133000_EnsureMuhasebeHesapPlaniMenuVisible")]` eklendi.
- Build dogrulama: `dotnet build backend/STYS.csproj -o backend/.tmp-build` BASARILI.

- [2026-04-19] Migration uygulanmama kök sebebi düzeltildi: 20260419113000, 20260419124500, 20260419133000 migration dosyalarına [DbContext(typeof(StysAppDbContext))] eklendi (EF migration assembly keşfi için).
- [2026-04-19] `SeedMuhasebeHesapPlaniFromExcel` migration SQL literal hatasi duzeltildi: `@"""` yerine `"""` kullanildi (SQL'e bastaki cift tirnak gitmesi engellendi).
- [2026-04-19] `SeedMuhasebeHesapPlaniFromExcel` migrationinda duplicate key (2601) icin seed normalize edildi: `TamKod` tekillestirme, kardes `Kod` cakisma suffixleme ve `MERGE` update adiminda `Kod` ezme kaldirildi.
- [2026-04-19] Muhasebe agac ekranlari lazy-load yapildi: Tasinir Kodlari ve Muhasebe Hesap Plani icin `tree/roots` + `tree/children` endpointleri eklendi; frontend `p-treeTable` `onNodeExpand` ile cocuklari acildikca cekiyor.
- [2026-04-19] Muhasebe/Tasinir agac lazy-load mantigi parentId kolonuna bagliliktan cikarildi; `TamKod + DuzeyNo` ile kok/alt seviye hesaplama yapilarak ilk acilista tum verinin gelmesi engellendi.
- [2026-04-19] Tasinir Kodlari lazy tree davranisi kullanici talebine gore netlestirildi: ilk acilista sadece `UstKodId = NULL` kokleri, expand'da sadece `UstKodId = secilenId` cocuklari getiriliyor.
- [2026-04-19] Tree load sirasinda DbContext concurrency hatasi duzeltildi: `Task.WhenAll` ile paralel `HasChildren` sorgulari kaldirildi, ardışık await akışına çevrildi (TasinirKodService, MuhasebeHesapPlaniService).
- [2026-04-19] DB incelemesi: `muhasebe.TasinirKodlar` icinde bazi alt kodlarin `UstKodId` degeri NULL oldugu dogrulandi (ornek: `150.12.9.1.02`, `150.12.9.1.3`, `150.12.9.99`).
- [2026-04-19] Duzenleme: import akisinda `UstTamKod` bos gelirse `TamKod`dan parent otomatik turetiliyor; parent bagi bundan sonra null kalmayacak.
- [2026-04-19] Yeni migration eklendi: `20260419174000_BackfillTasinirKodParentIds` (mevcut kayitlar icin `UstKodId` nearest-ancestor mantigiyla backfill).
- [2026-04-19] Tasinir Kodlari agac ekraninda satira tiklayarak expand/collapse destegi eklendi; expand aninda alt dugumler lazy yukleniyor.
- [2026-04-19] Tasinir Kodlari agac satirina `ttRow` baglandi; PrimeNG tree state'i dogru takip edilerek expand/collapse davranisi duzeltildi.
- [2026-04-19] Muhasebe icin yeni master tanim modulu eklendi: `KasaBankaHesaplari` (NakitKasa/Banka hesap tanimlari, muhasebe hesap plani baglantili).
- [2026-04-19] Backend eklendi: `backend/Muhasebe/KasaBankaHesaplari` altinda Entity/Dto/Repository/Service/Controller/Mapping dosyalari olusturuldu.
- [2026-04-19] Is kurali eklendi: Nakit kasa hesaplari yalnizca `1.10.100*`, banka hesaplari yalnizca `1.10.102*` muhasebe kodlarina baglanabilir.
- [2026-04-19] `KasaHareket` ve `BankaHareket` tablolari master hesaba baglandi: yeni `KasaBankaHesapId` alanlari ve FK iliskileri eklendi.
- [2026-04-19] Kasa/Banka hareket servislerinde dogrulama guncellendi: secilen master hesap aktif olma + tip uyumu zorunlu, harekette kod/banka/hesap bilgileri master hesaptan normalize edilir.
- [2026-04-19] Yeni API endpointleri eklendi: `api/muhasebe/kasa-banka-hesaplari` CRUD + `tip/{tip}` + `muhasebe-hesap-secimleri/{tip}`.
- [2026-04-19] Frontend eklendi: `src/app/pages/muhasebe/kasa-banka-hesaplari` (liste/form, tip bazli muhasebe kod secimi, banka alanlari).
- [2026-04-19] Frontend entegrasyonu: `kasa-hareketleri` ve `banka-hareketleri` formlarinda serbest text yerine master hesap secimi kullanildi.
- [2026-04-19] Route eklendi: `/muhasebe/kasa-banka-hesaplari`.
- [2026-04-19] Permission/erisim tanimi eklendi: `StructurePermissions.KasaBankaHesapYonetimi` ve `ErisimTeshisModulTanimlari` kaydi.
- [2026-04-19] Migration eklendi: `20260419193034_AddKasaBankaHesapTanimiVeHareketBaglantisi`.
  - `muhasebe.KasaBankaHesaplari` tablosu
  - Kasa/Banka hareketlerine `KasaBankaHesapId` kolon + index + FK
  - Menu/role seed: `KasaBankaHesapYonetimi` ve `muhasebe/kasa-banka-hesaplari`
  - Varsayilan seed hesaplar: `KASA-MERKEZ` (1.10.100), `BNK-VARSAYILAN` (1.10.102)
- [2026-04-19] Muhasebe'de yeni `Hesaplar` modulu eklendi: bir hesap kaydina coklu `Kasa Hesabi`, `Banka Hesabi` ve `Depo` baglanabilir hale getirildi.
- [2026-04-19] Backend eklendi: `backend/Muhasebe/Hesaplar` altinda Entity/Dto/Repository/Service/Controller/Mapping dosyalari olusturuldu.
- [2026-04-19] Yeni tablolar: `muhasebe.Hesaplar`, `muhasebe.HesapKasaBankaBaglantilari`, `muhasebe.HesapDepoBaglantilari`.
- [2026-04-19] Yeni API: `api/muhasebe/hesaplar` CRUD + lookup endpointleri (`kasa-hesaplari`, `banka-hesaplari`, `depolar`, `muhasebe-kodlari`).
- [2026-04-19] Permission eklendi: `HesapYonetimi.Menu/View/Manage`; erisim teshis modul tanimina `muhasebe/hesaplar` eklendi.
- [2026-04-19] Frontend eklendi: `src/app/pages/muhasebe/hesaplar` sayfasi (liste + dialog, coklu baglama secimleri).
- [2026-04-19] Route eklendi: `/muhasebe/hesaplar`.
- [2026-04-19] Migration eklendi: `20260419202646_AddMuhasebeHesaplarAndBindings` (tablo + index + FK + menu/role seed).
- [2026-04-19] Muhasebe tesis-scope genisletildi: `CariKart`, `KasaBankaHesap`, `Hesap`, `TasinirKart` varliklarina `TesisId` alani eklendi; servislerde read/write scope kontrolu zorunlu hale getirildi (scoped kullanici sadece atanmis tesis verilerini gorebilir/yazabilir).
- [2026-04-19] Tesis-muhasebeci iliskisi eklendi: yeni `dbo.TesisMuhasebecileri` modeli/repository/service akisi ile tesise muhasebeci atama desteklendi (resepsiyonist mantigi ile).
- [2026-04-19] Kullanici atama markerlari eklendi: `KullaniciAtama.MuhasebeciAtanabilir` ve `KullaniciAtama.MuhasebeciAtayabilir`.
- [2026-04-19] Scope altyapisi guncellendi: AccessScope, YoneticiAday ve scoped user servisleri `TesisMuhasebecileri` uzerinden muhasebeci kapsamlarini hesaplayacak sekilde genisletildi.
- [2026-04-19] Tesis Yonetimi UI guncellendi: tesis dialoguna `Muhasebeciler` coklu secimi eklendi; aday listesi `/ui/yoneticiaday/muhasebeciler` endpointinden cekiliyor.
- [2026-04-19] Yeni migration eklendi: `20260419223000_AddMuhasebeTesisScopeAndMuhasebeciAssignments`.
  - `dbo.TesisMuhasebecileri` tablo/FK/index
  - Muhasebe tablolarina `TesisId` kolon + FK/index/benzersizlik normalizasyonu
  - `MuhasebeciGrubu` ve `MuhasebeciAtanabilir/Atayabilir` rol-grup seed/atama

## Tur 126 - Muhasebe FE Tesis Secimi / Listeleme Iyilestirmesi

### Guncellenen Ekranlar
- frontend/src/app/pages/muhasebe/cari-kartlar
- frontend/src/app/pages/muhasebe/kasa-banka-hesaplari
- frontend/src/app/pages/muhasebe/hesaplar
- frontend/src/app/pages/muhasebe/tasinir-kartlari
- frontend/src/app/pages/muhasebe/depolar

### Yapilanlar
- CRUD dialoglarina `Tesis` secimi eklendi; create/update payload'larina `tesisId` dahil edildi.
- Liste tablolarina `Tesis` kolonu eklendi.
- Ust alana tesis filtre dropdown'u eklendi.
- Servislere `getTesisler()` metodu eklendi (`/ui/rezervasyon/tesisler`).
- `getPaged` cagrilarina opsiyonel `tesisId` query param destegi eklendi.
- Depolar ekranindaki sayisal `Tesis Id` girisi dropdown secime cevrildi.

### Build
- Frontend: BASARILI (`npm run build`)

## Tur 127 - StysAppDbContext Migration Invalid column name `TesisId` Duzeltmesi

### Sorun
- `20260419223000_AddMuhasebeTesisScopeAndMuhasebeciAssignments` migration'i tek SQL batch icinde hem `TesisId` kolonunu ekleyip hem de ayni kolona FK/index olusturuyordu.
- SQL Server derleme asamasinda ayni batch icindeki yeni kolonu gormedigi icin `Invalid column name 'TesisId'` hatasi olusuyordu.

### Duzeltme
- `backend/Infrastructure/EntityFramework/Migrations/20260419223000_AddMuhasebeTesisScopeAndMuhasebeciAssignments.cs`
  - `Up` icindeki SQL iki ayri `migrationBuilder.Sql(...)` cagrisi olacak sekilde bolundu:
    1. tablo/kolon olusturma (`TesisId` add)
    2. FK + index + rol/grup seed islemleri

### Dogrulama
- `dotnet build backend/STYS.csproj` -> BASARILI

## Tur 135 - Muhasebe Detay Hesap Standardizasyonu Devam

### Yapilanlar
- `IMuhasebeDetayHesapService` guclendirildi:
  - `CreateOrResolveDetayHesapAsync(...)` imzasina `kaynakId` eklendi.
  - typed sonuc modeli (`MuhasebeDetayHesapSonuc`) korunarak kullanildi.
- `MuhasebeDetayHesapService` transaction davranisi standartlandi:
  - Aktif transaction varsa mevcut transaction’a katiliyor.
  - Aktif transaction yoksa kendi transaction’ini acip commit/rollback yapiyor.
  - Bu sayede cagirici servis transaction’i ile tek butunluk saglaniyor.
- Ortak serviste "mevcut hesap baska kaynaga bagli mi?" kontrolu merkezilestirildi:
  - `CariKart`, `FinansalHesap`, `Depo`, `TasinirKart` capraz kontrol ediliyor.
  - Kaynak baska bir karta bagliysa acik hata donuluyor.
- Ana hesap bulunamama hata mesajlari iyilestirildi:
  - Depo icin `Depo için 1.15.150 ana hesabı bulunamadı.`
  - Tasinir kart icin `Taşınır kart için 1.15.150 ana hesabı bulunamadı.`
- `CariKartService` create akisinda ortak servis kullanimi transaction ile guvenli hale getirildi.
- `KasaBankaHesapService` create akisinda ortak servis kullanimi transaction ile guvenli hale getirildi.
- `DepoService` create akisinda detay hesap + ana kayit tek transaction’a alindi.
- `TasinirKartService` create akisinda detay hesap + ana kayit tek transaction’a alindi.

### Notlar
- `Depo.Kod` su an muhasebe detay hesap kodu ile esitleniyor. Ileride operasyonel depo kodu ayrimi gerekirse `DepoOperasyonelKod` benzeri ayri alan degerlendirilebilir.
- `TasinirKart.StokKodu` su an otomatik muhasebe detay hesap kodu ile esitleniyor. Is ihtiyacina gore operasyonel stok kodu ile muhasebe kodu ayrilabilir.
- `Depo` ve `TasinirKart` gecici/standart olarak `1.15.150` ana hesabini kullaniyor. Ayrı muhasebe kirilimi istenirse `MuhasebeAnaHesapKodlari` guncellenmelidir.

### Migration / Snapshot
- Bu turda ek bir schema degisikligi yapilmadi.
- Yeni migration yazilmadi.
- `StysAppDbContextModelSnapshot` degisikligi gerekmiyor.

### Build Sonuclari
- Backend build: `dotnet build backend/STYS.csproj /p:NoWarn=NU1903` -> BASARILI (mevcut warningler disinda)
- Frontend build: `npm run build` -> BASARILI (mevcut bundle budget warningleri disinda)

## Tur 137 - Muhasebe Sayaç Retry/Transaction Düzeltmesi

### Yapilanlar
- `MuhasebeDetayHesapService` icinde ilk sayaç satiri olusturma akisi sadeleştirildi:
  - Sayaç yoksa ayni attempt icinde `SonSiraNo = 1` set edilip kaydediliyor.
  - Ilk kayitta dogrudan `... .1` kodu uretimi saglandi.
- Retry/transaction davranisi guclendirildi:
  - Dis transaction varsa retry kapatildi (`maxAttempt = 1`), hata dogrudan yukari firlatiliyor.
  - Sadece servis-owned transaction senaryosunda retry devam ediyor.
  - Owned transaction’da exception durumunda rollback kesinlestirildi.
- Unique conflict (2601/2627) akisi korundu:
  - Sayaç insert conflictinde olusan `created` entity detach edilip tekrar deneme yapiliyor.
- Sayaç okuma SQL lock davranisi korundu:
  - `[muhasebe].[MuhasebeHesapKoduSayaclari] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)`
- Existing detay hesapta `SiraNo` donusu korundu:
  - Kod son segmentinden parse edilmeye devam ediyor; parse edilemezse fallback korunuyor.
- `KasaBankaHesapService` temizlikleri dogrulandi ve korundu:
  - `entity.Tip = entity.Tip;` yok.
  - `ApplyTipDefaultsAndValidateAsync` async.
  - Bagli banka hesabi kontrolu `FirstOrDefaultAsync`.

### Migration / Snapshot
- Bu turda schema degisikligi yok.
- Yeni migration yazilmadi.
- `StysAppDbContextModelSnapshot` degisikligi gerekmiyor.

### Build Sonuclari
- Backend build: `dotnet build backend/STYS.csproj /p:NoWarn=NU1903` -> BASARILI (mevcut warningler disinda)
- Frontend build: `npm run build` -> BASARILI (mevcut bundle budget warningleri disinda)

## Tur 136 - Muhasebe Sayaç Concurrency Güçlendirmesi

### Yapilanlar
- `MuhasebeDetayHesapService` icindeki `MuhasebeHesapKoduSayac` artirim akisi SQL Server lock hintleri ile guclendirildi:
  - `UPDLOCK, ROWLOCK, HOLDLOCK` ile sayaç satiri transaction icinde kilitlenerek okunuyor.
  - Sayaç satiri yoksa insert denemesi yapiliyor; unique conflict olursa tekrar okuyup devam ediliyor.
  - Sayaç artirimi aktif transaction icinde atomik olarak tamamlanıyor.
- Transaction davranisi korundu:
  - Dis transaction varsa ortak servis ona katiliyor.
  - Dis transaction yoksa servis kendi transaction’ini aciyor.
  - Rollback/commit sorumlulugu buna gore ayriliyor.
- Existing detay hesap bulunmasi durumunda `SiraNo` davranisi iyilestirildi:
  - Kodun son segmentinden parse edilip sonuc modeline yansitiliyor (parse edilemezse mevcut davranisa fallback).
- Merkezi "baska kaynaga bagli hesap" kontrolleri korunup devam ettirildi.
- `KasaBankaHesapService` teknik temizlik:
  - Gereksiz `entity.Tip = entity.Tip;` satiri kaldirildi.
  - Senkron `FirstOrDefault` kullanimı `FirstOrDefaultAsync`e cekildi.
  - Tip/default/validasyon metodu async akisa alindi.

### Migration / Snapshot
- Bu turda schema degisikligi yapilmadi.
- Yeni migration eklenmedi.
- `StysAppDbContextModelSnapshot` degisikligi gerekmiyor.

### Notlar
- Sayaç geri alma davranisi transaction rollback ile birlikte calisir. Ana entity kaydi hata alirsa yetim detay hesap kalmaz; sayaç artisi da rollback olur.
- Depo.Kod ve TasinirKart.StokKodu mevcut davranis geregi muhasebe detay hesap kodu ile eslenmeye devam ediyor.

### Build Sonuclari
- Backend build: `dotnet build backend/STYS.csproj /p:NoWarn=NU1903` -> BASARILI (mevcut warningler disinda)
- Frontend build: `npm run build` -> BASARILI (mevcut bundle budget warningleri disinda)
- `dotnet ef database update --context StysAppDbContext --no-build` -> BASARILI (`Done.`)

## Tur 128 - Global Paket Turleri Modulu (Tesis Bagimsiz)

### Backend
- Yeni muhasebe modulu eklendi: `backend/Muhasebe/PaketTurleri`
  - `Entities/PaketTuru.cs`
  - `Dtos/PaketTuruDtos.cs`
  - `Repositories/IPaketTuruRepository.cs`, `PaketTuruRepository.cs`
  - `Services/IPaketTuruService.cs`, `PaketTuruService.cs`
  - `Controllers/PaketTurleriController.cs`
  - `Mapping/PaketTuruProfile.cs`
- API endpointi acildi: `api/muhasebe/paket-turleri` (list/paged/byId/create/update/delete)
- `StructurePermissions` icine yeni domain eklendi:
  - `PaketTuruYonetimi.Menu`
  - `PaketTuruYonetimi.View`
  - `PaketTuruYonetimi.Manage`
- `StysAppDbContext` guncellendi:
  - `DbSet<PaketTuru>`
  - `muhasebe.PaketTurleri` tablo konfigurasyonu (unique `Ad`, unique `KisaAd`, index `AktifMi`)

### Migration
- Yeni migration eklendi: `20260420080427_AddMuhasebePaketTurleri`
  - `muhasebe.PaketTurleri` tablosu olusturuldu.
  - Varsayilan global paket turleri seed edildi:
    - Adet, Kilogram, Cuval, Kasa, Koli, Teneke, Kova, Paket, Litre, Demet
  - Menu/permission seed eklendi:
    - `PaketTuruYonetimi` role'leri (`Menu/View/Manage`)
    - `Muhasebe` ana menu alti `muhasebe/paket-turleri` menu item
    - Admin, TesisYoneticiGrubu ve MuhasebeciGrubu role atamalari

### Frontend
- Yeni ekran eklendi: `frontend/src/app/pages/muhasebe/paket-turleri`
  - `paket-turleri.dto.ts`
  - `paket-turleri.service.ts`
  - `paket-turleri.ts`
  - `paket-turleri.html`
- Route eklendi:
  - `muhasebe/paket-turleri`

### Entegrasyon
- `tasinir-kartlari` formundaki `Birim` alani serbest text yerine paket turu secimi olacak sekilde baglandi.
  - `TasinirKartlariService` icine `getPaketTurleri()` eklendi.
  - Dialog acilisinda aktif paket turleri yuklenip dropdown'da listeleniyor.

### Dogrulama
- `dotnet build backend/STYS.csproj` -> BASARILI
- `npm run build` -> BASARILI (mevcut bundle budget warningleri disinda)

## Tur 134 - Muhasebe Detay Hesap Ortak Servis Standardizasyonu

### Yapilanlar
- `IMuhasebeDetayHesapService` standarda alinip metod imzasi guncellendi:
  - `CreateOrResolveDetayHesapAsync(int tesisId, string anaMuhasebeHesapKodu, string kaynakTipi, string kaynakAd, ...)`
- Sonuc modeli eklendi:
  - `MuhasebeDetayHesapSonuc` (`MuhasebeHesapPlaniId`, `Kod`, `AnaMuhasebeHesapKodu`, `SiraNo`)
- Ortak servis:
  - ana hesap arama (`TesisId = null` + `Kod`)
  - sayac tablosu uzerinden sira uretimi
  - `{AnaKod}.{SiraNo}` formatinda detay hesap uretimi
  - duplicate kodda kaynaga baglilik kontrolu
  - aciklayici ana hesap hata mesajlari
- Tekrarlayan kodlar kaldirildi ve servisler ortak servise baglandi:
  - `CariKartService`
  - `KasaBankaHesapService`
  - `DepoService`
  - `TasinirKartService`

### Notlar
- `CariKart` tarafinda `TesisSegmenti` runtime kullanimi yok (eski format kaldirilmis durumda).
- `MuhasebeHesapPlani` icin `TesisId nullable + filtered unique index` modeli korunuyor.

### Dogrulama
- `dotnet build backend/STYS.csproj` -> BASARILI

## Tur 133 - MuhasebeHesapPlani Zorunlu Baglanti Standardi

### Kisa Degerlendirme
- `MuhasebeHesapPlani` baglantisi zaten bulunan kartlar:
  - `CariKart`, `KasaBankaHesap`, `Depo`, `Hesap`
- Bu turda baglanti eklenen kart:
  - `TasinirKart` (`MuhasebeHesapPlaniId`, `AnaMuhasebeHesapKodu`, `MuhasebeHesapSiraNo`)
- Hareket tarafinda dogrudan hesap id saklamak yerine kart baglantisi zorunlu kontrolu uygulandi:
  - `CariHareket`, `KasaHareket`, `BankaHareket`, `StokHareket`, `TahsilatOdemeBelgesi`

### Backend
- Yeni ortak servis eklendi:
  - `IMuhasebeDetayHesapService`
  - `MuhasebeDetayHesapService`
- Servislerin kullanimi standartlandi:
  - `DepoService` ve `TasinirKartService` create akisinda muhasebe detay hesabi otomatik olusturuyor.
  - Kod/hesap alanlari manuel secim yerine sistem tarafindan uretiliyor (`{AnaHesapKodu}.{SiraNo}`).
  - Muhasebe hesabi olusmus kayitlarda kritik alan degisimi engellendi (tesis/tip vb.).
  - Ad degisimi bagli `MuhasebeHesapPlani.Ad` alanina yansitiliyor.
  - Pasife alma/silme akisinda bagli hesap da pasifleniyor (hard delete yok).
- Hareket servisleri validasyonlari guclendirildi:
  - Secilen kartin muhasebe baglantisi yoksa islem engelleniyor ve acik hata veriliyor.
- `StysAppDbContext` mapping/index guncellemesi:
  - `TasinirKart -> MuhasebeHesapPlani` FK
  - `Depo` kod unique kurali `TesisId + Kod` olarak duzenlendi
  - Muhasebe otomasyon alanlari icin indexler eklendi

### Migration
- Yeni migration:
  - `20260429183841_EnforceMuhasebeHesapPlaniLinks`
- Icerik:
  - `TasinirKartlar` tablosuna muhasebe baglantisi kolonlari
  - `Depolar` tablosuna otomasyon kolonlari
  - `Depolar` icin unique indexin `TesisId+Kod` modeline alinmasi
  - `TasinirKartlar` icin FK/indexler

### Frontend
- `muhasebe/depolar`:
  - Muhasebe kodu manuel secimi kaldirildi.
  - Kod/muhasebe alanlari readonly + `Sistem tarafından oluşturulacak`.
- `muhasebe/tasinir-kartlari`:
  - Stok kodu manuel zorunlulugu kaldirildi.
  - Kod readonly + `Sistem tarafından oluşturulacak`.

### Konfigurasyon
- `appsettings.json` ve `appsettings.Development.json` icine eklendi:
  - `Muhasebe:AnaHesapKodlari:Depo = 1.15.150`
  - `Muhasebe:AnaHesapKodlari:TasinirKart = 1.15.150`

### Dogrulama
- `dotnet build backend/STYS.csproj` -> BASARILI
- `npm run build` -> BASARILI (mevcut bundle budget warningleri disinda)

## Tur 133 - CariKart TesisSegmenti Kaldirma ve Kod Hizalama

### Backend
- `CariKart` modelinden `TesisSegmenti` kaldirildi:
  - `backend/Muhasebe/CariKartlar/Entities/CariKart.cs`
  - `backend/Muhasebe/CariKartlar/Dtos/CariKartDtos.cs`
  - `backend/Infrastructure/EntityFramework/StysAppDbContext.cs` (EF mapping temizligi)
- `CariKartService` kod uretimi sadeleştirildi:
  - eski: `{AnaKod}.{TesisSegmenti}.{SiraNo}`
  - yeni: `{AnaKod}.{SiraNo}`
- Cari detay muhasebe hesabi cozumleme/olusturma tesise bagli hale getirildi (`MuhasebeHesapPlani.TesisId` ile).
- Ana hesap aramasi yalnizca genel hesaplarda (`TesisId = null`) yapilacak sekilde guncellendi.

### Migration
- Yeni migration:
  - `20260428205346_RemoveCariKartTesisSegmentiAndAlignCodes`
- Icerik:
  - `CariKartlar.TesisSegmenti` kolonu kaldirildi.
  - Backfill SQL ile `Tedarikci/Musteri/KurumsalMusteri` kayitlari `{AnaKod}.{SiraNo}` formatina cevrildi.
  - Bagli `MuhasebeHesapPlanlari` kayitlarinin `Kod/TamKod/Ad/TesisId` alanlari CariKart ile hizalandi.
  - `MuhasebeHesapKoduSayaclari` verileri yeni formatla tekrar senkronlandi.

### Frontend
- `cari-kartlar` modelinden `tesisSegmenti` kaldirildi:
  - `frontend/src/app/pages/muhasebe/cari-kartlar/cari-kartlar.dto.ts`
  - `frontend/src/app/pages/muhasebe/cari-kartlar/cari-kartlar.ts`

## Tur 134 - Finansal Hesaplar Menu/Breadcrumb Isim Hizalama

### Frontend
- Route breadcrumb guncellendi:
  - `muhasebe/kasa-banka-hesaplari` -> `['Muhasebe', 'Finansal Hesaplar']`
  - dosya: `frontend/src/app.routes.ts`

### Backend
- Erisim teshis modul etiketi guncellendi:
  - `Kasa/Banka Hesaplari` -> `Finansal Hesaplar`
  - dosya: `backend/ErisimTeshis/ErisimTeshisModulTanimlari.cs`
- Veritabani menu etiketi icin migration eklendi:
  - `20260428210928_RenameKasaBankaMenuToFinansalHesaplar`
  - route `muhasebe/kasa-banka-hesaplari` label degerini `Finansal Hesaplar` olarak gunceller

## Tur 129 - Paket Turleri Seed (Referans Gorsel)

- Yeni migration eklendi: `20260420123000_SeedPaketTurleriFromReferenceImage`
  - Gorseldeki paket turleri idempotent sekilde seed edildi:
    - Adet (Ad.)
    - Kilogram (Kg.)
    - Cuval (Cuv.)
    - Kasa (Kas.)
    - Koli (Kol.)
    - Teneke (Ten.)
    - Kova (Kov.)
    - Paket (Pk.)
    - Lire (L.)
    - Demet (Dm.)
  - Onceki seedde bulunan `Litre` kaydi `Lire` olarak normalize edildi.
- Build dogrulamasi:
  - `dotnet build backend/STYS.csproj` -> BASARILI

## Tur 130 - Depo Hiyerarsisi ve Depo Cikis Gruplari Gelistirmesi

### Backend
- `Depo` modeli genisletildi:
  - `UstDepoId`, `UstDepo`, `AltDepolar`
  - `MuhasebeHesapPlaniId`
  - `MalzemeKayitTipi` (enum: `MalzemeleriAyriKayittaTut`, `FiyatFarkliMalzemeleriAyriKayittaTut`, `MalzemeleriAyniKayittaTut`)
  - `SatisFiyatlariniGoster`, `AvansGenel`
  - `DepoCikisGruplari`
- Yeni entity eklendi: `DepoCikisGrup`
  - `DepoId`, `CikisGrupAdi`, `KarOrani`, `LokasyonId`
- DTO/request genisletildi:
  - `DepoDto`, `CreateDepoRequest`, `UpdateDepoRequest`
  - `DepoCikisGrupDto`, `CreateDepoCikisGrupRequest`
- `DepoService` guncellendi:
  - Ust depo validasyonlari (aynı tesis, kendisini parent secememe, dongu engeli)
  - Muhasebe kodu validasyonu
  - Cikis grup satirlarini create/update akışında senkronize eden logic
  - Alt depo varsa silmeyi engelleyen kontrol
- `DepolarController` guncellendi:
  - `GET /api/muhasebe/depolar` icin opsiyonel `tesisId` filtre
  - `GET /api/muhasebe/depolar/tree` eklendi
  - `GET /api/muhasebe/depolar/paged` icin `tesisId` filtre destegi
- `StysAppDbContext` guncellendi:
  - `DbSet<DepoCikisGrup>`
  - `Depo` iliski/index/alan konfigleri
  - `DepoCikisGruplari` tablo konfigi

### Migration
- Yeni migration eklendi: `20260424083818_AddDepoHierarchyAndDepotOutputGroups`
  - `muhasebe.Depolar` tablosuna yeni kolonlar
  - self-reference FK (`UstDepoId`)
  - muhasebe hesap plani FK (`MuhasebeHesapPlaniId`)
  - `muhasebe.DepoCikisGruplari` tablosu
  - ilgili indexler

### Frontend (Angular)
- `muhasebe/depolar` modulu guncellendi:
  - yeni model alanlari eklendi
  - `malzeme kayit tipi` radio group
  - `satis fiyatlarini goster`, `avans genel`, `aktif` checkbox alanlari
  - `ust depo` ve `muhasebe kodu` secimleri
  - cikis gruplari icin alt grid (satir ekle/sil)
- Listeleme `p-treeTable` formatina gecirildi (depo/agac gorunumu)
- Ust depo seciminde:
  - secilen tesisin depolari listelenir
  - kendisi ve alt depolari parent seciminden dislanir

### Dogrulama
- `dotnet build backend/STYS.csproj` -> BASARILI
- `npm run build` -> BASARILI (mevcut bundle budget warningleri disinda)

## Tur 131 - CariKart / Muhasebe Hesap Plani Entegrasyonu

### Backend
- `CariKart` otomatik muhasebe kodlama icin genisletildi:
  - `MuhasebeHesapPlaniId`
  - `AnaMuhasebeHesapKodu`
  - `MuhasebeHesapSiraNo`
  - `TesisSegmenti`
- Yeni entity/repository eklendi:
  - `MuhasebeHesapKoduSayac`
  - `IMuhasebeHesapKoduSayacRepository`, `MuhasebeHesapKoduSayacRepository`
- `CariKartService` akisi guncellendi:
  - `Tedarikci/Musteri/KurumsalMusteri` icin `CariKodu` otomatik uretilir:
    - `3.32.320.{TesisSegmenti}.{SiraNo}` (Tedarikci)
    - `1.12.120.{TesisSegmenti}.{SiraNo}` (Musteri/KurumsalMusteri)
  - Sayaç `TesisId + AnaHesapKodu` bazli tutulur.
  - Ayni islemde ilgili `MuhasebeHesapPlani` detay hesabi olusturulur/iliskilendirilir.
  - `CariKart.MuhasebeHesapPlaniId` set edilir.
  - Eslik durumlarinda retry/konflikt korumasi eklendi.
  - Update tarafinda `CariKodu` manuel degistirilemez.
  - Muhasebe hesabi bagli kayitlarda `CariTipi/Tesis` degisikligi engellendi.
  - Unvan degisince bagli muhasebe hesap adi guncellenir.
  - Silme/pasifleme sonrasi bagli muhasebe hesap fiziksel silinmez, pasife cekilir.
- `MuhasebeHesapPlani` tarafinda `Kod` global benzersizlige cekildi.

### EF / Migration
- Yeni migration eklendi:
  - `20260427131218_AddCariKartMuhasebeHesapEntegrasyonu`
- Icerik:
  - `CariKartlar` tablosuna yeni kolonlar
  - `muhasebe.MuhasebeHesapKoduSayaclari` tablosu (+ `RowVersion` concurrency token)
  - `CariKart -> MuhasebeHesapPlani` FK
  - `MuhasebeHesapPlani.Kod` uzunlugu `64` ve global unique index
  - Backfill SQL:
    - mevcut `Tedarikci/Musteri/KurumsalMusteri` cari kartlar icin kod, segment, sira no ve muhasebe detay hesap iliskisi olusturulur
    - sayac tablosu son kullanilan degerlerle doldurulur
    - duplicate/ana hesap eksik senaryolari icin guvenli hata uretimi eklenir

### Frontend
- `muhasebe/cari-kartlar` modelleri guncellendi:
  - `muhasebeHesapPlaniId`, `anaMuhasebeHesapKodu`, `muhasebeHesapSiraNo`, `tesisSegmenti`
  - create requestte `cariKodu` opsiyonel hale getirildi
- Form davranisi guncellendi:
  - `Tedarikci/Musteri/KurumsalMusteri` icin `CariKodu` readonly + placeholder:
    - `Sistem tarafından oluşturulacak`
  - Muhasebe hesabi olusmus kayitlarda `CariTipi` ve `Tesis` alanlari kilitlenir.
- Listeye `Ana Hesap` kolonu eklendi.

### Dogrulama
- `dotnet build backend/STYS.csproj` -> BASARILI
- `npm run build` -> BASARILI (mevcut bundle budget warningleri disinda)

## Tur 132 - Kasa/Banka Modulu -> Finansal Hesaplar Genisletmesi

### Backend
- `KasaBankaHesap` tek tablo yapisi korunarak genisletildi:
  - yeni tipler: `KrediKarti`, `DovizHesabi`
  - yeni alanlar: `AnaMuhasebeHesapKodu`, `MuhasebeHesapSiraNo`, `ParaBirimi`, `ValorGunSayisi`,
    `KartAdi`, `KartNoMaskeli`, `KartLimiti`, `HesapKesimGunu`, `SonOdemeGunu`,
    `BagliBankaHesapId`, `SorumluKisi`, `Lokasyon`
- `MuhasebeHesapPlani` genisletildi:
  - `TesisId` (nullable) ve tesis bazli detay hesap mantigi
- `KasaBankaHesapService` akisi finansal hesap mantigina cekildi:
  - kod artik manuel degil, otomatik uretiliyor:
    - `{AnaMuhasebeHesapKodu}.{TesisBazliSiraNo}`
  - tip->ana hesap:
    - `NakitKasa -> 1.10.100`
    - `Banka -> 1.10.102`
    - `DovizHesabi -> 1.10.102`
    - `KrediKarti -> 1.10.109`
  - ana hesaplar `TesisId = null` kapsaminda aranir
  - valör defaultlari:
    - kasa/banka/doviz: `0`
    - kredi karti: `1`
  - create/update/delete icin bagli `MuhasebeHesapPlani` detay kaydi senkronu eklendi
  - tip/tesis degisikligi, muhasebe linki olusan kayitlarda engellendi
  - access scope/tesis yetki kontrolleri korundu
- `StysAppDbContext` mapping/indexleri guncellendi:
  - `MuhasebeHesapPlani` icin filtered unique index modeli:
    - genel: `Kod` ve `TamKod` unique (`TesisId IS NULL`)
    - tesis ozel: `TesisId+Kod`, `TesisId+TamKod` unique (`TesisId IS NOT NULL`)
  - `KasaBankaHesap` yeni alan mapping/index/FK (bagli banka self-reference dahil)

### Migration
- Yeni migration:
  - `20260428201154_ExpandFinancialAccountsModel`
- Icerik:
  - `MuhasebeHesapPlanlari` tablosuna `TesisId` eklendi
  - `KasaBankaHesaplari` tablosuna yeni finansal alanlar eklendi
  - filtered unique indexler yeni modele gore guncellendi
  - backfill SQL eklendi:
    - mevcut finansal hesaplar tipine gore kodlar yeniden olusturulur
    - ilgili tesiste detay muhasebe hesaplari olusturulur/iliskilendirilir
    - sayaç tablosu (`MuhasebeHesapKoduSayaclari`) son degerlerle guncellenir
    - `1.10.109` yoksa seed edilir (`KREDI KARTLARI`)

### Frontend
- `muhasebe/kasa-banka-hesaplari` ekrani finansal hesaplar formatina alindi:
  - tek `Yeni` butonu
  - once hesap tipi secim dialogu
  - tip secimine gore dinamik alanlar
  - kod readonly + `Sistem tarafından oluşturulacak` placeholder
  - `Valör Süresi (Gun)` alani ve yardim metni eklendi
  - liste kolonlari tip/kod/ad/tesis/para birimi/valör/banka/durum olacak sekilde guncellendi
  - tip filtresi eklendi

### Dogrulama
- `dotnet build backend/STYS.csproj` -> BASARILI
- `npm run build` -> BASARILI (mevcut bundle budget warningleri disinda)
## Tur 138 - Standart Depoların Tüm Tesislere Seed Edilmesi
- Eski sistemden gelen 13 standart depo tüm aktif tesislere seed edildi.
- Her tesis için depo muhasebe detay hesapları Depo ana hesabı altında üretildi.
- Eski Muhasebe_kodu değerleri Aciklama alanında referans olarak saklandı.
- Cikis_Muhasebe_Kodu değerleri NULL olduğu için DepoCikisGrup oluşturulmadı.
- Migration: `20260429200000_SeedStandartDepolariTumTesislere`.
- Not: Frontend tarafında değişiklik yapılmadı.
- Backend build: Başarılı (`dotnet build backend/STYS.csproj /p:NoWarn=NU1903`).
- Frontend build: Bu turda frontend değişikliği olmadığı için çalıştırılmadı.

---
## Tur 141 - TasinirKodMuhasebeHesapEsleme Unique Index Düzeltmesi
- `TasinirKodMuhasebeHesapEsleme` entity'sindeki unique index `(TasinirKodId, MuhasebeHesapPlaniId, IslemTuru)` → `(TasinirKodId, MalzemeTipi, HareketTipi)` olarak değiştirildi.
- Migration `20260514090929_AddMalzemeTipiHareketTipiToTasinirKodMuhasebeHesapEsleme`:
  - Sütun uzunlukları `nvarchar(32)` → `nvarchar(64)` (DbContext ile uyumlu hale getirildi).
  - Unique index `TasinirKodId_MalzemeTipi_HareketTipi` olarak değiştirildi, filtre `[IsDeleted] = 0 AND [AktifMi] = 1 AND [VarsayilanMi] = 1`.
- ModelSnapshot (`StysAppDbContextModelSnapshot.cs`) zaten yeni index ile uyumluydu.

### Değiştirilen dosyalar
- `backend/Infrastructure/EntityFramework/Migrations/20260514090929_AddMalzemeTipiHareketTipiToTasinirKodMuhasebeHesapEsleme.cs`
- `changes.md`

### Build sonuçları
- Backend: Başarılı (`dotnet build backend/STYS.csproj`)
- Frontend: Bu turda frontend değişikliği yok.

---

## Tur 142 - Taşınır Kod ↔ Muhasebe Hesap Eşleme Altyapısı

### Özet
Taşınır kod (malzeme türü/kategorisi) ile muhasebe hesapları arasında eşleme yapılmasını sağlayan `TasinirKodMuhasebeHesapEsleme` entity'si ve bağlı CRUD yapıları (DTO, Repository, Service, Controller, Mapping, Permission) oluşturuldu. Her taşınır kod için bir stok hesabı, bir maliyet/gider hesabı, bir gelir hesabı tanımlanabilir. Tesis bazlı override desteği mevcut.

### Yeni Dosyalar
- backend/Muhasebe/MuhasebeHesapPlanlari/Entities/HesapTipleri.cs — enum (AnaHesap, AltHesap, YardimciHesap)
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Entities/TasinirKodMuhasebeHesapEsleme.cs — entity
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Dtos/TasinirKodMuhasebeHesapEslemeDtos.cs — DTO + Request/Filter sınıfları
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Repositories/ITasinirKodMuhasebeHesapEslemeRepository.cs — repository interface
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Repositories/TasinirKodMuhasebeHesapEslemeRepository.cs — repository implementation
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Services/ITasinirKodMuhasebeHesapEslemeService.cs — service interface
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Services/TasinirKodMuhasebeHesapEslemeService.cs — service implementation
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Controllers/TasinirKodMuhasebeHesapEslemeController.cs — CRUD controller
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Mapping/TasinirKodMuhasebeHesapEslemeProfile.cs — AutoMapper profile

### Değiştirilen Dosyalar
- backend/Muhasebe/Common/Constants/MuhasebeAnaHesapKodlari.cs — 7 yeni ana hesap kodu eklendi (150, 151, 152, 153, 157, 600, 602)
- backend/Muhasebe/MuhasebeHesapPlanlari/Dtos/MuhasebeHesapPlaniDtos.cs — HesapTipi, Aciklama, AnaHesapKodu alanları eklendi
- backend/Muhasebe/MuhasebeHesapPlanlari/Entities/MuhasebeHesapPlani.cs — AnaHesapKodu alanı eklendi
- backend/Data/StysAppDbContext.cs — DbSet + Fluent API konfigürasyonu eklendi
- backend/Muhasebe/TasinirKodlari/Entities/TasinirKod.cs — TasinirKodMuhasebeHesapEslemeleri navigation property eklendi
- backend/StructurePermissions.cs — TasinirKodMuhasebeHesapEslemeYonetimi izin sınıfı eklendi
- backend/Program.cs — ITasinirKodMuhasebeHesapEslemeService DI kaydı eklendi
- changes.md — bu dosya

### Build Sonuclari (Tur 142)
- Backend: BASARILI (`dotnet build backend/STYS.csproj` — 5 warning, 0 error)
- Frontend: CALISTIRILMADI (frontend değişikliği yok)

---

## Tur 143 - TasinirKodMuhasebeHesapEsleme: MalzemeTipi + HareketTipi ve Varsayılan Getirme
- `TasinirKodMuhasebeHesapEsleme` entity'sine `MalzemeTipi` (max 32) ve `HareketTipi` (max 32) alanları eklendi.
- `VarsayilanMi` alanı zorunlu (non-nullable) hale getirildi.
- DTO ve request modellerine yeni alanlar eklendi (`CreateTasinirKodMuhasebeHesapEslemeRequest`, `UpdateTasinirKodMuhasebeHesapEslemeRequest`, `TasinirKodMuhasebeHesapEslemeDto`).
- Repository interface ve implementation'a `GetVarsayilanAsync(tasinirKodId, islemTuru, malzemeTipi, hareketTipi)` eklendi.
- Service interface ve implementation'a aynı metod + validasyon eklendi (varsayılan bulunamazsa 404, birden fazla varsa hata).
- Controller'a `GET /{tasinirKodId}/varsayilan` endpoint'i eklendi.
- DbContext Fluent API'de `MalzemeTipi`, `HareketTipi` ve `VarsayilanMi` alan konfigürasyonu güncellendi.
- Index unique constraint `(TasinirKodId, MuhasebeHesapPlaniId, IslemTuru)` olarak düzeltildi.
- Migration eklendi: `AddMalzemeTipiHareketTipiToTasinirKodMuhasebeHesapEsleme`.

### Olusturulan Dosyalar (Tur 143)
- backend/Infrastructure/EntityFramework/Migrations/20260514090931_AddMalzemeTipiHareketTipiToTasinirKodMuhasebeHesapEsleme.cs
- backend/Infrastructure/EntityFramework/Migrations/20260514090931_AddMalzemeTipiHareketTipiToTasinirKodMuhasebeHesapEsleme.Designer.cs

### Degisen Dosyalar (Tur 143)
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Entities/TasinirKodMuhasebeHesapEsleme.cs
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Dtos/TasinirKodMuhasebeHesapEslemeDtos.cs
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Repositories/ITasinirKodMuhasebeHesapEslemeRepository.cs
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Repositories/TasinirKodMuhasebeHesapEslemeRepository.cs
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Services/ITasinirKodMuhasebeHesapEslemeService.cs
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Services/TasinirKodMuhasebeHesapEslemeService.cs
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Controllers/TasinirKodMuhasebeHesapEslemeController.cs
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Infrastructure/EntityFramework/Migrations/StysAppDbContextModelSnapshot.cs
- changes.md

### Build Sonuclari (Tur 143)
- Backend: BASARILI
- Frontend: CALISTIRILMADI (frontend degisikligi yok)
---

## Tur 141 - .clinerules/changelog-updates.md Kural Dosyasi Olusturma

### Yapilanlar
- Her tur/islem sonrasi `changes.md` dosyasinin guncellenmesini zorunlu kilan Cline kural dosyasi olusturuldu.
- Kural dosyasi STYS projesine ozgu olup, degisiklik turu sonunda build sonuclari dahil tum bilgilerin kaydedilmesini sart kosar.

### Yeni Dosyalar
- .clinerules/changelog-updates.md

### Degisen Dosyalar
- changes.md

### Build Sonuclari (Tur 141)
- Backend: CALISTIRILMADI (islem sadece dosya olusturma)
- Frontend: CALISTIRILMADI (islem sadece dosya olusturma)
## Tur 139 - Muhasebe Hesap Planı TamKod Ana Hesap Arama Düzeltmesi
- MuhasebeDetayHesapService ana hesap araması TamKod öncelikli hale getirildi.
- Kod alanı geriye uyumluluk için fallback olarak desteklendi.
- TamKod eşleşmesi yoksa Kod ile arama yapılıyor; birden fazla Kod eşleşmesinde açık hata üretiliyor.
- Detay hesaplarda Kod ve TamKod değerlerinin tam detay kod olarak kalması korundu.
- Kredi kartı için 1.10.109 kullanımının bilinçli kurum içi karar olduğu not edildi.
- Tek Düzen Hesap Planı seed verileri bu turda değiştirilmedi; ayrı turda doğrulama/düzeltme migration’ı değerlendirilmeli.
- Seed migration değiştirilmedi.
- Schema değişikliği olmadığı için migration yazılmadı.
- Kredi kartı / POS takipleri için `1.10.109` kullanımı bilinçli kurum içi karar olarak korunmuştur.
- Tek Düzen Hesap Planı’nda 109 resmi adlandırılmış hesap olarak görünmese de STYS içinde kredi kartı/pos finansal hesap kırılımı için kullanılmaya devam edecektir.
- Bu karar ileride muhasebe politikası değişirse `MuhasebeAnaHesapKodlari` üzerinden revize edilebilir.
- Backend build: Başarılı (`dotnet build backend/STYS.csproj /p:NoWarn=NU1903`).
- Frontend build: Bu turda frontend değişikliği olmadığı için çalıştırılmadı.
## Tur 140 - Muhasebe Detay Kod Formatı D3 Standardı
- Muhasebe detay kod üretimi tüm akışlarda `...001` formatına çekildi (`D3`).
- Ortak `MuhasebeDetayHesapService` kod üretimi `AnaKod.SiraNo(D3)` olacak şekilde güncellendi.
- Yeni migration eklendi: `20260430012000_NormalizeMuhasebeDetayKodFormatToD3`.
- Migration; `CariKartlar`, `KasaBankaHesaplari`, `Depolar`, `TasinirKartlar` ve bağlı `MuhasebeHesapPlanlari` kayıtlarını `D3` formatına normalize eder.
- Sayaç (`MuhasebeHesapKoduSayaclari`) integer mantığı korunur; sadece string kod formatı standartlaştırılır.
- Backend build: Başarılı (`dotnet build backend/STYS.csproj /p:NoWarn=NU1903`).
- Frontend build: Bu turda frontend değişikliği olmadığı için çalıştırılmadı.

## Tur 142 - TasinirKartService AddAsync: Sabit Hesap Kodundan Dinamik Eşlemeye Geçiş

### Yapılan Değişiklikler
- `TasinirKartService.AddAsync` içindeki `ResolveTasinirKartAnaHesapKodu()` çağrısı kaldırıldı.
- Yerine `ResolveTasinirKartAnaHesapKoduAsync(dto, CancellationToken.None)` eklendi.
- Yeni metot `ResolveTasinirKartAnaHesapKoduAsync`:
  - `ITasinirKodMuhasebeHesapEslemeService.GetVarsayilanAsync(dto.TasinirKodId, dto.MalzemeTipi, "Giris", cancellationToken)` ile varsayılan eşlemeyi sorgular.
  - Eşleme bulunamazsa → `"Seçilen taşınır kodu ve malzeme tipi için giriş hareketine ait varsayılan muhasebe hesap eşlemesi bulunamadı."` hatası (400).
  - Eşlemedeki `MuhasebeHesapPlaniId` ile `_dbContext.MuhasebeHesapPlanlari`'ndan hesap aranır (`!x.IsDeleted`).
  - Hesap bulunamazsa → `"Eşlenen muhasebe hesabı bulunamadı."` hatası (400).
  - Hesap pasifse (`!anaHesap.AktifMi`) → `"Eşlenen muhasebe hesabı aktif değildir."` hatası (400).
  - Başarılı → `anaHesap.TamKod` döndürülür.
- Eski `ResolveTasinirKartAnaHesapKodu()` metodu kaldırıldı (artık `MuhasebeAnaHesapKodlari.TasinirKart` sabiti kullanılmıyor).
- Mevcut transaction ve `MuhasebeDetayHesapService.CreateOrResolveDetayHesapAsync` akışı korundu.

### Beklenen Davranış
- Taşınır kart oluştururken sabit `MuhasebeAnaHesapKodlari.TasinirKart` kullanılmayacak.
- `TasinirKodId + MalzemeTipi + HareketTipi=Giris` için varsayılan eşleme bulunacak.
- Bulunan eşlemenin `MuhasebeHesapPlani.TamKod` değeri ana hesap kodu olarak `MuhasebeDetayHesapService`'e gönderilecek.

### Değişen Dosyalar
- backend/Muhasebe/TasinirKartlari/Services/TasinirKartService.cs

### Build Sonuçları (Tur 142)
- Backend: BAŞARILI (`dotnet build backend/STYS.csproj`) — 0 Error, 6 pre-existing warning
- Frontend: Çalıştırılmadı (değişiklik yok)

## Tur 143 - MuhasebeHesapPlani Detay/Hareket Görebilir Hesap Mantığının Sağlamlaştırılması

### Yapılan Değişiklikler

#### 1. Entity, DTO, Migration (Değişiklik Yok)
- `MuhasebeHesapPlani` entity'sinde (`DetayHesapMi`, `HareketGorebilirMi`) alanları zaten mevcut.
- DTO'lar (`CreateMuhasebeHesapPlaniRequest`, `UpdateMuhasebeHesapPlaniRequest`, `MuhasebeHesapPlaniDto`) bu alanları zaten taşıyor.
- Database tablosunda `DetayHesapMi` ve `HareketGorebilirMi` sütunları (`bit`) halihazırda snapshot'ta mevcut — yeni migration yazılmadı.
- `StysAppDbContext` Fluent API konfigürasyonuna `.IsRequired()` eklendi.

#### 2. MuhasebeDetayHesapService — Otomatik Detay Hesap Oluşturma
- `CreateOrResolveDetayHesapAsync` içinde yeni detay hesap oluşturulurken `DetayHesapMi = true` ve `HareketGorebilirMi = true` ayarlandı (yeni entity `new MuhasebeHesapPlani`).
- Mevcut detay hesap güncellenirken (`existing.Ad`, `existing.AktifMi` yanında) `existing.DetayHesapMi = true` ve `existing.HareketGorebilirMi = true` eklendi.

#### 3. MuhasebeHesapPlaniService — NormalizeAndValidateAsync
- `HareketGorebilirMi = true && DetayHesapMi = false` durumu için validasyon hatası eklendi: `"Hareket gorebilir hesap ayni zamanda detay hesap olmalidir."` (400).

#### 4. TasinirKodMuhasebeHesapEslemeService — ValidateAsync
- `StysAppDbContext` enjeksiyonu eklendi (constructor + private field).
- `ValidateAsync` içinde `MuhasebeHesapPlaniId` ile hesap sorgulanır:
  - Hesap bulunamazsa → `"Seçilen muhasebe hesabı bulunamadı."` (400)
  - `IsDeleted` → `"Seçilen muhasebe hesabı silinmiştir."` (400)
  - `!AktifMi` → `"Seçilen muhasebe hesabı aktif değildir."` (400)
  - `TesisId.HasValue` → `"Taşınır kod muhasebe eşlemesi için tesis bağımsız ana hesap seçilmelidir."` (400)
  - `DetayHesapMi` → `"Taşınır kod muhasebe eşlemesi için detay hesap değil, ana hesap seçilmelidir."` (400)
  - `HareketGorebilirMi` → `"Taşınır kod muhasebe eşlemesi için hareket görebilir detay hesap seçilemez."` (400)

#### 5. TasinirKartService — ResolveTasinirKartAnaHesapKoduAsync
- Mevcut `!IsDeleted` ve `!AktifMi` kontrollerinin ardından:
  - `TesisId.HasValue` → `"Eşlenen muhasebe hesabı tesis bağımsız ana hesap olmalıdır."` (400)
  - `DetayHesapMi` → `"Eşlenen muhasebe hesabı detay hesap olmamalıdır."` (400)
  - `HareketGorebilirMi` → `"Eşlenen muhasebe hesabı hareket görebilir detay hesap olmamalıdır."` (400)
  - kontrolleri eklendi.
  -
  - **Not (Tur 144):** Bu kontrollerin yönü Tur 144'te düzeltilmiştir — eşleme ana hesaba (`DetayHesapMi=false`, `HareketGorebilirMi=false`, `TesisId=null`) yapılmalıdır; otomatik detay hesap `MuhasebeDetayHesapService` tarafından ayrıca oluşturulur.

### Değişen Dosyalar
- backend/Infrastructure/EntityFramework/StysAppDbContext.cs
- backend/Muhasebe/Common/Services/MuhasebeDetayHesapService.cs
- backend/Muhasebe/MuhasebeHesapPlanlari/Services/MuhasebeHesapPlaniService.cs
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Services/TasinirKodMuhasebeHesapEslemeService.cs
- backend/Muhasebe/TasinirKartlari/Services/TasinirKartService.cs

### Manuel Test Senaryoları
1. **Tesis Bazlı Hesaba Eşleme Yapılamaz**: `TesisId` dolu olan bir hesap eşlemeye bağlanmaya çalışıldığında 400 hatası alınmalı.
2. **Detay Hesaba Eşleme Yapılamaz**: `DetayHesapMi=true` olan bir hesap eşlemeye bağlanmaya çalışıldığında 400 hatası alınmalı.
3. **Hareket Görebilir Hesaba Eşleme Yapılamaz**: `HareketGorebilirMi=true` olan bir hesap eşlemeye bağlanmaya çalışıldığında 400 hatası alınmalı.
4. **Silinmiş Hesaba Eşleme Yapılamaz**: `IsDeleted=true` hesaba eşleme denendiğinde 400 hatası alınmalı.
5. **Pasif Hesaba Eşleme Yapılamaz**: `AktifMi=false` hesaba eşleme denendiğinde 400 hatası alınmalı.
6. **Hareket Görebilir Ama Detay Olmayan Hesap Oluşturulamaz**: API üzerinden `HareketGorebilirMi=true, DetayHesapMi=false` gönderildiğinde 400 hatası alınmalı.
7. **Otomatik Detay Hesap Doğru Bayraklarla Oluşur**: `TasinirKartService.AddAsync` → `MuhasebeDetayHesapService` akışında oluşan yeni detay hesap `DetayHesapMi=true, HareketGorebilirMi=true` olmalı.

### Build Sonuçları (Tur 143)
- Backend: BAŞARILI (`dotnet build backend/STYS.csproj`) — 0 Error, 6 pre-existing warning
- Frontend: Çalıştırılmadı (değişiklik yok)

## Tur 144 - Faz 3 Düzeltmesi: Eşleme Ana Hesap Mantığı

### Problem
Tur 143'te `TasinirKodMuhasebeHesapEsleme` için seçilecek `MuhasebeHesapPlani` kaydının `DetayHesapMi=true` ve `HareketGorebilirMi=true` olması zorunlu kılınmıştı. Ancak mimari gereği eşleme tablosu **ana hesabı** göstermelidir — detay hesap `MuhasebeDetayHesapService` tarafından otomatik oluşturulur.

### Yapılan Değişiklikler

#### TasinirKodMuhasebeHesapEslemeService.ValidateAsync
- `!DetayHesapMi` → hata kontrolleri KALDIRILDI
- `!HareketGorebilirMi` → hata kontrolleri KALDIRILDI
- Yerine eklendi:
  - `TesisId.HasValue` → `"Taşınır kod muhasebe eşlemesi için tesis bağımsız ana hesap seçilmelidir."` (400)
  - `DetayHesapMi` → `"Taşınır kod muhasebe eşlemesi için detay hesap değil, ana hesap seçilmelidir."` (400)
  - `HareketGorebilirMi` → `"Taşınır kod muhasebe eşlemesi için hareket görebilir detay hesap seçilemez."` (400)

#### TasinirKartService.ResolveTasinirKartAnaHesapKoduAsync
- `!DetayHesapMi` → hata kontrolü KALDIRILDI
- `!HareketGorebilirMi` → hata kontrolü KALDIRILDI
- Yerine eklendi:
  - `TesisId.HasValue` → `"Eşlenen muhasebe hesabı tesis bağımsız ana hesap olmalıdır."` (400)
  - `DetayHesapMi` → `"Eşlenen muhasebe hesabı detay hesap olmamalıdır."` (400)
  - `HareketGorebilirMi` → `"Eşlenen muhasebe hesabı hareket görebilir detay hesap olmamalıdır."` (400)

### Doğru Mimari
```
TasinirKodMuhasebeHesapEsleme → Ana Hesap (ör: 1.15.150, TesisId=null, DetayHesapMi=false, HareketGorebilirMi=false)
         ↓ TamKod
MuhasebeDetayHesapService → Otomatik Detay Hesap (ör: 1.15.150.001, TesisId=dolu, DetayHesapMi=true, HareketGorebilirMi=true)
```

### Değişmeyenler
- `MuhasebeDetayHesapService`: Detay hesaplarda `DetayHesapMi=true`, `HareketGorebilirMi=true` kalır.
- `MuhasebeHesapPlaniService`: `HareketGorebilirMi=true && DetayHesapMi=false` validasyonu kalır.
- `StysAppDbContext`: `.IsRequired()` konfigürasyonu kalır.
- Migration yazılmadı.

### Değişen Dosyalar (Tur 144)
- backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri/Services/TasinirKodMuhasebeHesapEslemeService.cs
- backend/Muhasebe/TasinirKartlari/Services/TasinirKartService.cs

### Manuel Test Senaryoları (Tur 144)
1. **Ana hesap eşlemeye bağlanabilmeli**: `1.15.150` (TesisId=null, DetayHesapMi=false, HareketGorebilirMi=false) hesabı eşlemeye bağlanabilmeli.
2. **Tesis bazlı detay hesap eşlemeye bağlanamamalı**: `TesisId` dolu detay hesap seçilirse 400 hatası alınmalı.
3. **Taşınır kart oluşturmada ana hesap altında detay hesap oluşmalı**: `TasinirKartService.AddAsync` → `1.15.150` altında yeni detay hesap (örn. `1.15.150.001`) oluşmalı.
4. **Oluşan detay hesap doğru bayraklı olmalı**: Yeni detay hesap `DetayHesapMi=true, HareketGorebilirMi=true`.

### Build Sonuçları (Tur 144)
- Backend: BAŞARILI (`dotnet build backend/STYS.csproj`) — 0 Error, 6 pre-existing warning
- Frontend: Çalıştırılmadı (değişiklik yok)

## Tur 145 - Faz 4: KDV Vergi Hesap Eşleme Altyapısı

### Amaç
Stok hareketlerindeki KDV hesaplarının doğru belirlenebilmesi için vergi tipi ve oranına göre alış/satış KDV hesap eşleme altyapısı oluşturuldu. Tesis bazlı veya genel tanım yapılabilir.

### Yeni Dosyalar

#### 1. Constants
- **`backend/Muhasebe/Common/Constants/VergiTipleri.cs`**
  - Desteklenen vergi tipleri: `"KDV"` (başlangıçta sadece KDV)
  - `Hepsi` dizisi ile genişletilebilir yapı

#### 2. Entity
- **`backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Entities/MuhasebeVergiHesapEsleme.cs`**
  - `BaseEntity<int>` tabanlı
  - Alanlar: `TesisId` (int?, nullable), `VergiTipi` (string), `Oran` (decimal), `AlisKdvHesapId` (int), `SatisKdvHesapId` (int), `AktifMi` (bool)
  - Navigation: `AlisKdvHesap`, `SatisKdvHesap` → `MuhasebeHesapPlani`

#### 3. DTOs
- **`backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Dtos/MuhasebeVergiHesapEslemeDtos.cs`**
  - `MuhasebeVergiHesapEslemeDto`: Navigation display alanları (`AlisKdvHesapKodu`, `AlisKdvHesapAdi`, `SatisKdvHesapKodu`, `SatisKdvHesapAdi`)
  - `CreateMuhasebeVergiHesapEslemeRequest`
  - `UpdateMuhasebeVergiHesapEslemeRequest`

#### 4. AutoMapper Profile
- **`backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Mapping/MuhasebeVergiHesapEslemeProfile.cs`**
  - Entity → Dto: navigation display alanları `ForMember` ile eşlenir
  - Dto → Entity, Request → Dto

#### 5. Repository
- **`backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Repositories/IMuhasebeVergiHesapEslemeRepository.cs`**
  - `GetAktifEslemeAsync(vergiTipi, oran, tesisId)` metodu
- **`backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Repositories/MuhasebeVergiHesapEslemeRepository.cs`**
  - Önce tesis özel (`TesisId` dolu) kayıt arar
  - Bulunamazsa genel (`TesisId` null) kayda düşer
  - `AlisKdvHesap` ve `SatisKdvHesap` navigation property'lerini `Include` eder

#### 6. Service
- **`backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Services/IMuhasebeVergiHesapEslemeService.cs`**
  - `IBaseRdbmsService<...>` + `GetAktifEslemeAsync`
- **`backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Services/MuhasebeVergiHesapEslemeService.cs`**
  - `BaseRdbmsService` tabanlı
  - `ValidateAsync` kontrolleri:
    - `VergiTipi` boş olamaz, desteklenen tiplerden olmalı
    - `Oran` 0-100 arası olmalı
    - `AlisKdvHesapId` ve `SatisKdvHesapId` > 0
    - Alış ve satış KDV hesapları: mevcut, silinmemiş, aktif, tesis bağımsız (`TesisId=null`), `DetayHesapMi=false`, `HareketGorebilirMi=false`
  - `ValidateKdvHesapAsync` helper: ana hesap zorunluluğu kontrolleri

#### 7. Controller
- **`backend/Muhasebe/MuhasebeVergiHesapEslemeleri/Controllers/MuhasebeVergiHesapEslemeController.cs`**
  - Route: `api/muhasebe/vergi-hesap-esleme`
  - Endpoint'ler:
    - `GET /aktif?vergiTipi=&oran=&tesisId=` → aktif eşleme sorgulama
    - `GET /` → tümünü listele
    - `GET /{id}` → tekil getir
    - `POST /` → yeni oluştur
    - `PUT /{id}` → güncelle
    - `DELETE /{id}` → sil
  - Yetkilendirme: `MuhasebeVergiHesapEslemeYonetimi.View` / `.Manage`

### Değişen Dosyalar

#### 8. StysAppDbContext
- **`backend/Infrastructure/EntityFramework/StysAppDbContext.cs`**
  - `DbSet<MuhasebeVergiHesapEsleme> MuhasebeVergiHesapEslemeleri` eklendi
  - Fluent API konfigürasyonu:
    - Tablo: `MuhasebeVergiHesapEslemeleri`, schema: `muhasebe`
    - `VergiTipi`: nvarchar(32), required
    - `Oran`: decimal(5,2)
    - `AktifMi`: required
    - İki filtrelenmiş unique index:
      1. `[VergiTipi, Oran]` WHERE `IsDeleted=0 AND AktifMi=1 AND TesisId IS NULL` (genel kayıt)
      2. `[TesisId, VergiTipi, Oran]` WHERE `IsDeleted=0 AND AktifMi=1 AND TesisId IS NOT NULL` (tesis özel)
    - Navigation: `AlisKdvHesap`, `SatisKdvHesap` → `Restrict` delete behavior

#### 9. StructurePermissions
- **`backend/StructurePermissions.cs`**
  - `MuhasebeVergiHesapEslemeYonetimi` sınıfı eklendi: `Menu = 1216`, `View = 1217`, `Manage = 1218`

#### 10. Program.cs
- **`backend/Program.cs`**
  - DI kaydı: `builder.Services.AddScoped<IMuhasebeVergiHesapEslemeService, MuhasebeVergiHesapEslemeService>()`
  - Using: `STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Services`

### Migration
- Adı: `AddMuhasebeVergiHesapEsleme`
- Tablo: `MuhasebeVergiHesapEslemeleri` (schema: `muhasebe`)
- Sütunlar: `Id`, `TesisId` (nullable), `VergiTipi` (nvarchar 32), `Oran` (decimal 5,2), `AlisKdvHesapId`, `SatisKdvHesapId`, `AktifMi` + BaseEntity alanları
- FK: `AlisKdvHesapId` → `MuhasebeHesapPlanlari(Id)` Restrict
- FK: `SatisKdvHesapId` → `MuhasebeHesapPlanlari(Id)` Restrict
- Unique index 1: `[TesisId, VergiTipi, Oran]` (tesis özel, filtered)
- Unique index 2: `[VergiTipi, Oran]` (genel, filtered)

### Manuel Test Senaryoları
1. **Genel KDV eşlemesi oluşturulabilmeli**: `TesisId=null`, `VergiTipi=KDV`, `Oran=20`, geçerli ana hesaplarla POST → 200
2. **Aynı vergi tipi+orana ikinci genel kayıt eklenememeli**: Duplicate unique index → veritabanı hatası
3. **Tesis özel KDV eşlemesi genel kaydı override etmeli**: `GET /aktif?vergiTipi=KDV&oran=20&tesisId=1` → tesis özel kayıt dönmeli, yoksa genel kayıt dönmeli
4. **KDV hesapları ana hesap olmalı**: `TesisId` dolu veya `DetayHesapMi=true` hesap seçilirse 400 hatası
5. **Silinmiş/Pasif hesap seçilememeli**: `IsDeleted=true` veya `AktifMi=false` hesap → 400
6. **Geçersiz vergi tipi reddedilmeli**: `VergiTipi=OTV` → 400 "Desteklenmeyen vergi tipi"
7. **Oran 0-100 dışı reddedilmeli**: `Oran=150` → 400

### Build Sonuçları (Tur 145)
- Backend: BAŞARILI (`dotnet build backend/STYS.csproj`) — 0 Error, 5 pre-existing warning
- Migration: `dotnet ef migrations add AddMuhasebeVergiHesapEsleme` — BAŞARILI
- Frontend: Çalıştırılmadı (değişiklik yok)

## Tur 146 - Faz 5: Muhasebe Fişi Temel Altyapısı

### Amaç
Manuel/taslak muhasebe fişi oluşturma altyapısı kuruldu. Bu fazda sadece manuel fiş girişi yapılabilir; stok hareketinden otomatik fiş üretme, cari/kasa/banka hareketlerine dokunma, dönemler, yevmiye no sayacı, raporlama ve KDV eşleme bağlantısı yapılmadı.

### Yeni Dosyalar

#### 1. Constants (3 adet)
- **`backend/Muhasebe/Common/Constants/MuhasebeFisTipleri.cs`**
  - Tip listesi: Mahsup, Tahsil, Tediye, Acilis, Kapanis, Stok, Duzeltme
- **`backend/Muhasebe/Common/Constants/MuhasebeFisDurumlari.cs`**
  - Durum listesi: Taslak, Onayli, Iptal, TersKayit
- **`backend/Muhasebe/Common/Constants/MuhasebeKaynakModulleri.cs`**
  - Kaynak modülleri: Manuel, StokHareket, CariHareket, KasaHareket, BankaHareket, TahsilatOdemeBelgesi

#### 2. Entities (2 adet)
- **`backend/Muhasebe/MuhasebeFisleri/Entities/MuhasebeFis.cs`**
  - `BaseEntity<int>` tabanlı
  - Alanlar: TesisId, MaliYil, Donem, FisNo, YevmiyeNo, FisTarihi, FisTipi, KaynakModul, KaynakId, Durum, ToplamBorc, ToplamAlacak, Aciklama
  - Navigation: `Satirlar` → `ICollection<MuhasebeFisSatir>`
- **`backend/Muhasebe/MuhasebeFisleri/Entities/MuhasebeFisSatir.cs`**
  - `BaseEntity<int>` tabanlı
  - Alanlar: MuhasebeFisId, MuhasebeHesapPlaniId, SiraNo, Borc, Alacak, ParaBirimi, Kur, CariKartId, TasinirKartId, DepoId, KasaBankaHesapId, Aciklama
  - Navigation: `MuhasebeFis`, `MuhasebeHesapPlani`

#### 3. DTOs
- **`backend/Muhasebe/MuhasebeFisleri/Dtos/MuhasebeFisDtos.cs`**
  - `MuhasebeFisDto`: Satirlar listesi + BaseRdbmsDto alanları
  - `MuhasebeFisSatirDto`: MuhasebeHesapKodu, MuhasebeHesapAdi display alanları
  - `CreateMuhasebeFisRequest`: TesisId, MaliYil, Donem, FisTarihi, FisTipi, KaynakModul, KaynakId, Aciklama, Satirlar
  - `CreateMuhasebeFisSatirRequest`: MuhasebeHesapPlaniId, SiraNo, Borc, Alacak, ParaBirimi, Kur + nullable referans alanları
  - `UpdateMuhasebeFisRequest`: TesisId, MaliYil, Donem, FisTarihi, FisTipi, Aciklama, Satirlar

#### 4. AutoMapper Profile
- **`backend/Muhasebe/MuhasebeFisleri/Mapping/MuhasebeFisProfile.cs`**
  - Entity↔Dto, Request→Dto, Request→Entity eşlemeleri
  - Satır display alanları `ForMember` ile navigation'dan çözülür

#### 5. Repository
- **`backend/Muhasebe/MuhasebeFisleri/Repositories/IMuhasebeFisRepository.cs`**
  - `GetByIdWithSatirlarAsync(int id)` — fişi satırları ve hesap planı bilgileriyle getirir
  - `GetByKaynakAsync(string kaynakModul, int kaynakId)` — kaynağa göre fişleri listeler
- **`backend/Muhasebe/MuhasebeFisleri/Repositories/MuhasebeFisRepository.cs`**
  - `BaseRdbmsRepository` tabanlı

#### 6. Service
- **`backend/Muhasebe/MuhasebeFisleri/Services/IMuhasebeFisService.cs`**
  - `IBaseRdbmsService<MuhasebeFisDto, MuhasebeFis, int>` + `GetByIdWithSatirlarAsync`, `GetByKaynakAsync`
- **`backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs`**
  - `AddAsync`: normalize + validate → entity oluştur → `_dbContext` üzerinden kaydet → reload
  - `UpdateAsync`: sadece Taslak fiş güncellenebilir; eski satırlar soft-delete, yeniler eklenir
  - `DeleteAsync`: sadece Taslak fiş silinebilir; satırlar ve fiş soft-delete
  - `NormalizeAndValidateCreateAsync`: 20 validasyon kuralı
    - TesisId > 0, MaliYil 2000-2100, Donem 1-12
    - FisTarihi zorunlu, FisTipi desteklenenlerden
    - KaynakModul boşsa "Manuel", desteklenenlerden olmalı
    - En az 2 satır, her satırda hesap mevcut/silinmemiş/aktif/DetayHesapMi=true/HareketGorebilirMi=true
    - Borc ve Alacak aynı anda >0 olamaz, ikisi de 0 olamaz, negatif olamaz
    - ToplamBorc = ToplamAlacak
    - ParaBirimi boşsa "TRY", Kur ≤ 0 ise 1, SiraNo boş/0 ise otomatik

#### 7. Controller
- **`backend/Muhasebe/MuhasebeFisleri/Controllers/MuhasebeFisController.cs`**
  - Route: `api/muhasebe/fisler`
  - Endpoint'ler:
    - `GET /` → tüm fişleri listele (View)
    - `GET /{id}` → fişi satırlarıyla getir (View)
    - `GET /by-kaynak?kaynakModul=&kaynakId=` → kaynağa göre fişler (View)
    - `POST /` → yeni fiş oluştur (Manage)
    - `PUT /{id}` → fiş güncelle (Manage)
    - `DELETE /{id}` → fiş sil (Manage)
  - Yetkilendirme: `MuhasebeFisYonetimi.View` / `.Manage`

### Değişen Dosyalar

#### 8. StysAppDbContext
- **`backend/Infrastructure/EntityFramework/StysAppDbContext.cs`**
  - Using: `STYS.Muhasebe.MuhasebeFisleri.Entities`
  - DbSet'ler: `MuhasebeFisler`, `MuhasebeFisSatirlari`
  - Fluent API (`MuhasebeFis`):
    - Table: `MuhasebeFisler`, schema: `muhasebe`
    - FisNo: nvarchar(64), required
    - FisTipi: nvarchar(32), required
    - KaynakModul: nvarchar(64), required
    - Durum: nvarchar(32), required
    - ToplamBorc/ToplamAlacak: decimal(18,2)
    - Aciklama: nvarchar(1024)
    - Index: `[TesisId, FisTarihi]`, `[KaynakModul, KaynakId]` (filtered), `[Durum]`
    - HasMany Satirlar → Cascade delete
  - Fluent API (`MuhasebeFisSatir`):
    - Table: `MuhasebeFisSatirlari`, schema: `muhasebe`
    - Borc/Alacak: decimal(18,2), Kur: decimal(18,6)
    - ParaBirimi: nvarchar(3), required
    - Aciklama: nvarchar(512)
    - Index: `[MuhasebeFisId]`, `[MuhasebeHesapPlaniId]`
    - FK MuhasebeHesapPlani → Restrict

#### 9. StructurePermissions
- **`backend/StructurePermissions.cs`**
  - `MuhasebeFisYonetimi` sınıfı eklendi: Menu, View, Manage

#### 10. Program.cs
- **`backend/Program.cs`**
  - Using: `STYS.Muhasebe.MuhasebeFisleri.Services`
  - DI: `builder.Services.AddScoped<IMuhasebeFisService, MuhasebeFisService>()`

### Migration
- Adı: `AddMuhasebeFisleri` (`20260514221833_AddMuhasebeFisleri`)
- Tablolar:
  - `MuhasebeFisler` (schema: `muhasebe`): TesisId, MaliYil, Donem, FisNo, YevmiyeNo, FisTarihi, FisTipi, KaynakModul, KaynakId, Durum, ToplamBorc, ToplamAlacak, Aciklama + BaseEntity
  - `MuhasebeFisSatirlari` (schema: `muhasebe`): MuhasebeFisId, MuhasebeHesapPlaniId, SiraNo, Borc, Alacak, ParaBirimi, Kur, CariKartId, TasinirKartId, DepoId, KasaBankaHesapId, Aciklama + BaseEntity
- FK: `MuhasebeFisId` → `MuhasebeFisler(Id)` Cascade
- FK: `MuhasebeHesapPlaniId` → `MuhasebeHesapPlanlari(Id)` Restrict

### Manuel Test Senaryoları
1. **İki satırlı taslak mahsup fişi oluştur**: Borç 1000, Alacak 1000 → 200 OK, Durum=Taslak, FisNo TASLAK-... formatında
2. **Toplam borç/alacak eşit değilse hata**: Borç 1000, Alacak 500 → 400 "eşit olmalıdır"
3. **Aynı satırda hem borç hem alacak**: 400 "hem borç hem alacak girilemez"
4. **Borç/alacak ikisi de 0**: 400 "borç veya alacak girilmelidir"
5. **Ana hesap seçilirse**: DetayHesapMi=false hesap → 400 "ana hesap seçilemez"
6. **Detay+HareketGorebilir hesap**: 200 başarılı
7. **Taslak fiş güncellenebilmeli**: PUT → eski satırlar silinir, yeniler eklenir
8. **Taslak fiş silinebilmeli**: DELETE → 200, fiş ve satırları soft-delete
9. **Onaylı fiş güncellenemez/silinemez**: Onaylı faturası simüle → 400 "taslak durumundaki"

### Build Sonuçları (Tur 146)
- Backend: BAŞARILI (`dotnet build backend/STYS.csproj`) — 0 Error, 5 pre-existing warning
- Migration: `dotnet ef migrations add AddMuhasebeFisleri` — BAŞARILI
- Frontend: Çalıştırılmadı (değişiklik yok)

---

## Tur 146-Düzeltme: MuhasebeFis Modülü Soft Delete Güvenlik Düzeltmesi

**Tarih:** 2026-05-15
**Commit:** devam ediyor

### Amaç

MuhasebeFis modülünde repository ve service katmanlarında soft-delete (IsDeleted) filtrelerinin eksik olduğu tespit edildi.
Silinmiş fiş ve satırların sorgu sonuçlarında dönmeye devam etmesi, güncellenebilmesi ve tekrar silinebilmesi sorunları giderildi.

### 1. Değiştirilen Dosyalar

| Dosya | Açıklama |
|---|---|
| `backend/Muhasebe/MuhasebeFisleri/Repositories/MuhasebeFisRepository.cs` | 2 metoda IsDeleted filtresi eklendi |
| `backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs` | 2 metoda IsDeleted filtresi + yorum düzeltmesi |

### 2. Repository Soft Delete Filtreleri

**GetByIdWithSatirlarAsync:**
```csharp
.Include(x => x.Satirlar.Where(s => !s.IsDeleted))
    .ThenInclude(s => s.MuhasebeHesapPlani)
.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
```
- Fiş: `!x.IsDeleted`
- Satırlar: `s => !s.IsDeleted` (EF Core 10 filtered Include)

**GetByKaynakAsync:**
```csharp
.Include(x => x.Satirlar.Where(s => !s.IsDeleted))
    .ThenInclude(s => s.MuhasebeHesapPlani)
.Where(x => x.KaynakModul == kaynakModul && x.KaynakId == kaynakId && !x.IsDeleted)
```
- Fiş: `!x.IsDeleted`
- Satırlar: `s => !s.IsDeleted`

### 3. Service update/delete Güvenliği

**UpdateAsync:**
- Fiş arama: `x.Id == dto.Id && !x.IsDeleted`
- Include: `Satirlar.Where(s => !s.IsDeleted)`
- Eski satır silme: `existing.Satirlar.Where(s => !s.IsDeleted)` (zaten silinmiş satırları tekrar silmez)

**DeleteAsync:**
- Fiş arama: `x.Id == id && !x.IsDeleted`
- Include: `Satirlar.Where(s => !s.IsDeleted)`
- Satır silme: `existing.Satirlar.Where(s => !s.IsDeleted)`
- Yorum: "soft-delete" → "Platform BaseEntity silme davranışı üzerinden"

### 4. Build Sonucu
- Backend: BAŞARILI — 0 Error, 2 warning (Snappier NU1903 — pre-existing)

### 5. Manuel Test Senaryosu

| # | Test | Beklenen |
|---|---|---|
| 1 | Silinmiş fiş GetByIdWithSatirlarAsync | 404 NotFound |
| 2 | Silinmiş fiş by-kaynak endpoint | Listede görünmemeli |
| 3 | Silinmiş fiş update | 404 "Fiş bulunamadı" |
| 4 | Silinmiş fiş delete | 404 "Fiş bulunamadı" |
| 5 | Silinmiş satırlar fiş detayı | Satirlar[]'da sadece IsDeleted=false olanlar |

---

## Tur 146 — Faz 6: Muhasebe Dönemleri Altyapısı
**Tarih:** 2026-05-15

### 1. Eklenen Dosyalar

| # | Dosya | Açıklama |
|---|---|---|
| 1 | `backend/Muhasebe/MuhasebeDonemleri/Entities/MuhasebeDonem.cs` | Muhasebe dönemi entity (TesisId FK, MaliYil, DonemNo, tarih aralığı, KapaliMi, KapanisTarihi) |
| 2 | `backend/Muhasebe/MuhasebeDonemleri/Dtos/MuhasebeDonemDtos.cs` | MuhasebeDonemDto + CreateMuhasebeDonemRequest + UpdateMuhasebeDonemRequest |
| 3 | `backend/Muhasebe/MuhasebeDonemleri/Mapping/MuhasebeDonemProfile.cs` | AutoMapper profile (Entity→Dto TesisAdi map, Request→Dto) |
| 4 | `backend/Muhasebe/MuhasebeDonemleri/Repositories/IMuhasebeDonemRepository.cs` | Repository interface (GetAktifDonemAsync, GetByTesisYilDonemAsync, TarihAraligiCakisiyorMuAsync) |
| 5 | `backend/Muhasebe/MuhasebeDonemleri/Repositories/MuhasebeDonemRepository.cs` | Repository implementation (filtered queries with IsDeleted=false) |
| 6 | `backend/Muhasebe/MuhasebeDonemleri/Services/IMuhasebeDonemService.cs` | Service interface (GetAktifDonemAsync, DonemKapatAsync, DonemAcAsync) |
| 7 | `backend/Muhasebe/MuhasebeDonemleri/Services/MuhasebeDonemService.cs` | Service implementation (14 validasyon kuralı, CRUD, dönem aç/kapat) |
| 8 | `backend/Muhasebe/MuhasebeDonemleri/Controllers/MuhasebeDonemController.cs` | Controller (`ui/muhasebe/donemler` route) |
| 9 | `backend/Infrastructure/EntityFramework/Migrations/20260515064707_AddMuhasebeDonemleri.cs` | EF Core migration |

### 2. Değiştirilen Dosyalar

| # | Dosya | Değişiklik |
|---|---|---|
| 1 | `backend/Infrastructure/EntityFramework/StysAppDbContext.cs` | using eklendi, DbSet<MuhasebeDonem> eklendi, Fluent API konfigürasyonu eklendi |
| 2 | `backend/StructurePermissions.cs` | MuhasebeDonemYonetimi permission class eklendi (Menu, View, Manage, ClosePeriod) |
| 3 | `backend/Program.cs` | IMuhasebeDonemService DI registration eklendi |
| 4 | `backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs` | IMuhasebeDonemService inject, AddAsync/UpdateAsync içinde açık dönem kontrolü eklendi |

### 3. DbContext Değişikliği

**Fluent API:**
```csharp
entity.ToTable("MuhasebeDonemler", muhasebeSchema);
entity.Property(x => x.Aciklama).HasMaxLength(1024);
entity.HasOne(x => x.Tesis).WithMany().HasForeignKey(x => x.TesisId).OnDelete(DeleteBehavior.Restrict);
entity.HasIndex(x => new { x.TesisId, x.MaliYil, x.DonemNo }).IsUnique().HasFilter("[IsDeleted] = 0");
entity.HasIndex(x => new { x.TesisId, x.BaslangicTarihi, x.BitisTarihi });
entity.HasIndex(x => x.KapaliMi);
```

### 4. Migration Adı
`20260515064707_AddMuhasebeDonemleri`

**Tablo:** `muhasebe.MuhasebeDonemler` (TesisId FK Restrict, 3 index: unique filtered, tarih aralığı, KapaliMi)

### 5. MuhasebeDonemService Validasyonları

1. TesisId > 0 (Geçerli bir tesis seçilmelidir)
2. Tesis var mı, silinmiş mi (Seçilen tesis bulunamadı)
3. MaliYil 2000-2100 aralığında olmalı
4. DonemNo 1-12 aralığında olmalı
5. BaslangicTarihi < BitisTarihi
6. Aynı TesisId + MaliYil + DonemNo için ikinci kayıt oluşturulamaz
7. Aynı tesis içinde tarih aralıkları çakışamaz
8. Create sırasında KapaliMi=false, KapanisTarihi=null
9. Kapalı dönem silinemez
10. Kapalı dönemde tarih/dönem bilgileri değiştirilemez (sadece Aciklama)
11. KapaliMi değişikliği update üzerinden yapılamaz (DonemKapatAsync/DonemAcAsync kullanılmalı)
12. Dönem kapatılırken zaten kapalıysa hata
13. Dönem açılırken zaten açıksa hata
14. Tüm hata mesajları Türkçe, BaseException ile fırlatılır

### 6. MuhasebeFisService Açık Dönem Kontrolü

NormalizeAndValidateCreateAsync sonunda:
- `GetAktifDonemAsync(tesisId, fisTarihi)` çağrısı
- Dönem yoksa: "Fiş tarihi için açık muhasebe dönemi bulunamadı"
- MaliYıl/Dönem uyumsuzsa: "Fişin mali yılı/dönemi, açık muhasebe dönemi ile uyumlu değildir"

### 7. Controller Endpointleri

| Metod | Route | Permission |
|-------|-------|-----------|
| GET | `ui/muhasebe/donemler` | View |
| GET | `ui/muhasebe/donemler/{id}` | View |
| GET | `ui/muhasebe/donemler/aktif?tesisId=&tarih=` | View |
| POST | `ui/muhasebe/donemler` | Manage |
| PUT | `ui/muhasebe/donemler/{id}` | Manage |
| DELETE | `ui/muhasebe/donemler/{id}` | Manage |
| POST | `ui/muhasebe/donemler/{id}/kapat` | ClosePeriod |
| POST | `ui/muhasebe/donemler/{id}/ac` | ClosePeriod |

### 8. Build Sonucu
- Backend: BAŞARILI — 0 Error, 6 warning (Snappier NU1903 + diğer pre-existing)

### 9. Manuel Test Senaryosu

| # | Test | Beklenen |
|---|---|---|
| 1 | TesisId=1, MaliYil=2026, DonemNo=1 için açık dönem oluştur | 200, KapaliMi=false |
| 2 | DTO'da TesisAdi doğru gelmeli | Tesis.Ad değeri |
| 3 | Aynı tesis/yıl/dönem için ikinci kayıt | 400 "Aynı tesis, mali yıl ve dönem için kayıt zaten mevcut" |
| 4 | Aynı tesis içinde çakışan tarih aralıklı dönem | 400 "Seçilen tarih aralığı ... çakışıyor" |
| 5 | Farklı tesis için aynı tarih/yıl/dönem | 200 (farklı tesis, bağımsız) |
| 6 | Açık döneme ait tarihle taslak fiş oluştur | 200 |
| 7 | Dönem kapatıldıktan sonra aynı tarihe fiş | 400 "Fiş tarihi için açık muhasebe dönemi bulunamadı" |
| 8 | Kapalı dönem silinememeli | 400 "Kapalı dönem silinemez" |
| 9 | Kapalı dönem tekrar açılabilmeli | 200, KapaliMi=false, KapanisTarihi=null |

---

## Tur 147 — Faz 6 Düzeltme (2026-05-15)

**Amaç:** Faz 6'da oluşturulan MuhasebeDonemleri modülünde 3 düzeltme: açık dönem kontrolü pozisyonu, TesisAdi include, ve route doğrulaması.

### 1. Route Doğrulaması
- [`MuhasebeDonemController`](backend/Muhasebe/MuhasebeDonemleri/Controllers/MuhasebeDonemController.cs:10) route'u `[Route("ui/muhasebe/donemler")]` — zaten doğru, değişiklik yapılmadı.

### 2. MuhasebeFisService Açık Dönem Kontrolü Düzeltmesi
- [`NormalizeAndValidateCreateAsync`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs:153) içinde açık dönem kontrolü (adım 4b), **"4. FisTarihi zorunlu"** kontrolünden hemen sonraki doğru konuma taşındı.
- Daha önce metodun sonunda (satır validasyonları loop'undan sonra) yer alıyordu — bu, mali yıl/dönem uyumsuzluğunun gereksiz satır validasyonları yapıldıktan sonra yakalanmasına neden oluyordu.
- Kontrol artık FisTipi/KaynakModul/Satirlar validasyonlarından önce çalışıyor.

### 3. TesisAdi Include Düzeltmesi
- [`GetByIdAsync`](backend/Muhasebe/MuhasebeDonemleri/Services/MuhasebeDonemService.cs:29) ve [`GetAllAsync`](backend/Muhasebe/MuhasebeDonemleri/Services/MuhasebeDonemService.cs:35) metodları `Tesis` navigation property include edilerek override edildi.
- Base metodlar include yapmadığı için AutoMapper `TesisAdi` (mapping: `s.Tesis.Ad`) her zaman null dönüyordu.
- Override imzaları base sınıfla uyumlu: `Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null`.
- `AddAsync` zaten reload ile Tesis include ediyordu.

### 4. Build Sonucu
- Backend: BAŞARILI — 0 Error, 6 warning (tümü pre-existing)

### 5. Değiştirilen Dosyalar
Dosya | Değişiklik |
|---|---|
[`MuhasebeFisService.cs`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs:153) | Açık dönem kontrolü "FisTarihi zorunlu" sonrasına taşındı |
[`MuhasebeDonemService.cs`](backend/Muhasebe/MuhasebeDonemleri/Services/MuhasebeDonemService.cs:29) | GetByIdAsync/GetAllAsync override — Tesis include eklendi |
`MuhasebeDonemController.cs` | Değişiklik YOK (route zaten `ui/muhasebe/donemler`) |

### 6. Manuel Test Senaryosu
# | Test | Beklenen |
|---|---|---|
1 | GET `ui/muhasebe/donemler` — dönem listesi | TesisAdi dolu gelmeli |
2 | GET `ui/muhasebe/donemler/{id}` — tek dönem | TesisAdi dolu gelmeli |
3 | Açık dönem yokken fiş oluştur | 400 "Fiş tarihi için açık muhasebe dönemi bulunamadı" |
4 | Açık dönem varken MaliYil/Donem uyumsuz fiş | 400 "Fişin mali yılı/dönemi, açık muhasebe dönemi ile uyumlu değildir" |
5 | Açık dönem varken uyumlu fiş | 200 |
6 | Route değişmedi mi? | `ui/muhasebe/donemler` — evet, aynı |

---

## Tur 148 — Faz 7: Fiş Onaylama ve Yevmiye No Sayacı (2026-05-15)

**Amaç:** Taslak fiş onaylama ve tesis+mali yıl bazlı yevmiye no sayaç altyapısı.

### 1. Yeni Entity: MuhasebeYevmiyeNoSayac
- [`MuhasebeYevmiyeNoSayac.cs`](backend/Muhasebe/MuhasebeFisleri/Entities/MuhasebeYevmiyeNoSayac.cs) — `BaseEntity<int>`, alanlar: `TesisId`, `MaliYil`, `SonNumara`.
- Sayaç tesis ve mali yıl bazlı çalışır.

### 2. DbContext Değişiklikleri
- [`StysAppDbContext`](backend/Infrastructure/EntityFramework/StysAppDbContext.cs:154) içine `public DbSet<MuhasebeYevmiyeNoSayac> MuhasebeYevmiyeNoSayaclari` eklendi.
- Fluent API: `muhasebe.MuhasebeYevmiyeNoSayaclari` tablosu, `TesisId + MaliYil` unique filtered index (`[IsDeleted] = 0`).
- Migration: `20260515073138_AddMuhasebeYevmiyeNoSayac`

### 3. IMuhasebeFisService
- [`OnaylaAsync(int id, CancellationToken)`](backend/Muhasebe/MuhasebeFisleri/Services/IMuhasebeFisService.cs:11) metodu eklendi.

### 4. MuhasebeFisService.OnaylaAsync
- [`MuhasebeFisService.OnaylaAsync`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs:100) — Transaction içinde çalışır.
- Kontrol listesi:
  1. Fiş var ve silinmemiş olmalı
  2. Sadece Taslak fiş onaylanabilir
  3. YevmiyeNo zaten varsa tekrar onaylanamaz
  4. En az iki satır olmalı
  5. ToplamBorc = ToplamAlacak
  6. ToplamBorc > 0
  7. Açık muhasebe dönemi kontrolü (IMuhasebeDonemService)
  8. Satır hesapları: silinmemiş, aktif, DetayHesapMi=true, HareketGorebilirMi=true
  9. YevmiyeNo üretimi (transaction içinde UPDLOCK, ROWLOCK, HOLDLOCK)
  10. Fiş Durum = Onayli, YevmiyeNo set edilir
- Update/Delete: Zaten sadece Taslak fişlere izin veriliyor (değişiklik yok).

### 5. Yevmiye No Üretim Mantığı
- [`YevmiyeNoUretAsync`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs:40) — `MuhasebeHesapKoduSayac` ile aynı SQL lock pattern'i:
  ```sql
  SELECT * FROM [muhasebe].[MuhasebeYevmiyeNoSayaclari] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
  WHERE [IsDeleted] = 0 AND [TesisId] = @tesisId AND [MaliYil] = @maliYil
  ```
- Yoksa oluştur (SonNumara=1), varsa SonNumara bir artır.
- 3 deneme + unique conflict retry + son deneme lock ile.
- Transaction zorunlu — yoksa 500 hata.

### 6. Controller Endpoint
- `POST ui/muhasebe/fisler/{id}/onayla` — [`MuhasebeFisController.Onayla`](backend/Muhasebe/MuhasebeFisleri/Controllers/MuhasebeFisController.cs:78)
- Permission: `MuhasebeFisYonetimi.Manage`

### 7. Build Sonucu
- Backend: BAŞARILI — 0 Error, 6 warning (tümü pre-existing)

### 8. Eklenen Dosyalar
Dosya | Açıklama |
|---|---|
[`MuhasebeYevmiyeNoSayac.cs`](backend/Muhasebe/MuhasebeFisleri/Entities/MuhasebeYevmiyeNoSayac.cs) | Entity |
[`20260515073138_AddMuhasebeYevmiyeNoSayac.cs`](backend/Infrastructure/EntityFramework/Migrations/20260515073138_AddMuhasebeYevmiyeNoSayac.cs) | EF Migration |

### 9. Değiştirilen Dosyalar
Dosya | Değişiklik |
|---|---|
[`StysAppDbContext.cs`](backend/Infrastructure/EntityFramework/StysAppDbContext.cs:154) | DbSet + Fluent API |
[`IMuhasebeFisService.cs`](backend/Muhasebe/MuhasebeFisleri/Services/IMuhasebeFisService.cs:11) | `OnaylaAsync` imzası |
[`MuhasebeFisService.cs`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs:40) | `OnaylaAsync` + `YevmiyeNoUretAsync` + `IsUniqueConflict` |
[`MuhasebeFisController.cs`](backend/Muhasebe/MuhasebeFisleri/Controllers/MuhasebeFisController.cs:78) | `POST {id}/onayla` endpoint |

### 10. Manuel Test Senaryosu
# | Test | Beklenen |
|---|---|---|
1 | Taslak fiş onayla | 200, Durum=Onayli, YevmiyeNo > 0 |
2 | Aynı tesis/mali yılda ikinci fiş onayla | YevmiyeNo bir artmalı |
3 | Onaylı fişi tekrar onayla | 400 "Yalnızca taslak durumundaki fişler onaylanabilir" |
4 | Onaylı fişi güncelle | 400 "Yalnızca taslak durumundaki fişler güncellenebilir" |
5 | Onaylı fişi sil | 400 "Yalnızca taslak durumundaki fişler silinebilir" |
6 | Açık dönem yokken fiş onayla | 400 "Fiş tarihi için açık muhasebe dönemi bulunamadı" |
7 | Borç/alacak dengesi bozuk fiş onayla | 400 "Toplam borç (...) ile toplam alacak (...) eşit olmalıdır" |
8 | Farklı tesis, aynı mali yıl — sayaç bağımsız | YevmiyeNo 1'den başlar |

## Tur 149: Faz 7 Düzeltme — Onay Validasyonlarını Güçlendirme (2026-05-15)

### 1. Uygulanan Çözümün Özeti

[`OnaylaAsync`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs:101) metodunda daha önce sadece fiş başlığındaki `ToplamBorc`/`ToplamAlacak` karşılaştırılıyordu. Satır toplamları yeniden hesaplanmıyor, satır bazlı borç/alacak kontrolleri (negatif, çift giriş, sıfır) yapılmıyordu. Bu düzeltme ile onay validasyonları güçlendirildi.

### 2. Değiştirilen Dosya

| Dosya | Değişiklik |
|---|---|
| [`MuhasebeFisService.cs`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs:127) | `OnaylaAsync` içinde satır bazlı validasyonlar, satır toplam hesaplama, başlık-satır uyum kontrolü eklendi |

### 3. Eklenen Onay Validasyonları

| # | Kontrol | Hata Mesajı |
|---|---|---|
| 4 | Satır borç/alacak negatif olamaz | "Satır {SiraNo}: borç veya alacak negatif olamaz." |
| 4 | Aynı satırda hem borç hem alacak girilemez | "Satır {SiraNo}: hem borç hem alacak girilemez." |
| 4 | Satırda borç veya alacak girilmelidir | "Satır {SiraNo}: borç veya alacak girilmelidir." |
| 6 | Satır toplam borç = satır toplam alacak | "Satır toplam borç (X) ile satır toplam alacak (Y) eşit olmalıdır." |
| 7 | Satır toplam borç > 0 | "Toplam borç tutarı sıfırdan büyük olmalıdır." |
| 8 | Fiş başlığı toplamları satır toplamlarıyla uyumlu olmalı | "Fiş toplamları satır toplamları ile uyumlu değildir." |

### 4. Backend Build Sonucu

```
0 hata, 6 uyarı (önceden var olan)
```

### 5. Manuel Test Senaryosu

| # | Test | Beklenen |
|---|---|---|
| 1 | Satır toplamları eşit olmayan fiş onayla | 400 "Satır toplam borç (...) ile satır toplam alacak (...) eşit olmalıdır." |
| 2 | Başlık toplamları satır toplamlarından farklı fiş onayla | 400 "Fiş toplamları satır toplamları ile uyumlu değildir." |
| 3 | Aynı satırda hem borç hem alacak varsa onayla | 400 "Satır X: hem borç hem alacak girilemez." |
| 4 | Borç ve alacak ikisi de sıfır olan satır varsa onayla | 400 "Satır X: borç veya alacak girilmelidir." |
| 5 | Negatif borç/alacak olan satır varsa onayla | 400 "Satır X: borç veya alacak negatif olamaz." |
| 6 | Geçerli taslak fiş onayla | 200, Durum=Onayli, YevmiyeNo > 0 |

## Tur 150: Faz 8 — Fiş İptali ve Ters Kayıt Altyapısı (2026-05-18)

### 1. Uygulanan Çözümün Özeti

Onaylı muhasebe fişini iptal etme ve ters kayıt fişi oluşturma altyapısı kuruldu. İptal sırasında orijinal fişin borç/alacak yönleri ters çevrilmiş, tesis+mali yıl bazlı yeni bir yevmiye numarasına sahip ters kayıt fişi oluşturulur. Tüm işlem tek bir transaction içinde atomik olarak gerçekleşir.

### 2. Yeni Dosyalar

| Dosya | Açıklama |
|---|---|
| [`20260518104613_AddMuhasebeFisTersKayitBaglantilari.cs`](backend/Infrastructure/EntityFramework/Migrations/20260518104613_AddMuhasebeFisTersKayitBaglantilari.cs) | `MuhasebeFisler` tablosuna `TersKayitFisId`, `IptalEdilenFisId` nullable FK + index |

### 3. Değiştirilen Dosyalar

| Dosya | Değişiklik |
|---|---|
| [`MuhasebeFis.cs`](backend/Muhasebe/MuhasebeFisleri/Entities/MuhasebeFis.cs:32) | `TersKayitFisId`, `IptalEdilenFisId` + navigation property'ler eklendi |
| [`MuhasebeFisDtos.cs`](backend/Muhasebe/MuhasebeFisleri/Dtos/MuhasebeFisDtos.cs:19) | `TersKayitFisId`, `IptalEdilenFisId` DTO alanları + `MuhasebeFisIptalRequest` eklendi |
| [`StysAppDbContext.cs`](backend/Infrastructure/EntityFramework/StysAppDbContext.cs:1921) | `HasOne/WithMany/HasForeignKey` + `HasIndex` (TersKayitFisId, IptalEdilenFisId) |
| [`IMuhasebeFisService.cs`](backend/Muhasebe/MuhasebeFisleri/Services/IMuhasebeFisService.cs:12) | `IptalEtAsync` imzası |
| [`MuhasebeFisService.cs`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs:203) | `IptalEtAsync` implementasyonu (14 adım) |
| [`MuhasebeFisController.cs`](backend/Muhasebe/MuhasebeFisleri/Controllers/MuhasebeFisController.cs:85) | `POST {id}/iptal` endpoint |

### 4. Entity/DTO Alanları

| Entity | DTO | Açıklama |
|---|---|---|
| `int? TersKayitFisId` | `int? TersKayitFisId` | Orijinal fişin ters kaydını işaret eder |
| `int? IptalEdilenFisId` | `int? IptalEdilenFisId` | Ters kayıt fişinin orijinal fişini işaret eder |
| `MuhasebeFis? TersKayitFis` | — | Navigation property (yalnızca entity) |
| `MuhasebeFis? IptalEdilenFis` | — | Navigation property (yalnızca entity) |

### 5. IptalEtAsync Kuralları (14 Adım)

| # | Kural | Hata (HTTP) |
|---|---|---|
| 1 | Fiş var olmalı, silinmemiş olmalı | 404 |
| 2 | Sadece Onayli fiş iptal edilebilir | 400 |
| 3 | YevmiyeNo dolu olmalı | 400 |
| 4 | Daha önce iptal edilmemiş olmalı (Durum=Iptal veya TersKayitFisId dolu) | 400 |
| 5 | Ters kayıt fişi iptal edilemez (Durum=TersKayit) | 400 |
| 6 | Açık muhasebe dönemi bulunmalı | 400 |
| 7 | En az 2 aktif satır olmalı | 400 |
| 8 | Ters kayıt fişi oluştur (borç↔alacak ters çevrilir) | — |
| 9 | Ters kayıt borç/alacak dengesi kontrolü | 400 |
| 10 | Ters kayıt satır hesapları doğrulama | 400 |
| 11 | Ters kayıt için yeni YevmiyeNo üret | — |
| 12 | Ters kayıt fişini kaydet | — |
| 13 | Orijinal fişi Iptal yap, TersKayitFisId ata | — |
| 14 | Herhangi bir hata → full rollback | — |

### 6. Ters Kayıt Oluşturma Mantığı

- **FisNo**: `"TERS-" + orijinal.FisNo`
- **FisTipi**: `MuhasebeFisTipleri.Duzeltme`
- **Durum**: `MuhasebeFisDurumlari.TersKayit`
- **ToplamBorc**: orijinalin ToplamAlacak değeri
- **ToplamAlacak**: orijinalin ToplamBorc değeri
- **Satır Borc**: orijinal satırın Alacak değeri
- **Satır Alacak**: orijinal satırın Borc değeri
- **Diğer alanlar** (MuhasebeHesapPlaniId, CariKartId, vb.): aynen korunur
- **Aciklama**: istekten gelen değer veya `"Fiş iptal ters kaydı: {orijinalFisNo}"`

### 7. Controller Endpoint

```
POST /ui/muhasebe/fisler/{id}/iptal
Permission: MuhasebeFisYonetimi.Manage
Body: { "aciklama": "..." } (opsiyonel)
Response: İptal edilmiş MuhasebeFisDto (Durum=Iptal, TersKayitFisId dolu)
```

### 8. Backend Build Sonucu

```
0 hata, 6 uyarı (önceden var olan)
```

### 9. Migration

```
20260518104613_AddMuhasebeFisTersKayitBaglantilari
- MuhasebeFisler tablosuna TersKayitFisId (nullable int, FK self-reference, Restrict)
- MuhasebeFisler tablosuna IptalEdilenFisId (nullable int, FK self-reference, Restrict)
- Index: IX_MuhasebeFisler_TersKayitFisId
- Index: IX_MuhasebeFisler_IptalEdilenFisId
```

### 10. Manuel Test Senaryosu

| # | Test | Beklenen |
|---|---|---|
| 1 | Onaylı fişi iptal et | 200, orijinal Durum=Iptal, TersKayitFisId dolu |
| 2 | Ters kayıt fişi oluştu mu? | Ters kayıt Durum=TersKayit, kendi YevmiyeNo'su var |
| 3 | Ters kayıt IptalEdilenFisId doğru mu? | Orijinal fiş Id'sini göstermeli |
| 4 | Taslak fişi iptal etmeye çalış | 400 "Yalnızca onaylı durumdaki fişler iptal edilebilir." |
| 5 | İptal edilmiş fişi tekrar iptal et | 400 "Fiş zaten iptal edilmiş." |
| 6 | Ters kayıt fişini iptal etmeye çalış | 400 "Ters kayıt fişi iptal edilemez." |
| 7 | Kapalı dönemdeki fişi iptal et | 400 "Fiş tarihi için açık muhasebe dönemi bulunamadı." |
| 8 | Ters kayıt satırlarında borç/alacak doğru mu? | Ters Borc = Orijinal Alacak, Ters Alacak = Orijinal Borc |
| 9 | Ters kayıt borç/alacak dengesi doğru mu? | ToplamBorc = ToplamAlacak, > 0 |
| 10 | Onaylı fişi update/delete et | 400 (zaten Taslak olmadığı için) |

## Tur 151: Faz 8 Düzeltme — İptal Kontrol Sırası ve Akış Sadeleştirmesi (2026-05-18)

### 1. Değiştirilen Dosya

- [`backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs) — `IptalEtAsync` metodu

### 2. Durum Kontrol Sırası Düzeltmesi

**Eski sıra:**
1. `Durum != Onayli` → 400 ← diğer kontrolleri ulaşılamaz yapar
2. `Durum == Iptal` → 400
3. `Durum == TersKayit` → 400

**Yeni sıra:**
1. `Durum == Iptal` → 400 "Fiş zaten iptal edilmiş."
2. `Durum == TersKayit` → 400 "Ters kayıt fişi iptal edilemez."
3. `Durum != Onayli` → 400 "Yalnızca onaylı durumdaki fişler iptal edilebilir."
4. `TersKayitFisId.HasValue` → 400 (Durum `Onayli` ama FK doluysa yine hata)

Bu sayede tüm kontroller ulaşılabilir ve anlamlı hata mesajları döner.

### 3. Orijinal Fiş İptal Akışı Sadeleştirmesi

**Eski akış (3 SaveChanges):**
```
AddAsync(tersFis)
orijinal.Durum = Iptal; orijinal.TersKayitFisId = default
IsModified = true
SaveChanges       ← 1
orijinal.TersKayitFisId = tersFis.Id
SaveChanges       ← 2
```

**Yeni akış (2 SaveChanges):**
```
AddAsync(tersFis)
SaveChanges       ← 1 (tersFis.Id üretilir)
orijinal.Durum = Iptal; orijinal.TersKayitFisId = tersFis.Id
SaveChanges       ← 2 (ikisi birden)
```

Kaldırılan:
- `orijinalFis.TersKayitFisId = default`
- `_dbContext.Entry(orijinalFis).Property(x => x.Durum).IsModified = true`
- Ara `SaveChangesAsync` (gerek yok, tersFis'in Id'si ilk SaveChanges'tan sonra EF tarafından doldurulur)

### 4. Yorum Numaralandırması

IptalEtAsync içindeki adım yorumları 1-14 olarak yeniden numaralandı:
1. Fişi getir, 2. Iptal kontrolü, 3. TersKayit kontrolü, 4. Onayli kontrolü, 5. TersKayitFisId, 6. YevmiyeNo, 7. Açık dönem, 8. Satır sayısı, 9. Ters fiş oluştur, 10. Denge, 11. Hesap doğrula, 12. Yevmiye no üret, 13. Ters fişi kaydet, 14. Orijinali iptal et.

### 5. Build Sonucu

```
0 hata, 6 uyarı (önceden var olan)
```

### 6. Manuel Test Senaryosu

| # | Test | Beklenen |
|---|---|---|
| 1 | İptal edilmiş fişi tekrar iptal et | 400 "Fiş zaten iptal edilmiş." |
| 2 | Ters kayıt fişini iptal etmeye çalış | 400 "Ters kayıt fişi iptal edilemez." |
| 3 | Taslak fişi iptal etmeye çalış | 400 "Yalnızca onaylı durumdaki fişler iptal edilebilir." |
| 4 | Onaylı fişi iptal et | 200, Durum=Iptal, TersKayitFisId=dolu, ters kayıt oluşmuş |
| 5 | Ters kayıt fişi ayrı YevmiyeNo aldı mı? | Orijinalden farklı bir YevmiyeNo |

## Tur 152: Faz 9 — Fiş Listeleme/Filtreleme ve Yevmiye Defteri (2026-05-18)

### 1. Eklenen DTO'lar

**Dosya:** [`backend/Muhasebe/MuhasebeFisleri/Dtos/MuhasebeFisDtos.cs`](backend/Muhasebe/MuhasebeFisleri/Dtos/MuhasebeFisDtos.cs)

| DTO | Açıklama |
|---|---|
| `MuhasebeFisFilterDto` | 14 filtre alanı + Page/PageSize + `Normalize()` metodu |
| `YevmiyeDefteriSatirDto` | 17 alan: fiş bilgisi + hesap kodu/adı + borç/alacak |
| `YevmiyeDefteriDto` | Satır listesi + ToplamBorc + ToplamAlacak |

**Filtre alanları:** TesisId, MaliYil, Donem, BaslangicTarihi, BitisTarihi, FisTipi, Durum, KaynakModul, KaynakId, YevmiyeNoBaslangic, YevmiyeNoBitis, FisNo (Contains), Aciklama (Contains), Page, PageSize.

**Normalize kuralları:** Page < 1 → 1, PageSize < 1 → 50, PageSize > 500 → 500.

### 2. Değiştirilen Repository Metotları

**Dosya:** [`backend/Muhasebe/MuhasebeFisleri/Repositories/IMuhasebeFisRepository.cs`](backend/Muhasebe/MuhasebeFisleri/Repositories/IMuhasebeFisRepository.cs)

3 yeni metot eklendi:
- `Task<List<MuhasebeFis>> GetFilteredAsync(MuhasebeFisFilterDto filter, ...)`
- `Task<int> CountFilteredAsync(MuhasebeFisFilterDto filter, ...)`
- `Task<List<MuhasebeFis>> GetYevmiyeDefteriAsync(MuhasebeFisFilterDto filter, ...)`

**Dosya:** [`backend/Muhasebe/MuhasebeFisleri/Repositories/MuhasebeFisRepository.cs`](backend/Muhasebe/MuhasebeFisleri/Repositories/MuhasebeFisRepository.cs)

- `ApplyFilter()` — ortak filtreleme mantığı (private): IsDeleted=false + tüm filtre alanları (eşitlik, aralık, Contains)
- `GetFilteredAsync` — Include Satırlar+HesapPlani, OrderByDescending FisTarihi → Id, Skip/Take
- `CountFilteredAsync` — ApplyFilter + CountAsync
- `GetYevmiyeDefteriAsync` — Include Satırlar+HesapPlani, default Durum filtresi (Onayli, TersKayit), OrderBy FisTarihi → YevmiyeNo → Id
- Tüm sorgularda `AsNoTracking()` kullanıldı

### 3. Değiştirilen Service Metotları

**Dosya:** [`backend/Muhasebe/MuhasebeFisleri/Services/IMuhasebeFisService.cs`](backend/Muhasebe/MuhasebeFisleri/Services/IMuhasebeFisService.cs)

3 yeni metot eklendi (interface).

**Dosya:** [`backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs)

- `GetFilteredAsync` — filter.Normalize() → repository → AutoMapper.Map
- `CountFilteredAsync` — filter.Normalize() → repository
- `GetYevmiyeDefteriAsync` — filter.Normalize() → repository → flatten satırlar (fiş bilgisi + hesap Kodu/Adı + talep bilgisi) → ToplamBorc/ToplamAlacak hesapla → YevmiyeDefteriDto döndür

AutoMapper mevcut `MuhasebeFisProfile` zaten `MuhasebeHesapKodu`/`MuhasebeHesapAdi` mapping'ini yapıyordu, değişiklik gerekmedi.

### 4. Controller Endpointleri

**Dosya:** [`backend/Muhasebe/MuhasebeFisleri/Controllers/MuhasebeFisController.cs`](backend/Muhasebe/MuhasebeFisleri/Controllers/MuhasebeFisController.cs)

| Method | Route | Permission | Response |
|---|---|---|---|
| POST | `ui/muhasebe/fisler/filter` | View | `List<MuhasebeFisDto>` |
| POST | `ui/muhasebe/fisler/filter/count` | View | `int` |
| POST | `ui/muhasebe/fisler/yevmiye-defteri` | View | `YevmiyeDefteriDto` |

Body: `MuhasebeFisFilterDto` (JSON)

### 5. Yevmiye Defteri Default Durum Filtresi

- Kullanıcı `Durum` göndermezse: `WHERE Durum IN ('Onayli', 'TersKayit')`
- Kullanıcı `Durum` gönderirse: gönderilen değere göre eşitlik
- `Iptal` durumundaki orijinal fiş default olarak yevmiye defterine dahil edilmez
- Ters kayıt fişleri (Durum=TersKayit) default olarak dahil edilir

### 6. Build Sonucu

```
0 hata, 6 uyarı (önceden var olan)
```

### 7. Manuel Test Senaryosu

| # | Test | Beklenen |
|---|---|---|
| 1 | TesisId + MaliYil + Donem filtresiyle fiş listesi | Filtreyle eşleşen fişler dönmeli |
| 2 | Tarih aralığı filtresi | BaslangicTarihi - BitisTarihi aralığındaki fişler |
| 3 | Durum=Taslak filtresi | Sadece Taslak durumundaki fişler |
| 4 | KaynakModul + KaynakId filtresi | Belirtilen kaynaktan oluşan fişler |
| 5 | YevmiyeNo aralığı filtresi | Belirtilen yevmiye no aralığı |
| 6 | Page/PageSize | Sayfalama düzgün çalışmalı |
| 7 | Yevmiye defteri (boş Durum) | Sadece Onayli + TersKayit fişleri gelmeli |
| 8 | Yevmiye defteri (boş Durum) | Iptal durumundaki orijinal fiş gelmemeli |
| 9 | Yevmiye defteri satırları | MuhasebeHesapKodu ve MuhasebeHesapAdi dolu gelmeli |
| 10 | Yevmiye defteri toplamları | ToplamBorc ve ToplamAlacak doğru hesaplanmalı |

---

## Tur 153 — Faz 9 Düzeltme: ApplyFilter'a Durum filtresi eklendi (2026-05-18)

### Problem
`MuhasebeFisFilterDto` içinde `Durum` alanı olmasına rağmen `ApplyFilter` metodunda `Durum` filtresi uygulanmıyordu. `POST /ui/muhasebe/fisler/filter` ve `POST /ui/muhasebe/fisler/filter/count` endpointlerine `Durum="Taslak"` gönderildiğinde filtre çalışmıyor, tüm durumdaki fişler dönüyordu.

### Yapılan
- `MuhasebeFisRepository.ApplyFilter` metoduna `FisTipi` filtresinden sonra `Durum` filtresi eklendi:
  ```csharp
  if (!string.IsNullOrWhiteSpace(filter.Durum))
      query = query.Where(x => x.Durum == filter.Durum);
  ```
- `GetYevmiyeDefteriAsync` içindeki özel Durum mantığı (varsayılan: `Onayli` + `TersKayit`) korundu — `ApplyFilter` içindeki yeni Durum filtresi `string.IsNullOrWhiteSpace` kontrolü yaptığı için boş Durum durumunda devreye girmez, dolu Durum durumunda ise iki filtre aynı koşulu ekler (çakışma yok, zararsız).

### Değişen Dosyalar
| # | Dosya | Değişiklik |
|---|-------|------------|
| 1 | `backend/Muhasebe/MuhasebeFisleri/Repositories/MuhasebeFisRepository.cs` | `ApplyFilter` metoduna `Durum` filtresi eklendi (`FisTipi` ile `KaynakModul` arasına) |

### Build
- **Backend:** ✅ 0 errors, 1 warning (önceden var olan NuGet uyarısı)

### Test
| # | Test | Beklenen |
|---|---|---|
| 1 | filter endpoint'ine Durum=Taslak gönder | Sadece Taslak fişler dönmeli |
| 2 | filter endpoint'ine Durum=Onayli gönder | Sadece Onayli fişler dönmeli |
| 3 | filter endpoint'ine Durum boş/null gönder | Tüm durumdaki fişler dönmeli (filtre uygulanmamalı) |
| 4 | yevmiye-defteri endpoint'ine boş Durum | Öncekiyle aynı: Onayli + TersKayit (özel mantık korunuyor) |

---

## Tur 154 — Faz 10: Muavin Defter (2026-05-18)

### Yapılan
Muavin defter endpoint'i eklendi. Belirli bir muhasebe hesabının hareketlerini tarih aralığında listeleyip yürüyen bakiye hesaplar.

### Eklenen DTO'lar
| DTO | Açıklama |
|-----|----------|
| `MuavinDefterFilterDto` | TesisId, MuhasebeHesapPlaniId, tarih aralığı, MaliYil, Donem, AltHesaplariDahilEt, Page/PageSize |
| `MuavinDefterSatirDto` | Fiş bilgileri + Hesap bilgileri + Borc/Alacak + Bakiye/BakiyeTipi + Açıklamalar |
| `MuavinDefterDto` | Hesap bilgileri + ToplamBorc/ToplamAlacak/Bakiye/BakiyeTipi + Satirlar |

### Repository
- `IMuhasebeFisRepository.GetMuavinDefterAsync(filter, hesapKoduPrefix, cancellationToken)` eklendi
- `MuhasebeFisRepository.GetMuavinDefterAsync`: Sadece Onayli ve TersKayit fişlerini getirir. TesisId zorunlu. MaliYil, Donem, BaslangicTarihi, BitisTarihi opsiyonel filtreler. Satırlar ve hesap planı include edilir. FisTarihi → YevmiyeNo → Id sıralaması.

### Service
- `IMuhasebeFisService.GetMuavinDefterAsync(filter, cancellationToken)` eklendi
- `MuhasebeFisService.GetMuavinDefterAsync`:
  1. Normalize + validasyon (TesisId, MuhasebeHesapPlaniId, tarih sırası)
  2. Seçilen hesabı bul (AktifMi=true, IsDeleted=false) → bulunamazsa 404
  3. Repository'den fişleri çek
  4. Satırları flatten et + hesap filtresi uygula:
     - `AltHesaplariDahilEt=false` → sadece `satir.MuhasebeHesapPlaniId == filter.MuhasebeHesapPlaniId`
     - `AltHesaplariDahilEt=true` → `satir.MuhasebeHesapPlani.TamKod.StartsWith(hesapKoduPrefix)`
  5. Sırala: FisTarihi → YevmiyeNo → FisId → SiraNo
  6. Yürüyen bakiye hesapla (tüm satırlar üzerinden): bakiye += Borc - Alacak, BakiyeTipi = Borc/Alacak/Sifir
  7. Toplamlar (tüm filtrelenmiş satırlar): ToplamBorc, ToplamAlacak, net bakiye
  8. Sayfalama (sadece istenen sayfadaki satırlar)
  9. DTO'yu döndür

### Controller
- `POST ui/muhasebe/fisler/muavin-defter` endpoint'i
- Body: `MuavinDefterFilterDto`
- Response: `MuavinDefterDto`
- Permission: `MuhasebeFisYonetimi.View`

### Durum Kuralları
| Durum | Muavinde Görünür? |
|-------|-------------------|
| Onayli | ✅ Evet |
| TersKayit | ✅ Evet |
| Taslak | ❌ Hayır |
| Iptal | ❌ Hayır |

### Değişen Dosyalar
| # | Dosya | Değişiklik |
|---|-------|------------|
| 1 | `backend/Muhasebe/MuhasebeFisleri/Dtos/MuhasebeFisDtos.cs` | `MuavinDefterFilterDto`, `MuavinDefterSatirDto`, `MuavinDefterDto` eklendi |
| 2 | `backend/Muhasebe/MuhasebeFisleri/Repositories/IMuhasebeFisRepository.cs` | `GetMuavinDefterAsync` imzası eklendi |
| 3 | `backend/Muhasebe/MuhasebeFisleri/Repositories/MuhasebeFisRepository.cs` | `GetMuavinDefterAsync` implementasyonu |
| 4 | `backend/Muhasebe/MuhasebeFisleri/Services/IMuhasebeFisService.cs` | `GetMuavinDefterAsync` imzası eklendi |
| 5 | `backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs` | `GetMuavinDefterAsync` implementasyonu (flatten + filtre + bakiye + sayfalama) |
| 6 | `backend/Muhasebe/MuhasebeFisleri/Controllers/MuhasebeFisController.cs` | `POST muavin-defter` endpoint'i |

### Build
- **Backend:** ✅ 0 errors, 5 warnings (tümü önceden var olan uyarılar)

### Manuel Test
| # | Test | Beklenen |
|---|---|---|
| 1 | Belirli bir hesap için muavin defter | Sadece o hesabın hareketleri |
| 2 | Durum kontrolü | Sadece Onayli + TersKayit fiş satırları |
| 3 | Taslak fişler | Görünmemeli |
| 4 | Iptal fişler | Görünmemeli |
| 5 | AltHesaplariDahilEt=false | Sadece seçilen hesap |
| 6 | AltHesaplariDahilEt=true | Alt hesaplar da dahil |
| 7 | Toplamlar | Tüm filtrelenmiş satırları kapsamalı |
| 8 | BakiyeTipi | Borc/Alacak/Sifir doğru |
| 9 | Page/PageSize | Satır bazlı sayfalama |
| 10 | Yürüyen bakiye | Her satırda kümülatif bakiye doğru |

---

## Tur 155 — Faz 11: Mizan (2026-05-18)

### Yapılan
Mizan endpoint'i eklendi. Belirli bir tesisin hesap planındaki hesapların borç/alacak toplamlarını ve bakiyelerini hesap bazında gruplayarak listeler.

### Eklenen DTO'lar
| DTO | Açıklama |
|-----|----------|
| `MizanFilterDto` | TesisId, tarih aralığı, MaliYil, Donem, SadeceHareketGorenHesaplar, AltHesaplariDahilEt, HesapKoduBaslangic/Bitis, Page/PageSize |
| `MizanSatirDto` | Hesap bilgileri + ToplamBorc/Alacak + BorcBakiye/AlacakBakiye + Bakiye/BakiyeTipi |
| `MizanDto` | GenelToplamBorc/Alacak + GenelBorcBakiye/AlacakBakiye + Satirlar |

### Repository
- `IMuhasebeFisRepository.GetMizanFisleriAsync(filter, cancellationToken)` eklendi
- `MuhasebeFisRepository.GetMizanFisleriAsync`: Sadece Onayli ve TersKayit fişlerini getirir. TesisId zorunlu. MaliYil, Donem, tarih aralığı opsiyonel. Satırlar + hesap planı include/ThenInclude. AsNoTracking. Sayfalama yok (service tarafında uygulanır).

### Service
- `IMuhasebeFisService.GetMizanAsync(filter, cancellationToken)` eklendi
- `MuhasebeFisService.GetMizanAsync`:
  1. Normalize + validasyon (TesisId > 0, tarih sırası, MaliYil 2000-2100, Donem 1-12)
  2. Repository'den fişleri çek
  3. Satırları flatten et:
     - Sadece `satir.IsDeleted=false`, `hesap != null`, `hesap.IsDeleted=false`, `hesap.AktifMi=true`
     - HesapKoduBaslangic/Bitis string karşılaştırma filtresi
  4. Hesap bazında `GroupBy` (Id, TamKod, Ad, DetayHesapMi, HareketGorebilirMi):
     - ToplamBorc = Sum(Borc), ToplamAlacak = Sum(Alacak)
     - Net = ToplamBorc - ToplamAlacak
     - BorcBakiye = Net > 0 ? Net : 0
     - AlacakBakiye = Net < 0 ? Abs(Net) : 0
     - Bakiye = Abs(Net), BakiyeTipi = Borc/Alacak/Sifir
  5. `SadeceHareketGorenHesaplar=true` → ToplamBorc != 0 veya ToplamAlacak != 0 olanlar
  6. `AltHesaplariDahilEt` bu fazda konsolidasyon yapmaz; gelecek faza bırakıldı
  7. Sırala: HesapKodu ascending (Ordinal)
  8. Genel toplamlar (sayfalama öncesi, tüm satırlar)
  9. Sayfalama (hesap satırı bazlı)

### Controller
- `POST ui/muhasebe/fisler/mizan` endpoint'i
- Body: `MizanFilterDto`
- Response: `MizanDto`
- Permission: `MuhasebeFisYonetimi.View`

### Bakiye Hesaplama
| Alan | Formül |
|------|--------|
| Net | ToplamBorc - ToplamAlacak |
| BorcBakiye | Net > 0 ? Net : 0 |
| AlacakBakiye | Net < 0 ? Abs(Net) : 0 |
| Bakiye | Abs(Net) |
| BakiyeTipi | Net > 0 → "Borc", Net < 0 → "Alacak", Net == 0 → "Sifir" |

### Durum Kuralları
| Durum | Mizanda Görünür? |
|-------|------------------|
| Onayli | ✅ Evet |
| TersKayit | ✅ Evet |
| Taslak | ❌ Hayır |
| Iptal | ❌ Hayır |

### Değişen Dosyalar
| # | Dosya | Değişiklik |
|---|-------|------------|
| 1 | `backend/Muhasebe/MuhasebeFisleri/Dtos/MuhasebeFisDtos.cs` | `MizanFilterDto`, `MizanSatirDto`, `MizanDto` eklendi |
| 2 | `backend/Muhasebe/MuhasebeFisleri/Repositories/IMuhasebeFisRepository.cs` | `GetMizanFisleriAsync` imzası eklendi |
| 3 | `backend/Muhasebe/MuhasebeFisleri/Repositories/MuhasebeFisRepository.cs` | `GetMizanFisleriAsync` implementasyonu |
| 4 | `backend/Muhasebe/MuhasebeFisleri/Services/IMuhasebeFisService.cs` | `GetMizanAsync` imzası eklendi |
| 5 | `backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs` | `GetMizanAsync` implementasyonu (flatten + group + bakiye + sayfalama) |
| 6 | `backend/Muhasebe/MuhasebeFisleri/Controllers/MuhasebeFisController.cs` | `POST mizan` endpoint'i |

### Build
- **Backend:** ✅ 0 errors, 5 warnings (tümü önceden var olan uyarılar)

### Manuel Test
| # | Test | Beklenen |
|---|---|---|
| 1 | Belirli tesis için mizan | Hesaplar listelenmeli |
| 2 | Durum kontrolü | Sadece Onayli + TersKayit fişler |
| 3 | Taslak fişler | Görünmemeli |
| 4 | Iptal fişler | Görünmemeli |
| 5 | Hesap bazında gruplama | ToplamBorc ve ToplamAlacak doğru |
| 6 | BorcBakiye / AlacakBakiye | Doğru hesaplanmalı |
| 7 | GenelToplamBorc / GenelToplamAlacak | Dönmeli |
| 8 | Tarih aralığı filtresi | Çalışmalı |
| 9 | HesapKoduBaslangic/Bitis | Çalışmalı |
| 10 | Page/PageSize | Hesap satırı bazlı çalışmalı |
| 11 | Toplamlar | Tüm filtrelenmiş satırları kapsamalı |

---

## Faz 12 - Mizan'a Üst Hesap Konsolidasyonu Desteği (2026-05-18)

### Amaç
Mizan raporunda `AltHesaplariDahilEt=true` iken alt hesapların hareket toplamlarının üst hesaplara konsolide edilmesi.

### Yapılan Değişiklikler

#### 1. `MizanSatirDto` — Yeni Alanlar
| Alan | Tip | Açıklama |
|------|-----|----------|
| `KonsolideSatirMi` | bool | Alt hesapların konsolidasyonu ile oluşan üst hesap satırı ise true |
| `Seviye` | int | Hesap kodundaki segment sayısı (örn: 150 → 1, 150.01 → 2) |

#### 2. `GetUstHesapKodlari(string tamKod)` — Yeni Private Helper
- Nokta ile ayrılmış tam hesap kodundan üst hesap kodlarını türetir
- `"150.01.001"` → `["150", "150.01"]`
- `"150"` → `[]`

#### 3. `GetMizanAsync` — Konsolidasyon Mantığı
- **Seviye**: Her satır için hesap kodu segment sayısından hesaplanır
- **AltHesaplariDahilEt=true**: Üst hesap kodları türetilir → DB'den sorgulanır → alt toplamlar konsolide edilir → `KonsolideSatirMi=true`
- **Genel toplamlar**: Sadece `KonsolideSatirMi=false` satırlardan (çift sayım önlenir)
- **AltHesaplariDahilEt=false**: Faz 11 davranışı korunur

### Örnek
Hareketler:
| Hesap | Borç | Alacak |
|-------|------|--------|
| 150.01 | 100 | 0 |
| 150.02 | 200 | 50 |

Sonuç:
| Hesap | Borç | Alacak | KonsolideSatirMi | Seviye |
|-------|------|--------|------------------|--------|
| 150 | 300 | 50 | true | 1 |
| 150.01 | 100 | 0 | false | 2 |
| 150.02 | 200 | 50 | false | 2 |

### Değişen Dosyalar
| # | Dosya | Değişiklik |
|---|-------|------------|
| 1 | backend/Muhasebe/MuhasebeFisleri/Dtos/MuhasebeFisDtos.cs | MizanSatirDto'ya KonsolideSatirMi ve Seviye alanları eklendi |
| 2 | backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs | GetMizanAsync konsolidasyon + GetUstHesapKodlari helper |

### Build
- **Backend:** ✅ 0 errors, 5 warnings (tümü önceden var olan uyarılar)

### Manuel Test
| # | Test | Beklenen |
|---|---|---|
| 1 | AltHesaplariDahilEt=true | Üst hesaplar konsolide toplamlarla listelenmeli |
| 2 | AltHesaplariDahilEt=false | Sadece doğrudan hareket gören hesaplar (Faz 11) |
| 3 | KonsolideSatirMi | Konsolide satırlarda true, gerçek satırlarda false |
| 4 | Seviye | 150→1, 150.01→2, 150.01.001→3 |
| 5 | Genel toplamlar | Sadece gerçek hareket satırlarından, çift sayım yok |
| 6 | SadeceHareketGorenHesaplar=true | Sıfır bakiyeli konsolide hesaplar listelenmemeli |
| 7 | Üst hesabın kendi hareketi | Konsolidasyon kendi hareketini KAPSAMAZ (ayrı satır) |
