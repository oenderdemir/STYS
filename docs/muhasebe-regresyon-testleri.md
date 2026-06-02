# Muhasebe Regresyon Testleri - Faz B-3

## Test Edilen Senaryolar
- Cari kart oluşturma ve açılış bakiyesi cari hareketi akışı.
- Cari kart banka hesapları ve yetkili kişiler detay/edit akışı.
- Satış/fatura belgesi satır parametreleri ve toplamlar.
- Belgeden muhasebe fişi oluşturma, duplicate engeli ve fiş onayı.
- Muhasebe fişi onay sonrası düzenleme kısıtı ve rapor görünürlüğü.
- Belge iptali, ters kayıt, stok/cari hareket iptali ve buton görünürlüğü.
- Cari hareket kapama koruması ve açılış bakiyesi düzeltme akışı.
- Yevmiye, muavin, mizan, cari bakiye ve export kontrolleri.
- Tesis / yetki scope ve hesaplar ekranı tesis filtresi.
- İndirim / ÖTV / ÖİV / Konaklama Vergisi alanlarının belge ve fiş etkisi.

## Bulunan Hatalar
- Kritik regresyon hatası bulunmadı.
- Ek vergi alanları belgede saklanıyor, ancak muhasebe fişi stratejileri bunları ayrı hesap satırı olarak kullanmıyor.

## Yapılan Düzeltmeler
- Düzeltme yapılmadı.

## Eksik Kalan İş Kuralları
- ÖTV / ÖİV / Konaklama Vergisi satır bilgileri belgede korunuyor; muhasebe fişi toplam etkisi için ayrı hesap satırı gereksinimi varsa sonraki faz gerekir.
- İndirim oranı/tutarı belge matrahını etkiliyor; mevcut iş kuralı oran/tutar tek satır için normalize edilerek devam ediyor.

## Backend
- Kod değişikliği yapılmadı; mevcut muhasebe fişi stratejileri yalnız ToplamMatrah, ToplamKdv ve tevkifat akışını kullanıyor.

## Frontend
- Kod değişikliği yapılmadı; satış belgesi satırında parametre blokları zaten mevcut.

## Migration
- Gerekmedi.

## Build
- `dotnet build backend/STYS.csproj` başarılı.
- `npm run build` başarılı.
- Mevcut uyarılar var, hata yok.

## Test
- Kod incelemesi ve build doğrulaması yapıldı.
- Manuel runtime test yapılmadı.

## Commit
- Doküman güncellendi; commit oluşturulacak.

---

## Faz C - Muhasebe Dönem Kapatma / Kilitleme Kontrolü

### Test Edilen Senaryolar
- Açık döneme fiş oluşturma.
- Kapalı döneme fiş oluşturma/güncelleme/onaylama.
- Kapalı döneme ait satış belgesi muhasebeleştirme.
- Kapalı döneme ters kayıt oluşturacak iptal.
- Açık dönemde aynı akışların çalışmaya devam etmesi.

### Tespit
- Fiş oluşturma ve satış belgesi muhasebeleştirme akışlarında açık dönem kontrolü mevcut.
- Muhasebe fişi create/update/onay/iptal akışları açık dönem olmadan ilerlemiyor.
- Dönem servisinde kapalı dönemde dönem güncelleme/silme kısıtları mevcut.

### Bulunan Hatalar
- Kritik dönem kilitleme hatası bulunmadı.

### Yapılan Düzeltmeler
- Düzeltme yapılmadı.

### Eksik Kalan İş Kuralları
- Ayrı stok/cari hareket oluşturma akışları için bağımsız dönem kontrolü ihtiyacı raporlanmadı; mevcut tasarım fiş üretim akışına bağlı çalışıyor.

### Backend
- Değişiklik yok.

### Frontend
- Değişiklik yok.

### Migration
- Gerekmedi.

### Build
- `dotnet build backend/STYS.csproj` başarılı.
- `npm run build` başarılı.

### Test
- Kod incelemesi ve build doğrulaması yapıldı.
- Manuel runtime test yapılmadı.

---

## Faz C-1 - Bağımsız Hareketlerde Dönem Kontrolü

### Test Edilen Senaryolar
- Cari hareket oluşturma/güncelleme.
- Stok hareketi oluşturma/güncelleme.
- Tahsilat/ödeme belgesi oluşturma/güncelleme/silme.
- Kasa hareketi oluşturma/güncelleme.
- Banka hareketi oluşturma/güncelleme.

### Tespit
- Bağımsız hareket servislerinde dönem kontrol boşluğu vardı.
- Kapalı dönem için `İşlem tarihi kapalı muhasebe dönemindedir.` mesajı ile merkezi guard eklendi.

### Bulunan Hatalar
- Kritik regresyon hatası bulunmadı; dönem kontrol boşluğu kapatıldı.

