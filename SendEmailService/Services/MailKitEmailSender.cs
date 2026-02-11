using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using SendEmailService.Configurations;
using SendEmailService.Models;

namespace SendEmailService.Services;

public sealed class MailKitEmailSender : IEmailSender
{
    private readonly IOptionsMonitor<SmtpOptions> _smtpOptions;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(IOptionsMonitor<SmtpOptions> smtpOptions, ILogger<MailKitEmailSender> logger)
    {
        _smtpOptions = smtpOptions;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var opt = _smtpOptions.CurrentValue;

        if (string.IsNullOrWhiteSpace(opt.Host))
            throw new InvalidOperationException("SMTP host is empty. Set Smtp__Host in .env or appsettings.");
        if (string.IsNullOrWhiteSpace(opt.FromAddress))
            throw new InvalidOperationException("SMTP FromAddress is empty. Set Smtp__FromAddress in .env or appsettings.");
        if (string.IsNullOrWhiteSpace(message.To))
            throw new ArgumentException("EmailMessage.To is empty.");

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(opt.FromName ?? "", opt.FromAddress));
        email.To.Add(MailboxAddress.Parse(message.To));
        email.Subject = message.Subject ?? "";

        email.Body = new TextPart(TextFormat.Html)
        {
            Text = message.Body ?? ""
        };

        var secureSocketOptions = opt.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;

        using var client = new SmtpClient();

        // Avoid some corporate SMTP servers failing on OAuth2 mechanisms
        client.AuthenticationMechanisms.Remove("XOAUTH2");

        await client.ConnectAsync(opt.Host, opt.Port, secureSocketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(opt.UserName))
        {
            await client.AuthenticateAsync(opt.UserName, opt.Password ?? "", cancellationToken);
        }

        await client.SendAsync(email, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("Sent email to {To} (Subject: {Subject})", message.To, message.Subject);
    }
}

