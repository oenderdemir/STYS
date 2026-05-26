# Muhasebe Modülü Kullanıcı Kabul Test Senaryoları

## 1. Ön Koşullar
- Kullanıcının muhasebe modülü yetkileri olmalı.
- Kullanıcının en az bir tesise yetkisi olmalı.
- Muhasebe tesis seçimi yapılmış olmalı.
- Açık muhasebe dönemi bulunmalı.
- Hesap planında gerekli hesaplar bulunmalı:
  - 120
  - 320
  - 600
  - 610
  - 153
  - 191
  - 391
  - 740/770
- Tevkifat hesap eşlemeleri tanımlı olmalı.
- Cari kartlar tanımlı olmalı.
- Taşınır kart/depo tanımlı olmalı.

## 2. Satış Faturası Testleri

Senaryo 2.1 — Normal satış faturası
Adımlar:
1. Satış Belgeleri ekranına gir.
2. Satış faturası oluştur.
3. Müşteri cari seç.
4. Ürün/hizmet satırı ekle.
5. KDV oranı gir.
6. Muhasebe onayına gönder.
7. Muhasebe onayla.
8. Muhasebe fişi oluştur.

Beklenen:
- 120 borç oluşur.
- 600 alacak oluşur.
- 391 alacak oluşur.
- Cari hareket müşteri borç yönünde oluşur.
- Stok satırı varsa stok çıkış hareketi oluşur.

Senaryo 2.2 — Hizmet satış faturası
Beklenen:
- Muhasebe fişi oluşur.
- Cari hareket oluşur.
- Stok hareketi oluşmaz.

Senaryo 2.3 — Depo seçilmemiş stok satırı
Beklenen:
- Stok çıkışı gerektiren satırda depo yoksa açık hata alınır.

## 3. Tevkifatlı Satış Faturası Testleri

Senaryo 3.1 — Tevkifatlı satış faturası
Beklenen:
- 120 borç = GenelToplam
- Tevkifat hesabı borç = TevkifatTutari
- 600 alacak = Matrah
- 391 alacak = KDV
- Cari hareket GenelToplam kadar borç oluşur.

Senaryo 3.2 — Tevkifat hesap eşlemesi yok
Beklenen:
- Muhasebe fişi oluşturulmaz.
- Kullanıcıya açık hata gösterilir.

## 4. Alış Faturası Testleri

Senaryo 4.1 — Normal alış faturası
Beklenen:
- 153/740/770 borç oluşur.
- 191 borç oluşur.
- 320 alacak oluşur.
- Cari hareket tedarikçi alacak yönünde oluşur.
- Stok satırı varsa stok giriş hareketi oluşur.

Senaryo 4.2 — Hizmet alış faturası
Beklenen:
- Gider hesabına borç yazılır.
- Stok hareketi oluşmaz.

Senaryo 4.3 — Depo seçilmemiş stok alış satırı
Beklenen:
- Stok giriş hareketi oluşturulmaz.
- Kullanıcıya açık hata verilir.

## 5. Tevkifatlı Alış Faturası Testleri

Senaryo 5.1 — Tevkifatlı alış faturası
Beklenen:
- 153/740/770 borç oluşur.
- 191 borç oluşur.
- 320 alacak oluşur.
- Tevkifat hesabı alacak oluşur.
- Cari hareket GenelToplam kadar alacak oluşur.

Senaryo 5.2 — Alış tevkifat hesap eşlemesi yok
Beklenen:
- Muhasebe fişi oluşturulmaz.
- Açık hata gösterilir.

## 6. Satış İade Faturası Testleri

Senaryo 6.1 — Satış iade faturası
Beklenen:
- 610 borç oluşur.
- 391 borç oluşur.
- 120 alacak oluşur.
- Cari hareket müşteri alacak yönünde oluşur.
- Stok satırı varsa stok giriş hareketi oluşur.

## 7. Alış İade Faturası Testleri

Senaryo 7.1 — Alış iade faturası
Beklenen:
- 320 borç oluşur.
- 153/740/770 alacak oluşur.
- 191 alacak oluşur.
- Cari hareket tedarikçi borç yönünde oluşur.
- Stok satırı varsa stok çıkış hareketi oluşur.

## 8. Kapalı Kapsam Testleri

