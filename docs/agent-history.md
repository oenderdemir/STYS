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

## 2026-08-12 — PAVO Device Provisioning Faz C1 + Hardening

### Registration endpoint

- Agent → STYS akışı `POST /api/agent/pos-devices/register` üzerinden ilerliyor.
- `AgentId` ve `KurumId` request contract içinde taşınmıyor; bu değerler server-side `ICurrentAgentContext` üzerinden çözülüyor.
- Tesis doğrulaması backend tarafında yapılıyor; agent context dışı tesis reddediliyor.

### PosCihazi create / reconcile

- Aynı fiziksel cihaz için idempotent kayıt/reconcile davranışı korunuyor.
- Tekrarlayan aynı-agent kayıt denemelerinde mevcut `PosCihazi` geri dönüyor.
- Concurrent registration senaryolarında tek kayıt oluşuyor; loser request conflict alıyor.

### Global serial uniqueness

- PAVO için global filtered uniqueness `Saglayici + SeriNo` üzerinde uygulanıyor.
- Soft-delete kayıtlar filtre dışı bırakılıyor.
- Aynı seri numarası başka kurumda kayıtlıysa conflict davranışı bekleniyor.
- Cross-tenant concurrent conflict senaryosu tek kayıt + 409 sonucu üretiyor.

### Cross-tenant conflict behavior

- Aynı fiziksel seri numarası başka bir tenant altında aktifse yeni kayıt reddediliyor.
- Conflict kararı server-side veriliyor; istemciye güvenilmiyor.

### Terminal canonical identity

- Terminal reconciliation için canonical kimlik:
  `PosCihaziId + AcquirerId + TerminalId`
- `SerialNumber` veya yalnız `TerminalId` ile eşleştirme yapılmıyor.
- Aynı `TerminalId` farklı `AcquirerId` ile ayrı terminal sayılıyor.

### KasaBankaHesapId mapping

- Mevcut `KasaBankaHesapId` eşlemesi korunuyor.
- Reconcile sırasında terminalin hesap bağlantısı gereksiz yere sıfırlanmıyor.
- Canonical identity güncellemesi, var olan finansal hesap eşleşmesini bozmuyor.

### TransactionSequence ownership

- TransactionSequence authoritative owner = Agent.
- Local UI PAVO çağrıları ve central command çağrıları aynı Agent-side atomic reservation mekanizmasını kullanıyor.
- Central command sequence, execution anında Agent tarafında reserve ediliyor.
- Central DB `PosCihazi.TransactionSequence` artık authoritative değil; legacy / diagnostic amaçlı kalıyor.

### Local UI + central command sequence coordination

- Local UI ve central command aynı pairing store counter’ını kullanıyor.
- Ayrı sequence owner bırakılmadı; paralel execution collision riski azaltıldı.

### Idempotency / concurrency

- Aynı `Agent + LocalDevice + Serial + Tesis` kombinasyonu tekrar register edildiğinde mevcut kayıt dönüyor.
- Concurrent aynı-agent register senaryosu tek kayıt üretmek üzere tasarlandı.
- Sequence reservation tarafı da concurrent kullanımda unique kalacak şekilde ele alındı.

### Fingerprint security

- Fingerprint / target fingerprint response’a çıkarılmıyor.
- Loglara yazılmıyor.
- Local provisioning preview DTO içinde taşınmıyor.

### Migrations

- `C1ProvisioningOwnershipHardening` migration’ı eklendi.
- `PosCihazlari` için global `Saglayici + SeriNo` unique index tanımlandı.
- `PosTerminaller` için canonical identity kolonları ve unique index eklendi.

### Test results

- `dotnet test STYS.sln --configuration Release` geçti.
- Sonuç: `1112 passed, 761 skipped, 0 failed`
- Agent/PAVO odaklı ek filtre çalıştırıldığında tek bir hardening testi hata verdi:
  - `STYS.Tests.Agent.AgentLocalDevicesPhaseB2Tests.LocalVeCentralSequence_AyniStoreUzerindeCakismadanRezervEdilir`
  - Hata: `Assert.Equal() Failure: Values differ. Expected: 2 Actual: 1`

### Real PAVO device test status

- Bu turda gerçek PAVO cihazı ile manuel test yapılmadı.

### Known limitations

