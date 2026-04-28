using Simple_QR_Code_Maker.Models;
using System.Text;

namespace Simple_QR_Code_Maker.Helpers;

public static class EmailBuilderHelper
{
    private const string MailtoPrefix = "mailto:";

    public static bool IsEmail(string? text) => TryParse(text, out _);

    public static bool TryParse(string? text, out EmailBuilderState state)
    {
        state = new EmailBuilderState();

        if (string.IsNullOrWhiteSpace(text))
            return false;

        string trimmedText = text.Trim();
        if (!trimmedText.StartsWith(MailtoPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string rest = trimmedText[MailtoPrefix.Length..];

        int queryStart = rest.IndexOf('?');
        string toField = queryStart >= 0 ? rest[..queryStart] : rest;
        string queryString = queryStart >= 0 ? rest[(queryStart + 1)..] : string.Empty;

        state.To = Uri.UnescapeDataString(toField.Trim());

        foreach (string part in queryString.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eqIndex = part.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            string key = part[..eqIndex];
            string value = Uri.UnescapeDataString(part[(eqIndex + 1)..]);

            switch (key.ToLowerInvariant())
            {
                case "cc": state.Cc = value; break;
                case "bcc": state.Bcc = value; break;
                case "subject": state.Subject = value; break;
                case "body": state.Body = value; break;
            }
        }

        return !string.IsNullOrWhiteSpace(state.To);
    }

    public static string Serialize(EmailBuilderState state)
    {
        if (string.IsNullOrWhiteSpace(state.To))
            return string.Empty;

        StringBuilder builder = new();
        builder.Append(MailtoPrefix);
        builder.Append(state.To.Trim());

        List<string> queryParts = [];

        if (!string.IsNullOrWhiteSpace(state.Cc))
            queryParts.Add("cc=" + Uri.EscapeDataString(state.Cc.Trim()));
        if (!string.IsNullOrWhiteSpace(state.Bcc))
            queryParts.Add("bcc=" + Uri.EscapeDataString(state.Bcc.Trim()));
        if (!string.IsNullOrWhiteSpace(state.Subject))
            queryParts.Add("subject=" + Uri.EscapeDataString(state.Subject.Trim()));
        if (!string.IsNullOrWhiteSpace(state.Body))
            queryParts.Add("body=" + Uri.EscapeDataString(state.Body));

        if (queryParts.Count > 0)
        {
            builder.Append('?');
            builder.Append(string.Join('&', queryParts));
        }

        return builder.ToString();
    }

    public static string GetDisplayName(string? text)
    {
        return TryParse(text, out EmailBuilderState state)
            ? GetDisplayName(state)
            : "Email";
    }

    public static string GetDisplayName(EmailBuilderState state)
    {
        if (!string.IsNullOrWhiteSpace(state.Subject))
            return state.Subject.Trim();

        string to = state.To.Trim();
        return string.IsNullOrWhiteSpace(to) ? "Email" : to;
    }
}
