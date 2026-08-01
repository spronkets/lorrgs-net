# Warcraft Logs API Reference

This document captures Warcraft Logs API details from official documentation and validated schema behavior.

Last updated: 2026-08-01

## Sources

- OAuth and API overview: https://www.warcraftlogs.com/api/docs
- GraphQL schema root: https://www.warcraftlogs.com/v2-api-docs/warcraft/
- Query root object: https://www.warcraftlogs.com/v2-api-docs/warcraft/query.doc.html

## Slug and ID Coverage Status

Short answer: partially documented in official API.

- Zone and encounter IDs are fully exposed and documented.
- Zone and encounter slugs are not exposed in Warcraft Logs GraphQL schema.

Validation query result (2026-08-01):

- `Cannot query field "slug" on type "Zone".`
- `Cannot query field "slug" on type "Encounter".`

Implication:

- For raids/bosses, the authoritative keys from Warcraft Logs are numeric IDs (`zone.id`, `encounter.id`) plus names.
- Any raid/boss slug used by consumers is an application-level mapping, not an official Warcraft Logs field.

## Full Raid and Encounter Catalog (Looked Up)

Authoritative snapshot files generated from Warcraft Logs GraphQL across configured editions:

- [Lorrgs.WarcraftLogs/Docs/warcraftlogs-zones-summary-2026-08-01.json](Lorrgs.WarcraftLogs/Docs/warcraftlogs-zones-summary-2026-08-01.json)
- [Lorrgs.WarcraftLogs/Docs/warcraftlogs-encounters-2026-08-01.json](Lorrgs.WarcraftLogs/Docs/warcraftlogs-encounters-2026-08-01.json)

Coverage summary:

- Total zones: 221
- Total encounters: 1425

Per-edition totals:

- anniversary: 38 zones, 244 encounters
- era: 29 zones, 163 encounters
- mistsofpandaria: 88 zones, 552 encounters
- retail: 66 zones, 466 encounters

Each catalog row contains exact IDs and names:

- zone summary rows: `edition`, `expansionId`, `expansionName`, `zoneId`, `zoneName`, `encounterCount`
- encounter detail rows: `edition`, `expansionId`, `expansionName`, `zoneId`, `zoneName`, `encounterId`, `encounterName`, `journalID`

## Transport and Authentication

### OAuth

- Authorize URL: `https://www.warcraftlogs.com/oauth/authorize`
- Token URL: `https://www.warcraftlogs.com/oauth/token`

Supported flows:

- Client Credentials: public API usage
- Authorization Code: user/private API usage
- PKCE: browser/native app usage without client secret

### GraphQL endpoints

- Public endpoint: `https://www.warcraftlogs.com/api/v2/client`
- Private endpoint: `https://www.warcraftlogs.com/api/v2/user`

All GraphQL calls are HTTP POST with:

- `Authorization: Bearer <access_token>`
- `Content-Type: application/json`

GraphQL body format:

```json
{
  "query": "query ...",
  "variables": {}
}
```

## Schema Root: Query Endpoints

From `Query`:

- `characterData: CharacterData`
- `gameData: GameData`
- `guildData: GuildData`
- `progressRaceData: ProgressRaceData`
- `rateLimitData: RateLimitData`
- `reportData: ReportData`
- `userData: UserData`
- `worldData: WorldData`
- `reportComponentData: ReportComponentData`
- `systemReportComponentData: ReportComponentData`

## Endpoint Contracts

### 1) CharacterData

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/characterdata.doc.html

- `character(id, name, serverSlug, serverRegion): Character`
- `characters(guildID, limit, page): CharacterPagination`

Request args:

- `id: Int` optional
- `name: String` optional, requires `serverSlug` + `serverRegion`
- `serverSlug: String` optional
- `serverRegion: String` optional
- `guildID: Int` required for `characters`
- `limit: Int` optional, default 100, min 1, max 100
- `page: Int` optional, default first page

### 2) GameData

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/gamedata.doc.html

- `abilities(limit, page): GameAbilityPagination`
- `ability(id): GameAbility`
- `achievements(limit, page): GameAchievementPagination`
- `achievement(id): GameAchievement`
- `affixes: [GameAffix]`
- `affix(id): GameAffix`
- `classes(faction_id, zone_id): [GameClass]`
- `class(id, faction_id, zone_id): GameClass`
- `enchants(limit, page): GameEnchantPagination`
- `enchant(id): GameEnchant`
- `factions: [GameFaction]`
- `items(limit, page): GameItemPagination`
- `item(id): GameItem`
- `item_sets(limit, page): GameItemSetPagination`
- `item_set(id): GameItemSet`
- `maps(limit, page): GameMapPagination`
- `map(id): GameMap`
- `npcs(limit, page): GameNPCPagination`
- `npc(id): GameNPC`
- `zones(limit, page): GameZonePagination`
- `zone(id): GameZone`

Pagination args are generally:

- `limit: Int` default 100, min 1, max 100
- `page: Int` default first page

### 3) GuildData

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/guilddata.doc.html

- `guild(id, name, serverSlug, serverRegion): Guild`
- `guilds(limit, page, serverID, serverSlug, serverRegion): GuildPagination`

Pagination args:

- `limit: Int` default 100, min 1, max 100
- `page: Int` default first page

