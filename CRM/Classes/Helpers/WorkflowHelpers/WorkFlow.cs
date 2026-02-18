using CRM.Business.Workflows;
using CRM.Business.Workflows.Config;
using CRM.Business.Workflows.StepTypesConfigObject;
using CRM.Models.Clients;
using CRM.Models.ECom;
using CRM.Models.Workflows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.Linq;
using System.Linq.Dynamic;
using System.Runtime.ExceptionServices;
using System.Transactions;
using static CRM.Models.Workflows.Workflow;

namespace CRM.Classes.Helpers.WorkflowHelpers
{
    public class WorkFlow
    {
        public enum WorkflowExists_Status
        {
            None,
            InProgress_Workflow,
            Completed_Workflow
        }

        public enum WorkflowStart_Status
        {
            Success,
            InProgress_Workflow_Exists,
            Completed_Workflow_Exists,
            Unknown_Error
        }

        public List<WorkflowStatus> GetOpenWorkflowsStatus(string ContextObjectCode, int iObjectRefID, DbConnection p_dbConn = null)
        {
            List<WorkflowStatus> workflowStatusList = new List<WorkflowStatus>();
            DbConnection conn = p_dbConn;

            try
            {
                if (conn == null)
                {
                    conn = EFHelper.GetDbConnection();
                }

                using (var db = new ClientModel(conn))
                {
                    //Get primary object workflow list.
                    var workflowList = db.Workflows.Where(w => w.Context_Object_Code.Equals(ContextObjectCode, StringComparison.OrdinalIgnoreCase)).Include(w => w.Workflow_Transactions_Headers.Select(h => h.Workflow_StepTransactions.Select(t => t.Workflow_Steps))).ToList();
                    foreach (var workflow in workflowList)
                    {
                        var workflowStatus = workflow.GetWorkflowStatus(iObjectRefID);
                        workflowStatusList.AddRange(workflowStatus);
                    }

                    //Get related objects workflow list for Account object.
                    if(ContextObjectCode.Equals(new Accounts().GetThisObjectCode()))
                    {
                        //Find account.
                        var _account = db.Accounts.Where(a => a.ID == iObjectRefID).AsNoTracking().FirstOrDefault();
                        //List all invoices in account.
                        var _invoicesList = _account.ListInvoices(enInvoiceStatus: InvoiceStatus.All, dbConn: db.Database.Connection);
                        //List all corresponding orders and then finally list all order items in that will need to be checked in workflow for open any order item open workflow.
                        var _orderIDsList = _invoicesList.Where(i => i.Client_Orders_ID != null).Select(i => i.Client_Orders_ID).ToList();
                        using (var ecommDb = new EComModel(db.Database.Connection))
                        {
                            var _orderItemsList = ecommDb.Client_OrderItems.Where(i => _orderIDsList.Contains(i.Client_Orders_ID)).Select(i => i.ID).ToArray();
                            string orderItemObjectCode = new Client_OrderItems().GetThisObjectCode();
                            workflowStatusList.AddRange(GetRelatedObjectsOpenWorkflowsStatus(orderItemObjectCode, _orderItemsList.ToArray(), db.Database.Connection));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ExceptionDispatchInfo.Capture(e).Throw();
            }
            finally
            {
                if (p_dbConn == null)
                {
                    conn.Dispose();
                }
            }

            return workflowStatusList;
        }

        public List<WorkflowStatus> GetRelatedObjectsOpenWorkflowsStatus(string ContextObjectCode, int[] ObjectRefIDList, DbConnection p_dbConn = null)
        {
            List<WorkflowStatus> workflowStatusList = new List<WorkflowStatus>();
            DbConnection conn = p_dbConn;

            try
            {
                if (conn == null)
                {
                    conn = EFHelper.GetDbConnection();
                }

                using (var db = new ClientModel(conn))
                {
                    //Create a list of references to open workflow so that resource expensive method Workflow.GetWorkflowStatus() is called only on open or recently closed workflows.
                    var openOrRecentlyClosedWorkflows = db.Workflow_Transactions_Headers.Where(t => (t.DelFlag == null || t.DelFlag == false) &&
                                t.Context_Object_Code.Equals(ContextObjectCode, StringComparison.OrdinalIgnoreCase)
                                && ObjectRefIDList.Contains(t.Object_RefID)
                                && (!(t.WorkflowComplete ?? false) || (DbFunctions.DiffDays(DateTime.UtcNow, t.WorkflowComplete_Time) ?? 0) <= m_ListRecentlyCompletedWorkflows_NumberOfDays)
                                ).Include(t => t.Workflow).Include(w => w.Workflow_StepTransactions.Select(t => t.Workflow_Steps)).ToList();
                    foreach (var workflow in openOrRecentlyClosedWorkflows)
                    {
                        if (workflow.Workflow_StepTransactions != null)
                        {
                            workflow.Workflow_StepTransactions = workflow.Workflow_StepTransactions
                                .Where(st => (st.DelFlag == null || st.DelFlag == false))
                                .ToList();
                        }
                        var workflowStatus = workflow.Workflow.GetWorkflowStatus(workflow.Object_RefID);
                        //Mark each workflow status object as related workflow.
                        foreach (var relatedWworkflowStatus in workflowStatus)
                        {
                            relatedWworkflowStatus.IsRelatedWorkflow = true;
                        }
                        workflowStatusList.AddRange(workflowStatus);
                    }
                }
            }
            catch (Exception e)
            {
                ExceptionDispatchInfo.Capture(e).Throw();
            }
            finally
            {
                if (p_dbConn == null)
                {
                    conn.Dispose();
                }
            }

            return workflowStatusList;
        }


        /// <summary>
        /// Returns WorkflowStart_Status type:
        /// Success: A new workflow was started successfully.
        /// InProgress_Workflow_Exists: An open workflow already exists.
        /// Completed_Workflow_Exists: A closed / completed workflow already exists.
        /// </summary>
        public (WorkflowStart_Status workflowStart_Status, Workflow_Transactions_Headers workflow_Transactions_Header) Start(WorkflowCode WorkflowCode, string ContextObjectCode, int iObjectRefID
            , int? workflowTriggerSource_Workflow_StepTransactions_ID = null
            , bool bStartNewWorkflowEvenIfAnyCompletedWorkflowExists = false
            , int? dependsOnWorkflowTransactionHeaderID = null
            , List<TriggerWorkflow_ConfigData_ContextObjectRefID_Map> triggerWorkflowsList = null
            , string additionalWorkflowDescription = null
            , DbConnection p_dbConn = null)
        {
            WorkflowStart_Status iResult = WorkflowStart_Status.Success;
            DbConnection conn = p_dbConn;
            if (dependsOnWorkflowTransactionHeaderID != null && workflowTriggerSource_Workflow_StepTransactions_ID != null)
            {
                throw new Exception("Specify either of \"dependsOnWorkflowTransactionHeaderID\" or \"workflowTriggerSource_Workflow_StepTransactions_ID\" parameters, not both together.");
            }
            try
            {
                if (conn == null)
                {
                    conn = EFHelper.GetDbConnection();
                }

                var workflowStatus = WorkflowExists(WorkflowCode, ContextObjectCode, iObjectRefID, conn);
                switch (workflowStatus.workflowExists_Status)
                {
                    case WorkflowExists_Status.InProgress_Workflow:
                        return (WorkflowStart_Status.InProgress_Workflow_Exists, workflowStatus.workflow_Transactions_Header);
                    case WorkflowExists_Status.Completed_Workflow:
                        if (!bStartNewWorkflowEvenIfAnyCompletedWorkflowExists)
                        {
                            return (WorkflowStart_Status.Completed_Workflow_Exists, workflowStatus.workflow_Transactions_Header);
                        }
                        break;
                }

                //Validate workflow.
                CreateInMemoryWorkflowMap(WorkflowCode, conn);

                var transactionOptions = new TransactionOptions();
                transactionOptions.Timeout = new System.TimeSpan(0, 10, 0);
                using (TransactionScope scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions))
                {
                    using (var db = new ClientModel(conn))
                    {
                        //Get matching Workflow
                        var workflow = db.Workflows.Where(w => w.Context_Object_Code.Equals(ContextObjectCode, System.StringComparison.OrdinalIgnoreCase)
                                        && w.Code.Equals(WorkflowCode.ToString(), System.StringComparison.OrdinalIgnoreCase)
                                        ).FirstOrDefault();

                        if (!(workflow.IsActive ?? false))
                        {
                            throw new Exception("CRM.Classes.Helpers.Workflow::Start(): Workflow with Workflow code " + WorkflowCode.ToString() + " and Object code " + ContextObjectCode + " is not active.");
                        }

                        //Get first step in the Workflow.
                        var workflowStep = db.Workflow_Steps.Where(ws => ws.Workflows_ID == workflow.ID && (ws.IsStartStep ?? false) == true).FirstOrDefault();
                        if (workflowStep == null) 
                        {
                            throw new Exception("No starting step defined for Workflow code: " + workflow.Code + ".");
                        }

                        //Create transaction header and step transaction.
                        Workflow_Transactions_Headers workflow_Transactions_Header = new Workflow_Transactions_Headers()
                        {
                            Workflows_ID = workflow.ID,
                            Context_Object_Code = workflow.Context_Object_Code,
                            Object_RefID = iObjectRefID,
                            WorkflowTriggerSource_Workflow_StepTransactions_ID = workflowTriggerSource_Workflow_StepTransactions_ID
                        };

                        if(dependsOnWorkflowTransactionHeaderID != null)
                        {
                            workflow_Transactions_Header.DependsOn_WorkFlows_Workflow_Transactions_Headers_ID = dependsOnWorkflowTransactionHeaderID;
                            workflow_Transactions_Header.DependsOn_WorkFlows_WaitingOnCompletion = true;
                        }

                        if(triggerWorkflowsList != null)
                        {
                            workflow_Transactions_Header.ConfigData = ParseTriggerWorkflowInfoAndUpdateConfigData(triggerWorkflowsList, workflow_Transactions_Header);
                        }

                        if(!string.IsNullOrWhiteSpace(additionalWorkflowDescription))
                        {
                            var configDataObject = workflow_Transactions_Header.ParseConfigData();
                            configDataObject.AdditionalWorkflowDescription = additionalWorkflowDescription;
                            workflow_Transactions_Header.UpdateConfigData(configDataObject);
                        }

                        db.Workflow_Transactions_Headers.Add(workflow_Transactions_Header);
                        db.SaveChanges();

                        Workflow_StepTransactions workflow_StepTransactions = new Workflow_StepTransactions()
                        {
                            Workflow_Steps_ID = workflowStep.ID,
                            Workflow_Transactions_Headers_ID = workflow_Transactions_Header.ID,
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
                        
                        scope.Complete();
                        return (iResult, workflow_Transactions_Header);
                    }
                }
            }
            catch (Exception e)
            {
                ExceptionDispatchInfo.Capture(e).Throw();
            }
            finally
            {
                if (p_dbConn == null)
                {
                    conn.Dispose();
                }
            }

            return (WorkflowStart_Status.Unknown_Error, null);
        }

        protected string ParseTriggerWorkflowInfoAndUpdateConfigData(List<TriggerWorkflow_ConfigData_ContextObjectRefID_Map> triggerWorkflowsList, Workflow_Transactions_Headers workflow_Transactions_Header)
        {
            string _configData = null;

            var _configDataObject = workflow_Transactions_Header.ParseConfigData();

            //Search for each step in this workflow corresponding to list of trigger workflow codes specified and then add tagret object code and object ref id against each step in ConfigData.
            var workflowSteps = workflow_Transactions_Header.Workflow.Workflow_Steps.Where(s => s.Workflow_StepTypes_ID == (byte)Workflow_StepTypes.StepType.TriggerWorkflow).ToList();
            if(workflowSteps.Count <= 0)
            {
                throw new Exception("ParseTriggerWorkflowInfoAndUpdateConfigData(): Trigger Workflows List was specified but no workflow step was found of type " + Workflow_StepTypes.StepType.TriggerWorkflow.ToString() + ".");
            }

            List<TriggerWorkflow_ConfigData_ContextObjectRefID_Map> _triggerWorkflowsList_Processed = new List<TriggerWorkflow_ConfigData_ContextObjectRefID_Map>();
            foreach (var step in workflowSteps)
            {
                if(string.IsNullOrWhiteSpace(step.Workflow_StepTypes_ConfigData))
                {
                    throw new Exception("ParseTriggerWorkflowInfoAndUpdateConfigData(): Step ID:" + step.ID + " cannot have empty Workflow_StepTypes_ConfigData.");
                }

                var StepType_ConfigData = (TriggerWorkflow)step.Workflow_StepType.ParseConfigData(step.Workflow_StepTypes_ConfigData);
                if(StepType_ConfigData == null || StepType_ConfigData.WorkflowCodes.Length <= 0)
                {
                    throw new Exception("ParseTriggerWorkflowInfoAndUpdateConfigData(): Step ID:" + step.ID + " has an invalid Workflow_StepTypes_ConfigData value or no workflow codes are defined.");
                }

                //Add Step ID if not already in config data.
                if (!_configDataObject.TriggerWorkflow.Exists(c => c.StepID == step.ID)) 
                {
                    _configDataObject.TriggerWorkflow.Add(new TriggerWorkflow_ConfigData { StepID = step.ID });
                }

                //List all workflow and context object ids for corresponding WorkflowCodes listed in config of this step.
                var _selection = triggerWorkflowsList.Where(w => StepType_ConfigData.WorkflowCodes.Contains(w.WflowCode)).ToList();
                _configDataObject.TriggerWorkflow.Where(c => c.StepID == step.ID).FirstOrDefault().TargetObject.AddRange(_selection);

                //Add to processing list those that are not already added to filter out the ones that are not processed so that it can be flagged.
                _triggerWorkflowsList_Processed.AddRange(_selection);
            }

            _configData = JsonConvert.SerializeObject(_configDataObject);

            return _configData;
        }

        /// <summary>
        /// Returns an integer:
        /// 0: No workflow exists.
        /// 1: An open workflow exists.
        /// 2: A closed / concluded workflow exists.
        /// </summary>
        public (WorkflowExists_Status workflowExists_Status, Workflow_Transactions_Headers workflow_Transactions_Header) WorkflowExists(WorkflowCode WorkflowCode, string ContextObjectCode, int iObjectRefID, DbConnection dbConn = null)
        {
            WorkflowExists_Status iStatus = WorkflowExists_Status.None;
            Workflow_Transactions_Headers workflow_Transactions_Headers = null;

            using (var db = new ClientModel(dbConn))
            {
                var workflow = db.Workflows.Where(w => w.Context_Object_Code.Equals(ContextObjectCode, System.StringComparison.OrdinalIgnoreCase)
                                && w.Code.Equals(WorkflowCode.ToString(), System.StringComparison.OrdinalIgnoreCase)
                                ).FirstOrDefault();

                if (workflow != null)
                {
                    workflow_Transactions_Headers = db.Workflow_Transactions_Headers.Where(wh => wh.Context_Object_Code.Equals(ContextObjectCode, System.StringComparison.OrdinalIgnoreCase)
                    && wh.Workflows_ID == workflow.ID
                    && wh.Object_RefID == iObjectRefID
                    ).FirstOrDefault();

                    if (workflow_Transactions_Headers != null)
                    {
                        if (workflow_Transactions_Headers.WorkflowComplete ?? false)
                            iStatus = WorkflowExists_Status.Completed_Workflow;
                        else
                            iStatus = WorkflowExists_Status.InProgress_Workflow;
                    }
                }

            }

            return (iStatus, workflow_Transactions_Headers);
        }

        public WorkflowStepsMap CreateInMemoryWorkflowMap(WorkflowCode WorkflowCode, DbConnection p_dbConn = null)
        {
            DbConnection conn = p_dbConn;

            try
            {
                if (conn == null)
                {
                    conn = EFHelper.GetDbConnection();
                }

                using (var db = new ClientModel(conn))
                {
                    //Get matching Workflow
                    var workflow = db.Workflows.Where(w => w.Code.Equals(WorkflowCode.ToString(), System.StringComparison.OrdinalIgnoreCase) && (w.DelFlag ?? false) == false).FirstOrDefault();

                    if (workflow == null)
                    {
                        throw new Exception("CRM.Classes.Helpers.Workflow::Start(): No Workflow found with Workflow code " + WorkflowCode.ToString() + ".");
                    }

                    //Create a local list of all steps for the specified Workflow from database.
                    var workflowStepsList = db.Workflow_Steps.Where(ws => ws.Workflows_ID == workflow.ID && (ws.IsActive ?? false) == true && (ws.DelFlag ?? false) == false).ToList();

                    //Add step type config data template if the config data column is null or empty.
                    bool bWorkflowStepConfigDataUpdated = false;
                    foreach (var workflowStep in workflowStepsList)
                    {
                        if (string.IsNullOrWhiteSpace(workflowStep.Workflow_StepTypes_ConfigData))
                        {
                            workflowStep.Workflow_StepTypes_ConfigData = workflowStep.Workflow_StepType.GetConfigDataTemplateJson();
                            db.Entry(workflowStep).State = System.Data.Entity.EntityState.Modified;
                            bWorkflowStepConfigDataUpdated = true;
                        }
                    }

                    if (bWorkflowStepConfigDataUpdated)
                    {
                        db.SaveChanges();
                    }

                    var workflowStepsMapBuilder = new WorkflowMapBuilder();

                    return workflowStepsMapBuilder.CreateInMemoryWorkflowMap(workflowStepsList);
                }
            }
            catch (Exception e)
            {
                ExceptionDispatchInfo.Capture(e).Throw();
            }
            finally
            {
                if (p_dbConn == null)
                {
                    conn.Dispose();
                }
            }

            return null;
        }

        /* Replaced by WorkflowMapBuilder class to with improved circular reference check and better code manageability. Keeping the old code commented for reference for now.
        private WorkflowStepsMap m_WorkflowStepsMap;
        public WorkflowStepsMap CreateInMemoryWorkflowMap(WorkflowCode WorkflowCode, DbConnection p_dbConn = null)
        {
            DbConnection conn = p_dbConn;

            try
            {
                if (conn == null)
                {
                    conn = EFHelper.GetDbConnection();
                }

                using (var db = new ClientModel(conn))
                {
                    //Get matching Workflow
                    var workflow = db.Workflows.Where(w => w.Code.Equals(WorkflowCode.ToString(), System.StringComparison.OrdinalIgnoreCase) && (w.DelFlag ?? false) == false).FirstOrDefault();

                    if (workflow == null)
                    {
                        throw new Exception("CRM.Classes.Helpers.Workflow::Start(): No Workflow found with Workflow code " + WorkflowCode.ToString() + ".");
                    }

                    //Create a local list of all steps for the specified Workflow from database.
                    var workflowStepsList = db.Workflow_Steps.Where(ws => ws.Workflows_ID == workflow.ID && (ws.IsActive ?? false) == true && (ws.DelFlag ?? false) == false).ToList();

                    //Add step type config data template if the config data column is null or empty.
                    bool bWorkflowStepConfigDataUpdated = false;
                    foreach (var workflowStep in workflowStepsList) 
                    { 
                        if(string.IsNullOrWhiteSpace(workflowStep.Workflow_StepTypes_ConfigData))
                        {
                            workflowStep.Workflow_StepTypes_ConfigData = workflowStep.Workflow_StepType.GetConfigDataTemplateJson();
                            db.Entry(workflowStep).State = System.Data.Entity.EntityState.Modified;
                            bWorkflowStepConfigDataUpdated = true;
                        }
                    }

                    if (bWorkflowStepConfigDataUpdated) 
                    {
                        db.SaveChanges();
                    }

                    //Validate there is one Start step.
                    var workflowFirstStep = workflowStepsList.Where(ws => ws.Workflows_ID == workflow.ID && (ws.IsStartStep ?? false) == true).ToList();
                    if (workflowFirstStep.Count > 1)
                    {
                        throw new Exception("CRM.Classes.Helpers.Workflow::Start(): Setup error: More than one first steps found for Workflow code " + WorkflowCode.ToString() + ".");
                    }
                    else if (workflowFirstStep.Count < 1)
                    {
                        throw new Exception("CRM.Classes.Helpers.Workflow::Start(): Setup error: No first step found for Workflow code " + WorkflowCode.ToString() + ".");
                    }

                    //Create workflow map
                    m_WorkflowStepsMap = new WorkflowStepsMap();
                    m_WorkflowStepsMap.WorkflowStep = workflowFirstStep[0];

                    m_iExpandStep_FunctionRecurseCounter = 0;
                    BuildStep(workflowStepsList, m_WorkflowStepsMap);
                }
            }
            catch (Exception e)
            {
                ExceptionDispatchInfo.Capture(e).Throw();
            }
            finally
            {
                if (p_dbConn == null)
                {
                    conn.Dispose();
                }
            }

            return m_WorkflowStepsMap;
        }

        private short m_iExpandStep_FunctionRecurseCounter = 0;
        private short m_iExpandStep_FunctionRecurseCount = 500;
        private void BuildStep(List<Workflow_Steps> workflowStepsList, WorkflowStepsMap workflowStepsMap)
        {
            if (++m_iExpandStep_FunctionRecurseCounter > m_iExpandStep_FunctionRecurseCount)
            {
                throw new Exception("Maximum configured ExpandStep() function recurse count of " + m_iExpandStep_FunctionRecurseCount + " reached.");
            }

            if (!workflowStepsMap.IsStepExpansionProcessed)
            {
                workflowStepsMap.NextSteps = new List<WorkflowStepsMap>();

                //Next steps specified.
                if (workflowStepsMap.WorkflowStep.Workflow_Steps_NextStep_ID != null)
                {
                    var nextSteps = workflowStepsList.Where(ws => ws.ID == workflowStepsMap.WorkflowStep.Workflow_Steps_NextStep_ID.Value).ToList();
                    foreach (var nextStep in nextSteps)
                    {
                        //Check if the step is already added to map in which case it is a circular reference situation.
                        if(FindStepInWorkflowStepMap(nextStep, workflowStepsMap) != null)
                        {
                            throw new Exception("Circular reference found. While processing Workflow Step ID " + workflowStepsMap.WorkflowStep.ID + " it looped back to Workflow Step ID " + nextStep.ID+ ".");
                        }

                        workflowStepsMap.NextSteps.Add(new WorkflowStepsMap { WorkflowStep = nextStep, _ParentStepMap = workflowStepsMap });
                    }

                }

                //Check if this step is in previous step of other steps which is the case of one-to-many relation that is one step splitting into mlutiple parallel steps.
                var parallelSteps = workflowStepsList.Where(ws => ws.Workflow_Steps_PreviousStep_ID == workflowStepsMap.WorkflowStep.ID).ToList();
                if (parallelSteps.Count> 0)
                {
                    foreach (var nextStep in parallelSteps)
                    {
                        //Check if the step is already added to map in which case it is a circular reference situation.
                        if (FindStepInWorkflowStepMap(nextStep, workflowStepsMap) != null)
                        {
                            throw new Exception("Circular reference found. While processing Workflow Step ID " + workflowStepsMap.WorkflowStep.ID + " for parallel steps it looped back to Workflow Step ID " + nextStep.ID + ".");
                        }

                        workflowStepsMap.NextSteps.Add(new WorkflowStepsMap { WorkflowStep = nextStep, _ParentStepMap = workflowStepsMap });
                    }

                }

                workflowStepsMap.IsStepExpansionProcessed = true;

                //Recurse to build all steps.
                foreach(var workflowMapStep in workflowStepsMap.NextSteps)
                {
                    BuildStep(workflowStepsList, workflowMapStep);
                }
            }
        }
        */

        public void UpdateWorkflow_StepType_ConfigDataTemplate()
        {
            using (var db = new ClientModel())
            {
                foreach(var StepType in db.Workflow_StepTypes.ToList())
                {
                    StepType.UpdateConfigDataTemplateField();
                }

                db.SaveChanges();
            }
        }

        public WorkflowStepsMap FindStepInWorkflowStepMap(Workflow_Steps searchWorkflowStep, WorkflowStepsMap workflowStepsMap)
        {
            if(searchWorkflowStep.ID == workflowStepsMap.WorkflowStep.ID)
            {
                return workflowStepsMap;
            }
            else
            {
                if (workflowStepsMap.NextSteps != null)
                {
                    foreach (var step in workflowStepsMap.NextSteps)
                    {
                        var nextWorkflowStepsMap = FindStepInWorkflowStepMap(searchWorkflowStep, step);
                        if (nextWorkflowStepsMap != null)
                            return nextWorkflowStepsMap;
                    }
                }
            }

            return null;
        }

        public static bool IsValidActionResponseJson(string jsonString)
        {
            bool bValid = false;
            try
            {
                var jsonResponseObject = JObject.Parse(jsonString);
                if (jsonResponseObject != null && jsonResponseObject.ContainsKey(nameof(WorkflowStepActionParameters.sid)))
                {
                    bValid = true;
                }
            }
            catch (Exception)
            {
                throw;
            }

            return bValid;
        }
    }
}
