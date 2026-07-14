# Rezervasyon Gelir Tahakkuku (Faz 2) — Tasarım ve Uygulama Bulguları

Faz 1'de kurulan "ödeme ≠ gelir" ilkesinin ikinci ayağı: check-out sonrası konaklama gelirinin
muhasebeye — tahsilattan bağımsız, doğru dönemde ve doğru hesaplarla — nasıl yansıtılacağı.

İlgili tasarım dokümanları (kod içermez): mimari tasarım ve somut uygulama planı bu turdan önce
ayrı Artifact dokümanları olarak onaylandı. Bu dosya, uygulama sırasında ortaya çıkan bulguları ve
kabul edilen korumaları kayıt altına alır.

## 1. Korunan kurallar

1. **Ödeme tahsilattır, gelir değildir.** Gelir yalnızca `SatisBelgesi`/fatura akışıyla oluşur.
2. **Check-out, gelir belgesi taslağı oluşturulamadı diye asla başarısız olmaz.** Best-effort.
3. **Muhasebe fişi ve tahsilat kapatma muhasebe kontrolünde kalır.** Otomatik zincirlenmez.
4. **Tahsilat kapatma, `SatisBelgesi` kaynaklı `CariHareket` oluşmadan önce çalışmaz.**

## 2. Mimari kararlar

### 2.1 Cari kart çözümleme ortaklaştırıldı

Faz 1'de `RezervasyonOdemeMuhasebeService` içine gömülü olan kademeli cari kart çözümleme mantığı
(rezervasyon → override → TCKN/telefon eşleşmesi → tesis varsayılanı → 422) `IRezervasyonCariKartResolver`
/ `RezervasyonCariKartResolver`'a çıkarıldı. Hem tahsilat hem gelir tahakkuku tarafı aynı resolver'ı
kullanıyor — davranış değişmedi, yalnızca kod tekilleşti.

### 2.2 RezervasyonSatisBelgesiService genişletildi, yeniden yazılmadı

- `CariKartId` artık taslak isteğine set ediliyor (daha önce hiç doldurulmuyordu — fiş üretilse
  bile `CariHareket` hiç oluşmuyordu).
- `SatisBelgesiTaslakOlusturRequest`'e (genel amaçlı, tüm operasyon modülleri kullanır)
  `CariKartId` alanı eklendi — additive, geriye dönük uyumlu.
- `BuildSatirlarAsync` konaklama satırlarının yanına ek hizmet (`RezervasyonEkHizmetler`) ve
  restoran (`RezervasyonOdemeler` içinde `OdemeTipi=="OdayaEkle"`, negatif tutar) satırlarını
  ekliyor. `SatisBelgesiSatirTipi` enum'unda `EkHizmet` ve `YiyecekIcecek` zaten tanımlıydı —
  yeni enum değeri gerekmedi.

**Bilinen sınırlama:** Ne ek hizmet ne restoran siparişi satır bazında KDV kırılımı taşıyor.
Her iki satır tipi de `VarsayilanKdvOrani` (%10) ile faturalanıyor. Gerçek oran farklıysa (örn.
alkollü içecek) bu bir veri doğruluğu açığıdır — kapsamlı çözüm restoran/ek hizmet modüllerinin
KDV bilgisi taşımasını gerektirir; bu turun kapsamı dışında bırakıldı.

### 2.3 Tahsilat kapama — CariHareketKapamaService'e SIFIR değişiklik

Mimari tasarımda önerilen "kapama çekirdeğinin yeniden yazılması" fikri, kodu okuyunca gereksiz
çıktı: `CariHareketKapamaService.TahsilatOdemeIcinCariHareketOlusturVeKapatAsync` zaten yalnızca
`TahsilatOdemeBelgesi.KapatilacakCariHareketId` dolu mu diye bakıyor — kim doldurduğu önemli değil.

`RezervasyonGelirTahakkukService.KapatOncekiTahsilatlariAsync`:
1. Rezervasyonun `SatisBelgesiId`'si dolu değilse 400.
2. `SatisBelgesi` kaynaklı, `Aktif` bir `CariHareket` (fatura hareketi) yoksa 400 — kural #4'ün
   uygulanma noktası burası; `Durum`/`MuhasebeFisId` string'ine değil, doğrudan `CariHareket`'in
   varlığına bakılıyor (otoriter sinyal).
