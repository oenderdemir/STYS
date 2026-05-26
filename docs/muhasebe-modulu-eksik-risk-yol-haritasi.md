# Muhasebe Modülü Eksik/Risk Listesi ve Yol Haritası

## 1. Mevcut Durum
- Satış faturası muhasebe fişi var.
- Alış faturası muhasebe fişi var.
- Satış/alış tevkifat fişi var.
- Satış/alış iade fişi var.
- Stok giriş/çıkış hareketleri var.
- Cari hareket oluşturma var.
- Tahsilat/ödeme ile cari kapama var.
- Cari bakiye/açık-kapalı hareket takibi var.
- KDV/tevkifat raporları var.
- Kabul test senaryoları hazır.

## 2. Kalan Fonksiyonel Eksikler

### 2.1 Tahsilat/Ödeme Geri Alma
- Kapama yapılmış tahsilat/ödeme belgesinin iptal/geri alma mekanizması eklendi.
- Kapama hareketi iptal edilir, ilişkili fatura hareketinin kapanan tutarı geri alınır.
- Faz 87 tamamlandı.

### 2.2 Muhasebe Fişi İptal/Ters Kayıt Etki Analizi
- Fiş iptal/ters kayıt yapıldığında ilişkili stok/cari hareket etkisi net değil.
- Önerilen faz: Faz 88 — Muhasebe Fişi İptal/Ters Kayıt Etki Analizi

### 2.3 Stok Miktar Yeterlilik Kontrolü
- Satış/alış iade çıkışlarında stok yeterlilik kontrolü net değil.
- Önerilen faz: Faz 89 — Stok Bakiye ve Negatif Stok Kontrolü

### 2.4 Cari Alt Hesap Entegrasyonu
- Cari kart bazlı alt hesap kullanımı netleşmeli.
- Önerilen faz: Faz 90 — Cari Kart Muhasebe Alt Hesap Entegrasyonu

### 2.5 Ürün/Hizmet Hesap Eşlemesi
- Fallback hesap kullanımı yerine daha net eşleme yönetimi gerekebilir.
- Önerilen faz: Faz 91 — Ürün/Hizmet Muhasebe Hesap Eşleme İyileştirmesi

### 2.6 Tevkifatlı İade
- Tevkifatlı iade faturaları kapalı.
- Önerilen faz: Faz 92 — Tevkifatlı İade Muhasebe Analizi

### 2.7 KDV/Tevkifat Beyanname Çıktısı
- Raporlar var, beyanname formatı yok.
- Önerilen faz: Faz 93 — KDV/Tevkifat Rapor Çıktıları

### 2.8 Yetki ve Tesis Güvenliği
- Uçtan uca güvenlik testi ihtiyacı devam ediyor.
- Önerilen faz: Faz 94 — Muhasebe Yetki/Tesis Güvenlik Testleri

### 2.9 Performans ve Büyük Veri
- Büyük veri altında rapor performansı ve export stratejisi eksik.
- Önerilen faz: Faz 95 — Muhasebe Rapor Performans İyileştirmesi

### 2.10 Kullanıcı Deneyimi
- Tahsilat/ödeme cari hareket seçimi daha anlaşılır hale getirilebilir.
- Önerilen faz: Faz 96 — Tahsilat/Ödeme Cari Hareket Seçim UI

## 3. Teknik Riskler
- Transaction bütünlüğü
- Duplicate kayıt kontrolleri
- İptal/geri alma eksikleri
- Tevkifatlı iade kapalı olması
- Rapor performansı
- Genel hesap fallback kullanımı
- Manuel `CariKartId` null belgeler
- LocalStorage tesis manipülasyonu
- Frontend validasyon ile backend validasyon farkları
- Migration geçmişi ve ortam uyumu

## 4. Muhasebe Doğruluk Riskleri
- 120/320 alt hesap ayrımı
- 191/391 yönleri
- 360/136 tevkifat hesapları
- 610 satış iade hesabı
- Alış iade gider/stok hesabı alacak yönü
- KDV istisna satırlarının rapor etkisi
- Tevkifatın cari GenelToplam’a etkisi
- Stok hareketi ile muhasebe fişi tutar uyumu

## 5. Önerilen Sonraki Fazlar
- Faz 88 — Muhasebe Fişi İptal/Ters Kayıt Etki Analizi
- Faz 89 — Stok Bakiye ve Negatif Stok Kontrolü
- Faz 90 — Cari Kart Muhasebe Alt Hesap Entegrasyonu
- Faz 91 — Ürün/Hizmet Muhasebe Hesap Eşleme İyileştirmesi
- Faz 92 — Tevkifatlı İade Muhasebe Analizi
- Faz 93 — KDV/Tevkifat Rapor Çıktıları
- Faz 94 — Muhasebe Yetki/Tesis Güvenlik Testleri
- Faz 95 — Muhasebe Rapor Performans İyileştirmesi
- Faz 96 — Tahsilat/Ödeme Cari Hareket Seçim UI

## 6. Öncelik Önerisi
1. Faz 88 — Muhasebe Fişi İptal/Ters Kayıt Etki Analizi
2. Faz 89 — Stok Bakiye ve Negatif Stok Kontrolü
3. Faz 90 — Cari Kart Muhasebe Alt Hesap Entegrasyonu
4. Faz 96 — Tahsilat/Ödeme Cari Hareket Seçim UI
5. Faz 93 — KDV/Tevkifat Rapor Çıktıları

Gerekçe:
- Önce veri bütünlüğünü bozan geri alma/iptal/stok negatifliği riskleri kapatılmalı.
- Sonra muhasebe doğruluğunu artıran alt hesap/eşleme işleri yapılmalı.
- UI iyileştirme daha sonra yapılabilir.
