# LorrgsNET (.NET + Svelte)

This repository is a .NET + Svelte implementation inspired by the original Python project:

- https://github.com/gitarrg/lorrgs
- https://github.com/gitarrg/lorrgs/blob/main/README.md

Like the original, the goal is to analyze and compare cooldown usage from top Warcraft Logs by spec and comp.

This repository is not the official hosted service at https://lorrgs.io/. It is a separate local/self-hosted .NET + Svelte implementatio for my own usage right now.

- No built-in user system (no signup/login/account management).
- No relational database.
- Data is fetched from Warcraft Logs/reference sources and cached (memory/files) for performance/token usage.

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
