# DesliderClaude

A public-facing web app that helps a group of friends decide **which boardgame to play on a given night**.

Not a rating/review site. Think Tinder-style swipes collected asynchronously over hours or days, with a live ranking the host locks in when it's time to play.

## Goal

A host creates a **Game Night**, shares a link, and friends swipe on the candidate games whenever they have a minute. When the host closes voting, everyone sees the ranking and picks from the top.

## Core Voting Mode

**MVP = Swipe ("Tinder" style), async, continuous.**
- Each person swipes yes/no on games at their own pace. **Voting never "ends"** from the voter's side — the swipe page always has another card — until the host closes the night.
- **Next-game selection is weighted random**, per voter. Games the voter has never swiped get a very high fixed weight (strongly preferred); games the voter has swiped stay in the pool with a weight that grows with time since their last swipe. Weight is never zero — even a recently-swiped game can reappear. The game just swiped is skipped as the immediate next pick when another game is available.
- **A swipe for a game the voter has already swiped overrides the previous value** (upsert on `(VoterId, GameId)`). Only the latest swipe per voter per game counts in the ranking. This is the intended workflow: revisiting a game is how you change your mind.
- Ranking = games sorted by yes-count of the latest swipe per voter. Top of the ranking is the pick; the rest is fallback / discussion material.
- **Later:** Tournament bracket mode as a second option for bigger libraries / more dramatic picks.

### Voter routes

- `/night/{shareCode}` — join (display name → cookie)
- `/night/{shareCode}/swipe` — continuous swipe loop; redirects to `/winner` when the night is closed
- `/night/{shareCode}/ranking` — full live standings (meta-refresh every 5 s while open)
- `/night/{shareCode}/winner` — celebratory top-pick hero + runners-up. Shown as the "current pick" while open and the locked-in verdict once the host closes the night

## Game Night Model

- A host creates a **Game Night** (name, optional target date, candidate game list).
- Host picks the candidate games (host-only for MVP; invitee suggestions later).
- Host shares a **link**. Anyone with the link can vote — no account required, just a display name.
- Friends swipe whenever they're ready, over hours or days. They can **change their swipes** until the host closes voting.
- **Results are visible** to anyone with the link as votes come in — live ranking updates while the Game Night is open.
- Host closes voting → ranking is finalized and locked in.
- Game Nights are persisted — history of past nights is browsable by the host.

## Tech Stack

- **.NET 10**
- **.NET Aspire** for local dev orchestration, service discovery, health checks, and OpenTelemetry out of the box. Adds an `AppHost` project that launches everything with one F5, and a `ServiceDefaults` project for shared cross-cutting config.
- **Blazor Web App** (unified server + WASM rendering modes, .NET 8+ style) — picked over standalone Blazor WASM because we need a server anyway for SQLite, and the unified model is simpler.
- **PWA** (manifest + service worker) so users can "Add to Home Screen" and get an app-like experience without app store distribution.
- **SQLite** for storage (plenty for this; revisit later if needed).
- **Entity Framework Core** for data access.
- **SignalR** — optional for MVP. Live ranking can start as polling every few seconds; upgrade to SignalR in v1 for smoother real-time updates.
- **Auth:** lightweight — host creates a Game Night behind a simple host identity (TBD: cookie-based host token, or basic OAuth). Voters just enter a display name.

### Future: Native App Path

If we later want App Store / Play Store presence, the path is **.NET MAUI Blazor Hybrid** — it wraps the same Blazor components in a native shell, so most of the MVP code carries over.

## Hosting

TBD — decide later.

## Open Questions

1. **Host identity:** cookie-based host token (simple, no login) vs. lightweight OAuth (GitHub/Google) for the host account. Voters stay anonymous (display name only).
2. **Game library source:** manual entry first. BoardGameGeek API integration later for autofill/cover images.
3. **Ranking tie-breaker:** if two games have the same yes-count, how do we order them? (Earliest-added wins? Random? Let the host nudge?)
4. **Anonymous swipes:** can anyone with the link vote as many "identities" as they want? For friends this is fine; for true public we'd need at least a per-browser vote limit. Defer until we decide how public "public" is.

## Feature Plan

### MVP — async "Game Night"
- [x] Host creates a Game Night (name, optional date, candidate games), gets a share link
- [x] Anyone with the link lands on a join page, enters a display name
- [x] Swipe UI: voter swipes yes/no through the candidate list, can change swipes until close
- [x] Live ranking page: anyone with the link sees current ranking and vote counts, updating as swipes come in
- [x] Host dashboard: sees who has voted, can close voting
- [x] On close: ranking is locked/final

