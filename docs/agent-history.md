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

- Heartbeat endpoint'i statik yanıt dönüyor (Faz 2'de dinamik hale gelecek)
- Config endpoint'i hard-coded değerler dönüyor (Faz 2'de DB tabanlı olacak)
- Command polling endpoint'i henüz implemente edilmedi (Faz 2)
- Agent SQLite offline storage implemente edilmedi (Faz 3)
- Client certificate auth implemente edilmedi (Faz 4)
