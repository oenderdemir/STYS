# Satış / Alış Belgesi Muhasebe Fişi Strateji Tasarımı

## Amaç

`SatisBelgesiMuhasebeFisService` içinde büyüyen satış/alış/iade kurallarını tek bir if/else bloğu içinde şişirmek yerine belge tipi bazlı stratejilere bölmek gerekir. Kısa vadede `SatisBelgesi` altyapısı korunur, ancak muhasebe fişi üretim sorumluluğu strateji tabanlı hale getirilir.

Bu tasarım fazında kod değişikliği yapılmaz. Amaç, mevcut servis akışını inceleyip ileriki uygulama fazı için net bir mimari çerçeve çıkarmaktır.

## Mevcut `SatisBelgesiMuhasebeFisService` analizi

### Yaptığı işler

- Belgeyi önce repository ile ön-okur, sonra transaction içinde tekrar DB’den yükler.
- Belgenin silinmiş olup olmadığını, muhasebe onay durumunu ve `MuhasebeFisId` varlığını kontrol eder.
- Toplam matrah, KDV ve genel toplam tutarlılığını doğrular.
- Açık muhasebe dönemini `IMuhasebeDonemService` üzerinden bulur.
- Aynı kaynak belge için aktif fiş olup olmadığını `MuhasebeFisler` tablosundan denetler.
- Satır toplamlarını doğrular.
- 120 / 600 / 391 hesaplarını hesap planı ve vergi-hesap eşleme üzerinden bulur.
- Muhasebe fişi ana kaydını ve satırlarını `StysAppDbContext` üzerinden yazar.
- Belgeye `MuhasebeFisId` ve oluşturma zamanını işler.
- Transaction sonunda güncel belgeyi yeniden okuyup DTO döner.

### Bağımlılıklar

- `ISatisBelgesiRepository`
- `StysAppDbContext`
- `IMapper`
- `IMuhasebeDonemService`
- `ILogger<SatisBelgesiMuhasebeFisService>`

### Satışa özel kalan alanlar

- Belge tipi kontrolleri doğrudan satış merkezlidir.
- Gelir hesabı olarak 600, alıcı hesabı olarak 120, KDV hesabı olarak 391 kurgusu sabittir.
- Tevkifatlı belgeler açık hata ile reddedilmektedir.
- Proforma ve iade tipleri muhasebe fişi üretiminde desteklenmemektedir.
- Fiş açıklamaları ve kaynak modül kodları satış belgesine göre üretilmektedir.

### Ortak altyapı olabilecek kısımlar

- Belge yükleme ve varlık doğrulama
- Muhasebe onay durumu kontrolü
- Aynı kaynak belge için tek fiş kontrolü
- Açık dönem kontrolü
- Fiş numarası üretimi
- Transaction başlatma / commit / rollback akışı
- Fiş ana kaydı ve satırlarının kaydedilmesi
- Belgeye `MuhasebeFisId` yazılması

## Problem

Satış, alış ve iade kuralları tek serviste if/else ile büyüdüğünde:

- servis okunabilirliği düşer,
- test kapsamı zorlaşır,
- satış kuralları alış kurallarını gölgeler,
- yeni belge tipleri eklendiğinde hata riski artar.

## Önerilen mimari

### Orchestrator

`SatisBelgesiMuhasebeFisService` orkestratör olarak kalır:

- belgeyi yükler,
- ortak validasyonları yapar,
- uygun stratejiyi seçer,
- stratejiden satırları veya fiş bileşenlerini alır,
- `MuhasebeFisService`/`DbContext` üzerinden fişi kaydeder,
- belgeye `MuhasebeFisId` yazar,
- belge durumunu günceller.

### Strateji arayüzü

Önerilen temel arayüz:

```csharp
public interface ISatisBelgesiMuhasebeFisStratejisi
{
    bool Destekler(SatisBelgesi belge);
    Task<IReadOnlyList<CreateMuhasebeFisSatiriRequest>> SatirlariOlusturAsync(
        SatisBelgesi belge,
        CancellationToken cancellationToken);
}
```

