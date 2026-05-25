# Satış Belgesi Kullanıcı Akışı

## Faz 70 — Cari Kart Seçimi, Depo Bazlı Satırlar ve Tevkifat Hazırlığı

Bu fazda Satış Belgeleri ekranındaki yeni/düzenle formu gerçek fatura girişine daha yakın hale getirildi.

### Ana davranış
- Satış belgesi oluştururken cari kart seçilir.
- Cari kart seçenekleri seçili çalışma tesisinden gelir.
- Cari seçildiğinde müşteri snapshot alanları otomatik doldurulur.
- Manuel müşteri bilgisi girişi opsiyonel hale getirildi.
- Form, seçili çalışma tesisi bağlamında çalışır.

### Satır bazlı alanlar
- Her satır için ürün/hizmet veya taşınır/stok kartı seçilebilir.
- Her satır için depo seçilebilir.
- Miktar, birim, birim fiyat, indirim, KDV tipi, KDV oranı ve istisna/tevkifat alanları ayrı ayrı tutulur.
- Satır bazında depo, seçili çalışma tesisine göre gelen depo listesinden seçilir.

### KDV istisna / tevkifat ayrımı
- KDV istisna ile tevkifat aynı şey değildir.
- KDV istisnada KDV hesaplanmaz ve istisna tanımı gerekir.
- Tevkifatta KDV hesaplanır, ancak bunun bir kısmı tevkif edilir.
- UI'da toplam KDV, tevkif edilen KDV, net KDV ve genel toplam ayrı gösterilir.

### Backend durum
- Bu fazda satış belgesi satır modeline `TasinirKartId`, `DepoId`, `Birim`, `IndirimTutari`, `TevkifatPay`, `TevkifatPayda`, `TevkifatTutari` alanları eklendi.
- Satış belgesi entity'sine `CariKartId` eklenmedi; müşteri bilgileri snapshot olarak saklanmaya devam ediyor.
- Muhasebe fişi üretim mantığı değiştirilmedi.
- Tevkifatlı satış belgesi, satış belgesi kaydında destekleniyor; muhasebe fişi tarafındaki otomatik üretim kurgusu ayrı olarak korunuyor.

### Güvenlik ve kapsam
- TesisId her zaman `MuhasebeTesisContextService` üzerinden alınır.
- Cari kart, depo ve taşınır/stok kart listeleri seçili çalışma tesisine göre filtrelenir.
- Frontend localStorage güvenlik sınırı değildir; backend access scope korunur.

### Bilinen sınır
- Otomatik muhasebe fişi üretim akışı bu fazın konusu değildir.
- e-Fatura / e-Arşiv, resmi fatura numarası üretimi ve otomatik tahsilat kapama yapılmamıştır.

