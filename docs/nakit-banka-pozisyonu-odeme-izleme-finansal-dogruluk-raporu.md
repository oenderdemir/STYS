# Nakit ve Banka Pozisyonu + Ödeme İzleme — Finansal Doğruluk Düzeltmeleri Raporu

Tarih: 2026-07-26
Esas alınan commit: `204432a4c4baabed6e863d4ff56f7ba2acbe7d8e`

Bu tur, `204432a` ile eklenen **Nakit ve Banka Pozisyonu** ve **Ödeme İzleme** ekranlarındaki
finansal doğruluk, veri kalitesi ve eşleştirme güvenilirliği sorunlarını düzeltir. Hiçbir veri
modeli değişikliği yapılmamıştır (migration gerekmedi); düzeltmeler sorgu, sınıflandırma ve
raporlama katmanındadır. Her iki ekran da **salt-okunur** kalmıştır.

---

## 1. Analiz — veri modelinin gerçekte sağladıkları

Kod okunarak (varsayım yapılmadan) doğrulanan tarihçe alanları:

| Bilgi | Gerçek alan | Durum |
|---|---|---|
| Kaydın sisteme oluşturulma zamanı | `BaseEntity.CreatedAt` | ✅ var |
| Aktarım zamanı | `PosTahsilatValor.AktarimTarihi` | ✅ var (önceki kod **kullanmıyordu**) |
| Soft-delete zamanı | `BaseEntity.DeletedAt` | ✅ var |
| Muhasebe fişi tarihi/oluşturulması | `MuhasebeFis.FisTarihi` / `CreatedAt` | ✅ var |
| **İptal zamanı** | — | ❌ **yok** |
| **Ters kayıt zamanı** | — | ❌ **yok** |
| **Durum geçiş tarihçesi** | — | ❌ **yok** |

`PosTahsilatValorDegisiklikGecmisi` tablosu **yalnızca manuel komisyon/net/hesap düzenlemelerinin**
audit izini tutar (`IslemTipi` = `ManuelKomisyonDuzenleme` vb.); durum geçişlerini kaydetmez.
Dolayısıyla genel bir durum tarihçesi kaynağı **yoktur**.

### Doğrulanan gerçek hatalar (düzeltilmeden önceki davranış)

1. `UpdatedAt` alanı iptal tarihi gibi kullanılıyordu (genel audit alanı — iptal dışı her
   güncellemede de değişir).
2. Kaydın rapor tarihinde var olup olmadığı `CreatedAt` yerine `OdemeTarihi` ile belirleniyordu —
   sonradan geriye dönük girilen bir ödeme geçmiş rapora sızabiliyordu.
3. `AktarimTarihi` hiç kullanılmıyordu.
4. `Aktariliyor` ve `TersKayitOlusturuluyor` durumları son `else` dalına düşerek **normal bekleyen**
   sayılıyordu; projeye eklenecek yeni/tanınmayan bir durum da aynı şekilde sessizce toplama
   girecekti.
5. `MuhasebeHesapPlani.AktifMi` hiç kontrol edilmiyordu (yalnızca `IsDeleted`).
6. Muhasebe hesabı bağlantısı olmayan bir banka hesabı için `TahminiBakiye = 0 + POS` şeklinde
   gerçekte var olmayan bir "bakiye" üretiliyordu.
7. Mükerrerlik kontrolü yalnızca `MuhasebeHesapPlaniId` ile gruplanıyordu; farklı tesislerin aynı
   global hesap planını kullanması **sahte mükerrerlik uyarısı** üretiyordu.
8. Ödeme İzleme'de `BelgeNo.Contains(...)` eşleşmesi **Kesin** eşleşme üretiyordu; kısa bir metin
   çok sayıda belgeyi kesin eşleşmeye çeviriyordu. Minimum uzunluk doğrulaması yoktu.
9. Toleranslı tarih eşleşmesi kullanıcıya "birebir" olarak açıklanıyordu.
10. `BakiyeyeDahilMi` yalnızca `belge.Durum == Aktif` kontrolüne dayanıyordu; fişi ve cari hareketi
    olmayan bir ödeme "bakiyeye dahil" görünüyordu.
