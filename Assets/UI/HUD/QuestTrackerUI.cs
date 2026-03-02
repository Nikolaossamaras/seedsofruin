using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SoR.Core;
using SoR.Systems.Quests;

namespace SoR.UI
{
    public class QuestTrackerUI : MonoBehaviour
    {
        // ---- constants ----
        private const float PanelWidth = 260f;
        private const float MarginLeft = 12f;
        private const float ButtonSize = 28f;
        private const float ExpandDuration = 0.3f;
        private const float CollapseDuration = 0.25f;
        private const float CompletedRemovalDelay = 2f;
        private const string CollapsePrefsKey = "QuestTracker_Collapsed";

        // ---- styling ----
        private static readonly Color BgColor = new Color(0.08f, 0.06f, 0.12f, 0.85f);
        private static readonly Color HeaderColor = new Color(0.95f, 0.85f, 0.5f);
        private static readonly Color ObjectiveIncomplete = new Color(0.75f, 0.75f, 0.75f);
        private static readonly Color ObjectiveComplete = new Color(0.4f, 0.9f, 0.4f);
        private static readonly Color SeparatorColor = new Color(0.4f, 0.4f, 0.4f, 0.4f);

        // ---- references ----
        private Font _font;
        private RectTransform _rootRt;
        private RectTransform _contentPanelRt;
        private CanvasGroup _contentCg;
        private RectTransform _bgRt;
        private Text _headerText;
        private Text _arrowText;
        private Transform _entriesParent;

        // ---- state ----
        private bool _collapsed;
        private Coroutine _collapseCoroutine;

        private class QuestEntryUI
        {
            public GameObject Root;
            public Text QuestNameText;
            public List<Text> ObjectiveTexts;
        }
        private readonly Dictionary<string, QuestEntryUI> _questEntries = new();

        public bool IsCollapsed => _collapsed;

        // ================================================================
        // Factory
        // ================================================================

        public static QuestTrackerUI Create(Canvas canvas, Font font)
        {
            // Root
            var rootGo = new GameObject("QuestTrackerRoot");
            rootGo.transform.SetParent(canvas.transform, false);
            var rootRt = rootGo.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0f, 0.5f);
            rootRt.anchorMax = new Vector2(0f, 0.5f);
            rootRt.pivot = new Vector2(0f, 0.5f);
            rootRt.anchoredPosition = new Vector2(MarginLeft, 0f);
            rootRt.sizeDelta = new Vector2(PanelWidth + ButtonSize, 400f);

            var ui = rootGo.AddComponent<QuestTrackerUI>();
            ui._font = font;
            ui._rootRt = rootRt;

            // Content panel
            var contentGo = new GameObject("ContentPanel");
            contentGo.transform.SetParent(rootGo.transform, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 0.5f);
            contentRt.anchorMax = new Vector2(0f, 0.5f);
            contentRt.pivot = new Vector2(0f, 0.5f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(PanelWidth, 200f);
            ui._contentPanelRt = contentRt;
            ui._contentCg = contentGo.AddComponent<CanvasGroup>();

            // Background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(contentGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgGo.AddComponent<Image>().color = BgColor;
            ui._bgRt = bgRt;

            // Header text
            var headerGo = new GameObject("HeaderText");
            headerGo.transform.SetParent(bgGo.transform, false);
            var headerRt = headerGo.AddComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, -6f);
            headerRt.sizeDelta = new Vector2(-20f, 22f);
            var headerText = headerGo.AddComponent<Text>();
            headerText.font = font;
            headerText.fontSize = 15;
            headerText.fontStyle = FontStyle.Bold;
            headerText.color = HeaderColor;
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.text = "Active Quests";
            ui._headerText = headerText;

            // Entries parent (holds quest entries below header)
            ui._entriesParent = bgGo.transform;

