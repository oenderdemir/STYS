# e-Belge Test Stratejisi (Faz 2B.9)

## Amaç

Bu doküman, e-Belge (e-fatura/e-arşiv UBL-TR) test kümesinin **envanterini**, **katmanlarını**,
**kritik invariantlarını** ve bu katmanlara göre çalıştırılan **test profillerini** tanımlar.

Faz 2B.9 bir "test sayısını düşürme projesi" DEĞİLDİR. Amaç:

- Testleri açık, tek bir birincil katmana (`TestLevel`) atamak,
- Hızlı PR geri bildirimini (`fast`) ağır entegrasyon/sidecar/kriptografi testlerinden ayırmak,
- Aynı invariantı GERÇEKTEN tekrarlayan testleri güvenli biçimde birleştirmek (kanıtlı eşdeğerlik ile),
- Kritik mali belge güvenlik invariantlarının bir manifestte açıkça görünür kalmasını sağlamak,
- Uzun `FullyQualifiedName~A|FullyQualifiedName~B|...` filtresini kısa, trait-tabanlı komutlara
  indirmek.

Başarı ölçütü **"daha az test"** değil, **"her kritik invariant açıkça korunuyor, testlerin hangi
profilde çalışacağı belli, aynı davranış gereksiz yere tekrar edilmiyor"** ifadesidir.

## Mevcut test envanteri

Envanter, kaynak koddan ve `dotnet test --list-tests` GERÇEK keşif çıktısından hesaplanmıştır
(tahmin edilmemiştir). e-Belge alanına ait olan ama dosya adı `EBelge` önekini TAŞIMAYAN
`SaxonSidecarEBelgeSchematronValidatorTests` sınıfı da envantere dahil edilmiştir; buna karşılık
yalnız isim benzerliği nedeniyle `Belge` alt dizesiyle eşleşen (ör. `FaturaBelgeYonuTests`,
`TicariBelgeIptalYarisKosuluIntegrationTests`) 8 Fatura/Ödeme alanı sınıfı envanterin DIŞINDA
tutulmuştur - bunlar e-Belge (e-fatura/e-arşiv UBL-TR) alanına ait DEĞİLDİR.

**Toplam: 466 test, 31 sınıf** (`dotnet test --list-tests --filter "Domain=EBelge"` ile doğrulanır).

