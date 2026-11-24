# L1 灰盒資料與驗收

在 Unity Editor 選 Starbind → Chapter Graybox Explorer。視窗持有獨立預覽場景，讀取 Editor/Graybox/chapters.json，使用 Grid、Tilemap、TilemapRenderer 與 TilemapCollider2D；正式 Player 的 Build 場景仍為 Village、Workshop、Forest。

- 資料以左下為原點，row 索引對應 y，字元索引對應 x。一格相當於正式地圖一個 32px 地磚。每張圖整區載入，切章清空三層（通路、牆、錨點）再重建。
- 點號表示可走，井號表示有 Grid collider 的可見牆。白點的四向移動以相同 Tilemap 查詢。圖例列出各地標名稱；入口、任務、遭遇、機關、Boss 與支線各有一種顏色。
- 地區 ID 使用 forest、mine、lake、observatory、citadel，地標 ID 為地區與穩定索引組合；跨章目標 ID 明列於 previous/next。入口及出口不與遭遇錨點重合。
- 資料來源為 tools/map/chapter_grayboxes.py。執行 python3 tools/map/chapter_grayboxes.py 可重建 JSON 與 CSV。修改後由 EditMode 的 ChapterRouteTests 驗證格子連通、六個錨點、返回點、渲染與牆型別。PlayMode 另驗既有首章實際移動、場景切換與碰撞。

## 用魔女實際試走

選 Starbind → Open Chapter Walkthrough，進入 Play。此開發場景從同一份章節資料建立五區，以原本的 Player prefab、WorldSolid 碰撞與 LocalTeleport 執行。使用 WASD 行走，到章末或下一章入口靠近標記按 F 往返，右上 HUD 顯示地區／地標。停止 Play 後從森林入口重新開始。正式 Demo 內的出口與存檔未連到這個試走場景。
