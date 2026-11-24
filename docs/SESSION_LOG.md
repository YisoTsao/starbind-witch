# 工作階段紀錄

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
