# BIOME_DESIGN.md — Biome 設計

> 版本 v1（2026-11-26）｜資料：`Assets/_Game/Data/World/<Biome>/BIO_*.asset`（`BiomeDefinition`）
> 產生方式與欄位定義見 `WORLD_GENERATOR_SPEC.md` §3。

## 1. Biome 的組成

一個 Biome = **地形角色 Tile** ＋ **物件目錄（catalog）** ＋ **散佈規則（rules）** ＋ **森林牆樹種** ＋ **光與色**。
Biome 不知道任何座標；座標屬於 RegionBlueprint。因此同一 Biome 可產生多個區域。

| 地形角色 | Tile 類型 | 說明 |
|---|---|---|
| ground | `PeriodicTile`（4×4 片＝128 px 週期） | 主草地，消除 32 px 重複感 |
| darkGround | `NeighborMaskTile` Full8 | 森林地面／樹冠陰影；邊緣為交錯點陣過渡 |
| path | Full8 | 土路，草葉蓋邊 |
| paver | Full8 | 遺跡石板，邊緣苔蘚、缺石 |
| water | Full8 | 水面＋石岸環＋潮濕草緣 |
| cliffFace / cliffWaterfall / cliffRim | EastWestNorth / NorthEastWestOpen | 2 格高立面、瀑布、高台緣 |
| details[] | `VariantTile`（各 2 變體） | 透明地面細節，依 `DetailKind` 對應 |

## 2. 紫幻森林 Moonveil（`BIO_moonveil`）

### 2.1 色彩與光

| 項目 | 值 | 依據 |
|---|---|---|
| 調色來源 | 草、暗草、土、水、石板、岩各自從 `references/03` 以 k-means 取 6–8 色；草地提亮約 16–20% | 圖 02 的草地比圖 03 的素材表亮 |
| 物件調色盤 | 45 個物件共用 64 色（從 `references/04` k-means） | 跨物件一致 |
| 全域光 | 色 (0.94, 0.90, 1.0)、強度 1.0 | 微紫但不壓暗；第一版 0.8 太暗，已在實機截圖後調整 |
| 背景色 | `#120E1E` | 地圖外 |
| 角色暗化 | 0.06 | 角色在暗林地上仍可讀 |
| 環境光點 | 34 個螢火蟲／魔力光點（`AmbientMotes`） | |
| 局部光 | 吊燈樹、路燈、石燈、營火、神龕燭火、巨菇、水晶；每區 ≤56 個 Light2D | 圖 01/02 的暖燈與冷光 |

規則：素材本身不烘焙夜色，夜色由 Global Light 決定（沿用 `ART_DIRECTION.md` §7）。

### 2.2 森林牆

| 樹種 | 權重 | 備註 |
|---|---|---|
| tree_purple_a / tree_purple_b | 3 / 3 | 主體紫樹 |
| tree_wisteria | 2.2 | 藍紫紫藤，打破色塊 |
| tree_pine | 1.3 | 深藍針葉，增加輪廓變化 |
| tree_blossom | 1.0 | 淡紫花樹 |
| tree_lantern | 0.5 | 掛燈古樹（有光源，受光源上限控制） |
| tree_dead | 0.25 | 枯樹 |

樹距 2.7 格、錯位六角格＋抖動；距空地 >4 格的樹保留 75%（樹冠仍閉合但少 25% 物件）。只有靠邊的樹會淡出（OcclusionFader），深林樹不加碰撞器（由 Collision Tilemap 負責）。

### 2.3 散佈規則

| 規則 | 棲地 | 密度/100格 | 間距 | 物件 | 偏好 |
|---|---|---|---|---|---|
| edge_undergrowth | ForestEdge | 70 | 1.15 | 灌木×4、薰衣草、鈴蘭、苔岩 | — |
| clearing_rocks | Clearing | 2.0 | 3 | 大岩、岩堆、苔岩、小岩 | 離路 ≥2 |
| clearing_flowers | Clearing | 4.0 | 2 | 粉花、紫花、薰衣草、鈴蘭、花叢 | 採集林地、湖畔 ×3 |
| clearing_deadwood | Clearing | 0.7 | 4 | 空心倒木、樹根 | 離路 ≥3 |
| mushrooms | Clearing | 0.8 | 2.2 | 雙菇、高菇、層菇、巨菇 | 菇谷、古樹 ×9 |
| crystals | Clearing | 1.2 | 2.4 | 小晶、紫晶、藍晶 | **只在**水晶林地 13 格內 ×9 |
| ruins | Clearing | 0.35 | 4 | 斷柱、殘柱、高柱 | 祭壇、首領空地、水晶林地 ×5 |
| shore | Shore | 13 | 2.2 | 蘆葦、苔岩、鈴蘭 | — |
| lilies | WaterSurface | 4 | 3 | 荷葉 | 距岸 ≥2 |

共同限制：不壓路（碰撞矩形不得覆蓋土路/橋）、不進 POI 核心（`clearCore`）、不在石階 2 格內、不貼入口出生點。

### 2.4 地面細節

小花叢覆蓋率 0.17（低頻雜訊塊狀出現，不均勻撒點）、苜蓿 5%、路邊碎石 18%、菇谷發光小菇 22%、花瓣零星。只出現在亮草上，不在路、石板、暗林地上。

## 3. 新 Biome 檢查表

1. 美術 manifest（地形 8 表＋物件表）通過拼接與開放邊檢查（`moonveil_art.py` 的 `check_seams`／`check_open_borders`）。
2. 每個物件都有：pivot 在接地點、碰撞腳印（或明確無碰撞）、是否淡出、光源。
3. 森林牆至少 4 種樹，色相/明度有區別。
4. 至少一條 Clearing 規則、一條 ForestEdge 規則、一條水岸規則。
5. 全域光在實機截圖中檢查：角色、敵人、敵方預警的亮度層級符合 `ART_DIRECTION.md` §5.2。

## 4. 規劃中的 Biome

| Biome | 用途 | 地形差異 | 新物件 |
|---|---|---|---|
| moonveil_deep（紫幻深處） | 古樹聖域 | 更多暗林地、根系地面 Tile、發光苔 | 巨型古樹（多部件）、根橋、靈火 |
| 其他章節 | 遷移舊場景 | 依 `chapter_art.py` 現有色盤 | 見 `MISSING_ASSETS.md` §6 |
