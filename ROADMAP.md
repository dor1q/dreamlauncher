# Dream Launcher Roadmap

This roadmap keeps Dream Launcher focused on the features needed for a reliable old-Fortnite launcher on C# + WPF.

## Product Areas

- app shell with home, library, downloads, status, store/extra pages, leaderboard, and settings;
- local library for installed Fortnite builds;
- build detection from an existing install path;
- launch state with close-game controls;
- backend exchange-code flow before launching;
- chunked download, verify, repair, cancel, progress, speed, and ETA;
- live backend communication through WebSocket events;
- match/server status grouped by state and region;
- user options for content directory, launch args, gameplay tweaks, theme/layout, and account actions;
- auto-update packaging and clearer reportable errors.

## Phase 1 - Launch And Library Foundation

- [x] Discord OAuth login in launcher.
- [x] Backend endpoint that converts Discord login into a Dream exchange code.
- [x] Exchange-code placeholders in build launch arguments.
- [x] Import an existing build folder from the UI.
- [x] Validate required executable paths before launch.
- [x] Track launch state: idle, launching, launched, closing.
- [x] Close known Fortnite/Epic processes from the launcher.
- [x] Show cleaner launch errors that users can report.

## Phase 2 - Real Status Surface

- [ ] Add backend `/launcher/api/status` for backend, XMPP, matchmaker, and game server state.
- [ ] Add launcher status page/cards grouped by service.
- [ ] Poll status first, then move to WebSocket/SSE once backend events exist.
- [ ] Show active/recent matches when backend exposes them.

## Phase 3 - Downloads, Verify, Repair

- [ ] Define a Dream manifest format for available builds.
- [ ] Add content directory setting.
- [ ] Add install/verify/repair UI.
- [ ] Add progress, speed, ETA, and cancel support.
- [ ] Decide whether downloads are simple file manifests first or chunked manifests later.

## Phase 4 - Settings And Gameplay Tweaks

- [ ] Add custom launch arguments.
- [ ] Add optional gameplay flags such as simple edit, disable pre-edits, and reset on release only when the client/server supports them.
- [ ] Add layout preferences for compact/list build views.
- [ ] Add theme basics without making the launcher depend on a skin system too early.

## Phase 5 - Release Quality

- [ ] Package installer builds.
- [ ] Add auto-update strategy.
- [ ] Add crash/error report export.
- [ ] Add CI once the local .NET SDK is installed.

## Phase 6 - Optional Product Pages

- [ ] News/home feed.
- [ ] Leaderboard.
- [ ] Friends/activity.
- [ ] Store/donate pages if the backend has real data for them.
