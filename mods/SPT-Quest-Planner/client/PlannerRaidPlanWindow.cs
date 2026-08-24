using System;
using BepInEx.Configuration;
using UnityEngine;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidPlanWindow
    {
        private const int WindowId = 0x51504C4E;
        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.50f);
        private static readonly Color WindowBackgroundColor = new Color(0.075f, 0.08f, 0.085f, 1f);
        private readonly PlannerRaidPlanPresentationController presentation;
        private readonly Func<long> revisionProvider;
        private Rect windowRect = new Rect(160f, 100f, 900f, 620f);
        private Vector2 locationScroll;
        private Vector2 detailScroll;
        private bool visible;

        public PlannerRaidPlanWindow(
            PlannerRaidPlanPresentationController presentation,
            Func<long> revisionProvider)
        {
            this.presentation = presentation ?? throw new ArgumentNullException("presentation");
            this.revisionProvider = revisionProvider ?? throw new ArgumentNullException("revisionProvider");
        }

        public bool Visible { get { return visible; } }

        public void Toggle() { visible = !visible; }
        public void Hide() { visible = false; }

        public void Draw()
        {
            if (!visible) return;

            DrawModalBackdrop();
            DrawOpaqueWindowBackground(windowRect);
            windowRect = GUI.ModalWindow(WindowId, windowRect, DrawWindow, "Quest planner MOD SPT");

            Event current = Event.current;
            if (current != null && IsPointerEvent(current.type))
                current.Use();
        }

        private static void DrawModalBackdrop()
        {
            Color previous = GUI.color;
            GUI.color = BackdropColor;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private static void DrawOpaqueWindowBackground(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = WindowBackgroundColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private static bool IsPointerEvent(EventType type)
        {
            return type == EventType.MouseDown ||
                   type == EventType.MouseUp ||
                   type == EventType.MouseDrag ||
                   type == EventType.ScrollWheel ||
                   type == EventType.ContextClick;
        }

        private void DrawWindow(int id)
        {
            long revision = revisionProvider();
            PlannerRaidPlanViewModel viewModel = presentation.GetViewModel(revision, 12);
            PlannerRaidPlanUiState uiState = presentation.UiState;
            PlannerRaidPlanCard selected = uiState.ResolveSelection(viewModel);

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Raid plans: " + viewModel.LocationCount + "   Ready: " + viewModel.ReadyLocationCount, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Refresh data", GUILayout.Width(100f)))
            {
                Plugin instance = Plugin.Instance;
                if (instance != null) instance.RequestStateRefresh("ui-manual");
            }
            if (GUILayout.Button("X", GUILayout.Width(30f))) visible = false;
            GUILayout.EndHorizontal();

            DrawControls(uiState);

            GUILayout.BeginHorizontal();
            DrawLocationList(viewModel, uiState);
            DrawDetails(selected);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width - 140f, 24f));
        }

        private static void DrawControls(PlannerRaidPlanUiState uiState)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Rank:", GUILayout.Width(38f));

            bool readyFirst = uiState.RankingMode == PlannerRaidPlanRankingMode.ReadyFirst;
            if (GUILayout.Toggle(readyFirst, "Ready first", "Button", GUILayout.Width(100f)) && !readyFirst)
                uiState.SetRankingMode(PlannerRaidPlanRankingMode.ReadyFirst);

            bool densityFirst = uiState.RankingMode == PlannerRaidPlanRankingMode.QuestDensityFirst;
            if (GUILayout.Toggle(densityFirst, "Quest density", "Button", GUILayout.Width(110f)) && !densityFirst)
                uiState.SetRankingMode(PlannerRaidPlanRankingMode.QuestDensityFirst);

            GUILayout.Space(12f);
            bool includeAvailable = GUILayout.Toggle(uiState.IncludeAvailable, "Include available quests", GUILayout.Width(160f));
            if (includeAvailable != uiState.IncludeAvailable)
                uiState.SetIncludeAvailable(includeAvailable);

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawLocationList(PlannerRaidPlanViewModel viewModel, PlannerRaidPlanUiState uiState)
        {
            GUILayout.BeginVertical(GUILayout.Width(250f));
            GUILayout.Label("Locations");
            locationScroll = GUILayout.BeginScrollView(locationScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < viewModel.Cards.Count; i++)
            {
                PlannerRaidPlanCard card = viewModel.Cards[i];
                bool selected = string.Equals(card.LocationId, uiState.SelectedLocationId, StringComparison.OrdinalIgnoreCase);
                string label = "#" + card.Rank + "  " + PlannerDisplayNames.Location(card.LocationId) + "\n" +
                               card.QuestCount + " quests / " + card.ObjectiveCount + " objectives" +
                               (card.PreparationReady ? "  [READY]" : "  [MISSING " + card.MissingBringTemplateCount + "]");
                if (GUILayout.Toggle(selected, label, "Button", GUILayout.MinHeight(48f)) && !selected)
                    uiState.SelectLocation(card.LocationId);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawDetails(PlannerRaidPlanCard card)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (card == null)
            {
                GUILayout.Label("No raid opportunities available for the current quest state.");
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label(PlannerDisplayNames.Location(card.LocationId) + " — raid plan");
            GUILayout.Label(card.QuestCount + " relevant quests   " + card.ObjectiveCount + " objectives");
            GUILayout.Label(card.PreparationReady
                ? "Preparation: ready"
                : "Preparation: missing " + card.MissingBringTemplateCount + " required item type(s)");
            if (card.KnownRemainingWork > 0d)
                GUILayout.Label("Known remaining counter work: " + FormatNumber(card.KnownRemainingWork));

            PlannerClientCache cache = Plugin.Cache;
            PlannerTopologyIndex topology = cache == null ? null : cache.TopologyIndex;
            PlannerLocaleIndex locale = cache == null ? null : cache.LocaleIndex;

            detailScroll = GUILayout.BeginScrollView(detailScroll, GUILayout.ExpandHeight(true));
            GUILayout.Space(6f);
            GUILayout.Label("Objectives");
            for (int i = 0; i < card.Objectives.Count; i++)
            {
                PlannerRaidObjective objective = card.Objectives[i];
                string progress = objective.HasProgress
                    ? "  " + FormatNumber(objective.CurrentValue ?? 0d) + "/" + FormatNumber(objective.RequiredValue ?? 0d) +
                      " (remain " + FormatNumber(objective.RemainingValue ?? 0d) + ")"
                    : string.Empty;
                string targets = FormatTargets(objective, locale);
                string questLabel = PlannerQuestLabels.Resolve(topology, locale, objective.QuestId);
                GUILayout.Label("• " + PlannerDisplayNames.Objective(objective.Kind) + " — " + questLabel + progress + targets);
            }

            GUILayout.Space(10f);
            GUILayout.Label("Bring / preparation");
            if (card.BringNeeds.Count == 0)
            {
                GUILayout.Label("No proven bring-items for this raid plan.");
            }
            else
            {
                for (int i = 0; i < card.BringNeeds.Count; i++)
                {
                    PlannerRaidBringNeed need = card.BringNeeds[i];
                    string itemLabel = locale == null ? need.TemplateId : locale.ItemName(need.TemplateId);
                    GUILayout.Label("• " + itemLabel + "  need " + FormatNumber(need.Required) +
                                    " / owned " + FormatNumber(need.Owned) +
                                    " / missing " + FormatNumber(need.Missing));
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private static string FormatTargets(PlannerRaidObjective objective, PlannerLocaleIndex locale)
        {
            if (objective.Targets.Count == 0) return string.Empty;
            string[] labels = new string[objective.Targets.Count];
            for (int i = 0; i < objective.Targets.Count; i++)
            {
                string target = objective.Targets[i];
                labels[i] = locale == null ? target : locale.ItemName(target);
            }
            return "  [" + string.Join(", ", labels) + "]";
        }

        private static string FormatNumber(double value)
        {
            double rounded = Math.Round(value);
            return Math.Abs(value - rounded) < 0.000001d ? rounded.ToString("0") : value.ToString("0.##");
        }
    }
}
