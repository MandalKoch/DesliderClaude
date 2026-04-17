# DesliderClaude

A public-facing web app that helps a group of friends decide **which boardgame to play on a given night**.

Not a rating/review site. Think Tinder-style swipes collected asynchronously over hours or days, with a live ranking the host locks in when it's time to play.

## Goal

A host creates a **Game Night**, shares a link, and friends swipe on the candidate games whenever they have a minute. When the host closes voting, everyone sees the ranking and picks from the top.

## Core Voting Mode

**MVP = Swipe ("Tinder" style), async.**
- Each person swipes yes/no on each candidate game, at their own pace.
- Ranking = games sorted by yes-count. Top of the ranking is the pick; the rest is fallback / discussion material.
- **Later:** Tournament bracket mode as a second option for bigger libraries / more dramatic picks.

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
- [ ] Host creates a Game Night (name, optional date, candidate games), gets a share link
- [ ] Anyone with the link lands on a join page, enters a display name
- [ ] Swipe UI: voter swipes yes/no through the candidate list, can change swipes until close
- [ ] Live ranking page: anyone with the link sees current ranking and vote counts, updating as swipes come in
- [ ] Host dashboard: sees who has voted, can close voting
- [ ] On close: ranking is locked/final

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
    └── DesliderClaude.Tests/              # (planned)
```

**MigrationService / seed flow:** `AppHost` boots `MigrationService` first with the shared SQLite connection string; the service applies any pending EF migrations on startup, then stays up to serve `POST /seed`. `Web` has `.WaitFor(migrations)`, so it only starts once the DB schema is ready. In the Aspire dashboard, the `migrations` resource carries a **"Reset & seed sample data"** command — it POSTs to the service's `/seed` endpoint, which wipes every table and inserts a sample Game Night (`sample-night` share code) so you can exercise the UI without clicking through a fresh setup.

## Conventions

- C# file-scoped namespaces, nullable enabled, implicit usings on.
- `async`/`await` everywhere for I/O. No `.Result` / `.Wait()`.
- EF Core migrations checked in. Never edit an applied migration — add a new one.
- Keep session/voting logic in `Core` (pure, testable), not in Blazor components.
- Blazor components inject and call **`Core` service interfaces**; they never touch `DbContext` directly. `Data` is an implementation detail behind those interfaces.
- **Generate Guids with `Guid.CreateVersion7()`** (RFC 9562 UUID v7), never `Guid.NewGuid()` (v4). v7 is time-ordered, so inserts preserve B-tree index locality and IDs are chronologically sortable. Applies to every entity PK and any other Guid we mint.
- **`README.md` is the public marketing page, not a mirror of this file.** It describes what DesliderClaude *is* to end users — the product story, how it feels to use. Only update `README.md` when the product story itself changes (new user-facing concept, rename, repositioning). Architecture / structure / convention / status changes stay in `CLAUDE.md` and do **not** require a README update.

## Status

**2026-04-18** — Scaffold pass 2 + migration service complete. `Core` / `Data` projects wired, four entities (`GameNight`, `Game`, `Voter`, `Swipe`) with `Guid.CreateVersion7()` PKs, first migration `InitialCreate` checked in. `MigrationService` applies migrations on startup and serves `POST /seed`; AppHost exposes a **"Reset & seed sample data"** command on the `migrations` resource in the Aspire dashboard. `Web` has `.WaitFor(migrations)`. Both services share a SQLite file under `%TEMP%/desliderclaude/`. Solution builds green.
- Decided: async swipe voting, Game Night model with link-invite, Blazor Web App (unified) + PWA, .NET Aspire, SQLite. Hosting TBD.
- Share codes: Haikunator-generated `adjective-noun-NNNN` (e.g. `autumn-frog-1234`) on `GameNight.ShareCode`, unique indexed.
- Sample data: share code `sample-night`, three voters (Alice, Bob, Cara), six games, a mix of pre-swiped votes.
- Voter flow live: `/night/{shareCode}` join (sets per-Night cookie), `/night/{shareCode}/swipe` iterates through games with yes/no, `/night/{shareCode}/ranking` shows a meta-refresh live ranking. All three pages are static SSR (no SignalR circuit yet) — swipe uses form POSTs + Blazor enhanced navigation so the transitions feel smooth without full reloads.
- Next step: host flow (create a Game Night page + host dashboard with close button); then PWA manifest + service worker; then SignalR to replace the 5-second meta-refresh on the ranking page.
