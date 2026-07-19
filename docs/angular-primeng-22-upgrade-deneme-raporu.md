# Angular + PrimeNG 22 Upgrade Deneme Raporu (Güncelleme — Node blokajı kalktıktan sonra)

**Branch:** `upgrade/angular-primeng-22` (deneysel, `main`'den ayrı — main'e merge edilmedi, **edilmemeli**)
**Taban:** `main` @ commit `3c75755` (Seçenek A tamamlanmış, `npm audit --omit=dev` 0 vulnerability, build temiz)
**Bu turun ön koşulu:** Node.js, sistemde kurulu **nvm4w** (`C:\Users\cuce\AppData\Local\nvm`) aracılığıyla `24.13.1` → **`24.18.0`**'a yükseltildi (proje-bazlı değil, nvm'in sistem-geneli symlink mekanizmasıyla — bkz. `docs/node-angular22-upgrade-hazirlik-raporu.md`).

**SONUÇ (özet — önceki raporu geçersiz kılar):** Angular 22 + PrimeNG 22 upgrade'i, kod seviyesinde **başarıyla tamamlandı** — `npm run build` **0 hata** ile geçiyor, `npm audit --omit=dev` **0 vulnerability**, backend etkilenmedi, Playwright smoke testleri önceki (Angular 21) baseline ile birebir aynı sonuçları veriyor. **ANCAK upgrade sırasında PrimeNG'nin lisans modelini değiştirdiği tespit edildi: PrimeNG 22, artık ücretsiz/MIT değil, ticari bir lisans anahtarı (PrimeUI License) gerektiriyor ve bu olmadan uygulama her ekranda kırmızı "Invalid PrimeUI License" uyarı banner'ı gösteriyor.** Bu, kod uyumluluğu meselesi değil, **iş/hukuki bir lisanslama kararı** gerektiren bir bulgu — bu nedenle **merge önerilmiyor**, ayrı bir onay gerekiyor.

---

## 1. Ön Koşul: Node.js Upgrade (nvm4w ile)

- Sistemde **nvm4w** kurulu olduğu (`C:\Users\cuce\AppData\Local\nvm\nvm.exe`) doğrulandı — önceki raporun "hiç version manager yok" tespiti **yanlıştı**, sadece `NVM_HOME`/`NVM_SYMLINK` ortam değişkenleri o oturumda set değildi.
- `nvm install 24.18.0` + `nvm use 24.18.0` ile Node **24.13.1 → 24.18.0**'a geçildi (Angular 22'nin `^24.15.0` gereksinimini karşılıyor).
- **Not:** nvm4w sistem genelinde TEK bir symlink (`C:\nvm4w\nodejs`) üzerinden çalışıyor — gerçek anlamda proje-bazlı izolasyon sağlamıyor (önceki raporda önerilen "fnm ile proje bazlı" senaryosundan farklı, daha çok Seçenek A/sistem-geneli davranışına yakın, ama nvm'in kendi sürüm-değiştirme kolaylığı sayesinde geri dönüş çok kolay: `nvm use 24.13.1`).

---

## 2. Güncellenen Paketler

| Paket | Eski (main) | Yeni (bu branch) |
|---|---|---|
| `@angular/core`, `common`, `compiler`, `compiler-cli`, `forms`, `platform-browser(-dynamic)`, `router` | 21.2.18 | **22.0.7** |
| `@angular/cli`, `@angular-devkit/build-angular` | 21.2.19 | **22.0.7** |
| `@angular/cdk` | 21.2.14 | **22.0.5** (22.0.6/22.0.7 hiç yayınlanmamış) |
| `typescript` | 5.9.3 | **6.0.3** |
| `primeng` | 21.1.9 | **22.0.0** |
| `@primeuix/themes` | 2.0.3 | **3.0.0** |
| `primeicons` | 7.0.0 | **değiştirilmedi** (opsiyonel, gerekmedi) |

`ng update @angular/core@22 @angular/cli@22` **resmi migration schematic'leri çalıştırdı**: 139 dosyada otomatik `changeDetection: ChangeDetectionStrategy.Eager` eklendi (pre-v22 davranışını korumak için, Angular'ın kendi resmi codemod'u — `Eager`, yeni `ChangeDetectionStrategy` enum'unda gerçek, geçerli bir üye, `Default` ile aynı sayısal değere sahip bir takma ad; IDE'de görülen "geçersiz enum" uyarısı doğrulandı ve **yanlış pozitif** — TS sunucusunun eski tip önbelleğinden kaynaklanıyor, gerçek `ng build` bunu hatasız kabul ediyor), 1 dosyada `$safeNavigationMigration()` sarmalaması (optional chaining'in fonksiyon argümanı olarak geçirilmesiyle ilgili bir semantik incelik), `tsconfig.app.json`/`tsconfig.spec.json`'a yeni "extended diagnostics" bastırma ayarları eklendi.

