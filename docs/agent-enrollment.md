# STYS Agent — Enrollment & Approval Lifecycle

Bu doküman, Windows'a kurulmuş bir STYS Agent'ın ilk çalıştırmada STYS'e nasıl kaydolduğunu, kalıcı
credential edindiğini ve kurum politikası gerektiriyorsa onaydan sonra nasıl normal çalışma moduna
geçtiğini anlatır.

## Lifecycle

```
Installer (E2D3 unified package)
    │  bootstrap config + tek kullanımlık enrollment code
    ▼
Enrollment            POST /api/agent/enroll
    │
    ▼
Registration          Agent kaydı + AgentCredential (ClientId / ClientSecret)
    │                 enrollment code consume edilir (tek kullanımlık)
    ▼
RequiresApproval?
    ├── NO  ─────────────────────────────────────────┐
    │                                                │
    └── YES → PendingApproval                        │
                 │  POST /api/agent/enrollment/status│  (credential ile polling)
                 │                                   │
                 ▼                                   │
             STYS operatörü onaylar                  │
             POST /api/ui/agent/{id}/approve         │
                 │                                   │
                 ▼                                   ▼
              Authentication    POST /api/agent/auth/token  → kısa ömürlü JWT
                 │
                 ▼
              Configuration → Heartbeat → Command polling → Online
```

## Kavramlar

| Kavram | Anlamı |
|---|---|
| `AgentInstallationSession` | Installer/initial deployment oturumu. |
| Enrollment code | Yeni Agent'ın ilk kayıtta kullandığı kısa ömürlü, **tek kullanımlık** secret. |
| `Agent` | STYS'teki kalıcı agent kaydı. |
| `AgentInstanceId` | Agent'ın makinede ürettiği ve `instance.id` dosyasında sakladığı stabil kimlik. Makine adı veya IP kimlik değildir. |
| `AgentCredential` | Register sonrası verilen kalıcı `ClientId` + `ClientSecret`. |
| Access token | `auth/token` üzerinden alınan kısa ömürlü JWT. Kalıcı secret değildir. |
| `PendingApproval` | Agent kaydolmuş, credential almış, ancak henüz operasyonel erişimi yok. |

`PendingApproval`, "offline" demek değildir: lifecycle durumu ile bağlantı durumu ayrı kavramlardır.

## Security

### Tek kullanımlık enrollment code

- Kod `RandomNumberGenerator` ile üretilir (karışması kolay karakterler alfabede yoktur).
- **Veritabanında plaintext tutulmaz.** Sadece `CodeHash` (SHA-256) ve tanıma amaçlı, tek başına
  kullanılamayan kısa `CodePrefix` saklanır. Ortak hash mantığı:
  `backend/Agent/Services/AgentEnrollmentCodeHasher.cs`.
- Plaintext kod **yalnızca** üretildiği response'ta döner (`AgentEnrollmentCodeDto.Code`). Listeleme
  çağrıları her zaman `Code = null` döndürür; UI listede sadece prefix gösterir.
- Kod kurum (ve varsa installation session) ile bağlıdır; başka kurum için kullanılamaz.
- Kullanıldığında `KullanimSayisi` artar ve limit dolunca `Durum = Used` olur.

### Replay ve concurrency

- Register tek transaction'dır: Agent + credential oluşturma ve kodun consume edilmesi atomiktir.
  Ağ hatası nedeniyle transaction commit edilmezse kod tüketilmez ve Agent retry edebilir.
- `AgentEnrollment.ConcurrencyToken` bir EF concurrency token'ıdır ve her kayıtta döndürülür. Aynı
  kodla paralel iki register geldiğinde ikisi de aynı orijinal token'ı okur; yalnızca ilki
  `SaveChanges` ile eşleşir, ikincisi `DbUpdateConcurrencyException` alır ve generic hata ile
  reddedilir. **İkinci bir Agent oluşmaz.**

### Bilgi sızdırmama

`POST /api/agent/enroll` anonim bir endpoint olduğu için geçersiz/expired/kullanılmış/revoke edilmiş
kodların hepsi **aynı** generic mesajı döndürür. "Kod var ama süresi dolmuş" gibi ayrımlar saldırgana
yardımcı olacağı için verilmez.

### Loglama

Loglanmaz: enrollment code plaintext, agent credential, access token, Authorization header.
Loglanabilir: `InstallationSessionId`, `AgentId`, durum geçişi, generic başarı/başarısızlık nedeni,
makine adı.

### Local secret storage (Windows)

Agent credential'ı `FileAgentCredentialStore` üzerinden **DPAPI** (`ProtectedData`,
`DataProtectionScope.CurrentUser`) ile şifrelenmiş olarak saklanır; plaintext json/config'e yazılmaz.
Register başarılı olduğunda process'teki `STYS_ENROLLMENT_CODE` temizlenir — kod artık gerekli
değildir, Agent yalnızca kalıcı credential kullanır.

