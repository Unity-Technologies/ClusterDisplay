using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class PlayerInputHandler : MonoBehaviour
    {
        [Tooltip("Sensitivity multiplier for moving the camera around")]
        public float LookSensitivity = 1f;

        [Tooltip("Additional sensitivity multiplier for WebGL")]
        public float WebglLookSensitivityMultiplier = 0.25f;

        [Tooltip("Limit to consider an input when using a trigger on a controller")]
        public float TriggerAxisThreshold = 0.4f;

        [Tooltip("Used to flip the vertical input axis")]
        public bool InvertYAxis = false;

        [Tooltip("Used to flip the horizontal input axis")]
        public bool InvertXAxis = false;

        GameFlowManager m_GameFlowManager;
        PlayerCharacterController m_PlayerCharacterController;
        bool m_FireInputWasHeld;

        void Start()
        {
            m_PlayerCharacterController = GetComponent<PlayerCharacterController>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerCharacterController, PlayerInputHandler>(
                m_PlayerCharacterController, this, gameObject);
            m_GameFlowManager = FindObjectOfType<GameFlowManager>();
            DebugUtility.HandleErrorIfNullFindObject<GameFlowManager, PlayerInputHandler>(m_GameFlowManager, this);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void LateUpdate()
        {
            m_FireInputWasHeld = GetFireInputHeld();
        }

        public bool CanProcessInput()
        {
            return Cursor.lockState == CursorLockMode.Locked && !m_GameFlowManager.GameIsEnding;
        }

        public Vector3 GetMoveInput()
        {
            if (CanProcessInput())
            {
                Vector3 move = new Vector3(Input.GetAxisRaw(GameConstants.k_AxisNameHorizontal), 0f,
                    Input.GetAxisRaw(GameConstants.k_AxisNameVertical));

                // constrain move input to a maximum magnitude of 1, otherwise diagonal movement might exceed the max move speed defined
                move = Vector3.ClampMagnitude(move, 1);

                return move;
            }

            return Vector3.zero;
        }

        public float GetLookInputsHorizontal() =>
            GetLookAxis(GameConstants.k_MouseAxisNameHorizontal);

        public float GetLookInputsVertical() =>
            -GetLookAxis(GameConstants.k_MouseAxisNameVertical);

        public bool GetJumpInputDown()
        {
            if (CanProcessInput())
                return Input.GetKeyDown(KeyCode.Space);

            return false;
        }

        public bool GetJumpInputHeld()
        {
            if (CanProcessInput())
                return Input.GetKey(KeyCode.Space);

            return false;
        }

        public bool GetFireInputDown()
        {
            return GetFireInputHeld() && !m_FireInputWasHeld;
        }

        public bool GetFireInputReleased()
        {
            return !GetFireInputHeld() && m_FireInputWasHeld;
        }

        public bool GetFireInputHeld()
        {
            if (CanProcessInput())
                return Input.GetMouseButton(0);

            return false;
        }

        public bool GetAimInputHeld()
        {
            if (CanProcessInput())
                return Input.GetMouseButton(1);
            return false;
        }

        public bool GetSprintInputHeld()
        {
            if (CanProcessInput())
                return Input.GetKey(KeyCode.LeftShift);

            return false;
        }

        public bool GetCrouchInputDown()
        {
            if (CanProcessInput())
                return Input.GetKeyDown(KeyCode.LeftControl);

            return false;
        }

        public bool GetCrouchInputReleased()
        {
            if (CanProcessInput())
                return Input.GetKeyUp(KeyCode.LeftControl);

            return false;
        }

        public bool GetReloadButtonDown()
        {
            if (CanProcessInput())
                return Input.GetKeyDown(KeyCode.R);

            return false;
        }

        public int GetSwitchWeaponInput()
        {
            if (CanProcessInput())
            {
                if (Input.GetAxis(GameConstants.k_ButtonNameSwitchWeapon) > 0f)
                    return -1;
                else if (Input.GetAxis(GameConstants.k_ButtonNameSwitchWeapon) < 0f)
                    return 1;
            }

            return 0;
        }

        public int GetSelectWeaponInput()
        {
            if (CanProcessInput())
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                    return 1;
                else if (Input.GetKeyDown(KeyCode.Alpha2))
                    return 2;
                else if (Input.GetKeyDown(KeyCode.Alpha3))
                    return 3;
                else if (Input.GetKeyDown(KeyCode.Alpha4))
                    return 4;
                else if (Input.GetKeyDown(KeyCode.Alpha5))
                    return 5;
                else if (Input.GetKeyDown(KeyCode.Alpha6))
                    return 6;
                else if (Input.GetKeyDown(KeyCode.Alpha7))
                    return 7;
                else if (Input.GetKeyDown(KeyCode.Alpha8))
                    return 8;
                else if (Input.GetKeyDown(KeyCode.Alpha9))
                    return 9;
                else
                    return 0;
            }

            return 0;
        }

        float GetLookAxis(string lookAxisName)
        {
            if (CanProcessInput())
            {
                // Check if this look input is coming from the mouse
                float i = Input.GetAxisRaw(lookAxisName);

                // apply sensitivity multiplier
                i *= LookSensitivity;

                // reduce mouse input amount to be equivalent to stick movement
                i *= 0.01f;

                return i;
            }

            return 0f;
        }
    }
}