11. Cari dökümde `ValorBekliyor` + `MutabakatBekliyor` + `Hata` tek toplamda birleşiyordu; para
    birimi ayrımı yoktu.

---

## 2. Geçmiş tarihli POS pozisyonu — **DESTEKLENMİYOR** (bilinçli karar)

İptal zamanı ve durum geçiş tarihçesi veri modelinde bulunmadığından, bir kaydın geçmiş bir
tarihteki gerçek durumu (bekliyor / mutabakat / hata / iptal) **deterministik olarak
kurulamaz**. Veri modelinin sağlayamadığı bir tarihsel doğruluk tahmin edilerek üretilmemiştir.

Uygulanan davranış:

- Geçmiş rapor tarihinde **POS/valör pozisyonu hiç hesaplanmaz**; POS valör kayıtları sorgulanmaz
  bile.
- Tüm POS tutarları (bekleyen, mutabakat, hatalı, tarih grupları) finansal toplamların
  **tamamından** çıkarılır.
- `PosPozisyonuHesaplandiMi = false` ve kullanıcıya gösterilebilir bir
  `PosPozisyonuHesaplanmamaNedeni` döner; ayrıca `GecmisTarihPosPozisyonuHesaplanmadi` uyarısı
  üretilir.
- Frontend bu durumda POS'a bağlı 6 özet kartını **gizler**; kalan kartın başlığı "Toplam Banka
  Muhasebe Pozisyonu (POS hariç)" olur.
- Gelecek tarihli rapor isteği backend'de `400` ile reddedilir; frontend'de tarih seçici
  `maxDate = bugün` ile sınırlıdır.

**Muhasebe bakiyesi bundan bağımsızdır**: fiş satırları gerçek `FisTarihi` taşıdığı için geçmiş
tarihli muhasebe bakiyesi hesaplanmaya devam eder ve `TahminiBakiye` bu durumda yalnızca muhasebe
bakiyesine eşittir. Muhasebe tarafının geçmiş tarihi desteklemesi ile POS tarafının desteklememesi
birbirine karıştırılmamıştır.

---

## 3. Durum allowlist'i — yeni, saf ve test edilebilir bileşen

Sınıflandırma mantığı büyük servisten çıkarılıp DB'siz, yan etkisiz, doğrudan birim testi
yazılabilen bir bileşene taşındı:

`backend/Muhasebe/NakitBankaPozisyonu/Services/PosValorFinansalSiniflandirici.cs`

**Tasarım kuralı — ALLOWLIST:** yalnızca açıkça "normal bekleyen" sayılan tek durum ve tüm veri
kalitesi kontrollerinden geçen kayıtlar finansal toplama girer. Bunun dışındaki **her şey** güvenli
varsayılan olarak toplamın dışında tutulur. Projeye yeni bir `Durum` sabiti eklendiğinde bu kod
güncellenmese bile tutar sessizce bekleyen toplamına **sızmaz**.

| Durum | Kategori | Tahmini bakiyeye |
|---|---|---|
| `ValorBekliyor` | `NormalBekleyen` | ✅ dahil (veri kalitesi kapısından geçerse) |
| `Aktarildi` | `Aktarilmis` | ❌ (muhasebe bakiyesi zaten içerir) |
| `MutabakatBekliyor` | `MutabakatBekliyor` | ❌ ayrı gösterilir |
| `Hata` | `Hatali` | ❌ ayrı gösterilir |
| `Iptal` | `IptalEdilmis` | ❌ |
| `Aktariliyor` | `AktarimSurecinde` | ❌ ayrı gösterilir |
| `TersKayitOlusturuluyor` | `TersKayitSurecinde` | ❌ ayrı gösterilir |
| `AktarimFisiIptalEdildi` | `TersKayitSurecinde` | ❌ ayrı gösterilir |
| **tanınmayan değer** | `TaninmayanDurum` | ❌ + veri kalitesi uyarısı |