### Token lifecycle

- Credential kalıcıdır ve revoke/rotate edilebilir; access token geçicidir ve yenilenir.
- `PendingApproval`, `Rejected`, `Disabled`, `Revoked` durumlarındaki agent'lar token **alamaz**
  (403).
- `Reject` ve `Disable`/`Revoke` mevcut credential'ları da devre dışı bırakır
  (`AktifMi = false`, `RevokedAt` set edilir, `CredentialVersion` artar). Daha önce alınmış bir
  access token, TTL'i dolana kadar geçerli kalır; bu fazda token blacklist kurulmamıştır.

## Approval

Yetkili STYS kullanıcısı (`StructurePermissions.AgentYonetimi.Manage`):

| Endpoint | Geçiş |
|---|---|
| `POST /api/ui/agent/{id}/approve` | `PendingApproval → Active` |
| `POST /api/ui/agent/{id}/reject` | `PendingApproval → Rejected` (credential iptal edilir) |
| `POST /api/ui/agent/{id}/disable` | `→ Disabled` |
| `POST /api/ui/agent/{id}/revoke` | `→ Revoked` |

Tenant izolasyonu `EnforceKurumAccess` ile sağlanır: Kurum A yöneticisi Kurum B agent'ını
onaylayamaz/reddedemez.

`AgentDurum`: `PendingApproval = 0`, `Active = 1`, `Disabled = 2`, `Revoked = 3`, `Rejected = 4`.
`Rejected`, hiç onaylanmamış bir kaydın reddedilmesidir; `Revoked` ise daha önce onaylanmış bir
agent'ın erişiminin geri alınmasıdır.

## Agent startup state machine

```
local credential var mı?
├── hayır → enrollment code var mı?
│            ├── hayır → bekle (worker'lar başlamaz)
│            └── evet  → Register
│                          ├── Durum = Active          → token al → Online
│                          └── Durum = PendingApproval → credential sakla, token İSTEME
└── evet  → POST enrollment/status
             ├── Approved         → token al → Online
             ├── PendingApproval  → beklemeye devam (15 sn aralıkla)
             └── Rejected/Disabled/Revoked → re-enrollment gerekli olarak işaretle
```

Worker'lar (`heartbeat`, `command polling`, konfigürasyon, PAVO) `AgentAuthenticationState`
üzerinden gate edilir; `MarkAuthenticated()` çağrılmadan hiçbiri başlamaz. `PendingApproval`
durumunda bu çağrı yapılmaz, dolayısıyla pending bir agent komut çekmez ve heartbeat göndermez.

Polling: kimlik doğrulama denemeleri 5 saniyede bir; agent'ın `PendingApproval` olduğu bilindiğinde
15 saniyeye düşer ve bekleme durumu log'a **yalnızca bir kez** yazılır (log spam olmaz).

## Troubleshooting

| Belirti | Sebep / çözüm |
|---|---|
| "Enrollment kodu geçersiz veya kullanılamaz durumda." | Kod yanlış, süresi dolmuş, zaten kullanılmış veya iptal edilmiş olabilir. Endpoint güvenlik nedeniyle ayrım yapmaz. Yeni kod üretin. |
| Agent "onay bekleniyor" durumunda kaldı | Kurum politikası onay gerektiriyor. STYS → Agent Yönetimi ekranından **Onayla**'ya basın. Agent 15 sn içinde kendiliğinden aktifleşir; yeniden kurulum gerekmez. |
| Agent reddedildi | Credential iptal edilmiştir. Agent yeniden kaydolmalıdır: yeni enrollment code üretip kurulumu tekrarlayın. |
| Kod listede görünüyor ama okunamıyor | Beklenen davranış. Plaintext kod yalnızca üretim anında gösterilir; listede sadece prefix vardır. Kod kaybolduysa yenisini üretin. |
| Backend erişilemiyor | Agent enrollment code'u tüketmez ve retry eder. STYS adresi/TLS/ağ erişimini kontrol edin. |
| Agent farklı bir STYS adresine taşındı | Mevcut local credential geçersiz sayılır ve re-enrollment istenir. |

## Migration notu

`E2D4EnrollmentCodeHashing` migration'ı `Code` kolonunu `CodeHash` olarak yeniden adlandırır,
`CodePrefix` ekler ve **mevcut satırlardaki plaintext kodları SHA-256 hash'lerine dönüştürür**
(idempotent; zaten hash'lenmiş satırlar atlanır). Hash tek yönlü olduğu için rollback plaintext'i
geri getirmez: rollback sonrası eski kodlar kullanılamaz, yeni kod üretilmelidir. Mevcut Agent
kayıtlarının `Durum` değeri değişmez — çalışan agent'lar `PendingApproval`'a düşmez.
