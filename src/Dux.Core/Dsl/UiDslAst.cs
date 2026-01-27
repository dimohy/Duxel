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
}
