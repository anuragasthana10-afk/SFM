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
       

        // GET: Workflows
        public ActionResult Index(string __csc = "", string __csc_refid = "")
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
            if(workflow == null)
            {
                return HttpNotFound();
            }

            var workflowHelper = new CRM.Classes.Helpers.WorkflowHelpers.WorkFlow();
            WorkflowCode workflowCode;
            Enum.TryParse<WorkflowCode>(workflow.Code, out workflowCode);
            var workflowStepsMap = workflowHelper.CreateInMemoryWorkflowMap(workflowCode, db.Database.Connection);

            return PartialView("WorkflowGraph", workflowStepsMap);
        }

        public ActionResult ListWorkFlowListPartialView(string __csc = "", string __csc_refid = "")
        {
            ViewBag.ContextScreenCode = __csc;
            ViewBag.ContextScreen_RefID = __csc_refid;
            int accountId = Convert.ToInt32(__csc_refid);
            List <WorkflowStatus>  workflowStatuses= (new Accounts() { ID = accountId }).GetOpenWorkflowsStatus()?? new List<WorkflowStatus>(); 

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
            // Read raw JSON body
            //string jsonBody = "{\r\n    \"workflowHeaderID\": \"7\",\r\n    \"workflowData\": \"{\\\"WorkflowCode\\\":\\\"COMPSETUP\\\",\\\"WorkflowName\\\":\\\"Company setup\\\",\\\"IsWorkflowComplete\\\":false,\\\"WorkflowCompleteTime\\\":null,\\\"Workflow_Transactions_Headers_ID\\\":7,\\\"WorkflowStepStatuses\\\":[{\\\"StepName\\\":\\\"start comp setup\\\",\\\"IsCurrentStep\\\":false,\\\"IsInErrorState\\\":false,\\\"IsStopped\\\":false,\\\"Status\\\":null,\\\"EstimatedDaysRemaining\\\":null,\\\"EstimatedDaysRemaining_StatusHint\\\":\\\"Normal\\\",\\\"UserComment\\\":\\\"oum\\\",\\\"ActionMessage\\\":\\\"Approve, Review or Reject this task / request.\\\",\\\"ActionedTime\\\":\\\"2025-11-28T06:14:02.16\\\",\\\"ActionedByUser\\\":\\\"Vimla Naik\\\",\\\"CreateTime\\\":\\\"2025-11-26T10:56:26.7046967\\\",\\\"Actions\\\":[]},{\\\"StepName\\\":\\\"Document collection\\\",\\\"IsCurrentStep\\\":true,\\\"IsInErrorState\\\":false,\\\"IsStopped\\\":false,\\\"Status\\\":\\\"Pending action.\\\",\\\"EstimatedDaysRemaining\\\":null,\\\"EstimatedDaysRemaining_StatusHint\\\":\\\"Normal\\\",\\\"UserComment\\\":\\\"\\\",\\\"ActionMessage\\\":\\\"Approve, Review or Reject this task / request.\\\",\\\"ActionedTime\\\":null,\\\"ActionedByUser\\\":null,\\\"CreateTime\\\":\\\"2025-11-28T06:14:02.6165763\\\",\\\"Actions\\\":[{\\\"ActionURL\\\":\\\"~/api/Workflow/ProcessResponse\\\",\\\"ActionLabel\\\":\\\"Approve\\\",\\\"ActionParameters\\\":\\\"{\\\\\\\"sid\\\\\\\":49,\\\\\\\"act\\\\\\\":\\\\\\\"1\\\\\\\",\\\\\\\"msg\\\\\\\":null}\\\",\\\"ActionUIHint\\\":\\\"Primary\\\"},{\\\"ActionURL\\\":\\\"~/api/Workflow/ProcessResponse\\\",\\\"ActionLabel\\\":\\\"Review\\\",\\\"ActionParameters\\\":\\\"{\\\\\\\"sid\\\\\\\":49,\\\\\\\"act\\\\\\\":\\\\\\\"2\\\\\\\",\\\\\\\"msg\\\\\\\":null}\\\",\\\"ActionUIHint\\\":\\\"Secondary\\\"},{\\\"ActionURL\\\":\\\"~/api/Workflow/ProcessResponse\\\",\\\"ActionLabel\\\":\\\"Reject\\\",\\\"ActionParameters\\\":\\\"{\\\\\\\"sid\\\\\\\":49,\\\\\\\"act\\\\\\\":\\\\\\\"3\\\\\\\",\\\\\\\"msg\\\\\\\":null}\\\",\\\"ActionUIHint\\\":\\\"Tertiary\\\"}]}]}\"\r\n}";//new StreamReader(Request.InputStream).ReadToEnd();
            string jsonBody = new StreamReader(Request.InputStream).ReadToEnd();
            // Parse JSON manually
            JObject json = JObject.Parse(jsonBody);
            int workflowHeaderID = (int)json["workflowHeaderID"];            
            var workflowDataToken = json["workflowData"];
            JObject workflowData;

            // If it's a JSON string → parse it
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
                workflowData = null; // default
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
