# LorrgsNET (.NET + Svelte)

This repository is a .NET + Svelte implementation inspired by the original Python project:

- https://github.com/gitarrg/lorrgs
- https://github.com/gitarrg/lorrgs/blob/main/README.md

Like the original, the goal is to analyze and compare cooldown usage from top Warcraft Logs by spec and comp.

This repository is not the official hosted service at https://lorrgs.io/. It is a separate local/self-hosted .NET + Svelte implementatio for my own usage right now.

- No built-in user system (no signup/login/account management).
- No relational database.
- Data is fetched from Warcraft Logs/reference sources and cached (memory/files) for performance/token usage.

### Warcraft Logs response caching

Successful GraphQL responses are stored as JSON files in the API's
`Data/cache/wcl-graphql` directory for 24 hours by default
(`WarcraftLogs:GraphQlCacheTtlSeconds = 86400`), independently of `Cache:DefaultTtlSeconds`.
Identical queries and variables reuse these files, including after API restarts.
An explicit refresh skips reading the cached response but saves the new response
to disk and restarts its TTL. OAuth tokens remain in memory only.
When Warcraft Logs returns HTTP 429, the API returns the rate-limit error immediately
instead of retrying. Further requests to that upstream host are paused for its
`Retry-After` duration (one minute if not supplied); valid cached responses are
still served during the cooldown. Ranking enrichment stops on a rate limit rather
than attempting the remaining batches.
Faction enrichment selects the nested Warcraft Logs `GameFaction.id` for both
characters and guilds. A GraphQL rejection stops the remaining faction batches
and is logged; rankings still return with any metadata already available instead
of repeatedly submitting a rejected operation. GraphQL failures are not cached.

HTTP error responses carry `Cache-Control: no-store`, so browsers and proxies
do not retain failures such as rate-limit responses. This does not change the
24-hour file cache for successful Warcraft Logs data.

### Ranking analytics

Spec rankings now open a compact **aligned timeline workspace** by default.
Spec Rankings uses the timeline directly, without a timeline/table view toggle.

The timeline workspace composes focused Svelte 5 components: `SpellSelection`
owns spell presets and selection, `TimelineRow` renders each player's metadata
and events, `TimelineMagnifier` renders the hover preview, and `Toast` handles
clipboard feedback. Their tests live beside the components. The reactive
`timelineLoader.svelte.ts` module owns sequential loading, top-three
prioritization, retry and cancellation; `spellClassification.ts` owns preset
classification. Both modules have focused tests, while workspace integration
tests cover their shared selection, filtering, zoom and clipboard behavior.
The rotation analyzer remains a separate tool. Player rows share the same elapsed
time axis; they are not stretched to each player's kill time.

Timelines show real casts (with spell icons), measured buff intervals and deaths.
Spell selection distinguishes player-cast abilities from other effects received
by that player. DPS / power windows such as Bloodlust, Heroism, Power Infusion,
Avenging Wrath and observed haste effects have gold-accented buttons and stronger
uptime bars. External buffs remain visible because they affect performance; they
are not labeled as spells cast by the selected class. These highlights identify
observed windows, not a computed DPS gain. Only the player's casts and buffs
targeting that player feed the timeline; other raid members' casts are excluded.
Spell buttons, cast borders and uptime bars share stable per-spell colors within
gold (power), blue/teal (player abilities), and softer mixed (received effects)
palettes. Hover or keyboard-focus a selector or event to highlight the same spell
across player rows and dim others. The detail strip identifies the spell, player,
event time and measured uptime; outlines and text supplement color. Separate
spell lanes remain available for dense or overlapping timelines.
The compact toolbar uses labeled region buttons, paired kill-time/date inputs,
icon-style timeline toggles and icon-first spell groups. Group headings toggle
their spells together; **Spell names** expands the icons into labeled buttons.
Search, individual toggles, hover/focus details, sorting and summary statistics
remain available. Sample notes and timeline guidance are expandable rather than
occupying the main workspace.
Use spell selection/search, cast/buff/death toggles, separate spell lanes, and
percentage zoom controls to compare usage. Highlighting a spell by hover or keyboard focus shows measured
seconds between consecutive visible casts per player within the existing row height.
Hovering an event opens a section of the same timeline at the next doubling of
the selected zoom, without resizing the rows or making additional requests.
Zoom options and the magnifier limit share the `TIMELINE_ZOOM_LEVELS` definition.
At the highest available zoom, the hover magnifier is disabled; spell highlighting
and measured cast intervals remain available.
These are cast-to-cast intervals, not cooldown estimates. Copy note exports selected
cast timestamps. Cooldown recovery windows and cast-time overlays are not
inferred from cast counts or hardcoded modern spell values.
Clipboard feedback uses the reusable `Toast.svelte` component, which owns its
success/error announcements, close button, styling, and four-second dismissal
timer. A new copy result restarts the timer; unmounting clears it.