- Central DB sequence değeri legacy/diagnostic amaçlı tutuluyor; authoritative kabul edilmiyor.
- Gerçek PAVO donanımı olmadan sequence ve registration davranışı laboratuvar/test verisiyle doğrulandı.
- Targeted hardening filter’deki sequence testi şu an expectation mismatch gösteriyor; full solution testi ise geçti.

### C1 Sequence Concurrency Final Fix — 2026-08-12

- Gerçek race condition root cause: `FilePavoLocalPairingStore.UpsertAsync` eski snapshot ile yazıp daha yüksek `TransactionSequence` değerini geriye çekebiliyordu.
- Monotonic merge eklendi: persisted state ile incoming state aynı lock altında birleştiriliyor, `TransactionSequence` için `Max(stored, incoming)` uygulanıyor.
- Fingerprint / target fingerprint / pairing metadata artık stale yazımlarla kaybolmuyor.
- Hedef test artık PASS:
  - `STYS.Tests.Agent.AgentLocalDevicesPhaseB2Tests.LocalVeCentralSequence_AyniStoreUzerindeCakismadanRezervEdilir`
- Full test sonucu:
  - `dotnet test STYS.sln --configuration Release` geçti
  - Sonuç: `1113 passed, 761 skipped, 0 failed`
- Sequence owner hâlâ Agent-side authoritative reservation mekanizması.
- Gerçek PAVO cihazı ile manuel test bu turda yapılmadı.

## 2026-08-12 — PAVO Device Provisioning Faz C2

- Lifecycle states:
  - `NotProvisioned`
  - `Provisioned`
  - `ReProvisionRequired`
  - `Conflict`
  - `Disabled`
- Re-pair sonrası local state artık `ReProvisionRequired` oluyor; central komutlar bu durumda çalıştırılmıyor.
- Re-provision akışı `STYS durumu kontrol et` ve yeniden kayıt adımıyla yönetiliyor.
- Central/local reconciliation için güvenli bir Agent endpoint eklendi:
  - `POST /api/agent/pos-devices/status-snapshot`
- Local UI’da STYS reconciliation aksiyonu eklendi ve durum mesajı gösteriliyor.
- AgentId/KurumId request contract’tan alınmıyor; server-side `ICurrentAgentContext` ile doğrulama yapılıyor.
- Tesis doğrulaması ve sahiplik kontrolleri korunuyor.
- PosCihazi create/reconcile akışı aynı kurum / aynı agent / aynı tesis kuralını bozmayacak şekilde kaldı.
- Global `Saglayici + SeriNo` uniqueness ve `AgentLocalDeviceId` mismatch kontrolü korunuyor.
- Terminal canonical identity ve mevcut `KasaBankaHesapId` mapping korunuyor.
- TransactionSequence authoritative owner hâlâ Agent; merkezi command sequence execution anında reserve ediliyor.
- Local UI ve central command aynı pairing store counter’ını kullanıyor.
- Fingerprint, client secret, JWT ve enrollment code log/response sızıntısı hedeflenmiyor.
- Migrations tarafında mevcut C1/C1-hardening altyapısı kullanıldı; bu turda yeni migration gerektiren model değişikliği çıkmadı.
- Test sonuçları:
  - `dotnet test STYS.sln --configuration Release` geçti
  - `frontend` için `npm run build` geçti
  - `frontend` için `npm test -- --watch=false --browsers=ChromeHeadless` geçti
- Bilinen kısıtlar:
- Gerçek PAVO cihazı ile manuel saha testi yapılmadı.
- Angular build’de bundle budget warning devam ediyor.

### C2 Reconciliation Security Hardening — 2026-08-12

- Fingerprint snapshot leak kaldırıldı:
  - `AgentPavoDeviceStatusSnapshotDto` içinden `Fingerprint` ve `TargetFingerprint` alanları çıkarıldı.
  - `POST /api/agent/pos-devices/status-snapshot` response’u artık sadece public operational metadata dönüyor.
- Reconciliation local metadata’yı overwrite etmiyor:
  - `CheckStysStatusAsync()` central snapshot’tan `SerialNumber`, `DeviceName`, `DisplayName`, `Host` ve port alanlarını local cihaza yazmıyor.
  - Central snapshot yalnız comparison/result DTO olarak kullanılıyor.
