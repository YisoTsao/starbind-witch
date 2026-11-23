# ASSET_GAPS.md — 美術資產缺口追蹤

> 由 `ASSET_GAPS_TEMPLATE.md` 建立（模板保留不改）｜盤點日期：2026-11-21
> 狀態欄使用 `ASSET_INVENTORY.md` 的定義：Reference Only / Extractable / Needs Cleanup / Production Ready / Missing
> 「底稿來源」= 可裁出當重繪底稿的位置；空白表示需從零繪製。
> **目前全部項目皆非 Production Ready。**
> 2026-11-21 起，Village 場景使用標示 `_draft` 的草稿（選項 C）：地形由 `tools/art/gen_tiles.py` 程式產生，物件與魔女由 `tools/art/extract_drafts.py` 從圖 A 擷取（AI-derived）。草稿只能用來驗證系統和構圖，**全部需要重繪才能成為正式美術**。

## 優先級說明

- **P0**：第一個可走動場景（Village 一角＋魔女走路）必需
- **P1**：Phase 1 戰鬥測試必需
- **P2**：Phase 2–5
- **P3**：正式版／打磨

## 1. 地形 Tile（全部需新繪）

| Asset ID | 類別 | 目標尺寸/格式 | 底稿來源 | 狀態 | 遮擋/碰撞/動畫需求 | 優先 |
|---|---|---|---|---|---|---|
| TILE_grass_01–06 | 草地 Random Tile | 32×32 RGB(A)，6 變體 | A 草地樣貌 | Needs Cleanup（草稿 `TILE_grass_draft` 4 變體，程式產生；缺色塊與細節） | 可行走；無縫 | P0 |
| RT_path_dirt | 土路 Rule Tile | 47 格 Blob | A 土路 | Needs Cleanup（草稿 256 格 `TILE_path_draft_256`，拼接測試 PASS） | 可行走；邊緣透明蓋草地 | P0 |
| RT_path_stone | 石板路 | 47 格 Blob | A 東側石板、B 紫石 | Missing | 可行走 | P2 |
| RT_water_grass | 水面＋草岸 | 47 格 × 3–4 幀 | A 池塘 | Needs Cleanup（草稿 256 格 `TILE_water_draft_256`，靜態，拼接測試 PASS） | 不可行走；動畫 | P0 |
| TILE_waterfall | 瀑布 | 32 寬 × 2–3 tiles 高，4 幀 | A 西側、B Cliffs | Needs Cleanup（單格靜態草稿） | 動畫 | P2 |
| RT_cliff_dirt | 懸崖（頂邊/立面/轉角） | 約 16–24 tiles | A 西/南懸崖 | Needs Cleanup（立面 4 種＋高台側壁 8 種草稿；外觀與圖 A 差距最大） | Collision 於立面 | P0 |
| TILE_stairs_stone | 石階 | 32×32 ×3 | A 小屋前石階 | Missing | 可行走 | P0 |
| TILE_bridge_wood | 小橋 | 32×32 模組（橋頭/中段/橋尾） | — | Missing | 可行走；Collision 不畫 | P0 |
| TILE_lilypad_01–03 | 荷葉 | ≤32×32 RGBA | A 下池 | Missing | GroundDetails | P2 |
| TILE_forest_floor_* | 森林地面變體 | 32×32 | B 深草 | Missing | 可行走 | P2 |
| TILE_floor_wood_* | 室內木地板 | 32×32 Random | C 地板（圖案） | Missing | 可行走 | P2 |
| RT_wall_interior | 室內牆（牆頂＋牆面） | 約 12 tiles | C 室內牆面 | Missing | 不可行走 | P2 |

## 2. 建築與 Props