`ng update @angular/cdk@22 --force` sorunsuz geçti (migration: "No changes made").

`npm install primeng@22 @primeuix/themes@3` ERESOLVE hatası vermeden kuruldu.

---

## 3. TypeScript Peer Dependency Sonucu

**Önceki tahmin doğrulandı, düzeltildi:** Angular 22, `typescript: '>=6.0 <6.1'` istiyor (TS 7.x değil). `typescript@6.0.3` gerçek, kararlı bir sürüm ve sorunsuz kuruldu, TypeScript 7'ye **geçilmedi**.

---

## 4. Build Sonucu

İlk deneme **11 farklı derleme hatası** üretti (tümü PrimeNG 22'nin component API değişikliklerinden kaynaklanıyordu). Tüm hatalar teşhis edilip **minimal, mekanik düzeltmelerle** giderildi (bkz. Bölüm 5). Son durum:

```
Application bundle generation complete. [31.548 seconds]
Initial total: 4.51 MB (raw) / 658.57 kB (transfer)
▲ [WARNING] bundle initial exceeded maximum budget. Budget 2.80 MB was not met by 1.71 MB with a total of 4.51 MB.
exit code: 0
```

**0 hata.** Tek uyarı: bundle-budget (bilinen, Angular 21'de de vardı; Angular/PrimeNG 22'nin kendisi biraz daha büyük olduğundan bütçe aşımı arttı — 1.43 MB → 1.71 MB — ama bu blocker değil, önceden de kabul edilen bir durumdu).

`npm audit --omit=dev` → **`found 0 vulnerabilities`**.

`dotnet build STYS.sln` / `dotnet test` → **0 warning, 0 error, 297 passed / 0 failed / 18 skipped** (backend'e hiç dokunulmadı, beklenen sonuç).

---

## 5. Kırılan Yerler ve Yapılan Minimal Düzeltmeler

Hiçbiri `app.topbar.ts`, `app.menu.ts`, `layout.service.ts` gibi **korunması istenen özel şablon dosyalarına dokunmadı**. Tüm düzeltmeler ya PrimeNG'nin resmi component selector yeniden adlandırmaları (camelCase → kebab-case, PrimeNG'nin kendi `.d.ts` dosyalarından doğrulandı) ya da izole, tek-satırlık tip/API düzeltmeleriydi:

| Kategori | Detay | Etkilenen dosya sayısı |
|---|---|---|
| **PrimeNG selector rename** | `p-sortIcon`→`p-sort-icon`, `p-treeTableToggler`→`p-treetable-toggler`, `p-treeTable`→`p-treetable`, `p-confirmDialog`→`p-confirm-dialog`, `p-fileUpload`→`p-fileupload`, `p-progressSpinner`→`p-progress-spinner`, `p-columnFilter`→`p-column-filter`, `p-autoComplete`→`p-autocomplete`, `p-inputNumber`→`p-inputnumber`, `p-multiSelect`→`p-multiselect`, `p-radioButton`→`p-radiobutton`, `p-tableCheckbox`/`p-tableHeaderCheckbox`→kebab-case | ~21 dosya (mekanik `sed` ile, her biri PrimeNG'nin `node_modules/primeng/types/*.d.ts` içindeki gerçek `ɵcmp` selector tanımından doğrulandı) |
| **Kaldırılmış component** | `p-treeTableCheckbox` PrimeNG 22'de hiç yok artık (sadece `treedemo.ts` — Sakai UIKit demo sayfası, iş mantığı değil — etkiledi); kaldırıldı, `selectionMode="checkbox"` zaten otomatik render ediyor | 1 dosya (demo) |
| **AutoComplete input rename** | `minLength` → `minQueryLength` | 3 dosya (satis-belgeleri, tasinir-kartlari, tasinir-kodlari — gerçek iş sayfaları) |
| **Chip input kaldırıldı** | `[styleClass]` → `[class]` (Angular'ın native class binding'i her zaman çalışır, Chip artık kendi `styleClass` @Input'unu deklare etmiyor) | 1 dosya (musteri-menu.html) |
| **tsconfig deprecation** | `baseUrl` TS 6.0'da deprecated → TypeScript'in kendi önerdiği `"ignoreDeprecations": "6.0"` eklendi (davranış değişmedi, `@/*` path alias'ı hâlâ çalışıyor) | tsconfig.json |
| **Theme API tip sıkılaştırması** | `app.configurator.ts` (Sakai'nin kendi dosyası, özelleştirilmemiş) — `surfacePalette` artık `undefined` kabul etmiyor, null-safe koşullu çağrıya çevrildi | 1 dosya (Sakai boilerplate) |
| **Event tipi sıkılaştırması** | `oda-temizlik-yonetimi.ts`'de PrimeNG Menu'nün `.show()` metodu artık gerçek `Event` istiyor; sentetik "anchor event" nesnesi `as unknown as Event` ile tip belirtildi (çalışma zamanı davranışı değişmedi) | 1 dosya (gerçek iş sayfası) |
| **Chart tip sıkılaştırması** | 3 rapor sayfasında `chartData`/`chartOptions` `unknown` olarak deklare edilmişti, PrimeNG 22'nin `p-chart` binding'leri artık `unknown`'ı kabul etmiyor → `any`'ye genişletildi (davranış değişmedi, zaten gevşek tipliydi) | 3 dosya (raporlar) |
| **pButton API kırılımı** | `[pButton]` direktifinin `icon`/`label` @Input()'ları kaldırıldı (artık projected content bekliyor); tek kullanım yeri **`tabledemo.ts`** (UIKit demo, iş mantığı değil) — `<p-button icon=... label=... (onClick)=...>` component'ine çevrildi (bu component icon/label'ı hâlâ normal @Input olarak destekliyor) | 1 dosya (demo) |

**Önemli:** İlk taramada `pButton icon=/label=` deseninin 22 dosyada (`app.menu.ts` dahil) göründüğü düşünülmüştü (kaba `grep` ile) — **gerçek derleme hatası sadece 1 dosyada (tabledemo.ts) çıktı**. `app.menu.ts` ve diğer 20 dosya, statik (interpolasyonsuz) `icon="pi pi-x"` gibi düz string attribute kullandığından derleme hatası vermedi; bu dosyalara **hiç dokunulmadı**.

---

## 6. Playwright Smoke Sonucu

Backend (Node ile ilgisiz, `.NET`) ve frontend (`npm start`, Angular 22 dev-server) gerçek ortamda ayağa kaldırılıp test edildi. **Tüm sonuçlar Seçenek A (Angular 21) baseline'ıyla birebir aynı:**

| Senaryo | Sonuç |
|---|---|
| Login (trt-admin) | ✅ |
| Layout/dashboard/topbar/sidebar | ✅ (ekran görüntüsüyle doğrulandı — **tek fark: "Invalid PrimeUI License" banner'ı, bkz. Bölüm 7**) |
| Menü — Rezervasyon görünür | ✅ |
| Rezervasyon listesi `p-table` lazy-load | ✅ (10 kayıt) |
| Muhasebe Fişleri `p-table` | ✅ (29 kayıt) |
| Toast container mount | ✅ |
| DynamicDialog (stok hareketleri) sayfası | ✅ açıldı |
| Konsol hatası | Sadece bilinen, önceden var olan 403/depo-filtre hatası (bu upgrade'den bağımsız, önceki turlarda da görülmüştü) |

Rezervasyon ödeme dialogu, Fişe Git linki, disabled iptal butonu, resepsiyonist 403 senaryoları bu turda **ayrıca tekrar koşulmadı** çünkü Bölüm 7'deki lisans bulgusu tespit edilir edilmez tüm ek doğrulama çalışması durduruldu (lisans sorunu çözülmeden bu ekranların "production-ready" sayılması zaten mümkün değil).

---

## 7. KRİTİK BULGU: PrimeNG 22 Artık Ücretsiz Değil — PrimeUI Ticari Lisans Zorunluluğu

Dashboard ekran görüntüsünde, "Bugün Check-out Yapacaklar" kartının üzerinde **kırmızı bir banner** görüldü: **"Invalid PrimeUI License"**.

### Kök neden (kod incelemesiyle doğrulandı)

`node_modules/primeng/fesm2022/primeng-config.mjs` içinde, `providePrimeNG(...)` (bizim `app.config.ts`'te zaten çağırdığımız fonksiyon) artık şunu yapıyor:

```js
const license = features?.map((f) => f.license).find(Boolean);
if (license) registerLicense({ primeui: license });
verifyLicense('primeui', { releaseDate: RELEASE_DATE }).then((result) => {
    PrimeNGConfig._setVerified(result.valid);
    if (!result.valid) {
        console.warn(`[PrimeUI] ${result.message}`);
        showInvalidLicenseBanner();
    }
});
```

Bizim `providePrimeNG({ theme: {...}, translation: trLocale })` çağrımızda `license` alanı **hiç yok** — bu yüzden `verifyLicense` geçersiz dönüyor ve `primeng-basecomponent.mjs`'deki **her PrimeNG component'inin** `ngAfterViewInit`'inde banner tetikleniyor (`if (this.config?.verified() === false) showInvalidLicenseBanner();`).

### `node_modules/primeng/LICENSE.md`'den (birebir alıntı)

> This package is part of **PrimeUI**, a family of commercial UI libraries by PrimeTek Informatics.
>
> **Community License (Free)** — Free for organizations that meet all of the following criteria:
> - Less than $1,000,000 USD in annual gross revenue
> - Fewer than 5 developers
> - Fewer than 10 employees
> - Less than $3,000,000 USD in venture capital or outside funding
>
> **Commercial License (Paid)** — For organizations that do not qualify for the Community License. Licensed per developer, perpetual, with one year of updates included.
>
> **A valid license key is required to use this software.**

### Bunun anlamı

- PrimeNG 21'de (ve öncesinde) kütüphane tamamen MIT/ücretsizdi. **PrimeNG 22 ile birlikte bu değişti** — artık "PrimeUI" adı altında ticari bir lisans modeline geçilmiş.
- STYS'in bu **Community License**'a uygun olup olmadığı (gelir, geliştirici sayısı, çalışan sayısı, dış yatırım kriterleri) **tamamen iş/organizasyonel bir sorudur — bu raporun veya bu agent'ın karar verebileceği bir konu değildir.**
- Uygun olsa bile, gerçek bir **lisans anahtarı** primeui.dev üzerinden **kayıt/başvuru** ile alınıp `providePrimeNG({ ..., license: 'ANAHTAR' })` şeklinde koda eklenmesi gerekiyor — bu adım da bu görevin (kod uyumluluk düzeltmesi) kapsamı dışında.
- **Bu banner, lisans kaydı yapılmadan production'a çıkılırsa her ekranda, her kullanıcıya görünür kalacaktır.** Bu, teknik bir hata değil, kasıtlı bir ticari lisans zorlama mekanizması.

### Neden bypass edilmedi

Bu bulguyu tespit eder etmez, **hiçbir kod değişikliği ile bu banner'ı gizlemeye/bypass etmeye çalışılmadı** (ör. CSS ile banner'ı gizlemek, `verifyLicense` çağrısını mock'lamak, sahte bir lisans anahtarı üretmeye çalışmak). Bunlar hem lisans şartlarını ihlal eder hem de bu agent'ın yetki alanı dışındadır — kullanıcının/organizasyonun açık onayı ve muhtemelen gerçek bir lisans satın alma/kayıt süreci gerektirir.

---

## 8. Risk Seviyesi

**Kod uyumluluğu açısından: Düşük-Orta** (tüm derleme hataları başarıyla, mekanik/izole düzeltmelerle giderildi, korunması istenen dosyalara dokunulmadı, backend/test/smoke sonuçları değişmedi).

**Genel (lisanslama dahil): YÜKSEK / ÇÖZÜLMEMİŞ.** Kod tarafı hazır olsa bile, **PrimeUI lisans durumu netleşmeden bu upgrade production'a alınamaz.**

---

## 9. Main'e Merge Öneriliyor mu?

**Hayır — kesinlikle önerilmiyor**, şu üç sebepten:

1. PrimeUI lisans durumu netleşmedi (Bölüm 7 — **birincil, engelleyici sebep**).
2. Bundle budget aşımı arttı (4.51 MB, önceden 4.22-4.23 MB) — ayrı bir performans değerlendirmesi gerektirir.
3. Rezervasyon ödeme dialogu, Fişe Git linki, ödeme iptal senaryoları gibi kritik iş akışları bu turda **tam smoke testten geçirilmedi** (lisans bulgusu nedeniyle çalışma erken durduruldu).

---

## 10. Merge İçin Ek İş Gerekiyor mu?

Evet, sırasıyla:

1. **(Karar — önce bu)** Organizasyonun PrimeUI Community License kriterlerine uyup uymadığının değerlendirilmesi; uymuyorsa Commercial License satın alma kararı.
2. Uygun lisans türüne göre primeui.dev üzerinden lisans anahtarı alınması ve `app.config.ts`'teki `providePrimeNG({...})` çağrısına eklenmesi (küçük, tek satırlık bir kod değişikliği — ama önce Adım 1 tamamlanmalı).
3. Lisans anahtarı eklendikten sonra "Invalid PrimeUI License" banner'ının gerçekten kaybolduğunun doğrulanması.
4. Rezervasyon ödeme dialogu, Fişe Git linki, ödeme iptal senaryoları (A-D), muhasebe fişi akışları dahil **tam smoke test turunun** tamamlanması.
5. Bundle budget artışının (1.43 MB → 1.71 MB aşım) ayrıca değerlendirilmesi.
6. Node.js sürüm yükseltmesinin (Bölüm 1) kalıcı hale getirilmesi — hangi mekanizmayla (nvm kalıcı `nvm use`, `engines` alanı, CI/CD güncellemesi) ayrıca karara bağlanmalı.

---

## Branch Commit Geçmişi (bu tur)

```
de4f9c3 wip: Angular 22 + PrimeNG 22 upgrade - build basariyla geciyor (ara adim)
ffc3a2c wip: ng update @angular/core@22 @angular/cli@22 (ara adim)
```

Bu commit'ler **main'e merge edilmemeli** — branch, lisans kararı netleşene kadar deneysel/beklemede kalmalı.
