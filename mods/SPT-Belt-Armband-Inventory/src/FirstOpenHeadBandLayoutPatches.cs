using System;

namespace SPTBeltArmbandInventory
{
    // Compatibility shim retained only because Plugin.cs still owns this lifecycle slot.
    // The former six-pass first-open Harmony positioner directly conflicted with the
    // structural Gear Panel reflow by moving slot16 back above the already-shifted
    // Headwear after the structural placement had succeeded. Structural reflow is now
    // the sole owner of HeadBand geometry.
    internal static class FirstOpenHeadBandLayoutRuntime
    {
        internal static Action RequestFlush;
        internal static bool HasPending => false;
        internal static void Flush() { }
        internal static void Reset() { RequestFlush = null; }
    }

    internal sealed class FirstOpenHeadBandLayoutPatches : IDisposable
    {
        readonly Action<string> logInfo;

        internal FirstOpenHeadBandLayoutPatches(Action<string> logInfo, Action<string> logWarning)
        {
            this.logInfo = logInfo;
        }

        internal bool TryInstall()
        {
            logInfo?.Invoke("B&A&HB legacy first-open HeadBand positioner disabled: structural Gear Panel reflow is the sole placement owner; no competing SlotView.Show position patch installed.");
            return true;
        }

        public void Dispose()
        {
            FirstOpenHeadBandLayoutRuntime.Reset();
        }
    }
}
