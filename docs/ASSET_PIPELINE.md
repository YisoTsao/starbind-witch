# ASSET_PIPELINE.md — 從參考圖到 Unity 正式素材

> 版本：Phase 0 草案（2026-11-21）｜相關：`ART_DIRECTION.md`、`ASSET_INVENTORY.md`、`ASSET_GAPS.md`

---

## 1. 總流程

```text
references/ (AI 參考圖，唯讀)
   │  ① 裁切：只裁 ASSET_INVENTORY 標為 Extractable 的物件
   ▼
art-source/_drafts/<id>_crop.png              (狀態：Extractable)
   │  ② 降採樣到世界比例（ART_DIRECTION §3）＋ 調色盤索引
   ▼
art-source/_drafts/<id>_draft.png             (狀態：Needs Cleanup)
   │  ③ 像素重繪：輪廓、去光暈、重新打光（左上光源）、去抗鋸齒
   ▼
art-source/<category>/<id>.aseprite|.png      (原始工作檔，不進 Unity)
   │  ④ 匯出 PNG RGBA（精確尺寸、透明底）
   ▼
Assets/_Game/Art/<category>/<id>.png
   │  ⑤ Unity 匯入規則自動套用（PPU/Filter/壓縮/Pivot）
   │  ⑥ 製作 Tile / Rule Tile / Prefab（Collider、Sorting）
   ▼
⑦ 驗收場景（ArtQA）並排截圖 → 通過才標 Production Ready
```

- `references/` 永遠不修改、不刪除。
- `art-source/` 不在 `Assets/` 底下，不會被 Unity 匯入，存放工作檔與草稿。
- 地形 Tile **不走 ①②**，直接從 ③ 開始新繪（圖 B/C 的地磚無法無縫拼接）。

---

## 2. 命名規則

```text
<類別>_<物件>_<變體>[_<動作>_<方向>].png

TILE_grass_01.png           地形單塊
RT_path_dirt.asset          Rule Tile
PROP_tree_blossom_01.png    單張道具
PROP_tree_blossom_01_canopy.png / _trunk.png   拆件
BLD_cottage_roof_left.png   建築模組
CHR_witch_walk_down.png     角色動作（橫向 Sprite Strip）
NPC_knight_idle_down.png
ENM_slime_move.png
VFX_fire_bolt_loop.png
UI_hud_hp_frame.png
DBG_collision_block.png     僅編輯器可見
```

- 全小寫，底線分隔，不得使用中文或空白（中文名稱寫在 Asset 表的「顯示名稱」欄）。
- 每個資產有唯一 Asset ID，對應 `ASSET_GAPS.md` 的一列。

---

## 3. 尺寸、Alpha、Pivot

| 類型 | 畫布尺寸 | Alpha | Pivot | Unity Sprite Mode |
|---|---|---|---|---|
| 地形 Tile | 精確 32×32，或 32 的倍數 Tilesheet | 地面基底不透明；轉角/邊緣透明 | Center | Multiple，Grid by Cell Size 32×32，無 Padding |
| 道具 / 樹 / 建築 | 依世界比例，**長寬皆取偶數**；不必是 32 的倍數 | 完全透明背景，無半透明殘邊 | **Bottom Center（接地點）**，以自訂像素座標設定 | Single 或 Multiple |
| 角色動畫 | 64×64 或 96×96 每幀，同動作同尺寸 | 透明 | 腳底中心，全部幀相同像素座標 | Multiple，Grid by Cell Size |
| VFX | 每幀固定大小（32/48/64/96/128） | 透明（可有半透明像素，但不得有模糊光暈） | 依特效：投射物 Center、地面爆炸 Bottom Center | Multiple |
| UI | 依參考解析度 1×繪製 | 透明 | Center | Single，九宮格需設 Border |

**Alpha 檢查（自動）**
- 角落像素 alpha 必須為 0（地形基底除外）。
- 不允許「幾乎透明」像素（alpha 1–15）殘留。
- 不允許棋盤格底（偵測灰白交錯圖樣）。

