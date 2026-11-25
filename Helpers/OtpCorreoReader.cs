using System.Net;
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Playwright.Finexus.Api;

namespace Playwright.Finexus.Helpers;

public static class OtpCorreoReader
{
    private static readonly Regex OtpRegex = new(@"firmar\s+los\s+documentos\s+es:\s*(\d{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);

    private static async Task<ImapClient> ConectarAsync(CancellationToken ct)
    {
        var settings = TestSettings.Mail;
        if (string.IsNullOrWhiteSpace(settings.Password))
            throw new InvalidOperationException("Mail:Password no configurado (User Secrets o variable Mail__Password).");

        var client = new ImapClient();
        await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.SslOnConnect, ct);
        await client.AuthenticateAsync(settings.User, settings.Password, ct);
        return client;
    }

    /// <summary>Mueve a la papelera los OTP previos y devuelve el UID máximo actual del Inbox (línea base).</summary>
    public static async Task<uint> ObtenerLineaBaseAsync(CancellationToken ct = default)
    {
        var settings = TestSettings.Mail;
        using var client = await ConectarAsync(ct);
        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, ct);

        var antiguos = await inbox.SearchAsync(SearchQuery.SubjectContains(settings.Subject), ct);
        if (antiguos.Count > 0)
        {
            var trash = client.GetFolder(SpecialFolder.Trash);
            await inbox.MoveToAsync(antiguos, trash, ct);
        }

        var todos = await inbox.SearchAsync(SearchQuery.All, ct);
        var baseUid = todos.Count == 0 ? 0u : todos.Max(u => u.Id);
        await inbox.CloseAsync(false, ct);
        await client.DisconnectAsync(true, ct);
        return baseUid;
    }

    public static async Task<string> ObtenerOtpAsync(uint uidBase, string? destinatario = null, CancellationToken ct = default)
    {
        var settings = TestSettings.Mail;
        using var client = await ConectarAsync(ct);

        for (var attempt = 1; attempt <= settings.MaxAttempts; attempt++)
        {
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, ct);
            SearchQuery query = SearchQuery.SubjectContains(settings.Subject);
            if (!string.IsNullOrWhiteSpace(destinatario)) query = query.And(SearchQuery.ToContains(destinatario));

            var uids = await inbox.SearchAsync(query, ct);
            foreach (var uid in uids.Where(u => u.Id > uidBase).OrderByDescending(u => u.Id))
            {
                var msg = await inbox.GetMessageAsync(uid, ct);
                var body = msg.HtmlBody ?? msg.TextBody ?? string.Empty;
                var text = WebUtility.HtmlDecode(TagRegex.Replace(body, " "));
                var match = OtpRegex.Match(text);
                if (match.Success) return match.Groups[1].Value;
            }

            await inbox.CloseAsync(false, ct);
            await Task.Delay(TimeSpan.FromSeconds(settings.DelaySeconds), ct);
        }

        throw new TimeoutException($"No llegó el correo '{settings.Subject}' en {settings.MaxAttempts * settings.DelaySeconds} s.");
    }
}
