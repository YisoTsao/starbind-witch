# SPEC_CONFLICTS.md — 規格衝突與待決定事項

> 建立：2026-11-21｜依 CLAUDE.md「如規格衝突，列出差異」建立。
> **原始規格檔未修改。** 每列的「建議」需使用者確認後，才回寫 `GAME_DESIGN_V3.md`（並記入其 §20 版本表）。

| ID | 衝突 | 來源 A | 來源 B | 建議 | 狀態 |
|---|---|---|---|---|---|
| C-01 | **魔女身高** | GDD §3.1：角色高約 48–64 px，Canvas 96×96 | 圖 A 實測魔女約 33 px（≈1 tile）；使用者指定圖 A 為最重要視覺參考 | 取 **36–44 px（建議 40）**，畫布 64×64，大動作 96×96。理由：480×270 下 64 px 角色佔畫面高度 24%，60 隻敵人同屏會過擠；40 px 仍比圖 A 略大，保留表情與帽子辨識 | **已決定：40 px**（使用者 2026-11-21）。目前草稿魔女仍是圖 A 的約 33 px，正式角色依 40 px 繪製 |
| C-02 | 小屋互動鍵 | GDD §3.4「門口互動（E）」 | GDD §4.1、CLAUDE.md：**F 互動**、E 為法術 | 採 **F**（GDD 自己在 §4.1 已註記統一） | 建議採用 |
| C-03 | 本階段範圍 | GDD §18 第一個任務含 Phase 1 起點（法杖、火球、史萊姆、HP/MP） | CLAUDE.md「只完成 Phase 0」；本次指示「暫不做戰鬥」 | 以本次指示為準：**只做 Phase 0** | 已依此執行 |
| C-04 | 非整數倍螢幕 | 480×270 參考解析度 | 1440p = 5.33×、Steam Deck 1280×800 = 2.67× | Pixel Perfect「Upscale Render Texture」＋黑邊（整數倍：1440p 用 5×=2400×1350，Deck 用 2×=960×540 置中）。替代：允許非整數縮放。**已決定（使用者授權由 Claude 決定）：整數倍縮放、不加黑邊、可見範圍略為擴大**（Pixel Perfect Camera Crop Frame = None）。1080p 精確 480×270；1440p 5× 時可見 512×288；4K 8×；Steam Deck 2× 時可見 640×400。理由：像素永遠清晰、沒有黑邊，代價是大螢幕能看到稍多地圖（單人遊戲可接受）。若之後 Deck 實測覺得視野過大，再改為 Windowbox | 已決定 |
| C-05 | 灰盒 vs 禁止簡陋素材 | GDD §15 Phase 0/1「灰盒與 Placeholder」；CLAUDE.md 允許「標示過的 Placeholder」 | 本次指示：不要**自行**用低品質方塊、隨機顏色取代正式美術 | 見 `PHASE0_PLAN.md` §2 選項 | **已決定：選項 C**。畫面全部使用標示 draft 的草稿，不宣稱為正式美術 |
| C-06 | 資料夾結構 | GDD §13.1：`Art/{…,Environment,…}`、`Prefabs/{…,Rooms}`、`docs/CLAUDE.md` | 本次指示：`Art/{Environments, Props,…}`、根目錄 `CLAUDE.md` | 合併兩者：`Art/Environment`（單數，依 GDD）＋ `Art/Props`；`Prefabs/World` 取代 `Rooms`（避免暗示房間制）；`CLAUDE.md` 留在**根目錄**（Claude Code 自動讀取位置） | 已依此建立 |
| C-07 | 場景清單 | IMPLEMENTATION_PLAN P0-01：Bootstrap / Workshop / Forest / VFXPlayground | GDD §3.4：出生村莊含小屋、廣場、訓練場、溪橋 | 新增 **Village**（村莊戶外），Workshop 專指小屋室內 | 待確認 |
| C-08 | Unity 版本 | 本機僅安裝 6000.6.8f1 | Steam 長期維護一般選 LTS | 若 6000.6 不是 LTS，建議安裝目前的 LTS 版本再建專案；建立後再換版本成本高 | **已決定：使用 6000.6.8f1**（使用者） |
| C-09 | 圖 B/C 標示「32×32」 | 圖面文字 | 實測：B 地形塊約 47 px、C 地板約 57 px，且非整數縮放 | 不依圖面標示切割；地形全部新繪（見 ASSET_PIPELINE） | 事實記錄 |
| C-10 | 色調 | 圖 A 白天（平均亮度 0.57） | 圖 B 0.31（黃昏）、圖 C 0.24（夜） | 素材統一畫成白天中性色，晝夜/室內氣氛用 2D Light | 建議採用 |
| C-11 | 室內視角 | GDD：俯視 3/4，**不是斜 45°** | 圖 C 室內預覽與範例組合為斜視角 | 室內只取物件造型，房間依俯視 3/4 重新規劃 | 建議採用 |
| C-12 | 排序層命名 | GDD §3.1：Ground < Decal < Props < Characters < Canopy | GDD §3.2 Tilemap 列表；圖 C 圖層建議 Floor/Walls/Objects/Foreground | 統一為 `TECH_ARCHITECTURE.md` §4：Ground / GroundDecal / World（Props 與角色同層做 Y 排序）/ VFX / Telegraph / Foreground / WorldUI。Props 與 Characters **必須同層**才能互相 Y 遮擋 | 建議採用 |
| C-13 | 學派範圍 | 本次指示列出「六大魔法學派」 | GDD §5.2：Demo 只做火/冰/雷 | 依 GDD：六學派是長期設計，Demo 只做三個；不擴大 MVP | 依 GDD |
| C-14 | 檔案命名 | 使用者清單：`ChatGPT Image Nov 20, 2026, 10_49_27 PM.jpg`、`Starbind Witch Cottage Tileset.png`、`Starbind_Witch_Development_Spec_v3/` 資料夾 | 實際：已改名為 `forest_tileset_reference.jpg`、`cottage_tileset_reference.png`；規格已在 `docs/` 與根目錄，無 Spec 資料夾 | 沿用實際檔名；對照記錄於 ASSET_INVENTORY | 事實記錄 |
| C-15 | ASSET_GAPS 檔 | CLAUDE.md 指 `docs/ASSET_GAPS.md` | 只有 `ASSET_GAPS_TEMPLATE.md` | 已依模板建立 `ASSET_GAPS.md`，模板保留 | 已處理 |
| C-17 | 地形碰撞 | GDD §3.1：Tilemap Collider 2D + **Composite Collider 2D** | 6000.6.8f1 實測：Composite (Merge, Polygons) 對本地圖產生 0 條路徑，所有地形碰撞消失 | 改用每格獨立碰撞（TilemapCollider2D 不經 Composite）。行為契約相同（不可穿越），只差實作；之後 Unity 版本更新時可重測 | 已實作，待使用者確認 |
| C-18 | 鏡頭元件 | TECH_ARCHITECTURE 原案：Cinemachine 3 | 實作：自製 `CameraFollow2D`（平滑跟隨＋限制邊界）＋ URP Pixel Perfect Camera | 少一個套件依賴；需要鏡頭震動、多目標時再評估 Cinemachine | 已實作 |
| C-19 | 授權 | 三張參考圖為 AI 生成（使用者確認） | 草稿物件是從圖 A 擷取的 | 所有 `*_draft` 資產標記為 AI-derived，不得視為正式素材；Steam 上架需揭露 AI 生成內容。正式素材需人工重繪並記錄作者 | 已記錄 |
| C-16 | 文件職責重疊 | 本次新增 `ASSET_INVENTORY.md`（現有檔案） | `ASSET_GAPS.md`（缺口追蹤） | Inventory = 「有什麼、能不能用」；Gaps = 「要做什麼、做到哪」，互相連結 | 已處理 |

