TB Doctor — v1.1.1 (Timberborn 1.1)
===================================

Terrain-mesh boundary repair
----------------------------
When resized terrain height is a multiple of eight (for example 40), vanilla
TerrainMeshManager may enqueue an upper-halo render-tile update that falls just
outside the range created at load. The missing dictionary key (1,10,5) is a
renderer tile index, NOT the location of a damaged terrain block.

TB Doctor tracks the loaded renderer height. When the next missing top-halo
tile is requested, and the tile directly underneath exists at the same X/Y,
it invokes Timberborn's original InstantiateTile method. The original mesh
update then proceeds normally. This creates a renderer GameObject and mesh
component; it does NOT add/delete terrain, modify a save, or alter buildings.

If the vanilla factory fails, only the exact missing upper-halo dictionary
exception can be contained. Other tile failures still propagate, and warnings
are logged. The patch does no full-map scans and no work during unrelated
render updates beyond constant-time dictionary checks.

The fix uses one unavoidable reflection boundary: the game's internal
TerrainMeshManager type, private InitializeTerrainTiles/UpdateTileMesh hooks,
and private InstantiateTile(Vector3Int) factory. Harmony binds the hooks at
startup. The factory MethodInfo is resolved once and invoked only when a
verified missing halo tile requires repair. The game's private _tiles field
is provided by Harmony field injection. There is no repeated method lookup.

Existing demolition protection and 30-second notifications remain enabled.
Older demolition and notification code still uses private-member reflection;
a separate modernization pass is needed before claiming those paths are fully
typed or conform to every AI guideline.

Verification
------------
Compiled in private TB-dlls GitHub Actions using pinned genuine Timberborn 1.1
and Harmony DLLs. Compilation is not runtime testing. Test the original
height-40 reproduction with construction at Z=37-38; verify that a missing
renderer tile is restored and the game keeps running. Compare with height 41
and test save/reload, both factions, demolition recovery and other mods.

Only this mod's DLL and manifest belong in the installable distribution.
Author: tharbad08.
