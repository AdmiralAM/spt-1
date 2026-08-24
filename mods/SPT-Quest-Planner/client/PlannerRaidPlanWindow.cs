using System;
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
        private Vector2 progressionScroll;
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
            else
            {
                visible = true;
                PlannerRaidPlanUiState state = presentation.UiState;
                if (state != null && state.HasActivePlan) state.SelectLocation(state.ActiveLocationId);
            }
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
            windowRect = GUI.ModalWindow(WindowId, windowRect, DrawWindow, "Quest Planner", opaqueWindowStyle);
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
            PlannerRaidPlanViewModel viewModel = presentation.GetViewModel(revision, 16);
            PlannerRaidPlanUiState uiState = presentation.UiState;

            GUILayout.BeginVertical();
            DrawHeader(viewModel, uiState);
            DrawWorkspaceTabs(uiState);
            GUILayout.Space(6f);
            if (uiState.WorkspaceMode == PlannerWorkspaceMode.Progression) DrawProgressionWorkspace(uiState);
            else DrawRaidWorkspace(viewModel, uiState);
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, windowRect.width - 140f, 24f));
        }

        private void DrawHeader(PlannerRaidPlanViewModel viewModel, PlannerRaidPlanUiState uiState)
        {
            GUILayout.BeginHorizontal();
            PlannerRaidPlanCard focusedRaid = FocusedRaid(viewModel, uiState);
            string status = uiState.HasActivePlan
                ? "Active plan: " + PlannerDisplayNames.Location(uiState.ActiveLocationId)
                : focusedRaid != null
                    ? "Focus raid: " + PlannerDisplayNames.Location(focusedRaid.LocationId)
                    : viewModel.TopRecommendation == null
                        ? "No raid plan available"
                        : "Best raid: " + PlannerDisplayNames.Location(viewModel.TopRecommendation.LocationId);
            GUILayout.Label(status, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Refresh", GUILayout.Width(75f)))
            {
                Plugin instance = Plugin.Instance;
                if (instance != null) instance.RequestStateRefresh("ui-manual");
            }
            if (GUILayout.Button("X", GUILayout.Width(30f))) Hide();
            GUILayout.EndHorizontal();
        }

        private static void DrawWorkspaceTabs(PlannerRaidPlanUiState uiState)
        {
            GUILayout.BeginHorizontal();
            bool raid = uiState.WorkspaceMode == PlannerWorkspaceMode.RaidPlanner;
            if (GUILayout.Toggle(raid, "PLAN A RAID", "Button", GUILayout.Height(32f)) && !raid)
                uiState.SetWorkspaceMode(PlannerWorkspaceMode.RaidPlanner);
            bool progression = uiState.WorkspaceMode == PlannerWorkspaceMode.Progression;
            if (GUILayout.Toggle(progression, "WHAT TO DO NEXT", "Button", GUILayout.Height(32f)) && !progression)
                uiState.SetWorkspaceMode(PlannerWorkspaceMode.Progression);
            GUILayout.EndHorizontal();
        }

        private void DrawRaidWorkspace(PlannerRaidPlanViewModel viewModel, PlannerRaidPlanUiState uiState)
        {
            PlannerRaidPlanCard selected = uiState.ResolveSelection(viewModel);
            PlannerRaidPlanCard active = uiState.ResolveActivePlan(viewModel);

            if (active != null) DrawActivePlanBanner(active, uiState);
            else DrawRecommendedRaid(viewModel, uiState);

            DrawRaidControls(uiState);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            DrawLocationList(viewModel, uiState);
            DrawDetails(selected, active, uiState);
            GUILayout.EndHorizontal();
        }

        private static void DrawActivePlanBanner(PlannerRaidPlanCard active, PlannerRaidPlanUiState uiState)
        {
            GUILayout.BeginHorizontal("box");
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label("CURRENT RAID PLAN — " + PlannerDisplayNames.Location(active.LocationId));
            GUILayout.Label(PlanBenefit(active));
            GUILayout.Label("Preparation: " + active.PreparationLabel + ".");
            GUILayout.EndVertical();
            if (GUILayout.Button("Choose another raid", GUILayout.Width(135f), GUILayout.Height(50f))) uiState.ClearActivePlan();
            GUILayout.EndHorizontal();
        }

        private static void DrawRecommendedRaid(PlannerRaidPlanViewModel viewModel, PlannerRaidPlanUiState uiState)
        {
            PlannerRaidPlanCard focused = FocusedRaid(viewModel, uiState);
            PlannerRaidPlanCard recommended = focused ?? viewModel.TopRecommendation;
            if (recommended == null)
            {
                GUILayout.BeginVertical("box");
                GUILayout.Label("No actionable raid plan can be proven from your current active quests.");
                GUILayout.Label("Try including quests you can accept, or check WHAT TO DO NEXT.");
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginHorizontal("box");
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label((focused != null ? "BEST RAID FOR YOUR FOCUS — " : "BEST RAID RIGHT NOW — ") + PlannerDisplayNames.Location(recommended.LocationId));
            GUILayout.Label(PlanBenefit(recommended));
            GUILayout.Label(focused != null
                ? "Why: this is the highest-ranked raid that advances your focused quest."
                : "Why this is #1: " + recommended.RankReason);
            GUILayout.Label("Preparation: " + recommended.PreparationLabel + ".");
            GUILayout.EndVertical();
            if (GUILayout.Button("USE THIS PLAN", GUILayout.Width(130f), GUILayout.Height(66f))) uiState.ActivateLocation(recommended.LocationId);
            GUILayout.EndHorizontal();
        }

        private static void DrawRaidControls(PlannerRaidPlanUiState uiState)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Prefer:", GUILayout.Width(45f));
            bool readyFirst = uiState.RankingMode == PlannerRaidPlanRankingMode.ReadyFirst;
            if (GUILayout.Toggle(readyFirst, "Ready to go", "Button", GUILayout.Width(105f)) && !readyFirst)
                uiState.SetRankingMode(PlannerRaidPlanRankingMode.ReadyFirst);
            bool densityFirst = uiState.RankingMode == PlannerRaidPlanRankingMode.QuestDensityFirst;
            if (GUILayout.Toggle(densityFirst, "Most quest progress", "Button", GUILayout.Width(135f)) && !densityFirst)
                uiState.SetRankingMode(PlannerRaidPlanRankingMode.QuestDensityFirst);
            GUILayout.Space(12f);
            bool includeAvailable = GUILayout.Toggle(uiState.IncludeAvailable, "Include quests I can accept", GUILayout.Width(185f));
            if (includeAvailable != uiState.IncludeAvailable) uiState.SetIncludeAvailable(includeAvailable);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawLocationList(PlannerRaidPlanViewModel viewModel, PlannerRaidPlanUiState uiState)
        {
            GUILayout.BeginVertical(GUILayout.Width(305f));
            GUILayout.Label(uiState.HasActivePlan ? "OTHER RAID OPTIONS" : "RAID OPTIONS");
            locationScroll = GUILayout.BeginScrollView(locationScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < viewModel.Cards.Count; i++)
            {
                PlannerRaidPlanCard card = viewModel.Cards[i];
                bool selected = string.Equals(card.LocationId, uiState.SelectedLocationId, StringComparison.OrdinalIgnoreCase);
                bool active = string.Equals(card.LocationId, uiState.ActiveLocationId, StringComparison.OrdinalIgnoreCase);
                bool focus = uiState.HasProgressionTarget && card.SupportsQuest(uiState.ProgressionTargetQuestId);
                string prefix = active ? "CURRENT  " : focus ? "FOCUS  #" + card.Rank + "  " : "#" + card.Rank + "  ";
                string label = prefix + PlannerDisplayNames.Location(card.LocationId) + "\n" +
                               card.ActionSummary + "  •  " + card.PreparationLabel;
                if (GUILayout.Toggle(selected, label, "Button", GUILayout.MinHeight(54f)) && !selected)
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
                GUILayout.Label("Nothing actionable is mapped to a raid right now.");
                GUILayout.EndVertical();
                return;
            }

            bool isActive = active != null && string.Equals(active.LocationId, card.LocationId, StringComparison.OrdinalIgnoreCase);
            bool advancesFocus = uiState.HasProgressionTarget && card.SupportsQuest(uiState.ProgressionTargetQuestId);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(PlannerDisplayNames.Location(card.LocationId) + (isActive ? " — CURRENT PLAN" : advancesFocus ? " — advances your focus" : " — preview"));
            GUILayout.Label(PlanBenefit(card));
            GUILayout.Label("Preparation: " + card.PreparationLabel + ".");
            GUILayout.EndVertical();
            if (!isActive && GUILayout.Button(uiState.HasActivePlan ? "SWITCH TO THIS RAID" : "USE THIS PLAN", GUILayout.Width(145f), GUILayout.Height(50f)))
                uiState.ActivateLocation(card.LocationId);
            GUILayout.EndHorizontal();

            PlannerClientCache cache = Plugin.Cache;
            PlannerTopologyIndex topology = cache == null ? null : cache.TopologyIndex;
            PlannerLocaleIndex locale = cache == null ? null : cache.LocaleIndex;
            detailScroll = GUILayout.BeginScrollView(detailScroll, GUILayout.ExpandHeight(true));

            GUILayout.Space(6f);
            GUILayout.Label("BEFORE YOU GO");
            DrawPreparation(card, locale);

            GUILayout.Space(12f);
            GUILayout.Label("DO THIS IN RAID");
            if (card.Objectives.Count == 0) GUILayout.Label("No proven in-raid task remains for this option.");
            else
            {
                for (int i = 0; i < card.Objectives.Count; i++)
                {
                    PlannerRaidObjective objective = card.Objectives[i];
                    string progress = ObjectiveProgress(objective);
                    string questLabel = PlannerQuestLabels.Resolve(topology, locale, objective.QuestId);
                    string focusMarker = uiState.HasProgressionTarget && string.Equals(objective.QuestId, uiState.ProgressionTargetQuestId, StringComparison.Ordinal) ? " [FOCUS]" : string.Empty;
                    GUILayout.Label("□ " + PlannerDisplayNames.ObjectiveAction(objective, locale) + progress + focusMarker);
                    GUILayout.Label("    Quest: " + questLabel);
                }
            }
            if (card.ObjectiveCount > card.Objectives.Count)
                GUILayout.Label("… plus " + (card.ObjectiveCount - card.Objectives.Count) + " additional mapped task(s).");

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private static void DrawPreparation(PlannerRaidPlanCard card, PlannerLocaleIndex locale)
        {
            if (card.PreparationReady)
            {
                GUILayout.Label("✓ Nothing proven by the planner is missing before this raid.");
                return;
            }

            for (int i = 0; i < card.BringNeeds.Count; i++)
            {
                PlannerRaidBringNeed need = card.BringNeeds[i];
                string itemLabel = PlannerDisplayNames.Target(need.TemplateId, locale);
                if (string.IsNullOrWhiteSpace(itemLabel)) itemLabel = "required item";
                if (need.Missing <= 0d)
                    GUILayout.Label("✓ " + itemLabel + " — enough owned (" + FormatNumber(need.Owned) + "/" + FormatNumber(need.Required) + ")");
                else
                    GUILayout.Label("⚠ Get " + FormatNumber(need.Missing) + " more " + itemLabel);
            }
            if (card.UnresolvedPreparationCount > 0)
                GUILayout.Label("⚠ Check " + card.UnresolvedPreparationCount + " ambiguous requirement(s) manually.");
        }

        private void DrawProgressionWorkspace(PlannerRaidPlanUiState uiState)
        {
            try
            {
                PlannerRecommendationSnapshot snapshot = Plugin.GetRecommendations(32);
                if (snapshot.Recommendations.Count == 0)
                {
                    GUILayout.BeginVertical("box");
                    GUILayout.Label("No active or immediately available quest can be recommended right now.");
                    GUILayout.EndVertical();
                    return;
                }

                PlannerRecommendationViewModel selected = ResolveProgressionTarget(snapshot, uiState);
                PlannerRecommendationViewModel top = snapshot.Recommendations[0];
                if (selected == null)
                {
                    GUILayout.BeginHorizontal("box");
                    GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                    GUILayout.Label("BEST NEXT QUEST — " + top.QuestName);
                    GUILayout.Label(top.StateLabel + "  •  " + top.ActionSummary);
                    GUILayout.Label(ProgressionBenefit(top));
                    GUILayout.EndVertical();
                    if (GUILayout.Button("FOCUS THIS QUEST", GUILayout.Width(135f), GUILayout.Height(54f))) uiState.SelectProgressionTarget(top.QuestId);
                    GUILayout.EndHorizontal();
                }
                else DrawProgressionTarget(selected, uiState);

                GUILayout.Space(8f);
                GUILayout.Label("OTHER GOOD NEXT STEPS");
                progressionScroll = GUILayout.BeginScrollView(progressionScroll, GUILayout.ExpandHeight(true));
                int visibleCount = Math.Min(10, snapshot.Recommendations.Count);
                for (int i = 0; i < visibleCount; i++)
                {
                    PlannerRecommendationViewModel value = snapshot.Recommendations[i];
                    bool isTarget = string.Equals(value.QuestId, uiState.ProgressionTargetQuestId, StringComparison.Ordinal);
                    string label = (isTarget ? "FOCUS  " : "#" + value.Rank + "  ") + value.QuestName + "\n" +
                                   value.StateLabel + "  •  " + value.ActionSummary + "  •  " + ProgressionBenefit(value);
                    if (GUILayout.Toggle(isTarget, label, "Button", GUILayout.MinHeight(48f)) && !isTarget)
                        uiState.SelectProgressionTarget(value.QuestId);
                }
                GUILayout.EndScrollView();
            }
            catch (Exception ex)
            {
                GUILayout.BeginVertical("box");
                GUILayout.Label("Quest recommendations are temporarily unavailable.");
                GUILayout.Label(ex.GetBaseException().Message);
                GUILayout.EndVertical();
            }
        }

        private static PlannerRecommendationViewModel ResolveProgressionTarget(PlannerRecommendationSnapshot snapshot, PlannerRaidPlanUiState uiState)
        {
            if (!uiState.HasProgressionTarget) return null;
            for (int i = 0; i < snapshot.Recommendations.Count; i++)
            {
                PlannerRecommendationViewModel value = snapshot.Recommendations[i];
                if (string.Equals(value.QuestId, uiState.ProgressionTargetQuestId, StringComparison.Ordinal)) return value;
            }
            uiState.ClearProgressionTarget();
            return null;
        }

        private static void DrawProgressionTarget(PlannerRecommendationViewModel target, PlannerRaidPlanUiState uiState)
        {
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label("FOCUSED QUEST — " + target.QuestName);
            GUILayout.Label(target.StateLabel + "  •  " + target.ActionSummary);
            GUILayout.Label(ProgressionBenefit(target));
            GUILayout.EndVertical();
            if (GUILayout.Button("Clear focus", GUILayout.Width(85f), GUILayout.Height(42f))) uiState.ClearProgressionTarget();
            GUILayout.EndHorizontal();

            PlannerRaidPlanCard focusRaid = Plugin.GetRaidForProgressionTarget();
            if (focusRaid != null)
            {
                GUILayout.Space(5f);
                GUILayout.BeginHorizontal();
                GUILayout.Label("RAID FOR THIS QUEST: " + PlannerDisplayNames.Location(focusRaid.LocationId) + " • " + focusRaid.ActionSummary, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("SHOW RAID", GUILayout.Width(95f))) uiState.SetWorkspaceMode(PlannerWorkspaceMode.RaidPlanner);
                GUILayout.EndHorizontal();
            }

            if (target.BlockerQuestNames.Count > 0)
            {
                GUILayout.Space(5f);
                GUILayout.Label("DO THESE FIRST");
                for (int i = 0; i < target.BlockerQuestNames.Count; i++) GUILayout.Label("□ " + target.BlockerQuestNames[i]);
            }
            if (!target.FullyOwned)
            {
                GUILayout.Space(5f);
                GUILayout.Label("ITEMS STILL NEEDED");
                GUILayout.Label("Need " + FormatNumber(target.TotalOutstanding) + " item(s) across this path" +
                                (target.FirOutstanding > 0d ? ", including " + FormatNumber(target.FirOutstanding) + " FIR" : string.Empty) + ".");
            }
            if (target.ImmediateUnlockQuestNames.Count > 0)
            {
                GUILayout.Space(5f);
                GUILayout.Label("WHAT THIS OPENS");
                GUILayout.Label(string.Join(", ", target.ImmediateUnlockQuestNames));
            }
            GUILayout.EndVertical();
        }

        private static PlannerRaidPlanCard FocusedRaid(PlannerRaidPlanViewModel viewModel, PlannerRaidPlanUiState uiState)
        {
            if (viewModel == null || uiState == null || !uiState.HasProgressionTarget) return null;
            return viewModel.BestForQuest(uiState.ProgressionTargetQuestId);
        }

        private static string PlanBenefit(PlannerRaidPlanCard card)
        {
            if (card == null) return string.Empty;
            return "Advance " + card.ObjectiveCount + " task(s) across " + card.QuestCount + " quest(s) • " + card.ActionSummary + ".";
        }

        private static string ProgressionBenefit(PlannerRecommendationViewModel value)
        {
            if (value == null) return string.Empty;
            if (value.ImmediateUnlockCount > 0) return "Opens " + value.ImmediateUnlockCount + " next quest(s)";
            if (value.PathQuestCount > 1) return "Part of a " + value.PathQuestCount + "-quest progression path";
            return "Direct progression";
        }

        private static string ObjectiveProgress(PlannerRaidObjective objective)
        {
            if (!objective.HasProgress) return string.Empty;
            return " — " + FormatNumber(objective.CurrentValue ?? 0d) + "/" + FormatNumber(objective.RequiredValue ?? 0d) +
                   " done, " + FormatNumber(objective.RemainingValue ?? 0d) + " left";
        }

        private static string FormatNumber(double value)
        {
            double rounded = Math.Round(value);
            return Math.Abs(value - rounded) < 0.000001d ? rounded.ToString("0") : value.ToString("0.##");
        }
    }
}
