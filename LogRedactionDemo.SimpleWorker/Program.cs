using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;

var builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddHostedService<Worker>()
    .AddLogging(lb =>
    {
        lb.EnableRedaction();

        // Log structured logs as JSON to console so we can see the actual structured data
        lb.AddJsonConsole(o => o.JsonWriterOptions = new JsonWriterOptions { Indented = true });

        // AddRedaction make sure the redactor provider is hooked up so that the logger can get a redactor
        // bey default the ErasingRedactor is added as the fallback redactor which erases all data marked with any
        // DataClassificationAttribute
        // lb.Services.AddRedaction();

        // This is how you can configure redactors in more detail
        lb.Services.AddRedaction(rb =>
            rb.SetRedactor<ErasingRedactor>(
                    new DataClassificationSet(new DataClassification("MyTaxonomy", "MyClassification")))
                .SetFallbackRedactor<NullRedactor>());
    });

var host = builder.Build();
host.Run();

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                // Redacts the Name and Email properties of the User object, but not the InnerData property's RedactedData property, unless Transitive = true is set
                _logger.UserLoggedIn(new User("abcd", "Charles", "charles.mingus@bluenote.com", new InnerUserData()));
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}

public record User
{
    public User(string Id, [PersonalData] string Name, [PersonalData] string Email, InnerUserData innerData)
    {
        this.Id = Id;
        this.Name = Name;
        this.Email = Email;
        this.InnerData = innerData;
    }

    public string Id { get; }

    [PersonalData] 
    public string Name { get; }

    [PersonalData] 
    public string Email { get; }

    public InnerUserData InnerData { get; }
}

public record InnerUserData
{
    [PersonalData]
    public string RedactedData { get; } = Guid.NewGuid().ToString();
    public string PublicData { get; } = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture);
}

// logging code that logs the user
public static partial class Log
{
    [LoggerMessage(LogLevel.Information, "User logged in")]
#pragma warning disable EXTEXP0003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public static partial void UserLoggedIn(this ILogger logger, [LogProperties(Transitive = true)] User user);
#pragma warning restore EXTEXP0003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
}

public class PersonalDataAttribute : DataClassificationAttribute
{
    // both of those strings are arbitrary identifiers you can pick.
    // You would use them later when configuring redaction to set the policies for your different named classifications.
    public PersonalDataAttribute() : base(new DataClassification("MyTaxonomy", "MyClassification"))
    {
    }
}