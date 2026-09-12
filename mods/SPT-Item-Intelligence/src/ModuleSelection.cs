namespace SPTItemIntelligence
{
    public sealed class ModuleSelection
    {
        public static readonly ModuleSelection Default = new ModuleSelection();
        public ModuleSelection(bool markers = true, bool tooltips = true, bool quests = true, bool futureQuests = true,
            bool hideout = true, bool value = true, bool relevance = true, bool backgrounds = false)
        { Markers = markers; Tooltips = tooltips; Quests = quests; FutureQuests = futureQuests; Hideout = hideout; Value = value; Relevance = relevance; Backgrounds = backgrounds; }
        public bool Markers { get; }
        public bool Tooltips { get; }
        public bool Quests { get; }
        public bool FutureQuests { get; }
        public bool Hideout { get; }
        public bool Value { get; }
        public bool Relevance { get; }
        public bool Backgrounds { get; }
        public bool Requirements => (Markers || Tooltips) && (Quests || FutureQuests || Hideout);
        public bool Prices => (Tooltips && Value) || Backgrounds;
        public bool CraftBarter => Tooltips && Relevance;
        public bool AnyConsumer => Requirements || Prices || CraftBarter;
        public bool TrackViews => AnyConsumer && (Markers || Tooltips || Backgrounds);
        public int DataKey => (Requirements && Quests ? 1 : 0) | (Requirements && FutureQuests ? 2 : 0) |
            (Requirements && Hideout ? 4 : 0) | (Prices ? 8 : 0) | (CraftBarter ? 16 : 0);
        public int Key => (Markers ? 1 : 0) | (Tooltips ? 2 : 0) | (Quests ? 4 : 0) | (FutureQuests ? 8 : 0) |
            (Hideout ? 16 : 0) | (Value ? 32 : 0) | (Relevance ? 64 : 0) | (Backgrounds ? 128 : 0);
        public RequirementIndexOptions RequirementOptions => new RequirementIndexOptions
        { IncludeCurrentQuests = Requirements && Quests, IncludeFutureQuests = Requirements && FutureQuests, IncludeHideout = Requirements && Hideout };
    }
}
