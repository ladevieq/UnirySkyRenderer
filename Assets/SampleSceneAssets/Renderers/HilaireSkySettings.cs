using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[VolumeComponentMenu("Sky/Hilaire Sky")]
// SkyUniqueID does not need to be part of built-in HDRP SkyType enumeration.
// This is only provided to track IDs used by HDRP natively.
// You can use any integer value.
[SkyUniqueID(NEW_SKY_UNIQUE_ID)]
public class HilaireSkySettings : SkySettings
{
    const int NEW_SKY_UNIQUE_ID = 20382390;

    [Tooltip("Specify the cubemap HDRP uses to render the sky.")]
    public CubemapParameter hdriSky = new CubemapParameter(null);

    public override Type GetSkyRendererType()
    {
        return typeof(HilaireSkyRenderer);
    }

    public override int GetHashCode()
    {
        int hash = base.GetHashCode();
        unchecked
        {
            hash = hdriSky.value != null ? hash * 23 + hdriSky.GetHashCode() : hash;
        }
        return hash;
    }

    public override int GetHashCode(Camera camera)
    {
        // Implement if your sky depends on the camera settings (like position for instance)
        return GetHashCode();
    }
}
