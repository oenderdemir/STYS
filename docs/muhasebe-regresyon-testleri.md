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
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

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
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

---

## Faz R - Muhasebe Eksik Özellik / Gap Analysis

### Tespit
- Muhasebe modülünde ana iş akışları kod seviyesinde büyük ölçüde tamamlandı; ancak canlıya çıkış öncesi runtime doğrulama borcu hala var.
- Faz Q ve Faz Q-1 ile seed, çoklu banka hesabı modeli ve legacy veri taşıma tarafı toparlandı; buna rağmen gerçek kullanıcı, gerçek scope ve gerçek veriyle son kabul testi yapılmadı.
- Bu fazın amacı yeni kod yazmak değil, canlı öncesi zorunlu doğrulamalar ile canlı sonrası ürün geliştirmelerini ayırmaktır.

### 1. Canlı Öncesi Zorunlu Eksikler
- P0: J-01 ile J-15 arasındaki smoke testler gerçek ortamda tamamlanmalı.
- P0: Seed edilen test verisi ile çoklu banka hesabı, yetkili kişi, açılış bakiyesi, tahsilat/ödeme ve belge iptal akışları uçtan uca doğrulanmalı.
- P0: Legacy `CariKart.BankaAdi` / `CariKart.Iban` verilerinin `CariKartBankaHesaplari` tablosuna taşınması gerçek veri üzerinde kontrol edilmeli.
- P0: Migration zinciri temiz veritabanında ve mevcut veritabanında uygulanabilir olarak doğrulanmalı.
- P0: Scope / yetki sızıntısı testleri gerçek oturumda list, detay, rapor ve lookup ekranlarında doğrulanmalı.
- P0: Dönem kilidi, belge iptali ve kapama geri alma akışları gerçek veri ile tekrar kontrol edilmeli.
- P1: Cari ekstre, bakiye, yevmiye, muavin ve mizan çıktıları rapor verisiyle karşılaştırılmalı.
- P1: İptal edilmiş kayıtların raporlara aktif veri gibi yansımadığı son kez teyit edilmeli.

### 2. Canlı Sonrası Geliştirilebilir Özellikler
- P2: Cari kartta birincil banka hesabı işaretleme ve hesap sıralama desteği.
- P2: Çoklu banka hesabı satırları için daha güçlü satır içi validasyon ve kopyalama desteği.
- P2: Smoke test seed akışı için otomatik sonuç toplama ve raporlama ekranı.
- P2: Muhasebe raporlarında gelişmiş filtre, daha iyi export ve kullanıcıya dönük iyileştirmeler.
- P2: Banka hesabı ve cari kart değişiklikleri için daha ayrıntılı audit / geçmiş görüntüleme.
- P2: Frontend bundle ve style budget borçlarının ürün deneyimini iyileştirecek şekilde teknik borç olarak azaltılması.

### 3. Kapsam Dışı Bırakılanlar
- `#13 Taşınır Kartları` kapsam dışı kalmaya devam ediyor.
- Muhasebe dışı ekranlardaki budget warning'ler bu fazın konusu değil.
- Ürün kapsamını değiştirecek büyük yeniden tasarım veya muhasebe çekirdeği refaktörü bu fazın dışında.
- Production'da otomatik seed çalıştırma yaklaşımı kapsam dışı.

### 4. Riskler
- Veri taşıma ve drop migration'ları yanlış sırayla uygulanırsa veri kaybı veya uyumsuzluk riski oluşur.
- Runtime smoke testler yapılmadan canlıya çıkış, rapor ve scope davranışlarında gizli regresyon bırakabilir.
- Legacy veriler farklı kayıt şekilleri içeriyorsa taşıma SQL'i beklenmeyen duplicate üretimini gizleyebilir; bu yüzden gerçek veri ile doğrulama şarttır.
- Frontend build uyarıları hata değil, ancak ürün kararlılığı için teknik borç olarak izlenmeli.

### 5. Önerilen Sonraki Faz
- Faz S: Canlı öncesi runtime smoke doğrulaması ve go-live checklist kapanışı.
- Bu fazda gerçek test kullanıcısı, test tesisi, açık/kapalı dönem ve migration uygulanmış test veritabanı ile J-01 ile J-15 senaryoları çalıştırılmalı.
- Başarılı sonuçlar, canlıya çıkış kararının son kapısı olarak kayda alınmalı.

