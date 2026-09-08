using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "InputReaderSO", menuName = "Scriptable Objects/InputReaderSO")]
public class InputReaderSO : ScriptableObject,GameInput.IPlayerActions
{
    public void OnMove(InputAction.CallbackContext context)
    {
        
    }
}
