# e-Belge Test Stratejisi (Faz 2B.9 / 2B.9.1)

## Amaç

Bu doküman, e-Belge (e-fatura/e-arşiv UBL-TR) test kümesinin **envanterini**, **katmanlarını**,
**kritik invariantlarını** ve bu katmanlara göre çalıştırılan **test profillerini** tanımlar.

Faz 2B.9.1, Faz 2B.9'da kurulan profil/trait sistemini GERÇEK bir güvenlik/release kapısı haline
getiren bir sertleştirme turudur: profil tanımları artık PowerShell/Bash arasında elle kopyalanmıyor
(tek merkezi JSON manifesti), trait sözleşmesi (`Domain`/`TestLevel`/`Dependency`/`CriticalInvariant`)
reflection tabanlı bir contract test sınıfıyla OTOMATİK doğrulanıyor, ağır profiller (`integration`/
`nightly`/`release`) eksik bağımlılıkta (SQL Server/Java sidecar) veya eksik kritik invariantta
fail-closed davranıyor, ve Bash script'in `set -e` kaynaklı "dotnet test başarısız olunca özet hiç
çalışmaz" hatası düzeltildi. Faz 2B.9'un ORİJİNAL envanteri/katman/birleştirme/flaky-politika içeriği
bu bölümlerde KORUNMUŞTUR; yalnız 2B.9.1'in eklediği/değiştirdiği kısımlar güncellendi.

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

**Faz 2B.9 taban toplamı: 466 test, 31 sınıf.** Faz 2B.9.1, trait sözleşmesini VE kritik invariant
manifestini OTOMATİK doğrulayan `EBelgeTestMetadataContractTests` sınıfını (22 test - 1 `[Fact]` x5 +
1 `[Theory]` x10 satır + 1 `[Theory]` x7 satır) ekledi - **yeni toplam: 488 test, 32 sınıf**
(`dotnet test --list-tests --filter "Domain=EBelge"` ile doğrulanır). Ayrıca `Domain=EBelge`
sözleşmesine BİLİNÇLİ OLARAK tabi OLMAYAN (allowlist ile hariç tutulan) 2 dependency-preflight testi
eklendi - bunlar 488'e DAHİL DEĞİLDİR (bkz. "Dependency fail-closed politikası").

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
| EBelgeTestMetadataContractTests (Faz 2B.9.1) | 22 | Contract | - | trait sözleşmesi + kritik invariant manifesti OTOMATİK doğrulaması |

`TestLevel` dağılımı (gerçek keşif ile doğrulandı, toplam 488):

| TestLevel | Sayı |
|---|---:|
| Unit | 217 |
| Contract | 68 |
| SqlIntegration | 112 |
| SidecarIntegration | 16 |
| CryptoIntegration | 69 |
| WorkerEndToEnd | 4 |
| ReleaseGate | 2 |
| **Toplam** | **488** |

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

## Metadata contract testi (Faz 2B.9.1)

`EBelgeTestMetadataContractTests` (`tests/STYS.Tests/EBelgeTestMetadataContractTests.cs`), yukarıdaki
trait taksonomisini artık yalnız bir DOKÜMAN olarak DEĞİL, derlenmiş assembly'yi `System.Reflection`
üzerinden tarayarak OTOMATİK doğrular. Sınıf adında `EBelge` geçen HER PUBLIC test sınıfı (tek istisna:
açık bir allowlist - bkz. aşağı) "e-Belge test sınıfı" sayılır; her `[Fact]`/`[Theory]` METODU için
sınıf+metod düzeyi trait'ler birlikte hesaplanır. xUnit v2'nin `TraitAttribute`'ı `Name`/`Value`'yu
public property olarak İFŞA ETMEDİĞİ için okuma `CustomAttributeData` (ham metadata) üzerinden yapılır
- attribute NESNESİ hiç OLUŞTURULMAZ.

Doğrulanan kurallar (7 test, 22 test case - `Fact` başına onlarca ayrı test YAZILMADI, her kural
kendi İÇİNDE TÜM e-belge test metodlarını gezip aggregate bir hata mesajıyla raporlar):

