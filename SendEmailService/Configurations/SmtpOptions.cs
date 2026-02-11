namespace SendEmailService.Configurations;

public sealed class SmtpOptions
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 587;

    /// <summary>
    /// If true: SSL-on-connect. If false: STARTTLS when available (MailKit SecureSocketOptions.Auto).
    /// </summary>
    public bool UseSsl { get; init; } = false;

    public string UserName { get; init; } = "";
    public string Password { get; init; } = "";

    public string FromAddress { get; init; } = "";
    public string? FromName { get; init; } = "SendEmailService";
}

