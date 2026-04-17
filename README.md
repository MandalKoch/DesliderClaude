<div align="center">

# 🎲 DesliderClaude 🎲

### *Swipe. Rank. Play.*

**The async way to decide which boardgame your group plays tonight.**

![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Blazor](https://img.shields.io/badge/Blazor-Web%20App-5C2D91?style=for-the-badge&logo=blazor&logoColor=white)
![Aspire](https://img.shields.io/badge/.NET-Aspire-8A2BE2?style=for-the-badge&logo=dotnet&logoColor=white)
![SQLite](https://img.shields.io/badge/SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)
![PWA](https://img.shields.io/badge/PWA-Ready-FF6F00?style=for-the-badge&logo=pwa&logoColor=white)
![Status](https://img.shields.io/badge/status-🛠️%20scaffold-yellow?style=for-the-badge)

</div>

---

## 🌈 What is this?

> No more "so… what do you wanna play?" for 45 minutes while the pizza gets cold.

**DesliderClaude** is a tiny web app for a group of friends to pick a boardgame together — **asynchronously**. The host creates a **Game Night**, shares a link, everyone swipes **yes / no** on the candidate games whenever they get a minute, and a live ranking updates in real time. When the host closes voting, the top of the list is tonight's game. 🎉

Not a review site. Not BoardGameGeek. Just: *"of these 12 games, which do we actually want to play tonight?"*

---

## ✨ Features

### 🟢 MVP — async Game Night
- 🎟️ **Host creates a Game Night** — name, optional date, pick candidate games, get a share link
- 👋 **Anyone with the link joins** — just a display name, no account
- 👉👈 **Swipe UI** — yes/no through the list, change your mind until voting closes
- 📊 **Live ranking** — visible to everyone with the link, updates as swipes roll in
- 🔒 **Host closes voting** — ranking locks, top game wins

### 🟣 v1 — polish & scale
- 📚 Reusable **game library** (name, cover, player count, play time)
- ✅ Mark games as **played** so they rotate out of future candidate lists
- 🔌 **BoardGameGeek** scraping for autofill (covers, metadata)
- ⚡ **SignalR** live "X people have voted"
- 🏆 **Tournament bracket** mode for bigger libraries
- 🗂️ Game Night **history** for the host
- 🔍 Filters — *"only games for 4+ players"*, *"under 60 min"*, etc.

### 🟠 v2 — accounts & ops
- 🔐 **OAuth** login for hosts (GitHub / Google)
- 👑 **Admin role** + admin panel (manage users, Game Nights, games)
- 📊 **OpenTelemetry** metrics dashboard for admin — request rates, active Game Nights, swipe volume, errors

### 🔵 Later
- 💡 Invitee-suggested games
- 👥 Multiple groups / private groups
- 📈 Stats — which games win most, who swipes yes on what

---

## 🧱 Tech Stack

| Layer | Choice | Why |
|---|---|---|
| 🎯 Platform | **.NET 10** | Latest LTS-track runtime |
| 🛰️ Orchestration | **.NET Aspire** | One-F5 local dev, telemetry, health, service discovery |
| 🎨 UI | **Blazor Web App** (unified server + WASM) | Server for DB access, WASM for snappy swipes |
| 📱 Shell | **PWA** | "Add to Home Screen" — no app store needed |
| 💾 Storage | **SQLite + EF Core** | Plenty for a group of friends; revisit later |
| 🔔 Realtime | **SignalR** *(v1)* | Polling in MVP, SignalR when we need smooth live updates |

### 🚀 Future native path
If we want App Store / Play Store later → **.NET MAUI Blazor Hybrid** wraps the same components in a native shell.

---

## 🗺️ Project Structure *(planned)*

```
DesliderClaude.slnx
├── 🎛️  src/
│   ├── DesliderClaude.AppHost/            # Aspire orchestrator — F5 here ✅
│   ├── DesliderClaude.ServiceDefaults/    # Telemetry, health, service discovery ✅
│   ├── DesliderClaude.Web/                # Blazor Web App — server host ✅
│   ├── DesliderClaude.Web.Client/         # Blazor WASM client ✅
│   ├── DesliderClaude.Data/               # EF Core, migrations, entities 🚧
│   └── DesliderClaude.Core/               # Domain models & voting logic 🚧
└── 🧪 tests/
    └── DesliderClaude.Tests/              # 🚧
```

---

## 🧭 Conventions

- 🧹 File-scoped namespaces, nullable enabled, implicit usings on
- ⚙️ `async` / `await` everywhere for I/O — **no** `.Result` / `.Wait()`
- 📜 EF Core migrations checked in; never edit an applied one — add a new one
- 🧠 Keep voting logic in `Core` (pure, testable) — not in Blazor components
- 🔌 Blazor components inject **`Core` service interfaces** — they **never** touch `DbContext` directly. `Data` sits behind those interfaces.

---

## 🚧 Status

**📅 2026-04-18 — Scaffold pass 1 ✅.** Aspire `AppHost` + `ServiceDefaults` + Blazor Web App (server + WASM client) build green. `F5` on `AppHost` brings everything up.

✅ **Decided:** async swipe voting, Game Night model with link-invite, Blazor Web App + PWA, .NET Aspire, SQLite
⏭️ **Next:** `Core` + `Data` class libraries, `GameNight` / `Game` / `Voter` / `Swipe` entities, first EF Core migration, PWA manifest
❓ **Hosting:** TBD

---

<div align="center">

*Made with 🎲, ☕, and way too many unplayed boardgames.*

🤖 Coded with [Claude Code](https://claude.com/claude-code).

</div>
