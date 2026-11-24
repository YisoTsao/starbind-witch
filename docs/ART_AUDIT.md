# ART-001｜既有畫面與可重用資產

本次以 F07 的魔女、菇王與預警相對位置作為比較基準。S07 提供同屏元素形狀，V11 提供村莊崖面比例，P02 提供實際 Player 的中文與面板觀感。原始證據均位於 docs/evidence/final/。

| 類別 | 處置 | 依據與送審前需驗證項目 |
|---|---|---|
| 地磚、地形與道具 | keep 遮罩與錨點，refine 表面細節 | Assets/_Game/Art/Tilesets、Props、decor_manifest.json；32×32 地磚已有道路與水面拼接測試，新圖仍須跑轉角與遮擋測試。 |
| 角色、NPC、敵人 | refine | Art/Characters、Enemies、character_manifest.json（均位於 Assets/_Game/）；先保留帽尖、菌傘與腳底位置，分方向檢查背景對比、透明邊及攻擊關鍵幀。 |
| 施法與命中 | refine | Assets/_Game/Art/spell_vfx_manifest.json、S07；火與雷的亮度容易接近 HUD，下一階段須測多法術重疊與命中幀。 |
| 物品、HUD、選單與字型 | keep 結構，refine 圖示 | Assets/_Game/Art/ui_manifest.json、U02、P02；用正式字型在遊戲內測小尺寸，保留既有資訊層級。 |
| 光照、轉場 | unknown（獨立驗收） | F07 可檢視日間明暗，單張畫面無法驗證移動閃爍、切換與技能消散。 |
| 正式樣本與授權 | missing（G2 核准）；來源權利待逐項確認 | ASSET_INVENTORY.md、ASSET_GAPS.md；Fusion Pixel 授權隨附，其餘參考圖沿用唯讀規則。 |

概念圖以內建 imagegen 從 F07 產生，保存在 docs/evidence/art-001/。遊戲素材維持原匯入設定。個別素材規格與缺口續用既有清冊。
