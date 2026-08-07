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
| UBL Üretimi | `ublFeatureUygulanabilirMi`/`ublFeatureAktifMi`/`islemeHazirMi` | Aşağıdaki dört duruma bkz. — Faz 2B.11.1 |
| İmzalama | `signingGateUygulanabilirMi`/`signingSuAnMumkunMu` | Yalnız yerel imza gerektiren yöntemlerde (bugün: hiçbiri production'da) anlamlıdır |
| Otomatik Gönderim | `otomatikGonderimGerekliMi` | Bugün hiçbir production yöntemi otomatik gönderim yapmaz |

Kart renkleri (severity) PrimeNG'nin standart `success`/`warn`/`danger`/`secondary` haritasını
kullanır — sabit hex renk kodu YOKTUR. Durum yalnız renkle DEĞİL, aynı zamanda metinle de ifade
edilir (ör. "Hazır" / "Eksik" / "Uygulanamaz") — renk körlüğü erişilebilirlik gereksinimini
karşılamak için.

### UBL Üretimi kartı — dört durum (Faz 2B.11.1)

`EBelgeUbl.Enabled` global feature flag'i (bkz. `docs/e-belge-ubl-pdf-eposta-renderer-hazirlik-raporu.md`,
"Kesin ürün kararı: devreye alma tarihi") de readiness'e katılır — ama YALNIZ yöntem GERÇEKTEN
yerel unsigned UBL GEREKTİRİYORSA (`ublFeatureUygulanabilirMi`, bugün yalnız GİB Portal). Frontend
bu flag'in DEĞERİNİ kendisi TAHMİN ETMEZ — her zaman backend'den okunan `ublFeatureAktifMi` alanına
bakar:

| Durum | Koşul | Kart |
|---|---|---|
| Uygulanamaz | Yerel UBL bu yöntemde N/A (`ublFeatureUygulanabilirMi=false` — Kullanılmayacak/Harici Muhasebe Sistemi) | secondary |
| Kapalı | Yerel UBL gerekli AMA `EBelgeUbl.Enabled=false` | danger |
| Bekliyor | Yerel UBL gerekli, flag açık, ama BAŞKA bir readiness blokajı var (ör. satıcı verisi eksik) | warn |
| Hazır | TÜM koşullar sağlanmış | success |

"Kapalı" durumunda `blokajNedenleri` içinde `EBELGE_UBL_FEATURE_DISABLED` kodu (Türkçe etiket:
"UBL üretimi devre dışı") görünür ve `islemeHazirMi=false` olur — GİB Portal politikası aktif,
satıcı verisi tam, aktivasyon tarihi gelmiş olsa BİLE.

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

**Faz 2B.11.1 düzeltmesi**: "Harici Sistem Kodu" alanının ne zaman ZORUNLU olduğu bilgisi de
(`hariciSistemKoduGerekliMi`) artık AYNI `yontemler` dizisinden, seçili yönteme karşılık gelen
`EBelgeYontemReadinessModel` kaydından okunur (`secilenYontemCapability` getter'ı üzerinden) —
ÖNCEKİ sürüm bunu `entegrasyonYontemi === HariciMuhasebeSistemi` biçiminde component içinde
YENİDEN hesaplıyordu (backend'in ZATEN döndürdüğü bir business kuralının frontend'de İKİNCİ bir
kopyası). Aynı prensip operasyonel/disabled, snapshot, unsigned UBL, imza, otomatik gönderim gibi
TÜM capability alanları için geçerlidir — component hiçbirini yöntem enum'una göre YENİDEN
TÜRETMEZ, yalnız backend'in döndürdüğü değeri okur.

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

## Runtime güvenlik garantisi (Faz 2B.11.1)

Bu ekranın readiness kartı bir GÜVENLİK KONTROLÜ DEĞİLDİR — yalnız GÖRÜNTÜLEME amaçlıdır.
Backend'in KENDİSİ (`SatisBelgesiService.FaturaKesAsync`) de fail-closed'dır: politika yerel
unsigned UBL üretimini gerektiriyorsa (bugün yalnız GİB Portal) ve `EBelgeUbl.Enabled=false`
ise, satış kesimi resmî fatura numarası verilmeden/sayaç tüketilmeden/immutable karar
yazılmadan REDDEDİLİR (`EBELGE_UBL_FEATURE_DISABLED`, HTTP 503) — ekran "Kapalı" gösterse de
göstermese de, kullanıcı readiness'i hiç görmeden doğrudan API'yi çağırsa bile AYNI kural
uygulanır. Kullanılmayacak/Harici Muhasebe Sistemi gibi yerel UBL gerektirmeyen yöntemler bu
kontrolden hiç etkilenmez.

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

STYS'te yetkilendirme ÜÇ AYRI, birbirine KARIŞTIRILMAYAN katmandan oluşur (bkz. Faz 2B.11.2,
`docs/e-belge-kurum-politikasi-ve-yonlendirme-stratejisi.md` "Faz 2B.11.2" bölümü):

