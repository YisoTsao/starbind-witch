# FULL_BUILD_REPORT.md — 完整草稿版：村莊 48×40、小屋室內、月影森林、全部戰鬥系統

> 日期：2026-11-21｜Unity 6000.6.8f1｜URP 17.6（2D Renderer）
> 範圍：依使用者指示一次完成 Phase 1–5 的全部系統（與 CLAUDE.md「一次一階段」不同，見 SPEC_CONFLICTS C-20）。
> **美術、音效全部是程式產生的草稿（`*_draft`），不是正式素材，也不宣稱達到參考圖品質。**

---

## 1. 本次完成 / 未完成

### 完成（皆有自動測試或實際畫面）

| 範圍 | 內容 |
|---|---|
| **星露村 48×40**（`Scenes/Village.unity`） | Phase 0 北側區塊（圖 A）放在 (10,20)，向外擴充：西側高台與瀑布、河流南流、**木橋**（橋欄碰撞）、溪西弓箭手洛恩、南路祭司莉亞（對話＋全回復）、**騎士訓練場**（凱恩、4 個不倒木偶、箭靶、柵欄）、東側傳送星盤（回小屋門口）、路牌、森林告示牌、南側**森林出口**。艾拉在小屋門口給主線指引 |
| **魔女小屋室內 14×10**（`Scenes/Workshop.unity`） | 俯視 3/4 牆面（牆頂＋牆面）、木地板、書櫃×2、圓窗、衣櫃、床、研究桌、煉金台、大鍋、魔典台、地毯、餐桌椅、盆栽；門口 F 回村莊。**魔典台**＝Q/E/開局自動法術與法杖/護符配置；**研究桌**＝8 個永久研究節點；**床**＝回復、補藥水、存檔 |
| **月影森林 64×64**（`Scenes/Forest.unity`） | 手工骨架：Z01 入口 → 岔路（西：Z02 蘑菇林道、池塘；東：寶箱支線）→ Z03 星痕祭壇空地（6 石柱）→ Z04 橫越全圖的河，**斷橋**（拉桿修復）與**藤蔓捷徑**（火燒開，涉水過河）→ Z05 菇王棲地（圓形空地、8 石柱、進場後藤蔓封門）。樹牆、碰撞、可生成格、區域名稱 |
| 法杖物理攻擊 | 左鍵短按三連擊 10/12/18（扇形/第三段範圍震擊、擊退、短暫硬直、hit-stop、震動）；連擊視窗與輸入緩衝；命中回魔（每秒上限） |
| 蓄力魔彈 | 按住左鍵（或右鍵直接蓄力）→ 放開，三段 18/26/34，等級越高越大、貫穿越多 |
| Dash | Space，2.6 單位/0.2 秒，CD 2.5 秒，無敵 0.15 秒；牆會擋住；**完美閃避**（被打中瞬間在 Dash 中）→ 下一發魔彈 +10，精英/Boss 招式 +5 共鳴 |
| 主動法術 Q/E | 火球術、分裂火球、穿透冰晶、雷弧落擊、旋風牽引（MP、冷卻、HUD 冷卻顯示） |
| 自動法術 | 追蹤魔彈、冰晶環繞、自動雷脈衝、星火護符；每 0.2 秒找目標、沒目標不施放、每幀預算 2；開局 2 槽，Lv5／Lv9 解鎖第 3／4 槽 |
| 元素與反應 | 燃燒（DoT）、冰緩（-30% 速度）、感電（受傷 +10%）、風；**蒸汽爆裂、超載爆燃、碎晶雷暴（電弧跳躍）、火焰龍捲**；每目標最多 2 個標記、反應消耗標記、同目標同反應 0.5 秒保護、每秒全域預算、深度上限、群體上限 8、龍捲同時最多 3 |
| 魔典超載 R | 共鳴（第三段命中 +2、主動直擊每次施法上限 +2、反應 +8、擊殺每秒上限 +3、完美閃避 +5）；滿 100 啟動 8 秒：冷卻 ×0.75、反應傷害 ×1.35、光環 VFX |
| 三頁魔典 | 形態×元素×修飾以 `SpellFactory` 白名單建立（9 組），不在白名單的組合會被拒絕；Tab 開構築頁 |
| 升級 | 星屑 XP 自動吸附、升級暫停三選一（滑鼠或 1/2/3）、**15 張卡**（前置、排除、自動槽、疊加上限、seed 可重現） |
| 裝備（GDD §9.2） | 新芽／月環／風暴法杖、餘燼護符／霜晶胸針，以 StatModifier 套用 |
| 藥水 | 1：生命藥水 +30、2：魔力藥水 +35；寶箱、掉落、研究可增加 |
| 敵人 | 苔蘚史萊姆（追擊、接觸）、毒孢菇（保持距離、蓄力吐孢子）、樹根守衛（紅色條狀預警 → 衝鋒 → 破防硬直）、迷途幽光（繞圈 → 預警 → 突刺）、孢子精英（五向孢子扇、地面重擊、召喚史萊姆） |
| 怪潮 | `SpawnDirector`：依時間 0–12 分鐘的 GDD §10.2 目標同屏數、依區域換怪種、畫面外且距離 ≥ 9、只在可生成格；**同屏上限 60**；6 分鐘或祭壇怪潮後出現精英；Boss 戰停止一般生成 |
| 星痕祭壇 | F 觸發 26 隻怪潮，清完給書頁＋額外升級＋精英 |
| 月蝕菇王 | 三階段（100–70%：瞄準孢子彈、毒圈；70–35%：＋召喚、環形孢子；35–0%：加速、交錯預警區，招式後弱點暴露 2 秒受傷 ×1.5）；所有大招前搖 ≥ 0.7 秒且有地面預警；換階段 1 秒無敵；鏡頭同時框住 Boss；Boss 血條 |
| 結算與回村 | 勝利：光門 → 結果頁 → 回村莊（`spawn_from_forest`），材料 100%；死亡：結果頁 → 回工坊床邊，材料保留 50%；研究點數；Esc 可放棄本局 |
| 存檔 | JSON（版本、FNV 校驗、tmp→replace、.bak 備份、損毀時回退預設值）；研究、配置、裝備、材料、任務旗標 |
| VFX（顯示層，不決定傷害） | 33 組像素 flipbook（物件池）、共用粒子系統 Emit、2D 點光閃光（上限 12）、雷弧 LineRenderer、命中白閃（自製 `Starbind/SpriteFlash` shader）、傷害數字（像素字、同目標 0.15 秒內合併）、hit-stop、鏡頭震動、預警層永遠在我方特效之上 |
| UI | HUD（HP 延遲條、MP、共鳴/超載、等級 XP、區域與目標、計時/擊殺/敵數、Q/E/R/Dash/藥水冷卻、自動槽）、對話框、升級卡、結果頁、暫停選單、Tab 構築頁、魔典台、研究桌 |
| 音效 | 33 個程式合成 SFX（同音效 35 ms 節流、14 聲道池） |