1. `TumEBelgeTestleriDomainEBelgeTasirVeTamOlarakBirGecerliTestLevelTasir` - her test `Domain=EBelge`
   taşır VE tam olarak BİR, whitelist'teki bir `TestLevel` değerine sahiptir (bu tek kural, görev
   md.2 kural 1-3'ü VE `ReleaseGate`'in ikinci bir `TestLevel` taşımadığını -aynı "tam olarak 1"
   kısıtı ile- birlikte kapsar).
2. `DependencyDegerleriYalnizWhitelistIcindedir` - `Dependency` değerleri yalnız `SqlServer`/
   `JavaSidecar`/`Cryptography` whitelist'indedir.
3. `CriticalInvariantDegerleriYalnizWhitelistIcindedirVeKasitsizTekrarEtmez` - `CriticalInvariant`
   değerleri yalnız `EBelgeCriticalInvariantManifest.KnownInvariants`'tadır VE bir invariant KASITSIZ
   olarak birden fazla teste UYGULANMAMIŞTIR (bilinçli bir tekrar gerekirse
   `KasitliCriticalInvariantTekrarlari` allowlist'ine gerekçeyle eklenir - şu an boş).
4. `KritikInvariantManifestindekiHerBirIcinEnAzBirGecerliTestBulunur` (`[Theory]`, manifestteki 10
   invariant için 10 satır) - HER invariant için en az bir test bulunur VE o test geçerli
   `Domain`/`TestLevel` taşır.
5. `HerBilinenTestLevelIcinEnAzBirGercekTestVardir` (`[Theory]`, 7 satır) - her whitelist `TestLevel`
   değeri en az bir gerçek test tarafından kullanılır (boş/ölü kategori YOK).
6. `ReleaseProfiliTumKritikInvariantlariKapsar` - `release` profilinin `Domain=EBelge` filtresinin
   10 kritik invariant testinin TAMAMINI OTOMATİK kapsadığını doğrudan doğrular.
7. `ProfilManifestindekiBilinenListelerKodlaSenkrondur` - `scripts/ebelge-test-profiles.json`'daki
   `knownTestLevels`/`knownDependencies` dizileri, BURADAKİ C# whitelist'iyle HER ZAMAN birebir aynı
   kalır (biri güncellenip diğeri UNUTULURSA bu test BAŞARISIZ olur - "aynı listeyi elle kopyalama"
   riskine karşı OTOMATİK bir kilit).

**Kanıtlanmış negatif senaryo**: `EBelgeCriticalInvariantManifest.KnownInvariants`'a GEÇİCİ olarak
mevcut hiçbir testin karşılamadığı sahte bir isim eklenip contract testleri çalıştırıldığında,
`KritikInvariantManifestindekiHerBirIcinEnAzBirGecerliTestBulunur` VE `ReleaseProfiliTumKritikInvariantlariKapsar`
AÇIKÇA, anlaşılır bir mesajla BAŞARISIZ olur (bkz. "Dependency-negatif test sonuçları"). Bu, "kritik
testlerden biri silinirse release profili başlamadan/test çalışması sırasında başarısız olmalı"
kuralının GERÇEK KANITIDIR - sahte bir manifest girdisiyle test edilmiştir, GERÇEK bir kritik test
SİLİNEREK DEĞİL.

Sınıf adında `EBelge` geçtiği halde bu sözleşmeye tabi OLMAYAN tek istisna:
`EBelgeSqlSidecarPreflightTests` (açık allowlist - bkz. aşağı, bu sınıf DOMAIN davranışı değil, test
ALTYAPISININ KENDİSİNİN çalışabilir olduğunu doğrular).

## Kritik invariantlar (manifest)

`[Trait("CriticalInvariant", "...")]` ile işaretlenmiş, az sayıda ve gerçekten kritik test. Liste TEK
bir merkezi C# kaynağında tanımlıdır (`EBelgeCriticalInvariantManifest.KnownInvariants`,
`tests/STYS.Tests/EBelgeCriticalInvariantManifest.cs`) - script veya başka bir dosyaya ELLE
KOPYALANMAZ; hem contract testi hem `release` profilinin kritik-invariant preflight adımı
(`dotnet test --filter "FullyQualifiedName~EBelgeTestMetadataContractTests"`) bunu referans alır.
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