### v1
- [ ] Game library (reusable across Game Nights) with name, image, player count, play time
- [ ] Host can mark a game as played (removes it from future Game Night candidate lists, or flags it so it's deprioritized)
- [ ] BoardGameGeek scraping/API integration for autofill (name, cover, player count, play time)
- [ ] Live "X people have voted" via SignalR
- [ ] Tournament bracket mode
- [ ] Game Night history for host
- [ ] Filters: "only games for 4+ players", "under 60 min", etc.

### v2
- [ ] OAuth login for hosts (GitHub/Google) — replaces the lightweight host token
- [ ] Admin role + admin panel (site-wide: manage users, Game Nights, games)
- [ ] OpenTelemetry metrics dashboard visible to admin (request rates, active Game Nights, swipe volume, errors)

### Later
- [ ] Invitee-suggested games
- [ ] Multiple groups / private groups
- [ ] Stats: which games win most often, who votes yes on what

## Project Structure (planned)

Aspire-standard layout:

```
DesliderClaude.slnx
├── src/
│   ├── DesliderClaude.AppHost/            # Aspire orchestrator — run this with F5
│   ├── DesliderClaude.ServiceDefaults/    # Shared Aspire defaults (telemetry, health, service discovery)
│   ├── DesliderClaude.MigrationService/   # Worker: applies EF migrations on startup; POST /seed resets+seeds
│   ├── DesliderClaude.Web/                # Blazor Web App — server host (UI + optional SignalR hubs)
│   ├── DesliderClaude.Web.Client/         # Blazor WASM client (Auto-interactive components)
│   ├── DesliderClaude.Data/               # EF Core DbContext, Fluent API configs, service impls
│   └── DesliderClaude.Core/               # Domain entities, service interfaces, share-code generator
└── tests/
    └── DesliderClaude.Tests/              # Playwright E2E (NUnit + Microsoft.Playwright.NUnit)
```

**MigrationService / seed flow:** `AppHost` boots `MigrationService` first with the shared SQLite connection string; the service applies any pending EF migrations on startup, then stays up to serve `POST /seed`. `Web` has `.WaitFor(migrations)`, so it only starts once the DB schema is ready. In the Aspire dashboard, the `migrations` resource carries a **"Reset & seed sample data"** command — it POSTs to the service's `/seed` endpoint, which wipes every table and inserts a sample Game Night (`sample-night` share code) so you can exercise the UI without clicking through a fresh setup.

## Conventions

- C# file-scoped namespaces, nullable enabled, implicit usings on.
- `async`/`await` everywhere for I/O. No `.Result` / `.Wait()`.
- EF Core migrations checked in. Never edit an applied migration — add a new one.
- Keep session/voting logic in `Core` (pure, testable), not in Blazor components.
- Blazor components inject and call **`Core` service interfaces**; they never touch `DbContext` directly. `Data` is an implementation detail behind those interfaces.
- **Folder layout for services:** every `Services/` folder contains the interfaces at its root, with implementations in a `Services/Imps/` subfolder (namespace `...Services.Imps`). One `Imps/` per `Services/` folder — never mix interfaces and impls at the same level.
- **Folder layout for data shapes:** entities and DTOs live in `Models/` (namespace `...Models`), never in `Services/` or `Entities/`. The distinction between "entity" (EF-mapped) and "DTO" (projection / return shape) is not a folder-level concern.
- **Generate Guids with `Guid.CreateVersion7()`** (RFC 9562 UUID v7), never `Guid.NewGuid()` (v4). v7 is time-ordered, so inserts preserve B-tree index locality and IDs are chronologically sortable. Applies to every entity PK and any other Guid we mint.
- **`README.md` is the public marketing page, not a mirror of this file.** It describes what DesliderClaude *is* to end users — the product story, how it feels to use. Only update `README.md` when the product story itself changes (new user-facing concept, rename, repositioning). Architecture / structure / convention / status changes stay in `CLAUDE.md` and do **not** require a README update.
- **Credit what we use.** When a third-party library, Claude Code skill, or external source materially shapes code we ship, credit it. Add a short line to `README.md` (footer area) linking out — author name + project/site URL. Examples already in the tree: [Haikunator](https://github.com/Atrox/haikunator) (share-code generation), the `emil-design-eng` skill / [Emil Kowalski](https://animations.dev/) (swipe-gesture polish). This rule is a product-story change (it's user-visible acknowledgement), so updating the README *is* warranted — one of the few exceptions to the rule above.
- Before you commit run `dotnet format` to format your code.
- Before you push run `dotnet format --check` to check formatting.
- Before you push run all tests with `dotnet test`.
- **Commit message style:** Use imperative mood, present tense, and focus on what the commit does, not what it fixes. Examples: "Add feature X", "Fix bug Y", "Refactor Z for clarity". Avoid "Fixes #123" in the commit message itself, as it's redundant with GitHub's auto-linking.

## Status

### Where we are (as of 2026-04-19)

**Product** — the full MVP loop works end-to-end and is live at [desliderclaude.fly.dev](https://desliderclaude.fly.dev/):

- **Auth (phase 1, local):** cookie-based login (`deslider-auth`, 30-day sliding, HttpOnly). `/register` and `/signin` are local username + password (PBKDF2-SHA256, 600k iters, versioned hash). `/account` shows the current username + provider, lets local users change their password, and has sign-out. The `ExternalLogin` table is in place for Google/Apple to drop in without a schema change.
- Home page → share-code join form is always visible. **Signed-in** users see "Host a new night →"; unauth users see "Sign in to host →" / "Create account". `/create` has `[Authorize]` (redirects to `/signin?returnUrl=/create`).
- `/create` mints a `GameNight` + `HostToken`, drops a `deslider-host-{shareCode}` cookie, redirects to `/night/{shareCode}/host`.
- Host dashboard — share URL with copy button, live counters (voters / games / swipes), compact ranking snapshot (10 s meta-refresh while open), **Close voting** button. Cookie-gated: no cookie / mismatch → "Not your night" screen.
- Voter loop unchanged — `/night/{shareCode}` join → `/swipe` continuous weighted-random deck → `/ranking` live standings → `/winner` hero view. Closing voting flips every voter's swipe screen to the winner.
- Swipe UI is still static SSR + enhanced navigation; pointer-drag gesture via `wwwroot/swipe.js`.

**Infra**
- Single-region Fly.io machine (`fra`) fronted by the automatic-HTTPS proxy; SQLite on a 1 GB mounted volume at `/data`. EF migrations apply on Web startup.
- GitHub Actions (`.github/workflows/fly-deploy.yml`) auto-deploys on push to `main` via `superfly/flyctl-actions`; `FLY_API_TOKEN` repo secret holds an app-scoped deploy token (1-year expiry).
- `main` is protected — PR-required + verified-signatures ruleset (owner bypass enabled).
- Licensed Apache 2.0.

**Tests** — `tests/DesliderClaude.Tests/` (NUnit + Playwright) drives the host flow end-to-end in real Chromium. `dotnet test` is a one-liner after the first-time `playwright install chromium`.

**Next up**
- Phase 2: link `GameNight.CreatedByUserId` + `Voter.UserId` to the logged-in user so the landing page can list "nights you're part of" (filter + order; default order = missing-vote first, then nearest `TargetDate`).
- Google OAuth — slot `Microsoft.AspNetCore.Authentication.Google` in behind the existing cookie scheme, link via the `ExternalLogin` table.
- Apple Sign-In via `AspNet.Security.OAuth.Apple` (required if we ever ship MAUI to the App Store).
- PWA manifest + service worker; then SignalR to replace the meta-refresh loops.

**Still deferred / TBD**
- Ranking tie-breaker rule (Open Question 3) — right now it's `ORDER BY YesCount DESC, Name ASC`, so alphabetical on ties. Good enough for MVP.
- Host cookie (`deslider-host-{shareCode}`) still guards the host dashboard; will be replaced by "are you the `CreatedByUserId`?" once phase 2 lands.
- Host has no way to recover a lost cookie today. Phase 2 solves this once the night is tied to a user account.

### History

**2026-04-19 (later III)** — Auth phase 1 (local username + password). New `User` entity + `ExternalLogin` table (empty for now — the shape is here so Google/Apple drop in without a schema change). `PasswordHasher` is a self-contained PBKDF2-SHA256/600k-iter hasher (no Identity framework). Cookie auth scheme (`deslider-auth`, HttpOnly, 30-day sliding). New pages: `/register`, `/signin`, `/account` (change password + sign out). `/create` is `[Authorize]`-gated. Home CTA toggles via `<AuthorizeView>`; header shows the username (linked to `/account`) when signed in, "Sign in" otherwise. Migration `AddUsers`. Six E2E tests covering the full auth + create flow.

**2026-04-19 (later II)** — Playwright E2E tests added. `tests/DesliderClaude.Tests/` is an NUnit project using `Microsoft.Playwright.NUnit` + `Microsoft.AspNetCore.Mvc.Testing`. `WebAppFixture` is a `WebApplicationFactory<Program>` that builds **two** hosts on `CreateHost` — a TestServer one for the factory's internals and a real Kestrel one bound to a random loopback port for Playwright to hit (canonical workaround for dotnet/aspnetcore#33846). Each fixture uses an ephemeral SQLite file under `%TEMP%/desliderclaude-test-*.db`. `HostFlowTests` covers the happy path (create → dashboard → close) and cookie-gating (fresh browser context hitting the host URL gets "Not your night"). `public partial class Program;` added to `Web/Program.cs` so the factory can reference it. First-time setup: `pwsh tests/DesliderClaude.Tests/bin/Debug/net10.0/playwright.ps1 install chromium`, then `dotnet test`.

**2026-04-19 (later)** — Host flow live. `/create` lets anyone spin up a Game Night (name, optional date, candidate games via one-per-line textarea). On submit, `GameNightService.CreateAsync` mints a `HostToken`; the web layer drops it in a `deslider-host-{shareCode}` cookie (365-day, not HttpOnly, mirroring `VoterCookie`) and redirects to `/night/{shareCode}/host`. The host dashboard shows the share URL (copy button via `navigator.clipboard`), voters/games/swipes counters, the live ranking snapshot (10 s meta-refresh while open), and a **Close voting** form that POSTs to `CloseAsync` with the cookie token. No cookie / mismatched token → "Not your night" state. Added `IVotingService.GetVoterCountAsync`. Home page grew a "Host a new night →" button alongside the share-code join form. Deployed to Fly.io via the auto-deploy pipeline.

**2026-04-19** — Fly.io deploy scaffolding. `Dockerfile` + `.dockerignore` + `fly.toml` at repo root target just `DesliderClaude.Web` (Aspire `AppHost` is dev-only and not deployed). `Web` now applies EF migrations on startup (`db.Database.MigrateAsync()` in `Program.cs`), so `MigrationService` is no longer required in prod — it stays for Aspire local dev and the `/seed` command. `fly.toml` mounts a volume at `/data`, points `ConnectionStrings__DesliderClaudeDb` at `/data/desliderclaude.db`, enables `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` for proxy-aware HTTPS, and pins to a single machine (SQLite + volume ⇒ no horizontal scale). First deploy: `fly launch --no-deploy --copy-config`, then `fly volume create desliderclaude_data --size 1 --region fra`, then `fly deploy`.

**2026-04-18** — Scaffold pass 2 + migration service complete. `Core` / `Data` projects wired, four entities (`GameNight`, `Game`, `Voter`, `Swipe`) with `Guid.CreateVersion7()` PKs, first migration `InitialCreate` checked in. `MigrationService` applies migrations on startup and serves `POST /seed`; AppHost exposes a **"Reset & seed sample data"** command on the `migrations` resource in the Aspire dashboard. `Web` has `.WaitFor(migrations)`. Both services share a SQLite file under `%TEMP%/desliderclaude/`. Solution builds green.
- Decided: async swipe voting, Game Night model with link-invite, Blazor Web App (unified) + PWA, .NET Aspire, SQLite. Hosting TBD.
- Share codes: Haikunator-generated `adjective-noun-NNNN` (e.g. `autumn-frog-1234`) on `GameNight.ShareCode`, unique indexed.
- Sample data: share code `sample-night`, three voters (Alice, Bob, Cara), six games, a mix of pre-swiped votes.
- Voter flow live: `/night/{shareCode}` join (sets per-Night cookie), `/night/{shareCode}/swipe` **continuous** yes/no loop with weighted-random next-game selection, `/night/{shareCode}/ranking` meta-refresh live standings, `/night/{shareCode}/winner` celebratory top-pick view. All static SSR — swipe uses form POSTs + Blazor enhanced navigation so the transitions feel smooth without full reloads.
- Swipe gesture: pointer-based drag (translate + rotate, velocity fling), HUD-stamp overlays, enter/exit animations re-inited on `Blazor.enhancedload`. Buttons remain as the primary/a11y path. `wwwroot/swipe.js` is the whole implementation.
