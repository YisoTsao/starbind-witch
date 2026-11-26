# 星痕魔女｜專案狀態

- project_identity：Starbind Witch（星痕魔女）
- repo_root：含 Assets/、Packages/、ProjectSettings/ 的本儲存庫根目錄
- engine：Unity 6000.6.8f1，C#
- last_updated：2026-11-26 +08:00
- git_commit：feat/full-build-draft（WVO-001～003 於 2026-11-26 一併提交並推送）
- current_stage：G2、G4 approved；第 2 章～終章草稿可玩；**World & Visual Overhaul 完成到第二輪**：Witch World Studio v1.1；**紫幻森林已取代月影森林成為第 1 章**（WVO-1 approved）；第三輪：紫色月蝕菇王、紫幻藤蔓／拉桿、草地與石板 v2、接地陰影；**手機瀏覽器版（WebGL＋觸控）**；WVO-2 授權由使用者確認
- art_direction_status：approved（A 林緣＋B 戰鬥區）；既有視角、32×32 地磚與參考圖已確定
- golden_sample_status：approved（G2，2026-11-24 使用者確認）
- chapter_1_status：G4 approved（2026-11-24）；chapters_2_5_status：草稿可玩，未經真人試玩
- evidence：PROJECT_AUDIT.md、SESSION_LOG.md、evidence/final/
- blockers／open_decisions：第 2 章～終章真人試玩、正式美術與音樂、素材授權、Windows 建置，詳見 DECISIONS.md

## task_table

| ID | 狀態 | 依賴與驗收條件 |
|---|---|---|
| AUD-001 | verified | 現有程式、場景、素材與測試證據已核對，問題依優先度列於 PROJECT_AUDIT.md。 |
| ART-001 | verified | 使用相同森林戰鬥構圖提出三案，列出差異、工時與風險，交 G1 選擇。 |
| ART-002 | verified（G2 approved） | G1 選案後製作四類樣本，在引擎內驗收。 |
| WRL-001 | verified（L0 草案） | WORLD_MAP.md、CHAPTER_PLAN.md、ZONE_MANIFEST.csv 共五章、30 個灰盒地標。 |
| MAP-001 | verified（L1） | Editor 路線審圖器與 Dev/ChapterWalkthrough 試走場景；入口、碰撞及雙向返回測試見 evidence/world-graybox/。 |
| SHIP-001 | in_progress | macOS Player Build 與效能採樣；Windows 目標、素材權利與發行驗收待補。 |
| CH1-001 | verified（G4 approved） | 村莊至 Boss 與回村，使用者試玩通過。 |
| WVO-001 | verified（系統）／in_progress（美術） | Witch World Studio v1.1、紫幻森林 160×120、HUD 區名面板。見 WORLD_GENERATOR_SPEC.md、evidence/moonveil/。 |
| WVO-002 | verified | 紫幻森林取代第 1 章：章節/任務/村莊出口/目標追蹤/Benchmark 改指向 MoonveilForest；斷橋拉桿與藤蔓移植；石階、柱狀岩懸崖、直向橋、魔法陣、森林碎塊清除、閘門可達性驗證；舊森林移到 Scenes/Dev/Legacy。修正灰盒預覽測試（Retina 下只讀到 1/4 畫面）。**EditMode 80/80、PlayMode 75/75（含 Evidence）**；macOS Player 126.3 MB 0 errors；第 1 章 1080p 60 敵 p95 1.73 ms。 |
| WVO-003 | verified | 美術第三輪＋手機版：菇王紫色版、藤蔓（燃燒 3 幀）、拉桿、接地陰影（PROC-DRAFT）、草地/石板 v2、樹木像素清理；觸控操作（虛擬搖桿、拇指技能區、互動/選單鈕、自動瞄準）；WebGL 建置入口與 IndexedDB 存檔同步。**EditMode 80/80、PlayMode 77/77（含 Evidence 與 2 例觸控測試）**；macOS Player 126.6 MB 0 errors；1080p 60 敵 p95 1.97 ms。WebGL 18.4 MB，headless Chrome 手機視窗觸控驗證（evidence/mobile/）；真機未測。 |
| CH2-5-001 | in_progress（草稿可玩） | 礦坑、湖、觀星台、城塞四章場景、機關、Boss、新學派、任務系統、星盤選關、結局選擇；EditMode 67/69（2 個既有灰盒預覽失敗）、PlayMode 70/70。待真人試玩與正式美術。 |

next_action：
1. 使用者在手機瀏覽器試玩第 1 章（WebGL 版，見 PERFORMANCE_REPORT §5 的架設方式），回報按鈕大小、幀率與發熱。
2. 需要原生 App 時：Unity Hub 加裝 Android（含 SDK/NDK/JDK）或 iOS 模組（另需 Xcode），再建置。
3. 手繪正式美術（地形、樹、菇王）需美術人員；產生器 v2：東西向懸崖、空地邊緣次格平滑。
（原事項保留：第 2 章～終章真人試玩、正式音效與音樂。）
- AUD-001 回歸：evidence/audit-2026-11-24/EditMode.xml（51/51）、PlayMode.xml（57/57，同目錄）；道路與水面拼接通過。

- ART-002 送審證據：ART_002_SAMPLES.md、evidence/art-002/；樣本副本 EditMode 51/51、PlayMode 57/57。

- 最新 QA（2026-11-24 16:42 +08:00）：EditMode 56/56、PlayMode 58/58、macOS Player 可啟動。1080p 最大 112.81 ms，待定位。證據見 QA_REPORT.md。
- 灰盒預覽 2 例失敗（2026-11-24 起）已於 2026-11-26 修正：測試在 Retina 只讀取 512×512 而預覽為 2 倍尺寸；另補預覽相機 aspect 與 unlit 材質。
- 效能（2026-11-24 補充）：112.81 ms 長幀已定位為首次 Metal shader 編譯（冷快取，約 150 ms），不是 1080p；暖身後穩定狀態最長幀 15.08 ms。首次預熱未實作；EditMode 灰盒渲染兩例在 6000.6.8f1 失敗，待查。見 QA_REPORT.md。
