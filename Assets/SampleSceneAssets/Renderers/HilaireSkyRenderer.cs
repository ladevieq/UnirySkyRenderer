using System;
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

    private ProfilingSampler transmittancePS = new ProfilingSampler("Compute Transmittance");

    enum LUTName
    {
        Transmittance,
        SkyView,
        AerialPerspective,
        MultiScattering,
    };

    public static Vector3Int[] TablesSize = {
        new Vector3Int(256, 64, 1),
        new Vector3Int(200, 100, 1),
        new Vector3Int(32, 32, 32),
        new Vector3Int(32, 32, 1),
    };

    private RTHandle[] LUTs = new RTHandle[(int)Enum.GetValues(typeof(LUTName)).Length];

    GraphicsFormat colorFormat = GraphicsFormat.R32G32B32A32_SFloat;

    ComputeShader transmittancePrecomputationCS;
    ComputeShader skyviewPrecomputationCS;
    ComputeShader multiscatteringPrecomputationCS;

    public bool UpdateLUTs = true;

    private void AllocateTables()
    {
        foreach (var LUTType in Enum.GetValues(typeof(LUTName)))
        {
            var LUTSize = TablesSize[(int)LUTType];
            LUTs[(int)LUTType] = RTHandles.Alloc(LUTSize.x, LUTSize.y, LUTSize.z,
                colorFormat: colorFormat,
                enableRandomWrite: true,
                name: $"{Enum.GetName(typeof(LUTName), LUTType)}Table");

            Debug.Assert(LUTs[(int)LUTType] != null);
        }
    }

    public override void Build()
    {
        AllocateTables();

        transmittancePrecomputationCS = (ComputeShader)AssetDatabase.LoadAssetAtPath<ComputeShader>(Path.Combine(ShadersPath, "TransmittancePrecompute.compute"));
        skyviewPrecomputationCS = (ComputeShader)AssetDatabase.LoadAssetAtPath<ComputeShader>(Path.Combine(ShadersPath, "SkyviewPrecompute.compute"));
        multiscatteringPrecomputationCS = (ComputeShader)AssetDatabase.LoadAssetAtPath<ComputeShader>(Path.Combine(ShadersPath, "MultiScattering.compute"));
    }

    public override void Cleanup()
    {
        foreach (var LUTType in Enum.GetValues(typeof(LUTName)))
        {
            LUTs[(int)LUTType].Release();
        }
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
            cmd.SetComputeTextureParam(transmittancePrecomputationCS, 0, Shader.PropertyToID("Result"), LUTs[(int)LUTName.Transmittance]);

            cmd.DispatchCompute(
                transmittancePrecomputationCS,
                transmittancePrecomputationCS.FindKernel("CSMain"),
                TablesSize[(int)LUTName.Transmittance].x / 8,
                TablesSize[(int)LUTName.Transmittance].y / 8,
                TablesSize[(int)LUTName.Transmittance].z
            );

            cmd.SetComputeTextureParam(multiscatteringPrecomputationCS, 0, Shader.PropertyToID("Result"), LUTs[(int)LUTName.MultiScattering]);

            cmd.DispatchCompute(
                multiscatteringPrecomputationCS,
                multiscatteringPrecomputationCS.FindKernel("CSMain"),
                TablesSize[(int)LUTName.MultiScattering].x / 8,
                TablesSize[(int)LUTName.MultiScattering].y / 8,
                TablesSize[(int)LUTName.MultiScattering].z
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
        foreach (var LUTType in Enum.GetValues(typeof(LUTName)))
        {
            ExportLUT(LUTs[(int)LUTType].rt);
        }
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

        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);

        RenderTexture.active = old_rt;
    }
}
