# E-Belge Kurum Politikası ve Yönlendirme Stratejisi (Faz 2B.10)

## Amaç

Faz 2B.9'a kadar e-belge işleme tek bir global anahtar (`EBelgeProcessing.Enabled` +
`NotBeforeLocalDate=2026-09-15`) tarafından yönetiliyordu: kapı açıldığında TÜM kurumlar için
aynı davranış (yerel snapshot → UBL → imza → outbox) devreye giriyordu. Gerçekte her kurumun
e-belge süreci farklıdır (bazıları GİB Portal kullanır, bazıları özel entegratör, bazıları
muhasebeyi tamamen harici bir sistemde yürütür). Faz 2B.10, global kapının ÖNÜNE değil,
**arkasına** — kurum bazlı, fail-closed, denetlenebilir bir yönlendirme katmanı ekler: hangi
kurumun hangi yöntemle işleneceğine karar verir, ama global kapıyı asla kendi başına açamaz.

## Mevcut Global E-Belge Kapıları (değişmedi)

Faz 2B.10 bu kapıları **yeniden yazmaz**, yalnız üzerlerine ek bir katman kurar:

1. **UBL ön-kesim kapısı** (`EBelgeUblOptions`, `SatisBelgesiService.FaturaKesAsync` içinde) —
   14.09.2026 Türkiye yerel tarih kapısı, kesim anındaki belge tarihine göre değerlendirilir.
2. **Global işleme aktivasyon kapısı** (`EBelgeProcessingOptions`, `IEBelgeProcessingActivationGate`) —
   `Enabled=true` VE `NotBeforeLocalDate=2026-09-15` (Europe/Istanbul) geçilmeden worker hiçbir
   mesaj claim etmez.
3. **İmzalama aktivasyon kapısı** (`IEBelgeSigningActivationGate`) — `UblImzala` mesajı
   oluşturulup oluşturulmayacağına karar verir.

Karar sırası (öncelik en yüksekten en düşüğe): **(1) global feature flag → (2) global
cutover/not-before → (3) kurum politikası → (4) immutable per-document plan → (5) job-type-specific
activation gate**. Kurum politikası bu sıralamada global kapılardan SONRA gelir — global kapı
kapalıyken hiçbir kurum politikası (aktif olsa bile) yerel işleme, claim veya imzalama açamaz.

## Kurum Politikası Veri Modeli

`KurumEBelgePolitikasi` (kurum başına en fazla bir satır):

| Alan | Açıklama |
|---|---|
| `KurumId` | `Kurum`'a FK — VKN/VergiDairesi/TenantKey BURADA TEKRARLANMAZ |
| `EntegrasyonYontemi` | `EBelgeEntegrasyonYontemi` |
| `AktifMi` | varsayılan `false` |
| `AktivasyonYerelTarihi` | Türkiye yerel takvim tarihi — global tarihten ÖNCE OLAMAZ |
| `HariciSistemKodu` | yalnız `HariciMuhasebeSistemi` için zorunlu; secret DEĞİLDİR, uzunluk sınırlı |
| `PolitikaSurumu` | ilk sürüm 1, her anlamlı değişiklikte artar |
| `RowVersion` | optimistic concurrency |

PIN/parola/token/endpoint-secret/sertifika bilgisi bu entity'de ASLA tutulmaz. Soft-delete,
geçmiş kararları bozmaz (kararlar ayrı, immutable bir tabloda saklanır — aşağıya bkz.).

## Entegrasyon Yöntemi Matrisi

`EBelgeEntegrasyonYontemi`:

| Değer | Anlamı |
|---|---|
| `Yapilandirilmadi` (0) | Politika hiç ayarlanmamış — ASLA sessizce `Kullanilmayacak` yorumlanmaz |
| `Kullanilmayacak` (1) | Kurum için e-belge süreci STYS dışında/gerekmez — satış normal tamamlanır |
| `HariciMuhasebeSistemi` (2) | Süreç tamamen harici bir muhasebe sisteminde yürütülür (bu fazda adaptör YOK) |
| `GibPortal` (3) | STYS yerel snapshot+UBL üretir, GİB Portal üzerinden manuel gönderim kurumda kalır |
| `OzelEntegrator` (4) | Enum'da mevcut, gerçek adaptör OLMADAN production'da aktive EDİLEMEZ |
| `DogrudanGib` (5) | Enum'da mevcut, gerçek adaptör OLMADAN production'da aktive EDİLEMEZ |

Yöntem yeteneklerinin TEK, merkezi kaynağı `IEBelgeYontemYetenekSaglayici` / `EBelgeYontemYetenekleri`
kaydıdır (`OperasyonelMi`, `YerelSnapshotOlustur`, `YerelUnsignedUblOlustur`, `YerelImzaOlustur`,
`OtomatikGonderimYap`) — bağımsız/çelişebilen config boolean'ları KULLANILMAZ.

Production matrisi:

| Yöntem | OperasyonelMi | Snapshot | UnsignedUbl | İmza | OtomatikGönderim |
|---|---|---|---|---|---|
| Kullanilmayacak | ✔ | – | – | – | – |
| HariciMuhasebeSistemi | ✔ | – | – | – | – |
| GibPortal | ✔ | ✔ | ✔ | – | – |
| OzelEntegrator | – | – | – | – | – |
| DogrudanGib | – | – | – | – | – |

## Desteklenen / Henüz Desteklenmeyen Yöntemler

- **Production'da aktive edilebilir**: `Kullanilmayacak`, `HariciMuhasebeSistemi` (yalnız karar
  kaydı — dış sistem çağrısı bu fazda YOK), `GibPortal` (yerel snapshot+unsigned UBL).
- **Enum'da var, production'da aktive EDİLEMEZ**: `OzelEntegrator`, `DogrudanGib` — gerçek bir
  adaptör/HSM/mali mühür entegrasyonu eklenmeden `OperasyonelMi=false` döner; yönetim servisi
  bunları `AktifMi=true` olarak kabul etmez (`EBELGE_KURUM_POLICY_METHOD_NOT_IMPLEMENTED`).
- Test ortamında (yalnız test assembly'sinde, PRODUCTION DI'a asla kaydedilmeyen
  `EBelgeTestYontemYetenekSaglayici`), `DogrudanGib` tam operasyonel işaretlenir — bu SADECE
  mevcut (Faz 2B.5-2B.9) gerçek XAdES/SignedReady testlerinin bozulmamasını sağlar.

## Immutable Satış Belgesi Kararı

`SatisBelgesiEBelgeKarari` — bir satış belgesinin e-belge kararının, kesim ANINDAKİ politika
sürümüne göre alınmış, KALICI snapshot'ı:

- `(KurumId, SatisBelgesiId)` tekil (filtresiz unique index — soft-delete edilmiş bir karar bile
  rezervasyonu korur).
- `YerelSnapshotOlustur`/`YerelUnsignedUblOlustur`/`YerelImzaOlustur`/`OtomatikGonderimYap`
  kullanıcı config'i DEĞİLDİR — karar anında yetenek sağlayıcısından TÜRETİLMİŞ, salt-okunur bir
  plandır.
- Oluşturulduktan SONRA update/delete EDİLEMEZ (`StysAppDbContext.ApplyAuditInfo` immutability
  koruması — EBelgeSnapshot/EBelgeArtifact ile AYNI desen).
- Kurum politikası SONRADAN değişse (hatta silinse) bile geçmiş kararlar ASLA yeniden yorumlanmaz.
- Tenant-aware composite FK'ler (`(SatisBelgesiId, KurumId)`, `(KurumEBelgePolitikasiId, KurumId)`,
  `(EBelgeKaydiId, KurumId)`) — başka kurumun kaydına ASLA bağlanamaz.

## Satış Belgesi Transaction Akışı

`SatisBelgesiService.FaturaKesAsync` içinde, mevcut UBL ön-kesim kapısından SONRA:

