using CRM.Classes.Helpers.WorkflowHelpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CRM.Classes.Helpers.WorkflowHelpers
{
    public sealed class WorkflowGraphLayout
    {
        public IReadOnlyList<WorkflowGraphNode> Nodes { get; set; }
        public IReadOnlyList<WorkflowGraphEdge> Edges { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public WorkflowGraphLayout()
        {
            Nodes = Array.Empty<WorkflowGraphNode>();
            Edges = Array.Empty<WorkflowGraphEdge>();
        }
    }

    public sealed class WorkflowGraphNode
    {
        public short StepId { get; set; }
        public string Name { get; set; }
        public byte Workflow_StepTypes_ID { get; set; }
        public bool IsOrphanChain { get; set; }
        public bool IsStartStep { get; set; }
        public string Description { get; set; }
        public string Workflow_StepTypes_ConfigData { get; set; }
        public string UserStepInstructions { get; set; }
        public string PreStepCompletion_DataValidation { get; set; }
        public string OnStepCompletion_Notifications { get; set; }
        public string OnStepCompletion_FieldUpdates { get; set; }
        public string OnStepCompletion_APICalls { get; set; }
        public string OnStepReview_Notifications { get; set; }
        public string OnStepReview_FieldUpdates { get; set; }
        public string OnStepReview_APICalls { get; set; }
        public string OnStepReject_Notifications { get; set; }
        public string OnStepReject_FieldUpdates { get; set; }
        public string OnStepReject_APICalls { get; set; }
        public string OnStepError_Notifications { get; set; }
        public int Level { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public WorkflowGraphNode()
        {
            Name = string.Empty;
        }
    }

    public sealed class WorkflowGraphEdge
    {
        public short FromStepId { get; set; }
        public short ToStepId { get; set; }
    }

    public static class WorkflowGraphLayoutBuilder
    {
        public static WorkflowGraphLayout Build(
            WorkflowStepsMap root,
            double nodeWidth = 220,
            double nodeHeight = 80,
            double horizontalGap = 80,
            double verticalGap = 70,
            double canvasPadding = 24)
        {
            var visitedNodes = new Dictionary<short, WorkflowGraphNode>();
            var seenEdges = new HashSet<string>(StringComparer.Ordinal);
            var edges = new List<WorkflowGraphEdge>();
            var queue = new Queue<Tuple<WorkflowStepsMap, int, bool>>();

            queue.Enqueue(new Tuple<WorkflowStepsMap, int, bool>(root, 0, false));
            foreach (var disconnectedRoot in root.DisconnectedStepMaps)
            {
                queue.Enqueue(new Tuple<WorkflowStepsMap, int, bool>(disconnectedRoot, 0, true));
            }

            while (queue.Count > 0)
            {
                var currentTuple = queue.Dequeue();
                var current = currentTuple.Item1;
                var level = currentTuple.Item2;
                var isOrphanChain = currentTuple.Item3;
                var step = current.WorkflowStep;

                if (!visitedNodes.ContainsKey(step.ID))
                {
                    visitedNodes[step.ID] = new WorkflowGraphNode
                    {
                        StepId = step.ID,
                        Name = step.Name,
                        Workflow_StepTypes_ID = step.Workflow_StepTypes_ID,
                        IsStartStep = step.IsStartStep ?? false,
                        Description = step.Description,
                        Workflow_StepTypes_ConfigData = step.Workflow_StepTypes_ConfigData,
                        UserStepInstructions = step.UserStepInstructions,
                        PreStepCompletion_DataValidation = step.PreStepCompletion_DataValidation,
                        OnStepCompletion_Notifications = step.OnStepCompletion_Notifications,
                        OnStepCompletion_FieldUpdates = step.OnStepCompletion_FieldUpdates,
                        OnStepCompletion_APICalls = step.OnStepCompletion_APICalls,
                        OnStepReview_Notifications = step.OnStepReview_Notifications,
                        OnStepReview_FieldUpdates = step.OnStepReview_FieldUpdates,
                        OnStepReview_APICalls = step.OnStepReview_APICalls,
                        OnStepReject_Notifications = step.OnStepReject_Notifications,
                        OnStepReject_FieldUpdates = step.OnStepReject_FieldUpdates,
                        OnStepReject_APICalls = step.OnStepReject_APICalls,
                        OnStepError_Notifications = step.OnStepError_Notifications,
                        IsOrphanChain = isOrphanChain,
                        Level = level,
                        Width = nodeWidth,
                        Height = nodeHeight
                    };
                }
                else if (isOrphanChain)
                {
                    visitedNodes[step.ID].IsOrphanChain = true;
                }

                foreach (var child in current.NextSteps)
                {
                    var edgeKey = step.ID + "->" + child.WorkflowStep.ID;
                    if (seenEdges.Add(edgeKey))
                    {
                        edges.Add(new WorkflowGraphEdge
                        {
                            FromStepId = step.ID,
                            ToStepId = child.WorkflowStep.ID
                        });
                    }

                    queue.Enqueue(new Tuple<WorkflowStepsMap, int, bool>(child, level + 1, isOrphanChain));
                }
            }

            var nodes = visitedNodes.Values
                .OrderBy(n => n.Level)
                .ThenBy(n => n.StepId)
                .ToList();

            var perLevelCounters = new Dictionary<int, int>();
            foreach (var node in nodes)
            {
                if (!perLevelCounters.ContainsKey(node.Level))
                {
                    perLevelCounters[node.Level] = 0;
                }
                else
                {
                    perLevelCounters[node.Level] = perLevelCounters[node.Level] + 1;
                }

                var indexWithinLevel = perLevelCounters[node.Level];
                node.X = canvasPadding + indexWithinLevel * (nodeWidth + horizontalGap);
                node.Y = canvasPadding + node.Level * (nodeHeight + verticalGap);
            }

            var width = nodes.Count == 0 ? canvasPadding * 2 : nodes.Max(n => n.X + n.Width) + canvasPadding;
            var height = nodes.Count == 0 ? canvasPadding * 2 : nodes.Max(n => n.Y + n.Height) + canvasPadding;

            return new WorkflowGraphLayout
            {
                Nodes = nodes,
                Edges = edges,
                Width = width,
                Height = height
            };
        }
    }

    public sealed class WorkflowMapEditor
    {
        public bool Connect(WorkflowStepsMap from, WorkflowStepsMap to)
        {
            if (from.WorkflowStep.ID == to.WorkflowStep.ID)
            {
                return false;
            }

            if (from.NextSteps.Any(x => x.WorkflowStep.ID == to.WorkflowStep.ID))
            {
                return false;
            }

            if (IsDescendant(to, from.WorkflowStep.ID))
            {
                return false;
            }

            from.NextSteps.Add(new WorkflowStepsMap
            {
                WorkflowStep = to.WorkflowStep,
                _ParentStepMap = from,
                IsStepExpansionProcessed = true
            });

            return true;
        }

        public bool Disconnect(WorkflowStepsMap from, short toStepId)
        {
            var before = from.NextSteps.Count;
            from.NextSteps.RemoveAll(x => x.WorkflowStep.ID == toStepId);
            return from.NextSteps.Count != before;
        }

        private static bool IsDescendant(WorkflowStepsMap root, short targetStepId)
        {
            var queue = new Queue<WorkflowStepsMap>();
            var visited = new HashSet<short>();

            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current.WorkflowStep.ID))
                {
                    continue;
                }

                if (current.WorkflowStep.ID == targetStepId)
                {
                    return true;
                }

                foreach (var next in current.NextSteps)
                {
                    queue.Enqueue(next);
                }
            }

            return false;
        }
    }
}
