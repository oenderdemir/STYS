# Rezervasyon Ödeme → Muhasebe Entegrasyonu — Bulgular ve Uygulama Notları

Bu doküman, rezervasyon ödeme ekranının muhasebe modülüne (`TahsilatOdemeBelgesi`) entegre edilmesi
sırasında yapılan analiz ve uygulama çalışmasının bulgularını özetler.

## 1. Mevcut yapıda tespit edilen gerçek boşluklar

- **`Rezervasyon` entity'sinde hiçbir cari kart bağlantısı yoktu.** Ne `CariKartId`, ne `KurumId`, ne
  de `MusteriId` alanı mevcuttu. Konaklama faturalama akışı (`RezervasyonSatisBelgesiService`) da
  rezervasyondan bir cari kart türetmiyordu — misafir bilgisi (ad/TCKN/telefon) sadece fatura taslağına
  request üzerinden aktarılıyordu, kalıcı bir cari kart eşlemesi yoktu.
- **`TahsilatOdemeBelgesi` → `MuhasebeFis` üretimi hiç yoktu.** `MuhasebeKaynakModulleri.TahsilatOdemeBelgesi`
  sabiti sadece `CariHareket.KaynakModul` için kullanılıyordu; fiş üretimi için kullanılmıyordu.
- **`TahsilatOdemeBelgesi.AddAsync` zaten istenen davranışı sağlıyordu:** `KapatilacakCariHareketId`
  `null` bırakılırsa hiçbir `CariHareket` doğmuyor — bu, "ödeme = tahsilat, gelir değil" ayrımının
  üzerine oturduğu mevcut mekanizma oldu, yeniden icat edilmedi.
- **`Rezervasyonlar.OdemeTipleri` ile `Muhasebe.OdemeYontemleri` iki ayrı sabit sınıfıydı** ve
  senkron değildi (`HavaleEft` rezervasyon tarafında yoktu). Hizalandı.

## 2. Uygulanan mimari kararlar