1. Global cutover kontrolü (değişmedi).
2. `IEBelgeKurumPolitikaServisi.DegerlendirAsync(kurumId, belgeTarihi)` çağrılır.
3. Karar fail-closed bir nedenle geldiyse (`PolitikaYapilandirilmadi`/`PolitikaPasif`/
   `KurumAktivasyonTarihiGelmedi`/`YontemHenuzDesteklenmiyor`/`PolitikaGecersiz`) →
   `EBelgeKurumPolitikaEngelliException` fırlatılır, TÜM işlem (resmi fatura no dahil) rollback
   olur.
4. `Kullanilmayacak`/`HariciMuhasebeSistemi`/global henüz aktif değil gibi NORMAL (hata olmayan)
   durumlarda satış NORMAL tamamlanır; `SatisBelgesiEBelgeKarari` HER ZAMAN yazılır (karar kaydı
   olmadan sessizce e-belgesiz tamamlanma YOKTUR).
5. Yetenekler yerel snapshot gerektiriyorsa (`GibPortal`), AYNI transaction'da `EBelgeKaydi` +
   `EBelgeSnapshot` + `ArtefaktOlustur` outbox mesajı oluşturulur.

## Outbox Yönlendirmesi

- `ArtefaktOlustur` mesajı yalnız `YerelSnapshotOlustur=true` VE `YerelUnsignedUblOlustur=true`
  olduğunda oluşturulur.
- `UblImzala` mesajı YALNIZ üç koşulun TÜMÜ sağlandığında oluşturulur: immutable kararın
  `YerelImzaOlustur=true`'su + global imzalama kapısının açık olması + kurum politikasının O ANDA
  hâlâ aktif olması. `GibPortal`'ın beklenen NİHAİ durumu `UnsignedUblHazir`'dır — bu bir hata
  DEĞİLDİR.
- Worker/handler savunma katmanı (`IEBelgeKurumPolitikaServisi.IslemHalaIzinliMiAsync`), claim
  SONRASI, pahalı dış işlemden ÖNCE tekrar kontrol eder — mesajın var OLMASI, işlemenin HÂLÂ
  izinli olduğu anlamına GELMEZ. Politika claim SONRASINDA kapanırsa: artifact/imza
  kalıcılaştırılmaz, güvenli biçimde yeniden bekleme durumuna alınır (mevcut
  `TryFailAsync` + retry gecikmesi altyapısı yeniden kullanılır), ham hata loglanmaz.
- Karar kaydı hiç YOKSA (karar-öncesi/legacy mesaj), geriye dönük uyumluluk için işlem izinli
  sayılır — bkz. "Legacy Kayıt Politikası".

## Signing Backfill Kuralları

`EBelgeSigningBackfillService`, yalnız kendisine ait immutable kararın `YerelImzaOlustur=true`
olduğu kayıtlar için `UblImzala` mesajı oluşturur. Karar kaydı OLMAYAN (legacy) veya yöntemi
imza gerektirmeyen (`GibPortal`/`Kullanilmayacak`/desteklenmeyen `OzelEntegrator`/`DogrudanGib`)
kayıtlar backfill'e ASLA dahil edilmez.

## Politika Değişiklik Kuralları

`EBelgeKurumPolitikaYonetimServisi.GuncelleAsync`:

- **Aktif → Pasif her zaman izinlidir** (acil kill switch) — pending iş olsa bile.
- **Yöntem değişimi**, kurumun devam eden (non-terminal) `EBelgeKaydi`/outbox işi/aktif lease'i
  varken ENGELLENİR (`EBELGE_KURUM_POLICY_CHANGE_BLOCKED`).
- **Pasif → Aktif** geçişi için: yöntem operasyonel olmalı, aktivasyon tarihi global tarihten
  önce OLAMAZ, yönteme özgü zorunlu alanlar dolu olmalı (`HariciMuhasebeSistemi` →
  `HariciSistemKodu` zorunlu; `Kullanilmayacak`/`GibPortal` → boş olmalı).