| Test sınıfı | Sayı | TestLevel | Dependency | Koruduğu invariant (özet) |
|---|---:|---|---|---|
| EBelgeOutboxMesajIslemeServiceTests | 51 | Unit | - | outbox mesaj işleme sonuç/hata sınıflandırması (in-memory fake) |
| EBelgeOutboxWorkerTests | 45(43 metod) | Unit | - | worker semaphore/task orkestrasyon, güvenli loglama (fake harness) |
| EBelgeXmlImzalayiciTests | 37 | CryptoIntegration | Cryptography | gerçek RSA imza üretimi/doğrulama, tamper/wrapping reddi |
| EBelgeUblImzalamaServiceIntegrationTests | 32 | CryptoIntegration | SqlServer, JavaSidecar, Cryptography | atomik imzalama servisi, hash zinciri, idempotency |
| EBelgeUblPreCutValidatorTests | 22 | Contract | - | e-Arşiv/e-Fatura ön-kesim whitelist kuralları |
| EBelgeArtefaktOlusturmaServiceIntegrationTests | 22 | SqlIntegration | SqlServer, JavaSidecar | atomik artefakt oluşturma, idempotency, stale-worker |
| EBelgeOutboxLeaseTransitionIntegrationTests | 21 | SqlIntegration | SqlServer | claim/complete/fail/renew lease geçişleri |
| EBelgeOutboxWorkerHealthCheckTests | 18(14 metod) | Unit | - | health karar matrisi, activation fallback |
| EBelgeCanonicalSnapshotReaderTests | 18 | Contract | - | canonical snapshot JSON şema/hash sözleşmesi |
| EBelgeArtefaktOlusturOutboxHandlerTests | 17 | Unit | - | outbox handler orkestrasyonu (fake servis) |
| EBelgeProcessingOptionsValidatorTests | 16 | Unit | - | options validator (poll/lease/parallelism sınırları) |
| EBelgeOutboxRetryPolicyTests | 16 | Unit | - | retry policy hesaplama |
| EBelgeSchematronSidecarIntegrationTests | 14 | SidecarIntegration | JavaSidecar | gerçek Saxon sidecar (manifest, XXE, limit, restart) |
| EBelgeProcessingActivationGateTests | 14 | Unit | - | processing activation kapısı (tarih/timezone/fail-closed) |
| EBelgeFaz1IntegrationTests | 13 | SqlIntegration | SqlServer | Faz1 canonical snapshot SQL akışı |
| SaxonSidecarEBelgeSchematronValidatorTests | 10 | Unit | - | HTTP validator sınıfı, mock `HttpMessageHandler` (gerçek süreç YOK) |
| EBelgeUblPreCutIntegrationTests | 10 | SqlIntegration | SqlServer | ön-kesim SQL entegrasyonu |
| EBelgeSigningActivationGateTests | 10 | Unit | - | imzalama activation kapısı |
| EBelgeOutboxClaimLeaseIntegrationTests | 10 | SqlIntegration | SqlServer | claim/lease temel SQL akışı, **LeaseTakeover** |
| EBelgeOutboxFaz2AIntegrationTests | 9 | SqlIntegration | SqlServer | Faz2A migration/backfill |
| EBelgeSnapshotUblHazirlikIntegrationTests | 8 | SqlIntegration | SqlServer | snapshot->UBL hazırlık SQL akışı |
| EBelgeSigningBackfillServiceIntegrationTests | 7 | SqlIntegration | SqlServer | signing backfill servisi |
| EBelgeOutboxWorkerMetricsTests | 7 | Unit | - | worker metrics sayaçları |
| EBelgeUblRendererFlowTests | 6 | Unit | - | renderer akış testleri (sahte validator) |
| EBelgeCutoverGateIntegrationTests | 6 | SqlIntegration | SqlServer | cutover kapısı SQL entegrasyonu |
| EBelgeCanonicalSnapshotV1V2ReaderTests | 6 | Contract | - | V1/V2 reader sürüm sözleşmesi |
| EBelgeArtifactEntityIntegrationTests | 6 | SqlIntegration | SqlServer | **TenantIsolation**, **UnsignedExactByteHash** |
| EBelgeOutboxWorkerIntegrationTests | 4+1 | WorkerEndToEnd(+1 ReleaseGate) | SqlServer, JavaSidecar, Cryptography | gerçek worker E2E, **WorkerEndToEndSignedReady** |
| EBelgeUblRendererSmokeTests | 4 | Unit | - | renderer duman testleri |
| EBelgeUblRendererEndToEndIntegrationTests | 2+1 | SidecarIntegration(+1 ReleaseGate) | JavaSidecar | gerçek e-Arşiv renderer+sidecar zinciri |
| EBelgeCanonicalPayloadTests | 3 | Unit | - | immutable payload, exact-byte SHA-256 |

`TestLevel` dağılımı (gerçek keşif ile doğrulandı, toplam 466):

| TestLevel | Sayı |
|---|---:|
| Unit | 217 |
| Contract | 46 |
| SqlIntegration | 112 |
| SidecarIntegration | 16 |
| CryptoIntegration | 69 |
| WorkerEndToEnd | 4 |
| ReleaseGate | 2 |
| **Toplam** | **466** |

## Test katmanları

Tek, e-Belgeye özel trait yapısı kullanılır (repository genelini etkileyen mevcut
`SqlServerIntegrationCollection`/`SchematronSidecarCollection` gibi paylaşılan altyapılar
DEĞİŞTİRİLMEDİ - yalnız üzerlerine ek `[Trait]` eklendi):

```csharp
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "Unit")]
[Trait("Dependency", "SqlServer")]      // gerekirse, birden fazla olabilir
[Trait("CriticalInvariant", "TenantIsolation")]  // yalnız kritik testlerde
```

Kabul edilen `TestLevel` değerleri: `Unit`, `Contract`, `SqlIntegration`, `SidecarIntegration`,
`CryptoIntegration`, `WorkerEndToEnd`, `ReleaseGate`. Her test **tam olarak bir** `TestLevel`
değeri taşır (gerçek keşifle doğrulandı: yedi kategorinin toplamı == `Domain=EBelge` toplamı).

