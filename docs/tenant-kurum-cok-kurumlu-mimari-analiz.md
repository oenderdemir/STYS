# STYS Cok Kurumlu Yapi Analizi ve Tenant Gecis Plani

## Amac

Bu dokuman, STYS projesinde "Kurum = Tenant" modeline gecis icin mevcut mimariyi incelemek ve ilk uygulanabilir faz icin net bir yol haritasi cikarmak amaciyla hazirlanmistir.

Bu fazda kod degisikligi hedeflenmemistir. Odak, mevcut yapinin tenant-aware hale nasil getirilecegini en az kirici yolla tanimlamaktir.

## Temel Kararlar

- Kurum, tenant karsiligidir.
- Tesis tenant degildir; Kurum altinda yer alir.
- Bir kurumun birden fazla tesisi olabilir.
- Bir kullanici birden fazla kuruma bagli olabilir.
- Aktif `KurumId` JWT claim icinde tasinmalidir.
- Request body icindeki `KurumId` guvenilir kabul edilmemelidir.
- Tenant'a bagli tum kayitlarin `KurumId` degeri backend tarafinda current tenant context uzerinden set edilmelidir.
- Tenant izolasyonu EF Core global query filter ile saglanmalidir.
- `Country`, `Il`, global roller/yetkiler ve `MenuItem` gibi sistem tablolar tenant bagimsiz kalmalidir.
- Muhasebe, rezervasyon, kamp ve restoran modulleri kapsam dahilindedir; ancak tenant foundation oturduktan sonra ayrik fazlarda tenant-aware hale getirilmelidir.

## 1. Mevcut Mimari Ozeti

### 1.1 Veri katmani

- [BaseEntity.cs](/c:/Users/cuce/source/repos/STYS/platform/TOD.Platform.Persistence.Rdbms/Entities/BaseEntity.cs) tum ana entity'lerde `IsDeleted` ve audit alanlarini sagliyor.
- [StysAppDbContext.cs](/c:/Users/cuce/source/repos/STYS/backend/Infrastructure/EntityFramework/StysAppDbContext.cs) icinde tum STYS domain entity'leri tek `DbContext` altinda toplaniyor.
- Su anki tek global filter `IsDeleted = false` uzerine kurulu. Tenant filter yok.
- Bircok domain tablosu zaten `TesisId` tasiyor. Bu, tenant gecisi icin avantajli ama eksik bir ara katman.

### 1.2 Identity ve yetki modeli

- [User.cs](/c:/Users/cuce/source/repos/STYS/platform/TOD.Platform.Identity/Users/Entities/User.cs), [UserGroup.cs](/c:/Users/cuce/source/repos/STYS/platform/TOD.Platform.Identity/UserGroups/Entities/UserGroup.cs), [UserUserGroup.cs](/c:/Users/cuce/source/repos/STYS/platform/TOD.Platform.Identity/UserUserGroups/Entities/UserUserGroup.cs), [UserGroupRole.cs](/c:/Users/cuce/source/repos/STYS/platform/TOD.Platform.Identity/UserGroupRoles/Entities/UserGroupRole.cs), [Role.cs](/c:/Users/cuce/source/repos/STYS/platform/TOD.Platform.Identity/Roles/Entities/Role.cs) zinciri mevcut authorization modelini kuruyor.
- Yetkiler global `Domain.Name` formatinda calisiyor.
- `UserGroup` ve `Role` kavramlari tenant bagimsiz.
- `UserService` ve `StysScopedUserService` uzerinden grup atama ve scoped kullanici yonetimi yurutuluyor.

### 1.3 JWT ve auth modeli

