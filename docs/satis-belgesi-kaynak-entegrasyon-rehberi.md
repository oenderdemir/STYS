# Satış Belgesi Kaynak Entegrasyon Rehberi

> **Faz 60** — Ortak satış belgesi taslak oluşturma servisi.
> Operasyon modülleri (otel, restoran, kamp vb.) tarafından kullanılmak üzere hazırlanmıştır.

---

## 1. Amaç

Bu servis, operasyon modüllerinin fatura/satış belgesi taslağı oluşturması için **tek bir ortak giriş noktası** sağlar.

Operasyon modülleri:
- Doğrudan `SatisBelgesi` entity'si oluşturmaz.
- Doğrudan `ISatisBelgesiService.CreateAsync` çağırmaz.
- Bunun yerine `ISatisBelgesiTaslakOlusturmaService.KaynaktanTaslakOlusturAsync` çağırır.

Bu sayede:
- Validasyon merkezi olarak yapılır.
- Duplicate kaynak kontrolü yapılır.
- Access scope kontrolü yapılır.
- Satır KDV hesaplamaları `SatisBelgesiService` tarafından yapılır.

---

## 2. Kaynak Modül Prensibi

Her satış belgesi bir **kaynağa** bağlıdır:

| Alan | Açıklama |
|------|----------|
| `KaynakModul` | Belgeyi oluşturan modül (Otel, Restoran, Kamp, Manuel, EkHizmet, Diger) |
| `KaynakTipi` | Kaynağın alt tipi (Checkout, Adisyon, KampRezervasyon, Manuel) |
| `KaynakId` | Kaynağın benzersiz kimliği (KonaklamaId, AdisyonId, RezervasyonId) |

Bu üç alan birlikte **tekil (unique)** olmalıdır. Aynı kaynaktan ikinci kez taslak oluşturulamaz.

---

## 3. API Endpoint

```
POST ui/muhasebe/satis-belgeleri/kaynaktan-taslak-olustur
```

**Permission:** `MuhasebeFisYonetimi.Manage`

**Request Body:** `SatisBelgesiTaslakOlusturRequest`

**Response:** `SatisBelgesiDto`

---

## 4. Örnek Payload'lar

### 4.1 Otel Checkout

```json
{
  "kaynakModul": 2,
  "kaynakTipi": "Checkout",
  "kaynakId": "12345",
  "tesisId": 1,
  "belgeTarihi": "2026-05-24T12:00:00",
  "kurumsalMi": false,
  "musteriAdSoyad": "Ahmet Yılmaz",
  "musteriTcKimlikNo": "12345678901",
  "musteriTelefon": "5551234567",
  "aciklama": "Otel konaklama — Oda 301, 20-24 Mayıs 2026",
  "satirlar": [
    {
      "satirTipi": 1,
      "aciklama": "Konaklama (4 gece)",
      "miktar": 4,
      "birimFiyat": 2500.00,
      "kdvUygulamaTipi": 1,
      "kdvOrani": 10.0,
      "kaynakSatirId": "otel-gece-001"
    },
    {
      "satirTipi": 4,
      "aciklama": "Oda servisi kahvaltı",
      "miktar": 2,
      "birimFiyat": 350.00,
      "kdvUygulamaTipi": 1,
      "kdvOrani": 10.0,
      "kaynakSatirId": "otel-ekhizmet-001"
    }
  ]
}
```

### 4.2 Restoran Adisyon

```json
{
  "kaynakModul": 3,
  "kaynakTipi": "Adisyon",
  "kaynakId": "AD-2026-0042",
  "tesisId": 1,
  "belgeTarihi": "2026-05-24T20:30:00",
  "kurumsalMi": false,
  "musteriAdSoyad": "Mehmet Demir",
  "aciklama": "Restoran adisyon — Masa 7",
  "satirlar": [
    {
      "satirTipi": 2,
      "aciklama": "Izgara bonfile",
      "miktar": 1,
      "birimFiyat": 850.00,
      "kdvUygulamaTipi": 1,
      "kdvOrani": 10.0,
      "kaynakSatirId": "adisyon-satir-001"
    },
    {
      "satirTipi": 2,
      "aciklama": "Ayran",
      "miktar": 2,
      "birimFiyat": 40.00,
      "kdvUygulamaTipi": 1,
      "kdvOrani": 10.0,
      "kaynakSatirId": "adisyon-satir-002"
    }
  ]
}
```

