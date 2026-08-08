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

- Kurum izolasyonu UI controller'larda henüz tam olarak uygulanmadı (mevcut `UIController` + `ICurrentTenantAccessor` entegrasyonu ile yapılacak)
- Tesis doğrulaması enrollment sırasında yapılmıyor (Faz 2)
- Config endpoint'i hala hard-coded (Faz 2)
- Command execution altyapısı yok (Faz 2)
- HTTP resiliency (Polly retry/circuit breaker) eklenmedi (Faz 2)
- Feature flag mekanizması yok (Faz 2)
