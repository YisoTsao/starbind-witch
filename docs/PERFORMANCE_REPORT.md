# PERFORMANCE_REPORT.md — World & Visual Overhaul 效能紀錄

> 2026-11-26｜機器：MacBookPro18,3 / Apple M1 Pro（注意：`QA_REPORT.md` 舊數據來自 Apple M2，不可直接比較，本表另測同機 Forest 作基準）
> Unity 6000.6.8f1，macOS Player（Mono），1920×1080 視窗，vSync 關閉，非開發版 Build

## 1. Player 實測（`BenchmarkMode`，60 敵＋自動技能＋超載，12 秒）

指令：`StarbindWitch.app/Contents/MacOS/<exe> -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -starbindBenchmark -benchmarkScene <Scene> -benchmarkSeconds 12 -benchmarkOut <file>.json`
（本次新增 `-benchmarkScene`；紫幻森林在主路 (78, 47) 作戰）

| 場景 | 幀數 | 平均 ms | 平均 FPS | p95 ms | p99 ms | 最長 ms | >16.7 ms 幀 | 最少同屏敵 | 暖身最長 ms | GC 堆 MB |
|---|---|---|---|---|---|---|---|---|---|---|
| Forest（64×64，基準） | 9,534 | 1.26 | 794 | 1.52 | 2.50 | 10.87 | 0 | 50 | 20.7 | 7.6 |
| MoonveilForest（第一輪，單獨區域） | 7,720 | 1.55 | 643 | 1.89 | 2.82 | 10.98 | 0 | 47 | 20.6（冷快取首跑 143.0） | 8.0 |
| 第 1 章＝MoonveilForest（第二輪，預設場景） | 8,011 | 1.50 | 668 | 1.73 | 1.90 | 10.89 | 0 | 50 | 20.1（首跑 22.3） | 7.4 |
| **第 1 章（第三輪：接地陰影＋觸控 UI 程式）第 1 次** | 7,587 | **1.58** | 632 | **1.98** | 2.20 | 12.43 | **0** | 49 | 141.6（冷 shader 快取） | 8.3 |
| **第 1 章（第三輪）第 2 次** | 7,581 | **1.58** | 632 | **1.97** | 2.15 | 9.72 | **0** | 50 | 21.2 | 7.1 |

證據：`docs/evidence/moonveil/benchmark_forest.json`（舊森林基準）、`benchmark_ch1_moonveil.json/.png`（第二輪）、`benchmark_ch1_v3_run1/2.json`（第三輪）。第三輪多了約 0.08 ms，來自空地旁物件的接地陰影（約 600 個額外 SpriteRenderer）。冷 shader 快取的首跑長幀（143 ms）與既有結論相同（首次 Metal shader 編譯，見 `QA_REPORT.md`），第二次執行即消失。

結論：地圖面積 ×4.7、物件約 2,600 個，平均幀時間增加約 0.3 ms，仍遠低於 16.7 ms 預算。

## 2. 場景組成（`moonveil_forest.json`，seed 20261126）

| 項目 | 數量 | 作法 |
|---|---|---|
| 地形格 | 19,200 格 × 8 圖層 | 全部 Tilemap（Chunk 模式）；不產生逐格 GameObject |
| 物件 | 2,588（樹 2,050、灌木/花/岩等 ~490、固定物件 49） | Prefab 實例；依 16×16 格分塊放在 `World/Props/Chunk_x_y` |
| 會淡出的物件 | 565 | 只有靠空地 ≤4 格的樹與高物件；深林樹不掛 OcclusionFader |
| 阻擋碰撞器（物件） | 112 | 森林牆由 Collision Tilemap 負責，深林樹不加碰撞器 |
| Light2D | 56（上限） | 間距 ≥5 格；固定物件、路燈優先 |
| Sprite Atlas | 1（`ATL_moonveil.spriteatlasv2`） | 地形 8 表＋45 物件 |
| 產生＋驗證 | 45–50 ms | Editor 時期，一次性 |

## 3. 本次效能相關改動

- `OcclusionFader`：alpha 已到目標值時不再寫入 `SpriteRenderer.color`（大地圖數百個淡出器，避免每幀弄髒渲染資料）。
- 光源上限與間距：`WorldMapExporter.ChooseLights`。
- 深林樹抽稀（`deepForestKeep` 0.75）與不掛元件。

## 4. 尚未量測／風險

| 項目 | 狀態 |
|---|---|
| Build 大小 | macOS 第三輪 126.6 MB；WebGL 18.4 MB（見 §5）；第二輪 126.3 MB（舊森林退出 Build 後比第一輪 129.2 MB 小；比改版前 103.5 MB 多 22.8 MB，主要為無壓縮的 Moonveil 貼圖）。可評估壓縮格式或縮小 256 遮罩表 |
| 場景載入時間 | 未量測（Editor 內開場景約 1–2 秒體感）；下一步加入載入計時 |
| Windows／低階 GPU | 未量測（本機無 Windows 模組，`TECH_ARCHITECTURE.md` §1） |
| Profiler 細項（Draw call、SetPass） | 未擷取；下一步用 Frame Debugger 確認 Atlas 是否生效（Tilemap 與 SpriteRenderer 合批） |
| 區塊串流 | 未實作；若區域擴大到 256×256 以上再評估 |

## 5. 手機瀏覽器版（WebGL，2026-11-26）

- 建置：`Starbind → Build WebGL (mobile browser)` 或 `Unity -batchmode -buildTarget WebGL -executeMethod Starbind.Editor.GameContentBuilder.BuildWebGLPlayerBatch -buildOut <dir>`。輸出約 **18.4 MB**（Gzip：data 8.5 MB、wasm 10.7 MB），0 errors，建置 3 分 24 秒。
- 架設：任何靜態網站伺服器都可以（已開 decompression fallback，不需設定 `Content-Encoding`；設定了啟動會更快）。區網試玩：在輸出資料夾執行 `python3 -m http.server 8000`，手機連 `http://<電腦 IP>:8000/`。
- 已驗證（本機 headless Chrome、手機視窗 844×390、觸控模擬，`docs/evidence/mobile/`）：載入村莊、第一次觸控後出現觸控 UI、按住虛擬搖桿魔女會移動。
- **未驗證**：真實手機的幀率、發熱、記憶體與載入時間（本機 Chrome 走 Rosetta＋軟體 WebGL，不能代表手機效能）；iOS Safari 的音訊與全螢幕行為。
- 已知：在測試瀏覽器中 URP 後處理 shader（Bloom 等）被回報不支援，後處理不執行（畫面少了泛光，遊戲邏輯不受影響）。需要在真機確認，必要時把 Bloom shader 加入 Always Included 或關閉 WebGL 的後處理。
- 存檔：WebGL 的 `persistentDataPath` 位於瀏覽器 IndexedDB；`SaveService` 每次存檔後呼叫 `FS.syncfs`（`Plugins/WebGL/StarbindFileSystem.jslib`）。清除網站資料會清掉存檔。
