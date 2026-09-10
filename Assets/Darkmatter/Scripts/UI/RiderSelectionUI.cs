using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class Rider
{
    public Button riderButton;
    public GameObject selectedImage;
}

public class RiderSelectionUI : MonoBehaviour
{
    public const int NoRiderSelected = -1;
    [Header("Riders")] [SerializeField] private List<Rider> riders = new List<Rider>();
    [Header("Race")] [SerializeField] private Button letsRaceButton;

    public int SelectedRiderIndex { get; private set; } = NoRiderSelected;
    public bool HasSelection => SelectedRiderIndex != NoRiderSelected;
    public int RiderCount => riders.Count;


    public Button RaceButton => letsRaceButton;

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