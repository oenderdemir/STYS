## BU DOSYA SADECE DEVELOPMENT/TEST ORTAMI IÇİNDİR.

# Muhasebe Smoke Test Seed Rehberi

Bu rehber, Faz N-1'de tanımlanan runtime smoke testlerinin kontrollü test/dev ortamında hazırlanması için manuel ve production-safe adımları içerir.

## Kapsam

- Test tesisi oluşturma
- Test kullanıcı scope hazırlığı
- Açık ve kapalı muhasebe dönemi oluşturma
- Minimum muhasebe hesap planı hazırlığı
- Cari kart, banka hesabı ve yetkili kişi seed'i
- Satış belgesi, tahsilat/ödeme ve rapor test verisi hazırlığı
- Yetkisiz tesis görünürlük testi için scope dışı kayıt

## İlgili Entity / Tablo Grupları

Bu rehber doğrudan SQL seed script'i yerine ekran bazlı hazırlık yaklaşımını önerir. Hazırlıkta dikkate alınacak ana entity grupları:

- `Tesisler`
- `KullaniciTesisSahiplikleri`
- `TesisMuhasebecileri`
- `MuhasebeDonemler`
- `MuhasebeHesapPlanlari`
- `Hesaplar`
- `KasaBankaHesaplari`
- `CariKartlar`
- `CariKartYetkiliKisileri`
- `CariHareketler`
- `TahsilatOdemeBelgeleri`
- `SatisBelgeleri`
- `SatisBelgesiSatirlari`
- `MuhasebeFisler`
- `MuhasebeFisSatirlari`
- `MuhasebeHesapBakiyeleri`

## Manuel Hazırlık Adımları

### 1. Test Tesisi

- `TEST MUHASEBE TESISI` adıyla bir tesis oluştur.
- Test kullanıcısına bu tesis için erişim ver.
- Tesisin üretim verisiyle karışmaması için ad prefix'i sabit tut.

### 2. Test Kullanıcısı

- Muhasebe yetkili bir kullanıcı oluştur.
- Kullanıcıyı `TEST MUHASEBE TESISI` scope'una ekle.
- Yetkisiz tesis testi için aynı kullanıcıya ikinci bir tesis scope'u verme.

### 3. Açık / Kapalı Dönem

- Bugünün tarihini kapsayan bir `Açık/Aktif` muhasebe dönemi oluştur.
- Geçmiş tarihli bir `Kapalı/Kilitli` dönem oluştur.
- Kapalı dönem, smoke testte fiş oluşturma ve iptal engeli için kullanılacak.

### 4. Minimum Hesap Planı

En az aşağıdaki hesapları hazırla:

- Kasa/Banka hesabı
- Alıcı/Satıcı cari hesap
- Gelir hesabı
- KDV hesabı
- Stok/Hizmet hesabı
- İndirim, ÖTV, ÖİV ve konaklama vergisi için gereken hesaplar

### 5. Cari Kart

- `TEST CARI MUSTERI` oluştur.
- Gerekiyorsa `TEST CARI TEDARIKCI` oluştur.
- Cari karta en az 1 banka hesabı ve 1 yetkili kişi ekle.
- Banka hesabı ve yetkili kişi verileri de `TEST_` prefix'li olmalı.

### 6. Satış Belgesi

- En az 1 satış belgesi oluştur.
- En az 1 satır ekle.
- KDV ve indirim alanlarını doldur.
- ÖTV / ÖİV / konaklama vergisi alanlarını küçük ve kontrollü değerlerle dene.
- Belgeyi onaylı muhasebe fişi oluşturacak şekilde kurgula.

### 7. Tahsilat / Ödeme

- Açık bir cari hareket oluştur.
- Bu hareketi kapatacak en az 1 tahsilat/ödeme belgesi hazırla.
- İptal senaryosunda kapama geri alınabilir olmalı.

### 8. Rapor Verisi

- En az 1 onaylı muhasebe fişi oluştur.
- Bu fişin yevmiye, muavin ve mizan raporlarında görünmesini sağla.

### 9. Yetkisiz Tesis Kayıtları

- Test kullanıcısının göremeyeceği ikinci bir tesis veya scope dışı kayıt oluştur.
- Bu kayıtlar liste/detay endpointlerinde dönmemeli.

## Seed Güvenlik Kuralları

- Production’da otomatik çalıştırma yok.
- Destructive `DELETE` yok.
- Önce `SELECT` / `IF NOT EXISTS` yaklaşımı tercih edilir.
- Geri alma işlemleri iptal / ters kayıt akışlarıyla yapılır.
- SQL script gerekirse ayrıca `TEST/DEV ONLY` etiketiyle ve yorumlu rollback notlarıyla hazırlanır.

## Not

Bu rehber, otomatik seed mekanizması yerine manuel ve kontrollü hazırlık için tasarlanmıştır. Script ihtiyacı sonraki fazda ayrıca ele alınabilir.
## Faz Q - Development/Test Smoke Seed Rehberi

BU SCRIPT SADECE DEVELOPMENT/TEST ORTAMI İÇİNDİR.

### Amaç
- Muhasebe smoke testleri için gerekli minimum test verisini güvenli ve idempotent şekilde hazırlamak.
- Production ortamında otomatik seed çalıştırmamak.

### Seed Noktası
- `POST /ui/muhasebe/dev-tools/seed-smoke-test-data`
- Endpoint yalnızca `Development` ve `Test` ortamlarında aktif.
- `TEST_` ve `TEST MUHASEBE` önekleriyle çalışan kayıtlar oluşturur veya yeniden kullanır.

### Hazırlanan Veriler
- `TEST MUHASEBE TESISI`
- `TEST MUHASEBE YETKISIZ TESISI`
- `muhasebe-admin` veya `admin` hesabı için tesis scope ataması
- Açık ve kapalı muhasebe dönemi
- `TEST-` önekli muhasebe hesap planları
- `TEST_CARI_MUSTERI` ve `TEST_CARI_TEDARIKCI`
- Cari yetkili kişi kayıtları
- Cari kart banka hesapları
- Kasa ve banka hesapları
- Açık cari hareket
- Onaylı muhasebe fişi
- Yevmiye sayacı ve muhasebe bakiye satırları

### Test/Dev Kullanım Notu
- Önce identity ortamında `muhasebe-admin` veya `admin` hesabı bulunmalı.
- Aynı seed tekrar çalıştırıldığında duplicate oluşturmadan mevcut kayıtları yeniden kullanır.
- Production'da endpoint map edilmez.

### Manuel Smoke Tamamlama
- Satış belgesi ve tahsilat/ödeme UI akışları hazırlanan test verisi üzerinde çalıştırılmalıdır.
- Yetkisiz tesis görünürlüğü ikinci test tesis üzerinde doğrulanmalıdır.

### Rollback Notu
- Seed script destructive değildir.
- Geri almak için test/dev veritabanı silme yerine ilgili test kayıtları temizlenmelidir.
