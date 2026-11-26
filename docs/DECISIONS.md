# 已確認決策與人工關卡

| ID | 狀態 | 決定或通過條件 | 依據 |
|---|---|---|---|
| D01 | approved（既有約束） | 魔女為唯一玩家角色，採連續走動的 2D 俯視 Tilemap，正式地磚 32×32。 | CLAUDE.md、docs/GAME_DESIGN_V3.md |
| D02 | approved（既有約束） | 參考圖唯讀，草稿與正式素材分開，沿用三頁魔典及既有戰鬥操作。 | CLAUDE.md |
| G1 | approved | 使用者選定「A 林緣＋B 戰鬥區」。 | 2026-11-24 對話選案、docs/ART_DIRECTION_OPTIONS.md |
| G2 | approved | 角色、森林、法術、UI 四類樣本由使用者本人確認通過（2026-11-24）。 | pixel-rpg-game-director v2.0 |
| G3 | 按需確認 | 重大重構、引擎遷移、破壞存檔等須逐項取得批准。 | pixel-rpg-game-director v2.0 |
| G4 | approved | 使用者本人試玩第一章後確認通過，無修改要求（2026-11-24）。 | 2026-11-24 對話 |
| Q-1 | approved | 第 2 章～終章採用 STORY_WORLD_QUESTS §2.3／§4.4 草案（晶核魔像、湖影、觀星儀守護者、扭曲魔典），回寫 GDD v3.1。 | 2026-11-24 對話 |
| Q-2 | approved | 結局由玩家二選一：A 封印／B 共存（多結局）。 | 2026-11-24 對話 |
| Q-3 | approved | 建立正式任務系統（QuestDefinition＋QuestLog），沿用既有任務旗標 ID 以相容舊存檔。 | 隨 Q-1 採用草案建議 |
| Q-4 | approved | 傳送星盤改為地區選擇器（小屋門口＋已解鎖章節）。 | 隨 Q-1 採用草案建議 |
| Q-5 | approved | 主角正式名稱定為「露米」。 | 隨 Q-1 採用草案建議 |
| Q-6 | approved | 王子維持 NPC，在第 4 章帶來國王委託。 | 隨 Q-1 採用草案建議 |
| ART-CH | approved（範圍） | 第 2 章以後美術做程式草稿，比照 G2 樣本風格；正式素材待日後替換。 | 2026-11-24 對話 |
| WVO-1 | approved（2026-11-26） | 使用者指示「用紫幻森林取代第 1 章的月影森林」：第 1 章場景改為 `Scenes/Regions/MoonveilForest.unity`，地名改為紫幻森林；舊 64×64 森林移到 `Scenes/Dev/Legacy/Forest_v1.unity`（不建置、不出貨）；斷橋拉桿與可燒藤蔓移植到紫幻森林；全部森林測試改寫到新地圖。 | 2026-11-26 對話 |
| WVO-2 | approved（2026-11-26，使用者陳述） | 紫幻森林物件以 `references/04` 切圖縮放量化為 REF-DRAFT、地形以 `references/03` 取色程式繪製。使用者在對話中表示「參考圖授權確認」，本專案據此視 `references/01`–`04` 的衍生使用為已授權。**Claude 無法查證來源與授權條款**（圖像疑為 AI 生成，權利取決於生成工具條款與提供者）；書面授權證明由使用者保存，發行前仍建議法務確認。REF-DRAFT 標示保留，方便日後替換。 | 2026-11-26 對話；ART_DIRECTION §8.2 |
| WVO-3 | 本次採用 | 地圖在 Editor 產生並提交 JSON（可 diff、同 seed 可重現）；Runtime 不產生地圖。 | WORLD_GENERATOR_SPEC §1 |
| WVO-4 | 本次採用（2026-11-26） | 「第一章做成手機版也可以玩」：本機 Unity 只有 Mac／WebGL 模組，先做**手機瀏覽器 WebGL 版**＋觸控操作（虛擬搖桿、HUD 技能槽變拇指按鈕、互動與選單按鈕、攻擊自動瞄準最近敵人）。原生 Android／iOS 需要在 Unity Hub 加裝模組（iOS 另需 Xcode）後才能建置，程式不需再改。 | 2026-11-26 對話；UI_ART_DIRECTION §5 |
| WVO-5 | 本次採用（2026-11-26） | 使用者指定調整的美術：月蝕菇王改為紫色調、紫幻藤蔓（含燃燒 3 幀）與拉桿、地形與樹木。菇王／藤蔓／拉桿／接地陰影改為**程式繪製 PROC-DRAFT**（不取自參考圖，`tools/art/moonveil_creatures.py`）；草地與石板重畫；樹木保留 REF-DRAFT 但做像素清理並加接地陰影。**不是手繪美術**：真正手繪版需美術人員。 | 2026-11-26 對話；MISSING_ASSETS |
