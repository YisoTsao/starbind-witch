# 工作階段紀錄

## 2026-11-26 +08:00｜WVO-003 美術第三輪與手機版

- 使用者要求：紫色調月蝕菇王、紫幻藤蔓與拉桿、地形與樹木都要調整；參考圖授權確認；第 1 章要能在手機玩；最後 commit 並 push。
- 授權：使用者表示「參考圖授權確認」，記入 DECISIONS WVO-2（approved，使用者陳述；Claude 無法查證）。
- 美術：新增 `tools/art/moonveil_creatures.py`（程式繪製菇王 6 幀、藤蔓＋燃燒 3 幀＋燒毀、拉桿關/開、4 種接地陰影）；`moonveil_art.py` 草地改 `meadow_fill`、石板改 `flagstone_fill`（週期 Voronoi）、物件 `despeckle`；拼接檢查 0 錯誤。產生器把藤蔓／拉桿精靈寫進地圖 JSON；`BurnableVine` 播放燃燒幀；`BridgeSwitch` 可設定拉桿開啟圖；Boss 石階閘門改用紫幻藤蔓；`GameContentBuilder.AddContactShadow` 為紫幻森林的地面物件加陰影。
- 手機：`TouchControls`（On-Screen Stick/Button → 虛擬手把）、`Hud.SetTouchLayout`、`PlayerInputReader.TouchMode`、`PlayerCombat` 自動瞄準、手把方向鍵綁定道具 1–3、對話可點擊、WebGL 建置入口（Gzip＋解壓後備、橫向）、`SaveService` 在 WebGL 用 jslib 同步 IndexedDB。
- 驗證（專案副本，Unity 6000.6.8f1）：EditMode 80/80、PlayMode 77/77；macOS Player 126.6 MB；Benchmark 1080p 60 敵 avg 1.58 ms、p95 1.97 ms、最長 12.43 ms（首跑；第二次 9.72 ms），無超過 16.7 ms 的幀。證據 `docs/evidence/moonveil/M13`–`M18`。

## 2026-11-26 +08:00｜紫幻森林取代第 1 章（WVO-002）

- 使用者指示：用紫幻森林取代第 1 章月影森林，全部做完不中斷。WVO-1 approved。
- 章節 forest.scene→MoonveilForest、標題紫幻森林、Boss 區首領空地、任務 M1-03 文字；village.py/village.json 出口；ObjectiveTracker、GameSession、RunManager 後備、BenchmarkMode 預設；舊 Forest.unity → Scenes/Dev/Legacy/Forest_v1.unity（不建置）。
- 產生器 v1.1：Stairs 格、brokenBridge（拉桿在來路側）、vineGateAt（量測走廊寬）、地面貼花、依水流判斷橋向、森林碎塊清除、閘門關閉可達性驗證。美術：柱狀岩懸崖、石階、斷橋、直向橋、魔法陣。
- 測試改寫：Combat/Flow/Polish/Evidence 全部改用紫幻森林與動態座標；新增 WorldStudioTests 3 例。修正 ChapterRouteTests 灰盒預覽在 Retina 只讀 1/4 畫面（既有 2 例失敗）。
- 結果（副本，6000.6.8f1）：EditMode 80/80、PlayMode 75/75（含 Evidence）；macOS Player 126.3 MB 0 errors；第 1 章 1080p 60 敵平均 1.50 ms、p95 1.73 ms、最長 10.89 ms。截圖 docs/evidence/final/（第 1 章各畫面已換成紫幻森林）與 docs/evidence/moonveil/。
- 未做：真人試玩、正式美術（含紫色調 Boss）、音樂。

## 2026-11-26 +08:00｜World & Visual Overhaul 第一階段（WVO-001）

