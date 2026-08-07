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

## Faz 2B.10.1: Claim, Kill-Switch ve Idempotency Sertleştirmesi

Faz 2B.10, kurum politikasını satış akışının (FaturaKesAsync) ve artifact/imza commit-öncesi
kontrol noktalarının önüne kurdu, ama **outbox claim SQL'i** ile **worker'ın karar-öncesi (legacy)
kayıtlara verdiği geriye-dönük-uyumluluk fail-open yanıtı** bu sertleştirmenin DIŞINDA kalmıştı.
Faz 2B.10.1 bu iki açığı VE üç ek üretim davranış açığını kapatır — kurum politika veri modeli
veya entegrasyon yöntemleri DEĞİŞMEDİ.

### Claim eligibility SQL/transaction yaklaşımı

`EBelgeOutboxClaimLeaseService`'in raw SQL'i artık `SatisBelgesiEBelgeKararlari` VE
`KurumEBelgePolitikalari`'na `INNER JOIN` yapar — bir mesaj YALNIZ (a) kendi immutable kararı
bulunuyorsa, (b) kurumun GÜNCEL politikası aktifse VE kararla AYNI yöntemdeyse, (c) kurumun
aktivasyon tarihi (Türkiye yerel, sabit UTC+3 ofsetiyle hesaplanan `@BuguneTrTarih`) geçmişse VE
(d) kararın KENDİ snapshot alanı (iş türüne göre `YerelSnapshotOlustur`+`YerelUnsignedUblOlustur`
veya `YerelImzaOlustur`) true ise ADAY olabilir. Yöntem → yetenek matrisi SQL'de İKİNCİ KEZ
hard-code EDİLMEZ — otoriter iş yeteneği immutable karardan, güncel politika İSE yalnız aktiflik/
yöntem-uyumu/aktivasyon-tarihi İÇİN kullanılır. Uygunsuz adaylar `WHERE`/`JOIN` içinde tamamen
elenir (post-filter DEĞİL) — bu yüzden `TOP (1)` doğal olarak SIRADAKİ uygun mesajı seçer, bloklu
bir ilk aday sonraki mesajları AÇLIĞA (starvation) sürüklemez. Mevcut `UPDLOCK/READPAST/ROWLOCK`
atomikliği VE çoklu-worker güvenliği DEĞİŞMEDEN korunur; politika/karar JOIN'leri `READPAST` ile
okunur (eşzamanlı bir politika güncellemesiyle KİLİT ÇEKİŞMESİNE girmez, yalnız o turda ADAY
DIŞI bırakılır).

### Pasif politikada attempt tüketilmeme garantisi

Politika claim ANINDA zaten pasif/uyumsuzsa mesaj hiç ADAY olmadığından `DenemeSayisi` ARTMAZ.
Politika claim SONRASINDA (worker render/imza ile MEŞGULKEN) kapatılırsa, yeni
`IEBelgeOutboxLeaseTransitionService.TryReleasePolicyBlockedAsync` mesajı `Durum=Bekliyor`'a
döndürür VE claim'de artırılan denemeyi 1 AZALTIR (0'ın altına DÜŞMEZ) — politika yarışı nedeniyle
tüketilen bir deneme, GERÇEK bir işleme denemesi SAYILMAZ.

### Claim sonrası kill-switch yarışı

Dört kesin kontrol noktası: (1) claim ÖNCESİ (claim SQL'in KENDİSİ), (2) handler başlamadan ÖNCE
(`EBelgeOutboxMesajIslemeService.IsleAsync`, mevcut savunma katmanı — ARTIK zengin
`EBelgeIslemPolitikaUygunlukSonucu` kullanır), (3) pahalı render/imza işleminden SONRA (yeni),
(4) atomik DB commit'inden HEMEN ÖNCE (yeni, (3) ile AYNI kontrol noktası — render/imza zaten
transaction DIŞINDA çalıştığından, commit-öncesi kontrol doğal olarak "işlem sonrası" kontrolü de
kapsar). Politika (2)/(3)-(4)'te bloklarsa: `TryReleasePolicyBlockedAsync` KULLANILIR (genel
`TryFailAsync`/Durum=Hata DEĞİL) — bu, kill switch'in NORMAL bir teknik hata OLMADIĞINI, retry
churn/alarm gürültüsü ÜRETMEMESİ gerektiğini yansıtır.

### Artifact commit öncesi politika kontrolü

`EBelgeArtefaktOlusturmaService.DenemeBasariAtomikAsync`, lease ownership doğrulandıktan SONRA,
artifact/EBelgeKaydi DEĞİŞTİRİLMEDEN ÖNCE, AYNI açık transaction içinde
`DegerlendirIslemUygunlugunuAsync` ile TEKRAR doğrular. Uygun değilse: `TryReleasePolicyBlockedAsync`
AYNI transaction içinde çağrılır ve commit edilir (yeni `AtomikPolitikaBloklu` sonuç türü) — artifact
insert EDİLMEZ, `EBelgeKaydi.Durum` DEĞİŞMEZ, `TryCompleteJobAsync` HİÇ çağrılmaz. Politika okuması
İLE release yazması AYNI transaction/bağlantı üzerinde olduğundan yeni bir TOCTOU penceresi AÇILMAZ.

### SignedReady commit öncesi politika kontrolü

`EBelgeUblImzalamaService`'e YENİ bir `IEBelgeKurumPolitikaServisi` bağımlılığı eklendi. Hem YENİ
imza yolunda (`DenemeYeniSignedInsertAtomikAsync`) hem idempotent/rakip-satır yolunda
(`IslemMevcutSignedAsync`), ownership doğrulamasından SONRA, `SignedReady` artefaktı
YAZILMADAN/`EBelgeKaydi.Durum` İLERLETİLMEDEN ÖNCE AYNI kontrol uygulanır. Politika imzalama
SIRASINDA kapatılırsa: SignedReady YAZILMAZ, imza sonucu (bytes/sertifika/algoritma bilgisi)
DISCARD edilir — `SonHataMesaji`'na yalnız SABİT, güvenli bir işaret yazılır, imza içeriği/
sertifika ASLA loglanmaz/saklanmaz.

### Legacy kararların fail-closed davranışı

`IEBelgeKurumPolitikaServisi.IslemHalaIzinliMiAsync` (bool, karar-yoksa-`true`) KALDIRILDI; yerine
zengin `DegerlendirIslemUygunlugunuAsync` (`EBelgeIslemPolitikaUygunlukSonucu` +
`EBelgeIslemPolitikaUygunlukNedeni`: `Uygun`/`KararBulunamadi`/`PolitikaBulunamadi`/`PolitikaPasif`/
`YontemDegisti`/`AktivasyonTarihiGelmedi`/`ImmutableYetenekYok`/`GuncelYontemDesteklenmiyor`/
`TenantUyusmazligi`) geldi. Karar kaydı hiç YOKSA sonuç ARTIK `KararBulunamadi` (fail-closed) —
ÖNCEKİ "geriye dönük uyumluluk için true" davranışı KALDIRILDI. `FaturaKesAsync`'in idempotent
tekrar dalı da AYNI ilkeyi izler: `FaturalamaDurumu=Kesildi` olan ama karşılık gelen
`SatisBelgesiEBelgeKarari` bulunamayan bir belge (Faz 2B.10 ÖNCESİ kesilmiş olabilir)
`EBelgeKurumPolitikaKararBulunamadiException` (`EBELGE_KURUM_POLICY_DECISION_NOT_FOUND`) fırlatır —
otomatik yorumlama/varsayım YAPILMAZ. Mevcut legacy kayıtların ele alınışı: **manuel inceleme →
kurum ve satış bazında yöntem kararı → kontrollü backfill** (bu turda otomatik legacy backfill
YAZILMADI).

