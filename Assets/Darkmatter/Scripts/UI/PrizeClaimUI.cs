using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The prize form. Reads the two fields and hands them to <see cref="PrizeClaimStore"/>.
///
/// The panel owns its own widgets, the same way the rider selection owns its buttons: the race
/// only has to know when to put this screen up, not what is on it.
///
/// Submit is greyed out until both fields have something in them, and greyed out again once the
/// claim is in. That is the whole of the feedback for now: there is nothing on the panel to
/// write a message into, and a button that visibly cannot be pressed says more than a button
/// that can be pressed and silently does nothing.
/// </summary>
[DisallowMultipleComponent]
public class PrizeClaimUI : MonoBehaviour
{
    [Header("Form")]
    [SerializeField] private TMP_InputField nameField;
    [SerializeField] private TMP_InputField phoneField;

    [Tooltip("Stores what was typed. Wired up here rather than in the inspector, so it cannot " +
             "come unstuck when the button is renamed or the scene is merged.")]
    [SerializeField] private Button submitButton;

    /// <summary>True once this rider's claim is in the file.</summary>
    public bool HasClaimed { get; private set; }

    /// <summary>
    /// What was stored, trimmed and stamped, for whatever puts a reward code up next. Only
    /// meaningful while <see cref="HasClaimed"/> is true.
    /// </summary>
    public PrizeClaim LastClaim { get; private set; }

    private void Awake()
    {
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(Submit);
        }
        else
        {
            Debug.LogError($"{name}: no Submit button assigned, so nothing will store the claim.", this);
        }

        if (nameField == null || phoneField == null)
        {
            Debug.LogError($"{name}: the name and phone number fields both need assigning, or " +
                           "there is nothing to store.", this);
        }

        if (nameField != null)
        {
            nameField.onValueChanged.AddListener(OnFieldChanged);
        }

        if (phoneField != null)
        {
            phoneField.onValueChanged.AddListener(OnFieldChanged);
        }
    }

    /// <summary>
    /// Every time the panel comes up it is a new rider, so the last one's details do not sit
    /// there waiting to be submitted again under someone else's name.
    /// </summary>
    private void OnEnable()
    {
        HasClaimed = false;
        LastClaim = default;

        if (nameField != null)
        {
            nameField.SetTextWithoutNotify(string.Empty);
        }

        if (phoneField != null)
        {
            phoneField.SetTextWithoutNotify(string.Empty);
        }

        Refresh();
    }

    /// <summary>
    /// Stores the claim. Wired to the Submit and Claim button, and safe to call from anywhere
    /// else: a second call once the claim is in does nothing, so a double tap is one entry.
    /// </summary>
    public void Submit()
    {
        if (HasClaimed)
        {
            return;
        }

        string typedName = nameField != null ? nameField.text : string.Empty;
        string typedPhone = phoneField != null ? phoneField.text : string.Empty;

        if (!PrizeClaimStore.Add(typedName, typedPhone, out PrizeClaim stored))
        {
            // Only reachable if something else called this, since the button is not pressable
            // until both fields have something in them.
            Refresh();
            return;
        }

        HasClaimed = true;
        LastClaim = stored;
        Refresh();
    }

    private void OnFieldChanged(string _)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (submitButton == null)
        {
            return;
        }

        submitButton.interactable = !HasClaimed && HasBothFields();
    }

    private bool HasBothFields()
    {
        return nameField != null && !string.IsNullOrWhiteSpace(nameField.text)
               && phoneField != null && !string.IsNullOrWhiteSpace(phoneField.text);
    }
}