### 未完成 / 限制（誠實列出）

- **正式美術與音樂**：全部是程式繪製草稿；沒有音樂。魔女左向是側面翻轉；沒有攻擊/受擊/死亡專屬動畫。中文使用系統字型（需授權點陣字型）。見 `ASSET_GAPS.md` 新增段。
- **Windows Build**：本機沒有 Windows Build Support；已用 macOS Player Build 驗證獨立啟動與效能（C-27）。
- **真人試玩驗收**：「連續戰鬥 3–5 分鐘好玩」「三種流派有明顯差異」「10–12 分鐘一局節奏」需要人實際玩；自動測試只證明規則與流程正確。
- **按鍵重綁 UI、音量設定 UI、小地圖**未做（設定值已在存檔結構中）。
- GDD §5.6 失控書頁只做「倒轉書頁」；失控超載（§7.2）未做（GDD 標示正式版候選）。
- 你的 Unity 編輯器開著這個專案，所以建置與測試在暫存鏡像專案執行，產物（場景、Prefab、資料、`.meta`）已複製回 repo。回到 Unity 時會重新匯入；若 Village 場景正開著，請選擇重新載入。

## 2. 修改 / 新增檔案

**工具（新增）**：`tools/art/pixlib.py`、`tools/art/gen_game_drafts.py`、`tools/audio/gen_sfx.py`、`tools/map/village.py`、`tools/map/workshop.py`、`tools/map/forest.py`

**資料**：`Assets/_Game/Data/Maps/{village,workshop,forest}.json`；`Data/Spells/SPL_*.asset`（9）、`Data/Enemies/ENM_*.asset`（6）、`Data/Upgrades/UPG_U01–U15.asset`；`Resources/GameDatabase.asset`；`Materials/MAT_vfx_unlit.mat`、`MAT_character_flash.mat`；`Shaders/SpriteFlash.shader`；`Input/StarbindControls.inputactions`（新增 Aim/Attack/ChargeAlt/Dash/Spell1/Spell2/Overdrive/Grimoire/Item1–3）

