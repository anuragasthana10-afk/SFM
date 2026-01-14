using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using CRM.Classes.ExternalServices.Zoho.Books.Persistence;

namespace CRM.Classes.ExternalServices.Zoho.Books.Reporting
{
    public static class SyncOperationReportBuilder
    {
        public static string BuildHtmlTable(
            IReadOnlyList<SyncOperationRecord> operations,
            Func<string, int, string, string> resolveUserCode)
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

            foreach (var operation in operations)
            {
                var code = resolveUserCode(operation.EntityType, operation.LocalKey, operation.LocalKeyText);
                var status = operation.Success ? "Success" : "Failure";
                var error = operation.Success ? string.Empty : operation.ErrorMessage ?? string.Empty;

                builder.AppendLine("    <tr>");
                builder.AppendLine($"      <td>{WebUtility.HtmlEncode(operation.EntityType)}</td>");
                builder.AppendLine($"      <td>{WebUtility.HtmlEncode(code)}</td>");
                builder.AppendLine($"      <td>{WebUtility.HtmlEncode(status)}</td>");
                builder.AppendLine($"      <td>{WebUtility.HtmlEncode(error)}</td>");
                builder.AppendLine("    </tr>");
            }

            builder.AppendLine("  </tbody>");
            builder.AppendLine("</table>");
            return builder.ToString();
        }
    }
}
