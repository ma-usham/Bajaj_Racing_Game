using UnityEngine.InputSystem;
using UnityEngine;

namespace Darkmatter.Core
{
    [CreateAssetMenu(fileName = "InputReaderSO", menuName = "Scriptable Objects/InputReaderSO")]
    public class InputReaderSO : ScriptableObject, GameInput.IPlayerActions
    {
        public float Steer { get; private set; }
        public bool IsBraking { get; private set; }
        private GameInput gameInput;

        public void Enable()
        {
            if (gameInput == null)
            {
                gameInput = new GameInput();
                gameInput.Player.AddCallbacks(this);
            }

            gameInput.Player.Enable();
        }

        public void Disable()
        {
            Steer = 0f;
            IsBraking = false;

            gameInput?.Player.Disable();
        }

        private void OnDisable()
        {
            Steer = 0f;
            IsBraking = false;

            if (gameInput == null)
            {
                return;
            }

            gameInput.Player.RemoveCallbacks(this);
            gameInput.Player.Disable();
            gameInput.Dispose();
            gameInput = null;
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            Steer = context.ReadValue<float>();
        }

        public void OnBrake(InputAction.CallbackContext context)
        {
            IsBraking = context.ReadValueAsButton();
        }
    }
}
