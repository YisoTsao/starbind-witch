# WORLD_DESIGN.md — 世界與區域設計（World & Visual Overhaul）

> 版本 v1（2026-11-26）｜上位：`GAME_DESIGN_V3.md`、`WORLD_STRUCTURE.md`、`STORY_WORLD_QUESTS.md`
> 視覺基準：`references/01_moonveil_forest_overview.png`～`04_moonveil_forest_props.png`（實際位置在 `references/`，不是 `docs/references/`）
> 相關：`WORLD_GENERATOR_SPEC.md`（產生器）、`BIOME_DESIGN.md`、`LEVEL_DESIGN.md`、`MISSING_ASSETS.md`

## 1. 現況盤點（2026-11-26 起點）

| 範圍 | 已有 | 本次是否改動 |
|---|---|---|
| 控制/相機 | Input System、`PlayerMotor` 八方向、Dash、`CameraFollow2D`、Pixel Perfect 480×270、PPU 32 | 不改 |
| 戰鬥 | 法杖三連、蓄力魔彈、Q/E、自動槽、R 超載、`AbilityRunner`/`HitResolver`/`DamageResolver`、元素反應（預算、深度上限）、Pool | 不改 |
| 構築/局外 | 三頁魔典、升級卡、研究桌、任務、JSON 存檔（校驗＋備份） | 不改 |
| 世界 | 村莊 48×40、工坊、月影森林 64×64、第 2 章～終章四場景；地圖由 `tools/map/*.py` 產 JSON，`GameContentBuilder` 建場景 | **擴充**：新增 Witch World Studio 與紫幻森林區域；舊場景保留 |
| 地形 | `VariantTile`、`NeighborMaskTile`（256 遮罩，拼接測試 PASS） | 重用；新增 `PeriodicTile` |
| UI | uGUI HUD（左上狀態、右上區名/目標/小地圖、下方技能列、Boss 條） | **調整**右上區名面板 |
| 美術 | 全部為程式草稿或從參考圖擷取的 AI-derived 草稿 | 新增紫幻森林 REF-DRAFT 素材 |

測試基線（本次開始前，`PROJECT_STATE.md`）：EditMode 67/69（2 例既有灰盒預覽失敗）、PlayMode 70/70（含 Evidence）。

## 2. 目前畫面 vs 四張參考圖的差距

以 `docs/evidence/final/F00_forest_overview_native_2048.png`、`F01_forest_entrance_hud.png`（舊月影森林）對照：

| 面向 | 舊畫面 | 參考圖 | 差距等級 |
|---|---|---|---|
| 色調 | 白天亮綠草＋綠/藍圓樹 | 暮色紫：紫色樹冠、暗藍綠林地、暖燈點綴 | 大 |
| 地形 | 單一草地、階梯狀直角土路、矩形河道 | 土路有草邊蓋、石板廣場、星光水面＋石岸、懸崖、瀑布 | 大 |
| 森林牆 | 稀疏樹木散佈在草地上，看得到地面 | 樹冠完全閉合，邊緣灌木與花叢分層 | 大 |
| 地標 | 石柱與祭壇圓盤 | 月影神龕拱門、古樹、巨大蘑菇、水晶群、帳篷營地、遺跡拱門 | 大 |
| 光 | 全域光，少量點光 | 吊燈、蘑菇、水晶、營火局部暖/冷光 | 中 |
| 密度 | 空地大片無細節 | 草地上有小花、碎石、蕨類；但戰鬥區仍保持乾淨 | 中 |
| 比例 | 魔女 ≈1.25 tile，視野 15×8.4 tiles | 圖 02 魔女 ≈34 美術像素，視野約相同 | 小（已一致） |
| HUD | 版面與圖 02 已相近；右上靠右排列、無裝飾 | 右上置中金色標題「✦ 區名 ✦」＋「◆ 目標 0/1」 | 小 |

**結論**：比例、鏡頭、HUD 版面已接近；差距集中在「地形語彙、森林牆、地標物件、色調與光」。這些正是可以做成可重用 Tile/Prop/規則的部分，因此本次以產生器＋素材管線處理，而非手刷單一場景。

## 3. 四張參考圖 → 系統與資產需求

| 參考 | 定義什麼 | 對應系統 | 對應資產 |
|---|---|---|---|
| 01 Overview | 區域拓樸：南入口→中央祭壇→東北懸崖上的首領空地；西側湖與營地、西北菇谷、北方古樹、東側水晶與採集林地；主路＋支線＋環線；兩條水系（湖出水、瀑布河）與橋 | `RegionBlueprint`（POI 座標、道路圖、水系、懸崖）、`WorldGenerator`、`Validate` | 地標 Prefab（拱門、神龕、帳篷、水晶、巨菇、古樹）、橋、石階 |
| 02 Gameplay | 鏡頭距離、角色比例、戰鬥區留白、道路寬 3 格、邊緣層次、局部光、HUD 版式 | Pixel Perfect 480×270（不變）、`PropRule.roadClearance/clearCore`、Light2D 上限、`Hud` | 光源 Prefab、HUD 框與圖示 |
| 03 Tileset | 地形種類：亮草、暗草、土路、石板、星光水面＋石岸、懸崖立面（含藤蔓）、樹根、地面細節 | Tilemap 圖層（GroundBase/Dark/Water/Path/Paver/Detail/Cliff）、`NeighborMaskTile` 256、`PeriodicTile` | 8 張地形表（見 `ASSET_CATALOG.md`） |
| 04 Props | 物件目錄：6 種樹、枯木/倒木/樹根、4 種灌木、4 種岩石、4 種花、4 種菇、3 種水晶、蘆葦、荷葉、石柱×3、拱門、神龕、石圈、4 種燈、路牌、木橋、帳篷、營火、商車 | `BiomeDefinition.catalog`、`PropRule`、`PoiStamp`、OcclusionFader、Light2D | 45 個物件精靈與 Prefab |

