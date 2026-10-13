using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Timberborn.ModManagerScene;

namespace DemolitionJobDoctor
{
    public sealed class ModStarter : IModStarter
    {
        public void StartMod(IModEnvironment modEnvironment)
        {
            var harmony = new Harmony("tharbad08.TBDoctor");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Info("loaded; stale demolition targets will be skipped instead of crashing builder job selection");
        }
    }

    internal static class Log
    {
        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        private static readonly object CountLock = new object();

        internal static void Info(string message)
        {
            GameLog.Info(message);
        }

        internal static void Recovered(string patch, Exception ex)
        {
            var key = patch + "|" + ex.GetType().FullName + "|" + ex.Message;
            int count;
            lock (CountLock)
            {
                if (!Counts.TryGetValue(key, out count))
                    count = 0;
                Counts[key] = ++count;
            }

            // Player.log gets complete exception details, including stack trace.
            // Rate-limit identical failures to avoid flooding the log each tick.
            if (count <= 3 || count == 10 || count == 50 || count % 250 == 0)
                GameLog.Warning("Recovered " + patch + " failure #" + count +
                                "; skipped bad demolition job. Details:\n" + ex);

            // One notification per distinct exception signature, on the UI thread.
            if (count == 1)
                NotificationBridge.EnqueueRecovery(patch, ex);
        }
    }

    internal static class RecoveryPolicy
    {
        internal static bool IsRecoverable(Exception ex)
        {
            if (ex == null)
                return false;

            // These are the usual failure modes for an entity/component that was
            // removed, replaced, partially initialized, or invalidated by another mod.
            return ex is NullReferenceException
                || ex is InvalidOperationException
                || ex is ArgumentException
                || ex is IndexOutOfRangeException
                || ex is KeyNotFoundException;
        }
    }

    [HarmonyPatch]
    internal static class DemolishJobProviderIsReachablePatch
    {
        private static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Timberborn.Demolishing.DemolishJobProvider");
            if (type == null)
                throw new MissingMemberException("Timberborn.Demolishing.DemolishJobProvider was not found");

            var candidates = type.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                             BindingFlags.Public | BindingFlags.NonPublic)
                                 .Where(m => m.Name == "IsReachable" && m.ReturnType == typeof(bool))
                                 .ToArray();

            if (candidates.Length != 1)
                throw new AmbiguousMatchException("Expected exactly one bool DemolishJobProvider.IsReachable method, found " + candidates.Length);

            return candidates[0];
        }

        // A finalizer runs even when the original throws. Returning null suppresses the
        // exception. Setting __result=false makes this one target look unreachable, so
        // GetJob can keep scanning other valid demolition jobs.
        private static Exception Finalizer(Exception __exception, ref bool __result)
        {
            if (__exception == null)
                return null;

            if (!RecoveryPolicy.IsRecoverable(__exception))
                return __exception;

            __result = false;
            Log.Recovered("IsReachable", __exception);
            return null;
        }
    }

    [HarmonyPatch]
    internal static class DemolishJobProviderGetJobPatch
    {
        private static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Timberborn.Demolishing.DemolishJobProvider");
            if (type == null)
                throw new MissingMemberException("Timberborn.Demolishing.DemolishJobProvider was not found");

            var candidates = type.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                             BindingFlags.Public | BindingFlags.NonPublic)
                                 .Where(m => m.Name == "GetJob")
                                 .ToArray();

            if (candidates.Length != 1)
                throw new AmbiguousMatchException("Expected exactly one DemolishJobProvider.GetJob method, found " + candidates.Length);

            return candidates[0];
        }

        // Last-resort net. Normally IsReachable handles the bad target and GetJob keeps
        // scanning. If another stale-state exception escapes elsewhere in GetJob, do not
        // crash the simulation; this provider simply yields no job for this decision tick.
        private static Exception Finalizer(Exception __exception)
        {
            if (__exception == null)
                return null;

            if (!RecoveryPolicy.IsRecoverable(__exception))
                return __exception;

            Log.Recovered("GetJob", __exception);
            return null;
        }
    }
}