| Asset ID | 類別 | 目標尺寸/格式 | 底稿來源 | 狀態 | 遮擋/碰撞/動畫需求 | 優先 |
|---|---|---|---|---|---|---|
| BLD_cottage_exterior | 魔女小屋（牆/門/窗/屋頂/煙囪/紫藤/燈） | 約 160–176 × 144–176，模組化 RGBA | A 北中；B Buildings (345,5)；C 外觀區 | Needs Cleanup（`PROP_cottage_draft` 擷取自圖 A；屋頂上緣被裁切、兩側殘留雜點） | 屋頂 Foreground 淡出；牆底碰撞；門 F 互動；煙/燈動畫 | P0 |
| PROP_tree_blossom_01 | 粉花大樹 | 約 120×128，樹幹/樹冠分件 | A 左上；B Trees | Needs Cleanup（`PROP_tree_blossom_draft`，未拆樹幹/樹冠，以淡出處理遮擋） | 樹幹碰撞、樹冠前景 | P0 |
| PROP_tree_blossom_02 | 粉花樹（第二型） | 約 112×120 | A 東 | Extractable | 同上 | P2 |
| PROP_tree_green_01–02 | 綠樹 / 灌木樹 | 約 56×72 | A 多處；B Trees | Needs Cleanup（`PROP_tree_green_s/m/b_draft`） | 同上 | P0 |
| PROP_tree_wisteria_01 | 紫藤樹 | 約 112×128 | B Trees | Extractable | 同上 | P2 |
| PROP_fence_wood | 木柵欄 | 32×32 Rule Tile 或段 | A；B Props；C 外部裝飾 | Needs Cleanup（3 段固定長度草稿；尚非可接續模組） | 底部碰撞 | P0 |
| PROP_sign_direction | 路牌 | 約 32×48 | A；B Banners | Needs Cleanup（`PROP_sign_draft`；尚未加互動） | F 互動 | P0 |
| PROP_banner_moon | 紫色月亮旗 | 約 16×40 | A；B；C | Extractable | 無碰撞；可擺動 | P2 |
| PROP_rock_01–03 | 岩石 | 16–48 px | A；B Props/Variations | Needs Cleanup（`PROP_rock_l/m/s_draft`） | 碰撞 | P0 |
| PROP_stump_01 | 樹樁 | 約 40×28 | A 南；B Trees | Needs Cleanup（`PROP_stump_draft`） | 碰撞 | P2 |
| PROP_mushroom_glow_01 | 發光蘑菇 | 約 24×24 | A；B Vegetation | Needs Cleanup（`PROP_mushroom_glow_draft`，無 2D Light） | 無碰撞；2D Light | P2 |
| PROP_flower_01–06 | 小花叢 | ≤32×32 | A；B Vegetation | Needs Cleanup（只有 1 種花叢草稿 `PROP_flowerbush_draft`） | GroundDetails | P0 |
| PROP_bush_01–02 | 花叢灌木 | 約 32×28 | A | Extractable | 選擇性碰撞 | P2 |
| PROP_barrel / PROP_crate | 木桶 / 木箱 | 約 20×24 | A；B Interactive；C | Extractable | 碰撞 | P0 |
| PROP_lantern_post | 燈柱 | 約 16×48 | B Banners；C | Extractable | 2D Light | P2 |
| PROP_stall_purple | 紫帳篷攤位 | 約 96×80 | A；B Props | Needs Cleanup（`PROP_stall_draft`） | 頂棚 Foreground | P2 |
| PROP_crystal_fountain | 傳送星盤（水晶噴泉） | 約 64×64 | A 東；B Props | Needs Cleanup（`PROP_crystal_fountain_draft`，靜態） | 碰撞；F；發光動畫 | P2 |
| PROP_training_target / dummy | 箭靶 / 稻草人 | 約 32×48 | B Props | Extractable | 可被攻擊 | P1 |
| PROP_chest_01–03 | 寶箱 | 約 32×28，開啟 3 幀 | B Interactive | Extractable | F；動畫 | P2 |
| PROP_star_altar | 星痕祭壇 | 約 48×48 | B Interactive／Path & Decals | Extractable | F 觸發怪潮 | P2 |
| PROP_vine_burnable | 可燃藤蔓 | 32×64 | — | Missing | 碰撞；燃燒動畫 | P2 |
| PROP_bridge_broken | 斷橋＋開關 | — | — | Missing | 狀態切換 | P2 |
| PROP_ruin_pillar / arch | 古代石柱／石拱 | 約 32×64 / 96×80 | B Props | Extractable | 碰撞 | P2 |

