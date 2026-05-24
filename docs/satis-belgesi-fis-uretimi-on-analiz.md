# Satış Belgesi Fiş Üretimi — Ön Analiz Raporu

**Tarih:** 24 Mayıs 2026  
**Faz:** 65A (Analiz)  
**Amaç:** Mevcut muhasebe fişi, hesap planı, dönem ve yevmiye no yapılarını incelemek; Satış Belgesi → Muhasebe Fişi dönüşümü için gerekli tasarımı belirlemek.  
**Kapsam:** Bu fazda **kod değişikliği yapılmayacak, migration oluşturulmayacak, endpoint eklenmeyecektir.**

---

## 1. İncelenen Dosyalar

| # | Dosya | Satır | Açıklama |
|---|-------|-------|----------|
| 1 | [`MuhasebeFis.cs`](backend/Muhasebe/MuhasebeFisleri/Entities/MuhasebeFis.cs) | 39 | Ana fiş entity'si |
| 2 | [`MuhasebeFisSatir.cs`](backend/Muhasebe/MuhasebeFisleri/Entities/MuhasebeFisSatir.cs) | 32 | Fiş satır entity'si |
| 3 | [`MuhasebeYevmiyeNoSayac.cs`](backend/Muhasebe/MuhasebeFisleri/Entities/MuhasebeYevmiyeNoSayac.cs) | 14 | Yevmiye no sayacı |
| 4 | [`MuhasebeFisService.cs`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs) | 2367 | Fiş servisi (tam) |
| 5 | [`MuhasebeFisRepository.cs`](backend/Muhasebe/MuhasebeFisleri/Repositories/MuhasebeFisRepository.cs) | 210 | Fiş repository |
| 6 | [`MuhasebeFisDtos.cs`](backend/Muhasebe/MuhasebeFisleri/Dtos/MuhasebeFisDtos.cs) | 419 | Tüm fiş DTO'ları |
| 7 | [`MuhasebeFisController.cs`](backend/Muhasebe/MuhasebeFisleri/Controllers/MuhasebeFisController.cs) | 201 | Fiş controller |
| 8 | [`IMuhasebeFisService.cs`](backend/Muhasebe/MuhasebeFisleri/Services/IMuhasebeFisService.cs) | 26 | Fiş servis arayüzü |
| 9 | [`MuhasebeFisTipleri.cs`](backend/Muhasebe/Common/Constants/MuhasebeFisTipleri.cs) | 23 | Fiş tipi sabitleri |
| 10 | [`MuhasebeFisDurumlari.cs`](backend/Muhasebe/Common/Constants/MuhasebeFisDurumlari.cs) | 17 | Fiş durum sabitleri |
| 11 | [`MuhasebeKaynakModulleri.cs`](backend/Muhasebe/Common/Constants/MuhasebeKaynakModulleri.cs) | 23 | Kaynak modül sabitleri |
| 12 | [`MuhasebeHesapPlani.cs`](backend/Muhasebe/MuhasebeHesapPlanlari/Entities/MuhasebeHesapPlani.cs) | 72 | Hesap planı entity'si |
| 13 | [`MuhasebeDonem.cs`](backend/Muhasebe/MuhasebeDonemleri/Entities/MuhasebeDonem.cs) | 24 | Muhasebe dönemi entity'si |
| 14 | [`StysAppDbContext.cs`](backend/Infrastructure/EntityFramework/StysAppDbContext.cs) | 2413 | DbContext (fiş/hesap ilişkileri) |
| 15 | [`SatisBelgesi.cs`](backend/Muhasebe/SatisBelgeleri/Entities/SatisBelgesi.cs) | 49 | Satış belgesi entity'si |
| 16 | [`SatisBelgesiSatiri.cs`](backend/Muhasebe/SatisBelgeleri/Entities/SatisBelgesiSatiri.cs) | 37 | Satış belgesi satır entity'si |
| 17 | [`SatisBelgesiDurumu.cs`](backend/Muhasebe/SatisBelgeleri/Enums/SatisBelgesiDurumu.cs) | 12 | Belge durum enum'u |
| 18 | [`SatisBelgesiTipi.cs`](backend/Muhasebe/SatisBelgeleri/Enums/SatisBelgesiTipi.cs) | 9 | Belge tipi enum'u |
| 19 | [`SatisKaynakModulu.cs`](backend/Muhasebe/SatisBelgeleri/Enums/SatisKaynakModulu.cs) | 11 | Kaynak modül enum'u |
| 20 | [`SatisBelgesiSatirTipi.cs`](backend/Muhasebe/SatisBelgeleri/Enums/SatisBelgesiSatirTipi.cs) | 13 | Satır tipi enum'u |

---

## 2. Mevcut Yapıların Özeti

### 2.1 MuhasebeFis Entity'si