- 基準 e97dcc9（feat/full-build-draft）。使用者要求依 `references/01`–`04` 分析並實作 Witch World Studio 與紫幻森林。參考圖位於 `references/`（指示寫 `docs/references/`）。
- 新增：`tools/art/moonveil_art.py`（8 地形表＋45 物件，拼接/開放邊檢查 0 錯）、`Scripts/World/Studio/`（Biome/POI/Blueprint SO、WorldGenerator、WorldMapData、Validate）、`PeriodicTile`、`Editor/WorldStudio/`（World Studio 視窗、ArtImporter、Exporter、Pipeline、MoonveilPreset）、MoonveilForest 場景、文件 10 份。修改：GameContentBuilder（Studio 圖層、光、分塊、BuildRegionScene）、HUD 區名面板、MapData、SceneConfig/RunManager/GameSession（區域開場與目標覆蓋）、OcclusionFader、BenchmarkMode（-benchmarkScene）。
- scratchpad 副本以 6000.6.8f1 驗證：EditMode 75/77（2 例為既有灰盒預覽失敗）、PlayMode 非 Evidence 68/68（含新 4 例；橋測試第一版不穩已改為依碰撞找通道）、CaptureMoonveil 通過。macOS Player 129.2 MB 0 errors；1080p 60 敵 Moonveil 平均 1.55 ms、p95 1.89 ms、最長 10.98 ms（M1 Pro）。截圖 docs/evidence/moonveil/。
- 未做：真人試玩、正式美術、紫幻森林專屬敵人/Boss/任務、村莊出口改接。

## 2026-11-25 +08:00｜第 2 章～終章（CH2-5-001）

- 基準 0fd0154。使用者試玩第一章，G4 通過；要求把後續關卡與美術全部完成。決定：採用草案（Q-1）、結局玩家選擇（Q-2）、美術做 G2 風格程式草稿。一次完成多章記為 SPEC_CONFLICTS C-33。
- G2 核准樣本併入 Assets。章節資料化（ChapterDefinition），新增四章場景、機關、22 種小怪與精英＋4 Boss、自然／月影／星痕學派、光束與迴響、野火蔓延、U16–U20、失控超載研究、任務系統、星盤地區選擇器、結局選擇。修正兩個實作中發現的 bug：星彈反彈重複命中同一目標、建置器 Prefab 同名覆蓋。
- 在 scratchpad 副本以 6000.6.8f1 建置：Build All 7 個場景、macOS Player 117.1 MB 0 errors。EditMode 67/69（ChapterRouteTests forest/lake 灰盒預覽，改動前即失敗）、PlayMode 70/70；validate_chapters.py 4/4、seam_test 10/10 PASS。1080p 基準 p95 1.70 ms、最長 12.09 ms。擷圖在 docs/evidence/chapters/。
- 未做：真人試玩、正式美術／音效／音樂、Windows。

## 2026-11-24 +08:00｜SHIP-001 長幀定位

- 基準 b888dca。`BenchmarkMode` 加入長幀紀錄與 30 幀暖身（`warmup_max_ms` 另記），只在 -starbindBenchmark 時生效。
- 在 scratchpad 副本以 6000.6.8f1 建置 macOS Player（103.4 MB、0 errors）。112.81 ms 是首次 Metal shader 編譯：清快取 149–151 ms、熱快取約 18 ms；排除暖身後 1080p／540p 最長幀 15.08／12.12 ms，沒有超過 16.7 ms 的幀。證據在 evidence/perf-2026-11-24/。
- PlayMode 58/58；EditMode 54/56（ChapterRouteTests forest、lake 渲染門檻；修改前的基準版本同樣失敗）。
- 下一步候選：載入時 shader 預熱；查明灰盒渲染測試在 6000.6.8f1 的差異。

## 2026-11-24 +08:00｜G2 核准

- 使用者本人確認 ART-002 四類樣本（角色、森林、法術、UI）通過，G2 改為 approved。更新 DECISIONS.md、PROJECT_STATE.md。未修改程式與場景，未重跑測試。
- 下一步：S5 首章 QA 與 G4 人工驗收；正式素材替換前須先確認來源授權。

## 2026-11-24 +08:00｜WRL-001、MAP-001 與首章 QA

- 基準 feat/full-build-draft / 98d8e1f。對照總監 v2.0，依既有 GDD、故事草案及 G1 選擇補世界、地圖、任務、資產、UI、動畫與製作規範。詳見 PRODUCTION_BACKLOG.md、QA_REPORT.md 及 RELEASE_CHECKLIST.md。
- 實作 tools/map/chapter_grayboxes.py、Editor/Graybox、World/ChapterGraybox、Dev/ChapterWalkthrough.unity；產出五章資料、30 筆地標及實際 Editor／Play Mode 畫面。新增 ChapterRouteTests、ChapterWalkthroughTests。
- 修改 GameUI、GameSession、FlowPlayTests、BenchmarkMode 與字型預熱字集。驗證工坊任務旗標、出村至回村與磁碟重讀；修正菇王預警中央通知，將效能截圖移出計時區間。tools/art/asset_manifest.py 建立 261 筆資產清單。
- 本輪 EditMode 56/56、PlayMode 58/58，失敗與跳過均為 0。macOS Build 103.5 MB、0 errors。Apple M2、1080p、12 秒 p95 1.20 ms、最長 112.81 ms；單幀尖峰待 profiler 定位。
- 所有最新引擎報告與地圖畫面在 evidence/world-graybox/。專案指定版本為 6000.6.8f1，本機驗證使用 6000.6.10f1 副本。
- G1 已定；G2 仍待本人確認，G4、後章正式內容授權、商用素材來源與 Windows 實機尚待處理。