Senaryo 8.1 — Proforma fiş oluşturma
Beklenen:
- Muhasebe fişi oluşturulamaz.

Senaryo 8.2 — Legacy IadeFaturasi
Beklenen:
- Muhasebe fişi oluşturulamaz.

Senaryo 8.3 — Tevkifatlı iade
Beklenen:
- Muhasebe fişi oluşturulamaz.
- “Tevkifatlı iade faturaları henüz desteklenmiyor.” benzeri açık hata alınır.

## 9. Cari Hareket ve Kapama Testleri

Senaryo 9.1 — Satış faturası cari hareketi
Beklenen:
- Müşteri cari hareketi borç yönünde oluşur.
- KalanTutar belge toplamı kadar olur.
- KapandiMi false olur.

Senaryo 9.2 — Kısmi tahsilat
Beklenen:
- KapananTutar artar.
- KalanTutar azalır.
- KapandiMi false kalır.

Senaryo 9.3 — Tam tahsilat
Beklenen:
- KalanTutar 0 olur.
- KapandiMi true olur.

Senaryo 9.4 — Fazla kapama
Beklenen:
- İşlem engellenir.
- Açık hata gösterilir.

Senaryo 9.5 — Kapama yapılmış tahsilat/ödeme belgesini güncelleme/silme
Beklenen:
- Güncelleme ve silme engellenir.

## 10. Cari Bakiye Testleri

Senaryo 10.1 — Cari bakiye özeti
Beklenen:
- Toplam borç doğru görünür.
- Toplam alacak doğru görünür.
- Bakiye doğru hesaplanır.
- Bakiye yönü doğru görünür.

Senaryo 10.2 — Açık hareketler
Beklenen:
- KapandiMi=false ve KalanTutar>0 hareketler listelenir.

Senaryo 10.3 — Kapanan hareketler
Beklenen:
- KapandiMi=true hareketler listelenir.

## 11. KDV Rapor Testleri

Senaryo 11.1 — Satış KDV raporu
Beklenen:
- Satış KDV’si hesaplanan KDV toplamına eklenir.

Senaryo 11.2 — Alış KDV raporu
Beklenen:
- Alış KDV’si indirilecek KDV toplamına eklenir.

Senaryo 11.3 — Satış iade etkisi
Beklenen:
- Satış iade KDV’si net KDV hesabında düşülür.

Senaryo 11.4 — Alış iade etkisi
Beklenen:
- Alış iade KDV’si indirilecek KDV etkisinden düşülür.

Senaryo 11.5 — KDV istisnası
Beklenen:
- İstisna satırı istisna özetinde görünür.

## 12. Tevkifat Rapor Testleri

Senaryo 12.1 — Satış tevkifat raporu
Beklenen:
- Satış tevkifat tutarı satış tevkifat toplamında görünür.

Senaryo 12.2 — Alış tevkifat raporu
Beklenen:
- Alış tevkifat tutarı alış tevkifat toplamında görünür.

Senaryo 12.3 — Oran bazlı tevkifat
Beklenen:
- Pay/payda bazında gruplanır.

## 13. Tesis ve Yetki Testleri

Senaryo 13.1 — Tesis seçilmeden muhasebe ekranına giriş
Beklenen:
- Kullanıcıdan tesis seçmesi istenir.

Senaryo 13.2 — Yetkisiz tesis verisi
Beklenen:
- Yetkisiz tesisin belgeleri/hareketleri/raporları görünmez.

Senaryo 13.3 — LocalStorage manipülasyonu
Beklenen:
- Backend yetkisiz tesis işlemini reddeder.

## 14. Duplicate Testleri

Senaryo 14.1 — Aynı belgeye ikinci muhasebe fişi
Beklenen:
- Engellenir.

Senaryo 14.2 — Aynı belgeye ikinci stok hareketi
Beklenen:
- Engellenir.

Senaryo 14.3 — Aynı tahsilat/ödeme belgesine ikinci cari hareket
Beklenen:
- Engellenir.

## 15. Kabul Kriterleri
- Tüm kritik satış/alış/iade akışları başarılı.
- Stok hareket yönleri doğru.
- Cari hareket yönleri doğru.
- KDV/tevkifat raporları doğru.
- Yetki/tesis kontrolleri çalışıyor.
- Duplicate kayıt oluşmuyor.
- Backend build başarılı.
- Frontend build başarılı.
