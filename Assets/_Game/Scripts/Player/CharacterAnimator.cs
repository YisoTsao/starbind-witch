using Starbind.Core;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.Player
{
    /// <summary>Picks frames from an 18-frame 4-direction sheet (down 0-5, up 6-11, side 12-17; each:
    /// idle, walk1-4, cast). Left = side frames flipped. Used by the witch and the NPCs. Display only.</summary>
    public sealed class CharacterAnimator : MonoBehaviour
    {
        [SerializeField] SpriteRenderer target;
        [SerializeField] string sheet = "CHR_witch_draft";
        [SerializeField] float walkFps = 8f;

        Sprite[] frames;
        float t;
        float castUntil;

        public Vector2 Facing { get; set; } = Vector2.down;
        public bool Moving { get; set; }
        public int CurrentFrame { get; private set; }

        public void Configure(SpriteRenderer sr, string sheetName)
        {
            target = sr;
            sheet = sheetName;
            frames = null;
        }

        public void PlayCast(float seconds = 0.18f) => castUntil = Time.time + seconds;

        void Update()
        {
            if (frames == null)
            {
                var set = GameDatabase.Instance != null ? GameDatabase.Instance.Sprites(sheet) : null;
                if (set == null || set.frames.Length < 18)
                    return;
                frames = set.frames;
            }
            int row;
            bool flip = false;
            if (Mathf.Abs(Facing.x) > Mathf.Abs(Facing.y) * 1.1f)
            {
                row = 2;
                flip = Facing.x < 0;
            }
            else
                row = Facing.y > 0 ? 1 : 0;
            int f;
            if (Time.time < castUntil)
                f = 5;
            else if (Moving)
            {
                t += Time.deltaTime;
                f = 1 + (int)(t * walkFps) % 4;
            }
            else
            {
                t = 0;
                f = 0;
            }
            CurrentFrame = row * 6 + f;
            target.sprite = frames[CurrentFrame];
            target.flipX = flip;
        }
    }
}