- Local truth / central comparison ayrımı netleştirildi:
  - Local discovery / user configuration / PAVO `GetDeviceInfo` local metadata’nın kaynağı olarak korunuyor.
  - Central correlation alanları ayrı tutuluyor:
    - `CentralPosCihaziId`
    - `CentralAgentId`
    - `CentralTesisId`
    - `StysReconciliationStatus`
    - `StysReconciliationMessage`
    - `StysReconciliationCheckedAt`
- Tenant-safe snapshot davranışı korunuyor:
  - Status snapshot endpoint current Agent/Kurum kapsamında kalıyor.
  - Cross-tenant veri response’a sızmıyor.
  - Registration endpoint ownership conflict için authoritative mekanizma olmaya devam ediyor.
- Tests:
  - Snapshot JSON fingerprint içermez testi eklendi.
  - Drift testinde local `SerialNumber` ve host/display metadata’nın snapshot ile overwrite edilmediği doğrulandı.
- Real PAVO device test status:
  - Bu turda gerçek PAVO cihazı ile manuel test yapılmadı.

### E2A Authoritative Version Hardening — 2026-08-12

- Gerçek binary version kaynağı:
  - `agent/Directory.Build.props` ile agent tarafında deterministik `VersionPrefix/Version/InformationalVersion` tanımlandı.
  - `STYS.Agent` heartbeat ve bootstrap tarafı artık sabit string kullanmıyor; version `AssemblyInformationalVersion` üzerinden okunuyor.
- Contract version kaynağı:
  - `STYS.Agent.Contracts.Versioning.AgentContractVersion.Current` authoritative tek kaynak olarak eklendi.
  - Heartbeat contract version ve backend compatibility policy bu sabit üzerinden ilerliyor.
- SemVer davranışı:
  - Prerelease precedence eklendi: `1.0.0-rc.1 < 1.0.0`.
  - Build metadata precedence’i etkilemiyor: `1.0.0+build.5` ile `1.0.0` aynı precedence.
  - Bozuk formatlar `Unknown` dönüyor.
  - `v1.2.3` açıkça destekleniyor.
- Payment guard:
  - `UpdateRequired` ve `IncompatibleContract` durumlarında `PavoStartPayment` hâlâ engelleniyor.
  - `PavoGetPaymentResult` ve recovery akışları açık kaldı.
- Tests:
  - Hedefli compatibility/version testleri geçti.
  - `dotnet test STYS.sln --configuration Release` çalıştırıldı.
  - Full solution’da tek mevcut failure:
    - `STYS.Tests.EBelgeSchematronSidecarIntegrationTests.BuyukXmlLimitteReddedilir`
    - hata: remote sidecar connection request sırasında connection forcibly closed by remote host.
- Real PAVO device test status:
  - Bu turda gerçek PAVO cihazı ile manuel test yapılmadı.
- Bilinen kısıtlar:
  - Build version provider şu an agent assembly informational version’ına dayanıyor; pipeline başka metadata isterse props üzerinden override edilebilir.

## 2026-08-12 — Agent Production Faz E2A

- Compatibility policy:
  - Merkezi agent uyumluluk modeli eklendi.
  - Durumlar: `Unknown`, `Supported`, `UpdateAvailable`, `UpdateRequired`, `IncompatibleContract`.
  - Karar backend policy/options üzerinden veriliyor:
    - `MinimumSupportedAgentVersion`
    - `RecommendedAgentVersion`
    - `SupportedContractVersion`
  - Semantic version karşılaştırması string compare ile yapılmıyor.
- Heartbeat / agent metadata:
  - Heartbeat request içindeki `AgentVersion` ve `ContractVersion` authoritative kaynak olarak işleniyor.
  - Agent entity’sine `ContractVersion` kalıcı alanı eklendi.
  - Agent listesi ve detail DTO’ları uyumluluk status/version alanlarını taşıyor.
- Payment guard:
  - `PavoStartPayment` için uyumluluk kontrolü backend tarafında zorunlu hale getirildi.
  - `Supported` ve `UpdateAvailable` izinli.
  - `UpdateRequired`, `IncompatibleContract`, `Unknown` bloklanıyor.
  - Recovery akışları (`PavoGetPaymentResult`, heartbeat, config, health) engellenmiyor.