### Yapılan Düzeltmeler
- Cari, stok, tahsilat/ödeme, kasa ve banka hareketlerinde open-period guard eklendi.
- Kapalı dönemde güncelleme ve ilgili silme akışı engellendi.

### Eksik Kalan İş Kuralları
- Ayrı kasa/banka hareketi iptal akışları için ek ters kayıt kuralı gerekirse sonraki fazda ele alınmalı.

### Backend
- `MuhasebeDonemKontrolHelper` eklendi.
- İlgili hareket servisleri açık dönem kontrolü kullanır hale getirildi.

### Frontend
- Değişiklik yok.

### Migration
- Gerekmedi.

### Build
- `dotnet build backend/STYS.csproj` başarılı.
- `npm run build` çalıştırılmadı; frontend değişikliği yok.

### Test
- Kod incelemesi ve build doğrulaması yapıldı.
- Manuel runtime test yapılmadı.

### Commit
- Doküman güncellendi; commit oluşturulacak.

### Faz C-1 Notu
- Stok hareketinde varsayılan tarih ataması, dönem kontrolünden önce çalışacak şekilde düzeltildi.

---

## Faz D - Cari Kapama Geri Alma / Tahsilat-Ödeme İptal Akışı

### Test Edilen Senaryolar
- Tahsilat/ödeme ile kısmi kapama.
- Kapama geri alma.
- Tahsilat/ödeme iptali.
- Kapalı dönemde iptal engeli.
- Tam kapama geri alma.

### Tespit
- Kapama geri alma metodu vardı; iptal davranışı tek yerde toplanmadı.
- Tahsilat/ödeme silme akışı fiziksel delete yerine iptal davranışına çevrildi.
- İptal edilen kapama hareketinin aktif ilişki alanı temizlenmediği için rapor/ekstre yorumlarında yanlış pozitif risk vardı.

### Bulunan Hatalar
- Geri alma akışında açık dönem ve belge iptal kontrolü eksikti.
- Bazı hata mesajları istenen standart metinlerle uyumsuzdu.

### Yapılan Düzeltmeler
- Tahsilat/ödeme iptali güvenli akışa alındı.
- Kapama geri alma işlemine açık dönem ve ilişki doğrulaması eklendi.
- `CariHareketKapamaService.GeriAlAsync` içinde kapama hareketi `Durum=Iptal`, `KapananTutar=0`, `KalanTutar=0`, `KapandiMi=true`, `IliskiliCariHareketId=null` olacak şekilde temizleniyor.
- `/ui/muhasebe/tahsilat-odeme-belgeleri/{id}/iptal` endpointi eklendi.

### Eksik Kalan İş Kuralları
- UI’da ayrı iptal butonu yok; mevcut delete aksiyonu iptal davranışını kullanıyor.
- `HasCariHareketAsync` aktif hareketleri sayıyor; iptal edilmiş kapama hareketleri sorgu dışı kalıyor.

### Backend
- `CariHareketKapamaService` ve `TahsilatOdemeBelgesiService` güncellendi.
- `ITahsilatOdemeBelgesiService` iptal metodu eklendi.

### Frontend
- Değişiklik yok.

### Migration
- Gerekmedi.

### Build
- `dotnet build backend/STYS.csproj` başarılı.
- `npm run build` çalıştırılmadı; frontend değişikliği yok.

### Test
- Kod incelemesi ve backend build doğrulaması yapıldı.
- Manuel runtime test yapılmadı.

### Commit
- Bu fazda kod değişikliği yapıldı; commit oluşturulacak.

---

## Faz E-1 - Yevmiye / Muavin / Mizan Scope Doğrulaması

### Tespit
- Yevmiye, muavin ve mizan rapor akışlarında filtreler ve export davranışı mevcut.
- Faz E-1 kapsamı yevmiye/muavin/mizan tesis scope kontrolü ile sınırlıydı.
- Cari ekstre/bakiye, tahsilat-ödeme, kasa/banka/stok rapor kontrolleri Faz E-2'ye bırakıldı.

### İş Kuralı Kararı
- Raporlar yalnızca geçerli tesis ile ve mevcut access scope içinde çalışmalı.
- Muavin raporu, rapor tesisine uymayan hesapla oluşturulmamalı.
- Faz E-1 sadece yevmiye, muavin ve mizan scope doğrulamasını kapsar.

### Yapılan Değişiklikler
- Yevmiye, muavin, mizan ve hızlı mizan raporlarında tesis access scope kontrolü eklendi.
- Muavin raporunda seçilen muhasebe hesabı ile çalışma tesisi uyumu doğrulandı.
- Küçük rapor tasarımı değişikliği yapılmadı.

