using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class ObjectiveToast : MonoBehaviour
    {
        [Header("References")] [Tooltip("Text content that will display the title")]
        public TMPro.TextMeshProUGUI TitleTextContent;

        [Tooltip("Text content that will display the description")]
        public TMPro.TextMeshProUGUI DescriptionTextContent;

        [Tooltip("Text content that will display the counter")]
        public TMPro.TextMeshProUGUI CounterTextContent;

        [Tooltip("Rect that will display the description")]
        public RectTransform SubTitleRect;

        [Tooltip("Canvas used to fade in and out the content")]
        public CanvasGroup CanvasGroup;

        [Tooltip("Layout group containing the objective")]
        public HorizontalOrVerticalLayoutGroup LayoutGroup;

        [Header("Transitions")] [Tooltip("Delay before moving complete")]
        public float CompletionDelay;

        [Tooltip("Duration of the fade in")] public float FadeInDuration = 0.5f;
        [Tooltip("Duration of the fade out")] public float FadeOutDuration = 2f;

        [Header("Sound")] [Tooltip("Sound that will be player on initialization")]
        public AudioClip InitSound;

        [Tooltip("Sound that will be player on completion")]
        public AudioClip CompletedSound;

        [Header("Movement")] [Tooltip("Time it takes to move in the screen")]
        public float MoveInDuration = 0.5f;

        [Tooltip("Animation curve for move in, position in x over time")]
        public AnimationCurve MoveInCurve;

        [Tooltip("Time it takes to move out of the screen")]
        public float MoveOutDuration = 2f;

        [Tooltip("Animation curve for move out, position in x over time")]
        public AnimationCurve MoveOutCurve;

        float m_StartFadeTime;
        bool m_IsFadingIn;
        bool m_IsFadingOut;
        bool m_IsMovingIn;
        bool m_IsMovingOut;
        AudioSource m_AudioSource;
        RectTransform m_RectTransform;

        public void Initialize(string titleText, string descText, string counterText, bool isOptionnal, float delay)
        {
            // set the description for the objective, and forces the content size fitter to be recalculated
            Canvas.ForceUpdateCanvases();

            TitleTextContent.text = titleText;
            DescriptionTextContent.text = descText;
            CounterTextContent.text = counterText;

            if (GetComponent<RectTransform>())
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
            }

            m_StartFadeTime = Time.time + delay;
            // start the fade in
            m_IsFadingIn = true;
            m_IsMovingIn = true;
        }

        public void Complete()
        {
            m_StartFadeTime = Time.time + CompletionDelay;
            m_IsFadingIn = false;
            m_IsMovingIn = false;

            // if a sound was set, play it
            PlaySound(CompletedSound);

            // start the fade out
            m_IsFadingOut = true;
            m_IsMovingOut = true;
        }

        void Update()
        {
            float timeSinceFadeStarted = Time.time - m_StartFadeTime;

            SubTitleRect.gameObject.SetActive(!string.IsNullOrEmpty(DescriptionTextContent.text));

            if (m_IsFadingIn && !m_IsFadingOut)
            {
                // fade in
                if (timeSinceFadeStarted < FadeInDuration)
                {
                    // calculate alpha ratio
                    CanvasGroup.alpha = timeSinceFadeStarted / FadeInDuration;
                }
                else
                {
                    CanvasGroup.alpha = 1f;
                    // end the fade in
                    m_IsFadingIn = false;

                    PlaySound(InitSound);
                }
            }

            if (m_IsMovingIn && !m_IsMovingOut)
            {
                // move in
                if (timeSinceFadeStarted < MoveInDuration)
                {
                    LayoutGroup.padding.left = (int) MoveInCurve.Evaluate(timeSinceFadeStarted / MoveInDuration);

                    if (GetComponent<RectTransform>())
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
                    }
                }
                else
                {
                    // making sure the position is exact
                    LayoutGroup.padding.left = 0;

                    if (GetComponent<RectTransform>())
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
                    }

                    m_IsMovingIn = false;
                }

            }

            if (m_IsFadingOut)
            {
                // fade out
                if (timeSinceFadeStarted < FadeOutDuration)
                {
                    // calculate alpha ratio
                    CanvasGroup.alpha = 1 - (timeSinceFadeStarted) / FadeOutDuration;
                }
                else
                {
                    CanvasGroup.alpha = 0f;

                    // end the fade out, then destroy the object
                    m_IsFadingOut = false;
                    Destroy(gameObject);
                }
            }

            if (m_IsMovingOut)
            {
                // move out
                if (timeSinceFadeStarted < MoveOutDuration)
                {
                    LayoutGroup.padding.left = (int) MoveOutCurve.Evaluate(timeSinceFadeStarted / MoveOutDuration);

                    if (GetComponent<RectTransform>())
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
                    }
                }
                else
                {
                    m_IsMovingOut = false;
                }
            }
        }

        void PlaySound(AudioClip sound)
        {
            if (!sound)
                return;

            if (!m_AudioSource)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
                m_AudioSource.outputAudioMixerGroup = AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.HUDObjective);
            }

            m_AudioSource.PlayOneShot(sound);
        }
    }
}