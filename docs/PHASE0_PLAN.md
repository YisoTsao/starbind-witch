# PHASE0_PLAN.md — 第一個 Unity 可玩場景

> 建立：2026-11-21｜對應 `IMPLEMENTATION_PLAN.md` Phase 0（P0-01、P0-02）
> 完成定義（沿用）：**小屋外地圖可遊走，無穿牆**；Unity Play 能移動、鏡頭跟隨、碰撞正確；附 Game View 截圖。
> 不包含：戰鬥、法術、敵人、UI 數值、存檔。

---

## 1. 目標場景

**Village 北側區塊**＝圖 A 的可玩版本（約 24 × 16 tiles，位於 Village 48×40 的北中），加上通往 Workshop 的門。

驗收要能做到：
1. 魔女在出生點出現，WASD 八方向移動，4 方向走路動畫。
2. 鏡頭平滑跟隨、像素完美、不超出地圖邊界。
3. 無法穿過：小屋牆、柵欄、樹幹、岩石、水面、懸崖立面。
4. 可以走：草地、土路、石階、橋。
5. 走到屋頂、樹冠後方時被遮住，且遮擋物淡出。
6. 在小屋門口按 F 進入 Workshop（空房間＋牆碰撞即可），再按 F 出來回到門口。

---

## 2. 美術策略（**已決定：選項 C**，見 SPEC_CONFLICTS C-05）

| 選項 | 做法 | 優點 | 風險 |
|---|---|---|---|
| **A. 美術先行** | 先完成 P0 美術包（下表），再搭場景 | 第一次看到的場景就是正式風格 | 程式被美術進度卡住；需要像素美術人力 |
| **B. 程式先行** | 用明確標示的 Placeholder（`_Placeholder/`，不進 Atlas，Build 時檢查報錯）先完成全部 P0 系統 | 系統可立即驗收 | 與本次「不要用簡陋素材」指示衝突，需使用者明確同意 |
| **C. 並行（建議）** | 程式只用**除錯碰撞 Tile**（編輯器工具，不是美術）驗證移動/碰撞/鏡頭；同時製作 P0 美術包；美術到位後組成正式 Village 北區 | 不阻塞、不以簡陋素材冒充美術 | 需要一個 Placeholder 魔女方塊才能測移動（最小範圍，標示清楚） |

### P0 最小美術包（選 A 或 C 時）

| 資產 | 數量 | 來源 |
|---|---|---|
| 草地 Random Tile | 4–6 | 新繪，參考圖 A |
| 土路 Rule Tile | 47 | 新繪 |
| 水＋草岸 Rule Tile | 47（先靜態，動畫後補） | 新繪 |
| 懸崖 | ~16 | 新繪 |
| 石階、木橋 | 3 + 3 | 新繪 |
| 小屋外觀（模組） | 1 組 | 圖 A/B/C 底稿重繪 |
| 粉花大樹、綠樹 | 2 | 圖 A/B 底稿重繪（樹幹/樹冠分件） |
| 柵欄、岩石 ×2、木桶、木箱、路牌、小花 ×4 | 約 10 | 底稿重繪 |
| 魔女 Idle + Walk × 4 方向 | 40 幀 | 新繪，造型依圖 A |

---

## 3. 工作項目

