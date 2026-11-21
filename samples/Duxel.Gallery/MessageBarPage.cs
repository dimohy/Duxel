using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class MessageBarPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private readonly UiState<float> _width = new(560);
    private readonly UiState<bool> _multiline = new(false);
    private readonly UiState<bool> _square = new(false);
    private readonly UiState<bool> _rightToLeft = new(false);
    private readonly UiState<int> _actions = new(0);
    private readonly UiState<bool> _reducedMotion = new(false);
    private readonly UiState<bool> _announce = new(true);
    private readonly UiState<bool> _polite = new(false);
    private readonly UiState<int> _announcementCount = new(0);
    private readonly UiState<string> _lastAnnouncement = new("아직 없음");
    private UiAccessibilityTree? _observedTree;
    private UiImmediateContext? _announcementUi;
    private int? _verificationAnnouncementBaseline;
    private readonly UiFluentMessageBarGroup _group;
    private readonly UiFluentMessageBarGroup _dynamicGroup;
    private readonly List<UiKeyedWidget> _dynamicItems = [];
    private long _nextMessage;
    private readonly UiFluentMessageBar[] _messages;
    private readonly UiWidget _root;
    private readonly Action _addDynamic, _removeFirstDynamic, _reverseDynamic;
    private UiMessageBarOptions _resolvedOptions;
    public IReadOnlyList<IGallerySample> Samples { get; }
    public string Title => "메시지 표시";
    public string Description => "Add or reorder messages and inspect how the title and body are linked for accessibility. A new message requests a screen reader announcement. Verify speech with your screen reader.";
    internal MessageBarPage()
    {
        _resolvedOptions = ResolveOptions(new());
        var fixedSample = GallerySample.Create(() =>
        {
            UiFluentMessageBarGroup? group = null;
            var messages = Enum.GetValues<UiMessageBarIntent>().Select((intent, index) => Fluent.MessageBar(
                "메시지 " + intent,
                Fluent.MessageBarBody("작업 내용은 유지됩니다. 너비를 바꾸어 줄바꿈을 확인하세요.", Fluent.MessageBarTitle(intent.ToString())),
                Fluent.MessageBarActions([Fluent.Button("재시도", () => _actions.Value++)]),
                Fluent.Button("닫기", () => group!.SetVisible(index, false)), _resolvedOptions with { Intent = intent })).ToArray();
            group = Fluent.MessageBarGroup(messages, new(Gap : 10));
            return group;
        });
        _group = fixedSample.Preview;
        _messages = _group.Children.ToArray().Cast<UiFluentMessageBar>().ToArray();
        var fixedFactory = fixedSample.Source;
        fixedSample.BindSource(() => (_resolvedOptions, Actions : _actions.Value, Reduced : _reducedMotion.Value,
            Hidden : string.Join(",", Enumerable.Range(0, _messages.Length).Where(index => !_group.IsVisible(index)))), state =>
            $"var _actions = new UiState<int>({state.Actions});\n"
            + $"var _resolvedOptions = {OptionsSource(state._resolvedOptions)};\n\n" + fixedFactory
            + $"\n((UiFluentMessageBarGroup)example).Options = ((UiFluentMessageBarGroup)example).Options with {{ ReducedMotion = {GallerySample.Boolean(state.Reduced)} }};"
            + string.Concat(state.Hidden.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(index =>
                $"\n((UiFluentMessageBarGroup)example).SetVisible({index}, false);")));

        var groupDefinition = GallerySample.Define("_dynamicGroup", () => Fluent.MessageBarGroup([], new(Gap : 10, AnimateEnter : true)));
        _dynamicGroup = groupDefinition.Value;
        var remove = GallerySample.Define<Action<string>>("removeDynamic", () => (string key) =>
        {
            _dynamicItems.RemoveAll(item => item.Key == key);
            _dynamicGroup.SetItems(_dynamicItems.ToArray());
        });
        var removeDynamic = remove.Value;
        var messageDefinition = GallerySample.Define<Func<string, UiFluentMessageBar>>("createMessage", () => (string key) =>
            Fluent.MessageBar(key,
                Fluent.MessageBarBody("메시지를 추가·삭제하거나 순서를 바꿔 보세요. 예제를 닫을 때 사용한 자원도 해제됩니다.", Fluent.MessageBarTitle(key)),
                containerAction : Fluent.Button("삭제", () => removeDynamic(key)), options : _resolvedOptions));
        var createMessage = messageDefinition.Value;
        var add = GallerySample.Define<Action>("addDynamic", () => () =>
        {
            var key = "message-" + (++_nextMessage).ToString(System.Globalization.CultureInfo.InvariantCulture);
            _dynamicItems.Add(new(key, createMessage(key)));
            _dynamicGroup.SetItems(_dynamicItems.ToArray());
        });
        var addDynamic = _addDynamic = add.Value;
        var first = GallerySample.Define<Action>("removeFirstDynamic", () => () =>
        {
            if (_dynamicItems.Count > 0)
                removeDynamic(_dynamicItems[0].Key);
        });
        var removeFirstDynamic = _removeFirstDynamic = first.Value;
        var reverse = GallerySample.Define<Action>("reverseDynamic", () => () =>
        {
            _dynamicItems.Reverse();
            _dynamicGroup.SetItems(_dynamicItems.ToArray());
        });
        var reverseDynamic = _reverseDynamic = reverse.Value;
        var dynamicSample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min,
            CrossAxisAlignment : UiCrossAxisAlignment.Stretch),
            Dux.Wrap(new(Spacing : 12, RunSpacing : 8), Fluent.Button("메시지 추가", addDynamic),
                Fluent.Button("첫 메시지 삭제", removeFirstDynamic), Fluent.Button("순서 뒤집기", reverseDynamic)), _dynamicGroup));
        var dynamicFactory = dynamicSample.Source;
        dynamicSample.BindSource(() => (_resolvedOptions, Next : _nextMessage, Reduced : _reducedMotion.Value,
            Keys : string.Join(",", _dynamicItems.Select(item => GallerySample.String(item.Key)))), state =>
            $"var _resolvedOptions = {OptionsSource(state._resolvedOptions)};\n"
            + $"var _nextMessage = {state.Next}L;\nvar _dynamicItems = new List<UiKeyedWidget>();\n"
            + groupDefinition.Source + $"\n_dynamicGroup.Options = _dynamicGroup.Options with {{ ReducedMotion = {GallerySample.Boolean(state.Reduced)} }};\n\n"
            + remove.Source + "\n\n" + messageDefinition.Source + "\n\n" + add.Source + "\n\n" + first.Source + "\n\n" + reverse.Source
            + (state.Keys.Length == 0 ? string.Empty : "\n\nforeach (var key in new string[] { " + state.Keys + " })\n{\n"
                + "    _dynamicItems.Add(new(key, createMessage(key)));\n}\n_dynamicGroup.SetItems(_dynamicItems.ToArray());")
            + "\n\n" + dynamicFactory);
        Samples = [fixedSample, dynamicSample];
        _root = new UiScrollView(Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
        [
            Fluent.Slider("MessageBar 너비", _width.Binding, 300, 800, 1),
            Dux.Wrap(new(Spacing: 16, RunSpacing: 8), Fluent.Switch("여러 행 배치", _multiline.Binding),
                Fluent.Switch("각진 모서리", _square.Binding), Fluent.Switch("오른쪽에서 왼쪽 배치", _rightToLeft.Binding)),
            Fluent.Body1(() => $"너비 : {_width.Value:F0} / 액션 실행 : {_actions.Value}"),
            Dux.Wrap(new(Spacing: 16, RunSpacing: 8), Fluent.Switch("표시할 때 자동 공지", _announce.Binding),
                Fluent.Switch("차분한 공지로 고정", _polite.Binding)),
            Fluent.Caption1(() => $"공지 요청 : {_announcementCount.Value} / {_lastAnnouncement.Value}"),
            Dux.Wrap(new(Spacing: 16, RunSpacing: 8), Fluent.Button("모두 다시 열기", () =>
            {
                for (var i = 0; i < _messages.Length; i++) _group.SetVisible(i, true);
            }), Fluent.Switch("동작 줄이기", _reducedMotion.Binding)),
            fixedSample,
            Fluent.Subtitle1("키를 가진 동적 메시지"),
            dynamicSample
        ]));
        _root.AccessibleName = Title;
    }
    private void AddDynamic() => _addDynamic();
    private void RemoveFirstDynamic() => _removeFirstDynamic();
    private void ReverseDynamic() => _reverseDynamic();
    internal int RequestedDynamicCount => _dynamicGroup.Items.Length;
    internal int PhysicalDynamicCount => _dynamicGroup.Children.Length;
    internal int AnnouncementCount => _announcementCount.Value;
    internal UiFluentMessageBarGroup FixedMessages => _group;
    internal UiFluentMessageBarGroup DynamicMessages => _dynamicGroup;
    internal void ApplyVerificationAction(string action)
    {
        switch (action)
        {
            case "add":
                _verificationAnnouncementBaseline = _announcementCount.Value;
                for (var i = 0; i < _messages.Length; i++)
                    _group.SetVisible(i, false);
                _width.Value = 800;
                AddDynamic();
                AddDynamic();
                AddDynamic();
                break;
            case "reverse":
                ReverseDynamic();
                break;
            case "remove":
                RemoveFirstDynamic();
                break;
            case "clear":
                _dynamicItems.Clear();
                _dynamicGroup.SetItems([]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }
    internal void VerifyDynamicState(string action)
    {
        var expected = action switch
        {
            "add" or "reverse" => 3,
            "remove" => 2,
            "clear" => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        if (RequestedDynamicCount != expected || PhysicalDynamicCount != expected)
            throw new InvalidOperationException("MessageBar dynamic state did not reach its settled requested and physical collection.");
        var keys = _dynamicGroup.Items.ToArray().Select(item => item.Key).ToArray();
        var expectedKeys = action switch
        {
            "add" => new[] { "message-1", "message-2", "message-3" },
            "reverse" => ["message-3", "message-2", "message-1"],
            "remove" => ["message-2", "message-1"],
            "clear" => [],
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        if (!keys.SequenceEqual(expectedKeys))
            throw new InvalidOperationException("MessageBar dynamic key order differs from the executed commands.");
        if (_verificationAnnouncementBaseline is not { } baseline || _announcementCount.Value != baseline + 3)
            throw new InvalidOperationException("MessageBar mount announcements must occur once per inserted item and not repeat during reorder or removal.");
    }
    private void ObserveAnnouncements(UiImmediateContext ui)
    {
        _announcementUi = ui;
        var tree = ui.AccessibilityTree;
        if (ReferenceEquals(tree, _observedTree))
            return;
        if (_observedTree is { } previous)
            previous.Announcement -= OnAnnouncement;
        _observedTree = tree;
        if (tree is not null)
            tree.Announcement += OnAnnouncement;
    }
    private void OnAnnouncement(UiAccessibilityAnnouncement announcement)
    {
        _announcementCount.Value++;
        var text = new System.Globalization.StringInfo(announcement.Message);
        _lastAnnouncement.Value = (announcement.Priority == UiAnnouncementPriority.Polite ? "차분 : " : "즉시 : ")
            + text.SubstringByTextElements(0, Math.Min(28, text.LengthInTextElements));
        if (_announcementUi is { } ui)
            ui.RequestFrameAt(ui.GetTime());
    }
    UiWidget IObserverNaturalPage.MeasureRoot => _root;
    void IObserverNaturalPage.PrepareContent(UiImmediateContext ui) => PrepareContent(ui);
    private void PrepareContent(UiImmediateContext ui)
    {
        ObserveAnnouncements(ui);
        _resolvedOptions = ResolveOptions(new());
        _group.Options = _group.Options with
        {
            ReducedMotion = _reducedMotion.Value
        };
        _dynamicGroup.Options = _dynamicGroup.Options with
        {
            ReducedMotion = _reducedMotion.Value
        };
        foreach (var message in _messages)
            message.Options = ResolveOptions(message.Options);
        foreach (var item in _dynamicItems)
        {
            var message = (UiFluentMessageBar)item.Widget;
            message.Options = ResolveOptions(message.Options);
        }
    }
    public void Render(UiImmediateContext ui) { PrepareContent(ui); _root.Render(ui); }
    private UiMessageBarOptions ResolveOptions(UiMessageBarOptions options) => options with
    {
        Width = _width.Value,
        Layout = _multiline.Value ? UiMessageBarLayoutMode.MultiLine : UiMessageBarLayoutMode.Auto,
        Shape = _square.Value ? UiMessageBarShape.Square : UiMessageBarShape.Rounded,
        RightToLeft = _rightToLeft.Value,
        DisableAnnouncement = !_announce.Value,
        AnnouncementPriority = _polite.Value ? UiAnnouncementPriority.Polite : null
    };
    private static string OptionsSource(UiMessageBarOptions options) =>
        $"new UiMessageBarOptions(Intent : UiMessageBarIntent.{options.Intent}, Layout : UiMessageBarLayoutMode.{options.Layout},\n"
        + $"    Shape : UiMessageBarShape.{options.Shape}, Width : {(options.Width is { } width ? width.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "f" : "null")},\n"
        + $"    HideIcon : {GallerySample.Boolean(options.HideIcon)}, RightToLeft : {GallerySample.Boolean(options.RightToLeft)})\n"
        + $"{{ DisableAnnouncement = {GallerySample.Boolean(options.DisableAnnouncement)},\n"
        + $"    AnnouncementPriority = {(options.AnnouncementPriority is { } priority ? "UiAnnouncementPriority." + priority : "null")},\n"
        + $"    AnnouncementText = {GallerySample.String(options.AnnouncementText)} }}";
    public void Dispose()
    {
        if (_observedTree is { } tree)
            tree.Announcement -= OnAnnouncement;
        _observedTree = null;
        _announcementUi = null;
        _root.Dispose();
    }
}
