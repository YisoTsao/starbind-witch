using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Starbind.Controls
{
    /// <summary>Reads the Gameplay action map (GDD §4.1). Owns no gameplay logic: exposes values and
    /// button events only.</summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;

        InputActionMap gameplay;
        InputAction move, interact, aim, aimStick, attack, chargeAlt, dash, spell1, spell2, overdrive, grimoire, pause, item1, item2, item3, map;

        public event Action InteractPressed;
        public event Action AttackPressed;
        public event Action AttackReleased;
        public event Action ChargeAltPressed;
        public event Action ChargeAltReleased;
        public event Action DashPressed;
        public event Action Spell1Pressed;
        public event Action Spell2Pressed;
        public event Action OverdrivePressed;
        public event Action GrimoirePressed;
        public event Action PausePressed;
        public event Action MapPressed;
        public event Action<int> ItemPressed;

        public Vector2 Move => move != null ? move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 AimScreen => aim != null ? aim.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 AimStick => aimStick != null ? aimStick.ReadValue<Vector2>() : Vector2.zero;
        public bool AttackHeld => attack != null && attack.IsPressed();
        public bool ChargeAltHeld => chargeAlt != null && chargeAlt.IsPressed();
        public bool PointerActive { get; private set; }

        void Awake()
        {
            gameplay = actions.FindActionMap("Gameplay", true);
            move = gameplay.FindAction("Move", true);
            interact = gameplay.FindAction("Interact", true);
            aim = gameplay.FindAction("Aim");
            aimStick = gameplay.FindAction("AimStick");
            attack = gameplay.FindAction("Attack");
            chargeAlt = gameplay.FindAction("ChargeAlt");
            dash = gameplay.FindAction("Dash");
            spell1 = gameplay.FindAction("Spell1");
            spell2 = gameplay.FindAction("Spell2");
            overdrive = gameplay.FindAction("Overdrive");
            grimoire = gameplay.FindAction("Grimoire");
            pause = gameplay.FindAction("Pause");
            map = gameplay.FindAction("Map");
            item1 = gameplay.FindAction("Item1");
            item2 = gameplay.FindAction("Item2");
            item3 = gameplay.FindAction("Item3");
        }

        void OnEnable()
        {
            interact.performed += OnInteract;
            Hook(attack, _ => AttackPressed?.Invoke(), _ => AttackReleased?.Invoke());
            Hook(chargeAlt, _ => ChargeAltPressed?.Invoke(), _ => ChargeAltReleased?.Invoke());
            Hook(dash, _ => DashPressed?.Invoke());
            Hook(spell1, _ => Spell1Pressed?.Invoke());
            Hook(spell2, _ => Spell2Pressed?.Invoke());
            Hook(overdrive, _ => OverdrivePressed?.Invoke());
            Hook(grimoire, _ => GrimoirePressed?.Invoke());
            Hook(pause, _ => PausePressed?.Invoke());
            if (map != null)
                Hook(map, _ => MapPressed?.Invoke());
            Hook(item1, _ => ItemPressed?.Invoke(1));
            Hook(item2, _ => ItemPressed?.Invoke(2));
            Hook(item3, _ => ItemPressed?.Invoke(3));
            if (aim != null)
                aim.performed += OnAim;
            gameplay.Enable();
        }

        void OnDisable()
        {
            interact.performed -= OnInteract;
            gameplay.Disable();
            // Rebuild delegates on next enable: clear all handlers we added.
            foreach (var a in new[] { attack, chargeAlt, dash, spell1, spell2, overdrive, grimoire, pause, item1, item2, item3, map })
                Unhook(a);
            if (aim != null)
                aim.performed -= OnAim;
        }

        readonly System.Collections.Generic.Dictionary<InputAction, (Action<InputAction.CallbackContext>, Action<InputAction.CallbackContext>)> hooks =
            new System.Collections.Generic.Dictionary<InputAction, (Action<InputAction.CallbackContext>, Action<InputAction.CallbackContext>)>();

        void Hook(InputAction a, Action<InputAction.CallbackContext> started, Action<InputAction.CallbackContext> canceled = null)
        {
            if (a == null)
                return;
            a.started += started;
            if (canceled != null)
                a.canceled += canceled;
            hooks[a] = (started, canceled);
        }

        void Unhook(InputAction a)
        {
            if (a == null || !hooks.TryGetValue(a, out var h))
                return;
            a.started -= h.Item1;
            if (h.Item2 != null)
                a.canceled -= h.Item2;
            hooks.Remove(a);
        }

        void OnAim(InputAction.CallbackContext _) => PointerActive = true;

        void OnInteract(InputAction.CallbackContext _) => InteractPressed?.Invoke();
    }
}
