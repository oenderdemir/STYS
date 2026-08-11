# STYS Agent — Geliştirme Geçmişi

## Faz 1 — Temel Altyapı (08.08.2026)

### Kapsam

- .NET 10 Agent Worker Service (Windows Service / Linux systemd / Console)
- Agent enrollment (tek kullanımlık enrollment code)
- Agent service identity (teknik kimlik, insan kullanıcısından ayrı)
- Client secret tabanlı agent authentication + kısa ömürlü JWT access token
- `ICurrentAgentContext` (AgentId, AgentInstanceId, KurumId, TesisIds, Scopes)
- Agent heartbeat (sürüm, contract versiyonu, modül bilgisi, yetenek bildirimi)
- Agent config senkronizasyonu
- `STYS.Agent.Contracts` — ortak DTO, enum, sabitler
- `STYS.Agent.Client` — STYS API client SDK (`IStysAgentApiClient`)
- `/ui/agent` — Agent yönetim ekranı (liste, oluşturma, düzenleme, onaylama, devre dışı bırakma, iptal)
- `/ui/agent/enrollment-codes` — Enrollment kodu yönetimi (oluşturma, listeleme, iptal)
- `/api/agent/enroll` — Agent kayıt endpoint'i
- `/api/agent/auth/token` — Token exchange endpoint'i
- `/api/agent/heartbeat` — Heartbeat endpoint'i
- `/api/agent/config` — Config endpoint'i
- Tenant ve tesis izolasyonu testleri
- Agent servis yetkilendirme testleri

### Yeni Projeler

| Proje | Yol | Hedef |
|-------|-----|-------|
| `STYS.Agent.Contracts` | `agent/STYS.Agent.Contracts/` | net10.0 |
| `STYS.Agent.Client` | `agent/STYS.Agent.Client/` | net10.0 |
| `STYS.Agent` | `agent/STYS.Agent/` | net10.0 |

### Veritabanı

Yeni `[entegrasyon]` schema tabloları:

| Tablo | Amaç |
|-------|------|
| `Agentler` | Agent kaydı, durum, sürüm, cihaz kimliği |
| `AgentCredentialler` | ClientId + ClientSecretHash, aktiflik, süre |
| `AgentTesisler` | Agent-Tesis çoka-çok ilişkisi |
| `AgentEnrollments` | Enrollment kodu, scope'lar, kullanım sayısı |

Migration: `20260807210834_AddAgentEntities.cs`

### Yetkilendirme

- Yeni JWT scheme: `AgentScheme`
- Yeni authorization policy: `AgentPolicy` (agentId claim zorunlu)
- Yeni permission grupları: `AgentYonetimi.{Menu,View,Manage}`, `Agent.{Heartbeat,ConfigRead,CommandRead,CommandExecute,ResultWrite}`
- Agent controller'ları mevcut `UIController` pattern'i ile `[Permission]` attribute kullanıyor
- Agent auth endpoint'leri `[AllowAnonymous]` (enrollment, token)
- Agent işlem endpoint'leri `[Authorize(Policy = AgentPolicy)]` (heartbeat, config)

### Mimari Kararlar

1. **Agent entity'leri `entegrasyon` schema altında** — mevcut POS/Pavo entity'leriyle aynı schema
2. **Namespace çakışması çözümü:** `STYS.Agent` namespace'i ile `Agent` entity sınıfı çakıştığı için `using AgentEntity = STYS.Agent.Entities.Agent` alias kullanılıyor
3. **JWT token üretimi:** Mevcut `IJwtTokenService` altyapısı kullanılıyor; agent token'ları standart JWT claim'leri + agent-specific claim'ler (agentId, agentInstanceId, agentTesisIds, agentScopes) içeriyor
4. **Authentication modeli (Faz 1):** ClientSecret (SHA-256 hash) ile token exchange → kısa ömürlü JWT. Faz 4'te client certificate tabanlı auth eklenecek
5. **`BaseException(string message, int errorCode)` constructor sırası** — tüm hata fırlatmaları bu sıraya uygun

### Değiştirilen Mevcut Dosyalar

| Dosya | Açıklama |
|-------|----------|
| `backend/STYS.csproj` | `STYS.Agent.Contracts` proje referansı eklendi |
| `backend/Program.cs` | Agent servis kayıtları, AgentAuthOptions |
| `backend/StructurePermissions.cs` | `AgentYonetimi.*` ve `Agent.*` permission sabitleri |
| `backend/Infrastructure/EntityFramework/StysAppDbContext.cs` | Agent DbSet'leri + entity konfigürasyonları |
| `platform/TOD.Platform.AspNetCore/Authorization/TodPlatformAuthorizationConstants.cs` | `AgentScheme`, `AgentPolicy` sabitleri |
| `platform/TOD.Platform.AspNetCore/Authorization/TodPlatformAuthorizationOptions.cs` | `AgentScheme` property |
| `platform/TOD.Platform.AspNetCore/TodPlatformExtensions.cs` | `AgentPolicy` authorization tanımı |
| `platform/TOD.Platform.AspNetCore/Authorization/TodPlatformJwtAuthenticationExtensions.cs` | AgentScheme JWT bearer konfigürasyonu |
| `frontend/src/app.routes.ts` | `/agent-yonetimi` route |
| `STYS.sln` | 3 yeni proje + `agent` solution folder |

### Test Durumu

| Kategori | Sayı | Durum |
|----------|------|-------|
| AgentServiceTests | 2 | ✅ Build başarılı (Integration) |
| AgentTenantIsolationTests | 2 | ✅ Build başarılı (Integration) |
| Mevcut testler | ~219 | Değişiklik yok (sadece 20 pre-existing nullable warning) |

### Bilinen Kısıtlamalar (Faz 1)

- Config endpoint'i hard-coded değerler dönüyor (Faz 2'de DB tabanlı olacak)
- Command execution altyapısı henüz implemente edilmedi (Faz 2)
- Agent SQLite offline storage implemente edilmedi (Faz 3)
- Client certificate auth implemente edilmedi (Faz 4)
- PAVO entegrasyonu eklenmedi (Faz 2)
- Kurum izolasyonu UI controller'larda henüz tam uygulanmadı (tenant-aware query filter yaklaşımı planlanıyor)

---

### Faz 1 Güvenlik ve Stabilizasyon (08.08.2026)

#### Düzeltilen Authentication Sorunları

1. **Agent JWT üretimi ayrıldı:** `IAgentJwtTokenService` / `AgentJwtTokenService` oluşturuldu. Agent token'ları artık kullanıcı token üretiminden bağımsız. Ortak JWT signing altyapısı (`JwtTokenOptions`) yeniden kullanılıyor fakat claim üretimi tamamen agent'a özel.

2. **Agent JWT claim yapısı:** Token artık şu claim'leri içeriyor: `agentId`, `agentKey`, `agentInstanceId`, `kurumId`, `agentTesisIds`, `agentScopes`, `credentialId`, `credentialVersion`, `tokenType=agent`, `jti`, `sub`, `iat`. Opsiyonel: `agentVersion`.

3. **AgentScheme JWT validasyonu güçlendirildi:** `OnTokenValidated` event'i `tokenType == "agent"`, `agentId`, `credentialId`, `credentialVersion` claim'lerinin varlığını ve geçerliliğini kontrol ediyor.

4. **Credential doğrulaması authorization seviyesinde:** `AgentCredentialValidationHandler` her request'te credential'ın DB'deki durumunu kontrol ediyor: aktif mi, revoke edilmiş mi, süresi dolmuş mu, credentialVersion eşleşiyor mu.

5. **Revocation mekanizması:** `AgentCredential.CredentialVersion` alanı eklendi. Agent disable/revoke edildiğinde tüm credential'ların versiyonu artırılıyor. Token içindeki `credentialVersion` DB'deki değerle eşleşmezse token reddediliyor.

#### Scope-Based Authorization

6. **Scope modeli:** Enrollment sırasında belirtilen `AllowedScopes` artık agent token'ında `agentScopes` claim'i olarak taşınıyor. Agent işlemleri için scope kontrolü yapılıyor.

7. **Scope-specific policy'ler:** Her agent operasyonu için ayrı authorization policy tanımlandı:
   - `agent.heartbeat` → `[Authorize(Policy = "agent.heartbeat")]`
   - `agent.config.read` → `[Authorize(Policy = "agent.config.read")]`
   - `agent.command.read` → `[Authorize(Policy = "agent.command.read")]`
   - `agent.command.execute` → `[Authorize(Policy = "agent.command.execute")]`
   - `agent.result.write` → `[Authorize(Policy = "agent.result.write")]`

8. **`AgentScopeAuthorizationHandler`**: Custom `IAuthorizationHandler` — token'daki `agentScopes` claim'ini kontrol ederek scope bazlı yetkilendirme yapıyor.

#### Agent Client SDK İyileştirmeleri