## 3. 室內家具（Workshop）

| Asset ID | 類別 | 目標尺寸/格式 | 底稿來源 | 狀態 | 遮擋/碰撞/動畫需求 | 優先 |
|---|---|---|---|---|---|---|
| FURN_bookshelf_01–02 | 書櫃 | 約 48×64 | C 書櫃區 | Extractable | 碰撞 | P2 |
| FURN_desk_research | 研究桌 | 約 64×40 | C 書桌 | Extractable | F | P2 |
| FURN_grimoire_stand | 魔典台 | 約 32×40 | C 小書台 | Extractable | F；發光動畫 | P2 |
| FURN_cauldron | 大鍋＋煉金台 | 約 48×48，冒泡 4 幀 | C 魔法工作區 | Extractable | F；動畫 | P2 |
| FURN_bed_canopy | 帷幔床 | 約 64×80 | C 家具 | Extractable | 碰撞；F（存檔候選） | P2 |
| FURN_table_chairs / wardrobe / vanity / sofa | 生活家具 | 依比例 | C 家具 | Extractable | 碰撞 | P3 |
| DECO_small_* | 蠟燭、書堆、藥水、水晶、畫框、貓… | ≤24 px | C 小物件 | Extractable（需重繪） | 無 | P3 |
| DECO_rug_star | 星紋地毯 | 約 96×64 | C 地毯 | Extractable | GroundDecal | P2 |

## 4. 角色

| Asset ID | 類別 | 目標尺寸/格式 | 底稿來源 | 狀態 | 遮擋/碰撞/動畫需求 | 優先 |
|---|---|---|---|---|---|---|
| CHR_witch_idle / walk | 魔女（4 方向） | 64×64/幀（角色高待定 36–44） | A 正面站姿（僅一個方向） | **Needs Cleanup（只有單一正面靜態草稿 `CHR_witch_front_draft`，約 33 px；正式版 40 px、4 方向動畫仍 Missing）** | Idle 4、Walk 6 幀 × 4 方向 | **P0** |
| CHR_witch_cast / melee / dash / hit / death | 魔女戰鬥動作 | 64×64；近戰 96×96 | — | Missing | 見 ART_DIRECTION §6.1 | P1 |
| NPC_ella / knight / priest / archer | NPC | 64×64 | — | Missing | Idle / Walk | P2 |
| ENM_slime | 苔蘚史萊姆 | 32×32 或 48×48 畫布 | — | Missing | Move / Hit / Death | P1 |
| ENM_spore_mushroom / root_guard / wisp | 一般敵人 | 48–64 畫布 | — | Missing | 依 GDD §10.1 | P2 |
| ENM_spore_elite | 孢子精英 | 64–96 | — | Missing | | P2 |
| BOSS_eclipse_mushroom_king | 月蝕菇王 | 128–160 | — | Missing | 三階段動作 | P2 |

## 5. VFX

| Asset ID | 類別 | 目標尺寸/格式 | 底稿來源 | 狀態 | 需求 | 優先 |
|---|---|---|---|---|---|---|
| VFX_staff_swing_1–3 | 法杖三段 | 96×96 序列 | — | Missing | start/end | P1 |
| VFX_charged_bolt | 蓄力魔彈 | 32×32 | — | Missing | 飛行/命中 | P1 |
| VFX_fire_bolt | 火球飛行/爆炸 | 32 / 64 序列 | B 營火（概念） | Missing | 發射/命中 | P1 |
| VFX_ice / lightning / wind | 冰晶、雷鏈、旋風 | 序列 | B 魔法環（概念） | Missing | | P2 |
| VFX_fire_tornado | 火焰龍捲 | 64×96 序列 | — | Missing | start/loop/end | P2 |
| VFX_hit_spark | 命中火花 | 16–32 | — | Missing | 大量、低成本 | P1 |
| VFX_overdrive | 超載 | 全螢幕 + 角色 | — | Missing | | P2 |
| VFX_telegraph_* | 敵方地面預警 | 圓/扇/線 | — | Missing | Telegraph 層 | P2 |
| VFX_ambient_* | 花瓣、螢火、葉子、煙囪煙 | 8–16 px | B Effects；C 特效（概念） | Reference Only | 氛圍 | P3 |

