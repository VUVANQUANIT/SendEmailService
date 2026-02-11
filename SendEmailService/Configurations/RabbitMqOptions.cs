namespace SendEmailService.Configurations;

public sealed class RabbitMqOptions
{
    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";

    public string QueueName { get; init; } = "email_queue";

    /// <summary>
    /// How many unacked messages per consumer.
    /// </summary>
    public ushort PrefetchCount { get; init; } = 10;

    /// <summary>
    /// Optional connection name shown in RabbitMQ Management UI.
    /// </summary>
    public string? ClientProvidedName { get; init; } = "SendEmailService";
}