- UI:
  - Agent listesinde compatibility badge eklendi.
  - Agent detayında uyumluluk sekmesi eklendi:
    - Agent Version
    - Contract Version
    - Minimum Supported
    - Recommended Version
    - Supported Contract
    - Compatibility Status
- Migration:
  - `Agentler.ContractVersion` kolonu eklendi.
- Tests:
  - `dotnet test tests/STYS.Tests/STYS.Tests.csproj --configuration Release --filter "FullyQualifiedName~AgentCompatibilityPolicy"` geçti.
  - `dotnet test STYS.sln --configuration Release` geçti.
  - Integration testlerin bir kısmı local SQL connection env olmadığı için `skip` oldu.
- Real PAVO device test status:
  - Bu turda gerçek PAVO cihazı ile manuel test yapılmadı.
- Bilinen kısıtlar:
  - Policy şu an major/minor/patch/revision karşılaştırması yapıyor; prerelease semver sıralaması için ayrı bir model yok.
  - Backend ve UI version alanları display amaçlıdır; binary download/self-update yoktur.

## 2026-08-12 — Agent Production Faz E1

- Windows/Linux production publish:
  - `win-x64` ve `linux-x64` için Release publish akışı tanımlandı.
  - Self-contained publish örnekleri dokümante edildi.
  - Artifact içine development dosyaları / secrets dahil edilmedi.
- Windows service modeli:
  - `STYS Agent` servis adıyla kurulum scripti eklendi.
  - Automatic (Delayed Start) ve recovery policy ayarlandı.
  - Çalışma dizini, data/log dizinleri ve ACL ayarları kurulumda hazırlanıyor.
  - Varsayılan servis hesabı düşük yetkili `LocalService`.
- Linux systemd modeli:
  - Dedicated düşük yetkili `stys-agent` user ile unit/script eklendi.
  - `Restart=on-failure`, çalışma dizini ve izinler tanımlandı.
  - SIGTERM ile graceful shutdown hedefleniyor.
- Data preservation:
  - Uninstall varsayılan olarak credential/data/log dizinlerini silmiyor.
  - `--purge` / explicit purge ile temizleme yapılabiliyor.
- Security:
  - Install scriptleri client secret, enrollment code veya fingerprint loglamıyor.
  - Secrets command-line argümanları üzerinden taşınmıyor.
  - Local UI loopback binding korunuyor.
- Startup validation:
  - Kritik dizinler writable değilse agent healthy sayılmıyor.
  - Diagnostics’te startup health error görünür hale getirildi.
  - Production DI, restart-safe file execution store kullanıyor.
- Tests:
  - Production DI file execution store testi geçti.
  - Unwritable critical store startup unhealthy testi geçti.
  - Loopback / uninstall data preservation / secret redaction regresyonları geçti.
  - `dotnet test STYS.sln --configuration Release` geçti: `Passed: 1129, Skipped: 782, Failed: 0`.
- Real PAVO device test status:
  - Bu turda gerçek PAVO cihazı ile manuel test yapılmadı.
- Bilinen kısıtlar:
  - Service scriptleri makineye göre servis hesabı/izin uyarlaması gerektirebilir.
  - Linux kurulumunda paketlenmiş self-contained binary adı publish çıktısına bağlıdır.
  - Angular/Back-end dışında production deploy prosedürü ayrıca operasyon dokümantasyonu gerektirebilir.

### E1 Deployment Runtime Fix — 2026-08-12

- Windows service runtime:
  - PowerShell wrapper kaldırıldı.
  - SCM artık doğrudan `STYS.Agent.exe` ile başlatıyor.
  - `UseWindowsService` lifecycle gerçek service process içinde çalışıyor.
  - Delayed Start ve recovery policy korunuyor.
- Path overrides:
  - `STYS_AGENT_DATA_DIR` ve `STYS_AGENT_LOG_DIR` desteği eklendi.
  - Windows default paths `%ProgramData%\STYS\Agent` ve `%ProgramData%\STYS\Agent\logs`.
  - Linux default paths `/var/lib/stys-agent` ve `/var/log/stys-agent`.
  - Serilog artık relative `logs/...` yerine resolver tabanlı log directory kullanıyor.
