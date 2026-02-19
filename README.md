# Workflow module notes

## Current implementation

This module supports an explicit transition-entity model for workflow graph expansion and now includes a Metronic-compatible MVC designer UI with command-style template APIs.

### Primary files

- `CRM/Classes/Helpers/WorkflowHelpers/WorkFlow.cs`
- `CRM/Classes/Helpers/WorkflowHelpers/WorkflowMapBuilder.cs`
- `CRM/Classes/Helpers/WorkflowHelpers/WorkflowGraphLayout.cs`
- `CRM/Classes/Helpers/WorkflowHelpers/WorkflowStepsMap.cs`
- `CRM/Models/Workflows/Workflow_StepTransition.cs`
- `CRM/Models/Workflows/Workflow_StepDesignerLayout.cs`
- `CRM/Models/Workflows/Workflow_TemplateAudit.cs`
- `CRM/Models/Clients/ClientModel.cs`
- `Web/Controllers/WebAPI/Workflow/WorkflowController.cs`
- `Web/Views/Workflows/WorkflowGraph.cshtml`
- `Database/Workflow/001_workflow_schema_changes.sql`
- `Database/Workflow/002_workflow_template_designer_tables.sql`

## Transition model behavior

`WorkflowMapBuilder` supports two edge sources:

1. **Primary (new):** `Workflow_StepTransitions` rows (active + non-deleted), ordered by `SortOrder` then `ID`.
2. **Fallback (legacy):** `Workflow_Steps_NextStep_ID` and `Workflow_Steps_PreviousStep_ID`.

This enables phased production migration without breaking existing workflows.

## Template designer APIs (command-style)

- `GetTemplateGraph`
- `AddTemplateStep`
- `UpdateTemplateStep`
- `DeleteTemplateStep`
- `AddTemplateTransition`
- `UpdateTemplateTransition`
- `RemoveTemplateTransition`
- `SaveStepLayout`

Template APIs are admin-role restricted and write a record to `Workflow_TemplateAudit` per change.

## Migration plan summary

1. Run `Database/Workflow/001_workflow_schema_changes.sql` to create and backfill transition rows.
2. Run `Database/Workflow/002_workflow_template_designer_tables.sql` for layout and template audit tables.
3. Deploy application code (dual-read and dual-write compatibility in place).
4. Validate runtime parity between transition edges and legacy links.
5. After stabilization, retire legacy step-link columns.