### Eksik Kalan İş Kuralları
- Cari ekstre/bakiye, tahsilat-ödeme, kasa/banka/stok rapor kontrolleri Faz E-2'de ele alınacak.
- Büyük export altyapısı değişikliği yapılmadı.
- Yeni muhasebe hesap planı tasarımı yapılmadı.
- Migration gerekmedi.

### Backend
- `MuhasebeFisService` içinde rapor tesis yetki kontrolü eklendi.

### Frontend
- Değişiklik yapılmadı.

### Migration
- Gerekmedi.

### Build
- `dotnet build backend/STYS.csproj` başarılı.
- `npm run build` çalıştırılmadı; frontend değişikliği yok.
- Build sırasında 1 warning var, hata yok.

### Test
- Kod incelemesi yapıldı.

### Commit
- `957bd7a` commit'i oluşturuldu ve pushlandı.

---

## Faz E-2 - Cari / Tahsilat / Kasa / Banka / Stok Rapor Scope Doğrulaması

### Tespit
- Cari ekstre/bakiye, tahsilat-ödeme, kasa, banka ve stok rapor akışlarında tesis scope ve aktif kayıt filtresi davranışı mevcut.
- Faz E-2 kapsamı bu raporların scope kontrolleri ile sınırlıydı.
- Kritik regresyon hatası bulunmadı.

### İş Kuralı Kararı
- Raporlar yalnızca kullanıcının erişebildiği tesis ve aktif kayıtlar üzerinden çalışmalı.
- Cari ekstre/bakiye, tahsilat-ödeme, kasa/banka ve stok özetleri tesis filtresi dışında sonuç üretmemeli.
- Faz E-2, yevmiye/muavin/mizan dışındaki bu rapor kontrollerini kapsar.

### Yapılan Değişiklikler
- Kod değişikliği yapılmadı; mevcut servis ve ekran davranışları dokümante edildi.
- Faz E-2 kapsam notu ve dışarıda bırakılan kontroller açık yazıldı.
- Faz E-1 ile karışmaması için bölüm başlığı ayrı tutuldu.

### Eksik Kalan İş Kuralları
- Büyük export altyapısı değişikliği yapılmadı.
- Yeni muhasebe hesap planı tasarımı yapılmadı.
- Faz E-1 kapsamı dışındaki raporların ek UI/servis refaktörü gerekirse sonraki fazda ele alınacak.

### Backend
- Değişiklik yapılmadı.

### Frontend
- Değişiklik yapılmadı.

### Migration
- Gerekmedi.

### Build
- `dotnet build backend/STYS.csproj` başarılı.
- `npm run build` çalıştırılmadı; frontend değişikliği yok.
- Build sırasında 1 warning var, hata yok.

### Test
- Kod incelemesi ve backend build doğrulaması yapıldı.
- Manuel runtime test yapılmadı.

### Commit
- Doküman güncellendi.
- Commit oluşturuldu: 5ab2f84

---

## Faz F - Tesis / Yetki Scope Genel Audit Taraması

### Tespit
- Muhasebe modülü genelinde liste, detay, lookup, create/update/delete/iptal ve rapor akışları tarandı.
- Kayıt seviyesinde yetki boşluğu bulunan yerler update/delete/iptal akışlarında toplandı.
- Kritik veri sızıntısı riski bulunan birkaç servis için mevcut kaydın tesis erişimi ayrıca zorunlu hale getirildi.

### İş Kuralı Kararı
- Kullanıcının scope'u varsa yalnız yetkili olduğu tesis kayıtlarına erişebilmeli.
- Mevcut kaydın tesis yetkisi doğrulanmadan update/delete/iptal yapılmamalı.
- Global referans kayıtlar yalnız gerçekten tesis bağımsız kabul edilen alanlarda serbest kalmalı.

### Yapılan Değişiklikler
- `CariHareketService`, `KasaHareketService`, `BankaHareketService` ve `StokHareketService` update/delete akışlarına mevcut kayıt scope kontrolü eklendi.
- `TahsilatOdemeBelgesiService` update ve iptal akışlarına mevcut kayıt scope kontrolü eklendi.
- `DepoService` ve `TasinirKartService` update/delete akışlarına mevcut kayıt scope kontrolü eklendi.
- `HesapService` update akışına mevcut kayıt scope kontrolü eklendi.
- `MuhasebeFisService` update/delete akışlarında fişin tesis yetkisi tekrar doğrulandı.

### Eksik Kalan İş Kuralları
- Tesis bağımsız global referans veriler için yeni bir ortak helper çıkarılmadı.
- `MuhasebeHesapPlanlari` gibi gerçekten global kabul edilen lookup alanlarına dokunulmadı.
- Büyük refactor yapılmadı; sadece eksik scope guard'ları kapatıldı.