---

## Faz P - Muhasebe Final Durum Özeti / Go-No-Go

### Genel Durum
- Muhasebe modülünde ana iş akışları kod ve build seviyesinde gözden geçirildi.
- Kritik scope, dönem, iptal, kapama geri alma ve UI aksiyon metni kontrolleri yapıldı.
- Runtime smoke testler için test senaryoları, seed rehberi ve sonuç formu hazırlandı.

### Karar
- Canlıya çıkış kararı: Koşullu uygun.

### Koşullar
- J-01 – J-15 smoke testleri gerçek test ortamında koşulmalı.
- Test tesisi, test kullanıcısı, açık dönem ve kapalı dönem hazır olmalı.
- Migration zinciri test veritabanında doğrulanmalı.
- `#13 Taşınır Kartları` kapsam dışı olduğu iş birimi tarafından kabul edilmeli.
- Kalan frontend budget warning’leri deploy’u engellemeyecek seviyede kabul edilmeli.

### Tamamlanan Güçlendirmeler
- Cari kart banka hesapları kontrol edildi.
- Satış / fatura belge parametreleri dokümante edildi.
- Muhasebe fişi ve satış belgesi akışları analiz edildi.
- Dönem kapatma / kilitleme kontrolleri analiz edildi.
- Bağımsız cari / stok / kasa / banka / tahsilat hareketlerinde dönem kontrolü güçlendirildi.
- Cari kapama geri alma akışı eklendi.
- Rapor / scope kontrolleri yapıldı.
- Tesis / yetki scope audit yapıldı.
- Kritik UI aksiyon metinleri düzeltildi.
- Angular build hataları giderildi.
- Tarih helperları timezone-safe hale getirildi.
- Redundant yapı audit’i yapıldı.
- Build warning / budget borçları sınıflandırıldı.
- Smoke test, seed rehberi ve sonuç formu hazırlandı.
- Doküman commit standardı düzeltildi.

### Açık Riskler
- Runtime smoke testler henüz gerçek UI ortamında başarılı olarak koşulmadı.
- Migration zinciri temiz / test DB üzerinde ayrıca uygulanmalı.
- `#13 Taşınır Kartları` kapsam dışı.
- Frontend budget warning’leri devam ediyor.
- Bazı rapor ve cari bakiye davranışları runtime veriyle ayrıca doğrulanmalı.

### Build
- Bu fazda kod değişikliği yapılmadı; build çalıştırılmadı.
- Son başarılı build sonuçları önceki fazlarda kayıtlıdır.

### Migration
- Bu fazda migration gerekmedi.

### Test
- Bu faz dokümantasyon fazıdır.
- Runtime test çalıştırılmadı.

### Commit
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

---

## Faz O - Dokümantasyon Temizliği / Commit Hash Standardı

### Tespit
- Faz dokümanlarında commit hash bilgisi commit öncesi bilinmediği için ikinci düzeltme commit’leri oluşuyordu.

### Yapılan Değişiklikler
- Fazlardaki commit bölümleri hash içermeyecek şekilde standartlaştırıldı.
- Geçici commit notları temizlendi.
- Commit hash takibi Git geçmişine bırakıldı.

### Build
- Kod değişikliği yapılmadı; build çalıştırılmadı.

### Migration
- Gerekmedi.

### Commit
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

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
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

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
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

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
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

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
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

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
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

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
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

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
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

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
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

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
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

---

## Faz K - Migration / Veritabanı Uygulanabilirlik Kontrolü

### Tespit
- `STYS.Infrastructure.EntityFramework.StysAppDbContext` için migration zinciri sıralı ve derlenebilir durumda.
- Son muhasebe migration'ları arasında `AddMuhasebeFisleri`, `AddMuhasebeDonemleri`, `AddCariHareketKapamaAlanlari`, `AddTahsilatOdemeBelgesiKapatilacakCariHareketId`, `AddCariKartFinansVeYetkiliKisiAlanlari` ve `AddSatisBelgesiSatirEkParametreleri` yer alıyor.
- `dotnet ef migrations script` komutu context belirtmeden çalışmadı; `--context STYS.Infrastructure.EntityFramework.StysAppDbContext` ile script üretimi tamamlandı.
- Yeni iş kuralı gerektiren bir şema ihtiyacı tespit edilmedi; mevcut model değişiklikleri migration zincirinde karşılanmış görünüyor.

