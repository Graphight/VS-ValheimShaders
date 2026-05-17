namespace ValheimShaders.Patch;

public interface IShaderPatch
{
    string Filename { get; }
    bool Optional { get; }
    string Apply(string source);
}
