# KBS entegrasyonu

Bu modül, konaklayanların fiilî giriş/çıkış olaylarını rezervasyonun mali ve operasyonel check-in/check-out akışından ayırır. KBS bildiriminin birimi `RezervasyonKonaklayan`dır. Fiilî çıkışta borç kontrol edilmez ve rezervasyonun mali check-out durumu değiştirilmez.

## Sağlayıcılar

- `Fake`: geliştirme ve otomatik testlerde kullanılan, dış ağa çıkmayan sentetik connector. Başarı, geçici hata, timeout, yetki/IP hatası, geçersiz veri, mevcut/bulunamayan kayıt ve belirsiz sonuç üretebilir.
- `Jandarma/Soap`: resmî hizmet için ayrılmış adapter sınırıdır. Kamuya açık bir test servisi yoktur. Resmî WSDL'den kontrollü olarak üretilmiş sözleşme repository'ye sağlanmadığı için bu sürüm ağ çağrısı yapmaz ve açık bir configuration error döndürür. Üretim adapteri tesis kodu, yetkili/parola ve kurumca tanımlanmış kaynak IP gerektirir.
- `EGM/Excel`: EGM için belgelenmiş kamuya açık test/web servis API'si varsayılmaz. Tarayıcı, OTP veya portal otomasyonu yoktur. Resmî Excel şablonu ve kolon eşlemesi sağlanmadan kolonlar tahmin edilmez ve dosya üretilmez.

## Canlı gönderim güvenliği

Canlı çağrı üç ayrı kilitle korunur: uygulama `Production` ortamında olmalı, `Kbs:LiveConnectorsEnabled=true` olmalı ve ilgili tesis ayarında `CanliGonderimAktifMi=true` olmalıdır. Varsayılan yapılandırmada global kilit kapalıdır. Development/Test ortamında Jandarma connector her durumda `LIVE-DISABLED` döndürür. Testlerde production endpoint'ine istek gönderebilecek bir HTTP istemcisi kayıtlı değildir.

Parola ayarlarda saklanmaz. `SecretReference`, işletmenin secret manager/vault sistemindeki kayda işaret eder. Parola; `appsettings`, `.env`, log veya veritabanında düz metin olarak tutulmamalıdır. Üretim adapteri eklenirken secret yalnızca çağrı anında çözülmeli ve bellekte mümkün olan en kısa süre tutulmalıdır.

## Outbox ve hata sınıflandırması

Fiilî olay ile `KbsBildirim` aynı EF `SaveChanges` sınırında kalıcılaştırılır. Sağlayıcı çağrısı transaction dışında worker tarafından yapılır. Giriş/çıkış olay anahtarları `stay:{konaklayanId}:giris|cikis` biçiminde zamandan bağımsızdır. Unique index ve konaklayan `rowversion` alanı eşzamanlı isteklerde tek kazanan üretir; yalnızca beklenen KBS unique constraint ihlalleri idempotent başarı olarak ele alınır.

Outbox payload'ı oluşturma anındaki verinin canonical JSON snapshot'ıdır. SHA-256 hash ile bütünlüğü doğrulanır ve ASP.NET Core Data Protection ile şifrelenerek `ProtectedPayload` alanında saklanır. Worker ve EGM Excel üretimi konaklayan tablosunu yeniden okumaz. Çözme, hash veya kimlik invariant kontrolü başarısızsa kayıt `MudahaleGerekli` durumuna alınır ve gönderilmez. Production ortamında kalıcı key-ring dizini `Kbs:PayloadProtectionKeyRingPath` ile açıkça yapılandırılmalıdır; hazır olmayan koruma altyapısı fail-closed davranır.

- `Transient`: ağ/timeout/geçici servis hatası; exponential backoff ve jitter ile sınırlı retry.
- `Permanent`: geçersiz girdi veya bulunamayan kayıt; retry yok, müdahale gerekir.
- `Configuration`: yetki, IP, credential veya eksik adapter/ayar; retry yok.
- `Uncertain`: cevap alınamadı ve karşı tarafta sonuç oluşmuş olabilir; otomatik başarı/retry yok, mutabakat gerekir.

Worker yeniden başlatıldığında süresi geçmiş `Gonderiliyor` kayıtları otomatik başarı sayılmaz; `SonucuBelirsiz` durumuna taşınır.

Worker her kaydı ayrı scope ve hata sınırı içinde işler. Tek bir poison kayıt batch'i durdurmaz. Harici sistem sonucu alındıktan sonra yerel sonuç kaydı başarısız olursa otomatik tekrar gönderim yapılmaz; kayıt ayrı context ile `SonucuBelirsiz` durumuna taşınmaya çalışılır.

