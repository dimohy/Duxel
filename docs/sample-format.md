# Generated layout sample formatting

The following existing FBA examples were reformatted on 2026-11-10:

- `layout_containers_fba.cs`: Container, Flex and Stack.
- `layout_single_child_fba.cs`: Padding, Align, Center and constraints.
- `layout_flow_containers_fba.cs`: Wrap and Grid.
- `layout_input_allocation_fba.cs`: editable input with owned slots.
- `layout_constrained_controls_fba.cs`: allocated Slider, Switch and Checkbox.

Assignments and named arguments use spaces (` = `, ` : `); declarations, callbacks and control flow use readable blocks. Display labels use `Count : 0`, `Columns : 3`, `Inset : 8` and `Width : 280`. Package/property directives and the numeric interpolation format `{next:0}` retain their required syntax. Existing dimensions, callbacks and ownership remain the same.

Each example is qualified separately against current in-tree sources with pinned .NET 11 RC3, explicit generator analyzer metadata where applicable, and a 15-second actual app run. Local copies replace only package directives with project references. Detailed build/run evidence is recorded in `artifacts/sample-format-2026-11-10/verification.json`. Startup alone does not prove manual input, official appearance or performance acceptance. These formatting changes add no completed whole-goal gate; current NativeAOT/runtime comparison and integrated performance remain pending.
