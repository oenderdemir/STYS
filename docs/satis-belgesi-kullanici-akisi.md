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

## Faz 70C — Form UI Regresyon Düzeltmesi ve Paket Türü/Birim Seçimi

Bu fazda satış belgesi formunun görsel yerleşimi toparlandı ve satırdaki `Birim` alanı manuel text yerine Paket Türleri lookup'u ile seçilebilir hale getirildi.

### UI düzeni
- Yeni Satış Belgesi dialog'u daha geniş ve dengeli bir yerleşime alındı.
- `Belge & Cari Bilgileri` tabı iki kolonlu kart yapısında gösterilir.
- `Satırlar` tabı daha kompakt ve taşmasız bir grid ile düzenlenir.
- KDV Tipi ve KDV Oranı alanları görünür ve seçilebilir halde tutulur.
- Genel toplam tek bir özet çubuğunda gösterilir; footer'daki tekrar kaldırıldı.

### Birim alanı
- Satırdaki `Birim` alanı artık manuel input değildir.
- Birim seçenekleri `PaketTurleriService` ile gelen aktif paket türlerinden üretilir.
- Paket Türleri global tanımdır; bu lookup için `tesisId` gönderilmez.
- Paket türü listesinde `Adet` varsa varsayılan seçim `Adet` olur.
- Liste boşsa fallback olarak `Adet` kullanılır.
- Satır düzenleme ekranında eski birim değeri listede yoksa korunur ve seçenek olarak gösterilir.

### Kapsam
- Satış belgesi entity/model alanları bu fazda değiştirilmedi.
- Muhasebe fişi üretim mantığına dokunulmadı.
- Tevkifat için kalıcı backend muhasebe kaydı eklenmedi.
- Cari, depo ve taşınır/stok seçimleri seçili çalışma tesisi bağlamında çalışmaya devam eder.

## Faz 72 — Satış / Alış Belgesi Ayrımı

Bu fazda satış ve alış belgeleri aynı altyapı üzerinde iki ayrı kullanıcı akışı olarak sunuldu.

### Ekran ayrımı
- `Satış Belgeleri` ekranı satış belge tipleriyle sınırlandı.
- `Alış Belgeleri` ekranı aynı component/service altyapısını kullanarak alış belge tipleriyle açıldı.
- Alış ekranında varsayılan belge tipi `AlisFaturasi` olarak ayarlandı.

### Cari seçimi
- Satış ekranında müşteri tipi cariler gösterilir.
- Alış ekranında cari kart seçimi tedarikçi tipleriyle sınırlandırılır.
- Alış ekranında manuel giriş açılsa bile akış tedarikçi snapshot mantığıyla çalışır.

### Muhasebe fişi
- Alış belgelerinde `Muhasebe Fişi Oluştur` aksiyonu gösterilmez.
- Satış iade ve legacy iade tiplerinde de muhasebe fişi üretimi desteklenmez.
- Bu fazda muhasebe fişi üretim algoritması genişletilmedi.

### Kapsam
- Paket türleri global lookup olarak kalır.
- Tesis context davranışı korunur.
- Bu fazda stok hareketi üretimi eklenmedi.

### Menü / yetki notu
- `Alış Belgeleri` menü kaydı mevcut `MuhasebeFisYonetimi.View` yetkisini kullanır.
- Bu eşleştirme operasyonel olarak yeterli bir ara çözümdür; daha semantik bir menü yetkisi ileride ayrı fazda ele alınacaktır.
