using Starbind.Player;
using Starbind.World;
using UnityEngine;
using UnityEngine.UI;

namespace Starbind.UI
{
    /// <summary>Full-screen world map (M): the whole MapData at the largest integer scale that fits, zone
    /// names, NPC names, exits / doors / star plate, the witch's position, and a legend.</summary>
    public static class WorldMapPanel
    {
        public static int LastScale { get; private set; }

        public static void Build(RectTransform panel, MapData d)
        {
            var area = new Vector2(panel.sizeDelta.x - 440, panel.sizeDelta.y - 150);
            int scale = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(area.x / d.Texture.width, area.y / d.Texture.height)));
            LastScale = scale;
            var size = new Vector2(d.Texture.width, d.Texture.height) * scale;
            float cell = MapData.Px * scale;

            var frame = UIKit.Image(panel, "MapFrame", UIKit.Panel, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, -20), size + new Vector2(36, 36));
            UIKit.Skin(frame, "UI_slot_draft");
            var mapRt = UIKit.Rect(frame.transform, "Map", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var raw = mapRt.gameObject.AddComponent<RawImage>();
            raw.texture = d.Texture;
            raw.raycastTarget = false;

            // zone names (skip the catch-all zones that cover most of the map)
            var zm = ZoneMap.Current;
            if (zm != null && zm.Zones != null)
                foreach (var z in zm.Zones)
                {
                    if (z.rect.width * z.rect.height > d.Width * d.Height * 0.4f)
                        continue;
                    UIKit.Label(mapRt, "Zone", z.displayName, 24, new Color(1f, 0.95f, 0.8f, 0.95f), TextAnchor.MiddleCenter,
                        Vector2.zero, new Vector2(0.5f, 0.5f), z.rect.center * cell, new Vector2(260, 34));
                }
            foreach (var p in d.Pois)
            {
                var pos = p.Position * cell;
                UIKit.Icon(mapRt, "Poi_" + p.kind, MiniMap.MarkerSprite(p.kind), Vector2.zero, pos, 3f);
                // names for people and the star plate; doors / exits sit inside named zones already
                if ((p.kind == "npc" || p.kind == "star" || p.kind == "altar") && !string.IsNullOrEmpty(p.label))
                    UIKit.Label(mapRt, "Name", p.label, 20, p.kind == "npc" ? UIKit.Gold : UIKit.Text, TextAnchor.LowerCenter,
                        Vector2.zero, new Vector2(0.5f, 0f), pos + new Vector2(0, 16), new Vector2(200, 26));
            }
            if (ObjectiveTracker.TryGet(out var goal, out var goalLabel))
            {
                UIKit.Icon(mapRt, "Quest", "UI_marker_quest_draft", Vector2.zero, goal * cell, 4f);
                UIKit.Label(mapRt, "QuestName", "目標：" + goalLabel, 24, new Color(1f, 0.92f, 0.5f), TextAnchor.LowerCenter,
                    Vector2.zero, new Vector2(0.5f, 0f), goal * cell + new Vector2(0, 34), new Vector2(320, 34));
            }
            var pl = PlayerController.Current;
            if (pl != null)
            {
                var m = UIKit.Icon(mapRt, "Player", "UI_marker_player_draft", Vector2.zero, (Vector2)pl.transform.position * cell, 4f);
                var anim = pl.GetComponentInChildren<CharacterAnimator>();
                var f = anim != null ? anim.Facing : Vector2.up;
                m.rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg - 90f);
                UIKit.Label(mapRt, "You", "你在這裡", 22, new Color(1f, 0.95f, 0.6f), TextAnchor.UpperCenter, Vector2.zero, new Vector2(0.5f, 1f),
                    (Vector2)pl.transform.position * cell - new Vector2(0, 26), new Vector2(200, 30));
            }

            // legend
            var legend = UIKit.Image(panel, "Legend", UIKit.Panel, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-40, -20), new Vector2(320, 580));
            UIKit.Skin(legend, "UI_slot_draft");
            UIKit.Label(legend.transform, "Title", "圖例", 28, UIKit.Gold, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(280, 40));
            (string sprite, string text)[] rows =
            {
                ("UI_marker_player_draft", "魔女（你）"), ("UI_marker_npc_draft", "重要人物"), ("UI_marker_villager_draft", "村民"),
                ("UI_marker_door_draft", "建築入口"), ("UI_marker_star_draft", "出口 / 星盤 / 祭壇"),
                ("UI_marker_enemy_draft", "敵人"), ("UI_marker_boss_draft", "首領"), ("UI_marker_quest_draft", "目標（下一步）"),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                float y = -90 - i * 56;
                UIKit.Icon(legend.transform, "Key", rows[i].sprite, new Vector2(0, 1), new Vector2(54, y), 3f);
                UIKit.Label(legend.transform, "Text", rows[i].text, 22, UIKit.Text, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(92, y), new Vector2(210, 40));
            }
            UIKit.Label(panel, "Hint", "M / Esc 關閉地圖", 22, UIKit.Muted, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 22), new Vector2(400, 30));
        }
    }
}