3. Rezervasyona ait, henüz kapanmamış her `Aktif` `TahsilatOdemeBelgesi` için `KapatilacakCariHareketId`
   fatura hareketinin id'sine set edilir, sonra **değiştirilmemiş** `TahsilatOdemeIcinCariHareketOlusturVeKapatAsync`
   tekrar tekrar çağrılır.
4. Kapama kaydı hep aynı `KaynakModul=TahsilatOdemeBelgesi`/`KaynakId=belge.Id` şemasını kullandığı
   için, iptal/geri alma tarafında da `CariHareketKapamaService.GeriAlAsync` hiçbir değişiklik
   gerektirmeden çalışıyor (Senaryo 10/11 ile doğrulandı).

Tetikleme kararı: `SatisBelgesiMuhasebeFisService`'e rezervasyona özel bir çağrı **eklenmedi** —
genel amaçlı kalıyor. Kapama, `RezervasyonController`'daki ayrı `tahsilatlari-kapat` uç noktasıyla,
muhasebe ekibinin bilinçli aksiyonu olarak tetikleniyor.

### 2.4 Check-out best-effort izolasyonu

`RezervasyonService.TamamlaCheckOutAsync`: check-out'un kendi `SaveChangesAsync`'i ve bildirimi
tamamlandıktan SONRA, ayrı bir `try/catch` bloğunda `IRezervasyonGelirTahakkukService.OlusturTaslakAsync`
çağrılıyor. Bu blok check-out'un transaction'ının tamamen dışında; hata durumunda:
- `_domainLogger.Failed(...)` ile loglanıyor,
- `Severity=Warn` bir bildirim üretilmeye çalışılıyor (bu da kendi try/catch'i içinde — best-effort
  içinde best-effort),
- `TamamlaCheckOutAsync` her koşulda normal döner.

Doğrulama: `CheckOut_GelirBelgesiTaslakBasarisizOlsaBileCheckOutTamamlanir` testi, gelir tahakkuku
servisi `FailOnOlustur=true` bir sahte ile değiştirilip check-out'un yine `CheckOutTamamlandi`
durumuna geçtiğini kanıtlıyor.

### 2.5 Tahsilat Kapama Durumu — kalıcı alan yok, isteğe bağlı hesaplanıyor

`RezervasyonGelirOzetiDto.TahsilatKapamaDurumu` (Kapatılmadı / Kısmen Kapatıldı / Tam Kapatıldı /
Hata) hiçbir yeni DB alanına yazılmıyor — `RezervasyonGelirTahakkukService.BuildOzetAsync` içinde
istek anında hesaplanıyor:

- Rezervasyona ait `Aktif` `TahsilatOdemeBelgesi` adayları bulunur.
- Her aday için aktif bir kapama `CariHareket`'i var mı (`KaynakModul=TahsilatOdemeBelgesi`) kontrol edilir.
- `KapatilacakCariHareketId` dolu ama kapama hareketi yoksa → o belge **Hata** sayılır (kapama
  denenmiş ama tamamlanmamış — örn. `TahsilatOdemeIcinCariHareketOlusturVeKapatAsync` içindeki bir
  doğrulama hatası nedeniyle).
- `errorCount > 0` → `Hata`; `closedCount == 0` → `Kapatilmadi`; `closedCount == total` → `TamKapatildi`;
  aksi halde `KismenKapatildi`. Aday yoksa (hiç tahsilat yok) → `TamKapatildi` (bekleyen aksiyon yok).

## 3. Entity ve migration

- `Rezervasyon.SatisBelgesiId` (nullable int) + navigasyon eklendi.
- Migration `AddRezervasyonSatisBelgesiEntegrasyonu`: tek kolon + filtrelenmiş unique index
  (`WHERE IsDeleted=0 AND SatisBelgesiId IS NOT NULL`, `KapatilacakCariHareketId` index'iyle aynı
  desen) + FK (Restrict). Additive-only; `ZZZ_CheckNoPendingChanges` boş diff ile doğrulandı.
- `GelirBelgesiDurumu` gibi ayrı bir durum alanı **bilerek eklenmedi** — tekil doğruluk kaynağı
  `SatisBelgesi.Durum`'dur; ikinci bir alan senkron kalma yükü ve sapma riski taşırdı.
