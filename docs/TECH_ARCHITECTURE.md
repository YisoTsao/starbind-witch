# TECH_ARCHITECTURE.md — Unity 技術架構

> 版本：Phase 0 草案（2026-11-21）｜上位：`GAME_DESIGN_V3.md` §13–§14
> 本文件補充 GDD 未定的實作細節；**模組職責以 GDD §13.2 為準**，此處不重複。

---

## 1. 引擎與套件

| 項目 | 決定 | 狀態 |
|---|---|---|
| Unity 版本 | **6000.6.8f1**（使用者決定） | 定案 |
| 範本 | Universal 2D（URP + 2D Renderer） | 定案 |
| Build 模組 | 需加裝 **Windows Build Support (Mono)**；本機目前只有 Mac 與 WebGL | **待安裝** |
| 版本控制 | Git 已建立，`.gitignore` 已加入；Git LFS（`*.png *.psd *.aseprite *.wav *.ogg`）尚未安裝 | 部分完成 |

### 1.1 套件（以 Package Manager 對應該編輯器版本的 Verified 版本為準）

| 套件 | 用途 | Phase |
|---|---|---|
| `com.unity.render-pipelines.universal` | URP 2D Renderer、2D Light、**Pixel Perfect Camera** | 0 |
| `com.unity.2d.tilemap` / `com.unity.2d.tilemap.extras` | Tilemap、Rule Tile、Animated/Random Tile | 0 |
| `com.unity.2d.sprite` | Sprite Editor | 0 |
| `com.unity.inputsystem` | 輸入、重綁定 | 0 |
| ~~`com.unity.cinemachine`~~ | Phase 0 改用自製 `CameraFollow2D`（SPEC_CONFLICTS C-18） | — |
| `com.unity.test-framework` | EditMode/PlayMode 測試 | 0 |
| `com.unity.2d.aseprite` | 若美術使用 Aseprite（待確認工具） | 選用 |
| `com.unity.textmeshpro`（Unity 6 內建 uGUI） | 中文點陣字 | 1 |

不安裝：3D 相關、Netcode、Visual Scripting（範本自帶者移除）。

---

## 2. Project Settings

| 設定 | 值 |
|---|---|
| Physics 2D Gravity | (0, 0) |
| Graphics → Transparency Sort Mode | **Custom Axis (0, 1, 0)**（URP：設在 2D Renderer Data） |
| Sprite Sort Point | Pivot（所有 World 層 SpriteRenderer） |
| Color Space | Linear |
| Fixed Timestep | 0.02（50 Hz）；角色移動用 Rigidbody2D + interpolation |
| Enter Play Mode Options | 關閉 Domain Reload（加速迭代）→ 所有 static 須在 `[RuntimeInitializeOnLoadMethod]` 重設 |
| Default Resolution | 1920×1080，Windowed / Fullscreen Window 可切 |
| Run In Background | Off；失焦時暫停（GDD §14.3） |

---

## 3. 攝影機與像素完美

```text
Bootstrap
└── CameraRig
    ├── Main Camera (Orthographic)
    │   ├── PixelPerfectCamera (URP)
    │   │     Assets PPU = 32
    │   │     Reference Resolution = 480 × 270
    │   │     Grid Snapping = Upscale Render Texture
    │   │     Crop Frame = None（整數倍、不加黑邊、可見範圍略擴大，見 C-04）
    │   └── CameraFollow2D：SmoothDamp 0.15 s、目標偏移 +0.6、限制在地圖 Rect 內
    （已實作於 Village 場景；暫未使用 Cinemachine，見 C-18）
```

- Orthographic Size 由 PPC 決定（270 / 2 / 32 = 4.21875）。
- 跟隨目標是 Player 下的 `CameraTarget` 子物件，可依滑鼠瞄準方向偏移 ≤ 1.5 tiles（Phase 1 調手感）。
- 換場景時 Confiner 改指新場景的 `CameraBounds`，並 `InvalidateCache`。
- 室內小地圖：Bounds 小於可見範圍時鎖定置中。

---

## 4. Sorting Layers

