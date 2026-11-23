using System.Collections.Generic;
using Starbind.Enemies;
using Starbind.Player;
using Starbind.World;
using UnityEngine;
using UnityEngine.UI;

namespace Starbind.UI
{
    /// <summary>Top-right minimap: the scene's MapData scrolled so the witch stays in the centre, with
    /// markers for NPCs, exits, the star plate, enemies and the boss, plus the current objective
    /// (ObjectiveTracker): a blinking star when it is on the minimap, an arrow on the rim pointing at it
    /// when it is off the minimap. Display only.</summary>
    public sealed class MiniMap : MonoBehaviour
    {
        public const float CellPx = 12f;   // UI px per map cell (3 UI px per map texel)
        const int MaxEnemyMarkers = 60;

        RectTransform view, content;
        RawImage map;
        Image playerMarker;
        readonly List<Image> poiMarkers = new List<Image>();
        readonly List<Image> enemyMarkers = new List<Image>();
        Image bossMarker;
        Image questMarker, questArrow;
        MapData shown;

        public MapData Shown => shown;
        public int VisibleEnemyMarkers { get; private set; }
        public RectTransform PlayerMarker => playerMarker != null ? playerMarker.rectTransform : null;
        public RectTransform QuestMarker => questMarker != null ? questMarker.rectTransform : null;
        /// <summary>True while the objective is off the minimap and the rim arrow is shown.</summary>
        public bool QuestArrowShown => questArrow != null && questArrow.gameObject.activeSelf;
        public RectTransform QuestArrow => questArrow != null ? questArrow.rectTransform : null;

        public void Build(RectTransform frame)
        {
            view = UIKit.Rect(frame, "View", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            view.offsetMin = new Vector2(18, 18);
            view.offsetMax = new Vector2(-18, -18);
            view.gameObject.AddComponent<RectMask2D>();
            var bg = view.gameObject.AddComponent<Image>();
            bg.sprite = UIKit.White;
            bg.color = new Color(0.06f, 0.04f, 0.1f, 1f);
            bg.raycastTarget = false;
            content = UIKit.Rect(view, "Map", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Vector2.one);
            map = content.gameObject.AddComponent<RawImage>();
            map.raycastTarget = false;
            for (int i = 0; i < MaxEnemyMarkers; i++)
            {
                var m = UIKit.Icon(content, "Enemy", "UI_marker_enemy_draft", Vector2.zero, Vector2.zero, 2f);
                m.gameObject.SetActive(false);
                enemyMarkers.Add(m);
            }
            bossMarker = UIKit.Icon(content, "Boss", "UI_marker_boss_draft", Vector2.zero, Vector2.zero, 2f);
            bossMarker.gameObject.SetActive(false);
            playerMarker = UIKit.Icon(view, "Player", "UI_marker_player_draft", new Vector2(0.5f, 0.5f), Vector2.zero, 2f);
            questMarker = UIKit.Icon(view, "Quest", "UI_marker_quest_draft", new Vector2(0.5f, 0.5f), Vector2.zero, 2f);
            questArrow = UIKit.Icon(view, "QuestArrow", "UI_marker_arrow_draft", new Vector2(0.5f, 0.5f), Vector2.zero, 2f);
            questMarker.gameObject.SetActive(false);
            questArrow.gameObject.SetActive(false);
            var orn = UIKit.Icon(frame, "Ornament", "UI_moon_ornament_draft", new Vector2(0.5f, 1), new Vector2(0, 2), 3f);
            orn.rectTransform.SetAsLastSibling();
        }

        void Rebind(MapData d)
        {
            shown = d;
            foreach (var m in poiMarkers)
                Destroy(m.gameObject);
            poiMarkers.Clear();
            if (d == null)
                return;
            map.texture = d.Texture;
            content.sizeDelta = new Vector2(d.Width * CellPx, d.Height * CellPx);
            foreach (var p in d.Pois)
            {
                var m = UIKit.Icon(content, "Poi_" + p.kind, MarkerSprite(p.kind), Vector2.zero, Vector2.zero, 2f);
                poiMarkers.Add(m);
            }
            playerMarker.rectTransform.SetAsLastSibling();
            questArrow.rectTransform.SetAsLastSibling();
            questMarker.rectTransform.SetAsLastSibling();
        }

        public static string MarkerSprite(string kind) => kind switch
        {
            "npc" => "UI_marker_npc_draft",
            "villager" => "UI_marker_villager_draft",
            "door" => "UI_marker_door_draft",
            _ => "UI_marker_star_draft",
        };

        /// <summary>Objective star inside the view, or clamped to the rim with an arrow when it is outside.</summary>
        void UpdateQuest(Vector2 playerWorld)
        {
            bool has = ObjectiveTracker.TryGet(out var target, out _);
            questMarker.gameObject.SetActive(has);
            if (!has)
            {
                questArrow.gameObject.SetActive(false);
                return;
            }
            var offset = (target - playerWorld) * CellPx;
            var half = view.rect.size * 0.5f - new Vector2(16, 16);
            bool outside = Mathf.Abs(offset.x) > half.x || Mathf.Abs(offset.y) > half.y;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            if (outside)
            {
                float k = Mathf.Min(half.x / Mathf.Max(0.001f, Mathf.Abs(offset.x)), half.y / Mathf.Max(0.001f, Mathf.Abs(offset.y)));
                var rim = offset * k;
                questArrow.gameObject.SetActive(true);
                questArrow.rectTransform.anchoredPosition = rim;
                questArrow.rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg - 90f);
                questArrow.color = new Color(1, 1, 1, 0.65f + 0.35f * pulse);
                questMarker.rectTransform.anchoredPosition = rim - offset.normalized * 22f;
            }
            else
            {
                questArrow.gameObject.SetActive(false);
                questMarker.rectTransform.anchoredPosition = new Vector2(Mathf.Round(offset.x), Mathf.Round(offset.y));
            }
            float sc = 1f + 0.18f * pulse;
            questMarker.rectTransform.localScale = new Vector3(sc, sc, 1);
        }

