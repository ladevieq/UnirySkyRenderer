using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;

class HilaireSkyRenderer : SkyRenderer
{
    string LUTsExportPath = "Assets/LUTs";
    string ShadersPath = "Assets/SampleSceneAssets/Shaders";

    RTHandle transmittanceLUT;
    RTHandle skyviewLUT;

    private ProfilingSampler transmittancePS = new ProfilingSampler("Compute Transmittance");


    readonly Vector2Int transmittanceLUTSize = new Vector2Int(256, 64);
    readonly Vector2Int skyviewLUTSize = new Vector2Int(256, 64);

    GraphicsFormat colorFormat = GraphicsFormat.R32G32B32A32_SFloat;

    ComputeShader transmittancePrecomputationCS;
    ComputeShader skyviewPrecomputationCS;

    public bool UpdateLUTs = true;

    private void AllocateTransmittanceTable()
    {
        transmittanceLUT = RTHandles.Alloc(transmittanceLUTSize.x, transmittanceLUTSize.y,
            colorFormat: colorFormat,
            enableRandomWrite: true,
            name: "TransmittanceTable");

        Debug.Assert(transmittanceLUT != null);
    }

    private void AllocateSkyViewTable()
    {
        skyviewLUT = RTHandles.Alloc(transmittanceLUTSize.x, transmittanceLUTSize.y,
            colorFormat: colorFormat,
            enableRandomWrite: true,
            name: "SkyviewTable");

        Debug.Assert(skyviewLUT != null);
    }

    public override void Build()
    {
        AllocateTransmittanceTable();
        AllocateSkyViewTable();

        transmittancePrecomputationCS = (ComputeShader)AssetDatabase.LoadAssetAtPath<ComputeShader>(Path.Combine(ShadersPath, "TransmittancePrecompute.compute"));
        skyviewPrecomputationCS = (ComputeShader)AssetDatabase.LoadAssetAtPath<ComputeShader>(Path.Combine(ShadersPath, "SkyviewPrecompute.compute"));
    }

    public override void Cleanup()
    {
        // throw new System.NotImplementedException();
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
            cmd.SetComputeTextureParam(transmittancePrecomputationCS, 0, Shader.PropertyToID("Result"), transmittanceLUT);

            cmd.DispatchCompute(
                transmittancePrecomputationCS,
                transmittancePrecomputationCS.FindKernel("CSMain"),
                transmittanceLUTSize.x / 8,
                transmittanceLUTSize.y / 8,
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

    private void ExportLUTs()
    {
        ExportLUT(transmittanceLUT.rt);
        ExportLUT(skyviewLUT.rt);
    }

    private void ExportLUT(RenderTexture rt)
    {
        var path = Path.Combine(LUTsExportPath, rt.name + ".exr");
        if (AssetDatabase.LoadAssetAtPath<Texture>(path))
            return;

        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false);
        var old_rt = RenderTexture.active;
        RenderTexture.active = rt;

        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        var bytes = ImageConversion.EncodeToEXR(tex);

        File.WriteAllBytes(path, bytes);

        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);

        RenderTexture.active = old_rt;
    }
}