### Pipeline gerekmeyen yöntemlerde UBL doğrulamalarının atlanması

`SatisBelgesiService.FaturaKesAsync` akışı YENİDEN sıralandı: belge/transaction doğrulamaları →
global activation/cutover (`EnsureCutoverTarihGecerli`, DEĞİŞMEDİ) → kurum politikası
(`DegerlendirAsync`/`EnsurePolitikaEngelDegil`) → **yalnız `politikaKarari.Yetenekler.
YerelSnapshotOlustur=true` ise** UBL hazırlığı (`EnsureUblHazirlikKaynaklari`, cari kart e-Fatura/
e-Arşiv bayrağı kontrolü, `ResolveEBelgeKanali`, `_ublPreCutValidator.Validate`). `Kullanilmayacak`/
`HariciMuhasebeSistemi`/henüz aktif olmayan global kapı durumlarında bu alanların DOLU olması ARTIK
ZORUNLU DEĞİLDİR — satış, e-belgeyle İLGİSİZ UBL kısıtlarıyla REDDEDİLMEZ. `GibPortal` İÇİN
davranış DEĞİŞMEDİ (UBL alanları/kanal çözümü hâlâ zorunlu).

### Yöntem-aware idempotency

`FaturaKesAsync`'in idempotent-tekrar dalı artık immutable `SatisBelgesiEBelgeKarari` üzerinden
dallanır: `YerelSnapshotOlustur=false` olan kararlarda (`Kullanilmayacak`/`HariciMuhasebeSistemi`/
global henüz aktif değilken alınan kararlar) `EBelgeKaydi` bulunMAMASI beklenen durumdur — ikinci
`FaturaKesAsync` çağrısı artık "EBelgeKaydi bulunamadı" veri-tutarsızlığı hatası YERİNE mevcut
sonucu idempotent olarak döner, yeni sayaç TÜKETMEZ, ikinci karar OLUŞTURMAZ.
`YerelSnapshotOlustur=true` olan kararlarda (`GibPortal`) mevcut EBelgeKaydi/snapshot/outbox
tutarlılık kontrolleri AYNEN korunur.

### Politika sürümü karar yarışı

İmmutable karar PERSIST edilmeden ÖNCE, kullanılan politika satırının (varsa) `DegerlendirAsync`'in
kullandığı sürümle HÂLÂ AYNI olduğu doğrulanır. **Faz 2B.10.1'deki ilk uygulama** yalnız
`PolitikaSurumu` sütununu `AsNoTracking()` ile unlocked yeniden okuyordu — bu, "aynı transaction
içinde" olsa bile bir SERİLEŞTİRME GARANTİSİ DEĞİLDİR (bkz. aşağıdaki Faz 2B.10.2 bölümü, "Neden
READ COMMITTED SELECT yetersizdir"). **Faz 2B.10.2** bu kontrolü, satır düzeyinde gerçek bir kilitle
(`IEBelgeKurumPolitikaTransactionGuard`) değiştirir — güncel davranış için bkz. "Politika versiyonu
serileştirmesi" alt bölümü. Uyuşmazlıkta AYNI `EBelgeKurumPolitikaKararCakismasiException`
(`EBELGE_KURUM_POLICY_DECISION_CONFLICT`) fırlatılır — TÜM satış kesim işlemi (resmî numara/sayaç
dahil) rollback olur, eski bir politika sürümüne göre karar YAZILMAZ.

### Yeni kritik invariant'lar

`InactivePolicyNeverClaims` (pasif/uyumsuz politika mesajları claim edilmez),
`PolicyKillSwitchPreventsCommit` (kill switch, artifact/SignedReady commit'ini engeller),
`NonLocalRouteIsIdempotent` (yerel pipeline gerektirmeyen yöntemlerde tekrar kesim idempotenttir),
`LegacyDecisionNeverProcesses` (karar-öncesi/legacy kayıtlar hiçbir aşamada işlenmez).

## Faz 2B.10.2: Policy Commit Serialization ve Signing Gate Sertleştirmesi

Faz 2B.10/2B.10.1, kurum politikasını commit-öncesi bir kontrol noktası olarak kurdu, ama bu
kontroller HER YERDE **unlocked** bir SELECT'e dayanıyordu — "aynı transaction içinde okundu"
olması bile bir serileştirme garantisi VERMEZ. Faz 2B.10.2, mimariyi YENİDEN TASARLAMADAN
(architecture DEĞİŞMEDİ), yalnız iki somut TOCTOU (time-of-check-to-time-of-use) açığını kapatır:
(1) politika kontrolü İLE artifact/SignedReady/satış-kararı commit'i ARASINDAKİ yarış penceresi,
(2) kuyruğa alınmış bir `UblImzala` mesajının, global signing gate KAPANDIKTAN SONRA bile
imzalanabilmesi.

### Neden READ COMMITTED SELECT yetersizdir

SQL Server'ın varsayılan izolasyon seviyesi (READ COMMITTED) altında bir `SELECT` (AsNoTracking
dahil), okuduğu satırı yalnız okuma ANI için kilitler (RCSI açıksa hiç kilitlemez) — okuma
tamamlanır tamamlanmaz satır serbest kalır. Bu, "aynı EF Core transaction'ı içinde" çalıştırılan
bir SELECT için BİLE geçerlidir: transaction açık olması, okunan satırın transaction SONUNA kadar
DEĞİŞMEYECEĞİNİ garanti ETMEZ. Sonuç: bir worker/satış akışı politikayı "aktif" olarak okuyup
render/imza gibi pahalı bir iş yaptıktan SONRA, başka bir oturum satırı deaktive edip commit
edebilir — worker bunu ASLA GÖRMEDEN eski politikaya göre artifact/SignedReady/karar YAZABİLİR.
Bu, Faz 2B.10.1'in "aynı transaction içinde yeniden oku" deseninin (`PolitikaSurumu` kontrolü)
KENDİSİNİN de bu açığa sahip olduğu anlamına gelir.

### Politika satırı transaction-scope kilidi

`IEBelgeKurumPolitikaTransactionGuard.KilitleVeOkuAsync(kurumId, ct)` — `KurumEBelgePolitikalari`
satırını `WITH (UPDLOCK, HOLDLOCK, ROWLOCK)` ile okur (`EBelgeKurumPolitikaTransactionGuard`, ambient
EF Core transaction'ına `Database.CurrentTransaction`/`GetDbConnection()` üzerinden katılır — bkz.
outbox lease servislerinin AYNI ham-SQL-ambient-transaction deseni). `HOLDLOCK`, normalde ifade
kapsamlı olan update kilidini transaction COMMIT/ROLLBACK edilene kadar TUTULAN bir kilide
dönüştürür; `KurumId` üzerindeki UNIQUE INDEX ile birleşince, satır HİÇ YOKSA bile aynı `KurumId`
ile YENİ bir satır eklenmesine karşı (phantom insert) key-range koruması sağlar. Bilinçli olarak
TÜM transaction `SERIALIZABLE`'a YÜKSELTİLMEZ — yalnız politika satırı üzerinde HEDEFLİ bir satır/
key-range kilidi kullanılır.

