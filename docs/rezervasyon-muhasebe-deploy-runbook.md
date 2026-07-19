# Rezervasyon Muhasebe Entegrasyonu Deploy Runbook

**Durum:** Deploy öncesi resmi runbook.
**Kapsanan commit aralığı:** `650d8cb` (Aktif muhasebe fişli rezervasyon ödeme iptali engellendi) → `031568b` (ws ve form-data güvenlik yamaları).
**İlgili dokümanlar:** `docs/rezervasyon-odeme-muhasebe-release-hardening-raporu.md`, `docs/rezervasyon-odeme-muhasebe-entegrasyonu-dogrulama-raporu.md`, `docs/angular-primeng-upgrade-degerlendirme-raporu.md`.

---

## 1. Kapsam

Bu deploy ile production'a taşınacak ana değişiklikler:

- **Rezervasyon ödemelerinin `TahsilatOdemeBelgesi` ile ilişkilendirilmesi** — `RezervasyonOdeme.TahsilatOdemeBelgesiId` üzerinden, migration `20260713155404_AddRezervasyonOdemeMuhasebeEntegrasyonu`.
- **Kasa/Banka/POS hesap seçimi** — rezervasyon ödeme alma akışında `KasaBankaHesapId` seçimi ve doğrulaması.
- **Rezervasyon ödeme iptali ve muhasebe fişi güvenliği** — Taslak/Onaylı muhasebe fişi olan ödemelerin backend'de 409 ile iptalinin engellenmesi (`650d8cb`); durum bazlı kural, `MuhasebeFisId` hiç sıfırlanmaz.
- **QuickCreate cari kart yetkisi** — `CariKartYonetimi.QuickCreate` permission'ı, dar kapsamlı `POST ui/rezervasyon/cari-kart-hizli-olustur` endpoint'i, migration `20260718120000_AddCariKartQuickCreatePermission` (yalnızca ResepsiyonistGrubu'na verilir, genel `CariKartYonetimi.Manage` verilmez).
- **Muhasebe fişi durumunun ödeme ekranında gösterilmesi** — rezervasyon ödeme dialogunda "Fiş #... - Taslak/Onaylı/İptal/Ters Kayıt" etiketi ve yetkiye göre "Fişe Git" linki (`MuhasebeFisYonetimi.View`).
- **Gelir belgesi / tahsilat kapama akışı** — check-out sonrası satış/gelir belgesi taslağı üretimi ve tahsilat kapama kuralları (önceki fazlarda tamamlanmış, bu deploy kapsamında regresyon olarak doğrulanmalı).
- **Angular/PrimeNG 21 patch hizalaması** — `@angular/*` 21.0.6→21.2.18, `primeng` 21.0.2→21.1.9, `@primeuix/themes` 2.0.2→2.0.3 (major değişikliği yok, template/layout dosyalarına dokunulmadı).
- **NuGet/npm production vulnerability temizliği** — `Microsoft.OpenApi` 2.3.0→2.7.5, `SixLabors.ImageSharp` 2.1.9→2.1.11, kullanılmayan `primeclt` paketinin kaldırılması, `ws`/`form-data` patch güncellemeleri. `dotnet build` ve `npm audit --omit=dev` artık sıfır uyarı/vulnerability üretiyor.

---

## 2. Deploy Öncesi Kontroller

### 2.1 Kod/CI kontrolü

- [ ] `git status` temiz, `origin/main` güncel (`git fetch && git log origin/main -1` ile karşılaştırılmalı).
- [ ] `dotnet build STYS.sln` — **0 error**.
- [ ] `npm run build` (frontend) — **0 error** (bundle budget uyarısı hariç, bkz. Bölüm 7).
- [ ] `dotnet test tests/STYS.Tests/STYS.Tests.csproj --no-build` — **Failed: 0** (entegrasyon testleri env var yoksa skip olmalı).
- [ ] `npm audit --omit=dev` (frontend) — **`found 0 vulnerabilities`**.
- [ ] `dotnet restore STYS.sln` / `dotnet build STYS.sln` — **NU1902/NU1903 dahil hiçbir NuGet vulnerability warning üretmemeli**.

### 2.2 Veritabanı yedeği

- [ ] Prod migration uygulanmadan **önce tam DB yedeği** alınmalı (full backup, point-in-time restore imkânı olacak şekilde).
- [ ] Yedeğin geri yüklenebilirliği (restore testi) daha önce doğrulanmış bir prosedürle teyit edilmiş olmalı.

### 2.3 Migration ön-kontrol sorgusu

`IX_TahsilatOdemeBelgeleri_KaynakModul_KaynakId` unique filtered index'i migration `20260713155404_AddRezervasyonOdemeMuhasebeEntegrasyonu` içinde oluşturuluyor. Bu index, mevcut satırlar arasında `KaynakModul`+`KaynakId` çakışması varsa migration'ın uygulanmasını engeller. Aşağıdaki sorgu **gerçek prod DB'de, migration uygulanmadan önce** çalıştırılmalı:

```sql
SELECT KaynakModul, KaynakId, COUNT(*)
FROM muhasebe.TahsilatOdemeBelgeleri
WHERE IsDeleted = 0 AND KaynakId IS NOT NULL
GROUP BY KaynakModul, KaynakId
HAVING COUNT(*) > 1;
```

**Beklenen sonuç: 0 satır.**

Eğer 0 satır dönmezse:

- **Migration uygulanmamalı.**
- Tekrarlı `KaynakModul`/`KaynakId` kayıtları ayrıca incelenmeli.
- Hangi kayıtların gerçek/iptal/silinmiş olduğu değerlendirilmeden devam edilmemeli — kayıtlardan hangisinin "asıl" tutulacağı, diğerlerinin `IsDeleted=1` yapılıp yapılmayacağı ayrı bir veri temizliği kararı gerektirir ve bu runbook'un kapsamı dışındadır.

---

## 3. Migration Uygulama

Proje EF Core migration'ları `StysAppDbContext` altında yönetiliyor. Standart komut:

```bash
dotnet ef database update --context StysAppDbContext --project backend/STYS.csproj --startup-project backend/STYS.csproj
```

Uygulamadan önce, üretilecek SQL'i gözden geçirmek için idempotent script çıkarılması önerilir:

```bash
dotnet ef migrations script --context StysAppDbContext --project backend/STYS.csproj --startup-project backend/STYS.csproj --idempotent --output artifacts/migration-deploy.sql
```

Mevcut migration zincirinin doğrulanması:

```bash
dotnet ef migrations list --context StysAppDbContext --project backend/STYS.csproj --startup-project backend/STYS.csproj
```

*Not: Kurumsal deploy pipeline'ı farklı bir migration mekanizması (ör. CI/CD adımı, konteyner init container, ayrı bir migration runner) kullanıyorsa, yukarıdaki komutlar **kurum pipeline'ına göre uyarlanacak**.*

**Önemli kurallar:**

- Migration **yalnızca** Bölüm 2.3'teki ön-kontrol sorgusu 0 satır döndürdüğünde uygulanmalı.
- **Aynı anda birden fazla deploy/migration çalıştırılmamalı** — paralel migration denemeleri EF Core migration history tablosunda çakışmaya ve tutarsız şema durumuna yol açabilir.

---

## 4. Deploy Sırası

Önerilen sıra:

1. **DB backup**
2. **Migration ön-kontrol sorgusu** (Bölüm 2.3)
3. **Backend deploy** (yeni kod, henüz migration uygulanmamış DB ile çalışacak şekilde — mevcut migration'lar geriye uyumlu tasarlanmıştır, bkz. Bölüm 1'deki nullable alan/varsayılan değer notları)
4. **Migration uygulama** (Bölüm 3)
5. **Frontend deploy**
6. **Uygulama restart / cache temizliği**
7. **Smoke test** (Bölüm 5)

*Not: Mevcut pipeline bu sıradan farklıysa (ör. backend+migration tek adımda, blue-green deploy, vb.) **kurum pipeline'ına göre uyarlanacak**.*

---

## 5. Deploy Sonrası Smoke Test

### Genel

- [ ] Login çalışıyor mu?
- [ ] Dashboard açılıyor mu?
- [ ] Menü yetkiye göre doğru mu?

### Resepsiyonist

- [ ] Rezervasyon listesi açılıyor mu?
- [ ] Rezervasyon ödeme dialogu açılıyor mu?
- [ ] Nakit/Havale-EFT/Kredi Kartı ödeme alınabiliyor mu?
- [ ] Kasa/Banka/POS hesabı seçilebiliyor mu?
- [ ] Hızlı cari kart oluşturulabiliyor mu (QuickCreate)?
- [ ] Resepsiyonist muhasebe ekranlarına giremiyor mu (Cari Kartlar, Tahsilat/Ödeme Belgeleri, Muhasebe Fişleri, Satış Belgeleri, Kasa/Banka/POS Hesapları — menüde görünmemeli, direkt URL'de backend 403 vermeli)?

### Muhasebe

- [ ] Tahsilat/Ödeme Belgesi ekranı açılıyor mu?
- [ ] Rezervasyon ödemesinden oluşan tahsilat belgesi görünüyor mu?
- [ ] Muhasebe fişi oluşturulabiliyor mu?
- [ ] Muhasebe fişi listede görünüyor mu?
- [ ] Fiş iptal edilince rezervasyon ödeme ekranında durum güncelleniyor mu (Taslak/Onaylı → İptal etiketine dönüyor, iptal butonu tekrar aktif oluyor mu)?

### Ödeme iptali

- [ ] Fişsiz ödeme iptal edilebiliyor mu? (buton aktif)
- [ ] Taslak fişli ödeme iptal edilemiyor mu? (buton disabled, backend 409)
- [ ] Onaylı fişli ödeme iptal edilemiyor mu? (buton disabled, backend 409)
- [ ] İptal fişli ödeme iptal edilebiliyor mu? (buton aktif)
- [ ] Otomatik ters kayıt oluşmuyor mu? (Onaylı fişli ödeme iptal denemesinde yeni bir ters kayıt fişi üretilmemeli)

### Gelir / tahakkuk

- [ ] Check-out sonrası gelir belgesi taslağı oluşuyor mu?
- [ ] Satış belgesi muhasebe fişi oluşturulabiliyor mu?
- [ ] Tahsilat kapama çalışıyor mu?
- [ ] Farklı cari kartlı tahsilatların kapatılması beklenen kurala göre davranıyor mu?

### Frontend

- [ ] `p-table` lazy-load çalışıyor mu?
- [ ] `p-select` çalışıyor mu?
- [ ] `p-datepicker` çalışıyor mu?
- [ ] DynamicDialog açılıp kapanıyor mu?
- [ ] Toast mesajları görünüyor mu?
- [ ] Fişe Git linki yetkiye göre çalışıyor mu? (muhasebe kullanıcı görüyor + doğru fişe gidiyor, resepsiyonist görmüyor + direkt URL'de backend engelliyor)

---

## 6. Rollback Notları

- **Kod rollback yapılacaksa backend + frontend birlikte düşünülmeli.** Frontend, backend'in yeni DTO alanlarını (`MuhasebeFisId`, `MuhasebeFisDurumu`, `MuhasebeFisNo`) ve yeni endpoint'i (`cari-kart-hizli-olustur`) varsayarak çalışıyor; sadece frontend'i eski sürüme almak bu alanların boş dönmesine, sadece backend'i eski sürüme almak ise frontend'in var olmayan endpoint'lere istek atmasına yol açar.
- **Migration uygulandıktan sonra rollback için DB backup önemlidir.** Kod rollback'i migration'ı otomatik geri almaz; şema değişikliğinin geri alınması ayrı bir `dotnet ef database update <önceki-migration>` veya backup'tan restore gerektirir.
- **Muhasebe fişi/tahsilat belgesi üretilmiş canlı kayıtlar varsa uygulama rollback'i veri modelini geriye uyumsuz bırakabilir.** Yeni migration sonrası oluşturulan `TahsilatOdemeBelgesi`/`MuhasebeFisi` kayıtları, eski kod sürümünün bilmediği ilişkileri (`TahsilatOdemeBelgesiId`, `MuhasebeFisId`) taşır; eski koda dönüldüğünde bu kayıtlar ya görünmez ya da tutarsız görünür.
- **Bu nedenle deploy sonrası canlı işlem başladıysa rollback yerine hotfix tercih edilebilir.** Migration uygulanıp gerçek ödeme/fiş kayıtları oluşturulduktan sonra tam rollback yerine, sorunu hedefleyen küçük bir düzeltme (hotfix) ile ileri gitmek, veri bütünlüğü riskini daha düşük tutar.

---

## 7. Bilinen Uyarılar

- **Frontend bundle budget warning biliniyor, blocker değildir.** `ng build` çıktısında `bundle initial exceeded maximum budget (4.22-4.23 MB / 2.80 MB)` uyarısı önceden bilinen, mevcut deploy kapsamında çözülmesi planlanmayan bir durumdur.
- **DevDependency-only npm audit uyarıları production audit kapsamı dışındadır.** `npm audit` (flag'siz) devDependency zincirlerinden (ör. `webpack-dev-server`→`sockjs`→`uuid`) kaynaklanan ek uyarılar gösterebilir; bunlar `npm audit --omit=dev` ile filtrelenip production riskine dahil edilmemelidir.
- **Angular route seviyesinde bazı muhasebe ekranlarında guard eksik olabilir; backend 403 ve menü gizleme ile veri güvenliği sağlanmaktadır.** Resepsiyonist gibi yetkisiz kullanıcılar muhasebe route'larına direkt URL ile gittiğinde sayfa kabuğu (layout) render olabilir, ancak tüm veri istekleri backend'den 403 döner ve ekranda boş tablo/hata toast'ı görünür — gerçek güvenlik sınırı korunmaktadır. Bu, ayrı bir UX hardening backlog maddesidir (bkz. Bölüm 8).

---

## 8. Backlog

- Muhasebe route'larına frontend permission guard eklenmesi (şu an sadece backend 403 + menü gizleme ile korunuyor).
- Bundle size / lazy loading iyileştirmesi (route bazlı eager import'ların azaltılması).
- Angular 22 / PrimeNG 22 ayrı branch değerlendirmesi (bkz. `docs/angular-primeng-upgrade-degerlendirme-raporu.md`, Seçenek B — bu deploy'a dahil değil).
- DevDependency audit takibi (`uuid`/`sockjs`/`webpack-dev-server` zincirindeki "No fix available" uyarılarının üst paket güncellemesiyle ne zaman çözülebileceğinin periyodik takibi).

---

## Deploy Onay Checklist

| Adım | Durum |
|---|---|
| DB backup alındı mı? | ☐ |
| Ön-kontrol sorgusu 0 satır mı? | ☐ |
| Backend deploy edildi mi? | ☐ |
| Migration uygulandı mı? | ☐ |
| Frontend deploy edildi mi? | ☐ |
| Smoke test geçti mi? | ☐ |
| Pilot kullanıcı onayı alındı mı? | ☐ |
