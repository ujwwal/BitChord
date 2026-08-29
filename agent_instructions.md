# BitChord WinUI Platform Migration — Agent Instructions

## Mission

This repository is undergoing a WinUI platform migration only.

The goal is to migrate the existing BitChord application from its current Android platform implementation to WinUI while preserving the app’s current frontend and backend behavior exactly. The product must remain functionally and visually consistent with the existing app. No redesigns, product changes, feature removals, or UX shifts are allowed.

This is not a rewrite of the product. This is a platform port.

## Core rule

Single governing rule:

- This is a WinUI platform migration only.
- The existing frontend must remain the same.
- The existing backend behavior must remain the same.
- The implementation may change platform-specific code, APIs, and app structure, but the product experience must remain faithful to the current app.

## Non-negotiable principles

### 1. Preserve the current app exactly

Agents must treat the Android app as the source of truth for:
- UI layout and hierarchy
- navigation patterns
- screen composition
- colors and material styling
- spacing, typography, and sizing
- icons and shapes
- loading states and transitions
- behavior and interaction semantics
`
If an implementation cho`ice changes the user-facing product, it is not acceptable unless it is a direct platform-equivalent adaptation required by WinUI.

### 2. No redesign

The following are explicitly not allowed:
- changing the layout of existing screens
- redesigning components or navigation patterns
- changing copy, labels, or wording in the application UI
- altering existing screen order or tab hierarchy
- changing product flows or feature logic
- replacing a screen with a different design concept
- introducing a “better” UX instead of a faithful port

### 3. No functional drift

Agents must not change the backend behavior or app logic beyond what is required for the platform migration.

Do not:
- remove features
- add new features
- simplify workflows
- alter default behavior
- change data contracts unless required for WinUI compatibility
- re-architect the product for convenience if it changes runtime behavior

### 4. Port, do not invent

When translating features to WinUI, the correct approach is:
- match the existing Android behavior closely
- keep the same screen structure and interaction flow
- use WinUI-native APIs where necessary
- preserve the same data semantics and visible states
- prefer parity over novelty

### 5. Maintain cross-platform consistency

Any code or UI port should be judged against the original app’s current behavior.

If a WinUI implementation is not visually or behaviorally equivalent to the current app, it is not correct.

## What is acceptable

The following are acceptable during migration:

- building the WinUI app shell and navigation structure
- porting UI components to WinUI controls while keeping the same design
- translating Android layouts and screen states into equivalent WinUI views
- using WinUI-native rendering, styling, or animation APIs when needed
- porting shared business logic and data models to platform-neutral code
- creating platform adapters that preserve the same app behavior
- improving technical compatibility when it does not alter the product experience

## What is not acceptable

The following are not acceptable:

- redesigning the UI to be “cleaner,” “more modern,” or “more native” if it differs from the current app
- removing or hiding existing functionality
- changing product flows to fit a preferred architecture
- changing how screens look, even if it seems improvement-oriented
- replacing established user flows with simplified versions
- adding optional features or convenience layers not present in the current app
- treating the migration as a greenfield product rebuild
- creating a “new version” of the app instead of a faithful platform port

## Scope boundaries

### In scope
- WinUI app shell and navigation
- platform-specific UI rendering and view hosting
- data access and API compatibility layers
- shared model and business logic porting
- state handling required for the same user experience
- performance, stability, and Windows integration required by the platform

### Out of scope
- redesigning the product
- feature expansion
- UX improvements unrelated to the platform migration
- backend re-architecture for preference or cleanliness
- introducing new product capabilities
- refactoring away from the app’s established behavior

## Decision standard

When in doubt, ask:

- Does this preserve the current app’s frontend exactly?
- Does this preserve the current backend behavior exactly?
- Does this change the product experience in any visible or functional way?
- Would this be accepted as a faithful platform port rather than a redesign?

If the answer is no to any of these, it should not be done.

## Quality bar

All migration work must meet this quality bar:
- matches the current app structure and visual design
- preserves existing user flows and product behavior
- is executable and stable in WinUI
- avoids platform-specific shortcuts that alter the product experience
- remains consistent with the Android source of truth

## Implementation guidance for agents

Agents should work in the following order:

1. understand the existing Android implementation and design
2. find the matching WinUI equivalent without changing user-facing design
3. port the platform code while preserving behavior and visual parity
4. verify the app still matches the original interface and product flow
5. stop if a change begins to redesign or drift from the existing product

## Final rule

The final answer to any migration decision is simple:

- If it preserves the app exactly and only changes the platform, it is valid.
- If it changes the app’s frontend, backend behavior, or product design, it is not valid.

This project is a WinUI platform migration only. The existing frontend and backend remain the same.
