# CLAUDE.md — Starbind Witch 專案協作規範

## 重要文件
- 主規格：`docs/GAME_DESIGN_V3.md`，是產品需求唯一準則。
- 階段表：`docs/IMPLEMENTATION_PLAN.md`。
- 素材缺口：`docs/ASSET_GAPS.md`。
- 美術與管線：`docs/ART_DIRECTION.md`、`docs/ASSET_INVENTORY.md`、`docs/ASSET_PIPELINE.md`。
- 世界與技術：`docs/WORLD_STRUCTURE.md`、`docs/TECH_ARCHITECTURE.md`。
- 法術與升級規劃書（含 Mermaid 流程圖）：`docs/SPELL_UPGRADE_SYSTEM.md`；故事線、世界地圖、任務清單：`docs/STORY_WORLD_QUESTS.md`（第 2 章以後的草案已採用並回寫 GDD v3.1 §2.2.1）。
- 規格衝突與待決定：`docs/SPEC_CONFLICTS.md`；目前階段計畫：`docs/PHASE0_PLAN.md`；Phase 0 結果：`docs/PHASE0_REPORT.md`；完整草稿版（Phase 1–5 系統、三個場景）結果：`docs/FULL_BUILD_REPORT.md`；第 2 章～終章（四個章節場景、新學派、任務系統、結局）結果：`docs/CHAPTERS_BUILD_REPORT.md`。
- 草稿美術工具：`tools/art/`（擷取、地形產生、拼接測試、`gen_game_drafts.py` 角色/敵人/VFX/家具、`extract_characters.py` 從 references/tilesets 的 Chibi 角色表切出魔女/王子/12 種村民、`village_decor.py` 村莊裝飾貼花/魔女擺設/路燈/柵欄、`ui_art.py` 9-slice UI 框、升級卡/魔典/任務標記與 32×32 圖示、`spell_vfx.py` 爆炸/天降雷擊/衝擊波/魔法陣等法術特效、`font_charset.py` 點陣字型預熱字集、`chapter_art.py` 第 2 章～終章地形/敵人/Boss/機關/特效/圖示）、`tools/audio/`（草稿音效）、`tools/map/`（village / workshop / forest 配置、`chapters.py` 四章地圖、`validate_chapters.py` 路線與機關可完成性驗證）。七個場景與所有資料由選單 `Starbind → Build All Game Content (draft art)` 重建（舊選單 Build Village Scene 也會呼叫它）。
- 參考圖（唯讀，不得修改/刪除/直接匯入）：`references/`；美術工作檔：`art-source/`（不進 Unity）。

## 開始前
1. 先閱讀上述現有文件及專案 Scene、Prefab、Scripts、Packages。
2. 必須先辨識能重用的既有內容，禁止未經說明重建。
3. 先回報本次預計修改項目，再實作。
4. 如規格與既有實作衝突，列出差異，優先按 v3.0 修正；若會破壞資料則先保留並說明。

## 不可違背的設計
- 唯一玩家角色：魔女。騎士/祭司/弓箭手僅 NPC。
- 真正 2D 俯視 Tilemap、連續走動可探索場景；不要固定競技場、斜45度等角地圖或全場景背景圖。
- 基礎攻擊：法杖物理近戰與蓄力魔彈；Q/E 主動；自動技能獨立觸發；R 共鳴超載；F 互動。
- 三頁魔典：形態、元素、修飾；有白名單、反應預算與上限。
- 美術方向：使用者提供的 Unity 俯視 RPG 參考圖；正式地磚 32×32，獨立道具透明 RGBA。
- 不開發多人、四職業玩家、無限制世界、交易系統。

## Unity 架構與品質
- 首選 Unity 2D URP、Pixel Perfect Camera、2D Tilemap、Input System。
- ScriptableObject 儲存靜態設計資料；Runtime 狀態不得回寫資產本體。
- 玩家、法術、敵人、元素、升級、背包、存檔與 VFX 分離，避免巨大 Manager。
- 主動與自動施法共用 AbilityRunner、HitResolver、DamageResolver。
- VFX 不負責真實傷害；敵人、投射物和 VFX 使用 Pool。
- 元素反應不可無限遞迴；爆炸、跳躍、複寫都有明確上限。
- 所有地圖物件須有正確 Pivot/Sorting/Collider；角色能在屋簷與樹冠下遮擋。
- 盡量以 EditMode 測試純數值邏輯，以 PlayMode 驗證控制/碰撞/Scene。

## 素材驗證
- 不將有文字、框線、棋盤底或展示標題的 AI 概念圖作為正式 Sprite Atlas。
- Ground/Autotile 必須實際無縫拼接；大型物件不限 32×32，但比例及 Pivot 一致。
- 素材缺失時可使用標示過的 Placeholder，列入 `ASSET_GAPS.md`。
- 不要聲稱已達到參考圖品質，除非有真實 Game View 圖供比較。

## 一次只執行一階段
- Phase 0：Unity 基礎與可移動 Tilemap。
- Phase 1：法杖、火球、史萊姆、基礎戰鬥。
- Phase 2：元素反應與自動技能。
- Phase 3：魔典、升級卡。
- Phase 4：森林、怪潮、敵人 AI。
- Phase 5：超載、Boss、局外養成、存檔。
- Phase 6：正式資產、VFX、效能、Build。

## 每次交付格式
1. 本次完成／未完成功能。
2. 實際修改/新增檔案路徑。
3. Play Mode / Editor/Test 執行結果，測試數量及失敗情況。
4. 如何操作驗證與必要設定。
5. 效能數據（若相關）與 Console 錯誤。
6. 已知風險及下一階段候選，不自動延伸。

## 第一個 Prompt
請閱讀 `docs/GAME_DESIGN_V3.md` 與本文件，盤點現有 Unity 專案。先建立 `docs/IMPLEMENTATION_PLAN.md` 和 `docs/ASSET_GAPS.md`，接著只完成 Phase 0 的可走動 Tilemap 與攝影機，不要一次實作整款遊戲。必須提供真實驗收結果。