- Her güncelleme optimistic concurrency (`RowVersion`) ile korunur — çakışma güvenli bir 409
  (`EBELGE_KURUM_POLICY_CONCURRENCY_CONFLICT`) döner.

## Kill Switch Davranışı

Bir kurum politikasının `AktifMi=false`'a çekilmesi ANINDA etkilidir: yeni satışlar için
`DegerlendirAsync` artık `PolitikaPasif` döner (fail-closed, satış e-belge OLMADAN devam
etmez — açıkça hata fırlatılır), worker savunma katmanı devam eden işleri güvenli biçimde
bekletir. Kill switch, global kapıyı ETKİLEMEZ — yalnız o kurumu durdurur.

## Tenant İzolasyonu

`Kurum → Politika`, `SatisBelgesi → Karar`, `Karar → Politika`, `Karar → EBelgeKaydi` ilişkilerinin
TÜMÜ composite FK (`(...Id, KurumId)`) ile DB düzeyinde korunur — uygulama düzeyi kontrol TEK
BAŞINA yeterli sayılmaz. `IEBelgeKurumPolitikaServisi.DegerlendirAsync(kurumId, ...)` her zaman
kurum bazlı sorgulanır; bir kurumun politikası başka bir kurumun kararını ASLA etkilemez (bkz.
`InstitutionPolicyTenantIsolation` kritik invariant'ı).

## Audit

Her anlamlı politika değişikliği `KurumEBelgePolitikaRevizyonu` tablosuna (immutable) yazılır —
eski/yeni yöntem, eski/yeni aktiflik, eski/yeni sürüm, değişiklik nedeni (zorunlu, sınırlı
uzunluklu). Actor bilgisi (`CreatedBy`) mevcut `StysAppDbContext.ApplyAuditInfo` mekanizmasıyla
authenticated current user'dan OTOMATİK gelir — request body'den ALINMAZ. VKN/TCKN/token/
parola/sertifika/endpoint-secret/müşteri bilgisi audit kaydına ASLA yazılmaz.

## API

`KurumEBelgePolitikasiController` (`ui/kurumlar/{kurumId}/e-belge-politikasi`), mevcut
`KurumController` yetkilendirme deseniyle (`ICurrentTenantAccessor.IsSuperAdmin/IsKurumAdmin/
GetCurrentKurumId`, mevcut `StructurePermissions.MuhasebeSatisBelgeleriYonetimi` yetkisi) AYNI
yaklaşımı kullanır — yeni bir rol/izin İCAT EDİLMEDİ:

| Endpoint | Açıklama |
|---|---|
| `GET .../e-belge-politikasi` | Mevcut politikayı DTO olarak döner (entity DOĞRUDAN dönmez) |
| `PUT .../e-belge-politikasi` | Politikayı oluşturur/günceller (RowVersion zorunlu) |
| `GET .../e-belge-politikasi/revizyonlar` | Audit geçmişini döner |

Kullanıcı yalnız kendi yetkili olduğu kurumun politikasını görebilir/değiştirebilir; cross-tenant
GET/PUT 403 ile reddedilir. Response DTO'ları VKN/kurum kimlik detayı İÇERMEZ.

## Migration

Migration `KURUM_EBELGE_POLITIKALARI`, `SATIS_BELGESI_EBELGE_KARARLARI`,
`KURUM_EBELGE_POLITIKA_REVIZYONLARI` tablolarını (PK/FK/tenant-aware unique index/RowVersion/enum
check constraint/max length/aktivasyon-tarihi kısıtı/silme davranışı ile) oluşturur. Migration
**hiçbir kurum için aktif politika seed ETMEZ**, mevcut e-belge kayıtlarına `DogrudanGib`
ATAMAZ, mevcut outbox mesajlarını yeniden YÖNLENDİRMEZ, mevcut SignedReady artefaktlarına
DOKUNMAZ, UBL/XML ÜRETMEZ.

## Legacy Kayıt Politikası

Faz 2B.10 ÖNCESİ oluşturulmuş `EBelgeKaydi`/outbox kayıtlarının bir `SatisBelgesiEBelgeKarari`
karşılığı YOKTUR. Bu kayıtlar:

- Worker savunma katmanında (`IslemHalaIzinliMiAsync`) geriye dönük uyumluluk için "izinli"
  sayılır (mevcut akışları bozmamak için) — ama bu, bu kayıtların OTOMATİK olarak `DogrudanGib`
  varsayıldığı ANLAMINA GELMEZ.
- Signing backfill'e ASLA dahil edilmez (karar YOKSA `YerelImzaOlustur=true` koşulu hiçbir zaman
  sağlanmaz).
