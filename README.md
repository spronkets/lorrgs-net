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
`Data/cache/wcl-graphql` directory for 10 days by default
(`WarcraftLogs:GraphQlCacheTtl = "10.00:00:00"`).
This setting uses .NET `TimeSpan` format (`d.hh:mm:ss`) with a one-minute minimum.
Use `WarcraftLogs__GraphQlCacheTtl` for environment overrides, replacing the former
`WarcraftLogs:GraphQlCacheTtlSeconds` setting.

Cache data only when it avoids external API calls or substantial computation.
World-data reference lists and the locally configured raid catalog are returned
directly from server definitions, without additional memory or disk caching.
Their small lookups, filtering, and sorting do not justify a separate cache.
The former `WorldData:CacheTtl`, `WorldData:CleanupIntervalMinutes`, and legacy
`Cache:DefaultTtl`/`Cache:DefaultTtlSeconds` settings are no longer used.
Old world-data cache files are ignored and may be deleted; no new ones are written.

All `/api/worlddata` requests require an `edition` query parameter. The frontend
passes the selected edition on initial loading and reloads world data when the
selection changes. Edition aliases are normalized:
`midnight` maps to `retail`, `mop` to `mistsofpandaria`, and `classic` to `era`.
Missing or unsupported editions return HTTP 400. Edition validation does not add
edition-specific source data or filtering to the existing hardcoded reference lists.
The raid-catalog endpoint returns one combined object grouped by edition.

Identical GraphQL queries and variables reuse the GraphQL files, including after API restarts.
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
10-day file cache for successful Warcraft Logs data.

Private and deleted upstream reports are distinguished from other GraphQL failures.
Ranking metadata batches omit only inaccessible reports and retain healthy partial
results. When an access error lacks usable paths or partial data, only the failed
batch is retried per report to identify unavailable entries. Partial-error GraphQL
responses are never cached as successes. Cached data is not revalidated with extra
permission requests. Timeline endpoints return explanatory 403/404 Problem Details
for upstream private/missing reports; these statuses describe Warcraft Logs
availability, not new authentication requirements. This API remains public, with
its existing unrestricted CORS policy.

### Ranking analytics

The home page selects an edition and raid/boss, then routes to the separate
Spec Rankings or Comp Rankings flow. Both flows also remain accessible from the
application bar. All three pages use the same encounter-filter component.
The selected encounter is carried into either flow; spec rankings offer
specialization choices and comp rankings offer composition and kill-time filters.
Composition search uses compact icon-based role and specialization count controls
with comparison operators and full accessible labels. Blank counts are unrestricted;
zero explicitly excludes an entry. The responsive grid stacks on narrow phones,
and filter expressions remain shareable in the URL.
Search and filter inputs share the encounter selectors' styling across ranking
filters, spell search, timeline zoom, analyzer inputs, and note options.

**Comp Rankings** searches Warcraft Logs encounter `fightRankings(metric: execution)`
for the selected edition, raid, boss, and difficulty,
only after clicking Search when entering from the launcher or navigation. Shared
search URLs containing `compPage` restore results automatically.
Inspected report fights include Warcraft Logs raid-level `speed` and `execution`
rankings: `rank`, `best`, `totalParses`, and `rankPercent`. Unavailable rankings
remain null; these are not calculated from individual player parses.
Composition result rows sort by ascending execution rank as details load; kills
without a rank remain last in their original order.
Rows show guild, server, and region when supplied by Warcraft Logs, with speed
and execution displayed as rank (Top percentage), with total ranked kills in
tooltips. Raid sizes are
catalog metadata shown in raid selection, not per-kill statistics; Anniversary
raids have their fixed 10- or 25-player sizes configured.
It returns the complete
summary page, with kills ordered by deaths, damage taken, then duration. All repeated `role` and `spec`
filters must match; comparisons are `eq`, `lt`, `gt`, `lte`, or `gte`, including
zero counts for absent roles/specs. Kill-time bounds use Maska-masked `m:ss` duration fields
(seconds in the API); existing links using seconds remain supported. Searches, filters, and page selection have
shareable URLs with browser back/forward restoration.

The API fetches **one full upstream page of up to 50 kill summaries** with one
GraphQL query. There is no results limit; all summaries and upstream pagination
are preserved. The frontend inspects only the **first three kills initially**,
requesting their actual Summary-table rosters and composition separately, together
with report-wide fight metadata and cooldown events (including other bosses and wipes).
Every other kill has its own **Load kill details** action; clicking it requests
that kill's composition and loads its report-wide timeline data. There is no bulk
"load next three" action for kill details.

