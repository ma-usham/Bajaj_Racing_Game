using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the Select Rider panel: exactly one rider can be selected at a time, the
/// selected rider shows its "Selected" badge, and the Let's Race button only becomes
/// interactable once a rider has been picked.
/// </summary>
[DisallowMultipleComponent]
public class RiderSelectionUI : MonoBehaviour
{
    public const int NoRiderSelected = -1;

    [Serializable]
    public class Rider
    {
        [Tooltip("The rider card the player clicks.")]
        public Button riderButton;

        [Tooltip("Badge that is switched on while this rider is the selected one.")]
        public GameObject selectedImage;
    }

    [Header("Riders")]
    [SerializeField] private List<Rider> riders = new List<Rider>();

    [Header("Race")]
    [SerializeField] private Button letsRaceButton;

    /// <summary>Index of the picked rider, or <see cref="NoRiderSelected"/> when none is.</summary>
    public int SelectedRiderIndex { get; private set; } = NoRiderSelected;

    public bool HasSelection => SelectedRiderIndex != NoRiderSelected;

    public int RiderCount => riders.Count;

    private void Awake()
    {
        for (int i = 0; i < riders.Count; i++)
        {
            Button button = riders[i].riderButton;
            if (button == null)
            {
                Debug.LogError($"{name}: rider {i} has no button assigned.", this);
                continue;
            }

            int index = i;
            button.onClick.AddListener(() => SelectRider(index));
        }

        if (letsRaceButton == null)
        {
            Debug.LogError($"{name}: Let's Race button is not assigned.", this);
        }

        Refresh();
    }

    /// <summary>Picks a rider by index. Out of range values clear the selection.</summary>
    public void SelectRider(int index)
    {
        if (index < 0 || index >= riders.Count)
        {
            ClearSelection();
            return;
        }

        if (SelectedRiderIndex == index)
        {
            return;
        }

        SelectedRiderIndex = index;
        Refresh();
    }

    public void ClearSelection()
    {
        if (!HasSelection)
        {
            return;
        }

        SelectedRiderIndex = NoRiderSelected;
        Refresh();
    }

    /// <summary>Pushes the current selection onto the badges and the Let's Race button.</summary>
    private void Refresh()
    {
        for (int i = 0; i < riders.Count; i++)
        {
            GameObject badge = riders[i].selectedImage;
            if (badge != null)
            {
                badge.SetActive(i == SelectedRiderIndex);
            }
        }

        if (letsRaceButton != null)
        {
            letsRaceButton.interactable = HasSelection;
        }
    }
}
