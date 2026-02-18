namespace CRM.Models.Workflows
{
    using System.Collections.Generic;

    public partial class Workflow_Steps
    {
        public virtual ICollection<Workflow_StepTransition> Workflow_StepTransitions_From { get; set; }

        public virtual ICollection<Workflow_StepTransition> Workflow_StepTransitions_To { get; set; }
    }
}