### Veri kalitesi kapısı (yalnızca `ValorBekliyor` adayları için)

Aşağıdakilerden **herhangi biri** başarısızsa kayıt hiçbir toplama girmez, yalnızca uyarı üretir:

- Bağlı banka hesabı bulunamıyor / pasif / silinmiş
- Banka hesabının geçerli (mevcut + aktif + silinmemiş) muhasebe hesabı bağlantısı yok
- `ValorBekliyor` olduğu hâlde `MuhasebeFisId` veya `TersKayitMuhasebeFisId` taşıyor
- Beklenen valör tarihi boş/varsayılan
- Para birimi tanımsız veya banka hesabınınkiyle uyuşmuyor
- `NetTutar ≠ BrutTutar − KomisyonTutari` (±0.01 yuvarlama toleransı)
- `NetTutar ≤ 0`

Ayrıca durum/fiş ilişkisi tutarsızlıkları ayrı uyarı olarak üretilir: `Aktarildi` olup `MuhasebeFisId`
yok; `AktarimFisiIptalEdildi` olup `TersKayitMuhasebeFisId` yok.

---

## 4. Bağlantı geçerliliği, pasif hesap ve sahte bakiye

- `MuhasebeHesapPlani.AktifMi` artık normal hesaplamada dikkate alınıyor; pasif hesap için ayrı
  `PasifBaglantiliMuhasebeHesabi` uyarısı üretiliyor.
- Bakiye hesabı yalnızca **geçerli** (`!IsDeleted && AktifMi`) hesap planları için yapılıyor.
- Geçersiz bağlantıda `MuhasebeBakiyesiGecerliMi = false` ve `TahminiBakiye = null` dönüyor —
  yalnızca POS tutarından oluşan **sahte bir bakiye üretilmiyor**. Frontend bu hücrelerde `—` ve
  "Hesaplanamıyor" gösteriyor.
- Genel özette geçersiz bağlantılı hesaplar muhasebe bakiyesi toplamına katılmıyor.

**Bilinen sınırlama:** bir muhasebe hesabının *geçmişteki* aktiflik durumu veri modelinde
tutulmadığı için bugünkü `AktifMi` değeri kullanılır; tarihsel aktiflik uydurulmaz.

---

## 5. Tesis bazlı kapsam ve mükerrerlik yönü

- Bakiye, son hareket tarihi ve hesap eşleştirmeleri `(TesisId, MuhasebeHesapPlaniId)` **bileşik
  anahtarıyla** yapılıyor.
- Mükerrerlik kontrolü de aynı bileşik anahtara taşındı: farklı tesislerin aynı **global** hesap
  planını kullanması artık uyarı üretmiyor; gerçek mükerrerlik ancak **aynı tesiste** aynı hesap
  planına birden fazla aktif banka hesabı bağlıysa raporlanıyor.
- İki yön açıkça ayrıldı ve karıştırılmadı:
  - `AyniMuhasebeHesabinaBirdenFazlaAktifBankaHesabiBagli` — gerçek ve kontrol edilen yön.
  - `AyniBankaHesabiBirdenFazlaMuhasebeHesabinaBagli` — `KasaBankaHesap.MuhasebeHesapPlaniId`
    tekil bir FK olduğu için **şemayla yapısal olarak imkânsız**; sabit yalnızca iki yönün
    karıştırılmaması için tanımlı.
- Yetkisiz tesis verisi hiçbir özet/uyarı/toplam/detaya girmiyor; kapsam backend sorgusunun
  kendisinde uygulanıyor.

---

## 6. Para birimi güvenliği

- Uyarılı tutarlar `(UyariTipi, ParaBirimi)` kırılımında toplanıyor; farklı para birimleri hiçbir
  yerde birleştirilmiyor.
- `VeriKalitesiUyariDto` artık `ParaBirimi` taşıyor.
- Genel özet kartları yalnızca raporlama para birimini (TRY) yansıtıyor; diğer para birimleri
  `ParaBirimiOzetleri` altında ayrı.
