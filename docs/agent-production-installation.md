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

## Windows service

Use `scripts/agent/install-agent.ps1` to install the service as `STYS Agent`.

- startup type: Automatic (Delayed Start)
- restart recovery: enabled
- service account: `NT AUTHORITY\LocalService`
- service binPath: direct `STYS.Agent.exe`
- local UI binding: loopback only
- data/log directories are created under `%ProgramData%\STYS\Agent`
- service-scoped environment overrides:
  - `STYS_AGENT_DATA_DIR`
  - `STYS_AGENT_LOG_DIR`
  - `STYS_AGENT_LOCAL_UI_PORT`
- uninstall preserves data by default

Use `scripts/agent/uninstall-agent.ps1` to remove the service. Add `-Purge` only when you also want to delete data and logs.

## Linux systemd

Use `scripts/agent/install-agent.sh` to deploy the agent and register the `stys-agent` systemd unit.

- positional parameters:
  - 1: publish directory
  - 2: install directory
  - 3: data directory
  - 4: log directory
  - 5: local UI port

- dedicated low-privilege user: `stys-agent`
- restart policy: `Restart=on-failure`
- working directory: `/opt/stys-agent`
- install directory ownership: `root:root`
- install directory permissions: read/execute for the service user, no write access
- data/log directories: `/var/lib/stys-agent` and `/var/log/stys-agent`
- local UI binding: loopback only
- unit-scoped environment overrides:
  - `STYS_AGENT_DATA_DIR`
  - `STYS_AGENT_LOG_DIR`
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
- install directory: `%ProgramFiles%\STYS\Agent Updater`
- data/log directories: `%ProgramData%\STYS\Agent\Updater`

Linux:

- publish target: `artifacts/agent-updater/linux-x64`
- install script: `scripts/agent/install-agent-updater.sh`
- unit file: `scripts/agent/stys-agent-updater.service`
- service user: `root`
- install directory: `/opt/stys-agent-updater`
- data/log directories: `/var/lib/stys-agent-updater` and `/var/log/stys-agent-updater`

Upgrade flow:

1. Backend sends `AgentStageUpgrade`.
2. Agent stages the signed package.
3. Backend sends `AgentApplyUpgrade`.
4. Agent writes a fixed apply request under its data directory.
5. The privileged updater verifies the staged release again, replaces application binaries atomically, starts the agent, and rolls back on health failure.

The updater keeps configuration/data/credentials outside the replaced application binaries and only stages trusted release artifacts selected by STYS.
