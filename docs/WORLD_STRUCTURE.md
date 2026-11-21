# WORLD_STRUCTURE.md — 魔女小屋／星露村／月影森林

> 版本：Phase 0 草案（2026-11-21）｜上位：`GAME_DESIGN_V3.md` §2.2、§2.4、§3.3、§3.4、§11
> 所有尺寸單位：tile（32 px = 1 Unity unit）。地圖原點為左下角 (0,0)。

---

## 1. 場景與連接總覽

```text
          ┌──────────────────────┐
          │  Bootstrap (常駐)     │  Input、Camera Rig、存檔、場景切換、HUD
          └──────────┬───────────┘
                     │ Additive 載入（同時只載一個 Gameplay 場景）
     ┌───────────────┼────────────────────────┐
     ▼               ▼                        ▼
┌──────────┐  F 門  ┌──────────────┐  邊界    ┌──────────────────────┐
│ Workshop │◀──────▶│   Village    │────────▶│        Forest         │
│ 魔女小屋  │        │   星露村      │◀────────│      月影森林          │
│ 室內      │        │ (安全區)      │  回村入口 │ Z01→Z02→Z03→Z04→Z05   │
└──────────┘        └──────────────┘          └──────────────────────┘
```

| 場景 | 檔案 | 類型 | 大小（初版） | 敵人 |
|---|---|---|---|---|
| Bootstrap | `Scenes/Bootstrap.unity` | 常駐 | — | — |
| Village 星露村 | `Scenes/Village.unity` | 手工 | **48 × 40** | 禁止 |
| Workshop 魔女小屋室內 | `Scenes/Workshop.unity` | 手工 | 14 × 10 | 禁止 |
| Forest 月影森林 | `Scenes/Forest.unity` | 手工骨架＋seed 配置 | **64 × 64**（可擴至 128×128） | 有 |
| ArtQA / VFXPlayground | `Scenes/Dev/*.unity` | 開發用 | — | 測試 |

> 命名說明：`IMPLEMENTATION_PLAN.md` P0-01 列出 Bootstrap / Workshop / Forest / VFXPlayground，沒有村莊場景；GDD §3.4 又要求出生村莊。本文件提案 **Village（村莊戶外）＋ Workshop（小屋室內工坊）** 兩個場景。見 `SPEC_CONFLICTS.md` C-07。

### 1.1 轉場規則

| 連接 | 觸發 | 表現 | 實作 |
|---|---|---|---|
| Village ↔ Workshop | 門口按 **F** | 0.25 秒淡出淡入 | `SceneTransitionService` 卸載/載入 + `SpawnPointId` |
| Village → Forest | 走進南側森林出口邊界觸發區 | 淡出淡入，不是戰鬥房間切換 | 同上；出口前有路牌提示 |
| Forest → Village | Z05 Boss 後的回村入口；或死亡結算後 | 結果頁 → 載入 Village `spawn_square` | `RunManager` |
| Forest 內部 | **無場景切換** | 連續走動 | 區域以 Trigger 標記 Z01–Z05 |

- 每個場景有命名出生點：`spawn_default`、`spawn_from_workshop`、`spawn_from_forest` 等。
- 轉場服務只依 `SpawnPointId` 找位置，不依座標寫死。

---

## 2. Village 星露村（48 × 40）

以圖 A 為**北側中央區塊**的樣貌，向外擴充 GDD §3.4 要求的功能區。

```text
 y=40 ┌────────────────────────────────────────────────┐
      │ 樹林邊界（不可走，樹冠遮擋）                      │
      │      ┌────────┐                                 │
      │ 花樹 │ 魔女小屋 │ 柵欄────────   紫帳篷攤位 花樹   │
      │      └──門────┘                                 │
 高台  │ 池塘 ┊   石階                                   │
 ≈y30 │  瀑布┊    │                                    │
      │ 懸崖 ┊   中央廣場 ★出生 ── 東路 ── 傳送星盤       │
      │ 下池 ┊   路牌        │              (水晶噴泉)    │
      │  ══橋══ ─ 西路       │                           │
      │ 弓箭手洛恩            │         騎士訓練場          │
      │ (溪流西岸)            │     箭靶・稻草人・凱恩     │
      │               祭司莉亞│                            │
      │                 南路 │                            │
      │ 懸崖────────────┐    │    ┌──────────── 懸崖      │
 y=0  └─────────────────┘ 森林出口 └─────────────────────┘
                         (往 Forest)
```

| 功能點 | 約略位置 (tile) | 互動 | 參考 |
|---|---|---|---|
| 魔女小屋（外觀） | 中北 (19–25, 31–36) | 門 F → Workshop | 圖 A 北中 |
| 中央廣場／出生點 `spawn_square` | (24, 24) | — | 圖 A 魔女站位 |
| 路牌 | (19, 23) | F 顯示方向 | 圖 A 中西 |
| 艾拉 NPC | 小屋門前 | F 對話 | GDD §2.3 |
| 傳送星盤（水晶噴泉） | 東 (38, 22) | F（Demo 無功能或回小屋） | 圖 A 東 |
| 紫帳篷攤位 | 東北 (33, 31) | 裝飾（Demo 不做商店） | 圖 A 東北 |
| 溪流＋瀑布＋池塘 | 西 (0–12, 14–34) | 不可走 | 圖 A 西 |
| 小橋 | 西 (8–10, 17) | 可走 | GDD §3.4；圖 A 無橋，需新繪 |
| 弓箭手洛恩 | 溪西 (4, 14) | F 對話 | |
| 騎士訓練場＋凱恩 | 東南 (32–42, 10–17) | 靶子、木偶（Phase 1 法杖教學） | 圖 B 箭靶/稻草人 |
| 祭司莉亞 | 南 (20, 12) | F 對話 | |
| 森林出口 | 南中 (22–26, 0–2) | 邊界觸發 → Forest | 圖 A 南路延伸 |