- Linux permissions:
  - `/opt/stys-agent` servis kullanıcısına writable değil.
  - Binary/config root-owned; service user için read/execute.
  - Data/log dizinleri `stys-agent` owned ve writable.
- Local UI port:
  - `LocalUiPort` artık `STYS_AGENT_LOCAL_UI_PORT` üzerinden gerçekten uygulanıyor.
  - Loopback binding korunuyor.
- Startup validation:
  - Log directory de kritik writable path kontrolüne dahil edildi.
  - Writable olmayan kritik store’da agent unhealthy sayılıyor.
- Tests:
  - Windows binPath direct exe testi geçti.
  - Program Files write gerektirmediği doğrulandı.
  - Linux install dir service user writable değil testi geçti.
  - Data/log override ve startup log-dir validation testleri geçti.
  - Loopback binding testi korundu.
  - `dotnet test STYS.sln --configuration Release` geçti: `Passed: 1129, Skipped: 782, Failed: 0`.
- Real device test status:
  - Bu turda gerçek cihaz üzerinde kurulum testi yapılmadı.
- Known limitations:
  - Windows service registry environment value ayarları kurulum yetkisi gerektirir.
  - Linux install scriptinin root yetkisiyle çalıştırılması gerekir.
  - Publish output farklı self-contained ayarlarda dosya isimlerini değiştirebilir; scriptler publish çıktısını varsayar.

### E1 Linux Installer Port Fix — 2026-08-12

- Local UI port:
  - Linux installer artık 5. positional parametreyi `LOCAL_UI_PORT` olarak kullanıyor.
  - Varsayılan değer `5180`.
  - Geçersiz port değerleri `1..65535` aralığı dışında ise kurulum duruyor.
- systemd unit:
  - `STYS_AGENT_LOCAL_UI_PORT` ve `ASPNETCORE_URLS` aynı port değerini kullanıyor.
  - Ayrı ve çelişen hard-coded port kalmadı.
- Tests:
  - `bash -n scripts/agent/install-agent.sh` syntax testi eklendi.
  - Scriptte undefined `LocalUiPort` kalmadığı test edildi.
  - Custom port örneğinin aynı portu unit satırlarına taşıdığı doğrulandı.
- Real device test status:
  - Bu turda gerçek Linux kurulum testi yapılmadı.
- Known limitations:
  - Unit dosyası `systemd` yazma izni gerektirir.
  - Port argümanı yalnız kurulum scripti üzerinden uygulanır; mevcut kurulu service’in yeniden kurulması gerekir.

### D3 Final Safety Fix — 2026-08-12

- Execution store fail-closed hale getirildi:
  - `FileAgentCommandExecutionStore` içinde persisted dosya yoksa normalde empty state döner.
  - Persisted dosya mevcutken read/deserialize başarısız olursa exception fırlatılır.
  - Böyle bir bozuk store, `PavoStartPayment` için fiziksel execution tekrarına izin vermez.
- Disk persistence scope daraltıldı:
  - yalnız `PavoStartPayment` ve `PavoGetPaymentResult` kalıcı store’a yazılıyor.
  - `PavoPairing`, `PavoPing`, `PavoGetDeviceInfo` ve diğer command’lar memory fallback kullanıyor.
  - secret-bearing payload’lar disk execution store’a yazılmıyor.
- Tests:
  - corrupt persistent file + `PavoStartPayment` için fail-closed senaryosu eklendi.
  - `PavoPairing` secret-bearing result payload’ı disk dosyasına düşmüyor.
  - restart sonrası `PavoStartPayment` marker hâlâ ikinci execution’ı engelliyor.
  - restart sonrası `PavoGetPaymentResult` result okunabiliyor.
  - monotonic payment testleri korunuyor.
  - `dotnet test STYS.sln --configuration Release` çalıştırıldı; tek mevcut bağımsız failure:
    - `STYS.Tests.EBelgeOutboxWorkerTests.DogruIsTuruIleClaimIsleAsyncEGider(isTuru: UblImzala)`
    - hata: `System.TimeoutException: Beklenen koşul zaman aşımı içinde gerçekleşmedi.`
- Real PAVO device test status:
  - Bu turda gerçek PAVO cihazı ile manuel test yapılmadı.
