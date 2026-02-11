using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SendEmailService.Configurations;
using SendEmailService.Models;
using SendEmailService.Services;

namespace SendEmailService.Workers;

public sealed class EmailWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<EmailWorker> _logger;
    private readonly RabbitMqOptions _rabbitOptions;
    private readonly IEmailSender _emailSender;

    private IConnection? _connection;
    private IChannel? _channel;
    private string? _consumerTag;

    public EmailWorker(
        ILogger<EmailWorker> logger,
        IOptions<RabbitMqOptions> rabbitOptions,
        IEmailSender emailSender)
    {
        _logger = logger;
        _rabbitOptions = rabbitOptions.Value;
        _emailSender = emailSender;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumerAsync(stoppingToken);

                // Keep the worker alive while consumer is running.
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmailWorker crashed. Retrying in 5 seconds...");
                await SafeShutdownAsync(CancellationToken.None);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        await SafeShutdownAsync(CancellationToken.None);
    }

    private async Task StartConsumerAsync(CancellationToken cancellationToken)
    {
        await SafeShutdownAsync(CancellationToken.None);

        var factory = new ConnectionFactory
        {
            HostName = _rabbitOptions.HostName,
            Port = _rabbitOptions.Port,
            UserName = _rabbitOptions.UserName,
            Password = _rabbitOptions.Password,
            VirtualHost = _rabbitOptions.VirtualHost,

            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            ConsumerDispatchConcurrency = 1
        };

        _logger.LogInformation(
            "Connecting RabbitMQ {Host}:{Port} vhost={VHost} queue={Queue}",
            _rabbitOptions.HostName,
            _rabbitOptions.Port,
            _rabbitOptions.VirtualHost,
            _rabbitOptions.QueueName);

        _connection = await factory.CreateConnectionAsync(_rabbitOptions.ClientProvidedName, cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _channel.QueueDeclareAsync(
            queue: _rabbitOptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: _rabbitOptions.PrefetchCount,
            global: false,
            cancellationToken: cancellationToken);

        var channel = _channel;
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            var json = Encoding.UTF8.GetString(ea.Body.Span);

            EmailMessage? emailData;
            try
            {
                emailData = JsonSerializer.Deserialize<EmailMessage>(json, JsonOptions);
                if (emailData is null)
                {
                    _logger.LogWarning("Received null/invalid JSON. Ack message to drop. Payload: {Json}", json);
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize JSON. Ack message to drop. Payload: {Json}", json);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
                return;
            }

            try
            {
                _logger.LogInformation("[RECEIVED] Send email to: {To}", emailData.To);

                await _emailSender.SendAsync(emailData, cancellationToken);

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Send email failed. Nack (requeue=true). To={To}", emailData.To);
                await channel.BasicNackAsync(
                    ea.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken: CancellationToken.None);
            }
        };

        _consumerTag = await channel.BasicConsumeAsync(
            queue: _rabbitOptions.QueueName,
            autoAck: false,
            consumerTag: string.Empty,
            noLocal: false,
            exclusive: false,
            arguments: null,
            consumer: consumer,
            cancellationToken: cancellationToken);

        _logger.LogInformation("RabbitMQ consumer started. consumerTag={ConsumerTag}", _consumerTag);
    }

    private async Task SafeShutdownAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_channel is not null && !string.IsNullOrWhiteSpace(_consumerTag))
            {
                try
                {
                    await _channel.BasicCancelAsync(_consumerTag, noWait: false, cancellationToken);
                }
                catch
                {
                    // ignore
                }
            }

            if (_channel is not null)
            {
                await _channel.DisposeAsync();
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            _channel = null;
            _consumerTag = null;
        }

        try
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            _connection = null;
        }
    }
}
