# Loading 狀態 + 行動版 UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在所有 API 操作加上情境化 loading 狀態，並將行動版改為小地圖常駐 + 清單的雙區佈局。

**Architecture:** 三種 loading 場景（頁面級、卡片按鈕、Modal 按鈕）各自用對應的 helper function 管理，不共用計數器。行動版佈局改為 column flexbox，地圖固定 110px 高，點展開才切全螢幕。

**Tech Stack:** Vanilla JS、CSS（無框架），Leaflet 1.9.4（地圖），ASP.NET Core 靜態檔案服務。

---

## 檔案變更對應

| 檔案 | 任務 |
|---|---|
| `src/WebApp/wwwroot/style.css` | Task 1、Task 4 |
| `src/WebApp/wwwroot/app.js` | Task 2、Task 3、Task 5 |
| `src/WebApp/wwwroot/index.html` | Task 4 |

---

## Task 1：CSS — Spinner 動畫與 Loading 樣式

**Files:**
- Modify: `src/WebApp/wwwroot/style.css`

- [ ] **Step 1：在 style.css 最頂部加入 @keyframes spin**

在 `*, *::before, *::after { ... }` 之前插入：

```css
@keyframes spin { to { transform: rotate(360deg); } }
```

- [ ] **Step 2：在 `.btn-591` 規則之後加入 spinner 相關 class**

```css
.btn-spinner {
  display: inline-block;
  width: 12px;
  height: 12px;
  border: 2px solid rgba(255,255,255,0.3);
  border-top-color: currentColor;
  border-radius: 50%;
  animation: spin 0.7s linear infinite;
  vertical-align: middle;
  margin-right: 4px;
  flex-shrink: 0;
}

.badge-spinner {
  display: inline-block;
  width: 12px;
  height: 12px;
  border: 2px solid #334155;
  border-top-color: #6366f1;
  border-radius: 50%;
  animation: spin 0.7s linear infinite;
  vertical-align: middle;
  margin-right: 4px;
  flex-shrink: 0;
}

#listContainer.loading {
  opacity: 0.35;
  pointer-events: none;
  transition: opacity 0.15s;
}
```

- [ ] **Step 3：手動驗證 CSS 語法正確**

啟動開發伺服器，確認沒有 CSS 解析錯誤（開啟瀏覽器 DevTools → Console，應無 CSS 錯誤）。

- [ ] **Step 4：Commit**

```bash
git add src/WebApp/wwwroot/style.css
git commit -m "feat: add spinner animation and loading CSS classes"
```

---

## Task 2：JS — Loading Helper Functions + 頁面級 Loading

**Files:**
- Modify: `src/WebApp/wwwroot/app.js`

- [ ] **Step 1：在 app.js 最頂部（let map; 之前）加入 helper functions**

```js
function setPageLoading(on) {
  const badge = document.getElementById('countBadge');
  const list = document.getElementById('listContainer');
  if (on) {
    badge.innerHTML = '<span class="badge-spinner"></span> 載入中...';
    list.classList.add('loading');
  } else {
    list.classList.remove('loading');
    if (badge.querySelector('.badge-spinner')) {
      badge.textContent = '—';
    }
  }
}

function setButtonLoading(btn, on, loadingText = '') {
  if (on) {
    btn.dataset.originalHtml = btn.innerHTML;
    btn.innerHTML = `<span class="btn-spinner"></span>${loadingText}`;
    btn.disabled = true;
  } else {
    btn.innerHTML = btn.dataset.originalHtml || '';
    btn.disabled = false;
    delete btn.dataset.originalHtml;
  }
}
```

- [ ] **Step 2：將 `loadView` 包上 setPageLoading**

找到現有的 `loadView` function（約第 40 行），改為：

```js
async function loadView(view) {
  clearMarkers();
  setPageLoading(true);
  try {
    if (view === 'favorites') {
      await loadFavorites();
    } else {
      await loadAllListings();
    }
  } finally {
    setPageLoading(false);
  }
}
```

- [ ] **Step 3：在 `applyFilters` 的 all-listings 分支加上 setPageLoading**

找到 `applyFilters` function 中 `const listings = await apiFetch(...)` 那行，把從 `const listings = ...` 到最後的 `renderMarkers(listings)` 改為：

```js
  setPageLoading(true);
  try {
    const listings = await apiFetch('/api/listings?' + params.toString());
    currentListings = listings;
    document.getElementById('countBadge').textContent = `${listings.length} 筆物件`;
    renderList(listings, currentFavorites);
    renderMarkers(listings);
  } finally {
    setPageLoading(false);
  }
```

- [ ] **Step 4：手動測試頁面級 loading**

啟動 app，開啟瀏覽器 DevTools → Network，將 throttling 設為 Slow 3G。  
切換「⭐ 收藏」↔「🔍 所有物件」，確認：
- badge 顯示 spinner + 「載入中...」
- listContainer 淡出至約 35% 透明度
- 資料載入完後 badge 恢復正常文字、listContainer 恢復不透明

- [ ] **Step 5：Commit**

