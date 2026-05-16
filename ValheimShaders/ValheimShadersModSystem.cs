using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ValheimShaders;

public class ValheimShadersModSystem : ModSystem
{
    public static ValheimShadersModSystem Instance { get; private set; } = null!;

    private Harmony? _harmony;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI capi)
    {
        Instance = this;

        _harmony = new Harmony(Mod.Info.ModID);
        _harmony.PatchAll();

        Mod.Logger.Notification("[ValheimShaders] Loaded.");
    }

    public override void Dispose()
    {
        _harmony?.UnpatchAll(Mod.Info.ModID);
        base.Dispose();
    }
}
