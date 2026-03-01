using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SoR.Core;
using SoR.Systems.Inventory;
using SoR.Systems.Quests;

namespace SoR.UI
{
    public class ItemNotificationUI : MonoBehaviour
    {
        // ---- constants ----
        private const float EntryWidth = 280f;
        private const float EntryHeight = 56f;
        private const float SlideInDuration = 0.35f;
        private const float HoldDuration = 2.5f;
        private const float QuestHoldDuration = 3.5f;
        private const float SlideOutDuration = 0.3f;
        private const float OffScreenX = 300f;
        private const float RestingX = -20f;
        private const float StackShift = 64f; // EntryHeight + 8px spacing
        private const float ShiftDuration = 0.15f;
        private const int MaxVisible = 5;

        private Font _font;
        private RectTransform _containerRt;
        private readonly List<RectTransform> _activeNotifications = new();

        // ================================================================
        // Factory
        // ================================================================

        public static ItemNotificationUI Create(Canvas canvas, Font font)
        {
            var rootGo = new GameObject("NotificationContainer");
            rootGo.transform.SetParent(canvas.transform, false);

            var rt = rootGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            var ui = rootGo.AddComponent<ItemNotificationUI>();
            ui._font = font;
            ui._containerRt = rt;
            return ui;
        }

        // ================================================================
        // Event wiring
        // ================================================================