- `SatisBelgesiTaslakOlusturRequest.CariKartId` (genel amaçlı DTO, additive).

## 4. Yeni uç noktalar

- `GET  /ui/rezervasyon/kayitlar/{id}/gelir-ozeti` — `RezervasyonGelirOzetiDto`
- `POST /ui/rezervasyon/kayitlar/{id}/gelir-belgesi-olustur` — idempotent, `SatisBelgesiDto` döner
- `POST /ui/rezervasyon/kayitlar/{id}/tahsilatlari-kapat` — `RezervasyonTahsilatKapamaSonucuDto` döner

Mevcut `kayitlar/{id}/satis-belgesi-taslagi-olustur` uç noktası **değiştirilmedi** — genel amaçlı
kalmaya devam ediyor.

## 5. Test bulguları

- **InMemory (RezervasyonGelirTahakkukuTests.cs, 5 senaryo):** cari kart çözümleme (başarılı/422),
  ek hizmet+restoran satırları, idempotent taslak oluşturma, retroaktif kapama (2 tahsilat) — hepsi
  `SatisBelgesiService.CreateAsync`'in kendisi transaction açmadığı için InMemory'de çalışabiliyor.
- **Gerçek SQL Server gerektirenler (RezervasyonOdemeMuhasebeIntegrationTests.cs, 3 yeni senaryo):**
  eşzamanlı gelir belgesi oluşturma (mevcut `SatisBelgesi(KaynakModul,KaynakTipi,KaynakId)` unique
  index'i tarafından korunuyor), kapatılmış tahsilatın `GeriAlAsync` ile geri alınması (bu metod
  kendi transaction'ını açıyor — InMemory relational olmayan provider'da desteklenmiyor), dönem
  kapalıyken kapama geri almanın engellenmesi. Üç senaryo da mevcut fatura onay/fiş zincirini
  yeniden kurmak yerine, `SatisBelgesiMuhasebeFisService.CreateCariHareketAsync`'in üreteceğiyle
  birebir aynı şemada bir `CariHareket` doğrudan seed ediliyor — test odağı retroaktif kapama/geri
  alma mantığı, satış belgesi onay akışı değil (o akış kendi testlerinde kapsanıyor).
- **Cleanup sırası düzeltmesi:** `TahsilatOdemeBelgeleri.KapatilacakCariHareketId` artık gerçekten
  doluyor (daha önce hep null'du), bu yüzden entegrasyon testinin `CleanupAsync`'i `TahsilatOdemeBelgeleri`'ni
  `CariHareketler`'den ÖNCE silecek şekilde yeniden sıralandı (FK Restrict).
- **Pre-existing, bu turla ilgisiz:** `RezervasyonServiceTests.cs`'teki paylaşılan
  `SeedReservationFixtureWithTenRoomsAsync` helper'ı `Tesis.KurumId` set etmiyor; bu yüzden dosyadaki
  ~77 test (yeni eklenen `CheckOut_GelirBelgesiTaslakBasarisizOlsaBileCheckOutTamamlanir` dahil)
  "Aktif kurum bilgisi bulunamadi" hatasıyla başarısız oluyor — Faz 1'de zaten belgelenmiş, worktree
  izolasyonuyla doğrulanmış, bu turda dokunulmamış bir sorun (kapsam dışı, test-altyapısı).

### Çalıştırma

```
dotnet test                                                         # normal, DB gerekmez
STYS_INTEGRATION_TEST_CONNECTION_STRING="Server=localhost,14333;Database=STYSDB;User Id=sa;Password=<yerel-sifre>;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True" \
  dotnet test --filter Category=Integration                        # 11/11, gercek SQL Server
```

## 6. Frontend

Rezervasyon ödeme dialogunda (check-out sonrası görünür) yeni "Konaklama Geliri" paneli: Gelir
Durumu (SatisBelgesi.Durum), Gelir Belgesi No, Muhasebe Fişi (var/yok), Tahsilat Kapama Durumu
(4 durum, renkli `p-tag`). "Gelir Belgesi Oluştur" (idempotent, best-effort başarısız olduysa elle
tekrar dener) ve "Tahsilatları Kapat" (yalnızca fiş varsa aktif) butonları.
