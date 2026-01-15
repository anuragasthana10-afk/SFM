using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using CRM.Classes.ExternalServices.Zoho.Books.Persistence;

namespace CRM.Classes.ExternalServices.Zoho.Books.Reporting
{
    public static class SyncOperationReportBuilder
    {
        public static string BuildHtmlTable(
            IReadOnlyList<SyncOperationRecord> operations,
            Func<string, IReadOnlyCollection<int>, IDictionary<int, string>> resolveUserCodes)
        {
            var builder = new StringBuilder();
            builder.AppendLine("<table>");
            builder.AppendLine("  <thead>");
            builder.AppendLine("    <tr>");
            builder.AppendLine("      <th>Entity</th>");
            builder.AppendLine("      <th>Code</th>");
            builder.AppendLine("      <th>Status</th>");
            builder.AppendLine("      <th>Error</th>");
            builder.AppendLine("    </tr>");
            builder.AppendLine("  </thead>");
            builder.AppendLine("  <tbody>");

            var codeLookup = new Dictionary<string, IDictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var operationGroup in operations.GroupBy(operation => operation.EntityType))
            {
                var missingCodes = operationGroup
                    .Where(operation => string.IsNullOrWhiteSpace(operation.LocalKeyText))
                    .Select(operation => operation.LocalKey)
                    .Distinct()
                    .ToList();

                codeLookup[operationGroup.Key] = missingCodes.Count == 0
                    ? new Dictionary<int, string>()
                    : resolveUserCodes(operationGroup.Key, missingCodes);
            }

            foreach (var operation in operations)
            {
                var code = string.IsNullOrWhiteSpace(operation.LocalKeyText)
                    ? ResolveMissingCode(operation, codeLookup)
                    : operation.LocalKeyText;
                var status = operation.Success ? "Success" : "Failure";
                var statusColor = operation.Success ? "green" : "red";
                var errorColor = operation.Success ? string.Empty : "red";
                var error = operation.Success ? string.Empty : operation.ErrorMessage ?? string.Empty;

                builder.AppendLine("    <tr>");
                builder.AppendLine($"      <td>{WebUtility.HtmlEncode(operation.EntityType)}</td>");
                builder.AppendLine($"      <td>{WebUtility.HtmlEncode(code)}</td>");
                builder.AppendLine($"      <td style=\"color: {statusColor};\">{WebUtility.HtmlEncode(status)}</td>");
                builder.AppendLine($"      <td{(string.IsNullOrEmpty(errorColor) ? string.Empty : $" style=\"color: {errorColor};\"")}>{WebUtility.HtmlEncode(error)}</td>");
                builder.AppendLine("    </tr>");
            }

            builder.AppendLine("  </tbody>");
            builder.AppendLine("</table>");
            return builder.ToString();
        }

        private static string ResolveMissingCode(
            SyncOperationRecord operation,
            IReadOnlyDictionary<string, IDictionary<int, string>> codeLookup)
        {
            if (codeLookup.TryGetValue(operation.EntityType, out var entityCodes)
                && entityCodes.TryGetValue(operation.LocalKey, out var resolvedCode)
                && !string.IsNullOrWhiteSpace(resolvedCode))
            {
                return resolvedCode;
            }

            return $"{operation.EntityType}-{operation.LocalKey}";
        }
    }
}
