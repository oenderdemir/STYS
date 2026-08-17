# STYS Agent — Signed Release Publishing and Remote Upgrade

Operational guide for publishing a new Windows agent version and rolling it out to remote agents
from the STYS UI.

Supported runtime in this phase: **win-x64** only.

## Trust model

```
STYS backend  --( RSA-PSS / SHA-256 signature over the release manifest )-->  AgentRelease
                                                                                   |
                                                                                   v
                                                        Remote agent verifies with the PUBLIC key
                                                        provisioned as its trust anchor
```

The private key signs; it lives only on the STYS server. The public key is provisioned to every
agent machine by the installer. An agent applies nothing it cannot verify against its trust anchor.

## 1. Create the release signing key pair

Generate once, off the developer machine if possible, and treat the private key as a production
secret.

```bash
# Private key (STYS server only)
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:4096 -out release-private-key.pem

# Public key (agent trust anchor)
openssl rsa -in release-private-key.pem -pubout -out release-public-key.pem
```

**Never commit either file.** `trust/` is git-ignored in this repository; that is a convenience for
local work, not a place to keep production keys.

## 2. Install the private key on the STYS server

Place the file somewhere only the STYS service account can read, then point configuration at it:

```json
{
  "AgentReleasePublishing": {
    "StorageRootPath": "D:\\STYS\\agent-releases",
    "SigningPrivateKeyPemPath": "D:\\STYS\\secrets\\release-private-key.pem",
    "MaxPackageSizeBytes": 536870912
  }
}
```

Environment variable form (preferred in container/production deployments):

```
AgentReleasePublishing__SigningPrivateKeyPemPath=/run/secrets/release-private-key.pem
AgentReleasePublishing__StorageRootPath=/var/lib/stys/agent-releases
```

Notes:

- `StorageRootPath` must be **outside** the web root. Packages are served only through the
  authenticated agent endpoint, never as static files.
- Both settings are optional at startup. A deployment that never publishes releases starts
  normally; publishing fails with `Agent release signing private key yapılandırılmamış.` if the key
  is missing when someone tries.
- The private key is never written to the database, returned to the UI, logged, or placed inside a
  package.

## 3. Provision the public key to agents

The installer places the trust anchor at:

- Windows: `%ProgramData%\STYS\AgentTrust\release-public-key.pem`
- Linux: `/etc/stys-agent/trust/release-public-key.pem`

The unified installer package embeds whichever key the backend resolves at package-build time (see
`docs/agent-production-installation.md`). Use the **public half of the same key pair** configured in
step 2, or agents will reject every release you publish.

## 4. Build the release package

```powershell
.\scripts\agent\build-agent-release.ps1 -Version 1.0.1
```

Output:

```
artifacts/agent-releases/stys-agent-1.0.1-win-x64.zip
```

The script publishes `STYS.Agent` as self-contained single-file for win-x64, matching the deployment
model used by the installer, and writes the archive with install-directory contents **at the archive
root** — the updater extracts it straight over the install directory, so a wrapper folder would
break the layout.

Excluded from the package:

- `bootstrap.json` — written per machine by the installer
- `*.pdb` — pass `-IncludeSymbols` to keep them

`appsettings*.json` and `bootstrap.json` are additionally protected on the agent side: the updater
preserves the existing copies rather than overwriting operator configuration.

## 5. Publish from the STYS UI

**Agent Yönetimi → Agent Sürümleri → Yeni Sürüm Yayınla**

Fill in Version, Contract Version, Runtime (`win-x64`), optional release notes, and select the ZIP.

The backend then:

1. streams the upload to a temp file, computing SHA-256 and size **from the received bytes**
   (any client-supplied hash is ignored),
2. opens a transaction and inserts a disabled draft to obtain the release id,
3. builds the canonical manifest via `AgentReleaseManifest.BuildSignaturePayload(...)` — the id is
   part of it, which is why signing cannot happen earlier,
4. signs it with RSA-PSS/SHA-256,
5. moves the package into permanent storage,
6. stores the Base64 signature and applies the requested enabled state,
7. commits.

Any failure rolls the transaction back and deletes both the temp and any moved file. A half-written
or unsigned release is never selectable for staging.

Duplicate protection: one release per (kurum, runtime, version); a second attempt returns 409.

## 6. Stage the update on a remote agent

**Agent Yönetimi → agent seç → Uyumluluk → Güncellemeyi Hazırla**

STYS picks the highest enabled, signed release for the agent's runtime and contract version that is
newer than the agent's current version, and queues an `AgentStageUpgrade` command. The agent
downloads the package from the authenticated endpoint, verifies SHA-256, size and signature, and
reports `Staged`.

The Uyumluluk tab shows current version, recommended version, and staging state
(`Hazırlanıyor` / `Staged` / `Başarısız`).

## 7. Apply

**Güncellemeyi Uygula** — enabled only after a successful `Staged` result.

The agent writes an apply request; the `STYS Agent Updater` service performs the swap. If that
service is not installed the agent refuses with `AGENT_UPDATER_NOT_AVAILABLE` instead of waiting
for a command that nothing will execute.

## 8. Verify the update

- The agent's next heartbeat reports the new `AgentVersion`; the agent list shows it.
- Agent status returns to Online.
- `%ProgramData%\STYS\Agent\logs\agent-<date>.log` records startup of the new build.

## 9. Rollback

The updater backs up the install directory before replacing it. After the swap it starts the
service and runs a health probe; if the probe fails it restores the backup, restarts the previous
build, and reports failure. Rollback is automatic — there is no manual rollback command.

## 10. Key rotation

**Not supported in this phase.** An agent trusts exactly one release public key, and releases
already published are signed with the old key. Rotating today means: publish nothing during the
change, re-provision the new public key to every agent machine, then publish new releases. Plan a
maintenance window, or wait for staged rotation support.

## Security summary

| Property | Where enforced |
| --- | --- |
| SHA-256 computed server-side | `AgentReleasePackageStorage.WriteTempAsync` |
| Package size computed server-side | same |
| Manifest signed server-side | `AgentReleaseSigner.SignManifest` |
| Private key never leaves the server | never persisted, logged, or serialised |
| Unsigned release never staged | `AgentReleaseService.SelectBestReleaseAsync` |
| Disabled release never staged | same |
| Tenant isolation | `AgentReleasePublishingService` + `ICurrentTenantAccessor` |
| Path traversal blocked | `AgentReleasePackageStorage.MoveToFinal` |
| Package download authenticated | `GET /api/agent/releases/{id}/package` |
