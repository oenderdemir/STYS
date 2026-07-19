# Angular + PrimeNG Upgrade Değerlendirme Raporu

**Tarih:** 2026-07-19
**Kapsam:** Sadece analiz — kod değişikliği, paket yükseltmesi veya `npm install/update` yapılmamıştır.
**Yöntem:** `package.json`/`package-lock.json`/`node_modules` içeriği, `angular.json`, `tsconfig*.json`, layout/routing/auth kaynak kodu, `git log`/`git diff` ile Sakai taban commit'ine (`51760a1`) karşı fark analizi, `npm outdated` ve `npm view` (yalnızca bilgi amaçlı, install yapılmadı), `npm run build` (mevcut `node_modules` ile, install yapılmadan).

---

## 1. Mevcut Sürüm Durumu

| Bileşen | Mevcut sürüm (yüklü) | package.json aralığı |
|---|---|---|
| Angular (`@angular/core`) | **21.0.6** | `^21` |
| Angular CLI | **21.2.7** | `^21` |
| Angular CDK | **21.2.14** | `^21.2.14` (core'dan ileride — dikkat) |
| TypeScript | **5.9.3** | `~5.9.3` |
| Node.js (bu ortamda) | **24.13.1** | belirtilmemiş (`engines` alanı yok) |
| npm | 11.8.0 | — |
| PrimeNG | **21.0.2** | `^21.0.2` |
| PrimeIcons | **7.0.0** | `^7.0.0` |
| PrimeFlex | **Kullanılmıyor** | paket listesinde yok |
| `@primeuix/themes` | 2.0.2 | `^2.0.0` |
| RxJS | 7.8.2 | `~7.8.0` |
| Zone.js | **Yok — proje zoneless** | `provideZonelessChangeDetection()` |
| Tailwind CSS | 4.1.18 | `^4.1.11` (+ `tailwindcss-primeui` eklentisi) |

**Template/Sakai kaynağı:** Proje, resmi **PrimeNG Sakai (Angular 21)** starter'ından türetilmiş. Bunu `git log` üzerinden doğruladım: `51760a1 "projeye sakai 21 eklendi."` commit'i, orijinal Sakai iskeletini (angular.json, package.json, layout/, tüm demo sayfaları) tek seferde ekliyor. Sonraki tüm commit'ler bunun üzerine inşa edilmiş.

**Mimari yapı:**
- **Tamamen standalone** — `grep -rl "NgModule" src/app` → **0 sonuç**. Hiçbir NgModule kalıntısı yok.
- **Zoneless** — `zone.js` package.json'da yok, `app.config.ts` içinde `provideZonelessChangeDetection()` açıkça kullanılıyor. Bu, projenin zaten Angular'ın en yeni mimari yönüne (signals + zoneless) geçmiş olduğu anlamına geliyor.
- **Fonksiyonel interceptor/guard** — `authTokenInterceptor: HttpInterceptorFn`, `authGuard: CanActivateFn` — modern (class tabanlı değil) API kullanılıyor.
- **Auth state signal tabanlı** — Talep edilen incelemede "BehaviorSubject auth state" aranmıştı; gerçekte `AuthService` **Angular `signal()`** kullanıyor (`isKurumAdmin = signal(...)`, `aktifKurumId = signal(...)` vb.), `BehaviorSubject` değil. Bu, mevcut tasarımın zaten hedef mimariye (signals) uygun olduğu anlamına gelir — bu konuda bir düzeltme notu.
- **Route yapısı** — Route dosyasında (`app.routes.ts`) çoğu sayfa **eager** import ediliyor (üstte `import { X } from './app/pages/...'`); sadece 3 grup (`uikit-routes`, `pages-routes`, `auth-routes`) `loadChildren`/`loadComponent` ile lazy. Bu, bundle-budget uyarısının kök nedenlerinden biri (aşağıda ayrıca ele alınıyor) ama upgrade riskiyle ilgisi yok.
- **Builder** — `angular.json` zaten `@angular-devkit/build-angular:application` (esbuild tabanlı yeni builder) kullanıyor. Eski `browser`/Webpack builder'dan geçiş **zaten yapılmış**.

**Build komutu:** `ng build` (→ `npm run build`), production config varsayılan (`defaultConfiguration: "production"`).

**Mevcut `npm run build` sonucu** (bu oturumda, `node_modules` zaten mevcut olduğundan install yapılmadan çalıştırıldı):
```
Application bundle generation complete. [22.163 saniye]
Initial total: 4.22 MB (raw) / 670.75 kB (transfer)
▲ [WARNING] bundle initial exceeded maximum budget. Budget 2.80 MB was not met by 1.42 MB with a total of 4.22 MB.
```
- **Hata: 0**
- **Warning: 1** (bundle budget — bilinen, blocker değil)
- TypeScript strict warning: **yok**
- Angular template warning: **yok**

---

## 2. Hedef Sürüm Durumu

`npm view <paket> version` ile toplanan **güncel npm sürümleri** (hiçbiri yüklenmedi):

| Paket | Mevcut (yüklü) | npm "Wanted" (aynı major içinde) | npm "Latest" | Major fark |
|---|---|---|---|---|
| `@angular/core` | 21.0.6 | 21.2.18 | **22.0.7 (cdk: 22.0.5)** | ✅ Evet (21→22) |
| `@angular/cli` | 21.2.7 | 21.2.19 | **22.0.7** | ✅ Evet |
| `primeng` | 21.0.2 | 21.1.9 | **22.0.0** | ✅ Evet |
| `primeicons` | 7.0.0 | 7.0.0 | **8.0.0** | ✅ Evet |
| `primeflex` | — (kullanılmıyor) | — | 4.0.0 | İlgisiz (proje Tailwind kullanıyor) |
| `@primeuix/themes` | 2.0.2 | 2.0.3 | **3.0.0** | ✅ Evet |
| `typescript` | 5.9.3 | 5.9.3 | **7.0.2** | ✅ Evet (büyük atlama, 6.x satırı atlanmış) |
| `rxjs` | 7.8.2 | 7.8.2 | 7.8.2 | ❌ Fark yok |
| `@microsoft/signalr` | 9.0.6 | 9.0.6 | 10.0.0 | ✅ Evet (frontend dışı etkisi sınırlı) |

**Uyum matrisi değerlendirmesi:**
- **Angular 22 ↔ TypeScript:** Angular'ın geleneksel politikası her major sürümde desteklenen TS aralığını daraltıp yükseltmektir. TypeScript 5.9.3 → 7.0.2 arası **iki majör atlama** (TS duyurduğu yeni versiyonlama şemasıyla 6.x'i atlayıp doğrudan 7'ye geçti — bu, Go tabanlı yeni derleyici (`tsgo`) geçişiyle ilişkili büyük bir sürüm). Angular 22'nin resmi olarak hangi TS aralığını desteklediği (`peerDependencies`) **install yapmadan kesin doğrulanamadı** — bu, upgrade öncesi mutlaka Angular resmi "update guide" (`angular.dev/update-guide`) üzerinden doğrulanması gereken bir nokta.
- **PrimeNG 22 ↔ Angular 22:** PrimeNG major sürümleri genelde aynı yıl/döngüde karşılık gelen Angular major'ıyla hizalı çıkar (PrimeNG 21 ↔ Angular 21 gibi). PrimeNG 22'nin Angular 22 ile eşleştiği varsayımı makul ama **peerDependencies kontrolü yapılmadan kesinleştirilmemeli**.
- **Node sürümü:** Bu ortamda Node 24.13.1 çalışıyor — hem Angular 21 hem de öngörülen Angular 22 gereksinimleri için **fazlasıyla yeterli** (Angular son sürümler genelde Node 20.19+/22.12+ ister).
- **RxJS:** Zaten hedef sürümde, herhangi bir upgrade gerektirmiyor.
- **PrimeFlex:** Projede hiç kullanılmıyor (Tailwind + `tailwindcss-primeui` tercih edilmiş) — bu nedenle PrimeFlex'in versiyon durumu **bu proje için tamamen ilgisiz**.

**Genel değerlendirme:** Hem Angular hem PrimeNG için **tam bir major sürüm farkı** var (21→22), ama önce **aynı major (21.x) içinde patch/minor güncellemeler** de mevcut (Angular 21.0.6→21.2.18, PrimeNG 21.0.2→21.1.9) — bunlar risk açısından 22'ye geçişten çok daha düşük risklidir ve ayrı, daha erken bir adım olarak değerlendirilebilir.

---

## 3. Angular Upgrade Risk Analizi

- **Doğrudan geçiş mümkün mü?** Angular'ın resmi desteklenen yolu tek majör atlamadır (21→22 doğrudan desteklenir, "iki majör birden atlama" resmi olarak desteklenmez). Şu an proje 21'de olduğu için **21→22 tek adımlık `ng update` çağrısı** teorik olarak yeterli olmalı; ara majöre gerek yok.
- **`ng update` sırası (önerilen, resmi Angular pratiğine göre):**
  1. `ng update @angular/core@22 @angular/cli@22` (Angular CLI schematics otomatik migration'ları uygular)
  2. `ng update @angular/cdk@22` (CDK'yı core ile hizala — şu an CDK zaten 21.2.14 ile core'un 21.0.6'sının ilerisinde, versiyon uyumsuzluğu potansiyeli mevcut, bu yüzden bu adım özellikle önemli)
  3. TypeScript'i Angular 22'nin desteklediği aralığa çek (muhtemelen `ng update` bunu otomatik günceller, ama TS 7.x'e atlama manuel doğrulama ister)
  4. `ng update primeng@22` (PrimeNG kendi migration schematic'lerini varsa çalıştırır)
  5. `primeicons@8`, `@primeuix/themes@3` güncellemeleri
- **TypeScript uyumsuzluğu riski:** **Orta-Yüksek.** 5.9→7.0 atlaması, sadece Angular değil TS derleyicisinin kendisinin de büyük bir sürüm politikası değişikliği içeriyor. Angular Compiler CLI (`@angular/compiler-cli`) bu yeni TS sürümüyle test edilmiş olmalı ama bağımsız doğrulama (peerDependency kontrolü) yapılmadan riski "düşük" diye işaretlemek erken olur.
- **RxJS uyumsuzluğu:** **Yok** — zaten hedef sürümde.
- **Zone.js uyumsuzluğu:** **Yok/İlgisiz** — proje zaten zoneless, zone.js hiç kullanılmıyor. Bu aslında upgrade'i **kolaylaştıran** bir faktör: zoneless→zoneless geçişte kırılma riski daha düşük, çünkü zone.js'in kendi geriye dönük uyumluluk sorunlarıyla hiç uğraşılmıyor.
- **Standalone yapı kırılma riski:** **Düşük.** Proje zaten %100 standalone; Angular'ın gelecekteki majörlerinde NgModule desteğinin kaldırılması gibi bir senaryo bu projeyi etkilemez (zaten hiç NgModule yok).
- **Router API değişiklikleri:** `provideRouter`, `withInMemoryScrolling`, `withEnabledBlockingInitialNavigation` gibi fonksiyonel API'ler kullanılıyor — bunlar modern router API'sinin parçası, Angular 22'de kaldırılma riski düşük ama `withEnabledBlockingInitialNavigation`'ın olası deprecate durumu **update guide'da özellikle kontrol edilmeli** (bu API bazı Angular sürümlerinde "gereksiz hale geldi" uyarısı almıştı; zoneless mimaride davranışı farklılaşabilir).
- **HTTP interceptor yapısı:** Fonksiyonel (`HttpInterceptorFn`) — modern API, Angular 22'de kırılma riski **düşük**.
- **Auth guard / permission guard:** Fonksiyonel (`CanActivateFn`, `CanActivateChildFn`) — modern API, risk **düşük**.
- **Environment/`env.js` yapısı:** `window.__env` runtime config deseni Angular'ın build sistemine bağlı değil (saf runtime JS dosyası, `index.html`'de script tag ile yükleniyor olmalı) — Angular sürüm yükseltmesinden **etkilenmez**.
- **Build optimizer / esbuild / application builder geçiş riski:** **Yok** — proje zaten yeni `application` builder'ı kullanıyor, bu geçiş **daha önce yapılmış**. `angular.json` builder değişikliği gerekmiyor.
- **Genel Angular tarafı risk seviyesi: Orta.** Mimari olarak proje zaten çok modern (standalone + zoneless + esbuild + fonksiyonel API'ler) olduğundan, tipik "Angular 12'den 17'ye geçiş" tarzı büyük mimari risklerin **çoğu zaten yok**. Asıl risk, TypeScript'in 5.9→7.0 sıçramasında ve CDK/core versiyon hizalamasında yoğunlaşıyor.

---

## 4. PrimeNG Upgrade Risk Analizi

**Projede kullanılan PrimeNG component'leri** (import sayısına göre, `primeng/*` alt paketlerinden):

| Component | Kullanım sayısı (import) | Risk seviyesi |
|---|---|---|
| Button | 139 | Düşük |
| (primeng/api — MessageService, MenuItem vb.) | 101 | Düşük |
| Toast | 90 | Düşük |
| InputText | 89 | Düşük |
| **Select** (`primeng/select`) | 85 | Düşük — **zaten yeni isimle** |
| Table | 83 | **Orta** (lazy-load, en kritik component) |
| Toolbar | 66 | Düşük |
| Dialog | 63 | **Orta** (rezervasyon ödeme dialogu dahil) |
| Tag | 60 | Düşük |
| ConfirmDialog | 45 | Düşük-Orta |
| Checkbox | 36 | Düşük |
| **DatePicker** (`primeng/datepicker`) | 33 | Düşük — **zaten yeni isimle** |
| InputNumber | 32 | Düşük |
| IconField/InputIcon | 26+26 | Düşük |
| Card | 18 | Düşük |
| MultiSelect | 15 | Düşük |
| Tooltip | 13 | Düşük |
| ToggleSwitch | 13 | Düşük |
| Textarea | 10 | Düşük |
| Tabs, Menu, Divider | 8 civarı | Düşük |
| TreeTable | 5 | Orta (az kullanım ama karmaşık) |
| Chart | 5 | Orta (chart.js entegrasyonu, tema senkronu) |
| DynamicDialog (`primeng/dynamicdialog`) | 3 | Orta — **sadece 3 dosyada** (`stok-hareketleri`, `tasinir-fis-taslagi` ekranları) |
| Tree | 1 | Düşük |
| **Popover** (`primeng/popover`) | 2 | Düşük — **zaten yeni isimle** |
| Diğer 30+ component | 1-4 arası | Düşük |

**Önemli düzeltme — rename/deprecated durumu:** Görev tanımında "p-calendar/p-dropdown rename riski" özellikle sorulmuştu. İnceleme sonucu: **proje bu geçişi zaten yapmış.**
- `primeng/dropdown` **değil**, `primeng/select` kullanılıyor (85 yerde) → PrimeNG v18+ rename'i zaten uygulanmış.
- `primeng/calendar` **değil**, `primeng/datepicker` kullanılıyor (33 yerde) → aynı şekilde zaten güncel.
- `primeng/overlaypanel` **değil**, `primeng/popover` kullanılıyor → aynı şekilde güncel.

Bu, PrimeNG 21→22 geçişinde **en büyük kırılma riskini büyük ölçüde ortadan kaldırıyor**, çünkü tipik olarak component rename/deprecation acısı büyük major atlamalarda (ör. v17'den v19'a) yaşanır; proje zaten en güncel isimlendirmede.

**DynamicDialog / DialogService:** Sadece 2 muhasebe ekranında (`tasinir-fis-taslagi`, `stok-hareketleri`) kullanılıyor. Görev tanımında bahsedilen **"farklı cari seçme" ve "hızlı cari kart oluşturma" dialogları DynamicDialog DEĞİL — sıradan `p-dialog`** (statik template içinde, `[(visible)]` binding ile) kullanıyor. Bu, o akışlar için upgrade riskini **azaltıyor** (DialogService'in provider/injection API'sinde olası değişiklikler bu akışları etkilemez).

**p-table lazy load:** 30 farklı sayfada `[lazy]="true"`/`(onLazyLoad)` deseni kullanılıyor — bu, PrimeNG table API'sinde en **geniş yüzey alanına sahip** entegrasyon noktası. `LazyLoadEvent` tipi veya `onLazyLoad` event imzasında bir değişiklik olursa **30 dosyayı aynı anda etkiler**. Upgrade sonrası regresyon testinde en yüksek öncelik burada olmalı.

**Theme import yapısı:** `@primeuix/themes/aura` (yeni "Theming API" / design-token tabanlı sistem, `providePrimeNG({ theme: { preset: Aura, ... } })`). Bu, PrimeNG v18+'ın **zaten en yeni** tema mimarisi — eski `primeng/resources/themes/*.css` import deseni **hiç kullanılmıyor**. `@primeuix/themes` 2.x→3.x major atlaması PrimeNG 22 ile birlikte gelecek en riskli tema tarafı değişiklik olabilir, ama proje zaten token-tabanlı CSS custom property'lere (`var(--surface-card)`, `var(--primary-color)` vb.) dayandığından, olası kırılma büyük ihtimalle **token isim değişiklikleri** ile sınırlı kalır (yapısal bir yeniden yazım değil).

**Custom SCSS etkisi:** `src/assets/styles.scss` içindeki proje-özel stiller (`.satis-belgesi-dialog` altındaki geniş kural seti) doğrudan PrimeNG CSS class'larına (`.p-dialog-content`, `.p-select`, `.p-datepicker`, `.p-inputnumber`, `.p-textarea`, `.p-datatable-thead`) bağımlı. Bu class isimleri PrimeNG v18+'ta zaten stabilize olmuş isimler (BEM benzeri `p-component-part` deseni) — 21→22 geçişinde bu isimlerin **tekrar değişmesi düşük ihtimal**, ama upgrade sonrası görsel smoke test bu dosyanın etkilediği `satis-belgesi-dialog` ekranını mutlaka kapsamalı.

**Reactive Forms entegrasyonu:** İncelenen dosyalarda PrimeNG form component'leri (`p-select`, `p-datepicker`, `p-inputnumber` vb.) genelde `[(ngModel)]` veya düz property binding ile kullanılıyor; ayrı bir "Reactive Forms `ControlValueAccessor` uyumsuzluğu" riski taşıyan derin bir `formGroup`/`formControlName` kullanım deseni bu incelemede özel bir kırılma noktası olarak öne çıkmadı.

---

## 5. Template ve Layout Özelleştirmeleri

Sakai taban commit'i (`51760a1`) ile mevcut `HEAD` arasında `frontend/src/app/layout/` altında `git diff --stat` çalıştırıldı:

| Dosya | Durum | Değişim büyüklüğü |
|---|---|---|
| `app.topbar.ts` | **Ağır özelleştirilmiş** | +1039 satır |
| `app.menu.ts` | **Ağır özelleştirilmiş** | ~353 satır değişti |
| `app.menuitem.ts` | Özelleştirilmiş | ~94 satır değişti |
| `layout.service.ts` | Özelleştirilmiş | ~45 satır değişti |
| `app.layout.ts` | Özelleştirilmiş | ~62 satır değişti |
| `app.footer.ts` | Hafif özelleştirilmiş | ~25 satır değişti |
| `app.breadcrumb.ts` / `.scss` | **Tamamen yeni** (Sakai tabanında yoktu) | +64 / +19 satır |
| `app.menu.scss` | **Tamamen yeni** | +97 satır |
| `app.menuitem.scss` | **Tamamen yeni** | +72 satır |
| `app.topbar.scss` | **Tamamen yeni** | +123 satır |
| `app.configurator.ts` | **Değişmemiş** | Sakai orijinali (tema seçici) |
| `app.floatingconfigurator.ts` | **Değişmemiş** | Sakai orijinali |
| `app.sidebar.ts` | **Değişmemiş** | Sakai orijinali |

**Tespit edilen özel davranışlar** (kod incelemesiyle doğrulandı):
- **`app.topbar.ts` (+1039 satır)** — En büyük özelleştirme. İçe aktarılan servisler: `AuthService`, `KurumService`/`KurumModel` (kurum/tenant logosu ve context gösterimi), `NotificationService` + SignalR bildirim modeli, `VersionService` (backend/frontend versiyon bilgisi gösterimi). Kullanıcı adı gösterimi, kurum seçimi, şifre değiştirme dialogu, bildirim popover'ı gibi tamamen custom UI parçaları burada.
- **`app.menu.ts`** — Sakai'nin statik `MENU_ITEMS` dizisi tamamen kaldırılmış; yerine **çalışma zamanında backend'den gelen, yetkiye göre filtrelenen** `MenuRuntimeService` (`core/menu/`) enjekte ediliyor. Ayrıca menü içi arama özelliği (`searchTerm`, `menuSearchResults`, Türkçe karakter normalize eden `normalizeText`) tamamen custom, Sakai'de yok.
- **`app.breadcrumb.ts`** — Sakai orijinalinde hiç yok; route/menü eşleşmesinden breadcrumb üreten proje-özel bir component.
- **`layout.service.ts`** — tema/sidebar durumu yönetimine ek olarak proje-özel state eklenmiş (tam diff detayına girilmedi ama +45 satırlık fark önemli boyutta).

**Menü/yetki entegrasyonu:** Menü item'ları statik değil; `MenuRuntimeService` backend'den (`core/menu/dto`, `core/menu/models`) gelen permission-aware menü ağacını `computed`/`signal` ile `app.menu.ts`'e besliyor. Bu, PrimeNG/Angular sürüm yükseltmesinden **bağımsız**, proje-özel bir mimari katman — upgrade bunu etkilemez, ama upgrade sonrası regresyon testinde "yetkiye göre menü gizleme" mutlaka kontrol edilmeli çünkü bu mekanizma PrimeNG'nin kendi menü component'ine değil, projenin kendi runtime servisine dayanıyor.

**Idle timeout / oturum zaman aşımı:** Görevde "idle timeout" olarak adlandırılan özellik kod tabanında `idle` anahtar kelimesiyle **bulunamadı** (bulunan 4 dosyadaki "Idle" eşleşmeleri yanlış pozitif — Türkçe "KategoriIdleri" yani "Kategori Id'leri" ifadesi). Gerçek mekanizma **`AuthService` içinde `inactivityTimeoutMs`/`inactivityTimeoutHandle` adıyla** implemente edilmiş: `window.__env.sessionInactivityTimeoutMs` (varsayılan 10 dakika) üzerinden `env.js`'ten okunuyor, saf `setTimeout`/`clearTimeout` ile çalışıyor, süre dolunca `logout({ reason: 'inactivity' })` çağırıyor. Bu, **PrimeNG/Angular'a hiç bağımlı olmayan saf TypeScript/DOM API'si** — upgrade riski **yok**, ama fonksiyonel regresyon testine dahil edilmeli.

**`env.js`/`appBasePath`:** `frontend/public/env.js` (build çıktısında `dist/sakai-ng/browser/env.js`) — runtime'da `window.__env` global'ini set eden, deploy-zamanı konfigüre edilen bir dosya (`apiBaseUrl`, `sessionInactivityTimeoutMs`, `appBasePath`, `environment`). Bu, Angular'ın build sistemine dahil değil, `index.html`'e ayrı `<script>` olarak yükleniyor olmalı — Angular/PrimeNG sürüm yükseltmesinden **tamamen bağımsız**.

**Sonuç — hangi strateji daha güvenli?**
- **Template'i tamamen yeni Sakai sürümüyle değiştirmek şu an için gereksiz ve yüksek riskli.** `app.topbar.ts`, `app.menu.ts` gibi dosyalardaki 1000+ satırlık özelleştirme, yeni bir Sakai sürümüne "merge" edilmeye çalışılırsa manuel, hataya açık bir yeniden entegrasyon süreci gerektirir.
- **Mevcut template korunup sadece Angular/PrimeNG paket sürümleri yükseltilmeli.** Sakai template dosyaları zaten proje kod tabanının bir parçası (npm paketi değil, kaynak dosya olarak kopyalanmış) — bu nedenle "yeni Sakai sürümü" diye ayrı bir paket güncellemesi zaten yok; sadece altındaki Angular/PrimeNG paketleri güncellenir, component API'leri (varsa) uyarlanır.
- **"Yeni template'i ayrı branch'te karşılaştırma" seçeneği (Seçenek C) sadece görsel/UX yenileme motivasyonuyla ayrı bir gelecek proje olarak ele alınmalı**, mevcut upgrade ihtiyacıyla **birleştirilmemeli**.

---

## 6. Kritik İş Akışları ve Riskler

| Akış | Mevcut implementasyon | Upgrade riski |
|---|---|---|
| Login/auth akışı | `AuthService` + `signal` state, fonksiyonel guard | **Düşük** — modern API zaten kullanılıyor |
| `authTokenInterceptor` | Fonksiyonel `HttpInterceptorFn`, 401→refresh→retry deseni | **Düşük** — fonksiyonel interceptor API'si stabil |
| Auth state (signal, BehaviorSubject değil) | `signal()` tabanlı | **Düşük** — zaten hedef mimaride |
| Idle/inactivity timeout | Saf `setTimeout`, `env.js` config | **Yok** (framework'e bağımlı değil) |
| `LayoutService` | Custom, tema/sidebar state | **Düşük-Orta** — PrimeNG configurator API'sinde değişiklik olursa etkilenebilir |
| Permission'a göre menü/aksiyon görünürlüğü | `MenuRuntimeService`, backend-driven, kendi mimarisi | **Düşük** — PrimeNG'ye bağımlı değil |
| DynamicDialog (cari/müşteri seçim) | Sadece 2 muhasebe ekranında (stok/tasinir), rezervasyon cari seçiminde **kullanılmıyor** | **Orta** (sınırlı dosya sayısı) |
| Rezervasyon ödeme dialogu | Düz `p-dialog`, `[(visible)]` | **Orta** — Dialog en yoğun kullanılan component'lerden biri (63 yerde) |
| Hızlı cari kart oluşturma | Nested `p-dialog`, reactive olmayan form binding | **Düşük-Orta** |
| Farklı cari seçimi | Aynı dialog akışı içinde, DynamicDialog değil | **Düşük** |
| Kasa/Banka/POS hesap seçimi | `p-select` (zaten yeni isimle) | **Düşük** |
| Aktif fişli ödeme iptal butonu disabled | Saf Angular template `[disabled]` binding + `pTooltip`, backend state'e dayalı | **Düşük** — PrimeNG'nin kendisi değil, uygulama mantığı |
| Fişe git linki | `routerLink` + `queryParams`, saf Angular Router | **Düşük** |
| Tahsilat/gider/muhasebe ekranları | Ağırlıklı `p-table` lazy-load + `p-dialog` | **Orta** |
| `p-table` lazy load (30 sayfa) | `[lazy]="true"`, `(onLazyLoad)` | **Orta-Yüksek** — en geniş yüzey alanı, tek bir API değişikliği 30 dosyayı etkileyebilir |
| Tarih formatı kullanılan formlar | `p-datepicker` (zaten yeni isim) + `dd.mm.yy` custom translation | **Düşük** |
| Dialog close/onClose akışları | `(onHide)`/`[(visible)]` deseni (native p-dialog, DynamicDialog değil) | **Düşük** |
| Toast/error mesajları | `MessageService`, `primeng/api` | **Düşük** — 90 yerde kullanılan stabil API |
| Backend `BaseException` mesaj gösterimi | `tryReadApiMessage` (proje-özel util, `core/api`) | **Yok** — PrimeNG/Angular'a bağımlı değil |

**En yüksek risk taşıyan iki nokta:** (1) `p-table` lazy-load deseni (30 dosya, tek noktadan kırılırsa geniş etki), (2) `p-dialog` genel davranışı (63 kullanım, rezervasyon ödeme akışı dahil). Bu ikisi, upgrade sonrası test planında en yüksek önceliği almalı.

---

## 6a. Build ve Test — Mevcut Durum (bu oturumda çalıştırıldı)

`node_modules` zaten mevcut olduğundan `npm install` **yapılmadı**, sadece `npm run build` çalıştırıldı:

- **Build sonucu:** ✅ Başarılı, 0 hata
- **Warning:** 1 — bundle budget (`4.22 MB` / `2.80 MB` bütçesi, bilinen ve blocker değil)
- **TypeScript strict warning:** Yok
- **Angular template warning:** Yok
- **Bundle budget warning:** Var (yukarıda), upgrade'den bağımsız, önceden var olan bir durum

---

## 7. Upgrade Seçenekleri

### Seçenek A — Düşük riskli (sadece patch/minor, mevcut major'da kal)
- Angular 21.0.6 → 21.2.18, PrimeNG 21.0.2 → 21.1.9, `@angular/cdk` hizalama, TypeScript 5.9.3'te kal.
- **Avantaj:** En düşük risk; CDK/core versiyon uyumsuzluğunu giderir; güncel güvenlik/hata düzeltmeleri alınır; template'e hiç dokunulmaz.
- **Risk:** Çok düşük — aynı major içi patch/minor sürümler genelde geriye dönük uyumludur.
- **Tahmini etki:** 1-2 saatlik doğrulama + `npm update` kapsamındaki paketler.
- **Geri dönüş planı:** `package-lock.json`'u önceki commit'e revert etmek yeterli.
- **Test ihtiyacı:** `npm run build` + mevcut smoke test seti (bu oturumda zaten defalarca çalıştırılan senaryolar).
- **Öneriliyor mu?** ✅ **Evet — ilk adım olarak önerilir.**

### Seçenek B — Kontrollü framework upgrade (Angular 21→22, PrimeNG 21→22)
- `ng update @angular/core@22 @angular/cli@22`, ardından `@angular/cdk@22`, TypeScript'in Angular 22 tarafından desteklenen sürümüne çekilmesi, `ng update primeng@22`, `primeicons@8`, `@primeuix/themes@3`.
- Mevcut Sakai/template dosyaları (`app.topbar.ts`, `app.menu.ts` vb.) **korunur**, sadece gerekli import/API düzeltmeleri yapılır.
- **Avantaj:** Güncel major sürüm desteği, uzun vadede sürdürülebilirlik, güvenlik yaması penceresi genişler.
- **Risk:** Orta — özellikle TypeScript 5.9→7.0 sıçraması ve `p-table`/`p-dialog` yoğun kullanım nedeniyle regresyon riski var. Ama proje zaten standalone+zoneless+esbuild+fonksiyonel-API olduğundan, tipik büyük Angular major atlamalarında görülen mimari kırılmaların **çoğu bu projede zaten yok**.
- **Tahmini etki:** Çok günlük bir iş; `ng update` migration schematic'lerinin çalıştırılması + manuel doğrulama + tam regresyon test turu gerekir.
- **Geri dönüş planı:** Ayrı branch üzerinde yapılmalı; sorun çıkarsa branch terk edilir, main etkilenmez.
- **Test ihtiyacı:** Bölüm 8'deki tam test planı.
- **Öneriliyor mu?** ✅ **Evet — ama Seçenek A'dan SONRA, ayrı bir branch'te, adım adım.**

### Seçenek C — Template refresh (yeni Sakai sürümü alınıp custom değişiklikler taşınır)
- **Avantaj:** Yeni Sakai sürümünün olası UX/tasarım iyileştirmelerinden yararlanma.
- **Risk:** **Yüksek** — `app.topbar.ts` (+1039 satır), `app.menu.ts` (+353 satır) gibi ağır özelleştirmelerin yeni bir taban dosyaya manuel taşınması gerekir; otomatik/güvenli bir "merge" yolu yok.
- **Tahmini etki:** Çok yüksek — haftalar mertebesinde, tam görsel regresyon testi şart.
- **Geri dönüş planı:** Ayrı branch zorunlu; ana geliştirme akışını kesinlikle etkilememeli.
- **Test ihtiyacı:** Tam görsel regresyon (Bölüm 8) + tüm kritik iş akışı regresyonu.
- **Öneriliyor mu?** ❌ **Şu an için önerilmiyor.** Şu anki ihtiyaç (güvenlik/patch güncellemeleri, framework güncelliği) Seçenek A+B ile karşılanabiliyor; template yenileme ayrı, iş değeri gerektiren bir karar olmalı (UX yenileme talebi gelirse ayrıca değerlendirilebilir).

---

## 7a. Önerilen Yol (Net Karar)

**Şimdi upgrade edilmeli mi?** Kısmen — **Seçenek A hemen, Seçenek B planlı ve ayrı branch'te**, Seçenek C şimdilik gündemde değil.

**Sıralama:**
1. **Seçenek A** (aynı major içi patch/minor + CDK hizalama) — düşük risk, hemen yapılabilir, ayrı `chore/` branch'inde.
2. Seçenek A stabilize olduktan sonra **Seçenek B** (Angular 22 + PrimeNG 22) — `upgrade/angular-primeng-vNext` branch'inde, adım adım (Bölüm 9'daki commit planı), tam test turu ile.
3. **Seçenek C** gündemde değil — ayrı bir ürün/tasarım kararı gerektirir, bu değerlendirmenin kapsamı dışında bırakılmalı.

**Neden hemen tam upgrade yapılmıyor:** TypeScript'in 5.9→7.0 büyük atlaması ve Angular/PrimeNG peer-dependency uyumunun `npm install` yapılmadan kesin doğrulanamaması nedeniyle, B seçeneği **install/update denemesi + Angular resmi update-guide çıktısının okunması** ile başlamalı — bu adım kod değişikliği gerektirmez ama bu değerlendirmenin ("kod değiştirme, paket yükseltme yapma") kapsamı dışında.

---

## 8. Test Planı (Upgrade Yapılırsa)

### Genel
- `npm run build` (production)
- `eslint.config.js` mevcut → `npx eslint .` çalıştırılmalı
- Smoke login (her rol: superadmin, kurum admin, resepsiyonist, muhasebeci)
- Sayfa yenileme (F5) sonrası auth/oturum kalıcılığı
- Route navigation (özellikle lazy route grupları: `uikit-routes`, `pages-routes`, `auth-routes`)
- Permission'a göre menü görünürlüğü (`MenuRuntimeService`)

### PrimeNG Component Smoke
- `p-table` lazy load (30 sayfadan en az 5-6 temsilci sayfa: rezervasyon listesi, muhasebe fişleri, tahsilat/ödeme belgeleri, cari kartlar, kullanıcı yönetimi)
- `p-dialog` (rezervasyon ödeme dialogu + iç içe "Yeni Cari Kart" dialogu)
- `DynamicDialog` (stok hareketleri, taşınır fiş taslağı ekranları)
- `p-select` (kasa/banka/POS seçimi, cari tipi vb.)
- `p-datepicker` (rezervasyon giriş/çıkış tarihleri, fiş tarihleri)
- `p-toast` (başarı/hata bildirimleri)
- `p-confirmDialog` (fiş onay/iptal, tahsilat kapama geri alma)
- `p-tree` / `p-treetable` (kullanım az ama mutlaka kontrol edilmeli)
- `p-accordion`
- `p-toolbar`
- `p-chart` (dashboard/rapor grafikleri)

### İş Akışı Smoke (bu oturumdaki önceki turlarda zaten uçtan uca doğrulanmış senaryolar — upgrade sonrası TEKRARLANMALI)
- Rezervasyon oluşturma
- Rezervasyon ödeme alma (Nakit/Havale-EFT/Kredi Kartı)
- Hızlı cari kart oluşturma (QuickCreate)
- Farklı cari seçme
- Kasa/Banka/POS seçimi
- Fişe git linki (muhasebe kullanıcı görür + doğru route'a gider; resepsiyonist görmez + backend 403)
- Aktif/Taslak/Onaylı fişli ödeme iptal butonu disabled + tooltip metni + backend 409
- İptal/Ters Kayıt fişli ödeme iptal edilebiliyor
- Muhasebe fişi ekranı (oluştur/onayla/iptal)
- Tahsilat/ödeme belgesi ekranı
- Satış belgesi ekranı

### Görsel Regresyon
- Login sayfası
- Ana layout (sidebar açık/kapalı, dark/light tema)
- Sidebar (menü arama dahil)
- Topbar (kurum logosu, kullanıcı menüsü, bildirimler)
- Menü (yetkiye göre filtrelenmiş hali, en az 2 farklı rol için)
- Mobil görünüm (responsive breakpoint'ler)
- Dialoglar (rezervasyon ödeme, hızlı cari, muhasebe fiş onay/iptal confirm dialog)
- Tablolar (`satis-belgesi-dialog` özel stilleri dahil, çünkü bu dosya PrimeNG class isimlerine sıkı bağımlı)
- Formlar (satış belgesi form grid'leri gibi CSS grid + PrimeNG input kombinasyonları)

---

## 9. Önerilen Branch/Commit Planı

**Doğrudan `main` üzerinde çalışılmamalı.**

**Adım 1 — Sadece rapor commit'i:**
- Branch: `chore/angular-primeng-upgrade-assessment`
- Commit: `docs: Angular PrimeNG upgrade degerlendirme raporu eklendi`
- İçerik: bu rapor dosyası (`docs/angular-primeng-upgrade-degerlendirme-raporu.md`), kod değişikliği yok.

**Adım 2 — Upgrade yapılacaksa (ayrı branch, `upgrade/angular-primeng-vNext`), önerilen commit sırası:**
1. Angular core/cli update (+ CDK hizalama)
2. TypeScript/RxJS/Zone uyumluluk düzeltmeleri (RxJS zaten güncel, Zone.js zaten yok — büyük ihtimalle sadece TypeScript)
3. PrimeNG/PrimeIcons/`@primeuix/themes` update
4. Template/layout düzeltmeleri (varsa — `app.topbar.ts`, `app.menu.ts` gibi ağır özelleştirilmiş dosyalarda API uyarlaması gerekirse)
5. Component breaking change düzeltmeleri (varsa — özellikle `p-table` lazy-load ve `p-dialog` kullanan dosyalarda)
6. Test/build düzeltmeleri (Karma/Jasmine, ESLint config güncellemeleri gerekirse)

Bu rapor, **şu an sadece Adım 1'i** (rapor commit'i) önermektedir; henüz hiçbir branch oluşturulmamış veya commit atılmamıştır — kullanıcı onayı bekleniyor.

---

## 9a. Kalan Sorular / Riskler (Onay Gereken Konular)

1. **TypeScript 5.9.3 → 7.0.2 uyumluluğu** — Angular 22'nin `peerDependencies`'i `npm install` yapılmadan kesin doğrulanamadı. Upgrade denemesi öncesi Angular resmi update-guide (`update.angular.dev`, sürüm 21→22 seçilerek) çıktısı okunmalı.
2. **`@angular/cdk` (21.2.14) zaten `@angular/core` (21.0.6)'nın ilerisinde** — bu, mevcut `^21` aralığındaki gevşek versiyon pinning'den kaynaklanıyor olabilir; Seçenek A'da bu hizalama öncelikli ele alınmalı, aksi halde ileride sessiz bir versiyon çakışmasına yol açabilir.
3. **`@primeuix/themes` 2.x→3.x major atlaması** PrimeNG 22 ile birlikte gelecek; bu paketin CHANGELOG'u (breaking change listesi) `npm install` yapılmadan bu ortamdan incelenemedi — upgrade denemesi sırasında ayrıca okunmalı.
4. **Bundle budget uyarısı** (4.22 MB / 2.80 MB) upgrade'den bağımsız, önceden var olan bir durum; upgrade sırasında bütçe daha da kötüleşirse (yeni paket sürümleri genelde biraz büyür) ayrıca değerlendirilmeli, ama bu raporun kapsamında "blocker" sayılmıyor.
5. **Görsel regresyon testi için otomatik bir araç (ör. Percy, Chromatic) projede yok** — Bölüm 8'deki görsel regresyon adımları şu an **manuel** yapılmak zorunda; upgrade kapsamı büyükse (Seçenek B/C) bu süreç için ekstra zaman ayrılmalı.
6. **Karar onayı bekleniyor:** Bu raporun `chore/angular-primeng-upgrade-assessment` branch'inde commit edilip edilmeyeceği, ve Seçenek A'nın (düşük riskli patch/minor güncelleme) ne zaman uygulamaya alınacağı kullanıcı onayı gerektiriyor.
