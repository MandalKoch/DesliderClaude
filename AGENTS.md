# Project guide

Read `CLAUDE.md` for established conventions and project history. Some overview/status sections are stale; prefer current source and the corrections below. This guide records a source inspection on 2026-10-27, not a runtime verification.

## Product and current behavior

DesliderClaude helps friends choose a boardgame asynchronously. Preserve the user's mobile-first web requirement. Hosts sign in, create a night, and share a link; guests can vote with a display name without an account.

- Voting picks randomly from unvoted games, once per game. After the last vote, navigate to `/night/{shareCode}/votes`; users can change or remove votes there. The old continuous weighted-random loop is obsolete.
- `/night/{shareCode}/winner` contains the standings and winner. There is no separate `/ranking` route. Ranking sorts by yes count descending, then game name.
- Host authority comes from `GameNight.CreatedByUserId` and the signed-in user, not a host-token cookie. Hosts can close/reopen nights and manage voters, including named invitations. Anonymous sessions have invite/restore links carrying voter tokens; treat those links as credentials.
- Gatherings at `/gatherings/create` and `/gathering/{shareCode}` support shared suggestions, interest voting, host-confirmed bring assignments, independent play votes, and actual played history. Guests join once per gathering; members can withdraw their own unused suggestions and hosts can manage them. Confirmed or historically used games are protected from deletion.
- Night setup and gathering pickers use explicit tap-to-add lists, saved libraries, and BGG name search. Browsing/filtering never selects games automatically. Enhanced form posts preserve picker context; same-page links must include the current path because the app has a root `<base>`.
- BGG libraries already exist at `/account/libraries`: import GeekLists or collections, refresh/delete imports, and select candidates when creating nights. Cached BGG metadata includes images, player counts, durations, recommendations, and game type. Expansions/accessories are excluded from candidates. Fully populated cached games are not fetched again on ordinary import refresh.
- Local username/password auth uses a 30-day sliding HttpOnly `deslider-auth` cookie and PBKDF2-SHA256. Anonymous voting uses a per-night 60-day cookie. `/admin` uses separate configured HTTP Basic Auth; unconfigured admin returns 404.
- Most UI is static Blazor SSR with form POSTs and enhanced navigation. Interactive server/WASM modes are registered, but that does not mean the swipe UI runs in WASM. `Web.Client` is largely scaffolding.
- README PWA/installability claims are ahead of implementation: no manifest or service worker was found. OAuth and SignalR remain future work; verify current source before treating roadmap checkboxes as authoritative.

## Code map

- `DesliderClaude.slnx`: .NET 10 solution.
- `src/DesliderClaude.Core`: entities/DTOs under `Models`, service interfaces under `Services`, pure implementations under `Services/Imps`.
- `src/DesliderClaude.Data`: EF Core 10 SQLite context, configurations, checked-in migrations, service implementations, BGG HTTP client/import cache. DI registrations in `ServiceCollectionExtensions.cs`.
- `src/DesliderClaude.Web`: server composition in `Program.cs`, pages in `Components/Pages`, auth helpers/middleware, UI assets. Pages use Core interfaces rather than accessing DbContext.
- UI entry points: `Components/App.razor`, `Components/Layout/MainLayout.razor`, `wwwroot/app.css`, `wwwroot/swipe.js`. The current aesthetic is a warm dark tabletop theme, with Outfit typography, cream text, sage and terracotta accents, dice branding, and decorative meeples. Preserve working touch gestures and accessible button alternatives.
- `src/DesliderClaude.AppHost`: Aspire 13.6.4 local orchestration; starts MigrationService and Web with shared temp-directory SQLite database.
- `src/DesliderClaude.MigrationService`: applies migrations and offers `POST /seed`. Reset/seed deletes existing data; do not invoke casually.
- `src/DesliderClaude.ServiceDefaults`: telemetry, discovery, resilience, health endpoints.
- `tests/DesliderClaude.Tests`: NUnit + Playwright Chromium E2E tests for auth/host flow, vote editing/restoration, voter management, and BGG imports. `WebAppFixture` uses temporary SQLite and real Kestrel on a random loopback port, plus TestServer; BGG is faked.

## Development and deployment