            // Collapse button — right edge of root
            var btnGo = new GameObject("CollapseButton");
            btnGo.transform.SetParent(rootGo.transform, false);
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 0.5f);
            btnRt.anchorMax = new Vector2(0f, 0.5f);
            btnRt.pivot = new Vector2(0f, 0.5f);
            btnRt.anchoredPosition = new Vector2(PanelWidth, 0f);
            btnRt.sizeDelta = new Vector2(ButtonSize, ButtonSize);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.12f, 0.1f, 0.18f, 0.9f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            var arrowGo = new GameObject("ArrowText");
            arrowGo.transform.SetParent(btnGo.transform, false);
            var arrowRt = arrowGo.AddComponent<RectTransform>();
            arrowRt.anchorMin = Vector2.zero;
            arrowRt.anchorMax = Vector2.one;
            arrowRt.offsetMin = Vector2.zero;
            arrowRt.offsetMax = Vector2.zero;
            var arrowText = arrowGo.AddComponent<Text>();
            arrowText.font = font;
            arrowText.fontSize = 16;
            arrowText.fontStyle = FontStyle.Bold;
            arrowText.color = HeaderColor;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.text = "\u25C4"; // ◄
            ui._arrowText = arrowText;

            btn.onClick.AddListener(() => ui.SetCollapsed(!ui._collapsed));

            // Restore collapse state
            ui._collapsed = PlayerPrefs.GetInt(CollapsePrefsKey, 0) == 1;
            if (ui._collapsed)
            {
                ui._contentCg.alpha = 0f;
                ui._arrowText.text = "\u25BA"; // ►
                rootRt.anchoredPosition = new Vector2(-(PanelWidth - ButtonSize) + MarginLeft, 0f);
            }

