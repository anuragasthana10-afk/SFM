# Workflow module notes

## Current implementation

This module now supports an explicit transition-entity model for workflow graph expansion while keeping backward compatibility during migration.

### Primary files

- `CRM/Classes/Helpers/WorkflowHelpers/WorkFlow.cs`
- `CRM/Classes/Helpers/WorkflowHelpers/WorkflowMapBuilder.cs`
- `CRM/Classes/Helpers/WorkflowHelpers/WorkflowGraphLayout.cs`
- `CRM/Classes/Helpers/WorkflowHelpers/WorkflowStepsMap.cs`
- `CRM/Models/Workflows/Workflow_StepTransition.cs`
- `CRM/Models/Clients/ClientModel.cs`
- `Database/Workflow/001_workflow_schema_changes.sql`

## Transition model behavior

`WorkflowMapBuilder` now supports two edge sources:

1. **Primary (new):** `Workflow_StepTransitions` rows (active + non-deleted), ordered by `SortOrder` then `ID`.
2. **Fallback (legacy):** `Workflow_Steps_NextStep_ID` and `Workflow_Steps_PreviousStep_ID`.

This enables phased production migration without breaking existing workflows.

## Migration plan summary

1. Run `Database/Workflow/001_workflow_schema_changes.sql` to create and backfill transition rows.
2. Deploy application code (already dual-read).
3. Validate runtime parity between transition edges and legacy links.
4. Move write paths to transition table (future step).
5. After stabilization, retire legacy step-link columns.
