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
| Host runner `scripts/run-schema-service.ps1` | exports non-signing `.env` keys by default; UserService alone receives signing keys to the process environment, then maps `SMARTPARK_*` keys to ASP.NET config (`ConnectionStrings__*`, `Services__*`, `DevelopmentAdmin/Owner/Operator__*`, `Otp__Smtp__*`, `Jwt__PrivateKeyPem`, `Workflow__KeyDirectory`) | add a mapping line in the runner, or name the key in ASP.NET convention (`Section__Key`) |
| Docker app stack `compose.app.yml` | containers receive only what is listed under `environment:`, interpolated from `.env` (`${KEY}`) | add an `environment:` entry, then recreate containers |
| DB / Sonar / AI Compose stacks | ports and passwords interpolated from `.env` at container creation | add interpolation in the matching compose file |
| FE host (Vite) | `FE/.env` — setup copies the template when missing and fills MapTiler + AI URLs only when empty | `FE/.env.example` (+ optional fill logic in step `[6/7]`) |
| FE container | `VITE_*` are set explicitly in `compose.app.yml`; the container does not read the root `.env` | edit the `frontend` service `environment:` in `compose.app.yml` |

> `.env` never reaches a container by itself. Compose interpolation and the user-secrets sync are the only bridges.

---

# 3. Changing an Existing `.env` Value — What to Re-run

| Change | Re-run | Notes |
|---|---|---|
| Value used by the Docker app stack (`SMARTPARK_SERVICE_KEY`, `SMARTPARK_ADMIN/OWNER/OPERATOR_*`, `SMARTPARK_SMTP_*`, `SMARTPARK_JWT_PRIVATE_KEY_PEM`) | `.\scripts\run-app-service.ps1 -Service <X\|All> -NoBuild` | Containers read env at creation; Compose recreates services whose config changed. No image rebuild needed. |
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
- `Otp__Smtp__*` and `Jwt__PrivateKeyPem` **are** passed to the `user-service` container by the app stack (`compose.app.yml`); `Otp:Sms:*`, `Onboarding:PublicOrigin`, and `Services:StructureReallocation` are **not** — they have no provisioning path yet (host or container), so set them manually or add them to `.env.example`, the runner, setup, and `compose.app.yml`.

---

# 4. Adding a New Key — Step-by-Step

## 4.1 Template and local env

1. Add the key (placeholder value + short comment) to `.env.example` — committed. Signing placeholders matching `replace-with-*` are treated as "not configured" by setup.
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

# 8. JWT signing and public trust (SPARK-327)

The loader now excludes private JWT names and clears inherited private aliases by default. Only the UserService runner opts into signing-secret export; it restores the prior signing environment afterward. `-NoExport` only parses and does not modify environment. Do not launch unrelated services from a shell where signing secrets were manually exported.

| .env source | Setup user-secrets and host environment | Docker environment | Recipient |
| --- | --- | --- | --- |
| `SMARTPARK_JWT_ISSUER` | `Jwt:Issuer` / `Jwt__Issuer` | `Jwt__Issuer` | UserService |
| `SMARTPARK_JWT_AUDIENCE` | `Jwt:Audience` / `Jwt__Audience` | `Jwt__Audience` | UserService |
| `SMARTPARK_JWT_KEY_ID` | `Jwt:KeyId` / `Jwt__KeyId` | `Jwt__KeyId` | UserService |
| `SMARTPARK_JWT_PRIVATE_KEY_PEM_BASE64` | `Jwt:PrivateKeyPemBase64` / `Jwt__PrivateKeyPemBase64` | `Jwt__PrivateKeyPemBase64` | UserService only |
| `SMARTPARK_JWT_PRIVATE_KEY_PEM` (legacy) | `Jwt:PrivateKeyPem` / `Jwt__PrivateKeyPem` | `Jwt__PrivateKeyPem` | UserService only |
| `SMARTPARK_JWT_JWKS_PATH` | `Jwt:JwksPath` / `Jwt__JwksPath` | `Jwt__JwksPath` | UserService |
| issuer / audience above | `JwtValidation:Issuer`, `JwtValidation:Audience` (host uses `__`) | same `__` names | Facility, Operation, Payment |
| UserService URL + JWKS path | `JwtValidation:JwksUri` / `JwtValidation__JwksUri` | `http://user-service:8080` + configured path | Facility, Operation, Payment |

