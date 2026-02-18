using CRM.Business.Workflows;
using CRM.Business.Workflows.PostStepCompletionTaskTypes;
using CRM.Business.Workflows.PreStepCompletionTaskTypes;
using CRM.Business.Workflows.StepTypesConfigObject;
using CRM.Classes.Helpers;
using CRM.Classes.Helpers.WorkflowHelpers;
using CRM.Classes.Results;
using CRM.Models.Clients;
using CRM.Models.Workflows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http.Formatting;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Web.Classes;
using static Web.Controllers.WebAPI.Workflow.WorkflowController;

namespace Web.Controllers.WebAPI.Workflow
{
    [CRMAuthorizeApi]
    [Authorize]
    public class WorkflowController : ApiControllerBase
    {
        private ClientModel db = new ClientModel();

        //Process workflow status
        [AcceptVerbs("POST")]
        [ActionName("GetWorkflowStatus")]
        public async Task<IHttpActionResult> GetWorkflowStatus()
        {
            APICallResponse response = new APICallResponse();
            response.ResponseCode = "0";

            string jsonString = await Request.Content.ReadAsStringAsync();
            var jsonRequest = JObject.Parse(jsonString);

            if (jsonRequest == null || !jsonRequest.ContainsKey("__obc") || !jsonRequest.ContainsKey("__obc_refid"))
            {
                response.ErrorCode = "-1";
                response.ErrorMessage = "Invalid Json request string.";
                return Ok(response);
            }
            else
            {
                WorkFlow workFlow = new WorkFlow();
                var workFlowStatus = workFlow.GetOpenWorkflowsStatus(jsonRequest["__obc"].ToString(), int.Parse(jsonRequest["__obc_refid"].ToString()));
                response.AdditionalData1 = JsonConvert.SerializeObject(workFlowStatus);
                response.ResponseCode = "1";
                response.ResponseMessage = "Success";
            }

            return Ok(response);
        }


        //Process workflow status for specific workflow instance
        [AcceptVerbs("POST","GET")]
        [ActionName("GetWorkflowInstanceStatus")]
        public IHttpActionResult GetWorkflowInstanceStatus(int workflowTxnHeaderID)
        {
            APICallResponse response = new APICallResponse();
            response.ResponseCode = "0";

            var workflowTransactionHeader = db.Workflow_Transactions_Headers.Where(w => w.ID == workflowTxnHeaderID).Include(w => w.Workflow).Include(w => w.Workflow_StepTransactions.Select(t => t.Workflow_Steps)).FirstOrDefault();
            if(workflowTransactionHeader != null)
            {
                var workFlowStatus = workflowTransactionHeader.GetWorkflowStatus();
                response.AdditionalData1 = JsonConvert.SerializeObject(workFlowStatus);
                response.ResponseCode = "1";
                response.ResponseMessage = "Success";
            }

            return Ok(response);
        }


        //Process workflow step response
        [AcceptVerbs("POST")]
        [ActionName("ProcessResponse")]
        public async Task<IHttpActionResult> ProcessResponse()
        {
            APICallResponse response = new APICallResponse();
            response.ResponseCode = "0";

            string jsonString = await Request.Content.ReadAsStringAsync();
            if (!WorkFlow.IsValidActionResponseJson(jsonString))
            {
                response.ErrorCode = "-1";
                response.ErrorMessage = "Invalid Json response string.";
                return Ok(response);
            }
            else
            {
                try
                {
                    var responseObject = JsonConvert.DeserializeObject<WorkflowStepActionParameters>(jsonString);
                    var workflowStepTxn = db.Workflow_StepTransactions.Find(responseObject.sid);
                    if (workflowStepTxn != null)
                    {
                        db.Entry(workflowStepTxn).State = System.Data.Entity.EntityState.Detached;
                        workflowStepTxn.ProcessStep(responseObject.msg, jsonString);
                    }
                    else
                    {
                        return NotFound();
                    }
                }
                catch (Exception ex)
                {
                    response.ErrorCode = "-1";
                    response.ErrorMessage = "Error: " + ex.Message;
                    return Ok(response);
                }
            }

            response.ResponseCode = "1";
            response.ResponseMessage = "Success";

            return Ok(response);
        }

        //Get json templates
        [AcceptVerbs("GET")]
        [ActionName("ConfigJsonTemplate")]
        public IHttpActionResult ConfigJsonTemplate(string configType, string templateName)
        {
            object response = "Object with config type: \""+ configType + "\" and template name: \"" + templateName + "\" not found.";

            if (configType.Equals("StepTypeConfig", System.StringComparison.OrdinalIgnoreCase))
            {
                if (templateName.Equals("APICall", System.StringComparison.OrdinalIgnoreCase))
                {
                    response = new CRM.Business.Workflows.StepTypesConfigObject.APICall() { };
                }
                else if (templateName.Equals("ApproveReviewReject", System.StringComparison.OrdinalIgnoreCase))
                {
                    response = new ApproveReviewReject() { };
                }
                else if (templateName.Equals("SimpleStepCompletion", System.StringComparison.OrdinalIgnoreCase))
                {
                    response = new SimpleStepCompletion() { };
                }
                else if (templateName.Equals("TimedStepCompletion", System.StringComparison.OrdinalIgnoreCase))
                {
                    response = new TimedStepCompletion() { };
                }
                else if (templateName.Equals("TriggerWorkflow", System.StringComparison.OrdinalIgnoreCase))
                {
                    response = new TriggerWorkflow() { };
                }
            }
            else if (configType.Equals("PreStepCompletionConfig", System.StringComparison.OrdinalIgnoreCase))
            {
                if (templateName.Equals("DataValidation", System.StringComparison.OrdinalIgnoreCase))
                {
                    response = new DataValidations() { DataValidationList = new DataValidation[] { new DataValidation() } };
                }
            }
            else if (configType.Equals("StepCompletionConfig", System.StringComparison.OrdinalIgnoreCase))
            {
                if (templateName.Equals("Notifications", System.StringComparison.OrdinalIgnoreCase))
                {
                    response = new Notifications() { NotificationList = new Notification[] { new Notification() } };
                }
                else if (templateName.Equals("FieldUpdates", System.StringComparison.OrdinalIgnoreCase))
                {
                    response = new FieldUpdates() { FieldUpdateList = new FieldUpdate[] { new FieldUpdate() } };
                }
                else if (templateName.Equals("APICalls", System.StringComparison.OrdinalIgnoreCase))
                {
                    response = new APICalls() { APICallList = new CRM.Business.Workflows.PostStepCompletionTaskTypes.APICall[] { new CRM.Business.Workflows.PostStepCompletionTaskTypes.APICall() } };
                }
            }

            return Ok(response);
        }

