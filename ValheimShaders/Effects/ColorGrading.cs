using System;
using OpenTK.Graphics.OpenGL;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ValheimShaders.Effects;

public class ColorGrading : IRenderer, IDisposable
{
    public double RenderOrder => 0.35;
    public int RenderRange => 1;

    private readonly ICoreClientAPI _capi;
    private IShaderProgram? _shader;

    // LUT textures — generated in C# so no asset PNG required
    private int _lutNoon;
    private int _lutDusk;
    private int _lutNight;

    // Scene copy texture: CopyTexSubImage2D reads from the currently bound read FBO
    // (Primary) and writes into this texture. We then read it in the LUT shader and
    // draw back to Primary directly — no blit, no FBO format-class restriction.
    private int _sceneCopyTex;
    private int _sceneCopyWidth;
    private int _sceneCopyHeight;

    // Empty VAO required by Core Profile for the gl_VertexID draw call
    private int _emptyVao;

    // Cached GL uniform locations
    private int _locScene;
    private int _locLut;
    private int _locStrength;

    private bool _ready;

    public ColorGrading(ICoreClientAPI capi)
    {
        _capi = capi;

        bool ok = true;
        _shader = ValheimShadersModSystem.Instance.Shaders.Register("colorgrading", ref ok);
        if (!ok || _shader == null)
        {
            capi.Logger.Warning("[ValheimShaders] colorgrading shader failed to compile — colour grading disabled.");
            return;
        }

        int prog = _shader.ProgramId;
        _locScene = GL.GetUniformLocation(prog, "sceneTex");
        _locLut = GL.GetUniformLocation(prog, "lutTex");
        _locStrength = GL.GetUniformLocation(prog, "lutStrength");

        _lutNoon = GenerateLut(LutStyle.Noon);
        _lutDusk = GenerateLut(LutStyle.Dusk);
        _lutNight = GenerateLut(LutStyle.Night);

        _emptyVao = GL.GenVertexArray();

        AllocSceneCopyTex(capi.Render.FrameWidth, capi.Render.FrameHeight);

        _ready = true;

        capi.Event.RegisterRenderer(this, EnumRenderStage.AfterFinalComposition, "valheimshaders-colorgrading");
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (!_ready || !ModSettings.Current.EnableColorGrading) return;

        var render = _capi.Render;
        int w = render.FrameWidth;
        int h = render.FrameHeight;

        if (w != _sceneCopyWidth || h != _sceneCopyHeight)
            AllocSceneCopyTex(w, h);

        // At AfterFinalComposition, Primary is the bound framebuffer for both read and draw.
        // Copy its colour buffer into _sceneCopyTex — no format-class restriction, GL handles conversion.
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _sceneCopyTex);
        GL.CopyTexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, 0, 0, w, h);

        // Guard GL state left over from 3D rendering passes
        render.GLDisableDepthTest();
        render.GlDisableCullFace();
        render.GlToggleBlend(false);

        // Run LUT pass — reads _sceneCopyTex, draws directly into Primary (still bound)
        GL.UseProgram(_shader!.ProgramId);

        GL.Uniform1(_locScene, 0);  // unit 0 = _sceneCopyTex (already bound above)

        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, PickLut((float)_capi.World.Calendar.DayLightStrength));
        GL.Uniform1(_locLut, 1);

        GL.Uniform1(_locStrength, ModSettings.Current.LutStrength);

        GL.ActiveTexture(TextureUnit.Texture0);

        GL.BindVertexArray(_emptyVao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GL.BindVertexArray(0);

        GL.UseProgram(0);
    }

    // Phase 2: simple three-zone LUT selection. Phase 6 will add smooth blending.
    private int PickLut(float dayLight)
    {
        if (dayLight > 0.6f) return _lutNoon;
        if (dayLight > 0.25f) return _lutDusk;
        return _lutNight;
    }

    private void AllocSceneCopyTex(int width, int height)
    {
        if (_sceneCopyTex != 0)
            GL.DeleteTexture(_sceneCopyTex);

        _sceneCopyTex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _sceneCopyTex);
        // RGBA8 is fine — final.fsh already clamps to [0,1] and Primary stores LDR values.
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
            width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2D, 0);

        _sceneCopyWidth = width;
        _sceneCopyHeight = height;
    }

    // -------------------------------------------------------------------------
    // LUT generation — 256×16 RGBA, 16 slices of 16×16.
    // For input (r,g,b): slice = b*15, col = r*15, row = g*15.
    // -------------------------------------------------------------------------

    private enum LutStyle { Noon, Dusk, Night }

    private static int GenerateLut(LutStyle style)
    {
        const int size = 16;
        const int width = size * size; // 256
        const int height = size;       // 16
        byte[] pixels = new byte[width * height * 4];

        for (int sliceB = 0; sliceB < size; sliceB++)
        {
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    float r = col / 15f;
                    float g = row / 15f;
                    float b = sliceB / 15f;

                    (float or, float og, float ob) = TransformColor(r, g, b, style);

                    int px = sliceB * size + col;
                    int py = row;
                    int idx = (py * width + px) * 4;

                    pixels[idx + 0] = ToByte(or);
                    pixels[idx + 1] = ToByte(og);
                    pixels[idx + 2] = ToByte(ob);
                    pixels[idx + 3] = 255;
                }
            }
        }

        int tex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, tex);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
            width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        return tex;
    }

    private static (float r, float g, float b) TransformColor(float r, float g, float b, LutStyle style)
    {
        return style switch
        {
            LutStyle.Noon => ApplyNoon(r, g, b),
            LutStyle.Dusk => ApplyDusk(r, g, b),
            LutStyle.Night => ApplyNight(r, g, b),
            _ => (r, g, b)
        };
    }

    // Noon: strong amber midtones, punchy contrast, cool shadows
    private static (float, float, float) ApplyNoon(float r, float g, float b)
    {
        r = SCurve(r, 0.65f);
        g = SCurve(g, 0.62f);
        b = SCurve(b, 0.55f);

        float mid = MidtoneMask(r, g, b);
        r += mid * 0.18f;
        g += mid * 0.07f;
        b -= mid * 0.14f;

        float shadow = ShadowMask(r, g, b);
        r -= shadow * 0.05f;
        b += shadow * 0.07f;

        return (Clamp01(r), Clamp01(g), Clamp01(b));
    }

    // Dusk: strong amber cast, teal-shifted shadows, lifted blacks
    private static (float, float, float) ApplyDusk(float r, float g, float b)
    {
        float blackLift = 0.05f;
        r = r * (1f - blackLift) + blackLift;
        g = g * (1f - blackLift) + blackLift;
        b = b * (1f - blackLift) + blackLift;

        float shadow = ShadowMask(r, g, b);
        r -= shadow * 0.07f;
        g += shadow * 0.03f;
        b += shadow * 0.10f;

        float mid = MidtoneMask(r, g, b);
        r += mid * 0.13f;
        g += mid * 0.06f;
        b -= mid * 0.10f;

        float luma = Luma(r, g, b);
        r = Lerp(luma, r, 0.80f);
        g = Lerp(luma, g, 0.80f);
        b = Lerp(luma, b, 0.80f);

        return (Clamp01(r), Clamp01(g), Clamp01(b));
    }

    // Night: heavily desaturated blue-grey, lifted blacks
    private static (float, float, float) ApplyNight(float r, float g, float b)
    {
        float blackLift = 0.05f;
        r = r * (1f - blackLift) + blackLift;
        g = g * (1f - blackLift) + blackLift;
        b = b * (1f - blackLift) + blackLift;

        float luma = Luma(r, g, b);
        r = Lerp(luma, r, 0.50f);
        g = Lerp(luma, g, 0.53f);
        b = Lerp(luma, b, 0.50f);

        r -= 0.05f;
        b += 0.08f;

        float shadow = ShadowMask(r, g, b);
        b += shadow * 0.06f;

        return (Clamp01(r), Clamp01(g), Clamp01(b));
    }

    // pivot > 0.5 → lifted shadows, more contrast
    private static float SCurve(float x, float pivot)
    {
        if (x <= pivot)
            return pivot * (x / pivot) * (x / pivot);
        float t = (x - pivot) / (1f - pivot);
        return pivot + (1f - pivot) * (1f - (1f - t) * (1f - t));
    }

    private static float Luma(float r, float g, float b) => r * 0.299f + g * 0.587f + b * 0.114f;
    private static float MidtoneMask(float r, float g, float b) { float l = Luma(r, g, b); return 4f * l * (1f - l); }
    private static float ShadowMask(float r, float g, float b) => 1f - Luma(r, g, b);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    private static byte ToByte(float v) => (byte)(Clamp01(v) * 255f + 0.5f);

    public void Dispose()
    {
        if (_ready)
        {
            _capi.Event.UnregisterRenderer(this, EnumRenderStage.AfterFinalComposition);
            _ready = false;
        }

        if (_emptyVao != 0) { GL.DeleteVertexArray(_emptyVao); _emptyVao = 0; }
        if (_sceneCopyTex != 0) { GL.DeleteTexture(_sceneCopyTex); _sceneCopyTex = 0; }
        if (_lutNoon != 0) GL.DeleteTexture(_lutNoon);
        if (_lutDusk != 0) GL.DeleteTexture(_lutDusk);
        if (_lutNight != 0) GL.DeleteTexture(_lutNight);
        _lutNoon = _lutDusk = _lutNight = 0;

        _shader?.Dispose();
        _shader = null;
    }
}