### 4) ReportData

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/reportdata.doc.html

- `report(code, allowUnlisted): Report`
- `reports(endTime, guildID, guildName, guildServerSlug, guildServerRegion, guildTagID, userID, limit, page, startTime, zoneID, gameZoneID): ReportPagination`

Important args:

- `code: String` required for `report`
- `allowUnlisted: Boolean` optional
- `startTime/endTime: Float` unix milliseconds range for `reports`
- `limit: Int` default 100, min 1, max 100
- `page: Int` default first page

### 5) WorldData

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/worlddata.doc.html

- `encounter(id): Encounter`
- `expansion(id): Expansion`
- `expansions: [Expansion]`
- `region(id): Region`
- `regions: [Region]`
- `server(id, region, slug): Server`
- `subregion(id): Subregion`
- `zone(id): Zone`
- `zones(expansion_id): [Zone]`

### 6) ProgressRaceData

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/progressracedata.doc.html

- `progressRace(serverRegion, serverSubregion, serverSlug, zoneID, competitionID, difficulty, size, guildID, guildName): JSON`
- `detailedComposition(competitionID, guildID, guildName, serverSlug, serverRegion, encounterID, difficulty, size): JSON`

Notes:

- Marked as not frozen by official docs.
- Response JSON shape may change without notice.

### 7) UserData

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/userdata.doc.html

- `user(id): User`
- `currentUser: User` (user endpoint only)

### 8) RateLimitData

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/ratelimitdata.doc.html

Fields:

- `limitPerHour: Int!`
- `pointsSpentThisHour: Float!`
- `pointsResetIn: Int!`

### 9) ReportComponentData

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/reportcomponentdata.doc.html

- `list: [ReportComponent!]!`
- `get(key: String!): ReportComponent`
- `evaluateScript(contents: String!, filter: ReportComponentFilter, debug: Boolean, reportCode: String!): ReportComponentResult`

## High-Value Nested Endpoints

### Encounter.characterRankings

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/encounter.doc.html

`characterRankings(...) : JSON`

Available args include:

- `bracket: Int`
- `difficulty: Int`
- `filter: String`
- `page: Int`
- `partition: Int`
- `serverRegion: String`
- `serverSlug: String`
- `size: Int`
- `leaderboard: LeaderboardRank`
- `hardModeLevel: HardModeLevelRankFilter`
- `metric: CharacterRankingMetricType`
- `includeCombatantInfo: Boolean`
- `includeOtherPlayers: Boolean`
- `className: String`
- `specName: String`
- `externalBuffs: ExternalBuffRankFilter`
- `covenantID: Int`
- `soulbindID: Int`

### Encounter.fightRankings

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/encounter.doc.html

`fightRankings(...) : JSON`

Similar args to `characterRankings`, but with fight metric type.

### Report object data endpoints

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/report.doc.html

- `events(...) : ReportEventPaginator`
- `fights(...) : [ReportFight]`
- `graph(...) : JSON`
- `table(...) : JSON`
- `playerDetails(...) : JSON`
- `rankings(...) : JSON`

These have extensive filtering args for event/fight/time windows and should be queried selectively due payload size.

## Pagination Response Models (Official)

### ReportPagination

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/reportpagination.doc.html

Fields:

- `data: [Report]`
- `total: Int!`
- `per_page: Int!`
- `current_page: Int!`
- `from: Int`
- `to: Int`
- `last_page: Int!`
- `has_more_pages: Boolean!`

### CharacterPagination

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/characterpagination.doc.html

Fields identical to `ReportPagination`, with `data: [Character]`.

### GuildPagination

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/guildpagination.doc.html

Fields identical to `ReportPagination`, with `data: [Guild]`.

## Zone and Encounter Models (Official)

### Zone

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/zone.doc.html

Fields:

- `id: Int!`
- `brackets: Bracket`
- `difficulties: [Difficulty]`
- `encounters: [Encounter]`
- `expansion: Expansion!`
- `frozen: Boolean!`
- `name: String!`
- `partitions: [Partition]`

### Encounter

Docs: https://www.warcraftlogs.com/v2-api-docs/warcraft/encounter.doc.html

Fields:

- `id: Int!`
- `name: String!`
- `zone: Zone!`
- `journalID: Int!`
- `characterRankings(...): JSON`
- `fightRankings(...): JSON`

## JSON Fields With Non-Frozen Shapes

Per official docs, these are not guaranteed stable and should be treated as dynamic contracts:

- `Encounter.characterRankings(...): JSON`
- `Encounter.fightRankings(...): JSON`
- `Character.zoneRankings(...): JSON`
- `Character.encounterRankings(...): JSON`
- `Report.graph(...): JSON`
- `Report.table(...): JSON`
- `Report.playerDetails(...): JSON`
- `Report.rankings(...): JSON`
- `ProgressRaceData.progressRace(...): JSON`
- `ProgressRaceData.detailedComposition(...): JSON`

## Recommended Documentation Maintenance

- Keep this file aligned to official Warcraft Logs documentation and verified schema behavior.
- For schema changes, compare against: https://www.warcraftlogs.com/v2-api-docs/warcraft/
- Treat JSON-returning fields as dynamic contracts unless explicitly frozen in official docs.
