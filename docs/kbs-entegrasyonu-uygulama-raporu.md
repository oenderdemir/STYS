# KBS Entegrasyonu Uygulama Raporu

**Proje:** STYS

**Kapsam:** Türkiye Kimlik Bildirim Sistemi entegrasyon altyapısı

**Tarih:** 20 Temmuz 2026
**Durum:** Ortak altyapı, Fake connector, SQL outbox, API, kullanıcı arayüzü, migration ve otomatik testler tamamlandı. Resmî Jandarma WSDL sözleşmesi ile güncel EGM Excel şablonu bekleniyor.

## 1. Yönetici özeti

STYS projesine, konaklayanların fiziksel giriş ve çıkışlarının kolluk sistemlerine bildirilmesini yönetmek üzere tesis ve tenant bazlı bir KBS modülü eklenmiştir. Tasarımın ana ilkesi, KBS bildiriminin rezervasyon yerine `RezervasyonKonaklayan` bazında yönetilmesidir.

Fiziksel giriş/çıkış ile rezervasyonun operasyonel ve mali check-in/check-out işlemleri birbirinden ayrılmıştır. Böylece bir kişinin fiilî çıkışı, rezervasyonda kalan borç olsa bile kaydedilebilir; bu işlem rezervasyonun mali check-out durumunu otomatik olarak değiştirmez.

Gerçek EGM veya Jandarma sistemine hiçbir test verisi gönderilmemiştir. Canlı entegrasyon varsayılan olarak kapalıdır. Resmî sözleşme veya şablon bulunmayan alanlar tahmin edilmemiştir.

## 2. İncelenen mevcut mimari

Uygulama öncesinde aşağıdaki alanlar incelenmiştir:

- Rezervasyon, konaklayan planı, oda değişikliği, ödeme ve check-out akışları
- Tesis ve kurum ilişkileri
- Tenant global query filter ve `ITenantEntity` kuralları
- EF Core DbContext, migration ve model snapshot yapısı
- Mevcut yetki ve menü seed yaklaşımı
- Lisans modülü filtreleri
- Hosted service örnekleri
- SignalR tabanlı bildirim sistemi
- HTTP request/response ve domain loglama yapısı
- Angular PrimeNG/Sakai sayfa ve servis düzeni
- Mevcut xUnit test altyapısı

Mevcut mimari korunmuş; yeni framework veya ek mimari katman eklenmemiştir.

## 3. Konaklayan modeli ve rezervasyon davranışı

`RezervasyonKonaklayan` modeli aşağıdaki alanlarla geriye uyumlu olarak genişletilmiştir:

- Ad, Soyad
- KimlikTuru, KimlikNo
- BelgeNo, BelgeTuru
- UyrukKodu
- DogumTarihi, DogumYeri
- Cinsiyet, Telefon, AracPlakasi
- FiiliGirisTarihi, FiiliCikisTarihi
- KonaklamaKullanimSekli

Eski `AdSoyad`, `TcKimlikNo` ve `PasaportNo` alanları korunmuştur. Eski `AdSoyad` değerleri otomatik olarak ad ve soyada bölünmemiştir.

Konaklayan planı kaydedilirken kişilerin silinip yeniden oluşturulması engellenmiştir. Mevcut kişiler sıra numarası üzerinden güncellenmekte, böylece kalıcı KBS bildirimlerinin `RezervasyonKonaklayanId` ilişkisi korunmaktadır.

Eklenen fiziksel olay işlemleri:

- `FiiliGirisYapAsync`
- `FiiliCikisYapAsync`
- `OdaDegisikligiBildirAsync`
- `GelmeyecekOlarakIsaretleAsync`

Fiilî giriş ve çıkış işlemleri idempotenttir. Fiilî çıkış akışında ödeme veya kalan borç kontrolü bulunmaz. Bu işlem mevcut `TamamlaCheckOutAsync` metodunu çağırmaz ve rezervasyonun mali durumunu değiştirmez.

## 4. KBS modülü

Bağımsız `backend/Kbs` modülü altında aşağıdaki bileşenler oluşturulmuştur:

- Tesis ayarı, bildirim ve bildirim denemesi entity’leri
- API DTO’ları ve KBS sabitleri
- Ortak connector sözleşmesi ve resolver
- Fake KBS connector
- Güvenlik kilitli Jandarma connector sınırı
- EGM Excel connector sınırı
- Bildirim oluşturma ve yönetim servisleri
- SQL outbox worker
- Tenant ve yetki korumalı KBS controller
- KBS options sınıfı

