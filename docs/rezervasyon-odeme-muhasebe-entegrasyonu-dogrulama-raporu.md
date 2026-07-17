# Rezervasyon Ödeme → Muhasebe Entegrasyonu — Nihai Doğrulama Raporu

Bu doküman, rezervasyon tahsilatlarının ve check-out sonrası gelir/satış belgesi akışının
muhasebe modülüne entegrasyonu için yapılan tüm geliştirme ve doğrulama çalışmasının **nihai
kapanış raporudur**. `docs/rezervasyon-odeme-muhasebe-entegrasyonu-bulgular.md` ilk analiz/bulgu
dokümanıdır; bu doküman ise o çalışmanın üzerine eklenen cari-split, kasa/banka varsayılan seçim
ve gelir tarafı düzeltmelerini kapsayan **uçtan uca, tarayıcı tabanlı manuel doğrulamanın** kaydıdır.
Bu doküman kapsamında kod değişikliği yapılmamıştır — yalnızca daha önce commit edilmiş
değişiklikler test edilmiş ve sonuçlar belgelenmiştir.

## 1. Amaç

Rezervasyon modülünde alınan ödemelerin (tahsilat) ve check-out sonrası oluşan gelirin (satış
belgesi) muhasebe modülüne doğru, tutarlı ve **cari bazında ayrıştırılabilir** şekilde
aktarıldığını uçtan uca doğrulamak. Özellikle şu soruya kesin cevap aranmıştır:

> Bir rezervasyonun ana carisi ile o rezervasyon için tahsilat yapan cari farklı olduğunda
> (örn. rezervasyonun bir kısmını bir misafir, kalan kısmını başka bir misafir/kurum ödediğinde),
> tahsilat kayıtları, gelir/satış belgesi, muhasebe fişleri ve cari hareketler doğru cari
> ayrımını koruyarak mı çalışıyor?

## 2. Nihai mimari karar

- **Ödeme ≠ Gelir.** Rezervasyon ödemesi alındığında yalnızca `RezervasyonOdeme` +
  `TahsilatOdemeBelgesi` oluşur; `MuhasebeFis` **otomatik üretilmez**. Muhasebe fişi üretimi
  hem tahsilat hem satış belgesi tarafında **ayrı, bilinçli, manuel bir aksiyondur**
  (`ITahsilatOdemeBelgesiMuhasebeFisService`, `SatisBelgesiMuhasebeFisService`).
- **Rezervasyonun bir "ana/fatura carisi" vardır, ama her tahsilat kendi carisini taşıyabilir.**
  `Rezervasyon.CariKartId` yalnızca **ilk kez** belirlendiğinde yazılır ve check-out sonrası
  satış belgesinin müşterisini belirler. `TahsilatOdemeBelgesi.CariKartId` ise o tahsilatı
  **fiilen yapan** cariyi taşır — ikisi kasıtlı olarak birbirinden bağımsızdır.
- **Cari kart çözümleme önceliği:** kullanıcının bu ödeme için açıkça seçtiği/override ettiği
  cari → rezervasyonda önbelleklenmiş ana cari → TCKN/VKN veya (telefon + ad-soyad) eşleşmesi →
  tesisin varsayılan "Rezervasyon Misafirleri" cari kartı → hiçbiri yoksa HTTP 422 ile kullanıcıdan
  seçim istenir. Otomatik cari kart **oluşturulmaz**; kullanıcı isterse hızlı-oluşturma diyaloğuyla
  elle oluşturur.
- **Kasa/Banka/POS hesabı** ödeme tipine göre filtrelenmiş listeden seçilir; liste boş değilse
  ilk uygun hesap otomatik seçilir, ama seçim her zaman kullanıcı tarafından değiştirilebilir.
- **Fiş iptali durum-bazlıdır, `MuhasebeFisId` hiç sıfırlanmaz.** Bir belgenin yeniden
  fişlenebilir olup olmadığı, bağlı fişin **güncel durumuna** (`Taslak`/`Onaylı` = engelli,
  `İptal`/`TersKayıt` = serbest) bakılarak belirlenir — foreign key kalıcıdır, denetim izi korunur.

