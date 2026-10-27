# `van/` — SmartParking UI redesign prototype

This folder is a sandbox for testing a redesign of the SmartParking frontend currently in `FE/`. It is a design prototype; its fixtures and screens do not imply that APIs, prices, availability or workflows are live or approved.

## Source of truth

1. [`design-brief.md`](design-brief.md) defines product goals and user context.
2. [`design-system.md`](design-system.md) is the sole authority for visual identity, tokens, type, layout, spacing, components and screen states.
3. [`ai-ui-rules.md`](ai-ui-rules.md) defines implementation practices.
4. [`specs/driver-features-spec.md`](specs/driver-features-spec.md) and requirements/contracts in the repository define business behavior. Verify them against current source before treating inventory claims as implemented.
5. [`AGENTS.md`](AGENTS.md) defines the required workflow and conflict resolution.

Do not copy old visual rules from `FE/` or historical content that conflicts with `design-system.md`. When a standard is missing or two sources disagree, resolve the documentation first and then update the implementation as a separate, reviewable change.

## Run locally

```bash
npm install
npm run dev
```

The map prototype may require `VITE_MAPTILER_API_KEY`. Keep secrets out of source control. Use an approved key or a non-live fallback and label fixture content clearly.
