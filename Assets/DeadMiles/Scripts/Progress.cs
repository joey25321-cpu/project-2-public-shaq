using System;
using UnityEngine;

namespace DeadMiles
{
    public enum UpgradeType { Engine, Tires, Suspension, FuelTank, Armor, Booster }

    [Serializable]
    public class SaveData
    {
        public int money;
        public int day = 1;
        public float bestDistance;
        public int totalKills;
        public bool escaped;
        public bool batterySaver;
        public int[] levels = new int[6];

        const string Key = "deadmiles_save_v1";

        public int Level(UpgradeType t) => levels[(int)t];

        public static SaveData Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var s = JsonUtility.FromJson<SaveData>(json);
                    if (s != null)
                    {
                        if (s.levels == null || s.levels.Length != 6) Array.Resize(ref s.levels, 6);
                        return s;
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("Save load failed: " + e.Message); }
            return new SaveData();
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public static void Wipe() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
    }

    public static class Upgrades
    {
        public const int MaxLevel = 5;
        public const int Count = 6;

        public static readonly string[] Names = { "Engine", "Tires", "Suspension", "Fuel Tank", "Armor", "Rocket Booster" };

        public static readonly string[] Info =
        {
            "More torque and a higher top speed.",
            "Better grip for climbing hills.",
            "Softer landings and better traction.",
            "Drive further before running dry.",
            "Plating and a ram. Take more hits.",
            "Strap a rocket on. Hold BOOST to fly."
        };

        static readonly int[] baseCost = { 120, 300, 700, 1400, 2800 };
        static readonly float[] mult = { 1.2f, 0.8f, 0.7f, 1.0f, 1.0f, 1.5f };

        /// <summary>Price of the next level, or -1 when maxed.</summary>
        public static int Cost(UpgradeType t, int currentLevel)
        {
            if (currentLevel >= MaxLevel) return -1;
            return Mathf.RoundToInt(baseCost[currentLevel] * mult[(int)t] / 10f) * 10;
        }
    }

    /// <summary>All tunable physics numbers for the car, derived from upgrade levels.</summary>
    public struct CarStats
    {
        public float mass;          // kg
        public float torque;        // total Nm at the wheels
        public float topSpeed;      // m/s
        public float grip;          // tyre friction stiffness
        public float spring;        // N/m per wheel
        public float damper;        // Ns/m per wheel
        public float travel;        // suspension distance m
        public float fuelCapacity;  // seconds of full throttle
        public float maxHealth;
        public float damageScale;   // 1 = full damage
        public float boostCapacity; // seconds
        public float boostAccel;    // m/s^2
        public int armorLevel, boosterLevel;

        public static CarStats From(SaveData s)
        {
            int e = s.Level(UpgradeType.Engine), t = s.Level(UpgradeType.Tires), su = s.Level(UpgradeType.Suspension);
            int f = s.Level(UpgradeType.FuelTank), a = s.Level(UpgradeType.Armor), b = s.Level(UpgradeType.Booster);
            return new CarStats
            {
                mass = 1450f + a * 70f + (b > 0 ? 60f : 0f),
                torque = 2600f + e * 900f,
                topSpeed = 21f + e * 4.5f,
                grip = 1.0f + t * 0.22f,
                spring = 38000f + su * 7000f,
                damper = 3600f + su * 650f,
                travel = 0.30f + su * 0.035f,
                fuelCapacity = 22f + f * 11f,
                maxHealth = 100f + a * 45f,
                damageScale = 1f - a * 0.11f,
                boostCapacity = b > 0 ? 2.5f + b * 1.3f : 0f,
                boostAccel = b > 0 ? 9f + b * 1.8f : 0f,
                armorLevel = a,
                boosterLevel = b
            };
        }
    }

    public class RunStats
    {
        public float startX;
        public float distance;
        public int kills;
        public int pickups;
        public float time;
        public string endReason;
        public bool escaped;
        public int distanceMoney, killMoney, bonusMoney;
        public int Total => distanceMoney + killMoney + bonusMoney;
    }
}