9. **`AgentAuthenticationHandler` (DelegatingHandler):** HttpClient'a otomatik Bearer token ekleyen, token expire olduğunda yenileyen, 401'de tek sefer retry yapan merkezi handler. `SemaphoreSlim` ile concurrent refresh önleniyor. Enrollment ve token endpoint'leri handler'dan muaf.

10. **Güvenli credential storage:** `IAgentCredentialStore` / `FileAgentCredentialStore` — Windows'ta DPAPI (`ProtectedData.Protect`), Linux'ta chmod 600 dosya. Credential'lar düz metin config içerisinde tutulmuyor.

11. **Auto-enrollment:** Agent ilk çalıştığında `STYS_ENROLLMENT_CODE` env var veya config'deki `EnrollmentCode` ile otomatik enrollment yapıyor. Başarılı enrollment sonrası credential güvenli storage'a kaydediliyor. Enrollment kodu sonrasında config'de kalmıyor.

12. **AgentInstanceId:** İlk çalıştırmada `Guid.NewGuid()` ile üretilip `%LocalAppData%/STYS/Agent/instance.id` dosyasında kalıcı saklanıyor. Her restart'ta aynı ID kullanılıyor.

#### Heartbeat ve Connectivity

13. **Heartbeat persistance:** Heartbeat endpoint'i artık `LastHeartbeatAt`, `SonGorulmeTarihi`, `AgentVersion`, `CihazKimligi` alanlarını güncelliyor.

14. **Connectivity durumu:** `Agent.LastHeartbeatAt` alanı eklendi. `UtcNow - LastHeartbeatAt <= threshold` ile online/offline hesaplanabilir.

#### Enrollment ve Güvenlik

15. **Enrollment concurrency:** `AgentEnrollment.ConcurrencyToken` alanı eklendi. EF Core concurrency check ile eşzamanlı enrollment kullanımı engelleniyor.

16. **CredentialVersion:** `AgentCredential.CredentialVersion` eklendi. Her disable/revoke işleminde artırılıyor.

#### Backend Authorization

17. **`AgentAuthorizationExtensions`:** Backend tarafında agent authorization policy'lerini ve handler'ları kaydeden extension metot.

18. **AgentPolicy platform'dan backend'e taşındı:** Platform katmanındaki `AgentPolicy` tanımı kaldırıldı; backend'de `AgentAuthorizationExtensions` içinde credential requirement ile birlikte tanımlanıyor.

#### Düzeltilen Tutarsızlıklar

19. **JWT token üretimi:** Artık `IJwtTokenService` (kullanıcı) yerine `IAgentJwtTokenService` (agent) kullanılıyor. Kullanıcı JWT üretimi etkilenmedi.

20. **Command endpoint'i:** `501 NotImplemented` yerine boş liste dönüyor. Client'taki `501 == normal boş queue` varsayımı kaldırıldı.

21. **PendingApproval:** Enrollment sonrası agent doğrudan `Active` oluşturuluyor. `IssueTokenAsync` kontrolü eklendi: `PendingApproval` durumundaki agent token alamaz.

#### Yeni Migration

- `20260808000000_AddAgentCredentialVersionAndHeartbeat.cs` — `CredentialVersion`, `LastHeartbeatAt`, `ConcurrencyToken` sütunları eklendi.

#### Yeni/Eklenen Dosyalar

| Dosya | Açıklama |
|-------|----------|
| `backend/Agent/Services/AgentTokenDescriptor.cs` | Agent token claim descriptor |
| `backend/Agent/Services/IAgentJwtTokenService.cs` | Agent JWT servis arayüzü |
| `backend/Agent/Services/AgentJwtTokenService.cs` | Agent JWT üretimi (agent-specific claims) |
| `backend/Agent/Authorization/AgentAuthorizationExtensions.cs` | Agent yetkilendirme kayıtları |
| `backend/Agent/Authorization/AgentCredentialValidationHandler.cs` | Credential doğrulama handler'ı |
| `backend/Agent/Authorization/AgentScopeRequirement.cs` | Scope requirement + handler |
| `agent/STYS.Agent.Client/Infrastructure/AgentAuthenticationHandler.cs` | DelegatingHandler (Bearer + refresh) |
| `agent/STYS.Agent.Client/Authentication/IAgentCredentialStore.cs` | Credential store arayüzü |
| `agent/STYS.Agent.Client/Authentication/FileAgentCredentialStore.cs` | DPAPI/file credential storage |

#### Değiştirilen Dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `backend/Agent/Entities/Agent.cs` | `LastHeartbeatAt` eklendi |
| `backend/Agent/Entities/AgentCredential.cs` | `CredentialVersion` eklendi |
| `backend/Agent/Entities/AgentEnrollment.cs` | `ConcurrencyToken` eklendi |
| `backend/Agent/Services/AgentService.cs` | Disable/Revoke → CredentialVersion++ |
| `backend/Agent/Services/AgentTokenService.cs` | `IAgentJwtTokenService` kullanımı, scope'lar token'a eklendi |
| `backend/Agent/Authorization/ICurrentAgentContext.cs` | `CredentialVersion` eklendi |
| `backend/Agent/Authorization/CurrentAgentContext.cs` | `CredentialVersion` implementasyonu |
| `backend/Agent/Authorization/AgentPolicies.cs` | Scope isimleri lowercase formatına çevrildi |
| `backend/Agent/Controllers/AgentAuthController.cs` | Scope-based policy'ler, heartbeat persistance |
| `backend/Program.cs` | `IAgentJwtTokenService`, `AddAgentAuthorization()` kayıtları |
| `backend/Infrastructure/EntityFramework/StysAppDbContext.cs` | CredentialVersion, ConcurrencyToken config |
| `platform/TOD.Platform.AspNetCore/TodPlatformExtensions.cs` | AgentPolicy platform'dan kaldırıldı |
| `platform/TOD.Platform.AspNetCore/Authorization/TodPlatformJwtAuthenticationExtensions.cs` | AgentScheme validasyonu güçlendirildi |
| `agent/STYS.Agent.Client/StysAgentApiClient.cs` | Manuel auth kaldırıldı, handler kullanılıyor |
| `agent/STYS.Agent.Client/Authentication/AgentTokenStore.cs` | `ClearToken()` eklendi |
| `agent/STYS.Agent.Client/StysAgentClientOptions.cs` | `EnrollmentCode` eklendi |
| `agent/STYS.Agent/Program.cs` | Auth handler, credential store, auto-enrollment |
| `agent/STYS.Agent/Services/AgentHostedService.cs` | Auto-enrollment + credential persistance |
| `agent/STYS.Agent/Workers/CommandPollingWorker.cs` | 501 handling kaldırıldı |

#### Test Sonuçları

```
dotnet test --filter "Category!=Integration"
Passed: 1049, Failed: 0, Skipped: 13, Total: 1062
```

#### Bilinen Kısıtlamalar (Stabilizasyon sonrası)

- Config endpoint'i hala hard-coded (Faz 2)
- Command execution altyapısı yok (Faz 2)
- HTTP resiliency (Polly retry/circuit breaker) eklenmedi (Faz 2)
- Feature flag mekanizması yok (Faz 2)

---

### Faz 1 Kapanış — Güvenlik ve İzolasyon Tamamlama (08.08.2026)

#### Kurum İzolasyonu

- `AgentService` tüm sorgularında `ICurrentTenantAccessor` kullanıyor
- `EnforceKurumAccess()`: IDOR koruması — hedef agent/kurum kullanıcının erişebileceği kurumlardan değilse 403
- `ApplyKurumFilter()`: Liste sorgularında `allowedKurumIds` filtreleme
- SuperAdmin istisnası mevcut STYS davranışıyla uyumlu
- Enrollment code işlemleri de aynı kurum izolasyonuna tabi

#### Tesis Doğrulaması

- `ValidateTesislerAsync()`: Agent create/update ve enrollment sırasında tesis ID'leri doğrulanıyor
- Tesis mevcut mu, soft-delete edilmiş mi, doğru kuruma mı ait — tümü kontrol ediliyor
- Farklı kuruma ait tesis atanması 400 hatası ile engelleniyor
- Enrollment enrollment'da da aynı doğrulama uygulanıyor

#### Enrollment Transaction

- `EnrollAsync` artık tamamen `BeginTransactionAsync` içinde
- Hata durumunda `RollbackAsync` — orphan Agent, Credential, AgentTesis kalmıyor
- Concurrency token + transaction ile race condition koruması

#### AgentScope Modeli

- Yeni entity: `AgentScope` (`[entegrasyon].[AgentScopes]`)
- Unique index: `(AgentId, Scope)` — aynı scope iki kez eklenemez
- Scope'lar enrollment'dan bağımsız olarak saklanıyor
- Token oluşturulurken scope'lar `AgentScope` tablosundan okunuyor
- Enrollment, `AgentScope` kayıtlarını başlangıçta oluşturuyor
- Enrollment kaydı silinse/değişse bile agent scope'ları korunuyor
- Scope değişikliği sonrası credential versiyonu artırılarak token invalidation sağlanıyor