```csharp
// MuhasebeFis.cs (39 satır)
public class MuhasebeFis : BaseEntity<int>
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public string Donem { get; set; }          // string, örn: "Ocak", "Şubat"
    public string FisNo { get; set; }           // Format: "{MaliYil}-{Kod}-{6 digit}"
    public int? YevmiyeNo { get; set; }         // Onayda atanır
    public DateTime FisTarihi { get; set; }
    public string FisTipi { get; set; }         // Sabit: Mahsup/Tahsil/Tediye/Acilis/Kapanis/Stok/Duzeltme
    public string KaynakModul { get; set; }     // Sabit: Manuel/StokHareket/.../TasinirHareket
    public int? KaynakId { get; set; }          // Kaynak entity'nin Id'si
    public string Durum { get; set; }           // Sabit: Taslak/Onayli/Iptal/TersKayit
    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }
    public string Aciklama { get; set; }
    public int? TersKayitFisId { get; set; }
    public int? IptalEdilenFisId { get; set; }
}
```

**Kritik bulgular:**
- `KaynakModul` **string** tipinde, yeni modül eklemek için sadece sabit sınıfına ekleme yeterli
- `KaynakId` **nullable int** — SatisBelgesi.Id (int) ile uyumlu
- `FisTipi` **string** — satış belgesi için `Mahsup` uygun
- `YevmiyeNo` nullable — onaylanana kadar null
- Unique constraint: `(TesisId, FisNo)` ve `(TesisId, KaynakModul, KaynakId) WHERE Durum <> 'Iptal'`

### 2.2 MuhasebeFisSatir Entity'si

```csharp
// MuhasebeFisSatir.cs (32 satır)
public class MuhasebeFisSatir : BaseEntity<int>
{
    public int MuhasebeFisId { get; set; }
    public int MuhasebeHesapPlaniId { get; set; }  // FK → MuhasebeHesapPlani
    public int SiraNo { get; set; }
    public decimal Borc { get; set; }
    public decimal Alacak { get; set; }
    public string ParaBirimi { get; set; }           // "TRY"
    public decimal Kur { get; set; }                 // 1.0
    public int? CariKartId { get; set; }
    public int? TasinirKartId { get; set; }
    public int? DepoId { get; set; }
    public int? KasaBankaHesapId { get; set; }
    public string Aciklama { get; set; }
}
```

### 2.3 MuhasebeHesapPlani

```csharp
public class MuhasebeHesapPlani : BaseEntity<int>
{
    public string Kod { get; set; }           // Son seviye kod
    public string TamKod { get; set; }         // Nokta ile ayrılmış: "120", "120.01", "120.01.001"
    public string? ResmiKod { get; set; }
    public string? UygulamaKodu { get; set; }
    public string Ad { get; set; }
    public int SeviyeNo { get; set; }
    public HesapTipi HesapTipi { get; set; }   // AnaHesap/AltHesap/DetayHesap
    public string? AnaHesapKodu { get; set; }
    public int? TesisId { get; set; }          // null = global
    public int? UstHesapId { get; set; }
    public bool AktifMi { get; set; }
    public bool DetayHesapMi { get; set; }
    public bool HareketGorebilirMi { get; set; }
}
```

**Kritik: Hesap bulma stratejisi** — mevcut `TasinirMuhasebeFisiTaslagiOlusturAsync` metodunda kullanılan pattern:

```csharp
// Önce tesis özel, sonra global hesap (aynı TamKod için)
var hesap = await _dbContext.MuhasebeHesapPlanlari
    .Where(x => x.TamKod == hedefTamKod && !x.IsDeleted && x.AktifMi 
        && x.HareketGorebilirMi && x.DetayHesapMi
        && (x.TesisId == tesisId || x.TesisId == null))
    .OrderByDescending(x => x.TesisId == tesisId)  // tesis özel öncelikli
    .FirstOrDefaultAsync();
```

### 2.4 MuhasebeDonem

```csharp
public class MuhasebeDonem : BaseEntity<int>
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public int DonemNo { get; set; }
    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }
    public bool KapaliMi { get; set; }
    public DateTime? KapanisTarihi { get; set; }
}
```

Dönem bulma: `ValidateOpenPeriodAsync` → `_muhasebeDonemService.GetAktifDonemAsync(tesisId)` → `KapaliMi == false` olan dönemi döner.

### 2.5 MuhasebeKaynakModulleri

Mevcut kaynak modülleri:
```csharp
Manuel, StokHareket, CariHareket, KasaHareket, BankaHareket, 
TahsilatOdemeBelgesi, TasinirHareket
```

**Yeni modül eklenmesi gerekenler:**
- `SatisBelgesi` — satış belgesi kaynaklı fişler için

### 2.6 SatisBelgesi Entity'si (Mevcut Durum)