- Cari hareket dökümü tamamen para birimi bazına taşındı (aşağıda).
- Kur dönüşüm altyapısı olmadığından varsayımsal TL karşılığı **üretilmiyor**.

---

## 7. Ödeme İzleme — eşleşme güveni

**Kesin eşleşme** artık yalnızca benzersiz referansın **normalize edilmiş tam eşitliği** ile
üretiliyor:

- `Contains` / kısmi metin eşleşmesi **kaldırıldı**.
- Normalizasyon yalnızca ayırıcı karakterleri temizler ve büyük harfe çevirir; harf/rakam korunur,
  farklı gerçek numaralar aynı değere dönüşmez.
- Minimum referans uzunluğu (4 karakter) backend'de doğrulanıyor; kısa referansla geniş/hassas
  arama `400` ile engelleniyor.
- Toleranslı tarih artık "birebir" olarak raporlanmıyor: `TarihBirebirMi` + `TarihFarkiGun`
  dönüyor, gerekçe metni farkı açıkça yazıyor.
- Her sonuçta `EslesenAlanlar` ve `UyusmayanAlanlar` dönüyor; beyan edilen yöntem/hesap kayıtla
  çelişiyorsa sonuç en düşük güven seviyesinde kalıyor.

| Seviye | Koşul |
|---|---|
| `Kesin` | Benzersiz referans (belge/dekont no) birebir eşleşiyor |
| `YuksekOlasilik` | Tutar + para birimi + dar tarih aralığı + yöntem/hesap uyuşuyor, çelişki yok |
| `IncelenmesiGereken` | Yalnızca zayıf sinyaller (tutar + tarih) veya yöntem/hesap çelişkisi |
| `EslesmeYok` | Yeterli kanıt yok |

Yalnızca aynı tutara dayanarak iki kayıt aynı ödeme kabul edilmiyor.

---

## 8. Ödeme İzleme — "Bakiyeye dahil mi?" gerçek mali etkiye göre

Tek boolean yetersiz olduğundan response genişletildi:

- `BakiyeyeDahilMi`
- `BakiyeyeDahilEdilmeDurumu` — `TamamenDahil` | `KismenDahil` | `DahilDegil`
- `BakiyeyeDahilEdilmemeNedenKodlari`
- `BakiyeyeDahilEdilmemeAciklamalari`
- `EtkiledigiCariVeyaBorc`, `EtkiledigiTutar`

Değerlendirilen gerçek koşullar: ödeme belgesinin durumu, kapama cari hareketinin varlığı ve
durumu, ödeme yöntemine göre zorunlu muhasebe fişinin varlığı ve durumu, kredi kartı ödemelerinde
POS valör zincirinin varlığı ve aktarım durumu.

Neden kodları: `OdemeIptalEdilmis`, `CariHareketiYok`, `CariHareketiIptalEdilmis`,
`ZorunluMuhasebeFisiYok`, `MuhasebeFisiIptalEdilmis`, `PosValorKaydiYok`,
`PosValorHenuzAktarilmamis`.

Artık aktif bir ödeme belgesinin fişi/cari hareketi yokken ekranda "bakiyeye dahil" + "fiş yok"
şeklinde çelişkili bilgi gösterilmiyor.

---

## 9. Cari hareket dökümü — durum ve para birimi ayrımı

`CariHareketDokumDto` para birimi bazına taşındı (`ParaBirimiOzetleri`). Her para birimi için ayrı:

- `ToplamBorc`, `ToplamAlacak`
- `IptalEdilmisTutar`
- `NormalAktarilmayiBekleyenPos` (yalnızca `ValorBekliyor`)
- `MutabakatBekleyenPos`
- `HataliPos`
- `AktarimSurecindekiPos`
- `AciklananKalanBakiye`