Bu kilit üç yerde kullanılır: `EBelgeArtefaktOlusturmaService.DenemeBasariAtomikAsync` (artifact
commit'inden önce), `EBelgeUblImzalamaService`'in HER İKİ SignedReady commit yolu
(`IslemMevcutSignedAsync`/`DenemeYeniSignedInsertAtomikAsync`), `SatisBelgesiService.FaturaKesAsync`
(karar persist edilmeden önce). Uygunluk ALGORİTMASI (`IEBelgeKurumPolitikaServisi`) iki kez
YAZILMAZ — `DegerlendirIslemUygunlugunuAsync` (unlocked, worker'ın İLK savunma katmanı/claim
sonrası kontrolü) ve `DegerlendirIslemUygunlugunuKilitliAsync` (kilitlenmiş bir anlık görüntü
İLE, commit-öncesi SON kontrol), AYNI özel çekirdek metodu (`DegerlendirIslemUygunlugunuCoreAsync`)
paylaşır — yalnız politikanın NASIL okunduğu (`politikaGetir` delegesi) farklıdır.

### Linearization point

İki eşzamanlı işlem (worker'ın commit-öncesi kontrolü VE bir kill switch `GuncelleAsync` çağrısı)
AYNI politika satırını hedeflediğinde, hangisi satır kilidini ÖNCE kazanırsa O taraf "kazanır" —
bu, sistemin **linearization point**'idir. Her iki sıralama da DOĞRU kabul edilir:

- **Kill switch önce kazanır**: worker'ın kilit isteği, kill switch'in transaction'ı commit
  edene kadar BLOKE olur; worker sonunda GÜNCEL (pasif) politikayı görür, artifact/SignedReady/
  karar YAZMAZ (`AtomikPolitikaBloklu`).
- **Worker önce kazanır**: kill switch'in `UPDATE`'i (SELECT'i DEĞİL — SELECT bir S kilidi
  ister, U kilidiyle UYUMLUDUR; UPDATE bir X kilidi ister, U kilidiyle ÇAKIŞIR), worker COMMIT
  edene kadar BLOKE olur; worker eski (o anki) politikaya göre commit eder, kill switch SONRA
  tamamlanır. Bu KABUL EDİLEBİLİR bir sonuçtur — kill switch çağrısı HENÜZ başarıyla DÖNMEMİŞTİR.

Garanti edilen tek şey: **bir deaktivasyon çağrısı başarıyla döndükten SONRA, artık eski politikaya
göre HİÇBİR yeni artifact/SignedReady/karar commit edilemez.** Bu, GERÇEK iki-transaction testlerle
kanıtlanır (bkz. Test Kapsamı bölümü) — sıralı ("önce deaktive et, SONRA servisi çağır") simülasyon
BU garantiyi KANITLAMAZ.

### Kilit sırası

Deadlock'tan kaçınmak için TÜM akışlar AYNI kısmi sırayı izler:

- **Worker (outbox)**: Outbox sahiplik satırı → Kurum politika satırı → EBelgeKaydi/artifact yazımları.
- **Satış (FaturaKesAsync)**: SatisBelgesi satırı → Kurum politika satırı → sayaç/karar/EBelgeKaydi yazımları.
- **Kill switch (`EBelgeKurumPolitikaYonetimServisi.GuncelleAsync`)**: yalnız kurum politika
  satırı → commit. Outbox/SatisBelgesi/sayaç satırlarından HİÇBİRİNİ kilitlemez — kısa bir
  "politika satırı güncelle → commit" işlemi olarak KALIR (bu, worker/satış akışlarıyla asla
  TERS sırada kilit istemediği anlamına gelir — deadlock imkânsızdır).

### Global signing gate — gerçek bir commit kapısı

Faz 2B.10.1'e kadar `IEBelgeSigningActivationGate`, YALNIZ mesaj-OLUŞTURMA anında
(`EBelgeArtefaktOlusturmaService.ImzalamaMesajiOlusturulmaliMiAsync`) kontrol ediliyordu —
`EBelgeUblImzalamaService`'in gate'e HİÇ bağımlılığı yoktu. Bu, gate KAPANDIKTAN SONRA, ÖNCEDEN
kuyruğa alınmış bir `UblImzala` mesajının YİNE DE imzalanabileceği anlamına geliyordu. Faz 2B.10.2
bunu iki katmanlı bir kontrole dönüştürür — `CanSignNow()` (aynı `Enabled`/`NotBeforeLocalDate`/
Europe/Istanbul/`TimeProvider` algoritmasını `ShouldCreateSigningMessage()` İLE PAYLAŞAN, tek bir
özel `Degerlendir()` çekirdeği üzerinden — algoritma İKİ YERDE YENİDEN YAZILMAZ):

1. **Handler başlangıcı** (`EBelgeUblImzalamaService.ImzalaAsync`'in EN BAŞI) — gate kapalıysa,
   imza/render işine HİÇ GİRİLMEDEN mesaj `AtomikPolitikaBloklu` ile sonuçlandırılır (outbox
   Bekliyor kalır, attempt iade edilir, retry churn OLMAZ).
2. **Commit-öncesi ikinci kontrol** — imza operasyonu (transaction DIŞI) tamamlandıktan SONRA,
   SignedReady yazılmadan HEMEN ÖNCE, kurum politika kilidiyle AYNI kısa commit transaction'ı
   içinde gate TEKRAR kontrol edilir. Gate imza SIRASINDA kapandıysa: SignedReady YAZILMAZ,
   EBelgeKaydi.Durum İLERLEMEZ, imza sonucu (private key/imza bytes dahil) DISCARD edilir.

Global signing gate, kurum politikasından BAĞIMSIZDIR — bir mesajın imzalanabilmesi İÇİN
`Kurum politikası aktif AND immutable karar YerelImzaOlustur=true AND EBelgeSigning gate aktif`
koşullarının ÜÇÜ BİRDEN sağlanmalıdır. Gate yeniden açıldığında, önceden bloklanmış mesaj
(politika kill switch'iyle AYNI mekanizma — outbox Bekliyor durumunda kaldığından) claim filtresi
tarafından YENİDEN seçilebilir.

### Politika versiyonu serileştirmesi (satış akışı)

`SatisBelgesiService.FaturaKesAsync`, `DegerlendirAsync`'in (unlocked) kararını SatisBelgesi
satır kilidinden SONRA ama sayaç satırı kilitlenmeden ÖNCE (bkz. yukarıdaki kilit sırası) transaction
guard İLE doğrular: kilitli anlık görüntünün `Id`/`KurumId`/`PolitikaSurumu`/`AktifMi`/
`EntegrasyonYontemi`/`AktivasyonYerelTarihi` alanlarının TAMAMI, `DegerlendirAsync`'in döndürdüğü
kararla eşleşmelidir. Herhangi bir alan uyuşmazsa `EBelgeKurumPolitikaKararCakismasiException`
fırlatılır — TÜM satış kesim işlemi (sayaç dahil) rollback olur.

### Test Kapsamı ve yeni kritik invariant'lar

GERÇEK, örtüşen iki-transaction/iki-bağlantı testleri (sıralı simülasyon DEĞİL — bir taraf satırı
AÇIK bir transaction'da tutarken diğer taraf GERÇEKTEN blokajla karşılaşır, bu `Task.WhenAny` +
zaman aşımıyla KANITLANIR):

- `EBelgeArtefaktOlusturmaServiceIntegrationTests.GercekEszamanliKillSwitchWorkerBlokeEderVeArtefaktYazdirmaz`
- `EBelgeUblImzalamaServiceIntegrationTests.GercekEszamanliKillSwitchImzaSirasindaWorkerBlokeEderVeSignedReadyYazdirmaz`
- `EBelgeKurumPolitikaYonetimServisiIntegrationTests.WorkerPolitikaKilidiOnceAlinirsaKillSwitchGuncellemesiKilitSerbestKalanaKadarGercektenBlokeOlur`
  (ters sıralama — worker'ın kilidi ÖNCE kazanması)
- `SatisBelgesiEBelgeKarariSaleFlowIntegrationTests.PolitikaSurumuKararDegerlendirmesiIlePersistArasindaDegisirseTumSatisKesimiRollbackOlur`
  (iki GERÇEK `DbContext`, görev md.12 deseninde)

Signing gate testleri (`EBelgeUblImzalamaServiceIntegrationTests`):
`SigningGateKapaliykenKuyruktakiMesajHicIslenmeyeBaslamazVeSignedReadyYazilmaz`,
`SigningGateImzaSirasindaKapanirsaSignedReadyCommitEdilmezImzaSonucuDiscardEdilir` (toggle test
double ile deterministik gate geçişi), `SigningGateTekrarAcilincaKuyruktakiMesajYenidenIslenebilirVeSignedReadyUretilir`;
`EBelgeSigningActivationGateTests.CanSignNowVeShouldCreateSigningMessageHerZamanAyniSonucuDoner`
(iki gate metodunun AYNI çekirdeği paylaştığının kanıtı).

Yeni kritik invariant'lar (`EBelgeCriticalInvariantManifest`): `SigningGatePreventsQueuedSigning`
(kuyruğa alınmış imza mesajları, gate kapalıyken/imza sırasında kapanırken commit edilemez),
`PolicyDecisionVersionIsSerialized` (satış kararı, eski bir politika sürümüne göre commit
edilemez). `PolicyKillSwitchPreventsCommit` (Faz 2B.10.1) artık GERÇEK iki-transaction testlerle
de kanıtlanır (önceki sıralı testler KORUNDU, güçlendirici testler EKLENDİ).

## Faz 2B.10.3: Signing Gate Claim Eligibility ve Churn Engelleme

Faz 2B.10.2, global signing gate'i GERÇEK bir commit-öncesi kapı yaptı (handler başında + SignedReady
commit'inden hemen önce) - ama bu iki kontrol noktasının İKİSİ de claim SONRASI çalışıyordu. Sonuç:
gate kapalıyken bir `UblImzala` mesajı YİNE claim edilip HEMEN "Bekliyor"a bırakılıyor, tekrar claim
edilebiliyordu - `DenemeSayisi` tüketilmese bile bu, worker/SQL churn üretiyordu. Faz 2B.10.3, mimariyi
DEĞİŞTİRMEDEN bu tek problemi kapatır: **signing gate ARTIK claim eligibility'nin KENDİSİNİN bir
parçası** - gate kapalıyken bir `UblImzala` mesajı hiç ADAY OLMAZ.

### Signing gate claim eligibility

`EBelgeOutboxClaimLeaseService`, `IEBelgeSigningActivationGate` bağımlılığı alır ve `TryClaimNextAsync`'in
BAŞINDA `CanSignNow()`'ı BİR KEZ değerlendirir - bu type-safe bool karar, claim SQL'ine `@SigningAllowed`
BIT parametresi olarak geçirilir: `AND (outbox.IsTuru <> 2 OR @SigningAllowed = 1)`. `Enabled`/tarih/
timezone/config-parsing ALGORİTMASI SQL'e TAŞINMAZ - tek kaynak-of-truth `IEBelgeSigningActivationGate`
olarak KALIR; SQL yalnız caller'ın ZATEN hesapladığı kararı KULLANIR. Bu, mevcut politika/yöntem/
aktivasyon-tarihi uygunluğunun (bkz. Faz 2B.10.1) YERİNE GEÇEN değil, ONA EKLENEN bir AND katmanıdır -
`ArtefaktOlustur` mesajları bundan HİÇ ETKİLENMEZ.

### Üç katmanlı signing gate savunması

Claim eligibility TEK BAŞINA yeterli DEĞİLDİR - claim ANINDA gate açık olabilir ama imzalama SIRASINDA
kapanabilir (aynı TOCTOU açığı, farklı bir pencere). Bu yüzden ÜÇ AYRI savunma katmanı VARDIR:

1. **Claim öncesi** (`EBelgeOutboxClaimLeaseService`) - gate kapalıysa mesaj hiç claim edilmez.
2. **Handler/imza öncesi** (`EBelgeUblImzalamaService.ImzalaAsync`'in EN BAŞI) - Faz 2B.10.2'den
   KORUNDU; imza/render işine hiç girilmeden erken çıkış.
3. **SignedReady commit öncesi** (Faz 2B.10.2'den KORUNDU) - imza operasyonu tamamlandıktan SONRA,
   SignedReady yazılmadan HEMEN ÖNCE TEKRAR kontrol.

Üçü de AYNI `IEBelgeSigningActivationGate.CanSignNow()` çekirdeğini kullanır - HİÇBİRİ algoritmayı
kendi başına YENİDEN YAZMAZ.

### Gate kapalı queue starvation davranışı

Signing gate yalnız `UblImzala` iş türünü hedefler - kuyrukta ÖNCE gelen bloklu bir signing mesajı,
SONRAKİ uygun bir `ArtefaktOlustur` mesajının claim edilmesini ENGELLEMEZ (claim SQL'in `WHERE`
yüklemi ineligible satırları CTE'den TAMAMEN eler, `TOP (1) ... ORDER BY` sıradaki UYGUN adaya geçer).
Worker'da AYRICA bir "signing kapalıysa TÜM polling turunu durdur" dalı YOKTUR - böyle bir dal
starvation'ı KENDİSİ ÜRETİRDİ.

### Gate kapalı attempt/lease garantisi

Gate kapalıyken `TryClaimNextAsync` bir `UblImzala` mesajı İÇİN: satır GÜNCELLENMEZ (`Durum`/
`KilitToken`/`KilitBitisZamaniUtc`/`DenemeSayisi` HİÇBİRİ değişmez), `null` döner. Aynı mesaj İÇİN art
arda/aynı poll turunda tekrarlanan claim çağrıları HEP `null` döner ve DB durumu HER SEFERİNDE AYNI
kalır - gerçek churn (SQL yazımı/lease/log gürültüsü) ORTADAN KALKAR (Faz 2B.10.2'nin "claim SONRASI
hemen Bekliyor'a bırak" davranışının AKSİNE).

### Transaction guard fail-fast sözleşmesi

`EBelgeKurumPolitikaTransactionGuard.KilitleVeOkuAsync` ARTIK açık bir ambient transaction OLMADAN
çağrılırsa `InvalidOperationException` fırlatır (Faz 2B.10.2'de sessizce, transaction OLMADAN da
çalışıyordu - bu durumda `HOLDLOCK`'un verdiği garanti sessizce KAYBOLUYORDU). Bu, business bir
fallback DEĞİL, bir programlama/çağıran hatasıdır - fail-closed bir DEĞER DÖNDÜRMEK yerine fail-FAST
bir exception TERCİH edilir. TÜM production çağrı noktaları (`EBelgeArtefaktOlusturmaService`,
`EBelgeUblImzalamaService`'in her iki yolu, `SatisBelgesiService.FaturaKesAsync`) zaten AÇIK bir
transaction içinde çağırır - bu değişiklik onların davranışını ETKİLEMEZ.

### `TryReleasePolicyBlockedAsync` isimlendirme notu

Bu metot ARTIK yalnız kurum politikası nedeniyle DEĞİL, global signing gate commit-öncesi kapandığında
DA (`EBelgeUblImzalamaService`'in her iki kontrol noktasında) kullanılır - outbox satırının davranışı
(terminal olmayan, Bekliyor, attempt iade, retry churn yok) İKİ senaryoda da AYNI olduğundan ayrı bir
sonuç türü/DB geçişi İCAT EDİLMEDİ. İsim "Policy" desin, semantiği ARTIK "kurum politikası VEYA global
signing gate nedeniyle non-terminal eligibility release"dir. Bu tur BÜYÜK bir rename YAPMADI (davranış
isimden daha önemlidir) - ilerideki bir fazda `TryReleaseEligibilityBlockedAsync` gibi daha genel bir
adla değiştirilmesi değerlendirilebilir.

### Test kapsamı

`EBelgeOutboxClaimLeasePolicyEligibilityIntegrationTests`'e veri-odaklı biçimde eklenen testler: gate
kapalıyken UblImzala claim edilmez/attempt değişmez/lease oluşmaz, art arda 5 claim çağrısı hep `null`
döner ve DB durumu değişmez kalır, bloklu bir signing mesajı sıradaki uygun `ArtefaktOlustur`
mesajının claim edilmesini engellemez (starvation yok), gate tekrar açılınca AYNI mesaj claim edilir,
GERÇEK `EBelgeSigningActivationGate` ile aktivasyon-tarihi-gelmemiş/geçersiz-config senaryolarında
signing claim edilmez (fail-closed), politika pasif + gate açık / politika aktif + gate kapalı
kombinasyonlarının HER İKİ filtrenin de BAĞIMSIZ çalıştığını kanıtladığı testler.
`EBelgeKurumPolitikaYonetimServisiIntegrationTests.TransactionGuardAcikTransactionOlmadanCagrilirsaFailFastExceptionFirlatir`
guard'ın fail-fast sözleşmesini doğrular; Faz 2B.10.2'nin GERÇEK serialization testleri (mevcut guard
çağrı noktalarının TÜMÜ zaten açık transaction içinde olduğundan) DEĞİŞİKLİK OLMADAN geçmeye devam
eder. `SigningGatePreventsQueuedSigning` kritik invariant'ı ARTIK yalnız "claim edilmiş mesaj
SignedReady üretmiyor" değil, DAHA GÜÇLÜ biçimde "gate kapalıyken signing mesajı hiç claim edilmiyor"
davranışını da kanıtlar.

## Faz 2B.11: E-Belge Yönetim, Readiness ve UBL Veri Tamamlama Ekranları

Faz 2B.10-2B.10.3, kurum bazlı e-belge yönlendirmesini TAMAMEN backend'de kurdu — ama kurum
yöneticilerinin politikayı görüp yönetebileceği, hazırlık durumunu anlayabileceği bir arayüz YOKTU
(politika yalnız API üzerinden yönetilebiliyordu). Faz 2B.11, mimariyi/karar mantığını
DEĞİŞTİRMEDEN, YALNIZ bir yönetim/gözlemleme katmanı ekler: yeni bir salt-okunur `readiness`
endpoint'i + servisi, ve bunu tüketen bir Angular ekranı. Business logic (yöntem yeteneği, global
kapı durumu, aktivasyon tarihi, satıcı verisi tamlığı, imzalama uygulanabilirliği) BURADA da
frontend'e SIZMAZ — tamamı backend'de hesaplanır, frontend yalnız görselleştirir. Kullanım detayları
için bkz. `docs/e-belge-yonetim-ekrani-kullanim-rehberi.md`.

### Readiness servisi — mevcut yapıların BİR ARAYA GETİRİLMESİ

Yeni `IEBelgeKurumReadinessService`/`EBelgeKurumReadinessService`
(`backend/Muhasebe/SatisBelgeleri/Services/EBelgeKurumReadinessServisi.cs`), `GET
.../e-belge-politikasi/readiness`'in tek arkasındaki serviS, mevcut
`IEBelgeYontemYetenekSaglayici`/`IEBelgeProcessingActivationGate`/`IEBelgeSigningActivationGate`/
kurum politikası okumasını BİR ARAYA GETİRİR — hiçbirinin algoritmasını İKİNCİ KEZ YAZMAZ, yeni bir
karar mantığı İCAT ETMEZ. Salt-okunur bir GET olduğundan Faz 2B.10.2'nin satır kilidi/transaction
guard mekanizmasına (yalnız commit-öncesi yarış pencereleri için gereklidir) ihtiyaç DUYMAZ —
`AsNoTracking()` ile düz okuma yeterlidir.

`KurumEBelgeReadinessDto`, kurumun e-belge hazırlığının TAM anlık görüntüsünü döner: politika
yapılandırılmış/aktif mi, yöntem operasyonel mi, global işleme kapısı durumu (metin + bool),
global minimum aktivasyon tarihi (yalnız GÖRÜNTÜLEME amaçlı — backend validasyonunun YERİNE
GEÇMEZ), yerel snapshot/unsigned-UBL/imza/otomatik-gönderim gerekliliği, satıcı ana verisi
tamlığı + eksik alan kodları, imzalama kapısının UYGULANABİLİR olup olmadığı (yalnız yerel imza
gerektiren yöntemlerde anlamlıdır) + şu an mümkün olup olmadığı, genel `islemeHazirMi` bayrağı,
blokaj nedeni kodları dizisi, ve TÜM yöntemler için (yalnız seçili olan değil) capability listesi
(`yontemler`) — bu SONUNCUSU, frontend'in "hangi yöntem seçilebilir/devre dışı" listesini
KENDİ HESAPLAMADAN, doğrudan backend'den kurabilmesi içindir.

### Güvenli kod sözlüğü — `EBelgeKurumReadinessKodlari`

Eksik satıcı alanları (`VERGI_NO`/`VERGI_DAIRESI`/`ADRES`/`ILCE`/`IL`) ve genel blokaj nedenleri,
ham alan değeri/VKN/müşteri bilgisi TAŞIMAYAN, SABİT kod string'leridir. Mevcut karar servisinin
(`EBelgeKurumPolitikaEngelliException`) ZATEN kullandığı dört kod (`NOT_CONFIGURED`/`INACTIVE`/
`BEFORE_ACTIVATION_DATE`/`METHOD_NOT_IMPLEMENTED`) BİREBİR REUSE edilir — YENİDEN İCAT EDİLMEZ; bu
turda yalnız karar servisinde karşılığı olmayan üç YENİ kod eklenir: global işleme kapısı kapalı
(`EBELGE_GLOBAL_PROCESSING_DISABLED`), satıcı ana verisi eksik
(`EBELGE_KURUM_POLICY_SELLER_DATA_INCOMPLETE`), imzalama kapısı kapalı
(`EBELGE_SIGNING_GATE_DISABLED`). Frontend bu kodları KENDİ Türkçe etiket haritasına çevirir
(`EBELGE_BLOKAJ_NEDENI_LABELS`) — backend TÜRKÇE METİN üretmez, yalnız type-safe kod.

### API ve yetkilendirme

`KurumEBelgePolitikasiController`'a eklenen TEK yeni action: `GET
.../e-belge-politikasi/readiness`. Mevcut `GET policy`/`PUT policy`/`GET revizyonlar`
sözleşmesi/yetkilendirme deseni (`EnsureCanAccessKurumAsync`) AYNEN kullanılır — yeni bir rol/izin
İCAT EDİLMEMİŞTİR, cross-tenant erişim AYNI şekilde 403 ile reddedilir.

### Frontend — E-Belge Yönetimi ekranı

`frontend/src/app/pages/muhasebe/e-belge-yonetimi/` — `/muhasebe/e-belge-yonetimi` rotası, Muhasebe
menüsü altında YENİ bir DB-driven menu kaydı (`pi pi-file-edit`, migration
`20260807000000_AddEBelgeYonetimiMenuFaz2B11` — ZATEN var olan `MuhasebeSatisBelgeleriYonetimi.Menu`
rolüne bağlanır, YENİ rol/izin İCAT EDİLMEZ). Ekran: hazırlık durumu kart grid'i (PrimeNG severity
haritalı, renk-körü DOSTU — durum metinle de ifade edilir), politika formu (aktivasyon/pasifleştirme/
yöntem-değişikliği için AYRI onay diyalogları, desteklenmeyen yöntemler disabled + "Henüz
desteklenmiyor"), revizyon geçmişi tablosu. TypeScript `EBelgeEntegrasyonYontemi` enum'u backend
enum'unun int değerleriyle BİREBİR eşleşir; capability matrix frontend'de İKİNCİ KEZ YAZILMAZ, TÜMÜ
`readiness.yontemler`'den okunur. Tarih alanları (aktivasyon tarihi) yalnız `yyyy-MM-dd` takvim
değeri olarak işlenir — UTC dönüşümü YOKTUR (off-by-one koruması). 409 concurrency yanıtında ekran
politika+readiness+revizyonları BAŞTAN yeniden yükler — eski `rowVersion` ile SESSİZCE ÜZERİNE
YAZMAZ.

### Kurum ve Cari Kart alan tamamlaması

Bu tur ayrıca iki ÖNCEDEN VAR OLAN, backend'de zaten desteklenen ama frontend modelinde eksik
taşınan alan grubunu tamamlar (yeni migration/backend mapping GEREKMEDİ — AutoMapper 1:1 property
eşlemesi zaten çalışıyordu, yalnız frontend TypeScript arayüzleri eksikti):

- **Kurum**: `vergiDairesi`/`adres`/`ilce`/`il` — Kurum Yönetimi ekranındaki Kurum Bilgileri
  sekmesi, bu alanları içeren mantıksal alt bölümlere ayrıldı (Temel Bilgiler / Mali-E-Belge
  Bilgileri / İletişim / Tenant-Giriş / Logo). Önceden bu alanlar formda hiç görünmediğinden
  düzenleme-kaydetme akışında SESSİZCE kayboluyordu — bu Faz 2B.11'in "Satıcı ana verileri"
  gereksinimi için KRİTİKTİR (readiness bu alanları kontrol eder).
- **Cari Kart**: `ad`/`soyad` — "E-Belge Bilgileri" bölümünde gerçek kişi/kurumsal ayrımı
  `SatisBelgesiService.ApplyCariSnapshot`'ın kullandığı AYNI kuralla (`CariTipi != "Musteri"` =
  kurumsal) yapılır; e-Fatura/e-Arşiv onayları AYNI bölümde toplanır.

### Kasıtlı olarak YAPILMAYANLAR

HSM/mali mühür/GİB gerçek entegrasyonu, PDF/e-posta üretimi, yeni bir entegrasyon adaptörü, yeni
rol/izin icadı, frontend'de backend capability matrix'inin ikinci bir kopyası, `EBelgeProcessing`/
`EBelgeSigning`/`EBelgeUbl` global kapılarının flip edilmesi veya `NotBeforeLocalDate` değişikliği,
herhangi bir kurumun politikasının otomatik seed edilmesi, Kurum Yönetimi/Cari Kart ekranlarının
baştan yazılması, VKN/TCKN/PII'nin readiness yanıtına/loglara eklenmesi.

## Faz 2B.11.1: E-Belge Readiness ve Frontend Authorization Sertleştirmesi

Faz 2B.11, readiness ekranını ve API'sini kurdu, ama üç kök problem KALDI: (1) readiness hesabı
`EBelgeUbl.Enabled` global feature flag'ini HİÇ değerlendirmiyordu — GİB Portal politikası aktif +
satıcı verisi tam olduğunda, UBL flag KAPALI olsa bile "Hazır" görünüyordu; (2) runtime'ın KENDİSİ
(satış akışı) bu durumda fail-closed DEĞİLDİ — V1 snapshot'a SESSİZCE geriler, yanlış artifact
tüketilebilirdi; (3) frontend bazı yöntem kurallarını (`hariciSistemKoduGerekliMi`) backend
capability'sinden OKUMAK yerine kendi içinde YENİDEN hesaplıyordu; (4) E-Belge Yönetimi ekranı
gereksiz yere `UserManagement.View`/`Manage` bağımlılığı taşıyordu ve SuperAdmin frontend'de
domain-spesifik izne SAHİP OLMAYA zorlanıyordu (backend'in `View/Manage OR SuperAdmin`
sözleşmesiyle TUTARSIZ). Bu tur küçük ve hedeflidir — Faz 2B.11 ekranlarının/mimarisinin HİÇBİRİ
yeniden yazılmadı, yalnız bu dört problem kapatıldı.

### UBL feature flag readiness'e dahil edildi

`EBelgeKurumReadinessService` artık `IOptions<EBelgeUblOptions>` bağımlılığı alır. Readiness DTO'ya
iki YENİ, güvenli (VKN/secret İÇERMEYEN) alan eklendi: `UblFeatureUygulanabilirMi` (=
`YerelUnsignedUblOlustur` — yöntemin yerel unsigned UBL GEREKTİRİP gerektirmediği) ve
`UblFeatureAktifMi` (= `EBelgeUblOptions.Enabled`'ın ham değeri). Semantik: yöntem yerel UBL
GEREKTİRMİYORSA (Kullanilmayacak/HariciMuhasebeSistemi) bu bayrağın değeri readiness'i HİÇ
ETKİLEMEZ ("Uygulanamaz"); GEREKTİRİYORSA (bugün yalnız GibPortal) VE flag KAPALIYSA, YENİ bir
güvenli kod (`EBELGE_UBL_FEATURE_DISABLED` — mevcut, runtime'da ZATEN kullanılan
`EBelgeUblFeatureDisabledException.SafeErrorCode` İLE BİREBİR AYNI, İKİNCİ bir kod İCAT
EDİLMEDİ) `BlokajNedenleri`'ne eklenir ve `IslemeHazirMi=false` olur — GİB Portal politikası aktif,
satıcı verisi tam, aktivasyon tarihi gelmiş olsa BİLE.

### Runtime'ın kendisi de fail-closed

Readiness bir GÜVENLİK KONTROLÜ DEĞİLDİR. `SatisBelgesiService.FaturaKesAsync`'e YENİ bir kontrol
eklendi (`EnsureUblFeatureAcikYerelUblGerekliyse`): politika kararı `YerelUnsignedUblOlustur=true`
DİYORSA (bugün yalnız GibPortal) ve `EBelgeUblOptions.Enabled=false` İSE, mevcut, güvenli
`EBelgeUblFeatureDisabledException` (HTTP 503) fırlatılır — YENİ bir exception İCAT EDİLMEDİ. Bu
kontrol, kurum politikası değerlendirmesinden HEMEN SONRA ama sayaç satırı henüz sorgulanmadan/
kilitlenmeden ÖNCE çalışır: resmî fatura numarası verilmez, sayaç TÜKETİLMEZ, immutable karar
YAZILMAZ, `EBelgeKaydi`/snapshot/outbox OLUŞTURULMAZ — TÜM transaction rollback olur. Non-local
yöntemler (`YerelUnsignedUblOlustur=false`) bu kontrolden HİÇ ETKİLENMEZ; satış normal akışıyla
tamamlanmaya devam eder.

Mevcut `_eBelgeUblOptions.Enabled ? CreateSnapshotV2(...) : CreateSnapshot(...)` (V1/V2 seçim)
ternary'si BİLİNÇLİ olarak DEĞİŞTİRİLMEDİ — V1 üretim yolu (legacy/non-local senaryolar için)
sistemden KALDIRILMADI. Ama YENİ guard'ın DOLAYLI bir sonucu olarak: `YerelUnsignedUblOlustur=true`
olan HERHANGİ bir politika için bu satıra ulaşıldığında `EBelgeUblOptions.Enabled` ARTIK HER ZAMAN
`true`'dur (aksi halde guard ZATEN önceden fırlatmıştır) — bu yüzden V2 üretimi bu route için
DETERMİNİSTİK hale gelir, ayrı bir kod değişikliği GEREKMEDEN.

### Frontend capability tek kaynak

E-Belge Yönetimi ekranındaki `hariciSistemKoduGerekliMi` getter'ı ÖNCEDEN
`entegrasyonYontemi === HariciMuhasebeSistemi` biçiminde component içinde YENİDEN hesaplanıyordu —
backend'in ZATEN `yontemler[].hariciSistemKoduGerekliMi` olarak döndürdüğü bir business kuralının
frontend'de İKİNCİ bir kopyası. Yeni `secilenYontemCapability` getter'ı, seçili yöntemin backend'den
gelen TAM `EBelgeYontemReadinessModel` kaydını bulur; `hariciSistemKoduGerekliMi` (ve dolaylı olarak
operasyonel/disabled, snapshot, unsigned UBL, imza, otomatik gönderim gibi TÜM capability alanları)
ARTIK yalnız BURADAN okunur — component İKİNCİ bir capability matrix OLUŞTURMAZ. Label/açıklama
metinleri (`EBELGE_YONTEM_LABELS`/`EBELGE_YONTEM_ACIKLAMALARI`) frontend'de KALIR — bunlar business
capability DEĞİLDİR, salt görüntüleme metnidir.

### UserManagement bağımsızlığı

E-Belge Yönetimi ekranının kurum bağlamı (aktif kurum adı, SuperAdmin kurum seçici) ÖNCEDEN
`KurumService.getAll()`/`getById()` kullanıyordu — backend'de İKİSİ de `UserManagement.View`
GEREKTİRİR (bkz. `KurumController`). Bu, salt e-belge Viewer/Manager izni olan ama
`UserManagement` izni OLMAYAN bir kullanıcının ekranı KULLANAMAMASINA yol açıyordu — Faz 2B.11'in
KENDİ hedefiyle (görev md.5: "muhasebe/e-belge yetkisine sahip kullanıcının UserManagement.View
gerektirmeden kullanabilmesi") ÇELİŞEN bir bağımlılıktı. Çözüm: kurum bağlamı ARTIK TEK, güvenli
`GET .../kurum/benim-kurumlarim` (`[Permission]` — yalnız kimlik doğrulama gerektirir,
`KurumController.GetMyKurumlar`) endpoint'ini kullanır; bu ZATEN tenant scope'una göre erişilebilir
kurumları döner (SuperAdmin için TÜM aktif kurumlar, normal kullanıcı için kendi erişilebilir
kurumları — `GetAccessibleKurumlarAsync`'in AYNI yolu). Aktif kurum bu listede YOKSA (teorik olarak
imkânsız ama fail-closed ele alınır) ekran "erişim yok" gösterir; SuperAdmin'in aktif kurumu YOKSA
sayfaya özgü selector `getMyKurumlar()`'dan doldurulur — YENİ bir global tenant selector İCAT
EDİLMEDİ. "Satıcı bilgilerini tamamla" kısayolu (`UserManagement.Manage OR SuperAdmin`) DEĞİŞMEDİ —
bu yalnız O BUTONUN görünürlüğünü etkiler, sayfanın KENDİSİNİ ETKİLEMEZ.

### SuperAdmin frontend/backend authorization eşleşmesi

Backend sözleşmesi HER İKİ endpoint için de OR semantiğidir: `[Permission(View, SuperAdminPermission)]`
+ `EnsureCanAccessKurumAsync`/`EnsureCanManageKurumAsync` (SuperAdmin domain-spesifik izne
GEREKSİNİM DUYMAZ). Frontend'in ÖNCEKİ `canView`/`canManage` getter'ları SuperAdmin'i de
`hasPermission('...View'/'...Manage')` kontrolüne TABİ TUTUYORDU (yanlış AND semantiği) —
domain-spesifik izni OLMAYAN "saf" bir SuperAdmin kullanıcı ekranı GÖREMİYORDU. Düzeltme:
`isSuperAdminUser()` artık HER İKİ getter'da da domain izin kontrolünden ÖNCE, koşulsuz `true`
döner — bu, sayfa render/buton etkinliği İÇİN kullanılan bir UX kontrolüdür.

> **Faz 2B.11.2 notu**: bu turda AYRICA, `/muhasebe/e-belge-yonetimi` rotasına eklenen
> `permissionOrSuperAdminGuard` adlı bir route guard'la SuperAdmin'i rota seviyesinde de "AYRI
> DEĞERLENDİRİLİR" hale getirmeye çalışılmıştı. Faz 2B.11.2'de bu yaklaşımın KENDİSİNİN, STYS'in
> DB-driven `MenuItemRoles` mimarisiyle TUTARSIZ olduğu anlaşıldı ve GERİ ALINDI — bkz. aşağıdaki
> "Faz 2B.11.2" bölümü. `canView`/`canManage` getter'larındaki SuperAdmin düzeltmesinin KENDİSİ
> (bu paragraf) DOĞRUdur ve KORUNDU.

### Kasıtlı olarak YAPILMAYANLAR

Faz 2B.11 ekranlarının/mimarisinin baştan yazılması, `EBelgeProcessing`/`EBelgeSigning`/`EBelgeUbl`
global kapılarının flip edilmesi veya `NotBeforeLocalDate` değişikliği, V1 snapshot üretim yolunun
sistemden kaldırılması, yeni bir DB migration'ı/tablo/sonuç türü icadı,
`TryReleasePolicyBlockedAsync` gibi mevcut paylaşılan mekanizmaların yeniden yazılması, test skip
etme, tüm solution test paketini çalıştırma.

## Faz 2B.11.2: DB-Driven Menu Authorization ile Route Sadeleştirmesi

Faz 2B.11.1, `/muhasebe/e-belge-yonetimi` rotasına `permissionOrSuperAdminGuard` adlı bir route
guard ekledi — backend'in `View OR SuperAdmin` sözleşmesini rota seviyesinde de yansıtmak
amacıyla. Bu, İYİ NİYETLİ ama MİMARİ OLARAK YANLIŞ bir düzeltmeydi: STYS frontend'de menü
görünürlüğü route guard İLE YÖNETİLMEZ — otoriter model `MenuItem → MenuItemRoles → Xxx.Menu`
zincirine dayanır (bkz. üstteki "Faz 2B.11: ... erişim" bölümleri). Angular route seviyesinde
`permissionGuard`/`permissionOrSuperAdminGuard` kullanmak, bu mimaride gereksiz bir İKİNCİ
yetkilendirme katmanı YARATIYORDU — ve `app.routes.ts`'teki DİĞER TÜM rotalar (rezervasyon, oda,
muhasebe, restoran, vb. — literalman onlarca rota) zaten HİÇBİR domain-permission guard
TAŞIMIYORDU; yalnız `ticari-belgeler` (önceki bir sapma) ve `muhasebe/e-belge-yonetimi` (Faz
2B.11.1'de eklenen) bu deseni İHLAL EDİYORDU. Bu tur her iki sapmayı da düzeltir.

### Route'lardan domain-permission guard'ları kaldırıldı

`app.routes.ts`'te hem `muhasebe/e-belge-yonetimi` (`permissionOrSuperAdminGuard`) hem
`ticari-belgeler` (`permissionGuard`) rotalarından `canActivate` kaldırıldı. Her iki rota da ARTIK
yalnız uygulamanın kök `authGuard`/`authChildGuard` ağacının (`{ path: '', canActivate: [authGuard],
canActivateChild: [authChildGuard], children: [...] }`) altındadır — TÜM diğer rotalarla AYNI
desen. Menü görünürlüğü DEĞİŞMEDİ (hâlâ `MenuItemRoles` + `Xxx.Menu` ile kontrol edilir); gerçek
işlem yetkisi DEĞİŞMEDİ (hâlâ backend `[Permission(...)]` + tenant/kurum scope kontrolleriyle
uygulanır). Route guard'ın kaldırılması bir GÜVENLİK GEVŞETMESİ DEĞİLDİR — kullanıcı doğrudan
URL'ye gelse bile (menüde görmese BİLE), yetkisiz bir API çağrısı backend tarafından AYNI şekilde
reddedilir; route guard SADECE erken, istemci-taraflı bir yönlendirme kısayoluydu, otoriter kontrol
HİÇBİR ZAMAN değildi.

### `permissionOrSuperAdminGuard` ve `permissionGuard` tamamen kaldırıldı

`permissionOrSuperAdminGuard`, yukarıdaki iki rotanın DIŞINDA hiçbir yerde kullanılmıyordu — TAMAMEN
silindi. `permissionGuard` da (yalnız `ticari-belgeler`'de kullanılıyordu) route'lardan kaldırıldıktan
sonra repository genelinde ARTIK HİÇBİR çağıranı KALMADIĞINDAN (repo genelinde AÇIKÇA arandı, tek
kullanım noktası `ticari-belgeler` idi), `frontend/src/app/pages/auth/permission.guard.ts` VE
`permission.guard.spec.ts` dosyalarının TAMAMI silindi, `auth/index.ts`'teki barrel export satırı
kaldırıldı — dead code BIRAKILMADI. Bu, "kullanılmıyorsa sil, kullanılıyorsa DOKUNMA" ilkesinin
UYGULANMASIdır; başka bir rota gerçekten `permissionGuard` kullanıyor olsaydı dosya KORUNURDU.

### Menu/action permission ayrımı

STYS yetki semantiği üç KATMANA ayrılır, birbirine KARIŞTIRILMAZ:

- **`.Menu`** — YALNIZ menü görünürlüğü içindir (`MenuItemRoles` üzerinden). Backend API
  authorization İÇİN KULLANILMAZ.
- **`.View`** — backend read API işlemleri içindir (`GET policy`/`GET readiness`/`GET revizyonlar`).
  Menü görünürlüğü İÇİN route guard olarak KULLANILMAZ.
- **`.Manage`** — backend write API + frontend işlem butonları içindir (`PUT policy`,
  Aktifleştir/Pasifleştir/Yöntem değiştir). Menü görünürlüğü İÇİN route guard olarak KULLANILMAZ.

### MenuItemRoles doğrulaması

İki mevcut, DEĞİŞTİRİLMEYEN migration doğrulandı:

- `muhasebe/e-belge-yonetimi` → `MuhasebeSatisBelgeleriYonetimi.Menu`
  (`20260807000000_AddEBelgeYonetimiMenuFaz2B11.cs`).
- `ticari-belgeler` → `TicariBelgeYonetimi.Menu` (`20260731210000_AddTicariBelgeYonetimiMenu.cs`,
  Faz 2B.11'den ÖNCEKİ bir migration — bu tur YENİ bir permission/migration İCAT ETMEDİ, yalnız
  mevcut bağlantının doğru olduğunu doğruladı).

### E-Belge component içi permission davranışı

`EBelgeYonetimi` component'indeki `canView`/`canManage` getter'ları DEĞİŞTİRİLMEDİ — bunlar
route authorization DEĞİLDİR, yalnız UI işlemlerini (edit alanlarının açılması, Kaydet,
Aktifleştir, Pasifleştir) kontrol eden UX yardımcılarıdır; backend YİNE otoriterdir. SuperAdmin
İÇİN ayrı bir Angular route guard OLUŞTURULMADI — SuperAdmin davranışı mevcut Menu API/backend
authorization mekanizmasıyla çözülür.

### Kasıtlı olarak YAPILMAYANLAR

Backend `[Permission(...)]`/`EnsureCanAccessKurumAsync`/`EnsureCanManageKurumAsync`
sözleşmelerinin değiştirilmesi, yeni bir permission/migration icadı, Faz 2B.11.1'in UBL/readiness
düzeltmelerinin (`EBelgeUbl.Enabled` readiness entegrasyonu, runtime fail-closed, frontend
capability tek kaynağı, UserManagement bağımsızlığı) GERİ ALINMASI, `EBelgeYonetimi`
component'inin `canView`/`canManage` mantığının route-authorization'a DÖNÜŞTÜRÜLMESİ, test sayısını
KORUMAK için anlamsız replacement test yazılması, test skip etme, tüm solution test paketini
çalıştırma.

## Kurum Süreç Analiz Şablonu

Kurum bazlı bilgi toplama için bkz. `docs/e-belge-kurum-surec-analizi-sablonu.md` — bu şablon
yalnız veri toplar, hukuki/mali karar VERMEZ.

## Sonraki Aşamalar

- `HariciMuhasebeSistemi` için gerçek dış sistem adaptörü (bu fazda YOK — yalnız karar kaydı).
- `OzelEntegrator`/`DogrudanGib` için gerçek adaptör/HSM/mali mühür entegrasyonu.
- Legacy (Faz 2B.10 öncesi) `EBelgeKaydi` kayıtları için açık bir backfill/migrasyon kararı.
- GİB Portal otomatik gönderim (bu fazda yalnız yerel unsigned UBL üretimi var, gönderim manuel).
