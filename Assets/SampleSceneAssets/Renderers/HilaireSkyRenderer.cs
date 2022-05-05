using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;

class HilaireSkyRenderer : SkyRenderer
{
    RTHandle transmitanceLUT;
    string transmittanceLUTPath = "Assets/transmittance.exr";

    private ProfilingSampler transmittancePS = new ProfilingSampler("Compute Transmittance");


    readonly Vector2Int transmitanceLUTSize = new Vector2Int(256, 64);

    GraphicsFormat s_ColorFormat = GraphicsFormat.R32G32B32A32_SFloat;

    ComputeShader s_TransmittancePrecomputationCS;

    public bool UpdateLUTs = true;

    // Material m_NewSkyMaterial; // Renders a cubemap into a render texture (can be cube or 2D)
    // MaterialPropertyBlock m_PropertyBlock = new MaterialPropertyBlock();

    // private static int m_RenderCubemapID = 0; // FragBaking
    // private static int m_RenderFullscreenSkyID = 1; // FragRender
    private void AllocateTransmittanceTable()
    {
        transmitanceLUT = RTHandles.Alloc(transmitanceLUTSize.x, transmitanceLUTSize.y,
            colorFormat: s_ColorFormat,
            enableRandomWrite: true,
            name: "TransmittanceTable");

        Debug.Assert(transmitanceLUT != null);
    }

    public override void Build()
    {
        AllocateTransmittanceTable();

        s_TransmittancePrecomputationCS = (ComputeShader)AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/SampleSceneAssets/Shaders/TransmittancePrecompute.compute");
    }

    public override void Cleanup()
    {
        throw new System.NotImplementedException();
    }

    // Project dependent way to retrieve a shader.
    // Shader GetNewSkyShader()
    // {
    //     // Implement me
    //     return null;
    // }

    public override bool RequiresPreRenderSky(BuiltinSkyParameters builtinParams)
    {
        return UpdateLUTs;
    }

    public override void PreRenderSky(BuiltinSkyParameters builtinParams)
    {
        var cmd = builtinParams.commandBuffer;
        using (new ProfilingScope(cmd, transmittancePS))
        {
            cmd.SetComputeTextureParam(s_TransmittancePrecomputationCS, 0, Shader.PropertyToID("Result"), transmitanceLUT);

            cmd.DispatchCompute(
                s_TransmittancePrecomputationCS,
                s_TransmittancePrecomputationCS.FindKernel("CSMain"),
                transmitanceLUTSize.x / 8,
                transmitanceLUTSize.y / 8,
                1
            );
        }

        ExportLUTs();
        // UpdateLUTs = false;
    }

    public override void RenderSky(BuiltinSkyParameters builtinParams, bool renderForCubemap, bool renderSunDisk)
    {
        // using (new ProfilingSample(builtinParams.commandBuffer, "Draw sky"))
        // {
        //     var newSky = builtinParams.skySettings as HilaireSkySettings;

        //     int passID = renderForCubemap ? m_RenderCubemapID : m_RenderFullscreenSkyID;

        //     float intensity = GetSkyIntensity(newSky, builtinParams.debugSettings);
        //     float phi = -Mathf.Deg2Rad * newSky.rotation.value; // -rotation to match Legacy
        //     m_PropertyBlock.SetTexture(_Cubemap, newSky.hdriSky.value);
        //     m_PropertyBlock.SetVector(_SkyParam, new Vector4(intensity, 0.0f, Mathf.Cos(phi), Mathf.Sin(phi)));
        //     m_PropertyBlock.SetMatrix(_PixelCoordToViewDirWS, builtinParams.pixelCoordToViewDirMatrix);

        //     CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_NewSkyMaterial, m_PropertyBlock, passID);
        // }
    }

    // TODO: Access sky renderer in order to make this static
    public void ExportLUTs()
    {
        if (AssetDatabase.LoadAssetAtPath<Texture>(transmittanceLUTPath))
        {
            return;
        }
        var transmitanceRT = transmitanceLUT.rt;
        Texture2D tex = new Texture2D(transmitanceRT.width, transmitanceRT.height, TextureFormat.RGBAFloat, false);
        var old_rt = RenderTexture.active;
        RenderTexture.active = transmitanceRT;

        tex.ReadPixels(new Rect(0, 0, transmitanceRT.width, transmitanceRT.height), 0, 0);
        tex.Apply();

        var bytes = ImageConversion.EncodeToEXR(tex);
        Object.DestroyImmediate(tex);

        File.WriteAllBytes(transmittanceLUTPath, bytes);

        Texture exr = AssetDatabase.LoadAssetAtPath<Texture>(transmittanceLUTPath);
        AssetDatabase.Refresh();
        EditorUtility.SetDirty(exr);
        AssetDatabase.ImportAsset(transmittanceLUTPath);
        RenderTexture.active = old_rt;
    }
}