Composition filters are evaluated only for inspected rosters. Uninspected kills
remain visible as **Composition pending**, never falsely excluded as non-matches.
The match/pending counts make a partially inspected search explicit. The initial
three are not backfilled to find three matches. Duration bounds can be applied
immediately to summaries. The summary API validates filter syntax but intentionally
does not discard summaries using filters; API consumers must inspect details before
applying composition filters. A page with no inspected matches is not exhaustive:
inspect remaining rows or use Next page when available.

Private/deleted/archived reports remain as summary rows with explanatory per-row
errors and retry actions. Any detail failure (including HTTP 429) pauses automatic
loading immediately; it never silently requests replacement kills. Cancellation,
edition changes and navigation abort outstanding work. Refresh bypasses WCL
GraphQL file-cache reads for the summary page and individually requested details.
No separate composition, roster, catalog, or world-data cache is introduced.

Expand a result's **Roster & cooldown timelines** to use the same aligned
player/boss timeline workspace as Spec Rankings: observed phases, spell controls,
zoom, references, analyzer links, loading/retry controls, and note export.
The composition workspace starts on **Everything**, matching Spec Rankings;
the cross-class **Raid cooldowns** preset is available as an optional filter.
Roster timelines are grouped into Tanks, Healers, and DPS, sorted by parse
percentile within each group rather than numbered as arbitrary ranks. Percentiles
come from Warcraft Logs report rankings (HPS for healers, DPS for tanks and damage
dealers); missing parses are shown as unavailable, not zero.
Each role has its own timeline section and time axis, with shared workspace controls.
Player regions come from the roster or ranking metadata. Guild names use batched,
cached Warcraft Logs character lookups and are displayed only when membership is
unambiguous; a report's guild is not assumed to be every player's guild.
Everything/Healing/Tank/Consumables remain available. Loading kill details retrieves
all pages of casts, buffs and deaths across the report once through the existing
GraphQL file cache. Each roster player's selected-fight timeline and the boss
reference are derived from that shared stream, so expanding the result or switching
roster roles does not require separate player loads. Other fights from the same
report reuse the cached metadata and event pages. This is not a download of raw
damage/healing events, which the cooldown workspace does not use.
Spec Rankings also checks this report-wide cache before requesting player events:
complete cached coverage supplies player timelines, boss references, and fight
phases without additional Warcraft Logs calls. Missing or expired event pages
fall back to the existing player-specific loader rather than downloading the
entire report automatically. Cache-only enrichment never makes upstream requests
and never treats partial report coverage as a complete timeline.
After verifying that the complete report-wide metadata and event pages were
persisted successfully, composition loading deletes covered player-specific event
cache pages for that report. Other reports and endpoint/edition caches are untouched;
failed or incomplete report downloads retain the individual caches.
Unknown edition-specific specs remain in rosters
with their upstream spec name and role instead of being silently dropped.
The existing hardcoded raid/spec catalog's mapping limitations still apply.
See `Lorrgs.Api/CompRankings.http` for API examples and validation failures.

Anniversary is Warcraft Logs Classic Fresh. Its Zul'Aman catalog uses WCL zone
`1062` and the original final boss, Zul'jin, not Daakara. Zone `1059` must not be
used for Anniversary Zul'Aman rankings. The final-boss URL uses `boss=zuljin`.
All six bosses have explicit Classic Fresh encounter mappings (`101189` through
`101194`). The Classic Fresh API calls encounter `101194` "Daakara"; the local
catalog displays Zul'jin and passes the verified encounter ID rather than relying
on that upstream name.

