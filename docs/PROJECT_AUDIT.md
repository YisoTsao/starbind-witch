# 星痕魔女｜現況盤點 AUD-001

2026-11-24，以 feat/full-build-draft 的 98d8e1f 為基準。產品需求沿用 GAME_DESIGN_V3.md。Phase 1–5 已有可玩草稿，導入總監流程的 S0 代表本次盤點階段。

| 領域 | 判定與可重用內容 | 可查證位置 |
|---|---|---|
| 引擎與入口 | 已存在。Unity 6000.6.8f1、C#；Build 首場 Village，另有 Workshop、Forest。 | ProjectSettings/ProjectVersion.txt、ProjectSettings/EditorBuildSettings.asset、Assets/_Game/Scenes/ |
| 行走與任務 | 已存在。ObjectiveTracker 依訓練命中、Boss、回程門狀態更新目標；既有指引可沿用。獨立地下城場景缺失。 | Assets/_Game/Scripts/World/ObjectiveTracker.cs、Assets/_Game/Tests/PlayMode/FlowPlayTests.cs |
| 戰鬥與成長 | 已存在。主動與自動施法、元素反應、升級與怪潮有程式與測試。 | Assets/_Game/Scripts/Spells/AbilityRunner.cs、Assets/_Game/Scripts/Combat/ReactionResolver.cs、Assets/_Game/Tests/EditMode/CombatLogicTests.cs |
| 資料與存檔 | 已存在。SaveService 寫入帶版本與校驗值的 JSON，保留備份；變更格式前需評估舊檔。 | Assets/_Game/Scripts/Save/SaveService.cs、ProfileData.cs（同目錄） |
| UI | 已存在。HUD、小地圖、魔典、設定與繁中字型有實作與歷史截圖。 | Assets/_Game/Scripts/UI/、Assets/_Game/Resources/Fonts/FusionPixel12_OFL.txt、docs/evidence/final/P02_mac_player_settings_panel.png |
| 像素美術與聲音 | 有雛形。32×32 地磚、草稿道具、角色、法術幀及音效可供試玩。需重做的正式資產逐項見缺口表；來源權利無法由 PNG 判定。 | docs/ART_DIRECTION.md、docs/ASSET_GAPS.md、tools/art/、tools/audio/、docs/evidence/final/S07_redrawn_effect_art.png |
| 發行品質 | 不可確定。歷史 macOS Player 測得 p95 1.90 ms、單幀最高 86.33 ms，樣本僅 12 秒。低階裝置與完整一局體感仍待測。 | docs/evidence/final/benchmark_mac_player.json、docs/FULL_BUILD_REPORT.md |

## 優先處理

1. 美術方向已有俯視、地磚及參考圖約束。下一批以同一森林戰鬥構圖比較可讀性與製作成本，再請使用者選定表現方式。
2. 首章已有 Boss 與回村流程。以實機完整一局驗證目標引導、戰鬥提示與回程，記錄卡點，作為 G4 的玩法證據。
3. 重測首次施法與場景載入，定位 Player 單幀尖峰；保留平均值、p95 和最長幀。

本次測試條件與結果見 SESSION_LOG.md。當前沒有第一章正式品質或後續章節量產的批准紀錄。

已目視對照歷史截圖 F01、S07、P02、V11。F01 的森林入口以道路和路標引導，畫面右上同時承載任務文字與小地圖；S07 的大面積火焰及冰刺接近角落 HUD。ART-001 的三案應保留同一套 HUD、敵人位置與特效時機，以便比較玩家和敵方預警的辨識度。這些截圖不足以驗收移動中的手感。

## 2026-11-24 續查

本輪補齊五章 L0/L1、逐檔資產索引、可試走場景及升級／勝利進度驗證。區名通知遮住菇王的問題已調整。發售權利與人工關卡仍見 DECISIONS.md。範圍與結果分別見 [製作清單](PRODUCTION_BACKLOG.md) 及 [QA 報告](QA_REPORT.md)。
