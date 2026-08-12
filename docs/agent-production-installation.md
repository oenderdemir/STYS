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

Development secrets, bootstrap values, credentials, and enrollment codes are not part of the publish output. The agent stores runtime state under its data directory at first run.

## Windows service

Use `scripts/agent/install-agent.ps1` to install the service as `STYS Agent`.

- startup type: Automatic (Delayed Start)
- restart recovery: enabled
- service account: `NT AUTHORITY\LocalService`
- local UI binding: loopback only
- data/log directories are created under `%ProgramData%\STYS\Agent`
- uninstall preserves data by default

Use `scripts/agent/uninstall-agent.ps1` to remove the service. Add `-Purge` only when you also want to delete data and logs.

## Linux systemd

Use `scripts/agent/install-agent.sh` to deploy the agent and register the `stys-agent` systemd unit.

- dedicated low-privilege user: `stys-agent`
- restart policy: `Restart=on-failure`
- working directory: `/opt/stys-agent`
- data/log directories: `/var/lib/stys-agent` and `/var/log/stys-agent`
- local UI binding: loopback only

Use `scripts/agent/uninstall-agent.sh` to remove the unit and binaries. Pass `--purge` only when you also want to delete data and logs.