### Backend
- backend/Muhasebe/CariHareketler/Services/CariHareketService.cs
- backend/Muhasebe/KasaHareketleri/Services/KasaHareketService.cs
- backend/Muhasebe/BankaHareketleri/Services/BankaHareketService.cs
- backend/Muhasebe/StokHareketleri/Services/StokHareketService.cs
- backend/Muhasebe/TahsilatOdemeBelgeleri/Services/TahsilatOdemeBelgesiService.cs
- backend/Muhasebe/Depolar/Services/DepoService.cs
- backend/Muhasebe/TasinirKartlari/Services/TasinirKartService.cs
- backend/Muhasebe/Hesaplar/Services/HesapService.cs
- backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs

### Frontend
- Değişiklik yapılmadı.

### Migration
- Gerekmedi.

### Build
- `dotnet build backend/STYS.csproj` başarılı.
- `npm run build` çalıştırılmadı; frontend değişikliği yok.
- Build sırasında 1 warning var, hata yok.

### Test
- Kod incelemesi ve backend build doğrulaması yapıldı.
- Manuel runtime test yapılmadı.

### Commit
- Doküman güncellendi.
- Commit oluşturuldu: 9f062b0

---

## Faz G - Muhasebe UI/UX Aksiyon İsimleri ve Kritik İşlem Onayları

### Tespit
- Tahsilat/ödeme ekranında sil aksiyonu iptal davranışıyla eşleşmiyordu.
- Satış belgesi iptal akışı ve muhasebe fişi taslak/iptal aksiyonları doğru bulundu.
- Cari, stok, kasa ve banka hareketlerinde fiziksel silme davranışıyla uyumlu sil butonları korunabildi.
- Tahsilat/ödeme sayfasında default `p-confirmDialog` eksikti; eklendi.

### İş Kuralı Kararı
- İptal/ters kayıt yapan aksiyonlar kullanıcıya "Sil" olarak gösterilmemeli.
- Fiziksel silme ile işlemsel iptal birbirinden ayrılmalı.
- İptal edilmiş kayıtlarda iptal aksiyonu görünmemeli.

### Yapılan Değişiklikler
- Tahsilat/ödeme ekranında sil aksiyonu "İptal Et" olarak güncellendi.
- Tahsilat/ödeme iptal aksiyonu `pi-ban` ikonu ve iptal onay metniyle uyumlu hale getirildi.
- Tahsilat/ödeme hizmeti iptal endpointine yönlendirildi.
- Satış belgesi ve muhasebe fişi aksiyonları kontrol edildi; mevcut metinler doğru bulunduğu için değişiklik yapılmadı.

### Eksik Kalan İş Kuralları
- Cari, stok, kasa ve banka hareketleri için mevcut fiziksel silme davranışı korundu.
- Ayrı bir ortak UI aksiyon helper'ı çıkarılmadı.
- Backend tarafında ek işlem yapılmadı.
- Mevcut Angular build hataları için ayrı Faz G-1 açılacak.

### Backend
- Değişiklik yapılmadı.

### Frontend
- frontend/src/app/pages/muhasebe/tahsilat-odeme-belgeleri/tahsilat-odeme-belgeleri.ts
- frontend/src/app/pages/muhasebe/tahsilat-odeme-belgeleri/tahsilat-odeme-belgeleri.html
- frontend/src/app/pages/muhasebe/tahsilat-odeme-belgeleri/tahsilat-odeme-belgeleri.service.ts

### Migration
- Gerekmedi.

### Build
- `npm run build` başarısız.
- Hata bizim değişikliklerden bağımsız mevcut Angular sorunlarından geldi: `frontend/src/app/pages/muhasebe/fisler/muhasebe-fisler.component.html`, `frontend/src/app/pages/muhasebe/fisler/muhasebe-fisler.component.ts`, `frontend/src/app/pages/muhasebe/muavin-defter/muavin-defter.component.html`, `frontend/src/app/pages/muhasebe/yevmiye-defteri/yevmiye-defteri.component.html`.

### Test
- Satış belgesi ve muhasebe fişi aksiyon metinleri incelendi.
- Tahsilat/ödeme iptal akışı için frontend build tekrar denendi; mevcut repo hataları nedeniyle tamamlanamadı.
- Bu build hataları için Faz G-1 açılacak şekilde not düşüldü.

### Commit
- Doküman güncellendi.
- Commit oluşturuldu: 900d6e3

---

## Faz G-1 - Angular Build Hatalarının Düzeltimi

### Tespit
- `npm run build` sırasında Faz G değişikliklerinden bağımsız mevcut Angular derleme hataları vardı.
- Hatalar `muhasebe-fisler`, `muavin-defter` ve `yevmiye-defteri` ekranlarındaki PrimeNG toolbar kullanımı, eksik import ve template kapanış hatalarından kaynaklanıyordu.
- `muhasebe-fisler` ekranında ayrıca `formatDateForApi` export eksikti.

