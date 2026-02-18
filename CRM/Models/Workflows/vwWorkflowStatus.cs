namespace CRM.Models.Workflows
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public partial class vwWorkflowStatus
    {
        public string AccountName { get; set; }

        public string AccountManagerName { get; set; }

        public string AccountManagerID { get; set; }

        public string InvoiceNumber { get; set; }

        public string InvoiceItemName { get; set; }

        public string Assigned_To_Name { get; set; }

        public string Reporting_To_Name { get; set; }

        public string Workflow_Status { get; set; }

        public string Waiting_On_Step_Name { get; set; }

        public int DaysRemaining { get; set; }
        public string DaysRemainingText { get; set; }
        public string DisplayName { get; set;  }

        public int Workflow_Transactions_Headers_ID { get; set; }
        public short Workflows_ID { get; set; }
        public string Context_Object_Code { get; set; }
        public int Object_RefID { get; set; }
        public DateTime Workflow_StartDate { get; set; }
        public string Workflow_Name { get; set; }
        public short Workflow_Steps_ID { get; set; }
        
        public int Workflow_StepTransactions_ID { get; set; }
        public int? Assigned_Users_Id { get; set; }
        public string Assigned_User_Name { get; set; }
        public int? AssignedBy_Users_Id { get; set; }
        public DateTime? AssignedBy_Users_Time { get; set; }
        public int? ActionedBy_Users_Id { get; set; }
        public DateTime? ActionedBy_Users_Time { get; set; }
        public bool? StepExecuted { get; set; }
        public bool? ErrorInExecution { get; set; }
        public bool? ExecutionStopped { get; set; }
        public bool? IsWaitingOnTriggeredWorkflowToComplete { get; set; }
        public DateTime Step_CreateDate { get; set; }
        public DateTime? Step_ModifyDate { get; set; }
        public string Task_Status { get; set; }

        public int? ReportingTo_Users_Id { get; set; }
        public string ReportingTo_User_Name { get; set; }
    }
}