Setup skips `replace-with-*` signing placeholders; runtime rejects them as malformed if supplied directly. The template's configured KeyId requires a real stable key. Both PEM forms must represent the same key if populated. `setup.ps1` never generates or silently replaces a signing key. For a fresh private local checkout, the explicit opt-in `scripts/initialize-assistant-docker.ps1` creates a dedicated local JWT/TLS/keyring set and refuses pre-existing signing material. `SMARTPARK_JWT_PRIVATE_KEY_FILE` resolves relative to the root `.env` directory in setup, host and Docker User runners; only User receives its contents. Keep does not merge new template fields. See LOCAL-SETUP §5.5 for manual RSA 3072 generation, Base64 encoding, reconfiguration and rotation limits. JWT changes require no database recreation. Consumer settings prepare a future middleware contract only.

# Cloudinary development media settings (SPARK-373)

Add these fields manually to an existing ignored local `.env`; Keep does not merge template additions:

```dotenv
SMARTPARK_CLOUDINARY_CLOUD_NAME=
SMARTPARK_CLOUDINARY_API_KEY=
SMARTPARK_CLOUDINARY_API_SECRET=
SMARTPARK_USER_MEDIA_ENABLED=false
```

Setup never generates provider credentials. Obtain development values privately; never put them in `FE/.env`, `VITE_*`, browser responses, committed files or rendered configuration logs. The same credential grants its provider permissions to every recipient; folders do not isolate services.

| Consumer | Bridge | Enabled behavior |
|---|---|---|
| User / Facility (Parking) / Operation (Reservation) / Payment direct .NET launches | `setup.ps1` maps the three optional names to `MediaStorage:Cloudinary:CloudName`, `ApiKey`, `ApiSecret` in each project's user-secrets; runner maps to equivalent `__` environment names. | Only User receives `MediaStorage:Enabled`. Other services receive credentials only. |
| Four .NET containers | `compose.app.yml` backend environment mappings | Only `user-service` receives the feature switch. |
| Python `ai-rest` and `ai-live` | `compose.ai.yml` explicitly supplies the three `SMARTPARK_CLOUDINARY_*` names | Configuration only; `ai-live` database settings remain present. |
| Frontend / databases / RabbitMQ / Sonar | No media credential mapping | Excluded. |

The feature defaults off even when credentials are present. Enabled UserService requires all three settings; startup validation reports setting names only. Startup behavior must be verified against the delivered media implementation, not inferred from the configuration mappings.

After rotation or removal, rerun `./setup.ps1 -SkipDatabase -SkipRestore` for user-secrets. It replaces changed values and removes blank/missing optional credentials. Optional Team imports use existing conflict handling; reconcile conflicting local values explicitly. The feature switch is a local opt-in, not a Team credential import. Restart direct-host processes; the runner reads the parsed file, clears stale mapped credentials for that launch, and restores the caller's media environment on exit. Recreate affected containers using `./scripts/run-app-service.ps1 -Service <X|All> -NoBuild` and the existing AI launch procedure. No database reset is needed for credential changes.

Run `powershell.exe -NoProfile -File scripts/test-setup.ps1` and `powershell.exe -NoProfile -File scripts/test-media-compose.ps1`. Both use synthetic fixtures; the latter renders configuration only, suppresses values, and does not start containers. Live Cloudinary verification is separate.

Automatic image deletion is deferred under [USER-MEDIA-CLEANUP-01](../05-project-management/decision-log/2026-11-06-user-media-cleanup-deferral.md); timestamps and expired-read blocking belong to this foundation. Do not equate read blocking with provider byte deletion.

# Related Documents

- `QUICK_START.md` — onboarding happy path, progress lines, flags.
- `LOCAL-SETUP.md` — detailed local setup, Docker app stack, volume/migration.
- `UPDATE_SCRIPT_GUIDE.md` — setup.ps1 structure and maintenance rules.
- `docs/07-skills/SmartParking_Git_Conventional_Commits_Jira_SKILL_v1.0.md` — commit/PR conventions.
