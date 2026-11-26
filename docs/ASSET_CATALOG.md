# ASSET_CATALOG.md — 紫幻森林資產目錄

> 版本 v2（2026-11-26 第三輪）｜由 `Assets/_Game/Art/Biomes/Moonveil/moonveil_manifest.json` 生成（`tools/art/moonveil_art.py`）
> **所有項目狀態皆為 REF-DRAFT**：調色取自 `references/03`、物件從 `references/04` 切出並縮放、量化（AI 概念圖衍生），僅供驗證系統、構圖與比例。正式版需逐項重繪，見 `MISSING_ASSETS.md`。
> 規格：PPU 32、Point、無壓縮、無 Mip；地形 32×32；物件透明 RGBA、Pivot 在接地點（底部中心往上 pivotY 像素）。

## 1. 地形表（8）

| 名稱 | 類型 | 切片 | 檔案 |
|---|---|---|---|
| `TILE_mv_grass_macro` | periodic | 4×4 | `Assets/_Game/Art/Biomes/Moonveil/Draft/TILE_mv_grass_macro.png` |
| `TILE_mv_dark_256` | mask256 | 16×16 | `Assets/_Game/Art/Biomes/Moonveil/Draft/TILE_mv_dark_256.png` |
| `TILE_mv_path_256` | mask256 | 16×16 | `Assets/_Game/Art/Biomes/Moonveil/Draft/TILE_mv_path_256.png` |
| `TILE_mv_paver_256` | mask256 | 16×16 | `Assets/_Game/Art/Biomes/Moonveil/Draft/TILE_mv_paver_256.png` |
| `TILE_mv_water_256` | mask256 | 16×16 | `Assets/_Game/Art/Biomes/Moonveil/Draft/TILE_mv_water_256.png` |
| `TILE_mv_cliff` | cliff | 10×1 | `Assets/_Game/Art/Biomes/Moonveil/Draft/TILE_mv_cliff.png` |
| `TILE_mv_rim` | rim | 8×1 | `Assets/_Game/Art/Biomes/Moonveil/Draft/TILE_mv_rim.png` |
| `TILE_mv_detail` | detail | 12×1 | `Assets/_Game/Art/Biomes/Moonveil/Draft/TILE_mv_detail.png` |

- mask256：索引 = 8 鄰遮罩（N=1 NE=2 E=4 SE=8 S=16 SW=32 W=64 NW=128）。四張皆通過「整圖對照」拼接測試（0 像素不一致）與「開放邊透明」測試（0 張）。
- cliff：[上排 中/左端/右端/單格][下排 同][瀑布 上/下]，2 格高；第二輪改為程式繪製的柱狀岩（PROC-DRAFT）。
- stairs（`TILE_mv_stairs`，8 片，同 cliff 排列）：嵌在崖面的石階（PROC-DRAFT）。
- 追加物件（非從圖 04 切出）：`PROP_mv_bridge_broken`（由木橋挖去中段，REF-DRAFT 衍生）、`PROP_mv_bridge_vertical`（96×150，PROC-DRAFT）、`PROP_mv_magic_circle`（160×160 地面貼花、pivot 置中，PROC-DRAFT）。
- detail：花（粉）×2、花（紫）×2、苜蓿×2、碎石×2、發光小菇×2、花瓣×2。

對應 Tile 資產（`Assets/_Game/Tiles/Moonveil/`）：`TL_mv_grass_macro`（PeriodicTile）、`TL_mv_dark_256`、`TL_mv_path_256`、`TL_mv_paver_256`、`TL_mv_water_256`（NeighborMaskTile Full8）、`TL_mv_cliff_face`、`TL_mv_waterfall`（EastWestNorth）、`TL_mv_rim`（NorthEastWestOpen）、`TL_mv_detail_*`（VariantTile ×6）。Sprite Atlas：`Art/Biomes/Moonveil/ATL_moonveil.spriteatlasv2`。

## 2. 物件（45＋3 追加）

縮放依類別（以圖 02 魔女 ≈34 px 為準）：樹 0.50、大型遺跡/營地 0.38–0.44、中型 0.34–0.38、小型 0.24–0.28。

