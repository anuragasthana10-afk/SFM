
namespace CRM.Business.Workflows.PostStepCompletionTaskTypes
{
    public class APICall
    {
        public APICall()
        {
            APICallName = "";
            APIURL = "";
            AuthToken = "";
            Headers = new string[] { };
            FormFieldData = new string[] { };
        }

        public string APICallName { get; set; }
        public string APIURL { get; set; }
        public string AuthToken { get; set; }
        public string[] Headers { get; set; }
        public string[] FormFieldData { get; set; }
    }
}
