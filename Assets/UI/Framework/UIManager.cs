using System.Collections.Generic;
using UnityEngine;
using SoR.Core;

namespace SoR.UI
{
    public class UIManager : MonoBehaviour, IService
    {
        [SerializeField] private Transform _screenContainer;
        [SerializeField] private Transform _popupContainer;

        private readonly Stack<UIScreen> _screenStack = new();
        private readonly Dictionary<System.Type, UIPopup> _activePopups = new();

        public Transform ScreenContainer => _screenContainer;
        public Transform PopupContainer => _popupContainer;

        public UIScreen CurrentScreen => _screenStack.Count > 0 ? _screenStack.Peek() : null;

        public void Initialize()
        {
            Debug.Log("[UIManager] Initialized.");
        }

        public void Dispose()
        {
            HideAllScreens();
        }

        /// <summary>
        /// Shows a screen of the specified type. Hides the current screen if one is active.
        /// </summary>
        public T ShowScreen<T>() where T : UIScreen
        {
            T screen = _screenContainer != null
                ? _screenContainer.GetComponentInChildren<T>(true)
                : FindObjectOfType<T>(true);

            if (screen == null)
            {
                Debug.LogWarning($"[UIManager] Screen of type {typeof(T).Name} not found.");
                return null;
            }

            if (CurrentScreen != null)
            {
                CurrentScreen.Hide();
            }

            _screenStack.Push(screen);
            screen.Show();
            return screen;
        }

        /// <summary>
        /// Hides the current screen and reveals the previous one, if any.
        /// </summary>
        public void HideCurrentScreen()
        {
            if (_screenStack.Count == 0)
                return;

            var screen = _screenStack.Pop();
            screen.Hide();

            if (_screenStack.Count > 0)
            {
                _screenStack.Peek().Show();
            }
        }

        /// <summary>
        /// Hides all screens in the stack.
        /// </summary>
        public void HideAllScreens()
        {
            while (_screenStack.Count > 0)
            {
                var screen = _screenStack.Pop();
                screen.Hide();
            }
        }

        /// <summary>
        /// Shows a popup of the specified type.
        /// </summary>
        public T ShowPopup<T>() where T : UIPopup
        {
            var popupType = typeof(T);

            if (_activePopups.ContainsKey(popupType))
            {
                Debug.LogWarning($"[UIManager] Popup of type {popupType.Name} is already shown.");
                return _activePopups[popupType] as T;
            }

            T popup = _popupContainer != null
                ? _popupContainer.GetComponentInChildren<T>(true)
                : FindObjectOfType<T>(true);

            if (popup == null)
            {
                Debug.LogWarning($"[UIManager] Popup of type {popupType.Name} not found.");
                return null;
            }

            _activePopups[popupType] = popup;
            popup.Show(null);
            return popup;
        }

        /// <summary>
        /// Hides a popup of the specified type.
        /// </summary>
        public void HidePopup<T>() where T : UIPopup
        {
            var popupType = typeof(T);

            if (_activePopups.TryGetValue(popupType, out var popup))
            {
                popup.Hide();
                _activePopups.Remove(popupType);
            }
        }
    }
}