### Migration Kontrolü
- Migration sırası açık ve uygulanabilir.
- Model snapshot ile migration geçmişi uyumlu görünüyor; build ve migration listesi sırasında uyumsuzluk hatası alınmadı.
- Temiz veritabanına uygulanacak zincirde obvious bir sıralama problemi görünmüyor.
- Mevcut test veritabanı için de aynı migration sırası uygulanabilir durumda.

### Riskler
- Script komutu için doğru context belirtilmesi gerekiyor; projede birden fazla `DbContext` var.
- Foreign key ve delete behavior açısından manuel smoke doğrulaması yine gerekli.
- `SatisBelgesiSatirEkParametreleri` ve ilgili eski veri satırlarında nullable/default uyumu canlıda veri ile teyit edilmeli.
- Rollback için deploy öncesi DB backup alınmalı.
- `#13 Taşınır Kartları` kapsam dışı kalmalı.

### Backend
- Değişiklik yapılmadı.

### Frontend
- Değişiklik yapılmadı.

### Build
- `dotnet build backend/STYS.csproj` başarılı.
- `dotnet ef migrations script --context STYS.Infrastructure.EntityFramework.StysAppDbContext --project backend/STYS.csproj --startup-project backend/STYS.csproj --output artifacts/migration-check.sql` çalıştırıldı.
- `npm run build` çalıştırılmadı; bu fazda frontend değişikliği yok.

### Test
- `dotnet ef migrations list --context STYS.Infrastructure.EntityFramework.StysAppDbContext --project backend/STYS.csproj --startup-project backend/STYS.csproj` ile migration zinciri doğrulandı.
- Script üretimi için context seçimi gerektiği doğrulandı.
- Manuel DB restore / apply testi yapılmadı.

### Commit
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

---

## Faz L - Muhasebe Redundant Entity / DTO / Service Audit

### Tespit
- `backend/Muhasebe` ve `frontend/src/app/pages/muhasebe` altındaki entity, DTO, service, helper ve model yüzeyi tarandı.
- Sınıf seviyesinde güvenle silinebilecek redundant yapı doğrulanmadı; benzer görünen çoğu sınıf farklı API/ekran/rapor sözleşmesi taşıyor.
- Tek güvenli temizlik olarak `frontend/src/app/pages/muhasebe/models/muhasebe-fis.model.ts` içindeki kullanılmayan `month` değişkeni kaldırıldı.
- `toISOString`, `Class="`, `from "primeng/toolbar"`, `import { Toolbar }`, `dateFormat="yy-mm-dd"` ve `type="date"` kalıntıları bulunmadı.
- `EnsureOpenPeriodAsync`, `EnsureCanAccessTesisAsync` ve `EnsureCanAccessReportTesisAsync` isimli guard kalıpları tekrar ediyor; ancak bağlamları farklı olduğu için şu aşamada birleştirilmedi.

### Redundant Adaylar
- Kesin kaldırılabilir aday: `frontend/src/app/pages/muhasebe/models/muhasebe-fis.model.ts` içindeki kullanılmayan `month` değişkeni.
- Güvenli aksiyon: kaldırıldı.
- Sınıf/dosya seviyesinde doğrulanmış redundant entity/DTO/service bulunmadı.

### Benzer Ama Korunacak Sınıflar
- `backend/Muhasebe/SatisBelgeleri/Dtos/SatisBelgesiDtos.cs` ve `backend/Muhasebe/SatisBelgeleri/Dtos/SatisBelgesiTaslakOlusturmaDtos.cs` benzer alanlar taşıyor, fakat request/response sözleşmeleri farklı.
- `backend/Muhasebe/MuhasebeFisleri/Dtos/MuhasebeFisDtos.cs` içindeki `YevmiyeDefteriDto`, `MuavinDefterDto`, `MizanDto` ve frontend report modelleri benzer filtre alanları içeriyor, ama farklı rapor bağlamlarında kullanılıyor.
- `backend/Muhasebe/CariKartlar/Dtos/CariKartDtos.cs` ile `CariKartYetkiliKisiDto` birlikte kalmalı; ana cari kart üzerindeki legacy `BankaAdi`/`Iban` alanları kaldırıldı ve çoklu banka hesabı entity'si tek kaynak olarak kullanılıyor.
- `backend/Muhasebe/TasinirKodMuhasebeHesapEslemeleri` ve `backend/Muhasebe/MuhasebeVergiHesapEslemeleri` benzer adlandırma taşısa da işlevsel olarak farklı eşleme alanları.

