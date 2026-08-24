using System;
using BepInEx.Configuration;
using UnityEngine;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidPlanWindow
    {
        private const int WindowId = 0x51504C4E;
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

        public void Toggle()
        {
            visible = !visible;
        }

        public void Hide()
        {
            visible = false;
        }

        public void Draw()
        {
            if (!visible) return;
            windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "Quest planner MOD SPT");
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
            if (GUILayout.Button("Refresh view", GUILayout.Width(100f))) presentation.Invalidate();
            if (GUILayout.Button("X", GUILayout.Width(30f))) visible = false;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawLocationList(viewModel, uiState);
            DrawDetails(selected);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width - 140f, 24f));
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
                string label = "#" + card.Rank + "  " + card.LocationId + "\n" +
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

            GUILayout.Label(card.LocationId + " — raid plan");
            GUILayout.Label(card.QuestCount + " relevant quests   " + card.ObjectiveCount + " objectives");
            GUILayout.Label(card.PreparationReady
                ? "Preparation: ready"
                : "Preparation: missing " + card.MissingBringTemplateCount + " required item type(s)");
            if (card.KnownRemainingWork > 0d)
                GUILayout.Label("Known remaining counter work: " + FormatNumber(card.KnownRemainingWork));

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
                string targets = objective.Targets.Count == 0 ? string.Empty : "  [" + string.Join(", ", objective.Targets) + "]";
                GUILayout.Label("• " + objective.Kind + " — " + objective.ConditionType + progress + targets);
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
                    GUILayout.Label("• " + need.TemplateId + "  need " + FormatNumber(need.Required) +
                                    " / owned " + FormatNumber(need.Owned) +
                                    " / missing " + FormatNumber(need.Missing));
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private static string FormatNumber(double value)
        {
            double rounded = Math.Round(value);
            return Math.Abs(value - rounded) < 0.000001d ? rounded.ToString("0") : value.ToString("0.##");
        }
    }
}
