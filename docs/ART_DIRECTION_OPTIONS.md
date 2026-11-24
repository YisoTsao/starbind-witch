# ART-001｜月蝕菇王施放直線攻擊時，你先看到什麼？

[原始遊戲畫面](evidence/final/F07_boss_phase1_telegraph.png)中，魔女位於菇王正下方，兩側橘紅邊線標示即將命中的範圍。三案沿用同一鏡頭、角色比例、Boss、預警與 HUD 配置，並把火、冰水、雷放在相同區域比較形狀。元素並列僅供美術選案。遊戲持續沿用 32×32 地磚、480×270 虛擬解析度、白天俯視構圖與既有按鍵。

| | [A 林間日光](evidence/art-001/A.png) | [B 剪影林地](evidence/art-001/B.png) | [C 魔典木刻](evidence/art-001/C.png) |
|---|---|---|---|
| 第一眼 | 花叢、受光的樹冠和苔石圍住空地，最接近既有村落參考。 | 平坦草地讓魔女腳下的危險邊線最容易追蹤。 | 版畫枝葉與金色符印將戰鬥畫面連成魔典書頁。 |
| 森林與村莊 | 樹冠以三階明暗成團，村屋庭院沿用花草與暖色屋頂。 | 角形樹冠配大片草色，村屋由屋脊與門洞剪影區分。 | 蕨葉與樹皮排線集中於林緣，村屋門楣呼應木刻紋樣。 |
| 角色與怪物 | 保留帽尖、菌傘比例，用局部暗邊與柔和塊面塑形。 | 以選擇性單像素暗線強化帽尖、杖端、菌傘；攻擊前搖先改剪影。 | 同比例角色保留亮臉，衣角和菌傘才加刻紋；讓杖端在施法時仍可見。 |
| 法術與動畫 | 火焰旋起、水弧收尖、雷枝短閃；亮點集中於命中幀。 | 火滴、水月牙、雷折線各用大色塊；以伸縮與斷幀表達命中。 | 符印先描邊、元素再出現、最後斷環；多重施法時須限制同屏符線。 |
| HUD 與後續章節 | 保留紫金框；按地區替換植物群與石材。 | 保留欄位與字型，以簡化邊框搭配清楚的技能圖示；新章靠地標剪影辨認。 | 卷邊集中於魔典與章節入口；以每章特有生物替換紋樣。 |
| 可重用與風險 | 沿用地形遮罩與素材錨點；亮葉與火花可能搶走對敵方前搖的注意。 | 地形變體與法術圖示容易重用；村莊和森林可能過於相似。 | 沿用九宮格與原 HUD 佈局；細線易於縮放時消失，邊飾可能遮擋敵人。 |
| 代表樣本估時 | 4–6 人日，主要用於樹冠、草地變體和逐幀高光。 | 3–4 人日，主要用於剪影試讀與元素動作。 | 6–9 人日，主要用於可拼接木刻、符印時序與 UI 縮放。 |

估時假設一名像素美術以既有 Unity 管線製作一段森林、魔女與一敵、三種短特效及一組 HUD，含一次引擎內修訂。以上為提案估算。現階段推薦 **A 的森林外緣搭配 B 的戰鬥空地**：探索時保有參考圖的植物層次，進入戰鬥後，草地降低亮點密度，讓預警邊線保持連續。

## 驗收與選擇

三張 PNG 均為 1672×941 的概念圖。逐張檢視可見角色、菇王、長條預警、三種元素與 HUD。生成圖的字形和特效尺寸略有差異，B 缺少中央區名，C 增加了持書姿態與裝飾邊框。這些差異僅供構圖解讀；正式 HUD、角色操作與字型仍以專案為準。移動可讀性須留到引擎樣本驗證。

G1 可選 A、B、C，或指定混合的部位。選定後才進行 ART-002 的角色、環境、VFX、UI 四類樣本並送 G2。

<details><summary>內建 imagegen 使用的三組提示詞（共同輸入為 F07）</summary>

Edit the reference screenshot as a 16:9 pixel RPG art-direction concept. Match EXACT camera, positions and scale of witch, mushroom boss, rectangular red vertical attack telegraph and entire HUD. Keep bright daytime green forest clearing 32px tiles virtual 480x270. Keep the center lane uncluttered. Same three small spell studies in the arena: fire on left, icy water crescent on right, lightning upper right. No titles. A: sunlit botanical clusters. Rounded overlapping leafy crowns, moss and tiny flowers concentrated at margins, sculpted 3-tone volumes, warm daylight.

Edit the reference screenshot as a 16:9 pixel RPG art-direction concept. Match EXACT camera, positions and scale of witch, mushroom boss, rectangular red vertical attack telegraph and entire HUD. Keep bright daytime green forest clearing 32px tiles virtual 480x270. Keep the center lane uncluttered. Same three small spell studies in the arena: fire on left, icy water crescent on right, lightning upper right. No titles. B: crisp silhouette pixel art. Large quiet flat grass clusters, cut-paper angular canopy silhouettes, selective dark 1px outlines, sharply legible graphical spell shapes.

Edit the reference screenshot as a 16:9 pixel RPG art-direction concept. Match EXACT camera, positions and scale of witch, mushroom boss, rectangular red vertical attack telegraph and entire HUD. Keep bright daytime green forest clearing 32px tiles virtual 480x270. Keep the center lane uncluttered. Same three small spell studies in the arena: fire on left, icy water crescent on right, lightning upper right. No titles. C: illuminated witch's field grimoire. Decorative folk woodcut forest margins, grouped engraved hatching, curling fern motifs and subtle stepped shadow bands, magical sigils with hollow centers. Keep daylight.

</details>
