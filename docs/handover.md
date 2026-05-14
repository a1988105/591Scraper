# Scraper591Service HTML Parser — Handover to Codex

## Context

The 591 rental scraper (`src/Scraper/Services/Scraper591Service.cs`) switched from calling
the AJAX API (`/home/search/rsList`, always returns HTTP 419) to parsing the SSR HTML from the
public list page. The switch is committed but the regex patterns in `ParseHtmlListings` are
**wrong** — the method returns 0 items on every run.

## The Bug

`ParseHtmlListings` uses patterns that assume text is a direct child of its container div.
The actual 591 HTML has deeply-nested content. Three patterns fail:

| Pattern in code | What it tries to match | Actual result |
|---|---|---|
| `class="item-info-title"[^>]*>([^<]+)` | Title text directly in div | Empty (title is in nested `<a title="...">`) |
| `class="price font-arial"[^>]*>([^<]+)` | Price text directly in div | Matches only 15/30 items |
| `class="item-info-txt"[^>]*>([^<]+)` | Kind/address text | Empty (nested in spans) |

Because `titles.Count == 0`, `count = Math.Min(ids, titles, prices) == 0` → 0 results returned.

---

## Live HTML Structure (from curl of production page)

Search URL format:
```
https://rent.591.com.tw/list?region=1&section=7&kind=2,8&price=0_20000&order=posttime&orderType=desc
```

Each listing card looks like this in the SSR HTML (simplified):
```html
<div class="item" data-id="21256534" data-v-734829f9 data-v-439ec912>
  ...
  <!-- TITLE is in the anchor's title attribute, NOT in div text -->
  <div class="item-info-title" data-v-439ec912>
    <a class="link v-middle"
       href="https://rent.591.com.tw/21256534"
       target="_blank"
       title="租補-信義區公所廣慈後山埤站旁5坪套房"
       data-v-439ec912>
      <!--[-->租補-信義區公所廣慈後山埤站旁5坪套房<!--]-->
    </a>
  </div>
  ...
  <!-- PRICE works but only ~15/30 items have this class; others show 面議 or use different markup -->
  <div class="price-info" data-v-93fbb22b>
    <div class="price font-arial" data-v-93fbb22b>12,900</div>
  </div>
  ...
  <!-- KIND+SIZE — text nested in spans inside item-info-txt; icon class identifies which field -->
  <div class="item-info-txt" data-v-439ec912>
    <i class="ic-house house-home" data-v-439ec912></i>
    <span data-v-439ec912>獨立套房</span>
    <!----><span class="line" data-v-439ec912>
      <div class="inline-flex-row" data-v-439ec912>5坪</div>
    </span>
    <span class="line" data-v-439ec912>
      <div class="inline-flex-row" data-v-439ec912>2F/4F</div>  <!-- floor info -->
    </span>
  </div>
  <!-- ADDRESS — also in item-info-txt but with house-place icon -->
  <div class="item-info-txt" data-v-439ec912>
    <i class="ic-house house-place" data-v-439ec912></i>
    <span data-v-439ec912><!--[-->描述文字<!--]--></span>
    <span data-v-439ec912>
      <div class="inline-flex-row" data-v-439ec912>信義區-福德街268巷</div>
    </span>
  </div>
</div>
```

## Verified Working Patterns (tested on live HTML)

Patterns confirmed to work on the live page (counts verified against 30-item result page):

| Field | Regex | Count | Notes |
|---|---|---|---|
| Post ID | `class="item" data-id="(\d+)"` | 30 ✓ | Exact match |
| Title | `title="([^"]+)"` | 19 global (some items lack title attr or have emoji-only titles) | Use first match **within block** |
| Price | `class="price font-arial"[^>]*>([0-9,]+)` | 15 global | Use within block; default to "0" if missing |
| Kind | `>(整層住家\|獨立套房\|雅房\|分租套房)<` | 42 global | Use first match **within block** |
| Size | `>(\d+\.?\d*)坪<` | works | Use first `(\d+\.?\d*)坪` within block; `class="line"` contains size |
| Address | `>([^<]*[區縣市]-[^<]+)<` | 45 global | Use **last** match within block (address is after kind/size) |

## Required Fix

**Replace `ParseHtmlListings` with a per-block approach.** Extract each item's block of HTML
individually, then parse within that block so counts can never misalign.

### Algorithm