```csharp
public class SatisBelgesi : BaseEntity<int>
{
    public string BelgeNo { get; set; }
    public SatisBelgesiTipi BelgeTipi { get; set; }    // FaturaTaslagi, SatisFaturasi, IadeFaturasi, Proforma
    public SatisBelgesiDurumu Durum { get; set; }       // Taslak..IptalEdildi
    public SatisKaynakModulu KaynakModul { get; set; }
    public string? KaynakTipi { get; set; }
    public string? KaynakId { get; set; }
    public int? TesisId { get; set; }
    public DateTime BelgeTarihi { get; set; }
    public DateTime? VadeTarihi { get; set; }
    // Müşteri bilgileri...
    public bool KurumsalMi { get; set; }
    public decimal ToplamMatrah { get; set; }
    public decimal ToplamKdv { get; set; }
    public decimal GenelToplam { get; set; }
    public string? Aciklama { get; set; }
    public string? RedNedeni { get; set; }
    public string? ResmiFaturaNo { get; set; }
    public string? EBelgeUuid { get; set; }
    public DateTime? MuhasebeOnayinaGonderilmeTarihi { get; set; }
    public DateTime? MuhasebeOnayTarihi { get; set; }
    public DateTime? FaturaKesimTarihi { get; set; }
    public DateTime? MusteriyeGonderimTarihi { get; set; }
}
```

**🚨 Kritik bulgu:** `SatisBelgesi` entity'sinde **`MuhasebeFisId` alanı yok!**  
Bu alanın **Faz 65B'de eklenmesi zorunludur**. Fiş oluşturulduktan sonra belge ↔ fiş ilişkisini takip etmek için gereklidir.

### 2.7 SatisBelgesiSatiri Entity'si

```csharp
public class SatisBelgesiSatiri : BaseEntity<int>
{
    public int SatisBelgesiId { get; set; }
    public int SiraNo { get; set; }
    public SatisBelgesiSatirTipi SatirTipi { get; set; }
    public string Aciklama { get; set; }
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal Matrah { get; set; }
    public KdvUygulamaTipi KdvUygulamaTipi { get; set; }
    public int? KdvIstisnaTanimId { get; set; }
    public string? KdvIstisnaKodu { get; set; }
    public string? KdvIstisnaAciklamasi { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal KdvTutari { get; set; }
    public decimal SatirToplami { get; set; }
    public string? KaynakSatirId { get; set; }
}
```

### 2.8 Mevcut Otomatik Fiş Üretim Örneği: TasinirMuhasebeFisiTaslagiOlusturAsync

Bu metot (2367 satırlık servisin ~1887-2365 satırları), **takip edilmesi gereken referans implementasyondur**:

1. Dönem kontrolü (`ValidateOpenPeriodAsync`)
2. Taşınır kod → muhasebe hesap eşlemesi (`TasinirKodMuhasebeHesapEsleme`)
3. Borç hesabı tespiti (TamKod ile)
4. KDV yönü tespiti (giriş → 191, çıkış → 391)
5. Alacak hesabı tespiti (TamKod ile)
6. KDV hesabı kontrolü (`GetKdvHesabiAsync`)
7. KDV hesaplama
8. Kaynak modül/ID belirleme
9. Duplicate fiş kontrolü (transaction dışı + transaction içi)
10. Açıklama oluşturma
11. Transaction içinde fiş + satır oluşturma (3 retry ile FisNo çakışması)

---

## 3. Analiz Soruları ve Cevapları

### Q1: MuhasebeFis entity'si SatisBelgesi bilgilerini taşıyabilir mi?

**✅ Evet.** `KaynakModul` ve `KaynakId` alanları tam olarak bu amaçla tasarlanmıştır. `KaynakModul = "SatisBelgesi"`, `KaynakId = satisBelgesi.Id` ile belge ↔ fiş ilişkisi kurulabilir.

### Q2: Yeni bir KaynakModul sabiti eklenmeli mi?

**✅ Evet.** [`MuhasebeKaynakModulleri`](backend/Muhasebe/Common/Constants/MuhasebeKaynakModulleri.cs:3) sınıfına `SatisBelgesi` sabiti eklenmelidir.

### Q3: KaynakId (int?) SatisBelgesi.Id (int) için uygun mu?

**✅ Evet.** SatisBelgesi.Id integer tipindedir ve KaynakId nullable int ile tam uyumludur.

### Q4: Fiş tipi ne olmalı?

**Mahsup** fişi olmalıdır. Satış belgesi, borç/alacak dengelemesi yapan bir muhasebe kaydıdır. Mevcut `TasinirMuhasebeFisiTaslagiOlusturAsync` de Mahsup tipini kullanmaktadır.

### Q5: Fiş hangi durumda oluşturulmalı?

İki yaklaşım değerlendirilmiştir:

