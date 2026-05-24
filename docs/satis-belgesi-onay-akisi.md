# Satış Belgesi Onay Akışı Dokümantasyonu

> **Faz 64** | Son güncelleme: 2026-05-24  
> **Durum:** ✅ Tamamlandı  
> **Kapsam:** Satış belgesi taslaklarının muhasebe tarafından onaylanması, reddedilmesi, iptal edilmesi ve ileride muhasebe fişi üretimine hazır hale getirilmesi için eksik kontrol ve validasyonların tamamlanması.

---

## 1. Durum Geçiş Diyagramı

```
┌──────────┐     Onaya Gönder     ┌───────────────────┐      Onayla       ┌───────────────────┐
│  Taslak  │ ────────────────────→ │ MuhasebeOnayinda  │ ────────────────→ │ MuhasebeOnaylandi  │
│   (1)    │                       │       (2)         │                   │       (3)          │
└──────────┘                       └───────────────────┘                   └───────────────────┘
     │                                    │    │                                    │
     │                                    │    │                                    │ (Faz 65)
     │                                    │    │ Reddet                             ▼
     │                                    │    └──────────────┐            ┌───────────────────┐
     │                                    ▼                   ▼            │   FaturaKesildi   │
     │                            ┌──────────────┐    ┌──────────────┐     │       (5)          │
     │                            │  IptalEdildi │    │  Reddedildi  │     └───────────────────┘
     │                            │     (7)      │    │     (4)      │              │
     │                            └──────────────┘    └──────────────┘              │
     │                                  ▲                    │                      ▼
     │                                  │                    │ Güncelle       ┌───────────────────┐
     │                                  │                    └──────────────→ │ MusteriyeGonderildi│
     │                                  │                            Taslak   │       (6)          │
     │                                  │                            (1)      └───────────────────┘
     │                                  │
     │  Sil / Güncelle                  │ İptal (herhangi bir
     └──────────────────────────────────┘ durumdan, FaturaKesildi
                                        ve MusteriyeGonderildi hariç)
```

### Detaylı Geçiş Kuralları

| Başlangıç Durumu | Hedef Durum | Tetikleyici | Açıklama |
|---|---|---|---|
| Taslak (1) | MuhasebeOnayinda (2) | `MuhasebeOnayinaGonderAsync` | 12 validasyon kuralından geçer |
| MuhasebeOnayinda (2) | MuhasebeOnaylandi (3) | `MuhasebeOnaylaAsync` | İçerik re-validasyonu yapılır |
| MuhasebeOnayinda (2) | Reddedildi (4) | `ReddetAsync` | RedNedeni zorunlu |
| MuhasebeOnayinda (2) | IptalEdildi (7) | `IptalEtAsync` | |
| MuhasebeOnaylandi (3) | IptalEdildi (7) | `IptalEtAsync` | Fiş üretilmeden önce iptal edilebilir |
| Reddedildi (4) | Taslak (1) | `UpdateAsync` | Güncelleme sırasında otomatik; RedNedeni temizlenir |
| Reddedildi (4) | IptalEdildi (7) | `IptalEtAsync` | |
| Taslak (1) | IptalEdildi (7) | `IptalEtAsync` | |
| MuhasebeOnaylandi (3) | FaturaKesildi (5) | ❌ Bu fazda DEĞİL | Faz 65 konusu |
| FaturaKesildi (5) | MusteriyeGonderildi (6) | ❌ Bu fazda DEĞİL | Faz 65 konusu |

### İzin Verilmeyen Geçişler

- **FaturaKesildi (5) → İptal:** Fatura kesilmiş belge iptal edilemez
- **MusteriyeGonderildi (6) → İptal:** Müşteriye gönderilmiş belge iptal edilemez
- **IptalEdildi (7) → Herhangi bir şey:** İptal edilmiş belge tekrar aktif edilemez
- **MuhasebeOnaylandi (3) → Red:** Onaylanmış belge reddedilemez (iptal edilebilir)
- **Taslak (1) → MuhasebeOnaylandi (3):** Arada onaya gönderme adımı zorunludur

---

