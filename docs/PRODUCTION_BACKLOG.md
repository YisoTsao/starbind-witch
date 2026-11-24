# 導演流程缺口與製作順序

| 工作 | 實際落點 | 完成條件／尚缺的證據 |
|---|---|---|
| S0 稽核與願景 | PROJECT_AUDIT、GAME_DESIGN_V3 §1–3、IMPLEMENTATION_PLAN | 既有可玩迴圈為村莊準備、森林構築、菇王、戰利品及工坊研究。產品範圍沿用 GDD。 |
| S1 探索 | ART_DIRECTION_OPTIONS、ART_AUDIT、DECISIONS | 三案交付；使用者選定 A 林緣＋B 戰鬥區。 |
| S2 樣本與資產規範 | ART_002_SAMPLES、ART_DIRECTION、ASSET_PIPELINE、SPELL_VFX_ART_DIRECTION、ASSET_MANIFEST.csv | 四類樣本已有引擎證據。批製前須取得樣本確認；角色參考圖商用權另須來源證明。 |
| S3 世界藍圖 | STORY_WORLD_QUESTS、WORLD_MAP、CHAPTER_PLAN、ZONE_MANIFEST.csv | 既定 Demo 與後續章節草案已區分；後續劇情 Q1–Q6 仍保留。 |
| S4 全章灰盒 | Chapter Graybox Explorer、ChapterWalkthrough 試走場景、MAP_DESIGN_STANDARD、evidence/world-graybox/ | 五個模型可巡覽，實際首章另有 Village、Workshop、Forest。後四章玩法機關仍為灰盒錨點。 |
| S5 首章 QA（優先） | FlowPlayTests、evidence/world-graybox/、ASSET_GAPS | 確認合法素材，再做 G2 樣本以外的正式替換。測首玩指引、祭壇／斷橋、三種構築、Boss 預警與完整一局的節奏。記錄耗時、死亡原因與卡點，交 G4 人工驗收。 |
| S6 後章製作 | STORY_WORLD_QUESTS §4.4 | 依 G4 與後章授權，先做礦坑的救援與爆破、一場遭遇及回村存檔，再逐章驗收。 |
| S7 發行 | FULL_BUILD_REPORT、ASSET_INVENTORY、ASSET_GAPS | macOS 可測；Windows 匯出與低階裝置基線、發售權利證明、音樂及首章以後內容仍待完成。 |

## 功能與規格的權威來源

- 世界觀、角色與章節地理沿用 STORY_WORLD_QUESTS 和 WORLD_STRUCTURE。終章選擇、後續地區傳送與王子主線身分仍待決。
- 魔法白名單、反應保護與卡池沿用 SPELL_UPGRADE_SYSTEM、GAME_DESIGN_V3 與 TECH_ARCHITECTURE。戰鬥、道具和存檔已有各自程式與回歸。
- 美術格線、取樣、尺寸與遮擋沿用 ART_DIRECTION 和 ASSET_PIPELINE；樣本尺寸、施法幀、UI 與限制見 ART_002_SAMPLES。ASSET_MANIFEST 依真實 Unity GUID 建立，空白框格代表現有作者清單未提供該欄位；授權仍須查來源。
- UI 的操作、字型、魔典與地圖驗收以 GAME_DESIGN_V3、FULL_BUILD_REPORT 及 PlayMode 為準。鍵鼠操作可測；手把、不同長寬比與真人可讀性須另外驗收。

## 首章人工驗收記錄項

從乾淨存檔進村、交談並到工坊後走南路進森林；用升級卡構築一次、完成祭壇、修橋或燒藤、擊敗菇王、由光門結算回村。重新啟動後核對研究材料與勝利數。第二局以不同流派測教學資訊與難度。記錄裝置、解析度、完成時間、卡住的位置與截圖，再決定 G4。自動測試使用受控狀態與直接傷害加速部分戰鬥，真人體感另行記錄。
