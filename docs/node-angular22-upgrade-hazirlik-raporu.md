# Node.js Upgrade Hazırlık Raporu — Angular 22 Denemesi İçin

**Kapsam:** Sadece altyapı planı ve tekrar deneme prosedürü. Kod/paket değişikliği yok.
**İlgili branch:** `upgrade/angular-primeng-22` (main'e merge edilmedi, commit `5472f1d`).
**İlgili önceki rapor:** `docs/angular-primeng-22-upgrade-deneme-raporu.md`.

---

## 1. Mevcut Blokaj

- **Mevcut Node sürümü:** `v24.13.1` (bu geliştirme ortamında, sistem geneli tek kurulum — `C:\Program Files\nodejs`).
- **Angular 22'nin istediği Node aralığı:** `^22.22.3 || ^24.15.0 || >=26.0.0` (`@angular/core@22.0.7` ve `@angular/cli@22.0.7`'nin `package.json` `engines.node` alanından doğrulandı).
- **Neden build/update başlamıyor?** Bu, npm'in `engines` alanı için verdiği bir **uyarı** değil — `ng` binary'sinin kendi içinde, herhangi bir komut (`update`, `build`, `serve` vb.) çalıştırılmadan **en başta** yaptığı sert bir sürüm kontrolü. `v24.13.1`, `^24.15.0` aralığına girmiyor (24.13.1 < 24.15.0), `^22.22.3` aralığına girmiyor (22.x değil), `>=26.0.0`'a girmiyor (26.x değil) — üç aralıktan hiçbiri karşılanmıyor, bu yüzden Angular CLI hiçbir işlem yapmadan hemen çıkıyor.
- **Kod seviyesinde kırılma gözlemlendi mi?** **Hayır.** Derleyici/CLI hiç çalışmadığı için TypeScript, PrimeNG component API'si, theme/stil veya runtime seviyesinde herhangi bir hata **gözlemlenemedi**. Bu blokaj tamamen ortam seviyesinde; kod tarafında bilinen bir sorun yok.

---

## 2. TypeScript Düzeltmesi

- Önceki değerlendirme raporunda (`docs/angular-primeng-upgrade-degerlendirme-raporu.md`), `npm view typescript version` "latest" etiketinin `7.0.2` olmasından yola çıkarak Angular 22'nin TypeScript 7.x gerektirebileceği **tahmin edilmişti**.
- Gerçek peer dependency kontrolü (`npm view @angular/compiler-cli@22 peerDependencies`) bu tahmini **düzeltti**: Angular 22, `typescript: '>=6.0 <6.1'` istiyor — yani **6.0.x hattını**, 7.x'i değil.
- `typescript@6.0.3`, npm'de gerçek, kararlı (stable) bir sürüm olarak mevcut ve bu aralığı tam karşılıyor; önceki denemede sorunsuz kuruldu.
- **Önerilen TS hedefi: `6.0.x`** (spesifik olarak, deneme anında mevcut en güncel patch — `6.0.3` veya sonrası).
- **TypeScript 7'ye geçilmeyecek.** Bu, hem gereksiz (Angular 22 istemiyor) hem de daha büyük, ayrı bir risk taşıyan bir sıçrama olurdu (5.9→7.0 iki majör atlama; 5.9→6.0.x tek bir majör).

---

## 3. Node Upgrade Seçenekleri

### Seçenek A — Sistem geneli Node 24.15.0+ kurulumu