## 2. Validasyon Kuralları (12 Kural)

### `ValidateBelgeOnayaGonderilebilir` Metodu

Belge muhasebe onayına gönderilmeden önce aşağıdaki 12 kontrol yapılır:

| # | Kural | Hata Mesajı |
|---|---|---|
| 1 | En az 1 aktif (silinmemiş) satır olmalı | "Satır içermeyen belge muhasebe onayına gönderilemez." |
| 2 | `ToplamMatrah > 0` | "Belge toplam matrahı sıfırdan büyük olmalıdır." |
| 3 | `GenelToplam > 0` | "Belge genel toplamı sıfırdan büyük olmalıdır." |
| 4 | Kurumsal müşteri → MusteriUnvan + MusteriVergiNo dolu | "Kurumsal müşteri için ünvan zorunludur." / "Kurumsal müşteri için vergi numarası zorunludur." |
| 5 | Bireysel müşteri → MusteriAdSoyad dolu | "Bireysel müşteri için ad soyad zorunludur." |
| 6 | Tevkifatlı satır olmamalı (`KdvUygulamaTipi.Tevkifatli`) | "Tevkifatlı satış satırları bu aşamada desteklenmemektedir. (SıraNo: X)" |
| 7 | Her satırda geçerli KDV uygulama tipi (Kdvli, TamIstisna, KismiIstisna, KdvKapsamDisi) | "Geçersiz KDV uygulama tipi: X. (SıraNo: Y)" |
| 8 | KDV'li satırda `KdvOrani > 0` | "KDV'li satırda KDV oranı sıfırdan büyük olmalıdır. (SıraNo: X)" |
| 9 | KDV'siz satırda `KdvIstisnaTanimId` zorunlu | "KDV'li olmayan satırda KDV istisna tanımı zorunludur. (SıraNo: X)" |
| 10 | Satır toplamları = Belge toplamları (Matrah, KDV, GenelToplam) | "Belge toplam matrahı (X) satır toplamlarıyla (Y) uyuşmuyor. Belgeyi güncelleyip tekrar deneyin." |
| 11 | Kaynak duplicate kontrolü (KaynakId varsa) | "Bu kaynaktan zaten bir satış belgesi oluşturulmuş. (Modül: X, Tip: Y, KaynakId: Z)" |
| 12 | KDV istisna tanımı geçerlilik: tanım mevcut, aktif, uygulama tipi eşleşiyor, satışta kullanılabilir, tarih aralığında | Çeşitli (bkz. `ValidateKdvIstisnaTanimAsync`) |

### `ValidateBelgeMuhasebeOnaylanabilir` Metodu

`ValidateBelgeOnayaGonderilebilir` ile aynı 12 kuralı çalıştırır. Bu sayede onaya gönderme ile onaylama arasında belge içeriğinin değişmediğinden emin olunur. `MuhasebeOnaylaAsync` artık `Satirlar`'ı da include eder.

---

## 3. Entity ve Model Değişiklikleri

**Bu fazda entity/model değişikliği YOKTUR.** Yeni migration oluşturulmamıştır.

### Fiş Üretim Ön Hazırlık Değerlendirmesi

| Değerlendirme | Sonuç |
|---|---|
| `MuhasebeFisId` alanı | Entity'de mevcut DEĞİL. Faz 65'te `SatisBelgesi` entity'sine `MuhasebeFisId?` navigasyon property'si eklenecek |
| `MuhasebeOnaylandi` durumu | Bu fazda tamamlandı. Faz 65'te bu durumdan fiş üretimi yapılacak |
| Fiş entity'si | `MuhasebeFis` entity'si mevcut, Faz 65'te `SatisBelgesi` ile ilişkilendirilecek |
| KDV hesaplama algoritması | Mevcut haliyle yeterli. Faz 65'te fişe aktarılırken kullanılacak |

---

## 4. Yetkilendirme

### Controller Yetkileri

Tüm endpoint'ler mevcut `MuhasebeFisYonetimi` permission domain'i altında:

