# 星痕魔女｜專案狀態

- project_identity：Starbind Witch（星痕魔女）
- repo_root：含 Assets/、Packages/、ProjectSettings/ 的本儲存庫根目錄
- engine：Unity 6000.6.8f1，C#
- last_updated：2026-11-24 +08:00
- git_commit：98d8e1f，feat/full-build-draft（盤點起點）
- current_stage：S2 樣本 G2 approved；S3 藍圖與 S4 可逆灰盒已交付；S5 首章驗收進行中
- art_direction_status：approved（A 林緣＋B 戰鬥區）；既有視角、32×32 地磚與參考圖已確定
- golden_sample_status：approved（G2，2026-11-24 使用者確認）
- chapter_1_status：in_progress；三場景可玩草稿，G4 待驗收
- evidence：PROJECT_AUDIT.md、SESSION_LOG.md、evidence/final/
- blockers／open_decisions：G4 首章驗收、素材授權來源，詳見 DECISIONS.md

## task_table

| ID | 狀態 | 依賴與驗收條件 |
|---|---|---|
| AUD-001 | verified | 現有程式、場景、素材與測試證據已核對，問題依優先度列於 PROJECT_AUDIT.md。 |
| ART-001 | verified | 使用相同森林戰鬥構圖提出三案，列出差異、工時與風險，交 G1 選擇。 |
| ART-002 | verified（G2 approved） | G1 選案後製作四類樣本，在引擎內驗收。 |
| WRL-001 | verified（L0 草案） | WORLD_MAP.md、CHAPTER_PLAN.md、ZONE_MANIFEST.csv 共五章、30 個灰盒地標。 |
| MAP-001 | verified（L1） | Editor 路線審圖器與 Dev/ChapterWalkthrough 試走場景；入口、碰撞及雙向返回測試見 evidence/world-graybox/。 |
| SHIP-001 | in_progress | macOS Player Build 與效能採樣；Windows 目標、素材權利與發行驗收待補。 |
| CH1-001 | in_progress | 村莊至 Boss 與回村已有實作，正式切片須補齊內容並通過 G4（G2 已通過）。 |

next_action：G2 已通過；依 PRODUCTION_BACKLOG.md 核對可用素材來源，完成首章人工節奏驗收後送 G4。
- AUD-001 回歸：evidence/audit-2026-11-24/EditMode.xml（51/51）、PlayMode.xml（57/57，同目錄）；道路與水面拼接通過。

- ART-002 送審證據：ART_002_SAMPLES.md、evidence/art-002/；樣本副本 EditMode 51/51、PlayMode 57/57。

- 最新 QA（2026-11-24 16:42 +08:00）：EditMode 56/56、PlayMode 58/58、macOS Player 可啟動。1080p 最大 112.81 ms，待定位。證據見 QA_REPORT.md。