### 4.3 Kamp Rezervasyon

```json
{
  "kaynakModul": 4,
  "kaynakTipi": "KampRezervasyon",
  "kaynakId": "REZ-2026-0089",
  "tesisId": 2,
  "belgeTarihi": "2026-05-24T10:00:00",
  "kurumsalMi": false,
  "musteriAdSoyad": "Ayşe Kaya",
  "musteriTcKimlikNo": "98765432109",
  "musteriTelefon": "5329876543",
  "aciklama": "Kamp rezervasyon — 1-7 Temmuz 2026",
  "satirlar": [
    {
      "satirTipi": 3,
      "aciklama": "Kamp konaklama (7 gece)",
      "miktar": 7,
      "birimFiyat": 750.00,
      "kdvUygulamaTipi": 1,
      "kdvOrani": 10.0,
      "kaynakSatirId": "kamp-gece-001"
    }
  ]
}
```

### 4.4 Kurumsal Müşteri (Manuel)

```json
{
  "kaynakModul": 1,
  "kaynakTipi": "Manuel",
  "kaynakId": "MAN-2026-0005",
  "tesisId": 1,
  "belgeTarihi": "2026-05-24T15:00:00",
  "vadeTarihi": "2026-06-24",
  "kurumsalMi": true,
  "musteriUnvan": "ABC Turizm Ltd. Şti.",
  "musteriVergiNo": "1234567890",
  "musteriVergiDairesi": "Beyoğlu",
  "musteriAdres": "İstiklal Cad. No:45 Beyoğlu/İstanbul",
  "musteriEposta": "muhasebe@abcturizm.com",
  "musteriTelefon": "2125554433",
  "aciklama": "Kurumsal grup rezervasyonu",
  "satirlar": [
    {
      "satirTipi": 1,
      "aciklama": "Grup konaklama (20 oda x 3 gece)",
      "miktar": 60,
      "birimFiyat": 1800.00,
      "kdvUygulamaTipi": 1,
      "kdvOrani": 10.0,
      "kaynakSatirId": "kurumsal-001"
    }
  ]
}
```

### 4.5 KDV İstisnalı Satır (KDV Kapsam Dışı)

```json
{
  "satirTipi": 99,
  "aciklama": "Kültür Bakanlığı muafiyetli konaklama",
  "miktar": 3,
  "birimFiyat": 2000.00,
  "kdvUygulamaTipi": 4,
  "kdvOrani": 0,
  "kdvIstisnaTanimId": 5,
  "kaynakSatirId": "istisna-001"
}
```

---

## 5. Validasyon Kuralları

### 5.1 Erken Validasyon (Taslak Oluşturma Servisi)

| Kural | Hata Mesajı |
|-------|-------------|
| `KaynakModul` geçerli olmalı | "Kaynak modül geçerli değil." |
| `KaynakTipi` boş olamaz | "Kaynak tipi zorunludur." |
| `KaynakId` boş olamaz | "Kaynak kimliği zorunludur." |
| `BelgeTarihi` default olamaz | "Belge tarihi zorunludur." |
| En az 1 satır | "En az bir satır eklenmelidir." |
| Kurumsal → `MusteriUnvan` zorunlu | "Kurumsal müşteri için ünvan zorunludur." |
| Kurumsal → `MusteriVergiNo` zorunlu | "Kurumsal müşteri için vergi numarası zorunludur." |
| Bireysel → `MusteriAdSoyad` zorunlu | "Bireysel müşteri için ad soyad zorunludur." |
| Satır `Aciklama` zorunlu | "Satır açıklaması zorunludur." |
| `Miktar > 0` | "Satır miktarı sıfırdan büyük olmalıdır." |
| `BirimFiyat >= 0` | "Birim fiyat negatif olamaz." |
| Tevkifatlı → hata | "Tevkifatlı satış satırları bu aşamada desteklenmemektedir." |
| KDV'li → `KdvOrani > 0` | "KDV'li satırda KDV oranı sıfırdan büyük olmalıdır." |
| KDV'siz → `KdvIstisnaTanimId` zorunlu | "KDV'li olmayan satırda KDV istisna tanımı zorunludur." |