| HTTP | Endpoint | Permission |
|---|---|---|
| `GET` | `/ui/muhasebe/satis-belgeleri/{id}` | `MuhasebeFisYonetimi.View` |
| `POST` | `/ui/muhasebe/satis-belgeleri/filter` | `MuhasebeFisYonetimi.View` |
| `POST` | `/ui/muhasebe/satis-belgeleri` | `MuhasebeFisYonetimi.Manage` |
| `POST` | `/ui/muhasebe/satis-belgeleri/kaynaktan-taslak-olustur` | `MuhasebeFisYonetimi.Manage` |
| `PUT` | `/ui/muhasebe/satis-belgeleri/{id}` | `MuhasebeFisYonetimi.Manage` |
| `DELETE` | `/ui/muhasebe/satis-belgeleri/{id}` | `MuhasebeFisYonetimi.Manage` |
| `POST` | `/ui/muhasebe/satis-belgeleri/{id}/muhasebe-onayina-gonder` | `MuhasebeFisYonetimi.Manage` |
| `POST` | `/ui/muhasebe/satis-belgeleri/{id}/muhasebe-onayla` | `MuhasebeFisYonetimi.Manage` |
| `POST` | `/ui/muhasebe/satis-belgeleri/{id}/reddet` | `MuhasebeFisYonetimi.Manage` |
| `POST` | `/ui/muhasebe/satis-belgeleri/{id}/iptal` | `MuhasebeFisYonetimi.Manage` |

### MuhasebeAdmin Rolü

`muhasebe-admin` kullanıcısı `MuhasebeAdmin` rolüne sahiptir. Bu rol `MuhasebeFisYonetimi.Admin` permission'ına sahip olduğu için `AccessScopeProvider.IsCurrentUserAdmin()` → `true` döner ve tüm tesis/restoran/kamp/depo kayıtlarını görebilir (sınırsız scope).

---

## 5. Base Service / Repository Kullanımı

`SatisBelgesiService`, [`BaseRdbmsService<SatisBelgesiDto, SatisBelgesi, int>`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs:14) sınıfından türemektedir.

### Kullanılan Base Metotlar

| Metot | Kullanım | Uyum |
|---|---|---|
| `Repository.FirstOrDefaultAsync()` | Belge getirme (include ile) | ✅ |
| `Repository.GetByIdAsync()` | ID ile getirme | ✅ |
| `Repository.AddAsync()` | Yeni belge ekleme | ✅ |
| `Repository.SaveChangesAsync()` | Değişiklikleri kaydetme | ✅ |
| `Mapper.Map<T>()` | Entity ↔ Dto dönüşümü | ✅ |

### Manuel Kullanımlar (İncelendi, Şimdilik Uygun)

| Konum | Kullanım | Değerlendirme |
|---|---|---|
| Line 250: `_satisBelgesiRepository.Update(belge)` | UpdateAsync içinde | `Repository.UpdateAsync()` kullanılabilirdi ama `Update` senkron çağrı da kabul edilebilir |
| Line 283: `_satisBelgesiRepository.Update(belge)` | DeleteAsync içinde | Aynı durum |
| `_db` (DbContext) direkt kullanımı | `FilterAsync`, `ThrowIfBelgeNoDuplicateAsync`, `ThrowIfKaynakDuplicateAsync`, `GenerateBelgeNoAsync`, `ValidateKdvIstisnaTanimAsync` | Sorgu esnekliği için gerekli, base service pattern'iyle uyumlu |

**Sonuç:** Base service kullanımı genel olarak uygun. `_satisBelgesiRepository.Update()` çağrıları Faz 65'te `Repository.UpdateAsync()` ile değiştirilebilir.

---

## 6. Manuel Test Kontrol Listesi

### 6.1 Onaya Gönderme Testleri

