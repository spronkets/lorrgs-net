# LorrgsNET (.NET + Svelte)

LorrgsNET is a local/self-hosted tool for comparing World of Warcraft cooldown
usage from Warcraft Logs. Explore specialization rankings, raid compositions,
and individual reports using aligned encounter timelines.

The application uses an ASP.NET Core API and a Svelte frontend. It has no built-in
user accounts or relational database; successful Warcraft Logs responses are
cached to reduce external requests.

## Origin and attribution

LorrgsNET is a personal, unofficial adaptation of [lorrgs.io](https://lorrgs.io/).
Its Python API was ported to .NET, and the frontend was rebuilt from scratch in
Svelte. The original project is available at [gitarrg/lorrgs](https://github.com/gitarrg/lorrgs).

Combat data comes from [Warcraft Logs](https://www.warcraftlogs.com/), and item
and ability tooltips are provided by [Wowhead](https://www.wowhead.com/).
World of Warcraft images and game artwork are copyright Blizzard Entertainment,
Inc. World of Warcraft, Warcraft, and Blizzard Entertainment are trademarks or
registered trademarks of Blizzard Entertainment, Inc. in the U.S. and/or other countries.

LorrgsNET is not affiliated with or endorsed by lorrgs.io, Warcraft Logs, Wowhead,
or Blizzard Entertainment. It is not the official hosted Lorrgs service.

## What it does

Choose an edition, raid, and boss, then open **Spec Rankings** or **Comp Rankings**.
Both flows carry the selected encounter and support shareable URLs with browser
back/forward restoration. **World Data** and **Rotation Analyzer** are available
under Tools.

### Spec Rankings

Compare ranked players on a shared elapsed-time axis. Timelines show observed
casts, buff intervals, deaths, and fight-specific phase transitions rather than
stretching each player's fight to the same length.

- Filter by region, kill time, and date; sort by performance, duration, or date.
  Original ranks are preserved. Date ranges include the entire final day in the
  browser's local timezone.
- Filters apply to the entire returned ranking page, without client-side pagination.
  Warcraft Logs supports a page argument, not a custom result count; `size` means raid size.
- Complete cached timelines render immediately. The top three eligible missing
  timelines load concurrently. Additional timelines can be loaded on demand.
- Changing filters or sort prioritizes the new top three and reuses loaded rows.
  Failed rows can be retried; leaving the workspace cancels pending requests.

### Comp Rankings

Search execution-ranked kills by edition, raid, boss, difficulty, and composition.
Entering from navigation waits for **Search**; shared URLs containing `compPage`
restore the search automatically.

**Searching and inspecting kills**

- One query retrieves up to 50 kill summaries in upstream ranking order.
- The first three kills are inspected initially. Use **Load kill details** to
  inspect remaining rows and retry failed inspections.
- Role/spec counts support `eq`, `lt`, `gt`, `lte`, and `gte`. All supplied filters
  must match. Blank counts are unrestricted; zero excludes that role/spec.
- Kill-time fields use masked `m:ss` durations. The API uses seconds, and existing
  links using seconds remain supported.
- Duration filters apply immediately. Composition filters apply only to inspected
  rosters; uninspected kills remain visible as **Composition pending**.
- The initial three kills are not backfilled to find three matches. A page with
  no inspected matches is not exhaustive: inspect remaining rows.

**Rankings and rosters**

Rows retain upstream ranking order and rank labels when filtered.
Headers show `#<rank> Report name (Top %)`, with speed alongside
and total ranked kills in tooltips. Guild, server, and region appear when available.
Raid size is shown in raid selection, not as a per-kill statistic.

Raid speed/execution values come from the selected fight's WCL rankings:
`rank`, `best`, `totalParses`, and `rankPercent`. They are not calculated from
individual player parses; unavailable values remain null.

Expand **Roster & cooldown timelines** to use the shared timeline workspace.
Players are grouped into Tanks, Healers, and DPS, then sorted by parse percentile
within each group. Healers use HPS parses; tanks and damage dealers use DPS.
Missing parses are unavailable, not zero.

Player guilds come from character metadata only when membership is unambiguous.
The report's guild is not assumed to be every player's guild. Unknown specs remain
in rosters with their upstream spec name and role.

Private, deleted, or archived reports retain summary rows with explanatory errors
and retry actions. Any detail failure, including HTTP 429, pauses automatic loading
without silently requesting replacement kills. Navigation and edition changes
cancel outstanding work.

### Shared timeline tools

**Boss references and phases**

The reference row shows real enemy casts and observed phases, not an estimated
boss schedule. It defaults to the first filtered/sorted report. Use the target
button beside a player's analyzer action to choose another fight.

Reference selection survives sorting; filtering out that report selects the first
visible one. Changing references cancels the previous request. Cached bundled
references need no extra load; otherwise only the selected reference is fetched.

Each player row uses its own observed phases, including before event loading.
Phase metadata is batched and reused from enrichment or cached boss metadata.
Empty phase lists are known data; missing phases are not guessed or copied from
the reference fight.

**Spell selection and inspection**

- Presets include Everything, class/spec Damage choices, Tank, Healing, and
  Consumables. Composition timelines also offer Raid cooldowns.
- Unsupported roles are hidden. Classes with several damage specs have separate
  spec choices; Rogue uses Combat in Classic editions and Outlaw in Retail.
- Presets use upstream tags, recognized ability names, and observed power windows.
  Unknown effects remain available under Everything instead of being assigned a guessed role.
- Active presets apply to newly loaded timelines. Clicking an active preset or
  Clear spells clears selection, including newly loaded spells. Individual
  spell/group toggles switch to custom selection.
- Hover or keyboard-focus a spell/event to highlight it across rows and inspect
  its player, timestamp, and measured uptime. Cast intervals are measured gaps,
  not cooldown estimates.
- Cast, buff, death, and icon toggles control visibility. Separate lanes help with
  overlaps; Spell names expands icon buttons into labels.

External buffs are distinguished from abilities cast by the player. Gold-accented
power windows identify observed effects such as Bloodlust or Power Infusion,
not calculated DPS gains. Cooldown recovery and cast-time overlays are not inferred
from cast counts or hardcoded modern spell values.

**Zoom and mobile layout**

At 100% zoom, the longest visible fight fits the chart. Higher zoom levels expand
aligned tracks with shared horizontal scrolling while preserving row heights.
The hover magnifier uses the next zoom level without additional requests; it is
disabled at maximum zoom or with Separate spells enabled. Spell highlighting and
measured intervals remain available.

Fight-end markers and duration bands use existing fight metadata before events
load. On phones, player details sit above tracks and controls have larger touch
targets. Rows expand with the page instead of introducing nested vertical scrolling.

**Notes and analyzer links**

Copy note opens a live MRT (Method Raid Tools / Smart CD Reminder) or NSRT
(Northern Sky Raid Tools) preview. Export selected observed casts with spell IDs,
tenth-second timing, optional player/nickname or specialization targeting, and
phase-relative timers where available. NSRT also includes encounter ID, difficulty,
and boss name.

Missing required metadata and partial timelines are identified in the editor.
Inferred markers never become exported casts. Copy to clipboard or select the
preview for manual copying; clipboard failures remain visible. Export requires
no additional WCL requests.

Analyzer links carry the selected fight and player identity. Player names are used
when actor IDs are missing; ambiguous names require manual selection. The separate
Rotation Analyzer retains raw cast metrics.

> **Use timings as a reference.** A successful cast sequence is not a universal
> cooldown plan. Adjust it for your raid's composition, strategy, damage output,
> and phase transitions.

## Download

Clone the repository and open its folder:

```powershell
git clone https://github.com/spronkets/lorrgs-net.git
Set-Location .\lorrgs-net
```

Alternatively, choose **Code > Download ZIP** on
[GitHub](https://github.com/spronkets/lorrgs-net), extract it, and open the extracted
folder in VS Code or a terminal.

## Set up

### Prerequisites

- **Local development:** .NET SDK 10, npm, and a Node.js version supported by Vite
  (20.19+ or 22.12+).
- **Containers:** Docker Desktop, or Docker Engine with Compose.
- **Either option:** Warcraft Logs OAuth client credentials.

### Warcraft Logs credentials

1. Sign in to [Warcraft Logs](https://www.warcraftlogs.com/).
2. Open [API clients](https://www.warcraftlogs.com/api/clients/) and create an application.
   A localhost URL is sufficient for local development.
3. Copy the client ID and secret into one of the configurations below.

Never commit real credentials.

**Local API:** create `Lorrgs.Api/appsettings.local.json`. The API loads this
optional override at startup.

```json
{
  "WarcraftLogs": {
    "ClientId": "your_client_id",
    "ClientSecret": "your_client_secret"
  }
}
```

**Docker:** copy `.env.example` to `.env`, then set:

```env
WARCRAFTLOGS_CLIENT_ID=your_client_id
WARCRAFTLOGS_CLIENT_SECRET=your_client_secret
# Optional overrides:
# WARCRAFTLOGS_BASE_URL=https://www.warcraftlogs.com/api/v2/client
# WARCRAFTLOGS_AUTH_URL=https://www.warcraftlogs.com/oauth/token
```

Compose maps these credentials to `WarcraftLogs__ClientId` and
`WarcraftLogs__ClientSecret` in the API container.

### Install local dependencies

Run from the repository root:

```powershell
dotnet restore .\Lorrgs.slnx
npm --prefix .\lorrgs-web ci
```

Reinstall frontend dependencies when `package-lock.json` changes.

## Run the application

### Local terminals

From the repository root, start the API and frontend in separate terminals:

```powershell
dotnet run --project .\Lorrgs.Api\Lorrgs.Api.csproj
```

```powershell
npm --prefix .\lorrgs-web run dev
```

### VS Code tasks

Open the Command Palette and choose **Tasks: Run Task**:

- **Dev: run full stack** starts both services after dependencies are installed.
- **API: restore**, **API: build**, and **API: run** manage the backend.
- **Web: install**, **Web: dev server**, and **Web: test** manage the frontend.

### Docker

From the repository root:

```powershell
docker compose up --build
```

To stop the containers:

```powershell
docker compose down
```

## Open the application

Once both services are running, open the web URL in a browser:

| Service | Default URL |
|---|---|
| Web application | http://localhost:5173 |
| API | http://localhost:5247 |
| Swagger (Development) | http://localhost:5247/swagger |

The web and API ports are the same for the local and Compose setups.

## Other details

### Caching and request handling

**GraphQL responses**

Successful WCL responses are stored as JSON in
`Lorrgs.Api/Data/cache/wcl-graphql` for **10 days** by default. Identical queries
and variables reuse these files across API restarts. OAuth tokens remain memory-only.

Set `WarcraftLogs:GraphQlCacheTtl` (environment:
`WarcraftLogs__GraphQlCacheTtl`) using .NET `TimeSpan` format (`d.hh:mm:ss`).
The default is `"10.00:00:00"`; the minimum is one minute.
Refresh bypasses reads, saves successful new responses, and restarts their TTL.

World-data lists and the configured raid catalog are returned directly without
separate caches. These legacy settings are unused:

- `WarcraftLogs:GraphQlCacheTtlSeconds`
- `Cache:DefaultTtl` / `Cache:DefaultTtlSeconds`
- `WorldData:CacheTtl` / `WorldData:CleanupIntervalMinutes`

Old world-data cache files are ignored and may be deleted.

**Report-wide timeline reuse**

Comp details retrieve report-wide fight metadata and all pages of cooldown casts,
buffs, and deaths, including other bosses and wipes. Raw damage/healing events are
not downloaded. Player timelines and boss references are derived from this shared
stream, avoiding separate requests when expanding rosters or switching roles.

Spec Rankings checks the same cached stream before requesting player events.
Cache-only reconstruction never contacts WCL or treats partial coverage as complete.
Missing/expired report-wide data falls back to the player-specific loader rather
than automatically downloading the full report.

After complete report-wide data is verified on disk, covered player-specific event
cache pages are deleted. Failed/incomplete downloads retain them; other reports
and editions are untouched.

Missing actor or spell metadata can be recovered from report master data.
Cache-only loading uses cached metadata; explicit loading may fetch it through
the same file cache. Timelines lacking required metadata are not bundled as complete.

**Errors and rate limits**

- GraphQL failures and partial-error responses are not cached as successes.
  HTTP errors carry `Cache-Control: no-store`.
- HTTP 429 immediately pauses upstream requests to that host for `Retry-After`
  (one minute when absent). Valid cached responses remain available; enrichment
  stops rather than trying the remaining batches.
- Private/missing report timelines return explanatory 403/404 Problem Details.
  These reflect upstream availability, not application login requirements.
- Healthy partial ranking results are retained. If an access error cannot identify
  a report, only the failed batch is retried individually. Cached data is not
  revalidated with extra permission requests.
- Rejected enrichment operations stop their remaining batches and are logged.
  Available ranking metadata still returns.

### API behavior

The API is public with unrestricted CORS and no built-in authentication.

- `/api/worlddata` requires `edition`. Aliases include `midnight` -> `retail`,
  `mop` -> `mistsofpandaria`, and `classic` -> `era`; missing/unsupported values
  return HTTP 400. Validation does not make hardcoded reference lists edition-specific.
- The raid-catalog endpoint returns a combined object grouped by edition.
- Composition summaries validate filter syntax but do not discard uninspected
  kills. Consumers must inspect rosters before applying composition filters.
- Ranking queries target only the selected encounter. Missing encounter mappings
  use a cached names/IDs lookup rather than querying every boss. Composition searches
  accept `zoneId` plus the boss slug when no hardcoded `encounterId` is available.
- Percentile enrichment batches up to ten report codes and restricts data to
  requested fights. Character lookups and phase-only metadata use batches of up
  to twenty; stable ordering avoids unnecessary cache misses.
- The info-only ranking endpoint skips per-report enrichment.
- Player timeline requests require an edition, fight ID, and player name or source
  ID. They resolve the exact actor and read at most three 10,000-event pages.
  Truncation is explicitly marked incomplete.
- Actor-name expressions and `useActorIDs: true` keep event selection aligned with
  report master data. Report-local IDs are not expression-language `source.id` values.
- Buff intervals are clipped at fight end only for complete streams. Unknown
  prepull application times are not invented.

Each API feature includes request examples and validation cases:
[WorldData](Lorrgs.Api/Features/WorldData/WorldData.http),
[Rankings](Lorrgs.Api/Features/Rankings/Rankings.http),
[CompRankings](Lorrgs.Api/Features/CompRankings/CompRankings.http),
[ReportTimeline](Lorrgs.Api/Features/ReportTimeline/ReportTimeline.http),
[WarcraftLogs](Lorrgs.Api/Features/WarcraftLogs/WarcraftLogs.http), and
[RaidCatalog](Lorrgs.Api/Features/RaidCatalog/RaidCatalog.http).

### Edition coverage and corrections

Current UI editions are Anniversary (Burning Crusade Classic), Mists of Pandaria,
Era (Classic), and Retail (Midnight). Catalog/spec mappings remain a work in progress;
availability depends on Warcraft Logs and reference data.

**Encounter mappings**

- Anniversary Zul'Aman uses WCL Classic Fresh zone `1062`, not `1059`, and
  encounter IDs `101189`-`101194`. The local final boss is Zul'jin (`boss=zuljin`),
  despite WCL calling encounter `101194` Daakara.
- Retail includes the Midnight Season 2 groups The Venomous Abyss, The Tidebound
  Grotto, and The Unbinding of Kith'ix, sharing WCL zone `53`. Unknown season
  start dates remain null.

**Retail specialization handling**

Ranking rows supply optional `specId` metadata for these timeline corrections:

| Specialization | Treatment |
|---|---|
| Marksmanship | Removes Explosive Shot recasts within 10 seconds |
| Restoration Druid | Removes Tree of Life recasts within 120 seconds |
| Restoration Shaman | Labels measured Ascendance intervals under 13 seconds as Deeply Rooted Elements, excluding fight-end-clipped intervals |
| Preservation | Measures Stasis store-to-release durations and excludes Dream Breath replays within one second of release |
| Devourer | Uses explicitly inferred Void Metamorphosis end markers, not synthetic casts |

A missing Stasis store may produce an inferred interval only in a complete stream.
Inferred events are labeled and excluded from note exports. Unknown specs and
non-Retail editions retain generic normalization. Corrections apply to cached and
fresh timelines without changing raw cache keys or TTL; rotation analysis remains raw.

**Nek'zali phases**

With a complete boss stream, the first Ritual of Awakening starts intermission 1.5;
the last Soul Transfer starts intermission 1.75 fifteen seconds before completion;
and the last raw WCL phase 3 transition becomes displayed phase 2.

Other encounters and incomplete streams retain observed transitions. Corrections
are recomputed from cached raw data and update matching player markers. Notes omit
intermissions and retain original integer WCL phase anchors.

Further upstream parity, curated edition-aware spell/cooldown metadata, and
duration/cooldown overlays remain ongoing. This application's explicit GraphQL
cache is not the upstream site's persisted-ranking freshness policy.

### Boss artwork and accessibility

Selectors, headings, and boss timeline rows share image resolution: world-data
icons take priority over verified Encounter Journal portraits. Lookup makes no
extra WCL requests and does not use RPGLogs-hosted images.

Boss artwork fits visible portrait bounds, excluding transparent padding and faint
shadows. Local SVG/CSS creates a portrait-colored frame, outline, and radial
backdrop. Patterns match for the same boss within a page and vary on reload;
effects require no image-analysis service or animation.

Selectors support keyboard navigation, type-to-jump, Escape cancellation, and
outside-click dismissal. Notices use accessible severity announcements, while
spell inspection supports keyboard focus as well as hover.

### Tests and coverage

Run from the repository root:

```powershell
npm --prefix .\lorrgs-web run test
npm --prefix .\lorrgs-web run lint
npm --prefix .\lorrgs-web run build
dotnet test .\Lorrgs.slnx
```

**Frontend:** every Svelte component has a sibling functional test file and must
meet **80% lines, statements, and branches individually**. Tests cover interactions,
reactive state, API failures, and edge cases. Static console output avoids terminal
redraw gaps.

All coverage reports, including focused-run output, belong under
`lorrgs-web/coverage/`. Open the [HTML report](lorrgs-web/coverage/index.html)
after running tests.

**Backend:** xUnit tests use queued HTTP responses and isolated temporary caches,
not live WCL calls. The client suite covers parsing, OAuth, editions, rate limits,
deduplication, and cache persistence. API tests cover rankings, catalogs,
enrichment, timelines, rotation analysis, and endpoint errors.
Expiry tests adjust temporary timestamps rather than waiting or modifying app caches.

To reproduce CI coverage enforcement, run these Coverlet commands **sequentially**:

```powershell
$coverage = Join-Path (Get-Location) "TestResults"
dotnet test Lorrgs.WarcraftLogs.Tests\Lorrgs.WarcraftLogs.Tests.csproj --configuration Release -p:CollectCoverage=true "-p:CoverletOutput=$coverage\warcraftlogs"
dotnet test Lorrgs.Api.Tests\Lorrgs.Api.Tests.csproj --configuration Release -p:CollectCoverage=true "-p:MergeWith=$coverage\warcraftlogs.json" "-p:CoverletOutput=$coverage\coverage" -p:Threshold=80
```

The second command merges client and API test coverage before enforcing **80%
line and branch coverage per production assembly** (`Lorrgs.Api` and
`Lorrgs.WarcraftLogs`). Both use `ThresholdType=line,branch` and
`ThresholdStat=minimum`; no production code is excluded.

JSON and Cobertura XML reports go to `TestResults`. CI uploads them as
`dotnet-coverage`, including on failure when available; enforcement does not depend
on artifact upload.

### Project structure

| Path | Purpose |
|---|---|
| `Lorrgs.Api/` | Minimal API features, HTTP contracts, and static World Data (`net10.0`) |
| `Lorrgs.WarcraftLogs/` | WCL client, GraphQL documents, data models, loading, caching, and enrichment; no API-project dependency |
| `lorrgs-web/` | Svelte 5 + Vite frontend |
| `lorrgs-web/src/pages/` | Application screens with sibling functional tests; navigation is composed in `App.svelte` |
| `lorrgs-web/src/lib/` | Shared frontend components, models, API clients, and utilities |
| `Lorrgs.Api.Tests/`, `Lorrgs.WarcraftLogs.Tests/` | Backend tests |
| `docker-compose.yml` | API and web containers |

### License

This project is licensed under the [MIT License](LICENSE).