### Hata Nedeni
- `p-toolbar` kullanan standalone componentlerde `ToolbarModule` importu eksikti.
- Bazı template'lerde `Class` attribute kullanımı ve kırık HTML blokları Angular parser hatası üretiyordu.
- `muhasebe-fis.model.ts` içinde kullanılan `formatDateForApi` yardımcı fonksiyonu export edilmemişti.

### Yapılan Değişiklikler
- `muhasebe-fis.model.ts` içine `formatDateForApi` export'u eklendi.
- `muhasebe-fisler.component.ts` içinde `ToolbarModule` importu ve standalone imports listesi düzeltildi.
- `muhasebe-fisler.component.html` içinde `p-toolbar` `styleClass` kullanacak şekilde düzeltildi.
- `muavin-defter.component.ts` içinde `ToolbarModule` importu eklendi.
- `muavin-defter.component.html` içinde kırık toolbar HTML yapısı ve `Class` kullanımı düzeltildi.
- `yevmiye-defteri.component.ts` içinde `ToolbarModule` importu eklendi.
- `yevmiye-defteri.component.html` içinde `p-toolbar` `styleClass` kullanacak şekilde düzeltildi.

### Eksik Kalan İş Kuralları
- Muhasebe iş kurallarında değişiklik yapılmadı.
- Backend tarafında değişiklik yapılmadı.
- Migration gerekmedi.

### Backend
- Değişiklik yapılmadı.

### Frontend
- `frontend/src/app/pages/muhasebe/fisler/muhasebe-fisler.component.ts`
- `frontend/src/app/pages/muhasebe/fisler/muhasebe-fisler.component.html`
- `frontend/src/app/pages/muhasebe/models/muhasebe-fis.model.ts`
- `frontend/src/app/pages/muhasebe/muavin-defter/muavin-defter.component.ts`
- `frontend/src/app/pages/muhasebe/muavin-defter/muavin-defter.component.html`
- `frontend/src/app/pages/muhasebe/yevmiye-defteri/yevmiye-defteri.component.ts`
- `frontend/src/app/pages/muhasebe/yevmiye-defteri/yevmiye-defteri.component.html`

### Migration
- Gerekmedi.

### Build
- `npm run build` başarılı.
- Mevcut warning'ler var, hata yok:
  - initial bundle budget aşıldı.
  - `kamp-basvuru.scss`, `garson-servis.scss`, `satis-belgeleri.component.scss` için budget warning'leri var.

### Test
- Frontend build tekrar çalıştırıldı ve başarıyla tamamlandı.
- Manuel runtime test yapılmadı.

### Commit
- Doküman güncellendi.
- Commit oluşturuldu: `2efe3a8`

---

## Faz H - Muhasebe Teknik Borç / Kod Standartları Temizliği

### Tespit
- Son fazlarda eklenen dönem kontrolü, tesis scope kontrolü, tarih helperı ve UI aksiyon metinlerinde küçük tutarsızlıklar gözden geçirildi.
- `muhasebe-fis.model.ts` içinde varsayılan tarih üretiminde `toISOString()` kalıntısı vardı.
- Kod tabanında build’i bozan yeni bir muhasebe iş kuralı tespit edilmedi.

### Yapılan Değişiklikler
- `muhasebe-fis.model.ts` içindeki tarih helper’ı local tarih parçalarıyla `yyyy-MM-dd` üretir hale getirildi.
- `createDefaultFisFilter()` varsayılan tarihleri aynı local helper üzerinden üretir hale getirildi.
- Faz G-1 dokümanındaki commit bilgisi `2efe3a8` olarak doğrulandı.
- Yeni iş kuralı eklenmedi; yalnızca küçük ve güvenli standartlaştırma yapıldı.

### Eksik Kalan İş Kuralları
- Büyük refactor yapılmadı.
- Mimari değişiklik yapılmadı.
- Yeni ortak helper çıkarılmadı.
- Migration gerekmedi.

### Backend
- Değişiklik yapılmadı.

### Frontend
- `frontend/src/app/pages/muhasebe/models/muhasebe-fis.model.ts`

### Migration
- Gerekmedi.

### Build
- `dotnet build backend/STYS.csproj` başarılı.
- `npm run build` başarılı.
- Mevcut warning'ler var, hata yok:
  - `backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs(610,47)` için `CS8629` warning'i var.
  - initial bundle budget aşıldı.
  - `kamp-basvuru.scss`, `garson-servis.scss`, `satis-belgeleri.component.scss` için budget warning'leri var.

### Test
- Backend ve frontend build doğrulaması yapıldı.
- Manuel runtime test yapılmadı.

### Commit
- Doküman güncellendi.
- Commit oluşturuldu: `7628014`

---

## Faz I - Muhasebe Canlıya Hazırlık / Final Checklist