            return ui;
        }

        // ================================================================
        // Unity lifecycle / Event wiring
        // ================================================================

        private void OnEnable()
        {
            EventBus.Subscribe<QuestAcceptedEvent>(OnQuestAccepted);
            EventBus.Subscribe<QuestObjectiveUpdatedEvent>(OnObjectiveUpdated);
            EventBus.Subscribe<QuestCompletedEvent>(OnQuestCompleted);
            EventBus.Subscribe<QuestAbandonedEvent>(OnQuestAbandoned);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<QuestAcceptedEvent>(OnQuestAccepted);
            EventBus.Unsubscribe<QuestObjectiveUpdatedEvent>(OnObjectiveUpdated);
            EventBus.Unsubscribe<QuestCompletedEvent>(OnQuestCompleted);
            EventBus.Unsubscribe<QuestAbandonedEvent>(OnQuestAbandoned);
        }

        private void Start()
        {
            RefreshAllQuests();
        }

        // ================================================================
        // Public API
        // ================================================================

        public void SetCollapsed(bool collapsed)
        {
            if (_collapsed == collapsed) return;
            _collapsed = collapsed;
            PlayerPrefs.SetInt(CollapsePrefsKey, collapsed ? 1 : 0);

            if (_collapseCoroutine != null)
                StopCoroutine(_collapseCoroutine);
            _collapseCoroutine = StartCoroutine(AnimateCollapse(collapsed));
        }

        public void RefreshAllQuests()
        {
            // Destroy existing entries
            foreach (var kvp in _questEntries)
            {
                if (kvp.Value.Root != null)
                    Destroy(kvp.Value.Root);
            }
            _questEntries.Clear();

            if (!ServiceLocator.TryResolve<QuestManager>(out var qm))
                return;

            var quests = qm.GetActiveQuests();
            float yOffset = -32f; // below header
            int count = 0;

            foreach (var kvp in quests)
            {
                var state = kvp.Value;
                if (state.IsComplete) continue;

                yOffset = BuildQuestEntry(state, yOffset);
                count++;
            }

            if (count == 0)
            {
                // "No active quests" placeholder
                var emptyGo = new GameObject("EmptyText");
                emptyGo.transform.SetParent(_entriesParent, false);
                var emptyRt = emptyGo.AddComponent<RectTransform>();
                emptyRt.anchorMin = new Vector2(0f, 1f);
                emptyRt.anchorMax = new Vector2(1f, 1f);
                emptyRt.pivot = new Vector2(0.5f, 1f);
                emptyRt.anchoredPosition = new Vector2(0f, yOffset);
                emptyRt.sizeDelta = new Vector2(-20f, 20f);
                var emptyText = emptyGo.AddComponent<Text>();
                emptyText.font = _font;
                emptyText.fontSize = 12;
                emptyText.fontStyle = FontStyle.Italic;
                emptyText.color = ObjectiveIncomplete;
                emptyText.alignment = TextAnchor.MiddleCenter;
                emptyText.text = "No active quests";

                // Track for cleanup
                _questEntries["__empty__"] = new QuestEntryUI
                {
                    Root = emptyGo,
                    QuestNameText = emptyText,
                    ObjectiveTexts = new List<Text>()
                };
                yOffset -= 26f;
            }

            RecalculatePanelHeight(yOffset);
        }

        // ================================================================
        // Quest entry builder
        // ================================================================

        private float BuildQuestEntry(QuestState state, float yOffset)
        {
            var def = state.Definition;
            var entryGo = new GameObject($"QuestEntry_{def.QuestId}");
            entryGo.transform.SetParent(_entriesParent, false);
            var entryRt = entryGo.AddComponent<RectTransform>();
            entryRt.anchorMin = Vector2.zero;
            entryRt.anchorMax = Vector2.one;
            entryRt.offsetMin = Vector2.zero;
            entryRt.offsetMax = Vector2.zero;

            // Quest name
            var nameGo = new GameObject("QuestNameText");
            nameGo.transform.SetParent(entryGo.transform, false);
            var nameRt = nameGo.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 1f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0.5f, 1f);
            nameRt.anchoredPosition = new Vector2(0f, yOffset);
            nameRt.sizeDelta = new Vector2(-20f, 20f);
            var nameText = nameGo.AddComponent<Text>();
            nameText.font = _font;
            nameText.fontSize = 14;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.text = def.QuestName;

            yOffset -= 20f;

            // Objectives
            var objectiveTexts = new List<Text>();
            for (int i = 0; i < def.Objectives.Count; i++)
            {
                var obj = def.Objectives[i];
                bool complete = state.ObjectiveProgress[i] >= obj.RequiredCount;

                var objGo = new GameObject($"ObjectiveText_{i}");
                objGo.transform.SetParent(entryGo.transform, false);
                var objRt = objGo.AddComponent<RectTransform>();
                objRt.anchorMin = new Vector2(0f, 1f);
                objRt.anchorMax = new Vector2(1f, 1f);
                objRt.pivot = new Vector2(0.5f, 1f);
                objRt.anchoredPosition = new Vector2(0f, yOffset);
                objRt.sizeDelta = new Vector2(-20f, 18f);
                var objText = objGo.AddComponent<Text>();
                objText.font = _font;
                objText.fontSize = 12;
                objText.color = complete ? ObjectiveComplete : ObjectiveIncomplete;
                objText.alignment = TextAnchor.MiddleLeft;

                string prefix = complete ? "  [X] " : "  \u25CF "; // ● for incomplete
                objText.text = $"{prefix}{obj.ObjectiveDescription} ({state.ObjectiveProgress[i]}/{obj.RequiredCount})";
                objectiveTexts.Add(objText);

                yOffset -= 18f + 3f; // line height + spacing between objectives
            }

            // Separator line
            var sepGo = new GameObject("SeparatorLine");
            sepGo.transform.SetParent(entryGo.transform, false);
            var sepRt = sepGo.AddComponent<RectTransform>();
            sepRt.anchorMin = new Vector2(0f, 1f);
            sepRt.anchorMax = new Vector2(1f, 1f);
            sepRt.pivot = new Vector2(0.5f, 1f);
            sepRt.anchoredPosition = new Vector2(0f, yOffset);
            sepRt.sizeDelta = new Vector2(-20f, 1f);
            var sepImg = sepGo.AddComponent<Image>();
            sepImg.color = SeparatorColor;
            yOffset -= 7f; // separator + spacing

            _questEntries[def.QuestId] = new QuestEntryUI
            {
                Root = entryGo,
                QuestNameText = nameText,
                ObjectiveTexts = objectiveTexts
            };

            return yOffset;
        }

        private void RecalculatePanelHeight(float yOffset)
        {
            float totalHeight = Mathf.Abs(yOffset) + 10f; // + bottom padding
            totalHeight = Mathf.Max(totalHeight, 50f);
            _contentPanelRt.sizeDelta = new Vector2(PanelWidth, totalHeight);
            _rootRt.sizeDelta = new Vector2(PanelWidth + ButtonSize, totalHeight);
        }

        // ================================================================
        // Event handlers
        // ================================================================

        private void OnQuestAccepted(QuestAcceptedEvent e)
        {
            RefreshAllQuests();

            // Scale-punch on new entry
            if (_questEntries.TryGetValue(e.QuestId, out var entry) && entry.Root != null)
            {
                StartCoroutine(ScalePunch(entry.Root.transform));
            }
        }

        private void OnObjectiveUpdated(QuestObjectiveUpdatedEvent e)
        {
            if (!_questEntries.TryGetValue(e.QuestId, out var entry))
                return;

            if (!ServiceLocator.TryResolve<QuestManager>(out var qm))
                return;

            var state = qm.GetQuestState(e.QuestId);
            if (state == null) return;

            if (e.ObjectiveIndex >= 0 && e.ObjectiveIndex < entry.ObjectiveTexts.Count)
            {
                var obj = state.Definition.Objectives[e.ObjectiveIndex];
                bool complete = e.Progress >= obj.RequiredCount;
                var text = entry.ObjectiveTexts[e.ObjectiveIndex];
                string prefix = complete ? "  [X] " : "  \u25CF ";
                text.text = $"{prefix}{obj.ObjectiveDescription} ({e.Progress}/{obj.RequiredCount})";
                text.color = complete ? ObjectiveComplete : ObjectiveIncomplete;

                // Brief highlight flash
                StartCoroutine(HighlightFlash(text));
            }
        }

        private void OnQuestCompleted(QuestCompletedEvent e)
        {
            if (!_questEntries.TryGetValue(e.QuestId, out var entry))
                return;

            // Mark all objectives green
            foreach (var objText in entry.ObjectiveTexts)
            {
                if (objText != null)
                    objText.color = ObjectiveComplete;
            }

            StartCoroutine(CompletedRemoval(e.QuestId, entry));
        }

        private void OnQuestAbandoned(QuestAbandonedEvent e)
        {
            if (!_questEntries.TryGetValue(e.QuestId, out var entry))
                return;

            StartCoroutine(FadeOutAndRefresh(entry.Root, 0.2f));
        }

        // ================================================================
        // Animation coroutines
        // ================================================================

        private IEnumerator AnimateCollapse(bool collapse)
        {
            float duration = collapse ? CollapseDuration : ExpandDuration;
            float startX = _rootRt.anchoredPosition.x;
            float endX = collapse ? -(PanelWidth - ButtonSize) + MarginLeft : MarginLeft;
            float startAlpha = _contentCg.alpha;
            float endAlpha = collapse ? 0f : 1f;

            _arrowText.text = collapse ? "\u25BA" : "\u25C4"; // ► or ◄

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = collapse ? EaseInQuad(t) : EaseOutQuad(t);

                _rootRt.anchoredPosition = new Vector2(
                    Mathf.Lerp(startX, endX, eased),
                    _rootRt.anchoredPosition.y);
                _contentCg.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
                yield return null;
            }

            _rootRt.anchoredPosition = new Vector2(endX, _rootRt.anchoredPosition.y);
            _contentCg.alpha = endAlpha;
            _collapseCoroutine = null;
        }

        private IEnumerator ScalePunch(Transform target)
        {
            // Quick scale up then back to normal
            float elapsed = 0f;
            const float duration = 0.2f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = 1f + 0.1f * Mathf.Sin(t * Mathf.PI);
                if (target != null)
                    target.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            if (target != null)
                target.localScale = Vector3.one;
        }

        private IEnumerator HighlightFlash(Text text)
        {
            if (text == null) yield break;
            Color original = text.color;
            float elapsed = 0f;
            const float duration = 0.2f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                text.color = Color.Lerp(Color.white, original, t);
                yield return null;
            }
            if (text != null)
                text.color = original;
        }

        private IEnumerator CompletedRemoval(string questId, QuestEntryUI entry)
        {
            yield return new WaitForSecondsRealtime(CompletedRemovalDelay);

            if (entry.Root != null)
            {
                yield return FadeOutEntry(entry.Root, 0.3f);
            }

            RefreshAllQuests();
        }

        private IEnumerator FadeOutAndRefresh(GameObject go, float duration)
        {
            yield return FadeOutEntry(go, duration);
            RefreshAllQuests();
        }

        private IEnumerator FadeOutEntry(GameObject go, float duration)
        {
            if (go == null) yield break;

            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (go == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            if (go != null) Destroy(go);
        }

        // ---- easing ----

        private static float EaseInQuad(float t) => t * t;
        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    }
}