| 順序 | Sorting Layer | 內容 | Y 排序 |
|---|---|---|---|
| 0 | `Ground` | 所有地面 Tilemap（Order 0–50） | 否 |
| 1 | `GroundDecal` | 地毯、法陣、腳下陰影、我方地面範圍 | 否 |
| 2 | `World` | 樹幹、建築牆、柵欄、NPC、玩家、敵人、掉落物、投射物 | **是**（Pivot 腳底 + Custom Axis） |
| 3 | `VFX` | 我方 A/B 級特效 | 否（Order 決定） |
| 4 | `Telegraph` | 敵方攻擊預警 | 否；**永遠高於我方 VFX**（GDD §4.4） |
| 5 | `Foreground` | 樹冠、屋頂、屋簷 | 否；`OcclusionFader` 淡出 |
| 6 | `WorldUI` | 傷害數字、互動提示 | 否 |

- 多部件物件（建築牆＋門＋窗）以 `SortingGroup` 包起來，整組用接地 Pivot 排序。
- Foreground 不跟角色做 Y 排序：樹冠永遠在上，玩家在其下方（碰撞框內或背後）時淡出到 40%。
- 高物件**不做成 Tile**（Tile 的 Y 排序需 Individual 模式，成本高且 Pivot 難控）；做成 Prefab 放在 `World`。

---

## 5. 物理層與碰撞

| Physics Layer | 用途 |
|---|---|
| `Player` (6) | 玩家本體（腳底膠囊 0.5×0.24 單位 ≈ 16×8 px） |
| `Enemy` (7) | 敵人本體 |
| `WorldSolid` (8) | Collision Tilemap、樹幹、牆、柵欄 |
| `Water` (4，Unity 內建) | 保留給之後的飛行敵人；目前水域碰撞放在 Collision Tilemap |
| `Hitbox` / `Hurtbox` | 戰鬥判定（Phase 1） |
| `Projectile` | 投射物 |
| `Interactable` (12) | F 互動觸發器 |
| `Trigger` (13) | Zone、轉場、怪潮觸發 |

Hitbox (9)、Hurtbox (10)、Projectile (11) 已預留名稱。

碰撞矩陣在 Phase 0 只啟用：Player↔WorldSolid、Player↔Water、Player↔Interactable、Player↔Trigger。

---

## 6. Tilemap 與碰撞策略

- **權威碰撞 = `Collision` Tilemap**（不渲染）：用除錯 Tile `DBG_block` 繪製，掛 `TilemapCollider2D`，**不使用 Composite**（6000.6.8f1 實測 Composite 產生 0 條路徑，見 SPEC_CONFLICTS C-17）。
  - 好處：美術 Tile 換圖不影響碰撞；橋只要不畫碰撞格就能走；可精確到懸崖立面。
  - Debug Tile 只在 Scene View 顯示（Tilemap Renderer 在 Play/Build 時關閉），**不屬於美術素材**。
- 大型 Prefab 自帶 Collider（只覆蓋接地腳印）。
- 高低差（Phase 0）：上下層同一物理層，懸崖立面畫 `DBG_block`，階梯處留空。真正分層行走（上下層獨立碰撞）列為之後再評估。
- `Markers` Tilemap：`Spawnable` / `NoSpawn` / `ZoneId`，供 Phase 4 `SpawnDirector` 查詢（轉為 bit grid，不在執行期逐格查 Tilemap）。

---

## 7. 程式架構

### 7.1 Assembly Definitions

目前實作（Phase 0）：

```text
Scripts/         Starbind.Gameplay        (Core, Controls, Player, Camera, World, World/Tiles)
Editor/          Starbind.Editor          (Editor only：匯入規則、專案設定、場景建置)
Tests/EditMode   Starbind.Tests.EditMode
Tests/PlayMode   Starbind.Tests.PlayMode
```

Core、Save 等組件在出現第二個使用者時再拆成獨立 asmdef，避免過早切分。命名空間 `Starbind.Controls` 用來避免與 `UnityEngine.Input` 衝突，`Starbind.CameraRig` 用來避免與 `UnityEngine.Camera` 衝突。

### 7.2 Phase 0 會建立的類別（只到移動與世界）