SQL Server, Java sidecar veya worker E2E BAŞLATMAZ, dependency preflight ÇALIŞTIRMAZ (manifestte
`requiredDependencies: []`). Saf kriptografi testleri (`EBelgeXmlImzalayiciTests`, gerçek RSA
kullanır) `CryptoIntegration` katmanına ayrıldığı ve `fast`'e DAHİL edilmediği için bu soru pratikte
kendiliğinden çözülmüştür - `fast` yalnız in-memory/fake-tabanlı testleri kapsar. Faz 2B.9.1'in yeni
22 metadata contract testi de saf reflection/dosya-okuma tabanlıdır (dış bağımlılık YOK), bu yüzden
`fast`'e dahil edilmiştir.

```
285 test, ~3 sn (yalnız test yürütme süresi)
```

## Integration profil

Amaç: merge öncesi servis/veritabanı güvenliği. `TestLevel` kapsamı: `Unit`, `Contract`,
`SqlIntegration`, `CryptoIntegration`.

**Önemli netleştirme (Faz 2B.9.1, görev md.9)**: "Integration profili `SidecarIntegration`
`TestLevel` katmanını SEÇMEZ" ifadesi "Java sidecar süreci HİÇ kullanılmıyor" ANLAMINA GELMEZ. Seçilen
`CryptoIntegration` testlerinin bir kısmı (ör. `EBelgeUblImzalamaServiceIntegrationTests`) ve bazı
`SqlIntegration` testleri (ör. `EBelgeArtefaktOlusturmaServiceIntegrationTests`) gerçek Unsigned UBL
üretmek İçin TAM XSD/Schematron doğrulamasını GEREKTİRİR - bu yüzden `integration` profili
manifestte AÇIKÇA `requiredDependencies: ["SqlServer", "JavaSidecar"]` olarak işaretlenmiştir VE
script HER İKİ dependency İçin de preflight ÇALIŞTIRIR (fail-closed - bkz. "Dependency fail-closed
politikası"). SAF `SidecarIntegration` katmanı (yalnız Schematron sınır/limit/restart senaryoları,
UBL üretimiyle İLGİSİZ) ölçülen süre ve CI altyapısı gerekçesiyle `nightly`/`release`'e bırakılmıştır
- doğru ifade: **"integration profili `SidecarIntegration` katmanını seçmez; ancak seçtiği
`CryptoIntegration`/`SqlIntegration` testlerinin tam XSD/Schematron doğrulaması nedeniyle Java
sidecar runtime bağımlılığı DEVAM EDER."**

```
466 test, ~111-131 sn (preflight dahil; gerçek SQL Server + gerçek Java Saxon sidecar + gerçek RSA test sertifikası)
```

## Nightly profil

Amaç: tüm ağır bağımlılıklar. `TestLevel` kapsamı: `Unit`, `Contract`, `SqlIntegration`,
`SidecarIntegration`, `CryptoIntegration`, `WorkerEndToEnd`. `requiredDependencies`: `SqlServer`,
`JavaSidecar` (fail-closed).

```
486 test, ~152-221 sn (gerçek SQL Server + gerçek Java Saxon sidecar + gerçek RSA + worker E2E)
```

## Release profil

Amaç: üretim öncesi tam kapı. `Domain=EBelge` altındaki TÜM testler (`TestLevel`'dan bağımsız,
`ReleaseGate` dahil). `requiredDependencies`: `SqlServer`, `JavaSidecar` (fail-closed);
`requireCriticalInvariants: true` (bkz. aşağı). Production sertifikası veya private key İSTEMEZ -
`EBelgeTestSertifikaSaglayici` yalnız test DI container'ında, bellekte üretilen self-signed bir
sertifikadır (bkz. görev md.10 kısıtı - production key hiçbir profile eklenmedi).