KBS ayarları tesis bazındadır. Aynı kuruma ait farklı tesisler farklı kolluk sistemi veya entegrasyon tipi seçebilir.

## 5. SQL outbox ve idempotency

Kalıcı outbox için aşağıdaki tablolar eklenmiştir:

- `KbsTesisAyarlari`
- `KbsBildirimler`
- `KbsBildirimDenemeleri`

Bildirimlerde tenant, tesis, rezervasyon, konaklayan, bildirim tipi, sağlayıcı, durum, idempotency anahtarı, olay anahtarı, payload sürümü/hash’i, deneme bilgileri, hata bilgileri, gönderim/tamamlanma tarihleri, Excel manifest hash’i ve rowversion tutulmaktadır.

Fiilî olay ve outbox kaydı tek EF Core `SaveChanges` sınırında kalıcılaştırılır. EF Core ilişkisel sağlayıcı bu çoklu değişikliği atomik transaction içinde yürütür. Harici connector çağrısı bu transaction’ın dışında, worker tarafından yapılır.

Aynı kişi, bildirim tipi ve fiziksel olay için tekrar kayıt oluşması iki ayrı unique index ile engellenmiştir:

- `IdempotencyKey`
- `KurumId + RezervasyonKonaklayanId + BildirimTipi + OlayAnahtari`

Deneme tablosunda ham kimlik bilgileri veya SOAP XML tutulmaz. Yalnızca hata sınıfı, sağlayıcı kodu ve maskelenmiş açıklama saklanır.

## 6. Connector davranışları

### Fake connector

Geliştirme ve otomatik testler dış ağa çıkmayan Fake connector ile çalışır. Desteklenen sentetik sonuçlar:

- Başarılı
- Geçici servis hatası
- Timeout
- Yetki/IP hatası
- Geçersiz veri
- Kayıt zaten mevcut
- Kayıt bulunamadı
- Sonucu belirsiz işlem

### Jandarma connector

Jandarma connector’u canlı çağrı yapmayan güvenli bir adapter sınırı olarak eklenmiştir. Development ve Test ortamlarında koşulsuz olarak `LIVE-DISABLED` sonucu üretir.

Resmî WSDL snapshot’ı repository’de bulunmadığı için SOAP sınıfları veya servis metotları tahmin edilmemiştir. Bu nedenle gerçek SOAP adapter, WSDL tabanlı DTO’lar, `ParametreListele` eşlemesi ve connector içi Polly circuit-breaker politikası resmî sözleşme sağlanana kadar açık blocker olarak bırakılmıştır.

### EGM Excel connector

EGM için tarayıcı/OTP otomasyonu uygulanmamıştır. Excel üretim altyapısı ClosedXML ile hazırlanmıştır ancak resmî şablon ve kolon eşlemesi olmadan dosya üretmez.

Gerekli ayarlar:

- `Kbs:EgmTemplatePath`
- `Kbs:EgmColumns:Ad`
- `Kbs:EgmColumns:Soyad`
- `Kbs:EgmColumns:KimlikNo`
- `Kbs:EgmColumns:BelgeNo`
- `Kbs:EgmColumns:UyrukKodu`
- `Kbs:EgmColumns:OlayTarihi`

Formül veya makro çalıştırılmaz. `=`, `+`, `-`, `@`, tab veya satır başı karakterleriyle başlayan hücreler formula injection’a karşı kaçışlanır.

Excel iş akışı şu durumları ayrı tutar:

1. `DosyaUretildi`
2. `YuklemeOnayiBekliyor`
3. `Dogrulandi`

Kullanıcının “KBS’ye yükledim” onayı bildirimi otomatik olarak başarılı yapmaz.

## 7. Worker ve hata yönetimi

`KbsBildirimWorker` hosted service olarak çalışır ve uygun outbox kayıtlarını işler.

Başlıca davranışlar:

- Rowversion ve durum koşullarıyla çift worker gönderimini engelleme
- Exponential backoff ve jitter
- Maksimum deneme sonrası `MudahaleGerekli`
- Yetki, IP, credential ve kalıcı veri hatalarında retry yapmama
- Belirsiz sonucu otomatik başarılı kabul etmeme
- Uygulama yeniden başladığında yarım kalan `Gonderiliyor` kayıtlarını `SonucuBelirsiz` olarak geri alma
- Sonuçları mevcut SignalR bildirim sistemi üzerinden tesis kullanıcılarına iletme
- CancellationToken desteği