        void LateUpdate()
        {
            var p = PlayerController.Current;
            var d = p != null ? MapData.Current : null;
            if (d != shown)
                Rebind(d);
            view.gameObject.SetActive(d != null);
            if (d == null)
                return;
            Vector2 w = p.transform.position;
            // content's anchored point is its bottom-left corner (pivot 0,0) centred in the view
            content.anchoredPosition = new Vector2(Mathf.Round(-w.x * CellPx), Mathf.Round(-w.y * CellPx));
            for (int i = 0; i < poiMarkers.Count; i++)
                poiMarkers[i].rectTransform.anchoredPosition = d.Pois[i].Position * CellPx;
            var anim = p.GetComponentInChildren<CharacterAnimator>();
            var f = anim != null ? anim.Facing : Vector2.up;
            playerMarker.rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg - 90f);
            UpdateQuest(w);

            int shownEnemies = 0;
            if (EnemySpawner.Exists)
            {
                var list = EnemySpawner.Instance.AliveList;
                for (int i = 0; i < list.Count && shownEnemies < MaxEnemyMarkers; i++)
                {
                    var e = list[i];
                    if (e == null || e == Enemy.ActiveBoss)
                        continue;
                    var m = enemyMarkers[shownEnemies++];
                    m.gameObject.SetActive(true);
                    m.rectTransform.anchoredPosition = (Vector2)e.transform.position * CellPx;
                }
            }
            for (int i = shownEnemies; i < enemyMarkers.Count; i++)
                if (enemyMarkers[i].gameObject.activeSelf)
                    enemyMarkers[i].gameObject.SetActive(false);
            VisibleEnemyMarkers = shownEnemies;
            var boss = Enemy.ActiveBoss;
            bool showBoss = boss != null && boss.Health != null && !boss.Health.IsDead;
            bossMarker.gameObject.SetActive(showBoss);
            if (showBoss)
                bossMarker.rectTransform.anchoredPosition = (Vector2)boss.transform.position * CellPx;
        }
    }
}