| ID | 範圍 | 依賴 | 驗收操作 | 自動測試 | 狀態 |
|---|---|---|---|---|---|
| P0-00 | 決定 Unity 版本（C-08）、安裝 Windows Build Support；`git init` + `.gitignore`（Unity）+ Git LFS | 使用者決定 | Unity Hub 可開啟專案 | — | 部分完成：版本已決定、Git 已建立；Windows 模組與 LFS 未安裝 |
| P0-01 | 以 Universal 2D 範本在**本資料夾根目錄**建立 Unity 專案（保留現有 `Assets/_Game` 結構與 docs/references） | P0-00 | 專案開啟無 Console 錯誤 | — | 完成 |
| P0-02 | 套件：Input System、Tilemap Extras、Cinemachine、Test Framework；移除不需要的範本套件 | P0-01 | `Packages/manifest.json` 檢查 | — | 完成（未用 Cinemachine，C-18） |
| P0-03 | Project Settings：Gravity 0、Custom Axis 排序、Sorting Layers、Physics Layers、碰撞矩陣 | P0-01 | 截圖 Settings | EditMode：Sorting Layer 與 Physics Layer 名稱存在 | 完成 |
| P0-04 | `PixelArtImportRules` 匯入規則 | P0-01 | 丟入測試 PNG 自動 PPU32/Point/None | EditMode：匯入設定斷言 | 完成 |
| P0-05 | Bootstrap 場景、`GameBootstrap`、`SceneTransitionService`、`SpawnPoint` | P0-03 | Play 自動載入 Village | PlayMode：載入後玩家位於 `spawn_square` | 部分完成：轉場與出生點完成；Bootstrap 未建立 |
| P0-06 | Input Actions（Move / Interact / Pause）+ `PlayerInputReader` + `PlayerMotor` | P0-05 | WASD 八方向，斜向不加速 | EditMode：方向正規化；PlayMode：2 秒移動距離 ≈ 9 units ±5% | 完成 |
| P0-07 | Camera Rig：Pixel Perfect Camera + Cinemachine Follow + Confiner2D | P0-06 | 1920×1080 下 4× 整數縮放；邊界不露出地圖外 | PlayMode：玩家走到角落時 Camera 視口在 Bounds 內 | 完成 |
| P0-08 | Grid + Tilemap 階層（WORLD_STRUCTURE §5）、Collision/Markers 除錯 Tile | P0-03 | Scene View 可見碰撞格；Play 時不可見 | PlayMode：對牆/水推 2 秒位置不越界 | 完成 |
| P0-09 | Y 排序 + `OcclusionFader` | P0-08 | 繞樹、繞屋一圈截圖 | PlayMode：玩家在樹冠下時 Canopy alpha < 0.5 | 完成 |
| P0-10 | `Interactable` / `PlayerInteractor`；門 → Workshop → 返回 | P0-05, P0-08 | F 進出，回到門口出生點 | PlayMode：往返後 SpawnPointId 正確 | 部分完成：互動與轉場完成；Workshop 場景未建立 |
| P0-11 | ArtQA 場景（身高尺、32 px 網格） | P0-04 | 放入新素材並排截圖 | — | 未開始（改用 07 比對圖與 PlayMode 擷圖驗證） |
| P0-12 | 美術包匯入、Rule Tile 設定、Prefab（依選項 A/C） | 美術交付 | Seam Test 通過（ASSET_PIPELINE §6） | EditMode：Tile 尺寸 32 倍數 | 草稿完成（正式美術未開始） |
| P0-13 | 組合 Village 北區（圖 A 對應布局） | P0-08–P0-12 | 與圖 A 並排截圖；驗收清單 §1 全部項目 | PlayMode 全部通過 | 草稿完成（28×20 北區） |
| P0-14 | 交付報告：修改檔案、測試結果、截圖 `docs/evidence/phase0/`、已知問題；更新 IMPLEMENTATION_PLAN 狀態 | 全部 | — | 測試 XML | 完成：見 PHASE0_REPORT.md |

**順序：** P0-00 → 01 → 02/03/04 → 05 → 06 → 07/08 → 09/10 → (11, 12 與程式並行) → 13 → 14

---

## 4. 不做

- 戰鬥、Dash、法術、敵人、HUD 數值、存檔、Forest 場景內容（只保留空場景檔）。
- 不把圖 A 放進場景當背景或描圖底圖（若需對位，只在 Scene View 用半透明參考層，且不進 Build）。
- 不宣稱「達到參考圖品質」，除非有 Game View 截圖並排比較。