| 類別 | 職責 |
|---|---|
| `GameBootstrap` | 常駐場景初始化（**尚未實作**，加入第二個場景時建立） |
| `SceneTransition`（已實作） | 淡出淡入、載入場景、`SpawnPointId` 定位；目標場景不存在時回報而不報錯 |
| `SpawnPoint` | 場景內命名出生點 |
| `PlayerInputReader` | 包裝 Input Actions，對外暴露 `Move`, `Interact` 等事件/值 |
| `PlayerMotor` | Rigidbody2D 八方向移動（4.5 u/s），不處理戰鬥 |
| `PlayerAnimatorDriver` | 依速度/方向切 4 方向動畫參數（**等正式角色動畫**） |
| `PlayerController`（已實作） | 輸入 → Motor／Interactor、出生點定位 |
| `CameraFollow2D`（已實作） | 跟隨與邊界限制 |
| `VariantTile`、`NeighborMaskTile`（已實作） | 草地變體、256 格自動拼接、懸崖立面、高台側壁 |
| `OcclusionFader` | Foreground 淡出 |
| `Interactable` / `PlayerInteractor` | F 互動（門、路牌） |
| `PixelArtImportRules` (Editor) | 匯入規則（見 ASSET_PIPELINE §4） |

Phase 1 以後的 `AbilityRunner`、`HitResolver` 等依 GDD §13.2。

### 7.3 Input Actions（`Assets/_Game/Input/StarbindControls.inputactions`）

| Action Map | Actions |
|---|---|
| `Gameplay` | Move (WASD/左搖桿), Aim (滑鼠/右搖桿), Attack (左鍵), Dash (Space), Spell1 (Q), Spell2 (E), Overdrive (R), Interact (**F**), Grimoire (Tab), Pause (Esc), Item1 (1), Item2 (2) |
| `UI` | Navigate, Submit, Cancel, Point, Click |

Phase 0 只接 Move、Interact、Pause。重綁定資料存設定檔（GDD §4.3）。

### 7.4 資料：ScriptableObject vs Runtime

- 靜態定義：GDD §13.3 列表；`Assets/_Game/Data/`。
- 執行期狀態：一般 C# 物件（`RunState`、`PlayerRuntimeStats`），**不寫回 SO**。
- 唯一鍵：字串 Id（如 `fire_bolt`），存檔只存 Id，不存 Unity Object 參照。
- Phase 0 只建 `MapDefinition`（場景名、出生點、Bounds、是否安全區）。

### 7.5 事件

- 場景內：C# event / 介面；`OnEnable` 訂閱、`OnDisable` 解除。
- 跨系統戰鬥事件：Phase 1 建 `CombatEventBus`（非全域 static，由 Bootstrap 持有並注入）。

---

## 8. UI 架構

- **uGUI**（Canvas）作為 HUD 與選單：像素對齊控制較成熟，與 Pixel Perfect Camera 搭配簡單。
- Canvas：Screen Space - Camera，`CanvasScaler` = Constant Pixel Size，Scale Factor = 目前整數倍率。
- 中文點陣字：TextMeshPro Bitmap 字型（需授權字型，見 ASSET_GAPS）。
- Phase 0 只有暫停選單骨架與互動提示。

---

## 9. 存檔（Phase 5 實作，Phase 0 只定位置）

- 路徑：`Application.persistentDataPath/save_v{N}.json`。
- 原子寫入：寫 `*.tmp` → `File.Replace`；保留 `*.bak`。
- `saveVersion` + 遷移函式表；校驗用簡單 hash，避免誤讀損毀檔。
- 設定（音量、按鍵）與進度分開存。

---

## 10. 測試

| 類型 | Phase 0 測試 |
|---|---|
| EditMode | 匯入規則驗證（PPU/Filter/壓縮）、Sorting Layer 存在、`MapDefinition` 出生點唯一 |
| PlayMode | 玩家在 Village 對牆移動 2 秒不穿越；走過橋可達西岸；水面不可進入；F 開門進入 Workshop 再返回出生點正確；Camera 不超出 Bounds |
| 證據 | Game View 截圖 → `docs/evidence/phase0/`；測試結果 XML |

命令列執行：`Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults <path>`。