### Tespit
- Faz B'den Faz H'ye kadar yapılan muhasebe değişiklikleri canlıya hazırlık için son kez gözden geçirildi.
- Yeni iş kuralı eklenmedi; odak teknik doğrulama, veri bütünlüğü ve operasyonel checklist oldu.
- `#13 Taşınır Kartları` ana geliştirmesi bu fazın kapsamı dışında bırakıldı.

### Final Checklist Özeti

#### Build / CI
- `dotnet build backend/STYS.csproj` başarılı mı: Evet.
- `npm run build` başarılı mı: Evet.
- Warning'ler bilinen ve kabul edilmiş mi: Evet.
- Bundle budget warning'leri deploy'u engelliyor mu: Hayır, kayıt altına alındı.

#### Migration
- Tüm migration'lar sıralı uygulanıyor mu: Kontrol edilmeli.
- Temiz veritabanına migration uygulanabiliyor mu: Kontrol edilmeli.
- Mevcut test veritabanına migration uygulanabiliyor mu: Kontrol edilmeli.
- Yeni eklenen kolonların default değerleri doğru mu: Kontrol edilmeli.
- Geri dönüş için backup alındı mı: Deploy öncesi alınmalı.

#### Yetki / Tesis Scope
- Liste endpointleri yetkisiz tesis verisi döndürmüyor mu: Kontrol edilmeli.
- Detay endpointleri yetkisiz kayıt döndürmüyor mu: Kontrol edilmeli.
- Update/delete/iptal akışları mevcut kayıt scope kontrolü yapıyor mu: Evet, kod tarafında audit edildi.
- Lookup endpointleri başka tesis verisi sızdırmıyor mu: Kontrol edilmeli.

#### Muhasebe Dönemleri
- Kapalı döneme fiş oluşturma engelleniyor mu: Evet, kontrol edildi.
- Kapalı dönemde belge muhasebeleştirme engelleniyor mu: Evet, kontrol edildi.
- Kapalı dönemde iptal/ters kayıt davranışı doğru mu: Evet, kontrol edildi.
- Bağımsız cari/stok/kasa/banka/tahsilat hareketleri dönem kontrolü yapıyor mu: Evet, kontrol edildi.

#### Cari Kart / Cari Hareket
- Cari kart açılış bakiyesi doğru mu: Kontrol edilmeli.
- Banka hesapları n adet kaydediliyor mu: Kontrol edilmeli.
- Yetkili kişiler n adet kaydediliyor mu: Kontrol edilmeli.
- Cari hareket bakiye, kapanan tutar, kalan tutar doğru mu: Kontrol edilmeli.
- Kapama geri alma doğru çalışıyor mu: Evet, kontrol edildi.

#### Satış/Fatura
- Satış belgesi oluşturma çalışıyor mu: Evet, mevcut akış kontrol edildi.
- Satır parametreleri korunuyor mu: Evet, mevcut akış kontrol edildi.
- İndirim, KDV, ÖTV, ÖİV, konaklama vergisi davranışı iş kuralına uygun mu: Evet, mevcut iş kuralı korunuyor.
- Belgeden fiş oluşturma çalışıyor mu: Evet, kontrol edildi.
- Aynı belge için duplicate fiş engelleniyor mu: Evet, kontrol edildi.
- Belge iptali doğru çalışıyor mu: Evet, kontrol edildi.

#### Muhasebe Fişi
- Yeni fişte default 1 satır geliyor mu: Kontrol edilmeli.
- Satır ekleme her tıklamada 1 satır ekliyor mu: Kontrol edilmeli.
- Borç/alacak eşitliği korunuyor mu: Kontrol edilmeli.
- Taslak fiş güncellenebiliyor mu: Evet, kontrol edildi.
- Onaylı fiş değiştirilemiyor mu: Evet, kontrol edildi.
- İptal/ters kayıt akışı doğru mu: Evet, kontrol edildi.

#### Tahsilat/Ödeme
- Aktif belge iptal edilebiliyor mu: Evet.
- İptal aksiyonu kullanıcıya "İptal Et" olarak görünüyor mu: Evet.
- Kapama varsa geri alınıyor mu: Evet.
- İptal edilmiş belge tekrar iptal edilemiyor mu: Evet.
- Günlük özet iptal kayıtları hariç tutuyor mu: Kontrol edilmeli.

#### Raporlar
- Yevmiye defteri tesis scope ile çalışıyor mu: Evet.
- Muavin defter hesap/tesis uyumu kontrol ediyor mu: Evet.
- Mizan tesis scope ile çalışıyor mu: Evet.
- Cari ekstre/bakiye aktif kayıtlarla doğru mu: Faz I risk listesinde doğrulama gerektiriyor.
- Export filtrelerle uyumlu mu: Evet, kontrol edildi.