Spec rankings now open a compact **aligned timeline workspace** by default.
Spec Rankings uses the timeline directly, without a timeline/table view toggle.
The first timeline row is the selected encounter's reference: real enemy casts
and observed phase transitions from a report, not an estimated boss schedule.
By default it uses the first filtered/sorted report. The target button beside each
player's Analyze action selects that fight as the reference and highlights the
active target. The selected target is disabled with a "Current boss reference"
tooltip; no separate reference-player label is shown. The selection survives
sorting; if filtered out, the first visible report is used.
Reference changes cancel in-flight requests for the old one.
Bundled boss timelines require no extra request; other references use the existing
API file-cache loading path.
The row shares the player time axis and zoom, and respects the cast/icon toggles.
The boss portrait appears beside the encounter name in the heading and reference
row. Each player row also shows green phase lines and labels at that report's own
observed transitions, including before event loading. Phase metadata is fetched
in batches of up to 20 reports and file-cached for 10 days; this does not fetch
boss event streams for every ranking or copy the reference fight's timings.
Phase metadata is also included in existing percentile-enrichment queries and
reused from cached boss metadata. Separate phase batches are skipped when their
fights already have metadata; an empty observed phase list is treated as known data, not a
reason to request it again. Phase lines remain behind casts, buffs and row controls.
Missing phase data is not guessed. The API bundles cached boss timelines with
rankings; otherwise only the current reference is fetched using the same 10-day
file cache. Boss and player event queries have independent cache keys.

Nek'zali phase labels are corrected once a complete boss event stream is available:
the first Ritual of Awakening starts intermission 1.5, the last Soul Transfer starts
intermission 1.75 fifteen seconds before its logged completion, and the last raw WCL
phase 3 transition starts displayed phase 2. Other encounters and incomplete boss
streams retain their observed transitions. These corrections are recomputed from
cached raw data; they do not require a separate Warcraft Logs request. Corrected
references update the matching player's phase markers when they finish loading.
Raid-note timers exclude intermission markers and retain the original integer WCL
phase anchor (for example, displayed phase 2 uses WCL phase 3).

Retail player timelines accept an optional `specId` query parameter; ranking rows
provide it automatically. Known specs remove Marksmanship Explosive Shot recasts
within ten seconds and Restoration Druid Tree of Life recasts within 120 seconds.
Restoration Shaman's measured Ascendance intervals under thirteen seconds are labeled
Deeply Rooted Elements, without reclassifying fight-end-clipped intervals.
Preservation Stasis stores use measured store-to-release durations and exclude
replayed Dream Breath casts inside one second of release. A missing Stasis store in
a complete stream is shown as an explicitly inferred interval, not a player cast.
Devourer Void Metamorphosis ends have explicitly inferred markers, not synthetic
casts. Inferred events are never included in raid-note cast exports. Unknown specs
and non-Retail editions keep the generic event normalization. These rules apply to
both bundled and individually requested timelines without changing raw file-cache
keys or the 10-day lifetime; the separate rotation analyzer retains raw cast metrics.

Backend regression tests use xUnit and the standard .NET test SDK.
`Lorrgs.WarcraftLogs.Tests` covers response parsing, edition configuration,
OAuth, HTTP retry/rate-limit behavior, concurrent request deduplication, and
10-day file caching across client instances. `Lorrgs.Api.Tests` covers rotation
pagination/scoring, cache expiry/invalidation/cleanup, raid catalog mapping,
ranking validation/fallbacks, percentile/faction enrichment, world-data endpoint
filtering and cache parity, timeline pagination/actor selection/buff
normalization, spec corrections, batch isolation, fresh/cache correction parity,
and endpoint error mappings. Both reference their production
projects and are included in the solution. Run all backend tests with
`dotnet test Lorrgs.slnx`, or select a project with
`dotnet test Lorrgs.WarcraftLogs.Tests\Lorrgs.WarcraftLogs.Tests.csproj`.
Tests use queued HTTP responses and isolated temporary caches, not live Warcraft
Logs requests. Cache expiry tests adjust temporary file timestamps or stored write times rather than waiting or
modifying application caches.

Collect and enforce coverage using Coverlet's native MSBuild integration:

```powershell
$coverage = Join-Path (Get-Location) "TestResults"
dotnet test Lorrgs.WarcraftLogs.Tests\Lorrgs.WarcraftLogs.Tests.csproj --configuration Release -p:CollectCoverage=true "-p:CoverletOutput=$coverage\warcraftlogs"
dotnet test Lorrgs.Api.Tests\Lorrgs.Api.Tests.csproj --configuration Release -p:CollectCoverage=true "-p:MergeWith=$coverage\warcraftlogs.json" "-p:CoverletOutput=$coverage\coverage" -p:Threshold=80
```

The first command writes Coverlet JSON and Cobertura XML reports under
`TestResults`. The second merges the library's JSON results with the API tests'
coverage before checking thresholds, so library code exercised by either test
project counts toward the same assembly.

