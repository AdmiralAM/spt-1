using System;

namespace SPTItemIntelligence
{
    public enum RequirementCoverage { NotNeeded, NeedMore, Enough }

    // One allocation policy shared by the immutable index and legacy text adapter.
    // First reserve all FIR-only obligations, then spend unrestricted inventory.
    public sealed class ItemRequirementAllocation
    {
        public ItemRequirementAllocation(int owned, int firOwned, int now, int later, int hideout, int nowFir, int laterFir)
        {
            Owned = Math.Max(0, owned);
            OwnedFir = Math.Min(Owned, Math.Max(0, firOwned));
            NowRequired = Math.Max(0, now);
            LaterRequired = Math.Max(0, later);
            HideoutRequired = Math.Max(0, hideout);
            NowFirRequired = Math.Min(NowRequired, Math.Max(0, nowFir));
            LaterFirRequired = Math.Min(LaterRequired, Math.Max(0, laterFir));
            Keep = checked(NowRequired + LaterRequired + HideoutRequired);
            int fir = OwnedFir;
            int nonFir = Owned - fir;
            NowFirAllocated = Take(ref fir, NowFirRequired);
            LaterFirAllocated = Take(ref fir, LaterFirRequired);
            NowAllocated = NowFirAllocated + TakeAny(ref nonFir, ref fir, NowRequired - NowFirRequired);
            HideoutAllocated = TakeAny(ref nonFir, ref fir, HideoutRequired);
            LaterAllocated = LaterFirAllocated + TakeAny(ref nonFir, ref fir, LaterRequired - LaterFirRequired);
            Surplus = fir + nonFir;
        }
        public int Owned { get; }
        public int OwnedFir { get; }
        public int NowRequired { get; }
        public int LaterRequired { get; }
        public int HideoutRequired { get; }
        public int NowFirRequired { get; }
        public int LaterFirRequired { get; }
        public int NowFirAllocated { get; }
        public int LaterFirAllocated { get; }
        public int NowAllocated { get; }
        public int LaterAllocated { get; }
        public int HideoutAllocated { get; }
        public int Keep { get; }
        public int Surplus { get; }
        public int KeepOwned => Owned - Surplus;
        public int NowMissing => NowRequired - NowAllocated;
        public int LaterMissing => LaterRequired - LaterAllocated;
        public int HideoutMissing => HideoutRequired - HideoutAllocated;
        public int Missing => Keep - KeepOwned;
        public bool MustKeep => Keep > 0;
        public RequirementCoverage Coverage => Keep == 0 ? RequirementCoverage.NotNeeded : Missing > 0 ? RequirementCoverage.NeedMore : RequirementCoverage.Enough;
        static int Take(ref int stock, int demand) { int used = Math.Min(stock, demand); stock -= used; return used; }
        static int TakeAny(ref int nonFir, ref int fir, int demand) { int used = Take(ref nonFir, demand); return used + Take(ref fir, demand - used); }
    }
}
