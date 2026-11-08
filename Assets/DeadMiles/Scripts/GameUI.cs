using System.Collections.Generic;
using UnityEngine;

namespace DeadMiles
{
    /// <summary>
    /// All screens (garage, HUD, pause, results) and the on-screen touch controls.
    /// Drawn with IMGUI on a virtual 1080-pixel-tall canvas that scales to any phone,
    /// and respects the camera cut-out (safe area). Buttons react to our own touch
    /// tracking so they work with either input backend.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public struct DriveInput { public float gas, brake, tilt; public bool boost; }

        const float H = 1080f;
        float scale = 1f, W = 1920f;
        Rect safe;

        // touch tracking
        readonly List<Vector2> pointers = new List<Vector2>();
        readonly List<Vector2> lastPointers = new List<Vector2>();
        readonly List<Vector2> taps = new List<Vector2>();

        // control rects (GUI space)
        Rect rGas, rBrake, rBoost, rBack, rFwd, rPause;
        bool pGas, pBrake, pBoost, pBack, pFwd;

        // styles
        bool stylesReady;
        GUIStyle sPanel, sText, sTitle, sBig, sSmall, sCenter, sButton, sRight, sBarLabel, sBold;
        Texture2D round, white;
        bool confirmReset;

        static readonly Color Accent = new Color(0.96f, 0.55f, 0.12f);
        static readonly Color PanelCol = new Color(0.06f, 0.06f, 0.07f, 0.78f);
        static readonly Color Good = new Color(0.35f, 0.85f, 0.35f);
        static readonly Color Bad = new Color(0.9f, 0.25f, 0.2f);

        GameManager GM => GameManager.I;

        // ------------------------------------------------------------------ per frame input

        void Update()
        {
            scale = Screen.height / H;
            W = Screen.width / scale;
            var sa = Screen.safeArea;
            safe = new Rect(sa.x / scale, (Screen.height - sa.yMax) / scale, sa.width / scale, sa.height / scale);
            Layout();

            lastPointers.Clear();
            lastPointers.AddRange(pointers);
            pointers.Clear();
            pointers.AddRange(GameInput.Pointers(scale));

            taps.Clear();
            foreach (var p in pointers)
            {
                bool wasDown = false;
                foreach (var q in lastPointers) if ((p - q).sqrMagnitude < 60f * 60f) { wasDown = true; break; }
                if (!wasDown) taps.Add(p);
            }

            pGas = Held(rGas); pBrake = Held(rBrake); pBoost = Held(rBoost); pBack = Held(rBack); pFwd = Held(rFwd);
        }

        bool Held(Rect r)
        {
            var big = new Rect(r.x - 25f, r.y - 25f, r.width + 50f, r.height + 50f);
            foreach (var p in pointers) if (big.Contains(p)) return true;
            return false;
        }

        bool Tapped(Rect r)
        {
            foreach (var p in taps) if (r.Contains(p)) return true;
            return false;
        }

        void Layout()
        {
            float l = safe.xMin + 30f, r = safe.xMax - 30f, b = safe.yMax - 30f;
            rBack = new Rect(l, b - 230f, 230f, 230f);
            rFwd = new Rect(l + 250f, b - 230f, 230f, 230f);
            rGas = new Rect(r - 270f, b - 290f, 270f, 290f);
            rBrake = new Rect(r - 270f - 230f, b - 210f, 210f, 210f);
            rBoost = new Rect(r - 270f, b - 290f - 190f, 270f, 170f);
            rPause = new Rect(r - 110f, safe.yMin + 24f, 110f, 90f);
        }

        public DriveInput ReadDriveInput()
        {
            var d = new DriveInput
            {
                gas = (pGas || GameInput.Key(Act.Gas)) ? 1f : 0f,
                brake = (pBrake || GameInput.Key(Act.Brake)) ? 1f : 0f,
                boost = pBoost || GameInput.Key(Act.Boost)
            };
            float tilt = 0f;
            if (pBack || GameInput.Key(Act.LeanBack)) tilt += 1f;
            if (pFwd || GameInput.Key(Act.LeanFwd)) tilt -= 1f;
            d.tilt = tilt;
            return d;
        }

        // ------------------------------------------------------------------ drawing helpers

        void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            white = Texture2D.whiteTexture;
            round = new Texture2D(48, 48, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            const float rad = 16f;
            for (int y = 0; y < 48; y++)
                for (int x = 0; x < 48; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(rad - x - 0.5f, x + 0.5f - (48f - rad)));
                    float dy = Mathf.Max(0f, Mathf.Max(rad - y - 0.5f, y + 0.5f - (48f - rad)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    round.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(rad - d)));
                }
            round.Apply();