**Pivot 規則**
- 以像素為單位設定（Custom Pivot），**Pivot 必須落在整數像素上**，避免半像素抖動。
- 接地點 = 物件接觸地面那一排像素的中心，不是圖片底部（圖片下方若有陰影，Pivot 在陰影上方的接地線）。

---

## 4. Unity 匯入規則（由 AssetPostprocessor 自動套用）

位置：`Assets/_Game/Editor/PixelArtImportRules.cs`（Phase 0 建立）

| 設定 | 值 |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Pixels Per Unit | **32** |
| Filter Mode | Point (no filter) |
| Compression | None |
| Generate Mip Maps | Off |
| sRGB | On |
| Wrap Mode | Clamp |
| Max Size | ≥ 原圖尺寸（不得被縮小） |
| Read/Write | Off（除非工具需要） |
| Mesh Type | Full Rect（地形 Tile）；Tight（道具，減少 Overdraw） |
| Extrude Edges | 0（地形 Tile），1（道具） |

- 規則依路徑套用：`Assets/_Game/Art/**`。
- 由 Postprocessor 驗證並在 Console 輸出警告：尺寸非偶數、地形 Tile 非 32 倍數、PPU 不符、Max Size 不足。

**Sprite Atlas**
- 依場景/用途分組：`Atlas_Village`、`Atlas_Forest`、`Atlas_Interior`、`Atlas_Characters`、`Atlas_VFX`、`Atlas_UI`。
- Padding 2–4，Filter Point，Compression None，**關閉 Tight Packing 與旋轉**（地形 Tile）。

---

## 5. Autotile / Rule Tile 規格

使用 `com.unity.2d.tilemap.extras` 的 **Rule Tile**。

| 地形 | 規格 | Tile 數 | 理由 |
|---|---|---|---|
| 土路（草上） | **47 格 Blob** | 47（含內角） | 主要道路，需內外轉角 |
| 石板路 | 47 格 Blob | 47 | |
| 水面（草岸） | 47 格 Blob＋動畫 3–4 幀 | 47×幀 | 岸邊需內角；用 Animated Rule Tile |
| 懸崖 | 自訂規則：頂邊／立面左中右／內外角／底部 | 約 16–24 | 3/4 視角立面需要 1–2 tiles 高。目前草稿：`TILE_cliff_draft.png` 10 格＝上排［中、左端、右端、單格］＋下排［同上］＋瀑布上／下；`NeighborMaskTile` 模式 `EastWestNorth`（東西相鄰決定端點，北方是否也是立面決定上排或下排） |
| 草地基底 | Random Tile（4–6 變體，權重） | 4–6 | 無轉角 |
| 室內木地板 | Random Tile | 2–4 | |
| 室內牆 | 自訂規則：牆頂＋牆面 2 段 | 約 12 | |

- 每個 Rule Tile 附 **規則表**（`art-source/tilesets/<name>_rules.png`：47 格排列模板＋每格鄰接遮罩），由美術依模板繪製，避免 Prompt 生成的錯位。
- 邊緣過渡以「上層地形蓋在下層」方式處理（土路 Tile 外緣透明，蓋在草地上）；不製作每兩種地形之間的組合 Tile。

---

## 6. 拼接測試（Seam Test）

每一組地形 Tile 必須通過下列測試才可標 Production Ready：

1. **自動 Wrap 測試**：腳本將單塊 Tile 以 4×4 平鋪輸出 PNG，檢查左右/上下接邊像素差異（容許 0）。
2. **Rule Tile 圖樣測試**：在 `ArtQA` 場景用固定圖樣（直線、L 形、T 形、十字、單點、2×2 洞、斜對角）塗刷，截圖比對。
3. **重複感測試**：20×12 tiles 大面積平鋪，在 480×270 Game View 截圖，人工確認看不出格線。
4. **Camera 移動測試**：Pixel Perfect Camera 下慢速移動，確認無接縫閃爍（若有，檢查 Atlas Padding 與 Extrude）。

---

## 7. 驗收場景 ArtQA

`Assets/_Game/Scenes/Dev/ArtQA.unity`（Phase 0 建立）

