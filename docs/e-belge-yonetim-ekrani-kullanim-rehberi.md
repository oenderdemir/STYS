# E-Belge Yönetimi Ekranı Kullanım Rehberi (Faz 2B.11)

Bu rehber, Faz 2B.10.x'te kurulan kurum bazlı e-belge yönlendirme mimarisinin (bkz.
`docs/e-belge-kurum-politikasi-ve-yonlendirme-stratejisi.md`) üzerine Faz 2B.11'de eklenen
**E-Belge Yönetimi** ekranını anlatır. Ekran, mevcut `KurumEBelgePolitikasiController` API'sinin
(policy GET/PUT/revizyonlar) ve yeni, salt-okunur `readiness` endpoint'inin bir kullanıcı arayüzüdür
— ekranın kendisi hiçbir iş kuralı taşımaz, TÜM hesaplama (yöntem yeteneği, global kapı durumu,
aktivasyon tarihi, satıcı verisi tamlığı, imzalama uygulanabilirliği) backend'de yapılır.

## E-Belge Yönetimi ekranına erişim

- Rota: `/muhasebe/e-belge-yonetimi`.
- Menüde: **Muhasebe → E-Belge Yönetimi** (`pi pi-file-edit` ikonu, DB-driven menu kaydı — bkz.
  migration `20260807000000_AddEBelgeYonetimiMenuFaz2B11`).
- Görünürlük, ZATEN var olan `MuhasebeSatisBelgeleriYonetimi.Menu` rolüne bağlıdır — Faz 2B.11 YENİ
  bir rol/izin İCAT ETMEMİŞTİR.
- Kurum bağlamı: kullanıcının aktif tek bir kurumu varsa ekran otomatik o kurumu yükler. SuperAdmin
  gibi birden fazla/hiç aktif kurumu olmayan kullanıcılar için ekrana ÖZGÜ minimal bir kurum seçici
  gösterilir (yeni bir global tenant selector İCAT EDİLMEMİŞTİR). Kullanıcının erişemediği bir
  kuruma yönlendirilirse (ör. eski bir bookmark/query param) ekran "erişim yok" durumunu gösterir —
  backend zaten cross-tenant istekleri 403 ile reddeder, frontend bu kontrolü YİNELEMEZ, yalnız
  sonucu gösterir.

## Hazırlık durumu

Ekranın üst kısmındaki kart grid'i, `GET .../e-belge-politikasi/readiness` yanıtının BİREBİR
görselleştirmesidir — kartlardan hiçbiri frontend'de ayrıca hesaplanmaz:

| Kart | Kaynak alan | Anlamı |
|---|---|---|
| Genel İşlem Kapısı | `globalProcessingDurumu`/`globalProcessingAktifMi` | Global `EBelgeProcessing.Enabled`+`NotBeforeLocalDate` kapısının durumu |
| Kurum Politikası | `politikaYapilandirilmisMi`/`politikaAktifMi` | Politika hiç yok / var-pasif / var-aktif |
| Entegrasyon Yöntemi | `entegrasyonYontemi`/`yontemOperasyonelMi` | Seçili yöntem ve production'da desteklenip desteklenmediği |
| Satıcı Bilgileri | `saticiAnaVerileriHazirMi` | Yerel UBL üretimi gerektiren yöntemlerde Kurum ana verisinin tamlığı |
| UBL Üretimi | `islemeHazirMi` | Yerel snapshot/UBL üretimi için TÜM koşulların sağlanıp sağlanmadığı |
| İmzalama | `signingGateUygulanabilirMi`/`signingSuAnMumkunMu` | Yalnız yerel imza gerektiren yöntemlerde (bugün: hiçbiri production'da) anlamlıdır |
| Otomatik Gönderim | `otomatikGonderimGerekliMi` | Bugün hiçbir production yöntemi otomatik gönderim yapmaz |

Kart renkleri (severity) PrimeNG'nin standart `success`/`warn`/`danger`/`secondary` haritasını
kullanır — sabit hex renk kodu YOKTUR. Durum yalnız renkle DEĞİL, aynı zamanda metinle de ifade
edilir (ör. "Hazır" / "Eksik" / "Uygulanamaz") — renk körlüğü erişilebilirlik gereksinimini
karşılamak için.

Bir yöntem yerel imza/snapshot gerektirmiyorsa (ör. `GİB Portal` imza istemez, `Kullanılmayacak`/
`Harici Muhasebe Sistemi` hiçbirini istemez) ilgili kart "Uygulanamaz" gösterir — kırmızı bir
blokaj olarak GÖSTERİLMEZ, çünkü backend bu alanları zaten `...GerekliMi=false`/
`signingGateUygulanabilirMi=false` olarak işaretler.

## Entegrasyon yöntemleri

Yöntem seçim listesi `GET readiness`'in `yontemler` dizisinden üretilir (backend
`IEBelgeYontemYetenekSaglayici`'nin BİREBİR yansıması — frontend'de İKİNCİ bir capability matrix
YOKTUR):