**美術/音效草稿**：`Art/{Characters,Enemies,Props,VFX,UI,Tilesets/Forest,Tilesets/Interior}/Draft/*.png`、`Art/draft_manifest.json`、`Audio/SFX/Draft/*.wav`；Tile 資產 `Tiles/{Forest,Interior}/*.asset`

**場景與 Prefab**：`Scenes/Village.unity`（重建為 48×40）、`Scenes/Workshop.unity`、`Scenes/Forest.unity`（新）；`Prefabs/Player/Player.prefab`（重建）、`Prefabs/Props/Draft/*`

**程式（`Assets/_Game/Scripts`，約 9,000 行）**
- Core：`GameSession`、`GameDatabase`、`GamePause`、`GameRng`、`ComponentPool`、`SceneConfig`、`BenchmarkMode`、`Team`、`StarbindLayers`（改）
- Combat：`CombatTypes`、`CombatWorld`（HitResolver＋DamageResolver＋反應效果）、`CombatEventBus`、`Health`、`StatusSet`、`ReactionResolver`、`HitRegistry`、`ResonanceMeter`、`StaffCombo`（含 ChargeRules、ManaOnHitLimiter）、`StatBlock`
- Spells：`SpellDefinition`、`RuntimeSpell`（含 `SpellFactory`）、`AbilityRunner`、`AutoCastController`、`Projectile`、`AreaStrike`、`OrbitRing`、`PullField`、`FireTornadoZone`、`SpellPools`
- Enemies：`EnemyDefinition`、`Enemy`、`EnemyBrains`、`BossBrain`、`EnemySpawner`、`SpawnDirector`、`Telegraph`、`HazardZone`
- Player：`PlayerController`（改）、`PlayerMotor`（改：Dash、擊退）、`PlayerVitals`、`PlayerCombat`、`PlayerBuild`、`CharacterAnimator`、`NpcFacing`；Controls：`PlayerInputReader`（改）
- Progression：`RunState`、`RunManager`、`UpgradeDefinition`、`UpgradeDirector`；Save：`ProfileData`、`SaveService`；Items：`Inventory`、`Equipment`
- World：`Pickup`、`SpawnGrid`、`ZoneMap`、`Talkable`、`TrainingDummy`、`AreaExit`、`LocalTeleport`、`WorkshopStation`、`StarAltar`、`Chest`、`BurnableVine`、`BridgeSwitch`、`ReturnPortal`、`BossArena`
- UI：`UIKit`、`Hud`、`GameUI`；VFX：`VfxService`、`SpriteFlipbook`、`HitFlash`、`DamageNumbers`、`CombatFeedback`、`Sfx`；Camera：`CameraFollow2D`（改：震動、Boss 雙焦點）
- Editor：`GameContentBuilder`（新，所有內容與三個場景）、`VillageSceneBuilder`（改為轉呼叫）

**測試**：`Tests/EditMode/CombatLogicTests.cs`（新）；`Tests/PlayMode/PlayTestBase.cs`、`CombatPlayTests.cs`、`FlowPlayTests.cs`、`GameEvidenceCapture.cs`（新）；`VillagePlayTests.cs`（座標改為 48×40，北側區塊位移 (10,20)）；舊 `VillageEvidenceCapture.cs` 由 `GameEvidenceCapture.cs` 取代（Phase 0 圖檔保留在 `docs/evidence/phase0/`）

**文件**：本檔、`IMPLEMENTATION_PLAN.md`（狀態）、`ASSET_GAPS.md`（新增段）、`SPEC_CONFLICTS.md`（C-20～C-28）、`CLAUDE.md`（文件索引與重建選單）

## 3. 測試結果（真實執行）

| 套件 | 結果 | 檔案 |
|---|---|---|
| EditMode | **38 / 38 通過** | `docs/evidence/final/test-results-EditMode.xml` |
| PlayMode | **40 / 40 通過**（含 4 個擷圖測試） | `docs/evidence/final/test-results-PlayMode.xml` |
| 編譯 | 0 錯誤、0 警告 | |
| macOS Player Build | 成功，82.2 MB，0 錯誤 | 建置在暫存資料夾（未放進 repo） |

