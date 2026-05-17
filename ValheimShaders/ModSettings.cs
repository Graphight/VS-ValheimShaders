using Newtonsoft.Json;
using Vintagestory.API.Client;

namespace ValheimShaders;

public class ModSettings
{
    public static ModSettings Current { get; private set; } = new();

    private volatile bool _enableColorGrading = true;
    public bool EnableColorGrading
    {
        get => _enableColorGrading;
        set => _enableColorGrading = value;
    }

    public float LutStrength { get; set; } = 0.85f;

    private static string SettingsPath(ICoreClientAPI capi) =>
        System.IO.Path.Combine(capi.GetOrCreateDataPath("ModConfig"), "valheimshaders.json");

    public static void Load(ICoreClientAPI capi)
    {
        var path = SettingsPath(capi);
        if (!System.IO.File.Exists(path))
        {
            Current = new ModSettings();
            return;
        }
        try
        {
            Current = JsonConvert.DeserializeObject<ModSettings>(System.IO.File.ReadAllText(path)) ?? new();
        }
        catch
        {
            Current = new ModSettings();
        }
    }

    public static void Save(ICoreClientAPI capi)
    {
        var path = SettingsPath(capi);
        System.IO.File.WriteAllText(path, JsonConvert.SerializeObject(Current, Formatting.Indented));
    }
}
