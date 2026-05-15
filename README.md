# 591 Rental Monitor

自動爬取 591 租屋網物件，存入 Supabase，透過 Telegram 發送新物件通知，並提供 Web 介面瀏覽與收藏。

## 架構

- **Scraper** — .NET 8 Console App，爬取 591、geocoding、寫入 Supabase、送 Telegram 通知
- **WebApp** — .NET 8 ASP.NET Core，提供 REST API + 靜態前端，讀取 Supabase 資料

---

## 前置需求

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- Supabase 專案（需建好 `listings` 與 `favorites` 資料表）
- Telegram Bot Token 與 Chat ID（僅 Scraper 需要）

---

## 環境變數設定

### Scraper

複製範本並填入實際值：

```bash
cp src/Scraper/.env.example src/Scraper/.env
```

`src/Scraper/.env`：

```
SUPABASE_URL=https://your-project.supabase.co
SUPABASE_KEY=your-supabase-service-role-key
TELEGRAM_BOT_TOKEN=your-telegram-bot-token
TELEGRAM_CHAT_ID=your-telegram-chat-id
```

### WebApp

```bash
cp src/WebApp/.env.example src/WebApp/.env
```

`src/WebApp/.env`：

```
SUPABASE_URL=https://your-project.supabase.co
SUPABASE_KEY=your-supabase-anon-key
```

---

## 搜尋條件設定（Scraper）

編輯 `src/Scraper/config.json`：

```json
{
  "Region": 3,
  "SectionCodes": [37],
  "Districts": [],
  "RoomTypes": [],
  "MinPrice": 18000,
  "MaxPrice": 30000,
  "MinSizePing": 2,
  "RequireFurniture": false,
  "RequireInternet": true,
  "NotifyHour": 10
}
```

| 欄位 | 說明 |
|------|------|
| `Region` | 縣市代碼（3 = 台北市） |
| `SectionCodes` | 行政區代碼清單 |
| `MinPrice` / `MaxPrice` | 租金範圍（元） |
| `MinSizePing` | 最小坪數 |
| `RequireFurniture` | 是否要求有家具 |
| `RequireInternet` | 是否要求有網路 |
| `NotifyHour` | 每日通知小時（24 小時制） |

---

## 啟動方式

### Scraper

```bash
cd src/Scraper
dotnet run
```

**特殊模式：**

```bash
# 補齊缺少座標的物件（呼叫 Nominatim geocoding）
dotnet run -- --backfill

# 重新抓取所有物件的設施資料
dotnet run -- --backfill-details
```

### WebApp

```bash
cd src/WebApp
dotnet run
```

預設監聽 `http://localhost:5000`，開啟瀏覽器即可使用。

---

## Docker（WebApp）

```bash
# 建置 image
docker build -t rental-webapp .

# 啟動（帶入環境變數）
docker run -p 8080:8080 \
  -e SUPABASE_URL=https://your-project.supabase.co \
  -e SUPABASE_KEY=your-supabase-anon-key \
  rental-webapp
```

瀏覽 `http://localhost:8080`。

---

## WebApp API

| 方法 | 路徑 | 說明 |
|------|------|------|
| GET | `/api/listings` | 取得物件清單，支援篩選參數 |
| GET | `/api/favorites` | 取得收藏清單（含物件資訊） |
| POST | `/api/favorites/{id}` | 加入收藏 |
| DELETE | `/api/favorites/{id}` | 移除收藏 |
| PATCH | `/api/favorites/{id}` | 更新收藏備註 |

`/api/listings` 篩選參數（皆為選填）：

| 參數 | 型別 | 說明 |
|------|------|------|
| `hasFurniture` | bool | 有家具 |
| `hasInternet` | bool | 有網路 |
| `hasNaturalGas` | bool | 有天然氣 |
| `hasParking` | bool | 有停車位 |
| `petAllowed` | bool | 可養寵物 |
| `maxPrice` | int | 最高租金 |
| `minSizePing` | double | 最小坪數 |
| `maxSizePing` | double | 最大坪數 |

---

## 執行測試

```bash
dotnet test
```
