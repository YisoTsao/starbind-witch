using Starbind.Core;
using UnityEngine;

namespace Starbind.Player
{
    /// <summary>Picks frames from a 3-direction sheet; left = side frames flipped. Display only.
    /// 8-way facing: diagonals (up-left, down-left, ...) use the side frames so any leftward heading
    /// reads as facing left; only near-vertical headings (within ~22 deg) use the up / down frames.
    /// Legacy layout (18 frames): down 0-5, up 6-11, side 12-17; each idle, walk1-4, cast.
    /// v2 layout (36 frames, sheets cut from the chibi references): down 0-11, up 12-23, side 24-35;
    /// each 0-1 idle, 2-6 walk (SpriteSet.walkFrames used), 7-11 action.
    /// Used by the witch and the NPCs.</summary>
    public sealed class CharacterAnimator : MonoBehaviour
    {
        public const int V2Frames = 36;
        /// <summary>|x| / |y| above which the side frames are used (tan 22 deg); 45 deg diagonals are sideways.</summary>
        public const float SideRatio = 0.4f;

        [SerializeField] string sheet = "CHR_witch_ref";
        [SerializeField] SpriteRenderer target;
        [SerializeField] float walkFps = 8f;
        [SerializeField] float idleFps = 2f;
        [SerializeField] float actionFps = 14f;

        Sprite[] frames;
        int walkFrames = 4;
        float t;
        float castUntil;
        float actionStart = -1f;

        public Vector2 Facing { get; set; } = Vector2.down;
        public bool Moving { get; set; }
        /// <summary>Hold the charged pose (v2: action frame 3, the glowing staff) while true.</summary>
        public bool Charging { get; set; }
        public int CurrentFrame { get; private set; }
        /// <summary>0 down, 1 up, 2 side (flipped when facing left).</summary>
        public int CurrentDirection { get; private set; }
        public bool FacingLeft { get; private set; }
        public bool IsV2 => frames != null && frames.Length >= V2Frames;

        public void Configure(SpriteRenderer sr, string sheetName)
        {
            target = sr;
            sheet = sheetName;
            frames = null;
        }

        /// <summary>Shows the action (cast / swing / wave) for at least <paramref name="seconds"/>. Repeated
        /// calls while it is showing extend it without restarting the sequence.</summary>
        public void PlayCast(float seconds = 0.18f, bool restart = false)
        {
            if (restart || Time.time >= castUntil)
                actionStart = Time.time;
            castUntil = Mathf.Max(castUntil, Time.time + seconds);
        }

        void Update()
        {
            if (frames == null)
            {
                var set = GameDatabase.Instance != null ? GameDatabase.Instance.Sprites(sheet) : null;
                if (set == null || set.frames.Length < 18)
                    return;
                frames = set.frames;
                walkFrames = Mathf.Clamp(set.walkFrames, 1, 5);
            }
            int dir;
            bool flip = false;
            if (Mathf.Abs(Facing.x) > Mathf.Abs(Facing.y) * SideRatio)
            {
                dir = 2;
                flip = Facing.x < 0;
            }
            else
                dir = Facing.y > 0 ? 1 : 0;
            CurrentDirection = dir;
            FacingLeft = flip;
            bool acting = Time.time < castUntil || Charging;
            if (!Moving)
                t = 0;
            CurrentFrame = IsV2 ? FrameV2(dir, acting) : FrameLegacy(dir, acting);
            target.sprite = frames[CurrentFrame];
            target.flipX = flip;
        }

        int FrameLegacy(int dir, bool acting)
        {
            int f;
            if (acting)
                f = 5;
            else if (Moving)
            {
                t += Time.deltaTime;
                f = 1 + (int)(t * walkFps) % 4;
            }
            else
                f = 0;
            return dir * 6 + f;
        }

        int FrameV2(int dir, bool acting)
        {
            int f;
            if (Charging)
                f = 7 + 3;
            else if (acting)
                f = 7 + 1 + Mathf.Min(3, (int)((Time.time - actionStart) * actionFps));
            else if (Moving)
            {
                t += Time.deltaTime;
                f = 2 + (int)(t * walkFps) % walkFrames;
            }
            else
                f = (int)(Time.time * idleFps) % 2;
            return dir * 12 + f;
        }
    }
}