## 3. Tahsilat akışı

```
Rezervasyon Ödemeleri dialogu
  → Ödeme Tipi seçilir (Nakit / KrediKarti / HavaleEft / OdayaEkle / Mahsup)
  → Kasa/Banka/POS Hesabı otomatik seçilir (nakit hareketi gerektiren tipler için zorunlu)
  → [opsiyonel] "Bu Ödeme İçin Farklı Cari Kart Seç" ile ödeme bazında cari override
  → Ödeme Kaydet
      → RezervasyonService.KaydetOdemeAsync
          → RezervasyonOdeme oluşturulur
          → RezervasyonOdemeMuhasebeService.TahsilatOlusturAsync
              → RezervasyonCariKartResolver.ResolveAsync (override öncelikli)
              → TahsilatOdemeBelgesi oluşturulur (KaynakModul=Rezervasyon, KaynakId=RezervasyonOdeme.Id)
              → Rezervasyon.CariKartId yalnızca ilk kez set edilir
```

İlgili başlangıç commitleri:

- **`1a93b5d`** — *Rezervasyon odemeleri TahsilatOdemeBelgesi uzerinden muhasebeye entegre edildi.*
  Temel entegrasyonu kurar: `RezervasyonOdemeMuhasebeService`, `OdemeYontemleri` sabitleri,
  `TahsilatOdemeBelgesi` üzerinde kaynak bağlantısı, migration.
- **`e4942fa`** — *6 kod inceleme bulgusu duzeltildi.* İlk implementasyonun review bulgularını
  kapatır (`TahsilatOdemeBelgesiService`, `RezervasyonOdemeMuhasebeService` sertleştirmeleri).
- **`82cc2a4`** — *TahsilatOdemeBelgesi validasyonuna requireCariMuhasebeHesabi parametresi
  eklendi.* Tesis `AlinanAvans` alacak hesabı kullanıyorsa cari kartın kendi hesap planı
  bağlantısının zorunlu olmadığını ayırt eder.
- **`e6ff969`** / **`4978abf`** — Uçtan uca doğrulama testleri eklenir ve ortam değişkenine bağlı
  hale getirilerek normal `dotnet test` akışından izole edilir.

## 4. Manuel tahsilat muhasebe fişi akışı

```
Tahsilat/Odeme Belgeleri ekranı
  → Belge satırında "Muhasebe Fişi Oluştur" (yalnızca fiş yoksa veya bağlı fiş İptal/TersKayıt ise görünür)
      → TahsilatOdemeBelgesiMuhasebeFisService.FisOlusturAsync
          → EnsureFisOlusturulabilirAsync: bağlı fişin GÜNCEL durumuna bakar (foreign key sıfırlanmaz)
          → Borç: seçilen Kasa/Banka/POS hesabı
          → Alacak: TahsilatOdemeBelgesi.CariKartId'nin kendi hesap planı hesabı
  → Muhasebe > Fişler ekranından fiş onaylanır
  → Fiş iptal edilirse: MuhasebeFisService.IptalEtAsync ters kayıt (TersKayıt) üretir
  → Aynı belgeden tekrar fiş oluşturulabilir (İptal/TersKayıt durumları engel sayılmaz)
```

İlgili commitler:

- **`305554d`** — *Tahsilat/Odeme Belgesi'nden manuel muhasebe fisi olusturma eklendi.*
  `TahsilatOdemeBelgeleriController.MuhasebeFisiOlustur` endpoint'i ve ekran butonu.
- **`46bc173`** — *Muhasebe Fisleri ekraninda liste hic gorunmuyordu.* `muhasebe-fis.service.ts`
  içindeki 9 metodun tamamı `ApiResponse<T>` zarfını hiç açmıyordu (`.pipe(map(unwrap))` eksikti);
  liste boş göründüğü ve sayfalama `NaN` gösterdiği için düzeltildi.