```bash
git add src/WebApp/wwwroot/app.js
git commit -m "feat: page-level loading state for loadView and applyFilters"
```

---

## Task 3：JS — 卡片與 Modal 按鈕 Loading

**Files:**
- Modify: `src/WebApp/wwwroot/app.js`

- [ ] **Step 1：更新 `buildCard` 中按鈕的 onclick，傳入 `this`**

找到 `buildCard` function 中的按鈕 HTML（約含 `quickToggleRejected` 的那行），改為：

```js
      <button class="card-reject-btn${isRejected ? ' is-rejected' : ''}"
        onclick="event.stopPropagation(); quickToggleRejected(${listing.id}, ${isRejected}, this)">
        ${isRejected ? '↩ 恢復' : '✕ 不考慮'}
      </button>
```

- [ ] **Step 2：更新 `quickToggleRejected` 接收第三個參數 btn 並加上 loading**

找到現有的 `quickToggleRejected` function，整體替換為：

```js
window.quickToggleRejected = async function (listingId, isCurrentlyRejected, btn) {
  setButtonLoading(btn, true, '處理中');
  try {
    if (isCurrentlyRejected) {
      await apiFetch(`/api/favorites/${listingId}`, 'PATCH', { status: '待看' });
    } else {
      if (!favoriteIds.has(listingId)) {
        await apiFetch(`/api/favorites/${listingId}`, 'POST');
      }
      await apiFetch(`/api/favorites/${listingId}`, 'PATCH', { status: '不考慮' });
    }
    await loadView(activeView);
  } catch {
    setButtonLoading(btn, false);
  }
};
```

- [ ] **Step 3：更新 `addFavorite` 加上 modal 按鈕 loading**

找到現有的 `addFavorite` function，整體替換為：

```js
window.addFavorite = async function () {
  const btn = document.querySelector('#modalActions button');
  setButtonLoading(btn, true, '加入中...');
  try {
    await apiFetch(`/api/favorites/${currentListingId}`, 'POST');
    await loadView(activeView);
    closeModal();
  } catch {
    setButtonLoading(btn, false);
  }
};
```

- [ ] **Step 4：更新 `removeFavorite` 加上 modal 按鈕 loading**

找到現有的 `removeFavorite` function，整體替換為：

```js
window.removeFavorite = async function () {
  const btn = document.querySelector('#modalActions button');
  setButtonLoading(btn, true, '移除中...');
  try {
    await apiFetch(`/api/favorites/${currentListingId}`, 'DELETE');
    await loadView(activeView);
    closeModal();
  } catch {
    setButtonLoading(btn, false);
  }
};
```

- [ ] **Step 5：手動測試卡片與 Modal loading**

Network throttling 設為 Slow 3G。

**卡片按鈕：**
- 點「✕ 不考慮」，確認按鈕變為 spinner + 「處理中」且 disabled
- 點「↩ 恢復」，同上
- 完成後卡片自然更新

**Modal 按鈕：**
- 在未收藏的物件上點地圖 marker 開 modal，點「加入收藏」，確認按鈕變為 spinner + 「加入中...」
- 在已收藏的物件開 modal，點「移除收藏」，確認 spinner + 「移除中...」
- Modal 完成後自動關閉

- [ ] **Step 6：Commit**

```bash
git add src/WebApp/wwwroot/app.js
git commit -m "feat: button-level loading for card reject and modal favorite actions"
```

---

## Task 4：HTML + CSS — 行動版小地圖佈局

**Files:**
- Modify: `src/WebApp/wwwroot/index.html`
- Modify: `src/WebApp/wwwroot/style.css`

- [ ] **Step 1：在 index.html 的 `#map` div 內加入展開按鈕**

找到：
```html
  <div id="map"></div>
```

改為：
```html
  <div id="map">
    <button class="mini-map-expand" onclick="toggleMobileView()">⛶ 展開</button>
  </div>
```

- [ ] **Step 2：更新 index.html 浮動按鈕的初始文字**

找到：
```html
  <div class="mobile-view-toggle" id="mobileViewToggle">
    <button onclick="toggleMobileView()">🗺 地圖</button>
  </div>
```

改為：
```html
  <div class="mobile-view-toggle" id="mobileViewToggle">
    <button onclick="toggleMobileView()">📋 清單</button>
  </div>
```

- [ ] **Step 3：更新 style.css 的 mobile media query 佈局規則**

在 `@media (max-width: 720px)` 區塊內，找到以下四條現有規則：

```css
  /* Layout: full-height, views toggled by class */
  .layout { position: relative; }
  #map { position: absolute; inset: 0; display: none; z-index: 1; }
  .layout.show-map #map { display: block; }
  .sidebar { width: 100%; border-left: none; flex: 1; min-height: 0; overflow-y: auto;
    padding-bottom: 72px; /* space for floating button */ }
  .layout.show-map .sidebar { display: none; }
```

全部替換為：