- [ ] **T1:** Kurumsal müşterili, KDV'li satırlı bir belgeyi Taslak → MuhasebeOnayinda yap
- [ ] **T2:** Bireysel müşterili, KDV'siz (istisnalı) satırlı bir belgeyi onaya gönder
- [ ] **T3:** Müşteri bilgisi eksik belge onaya gönderilmeye çalışıldığında hata al
- [ ] **T4:** ToplamMatrah=0 olan belge onaya gönderilmeye çalışıldığında hata al
- [ ] **T5:** Tevkifatlı satır içeren belge onaya gönderilmeye çalışıldığında hata al
- [ ] **T6:** Satır toplamları ile belge toplamları tutarsız belge onaya gönderilmeye çalışıldığında hata al
- [ ] **T7:** Aynı kaynaktan (KaynakModul+KaynakTipi+KaynakId) ikinci kez onaya göndermede hata al
- [ ] **T8:** Pasif/geçersiz KDV istisna tanımı olan belge onaya gönderilmeye çalışıldığında hata al

### 6.2 Onaylama Testleri

- [ ] **T9:** MuhasebeOnayinda → MuhasebeOnaylandi başarılı geçiş
- [ ] **T10:** Taslak durumundaki belgeyi doğrudan onaylamaya çalış → hata al
- [ ] **T11:** Reddedildi durumundaki belgeyi onaylamaya çalış → hata al

### 6.3 Ret Testleri

- [ ] **T12:** MuhasebeOnayinda → Reddedildi (RedNedeni ile)
- [ ] **T13:** RedNedeni boş gönder → hata al
- [ ] **T14:** Taslak durumundaki belgeyi reddetmeye çalış → hata al

### 6.4 İptal Testleri

- [ ] **T15:** Taslak → IptalEdildi
- [ ] **T16:** MuhasebeOnayinda → IptalEdildi
- [ ] **T17:** MuhasebeOnaylandi → IptalEdildi
- [ ] **T18:** Reddedildi → IptalEdildi
- [ ] **T19:** IptalEdildi → IptalEtAsync → "zaten iptal" hatası

### 6.5 Güncelleme ve Silme Testleri

- [ ] **T20:** Reddedildi belgeyi güncelle → Taslak durumuna dönmeli, RedNedeni temizlenmeli
- [ ] **T21:** IptalEdildi belgeyi güncellemeye çalış → hata al
- [ ] **T22:** Taslak belgeyi sil → soft-delete (belge + satırlar)
- [ ] **T23:** MuhasebeOnayinda belgeyi silmeye çalış → hata al

---

## 7. Bu Fazda Kapsam Dışı Bırakılanlar

| Konu | Açıklama | Planlanan Faz |
|---|---|---|
| Muhasebe fişi üretimi | `MuhasebeOnaylandi` → `FaturaKesildi` geçişi ve `MuhasebeFis` oluşturma | Faz 65 |
| e-Fatura / e-Arşiv | Resmi fatura numarası atama, e-belge entegrasyonu | Faz 66+ |
| Resmi fatura numarası | `ResmiFaturaNo` alanı mevcut ama bu fazda kullanılmıyor | Faz 66+ |
| Otel / Restoran / Kamp entegrasyonları | Kaynak modül değişikliği yok | Faz 67+ |
| Frontend büyük değişikliği | UI tarafında onay akışı butonları mevcut, ek değişiklik yok | - |
| KDV hesaplama algoritması değişikliği | Mevcut algoritma yeterli | - |
| `MuhasebeFisId` entity alanı | Henüz eklenmedi | Faz 65 |

---

## 8. Değişiklik Özeti

### Değişen Dosyalar

| Dosya | Değişiklik |
|---|---|
| [`SatisBelgesiService.cs`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs) | `MuhasebeOnayinaGonderAsync` → `ValidateBelgeOnayaGonderilebilir` çağrısı eklendi. `MuhasebeOnaylaAsync` → Satirlar include + `ValidateBelgeMuhasebeOnaylanabilir` çağrısı eklendi. İki yeni private metot: `ValidateBelgeOnayaGonderilebilir` (12 kural) ve `ValidateBelgeMuhasebeOnaylanabilir` |

### Değişmeyen / Onaylananlar

- **Entity:** Değişiklik yok
- **Enum:** Değişiklik yok
- **Controller:** Yetkiler mevcut haliyle yeterli
- **Migration:** Yeni migration gerekmedi (sadece kod değişikliği)
- **Dto'lar:** Değişiklik yok