## 2026-11-24 +08:00｜ART-002 引擎樣本送審

- 98d8e1f，feat/full-build-draft。使用者選定「A 林緣＋B 戰鬥區」，G1 approved。更新 ART_DIRECTION.md、PROJECT_STATE.md、DECISIONS.md 與本紀錄。
- 新增 art-source/art-002/ 的樣本製作腳本及七張覆蓋圖、ART_002_SAMPLES.md、evidence/art-002/ 的引擎截圖與測試報告。在獨立專案副本依原 GUID 匯入。
- PNG 檢查包含尺寸、逐格內容、角色與投射物透明邊、草地拼接邊色。Unity 6000.6.10f1 以 -batchmode -runTests -testPlatform PlayMode 測試 57/57 通過；同一副本 EditMode 51/51 通過，兩組均無失敗或跳過。
- 核對 F01、F07、S01、U02。同屏仍可見未精修的藍樹及預警上的區名浮字。下一步為使用者檢視四類樣本，確認 G2 或指定修訂。

## 2026-11-24 +08:00｜ART-001 美術探索

- 以 98d8e1f 的 F07 為底，內建 imagegen 生成三張概念比較，產出 ART_AUDIT.md、ART_DIRECTION_OPTIONS.md、evidence/art-001/{A,B,C}.png，更新 PROJECT_STATE.md、DECISIONS.md。
- 三張均經 Pillow 完整解碼及目視構圖檢查。差異與估時記於選案文件。本次只新增概念圖及文件，Unity 測試未重跑；上一輪 EditMode 51/51、PlayMode 57/57。
- 下一步待 G1 選案；G2 樣本與 G4 首章驗收待辦。

## 2026-11-24 +08:00｜AUD-001 首次基線

- 起點為 feat/full-build-draft 的 98d8e1f；開始前工作區已有 .gitignore 修改。
- 專案 docs/ 原無三份續作紀錄。讀取 v2.0 套件模板後，以現有程式、Build 場景清單、草稿素材、實機截圖及 XML 建立 PROJECT_STATE.md、PROJECT_AUDIT.md、DECISIONS.md 及本紀錄。
- 驗收範圍為盤點與回歸。遊戲程式、場景、存檔格式和專案指定引擎版本均沿用現況。
- 已逐例解析 docs/evidence/final/test-results-{EditMode,PlayMode}.xml。2026-11-21 的紀錄分別為 51/51、57/57。
- 本次執行 `PYTHONDONTWRITEBYTECODE=1 python3 tools/art/seam_test.py .` 通過。道路、水面各檢查 315,392 個內部像素，透明遮罩差異皆為 0。
- Unity 重跑方式：將 `git archive 98d8e1f` 解至暫存目錄，以本機 Unity 6000.6.10f1 對該副本依序執行 `Unity -batchmode -projectPath "$copy" -runTests -testPlatform "$mode" -testResults "$copy/$mode.xml" -logFile "$copy/$mode.log"`，mode 為 EditMode、PlayMode。專案原指定 6000.6.8f1。
- 目視核對既有 F01、S07、P02、V11；真人整局試玩與四類正式樣本驗收仍待進行。
- 下次執行 ART-001。保留既定參考與遊戲設計，提出三份同場景比較後送 G1；本次未新增任何人工批准。
- 本次 16:07 +08:00 完成重跑。EditMode 51/51、PlayMode 57/57，失敗與跳過均為 0。報告保存在 docs/evidence/audit-2026-11-24/{EditMode,PlayMode}.xml。另核對三個啟用場景的路徑與 .meta GUID，3/3 相符，並檢視本輪副本中的森林入口與重繪特效畫面。
