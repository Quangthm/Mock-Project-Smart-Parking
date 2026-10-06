# SmartParking Git Commit & Jira Linking Skill

**Version:** 1.0  
**Project:** SmartParking  
**Repository workflow:** GitHub + Jira  
**Jira project key:** `SPARK`  
**Status:** Team convention  
**Automated commit/branch validation:** Deferred for now

---

## 1. Purpose

Use this guide whenever you create a branch, commit code, or open a Pull Request for SmartParking.

The goals are to:

- keep Git history clear and reviewable;
- make each commit describe one logical change;
- follow **Conventional Commits 1.0.0**;
- link development activity back to the correct Jira work item using its `SPARK-<number>` key;
- make branches, commits, and Pull Requests easy for teammates and mentors to trace.

This guide adapts the team's existing Git workflow to **GitHub + Jira** while preserving the existing principles of clear commits, one focused change per commit, Pull Request review, and the `main` / `develop` development flow.

---

## 2. Core Rule

For normal development work, always connect Git activity to the Jira work item you are implementing.

Example Jira work item:

```text
SPARK-42 — Implement OTP login validation
```

Recommended traceability:

```text
Jira
SPARK-42
   |
   v
Branch
feat/SPARK-42-otp-validation
   |
   v
Commits
feat(auth): validate OTP expiration [SPARK-42]
test(auth): add OTP expiration tests [SPARK-42]
   |
   v
Pull Request
feat(auth): validate OTP expiration [SPARK-42]
   |
   v
develop
```

> **Important:** Keep the Jira key exactly as Jira shows it, for example `SPARK-42`. Use uppercase letters.

---

# 3. Conventional Commit Format

Use:

```text
<type>[optional scope]: <description> [JIRA-KEY]
```

For SmartParking:

```text
<type>(<scope>): <description> [SPARK-123]
```

Example:

```text
feat(auth): implement OTP verification [SPARK-42]
```

This keeps the commit compatible with the Conventional Commits structure while also giving Jira a work-item key to associate with the development activity.

## Components

### `type`

Describes the kind of change.

### `scope`

Optional, but recommended when it clearly identifies the affected module.

Examples:

```text
auth
user
reservation
parking
payment
notification
gateway
ai
database
infra
docs
```

### `description`

A short description of **what changed**.

Good:

```text
feat(auth): add OTP expiration validation [SPARK-42]
```

Bad:

```text
update code
fix bug
changes
work done
```

### Jira key

Add the relevant Jira key to the first line:

```text
[SPARK-42]
```

Do **not** use the Jira key as the scope:

```text
# Avoid
feat(SPARK-42): add OTP validation
```

The scope should describe the area of the codebase; the Jira key identifies the work item.

---

# 4. Team Commit Types

## `feat`

Use when adding a new feature or new user-visible/system capability.

```text
feat(auth): implement OTP login [SPARK-42]
feat(reservation): create reservation command [SPARK-81]
feat(parking): add slot availability endpoint [SPARK-96]
```

## `fix`

Use when correcting a bug.

```text
fix(auth): reject expired OTP codes [SPARK-47]
fix(reservation): prevent duplicate slot allocation [SPARK-103]
```

## `docs`

Use for documentation-only changes.

```text
docs(api): document login endpoint [SPARK-55]
docs(reservation): clarify allocation states [SPARK-110]
```

## `test`

Use when adding or changing tests without changing production behavior.

```text
test(auth): add invalid OTP test cases [SPARK-42]
test(user): add permission revocation tests [SPARK-68]
```

## `refactor`

Use when restructuring code without intentionally changing its behavior.

```text
refactor(auth): extract token generation service [SPARK-73]
refactor(user): simplify permission lookup [SPARK-68]
```

## `perf`

Use for performance improvements.

```text
perf(parking): reduce slot availability query time [SPARK-121]
```

## `build`

Use for build system or dependency-related changes.

```text
build(api): update Entity Framework Core packages [SPARK-130]
```

## `ci`

Use for CI/CD workflow changes.

