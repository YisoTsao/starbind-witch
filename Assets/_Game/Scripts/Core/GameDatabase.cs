using System;
using System.Collections.Generic;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.Spells;
using UnityEngine;

namespace Starbind.Core
{
    /// <summary>Named sprite animation (one sliced strip).</summary>
    [Serializable]
    public sealed class SpriteSet
    {
        public string name;
        public float fps = 10f;
        /// <summary>Walk frames used per direction in a v2 character sheet (CharacterAnimator).</summary>
        public int walkFrames = 4;
        public Sprite[] frames;
    }

    /// <summary>All static content the runtime needs, built by the editor content builder and loaded
    /// from Resources. Lookups are by string id; nothing here is written at runtime.</summary>
    [CreateAssetMenu(menuName = "Starbind/Game Database")]
    public sealed class GameDatabase : ScriptableObject
    {
        public const string ResourcePath = "GameDatabase";

        public List<SpriteSet> sprites = new List<SpriteSet>();
        public List<SpellDefinition> spells = new List<SpellDefinition>();
        public List<EnemyDefinition> enemies = new List<EnemyDefinition>();
        public List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();
        public List<ChapterDefinition> chapters = new List<ChapterDefinition>();
        public List<QuestDefinition> quests = new List<QuestDefinition>();
        public Material unlitMaterial;
        public Material characterMaterial;
        public List<AudioClip> sounds = new List<AudioClip>();

        Dictionary<string, SpriteSet> spriteIndex;
        Dictionary<string, SpellDefinition> spellIndex;
        Dictionary<string, EnemyDefinition> enemyIndex;
        Dictionary<string, AudioClip> soundIndex;

        static GameDatabase instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static GameDatabase Instance
        {
            get
            {
                if (instance == null)
                    instance = Resources.Load<GameDatabase>(ResourcePath);
                return instance;
            }
        }

        void Index()
        {
            if (spriteIndex != null)
                return;
            spriteIndex = new Dictionary<string, SpriteSet>();
            foreach (var s in sprites)
                spriteIndex[s.name] = s;
            spellIndex = new Dictionary<string, SpellDefinition>();
            foreach (var s in spells)
                spellIndex[s.id] = s;
            enemyIndex = new Dictionary<string, EnemyDefinition>();
            foreach (var e in enemies)
                enemyIndex[e.id] = e;
            soundIndex = new Dictionary<string, AudioClip>();
            foreach (var a in sounds)
                if (a != null)
                    soundIndex[a.name] = a;
        }

        public SpriteSet Sprites(string name)
        {
            Index();
            if (name != null && spriteIndex.TryGetValue(name, out var s))
                return s;
            if (name != null && spriteIndex.TryGetValue(name + "_draft", out s))
                return s;
            return null;
        }

        public Sprite Sprite(string name, int frame = 0)
        {
            var s = Sprites(name);
            return s != null && s.frames.Length > 0 ? s.frames[Mathf.Clamp(frame, 0, s.frames.Length - 1)] : null;
        }

        public SpellDefinition Spell(string id)
        {
            Index();
            return id != null && spellIndex.TryGetValue(id, out var s) ? s : null;
        }

        public EnemyDefinition Enemy(string id)
        {
            Index();
            return id != null && enemyIndex.TryGetValue(id, out var e) ? e : null;
        }

        public ChapterDefinition Chapter(string id)
        {
            foreach (var c in chapters)
                if (c != null && c.id == id)
                    return c;
            return null;
        }

        /// <summary>The chapter whose run happens in <paramref name="scene"/>, or null (village, workshop).</summary>
        public ChapterDefinition ChapterForScene(string scene)
        {
            foreach (var c in chapters)
                if (c != null && c.scene == scene)
                    return c;
            return null;
        }

        public AudioClip Sound(string name)
        {
            Index();
            return name != null && soundIndex.TryGetValue(name, out var a) ? a : null;
        }
    }
}
