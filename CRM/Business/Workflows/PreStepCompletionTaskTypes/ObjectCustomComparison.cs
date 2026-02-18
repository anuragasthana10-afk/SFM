

using System;
using Twilio.Rest.Api.V2010.Account.Usage.Record;

namespace CRM.Business.Workflows.PreStepCompletionTaskTypes
{
    public class ObjectCustomComparison
    {
        private object m_Operand;

        public ObjectCustomComparison(object operand) 
        {
            m_Operand = operand;
        }

        public static bool operator ==(ObjectCustomComparison leftOperand, ObjectCustomComparison rightOperand)
        {
            object _leftOperand = leftOperand.m_Operand, _rightOperand = rightOperand.m_Operand;

            if(leftOperand.m_Operand == null || rightOperand.m_Operand == null)
            {
                return leftOperand.m_Operand == rightOperand.m_Operand;
            }
            else if (leftOperand.m_Operand.GetType() == typeof(string))
            {
                return string.Equals((string)rightOperand.m_Operand, (string)rightOperand.m_Operand, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                return leftOperand.m_Operand == rightOperand.m_Operand;
            }
        }

        public static bool operator !=(ObjectCustomComparison leftOperand, ObjectCustomComparison rightOperand)
        {
            if (leftOperand.m_Operand == null || rightOperand.m_Operand == null)
            {
                return leftOperand.m_Operand != rightOperand.m_Operand;
            }
            else if (leftOperand.m_Operand.GetType() == typeof(string))
            {
                return !string.Equals((string)rightOperand.m_Operand, (string)rightOperand.m_Operand, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                return leftOperand.m_Operand != rightOperand.m_Operand;
            }
        }

        public static bool operator <=(ObjectCustomComparison leftOperand, ObjectCustomComparison rightOperand)
        {
            if (leftOperand.m_Operand == null || rightOperand.m_Operand == null)
            {
                return leftOperand <= rightOperand;
            }
            else if (leftOperand.m_Operand.GetType() == typeof(string))
            {
                throw new Exception("Cannot perform " + DataValidation.LogicalOperators.LessThanOrEqual.ToString() + " operation on text.");
            }
            else
            {
                return leftOperand <= rightOperand;
            }
        }

        public static bool operator >=(ObjectCustomComparison leftOperand, ObjectCustomComparison rightOperand)
        {
            if (leftOperand.m_Operand == null || rightOperand.m_Operand == null)
            {
                return leftOperand >= rightOperand;
            }
            else if (leftOperand.m_Operand.GetType() == typeof(string))
            {
                throw new Exception("Cannot perform " + DataValidation.LogicalOperators.GreaterThanOrEqual.ToString() + " operation on text.");
            }
            else
            {
                return leftOperand >= rightOperand;
            }
        }

        public static bool operator <(ObjectCustomComparison leftOperand, ObjectCustomComparison rightOperand)
        {
            if (leftOperand.m_Operand == null || rightOperand.m_Operand == null)
            {
                return leftOperand < rightOperand;
            }
            else if (leftOperand.m_Operand.GetType() == typeof(string))
            {
                throw new Exception("Cannot perform " + DataValidation.LogicalOperators.LessThan.ToString() + " operation on text.");
            }
            else
            {
                return leftOperand < rightOperand;
            }
        }

        public static bool operator >(ObjectCustomComparison leftOperand, ObjectCustomComparison rightOperand)
        {
            if (leftOperand.m_Operand == null || rightOperand.m_Operand == null)
            {
                return leftOperand > rightOperand;
            }
            else if (leftOperand.m_Operand.GetType() == typeof(string))
            {
                throw new Exception("Cannot perform " + DataValidation.LogicalOperators.GreaterThan.ToString() + " operation on text.");
            }
            else
            {
                return leftOperand > rightOperand;
            }
        }


        public override bool Equals(object obj)
        {
            return this == (obj as ObjectCustomComparison); // Reusing the == operator
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static object ConvertType(string sourceValue, Type FieldDataType)
        {
            if(sourceValue == null)
            {
                return null;
            }

            return Convert.ChangeType(sourceValue, FieldDataType);

            /*
            else if(FieldDataType == null)
            {
                return sourceValue;
            }
            else if (FieldDataType == typeof(DateTime))
            {
                return DateTime.Parse(sourceValue);
            }
            else if (FieldDataType == typeof(int))
            {
                return int.Parse(sourceValue);
            }
            else if (FieldDataType == typeof(double))
            {
                return double.Parse(sourceValue);
            }
            else if (FieldDataType == typeof(decimal))
            {
                return decimal.Parse(sourceValue);
            }
            return sourceValue;
            */

        }
    }
}