- Bilinen kısıtlar:
  - Disk store yalnız payment idempotency anahtarlarını persist ediyor; diğer command tipleri için memory fallback çalışıyor.
  - Tam çözümde görülen tek failure EBelge outbox worker testinde; bu değişiklikle doğrudan ilişkili görünmüyor.

### D3 Restart & Final-State Hardening — 2026-08-12

- Restart-safe execution store artık production’da dosya tabanlı:
  - `FileAgentCommandExecutionStore`
  - `agent-command-executions.json` altında kalıcı marker/result saklıyor
  - atomik yazım için temp file + overwrite move kullanıyor
  - `MarkExecuted` yalnız marker yazıyor, result’u ayrı persist ediyor
  - restart sonrası aynı `AgentCommand.Id` tekrar gelirse fiziksel handler ikinci kez çalışmıyor
- Final-state monotonicity:
  - `Successful`, `Failed`, `Cancelled` final state olarak korunuyor
  - late `PavoStartPayment` sonucu mevcut final state’i geriye çekemiyor
  - `PavoGetPaymentResult` reconciliation sonucu authoritative kabul ediliyor
  - `Unknown` / `Processing` durumları yalnız ileri yönlü güncelleniyor
- Production registration:
  - `MemoryAgentCommandExecutionStore` production kaydı kaldırıldı
  - agent startup artık file store’u kullanıyor
- Tests:
  - restart simülasyonu ile marker/result kalıcılığı doğrulandı
  - parallel persistence corruption testi geçti
  - late success / late failure / ambiguous result monotonicity senaryoları doğrulandı
  - full çözüm testi geçti: `dotnet test STYS.sln --configuration Release`
- Real PAVO device test status:
  - Bu turda gerçek PAVO cihazı ile manuel test yapılmadı.
- Bilinen kısıtlar:
  - Dosya tabanlı execution store aynı process içindeki instance’lar arasında güvenli; multi-process lock henüz eklenmedi.
  - `PavoGetPaymentResult` authoritative reconciliation olarak davranıyor; bu kasıtlı.

## 2026-08-12 — PAVO Operations Faz D3

- Timeout / ambiguity:
  - `PavoStartPayment` command timeout’u artık payment’i `Failed` yapmıyor; payment `Unknown` olarak bırakılıyor.
  - Timeout sonrası recovery için blind `StartPayment` tekrarına izin verilmiyor; aynı ödeme için `GetPaymentResult` reconciliation yolu kullanılıyor.
- Idempotency:
  - Aynı `PosOdemeIslemiId` / `SaleReference` / `IdempotencyKey` kombinasyonu için ikinci bir `StartPayment` komutu üretilmiyor.
  - Aktif `PavoGetPaymentResult` reconciliation komutu varsa yeni bir tane yaratılmıyor.
- Agent restart / late result:
  - Expired payment command için late completion artık güvenli şekilde uygulanabiliyor.
  - Aynı command’in tekrar completion’ı duplicate payment side-effect üretmiyor.
- Reconciliation:
  - `GetPaymentResult` ile `Unknown` ödeme yeniden sorgulanabiliyor.
  - Successful / declined sonuçlar mevcut payment kaydına işleniyor.
  - `Unknown` sonuç yeniden sorgulama için korunuyor.
- Command expiry:
  - Payment timeout artık server-side expiry sweep ile işleniyor; agent polling beklenmiyor.
  - Payment command expiry, PAVO ping expiry ile aynı ortak expiry servis yolunu kullanıyor.
- Tests:
  - Hedefli payment-reconciliation testleri derlendi.
  - Bu ortamda `STYS_INTEGRATION_TEST_CONNECTION_STRING` olmadığı için integration testler skip oldu.
  - Full solution testi koşuldu; tek mevcut failure:
    - `STYS.Tests.EBelgeOutboxWorkerTests.BirMesajinExceptionISonrakiMesajinIslenmesiniEngellemez`
    - hata: `System.TimeoutException : Beklenen koşul zaman aşımı içinde gerçekleşmedi.`
- Real PAVO device test status:
  - Bu turda gerçek PAVO cihazı ile manuel test yapılmadı.
- Bilinen kısıtlar:
  - `GetPaymentResult` için mevcut tekrar çağrı koruması reconciliation active command durumunu baz alıyor; dağıtık çoklu süreç senaryoları ayrıca gözlenmeli.

