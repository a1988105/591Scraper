# Dynamic Region And No Room Type Filter

## Goal

Make the 591 scraper stop hardcoding Taipei City and allow the search region to be configured from `config.json`. At the same time, remove room type filtering from the active configuration so the scraper can return all room types.

## Scope

This change only covers the list-search request construction in the scraper and the configuration surface that drives it.

Included:

- Add a configurable `Region` field to scraper config.
- Use `Region` instead of hardcoded `region=1` when building the 591 list URL.
- Treat an empty `RoomTypes` list as "do not filter by room type".
- Update the local `config.json` so room type filtering is disabled by default.

Excluded:

- Reworking district code mappings for every city.
- Replacing district names with a full city-aware mapping model.
- Changes to downstream storage, detail fetch, Telegram notification, or Supabase logic.

## Current State

The scraper currently builds the 591 list URL with a hardcoded `region=1`, which locks search to Taipei City. It also always emits a `kind=` query string derived from `RoomTypes`, so room type filtering remains active whenever values are present.

## Proposed Design

### Configuration

Add an integer `Region` property to `ScraperConfig` with a default value of `1`.

This keeps the current behavior for existing configurations while allowing operators to switch region by editing `config.json`.

### Search URL Construction

Update `Scraper591Service.SearchListingsAsync` to:

- read `config.Region` for the `region` query parameter
- continue translating `Districts` into `section` values using the existing mapping
- only include `kind` in the request when at least one configured room type maps to a known 591 code

If `RoomTypes` is empty, unknown, or resolves to no codes, the request should omit `kind` entirely. That makes the query return all room types supported by the selected region and district filters.

### Local Runtime Configuration

Update `config.json` to:

- set `Region` explicitly
- set `RoomTypes` to `[]`

This makes the immediate runtime behavior match the requested outcome without removing future support for room type filtering.

## Data Flow

1. `Program.cs` loads `config.json` into `ScraperConfig`.
2. `Scraper591Service.SearchListingsAsync` reads `config.Region`, `config.Districts`, and `config.RoomTypes`.
3. The service builds the 591 list URL.
4. When `RoomTypes` is empty, the URL contains no `kind` parameter.
5. The rest of the pipeline remains unchanged.

## Error Handling

- If `Districts` contains names not present in the current mapping, they are ignored as before.
- If `RoomTypes` contains unknown values, they are ignored; if nothing remains after mapping, `kind` is omitted.
- No new exceptions are introduced for missing `Region`; the default value preserves compatibility.

## Testing

- Build the project to verify the configuration model and URL-construction code compile.
- Confirm that with `RoomTypes: []`, the generated URL does not contain `kind=`.
- Confirm that changing `Region` in `config.json` changes the outgoing `region=` value.

## Risks

The current district-code mapping appears to be city-specific. Making `Region` configurable does not automatically make every district name portable across cities. Users must keep `Districts` aligned with whatever district code mapping the scraper currently supports.