## 6. UI / 字型 / 音效

| Asset ID | 類別 | 狀態 | 備註 | 優先 |
|---|---|---|---|---|
| UI_hud_* | HP/MP/共鳴、Q/E/R 槽、消耗品 | Missing | 暗紫＋金、簡潔 | P1 |
| UI_levelup_card | 三選一卡 | Missing | | P2 |
| UI_grimoire / inventory / map | 魔典、背包、小地圖 | Missing | | P2 |
| UI_prompt_interact | 「F」互動提示 | Needs Cleanup（`UI_prompt_F_draft` 14×14） | | P0 |
| FONT_zh_pixel | 繁中點陣字 12 px | **Done（2026-11-21）**：Fusion Pixel Font 12px Proportional 繁中（SIL OFL 1.1，可商用、可內嵌） | 授權檔 `Resources/Fonts/FusionPixel12_OFL.txt`、`art-source/licenses/fusion-pixel-font/` | — |
| SFX_* / BGM_* | 腳步、攻擊、UI、環境、村莊/森林/Boss 音樂 | Missing | 商用授權紀錄 | P1–P3 |

## 7. 工具 / 除錯（非美術素材，可直接製作）

| Asset ID | 用途 | 狀態 |
|---|---|---|
| DBG_block / DBG_spawn | Collision / Markers Tilemap，執行時不顯示 | 已建立（工具，不是美術） |
| PAL_starbind_main | 48–64 色主調色盤 | 部分：地形草稿使用的 30 色寫在 `tools/art/gen_tiles.py`（取自圖 A）；尚未輸出 .gpl |
| TPL_blob47_32 | 47 格 Rule Tile 繪製模板 | Missing |

## 驗收 checklist（每項資產）

- [ ] 圖片尺寸、格線正確
- [ ] 透明通道正確（非棋盤底）
- [ ] 無字、無標籤、無分隔框線
- [ ] 與基準像素密度、輪廓與調色盤一致
- [ ] 地磚實測可拼接（適用者）
- [ ] 樹木、屋子、家具 Pivot 與 Collider 合理
- [ ] 素材商用授權來源已記錄（含是否 AI 輔助）

## 新增：與參考圖比對後的缺口（2026-11-21）

| Asset ID | 類別 | 狀態 | 備註 |
|---|---|---|---|
| DET_grass_tuft_01–04 / DET_flower_01–06 / DET_leaf_01–02 | GroundDetails 草叢、小花、落葉 | Missing | 圖 A 草地的豐富度主要來自這些細節 |
| DET_lilypad_01–03 | 荷葉 | Missing | 同 TILE_lilypad |
| DECO_stone_paving_01–03 | 石板 Decal | Missing | 圖 A 東側與左下的石板 |
| PROP_tree_blossom_large | 左上巨大花樹 | Missing | 圖 A 中被裁切，無法擷取 |
| RT_cliff_corner_* | 懸崖內外轉角、曲線邊緣 | Needs Cleanup | 2026-11-21 第二輪：立面左右端有側壁轉角（陰影側石，接上高台側壁）；內角與曲線邊緣仍缺 |
| VFX_waterfall_splash | 瀑布底部水花（動畫） | Missing | |
| LIGHT_window / LIGHT_lantern | 窗光、燈籠光（2D Light Cookie） | Missing | |

## 新增：完整草稿版（Phase 1–5 系統）使用的程式產生草稿（2026-11-21）

> 由 `tools/art/gen_game_drafts.py`（像素繪圖：`tools/art/pixlib.py`）與 `tools/audio/gen_sfx.py` 產生，**不是從參考圖擷取**，也**全部不是正式美術/音效**（Needs Cleanup / Placeholder）。清單與 Pivot 在 `Assets/_Game/Art/draft_manifest.json`。