Spell presets provide Everything, Damage, Tank, Healing, and Consumables
selections. Role icons adapt to the selected class (for example Paladin:
Retribution, Protection, Holy). Unsupported roles are hidden. Classes with several
damage specs show separate spec buttons (for example Arcane, Fire, Frost) instead
of a single Damage button. Recognized spec abilities are selected alongside
shared class damage abilities and power buffs; unknown abilities remain available
under Everything. Rogue uses Combat in Classic editions and Outlaw in Retail.
Everything uses a spellbook; Consumables uses a potion icon and selects observed
potions, flasks, elixirs, healthstones, food buffs, and drums.
Presets use upstream spell tags/variations, recognized ability names
for older ranks, and observed power windows for Damage. Unknown effects remain
available under Everything rather than being guessed into a role. Individual
spell/group toggles remain available; choosing one switches to custom selection.
An active preset also applies to newly loaded timelines without extra requests.

The top three eligible reports for the current filter and sort load automatically,
sequentially. Changing filters or sort prioritizes the new top three, even during
loading; already loaded rows are reused. The timeline scroll resets to its top
when the first result changes so newly sorted results are visible. Load another
three or an individual row explicitly to inspect more. The shared time axis ends
at the longest fight in the filtered sample, using loaded timeline durations when
available, without rounding up to an extra minute. The chart stretches to fill the
available width at the default 100% zoom. Zoom options of 200%, 400%, and 800% expand
the time axis proportionally, enable horizontal scrolling, and preserve row heights.
Known fight-end markers, elapsed-time labels, and post-fight shading are shown for every
row before loading spell events, using fight metadata already in the rankings.
Unloaded rows show a vertically centered "Load report" button containing the report
name at time zero and a subtle duration band. The report date appears only in the
player column beside the analyzer link. These require no event requests.
Any loading error stops the queue,
shows the affected row's error and offers explicit retry. Leaving the workspace
cancels pending HTTP work. These requests use the selected edition's WCL host
and the same 24-hour disk cache as other successful GraphQL responses.
The initial spec rankings response includes a `timeline` on every report whose
complete metadata and event pages are available in the API's unexpired file cache.
These timelines render immediately, including rows outside the top three.
Cache-only reconstruction never contacts Warcraft Logs; missing, expired or
incomplete cached timelines are omitted. Only missing top-three timelines load
automatically afterward. There is no browser response cache or separate cache
probe request; the browser retains only the current workspace's rendered state.
The first uncached load still costs WCL requests; larger HTTP responses alone do
not reduce WCL's query work. Upstream instead preloads report events and saves a
complete ranking document, which its navigation endpoint serves directly.

`GET /api/warcraftlogs/reports/{code}/timeline` requires a fight ID and a player
name or source ID, plus an edition. It resolves the exact player, loads report
ability metadata, and reads at most three 10,000-event pages per player.
Event expressions match the resolved actor's name (as upstream does), while
`useActorIDs: true` keeps returned actor IDs aligned with report master data.
Report-local actor IDs are not used as expression-language `source.id` values.
Truncated timelines are explicitly marked incomplete. Buff application/removal
events supply observed uptime; active intervals are clipped at fight end only
when the event stream is complete. Unknown prepull application times are not
invented. Example requests are in [Timeline.http](Lorrgs.Api/Timeline.http).

Both views support region, kill-time, and date-range filters, plus
performance, kill-time, and date sorting. Kill times accept seconds or `m:ss`;
date ranges include the whole final day in the browser's local timezone.
Original ranks are preserved when filtering and sorting. Summary statistics
(best performance and median kill time) describe only the loaded, filtered
reports, not the entire upstream ranking population. Filtering/sorting itself is
local; newly promoted top-three timelines may require a cache-backed event load.

Report links use the selected edition's Warcraft Logs host and include the
fight and actor when available. Rotation analyzer links carry the player name
when ranking data has no actor ID; ambiguous names require manual selection
instead of automatically analyzing a different player.

### Upstream feature alignment

