using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Timberborn.TerrainSystem;
using UnityEngine;

namespace DemolitionJobDoctor
{
    // Timberborn's 8-layer renderer grid can invalidate the first tile beyond
    // the configured height when terrain height is a multiple of eight.
    // This restores ONLY the renderer tile, never terrain voxels.
    internal static class TerrainTileBoundaryRecovery
    {
        private const int TileHeight = 8;
        private static readonly ConditionalWeakTable<object, State> States =
            new ConditionalWeakTable<object, State>();
        private static readonly MethodInfo InstantiateTileMethod =
            (MethodInfo)FindMethod("InstantiateTile", new[] { typeof(Vector3Int) });
        [ThreadStatic] private static bool _repairing;

        private sealed class State
        {
            internal readonly int Height;
            internal State(int height) { Height = height; }
        }

        internal static MethodBase FindMethod(string name, Type[] arguments)
        {
            // Private renderer methods are not exposed as a public modding API.
            // Harmony resolves these only during patch setup (not per frame).
            var type = AccessTools.TypeByName(
                "Timberborn.TerrainSystemRendering.TerrainMeshManager");
            if (type == null)
                throw new MissingMemberException("TerrainMeshManager missing");
            return AccessTools.Method(type, name, arguments)
                ?? throw new MissingMethodException(type.FullName, name);
        }

        internal static void Track(object manager, ITerrainService terrain)
        {
            if (manager == null || terrain == null) return;
            States.Remove(manager);
            int height = terrain.Size.z;
            States.Add(manager, new State(height));
            if (height > 0 && height % TileHeight == 0)
                Log.Info("terrain renderer height=" + height
                    + " aligns with tile boundary; repair enabled");
        }

        private static bool IsExpectedHalo(object manager, Vector3Int index, IDictionary tiles)
        {
            if (manager == null || tiles == null
                || !States.TryGetValue(manager, out var state))
                return false;
            int height = state.Height;
            return height > 0 && height % TileHeight == 0
                && index.z == height / TileHeight
                && index.x >= 0 && index.y >= 0
                && tiles.Contains(new Vector3Int(index.x, index.y, index.z - 1));
        }

        internal static void Restore(object manager, Vector3Int index, object dictionary)
        {
            if (_repairing || !(dictionary is IDictionary tiles)
                || tiles.Contains(index) || !IsExpectedHalo(manager, index, tiles))
                return;

            _repairing = true;
            try
            {
                // The genuine game factory creates correct TileComponents,
                // registers the tile, and parents its GameObject properly.
                InstantiateTileMethod.Invoke(manager, new object[] { index });
                if (tiles.Contains(index))
                    Log.Info("restored missing renderer tile " + index
                        + " (no terrain or structures changed)");
                else
                    GameLog.Warning("factory did not register renderer tile " + index);
            }
            catch (Exception exception)
            {
                var cause = exception is TargetInvocationException wrapped
                    && wrapped.InnerException != null ? wrapped.InnerException : exception;
                GameLog.Warning("renderer tile creation failed at " + index + ": " + cause);
            }
            finally { _repairing = false; }
        }

        internal static Exception FinalizeUpdate(
            object manager, Vector3Int index, object dictionary, Exception exception)
        {
            if (!(exception is KeyNotFoundException)
                || !(dictionary is IDictionary tiles)
                || tiles.Contains(index) || !IsExpectedHalo(manager, index, tiles))
                return exception;

            // If factory creation failed, suppress only the known boundary
            // dictionary miss. The next terrain change retries the repair.
            Log.Recovered("TerrainMeshManager upper-halo " + index, exception);
            return null;
        }
    }

    [HarmonyPatch]
    internal static class TerrainTileInitializePatch
    {
        private static MethodBase TargetMethod() =>
            TerrainTileBoundaryRecovery.FindMethod("InitializeTerrainTiles", Type.EmptyTypes);

        private static void Postfix(object __instance, ITerrainService ____terrainService) =>
            TerrainTileBoundaryRecovery.Track(__instance, ____terrainService);
    }

    [HarmonyPatch]
    internal static class TerrainTileUpdatePatch
    {
        private static MethodBase TargetMethod() =>
            TerrainTileBoundaryRecovery.FindMethod(
                "UpdateTileMesh", new[] { typeof(Vector3Int) });

        private static void Prefix(object __instance, Vector3Int __0, object ____tiles) =>
            TerrainTileBoundaryRecovery.Restore(__instance, __0, ____tiles);

        private static Exception Finalizer(object __instance, Vector3Int __0,
            object ____tiles, Exception __exception) =>
            TerrainTileBoundaryRecovery.FinalizeUpdate(
                __instance, __0, ____tiles, __exception);
    }
}