- **Avantaj:** En basit yol; ek araç kurulumu gerektirmez; mevcut `C:\Program Files\nodejs` kurulumunun üzerine doğrudan güncelleme.
- **Risk:** **Bu makinedeki TÜM diğer Node/npm projelerini etkiler** — sadece STYS değil, sistemde çalışan başka Node tabanlı araçlar/projeler varsa (global npm paketleri, diğer repo'lar, CLI araçları) hepsi yeni Node sürümüyle çalışmak zorunda kalır. Geriye dönük uyumsuzluk riski bu projenin kapsamının dışına taşar.
- **Geri dönüş:** Eski Node sürümünü elle tekrar indirip kurmak gerekir; kolay değil, sürüm numarasını hatırlamak ve installer'ı tekrar bulmak gerekir. Node.js resmi kurulumu "sürüm değiştir" değil "üzerine kur/kaldır" mantığıyla çalışır — hızlı bir rollback mekanizması yoktur.
- **Diğer projelere etkisi:** **Yüksek.** Bu makinede STYS dışında Node projesi varsa (bu ortamda doğrulanamadı, ama tipik bir geliştirici makinesinde genelde birden fazla proje olur), hepsi etkilenir.

### Seçenek B — fnm ile proje bazlı Node yönetimi

- **Avantaj:** Sistem geneli Node sürümüne dokunmaz; her proje kendi dizininde ihtiyaç duyduğu Node sürümünü kullanır. Hızlı (Rust ile yazılmış), düşük overhead. Diğer projeler mevcut Node sürümlerinde kalmaya devam eder.
- **Risk:** Ek bir araç kurulumu ve geliştirici alışkanlığı gerektirir (`fnm use` çağrısını unutmak, terminal başlatıldığında shell entegrasyonunun otomatik devreye girmesi için `.bashrc`/PowerShell profile ayarı gerekir). Takım genelinde tutarlı kullanım için herkesin fnm kurması ve shell entegrasyonunu yapması gerekir.
- **Geri dönüş:** Çok kolay — `fnm use <eski-sürüm>` ile anında eski Node sürümüne dönülür, sistem Node'u hiç değişmediği için gerçek bir "rollback" bile gerekmez.
- **Windows kurulum notu:** `winget install Schniz.fnm` veya `choco install fnm` ile kurulabilir; PowerShell profiline (`$PROFILE`) `fnm env --use-on-cd | Out-String | Invoke-Expression` eklenmesi gerekir (dizine girildiğinde otomatik doğru Node sürümüne geçiş için). Git Bash kullanılıyorsa `~/.bashrc`'ye benzer bir `eval "$(fnm env)"` satırı eklenmeli.
- **`.node-version` dosyası önerisi:** Proje köküne (`frontend/.node-version` veya repo kökü) `24.15.0` (veya seçilen hedef sürüm) içeren tek satırlık bir dosya eklenmesi önerilir — fnm bu dosyayı otomatik okur, `fnm use` çağrısını dizine göre otomatikleştirir.

### Seçenek C — Volta ile proje bazlı pinleme

- **Avantaj:** `package.json` içine gömülü `"volta": { "node": "24.15.0" }` alanı ile Node sürümü **repo'nun kendisinde, versiyon kontrolü altında** pinlenir — `.node-version` gibi ayrı bir dosyaya gerek kalmaz, takım üyeleri repo'yu klonlayıp `volta install` yaptığında otomatik doğru sürümü alır. CI/CD sistemleri de (Volta destekliyorsa) aynı pin'i okuyabilir.
- **Risk:** Volta'nın kendi shim mekanizması (PATH'e kendi node/npm shim'lerini ekler) bazı özel araç zincirleriyle (ör. bu projede zaten kullanılan `dotnet`/`docker` gibi araçlarla PATH çakışması) nadir de olsa etkileşim sorunu yaratabilir; fnm'e göre daha "sihirli"/opak bir mekanizma.
- **`package.json` `engines`/`volta` alanı önerisi (örnek, uygulanmadı):**
  ```json
  "engines": {
    "node": ">=24.15.0 <25.0.0"
  },
  "volta": {
    "node": "24.15.0",
    "npm": "11.8.0"
  }
  ```

### Net Öneri

**Evet — sistem geneli yükseltme yerine proje bazlı `fnm` (Seçenek B) daha güvenli.** Gerekçe:
- Bu makine sadece STYS için kullanılmıyor olabilir; sistem geneli bir değişiklik, bu görevin onay kapsamının dışındaki projeleri riske atar.
- `fnm`, geri dönüşü en kolay ve en düşük yan-etkili seçenek — sistem Node'una hiç dokunmaz.
- Volta da makul bir alternatif ama shim mekanizması bu projenin zaten karmaşık olan araç zincirine (`.NET`, Docker, SQL Server container) bir katman daha ekliyor; fnm daha "şeffaf" (sadece PATH'e doğru node.exe'yi koyar).

**Bu proje için önerilen Node sürümü: `24.15.0`** (bkz. Bölüm 4 — gerekçe orada).

---

## 4. Önerilen Node Sürümü

**Node `24.15.0` (veya bu satırda yayınlanmış en güncel `24.x` patch) önerilir** — `22.22.3+` veya `26.0.0+` değil.

**Neden:**
- **Mevcut ortam zaten `v24.13.1`** — aynı major hat içinde (`24.x`) sadece bir patch/minor ileri gitmek, `22.x`'e geriye dönmekten veya `26.x`'e (henüz yeni, olgunlaşmamış bir major) atlamaktan çok daha düşük risklidir. Node 22.x'e "geri" gitmek anlamsız (zaten 24'te bulunuluyor); Node 26.x, bu raporun yazıldığı tarihte (proje bağlamında 2026 ortası) hâlâ yeni sayılabilecek bir major olup gereksiz ek risk taşır.
- Backend `.NET 10` hedeflediğinden ve genel proje tarihçesinde modern/güncel toolchain tercih edildiğinden (Angular 21→22, PrimeNG 21→22 denemesi zaten "en güncel kalma" motivasyonuyla yapılıyor), `24.x` LTS/current hattında kalmak tutarlı.
- `24.15.0`, Angular 22'nin izin verdiği en düşük `24.x` eşiği — mevcut sürüme (`24.13.1`) en yakın, en az sürtünmeli hedef.

