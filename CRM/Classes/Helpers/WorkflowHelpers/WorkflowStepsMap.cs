using CRM.Models.Workflows;
using System.Collections.Generic;

namespace CRM.Classes.Helpers.WorkflowHelpers
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

        public WorkflowStepsMap ParentStepMap
        {
            get { return _parentStepMap; }
            set { _parentStepMap = value; }
        }

        public Workflow_Steps WorkflowStep { get; set; }

        public List<WorkflowStepsMap> NextSteps { get; set; }

        public List<WorkflowStepsMap> DisconnectedStepMaps { get; set; }

        public bool IsStepExpansionProcessed { get; set; }
    }
}
