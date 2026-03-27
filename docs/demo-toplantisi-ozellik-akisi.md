# STYS Demo Toplantisi Icin Ozellik Akis Dokumani

Bu dokuman, canli demo toplantisinda hangi ozelliklerin hangi sirayla gosterilecegini ve her adimda hangi mesajin verilmesi gerektigini tarif eder.

## Demo Amaci

Izleyiciye sadece ekran gostermek degil, STYS'nin kurumsal operasyonu nasil duzenledigi anlatilmalidir.

Demo su uc basligi ispatlamalidir:

- sistem sadece rezervasyon almiyor, operasyonu yonetiyor
- gercek hayattaki istisna durumlarini destekliyor
- admin ve yonetim ekipleri icin izlenebilirlik sagliyor

## Demo Suresi

- Kisa demo: 10-15 dakika
- Standart demo: 20-30 dakika
- Derinlemesine demo: 45+ dakika

Bu akis 20-30 dakikalik standart toplantiya gore hazirlanmistir.

## Demo Oncesi Hazirlik

Asagidaki veri hazir olmali:

- birden fazla tesis
- farkli tipte odalar
- paylasimli oda ornegi
- aktif rezervasyonlar
- check-in bekleyen bir rezervasyon
- check-out bekleyen bir rezervasyon
- ek hizmet tanimlari ve tarifeleri
- temizlik bekleyen en az bir oda
- arizali veya bloklu bir oda ornegi

## Demo Akisi

## 1. Acilis ve Cevaplanan Problem

**Gosterilecek**
- ana uygulama yapisi
- temel menuler

**Mesaj**
STYS, rezervasyon, oda plani, odeme, temizlik ve operasyon akisini tek platformda toplar.

**Soruya cevap**
Bu sistem neyi bir araya getiriyor?

## 2. Rezervasyon Arama ve Senaryo Uretimi

**Gosterilecek**
- rezervasyon arama ekrani
- kisi sayisi, tesis, tarih, tip secimleri
- birden fazla senaryo sonucu

**Vurgu**

- sistem otomatik alternatif senaryo olusturur
- minimum oda mantigini dikkate alir
- gerekirse segmentli konaklama uretir
- gereksiz segment olusturmaz

**Mesaj**
Personel elde hesap yapmadan uygun alternatifleri sistemden alir.

## 3. Rezervasyon Kaydi ve Konaklayan Plani

**Gosterilecek**
- secilen senaryodan rezervasyon olusturma
- konaklayan plani dialogu
- kisi bazli oda / yatak atama
- katilim durumu secimi

**Vurgu**

- kim hangi odada kaliyor?
- paylasimli odada hangi yatakta kaliyor?
- misafir geldi mi, bekleniyor mu, gelmedi mi?

**Mesaj**
Rezervasyon bir kayittir; fiili konaklama ayri olarak yonetilir.

## 4. Check-in Sureci

**Gosterilecek**
- check-in butonu
- plan eksikse veya oda hazir degilse uyarilar
- plan tamamlaninca check-in

**Vurgu**

- plansiz check-in engellenir
- hazir olmayan oda icin operasyonel kontrol vardir
- check-in sadece teknik degil operasyonel bir akistir

## 5. Bekleniyor / Gelmedi Senaryosu

**Gosterilecek**
- 4 kisilik rezervasyonda 1 kisi geldi, digerleri bekleniyor
- sonra bir kisiyi `Gelmedi` yapma

**Vurgu**

- kapasite tekrar serbest kalir
- fiili gelenler ile rezervasyon kaydi ayrisir
- sistem gercek hayattaki no-show durumunu yonetir

**Mesaj**
Bu nokta klasik rezervasyon sistemlerinden ayrilan kritik bir ozelliktir.

## 6. Odeme ve Ek Hizmetler

**Gosterilecek**
- odeme ekrani
- parcali odeme
- ek hizmet ekleme
- varsayilan fiyat + manuel override

**Ornek**

- kurutemizleme
- ayakkabi boyama
- transfer

**Vurgu**

- hizmetler kisiye yazilir
- fiyat tarifesi vardir
- gerekirse o anda fiyat degistirilebilir

## 7. Check-out Sureci

**Gosterilecek**
- kalan odeme varken check-out engeli
- odeme tamamlandiktan sonra check-out

**Vurgu**

- finansal kontrol isletme icin zorunlu tutulur
- check-out sadece resepsiyon aksiyonu degil, tahsilat kapama adimidir

## 8. Oda Temizlik Sureci

**Gosterilecek**
- check-out sonrasi odanin kirli duruma gecmesi
- temizlik ekraninda gorevlilere dusen odalar
- temizleniyor ve hazir durumlari

**Mesaj**
Konaklama operasyonu resepsiyonda bitmez; housekeeping ile devam eder.

## 9. Oda Bakim / Ariza ve Oda Degisimi

**Gosterilecek**
- arizali oda kaydi
- etkilenen rezervasyonlar
- alternatif oda secimi
- gerekiyorsa check-in sonrasi oda degisimi

**Vurgu**

- sistem sorunlu odayi tespit eder
- alternatif secenek onerir
- operasyonel kesintide rezervasyon bozulmadan surec devam eder

## 10. Dashboard ve Raporlama

**Gosterilecek**
- bugun check-in yapacaklar
- bugun check-out yapacaklar
- bos / dolu kapasite
- odeme raporu export

**Mesaj**
Yonetim, anlik operasyonu ve gelir akisini tek yerden gorur.

## 11. Yetki ve Erisim Teshisi

**Gosterilecek**
- erisim teshis ekrani
- bir kullanici secip neden ekran goremiyor gostermek

**Ornek soru**

- neden yeni kayit ekleyemiyor?
- neden bu tesiste guncelleyemiyor?

**Mesaj**
Bu ozellik admin ve destek ekipleri icin kritik operasyonel hiz kazandirir.

## 12. Kapanis

**Gosterilecek**
- genel ozet
- uyarlama / pilot / canliya gecis mesaji

**Kapanis mesaji**
STYS; rezervasyon, operasyon, housekeeping, finans ve yetki yonetimini tek yapida birlestiren kurumsal bir platformdur.

## Demo Sirasinda Kullanilacak Konusma Kaliplari

### Acilis

"Burada sadece rezervasyon almiyoruz. Rezervasyonun operasyonel hayata nasil donustugunu birlikte gorecegiz."

### Senaryo Gosterirken

"Sistem uygunlugu, kapasiteyi ve kurallari ayni anda dikkate alarak secenekler uretir."

### Konaklayan Planinda

"Rezervasyon ile fiili konaklama ayridir. Bu ayrim saha operasyonu icin kritik."

### Odeme Ekraninda

"Ek hizmetler ve parcali odeme mantigi gercek isletme akisina uygun tasarlandi."

### Yetki Teshisinde

"Bu ekran sayesinde destek ekibi neden erisim olmadigini kod bakmadan anlayabilir."

## Demo Sonrasi Onerilen Takip Sorulari

- Kurumunuzda paylasimli oda veya yatak bazli planlama ihtiyaci var mi?
- Tesis bazli yetkilendirme sizin organizasyonunuza uygun mu?
- Ek hizmet ve odeme akisi mevcut operasyonunuza ne kadar yakin?
- Housekeeping veya ariza surecinde bugun en cok zorlandiginiz nokta nedir?

## Demo Basari Kriteri

Toplanti sonunda izleyici su uc noktayi net olarak anlamis olmali:

- STYS sadece rezervasyon degil, operasyon sistemidir
- istisna durumlari yonetebilir
- kurum yapisina uygun yetki ve tesis kapsami mantigi vardir
