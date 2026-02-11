using SendEmailService.Configurations;
using SendEmailService.Services;
using SendEmailService.Workers;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services
    .AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection("RabbitMQ"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.QueueName), "RabbitMQ.QueueName is required");

builder.Services
    .AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection("Smtp"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "Smtp.Host is required")
    .Validate(o => !string.IsNullOrWhiteSpace(o.FromAddress), "Smtp.FromAddress is required");

builder.Services.AddSingleton<IEmailSender, MailKitEmailSender>();
builder.Services.AddHostedService<EmailWorker>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