İki sınıfta (`EBelgeOutboxWorkerIntegrationTests`, `EBelgeUblRendererEndToEndIntegrationTests`)
`TestLevel` **sınıf düzeyinde DEĞİL, metod düzeyinde** uygulanmıştır - çünkü bu sınıflardaki tek bir
temsili metod (`GercekWorkerArtefaktOlusturdanUblImzalayaZincirlemeTamamlarUctanUcaSignedReadyUretir`,
`GercekEArsivRendererCiktisiSifirSchematronIhlaliyleBasariylaSonuclanir`) `ReleaseGate`'e, kalan
metodlar ise kendi doğal katmanına (`WorkerEndToEnd`/`SidecarIntegration`) aittir.

### ReleaseGate

`ReleaseGate`, TÜM üretim zincirini (Snapshot -> Unsigned UBL -> XSD -> Schematron -> Unsigned
artifact -> worker -> XAdES -> SignedReady) kanıtlayan **2 temsili** testten oluşur - diğer
kategorilerin kopyası DEĞİLDİR (bu iki metod artık `SidecarIntegration`/`WorkerEndToEnd`
kümesinde SAYILMAZ, yalnız `ReleaseGate`'te sayılır):

1. `EBelgeUblRendererEndToEndIntegrationTests.GercekEArsivRendererCiktisiSifirSchematronIhlaliyleBasariylaSonuclanir`
   (Snapshot -> Unsigned UBL -> XSD -> gerçek Schematron)
2. `EBelgeOutboxWorkerIntegrationTests.GercekWorkerArtefaktOlusturdanUblImzalayaZincirlemeTamamlarUctanUcaSignedReadyUretir`
   (worker -> gerçek XAdES -> SignedReady)

## Kritik invariantlar (manifest)

`[Trait("CriticalInvariant", "...")]` ile işaretlenmiş, az sayıda ve gerçekten kritik test.
Manifest test METOD isimlerine birebir bağımlı DEĞİLDİR - trait üzerinden discovery ile doğrulanır:

| CriticalInvariant | Test |
|---|---|
| TenantIsolation | `EBelgeArtifactEntityIntegrationTests.CrossTenantArtifactFkDbTarafindanReddedilir` |
| StaleWorkerCannotWrite | `EBelgeArtefaktOlusturmaServiceIntegrationTests.ReclaimEdilmisMesajdaEskiWorkerYazamazSadeceYeniSahipYazar` |
| LeaseTakeover | `EBelgeOutboxClaimLeaseIntegrationTests.LeaseSuresiDolmusIsleniyorKaydiYenidenClaimEdilir` |
| UnsignedExactByteHash | `EBelgeArtifactEntityIntegrationTests.ArtefaktBasariylaKaydedilirVeByteBirebirKorunur` |
| SignedExactByteHash | `EBelgeUblImzalamaServiceIntegrationTests.GecerliImzaTamAtomikBasariylaSignedReadyArtefaktUretirVeHashZinciriDogrulanir` |
| SignatureTamperRejected | `EBelgeXmlImzalayiciTests.TekByteDegisikligiImzayiBozar` |
| DuplicateXmlIdRejected | `EBelgeXmlImzalayiciTests.YinelenenIdIcerenUnsignedXmlReddedilir` |
| SchematronRealSidecar | `EBelgeSchematronSidecarIntegrationTests.UygunXmlSifirFailedAssertDoner` |
| ActivationNotBefore20260915 | `EBelgeProcessingActivationGateTests.OnBesEylulYerelGunBaslangicindaVeSonrasindaIslemeYapilabilir` |
| WorkerEndToEndSignedReady | `EBelgeOutboxWorkerIntegrationTests.GercekWorkerArtefaktOlusturdanUblImzalayaZincirlemeTamamlarUctanUcaSignedReadyUretir` |

`dotnet test --list-tests --filter "CriticalInvariant=TenantIsolation|CriticalInvariant=..."`
discovery ile 10/10 manifest testinin GERÇEKTEN bulunduğu doğrulanmıştır - `release` profili
`Domain=EBelge` filtresiyle bunların TAMAMINI zaten kapsar.

## Fast profil

Amaç: geliştirici/PR geri bildirimi. `TestLevel` kapsamı: `Unit`, `Contract`.

SQL Server, Java sidecar veya worker E2E BAŞLATMAZ. Saf kriptografi testleri (`EBelgeXmlImzalayiciTests`,
gerçek RSA kullanır) `CryptoIntegration` katmanına ayrıldığı ve `fast`'e DAHİL edilmediği için bu
soru pratikte kendiliğinden çözülmüştür - `fast` yalnız in-memory/fake-tabanlı testleri kapsar.

```
263 test, ~3 sn (yalnız test yürütme süresi)
```

## Integration profil

Amaç: merge öncesi servis/veritabanı güvenliği. `TestLevel` kapsamı: `Unit`, `Contract`,
`SqlIntegration`, `CryptoIntegration`. Java sidecar bu profile DAHİL EDİLMEDİ - `SqlIntegration`
ve `CryptoIntegration` testlerinin bir kısmı (ör. `EBelgeUblImzalamaServiceIntegrationTests`) zaten
sidecar'a bağımlı olduğundan sidecar süreci bu profilde de dolaylı olarak ayağa kalkar; ancak SAF
`SidecarIntegration` katmanı (yalnız Schematron sınır/limit/restart senaryoları) ölçülen süre
(nightly'de +2sn'lik ek yük) ve CI altyapısı gerekçesiyle `nightly`/`release`'e bırakıldı.

```
444 test, ~126 sn (gerçek SQL Server + gerçek RSA test sertifikası)
```

## Nightly profil

Amaç: tüm ağır bağımlılıklar. `TestLevel` kapsamı: `Unit`, `Contract`, `SqlIntegration`,
`SidecarIntegration`, `CryptoIntegration`, `WorkerEndToEnd`.

```
464 test, ~128 sn (gerçek SQL Server + gerçek Java Saxon sidecar + gerçek RSA + worker E2E)
```

## Release profil

Amaç: üretim öncesi tam kapı. `Domain=EBelge` altındaki TÜM testler (`TestLevel`'dan bağımsız,
`ReleaseGate` dahil). Production sertifikası veya private key İSTEMEZ - `EBelgeTestSertifikaSaglayici`
yalnız test DI container'ında, bellekte üretilen self-signed bir sertifikadır (bkz. görev md.10
kısıtı - production key hiçbir profile eklenmedi).

