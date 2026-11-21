using Duxel.Core;

internal static class WidgetTreeThemeTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        UiDrawData Frame(Action<UiImmediateContext> render, UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(render));
            return context.GetDrawData();
        }
        void Step(IUiView view, UiInputState? input = null) => Frame(view.Render, input).ReleasePooled();
        check(Dux.Button("Factory") is UiFluentButton, "the existing declarative factory returns an inheritable compiled Fluent widget");
        check(Fluent.ComponentNames.ToArray().Intersect(new[] { "Button", "Checkbox", "Switch", "Text", "ToggleButton" }).Count() == 5
            && Fluent.ComponentNames.ToArray().Distinct(StringComparer.Ordinal).Count() == Fluent.ComponentNames.Length && Fluent.ComponentNames.Contains("Dropdown") && Fluent.ComponentNames.Contains("AvatarGroup") && Fluent.ComponentNames.Contains("Breadcrumb") && Fluent.ComponentNames.Contains("CardPreview") && Fluent.ComponentNames.Contains("CardHeader") && Fluent.ComponentNames.Contains("CardFooter") && Fluent.ComponentNames.Contains("Card") && Fluent.ComponentNames.Contains("Accordion") && Fluent.ComponentNames.Contains("Label") && Fluent.ComponentNames.Contains("Image") && Fluent.ComponentNames.Contains("Icon") && Fluent.ComponentNames.Contains("Menu") && Fluent.ComponentNames.Contains("Textarea") && Fluent.ComponentNames.Contains("Tooltip") && Fluent.ComponentNames.Contains("TabList")
            && Fluent.ComponentNames.Contains("Spinner") && Fluent.ComponentNames.Contains("Field") && Fluent.ComponentNames.Contains("Input") && Fluent.ComponentNames.Contains("Link") && Fluent.ComponentNames.Contains("Slider") && Fluent.ComponentNames.Contains("RadioGroup")
            && Fluent.ComponentNames.Contains("Badge") && Fluent.ComponentNames.Contains("CounterBadge"),
            "the generated standard catalog deduplicates checkbox variants and excludes preserved legacy types");
        var requiredComponents = Fluent.RequiredComponentNames.ToArray();
        var missingComponents = Fluent.MissingComponentNames.ToArray();
        check(requiredComponents.Length == 55 && requiredComponents.Contains("CardNudge")
            && requiredComponents.Contains("ProgressIndicator") && !requiredComponents.Contains("AcrylicPane")
            && !requiredComponents.Contains("CalendarDatePicker") && !requiredComponents.Contains("NotificationCommon"),
            "the compiled requirements follow the official site instead of repository-wide APIs");
        check(requiredComponents.Contains("Checkbox") && requiredComponents.Contains("Button")
            && !requiredComponents.Contains("CounterBadge") && !requiredComponents.Contains("Body1")
            && !requiredComponents.Contains("ToggleButton"),
            "component variants remain available without inflating top-level scope");
        check(!missingComponents.Contains("Carousel") && !missingComponents.Contains("CardNudge") && !missingComponents.Contains("Button") && !missingComponents.Contains("Textarea") && !missingComponents.Contains("Tooltip")
            && missingComponents.SequenceEqual(requiredComponents.Except(Fluent.ComponentNames.ToArray()).Order(StringComparer.Ordinal)),
            "missing top-level factories are computed independently of supporting variants");
        check(!Fluent.IsCatalogScopeComplete, "the unfinished official scope cannot advertise complete component coverage");
        var manyButtons = Enumerable.Range(0, 10000).Select(_ => Fluent.Button("Same caption")).ToArray();
        var firstIds = new string[manyButtons.Length];
        try
        {
            Frame(ui =>
            {
                ui.PushID("large-parent");
                for (var i = 0; i < manyButtons.Length; i++)
                {
                    ui.SetCursorScreenPos(new(10, 10));
                    manyButtons[i].Render(ui);
                    firstIds[i] = ui.GetItemID()!;
                }
                ui.PopID();
            }).ReleasePooled();
            check(firstIds.Distinct(StringComparer.Ordinal).Count() == 10000,
                "ten thousand retained buttons with identical captions keep distinct input identities");
            var reusedIds = 0;
            Frame(ui =>
            {
                ui.PushID("large-parent");
                for (var i = 0; i < manyButtons.Length; i++)
                {
                    ui.SetCursorScreenPos(new(10, 10));
                    manyButtons[i].Render(ui);
                    if (ReferenceEquals(firstIds[i], ui.GetItemID()))
                        reusedIds++;
                }
                ui.PopID();
            }).ReleasePooled();
            check(reusedIds == 10000, "all ten thousand retained input IDs reuse their strings beyond the bounded immediate-mode cache");
            string ChangedParent(string parent)
            {
                string result = "";
                Frame(ui => { ui.PushID(parent); manyButtons[^1].Render(ui); result = ui.GetItemID()!; ui.PopID(); }).ReleasePooled();
                return result;
            }
            check(ChangedParent("other-parent") != firstIds[^1] && ChangedParent("large-parent") == firstIds[^1],
                "moving a retained widget between parent scopes recomputes the correct identity without retaining every old scope");
            var wrongCallbacks = 0;
            var lastCallbacks = 0;
            Action wrongAction = () => wrongCallbacks++;
            foreach (var retainedButton in manyButtons)
                retainedButton.Activated = wrongAction;
            manyButtons[^1].Activated = () => lastCallbacks++;
            void ClickLast(UiInputState input)
            {
                Frame(ui =>
                {
                    ui.PushID("large-parent");
                    for (var i = 0; i < manyButtons.Length; i++)
                    {
                        ui.SetCursorScreenPos(i == manyButtons.Length - 1 ? new(10, 10) : new(1000, 1000));
                        manyButtons[i].Render(ui);
                    }
                    ui.PopID();
                }, input).ReleasePooled();
            }
            ClickLast(idle with
            {
                MousePosition = new(20, 20),
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            manyButtons[^1].Label = "Changed during press";
            ClickLast(idle with
            {
                MousePosition = new(20, 20),
                LeftMouseReleased = true
            });
            check(lastCallbacks == 1 && wrongCallbacks == 0,
                "input for the ten-thousandth retained button activates exactly its callback despite a label change");
            var longParent = new string('p', 5000);
            var longId = ChangedParent(longParent);
            check(longId.StartsWith(longParent + "/", StringComparison.Ordinal) && manyButtons[^1].IdentityCacheCharacters == 0,
                "oversized parent identities remain correct without admitting unbounded widget-owned strings");
            check(ChangedParent("large-parent") == firstIds[^1] && manyButtons[^1].IdentityCacheCharacters <= 4096,
                "normal parent identities resume reuse within the per-widget character bound");
        }
        finally { foreach (var retainedButton in manyButtons) retainedButton.Dispose(); }
        var fluentSelected = false;
        var selectionChanges = 0;
        var selectedBinding = new UiBinding<bool>(() => fluentSelected, value => { fluentSelected = value; selectionChanges++; });
        using var checkbox = (UiFluentCheckbox)Dux.Checkbox("Choice", selectedBinding);
        void ChoiceFrame(IUiView view, UiInputState input) => Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); view.Render(ui); }, input).ReleasePooled();
        var down = idle with
        {
            MousePosition = new(20, 20),
            LeftMouseDown = true,
            LeftMousePressed = true
        };
        var up = idle with
        {
            MousePosition = new(20, 20),
            LeftMouseReleased = true
        };
        var toggleButtonValue = new UiState<bool>(false);
        using var toggleButton = new DerivedToggleButton(toggleButtonValue.Binding);
        ChoiceFrame(toggleButton, down);
        check(!toggleButtonValue.Value && toggleButton.Changes == 0, "compiled toggle button does not change selection before release");
        toggleButton.Label = "Renamed";
        ChoiceFrame(toggleButton, up);
        ChoiceFrame(toggleButton, idle);
        check(toggleButtonValue.Value && toggleButton.Changes == 1, "inherited toggle button preserves selection and one callback across label changes");
        UiColor ToggleFill()
        {
            using var data = Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); toggleButton.Render(ui); });
            return Primitives(data).First(p => p.Rect.Width >= 96 && p.Rect.Height >= 32).Color;
        }
        var checkedFill = ToggleFill();
        toggleButtonValue.Value = false;
        var uncheckedFill = ToggleFill();
        check(checkedFill != uncheckedFill, "toggle button paints checked state independently of the pointer's held state");
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); ui.BeginDisabled(); toggleButton.Render(ui); ui.EndDisabled(); }, down).ReleasePooled();
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); ui.BeginDisabled(); toggleButton.Render(ui); ui.EndDisabled(); }, up).ReleasePooled();
        check(!toggleButtonValue.Value && toggleButton.Changes == 1, "disabled toggle button leaves selection and callbacks unchanged");
        check(Dux.ToggleButton("Factory", toggleButtonValue.Binding) is UiFluentToggleButton,
            "declarative toggle button uses the generated inheritable implementation");
        ChoiceFrame(checkbox, down);
        check(!fluentSelected && selectionChanges == 0, "compiled Fluent checkbox defers binding writes until release");
        ChoiceFrame(checkbox, up);
        ChoiceFrame(checkbox, idle);
        check(fluentSelected && selectionChanges == 1, "compiled Fluent checkbox writes its binding exactly once per activation");
        var renamedValue = new UiState<bool>(false);
        using var renamed = Fluent.Checkbox("Before", renamedValue.Binding);
        ChoiceFrame(renamed, down);
        renamed.Label = "After";
        ChoiceFrame(renamed, up);
        check(renamedValue.Value, "retained control identity survives a display-label change during an active press");
        var renamedClicks = 0;
        using var renamedButton = Fluent.Button("Before", () => renamedClicks++);
        ChoiceFrame(renamedButton, down);
        renamedButton.Label = "After";
        ChoiceFrame(renamedButton, up);
        check(renamedClicks == 1, "the common button kernel separates retained input identity from displayed text");
        bool? mixedValue = null;
        var mixedChanges = 0;
        using var mixed = new DerivedMixedCheckbox(new(() => mixedValue, value => { mixedValue = value; mixedChanges++; }));
        mixed.Label = "##mixed";
        using (var mixedData = Frame(ui => mixed.Render(ui)))
            check(mixedData.DrawLists.Any(list => list.Commands.AsSpan().ToArray().Any(command => command.TextureId != new UiTextureId(2))),
                "mixed checkbox without label draws its independently compiled official glyph");
        ChoiceFrame(mixed, down);
        ChoiceFrame(mixed, up);
        check(mixedValue == true && mixedChanges == 1 && mixed.Activations == 1,
            "mixed checkbox transitions to checked through an inherited OnChanged hook");
        ChoiceFrame(mixed, down);
        ChoiceFrame(mixed, up);
        check(mixedValue == false && mixedChanges == 2, "checked checkbox transitions to unchecked without cycling back to mixed");
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); ui.BeginDisabled(); mixed.Render(ui); ui.EndDisabled(); }, down).ReleasePooled();
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); ui.BeginDisabled(); mixed.Render(ui); ui.EndDisabled(); }, up).ReleasePooled();
        check(mixedValue == false && mixedChanges == 2, "disabled compiled checkbox does not invoke a binding setter");
        var switchValue = new UiState<bool>(false);
        using var switchWidget = (UiFluentSwitch)Dux.Toggle("Switch", switchValue.Binding);
        using (var switchData = Frame(switchWidget.Render))
            check(Primitives(switchData).Any(p => p.Rect.Width == 40 && p.Rect.Height == 20 && p.CornerRadius == 10),
                "compiled Fluent switch uses its own track presentation instead of a checkbox alias");
        ChoiceFrame(switchWidget, down);
        ChoiceFrame(switchWidget, up);
        check(switchValue.Value, "compiled Fluent switch shares release activation and updates a boolean binding");
        var pressedValue = new UiState<bool>(false);
        using var pressSwitch = new UiToggle<bool, UiBooleanToggleValue, UiPressButtonBehavior, UiFluentSwitchPresentation>("Press switch", pressedValue.Binding);
        ChoiceFrame(pressSwitch, down);
        check(pressedValue.Value, "the same switch presentation supports a separately selected press behavior");
        ChoiceFrame(pressSwitch, up);
        check(pressedValue.Value, "press-selected switch does not toggle again on release");
        var recreatedClicks = 0;
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); Dux.Button("Recreated", () => recreatedClicks++).Render(ui); },
            idle with
            {
                MousePosition = new(20, 20),
                LeftMouseDown = true,
                LeftMousePressed = true
            }).ReleasePooled();
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); Dux.Button("Recreated", () => recreatedClicks++).Render(ui); },
            idle with
            {
                MousePosition = new(20, 20),
                LeftMouseDown = true
            }).ReleasePooled();
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); Dux.Button("Recreated", () => recreatedClicks++).Render(ui); },
            idle with
            {
                MousePosition = new(20, 20),
                LeftMouseReleased = true
            }).ReleasePooled();
        check(recreatedClicks == 1, "recreating a declarative button each frame preserves parent-scoped press/release identity");
        using (var keyedFactories = new KeyedFactoryButtons())
        {
            Step(keyedFactories);
            var first = keyedFactories.FirstId;
            var second = keyedFactories.SecondId;
            keyedFactories.Rename = true;
            keyedFactories.RequestRebuild();
            Step(keyedFactories);
            check(first != second && first == keyedFactories.FirstId && second == keyedFactories.SecondId,
                "owned parent-scoped factory buttons keep stable distinct child IDs across rebuild and label changes");
        }
        using (var leaf = new DirectLeaf())
        {
            for (var frame = 0; frame < 3; frame++)
                Step(leaf);
            leaf.RequestRebuild();
            Step(leaf);
            check(leaf.Renders == 4, "a leaf renders directly without a child Build tree and preserves explicit frame invalidation");
        }
        var legacyAllocation = FactoryAllocation(static () => LegacyButton("Action", static () => { }, default));
        var widgetAllocation = FactoryAllocation(static () => Dux.Button("Action", static () => { }));
        check(widgetAllocation <= legacyAllocation,
            $"inheritable factory construction does not increase allocated bytes: legacy={legacyAllocation}, widget={widgetAllocation}, count=1024");
        check(widgetAllocation * 4 <= legacyAllocation * 3,
            "unowned declarative factories preserve the measured25-percent allocation reduction without retained identity storage");
        using var inheritedButton = new DerivedButton();
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); inheritedButton.Render(ui); },
            idle with
            {
                MousePosition = new(20, 20),
                LeftMouseDown = true,
                LeftMousePressed = true
            }).ReleasePooled();
        check(inheritedButton.Activations == 0, "inherited built-in button preserves release activation");
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); inheritedButton.Render(ui); },
            idle with
            {
                MousePosition = new(20, 20),
                LeftMouseReleased = true
            }).ReleasePooled();
        check(inheritedButton.Activations == 1, "inherited built-in invokes the user's activation override once");
        using (var customData = Frame(ui => ui.Button<UiReleaseButtonBehavior, CircularButtonPresentation>("Circle")))
        using (var inheritedData = Frame(ui => inheritedButton.Render(ui)))
        {
            var circles = Primitives(customData).Where(primitive => primitive.CornerRadius < 0).ToArray();
            check(circles.Length == 1 && circles[0].CornerRadius == -20 && circles[0].Rect.Width == 40,
                "custom presentation replaces the drawing algorithm with a circle through the existing layer pipeline");
            var inheritedCircles = Primitives(inheritedData).Where(primitive => primitive.CornerRadius < 0).ToArray();
            check(inheritedCircles.SequenceEqual(circles),
                "custom presentation measures and paints the same circle through inherited and typed immediate buttons");
        }
        var directPress = false;
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); directPress = ui.Button<UiPressButtonBehavior, CircularButtonPresentation>("Circle"); },
            idle with
            {
                MousePosition = new(20, 20),
                LeftMouseDown = true,
                LeftMousePressed = true
            }).ReleasePooled();
        check(directPress, "the same circular presentation supports an independent press behavior");
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); directPress = ui.Button<UiPressButtonBehavior, CircularButtonPresentation>("Circle"); },
            idle with
            {
                MousePosition = new(20, 20),
                LeftMouseReleased = true
            }).ReleasePooled();
        check(!directPress, "compiled press behavior does not activate again on release");
        using var counter = new Counter();
        for (var i = 0; i < 128; i++)
            Step(counter);
        check(counter.Builds == 1 && counter.Renders == 128, "retained widget builds once while its input/render path executes each frame");
        Step(counter, idle with
        {
            MousePosition = new(20, 20),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Step(counter, idle with
        {
            MousePosition = new(20, 20),
            LeftMouseReleased = true
        });
        Step(counter);
        check(counter.State.Value == 1 && counter.Observed == 1 && counter.Builds == 2,
            "a real button callback updates watched state and rebuilds on the next frame");
        counter.State.Value = 1;
        Step(counter);
        check(counter.Builds == 2, "assigning equal state preserves the built tree");
        counter.RequestRebuild();
        Step(counter);
        check(counter.Builds == 3, "explicit invalidation rebuilds external dependencies");

        using var parent = new Parent();
        Step(parent);
        var a = parent.A;
        var b = parent.B;
        var aid = a!.Id;
        var bid = b!.Id;
        parent.Reverse.Value = true;
        Step(parent);
        check(ReferenceEquals(a, parent.A) && ReferenceEquals(b, parent.B) && aid == a.Id && bid == b.Id && aid != bid,
            "keyed children preserve their instances and distinct input IDs across reorder");
        parent.ShowB.Value = false;
        Step(parent);
        var retired = false;
        Frame(ui => { try { b.Render(ui); } catch (ObjectDisposedException) { retired = true; } }).ReleasePooled();
        check(retired, "removing a keyed child releases its widget lifecycle");
        parent.Dispose();
        var disposed = false;
        Frame(ui => { try { parent.Render(ui); } catch (ObjectDisposedException) { disposed = true; } }).ReleasePooled();
        check(disposed, "disposed widget trees reject further rendering");

        using var failed = new FailOnce();
        Frame(ui => { try { failed.Render(ui); } catch (InvalidOperationException) { } failed.Render(ui); }).ReleasePooled();
        check(failed.Attempts == 2 && failed.Renders == 1, "failed Build is retried without rendering a stale tree");
        using var duplicate = new DuplicateKey();
        var rejected = false;
        Frame(ui => { try { duplicate.Render(ui); } catch (InvalidOperationException) { rejected = true; } }).ReleasePooled();
        check(rejected, "duplicate keyed children fail explicitly");

        using var pure = new Pure();
        Frame(ui =>
        {
            for (var i = 0; i < 16; i++)
                pure.Render(ui);
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 256; i++)
                pure.Render(ui);
            check(GC.GetAllocatedBytesForCurrentThread() == allocated && pure.Builds == 1,
                "stable Build dependency traversal and ID scoping allocate no additional bytes after warmup");
        }).ReleasePooled();

        var clicks = 0;
        var button = new UiButton("Action", () => clicks++);
        context.SetDesign(UiCompiledDesign.Windows11 with
        {
            ControlSkins = UiControlSkins.Material3Expressive
        });
        using (var data = Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); button.Render(ui); }))
        {
            var primitive = data.DrawLists.SelectMany(l => l.RectFilledPrimitives?.AsSpan().ToArray() ?? []).First();
            check(primitive.Rect.Height >= 40 && primitive.Rect.Width >= 58 && primitive.CornerRadius == 20,
                "compiled widget skin changes actual button minimum size and rounded geometry");
        }
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); button.Render(ui); },
            idle with
            {
                MousePosition = new(20, 20),
                LeftMouseDown = true,
                LeftMousePressed = true
            }).ReleasePooled();
        check(clicks == 0, "release activation does not invoke on mouse down");
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); button.Render(ui); },
            idle with
            {
                MousePosition = new(20, 20),
                LeftMouseReleased = true
            }).ReleasePooled();
        check(clicks == 1, "release activation invokes once on mouse release");
        var pressSkin = UiControlSkins.Material3Expressive.Button with
        {
            Activation = UiButtonActivation.Press
        };
        var press = button.ControlStyle(pressSkin);
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); press.Render(ui); },
            idle with
            {
                MousePosition = new(20, 20),
                LeftMouseDown = true,
                LeftMousePressed = true
            }).ReleasePooled();
        check(clicks == 2, "per-view compiled interaction policy invokes on mouse down");
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); press.Render(ui); },
            idle with
            {
                MousePosition = new(20, 20),
                LeftMouseReleased = true
            }).ReleasePooled();
        check(clicks == 2, "press activation does not invoke again on release");

        var toggleValue = false;
        void DrawSwitch(UiImmediateContext ui)
        {
            ui.SetCursorScreenPos(new(10, 10));
            ui.Toggle("##switch", ref toggleValue);
        }
        using (var data = Frame(DrawSwitch))
        {
            var primitives = Primitives(data);
            check(primitives.Any(p => p.Rect.Width == 52 && p.Rect.Height == 32 && p.CornerRadius == 16)
                && primitives.Any(p => p.CornerRadius == -8), "Material switch uses the compiled track and off-thumb geometry");
        }
        Frame(DrawSwitch,
            idle with
            {
                MousePosition = new(20, 20),
                LeftMouseDown = true,
                LeftMousePressed = true
            }).ReleasePooled();
        using (var data = Frame(DrawSwitch,
            idle with
            {
                MousePosition = new(20, 20),
                LeftMouseReleased = true
            }))
            check(toggleValue && Primitives(data).Any(p => p.CornerRadius == -8), "switch activation updates the value immediately and starts from the off thumb size");
        using (var data = Frame(DrawSwitch))
            check(Primitives(data).Any(p => p.CornerRadius is < -8 and > -12), "switch thumb interpolates between the off and selected sizes");
        context.State.AdvanceTime(1);
        using (var data = Frame(DrawSwitch))
            check(toggleValue && Primitives(data).Any(p => p.CornerRadius == -12), "switch thumb reaches the compiled selected size when motion completes");

        var sliderValue = .5f;
        using (var data = Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); ui.SliderFloat("##slider", ref sliderValue, 0, 1); }))
            check(Primitives(data).Any(p => p.Rect.Height == 16 && p.CornerRadius == 8)
                && Primitives(data).Any(p => p.Rect.Width == 4 && p.Rect.Height == 44),
                "compiled slider skin sets track and handle geometry");

        context.SetDesign(UiCompiledDesign.Windows11 with
        {
            ControlSkins = UiControlSkins.Material3Expressive with
            {
                TextField = UiControlSkins.Material3Expressive.TextField with
                {
                    SelectAllOnFocus = true
                }
            }
        });
        context.State.ActiveId = null;
        var inputValue = "alpha beta";
        string? inputId = null;
        using (var data = Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); inputId = ui.GetID("##field"); ui.InputText("##field", ref inputValue, 100); },
            idle with
            {
                MousePosition = new(20, 20),
                LeftMouseDown = true,
                LeftMousePressed = true
            }))
        {
            check(context.State.GetSelection(inputId!) == new UiTextSelection(0, inputValue.Length),
                "text field theme controls select-all behavior on initial focus");
            check(Primitives(data).Any(p => p.Rect.Height == 2), "focused underline text field uses its compiled indicator thickness");
        }

        context.State.ActiveId = null;
        context.SetDesign(UiCompiledDesign.Windows11);
        var customText = new UiState<string>("alpha beta");
        var defaultText = new UiState<string>("gamma");
        var customField = Dux.TextField("custom-field", customText.Binding).TextFieldStyle(new UiTextFieldSkin
        {
            Appearance = UiTextFieldAppearance.Underline,
            MinimumHeight = 57,
            FocusedBorderWidth = 4,
            SelectAllOnFocus = true
        });
        var defaultField = Dux.TextField("default-field", defaultText.Binding);
        string? customFieldId = null;
        using (var data = Frame(ui =>
        {
            ui.SetCursorScreenPos(new(10, 10));
            customFieldId = ui.GetID("##custom-field");
            customField.Render(ui);
            ui.SetCursorScreenPos(new(10, 100));
            defaultField.Render(ui);
        }, idle with { MousePosition = new(20, 20), LeftMouseDown = true, LeftMousePressed = true }))
        {
            var primitives = Primitives(data);
            check(primitives.Any(p => p.Rect.Y == 63 && p.Rect.Height == 4)
                && context.State.GetSelection(customFieldId!) == new UiTextSelection(0, customText.Value.Length),
                "a local TextField skin controls minimum height, focused underline and selection");
            var defaultFieldPrimitives = primitives.Where(p => p.Rect.Y >= 100).ToArray();
            check(defaultFieldPrimitives.Length > 0 && defaultFieldPrimitives.All(p => p.Rect.Height < 57),
                "a sibling TextField keeps the global skin after a local override");
        }

        var selected = new UiState<int>(0);
        var segments = DuxelView.Controls.Segmented("segments", selected.Binding, new[] { "First", "Second" });
        context.SetDesign(UiCompiledDesign.Windows11 with
        {
            ControlSkins = UiControlSkins.Material3Expressive with
            {
                Segmented = UiControlSkins.Material3Expressive.Segmented with
                {
                    Activation = UiButtonActivation.Press
                }
            }
        });
        context.State.ActiveId = null;
        UiVector2 secondPosition = default;
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); segments.Render(ui); var min = ui.GetItemRectMin(); var max = ui.GetItemRectMax(); secondPosition = new((min.X + max.X) / 2, (min.Y + max.Y) / 2); }).ReleasePooled();
        Frame(ui => { ui.SetCursorScreenPos(new(10, 10)); segments.Render(ui); },
            idle with
            {
                MousePosition = secondPosition,
                LeftMouseDown = true,
                LeftMousePressed = true
            }).ReleasePooled();
        check(selected.Value == 1, "segmented selection uses its independent compiled activation policy");

        context.State.ActiveId = null;
        context.SetDesign(UiCompiledDesign.Windows11);
        float page = 0, jump = 0;
        var trackInput = idle with
        {
            MousePosition = new(16, 85),
            LeftMouseDown = true,
            LeftMousePressed = true
        };
        Frame(ui => page = ui.RenderScrollbarV("page", new(10, 10, 14, 100), 0, 300, 400, new(0, 0, 320, 240)), trackInput).ReleasePooled();
        context.SetDesign(UiCompiledDesign.Windows11 with
        {
            ControlSkins = new()
            {
                Scrollbar = new()
                {
                    TrackClick = UiScrollbarTrackClick.Jump,
                    ThumbCornerRadius = 4,
                    AlwaysShowThumb = true
                }
            }
        });
        using (var data = Frame(ui => jump = ui.RenderScrollbarV("jump", new(10, 10, 14, 100), 0, 300, 400, new(0, 0, 320, 240)), trackInput))
            check(jump > page && Primitives(data).Any(p => p.CornerRadius == 4), "scrollbar skin changes track-click behavior and thumb shape");

        context.State.ActiveId = null;
        context.SetDesign(UiCompiledDesign.Windows11);
        using var left = new SliderWidget(10);
        using var right = new SliderWidget(110);
        Frame(ui => { left.Render(ui); right.Render(ui); },
            idle with
            {
                MousePosition = new(20, 40),
                LeftMouseDown = true,
                LeftMousePressed = true
            }).ReleasePooled();
        check(left.Value != .5f && right.Value == .5f, "equal vertical-slider labels in separate widget instances do not share active input state");

        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        var watched = new UiState<int>(0);
        using var reactive = new Reactive(watched);
        Step(reactive);
        requests = 0;
        watched.Value = 1;
        check(requests == 1, "watched state schedules a frame while the host is idle without waiting for Render polling");
        Step(reactive);
        requests = 0;
        watched.Value = 2;
        check(requests == 1, "rebuilding a watched dependency does not duplicate frame notifications");
        reactive.Observe = false;
        reactive.RequestRebuild();
        Step(reactive);
        requests = 0;
        watched.Value = 3;
        check(requests == 0, "a removed build dependency no longer schedules the widget");
        reactive.Observe = true;
        reactive.RequestRebuild();
        Step(reactive);
        reactive.Dispose();
        requests = 0;
        watched.Value = 4;
        check(requests == 0, "disposing the widget detaches watched-state notifications");
        var weak = WatchTemporaryWidget(context, watched);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        check(!weak.IsAlive, "persistent state observes widgets weakly and does not retain an abandoned tree");

        using var shared = new Child();
        using var firstOwner = new SharedOwner(shared);
        using var secondOwner = new SharedOwner(shared);
        Step(firstOwner);
        var sharingRejected = false;
        Frame(ui => { try { secondOwner.Render(ui); } catch (InvalidOperationException) { sharingRejected = true; } }).ReleasePooled();
        check(sharingRejected, "a stateful child instance cannot be owned by two parents");
        using var recursive = new Recursive();
        var cycleRejected = false;
        var scopeRestored = false;
        Frame(ui => { var before = ui.GetID("after"); try { recursive.Render(ui); } catch (InvalidOperationException) { cycleRejected = true; } scopeRestored = before == ui.GetID("after"); }).ReleasePooled();
        check(cycleRejected && scopeRestored, "recursive widget rendering fails explicitly and restores the caller ID scope");

        var released = 0;
        using var failedOwner = new FailingOwner(watched, () => released++);
        Frame(ui => { try { failedOwner.Render(ui); } catch (InvalidOperationException) { } }).ReleasePooled();
        requests = 0;
        watched.Value = 5;
        check(released == 1 && requests == 0, "failed Build releases newly owned children and provisional state observations");
        var disposalCount = 0;
        var disposing = new DisposeOwner(() => disposalCount++);
        Step(disposing);
        var disposalErrors = false;
        try
        {
            disposing.Dispose();
        }
        catch (AggregateException error) { disposalErrors = error.Flatten().InnerExceptions.Count == 1; }
        check(disposalCount == 2 && disposalErrors, "all child disposal hooks run and failures remain observable");

        var detached = CreateDetachedContext(watched);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        check(!detached.Context.IsAlive, "a retained widget frame token does not keep a disposed rendering context alive");
        detached.Widget.Dispose();
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private static UiRectFilledPrimitive[] Primitives(UiDrawData data)
        => data.DrawLists.SelectMany(l => l.RectFilledPrimitives?.AsSpan().ToArray() ?? []).ToArray();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference WatchTemporaryWidget(UiContext context, UiState<int> state)
    {
        var root = new Reactive(state);
        context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)),
            new(default, false, false, false, false, false, false, 0, 0, [], [], default, default), new(0, 0, 320, 240), UiTextSettings.Default);
        context.Render(new Screen(root.Render));
        context.GetDrawData().ReleasePooled();
        return new WeakReference(root);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (Reactive Widget, WeakReference Context) CreateDetachedContext(UiState<int> state)
    {
        var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        var root = new Reactive(state);
        context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)),
            new(default, false, false, false, false, false, false, 0, 0, [], [], default, default), new(0, 0, 320, 240), UiTextSettings.Default);
        context.Render(new Screen(root.Render));
        context.GetDrawData().ReleasePooled();
        context.Dispose();
        return (root, new WeakReference(context));
    }
    private sealed class Reactive(UiState<int> state) : UiWidget
    {
        internal bool Observe = true;
        protected override IUiView Build(UiBuildContext context)
        {
            if (Observe)
                context.Watch(state);
            return Dux.View(static _ => { });
        }
    }
    private sealed class SharedOwner(Child child) : UiWidget
    {
        protected override IUiView Build(UiBuildContext context) => context.Child("shared", () => child);
    }
    private sealed class Recursive : UiWidget
    {
        protected override IUiView Build(UiBuildContext context) => Dux.View(Render);
    }
    private sealed class DisposableChild(Action onDispose, bool fail) : UiWidget
    {
        protected override IUiView Build(UiBuildContext context) => Dux.View(static _ => { });
        protected override void OnDispose()
        {
            onDispose();
            if (fail)
                throw new InvalidOperationException("declared disposal failure");
        }
    }
    private sealed class FailingOwner(UiState<int> state, Action onDispose) : UiWidget
    {
        protected override IUiView Build(UiBuildContext context)
        {
            context.Watch(state);
            context.Child("temporary", () => new DisposableChild(onDispose, false));
            throw new InvalidOperationException("declared build failure");
        }
    }
    private sealed class DisposeOwner(Action onDispose) : UiWidget
    {
        protected override IUiView Build(UiBuildContext context) => Dux.Column(
            context.Child("first", () => new DisposableChild(onDispose, true)), context.Child("second", () => new DisposableChild(onDispose, false)));
    }
    private sealed class SliderWidget(float x) : UiWidget
    {
        internal float Value = .5f;
        protected override IUiView Build(UiBuildContext context) => Dux.View(ui =>
        {
            ui.SetCursorScreenPos(new(x, 10));
            ui.VSliderFloat("same-label", new(40, 80), ref Value, 0, 1);
        });
    }
    private sealed class Counter : UiWidget
    {
        internal UiState<int> State { get; } = new(0);
        internal int Builds, Renders, Observed;
        protected override IUiView Build(UiBuildContext context)
        {
            Builds++;
            Observed = context.Watch(State);
            return Dux.View(ui => { Renders++; ui.SetCursorScreenPos(new(10, 10)); if (ui.Button("Increment", new(100, 30))) State.Value++; });
        }
    }
    private sealed class Child : UiWidget
    {
        internal string? Id;
        protected override IUiView Build(UiBuildContext context) => Dux.View(ui => Id = ui.GetID("action"));
    }
    private sealed class Parent : UiWidget
    {
        internal UiState<bool> Reverse { get; } = new(false);
        internal UiState<bool> ShowB { get; } = new(true);
        internal Child? A, B;
        protected override IUiView Build(UiBuildContext context)
        {
            var reverse = context.Watch(Reverse);
            var show = context.Watch(ShowB);
            A = context.Child("a", static () => new Child());
            if (!show)
                return A;
            B = context.Child("b", static () => new Child());
            return reverse ? Dux.Column(B, A) : Dux.Column(A, B);
        }
    }
    private sealed class FailOnce : UiWidget
    {
        internal int Attempts, Renders;
        protected override IUiView Build(UiBuildContext context)
        {
            if (++Attempts == 1)
                throw new InvalidOperationException("declared failure");
            return Dux.View(_ => Renders++);
        }
    }
    private sealed class DuplicateKey : UiWidget
    {
        protected override IUiView Build(UiBuildContext context)
        {
            context.Child("x", static () => new Child());
            return context.Child("x", static () => new Child());
        }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static IUiView LegacyButton(string label, Action clicked, UiVector2 size) => new UiView(ui =>
    {
        if (ui.Button(label, size))
            clicked();
    });
    private static long FactoryAllocation(Func<IUiView> create)
    {
        for (var i = 0; i < 64; i++)
            GC.KeepAlive(create());
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1024; i++)
            GC.KeepAlive(create());
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
    private sealed class DirectLeaf : UiControlWidget
    {
        internal int Renders;
        protected override void RenderControl(UiImmediateContext ui) => Renders++;
    }
    private sealed class DerivedToggleButton(UiBinding<bool> value) : UiFluentToggleButton("Selection", value)
    {
        internal int Changes;
        protected override void OnChanged(bool value)
        {
            Changes++;
            base.OnChanged(value);
        }
    }
    private sealed class DerivedMixedCheckbox(UiBinding<bool?> value) : UiFluentMixedCheckbox("Mixed", value)
    {
        internal int Activations;
        protected override void OnChanged(bool? value)
        {
            Activations++;
            base.OnChanged(value);
        }
    }
    private sealed class KeyedFactoryButtons : UiWidget
    {
        internal string? FirstId, SecondId;
        internal bool Rename;
        protected override IUiView Build(UiBuildContext context)
        {
            var first = context.Child("first", static () => (UiFluentButton)Dux.Button("Same"));
            var second = context.Child("second", static () => (UiFluentButton)Dux.Button("Same"));
            first.Label = Rename ? "Renamed" : "Same";
            return Dux.View(ui =>
            {
                first.Render(ui);
                FirstId = ui.GetItemID();
                second.Render(ui);
                SecondId = ui.GetItemID();
            });
        }
    }
    private sealed class DerivedButton : UiButton<UiReleaseButtonBehavior, CircularButtonPresentation>
    {
        internal int Activations;
        internal DerivedButton() : base("Derived") { }
        protected override void OnActivated() => Activations++;
    }
    private readonly struct CircularButtonPresentation : IUiButtonPresentation
    {
        public static UiVector2 Measure(UiControlContext context, UiVector2 textSize, UiVector2 requestedSize) => new(40, 40);
        public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds, UiButtonInteraction interaction)
        {
            context.Circle(new(bounds.X + 20, bounds.Y + 20), 20, context.Theme.Button);
        }
    }
    private sealed class Pure : UiWidget
    {
        private readonly UiState<int> _state = new(0);
        internal int Builds;
        protected override IUiView Build(UiBuildContext context)
        {
            Builds++;
            context.Watch(_state);
            return Dux.View(static _ => { });
        }
    }
}
