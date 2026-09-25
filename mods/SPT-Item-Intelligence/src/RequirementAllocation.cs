using System;

namespace SPTItemIntelligence
{
    public enum RequirementCoverage { NotNeeded, NeedMore, Enough }

    // One allocation policy shared by the immutable index and legacy text adapter.
    // First reserve all FIR-only obligations, then spend unrestricted inventory.
    public sealed class ItemRequirementAllocation
    {
        public ItemRequirementAllocation(int owned, int firOwned, int now, int later, int hideout, int nowFir, int laterFir, int exactOwned = -1, int exactFirOwned = -1, int hideoutFir = 0, int hideoutInstalled = 0, int hideoutCurrentRequired = 0, bool sharedAlternativePool = false,
            int fixedNow = -1, int fixedLater = -1, int fixedHideout = -1, int fixedNowFir = -1, int fixedLaterFir = -1, int fixedHideoutFir = -1)
        {
            Owned = Math.Max(0, owned);
            OwnedFir = Math.Min(Owned, Math.Max(0, firOwned));
            ExactOwned = exactOwned < 0 ? Owned : Math.Max(0, exactOwned);
            ExactOwnedFir = exactFirOwned < 0 ? Math.Min(ExactOwned, OwnedFir) : Math.Min(ExactOwned, Math.Max(0, exactFirOwned));
            NowRequired = Math.Max(0, now);
            LaterRequired = Math.Max(0, later);
            HideoutRequired = Math.Max(0, hideout);
            NowFirRequired = Math.Min(NowRequired, Math.Max(0, nowFir));
            LaterFirRequired = Math.Min(LaterRequired, Math.Max(0, laterFir));
            HideoutFirRequired = Math.Min(HideoutRequired, Math.Max(0, hideoutFir));
            HideoutCurrentRequired = Math.Max(0, hideoutCurrentRequired);
            HideoutInstalled = Math.Min(HideoutCurrentRequired, Math.Max(0, hideoutInstalled));
            HasSharedAlternativePool = sharedAlternativePool;
            FixedNowRequired = fixedNow;
            FixedLaterRequired = fixedLater;
            FixedHideoutRequired = fixedHideout;
            FixedNowFirRequired = fixedNowFir;
            FixedLaterFirRequired = fixedLaterFir;
            FixedHideoutFirRequired = fixedHideoutFir;
            Keep = checked(NowRequired + LaterRequired + HideoutRequired);
            FixedKeep = sharedAlternativePool && fixedNow >= 0 && fixedLater >= 0 && fixedHideout >= 0
                ? Math.Min(Keep, checked(Math.Max(0, fixedNow) + Math.Max(0, fixedLater) + Math.Max(0, fixedHideout))) : Keep;
            if (sharedAlternativePool && fixedNow >= 0 && fixedLater >= 0 && fixedHideout >= 0)
            {
                AllocateSharedAndFixed(fixedNow, fixedLater, fixedHideout,
                    fixedNowFir, fixedLaterFir, fixedHideoutFir,
                    out int nowAllocated, out int laterAllocated, out int hideoutAllocated,
                    out int nowFirAllocated, out int laterFirAllocated, out int hideoutFirAllocated,
                    out int surplus);
                NowAllocated = nowAllocated;
                LaterAllocated = laterAllocated;
                HideoutAllocated = hideoutAllocated;
                NowFirAllocated = nowFirAllocated;
                LaterFirAllocated = laterFirAllocated;
                HideoutFirAllocated = hideoutFirAllocated;
                Surplus = surplus;
                return;
            }
            int fir = OwnedFir;
            int nonFir = Owned - fir;
            NowFirAllocated = Take(ref fir, NowFirRequired);
            LaterFirAllocated = Take(ref fir, LaterFirRequired);
            HideoutFirAllocated = Take(ref fir, HideoutFirRequired);
            NowAllocated = NowFirAllocated + TakeAny(ref nonFir, ref fir, NowRequired - NowFirRequired);
            HideoutAllocated = HideoutFirAllocated + TakeAny(ref nonFir, ref fir, HideoutRequired - HideoutFirRequired);
            LaterAllocated = LaterFirAllocated + TakeAny(ref nonFir, ref fir, LaterRequired - LaterFirRequired);
            Surplus = fir + nonFir;
        }
        public int Owned { get; }
        public int OwnedFir { get; }
        public int ExactOwned { get; }
        public int ExactOwnedFir { get; }
        public int NowRequired { get; }
        public int LaterRequired { get; }
        public int HideoutRequired { get; }
        public int NowFirRequired { get; }
        public int LaterFirRequired { get; }
        public int HideoutFirRequired { get; }
        public int NowFirAllocated { get; }
        public int LaterFirAllocated { get; }
        public int HideoutFirAllocated { get; }
        public int HideoutInstalled { get; }
        public int HideoutCurrentRequired { get; }
        public bool HasSharedAlternativePool { get; }
        public int FixedNowRequired { get; }
        public int FixedLaterRequired { get; }
        public int FixedHideoutRequired { get; }
        public int FixedNowFirRequired { get; }
        public int FixedLaterFirRequired { get; }
        public int FixedHideoutFirRequired { get; }
        public int NowAllocated { get; }
        public int LaterAllocated { get; }
        public int HideoutAllocated { get; }
        public int Keep { get; }
        public int FixedKeep { get; }
        public int AlternativeKeep => Keep - FixedKeep;
        public int Surplus { get; }
        public int KeepOwned => Owned - Surplus;
        public int NowMissing => NowRequired - NowAllocated;
        public int LaterMissing => LaterRequired - LaterAllocated;
        public int HideoutMissing => HideoutRequired - HideoutAllocated;
        public int Missing => Keep - KeepOwned;
        public bool MustKeep => Keep > 0;
        public RequirementCoverage Coverage => Keep == 0 ? RequirementCoverage.NotNeeded : Missing > 0 ? RequirementCoverage.NeedMore : RequirementCoverage.Enough;
        void AllocateSharedAndFixed(int fixedNow, int fixedLater, int fixedHideout,
            int fixedNowFir, int fixedLaterFir, int fixedHideoutFir,
            out int nowAllocated, out int laterAllocated, out int hideoutAllocated,
            out int nowFirAllocated, out int laterFirAllocated, out int hideoutFirAllocated,
            out int surplus)
        {
            fixedNow = Math.Min(NowRequired, Math.Max(0, fixedNow));
            fixedLater = Math.Min(LaterRequired, Math.Max(0, fixedLater));
            fixedHideout = Math.Min(HideoutRequired, Math.Max(0, fixedHideout));
            fixedNowFir = Math.Min(NowFirRequired, Math.Min(fixedNow, Math.Max(0, fixedNowFir)));
            fixedLaterFir = Math.Min(LaterFirRequired, Math.Min(fixedLater, Math.Max(0, fixedLaterFir)));
            fixedHideoutFir = Math.Min(HideoutFirRequired, Math.Min(fixedHideout, Math.Max(0, fixedHideoutFir)));
            int exactFir = ExactOwnedFir;
            int exactNonFir = ExactOwned - exactFir;
            int otherFir = Math.Max(0, OwnedFir - exactFir);
            int otherNonFir = Math.Max(0, Owned - OwnedFir - exactNonFir);

            int nowFixedFir = Take(ref exactFir, fixedNowFir);
            int laterFixedFir = Take(ref exactFir, fixedLaterFir);
            int hideoutFixedFir = Take(ref exactFir, fixedHideoutFir);
            int nowSharedFir = TakeAny(ref otherFir, ref exactFir, NowFirRequired - fixedNowFir);
            int laterSharedFir = TakeAny(ref otherFir, ref exactFir, LaterFirRequired - fixedLaterFir);
            int hideoutSharedFir = TakeAny(ref otherFir, ref exactFir, HideoutFirRequired - fixedHideoutFir);

            int nowFixedAny = TakeAny(ref exactNonFir, ref exactFir, fixedNow - fixedNowFir);
            int hideoutFixedAny = TakeAny(ref exactNonFir, ref exactFir, fixedHideout - fixedHideoutFir);
            int laterFixedAny = TakeAny(ref exactNonFir, ref exactFir, fixedLater - fixedLaterFir);
            int nowSharedAny = TakeSharedAny(ref otherNonFir, ref exactNonFir, ref otherFir, ref exactFir,
                NowRequired - fixedNow - (NowFirRequired - fixedNowFir));
            int hideoutSharedAny = TakeSharedAny(ref otherNonFir, ref exactNonFir, ref otherFir, ref exactFir,
                HideoutRequired - fixedHideout - (HideoutFirRequired - fixedHideoutFir));
            int laterSharedAny = TakeSharedAny(ref otherNonFir, ref exactNonFir, ref otherFir, ref exactFir,
                LaterRequired - fixedLater - (LaterFirRequired - fixedLaterFir));

            nowFirAllocated = nowFixedFir + nowSharedFir;
            laterFirAllocated = laterFixedFir + laterSharedFir;
            hideoutFirAllocated = hideoutFixedFir + hideoutSharedFir;
            nowAllocated = nowFirAllocated + nowFixedAny + nowSharedAny;
            laterAllocated = laterFirAllocated + laterFixedAny + laterSharedAny;
            hideoutAllocated = hideoutFirAllocated + hideoutFixedAny + hideoutSharedAny;
            surplus = exactFir + exactNonFir + otherFir + otherNonFir;
        }
        static int TakeSharedAny(ref int otherNonFir, ref int exactNonFir, ref int otherFir, ref int exactFir, int demand)
        {
            int used = Take(ref otherNonFir, demand);
            used += Take(ref exactNonFir, demand - used);
            used += Take(ref otherFir, demand - used);
            used += Take(ref exactFir, demand - used);
            return used;
        }
        static int Take(ref int stock, int demand) { int used = Math.Min(stock, demand); stock -= used; return used; }
        static int TakeAny(ref int nonFir, ref int fir, int demand) { int used = Take(ref nonFir, demand); return used + Take(ref fir, demand - used); }
    }
}
