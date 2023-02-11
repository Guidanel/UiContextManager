using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UiContextManager
{
    public class UiContextManager : MonoBehaviour
    {
        // We want to make it simple to use this system for most cases. Most of the times there will only be one
        // context manager per scene and in that case we want to access it simply via UiContextManager.Main
        // as well as be able to register to the events simply via UiContextManager.OnMainContextChanged.
        // We can also use the UiContextManager for other UI parts of the scene, in which case we set _isMain to
        // false and we will need its reference to call the ChangeContext method or register to OnContextChanged.
        public static UiContextManager Main;

        [Tooltip("True: this context manager is accessible in the scene via UiContextManager.Main" +
                 "and context changes can be listened via OnMainContextChanged.")]
        [SerializeField]
        private bool _isMain = true;

        [Tooltip("The context at the start of the scene. 'None' means 'no context' so all the UI elements" +
                 " linked to a context will be hidden at the start.")]
        [SerializeField]
        private UiContext _startContext;

        [Tooltip("The contexts and their associated UI elements.")] [SerializeField]
        private List<ContextGroup> _contextGroups = new();

        // Called everytime the context changes on the Main UI context manager.
        public static event Action<UiContext, UiContext> OnMainContextChanged;

        // Called everytime the context changes on this UI context manager.
        public event Action<UiContext, UiContext> OnContextChanged;

        // The previous context.
        private UiContext _previousContext;

        // The current context.
        private UiContext _currentContext;

        // The stack of previous contexts.
        private Stack<UiContext> _previousContexts = new();

        // The speed multiplier can make the animations run slower/faster if they are set to use it.
        public static float SpeedMultiplier
        {
            private get
            {
                return _speedMultiplier;
            }
            set
            {
                // Values <= 0f are not valid.
                _speedMultiplier = Mathf.Max(0.01f, value);
            }
        }
        private static float _speedMultiplier = 1f;

        private void Awake()
        {
            if (_isMain)
            {
                if (Main != null)
                {
                    Debug.LogError("There are more than one Main UI context managers in this scene.");
                }

                Main = this;
            }

            // Initialize the context groups and hide them so they can be shown when the start context is set.
            for (int i = 0; i < _contextGroups.Count; i++)
            {
                _contextGroups[i].Initialize(_currentContext);
            }
        }

        private void Start()
        {
            if (_startContext != null)
            {
                ChangeContext(_startContext);
            }
        }

        private void OnDestroy()
        {
            Main = null;
        }

        // Change the context
        public void ChangeContext(UiContext newContext)
        {
            if (newContext == _currentContext)
            {
                return;
            }

            _previousContexts.Push(_previousContext);

            _previousContext = _currentContext;
            _currentContext = newContext;

            // Notify all the context UI groups of the context change
            for (int i = 0; i < _contextGroups.Count; i++)
            {
                _contextGroups[i].ChangeContext(_previousContext, _currentContext);
            }

            if (_isMain)
            {
                OnMainContextChanged?.Invoke(_previousContext, _currentContext);
            }

            OnContextChanged?.Invoke(_previousContext, _currentContext);
        }

        public bool IsInContext(UiContext context)
        {
            return _currentContext == context;
        }

        public void GoToPreviousContext()
        {
            if (_previousContexts.Count <= 0) return;

            ChangeContext(_previousContext);
            _previousContexts.Pop();
        }

        // Saves the new state of the button if an animation is ongoing or if the button is hidden,
        // otherwise immediately sets the new state. Very useful to make sure the new interactability
        // state will not be lost due to the animations overriding it while they happen.
        public void SetButtonInteractability(Button button, bool interactable)
        {
            for (int i = 0; i < _contextGroups.Count; i++)
            {
                _contextGroups[i].SetButtonInteractability(button, interactable);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            for (int i = 0; i < _contextGroups.Count; i++)
            {
                _contextGroups[i].RefreshNames();
            }
        }
#endif

        [Serializable]
        private class ContextGroup
        {
            // For readability in the inspector.
            [SerializeField] [HideInInspector] private string _groupName;
            
            [Tooltip("True: the speed of the animations are multiplied by the UiContextManager SpeedMultiplier.")]
            [SerializeField] private bool _useSpeedMultiplier;

            [Tooltip("All the contexts that the UI elements belong to.")]
            [SerializeField] private List<UiContext> _contexts = new ();
            [SerializeField] private List<UiElement> _uiElements = new ();

            public void Initialize(UiContext currentContext)
            {
                bool startsHidden = !ContainsContext(currentContext);
                for (int i = 0; i < _uiElements.Count; i++)
                {
                    _uiElements[i].Initialize(startsHidden);
                }
            }

            public void ChangeContext(UiContext previousContext, UiContext currentContext)
            {
                bool wasInPreviousContext = ContainsContext(previousContext);
                bool isInCurrentContext = ContainsContext(currentContext);

                if (wasInPreviousContext && !isInCurrentContext)
                {
                    Hide();
                }
                else if (!wasInPreviousContext && isInCurrentContext)
                {
                    Show();
                }
            }

            public void SetButtonInteractability(Button button, bool interactable)
            {
                for (int i = 0; i < _uiElements.Count; i++)
                {
                    _uiElements[i].SetButtonInteractability(button, interactable);
                }
            }

            private void Hide()
            {
                for (int i = 0; i < _uiElements.Count; i++)
                {
                    _uiElements[i].Hide(_useSpeedMultiplier);
                }
            }

            private void Show()
            {
                // Move the UI towards their initial positions. Kill the tweens moving them if applicable.
                for (int i = 0; i < _uiElements.Count; i++)
                {
                    _uiElements[i].Show(_useSpeedMultiplier);
                }
            }

            private bool ContainsContext(UiContext context)
            {
                return _contexts.Contains(context);
            }

#if UNITY_EDITOR
            public void RefreshNames()
            {
                _groupName = "";
                if (_contexts.Count > 0)
                {
                    _groupName = _contexts[0] != null ? _contexts[0].name : "null";
                    for (int i = 1; i < _contexts.Count; i++)
                    {
                        _groupName += $"; {(_contexts[i] != null ? _contexts[i].name : "null")}";
                    }
                }

                for (int i = 0; i < _uiElements.Count; i++)
                {
                    _uiElements[i].RefreshName();
                }
            }
#endif
            [Serializable]
            private class UiElement
            {
                public enum HideType
                {
                    Movement,
                    Scale
                }

                // For readability in the inspector.
                [SerializeField] private string _name; 
                [SerializeField] private Transform _transform;
                [SerializeField] private HideType _hideType;
                [SerializeField] private Vector3 _hideParameter;
                [SerializeField] private float _duration = 0.5f;
                [SerializeField] private float _hideDurationRatio = 0.5f;
                [SerializeField] private float _hideDelay;
                [SerializeField] private float _showDelay;

                [Tooltip("Set to true to not disable/enable the buttons interactivity when exiting or entering contexts. Useful when this transform's internal state is managed by another context manager")] 
                [SerializeField] private bool _ignoreButtonsInteractability;

                private RectTransform _rectTransform;
                private Vector3 _initialPosition;
                private Vector3 _initialScale;
                private Tween _tween;
                private Button[] _buttons = { };
                private Dictionary<Button, bool> _buttonsSavedState = new ();
                private bool _isShowStarted;
                private bool _isShowFinished;
                private bool _isHideStarted;

                public void Initialize(bool hide)
                {
                    if (_transform is RectTransform rectTransform)
                    {
                        _rectTransform = rectTransform;
                    }

                    _initialPosition = _rectTransform != null ? _rectTransform.anchoredPosition3D : _transform.localPosition;
                    _initialScale = _transform.localScale;

                    if (!_ignoreButtonsInteractability)
                    {
                        _buttons = _transform.GetComponentsInChildren<Button>(true);
                    }

                    SaveButtonsInteractability();

                    if (hide)
                    {
                        Hide(instantaneous: true);
                    }
                }

                public void SetButtonInteractability(Button button, bool interactable)
                {
                    if (!_buttonsSavedState.ContainsKey(button)) return;

                    if (_isShowFinished)
                    {
                        button.interactable = interactable;
                    }
                    else
                    {
                        // The button is hidden or in the way of being shown. We only want this state to be set at the end of the show animation.
                        _buttonsSavedState[button] = interactable;
                    }
                }

                private void SaveButtonsInteractability()
                {
                    for (int i = 0; i < _buttons.Length; i++)
                    {
                        Button button = _buttons[i];
                        if (button != null)
                        {
                            _buttonsSavedState[button] = button.interactable;
                        }
                    }
                }

                public void Hide(bool gameSpeedDependent = false, bool instantaneous = false)
                {
                    if (_isHideStarted || _transform == null)
                    {
                        return;
                    }

                    if (_isShowFinished)
                    {
                        // Save the buttons interactability state as they were in their visible state.
                        SaveButtonsInteractability();
                    }

                    _isShowStarted = false;
                    _isShowFinished = false;
                    _isHideStarted = true;

                    // Kill tween if any
                    KillOngoingTween();

                    switch (_hideType)
                    {
                        case HideType.Movement:
                            HideWithMovement(gameSpeedDependent, instantaneous);
                            break;
                        case HideType.Scale:
                            HideWithScale(gameSpeedDependent, instantaneous);
                            break;
                    }

                    // Make all the buttons non-interactable right away.
                    for (int i = 0; i < _buttons.Length; i++)
                    {
                        Button button = _buttons[i];
                        if (button != null)
                        {
                            button.interactable = false;
                        }
                    }
                }

                private void HideWithMovement(bool gameSpeedDependent = false, bool instantaneous = false)
                {
                    // Move the transform to the hide position.
                    if (instantaneous)
                    {
                        if (_rectTransform != null)
                        {
                            _rectTransform.anchoredPosition = _initialPosition + _hideParameter;
                        }
                        else
                        {
                            _transform.localPosition = _initialPosition + _hideParameter;
                        }
                    }
                    else
                    {
                        // We usually want elements to hide faster that they appear.
                        float hideDuration = _hideDurationRatio * _duration;
                        float timeDivider = gameSpeedDependent ? SpeedMultiplier : 1f;
                        if (_rectTransform != null)
                        {
                            _tween = _rectTransform.DOAnchorPos3D(_initialPosition + _hideParameter, hideDuration / timeDivider).SetEase(Ease.OutQuad).SetDelay(_hideDelay / timeDivider);
                        }
                        else
                        {
                            _tween = _transform.DOLocalMove(_initialPosition + _hideParameter, hideDuration / timeDivider).SetEase(Ease.OutQuad).SetDelay(_hideDelay / timeDivider);
                        }
                    }
                }

                private void HideWithScale(bool gameSpeedDependent = false, bool instantaneous = false)
                {
                    // Scale down to the target scale.
                    if (!instantaneous)
                    {
                        // We usually want elements to hide faster that they appear.
                        float hideDuration = _hideDurationRatio * _duration;
                        float timeDivider = gameSpeedDependent ? SpeedMultiplier : 1f;
                        _tween = _transform.DOScale(_hideParameter, hideDuration / timeDivider).SetEase(Ease.OutQuad).SetDelay(_hideDelay / timeDivider);
                    }
                    else
                    {
                        _transform.localScale = _hideParameter;
                    }
                }

                public void Show(bool gameSpeedDependent = false)
                {
                    if (_isShowStarted || _isShowFinished || _transform == null)
                    {
                        return;
                    }

                    _isShowStarted = true;
                    _isHideStarted = false;

                    // Kill tween if any
                    KillOngoingTween();

                    switch (_hideType)
                    {
                        case HideType.Movement:
                            ShowWithMovement(gameSpeedDependent);
                            break;
                        case HideType.Scale:
                            ShowWithScale(gameSpeedDependent);
                            break;
                    }
                }

                private void ShowWithMovement(bool gameSpeedDependent)
                {
                    float timeDivider = gameSpeedDependent ? SpeedMultiplier : 1f;
                    if (_rectTransform != null)
                    {
                        _tween = _rectTransform.DOAnchorPos3D(_initialPosition, _duration / timeDivider).SetEase(Ease.OutQuad).SetDelay(_showDelay / timeDivider)
                            .OnComplete(OnShowFinished);
                    }
                    else
                    {
                        _tween = _transform.DOLocalMove(_initialPosition, _duration / timeDivider).SetEase(Ease.OutQuad).SetDelay(_showDelay / timeDivider)
                            .OnComplete(OnShowFinished);
                    }
                }

                private void ShowWithScale(bool gameSpeedDependent)
                {
                    float timeDivider = gameSpeedDependent ? SpeedMultiplier : 1f;
                    _tween = _transform.DOScale(_initialScale, _duration / timeDivider).SetEase(Ease.OutBack).SetDelay(_showDelay / timeDivider)
                        .OnComplete(OnShowFinished);
                }

                private void OnShowFinished()
                {
                    // Restore the buttons interactability state.
                    for (int i = 0; i < _buttons.Length; i++)
                    {
                        Button button = _buttons[i];
                        button.interactable = _buttonsSavedState[button];
                    }

                    _isShowFinished = true;
                }

                private void KillOngoingTween()
                {
                    if (_tween != null && _tween.IsActive())
                    {
                        _tween.Kill();
                    }
                }

#if UNITY_EDITOR
                public void RefreshName()
                {
                    _name = _transform != null ? _transform.name : "-";
                }
#endif
            }
        }
    }
}