### 5.2 Nihai Validasyon (SatisBelgesiService)

`SatisBelgesiService.CreateAsync` içinde aynı validasyonlar **tekrarlanır**, artı:
- KDV istisna tanımının varlığı, aktifliği, uygulama tipi uyumu, geçerlilik tarihi
- Belge no duplicate kontrolü
- Kaynak duplicate kontrolü

---

## 6. Duplicate Kaynak Kontrolü

Aynı `KaynakModul` + `KaynakTipi` + `KaynakId` kombinasyonu için:

1. **Erken kontrol:** `ISatisBelgesiRepository.AnyAsync` ile yapılır. Varsa hata döner:
   > "Bu kaynak için daha önce satış belgesi taslağı oluşturulmuş."

2. **Nihai kontrol:** `SatisBelgesiService.CreateAsync` içinde `ThrowIfKaynakDuplicateAsync` ile tekrarlanır.

Bu double-check, operasyon modülüne erken bilgi verirken veri bütünlüğünü de garanti eder.

---

## 7. Access Scope

- `TesisId` varsa ve kullanıcı **scoped** ise, `TesisId` kullanıcının scope'undaki tesislerden biri olmalıdır. Değilse **403** döner.
- `TesisId` yoksa ve scope **tek tesis** içeriyorsa, `TesisId` otomatik set edilir.
- `TesisId` yoksa ve scope **birden çok tesis** içeriyorsa, hata döner: "Tesis seçimi zorunludur."
- Scope yoksa (unscoped), `TesisId` null kalabilir.

---

## 8. KDV / İstisna Bilgisi Nasıl Gönderilmeli

| `KdvUygulamaTipi` | `KdvOrani` | `KdvIstisnaTanimId` | Açıklama |
|-------------------|------------|---------------------|----------|
| `Kdvli` (1) | > 0 (örn: 10, 20) | null | Standart KDV'li satış |
| `TamIstisna` (2) | 0 (göz ardı edilir) | zorunlu | Tam KDV istisnası |
| `KismiIstisna` (3) | 0 (göz ardı edilir) | zorunlu | Kısmi KDV istisnası |
| `KdvKapsamDisi` (4) | 0 (göz ardı edilir) | zorunlu | KDV kapsamı dışı |
| `Tevkifatli` (5) | — | — | **Desteklenmez, hata döner** |

KDV istisna tanımları [`backend/Muhasebe/Kdv/`](backend/Muhasebe/Kdv/) altında yönetilir. `SatisBelgesiService`, gönderilen `KdvIstisnaTanimId`'nin geçerli, aktif, doğru uygulama tipinde ve satış işlemlerinde kullanılabilir olduğunu doğrular.

---

## 9. Matrah / KDV / Satır Toplamı

Bu alanlar **gönderilmez**, `SatisBelgesiService.CreateAsync` tarafından hesaplanır:

- **Matrah:** `Miktar × BirimFiyat`
- **KDV Tutarı:** `Matrah × KdvOrani ÷ 100` (istisna/kapsam dışı için 0)
- **Satır Toplamı:** `Matrah + KdvTutari`
- **Belge Toplamları:** Tüm satırların toplamı

---

## 10. Bu Servis Fatura KESMEZ — Sadece Taslak Oluşturur

`KaynaktanTaslakOlusturAsync` sonucu:

- `BelgeTipi = FaturaTaslagi`
- `Durum = Taslak`
- **e-Fatura/e-Arşiv entegrasyonu** bu aşamada yoktur.
- **Muhasebe fişi üretilmez.**
- Belge, muhasebe onayına gönderilip onaylandıktan sonra ileride fatura kesim adımına geçilebilir.

