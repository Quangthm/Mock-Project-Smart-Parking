# SmartParking Environment Configuration Skill

**Version:** 1.0
**Project:** SmartParking
**Scope:** `.env`, `.env.example`, `FE/.env`, Compose environment, user-secrets sync
**Status:** Team convention (companion to `UPDATE_SCRIPT_GUIDE.md`)

---

# 1. Purpose

Use this guide whenever you:

- change an existing value in `.env`; or
- add a new configuration key (port, API key, feature flag, …) to `.env.example` and `.env`.

Goals:

- know exactly which script must be re-run after a `.env` change;
- make sure a new key actually reaches the process that needs it (no silent no-op configuration);
- keep secrets out of Git, out of logs, and out of command arguments.

---

# 2. Configuration Flow Map

`.env` is local-only (gitignored). `.env.example` is the committed template. A value reaches a process only through a defined bridge:

| Consumer | How it reads configuration | Where a new key must be registered |
|---|---|---|
| `setup.ps1` steps `[2/7]`/`[3/7]` | loads `.env` via `scripts/load-env.ps1`; reuses/generates known credentials and validates them | credential generation list, validation lists |
| user-secrets (Visual Studio, direct `dotnet run`) | step `[5/7]` syncs the `$desired` map: writes missing keys, overwrites differing values, removes optional keys no longer in `.env` | `$desired` hashtable (+ `$removable` list for optional keys) |
| Host runner `scripts/run-schema-service.ps1` | exports **all** `.env` keys to the process environment, then maps `SMARTPARK_*` keys to ASP.NET config (`ConnectionStrings__*`, `Services__*`, `DevelopmentAdmin/Owner/Operator__*`, `Otp__Smtp__*`, `Jwt__PrivateKeyPem`, `Workflow__KeyDirectory`) | add a mapping line in the runner, or name the key in ASP.NET convention (`Section__Key`) |
| Docker app stack `compose.app.yml` | containers receive only what is listed under `environment:`, interpolated from `.env` (`${KEY}`) | add an `environment:` entry, then recreate containers |
| DB / Sonar / AI Compose stacks | ports and passwords interpolated from `.env` at container creation | add interpolation in the matching compose file |
| FE host (Vite) | `FE/.env` — setup copies the template when missing and fills MapTiler + AI URLs only when empty | `FE/.env.example` (+ optional fill logic in step `[6/7]`) |
| FE container | `VITE_*` are set explicitly in `compose.app.yml`; the container does not read the root `.env` | edit the `frontend` service `environment:` in `compose.app.yml` |

> `.env` never reaches a container by itself. Compose interpolation and the user-secrets sync are the only bridges.

---

# 3. Changing an Existing `.env` Value — What to Re-run

| Change | Re-run | Notes |
|---|---|---|
| Value used by the Docker app stack (`SMARTPARK_SERVICE_KEY`, `SMARTPARK_ADMIN/OWNER/OPERATOR_*`, `SMARTPARK_SMTP_*`) | `.\scripts\run-app-service.ps1 -Service <X\|All> -NoBuild` | Containers read env at creation; Compose recreates services whose config changed. No image rebuild needed. |
| Any value used by the host runner | restart `.\scripts\run-schema-service.ps1 -Service X` | The runner reads `.env` on every start. |
| Visual Studio / direct `dotnet run` (user-secrets) | `.\setup.ps1 -SkipDatabase -SkipRestore` | Step `[5/7]` overwrites differing user-secrets. |
| DB ports `SMARTPARK_DB_*_PORT` | `.\setup.ps1 -SkipRestore`, then restart runners | DB containers are recreated with the new port mapping. |
| MapTiler / AI endpoint keys for FE | edit `FE/.env` directly, restart Vite | Setup never overwrites a non-empty `FE/.env`. |
| `VITE_*` of the FE container, compose ports | edit `compose.app.yml`, then `run-app-service.ps1 -NoBuild` | |
| `SMARTPARK_DEV_PASSWORD` when DB volumes exist | **no script fixes this** | PostgreSQL keeps the password used at volume init. Restore the original value, run `.\setup.ps1 -RecreateDb` (destroys dev data), or `ALTER ROLE` inside each DB. Containers become unhealthy; a full setup run throws `authentication failed`. |
| Seeded admin/owner/operator passwords | same caveat | Seed runs once; existing DB records keep the old password. Restore the value or use `-RecreateDb`. |

Rules that always apply:

- Start app containers through `scripts/run-app-service.ps1` (or the `.cmd` wrappers). Docker Desktop "Start" / `docker start` reuse the previous image and environment — they do **not** rebuild or recreate.
- `-NoBuild` skips only the image build; Compose still recreates containers whose config changed.
- Full `setup.ps1` is needed only for: fresh volumes, `-RecreateDb`, or when the repo updates the setup scripts — not for ordinary `.env` edits.
- `Otp__Smtp__*` **is** passed to the `user-service` container by the app stack (`compose.app.yml`); `Jwt:*`, `Otp:Sms:*`, and `Onboarding:PublicOrigin` are **not** — add them to `compose.app.yml` if containers must use them.

