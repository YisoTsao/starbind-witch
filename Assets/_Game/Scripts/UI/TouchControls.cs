using System;
using System.Collections.Generic;
using Starbind.Controls;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace Starbind.UI
{
    /// <summary>Phone / tablet controls (mobile browser build). Uses the Input System on-screen controls,
    /// which drive a virtual gamepad, so the same StarbindControls actions and PlayerController handle
    /// touch, keyboard and pad alike:
    ///   left thumb   virtual stick                      -> &lt;Gamepad&gt;/leftStick (Move)
    ///   right thumb  HUD skill slots as buttons         -> staff, Q, E, R, dash, potions (see Hud.TouchSlots)
    ///                互動 button                          -> &lt;Gamepad&gt;/buttonSouth (Interact)
    ///   top left     暫停 / 魔典 / 地圖 / 任務 buttons     -> GameUI panels
    /// Attacks auto-aim at the nearest enemy while touch mode is on (PlayerInputReader.TouchMode).
    /// Shown on mobile platforms, after the first touch on any device, or with -touch / ForceOn.</summary>
    public sealed class TouchControls : MonoBehaviour
    {
        public const float StickRange = 110f;

        /// <summary>Tests and the -touch command line flag force the layout on desktop.</summary>
        public static bool ForceOn { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ForceOn = false;

        GameUI ui;
        Hud hud;
        RectTransform root, stickBase, portraitHint;
        readonly List<OnScreenControl> controls = new List<OnScreenControl>();

        public bool Active { get; private set; }
        public OnScreenStick Stick { get; private set; }
        public OnScreenButton InteractButton { get; private set; }
        public IReadOnlyList<OnScreenControl> Controls => controls;

        public void Build(GameUI owner, Hud h, RectTransform parent)
        {
            ui = owner;
            hud = h;
            root = UIKit.Stretch(parent, "Touch");
            root.gameObject.SetActive(false);

            // ---- movement stick (static origin, bottom left)
            stickBase = UIKit.Image(root, "StickBase", new Color(0.10f, 0.07f, 0.16f, 0.55f), new Vector2(0, 0), new Vector2(0.5f, 0.5f),
                new Vector2(250, 240), new Vector2(280, 280), Circle(64, 4)).rectTransform;
            stickBase.GetComponent<Image>().color = new Color(0.95f, 0.78f, 0.36f, 0.55f);
            var knobImg = UIKit.Image(stickBase, "Knob", new Color(0.86f, 0.8f, 1f, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(124, 124), Circle(32, 0));
            knobImg.raycastTarget = true;
            Stick = AddControl<OnScreenStick>(knobImg.gameObject, "<Gamepad>/leftStick");
            Stick.movementRange = StickRange;

            // ---- skill slots of the HUD become buttons
            foreach (var (frame, path) in hud.TouchSlots())
                AddControl<OnScreenButton>(frame.gameObject, path);

            // ---- interact
            var interact = UIKit.Image(root, "Interact", UIKit.PanelLight, new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-370, 486), new Vector2(108, 108), Circle(32, 3));
            interact.color = new Color(0.95f, 0.78f, 0.36f, 0.9f);
            interact.raycastTarget = true;
            UIKit.Label(interact.transform, "Label", "互動", 36, UIKit.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(108, 50));
            InteractButton = AddControl<OnScreenButton>(interact.gameObject, "<Gamepad>/buttonSouth");

            // ---- menu buttons (top left, below the notices)
            var menu = new (string label, Action act)[]
            {
                ("暫停", () => ui.TogglePause()), ("魔典", () => ui.ToggleGrimoire()), ("地圖", () => ui.ToggleMap()), ("任務", () => ui.ToggleQuests()),
            };
            for (int i = 0; i < menu.Length; i++)
            {
                var b = UIKit.Button(root, "Menu_" + menu[i].label, menu[i].label, new Vector2(0, 1), new Vector2(92 + i * 136, -410), new Vector2(120, 72), menu[i].act, 24);
                b.GetComponent<RectTransform>().anchorMin = b.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
            }

            // ---- portrait warning (the HUD and camera are laid out for landscape)
            portraitHint = UIKit.Stretch(root, "PortraitHint");
            var dim = portraitHint.gameObject.AddComponent<Image>();
            dim.sprite = UIKit.White;
            dim.color = new Color(0.03f, 0.02f, 0.06f, 0.92f);
            UIKit.Label(portraitHint, "Text", "請將裝置轉為橫向", 48, UIKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 80));
            portraitHint.gameObject.SetActive(false);

            if (ForceOn || Application.isMobilePlatform || HasArg("-touch"))
                SetActive(true);
        }

        T AddControl<T>(GameObject go, string path) where T : OnScreenControl
        {
            // set the control path while disabled: OnEnable binds the virtual device to that path
            bool was = go.activeSelf;
            go.SetActive(false);
            var c = go.AddComponent<T>();
            c.controlPath = path;
            go.SetActive(was);
            c.enabled = Active;
            controls.Add(c);
            return c;
        }

        public void SetActive(bool on)
        {
            Active = on;
            PlayerInputReader.TouchMode = on;
            root.gameObject.SetActive(on);
            hud.SetTouchLayout(on);
            foreach (var c in controls)
                if (c != null)
                    c.enabled = on;
        }

        void Update()
        {
            if (!Active)
            {
                var ts = Touchscreen.current;
                if (ForceOn || (ts != null && ts.primaryTouch.press.wasPressedThisFrame))
                    SetActive(true);
                return;
            }
            bool portrait = Screen.height > Screen.width;
            if (portraitHint.gameObject.activeSelf != portrait)
                portraitHint.gameObject.SetActive(portrait);
        }

        void OnDestroy()
        {
            if (Active)
                PlayerInputReader.TouchMode = false;
        }

        static bool HasArg(string a) => Array.IndexOf(Environment.GetCommandLineArgs(), a) >= 0;

        static readonly Dictionary<(int, int), Sprite> circles = new Dictionary<(int, int), Sprite>();

        /// <summary>Pixel-art circle (filled when ring == 0, else a ring that many pixels thick), point filtered.</summary>
        static Sprite Circle(int size, int ring)
        {
            if (circles.TryGetValue((size, ring), out var s) && s != null)
                return s;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    bool on = d <= r - 0.5f && (ring == 0 || d >= r - 0.5f - ring);
                    bool inner = ring > 0 && d < r - 0.5f - ring;
                    px[y * size + x] = on ? new Color32(255, 255, 255, 255) : inner ? new Color32(255, 255, 255, 60) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32);
            circles[(size, ring)] = s;
            return s;
        }
    }
}