Reviewed upstream changes from `b0db657` through `aaa5d59` and the live
[Nek'zali rankings page](https://lorrgs.io/spec_ranking/paladin-retribution/nekzali-the-soulcoiler).
The comparison workspace restores the core shared-time-axis workflow as a
first step toward parity, rather than treating a generic rankings table or
rotation score as an equivalent replacement.

Retail now includes the three Midnight Season 2 raid groups (The Venomous Abyss,
The Tidebound Grotto, and The Unbinding of Kith'ix), all 10 encounters, and Season 2
membership. These groups share the real WCL zone ID `53`; for example, Nek'zali
uses encounter ID `3470` and Kith'ix uses `3513`. Aggregated local catalog caches
use a new namespace so existing cache files cannot hide these additions.
The upstream data does not supply a Season 2 start date; unknown dates are
represented as `null`, not an invented release date.

Further parity still requires curated edition-aware spell categories and
cooldown metadata, boss ability rows, encounter phases and duration/cooldown
overlays beyond the measured player buff intervals now implemented.
Spec-specific processing (Stasis, Ascendance procs, Explosive Shot recasts, and
Void Metamorphosis) must run before cast counts are computed; applying these
rules to the current raw summaries without reliable spec metadata would be
incorrect. Likewise, upstream's persisted-ranking freshness/dirty policy is
not a replacement for this application's explicit 24-hour GraphQL file cache.

## Project Structure

- `Lorrgs.Api/` - ASP.NET Core API (`net10.0`) that talks to Warcraft Logs and serves data.
- `lorrgs-web/` - Svelte + Vite frontend.
- `docker-compose.yml` - Runs both API and web together.

## Prerequisites

For local VS Code/dev runs:

- .NET SDK 10
- Node.js 20+ and npm

For Docker runs:

- Docker Desktop (or Docker Engine + Compose)

## Configure Warcraft Logs Credentials

The API needs Warcraft Logs OAuth client credentials.

### Where to get OAuth credentials

1. Sign in to Warcraft Logs: https://www.warcraftlogs.com/
2. Open API clients management: https://www.warcraftlogs.com/api/clients/
3. Create a client application (for local dev, using a localhost URL in the app/client registration works) and copy the generated `Client ID` and `Client Secret`.
4. Use those values in either local API config (`appsettings.local.json`) or Docker `.env`.

Note: Treat the client secret like a password. Do not commit real credentials to source control.

### Option A: Local API config (`appsettings.local.json`)

Create or update `Lorrgs.Api/appsettings.local.json`:

```json
{
  "WarcraftLogs": {
    "ClientId": "your_client_id",
    "ClientSecret": "your_client_secret"
  }
}
```

The API loads `appsettings.local.json` at startup (optional file, local override).

### Option B: Docker config (`.env`)

Copy `.env.example` to `.env` (if needed), then set:

```env
WARCRAFTLOGS_CLIENT_ID=your_client_id
WARCRAFTLOGS_CLIENT_SECRET=your_client_secret
# Optional overrides:
# WARCRAFTLOGS_BASE_URL=https://www.warcraftlogs.com/api/v2/client
# WARCRAFTLOGS_AUTH_URL=https://www.warcraftlogs.com/oauth/token
```

`docker-compose.yml` maps these into the API as:

- `WarcraftLogs__ClientId`
- `WarcraftLogs__ClientSecret`

## Edition Coverage

The project supports multiple World of Warcraft editions/eras in rankings and raid catalog flows although this is still a work-in-progress.

- Current UI version options: `Anniversary` (Burning Crusade Classic), `Mists of Pandaria`, `Era` (Classic), and `Retail` (Midnight).
- Additional edition/raid mappings are available in the API catalog logic.

Because upstream data sources evolve, exact availability depends on Warcraft Logs/reference data at runtime.

## Run in VS Code (Tasks)

From VS Code Command Palette -> `Tasks: Run Task`:

Web tasks:

1. `Web: install` (run once initially, and whenever `package-lock.json` changes)
2. `Web: dev server`
3. `Web: test`

API tasks:

1. `API: build` (or `API: restore` then `API: build`)
2. `API: run`

Or run `Dev: run full stack` after dependencies are installed.

### Default Local URLs

- Web app: http://localhost:5173
- API base: http://localhost:5247
- API Swagger (Development): http://localhost:5247/swagger

## Run with Docker

From repository root:

```bash
docker compose up --build
```

Then open:

- Web app: http://localhost:5173
- API (container port mapped): http://localhost:5247

To stop:

```bash
docker compose down
```

## License

This project is licensed under the MIT License. See `LICENSE`.