        //Assign Task to User
        [AcceptVerbs("POST")]
        [ActionName("AssignTask")]
        public async Task<IHttpActionResult> AssignTask()
        {
            APICallResponse response = new APICallResponse();
            response.ResponseCode = "0";
            string responseMessage = "No operation performed.";

            string jsonString = await Request.Content.ReadAsStringAsync();
            if (!WorkFlow.IsValidActionResponseJson(jsonString))
            {
                response.ErrorCode = "-1";
                response.ErrorMessage = "Invalid Json string for user task assignment.";
                return Ok(response);
            }
            else
            {
                try
                {
                    var responseObject = JsonConvert.DeserializeObject<WorkflowStepTaskAssignerActionParameters>(jsonString);
                    var workflowStepTxn = db.Workflow_StepTransactions.Where(w => w.ID == responseObject.sid).Include(w => w.Workflow_Steps).FirstOrDefault();
                    if (workflowStepTxn != null)
                    {
                        db.Entry(workflowStepTxn).State = System.Data.Entity.EntityState.Detached;
                        workflowStepTxn.AssignStep(jsonString);
                    }
                    else
                    {
                        return NotFound();
                    }

                    if(responseObject.uid == 0)
                    {
                        responseMessage = "Task unassigned successfully.";
                    }
                    else
                    {
                        responseMessage = "Task assigned successfully.";
                    }
                }
                catch (Exception ex)
                {
                    response.ErrorCode = "-1";
                    response.ErrorMessage = "Error: " + ex.Message;
                    return Ok(response);
                }
            }

            response.ResponseCode = "1";
            response.ResponseMessage = responseMessage;

            return Ok(response);
        }



        [AcceptVerbs("POST")]
        [ActionName("List")]
        public IHttpActionResult GetWorkFlowStatus(FormDataCollection formData)
        {
            try
            {
                var obParams = new DatatableRequestParams(formData);

                // UI filters (not yet applied in SQL; wire them in if needed)
                var status = (formData["Task_Status"] ?? "").Trim();
                var workflowName = (formData["Workflow_Name"] ?? "").Trim();

                if (obParams.Pagination_PerPage <= 0) obParams.Pagination_PerPage = 10;
                if (obParams.Pagination_Page <= 0) obParams.Pagination_Page = 1;

                // Note: Account mapping is done via ObjectRelations for ANY object type (except direct ACCOUNT).
                // Optional “labels” light up only for specific object codes (INVOICES / COORDERITEMS / CLIENT).
                const string sql = @"
                    ;WITH base AS (
    SELECT
        h.ID WorkflowHeaderId,h.Context_Object_Code,h.Object_RefID,h.CreateDate Workflow_StartDate,w.Name Workflow_Name,
        st.ID Workflow_StepTransactions_ID,st.Assigned_Users_Id,(su.Firstname+' '+su.Lastname) Assigned_To_Name,(mgr.Firstname+' '+mgr.Lastname) Reporting_To_Name,
        s.ID Workflow_Steps_ID,s.Name Waiting_On_Step_Name,st.CreateDate as AssignedFromTime,
        CASE WHEN COALESCE(st.StepExecuted,0)=1 THEN 'Done'
             WHEN st.Assigned_Users_Id IS NOT NULL AND st.ActionedBy_Users_Id IS NULL THEN 'Waiting for Action'
             WHEN st.Assigned_Users_Id IS NOT NULL AND st.ActionedBy_Users_Id IS NOT NULL THEN 'In Progress'
             ELSE 'In Progress' END Workflow_Status,
        COALESCE(TRY_CAST(JSON_VALUE(s.Workflow_StepTypes_ConfigData,'$.EstimatedDaysToComplete') AS int),0) EstimatedDaysToComplete,
        JSON_VALUE(h.ConfigData,'$.AdditionalWorkflowDescription') AS DisplayName

    FROM Workflow_StepTransactions st
    JOIN Workflow_Transactions_Headers h ON h.ID=st.Workflow_Transactions_Headers_ID
    JOIN Workflow_Steps s ON s.ID=st.Workflow_Steps_ID
    JOIN Workflows w ON w.ID=h.Workflows_ID
    LEFT JOIN Security_Users su ON su.User_Id=st.Assigned_Users_Id
    LEFT JOIN Security_Users mgr ON mgr.User_Id=su.Supervisor_User_Id
    WHERE COALESCE(st.DelFlag,0)=0 AND COALESCE(h.DelFlag,0)=0 AND COALESCE(s.DelFlag,0)=0 AND COALESCE(w.DelFlag,0)=0 AND COALESCE(st.StepExecuted,0)=0
),
obj AS (
    SELECT b.*,
           CASE WHEN b.Context_Object_Code='ORDER' THEN b.Object_RefID WHEN b.Context_Object_Code='COORDERITEMS' THEN coi.Client_Orders_ID WHEN b.Context_Object_Code='INVOICES' THEN inv0.Client_Orders_ID ELSE NULL END OrderId,
           CASE WHEN b.Context_Object_Code='INVOICES' THEN b.Object_RefID END InvoiceId,
           CASE WHEN b.Context_Object_Code='COORDERITEMS' THEN b.Object_RefID END OrderItemId,
           coi.Products_ID
    FROM base b
    LEFT JOIN Client_OrderItems coi ON b.Context_Object_Code='COORDERITEMS' AND coi.ID=b.Object_RefID
    LEFT JOIN Invoices inv0 ON b.Context_Object_Code='INVOICES' AND inv0.ID=b.Object_RefID
),
acct AS (
    SELECT o.*,
           COALESCE(CASE WHEN o.Context_Object_Code='ACCOUNT' THEN o.Object_RefID END,or_order.ObjectScreen_RefID,or_obj.ObjectScreen_RefID) AccountId
    FROM obj o
    LEFT JOIN ObjectRelations or_order ON o.OrderId IS NOT NULL AND COALESCE(or_order.DelFlag,0)=0 AND or_order.ObjectScreen_Code='ACCOUNTS' AND or_order.AssociatedObject_Code IN ('Order','ORDER') AND or_order.AssociatedObject_RefID=o.OrderId
    LEFT JOIN ObjectRelations or_obj ON o.OrderId IS NULL AND o.Context_Object_Code<>'ACCOUNT' AND COALESCE(or_obj.DelFlag,0)=0 AND or_obj.ObjectScreen_Code='ACCOUNTS' AND or_obj.AssociatedObject_Code=o.Context_Object_Code AND or_obj.AssociatedObject_RefID=o.Object_RefID
)
SELECT
    a.AccountId,acc.Name AccountName,acc.ClientCode,
    inv.InvoiceNumber,prod.Name InvoiceItemName,

    /* show DisplayName (fallback to product/invoice item name if null) */
    COALESCE(NULLIF(a.DisplayName,''), prod.Name) AS DisplayName,

    a.WorkflowHeaderId,a.Workflow_Name,a.Waiting_On_Step_Name,
    a.Assigned_Users_Id,a.Assigned_To_Name,a.Reporting_To_Name,a.Workflow_Status,
    dr.DaysRemaining,PercentRemaining,
    CASE WHEN dr.DaysRemaining<0 THEN CONCAT(ABS(dr.DaysRemaining),' days overdue')
         WHEN dr.DaysRemaining=0 THEN 'Due today'
         ELSE CONCAT(dr.DaysRemaining,' days remaining') END DaysRemainingText,
    a.Workflow_StartDate
FROM acct a
LEFT JOIN Accounts acc ON acc.ID=a.AccountId
OUTER APPLY (SELECT TOP 1 i.InvoiceNumber FROM Invoices i WHERE (a.InvoiceId IS NOT NULL AND i.ID=a.InvoiceId) OR (a.InvoiceId IS NULL AND a.OrderId IS NOT NULL AND i.Client_Orders_ID=a.OrderId) ORDER BY i.CreateDate DESC) inv
LEFT JOIN Products prod ON prod.ID=a.Products_ID
CROSS APPLY (SELECT (a.EstimatedDaysToComplete - DATEDIFF(DAY,a.AssignedFromTime,GETUTCDATE())) DaysRemaining) dr
CROSS APPLY (select CASE WHEN ISNULL(a.EstimatedDaysToComplete, 0) != 0 THEN ((DATEDIFF(DAY,a.AssignedFromTime,GETUTCDATE()) / a.EstimatedDaysToComplete) * 100 ) ELSE NULL END AS PercentRemaining)  gh 

WHERE a.AccountId IS NOT NULL
ORDER BY dr.DaysRemaining ASC,a.Workflow_StartDate DESC,a.Workflow_Steps_ID ASC;



                    ";

                db.Configuration.ProxyCreationEnabled = false;

                var query = db.Database.SqlQuery<vwWorkflowStatus>(sql);
                var total = query.Count();

                var data = query
                    .Skip((obParams.Pagination_Page - 1) * obParams.Pagination_PerPage)
                    .Take(obParams.Pagination_PerPage)
                    .ToList();

                var wrapper = new PagedDataWrapper
                {
                    data = data,
                    meta = new PagedDataWrapperMeta
                    {
                        page = obParams.Pagination_Page,
                        perpage = obParams.Pagination_PerPage,
                        total = total,
                        pages = (int)Math.Ceiling(total / (double)obParams.Pagination_PerPage)
                    }
                };

                return new PagedDataWrapperResult(wrapper, Request);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message + (e.InnerException != null ? ("::" + e.InnerException.Message) : ""));
            }
        }

