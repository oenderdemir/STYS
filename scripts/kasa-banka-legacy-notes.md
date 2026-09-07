# Kasa/Banka legacy hesap düzeltmesi

Paylaşılan production örneğinde **iki legacy master kayıt** var: `KASA-MERKEZ` ve
`BNK-VARSAYILAN`. İkisi de aynı kök nedenden etkileniyor: eski
`20260419193034_AddKasaBankaHesapTanimiVeHareketBaglantisi` migration'ı finansal
hesapları ana hesaplara bağlamış. Production veritabanına bu çalışma sırasında
bağlanılmadı; sistemdeki toplam hesap/belge/kullanım sayıları henüz ölçülmedi.

Yeni işlem uygunluğu beş hesap planı sorununu kapsar: bağlantı/kayıt yok, silinmiş,
pasif, hareket göremez, detay hesap değil. Finansal hesabın kendisi de aktif ve
silinmemiş olmalıdır. Global filtre değiştirilmedi; geçmiş kayıt ve rapor sorguları
legacy hesaplara erişmeye devam eder. Ana hesaplar detay hesaba dönüştürülmez.

## Etkilenen seçim ve kayıt yolları

| Ekran/akış | Koruma |
| --- | --- |
| Rezervasyon ödeme hesabı seçimi ve ödeme kaydı | Ortak backend uygunluk sorgusu; geçersiz global fallback elenir |
| Kasa hareketleri, banka hareketleri | Tip bazlı aktif seçim sorgusu ve kayıt doğrulaması |
| Hesap tanımlarının kasa/banka bağlantıları | Paylaşılan `GetByTipAsync(..., true)` sorgusu |
| Finansal Hesaplar / kredi kartının bağlı bankası | Seçenekler backend tip sorgusundan gelir; bağlı banka kayıt doğrulaması |
| POS terminal eşleme, POS ödeme ve test ödemesi | Tip sorgusu ve doğrudan hesap doğrulamaları |
| Kantin varsayılan kasası/POS hesabı, satış ödeme hesabı | Seçenekler, varsayılan hesap kaydı ve satış doğrulaması |
| Genel tahsilat/ödeme belgesi API'si | Verilen finansal hesap kimliği ekleme/güncellemede doğrulanır |
| Genel muhasebe fişi oluşturma/güncelleme | Finansal hesap uygunluğu, tesis ve satır hesap planı eşleşmesi |

Finansal hesap düzenleme/silme işlemlerinin bağlı hesap planına etkisi yalnız
aynı tesisteki hareket görebilen detay hesaplara uygulanır. Böylece legacy
master üzerinde işlem yapmak KASA/BANKALAR ana hesap adını veya aktifliğini değiştirmez.
`TahsilatOdemeBelgesiMuhasebeFisService` içindeki sıkı hesap planı kontrolü korunur.

## Kullanım envanteri ve belgeler

`kasa-banka-legacy-diagnostic.sql` salt okunurdur. İlk sonuç finansal hesaplar için
TahsilatOdemeBelgesi, RezervasyonOdeme, KasaHareket, BankaHareket, PosOdemeIslemi ve
MuhasebeFisSatiri kullanım sayılarını verir. İkinci sonuç geçersiz hesaba bağlı
tüm tahsilat/ödeme belgelerini kaynak modülü, durum, tesis, geçmiş ve hedef sayısıyla listeler.
Silinmiş/iptal edilmiş kayıtlar da envantere dahildir.

Üçüncü sonuç veritabanı FK metadata'sından **tüm finansal hesap referanslarını** sayar.
Repository'deki diğer doğrudan referanslar: KantinSatisOdeme, PosTerminal,
HesapKasaBankaBaglanti. Farklı isimli referanslar: KasaBankaHesap.BagliBankaHesapId,
PosTahsilatValor.KrediKartiHesapId/BagliBankaHesapId,
KantinSatisNoktasi.VarsayilanNakitKasaId/VarsayilanPosHesapId.

## Repair sınırı

`kasa-banka-legacy-repair.sql` varsayılan `@ExecuteRepair = 0` ile yalnız plan gösterir.
`1` yapılırsa yalnız şu koşulların tamamını sağlayan belgeleri ve varsa tam eşleşen
rezervasyon ödemesini tek transaction içinde taşır:

- Belge aktif, silinmemiş ve pozitif tutarlıdır; cari ve kaynak tesisleri tutarlıdır.
- Belge üzerinde fiş kimliği/tarihi ve kaynak bağlantıları üzerinden **hiçbir fiş geçmişi yoktur**.
  Taslak, onaylı, iptal, ters kayıt ve silinmiş fişler de geçmiş sayılır.
- Aynı tesis, hesap tipi ve para biriminde, aynı tesise ait geçerli detay hesap planına
  bağlı tam bir hedef vardır. Desteklenen tipler NakitKasa/Nakit ve Banka/HavaleEft'tir.
- Kaynak bağımsız/manuel belgedir veya iki yöndeki bağlantıları, hesap, tutar, para birimi,
  ödeme yöntemi ve aktif durumu eşleşen rezervasyon ödemesidir.
- POS işlemi/valör, kantin ödemesi, kasa/banka hareketi veya cari kapama bağlantısı yoktur.

Fiş geçmişi olanlar B grubudur ve otomatik değiştirilmez. Fişsiz A grubunda hedef
yoksa yönetici Finansal Hesaplar ekranından tesis hesabı oluşturmalıdır. Birden fazla
hedef, bilinmeyen kaynak, tesis çelişkisi, POS/kantin/hareket/cari kapama bağlantısı,
iptal veya silinmiş belge manuel inceleme gerektirir. İlgili hareketleri topluca
yeniden bağlamak yerine bu bağlı iş akışları raporlanıp manuel bırakılır.

Hesap üretimi, master pasifleştirme/silme, fiş/satır/bakiye güncellemesi yapılmaz.
Script `SERIALIZABLE`, `XACT_ABORT` ve hata halinde rollback kullanır; yeniden çalıştırma
zaten düzeltilmiş belgeleri değiştirmez. Uygulama yazıcıları ve muhasebeleştirme işleri
durdurulmuş bakım aralığında çalıştırın; önizlemeyi ve işlem çıktısını bakım kaydına ekleyin.

## Hedef doğrulama

Beş test `FullyQualifiedName~LegacyFinansalHesap` filtresiyle seçilir; full suite gerekmez.
SQL testi `STYS_INTEGRATION_TEST_CONNECTION_STRING` ile verilen sunucuda benzersiz,
geçici bir test veritabanı oluşturup kaldırır; verilen veritabanının verisini değiştirmez.
SQL fixture onaylı, silinmiş ters kayıt, eksik hedef, çoklu hedef, valör, hareket,
para birimi uyuşmazlığı ve güvenli rezervasyon eşleşmesini kapsar.

2026-09-07 doğrulaması: **5 başarılı, 0 başarısız, 0 atlanan test**. SQL Server
2019 LocalDB üzerinde önizleme, gerçek repair ve tekrar çalıştırma doğrulandı.
On test belgesinden yalnız iki güvenli belge ve ilgili rezervasyon ödemesi taşındı.
Frontend `tsc --noEmit -p tsconfig.app.json` ve `git diff --check` başarılı.
Full suite çalıştırılmadı. Bellek baskısı nedeniyle test derlemesi analizörler
kapalı (`-m:1 -p:UseSharedCompilation=false -p:RunAnalyzers=false`) yapıldı;
mevcut nullable/obsolete/package uyarıları dışında derleme hatası yok.
