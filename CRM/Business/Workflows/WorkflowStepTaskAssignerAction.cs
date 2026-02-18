using iText.Layout.Element;
using System.Collections.Generic;

namespace CRM.Business.Workflows
{
    public class WorkflowStepTaskAssignerAction
    {
        public WorkflowStepTaskAssignerAction() 
        {
            ActionURL = "~/api/Workflow/AssignTask";
            ActionLabel = "";
            ActionParameters = new List<WorkflowStepTaskAssigneeUserParameter>();
        }

        public string ActionURL { get; set; }
        public string ActionLabel { get; set; }
        public List<WorkflowStepTaskAssigneeUserParameter> ActionParameters { get; set; }
    }
}