#### UI/UX
- Datepicker Türkçe mi: Kontrol edilmeli.
- Hafta Pazartesi'den başlıyor mu: Kontrol edilmeli.
- Tarih formatı dd.mm.yyyy mi: Evet, helper ve ekranlar uyumlu.
- Kritik işlem confirm mesajları doğru mu: Evet.
- Sil/İptal Et ayrımı doğru mu: Evet.

#### Bilinen Uyarılar / Teknik Borç
- Backend `CS8629` warning'i kayıt altına alınmalı.
- Frontend bundle budget warning'leri kayıt altına alınmalı.
- Runtime test yapılmayan fazlar not edilmeli.
- `#13 Taşınır Kartları` kapsam dışı not edilmeli.

#### Rollback Planı
- Deploy öncesi veritabanı backup alınacak.
- Migration sonrası smoke test yapılacak.
- Kritik hata halinde uygulama önceki sürüme alınacak.
- Veritabanı geri dönüşü için backup restore planı hazır olacak.
- Hatalı muhasebe hareketi oluşursa manuel SQL düzeltme yerine ters kayıt/iptal akışı kullanılacak.

### Açık Riskler
- Canlıya çıkış öncesi manuel test kapsamı eksik kalırsa rapor/scope hataları gözden kaçabilir.
- Bundle budget warning'leri teknik borç olarak kalıyor.
- Bazı cari kart ve hareket bakiyeleri için yalnız build seviyesinde doğrulama yapıldı.
- `#13 Taşınır Kartları` kapsamı ayrı fazda ele alınmalı.

### Yapılan Değişiklikler
- Faz I final checklist içeriği dokümana eklendi.
- Önceki fazlar kısa özet halinde canlıya hazırlık bağlamında toparlandı.
- Riskler, manuel testler, migration, build, scope, veri bütünlüğü ve rollback başlıkları tek yerde toplandı.

### Build / Test
- `dotnet build backend/STYS.csproj` başarılı.
- `npm run build` başarılı.
- Mevcut warning'ler var, hata yok.

### Migration
- Bu fazda migration oluşturulmadı.
- Canlıya çıkış öncesi mevcut migration sırasının ve temiz veritabanı uygulanabilirliğinin ayrıca doğrulanması gerekiyor.

### Commit
- Doküman güncellendi.
- Commit oluşturuldu: `3680304`

---

## Faz J - Canlı Öncesi Smoke Test Senaryoları

### Amaç
Canlıya geçmeden önce muhasebe modülündeki minimum kritik akışları hızlı ve uygulanabilir şekilde doğrulamak.

### Test Ortamı Varsayımları
- En az 1 aktif tesis olmalı.
- En az 1 açık muhasebe dönemi olmalı.
- En az 1 yetkili muhasebe kullanıcısı olmalı.
- Test için kullanılacak cari kart, hesap ve depo verileri hazır olmalı.
- Test verileri canlı veriyle karıştırılmamalı.

### Kapsam Dışı
- `#13 Taşınır Kartları` ana geliştirmesi bu smoke setin dışındadır.

### Senaryolar

#### J-01 Kullanıcı giriş / tesis seçimi
Ön koşul:
- Kullanıcı muhasebe yetkisine sahip.

Adımlar:
1. Uygulamaya giriş yap.
2. Muhasebe modülüne gir.
3. Çalışma tesisini seç.
4. Sayfayı yenile.

Beklenen sonuç:
- Seçili tesis korunur.
- Muhasebe ekranları seçili tesisle açılır.

Kritik kontrol:
- Başka tesis verisi görünmez.

#### J-02 Cari kart oluşturma
Ön koşul:
- Yetkili kullanıcı ve boş olmayan cari kart numarası var.

Adımlar:
1. Yeni cari kart oluştur.
2. Zorunlu alanları doldur.
3. Kaydet.

Beklenen sonuç:
- Cari kart kaydedilir.
- Listeye doğru bilgilerle düşer.

Kritik kontrol:
- Aynı kodla ikinci kayıt engellenir.

#### J-03 Cari kart banka hesabı ve yetkili kişi ekleme
Ön koşul:
- Oluşturulmuş bir cari kart var.

Adımlar:
1. Cari kartı aç.
2. Banka hesabı ve yetkili kişi ekle.
3. Kaydet.

Beklenen sonuç:
- Banka hesabı ve yetkili kişi kayıtları saklanır.

Kritik kontrol:
- Kayıtlar sayfa yenilense de korunur.

#### J-04 Açılış bakiyesi oluşturma
Ön koşul:
- Açık dönem ve ilgili cari kart hazır.

Adımlar:
1. Cari açılış bakiyesi gir.
2. Kaydet.
3. Cari hareket ekranında kontrol et.

Beklenen sonuç:
- Açılış bakiyesi hareketi oluşur.

Kritik kontrol:
- Bakiye ve kapanan/kalan tutar alanları tutarlı görünür.

