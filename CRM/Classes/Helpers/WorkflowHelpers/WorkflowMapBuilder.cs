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
            return CreateInMemoryWorkflowMap(workflowStepsList, new List<Workflow_StepTransition>());
        }

        public WorkflowStepsMap CreateInMemoryWorkflowMap(IReadOnlyList<Workflow_Steps> workflowStepsList, IReadOnlyList<Workflow_StepTransition> workflowStepTransitions)
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

            var transitionsByFromStepId = BuildTransitionsLookup(workflowStepTransitions, workflowStepsList);

            var root = new WorkflowStepsMap
            {
                WorkflowStep = workflowFirstStep[0]
            };

            _buildStepRecurseCounter = 0;
            BuildStep(workflowStepsList, root, null, transitionsByFromStepId);

            AddDisconnectedStepGraphs(workflowStepsList, root, transitionsByFromStepId);

            return root;
        }

        private static Dictionary<short, List<Workflow_StepTransition>> BuildTransitionsLookup(
            IReadOnlyList<Workflow_StepTransition> workflowStepTransitions,
            IReadOnlyList<Workflow_Steps> workflowStepsList)
        {
            var validStepIds = new HashSet<short>(workflowStepsList.Select(x => x.ID));

            return workflowStepTransitions
                .Where(t => (t.DelFlag ?? false) == false
                            && (t.IsActive ?? true)
                            && validStepIds.Contains(t.From_Workflow_Steps_ID)
                            && validStepIds.Contains(t.To_Workflow_Steps_ID))
                .GroupBy(t => t.From_Workflow_Steps_ID)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(x => x.SortOrder).ThenBy(x => x.ID).ToList());
        }

        private void BuildStep(
            IReadOnlyList<Workflow_Steps> workflowStepsList,
            WorkflowStepsMap workflowStepsMap,
            HashSet<short> allowedStepIds,
            Dictionary<short, List<Workflow_StepTransition>> transitionsByFromStepId)
        {
            if (++_buildStepRecurseCounter > _buildStepRecurseCount)
            {
                throw new Exception($"Maximum configured BuildStep() function recurse count of {_buildStepRecurseCount} reached.");
            }

            if (workflowStepsMap.IsStepExpansionProcessed)
            {
                return;
            }

            var candidateChildren = ResolveCandidateChildren(workflowStepsList, workflowStepsMap.WorkflowStep, transitionsByFromStepId);

            // Concern #1 addressed: de-duplicate children created from all lookup paths.
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
                BuildStep(workflowStepsList, child, allowedStepIds, transitionsByFromStepId);
            }
        }

        private static List<Workflow_Steps> ResolveCandidateChildren(
            IReadOnlyList<Workflow_Steps> workflowStepsList,
            Workflow_Steps currentStep,
            Dictionary<short, List<Workflow_StepTransition>> transitionsByFromStepId)
        {
            if (!transitionsByFromStepId.ContainsKey(currentStep.ID))
            {
                return new List<Workflow_Steps>();
            }

            var byId = workflowStepsList.ToDictionary(x => x.ID, x => x);
            var transitionChildren = new List<Workflow_Steps>();
            foreach (var transition in transitionsByFromStepId[currentStep.ID])
            {
                if (byId.ContainsKey(transition.To_Workflow_Steps_ID))
                {
                    transitionChildren.Add(byId[transition.To_Workflow_Steps_ID]);
                }
            }

            return transitionChildren;
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

        private void AddDisconnectedStepGraphs(
            IReadOnlyList<Workflow_Steps> workflowStepsList,
            WorkflowStepsMap root,
            Dictionary<short, List<Workflow_StepTransition>> transitionsByFromStepId)
        {
            var includedStepIds = new HashSet<short>();
            CollectStepIds(root, includedStepIds);

            var remainingSteps = workflowStepsList
                .Where(ws => !includedStepIds.Contains(ws.ID))
                .ToList();

            while (remainingSteps.Count > 0)
            {
                var disconnectedRootStep = FindDisconnectedRootCandidate(remainingSteps, transitionsByFromStepId);
                var disconnectedRoot = new WorkflowStepsMap
                {
                    WorkflowStep = disconnectedRootStep
                };

                var disconnectedStepIds = new HashSet<short>(remainingSteps.Select(x => x.ID));

                _buildStepRecurseCounter = 0;
                BuildStep(workflowStepsList, disconnectedRoot, disconnectedStepIds, transitionsByFromStepId);

                // Keep disconnected graphs isolated from the main chain.
                root.DisconnectedStepMaps.Add(disconnectedRoot);

                CollectStepIds(disconnectedRoot, includedStepIds);
                remainingSteps = workflowStepsList
                    .Where(ws => !includedStepIds.Contains(ws.ID))
                    .ToList();
            }
        }

        private static Workflow_Steps FindDisconnectedRootCandidate(
            List<Workflow_Steps> remainingSteps,
            Dictionary<short, List<Workflow_StepTransition>> transitionsByFromStepId)
        {
            if (transitionsByFromStepId.Count > 0)
            {
                var remainingStepIds = new HashSet<short>(remainingSteps.Select(x => x.ID));
                var incomingStepIds = new HashSet<short>(
                    transitionsByFromStepId.Values
                        .SelectMany(x => x)
                        .Where(x => remainingStepIds.Contains(x.From_Workflow_Steps_ID) && remainingStepIds.Contains(x.To_Workflow_Steps_ID))
                        .Select(x => x.To_Workflow_Steps_ID));

                var transitionRootCandidate = remainingSteps.FirstOrDefault(x => !incomingStepIds.Contains(x.ID));
                if (transitionRootCandidate != null)
                {
                    return transitionRootCandidate;
                }
            }

            // Fallback for disconnected/cyclic graphs with no clear root in transitions.
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