Bu faz için en uygun ayrım, stratejinin yalnızca fiş satırlarını üretmesi ve orkestratörün:

- belgeyi doğrulaması,
- fiş numarasını üretmesi,
- `MuhasebeFis` ana kaydını oluşturması,
- transaction bütünlüğünü yönetmesi

şeklindedir.

### Neden sadece satır üretimi?

- Muhasebe fişi ana kaydı orkestratörde tek noktadan tutulur.
- Fiş numarası, kaynak modül, kaynak id ve audit akışı aynı yerde kalır.
- Strateji sınıfı yalnızca belge tipine özgü muhasebe satırlarını üretir.
- Birim testleri daha kolay olur.

## Strateji sınıfları

Önerilen sınıflar:

- `SatisFaturasiMuhasebeFisStratejisi`
- `AlisFaturasiMuhasebeFisStratejisi`
- `SatisIadeFaturasiMuhasebeFisStratejisi`
- `AlisIadeFaturasiMuhasebeFisStratejisi`

Desteklenmeyen tipler:

- `Proforma` açık hata döner.
- `FaturaTaslagi` için karar ayrıca netleştirilmelidir; mevcut davranışta doğrudan fiş üretimi hedeflenmiyor.
- Legacy `IadeFaturasi` yeni kayıt için tercih edilmemeli, varsa geriye dönük destek düşünülmelidir.

## Orchestrator sorumlulukları

Ortak validasyonlar orkestratörde kalmalıdır:

- belge var mı,
- belge silinmiş mi,
- tesis erişimi var mı,
- belge muhasebe onaylı mı,
- `MuhasebeFisId` dolu mu,
- toplamlar sıfırdan büyük mü,
- satırlar var mı,
- satır toplamları belge toplamlarıyla uyumlu mu,
- belge tipi strateji tarafından destekleniyor mu,
- aynı kaynak belge için aktif fiş var mı,
- ters kayıt / iptal edilmiş fiş tutarlılığı mevcut mu.

Bu kontrollerin ardından strateji seçimi yapılır.

## Satış faturası stratejisi

Mevcut satış davranışı korunmalıdır:

- Borç: 120 Alıcılar / Cari
- Alacak: 600 Satış gelirleri
- Alacak: 391 Hesaplanan KDV

### Beklenen kurallar

- KDV istisna satırlarında 391 satırı oluşmamalıdır.
- Tevkifatlı belgeler bu fazda desteklenmiyorsa açık ve kullanıcı dostu hata verilmelidir.
- Kaynak modül ve belge numarası mevcut pattern ile korunmalıdır.

## Alış faturası stratejisi

Alış faturası için ayrı muhasebe mantığı gerekir:

- Alacak: 320 Satıcılar
- Borç: 191 İndirilecek KDV
- Borç: 153 Ticari Mallar / 740 Hizmet Üretim Maliyeti / ilgili stok-gider hesabı

### Hesap seçimi

Satır bazında hesap seçimi şu kaynaklardan birine bağlanmalıdır:

- taşınır kart üzerindeki hesap eşleşmesi,
- taşınır kodu / kategori hesabı,
- varsayılan stok / gider ana hesabı.

Eğer hesap eşleşmesi bulunamazsa kullanıcıya açık hata verilmelidir.

### Cari hesabı

- Tedarikçi cari kart kullanılmalıdır.
- Cari kart tipi `Tedarikci` değilse işlem reddedilmelidir.
- 320 altında hesap eşleşmesi bulunamazsa fiş üretilmemelidir.

## İade faturaları

İade faturaları için muhasebe yaklaşımı satış ve alıştan ters yönlüdür:

- satış iade: 120 ters, 600 ters, 391 düzeltme,
- alış iade: 320 ters, 153/740 ters, 191 düzeltme.

Bu fazda iade tipleri için otomatik üretim açılmamalı; ayrı karar ve test seti gerektirir.

## Tevkifat yaklaşımı

Tevkifat hem satış hem alışta farklı muhasebe etkileri doğurur.