**規則**
- 全區安全區：`NoSpawn` 覆蓋整張地圖。
- 主路寬 2–3 tiles，所有功能點從出生點 20 秒內可走到（4.5 u/s × 20 s = 90 tiles 路程上限）。
- 地圖邊界以樹林與懸崖自然封閉，外圍 2 tiles 為 Camera 邊界外的填充。

---

## 3. Workshop 魔女小屋室內（14 × 10）

- 俯視 3/4：上方 2 tiles 為牆面（牆頂＋牆面），其餘為地板。**不使用圖 C 的斜視角房間構圖。**
- 鏡頭：室內小於 15×8.4 可見範圍時，Camera 置中鎖定、不跟隨。

```text
┌────────────────────────────┐
│ 書櫃  書櫃  圓窗  衣櫃   床 │  ← 牆面（不可走）
│ 研究桌            梳妝台    │
│                            │
│ 煉金台   ★魔典台   地毯     │
│ 大鍋                 餐桌椅 │
│                            │
└────────────門──────────────┘  → 出口 F 回 Village spawn_from_workshop
```

| 物件 | 功能（依 GDD） | Phase |
|---|---|---|
| 魔典台／研究桌 | 書頁裝配、研究節點 | 3 / 5 |
| 煉金台＋大鍋 | 藥劑配方 | 5 |
| 書櫃 | 裝飾／教學文本 | — |
| 床 | 存檔點（候選） | 5 |
| 門 | F 出門 | 0 |

---

## 4. Forest 月影森林（64 × 64）

手工骨架＋同一 seed 重現的怪群、資源點、事件（GDD §3.3、§11）。

```text
 y=64 ┌──────────────────────────────────────────────┐
      │                 Z05 菇王棲地                   │
      │               (Boss 圓形空地 ~14×12)           │
      │                     ▲ 回村入口                │
      │        Z04 斷裂石橋（單一路徑瓶頸、可燃藤蔓）  │
      │    ┌────────────────┴─────┐                   │
      │    │  隱藏區(藤蔓後)        │                   │
      │  Z03 星痕祭壇（怪潮競技空地 ~12×12，可繞行）     │
      │          ▲             ▲                      │
      │   Z02 蘑菇林道 ◀── 岔路 ──▶ 寶箱支線            │
      │          ▲                                    │
      │   Z01 森林入口（史萊姆、基礎掉落）              │
 y=0  └──────────▲───────────────────────────────────┘
          從 Village 森林出口進入 spawn_from_village
```
<!-- docs/evidence/final -->
| Zone | 區域大小（約） | 內容 | 戰鬥空間要求 |
|---|---|---|---|
| Z01 森林入口 | 16×14 | 史萊姆、教學掉落 | 主路寬 3+ |
| Z02 蘑菇林道 | 20×14 | 毒孢菇、第一寶箱、岔路 | 至少一條繞行路 |
| Z03 星痕祭壇 | 18×16 | 祭壇 F 觸發怪潮、書頁 | 開闊空地 ≥ 12×12 |
| Z04 斷裂石橋 | 14×10 | 瓶頸、可燃藤蔓捷徑、斷橋開關 | 瓶頸前後各有空地 |
| Z05 菇王棲地 | 18×16 | Boss | 圓形空地 ≥ 14×12，無遮蔽物 |

**規則**
- 怪物生成：只在 `Spawnable` 區域、畫面外、距離玩家 ≥ 9 tiles。
- 水域、懸崖、建築、樹幹格為 `Blocked`；`NoSpawn` 用於 Boss 入口與安全點。
- Zone 邊界以 Trigger Collider 標記，用於音樂、怪潮、小地圖區域名稱。
- 大型裝飾不得封死主路（GDD §3.3）。

---

## 5. 共用 Tilemap 結構（每個 Gameplay 場景）

```text
<SceneName>
├── Grid (Cell Size 1,1)
│   ├── GroundBase        Sorting: Ground / 0
│   ├── Water             Sorting: Ground / 10   (Animated Rule Tile)
│   ├── GroundTransition  Sorting: Ground / 20
│   ├── Path              Sorting: Ground / 30
│   ├── Cliff             Sorting: Ground / 40
│   ├── GroundDetails     Sorting: Ground / 50
│   ├── Collision         不渲染；TilemapCollider2D + CompositeCollider2D（權威碰撞）
│   └── Markers           不渲染；Spawnable / NoSpawn / ZoneId（編輯器顯示）
├── World                 Sorting: World（Y 排序）
│   ├── Props/            樹幹、柵欄、建築牆、岩石（Prefab）
│   ├── NPCs/
│   └── Interactables/
├── Foreground            Sorting: Foreground（樹冠、屋頂，OcclusionFader）
├── SpawnPoints/          spawn_* 空物件
├── Zones/                Z01..Z05 Trigger
└── CameraBounds          PolygonCollider2D（Cinemachine Confiner）
```

說明：GDD §3.2 的 `BlockingProps / Characters / Foreground / Effects / UI` 在此對應到 Sorting Layer 與 GameObject 階層，而非都做成 Tilemap；高大物件做成 Prefab 才能正確 Y 排序。詳見 `TECH_ARCHITECTURE.md` §5–§6。