```css
  /* Layout: column flex, mini-map always visible */
  .layout { flex-direction: column; position: relative; }

  #map {
    flex: none;
    height: 110px;
    width: 100%;
    display: block;
    position: relative;
    z-index: 1;
  }

  .layout.show-map #map {
    position: absolute;
    inset: 0;
    height: auto;
    z-index: 2;
  }

  .sidebar {
    width: 100%;
    border-left: none;
    flex: 1;
    min-height: 0;
    overflow-y: auto;
    padding-bottom: 0;
  }
  .layout.show-map .sidebar { display: none; }

  /* Expand button inside mini-map */
  .mini-map-expand {
    position: absolute;
    bottom: 8px;
    right: 8px;
    background: rgba(99,102,241,0.9);
    color: white;
    border: none;
    border-radius: 5px;
    padding: 4px 10px;
    font-size: 0.78em;
    font-weight: 600;
    cursor: pointer;
    z-index: 1000;
    -webkit-tap-highlight-color: transparent;
  }
  .layout.show-map .mini-map-expand { display: none; }
```

- [ ] **Step 4：更新浮動按鈕的顯示規則**

在同一個 `@media (max-width: 720px)` 區塊最底部，找到：

```css
  /* Show floating toggle button */
  .mobile-view-toggle { display: flex; }
```

改為：

```css
  /* Floating button: only visible in full-screen map mode */
  .layout.show-map ~ .mobile-view-toggle { display: flex; }
```

- [ ] **Step 5：在桌機版（非 media query）隱藏展開按鈕**

找到 `/* ─── Mobile view toggle (desktop: hidden) ──────────────────────  */` 區塊，在其後加入：

```css
.mini-map-expand { display: none; }
```

- [ ] **Step 6：手動測試 HTML/CSS 佈局（桌機）**

在桌機瀏覽器（視窗 > 720px）確認：
- 展開按鈕不可見
- 雙欄佈局（地圖左、側邊欄右）完全正常
- 所有原有功能不受影響

- [ ] **Step 7：Commit**

```bash
git add src/WebApp/wwwroot/index.html src/WebApp/wwwroot/style.css
git commit -m "feat: mobile mini-map layout with expand button"
```

---

## Task 5：JS — 更新 toggleMobileView + 驗收測試

**Files:**
- Modify: `src/WebApp/wwwroot/app.js`

- [ ] **Step 1：重構 `toggleMobileView`**

找到現有的 `toggleMobileView` function：

```js
window.toggleMobileView = function () {
  mobileView = mobileView === 'list' ? 'map' : 'list';
  document.querySelector('.layout').classList.toggle('show-map', mobileView === 'map');
  document.querySelector('.mobile-view-toggle button').textContent =
    mobileView === 'map' ? '📋 清單' : '🗺 地圖';
  if (mobileView === 'map') setTimeout(() => map.invalidateSize(), 50);
};
```

整體替換為：

```js
window.toggleMobileView = function () {
  mobileView = mobileView === 'list' ? 'map' : 'list';
  document.querySelector('.layout').classList.toggle('show-map', mobileView === 'map');
  setTimeout(() => map.invalidateSize(), 50);
};
```

- [ ] **Step 2：行動版完整驗收測試**

用 DevTools 切到手機模擬（例：iPhone 14 Pro，390px 寬），reload 頁面，逐項確認：

**佈局：**
- [ ] 預設看到：topnav → tabs → 小地圖（110px，有標記） → 清單
- [ ] 小地圖右下角有「⛶ 展開」按鈕，可點擊
- [ ] 浮動按鈕不可見（預設清單模式）

**切換：**
- [ ] 點「⛶ 展開」→ 切換為全螢幕地圖，展開按鈕消失，浮動「📋 清單」按鈕出現
- [ ] 點「📋 清單」→ 切回小地圖 + 清單
- [ ] 地圖標記在小地圖和全螢幕都正確顯示（確認 `map.invalidateSize()` 生效）

**卡片互動：**
- [ ] 點清單卡片 → 只開 modal，小地圖不動
- [ ] 點小地圖標記 → 開 modal（現有行為）

**Loading（mobile throttling Slow 3G）：**
- [ ] 切換分頁：badge spinner 出現，清單淡出
- [ ] 點「✕ 不考慮」：按鈕 spinner，disabled
- [ ] Modal 操作：按鈕 spinner，disabled

- [ ] **Step 3：Commit**

```bash
git add src/WebApp/wwwroot/app.js
git commit -m "feat: refactor toggleMobileView for mini-map layout"
```

---

## 完成檢查清單

- [ ] Loading：切換分頁時 badge 有 spinner，清單淡出
- [ ] Loading：套用篩選（all listings）有 badge spinner
- [ ] Loading：「不考慮」/ 恢復按鈕有內嵌 spinner，disabled
- [ ] Loading：Modal 加入/移除按鈕有內嵌 spinner，disabled，Modal 留著直到完成
- [ ] 行動版：小地圖（110px）+ 清單為預設佈局
- [ ] 行動版：「⛶ 展開」切全螢幕，「📋 清單」切回
- [ ] 行動版：點卡片只開 modal，小地圖不動
- [ ] 桌機版：完全不受影響
