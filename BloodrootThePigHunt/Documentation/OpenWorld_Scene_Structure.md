# Bloodroot open-world scene foundation

The campaign foundation uses two Unity scenes:

- `Assets/Scenes/Campaign/Farm_PrologueHub.unity` is built from `Assets/Scenes/NewLevel_BaseNoTouch.unity` and contains the prologue-state and post-prologue hub-state placeholders.
- `Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity` is the one continuous open-world level containing Black Pines Forest, Stillwater Feed Mill, Harrow Estate, and Bloodroot Hollow.

The truck is the intended transition between the Farm and the open world. Moving between open-world regions must not load another scene.

## Generated folder structure

```text
Assets/
├── Audio/OpenWorld/
├── Editor/
│   ├── BloodrootOpenWorldSetup.cs
│   └── BloodrootOpenWorldTerrainProduction.cs
├── Materials/OpenWorld/
│   └── TerrainProduction/
├── PreFabs/OpenWorld/
│   ├── Farm/
│   ├── Gates/
│   └── Travel/
├── Scenes/
│   ├── Campaign/Farm_PrologueHub.unity
│   └── OpenWorld/
│       ├── Backups/Bloodroot_OpenWorld_PreTerrainProduction.unity
│       ├── Bloodroot_OpenWorld.unity
│       └── Data/
│           ├── Bloodroot_OpenWorld_Terrain.asset
│           └── Bloodroot_OpenWorld_Terrain_Production.asset
├── Scripts/Features/OpenWorld/
└── VFX/OpenWorld/
```

## Farm state hierarchy

The setup tool adds `__CAMPAIGN_STRUCTURE` without moving or replacing the copied farm content. This keeps the source farm intact while providing roots for later state wiring:

```text
__CAMPAIGN_STRUCTURE
├── _CORE
├── _PROLOGUE_STATE
└── _HUB_STATE
```

`_HUB_STATE` begins inactive. The future Farm state controller will disable `_PROLOGUE_STATE` and enable `_HUB_STATE` after the prologue is completed.

## Open-world hierarchy

```text
Bloodroot_OpenWorld
├── _CORE
├── _TERRAIN
├── _LIGHTING
├── AREA_00_BLACK_PINES_FOREST
├── AREA_01_STILLWATER_FEED_MILL
├── AREA_02_HARROW_ESTATE
└── AREA_03_BLOODROOT_HOLLOW
```

Every region's `Environment` remains loaded. Only the current area's mission-system root should be active at runtime. This is important because existing gameplay scripts locate managers and spawners globally.

## Editor commands

Use these Unity menu commands:

```text
Bloodroot > Open World > Create Farm + Open World Scene Structure
Bloodroot > Open World > Recreate Farm Hub From NewLevel BaseNoTouch
Bloodroot > Open World > Validate Farm + Open World Scene Structure
Bloodroot > Open World > Build Production Terrain Pass
Bloodroot > Open World > Repair Production Terrain Pass
Bloodroot > Open World > Validate Production Terrain Pass
Bloodroot > Open World > Recover Incomplete Terrain Repair
```

The creation command is intentionally non-destructive. If either target scene already exists, it stops without overwriting it. It also creates and saves the scenes additively so the scene already open in Unity is not saved, closed, or replaced. The Farm-only recreation command transactionally replaces `Farm_PrologueHub` from the saved `NewLevel_BaseNoTouch` asset without changing `Bloodroot_OpenWorld`.

## Production terrain pass

The one-shot terrain builder keeps `Bloodroot_OpenWorld_Terrain.asset` as the untouched baseline and assigns a separate `Bloodroot_OpenWorld_Terrain_Production.asset` to the live open-world scene. Before the first build, it also creates `Bloodroot_OpenWorld_PreTerrainProduction.unity` outside Build Settings.

The production TerrainData uses a 1025 heightmap, a 512 alphamap, a 1,400 x 1,400 metre footprint, and five ordered surface layers: Forest Loam, Wet Mud, Gravel Road, Exposed Clay Rock, and Bloodroot Corruption. Every layer uses constant low smoothness, zero metallic response, and black specular color so opaque albedo alpha cannot make the landscape glossy. Multiscale domain-warped relief gives the wilderness broad rolling forms plus smaller ground variation while protected pads and the truck-road core remain playable.