| 精靈 | 類別 | 像素 | Tiles | pivotY | 碰撞腳印 (w×h tiles) | 淡出 | 光 | v1 地圖使用數 |
|---|---|---|---|---|---|---|---|---|
| `PROP_mv_tree_lantern` | tree | 164×138 | 5.1×4.3 | 2 | 1.2×0.5 | ✔ | ✔ | 95 |
| `PROP_mv_tree_purple_a` | tree | 128×130 | 4.0×4.1 | 2 | 1.1×0.5 | ✔ |  | 582 |
| `PROP_mv_tree_wisteria` | tree | 140×124 | 4.4×3.9 | 2 | 1.1×0.5 | ✔ |  | 380 |
| `PROP_mv_tree_purple_b` | tree | 124×126 | 3.9×3.9 | 2 | 1.0×0.5 | ✔ |  | 506 |
| `PROP_mv_tree_blossom` | tree | 110×112 | 3.4×3.5 | 2 | 0.6×0.4 | ✔ |  | 176 |
| `PROP_mv_tree_pine` | tree | 78×120 | 2.4×3.8 | 2 | 0.6×0.4 | ✔ |  | 225 |
| `PROP_mv_tree_dead` | tree | 100×63 | 3.1×2.0 | 2 | 0.8×0.4 | ✔ |  | 44 |
| `PROP_mv_log_hollow` | deadwood | 81×40 | 2.5×1.2 | 2 | 2.0×0.5 |  |  | 2 |
| `PROP_mv_stump_roots` | deadwood | 83×49 | 2.6×1.5 | 1 | 1.4×0.5 |  |  | 2 |
| `PROP_mv_bush_dark` | bush | 33×24 | 1.0×0.8 | 1 | — |  |  | 121 |
| `PROP_mv_bush_flower` | bush | 36×26 | 1.1×0.8 | 1 | — |  |  | 74 |
| `PROP_mv_bush_green` | bush | 37×25 | 1.2×0.8 | 1 | — |  |  | 42 |
| `PROP_mv_bush_berry` | bush | 34×25 | 1.1×0.8 | 1 | — |  |  | 69 |
| `PROP_mv_rock_large` | rock | 62×36 | 1.9×1.1 | 1 | 1.6×0.6 |  |  | 6 |
| `PROP_mv_rock_cluster` | rock | 47×27 | 1.5×0.8 | 1 | 1.2×0.45 |  |  | 1 |
| `PROP_mv_rock_mossy` | rock | 37×23 | 1.2×0.7 | 1 | 1.0×0.4 |  |  | 54 |
| `PROP_mv_rock_small` | rock | 30×13 | 0.9×0.4 | 1 | — |  |  | 7 |
| `PROP_mv_flowers_pink` | flower | 34×20 | 1.1×0.6 | 1 | — |  |  | 8 |
| `PROP_mv_flowers_violet` | flower | 35×21 | 1.1×0.7 | 1 | — |  |  | 12 |
| `PROP_mv_lavender` | flower | 33×25 | 1.0×0.8 | 1 | — |  |  | 50 |
| `PROP_mv_bellflower` | flower | 29×24 | 0.9×0.8 | 1 | — |  |  | 59 |
| `PROP_mv_mushroom_giant` | mushroom | 46×35 | 1.4×1.1 | 1 | 0.8×0.35 |  | ✔ | 10 |
| `PROP_mv_mushroom_pair` | mushroom | 27×23 | 0.8×0.7 | 1 | — |  | ✔ | 4 |
| `PROP_mv_mushroom_tall` | mushroom | 27×26 | 0.8×0.8 | 1 | — |  | ✔ | 3 |
| `PROP_mv_mushroom_shelf` | mushroom | 38×24 | 1.2×0.8 | 1 | — |  | ✔ | 4 |
| `PROP_mv_crystal_violet` | crystal | 46×37 | 1.4×1.2 | 1 | 0.9×0.4 |  | ✔ | 2 |
| `PROP_mv_crystal_blue` | crystal | 32×29 | 1.0×0.9 | 1 | 0.7×0.35 |  | ✔ | 2 |
| `PROP_mv_crystal_small` | crystal | 25×20 | 0.8×0.6 | 1 | — |  | ✔ | 5 |
| `PROP_mv_reeds` | shore | 34×30 | 1.1×0.9 | 1 | — |  |  | 4 |
| `PROP_mv_lilypads` | water | 47×20 | 1.5×0.6 | 3 | — |  |  | 6 |
| `PROP_mv_pillar_tall` | ruin | 27×61 | 0.8×1.9 | 2 | 0.8×0.4 | ✔ |  | 7 |
| `PROP_mv_pillar_short` | ruin | 21×33 | 0.7×1.0 | 2 | 0.8×0.4 |  |  | 1 |
| `PROP_mv_pillars_broken` | ruin | 78×49 | 2.4×1.5 | 3 | 2.0×0.5 |  |  | 2 |
| `PROP_mv_arch_ruin` | ruin | 82×68 | 2.6×2.1 | 2 | — | ✔ |  | 2 |
| `PROP_mv_shrine_moon` | ruin | 90×75 | 2.8×2.3 | 2 | — | ✔ | ✔ | 1 |
| `PROP_mv_stone_circle` | ruin | 70×38 | 2.2×1.2 | 2 | — |  |  | 0 |
| `PROP_mv_lamp_post` | light | 28×48 | 0.9×1.5 | 1 | 0.35×0.25 |  | ✔ | 9 |
| `PROP_mv_lamp_crook` | light | 27×48 | 0.8×1.5 | 1 | 0.35×0.25 |  | ✔ | 0 |
| `PROP_mv_lantern_hang` | light | 12×33 | 0.4×1.0 | 1 | — |  | ✔ | 0 |
| `PROP_mv_lantern_stone` | light | 16×34 | 0.5×1.1 | 1 | 0.45×0.3 |  | ✔ | 8 |
| `PROP_mv_signpost` | camp | 37×57 | 1.2×1.8 | 2 | 0.35×0.25 |  |  | 0 |
| `PROP_mv_bridge` | bridge | 127×68 | 4.0×2.1 | 1 | — |  |  | 0 |
| `PROP_mv_tent` | camp | 154×86 | 4.8×2.7 | 2 | 3.6×1.0 | ✔ | ✔ | 1 |
| `PROP_mv_campfire_set` | camp | 98×60 | 3.1×1.9 | 2 | 0.9×0.5 |  | ✔ | 1 |
| `PROP_mv_merchant_cart` | camp | 120×83 | 3.8×2.6 | 2 | 3.2×0.9 |  | ✔ | 1 |

