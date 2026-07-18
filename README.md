# NoBarrelShadow

Tarkov draws a fake shadow of your gun barrel inside every flashlight beam. This mod deletes it — for you, the AI, and whatever's lying on the floor.

Shadows from flashlights would always block enemies and cause issues focusing them in your sight.

## What it does

- Kills the barrel shadow on every weapon light, including AI guns and dropped weapons (no more shadows bleeding through walls)
- Per-light range and intensity sliders in F12 — every flashlight in the game gets its own pair
- Covers all vanilla lights and the WTT ContentBackport set out of the box
- Meets a light it doesn't recognize? It learns the item's real name the first time you're in a raid with it, and files it under that from then on

## For mod authors

There's a small API to register your lights under their proper names instead of letting the codename guesser have a go:

```csharp
NoBarrelShadowAPI.RegisterLight(templateId, displayName);
```

Call it from your plugin's `Awake()` — load order doesn't matter.

## Install

Release zip into the SPT root folder. SPT 4.0.x / BepInEx 5.x.

Something broken? [Issues](../../issues) or the mod page comments. [CHANGELOG.md](CHANGELOG.md) for history.