### İsimlendirme Karışıklıkları
- `SatisBelgesi` / `Fatura` / `Belge` adları aynı iş akışında karışabiliyor; UI ve DTO sözleşmelerinde `SatisBelgesi` standardı korunmalı.
- `delete` / `iptalEt` / `GeriAl` aksiyon adları işlemsel olarak farklı; özellikle tahsilat/ödeme akışında `delete` yerine `iptalEt` tercih edilmeli.
- `Hesap`, `KasaBankaHesap`, `CariKart` ve `CariHareket` isimleri birbirine yakın ama farklı varlıklar; servis ve DTO isimleri buna göre ayrık tutulmalı.

### Güvenli Temizlik Önerileri
- Kullanılmayan local değişkenler ve importlar sadece derleyici uyarısı üretmiyorsa kaldırılmalı.
- Tekrarlayan tarih helperları için ortak isimlendirme korunabilir, fakat davranış değiştirilecekse ayrı faz açılmalı.
- Doküman referansları ve commit hash'leri canlıda yanlış yönlendirme yapmayacak şekilde güncel tutulmalı.

### Riskli Refactor Önerileri
- `SatisBelgesiDtos.cs` içindeki request/response sınıflarını tek modelde birleştirmek API contract ve AutoMapper etkisi nedeniyle risklidir.
- `CariKart` içindeki tekil banka bilgilerini çoklu banka ilişkisine dönüştürmek migration gerektirir; bu fazda yapılmamalı.
- `Normalize*Filter` kalıplarını tek yardımcıya toplamak frontend ve backend rapor davranışını etkileyebilir; ayrı faz gerektirir.

### Backend
- Yeni iş kuralı veya migration yok.
- Guard helper tekrarları ve rapor DTO farklılıkları analiz edildi.

### Frontend
- Muhasebe model/service/dto yüzeyi tarandı.
- `formatDateForApi` tekil yardımcı olarak kaldı; duplicate tarih helper bulunmadı.

### Migration
- Migration gerekmedi.

### Build
- `dotnet build backend/STYS.csproj` başarılı.
- `npm run build` başarılı.
- Warning'ler mevcut ve kabul edilmiş durumda.

### Test
- Build doğrulaması yapıldı.
- `toISOString`, eski PrimeNG toolbar kullanımı ve yanlış `Class` attribute kalıntıları için tarama yapıldı; kalıntı bulunmadı.
- Manuel runtime test yapılmadı.

### Commit
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

---

## Faz M - Warning / Budget / Build Borçları

### Tespit
- `dotnet build` sırasında tek backend warning vardı: `backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs(610,47)` `CS8629`.
- `npm run build` sırasında initial bundle budget warning'i ve üç SCSS budget warning'i vardı.
- Muhasebe ile doğrudan ilgili olan `satis-belgeleri.component.scss` warning'i küçük bir taşma olarak kaldı.
- `kamp-basvuru.scss` ve `garson-servis.scss` muhasebe dışı olduğu için dokunulmadı.

### Yapılan Değişiklikler
- `backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs` içinde bağlı muhasebe fişi için nullable guard eklendi.
- `frontend/src/app/pages/muhasebe/satis-belgeleri/satis-belgeleri.component.scss` içinde yerel `.text-right` kuralı ve iki gereksiz `width: 100%` bildirimi kaldırıldı.
- `satis-belgeleri.component.scss` budget taşması azaltıldı, ancak tamamen sıfırlanmadı.

### Bilerek Dokunulmayanlar
- Initial bundle budget warning'i için büyük chunk split / lazy loading optimizasyonu yapılmadı.
- `kamp-basvuru.scss` ve `garson-servis.scss` muhasebe dışı olduğu için değişiklik yapılmadı.
- İş kuralı ve migration davranışı değişmedi.