Script şu sırayla çalışır: (1) test metadata contract doğrulaması + (2) kritik invariant manifest
doğrulaması - İKİSİ de AYNI `EBelgeTestMetadataContractTests` filtresiyle, tek bir preflight adımında
birlikte kanıtlanır; (3) SqlServer + (4) JavaSidecar preflight; (5) `Domain=EBelge` filtresiyle TÜM
e-belge testleri (production default activation değerleri için mevcut `EBelgeProcessingOptionsValidatorTests`/
`EBelgeSigningActivationGateTests` dahil, `2026-09-15 Europe/Istanbul` activation testi
`EBelgeProcessingActivationGateTests` dahil, e-belge migration entegrasyon testleri `EBelgeOutboxFaz2AIntegrationTests`
dahil); (6) sıfır-skip kontrolü. Migration'ın GERÇEK SQL testleriyle (`EBelgeOutboxFaz2AIntegrationTests` -
"migration backfill" senaryoları) doğrulandığı BURADA belgelenmiştir; production deploy script'leri
(`scripts/deploy-*.ps1`) release test script'i İÇİNDE ÇALIŞTIRILMAZ, gerçek deploy GERÇEKLEŞTİRİLMEZ.

```
488 test, ~161-181 sn (preflight + gerçek SQL Server + gerçek Java Saxon sidecar + gerçek RSA + worker E2E + ReleaseGate)
```

## Tek merkezi profil manifesti (Faz 2B.9.1)

Profil tanımları (`testLevels`/`requiredDependencies`/`requireCriticalInvariants`/`failOnSkippedTests`/
`allDomainTests`) artık `scripts/ebelge-test-profiles.json`'da TEK bir yerde tanımlıdır - PowerShell
VE Bash script'lerinin HİÇBİRİNDE ayrı ayrı TEKRARLANMAZ. Manifest ayrıca kök seviyede
`knownTestLevels`/`knownDependencies` whitelist'lerini VE test projesinin yolunu (`testProject`)
taşır.

Her iki script de manifesti OKUR ve AYNI algoritmayla filtre üretir:

```
allDomainTests == true  -> "Domain=EBelge"
aksi halde                -> "(Domain=EBelge&TestLevel=X)|(Domain=EBelge&TestLevel=Y)|..."
```

**Kurallar (her ikisinde de uygulanır)**:

- Manifest dosyası BULUNAMAZSA test HİÇ ÇALIŞTIRILMAZ (script exit 1).
- Manifest JSON olarak PARSE EDİLEMEZSE (PowerShell: `ConvertFrom-Json` hatası; Bash: kök alanlar
  okunamaz) anlaşılır bir hata ile durulur.
- Bilinmeyen bir profil adı (`profiles` altında YOK) fail-fast'tir.
- Bir profilin `testLevels`'ındaki HERHANGİ bir değer `knownTestLevels` whitelist'inde DEĞİLSE
  fail-fast'tir.
- Bir profilin `requiredDependencies`'indeki HERHANGİ bir değer `knownDependencies` whitelist'inde
  DEĞİLSE fail-fast'tir.

**PowerShell tarafı** native `ConvertFrom-Json` kullanır (tam, güvenilir bir JSON parser).
**Bash tarafı**, POSIX shell'de genel amaçlı bir JSON parser (`jq` vb.) bu geliştirme/CI ortamında
GARANTİ OLMADIĞINDAN, BU depronun KENDİ, sabit-bicimli (2 boşluk girinti, her array elemanı kendi
satırında) manifest dosyasına ÖZEL, küçük bir `awk`/`grep`/`sed` tabanlı çıkarıcı kullanır (genel
JSON syntax doğrulaması YAPMAZ - format bozulursa alt seviyede "beklenen alan bulunamadı" hatası
üretir, yine de test ÇALIŞTIRILMAZ). İki taraf ARASINDAKİ denklik bu turda DOĞRUDAN karşılaştırmayla
kanıtlanmıştır - dört profilin TAMAMI için (`fast`/`integration`/`nightly`/`release`) PowerShell ve
Bash BİREBİR AYNI filtre string'ini üretti:

```
fast:        (Domain=EBelge&TestLevel=Unit)|(Domain=EBelge&TestLevel=Contract)
integration: (Domain=EBelge&TestLevel=Unit)|(Domain=EBelge&TestLevel=Contract)|(Domain=EBelge&TestLevel=SqlIntegration)|(Domain=EBelge&TestLevel=CryptoIntegration)
nightly:     (Domain=EBelge&TestLevel=Unit)|(Domain=EBelge&TestLevel=Contract)|(Domain=EBelge&TestLevel=SqlIntegration)|(Domain=EBelge&TestLevel=SidecarIntegration)|(Domain=EBelge&TestLevel=CryptoIntegration)|(Domain=EBelge&TestLevel=WorkerEndToEnd)
release:     Domain=EBelge
```

Ayrıca `EBelgeTestMetadataContractTests.ProfilManifestindekiBilinenListelerKodlaSenkrondur` (bkz.
"Metadata contract testi"), manifestin `knownTestLevels`/`knownDependencies` dizilerinin C#
whitelist'iyle HER ZAMAN senkron kaldığını OTOMATİK doğrular.

## Dependency fail-closed politikası (Faz 2B.9.1)

Önceki (Faz 2B.9) davranış: SQL bağlantısı YOKSA script yalnız UYARI verip testlerin sessizce
ATLANMASINA izin veriyordu - bu, "ağır testlerin çoğu skip edildi ama exit code 0" riskini taşıyordu.

**Yeni davranış**: `integration`/`nightly`/`release` için manifestteki `requiredDependencies`
listesindeki HER bir dependency, ANA test koşumundan ÖNCE bir PREFLIGHT ile doğrulanır - preflight
başarısız/eksikse script `dotnet test`'i HİÇ ÇALIŞTIRMADAN, non-zero exit code ile DURUR ("yeşile
zorlanmaz").

- **SqlServer**: önce `STYS_INTEGRATION_TEST_CONNECTION_STRING` ortam değişkeninin DOLU olduğu
  kontrol edilir (yoksa hemen fail); doluysa `EBelgeSqlSidecarPreflightTests.SqlServerTestVeriTabaniErisilebilirdir`
  (`Purpose=SqlPreflight` filtresiyle) çalıştırılır - bu test e-belge SQL entegrasyon testlerinin
  ZATEN kullandığı AYNI yolu (`SatisBelgesiMuhasebeTestSupport.CreateDbContext` + EF Core SqlServer
  provider + `Database.CanConnectAsync()`) kullanır; script kendi ham SqlClient/TCP kontrolünü İCAT
  ETMEZ. Baglantı dizesi HİÇBİR ZAMAN loglanmaz.
- **JavaSidecar**: `EBelgeSqlSidecarPreflightTests.JavaSchematronSidecarBaslatilabilirVeHazirOlur`
  (`Purpose=JavaSidecarPreflight`) çalıştırılır - `SchematronSidecarProcessFixture`'ın KENDİ, KISA
  ÖMÜRLÜ bir örneğini başlatıp `BaseUrl`'ün dolu olduğunu (yani java bulunabildiğini, sınıflar
  derlenmiş olduğunu, sürecin `/health/ready` VERDİĞİNİ) kanıtlayıp HEMEN kapatır - ana test
  koşumunun kendi fixture'larıyla ASLA eş zamanlı ÇALIŞMAZ (sıralı bir ön-prob). Java path/env
  ayrıntısı loglanmaz.
- **Cryptography**: gerçek RSA test sertifikası bellekte üretildiğinden (harici servis/dosya
  bağımlılığı YOK) ayrı bir preflight GEREKMEZ.

`release` profili AYRICA `requireCriticalInvariants: true` taşır - ana koşumdan ÖNCE
`EBelgeTestMetadataContractTests` filtresiyle kritik invariant manifestinin TAMAMININ karşılığı olan
en az bir testin var olduğu doğrulanır; başarısızsa `release` DURUR.

## Sıfır skipped test politikası (Faz 2B.9.1)

`dotnet test`'in KENDİ exit code'u, TÜM eşleşen testler `Skip` (xUnit `[Fact(Skip=...)]`/
`IntegrationFactAttribute`) OLSA BİLE `0` döner - "skip = başarı" YANILGISINI ÖNLEMEK için script
HER ZAMAN TRX sonucunun `<Counters>` elemanını (`total`/`passed`/`failed`/`notExecuted`) PARSE EDER
ve `notExecuted`'i (xUnit'in Skip için ürettiği TRX outcome) "Skipped" olarak RAPORLAR:

- **fast**: mevcut meşru skip politikası (dependency yoksa `IntegrationFactAttribute` skip eder)
  KORUNUR - `fast`'in KENDİSİ hiçbir dependency GEREKTİRMEDİĞİNDEN pratikte skip OLMAZ, ama
  `failOnSkippedTests: false` olduğundan varsa dahi skip sayısı yalnız GÖRÜNÜR raporlanır, profil
  BAŞARISIZ SAYILMAZ.
- **integration/nightly/release**: `failOnSkippedTests: true` - preflight'lar dependency'nin
  GERÇEKTEN var olduğunu ZATEN kanıtladığı için, ana koşumda `Skipped > 0` çıkarsa bu ARTIK
  "dependency yok" değil, "GERÇEK bir tutarsızlık" (ör. preflight sonrası bağlantı KOPTU) anlamına
  gelir - script bunu `dotnet test`'in KENDİ exit code'undan BAĞIMSIZ olarak profili BAŞARISIZ
  SAYAR (`FINAL_EXIT=1`).

## PowerShell/Bash eşdeğerliği (Faz 2B.9.1)

İki script AYNI manifesti, AYNI dependency politikasını (preflight sırası: SqlServer -> JavaSidecar
-> [release ise] kritik invariant), AYNI skip politikasını VE AYNI exit-code davranışını uygular.
Kanıt: 4 profilin TAMAMI (hem `--validate`/`-ValidateOnly` hem TAM koşum) her iki script'te de AYNI
sonuçları ÜRETTİ - bkz. "Dependency-negatif test sonuçları" ve "Test süreleri".

## Bash `set -e` düzeltmesi (Faz 2B.9.1)

Önceki `test-ebelge.sh`, `set -eu` kullanıyordu - `dotnet test` NORMAL bir test başarısızlığında
non-zero exit dönünce (ki bu SIRADAN bir durumdur, script hatası DEĞİLDİR), `-e` script'i SESSİZCE
sonlandırıyor, `EXIT_CODE=$?` satırı, profil özeti, TRX yolu VE skip kontrolü HİÇ ÇALIŞMIYORDU.

**Düzeltme**: `set -e` KALDIRILDI (yalnız `set -u` - tanımsız değişken kullanımını hata sayma -
KORUNDU, bu FARKLI bir güvenlik sınıfıdır). HER `dotnet test` çağrısı artık açık bir `if/then/else`
İÇİNDE çalıştırılır (`run_dotnet_test` fonksiyonu):

```sh
if dotnet test ...; then
    RUN_EXIT_CODE=0
else
    RUN_EXIT_CODE=$?
fi
```

Bu sayede TRX parse'ı, skip kontrolü ve güvenli özet HER DURUMDA (test başarılı/başarısız/preflight
hatası) çalışır.

## Dependency-negatif test sonuçları (Faz 2B.9.1)

Gerçek test SİLİNEREK negatif test YAPILMADI (görev md.12/md.24 kısıtı) - dependency'nin KENDİSİ
kontrollü biçimde geçici olarak KALDIRILDI/simüle EDİLDİ, sonra GERİ YÜKLENDİ:

| Senaryo | Yöntem | Sonuç |
|---|---|---|
| SQL env değişkeni YOK + `integration` (PowerShell) | `Remove-Item Env:\STYS_INTEGRATION_TEST_CONNECTION_STRING` | exit 1, "SqlServer dependency saglanamiyor" - `dotnet test` HİÇ ÇALIŞTIRILMADI |
| SQL env değişkeni YOK + `integration` (Bash) | `unset STYS_INTEGRATION_TEST_CONNECTION_STRING` | exit 1, aynı mesaj |
| Java sidecar YOK + `nightly` (Bash) | `sidecar/schematron-validator/out` GEÇİCİ olarak yeniden adlandırıldı (`out.bak`), preflight testi ÇALIŞTIRILDI, SONRA GERİ YÜKLENDİ | SQL preflight GEÇTİ, Java preflight `Failed: 1` ("Sidecar derlenmiş sınıfları... bulunamadı") - `nightly` exit 1, ana koşum HİÇ BAŞLAMADI |
| Bilinmeyen profil adı (`nonexistent-profile`) | doğrudan çağrı | exit 1, "bilinmeyen profil" + bilinen profil listesi |
| Kritik invariant manifestten eksik (simülasyon) | `EBelgeCriticalInvariantManifest.KnownInvariants`'a GEÇİCİ, hiçbir testin karşılamadığı sahte bir isim eklendi, contract testleri çalıştırıldı, SONRA GERİ ALINDI | `KritikInvariantManifestindekiHerBirIcinEnAzBirGecerliTestBulunur` VE `ReleaseProfiliTumKritikInvariantlariKapsar` AÇIKÇA `Failed` oldu (2/23 test) - `release` profilinin bu preflight adımı GERÇEK bir eksiklikte AYNI şekilde DURACAĞINI kanıtlar |
| PowerShell/Bash filtre denkliği | 4 profilin TAMAMI, HER İKİ script'te `--validate`/`-ValidateOnly` ile çalıştırıldı | Üretilen filtre string'i 4/4 profilde BİREBİR AYNI (bkz. "Tek merkezi profil manifesti") |

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
raporlanan geçici bağlantı sıfırlanması sorunu, Faz 2B.9.1'de BİR KEZ GERÇEKTEN GÖZLEMLENDİ: bir
`nightly` koşumunda `EBelgeSchematronSidecarIntegrationTests.BuyukXmlLimitteReddedilir`
`ConnectionReset`/soket okuma hatasıyla BAŞARISIZ oldu (bkz. "Flaky test politikası" - bu turda
BULUNAN 2. kayıt). Kök neden HALA kesin olarak İZOLE EDİLEMEDİ (hedefli, preflight-sonrası-tekrar
üretim denemesi başarısız oldu - tekrar üretilmedi; TAM `nightly` koşumu HEMEN sonra tekrar
çalıştırıldığında 486/486 TEMİZ geçti) - en olası açıklama YİNE bu bölümde açıklanan, birden fazla
JVM'in AYNI ANDA (nightly'nin TÜM SqlIntegration+SidecarIntegration+CryptoIntegration+WorkerEndToEnd
yükü altında) çalışabilme İHTİMALİDİR. Test yürütme süresi görece düşük olduğundan (nightly ~150-220
sn, sidecar testleri içinde en yavaşı ~7-8 sn) VE tek, tekrar üretilemeyen bir olay OLDUĞUNDAN bu
turda KOD DEĞİŞİKLİĞİ (serileştirme) YAPILMADI - **öneri AYNEN GEÇERLİ**: eğer ileride bu risk DAHA
SIK gözlemlenirse, en düşük riskli çözüm `SchematronSidecarProcessFixture`'a bir dosya-tabanlı
(`FileStream` + `FileShare.None`) process-level kilit eklemektir (xUnit collection yapısını hiç
değiştirmeden, yalnız fixture `InitializeAsync` içinde).

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

### Faz 2B.9.1'de gözlemlenen, KÖK NEDENİ kesin izole edilemeyen 1 flaky olay

- **Test**: `EBelgeSchematronSidecarIntegrationTests.BuyukXmlLimitteReddedilir` (`TestLevel=SidecarIntegration`)
- **Bağımlılık**: JavaSidecar (gerçek Saxon-HE süreci, büyük XML gövdesi göndererek boyut sınırını test eder)
- **Hata tipi**: `System.Net.Http.HttpRequestException` -> `IOException` -> `SocketException`
  (`ConnectionReset`) - sidecar'a büyük gövde YAZILIRKEN bağlantı KOPTU.
- **Olası shared-state nedeni**: `nightly` profilinin TAM yükü altında (SqlIntegration + SidecarIntegration
  + CryptoIntegration + WorkerEndToEnd testleri, birden fazlası KENDİ bağımsız sidecar sürecini
  başlatabilir - bkz. "Sidecar izolasyonu") birden fazla JVM'in AYNI ANDA CPU/G-Ç için YARIŞMASI,
  BÜYÜK bir HTTP gövdesi gönderiminin zamanlamaya en DUYARLI test OLMASI.