```text
ci(github): add backend build workflow [SPARK-134]
```

## `chore`

Use for maintenance work that does not fit the types above.

```text
chore(repo): update gitignore for local Postman files [SPARK-140]
```

## `style`

Use only for formatting/style changes that do not change behavior.

```text
style(api): apply formatting to authentication handlers [SPARK-145]
```

---

# 5. One Logical Change Per Commit

A commit should focus on one coherent change.

Good:

```text
feat(auth): add OTP expiration validation [SPARK-42]
```

Then separately:

```text
test(auth): add OTP expiration tests [SPARK-42]
```

Avoid:

```text
feat(auth): add OTP, update reservation, fix docs and change database [SPARK-42]
```

If the work contains unrelated changes, split it into multiple commits.

This makes:

- code review easier;
- debugging easier;
- reverting safer;
- Jira traceability clearer;
- Git history easier to understand.

---

# 6. Branch Naming with Jira Keys

Use the Jira key in feature/fix branches.

Recommended format:

```text
<type>/<JIRA-KEY>-<short-description>
```

Examples:

```text
feat/SPARK-42-otp-validation
feat/SPARK-81-create-reservation
fix/SPARK-103-duplicate-slot-allocation
docs/SPARK-110-reservation-allocation-spec
refactor/SPARK-68-permission-check
```

Use short, meaningful, lowercase descriptions separated by hyphens.

## Main branches

The project keeps the existing branch roles:

```text
main
develop
```

- `main` — accepted/release-ready code.
- `develop` — integration branch for ongoing development.

Normal feature/fix work should branch from the appropriate team integration branch and return through a Pull Request.

## Release branches

When the team creates a release branch for a sprint/release, use:

```text
release/sprint-1
release/sprint-2
```

If the release itself has a Jira work item, the Jira key may also be included.

---

# 7. Pull Request Naming

Use the same Conventional Commit style and include the Jira key.

Recommended:

```text
<type>(<scope>): <description> [SPARK-123]
```

Examples:

```text
feat(auth): implement OTP login [SPARK-42]
fix(reservation): prevent duplicate slot allocation [SPARK-103]
docs(api): document authentication endpoints [SPARK-55]
```

This is especially useful if the repository later uses **Squash and Merge**, because the PR title can become the final squash commit message while remaining Conventional-Commit compatible.

---

# 8. Pull Request Requirements

Every implementation Pull Request should:

- reference the Jira work item;
- use the Jira key in the PR title;
- explain what changed;
- stay focused on the intended work item;
- avoid unrelated code changes;
- contain no secrets or hardcoded credentials;
- target the correct branch, normally `develop` during active development;
- be reviewed before merge according to the team's workflow.

Example:

```markdown
## Jira

SPARK-42

## Change Description

- Added OTP expiration validation.
- Rejects OTPs older than the configured validity period.
- Added validation handling for invalid and expired OTP cases.

## Notes

No database schema changes.
```

---

# 9. Complete Example Workflow

Assume Jira contains:

```text
SPARK-42 — Implement OTP expiration validation
```

## Step 1 — Create branch

```bash
git checkout develop
git pull
git checkout -b feat/SPARK-42-otp-expiration
```

## Step 2 — Implement one logical change

```bash
git add .
git commit -m "feat(auth): validate OTP expiration [SPARK-42]"
```

## Step 3 — Add tests as a separate logical commit

```bash
git add .
git commit -m "test(auth): add OTP expiration cases [SPARK-42]"
```

## Step 4 — Push branch

```bash
git push -u origin feat/SPARK-42-otp-expiration
```

## Step 5 — Create Pull Request

PR title:

```text
feat(auth): validate OTP expiration [SPARK-42]
```

Target:

```text
develop
```

After review and approval, merge according to the team's agreed GitHub workflow.

---

# 10. Multiple Jira Work Items

Prefer **one branch / Pull Request focused on one Jira work item**.

If two Jira issues represent genuinely separate changes, split the work.

Preferred:

```text
feat(auth): add OTP verification [SPARK-42]
```

and:

```text
fix(auth): handle locked account login [SPARK-47]
```

Avoid combining unrelated Jira items into one commit:

```text
feat(auth): implement multiple changes [SPARK-42] [SPARK-47] [SPARK-81]
```

If one unavoidable change truly satisfies multiple tightly related Jira work items, list all relevant keys, but this should be the exception rather than the default.

---

# 11. Breaking Changes

Conventional Commits supports breaking changes using `!` and/or a `BREAKING CHANGE:` footer.

Example:

```text
feat(api)!: replace legacy login response contract [SPARK-160]

BREAKING CHANGE: login responses now return the new authentication result schema.
```

Use this only when the change breaks an existing API, contract, behavior, or compatibility expectation.

A breaking change should be clearly discussed in the Pull Request before merge.

---

# 12. Commit Body and Footers

For simple changes, one line is enough:

```text
fix(auth): reject expired OTP codes [SPARK-47]
```

For complex changes, add a body after a blank line:

```text
fix(auth): reject expired OTP codes [SPARK-47]

Validate OTP expiration before authentication succeeds.
Return the existing authentication failure response when the OTP is expired.
```

You may also use footers when needed:

```text
Refs: SPARK-47
```

However, for this project, keeping the Jira key in the first line is recommended because it is immediately visible in Git history and development tooling.

---

# 13. Good vs Bad Examples

| Bad | Better |
|---|---|
| `update` | `docs(api): update login response example [SPARK-55]` |
| `fix bug` | `fix(auth): reject expired OTP codes [SPARK-47]` |
| `login changes` | `feat(auth): add OTP verification [SPARK-42]` |
| `SPARK-42 login` | `feat(auth): implement OTP login [SPARK-42]` |
| `feat(SPARK-42): login` | `feat(auth): implement OTP login [SPARK-42]` |
| `feat: stuff` | `feat(reservation): add reservation creation endpoint [SPARK-81]` |
| `fix(auth): fix` | `fix(auth): prevent login for locked accounts [SPARK-47]` |

---

# 14. Quick Decision Guide

Ask:

```text
Did I add a capability?
    -> feat

Did I correct incorrect behavior?
    -> fix

Did I only change documentation?
    -> docs

Did I only add/change tests?
    -> test

Did I reorganize code without changing intended behavior?
    -> refactor

Did I improve performance?
    -> perf

Did I change CI/CD?
    -> ci

Did I change build/dependencies?
    -> build

Is it repository/maintenance work?
    -> chore
```

Then choose an optional scope and append the Jira key.

Example:

```text
fix(reservation): prevent conflicting slot claims [SPARK-103]
```

---

# 15. Pre-Commit Checklist

Before committing:

- [ ] I know which Jira work item this change belongs to.
- [ ] I used the correct `SPARK-<number>` key.
- [ ] My commit uses a meaningful Conventional Commit type.
- [ ] My scope, if used, describes the affected module.
- [ ] My description explains the actual change.
- [ ] The commit contains one logical change.
- [ ] I did not commit secrets, credentials, local environment files, or generated junk.
- [ ] I did not mix unrelated refactors/features/fixes into the same commit.

Before opening a Pull Request:

- [ ] The branch contains the Jira key.
- [ ] The PR title follows the team format.
- [ ] The PR references the Jira work item.
- [ ] The PR targets the correct branch.
- [ ] The change description is understandable to another teammate.
- [ ] Unrelated changes have been removed or split into another branch/PR.

---

# 16. Team Rules Summary

For SmartParking, use these as the default:

```text
Branch:
feat/SPARK-42-otp-validation

Commit:
feat(auth): validate OTP expiration [SPARK-42]

Pull Request:
feat(auth): validate OTP expiration [SPARK-42]

Target:
develop
```

The key principles are:

1. **Use Conventional Commits.**
2. **Include the Jira key.**
3. **Keep one logical change per commit.**
4. **Keep branches and PRs focused on their Jira work item.**
5. **Do not use vague messages such as `update` or `fix bug`.**
6. **Use Pull Requests for review before merging into shared branches.**

---