1. **Menü görünürlüğü** — `MenuItem` → `MenuItemRoles` → `Xxx.Menu` (bkz. "erişim" bölümü
   yukarıda). Bu ekran İÇİN `MuhasebeSatisBelgeleriYonetimi.Menu`.
2. **Frontend işlem UX'i** — `canView`/`canManage` getter'ları, gerektiği ÖLÇÜDE (form alanlarını
   göstermek/gizlemek, Kaydet/Aktifleştir/Pasifleştir butonlarını etkinleştirmek/devre dışı
   bırakmak için). Bunlar bir authorization KATMANI DEĞİLDİR — yalnız kullanıcı deneyimidir.
3. **Gerçek authorization** — backend endpoint'lerindeki `[Permission(...)]` attribute'ları
   (`StructurePermissions.MuhasebeSatisBelgeleriYonetimi.View`/`.Manage`, `OR SuperAdmin`) +
   tenant/kurum scope kontrolleri (`EnsureCanAccessKurumAsync`/`EnsureCanManageKurumAsync`).

Angular rotası (`/muhasebe/e-belge-yonetimi`) YALNIZ authentication ağacının (`authGuard`/
`authChildGuard`) altındadır — ek bir domain-permission route guard TAŞIMAZ (Faz 2B.11.1'de
eklenmiş olan `permissionOrSuperAdminGuard`, Faz 2B.11.2'de bu mimari nedenle KALDIRILDI — bkz.
aşağıdaki not). Bu, menü görünürlüğünün ZATEN `.Menu` rolü tarafından kontrol edildiği, ve
gerçek işlem yetkisinin HER ZAMAN backend'de uygulandığı bir mimaride, rota seviyesinde İKİNCİ
bir yetki kontrolünün gereksiz olmasındandır — kullanıcı doğrudan URL'ye gelse bile, yetkisiz
bir API çağrısı backend tarafından REDDEDİLMEYE devam eder (`EnsureCanAccessKurumAsync`/
`EnsureCanManageKurumAsync`, cross-tenant 403).

`canView`/`canManage` getter'ları, backend'in `View/Manage OR SuperAdmin` semantiğini BİREBİR
yansıtır (`isSuperAdminUser()` domain izin kontrolünden ÖNCE koşulsuz `true` döner) — ama bunlar
SAYFA RENDER/YÜKLEME davranışı ve buton etkinliği İÇİNDİR, kullanıcıyı başka bir rotaya
yönlendiren İKİNCİ bir authorization katmanı DEĞİLDİR.

**UserManagement bağımsızlığı**: ekranın kurum bağlamı (aktif kurum adı, SuperAdmin kurum seçici)
`KurumService.getAll()`/`getById()` (ikisi de backend'de `UserManagement.View` GEREKTİRİR)
KULLANMAZ — bunun yerine yalnız kimlik doğrulama gerektiren, tenant-scope'a göre ZATEN
erişilebilir kurumları döndüren `GET .../kurum/benim-kurumlarim` (`KurumService.getMyKurumlar()`)
kullanılır. Bu sayede salt e-belge Viewer/Manager izni olan, `UserManagement` izni OLMAYAN bir
kullanıcı ekranı TAM olarak kullanabilir. "Satıcı bilgilerini tamamla" kısayolu (Kurum Yönetimi
ekranına gider) HÂLÂ `UserManagement.Manage OR SuperAdmin` gerektirir — ama bu, yalnız O BUTONUN
görünürlüğünü etkiler, sayfanın KENDİSİNİN görüntülenmesini ETKİLEMEZ.

**Faz 2B.11.2 notu**: `permissionOrSuperAdminGuard` (ve genel `permissionGuard`) STYS'in
DB-driven `MenuItemRoles` mimarisiyle TUTARSIZ, gereksiz bir ikinci yetkilendirme katmanıydı —
KALDIRILDI (`permission.guard.ts` dosyasının kendisi de, repository genelinde başka hiçbir
çağıranı KALMADIĞINDAN, tamamen silindi). Bu, backend authorization'ın GEVŞETİLMESİ ANLAMINA
GELMEZ — yalnız frontend'in ZATEN otoriter olmayan, yanlışlıkla ikinci bir kaynak-of-truth
İZLENİMİ veren bir kontrolünün kaldırılmasıdır.

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
| `EBELGE_UBL_FEATURE_DISABLED` | Yerel unsigned UBL gerektiren bir yöntem seçili ama `EBelgeUbl.Enabled` global flag'i kapalı — bkz. "UBL Üretimi kartı" |

Bu kodlar HİÇBİR ZAMAN VKN/TCKN/adres gibi ham kişisel/kurumsal veri İÇERMEZ — yalnız hangi
alanın/kapının eksik/kapalı olduğunu işaret eden sabit birer işarettir.
