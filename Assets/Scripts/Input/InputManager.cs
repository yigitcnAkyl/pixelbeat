using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RhythmGame
{
    public enum ControlScheme
    {
        KeyboardASKL,   // Standart Ritim Düzeni: A, S, K, L
        Gamepad         // PS/Xbox Kolu: Kare (0), Çarpı (1), Üçgen (2), Daire (3)
    }

    /// <summary>
    /// Unity New Input System tabanlı Kontrol ve Şema Yöneticisi.
    /// Klavyede A, S, K, L düzenini kullanır; Gamepad bağlandığında otomatik geçiş yapar.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Aktif Kontrol Şeması")]
        [SerializeField] private ControlScheme currentScheme = ControlScheme.KeyboardASKL;

        // Eventler (HitDetector ve LaneController dinler)
        public static event Action<int> OnLaneDown;                 // (laneIndex)
        public static event Action<int> OnLaneUp;                   // (laneIndex)
        public static event Action<ControlScheme> OnSchemeChanged;   // (newScheme)

        public ControlScheme CurrentScheme => currentScheme;
        public bool IsGamepadConnected { get; private set; }

        // Şeritlerin anlık basılı tutulma durumu (0-3)
        private bool[] isLanePressed = new bool[4];

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            CheckConnectedDevices();
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            InputSystem.onDeviceChange += HandleDeviceChange;
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            InputSystem.onDeviceChange -= HandleDeviceChange;
#endif
        }

        private void Start()
        {
            CheckConnectedDevices();
        }

        private void Update()
        {
            // Menü açıkken şerit tuşlarını dinleme
            if (MainMenuManager.Instance != null && MainMenuManager.Instance.IsMenuOpen)
            {
                ReleaseAllLanes();
                return;
            }

            PollInputs();
        }

        #region Cihaz Algılama & Şema Seçimi

        private void CheckConnectedDevices()
        {
#if ENABLE_INPUT_SYSTEM
            IsGamepadConnected = Gamepad.current != null;
            if (IsGamepadConnected && currentScheme != ControlScheme.Gamepad)
            {
                SetControlScheme(ControlScheme.Gamepad);
            }
            else if (!IsGamepadConnected && currentScheme == ControlScheme.Gamepad)
            {
                SetControlScheme(ControlScheme.KeyboardASKL);
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad)
            {
                if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
                {
                    IsGamepadConnected = true;
                    SetControlScheme(ControlScheme.Gamepad);
                }
                else if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
                {
                    IsGamepadConnected = Gamepad.current != null;
                    if (!IsGamepadConnected && currentScheme == ControlScheme.Gamepad)
                    {
                        SetControlScheme(ControlScheme.KeyboardASKL);
                    }
                }
            }
        }
#endif

        public void SetControlScheme(ControlScheme newScheme)
        {
            if (currentScheme == newScheme) return;

            ReleaseAllLanes();
            currentScheme = newScheme;
            OnSchemeChanged?.Invoke(currentScheme);
        }

        #endregion

        #region Giriş Dinleme (Polling)

        private void PollInputs()
        {
#if ENABLE_INPUT_SYSTEM
            switch (currentScheme)
            {
                case ControlScheme.Gamepad:
                    PollGamepadInput();
                    break;

                case ControlScheme.KeyboardASKL:
                    PollKeyboardAskl();
                    break;
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void PollGamepadInput()
        {
            var gp = Gamepad.current;
            if (gp == null) return;

            // Lane 0: Kare (PS) / X (Xbox) veya Sol
            CheckButtonState(0, gp.buttonWest.isPressed || gp.dpad.left.isPressed);

            // Lane 1: Çarpı (PS) / A (Xbox) veya Aşağı
            CheckButtonState(1, gp.buttonSouth.isPressed || gp.dpad.down.isPressed);

            // Lane 2: Üçgen (PS) / Y (Xbox) veya Yukarı
            CheckButtonState(2, gp.buttonNorth.isPressed || gp.dpad.up.isPressed);

            // Lane 3: Daire (PS) / B (Xbox) veya Sağ
            CheckButtonState(3, gp.buttonEast.isPressed || gp.dpad.right.isPressed);
        }

        private void PollKeyboardAskl()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            CheckButtonState(0, kb.aKey.isPressed);
            CheckButtonState(1, kb.sKey.isPressed);
            CheckButtonState(2, kb.kKey.isPressed);
            CheckButtonState(3, kb.lKey.isPressed);
        }

        private void CheckButtonState(int laneIndex, bool isPressedNow)
        {
            if (isPressedNow && !isLanePressed[laneIndex])
            {
                isLanePressed[laneIndex] = true;
                TriggerLaneDown(laneIndex);
            }
            else if (!isPressedNow && isLanePressed[laneIndex])
            {
                isLanePressed[laneIndex] = false;
                TriggerLaneUp(laneIndex);
            }
        }
#endif

        private void TriggerLaneDown(int laneIndex)
        {
            OnLaneDown?.Invoke(laneIndex);
            if (LaneController.Instance != null)
            {
                LaneController.Instance.HandleLanePress(laneIndex);
            }
        }

        private void TriggerLaneUp(int laneIndex)
        {
            OnLaneUp?.Invoke(laneIndex);
            if (LaneController.Instance != null)
            {
                LaneController.Instance.HandleLaneRelease(laneIndex);
            }
        }

        private void ReleaseAllLanes()
        {
            for (int i = 0; i < 4; i++)
            {
                if (isLanePressed[i])
                {
                    isLanePressed[i] = false;
                    TriggerLaneUp(i);
                }
            }
        }

        #endregion

        #region Tuş İpuçları (Prompts)

        public string GetLanePrompt(int laneIndex)
        {
            switch (currentScheme)
            {
                case ControlScheme.Gamepad:
                    switch (laneIndex)
                    {
                        case 0: return "□";
                        case 1: return "✕";
                        case 2: return "△";
                        case 3: return "○";
                    }
                    break;

                case ControlScheme.KeyboardASKL:
                    switch (laneIndex)
                    {
                        case 0: return "A";
                        case 1: return "S";
                        case 2: return "K";
                        case 3: return "L";
                    }
                    break;
            }
            return "";
        }

        #endregion
    }
}