- [JwtTokenService.cs](/c:/Users/cuce/source/repos/STYS/platform/TOD.Platform.Security/Auth/Services/JwtTokenService.cs) bugun token icine kullanici kimligini ve `tokenVersion` bilgisini koyuyor.
- [AuthenticationService.cs](/c:/Users/cuce/source/repos/STYS/platform/TOD.Platform.Security/Auth/Services/AuthenticationService.cs) login/refresh akisini yonetiyor.
- [TodPlatformJwtAuthenticationExtensions.cs](/c:/Users/cuce/source/repos/STYS/platform/TOD.Platform.AspNetCore/Authorization/TodPlatformJwtAuthenticationExtensions.cs) token validate olduktan sonra permission claim'lerini DB'den principal'a geri yukluyor.
- Aktif tenant veya aktif kurum claim'i su an bulunmuyor.

### 1.4 Scope modeli

- [AccessScopeProvider.cs](/c:/Users/cuce/source/repos/STYS/backend/AccessScope/AccessScopeProvider.cs) mevcut veri erisim scope'unu hesapliyor.
- [DomainAccessScope.cs](/c:/Users/cuce/source/repos/STYS/backend/AccessScope/DomainAccessScope.cs) `IlIds`, `TesisIds`, `BinaIds` tasiyor.
- [UserActorScope.cs](/c:/Users/cuce/source/repos/STYS/backend/AccessScope/UserActorScope.cs) kullanici yonetimi actor-scope kurallari icin kullaniliyor.
- Bu yapi bugun tesis bazli calisiyor; tenant kavrami ust scope olarak eklenebilir.

### 1.5 Frontend durumu

- [auth.service.ts](/c:/Users/cuce/source/repos/STYS/frontend/src/app/pages/auth/auth.service.ts) token, permission ve session bilgisini localStorage'da tutuyor.
- [app.topbar.ts](/c:/Users/cuce/source/repos/STYS/frontend/src/app/layout/component/app.topbar.ts) aktif kullanici ve session aksiyonlarini gosteriyor.
- [muhasebe-tesis-context.service.ts](/c:/Users/cuce/source/repos/STYS/frontend/src/app/pages/muhasebe/services/muhasebe-tesis-context.service.ts) tesis secimini localStorage tabanli olarak yonetiyor.
- Frontend bugun "aktif tesis" mantigina hazir; benzer bir "aktif kurum" konteksi ust seviyede eklenebilir.

## 2. Kurum/Tenant Icin Uygun Katman Yerlesimi

### Backend domain

Kurum domain'i backend altinda olmalidir:

- `backend/Kurumlar/Entities/Kurum.cs`
- `backend/Kurumlar/Dto/KurumDto.cs`
- `backend/Kurumlar/Dto/CreateKurumRequest.cs`
- `backend/Kurumlar/Dto/UpdateKurumRequest.cs`
- `backend/Kurumlar/Repositories/IKurumRepository.cs`
- `backend/Kurumlar/Repositories/KurumRepository.cs`
- `backend/Kurumlar/Services/IKurumService.cs`
- `backend/Kurumlar/Services/KurumService.cs`
- `backend/Kurumlar/Controllers/KurumController.cs`

### Platform persistence/security

Tenant altyapisi platform altinda kalmalidir:

- `platform/TOD.Platform.Persistence.Rdbms/Entities/ITenantEntity.cs`
- `platform/TOD.Platform.Security/Auth/Services/ICurrentTenantAccessor.cs`
- `platform/TOD.Platform.Security/Auth/Services/HttpContextCurrentTenantAccessor.cs`
- tenant claim/constant yardimci dosyalari

### Platform identity

Kullanici-kurum iliskisi identity tarafinda tutulmalidir:

- `platform/TOD.Platform.Identity/UserKurums/Entities/UserKurum.cs`
- iliskili DTO/repository/service/controller dosyalari

### Bagimlilik siniri

- backend, `Kurum` entity'sini bilir.
- platform persistence yalniz tenant soyutlamasini bilir.
- platform security claim ve current tenant accessor seviyesinde kalir.
- platform identity `UserKurum` icinde sadece `KurumId` scalar alanini tutar.
- platform identity, `STYS.Kurumlar.Entities.Kurum` tipine navigation baglamaz.

