# TB Doctor AI contributor guide

Scope: Timberborn 1.1 diagnostic and narrowly scoped recovery mod. Preserve original game and mod behavior.

1. Version using `<game version>.<build number>` and use only `tharbad08` for attribution.
2. Confirm target methods, signatures, field types and IL against the genuine game DLLs; source-only CI checks do not count as compilation.
3. Prefer compile-time typed APIs and Harmony annotations. Avoid reflection; document unavoidable private-method boundaries, performance, and why no public API exists. Never reflectively discover members in a per-tick hot path.
4. Recovery must be specific to a verified fault. Do not suppress unrelated exceptions or skip normal updates. Rate-limit warnings.
5. Never invent terrain voxels, remove player structures, or alter the user's saved terrain to work around rendering failures. Preserve map-height settings.
6. Renderer tile repair may only create the one missing upper-halo tile when terrain height aligns with the eight-voxel renderer tile boundary and the valid tile directly below exists. Prefer the game's own tile construction routine.
7. Avoid full-map scans in gameplay, clear scene-scoped state, and preserve other mods' Harmony patches.
8. Build using private genuine game/mod references in GitHub Actions; ship only this mod's DLL and manifest, no reference game DLLs, Harmony, or compile-only stubs.
9. Test with/without Map Transformer, resized terrain 40/41, high-elevation terrain construction, save/reload, and existing demolition recovery. Compilation is not gameplay testing.
