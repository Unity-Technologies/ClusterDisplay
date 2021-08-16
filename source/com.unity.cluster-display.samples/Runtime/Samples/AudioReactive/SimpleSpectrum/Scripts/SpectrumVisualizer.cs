using System.Collections;
using System.Collections.Generic;
using Unity.ClusterDisplay;
using Unity.ClusterDisplay.RPC;
using Unity.Collections;
using UnityEngine;

[RequireComponent(typeof(MicrophoneListener))]
[ExecuteAlways]
// The MicrophoneListener class is where we get the spectrum from each frame, so inspect that class as well.
public class SpectrumVisualizer : MonoBehaviour
{
    [SerializeField] private Material spectrumMaterial;

    [SerializeField] private string spectrumTextureShaderProperty = "_spectrumTex";
    [SerializeField] private string frequencyShaderProperty = "_frequency";

    [SerializeField] private Texture2D spectrumTexture;
    [SerializeField][HideInInspector] private MicrophoneListener cachedMicrophoneListener;

    private float[] textureBuffer;
    private float[] cachedSpectrum;

    private MicrophoneListener microphoneListener
    {
        get
        {
            if (cachedMicrophoneListener == null)
                cachedMicrophoneListener = GetComponent<MicrophoneListener>();
            return cachedMicrophoneListener;
        }
    }

    [ClusterRPC(RPCExecutionStage.BeforeLateUpdate)]
    public void PropagateSpectrum (float[] spectrum) => cachedSpectrum = spectrum;

    private void LateUpdate()
    {
        if (ClusterDisplayState.IsEmitter)
        {
            if (microphoneListener == null)
                return;

            // Get spectrum data from microphone.
            var spectrum = microphoneListener.Spectrum;
            if (spectrum != null && spectrum.Length > 0)
                PropagateSpectrum(spectrum);
        }

        if (cachedSpectrum == null || cachedSpectrum.Length == 0)
            return;

        // Determine the dimension of a 2D texture that would fit our spectrum array.
        int dimension = Mathf.CeilToInt(Mathf.Sqrt(cachedSpectrum.Length));

        // Make 2D texture that will fit our 1D spectrum array.
        if (spectrumTexture == null || spectrumTexture.width != dimension || spectrumTexture.height != dimension)
        {
            spectrumTexture = new Texture2D(
                width: dimension, 
                height: dimension, 
                textureFormat: TextureFormat.RFloat, // Single channel 32-bit float texture.
                mipChain: false, 
                linear: true);

            spectrumTexture.filterMode = FilterMode.Point; // We don't want any filtering on the float data.
            spectrumTexture.wrapMode = TextureWrapMode.Clamp;
            spectrumTexture.anisoLevel = 1;
        }

        // Make sure our texture buffer is valid and matches the texture dimension.
        if (textureBuffer == null || textureBuffer.Length != dimension * dimension)
            textureBuffer = new float[dimension * dimension];

        if (spectrumMaterial == null)
        {
            Debug.LogError("Missing material.");
            return;
        }

        // Copy spectrum buffer into buffer that fits into the texture.
        System.Array.Copy(cachedSpectrum, textureBuffer, cachedSpectrum.Length);

        // Pack 1D array into 2D texture row by row.
        spectrumTexture.SetPixelData(textureBuffer, 0);
        spectrumTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        // Push spectrum texture data into material.
        spectrumMaterial.SetTexture(spectrumTextureShaderProperty, spectrumTexture);
    }
}