---

## 11. Servis Mimarisi

```
Operasyon Modülü (Otel/Restoran/Kamp)
        │
        ▼
ISatisBelgesiTaslakOlusturmaService
  ├── Erken validasyon
  ├── Access scope kontrolü
  ├── Duplicate kaynak kontrolü
  ├── Request dönüşümü
  │
        ▼
ISatisBelgesiService.CreateAsync
  ├── Nihai validasyon
  ├── KDV istisna doğrulama
  ├── Belge no üretimi
  ├── Matrah/KDV hesaplama
  └── Kayıt
```

---

## 12. Dosya Listesi

| Dosya | Açıklama |
|-------|----------|
| [`backend/Muhasebe/SatisBelgeleri/Dtos/SatisBelgesiTaslakOlusturmaDtos.cs`](../backend/Muhasebe/SatisBelgeleri/Dtos/SatisBelgesiTaslakOlusturmaDtos.cs) | Request DTO'ları |
| [`backend/Muhasebe/SatisBelgeleri/Services/ISatisBelgesiTaslakOlusturmaService.cs`](../backend/Muhasebe/SatisBelgeleri/Services/ISatisBelgesiTaslakOlusturmaService.cs) | Interface |
| [`backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiTaslakOlusturmaService.cs`](../backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiTaslakOlusturmaService.cs) | Implementation |
| [`backend/Muhasebe/SatisBelgeleri/Controllers/SatisBelgeleriController.cs`](../backend/Muhasebe/SatisBelgeleri/Controllers/SatisBelgeleriController.cs) | Controller endpoint |
| [`backend/Program.cs`](../backend/Program.cs) | DI registration |

---

## 13. Otel / Rezervasyon Checkout Entegrasyonu (Faz 61 / 61A)

### 13.1 Genel Bakış