- **`d00eab6`** — *Muhasebe fisi iptal edilince TahsilatOdemeBelgesi tekrar fislenemiyordu.*
  `TahsilatOdemeBelgesiMuhasebeFisService`'in naif `MuhasebeFisId.HasValue` kontrolü, bağlı fişin
  durumuna bakan `EnsureFisOlusturulabilirAsync`'e dönüştürüldü; ayrıca ters kayıt (`TersKayıt`)
  fişinin de yanlışlıkla engelleyici sayıldığı ikinci bir hata aynı commit'te giderildi.

## 5. Gelir/satış belgesi akışı

```
Check-out tamamlanır (RezervasyonService.TamamlaCheckOutAsync)
  → best-effort: RezervasyonGelirTahakkukService.OlusturTaslakAsync
      → SatisBelgesi taslağı oluşturulur (CariKartId = Rezervasyon.CariKartId)
      → Konaklama + varsa Ek Hizmet + Restoran/OdayaEkle satırları eklenir
      → Rezervasyon.SatisBelgesiId set edilir
  → (check-out'un kendisi bu adımdan bağımsız olarak zaten başarılıdır — muhasebe
     konfigürasyonu eksik olsa bile resepsiyon check-out'u tamamlayabilmelidir)

Muhasebe > Satış Belgeleri ekranı
  → "Muhasebe Onayına Gönder" → "Onayla" → "Muhasebe Fişi Oluştur"
      → SatisBelgesiMuhasebeFisService.MuhasebeFisiOlusturAsync
          → Borç: SatisBelgesi.CariKartId'nin kendi hesap planı hesabı
          → Alacak: Konaklama/Gelir hesabı + Hesaplanan KDV hesabı
          → CreateCariHareketAsync: SatisBelgesi kaynaklı CariHareket (KalanTutar = GenelToplam)
```

Temel altyapı commit'i: **`930f9c6`** (*Rezervasyon gelir tahakkuku (Faz 2) backend altyapisi
eklendi*) — `RezervasyonGelirTahakkukService`, `RezervasyonCariKartResolver`,
`CariHareketKapamaService` entegrasyonu, `Rezervasyon.SatisBelgesiId`. `108fc60` bu akışın
rezervasyon ödeme dialogundaki panelini ekler ("Konaklama Geliri" bölümü — gelir durumu, fiş
durumu, tahsilat kapama durumu).

## 6. Tahsilat kapama akışı

```
Rezervasyon Ödemeleri dialogu → Konaklama Geliri paneli → "Tahsilatları Kapat"
  → RezervasyonGelirTahakkukService.KapatOncekiTahsilatlariAsync
      → Ön koşul: SatisBelgesi kaynaklı, Aktif durumda bir CariHareket bulunmalı
        (yani satış belgesi fişi onaylanmış/oluşmuş olmalı)
      → Rezervasyona ait her Aktif TahsilatOdemeBelgesi için:
          → CariHareketKapamaService.TahsilatOdemeIcinCariHareketOlusturVeKapatAsync
              → kapatilacak.CariKartId == belge.CariKartId DEĞİLSE reddedilir
                ("Kapatilacak cari hareket secilen cari kart ile uyumlu degil")
              → eşleşiyorsa: satış belgesi hareketinin KalanTutar'ı kapama tutarı kadar düşer
```

Bu davranış **kasıtlıdır**: satış belgesi tek bir cariye (rezervasyonun ana carisi) kesilir;
farklı bir cariden alınan tahsilat o borcu **doğrudan** kapatamaz — muhasebesel olarak iki farklı
cari hesabı birbirini kapatamaz. Cari-split senaryosunda bu, "kısmi kapama" (bazı tahsilatlar
kapanır, ana cariden farklı olanlar hata olarak raporlanır) şeklinde gözlemlenir; bu bir hata
değil, doğru muhasebe kısıtlamasıdır (bkz. Bölüm 11).

## 7. Cari kart davranışı

