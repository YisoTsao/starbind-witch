# PHASE0_REPORT.md — 星露村・魔女小屋外圍（Starlumen Village）第一個可走動場景

> 日期：2026-11-21｜Unity 6000.6.8f1｜URP 17.6（2D Renderer）｜場景：`Assets/_Game/Scenes/Village.unity`
> **美術狀態：全部為草稿（Needs Cleanup）。** 依使用者選擇的選項 C，畫面上所有圖像都標示為 draft，沒有任何一項是正式美術。

---

## 1. 本次完成

| 項目 | 狀態 | 實作 |
|---|---|---|
| Unity 2D URP 專案設定 | 完成 | Universal 2D 範本併入 repo 根目錄；移除 Visual Scripting / Timeline / Collab 等不需要的套件 |
| 32×32 Grid / Tilemap | 完成 | `Grid` + GroundBase / Water / Path / Cliff / Collision 5 層，PPU 32 |
| 草地、道路、河流、基本地形 | 完成（草稿圖） | 草地 4 變體、土路與水面 256 格自動拼接、懸崖立面、高台側壁、瀑布 |
| 魔女小屋 + 可互動入口 | 完成 | 小屋碰撞只覆蓋牆基；門口 F 提示 → 淡出淡入 → 呼叫 `Workshop`（場景尚未建立，會回報「not built yet」） |
| 樹、岩石、圍欄等物件 | 完成（草稿圖） | 18 種物件、39 個實例，位置直接取自參考圖 A |
| Player Spawn Point | 完成 | `spawn_square`（＝圖 A 魔女位置）、`spawn_from_workshop` |
| Camera Follow | 完成 | URP Pixel Perfect Camera（480×270，PPU 32，Upscale Render Texture）＋ `CameraFollow2D`（平滑、限制在地圖內） |
| 基本 Collider | 完成 | Collision Tilemap（水、懸崖、高台、地圖外圍）＋物件「腳印」碰撞框 |
| Y-axis Sorting | 完成 | 2D Renderer Custom Axis (0,1,0)；World 層 Sprite 以 Pivot 排序；玩家在樹後時樹淡出 |
| 可走動的出生場景 | 完成 | WASD／方向鍵／左搖桿八方向移動 4.5 u/s，F／A 鍵互動 |

## 2. 操作與重建

- **開啟：** Unity Hub → Add → 選 repo 根目錄 → 開啟 `Assets/_Game/Scenes/Village.unity` → Play。
- **操作：** WASD 移動、F 互動（小屋門口）。
- **重建場景（草稿美術或地圖資料改動後）：** 選單 `Starbind → Build Village Scene (draft art)`。注意：重建會重新產生場景，手動修改會被覆蓋；美術定稿後改為手動編輯場景。
- **重產草稿美術：**
  ```bash
  python3 tools/art/extract_drafts.py .   # 從圖 A 擷取物件草稿
  python3 tools/art/gen_tiles.py .        # 產生地形 Tile 草稿
  python3 tools/art/seam_test.py .        # 拼接測試（必須 PASS）
  python3 tools/map/village_north.py .    # 地圖配置 JSON
  ```

## 3. 測試結果（真實執行）

| 套件 | 結果 | 檔案 |
|---|---|---|
| EditMode | **9 / 9 通過** | `docs/evidence/phase0/test-results-EditMode.xml` |
| PlayMode（含擷圖 2 項） | **14 / 14 通過** | `docs/evidence/phase0/test-results-PlayMode.xml` |
| 地形拼接測試 | **PASS**：土路、水面各 0 / 315,392 像素不一致 | `tools/art/seam_test.py` |

PlayMode 驗證項目：出生點位置、移動速度 4.5 u/s（±7%）、斜向不加速、河流不可進入、懸崖立面不可穿越、小屋牆不可穿越、樹幹不可穿越、鏡頭不超出地圖（兩個角落）、玩家與物件同層並以 Pivot 排序、玩家在樹後時樹淡出 <0.6、門口出現 F 提示並觸發 Workshop 轉場、實際鍵盤 D 鍵移動與 F 鍵互動。

### 測試中發現並修正的問題

1. **CompositeCollider2D 讓所有地形碰撞消失。** 6000.6.8f1 下，`TilemapCollider2D` 以 Merge 交給 `CompositeCollider2D (Polygons)` 時產生 0 條路徑（Play mode 實測：0 個形狀；不用 Composite 為 210 個），河流和懸崖都能走過去。已改為每格獨立碰撞框；約 200 個靜態框，成本可忽略。這和 GDD §3.1「Composite Collider 2D」寫法不同，見 SPEC_CONFLICTS C-17。
2. **批次模式下鍵盤輸入被忽略**：Input System 預設會丟棄未聚焦視窗的輸入。只在測試中切換為 IgnoreFocus，遊戲設定不變。
3. **批次模式下 `WaitForEndOfFrame` 永遠不會返回**：擷圖改為等待數個一般影格後再讀取 RenderTexture。

## 4. 截圖（真實 Play mode 渲染）

所有畫面都是 Play mode 中遊戲攝影機的實際渲染輸出：Pixel Perfect Camera 480×270，整數倍放大到 1920×1080，與 Game View 是同一條渲染流程。因為批次模式沒有編輯器視窗，所以不是直接截取 Game View 視窗。