            sPanel = new GUIStyle { border = new RectOffset(16, 16, 16, 16) };
            sPanel.normal.background = round;

            sText = new GUIStyle { fontSize = 34, wordWrap = true, richText = true };
            sText.normal.textColor = Color.white;
            sSmall = Sz(sText, 26);
            sSmall.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            sTitle = new GUIStyle(sText) { fontSize = 92, fontStyle = FontStyle.Bold, wordWrap = false };
            sBig = new GUIStyle(sText) { fontSize = 52, fontStyle = FontStyle.Bold, wordWrap = false };
            sCenter = new GUIStyle(sText) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            sRight = new GUIStyle(sText) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold, wordWrap = false };
            sButton = Sz(sCenter, 40);
        }

        readonly Dictionary<long, GUIStyle> sized = new Dictionary<long, GUIStyle>();
        readonly Dictionary<GUIStyle, GUIStyle> shadows = new Dictionary<GUIStyle, GUIStyle>();

        /// <summary>Cached copy of a style at a different font size (avoids per-frame allocations).</summary>
        GUIStyle Sz(GUIStyle st, int size)
        {
            long key = ((long)st.GetHashCode() << 16) ^ size;
            if (!sized.TryGetValue(key, out var s2)) { s2 = new GUIStyle(st) { fontSize = size }; sized[key] = s2; }
            return s2;
        }

        void Panel(Rect r, Color c)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = c;
            GUI.Box(r, GUIContent.none, sPanel);
            GUI.backgroundColor = old;
        }

        void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        void Text(Rect r, string s, GUIStyle st, Color? col = null, bool shadow = true)
        {
            if (shadow)
            {
                if (!shadows.TryGetValue(st, out var sh))
                {
                    sh = new GUIStyle(st);
                    sh.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
                    shadows[st] = sh;
                }
                GUI.Label(new Rect(r.x + 3f, r.y + 3f, r.width, r.height), s, sh);
            }
            var old = st.normal.textColor;
            if (col.HasValue) st.normal.textColor = col.Value;
            GUI.Label(r, s, st);
            st.normal.textColor = old;
        }

        bool Button(Rect r, string label, Color c, bool enabled = true, int fontSize = 40)
        {
            bool held = enabled && Held(r);
            Panel(r, enabled ? (held ? c * 1.25f : c) : new Color(0.3f, 0.3f, 0.3f, 0.8f));
            var st = Sz(sButton, fontSize);
            Text(r, label, st, enabled ? Color.white : new Color(0.7f, 0.7f, 0.7f));
            if (enabled && Event.current.type == EventType.Repaint && Tapped(r))
            {
                Sfx.Play(Sfx.Click(), 0.6f);
                return true;
            }
            return false;
        }

        void Bar(Rect r, float t, Color c, string label)
        {
            Panel(r, new Color(0f, 0f, 0f, 0.55f));
            var inner = new Rect(r.x + 6f, r.y + 6f, (r.width - 12f) * Mathf.Clamp01(t), r.height - 12f);
            if (inner.width > 1f) Panel(inner, c);
            Text(new Rect(r.x + 16f, r.y, r.width, r.height), label, sBarLabel, Color.white);
        }

        // ------------------------------------------------------------------ screens

        void OnGUI()
        {
            if (GM == null) return;
            InitStyles();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            switch (GM.State)
            {
                case GameState.Garage: DrawGarage(); break;
                case GameState.Driving: DrawHud(); DrawControls(); break;
                case GameState.Paused: DrawHud(); DrawPause(); break;
                case GameState.Results: DrawHud(); DrawResults(); break;
            }
        }

        void DrawGarage()
        {
            var s = GM.Save;
            float l = safe.xMin + 40f, top = safe.yMin + 30f;

            Text(new Rect(l, top, 900f, 110f), "DEAD MILES", sTitle, Accent);
            Text(new Rect(l + 4f, top + 105f, 900f, 50f),
                $"Day {s.day}   ·   Best {s.bestDistance:0} m / {WorldGen.Goal:0} m   ·   {s.totalKills} walkers", sSmall);
            if (s.escaped) Text(new Rect(l + 4f, top + 150f, 900f, 50f), "EVAC REACHED - keep upgrading and go again!", sSmall, Good);

            // upgrades panel
            float pw = Mathf.Min(820f, W * 0.48f);
            var panel = new Rect(safe.xMax - pw - 30f, top, pw, safe.yMax - top - 30f);
            Panel(panel, PanelCol);
            Text(new Rect(panel.x + 30f, panel.y + 18f, 400f, 60f), "GARAGE", sBig);
            Text(new Rect(panel.x, panel.y + 22f, panel.width - 30f, 60f), $"$ {s.money:N0}", Sz(sRight, 48), Good);

            float rowH = (panel.height - 110f) / Upgrades.Count;
            for (int i = 0; i < Upgrades.Count; i++)
            {
                var t = (UpgradeType)i;
                int lvl = s.Level(t);
                int cost = Upgrades.Cost(t, lvl);
                var row = new Rect(panel.x + 20f, panel.y + 95f + i * rowH, panel.width - 40f, rowH - 10f);
                Panel(row, new Color(1f, 1f, 1f, 0.06f));
                Text(new Rect(row.x + 20f, row.y + 8f, row.width - 240f, 44f), Upgrades.Names[i], sBold);
                Text(new Rect(row.x + 20f, row.y + 50f, row.width - 250f, 40f), Upgrades.Info[i], sSmall);
                for (int p = 0; p < Upgrades.MaxLevel; p++)
                    Panel(new Rect(row.x + 20f + p * 46f, row.yMax - 26f, 38f, 14f), p < lvl ? Accent : new Color(1f, 1f, 1f, 0.15f));

                var br = new Rect(row.xMax - 210f, row.y + (row.height - 80f) * 0.5f, 195f, 80f);
                if (cost < 0) Button(br, "MAX", new Color(0.25f, 0.25f, 0.25f), false, 36);
                else if (Button(br, "$" + cost.ToString("N0"), new Color(0.15f, 0.45f, 0.2f), s.money >= cost, 36)) GM.TryBuy(t);
            }

            // drive + settings
            float b = safe.yMax - 40f;
            if (Button(new Rect(l, b - 170f, 420f, 170f), "DRIVE", Accent, true, 64)) GM.StartRun();
            if (Button(new Rect(l, b - 270f, 205f, 80f), s.batterySaver ? "60 FPS" : "120 FPS", new Color(0.2f, 0.2f, 0.25f), true, 30))
            {
                s.batterySaver = !s.batterySaver;
                s.Save();
                GM.ApplyFrameRate();
            }
            if (!confirmReset)
            {
                if (Button(new Rect(l + 215f, b - 270f, 205f, 80f), "Reset", new Color(0.3f, 0.12f, 0.1f), true, 30)) confirmReset = true;
            }
            else if (Button(new Rect(l + 215f, b - 270f, 205f, 80f), "Sure?", Bad, true, 30))
            {
                confirmReset = false;
                GM.ResetProgress();
            }
            Text(new Rect(l, b - 330f, 600f, 50f), "Tip: lean back in the air to land on your wheels", sSmall);
        }

        void DrawHud()
        {
            var car = GM.Car;
            var run = GM.Run;
            if (car == null || run == null) return;
            float l = safe.xMin + 30f, top = safe.yMin + 24f;

            // progress to evac
            float pw = Mathf.Min(900f, W - 1000f);
            if (pw < 400f) pw = 400f;
            var prog = new Rect((W - pw) * 0.5f, top, pw, 34f);
            Panel(prog, new Color(0f, 0f, 0f, 0.55f));
            float t = Mathf.Clamp01(run.distance / WorldGen.Goal);
            Panel(new Rect(prog.x + 5f, prog.y + 5f, (prog.width - 10f) * t, prog.height - 10f), Accent);
            float best = Mathf.Clamp01(GM.Save.bestDistance / WorldGen.Goal);
            if (best > 0f) Fill(new Rect(prog.x + 5f + (prog.width - 10f) * best - 2f, prog.y - 6f, 4f, prog.height + 12f), Color.white);
            Text(new Rect(prog.x, prog.yMax + 2f, prog.width, 50f), $"{run.distance:0} m  /  {WorldGen.Goal:0} m", sCenter);

            // gauges
            var st = car.stats;
            Bar(new Rect(l, top, 380f, 50f), car.fuel / st.fuelCapacity, car.fuel / st.fuelCapacity < 0.25f ? Bad : new Color(0.9f, 0.75f, 0.2f), "FUEL");
            Bar(new Rect(l, top + 60f, 380f, 50f), car.health / st.maxHealth, car.health / st.maxHealth < 0.3f ? Bad : Good, "HULL");
            if (st.boostCapacity > 0f)
                Bar(new Rect(l, top + 120f, 380f, 50f), car.boost / st.boostCapacity, new Color(0.3f, 0.6f, 1f), "BOOST");

            // stats
            float r = safe.xMax - 150f;
            float kmh = Mathf.Abs(car.ForwardSpeed) * 3.6f;
            Text(new Rect(r - 420f, top, 420f, 50f), $"{kmh:0} km/h", sRight);
            Text(new Rect(r - 420f, top + 50f, 420f, 50f), $"Walkers  {run.kills}", sRight);
            int cash = Mathf.RoundToInt(run.distance) + run.kills * GameManager.KillReward;
            Text(new Rect(r - 420f, top + 100f, 420f, 50f), $"+$ {cash:N0}", sRight, Good);

            if (GM.State == GameState.Driving && Button(rPause, "II", new Color(0f, 0f, 0f, 0.5f), true, 40)) GM.TogglePause();

            // popups
            var cam = GM.Rig.Cam;
            foreach (var p in GM.popups)
            {
                var sp = cam.WorldToScreenPoint(p.pos + Vector3.up * (Time.time - p.t) * 1.5f);
                if (sp.z < 0f) continue;
                var gp = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
                float a = 1f - (Time.time - p.t) / 1.2f;
                var c = p.color; c.a = a;
                Text(new Rect(gp.x - 150f, gp.y - 30f, 300f, 60f), p.text, Sz(sCenter, 40), c);
            }
        }

        void DrawControls()
        {
            var car = GM.Car;
            Ctrl(rBack, "LEAN\nBACK", pBack);
            Ctrl(rFwd, "LEAN\nFWD", pFwd);
            Ctrl(rBrake, "BRAKE", pBrake);
            Ctrl(rGas, "GAS", pGas, Accent);
            if (car != null && car.stats.boostCapacity > 0f) Ctrl(rBoost, "BOOST", pBoost, new Color(0.3f, 0.6f, 1f));
        }

        void Ctrl(Rect r, string label, bool pressed, Color? tint = null)
        {
            var c = tint ?? Color.white;
            Panel(r, pressed ? new Color(c.r, c.g, c.b, 0.65f) : new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f, 0.35f));
            Text(r, label, Sz(sCenter, 38), pressed ? Color.white : new Color(1f, 1f, 1f, 0.85f));
        }

        void DrawPause()
        {
            Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.45f));
            var p = new Rect((W - 600f) * 0.5f, 300f, 600f, 480f);
            Panel(p, PanelCol);
            Text(new Rect(p.x, p.y + 30f, p.width, 80f), "PAUSED", Sz(sCenter, 64));
            if (Button(new Rect(p.x + 60f, p.y + 150f, p.width - 120f, 120f), "RESUME", Accent, true, 48)) GM.TogglePause();
            if (Button(new Rect(p.x + 60f, p.y + 300f, p.width - 120f, 120f), "GIVE UP", new Color(0.35f, 0.15f, 0.12f), true, 44)) GM.GiveUp();
        }

        void DrawResults()
        {
            var run = GM.Run;
            Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.35f));
            var p = new Rect((W - 900f) * 0.5f, 150f, 900f, 780f);
            Panel(p, PanelCol);
            Text(new Rect(p.x, p.y + 30f, p.width, 80f), run.endReason, Sz(sCenter, 54), run.escaped ? Good : Accent);
            if (GM.NewBest) Text(new Rect(p.x, p.y + 105f, p.width, 50f), "NEW BEST DISTANCE!", Sz(sCenter, 34), Good);

            float y = p.y + 180f;
            Line(p, ref y, $"Distance   {run.distance:0} m", $"$ {run.distanceMoney:N0}");
            Line(p, ref y, $"Walkers   {run.kills} × ${GameManager.KillReward}", $"$ {run.killMoney:N0}");
            if (run.bonusMoney > 0) Line(p, ref y, "Evac bonus", $"$ {run.bonusMoney:N0}");
            Fill(new Rect(p.x + 60f, y + 10f, p.width - 120f, 3f), new Color(1f, 1f, 1f, 0.3f));
            y += 30f;
            Line(p, ref y, "TOTAL EARNED", $"$ {run.Total:N0}", Good);
            Text(new Rect(p.x + 60f, y, p.width - 120f, 50f), $"Bank: $ {GM.Save.money:N0}", sSmall);

            if (Button(new Rect(p.x + 200f, p.yMax - 170f, p.width - 400f, 130f), "CONTINUE", Accent, true, 52))
                GM.EnterGarage();
        }

        void Line(Rect p, ref float y, string left, string right, Color? c = null)
        {
            Text(new Rect(p.x + 60f, y, p.width - 120f, 60f), left, Sz(sText, 40), c);
            Text(new Rect(p.x + 60f, y, p.width - 120f, 60f), right, Sz(sRight, 40), c);
            y += 70f;
        }
    }
}