Harrow Estate occupies an irregular hill mass with a dedicated buildable overlook pad around 68 metres world elevation. Bloodroot Hollow uses a varying-radius, varying-height rim with one aligned southeastern entrance instead of a uniform geometric bowl. Its off-road arena floor remains gently uneven and playable. Black Pines Creek follows an 18-point meandering drainage, descends continuously, forms varied banks and floodplain widths, and stays clear of the truck road until a bridge or culvert is intentionally designed. The primary truck road is graded after the macro landforms and drainage so later terrain operations do not cut the route.

`Repair Production Terrain Pass` deterministically regenerates the production TerrainData and re-grounds the route markers. It creates a temporary TerrainData backup and rolls the terrain and scene back if the repair fails. If Unity is interrupted during that transaction, `Recover Incomplete Terrain Repair` restores the saved terrain and scene before another repair is attempted. The repair also heals missing internal alphamap control textures before painting. `Validate Production Terrain Pass` verifies terrain size and relief, five readable and normalized painted layers backed by two non-null control textures, matte layer settings and identity, wilderness variation, Harrow's buildable overlook and relative height, Hollow's irregular rim and playable floor, road grade and crossfall, creek sinuosity, descent, banks and road separation, route guides, progression spawn/gate grounding, backup integrity, Build Settings, and the NavMesh Surface configuration.

The terrain pass configures the open-world `NavMeshSurface` to use physics colliders, but deliberately does not bake it. Bake only after the major buildings, roads, rocks, bridges, collision meshes, and terrain holes are final enough for AI testing.

## What to inspect in the Unity Editor

1. In the **Project** window, open `Assets > Scenes > Campaign > Farm_PrologueHub`.
2. In the **Hierarchy**, expand `__CAMPAIGN_STRUCTURE`.
3. Confirm `_PROLOGUE_STATE` is active and `_HUB_STATE` is inactive. The existing Farm environment remains outside these roots so it can serve both states.
4. Select the Farm's existing `Level NavMesh Surface` object and use its **NavMesh Surface** component's **Bake** button before testing enemy AI. Unity clears the baked NavMesh data reference when a scene containing a `NavMeshSurface` is duplicated.
5. Open `Assets > Scenes > OpenWorld > Bloodroot_OpenWorld`.
6. In the **Hierarchy**, expand `Bloodroot_OpenWorld`. Black Pines mission systems begin active. Stillwater, Harrow, and Bloodroot Hollow mission systems begin inactive and each has a `Locked Entrance` placeholder.
7. Expand `_TERRAIN > Roads > Primary Progression Road` and `_TERRAIN > Rivers > Black Pines Creek` to inspect the generated route waypoints.
8. Select `_TERRAIN > Open World Terrain`. Its **Terrain Data** must be `Bloodroot_OpenWorld_Terrain_Production`, not the baseline asset.
9. Run **Bloodroot > Open World > Validate Production Terrain Pass** after any deterministic terrain regeneration.
10. Open **File > Build Profiles**, select the active platform profile, and inspect its **Scene List**. `Farm_PrologueHub` is index 0 and `Bloodroot_OpenWorld` is index 1. The previous `OutDated Level` entry remains enabled at index 2 so this foundation does not remove existing project setup.
11. Keep all four regions in `Bloodroot_OpenWorld`; do not create a scene transition between them. Only the Farm truck transition should load or unload the open-world scene.

## Next implementation stage

The scene foundation deliberately leaves gameplay wiring for a separate, reviewable pass:

1. Place the Farm prologue and hub spawn markers.
2. Add the Farm state controller and prologue completion trigger behavior.
3. Replace the open-world return-truck placeholder with the production truck visual.
4. Add truck interaction between the Farm and open world.
5. Add the open-world progression manager and gate behavior.
6. Move each area's enemies, objectives, spawners, and wave manager under its mission-system root.
7. Import or create production pine, rock/cliff, grass/brush, water, road/bridge, feed-mill, estate, and rural-clutter kits; those assets are not currently present in the repository.
8. Dress the completed terrain without moving the progression markers or cutting the graded truck corridor.
9. Add colliders to production props and bake the open-world NavMesh.