**EditMode 重點**：狀態最多 2 個且新替舊；7 種元素配對；反應消耗標記；次生傷害與深度上限不觸發反應；0.5 秒保護期；每秒預算；同施法同目標只結算一次；三連擊 10+12+18=40 與輸入緩衝；蓄力 18/26/34；命中回魔上限；共鳴各來源上限與 8 秒超載；白名單；資料庫內容（9 法術、15 卡、敵人 HP 符合 GDD、Shader 可編譯）；StatModifier 疊加/移除；背包上下限；存檔往返、竄改後用備份、全損用預設值；seed 可重現；升級卡前置/排除/槽位/疊加/雷弧上限；XP 升級；怪潮目標值落在 GDD 區間且 ≤ 60；裝備套用與替換。

**PlayMode 重點（實際場景）**：村莊 48×40 與 4 木偶 4 NPC；木橋可過、河與橋欄擋人；NPC 對話與任務旗標；小屋門 → 工坊 → 魔典台/研究桌 → 門 → 回村出生點；工坊牆壁擋人；研究扣材料、加 HP、解鎖法術並存檔；木偶上三連擊 40 傷害；滿蓄力 34；火→冰觸發蒸汽爆裂；Dash 約 2.6 單位；火球擊殺史萊姆並吸收 XP；沒目標時自動法術不施放、有目標才施放；火＋風產生火焰龍捲且會結束；碎晶雷暴跳 3 次；超載 8 秒數值；升級三選一暫停；敵人造成傷害、Dash 中閃避；風暴法杖每 3 次施法電弧；斷橋拉桿、藤蔓只被火燒；祭壇怪潮、清除獎勵、精英出現；60 上限（80 隻請求只成功 60）；一般生成距離 ≥ 9 且在可生成格；Boss 三階段、預警、勝利回村；死亡結算保留 50% 回工坊。原有 Phase 0 測試全數保留並通過。

### 測試中發現並修正的問題

1. **一個檔案放多個 MonoBehaviour** → 場景中元件遺失（森林斷橋拉桿不存在）。拆成一檔一類別（C-28）。
2. **建置器在第一個場景後 Tile 參考失效**（`NewScene(Single)` 卸載未存檔資產），工坊與森林沒有碰撞與地形。改為先存檔、每個場景依路徑重新載入 Tile。
3. **掉落物全部瞬移到 (0,0)**：拾取物把自己的 transform 歸零。已修正，XP 吸附測試通過。
4. **Boss 死後預警回呼仍執行**（物件池重用時會作用在新怪身上）。每個 AI 實例加「存活」守衛。
5. 投射物會停在木偶腳下的碰撞框而不造成傷害；轉場淡入期間按門被忽略導致測試不穩；精英以外的 VFX 數量假設錯誤等測試問題。
6. UI 面板的 Outline 讓半透明底色變成橄欖色 → 面板改為不透明。

## 4. 如何操作驗證

1. Unity Hub 開啟專案（若已開著，切回 Unity 讓它匯入新檔；Village 若提示重新載入請選是）。
2. 開 `Assets/_Game/Scenes/Village.unity` → Play（或 Build Settings 中三個場景都已加入）。
3. 操作：**WASD** 移動、**滑鼠**瞄準、**左鍵短按**三連擊、**按住左鍵/右鍵**蓄力魔彈、**Q/E** 法術、**Space** 閃避、**R** 超載、**F** 互動/對話、**1/2** 藥水、**Tab** 構築、**Esc** 暫停。
4. 建議路線：艾拉（門口）→ 訓練場打木偶（試 Q 火球＋E 旋風＝火焰龍捲）→ 進小屋魔典台換法術與法杖 → 南路出口進森林 → 祭壇 F → 斷橋拉桿或燒藤蔓 → 菇王 → 光門回村 → 研究桌用材料研究。
5. 重建所有內容：選單 **Starbind → Build All Game Content (draft art)**（舊選單 Build Village Scene 也會重建全部）。草稿重產：
   ```bash
   python3 tools/art/gen_game_drafts.py . && python3 tools/audio/gen_sfx.py .
   python3 tools/map/village_north.py . && python3 tools/map/village.py . && python3 tools/map/workshop.py . && python3 tools/map/forest.py .
   ```
6. 測試：Test Runner → EditMode / PlayMode → Run All（擷圖測試類別為 `Evidence`，會寫入 `docs/evidence/final/`）。
7. 效能測試：Player Build 加參數 `-starbindBenchmark -benchmarkOut <檔案.json> -benchmarkSeconds 12`。
8. 存檔位置：`Application.persistentDataPath/save_v1.json`（測試使用系統暫存資料夾，不碰正式存檔）。

## 5. 效能數據與 Console

