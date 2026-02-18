namespace CRM.Models.Workflows
{
    using System.Collections.Generic;

    public partial class Workflow
    {
        public virtual ICollection<Workflow_StepTransition> Workflow_StepTransitions { get; set; }
    }
}
