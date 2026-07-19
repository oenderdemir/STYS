# Angular + PrimeNG 22 Upgrade Deneme Raporu

**Branch:** `upgrade/angular-primeng-22` (deneysel, `main`'den ayrı — main'e merge edilmedi, edilmeyecek)
**Taban:** `main` @ commit `3c75755` (Seçenek A tamamlanmış, `npm audit --omit=dev` 0 vulnerability, build temiz)
**Sonuç (özet):** Upgrade **ortam seviyesinde bloke oldu** (Node.js sürüm gereksinimi karşılanmıyor) — kod/paket seviyesinde bir kırılma tespit edilemedi çünkü build hiç çalıştırılamadı. Branch, deneme sonunda **paket değişiklikleri geri alınmış, sadece bu rapor eklenmiş** halde main baseline'ına eşit durumda.

---

## Branch Adı

`upgrade/angular-primeng-22` — `main`'den `git checkout -b` ile açıldı, `main` origin ile senkron haldeyken.

---

## Güncellenen Paketler

**Hiçbiri kalıcı olarak güncellenmedi.** Deneme sırasında aşağıdaki paketler geçici olarak yüklendi, build denemesi ortam seviyesinde bloke olunca **tamamen geri alındı** (`git checkout -- package.json package-lock.json` + `npm install`). Şu an branch, `package.json`/`package-lock.json` açısından `main` ile birebir aynı (Angular 21.2.18 / PrimeNG 21.1.9 / TypeScript 5.9.3).

Denenen hedef sürümler (geri alındı):

| Paket | Mevcut (main) | Denenen (22-branch, geri alındı) |
|---|---|---|
| `@angular/core` ve ailesi | 21.2.18 | 22.0.7 |
| `@angular/cli` | 21.2.19 | 22.0.7 |
| `@angular/cdk` | 21.2.14 | **22.0.5** (22.0.6/22.0.7 hiç yayınlanmamış — cdk'nin sürüm numaralandırması core'dan bağımsız ilerliyor) |
| `@angular-devkit/build-angular` | 21.2.19 | 22.0.7 |
| `primeng` | 21.1.9 | 22.0.0 |
| `@primeuix/themes` | 2.0.3 | 3.0.0 |
| `typescript` | 5.9.3 | 6.0.3 (denendi, kalıcı değil) |
| `primeicons` | 7.0.0 | değiştirilmedi (aşağıda açıklandığı gibi opsiyonel) |

---

## TypeScript Peer Dependency Sonucu

**Kritik düzeltme — önceki değerlendirme raporunun (`docs/angular-primeng-upgrade-degerlendirme-raporu.md`) varsayımı yanlıştı.** O raporda `npm view typescript version` "latest" etiketinin 7.0.2 olmasından yola çıkarak Angular 22'nin TS 7.x gerektirebileceği tahmin edilmişti. Gerçek peer dependency kontrolü bunu **doğrulamadı**:

```
npm view @angular/compiler-cli@22 peerDependencies
→ { typescript: '>=6.0 <6.1', '@angular/compiler': '22.0.x' }
```

**Angular 22, TypeScript `>=6.0 <6.1` istiyor — yani spesifik olarak 6.0.x hattını, 7.x'i DEĞİL.** `typescript@6.0.3` npm'de gerçek, kararlı (stable, `-dev`/`-rc` değil) bir sürüm olarak mevcut ve bu aralığı karşılıyor. TypeScript 7.x'e geçmek **hiç gerekli değil** — bu, kullanıcının "zorunlu değilse geçme" talimatına tam uyumlu: 5.9.3 → 6.0.3 tek bir minor-major sıçraması, 5.9→7.0 gibi iki büyük sürüm atlamak değil.

`typescript@6.0.3` deneme sırasında başarıyla kuruldu (sadece geçici ara-durum uyarısı: Angular 21'in `@angular-devkit/build-angular`'ı henüz güncellenmemişken `>=5.9 <6.0` aralığını istiyordu — beklenen, geçici).

---

## Build Sonucu

**Çalıştırılamadı.** `npm run build` (→ `ng build`) şu hatayla **hemen** durdu, hiçbir derleme adımına girmeden:

```
Node.js version v24.13.1 detected.
The Angular CLI requires a minimum Node.js version of v22.22.3 or v24.15.0 or v26.0.0.
Please update your Node.js version or visit https://nodejs.org/ for additional instructions.
```

