# 系統邊界與驗收入口

| 鏈路 | 狀態與持久化界線 | 驗收入口 |
|---|---|---|
| 輸入 → 玩家 → 戰鬥 | PlayerController 對接移動、普攻、Q/E/R、物品與 F；自動技能依目標觸發。AbilityRunner、CombatWorld 與反應邏輯負責真正命中。 | CombatPlayTests 的法杖、蓄力、衝刺、反應及敵方攻擊測試 |
| 擊殺 → 經驗 → 構築 | 局內成長來自 RunState 與升級選擇；魔典、白名單與反應上限依 SPELL_UPGRADE_SYSTEM。 | 等級提升暫停／選卡、三頁魔典與反應上限測試 |
| 地圖 → 結算 → 研究 | 成功或死亡由 RunManager 結算，材料與勝敗寫入 ProfileData，成功研究才扣費並保存。 | FlowPlayTests 的勝利回村、死亡回工坊與研究支付 |
| 任務與磁碟 | 已完成任務使用字串 ID；SaveService 以校驗、備份與版本保護資料。公開資產保存靜態定義，個別玩家的材料與任務進度保存於個人檔。 | QUEST_DESIGN、EditMode 存檔回歸與 PlayMode 磁碟重讀 |

進一步資料格式與生命週期見 [技術架構](TECH_ARCHITECTURE.md)；獎勵與技能數值以 [產品規格](GAME_DESIGN_V3.md) 為依據。