Hata sınıfları:

- `Transient`
- `Permanent`
- `Configuration`
- `Uncertain`

## 8. API, yetki ve lisans

Eklenen yetkiler:

- `KbsYonetimi.Menu`
- `KbsYonetimi.View`
- `KbsYonetimi.Manage`
- `KbsYonetimi.Retry`
- `KbsYonetimi.Settings`
- `KbsYonetimi.SensitiveDataView`

Eklenen lisans modülü:

- `StysLicensedModules.Kbs`

API kapsamı:

- Tesis KBS ayarlarını getir/güncelle
- Bağlantı veya konfigürasyon kontrolü
- Günlük KBS özeti
- Filtreli ve sayfalı bildirim listesi
- Ayrı yetkili hassas veri görünümü
- Başarısız bildirimi yeniden kuyruğa alma
- Manuel müdahale durumu
- EGM giriş/çıkış Excel’i oluşturma
- EGM manifest tabanlı yükleme onayı
- Konaklayan fiilî giriş/çıkış
- Konaklayan oda değişikliği
- Konaklayanı gelmeyecek olarak işaretleme

KBS entity’leri `ITenantEntity` uygular. Kullanıcı API’leri mevcut global tenant filtresi altında çalışır. Worker yalnızca kendi kontrollü sistem sorgularında query filter’ı atlar ve kayıtları kurum/tesis bilgileriyle birlikte işler.

## 9. Angular kullanıcı arayüzü

PrimeNG/Sakai tasarımına uygun “KBS Bildirim Merkezi” sayfası eklenmiştir.

Sayfa özellikleri:

- Tesis ve durum seçimi
- Günlük başarılı, bekleyen, belirsiz/hatalı ve müdahale gereken sayaçları
- Bildirim tipi, sağlayıcı, durum, deneme sayısı ve son hata tablosu
- Varsayılan maskeli kişi adı
- Tekrar dene aksiyonu
- EGM giriş ve çıkış Excel üretimi
- Manifest tabanlı “KBS’ye yükledim” onayı
- Tesis KBS ayarları
- Secret yerine secret reference girişi
- Canlı gönderim güvenlik açıklaması

Rezervasyon konaklayan planına şu alanlar eklenmiştir:

- Yeni kimlik/KBS bilgileri
- KBS durumu ve son sonuç
- Eksik KBS bilgileri
- Fiilî giriş
- Fiilî çıkış
- Oda değişikliği bildirimi

## 10. Güvenlik kontrolleri

- `Kbs:LiveConnectorsEnabled` varsayılan olarak `false` değerindedir.
- Canlı gönderim için Production ortamı, global kilit ve tesis kilidi birlikte gereklidir.
- Development/Test ortamında Jandarma connector çağrısı engellenir.
- Veritabanında veya appsettings içinde KBS parolası tutulmaz.
- Yalnızca secret manager referansı saklanır.
- TCKN, YKN, pasaport, belge numarası ve SOAP payload alanları HTTP loglarında maskelenir.
- `/ui/kbs` request/response body’leri tamamen log kapsamı dışındadır.
- Worker logları bildirim/tesis/durum/kod bilgisiyle sınırlıdır.
- Ham SOAP XML veya connector payload’ı veritabanında tutulmaz.
- Liste API’si hassas alanları varsayılan olarak maskeler.
- EGM formula injection koruması uygulanır.
- Testlerde production endpoint’ine ulaşabilecek HTTP istemcisi yoktur.

## 11. Veritabanı migration’ı

`20260720220000_AddKbsIntegration` migration’ı aşağıdaki değişiklikleri içerir:

- Konaklayan tablosuna nullable KBS alanları
- Üç yeni KBS tablosu
- Tenant/tesis/rezervasyon/konaklayan foreign key’leri
- Idempotency, olay, worker ve deneme indexleri
- Rowversion concurrency alanı
- KBS yetkileri ve admin grup atamaları
- KBS Bildirim Merkezi menüsü
- Güncellenmiş EF model snapshot

Migration SQL’i başarıyla üretilmiş ancak gerçek bir veritabanına uygulanmamıştır.

