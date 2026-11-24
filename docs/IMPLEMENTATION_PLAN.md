# IMPLEMENTATION_PLAN.md — 星痕魔女 v3.0

> 本表是計畫，不代表任何階段已完成。請 Claude Code 在實作後更新每一項狀態和測試證據。
> 2026-11-21：依使用者指示一次實作 Phase 1–5 的全部系統（SPEC_CONFLICTS C-20），證據見 `docs/FULL_BUILD_REPORT.md`、`docs/evidence/final/`。

| Phase | 必做內容 | 完成定義 | 狀態 |
|---|---|---|---|
| 0 基礎 | Unity 2D URP、Input、Pixel Perfect、2D Tilemap、碰撞、相機跟隨 | 小屋外地圖可遊走，無穿牆 | **系統完成、美術為草稿**（2026-11-21；EditMode 9/9、PlayMode 14/14；見 `docs/PHASE0_REPORT.md`） |
| 1 核心戰鬥 | 法杖三連、蓄力魔彈、Dash、火球、史萊姆、HP/MP、死亡重開 | 能連續操作戰鬥 3–5 分鐘 | **系統完成、美術為草稿**（2026-11-21；見 `docs/FULL_BUILD_REPORT.md`）。手感（3–5 分鐘連續遊玩）尚未由真人試玩驗收 |
| 2 法術/Auto | 冰雷、旋風、元素反應、兩自動槽 | 反應正確、無無限觸發 | **系統完成**：4 反應（蒸汽爆裂/超載爆燃/碎晶雷暴/火焰龍捲），保護期、每秒預算、深度上限、目標上限 8，皆有 EditMode/PlayMode 測試 |
| 3 構築 | 三頁魔典、6 種白名單法術、15 張升級卡 | 三種不同流派可驗證 | **系統完成**：9 個白名單法術（6 組 GDD 組合＋旋風、追蹤魔彈、星火）、15 張卡、魔典台配置、Tab 構築頁。三流派的「實際可玩差異」尚未由真人試玩驗收 |
| 4 地圖/怪潮 | 連續森林、4 敵、精英、事件、怪潮 | 可完整探索、最多同屏60敵目標可測 | **完成（草稿美術）**：64×64 森林 Z01–Z05、4 敵＋精英、祭壇怪潮、寶箱、藤蔓、斷橋；60 敵上限與 macOS Player 實測（見報告） |
| 5 Boss/局外 | 菇王三階段、超載、戰利品、研究、存檔 | 10–12 分鐘完整一局、勝敗可回村 | **系統完成**：三階段菇王、超載、結算、研究桌、JSON 存檔（校驗＋備份）。10–12 分鐘節奏未經真人完整試玩 |
| 6 美術/發佈 | 正式 Tile/角色/怪物/VFX/UI/音效、效能、Windows Build | 可獨立啟動與實際遊玩、測試報告 | **未完成**：全部為程式產生草稿；已有 macOS Player Build 與效能數據；Windows 模組未安裝 |

## 2026-11-24 續作

已建立五章 L1 灰盒與獨立試走場景。正式 Demo 保留原本三個 Build 場景。首章回歸現涵蓋出村、森林菇王、勝利回村及磁碟重讀；手感與 G4 待人工驗收。最新階段依據見 [專案狀態](PROJECT_STATE.md) 與 [製作清單](PRODUCTION_BACKLOG.md)。

## 實作任務模板

每一項工作請維持以下欄位：`ID`、`範圍`、`依賴`、`驗收操作`、`自動測試`、`相關資產`、`狀態`、`測試證據`。

### P0-01 場景與相機
- Scope：建立 Bootstrap、Workshop、Forest、VFXPlayground 場景；攝影機與 Unity Input System。
- Acceptance：WASD 可移動、相機平滑跟隨、不超出地圖邊界。
- Evidence：Play Mode 操作與 Game View 截圖。

### P0-02 Tilemap 與碰撞
- Scope：Ground、Path、Water、Cliff、Props、Foreground 分層。
- Acceptance：橋可走、河不可走、樹幹碰撞、樹冠可遮擋玩家。
- Evidence：Inspector 中 Tilemap 層與 Collision 示意。

### P1-01 法杖與魔彈
- Scope：三段連擊、短/長按輸入、蓄力投射物、傷害事件。
- Acceptance：每段命中有對應數值、不重複命中、第三段有反饋。
- Evidence：測試報告與 Debug Combat Log。

### P2-01 元素反應
- Scope：Fire/Ice/Lightning、Burn/Chill/Shock、ReactionResolver、事件上限。
- Acceptance：火冰、火雷、冰雷三種反應，次生反應不可無限觸發。
- Evidence：EditMode 測試與 Play Mode 場景。

### P3-01 法術構築
- Scope：形態/元素/修飾 SO、SpellFactory、白名單、升級三選一。
- Acceptance：6 組測試配置正確施放，禁止不支援組合進入抽卡池。
- Evidence：單元測試及裝配 UI 截圖。

### P4-01 怪潮
- Scope：SpawnDirector、區域觸發、物件池、怪數上限、精英。
- Acceptance：30/50/60 隻壓測，實際 FPS 與 GC Alloc 報告。
- Evidence：Unity Profiler 記錄。

### P5-01 10 分鐘垂直切片
- Scope：出生、工坊、森林、怪潮、菇王、通關返回、研究存檔。
- Acceptance：新使用者可依教學獨立完成單局，無阻塞性錯誤。
- Evidence：Windows Build、測試清單、已知問題。