CI requires at least **80% line coverage and 80% branch coverage separately for
each production assembly** (`Lorrgs.Api` and `Lorrgs.WarcraftLogs`). Both projects
configure `ThresholdType=line,branch` and `ThresholdStat=minimum`. Coverlet
natively merges coverage and fails `dotnet test` if either metric in either
assembly is below the threshold. Run these commands sequentially, not in
parallel. No production code is excluded and no custom coverage parser is used.

CI also uploads the reports as the `dotnet-coverage` artifact, including on test
failure when reports are available. Uploading is optional; the coverage check
reads the local reports and does not depend on the artifact step. Coverage
complements behavior assertions; it does not prove every regression is prevented.

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
Search, individual toggles, hover/focus details, sorting and report counts
remain available. Sample notes and timeline guidance are expandable rather than
occupying the main workspace.
The compact application bar keeps Spec Rankings and Comp Rankings visible, with
World Data and Rotation Analyzer under Tools and a separate theme button.
Branding attribution appears in the footer. The comparison heading retains edition,
raid, specialization, boss artwork, and Change selection without an enclosing card.
Region and kill-time filters stay visible; date range, sorting, and Reset filters
live under Filters, whose active count includes all non-default filter and sort values.
Class-colored comparison and World Data labels use theme-aware text tones with a
light-mode lightness ceiling for readability. Icons, badges, and class accents
retain their original colors; the comparison's dark-mode text treatment is unchanged.
Report-count, best DPS, and median kill-time summaries are omitted. The timeline
shows all reports returned for the requested Warcraft Logs ranking page, without
client-side pagination or a Load more button. Filters and sorting apply to the
entire returned response.
Warcraft Logs exposes a `page` argument but no supported page-size argument;
`size` means raid size, not result count. The upstream lorrgs.io site's 50-report
limit is applied by its own loader after fetching rankings, not sent to Warcraft
Logs. No custom API batching or cursor layer is used here. Successful Warcraft
Logs responses remain in the existing 10-day file cache.
Ranking queries target only the selected encounter, not every boss in its raid.
When an encounter ID is unavailable, a cached names/IDs-only lookup resolves it
before requesting rankings. The comparison requests only the ranked player
(`includeOtherPlayers=false`); public API callers can still request raid rosters.
Percentile enrichment batches up to ten unique report codes, combines players
from the same report, and restricts rankings and fight metadata to the requested
fight IDs. Phase metadata returned by that query is reused; phase-only batches
include only unresolved reports and fights. Character lookups remain deduplicated
and batched in groups of twenty. Report and character batches use stable ordering
so ranking-order changes alone do not invalidate their file-cache entries.
The info-only ranking endpoint skips all per-report enrichment.
Rate-limit responses use a friendly message in rankings and timelines instead of
raw HTTP errors, without automatically retrying or claiming a specific reset time.
Inline errors, warnings, and informational notices share the theme-aware Alert
component, with severity icons, accessible announcements, and compact timeline
variants. Notices show their message directly without an "Error:" prefix.
Only visible reports enter the timeline loader; its existing three-at-a-time
loading controls remain available.
On phones, navigation uses two predictable rows. The comparison fills the width with
spec and boss side by side and Change selection at the top right. Region and kill-time
controls use full-width groups with larger touch targets. Player and boss details sit
above their tracks instead of consuming the chart width. At 100% zoom the full fight
fits the available width, with time labels spaced for the screen and a small end gutter
for note actions. Higher zoom levels expand the aligned tracks and scroll horizontally
together, with details pinned to the visible left edge. Timeline rows expand with
the page on all devices rather than using nested vertical scrolling. Spell lists
also expand on small screens and touch-first devices. Scrollbar appearance remains
browser-native; zoomed timelines retain horizontal scrolling.
Use spell selection/search, cast/buff/death toggles, separate spell lanes, and
percentage zoom controls to compare usage. Highlighting a spell by hover or keyboard focus shows measured
seconds between consecutive visible casts per player within the existing row height.
Hovering an event opens a section of the same timeline at the next doubling of
the selected zoom, without resizing the rows or making additional requests.
Zoom options and the magnifier limit share the `TIMELINE_ZOOM_LEVELS` definition.
At the highest available zoom or with Separate spells enabled, the hover magnifier is disabled; spell highlighting
and measured cast intervals remain available.
These are cast-to-cast intervals, not cooldown estimates. Copy note opens a live
preview with MRT (Method Raid Tools / Smart CD Reminder) and NSRT (Northern Sky
Raid Tools) exports, optional player name/nickname or specialization targeting,
and phase-relative timers when the selected player's fight has observed phases.
Notes contain only selected cast events, with spell IDs and tenth-second timing;
NSRT includes encounter ID, difficulty, and boss name. Missing encounter/spec
metadata is reported rather than exporting an invalid note. Partial timelines are
explicitly marked in the editor. Click the preview to select it for manual copying,
or use Copy to clipboard; clipboard errors remain visible in the editor.
No additional Warcraft Logs requests are made for note generation.
Cooldown recovery windows and cast-time overlays are not
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
Clicking any active preset clears all spells, including newly loaded spells;
clicking it again reapplies that preset.
Clear spells also clears the selection and keeps newly loaded spells hidden.
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
and the same 10-day disk cache as other successful GraphQL responses.
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
performance, kill-time, and date sorting. Kill-time fields use `m:ss` durations;
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
not a replacement for this application's explicit 10-day GraphQL file cache.

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

