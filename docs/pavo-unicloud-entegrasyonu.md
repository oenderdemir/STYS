# PAVO UniCloud Entegrasyonu

Fiziksel POS entegrasyonu ödeme bazında isteğe bağlıdır. Bir kredi kartı/POS hesabına terminal
tanımlanmış olsa bile mevcut manuel ödeme akışı değişmez. Kullanıcı rezervasyon ödeme
ekranında açıkça seçtiği sağlayıcı ile tahsilatı başlatırsa cihaz akışı başlar.

## Sağlayıcı bağımsız mimari

Terminal, ödeme işlemi, rezervasyon doğrulaması ve muhasebeleştirme `Entegrasyonlar/Pos`
altındaki ortak katmanda yönetilir. PAVO yalnızca `IPosOdemeSaglayicisi` sözleşmesini
uygulayan ilk adaptördür. Yeni bir marka eklenirken genel ödeme servisi veya muhasebe
akışı kopyalanmaz; yeni sağlayıcı adaptörü DI'a kaydedilir.

Ortak API rotası `/ui/pos`, tablolar ise `entegrasyon.PosTerminaller` ve
`entegrasyon.PosOdemeIslemleri` adlarını kullanır. Her terminalde bir `SaglayiciKodu`
bulunur. Eski PAVO kayıtları migration sırasında veri kaybı olmadan bu tablolara taşınır
ve sağlayıcı kodları `PAVO` olarak atanır.

## Sunucu ayarları

Kimlik bilgileri kaynak koda veya veritabanına yazılmaz. Deployment ortamında aşağıdaki
environment variable değerleri tanımlanmalıdır:

```text
Pavo__Enabled=true
Pavo__BaseUrl=https://overunipos-test-integration-gateway.overtech.com.tr
Pavo__AppToken=<PAVO tarafından verilen değer>
Pavo__ApiKey=<PAVO tarafından verilen değer>
Pavo__RequestTimeoutSeconds=30
```

Canlı ortam base URL'si:

```text
https://overunipos-integration-gateway.pavopay.dev
```

## Terminal kurulumu

1. Muhasebe → Kasa/Banka/POS Hesapları ekranında kredi kartı/POS hesabını açın.
2. Sağlayıcı olarak PAVO'yu seçin; terminal adı, cihaz seri numarası ve sabit bir fingerprint girip kaydedin.
3. **Eşleşme Başlat** seçeneğini kullanın.
4. Üretilen kodu POS cihazında onaylayın.
5. **Eşleşmeyi Kontrol Et** ile durumun `Eşleşmiş` olduğunu doğrulayın.

Terminal tanımı yoksa veya terminal aktif/eşleşmiş değilse rezervasyon ekranında fiziksel POS
butonu gösterilmez; manuel ödeme bugünkü haliyle çalışır.

## Finansal kayıt sırası

POS işlemi ilk olarak `entegrasyon.PosOdemeIslemleri` tablosunda bekleyen işlem olarak
oluşturulur. Kart işlemi PAVO tarafından kesin başarılı bildirilmeden `RezervasyonOdeme`,
`TahsilatOdemeBelgesi` ve POS valör kaydı oluşturulmaz.

Başarılı sonuçtan sonra mevcut rezervasyon ödeme ve muhasebe servisi çağrılır. PAVO işlem
bağlantısı unique index ile korunur; aynı POS işlemi iki kez muhasebeleştirilemez.

## Arka plan durum takibi

`PosOdemeDurumTakipHostedService`, bekleyen POS işlemlerini tarayıcı açık olmasa da
periyodik olarak sorgular. Cloud uygulamasının birden fazla instance'ı çalıştığında aynı
işlemin eşzamanlı sorgulanmasını engellemek için veritabanında süreli lease kullanılır.
Sağlayıcının durum kodu, son sorgu zamanı, deneme sayısı ve geçici sorgu hatası işlem
kaydında tutulur.

Takip süresi veya deneme sınırı aşılırsa işlem otomatik başarısız sayılmaz;
`MutabakatGerekli` durumuna alınır. Böylece cevabı kaybolmuş fakat karttan çekilmiş bir
işlem yanlışlıkla başarısız olarak kapatılmaz. Ayarlar `PosOdemeTakip` appsettings
bölümünden değiştirilebilir.

## Saha doğrulaması

Canlıya geçmeden önce PAVO demo ortamında aşağıdaki senaryolar doğrulanmalıdır:

- başarılı satış;
- kart reddi ve kullanıcının işlemi terk etmesi;
- POS meşgulken kuyruk davranışı;
- tarayıcı kapatılıp işlem yeniden sorgulandığında sonuç;
- aynı ödeme butonuna art arda basılması;
- PAVO başarılıyken yerel muhasebe doğrulama hatası;
- tesis/POS internet kesintisi ve sonradan mutabakat.