## 3. KurumAdmin Modeli Nasil Kurulmali

## Onerilen ilk model

Ilk faz icin en az kirici cozum:

```csharp
public class UserKurum : BaseEntity<Guid>
{
    public Guid UserId { get; set; }
    public int KurumId { get; set; }
    public bool VarsayilanMi { get; set; }
    public bool AktifMi { get; set; } = true;
    public bool IsKurumAdmin { get; set; }
}
```

### Neden yalnizca Role yeterli degil

- Kullanici bir kurumda admin, diger kurumda normal olabilir.
- Global `Role` bu farki tek basina temsil edemez.
- `KullaniciTipi.KurumAdmin` gibi global bir role, kurum bazli ayrimi tasiyamaz.

### Neden `UserKurum.IsKurumAdmin` daha uygun

- Mevcut permission modelini bozmadan kurum bazli yonetici bilgisi tasir.
- JWT uretiminde aktif kurum icin `isKurumAdmin` hesaplanabilir.
- Backend tarafinda "aktif kurumda admin mi" kontrolu kolaylasir.

### Uzun vadeli model

Ilk fazdan sonra kurum bazli farkli grup uyelikleri ihtiyaci dogarsa:

- `UserUserGroup` uzerine `KurumId` eklemek yerine
- `UserKurumGroup` veya benzeri ayri bir baglanti modeli daha dogru olur.

Cunku bugunku `UserUserGroup` yapisi global group semantigiyle calisiyor.

## 4. SuperAdmin / KurumAdmin / Normal Kullanici Yetki Ayrimi

### SuperAdmin

- Tum kurumlari gorebilir.
- Tum kurumlarda tenant filter bypass yapabilir.
- Kurum CRUD, global menu, global rol, global group yonetimi yapabilir.
- Mevcut `KullaniciTipi.Admin` mantigi buyuk olcude bu role icin yeniden kullanilabilir.

### KurumAdmin

- Sadece aktif kurum veya yetkili oldugu kurumlar icinde islem yapar.
- Kendi kurumunun tesislerini, kullanicilarini ve is domain kayitlarini yonetir.
- ID bilinse bile baska kurum verisi okuyamaz.
- Body'den gelen `KurumId` kabul edilmez; current tenant context esas alinir.

### Normal Kullanici

- Yetkili oldugu kurumlar arasinda gecis yapabilir.
- Secili kurum icinde permission + tesis scope kadar erisim saglar.
- Kurum yonetimi yapamaz.

## 5. Hangi Dosyalara Dokunulacagi

### Tenant foundation

- [backend/Program.cs](/c:/Users/cuce/source/repos/STYS/backend/Program.cs)
- [backend/Infrastructure/EntityFramework/StysAppDbContext.cs](/c:/Users/cuce/source/repos/STYS/backend/Infrastructure/EntityFramework/StysAppDbContext.cs)
- [platform/TOD.Platform.Identity/Infrastructure/EntityFramework/TodIdentityDbContext.cs](/c:/Users/cuce/source/repos/STYS/platform/TOD.Platform.Identity/Infrastructure/EntityFramework/TodIdentityDbContext.cs)
- [platform/TOD.Platform.Security/Auth/Services/JwtTokenService.cs](/c:/Users/cuce/source/repos/STYS/platform/TOD.Platform.Security/Auth/Services/JwtTokenService.cs)
- [platform/TOD.Platform.Security/Auth/DTO/GenerateTokenRequest.cs](/c:/Users/cuce/source/repos/STYS/platform/TOD.Platform.Security/Auth/DTO/GenerateTokenRequest.cs)
- [platform/TOD.Platform.Security/Auth/Services/AuthenticationService.cs](/c:/Users/cuce/source/repos/STYS/platform/TOD.Platform.Security/Auth/Services/AuthenticationService.cs)
- [platform/TOD.Platform.AspNetCore/Authorization/TodPlatformJwtAuthenticationExtensions.cs](/c:/Users/cuce/source/repos/STYS/platform/TOD.Platform.AspNetCore/Authorization/TodPlatformJwtAuthenticationExtensions.cs)
- [platform/TOD.Platform.Security/Auth/Services/ICurrentUserAccessor.cs](/c:/Users/cuce/source/repos/STYS/platform/TOD.Platform.Security/Auth/Services/ICurrentUserAccessor.cs)

