
using System;
using System.Collections.Generic;

namespace CRM.Business.Workflows
{
    public class WorkflowStatus
    {
        public WorkflowStatus() 
        {
            WorkflowStepStatuses = new List<WorkflowStepStatus>();
            IsWorkflowComplete = false;
            IsRelatedWorkflow = false;
        }

        public string WorkflowCode { get; set; }
        public string WorkflowName { get; set; }
        public string AdditionalWorkflowDescription { get; set; }
        public bool IsWorkflowComplete { get; set; }
        public DateTime? WorkflowCompleteTime { get; set; }
        public bool IsRelatedWorkflow { get; set; }
        public int Workflow_Transactions_Headers_ID { get; set; }
        public List<WorkflowStepStatus> WorkflowStepStatuses { get; set; }
    }
}