        // In: Web.Controllers.WebAPI.Workflow.WorkflowController

        public class WorkflowDashboardRow
        {
            public int AccountId { get; set; }
            public string AccountName { get; set; }
            public string AccountCode { get; set; }
            public int? AccountManagerUser_ID { get; set; }
            public string AccountManagerName { get; set; }

            public int WorkflowHeaderId { get; set; }
            public string WorkflowDisplayName { get; set; }      // displayName from ConfigData (fallbacks)
            public string Waiting_On_Step_Name { get; set; }
            public string Assigned_To_Name { get; set; }
            public string Workflow_Status { get; set; }

            public int DaysRemaining { get; set; }
            public string DaysRemainingText { get; set; }

            public int EstimatedDaysToComplete { get; set; }
            public int?  PercentRemaining { get; set; }
            public DateTime Workflow_StartDate { get; set; }
        }


        [AcceptVerbs("POST")]
        [ActionName("GetWorkflowDashboard")]
        public IHttpActionResult GetWorkflowDashboard(FormDataCollection formData)
        {
            try
            {
                var obParams = new DatatableRequestParams(formData);
                if (obParams.Pagination_PerPage <= 0) obParams.Pagination_PerPage = 10;
                if (obParams.Pagination_Page <= 0) obParams.Pagination_Page = 1;

                int currentUserId = CurrentContext.CurrentUser.User_Id;

                bool IsTrue(string v)
                {
                    v = (v ?? "").Trim();
                    return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("on", StringComparison.OrdinalIgnoreCase);
                }
                int? TryInt(string v)
                {
                    int x;
                    return int.TryParse((v ?? "").Trim(), out x) && x > 0 ? (int?)x : null;
                }

                // ---------------- Filters ----------------
                string assignedSel = (formData["Assigned_Users_Id"] ?? "").Trim();   // "", "UNASSIGNED", or numeric
                bool chkAssignedToMe = IsTrue(formData["AssignedToMe"]);
                bool chkMyAccounts = IsTrue(formData["MyAccounts"]);
                bool chkAssignedToMyTeam = IsTrue(formData["AssignedToMyTeam"]);
                bool hideStopped = IsTrue(formData["HideStopped"]);
                int? actionRoleId = TryInt(formData["ActionRoleId"]);

                int? selectedUserId = null;
                bool unassignedOnly = false;

                if (!string.IsNullOrWhiteSpace(assignedSel))
                {
                    if (assignedSel.Equals("UNASSIGNED", StringComparison.OrdinalIgnoreCase)) unassignedOnly = true;
                    else selectedUserId = TryInt(assignedSel);
                }

                var sqlFilterBase = new StringBuilder();
                var sqlFilterFinal = new StringBuilder();
                var prms = new List<SqlParameter> { new SqlParameter("@CurrentUserId", currentUserId) };

                // ---- Assigned dropdown logic (simplified as requested) ----
                // "" => no user restriction
                // UNASSIGNED => assigned is null
                // numeric => assigned equals selected user
                if (unassignedOnly) sqlFilterBase.Append(" AND st.Assigned_Users_Id IS NULL ");
                else if (selectedUserId.HasValue) { 
                   // sqlFilterBase.Append(" AND st.Assigned_Users_Id=@AssignedUserId "); 
                    prms.Add(new SqlParameter("@AssignedUserId", selectedUserId.Value));
                    sqlFilterFinal.Append(" AND (a.Assigned_Users_Id=@AssignedUserId OR (a.Assigned_Users_Id IS NULL AND acc.AccountManagerUser_ID=@AssignedUserId)) ");
                }

                // Hide stopped tasks (exclusion)
                if (hideStopped) sqlFilterBase.Append(" AND COALESCE(st.ExecutionStopped,0)=0 ");

                // Action role dropdown (direct filter)
                if (actionRoleId.HasValue) { sqlFilterBase.Append(" AND s.Action_Roles_Id=@ActionRoleId "); prms.Add(new SqlParameter("@ActionRoleId", actionRoleId.Value)); }

                // ---- OR block (AssignedToMe OR MyAccounts OR AssignedToMyTeam) ----
                // Only apply OR block when dropdown is "All" (no specific user/unassigned chosen)
                if (!unassignedOnly && !selectedUserId.HasValue)
                {
                    var orConds = new List<string>();

                    if (chkAssignedToMe) orConds.Add("a.Assigned_Users_Id=@CurrentUserId");
                    if (chkMyAccounts) orConds.Add("acc.AccountManagerUser_ID=@CurrentUserId");

                    if (chkAssignedToMyTeam)
                    {
                        var roleIds = db.Database.SqlQuery<int>(
                            "SELECT ur.Role_Id FROM Security_UserRoles ur WHERE ur.User_ID=@uid",
                            new SqlParameter("@uid", currentUserId)
                        ).ToList();

                        var csv = string.Join(",", roleIds.Distinct());
                        if (string.IsNullOrWhiteSpace(csv))
                        {
                            // if user has no roles, "AssignedToMyTeam" contributes nothing
                            // (do not force 1=0; keep other OR terms working)
                        }
                        else
                        {
                            prms.Add(new SqlParameter("@MyRoleIdsCsv", csv));
                            orConds.Add("EXISTS (SELECT 1 FROM STRING_SPLIT(@MyRoleIdsCsv, ',') r WHERE TRY_CAST(r.value AS int)=a.Action_Roles_Id)");
                        }
                    }

                    if (orConds.Count > 0)
                        sqlFilterFinal.Append(" AND (" + string.Join(" OR ", orConds) + ") ");
                }

                // ---------------- Main SQL ----------------
                string sql = $@"
                ;WITH base AS (
                    SELECT
                        h.ID WorkflowHeaderId, h.Context_Object_Code, h.Object_RefID, h.CreateDate Workflow_StartDate, w.Name Workflow_Name,
                        st.ID Workflow_StepTransactions_ID, st.Assigned_Users_Id,  COALESCE(  NULLIF(LTRIM(RTRIM(COALESCE(su.Firstname,'') + ' ' + COALESCE(su.Lastname,''))), ''),
                            NULLIF(LTRIM(RTRIM(ar.RoleName)), ''),   'unassigned') AS Assigned_To_Name,
                        s.ID Workflow_Steps_ID, s.Name Waiting_On_Step_Name, s.Action_Roles_Id Action_Roles_Id,
                        st.CreateDate AssignedFromTime,
                        COALESCE(TRY_CAST(JSON_VALUE(s.Workflow_StepTypes_ConfigData,'$.EstimatedDaysToComplete') AS int),0) EstimatedDaysToComplete,
                        JSON_VALUE(h.ConfigData,'$.AdditionalWorkflowDescription') DisplayName,

                        CASE
                            WHEN h.DependsOn_WorkFlows_Workflow_Transactions_Headers_ID IS NOT NULL AND COALESCE(h.DependsOn_WorkFlows_WaitingOnCompletion,0)=1
                                THEN 'Waiting on workflow ""' + COALESCE(wdep.Name,'') + '"" to complete.'
                            WHEN COALESCE(st.IsWaitingOnTriggeredWorkflowToComplete,0)=1
                                THEN 'Waiting on triggered workflow(s) to complete.'
                            WHEN COALESCE(st.StepExecuted,0)=0
                                THEN 'Pending action.'
                            WHEN COALESCE(st.ErrorInExecution,0)=1
                                THEN 'There was en error executing the workflow step: ' + COALESCE(st.ErrorMessage,'')
                            WHEN COALESCE(st.ExecutionStopped,0)=1
                                THEN 'Workflow stopped.'
                            ELSE ''
                        END Workflow_Status,

                        ROW_NUMBER() OVER (PARTITION BY h.ID ORDER BY st.CreateDate DESC, st.ID DESC) rn

                    FROM Workflow_StepTransactions st
                    JOIN Workflow_Transactions_Headers h ON h.ID=st.Workflow_Transactions_Headers_ID
                    JOIN Workflow_Steps s ON s.ID=st.Workflow_Steps_ID
                    JOIN Workflows w ON w.ID=h.Workflows_ID
                    LEFT JOIN Security_Users su ON su.User_Id=st.Assigned_Users_Id
                    LEFT JOIN Security_Roles ar ON ar.Role_Id = s.Action_Roles_Id 

                    LEFT JOIN Workflow_Transactions_Headers hdep ON hdep.ID=h.DependsOn_WorkFlows_Workflow_Transactions_Headers_ID AND COALESCE(hdep.DelFlag,0)=0
                    LEFT JOIN Workflows wdep ON wdep.ID=hdep.Workflows_ID AND COALESCE(wdep.DelFlag,0)=0

                    WHERE COALESCE(st.DelFlag,0)=0 AND COALESCE(h.DelFlag,0)=0 AND COALESCE(s.DelFlag,0)=0 AND COALESCE(w.DelFlag,0)=0
                      AND COALESCE(h.WorkflowComplete,0)=0
                      AND COALESCE(st.StepExecuted,0)=0
                      {sqlFilterBase}
                ),
                cur AS ( SELECT * FROM base WHERE rn=1 ),
                acct AS (
                    SELECT c.*,
                           COALESCE(
                             CASE WHEN c.Context_Object_Code='ACCOUNT' THEN c.Object_RefID END,
                             or_obj.ObjectScreen_RefID
                           ) AccountId
                    FROM cur c
                    LEFT JOIN ObjectRelations or_obj
                      ON c.Context_Object_Code<>'ACCOUNT'
                     AND COALESCE(or_obj.DelFlag,0)=0
                     AND or_obj.ObjectScreen_Code='ACCOUNTS'
                     AND or_obj.AssociatedObject_Code=c.Context_Object_Code
                     AND or_obj.AssociatedObject_RefID=c.Object_RefID
                )
                SELECT
                    acc.ID AccountId, acc.Name AccountName, acc.AccountCode,
                    acc.AccountManagerUser_ID,
                    NULLIF(LTRIM(RTRIM(COALESCE(am.Firstname,'') + ' ' + COALESCE(am.Lastname,''))), '') AS AccountManagerName,
                    a.WorkflowHeaderId,a.EstimatedDaysToComplete ,
                    COALESCE(NULLIF(a.DisplayName,''), a.Workflow_Name) WorkflowDisplayName,
                    a.Waiting_On_Step_Name, a.Assigned_To_Name, a.Workflow_Status,
                    dr.DaysRemaining,PercentRemaining,
    
                    CASE WHEN dr.DaysRemaining<0 THEN CONCAT(ABS(dr.DaysRemaining),' days overdue')
                         WHEN dr.DaysRemaining=0 THEN 'Due today'
                         ELSE CONCAT(dr.DaysRemaining,' days remaining') END DaysRemainingText,
                    a.Workflow_StartDate
                FROM acct a
                LEFT JOIN Accounts acc ON acc.ID=a.AccountId
                LEFT JOIN Security_Users am ON am.User_Id = acc.AccountManagerUser_ID
                CROSS APPLY (SELECT (a.EstimatedDaysToComplete - DATEDIFF(DAY,a.AssignedFromTime,GETUTCDATE())) DaysRemaining) dr
                CROSS APPLY (select CASE WHEN ISNULL(a.EstimatedDaysToComplete, 0) != 0 THEN (((ISNULL(a.EstimatedDaysToComplete, 0) - DATEDIFF(DAY,a.AssignedFromTime,GETUTCDATE())) / a.EstimatedDaysToComplete) * 100 ) ELSE NULL END AS PercentRemaining)  gh 
                WHERE a.AccountId IS NOT NULL
                  {sqlFilterFinal}
                ORDER BY dr.DaysRemaining ASC, a.Workflow_StartDate DESC, a.Workflow_Steps_ID ASC;";

                db.Configuration.ProxyCreationEnabled = false;

                var rows = db.Database.SqlQuery<WorkflowDashboardRow>(sql, prms.ToArray()).ToList();

                var now = DateTime.UtcNow;
                var accounts = rows
                    .GroupBy(r => new { r.AccountId, r.AccountName, r.AccountCode })
                    .Select(g =>
                    {
                        var minDate = g.Min(x => x.Workflow_StartDate);
                        var daysAgo = (int)Math.Floor((now - minDate).TotalDays);
                        var oldestText = daysAgo <= 0 ? "Today" : (daysAgo == 1 ? "1 day ago" : $"{daysAgo} days ago");
                        var mgrId = g.Select(x => x.AccountManagerUser_ID).FirstOrDefault();
                        var mgrName = g.Select(x => x.AccountManagerName).FirstOrDefault();

                        return new
                        {
                            id = "acc" + g.Key.AccountId,
                            accountId = g.Key.AccountId,
                            name = g.Key.AccountName,
                            accountCode = g.Key.AccountCode,
                            accountManagerUserId = mgrId,
                            accountManagerName = mgrName,
                            pendingCount = g.Select(x => x.WorkflowHeaderId).Distinct().Count(),
                            oldest = oldestText,
                            workflows = g.OrderBy(x => x.DaysRemaining).ThenByDescending(x => x.Workflow_StartDate).Select(x => new
                            {
                                name = x.WorkflowDisplayName,
                                waitingOn = x.Waiting_On_Step_Name,
                                assigned = x.Assigned_To_Name,
                                status = x.Workflow_Status,
                                estDays = x.EstimatedDaysToComplete,
                                headerId = x.WorkflowHeaderId,
                                dueText = (x.PercentRemaining != null)?x.DaysRemainingText: "N.A.",
                                dueClass = (x.PercentRemaining != null && x.PercentRemaining.Value <= 10) ? "text-danger"
                                            : (x.PercentRemaining != null && x.PercentRemaining.Value > 10 && x.PercentRemaining.Value <= 30) ? "text-warning"
                                            : (x.PercentRemaining != null) ? "text-success" : ""
                            }).ToList()
                        }; 
                    })
                    .OrderByDescending(a => a.pendingCount).ThenBy(a => a.name)
                    .ToList();

                var total = accounts.Count;
                var data = accounts.Skip((obParams.Pagination_Page - 1) * obParams.Pagination_PerPage).Take(obParams.Pagination_PerPage).ToList();

                var wrapper = new PagedDataWrapper
                {
                    data = data,
                    meta = new PagedDataWrapperMeta
                    {
                        page = obParams.Pagination_Page,
                        perpage = obParams.Pagination_PerPage,
                        total = total,
                        pages = (int)Math.Ceiling(total / (double)obParams.Pagination_PerPage)
                    }
                };

                return new PagedDataWrapperResult(wrapper, Request);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message + (e.InnerException != null ? ("::" + e.InnerException.Message) : ""));
            }
        }



        private const int MaxOutDegreePerStep = 25;
        private const int MaxInDegreePerStep = 50;

        private bool CanEditWorkflowTemplate()
        {
            return CurrentContext.CurrentUser.IsSysAdmin;
        }

        private IHttpActionResult ForbiddenTemplateAccess()
        {
            return BadRequest("Only system admin users can edit workflow templates.");
        }

        private void AuditTemplateChange(short workflowId, string actionName, object payload)
        {
            var audit = new Workflow_TemplateAudit
            {
                Workflows_ID = workflowId,
                ActionName = actionName,
                PayloadJson = payload == null ? null : JsonConvert.SerializeObject(payload),
                CreateUserID = CurrentContext.CurrentUser.User_Id,
                CreateDate = DateTime.UtcNow
            };

            db.Workflow_TemplateAudits.Add(audit);
        }

        public sealed class WorkflowTemplateGraphResponse
        {
            public short WorkflowId { get; set; }
            public string WorkflowCode { get; set; }
            public string WorkflowName { get; set; }
            public List<WorkflowGraphNode> Nodes { get; set; }
            public List<WorkflowGraphEdgeDto> Edges { get; set; }
            public bool CanEdit { get; set; }
        }

        public sealed class WorkflowGraphEdgeDto
        {
            public int TransitionId { get; set; }
            public short FromStepId { get; set; }
            public short ToStepId { get; set; }
            public string ConditionExpression { get; set; }
            public string DisplayLabel { get; set; }
            public int SortOrder { get; set; }
            public bool IsDefaultPath { get; set; }
            public bool IsActive { get; set; }
        }

        public sealed class AddStepCommand
        {
            public short WorkflowId { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public byte Workflow_StepTypes_ID { get; set; }
            public bool IsStartStep { get; set; }
            public double? X { get; set; }
            public double? Y { get; set; }
        }

        public sealed class UpdateStepCommand
        {
            public short WorkflowId { get; set; }
            public short StepId { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public bool? IsStartStep { get; set; }
            public byte? Workflow_StepTypes_ID { get; set; }
            public DateTime? ExpectedModifyDateUtc { get; set; }
        }

        public sealed class DeleteStepCommand
        {
            public short WorkflowId { get; set; }
            public short StepId { get; set; }
            public DateTime? ExpectedModifyDateUtc { get; set; }
        }

        public sealed class AddTransitionCommand
        {
            public short WorkflowId { get; set; }
            public short FromStepId { get; set; }
            public short ToStepId { get; set; }
            public string ConditionExpression { get; set; }
            public string DisplayLabel { get; set; }
            public int? SortOrder { get; set; }
            public bool? IsDefaultPath { get; set; }
            public bool? IsActive { get; set; }
        }

        public sealed class UpdateTransitionCommand
        {
            public short WorkflowId { get; set; }
            public int TransitionId { get; set; }
            public string ConditionExpression { get; set; }
            public string DisplayLabel { get; set; }
            public int? SortOrder { get; set; }
            public bool? IsDefaultPath { get; set; }
            public bool? IsActive { get; set; }
        }

        public sealed class RemoveTransitionCommand
        {
            public short WorkflowId { get; set; }
            public int TransitionId { get; set; }
        }

        public sealed class SaveStepLayoutCommand
        {
            public short WorkflowId { get; set; }
            public short StepId { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public DateTime? LastClientSyncDateUtc { get; set; }
        }

        [AcceptVerbs("GET")]
        [ActionName("GetTemplateGraph")]
        public IHttpActionResult GetTemplateGraph(short workflowId)
        {
            var workflow = db.Workflows.FirstOrDefault(w => w.ID == workflowId && (w.DelFlag ?? false) == false);
            if (workflow == null)
            {
                return NotFound();
            }

            Workflow.WorkflowCode workflowCode;
            if (!Enum.TryParse(workflow.Code, true, out workflowCode))
            {
                return BadRequest("Workflow code cannot be parsed to WorkflowCode enum.");
            }

            var helper = new WorkFlow();
            var root = helper.CreateInMemoryWorkflowMap(workflowCode, db.Database.Connection);
            var layout = WorkflowGraphLayoutBuilder.Build(root);

            var storedLayoutByStep = db.Workflow_StepDesignerLayouts
                .Where(x => x.Workflows_ID == workflowId && (x.DelFlag ?? false) == false)
                .ToList()
                .ToDictionary(x => x.Workflow_Steps_ID, x => x);

            foreach (var node in layout.Nodes)
            {
                Workflow_StepDesignerLayout persisted;
                if (storedLayoutByStep.TryGetValue(node.StepId, out persisted))
                {
                    node.X = persisted.PositionX;
                    node.Y = persisted.PositionY;
                }
            }

            var transitions = db.Workflow_StepTransitions
                .Where(t => t.Workflows_ID == workflowId && (t.DelFlag ?? false) == false)
                .OrderBy(t => t.From_Workflow_Steps_ID)
                .ThenBy(t => t.SortOrder)
                .ThenBy(t => t.ID)
                .ToList();

            var edges = transitions.Select(t => new WorkflowGraphEdgeDto
            {
                TransitionId = t.ID,
                FromStepId = t.From_Workflow_Steps_ID,
                ToStepId = t.To_Workflow_Steps_ID,
                ConditionExpression = t.ConditionExpression,
                DisplayLabel = t.DisplayLabel,
                SortOrder = t.SortOrder,
                IsDefaultPath = t.IsDefaultPath ?? false,
                IsActive = t.IsActive ?? true
            }).ToList();

            return Ok(new WorkflowTemplateGraphResponse
            {
                WorkflowId = workflow.ID,
                WorkflowCode = workflow.Code,
                WorkflowName = workflow.Name,
                Nodes = layout.Nodes.ToList(),
                Edges = edges,
                CanEdit = CanEditWorkflowTemplate()
            });
        }

        [AcceptVerbs("POST")]
        [ActionName("AddTemplateStep")]
        public IHttpActionResult AddTemplateStep(AddStepCommand cmd)
        {
            if (!CanEditWorkflowTemplate())
            {
                return ForbiddenTemplateAccess();
            }

            if (cmd == null || string.IsNullOrWhiteSpace(cmd.Name))
            {
                return BadRequest("Invalid command. Step name is required.");
            }

            var workflow = db.Workflows.FirstOrDefault(w => w.ID == cmd.WorkflowId && (w.DelFlag ?? false) == false);
            if (workflow == null)
            {
                return NotFound();
            }

            var stepType = db.Workflow_StepTypes.FirstOrDefault(t => t.ID == cmd.Workflow_StepTypes_ID);
            if (stepType == null)
            {
                return BadRequest("Invalid step type.");
            }

            if (cmd.IsStartStep)
            {
                var existingStartCount = db.Workflow_Steps.Count(s => s.Workflows_ID == cmd.WorkflowId && (s.IsStartStep ?? false) && (s.DelFlag ?? false) == false);
                if (existingStartCount > 0)
                {
                    return BadRequest("Only one start step is allowed.");
                }
            }

            var step = new Workflow_Steps
            {
                Workflows_ID = cmd.WorkflowId,
                Name = cmd.Name.Trim(),
                Description = cmd.Description,
                Workflow_StepTypes_ID = cmd.Workflow_StepTypes_ID,
                Workflow_StepTypes_ConfigData = stepType.GetConfigDataTemplateJson(),
                IsStartStep = cmd.IsStartStep,
                IsActive = true,
                CreateUserID = CurrentContext.CurrentUser.User_Id,
                CreateDate = DateTime.UtcNow,
                DelFlag = false
            };

            db.Workflow_Steps.Add(step);
            db.SaveChanges();

            if (cmd.X.HasValue && cmd.Y.HasValue)
            {
                db.Workflow_StepDesignerLayouts.Add(new Workflow_StepDesignerLayout
                {
                    Workflows_ID = cmd.WorkflowId,
                    Workflow_Steps_ID = step.ID,
                    PositionX = cmd.X.Value,
                    PositionY = cmd.Y.Value,
                    LastClientSyncDate = DateTime.UtcNow,
                    CreateUserID = CurrentContext.CurrentUser.User_Id,
                    CreateDate = DateTime.UtcNow,
                    DelFlag = false
                });
            }

            AuditTemplateChange(cmd.WorkflowId, "AddTemplateStep", cmd);
            db.SaveChanges();

            return Ok(new { StepId = step.ID });
        }

        [AcceptVerbs("POST")]
        [ActionName("UpdateTemplateStep")]
        public IHttpActionResult UpdateTemplateStep(UpdateStepCommand cmd)
        {
            if (!CanEditWorkflowTemplate())
            {
                return ForbiddenTemplateAccess();
            }

            if (cmd == null)
            {
                return BadRequest("Invalid command.");
            }

            var step = db.Workflow_Steps.FirstOrDefault(s => s.ID == cmd.StepId && s.Workflows_ID == cmd.WorkflowId && (s.DelFlag ?? false) == false);
            if (step == null)
            {
                return NotFound();
            }

            if (cmd.ExpectedModifyDateUtc.HasValue && step.ModifyDate.HasValue && step.ModifyDate.Value != cmd.ExpectedModifyDateUtc.Value)
            {
                return BadRequest("Step has been modified by another user. Refresh and retry.");
            }

            if (!string.IsNullOrWhiteSpace(cmd.Name))
            {
                step.Name = cmd.Name.Trim();
            }

            if (cmd.Description != null)
            {
                step.Description = cmd.Description;
            }

            if (cmd.Workflow_StepTypes_ID.HasValue)
            {
                step.Workflow_StepTypes_ID = cmd.Workflow_StepTypes_ID.Value;
            }

            if (cmd.IsStartStep.HasValue)
            {
                if (cmd.IsStartStep.Value)
                {
                    var anotherStartExists = db.Workflow_Steps.Any(s => s.Workflows_ID == cmd.WorkflowId && s.ID != cmd.StepId && (s.IsStartStep ?? false) && (s.DelFlag ?? false) == false);
                    if (anotherStartExists)
                    {
                        return BadRequest("Only one start step is allowed.");
                    }
                }

                step.IsStartStep = cmd.IsStartStep.Value;
            }

            step.ModifyUserID = CurrentContext.CurrentUser.User_Id;
            step.ModifyDate = DateTime.UtcNow;

            AuditTemplateChange(cmd.WorkflowId, "UpdateTemplateStep", cmd);
            db.SaveChanges();
            return Ok(new { StepId = step.ID, step.ModifyDate });
        }

        [AcceptVerbs("POST")]
        [ActionName("DeleteTemplateStep")]
        public IHttpActionResult DeleteTemplateStep(DeleteStepCommand cmd)
        {
            if (!CanEditWorkflowTemplate())
            {
                return ForbiddenTemplateAccess();
            }

            if (cmd == null)
            {
                return BadRequest("Invalid command.");
            }

            var step = db.Workflow_Steps.FirstOrDefault(s => s.ID == cmd.StepId && s.Workflows_ID == cmd.WorkflowId && (s.DelFlag ?? false) == false);
            if (step == null)
            {
                return NotFound();
            }

            if (cmd.ExpectedModifyDateUtc.HasValue && step.ModifyDate.HasValue && step.ModifyDate.Value != cmd.ExpectedModifyDateUtc.Value)
            {
                return BadRequest("Step has been modified by another user. Refresh and retry.");
            }

            var transitionCount = db.Workflow_StepTransitions.Count(t => t.Workflows_ID == cmd.WorkflowId && (t.DelFlag ?? false) == false && (t.From_Workflow_Steps_ID == cmd.StepId || t.To_Workflow_Steps_ID == cmd.StepId));
            if (transitionCount > 0)
            {
                return BadRequest("Cannot delete step while transitions exist. De-link transitions first.");
            }

            step.DelFlag = true;
            step.ModifyUserID = CurrentContext.CurrentUser.User_Id;
            step.ModifyDate = DateTime.UtcNow;

            foreach (var layout in db.Workflow_StepDesignerLayouts.Where(x => x.Workflows_ID == cmd.WorkflowId && x.Workflow_Steps_ID == cmd.StepId && (x.DelFlag ?? false) == false))
            {
                layout.DelFlag = true;
                layout.ModifyUserID = CurrentContext.CurrentUser.User_Id;
                layout.ModifyDate = DateTime.UtcNow;
            }

            AuditTemplateChange(cmd.WorkflowId, "DeleteTemplateStep", cmd);
            db.SaveChanges();
            return Ok(new { DeletedStepId = cmd.StepId });
        }

        [AcceptVerbs("POST")]
        [ActionName("AddTemplateTransition")]
        public IHttpActionResult AddTemplateTransition(AddTransitionCommand cmd)
        {
            if (!CanEditWorkflowTemplate())
            {
                return ForbiddenTemplateAccess();
            }

            if (cmd == null || string.IsNullOrWhiteSpace(cmd.ConditionExpression))
            {
                return BadRequest("ConditionExpression is required.");
            }

            if (cmd.FromStepId == cmd.ToStepId)
            {
                return BadRequest("Self-loop transitions are not allowed.");
            }

            var steps = db.Workflow_Steps.Where(s => s.Workflows_ID == cmd.WorkflowId && (s.DelFlag ?? false) == false && (s.ID == cmd.FromStepId || s.ID == cmd.ToStepId)).Select(s => s.ID).ToList();
            if (steps.Count != 2)
            {
                return BadRequest("Invalid step IDs for this workflow.");
            }

            var outDegree = db.Workflow_StepTransitions.Count(t => t.Workflows_ID == cmd.WorkflowId && t.From_Workflow_Steps_ID == cmd.FromStepId && (t.DelFlag ?? false) == false);
            if (outDegree >= MaxOutDegreePerStep)
            {
                return BadRequest("Max out-degree reached for source step.");
            }

            var inDegree = db.Workflow_StepTransitions.Count(t => t.Workflows_ID == cmd.WorkflowId && t.To_Workflow_Steps_ID == cmd.ToStepId && (t.DelFlag ?? false) == false);
            if (inDegree >= MaxInDegreePerStep)
            {
                return BadRequest("Max in-degree reached for target step.");
            }

            var duplicateExists = db.Workflow_StepTransitions.Any(t => t.Workflows_ID == cmd.WorkflowId && t.From_Workflow_Steps_ID == cmd.FromStepId && t.To_Workflow_Steps_ID == cmd.ToStepId && (t.DelFlag ?? false) == false);
            if (duplicateExists)
            {
                return BadRequest("Transition already exists between selected steps.");
            }

            var transition = new Workflow_StepTransition
            {
                Workflows_ID = cmd.WorkflowId,
                From_Workflow_Steps_ID = cmd.FromStepId,
                To_Workflow_Steps_ID = cmd.ToStepId,
                ConditionExpression = cmd.ConditionExpression,
                DisplayLabel = cmd.DisplayLabel,
                SortOrder = cmd.SortOrder ?? 0,
                IsDefaultPath = cmd.IsDefaultPath ?? false,
                IsActive = cmd.IsActive ?? true,
                CreateUserID = CurrentContext.CurrentUser.User_Id,
                CreateDate = DateTime.UtcNow,
                DelFlag = false
            };

            db.Workflow_StepTransitions.Add(transition);

            // Dual-write to legacy link columns for backward compatibility.
            var fromStep = db.Workflow_Steps.First(s => s.ID == cmd.FromStepId);
            var toStep = db.Workflow_Steps.First(s => s.ID == cmd.ToStepId);
            if (transition.IsDefaultPath ?? false)
            {
                fromStep.Workflow_Steps_NextStep_ID = cmd.ToStepId;
            }
            if (!toStep.Workflow_Steps_PreviousStep_ID.HasValue)
            {
                toStep.Workflow_Steps_PreviousStep_ID = cmd.FromStepId;
            }

            fromStep.ModifyUserID = CurrentContext.CurrentUser.User_Id;
            fromStep.ModifyDate = DateTime.UtcNow;
            toStep.ModifyUserID = CurrentContext.CurrentUser.User_Id;
            toStep.ModifyDate = DateTime.UtcNow;

            AuditTemplateChange(cmd.WorkflowId, "AddTemplateTransition", cmd);
            db.SaveChanges();
            return Ok(new { TransitionId = transition.ID });
        }

        [AcceptVerbs("POST")]
        [ActionName("UpdateTemplateTransition")]
        public IHttpActionResult UpdateTemplateTransition(UpdateTransitionCommand cmd)
        {
            if (!CanEditWorkflowTemplate())
            {
                return ForbiddenTemplateAccess();
            }

            if (cmd == null || string.IsNullOrWhiteSpace(cmd.ConditionExpression))
            {
                return BadRequest("ConditionExpression is required.");
            }

            var transition = db.Workflow_StepTransitions.FirstOrDefault(t => t.ID == cmd.TransitionId && t.Workflows_ID == cmd.WorkflowId && (t.DelFlag ?? false) == false);
            if (transition == null)
            {
                return NotFound();
            }

            transition.ConditionExpression = cmd.ConditionExpression;
            transition.DisplayLabel = cmd.DisplayLabel;
            if (cmd.SortOrder.HasValue)
            {
                transition.SortOrder = cmd.SortOrder.Value;
            }
            if (cmd.IsDefaultPath.HasValue)
            {
                transition.IsDefaultPath = cmd.IsDefaultPath.Value;
            }
            if (cmd.IsActive.HasValue)
            {
                transition.IsActive = cmd.IsActive.Value;
            }
            transition.ModifyUserID = CurrentContext.CurrentUser.User_Id;
            transition.ModifyDate = DateTime.UtcNow;

            // dual-write for default path
            if (transition.IsDefaultPath ?? false)
            {
                var fromStep = db.Workflow_Steps.FirstOrDefault(s => s.ID == transition.From_Workflow_Steps_ID && (s.DelFlag ?? false) == false);
                if (fromStep != null)
                {
                    fromStep.Workflow_Steps_NextStep_ID = transition.To_Workflow_Steps_ID;
                    fromStep.ModifyUserID = CurrentContext.CurrentUser.User_Id;
                    fromStep.ModifyDate = DateTime.UtcNow;
                }
            }

            AuditTemplateChange(cmd.WorkflowId, "UpdateTemplateTransition", cmd);
            db.SaveChanges();
            return Ok(new { TransitionId = transition.ID });
        }

        [AcceptVerbs("POST")]
        [ActionName("RemoveTemplateTransition")]
        public IHttpActionResult RemoveTemplateTransition(RemoveTransitionCommand cmd)
        {
            if (!CanEditWorkflowTemplate())
            {
                return ForbiddenTemplateAccess();
            }

            if (cmd == null)
            {
                return BadRequest("Invalid command.");
            }

            var transition = db.Workflow_StepTransitions.FirstOrDefault(t => t.ID == cmd.TransitionId && t.Workflows_ID == cmd.WorkflowId && (t.DelFlag ?? false) == false);
            if (transition == null)
            {
                return NotFound();
            }

            transition.DelFlag = true;
            transition.IsActive = false;
            transition.ModifyUserID = CurrentContext.CurrentUser.User_Id;
            transition.ModifyDate = DateTime.UtcNow;

            var fromStep = db.Workflow_Steps.FirstOrDefault(s => s.ID == transition.From_Workflow_Steps_ID && (s.DelFlag ?? false) == false);
            if (fromStep != null && fromStep.Workflow_Steps_NextStep_ID == transition.To_Workflow_Steps_ID)
            {
                fromStep.Workflow_Steps_NextStep_ID = null;
                fromStep.ModifyUserID = CurrentContext.CurrentUser.User_Id;
                fromStep.ModifyDate = DateTime.UtcNow;
            }

            AuditTemplateChange(cmd.WorkflowId, "RemoveTemplateTransition", cmd);
            db.SaveChanges();
            return Ok(new { RemovedTransitionId = cmd.TransitionId });
        }

        [AcceptVerbs("POST")]
        [ActionName("SaveStepLayout")]
        public IHttpActionResult SaveStepLayout(SaveStepLayoutCommand cmd)
        {
            if (!CanEditWorkflowTemplate())
            {
                return ForbiddenTemplateAccess();
            }

            if (cmd == null)
            {
                return BadRequest("Invalid command.");
            }

            var stepExists = db.Workflow_Steps.Any(s => s.ID == cmd.StepId && s.Workflows_ID == cmd.WorkflowId && (s.DelFlag ?? false) == false);
            if (!stepExists)
            {
                return BadRequest("Step not found for workflow.");
            }

            cmd.X = Math.Max(10d, Math.Min(9000d, cmd.X));
            cmd.Y = Math.Max(10d, Math.Min(9000d, cmd.Y));

            var now = DateTime.UtcNow;
            var layout = db.Workflow_StepDesignerLayouts.FirstOrDefault(x => x.Workflows_ID == cmd.WorkflowId && x.Workflow_Steps_ID == cmd.StepId && (x.DelFlag ?? false) == false);

            if (layout == null)
            {
                layout = new Workflow_StepDesignerLayout
                {
                    Workflows_ID = cmd.WorkflowId,
                    Workflow_Steps_ID = cmd.StepId,
                    PositionX = cmd.X,
                    PositionY = cmd.Y,
                    LastClientSyncDate = cmd.LastClientSyncDateUtc ?? now,
                    CreateUserID = CurrentContext.CurrentUser.User_Id,
                    CreateDate = now,
                    DelFlag = false
                };

                db.Workflow_StepDesignerLayouts.Add(layout);
            }
            else
            {
                if (cmd.LastClientSyncDateUtc.HasValue && layout.LastClientSyncDate.HasValue && layout.LastClientSyncDate.Value > cmd.LastClientSyncDateUtc.Value)
                {
                    return BadRequest("Layout was modified by another user. Reload template graph.");
                }

                layout.PositionX = cmd.X;
                layout.PositionY = cmd.Y;
                layout.LastClientSyncDate = now;
                layout.ModifyUserID = CurrentContext.CurrentUser.User_Id;
                layout.ModifyDate = now;
            }

            AuditTemplateChange(cmd.WorkflowId, "SaveStepLayout", cmd);
            db.SaveChanges();
            return Ok(new { cmd.StepId, LastClientSyncDateUtc = layout.LastClientSyncDate });
        }

    }
}
