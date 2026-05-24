# Satış Belgesi Bağlı Muhasebe Fişi İptal / Ters Kayıt Ön Analizi

**Faz 67** — Yalnızca analiz fazıdır. Kod değişikliği yapılmaz.

## 1. Mevcut Muhasebe Fişi İptal Yapısı

### 1.1 MuhasebeFis Entity

[`MuhasebeFis`](backend/Muhasebe/MuhasebeFisleri/Entities/MuhasebeFis.cs) entity'sinde iptal/ters kayıt için iki kritik alan vardır:

| Alan | Tip | Açıklama |
|------|-----|----------|
| `TersKayitFisId` | `int?` | Orijinal fiş iptal edildiğinde, oluşturulan ters kayıt fişinin ID'si buraya yazılır |
| `IptalEdilenFisId` | `int?` | Ters kayıt fişinde, hangi fişi iptal ettiğini gösterir |

İlişkiler:
- `TersKayitFis` → navigation to the reversal voucher
- `IptalEdilenFis` → navigation to the cancelled voucher

### 1.2 MuhasebeFisDurumlari

[`MuhasebeFisDurumlari`](backend/Muhasebe/Common/Constants/MuhasebeFisDurumlari.cs:3-16):

| Durum | Açıklama |
|-------|----------|
| `Taslak` | Yeni oluşturulmuş, henüz onaylanmamış |
| `Onayli` | Onaylanmış, yevmiye no almış, bakiyelere işlenmiş |
| `Iptal` | İptal edilmiş orijinal fiş (ters kayıt oluşturulduktan sonra) |
| `TersKayit` | Ters kayıt fişi (başka bir fişi iptal etmek için oluşturulmuş) |

### 1.3 IptalEtAsync Metodu

[`MuhasebeFisService.IptalEtAsync`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs:211-348) tam iş akışı:

1. **Orijinal fişi satırlarıyla getir** (Include Satirlar)
2. **Zaten iptal edilmemiş olmalı**: `Durum != Iptal`
3. **Ters kayıt fişi iptal edilemez**: `Durum != TersKayit`
4. **Sadece Onayli fiş iptal edilebilir**: `Durum == Onayli`
5. **TersKayitFisId doluysa zaten iptal**: Ek güvenlik
6. **YevmiyeNo dolu olmalı**: Onaylanmış fiş garantisi
7. **Açık dönem kontrolü**: `ValidateOpenPeriodAsync`
8. **Aktif satırlar en az 2**
9. **Ters kayıt fişi oluştur**:
   - `Durum = TersKayit`
   - `FisTipi = Duzeltme`
   - `FisNo = "TERS-" + orijinalFis.FisNo`
   - `KaynakModul = orijinalFis.KaynakModul` (aynen korunur)
   - `KaynakId = orijinalFis.KaynakId` (aynen korunur)
   - `IptalEdilenFisId = orijinalFis.Id`
   - **Borç/Alacak ters çevrilir**: Her satırda `Borc = s.Alacak`, `Alacak = s.Borc`
   - `ToplamBorc = orijinalFis.ToplamAlacak`, `ToplamAlacak = orijinalFis.ToplamBorc`
10. **Ters kayıt dengesi kontrolü**: ToplamBorc == ToplamAlacak
11. **Satır hesap planı doğrulaması**: Her hesap aktif, detay, hareket görebilir olmalı
12. **Yevmiye no üret**: Ters kayıt fişine de yevmiye no atanır
13. **Ters kayıt fişini kaydet**
14. **Orijinal fişi iptal et**: `Durum = Iptal`, `TersKayitFisId = tersFis.Id`
15. **Ters kayıt bakiyelerini güncelle**: `FisBakiyeleriniIsleAsync`
16. **Transaction commit**

### 1.4 OnaylaAsync Metodu

[`MuhasebeFisService.OnaylaAsync`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs:110-209):

- Sadece `Taslak` → `Onayli`
- Yevmiye no üretir
- Hesap bakiyelerini günceller
- 14 aşamalı validasyon zinciri

### 1.5 UpdateAsync ve DeleteAsync

- **UpdateAsync**: Sadece `Taslak` fişler güncellenebilir
- **DeleteAsync**: Sadece `Taslak` fişler silinebilir (soft-delete). Onaylı fişler için: "Onaylı fişler iptal/ters kayıt ile kapatılmalıdır."

### 1.6 MuhasebeFisTipleri

[`MuhasebeFisTipleri`](backend/Muhasebe/Common/Constants/MuhasebeFisTipleri.cs:3-23):

