using System.Collections.Generic;
using Vintagestory.API.Common;

namespace ValheimShaders.Patch;

public class ShaderPatcher
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, List<IShaderPatch>> _patches = new();

    public ShaderPatcher(ILogger logger)
    {
        _logger = logger;
    }

    public void Add(IShaderPatch patch)
    {
        if (!_patches.TryGetValue(patch.Filename, out var list))
        {
            list = new List<IShaderPatch>();
            _patches[patch.Filename] = list;
        }
        list.Add(patch);
    }

    public string ApplyPatches(string filename, string source)
    {
        if (!_patches.TryGetValue(filename, out var patches)) return source;

        foreach (var patch in patches)
        {
            var patched = patch.Apply(source);
            if (patched == source && !patch.Optional)
                _logger.Warning("[ValheimShaders] Shader patch for '{0}' had no effect — VS may have changed this shader.", filename);
            source = patched;
        }
        return source;
    }
}
