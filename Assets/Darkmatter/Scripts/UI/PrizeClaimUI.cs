using TMPro;
using UnityEngine;
using UnityEngine.UI;


[DisallowMultipleComponent]
public class PrizeClaimUI : MonoBehaviour
{
    [Header("Form")] [SerializeField] private TMP_InputField nameField;
    [SerializeField] private TMP_InputField phoneField;

    [Tooltip("Stores what was typed. Wired up here rather than in the inspector, so it cannot " +
             "come unstuck when the button is renamed or the scene is merged.")]
    [SerializeField]
    private Button submitButton;


    public bool HasClaimed { get; private set; }


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