| 情境 | 結果 | 來源 |
|---|---|---|
| **macOS Player**（MacBook Pro M1 Pro、1920×1080 視窗、VSync 關、60 隻持續補滿、超載＋4 自動法術＋Q/E 持續施放，12 秒） | 平均 **0.88 ms**（≈1135 FPS）、p95 1.08 ms、p99 2.19 ms、最長 120 ms（單一尖峰，推測首次載入/Shader 準備）、最少同屏 51、Managed heap 5.8 MB | `docs/evidence/final/benchmark_mac_player.json`、`P01_mac_player_benchmark_60_enemies.png` |
| Editor 批次模式 PlayMode（60 隻＋冰晶環繞，4 秒） | 平均 0.68 ms、p95 0.83 ms、最長 7.79 ms | `perf_60_enemies.json`（編輯器數據，僅參考） |

- 這是 Apple M1 Pro，不是 GDD 要求的「指定 Windows 硬體」；Windows 低階機需另測。未做 Profiler 的 GC Alloc 逐幀分析。
- Console：測試執行中 0 例外、0 錯誤。Player 記錄中有 URP 後製 Shader（Bloom/DoF 等）被剝除的警告：場景沒有使用後製，屬無害，但可在 URP Asset 關閉後製以消除。

## 6. 截圖（真實 Play mode 渲染）

`docs/evidence/final/`：遊戲攝影機（Pixel Perfect 480×270 → 1920×1080）＋另一台 UI 攝影機合成 HUD（批次模式擷取不到 Overlay UI，故 HUD 以 Screen Space Camera 另行渲染後合成；`P01` 則是 Player Build 真實畫面截圖）。

| 檔案 | 內容 |
|---|---|
| V00 / W00 / F00 | 三個場景 1:1 全圖 |
| V01–V07 | 村莊廣場 HUD、木橋與弓箭手、南路祭司、訓練場、火→冰蒸汽爆裂、法杖第三段、NPC 對話 |
| W01–W03 | 工坊室內、魔典台（法術＋裝備）、研究桌 |
| F01–F11 | 森林入口、火焰龍捲、碎晶雷暴、超載爆燃、60 隻怪潮＋超載＋自動法術、精英預警、菇王第 1/3 階段預警、升級卡、擊敗菇王光門、勝利結算 |
| P01 | macOS Player Build 實際畫面（60 隻戰鬥） |

## 7. 已知風險與下一階段候選（不自動延伸）

- **手感調整**需真人試玩：近戰扇形角度、蓄力門檻 0.22 秒、敵人密度與傷害、Boss HP 2200（C-22）。
- 大量敵人時樹根守衛/幽光的紅色預警可能過多，可能需要「離鏡頭遠的不顯示」或降低透明度。
- 碰撞仍為每格獨立框（C-17），森林約 3,000 個靜態框；Player 實測無問題，但若擴大到 128×128 建議改回 Composite 並在新版本重測。
- 程式產生的 VFX 與角色只是可讀的佔位；正式像素美術（角色 4 方向動作、怪物動作、VFX Sprite Sheet、UI 框、點陣字型、音樂）是最大工作量（Phase 6）。
- 候選下一步：(1) 安裝 Windows Build Support 並做 Windows 低階機 Profiler；(2) 真人試玩 10–12 分鐘並調數值；(3) 正式美術替換（`draft_manifest.json` 的 Pivot/尺寸可直接沿用）；(4) 按鍵重綁與音量 UI、小地圖。

## 8. 追加（2026-11-21）：角色替換為使用者 Chibi 角色表

- **魔女**改用 `CHR_witch_ref`（取自 `Chibi_Mage_Pixel_Art Sprite_Sheet.png`）：Idle 2 幀、Walk 5 幀、施法 5 幀。每一段連擊或施法都會從頭播放動作；蓄力時停在法杖發光的那一幀。
- **王子**（`NPC_prince_ref`，取自騎士表）站在訓練場，交談時會舉劍。
- **12 名村民**（`NPC_villager_01–12_ref`，以村民表換色）分布在村內各處，會小範圍散步。魔女靠近時會停下並轉向她，交談時會招手。
- 工具：`tools/art/extract_characters.py`。Runtime 改動：
  - `CharacterAnimator`：新增 v2 版面，舊的 18 幀版面仍可用。
  - 新增 `NpcWander`。
  - 修改 `NpcFacing`、`Talkable`、`PlayerController`。
