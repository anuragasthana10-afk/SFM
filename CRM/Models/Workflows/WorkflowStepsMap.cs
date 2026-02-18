using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CRM.Models.Workflows
{
    public class WorkflowStepsMap
    {
        private WorkflowStepsMap _parentStepMap;

        public WorkflowStepsMap()
        {
            IsStepExpansionProcessed = false;
            NextSteps = new List<WorkflowStepsMap>();
            DisconnectedStepMaps = new List<WorkflowStepsMap>();
        }

        public WorkflowStepsMap _ParentStepMap
        {
            get { return _parentStepMap; }
            set { _parentStepMap = value; }
        }

        // Backward-compatible alias used by previously added helper classes.
        public WorkflowStepsMap ParentStepMap
        {
            get { return _parentStepMap; }
            set { _parentStepMap = value; }
        }

        public Workflow_Steps WorkflowStep { get; set; }

        public List<WorkflowStepsMap> NextSteps { get; set; }

        // Additional roots for visualization; main transition chain remains under NextSteps from IsStartStep root.
        public List<WorkflowStepsMap> DisconnectedStepMaps { get; set; }

        public bool IsStepExpansionProcessed { get; set; }
    }

}