#### J-05 Satış belgesi oluşturma
Ön koşul:
- Aktif müşteri ve stok/hizmet satırı hazır.

Adımlar:
1. Yeni satış belgesi oluştur.
2. Satırları ve vergi alanlarını gir.
3. Kaydet.

Beklenen sonuç:
- Belge kaydedilir.
- Satır parametreleri korunur.

Kritik kontrol:
- İndirim/KDV/ÖTV/ÖİV alanları bozulmaz.

#### J-06 Satış belgesinden muhasebe fişi oluşturma
Ön koşul:
- Onaya uygun satış belgesi var.

Adımlar:
1. Belgeden fiş oluştur.
2. Oluşan fişi aç.

Beklenen sonuç:
- Muhasebe fişi oluşur.
- Belge-fiş bağı kurulur.

Kritik kontrol:
- Aynı belge için duplicate fiş oluşmaz.

#### J-07 Muhasebe fişi onaylama
Ön koşul:
- Taslak fiş var.

Adımlar:
1. Taslak fişi aç.
2. Onayla.
3. Listeyi yenile.

Beklenen sonuç:
- Fiş onaylı duruma geçer.

Kritik kontrol:
- Borç/alacak eşitliği korunur.

#### J-08 Belge iptal / ters kayıt kontrolü
Ön koşul:
- İptale uygun belge var.

Adımlar:
1. Belgeyi iptal et.
2. Oluşan ters/iptal kayıtları kontrol et.

Beklenen sonuç:
- İptal akışı tamamlanır.
- İlgili ters/iptal kayıtları oluşur.

Kritik kontrol:
- İptal edilen kayıtlar aktif toplamları etkilemez.

#### J-09 Tahsilat/ödeme oluşturma
Ön koşul:
- Cari kart ve açık dönem hazır.

Adımlar:
1. Tahsilat/ödeme belgesi oluştur.
2. Kaydet.

Beklenen sonuç:
- Belge kaydedilir.
- Aktif durum görünür.

Kritik kontrol:
- Listeye doğru tesis ile düşer.

#### J-10 Tahsilat/ödeme iptal ve kapama geri alma
Ön koşul:
- Aktif tahsilat/ödeme ve bağlı kapama var.

Adımlar:
1. Belge üzerinde `İptal Et` aksiyonunu çalıştır.
2. Onayı ver.
3. Cari kapamayı kontrol et.

Beklenen sonuç:
- Belge iptal edilir.
- Varsa cari kapama geri alınır.

Kritik kontrol:
- İptal edilmiş belge tekrar iptal edilemez.

#### J-11 Kapalı dönem kontrolü
Ön koşul:
- Kapalı bir muhasebe dönemi var.

Adımlar:
1. Kapalı döneme fiş veya belge oluşturmayı dene.
2. Aynı dönemde iptal/ters kayıt dene.

Beklenen sonuç:
- Engelleme mesajı gösterilir.

Kritik kontrol:
- Kapalı dönemde kayıt oluşmaz.

#### J-12 Yevmiye defteri kontrolü
Ön koşul:
- En az 1 onaylı fiş var.

Adımlar:
1. Yevmiye defterini aç.
2. Tesis ve tarih filtrelerini uygula.
3. Export varsa çalıştır.

Beklenen sonuç:
- Kayıtlar listelenir.
- Filtreler doğru çalışır.

Kritik kontrol:
- Yetkisiz tesis verisi görünmez.

#### J-13 Muavin defter kontrolü
Ön koşul:
- En az 1 hesap ve buna bağlı hareket var.

Adımlar:
1. Muavin defterini aç.
2. Hesap kodu gir.
3. Ara ve export al.

Beklenen sonuç:
- Hesap/tesis uyumlu sonuçlar gelir.

Kritik kontrol:
- Yanlış tesis hesabı kabul edilmez.

#### J-14 Mizan kontrolü
Ön koşul:
- Muhasebe hareketleri mevcut.

Adımlar:
1. Mizan ekranını aç.
2. Tesis filtresiyle çalıştır.

Beklenen sonuç:
- Tesis bazlı mizan alınır.

Kritik kontrol:
- Toplamlar dengeli görünür.

#### J-15 Yetkisiz tesis veri görünürlüğü kontrolü
Ön koşul:
- Kullanıcının erişemediği en az 1 tesis var.

Adımlar:
1. Yetkisiz tesisle ilişkili liste/detay ekranını aç.
2. Scope dışı kaydı doğrudan ID ile dene.

Beklenen sonuç:
- Veri görünmez veya erişim engellenir.

Kritik kontrol:
- Başka tesis verisi sızmaz.

### Build
- Kod değişikliği yapılmadı; build çalıştırılmadı.

### Migration
- Migration gerekmedi.

### Commit
- Doküman güncellendi; commit oluşturuldu: `dd6cf54`