### Scope ve user management

- [backend/AccessScope/AccessScopeProvider.cs](/c:/Users/cuce/source/repos/STYS/backend/AccessScope/AccessScopeProvider.cs)
- [backend/AccessScope/DomainAccessScope.cs](/c:/Users/cuce/source/repos/STYS/backend/AccessScope/DomainAccessScope.cs)
- [backend/AccessScope/UserActorScope.cs](/c:/Users/cuce/source/repos/STYS/backend/AccessScope/UserActorScope.cs)
- [backend/Kullanicilar/Services/StysScopedUserService.cs](/c:/Users/cuce/source/repos/STYS/backend/Kullanicilar/Services/StysScopedUserService.cs)
- [backend/Tesisler/Entities/Tesis.cs](/c:/Users/cuce/source/repos/STYS/backend/Tesisler/Entities/Tesis.cs)
- [backend/Tesisler/Services/TesisService.cs](/c:/Users/cuce/source/repos/STYS/backend/Tesisler/Services/TesisService.cs)

### Frontend

- [frontend/src/app/pages/auth/auth.service.ts](/c:/Users/cuce/source/repos/STYS/frontend/src/app/pages/auth/auth.service.ts)
- [frontend/src/app/pages/auth/dto/login-response.dto.ts](/c:/Users/cuce/source/repos/STYS/frontend/src/app/pages/auth/dto/login-response.dto.ts)
- [frontend/src/app/layout/component/app.topbar.ts](/c:/Users/cuce/source/repos/STYS/frontend/src/app/layout/component/app.topbar.ts)
- [frontend/src/app/core/menu/menu-runtime.service.ts](/c:/Users/cuce/source/repos/STYS/frontend/src/app/core/menu/menu-runtime.service.ts)
- [frontend/src/app/pages/muhasebe/services/muhasebe-tesis-context.service.ts](/c:/Users/cuce/source/repos/STYS/frontend/src/app/pages/muhasebe/services/muhasebe-tesis-context.service.ts)
- kullanici/rol/group yonetimi sayfalari

## 6. Hangi Yeni Dosyalarin Olusturulacagi

### Backend

- `backend/Kurumlar/*`
- tenant migration dosyalari

### Platform

- `platform/TOD.Platform.Persistence.Rdbms/Entities/ITenantEntity.cs`
- `platform/TOD.Platform.Security/Auth/Services/ICurrentTenantAccessor.cs`
- `platform/TOD.Platform.Security/Auth/Services/HttpContextCurrentTenantAccessor.cs`
- `platform/TOD.Platform.Identity/UserKurums/*`

### Frontend

- `frontend/src/app/core/tenant/kurum-session.service.ts`
- `frontend/src/app/core/tenant/dto/*`
- `frontend/src/app/pages/kurum-yonetimi/*`
- aktif kurum secim component veya topbar parcasi

## 7. Fazlara Bolunmus Uygulama Plani

### Faz 1 - Tenant Foundation

- `Kurum` tablosu
- `UserKurum` tablosu
- `Tesis.KurumId`
- aktif `KurumId` claim
- current tenant accessor
- tenant-aware global query filter altyapisi
- kurum degistirme backend/frontend akisi

### Faz 2 - Tesis ve Kullanici Yonetimi

- tesis CRUD kurum aware
- kurum admin kullanici kapsam kurallari
- kullanici yaratma ve group atamada kurum siniri