The boss selector displays portraits in both the selected value and the open list,
with keyboard navigation, type-to-jump, Escape cancellation and outside-click dismissal.
The selector, comparison heading and boss timeline share the same resolved image:
the world-data icon takes priority, followed by verified Encounter Journal portraits
from `bossPortraits.ts`. Ranking responses do not override this mapping, so images
stay consistent before and after rankings load, including with older cached responses.
If none exists, no image is
shown. Raid selectors likewise omit images when metadata is
missing, without reserving icon space. Portrait lookup does not query Warcraft Logs.
Ranking boss images also use Wowhead and are omitted when metadata is missing.
The app does not use RPGLogs-hosted images.
Boss artwork is rendered by the shared `BossIcon` component: a square bordered
frame with a horizontally centered, bottom-aligned portrait fitted to its visible bounds
and enlarged by 20% for a fuller composition, reused in selectors, headings and ranking
rows. Missing or failed images are omitted rather than replaced with placeholders.
Callers supply a boss `slug` and optionally an `iconName` from world metadata;
the component owns URL resolution and falls back to the slug's journal portrait.
Transparent image padding is excluded when fitting and aligning visible artwork.
Zoom is anchored to the visible artwork's bottom center, rather than transparent image padding.
The generated backdrop uses saturated portrait colors, broad dark radial bands,
uneven light-and-dark spiral bands reaching beyond the frame, and nine broad,
overlapping light fans without thin spoke highlights.
Light displacement breaks up the geometry without muddying the small icon with
grain. A dark edge vignette frames the composition; a solid white silhouette edge
uses a narrow antialiased alpha expansion to avoid whole-pixel dilation jumps.
A narrow, dark portrait-tinted backing outside the white edge provides local
contrast against bright fans without darkening the entire background.
Faint baked-in portrait shadows are removed with an alpha cutoff before outlining.
Portrait fitting uses the same shadow-removal cutoff, so invisible shadow padding
does not shrink the visible boss artwork.
Fan widths, lengths, and brightness use a fresh random seed per page load combined
with the boss slug. Matching bosses share a pattern within a page, but refreshing
generates a new one. All effects are local SVG/CSS with no extra image requests or animation.
The light fans, bands, and background glow originate at the alpha-weighted center
of the fitted visible portrait, not the square frame's center.
Fan reach is calculated from that origin to the frame edge, with varied overshoot
and angular widths so the light extends to the border.
The frame and background use the portrait's detected dominant color, excluding
transparent, near-black and near-white pixels. Detection samples the loaded image
in a small canvas; no additional API request or image-analysis service is used.

Cached player timelines with unavailable actor metadata are omitted from initial
rankings rather than failing the ranking request. Timeline loading first reuses
report-level master data when timeline metadata lacks actors or abilities. This
recovery includes spell names and icons, not just actor identities.
Cache-only loading never fetches missing master data; explicit loading can fetch it
through the same 10-day API file cache, shared across fights in a report.
Timelines missing spell metadata are not bundled as complete timelines.
If both sources lack actors, explicit
timeline requests report unavailable player metadata as not found. Character enrichment uses the
Warcraft Logs `guilds` collection; a guild name is only inferred when exactly one
guild is returned.

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