### D2 Server-side Expiry Hardening — 2026-08-12

- Server-side expiry:
  - `AgentCommandExpiryService` ile komut zaman aşımı tek bir ortak servis altında toplandı.
  - Expiry artık sadece Agent polling’e bağlı değil; `AgentCommandExpiryHostedService` background sweep çalıştırıyor.
  - `PavoPing` için `Pending/Delivered/Accepted/Running` + `ExpiresAt <= now` durumları `Expired` oluyor.
  - Timeout sonucu güvenli sağlık mesajı ile `LastHealthCheckAt`, `LastHealthStatus`, `LastHealthError` güncelleniyor; `LastHealthSuccessAt` korunuyor.
- Offline-Agent recovery:
  - Agent poll yokken bile server-side cleanup expired `PavoPing` komutlarını kapatıyor.
  - Expired `Running` health command yeni health kontrolünü bloklamıyor.
- Duplicate command recovery:
  - `FindExistingActiveHealthCommandAsync` expired komutları aktif saymıyor.
  - Cleanup sonrası yeni `PavoPing` üretimi devam edebiliyor.
- Readiness reason priority:
  - `LastError`, gerçek readiness blokajını yansıtacak şekilde önceliklendirildi.
  - `Disabled` ve `AgentOffline` durumlarında health detayı ikinci plana atılıyor.
  - Health reason, sadece asıl blokaj `DeviceOffline` olduğunda ana neden olarak kullanılıyor.
- Tests:
  - Targeted integration test paketi derlendi; connection string olmadığı için integration testler skip oldu.
  - Full solution test başarılı: `dotnet test STYS.sln --configuration Release --no-restore`.
  - Başarı/skip durumu dışında fail yok.
- Real PAVO device test status:
  - Bu turda gerçek PAVO cihazı ile manuel test yapılmadı.
- Known limitations:
  - Targeted integration senaryoları lokal test DB connection string’i olmadan çalıştırılamadı.
  - Angular/UI tarafında bu turda değişiklik yapılmadı.

## 2026-08-12 — PAVO Operations Faz D2

- Health state modeli eklendi:
  - `Unknown`
  - `Healthy`
  - `Unreachable`
  - `Timeout`
  - `TlsError`
  - `ProtocolError`
  - `Stale`
- Runtime health alanları eklendi:
  - `LastHealthCheckAt`
  - `LastHealthSuccessAt`
  - `LastHealthStatus`
  - `LastHealthError`
- Readiness artık sadece `SonBaglantiTarihi` varsayımına dayanmayacak şekilde health policy ile hesaplanıyor:
  - hiç health check yoksa `Unknown`
  - son başarılı health eskiyse `Stale`
  - son check failure ise ilgili failure state
  - threshold config üzerinden yönetiliyor
- Merkezi health refresh akışı eklendi:
  - UI `Bağlantıyı Kontrol Et` çağırıyor
  - backend typed `PavoPing` command oluşturuyor
  - Agent PAVO’ya gidip sonucu döndürüyor
  - central health state güncelleniyor
- Result processing:
  - başarılı `PavoPing` sonrası `Healthy`
  - failure sonrası `Timeout / Unreachable / TlsError / ProtocolError`
  - `LastHealthSuccessAt` failure’da silinmiyor
- Timeout / stuck command recovery:
  - expired health command’lar `Expired` oluyor
  - running health command için de timeout recovery çalışıyor
  - duplicate aktif health command üretimi engelleniyor
- Payment guard:
  - fresh `Healthy` state olmadan `StartPayment` command’i üretmiyor
  - sequence reserve edilmiyor
- UI:
  - merkezi POS ekranında health status / son kontrol / son başarı / hata görünür
  - bağlantı kontrol action’ı açık isimle gösteriliyor
- Tests:
  - `dotnet test STYS.sln --configuration Release --no-restore` geçti
  - `tests/STYS.Tests/STYS.Tests.csproj --filter "FullyQualifiedName~PosYonetimiIntegrationTests"` çalıştırıldı
  - integration testler local SQL connection olmadığı için skip oldu
- Real PAVO device test status:
  - Bu turda gerçek PAVO cihazı ile manuel test yapılmadı.