- 測試：
  - EditMode 38/38。
  - PlayMode 41/41，新增 `VillagersStrollWithinRangeAndCharactersUseReferenceSheets`。
  - 截圖：`docs/evidence/final/V01`、`V04`、`C01_character_lineup_witch_prince_villagers.png`。
- 限制：角色是 Needs Cleanup 的草稿（ASSET_GAPS 新增段落、SPEC_CONFLICTS C-29/C-30），畫面中比地磚略糊。

## 9. 追加（2026-11-21）：8 方向面向與魔女村莊美術加強

- **方向**：`CharacterAnimator` 斜向（左上/左下/右上/右下）改用側面幀，向左一律翻轉；新增 `CurrentDirection`、`FacingLeft`。PlayMode `EightWayFacingShowsLeftForEveryLeftwardHeading` 驗證 8 個方向（魔女與村民）。
- **地形**：草地 4 → 8 種變化（草葉、深色草叢、苜蓿、小白花）；泥土路加深色路緣、草叢蔓入、碎石；水面加淺灘帶、減少雜點。`seam_test.py` PASS。
- **道具清理**：櫻花樹（去除攤位與鄰樹殘片，左半鏡像補齊）、綠樹 B、岩石 L、發光蘑菇、攤位、水晶噴泉去殘片；三種柵欄依原尺寸重繪。
- **新素材**（`tools/art/village_decor.py`，31 張）：草叢、花叢、苜蓿、碎石、小蘑菇、落花、睡蓮、明暗草地塊、光點；魔女路燈、坩堝、藥草架、掃帚、藥水箱、蠟燭、南瓜（橘/紫）、紫水晶簇。
- **場景**：村莊約 297 個地面貼花、魔女擺設（小屋東側魔女小院、南瓜田、星盤與祭司處水晶與蠟燭）、路燈取代黑色燈柱、程式圓樹換成參考圖樹、邊界樹林雙排。
- **光線**：村莊全域光 0.9、淡紫；路燈、小屋窗戶、水晶噴泉、坩堝、水晶、蠟燭、發光蘑菇加 2D 點光源；`AmbientMotes` 26 個螢火蟲光點（固定數量、只顯示）。
- 測試：EditMode 38/38、PlayMode 43/43（新增 2 個）。前後對照：`docs/evidence/final/V08_before_after_village_polish.png`。仍為草稿，未達參考圖品質（見 ASSET_GAPS 新段落）。

## 10. 追加（2026-11-21）：向左面向修正、地圖與小地圖、介面精修

- **向左面向的真正原因**：角色與敵人使用的 `Starbind/SpriteFlash` Shader 沒有套用 SpriteRenderer 的 flipX（URP 由 Shader 的 `unity_SpriteProps` 翻轉），所以程式設了翻轉但畫面不變。已在頂點 Shader 加入 `UnityFlipSprite`；敵人的左右翻轉也一併修好。新增 PlayMode `FlippedCharacterIsMirroredOnScreen` 直接比對渲染像素（舊 Shader 下此測試失敗、修正後通過）。
- **地圖**：`MapData` 從場景 Tilemap（地面/水/路/崖/碰撞）與大型道具（樹、屋頂、柵欄、岩石、橋）產生每格 4 px 的地圖圖片，並收集 NPC、村民、出口、門、星盤、祭壇。右上角 `MiniMap` 以玩家為中心捲動，箭頭跟隨面向，森林顯示敵人（最多 60）與首領。M 鍵（手把十字上）開啟全螢幕 `WorldMapPanel`：整數倍縮放、區域名稱、NPC 名字、圖例；開啟時暫停遊戲，M / Esc 關閉。
- **介面**：`tools/art/ui_art.py` 產生 9-slice 面板、技能格、量表框、按鍵標籤、量表漸層填色、地圖標記與 14 個 32×32 圖示；HUD 換成頭像＋外框量表、技能格加按鍵標籤，對話框、選單、按鈕改用像素框（`UIKit.Skin`）。
- 測試：EditMode 40/40、PlayMode 46/46。截圖：`V10_witch_facing_down_left.png`、`V09_world_map.png`、`V01_village_square_hud.png`。

## 11. 追加（2026-11-21）：點陣字型、升級卡與魔典、小地圖目標、崖壁、誇張法術特效