`MutabakatBekliyor` ve `Hata` artık normal bekleyen POS ile **birleştirilmiyor**. `Aktarildi` /
`Iptal` / `AktarimFisiIptalEdildi` bakiye açıklamasına ayrı kalem olarak girmiyor (aktarılmış tutar
zaten muhasebe tarafında, iptal edilmiş tutar hiçbir yerde sayılmıyor) — böylece aynı ödeme
belge/cari hareket/valör/fiş kayıtlarında bulunmasına rağmen mükerrer toplanmıyor. POS tutarları
kalan bakiyeye otomatik eklenmiyor; yalnızca farkın nereden gelebileceğini açıklamak için
gösteriliyor.

---

## 10. Değişen dosyalar

### Backend

| Dosya | Değişiklik |
|---|---|
| `Muhasebe/NakitBankaPozisyonu/Services/PosValorFinansalSiniflandirici.cs` | **yeni** — saf, test edilebilir sınıflandırma bileşeni |
| `Muhasebe/NakitBankaPozisyonu/Services/NakitBankaPozisyonuService.cs` | geçmiş tarih kararı, sınıflandırıcıya bağlanma, geçerlilik kapısı, tesis kapsamlı mükerrerlik, uyarılı tutar toplama |
| `Muhasebe/NakitBankaPozisyonu/Dtos/NakitBankaPozisyonuDtos.cs` | `PosPozisyonuHesaplandiMi`, `UyariliTutarOzetiDto`, `MuhasebeBakiyesiGecerliMi`, nullable `TahminiBakiye`, yeni uyarı sabitleri, uyarıda `ParaBirimi` |
| `Muhasebe/OdemeIzleme/Services/OdemeIzlemeService.cs` | referans normalizasyonu + tam eşitlik, min uzunluk validation, eşleşme gerekçeleri, gerçek `BakiyeyeDahilMi`, para birimi/durum bazlı cari döküm |
| `Muhasebe/OdemeIzleme/Dtos/OdemeIzlemeDtos.cs` | güven seviyesi dokümantasyonu, bakiye neden kodları/durumları, `CariBakiyeParaBirimiOzetiDto`, eşleşme açıklanabilirlik alanları |

### Frontend

| Dosya | Değişiklik |
|---|---|
| `pages/muhasebe/nakit-banka-pozisyonu/nakit-banka-pozisyonu.dto.ts` | yeni alanlar + genişletilmiş uyarı etiketleri |
| `pages/muhasebe/nakit-banka-pozisyonu/nakit-banka-pozisyonu.html` | POS kartlarının koşullu gizlenmesi, "dahil edilmeyen tutarlar" tablosu, nullable bakiye gösterimi, uyarıda para birimi |
| `pages/muhasebe/odeme-izleme/odeme-izleme.dto.ts` | bakiye durumu/neden alanları, para birimi özetleri, eşleşme açıklanabilirlik alanları |
| `pages/muhasebe/odeme-izleme/odeme-izleme.html` | bakiye durumu rozeti + gerekçe listesi, para birimi bazlı döküm, eşleşen/uyuşmayan alan gösterimi, tarih toleransı rozeti |

### Testler