| Yaklaşım | Tetikleyici Durum | Artıları | Eksileri |
|----------|-------------------|----------|----------|
| **A (Önerilen)** | `MuhasebeOnaylandi` | Otomatik, kullanıcı hatasız | Onay anında ek iş yükü |
| B | `MuhasebeOnayinda` | Erken oluşturma | Reddedilirse gereksiz fiş, iptal karmaşası |
| C | Manuel buton | Kullanıcı kontrolü | Unutulabilir, ek UI ihtiyacı |

**Öneri: Yaklaşım A** — `MuhasebeOnaylandi` durumuna geçişte otomatik fiş oluşturma. Fiş **Taslak** olarak oluşturulur, muhasebecinin son kontrolden sonra onaylaması beklenir.

### Q6: Fiş Taslak mı yoksa Onayli olarak mı oluşturulmalı?

**Taslak** olarak oluşturulmalıdır. Gerekçe:
- Muhasebecinin fişi inceleme ve gerekirse düzeltme şansı olur
- `TasinirMuhasebeFisiTaslagiOlusturAsync` de Taslak oluşturmaktadır
- Otomatik onay risklidir — hesap eşleşmeleri, KDV tutarları kontrol edilmelidir
- Fiş ancak muhasebeci tarafından `OnaylaAsync` çağrıldığında onaylanmalıdır

### Q7: Aynı belgeden ikinci kez fiş oluşturulması nasıl engellenir?

Üç katmanlı koruma:

1. **SatisBelgesi.MuhasebeFisId kontrolü:** Belge üzerinde `MuhasebeFisId != null` ise zaten fiş var demektir.
2. **Kaynak duplicate kontrolü (transaction dışı):** `KaynakModul == "SatisBelgesi" && KaynakId == belge.Id && Durum != "Iptal"` ile mevcut fiş sorgulanır.
3. **Transaction içi duplicate kontrolü:** Race condition önlemi — mevcut `TasinirMuhasebeFisiTaslagiOlusturAsync`'teki gibi transaction içinde tekrar kontrol.

Mevcut **DB unique constraint** de koruma sağlar:
```sql
-- StysAppDbContext satır 1928-1930
CREATE UNIQUE INDEX ON MuhasebeFisler (TesisId, KaynakModul, KaynakId) 
WHERE IsDeleted = 0 AND KaynakModul IS NOT NULL AND KaynakId IS NOT NULL AND Durum <> 'Iptal'
```

### Q8: SatisBelgesi.MuhasebeFisId alanı gerekli mi?

**✅ Evet, zorunludur.** Gerekçe:
- Fiş → belge navigasyonu (`MuhasebeFis.KaynakId` ile) zaten mevcut
- Ancak belge → fiş navigasyonu için `MuhasebeFisId` gereklidir
- Kullanıcı arayüzünde "Fişe Git" butonu için gerekli
- Fiş durum takibi için (belge detayında fiş durumunu gösterme)
- Bu alan **Faz 65B'de migration ile eklenecektir**

### Q9: Kaç fiş satırı oluşturulmalı?

Standart satış faturası için **2 satır** (KDV'siz durumda) veya **3 satır** (KDV'li durumda):

| Senaryo | Satır 1 | Satır 2 | Satır 3 |
|---------|---------|---------|---------|
| KDV'li Satış | 120 ALICILAR (Borç) — GenelToplam | 600 YURTİÇİ SATIŞLAR (Alacak) — Matrah | 391 HESAPLANAN KDV (Alacak) — KdvTutari |
| KDV'siz (İstisna) | 120 ALICILAR (Borç) — GenelToplam | 600 YURTİÇİ SATIŞLAR (Alacak) — GenelToplam | — |
| İade Faturası | 120 ALICILAR (Alacak) — GenelToplam | 600 YURTİÇİ SATIŞLAR (Borç) — Matrah | 391 HESAPLANAN KDV (Borç) — KdvTutari |

### Q10: 120 Alıcılar hesabı nasıl bulunmalı?

Hesap planından `TamKod == "120"` (veya tesis özel `"120"`) ile `DetayHesapMi == true && HareketGorebilirMi == true` olan hesap bulunur.

**Önemli:** Müşteri bilgisi varsa, 120'nin alt hesabı (örn: `"120.01"`, `"120.01.001"`) kullanılmalıdır. Cari kart entegrasyonu için:

```csharp
// CariKart.AnaMuhasebeHesapKodu üzerinden alt hesap bulma
var cariHesap = await _dbContext.MuhasebeHesapPlanlari
    .Where(x => x.AnaHesapKodu == "120" && x.TamKod == "120.01" 
        && x.DetayHesapMi && x.HareketGorebilirMi)
    .FirstOrDefaultAsync();
```

Ancak cari kart tablosu (`CariKart`) henüz SatisBelgesi ile ilişkilendirilmemiştir. Faz 65B'de bu ilişkinin nasıl kurulacağı değerlendirilmelidir.

### Q11: 600 Yurtiçi Satışlar hesabı nasıl bulunmalı?

