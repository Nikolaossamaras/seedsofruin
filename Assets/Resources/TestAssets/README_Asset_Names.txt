Seeds of Ruin — Test Asset Naming Guide
========================================

Drop your assets into the folders below using these exact names.
The test scene will pick them up automatically via Resources.Load.

Folder              | Asset Name           | Type
--------------------|----------------------|------------------
Player/             | PlayerModel          | Prefab (.prefab)
Player/             | PlayerMaterial       | Material (.mat)
Enemies/            | EnemyModel           | Prefab (.prefab)
Enemies/            | EnemyMaterial        | Material (.mat)
Weapons/            | WeaponModel          | Prefab (.prefab)
Ground/             | GroundPrefab         | Prefab (.prefab)
Ground/             | GroundMaterial       | Material (.mat)
UI/                 | HealthBarSprite      | Sprite (.png)
UI/                 | VerdanceBarSprite    | Sprite (.png)
Companions/         | CompanionModel       | Prefab (.prefab)  (generic fallback)
Companions/         | CompanionMaterial    | Material (.mat)    (generic fallback)

Per-companion overrides (optional — place inside the companion's subfolder):

Companions/companion_villager/    | CompanionModel / CompanionMaterial
Companions/companion_farmer/      | CompanionModel / CompanionMaterial
Companions/companion_scout/       | CompanionModel / CompanionMaterial
Companions/companion_apprentice/  | CompanionModel / CompanionMaterial
Companions/companion_knight/      | CompanionModel / CompanionMaterial
Companions/companion_pyromancer/  | CompanionModel / CompanionMaterial
Companions/companion_ranger/      | CompanionModel / CompanionMaterial
Companions/companion_priest/      | CompanionModel / CompanionMaterial
Companions/companion_lyra/        | CompanionModel / CompanionMaterial
Companions/companion_thorne/      | CompanionModel / CompanionMaterial
Companions/companion_selene/      | CompanionModel / CompanionMaterial
Companions/companion_eldara/      | CompanionModel / CompanionMaterial

Notes:
- All paths are relative to Assets/Resources/TestAssets/
- If no custom asset is found the scene falls back to colored primitives.
- Prefabs will have their colliders stripped to avoid conflicts with
  the CharacterController / BoxCollider added at runtime.
- Player prefab is parented under the "Player" root at local (0,1,0).
- Enemy prefab is parented under the enemy root at local (0,0.75,0).
- Weapon prefab is parented to a "WeaponMount" child (or model root).
- Companion prefab is parented under the companion root at local (0,0.8,0).

Companion asset fallback chain:
  1. Per-companion: Companions/{companion_id}/CompanionModel
  2. Generic:       Companions/CompanionModel
  3. Primitive:     Capsule (scale 0.7) colored by element

Companion element colors (used for fallback capsule tinting):
  None    = white        Verdant = green (0.3, 0.9, 0.4)
  Pyro    = orange-red   Hydro   = blue  (0.3, 0.6, 1.0)
  Volt    = yellow       Umbral  = purple (0.6, 0.3, 0.8)
  Cryo    = light blue   Geo     = brown  (0.8, 0.65, 0.3)