| Asset ID（`*_draft`） | 類別 | 目前草稿 | 正式版需要 | 優先 |
|---|---|---|---|---|
| CHR_witch_draft | 魔女 4 方向（下/上/側×Idle+Walk4+Cast） | 48×48 畫布、角色 40 px（C-01）；左向為側面翻轉 | 正式 4 方向（含真正左向）、近戰揮杖、Dash、受擊、死亡動畫 | P1 |
| NPC_ella/knight/priest/archer_draft | 艾拉、凱恩、莉亞、洛恩 | 同魔女規格 | 正式 Idle/Walk | P2 |
| ENM_slime / spore_shroom / root_guard / wisp / spore_elite | 4 普通敵＋精英 | 4 幀循環（史萊姆擠壓、蘑菇彈跳、樹根走路、幽光飄動） | 攻擊/受擊/死亡動畫 | P1 |
| ENM_mushroom_king_draft | 月蝕菇王 96×96 | 待機 4 幀＋蓄力 2 幀 | 三階段外觀變化、招式動畫 | P2 |
| PROP_training_dummy / archery_target | 訓練場 | 單張 | — | P2 |
| PROP_bridge_wood / bridge_stone(_broken) | 村莊木橋、森林石橋 | 整張 Sprite（地面層 Order 35） | 改為橋頭/中段/橋尾模組 Tile | P2 |
| PROP_forest_oak / pine / dark, village_round | 樹 | 程式繪製 | 樹幹/樹冠拆件 | P2 |
| PROP_pillar / altar / chest(_open) / vine(_burnt) / lever(_on) / lamp / crate / glow_shroom / bush / rock_dark / log / starplate | 森林與村莊物件 | 單張 | — | P2 |
| INT_bookshelf / bed / table / chair / cauldron / alchemy / lectern / desk / wardrobe / rug / window / door_in / plant | 室內家具 | 單張 | 依圖 C 造型重繪 | P2 |
| TILE_forest_grass / forest_path_256 / forest_water_256 | 森林地形 | 沿用村莊自動拼接演算法、換色；拼接成立 | 森林專屬細節 | P2 |
| TILE_interior（地板 4＋牆頂＋牆面） | 室內地形 | 32×32 | 牆角、門框、踢腳 | P2 |
| PICK_xp / potion_hp / potion_mp / page / material | 掉落物 | 單張 | 拾取動畫 | P2 |
| VFX_*（33 組 flipbook） | 火球、爆炸、冰晶、冰爆、落雷、法杖斬擊、魔彈、蓄力、追蹤彈、星火、冰刃、雷環、旋風、火焰龍捲、蒸汽、超載、碎晶、命中、煙塵、毒孢、毒池、預警圈/條、超載光環、升級、燃燒/冰緩/感電圖示、光暈、粒子、陰影、傳送門 | 程式繪製、混色以抖動代替半透明 | 正式像素 VFX Sprite Sheet（GDD §12.2） | P1 |
| ICON_*（12） / UI_digits_draft | 技能圖示、傷害數字 | 16×16 / 6×8 | 正式 UI 圖示與像素數字 | P2 |
| UI 面板 | HUD、卡片、選單 | uGUI 純色面板＋系統字型（PingFang / 微軟正黑體） | **授權中文點陣字型**、UI 框線素材（暗紫/金） | P1 |
| sfx_*（33 個 WAV） | 法杖、魔彈、火/冰/雷、旋風、反應、受擊、拾取、升級、Boss 等 | 程式合成方波/噪音 | 取得商用授權音效；音樂（目前沒有） | P2 |
| MUSIC_* | 村莊、森林、Boss、超載層 | **Missing** | | P2 |

## 新增：從使用者 Chibi 角色表擷取的角色草稿（2026-11-21）