- `Rezervasyon.CariKartId`: rezervasyonun **ana/fatura carisi**. Yalnızca ilk kez (null iken)
  set edilir; sonraki ödemelerin farklı cari override etmesi bu alanı **sessizce değiştirmez**
  (`d075519` öncesi bu garanti yoktu — bkz. Bölüm 10).
- `TahsilatOdemeBelgesi.CariKartId`: o **spesifik tahsilatı yapan** cari. Ana cariden farklı
  olabilir.
- `SatisBelgesi.CariKartId`: check-out sonrası oluşan gelir belgesinin müşterisi; her zaman
  `Rezervasyon.CariKartId` ile aynıdır.
- Ödeme dialogunda "Bu Ödeme İçin Farklı Cari Kart Seç" / "Cari Kartı Değiştir" kontrolü **her
  zaman erişilebilir** (yalnızca otomatik eşleşme başarısız olduğunda değil); seçim "Kaldır" ile
  temizlenirse backend rezervasyonun ana carisine geri döner.
- Hızlı cari kart oluşturma diyaloğu (`a6f7e8e`), misafir bilgileriyle (ad-soyad, telefon, TCKN)
  önceden doldurulmuş minimal bir formla, ekranlar arası geçiş olmadan `Musteri` tipinde cari kart
  oluşturur ve otomatik seçer.

## 8. Kasa/Banka/POS davranışı

- Seçenekler ödeme tipine göre filtrelenir (`OdemeYontemleri.UygunKasaBankaHesapTipleri`:
  Nakit→NakitKasa, KrediKarti→KrediKarti, HavaleEft→Banka/DovizHesabi).
- Liste boş değilse **ilk kayıt otomatik seçilir**; mevcut seçim yeni listede hâlâ varsa korunur,
  yoksa listenin ilk kaydına düşülür; liste boşsa seçim `null` kalır ve kullanıcıya "bu tesis için
  tanımlı hesap bulunamadı" uyarısı gösterilir (`abee777`).
- Ödeme tipi değiştiğinde seçim önce sıfırlanır, sonra yeni tipin listesine göre yeniden
  otomatik seçilir.

## 9. Test edilen senaryolar

Aşağıdaki senaryolar bu doğrulama turunda **gerçek tarayıcı oturumuyla** (Playwright, çalışan
backend + SQL Server), gerçek kullanıcı arayüzü etkileşimleriyle uçtan uca test edilmiştir:

1. Kasa/Banka/POS varsayılan seçimi — boş liste, tek kayıt, çoklu kayıt, seçim korunması, ödeme
   tipi değişiminde sıfırlama.
2. İlk tahsilat → otomatik cari eşleşmesi başarısız → 422 paneli → hızlı cari kart oluşturma →
   `Rezervasyon.CariKartId` ilk kez set edilir.
3. İkinci tahsilat → manuel "Farklı Cari Kart Seç" → farklı bir cariye (Cari B) override →
   `Rezervasyon.CariKartId` **değişmez**, `TahsilatOdemeBelgesi.CariKartId` = Cari B.
4. "Kaldır" davranışı — override temizlenince backend ana cariyi kullanır.
5. Tahsilat belgesinden manuel muhasebe fişi oluşturma — Cari A ve Cari B belgeleri için ayrı ayrı,
   doğru cariye alacak yazan fişler.