- Satışta tahsil edilecek KDV azalır.
- Alışta 191 ve sorumlu vergi hesapları ayrışır.

Bu nedenle tevkifat desteği tek bir küçük if ile eklenmemelidir.
Bu konu ayrı bir fazda, kendi stratejileriyle ele alınmalıdır.

Öneri:

- Tevkifatlı belge desteklenmiyorsa açık hata ver.
- Ayrı Faz 75: tevkifat muhasebesi stratejileri.

## Stok hareketi ile ilişki

Bu faz stok hareketi oluşturmaz. Yine de ilerideki akış için önerilen sıra:

1. belge onaylanır,
2. stok hareketi oluşturulur,
3. muhasebe fişi oluşturulur,
4. belge durumu güncellenir.

İş kuralları gereği stok ve muhasebe fişinin aynı transaction içinde olması tercih edilir. Ancak bunun uygulama fazı ayrı planlanmalıdır.

## Hata mesajları

Önerilen kullanıcı mesajları:

- `Belge tipi desteklenmiyor.`
- `Alış faturası için tedarikçi cari bulunamadı.`
- `Tedarikçi cari için 320 hesabı bulunamadı.`
- `Satır için stok/gider hesabı bulunamadı.`
- `KDV hesap eşleşmesi bulunamadı.`
- `Tevkifatlı belgeler için muhasebe fişi henüz desteklenmiyor.`
- `İade faturaları için muhasebe fişi henüz desteklenmiyor.`
- `Belge zaten muhasebe fişine bağlı.`

## BaseService / repository uyumu

Bu servis cross-aggregate işlem yaptığı için mevcut yaklaşım uygundur:

- belge repository üzerinden okunur,
- satış belgesi ve muhasebe fişi aynı `DbContext` transaction’ında güncellenir,
- `BaseRdbmsService` tek başına yeterli değildir.

Strateji sınıfları bu yapıyı bozmaz; yalnızca satır üretim sorumluluğunu taşır.

## Test stratejisi

### Birim testleri

- satış faturası stratejisi 120 / 600 / 391 üretir,
- KDV istisna satırı 391 satırı üretmez,
- alış faturası stratejisi 153 / 191 / 320 üretir,
- hizmet alış satırı gider hesabı üretir,
- tevkifat desteklenmiyorsa hata verir,
- iade belge tipi desteklenmiyorsa hata verir,
- belge zaten fişe bağlıysa hata verir.

### Entegrasyon testleri

- satış faturası fiş oluşturur,
- alış faturası bu fazda kapalı kalır,
- yetkisiz tesis işlemi engellenir,
- aynı kaynak belge için ikinci fiş oluşturulamaz.

## Önerilen sonraki fazlar

- Faz 73B: satış stratejisinin dışarı alınması ve orkestratörün strateji seçimi.
- Faz 73C: alış faturası muhasebe fişi stratejisi.
- Faz 74: stok hareketi ile muhasebe fişi sıralaması.
- Faz 75: tevkifat muhasebesi.
- Faz 76: iade faturaları stratejileri.

## Sonuç

Kısa vadede `SatisBelgesi` altyapısı korunmalı, ancak muhasebe fişi üretimi belge tipi bazlı stratejilere bölünmelidir. En güvenli ayrım, orkestratörün ortak kontrol ve transaction sorumluluğunu taşıması; stratejilerin ise yalnızca belge tipi bazlı satır üretmesi olacaktır.

## Faz 73B — Satış Faturası Stratejiye Taşıma

- `ISatisBelgesiMuhasebeFisStratejisi` eklendi.
- `SatisFaturasiMuhasebeFisStratejisi` eklendi.
- Mevcut satış faturası 120 / 600 / 391 davranışı stratejiye taşındı.
- `SatisBelgesiMuhasebeFisService` orkestratör olarak kaldı.
- Alış / iade / tevkifat fişi üretimi hâlâ kapalıdır.
- Migration yapılmamıştır.
- Stok hareketi oluşturulmamıştır.

## Faz 73C — Alış Faturası Muhasebe Fişi Stratejisi