Hesap planından `TamKod == "600"` ile `DetayHesapMi == true && HareketGorebilirMi == true` olan hesap bulunur. Tesis özel hesap varsa önceliklidir.

### Q12: 391 Hesaplanan KDV hesabı nasıl bulunmalı?

Mevcut `GetKdvHesabiAsync` metodu aynen kullanılabilir:

```csharp
// MuhasebeFisService.cs satır 1527-1556
var kdvHesap = await GetKdvHesabiAsync(tesisId, "391", cancellationToken);
```

Bu metot önce `MuhasebeVergiHesapEsleme` tablosundan tesis özel eşleşmeyi, yoksa global eşleşmeyi bulur.

### Q13: KDV istisnalı (tam/kısmi istisna, kapsam dışı) işlemlerde fiş nasıl oluşturulmalı?

KDV tutarı 0 ise **KDV satırı oluşturulmaz**. Sadece 2 satırlı fiş:
- 120 ALICILAR (Borç) — GenelToplam (= Matrah)
- 600 YURTİÇİ SATIŞLAR (Alacak) — GenelToplam

Açıklamaya istisna türü ve kodu eklenir. Mevcut `TasinirMuhasebeFisiTaslagiOlusturAsync`'teki pattern takip edilir (satır 2118-2145).

### Q14: Tevkifatlı KDV durumunda fiş nasıl olmalı?

Tevkifatlı KDV'de KDV'nin bir kısmı alıcı tarafından ödenir. Bu durumda:
- 120 ALICILAR (Borç) — Matrah + Alıcıya Düşen KDV
- 600 YURTİÇİ SATIŞLAR (Alacak) — Matrah
- 391 HESAPLANAN KDV (Alacak) — Tevkifat tutarı (tam KDV)
- 360 ÖDENECEK VERGİLER (Alacak?) — Alıcı tarafından ödenecek kısım

⚠️ **Tevkifatlı KDV senaryosu karmaşıktır.** Faz 65B'de bu senaryonun ayrıca ele alınması, gerekirse Faz 65C'ye ertelenmesi önerilir. İlk aşamada tevkifatlı belgeler için **manuel fiş oluşturma** yönlendirmesi yapılabilir.

### Q15: İade faturası (SatisBelgesiTipi.IadeFaturasi) için nasıl fiş oluşturulmalı?

Satış faturasının **tersi** yönde kayıt:

| Satır | Hesap | Yön | Tutar |
|-------|-------|-----|-------|
| 1 | 600 YURTİÇİ SATIŞLAR | Borç | Matrah |
| 2 | 391 HESAPLANAN KDV | Borç | KdvTutari |
| 3 | 120 ALICILAR | Alacak | GenelToplam |

Bu, mevcut `TasinirMuhasebeFisiTaslagiOlusturAsync`'teki giriş/çıkış mantığına benzer şekilde uygulanabilir.

### Q16: Satır tiplerine göre farklı hesap kurgusu gerekli mi?

Şu an için **hayır**. Tüm satır tipleri (Konaklama, YiyecekIcecek, KampHizmeti, vb.) aynı 600 hesabı altında toplanabilir. İleride farklı gelir hesaplarına (601, 602, vb.) bölmek gerekirse:

```csharp
var gelirHesapKodu = satirTipi switch
{
    SatisBelgesiSatirTipi.Konaklama => "600.01",
    SatisBelgesiSatirTipi.YiyecekIcecek => "600.02",
    // ...
    _ => "600"
};
```

**Faz 65B'de sabit "600" kullanılması, esneklik için yapılandırılabilir altyapı bırakılması önerilir.**

### Q17: CariKart (Cari Kart) entegrasyonu gerekli mi?

Şu an için **zorunlu değil** ama faydalı olur. Cari kart varsa:
- `MuhasebeFisSatir.CariKartId` doldurulabilir
- 120 hesabının alt detay hesabı (müşteri bazlı) kullanılabilir
- Muavin defter raporlarında müşteri takibi yapılabilir

Faz 65B'de cari kart olmadan da çalışan, varsa cari kartı kullanan bir yapı kurulmalıdır. SatisBelgesi'nde `MusteriVergiNo`/`MusteriTcKimlikNo` alanları mevcut — bu bilgilerle CariKart eşleşmesi yapılabilir.

### Q18: Dönem kontrolü nasıl yapılmalı?

Mevcut `ValidateOpenPeriodAsync` metodu aynen kullanılmalıdır:

```csharp
// MuhasebeFisService.cs satır 1562-1579
var donem = await ValidateOpenPeriodAsync(tesisId, fisTarihi, cancellationToken);
```

Bu metot `BelgeTarihi`'nin içinde bulunduğu dönemin açık olup olmadığını kontrol eder, kapalıysa hata fırlatır.

### Q19: Yevmiye no üretimi nasıl olmalı?