6. Fiş onaylama → iptal → ters kayıt oluşumu → aynı belgeden tekrar fiş oluşturma (tek aktif fiş
   kuralı DB'den doğrulandı).
7. Check-out → gelir belgesi taslağı best-effort oluşumu → `SatisBelgesi.CariKartId` = ana cari.
8. Satış belgesi → muhasebe onayına gönder → onayla → muhasebe fişi oluştur — borç satırının
   **doğru** cariye yazıldığı fiş satırı seviyesinde doğrulandı.
9. Satış belgesi kaynaklı `CariHareket.KalanTutar`'ın doğru set edildiği ve tahsilat kapama
   sonrası doğru düştüğü DB seviyesinde doğrulandı.
10. "Tahsilatları Kapat" — aynı cariden gelen tahsilat kapanır, farklı cariden gelen tahsilat
    doğru şekilde reddedilir (kısmi kapama senaryosu).
11. Regresyon: normal satış faturası fişi, tahsilat belgesi fişi, fiş iptali sonrası tekrar
    fişleme — hepsi bu turdaki düzeltmelerden sonra da bozulmadan çalışıyor.

## 10. Düzeltilen kritik hatalar

| # | Commit | Hata | Etki |
|---|---|---|---|
| 1 | `abee777` | Kasa/Banka/POS listesi 2+ kayıt içerdiğinde seçim boş bırakılıyordu | Kullanıcı her seferinde elle seçim yapmak zorundaydı |
| 2 | `d075519` | `RezervasyonCariKartResolver.ResolveAsync`, rezervasyonun ana carisi belirlendikten sonra gelen `cariKartIdOverride`'ı hiç dikkate almıyordu | Bir rezervasyonun farklı kısımlarını farklı cariler ödeyemiyordu; mimari olarak imkansızdı |
| 3 | `fe3480e` | Frontend'de farklı cari seçme kontrolü yalnızca otomatik eşleşme başarısız olduğunda (422) görünüyordu | Backend düzeltmesi (#2) olsa bile kullanıcı bunu tetikleyecek bir arayüze sahip değildi |
| 4 | `46bc173` | `muhasebe-fis.service.ts`'deki 9 metodun tamamı `ApiResponse<T>` zarfını açmıyordu | Muhasebe Fişleri ekranı hep boş görünüyor, sayfalama `NaN` gösteriyordu |
| 5 | `d00eab6` | Fiş iptal edilince `TahsilatOdemeBelgesi.MuhasebeFisId` güncellenmiyor, ayrıca ters kayıt fişi de yanlışlıkla engelleyici sayılıyordu | İptal edilen bir fişin belgesi bir daha asla yeniden fişlenemiyordu |
| 6 | `422d8f7` (1/3) | `MuhasebeAnaHesapKodlari` sabitlerinde 5 hesap kodu (`GelirSatis`, `SatisIade`/`IndirimIade`, `KDVHesaplanan`, `KDVIndirilecek`, `AlinanSiparisAvanslari`) seed hesap planıyla uyumsuzdu (rakamlar sistematik yer değiştirmiş) | Sistemdeki **hiçbir** satış/alış faturası muhasebe fişi oluşturamıyordu |
| 7 | `422d8f7` (2/3) | Satış belgesi fiş stratejileri, borç satırını `SatisBelgesi.CariKartId`'yi hiç dikkate almadan tesisteki ilk/rastgele cari detay hesabına yazıyordu | Fatura borcu **tamamen alakasız bir müşteriye** kaydedilebiliyordu (test verisinde "Önder DEMİR" örneği) — cari-split'in gelir tarafındaki asıl amacını baltalayan en kritik bulgu |
| 8 | `422d8f7` (3/3) | `SatisBelgesiMuhasebeFisService.CreateCariHareketAsync`, oluşturduğu `CariHareket.KalanTutar` alanını hiç set etmiyordu (varsayılan 0 kalıyordu) | Hiçbir satış belgesi borcu tahsilatla **asla** kapatılamıyordu |

Tüm bu hatalar bu doğrulama turu sırasında bulunmuş, düzeltilmiş ve tekrar test edilerek
kapatıldığı doğrulanmıştır.

## 11. Bilinen sınırlamalar

- **Tahsilat kapama, farklı cariden gelen ödemeleri satış belgesinin borcuna doğrudan kapatamaz.**
  Bu kasıtlı bir muhasebe kısıtlamasıdır (Bölüm 6), ancak `RezervasyonGelirTahakkukService.
  BuildOzetAsync` içindeki `TahsilatKapamaDurumu` hesaplaması bu kısmi başarıyı her zaman
  **"Hata"** olarak etiketliyor — `KismenKapatildi` durumu tanımlı olmasına rağmen mantık
  (`hataliSayisi > 0 ? Hata : ...`) onu asla seçmiyor. Bu bir UX/sınıflandırma iyileştirmesi
  olarak ayrıca ele alınmalıdır; bu doküman kapsamında düzeltilmemiştir.
- **Restoran/OdayaEkle satırlarında ürün bazlı KDV kırılımı yoksa varsayılan KDV oranı
  kullanılmaktadır.** Ürün bazlı KDV takibi ayrı bir iş kalemi olarak değerlendirilmelidir.
- **Alış faturası (`AlisFaturasi`/`AlisIadeFaturasi`) muhasebe fişi üretimi için gereken hesap
  kodları** (`StokTicariMal`, `Demirbas`, `GiderHizmetMaliyet`, `GiderGenelYonetim`) hâlâ hiçbir
  tesis için seed edilmemiş durumda. Bu, `422d8f7`'nin kapsamı dışındadır (o commit yalnızca
  satış tarafını düzeltmiştir) ve ayrı bir seed/konfigürasyon çalışması gerektirir.
