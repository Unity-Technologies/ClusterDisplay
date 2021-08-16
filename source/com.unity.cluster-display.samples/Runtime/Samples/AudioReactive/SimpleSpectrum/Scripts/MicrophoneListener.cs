using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DefaultExecutionOrder(10)]
public class MicrophoneListener : MonoBehaviour
{
    #if UNITY_EDITOR
    [CustomEditor(typeof(MicrophoneListener))]
    public class MicrophoneSpectrumEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var instance = target as MicrophoneListener;
            base.OnInspectorGUI();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Device: ", GUILayout.Width(60));
            instance.selectedDeviceIndex = EditorGUILayout.Popup(instance.selectedDeviceIndex, Microphone.devices);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Play"))
                instance.Play();
        }
    }
    #endif

    [SerializeField][HideInInspector] private int selectedDeviceIndex;
    private string selectedDevice;
    private int currentWindowLengthInSeconds;

    [Tooltip("The size of the window we want to capture before playing it back.")]
    [SerializeField] private int windowLengthInSeconds = 5;

    [Tooltip("The frequency at which we want to record.")]
    [SerializeField] private int windowFequency = 24000;

    [Tooltip("Left or right channel.")]
    [SerializeField] private int channel = 0;

    [Tooltip("The size of our spectrum buffer.")]
    [SerializeField] private int spectrumLength = 256;

    [SerializeField] private AudioSource audioSource;

    private float[] spectrum;
    public float[] Spectrum => spectrum;

    private AudioSource source
    {
        get
        {
            if (audioSource == null)
            {
                audioSource = gameObject.GetComponent<AudioSource>();
                if (audioSource == null)
                    audioSource = gameObject.AddComponent<AudioSource>();
            }

            return audioSource;
        }
    }

    private void StopMicrophone ()
    {
        if (!string.IsNullOrEmpty(selectedDevice))
            return;
        Microphone.End(selectedDevice);
    }

    private void StartMicrophone ()
    {
        currentWindowLengthInSeconds = windowLengthInSeconds;
        try
        {
            source.clip = Microphone.Start(selectedDevice, loop: true, lengthSec: currentWindowLengthInSeconds, windowFequency);

            source.bypassEffects = true;
            source.bypassListenerEffects = true;
            source.bypassReverbZones = true;
            source.loop = true;

            PollSourcePlayState();

        }

        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    // This method waits for the device to begin it's capture window so we can playback the audio as we receive it with lower latency. See:
    // https://support.unity.com/hc/en-us/articles/206485253-How-do-I-get-Unity-to-playback-a-Microphone-input-in-real-time-
    private void WaitForBeggingOfWindow ()
    {
        while (!(Microphone.GetPosition(selectedDevice) > 0)) {}
    }

    private void PollSourcePlayState ()
    {
        if (!source.isPlaying && Microphone.IsRecording(selectedDevice))
        {
            WaitForBeggingOfWindow();
            source.Play();
        }
    }

    private void RestartMicrophone ()
    {
        StopMicrophone();
        StartMicrophone();
    }

    private void Start() => Play();
    private void Play()
    {
        if (selectedDeviceIndex >= Microphone.devices.Length)
            return;

        string device = Microphone.devices[selectedDeviceIndex];

        if (selectedDevice != device)
        {
            StopMicrophone();
            selectedDevice = device;
        }

        StartMicrophone();
    }

    private void Update()
    {
        if (currentWindowLengthInSeconds != windowLengthInSeconds)
            RestartMicrophone();
        PollSourcePlayState();

        if (spectrum == null || spectrum.Length != spectrumLength)
            spectrum = new float[spectrumLength];

        source.GetSpectrumData(spectrum, channel, FFTWindow.BlackmanHarris);
    }
}
