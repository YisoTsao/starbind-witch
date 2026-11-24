# CHAPTERS_BUILD_REPORT.md — 第 2 章～終章（草稿可玩）

> 日期：2026-11-24｜Unity 6000.6.8f1｜分支 feat/full-build-draft
> 依據：使用者試玩第一章後確認 G4 通過，要求「直接把後面所有的關卡跟美術都做完」；採用 STORY_WORLD_QUESTS 草案（Q-1）、結局由玩家選擇（Q-2）、美術比照 G2 樣本做程式草稿。一次完成多章與 CLAUDE.md「一次一階段」不同，記為 SPEC_CONFLICTS C-33。
> **美術與音效仍是程式草稿（Needs Cleanup），不宣稱達到參考圖品質。第 2 章以後沒有真人試玩過。**

## 1. 完成／未完成

### 完成

| 範圍 | 內容 |
|---|---|
| G2 樣本合併 | `art-source/art-002/overlay` 的 7 張核准樣本（魔女內描邊、三種森林樹冠、森林草地、火彈、火彈圖示）併入 `Assets`，沿用原 `.meta` 切片 |
| 章節資料化 | 新增 `ChapterDefinition`（場景、Boss、精英、各區怪物比重、解鎖與勝利旗標、首通獎勵、敵人 HP 倍率、終章旗標）。`RunManager`、`SpawnDirector`、`ObjectiveTracker`、HUD Boss 名稱都改讀章節資料；森林原有行為與既有 58 個 PlayMode 測試不變 |
| 第 2 章 星核礦坑（64×80） | 礦口 → 礦車爆破點（推礦車炸開落石）→ 晶礦大廳（晶礦怪潮）→ 斷橋深谷（兩座導電水晶：雷擊或按 F 點亮，開啟晶能屏障）→ 核心礦室；西側死巷救礦工杜克（支線，星砂 20）；深谷斷橋拉桿是另一條路線。Boss **晶核魔像**：雷擊不受傷但充能，滿充能放出有預警的過載新星，之後晶核裂開 3 秒（受傷 x1.5） |
| 第 3 章 星輝湖（80×64） | 湖畔營地（艾拉坦白封印）→ 東西浮島各兩座導電塔，點亮後架起光橋（任一側即可）→ 水晶遺跡怪潮 → 湖心；湖岸殘頁支線（書頁）。Boss **湖影**：用露米 Q 的元素射出鏡像彈、用 E 的元素放環形彈，潛入湖中在預警點浮出，第 2 階段起分出湖影分身 |
| 第 4 章 失落觀星台（64×96） | 山腳 → 折射鏡台（按 F 轉動兩面鏡子，把星光導向接收器）→ 雪道星門開啟 → 觀星儀廣場（機械怪潮）→ 塔頂；雪屋的舊觀測紀錄（支線，星核碎片 3）。Boss **觀星儀守護者**：會在末端折向玩家的折射光束、三向旋轉光束、星隕、召喚透鏡砲台 |
| 終章 黑月城塞（96×96） | 城門 → 西塔（自然）／北塔（月影）／東塔（星痕）三座試煉，三個都完成才開啟中央大廳；東塔旁古塔側室（支線，書頁）。Boss **扭曲魔典**：三階段各用一個學派（荊棘與毒花 → 暗影之手與瞬移 → 星環與複寫露米 Q 元素的彈幕）。勝利後選擇結局 **A 封印／B 共存**，播放對應尾聲並存檔 |
| 敵人 | 22 種新小怪與精英（含新行為「自爆」：晶蟎、虛空火花，0.6 秒地面預警）＋4 個 Boss。Boss 共用 `BossBrainBase`：70%／35% 換階段時無敵 1 秒，大招前搖 ≥ 0.7 秒，牽繩回場中心，召喚有上限 |
| 新學派與法術（首通獎勵） | 第 2 章：光束形態「稜光射線」（直線瞬間貫穿最多 8 名敵人，遇牆截斷）、自然「荊棘毒花」（自動，中毒持續傷害）；第 3 章：月影「暗影之手」（詛咒：受傷 +15%）、迴響修飾「月影迴響」（0.3 秒後 60% 回聲，最多 2 次）；第 4 章：星痕「星彈」（命中後反彈 2 次，每次 80%）。新增反應只有「野火蔓延」（火＋自然，燃燒擴散給 8 名以內敵人），同樣受反應預算、保護期與目標上限約束 |
| 升級卡 | U16–U20（稜光聚焦、荊棘蔓延、深影詛咒、重重迴響、星界連鎖），只在擁有對應法術時出現 |
| 失控超載（GDD §7.2） | 第 4 章通關後研究桌出現「禁書：失控超載」。共鳴 ≥ 50 未滿時按 R，以 HP 換提前超載（代價 = 缺少共鳴 x 0.3，上限 15；付完須剩 ≥ 30% HP；冷卻 30 秒；通知顯示實際扣除） |
| 任務系統（Q-3） | `QuestDefinition`＋`QuestLog`，任務狀態從既有旗標推導，舊存檔進度不丟。19 筆主線與支線；按 J 或暫停選單開啟任務日誌 |
| 傳送星盤（Q-4） | 村莊星盤改為地區選擇器：小屋門口＋所有已解鎖章節（顯示是否通關），1–6 數字鍵可選。通關後回到星盤旁 |
| 劇情 | 艾拉、凱恩、莉亞、洛恩、王子依進度換台詞（最後達成的階段優先），對話時完成對應任務旗標；兩種結局後艾拉與王子各有不同台詞 |
| 草稿美術 | `tools/art/chapter_art.py`：4 套地形（地面、路面與水面自動拼接、不可通行頂面）、26 張敵人與 Boss（22 種小怪與精英＋4 Boss）、36 張機關與裝飾、7 組法術與彈幕特效、5 個法術圖示、雪花環境粒子 |
| 地圖 | `tools/map/chapters.py`：房間位置與連線沿用已驗收的 L1 灰盒；所有機關所在的走道段都是直線，確保一個阻擋物就能完整封住 |