- `AlisFaturasiMuhasebeFisStratejisi` eklendi.
- Alış faturası için 153 / 740 / 770 + 191 / 320 kurgusu desteklendi.
- Stok satırlarında taşınır kartın kendi muhasebe hesabı, ardından taşınır kod varsayılan eşlemesi, ardından `153` fallback uygulanır.
- Hizmet satırlarında `740` öncelikli, `770` ikinci fallback olarak kullanılır.
- Tevkifat ve iade fiş üretimi hâlâ kapalıdır.
- Stok hareketi oluşturulmamıştır.
- `SatisBelgesi` üzerinde `CariKartId` olmadığı için 320 tarafı ilk sürümde genel hesap üzerinden çalışır; bu sınırlama not edilmiştir.
- Alış belgeleri ekranında `Muhasebe Fişi Oluştur` aksiyonu kontrollü şekilde görünür hale getirilmiştir.

## Faz 73C-A — MuhasebeFisSatir Kaynak Alanları Doğrulaması

- `MuhasebeFisSatir` entity’sinde `CariKartId`, `TasinirKartId`, `DepoId` ve `KasaBankaHesapId` alanları mevcut.
- Aynı alanlar `StysAppDbContextModelSnapshot` içinde de yer alıyor.
- `20260514221833_AddMuhasebeFisleri` başlangıç migration’ında `MuhasebeFisSatirlari` tablosu bu alanlarla oluşturulmuş durumda.
- Bu nedenle Faz 73C için ek migration gerekmedi.
- `AlisFaturasiMuhasebeFisStratejisi` ve orchestrator tarafındaki kaynak alan setlemeleri compile/runtime açısından güvenli kabul edildi.
- Stok/depo bağlantısı için ayrıca kolon ekleme veya migration ihtiyacı oluşmadı; Faz 74 stok hareketi fazına bırakıldı.

## Faz 74 — Alış Faturası Stok Giriş Hareketi

- Alış faturasında taşınır/stok satırları için stok giriş hareketi oluşturulur.
- Hizmet satırları stok hareketi oluşturmaz.
- `TasinirKartId` olan satırlarda `DepoId` zorunlu tutulur.
- Stok hareketi ve muhasebe fişi aynı transaction içinde ele alınır.
- Aynı kaynak alış faturası için duplicate stok giriş hareketi engellenir.
- Satış stok çıkışı, iade ve tevkifat kapsam dışıdır.
- Migration yapılmamıştır.

## Faz 74A — StokHareket Zorunlu Alan Kontrolü

- `StokHareket` entity’sinde `TesisId` alanı yok.
- Zorunlu alanlar: `DepoId`, `TasinirKartId`, `HareketTarihi`, `HareketTipi`, `Miktar`, `BirimFiyat`, `Tutar`, `Durum`, `KdvUygulamaTipi`, `KdvOrani`, `KdvTutari`.
- Faz 74’te oluşturulan stok hareketinde bu alanlar set ediliyor.
- Ek migration gerekmedi.

## Faz 75C — Satış Tevkifatı Muhasebe Fişi

- Tevkifatlı satış faturaları için ayrı strateji eklendi.
- Tevkifat karşı hesabı `TevkifatHesapEslemeService` üzerinden çözülüyor.
- Normal satış stratejisi tevkifatlı belgeleri dışlıyor.
- Alış tevkifatı, iade ve tevkifat rapor etkileri hâlâ kapsam dışıdır.

## Faz 75D — Alış Tevkifatı Muhasebe Fişi

- Tevkifatlı alış faturaları için ayrı strateji eklendi.
- Tevkifat karşı hesabı `TevkifatHesapEslemeService` üzerinden `IslemYonu = Alis` ile çözülüyor.
- Normal alış stratejisi tevkifatlı belgeleri dışlıyor.
- İade ve rapor etkileri hâlâ kapsam dışıdır.

## Faz 76 — Regresyon Kontrolü

