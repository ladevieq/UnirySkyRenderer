using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;

class HilaireSkyRenderer : SkyRenderer
{
    RTHandle transmitanceLUT;

    GraphicsFormat s_ColorFormat = GraphicsFormat.R16G16B16A16_SFloat;

    ComputeShader s_TransmittancePrecomputationCS;

    bool firstUpdate = true;

    // Material m_NewSkyMaterial; // Renders a cubemap into a render texture (can be cube or 2D)
    // MaterialPropertyBlock m_PropertyBlock = new MaterialPropertyBlock();

    // private static int m_RenderCubemapID = 0; // FragBaking
    // private static int m_RenderFullscreenSkyID = 1; // FragRender
    private RTHandle AllocateTransmittanceTable() {
        var table = RTHandles.Alloc(64, 256,
            colorFormat: s_ColorFormat,
            enableRandomWrite: true,
            name: "TransmittanceTable");

        Debug.Assert(table != null);

        return table;
    }

    public override void Build()
    {
        transmitanceLUT = AllocateTransmittanceTable();

        s_TransmittancePrecomputationCS = (ComputeShader)Resources.Load("TransmittancePrecompute");

        // s_TransmittancePrecomputationCS.SetTexture(Shader.PropertyToID("Result"), transmitanceLUT);
    }

    // Project dependent way to retrieve a shader.
    // Shader GetNewSkyShader()
    // {
    //     // Implement me
    //     return null;
    // }

    public override void Cleanup()
    {
        // CoreUtils.Destroy(m_NewSkyMaterial);
    }

    protected override bool Update(BuiltinSkyParameters builtinParams)
    {
        var cmd = builtinParams.commandBuffer;

        if (firstUpdate) {
            firstUpdate = false;

            cmd.SetComputeTextureParam(s_TransmittancePrecomputationCS, 0, Shader.PropertyToID("Result"), transmitanceLUT);

            cmd.DispatchCompute(
                s_TransmittancePrecomputationCS,
                s_TransmittancePrecomputationCS.FindKernel("CSMain"),
                64 / 8,
                256 / 8,
                1
            );

            Texture2D tex = new Texture2D(64, 256, TextureFormat.RGB24, false);
            var old_rt = RenderTexture.active;
            RenderTexture.active = transmitanceLUT.rt;

            tex.ReadPixels(new Rect(0, 0, 64, 256), 0, 0);
            tex.Apply();

            var bytes = ImageConversion.EncodeToPNG(tex);
            // Object.Destroy(tex);

            string path = Application.dataPath + "/transmittance.png";
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset("Assets/transmittance.png");
            RenderTexture.active = old_rt;
        }

        return false;
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
}
