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
