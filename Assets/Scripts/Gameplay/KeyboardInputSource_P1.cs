using UnityEngine;
using UnityEngine.InputSystem;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    public class KeyboardInputSource_P1 : MonoBehaviour, IInputSource
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

        void OnEnable() => Bind("Player1");

        void OnDisable() => Unbind();

        /// Switch to a different action map at runtime. Networked play points the local source at the shared
        /// "NetPlayer" layout so both players use the same keys; solo leaves the split Player1/Player2 maps.
        /// Safe after OnEnable: it tears the current map down first.
        public void Rebind(string mapName) => Bind(mapName);

        void Bind(string mapName)
        {
            Unbind();
            map = actions.FindActionMap(mapName, throwIfNotFound: true);
            moveAction = map.FindAction("Move", throwIfNotFound: true);
            jumpAction = map.FindAction("Jump", throwIfNotFound: true);
            dashAction = map.FindAction("Dash", throwIfNotFound: true);

            map.Enable();
            jumpAction.performed += OnJumpPerformed;
            dashAction.performed += OnDashPerformed;
        }

        void Unbind()
        {
            if (jumpAction != null) jumpAction.performed -= OnJumpPerformed;
            if (dashAction != null) dashAction.performed -= OnDashPerformed;
            if (map != null) map.Disable();
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
