using CRM.Business.Workflows;
using CRM.Business.Workflows.PostStepCompletionTaskTypes;
using CRM.Business.Workflows.PreStepCompletionTaskTypes;
using CRM.Business.Workflows.StepTypesConfigObject;
using CRM.Classes.Helpers;
using CRM.Classes.Helpers.WorkflowHelpers;
using CRM.Classes.Objects;
using CRM.Classes.Security;
using CRM.Models.Clients;
using CRM.Models.Communication;
using CRM.Models.ECom;
using CRM.Models.Objects;
using CRM.Models.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Transactions;
using static CRM.Business.Workflows.WorkflowStepStatus;

namespace CRM.Models.Workflows
{
    public partial class Workflow_StepTransactions
    {

        public WorkflowStepStatus GetCurrentStepStatus()
        {
            WorkflowStepStatus workflowStepStatus = new WorkflowStepStatus();

            workflowStepStatus.StepName = Workflow_Steps.Name;
            workflowStepStatus.CreateTime = CreateDate;

            if (Workflow_Transactions_Headers.DependsOn_WorkFlows_Workflow_Transactions_Headers_ID != null && (Workflow_Transactions_Headers.DependsOn_WorkFlows_WaitingOnCompletion ?? false))
            {
                workflowStepStatus.IsInWaitMode = true;
                workflowStepStatus.Status = "Waiting on workflow \"" + Workflow_Transactions_Headers.DependsOn_Workflow_Transaction_Header.Workflow.Name + "\" to complete.";
            }
            else if (IsWaitingOnTriggeredWorkflowToComplete ?? false)
            {
                workflowStepStatus.IsInWaitMode = true;
                workflowStepStatus.Status = "Waiting on triggered workflow(s) to complete.";
            }
            else if (!(StepExecuted ?? false))
            {
                workflowStepStatus.Status = "Pending action.";
            }
            else if (ErrorInExecution ?? false)
            {
                workflowStepStatus.IsInErrorState = true;
                workflowStepStatus.Status = "There was en error executing the workflow step: " + ErrorMessage;
            }
            else if (ExecutionStopped ?? false)
            {
                workflowStepStatus.IsStopped = true;
                workflowStepStatus.Status = "Workflow stopped.";
            }

            //Check if this is current step and mark accordingly.
            workflowStepStatus.IsCurrentStep = IsCurrentStep();

            workflowStepStatus.ActionedByUserID = ActionedBy_Users_Id;
            workflowStepStatus.ActionedTime = ActionedBy_Users_Time;
            workflowStepStatus.AssignedUserID = Assigned_Users_Id;
            workflowStepStatus.AssignedRoleID = Workflow_Steps.Action_Roles_Id;

            if (!string.IsNullOrWhiteSpace(UserComments))
                workflowStepStatus.UserComment = UserComments;
            else
                workflowStepStatus.UserComment = "";

            List<WorkflowStepActions> _actionsList = new List<WorkflowStepActions>();

            if (Workflow_Steps.Workflow_StepType.ID == (byte)Workflow_StepTypes.StepType.ApproveReviewReject)
            {
                var StepType_ConfigData = (ApproveReviewReject)Workflow_Steps.Workflow_StepType.ParseConfigData(Workflow_Steps.Workflow_StepTypes_ConfigData);
                workflowStepStatus.ActionMessage = StepType_ConfigData.UserInstructionalMessage;
                if (ExecutionStopped ?? false)
                {
                    workflowStepStatus.Status = "Rejected.";
                }
                else if (workflowStepStatus.IsCurrentStep && !workflowStepStatus.IsInWaitMode)
                {
                    WorkflowStepActionParameters actionParam = new WorkflowStepActionParameters();
                    actionParam.sid = ID;
                    actionParam.act = ((byte)ApproveReviewReject.UserResponse.Approve).ToString();
                    actionParam.msg = null;

                    _actionsList.Add(new WorkflowStepActions()
                    {
                        ActionLabel = StepType_ConfigData.ApproveButtonLabel,
                        ActionParameters = JsonConvert.SerializeObject(actionParam),
                        ActionUIHint = WorkflowStepActions.ActioUIHintTypes.Primary.ToString()
                    });

                    /*
                     * In case of Review check if there is any previous step with approve / reject or simple completion type step that is human interactive step type for them to review. 
                     * Otherwise we can't send this step to anyone to review.
                     */

                    //Search for previous Approve/Review/Reject type step
                    WorkflowStepsMap previousMapStep = SearchForPrevious_AnyOfSpecifiedStepTypes_WorkflowStep_InWorkflowMap(
                        new List<Workflow_StepTypes.StepType>()
                        {
                            Workflow_StepTypes.StepType.ApproveReviewReject,
                            Workflow_StepTypes.StepType.SimpleStepCompletion
                        }
                    );

                    if (previousMapStep != null)
                    {
                        actionParam = new WorkflowStepActionParameters();
                        actionParam.sid = ID;
                        actionParam.act = ((byte)ApproveReviewReject.UserResponse.Review).ToString();
                        actionParam.msg = null;

                        _actionsList.Add(new WorkflowStepActions()
                        {
                            ActionLabel = StepType_ConfigData.ReviewButtonLabel,
                            ActionParameters = JsonConvert.SerializeObject(actionParam),
                            ActionUIHint = WorkflowStepActions.ActioUIHintTypes.Secondary.ToString()
                        });
                    }

                    actionParam = new WorkflowStepActionParameters();
                    actionParam.sid = ID;
                    actionParam.act = ((byte)ApproveReviewReject.UserResponse.Reject).ToString();
                    actionParam.msg = null;

                    _actionsList.Add(new WorkflowStepActions()
                    {
                        ActionLabel = StepType_ConfigData.RejectButtonLabel,
                        ActionParameters = JsonConvert.SerializeObject(actionParam),
                        ActionUIHint = WorkflowStepActions.ActioUIHintTypes.Tertiary.ToString()
                    });

                    //Calculate ETA status for current step.
                    if (StepType_ConfigData.EstimatedDaysToComplete > 0)
                    {
                        //Get days elapsed since creation of this step.
                        var daysElapsed = (DateTime.UtcNow - CreateDate).Days;
                        workflowStepStatus.EstimatedDaysRemaining = StepType_ConfigData.EstimatedDaysToComplete - daysElapsed;
                        workflowStepStatus.EstimatedDaysRemaining_StatusHint = CalculateETACriticality(StepType_ConfigData.EstimatedDaysToComplete, daysElapsed).ToString();
                    }
                }
            }
            else if (Workflow_Steps.Workflow_StepType.ID == (byte)Workflow_StepTypes.StepType.SimpleStepCompletion)
            {
                var StepType_ConfigData = (SimpleStepCompletion)Workflow_Steps.Workflow_StepType.ParseConfigData(Workflow_Steps.Workflow_StepTypes_ConfigData);
                workflowStepStatus.ActionMessage = StepType_ConfigData.UserInstructionalMessage;

                if (workflowStepStatus.IsCurrentStep && !workflowStepStatus.IsInWaitMode)
                {
                    WorkflowStepActionParameters actionParam = new WorkflowStepActionParameters();
                    actionParam.sid = ID;
                    actionParam.act = ((byte)SimpleStepCompletion.UserResponse.Complete).ToString();
                    actionParam.msg = null;

                    _actionsList.Add(new WorkflowStepActions()
                    {
                        ActionLabel = StepType_ConfigData.TaskCompleteButtonLabel,
                        ActionParameters = JsonConvert.SerializeObject(actionParam)
                    });

                    //Calculate ETA status for current step.
                    if (StepType_ConfigData.EstimatedDaysToComplete > 0)
                    {
                        //Get days elapsed since creation of this step.
                        var daysElapsed = (DateTime.UtcNow - CreateDate).Days;
                        workflowStepStatus.EstimatedDaysRemaining = StepType_ConfigData.EstimatedDaysToComplete - daysElapsed;
                        workflowStepStatus.EstimatedDaysRemaining_StatusHint = CalculateETACriticality(StepType_ConfigData.EstimatedDaysToComplete, daysElapsed).ToString();
                    }
                }
            }
            else
            {
                //Other type of steps.
            }

            if ((this.Assigned_Users_Id.HasValue && this.Assigned_Users_Id == CurrentContext.CurrentUser.User_Id)
                || (!this.Assigned_Users_Id.HasValue && Workflow_Steps.Action_Roles_Id.HasValue && CurrentContext.CurrentUser.HasRole(Workflow_Steps.Action_Roles_Id.Value))
                || (this.Workflow_Steps.TaskAssigner_Roles_Id.HasValue && CurrentContext.CurrentUser.HasRole(this.Workflow_Steps.TaskAssigner_Roles_Id.Value))
                )
            {
                workflowStepStatus.Actions = _actionsList;
            }

            //Add task assigner actions.
            if (IsCurrentStep() && 
                    (Workflow_Steps.Workflow_StepType.ID == (byte)Workflow_StepTypes.StepType.ApproveReviewReject
                    || Workflow_Steps.Workflow_StepType.ID == (byte)Workflow_StepTypes.StepType.SimpleStepCompletion)
                )
            {
                workflowStepStatus.TaskAssignerAction = Workflow_Steps.GetTaskAssignerAction(ID, Assigned_Users_Id);
            }


            return workflowStepStatus;
        }


