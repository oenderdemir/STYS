# Alış Faturası ve Ticari Belge Altyapısı Ön Analizi

Bu doküman, mevcut `SatisBelgesi` altyapısının alış faturası / satın alma faturası ihtiyacını karşılayıp karşılayamayacağını, nerelerde yeterli kaldığını ve nerelerde yetersiz kalacağını değerlendirir.

## 1. Mevcut SatisBelgesi altyapısı analizi

- `SatisBelgesiTipi` enumu şu an `FaturaTaslagi`, `SatisFaturasi`, `IadeFaturasi`, `Proforma` değerlerini içeriyor.
- Enum DB tarafında `int` olarak tutuluyor; snapshot dosyasında `BelgeTipi` alanı `int` görünüyor.
- `SatisBelgesi` entity'si satışa isim olarak dar görünse de satır bazlı KDV, istisna, tevkifat, depo ve taşınır kart alanlarını taşıyabilecek kadar genel.
- Satır entity'sinde `DepoId`, `TasinirKartId`, `Birim`, `Miktar`, `BirimFiyat`, `IndirimTutari`, `KdvUygulamaTipi`, `KdvIstisnaTanimId`, `TevkifatPay`, `TevkifatPayda` alanları mevcut.
- Frontend formu da satış belgesi için fatura benzeri satır yapısına dönüşmüş durumda.
- `SatisBelgesiMuhasebeFisService` ise açık şekilde satış odaklı; 120 / 600 / 391 kurgusunu kullanıyor ve `IadeFaturasi` ile `Proforma` için fiş üretmeyi reddediyor.

## 2. Alış faturası ihtiyacı

- Alış faturası, satış belgesinin ters yönlü bir kopyası değildir.
- Alış faturasında cari kart tipi tedarikçi olmalı, KDV hesabı 191 tarafına kaymalı ve muhasebe fişi borç/alacak yönü değişmelidir.
- Ürün/taşınır satırlarında depo girişi oluşabilir; hizmet satırlarında depo hareketi oluşmamalıdır.
- Alış iade faturası ile satış iade faturası ters yönlü stok hareketleri üretmelidir.
- Tevkifat ve KDV istisnası alış faturasında da ayrı ele alınmalıdır.

## 3. Alternatif A/B/C değerlendirmesi

### Alternatif A - Ayrı Alış Faturaları Modülü

Artılar:
- Satış ve alış iş kuralları birbirine karışmaz.
- Muhasebe ve stok davranışı daha net ayrılır.
- Tedarikçi, alış iadesi, 191 KDV ve stok giriş akışı için özel validasyonlar kolaylaşır.

Eksiler:
- Tekrarlayan UI ve servis kodu artar.
- Fatura satırı / KDV / tevkifat / depo / cari altyapısı kopyalanabilir.
- Kullanıcı deneyiminde iki ayrı modül doğar.

### Alternatif B - Mevcut SatisBelgesi altyapısını ticari belgeye genişletmek

Artılar:
- Mevcut satır yapısı, KDV, tevkifat, depo ve taşınır kart desteği yeniden kullanılabilir.
- Tek UI ve tek kayıt altyapısı korunur.
- Yeni veritabanı şeması açmadan ilerlemek mümkündür.

Eksiler:
- `SatisBelgesi` adı semantik olarak dar kalır.
- Satışa özel validasyonlar alış için dallanmak zorunda kalır.
- Muhasebe fişi üretim servisleri if/else ağına dönüşebilir.

### Alternatif C - Yeni genel TicariBelge altyapısı

Artılar:
- En doğru domain adı budur.
- Satış, alış, iade ve proforma tek çatı altında doğal biçimde modellenir.

Eksiler:
- Büyük refactor gerekir.
- Veri taşıma / migration ihtimali yükselir.
- Bu aşama için maliyetlidir.

## 4. Önerilen kısa vadeli çözüm

- Kısa vadede mevcut veri yapısı korunabilir; ancak yeni alış davranışı `SatisBelgesiMuhasebeFisService` içine if/else ile doldurulmamalıdır.
- En doğru ara çözüm, ortak fatura alanlarını paylaşan bir ticari belge katmanı düşünmek ve satış/alış muhasebe davranışını strateji bazlı ayırmaktır.
- Kullanıcı tarafında menü ayrı gösterilebilir: `Satış Belgeleri` ve `Alış Belgeleri`.
- Teknik tarafta aynı altyapı kullanılsa bile servis katmanları satış ve alış için ayrılmalıdır.
- Uzun vadede `TicariBelge` adı daha doğru olacaktır.