- **Gelir hesabı ve KDV hesabı için tesise özel detay hesap otomatik oluşturma mekanizması
  yoktur.** Kasa/Banka/Cari kartlarda (`CreateOrResolveDetayHesapAsync`) mevcut olan bu desen,
  gelir tablosu hesapları için uygulanmamıştır; her yeni tesis için bu detay hesapların elle
  (veya bir seed script'iyle) tanımlanması gerekir.
- Bu doğrulama turunda kullanılan test kullanıcısının (`trt-admin`) Satış Belgeleri ekranı
  yetkisi eksikti; test için geçici olarak eklenip **test sonunda geri alınmıştır**. Kalıcı bir
  rol/yetki ataması yapılmamıştır — üretime geçiş öncesi ilgili rollerin doğru kullanıcı
  gruplarına atandığından ayrıca emin olunmalıdır.

## 12. Sonuç ve üretime geçiş notları

- **Tahsilat tarafı doğrulandı.** Ödeme kaydı, kasa/banka seçimi, cari override, ana cari
  korunumu — hepsi beklenen davranışı sergiliyor.
- **Gelir/satış belgesi tarafı doğrulandı.** Check-out sonrası taslak oluşumu, muhasebe onay
  akışı, fiş üretimi ve borç satırının doğru cariye yazılması doğrulandı.
- **Tahsilat kapama doğrulandı.** Aynı cariden gelen tahsilatlar doğru kapanıyor, farklı
  cariden gelenler doğru şekilde (ve öngörülebilir bir hata mesajıyla) reddediliyor.
- **Cari-split senaryosu doğrulandı.** Bir rezervasyonun farklı kısımlarını farklı cariler
  ödediğinde, tüm zincir (tahsilat → satış belgesi → muhasebe fişi → cari hareket → kapama)
  ana cariyi bozmadan ve her kaydın kendi cari referansını koruyarak çalışıyor.
- **Mevcut durumda yeni kritik hata bulunmadı.** Bölüm 11'deki sınırlamalar bilinen, kapsam
  dışı bırakılmış veya kasıtlı tasarım kararlarıdır; bloklayıcı değildir.

**Üretime geçiş öncesi önerilen ek adımlar** (bu doküman kapsamında yapılmamıştır):

1. Gelir tablosu ve KDV hesapları için tesis bazlı detay hesap oluşturmayı otomatikleştiren bir
   seed script'i veya tesis onboarding adımı eklenmesi.
2. Alış faturası tarafı için eksik hesap kodlarının (stok, demirbaş, gider) seed edilmesi ve
   ayrı bir alış-faturası regresyon turunun koşulması.
3. `TahsilatKapamaDurumu` hesaplamasının `KismenKapatildi` durumunu doğru raporlayacak şekilde
   düzeltilmesi (Bölüm 11).
4. Restoran/OdayaEkle satırları için ürün bazlı KDV kırılımının değerlendirilmesi (ayrı iş
   kalemi).
5. `trt-admin` ve benzeri operasyonel rollerin Satış Belgeleri ekranına kalıcı erişiminin
   (uygun görülüyorsa) rol/yetki tanımlarına eklenmesi.
