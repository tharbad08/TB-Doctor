using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace DemolitionJobDoctor
{
    // Unity's logger writes to Player.log, with a safe console fallback.
    internal static class GameLog
    {
        private static readonly MethodInfo UnityInfo = ResolveMethod("Log");
        private static readonly MethodInfo UnityWarning = ResolveMethod("LogWarning");

        private static MethodInfo ResolveMethod(string name)
        {
            Type debug = Type.GetType("UnityEngine.Debug, UnityEngine.CoreModule", false)
                         ?? AccessTools.TypeByName("UnityEngine.Debug");
            return debug == null ? null : debug.GetMethod(name, new[] { typeof(object) });
        }

        internal static void Info(string message) { Write(UnityInfo, message); }
        internal static void Warning(string message) { Write(UnityWarning, message); }

        private static void Write(MethodInfo logger, string message)
        {
            string line = "[TB Doctor] " + message;
            try
            {
                if (logger != null)
                {
                    logger.Invoke(null, new object[] { line });
                    return;
                }
            }
            catch (Exception) { /* Never throw from a demolition finalizer. */ }
            Console.WriteLine(line);
        }
    }

    internal static class NotificationBridge
    {
        private const string Prefix = "TB Doctor: ";
        private const int MaxPending = 24;
        private const double DisplaySeconds = 30.0;
        private static readonly object Sync = new object();
        private static readonly Queue<string> Pending = new Queue<string>();
        private static object _panel;
        private static object _service;
        private static MethodInfo _sendWarning;
        private static FieldInfo _durationField;
        private static double _nextNotificationAt;

        internal static void EnqueueRecovery(string patch, Exception error)
        {
            string text = Prefix + "broken demolition job skipped (" + patch +
                          ", " + error.GetType().Name + "). See Player.log.";
            lock (Sync)
            {
                if (Pending.Count < MaxPending)
                    Pending.Enqueue(text);
            }
        }

        internal static void AttachPanel(object panel)
        {
            try
            {
                Type type = panel.GetType();
                FieldInfo serviceField = AccessTools.Field(type, "_quickNotificationService");
                object service = serviceField == null ? null : serviceField.GetValue(panel);
                MethodInfo send = service == null ? null : AccessTools.Method(service.GetType(), "SendWarningNotification", new[] { typeof(string) });
                FieldInfo duration = AccessTools.Field(type, "_duration");
                if (service == null || send == null || duration == null)
                {
                    GameLog.Warning("QuickNotificationPanel fields or service not found; in-game alerts unavailable.");
                    return;
                }
                lock (Sync)
                {
                    _panel = panel;
                    _service = service;
                    _sendWarning = send;
                    _durationField = duration;
                    _nextNotificationAt = 0;
                }
                GameLog.Info("Native 30-second demolition warnings enabled.");
            }
            catch (Exception ex)
            {
                GameLog.Warning("Unable to connect to QuickNotificationService: " + ex);
            }
        }

        // Deliver notifications only during the game's UI update, never from
        // the demolition-job exception handler. Queue is bounded and thread-safe.
        internal static void Update(object panel)
        {
            string text;
            object service;
            MethodInfo send;
            lock (Sync)
            {
                if (!ReferenceEquals(_panel, panel) || _service == null || Pending.Count == 0)
                    return;
                double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                if (now < _nextNotificationAt)
                    return;
                text = Pending.Dequeue();
                service = _service;
                send = _sendWarning;
                _nextNotificationAt = now + DisplaySeconds;
            }
            try
            {
                send.Invoke(service, new object[] { text });
            }
            catch (Exception ex)
            {
                GameLog.Warning("Cannot display demolition warning: " + ex);
            }
        }

        // The built-in QuickNotificationPanel automatically hides alerts.
        // Override only TB Doctor's duration, leaving others unchanged.
        internal static void ExtendOurWarning(object panel, object eventArgs)
        {
            if (eventArgs == null || panel == null)
                return;
            try
            {
                PropertyInfo textProperty = eventArgs.GetType().GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
                string text = textProperty == null ? null : textProperty.GetValue(eventArgs, null) as string;
                if (text == null || !text.StartsWith(Prefix, StringComparison.Ordinal))
                    return;
                FieldInfo duration = _durationField ?? AccessTools.Field(panel.GetType(), "_duration");
                if (duration != null)
                    duration.SetValue(panel, (float)DisplaySeconds);
            }
            catch (Exception ex)
            {
                GameLog.Warning("Unable to apply 30-second notification duration: " + ex);
            }
        }
    }

    [HarmonyPatch]
    internal static class QuickNotificationPanelLoadPatch
    {
        private static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName("Timberborn.QuickNotificationSystem.QuickNotificationPanel");
            return type == null ? null : AccessTools.Method(type, "Load");
        }

        private static void Postfix(object __instance)
        {
            NotificationBridge.AttachPanel(__instance);
        }
    }

    [HarmonyPatch]
    internal static class QuickNotificationPanelUpdatePatch
    {
        private static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName("Timberborn.QuickNotificationSystem.QuickNotificationPanel");
            return type == null ? null : AccessTools.Method(type, "UpdateSingleton");
        }

        private static void Prefix(object __instance)
        {
            NotificationBridge.Update(__instance);
        }
    }

    [HarmonyPatch]
    internal static class QuickNotificationPanelDurationPatch
    {
        private static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName("Timberborn.QuickNotificationSystem.QuickNotificationPanel");
            return type == null ? null : AccessTools.Method(type, "OnNotificationSent");
        }

        // __1 is the event args: second parameter of the native event handler.
        private static void Postfix(object __instance, object __1)
        {
            NotificationBridge.ExtendOurWarning(__instance, __1);
        }
    }
}