- **Kullanılmayacak** — STYS bu kurum için yerel bir e-belge pipeline'ı oluşturmaz, satış normal
  tamamlanır. Production'da seçilebilir.
- **Harici Muhasebe Sistemi** — mali belge tamamen harici bir sistemde yönetilir; STYS yalnız bu
  kararı kaydeder (dış sisteme çağrı bu fazda YOKTUR). `Harici Sistem Kodu` alanı ZORUNLUDUR.
  Production'da seçilebilir.
- **GİB Portal** — STYS yerel snapshot ve doğrulanmış unsigned UBL üretir; gönderim GİB Portal
  üzerinden MANUEL yapılır, yerel imza YOKTUR. Production'da seçilebilir.
- **Özel Entegratör** — enum'da mevcuttur ama gerçek bir adaptör OLMADAN backend `OperasyonelMi=false`
  döner; seçenek listede **devre dışı**, yanında "Henüz desteklenmiyor" notuyla görünür. Aktif bir
  politika olarak KAYDEDİLEMEZ (backend `EBELGE_KURUM_POLICY_METHOD_NOT_IMPLEMENTED` ile 400 döner).
- **Doğrudan GİB** — aynı şekilde enum'da mevcuttur, gerçek bir HSM/mali mühür entegrasyonu OLMADAN
  devre dışı gösterilir ve aktive edilemez.

Devre dışı bırakma kararı TAMAMEN backend'in `operasyonelMi` alanından gelir — frontend hangi
yöntemlerin desteklendiğini kendi başına hardcode ETMEZ; yeni bir yöntem ileride production'da
aktive edilirse ekranda KOD DEĞİŞİKLİĞİ GEREKMEZ.

## Politika aktivasyonu

Formu doldurup **Kaydet**'e basıldığında önce istemci tarafı temel doğrulamalar çalışır (değişiklik
nedeni zorunlu, harici sistem kodu — yöntem gerektiriyorsa — zorunlu, aktif bir politika seçili
desteklenmeyen bir yönteme ayarlanamaz, aktif politika için aktivasyon tarihi zorunlu). Bu
doğrulamalar yalnız kullanıcı deneyimini iyileştirir; NİHAİ doğrulama HER ZAMAN backend'de tekrar
yapılır (`EBelgeKurumPolitikaYonetimServisi.GuncelleAsync`).

Geçiş türüne göre bir onay diyaloğu gösterilir:

- **Pasif → Aktif**: seçilen yöntem ve aktivasyon tarihini özetleyen bir onay istenir.
- **Yöntem değişikliği** (politika zaten aktifken): eski/yeni yöntem adları gösterilerek onay
  istenir. Kurumun bekleyen (non-terminal) e-belge işi varsa backend bu değişikliği
  `EBELGE_KURUM_POLICY_CHANGE_BLOCKED` ile reddeder — ekran bu hatayı olduğu gibi kullanıcıya
  gösterir, kendi başına bir "bekleyen iş var mı" kontrolü YAPMAZ.