### 2.1 第三輪追加（PROC-DRAFT，`tools/art/moonveil_creatures.py`，不取自參考圖）

| 名稱 | 類別 | 像素 | 用途 |
|---|---|---|---|
| `PROP_mv_vine` | mechanism | 32×44 | 可燒藤蔓（營地→湖畔捷徑）、Boss 石階閘門 |
| `PROP_mv_vine_burn_0..2` | mechanism | 32×44 ×3 | 燃燒動畫（每幀 0.12 秒，`BurnableVine.burnFrames`） |
| `PROP_mv_vine_burnt` | mechanism | 32×44 | 燒毀殘株 |
| `PROP_mv_lever` / `PROP_mv_lever_on` | mechanism | 24×32 | 斷橋拉桿關／開（`BridgeSwitch.leverOnSprite`） |
| `PROP_mv_shadow_s/m/l/xl` | shadow | 24×8、40×12、64×18、96×26 | 接地陰影（GroundDecal，`GameContentBuilder.AddContactShadow`） |
| `ENM_mushroom_king_draft`（Enemies/Draft） | enemy | 96×96 ×6 | 月蝕菇王紫色版：待機 0–3、蓄力 4–5（檔名與匯入設定沿用） |

Biome 欄位：`BiomeDefinition.vineSprite / vineBurntSprite / vineBurnFrames / leverSprite / leverOnSprite`；產生器寫入地圖 JSON 的 `vine.sprite/openSprite/frames`、`bridge_stone.leverSprite/leverOnSprite`、`boss_arena.sprite`。

Prefab：`Assets/_Game/Prefabs/Props/Draft/PROP_mv_*.prefab`（依精靈＋碰撞＋淡出組合共用）。
光源參數（色、半徑、強度、偏移）寫在 manifest 的 `light` 欄，由 `WorldMapExporter.ChooseLights` 在上限內分配。

## 3. 資料資產

| 資產 | 路徑 |
|---|---|
| Biome | `Assets/_Game/Data/World/Moonveil/BIO_moonveil.asset` |
| POI ×11 | `Assets/_Game/Data/World/Moonveil/POI_{junction,entrance,altar,ancient,mushroom,lake,camp,gathering,crystal,boss,waterfall}.asset` |
| Region Blueprint | `Assets/_Game/Data/World/Moonveil/REG_moonveil_forest.asset` |
| 地圖 JSON | `Assets/_Game/Data/Maps/moonveil_forest.json` |
| 場景 | `Assets/_Game/Scenes/Regions/MoonveilForest.unity`（已加入 Build Settings） |