### Faz 3 - Rezervasyon + Kamp + Restoran

- root entity bazli tenant awareness
- listeleme ve detay endpointlerinde tenant filter
- tesis secimleri aktif kuruma gore daraltma

### Faz 4 - Muhasebe

- muhasebe root kayitlari tenant aware
- tesis bazli secim ve genel tanim semantiklerinin ayrimi
- rapor, bakiye ve belge akislarinda tenant guvencesi

### Faz 5 - Sertlestirme

- admin ekranlari
- audit/rapor/bakim sorgulari
- seed/migration cleanup
- regression test matrisi

## 8. Ilk Uygulanacak Faz Icin Minimum Degisiklik Listesi

- `Kurum` entity ve CRUD olustur.
- `UserKurum` iliskisini ekle.
- `Tesis` entity'sine zorunlu `KurumId` ekle.
- mevcut tesisleri tek bir legacy kuruma backfill edecek migration yaz.
- login/refresh response'una aktif kurum baglamini ekle.
- JWT token icine `aktifKurumId` claim ekle.
- kurum degistirme endpoint'i ekle ve yeni token uret.
- `ICurrentTenantAccessor` uzerinden tenant context oku.
- `ITenantEntity` uygulayan entity'lerde query filter kullan.
- frontend topbar'a aktif kurum secimi ekle.
- kurum degisiminde aktif tesis secimini yeniden dogrula.

## 9. Riskler

### Migration riski

- `Tesis.KurumId` zorunlu hale gelirken mevcut verinin dogru backfill edilmesi gerekir.
- Yanlis backfill tum domain verisinin yanlis tenant altinda kalmasina neden olur.

### Global query filter riski

- Su anki `IsDeleted` filter ile tenant filter'in dogru compose edilmesi gerekir.
- Yanlis tasarimda ya veriler gizlenir ya da tenant izolasyonu delinmis olur.

### JWT / refresh token riski

- Kurum degisiminde refresh token/active claim uyumu bozulabilir.
- `UserKurum` degisiklikleri token invalidation ile baglanmalidir.

### Platform-backend bagimlilik riski

- `UserKurum` icinde `Kurum` navigation property acilirsa identity katmani backend'e baglanir.
- Bu kesinlikle engellenmelidir.

### Mevcut `TesisId` bazli sorgularin etkilenme riski

- Bircok servis bugun `TesisId` filtreli.
- Tenant foundation sonrasi bu servisler hemen bozulmaz; ancak aktif kurum disi tesis kullanimi backend'de de bloklanmalidir.

### Kurum admin yetki siniri riski

- Sadece global permission kontrolu yeterli degildir.
- Kurum admin kararinin aktif kurum baglaminda verilmesi gerekir.

### Role/permission modelinin tenant bagimsiz kalmasi riski

- Ilk faz icin bu kabul edilebilir.
- Ancak ayni kullanicinin farkli kurumlarda farkli group uyeligi ihtiyaci varsa ikinci asamada model genisletilmelidir.

## 10. Tenant Bagimsiz Kalacak Tablolar

- `Countries`
- `Iller`
- `Users`
- `Roles`
- `UserGroups`
- `UserGroupRoles`
- `MenuItems`
- `MenuItemRoles`
- `RefreshTokens`
- global sozluk / reference tablolari

## 11. Ilk Asamada Tenant-Aware Yapilacak Tablolar

- `Kurumlar`
- `UserKurums`
- `Tesisler`

Ilk foundation icin bunlar yeterlidir. Diger domain kayitlari gecici olarak `Tesis -> Kurum` baglantisi uzerinden korunabilir.

## 12. Sonraki Asamalara Birakilmasi Gereken Tablolar

- Rezervasyon root ve cocuk kayitlari
- Kamp root ve cocuk kayitlari
- Restoran root ve cocuk kayitlari
- Muhasebe root ve cocuk kayitlari
- agir rapor/ozet/bakiye tablolari