### 未完成／限制

- **真人試玩**：第 2 章～終章沒有人實際玩過，節奏、難度倍率（1.3／1.6／1.9／2.2）、每局時長和 Boss 手感都需要你試玩後調整。
- **美術**：全部是程式草稿。不可通行區只有頂面，沒有 3/4 側壁；多種小怪是森林敵人換色；Boss 沒有招式專屬動畫。詳見 ASSET_GAPS.md 新段落。
- **音效／音樂**：新 Boss 與機關沿用既有草稿音效，沒有新音效，也沒有音樂。
- **Windows Build**：本機仍只有 macOS 模組。
- **既有問題**：`ChapterRouteTests` 在 forest、lake 兩個灰盒預覽渲染失敗（只渲出 4 種顏色）。改動前的版本同樣失敗，只影響編輯器的灰盒預覽工具，不影響遊戲；本機只剩 6000.6.8f1，上一輪 56/56 是用 6000.6.10f1 跑的。

## 2. 修改／新增檔案

**程式（新增）**：`Progression/ChapterDefinition.cs`、`Progression/QuestDefinition.cs`（含 QuestLog）、`Enemies/BossBrainBase.cs`、`Enemies/ChapterBosses.cs`（GolemBrain、ShadowBrain、GuardianBrain、GrimoireBrain、BomberBrain）、`World/PowerGate.cs`、`World/ConductiveCrystal.cs`、`World/MineCart.cs`、`World/BeamPuzzle.cs`（含 BeamTracer）、`World/BeamMirror.cs`、`World/TrialSeal.cs`、`World/RegionSelector.cs`（含 EndingChoice）

**程式（修改）**：`Combat/{CombatTypes,CombatWorld,Health,ReactionResolver,ResonanceMeter}.cs`、`Core/{GameDatabase,GameSession,SceneConfig}.cs`、`Enemies/{BossBrain,Enemy,EnemyBrains,EnemyDefinition,EnemySpawner,SpawnDirector}.cs`、`Player/PlayerCombat.cs`、`Progression/{RunManager,UpgradeDirector}.cs`、`Spells/{AbilityRunner,Projectile,RuntimeSpell,SpellDefinition}.cs`、`UI/{GameUI,GrimoirePanel,Hud,MapData}.cs`、`VFX/{CombatFeedback,SpellFx}.cs`、`World/{ObjectiveTracker,StarAltar,Talkable}.cs`、`Editor/GameContentBuilder.cs`

**工具**：新增 `tools/art/chapter_art.py`、`tools/map/chapters.py`、`tools/map/validate_chapters.py`；修改 `tools/map/{village,forest,chapter_grayboxes}.py`、`tools/art/seam_test.py`

**資料與內容**：`Data/Maps/{mine,lake,observatory,citadel}.json`（新）、`village.json`、`forest.json`；`Data/Chapters/CH_*.asset`（5）、`Data/Quests/Q_*.asset`（19）、`Data/Spells/SPL_*.asset`（+5）、`Data/Enemies/ENM_*.asset`（+26）、`Data/Upgrades/UPG_U16–U20.asset`；`Resources/GameDatabase.asset`；`Tiles/{Mine,Lake,Observatory,Citadel}/`；`Art/chapter_manifest.json` 與各 `Draft/` 新圖；`Prefabs/Props/Draft/` 新 Prefab；`Resources/Fonts/FusionPixel12_charset.txt`

**場景**：新增 `Scenes/{Mine,Lake,Observatory,Citadel}.unity`；重建 `Village`、`Workshop`、`Forest`；Build Settings 共 7 個場景