> 由 `tools/art/extract_characters.py` 從 `references/tilesets/` 的三張 Chibi 角色表（唯讀，未修改）切割、去背、縮成 64×56 格（角色約 40 px 高）、減色後輸出到 `Assets/_Game/Art/Characters/Ref/`；清單與 Pivot 在 `Assets/_Game/Art/character_manifest.json`。
> 來源是有格線與雜訊背景的 AI 角色表，依 CLAUDE.md **不算正式 Sprite Atlas**，狀態一律 **Needs Cleanup**。實際 Game View 中比地磚略糊，尚未與參考圖品質比對。

| Asset ID（`*_ref`） | 來源 | 內容 | 需要清理 | 優先 |
|---|---|---|---|---|
| CHR_witch_ref | Chibi_Mage_Pixel_Art Sprite_Sheet.png | 玩家魔女，取代 CHR_witch_draft；下/上/側 × Idle 2＋Walk 5＋施法 5 | 手工修像素與外框；施法幀內含光球/弧光（應改由 VFX 處理）；正下方行走幀在原圖是斜側面；沒有 Dash/受擊/死亡 | P1 |
| NPC_prince_ref | Chibi_Knight_Pixel_Art Sprite_Sheet.png | 王子 NPC（訓練場）；Idle 2＋Walk 4＋揮劍 5 | 銀甲減色後偏暖；手工修整 | P2 |
| NPC_villager_01–12_ref | Chibi_Villager_Pixel_Sprite_Sheet.png | 12 種村民：髮色、膚色、背心、襯衫、褲子換色，草帽（繪製）、頭巾、圍裙；招手動作 5 幀 | 換色以顏色＋高度分區，少數像素誤判（手部、眼白）；體型與髮型相同，只靠配色區分；需要女性/老人等不同體型 | P2 |

## 新增：魔女村莊裝飾與地形加強（2026-11-21）

> 工具：`tools/art/village_decor.py`（程式繪製，清單 `Assets/_Game/Art/decor_manifest.json`）、`tools/art/gen_tiles.py`（草地 8 種、路緣草叢與碎石、水面淺灘帶）、`tools/art/extract_drafts.py`（擷取後清理：擦除鄰近物件殘片、櫻花樹鏡像補齊、柵欄依原尺寸重繪）。全部是 **Needs Cleanup** 草稿，尚未達到參考圖品質（前後對照：`docs/evidence/final/V08_before_after_village_polish.png`）。

| Asset ID | 用途 | 目前狀態 | 需要 | 優先 |
|---|---|---|---|---|
| DEC_tuft_0–4 / flowers_{pink,lilac,white,yellow,blue} / clover / pebbles_0–1 / shrooms_{red,violet} / petals / lily_0–1 | 地面裝飾貼花（GroundDecal，無碰撞） | 程式繪製，小尺寸 | 手繪變化、更多花種 | P2 |
| DEC_patch_light / light_s / dark | 打破 32 px 格線的大塊明暗草地 | 抖動邊緣橢圓 | 改為 Rule Tile 或多格草地區塊 | P2 |
| DEC_mote | 螢火蟲 / 魔力光點（AmbientMotes） | 5×5 十字 | 正式粒子 Sprite | P3 |
| PROP_lantern_post | 魔女路燈（取代 PROP_lamp，附 2D 光） | 程式繪製 | 依參考圖重繪 | P2 |
| PROP_cauldron / herb_rack / broom / potion_crate / candles / pumpkin(_violet) / crystal_cluster | 魔女風格擺設 | 程式繪製 | 依參考圖重繪、坩堝冒泡動畫 | P2 |
| PROP_fence_long / mid / east | 柵欄 | 擷取圖含欄間草地，改為同尺寸程式重繪 | 正式柵欄模組（端柱/中段） | P2 |
| PROP_tree_blossom | 櫻花樹 | 原圖左半被攤位遮住，以右半鏡像補齊 | 手繪左半，避免對稱感 | P2 |
| TILE_cliff（村莊北側崖面） | 崖面 | **2026-11-21 重畫**：灰紫色圓石錯層堆疊、苔蘚、草皮垂邊；高台側壁同色（不再像木柵欄）。**第二輪**：改成 2 格高（上排草皮垂邊、下排崖腳與接地陰影，石層跨格連續）、左右轉角、2 格高瀑布；高台側壁改成陰影側石 | 仍是程式草稿；內角、曲線邊緣、瀑布水花動畫仍缺 | P2 |
| 訓練場沙地 | 大片地面 | 只有碎石，畫面單調 | 沙地變化格、足跡、器材擺設 | P2 |

