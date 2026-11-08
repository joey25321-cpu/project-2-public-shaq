using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace DeadMiles
{
    public enum Act { Gas, Brake, LeanBack, LeanFwd, Boost, Pause }

    /// <summary>
    /// Reads multi-touch, mouse and keyboard through whichever input backend the project
    /// uses (old Input Manager or the new Input System), so it works either way.
    /// </summary>
    public static class GameInput
    {
        static readonly List<Vector2> pts = new List<Vector2>();
        static float guiScale = 1f;

        /// <summary>All active pointers in GUI coordinates (top-left origin, divided by the UI scale).</summary>
        public static List<Vector2> Pointers(float scale)
        {
            guiScale = scale;
            pts.Clear();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var ts = Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                    if (t.press.isPressed) Add(t.position.ReadValue());
            var m = Mouse.current;
            if (pts.Count == 0 && m != null && m.leftButton.isPressed) Add(m.position.ReadValue());
#else
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled) Add(t.position);
            }
            if (Input.touchCount == 0 && Input.GetMouseButton(0)) Add(Input.mousePosition);
#endif
            return pts;
        }

        static void Add(Vector2 screen) => pts.Add(new Vector2(screen.x, Screen.height - screen.y) / guiScale);

        public static bool Key(Act a)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var k = Keyboard.current;
            if (k == null) return false;
            switch (a)
            {
                case Act.Gas: return k.dKey.isPressed || k.rightArrowKey.isPressed;
                case Act.Brake: return k.aKey.isPressed || k.leftArrowKey.isPressed;
                case Act.LeanBack: return k.wKey.isPressed || k.upArrowKey.isPressed;
                case Act.LeanFwd: return k.sKey.isPressed || k.downArrowKey.isPressed;
                case Act.Boost: return k.spaceKey.isPressed || k.leftShiftKey.isPressed;
                case Act.Pause: return k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame;
            }
            return false;
#else
            switch (a)
            {
                case Act.Gas: return Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
                case Act.Brake: return Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
                case Act.LeanBack: return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
                case Act.LeanFwd: return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
                case Act.Boost: return Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift);
                case Act.Pause: return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
            }
            return false;
#endif
        }
    }
}