```csharp
internal static List<SearchItem> ParseHtmlListings(string html, ScraperConfig config)
{
    var itemRx = new Regex(@"class=""item""\s+data-id=""(\d+)""");
    var itemMatches = itemRx.Matches(html);
    var items = new List<SearchItem>();

    for (int i = 0; i < itemMatches.Count; i++)
    {
        var postId = itemMatches[i].Groups[1].Value;

        // Extract this item's block of HTML (up to the next item or end of string)
        var blockStart = itemMatches[i].Index;
        var blockEnd   = i + 1 < itemMatches.Count ? itemMatches[i + 1].Index : html.Length;
        var block      = html[blockStart..blockEnd];

        // Title — from the anchor's title attribute
        var titleM = Regex.Match(block, @"title=""([^""]+)""");
        var title  = titleM.Success ? titleM.Groups[1].Value : "";

        // Price — may be absent (面議 / promoted listing)
        var priceM = Regex.Match(block, @"class=""price font-arial""[^>]*>([0-9,]+)");
        var price  = priceM.Success ? priceM.Groups[1].Value.Replace(",", "") : "0";

        // Kind — first occurrence of a known kind name as direct element text
        var kindM    = Regex.Match(block, @">(整層住家|獨立套房|雅房|分租套房)<");
        var kindName = kindM.Success ? kindM.Groups[1].Value : "";

        // Size — first "N坪" pattern in block
        var sizeM = Regex.Match(block, @">(\d+\.?\d*)坪<");
        var area  = sizeM.Success ? sizeM.Groups[1].Value : "0";

        // Address — last "XX區/市-..." occurrence in block
        var addrMatches = Regex.Matches(block, @">([^<]*(?:區|市)-[^<]+)<");
        var address     = addrMatches.Count > 0
            ? addrMatches[addrMatches.Count - 1].Groups[1].Value.Trim()
            : "";

        // Filter
        if (!double.TryParse(area, out var sizePing) || sizePing < config.MinSizePing)
            continue;

        items.Add(new SearchItem
        {
            PostId   = postId,
            Title    = title,
            Price    = price,
            Address  = address,
            Area     = area,
            KindName = kindName,
            Photo    = ""
        });
    }

    return items;
}
```

### Also update `SearchListingsAsync`

The HttpRequestMessage constructor uses `class="item"` as the block-start pattern — that has a
literal space. In the regex string make sure there is `\s+` or a literal space as the HTML shows
`class="item" data-id=...` (space between them). The algorithm above uses `\s+` which is safer.

## Files to Edit

- **`src/Scraper/Services/Scraper591Service.cs`**
  - Replace the body of `ParseHtmlListings` with the per-block algorithm above.
  - The method signature stays `internal static List<SearchItem> ParseHtmlListings(string html, ScraperConfig config)`.
  - `SearchListingsAsync` stays unchanged (just passes HTML to the parser).

- **`tests/Scraper.Tests/Services/Scraper591ServiceTests.cs`**
  - Update `SampleListHtml` constant to match the **real** HTML structure above.
  - The test HTML must have `class="item" data-id="..."` containing nested anchors with `title=`,
    price in `<div class="price font-arial">`, kind in a `<span>` preceded by `house-home` icon,
    size in `>Nping<`, and address in the last `>XX區-...<` element.
  - Existing test assertions can stay the same (PostId, Price, KindName, Area, Address).

## Test HTML Template for `SampleListHtml`

```html
<html><body>
<div class="item" data-id="111">
  <div class="item-info-title">
    <a href="https://rent.591.com.tw/111" target="_blank" title="套房A">套房A</a>
  </div>
  <div class="price font-arial">15,000</div>
  <div class="item-info-txt">
    <i class="ic-house house-home"></i><span>獨立套房</span>
    <span class="line"><div class="inline-flex-row">10坪</div></span>
  </div>
  <div class="item-info-txt">
    <i class="ic-house house-place"></i>
    <span><div class="inline-flex-row">大安區-某街道</div></span>
  </div>
</div>
<div class="item" data-id="222">
  <div class="item-info-title">
    <a href="https://rent.591.com.tw/222" target="_blank" title="小套房B">小套房B</a>
  </div>
  <div class="price font-arial">5,000</div>
  <div class="item-info-txt">
    <i class="ic-house house-home"></i><span>獨立套房</span>
    <span class="line"><div class="inline-flex-row">5坪</div></span>
  </div>
  <div class="item-info-txt">
    <i class="ic-house house-place"></i>
    <span><div class="inline-flex-row">大安區-另一街道</div></span>
  </div>
</div>
</body></html>
```

## Expected Test Results (with BasicConfig: MinSizePing=8, MaxPrice=20000)

- Item 222 (5坪) should be filtered out by MinSizePing
- Item 111 should pass: PostId="111", Price="15000", KindName="獨立套房", Area="10", Address="大安區-某街道"

## Build & Test Commands

```bash
dotnet test tests/Scraper.Tests/Scraper.Tests.csproj -v quiet
dotnet build src/Scraper/Scraper.csproj -c Release
```

All 10 existing tests must still pass after the fix.

## Commit Message

```
fix: use per-block HTML parsing to fix 0-result scrape on 591 list page

Title, price, kind, size and address are all deeply nested in the
SSR HTML — positional regex across the whole page returns 0 items.
Switch to per-block extraction (one block per data-id match) so each
field is parsed within its own listing card.
```

## Repository

- Remote: `git@github.com:a1988105/591Scraper.git` (master branch)
- Push after commit once SSH is available, or switch remote to HTTPS.