- **Çözüm**: Bu turda KOD DEĞİŞİKLİĞİ yapılmadı (bkz. "Sidecar izolasyonu" - tek, tekrar üretilemeyen
  olay; agresif serileştirme KENDİSİ yeni bir performans/karmaşıklık maliyeti taşır). Retry/skip/
  assertion gevşetme KULLANILMADI.
- **Tekrar üretim adımları**: (1) hedefli deneme: SQL+Java preflight'ları sırayla çalıştırıp HEMEN
  ardından yalnız `EBelgeSchematronSidecarIntegrationTests` sınıfını çalıştırmak - TEKRAR
  ÜRETİLEMEDİ (14/14 geçti); (2) `EBelgeSchematronSidecarIntegrationTests`'i TEK BAŞINA çalıştırmak -
  TEKRAR ÜRETİLEMEDİ (14/14 geçti, 23 sn); (3) TAM `nightly` profilini HEMEN tekrar çalıştırmak -
  TEKRAR ÜRETİLEMEDİ (486/486 geçti). Sonuç: yalnız TAM nightly yükü altında, KONTROLLÜ bir tekrar
  üretim senaryosu KURULAMADI - olay şu an İZOLE, tek seferlik olarak KAYITLIDIR.

Bu iki kayıt DIŞINDA, bu turda alınan profil koşumlarında (fast x2, integration x3, nightly x2 +
hedefli tekrar denemeleri, release x1, her ikisi de PowerShell+Bash ile) başka hiçbir flaky/aralıklı
başarısızlık GÖZLEMLENMEDİ.

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

4 profilin Faz 2B.9.1'de alınan (yeni metadata contract testleri + dependency preflight adımları
DAHİL) koşum sonuçları - TRX dosyaları `tests/STYS.Tests/TestResults/ebelge-<profil>-<zaman
damgası>.trx` altında saklanmıştır:

| Profil | Test sayısı | Süre (ana koşum) | Preflight süresi | External dependency | Başarısız | Atlanan |
|---|---:|---:|---:|---|---:|---:|
| fast | 285 | ~2-3 sn | yok | yok | 0 | 0 |
| integration | 466 | ~94-115 sn | ~8 sn (SQL+Java) | gerçek SQL Server, gerçek Java sidecar, gerçek RSA test sertifikası | 0 | 0 |
| nightly | 486 | ~140-200 sn | ~9 sn (SQL+Java) | + worker E2E | 0 | 0 |
| release | 488 | ~140-160 sn | ~9 sn (SQL+Java) + ~0.3 sn (kritik invariant) | + ReleaseGate (tam zincir) | 0 | 0 |

(285/466/486/488 = Faz 2B.9'un 263/444/464/466 taban rakamlarına + Faz 2B.9.1'in 22 yeni metadata
contract testi.)

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

**Yalnız doğrulama** (manifest + filtre + metadata sözleşmesi - dış/ağır test ÇALIŞTIRMAZ, saniyeler
içinde biter):

```powershell
./scripts/test-ebelge.ps1 fast -ValidateOnly
./scripts/test-ebelge.ps1 release -ValidateOnly
```

```sh
./scripts/test-ebelge.sh fast --validate
./scripts/test-ebelge.sh release --validate
```

## Sonraki aşama

HSM/mali mühür entegrasyonu geliştirmesi için bu faz temiz bir test zemini bırakır: yeni HSM/mali
mühür testleri aynı trait taksonomisini (`Domain=EBelge`, uygun `TestLevel`, gerekirse yeni bir
`Dependency` değeri - ör. `Hsm`) kullanarak eklenebilir; `CryptoIntegration` katmanı zaten gerçek
imza altyapısı testlerinin doğal yeri olarak KURULMUŞTUR. Faz 2B.7.1'in "Sonraki faz" listesi (PDF/
e-posta/entegratör gönderimi, frontend) bu turda DEĞİŞMEDEN geçerlidir.