Bu, `ng update` komutunun da ilk adımda aynı sebeple reddettiği, `@angular/core@22.0.7`'nin kendi `package.json` `engines.node` alanında da (`^22.22.3 || ^24.15.0 || >=26.0.0`) doğrulanan **sert bir gereksinim** — npm'in sadece uyarı verip geçtiği (`EBADENGINE` warning, `--legacy-peer-deps` ile paket kurulumu yine de tamamlandı) bir durum değil, **Angular CLI'nin kendi binary'sinin çalışma zamanında yaptığı, bypass edilemeyen bir kontrol**.

Ortamda Node.js **v24.13.1** kurulu; gereken aralıklardan hiçbirine girmiyor (`^22.22.3`, `^24.15.0`, `>=26.0.0`). Bu makinede `nvm`/`fnm`/`volta` gibi bir Node sürüm yöneticisi de yok — tek, sistem geneli bir Node.js kurulumu var (`C:\Program Files\nodejs`). Sistem Node.js'ini yükseltmek, bu repo/branch kapsamının dışında, makine genelini etkileyen bir altyapı kararıdır — **kullanıcı talimatı gereği ("zorlamadan raporla") bu adım atılmadı.**

**Sonuç: kod seviyesinde derleme hatası, TypeScript/PrimeNG API kırılması gözlemlenemedi — çünkü derleyici hiç çalışmadı.**

---

## Audit Sonucu

Denendiği anda (`--legacy-peer-deps` ile kurulum sonrası, build denemesinden önce): `5 vulnerabilities (1 low, 4 moderate)` — ama bu geçici, tutarsız bir ara-durumdu (henüz derlenmemiş, kısmen uyumsuz bir paket ağacı). Anlamlı bir "hedef sürümde audit sonucu" **elde edilemedi** çünkü paket ağacı asla stabil/çalışır bir duruma ulaşmadı.

Geri alma sonrası: `npm audit --omit=dev` → **`found 0 vulnerabilities`** (main baseline ile aynı, değişiklik yok).

---

## Kırılan Yerler

**Tespit edilemedi** — build hiç çalışmadığı için TypeScript derleyici hatası, PrimeNG import/component API hatası, theme/style hatası, template/layout hatası veya runtime route/auth hatası gözlemlenemedi. Bu bölümde raporlanacak somut bir kırılma **yok**; blocker tamamen ortam (Node.js sürümü) seviyesinde.

### Peer dependency analizinden çıkan, build ile doğrulanamamış gözlemler (bilgi amaçlı)

- **`primeng@22`, `primeicons` yerine `@primeicons/angular@^8.0.0`'a bağımlı hale gelmiş.** Bu, PrimeNG'nin **kendi iç component'lerinde** kullandığı YENİ, ek bir Angular icon-component paketi — projenin kendi `pi pi-xxx` CSS class kullanımını (topbar, menü, butonlar — binden fazla kullanım) **değiştirmeye zorlamıyor**. `primeicons` paketi (düz CSS/font, projenin doğrudan bağımlılığı) hâlâ ayrı, bağımsız yayınlanıyor (v8.0.0 dahil) ve API'si değişmemiş görünüyor. **PrimeIcons 8'e geçiş projemiz için opsiyonel, zorunlu değil.**
- **`primeng@22`'nin `dependencies` listesinde `@primeuix/themes` artık yok** (yerine `@primeuix/styles@^3.0.0` / `@primeuix/styled@^1.0.0` iç paketleri var). Ancak bu, v21'de de zaten böyleydi — `@primeuix/themes` primeng'in kendi bağımlılığı değil, bizim projemizin `app.config.ts`'te `providePrimeNG({ theme: { preset: Aura, ... } })` için ayrıca kurduğumuz bir pakettir. `@primeuix/themes@3.0.0`, kendi `@primeuix/styled` bağımlılığı üzerinden (`^1.0.0`) primeng@22'nin iç motoruyla **versiyon olarak hizalı** — doğru eşleşen sürüm bu görünüyor, ama gerçek "Aura preset hâlâ aynı şekilde import edilebiliyor mu" sorusu **build çalışmadığı için doğrulanamadı**.
- Bu iki gözlem de **spekülatif/teorik** kalıyor — gerçek derleme olmadan kesin kanıt sunulamaz.

---