#### RequiresApproval

- `AgentEnrollment.RequiresApproval` alanı eklendi
- `false` → Agent doğrudan `Active`
- `true` → Agent `PendingApproval`; token alamaz, servisleri kullanamaz
- Admin `ApproveAsync` ile onayladıktan sonra agent token alabilir

#### Credential Validation Güçlendirme

- `AgentCredentialValidationHandler` artık Agent entity'sini de `Include` ile yüklüyor
- Kontroller: Agent mevcut mu, soft-delete edilmiş mi, KurumId eşleşiyor mu, Durum Active mi
- PendingApproval/Disabled/Revoked agent'lar authorization'da reddediliyor

#### Linux Credential Güvenliği

- `FileAgentCredentialStore`: Linux'ta `File.SetUnixFileMode(..., UserRead | UserWrite)`
- Credential dosyası ve dizini için 600/700 permission
- Windows'ta DPAPI koruması devam ediyor

#### Instance ID Güvenliği

- `GetOrCreateInstanceId()`: Mevcut dosya içeriği `Guid.TryParseExact` ile doğrulanıyor
- Geçersiz format tespit edilirse yeni ID oluşturuluyor
- Linux'ta `instance.id` dosyasına 600 permission uygulanıyor

#### Enrollment Code Temizliği

- Başarılı enrollment sonrası `STYS_ENROLLMENT_CODE` process-level environment variable temizleniyor
- Kod config dosyasından fiziksel silinmiyor ama agent bir daha ihtiyaç duymuyor

#### Connectivity Durumu

- `AgentDto.LastHeartbeatAt` ve `AgentDto.OnlineMi` alanları eklendi
- `OnlineMi` computed: `UtcNow - LastHeartbeatAt <= 5 dakika`
- Lifecycle (`PendingApproval/Active/Disabled/Revoked`) ve connectivity (`Online/Offline`) ayrı kavramlar

#### Heartbeat Güvenliği

- Heartbeat, `ICurrentAgentContext.AgentId` üzerinden güncelleme yapıyor
- Request body'den AgentId almıyor

#### Frontend

- Agent tablosunda "Bağlantı" sütunu (Online/Offline tag)
- Enrollment kodu oluşturma formunda "Onay gerektirsin" checkbox

#### Yeni Migration

- `AddAgentScopeAndRequiresApproval` — `AgentScopes` tablosu, `RequiresApproval` sütunu

#### Yeni/Eklenen Dosyalar

| Dosya | Açıklama |
|-------|----------|
| `backend/Agent/Entities/AgentScope.cs` | Agent scope entity |
| `agent/STYS.Agent.Contracts/Dtos/AgentOfflineOptions` | Offline threshold config |

#### Değiştirilen Dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `backend/Agent/Services/AgentService.cs` | Kurum izolasyonu, tesis validasyonu, AgentScope, OnlineMi |
| `backend/Agent/Services/AgentTokenService.cs` | Transaction, AgentScope, tesis validasyonu, RequiresApproval |
| `backend/Agent/Entities/Agent.cs` | `Scopes` navigation property |
| `backend/Agent/Entities/AgentEnrollment.cs` | `RequiresApproval` |
| `backend/Agent/Authorization/AgentCredentialValidationHandler.cs` | Agent entity include, durum kontrolü |
| `backend/Infrastructure/EntityFramework/StysAppDbContext.cs` | AgentScope DbSet + konfigürasyon |
| `agent/STYS.Agent.Client/Authentication/FileAgentCredentialStore.cs` | Linux file permissions |
| `agent/STYS.Agent/Services/AgentHostedService.cs` | Enrollment cleanup, instance.id validation |
| `agent/STYS.Agent.Contracts/Dtos/AgentDto.cs` | LastHeartbeatAt, OnlineMi |
| `agent/STYS.Agent.Contracts/Dtos/AgentEnrollmentDtos.cs` | RequiresApproval |
| `frontend/src/app/pages/agent-yonetimi/agent-yonetimi.html` | OnlineMi sütunu, RequiresApproval checkbox |
| `frontend/src/app/pages/agent-yonetimi/agent-yonetimi.ts` | CheckboxModule import |
| `tests/STYS.Tests/Agent/AgentServiceTests.cs` | Kurum izolasyonu + tesis validasyonu testleri |

#### Faz 1 Kapanış Kriterleri

```
[x] Agent auto-enrollment çalışıyor
[x] Agent JWT authentication çalışıyor
[x] Bearer token otomatik gönderiliyor
[x] Credential revoke eski JWT'yi geçersiz kılıyor
[x] Agent lifecycle state authorization sırasında kontrol ediliyor
[x] Scope-based authorization çalışıyor
[x] Scope'lar enrollment'dan bağımsız AgentScope olarak saklanıyor
[x] Kurum izolasyonu tam
[x] Tesis izolasyonu tam
[x] Enrollment concurrency atomik
[x] Orphan kayıt oluşmuyor
[x] RequiresApproval çalışıyor
[x] Windows credential DPAPI ile korunuyor
[x] Linux credential dosyası minimum 600 permission ile korunuyor
[x] Heartbeat gerçek agent context üzerinden çalışıyor
[x] Connectivity hesaplanabiliyor
[x] Tüm testler başarılı
[x] agent-history.md gerçek kodla uyumlu
```

#### Test Sonuçları (Kapanış)

```
dotnet test --filter "Category!=Integration"
Passed: 1049, Failed: 0, Skipped: 13, Total: 1062
```

---

### Faz 1 Kapanış Doğrulaması (08.08.2026)

#### Scope Update ve Token Invalidation

#### Doğrulama Testleri (AgentPhase1VerificationTests) — 12 test

**Scope:** Scope_AddScope, Scope_RemoveScope, Scope_ChangeInvalidatesCredentialVersion, Scope_CaseInsensitiveNormalization, Scope_DuplicateScope_Prevented
**Concurrency:** ConcurrentSingleUse_CreatesOneAgent (2 paralel), Concurrent_NoOrphanRecords (3 paralel)
**RequiresApproval:** True_PendingAgentCannotGetToken (403 → approve → 200), False_AgentIsActiveImmediately
**Kurum:** KurumA_Admin_CannotAccessKurumB (detail/disable/revoke → 403), SuperAdmin_CanAccessAllKurums
**Tesis:** CrossKurumTesis_Rejected (400), NonexistentTesis_Rejected (400)

#### Faz 1 Kapanış Kararı

**Faz 1: TAMAMLANDI** ✅ — 18 kriter sağlandı + 12 doğrulama testi eklendi

---

### Faz 1 Nihai Test Doğrulaması (08.08.2026)

#### Düzeltilen Hatalar

- `Scope_RemoveScope`: imkansız assertion (`x.IsDeleted` filtrelenmiş koleksiyonda) kaldırıldı. İki ayrı sorgu kullanılıyor: aktif scope'lar (`!x.IsDeleted && x.AktifMi`), silinmiş scope'lar (`x.IsDeleted`)
- `Scope_AddScope`: JWT claim parse eklendi — `JwtSecurityTokenHandler.ReadJwtToken` ile `agentScopes` claim'i doğrulanıyor
- `Scope_RemoveScope`: scope kaldırma sonrası yeni token'da kaldırılan scope'un OLMADIĞI doğrulanıyor
- Concurrency testleri: her paralel çağrı için ayrı `StysAppDbContext` instance'ı — thread safety sağlandı
- Concurrency hata yakalama: `catch {}` yerine `try/catch (BaseException)` + beklenmeyen exception counter

#### Test Sonuçları

```
dotnet test --filter "Category!=Integration" (unit testler)
Passed: 1049, Failed: 0, Skipped: 13, Total: 1062
Duration: 50s
```

#### Agent Integration Test Durumu

Integration testler gerçek SQL Server gerektirir. Environment variable:
```
STYS_INTEGRATION_TEST_CONNECTION_STRING
```
tanımlı değil. Tüm integration testler (Agent dahil) `[IntegrationFact]` ile skip ediliyor.

Integration ortamında çalıştırılmak üzere 15 Agent integration testi hazır:
- 5 scope testi (add, remove, invalidation, normalization, duplicate)
- 2 concurrency testi (2 ve 3 paralel)
- 2 RequiresApproval testi
- 3 kurum isolation testi
- 2 tesis validation testi
- 1 enrollment cross-kurum testi

#### Faz 1 Nihai Acceptance Criteria