## 4. 世界結構

- 世界由多個**區域（Region）**組成，每個區域＝一個 Unity 場景、一份 Blueprint、一個 Biome。
- 區域之間以 `AreaExit`（地圖邊緣）或星盤選關連結，沿用 `SceneTransition` 與 `SpawnPoint`。
- 區域可以「執行既有章節」：`RegionBlueprint.chapterId` 指向 `ChapterDefinition`，RunManager、SpawnDirector、Boss 流程不需修改；區名、開場與目標文字由 `SceneConfig.runIntro/runObjective` 覆蓋。
- 一個 Biome 可以服務多個區域（例：紫幻森林外圍、紫幻森林深處）。

### 4.1 區域清單（規劃）

| 區域 | Biome | 狀態 | 備註 |
|---|---|---|---|
| 紫幻森林 Moonveil Forest | moonveil | **第 1 章正式地圖**（REF-DRAFT 美術），`Scenes/Regions/MoonveilForest.unity` | 村莊南路與星盤都通往這裡；舊月影森林已退役（`Scenes/Dev/Legacy/Forest_v1.unity`） |
| 紫幻森林深處／古樹聖域 | moonveil | 規劃 | 同 Biome，新 Blueprint |
| 星核礦坑、星輝湖、失落觀星台、黑月城塞 | 各自 | 已有手作 JSON 場景 | 之後可遷移為 Studio Biome（需各自素材表） |

### 4.2 紫幻森林在主線的位置

**已決定（2026-11-26，DECISIONS WVO-1）：採方案 A。** 紫幻森林取代月影森林成為第 1 章：`ChapterDefinition forest.scene = MoonveilForest`、標題「紫幻森林」、Boss 區名「首領空地」；村莊南路出口、星盤、目標追蹤、Benchmark 預設都指向它。舊月影森林的兩個機關（斷橋拉桿、可燒藤蔓捷徑）已移植。故事文件中第 1 章的「月影森林」一律讀作「紫幻森林」（祭壇名「月影祭壇」保留）。

以下為當初的選項紀錄：

方案 A（建議）：試玩通過後，以 MoonveilForest 取代 `Forest.unity` 作為第 1 章地圖（村莊南路出口改指向 MoonveilForest，`ChapterDefinition.forest.scene` 改名），舊 Forest 移到 `Scenes/Dev/`。需同步更新 FlowPlayTests／CombatPlayTests 中寫死的森林座標。
方案 B：紫幻森林作為第 1 章之後的「自由探索區」，有獨立任務與 Boss。需新敵人與 Boss 美術（見 `MISSING_ASSETS.md`）。

（第一輪未改村莊出口；第二輪依使用者指示完成取代，所有 PlayMode 測試已改寫並通過 75/75。）

## 5. 探索節奏（以圖 01 為準）

| 階段 | 位置 | 體驗 | 敵人分層 |
|---|---|---|---|
| 進入 | 森林入口（南） | 石門、路燈、告示牌；安全區 | Z01 |
| 分岔 | 中央岔路 | 主路北上；西往隱藏營地（安全、寶箱）；東往採集林地（花叢、寶箱） | Z02 |
| 中心 | 月影祭壇 | 石板廣場＋神龕；F 啟動祭壇怪潮 | Z03 |
| 支線 | 西：湖畔（過橋）→ 蘑菇幽谷；北：古樹密林 | 景觀與寶箱；菇谷/古樹以環線相連 | Z03–Z04 |
| 收束 | 東：過瀑布河橋 → 岔路 → 水晶林地 / 懸崖石階 | 瀑布地標、石階是唯一上高台的路 | Z04 |
| 高潮 | 首領空地（東北高台） | 遺跡拱門、石柱圍成的圓形石板競技場；踏入觸發菇王、石階閘門關閉 | Z05 |

## 6. 開發順序建議

1. （本次完成）Studio 架構、紫幻森林 v1、HUD 調整、驗證與測試。
2. 使用者實機試玩紫幻森林並決定 §4.2 方案。
3. 正式美術第一批：地形 8 表＋森林牆 4 種樹＋邊緣灌木（影響畫面最大）。見 `MISSING_ASSETS.md` P0。
4. 產生器 v2：東西向懸崖、直向橋、空地邊緣次格平滑、執行期 culling 與區塊淡入。
5. 紫幻森林專屬敵人、Boss、任務線；之後把其他章節遷移到 Studio。