## 5. Satış / alış / iade belge tipi önerisi

Önerilen belge türleri:

- `SatisFaturasi`
- `AlisFaturasi`
- `SatisIadeFaturasi`
- `AlisIadeFaturasi`
- `Proforma`
- `FaturaTaslagi`

Mevcut `SatisBelgesiTipi` buna genişletilebilir; ama satışa özel isimleme uzun vadede yetersiz kalır.

## 6. Depo / stok hareketi önerisi

- Satırda `DepoId` ve `TasinirKartId` alanları mevcut olduğu için goods/stok satırları teknik olarak taşınabilir.
- Stok hizmet satırları için depo hareketi oluşturulmamalıdır.
- Alış faturasında ürün/taşınır satırları için depo girişi mantığı gereklidir.
- Satış faturasında depo çıkışı mantığı gereklidir.
- Alış iade için çıkış, satış iade için giriş mantığı gerekir.
- Stok hareketinin belge kaydında değil, onay / muhasebe onayı sonrasında üretilmesi daha güvenlidir.

## 7. Muhasebe fişi önerisi

### Satış faturası

- Borç: 120 Cari Alacaklar
- Alacak: 600 Satış Gelirleri
- Alacak: 391 Hesaplanan KDV

### Alış faturası

- Borç: 153 / 740 / ilgili stok-gider hesabı
- Borç: 191 İndirilecek KDV
- Alacak: 320 Satıcılar

### Teknik not

- `MuhasebeAnaHesapKodlari` içinde `CariTedarikci = 320`, `KDVIndirilecek = 191`, `KDVHesaplanan = 391` zaten tanımlı.
- `MuhasebeVergiHesapEsleme` entity'sinde hem `AlisKdvHesapId` hem `SatisKdvHesapId` bulunuyor.
- `MuhasebeFisService` stok hareketi yönüne göre 191 / 391 ayrımını zaten yapıyor.

Bu nedenle alış faturası için muhasebe tarafında altyapı tamamen sıfırdan başlamıyor; fakat satış servisinin içine gömülü tek bir `if` ile çözülmesi doğru olmaz.

## 8. Cari kart / tedarikçi etkisi

- `CariKart` yapısında `CariTipi` alanı vardır ve `CariKartTipleri.Tedarikci` değeri mevcuttur.
- Ayrı bir tedarikçi entity'si yoktur; aynı cari kart altyapısı tedarikçi rolüyle kullanılabilir.
- Alış faturası ekranı cari kart seçimini yalnızca tedarikçi tipleriyle sınırlandırmalıdır.
- Mevcut cari kart verisi buna uygundur.

## 9. KDV / tevkifat etkisi

- Satış faturasında KDV akışı 391 üzerinden ilerler.
- Alış faturasında 191 indirilecek KDV gerekir.
- KDV istisnası alış faturasında da desteklenmelidir; istisnada KDV sıfır olur.
- Tevkifat alış faturasında da gerekliyse ayrıca modellenmelidir.
- Ancak mevcut satış muhasebe servisinde tevkifatlı satış zaten desteklenmiyor; alış için bunu doğrudan aynı servis içinde çözmek risklidir.

## 10. Riskler

- `SatisBelgesiMuhasebeFisService` satışa özel kurgulu olduğu için alış desteği eklendiğinde servis karmaşıklığı hızla artar.
- Satışa özgü isimler alış kullanımını semantik olarak bulanıklaştırır.
- Stok hareketi, KDV, tevkifat ve muhasebe fişi aynı serviste dallanırsa test yüzeyi büyür.
- Alış iade, satış iade, hizmet alış ve stok alış farklı kurallar içerir; tek sınıfta yönetmek zorlaşır.

## 11. Önerilen sonraki fazlar

1. Önce ortak belge çekirdeği için teknik karar ver.
2. Eğer mevcut yapı korunacaksa, satış/alış muhasebe stratejilerini ayır.
3. Alış faturası için ayrı menü ve ayrı kullanıcı akışı belirle.
4. Stok hareketi üretimini onay sonrasına bağla.
5. Sonraki aşamada `TicariBelge` isimlendirme refactor'u planla.

## 12. Sonuç

- Kısa vadede mevcut yapı, küçük bir refactor ile alış faturası desteği verebilir.
- Ancak satış ve alış kuralları giderek ayrışacağı için uzun vadede `TicariBelge` adı daha doğru olacaktır.
- Bu analizde en güvenli yaklaşım, altyapıyı tamamen yeniden yazmadan önce satış/alış muhasebe ve stok stratejilerini ayırmaktır.