**測試**：新增 `Tests/EditMode/ChapterContentTests.cs`、`Tests/PlayMode/ChapterPlayTests.cs`；`GameEvidenceCapture.cs` 新增 `CaptureChapters`；`CombatLogicTests` 的法術數、卡片數改為 14、20

**文件**：本檔、GAME_DESIGN_V3（v3.1）、STORY_WORLD_QUESTS、DECISIONS、SPEC_CONFLICTS（C-33～C-35）、CHAPTER_PLAN、ZONE_MANIFEST.csv、ASSET_MANIFEST.csv、ASSET_GAPS、IMPLEMENTATION_PLAN、PROJECT_STATE、QA_REPORT、SESSION_LOG、CLAUDE.md

## 3. 測試結果（真實執行，Unity 6000.6.8f1，專案副本 batchmode）

| 套件 | 結果 | 說明 |
|---|---|---|
| EditMode | **67 / 69 通過**，2 失敗，0 跳過 | 失敗的 2 個是既有的 `ChapterRouteTests` forest、lake 灰盒預覽渲染（見上方限制）；新增 13 個章節測試全過 |
| PlayMode | **70 / 70 通過**，0 跳過 | 既有 58 個＋新增 11 個章節測試＋1 個章節擷圖 |
| 路線驗證 `tools/map/validate_chapters.py` | 4 / 4 PASS | 每章：關閉所有機關時到不了 Boss；依序打開「可到達觸發點」的機關後，Boss、所有觸發點與支線都能到達 |
| 拼接 `tools/art/seam_test.py` | PASS | 村莊＋四章共 10 張自動拼接圖，各 315,392 個內部像素，差異 0 |
| 編譯 | 0 錯誤 | 19 個既有的過時 API 警告（舊檔案） |
| macOS Player Build | 成功，117.1 MB，0 errors，7 個場景 | |

新增 PlayMode 測試涵蓋：四個章節場景各自以正確章節開局（尺寸、敵人 HP 倍率、可走動）；礦車、導電水晶（雷擊與 F 兩種方式）、斷橋拉桿；晶核魔像吸收雷擊、充能、過載與弱點暴露；湖的光橋與湖影模仿 Q 元素；觀星台轉鏡開門；城塞三塔試煉開啟大廳；四個 Boss 都有預警、三階段、擊敗後出現光門；通關解鎖獎勵法術、回到星盤、星盤列出下一章並能傳送、任務日誌可開啟；終章結局選擇寫入存檔且艾拉台詞改變；稜光射線貫穿三個敵人、月影迴響命中兩次並詛咒、星彈反彈到第二個敵人、荊棘毒花中毒與野火蔓延；失控超載扣除顯示的 HP。

## 4. 如何操作驗證

1. Unity 會重新匯入新增的場景、資料與圖片（若村莊場景開著，選擇重新載入）。內容可用選單 `Starbind → Build All Game Content (draft art)` 重建；美術先執行 `python3 tools/art/chapter_art.py .`，地圖執行 `python3 tools/map/chapters.py .`。
2. 從村莊進入第一章並擊敗月蝕菇王（或使用已通關的存檔）。回村後和艾拉對話，按 J 看任務日誌，到東側傳送星盤選「星核礦坑」。
3. 每章擊敗 Boss 後走進光門，結果頁會列出新書頁；在工坊魔典台裝上新法術。
4. 第 4 章通關後，研究桌出現「失控超載」。
5. 終章完成三塔試煉 → 擊敗扭曲魔典 → 結果頁 → 選擇結局。
6. 實際畫面：`docs/evidence/chapters/`（每章全圖、入口 HUD、機關、Boss 預警，加上星盤選單、任務日誌、結局選擇）。

## 5. 效能與 Console

- macOS Player、Apple M2、1920×1080、VSync 關、森林 60 隻同屏 12 秒：平均 1.34 ms、p95 1.70 ms、p99 2.68 ms、最長 12.09 ms，沒有超過 16.7 ms 的幀；暖身期（首次生成與施法）最長 19.91 ms。章節通用化沒有造成效能退步。新章節沒有另外做效能基準。
- Console：PlayMode 70 個測試沒有未預期的 Error 或 Exception（Unity Test Framework 遇到未預期的 Error 會讓測試失敗）；Player 記錄沒有錯誤。

## 6. 已知風險與下一步候選（不自動延伸）

- 需要你試玩第 2 章～終章，回報難度、時長、卡關點；之後才調整數值。
- 草稿美術的明顯缺口：不可通行區沒有側壁、多種換色小怪、Boss 沒有招式動畫、沒有新音效與音樂。
- 首次進入新場景時，shader 冷快取的停頓（約 150 ms）仍未處理（QA_REPORT 已記錄）。
- 灰盒預覽測試在 6000.6.8f1 的兩個失敗待查。
- Windows 建置、素材授權仍待處理。
