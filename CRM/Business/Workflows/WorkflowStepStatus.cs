
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CRM.Business.Workflows
{
    public class WorkflowStepStatus
    {
        public WorkflowStepStatus()
        {
            Actions = new List<WorkflowStepActions>();
            TaskAssignerAction = null;
            IsCurrentStep = false;
            IsInErrorState = false;
            IsStopped = false;
            IsInWaitMode = false;
            ActionedByUserID = 0;
            EstimatedDaysRemaining = null;
            EstimatedDaysRemaining_StatusHint = EstimatedDaysRemaining_StatusHints.Normal.ToString();
        }

        public enum EstimatedDaysRemaining_StatusHints
        {
            Normal = 1,
            Warning = 2,
            Critical = 3
        }

        public string StepName { get; set; }
        public bool IsCurrentStep { get; set; }
        public bool IsInErrorState { get; set; }
        public bool IsStopped { get; set; }
        public bool IsInWaitMode { get; set; }
        public string Status { get; set; }
        public int? EstimatedDaysRemaining { get; set; }
        public string EstimatedDaysRemaining_StatusHint { get; set; }
        public string UserComment { get; set; }
        public string ActionMessage { get; set; }
        public DateTime? ActionedTime { get; set; }

        [JsonIgnore]
        public int? ActionedByUserID { get; set; }
        public string ActionedByUser { get; set; }

        [JsonIgnore]
        public int? AssignedUserID { get; set; }
        public string AssignedUser { get; set; }

        [JsonIgnore]
        public int? AssignedRoleID { get; set; }
        public string AssignedRole { get; set; }


        public DateTime CreateTime { get; set; }

        public List<WorkflowStepActions> Actions { get; set; }

        public WorkflowStepTaskAssignerAction TaskAssignerAction { get; set; }
    }
}
