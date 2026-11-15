using System;
using PixelBeat.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Şerit tuşları (varsayılan S D K L). Basış/bırakış anı input event'inin kendi zaman damgasından
    /// şarkı zamanına çevrilir; kare gecikmesi değerlendirmeye karışmaz.
    /// </summary>
    public class LaneInput : MonoBehaviour
    {
        [SerializeField] private Conductor conductor;

        [SerializeField] private InputAction[] laneActions =
        {
            new InputAction("Lane 0", InputActionType.Button, "<Keyboard>/s"),
            new InputAction("Lane 1", InputActionType.Button, "<Keyboard>/d"),
            new InputAction("Lane 2", InputActionType.Button, "<Keyboard>/k"),
            new InputAction("Lane 3", InputActionType.Button, "<Keyboard>/l"),
        };

        /// <summary>(şerit, şarkı zamanı sn)</summary>
        public event Action<int, double> LanePressed;
        public event Action<int, double> LaneReleased;

        private Action<InputAction.CallbackContext>[] pressHandlers;
        private Action<InputAction.CallbackContext>[] releaseHandlers;

        public int LaneCount => laneActions.Length;

        public bool IsHeld(int lane) => laneActions[lane].IsPressed();

        public string GetKeyLabel(int lane) => laneActions[lane].GetBindingDisplayString();

        private void OnEnable()
        {
            pressHandlers = new Action<InputAction.CallbackContext>[laneActions.Length];
            releaseHandlers = new Action<InputAction.CallbackContext>[laneActions.Length];

            for (int i = 0; i < laneActions.Length; i++)
            {
                int lane = i;
                pressHandlers[i] = ctx => LanePressed?.Invoke(lane, conductor.SongTimeAtRealtime(ctx.time));
                releaseHandlers[i] = ctx => LaneReleased?.Invoke(lane, conductor.SongTimeAtRealtime(ctx.time));
                laneActions[i].performed += pressHandlers[i];
                laneActions[i].canceled += releaseHandlers[i];
                laneActions[i].Enable();
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < laneActions.Length; i++)
            {
                laneActions[i].performed -= pressHandlers[i];
                laneActions[i].canceled -= releaseHandlers[i];
                laneActions[i].Disable();
            }
        }
    }
}
