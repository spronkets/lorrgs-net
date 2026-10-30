# LorrgsNET (.NET + Svelte)

LorrgsNET is a local/self-hosted tool for comparing World of Warcraft cooldown
usage from Warcraft Logs. Explore specialization rankings, raid compositions,
and individual reports using aligned encounter timelines.

The application uses an ASP.NET Core API and a Svelte frontend. It has no built-in
user accounts or relational database; successful Warcraft Logs responses are
cached to reduce external requests.

## Features

- **Spec Rankings:** compare ranked players by encounter, with region, kill-time,
  and date filters.
- **Comp Rankings:** find execution-ranked kills by raid composition and inspect
  their rosters and cooldown usage.
- **Load Report:** open Warcraft Logs report codes or URLs to inspect kills and
  wipes without searching rankings.
- **Shared timelines:** compare player abilities, received effects, boss casts,
  and observed phases; filter spells, zoom, and export MRT or NSRT raid notes.
- **Tools:** browse World Data or inspect casts with the Rotation Analyzer.
- Share encounter and report selections through URLs.

> **Use timings as a reference.** Another player's cast sequence is not a universal
> cooldown plan. Adjust it for your raid's composition, strategy, and phase timings.

## Getting started

### Prerequisites

- **Local development:** .NET SDK 10, npm, and a Node.js version supported by Vite
  (20.19+ or 22.12+).
- **Containers:** Docker Desktop, or Docker Engine with Compose.
- **Either option:** Warcraft Logs OAuth client credentials.

### Download

```powershell
git clone https://github.com/spronkets/lorrgs-net.git
Set-Location .\lorrgs-net
```

Alternatively, download and extract the ZIP from
[GitHub](https://github.com/spronkets/lorrgs-net).

### Configure Warcraft Logs

1. Sign in to [Warcraft Logs](https://www.warcraftlogs.com/).
2. Open [API clients](https://www.warcraftlogs.com/api/clients/) and create an application.
   A localhost URL is sufficient for local development.
3. Configure the client ID and secret using one of the options below.

Never commit real credentials.

**Local API:** create `Lorrgs.Api/appsettings.local.json`:

```json
{
  "WarcraftLogs": {
    "ClientId": "your_client_id",
    "ClientSecret": "your_client_secret"
  }
}
```

**Docker:** copy `.env.example` to `.env` and set:

```env
WARCRAFTLOGS_CLIENT_ID=your_client_id
WARCRAFTLOGS_CLIENT_SECRET=your_client_secret
```

### Run locally

Install dependencies from the repository root:

```powershell
dotnet restore .\Lorrgs.slnx
npm --prefix .\lorrgs-web ci
```

Start the API and frontend in separate terminals:

```powershell
dotnet run --project .\Lorrgs.Api\Lorrgs.Api.csproj
```

```powershell
npm --prefix .\lorrgs-web run dev
```

In VS Code, **Tasks: Run Task > Dev: run full stack** starts both services after
dependencies are installed.

### Run with Docker

```powershell
docker compose up --build
```

To stop the containers:

```powershell
docker compose down
```

### Open the application

| Service               | Default URL                   |
| --------------------- | ----------------------------- |
| Web application       | http://localhost:5173         |
| API                   | http://localhost:5247         |
| Swagger (Development) | http://localhost:5247/swagger |

These ports are the same for local and Compose setups.

## Usage and limitations

Choose an edition, raid, and boss, then open **Spec Rankings** or **Comp Rankings**.
Use **Load Report** for your own reports; multiple codes or URLs can be separated
by spaces, commas, or newlines. World Data and Rotation Analyzer are under **Tools**.

- Spec Rankings initially loads three accessible player timelines. Load other
  rows individually as needed.
- Comp Rankings initially inspects three kills. Inspect additional rows to
  evaluate their compositions; uninspected rows are not confirmed matches.
- Blank composition counts are unrestricted; zero excludes that role or spec.
- Timelines show observed events rather than estimated cooldown schedules.
  Partial data and unavailable metadata are identified in the UI.
- Private, deleted, or archived reports may be unavailable. Archived events
  require Warcraft Logs subscriber user authorization, which this app's
  client-credentials integration does not provide.
- Warcraft Logs quotas apply. Rate limits pause upstream loading; failed rows
  can be retried while successful results remain available.
- Edition, encounter, and specialization coverage depends on Warcraft Logs and
  the project's reference data and remains a work in progress.

### Hosting and caching

The API has unrestricted CORS and no built-in authentication. Restrict access
appropriately if hosting it beyond your local machine.

Successful Warcraft Logs GraphQL responses are cached on disk in
`Lorrgs.Api/Data/cache/wcl-graphql` for **10 days** by default and reused across
API restarts. OAuth tokens remain memory-only.

Override the cache duration with `WarcraftLogs:GraphQlCacheTtl` (environment:
`WarcraftLogs__GraphQlCacheTtl`) in .NET `TimeSpan` format, such as
`"10.00:00:00"`. The minimum is one minute.

## Development

Run from the repository root:

```powershell
npm --prefix .\lorrgs-web run test
npm --prefix .\lorrgs-web run lint
npm --prefix .\lorrgs-web run build
dotnet test .\Lorrgs.slnx
```

Frontend tests enforce **80% lines, statements, and branches per Svelte component**.
Coverage reports are written to `lorrgs-web/coverage/`; open
the [HTML report](lorrgs-web/coverage/index.html) after running tests.
Backend tests use mocked responses and isolated caches, not live Warcraft Logs calls.

To reproduce backend CI coverage enforcement, run these commands sequentially:

```powershell
$coverage = Join-Path (Get-Location) "TestResults"
dotnet test Lorrgs.WarcraftLogs.Tests\Lorrgs.WarcraftLogs.Tests.csproj --configuration Release -p:CollectCoverage=true "-p:CoverletOutput=$coverage\warcraftlogs"
dotnet test Lorrgs.Api.Tests\Lorrgs.Api.Tests.csproj --configuration Release -p:CollectCoverage=true "-p:MergeWith=$coverage\warcraftlogs.json" "-p:CoverletOutput=$coverage\coverage" -p:Threshold=80
```

The merged report enforces **80% line and branch coverage per production assembly**.
Reports are written to `TestResults`.

### Project structure

| Path | Purpose |
| ---- | ------- |
| `Lorrgs.Api/` | ASP.NET Core API features and reference data |
| `Lorrgs.WarcraftLogs/` | Warcraft Logs client, GraphQL queries, and caching |
| `lorrgs-web/` | Svelte 5 + Vite frontend |
| `Lorrgs.Api.Tests/`, `Lorrgs.WarcraftLogs.Tests/` | Backend tests |
| `docker-compose.yml` | API and web containers |

API request examples are alongside their features:
[WorldData](Lorrgs.Api/Features/WorldData/WorldData.http),
[Rankings](Lorrgs.Api/Features/Rankings/Rankings.http),
[CompRankings](Lorrgs.Api/Features/CompRankings/CompRankings.http),
[ReportTimeline](Lorrgs.Api/Features/ReportTimeline/ReportTimeline.http),
[WarcraftLogs](Lorrgs.Api/Features/WarcraftLogs/WarcraftLogs.http), and
[RaidCatalog](Lorrgs.Api/Features/RaidCatalog/RaidCatalog.http).

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

## License

This project is licensed under the [MIT License](LICENSE).