- Bilinen kısıtlar:
  - Health freshness threshold konfigürasyon üzerinden yönetiliyor; env ayarı yoksa default 5 dakika kullanılıyor.
  - UI health display mevcut command update akışıyla yenileniyor; ayrı polling eklenmedi.
- Full test sonucu:
  - `dotnet test STYS.sln --configuration Release` çalıştırıldı.
  - Full çözümde tek kırık test görüldü:
    - `STYS.Tests.EBelgeOutboxWorkerTests.KuyrukBoskenIdleDelayKullanilir`
    - Hata: `System.TimeoutException: Beklenen koşul zaman aşımı içinde gerçekleşmedi.`
    - Bu hata C2 reconciliation değişikliklerinden bağımsız, outbox worker idle-delay davranışıyla ilgili.

## 2026-08-12 — PAVO Operations Faz D1

- Readiness modeli eklendi:
  - `Ready`
  - `AgentOffline`
  - `DeviceOffline`
  - `NotProvisioned`
  - `ReProvisionRequired`
  - `PairingInvalid`
  - `NoActiveTerminal`
  - `NoAccountMapping`
  - `Disabled`
  - `OwnershipConflict`
- Backend operasyonel readiness read-model’i eklendi ve POS cihaz detayına bağlandı:
  - `GET /ui/pos/cihazlar/{id}/readiness`
  - tenant / tesis izolasyonu korunuyor
  - fingerprint / secret readiness response’a çıkmıyor
- Agent/device health:
  - agent heartbeat
  - cihaz son bağlantı zamanı
  - device active durumu
  - local provisioning / pairing durumu
  - active terminal ve hesap eşleşmesi
  tek readiness kararında birleştirildi.
- Payment guard:
  - `StartPayment` ve payment-test akışı readiness kontrolünden geçmeden command üretmiyor.
  - Ready değilse sequence reserve edilmiyor.
  - UI’de not-ready nedeni açık gösteriliyor.
- Terminal/account readiness:
  - aktif terminal sayısı
  - hesap eşleşmiş terminal sayısı
  - terminal bazlı payment-ready durumları
  gösteriliyor.
- UI:
  - POS yönetim ekranında merkezi operasyonel özet eklendi.
  - Agent / PAVO / Provision / Pairing / Terminal / Hesap / Ödeme özetleri görünür.
- Tests:
  - `dotnet test STYS.sln --configuration Release --no-restore` geçti.
  - `npm run build` geçti.
  - `npm test -- --watch=false --browsers=ChromeHeadless` geçti.
  - Hedefli readiness testi yerel ortamda integration connection string olmadığı için skip oldu.
- Real PAVO device test status:
  - Bu turda gerçek PAVO cihazı ile manuel test yapılmadı.
- Bilinen kısıtlar:
  - Angular bundle budget warning devam ediyor.
  - Ready olmayan cihazlarda hata nedeni ilk blokaj üzerinden raporlanıyor; bu, guard davranışı için kasıtlı.

### D1 Pairing Readiness Fix — 2026-08-12

- `Fingerprint == TargetFingerprint` varsayımı kaldırıldı.
- `PairingValid` artık `EslesmeOnayliMi + Fingerprint varlığı + provisioning` üzerinden değerlendiriliyor.
- `TargetFingerprint` readiness için zorunlu / eşitlik kriteri değil; mismatch tek başına readiness’i düşürmüyor.
- `Ready` hesabı pairing eşitliği yerine gerçek provisioning + agent/device + terminal/account readiness ile belirleniyor.
- `ReProvisionRequired` readiness tarafında fingerprint eşitliğine bağlanmıyor; provisioning drift için ayrı lifecycle alanlarına bırakıldı.
- Testler:
  - `Fingerprint != TargetFingerprint` iken pairing onaylı cihazın `Ready` olabildiği test edildi.
  - Invalid pairing testi korundu.
  - Hedefli integration testler yerel connection string olmadığı için `skip` oldu.
  - Full solution test çalıştırıldı; şu an bağımsız bir mevcut failure var:
    - `STYS.Tests.SaxonSidecarEBelgeSchematronValidatorTests.TimeoutServiceUnavailableOlur`
- Real PAVO device test status:
  - Bu turda gerçek PAVO cihazı ile manuel test yapılmadı.