        private void OnEnable()
        {
            EventBus.Subscribe<ItemCollectedEvent>(OnItemCollected);
            EventBus.Subscribe<QuestCompletedEvent>(OnQuestCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ItemCollectedEvent>(OnItemCollected);
            EventBus.Unsubscribe<QuestCompletedEvent>(OnQuestCompleted);
        }

        private void OnItemCollected(ItemCollectedEvent e)
        {
            ShowNotification(FormatItemName(e.ItemId), e.Quantity);
        }

        private void OnQuestCompleted(QuestCompletedEvent e)
        {
            ShowQuestComplete(e.QuestName);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void ShowNotification(string itemName, int quantity)
        {
            // Enforce max visible — fast-dismiss oldest
            if (_activeNotifications.Count >= MaxVisible)
            {
                var oldest = _activeNotifications[0];
                _activeNotifications.RemoveAt(0);
                StopCoroutinesOnEntry(oldest);
                StartCoroutine(FastDismiss(oldest, 0.1f));
            }

            // Shift existing entries up
            for (int i = 0; i < _activeNotifications.Count; i++)
            {
                var entry = _activeNotifications[i];
                if (entry != null)
                {
                    float targetY = (_activeNotifications.Count - i) * StackShift;
                    StartCoroutine(ShiftEntry(entry, targetY));
                }
            }

            // Build new entry
            var entryRt = BuildNotificationEntry(itemName, quantity);
            _activeNotifications.Add(entryRt);
            StartCoroutine(NotificationLifecycle(entryRt));
        }

        public void ShowQuestComplete(string questName)
        {
            // Enforce max visible — fast-dismiss oldest
            if (_activeNotifications.Count >= MaxVisible)
            {
                var oldest = _activeNotifications[0];
                _activeNotifications.RemoveAt(0);
                StopCoroutinesOnEntry(oldest);
                StartCoroutine(FastDismiss(oldest, 0.1f));
            }

            // Shift existing entries up
            for (int i = 0; i < _activeNotifications.Count; i++)
            {
                var entry = _activeNotifications[i];
                if (entry != null)
                {
                    float targetY = (_activeNotifications.Count - i) * StackShift;
                    StartCoroutine(ShiftEntry(entry, targetY));
                }
            }

            // Build new entry
            var entryRt = BuildQuestNotificationEntry(questName);
            _activeNotifications.Add(entryRt);
            StartCoroutine(QuestNotificationLifecycle(entryRt));
        }

        // ================================================================
        // Entry builder
        // ================================================================

        private RectTransform BuildNotificationEntry(string itemName, int quantity)
        {
            // Entry root
            var entryGo = new GameObject("NotificationEntry");
            entryGo.transform.SetParent(_containerRt, false);
            var entryRt = entryGo.AddComponent<RectTransform>();
            entryRt.anchorMin = new Vector2(1f, 0.5f);
            entryRt.anchorMax = new Vector2(1f, 0.5f);
            entryRt.pivot = new Vector2(1f, 0.5f);
            entryRt.sizeDelta = new Vector2(EntryWidth, EntryHeight);
            entryRt.anchoredPosition = new Vector2(OffScreenX, 0f);

            var cg = entryGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            // Background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(entryGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.08f, 0.15f, 0.92f);

            // Icon background
            var iconGo = new GameObject("IconBg");
            iconGo.transform.SetParent(bgGo.transform, false);
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(10f, 0f);
            iconRt.sizeDelta = new Vector2(36f, 36f);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = GetIconColor(itemName);

            // Item name text
            var nameGo = new GameObject("ItemNameText");
            nameGo.transform.SetParent(bgGo.transform, false);
            var nameRt = nameGo.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.offsetMin = new Vector2(54f, 0f);
            nameRt.offsetMax = new Vector2(-50f, 0f);
            var nameText = nameGo.AddComponent<Text>();
            nameText.font = _font;
            nameText.fontSize = 15;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.text = itemName;

            // Quantity text
            var qtyGo = new GameObject("QuantityText");
            qtyGo.transform.SetParent(bgGo.transform, false);
            var qtyRt = qtyGo.AddComponent<RectTransform>();
            qtyRt.anchorMin = new Vector2(1f, 0f);
            qtyRt.anchorMax = new Vector2(1f, 1f);
            qtyRt.pivot = new Vector2(1f, 0.5f);
            qtyRt.anchoredPosition = new Vector2(-10f, 0f);
            qtyRt.sizeDelta = new Vector2(40f, EntryHeight);
            var qtyText = qtyGo.AddComponent<Text>();
            qtyText.font = _font;
            qtyText.fontSize = 13;
            qtyText.fontStyle = FontStyle.Bold;
            qtyText.color = new Color(1f, 0.85f, 0.3f);
            qtyText.alignment = TextAnchor.MiddleRight;
            qtyText.text = $"x{quantity}";

            return entryRt;
        }

        private RectTransform BuildQuestNotificationEntry(string questName)
        {
            // Entry root
            var entryGo = new GameObject("QuestNotificationEntry");
            entryGo.transform.SetParent(_containerRt, false);
            var entryRt = entryGo.AddComponent<RectTransform>();
            entryRt.anchorMin = new Vector2(1f, 0.5f);
            entryRt.anchorMax = new Vector2(1f, 0.5f);
            entryRt.pivot = new Vector2(1f, 0.5f);
            entryRt.sizeDelta = new Vector2(EntryWidth, EntryHeight);
            entryRt.anchoredPosition = new Vector2(OffScreenX, 0f);

            var cg = entryGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            // Background — dark green tint
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(entryGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.15f, 0.08f, 0.95f);

            // Icon background — gold square
            var iconGo = new GameObject("IconBg");
            iconGo.transform.SetParent(bgGo.transform, false);
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(10f, 0f);
            iconRt.sizeDelta = new Vector2(36f, 36f);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = new Color(1f, 0.85f, 0.3f);

            // Top text — "Quest Complete!" in gold
            var topGo = new GameObject("TopText");
            topGo.transform.SetParent(bgGo.transform, false);
            var topRt = topGo.AddComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 0.5f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.offsetMin = new Vector2(54f, 0f);
            topRt.offsetMax = new Vector2(-10f, 0f);
            var topText = topGo.AddComponent<Text>();
            topText.font = _font;
            topText.fontSize = 12;
            topText.color = new Color(1f, 0.85f, 0.3f);
            topText.alignment = TextAnchor.LowerLeft;
            topText.text = "Quest Complete!";

            // Bottom text — quest name in bold white
            var botGo = new GameObject("QuestNameText");
            botGo.transform.SetParent(bgGo.transform, false);
            var botRt = botGo.AddComponent<RectTransform>();
            botRt.anchorMin = new Vector2(0f, 0f);
            botRt.anchorMax = new Vector2(1f, 0.5f);
            botRt.offsetMin = new Vector2(54f, 0f);
            botRt.offsetMax = new Vector2(-10f, 0f);
            var botText = botGo.AddComponent<Text>();
            botText.font = _font;
            botText.fontSize = 14;
            botText.fontStyle = FontStyle.Bold;
            botText.color = Color.white;
            botText.alignment = TextAnchor.UpperLeft;
            botText.text = questName;

            return entryRt;
        }

        // ================================================================
        // Animation coroutines
        // ================================================================

        private IEnumerator NotificationLifecycle(RectTransform entryRt)
        {
            if (entryRt == null) yield break;
            var cg = entryRt.GetComponent<CanvasGroup>();

            // Slide in (0.35s) — ease-out-back
            float elapsed = 0f;
            while (elapsed < SlideInDuration)
            {
                if (entryRt == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / SlideInDuration);
                float eased = EaseOutBack(t);
                float x = Mathf.Lerp(OffScreenX, RestingX, eased);
                entryRt.anchoredPosition = new Vector2(x, entryRt.anchoredPosition.y);
                cg.alpha = t;
                yield return null;
            }
            if (entryRt == null) yield break;
            entryRt.anchoredPosition = new Vector2(RestingX, entryRt.anchoredPosition.y);
            cg.alpha = 1f;

            // Hold (2.5s)
            yield return new WaitForSecondsRealtime(HoldDuration);

            // Slide out (0.3s) — ease-in-quad
            if (entryRt == null) yield break;
            elapsed = 0f;
            while (elapsed < SlideOutDuration)
            {
                if (entryRt == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / SlideOutDuration);
                float eased = EaseInQuad(t);
                float x = Mathf.Lerp(RestingX, OffScreenX, eased);
                entryRt.anchoredPosition = new Vector2(x, entryRt.anchoredPosition.y);
                cg.alpha = 1f - t;
                yield return null;
            }

            if (entryRt == null) yield break;
            RemoveEntry(entryRt);
        }

        private IEnumerator QuestNotificationLifecycle(RectTransform entryRt)
        {
            if (entryRt == null) yield break;
            var cg = entryRt.GetComponent<CanvasGroup>();

            // Slide in (0.35s) — ease-out-back
            float elapsed = 0f;
            while (elapsed < SlideInDuration)
            {
                if (entryRt == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / SlideInDuration);
                float eased = EaseOutBack(t);
                float x = Mathf.Lerp(OffScreenX, RestingX, eased);
                entryRt.anchoredPosition = new Vector2(x, entryRt.anchoredPosition.y);
                cg.alpha = t;
                yield return null;
            }
            if (entryRt == null) yield break;
            entryRt.anchoredPosition = new Vector2(RestingX, entryRt.anchoredPosition.y);
            cg.alpha = 1f;

            // Hold (3.5s)
            yield return new WaitForSecondsRealtime(QuestHoldDuration);

            // Slide out (0.3s) — ease-in-quad
            if (entryRt == null) yield break;
            elapsed = 0f;
            while (elapsed < SlideOutDuration)
            {
                if (entryRt == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / SlideOutDuration);
                float eased = EaseInQuad(t);
                float x = Mathf.Lerp(RestingX, OffScreenX, eased);
                entryRt.anchoredPosition = new Vector2(x, entryRt.anchoredPosition.y);
                cg.alpha = 1f - t;
                yield return null;
            }

            if (entryRt == null) yield break;
            RemoveEntry(entryRt);
        }

        private IEnumerator ShiftEntry(RectTransform entryRt, float targetY)
        {
            if (entryRt == null) yield break;

            float startY = entryRt.anchoredPosition.y;
            float elapsed = 0f;
            while (elapsed < ShiftDuration)
            {
                if (entryRt == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / ShiftDuration);
                float y = Mathf.Lerp(startY, targetY, t);
                entryRt.anchoredPosition = new Vector2(entryRt.anchoredPosition.x, y);
                yield return null;
            }
            if (entryRt != null)
                entryRt.anchoredPosition = new Vector2(entryRt.anchoredPosition.x, targetY);
        }

        private IEnumerator FastDismiss(RectTransform entryRt, float duration)
        {
            if (entryRt == null) yield break;
            var cg = entryRt.GetComponent<CanvasGroup>();
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (entryRt == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float x = Mathf.Lerp(RestingX, OffScreenX, t);
                entryRt.anchoredPosition = new Vector2(x, entryRt.anchoredPosition.y);
                if (cg != null) cg.alpha = 1f - t;
                yield return null;
            }

            if (entryRt != null)
                Destroy(entryRt.gameObject);
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void RemoveEntry(RectTransform entryRt)
        {
            _activeNotifications.Remove(entryRt);
            if (entryRt != null)
                Destroy(entryRt.gameObject);

            // Slide remaining entries down to fill gap
            for (int i = 0; i < _activeNotifications.Count; i++)
            {
                var entry = _activeNotifications[i];
                if (entry != null)
                {
                    float targetY = (_activeNotifications.Count - 1 - i) * StackShift;
                    StartCoroutine(ShiftEntry(entry, targetY));
                }
            }
        }

        private void StopCoroutinesOnEntry(RectTransform entryRt)
        {
            // We can't stop per-object coroutines since they run on this MonoBehaviour,
            // but destroying the GO will cause null checks to bail out of running coroutines.
        }

        private static string FormatItemName(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            var parts = id.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join(" ", parts);
        }

        private static Color GetIconColor(string itemName)
        {
            string lower = itemName.ToLowerInvariant();
            if (lower.Contains("crystal") || lower.Contains("runic") || lower.Contains("moonstone"))
                return new Color(0.5f, 0.7f, 1f);
            if (lower.Contains("ancient") || lower.Contains("fire"))
                return new Color(1f, 0.85f, 0.3f);
            if (lower.Contains("wolf") || lower.Contains("pelt"))
                return new Color(0.8f, 0.6f, 0.3f);
            return new Color(0.6f, 0.6f, 0.6f);
        }

        // ---- easing functions ----

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private static float EaseInQuad(float t)
        {
            return t * t;
        }
    }
}