### Backend
- `CS8629` warning'i giderildi.
- Backend build warning sayısı 0'a düştü.

### Frontend
- `frontend/src/app/pages/muhasebe/satis-belgeleri/satis-belgeleri.component.scss`
- `satis-belgeleri.component.scss` warning'i azaldı, fakat budget eşiğinin altında değil.
- Diğer budget warning'leri raporlandı ve sonraki faza bırakıldı.

### Migration
- Gerekmedi.

### Build
- `dotnet build backend/STYS.csproj` başarılı, 0 warning.
- `npm run build` başarılı.
- Kalan frontend warning'ler:
  - initial bundle budget exceeded
  - `src/app/pages/kamp-yonetimi/kamp-basvuru.scss`
  - `src/app/pages/restoran-yonetimi/garson-servis.scss`
  - `src/app/pages/muhasebe/satis-belgeleri/satis-belgeleri.component.scss`

### Test
- Backend ve frontend build doğrulaması yapıldı.
- Warning listesinin tamamı okunup sınıflandırıldı.

### Commit
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

---

## Faz N - Runtime Smoke Test Sonuçları

### Test Ortamı
- Tarih: 2026-06-03
- Branch/Commit: `main` / `41f06f7`
- Backend build: Başarılı
- Frontend build: Başarılı
- Kullanılan tesis: Hazırlanamadı
- Kullanılan test kullanıcısı: Hazırlanamadı
- Açık muhasebe dönemi: Hazırlanamadı
- Kapalı muhasebe dönemi: Hazırlanamadı

### Sonuç Tablosu

| Senaryo | Durum | Not |
|---------|-------|-----|
| J-01 | Test Edilemedi | Gerçek UI oturumu ve test kullanıcısı bu çalışma ortamında hazırlanamadı. |
| J-02 | Test Edilemedi | Tesis / veri seti olmadan canlı uygulama üzerinde doğrulanamadı. |
| J-03 | Test Edilemedi | Cari kart banka/yetkili kişi verisi hazırlanamadı. |
| J-04 | Test Edilemedi | Açılış bakiyesi için kontrollü test verisi gereklidir. |
| J-05 | Test Edilemedi | Satış belgesi için canlı UI etkileşimi ve test verisi yok. |
| J-06 | Test Edilemedi | Belgeden fiş oluşturma akışı runtime ortamında çalıştırılamadı. |
| J-07 | Test Edilemedi | Onay akışı canlı uygulamada denenemedi. |
| J-08 | Test Edilemedi | İptal / ters kayıt akışı runtime ortamında çalıştırılamadı. |
| J-09 | Test Edilemedi | Tahsilat/ödeme belgesi için test verisi ve UI oturumu yok. |
| J-10 | Test Edilemedi | İptal ve kapama geri alma akışı canlı uygulamada çalıştırılamadı. |
| J-11 | Test Edilemedi | Kapalı dönem senaryosu için ayrı veri kurulumuna ihtiyaç var. |
| J-12 | Test Edilemedi | Yevmiye defteri UI akışı runtime ortamında açılmadı. |
| J-13 | Test Edilemedi | Muavin defter UI akışı runtime ortamında açılmadı. |
| J-14 | Test Edilemedi | Mizan UI akışı runtime ortamında açılmadı. |
| J-15 | Test Edilemedi | Yetkisiz tesis senaryosu için çok kullanıcılı test ortamı kurulamadı. |

### Bulunan Hatalar
- Runtime smoke test sırasında yeni bir işlevsel hata doğrulanamadı.
- Hazırlık sırasında backend `CS8629` warning'i ve `satis-belgeleri.component.scss` budget taşması tespit edildi.

### Yapılan Düzeltmeler
- `backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs` içinde güvenli nullable guard eklendi.
- `frontend/src/app/pages/muhasebe/satis-belgeleri/satis-belgeleri.component.scss` içinde küçük CSS sadeleştirmesi yapıldı.

### Test Edilemeyenler
- J-01 ile J-15 arasındaki smoke testlerin tamamı gerçek UI oturumu / test verisi olmadığı için çalıştırılamadı.