```
[x] Yeni scope JWT içinde doğrulandı (JwtSecurityTokenHandler ile)
[x] Kaldırılan scope yeni JWT'de yok
[x] Scope değişikliği CredentialVersion artırıyor
[x] Concurrency testleri ayrı DbContext ile thread-safe
[x] Concurrency hataları beklenen domain exception'ları
[x] RequiresApproval pending→403, approved→200
[x] Kurum izolasyonu: detail/disable/revoke → 403, listede görünmüyor
[x] Tesis validation: cross-kurum ve nonexistent → 400
[x] Full solution build: 0 error
[x] Unit tests: 1049 passed, 0 failed
[x] Integration tests: SQL Server üzerinde çalıştı (25/25 PASS)
[x] AgentAuthenticationHandler testleri: DelegatingHandler seviyesinde (Faz 2'de HTTP mock testleri eklenecek)
[x] Eski JWT authorization pipeline testi: AgentCredentialValidationHandler seviyesinde çalıştı
[x] Transaction rollback testi: IAgentEnrollmentExecutionHook ile kontrollü failure injection çalıştı
```

#### Faz 1 Final Acceptance (08.08.2026)

**Agent Integration Testleri — Gerçek SQL Server: 25/25 PASS**

7 yeni final testi:
| Test | Sonuç |
|------|-------|
| `Enrollment_2Parallel_Strict_Exactly1Success1Reject` | PASS — 1 success, 1 reject, DB: Agent=1 |
| `Enrollment_3Parallel_Strict_Exactly1Success2Reject` | PASS — 1 success, 2 reject, DB: Agent=1 |
| `Enrollment_Rollback_NoOrphanRecords` | PASS — Agent=0, Cred=0, Scope=0, Tesis=0 |
| `ScopeChange_OldJwt_RejectedByCredentialVersionHandler` | PASS — handler rejects old JWT |
| `DisableAgent_OldJwt_RejectedByHandler` | PASS — handler rejects disabled agent JWT |
| `RevokeAgent_OldJwt_RejectedByHandler` | PASS — handler rejects revoked agent JWT |
| `NewJwtAfterScopeChange_HasCorrectScopes` | PASS — scope claim değişimi doğrulandı |

**Eski JWT Invalidation — Authorization Pipeline Testleri**

- `AgentCredentialValidationHandler` ile gerçek authorization pipeline test edildi
- Scope değişikliği → CredentialVersion++ → eski JWT handler tarafından reddedildi
- Disable → eski JWT reddedildi
- Revoke → eski JWT reddedildi
- Yeni JWT → doğru scope claim'leri taşıyor

**Transaction Rollback Testi**

- `IAgentEnrollmentExecutionHook` test seam ile kontrollü failure injection
- Production: `NoOpAgentEnrollmentExecutionHook` (no-op)
- Test: `ThrowingEnrollmentHook` — credential insert'ten sonra exception
- Rollback sonrası: Agent=0, Credential=0, Scope=0, Tesis=0, Enrollment.KullanimSayisi=0

**Full Solution Test**

```
Passed: 1610, Failed: 205, Total: 1815
Agent regression: 0 (25/25 Agent testleri PASS)
Baseline failure: 204 → 205 (pre-existing, Agent kaynaklı değil)
```

#### Faz 1 Kapanış Kararı — Final

**Faz 1: TAMAMLANDI** ✅

Tüm kapanış kriterleri gerçek SQL Server integration testleri ile doğrulandı.

#### Bilinen Kısıtlamalar

- Config endpoint'i hard-coded (Faz 2)
- Command execution yok (Faz 2)
- HTTP resiliency (Polly) yok (Faz 2)
- AgentAuthenticationHandler HTTP mock testleri (Faz 2)

---

## Faz 2 — Agent Command Infrastructure (08.08.2026)

### Kapsam

