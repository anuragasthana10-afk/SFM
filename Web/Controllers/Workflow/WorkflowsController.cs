using CRM.Business.Workflows;
using CRM.Classes.Helpers;
using CRM.Models.BusinessCodeDefinition;
using CRM.Models.Clients;
using CRM.Models.Security;
using CRM.Models.Workflows;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Http.Results;
using System.Web.Mvc;
using Web.Classes.ExtensionMethods;
using static CRM.Models.Workflows.Workflow;


namespace Web.Controllers.Workflow
{

    [CRMAuthorize]
    [Authorize]
    public class WorkflowsController : Controller
    {
        private ClientModel db = new ClientModel();
        private RBAC_Model database = new RBAC_Model();
        private BusinessCodeDefinitionModel bcd = new BusinessCodeDefinitionModel();

        private bool CanViewWorkflowTemplate()
        {
            return CRM.Classes.Helpers.CurrentContext.CurrentUser.HasRoles("WorkflowViewer,WorkflowEditor,WorkflowAdministrator");
        }

        private bool CanEditWorkflowTemplate()
        {
            return CRM.Classes.Helpers.CurrentContext.CurrentUser.HasRoles("WorkflowEditor,WorkflowAdministrator");
        }

        // GET: Workflows
        public ActionResult IndexPartialView(string __csc = "", string __csc_refid = "")
        {
            ViewBag.ContextScreenCode = __csc;
            ViewBag.ContextScreen_RefID = __csc_refid;
            var workflow_list = db.Workflows.ToList();
            return PartialView("Index", workflow_list);
        }

        // GET: Workflows
        public ActionResult EditWorkflow(short ID, string __csc = "", string __csc_refid = "")
        {
            ViewBag.ContextScreenCode = __csc;
            ViewBag.ContextScreen_RefID = __csc_refid;
            var workflow = db.Workflows.Find(ID);
            if (workflow == null)
            {
                return HttpNotFound();
            }

            if (!CanViewWorkflowTemplate())
            {
                return new HttpStatusCodeResult(403, "Access denied. Requires WorkflowViewer, WorkflowEditor, or WorkflowAdministrator role.");
            }

            var workflowHelper = new CRM.Classes.Helpers.WorkflowHelpers.WorkFlow();
            WorkflowCode workflowCode;
            Enum.TryParse<WorkflowCode>(workflow.Code, out workflowCode);
            var workflowStepsMap = workflowHelper.CreateInMemoryWorkflowMap(workflowCode, db.Database.Connection);
            ViewBag.WorkflowID = workflow.ID;
            ViewBag.CanEditTemplate = CanEditWorkflowTemplate();

            return PartialView("WorkflowGraph", workflowStepsMap);
        }

        public ActionResult ListWorkFlowListPartialView(string __csc = "", string __csc_refid = "")
        {
            ViewBag.ContextScreenCode = __csc;
            ViewBag.ContextScreen_RefID = __csc_refid;
            int accountId = Convert.ToInt32(__csc_refid);
            List<WorkflowStatus> workflowStatuses = (new Accounts() { ID = accountId }).GetOpenWorkflowsStatus(__csc) ?? new List<WorkflowStatus>();

            return PartialView("WorkflowTransactionHeader", workflowStatuses);
        }

        public ActionResult ListWorkWithHeader(string __csc = "", string __csc_refid = "")
        {
            ViewBag.ContextScreenCode = __csc;
            ViewBag.ContextScreen_RefID = __csc_refid;
            int accountId = Convert.ToInt32(__csc_refid);
            List<WorkflowStatus> workflowStatuses = (new Accounts() { ID = accountId }).GetOpenWorkflowsStatus() ?? new List<WorkflowStatus>();

            return PartialView("WorkflowTransactionHeader", workflowStatuses);
        }


        public ActionResult RenderWorkflowView()
        {
            var jsonBody = new StreamReader(Request.InputStream).ReadToEnd();

            if (string.IsNullOrWhiteSpace(jsonBody))
            {
                return new HttpStatusCodeResult(400, "Request body is required.");
            }

            JObject json;
            try
            {
                json = JObject.Parse(jsonBody);
            }
            catch
            {
                return new HttpStatusCodeResult(400, "Invalid JSON payload.");
            }

            if (json["workflowHeaderID"] == null || json["workflowData"] == null)
            {
                return new HttpStatusCodeResult(400, "Required fields are missing.");
            }

            int workflowHeaderID = (int)json["workflowHeaderID"];
            var workflowDataToken = json["workflowData"];
            JObject workflowData;

            if (workflowDataToken.Type == JTokenType.String)
            {
                workflowData = JObject.Parse((string)workflowDataToken);
            }
            else if (workflowDataToken.Type == JTokenType.Object)
            {
                workflowData = (JObject)workflowDataToken;
            }
            else if (workflowDataToken.Type == JTokenType.Array)
            {
                workflowData = ((JArray)workflowDataToken).FirstOrDefault() as JObject;
            }
            else
            {
                workflowData = null;
            }

            ViewBag.TransactionHeaderID = workflowHeaderID;
            return PartialView("_WorkflowStatusPartial", workflowData);
        }


        public ActionResult JsonConfigTemplateList()
        {
            return View("JsonConfigTemplateList");
        }

        public ActionResult WorkFlowDashboardPartialView(string __csc = "", string __csc_refid = "")
        {
            ViewBag.ContextScreenCode = __csc;
            ViewBag.ContextScreen_RefID = __csc_refid;
            ViewBag.DisplayMode = "WorkflowDashboard";
            // List<WorkflowStatus> workflowStatuses = (new Accounts() { ID = 1111 }).GetOpenWorkflowsStatus() ?? new List<WorkflowStatus>();
            List<vwWorkflowStatus> workflowStatuses = new List<vwWorkflowStatus>();
            return PartialView("WorkflowDashboard", workflowStatuses);
        }

        public ActionResult WorkflowTaskDrilldown(string __csc = "", string __csc_refid = "")
        {
            ViewBag.ContextScreenCode = __csc;
            ViewBag.ContextScreen_RefID = __csc_refid;
            ViewBag.DisplayMode = "WorkflowDashboard";
            return PartialView("WorkflowTaskDrilldown");
        }

        /* public ActionResult WorkflowStatusPartial(int workflowTxnHeaderID, string __csc = "",  string __csc_refid = "")
         {
             ViewBag.ContextScreenCode = __csc;
             ViewBag.ContextScreen_RefID = __csc_refid;
             ViewBag.DisplayMode = "WorkflowStatus";

             var workflowTransactionHeader = db.Workflow_Transactions_Headers
                 .Where(w => w.ID == workflowTxnHeaderID)
                 .Include(w => w.Workflow)
                 .Include(w => w.Workflow_StepTransactions.Select(t => t.Workflow_Steps))
                 .FirstOrDefault();

             if (workflowTransactionHeader == null)
             {
                 return PartialView("_WorkflowStatusPartial", null);
             }

             var workflowStatus = workflowTransactionHeader.GetWorkflowStatus();

             return PartialView("_WorkflowStatusPartial", JsonConvert.SerializeObject(workflowStatus));
         }*/


    }
}
