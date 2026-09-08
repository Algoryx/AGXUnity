Run `pwsh -NoProfile -File Tests/LicenseManager~/Run.ps1` from the repository root with a .NET SDK installed.

This harness compiles the actual license manager, metadata parser, warning logic, and editor window against simulated AGX and Unity APIs. It uses temporary synthetic license files and never contacts a license server. Unity ignores the `~` directory, so these API substitutes are not included in project assemblies. Compiler outputs and synthetic files go to the system temporary directory.

The checks exercise asynchronous success, failure, exceptions and overlap; manual return across simulated reload and play transitions; source path identity; deletion ordering; imports; non-floating actions; warning suppression; and the controls emitted by the window. They do not verify native server behavior or Unity's visual layout. Check Connect/Return and server seat counts against authorized infrastructure, and inspect the window in Unity before release.

Loading regressions cover rejected service and legacy loads while an older license remains valid, preservation of failed runtime activation requests, cache refresh after exceptions, and automatic searches preserving valid native licenses without restoring stale metadata after failure.

The editor lifecycle checks use the same loading options as editor initialization: a floating file under Assets connects at startup even if native initialization found a valid non-floating license in the AGX installation. Repeated initialization preserves an existing seat, and manual return pauses checkout until Connect succeeds or a simulated editor restart clears session storage. Checks also cover preserving the native fallback when checkout is disabled or no floating file exists, and refreshing actual native validity when checkout fails after clearing that fallback.

Asset-import worker checks reject all license operations before native runtime access. The simulated editor API also rejects calls from background threads, exercising the cached process check used by asynchronous operations.