`IRezervasyonSatisBelgesiService`, check-out işlemi tamamlanmış rezervasyonlardan satış belgesi taslağı oluşturmak için [`ISatisBelgesiTaslakOlusturmaService`](#) ile entegrasyonu sağlar.

**Prensip:** Otel modülü (Rezervasyonlar) doğrudan `SatisBelgesi` entity'si oluşturmaz veya `ISatisBelgesiService.CreateAsync` çağırmaz. Bunun yerine, rezervasyon verisini `ISatisBelgesiTaslakOlusturmaService.KaynaktanTaslakOlusturAsync` metoduna iletir.

> **Önemli:** Bu endpoint **fatura KESMEZ** — yalnızca satış belgesi taslağı oluşturur. e-Fatura/e-Arşiv entegrasyonu ve muhasebe fişi üretimi bu aşamada yoktur.

### 13.2 API Endpoint

```
POST ui/rezervasyon/kayitlar/{rezervasyonId:int}/satis-belgesi-taslagi-olustur
```

**Permission:** `RezervasyonYonetimi.Manage`

**Response:** `SatisBelgesiDto`

### 13.3 Request Body (Tam)

```json
{
  "rezervasyonId": 12345,
  "kurumsalMi": false,
  "musteriUnvan": null,
  "musteriAdSoyad": "Ahmet Yılmaz",
  "musteriVergiNo": null,
  "musteriTcKimlikNo": "12345678901",
  "musteriVergiDairesi": null,
  "musteriAdres": null,
  "musteriEposta": "ahmet@example.com",
  "musteriTelefon": "5551234567",
  "belgeTarihi": null,
  "vadeTarihi": null,
  "aciklama": null,
  "kdvOrani": null,
  "kdvIstisnaTanimId": null
}
```

### 13.4 İş Akışı (Güncel — Faz 61A)

1. **Route/Body ID Eşleşmesi:** Route'daki `rezervasyonId` ile body'deki `RezervasyonId` eşleşmeli, aksi halde 400 döner.
2. **Rezervasyon Bulma ve Access Scope:** Rezervasyon ID ile bulunur. Kullanıcı scoped ise rezervasyonun `TesisId` değeri kullanıcının scope'undaki tesislerden biri olmalıdır, değilse 403 döner.
3. **Durum Validasyonu:** Yalnızca `CheckOutTamamlandi` durumundaki rezervasyonlar için taslak oluşturulabilir. `Iptal` durumundakiler reddedilir (400).
4. **Gece Sayısı Hesaplama:** `(CikisTarihi.Date - GirisTarihi.Date).Days`
5. **Toplam Ücret Validasyonu:** `rezervasyon.ToplamUcret <= 0` ise 400 hatası döner.
6. **Satış Satırları:** Her gece için bir `Konaklama` tipi satır oluşturulur. KDV parametreleri request'ten okunur (istisna veya override). Yuvarlama farkları son satıra eklenir.
7. **Müşteri Bilgileri:** Request önceliklidir. Boş bireysel alanlar rezervasyondan tamamlanır. Kurumsal fatura için `MusteriUnvan` ve `MusteriVergiNo` zorunludur.
8. **Belge Tarihi / Açıklama:** Request'teki değerler önceliklidir; boşsa rezervasyondan (`CikisTarihi` / otomatik açıklama) alınır.
9. **E‑posta / Telefon:** Request'te varsa request'ten, yoksa rezervasyondan alınır.
10. **Taslak Oluşturma:** `KaynakModul = Otel`, `KaynakTipi = "RezervasyonCheckout"`, `KaynakId = rezervasyonId.ToString()` ile [`ISatisBelgesiTaslakOlusturmaService.KaynaktanTaslakOlusturAsync`](#) çağrılır.

### 13.5 Access Scope

Rezervasyon için access scope kontrolü [`RezervasyonService.GetScopedReservationForManageAsync`](../backend/Rezervasyonlar/Services/RezervasyonService.cs) ile aynı pattern'i kullanır:

- `IUserAccessScopeService.GetCurrentScopeAsync()` ile mevcut scope alınır
- `scope.IsScoped && !scope.TesisIds.Contains(rezervasyon.TesisId)` → **403**

### 13.6 Satır Yapısı

| Alan | Varsayılan | KDV Override | İstisna |
|------|-----------|--------------|---------|
| `SatirTipi` | `Konaklama` (1) | `Konaklama` (1) | `Konaklama` (1) |
| `Aciklama` | `"Konaklama — {geceTarihi:dd.MM.yyyy}"` | aynı | aynı |
| `Miktar` | 1 | 1 | 1 |
| `BirimFiyat` | `ToplamUcret / geceSayisi` | aynı | aynı |
| `KdvUygulamaTipi` | `Kdvli` (1) | `Kdvli` (1) | İstisna tanımından okunur |
| `KdvOrani` | %10 | `request.KdvOrani` | 0 |
| `KdvIstisnaTanimId` | `null` | `null` | `request.KdvIstisnaTanimId` |
| `KaynakSatirId` | `"{rezervasyonId}_{geceTarihi:yyyyMMdd}"` | aynı | aynı |

### 13.7 Müşteri Bilgileri Çözümleme

#### Bireysel Fatura (`kurumsalMi = false`)

- `MusteriAdSoyad` zorunludur. Request'te boşsa `rezervasyon.MisafirAdiSoyadi` kullanılır.
- `MusteriTcKimlikNo` request'ten gelir; boşsa `rezervasyon.TcKimlikNo` kullanılır.
- `MusteriUnvan` ve `MusteriVergiNo` her zaman `null` olur.

#### Kurumsal Fatura (`kurumsalMi = true`)

- `MusteriUnvan` **zorunludur** (boşsa 400).
- `MusteriVergiNo` **zorunludur** (boşsa 400).
- `MusteriVergiDairesi` ve `MusteriAdres` request'ten aktarılır (isteğe bağlı).
- `MusteriAdSoyad` boş bırakılır.

### 13.8 Örnek Request'ler

#### Bireysel Fatura (tüm bilgiler request'ten)

```json
{
  "rezervasyonId": 12345,
  "kurumsalMi": false,
  "musteriAdSoyad": "Ahmet Yılmaz",
  "musteriTcKimlikNo": "12345678901",
  "musteriEposta": "ahmet@example.com",
  "musteriTelefon": "5551234567"
}
```

#### Bireysel Fatura (ad soyad rezervasyondan)

```json
{
  "rezervasyonId": 12345,
  "kurumsalMi": false
}
```

#### Kurumsal Fatura

```json
{
  "rezervasyonId": 12345,
  "kurumsalMi": true,
  "musteriUnvan": "ABC Turizm Ltd. Şti.",
  "musteriVergiNo": "1234567890",
  "musteriVergiDairesi": "Beyoğlu",
  "musteriAdres": "İstiklal Cad. No:45 Beyoğlu/İstanbul",
  "musteriEposta": "muhasebe@abcturizm.com",
  "musteriTelefon": "2125554433"
}
```

#### KDV Oranı Override (%20)

```json
{
  "rezervasyonId": 12345,
  "kurumsalMi": false,
  "kdvOrani": 20.0
}
```

#### KDV İstisna (Kapsam Dışı)

```json
{
  "rezervasyonId": 12345,
  "kurumsalMi": false,
  "kdvIstisnaTanimId": 5
}
```

### 13.9 Validasyon Özeti

| Kural | Hata Kodu | Mesaj |
|-------|-----------|-------|
| Route/Body ID eşleşmiyor | 400 | "Rezervasyon ID uyuşmazlığı: route ve body farklı." |
| Rezervasyon bulunamadı | 404 | "Rezervasyon bulunamadı." |
| Scoped kullanıcı yetkisiz tesis | 403 | "Bu rezervasyon için yetkiniz bulunmuyor." |
| İptal edilmiş rezervasyon | 400 | "İptal edilen rezervasyon için satış belgesi taslağı oluşturulamaz." |
| Check-out tamamlanmamış | 400 | "Satış belgesi taslağı yalnızca check-out tamamlanmış rezervasyonlar için oluşturulabilir." |
| Gece sayısı ≤ 0 | 400 | "Rezervasyon gece sayısı hesaplanamadı." |
| ToplamUcret ≤ 0 | 400 | "Rezervasyon toplam tutarı bulunamadığı için satış belgesi taslağı oluşturulamaz." |
| Kurumsal + ünvan boş | 400 | "Kurumsal fatura için müşteri ünvanı zorunludur." |
| Kurumsal + vergi no boş | 400 | "Kurumsal fatura için vergi numarası zorunludur." |
| Bireysel + ad soyad boş (rezervasyonda da yoksa) | 400 | "Bireysel fatura için müşteri ad soyad zorunludur." |
| KDV istisna tanımı bulunamadı | 400 | "KDV istisna tanımı bulunamadı (Id: {id})." |
| Duplicate kaynak | 409 | "Bu kaynak için daha önce satış belgesi taslağı oluşturulmuş." |

### 13.10 Dosya Listesi

| Dosya | Açıklama |
|-------|----------|
| [`backend/Rezervasyonlar/Dtos/RezervasyonSatisBelgesiDtos.cs`](../backend/Rezervasyonlar/Dtos/RezervasyonSatisBelgesiDtos.cs) | Request DTO (genişletilmiş — Faz 61A) |
| [`backend/Rezervasyonlar/Services/IRezervasyonSatisBelgesiService.cs`](../backend/Rezervasyonlar/Services/IRezervasyonSatisBelgesiService.cs) | Interface |
| [`backend/Rezervasyonlar/Services/RezervasyonSatisBelgesiService.cs`](../backend/Rezervasyonlar/Services/RezervasyonSatisBelgesiService.cs) | Implementation (güncellenmiş — Faz 61A) |
| [`backend/Rezervasyonlar/Controllers/RezervasyonController.cs`](../backend/Rezervasyonlar/Controllers/RezervasyonController.cs) | Controller endpoint |
| [`backend/Program.cs`](../backend/Program.cs) | DI registration |