- Gerçek production'da bu kayıtlar için açık bir backfill/migrasyon kararı GEREKİR — bu, Faz
  2B.10 kapsamı DIŞINDA, ayrı bir operasyonel adım olarak ele alınmalıdır.

## Logging ve Metrics

Güvenli log alanları: `KurumId`, `SatisBelgesiId`, `PolitikaId`, `PolitikaSurumu`,
`EntegrasyonYontemi`, `KararNedeni`. Kurum adı/VKN/TCKN/müşteri adı/adres/UBL XML/harici
kimlik bilgisi/sertifika/token ASLA LOGLANMAZ.

Önerilen metrikler (düşük kardinaliteli etiketlerle — `KurumId` bir metrik etiketi OLMAZ):

- `stys_ebelge_policy_decisions_total{method, decision_reason, result}`
- `stys_ebelge_policy_blocked_total{method, decision_reason}`
- `stys_ebelge_policy_changes_total{method, result}`

## Test Kapsamı

Faz 2B.9.1 profillerine (fast/integration/nightly/release) eklenen yeni testler:

- **Unit**: yöntem yetenek matrisi (`EBelgeKurumPolitikaYetenekMatrisiTests`), worker savunma
  katmanı (claim sonrası politika bloklu senaryo — `EBelgeOutboxMesajIslemeServiceTests`).
- **SqlIntegration**: karar servisi fail-closed matrisi + tenant izolasyonu
  (`EBelgeKurumPolitikaServisiIntegrationTests`), yönetim servisi concurrency/pending-block/kill-
  switch/audit (`EBelgeKurumPolitikaYonetimServisiIntegrationTests`), gerçek satış akışı per-yöntem
  davranışı (`SatisBelgesiEBelgeKarariSaleFlowIntegrationTests`), signing backfill exclusion
  (`EBelgeSigningBackfillServiceIntegrationTests`), API yetkilendirme/DTO/concurrency
  (`KurumEBelgePolitikasiControllerIntegrationTests`).

Yeni kritik invariant'lar (`EBelgeCriticalInvariantManifest`): `InstitutionPolicyFailClosed`
(kurum politikası tam aktif olsa bile global kapı kapalıyken karar HER ZAMAN fail-closed),
`InstitutionPolicyTenantIsolation` (kurum A politikası kurum B kararına sızmaz — hem servis hem
DB katmanında test edilir), `PortalRouteNeverSignsLocally` (GibPortal ASLA yerel imza mesajı
oluşturmaz).

## Kurum Süreç Analiz Şablonu

Kurum bazlı bilgi toplama için bkz. `docs/e-belge-kurum-surec-analizi-sablonu.md` — bu şablon
yalnız veri toplar, hukuki/mali karar VERMEZ.

## Sonraki Aşamalar

- `HariciMuhasebeSistemi` için gerçek dış sistem adaptörü (bu fazda YOK — yalnız karar kaydı).
- `OzelEntegrator`/`DogrudanGib` için gerçek adaptör/HSM/mali mühür entegrasyonu.
- Legacy (Faz 2B.10 öncesi) `EBelgeKaydi` kayıtları için açık bir backfill/migrasyon kararı.
- GİB Portal otomatik gönderim (bu fazda yalnız yerel unsigned UBL üretimi var, gönderim manuel).
