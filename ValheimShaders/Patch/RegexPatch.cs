using System.Text.RegularExpressions;

namespace ValheimShaders.Patch;

public class RegexPatch : IShaderPatch
{
    public string Filename { get; }
    public bool Optional { get; }

    private readonly string _pattern;
    private readonly string _replacement;

    public RegexPatch(string filename, string pattern, string replacement, bool optional = false)
    {
        Filename = filename;
        Optional = optional;
        _pattern = pattern;
        _replacement = replacement;
    }

    public string Apply(string source) => Regex.Replace(source, _pattern, _replacement);
}
