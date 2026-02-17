
using CRM.Business.Workflows;
using CRM.Classes.Helpers;
using CRM.Models.Security;
using Newtonsoft.Json;

namespace CRM.Models.Workflows
{
    public partial class Workflow_Steps
    {
        public WorkflowStepTaskAssignerAction GetTaskAssignerAction(int stepID, int? curent_Assigned_Users_Id)
        {
            WorkflowStepTaskAssignerAction _taskAssignerAction = null;

            if (this.TaskAssigner_Roles_Id.HasValue && this.Action_Roles_Id.HasValue)
            {
                //Check if current user has permission to assign task by role.
                if (CurrentContext.CurrentUser.HasRole(this.TaskAssigner_Roles_Id.Value))
                {
                    _taskAssignerAction = new WorkflowStepTaskAssignerAction { };
                    _taskAssignerAction.ActionParameters.Add(new WorkflowStepTaskAssigneeUserParameter
                    {
                        User_Id = 0,
                        User_Name = "(UnAssigned)",
                        ActionParameter = JsonConvert.SerializeObject(new WorkflowStepTaskAssignerActionParameters
                        {
                            sid = stepID,
                            uid = 0
                        })
                    });

                    foreach (var user in Security_Users.GetUsersHavingRole(this.Action_Roles_Id.Value))
                    {
                        _taskAssignerAction.ActionParameters.Add(new WorkflowStepTaskAssigneeUserParameter
                        {
                            User_Id = user.Key,
                            User_Name = user.Value,
                            IsSelected = (curent_Assigned_Users_Id ?? 0) == user.Key,
                            ActionParameter = JsonConvert.SerializeObject(new WorkflowStepTaskAssignerActionParameters { 
                                sid = stepID, 
                                uid = user.Key
                            })
                        });
                    }
                }
            }

            return _taskAssignerAction;
        }
    }
}