        protected bool IsCurrentStep()
        {
            return (!(StepExecuted ?? false) || (ErrorInExecution ?? false) || (ExecutionStopped ?? false));
        }

        protected EstimatedDaysRemaining_StatusHints CalculateETACriticality(int estimatedDays, int daysElapsed)
        {
            var percent = (daysElapsed / estimatedDays) * 100;

            if(percent >= 90)
            {
                return EstimatedDaysRemaining_StatusHints.Critical;
            }
            else if (percent >= 70)
            {
                return EstimatedDaysRemaining_StatusHints.Warning;
            }
            else
            {
                return EstimatedDaysRemaining_StatusHints.Normal;
            }
        }

        public void ProcessStep(string stepUserComment, string responseData, DbConnection p_dbConn = null)
        {
            DbConnection conn = p_dbConn;

            try
            {
                if (conn == null)
                {
                    conn = EFHelper.GetDbConnection();
                }

                var transactionOptions = new TransactionOptions();
                transactionOptions.Timeout = new System.TimeSpan(0, 10, 0);
                using (TransactionScope scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions))
                {
                    using (var db = new ClientModel(conn))
                    {
                        //Attach entity to context.
                        db.Workflow_StepTransactions.Attach(this);

                        //Check user permissions to process this step.
                        if(this.Workflow_Steps.Action_Roles_Id == null && this.Assigned_Users_Id == null)
                        {
                           throw new Exception("Opreration not allowed since this step doesn't have any assigned user or action role.");
                        }

                        if (this.Assigned_Users_Id != null && this.Assigned_Users_Id.Value != CurrentContext.CurrentUser.User_Id)
                        {
                            throw new Exception("Opreration not allowed since this step is assigned to another user.");
                        }
                        else if(this.Workflow_Steps.Action_Roles_Id != null && !CurrentContext.CurrentUser.HasRole(this.Workflow_Steps.Action_Roles_Id.Value))
                        {
                            throw new Exception("Opreration not allowed since current user doesn't have required role to process this task.");
                        }

                        //Check if this workflow is depdndent on another workflow and is still waiting for it to report completion.
                        if (Workflow_Transactions_Headers.DependsOn_WorkFlows_Workflow_Transactions_Headers_ID != null && (Workflow_Transactions_Headers.DependsOn_WorkFlows_WaitingOnCompletion ?? false))
                        {
                            throw new Exception("Workflow_StepTransactions::ProcessStep(): Opreration not allowed since this workflow transaction header ID: " + Workflow_Transactions_Headers.ID + " is still waiting on another workflow transaction header ID: " + Workflow_Transactions_Headers.DependsOn_WorkFlows_Workflow_Transactions_Headers_ID + " to report completion.");
                        }
                        else if (StepExecuted ?? false)
                        {
                            throw new Exception("Workflow_StepTransactions::ProcessStep(): Transaction Step ID:" + ID + " is already completed.");
                        }
                        else if (ExecutionStopped ?? false)
                        {
                            throw new Exception("Workflow_StepTransactions::ProcessStep(): Transaction Step ID:" + ID + " is in stopped state.");
                        }
                        else if (ErrorInExecution ?? false)
                        {
                            throw new Exception("Workflow_StepTransactions::ProcessStep(): Transaction Step ID:" + ID + " is in error state.");
                        }

                        __bOnStepCompletion_Invoked = false;
                        __bOnStepReview_Invoked = false;
                        __bOnStepReject_Invoked = false;
                        __bOnStepError_Invoked = false;

                        //Set screen context code if specified to set context for object model field updates to resolve access rights.
                        //Preserve original context screen code.
                        string originalContextScreenCode = CurrentContext.ScreenCode;
                        if (!string.IsNullOrWhiteSpace(Workflow_Transactions_Headers.Workflow.Context_Screen_Code))
                        {
                            CurrentContext.ScreenCode = Workflow_Transactions_Headers.Workflow.Context_Screen_Code;
                        }

                        //Validate processing parameters received for human interactive steps.
                        if (Workflow_Steps.Workflow_StepType.ID == (byte)Workflow_StepTypes.StepType.ApproveReviewReject)
                        {
                            var StepType_ConfigData = (ApproveReviewReject)Workflow_Steps.Workflow_StepType.ParseConfigData(Workflow_Steps.Workflow_StepTypes_ConfigData);
                            var responseObject = JsonConvert.DeserializeObject<WorkflowStepActionParameters>(responseData);
                            if (responseObject == null || !(
                                    responseObject.act.Trim() == ((byte)ApproveReviewReject.UserResponse.Approve).ToString()
                                    || responseObject.act.Trim() == ((byte)ApproveReviewReject.UserResponse.Review).ToString()
                                    || responseObject.act.Trim() == ((byte)ApproveReviewReject.UserResponse.Reject).ToString()
                                    )
                                )
                            {
                                throw new Exception("Workflow_StepTransactions::ProcessStep(): Invalid response for Step ID: " + ID + " for Step Type ID: " + Workflow_Steps.Workflow_StepType.ID);
                            }
                        }
                        else if (Workflow_Steps.Workflow_StepType.ID == (byte)Workflow_StepTypes.StepType.SimpleStepCompletion)
                        {
                            var StepType_ConfigData = (SimpleStepCompletion)Workflow_Steps.Workflow_StepType.ParseConfigData(Workflow_Steps.Workflow_StepTypes_ConfigData);
                            var responseObject = JsonConvert.DeserializeObject<WorkflowStepActionParameters>(responseData);
                            if (responseObject == null || !(responseObject.act.Trim() == ((byte)SimpleStepCompletion.UserResponse.Complete).ToString()))
                            {
                                throw new Exception("Workflow_StepTransactions::ProcessStep(): Invalid response for Step ID: " + ID + " for Step Type ID: " + Workflow_Steps.Workflow_StepType.ID);
                            }
                        }

                        //Populate context model data to process data validation, field update or replacing placdholders in notification messages etc.
                        PopulateContextObjectModel(db);

                        //Validate data if specified any before proceeding to process the step.
                        if (Workflow_Steps.Workflow_StepType.ID == (byte)Workflow_StepTypes.StepType.ApproveReviewReject)
                        {
                            var StepType_ConfigData = (ApproveReviewReject)Workflow_Steps.Workflow_StepType.ParseConfigData(Workflow_Steps.Workflow_StepTypes_ConfigData);
                            var responseObject = JsonConvert.DeserializeObject<WorkflowStepActionParameters>(responseData);
                            if (responseObject.act.Trim() == ((byte)ApproveReviewReject.UserResponse.Approve).ToString())
                            {
                                Process_PreStepCompletion_DataValidation(db);
                            }
                        }
                        else
                        {
                            Process_PreStepCompletion_DataValidation(db);
                        }

                        ResponseData = responseData;

                        if (Workflow_Steps.Workflow_StepType.ID == (byte)Workflow_StepTypes.StepType.APICall)
                        {
                            var StepType_ConfigData = (Business.Workflows.StepTypesConfigObject.APICall)Workflow_Steps.Workflow_StepType.ParseConfigData(Workflow_Steps.Workflow_StepTypes_ConfigData);
                            if (StepType_ConfigData != null)
                            {
                                //This step doesn't require any user interaction, so process the API call here. However, this type of step hasn't been implemented yet, so just mark it executed successfuly.
                                if (true)
                                {
                                    StepExecuted = true;
                                    ErrorInExecution = false;
                                    SuccessMessage = "API call success.";
                                }
                                else
                                {
                                    /******* Add api call response detailed error message. *******/
                                    ErrorInExecution = true;
                                    ErrorMessage = "Error in API call.";

                                    if (!StepType_ConfigData.OnErrorContinueToNextStep || true) //For now always stop at error as this config. may need to be moved to table column level.
                                    {
                                        ExecutionStopped = true;
                                        StepExecuted = false;
                                    }
                                    else
                                    {
                                        StepExecuted = true;
                                        ExecutionStopped = null;
                                    }
                                }
                            }
                        }
                        else if (Workflow_Steps.Workflow_StepType.ID == (byte)Workflow_StepTypes.StepType.TimedStepCompletion)
                        {
                            var StepType_ConfigData = (TimedStepCompletion)Workflow_Steps.Workflow_StepType.ParseConfigData(Workflow_Steps.Workflow_StepTypes_ConfigData);
                            TimeSpan completionDuration = new TimeSpan(StepType_ConfigData.DurationDays, StepType_ConfigData.DurationHours, StepType_ConfigData.DurationMinutes, StepType_ConfigData.DurationSeconds);
                            if (CreateDate.Add(completionDuration) <= DateTime.UtcNow)
                            {
                                StepExecuted = true;
                                ErrorInExecution = false;
                                SuccessMessage = "Completed step after waiting period.";
                            }
                        }
                        else if (Workflow_Steps.Workflow_StepType.ID == (byte)Workflow_StepTypes.StepType.ApproveReviewReject)
                        {
                            var StepType_ConfigData = (ApproveReviewReject)Workflow_Steps.Workflow_StepType.ParseConfigData(Workflow_Steps.Workflow_StepTypes_ConfigData);
                            var responseObject = JsonConvert.DeserializeObject<WorkflowStepActionParameters>(responseData);

                            if (responseObject.act.Trim() == ((byte)ApproveReviewReject.UserResponse.Reject).ToString())
                            {
                                StepExecuted = null;
                                ExecutionStopped = true;
                                SuccessMessage = "Rejected.";
                                Process_OnStepReject(db);
                            }
                            else
                            {
                                if (responseObject.act.Trim() == ((byte)ApproveReviewReject.UserResponse.Approve).ToString())
                                {
                                    StepExecuted = true;
                                    ExecutionStopped = null;
                                    SuccessMessage = "Approved.";
                                }
                                else if (responseObject.act.Trim() == ((byte)ApproveReviewReject.UserResponse.Review).ToString())
                                {
                                    /*
                                     * In case of Review check if there is any previous step with approve / reject or simple completion type step that is human interactive step type for them to review. 
                                     * Otherwise we can't send this step to anyone to review.
                                     */

                                    //Search for previous Approve/Review/Reject type step
                                    WorkflowStepsMap previousMapStep = SearchForPrevious_AnyOfSpecifiedStepTypes_WorkflowStep_InWorkflowMap(
                                        new List<Workflow_StepTypes.StepType>()
                                        {
                                                Workflow_StepTypes.StepType.ApproveReviewReject,
                                                Workflow_StepTypes.StepType.SimpleStepCompletion
                                        }
                                    );

                                    if (previousMapStep != null)
                                    {
                                        StepExecuted = true;
                                        ExecutionStopped = null;
                                        SuccessMessage = "Review requested.";
                                        Process_OnStepReview(db);
                                    }
                                    else
                                    {
                                        //No previous step found to send back for review, so skip processing any further.
                                        return;
                                    }
                                }
                            }

                            ErrorInExecution = false;
                            UserComments = stepUserComment;
                        }
                        else if (Workflow_Steps.Workflow_StepType.ID == (byte)Workflow_StepTypes.StepType.SimpleStepCompletion)
                        {
                            var StepType_ConfigData = (SimpleStepCompletion)Workflow_Steps.Workflow_StepType.ParseConfigData(Workflow_Steps.Workflow_StepTypes_ConfigData);
                            var responseObject = JsonConvert.DeserializeObject<WorkflowStepActionParameters>(responseData);

                            StepExecuted = true;
                            ErrorInExecution = false;
                            SuccessMessage = "Completed.";
                            UserComments = stepUserComment;
                        }
                        else
                        {
                            throw new Exception("Workflow_StepTransactions::ProcessStep(): Step processing not implented for Step Type ID: " + Workflow_Steps_ID);
                        }

                        if (StepExecuted ?? false)
                        {
                            ActionedBy_Users_Id = CurrentContext.CurrentUser.User_Id;
                            ActionedBy_Users_Time = DateTime.UtcNow;
                        }

                        //Process field updates, notifications, API calls etc.
                        if ((StepExecuted ?? false) && !(ErrorInExecution ?? false))
                        {
                            if (!__bOnStepReview_Invoked && !__bOnStepReject_Invoked)
                            {
                                Process_OnStepCompletion(db);
                            }
                        }
                        else if (ErrorInExecution ?? false)
                        {
                            Process_OnStepError(db);
                        }

                        db.Entry(this).State = System.Data.Entity.EntityState.Modified;
                        db.SaveChanges();

                        //Transition to next step
                        TransitionToNextStep(db);

                        scope.Complete();

                        //Restore original context screen code.
                        CurrentContext.ScreenCode = originalContextScreenCode;
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("Workflow_StepTransactions::ProcessStep(): txn sid:" + ID + "; workflow sid:" + Workflow_Steps_ID + "; " + e.Message + (e.InnerException != null ? " | Inner Exception: " + e.InnerException.Message : ""), e);
            }
            finally
            {
                if (p_dbConn == null)
                {
                    conn.Dispose();
                }
            }
        }

        [NotMapped]
        private Dictionary<string, WorkflowStepsMap> __workflowStepMapsList = new Dictionary<string, WorkflowStepsMap>();
        protected WorkflowStepsMap GetInMemoryWorkflowMap(string workflowCode)
        {
            if (__workflowStepMapsList.ContainsKey(workflowCode))
                return __workflowStepMapsList[workflowCode];
            else
            {
                var workflowHelper = new CRM.Classes.Helpers.WorkflowHelpers.WorkFlow();
                return __workflowStepMapsList[workflowCode] = workflowHelper.CreateInMemoryWorkflowMap(workflowCode.ParseEnum<Workflow.WorkflowCode>());
            }
        }

        protected bool CanTransitionToNextStep(ClientModel db)
        {
            bool bCanTransition = false;

            //Check if this transaction step is waiting on a triggered workflow to complete.
            /*
            bool bWaitingOnTriggeredWorkflowsToComplete = false;
            if (Workflow_Steps.Workflow_StepTypes_ID == (byte)Workflow_StepTypes.StepType.TriggerWorkflow && (Workflow_Steps.WaitForTriggeredWorkflowsToComplete ?? false))
            {
                var triggeredInProgressWorkflowTxnHeaders = db.Workflow_Transactions_Headers.Where(t => (t.WorkflowTriggerSource_Workflow_StepTransactions_ID ?? 0) == ID && (t.WorkflowComplete ?? false) == false).AsNoTracking().ToList();
                bWaitingOnTriggeredWorkflowsToComplete = triggeredInProgressWorkflowTxnHeaders.Count > 0;
            }
            */

            bCanTransition = (StepExecuted ?? false) && !(ExecutionStopped ?? false) && !(ErrorInExecution ?? false) && !(IsWaitingOnTriggeredWorkflowToComplete ?? false);

            return bCanTransition;
        }

        protected List<short> GetNextStepIDList()
        {
            List<short> nextStepIDList = new List<short>();

            try
            {
                //Get workflow code.
                string workflowCode = Workflow_Steps.Workflow.Code;

                //Get next step.
                var workflowMap = GetInMemoryWorkflowMap(workflowCode);
                var CurrentStepInMap = new WorkFlow().FindStepInWorkflowStepMap(Workflow_Steps, workflowMap);
                if (CurrentStepInMap == null)
                {
                    throw new Exception("Workflow_StepTransactions::GetNextStepIDList() - Current Workflow Step not found in workflow map.");
                }

                if (Workflow_Steps.Workflow_StepType.ID == (byte)Workflow_StepTypes.StepType.ApproveReviewReject)
                {
                    var responseObject = JsonConvert.DeserializeObject<WorkflowStepActionParameters>(ResponseData);
                    if(responseObject.act.Trim() == ((byte)ApproveReviewReject.UserResponse.Approve).ToString())
                    {
                        //List next steps IDs.
                        foreach (var mapStep in CurrentStepInMap.NextSteps)
                        {
                            nextStepIDList.Add(mapStep.WorkflowStep.ID);
                        }
                    }
                    else if (responseObject.act.Trim() == ((byte)ApproveReviewReject.UserResponse.Review).ToString())
                    {
                        //Search for previous Approve/Review/Reject type step
                        WorkflowStepsMap previousMapStep = SearchForPrevious_AnyOfSpecifiedStepTypes_WorkflowStep_InWorkflowMap(
                            new List<Workflow_StepTypes.StepType>() 
                            { 
                                Workflow_StepTypes.StepType.ApproveReviewReject, 
                                Workflow_StepTypes.StepType.SimpleStepCompletion 
                            }
                            );

                        if (previousMapStep != null) 
                        {
                            nextStepIDList.Add(previousMapStep.WorkflowStep.ID);
                        }
                    }
                }
                else
                {
                    //List next steps IDs.
                    foreach (var mapStep in CurrentStepInMap.NextSteps)
                    {
                        nextStepIDList.Add(mapStep.WorkflowStep.ID);
                    }
                }
            }
            catch (Exception e)
            {
                ExceptionDispatchInfo.Capture(e).Throw();
            }

            return nextStepIDList;
        }

        public bool TransitionToNextStep(ClientModel db)
        {
            bool bTransitionedToNextStep = false;

            try
            {
                if (CanTransitionToNextStep(db))
                {
                    //Get next steps IDs.
                    List<short> nextStepsIDs = GetNextStepIDList();

                    //Create next step transactions.
                    int stepsAdded = 0;
                    foreach (var stepID in nextStepsIDs)
                    {
                        var existingActiveStep = db.Workflow_StepTransactions.Where(t => t.Workflow_Steps_ID == stepID && t.Workflow_Transactions_Headers_ID == Workflow_Transactions_Headers_ID && (t.DelFlag ?? false) == false && (!(t.StepExecuted ?? false) || (ErrorInExecution ?? false) || (t.ExecutionStopped ?? false))).Count();
                        if (existingActiveStep <= 0)
                        {
                            Workflow_StepTransactions workflow_StepTransactions = new Workflow_StepTransactions()
                            {
                                Workflow_Steps_ID = stepID,
                                Workflow_Transactions_Headers_ID = Workflow_Transactions_Headers_ID,
                                Previous_Workflow_StepTransactions_ID = ID,
                            };
                            db.Workflow_StepTransactions.Add(workflow_StepTransactions);
                            db.SaveChanges();
                            // Ensure required nav props are loaded
                            db.Entry(workflow_StepTransactions).Reference(t => t.Workflow_Steps).Load();
                            db.Entry(workflow_StepTransactions).Reference(t => t.Workflow_Transactions_Headers).Load();
                            // If Workflow_Steps.Workflow is needed:
                            db.Entry(workflow_StepTransactions.Workflow_Steps).Reference(s => s.Workflow).Load();

                            // Populate context model for the newly created step so notifications/field-updates can resolve placeholders/users
                            workflow_StepTransactions.PopulateContextObjectModel(db);

                            workflow_StepTransactions.Process_OnStepCreate(db);
                            stepsAdded++;
                        }
                        else
                        {
                            throw new Exception("An active step with ID " + nextStepsIDs + " for this workflow already exists.");
                        }
                    }

                    if (stepsAdded > 0)
                    {                        
                        bTransitionedToNextStep = true;
                    }
                    else
                    {
                        Workflow_Transactions_Headers.UpdateWorkflowCompletionStatus(db);
                    }
                }
            }
            catch (Exception e)
            {
                ExceptionDispatchInfo.Capture(e).Throw();
            }

            return bTransitionedToNextStep;
        }

        protected WorkflowStepsMap SearchForPrevious_AnyOfSpecifiedStepTypes_WorkflowStep_InWorkflowMap(List<Workflow_StepTypes.StepType> stepTypeList)
        {
            //Get workflow code.
            string workflowCode = Workflow_Steps.Workflow.Code;

            //Get next step.
            var workflowMap = GetInMemoryWorkflowMap(workflowCode);
            var CurrentStepInMap = new WorkFlow().FindStepInWorkflowStepMap(Workflow_Steps, workflowMap);
            if (CurrentStepInMap == null)
            {
                throw new Exception("Workflow_StepTransactions::SearchForPrevious_AnyOfSpecifiedStepTypes_WorkflowStep_InWorkflowMap() - Current Workflow Step not found in workflow map.");
            }
            
            //Search for previous Approve/Review/Reject type step
            WorkflowStepsMap searchParentMapStep = CurrentStepInMap._ParentStepMap;
            List<byte> __stepTypes = new List<byte>();
            foreach (var stepType in stepTypeList)
            {
                __stepTypes.Add((byte)stepType);
            }

            while (searchParentMapStep != null)
            {
                if (__stepTypes.Contains(searchParentMapStep.WorkflowStep.Workflow_StepTypes_ID))
                {
                    return searchParentMapStep;
                }

                searchParentMapStep = searchParentMapStep._ParentStepMap;
            }

            return null;
        }

        public void AssignStep(string requestData, DbConnection p_dbConn = null)
        {
            DbConnection conn = p_dbConn;

            try
            {
                if(!IsCurrentStep())
                {
                    throw new Exception("Opreration not allowed since this is not the current step.");
                }

                if (!CurrentContext.CurrentUser.HasRole(this.Workflow_Steps.TaskAssigner_Roles_Id.Value))
                {
                    throw new Exception("Opreration not allowed since current user doesn't have required role to assign this task.");
                }

                if (conn == null)
                {
                    conn = EFHelper.GetDbConnection();
                }

                var transactionOptions = new TransactionOptions();
                transactionOptions.Timeout = new System.TimeSpan(0, 10, 0);
                using (TransactionScope scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions))
                {
                    using (var db = new ClientModel(conn))
                    {
                        db.Workflow_StepTransactions.Attach(this);
                        var requestObject = JsonConvert.DeserializeObject<WorkflowStepTaskAssignerActionParameters>(requestData);
                        this.AssignedBy_Users_Id = CurrentContext.CurrentUser.User_Id;
                        this.AssignedBy_Users_Time = DateTime.UtcNow;
                        if(requestObject.uid == 0)
                            this.Assigned_Users_Id = null;
                        else
                            this.Assigned_Users_Id = requestObject.uid;

                        db.Entry(this).State = System.Data.Entity.EntityState.Modified;
                        db.SaveChanges();

                        scope.Complete();
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("Workflow_StepTransactions::AssignTask(): txn sid:" + ID + "; workflow sid:" + Workflow_Steps_ID + "; " + e.Message + (e.InnerException != null ? " | Inner Exception: " + e.InnerException.Message : ""), e);
            }
            finally
            {
                if (p_dbConn == null)
                {
                    conn.Dispose();
                }
            }
        }


        #region Pre and Post Step Completion Handlers

        protected bool Process_PreStepCompletion_DataValidation(ClientModel db)
        {
            bool bIsValid = true;

            string dataValidationJson = Workflow_Steps.PreStepCompletion_DataValidation;

            if (string.IsNullOrWhiteSpace(dataValidationJson))
                return true;

            var jsonRequest = JObject.Parse(dataValidationJson);

            if (jsonRequest == null || !jsonRequest.ContainsKey(nameof(DataValidations.DataValidationList)))
            {
                throw new Exception("Process_PreStepCompletion_DataValidation(): Invalid PreStepCompletion DataValidation Json. Key \"" + nameof(DataValidations.DataValidationList) + "\" not found.");
            }

            var dataValidationObject = JsonConvert.DeserializeObject<DataValidations>(dataValidationJson);
            if (dataValidationObject == null)
            {
                throw new Exception("Process_PreStepCompletion_DataValidation(): Invalid PreStepCompletion DataValidation Json.");
            }

            foreach(var dataValidation in dataValidationObject.DataValidationList)
            {
                var modelPropertyValue = GetContextModelFieldValue(dataValidation.FieldName);

                if(!modelPropertyValue.ClassPropertyExists)
                {
                    throw new Exception("Process_PreStepCompletion_DataValidation(): Property " + dataValidation.FieldName + " not found in Context Model.");
                }

                if(modelPropertyValue.FieldValue == null)
                {
                    throw new Exception("Process_PreStepCompletion_DataValidation(): FieldValue cannot be null");
                }

                if (!dataValidation.CompareValues(modelPropertyValue.FieldValue, modelPropertyValue.FieldPropertyType))
                {
                    throw new Exception("Data Validation Exception:" + dataValidation.ValidationMessage);
                }
            }

            return bIsValid;
        }

        [NotMapped]
        private bool __bOnStepCreate_Invoked;
        public void Process_OnStepCreate(ClientModel db)
        {
            //Avoid calling the process multiple times.
            if (!__bOnStepCreate_Invoked)
            {
                __bOnStepCreate_Invoked = true;

                /**** Process OnStepCompletion ****/

                //Process notifications
                ProcessNotificationsJson(Workflow_Steps.OnStepCreate_Notifications, db);

                //Process field updates
                ProcessFieldUpdatesJson(Workflow_Steps.OnStepCreate_FieldUpdates, db);
            }
        }

        [NotMapped]
        private bool __bOnStepCompletion_Invoked;
        protected void Process_OnStepCompletion(ClientModel db)
        {
            //Avoid calling the process multiple times.
            if (!__bOnStepCompletion_Invoked)
            {
                __bOnStepCompletion_Invoked = true;

                /**** Process OnStepCompletion ****/

                //Process notifications
                ProcessNotificationsJson(Workflow_Steps.OnStepCompletion_Notifications, db);

                //Process field updates
                ProcessFieldUpdatesJson(Workflow_Steps.OnStepCompletion_FieldUpdates, db);
            }
        }

        [NotMapped]
        private bool __bOnStepReview_Invoked;
        protected void Process_OnStepReview(ClientModel db)
        {
            //Avoid calling the process multiple times.
            if (!__bOnStepReview_Invoked)
            {
                __bOnStepReview_Invoked = true;

                /**** Process OnStepReview ****/

                //Process notifications
                ProcessNotificationsJson(Workflow_Steps.OnStepReview_Notifications, db);

                //Process field updates
                ProcessFieldUpdatesJson(Workflow_Steps.OnStepReview_FieldUpdates, db);
            }
        }

        [NotMapped]
        private bool __bOnStepReject_Invoked;
        protected void Process_OnStepReject(ClientModel db)
        {
            //Avoid calling the process multiple times.
            if (!__bOnStepReject_Invoked)
            {
                __bOnStepReject_Invoked = true;

                /**** Process OnStepReject ****/

                //Process notifications
                ProcessNotificationsJson(Workflow_Steps.OnStepReject_Notifications, db);

                //Process field updates
                ProcessFieldUpdatesJson(Workflow_Steps.OnStepReject_FieldUpdates, db);
            }
        }

        [NotMapped]
        private bool __bOnStepError_Invoked;
        protected void Process_OnStepError(ClientModel db)
        {
            //Avoid calling the process multiple times.
            if (!__bOnStepError_Invoked)
            {
                __bOnStepError_Invoked = true;

                /**** Process OnStepError ****/

                //Process notifications
                ProcessNotificationsJson(Workflow_Steps.OnStepError_Notifications, db);
            }
        }

        protected void ProcessNotificationsJson(string notificationJson, ClientModel db)
        {
            if (string.IsNullOrWhiteSpace(notificationJson))
                return;

            var jsonRequest = JObject.Parse(notificationJson);

            if (jsonRequest == null || !jsonRequest.ContainsKey("NotificationList"))
            {
                throw new Exception("Invalid Notification Json. Key \"NotificationList\" not found.");
            }

            var jsonNotificationsObject = JsonConvert.DeserializeObject<Notifications>(notificationJson);
            if (jsonNotificationsObject == null)
            {
                throw new Exception("Invalid Notification Json.");
            }

            //Validate notifications list.
            foreach (var notification in jsonNotificationsObject.NotificationList)
            {
                if (string.IsNullOrWhiteSpace(notification.NotificationName))
                {
                    throw new Exception("NotificationName must be specified.");
                }
                else if (
                    (notification.CRMUserIDList.Length <= 0 || notification.CRMUserIDList[0] <= 0)
                    && (notification.ToEmailAddress.Length <= 0 || string.IsNullOrWhiteSpace(notification.ToEmailAddress[0]))
                    && (notification.CRMUserExpressionList.Length <= 0 || string.IsNullOrWhiteSpace(notification.CRMUserExpressionList[0]))
                    )
                {
                    throw new Exception("Atleast one of CRMUserExpressionList or CRMUserIDList or ToEmailAddress must be specified for notification: " + notification.NotificationName);
                }
                else if (string.IsNullOrWhiteSpace(notification.Subject))
                {
                    throw new Exception("NotificationName must be specified for notification: " + notification.NotificationName);
                }
                else if (string.IsNullOrWhiteSpace(notification.MessageBody))
                {
                    throw new Exception("MessageBody must be specified for notification: " + notification.NotificationName);
                }
            }

            //Process notifications list.
            foreach (var notification in jsonNotificationsObject.NotificationList)
            {
                string _CcEmailAddress = string.Join(",", Notification_ReplaceEmailAddress_Placeholders(notification.CcEmailAddress));
                string _BccEmailAddress = string.Join(",", Notification_ReplaceEmailAddress_Placeholders(notification.BccEmailAddress));
                string _ToEmailAddress = string.Join(",", Notification_ReplaceEmailAddress_Placeholders(notification.ToEmailAddress));

                string _Subject = notification.Subject;
                _Subject = ReplacePlaceholdersWithWorkflowPropertyValue(_Subject);
                _Subject = ReplacePlaceholdersWithContextObjectModelPropertyValue(_Subject);                

                string _MessageBody = notification.MessageBody;
                _MessageBody = ReplacePlaceholdersWithWorkflowPropertyValue(_MessageBody);
                _MessageBody = ReplacePlaceholdersWithContextObjectModelPropertyValue(_MessageBody);                

                int[] _CRMUserIDList_Combined = notification.CRMUserIDList.Distinct().ToArray();
                _CRMUserIDList_Combined = _CRMUserIDList_Combined.Union(ProcessCRMUserExpressionList(notification.CRMUserExpressionList, db)).Distinct().ToArray();

                if (_CRMUserIDList_Combined.Length > 0)
                {
                    Business.Communication.Helper.NotifyUsersWithCC(NotificationType.Type.WFLOW_NOTIF.ToString(), _Subject, _MessageBody, _CRMUserIDList_Combined, ToEmailAddress: _ToEmailAddress, CcEmailAddress: _CcEmailAddress, BccEmailAddress: _BccEmailAddress, _dbConnection: db.Database.Connection);
                }
                else
                {
                    Business.Communication.Helper.NotifyClientUser(NotificationType.Type.WFLOW_NOTIF.ToString(), _Subject, _MessageBody, ToEmailAddress: _ToEmailAddress, CcEmailAddress: _CcEmailAddress, BccEmailAddress: _BccEmailAddress, _dbConnection: db.Database.Connection);
                }
            }
        }

        protected int[] ProcessCRMUserExpressionList(string[] CRMUserExpressionList, ClientModel db)
        {
            List<int> _CRMUserIDList_FromExpressions = new List<int>();

            if (CRMUserExpressionList.Length > 0 && !string.IsNullOrWhiteSpace(CRMUserExpressionList[0]))
            {
                using (RBAC_Model dbSec = new RBAC_Model(db.Database.Connection))
                {
                    Security_Users obAMUser = null;
                    Security_Users obUser = null;

                    foreach (var crmUserExpression in CRMUserExpressionList)
                    {
                        if (Enum.TryParse<Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression>(crmUserExpression, out var predefinedExpression))
                        {
                            string _notificationUserTypeName = null;
                            if (predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.AM
                                || predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.AM_Supervisor
                                || predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.AM_Assistant
                                || predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.AM_HOD
                                )
                            {
                                if (obAMUser == null)
                                {
                                    if (this._topParentContextModel == null)
                                    {
                                        throw new Exception("For AM based CRMUserExpression, context model must have Top Parent Context Model set.");
                                    }

                                    Accounts _account = null;
                                    //Get account manager user.
                                    if(EFHelper.GetModelType(_contextModel.Entity) == typeof(Accounts))
                                    {
                                        _account = this._contextModel.Entity as Accounts;
                                    }
                                    else if (EFHelper.GetModelType(_topParentContextModel.Entity) == typeof(Accounts))
                                    {
                                        _account = this._topParentContextModel.Entity as Accounts;
                                    }
                                    else
                                    {
                                        throw new Exception("For AM based CRMUserExpression, context model or top parent context model must be of type Accounts.");
                                    }

                                    if (_account.AccountManagerUser_ID != null)
                                    {
                                        obAMUser = dbSec.Security_Users.Find(_account.AccountManagerUser_ID);
                                        if (obAMUser == null)
                                        {
                                            throw new Exception("Account Manager user not found for Account Code: " + _account.AccountCode);
                                        }
                                    }
                                    else
                                    {
                                        //Account manager user id can be empty for some reason so skip if not found instead of throwing exception.
                                        continue;
                                    }
                                }

                                if (predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.AM)
                                    _notificationUserTypeName = RBACUser.EmployeeBands.User.ToString();
                                else if (predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.AM_Supervisor)
                                    _notificationUserTypeName = RBACUser.EmployeeBands.Supervisor.ToString();
                                else if (predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.AM_Assistant)
                                    _notificationUserTypeName = RBACUser.EmployeeBands.Assistant.ToString();
                                else if (predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.AM_HOD)
                                    _notificationUserTypeName = RBACUser.EmployeeBands.HOD.ToString();

                                if (!string.IsNullOrWhiteSpace(_notificationUserTypeName))
                                {
                                    int? iNotificationUserID = obAMUser.ParseUserID(_notificationUserTypeName);
                                    if (iNotificationUserID != null)
                                    {
                                        _CRMUserIDList_FromExpressions.Add(iNotificationUserID.Value);
                                    }
                                }
                            }
                            else if (predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.User
                                || predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.User_Supervisor
                                || predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.User_Assistant
                                || predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.User_HOD
                                )
                            {
                                if (obUser == null)
                                {
                                    if (this._contextModel == null)
                                    {
                                        throw new Exception("For User based CRMUserExpression, context model must be set.");
                                    }

                                    //Get create and modify user if from context model
                                    var userID = GetContextModelFieldValue("CreateUserID");
                                    if (!userID.ClassPropertyExists || userID.FieldValue == null)
                                    {
                                        throw new Exception("For User based CRMUserExpression, context model must have CreateUserID property set.");
                                    }

                                    obUser = dbSec.Security_Users.Find(userID.FieldValue);
                                    if (obUser == null)
                                    {
                                        throw new Exception("Create User not found for context object model.");
                                    }
                                }

                                if (predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.User)
                                    _notificationUserTypeName = RBACUser.EmployeeBands.User.ToString();
                                else if (predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.User_Supervisor)
                                    _notificationUserTypeName = RBACUser.EmployeeBands.Supervisor.ToString();
                                else if (predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.User_Assistant)
                                    _notificationUserTypeName = RBACUser.EmployeeBands.Assistant.ToString();
                                else if (predefinedExpression == Business.Workflows.PostStepCompletionTaskTypes.Notification.CRMUserExpression.User_HOD)
                                    _notificationUserTypeName = RBACUser.EmployeeBands.HOD.ToString();

                                if (!string.IsNullOrWhiteSpace(_notificationUserTypeName))
                                {
                                    int? iNotificationUserID = obUser.ParseUserID(_notificationUserTypeName);
                                    if (iNotificationUserID != null)
                                    {
                                        _CRMUserIDList_FromExpressions.Add(iNotificationUserID.Value);
                                    }
                                }
                            }
                        }
                        else
                        {
                            throw new Exception("Invalid CRMUserExpression: " + crmUserExpression);
                        }
                    }
                }
            }

            return _CRMUserIDList_FromExpressions.ToArray();
        }

        protected void ProcessFieldUpdatesJson(string fieldUpdateJson, ClientModel db)
        {
            if (string.IsNullOrWhiteSpace(fieldUpdateJson))
                return;

            var jsonRequest = JObject.Parse(fieldUpdateJson);

            if (jsonRequest == null || !jsonRequest.ContainsKey("FieldUpdateList"))
            {
                throw new Exception("Invalid FieldUpdate Json. Key \"FieldUpdateList\" not found.");
            }

            var jsonFieldUpdatesObject = JsonConvert.DeserializeObject<FieldUpdates>(fieldUpdateJson);
            if (jsonFieldUpdatesObject == null)
            {
                throw new Exception("Invalid Field Update Json.");
            }

            //Validate field update list.
            foreach (var notification in jsonFieldUpdatesObject.FieldUpdateList)
            {
                if (string.IsNullOrWhiteSpace(notification.FieldName))
                {
                    throw new Exception("FieldName must be specified.");
                }
            }

            //Process field updates.
            foreach (var notification in jsonFieldUpdatesObject.FieldUpdateList)
            {
                UpdateContextModelField(notification.FieldName, notification.FieldValue, UserComments);
            }

        }

        protected string[] Notification_ReplaceEmailAddress_Placeholders(string[] EmailAddresses)
        {
            string [] _EmailAddresses = EmailAddresses;

            for (int i = 0; i < _EmailAddresses.Length; i++)
            {
                _EmailAddresses[i] = ReplacePlaceholdersWithContextObjectModelPropertyValue(_EmailAddresses[i]);
            }

            return _EmailAddresses;
        }


        protected string ReplacePlaceholdersWithContextObjectModelPropertyValue(string Text)
        {
            string _Text = Text;
            const string _StartDelimiter = "{{", _EndDelimiter = "}}";

            //Replace all specific placeholders that should be computed before performing replacement by property match in context object model.
            /*
            string replacePlaceholder = _StartDelimiter + "EXAMPLE_PLACEHOLDER" + _EndDelimiter;
            if (_Text.Contains(replacePlaceholder))
            {
                _Text = _Text.Replace(replacePlaceholder, "REPLACED_TEXT");
            }
            */

            //Retrieve all occurrences of placeholders to be replaced which are all expected to be property names of context object model.
            var ModelPropertyList = _Text.ExtractStringListWithinDelimiters(_StartDelimiter, _EndDelimiter);

            //Get values from context object model.
            Dictionary<string, string> PropertyValueList = new Dictionary<string, string>();
            foreach (var Property in ModelPropertyList)
            {
                var _value = GetContextModelFieldValue(Property);

                if (!_value.ClassPropertyExists)
                {
                    throw new Exception("For placeholder " + _StartDelimiter + Property + _EndDelimiter + ", corresponding property not found in context model.");
                }

                string _stringValue = "";

                if (_value.FieldValue != null)
                {
                    if (_value.FieldPropertyType.Equals(typeof(DateTime)))
                    {
                        _stringValue = Convert.ToDateTime(_value.FieldValue).ToString(Culture.CurrentDateFormat, CultureInfo.InvariantCulture);
                    }
                    else if (_value.FieldPropertyType.Equals(typeof(bool)))
                    {
                        if ((bool)_value.FieldValue)
                            _stringValue = "Yes";
                        else
                            _stringValue = "No";
                    }
                    else
                    {
                        _stringValue = _value.FieldValue.ToString();
                    }
                }

                PropertyValueList.Add(Property, _stringValue);
            }

            //Replace all placeholders.
            foreach (var PropertyValue in PropertyValueList)
            {
                _Text = _Text.Replace(_StartDelimiter + PropertyValue.Key + _EndDelimiter, PropertyValue.Value);
            }

            return _Text;
        }

        protected string ReplacePlaceholdersWithWorkflowPropertyValue(string Text)
        {
            string _Text = Text;
            const string _StartDelimiter = "{{", _EndDelimiter = "}}";

            //Replace all specific placeholders that should be computed before performing replacement by property match in context object model.
            string replacePlaceholder;
            replacePlaceholder = _StartDelimiter + "WFLOW_ADDL_WORKFLOW_DESCRIPTION" + _EndDelimiter;
            if (_Text.Contains(replacePlaceholder))
            {
                _Text = _Text.Replace(replacePlaceholder, this.Workflow_Transactions_Headers.ParseConfigData().AdditionalWorkflowDescription);
            }

            replacePlaceholder = _StartDelimiter + "WFLOW_STEP_NAME" + _EndDelimiter;
            if (_Text.Contains(replacePlaceholder))
            {
                _Text = _Text.Replace(replacePlaceholder, this.Workflow_Steps.Name);
            }

            return _Text;
        }

        #endregion


        #region Context Object Model Operations

        [NotMapped]
        protected DbEntityEntry _contextModel;

        [NotMapped]
        protected DbContext _modelDbContext;

        [NotMapped]
        protected DbEntityEntry _parentContextModel;

        [NotMapped]
        protected DbContext _parentContextModelDbContext;

        [NotMapped]
        protected DbEntityEntry _topParentContextModel;

        [NotMapped]
        protected DbContext _topParentContextModelDbContext;


        /// <summary>
        /// Retrieves the field value from the context object model or parent model depending on FieldNameExpression.
        /// For immediate context model field, provide field name only.
        /// For parent context model field, provide field name expression in format Parent.FieldName
        /// For top parent context model field, provide field name expression in format TopParent.FieldName
        /// </summary>
        /// <param name="FieldNameExpression"></param>
        /// <returns></returns>
        protected (bool ClassPropertyExists, Type FieldPropertyType, object FieldValue) GetContextModelFieldValue(string FieldNameExpression)
        {
            if (_contextModel != null)
            {
                DbEntityEntry targetModel = _contextModel;
                string FieldName = FieldNameExpression;
                string[] _targetModelAndFieldName = FieldNameExpression.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                if(_targetModelAndFieldName.Length > 0
                    && (_targetModelAndFieldName[0].Equals("Parent", StringComparison.OrdinalIgnoreCase) || _targetModelAndFieldName[0].Equals("TopParent", StringComparison.OrdinalIgnoreCase))
                    )
                {
                    if (_targetModelAndFieldName[0].Equals("Parent", StringComparison.OrdinalIgnoreCase))
                    {
                        targetModel = _parentContextModel;
                        FieldName = string.Join(".", _targetModelAndFieldName.Skip(1));
                        if (targetModel == null)
                        {
                            return (false, null, null);
                        }
                    }
                    else if (_targetModelAndFieldName[0].Equals("TopParent", StringComparison.OrdinalIgnoreCase))
                    {
                        targetModel = _topParentContextModel;
                        FieldName = string.Join(".", _targetModelAndFieldName.Skip(1));
                        if (targetModel == null)
                        {
                            return (false, null, null);
                        }
                    }
                }

                if (targetModel != null)
                {
                    if (targetModel.Entity.GetType().GetProperty(FieldName) == null)
                    {
                        return (false, null, null);
                    }

                    Type propertyType = Nullable.GetUnderlyingType(targetModel.Entity.GetType().GetProperty(FieldName).PropertyType) ?? targetModel.Entity.GetType().GetProperty(FieldName).PropertyType;
                    var fieldProperty = targetModel.Property(FieldName);
                    if (fieldProperty != null)
                    {
                        return (true, propertyType, fieldProperty.CurrentValue);
                    }
                }
            }

            //Just returning a null value doesnt makes it clear if the field / property value is null or the field / property doesnt exists in the model.
            return (false, null, null);
        }

        protected void UpdateContextModelField(string FieldNameExpression, object NewFieldValue, string UserUpdateComment)
        {
            //If entity was found with given key then perform update.
            if (_contextModel != null && _modelDbContext != null)
            {
                DbEntityEntry targetModel = _contextModel;
                DbContext targetDbContext = _modelDbContext;

                string FieldName = FieldNameExpression;
                string[] _targetModelAndFieldName = FieldNameExpression.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                if (_targetModelAndFieldName.Length > 0
                    && (_targetModelAndFieldName[0].Equals("Parent", StringComparison.OrdinalIgnoreCase) || _targetModelAndFieldName[0].Equals("TopParent", StringComparison.OrdinalIgnoreCase))
                    )
                {
                    if (_targetModelAndFieldName[0].Equals("Parent", StringComparison.OrdinalIgnoreCase))
                    {
                        targetModel = _parentContextModel;
                        targetDbContext = _parentContextModelDbContext;
                        if (targetModel == null)
                        {
                            throw new Exception("Parent context model not found for field update: " + FieldNameExpression);
                        }
                        FieldName = string.Join(".", _targetModelAndFieldName.Skip(1));
                    }
                    else if (_targetModelAndFieldName[0].Equals("TopParent", StringComparison.OrdinalIgnoreCase))
                    {
                        targetModel = _topParentContextModel;
                        targetDbContext = _topParentContextModelDbContext;
                        if (targetModel == null)
                        {
                            throw new Exception("Top Parent context model not found for field update: " + FieldNameExpression);
                        }
                        FieldName = string.Join(".", _targetModelAndFieldName.Skip(1));
                    }
                }

                targetModel.State = EntityState.Modified;

                //Set all other fields in the model as Not modified except for the field that has to be modified.
                PropertyInfo[] _modelProperties = targetModel.Entity.GetType().GetProperties().Where(p => !p.GetMethod.IsVirtual && !Attribute.IsDefined(p, typeof(NotMappedAttribute))).ToArray();
                foreach (PropertyInfo _propInfo in _modelProperties)
                {
                    if (_propInfo.Name.Equals(FieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetModel.Property(_propInfo.Name).IsModified = true;
                        Type type = Nullable.GetUnderlyingType(_propInfo.PropertyType) ?? _propInfo.PropertyType;
                        if (NewFieldValue == null)
                        {
                            targetModel.Property(_propInfo.Name).CurrentValue = null;
                        }
                        else
                        {
                            if (type.Equals(typeof(DateTime)))
                            {
                                targetModel.Property(_propInfo.Name).CurrentValue = DateTime.ParseExact(NewFieldValue.ToString(), Culture.CurrentDateFormat, CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                targetModel.Property(_propInfo.Name).CurrentValue = Convert.ChangeType(NewFieldValue, type);
                            }
                        }
                    }
                    else
                    {
                        if (!((SecureModelBase)targetModel.Entity).GetKeyFieldName().Equals(_propInfo.Name))
                        {
                            targetModel.Property(_propInfo.Name).IsModified = false;
                        }
                    }
                }

                //Set user comment that is to be updated in the audit trail records.
                CurrentContext.User_DataUpdateComment = UserUpdateComment + " (Workflow: \"" + Workflow_Transactions_Headers.Workflow.Name + "\", Step: \"" + Workflow_Steps.Name + "\", txn sid: " + ID + ")";

                //Save changes.
                targetDbContext.SaveChanges();
            }
        }

        public void PopulateContextObjectModel(ClientModel db)
        {
            if (_contextModel == null || _modelDbContext == null)
            {
                string strContextObjectCode = Workflow_Transactions_Headers.Context_Object_Code;
                string strModelClassName = "";
                string strRefID = Workflow_Transactions_Headers.Object_RefID.ToString();
                ObjectConfiguration objConfig = new ObjectConfiguration();

                strModelClassName = objConfig.GetObjectModelClassName(strContextObjectCode);

                string strDBContextClassName = objConfig.GetDBContextClassNameForModelClass(strModelClassName);

                //Instantiate db context class object.
                Type _typeDbContextClass = Type.GetType(strDBContextClassName);
                object[] constructorArgs = new object[] { db.Database.Connection, null };
                _modelDbContext = (DbContext)Activator.CreateInstance(_typeDbContextClass, constructorArgs);

                //Instantiate model object whose specified field needs to be updated with new value.
                Type _typeModelClass = Type.GetType(strModelClassName);
                DbSet obDbSet = _modelDbContext.Set(_typeModelClass);
                int _strRefID = Convert.ToInt32(strRefID);
                _contextModel = _modelDbContext.Entry(obDbSet.Find(new object[] { _strRefID }));

                //Get parent context model if any applies for this object type.
                if (strContextObjectCode.Equals(new Client_OrderItems().GetThisObjectCode(), StringComparison.OrdinalIgnoreCase))
                {
                    //Parent is Client_Orders
                    string parentObjectCode = new Client_Orders().GetThisObjectCode();
                    string parentModelClassName = objConfig.GetObjectModelClassName(parentObjectCode);
                    string parentDBContextClassName = objConfig.GetDBContextClassNameForModelClass(parentModelClassName);
                    //Instantiate db context class object.
                    Type parent_typeDbContextClass = Type.GetType(parentDBContextClassName);
                    constructorArgs = new object[] { db.Database.Connection, null };
                    _parentContextModelDbContext = (DbContext)Activator.CreateInstance(parent_typeDbContextClass, constructorArgs);
                    //Instantiate model object whose specified field needs to be updated with new value.
                    Type parent_typeModelClass = Type.GetType(parentModelClassName);
                    DbSet parent_obDbSet = _parentContextModelDbContext.Set(parent_typeModelClass);
                    int parent_strRefID = Convert.ToInt32(_contextModel.Property(nameof(Client_OrderItems.Client_Orders_ID)).CurrentValue);
                    _parentContextModel = _parentContextModelDbContext.Entry(parent_obDbSet.Find(new object[] { parent_strRefID }));
                }

                //Get top parent context model which will usually be the Accounts or Clients model.
                using (ObjectsModel relationsModel = new ObjectsModel(db.Database.Connection))
                {
                    string _associatedObjectCode;
                    int _associatedObjectRefId;
                    if (_parentContextModel != null)
                    {
                        _associatedObjectCode = ((ObjectModelBase)_parentContextModel.Entity).GetThisObjectCode();
                        _associatedObjectRefId = Convert.ToInt32(_parentContextModel.Property("ID").CurrentValue);
                    }
                    else
                    {
                        _associatedObjectCode = strContextObjectCode;
                        _associatedObjectRefId = _strRefID;
                    }

                    ObjectRelations objectRelation = relationsModel.ObjectRelations.AsNoTracking().FirstOrDefault(r => r.AssociatedObject_Code == _associatedObjectCode && r.AssociatedObject_RefID == _associatedObjectRefId && r.IsActive == true);

                    string topParentObjectCode = objectRelation.ObjectScreen_ObjectCode;
                    string topParentModelClassName = objConfig.GetObjectModelClassName(topParentObjectCode);
                    string topParentDBContextClassName = objConfig.GetDBContextClassNameForModelClass(topParentModelClassName);
                    //Instantiate db context class object.
                    Type topParent_typeDbContextClass = Type.GetType(topParentDBContextClassName);
                    constructorArgs = new object[] { db.Database.Connection, null };
                    _topParentContextModelDbContext = (DbContext)Activator.CreateInstance(topParent_typeDbContextClass, constructorArgs);
                    //Instantiate model object whose specified field needs to be updated with new value.
                    Type topParent_typeModelClass = Type.GetType(topParentModelClassName);
                    DbSet topParent_obDbSet = _topParentContextModelDbContext.Set(topParent_typeModelClass);
                    int topParent_strRefID = objectRelation.ObjectScreen_RefID;
                    _topParentContextModel = _topParentContextModelDbContext.Entry(topParent_obDbSet.Find(new object[] { topParent_strRefID }));
                }
            }
        }

        #endregion
    }
}