## Durum geçişleri ve mutabakat

| Kaynak durum | İşlem | Hedef durum |
|---|---|---|
| `Hazir`, `TekrarBekliyor` | Worker gönderimi | `Basarili`, `TekrarBekliyor`, `SonucuBelirsiz` veya `MudahaleGerekli` |
| `MudahaleGerekli` | Yetkili manuel retry | `Hazir` |
| `SonucuBelirsiz` | “İşlendi” mutabakatı | `Dogrulandi` |
| `SonucuBelirsiz` | “İşlenmedi” mutabakatı | `MudahaleGerekli` |
| `DosyaUretildi` | EGM yükleme onayı | `YuklemeOnayiBekliyor` |
| `YuklemeOnayiBekliyor` | EGM başarı/başarısızlık doğrulaması | `Dogrulandi` veya `MudahaleGerekli` |

Mutabakat işlemleri `KbsYonetimi.Reconciliation` yetkisi gerektirir. Açıklama zorunludur; kimlik/belge numarası yazılması reddedilir. Karar, kullanıcı, zaman, önceki/yeni durum ve opsiyonel kurum referansı `KbsDurumGecmisleri` tablosunda denetlenebilir olarak saklanır.

## EGM şablonu ve yükleme akışı

Resmî `.xlsx` dosyası güvenli bir deployment konumuna konur ve `Kbs:EgmTemplatePath` ayarlanır. `Kbs:EgmColumns` içinde `Ad`, `Soyad`, `KimlikNo`, `BelgeNo`, `UyrukKodu`, `OlayTarihi` kolon numaraları resmî şablona göre tanımlanır. Formül/makro çalıştırılmaz; `=`, `+`, `-`, `@`, tab veya satır başı ile başlayan metinler formula injection'a karşı kaçışlanır.

Dosyadaki bildirimler idempotency anahtarlarından türetilen manifest hash ile izlenir. Durumlar ayrı tutulur: `DosyaUretildi`, kullanıcı yükleme onayından sonra `YuklemeOnayiBekliyor`, kurumca doğrulandıktan sonra `Dogrulandi`. Yükleme onayı tek başına bildirimi `Basarili` yapmaz.

Oda değişikliği için serbest metin KBS endpoint'i yoktur. Bildirim yalnızca tesis/oda uygunluğu ve atama doğrulamalarını yapan canonical rezervasyon oda değişikliği akışından, gerçek eski/yeni oda kimlikleri ve taşınan konuklarla aynı `SaveChanges` biriminde üretilir.

## Hassas veri

TCKN/YKN, pasaport/belge numarası ve ham SOAP XML loglanmaz. Deneme tablosu yalnızca sağlayıcı kodu, hata sınıfı ve maskelenmiş açıklama tutar. Liste API'si kişi adını varsayılan olarak maskeler; açık görünüm ayrı `KbsYonetimi.SensitiveDataView` yetkisi gerektirir. SOAP payload saklanmaz.

## Pilot kontrol listesi

1. Tesisin EGM/Jandarma sorumluluk alanını kurumdan yazılı doğrulayın.
2. Jandarma için resmî WSDL snapshot'ı, tesis kodu, yetkili hesabı, secret reference ve izinli kaynak IP'yi doğrulayın.
3. EGM için güncel resmî şablonu ve imzalı kolon eşlemesini sağlayın.
4. Fake connector ile giriş, çıkış, oda değişimi, timeout, yetki ve belirsiz sonuç senaryolarını tamamlayın.
5. Pilot tenant/tesis yetkilerini ve KBS lisansını sınırlı kullanıcı grubuna verin.
6. Loglarda kimlik/payload bulunmadığını ve SignalR mesajlarının hassas veri taşımadığını kontrol edin.
7. Yedekleme, outbox izleme, müdahale ve mutabakat sorumlularını belirleyin.
8. Production'da önce global canlı kilidi, sonra yalnızca pilot tesis kilidini kontrollü değişiklik kaydıyla açın.
9. İlk gerçek bildirimden önce kurum onayı alın; sentetik veya gerçek kişisel veriyi kamuya açık/test dışı uçlara deneme amacıyla göndermeyin.

## Bilgi bekleyen noktalar

Jandarma SOAP metotları ve veri sözleşmeleri resmî WSDL snapshot'ı olmadan üretilmemiştir. EGM kolonları güncel resmî şablon olmadan tanımlanmamıştır. `ParametreListele` benzeri salt-okunur operasyon ancak resmî sözleşme incelendikten sonra adaptera eklenebilir. Bunlar sağlanana kadar ortak domain, fake connector, SQL outbox, API ve UI kullanılabilir; canlı entegrasyon kapalı kalır.