---

# 4. Adding a New Key — Step-by-Step

## 4.1 Template and local env

1. Add the key (placeholder value + short comment) to `.env.example` — committed. Placeholder values matching `replace-with-*` are treated as "not configured" by setup.
2. Add the real value to your local `.env`. Setup does **not** copy unknown keys into an existing `.env`; every developer must add them manually.
3. Never commit real secrets; never print key values into logs or command arguments.

## 4.2 Wire the key to its consumer(s)

Example — a key consumed by backend containers and the host runner:

```yaml
# compose.app.yml
environment:
  Feature__NewFlag: ${SMARTPARK_FEATURE_NEW_FLAG:?Set SMARTPARK_FEATURE_NEW_FLAG}
```

```powershell
# scripts/run-schema-service.ps1 — needed when the .env name follows SMARTPARK_*
# and must be mapped to an ASP.NET config key.
if ($env:SMARTPARK_FEATURE_NEW_FLAG) { $env:Feature__NewFlag = $env:SMARTPARK_FEATURE_NEW_FLAG }
```

```powershell
# setup.ps1 step [5/7] — when the key must also reach user-secrets.
$desired['Feature:NewFlag'] = $settings['SMARTPARK_FEATURE_NEW_FLAG']
# Optional key (removed when absent from .env): also append it to the $removable list.
```

If the key is a secret that must be generated or validated, extend step `[2/7]` (generation, with the `"[\r\n']"` guard) and step `[3/7]` (validation, e.g. port range).

Frontend:

- Host Vite: add it to `FE/.env.example`; to auto-fill on setup, extend step `[6/7]`. Otherwise each developer adds it to `FE/.env` manually.
- FE container: add it to the `frontend` service `environment:` in `compose.app.yml` — the container does not read the root `.env`.

## 4.3 Verify

```powershell
# PowerShell 5.1 parse. Keep the UTF-8 BOM when the file contains non-ASCII text:
# PowerShell 5.1 reads BOM-less scripts as ANSI and can fail to parse them.
powershell.exe -NoProfile -Command '& { $e=$null; [void][System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path setup.ps1).Path,[ref]$null,[ref]$e); if (@($e).Count) { @($e).Count } else { 0 } }'

# Compose render without printing secrets
docker compose --project-name smartpark-app --env-file .env -f compose.app.yml config --quiet

# Credential/import/idempotence harness (scratch checkout, no real DB/user-secrets)
.\scripts\test-setup.ps1
```

## 4.4 Apply and document

- Containers: `.\scripts\run-app-service.ps1 -Service <X|All>` (default build) or `-NoBuild` for env-only changes.
- Host runner: restart the affected service.
- User-secrets: `.\setup.ps1 -SkipDatabase -SkipRestore`.
- Docs: when the key affects other developers, update `QUICK_START.md` / `LOCAL-SETUP.md` — same rule as `UPDATE_SCRIPT_GUIDE.md`: "add the key to `.env.example`, wire setup/runner, and update the guides."

---

# 5. Anti-Patterns

- Adding a key to `.env` without wiring any consumer — silent no-op.
- Expecting `setup.ps1` to propagate a new `.env.example` key into existing `.env` files — it will not.
- Committing real secrets into `.env.example`, docs, or commits.
- Starting containers with `docker start` / Docker Desktop Start — stale image and stale environment.
- Expecting root `.env` changes to reach the FE container — `VITE_*` are set in `compose.app.yml`.
- Changing `SMARTPARK_DEV_PASSWORD` on an initialized volume and expecting it to apply.
- Logging secrets: setup sends credential JSON to `dotnet user-secrets set` through stdin only — keep it that way.

---

# 6. Pre-Change Checklist

- [ ] I know which consumer(s) read this key (setup step, user-secrets, host runner, compose, FE).
- [ ] The key exists in `.env.example` with a placeholder (no real secret).
- [ ] The value exists in my local `.env`.
- [ ] Every consumer is wired (compose `environment:`, runner mapping, `$desired`, FE env).
- [ ] Generated/validated keys are registered in steps `[2/7]`/`[3/7]`.
- [ ] Verified: PS 5.1 parse, `docker compose config --quiet`, `scripts/test-setup.ps1`.
- [ ] Re-ran the correct script for the change type (section 3).
- [ ] No secret was committed, logged, or printed.

---

# 7. Related Documents

- `QUICK_START.md` — onboarding happy path, progress lines, flags.
- `LOCAL-SETUP.md` — detailed local setup, Docker app stack, volume/migration.
- `UPDATE_SCRIPT_GUIDE.md` — setup.ps1 structure and maintenance rules.
- `docs/07-skills/SmartParking_Git_Conventional_Commits_Jira_SKILL_v1.0.md` — commit/PR conventions.
