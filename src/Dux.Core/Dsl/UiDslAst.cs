using Dux.Core;

namespace Dux.Core.Dsl;

public sealed class UiDslDocument
{
    public UiDslDocument(IReadOnlyList<UiDslNode> roots)
    {
        Roots = roots;
    }

    public IReadOnlyList<UiDslNode> Roots { get; }

    public void Emit(IUiDslEmitter emitter)
    {
        ArgumentNullException.ThrowIfNull(emitter);
        foreach (var node in Roots)
        {
            node.Emit(emitter);
        }
    }
}

public sealed class UiDslNode
{
    public UiDslNode(string name, IReadOnlyList<string> args, List<UiDslNode> children)
    {
        Name = name;
        Args = args;
        Children = children;
    }

    public string Name { get; }
    public IReadOnlyList<string> Args { get; }
    public List<UiDslNode> Children { get; }

    public void Emit(IUiDslEmitter emitter)
    {
        emitter.BeginNode(Name, Args);
        foreach (var child in Children)
        {
            child.Emit(emitter);
        }
        emitter.EndNode(Name);
    }
}

public interface IUiDslEmitter
{
    void BeginNode(string name, IReadOnlyList<string> args);
    void EndNode(string name);
}

public interface IUiDslEventSink
{
    void OnButton(string label);
    void OnCheckbox(string label, bool value);
}

public interface IUiDslValueSource
{
    bool TryGetBool(string id, out bool value);
    void SetBool(string id, bool value);

    bool TryGetInt(string id, out int value);
    void SetInt(string id, int value);

    bool TryGetUInt(string id, out uint value);
    void SetUInt(string id, uint value);

    bool TryGetFloat(string id, out float value);
    void SetFloat(string id, float value);

    bool TryGetDouble(string id, out double value);
    void SetDouble(string id, double value);

    bool TryGetString(string id, out string value);
    void SetString(string id, string value);

    bool TryGetVector2(string id, out UiVector2 value);
    void SetVector2(string id, UiVector2 value);

    bool TryGetVector4(string id, out UiVector4 value);
    void SetVector4(string id, UiVector4 value);

    bool TryGetColor(string id, out UiColor value);
    void SetColor(string id, UiColor value);
}