**Fiş oluşturma sırasında değil, onaylama sırasında.** Mevcut sistemde de fiş Taslak oluşturulduğunda yevmiye no atanmaz, `OnaylaAsync` çağrıldığında `YevmiyeNoUretAsync` ile üretilir. Bu pattern aynen korunmalıdır.

### Q20: Fiş onayı otomatik mi yapılmalı?

**Hayır, manuel olmalıdır.** Fiş Taslak oluşturulur, muhasebeci [`OnaylaAsync`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs:110) endpoint'ini çağırarak onaylar. Gerekçe:
- 13 adımlı onay validasyonu mevcut
- Muhasebecinin hesap kontrolü yapması gerekir
- Yanlış otomatik onayın düzeltilmesi zordur

### Q21: Fiş iptal senaryosu nasıl olmalı?

Mevcut `IptalEtAsync` mekanizması aynen geçerlidir:
- Onaylı fiş iptal edilir → otomatik ters kayıt fişi oluşturulur
- Ters kayıt fişinde borç/alacak swap edilir
- Orijinal fiş `Durum = "Iptal"` ve `TersKayitFisId` atanır

**SatisBelgesi tarafında:** Belge iptal edildiğinde, eğer bağlı fiş varsa, fişin de iptal edilmesi gerekir. Alternatif olarak belge iptalinde fiş iptali zorunlu tutulabilir.

### Q22: Belge tutarları ile fiş toplamları nasıl eşleşmeli?

| Belge Alanı | Fiş Karşılığı |
|-------------|---------------|
| ToplamMatrah | 600 hesap alacak tutarı |
| ToplamKdv | 391 hesap alacak tutarı |
| GenelToplam | 120 hesap borç tutarı |
| — | ToplamBorc = GenelToplam, ToplamAlacak = GenelToplam |

**Fiş dengesi:** `ToplamBorc == ToplamAlacak` olmalıdır. Mevcut `OnaylaAsync` bunu zorunlu kılar.

### Q23: Birden fazla KDV oranı olan belgeler nasıl işlenmeli?

SatisBelgesi **belge bazında** tek `ToplamMatrah`/`ToplamKdv`/`GenelToplam` tutar. Satırlar farklı KDV oranlarına sahip olabilir ancak belge seviyesinde toplam tutarlar üzerinden fiş oluşturulur. Bu durumda:

- Farklı KDV oranlarına sahip satırlar olsa da, fiş **tek bir KDV satırı** içerir
- KDV detayı fiş açıklamasında belirtilebilir
- İleride KDV oranı bazında ayrı fiş satırları istenirse genişletilebilir

### Q24: Mevcut TasinirMuhasebeFisiTaslagiOlusturAsync'ten ne kadar faydalanılabilir?

**Yaklaşık %80.** Temel akış (dönem kontrolü, hesap bulma, duplicate kontrolü, transaction yönetimi, FisNo üretimi) aynen kullanılabilir. Farklılıklar:

| Unsur | Taşınır Fişi | Satış Belgesi Fişi |
|-------|-------------|-------------------|
| Hesap eşleme kaynağı | TasinirKodMuhasebeHesapEsleme | Sabit "600" / "120" |
| KDV yönü | Hareket tipine bağlı (giriş/çıkış) | Her zaman çıkış (satış) |
| Borç/Alacak hesabı | Stok hesabı + KDV / Karşı hesap | 120 Alıcılar / 600 Satışlar |
| Duplicate kontrol | StokHareket kaynaklı | SatisBelgesi kaynaklı |
| Kaynak modül | TasinirHareket | SatisBelgesi (yeni) |

---

## 4. Risk Analizi

### Risk 1: Hesap planında 120/600/391 hesaplarının bulunamaması

**Olasılık:** Orta  
**Etki:** Yüksek (fiş oluşturulamaz)  
**Azaltma:**
- Hesap bulunamazsa açıklayıcı hata mesajı verilmeli
- Admin'e sistem kurulumunda bu hesapları tanımlaması için uyarı sistemi
- Alternatif hesap kodu konfigürasyonu (ileride)

### Risk 2: Aynı belge için mükerrer fiş oluşturma

**Olasılık:** Düşük  
**Etki:** Yüksek (mükerrer muhasebe kaydı, bakiye bozulması)  
**Azaltma:**
- Üç katmanlı koruma: `MuhasebeFisId` null check + transaction dışı duplicate + transaction içi duplicate
- DB unique constraint zaten mevcut
- Idempotency key olarak `(TesisId, KaynakModul, KaynakId)`

### Risk 3: Fiş oluşturma sırasında transaction/db hatası

**Olasılık:** Düşük  
**Etki:** Orta (belge onaylanır ama fiş oluşmaz)  
**Azaltma:**
- `MuhasebeOnaylandi` durumu fiş başarıyla oluştuktan sonra commit edilmeli
- Retry mekanizması (mevcut 3 deneme)
- Başarısız olursa belgeyi `MuhasebeOnayinda`'ya geri alma veya manuel müdahale için loglama

