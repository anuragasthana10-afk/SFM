using CRM.Classes.Helpers;
using CRM.Models.Workflows;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CRM.Classes.Helpers.WorkflowHelpers
{
    public sealed class WorkflowMapBuilder
    {
        private short _buildStepRecurseCounter;
        private readonly short _buildStepRecurseCount;

        public WorkflowMapBuilder(short recurseLimit = 500)
        {
            _buildStepRecurseCount = recurseLimit;
        }

        public WorkflowStepsMap CreateInMemoryWorkflowMap(IReadOnlyList<Workflow_Steps> workflowStepsList)
        {
            if (workflowStepsList.Count == 0)
            {
                throw new InvalidOperationException("No workflow steps provided.");
            }

            var workflowFirstStep = workflowStepsList.Where(ws => (ws.IsStartStep ?? false)).ToList();
            if (workflowFirstStep.Count > 1)
            {
                throw new InvalidOperationException("Setup error: More than one first step found.");
            }

            if (workflowFirstStep.Count < 1)
            {
                throw new InvalidOperationException("Setup error: No first step found.");
            }

            var root = new WorkflowStepsMap
            {
                WorkflowStep = workflowFirstStep[0]
            };

            _buildStepRecurseCounter = 0;
            BuildStep(workflowStepsList, root, null);

            AddDisconnectedStepGraphs(workflowStepsList, root);

            return root;
        }

        private void BuildStep(IReadOnlyList<Workflow_Steps> workflowStepsList, WorkflowStepsMap workflowStepsMap, HashSet<short> allowedStepIds)
        {
            if (++_buildStepRecurseCounter > _buildStepRecurseCount)
            {
                throw new Exception($"Maximum configured BuildStep() function recurse count of {_buildStepRecurseCount} reached.");
            }

            if (workflowStepsMap.IsStepExpansionProcessed)
            {
                return;
            }

            var candidateChildren = new List<Workflow_Steps>();

            if (workflowStepsMap.WorkflowStep.Workflow_Steps_NextStep_ID is short nextStepId)
            {
                candidateChildren.AddRange(workflowStepsList.Where(ws => ws.ID == nextStepId));
            }

            candidateChildren.AddRange(
                workflowStepsList.Where(ws => ws.Workflow_Steps_PreviousStep_ID == workflowStepsMap.WorkflowStep.ID));

            // Concern #1 addressed: de-duplicate children created from both lookup paths.
            foreach (var nextStep in candidateChildren
                         .GroupBy(x => x.ID)
                         .Select(g => g.First()))
            {
                if (allowedStepIds != null && !allowedStepIds.Contains(nextStep.ID))
                {
                    continue;
                }

                // Concern #2 addressed: check circular reference only in ancestry path.
                if (ExistsInParentChain(nextStep.ID, workflowStepsMap))
                {
                    throw new Exception(
                        $"Circular reference found. While processing Workflow Step ID {workflowStepsMap.WorkflowStep.ID} it looped back to Workflow Step ID {nextStep.ID}.");
                }

                workflowStepsMap.NextSteps.Add(new WorkflowStepsMap
                {
                    WorkflowStep = nextStep,
                    _ParentStepMap = workflowStepsMap
                });
            }

            workflowStepsMap.IsStepExpansionProcessed = true;

            foreach (var child in workflowStepsMap.NextSteps)
            {
                BuildStep(workflowStepsList, child, allowedStepIds);
            }
        }

        private static bool ExistsInParentChain(short stepId, WorkflowStepsMap current)
        {
            WorkflowStepsMap probe = current;

            while (probe != null)
            {
                if (probe.WorkflowStep.ID == stepId)
                {
                    return true;
                }

                probe = probe._ParentStepMap;
            }

            return false;
        }

        private void AddDisconnectedStepGraphs(IReadOnlyList<Workflow_Steps> workflowStepsList, WorkflowStepsMap root)
        {
            var includedStepIds = new HashSet<short>();
            CollectStepIds(root, includedStepIds);

            var remainingSteps = workflowStepsList
                .Where(ws => !includedStepIds.Contains(ws.ID))
                .ToList();

            while (remainingSteps.Count > 0)
            {
                var disconnectedRootStep = FindDisconnectedRootCandidate(remainingSteps);
                var disconnectedRoot = new WorkflowStepsMap
                {
                    WorkflowStep = disconnectedRootStep
                };

                var disconnectedStepIds = new HashSet<short>(remainingSteps.Select(x => x.ID));

                _buildStepRecurseCounter = 0;
                BuildStep(workflowStepsList, disconnectedRoot, disconnectedStepIds);

                // Keep disconnected graphs isolated from the main chain.
                root.DisconnectedStepMaps.Add(disconnectedRoot);

                CollectStepIds(disconnectedRoot, includedStepIds);
                remainingSteps = workflowStepsList
                    .Where(ws => !includedStepIds.Contains(ws.ID))
                    .ToList();
            }
        }

        private static Workflow_Steps FindDisconnectedRootCandidate(List<Workflow_Steps> remainingSteps)
        {
            var rootCandidate = remainingSteps.FirstOrDefault(candidate =>
                !remainingSteps.Any(other =>
                    other.Workflow_Steps_NextStep_ID == candidate.ID ||
                    candidate.Workflow_Steps_PreviousStep_ID == other.ID));

            if (rootCandidate != null)
            {
                return rootCandidate;
            }

            // Fallback for purely cyclic disconnected graphs.
            return remainingSteps[0];
        }

        private static void CollectStepIds(WorkflowStepsMap root, HashSet<short> stepIds)
        {
            var queue = new Queue<WorkflowStepsMap>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!stepIds.Add(current.WorkflowStep.ID))
                {
                    continue;
                }

                foreach (var child in current.NextSteps)
                {
                    queue.Enqueue(child);
                }

                foreach (var disconnectedRoot in current.DisconnectedStepMaps)
                {
                    queue.Enqueue(disconnectedRoot);
                }
            }
        }
    }
}
