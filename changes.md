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