- **字型**：所有 UI 改用 Fusion Pixel 12px 繁中（SIL OFL 1.1，可商用，授權檔隨字型附上）。字級自動對齊 12 的倍數；字圖集用 Point 取樣、Canvas 開 pixelPerfect；啟動時預先放入遊戲用到的 727 個字（`tools/art/font_charset.py`）。原本的系統字只在字型遺失時備用。
- **升級卡**（`UpgradeCardView`）：卡頂有依類型上色的緞帶（主動／元素／自動／被動／失控），圖示放在發光寶石座裡，下面依序是名稱、分隔線、說明、書頁等級（0 → 1／上限）和按鍵帽 1/2/3。失控書頁用紅框。卡片在暫停中依序彈出（`UIPop`，使用 unscaled time）。
- **魔典**（`GrimoirePanel`）：分成「形態／元素／修飾」三頁（GDD §5.1），用 1/2/3 或滑鼠切換，重新打開時回到上次的頁面。
  - 形態頁：Q/E 與自動槽的法術卡，含形態、元素與數值。
  - 元素頁：4 種元素的狀態效果與目前使用中的法術，以及 4 種反應和反應傷害倍率。
  - 修飾頁：各法術的穿透／分裂／折返／數量，本局書頁以彩色標籤顯示，下方是數值。
- **小地圖目標**（`ObjectiveTracker`）：不另做任務系統，從現有狀態推導下一步。
  - 村莊：先指向訓練場；打中訓練木樁 5 次後，改指南路出口，HUD 目標文字同步更新。
  - 工坊：指向門。
  - 森林：依序指向菇王棲地、菇王本身、光門。
  - 顯示方式：目標在小地圖內時顯示閃爍星標；在範圍外時，小地圖邊緣出現指向它的箭頭。M 全螢幕地圖也會標出「目標：…」並列入圖例。
- **崖壁**：崖面從木色直柱改成灰紫色圓石錯層堆疊，加上苔蘚與草皮垂邊；高台側壁用同一組色票（`gen_tiles.py`，只改到 cliff/rim 兩張圖）。
- **法術特效**（`SpellFx`＋`VfxService` 擴充，全部只負責顯示，不改戰鬥數值）：
  - 施法：腳下出現依元素上色的旋轉魔法陣，杖尖有星芒閃光、光源與火花。
  - 飛行：投射物帶發光拖尾（TrailRenderer）、跟隨光源（最多 16 個）與粒子。
  - 命中：火系是白熱閃光→火球→煙圈→焦痕＋衝擊波；冰系是冰晶新星；雷系是天降分岔雷擊＋地面電弧＋焦痕；風系是風刃。重擊會加上全螢幕閃光與鏡頭震動。
  - 旋風牽引：風刃環、每次傷害判定都發出衝擊波，結束時爆開。
  - 冰晶環繞：冰屑拖尾，命中時閃光。
  - 蓄力 3 段：衝擊波＋閃光。
  - 法杖第三段：衝擊波＋星芒。
  - 超載 R：大魔法陣、雙衝擊波、4 道天降雷擊與全螢幕白光。
  - 4 種元素反應都加大：超載爆燃有爆炸＋雷擊＋閃光，碎晶雷暴有 5 道粗雷，蒸汽爆裂有白色衝擊波。
  - 上限：每 0.1 秒最多 5 次完整命中特效，其餘用簡化版；光源上限 32（原本 12）；全螢幕閃光 alpha 最高 0.55，可用 `VfxService.ScreenFlashStrength` 關閉。
- **測試**：
  - EditMode 50/50，新增 `UiFontTests`：授權檔、Hinted Raster、字集涵蓋、字級對齊。
  - PlayMode 54/54（含 5 個 Evidence）。新增 `PolishPlayTests` 7 項：HUD 字型、卡片版面與彈出、魔典三頁、小地圖目標／邊緣箭頭方向、森林目標、每個法術的特效與傷害仍生效、超載閃光與關閉開關。
  - 測試結果在 `docs/evidence/final/test-results-*.xml`。
- **效能**（Editor 批次、60 隻怪＋超載＋自動法術）：平均 1.19 ms、p95 1.54 ms、最長 9.6 ms。加特效前是 0.68／0.83／7.8 ms，平均約增加 0.5 ms。Player Build 尚未重測。
- **截圖**：
  - 法術特效：`S01`–`S06`（火球施法／爆炸、雷弧天降雷擊、穿透冰晶、旋風牽引、超載）。
  - 介面：`U01` 升級卡、`U02`–`U04` 魔典三頁。
  - 新字型、崖壁、小地圖目標：`V01`（星標＋邊緣箭頭）、`V09`（全螢幕地圖目標）。其他舊截圖也已用新字型重拍。
  - 全螢幕閃光在獨立的 Overlay Canvas 上，批次截圖拍不到（由測試數值驗證）。