## Yapılan Minimal Düzeltmeler

**Hiçbiri.** Build hiç çalışmadığından, düzeltilecek bir hata da ortaya çıkmadı. Kod tarafında **sıfır değişiklik** yapıldı.

---

## Playwright Smoke Sonucu

**Çalıştırılmadı.** `npm run build` başarısız (Node gate) olduğundan `npm start`/`ng serve` de aynı Node sürüm kontrolüyle karşılaşacaktı — smoke test aşamasına hiç geçilmedi.

---

## Template Custom Dosyalarına Dokunuldu mu?

**Hayır.** `app.topbar.ts`, `app.menu.ts`, `app.layout.ts`, `layout.service.ts` ve diğer tüm özelleştirilmiş dosyalar **hiç değiştirilmedi**. Zaten kod seviyesinde herhangi bir değişikliğe gerek kalmadı (build asla o aşamaya ulaşmadı).

---

## Risk Seviyesi

**Değerlendirilemez / Bilinmiyor (ortam blokajı nedeniyle).**

Peer dependency analizi tek başına **düşük-orta** bir risk profiline işaret ediyor (TS sıçraması öngörülenden çok daha küçük — 5.9→6.0.x, 5.9→7.0 değil; PrimeNG'nin component rename çalışması projede zaten Seçenek A öncesinde tamamlanmış; icon/theme paketleri görünürde geriye uyumlu). **Ancak bu tamamen teorik bir değerlendirmedir** — gerçek derleme, TypeScript tip kontrolü, PrimeNG component API'leri ve runtime davranışı **hiç test edilemedi**. Bu nedenle gerçek riski "düşük" olarak etiketlemek yanıltıcı olur; doğru etiket **"bilinmiyor, ortam engeli aşılmadan belirlenemez"**dir.

---

## Main'e Merge Öneriliyor mu?

**Hayır.** Bu branch zaten hiçbir kod/paket değişikliği içermiyor (tamamen geri alındı) — merge edilecek bir fonksiyonel değişiklik yok, sadece bu rapor commit edilecek.

---

## Merge İçin Ek İş Gerekiyor mu?

Evet — Angular 22 upgrade'inin **denenmeye devam edilebilmesi için**, bu repodan bağımsız bir ön koşul gerekiyor:

1. **Node.js sürümü `^22.22.3`, `^24.15.0` veya `>=26.0.0` aralığına yükseltilmeli.** Bu, geliştirme ortamı (ve muhtemelen CI/CD, prod deploy ortamı) için ayrı, bilinçli bir karar gerektiren, bu görevin kapsamı dışında bir altyapı adımıdır. Öneri: önce izole bir test ortamında (ör. bir Node sürüm yöneticisi — `nvm-windows`, `fnm` — kurularak, sistem varsayılanı değiştirilmeden) Node 24.15+ ile bu branch tekrar denenmeli.
2. Node sürümü uygun hale geldikten sonra, bu raporun "Kırılan Yerler" bölümü **gerçek `npm run build` çıktısıyla yeniden doldurulmalı** — o zaman TypeScript derleyici hataları, PrimeNG API farkları, ve varsa theme/stil kırılmaları somut olarak görülebilecek.
3. O noktada, kullanıcının belirttiği minimal-düzeltme kurallarına (p-select/p-datepicker'a dokunma, DynamicDialog/DialogService davranışını bozma, template CSS'i yeniden yazma, menü/permission yapısına dokunma, zoneless/signal mimarisini değiştirme) uyularak asıl "Seçenek B" denemesi yapılabilir.

---

## Kalan Sorular / Öneri

- Node.js sürüm yükseltmesi **kim tarafından, ne zaman, hangi ortamlarda (sadece dev makine mi, CI/CD mi, prod deploy sunucusu mu)** yapılacak — bu bir onay/planlama kararı gerektiriyor, bu raporun kapsamında **verilmedi**.
- Node yükseltmesi onaylanırsa, bu branch (`upgrade/angular-primeng-22`) tekrar kullanılarak deneme kaldığı yerden (paket kurulumu + `npm run build`) devam ettirilebilir — branch şu an temiz/geri alınmış halde bekliyor.
- Alternatif olarak, Node yükseltmesi yapılana kadar **Seçenek A baseline'ında kalınması** öneriliyor (zaten `main`'de stabil, 0 vulnerability, build temiz).