```
466 test, ~127 sn (gerçek SQL Server + gerçek Java Saxon sidecar + gerçek RSA + worker E2E + ReleaseGate)
```

Migration doğrulaması ve production default config doğrulaması ayrı script/pipeline adımlarıdır
(bu depoda mevcut migration/deploy script'leri - `scripts/deploy-*.ps1` - zaten bunu kapsar);
`release` profili yalnız test seviyesindeki kapıyı çalıştırır.

## CI önerisi

Repository incelendi: `.github/workflows`, `eng/`, `Makefile` YOKTUR; `scripts/` altında yalnız
deploy/push script çiftleri (`.ps1`+`.sh`) bulunmaktadır - test yürütmeye özel bir script veya CI
pipeline'ı DAHA ÖNCE YOKTU.

**Bu turda hiçbir CI workflow dosyası EKLENMEDİ** (görev md.13/md.24 kısıtı - "yeni ve geniş
kapsamlı CI platformu kurma"). Yalnız `scripts/test-ebelge.ps1` + `scripts/test-ebelge.sh`
(repodaki mevcut PS1+SH ikili script konvansiyonuna uygun) hazırlandı. Önerilen bağlanma (repo bir
CI platformu benimsediğinde):

- Pull request -> `./scripts/test-ebelge.ps1 fast`
- main/merge -> `./scripts/test-ebelge.ps1 integration`
- Zamanlanmış (nightly) -> `./scripts/test-ebelge.ps1 nightly`
- Manuel/release workflow -> `./scripts/test-ebelge.ps1 release`

Ağır job'lar (`integration`/`nightly`/`release`) `STYS_INTEGRATION_TEST_CONNECTION_STRING` ve
derlenmiş sidecar (`sidecar/schematron-validator/out/classes` + JDK 17+) gerektirir; script bu
bağımlılıklar eksikse mevcut açık skip/fail politikasını (bkz. Flaky test politikası) DEĞİŞTİRMEDEN
kullanır.

## Sidecar izolasyonu

İncelenen mevcut altyapı: `SchematronSidecarProcessFixture` (GERÇEK Java Saxon-HE 13.0 sürecini
rastgele BOŞ bir TCP portunda başlatır, `/health/ready`'yi 500ms aralıklarla en fazla 60 kez
polling ile bekler - deterministik, sabit üst sınırlı bir hazır-olma kontrolü) ve
`SchematronSidecarCollection` (`ICollectionFixture` - `EBelgeSchematronSidecarIntegrationTests` ve
`EBelgeUblRendererEndToEndIntegrationTests` sınıflarının AYNI tek süreç örneğini PAYLAŞMASINI ve
xUnit'in bu ikisini SERİ çalıştırmasını sağlar).

3 sınıf (`EBelgeOutboxWorkerIntegrationTests`, `EBelgeArtefaktOlusturmaServiceIntegrationTests`,
`EBelgeUblImzalamaServiceIntegrationTests`) `IClassFixture<SchematronSidecarProcessFixture>` İLE
KENDİ BAĞIMSIZ sidecar sürecini başlatır (paylaşılan `SchematronSidecarCollection`'a DAHİL
DEĞİLDİR) - ancak bu 3 sınıf zaten `SqlServerIntegrationCollection`'a üye olduğundan (SQL
deadlock'larını önlemek için, bkz. `TestSupport/SqlServerIntegrationCollection.cs`), kendi
ARALARINDA ve solution genelindeki TÜM SQL entegrasyon testleriyle SERİ çalışırlar.

**Değerlendirme**: xUnit'te bir test sınıfı yalnız TEK bir `[Collection]`'a üye olabilir; bu 3
sınıfı `SchematronSidecarCollection`'a taşımak SQL serileştirme garantisini KAYBETTİRİR (deadlock
riskini geri getirir - başka modüllerin test davranışını etkiler, bkz. görev md.3 kısıtı); bunları
`SqlServerIntegrationCollection`'a EKLEMEK (2 saf sidecar sınıfını) İSE bu iki sınıfı SOLUTION
GENELİNDEKİ TÜM SQL testleriyle gereksiz yere serileştirir - "yalnız gerçekten ortak external
process kullanan testleri sınırlandır" ilkesine AYKIRIDIR (bu 5 sınıf AYNI süreci PAYLAŞMAZ, her biri
kendi rastgele portunda BAĞIMSIZ bir JVM başlatır). Bu yüzden fixture/collection yapısı
DEĞİŞTİRİLMEDİ.

Kalan teorik risk: paylaşılan-fixture grubu (2 sınıf) ile kendi-fixture grubu (3 sınıf, SQL
collection'ı üzerinden kendi aralarında seri) FARKLI xUnit collection'ları olduğundan birbirleriyle
PARALEL çalışabilir - yani en fazla 2 JVM süreci AYNI ANDA ayakta olabilir. Bu bir PORT çakışması
YARATMAZ (her fixture `GetFreeTcpPort()` ile bağımsız, rastgele bir port seçer) ve `DisposeAsync`
yalnız KENDİ `_process` alanını sonlandırır (başka bir fixture'ın sürecini ETKİLEMEZ) - yalnız CPU
kaynak REKABETİ (iki JVM'in aynı anda JIT/başlatma) teorik bir gecikme riski taşır. Bu depoda ÖNCEDEN
raporlanan geçici bağlantı sıfırlanması sorunu (bu turda yeniden ÜRETİLEMEDİ - 3 ardışık `nightly`/
`release` koşumu boyunca sidecar testleri hiç flaky DAVRANMADI) muhtemelen budur; test yürütme
süresi düşük olduğundan (nightly ~128 sn, sidecar testleri içinde en yavaşı 7.8 sn) ŞU AN için ek bir
serileştirme YAPILMADI - **öneri**: eğer ileride bu risk somut biçimde gözlemlenirse, en düşük
riskli çözüm `SchematronSidecarProcessFixture`'a bir dosya-tabanlı (`FileStream` + `FileShare.None`)
process-level kilit eklemektir (xUnit collection yapısını hiç değiştirmeden, yalnız fixture
`InitializeAsync` içinde).

## SQL izolasyonu

`SqlServerIntegrationCollection` (repo geneli, e-Belgeye özel DEĞİL) TÜM gerçek SQL Server testlerini
TEK bir xUnit collection'ında toplayarak SERİ çalıştırır - kök neden dokümante edilmiştir (paylaşılan
FK-yoğun tablolarda eşzamanlı INSERT/DELETE'in "deadlock victim" hatalarına yol açması). e-Belge SQL
entegrasyon testleri (12 sınıf, 112+ test) bu ORTAK collection'a üyedir; her sınıf kendi
`_uniqueSuffix`/`TestMarker` ile İZOLE veri üretir ve `DisposeAsync`'te yalnız KENDİ ürettiği
kayıtları temizler (global tablo temizliği YAPILMAZ) - bu turda bu davranış DEĞİŞTİRİLMEDİ, yalnız
gözlemlendi ve doğrulandı (4 profil koşumu boyunca hiçbir deadlock/paylaşılan-veri çakışması
GÖRÜLMEDİ).

## Flaky test politikası

Yeni politika: **bir test rastgele geçmiyorsa otomatik retry EKLENMEZ.** Flaky/zamana-bağımlı bir
test tespit edilirse skip/quarantine/retry/assertion gevşetme İLE sessizce yeşile ÇEVRİLMEZ - kök
neden bulunup DÜZELTİLİR.

### Bu turda bulunan ve düzeltilen gerçek "zaman bombası" testi

- **Test**: `EBelgeUblImzalamaServiceIntegrationTests.GecerliImzaTamAtomikBasariylaSignedReadyArtefaktUretirVeHashZinciriDogrulanir`
  (`CriticalInvariant=SignedExactByteHash`)
- **Bağımlılık**: SqlServer, JavaSidecar, Cryptography
- **Hata tipi**: `AtomikKaliciHata: EBELGE_SIGNING_CERTIFICATE_INVALID_PERIOD` ("Sertifika henüz
  geçerlilik tarihine ulaşmadı")
- **Kök neden**: Test, imzalama zamanı için SABİT bir takvim tarihi (`2026-08-05T10:00:00Z`)
  kullanıyordu; `EBelgeTestSertifikaSaglayici`'nin VARSAYILAN sertifika geçerlilik başlangıcı İSE
  GERÇEK duvar saatine göre (`DateTimeOffset.UtcNow.AddDays(-1)`, test SÜRECİNİN çalıştığı ANA göre)
  hesaplanıyordu. Takvim GERÇEKTEN `2026-08-06T10:00:00Z`'yi geçtiğinde, sabit değer artık
  sertifikanın `notBefore`'undan ÖNCEYE düşüyor - bu, düzeltilmeseydi O TARİHTEN İTİBAREN HER
  ÇALIŞTIRMADA (rastgele DEĞİL, KESİN olarak) başarısız olacak bir "zaman bombası" idi, klasik
  anlamda "flaky" değildi.
- **Çözüm**: Sabit takvim tarihi yerine, test ÇALIŞTIĞI ANIN gerçek zamanı (`DateTimeOffset.UtcNow`)
  kullanılacak şekilde değiştirildi - bu DEĞER hâlâ `FixedTimeProvider`'a verilip TEK bir çağrı İçinde
  sabitlendiği için testin kendi İçindeki determinizmi (imzalama zamanının `TimeProvider`'dan AYNEN
  saklandığının kanıtlanması) KORUNDU; yalnız sertifikanın geçerlilik penceresiyle İLİŞKİSİ artık
  HER ZAMAN güvenli. Assertion GEVŞETİLMEDİ, yalnız zamana bağımlı test girdisinin kök nedeni
  düzeltildi.
- **Tekrar üretim**: Değişiklik ÖNCESİ, gerçek sistem saati `2026-08-06T10:00:00Z`'yi geçtiğinde
  `./scripts/test-ebelge.ps1 integration` ile HER ZAMAN üretilebilir.

Bu değişiklik dışında, 4 profilin toplam 4 tam koşumunda (fast, integration x2, nightly, release)
başka hiçbir flaky/aralıklı başarısızlık GÖZLEMLENMEDİ.

## Birleştirilen testler

| Eski test(ler) | Korunan invariant | Yeni test | Neden eşdeğer/daha güçlü |
|---|---|---|---|
| `EBelgeOutboxWorkerHealthCheckTests.FreshStateEnabledTrueTarihAcikLoopBaslamadiUnhealthy`, `FreshStateEnabledFalseHealthyDisabled`, `FreshStateTarihOncesiHealthyBeforeActivationDate`, `FreshStateGecersizTarihDegraded`, `FreshStateGecersizTimeZoneDegraded` (5 ayrı `[Fact]`) | Activation-reason -> health-status karar matrisi (Disabled/BeforeActivationDate->Healthy, InvalidDate/InvalidTimeZone->Degraded, Active+loop-yok->Unhealthy) | `FreshStateAktivasyonKarariBeklenenHealthSonucunuUretir` (`[Theory]`, 5 `MemberData` satırı) | AYNI kod yolu (health check'in gerçek gate fallback değerlendirmesi), AYNI dependency seviyesi (in-memory, elle seed YOK), 5 senaryonun TAMAMI ayrı ayrı korunur - xUnit her satırı KENDİ parametreleriyle raporlar, teşhis zayıflamaz |
| `EBelgeOutboxWorkerTests.WorkerLevelExceptionMesajindakiLeaseTokenLoglanmaz`, `WorkerLevelExceptionMesajindakiXmlVeVknLoglanmaz`, `WorkerLevelExceptionMesajindakiPasswordVeSignatureValueLoglanmaz` (3 ayrı `[Fact]`) | Worker-seviyesi exception mesajındaki gizli değerlerin (token/XML/VKN/parola/imza) loga sızmaması | `WorkerLevelExceptionMesajindakiGizliDegerLogaSizmaz` (`[Theory]`, 3 `MemberData` satırı) | AYNI kod yolu (claim exception -> güvenli loglama), AYNI dependency seviyesi (fake harness), üçüncü senaryonun İKİ gizli değerinin AYNI mesajda BİRLİKTE test edilmesi de KORUNDU - hiçbir assertion KAYBOLMADI |

Her iki birleştirmede de **toplam test SENARYOSU (xUnit tarafından çalıştırılan case) sayısı
DEĞİŞMEDİ** (5+3=8 senaryo, birleştirme ÖNCESİ ve SONRASI da 8 kez çalışır) - yalnız kaynak kod
METOD sayısı azaldı (8 metod -> 2 metod). Bu, görev md.4'ün "isim benzerliğine göre silme, yalnız
GERÇEKTEN aynı invariantı/kod yolunu/dependency seviyesini paylaşan ve teşhis gücünü AZALTMAYAN
testleri birleştir" kuralına göre yapılmıştır.

## Korunan testler

Görev md.6'daki TÜM invariant kategorileri (tenant/artifact bütünlüğü, claim/lease, XML/Schematron,
XAdES, activation, worker) mevcut testlerde KORUNMUŞ olarak doğrulanmıştır - hiçbiri silinmedi veya
zayıflatılmadı. Bunların 10 tanesi yukarıdaki Kritik invariantlar manifestinde AYRICA işaretlidir;
kalanı (ör. duplicate artifact/idempotency conflict testleri, `SignerRole`/`KeyValue` testleri,
`SoftDelete` senaryoları) ilgili sınıfların `TestLevel`/`Dependency` traitleriyle keşfedilebilir
durumda, DEĞİŞTİRİLMEDEN kalmıştır.

## Test süreleri

4 profilin bu turda alınan TEMİZ (SQL container + JDK 17 hazır, `dotnet build` sonrası) koşum
sonuçları - TRX dosyaları `tests/STYS.Tests/TestResults/ebelge-<profil>-<zaman damgası>.trx`
altında saklanmıştır:

| Profil | Test sayısı | Süre | External dependency | Başarısız |
|---|---:|---:|---|---:|
| fast | 263 | ~3 sn (yalnız test) / ~21 sn (build dahil) | yok | 0 |
| integration | 444 | ~126 sn | gerçek SQL Server, gerçek RSA test sertifikası | 0 |
| nightly | 464 | ~128 sn | + gerçek Java Saxon sidecar, worker E2E | 0 |
| release | 466 | ~127 sn | + ReleaseGate (tam zincir) | 0 |

### En yavaş 10 test (release profili, TRX'ten)

| Süre | Test |
|---:|---|
| 7.78 sn | `EBelgeSchematronSidecarIntegrationTests.ReadyEndpointCompileOncesindeBasarisizdir` |
| 7.35 sn | `EBelgeSchematronSidecarIntegrationTests.SidecarRestartSonrasiAyniXmlAyniSonucuVerir` |
| 5.20 sn | `EBelgeUblPreCutIntegrationTests.EArsivKanaliKabulEdilirVeV2SnapshotDogruUretilir` |
| 3.92 sn | `EBelgeOutboxFaz2AIntegrationTests.MigrationBackfillAktifKayitIcinBirMesajUretir` |
| 3.44 sn | `EBelgeOutboxWorkerIntegrationTests.IkiInstanceAyniMesajiIsleyemezVeLeaseSuresiDolduktanSonraIkinciWorkerTamamlar` |
| 2.99 sn | `EBelgeOutboxWorkerIntegrationTests.WorkerKapatilipYenidenBaslatildigindaTamamlanmisMesajTekrarIslenmez` |
| 2.62 sn | `EBelgeUblRendererSmokeTests.SchematronDerlemesiXPath2ExistsFonksiyonundaBilinenEngeleTakilir` |
| 2.15 sn | `EBelgeArtefaktOlusturmaServiceIntegrationTests.SidecarErisilemiyorsaGeciciHataOlurArtefaktOlusmazVeSahiplikKontroluGerekmez` |
| 1.64 sn | `EBelgeOutboxWorkerTests.WorkerLevelExceptionMesajindakiGizliDegerLogaSizmaz` (VKN satırı) |
| 0.92 sn | `EBelgeUblImzalamaServiceIntegrationTests.AyniKaynagaEslesenMevcutSignedReadyIdempotentBasariylaTamamlanirIkinciSatirEklenmezVeYenidenDogrulanir` |

En yavaş iki test de gerçek Java sürecinin başlatılma/yeniden başlatılma maliyetini içerir
(`ReadyEndpointCompileOncesindeBasarisizdir` bilinçli olarak sidecar'ı henüz derleme
TAMAMLANMADAN sorgular; `SidecarRestartSonrasiAyniXmlAyniSonucuVerir` sürecin TAMAMEN yeniden
başlatılmasını bekler) - bu süreler sabit bir SLA olarak KODLANMADI, yalnız baseline olarak
kaydedildi.

## Kullanım komutları

Eski, uzun `FullyQualifiedName~A|FullyQualifiedName~B|...` filtresi artık birincil çalışma yöntemi
DEĞİLDİR (dokümantasyonda kaldırılmıştır). Yerine:

```powershell
./scripts/test-ebelge.ps1 fast
./scripts/test-ebelge.ps1 integration
./scripts/test-ebelge.ps1 nightly
./scripts/test-ebelge.ps1 release
```

```sh
./scripts/test-ebelge.sh fast
./scripts/test-ebelge.sh integration
./scripts/test-ebelge.sh nightly
./scripts/test-ebelge.sh release
```

Veya doğrudan trait-tabanlı `dotnet test` filtresi:

```bash
dotnet test tests/STYS.Tests/STYS.Tests.csproj --filter "(Domain=EBelge&TestLevel=Unit)|(Domain=EBelge&TestLevel=Contract)"
```

Tek bir kritik invariantı hedeflemek için:

```bash
dotnet test tests/STYS.Tests/STYS.Tests.csproj --filter "CriticalInvariant=LeaseTakeover"
```

## Sonraki aşama

HSM/mali mühür entegrasyonu geliştirmesi için bu faz temiz bir test zemini bırakır: yeni HSM/mali
mühür testleri aynı trait taksonomisini (`Domain=EBelge`, uygun `TestLevel`, gerekirse yeni bir
`Dependency` değeri - ör. `Hsm`) kullanarak eklenebilir; `CryptoIntegration` katmanı zaten gerçek
imza altyapısı testlerinin doğal yeri olarak KURULMUŞTUR. Faz 2B.7.1'in "Sonraki faz" listesi (PDF/
e-posta/entegratör gönderimi, frontend) bu turda DEĞİŞMEDEN geçerlidir.
