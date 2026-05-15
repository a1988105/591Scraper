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

## 部署到 Railway（WebApp）

目前專案根目錄已有 `Dockerfile`，Railway 會自動使用它。

### 1. 建立 Railway Service

有兩種做法：

- 連 GitHub repo，讓 Railway 自動從 repo 部署
- 用 CLI 在本機目錄部署：`railway up`

repo 根目錄已包含 `railway.json`，會指定：

- 使用 root `Dockerfile`
- healthcheck path 為 `/health`
- 只在 `Dockerfile`、`.dockerignore`、`src/WebApp/**` 變更時觸發此 service 部署

### 2. 設定必要環境變數

在 Railway 的 service variables 加入：

```env
SUPABASE_URL=https://your-project.supabase.co
SUPABASE_KEY=your-supabase-anon-key
```

不需要手動設定 `PORT`。Railway 會在執行時自動注入；WebApp 現在會自動綁定該埠。

### 3. 產生公開網址

部署完成後，到：

- `Settings`
- `Networking`
- `Public Networking`

按 `Generate Domain` 產生 `.railway.app` 網址。

### 4. 驗證

部署成功後可測：

- `/health`
- `/api/listings`

例如：

```text
https://your-app.up.railway.app/health
```

如果你是用 CLI，最短流程是：

```bash
railway login
railway init
railway up
```

### 5. 常見失敗點

- `SUPABASE_URL` 或 `SUPABASE_KEY` 沒設，服務會在啟動時直接失敗
- 如果 Supabase table 或 RLS policy 沒開好，API 會回 4xx/5xx
- Railway 使用 root `Dockerfile`；若之後搬位置，要設定 `RAILWAY_DOCKERFILE_PATH`

---

## 排程執行 Scraper（macOS）

在 macOS 上透過 `launchd` 設定 Scraper 定期自動執行，登出後依然會繼續跑。

### 1. 編譯並發佈執行檔

```bash
cd src/Scraper
dotnet publish -c Release -o ../../publish/scraper
```

發佈後 `publish/scraper/` 目錄內會包含執行檔與 `.env`、`config.json`。

### 2. 建立 LaunchDaemon plist

建立 `/Library/LaunchDaemons/com.erictsai.591scraper.plist`（需替換 `erictsai` 為你的使用者名稱）：

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.erictsai.591scraper</string>

    <key>UserName</key>
    <string>erictsai</string>

    <key>ProgramArguments</key>
    <array>
        <string>/Users/erictsai/Desktop/side project/591Scraper/publish/scraper/Scraper</string>
    </array>

    <key>WorkingDirectory</key>
    <string>/Users/erictsai/Desktop/side project/591Scraper/publish/scraper</string>

    <key>StartInterval</key>
    <integer>7200</integer>

    <key>StandardOutPath</key>
    <string>/Users/erictsai/Library/Logs/591Scraper/scraper.log</string>

    <key>StandardErrorPath</key>
    <string>/Users/erictsai/Library/Logs/591Scraper/scraper-error.log</string>

    <key>RunAtLoad</key>
    <false/>
</dict>
</plist>
```

`StartInterval` 單位為秒，`7200` = 每 2 小時。

### 3. 建立 log 目錄並載入排程

```bash
mkdir -p ~/Library/Logs/591Scraper
sudo cp /path/to/com.erictsai.591scraper.plist /Library/LaunchDaemons/
sudo launchctl load /Library/LaunchDaemons/com.erictsai.591scraper.plist
```

確認已載入：

```bash
sudo launchctl list | grep 591scraper
# 預期輸出：-  0  com.erictsai.591scraper
```

### 4. 常用指令

```bash
# 手動立刻跑一次
sudo launchctl start com.erictsai.591scraper

# 停用排程（重開機後也不會跑）
sudo launchctl unload /Library/LaunchDaemons/com.erictsai.591scraper.plist

# 重新啟用排程
sudo launchctl load /Library/LaunchDaemons/com.erictsai.591scraper.plist

# 完全移除
sudo launchctl unload /Library/LaunchDaemons/com.erictsai.591scraper.plist
sudo rm /Library/LaunchDaemons/com.erictsai.591scraper.plist

# 查看 log
tail -f ~/Library/Logs/591Scraper/scraper.log
```

> **注意**：電腦睡眠時不會執行，喚醒後到下一個排程時間點才會跑。`RunAtLoad` 設為 `false` 表示載入時不立刻執行。

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
