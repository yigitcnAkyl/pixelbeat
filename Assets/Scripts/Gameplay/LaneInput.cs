using System;
using System.Collections.Generic;
using PixelBeat.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Şerit girişi: dokunmatik (EnhancedTouch, multi-touch, her parmak ayrı) + Editor testi için tuşlar (S D K L).
    /// Basış/bırakış anı input event'inin kendi zaman damgasından şarkı zamanına çevrilir; kare gecikmesi değerlendirmeye karışmaz.
    /// GDD §2: parmak basılıyken başka şeride kayarsa eski şerit bırakılır, yeni şeride basış sayılmaz.
    /// </summary>
    public class LaneInput : MonoBehaviour
    {
        [SerializeField] private Conductor conductor;
        [SerializeField] private Playfield playfield;
        [SerializeField] private ScreenLayout screenLayout;

        [SerializeField] private InputAction[] laneActions =
        {
            new InputAction("Lane 0", InputActionType.Button, "<Keyboard>/s"),
            new InputAction("Lane 1", InputActionType.Button, "<Keyboard>/d"),
            new InputAction("Lane 2", InputActionType.Button, "<Keyboard>/k"),
            new InputAction("Lane 3", InputActionType.Button, "<Keyboard>/l"),
        };

        [Tooltip("Editor'de fare tıklaması dokunuş gibi sayılır (TouchSimulation). Unity Remote ile birlikte de çalışır.")]
        [SerializeField] private bool simulateTouchWithMouseInEditor = true;

        /// <summary>(şerit, şarkı zamanı sn)</summary>
        public event Action<int, double> LanePressed;
        public event Action<int, double> LaneReleased;

        /// <summary>Her yeni dokunuş (nerede olursa olsun): (oyun pikseli konum, şarkı zamanı sn). Butonlar ve kalibrasyon için.</summary>
        public event Action<Vector2, double> TouchStarted;

        /// <summary>false iken dokunuşlar şerit basışı üretmez (kalibrasyon ekranı). Tuşlar etkilenmez.</summary>
        public bool LaneTouchesEnabled { get; set; } = true;

        private readonly Dictionary<Finger, int> fingerLanes = new Dictionary<Finger, int>();
        private int[] touchCounts;
        private Action<InputAction.CallbackContext>[] pressHandlers;
        private Action<InputAction.CallbackContext>[] releaseHandlers;
        private bool simulationEnabledByUs;

        public int LaneCount => laneActions.Length;

        public bool IsHeld(int lane) => touchCounts[lane] > 0 || laneActions[lane].IsPressed();

        public string GetKeyLabel(int lane) => laneActions[lane].GetBindingDisplayString();

        private void Awake()
        {
            touchCounts = new int[laneActions.Length];
        }

        private void OnEnable()
        {
            pressHandlers = new Action<InputAction.CallbackContext>[laneActions.Length];
            releaseHandlers = new Action<InputAction.CallbackContext>[laneActions.Length];

            for (int i = 0; i < laneActions.Length; i++)
            {
                int lane = i;
                pressHandlers[i] = ctx => LanePressed?.Invoke(lane, conductor.SongTimeAtRealtime(ctx.time));
                releaseHandlers[i] = ctx =>
                {
                    if (touchCounts[lane] == 0) LaneReleased?.Invoke(lane, conductor.SongTimeAtRealtime(ctx.time));
                };
                laneActions[i].performed += pressHandlers[i];
                laneActions[i].canceled += releaseHandlers[i];
                laneActions[i].Enable();
            }

            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            if (simulateTouchWithMouseInEditor && !TouchSimulation.instance)
            {
                TouchSimulation.Enable();
                simulationEnabledByUs = true;
            }
#endif
            ETouch.onFingerDown += OnFingerDown;
            ETouch.onFingerMove += OnFingerMove;
            ETouch.onFingerUp += OnFingerUp;
        }

        private void OnDisable()
        {
            for (int i = 0; i < laneActions.Length; i++)
            {
                laneActions[i].performed -= pressHandlers[i];
                laneActions[i].canceled -= releaseHandlers[i];
                laneActions[i].Disable();
            }

            ETouch.onFingerDown -= OnFingerDown;
            ETouch.onFingerMove -= OnFingerMove;
            ETouch.onFingerUp -= OnFingerUp;
            if (simulationEnabledByUs)
            {
                TouchSimulation.Disable();
                simulationEnabledByUs = false;
            }
            EnhancedTouchSupport.Disable();

            fingerLanes.Clear();
            Array.Clear(touchCounts, 0, touchCounts.Length);
        }

        private void OnFingerDown(Finger finger)
        {
            var touch = finger.currentTouch;
            double songTime = conductor.SongTimeAtRealtime(touch.time);
            Vector2 px = screenLayout.ScreenToPx(touch.screenPosition);

            TouchStarted?.Invoke(px, songTime);

            if (!LaneTouchesEnabled || px.y >= playfield.TouchZoneTopYPx) return;

            int lane = playfield.LaneAtPx(px.x);
            fingerLanes[finger] = lane;
            touchCounts[lane]++;
            LanePressed?.Invoke(lane, songTime);
        }

        private void OnFingerMove(Finger finger)
        {
            if (!fingerLanes.TryGetValue(finger, out int lane) || lane < 0) return;

            var touch = finger.currentTouch;
            int now = playfield.LaneAtPx(screenLayout.ScreenToPx(touch.screenPosition).x);
            if (now == lane) return;

            // Başka şeride kaydı: eski şerit bırakılır, bu parmak kalkana kadar yeni basış üretmez.
            fingerLanes[finger] = -1;
            ReleaseTouch(lane, touch.time);
        }

        private void OnFingerUp(Finger finger)
        {
            if (!fingerLanes.TryGetValue(finger, out int lane)) return;
            fingerLanes.Remove(finger);
            if (lane >= 0) ReleaseTouch(lane, finger.currentTouch.time);
        }

        private void ReleaseTouch(int lane, double realtime)
        {
            touchCounts[lane] = Mathf.Max(0, touchCounts[lane] - 1);
            if (touchCounts[lane] == 0 && !laneActions[lane].IsPressed())
                LaneReleased?.Invoke(lane, conductor.SongTimeAtRealtime(realtime));
        }
    }
}
