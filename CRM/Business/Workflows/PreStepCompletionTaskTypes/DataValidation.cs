

using System;

namespace CRM.Business.Workflows.PreStepCompletionTaskTypes
{
    public class DataValidation
    {
        public DataValidation() 
        {
            FieldName = "";
            FieldValue = "";
            LogicalOperator = LogicalOperators.Equals.ToString();
            ValidationMessage = FieldName + " should have value: " + FieldValue;
        }

        public enum LogicalOperators
        {
            Equals,
            NotEquals,
            GreaterThan,
            GreaterThanOrEqual,
            LessThan,
            LessThanOrEqual,
        }

        public string FieldName { get; set; }
        public string LogicalOperator { get; set; }
        public string FieldValue { get; set; }
        public string ValidationMessage { get; set; }

        public bool CompareValues(object ContextObjectModel_FieldName_Value, Type FieldDataType)
        {
            bool bResult = false;

            ObjectCustomComparison _leftOperand = new ObjectCustomComparison(ContextObjectModel_FieldName_Value);
            ObjectCustomComparison _rightOperand = new ObjectCustomComparison(ObjectCustomComparison.ConvertType(FieldValue, FieldDataType));

            if (LogicalOperator.Equals(LogicalOperators.GreaterThan.ToString(), System.StringComparison.OrdinalIgnoreCase))
            {
                bResult = _leftOperand > _rightOperand;
            }
            else if (LogicalOperator.Equals(LogicalOperators.GreaterThanOrEqual.ToString(), System.StringComparison.OrdinalIgnoreCase))
            {
                bResult = _leftOperand >= _rightOperand;
            }
            else if (LogicalOperator.Equals(LogicalOperators.LessThan.ToString(), System.StringComparison.OrdinalIgnoreCase))
            {
                bResult = _leftOperand < _rightOperand;
            }
            else if (LogicalOperator.Equals(LogicalOperators.LessThanOrEqual.ToString(), System.StringComparison.OrdinalIgnoreCase))
            {
                bResult = _leftOperand <= _rightOperand;
            }
            else if (LogicalOperator.Equals(LogicalOperators.NotEquals.ToString(), System.StringComparison.OrdinalIgnoreCase))
            {
                bResult = _leftOperand != _rightOperand;
            }
            else //if (LogicalOperator.Equals(LogicalOperators.Equals.ToString(), System.StringComparison.OrdinalIgnoreCase))
            {
                bResult = _leftOperand == _rightOperand;
            }

            return bResult;
        }
    }
}
