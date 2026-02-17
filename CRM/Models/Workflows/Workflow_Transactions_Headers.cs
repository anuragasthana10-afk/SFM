
using Amazon.Runtime;
using CRM.Business.Workflows;
using CRM.Business.Workflows.Config;
using CRM.Models.Clients;
using CRM.Models.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace CRM.Models.Workflows
{
    public partial class Workflow_Transactions_Headers
    {
        public void UpdateWorkflowCompletionStatus(ClientModel db)
        {
            //Check if all steps in the workflow are complete and then mark the workflow as complete.
            var workflowStepsTransactions = db.Workflow_StepTransactions.Where(w => w.Workflow_Transactions_Headers_ID == ID
            &&
                (
                    (w.StepExecuted ?? false) == false
                    || (w.ExecutionStopped ?? false) == true
                    || (w.ErrorInExecution ?? false) == true
                )
            ).AsNoTracking().ToList();

            if (workflowStepsTransactions.Count <= 0)
            {
                WorkflowComplete = true;
                WorkflowComplete_Time = DateTime.UtcNow;
                db.SaveChanges();

                //Check any dependent workflow which is waiting for this workflow to report completion.
                foreach (var dependentWorkflow in Dependendent_Workflow_Transactions_Headers_List)
                {
                    dependentWorkflow.DependsOn_WorkFlows_WaitingOnCompletion = false;
                }
                db.SaveChanges();

                //Check if there are any workflow step waiting on this triggered workflow to complete. Then check if there are any other sibling triggered workflows and that all those are also completed.
                var workflowStepsTransactions_TriggerWorkflow = db.Workflow_StepTransactions.Where(w => w.ID == WorkflowTriggerSource_Workflow_StepTransactions_ID
                && (w.IsWaitingOnTriggeredWorkflowToComplete ?? false)).FirstOrDefault();
                if (workflowStepsTransactions_TriggerWorkflow != null)
                {
                    //List other sibling triggered workflows that are still incomplete on which the triggering workflow is waiting.
                    var restOfAllOtherTriggeredInProgressWorkflows = db.Workflow_Transactions_Headers.Where(h =>
                            h.WorkflowTriggerSource_Workflow_StepTransactions_ID == workflowStepsTransactions_TriggerWorkflow.ID
                            && !(h.WorkflowComplete ?? false)
                            ).ToList();

                    if (restOfAllOtherTriggeredInProgressWorkflows.Count <= 0)
                    {
                        workflowStepsTransactions_TriggerWorkflow.IsWaitingOnTriggeredWorkflowToComplete = false;
                        db.SaveChanges();
                    }
                }
            }
        }

        public WorkflowStatus GetWorkflowStatus()
        {
            WorkflowStatus workflowStatus = new WorkflowStatus();
            workflowStatus.WorkflowCode = Workflow.Code;
            workflowStatus.WorkflowName = Workflow.Name;

            //Parse config data retrieve and add additional workflow description.
            workflowStatus.AdditionalWorkflowDescription = ParseConfigData().AdditionalWorkflowDescription;

            workflowStatus.IsWorkflowComplete = WorkflowComplete ?? false;
            if (workflowStatus.IsWorkflowComplete)
            {
                workflowStatus.WorkflowCompleteTime = WorkflowComplete_Time;
            }
            workflowStatus.Workflow_Transactions_Headers_ID = ID;

            List<WorkflowStepStatus> workflowStepStatusList = new List<WorkflowStepStatus>();
            foreach (var step in Workflow_StepTransactions.OrderBy(w => w.CreateDate))
            {
                var stepStatus = step.GetCurrentStepStatus();
                workflowStepStatusList.Add(stepStatus);
            }

            //Update actioned by user names.
            using (var rbac = new RBAC_Model())
            {
                //List all user id and name to avoid multiple db queries.
                int[] _actionedByUserIDs = workflowStepStatusList.Where(u => u.ActionedByUserID != null).Select(w => w.ActionedByUserID.Value).ToArray();
                int[] _assignedUserIDs = workflowStepStatusList.Where(u => u.AssignedUserID != null).Select(w => w.AssignedUserID.Value).ToArray();
                int[] _userIDs = _actionedByUserIDs.Union(_assignedUserIDs).Distinct().ToArray();
                var usersList = rbac.Security_Users.Where(u => _userIDs.Contains(u.User_Id)).Select(u => new { UserID = u.User_Id, UserName = u.Firstname + " " + u.Lastname }).ToList();

                //Role names.
                int[] _assignedRoleIDs = workflowStepStatusList.Where(u => u.AssignedRoleID != null).Select(w => w.AssignedRoleID.Value).ToArray();
                var rolesList = rbac.Security_Roles.Where(u => _assignedRoleIDs.Contains(u.Role_Id)).Select(u => new { RoleID = u.Role_Id, RoleName = u.RoleName }).ToList();

                foreach (var workflowStepStatus in workflowStepStatusList)
                {
                    //Actioned by user.
                    workflowStepStatus.ActionedByUser = usersList.Where(u => u.UserID == workflowStepStatus.ActionedByUserID).FirstOrDefault()?.UserName;

                    //Assigned to user.
                    workflowStepStatus.AssignedUser = usersList.Where(u => u.UserID == workflowStepStatus.AssignedUserID).FirstOrDefault()?.UserName;

                    //Assigned to role.
                    workflowStepStatus.AssignedRole = rolesList.Where(u => u.RoleID == workflowStepStatus.AssignedRoleID).FirstOrDefault()?.RoleName;
                }

            }

            workflowStatus.WorkflowStepStatuses = workflowStepStatusList;

            return workflowStatus;

        }

        public Workflow_Transactions_Headers__ConfigData ParseConfigData()
        {
            Workflow_Transactions_Headers__ConfigData _configDataObject = new Workflow_Transactions_Headers__ConfigData();
            if (!string.IsNullOrWhiteSpace(ConfigData))
            {
                var jsonRequest = JObject.Parse(ConfigData);

                if (jsonRequest == null || !jsonRequest.ContainsKey(nameof(Workflow_Transactions_Headers__ConfigData.TriggerWorkflow)))
                {
                    throw new Exception("ParseTriggerWorkflowInfoAndUpdateConfigData(): Invalid ConfigData Json. Key \"" + nameof(Workflow_Transactions_Headers__ConfigData.TriggerWorkflow) + "\" not found.");
                }
                _configDataObject = JsonConvert.DeserializeObject<Workflow_Transactions_Headers__ConfigData>(ConfigData);
            }

            return _configDataObject;
        }

        public void UpdateConfigData(Workflow_Transactions_Headers__ConfigData _configDataObject)
        {
            ConfigData = JsonConvert.SerializeObject(_configDataObject);
        }
    }
}