---

## 5. Repo İçinde Sabitleme Önerisi

**Kod değişikliği yapılmadı, sadece öneri:**

- **`.node-version` eklenmeli mi?** Evet, önerilir — `fnm` (Seçenek B) tercih edilirse, repo köküne (veya `frontend/` altına, frontend-özel bir gereksinim olduğu için) `24.15.0` içeren bir `.node-version` dosyası eklenmesi, geliştiricilerin doğru Node sürümüne otomatik geçmesini sağlar.
- **`package.json` `engines` alanı eklenmeli mi?** Evet, önerilir — `frontend/package.json`'a şu an **hiç `engines` alanı yok** (doğrulandı); Angular 22'ye geçildiğinde `"engines": { "node": ">=24.15.0 <25.0.0" }` eklenmesi, yanlışlıkla eski Node ile `npm install`/`npm run build` çalıştırılmaya çalışıldığında en azından bir **uyarı** (varsayılan npm davranışı `engine-strict` kapalıyken sadece uyarı verir, build'i engellemez) üretir — tam bir garanti değil ama erken sinyal sağlar.
- **README/deploy docs'a Node sürümü yazılmalı mı?** Evet, önerilir — `docs/rezervasyon-muhasebe-deploy-runbook.md` ve varsa proje kök `README.md`'ye, deploy/geliştirme ortamı gereksinimleri listesine "Node.js 24.15.0+" notu eklenmesi, gelecekteki deploy'larda ve yeni geliştirici onboarding'inde bu gereksinimin gözden kaçmamasını sağlar. Bu, Angular 22 upgrade'i **fiilen tamamlandığında** yapılmalı — şu an (upgrade henüz gerçekleşmediği için) mevcut baseline (Angular 21) hâlâ eski Node sürümleriyle de çalıştığından, dokümana şimdiden yazmak yanıltıcı olur.

---

## 6. Angular 22 Denemesine Devam Prosedürü

Node upgrade (Seçenek B/fnm önerisiyle) yapıldıktan **sonra**, izlenecek adımlar:

### Ortam doğrulama

```bash
git checkout upgrade/angular-primeng-22
git reset --hard origin/main   # veya branch'in son temiz commiti (5472f1d)
node -v                         # 24.15.0+ olmalı
npm -v
npm ci
npm run build                   # Seçenek A baseline'ının hâlâ sorunsuz build ettiğini doğrula
```

### Upgrade denemesi

```bash
ng update @angular/core@22 @angular/cli@22
ng update @angular/cdk@22
npm install primeng@22 @primeuix/themes@3 primeicons@8 typescript@6.0.3
```

*Not: Önceki denemede `ng update`'in kendisi bloke olduğu için manuel `npm install` + `--legacy-peer-deps` yoluna gidilmişti; Node sürümü düzeldiğinde `ng update`'in resmi migration schematic'lerini çalıştırması beklenir — bu, manuel `npm install`'a göre daha güvenli olduğundan (otomatik kod migration'ları uygular), **önce `ng update` denenmeli**, sadece o da başarısız olursa manuel `npm install`'a geri dönülmeli.*

### Doğrulama

```bash
npm run build
npm audit --omit=dev
```

Ardından Playwright smoke (önceki Seçenek A denemesinde kullanılan aynı senaryo seti): login (admin+resepsiyonist), dashboard/layout/topbar/sidebar, menü permission, rezervasyon listesi `p-table` lazy-load, rezervasyon ödeme dialogu, `p-select`/`p-datepicker`, DynamicDialog aç/kapat, toast, Fişe Git linki, aktif fişli ödeme iptal butonu disabled, muhasebe fişleri `p-table`, resepsiyonist `/muhasebe/fisler` 403.

Son olarak: **sonuç raporu** — `docs/angular-primeng-22-upgrade-deneme-raporu.md`'nin bu ikinci (gerçek build çalıştırılmış) deneme sonucuyla güncellenmesi veya yeni bir rapor olarak eklenmesi.

---

## 7. Riskler

- **Node upgrade diğer lokal projeleri etkileyebilir** — bu, Seçenek A (sistem geneli) seçilirse gerçek bir risk; Seçenek B/C (proje bazlı) seçilirse bu risk **ortadan kalkar**.
- **CI/CD Node sürümü de güncellenmezse localde geçen build CI'da kalabilir** — bu repoda mevcut bir CI/CD pipeline tanımı (`.github/workflows`, `azure-pipelines.yml` vb.) **bulunamadı** (sadece `Dockerfile`'lar var); bu nedenle "CI'da hangi Node sürümü kullanılıyor" sorusu şu an **belirsiz** — Angular 22'ye fiilen geçilmeden önce, deploy/CI sürecinin Node sürümünün de netleştirilmesi/güncellenmesi gerekiyor.
- **Prod build ortamı Node 24.15+ değilse deploy pipeline kırılır** — `frontend/Dockerfile` içindeki base image'ın Node sürümü de bu upgrade ile birlikte güncellenmelidir (bu raporun kapsamında incelenmedi, ayrı bir kontrol gerektirir).
- **TypeScript 6 ile Angular compiler hataları çıkabilir** — 5.9→6.0.x geçişi küçük görünse de gerçek bir major sürüm atlaması; strict mode ayarları (`tsconfig.json`'da zaten `strict: true`, `strictTemplates: true` vb. aktif) altında yeni tip kontrolü davranışları derleme hatası üretebilir. Bu, Node sürümü düzeltilip gerçek build çalıştırılana kadar **bilinmiyor**.
- **PrimeNG 22 runtime/template farkları build sonrası ortaya çıkabilir** — önceki denemede tespit edilen `@primeicons/angular` ve `@primeuix/styles`/`@primeuix/styled` iç mimari değişiklikleri, gerçek build çalışmadan teorik kaldı; theme preset importunun (`@primeuix/themes/aura`) hâlâ aynı şekilde çalışıp çalışmadığı doğrulanamadı.

---

## 8. Karar Gerektiren Konular

- **Node sistem geneli mi yükseltilecek, yoksa proje bazlı mı (fnm/Volta) yönetilecek?** Bu raporun önerisi proje bazlı `fnm`, ama nihai karar kullanıcıya/ekibe ait.
- **fnm mi Volta mı kullanılacak?** Öneri fnm, ama takımın mevcut alışkanlıkları/tercihleri (varsa) dikkate alınmalı.
- **CI/CD Node sürümünü kim, ne zaman güncelleyecek?** Bu repoda mevcut bir CI/CD pipeline tanımı bulunamadığından, bu sorunun net bir sahibi/mekanizması şu an belirsiz — deploy sürecini yöneten ekiple ayrıca netleştirilmeli.
- **Angular 22 branch denemesi ne zaman tekrar açılacak?** Node upgrade kararı ve uygulaması tamamlanmadan bu adıma geçilmemeli; branch (`upgrade/angular-primeng-22`) şu an temiz/bekleme halinde, hazır olduğunda Bölüm 6'daki prosedürle devam ettirilebilir.
