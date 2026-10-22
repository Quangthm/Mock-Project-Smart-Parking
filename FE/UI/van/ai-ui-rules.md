# UI implementation rules for `van/`

This document governs implementation. It does not define a second visual system: follow [`design-system.md`](design-system.md) for all visual decisions and [`design-brief.md`](design-brief.md) for product intent.

## 1. Design from the task, not from a component checklist

Before editing JSX/CSS, establish:

- the user and task;
- the primary information and the next safe action;
- secondary actions and what can be deferred;
- data, permissions and relevant failure/empty/pending states;
- desktop and mobile reading/order/interaction.

Sketch the screen regions and hierarchy first. A screen must have a clear primary focus. Do not add a dock, floating panel, card, badge, icon or animation simply because a component exists. Reuse only where it improves consistency and usability.

## 2. Implement against design tokens

- Use the semantic CSS tokens and type/spacing/radius scales from `design-system.md`.
- Do not introduce new hex/rgb/OKLCH values or arbitrary spacing in component markup. If a repeated design need is missing, propose a token and add it centrally with a documented role.
- Use `Britti Sans` for headings and `Manrope` for body/UI, with the fallback and asset requirements in `design-system.md`. Do not silently substitute fonts.
- Keep layouts calm and flat. Avoid decorative gradients, neon/glow, heavy shadows, repeated translucent glass panels and card-within-card structures.
- `style={{...}}` is allowed only for runtime values not representable with the project's static classes (for example, map coordinates or a measured canvas value). Put reusable visual rules in CSS/tokens/components.

## 3. Component and interaction implementation

- Use the project's existing React, TypeScript, Tailwind, Radix and Lucide stack. Prefer existing shared UI components where their behavior and appearance match the design.
- Use CVA for a shared component with real variants; do not add CVA to one-off markup solely to satisfy a rule. Keep class merging in the established `cn()` utility when available.
- Use Lucide icons consistently. Keep icon-only controls accessible and large enough to operate.
- Implement visible keyboard focus, disabled, pressed and other relevant states. Do not rely on hover for essential actions. Respect reduced-motion preferences.
- Use semantic HTML, labels, keyboard operation, accessible names and sensible focus order. Color is never the only way to convey status.

## 4. Project structure and boundaries

- Keep business features grouped under `src/features/<feature>/` where that pattern already exists.
- A feature's `index.ts` is its public entry point when the feature is consumed elsewhere. Avoid importing another feature's private implementation files.
- Shared primitives belong in `src/components/ui/`; feature-specific composition belongs with its feature.
- Do not reorganize architecture unrelated to the requested screen. Do not create abstractions that add indirection without repeated use or clear ownership.
- Keep prototype fixtures separate from production/API assumptions and label sample content in the UI where a user could mistake it for live data.

## 5. Screen-state and content integrity

Implement the states appropriate to the flow: initial/loading, populated, empty, error/retry, pending/unknown and confirmed. Do not imply that data is current, an action succeeded, or a slot is allocated unless the source confirms it. Keep booking, payment, pass and physical-slot state separate.

Use real labels, prices, units and policies from source requirements. If a fixture is needed for visual design, mark it as sample data and avoid false product claims.

## 6. Review the whole screen

Before handing back a UI change, review at desktop and mobile sizes:

1. Can a person identify the page purpose and primary action quickly?
2. Is content grouped by task and is the hierarchy apparent without reading every label?
3. Is there enough space around controls and groups, with no cramped columns or collisions?
4. Are font, color, spacing, radius, borders and elevation consistent with the design system?
5. Are navigation, keyboard focus, touch targets and all relevant screen states usable?
6. Can fixture or uncertain information be mistaken for live/confirmed data?

Fix hierarchy, content grouping or responsive layout before polishing small effects. Do not report a visual check unless the rendered UI was actually inspected.