| Dosya | Değişiklik |
|---|---|
| `tests/STYS.Tests/PosValorFinansalSiniflandiriciTests.cs` | **yeni** — 14 birim testi (DB'siz) |
| `tests/STYS.Tests/NakitBankaPozisyonuServiceTests.cs` | eski geçmiş-tarih testleri yeni davranışla değiştirildi; ara/tanınmayan durum, pasif hesap, tesis mükerrerlik (iki yön) testleri eklendi |
| `tests/STYS.Tests/OdemeIzlemeServiceTests.cs` | kısmi belge no, min uzunluk, tarih toleransı, cari hareketi olmayan aktif belge, POS durum ayrımı, çoklu para birimi testleri eklendi |
| `frontend/.../nakit-banka-pozisyonu.spec.ts` | geçmiş tarih, maxDate, uyarılı tutar, geçersiz bağlantı testleri eklendi |
| `frontend/.../odeme-izleme.spec.ts` | bakiye gerekçeleri, kısmi eşleşme/tolerans testleri eklendi |

**Migration:** gerekmedi. Hiçbir veri modeli değişikliği yok; yalnızca sorgu/sınıflandırma
düzeltmeleri yapıldı.

---

## 11. Doğrulama sonuçları

Tüm komutlar gerçekten çalıştırıldı; sonuçlar aşağıdadır.

| Komut | Toplam | Başarılı | Başarısız | Atlanan | Uyarı |
|---|---|---|---|---|---|
| `dotnet build STYS.sln` | — | ✅ başarılı | 0 error | — | **0 warning** |
| `dotnet test tests/STYS.Tests/STYS.Tests.csproj` | **430** | **430** | **0** | **0** | — |
| `npx ng test --watch=false --browsers=ChromeHeadless` | **33** | **33** | **0** | 0 | — |
| `npx ng build` | — | ✅ başarılı | — | — | 1 (bundle budget — **önceden var olan**, bu turla ilgisiz) |

- Backend testleri **gerçek SQL Server**'a karşı çalıştırıldı
  (`Server=localhost,14333; Database=STYSDB`). InMemory provider finansal doğruluk kanıtı olarak
  **kullanılmadı**.
- Test sonrası fiziksel kalıntı kontrolü: `NBP-970` = 0, `ODZ-970` = 0, `PVI-970` = 0.
- Cleanup altyapısı (`TwoPhaseCleanupRunner`: bağımsız adımlar, `IgnoreQueryFilters` ile fiziksel
  kalıntı doğrulaması, yutulmayan hatalar, `AggregateException`) korundu ve yeni test sınıfında da
  aynı desen kullanıldı.

---

## 12. Bilinen sınırlamalar

1. **Geçmiş tarihli POS pozisyonu üretilmiyor.** Gerçekten istenirse `PosTahsilatValor` için zaman
   damgalı bir durum geçiş tablosu (en azından iptal ve ters kayıt zamanı) gerekir — ayrı bir iş
   kalemidir.
2. **Hesapların geçmişteki aktiflik durumu bilinmiyor**; bugünkü `AktifMi` kullanılıyor, tarihsel
   aktiflik uydurulmuyor.
3. **Çapraz-kaynak orphan araması yapılmadı.** Ödeme İzleme hâlâ `TahsilatOdemeBelgeleri`
   üzerinden başlıyor; "muhasebe fişi olup kaynak ödemesi olmayan", "cari hareketi olup ödeme
   belgesi olmayan" gibi bağımsız kayıt aramaları bu turda eklenmedi.
4. **Yetki dışı tesis/kurum için "olası kayıt var" uyarısı eklenmedi.** Böyle bir uyarının kaydın
   varlığını ifşa etme riski taşıdığı ve dikkatli bir yetki tasarımı gerektirdiği için yarım
   doğrulanmış hâlde gönderilmedi.
5. **POS/banka işlem referansı ile arama yok.** `TahsilatOdemeBelgesi` üzerinde `BelgeNo` dışında
   bir banka/POS referans veya dekont numarası alanı bulunmuyor (kod okunarak doğrulandı); bu
   filtre ancak veri modeline böyle bir alan eklenirse anlamlı olur.

---

## 13. Kapsam sınırı (korundu)

Bu turda **yapılmayanlar** (bilinçli olarak):

- Banka API entegrasyonu yok.
- STYS muhasebe bakiyesi gerçek banka kullanılabilir bakiyesi olarak sunulmuyor.
- Yanlış hesaptaki ödeme otomatik taşınmıyor, otomatik mahsup yapılmıyor.
- Muhasebe fişi veya ödeme kaydı oluşturulmuyor/değiştirilmiyor.
- Valör durumları değiştirilmiyor.
- Tarihsel veri yokken yaklaşık geçmiş POS durumu uydurulmuyor.
- Varsayımsal döviz kuru kullanılmıyor.
- Yalnızca benzer tutara dayanarak başka müşterinin ödemesi gösterilmiyor.
- Mevcut muhasebe/POS iş kuralları kanıtsız yeniden tanımlanmıyor.
