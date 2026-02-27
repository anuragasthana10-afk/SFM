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
using System.Net;
using System.Net.Http;
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
        [AcceptVerbs("POST", "GET")]
        [ActionName("GetWorkflowInstanceStatus")]
        public IHttpActionResult GetWorkflowInstanceStatus(int workflowTxnHeaderID)
        {
            APICallResponse response = new APICallResponse();
            response.ResponseCode = "0";

            var workflowTransactionHeader = db.Workflow_Transactions_Headers.Where(w => w.ID == workflowTxnHeaderID).Include(w => w.Workflow).Include(w => w.Workflow_StepTransactions.Select(t => t.Workflow_Steps)).FirstOrDefault();
            if (workflowTransactionHeader != null)
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
            object response = "Object with config type: \"" + configType + "\" and template name: \"" + templateName + "\" not found.";

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

                    if (responseObject.uid == 0)
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
    LEFT JOIN ObjectRelations or_order ON o.OrderId IS NOT NULL AND COALESCE(or_order.DelFlag,0)=0 AND or_order.ObjectScreen_Code in ('ACCOUNTS','OTHERSERVICES') AND or_order.AssociatedObject_Code IN ('Order','ORDER') AND or_order.AssociatedObject_RefID=o.OrderId
    LEFT JOIN ObjectRelations or_obj ON o.OrderId IS NULL AND o.Context_Object_Code<>'ACCOUNT' AND COALESCE(or_obj.DelFlag,0)=0 AND or_obj.ObjectScreen_Code in ('ACCOUNTS','OTHERSERVICES') AND or_obj.AssociatedObject_Code=o.Context_Object_Code AND or_obj.AssociatedObject_RefID=o.Object_RefID
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
            public int? PercentRemaining { get; set; }
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
                else if (selectedUserId.HasValue)
                {
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
                     AND or_obj.ObjectScreen_Code in ('ACCOUNTS' ,'OTHERSERVICES')
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
                                dueText = (x.PercentRemaining != null) ? x.DaysRemainingText : "N.A.",
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


        private sealed class UserRoleRow
        {
            public int Role_Id { get; set; }
            public string RoleName { get; set; }
        }

        private sealed class DrilldownRow
        {
            public int WorkflowHeaderId { get; set; }
            public string WorkflowDisplayName { get; set; }
            public string WorkflowName { get; set; }
            public string StepName { get; set; }
            public string ActionRoleName { get; set; }
            public int? ActionRoleId { get; set; }
            public int? AssignedUserId { get; set; }
            public string AssignedToName { get; set; }
            public DateTime StepCreateDateUtc { get; set; }
            public DateTime? ActionedByUsersTimeUtc { get; set; }
            public int? ActionedByUsersId { get; set; }
            public int EstimatedDaysToComplete { get; set; }
        }
        private sealed class DrilldownKpiSummary
        {
            public int PendingCount { get; set; }
            public int Due1To5Count { get; set; }
            public int Due6To25Count { get; set; }
            public int Due26PlusCount { get; set; }
            public int Overdue1To5Count { get; set; }
            public int Overdue6To25Count { get; set; }
            public int Overdue26PlusCount { get; set; }
            public int CompletedCount { get; set; }
            public int CompletedDelay1To5Count { get; set; }
            public int CompletedDelay6To25Count { get; set; }
            public int CompletedDelay26PlusCount { get; set; }
            public decimal? OnTimeCompletionRatePct { get; set; }
            public decimal? AvgBusinessDaysToComplete { get; set; }
            public decimal? AvgBusinessDaysDelay { get; set; }
        }

        private sealed class PendingTaskRow
        {
            public int WorkflowHeaderId { get; set; }
            public string WorkflowDisplayName { get; set; }
            public string WorkflowName { get; set; }
            public string StepName { get; set; }
            public int? ActionRoleId { get; set; }
            public string ActionRoleName { get; set; }
            public int? AssignedUserId { get; set; }
            public string AssignedToName { get; set; }
            public DateTime StepCreateDateUtc { get; set; }
            public int EstimatedDaysToComplete { get; set; }
            public int? AccountId { get; set; }
            public string AccountName { get; set; }
            public int? AccountManagerUserId { get; set; }
            public string AccountManagerName { get; set; }
        }

        private sealed class CompletedTaskRow
        {
            public int? AssignedUserId { get; set; }
            public int? ActionRoleId { get; set; }
            public DateTime StepCreateDateUtc { get; set; }
            public DateTime? ActionedByUsersTimeUtc { get; set; }
            public int? ActionedByUsersId { get; set; }
            public int EstimatedDaysToComplete { get; set; }
        }

        private sealed class WorkflowTaskDrilldownResponse
        {
            public string Level { get; set; }
            public object Meta { get; set; }
            public object SummaryKpi { get; set; }
            public object UserKpi { get; set; }
            public List<object> AggregateData { get; set; }
            public List<object> TaskData { get; set; }
            public bool CanExport { get; set; }
            public bool IsGlobalLeader { get; set; }
            public bool IsTeamLeader { get; set; }
        }

        private static string CsvEscape(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        }


        private const string AccountManagerRoleNameOption = "AccountManager";
        private const string RenewalRoleNameOption = "Renewal";
        private const int Bucket1MaxBusinessDays = 5;
        private const int Bucket2MaxBusinessDays = 25;

        private static int CountBusinessDays(DateTime startUtc, DateTime endUtc, HashSet<DateTime> holidayDates)
        {
            if (endUtc < startUtc) return 0;
            var s = startUtc.Date;
            var e = endUtc.Date;
            int count = 0;
            for (var d = s; d <= e; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday && (holidayDates == null || !holidayDates.Contains(d))) count++;
            }
            return count;
        }

        private static DateTime AddBusinessDays(DateTime startUtc, int businessDays, HashSet<DateTime> holidayDates)
        {
            if (businessDays <= 0) return startUtc.Date;
            var d = startUtc.Date;
            int added = 0;
            while (added < businessDays)
            {
                d = d.AddDays(1);
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday && (holidayDates == null || !holidayDates.Contains(d))) added++;
            }
            return d;
        }

        [AcceptVerbs("POST")]
        [ActionName("GetWorkflowTaskDrilldown")]
        public IHttpActionResult GetWorkflowTaskDrilldown(FormDataCollection formData)
        {
            try
            {
                int currentUserId = CurrentContext.CurrentUser.User_Id;
                int page = 1, perPage = 20;
                int tmp;
                if (int.TryParse(formData["pagination[page]"], out tmp) && tmp > 0) page = tmp;
                if (int.TryParse(formData["pagination[perpage]"], out tmp) && tmp > 0) perPage = Math.Min(200, tmp);

                string level = (formData["Level"] ?? "").Trim();
                string bucket = (formData["Bucket"] ?? "All").Trim();
                bool includeCompletedAgingBuckets = string.Equals((formData["IncludeCompletedAgingBuckets"] ?? "").Trim(), "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals((formData["IncludeCompletedAgingBuckets"] ?? "").Trim(), "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals((formData["IncludeCompletedAgingBuckets"] ?? "").Trim(), "on", StringComparison.OrdinalIgnoreCase);
                int? selectedUserId = int.TryParse(formData["SelectedUserId"], out tmp) ? (int?)tmp : null;
                int? selectedWorkflowHeaderId = int.TryParse(formData["SelectedWorkflowHeaderId"], out tmp) ? (int?)tmp : null;
                int? selectedRoleId = int.TryParse(formData["SelectedRoleId"], out tmp) ? (int?)tmp : null;
                string selectedWorkflowName = (formData["SelectedWorkflowName"] ?? "").Trim();

                var userRoles = db.Database.SqlQuery<UserRoleRow>(
                    @"SELECT ur.Role_Id, r.RoleName
                      FROM Security_UserRoles ur
                      INNER JOIN Security_Roles r ON r.Role_Id = ur.Role_Id
                      WHERE ur.User_Id = @p0", currentUserId).ToList();

                bool isGlobalLeader = userRoles.Any(x => string.Equals((x.RoleName ?? "").Trim(), "Workflow_GlobalLeader", StringComparison.OrdinalIgnoreCase));
                bool isTeamLeader = userRoles.Any(x => string.Equals((x.RoleName ?? "").Trim(), "Workflow_TeamLeader", StringComparison.OrdinalIgnoreCase));
                bool canExport = isGlobalLeader || isTeamLeader;

                var currentUserRoleIds = new HashSet<int>(userRoles.Select(x => x.Role_Id).Distinct());
                var teamRoleIds = new HashSet<int>(userRoles
                    .Where(x => !new[] { "Workflow_TeamLeader", "Workflow_GlobalLeader", "WorkflowViewer", "WorkflowEditor", "WorkflowAdministrator" }
                        .Contains((x.RoleName ?? "").Trim(), StringComparer.OrdinalIgnoreCase))
                    .Select(x => x.Role_Id)
                    .Distinct()
                    .ToList());

                var implicitOwnerRoleIds = new HashSet<int>(db.Database.SqlQuery<int>(
                    @"SELECT Role_Id FROM Security_Roles WHERE LTRIM(RTRIM(RoleName)) IN (@p0, @p1)", AccountManagerRoleNameOption, RenewalRoleNameOption).ToList());

                var teamMemberUserIds = isTeamLeader && teamRoleIds.Count > 0
                    ? new HashSet<int>(db.Database.SqlQuery<int>(
                        @"SELECT DISTINCT ur.User_ID FROM Security_UserRoles ur WHERE ur.Role_Id IN (" + string.Join(",", teamRoleIds) + ")").ToList())
                    : new HashSet<int>();

                var pendingSql = @";WITH base AS (
                        SELECT h.ID AS WorkflowHeaderId, COALESCE(NULLIF(JSON_VALUE(h.ConfigData,'$.AdditionalWorkflowDescription'),''), w.Name) AS WorkflowDisplayName, w.Name AS WorkflowName, s.Name AS StepName, s.Action_Roles_Id AS ActionRoleId, ar.RoleName AS ActionRoleName, st.Assigned_Users_Id AS AssignedUserId, NULLIF(TRIM(COALESCE(su.Firstname,'') + ' ' + COALESCE(su.Lastname,'')),'') AS AssignedToName, st.CreateDate AS StepCreateDateUtc, COALESCE(TRY_CAST(JSON_VALUE(s.Workflow_StepTypes_ConfigData,'$.EstimatedDaysToComplete') AS int),0) AS EstimatedDaysToComplete, COALESCE(CASE WHEN h.Context_Object_Code='ACCOUNT' THEN h.Object_RefID END, or_obj.ObjectScreen_RefID, or_order.ObjectScreen_RefID) AS AccountId
                        FROM Workflow_StepTransactions st
                        JOIN Workflow_Transactions_Headers h ON h.ID = st.Workflow_Transactions_Headers_ID
                        JOIN Workflow_Steps s ON s.ID = st.Workflow_Steps_ID
                        JOIN Workflows w ON w.ID = h.Workflows_ID
                        LEFT JOIN Security_Users su ON su.User_Id = st.Assigned_Users_Id
                        LEFT JOIN Security_Roles ar ON ar.Role_Id = s.Action_Roles_Id
                        LEFT JOIN ObjectRelations or_obj ON h.Context_Object_Code NOT IN ('ACCOUNT','COORDERITEMS') AND ISNULL(or_obj.DelFlag,0)=0 AND or_obj.ObjectScreen_ObjectCode='ACCOUNT' AND or_obj.ObjectScreen_Code=h.Context_Object_Code AND or_obj.AssociatedObject_RefID=h.Object_RefID
                        LEFT JOIN Client_OrderItems coi ON h.Context_Object_Code='COORDERITEMS' AND coi.ID=h.Object_RefID
                        LEFT JOIN Client_Orders co ON co.ID=coi.Client_Orders_ID
                        LEFT JOIN ObjectRelations or_order ON h.Context_Object_Code='COORDERITEMS' AND ISNULL(or_order.DelFlag,0)=0 AND or_order.ObjectScreen_ObjectCode='ACCOUNT' AND or_order.AssociatedObject_RefID=co.ID AND or_order.AssociatedObject_Code='ORDER'
                        WHERE ISNULL(st.DelFlag,0)=0 AND ISNULL(h.DelFlag,0)=0 AND ISNULL(s.DelFlag,0)=0 AND ISNULL(w.DelFlag,0)=0 AND ISNULL(h.WorkflowComplete,0)=0 AND ISNULL(st.StepExecuted,0)=0 AND ISNULL(st.ExecutionStopped,0)=0
                    )
                    SELECT b.*, acc.Name AS AccountName, acc.AccountManagerUser_ID AS AccountManagerUserId, NULLIF(TRIM(COALESCE(am.Firstname,'') + ' ' + COALESCE(am.Lastname,'')),'') AS AccountManagerName
                    FROM base b
                    LEFT JOIN Accounts acc ON acc.ID=b.AccountId
                    LEFT JOIN Security_Users am ON am.User_Id=acc.AccountManagerUser_ID;";

                var pendingRows = db.Database.SqlQuery<PendingTaskRow>(pendingSql).ToList();

                HashSet<DateTime> holidayDates;
                try
                {
                    holidayDates = new HashSet<DateTime>(db.Database.SqlQuery<DateTime>(@"SELECT HolidayDate FROM Workflow_BusinessCalendarHolidays WHERE COALESCE(DelFlag,0)=0").Select(x => x.Date));
                }
                catch
                {
                    holidayDates = new HashSet<DateTime>();
                }

                Func<PendingTaskRow, int?> getOwnerUserId = r =>
                {
                    if (r.ActionRoleId.HasValue && !implicitOwnerRoleIds.Contains(r.ActionRoleId.Value)) return null;
                    if (r.AssignedUserId.HasValue) return r.AssignedUserId;
                    if (r.ActionRoleId.HasValue && implicitOwnerRoleIds.Contains(r.ActionRoleId.Value) && r.AccountManagerUserId.HasValue)
                        return r.AccountManagerUserId;
                    return null;
                };

                Func<PendingTaskRow, bool> canCurrentUserAct = r =>
                    !r.AssignedUserId.HasValue && r.ActionRoleId.HasValue && currentUserRoleIds.Contains(r.ActionRoleId.Value);

                Func<PendingTaskRow, bool> canSeeRow = r =>
                {
                    if (isGlobalLeader) return true;
                    if (isTeamLeader)
                    {
                        if (r.ActionRoleId.HasValue && teamRoleIds.Contains(r.ActionRoleId.Value)) return true;
                        if (r.AssignedUserId.HasValue && teamMemberUserIds.Contains(r.AssignedUserId.Value)) return true;
                        if (!r.AssignedUserId.HasValue && r.ActionRoleId.HasValue && implicitOwnerRoleIds.Contains(r.ActionRoleId.Value) && r.AccountManagerUserId.HasValue && teamMemberUserIds.Contains(r.AccountManagerUserId.Value)) return true;
                        return false;
                    }
                    if (r.AssignedUserId == currentUserId) return true;
                    if (canCurrentUserAct(r)) return true;
                    if (!r.AssignedUserId.HasValue && r.ActionRoleId.HasValue && implicitOwnerRoleIds.Contains(r.ActionRoleId.Value) && r.AccountManagerUserId == currentUserId) return true;
                    return false;
                };

                var visible = pendingRows.Where(canSeeRow).ToList();

                var nowUtc = DateTime.UtcNow;
                DateTime rangeFromUtc;
                DateTime rangeToUtc = nowUtc;
                var dateRange = (formData["DateRange"] ?? "Last30Days").Trim();
                switch (dateRange)
                {
                    case "CurrentMonth":
                        rangeFromUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                        break;
                    case "CurrentYear":
                        rangeFromUtc = new DateTime(nowUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        break;
                    case "Custom":
                        DateTime dt;
                        if (DateTime.TryParse(formData["FromDateUtc"], out dt)) rangeFromUtc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                        else rangeFromUtc = nowUtc.AddDays(-30);
                        if (DateTime.TryParse(formData["ToDateUtc"], out dt)) rangeToUtc = DateTime.SpecifyKind(dt, DateTimeKind.Utc).AddDays(1).AddSeconds(-1);
                        break;
                    default:
                        rangeFromUtc = nowUtc.AddDays(-30);
                        break;
                }

                var bucket1Label = "1-" + Bucket1MaxBusinessDays + " days";
                var bucket2Label = (Bucket1MaxBusinessDays + 1) + "-" + Bucket2MaxBusinessDays + " days";
                var bucket3Label = (Bucket2MaxBusinessDays + 1) + "+ days";

                Func<PendingTaskRow, int?> businessDaysRemaining = r =>
                {
                    if (r.EstimatedDaysToComplete <= 0) return Bucket2MaxBusinessDays + 1;
                    var due = AddBusinessDays(r.StepCreateDateUtc, r.EstimatedDaysToComplete, holidayDates).Date;
                    if (due >= nowUtc.Date) return CountBusinessDays(nowUtc.Date, due, holidayDates) - 1;
                    return -CountBusinessDays(due, nowUtc.Date, holidayDates);
                };

                Func<PendingTaskRow, string> bucketFor = r =>
                {
                    var rem = businessDaysRemaining(r) ?? (Bucket2MaxBusinessDays + 1);
                    if (rem < 0)
                    {
                        var overdueDays = Math.Abs(rem);
                        if (overdueDays <= Bucket1MaxBusinessDays) return "Overdue1To5";
                        if (overdueDays <= Bucket2MaxBusinessDays) return "Overdue6To25";
                        return "Overdue26Plus";
                    }

                    if (rem <= Bucket1MaxBusinessDays) return "Due1To5";
                    if (rem <= Bucket2MaxBusinessDays) return "Due6To25";
                    return "Due26Plus";
                };

                Func<IEnumerable<PendingTaskRow>, DrilldownKpiSummary> summarize = rows =>
                {
                    var list = rows.ToList();
                    return new DrilldownKpiSummary
                    {
                        PendingCount = list.Count,
                        Due1To5Count = list.Count(x => bucketFor(x) == "Due1To5"),
                        Due6To25Count = list.Count(x => bucketFor(x) == "Due6To25"),
                        Due26PlusCount = list.Count(x => bucketFor(x) == "Due26Plus"),
                        Overdue1To5Count = list.Count(x => bucketFor(x) == "Overdue1To5"),
                        Overdue6To25Count = list.Count(x => bucketFor(x) == "Overdue6To25"),
                        Overdue26PlusCount = list.Count(x => bucketFor(x) == "Overdue26Plus")
                    };
                };

                Func<IEnumerable<PendingTaskRow>, string, IEnumerable<PendingTaskRow>> applyBucket = (rows, b) =>
                {
                    if (string.IsNullOrWhiteSpace(b) || b.Equals("All", StringComparison.OrdinalIgnoreCase)) return rows;
                    return rows.Where(x => string.Equals(bucketFor(x), b, StringComparison.OrdinalIgnoreCase));
                };

                // completed for on-time/avg delay at user level
                var completedSql = @"SELECT
                        st.Assigned_Users_Id AssignedUserId,
                        s.Action_Roles_Id ActionRoleId,
                        st.CreateDate StepCreateDateUtc,
                        st.ActionedBy_Users_Time ActionedByUsersTimeUtc,
                        st.ActionedBy_Users_Id ActionedByUsersId,
                        COALESCE(TRY_CAST(JSON_VALUE(s.Workflow_StepTypes_ConfigData,'$.EstimatedDaysToComplete') AS int),0) EstimatedDaysToComplete
                    FROM Workflow_StepTransactions st
                    JOIN Workflow_Transactions_Headers h ON h.ID = st.Workflow_Transactions_Headers_ID
                    JOIN Workflow_Steps s ON s.ID = st.Workflow_Steps_ID
                    WHERE COALESCE(st.DelFlag,0)=0
                      AND COALESCE(h.DelFlag,0)=0
                      AND COALESCE(s.DelFlag,0)=0
                      AND COALESCE(st.StepExecuted,0)=1
                      AND COALESCE(st.ExecutionStopped,0)=0
                      AND st.ActionedBy_Users_Time IS NOT NULL
                      AND st.ActionedBy_Users_Time >= @p0
                      AND st.ActionedBy_Users_Time <= @p1";
                var completedRows = db.Database.SqlQuery<CompletedTaskRow>(completedSql, rangeFromUtc, rangeToUtc).ToList();

                Func<int?, DrilldownKpiSummary> userCompletionKpi = uid =>
                {
                    var data = completedRows.Where(c => (c.AssignedUserId ?? c.ActionedByUsersId) == uid).ToList();
                    int ontime = 0, samples = 0;
                    decimal totalDays = 0m, totalDelay = 0m;
                    foreach (var c in data)
                    {
                        if (!c.ActionedByUsersTimeUtc.HasValue) continue;
                        var bd = CountBusinessDays(c.StepCreateDateUtc, c.ActionedByUsersTimeUtc.Value, holidayDates);
                        totalDays += bd;
                        if (c.EstimatedDaysToComplete > 0)
                        {
                            samples++;
                            if (bd <= c.EstimatedDaysToComplete) ontime++;
                            totalDelay += Math.Max(0, bd - c.EstimatedDaysToComplete);
                        }
                    }
                    return new DrilldownKpiSummary
                    {
                        CompletedCount = data.Count,
                        CompletedDelay1To5Count = data.Count(c => c.ActionedByUsersTimeUtc.HasValue && c.EstimatedDaysToComplete > 0 && Math.Max(0, CountBusinessDays(c.StepCreateDateUtc, c.ActionedByUsersTimeUtc.Value, holidayDates) - c.EstimatedDaysToComplete) >= 1 && Math.Max(0, CountBusinessDays(c.StepCreateDateUtc, c.ActionedByUsersTimeUtc.Value, holidayDates) - c.EstimatedDaysToComplete) <= Bucket1MaxBusinessDays),
                        CompletedDelay6To25Count = data.Count(c => c.ActionedByUsersTimeUtc.HasValue && c.EstimatedDaysToComplete > 0 && Math.Max(0, CountBusinessDays(c.StepCreateDateUtc, c.ActionedByUsersTimeUtc.Value, holidayDates) - c.EstimatedDaysToComplete) > Bucket1MaxBusinessDays && Math.Max(0, CountBusinessDays(c.StepCreateDateUtc, c.ActionedByUsersTimeUtc.Value, holidayDates) - c.EstimatedDaysToComplete) <= Bucket2MaxBusinessDays),
                        CompletedDelay26PlusCount = data.Count(c => c.ActionedByUsersTimeUtc.HasValue && c.EstimatedDaysToComplete > 0 && Math.Max(0, CountBusinessDays(c.StepCreateDateUtc, c.ActionedByUsersTimeUtc.Value, holidayDates) - c.EstimatedDaysToComplete) > Bucket2MaxBusinessDays),
                        OnTimeCompletionRatePct = samples == 0 ? (decimal?)null : Math.Round((ontime * 100m) / samples, 2),
                        AvgBusinessDaysToComplete = data.Count == 0 ? (decimal?)null : Math.Round(totalDays / data.Count, 2),
                        AvgBusinessDaysDelay = samples == 0 ? (decimal?)null : Math.Round(totalDelay / samples, 2)
                    };
                };

                if (string.IsNullOrWhiteSpace(level))
                {
                    level = (isGlobalLeader || isTeamLeader) ? "User" : "Workflow";
                }

                var summaryKpi = summarize(visible);

                if (string.Equals(level, "User", StringComparison.OrdinalIgnoreCase))
                {
                    var userGroups = visible
                        .Select(r => new { Row = r, OwnerUserId = getOwnerUserId(r) })
                        .Where(x => x.OwnerUserId.HasValue)
                        .GroupBy(x => new { UserId = x.OwnerUserId.Value, UserName = x.Row.AssignedToName ?? x.Row.AccountManagerName ?? ("User #" + x.OwnerUserId.Value) })
                        .Select(g => {
                            var rows = g.Select(x => x.Row);
                            var k = summarize(rows);
                            var ck = userCompletionKpi(g.Key.UserId);
                            return new
                            {
                                RowType = "User",
                                UserId = (int?)g.Key.UserId,
                                RoleId = (int?)null,
                                UserName = g.Key.UserName,
                                Pending = k.PendingCount,
                                Due1To5 = k.Due1To5Count,
                                Due6To25 = k.Due6To25Count,
                                Due26Plus = k.Due26PlusCount,
                                Overdue1To5 = k.Overdue1To5Count,
                                Overdue6To25 = k.Overdue6To25Count,
                                Overdue26Plus = k.Overdue26PlusCount,
                                OnTimePct = ck.OnTimeCompletionRatePct,
                                AvgDelay = ck.AvgBusinessDaysDelay,
                                CompletedDelay1To5 = includeCompletedAgingBuckets ? (int?)ck.CompletedDelay1To5Count : null,
                                CompletedDelay6To25 = includeCompletedAgingBuckets ? (int?)ck.CompletedDelay6To25Count : null,
                                CompletedDelay26Plus = includeCompletedAgingBuckets ? (int?)ck.CompletedDelay26PlusCount : null
                            };
                        });

                    var roleGroups = visible
                        .Where(r => !getOwnerUserId(r).HasValue && r.ActionRoleId.HasValue)
                        .GroupBy(r => new { RoleId = r.ActionRoleId.Value, RoleName = r.ActionRoleName ?? ("Role #" + r.ActionRoleId.Value) })
                        .Select(g => {
                            var k = summarize(g);
                            return new
                            {
                                RowType = "Role",
                                UserId = (int?)null,
                                RoleId = (int?)g.Key.RoleId,
                                UserName = g.Key.RoleName,
                                Pending = k.PendingCount,
                                Due1To5 = k.Due1To5Count,
                                Due6To25 = k.Due6To25Count,
                                Due26Plus = k.Due26PlusCount,
                                Overdue1To5 = k.Overdue1To5Count,
                                Overdue6To25 = k.Overdue6To25Count,
                                Overdue26Plus = k.Overdue26PlusCount,
                                OnTimePct = (decimal?)null,
                                AvgDelay = (decimal?)null,
                                CompletedDelay1To5 = includeCompletedAgingBuckets ? 0 : (int?)null,
                                CompletedDelay6To25 = includeCompletedAgingBuckets ? 0 : (int?)null,
                                CompletedDelay26Plus = includeCompletedAgingBuckets ? 0 : (int?)null
                            };
                        });

                    var grouped = userGroups.Concat(roleGroups).OrderByDescending(x => x.Pending).ToList();

                    var pageRows = grouped.Skip((page - 1) * perPage).Take(perPage).Cast<object>().ToList();
                    return Ok(new WorkflowTaskDrilldownResponse
                    {
                        Level = "User",
                        SummaryKpi = summaryKpi,
                        AggregateData = pageRows,
                        TaskData = new List<object>(),
                        UserKpi = null,
                        Meta = new { page = page, perpage = perPage, total = grouped.Count, pages = (int)Math.Ceiling(grouped.Count / (double)perPage), IncludeCompletedAgingBuckets = includeCompletedAgingBuckets, BucketLabels = new { Due1To5 = "Due: " + bucket1Label, Due6To25 = "Due: " + bucket2Label, Due26Plus = "Due: " + bucket3Label, Overdue1To5 = "Overdue: " + bucket1Label, Overdue6To25 = "Overdue: " + bucket2Label, Overdue26Plus = "Overdue: " + bucket3Label, CompletedDelay1To5 = "Completed overdue: " + bucket1Label, CompletedDelay6To25 = "Completed overdue: " + bucket2Label, CompletedDelay26Plus = "Completed overdue: " + bucket3Label } },
                        CanExport = canExport,
                        IsGlobalLeader = isGlobalLeader,
                        IsTeamLeader = isTeamLeader
                    });
                }

                if (string.Equals(level, "Workflow", StringComparison.OrdinalIgnoreCase))
                {
                    var scoped = visible;
                    if (selectedUserId.HasValue)
                        scoped = scoped.Where(r => getOwnerUserId(r) == selectedUserId.Value).ToList();
                    if (selectedRoleId.HasValue)
                        scoped = scoped.Where(r => !getOwnerUserId(r).HasValue && r.ActionRoleId == selectedRoleId.Value).ToList();

                    var grouped = applyBucket(scoped, bucket)
                        .GroupBy(r => r.WorkflowName)
                        .Select(g => {
                            var k = summarize(g);
                            return new
                            {
                                WorkflowName = g.Key,
                                WorkflowDisplayName = g.Key,
                                Pending = k.PendingCount,
                                Due1To5 = k.Due1To5Count,
                                Due6To25 = k.Due6To25Count,
                                Due26Plus = k.Due26PlusCount,
                                Overdue1To5 = k.Overdue1To5Count,
                                Overdue6To25 = k.Overdue6To25Count,
                                Overdue26Plus = k.Overdue26PlusCount
                            };
                        })
                        .OrderByDescending(x => x.Pending)
                        .ToList();

                    var userKpi = selectedUserId.HasValue ? userCompletionKpi(selectedUserId.Value) : null;
                    var pageRows = grouped.Skip((page - 1) * perPage).Take(perPage).Cast<object>().ToList();
                    return Ok(new WorkflowTaskDrilldownResponse
                    {
                        Level = "Workflow",
                        SummaryKpi = summarize(scoped),
                        UserKpi = userKpi,
                        AggregateData = pageRows,
                        TaskData = new List<object>(),
                        Meta = new { page = page, perpage = perPage, total = grouped.Count, pages = (int)Math.Ceiling(grouped.Count / (double)perPage), IncludeCompletedAgingBuckets = includeCompletedAgingBuckets, BucketLabels = new { Due1To5 = "Due: " + bucket1Label, Due6To25 = "Due: " + bucket2Label, Due26Plus = "Due: " + bucket3Label, Overdue1To5 = "Overdue: " + bucket1Label, Overdue6To25 = "Overdue: " + bucket2Label, Overdue26Plus = "Overdue: " + bucket3Label, CompletedDelay1To5 = "Completed overdue: " + bucket1Label, CompletedDelay6To25 = "Completed overdue: " + bucket2Label, CompletedDelay26Plus = "Completed overdue: " + bucket3Label } },
                        CanExport = canExport,
                        IsGlobalLeader = isGlobalLeader,
                        IsTeamLeader = isTeamLeader
                    });
                }

                // Task details level
                var taskScoped = visible;
                if (selectedUserId.HasValue) taskScoped = taskScoped.Where(r => getOwnerUserId(r) == selectedUserId.Value).ToList();
                if (selectedWorkflowHeaderId.HasValue) taskScoped = taskScoped.Where(r => r.WorkflowHeaderId == selectedWorkflowHeaderId.Value).ToList();
                if (selectedRoleId.HasValue) taskScoped = taskScoped.Where(r => !getOwnerUserId(r).HasValue && r.ActionRoleId == selectedRoleId.Value).ToList();
                if (!string.IsNullOrWhiteSpace(selectedWorkflowName)) taskScoped = taskScoped.Where(r => string.Equals(r.WorkflowName, selectedWorkflowName, StringComparison.OrdinalIgnoreCase)).ToList();
                taskScoped = applyBucket(taskScoped, bucket).ToList();

                var taskRows = taskScoped.Select(r => {
                    DateTime? due = r.EstimatedDaysToComplete > 0 ? AddBusinessDays(r.StepCreateDateUtc, r.EstimatedDaysToComplete, holidayDates) : (DateTime?)null;
                    return new
                    {
                        r.WorkflowHeaderId,
                        TaskName = r.WorkflowDisplayName,
                        r.WorkflowName,
                        r.StepName,
                        AssigneeOrRole = !string.IsNullOrWhiteSpace(r.AssignedToName) ? r.AssignedToName : (!string.IsNullOrWhiteSpace(r.ActionRoleName) ? r.ActionRoleName : "Unassigned"),
                        r.AccountName,
                        r.AccountManagerName,
                        BusinessDaysRemaining = due.HasValue ? (due.Value.Date >= nowUtc.Date ? (int?)(CountBusinessDays(nowUtc.Date, due.Value.Date, holidayDates) - 1) : (int?)(-CountBusinessDays(due.Value.Date, nowUtc.Date, holidayDates))) : null,
                        SlaBreach = due.HasValue && due.Value.Date < nowUtc.Date,
                        StepCreateDateUtc = r.StepCreateDateUtc,
                        StepCreateDate = r.StepCreateDateUtc.ToString("yyyy-MM-dd")
                    };
                }).OrderByDescending(x => x.SlaBreach).ThenBy(x => x.BusinessDaysRemaining ?? int.MaxValue).ThenBy(x => x.StepCreateDateUtc).ToList();

                string exportMode = (formData["ExportMode"] ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(exportMode))
                {
                    if (!canExport) return BadRequest("Export is allowed only for Workflow_TeamLeader or Workflow_GlobalLeader.");
                    var sb = new StringBuilder();
                    sb.AppendLine("WorkflowHeaderId,TaskName,StepName,AssigneeOrRole,AccountName,AccountManagerName,StepCreateDate,BusinessDaysRemaining,SlaBreach");
                    foreach (var r in taskRows)
                    {
                        sb.AppendLine(string.Join(",", new[] {
                            r.WorkflowHeaderId.ToString(), CsvEscape(r.TaskName), CsvEscape(r.StepName), CsvEscape(r.AssigneeOrRole),
                            CsvEscape(r.AccountName), CsvEscape(r.AccountManagerName),
                            CsvEscape(r.StepCreateDate),
                            r.BusinessDaysRemaining.HasValue ? r.BusinessDaysRemaining.Value.ToString() : "", r.SlaBreach ? "1" : "0"
                        }));
                    }
                    var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    var result = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
                    result.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
                    result.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                    {
                        FileName = string.Equals(exportMode, "Excel", StringComparison.OrdinalIgnoreCase) ? "workflow_task_drilldown.xls" : "workflow_task_drilldown.csv"
                    };
                    return ResponseMessage(result);
                }

                var pageTaskRows = taskRows.Skip((page - 1) * perPage).Take(perPage).Cast<object>().ToList();
                return Ok(new WorkflowTaskDrilldownResponse
                {
                    Level = "Task",
                    SummaryKpi = summarize(taskScoped),
                    UserKpi = selectedUserId.HasValue ? userCompletionKpi(selectedUserId.Value) : null,
                    AggregateData = new List<object>(),
                    TaskData = pageTaskRows,
                    Meta = new { page = page, perpage = perPage, total = taskRows.Count, pages = (int)Math.Ceiling(taskRows.Count / (double)perPage), IncludeCompletedAgingBuckets = includeCompletedAgingBuckets, BucketLabels = new { Due1To5 = "Due: " + bucket1Label, Due6To25 = "Due: " + bucket2Label, Due26Plus = "Due: " + bucket3Label, Overdue1To5 = "Overdue: " + bucket1Label, Overdue6To25 = "Overdue: " + bucket2Label, Overdue26Plus = "Overdue: " + bucket3Label, CompletedDelay1To5 = "Completed overdue: " + bucket1Label, CompletedDelay6To25 = "Completed overdue: " + bucket2Label, CompletedDelay26Plus = "Completed overdue: " + bucket3Label } },
                    CanExport = canExport,
                    IsGlobalLeader = isGlobalLeader,
                    IsTeamLeader = isTeamLeader
                });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message + (e.InnerException != null ? ("::" + e.InnerException.Message) : ""));
            }
        }



        private const int MaxOutDegreePerStep = 25;
        private const int MaxInDegreePerStep = 50;

        private bool CanViewWorkflowTemplate()
        {
            return CRM.Classes.Helpers.CurrentContext.CurrentUser.HasRoles("WorkflowViewer,WorkflowEditor,WorkflowAdministrator");
        }

        private bool CanEditWorkflowTemplate()
        {
            return CRM.Classes.Helpers.CurrentContext.CurrentUser.HasRoles("WorkflowEditor,WorkflowAdministrator");
        }

        private IHttpActionResult ForbiddenTemplateAccess()
        {
            return BadRequest("Access denied. Editing requires WorkflowEditor or WorkflowAdministrator role.");
        }

        private IHttpActionResult ForbiddenTemplateViewAccess()
        {
            return BadRequest("Access denied. Requires WorkflowViewer, WorkflowEditor, or WorkflowAdministrator role.");
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
            public List<StepTypeOptionDto> StepTypes { get; set; }
            public List<RoleOptionDto> Roles { get; set; }
            public bool CanEdit { get; set; }
        }

        public sealed class StepTypeOptionDto
        {
            public byte ID { get; set; }
            public string Name { get; set; }
        }

        public sealed class RoleOptionDto
        {
            public int Role_Id { get; set; }
            public string RoleName { get; set; }
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
            public string Workflow_StepTypes_ConfigData { get; set; }
            public string UserStepInstructions { get; set; }
            public string PreStepCompletion_DataValidation { get; set; }
            public string OnStepCompletion_Notifications { get; set; }
            public string OnStepCompletion_FieldUpdates { get; set; }
            public string OnStepCompletion_APICalls { get; set; }
            public string OnStepReview_Notifications { get; set; }
            public string OnStepReview_FieldUpdates { get; set; }
            public string OnStepReview_APICalls { get; set; }
            public string OnStepReject_Notifications { get; set; }
            public string OnStepReject_FieldUpdates { get; set; }
            public string OnStepReject_APICalls { get; set; }
            public string OnStepError_Notifications { get; set; }
            public string OnStepCreate_Notifications { get; set; }
            public string OnStepCreate_FieldUpdates { get; set; }
            public string OnStepCreate_APICalls { get; set; }
            public int? Action_Roles_Id { get; set; }
            public int? TaskAssigner_Roles_Id { get; set; }
        }

        public sealed class UpdateStepCommand
        {
            public short WorkflowId { get; set; }
            public short StepId { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public bool? IsStartStep { get; set; }
            public byte? Workflow_StepTypes_ID { get; set; }
            public string Workflow_StepTypes_ConfigData { get; set; }
            public string UserStepInstructions { get; set; }
            public string PreStepCompletion_DataValidation { get; set; }
            public string OnStepCompletion_Notifications { get; set; }
            public string OnStepCompletion_FieldUpdates { get; set; }
            public string OnStepCompletion_APICalls { get; set; }
            public string OnStepReview_Notifications { get; set; }
            public string OnStepReview_FieldUpdates { get; set; }
            public string OnStepReview_APICalls { get; set; }
            public string OnStepReject_Notifications { get; set; }
            public string OnStepReject_FieldUpdates { get; set; }
            public string OnStepReject_APICalls { get; set; }
            public string OnStepError_Notifications { get; set; }
            public string OnStepCreate_Notifications { get; set; }
            public string OnStepCreate_FieldUpdates { get; set; }
            public string OnStepCreate_APICalls { get; set; }
            public int? Action_Roles_Id { get; set; }
            public int? TaskAssigner_Roles_Id { get; set; }
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
            if (!CanViewWorkflowTemplate())
            {
                return ForbiddenTemplateViewAccess();
            }

            var workflow = db.Workflows.FirstOrDefault(w => w.ID == workflowId && (w.DelFlag ?? false) == false);
            if (workflow == null)
            {
                return NotFound();
            }

            if (!Enum.TryParse<CRM.Models.Workflows.Workflow.WorkflowCode>(workflow.Code, true, out var workflowCode))
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

            var stepTypes = db.Workflow_StepTypes
                .OrderBy(t => t.Name)
                .Select(t => new StepTypeOptionDto
                {
                    ID = t.ID,
                    Name = t.Name
                })
                .ToList();

            var roles = db.Database.SqlQuery<RoleOptionDto>(
                "SELECT Role_Id, RoleName FROM Security_Roles ORDER BY RoleName")
                .ToList();

            return Ok(new WorkflowTemplateGraphResponse
            {
                WorkflowId = workflow.ID,
                WorkflowCode = workflow.Code,
                WorkflowName = workflow.Name,
                Nodes = layout.Nodes.ToList(),
                Edges = edges,
                StepTypes = stepTypes,
                Roles = roles,
                CanEdit = CanEditWorkflowTemplate()
            });
        }

        private bool IsValidJsonOrEmpty(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            try
            {
                JToken.Parse(value);
                return true;
            }
            catch
            {
                return false;
            }
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

            if (!IsValidJsonOrEmpty(cmd.Workflow_StepTypes_ConfigData)
                || !IsValidJsonOrEmpty(cmd.PreStepCompletion_DataValidation)
                || !IsValidJsonOrEmpty(cmd.OnStepCompletion_Notifications)
                || !IsValidJsonOrEmpty(cmd.OnStepCompletion_FieldUpdates)
                || !IsValidJsonOrEmpty(cmd.OnStepCompletion_APICalls)
                || !IsValidJsonOrEmpty(cmd.OnStepReview_Notifications)
                || !IsValidJsonOrEmpty(cmd.OnStepReview_FieldUpdates)
                || !IsValidJsonOrEmpty(cmd.OnStepReview_APICalls)
                || !IsValidJsonOrEmpty(cmd.OnStepReject_Notifications)
                || !IsValidJsonOrEmpty(cmd.OnStepReject_FieldUpdates)
                || !IsValidJsonOrEmpty(cmd.OnStepReject_APICalls)
                || !IsValidJsonOrEmpty(cmd.OnStepError_Notifications)
                || !IsValidJsonOrEmpty(cmd.OnStepCreate_Notifications)
                || !IsValidJsonOrEmpty(cmd.OnStepCreate_FieldUpdates)
                || !IsValidJsonOrEmpty(cmd.OnStepCreate_APICalls))
            {
                return BadRequest("One or more JSON fields are invalid.");
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
                Workflow_StepTypes_ConfigData = string.IsNullOrWhiteSpace(cmd.Workflow_StepTypes_ConfigData) ? stepType.GetConfigDataTemplateJson() : cmd.Workflow_StepTypes_ConfigData,
                UserStepInstructions = cmd.UserStepInstructions,
                PreStepCompletion_DataValidation = cmd.PreStepCompletion_DataValidation,
                OnStepCompletion_Notifications = cmd.OnStepCompletion_Notifications,
                OnStepCompletion_FieldUpdates = cmd.OnStepCompletion_FieldUpdates,
                OnStepCompletion_APICalls = cmd.OnStepCompletion_APICalls,
                OnStepReview_Notifications = cmd.OnStepReview_Notifications,
                OnStepReview_FieldUpdates = cmd.OnStepReview_FieldUpdates,
                OnStepReview_APICalls = cmd.OnStepReview_APICalls,
                OnStepReject_Notifications = cmd.OnStepReject_Notifications,
                OnStepReject_FieldUpdates = cmd.OnStepReject_FieldUpdates,
                OnStepReject_APICalls = cmd.OnStepReject_APICalls,
                OnStepError_Notifications = cmd.OnStepError_Notifications,
                OnStepCreate_Notifications = cmd.OnStepCreate_Notifications,
                OnStepCreate_FieldUpdates = cmd.OnStepCreate_FieldUpdates,
                OnStepCreate_APICalls = cmd.OnStepCreate_APICalls,
                Action_Roles_Id = cmd.Action_Roles_Id,
                TaskAssigner_Roles_Id = cmd.TaskAssigner_Roles_Id,
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

            if (!IsValidJsonOrEmpty(cmd.Workflow_StepTypes_ConfigData)
                || !IsValidJsonOrEmpty(cmd.PreStepCompletion_DataValidation)
                || !IsValidJsonOrEmpty(cmd.OnStepCompletion_Notifications)
                || !IsValidJsonOrEmpty(cmd.OnStepCompletion_FieldUpdates)
                || !IsValidJsonOrEmpty(cmd.OnStepCompletion_APICalls)
                || !IsValidJsonOrEmpty(cmd.OnStepReview_Notifications)
                || !IsValidJsonOrEmpty(cmd.OnStepReview_FieldUpdates)
                || !IsValidJsonOrEmpty(cmd.OnStepReview_APICalls)
                || !IsValidJsonOrEmpty(cmd.OnStepReject_Notifications)
                || !IsValidJsonOrEmpty(cmd.OnStepReject_FieldUpdates)
                || !IsValidJsonOrEmpty(cmd.OnStepReject_APICalls)
                || !IsValidJsonOrEmpty(cmd.OnStepError_Notifications)
                || !IsValidJsonOrEmpty(cmd.OnStepCreate_Notifications)
                || !IsValidJsonOrEmpty(cmd.OnStepCreate_FieldUpdates)
                || !IsValidJsonOrEmpty(cmd.OnStepCreate_APICalls))
            {
                return BadRequest("One or more JSON fields are invalid.");
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

            if (cmd.UserStepInstructions != null)
            {
                step.UserStepInstructions = cmd.UserStepInstructions;
            }
            if (cmd.Workflow_StepTypes_ConfigData != null)
            {
                step.Workflow_StepTypes_ConfigData = cmd.Workflow_StepTypes_ConfigData;
            }
            if (cmd.PreStepCompletion_DataValidation != null)
            {
                step.PreStepCompletion_DataValidation = cmd.PreStepCompletion_DataValidation;
            }
            if (cmd.OnStepCompletion_Notifications != null)
            {
                step.OnStepCompletion_Notifications = cmd.OnStepCompletion_Notifications;
            }
            if (cmd.OnStepCompletion_FieldUpdates != null)
            {
                step.OnStepCompletion_FieldUpdates = cmd.OnStepCompletion_FieldUpdates;
            }
            if (cmd.OnStepCompletion_APICalls != null)
            {
                step.OnStepCompletion_APICalls = cmd.OnStepCompletion_APICalls;
            }
            if (cmd.OnStepReview_Notifications != null)
            {
                step.OnStepReview_Notifications = cmd.OnStepReview_Notifications;
            }
            if (cmd.OnStepReview_FieldUpdates != null)
            {
                step.OnStepReview_FieldUpdates = cmd.OnStepReview_FieldUpdates;
            }
            if (cmd.OnStepReview_APICalls != null)
            {
                step.OnStepReview_APICalls = cmd.OnStepReview_APICalls;
            }
            if (cmd.OnStepReject_Notifications != null)
            {
                step.OnStepReject_Notifications = cmd.OnStepReject_Notifications;
            }
            if (cmd.OnStepReject_FieldUpdates != null)
            {
                step.OnStepReject_FieldUpdates = cmd.OnStepReject_FieldUpdates;
            }
            if (cmd.OnStepReject_APICalls != null)
            {
                step.OnStepReject_APICalls = cmd.OnStepReject_APICalls;
            }
            if (cmd.OnStepError_Notifications != null)
            {
                step.OnStepError_Notifications = cmd.OnStepError_Notifications;
            }
            if (cmd.OnStepCreate_Notifications != null)
            {
                step.OnStepCreate_Notifications = cmd.OnStepCreate_Notifications;
            }
            if (cmd.OnStepCreate_FieldUpdates != null)
            {
                step.OnStepCreate_FieldUpdates = cmd.OnStepCreate_FieldUpdates;
            }
            if (cmd.OnStepCreate_APICalls != null)
            {
                step.OnStepCreate_APICalls = cmd.OnStepCreate_APICalls;
            }
            step.Action_Roles_Id = cmd.Action_Roles_Id;
            step.TaskAssigner_Roles_Id = cmd.TaskAssigner_Roles_Id;

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

            if (cmd == null)
            {
                return BadRequest("Invalid command.");
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
                ConditionExpression = string.IsNullOrWhiteSpace(cmd.ConditionExpression) ? null : cmd.ConditionExpression.Trim(),
                DisplayLabel = cmd.DisplayLabel,
                SortOrder = cmd.SortOrder ?? 0,
                IsDefaultPath = cmd.IsDefaultPath ?? false,
                IsActive = cmd.IsActive ?? true,
                CreateUserID = CurrentContext.CurrentUser.User_Id,
                CreateDate = DateTime.UtcNow,
                DelFlag = false
            };

            db.Workflow_StepTransitions.Add(transition);

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

            if (cmd == null)
            {
                return BadRequest("Invalid command.");
            }

            var transition = db.Workflow_StepTransitions.FirstOrDefault(t => t.ID == cmd.TransitionId && t.Workflows_ID == cmd.WorkflowId && (t.DelFlag ?? false) == false);
            if (transition == null)
            {
                return NotFound();
            }

            transition.ConditionExpression = string.IsNullOrWhiteSpace(cmd.ConditionExpression) ? null : cmd.ConditionExpression.Trim();
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
