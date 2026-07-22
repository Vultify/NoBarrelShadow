# Handoff: Black Division Assets — Clothing, Voicelines & Loadouts

**Date:** 2026-07-22
**Scope:** Reconnaissance of Black Division content across two external SPT (Single Player Tarkov) mod repos, in support of NoBarrelShadow.
**Status:** Research complete. No code changes made to this repo.

## Repos surveyed

| Repo | Role | Language |
|---|---|---|
| [`TacticalToaster/BlackDiv`](https://github.com/TacticalToaster/BlackDiv) | Server/plugin mod that spawns Black Division as an AI faction via **MoreBotsAPI**. Owns loadout *references* + appearance/voice wiring. Latest release **1.1.3** (2026-07-04). | C# |
| [`WelcomeToTarkov/Tarkov-1.0-Backport`](https://github.com/WelcomeToTarkov/Tarkov-1.0-Backport) | Ports Tarkov 1.0 content to SPT 4.0. Owns the *definitions* of every custom item ID and the physical bundle assets (clothing prefabs + voice audio). | C# |

**Division of labor:** `BlackDiv` says *which* IDs to equip and how the bot looks/sounds; `Tarkov-1.0-Backport` supplies *what those IDs are* and the actual Unity `.bundle` assets.

---

## 1. Clothing / Appearance

**Asset bundles** (`Tarkov-1.0-Backport`, under `WTT-ContentBackport/Resources/bundles/assets/content/characters/character/prefabs/`):

- Operator kit: `top_bd_operator_02/03/04.bundle` + `pants_bd_operator_02/03/04.bundle`
- Team-leader kit: `top_bd_team_leader_01.bundle` + `pants_bd_team_leader_01.bundle`

**Wiring** — appearance block in `BlackDiv/Server/Resources/db/bots/sharedTypes/blackDiv.json`. The bot rolls one top + one pant per spawn:

- **Body (tops):** `688ddcdd0551c30a1700d75e`, `688de6ea74514d35fd003554`, `688dec55152c0d07670564af`, `688df7e020025952eb0e0628`
- **Feet (pants):** `688dddcd152c0d0767056496`, `688de71374514d35fd003556`, `688dec1429e9479b850276b9`, `688df7b620025952eb0e0626`
- **Hands:** `5cc2e68f14c02e28b47de290`
- **Head:** `5cde96047d6c8b20b577f016`, `5fdb5950f5264a66150d1c6e`, `60a6aa8fd559ae040d0d951f`, `60a6aaad42fd2735e4589978` (stock game heads)

4 tops → 4 body IDs and 4 pants → 4 feet IDs, i.e. the 3 operator variants + 1 team-leader variant.

---

## 2. Voicelines

Two voice sets, shipped as `black_division_1_voice.bundle` and `black_division_2_voice.bundle` (in `.../audio/phrases/`), backed by a full phrase tree at `.../audio/phrases/black_division/black_division_01/` (and `_02/`).

Referenced in the bot config via voice IDs `6899fcdea469e729c40cbbde` and `6899fff4c17f776ecb07fafd`.

Phrase categories present:

- **command/** — attention, getincover, goforward, goloot, holdposition, onhealing, spreadout, stop, suppress
- **contact/** — inthefront, leftflank, rightflank, onsix, scav
- **enemy/** — enemydown, enemyhit, scavdown, lostvisual, noise
- **generic/** — ~20 lines: fight, mutter, onagony, onbeinghurt, onbreath, ondeath, onenemydown, onenemygrenade, onfirstcontact, onfriendlydown, ongoodwork, ongrenade, onoutofammo, onweaponreload, etc.
- **health/** — brokenhand, brokenleg, hurtlight/medium/heavy, hurtneardeath
- **help/** — needammo, needmedkit, needweapon
- **reaction/** — covering, goodwork, negative, onloot, onposition, roger
- **situationalcommand/** — checkhim · **situationalreaction/** — lootbody, weaponbroken, weaponjammed
- **teamstatus/** — down, friendlyfire, hit

---

## 3. Loadouts

Defined in `BlackDiv/Server/Resources/db/`:

- `CustomBotLoadouts/blackdivassault.json` — primary/detailed gear table
- `ModBotLoadouts/Armory/blackdivassault.json` — Armory/MoreBots variant
- `bots/sharedConfig/blackDiv.jsonc` — behavior tuning
- `config.jsonc` — spawn tuning

### `CustomBotLoadouts/blackdivassault.json` — equipment IDs (verbatim, with weights)

| Slot | Item IDs (weight) |
|---|---|
| FirstPrimaryWeapon | `5ba26383d4351e00334c93d9` (10), `5de7bd7bfd6b4e6e2276dc25` (10), `6183afd850224f204c1da514` (15), `5df8ce05b11454561e39243b` (7), `5447a9cd4bdc2dbd208b4567` (20), `5bb2475ed4351e00853264e3` (20), `661ceb1b9311543c7104149b` (7), `5fbcc1d9016cce60e8341ab3` (20) |
| SecondPrimaryWeapon | `5e81ebcd8e146c7080625e15` (5), `6275303a9f372d6ea97f9ec7` (1) |
| Holster | `5d67abc1a4b93614ec50137f` (1) |
| TacticalVest | `689479a4a733b1602007e2eb` (1), `689479cb47e5acd1e10be986` (4), `68947a4be4bf255d1b0ca746` (2), `689479eb30cc5ba7be00f5ff` (3) |
| Headwear | `5e4bfc1586f774264f7582d3`, `5e00c1ad86f774747333222c`, `5ea17ca01412a1425304d1c0`, `5b40e1525acfc4771e1c6611`, `6759655674aa5e0825040d62` (all 1) |
| FaceCover | `689b880fff8b4adc420f5b56` (1), `689b404db49f27df1c0873f6` (1), `5ab8f39486f7745cd93a1cca` (2), `5b432f3d5acfc4704b4a1dfb` (2), `5ab8f4ff86f77431c60d91ba` (1) |
| Backpack | `68947ab5a733b1602007e2fe`, `68947a8ce4bf255d1b0ca759`, `68947ad3e4bf255d1b0ca75c` (all 1) |
| Earpiece | `628e4e576d783146b124c64d`, `66b5f693acff495a294927e3`, `66b5f6985891c84aab75ca76` (all 1) |
| Eyewear | `5d6d2e22a4b9361bd5780d05`, `62a61c988ec41a51b34758d5`, `557ff21e4bdc2d89578b4586` (all 1) |

### `ModBotLoadouts/Armory/blackdivassault.json` — equipment IDs (verbatim, with weights)

| Slot | Item IDs (weight) |
|---|---|
| FirstPrimaryWeapon | `69066bef905ee9e06c462009` (20), `6906a1aeef59ca68d128e8b7` (20), `687afda52dc9fd6c0e14c602` (15), `6761b213607f9a6f79017c7e` (15), `66e718dc498d978477e0ba75` (10), `6920b28eabc4f9d229cb7e49` (8), `664a5b945636ce820472f225` (10) |
| Holster | `68b7f4060a4536984f82cf4b`, `665fe0e865683281eb8e7ed6`, `68452c3da87156b67d9ec538`, `6761b213607f9a6f79017d23` (all 1) |

### Behavior — `bots/sharedConfig/blackDiv.jsonc`

Face shields always down; forced weapon stocks; lasers ~75%; lights 25% day / 75% night; NVGs 10% day / 100% night; 25 preset loadout variations; carries USD ($100–500) and EUR (€50–300), no roubles; **not** flagged as a boss; `mustHaveUniqueName: false`.

### Spawns — `config.jsonc`

~25% spawn chance; hunt groups of 3–5; ~10 maps incl. Streets, Customs (`bigmap`), Labs, `labyrinth`; Labs gate spawn 20%, Labs start spawn 15%.

### Extras

- Achievements: `CustomAchievements/Achievements/BD_achievements.json` (common / rare / legendary tiers; icons `bdcommon.png`, `bdrare.png`, `bdlegendary.png`).
- Locale: `CustomLocales/en.json` → `ScavRole/BlackDiv` = **"Black Div."**

---

## 4. Vanilla vs. backported-custom items

The first 8 hex chars of a mongoID are a Unix timestamp, which cleanly separates base-game items from the new Tarkov-1.0 items the Backport adds.

**Vanilla EFT base items** (IDs ~2018–2022) — the `CustomBotLoadouts` file leans on these. Confidently identified:

- `5447a9cd4bdc2dbd208b4567` = Colt M4A1 5.56 · `5bb2475ed4351e00853264e3` = HK 416A5 5.56 · `5df8ce05b11454561e39243b` = AK-103 7.62×39 · `6183afd850224f204c1da514` = FN SCAR-H 7.62×51 · `5fbcc1d9016cce60e8341ab3` = SIG MCX .300 BLK · `5ba26383d4351e00334c93d9` = Desert Tech MDR 5.56 · `5e81ebcd8e146c7080625e15` = Colt M1911A1 .45
- Helmets: `5e4bfc1586f774264f7582d3` = MSA Gallet TC 800 · `5e00c1ad86f774747333222c` = LShZ-2DTM · `5ea17ca01412a1425304d1c0` = Ops-Core FAST MT

> Remaining vanilla IDs (face covers, eyewear, earpieces, some weapons) were **not** name-resolved to avoid guessing. Resolve against a Tarkov item DB (e.g. tarkov.dev) if names are needed.

**Backported Tarkov-1.0 custom items** (IDs late-2024 → 2026, minted by the mods):

- All `Armory` weapons (`69066bef…`, `6906a1ae…`, `687afda5…`, `6761b213…`, `66e718dc…`, `6920b28e…`, `664a5b94…`) and holsters (`68b7f406…`, `665fe0e8…`, `68452c3d…`).
- `CustomBotLoadouts` vests (`689479…`, `68947a…`), backpacks (`68947a…`), face covers (`689b88…`, `689b40…`), newest helmet (`6759655674aa5e0825040d62`).
- All clothing IDs in the appearance block (`688ddcdd…`, `688de6ea…`, …).

---

## 5. Where the custom definitions live (open thread)

- **Clothing** IDs trace cleanly to the `top_bd_operator_*` / `pants_bd_operator_*` / `*_team_leader_01` prefab bundles (established above).
- **Weapon/armor templates** could **not** be found in a plain JSON:
  - `bundles.json` (242 KB) is only a CRC/dependency manifest — none of the template IDs appear in it.
  - `Helpers/BaseGameItemEdits.cs` (49 KB) only *edits* existing items via a `switch` on ID and rewrites `Prefab.Path`; it does not mint the new IDs.
  - Conclusion: the backported item templates are registered **programmatically** at server load (WTT custom-item service), which is why grepping the repo for those IDs returns nothing.

### Suggested next steps

1. If names for the remaining vanilla IDs are needed, resolve them via a Tarkov item database.
2. To pin down exactly where each `688…` clothing template / `689…`/`690…` gear template is created, inspect the WTT custom-item registration path — start with `Commands/AllTheClothesCommand.cs` and the server bootstrap (`Mod.cs`-equivalent) rather than `BaseGameItemEdits.cs`.
3. Weapon *mod chains* (attachments) for both loadouts were confirmed present but not transcribed here — pull from the `inventory.mods` blocks of each loadout file if a full parts list is required.

---

## Source reference (files touched during recon)

**`TacticalToaster/BlackDiv`**
- `Server/Resources/db/CustomBotLoadouts/blackdivassault.json`
- `Server/Resources/db/ModBotLoadouts/Armory/blackdivassault.json`
- `Server/Resources/db/bots/sharedTypes/blackDiv.json`
- `Server/Resources/db/bots/sharedConfig/blackDiv.jsonc`
- `Server/Resources/config.jsonc`
- `Server/Resources/db/CustomLocales/en.json`
- `Server/Resources/db/CustomAchievements/Achievements/BD_achievements.json`

**`WelcomeToTarkov/Tarkov-1.0-Backport`**
- `WTT-ContentBackport/Resources/bundles/assets/content/audio/phrases/black_division/**`
- `WTT-ContentBackport/Resources/bundles/assets/content/characters/character/prefabs/*_bd_operator_*`, `*_bd_team_leader_*`
- `WTT-ContentBackport/Resources/bundles.json`
- `WTT-ContentBackport/Helpers/BaseGameItemEdits.cs`
