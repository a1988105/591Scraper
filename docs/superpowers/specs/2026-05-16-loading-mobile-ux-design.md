# Loading 狀態 + 行動版 UX 設計文件

**日期：** 2026-05-16  
**範圍：** `src/WebApp/wwwroot/app.js`、`style.css`、`index.html`

---

## 功能一：Loading 狀態

### 設計原則

Loading 指示器必須靠近動作發生的位置，讓使用者清楚知道「哪個操作在等待」。

### 三種場景

#### 場景一：頁面級載入（切換分頁、套用篩選）

- **觸發：** `loadView()`、`applyFilters()` 呼叫期間
- **Spinner 位置：** 右上角 `#countBadge`
- **視覺表現：**
  - Badge 顯示 `<spinner> 載入中...`（spinner 為 CSS animation，`border-top-color: #6366f1`）
  - `#listContainer` 淡出至 `opacity: 0.35`
- **結束時：** Badge 恢復正常文字（如「收藏 5 筆」），清單恢復 opacity

#### 場景二：卡片操作（「不考慮」/ 恢復按鈕）

- **觸發：** `quickToggleRejected()` 呼叫期間
- **Spinner 位置：** 按鈕內部
- **視覺表現：**
  - 按鈕文字改為 `<spinner> 處理中`
  - 按鈕設為 `disabled`，防止重複點擊
  - 卡片本身不淡出
- **結束時：** `loadView()` 重新渲染，卡片自然更新

#### 場景三：Modal 操作（加入 / 移除收藏）

- **觸發：** `addFavorite()`、`removeFavorite()` 呼叫期間
- **Spinner 位置：** Modal 內的操作按鈕
- **視覺表現：**
  - 加入收藏按鈕 → `<spinner> 加入中...`
  - 移除收藏按鈕 → `<spinner> 移除中...`
  - 按鈕設為 `disabled`
  - Modal 保持開啟，完成後關閉
- **結束時：** `loadView()` 重新渲染後 `closeModal()`

### 實作方式

```
全域 loadingCount（整數）不用於上述三種場景。
三種場景各自管理自己的 loading 狀態，不共用計數器。
```

- `setPageLoading(bool)` — 控制 badge spinner + listContainer opacity
- `setButtonLoading(btn, bool, loadingText)` — 控制單一按鈕的 spinner + disabled 狀態
- Spinner HTML：`<span class="btn-spinner"></span>`（CSS keyframe `spin`）

---

## 功能二：行動版 UX

### 設計方向

**清單為主 + 頂部小地圖**（方案 C），盡量不改動現有行為。

### 桌機版（> 720px）

完全不受影響，維持現有雙欄佈局（地圖左、側邊欄右）。

### 行動版（≤ 720px）佈局

```
┌─────────────────────┐
│ Topnav              │  ← 不變
│ Tabs                │  ← 不變
├─────────────────────┤
│                     │
│   小地圖（~110px）   │  ← 新增，固定高度
│              [⛶展開] │  ← 右下角按鈕
├─────────────────────┤
│ 清單（可滾動）       │  ← 現有側邊欄
│                     │
│                     │
└─────────────────────┘
```

### 互動行為

| 操作 | 結果 |
|---|---|
| 預設進入 | 顯示小地圖 + 清單 |
| 點「⛶ 展開」 | 切換全螢幕地圖，浮動按鈕改為「📋 清單」 |
| 點「📋 清單」 | 切回小地圖 + 清單 |
| 點清單卡片 | 只開 modal，小地圖不動 |
| 點地圖標記 | 開 modal（現有行為不變） |

### 實作方式

**CSS 變更（`style.css`）：**

- 行動版 `.layout` 改為 `flex-direction: column`
- 新增 `.mini-map` class：`height: 110px; flex-shrink: 0; position: relative`
- `#map` 在小地圖模式下固定 110px 高；全螢幕時 `position: absolute; inset: 0`
- `.sidebar` 在全螢幕模式時 `display: none`

**HTML 變更（`index.html`）：**

- 小地圖加一個展開按鈕 `<button class="mini-map-expand" onclick="toggleMobileView()">⛶ 展開</button>`

**JS 變更（`app.js`）：**

- `toggleMobileView()` 重構：
  - `mobileView === 'list'` → layout 顯示小地圖 + 清單，地圖高度 110px，`map.invalidateSize()`
  - `mobileView === 'map'` → layout 顯示全螢幕地圖，浮動按鈕文字改「📋 清單」
- 小地圖高度固定，不可拖曳調整（保持簡單）

### 不改動的項目

- 桌機版雙欄佈局
- 卡片點擊開 modal 邏輯
- 地圖標記點擊邏輯
- filter panel 收合行為
- modal 底部 sheet 行為
