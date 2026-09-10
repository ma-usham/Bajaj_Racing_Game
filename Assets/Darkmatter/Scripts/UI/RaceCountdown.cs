using System;
using System.Collections;
using Darkmatter.Core;
using TMPro;
using UnityEngine;

namespace Darkmatter.UI
{
    [DisallowMultipleComponent]
    public class RaceCountdown : MonoBehaviour
    {
        [Header("Label")] [Tooltip("Where the numbers are drawn. Switched off between races.")] [SerializeField]
        private TextMeshProUGUI label;

        [Header("Timing")]
        [Tooltip("Counts down from here. 3 gives the usual three, two, one.")]
        [Min(1)]
        [SerializeField]
        private int countFrom = 3;

        [Tooltip("Seconds each number is held for.")] [Min(0.1f)] [SerializeField]
        private float beatSeconds = 1f;

        [Tooltip("Shown in place of zero, when the bike is let go.")] [SerializeField]
        private string goWord = "GO!";

        [Tooltip("Seconds GO stays on screen. The bike is already moving through this, so it " +
                 "wants to be short enough not to sit over the road.")]
        [Min(0f)]
        [SerializeField]
        private float goHold = 0.7f;

        [Tooltip("How much bigger each number starts before it settles. 1 holds it still.")] [Min(1f)] [SerializeField]
        private float beatPunch = 1.5f;

        [Header("Sound")] [Tooltip("Optional. One per number. Needs an AudioManager in the scene.")] [SerializeField]
        private AudioClip countBeep;

        [Tooltip("Optional. Played on GO.")] [SerializeField]
        private AudioClip goBeep;

        private Coroutine running;

        public bool Running => running != null;

        public void Run(Action onGo)
        {
            Stop();
            running = StartCoroutine(Sequence(onGo));
        }

        public void Stop()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }

            Hide();
        }

        private IEnumerator Sequence(Action onGo)
        {
            for (int count = countFrom; count > 0; count--)
            {
                yield return Beat(count.ToString(), countBeep, beatSeconds);
            }

            onGo?.Invoke();

            yield return Beat(goWord, goBeep, goHold);

            Hide();
            running = null;
        }

        private IEnumerator Beat(string word, AudioClip sound, float seconds)
        {
            if (sound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySfx(sound);
            }

            if (label == null)
            {
                yield return new WaitForSeconds(seconds);
                yield break;
            }

            label.gameObject.SetActive(true);
            label.text = word;

            Transform drawn = label.transform;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                float t = seconds > 0f ? Mathf.Clamp01(elapsed / seconds) : 1f;
                drawn.localScale = Vector3.one * Mathf.Lerp(beatPunch, 1f, t * t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            drawn.localScale = Vector3.one;
        }

        private void Hide()
        {
            if (label != null)
            {
                label.transform.localScale = Vector3.one;
                label.gameObject.SetActive(false);
            }
        }
    }
}