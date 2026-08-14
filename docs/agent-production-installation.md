# STYS Agent Production Installation

## Publish

Windows:

```powershell
dotnet publish agent/STYS.Agent/STYS.Agent.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o artifacts/agent/win-x64
```

Linux:

```bash
dotnet publish agent/STYS.Agent/STYS.Agent.csproj -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true -o artifacts/agent/linux-x64
```

Updater publish:

Windows:

```powershell
dotnet publish agent/STYS.Agent.Updater/STYS.Agent.Updater.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o artifacts/agent-updater/win-x64
```

Linux:

```bash
dotnet publish agent/STYS.Agent.Updater/STYS.Agent.Updater.csproj -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true -o artifacts/agent-updater/linux-x64
```

Development secrets, bootstrap values, credentials, and enrollment codes are not part of the publish output. The agent stores runtime state under its data directory at first run.

## Unified installer package

The recommended deployment path for initial installation is the unified installer package produced by the backend for an installation session.

- package download endpoint:
  - `GET /api/ui/agent-installations/{id}/package`
- package root scripts:
  - `install-stys-agent.ps1`
  - `install-stys-agent.sh`
- package layout:
  - `agent/` publish output
  - `updater/` publish output
  - `config/bootstrap.json`
  - `trust/release-public-key.pem`
  - `scripts/agent/` helper scripts

The bootstrap config is written by STYS and contains only operational bootstrap values such as base URL, local UI port, target RID, and display name. It does not contain enrollment codes, client secrets, private signing keys, or JWTs.

The Windows and Linux installers prompt for the enrollment code interactively at install time and keep it out of script arguments, logs, and package contents.

## Trust boundary and release key provisioning

Agent and updater both resolve the trusted release verification key from a deterministic, externally provisioned source. The private signing key is not deployed with the runtime artifacts.

- expected public key file:
  - Windows: `%ProgramData%\STYS\AgentTrust\release-public-key.pem`
  - Linux: `/etc/stys-agent/trust/release-public-key.pem`
- environment override:
  - `STYS_AGENT_RELEASE_PUBLIC_KEY_PEM_PATH`
- optional inline override:
  - `STYS_AGENT_RELEASE_PUBLIC_KEY_PEM`

If the public key cannot be resolved, upgrade staging/apply fails closed.

## Windows service

Use `scripts/agent/install-agent.ps1` to install the service as `STYS Agent`.

- startup type: Automatic (Delayed Start)
- restart recovery: enabled
- service account: `NT AUTHORITY\LocalService`
- service binPath: direct `STYS.Agent.exe`
- local UI binding: loopback only
- data/log directories are created under `%ProgramData%\STYS\Agent`
- shared runtime data root:
  - `%ProgramData%\STYS\Agent`
- updater private data root:
  - `%ProgramData%\STYS\AgentUpdater\private`
- trust anchor directory:
  - `%ProgramData%\STYS\AgentTrust`
- service-scoped environment overrides:
  - `STYS_AGENT_SHARED_DATA_DIR`
  - `STYS_AGENT_UPDATER_PRIVATE_DATA_DIR`
  - `STYS_AGENT_LOG_DIR`
  - `STYS_AGENT_RELEASE_PUBLIC_KEY_PEM_PATH`
  - `STYS_AGENT_LOCAL_UI_PORT`
- uninstall preserves data by default

Use `scripts/agent/uninstall-agent.ps1` to remove the service. Add `-Purge` only when you also want to delete data and logs.

## Linux systemd

Use `scripts/agent/install-agent.sh` to deploy the agent and register the `stys-agent` systemd unit.

- positional parameters:
  - 1: publish directory
  - 2: install directory
  - 3: shared data directory
  - 4: updater private data directory
  - 5: log directory
  - 6: local UI port
  - 7: release public key path

- dedicated low-privilege user: `stys-agent`
- restart policy: `Restart=on-failure`
- working directory: `/opt/stys-agent`
- install directory ownership: `root:root`
- install directory permissions: read/execute for the service user, no write access
- shared data/log directories: `/var/lib/stys-agent` and `/var/log/stys-agent`
- updater private data directory: `/var/lib/stys-agent-updater`
- local UI binding: loopback only
- unit-scoped environment overrides:
  - `STYS_AGENT_SHARED_DATA_DIR`
  - `STYS_AGENT_UPDATER_PRIVATE_DATA_DIR`
  - `STYS_AGENT_LOG_DIR`
  - `STYS_AGENT_RELEASE_PUBLIC_KEY_PEM_PATH`
  - `STYS_AGENT_LOCAL_UI_PORT`

Use `scripts/agent/uninstall-agent.sh` to remove the unit and binaries. Pass `--purge` only when you also want to delete data and logs.

## Privileged updater

Upgrade apply is handled by a separate privileged updater service. The updater does not run inside the agent service account and does not reuse agent credentials.

Windows:

- publish target: `artifacts/agent-updater/win-x64`
- install script: `scripts/agent/install-agent-updater.ps1`
- service name: `STYS Agent Updater`
- service account: `LocalSystem`
- service binary path: direct `STYS.Agent.Updater.exe`
- updater install directory: `%ProgramFiles%\STYS\Agent Updater`
- agent install directory target: `%ProgramFiles%\STYS\Agent`
- shared data directory: `%ProgramData%\STYS\Agent`
- updater private data directory: `%ProgramData%\STYS\AgentUpdater\private`
- updater log directory: `%ProgramData%\STYS\AgentUpdater\logs`
- release public key path: `%ProgramData%\STYS\AgentTrust\release-public-key.pem`

Linux:

- publish target: `artifacts/agent-updater/linux-x64`
- install script: `scripts/agent/install-agent-updater.sh`
- unit file: `scripts/agent/stys-agent-updater.service`
- service user: `root`
- updater install directory: `/opt/stys-agent-updater`
- agent install directory target: `/opt/stys-agent`
- shared data directory: `/var/lib/stys-agent`
- updater private data directory: `/var/lib/stys-agent-updater`
- log directory: `/var/log/stys-agent-updater`
- release public key path: `/etc/stys-agent/trust/release-public-key.pem`

Upgrade flow:

1. Backend sends `AgentStageUpgrade`.
2. Agent stages the signed package.
3. Backend sends `AgentApplyUpgrade`.
4. Agent writes a fixed apply request under its data directory.
5. The privileged updater verifies the staged release again, replaces application binaries atomically, starts the agent, and rolls back on health failure.

The updater keeps configuration/data/credentials outside the replaced application binaries and only stages trusted release artifacts selected by STYS.