| Fiş Tipi | Kod | Açıklama |
|----------|-----|----------|
| Mahsup | MHS | Genel mahsup fişi |
| Tahsil | THS | Tahsilat fişi |
| Tediye | TDY | Tediye (ödeme) fişi |
| Acilis | ACL | Açılış fişi |
| Kapanis | KPN | Kapanış fişi |
| Stok | STK | Stok hareket fişi |
| **Duzeltme** | **DZT** | **Düzeltme/ters kayıt fişi** |

Satış belgesinden oluşturulan fiş `Mahsup` tipindedir. Ters kayıt fişi `Duzeltme` tipindedir.

### 1.7 MuhasebeKaynakModulleri

[`MuhasebeKaynakModulleri`](backend/Muhasebe/Common/Constants/MuhasebeKaynakModulleri.cs:12):

```csharp
public const string SatisBelgesi = "SatisBelgesi";
```

Ters kayıt fişinde `KaynakModul` aynen korunur (`SatisBelgesi`), `KaynakId` de aynen korunur (satış belgesi ID'si).

### 1.8 MuhasebeFisController

[`MuhasebeFisController`](backend/Muhasebe/MuhasebeFisleri/Controllers/MuhasebeFisController.cs:85-93):

```csharp
[HttpPost("{id:int}/iptal")]
[Permission(StructurePermissions.MuhasebeFisYonetimi.Manage)]
public async Task<ActionResult<MuhasebeFisDto>> IptalEt(
    int id,
    [FromBody] MuhasebeFisIptalRequest? request,
    CancellationToken cancellationToken)
```

Endpoint route: `POST /ui/muhasebe/fisler/{id}/iptal`

---

## 2. Satış Belgesi Üzerindeki Mevcut Koruma (Faz 66)

[`SatisBelgesiService`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs:70-79) içinde `ThrowIfMuhasebeFisiOlusmus` helper'ı:

```csharp
private static void ThrowIfMuhasebeFisiOlusmus(SatisBelgesi belge, string islemAdi)
{
    if (belge.MuhasebeFisId.HasValue)
    {
        throw new BaseException(
            $"Bu satış belgesi için muhasebe fişi oluşturulduğundan {islemAdi} işlemi yapılamaz. " +
            "Önce bağlı muhasebe fişi için iptal/ters kayıt süreci işletilmelidir.",
            errorCode: 400);
    }
}
```

Bu helper **sadece `MuhasebeFisId.HasValue`** kontrolü yapar. Bağlı fişin durumuna (`Taslak`, `Onayli`, `Iptal`, `TersKayit`) bakmaz. Fişin gerçekten var olup olmadığını da kontrol etmez.

### 2.1 Korunan Metotlar

| Metot | Koruma |
|-------|--------|
| `UpdateAsync` | ✅ MuhasebeFisId doluysa güncelleme engellenir |
| `DeleteAsync` | ✅ MuhasebeFisId doluysa silme engellenir |
| `IptalEtAsync` | ✅ MuhasebeFisId doluysa iptal engellenir |
| `ReddetAsync` | ✅ MuhasebeFisId doluysa reddetme engellenir |
| `MuhasebeOnayinaGonderAsync` | ✅ MuhasebeFisId doluysa onaya gönderme engellenir |
| `MuhasebeOnaylaAsync` | ✅ MuhasebeFisId doluysa onaylama engellenir |

---

## 3. 20 Analiz Sorusunun Cevapları

### Soru 1: MuhasebeFisService içinde iptal metodu var mı?

**Evet.** [`IptalEtAsync`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs:211-348) metodu mevcuttur. Tam teşekküllü bir iptal akışı içerir: ters kayıt fişi oluşturur, orijinal fişi iptal eder, bakiyeleri günceller.

### Soru 2: MuhasebeFisService içinde ters kayıt oluşturma metodu var mı?

**Evet, `IptalEtAsync` içinde.** Ters kayıt fişi oluşturma, iptal akışının ayrılmaz bir parçasıdır. Ayrı bir "sadece ters kayıt oluştur" metodu yoktur. `IptalEtAsync` hem ters kayıt fişini oluşturur hem de orijinal fişi iptal eder.

### Soru 3: Muhasebe fişi hangi durumlarda iptal edilebiliyor?

**Sadece `Onayli` durumundaki fişler.** Kodda açık kontrol:
```csharp
if (orijinalFis.Durum != MuhasebeFisDurumlari.Onayli)
    throw new BaseException("Yalnızca onaylı durumdaki fişler iptal edilebilir.", 400);
```

Taslak fişler `IptalEtAsync` ile iptal edilemez; onlar `DeleteAsync` ile silinir.

### Soru 4: Taslak fiş iptal/silinebiliyor mu?

**Evet, silinebiliyor.** [`DeleteAsync`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs:1438-1479) ile soft-delete yapılır. `IptalEtAsync` ile iptal edilemez (Durum != Onayli hatası alır).

### Soru 5: Onaylı fiş iptal edilebiliyor mu?

**Evet.** `Onayli` durumundaki fiş `IptalEtAsync` ile iptal edilebilir. Bu işlem otomatik olarak ters kayıt fişi oluşturur.

### Soru 6: Onaylı fiş için ters kayıt zorunlu mu?

**Evet.** Mevcut `IptalEtAsync` implementasyonu ters kayıt fişini zorunlu olarak oluşturur. Onaylı bir fişi ters kayıt oluşturmadan iptal etmek mümkün değildir. Bu muhasebe prensiplerine uygundur.

### Soru 7: Ters kayıt fişinde borç/alacak nasıl ters çevriliyor?

Her satırda:
```csharp
Borc = s.Alacak,   // orijinal satırdaki alacak → ters kayıtta borç
Alacak = s.Borc,   // orijinal satırdaki borç → ters kayıtta alacak
```

Fiş başlığında:
```csharp
ToplamBorc = orijinalFis.ToplamAlacak,
ToplamAlacak = orijinalFis.ToplamBorc,
```

Örnek: Orijinal fiş `120 Borç 1180 | 600 Alacak 1000 | 391 Alacak 180` ise, ters kayıt fişi `120 Alacak 1180 | 600 Borç 1000 | 391 Borç 180` olur.

### Soru 8: TersKayitFisId / IptalEdilenFisId alanları nasıl kullanılıyor?

- **Orijinal fişte**: `TersKayitFisId = tersFis.Id` — "Bu fişi iptal eden ters kayıt fişi budur"
- **Ters kayıt fişinde**: `IptalEdilenFisId = orijinalFis.Id` — "Bu ters kayıt fişi şu fişi iptal etti"

Navigation property'ler ile çift yönlü izlenebilirlik sağlanır.

### Soru 9: KaynakModul/KaynakId değerleri ters kayıt fişinde nasıl set ediliyor?

```csharp
KaynakModul = orijinalFis.KaynakModul,   // "SatisBelgesi"
KaynakId = orijinalFis.KaynakId,         // satış belgesi ID'si
```

**Aynen korunur.** Bu, ters kayıt fişinin de aynı satış belgesine işaret ettiği anlamına gelir. Bu davranış **teknik olarak sorunlu olabilir**: Aynı `(KaynakModul, KaynakId)` kombinasyonu ile birden fazla aktif fiş oluşur (orijinal fiş Iptal olsa da IsDeleted=false'tır).

### Soru 10: Fiş iptal edilince kaynak belgeye geri bildirim yapılıyor mu?

**Hayır.** `MuhasebeFisService.IptalEtAsync` sadece muhasebe fişi agrega'sı içinde çalışır. `SatisBelgesi.MuhasebeFisId` veya `SatisBelgesi.Durum` üzerinde herhangi bir değişiklik yapmaz. Satış belgesi servisi ile muhasebe fişi servisi arasında iptal bildirimi mekanizması yoktur.

Bu, şu anlama gelir: Fiş iptal edilse bile satış belgesinin `MuhasebeFisId` alanı dolu kalmaya devam eder ve Faz 66 koruması nedeniyle belge iptal edilemez.

### Soru 11: Şu anda SatisBelgesi.MuhasebeFisId doluysa belge iptali engelleniyor; bunu kaldırmak doğru olur mu?

**Hayır, kaldırılmamalı.** Ancak koruma mantığı geliştirilmelidir:

- Mevcut durum: `MuhasebeFisId.HasValue` → her zaman engelle
- Gelişmiş durum: Bağlı fişin durumuna göre karar verilmeli
  - Fiş `Iptal` veya `TersKayit` ise → belge iptaline izin verilebilir
  - Fiş `Onayli` ise → engellenmeli (önce ters kayıt şart)
  - Fiş `Taslak` ise → engellenmeli (önce fiş silinmeli)

### Soru 12: Satış belgesi iptal edilirken bağlı fiş Taslak ise ne yapılmalı?

**Öneri:** Satış belgesinden otomatik iptal yapılmamalı. Kullanıcı önce fiş ekranına yönlendirilmeli ve taslak fişi silmeli (`DeleteAsync`). Fiş silindikten sonra satış belgesinin `MuhasebeFisId` alanı temizlenmeli (veya manuel olarak null yapılmalı), ardından belge iptal edilebilir.

Alternatif: Satış belgesi iptal akışı içinde, eğer bağlı fiş `Taslak` ise otomatik olarak fişi silip `MuhasebeFisId`'yi null yapıp belgeyi iptal edebiliriz. Bu daha kullanıcı dostudur ancak cross-aggregate transaction gerektirir.

### Soru 13: Satış belgesi iptal edilirken bağlı fiş Onayli ise ne yapılmalı?

**Öneri:** Doğrudan iptal engellenmeli. Ters kayıt oluşturulması zorunludur:

1. Kullanıcı satış belgesinde "Bağlı Fişi İptal Et" aksiyonunu başlatır
2. Sistem `MuhasebeFisService.IptalEtAsync` çağrısı yapar (bu otomatik ters kayıt oluşturur)
3. İşlem başarılı olursa:
   - Fiş `Durum = Iptal`, `TersKayitFisId` dolu
   - Ters kayıt fişi `Durum = TersKayit`, `IptalEdilenFisId` dolu
4. Ardından satış belgesi iptal edilebilir hale gelir
5. Satış belgesi `Durum = IptalEdildi`

### Soru 14: Satış belgesi iptal edilirken bağlı fiş Iptal ise ne yapılmalı?

**Öneri:** Bağlı fiş zaten iptal edilmişse, satış belgesinin iptaline izin verilebilir. Bu durumda:

- `MuhasebeFisId` dolu olmasına rağmen bağlı fiş `Iptal` durumunda
- Muhasebe etkisi ters kayıt ile sıfırlanmıştır
- Belge iptal edilebilir

Ancak bu karar dikkatle değerlendirilmelidir:
- Fiş `Iptal` olsa da `IsDeleted = false`'tır, yevmiye kaydı durur
- Ters kayıt fişi muhasebe etkisini sıfırlamıştır
- Bu nedenle satış belgesinin iptali muhasebe bütünlüğünü bozmaz

### Soru 15: Bağlı fiş bulunamaz ama MuhasebeFisId doluysa ne yapılmalı?

**Veri tutarsızlığı durumu.** Bu senaryoda:

1. `MuhasebeFisId` değeriyle `MuhasebeFisler` tablosunda kayıt bulunamaz (silinmiş veya ID yanlış)
2. Bu bir veri bütünlüğü sorunudur
3. **Öneri:** Hata loglanmalı, kullanıcıya "Bağlı muhasebe fişi bulunamadı. Sistem yöneticinize başvurun." mesajı gösterilmeli
4. Manuel müdahale veya bakım script'i ile `MuhasebeFisId` temizlenmeli
5. Bu senaryo için otomatik düzeltme yapılmamalıdır (güvenli tarafta kalınmalı)

### Soru 16: Satış belgesi e-Fatura/e-Arşiv sürecine geçmişse iptal daha farklı mı olmalı?

**Evet, farklı olmalıdır.** Ancak bu fazın kapsamı dışındadır:

- `FaturaKesildi` ve `MusteriyeGonderildi` durumları mevcut `IptalEtAsync`'te zaten engellenmiştir
- e-Fatura/e-Arşiv iptali GİB tarafına fatura iptal bildirimi gerektirir
- Bu süreç muhasebe fişi iptalinden bağımsız olarak ele alınmalıdır
- e-Fatura iptal süreci ayrı bir fazda değerlendirilmelidir

### Soru 17: Proforma / FaturaTaslagi / SatisFaturasi / IadeFaturasi için iptal davranışı farklı mı olmalı?

[`SatisBelgesiTipi`](backend/Muhasebe/SatisBelgeleri/Enums/SatisBelgesiTipi.cs:3-9) enum'undaki tipler:

| Belge Tipi | Mevcut Fiş Oluşturma | İptal Davranışı Önerisi |
|-----------|---------------------|------------------------|
| **FaturaTaslagi** | ✅ Desteklenir | Standart ters kayıt süreci |
| **SatisFaturasi** | ✅ Desteklenir | e-Fatura iptali ile birlikte düşünülmeli |
| **IadeFaturasi** | ❌ Hata döner | Fiş oluşmadığı için bu senaryo oluşmaz |
| **Proforma** | ❌ Hata döner | Fiş oluşmadığı için bu senaryo oluşmaz |

Mevcut `SatisBelgesiMuhasebeFisService` Proforma ve IadeFaturasi için zaten hata döndüğünden, bu tipler için `MuhasebeFisId` dolu olma senaryosu oluşmaz.

### Soru 18: Tesis/dönem kapalıysa ters kayıt hangi döneme atılmalı?

Mevcut `IptalEtAsync` kodu:
```csharp
await ValidateOpenPeriodAsync(orijinalFis.TesisId, orijinalFis.FisTarihi, ...);
```

**Açık dönem kontrolü yapar.** Dönem kapalıysa iptal işlemi hata verir. Bu davranış muhasebe prensiplerine uygundur:

- Ters kayıt, orijinal fişle **aynı döneme** atılır (`MaliYil`, `Donem`, `FisTarihi` aynen korunur)
- Dönem kapalıysa iptal yapılamaz → önce dönem açılmalıdır
- Bu, muhasebe denetim izi açısından doğru yaklaşımdır

### Soru 19: Ters kayıt belge tarihi mi, iptal tarihi mi kullanmalı?

Mevcut kod:
```csharp
FisTarihi = orijinalFis.FisTarihi,  // orijinal fiş tarihi korunur
```

**Orijinal belge tarihi korunur.** Bu muhasebe prensiplerine uygundur:
- Ters kayıt, orijinal işlemin yapıldığı dönemi düzeltir
- İptal tarihi kullanılırsa, kapalı dönemde işlem yapılamaz sorunu da olmazdı ancak bu muhasebe denetimini zorlaştırırdı
- Mevcut davranış korunmalıdır

### Soru 20: Kullanıcı deneyimi açısından satış belgesinden mi fiş iptali başlatılmalı, yoksa önce fiş ekranından mı iptal/ters kayıt yapılmalı?

İlk aşama için **önce fiş ekranından** yaklaşımı önerilir. Gerekçeler:
- Muhasebe kontrolü fiş ekranında kalır
- Mevcut `MuhasebeFisService.IptalEtAsync` yetenekleri doğrudan kullanılır
- Satış belgesi servisi muhasebe fiş mantığına karışmaz
- Geliştirme maliyeti daha düşüktür

Daha sonraki fazda satış belgesinden "Bağlı Fişi İptal Et" aksiyonu eklenebilir.

---

## 4. Alternatif A/B/C Değerlendirmesi

### Alternatif A — Önce Muhasebe Fişi Ekranından İptal/Ters Kayıt

**Akış:**
1. Kullanıcı satış belgesinde "Fişe Git" butonuyla fiş ekranına gider
2. Muhasebe fişi ekranında fişin durumuna göre:
   - Taslak ise → siler
   - Onaylı ise → iptal eder (otomatik ters kayıt oluşur)
3. Fiş işlemi tamamlandıktan sonra, satış belgesindeki `MuhasebeFisId` referansı temizlenmeli veya belge iptal edilebilir hale gelmeli

**Artıları:**
- ✅ Mevcut `MuhasebeFisService` yetenekleri tam olarak kullanılır
- ✅ Muhasebe kontrolü tek yerde (fiş ekranı) kalır
- ✅ Satış belgesi servisi muhasebe fiş mantığından izole kalır
- ✅ Geliştirme maliyeti en düşük alternatiftir
- ✅ Test kapsamı daha küçüktür

**Eksileri:**
- ❌ Kullanıcı iki ekran arasında geçiş yapar
- ❌ Fiş iptal edildikten sonra `SatisBelgesi.MuhasebeFisId` alanı güncellenmezse belge iptali hala engellenir
- ❌ `MuhasebeFisId` temizliği için ek bir mekanizma gerekir (event/handler veya manuel müdahale)

**Ek Gereksinim:** Fiş iptal edildiğinde `SatisBelgesi.MuhasebeFisId`'nin temizlenmesi için:
- Ya `MuhasebeFisService.IptalEtAsync` içinden `SatisBelgesi` güncellenmeli (cross-aggregate)
- Ya da bir event/handler mekanizması ile bildirim yapılmalı

### Alternatif B — Satış Belgesinden "Bağlı Fişi İptal Et/Ters Kayıt Oluştur"

**Akış:**
1. Kullanıcı satış belgesinde "Bağlı Fişi İptal Et" butonuna tıklar
2. Sistem bağlı fişin durumuna göre:
   - Taslak → fişi siler, `MuhasebeFisId`'yi null yapar
   - Onaylı → `MuhasebeFisService.IptalEtAsync` çağrısı yapar
3. Ardından satış belgesi iptal edilebilir hale gelir

**Artıları:**
- ✅ Kullanıcı açısından tek ekrandan yönetim
- ✅ Satış belgesi iptal akışı daha akıcı olur
- ✅ `MuhasebeFisId` yönetimi tek transaction'da halledilir

**Eksileri:**
- ❌ Satış belgesi servisi muhasebe fiş iptal mantığını çağırmaya başlar
- ❌ Cross-aggregate transaction karmaşıklığı
- ❌ Tesis/dönem kuralları her iki serviste de kontrol edilmeli
- ❌ Hatalı kullanımda muhasebe bütünlüğü bozulabilir
- ❌ Geliştirme ve test maliyeti daha yüksek

### Alternatif C — Kontrollü İki Aşamalı Akış

**Akış:**
1. Satış belgesi üzerinde "İptal Talebi Oluştur" yapılır
2. Belge `IptalTalebiOlusturuldu` gibi bir ara duruma geçer
3. Muhasebe birimi bağlı fişi inceler ve işlem yapar
4. Fiş süreci tamamlanınca satış belgesi iptal edilir

**Artıları:**
- ✅ En güvenli muhasebe süreci
- ✅ Operasyon ve muhasebe sorumlulukları ayrılır
- ✅ Tam denetim izi
- ✅ İleride e-Fatura/e-Arşiv iptal süreçleriyle uyumlu

**Eksileri:**
- ❌ Daha fazla ekran ve durum yönetimi gerektirir
- ❌ Geliştirme süreci en uzun olanıdır
- ❌ `IptalTalebiOlusturuldu` gibi yeni bir durum enum'a eklenmeli (model değişikliği)
- ❌ Kullanıcı deneyimi daha karmaşık

### Önerilen Yaklaşım

**Kısa vadede (Faz 68): Alternatif A** — Önce fiş ekranından iptal, ardından belge iptali.

Bunun için yapılması gerekenler:
1. `MuhasebeFisService.IptalEtAsync` sonrası `SatisBelgesi.MuhasebeFisId` temizliği (event/handler veya callback)
2. `SatisBelgesiService.IptalEtAsync` içinde `MuhasebeFisId` kontrolünün geliştirilmesi (bağlı fiş `Iptal` ise izin ver)
3. `SatisBelgesiService.ThrowIfMuhasebeFisiOlusmus` helper'ının durum-bazlı hale getirilmesi

**Orta vadede (Faz 69+): Alternatif B** — Satış belgesinden tek tıkla iptal zinciri.

---

## 5. Taslak Fiş Senaryosu

**Durum:** Satış belgesine bağlı muhasebe fişi `Taslak` durumunda.

Bu senaryo normal şartlarda oluşmamalıdır, çünkü `SatisBelgesiMuhasebeFisService.MuhasebeFisiOlusturAsync` fişi `Taslak` olarak oluşturur ve aynı transaction'da `SatisBelgesi.MuhasebeFisId`'yi set eder. Yani fiş `Taslak` iken belgede `MuhasebeFisId` doludur.

Ancak fiş onaylanmadan önce belgenin iptal edilmesi gerekebilir.

**Öneri:**
- Satış belgesinden otomatik iptal yapılmasın
- Kullanıcı "Fişe Git" ile fiş ekranına yönlendirilsin
- Fiş ekranında taslak fiş silinsin
- Fiş silindikten sonra `MuhasebeFisId` temizlensin
- Ardından belge iptal edilebilsin

**Alternatif (daha kullanıcı dostu):**
- Satış belgesi iptal akışı içinde, bağlı fiş `Taslak` ise otomatik silinsin
- Bu, `SatisBelgesiMuhasebeFisService` (veya yeni bir servis) içinde cross-aggregate transaction ile yapılabilir

---

## 6. Onaylı Fiş Senaryosu

**Durum:** Satış belgesine bağlı muhasebe fişi `Onayli` durumunda.

Bu, en kritik senaryodur. Fiş onaylanmış, yevmiye no almış ve hesap bakiyelerine işlenmiştir.

**Öneri:**
1. Doğrudan iptal **kesinlikle engellenmeli**
2. Ters kayıt oluşturulması **zorunlu**
3. Ters kayıt işlemi `MuhasebeFisService.IptalEtAsync` üzerinden yapılmalı
4. İşlem başarılı olduktan sonra:
   - Orijinal fiş `Iptal`, ters kayıt fişi `TersKayit` durumuna geçer
   - `SatisBelgesi.MuhasebeFisId` temizlenmeli veya belge iptaline izin verilmeli

**Akış:**
```
Satış Belgesi (MuhasebeFisId dolu, Durum herhangi)
    │
    ├─ Kullanıcı belgeyi iptal etmek ister
    │
    ├─ Sistem kontrol eder: MuhasebeFisId dolu
    │
    ├─ Bağlı fiş Onayli ise:
    │   ├─ Hata: "Önce bağlı muhasebe fişi için iptal/ters kayıt süreci işletilmelidir."
    │   └─ Kullanıcıyı fiş ekranına yönlendir
    │
    ├─ Fiş ekranında IptalEtAsync çağrılır
    │   ├─ Ters kayıt fişi oluşturulur
    │   ├─ Orijinal fiş Iptal olur
    │   └─ SatisBelgesi.MuhasebeFisId temizlenir (yeni özellik)
    │
    └─ Kullanıcı satış belgesi ekranına döner, belgeyi iptal eder
```

---

## 7. İptal Edilmiş Fiş Senaryosu

**Durum:** Satış belgesine bağlı muhasebe fişi `Iptal` durumunda.

Bu senaryoda fiş zaten iptal edilmiş ve ters kaydı oluşturulmuştur. Muhasebe etkisi sıfırlanmıştır.

**Öneri:**
- Bağlı fiş `Iptal` ise satış belgesinin iptaline **izin verilebilir**
- `ThrowIfMuhasebeFisiOlusmus` helper'ı geliştirilmeli:
  - `MuhasebeFisId.HasValue && bağlıFiş.Durum != Iptal` → engelle
  - `MuhasebeFisId.HasValue && bağlıFiş.Durum == Iptal` → izin ver

**Dikkat edilmesi gerekenler:**
- Fiş `Iptal` olsa da `IsDeleted = false`'tır
- Ters kayıt fişi `TersKayit` durumundadır ve `IptalEdilenFisId` ile orijinal fişe bağlıdır
- Yevmiye defterinde hem orijinal fiş hem ters kayıt fişi görünür, net etki sıfırdır
- Satış belgesinin iptal edilmesi muhasebe kayıtlarını etkilemez

---

## 8. Veri Tutarsızlığı Senaryosu

**Durum:** `SatisBelgesi.MuhasebeFisId` dolu ancak `MuhasebeFisler` tablosunda karşılığı yok.

Bu senaryo şu durumlarda oluşabilir:
- Fiş doğrudan veritabanından silinmiş (hard delete)
- Fiş `IsDeleted = true` yapılmış ancak `MuhasebeFisId` temizlenmemiş
- Veri migrasyonu sırasında tutarsızlık oluşmuş

**Öneri:**
1. `ThrowIfMuhasebeFisiOlusmus` helper'ı geliştirilmeli: Önce bağlı fişin varlığı kontrol edilmeli
2. Fiş bulunamazsa:
   - **Otomatik düzeltme yapılmamalı** (güvenli tarafta kal)
   - Hata loglanmalı: `Logger.LogWarning("SatisBelgesi {Id} için MuhasebeFisId={FisId} dolu ancak fiş bulunamadı", belge.Id, belge.MuhasebeFisId)`
   - Kullanıcıya anlamlı hata mesajı: "Bağlı muhasebe fişi bulunamadı. Sistem yöneticinize başvurun."
3. Manuel müdahale için bakım script'i hazırlanabilir

---

## 9. e-Fatura/e-Arşiv Etkisi

**Mevcut Durum:**
- `SatisBelgesiDurumu.FaturaKesildi` ve `SatisBelgesiDurumu.MusteriyeGonderildi` durumları mevcut
- Bu durumlardaki belgelerin iptali `IptalEtAsync`'te zaten engellenmiş
- e-Fatura/e-Arşiv entegrasyonu henüz yapılmamış

**İleriye dönük değerlendirme:**
- e-Fatura iptali, GİB tarafına iptal bildirimi gerektirir
- Bu, muhasebe fişi iptalinden bağımsız bir süreçtir
- e-Fatura iptal edildikten sonra belge `IptalEdildi` durumuna geçebilir
- Muhasebe fişi iptali, e-fatura iptalinden önce veya sonra yapılabilir
- **Bu fazın kapsamı dışındadır**

---

## 10. Önerilen Yol Haritası

### Faz 68 — Bağlı Fiş Durumuna Göre Akıllı Koruma + MuhasebeFisId Temizliği

**Kapsam:**
1. `ThrowIfMuhasebeFisiOlusmus` helper'ını durum-bazlı hale getir:
   - Bağlı fişi yükle, durumuna bak
   - `Iptal` veya `TersKayit` → izin ver
   - `Onayli` → engelle (mevcut mesaj)
   - `Taslak` → engelle (yeni mesaj: "Bağlı muhasebe fişi taslak durumunda. Önce fişi silin veya onaylayın.")
   - Fiş bulunamaz → veri tutarsızlığı hatası
2. `MuhasebeFisService.IptalEtAsync` sonrası `SatisBelgesi.MuhasebeFisId` temizliği:
   - Event/handler veya doğrudan DbContext üzerinden
3. `MuhasebeFisService.DeleteAsync` sonrası `SatisBelgesi.MuhasebeFisId` temizliği (taslak fiş silinirse)

### Faz 69 — Satış Belgesinden Entegre İptal Zinciri

**Kapsam:**
1. Satış belgesi ekranında "Bağlı Fişi İptal Et" butonu
2. Tek transaction'da: fiş iptali → belge iptali
3. Cross-aggregate transaction yönetimi
4. Kullanıcıya süreç özeti dialog'u

### Faz 70+ — e-Fatura/e-Arşiv Entegrasyonu

**Kapsam:**
1. e-Fatura iptal bildirimi
2. e-Arşiv iptal süreci
3. GİB entegrasyonu

---

## 11. Faz 68 İçin Net Kararlar

| # | Karar | Gerekçe |
|---|-------|---------|
| 1 | `ThrowIfMuhasebeFisiOlusmus` durum-bazlı yapılacak | Sadece `HasValue` kontrolü yetersiz |
| 2 | Bağlı fiş `Iptal`/`TersKayit` ise belge iptaline izin verilecek | Muhasebe etkisi sıfırlanmış |
| 3 | Bağlı fiş `Taslak` ise belge iptali engellenecek | Önce fiş silinmeli |
| 4 | Bağlı fiş `Onayli` ise belge iptali engellenecek | Önce ters kayıt zorunlu |
| 5 | Fiş iptal/silme sonrası `MuhasebeFisId` temizlenecek | Belge iptal edilebilir hale gelmeli |
| 6 | Cross-aggregate transaction `SatisBelgesiMuhasebeFisService` benzeri yapıda olacak | Tutarlılık için |
| 7 | Otomatik ters kayıt yapılmayacak (manuel başlatılacak) | Güvenli tarafta kal |
| 8 | Frontend'de "Fişe Git" butonu mevcut, yeni buton eklenmeyecek | Kullanıcı fiş ekranına yönlendirilir |
| 9 | e-Fatura/e-Arşiv bu fazda ele alınmayacak | Kapsam dışı |
| 10 | Veri tutarsızlığı durumunda otomatik düzeltme yapılmayacak | Manuel müdahale gerekli |

---

## 12. Kaynak Dosya Referansları

| Dosya | Açıklama |
|-------|----------|
| [`MuhasebeFis.cs`](backend/Muhasebe/MuhasebeFisleri/Entities/MuhasebeFis.cs) | Fiş entity (TersKayitFisId, IptalEdilenFisId) |
| [`MuhasebeFisSatir.cs`](backend/Muhasebe/MuhasebeFisleri/Entities/MuhasebeFisSatir.cs) | Fiş satır entity (Borc/Alacak) |
| [`MuhasebeFisService.cs`](backend/Muhasebe/MuhasebeFisleri/Services/MuhasebeFisService.cs) | IptalEtAsync, OnaylaAsync, DeleteAsync |
| [`MuhasebeFisController.cs`](backend/Muhasebe/MuhasebeFisleri/Controllers/MuhasebeFisController.cs) | Iptal endpoint'i |
| [`MuhasebeFisDtos.cs`](backend/Muhasebe/MuhasebeFisleri/Dtos/MuhasebeFisDtos.cs) | DTO'lar |
| [`MuhasebeFisDurumlari.cs`](backend/Muhasebe/Common/Constants/MuhasebeFisDurumlari.cs) | Durum sabitleri |
| [`MuhasebeFisTipleri.cs`](backend/Muhasebe/Common/Constants/MuhasebeFisTipleri.cs) | Fiş tipi sabitleri |
| [`MuhasebeKaynakModulleri.cs`](backend/Muhasebe/Common/Constants/MuhasebeKaynakModulleri.cs) | Kaynak modül sabitleri |
| [`SatisBelgesi.cs`](backend/Muhasebe/SatisBelgeleri/Entities/SatisBelgesi.cs) | Satış belgesi entity (MuhasebeFisId) |
| [`SatisBelgesiService.cs`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiService.cs) | ThrowIfMuhasebeFisiOlusmus, IptalEtAsync |
| [`SatisBelgesiMuhasebeFisService.cs`](backend/Muhasebe/SatisBelgeleri/Services/SatisBelgesiMuhasebeFisService.cs) | MuhasebeFisiOlusturAsync |
| [`SatisBelgesiDurumu.cs`](backend/Muhasebe/SatisBelgeleri/Enums/SatisBelgesiDurumu.cs) | Satış belgesi durum enum'u |
| [`SatisBelgesiTipi.cs`](backend/Muhasebe/SatisBelgeleri/Enums/SatisBelgesiTipi.cs) | Satış belgesi tip enum'u |
| [`satis-belgesi-muhasebe-fisi.md`](docs/satis-belgesi-muhasebe-fisi.md) | Mevcut dokümantasyon |
