# Dynamic Region And No Room Type Filter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the 591 scraper read the search region from configuration and omit room type filtering when no room types are configured.

**Architecture:** Add a `Region` property to the runtime config model, extract list URL creation into a small helper in `Scraper591Service`, and cover the URL behavior with focused unit tests. Keep the rest of the scrape and downstream pipeline unchanged.

**Tech Stack:** .NET 8, C#, xUnit, scraper config JSON, `HttpClient`

---

### Task 1: Add focused URL-construction tests

**Files:**
- Create: `Tests/Scraper.Tests/Scraper.Tests.csproj`
- Create: `Tests/Scraper.Tests/Scraper591ServiceTests.cs`
- Modify: `Scraper.csproj`

- [ ] **Step 1: Write the failing test project**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\\..\\Scraper.csproj" />
  </ItemGroup>
</Project>
```

```csharp
using Scraper.Config;
using Scraper.Services;

namespace Scraper.Tests;

public class Scraper591ServiceTests
{
    [Fact]
    public void BuildSearchUrl_UsesConfiguredRegion()
    {
        var config = new ScraperConfig
        {
            Region = 3,
            MaxPrice = 20000
        };

        var url = Scraper591Service.BuildSearchUrl(config);

        Assert.Contains("region=3", url);
    }

    [Fact]
    public void BuildSearchUrl_OmitsKindWhenNoRoomTypesConfigured()
    {
        var config = new ScraperConfig
        {
            Region = 1,
            MaxPrice = 20000,
            RoomTypes = new List<string>()
        };

        var url = Scraper591Service.BuildSearchUrl(config);

        Assert.DoesNotContain("kind=", url);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\Tests\Scraper.Tests\Scraper.Tests.csproj`
Expected: FAIL because `Scraper591Service.BuildSearchUrl` and `ScraperConfig.Region` do not exist yet.

- [ ] **Step 3: Add minimal production surface**

```csharp
public int Region { get; set; } = 1;
```

```csharp
internal static string BuildSearchUrl(ScraperConfig config)
{
    return "";
}
```

- [ ] **Step 4: Run test to verify it still fails for the right reason**

Run: `dotnet test .\Tests\Scraper.Tests\Scraper.Tests.csproj`
Expected: FAIL on assertions because the helper returns the wrong URL content.

- [ ] **Step 5: Commit**

```bash
git add Scraper.csproj Tests/Scraper.Tests/Scraper.Tests.csproj Tests/Scraper.Tests/Scraper591ServiceTests.cs Config/ScraperConfig.cs Services/Scraper591Service.cs
git commit -m "test: cover scraper search url configuration"
```

### Task 2: Implement configurable region and optional room type filter

**Files:**
- Modify: `Config/ScraperConfig.cs`
- Modify: `Services/Scraper591Service.cs`
- Modify: `config.json`

- [ ] **Step 1: Write the failing behavior assertions**

```csharp
[Fact]
public void BuildSearchUrl_IncludesKindWhenMappedRoomTypesExist()
{
    var config = new ScraperConfig
    {
        Region = 1,
        MaxPrice = 20000,
        RoomTypes = new List<string> { "?游惜雿振" }
    };

    var url = Scraper591Service.BuildSearchUrl(config);

    Assert.Contains("kind=1", url);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\Tests\Scraper.Tests\Scraper.Tests.csproj`
Expected: FAIL because URL construction does not yet include the mapped room type behavior correctly.

- [ ] **Step 3: Implement the minimal URL builder and wire it into search**

```csharp
internal static string BuildSearchUrl(ScraperConfig config)
{
    var sections = string.Join(",",
        config.Districts
            .Where(d => DistrictCodes.ContainsKey(d))
            .Select(d => DistrictCodes[d]));

    var queryParts = new List<string>
    {
        $"region={config.Region}",
        $"section={sections}",
        $"price=0_{config.MaxPrice}",
        "order=posttime",
        "orderType=desc"
    };

    var kinds = string.Join(",",
        config.RoomTypes
            .Where(t => RoomTypeCodes.ContainsKey(t))
            .Select(t => RoomTypeCodes[t]));

    if (!string.IsNullOrWhiteSpace(kinds))
        queryParts.Add($"kind={kinds}");

    return $"https://rent.591.com.tw/list?{string.Join("&", queryParts)}";
}
```

```json
{
  "Region": 1,
  "RoomTypes": []
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\Tests\Scraper.Tests\Scraper.Tests.csproj`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Config/ScraperConfig.cs Services/Scraper591Service.cs config.json Tests/Scraper.Tests/Scraper591ServiceTests.cs
git commit -m "feat: make scraper region configurable"
```

### Task 3: Verify the application still builds

**Files:**
- No file changes required

- [ ] **Step 1: Run the scraper test project**

Run: `dotnet test .\Tests\Scraper.Tests\Scraper.Tests.csproj`
Expected: PASS

- [ ] **Step 2: Run the scraper build**

Run: `dotnet build .\Scraper.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit verification-ready state**

```bash
git add Config/ScraperConfig.cs Services/Scraper591Service.cs config.json Scraper.csproj Tests/Scraper.Tests/Scraper.Tests.csproj Tests/Scraper.Tests/Scraper591ServiceTests.cs docs/superpowers/plans/2026-05-14-dynamic-region-no-roomtype-filter.md
git commit -m "docs: add implementation plan for configurable scraper region"
```
