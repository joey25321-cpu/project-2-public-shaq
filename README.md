# Dead Miles

A side-view zombie driving game for Android, built in **Unity 6** with real physics.
Drive an armoured truck across a desert full of walkers and get as far as you can before the fuel runs out.
Spend what you earn on upgrades, then go again until you reach the **evac zone at 2,000 m**.

Tuned for high-end Android phones like the Galaxy S26 Ultra: 120 FPS, Vulkan, URP with HDR, soft shadows and post-processing.

**You don't need to write any code.** The scripts build the whole game (world, truck, zombies, lighting and menus) when you press Play.

---

## What's in the game

| | |
|---|---|
| **Truck physics** | 1.5-tonne rigidbody, four independent suspension wheels with tyre grip, engine torque curve with gear-shift sound, braking and reverse, air control |
| **Zombies** | Every walker is a 10-bone physics ragdoll. When you hit one, it really flies, and your truck loses a little speed |
| **World** | 2 km streamed desert highway with hills, jump ramps, boulders, dead trees and telegraph poles. Physics obstacles: crate stacks, barrels, wrecked cars and breakable barricades. Fuel cans to pick up |
| **Upgrades** | Engine, tyres, suspension, fuel tank, armour (adds plating, a spiked ram and window bars) and a rocket booster. Each has 5 levels |
| **Graphics** | Seamless rock, sand, dirt and rusty-paint textures with normal maps (generated in code), a procedural sky, dusty fog, ACES tonemapping, bloom, colour grading, dust, blood, sparks and smoke |
| **Sound** | Synthesised engine, impacts, splats and pickups |
| **Saving** | Progress saves automatically on the phone |

### Controls

| Phone (touch) | Keyboard (testing on PC) |
|---|---|
| **GAS**, bottom right | D or → |
| **BRAKE / reverse**, next to gas | A or ← |
| **BOOST**, above gas (needs the rocket upgrade) | Space |
| **LEAN BACK / LEAN FWD**, bottom left | W/↑ and S/↓ |
| **II**, top right, pauses | Esc |

Tip: lean back in the air so you land on your wheels.

---

## How to play it on your phone

### What you need (one time)
1. A Windows PC or Mac.
2. **Unity Hub** (free): https://unity.com/download
3. In Unity Hub, open **Installs**, click **Install Editor** and pick the latest **Unity 6 LTS** (6000.0.x).
   On the modules screen, tick **Android Build Support**, plus **OpenJDK** and **Android SDK & NDK Tools** underneath it.

### Open the project
1. Download this project from GitHub: **Code**, then **Download ZIP**. Unzip it somewhere easy, such as your Desktop.
2. In Unity Hub, click **Add**, then **Add project from disk**, and pick the unzipped folder.
3. Open it. The first time takes about 5–10 minutes.
   - If Unity asks which version to use, pick your Unity 6 version.
   - If a popup asks to *enable the new Input System backends*, click **Yes**. Unity restarts.
4. Setup runs on its own. When the Console says **"[Dead Miles] Setup complete"**, press **Play ▶** at the top to test with the keyboard.

### Put it on your S26 Ultra
1. On the phone: **Settings → About phone → Software information**. Tap **Build number** 7 times to turn on Developer options.
2. **Settings → Developer options**: turn on **USB debugging**.
3. Plug the phone into the PC with a USB cable and tap **Allow** on the phone.
4. In Unity's top menu: **Dead Miles → Build and Run on Phone (USB)**.
   The first build takes a while (10–20 min). After that, the game opens on your phone on its own.

To build an APK file you can copy to the phone instead, use **Dead Miles → Build Android APK**.
The file ends up in `Builds/Android/DeadMiles.apk`.

### Build in the cloud (optional)
There's a GitHub Action that builds the APK for you (**Actions → Build Android APK → Run workflow**).
It needs three repository secrets: `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD`.
The licence file comes from activating Unity once on a PC. See https://game.ci/docs/github/activation.

---

## Project layout

```
Assets/DeadMiles/Scripts/   the game (runs on the phone)
  GameManager.cs            game loop: garage → drive → results
  CarController.cs          truck physics, fuel, damage, booster
  Zombie.cs                 ragdoll walkers
  WorldGen.cs               terrain, road, props, obstacles, fuel cans, evac gate
  CameraRig.cs              side-view follow camera and garage orbit
  Atmosphere.cs             sun, sky, fog and post-processing
  GameUI.cs / GameInput.cs  menus, HUD and touch controls
  Progress.cs               save data, upgrade prices and car stats
  Tex.cs / Mats.cs / Geo.cs procedural textures, materials and meshes
  Fx.cs / Sfx.cs            particles and synthesised sound
Assets/DeadMiles/Editor/    automatic project setup and build menu
```

## Making it even more realistic

The game currently builds every model from code, so it runs without any downloads.
For photoreal graphics, the next step is to swap in real 3D assets: a scanned truck model, rigged zombie characters and photo-scanned ground textures.
Many good ones are free on the Unity Asset Store or Fab. Once they're imported, the code can be pointed at them.
