using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Business.Workflows.PreStepCompletionTaskTypes
{
    public class DataValidations
    {
        public DataValidations() 
        {
            DataValidationList = new DataValidation[] { };
        }

        public DataValidation[] DataValidationList { get; set; }
    }
}
