# Workflow graph rendering and light editing

This repository now contains a small, self-contained implementation for:

1. Building an in-memory workflow map with cycle protection.
2. Rendering the map as a top-down SVG graph in a Razor component.
3. Performing light edit operations (connect/disconnect) in-memory.
4. Including orphan/disconnected step chains so all workflow steps can be displayed.

## Files

- `CRM/Models/Workflows/WorkflowStepsMap.cs`: map and step models.
- `CRM/Classes/Helpers/WorkflowHelpers/WorkflowMapBuilder.cs`: recursive in-memory map builder.
- `CRM/Classes/Helpers/WorkflowHelpers/WorkflowGraphLayout.cs`: graph flattening + layout + light edit operations.
- `Web/Views/Workflows/WorkflowGraph.cshtml`: Razor view (MVC/Razor Pages) SVG renderer.

## Notes

The map builder keeps recursion depth at `500` as requested and addresses:

- duplicate child edge creation when a step appears through both `Workflow_Steps_NextStep_ID` and `Workflow_Steps_PreviousStep_ID`.
- circular reference checks using parent-chain path detection so only true path loops are rejected.

- orphan/disconnected steps are added as additional graph roots so all `workflowStepsList` entries are represented in the rendered diagram.

- In MVC/Razor Pages `.cshtml`, SVG labels are emitted with `Html.Raw` to ensure literal `<text>` SVG elements are rendered (avoiding Razor `<text>` pseudo-tag behavior).

- disconnected graphs are stored only in `DisconnectedStepMaps`; the primary workflow transition chain remains `root -> NextSteps` from the `IsStartStep` node.