## 13. Muhasebe Modulunun Tenant-Aware Yapilmasi Icin Ozel Plan

Muhasebe modulu bugun yogun sekilde `TesisId` ve bazen `TesisId = null` genel tanim mantigiyla calisiyor.

### Onerilen sira

1. aktif kurum disi tesislerin frontend secim listesine dusmesini engelle
2. muhasebe servislerinde tesis seciminin aktif kuruma ait oldugunu backend'de dogrula
3. su kok kayitlari tenant-aware hale getir:

- `CariKart`
- `SatisBelgesi`
- `MuhasebeFis`
- `MuhasebeDonem`
- `MuhasebeHesapBakiye`
- `StokHareket`
- `TahsilatOdemeBelgesi`
- `KasaBankaHesap`

### Ozel not

`TesisId = null` ile calisan genel tanimlar icin ayrik bir semantik gerekecek:

- sistem genel tanim
- kurum ortak tanim
- tesis ozel tanim

Bu ayrim ikinci muhasebe fazinda netlestirilmelidir.

## 14. Rezervasyon Modulunun Tenant-Aware Yapilmasi Icin Ozel Plan

[RezervasyonService.cs](/c:/Users/cuce/source/repos/STYS/backend/Rezervasyonlar/Services/RezervasyonService.cs) bugun zaten `scope.TesisIds` ile filtre yapiyor.

### Onerilen plan

1. aktif kurum disi tesis secimi engellensin
2. `Rezervasyon`, `RezervasyonOdeme`, `RezervasyonEkHizmet`, `RezervasyonSegment` zincirinde tenant guvencesi saglansin
3. rezervasyon root'una dogrudan `KurumId` eklenip eklenmeyecegi performans ve raporlama ihtiyacina gore karar verilsin

Yuksek hacimli raporlar bekleniyorsa root kayitta `KurumId` tutmak avantajlidir.

## 15. Kamp Modulunun Tenant-Aware Yapilmasi Icin Ozel Plan

[KampBasvuru.cs](/c:/Users/cuce/source/repos/STYS/backend/Kamp/Entities/KampBasvuru.cs) ve iliskili kamp kayitlari tesis bazli calisiyor.

### Onerilen plan

1. `KampDonemiTesis`, `KampBasvuru`, `KampRezervasyon` aktif kurum kapsaminda filtrelensin
2. kamp basvuru baglami yalniz aktif kurumdaki tesis atamalarini gostersin
3. kamp admin operasyonlari kurum admin kapsamiyla uyumlu hale getirilsin

Lookup tipi referanslari tenant bagimsiz kalabilir.

## 16. Restoran Modulunun Tenant-Aware Yapilmasi Icin Ozel Plan

Restoran modulu `backend/RestoranYonetimi/*` altinda ve root iliskisi yine tesis uzerinden kurulmus durumda.

### Onerilen plan

1. `Restoran`, `RestoranMasa`, `RestoranSiparis`, `RestoranOdeme`, `RestoranMenuKategori`, `RestoranMenuUrun` aktif kurum kapsaminda filtrelensin
2. garson ve restoran yonetici atamalari `UserKurum` + tesis kapsamiyla birlikte dogrulansin
3. musteri menu ve siparis akislarinda aktif kurum disi tesisler backend'de de reddedilsin

## Sonuc ve Oneri

Ilk uygulanmasi gereken dogru minimum adim tenant foundation'dir:

- `Kurum`
- `UserKurum`
- `Tesis.KurumId`
- aktif kurum claim
- current tenant accessor
- EF global tenant filter altyapisi

Kurum admin modeli ilk fazda `UserKurum.IsKurumAdmin` uzerinden kurulmalidir. Bu, mevcut global role/group yapisini kirmadan kurum bazli yonetim ayrimini saglar.

En az kirici ve en hizli ilerleyen yol budur.