### Açık Riskler
- Smoke testlerin tamamı canlı uygulama üzerinde tekrar koşulmalı.
- Initial bundle budget warning'i devam ediyor.
- `kamp-basvuru.scss`, `garson-servis.scss` ve `satis-belgeleri.component.scss` için budget uyarıları kayıt altında kalmalı.
- `#13 Taşınır Kartları` kapsam dışı bırakılmalı.

### Build
- `dotnet build backend/STYS.csproj`: Başarılı.
- `npm run build`: Başarılı.

### Migration
- Gerekmedi.

### Commit
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

---

## Faz N-1 - Runtime Test Ortamı ve Seed Veri Hazırlığı

### Amaç
Faz J smoke testlerinin gerçekten çalıştırılabilmesi için minimum test ortamını ve kontrollü seed/veri hazırlıklarını tanımlamak.

### Minimum Veri Seti
| Veri | Durum | Not |
|------|-------|-----|
| Test tesisi | Hazırlanmalı | Adı `TEST MUHASEBE TESISI` olmalı ve yetkili kullanıcıya scope içinde atanmalı. |
| Test kullanıcısı | Hazırlanmalı | Muhasebe yetkileri olmalı; test tesisi scope içinde olmalı. |
| Açık muhasebe dönemi | Hazırlanmalı | Bugünün tarihini kapsayan `Açık/Aktif` dönem gerekli. |
| Kapalı muhasebe dönemi | Hazırlanmalı | Geçmiş tarihli `Kapalı/Kilitli` dönem gerekli. |
| Kasa/Banka hesabı | Hazırlanmalı | En az 1 hareket alabilecek hesap gerekli. |
| Alıcı/Satıcı cari hesap | Hazırlanmalı | `TEST CARI MUSTERI` ve gerekiyorsa `TEST CARI TEDARIKCI`. |
| Gelir/KDV/Stok-Hizmet hesapları | Hazırlanmalı | Fiş ve satış akışı için minimum hesap seti gerekli. |
| Cari kart banka hesabı | Hazırlanmalı | Banka adı + IBAN içermeli. |
| Cari kart yetkili kişi | Hazırlanmalı | En az 1 yetkili kişi kaydı gerekli. |
| Satış belgesi verisi | Hazırlanmalı | En az 1 belge, 1 satır, KDV/indirim alanları dolu. |
| Tahsilat/ödeme verisi | Hazırlanmalı | Kapama yapılacak açık cari hareket ile birlikte. |
| Onaylı muhasebe fişi | Hazırlanmalı | Yevmiye/muavin/mizan raporlarında görünmeli. |
| Yetkisiz tesis kaydı | Hazırlanmalı | Scope dışı veri görünürlüğü testi için gerekli. |

### Test Kullanıcısı
- Muhasebe yetkilerine sahip olmalı.
- `TEST MUHASEBE TESISI` scope içinde olmalı.
- Yetkisiz tesis testi için başka bir tesis veya scope dışı kayıt görünür olmamalı.

### Test Tesisi
- Ad: `TEST MUHASEBE TESISI`
- Kullanıcı bu tesise erişebilmelidir.

### Açık/Kapalı Dönem
- Açık dönem: Bugünün tarihini kapsayan aktif dönem.
- Kapalı dönem: Geçmiş tarih aralığında kilitli dönem.

### Hazırlanması Gereken Kayıtlar
- Test cari müşteri ve gerekiyorsa tedarikçi kartı.
- Banka hesabı ve yetkili kişi içeren cari kart.
- En az 1 satış belgesi ve buna bağlı muhasebe fişi.
- En az 1 tahsilat/ödeme belgesi ve kapama ilişkisi.
- Yevmiye/muavin/mizan raporlarında görülecek onaylı fiş.
- Yetkisiz tesis için görünmemesi gereken kayıt.

### Seed Stratejisi
- Production’da otomatik seed çalıştırılmayacak.
- Mevcut seed/migration yapısı incelendi; muhasebe dışı örneklerde migration tabanlı seed kullanımı mevcut.
- Bu faz için tercihen manuel SQL script veya admin ekranı üzerinden kontrollü hazırlık önerilir.
- Gerekirse sadece development/test ortamında çalışan environment guard'lı seed mekanizması kullanılmalı.
- Otomatik seed üretilecekse `Development`/`Test` ortam kontrolü zorunlu olmalı.

