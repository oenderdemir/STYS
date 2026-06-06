# Revert Analizi

## Amaç
Bu çalışma, ilgili dönemde bozulan veya geri dönen doğru işleri güvenli şekilde geri almak için hazırlanmıştır.

## İncelenen Aralık
- Başlangıç: `84ef66c5d77c8edd0085c59631601a8c248721b6`
- Bitiş: `8f4b6b8a4d30f2e509063e0e11196c60cc2e10fd`

## Korunan Commitler
- `a9ef36f047ff26e7c0326cd3dce49438b502aadb`
- `be6e3f46f7d473319a1657873426371766fd8730`
- `1383d09dbd18421829bafefa445949041447a220`
- `b312838a93b1ab49ceb46cea0d1deebca9251967`
- `752035420e04b2d64eeed0dd0908ca6c8646b7b9`

Özellikle şu alanlar korunmuştur:
- `frontend/src/app.config.ts`
- `backend/Muhasebe/CariKartlar/Entities/CariKartBankaHesabi.cs`
- `backend/Muhasebe/CariKartlar/Dtos/CariKartDtos.cs`
- `backend/Muhasebe/CariKartlar/Services/CariKartService.cs`
- `backend/Infrastructure/EntityFramework/StysAppDbContext.cs`
- `frontend/src/app/pages/muhasebe/cari-kartlar/*`
- `frontend/src/app/pages/muhasebe/satis-belgeleri/*`
- `frontend/src/app/pages/muhasebe/fisler/*`

## Revert Edilen Commitler
| Commit | Mesaj | Revert yöntemi | Not |
|---|---|---|---|
| `8f4b6b8a4d30f2e509063e0e11196c60cc2e10fd` | `Merge branch 'main' of remote repository` | `git revert -m 2` | Ana parent tarafı korundu. Merge çatışmaları korunan versiyon tercih edilerek çözüldü. |
| `6c110f2dcbc67c9abb912e2af86f2558c4fdfe3f` | `merge ui` | `8f4b6b8 revert'i ile tree seviyesinde kapsandı` | Ayrı revert uygulanmadı; üst merge revert zinciri zaten nötralize etti. |
| `6d3114d248b647b0076b1db280ffe979732ccc8a` | `arayüz` | `8f4b6b8 revert'i ile tree seviyesinde kapsandı` | Ayrı revert uygulanmadı; üst merge revert ile etkisi geri alındı. |

## Manuel Restore Edilen Alanlar
- `backend/Muhasebe/CariKartlar/Entities/CariKart.cs` içindeki yinelenen `BankaHesaplari` property tanımı kaldırıldı.
- `frontend/src/app/pages/muhasebe/models/muhasebe-fis.model.ts` içine `parseApiDate` export'u geri eklendi.
- Bu iki küçük düzeltme revert sonrası build regresyonlarını kapattı.

## Conflict Çözümleri
- `backend/Muhasebe/CariKartlar/Dtos/CariKartDtos.cs`
- `backend/Muhasebe/CariKartlar/Services/CariKartService.cs`
- `frontend/src/app/pages/muhasebe/cari-kartlar/cari-kartlar.dto.ts`
- `frontend/src/app/pages/muhasebe/cari-kartlar/cari-kartlar.html`
- `frontend/src/app/pages/muhasebe/cari-kartlar/cari-kartlar.ts`
- `frontend/src/app/pages/muhasebe/fisler/muhasebe-fisler.component.html`
- `frontend/src/app/pages/muhasebe/models/muhasebe-fis.model.ts`
- `frontend/src/app/pages/muhasebe/muavin-defter/muavin-defter.component.html`
- `frontend/src/app/pages/muhasebe/muavin-defter/muavin-defter.component.ts`
- `frontend/src/app/pages/muhasebe/yevmiye-defteri/yevmiye-defteri.component.html`
- `frontend/src/app/pages/muhasebe/yevmiye-defteri/yevmiye-defteri.component.ts`
- Tüm çatışmalarda korunan taraf tercih edildi.

## Riskli Alanlar
- `backend/Infrastructure/EntityFramework/Migrations/*` ve snapshot dosyaları
- DTO/model uyumu
- frontend/backend API sözleşmesi
- tarih/datepicker davranışı

## Build/Test
- Backend: `dotnet build backend/STYS.csproj` başarılı.
- Frontend: `npm run build` başarılı.
- Frontend build sırasında mevcut budget warning'leri devam ediyor; hata seviyesinde değil.

## Sonuç
- Merge/UI etkileri güvenli şekilde nötralize edildi.
- Muhasebe, cari kart ve datepicker çalışmaları korundu.
- Repo history rewrite yapılmadı, force push yapılmadı.
