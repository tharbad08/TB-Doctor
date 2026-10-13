Demolition Job Doctor 0.1.0
===========================

Purpose
-------
A generic Timberborn 1.1 safety mod for demolition-job failures caused by stale,
removed, replaced, or partially initialized entities/components from any content mod.

Behavior
--------
1. DemolishJobProvider.IsReachable:
   - If a recoverable stale-state exception is thrown, the target is treated as
     unreachable and skipped.
   - The provider can continue scanning the remaining demolition jobs.

2. DemolishJobProvider.GetJob:
   - Last-resort finalizer. If a recoverable exception still escapes, the exception
     is suppressed for that decision tick so the simulation does not crash.

3. Logging:
   - Player.log contains [DemolitionJobDoctor] recovery messages.
   - Repeated identical failures are rate-limited to avoid log spam.

The mod does NOT blindly delete demolition orders. It quarantines only targets that
actually throw. This avoids accidentally removing legitimate demolition work.

Recoverable exception classes:
- NullReferenceException
- InvalidOperationException
- ArgumentException
- IndexOutOfRangeException
- KeyNotFoundException

Other exceptions are deliberately allowed through so unrelated serious bugs are not
silently hidden.

Build
-----
This source package includes build-arch-linux.sh. It auto-detects the standard Steam
Timberborn install and Workshop Harmony mod. Run it from this folder.

After Code.dll is built, install the folder in:
  ~/Documents/Timberborn/Mods/DemolitionJobDoctor/

The folder should contain at least:
  manifest.json
  Code.dll

Compatibility note
------------------
If using Keystone MOM Compat v6, remove the v6 demolition crash guard once this
standalone mod is installed. Keep the Keystone/MoM blueprint compatibility changes;
the generic demolition protection belongs here instead.
