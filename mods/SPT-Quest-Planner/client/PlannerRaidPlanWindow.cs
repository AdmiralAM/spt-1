using System;
using BepInEx.Configuration;
using UnityEngine;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidPlanWindow
    {
        private const int WindowId = 0x51504C4E;
        private static readonly Color WindowBackgroundColor = new Color(0.075f, 0.08f, 0.085f, 1f);
        private readonly PlannerRaidPlanPresentationController presentation;
        private readonly Func<long> revisionProvider;
        private readonly PlannerUiRaycastBlocker inputBlocker = new PlannerUiRaycastBlocker();
        private Rect windowRect = new Rect(140f, 80f, 980f, 740f);
        private Vector2 locationScroll;
        private Vector2 detailScroll;
        private bool visible;
        private GUIStyle opaqueWindowStyle;
        private Texture2D opaqueWindowTexture;

        public PlannerRaidPlanWindow(PlannerRaidPlanPresentationController presentation, Func<long> revisionProvider)
        {
            this.presentation = presentation ?? throw new ArgumentNullException("presentation");
            this.revisionProvider = revisionProvider ?? throw new ArgumentNullException("revisionProvider");
        }

        public bool Visible { get { return visible; } }

        public void Toggle()
        {
            if (visible) Hide();
            else visible = true;
        }

        public void Hide()
        {
            visible = false;
            inputBlocker.Release();
        }

        public void Draw()
        {
            if (!visible) return;
            inputBlocker.Ensure();
            EnsureOpaqueWindowStyle();

            Color previousColor = GUI.color;
            Color previousBackground = GUI.backgroundColor;
            GUI.color = Color.white;
            GUI.backgroundColor = Color.white;
            windowRect = GUI.ModalWindow(WindowId, windowRect, DrawWindow, "Quest planner MOD SPT", opaqueWindowStyle);
            GUI.backgroundColor = previousBackground;
            GUI.color = previousColor;

            Event current = Event.current;
            if (current != null && IsPointerEvent(current.type)) current.Use();
        }

        private void EnsureOpaqueWindowStyle()
        {
            if (opaqueWindowStyle != null && opaqueWindowTexture != null) return;
            opaqueWindowTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            opaqueWindowTexture.name = "QuestPlannerOpaqueWindow";
            opaqueWindowTexture.hideFlags = HideFlags.HideAndDontSave;
            opaqueWindowTexture.SetPixel(0, 0, WindowBackgroundColor);
            opaqueWindowTexture.Apply(false, true);
            opaqueWindowStyle = new GUIStyle(GUI.skin.window);
            opaqueWindowStyle.normal.background = opaqueWindowTexture;
            opaqueWindowStyle.onNormal.background = opaqueWindowTexture;
            opaqueWindowStyle.hover.background = opaqueWindowTexture;
            opaqueWindowStyle.onHover.background = opaqueWindowTexture;
            opaqueWindowStyle.active.background = opaqueWindowTexture;
            opaqueWindowStyle.onActive.background = opaqueWindowTexture;
            opaqueWindowStyle.focused.background = opaqueWindowTexture;
            opaqueWindowStyle.onFocused.background = opaqueWindowTexture;
        }

        private static bool IsPointerEvent(EventType type)
        {
            return type == EventType.MouseDown || type == EventType.MouseUp || type == EventType.MouseDrag ||
                   type == EventType.ScrollWheel || type == EventType.ContextClick;
        }

        private void DrawWindow(int id)
        {
            long revision = revisionProvider();
            PlannerRaidPlanViewModel viewModel = presentation.GetViewModel(revision, 12);
            PlannerRaidPlanUiState uiState = presentation.UiState;
            PlannerRaidPlanCard selected = uiState.ResolveSelection(viewModel);
            PlannerRaidPlanCard active = uiState.ResolveActivePlan(viewModel);

            GUILayout.BeginVertical();
            DrawHeader(viewModel);
            DrawActivePlanBanner(active, uiState);
            DrawRecommendedRaid(viewModel, uiState, active);
            DrawRecommendations(uiState);
            GUILayout.Space(4f);
            DrawControls(uiState);
            GUILayout.BeginHorizontal();
            DrawLocationList(viewModel, uiState);
            DrawDetails(selected, active, uiState);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, windowRect.width - 140f, 24f));
        }

        private void DrawHeader(PlannerRaidPlanViewModel viewModel)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Raid plans: " + viewModel.LocationCount + "   Ready: " + viewModel.ReadyLocationCount, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Refresh data", GUILayout.Width(100f)))
            {
                Plugin instance = Plugin.Instance;
                if (instance != null) instance.RequestStateRefresh("ui-manual");
            }
            if (GUILayout.Button("X", GUILayout.Width(30f))) Hide();
            GUILayout.EndHorizontal();
        }

        private static void DrawActivePlanBanner(PlannerRaidPlanCard active, PlannerRaidPlanUiState uiState)
        {
            if (active == null) return;
            GUILayout.BeginHorizontal("box");
            GUILayout.Label("ACTIVE RAID PLAN: " + PlannerDisplayNames.Location(active.LocationId) +
                            "  |  " + active.QuestCount + " quest(s) / " + active.ObjectiveCount + " objective(s)" +
                            (active.PreparationReady ? "  |  READY" : "  |  PREPARATION NEEDED"), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Clear", GUILayout.Width(60f))) uiState.ClearActivePlan();
            GUILayout.EndHorizontal();
        }

        private static void DrawRecommendedRaid(PlannerRaidPlanViewModel viewModel, PlannerRaidPlanUiState uiState, PlannerRaidPlanCard active)
        {
            PlannerRaidPlanCard recommended = viewModel.TopRecommendation;
            if (recommended == null) return;

            GUILayout.BeginHorizontal("box");
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label("RECOMMENDED RAID — " + PlannerDisplayNames.Location(recommended.LocationId));
            GUILayout.Label(
                recommended.QuestCount + " quest(s) / " + recommended.ObjectiveCount + " proven objective(s)  |  " +
                (recommended.PreparationReady ? "ready to go" : "missing " + recommended.MissingBringTemplateCount + " preparation item type(s)"));
            GUILayout.EndVertical();

            bool alreadyActive = active != null && string.Equals(active.LocationId, recommended.LocationId, StringComparison.OrdinalIgnoreCase);
            GUI.enabled = !alreadyActive;
            if (GUILayout.Button(alreadyActive ? "ACTIVE" : "PLAN THIS RAID", GUILayout.Width(135f), GUILayout.Height(38f)))
                uiState.ActivateLocation(recommended.LocationId);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private static void DrawRecommendations(PlannerRaidPlanUiState uiState)
        {
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            GUILayout.Label("WHAT TO DO NEXT", GUILayout.ExpandWidth(true));
            if (uiState.HasProgressionTarget && GUILayout.Button("Clear target", GUILayout.Width(85f))) uiState.ClearProgressionTarget();
            GUILayout.EndHorizontal();

            try
            {
                PlannerRecommendationSnapshot snapshot = Plugin.GetRecommendations(3);
                if (snapshot.Recommendations.Count == 0)
                {
                    GUILayout.Label("No actionable Active/Available quest recommendations.");
                }
                else
                {
                    PlannerRecommendationViewModel selected = null;
                    for (int i = 0; i < snapshot.Recommendations.Count; i++)
                    {
                        PlannerRecommendationViewModel value = snapshot.Recommendations[i];
                        if (string.Equals(value.QuestId, uiState.ProgressionTargetQuestId, StringComparison.Ordinal)) selected = value;

                        string burden = value.FullyOwned
                            ? "items ready"
                            : "missing " + FormatNumber(value.TotalOutstanding) +
                              (value.FirOutstanding > 0d ? " (FIR " + FormatNumber(value.FirOutstanding) + ")" : string.Empty);
                        string label = "#" + value.Rank + "  " + value.QuestName +
                                       "   | blockers " + value.ImmediateBlockerCount +
                                       " | " + burden +
                                       " | unlocks " + value.ImmediateUnlockCount;
                        bool isTarget = string.Equals(value.QuestId, uiState.ProgressionTargetQuestId, StringComparison.Ordinal);
                        if (GUILayout.Toggle(isTarget, label, "Button", GUILayout.MinHeight(26f)) && !isTarget)
                            uiState.SelectProgressionTarget(value.QuestId);
                    }

                    if (selected == null && uiState.HasProgressionTarget)
                    {
                        for (int i = 0; i < snapshot.Recommendations.Count; i++)
                        {
                            if (string.Equals(snapshot.Recommendations[i].QuestId, uiState.ProgressionTargetQuestId, StringComparison.Ordinal))
                            {
                                selected = snapshot.Recommendations[i];
                                break;
                            }
                        }
                    }
                    DrawProgressionTarget(selected);
                }
            }
            catch (Exception ex)
            {
                GUILayout.Label("Recommendations unavailable: " + ex.GetBaseException().Message);
            }
            GUILayout.EndVertical();
        }

        private static void DrawProgressionTarget(PlannerRecommendationViewModel target)
        {
            if (target == null) return;
            GUILayout.Space(4f);
            GUILayout.Label("PROGRESSION TARGET — " + target.QuestName);
            GUILayout.Label("Path: " + target.PathQuestCount + " incomplete quest(s)  |  blockers: " + target.ImmediateBlockerCount +
                            "  |  missing items: " + FormatNumber(target.TotalOutstanding) +
                            (target.FirOutstanding > 0d ? " (FIR " + FormatNumber(target.FirOutstanding) + ")" : string.Empty) +
                            "  |  immediate unlocks: " + target.ImmediateUnlockCount);
            if (target.BlockerQuestNames.Count > 0)
                GUILayout.Label("Blockers: " + string.Join(" → ", target.BlockerQuestNames));
            if (target.ImmediateUnlockQuestNames.Count > 0)
                GUILayout.Label("Unlocks next: " + string.Join(", ", target.ImmediateUnlockQuestNames));
            if (target.Reasons.Count > 0)
                GUILayout.Label("Why: " + string.Join("; ", target.Reasons));
        }

        private static void DrawControls(PlannerRaidPlanUiState uiState)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Raid ranking:", GUILayout.Width(78f));
            bool readyFirst = uiState.RankingMode == PlannerRaidPlanRankingMode.ReadyFirst;
            if (GUILayout.Toggle(readyFirst, "Ready first", "Button", GUILayout.Width(100f)) && !readyFirst)
                uiState.SetRankingMode(PlannerRaidPlanRankingMode.ReadyFirst);
            bool densityFirst = uiState.RankingMode == PlannerRaidPlanRankingMode.QuestDensityFirst;
            if (GUILayout.Toggle(densityFirst, "Quest density", "Button", GUILayout.Width(110f)) && !densityFirst)
                uiState.SetRankingMode(PlannerRaidPlanRankingMode.QuestDensityFirst);
            GUILayout.Space(12f);
            bool includeAvailable = GUILayout.Toggle(uiState.IncludeAvailable, "Include available quests", GUILayout.Width(160f));
            if (includeAvailable != uiState.IncludeAvailable) uiState.SetIncludeAvailable(includeAvailable);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawLocationList(PlannerRaidPlanViewModel viewModel, PlannerRaidPlanUiState uiState)
        {
            GUILayout.BeginVertical(GUILayout.Width(280f));
            GUILayout.Label("RAID OPTIONS");
            locationScroll = GUILayout.BeginScrollView(locationScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < viewModel.Cards.Count; i++)
            {
                PlannerRaidPlanCard card = viewModel.Cards[i];
                bool selected = string.Equals(card.LocationId, uiState.SelectedLocationId, StringComparison.OrdinalIgnoreCase);
                bool active = string.Equals(card.LocationId, uiState.ActiveLocationId, StringComparison.OrdinalIgnoreCase);
                string label = (active ? "ACTIVE  " : "#" + card.Rank + "  ") + PlannerDisplayNames.Location(card.LocationId) + "\n" +
                               card.QuestCount + " quest(s) / " + card.ObjectiveCount + " objective(s)" +
                               (card.PreparationReady ? "  [READY]" : "  [MISSING " + card.MissingBringTemplateCount + "]");
                if (GUILayout.Toggle(selected, label, "Button", GUILayout.MinHeight(48f)) && !selected)
                    uiState.SelectLocation(card.LocationId);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawDetails(PlannerRaidPlanCard card, PlannerRaidPlanCard active, PlannerRaidPlanUiState uiState)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (card == null)
            {
                GUILayout.Label("No proven raid opportunities for the current Active quest state.");
                GUILayout.EndVertical();
                return;
            }

            bool isActive = active != null && string.Equals(active.LocationId, card.LocationId, StringComparison.OrdinalIgnoreCase);
            GUILayout.BeginHorizontal();
            GUILayout.Label(PlannerDisplayNames.Location(card.LocationId) + (isActive ? " — ACTIVE RAID PLAN" : " — raid preview"), GUILayout.ExpandWidth(true));
            if (!isActive && GUILayout.Button("SET ACTIVE PLAN", GUILayout.Width(125f))) uiState.ActivateLocation(card.LocationId);
            if (isActive && GUILayout.Button("CLEAR ACTIVE", GUILayout.Width(100f))) uiState.ClearActivePlan();
            GUILayout.EndHorizontal();

            GUILayout.Label(card.QuestCount + " relevant quest(s)   " + card.ObjectiveCount + " proven objective(s)");
            GUILayout.Label(card.PreparationReady ? "Preparation: READY" : "Preparation: missing " + card.MissingBringTemplateCount + " required item type(s)");
            if (card.KnownRemainingWork > 0d) GUILayout.Label("Known remaining counter work: " + FormatNumber(card.KnownRemainingWork));

            PlannerClientCache cache = Plugin.Cache;
            PlannerTopologyIndex topology = cache == null ? null : cache.TopologyIndex;
            PlannerLocaleIndex locale = cache == null ? null : cache.LocaleIndex;
            detailScroll = GUILayout.BeginScrollView(detailScroll, GUILayout.ExpandHeight(true));

            GUILayout.Space(6f);
            GUILayout.Label("BEFORE RAID");
            if (card.BringNeeds.Count == 0) GUILayout.Label("✓ No proven bring-items required for this plan.");
            else
            {
                for (int i = 0; i < card.BringNeeds.Count; i++)
                {
                    PlannerRaidBringNeed need = card.BringNeeds[i];
                    string itemLabel = locale == null ? need.TemplateId : locale.ItemName(need.TemplateId);
                    string marker = need.Missing <= 0d ? "✓ " : "⚠ ";
                    GUILayout.Label(marker + itemLabel + "  need " + FormatNumber(need.Required) + " / owned " + FormatNumber(need.Owned) + " / missing " + FormatNumber(need.Missing));
                }
            }

            GUILayout.Space(10f);
            GUILayout.Label("IN RAID CHECKLIST");
            for (int i = 0; i < card.Objectives.Count; i++)
            {
                PlannerRaidObjective objective = card.Objectives[i];
                string progress = objective.HasProgress ? "  " + FormatNumber(objective.CurrentValue ?? 0d) + "/" + FormatNumber(objective.RequiredValue ?? 0d) + " (remain " + FormatNumber(objective.RemainingValue ?? 0d) + ")" : string.Empty;
                string targets = FormatTargets(objective, locale);
                string questLabel = PlannerQuestLabels.Resolve(topology, locale, objective.QuestId);
                GUILayout.Label("□ " + PlannerDisplayNames.Objective(objective.Kind) + " — " + questLabel + progress + targets);
            }

            if (card.ObjectiveCount > card.Objectives.Count)
                GUILayout.Label("… " + (card.ObjectiveCount - card.Objectives.Count) + " more objective(s) hidden from this compact view.");

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private static string FormatTargets(PlannerRaidObjective objective, PlannerLocaleIndex locale)
        {
            if (objective.Targets.Count == 0) return string.Empty;
            string[] labels = new string[objective.Targets.Count];
            for (int i = 0; i < objective.Targets.Count; i++) labels[i] = locale == null ? objective.Targets[i] : locale.ItemName(objective.Targets[i]);
            return "  [" + string.Join(", ", labels) + "]";
        }

        private static string FormatNumber(double value)
        {
            double rounded = Math.Round(value);
            return Math.Abs(value - rounded) < 0.000001d ? rounded.ToString("0") : value.ToString("0.##");
        }
    }
}
