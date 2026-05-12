# Dream Launcher Roadmap

This roadmap keeps Dream Launcher focused on a practical C# + WPF desktop app: authenticate the player, understand local builds, communicate with the Dream backend, launch reliably, and surface problems clearly.

## Status Legend

| Mark | Meaning |
| --- | --- |
| Done | Implemented and build-verified |
| Active | Current implementation track |
| Queued | Planned but not started |
| Blocked | Waiting on backend, assets, infrastructure, or product decision |

## Product Snapshot

| Area | Status | Notes |
| --- | --- | --- |
| WPF app foundation | Done | .NET 8 WPF shell, settings, services, local runtime files |
| Discord authorization | Done | Browser OAuth callback through local loopback listener |
| Dream backend identity | Done | Launcher requests a backend exchange code before game start |
| Local build library | Active | Existing folder import is ready; remote manifests are next |
| Status surface | Active | Backend status endpoint is wired; richer UI grouping is next |
| Downloads and repair | Queued | Needs official manifest format and content source |
| Packaging and updates | Queued | Requires release channel decision |

## Now

| Priority | Work | Outcome |
| --- | --- | --- |
| P0 | Keep WPF build green | Launcher remains easy to iterate on |
| P0 | Finish local environment setup | `dotnet`, MongoDB, backend, and VS tooling work from normal shells |
| P1 | Expand status UI grouping | Launcher can show each backend service without relying on logs |
| P1 | Harden account-link errors | User sees clear messages when Discord is not linked to a Dream account |
| P2 | Prepare build manifest schema | Future install, verify, and repair work has a stable data contract |

## Phase 0 - Repository And Environment

Goal: make the repository easy to clone, build, run, and continue.

- [x] WPF solution committed.
- [x] GitHub remote configured.
- [x] README explains setup, auth, build manifest, and backend contract.
- [x] Roadmap split into clear implementation phases.
- [x] Local setup checklist documented.
- [x] Build verified with .NET 8 SDK.
- [ ] Add CI build after final toolchain paths are stable.

## Phase 1 - Authentication And Launch Foundation

Goal: launch a selected local build only after a valid Discord-backed Dream session exists.

- [x] Discord OAuth login in launcher.
- [x] Local loopback callback listener.
- [x] Saved local Discord session.
- [x] Backend endpoint that converts Discord login into a Dream exchange code.
- [x] Exchange-code placeholders in build launch arguments.
- [x] Import an existing build folder from the UI.
- [x] Validate required executable paths before launch.
- [x] Track launch state: idle, launching, launched, closing.
- [x] Close known Fortnite/Epic processes from the launcher.
- [x] Show cleaner launch errors that users can report.
- [ ] Add explicit "Discord account is not linked" state once backend returns a stable error code.

## Phase 2 - Real Service Status

Goal: replace guesswork with a clear operational status page.

- [x] Add backend `/launcher/api/status`.
- [x] Return backend, MongoDB, XMPP, and matchmaker status.
- [x] Keep TCP game-server check as a fallback signal.
- [x] Read backend status in launcher and log service-level health.
- [ ] Add launcher status cards grouped by service.
- [ ] Add game-server process/session status once backend exposes it.
- [ ] Add recent incidents or maintenance message when the backend exposes it.
- [ ] Move from polling to WebSocket/SSE after backend events exist.

## Phase 3 - Builds, Downloads, Verify, Repair

Goal: let the launcher manage game builds instead of only launching manually imported folders.

- [ ] Define official Dream build manifest format.
- [ ] Add content directory setting.
- [ ] List available remote builds.
- [ ] Add install action.
- [ ] Add file verification.
- [ ] Add repair action for missing or changed files.
- [ ] Add progress, speed, ETA, pause/cancel, and retry behavior.
- [ ] Add disk-space checks before install.

## Phase 4 - Settings And Player Controls

Goal: keep useful controls visible without turning settings into a dumping ground.

- [ ] Add custom launch arguments.
- [ ] Add compact/list library layout preference.
- [ ] Add theme basics.
- [ ] Add account sign-out and session refresh.
- [ ] Add gameplay flags only when the client/server support them.
- [ ] Add config validation before saving settings.

## Phase 5 - Release Quality

Goal: make the launcher safe to distribute to real users.

- [ ] Create installer build.
- [ ] Decide update channel strategy.
- [ ] Add auto-update implementation.
- [ ] Add signed release artifacts if distribution requires it.
- [ ] Add crash report export.
- [ ] Add user-safe diagnostics bundle.
- [ ] Add CI checks for build and formatting.

## Phase 6 - Product Pages

Goal: add extra pages only when real backend data exists.

- [ ] Home/news feed.
- [ ] Account overview.
- [ ] Friends or activity.
- [ ] Leaderboard.
- [ ] Store/donate surface.
- [ ] Admin-maintained announcements.

## Definition Of Done

Every roadmap item is complete only when:

- the launcher builds without warnings or errors;
- the UI state is clear for success, loading, and failure;
- secrets and local sessions are not committed;
- errors include enough context for support;
- README or roadmap changes match the implemented behavior.

## Backlog Guardrails

- Prefer working launch and status features before decorative pages.
- Keep Discord authentication as the primary identity path.
- Keep local build management explicit and inspectable.
- Do not commit game files, tokens, client secrets, or user-specific runtime state.