### Risk 4: KDV tutar uyuşmazlığı

**Olasılık:** Orta  
**Etki:** Orta (fiş dengesi bozulur, onaylanamaz)  
**Azaltma:**
- Belge toplamları (`ToplamMatrah + ToplamKdv == GenelToplam`) fiş oluşturmadan önce validate edilmeli
- KDV yuvarlama farkları için küçük tolerans (0.01 TL)

### Risk 5: Kapalı döneme fiş oluşturma

**Olasılık:** Düşük  
**Etki:** Orta  
**Azaltma:**
- `ValidateOpenPeriodAsync` ile dönem kontrolü
- Belge tarihi ile dönem uyuşmazlığında net hata mesajı

### Risk 6: Concurrent fiş oluşturma (race condition)

**Olasılık:** Düşük  
**Etki:** Yüksek  
**Azaltma:**
- Transaction içi duplicate check
- Db unique constraint son savunma hattı
- `FisNo` üretimi transaction içinde (mevcut yapı)

### Risk 7: Belge iptali → fiş iptali senkronizasyonu

**Olasılık:** Orta  
**Etki:** Orta  
**Azaltma:**
- Belge iptal edilirken bağlı fiş varsa otomatik iptal etme
- Veya belge iptalini fiş iptal edilene kadar engelleme
- Faz 65C'de ele alınması önerilir

### Risk 8: Cari kart eşleşme sorunu

**Olasılık:** Orta  
**Etki:** Düşük (cari kart olmadan da fiş oluşturulabilir)  
**Azaltma:**
- Cari kart opsiyonel olsun
- Eşleşme yoksa genel 120 hesabı kullanılsın
- Vergi no/TCKN ile eşleştirme yapılsın

### Risk 9: Performans (büyük hacimli belge onayı)

**Olasılık:** Düşük  
**Etki:** Düşük  
**Azaltma:**
- Tek belge için tek fiş → performans etkisi minimal
- Toplu onay senaryosu için batch processing düşünülebilir (ileride)

---

## 5. Hesap Kurgusu Önerisi (120 / 600 / 391)

### 5.1 Standart KDV'li Satış Faturası

```
FİŞ TİPİ: Mahsup
KAYNAK MODÜL: SatisBelgesi

SATIR 1: 120 ALICILAR HESABI                    BORÇ    GenelToplam
SATIR 2: 600 YURTİÇİ SATIŞLAR HESABI            ALACAK  ToplamMatrah
SATIR 3: 391 HESAPLANAN KDV HESABI              ALACAK  ToplamKdv

TOPLAM BORÇ = TOPLAM ALACAK = GenelToplam ✓
```

### 5.2 KDV İstisnalı / Kapsam Dışı Satış Faturası

```
SATIR 1: 120 ALICILAR HESABI                    BORÇ    GenelToplam (Matrah)
SATIR 2: 600 YURTİÇİ SATIŞLAR HESABI            ALACAK  GenelToplam (Matrah)

TOPLAM BORÇ = TOPLAM ALACAK = Matrah ✓
(KDV satırı yok)
```

### 5.3 İade Faturası

```
SATIR 1: 600 YURTİÇİ SATIŞLAR HESABI            BORÇ    ToplamMatrah
SATIR 2: 391 HESAPLANAN KDV HESABI              BORÇ    ToplamKdv
SATIR 3: 120 ALICILAR HESABI                    ALACAK  GenelToplam

TOPLAM BORÇ = TOPLAM ALACAK = GenelToplam ✓
```

### 5.4 Hesap Kodları Hiyerarşisi

Tesis özel hesap kullanımı desteklenmelidir. Arama stratejisi:

```
1. TAMKOD = "120" AND TesisId = @TesisId → tesis özel
2. TAMKOD = "120" AND TesisId IS NULL    → global
3. TAMKOD = "600" AND TesisId = @TesisId → tesis özel
4. TAMKOD = "600" AND TesisId IS NULL    → global
5. 391 için GetKdvHesabiAsync()          → vergi eşleme üzerinden
```

---

## 6. Tasarım Önerileri

### 6.1 Yeni Metot: SatisBelgesiMuhasebeFisiTaslagiOlusturAsync

**Konum:** [`MuhasebeFisService`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs) (yeni metod)

**İmza:**
```csharp
public async Task<TasinirMuhasebeFisiOlusturResultDto> 
    SatisBelgesiMuhasebeFisiTaslagiOlusturAsync(
        SatisBelgesiMuhasebeFisiOlusturRequest request,
        CancellationToken cancellationToken = default)
```