## 新增：像素 UI 與 32×32 圖示（2026-11-21）

> 工具：`tools/art/ui_art.py`（程式繪製，清單 `Assets/_Game/Art/ui_manifest.json`，含 9-slice `border`）。原本 `gen_game_drafts.py` 的 16×16 ICON_* 已改由此工具產生 32×32 版本。狀態 **Needs Cleanup**。

| Asset ID | 用途 | 目前狀態 | 需要 | 優先 |
|---|---|---|---|---|
| UI_panel / slot / bar_frame / keycap（9-slice） | HUD、對話框、選單、地圖面板、按鈕 | 金色雙框＋角落寶石，3× 整數倍顯示 | 正式 UI 框（含裝飾角、分隔線） | P1 |
| UI_bar_fill_{hp,mp,res,xp,boss,lag} | 條狀量表填色 | 8 px 垂直漸層 | 動態光澤 / 損血閃爍 | P2 |
| UI_moon_ornament / UI_marker_* | 小地圖裝飾與地圖標記 | 程式繪製 | — | P3 |
| ICON_*（14 個，含 potion_hp/mp） | 技能、物品圖示 | 32×32，外框＋陰影＋光暈 | 依美術方向重繪、冷卻中的灰階版 | P1 |
| 中文字型 | 所有 UI 文字 | **已換成 Fusion Pixel 12px（OFL）**，字級對齊 12 的倍數 | 若之後要 16 px 或粗體標題字，需另選字型 | — |

## 新增：法術特效、升級卡/魔典 UI、任務標記（2026-11-21）

> 法術特效的現行版、材質與匯入流程見 [法術特效美術規格](SPELL_VFX_ART_DIRECTION.md)。這裡保留 UI 與後續美術需求。

| Asset ID | 用途 | 目前狀態 | 需要 | 優先 |
|---|---|---|---|---|
| VFX_blast | 火系爆炸（預備星芒→白熱衝擊幀→火舌火球→破洞煙圈→煙團散去） | 96×96、12 幀；第二輪改成分層賽璐珞明暗＋深色描邊＋不規則輪廓 | 美術手繪爆炸 Sprite Sheet | P2 |
| VFX_sky_bolt | 天降雷擊（主雷、殘影、二次落雷、地面電弧與煙） | 48×192、9 幀，紫色光暈沿主幹與分枝，接地點另疊細電弧 | 美術手繪雷擊 | P2 |
| VFX_shockwave / VFX_flash / VFX_magic_circle | 衝擊波環、星芒閃光、施法魔法陣（白色，依元素上色） | 程式繪製 | 正式魔法陣圖騰 | P2 |
| VFX_frost_nova / VFX_wind_blades / VFX_scorch / VFX_bloom | 冰晶爆、風刃、焦痕、柔光 | 第二輪：冰晶爆 10 幀（地面結冰、雙面受光冰柱由後往前長出、碎裂飛散）；風刃 8 幀（3 道粗頭細尾月牙＋速度線）；焦痕最大縮放 1.1 | 美術手繪 | P3 |
| 全螢幕閃光 | 超載、超載爆燃、蓄力 3 段 | 純色疊加，最高 alpha 0.55；**暫停選單 → 設定** 可選強／弱／關，存進存檔 | — | Done |
| UI_card / banner / divider / tab(_active) / socket / page | 升級卡、魔典分頁 | 9-slice 程式繪製 | 正式卡框、書本內頁 | P2 |
| UI_marker_quest / UI_marker_arrow | 小地圖目標星標、邊緣箭頭 | 程式繪製 | — | P3 |
