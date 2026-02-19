
namespace CRM.Business.Workflows.StepTypesConfigObject
{
    public class APICall : StepType_ConfigObjectBase
    {
        public APICall() 
        {
            OnErrorContinueToNextStep = false;
            APIURL = "";
            AuthToken = "";
            Headers = new string[] { };
            FormFieldData = new string[] { };
        }

        public bool OnErrorContinueToNextStep { get; set; }
        public string APIURL { get; set; }
        public string AuthToken { get; set; }
        public string[] Headers { get; set; }
        public string[] FormFieldData { get; set; }
    }
}
