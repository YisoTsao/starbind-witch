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
