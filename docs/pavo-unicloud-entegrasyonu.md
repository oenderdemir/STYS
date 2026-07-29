# PAVO UniCloud Entegrasyonu

PAVO entegrasyonu ödeme bazında isteğe bağlıdır. Bir kredi kartı/POS hesabına terminal
tanımlanmış olsa bile mevcut manuel ödeme akışı değişmez. Kullanıcı rezervasyon ödeme
ekranında açıkça **PAVO ile Tahsil Et** seçerse UniCloud akışı başlar.

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
2. PAVO terminal adı, cihaz seri numarası ve sabit bir fingerprint girip kaydedin.
3. **Eşleşme Başlat** seçeneğini kullanın.
4. Üretilen kodu POS cihazında onaylayın.
5. **Eşleşmeyi Kontrol Et** ile durumun `Eşleşmiş` olduğunu doğrulayın.

Terminal tanımı yoksa veya terminal aktif/eşleşmiş değilse rezervasyon ekranında PAVO
butonu gösterilmez; manuel ödeme bugünkü haliyle çalışır.

## Finansal kayıt sırası

PAVO işlemi ilk olarak `entegrasyon.PavoOdemeIslemleri` tablosunda bekleyen işlem olarak
oluşturulur. Kart işlemi PAVO tarafından kesin başarılı bildirilmeden `RezervasyonOdeme`,
`TahsilatOdemeBelgesi` ve POS valör kaydı oluşturulmaz.

Başarılı sonuçtan sonra mevcut rezervasyon ödeme ve muhasebe servisi çağrılır. PAVO işlem
bağlantısı unique index ile korunur; aynı PAVO işlemi iki kez muhasebeleştirilemez.

## Saha doğrulaması

Canlıya geçmeden önce PAVO demo ortamında aşağıdaki senaryolar doğrulanmalıdır:

- başarılı satış;
- kart reddi ve kullanıcının işlemi terk etmesi;
- POS meşgulken kuyruk davranışı;
- tarayıcı kapatılıp işlem yeniden sorgulandığında sonuç;
- aynı ödeme butonuna art arda basılması;
- PAVO başarılıyken yerel muhasebe doğrulama hatası;
- tesis/POS internet kesintisi ve sonradan mutabakat.
