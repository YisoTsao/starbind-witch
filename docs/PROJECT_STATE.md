# 星痕魔女｜專案狀態

- project_identity：Starbind Witch（星痕魔女）
- repo_root：含 Assets/、Packages/、ProjectSettings/ 的本儲存庫根目錄
- engine：Unity 6000.6.8f1，C#
- last_updated：2026-11-25 +08:00
- git_commit：98d8e1f，feat/full-build-draft（盤點起點）
- current_stage：G2、G4 approved；S6 第 2 章～終章已做成草稿可玩場景（CHAPTERS_BUILD_REPORT.md），待真人試玩
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
| CH2-5-001 | in_progress（草稿可玩） | 礦坑、湖、觀星台、城塞四章場景、機關、Boss、新學派、任務系統、星盤選關、結局選擇；EditMode 67/69（2 個既有灰盒預覽失敗）、PlayMode 70/70。待真人試玩與正式美術。 |

next_action：使用者試玩第 2 章～終章並回報難度與節奏；之後依 ASSET_GAPS.md 新段落排正式美術、音效與音樂。
- AUD-001 回歸：evidence/audit-2026-11-24/EditMode.xml（51/51）、PlayMode.xml（57/57，同目錄）；道路與水面拼接通過。

- ART-002 送審證據：ART_002_SAMPLES.md、evidence/art-002/；樣本副本 EditMode 51/51、PlayMode 57/57。

- 最新 QA（2026-11-24 16:42 +08:00）：EditMode 56/56、PlayMode 58/58、macOS Player 可啟動。1080p 最大 112.81 ms，待定位。證據見 QA_REPORT.md。
- 效能（2026-11-24 補充）：112.81 ms 長幀已定位為首次 Metal shader 編譯（冷快取，約 150 ms），不是 1080p；暖身後穩定狀態最長幀 15.08 ms。首次預熱未實作；EditMode 灰盒渲染兩例在 6000.6.8f1 失敗，待查。見 QA_REPORT.md。
