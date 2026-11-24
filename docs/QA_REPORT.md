# 2026-11-24｜世界灰盒與首章 QA

驗收基準為 feat/full-build-draft 的 98d8e1f 加本次工作區變更。原專案使用 Unity 6000.6.8f1，本機以 6000.6.10f1 在獨立副本匯入與執行。副本使用本輪工作區的遊戲素材；ART-002 送審覆蓋圖留在 art-source/。

## 地圖與系統

| 項目 | 範圍與限制 |
|---|---|
| 路線遍歷 | 五章各六個錨點。由入口 BFS 覆蓋全部通路，再循求出的路徑逐格移動至每個錨點。森林／礦坑／湖／觀星台／城塞分別有 544、653、667、845、981 個可走格。 |
| 試走 | 使用開發場景與原魔女 prefab，實測移動受南牆阻擋；從四個出口用 PlayerInteractor 前往下一章，再由入口返回原點。測試使用傳送定位各出口；遊戲中可自行步行。 |
| 首章 | 現有回歸涵蓋 NPC、工坊、戰鬥、祭壇、斷橋、三階段 Boss 與勝敗結算。新增驗證從村莊實際切換 Forest，再以受控傷害推進 Boss，回村後另開 SaveService 從磁碟重讀勝利與碎片。 |
| 工坊 | 魔典台選擇法術後寫入首次配置旗標；首勝前及失敗研究均不記首勝後研究。成功後由磁碟讀回。使用原 completedQuests 字串清單。 |
| 戰鬥可讀性 | 左上通知移至狀態區下方，菇王臉部與前方預警區保持可見。對照 [原畫面](evidence/final/F07_boss_phase1_telegraph.png) 與 [本輪畫面](evidence/world-graybox/boss-notice.png)。 |
| 資料與美術 | 261 筆匯入資產 GUID 唯一，所有路徑存在；30 個區域條目的出口皆有相對入口。重建兩份 CSV 與灰盒 JSON 後雜湊一致。村莊道路與水面各 315,392 像素，拼接遮罩差異均為 0。 |

## 重現方式

- 資料檢查：python3 tools/map/chapter_grayboxes.py、python3 tools/art/asset_manifest.py、python3 tools/art/seam_test.py .
- 測試：對獨立副本執行 Unity -batchmode -projectPath COPY -runTests -testPlatform EditMode（或 PlayMode）-testResults RESULT.xml -logFile LOG。
- macOS 建置：Unity -batchmode -projectPath COPY -executeMethod Starbind.Editor.GameContentBuilder.BuildMacPlayerBatch -buildOut DEST.app -logFile LOG。以內建 -starbindBenchmark -benchmarkSeconds 12 -benchmarkOut RESULT.json 啟動編譯好的 Player；基準擷取移到採樣結束後，以免 PNG 回讀混入幀耗時。
- 人工項目：首章 10–12 分鐘體感、低階 Windows、資產權利、G2 與 G4 狀態見 [製作清單](PRODUCTION_BACKLOG.md) 與 [發行檢查表](RELEASE_CHECKLIST.md)。

## 本輪結果

[EditMode 56/56](evidence/world-graybox/EditMode.xml)、[PlayMode 58/58](evidence/world-graybox/PlayMode.xml)，失敗、跳過均為 0。macOS Build 報告 103.5 MB、0 errors；已從工作區 Builds/mac-20261124/Starbind.app 啟動並執行基準。

| Apple M2、VSync 關、每組 12 秒 | 平均 | p95 | p99 | 最長 | 取樣 |
|---|---:|---:|---:|---:|---:|
| [960×540](evidence/world-graybox/player-960.json) | 0.95 ms | 1.11 ms | 1.50 ms | 32.99 ms | 12,579 幀 |
| [1920×1080](evidence/world-graybox/player-hd.json) | 1.02 ms | 1.20 ms | 1.60 ms | 112.81 ms | 11,797 幀 |

測試持續補怪至 60，兩組最低同屏分別為 52、50。1080p 有超過 16.7 ms 的長幀，原因待逐幀 profiler 定位。這兩次短程測試只代表此裝置與此情境。[Player 戰鬥畫面](evidence/world-graybox/player-combat.png)與[設定畫面](evidence/world-graybox/player-settings.png)來自編譯版。

[礦坑 Game Camera](evidence/world-graybox/mine-play.png)顯示角色與互動提示；五章全圖由 Editor 的渲染攝影機擷取。正式首章的實測與後章 L1 分開驗收。