- Run local orchestration: `aspire run --apphost src/DesliderClaude.AppHost/DesliderClaude.AppHost.csproj` (or launch AppHost from Rider).
- Run web alone: `dotnet run --project src/DesliderClaude.Web`; default DB is `desliderclaude.db` relative to the working directory. Launch profiles use HTTP 5046 / HTTPS 7223.
- Build: `dotnet build DesliderClaude.slnx`. Test: `dotnet test DesliderClaude.slnx`. Browser setup after building: `pwsh tests/DesliderClaude.Tests/bin/Debug/net10.0/playwright.ps1 install chromium`.
- Per `CLAUDE.md`, format before commit and check formatting/test before push. No commit or push is implied by a task.
- Config keys: `ConnectionStrings__DesliderClaudeDb`, `Bgg__ApiToken`, `Admin__Username`, `Admin__Password`. Do not store secret values in notes or source.
- Web applies EF migrations on startup. Add migrations instead of changing applied migrations.
- Docker publishes Web only. Fly config targets app `desliderclaude`, Frankfurt, SQLite volume at `/data`, one machine. Do not introduce horizontal scaling with this storage model.
- `.github/workflows/fly-deploy.yml` deploys pushes to `main` (Markdown-only and LICENSE changes ignored), or manual dispatch. Configured hosting is not proof of current remote availability.

## Conventions and known limits

- Follow `CLAUDE.md`: file-scoped namespaces, nullable/implicit usings, asynchronous I/O, UUID v7 via `Guid.CreateVersion7()`, source-generated `[LoggerMessage]` logs, low log volume. Keep interfaces/implementations and UI/data concerns separated.
- README is public product copy; architecture and development notes belong here or in `CLAUDE.md`. Preserve required third-party credits.
- Source review is not a security audit. Voting write services accept a voter token and game ID; check game/night membership and closed-night enforcement carefully before changing these paths.
- Initial environment check: `/usr/bin/dotnet` points to an incomplete `/usr/lib/dotnet` installation and reports `Error: [/usr/lib/dotnet/host/fxr] does not exist`. `/home/andi/.dotnet/dotnet --version` succeeds with SDK `10.0.401`; use that executable in this environment.
- CLI resolution repaired on 2026-10-27 with `/home/andi/.local/bin/dotnet` symlinked to `/home/andi/.dotnet/dotnet`. The existing PATH prioritizes this directory. Plain `dotnet --version` now succeeds; the incomplete system copy remains untouched.
- A solution build with the working SDK and `--no-restore` stopped during Aspire SDK resolution: NuGet's service index was unreachable and `Aspire.AppHost.Sdk/13.2.2` could not be resolved. This does not establish that the package is absent from NuGet or that project source fails compilation. Build/tests remain unverified. Docker is not needed by the current AppHost, which starts only local .NET projects.
- Follow-up verification on 2026-10-27: restore and full solution build succeed with elevated execution/network access (0 errors, 20 dependency advisory warnings). Restricted execution cannot resolve NuGet DNS and may stop builds without diagnostics; no NuGet configuration change was needed. Advisories concern WebAssembly.Server 10.0.7, SQLitePCLRaw.lib.e_sqlite3 2.1.11, and MessagePack 2.5.192. The 19 browser tests all fail in setup because Playwright Chromium headless shell revision 1217 is not installed; no application test result has been established.
- Dependency remediation on 2026-10-27 supersedes those warnings: existing Microsoft 10.0.7 package references updated to 10.0.14; EF now resolves SQLitePCLRaw 2.1.13. AppHost explicitly pins MessagePack 2.5.307 to patch Aspire's transitive dependency without a larger Aspire upgrade. Restore and solution build succeed with zero warnings/errors; NuGet audit including transitive packages reports no known vulnerabilities across all eight projects. Playwright Chromium revision 1217 is installed. All 19 browser tests pass both before and after the dependency updates.

- Aspire upgrade on 2026-10-28: AppHost SDK now uses stable 13.6.4. Its hosting packages already require MessagePack 2.5.307, so the explicit security pin was removed. Launch profiles no longer enable anonymous dashboard access or set the removed dashboard MCP endpoint. Existing local SQLite storage and project resources are unchanged.
- Aspire verification on 2026-10-28: all 26 tests pass; upgraded AppHost starts both resources healthy in isolated mode with a separate temporary database. `AspireUseCliBundle=true` is enabled. Local CLI 13.6.4 is installed as a .NET global tool; `/home/andi/.local/bin/aspire` supplies `DOTNET_ROOT=/home/andi/.dotnet` because native executables cannot use the incomplete registered system runtime. `aspire setup --install-path /home/andi/.aspire` initialized the standard per-user bundle so ordinary MSBuild resolves Dashboard/DCP without falling back to the broken system DNX. AppHost build succeeds with zero warnings/errors.
- AppHost local-storage limitation: its SQLite path is under `Path.GetTempPath()`. Temporary-directory cleanup or a new Rider Flatpak session can therefore expose a fresh database; this is separate from the production SQLite volume. No reset or seed command was run during this work.
