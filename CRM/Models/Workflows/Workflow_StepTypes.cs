using CRM.Business.Workflows.StepTypesConfigObject;
using Newtonsoft.Json;
using System;
using ThirdParty.Json.LitJson;

namespace CRM.Models.Workflows
{
    public partial class Workflow_StepTypes
    {
        public enum StepType : byte
        {
            ApproveReviewReject = 1,
            SimpleStepCompletion = 2,
            APICall = 3,
            TriggerWorkflow = 4,
            TimedStepCompletion = 5
        }

        public StepType_ConfigObjectBase ParseConfigData(string jsonData)
        {
            StepType_ConfigObjectBase stepType_ConfigObjectBase = null;

            if (ID == (byte)StepType.ApproveReviewReject)
            {
                stepType_ConfigObjectBase = JsonConvert.DeserializeObject<ApproveReviewReject>(jsonData);
            }
            else if (ID == (byte)StepType.SimpleStepCompletion)
            {
                stepType_ConfigObjectBase = JsonConvert.DeserializeObject<SimpleStepCompletion>(jsonData);
            }
            else if (ID == (byte)StepType.APICall)
            {
                stepType_ConfigObjectBase = JsonConvert.DeserializeObject<APICall>(jsonData);
            }
            else if (ID == (byte)StepType.TriggerWorkflow)
            {
                stepType_ConfigObjectBase = JsonConvert.DeserializeObject<TriggerWorkflow>(jsonData);
            }
            else if (ID == (byte)StepType.TimedStepCompletion)
            {
                stepType_ConfigObjectBase = JsonConvert.DeserializeObject<TimedStepCompletion>(jsonData);
            }
            else
            {
                throw new Exception("Add the new Workflow Step Type ID " + ID + " to Workflow_StepTypes::ParseConfigData() function.");
            }

            return stepType_ConfigObjectBase;
        }

        public string GetConfigDataTemplateJson()
        {
            string JsonTemplate = "";

            if (ID == (byte)StepType.ApproveReviewReject)
            {
                JsonTemplate = JsonConvert.SerializeObject(new ApproveReviewReject());
            }
            else if (ID == (byte)StepType.SimpleStepCompletion)
            {
                JsonTemplate = JsonConvert.SerializeObject(new SimpleStepCompletion());
            }
            else if (ID == (byte)StepType.APICall)
            {
                JsonTemplate = JsonConvert.SerializeObject(new APICall());
            }
            else if (ID == (byte)StepType.TriggerWorkflow)
            {
                JsonTemplate = JsonConvert.SerializeObject(new TriggerWorkflow());
            }
            else if (ID == (byte)StepType.TimedStepCompletion)
            {
                JsonTemplate = JsonConvert.SerializeObject(new TimedStepCompletion());
            }
            else
            {
                throw new Exception("Add the new Workflow Step Type ID " + ID + " to Workflow_StepTypes::UpdateConfigDataTemplateField() function.");
            }

            return JsonTemplate;
        }

        public void UpdateConfigDataTemplateField()
        {
            Workflow_StepTypes_ConfigDataTemplate = GetConfigDataTemplateJson();
        }
    }
}
