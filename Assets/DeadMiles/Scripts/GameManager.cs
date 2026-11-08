using System.Collections.Generic;
using UnityEngine;

namespace DeadMiles
{
    public enum GameState { Garage, Driving, Paused, Results }

    /// <summary>
    /// Creates the whole game at runtime (no scene setup needed) and runs the loop:
    /// Garage (buy upgrades) → Drive (as far as you can) → Results (get paid) → Garage.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public SaveData Save { get; private set; }
        public GameState State { get; private set; }
        public CarController Car { get; private set; }
        public RunStats Run { get; private set; }
        public WorldGen World { get; private set; }
        public CameraRig Rig { get; private set; }
        public bool NewBest { get; private set; }

        public const int KillReward = 12;
        public const int EvacBonus = 5000;

        public struct Popup { public Vector3 pos; public string text; public float t; public Color color; }
        public readonly List<Popup> popups = new List<Popup>();

        GameUI ui;
        float flipTimer, fuelTimer, stuckTimer, bestX;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<GameManager>() != null) return;
            new GameObject("DeadMiles").AddComponent<GameManager>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;

            Save = SaveData.Load();
            ApplyFrameRate();
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            Time.fixedDeltaTime = 1f / 100f;
            Time.maximumDeltaTime = 1f / 15f;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            Physics.defaultSolverIterations = 10;
            Physics.defaultSolverVelocityIterations = 2;

            // Turn off anything a default scene might contain; we build our own.
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.gameObject.SetActive(false);

            Rig = CameraRig.Create();
            Atmosphere.Setup(transform, Rig.Cam);
            Fx.Init(transform);
            Sfx.Init(transform);
            World = new GameObject("World").AddComponent<WorldGen>();
            ui = gameObject.AddComponent<GameUI>();

            EnterGarage();
        }

        public void ApplyFrameRate() => Application.targetFrameRate = Save.batterySaver ? 60 : 120;

        // ------------------------------------------------------------------ flow

        public void EnterGarage()
        {
            Time.timeScale = 1f;
            State = GameState.Garage;
            popups.Clear();
            World.ResetWorld();
            SpawnCar(true);
        }

        void SpawnCar(bool snapCamera)
        {
            if (Car != null) Destroy(Car.gameObject);
            const float x = 8f;
            Car = CarController.Create(new Vector3(x, WorldGen.Height(x) + 0.08f, 0f), CarStats.From(Save));
            Rig.Garage(Car, snapCamera);
        }

        public bool TryBuy(UpgradeType t)
        {
            int lvl = Save.Level(t);
            int cost = Upgrades.Cost(t, lvl);
            if (cost < 0 || Save.money < cost) return false;
            Save.money -= cost;
            Save.levels[(int)t] = lvl + 1;
            Save.Save();
            SpawnCar(false);
            Sfx.Play(Sfx.Cash(), 0.8f);
            return true;
        }

        public void StartRun()
        {
            if (State != GameState.Garage) return;
            State = GameState.Driving;
            Run = new RunStats { startX = Car.transform.position.x };
            bestX = Car.transform.position.x;
            flipTimer = fuelTimer = stuckTimer = 0f;
            Car.Release();
            Rig.Follow(Car);
        }

        public void TogglePause()
        {
            if (State == GameState.Driving) { State = GameState.Paused; Time.timeScale = 0f; AudioListener.pause = true; }
            else if (State == GameState.Paused) { State = GameState.Driving; Time.timeScale = 1f; AudioListener.pause = false; }
        }

        public void GiveUp()
        {
            if (State == GameState.Paused) TogglePause();
            EndRun("You turned back", false);
        }

        void EndRun(string reason, bool escaped)
        {
            if (State != GameState.Driving) return;
            State = GameState.Results;
            Run.endReason = reason;
            Run.escaped = escaped;
            Run.distanceMoney = Mathf.RoundToInt(Run.distance);
            Run.killMoney = Run.kills * KillReward;
            Run.bonusMoney = escaped ? EvacBonus : 0;

            NewBest = Run.distance > Save.bestDistance + 0.5f;
            Save.money += Run.Total;
            Save.bestDistance = Mathf.Max(Save.bestDistance, Run.distance);
            Save.totalKills += Run.kills;
            Save.day++;
            if (escaped) Save.escaped = true;
            Save.Save();

            Car.inThrottle = Car.inBrake = Car.inTilt = 0f;
            Car.inBoost = false;
            Sfx.Play(Sfx.Cash(), 0.9f);
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            if (GameInput.Key(Act.Pause))
            {
                if (State == GameState.Driving || State == GameState.Paused) TogglePause();
            }

            for (int i = popups.Count - 1; i >= 0; i--)
                if (Time.time - popups[i].t > 1.2f) popups.RemoveAt(i);

            if (Car == null) return;
            float x = Car.transform.position.x;
            if (State != GameState.Garage) World.Stream(x);

            if (State != GameState.Driving) return;

            var input = ui.ReadDriveInput();
            Car.inThrottle = input.gas;
            Car.inBrake = input.brake;
            Car.inTilt = input.tilt;
            Car.inBoost = input.boost;

            float dt = Time.deltaTime;
            Run.time += dt;
            Run.distance = Mathf.Max(Run.distance, x - Run.startX);
            float speed = Car.Body.GetVel().magnitude;

            if (x >= WorldGen.Goal) { EndRun("You reached the evac zone!", true); return; }
            if (Car.health <= 0f) { Car.health = 0f; EndRun("Your truck was wrecked", false); return; }
            if (Car.transform.position.y < WorldGen.Height(x) - 30f) { EndRun("You fell off the world", false); return; }

            flipTimer = Car.UpsideDown && speed < 2.5f ? flipTimer + dt : 0f;
            if (flipTimer > 2.5f) { EndRun("You flipped over", false); return; }

            fuelTimer = Car.fuel <= 0f && speed < 0.8f ? fuelTimer + dt : 0f;
            if (fuelTimer > 1.5f) { EndRun("Out of fuel", false); return; }

            if (x > bestX + 1f) { bestX = x; stuckTimer = 0f; }
            else stuckTimer += dt;
            if (stuckTimer > (Run.distance < 5f ? 25f : 10f)) { EndRun("You got stuck", false); return; }
        }

        // ------------------------------------------------------------------ events

        public void OnZombieKilled(Vector3 pos)
        {
            if (State != GameState.Driving || Run == null) return;
            Run.kills++;
            popups.Add(new Popup { pos = pos, text = "+$" + KillReward, t = Time.time, color = new Color(1f, 0.85f, 0.3f) });
            Shake(0.25f);
        }

        public void OnPickup(Vector3 pos)
        {
            if (Run != null) Run.pickups++;
            popups.Add(new Popup { pos = pos, text = "+FUEL", t = Time.time, color = new Color(1f, 0.5f, 0.3f) });
        }

        public void Shake(float amount) { if (Rig != null) Rig.Shake(amount); }

        public void ResetProgress()
        {
            SaveData.Wipe();
            Save = SaveData.Load();
            ApplyFrameRate();
            EnterGarage();
        }
    }
}