- Rezervasyon ödemesi kaydedilirken **sadece `TahsilatOdemeBelgesi` oluşturulur**, `MuhasebeFis`
  otomatik üretilmez (revizyon isteği #1/#2). Fiş üretimi `ITahsilatOdemeBelgesiMuhasebeFisService`
  üzerinden ayrı, manuel/isteğe bağlı bir aksiyon olarak tasarlandı; kaynak modülden bağımsızdır.
- Cari kart çözümleme sırası: **rezervasyonda önbelleklenmiş → kullanıcının seçtiği → TCKN/VKN veya
  (telefon + ad-soyad birlikte) eşleşmesi → tesisin konfigüre edilmiş varsayılan "Rezervasyon
  Misafirleri" cari kartı → hiçbiri yoksa HTTP 422 ile kullanıcıdan seçim istenir.** Otomatik cari
  kart **oluşturulmaz** (revizyon isteği #4/#5 gereği bilinçli olarak).
- Tahsilat fişinde alacak hesabı hard-code değil: `Tesis.RezervasyonTahsilatAlacakHesapTipi`
  (`Cari` | `AlinanAvans`) ile konfigüre edilebilir.
- `RezervasyonOdeme` ve `TahsilatOdemeBelgesi` aynı transaction içinde oluşur; bildirim commit'ten
  sonra gönderilir (muhasebe adımı başarısız olursa kullanıcı yanlışlıkla "ödeme alındı" bildirimi
  görmesin diye).
- Ödeme iptali fiziksel silme değildir: `RezervasyonOdeme.Durum = Iptal` + bağlı `TahsilatOdemeBelgesi`
  iptali (mevcut `TahsilatOdemeBelgesiService.IptalEtAsync` / `CariHareketKapamaService.GeriAlAsync`)
  + fiş onaylanmışsa `MuhasebeFisService.IptalEtAsync` (ters kayıt) yeniden kullanılır.

## 3. Kritik olay: `dotnet ef migrations add` tehlikeli bir migration üretti

İlk migration üretim denemesinde `dotnet ef migrations add` **102 tabloyu `DropTable`/`CreateTable`
ile yeniden oluşturan** bir migration dosyası üretti (6300+ satır). Bu, veritabanına uygulansaydı
tüm veriyi silip şemayı sıfırdan kurmaya çalışacaktı.

**Kök neden:** `dotnet ef migrations remove` komutu, snapshot dosyasını (`StysAppDbContextModelSnapshot.cs`)
önceki doğru duruma geri almak yerine **neredeyse boş bir duruma düşürdü** (9086 satırdan 21 satıra).
Bu bozuk snapshot üzerinden alınan bir sonraki `migrations add`, EF Core'un modeli "sıfırdan" olarak
yorumlamasına yol açtı.

**Alınan önlem:**
1. Migration hemen uygulanmadan fark edildi (Up() içeriği incelenerek — `CreateTable`/`DropTable`
   sayısı 0 olmalıyken 102/102 çıktı).
2. Bozulan snapshot dosyası `git checkout -- ...StysAppDbContextModelSnapshot.cs` ile geri yüklendi.
3. Migration, temiz snapshot durumundan yeniden üretildi — bu sefer sadece `AddColumn`/`CreateIndex`/
   `AddForeignKey` içeren, 314 satırlık, güvenli/ek nitelikli bir migration çıktı
   (`20260713155404_AddRezervasyonOdemeMuhasebeEntegrasyonu`).
4. Migration **veritabanına uygulanmadı** (`dotnet ef database update` çalıştırılmadı) — geri alınması
   zor bir işlem olduğu için kullanıcı onayı bekleniyor.

**Öneri:** Bu ortamda `dotnet ef migrations remove` komutuna güvenilmemeli. Bir migration'ın yanlış
üretildiği tespit edilirse, `migrations remove` çalıştırmak yerine önce `git status` ile snapshot
dosyasının durumu kontrol edilmeli; gerekirse migration `.cs`/`.Designer.cs` dosyaları elle silinip
snapshot `git checkout` ile geri yüklenmelidir.

## 4. Yetkilendirme kapsamı notu

Mevcut `GET /ui/muhasebe/kasa-banka-hesaplari/tip/{tip}` ve `GET /ui/muhasebe/cari-kartlar` uç
noktaları `KasaBankaHesapYonetimi.View` / `CariKartYonetimi.View` (muhasebe modülü) izni gerektiriyor.
Resepsiyon kullanıcılarının bu izinlere sahip olmama ihtimali yüksek olduğundan, ödeme dialogundaki
kasa/banka ve cari kart seçimleri için **rezervasyon modülü kapsamında, `RezervasyonYonetimi.Manage`
izniyle çalışan iki dar kapsamlı proxy uç noktası** eklendi:
- `GET /ui/rezervasyon/kayitlar/{id}/kasa-banka-hesap-secenekleri?odemeTipi=...`
- `GET /ui/rezervasyon/kayitlar/{id}/cari-kart-secenekleri?arama=...`

İkisi de rezervasyonun kendi `TesisId`'sine göre filtrelenir ve minimal alan döner (muhasebe hesap
planı detaylarını göstermez).

## 5. Değişen/eklenen dosyalar

Ayrıntılı liste için `changes.md` içindeki ilgili tur girdisine bakınız.

## 6. İkinci tur — kod inceleme düzeltmeleri

İlk uygulamanın kod incelemesinde 6 madde tespit edildi ve düzeltildi:

1. **Nested transaction riski:** `RezervasyonService.IptalOdemeAsync` transaction açıyor, içeride
   çağrılan `TahsilatOdemeBelgesiService.IptalEtAsync` da koşulsuz `BeginTransactionAsync` açıyordu.
   `IptalEtAsync` artık `CariHareketKapamaService.GeriAlAsync` ile aynı `ownsTransaction` desenini
   kullanıyor — ambient transaction varsa kendi transaction'ını açmıyor/kapatmıyor.
2. **Atlanan validasyonlar:** `RezervasyonOdemeMuhasebeService.TahsilatOlusturAsync`,
   `TahsilatOdemeBelgesi`'ni `TahsilatOdemeBelgesiService.AddAsync` yerine doğrudan `DbContext` ile
   eklediği için `ValidateAsync`/`EnsureOpenPeriodAsync`/`ValidateKapatilacakCariHareketAsync`
   atlanıyordu. Bu üçü `ITahsilatOdemeBelgesiService.ValidateOlusturmaAsync(...)` adıyla ortak,
   public bir metoda çıkarıldı; hem `AddAsync` hem `RezervasyonOdemeMuhasebeService` aynı metodu
   çağırıyor — tek doğrulama kaynağı korunuyor.
3. **Yetersiz duplicate koruması:** `RezervasyonOdemeler.TahsilatOdemeBelgesiId` üzerindeki unique
   index tek başına yeterli değildi (uygulama dışı/elle DB erişiminde aynı kaynaktan ikinci belge
   üretilebilirdi). `TahsilatOdemeBelgeleri` üzerinde `(KaynakModul, KaynakId)` için
   `IsDeleted = 0 AND KaynakId IS NOT NULL` filtreli unique index eklendi — iki katmanlı savunma.
4. **BelgeNo race condition:** `GenerateBelgeNoAsync` MAX+1 sorgusuna dayandığından yarış durumuna
   açıktı. `BelgeNo` üzerindeki mevcut unique index korunarak, ambient transaction içinde
   `IDbContextTransaction.CreateSavepointAsync`/`RollbackToSavepointAsync` ile 3 denemelik güvenli
   retry eklendi (`SatisBelgesiMuhasebeFisService`'teki retry desenine benzer, ancak burada iç içe
   transaction yerine savepoint kullanıldı çünkü çağrı zaten ambient transaction içinde çalışıyor).
5. **Koşulsuz cari kart hesap planı zorunluluğu:** `TahsilatOdemeBelgesiMuhasebeFisService`,
   `Tesis.RezervasyonTahsilatAlacakHesapTipi = AlinanAvans` olsa bile `CariKart.MuhasebeHesapPlaniId`
   zorunlu tutuyordu (ve `ResolveAlacakHesabiAsync` içinde null-forgiving operatörle potansiyel
   `NullReferenceException` riski vardı). Kontrol artık yalnızca `Cari` modunda, `ResolveAlacakHesabiAsync`
   içinde koşullu olarak çalışıyor.
6. **Normalize edilmemiş odemeTipi:** `GetKasaBankaHesapSecenekleriAsync`, `KaydetOdemeAsync`'in
   kullandığı `NormalizeOdemeTipi` metodunu kullanmıyordu; büyük/küçük harf farkı olan bir değer
   sessizce boş liste veya "Gecersiz odeme tipi" hatası dönebiliyordu. Artık aynı normalizasyon
   uygulanıyor.

**Migration güncellemesi:** Fix #3'teki yeni index, henüz veritabanına uygulanmamış olan
`20260713155404_AddRezervasyonOdemeMuhasebeEntegrasyonu.cs` migration dosyasına elle eklendi (aracı
tekrar riske atmamak için `dotnet ef migrations add` çalıştırılmadı — snapshot dosyası da elle
güncellendi). Doğrulama için geçici bir `dotnet ef migrations add ZZZ_CheckNoPendingChanges` denemesi
yapıldı; **boş** (0 operasyon) migration üretmesi, elle yapılan snapshot düzenlemesinin modelle tam
örtüştüğünü doğruladı. Bu geçici dosyalar `dotnet ef migrations remove` KULLANILMADAN elle silindi.

## 7. Üçüncü tur — Fix #5'in eksik kalan yarısı

İkinci tur düzeltmesinde `TahsilatOdemeBelgesiMuhasebeFisService.ResolveAlacakHesabiAsync`'teki
koşulsuz `CariKart.MuhasebeHesapPlaniId` zorunluluğu giderilmişti, ancak **fiş üretiminden önceki**
`TahsilatOdemeBelgesiService.ValidateAsync` (AddAsync/`ValidateOlusturmaAsync` üzerinden) hâlâ
koşulsuzdu — yani `AlinanAvans` modunda dahi `RezervasyonOdemeMuhasebeService.TahsilatOlusturAsync`,
cari kartın `MuhasebeHesapPlaniId`'si boşsa `TahsilatOdemeBelgesi` oluşturma aşamasında (fiş üretiminden
çok önce) hatayla karşılaşıyordu.

Çözüm: `ValidateAsync`/`ValidateOlusturmaAsync`/`ITahsilatOdemeBelgesiService.ValidateOlusturmaAsync`
imzasına `bool requireCariMuhasebeHesabi` parametresi eklendi:
- Genel `TahsilatOdemeBelgesi` ekranı (`AddAsync`, `UpdateAsync`) her zaman `true` geçiriyor — bu ekran
  her durumda cari hesap üzerinden çalışır.
- `RezervasyonOdemeMuhasebeService.TahsilatOlusturAsync`, çağrıdan önce `Tesis.RezervasyonTahsilatAlacakHesapTipi`'ni
  okuyup `requireCariMuhasebeHesabi = (tipi != AlinanAvans)` olarak hesaplıyor ve bunu geçiriyor —
  böylece `AlinanAvans` modundaki tesislerde, muhasebe hesap planı bağlantısı olmayan bir misafir cari
  kartıyla da rezervasyon tahsilatı sorunsuz oluşturulabiliyor.
