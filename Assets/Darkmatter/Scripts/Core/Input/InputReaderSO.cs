using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the generated <see cref="GameInput"/> asset and exposes the Player map as plain
/// values, so gameplay scripts read input without touching the Input System directly.
///
/// Listening is started and stopped explicitly by the consumer (see <see cref="Enable"/>),
/// not from this asset's own OnEnable. Asset load order is not play mode order, so binding
/// the two leaves the map disabled, or pointing at an asset that Dispose already destroyed,
/// at the moment play starts.
/// </summary>
[CreateAssetMenu(fileName = "InputReaderSO", menuName = "Scriptable Objects/InputReaderSO")]
public class InputReaderSO : ScriptableObject, GameInput.IPlayerActions
{
    /// <summary>Steering axis, -1 (left) to 1 (right).</summary>
    public float Steer { get; private set; }

    /// <summary>True while the brake is held.</summary>
    public bool IsBraking { get; private set; }

    private GameInput gameInput;

    /// <summary>Starts listening. Call from the consuming MonoBehaviour's OnEnable.</summary>
    public void Enable()
    {
        if (gameInput == null)
        {
            gameInput = new GameInput();
            gameInput.Player.AddCallbacks(this);
        }

        gameInput.Player.Enable();
    }

    /// <summary>Stops listening and clears any held input.</summary>
    public void Disable()
    {
        Steer = 0f;
        IsBraking = false;

        gameInput?.Player.Disable();
    }

    /// <summary>
    /// Runs when the asset itself is unloaded (domain reload, leaving play mode). The wrapper
    /// is torn down only here, never in <see cref="Disable"/>, because Dispose destroys the
    /// shared InputActionAsset and every other consumer with it.
    /// </summary>
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