| 檔案 | 內容 |
|---|---|
| `docs/evidence/phase0/01_spawn_1080p.png` | 出生點（對應圖 A 視角） |
| `docs/evidence/phase0/02_cottage_door_prompt_1080p.png` | 小屋門口 F 提示 |
| `docs/evidence/phase0/03_behind_blossom_tree_fade_1080p.png` | 玩家在花樹後：Y 排序＋淡出 |
| `docs/evidence/phase0/04_west_waterfall_river_1080p.png` | 西側瀑布與河流 |
| `docs/evidence/phase0/05_south_meadow_1080p.png` | 南側草地與通往森林的路 |
| `docs/evidence/phase0/06_east_edge_1080p.png` | 東側邊界 |
| `docs/evidence/phase0/00_overview_native_896x640.png` | 整張地圖 1:1（1 texel = 1 美術像素） |
| `docs/evidence/phase0/07_compare_reference_vs_game_native_x3.png` | **上：圖 A；下：遊戲中同一區域**，皆為原生密度 |

自動畫面檢查（ut_review.image_checks）：7 張全部通過；無全白、全黑、單色或洋紅色缺材質畫面。平均亮度 0.59–0.66，圖 A 為 0.57。

## 5. 與參考圖 A 的比對：仍需改善

依 `07_compare_reference_vs_game_native_x3.png` 逐項比較。**場景構圖、比例、視角與物件位置已一致；差距集中在程式產生的地形草稿。**

| # | 項目 | 參考圖 A | 目前遊戲 | 改善方式 | 優先 |
|---|---|---|---|---|---|
| 1 | **懸崖** | 圓潤厚實的岩柱、不規則輪廓、立面約 1–1.5 tiles、轉角圓滑 | 筆直一排岩柱，像木柵；高台側壁只是一條細牆 | 美術手繪懸崖 Rule Tile（立面 2 格高、內外轉角、側壁）；高台邊界改為不規則形狀 | 高 |
| 2 | **水岸形狀** | 有機曲線水岸、淺色泡沫線、荷葉、深淺水色 | 以格子為單位的長方形水域，邊角圓化不足 | 地圖改用更細緻的水域形狀（斜角階梯）；美術重繪水岸 47/256 格；加荷葉 GroundDetails | 高 |
| 3 | **瀑布** | 寬、多段水流、底部白色水花與漣漪 | 單格靜態水柱 | 2 格寬、4 幀動畫、底部水花 Sprite | 中 |
| 4 | **道路形狀** | 自然寬窄變化、草叢咬邊明顯 | 依格子排列，路緣較平順 | 地圖增加路寬變化；路緣草叢 Tile 加強；或改由美術手繪 Rule Tile | 中 |
| 5 | **草地細節** | 深淺草叢、亮色小花散布、色塊變化 | 均勻單一草色 | GroundDetails 層：小花、草叢、落葉 Sprite（約 10 種）；草地加大面積色塊變體 | 高 |
| 6 | **左上大花樹** | 巨大粉色花樹（參考圖中被裁切） | 缺（擷取來源不完整） | 美術重繪完整大花樹 | 中 |
| 7 | **小屋屋頂** | 參考圖頂端被裁切 | 屋頂上緣是平切線，目前貼著地圖上緣所以看不到 | 美術補完整屋頂與煙囪 | 中 |
| 8 | **物件邊緣** | 乾淨輪廓 | 擷取的草稿有殘留草色雜點（圍欄、花樹、小屋兩側紫藤） | 像素美術清理（ASSET_PIPELINE §8 步驟 5–6） | 中 |
| 9 | **魔女** | 單一正面圖 | 單一正面靜態圖（約 33 px 高），沒有走路動畫和方向 | 正式角色：40 px 高、4 方向 Idle/Walk（已決定） | **高** |
| 10 | **光影** | 窗光、燈籠暖光 | 只有一盞全域白光；暖光是圖片本身的顏色 | 窗戶與燈籠加 URP Light2D；整體色調以 Volume 微調 | 低 |
| 11 | **石板路、小石徑** | 東側與左下有石板 | 缺 | 石板 Decal Sprite | 低 |
| 12 | **高低差邏輯** | 南側懸崖上下兩層 | 南側懸崖西端與道路相接處沒有坡道，視覺上不連續 | 地圖設計時給坡道或階梯 | 中 |

## 6. 未完成 / 已知限制

- Workshop（室內）場景尚未建立；門口互動會淡出淡入並回報 `scene 'Workshop' is not built yet`。
- 森林出口是封住的（地圖外圍碰撞）；Forest 場景屬於下一階段。
- 星露村目前只做北側 28×20 tiles，WORLD_STRUCTURE 規劃為 48×40；訓練場、NPC、橋尚未製作。
- 沒有 Bootstrap 常駐場景；Village 可以單獨執行。轉場服務已可用，Bootstrap 在加入第二個場景時一起建立。
- 沒有 Windows Build：本機尚未安裝 Windows Build Support 模組。
- 沒有效能數據：此場景無戰鬥或大量物件，本階段未做 Profiler 量測。
- Git LFS 未安裝；變更尚未 commit。