## 完整草稿版（2026-11-21）新增

| ID | 衝突 | 來源 A | 來源 B | 處理 | 狀態 |
|---|---|---|---|---|---|
| C-20 | **一次完成多個階段** | CLAUDE.md「一次只執行一階段」、GDD §17-3「禁止一個 Prompt 同時要求整個遊戲完成」 | 使用者 2026-11-21 明確指示：小屋室內、森林、48×40 村莊其餘部分、所有戰鬥功能與魔法效果一次做完、不要停 | 依使用者最新指示執行；每個系統仍附 EditMode/PlayMode 測試與實際畫面，未宣稱正式美術 | 已依使用者指示 |
| C-21 | Bootstrap 常駐場景 | WORLD_STRUCTURE §1：Bootstrap 場景 Additive 載入 | 實作 | 改為 `GameSession`（DontDestroyOnLoad，首次需要時自動建立）持有存檔、UI、背包；場景仍單獨載入。效果相同、少一個場景切換流程 | 已實作，待確認 |
| C-22 | 月蝕菇王 HP | GDD §10.3 未給數值 | 實作 | 初始設計值 2200（反應與超載期間約 3–4 分鐘），待實測調整 | 設計值 |
| C-23 | 自動槽數量 | GDD §6.1 初始 1 槽、Demo 需 2 槽同時裝配 | 實作 | 開局 2 槽（魔典台配置 1 個開局自動法術＋局內 1 個），Lv5 解鎖第 3 槽、Lv9 第 4 槽 | 設計值 |
| C-24 | 失控書頁 | GDD §5.6 四種、Demo 先選一張 | 實作 | 只做 U15 倒轉書頁（升級卡） | 依 GDD |
| C-25 | 「火焰龍捲」上限 | GDD §5.5 只說 3 秒移動火焰區 | 實作 | 同時最多 3 個；傷害為不可再觸發反應的次生傷害 | 已實作 |
| C-26 | 投射物與地形 | GDD 未定 | 實作 | 投射物飛越水面/懸崖格，但會被樹幹、建築等實體物件擋下；可受擊物件（木偶、敵人）由命中判定處理 | 已實作 |
| C-27 | Windows Build | GDD §19 首要 Windows | 本機沒有 Windows Build Support 模組 | 以 macOS Player Build 驗證可獨立啟動與效能；Windows 需安裝模組後重跑 `BuildMacPlayerBatch` 的 Windows 版本 | 未完成 |
| C-28 | 多個 MonoBehaviour 同一檔案 | Unity 序列化需要「一檔一個 MonoBehaviour、檔名=類別名」 | 初版把多個互動元件寫在同一檔，導致場景中元件遺失（Forest 斷橋拉桿） | 已拆為一檔一類別，並以 PlayMode 測試確認 | 已修正 |