- **已知風險**：
  - 特效全是程式草稿，未達參考圖品質。
  - 超載與大量反應同時發生時畫面很滿，可能蓋過敵人預警，需要真人試玩調整。
  - 崖面仍只有 1 格高。

## 12. 第二輪：閃光設定、特效重繪、2 格高崖面、Player 重測（2026-11-21）

- **全螢幕閃光設定**：暫停選單（Esc）新增「設定」，可選強／弱／關（1/2/3 鍵或滑鼠），立即生效並存進存檔（`ProfileData.screenFlash`）。在設定頁按 Esc 會回到暫停選單。舊存檔沒有這個欄位時預設為「強」（`SaveService` 改用 `FromJsonOverwrite` 讀取，保留欄位預設值）。
- **特效重繪**（`tools/art/spell_vfx.py` v2，仍是程式繪製的草稿，不是美術手繪）：
  - 爆炸 12 幀：預備星芒 → 白熱衝擊幀 → 帶火舌的不規則火球（分層賽璐珞明暗＋深色描邊）→ 中心破洞變煙圈 → 煙團上升散去，碎屑受重力下墜。
  - 冰晶爆 10 幀：地面結冰，雙面受光的冰柱由後往前長出，中央冰塔，最後碎裂飛散。
  - 天降雷擊 9 幀：先導電 → 粗主雷＋地面爆光 → 殘影 → 二次落雷 → 地面電弧與煙。
  - 風刃 8 幀：3 道粗頭細尾的月牙刃＋速度線與葉片。衝擊波 8 幀：亮前緣＋網點尾跡。
- **崖面 2 格高＋轉角**：
  - `TILE_cliff_draft.png` 改成上排（草皮垂邊）＋下排（崖腳、接地陰影），石層跨兩格連續，石塊大小更不規則；瀑布也改成 2 格高。
  - 立面兩端有轉角：陰影中的側面石塊往下接到地面，與高台側壁（rim）相連；高台側壁也改用同一套陰影側石。
  - 新的 `NeighborMaskMode.EastWestNorth`：東西相鄰決定端點，北方是否也是立面決定上排或下排。瀑布改為 `TL_waterfall_tall_draft`，舊的 `TL_waterfall_draft` 已刪除（沒有其他引用）。
  - 地圖（`tools/map/village.py`）：高台立面往上長進高台第一排，高台水池跟著上移一排；噴泉前的獨立崖往下長一排並加上碰撞；被新崖面蓋住的道具往下移。
- **測試**：EditMode 51/51（新增舊存檔缺欄位的預設值）；PlayMode 57/57（新增：設定按鈕／三段閃光／存檔／超載關閉時不閃、2 格高崖面＋左右轉角＋崖腳碰撞＋瀑布、重繪後的特效幀數）。因地圖變動調整兩項舊測試：崖面阻擋測試的起點往下移一格，小地圖草地取樣點從 (30,30) 改到 (30,28)。Console 0 例外。
- **效能**：
  - macOS Player（M1 Pro、1920×1080 視窗、VSync 關、60 隻持續補滿＋超載＋自動法術＋Q/E，12 秒）：平均 **1.52 ms**（約 656 FPS）、p95 1.90 ms、p99 2.38 ms、最長 86 ms（單一尖峰）、最少同屏 54、Managed heap 5.9 MB、0 例外。加特效前的 Player 是 0.88／1.08／2.19 ms，平均多 0.64 ms，仍遠低於 16.7 ms。
  - Editor 批次：平均 1.29 ms、p95 1.73 ms、最長 9.6 ms。
  - Player Build 98.4 MB（前一版 82.2 MB），0 錯誤。
- **截圖**：`S07`（重繪後的 4 種特效並排）、`U05` 暫停選單、`U06` 設定、`V11` 高台 2 格高崖面、`V12` 噴泉前崖面與轉角、`P01` Player 戰鬥、`P02` Player 設定畫面。
  - 注意：批次截圖（`U05`/`U06` 等）裡少數複雜中文字缺像素；同一畫面在真的 Player 視窗（`P02`）完全正常，問題只在截圖工具的 UI 攝影機。
- **已知風險**：特效與崖面仍是程式草稿，沒有達到參考圖品質；崖面只有直線與外轉角，沒有內轉角與曲線；Player 的單一 86 ms 尖峰推測是首次載入／Shader 準備，沒有逐幀分析。