## 12. Test kapsamı ve sonuçları

Eklenen KBS testleri aşağıdaki davranışları kapsar:

- Aynı fiilî girişte iki giriş bildirimi oluşmaması
- Aynı fiilî çıkışta iki çıkış bildirimi oluşmaması
- Borçlu rezervasyonda fiilî çıkış yapılabilmesi
- Fiilî çıkışın mali check-out’u değiştirmemesi
- Eksik kimlik bilgisi validasyonu
- Fake başarı, timeout, yetki/IP ve belirsiz sonuçları
- Timeout retry davranışı
- Yetki/IP hatasında retry yapılmaması
- Worker recovery
- Tenant izolasyonu
- HTTP ve worker log maskelemesi
- Excel formula injection koruması
- EGM yükleme onayının başarı sayılmaması
- Development ortamında canlı connector kilidi

Doğrulama sonuçları:

- Backend build: başarılı, 0 hata, 0 uyarı
- KBS testleri: 13/13 başarılı
- Tam solution testi: 310 başarılı, 0 başarısız, 18 mevcut SQL entegrasyon testi skipped
- Angular production build: başarılı
- Migration SQL üretimi: başarılı
- `git diff --check`: başarılı

Angular build’de repository’nin mevcut bundle bütçe uyarısı devam etmektedir: başlangıç paketi 4.25 MB, tanımlı bütçe 2.80 MB.

## 13. Tamamlanmayan ve bilgi bekleyen konular

### Jandarma

- Resmî WSDL snapshot’ı
- Resmî SOAP adapter sınıfları
- Salt-okunur bağlantı kontrol metodunun doğrulanması
- `ParametreListele` ve ülke/parametre eşlemeleri
- Resmî hata kodu tablosu
- Adapter seviyesinde Polly retry ve circuit-breaker
- Tesis kodu, yetkili hesabı, secret manager kaydı ve kaynak IP onayı

### EGM

- Güncel resmî Excel şablonu
- Resmî kolon eşlemesi ve şablon sürümü
- Yükleme sonrası resmî doğrulama/mutabakat yöntemi

Bu bilgiler sağlanana kadar canlı entegrasyon güvenli biçimde kapalı kalır. Fake connector, domain, outbox, API, kullanıcı arayüzü ve otomatik testler kullanılabilir.

## 14. Canlı pilot öncesi kontrol listesi

1. Pilot tesisin EGM/Jandarma sorumluluk alanını yazılı olarak doğrulayın.
2. KBS lisansını yalnızca pilot kurum/tesis için etkinleştirin.
3. KBS yetkilerini sınırlı kullanıcı grubuna atayın.
4. Resmî WSDL veya Excel şablonunu kontrollü kaynak olarak repository/deployment sürecine dahil edin.
5. Secret manager entegrasyonunu ve kaynak IP yetkisini doğrulayın.
6. Migration’ı production benzeri SQL Server ortamında prova edin.
7. Eşzamanlı worker ve rowversion testlerini gerçek SQL Server üzerinde çalıştırın.
8. Log ve observability sistemlerinde hassas veri taraması yapın.
9. Fake connector ile başarı, timeout, yetki ve belirsiz sonuç kabul testlerini tamamlayın.
10. Mutabakat ve manuel müdahale sorumlularını belirleyin.
11. Global canlı kilidi değişiklik kaydı ve çift onayla açın.
12. Yalnızca pilot tesisin canlı gönderim kilidini etkinleştirin.
13. İlk gerçek bildirim öncesinde kurum ve veri sorumlusu onayı alın.

## 15. Değiştirilen ana alanlar

- `backend/Kbs`
- `backend/Rezervasyonlar`
- `backend/Infrastructure/EntityFramework`
- `backend/Licensing`
- `backend/Program.cs`
- `backend/StructurePermissions.cs`
- `platform/TOD.Platform.AspNetCore/Middleware`
- `frontend/src/app/pages/kbs-bildirim-merkezi`
- `frontend/src/app/pages/rezervasyon-yonetimi`
- `tests/STYS.Tests/KbsIntegrationTests.cs`
- `docs/kbs-entegrasyonu.md`

## 16. Önerilen commit mesajı

```text
feat(kbs): add secure guest notification integration infrastructure
```

Türkçe alternatif:

```text
feat(kbs): güvenli konaklayan bildirim altyapısını ekle
```