- 魔女身高標尺（含帽 40 px 參考線）＋ 32 px 網格。
- 新素材放在魔女旁邊截圖，檢查比例、亮度（與圖 A 調色盤樣本並排）、輪廓。
- 碰撞顯示（Gizmos）＋ Sorting 測試：魔女繞物件前後走一圈，確認遮擋正確。
- 截圖存放 `docs/evidence/art/<asset_id>.png` 作為驗收證據。

---

## 8. 由參考圖萃取底稿的步驟（Extractable 物件）

1. 依 `ASSET_INVENTORY.md` 座標裁切，存 `art-source/_drafts/<id>_crop.png`。
2. 量測物件目前寬度 → 依 `ART_DIRECTION.md` §3 目標寬度計算縮放比。
3. 以 **Box/Area** 降採樣到目標尺寸（不要用 Nearest，會隨機丟細節）。
4. 調色盤索引到 `starbind_main` 主盤（不使用遞色 Dithering）。
5. 去背：圖 B/C 背景為漸層紫且帶外發光，**無法用單一顏色去背**，需手動描輪廓遮罩。
6. 人工像素重繪：輪廓、陰影方向、去除殘餘發光、亮度調整至白天基調。
7. 存檔為工作檔，標記為 `Needs Cleanup` → 美術完成後改 `Production Ready`。

> 步驟 1–4 可用腳本產生草稿（Phase 0 後可建立 `tools/art/extract_draft.py`）；步驟 5–6 必須由像素美術手工完成，**不可宣稱自動輸出為正式素材**。

---

## 9. Prefab 製作規則

| 物件 | 結構 |
|---|---|
| 樹 | `PROP_tree_x` (root, Pivot 接地) → `Trunk` (SpriteRenderer, World, CapsuleCollider2D/BoxCollider2D 只覆蓋底部約 12×8 px) → `Canopy` (SpriteRenderer, Foreground, OcclusionFader) |
| 建築 | root（`SortingGroup` 只用於 World 層的牆面部分）→ `Walls`、`Door`（互動觸發器）、`Roof`（Foreground, OcclusionFader）、`Deco/*`；Collider 為牆底一條（腳底線），**不覆蓋整面牆**，讓玩家可走到屋頂後方被遮擋 |
| 柵欄 | 每段 Prefab 或 Rule Tile（擇一，建議 Tile 以便快速繪製）；Collider 為底部細條 |
| 互動物件 | root → `Visual`、`InteractTrigger`（CircleCollider2D isTrigger）、`Interactable` 元件 |

**Collider 原則：** 3/4 視角下，碰撞只放在物件「腳印」（接地面），而不是整個圖片範圍。

---

## 10. 授權與紀錄

- 每個正式素材在 `ASSET_GAPS.md` 記錄：作者、工具、來源參考、是否 AI 輔助、授權。
- AI 參考圖衍生的素材需標記 `AI-assisted`，供 Steam 內容揭露表使用。

## 點陣字型（2026-11-21）

- 字型：`Assets/_Game/Resources/Fonts/FusionPixel12_zh_tw.ttf`（Fusion Pixel Font 12px Proportional 繁中，2026.11.10 版，SIL OFL 1.1）。授權：同資料夾的 `FusionPixel12_OFL.txt`；包含的子字型授權在 `art-source/licenses/fusion-pixel-font/LICENSES/`。
- 匯入：`PixelArtImportRules.OnPreprocessAsset` 設成 Dynamic、Hinted Raster（無反鋸齒）。執行時 `UIKit` 把字圖集設成 Point 取樣，`GameUI` 的 Canvas 開 `pixelPerfect`。
- 字級一律用 12 的倍數（`UIKit.PixelSize` 會自動對齊）；不要用 `FontStyle.Bold`，合成粗體會讓點陣字糊掉。
- 預熱字集：`Resources/Fonts/FusionPixel12_charset.txt`。新增中文文字後執行 `python3 tools/art/font_charset.py .` 重新產生（掃描 Scripts、Data 與 tools/map 的所有非 ASCII 字元，加上 ASCII 可列印字元）。EditMode `PrewarmCharsetIsFullyCoveredByTheFont` 會檢查字型是否涵蓋整個字集。