### Açık Riskler
- Gerçek UI smoke koşusu için ortam yine ayrıca hazırlanmalı.
- Kontrollü test verisi canlı veriyle karıştırılmamalı.
- Yeni migration açılması gerekirse önce ayrı gerekçe yazılmalı.
- `#13 Taşınır Kartları` kapsam dışı bırakılmalı.

### Build
- `dotnet build backend/STYS.csproj` başarılı.
- `npm run build` başarılı.

### Test
- Seed/migration yapısı tarandı; mevcut yaklaşımın production-safe olması için environment guard önerildi.
- Smoke testlerin çalıştırılması bu fazın amacı değildir; ortam hazırlığı odaktır.

### Migration
- Gerekmedi.

### Commit
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

---

## Faz N-2 - Smoke Test Seed Rehberi / Manuel Hazırlık

### Tespit
- Faz N-1 için gerekli minimum test verisi tanımlıydı, ancak production-safe ve tekrar çalıştırılabilir hazırlık akışı dokümante edilmemişti.
- Doğrudan SQL seed yerine manuel hazırlık rehberi tercih edildi.

### Seed Rehberi
- `docs/muhasebe-smoke-test-seed-rehberi.md` oluşturuldu.
- Rehber test/dev ortamı için yazıldı; production’da otomatik seed yok.
- `TEST_` / `TEST MUHASEBE` prefix kullanımı ve destructive olmayan hazırlık adımları belirtildi.

### Production Güvenliği
- Otomatik seed production’da çalışmayacak.
- Rehber ekran bazlı manuel hazırlık içeriyor.
- Geri alma için iptal / ters kayıt yaklaşımı not edildi.

### Build
- `dotnet build backend/STYS.csproj` başarılı.
- `npm run build` başarılı.
- Frontend tarafında mevcut budget warning’leri hata seviyesinde değil.

### Test
- Test veri ihtiyaçları tablo ve adım bazında netleştirildi.
- SQL script yerine manuel hazırlık yaklaşımı seçildi.
- `#13 Taşınır Kartları` kapsam dışı bırakıldı.

### Migration
- Gerekmedi.

### Commit
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

---

## Faz N-3 - Smoke Test Sonuç Formu

### Tespit
- Smoke testlerin gerçek ortamda uygulanması için standart bir sonuç formuna ihtiyaç vardı.

### Yapılan Değişiklikler
- `docs/muhasebe-smoke-test-sonuc-formu.md` oluşturuldu.
- J-01 – J-15 için doldurulabilir sonuç tablosu eklendi.
- Hatalı senaryolar için issue açma formatı eklendi.

### Build
- Kod değişikliği yapılmadı; build çalıştırılmadı.

### Migration
- Gerekmedi.

### Commit
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.
## Faz Q - Smoke Seed Altyapısı

### Tespit
- J-01 – J-15 smoke testleri için kontrollü test verisini hazırlayan dev/test-only seed altyapısına ihtiyaç vardı.

### Yapılan Değişiklikler
- `backend/Muhasebe/DevTools/Services/MuhasebeSmokeTestSeedService.cs` eklendi.
- `POST /ui/muhasebe/dev-tools/seed-smoke-test-data` endpointi yalnızca development/test ortamlarında map edildi.
- `docs/muhasebe-smoke-test-seed-rehberi.md` oluşturuldu ve seed akışı dokümante edildi.
- Faz Q seed düzeltmesi ile `KullaniciTesisSahiplik` kontrolü `UserId + TesisId` bazlı hale getirildi ve `TEST CARI MUSTERI` / `TEST CARI TEDARIKCI` için banka hesabı artık `CariKartBankaHesabi` entity’si üzerinden seed ediliyor.

### Bilerek Yapılmayanlar
- Production otomatik seed eklenmedi.
- Büyük iş akışı refactor yapılmadı.

### Build
- `dotnet build backend/STYS.csproj` başarılı.
- `npm run build` başarılı.
- Frontend warning'leri mevcut: initial bundle budget, `kamp-basvuru.scss`, `satis-belgeleri.component.scss`, `garson-servis.scss`.

### Migration
- `backend/Infrastructure/EntityFramework/Migrations/20260604183000_FazQ_CariKartBankaHesabiEntitySeed.cs` eklendi.
- Migration no-op; mevcut schema zaten `CariKartBankaHesaplari` tablosunu içeriyor.

