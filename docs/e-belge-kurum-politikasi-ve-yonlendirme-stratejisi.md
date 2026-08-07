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

## Kurum Süreç Analiz Şablonu

Kurum bazlı bilgi toplama için bkz. `docs/e-belge-kurum-surec-analizi-sablonu.md` — bu şablon
yalnız veri toplar, hukuki/mali karar VERMEZ.

## Sonraki Aşamalar

- `HariciMuhasebeSistemi` için gerçek dış sistem adaptörü (bu fazda YOK — yalnız karar kaydı).
- `OzelEntegrator`/`DogrudanGib` için gerçek adaptör/HSM/mali mühür entegrasyonu.
- Legacy (Faz 2B.10 öncesi) `EBelgeKaydi` kayıtları için açık bir backfill/migrasyon kararı.
- GİB Portal otomatik gönderim (bu fazda yalnız yerel unsigned UBL üretimi var, gönderim manuel).