**Akış:**
1. Dönem kontrolü (`ValidateOpenPeriodAsync`)
2. Belgeyi bul ve doğrula (`MuhasebeOnaylandi` durumunda mı?)
3. Belgenin `MuhasebeFisId` alanı null mı?
4. Kaynak duplicate kontrolü (transaction dışı)
5. 120 Alıcılar hesabını bul
6. 600 Satışlar hesabını bul
7. KDV varsa 391 hesabını bul (`GetKdvHesabiAsync`)
8. KDV hesaplama (belge toplamlarından al)
9. Belge tipine göre borç/alacak yönlerini belirle (Satış/İade)
10. KDV durumuna göre satır sayısını belirle (2 veya 3)
11. **YENİ KONTROL: SatisBelgesi → fiş toplam tutarlılığı** (`ToplamMatrah + ToplamKdv ≈ GenelToplam`)
12. Transaction içinde fiş + satır oluştur (retry ile)
13. Fiş ID'sini belgeye geri yaz (`SatisBelgesi.MuhasebeFisId = fis.Id`)

### 6.2 Gerekli Yeni DTO

```csharp
public class SatisBelgesiMuhasebeFisiOlusturRequest
{
    public int SatisBelgesiId { get; set; }
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public DateTime FisTarihi { get; set; }
}
```

### 6.3 Gerekli Sabit Ekleme

[`MuhasebeKaynakModulleri`](backend/Muhasebe/Common/Constants/MuhasebeKaynakModulleri.cs:3):
```csharp
public const string SatisBelgesi = "SatisBelgesi";
```

### 6.4 Gerekli Entity Değişikliği (Faz 65B Migration)

[`SatisBelgesi`](backend/Muhasebe/SatisBelgeleri/Entities/SatisBelgesi.cs:6):
```csharp
public int? MuhasebeFisId { get; set; }
public MuhasebeFis? MuhasebeFis { get; set; }
```

### 6.5 İsteğe Bağlı Servis

[`SatisBelgesiService`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs)'e eklenecek:
```csharp
// MuhasebeOnaylaAsync içinde, onay başarılı olduktan sonra:
if (belge.Durum == SatisBelgesiDurumu.MuhasebeOnaylandi)
{
    await _muhasebeFisService.SatisBelgesiMuhasebeFisiTaslagiOlusturAsync(
        new SatisBelgesiMuhasebeFisiOlusturRequest
        {
            SatisBelgesiId = belge.Id,
            TesisId = belge.TesisId.Value,
            MaliYil = maliYil,
            FisTarihi = belge.BelgeTarihi
        }, cancellationToken);
}
```

---

## 7. Faz 65B/65C için Eylem Planı

### Faz 65B: MuhasebeFiş Entegrasyonu

| Sıra | Görev | Açıklama |
|------|-------|----------|
| 1 | Migration: `SatisBelgesi.MuhasebeFisId` | Nullable FK ekle |
| 2 | Sabit: `MuhasebeKaynakModulleri.SatisBelgesi` | Yeni kaynak modülü |
| 3 | DTO: `SatisBelgesiMuhasebeFisiOlusturRequest` | Yeni request DTO'su |
| 4 | Metot: `SatisBelgesiMuhasebeFisiTaslagiOlusturAsync` | Ana fiş üretim metodu |
| 5 | Entegrasyon: `SatisBelgesiService.MuhasebeOnaylaAsync` | Onay sonrası fiş oluşturma |
| 6 | Validasyon: Belge toplamları tutarlılık kontrolü | |
| 7 | Testler | Birim + entegrasyon testleri |
| 8 | Dokümantasyon güncelleme | |

### Faz 65C: Gelişmiş Özellikler (Opsiyonel)

| Sıra | Görev | Açıklama |
|------|-------|----------|
| 1 | Tevkifatlı KDV desteği | Karmaşık fiş yapısı |
| 2 | CariKart otomatik eşleme | Vergi no/TCKN ile |
| 3 | Belge iptali → fiş iptali | Otomatik senkronizasyon |
| 4 | Toplu onay + fiş üretimi | Batch processing |

---

## 8. Sonuç

Satış belgesinden muhasebe fişi üretimi için gerekli tüm altyapı **mevcuttur**. Mevcut `TasinirMuhasebeFisiTaslagiOlusturAsync` metodu, yeni `SatisBelgesiMuhasebeFisiTaslagiOlusturAsync` için %80 oranında referans alınabilecek bir implementasyondur.

**Temel değişiklikler:**
1. `SatisBelgesi.MuhasebeFisId` alanı (migration)
2. `MuhasebeKaynakModulleri.SatisBelgesi` sabiti
3. `SatisBelgesiMuhasebeFisiTaslagiOlusturAsync` metodu
4. `SatisBelgesiService.MuhasebeOnaylaAsync` entegrasyonu

**Önerilen hesap kurgusu:** 120 Alıcılar (Borç), 600 Satışlar (Alacak), 391 KDV (Alacak) — iade faturası için ters yön.

**Tahmini iş yükü:** Faz 65B — 3-4 saat. Faz 65C — 2-3 saat (opsiyonel).