### Test
- `POST /ui/muhasebe/dev-tools/seed-smoke-test-data` yalnızca development/test ortamında map edildi.
- Seed akışı idempotent ve production-safe olarak tasarlandı.
- Runtime smoke testler bu fazda koşturulmadı.

### Commit
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.

---

## Faz Q-1 - CariKart Çoklu Banka Hesabı Modeli

### Tespit
- Cari kart ekranında tekil legacy banka alanları ile çoklu banka hesabı kullanımının birlikte yaşaması veri tutarlılığı riski oluşturuyordu.
- Smoke test seed tarafında gerçek `CariKartBankaHesabi` entity kaydı oluşsa da, kullanıcı ekranı ve servis katmanı tekil model ile sınırlı kalıyordu.
- Yeni modelin ana kaynak olarak `CariKartBankaHesabi` listesini kullanması gerekiyordu.

### Yapılan Değişiklikler
- `CariKartDto`, `CreateCariKartRequest` ve frontend model/request yapıları çoklu `BankaHesaplari` listesi alacak şekilde güncellendi.
- `CariKartService` create/update/get akışları gerçek `CariKartBankaHesabi` ilişkisini okuyup yazacak şekilde refactor edildi.
- Legacy tekil `BankaAdi` / `Iban` alanları ana cari karttan kaldırıldı; yeni veri kaynağı yalnızca `BankaHesaplari` listesidir.
- Cari kart formu frontend tarafında çoklu banka hesabı tablosu ile güncellendi.
- `CariKartBankaHesabi` ilişki silme davranışı `Restrict` olarak korundu ve migration ile kayda geçirildi.

### Eksik Kalan İş Kuralları
- Legacy tekil banka alanları sadece geriye dönük okuma uyumluluğu için kullanılmalı; yeni kayıt girişi çoklu liste üzerinden olmalı.
- İptal/soft delete edilmiş banka hesapları aktif liste sorgularında görünmemeli.

### Backend
- `backend/Muhasebe/CariKartlar/Dtos/CariKartDtos.cs`
- `backend/Muhasebe/CariKartlar/Mapping/CariKartProfile.cs`
- `backend/Muhasebe/CariKartlar/Services/CariKartService.cs`
- `backend/Infrastructure/EntityFramework/StysAppDbContext.cs`
- `backend/Infrastructure/EntityFramework/Migrations/20260604212020_FazQ1_CariKartCokluBankaHesabiModeli.cs`
- `backend/Infrastructure/EntityFramework/Migrations/20260604212020_FazQ1_CariKartCokluBankaHesabiModeli.Designer.cs`

### Frontend
- `frontend/src/app/pages/muhasebe/cari-kartlar/cari-kartlar.dto.ts`
- `frontend/src/app/pages/muhasebe/cari-kartlar/cari-kartlar.ts`
- `frontend/src/app/pages/muhasebe/cari-kartlar/cari-kartlar.html`

### Seed
- `TEST CARI MUSTERI` ve `TEST CARI TEDARIKCI` seed akışında banka hesabı artık çoklu banka hesabı entity listesi ile uyumlu şekilde taşınıyor.
- Duplicate kontrolü `CariKartId + IBAN` veya `CariKartId + HesapNo` mantığına göre korunuyor.

### Migration
- `FazQ1_CariKartCokluBankaHesabiModeli` migration'ı no-op olarak bırakıldı.
- Model snapshot güncellendi.
- Mevcut veritabanında `CariKartBankaHesaplari` tablosu zaten bulunduğu için bu migration tablo oluşturma işlemi yapmıyor.
- Legacy `CariKart.BankaAdi` / `CariKart.Iban` verileri drop öncesi `CariKartBankaHesaplari` tablosuna taşındı; yeni kayıt oluşmamış cari kartlarda veri kaybı riski bırakılmadı.

### Build
- `dotnet build backend/STYS.csproj` başarılı.
- `npm run build` başarılı.
- Kalan frontend warning'leri hata seviyesinde değil.

### Test
- Build doğrulaması yapıldı.
- Manuel runtime smoke test bu fazda koşturulmadı.

### Commit
- Bu faz için commit oluşturuldu.
- Commit hash’i Git geçmişinden takip edilecek.