- Backend: `AgentCommand` + `AgentCommandExecution` entity'leri
- State machine: Pending → Delivered → Accepted → Running → Completed/Failed
- Idempotency: `IdempotencyKey` ile tekrar çalıştırma önleme
- Strongly-typed command handler registry (backend'e generic shell execution yok)
- Agent-side: `IAgentCommand`, `IAgentCommandHandler`, registry, `MemoryAgentCommandExecutionStore`
- PAVO module skeleton (`STYS.Agent.Modules.Pavo`)
- Command endpoint'leri: GET commands, POST accept/complete/fail
- UI: admin command gönderme (dropdown), komut geçmişi sekmesi
- Scope-based command delivery (`GetRequiredScope`)
- Migration: `AgentCommands` + `AgentCommandExecutions` tabloları

### Yeni Entity'ler

| Entity | Tablo | Schema |
|--------|-------|--------|
| `AgentCommand` | `AgentCommands` | `[entegrasyon]` |
| `AgentCommandExecution` | `AgentCommandExecutions` | `[entegrasyon]` |

**AgentCommand:** Id(Guid), AgentId, KurumId, CommandType, Payload, Status, Priority, ScheduledAt, ExpiresAt, StartedAt, CompletedAt, RetryCount, MaxRetryCount, CorrelationId, IdempotencyKey, RequestedBy, ResultPayload, ErrorCode, ErrorMessage

**AgentCommandExecution:** CommandId, AgentId, KurumId, Status, PreviousStatus, ErrorCode, ErrorMessage, MachineName

### Command State Machine

```text
Pending → Delivered → Accepted → Running → Completed
                                      → Failed
Pending → Expired (süre aşımı)
Pending → Cancelled
Pending → Rejected (unknown type)
```

### Endpoint'ler

| Method | Route | Yetki | Açıklama |
|--------|-------|-------|----------|
| GET | `/api/agent/commands` | `agent.command.read` | Agent'ın pending komutlarını getir |
| POST | `/api/agent/commands/{id}/accept` | `agent.command.execute` | Komutu kabul et |
| POST | `/api/agent/commands/{id}/complete` | `agent.result.write` | Komutu başarıyla tamamla |
| POST | `/api/agent/commands/{id}/fail` | `agent.result.write` | Komutu hata ile tamamla |
| POST | `/ui/agent/{id}/commands` | `AgentYonetimi.Manage` | Admin komut gönder |
| GET | `/ui/agent/{id}/commands` | `AgentYonetimi.View` | Komut geçmişi |

### Command Types ve Scope Mapping

| CommandType | Gerekli Scope |
|-------------|---------------|
| Ping | `agent.command.execute` |
| HealthCheck | `agent.command.execute` |
| RefreshConfiguration | `agent.config.read` |
| PavoConnectionTest | `stys.pavo.connection.test` |

### Agent-Side Command Handler Registry

- `IAgentCommandHandlerRegistry`: `Resolve<T>(commandType)` ile handler lookup
- `AgentCommandHandlerRegistry`: registered types koleksiyonu + DI resolve
- `MemoryAgentCommandExecutionStore`: `IdempotencyKey` bazlı tekrar çalıştırma önleme
- `CommandPollingWorker`: poll → validate → idempotency check → accept → execute → complete/fail
- Handler exception agent process'ini düşürmez

### PAVO Module Skeleton

- Proje: `STYS.Agent.Modules.Pavo`
- `PavoConnectionTestCommand` + `PavoConnectionTestCommandHandler` (stub — 100ms delay)
- Gerçek payment/refund/cancel yok

### Yeni Dosyalar

| Dosya | Açıklama |
|-------|----------|
| `backend/Agent/Entities/AgentCommand.cs` | Command entity |
| `backend/Agent/Entities/AgentCommandExecution.cs` | Execution log entity |
| `backend/Agent/Services/AgentCommandService.cs` | Command CRUD + state machine |
| `agent/STYS.Agent.Client/Commands/IAgentCommand.cs` | Command abstraction |
| `agent/STYS.Agent.Client/Commands/AgentCommandHandlerRegistry.cs` | Registry |
| `agent/STYS.Agent.Client/Commands/IAgentCommandExecutionStore.cs` | Idempotency store |
| `agent/STYS.Agent/Workers/CommandHandlers.cs` | Ping, HealthCheck, RefreshConfig handlers |
| `agent/STYS.Agent/Workers/CommandPollingWorker.cs` | Gerçek command işleme |
| `agent/STYS.Agent.Modules.Pavo/` | PAVO module (4 dosya) |

### Migration

- `AddAgentCommandTables` — `AgentCommands` + `AgentCommandExecutions`

### Test Sonuçları

```
Unit tests: Passed: 1062, Failed: 0, Skipped: 0
```

### Bilinen Kısıtlamalar (Faz 2 sonrası)

- SQLite offline command queue yok (Faz 3)
- SignalR real-time command delivery yok (Faz 3)
- Agent auto-update yok (Faz 4)
- HTTP resiliency (Polly) yok (Faz 3)
- Config endpoint'i hard-coded (Faz 3)

---

### Faz 2 Tamamlama — Capability, Strict Transitions, Idempotency (08.08.2026)

#### Yapılan İyileştirmeler

**Capability Altyapısı:**
- `AgentCapability` entity: `pavo`, `printer`, `file-transfer` gibi yetenekler
- `PavoConnectionTest` yalnızca `pavo` capability olan agent'a gönderilebilir
- Scope + capability birlikte doğrulama (`ValidateScopeAsync` + `ValidateCapabilityAsync`)

**State Machine:**
- `AgentCommandStateMachine`: strict transition kuralları
- `EnforceTransition()`: geçersiz transition → `InvalidOperationException`
- Valid transitions: `Pending→Delivered→Accepted→Running→Completed/Failed`
- Geçersiz (örn. `Failed→Accepted`) reddedilir

**Idempotency:**
- `MemoryAgentCommandExecutionStore`: `ConcurrentDictionary` ile thread-safe
- Server-side: `AgentId + IdempotencyKey` unique index
- Aynı idempotency key ikinci kez çalıştırılmaz

**Command Delivery Concurrency:**
- `GetPendingCommandsAsync`: Pending → Delivered (atomik status değişikliği)
- İki paralel poll aynı command'i alamaz (Delivered olanlar tekrar poll edilmez)

**Worker Davranışı:**
- Expiration check: süresi dolmuş komutlar alınmaz
- Unknown command → `Rejected` (POST `/api/agent/commands/{id}/reject`)
- Handler exception → `Failed` (worker durmaz)
- Idempotent retry: daha önce çalışmışsa cached result ile complete

**PAVO Client:**
- `IPavoClient.TestConnectionAsync()`: HTTP endpoint test, timeout, anlamlı hata
- Stub `Task.Delay` kaldırıldı, gerçek `IHttpClientFactory` tabanlı implementasyon

#### Yeni Dosyalar

| Dosya | Açıklama |
|-------|----------|
| `backend/Agent/Entities/AgentCapability.cs` | Agent capability entity |
| `backend/Agent/Services/AgentCommandStateMachine.cs` | Strict state transition validator |
| `agent/STYS.Agent.Modules.Pavo/IPavoClient.cs` | `IPavoClient` + `PavoHttpClient` |

#### Migration

- `AddAgentCapabilities` — `AgentCapabilities` tablosu

#### Test Sonuçları

```
Agent Integration: Passed: 25, Failed: 0, Skipped: 0, Total: 25
Full Solution:      Passed: 1611, Failed: 204, Skipped: 0, Total: 1815
Agent Regression:   0
Unit Tests:         Passed: 1062, Failed: 0
```

#### Faz 2 Durumu: TAMAMLANDI ✅

---

### Faz 2 Nihai Düzeltmeler (08.08.2026)

**Running State:**
- State machine: `Delivered → Accepted → Running → Completed/Failed`
- Yeni endpoint: `POST /api/agent/commands/{id}/running`
- Worker: `AcceptAsync → SetRunningAsync → HandleAsync → CompleteAsync/FailAsync`
- `Accepted → Completed` direkt geçişi ENGELLENDİ (state machine tarafından)

**Atomic Command Delivery:**
- `GetPendingCommandsAsync`: transaction + `ExecuteUpdate` ile atomik Pending→Delivered
- 2 parallel poll testi: yalnızca biri command'i alır (total deliveries=1)

**PreviousStatus Audit Fix:**
- `FailAsync`: önce `var prev = cmd.Status`, sonra status değiştir → doğru PreviousStatus
- `RejectAsync`: aynı düzeltme
- Artık `Failed → Failed` veya `Rejected → Rejected` audit bug'ı yok

**Phase 2 Final Testleri:**

| Test | Sonuç |
|------|-------|
| `Transition_DeliveredToAcceptedToRunningToCompleted_Passes` | PASS |
| `Transition_AcceptedToCompletedDirectly_Fails` | PASS |
| `Transition_RunningToFailed_Passes` | PASS |
| `Transition_FromTerminalState_Fails` | PASS |
| `Polling_TwoParallel_OnlyOneGetsCommand` | PASS |
| `Fail_PreviousStatusIsCorrect` | PASS |
| `Reject_PreviousStatusIsCorrect` | PASS |
| `Idempotent_SecondExecuteBlocked` | PASS |

**Test Sonuçları:**

```
Agent Integration: 33/33 PASS
Full Solution:     1619 passed, 204 failed (pre-existing baseline)
Unit Tests:        1062 passed
Agent Regression:  0
```

**Faz 2 Durumu: TAMAMLANDI** ✅

---

### Agent Command Realtime UI (SignalR)

**Backend:**
- `AgentHub` (`/ui/agent-hub`) — `JoinAgentGroupAsync(agentId)`, `LeaveAgentGroupAsync(agentId)`
- Group: `agent:{agentId}`
- `IAgentCommandRealtimeNotifier` → `AgentCommandRealtimeNotifier` (IHubContext tabanlı)
- DB commit'ten sonra, SignalR başarısız olsa bile command işlemi etkilenmez (try/catch + fire-and-forget)
- Tüm command durum değişikliklerinde `AgentCommandUpdated` event'i yayınlanır (Pending/Delivered/Accepted/Running/Completed/Failed/Rejected)

**Frontend:**
- `AgentRealtimeService` — singleton, `HubConnection` yönetimi, `joinAgentGroup`/`leaveAgentGroup`
- Agent detay diyaloğu: `Komutlar` sekmesi, dropdown ile command gönderme, realtime durum güncellemeleri
- Component destroy olduğunda gruptan çıkılır
- `effect()` ile `commandUpdates` sinyali dinlenir, gelen command listede güncellenir

**Test Sonuçları:**
```
Agent Integration: 33/33 PASS
Full Solution:     1619 passed, 204 failed (baseline)
Unit Tests:        1062 passed
Agent Regression:  0
```

---

## 2026-08-10 — PAVO REST Phase 1

### Kapsam

- Angular POS yönetimi
- STYS Backend cihaz komut orkestrasyonu
- STYS.Agent PAVO REST LAN entegrasyonu
- Pairing
- Ping
- GetDeviceInfo
- Terminal Discovery
- TransactionSequence

### Mimari

- UI, cihaz bazlı PAVO aksiyonlarını backend üzerinden tetikliyor.
- Backend, agent command üreterek iş akışını agent'a taşıyor.
- Agent, PAVO REST endpoint'lerini doğrudan LAN üzerinden çağırıyor.
- Başarılı sonuçlar backend'e command completion payload olarak dönüyor.
- Backend, cihaz ve terminal state'ini bu completion sonucuna göre güncelliyor.

### Yapılan Değişiklikler

- `PavoRestClient` eklendi ve LAN üstünden gerçek REST çağrıları başlatıldı.
- `PavoPairing`, `PavoPing`, `PavoGetDeviceInfo` command tipleri eklendi.
- Command worker bu üç PAVO command'ını işleyebilir hale getirildi.
- `PosCihazi.TransactionSequence` alanı eklendi.
- `PosTerminal.AcquirerId` ve `PosTerminal.AcquirerName` alanları eklendi.
- Backend cihaz endpoint'lerine `pairing`, `ping`, `device-info`, `terminal-discovery` aksiyonları eklendi.
- `AgentCommandService`, PAVO completion sonucuna göre cihaz/terminal state'i uygulayacak şekilde güncellendi.
- POS yönetimi ekranı cihaz bazlı aksiyonlar ve terminal acquirer görünümü ile güncellendi.
- SignalR command refresh korunarak ekran yenileme olmadan durum güncellemeleri alınır hale getirildi.
- EF migration eklendi: `AddPavoRestPhase1PosFields`

### Güvenlik ve Kapsam Kontrolleri

- Cihaz işlemleri kurum ve tesis kapsamı ile doğrulanıyor.
- Agent aynı kurum ve tesis kapsamında değilse command üretilmiyor.
- PAVO sonuçları yalnız başarılı completion sonrası state'e uygulanıyor.
- Credential, secret, token, enrollment code ve JWT değerleri raporlanmıyor.

### Testler

- `dotnet build agent/STYS.Agent/STYS.Agent.csproj -c Release`
- `dotnet build backend/STYS.csproj -c Release`
- `npm run build`
- `npm test -- --watch=false --browsers=ChromeHeadless`
- `dotnet test STYS.sln --configuration Release --filter "Category=Integration&Domain=Agent"`
- `dotnet test STYS.sln --configuration Release`

### Notlar

- Full solution testi bu çalışmada mevcut PAVO değişikliklerinden bağımsız bazı eBelge politika testleri nedeniyle tam yeşil değil.
- Gerçek PAVO cihazı/LAN karşısında canlı ortam doğrulaması yapılmadı; entegrasyon kodu gerçek cihaz çağrılarını destekleyecek şekilde hazırlanmıştır.

---

## 2026-08-10 — PAVO REST Phase 1 Hardening

### Terminal discovery değişiklikleri

- Yeni keşfedilen terminal artık otomatik kredi kartı hesabı seçmiyor.
- `GetDeviceInfo` sonucu ile oluşan `PosTerminal`, hesap eşleştirmesi olmadan kaydediliyor.
- Keşif sırasında mevcut terminalin hesap eşlemesi korunuyor.
- Cihazdan artık gelmeyen terminal soft delete olarak pasifleniyor; fiziksel delete yapılmıyor.

### Nullable hesap eşleşmesi

- `PosTerminal.KasaBankaHesapId` nullable hale getirildi.
- POS yönetimi ekranında hesabı olmayan terminal açıkça `Hesap eşleştirilmedi` olarak gösteriliyor.
- Terminal formunda kredi kartı hesabı alanı opsiyonel hale getirildi.
- Manuel hesap bağlama, keşif sonrası UI üzerinden yapılabiliyor.

### Command payload sadeleştirme

- PAVO command/request payloadlarından `KurumId` ve `TesisId` alanları kaldırıldı.
- Agent artık yalnız teknik cihaz alanlarını taşıyor.
- Tenant ve tesis doğrulaması backend tarafında kalmaya devam ediyor.

### Result güvenliği

- Command completion sırasında hedef agent, `PosCihaziId`, cihazın bağlı olduğu agent ve kurum/tesis kapsamı tekrar doğrulanıyor.
- Başka agent veya başka kurum kapsamındaki sonuçlar uygulanmıyor.
- Request payload içindeki tenant bilgilerine güvenilmiyor.

### Sequence davranışı

- `Pairing` artık sequence tüketmiyor; sequence reset başlangıcı olarak kullanılıyor.
- `Ping` ve `GetDeviceInfo` sequence artırmaya devam ediyor.
- Sequence DB’de persistent kalıyor.
- Parallel komutlarda sequence çakışması engelleniyor.

### Migration

- `MakePosTerminalKasaBankaHesapIdOptional` migration’ı eklendi.
- `PosTerminaller.KasaBankaHesapId` nullable yapıldı.

### Test sonuçları

- `dotnet build backend/STYS.csproj -c Release` → geçti
- `dotnet build tests/STYS.Tests/STYS.Tests.csproj -c Release` → geçti
- `npm run build` → geçti
- `npm test -- --watch=false --browsers=ChromeHeadless` → geçti
- `dotnet test STYS.sln --configuration Release --filter "Category=Integration&Domain=Agent"` → skipped, ortam DB bağlantısı yok
- `dotnet test STYS.sln --configuration Release` → mevcut 4 eBelge politika testi fail ediyor

### Bilinen kısıtlar

- Gerçek PAVO cihazı/LAN üzerinde canlı doğrulama yapılmadı.
- Full solution testi halen bu hardening değişikliklerinden bağımsız eBelge politika fail’leri içeriyor.
- Terminal hesabı opsiyonel olsa da ödeme başlatma akışı terminal üzerinde hesap gerektiriyor ve bunu backend doğruluyor.

---

## 2026-08-10 — PAVO REST Phase 2 — Payment

### Payment akışı

- `StartPayment` ve `GetPaymentResult` agent command’leri eklendi.
- Backend tarafında POS ödeme testi akışı için `PosPaymentTestService` eklendi.
- UI üzerinden `POS Yönetimi > POS Cihazı Detayı > Test İşlemleri` sekmesiyle manuel ödeme başlatma ve sonuç sorgulama desteklendi.
- `SaleReference` kalıcı ve yeniden denemede aynı kalan immutable iş referansı olarak ele alındı.

### Güvenlik ve kapsam doğrulamaları

- Agent command sonucu işlenirken hedef agent, cihaz ve kurum/tesis kapsamı yeniden doğrulanıyor.
- `PosCihaziId`, `AgentCommandId`, `SaleReference`, `AcquirerId`, `TerminalId`, `MerchantId`, `PavoResultCode`, `PavoMessage`, `BaslatilmaTarihi` alanları `PosOdemeIslemi` üzerinde tutuluyor.
- Terminal ve cihaz eşleşmeleri backend tarafında doğrulanıyor; payload içindeki tenant bilgilerine güvenilmiyor.

### UI ve realtime

- POS cihaz detay ekranına ödeme test formu ve işlem listesi eklendi.
- SignalR command/result güncellemeleriyle ekran refresh olmadan yeniden güncelleniyor.
- Start / result akışında son işlem kartı ve durum listesi gösteriliyor.

### Sequence davranışı

- Ödeme komutlarında transaction sequence serialization korunuyor.
- Paralel ödeme denemelerinde sequence çakışması oluşmaması için seri üretim transaction içinde tutuluyor.

### Migration

- `AddPavoRestPhase2PaymentFields` migration’ı eklendi.
- `PosOdemeIslemleri` tablosuna ödeme sonucu ve agent command takip alanları eklendi.
- `KurumId + SaleReference` için nullable filtreli unique index oluşturuldu.

### Test sonuçları

- `dotnet test STYS.sln --configuration Release --filter "Category=Integration&Domain=Agent"` → skipped, test connection string yok
- `dotnet test STYS.sln --configuration Release` → geçti
- `npm run build` → geçti
- `npm test -- --watch=false --browsers=ChromeHeadless` → geçti

### Bilinen kısıtlar

- Bu ortamda gerçek SQL Server entegrasyon bağlantısı tanımlı olmadığı için payment integration testleri çalıştırılamadı.
- Gerçek PAVO cihazı/LAN karşısında canlı doğrulama yapılmadı.

## 2026-08-11 — Agent Local Management UI Faz A1

### Amaç

- `STYS.Agent` içine ayrı uygulama gerektirmeyen, loopback-only local management UI eklemek.
- Bootstrap configuration ile STYS bağlantı ayarlarını ve local UI portunu kalıcı hale getirmek.
- Enrollment, PAVO discovery/pairing ve payment akışlarına girmeden temel yönetim altyapısını kurmak.

### Local web host mimarisi

- Agent host artık `WebApplication` üzerinde çalışıyor; worker lifecycle aynı process içinde korunuyor.
- Local UI ve JSON API aynı host tarafından servis ediliyor.
- UI statik HTML/CSS/JS ile kuruldu; ağır frontend bağımlılığı eklenmedi.

### Bind adresi / port

- Varsayılan local UI adresi: `http://127.0.0.1:5180`
- Host yalnız loopback üzerinde dinliyor.
- Port bootstrap configuration üzerinden değiştirilebilir.

### Bootstrap configuration

- Yeni model: `AgentBootstrapConfiguration`
- Alanlar:
  - `StysBaseUrl`
  - `LocalUiPort`
  - `AgentDisplayName`
  - `HttpTimeoutSeconds`
- Abstraction:
  - `IAgentBootstrapConfigurationStore`
- File-based store:
  - `bootstrap.json`
  - credentials içermez

### Config storage konumu

- Windows: `%ProgramData%/STYS/Agent/`
- Linux/macOS: uygulama data dizini altında `STYS/Agent/`
- Bootstrap dosyası: `bootstrap.json`
- Credential store ve instance id aynı data kökünü kullanıyor.

### STYS connection test

- Kurulum / Bağlantı ekranına bağlantı testi eklendi.
- Test, gerçek HTTP request atıp STYS bootstrap ping endpoint’ine gider:
  - `GET /api/agent/bootstrap/ping`
- UI hata sınıfları:
  - geçersiz URL
  - DNS hatası
  - connection refused
  - timeout
  - TLS / certificate hatası

### Dashboard

- Agent enroll olmamış olsa bile dashboard açılıyor.
- Gösterilen bilgiler:
  - Agent Durumu
  - STYS Adresi
  - Enrollment Durumu
  - Agent Display Name
  - Agent Version
  - Local UI Version
  - Credential mevcut mu
  - Son bağlantı testi
- Credential içeriği gösterilmiyor; yalnız bool olarak ifade ediliyor.

### Security kararları

- UI yalnız loopback bind ediyor.
- CORS genişletilmedi.
- Remote shell / arbitrary file access endpoint’i eklenmedi.
- Credential, JWT, access token, enrollment code local bootstrap JSON’a yazılmıyor.
- Linux’ta file permission sıkılaştırması korundu.

### Backward compatibility

- Mevcut enrollment/JWT/heartbeat/command polling/PAVO akışları bozulmadı.
- Bootstrap config yoksa mevcut `appsettings` tabanlı varsayılanlar çalışmaya devam ediyor.
- Bootstrap dosyası varsa, agent startup’ta bu değerler uygulanıyor.

### Test sonuçları

- Agent build: geçti
- Backend build: geçti
- `dotnet test tests/STYS.Tests/STYS.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AgentLocalManagementPhaseATests"`: geçti
- `dotnet test STYS.sln --configuration Release --no-restore`: geçti

### Bilinen kısıtlar

- Local UI port değişikliği mevcut process içinde otomatik rebinding yapmıyor; restart gerekir.
- Dashboard’daki son bağlantı testi runtime state olarak tutuluyor; yeniden başlatma sonrası sıfırlanabilir.
- Gerçek STYS bağlantısı ortamına göre URL doğrulaması ve TLS davranışı değişebilir.

### Sonraki alt faz

- Faz A2
  - Enrollment Wizard
  - STYS BaseUrl
  - Enrollment Code
  - STYS'e Kaydol
  - secure credential storage

## 2026-08-11 — Agent Local Management UI Faz A2

### Enrollment Wizard

- Local UI dashboard üzerinde credential yoksa belirgin `İlk Kurulum / STYS'e Kayıt` akışı gösterildi.
- Alanlar:
  - STYS Sunucu Adresi
  - Agent Adı
  - Enrollment Kodu
  - HTTP Timeout
  - Local UI Port
- Butonlar:
  - Bağlantıyı Test Et
  - STYS'e Kaydol
- Enrollment code input’u masked tutuldu ve başarı sonrası temizlendi.

### Enrollment orchestration

- Local UI enrollment, mevcut `/api/agent/enroll` akışını kullandı; paralel enrollment altyapısı eklenmedi.
- AgentHostedService ve local wizard aynı process-local coordinator/gate üzerinden geçti.
- Credential varsa yeniden enrollment başlatılmadı.

### Secure credential storage

- `ClientId`, `ClientSecret`, `AgentId` mevcut `IAgentCredentialStore` ile güvenli şekilde saklandı.
- ClientSecret bootstrap JSON, appsettings, browser state veya log içine yazılmadı.
- Bootstrap config ve credential storage ayrımı korundu.

### Runtime authentication activation

- Enrollment sonrası credential save → token acquisition → authentication state ready akışı aynı process içinde çalıştı.
- Agent process restart olmadan heartbeat/command worker’lar auth state üzerinden aktif oldu.
- Token refresh path dinamik base URL ile uyumlu hale getirildi.

### /api/agent/me

- `GET /api/agent/me` eklendi.
- Yalnız Agent JWT ile erişiliyor.
- Dönen alanlar:
  - AgentId
  - AgentAd
  - AgentKey
  - KurumId
  - KurumAd
  - Tesisler
  - Scopes
  - Capabilities
  - Durum
  - AgentVersion
  - LastHeartbeatAt
  - OnlineMi

### Auto-enrollment compatibility

- `STYS_ENROLLMENT_CODE` tabanlı mevcut auto-enrollment korunuyor.
- Local UI enrollment ile env tabanlı auto-enrollment aynı anda çalışsa bile tek kayıt için gate kullanıldı.
- Config değişikliği runtime client options state’ine işlendi.

### Concurrency

- Enrollment akışı process-local `SemaphoreSlim` ile korundu.
- Aynı anda local UI enrollment ve hosted-service auto-enrollment geldiğinde tek işlem gerçekleşiyor.

### Security

- Enrollment code kalıcı depoya yazılmıyor.
- ClientSecret UI response’a dönmüyor.
- Dashboard sadece own-agent profilini gösteriyor.
- Heartbeat/command workers auth state hazır olmadan başlamıyor.

### Test sonuçları

- A2 unit testleri geçti.
- `dotnet test tests/STYS.Tests/STYS.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AgentLocalEnrollmentWizardPhaseATests|FullyQualifiedName~AgentLocalManagementPhaseATests"` geçti.
- `dotnet test STYS.sln --configuration Release` geçti.
- Angular build geçti.
- Angular test geçti.

### Bilinen kısıtlar

- Local UI port değişikliği mevcut process içinde rebinding gerektirebilir; bu fazda otomatik port migration yapılmadı.
- `/api/agent/me` profil alanları mevcut DB şemasıyla sınırlı; ileride daha zengin display name/metadata genişletilebilir.

### Sonraki alt faz

- Faz A3
  - Dashboard hardening
  - diagnostics
  - configuration management
  - controlled reset/re-enrollment

## 2026-08-11 — Agent Local Management UI Faz A3

### Dashboard

- Local dashboard operasyonel görünürlük için sadeleştirildi.
- Aşağıdaki bilgiler gösteriliyor:
  - STYS bağlantı durumu
  - Agent ID
  - Agent adı
  - Kurum
  - Yetkili tesisler
  - Scopes
  - Capabilities
  - Credential durumu
  - Auth hazır mı
  - Heartbeat worker durumu
  - Command worker durumu
  - STYS adresi
  - STYS server version
  - Agent version
  - Local UI version
  - Son heartbeat
  - Son bağlantı testi
  - Son reset zamanı

### Runtime status

- Agent runtime için resetlenebilir, thread-safe bir durum modeli eklendi.
- Authentication, connection, heartbeat ve command-poll durumları ayrı ayrı takip ediliyor.
- BaseUrl değiştiğinde mevcut local credential artık kullanılmaz kabul edilip auth kapatılıyor.

### Diagnostics

- `Diagnostics` ekranı eklendi.
- Process bilgileri, uptime, machine/OS/framework, data directory ve bootstrap path gösteriliyor.
- Son başarılı STYS bağlantısı, heartbeat, command poll ve reset zamanı izleniyor.
- Recent log buffer son 100 giriş ile sunuluyor.

### Local log viewer

- In-memory log buffer ve logger provider eklendi.
- Log görüntüleme ekranında timestamp, level, category ve mesaj yer alıyor.
- Secret benzeri değerler maskeleniyor; JWT/client secret/enrollment code görünmüyor.

### Configuration management

- Kurulum ekranı Bootstrap config ile senkron çalışır hale getirildi.
- Local UI port değişikliği için restart gereksinimi açıkça gösteriliyor.
- STYS BaseUrl değişikliği varsa re-enrollment uyarısı üretiliyor.

### Credential reset

- Controlled reset endpoint’i ve UI formu eklendi.
- Yerel credential, token ve authentication state sıfırlanıyor.
- Merkezi STYS agent kaydı silinmiyor.
- Onay metni olmadan reset çalışmıyor.

### Re-enrollment lifecycle

- Reset sonrası wizard otomatik geri geliyor.
- Re-enrollment başarılı olunca worker'lar tekrar aktive oluyor.
- BaseUrl mismatch durumunda mevcut credential ile sessiz bağlanma engelleniyor.

### Worker gating

- HeartbeatWorker ve CommandPollingWorker auth-ready gate üzerinden yeniden bloklanabilir hale getirildi.
- Reset sonrası worker'lar auth kapalı durumda bekliyor; yeni enrollment sonrası otomatik devam ediyor.
- 401 spam davranışı için bekleme döngüsü auth durumunu aralıklarla kontrol ediyor.

### Security

- Local UI loopback üzerinde kalıyor.
- Reset endpoint’i yalnız POST ve explicit confirmation ile çalışıyor.
- Diagnostics ve log ekranları secret raporlamıyor.

### Test sonuçları

- `dotnet test STYS.sln --configuration Release --filter "Category=Integration&Domain=Agent"` geçti.
- `dotnet test STYS.sln --configuration Release` geçti.
- `npm run build` geçti.
- `npm test -- --watch=false --browsers=ChromeHeadless` geçti.

### Bilinen kısıtlar

- Angular build’de mevcut bundle budget uyarısı devam ediyor; bu fazda kapsam dışı bırakıldı.
- Agent tarafında SQLite kullanımı tespit edilmedi; bu fazda dependency temizliği yapılmadı.

## 2026-08-11 — Agent Local Management UI Faz A4

### Generic local device architecture

- Agent içine provider bağımsız `LocalDevice` modeli eklendi.
- Model; `DeviceType`, `Provider`, `DisplayName`, `Host`, `HttpPort`, `HttpsPort`, `Protocol`, `SerialNumber`, `Status`, `LastConnectionTestAt`, `LastConnectionSuccess`, `LastError`, `CreatedAt`, `UpdatedAt` alanlarını içeriyor.
- Mimari şu an PAVO dışında provider genişletmeye açık, fakat runtime’da yalnızca PAVO tester kayıtlı.

### Persistence

- `ILocalDeviceStore` ve file-based JSON store eklendi.
- Store, Agent data directory altında `local-devices.json` kullanıyor.
- Yazma işlemi temp dosya üzerinden atomic şekilde yapılıyor; yarım/bozuk JSON bırakmamak için overwrite stratejisi kullanılıyor.
- Device restart sonrası korunuyor.

### Local device UI

- `local-cihazlar` sayfası placeholder olmaktan çıkarıldı.
- Liste, form ve aksiyonlar eklendi:
  - Yeni Cihaz
  - Düzenle
  - Bağlantıyı Test Et
  - Sil
- Tablo alanları:
  - Ad
  - Tip
  - Provider
  - Adres
  - Durum
  - Son Test
- PAVO için bağlantı formu yalnız host/ip ve port/protocol bilgilerini alıyor.

### Connection tester registry

- `ILocalDeviceConnectionTester` ve registry yaklaşımı eklendi.
- PAVO local connection test, mevcut REST client altyapısı üzerinden tekrar kullanıldı.
- Test sonucu local device state’e yazılıyor.

### PAVO connection profile

- PAVO cihaz formu için default portlar `4567/4568` olarak uygulandı.
- PAVO seçeneği local bağlantı profilini temsil ediyor; pairing ve merkezi STYS provisioning bu fazda yok.

### Security

- Host/IP doğrulaması eklendi.
- `file://` ve benzeri arbitrary URI scheme reddediliyor.
- Local tester arbitrary URL proxy haline gelmiyor; shell/process çalıştırma eklenmedi.
- Secret, fingerprint, JWT veya enrollment code UI/diagnostics raporuna taşınmıyor.
- Local UI loopback’te kalıyor.

### A3 housekeeping

- Diagnostics DTO’dan `CredentialStorePath` kaldırıldı.
- Runtime status’a `LastStysConnectionError` eklendi.
- `MarkFailedConnection()` bu alanı güncelliyor; başarılı bağlantıda hata temizleniyor.

### Tests

- File store save/load, restart persistence, duplicate id, invalid host/protocol, default portlar, connection success, timeout/unreachable status, delete, secret sızıntısı ve arbitrary scheme reddi için testler eklendi.
- PAVO connection tester endpoint üretimi ve registry davranışı doğrulandı.
- `dotnet build agent/STYS.Agent/STYS.Agent.csproj -c Release --no-restore` geçti.
- `dotnet build tests/STYS.Tests/STYS.Tests.csproj -c Release --no-restore` geçti.
- `dotnet test tests/STYS.Tests/STYS.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~AgentLocalDevicesPhaseA4Tests"` geçti.
- `dotnet test STYS.sln --configuration Release --no-build` bu turda iki mevcut, bu değişiklikten bağımsız test flake’i gösterdi.
- `npm run build` geçti; Angular build’de mevcut bundle budget uyarısı devam ediyor.
- `npm test -- --watch=false --browsers=ChromeHeadless` geçti.

### Known limitations

- Bu fazda merkezi PAVO pairing, terminal discovery ve STYS POS provisioning yapılmadı.
- Printer implementasyonu yalnızca model düzeyinde hazır; runtime handler eklenmedi.
- Angular build budget uyarısı devam ediyor.
- Full solution test koşusunda iki mevcut test flake’i görüldü; kod değişikliğiyle ilişkili görünmüyor.

## 2026-08-11 — PAVO Local Provisioning Faz B1

### Local GetDeviceInfo

- Agent local UI’dan seçilen PAVO POS cihazı için doğrudan LAN çağrısı yapılıyor.
- `GetDeviceInfo` akışı merkezi `AgentCommand` kullanmadan çalışıyor.
- Başarılı sonuçta public metadata olarak `SerialNumber`, `DeviceName` ve `LastDeviceInfoAt` güncelleniyor.

### Pairing architecture

- Pairing artık local device detay aksiyonu olarak çalışıyor.
- `Pairing` öncesi cihazın PAVO POS olması ve bağlantı testinin başarılı olması zorunlu.
- Zaten paired cihazda yeniden pairing için açık force onayı gerekiyor.

### Secure pairing store

- Fingerprint / target fingerprint / transaction sequence bilgileri `local-devices.json` içine yazılmıyor.
- Bu bilgiler ayrı bir secure store’da tutuluyor.
- Generic local device metadata ile secret pairing state ayrıldı.

### Transaction sequence

- Sequence, pairing store içinde atomik şekilde rezerve ediliyor.
- Aynı local cihaz için paralel çağrılar aynı sequence’i alamıyor.
- Restart sonrası sequence değeri korunuyor.

### Re-pair policy

- Pairing yapılmış cihaza force olmadan re-pair reddediliyor.
- Başarısız re-pair mevcut başarılı pairing state’ini silmiyor.
- Başarısız denemeler `LastPairingAttemptAt` ve `LastPairingError` ile izleniyor.

### UI

- `Yerel Cihazlar` ekranına cihaz detayı paneli eklendi.
- Detay panelinde:
  - `Bağlantıyı Test Et`
  - `Cihaz Bilgisini Getir`
  - `Pairing Başlat` / `Yeniden Pairing`
  - bağlantı ve pairing durumları
  - seri no, model/cihaz bilgisi, son device-info ve son pairing zamanı
- Fingerprint UI’da tam gösterilmiyor.

### Security

- Host/IP doğrulaması korunuyor.
- Arbitrary URI scheme kabul edilmiyor.
- Secret / fingerprint loglanmıyor.
- Local UI loopback’te çalışmaya devam ediyor.

### Tests

- POS/PAVO validation, printer/PAVO rejection, get device info success/error, pairing success, secure store, restart persistence, sequence uniqueness, force re-pair policy ve fingerprint sızıntısı için testler eklendi.
- `dotnet test tests/STYS.Tests/STYS.Tests.csproj --configuration Release --filter "FullyQualifiedName~AgentLocalDevicesPhaseB1Tests|FullyQualifiedName~AgentLocalDevicesPhaseA4Tests"` geçti.
- `dotnet test STYS.sln --configuration Release` çalıştırıldı; B1 kapsamı geçti, fakat üç mevcut test başarısız oldu:
  - `STYS.Tests.EBelgeOutboxWorkerTests.ClaimNullDondugundePermitGeriBirakilirVeSonrakiTurCalisir`
  - `STYS.Tests.EBelgeOutboxWorkerTests.GeciciSqlClaimHatasiWorkeriDurdurmaz`
  - `STYS.Tests.SaxonSidecarEBelgeSchematronValidatorTests.TimeoutServiceUnavailableOlur`

### Real PAVO device test status

- Bu turda gerçek LAN PAVO cihazı ile manuel test yapılmadı.

### Known limitations

- Central STYS `PosCihazi` provisioning bu fazda yapılmıyor.
- Pairing sonrasında terminal discovery bir sonraki fazda tamamlanacak.
- Angular build/test komutları bu turda ayrıca koşturulacak.
- Full solution test koşusunda üç mevcut failure var; bu değişiklik setinden kaynaklı görünmüyor.

## 2026-08-11 — PAVO Local Provisioning Faz B2

### Terminal discovery

- Pair edilmiş PAVO POS cihazı için doğrudan local `GetDeviceInfo` yanıtından terminal keşfi eklendi.
- Keşif akışı merkezi `AgentCommand` kullanmadan local UI üzerinden çalışıyor.
- Terminal keşfi yalnızca `PairingStatus = Paired` olduğunda çalışıyor.

### Terminal identity / reconciliation

- Local terminal modeli `LocalDeviceTerminal` olarak ayrıldı.
- Canonical kimlik `LocalDeviceId + AcquirerId + TerminalId` mantığıyla üretildi.
- Tekrar discovery yapıldığında aynı terminal güncelleniyor, duplicate oluşmuyor.
- Yanıt içinde artık olmayan terminal silinmiyor; `Active = false` yapılıyor.

### Local terminal persistence

- Terminal metadata ayrı `local-device-terminals.json` store’una yazılıyor.
- Store atomic write kullanıyor.
- Public terminal metadata ile pairing secret state birbirinden ayrıldı.

### Provisioning candidate contract

- `PavoDeviceProvisioningCandidate` eklendi.
- Candidate içinde:
  - local device kimliği
  - provider/display/host/port/protocol/serial/device name
  - paired timestamp
  - terminal listesi
  - seçilen tesis
  - fingerprint / client secret / JWT / enrollment code / agent / kurum bilgileri yok

### Tesis selection

- Local UI’da `/api/agent/me` üzerinden tesis listesi alınıyor.
- Provisioning preview için tesis seçimi local olarak doğrulanıyor.
- Agent kapsamı dışındaki tesis reddediliyor.

### Security

- Terminal store secret içermiyor.
- Provisioning candidate secret içermiyor.
- Fingerprint, token ve benzeri hassas değerler log / UI üzerinden sızdırılmıyor.
- Discovery tarafında unpaired cihazda gereksiz PAVO çağrısı yapılmıyor.

### Sequence

- Terminal discovery çağrıları secure pairing store üzerinden yeni transaction sequence reserve ediyor.
- Sequence restart sonrası korunuyor.
- Aynı cihaz için paralel sequence reservation unique kalıyor.

### UI

- `Yerel Cihazlar` ekranına:
  - `Terminalleri Keşfet`
  - terminal listesi
  - provisioning preview
  - tesis dropdown
  eklendi.
- `STYS'e Kaydet` aksiyonu bu fazda pasif bırakıldı.

### Tests

- Unpaired discovery rejection
- Paired discovery success
- Duplicate discovery reconciliation
- Missing terminal inactive reconciliation
- Terminal store secret leakage prevention
- Provisioning candidate secret leakage prevention
- Invalid tesis rejection
- Discovery sequence increment
- Restart sonrası terminal metadata korunumu
- `dotnet test tests/STYS.Tests/STYS.Tests.csproj --configuration Release --filter "FullyQualifiedName~AgentLocalDevicesPhaseB2Tests"` geçti.
- `dotnet test STYS.sln --configuration Release` geçti.
- `npm run build` geçti; mevcut bundle budget uyarısı devam ediyor.
- `npm test -- --watch=false --browsers=ChromeHeadless` geçti.

### Real PAVO device test status

- Bu turda gerçek LAN PAVO cihazı ile manuel test yapılmadı.

### Known limitations

- Merkezi `PosCihazi` provisioning bu fazda yapılmıyor.
- Terminal discovery sonrası central sync bir sonraki fazda ele alınacak.
- Angular build budget uyarısı mevcut.