- **Aktif → Pasif**: bkz. "Kill switch" bölümü.

## Kill switch

Bir politikayı Aktif'ten Pasif'e çekmek HER ZAMAN izinlidir (acil "kill switch" — bekleyen iş olsa
bile). Onay diyaloğu açıkça şunu belirtir: *"E-belge işlemleri durdurulacaktır. Bekleyen e-belge
işleri yeni işlem başlatmayacak ve politika tekrar aktifleştirilene kadar bekleyecektir."* Kayıt
başarılı olduğunda backend'deki kill switch ANINDA etkilidir (bkz. strateji dokümanı, "Kill Switch
Davranışı") — worker savunma katmanları devam eden işleri güvenli biçimde bekletir, ekran bunu
yalnız gösterir.

## Aktivasyon tarihi

Aktivasyon tarihi seçici (`p-datepicker`), backend'in `globalMinimumAktivasyonYerelTarihi`
alanından okunan minimum tarihi (bugün: `2026-09-15`, `EBelgeProcessingOptions.NotBeforeLocalDate`)
kullanıcıya gösterir ve seçilebilir minimum olarak uygular — bu değer frontend'de SABİT bir
tarih/magic string olarak YAZILMAMIŞTIR, her yüklemede backend'den gelir. Tarih, saat dilimi
kaymasını (off-by-one) önlemek için yalnız `yyyy-MM-dd` takvim değeri olarak okunur/yazılır — UTC
dönüşümü hiçbir noktada yapılmaz. Nihai doğrulama (tarihin gerçekten global minimumdan önce
olmadığı) YİNE backend'de yapılır; frontend'deki minimum sadece kullanıcıyı erken yönlendirir.

## Satıcı ana verileri

Yerel snapshot/UBL üretimi gerektiren bir yöntem (bugün yalnız `GİB Portal`) seçiliyken, backend
Kurum'un VKN/Vergi Dairesi/Adres/İlçe/İl alanlarının doluluğunu kontrol eder ve eksik olanları
`eksikSaticiAlanlari` içinde GÜVENLİ KOD olarak döner (`VERGI_NO`, `VERGI_DAIRESI`, `ADRES`,
`ILCE`, `IL` — ham değer/VKN İÇERMEZ). Ekran bu kodları Türkçe etikete çevirir
(`EBELGE_BLOKAJ_NEDENI_LABELS`) ve eksik alan varsa **Kurum Yönetimi** ekranına götüren bir kısayol
gösterir (kullanıcının o ekranı görme izni varsa). Kurum bilgileri güncellenip kaydedildikten sonra
readiness'in yeniden yüklenmesiyle uyarı otomatik kalkar — ekran kendi başına "tamam" varsayımı
YAPMAZ, her zaman backend'e yeniden sorar.

## Cari kart e-belge bilgileri