- Normal satış, satış tevkifat, normal alış ve alış tevkifat akışları kontrol edildi.
- Alış stok giriş hareketi davranışı doğrulandı.
- Proforma ve iade belgeleri hâlâ kapalı.
- Migration gerekmedi.

## Faz 77 — Satış Stok Çıkış Hareketi

- Satış faturasında taşınır/stok satırları için stok çıkış hareketi oluşturulur.
- Hizmet satırları stok hareketi oluşturmaz.
- Duplicate çıkış hareketi engellenir.
- Alış stok giriş davranışı korunur.
- İade belgeleri kapsam dışıdır.
- Migration yapılmamıştır.

## Faz 78 — Satış / Alış İade Faturaları

- `SatisIadeFaturasiMuhasebeFisStratejisi` ve `AlisIadeFaturasiMuhasebeFisStratejisi` eklendi.
- Satış iade için 610 / 391 / 120, alış iade için 320 / 153-740-770 / 191 kurgusu desteklendi.
- Satış iade stok girişi, alış iade stok çıkışı eklendi.
- Legacy `IadeFaturasi` kapalı kaldı.
- Tevkifatlı iade hâlâ desteklenmiyor.
- Migration yapılmamıştır.

## Faz 78A — İade Akış Sırası

- Strateji seçimi stok hareketinden önce doğrulanır.
- Fiş satırları üretildikten sonra stok hareketi oluşturulur.
- Transaction bütünlüğü korunur.

## Faz 79 — CariKart Bağlantısı

- `SatisBelgesi` üzerinde nullable `CariKartId` alanı eklendi.
- Snapshot alanları korunurken gerçek cari bağlantısı da taşınır.
- Satış belgelerinde müşteri tipli, alış belgelerinde tedarikçi tipli cari kart doğrulaması eklenmiştir.
- Manuel müşteri/cari bilgisi modunda `CariKartId` boş kalabilir.
- Ek migration gerekli oldu ve oluşturuldu.

## Faz 80 — Cari Hareket Oluşturma

- Muhasebe fişi oluşturulurken `CariKartId` dolu belgeler için cari hareket oluşturulur.
- `CariKartId` boş manuel belgelerde cari hareket oluşturulmaz.
- Satış, alış ve iade belge yönleri için borç/alacak yönü belge toplamına göre set edilir.
- Aynı belge için duplicate cari hareket engellenir.
- Tahsilat/ödeme kapama sonraki fazlara bırakılmıştır.
- Migration yapılmamıştır.

## Faz 81A — Cari Hareket Kapama Alanları

- `KapananTutar`, `KalanTutar`, `IliskiliCariHareketId`, `KapandiMi` alanları eklendi.
- `KalanTutar` yeni kayıtlarda hareket tutarı kadar, eski kayıtlarda backfill ile başlatılır.
- Kapama mantığı Faz 81B’ye bırakılmıştır.

## Faz 81A-A — Request Model Temizliği

- `CreateCariHareketRequest` ve `UpdateCariHareketRequest` içinden kapama alanları kaldırıldı.
- Kapama alanları sadece sistem tarafından yönetilir.

## Faz 81B-A — Tahsilat/Ödeme Bağlantı Alanı

- `TahsilatOdemeBelgesi` üzerine `KapatilacakCariHareketId` eklendi.
- Kapama hesaplaması Faz 81B’ye bırakıldı.

## Faz 81B — Tahsilat/Ödeme ile Cari Kapama

- Tahsilat/ödeme belgesi oluşturulurken seçilen cari hareket kısmi veya tam kapatılır.
- `KapananTutar`, `KalanTutar`, `KapandiMi` ve `IliskiliCariHareketId` güncellenir.
- Duplicate ve fazla kapama engellenir.
- Update/delete geri alma sonraki faza bırakılmıştır.
- Migration yapılmamıştır.

## Faz 81B-A2 — Kaynak Modül Sabiti

- `TahsilatOdemeBelgesi` kaynak modül kullanımı merkezi `MuhasebeKaynakModulleri` sabitine taşındı.
- Cari kapama servisleri artık string literal yerine ortak sabiti kullanır.
