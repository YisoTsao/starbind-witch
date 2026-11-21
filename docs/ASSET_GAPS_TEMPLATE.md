# ASSET_GAPS.md — 美術資產缺口追蹤模板

> 開發時請複製為 `docs/ASSET_GAPS.md`，由 Claude Code 依現有檔案盤點與更新。

| Asset ID | 類別 | 目標尺寸/格式 | 當前檔案 | 狀態 | 遮擋/碰撞/動畫需求 | 來源/授權 |
|---|---|---|---|---|---|---|
| TILE_GROUND_01 | 草地 Tile | 32×32 RGBA | — | 待確認 | 可行走；無縫 | — |
| TILE_PATH_01 | 道路 Autotile | 32×32/Tile | — | 待確認 | 邊緣轉角規則 | — |
| TILE_WATER_01 | 水面/河岸 | 32×32/Tile | — | 待確認 | 水面禁止通行 | — |
| TILE_CLIFF_01 | 懸崖 | 32×32/Tile | — | 待確認 | Collider | — |
| PROP_TREE_01 | 櫻花樹 | 獨立透明 PNG | — | 待確認 | 樹幹碰撞，樹冠前景 | — |
| PROP_HOUSE_01 | 魔女小屋 | 模組化/獨立PNG | — | 待確認 | 入口 F、門碰撞 | — |
| SPR_WITCH_01 | 魔女 | 96×96 Canvas/Frame（建議） | — | 待確認 | Idle/Walk/Cast/Hit | — |
| SPR_SLIME_01 | 史萊姆 | 64×64 Canvas（建議） | — | 待確認 | Move/Hit/Death | — |
| VFX_FIRE_01 | 火球/爆炸 | RGBA Sprite Sheet | — | 待確認 | 發射/命中 | — |
| VFX_REACTION_01 | 火焰龍捲 | RGBA Sprite Sheet | — | 待確認 | start/loop/end | — |

## 驗收 checklist
- [ ] 圖片尺寸、格線正確
- [ ] 透明通道正確（非棋盤底）
- [ ] 無字、無標籤、無分隔框線
- [ ] 與基準像素密度、輪廓與調色盤一致
- [ ] 地磚實測可拼接（適用者）
- [ ] 樹木、屋子、家具 Pivot 與 Collider 合理
- [ ] 素材商用授權來源已記錄