Bu ekranın kapsamı DIŞINDA olsa da ilişkili bir düzeltme: **Cari Kartlar** ekranındaki
"E-Belge Bilgileri" bölümü artık gerçek kişi/kurumsal ayrımını (`CariTipi != "Musteri"` =
kurumsal — `SatisBelgesiService.ApplyCariSnapshot`'ın kullandığı AYNI kural) yansıtır: gerçek
kişi cariler için Ad/Soyad/TCKN, kurumsal cariler için Vergi No/Vergi Dairesi alanları gösterilir;
e-Fatura/e-Arşiv mükellefiyet onay kutuları aynı bölümde toplanmıştır. Bu alanlar backend'de zaten
mevcuttu — Faz 2B.11 yalnız frontend modelinin/kaydetme payload'ının eksik taşıdığı `ad`/`soyad`
alanlarını tamamlamıştır (önceden bu alanlar formda görünmediği için düzenleme sırasında SESSİZCE
kaybediliyordu).

## Revizyon geçmişi

`GET .../e-belge-politikasi/revizyonlar` sonucu, en yeniden en eskiye sıralı bir tabloda gösterilir:
değişiklik zamanı, eski/yeni yöntem, eski/yeni aktiflik, değişiklik nedeni, değiştiren kullanıcı.
Bu kayıtlar immutable'dır (`KurumEBelgePolitikaRevizyonu`) — ekran yalnız görüntüler, DÜZENLEME/SİLME
imkânı YOKTUR.

## Concurrency uyarısı

Politika `RowVersion` ile optimistic concurrency korumalıdır. Ekran açıkken başka bir kullanıcı
(veya başka bir sekme) aynı politikayı güncellerse, kaydetme isteği backend'den 409 döner. Ekran bu
durumda:

1. Kullanıcıya "Politika siz ekranı açtıktan sonra başka bir kullanıcı tarafından değiştirilmiş"
   mesajını gösterir.
2. Politika + hazırlık durumu + revizyon geçmişini **baştan yeniden yükler** (`refreshAll()`) —
   eski `rowVersion` ile SESSİZCE ÜZERİNE YAZMAZ.
3. Kullanıcı, güncel veriyi gördükten sonra değişikliğini isterse tekrar uygular.

## Yetkilendirme

Ekran, mevcut `StructurePermissions.MuhasebeSatisBelgeleriYonetimi` ailesini kullanır — yeni bir
izin İCAT EDİLMEMİŞTİR:

- **View** (veya SuperAdmin): salt-okunur erişim — kayıt/aktivasyon/pasifleştirme kontrolleri
  gizlenir/disabled gösterilir.
- **Manage** (veya SuperAdmin, veya kurum yöneticisi kendi kurumu için): politika kaydetme.

Frontend'deki bu ayrım YALNIZ kullanıcı deneyimi içindir — gerçek yetki kontrolü HER ZAMAN
backend'de (`EnsureCanAccessKurumAsync`/`EnsureCanManageKurumAsync`) yapılır; bir kullanıcı UI'ı
atlayıp doğrudan API'yi çağırsa bile aynı kısıtlamalarla karşılaşır.

## Sık görülen readiness blokajları

`blokajNedenleri` dizisindeki güvenli kodların Türkçe karşılıkları:

| Kod | Anlamı / çözüm |
|---|---|
| `EBELGE_KURUM_POLICY_NOT_CONFIGURED` | Kurum için hiç politika oluşturulmamış — formu doldurup kaydedin |
| `EBELGE_KURUM_POLICY_INACTIVE` | Politika var ama pasif — aktifleştirin |
| `EBELGE_KURUM_POLICY_BEFORE_ACTIVATION_DATE` | Aktivasyon tarihi henüz gelmedi — bekleyin veya tarihi kontrol edin |
| `EBELGE_KURUM_POLICY_METHOD_NOT_IMPLEMENTED` | Seçili yöntem production'da henüz desteklenmiyor |
| `EBELGE_GLOBAL_PROCESSING_DISABLED` | Global e-belge işleme kapısı kapalı — kurum politikası ne olursa olsun beklenmelidir, bu KURUM DIŞI bir kapıdır |
| `EBELGE_KURUM_POLICY_SELLER_DATA_INCOMPLETE` | Kurum'un VKN/Vergi Dairesi/Adres/İlçe/İl bilgilerinden biri veya birkaçı eksik — bkz. "Satıcı ana verileri" |
| `EBELGE_SIGNING_GATE_DISABLED` | Yerel imza gerektiren bir yöntem seçili ama global imzalama kapısı kapalı |

Bu kodlar HİÇBİR ZAMAN VKN/TCKN/adres gibi ham kişisel/kurumsal veri İÇERMEZ — yalnız hangi
alanın/kapının eksik/kapalı olduğunu işaret eden sabit birer işarettir.
