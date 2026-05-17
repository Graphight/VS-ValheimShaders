using System;
using Vintagestory.API.Client;

namespace ValheimShaders;

public class Shaders : IDisposable
{
    private readonly ICoreClientAPI _capi;
    private readonly string _modId;
    private MeshRef? _screenQuad;

    public Shaders(ICoreClientAPI capi, string modId)
    {
        _capi = capi;
        _modId = modId;
    }

    // Registers a shader by name, loading .vsh/.fsh from assets/<modId>/shaders/<name>.
    public IShaderProgram Register(string name, ref bool success)
    {
        var prog = _capi.Shader.NewShaderProgram();
        prog.AssetDomain = _modId;
        _capi.Shader.RegisterFileShaderProgram(name, prog);
        if (!prog.Compile()) success = false;
        return prog;
    }

    // Renders a fullscreen quad in NDC space. Expects a shader to already be active.
    public void RenderFullscreenQuad()
    {
        _screenQuad ??= CreateScreenQuad();
        _capi.Render.RenderMesh(_screenQuad);
    }

    private MeshRef CreateScreenQuad()
    {
        var mesh = QuadMeshUtil.GetCustomQuadModelData(-1f, -1f, 0f, 2f, 2f);
        mesh.Normals = null;
        mesh.Rgba = null;
        return _capi.Render.UploadMesh(mesh);
    }

    public void Dispose()
    {
        _screenQuad?.Dispose();
        _screenQuad = null;
    }
}
