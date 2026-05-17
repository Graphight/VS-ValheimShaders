using HarmonyLib;
using ValheimShaders.Effects;
using ValheimShaders.Patch;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ValheimShaders;

public class ValheimShadersModSystem : ModSystem
{
    public static ValheimShadersModSystem Instance { get; private set; } = null!;
    public ICoreClientAPI ClientApi { get; private set; } = null!;
    public Shaders Shaders { get; private set; } = null!;
    public ShaderPatcher ShaderPatcher { get; private set; } = null!;

    private Harmony? _harmony;
    private ColorGrading? _colorGrading;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI capi)
    {
        Instance = this;
        ClientApi = capi;
        Shaders = new Shaders(capi, Mod.Info.ModID);
        ShaderPatcher = new ShaderPatcher(Mod.Logger);

        ModSettings.Load(capi);

        _harmony = new Harmony(Mod.Info.ModID);
        _harmony.PatchAll();

        _colorGrading = new ColorGrading(capi);

        capi.Input.RegisterHotKey("valheimshaders-toggle", "Toggle ValheimShaders effects", GlKeys.F8, HotkeyType.DevTool);
        capi.Input.SetHotKeyHandler("valheimshaders-toggle", _ =>
        {
            ModSettings.Current.EnableColorGrading = !ModSettings.Current.EnableColorGrading;
            ModSettings.Save(capi);
            capi.ShowChatMessage($"[ValheimShaders] Colour grading: {(ModSettings.Current.EnableColorGrading ? "ON" : "OFF")}");
            return true;
        });

        Mod.Logger.Notification("[ValheimShaders] Loaded — colour grading active. F8 to toggle.");
    }

    public override void Dispose()
    {
        _colorGrading?.Dispose();
        _colorGrading = null;
        _harmony?.UnpatchAll(Mod.Info.ModID);
        Shaders?.Dispose();
        base.Dispose();
    }
}
