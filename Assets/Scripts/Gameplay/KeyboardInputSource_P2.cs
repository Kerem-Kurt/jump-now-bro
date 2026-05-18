using UnityEngine;
using UnityEngine.InputSystem;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    public class KeyboardInputSource_P2 : MonoBehaviour, IInputSource
    {
        [SerializeField] InputActionAsset actions;

        InputActionMap map;
        InputAction moveAction;
        InputAction jumpAction;
        InputAction dashAction;

        bool jumpPressedFlag;
        bool dashPressedFlag;

        public bool MoveLeft  => moveAction != null && moveAction.ReadValue<Vector2>().x < -0.5f;
        public bool MoveRight => moveAction != null && moveAction.ReadValue<Vector2>().x >  0.5f;
        public bool JumpPressed => jumpPressedFlag;
        public bool JumpHeld    => jumpAction != null && jumpAction.IsPressed();
        public bool DashPressed => dashPressedFlag;

        void Awake()
        {
            map = actions.FindActionMap("Player2", throwIfNotFound: true);
            moveAction = map.FindAction("Move", throwIfNotFound: true);
            jumpAction = map.FindAction("Jump", throwIfNotFound: true);
            dashAction = map.FindAction("Dash", throwIfNotFound: true);
        }

        void OnEnable()
        {
            map.Enable();
            jumpAction.performed += OnJumpPerformed;
            dashAction.performed += OnDashPerformed;
        }

        void OnDisable()
        {
            jumpAction.performed -= OnJumpPerformed;
            dashAction.performed -= OnDashPerformed;
            map.Disable();
        }

        public void Tick()
        {
            jumpPressedFlag = false;
            dashPressedFlag = false;
        }

        void OnJumpPerformed(InputAction.CallbackContext _) => jumpPressedFlag = true;
        void OnDashPerformed(InputAction.CallbackContext _) => dashPressedFlag = true;
    }
}
