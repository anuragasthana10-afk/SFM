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
            builder.AppendLine("<table style=\"border-collapse: collapse; width: 100%;\">");
            builder.AppendLine("  <thead>");
            builder.AppendLine("    <tr>");
            builder.AppendLine("      <th style=\"text-align: center; vertical-align: middle; border: 1px solid #e6e6e6; padding: 6px;\">Location</th>");
            builder.AppendLine("      <th style=\"text-align: center; vertical-align: middle; border: 1px solid #e6e6e6; padding: 6px;\">Entity</th>");
            builder.AppendLine("      <th style=\"text-align: center; vertical-align: middle; border: 1px solid #e6e6e6; padding: 6px;\">Code</th>");
            builder.AppendLine("      <th style=\"text-align: center; vertical-align: middle; border: 1px solid #e6e6e6; padding: 6px;\">Status</th>");
            builder.AppendLine("      <th style=\"text-align: center; vertical-align: middle; border: 1px solid #e6e6e6; padding: 6px;\">Error</th>");
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
                var location = ResolveLocationLabel(operation.Location);

                builder.AppendLine("    <tr>");
                builder.AppendLine($"      <td style=\"text-align: left; vertical-align: middle; border: 1px solid #e6e6e6; padding: 6px;\">{WebUtility.HtmlEncode(location)}</td>");
                builder.AppendLine($"      <td style=\"text-align: left; vertical-align: middle; border: 1px solid #e6e6e6; padding: 6px;\">{WebUtility.HtmlEncode(operation.EntityType)}</td>");
                builder.AppendLine($"      <td style=\"text-align: left; vertical-align: middle; border: 1px solid #e6e6e6; padding: 6px;\">{WebUtility.HtmlEncode(code)}</td>");
                builder.AppendLine($"      <td style=\"text-align: center; vertical-align: middle; border: 1px solid #e6e6e6; padding: 6px; color: {statusColor};\">{WebUtility.HtmlEncode(status)}</td>");
                builder.AppendLine($"      <td style=\"text-align: left; vertical-align: middle; border: 1px solid #e6e6e6; padding: 6px;{(string.IsNullOrEmpty(errorColor) ? string.Empty : $" color: {errorColor};")}\">{WebUtility.HtmlEncode(error)}</td>");
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

        private static string ResolveLocationLabel(byte location)
        {
            if (Enum.IsDefined(typeof(ZohoBooksLocation), location))
            {
                return ((ZohoBooksLocation)location).ToString();
            }

            return location.ToString();
        }
    }
}
