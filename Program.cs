using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureFunctionsWebApplication() // Use Web Application for ASP.NET Core Integration
    .ConfigureServices(services =>
    {
        // Register ServiceBusClient with Dependency Injection
        services.AddSingleton(provider =>
        {
            var connectionString = Environment.GetEnvironmentVariable("ServiceBusConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("ServiceBusConnection is not configured.");
            }
            return new ServiceBusClient(connectionString);
        });

        // Add queue name configuration from environment variables
        services.AddSingleton(provider =>
        {
            var inputQueueName = Environment.GetEnvironmentVariable("InputQueueName");
            var outputQueueName = Environment.GetEnvironmentVariable("OutputQueueName");

            if (string.IsNullOrEmpty(inputQueueName) || string.IsNullOrEmpty(outputQueueName))
            {
                throw new InvalidOperationException("InputQueueName or OutputQueueName is not configured.");
            }

            return new QueueConfiguration(inputQueueName, outputQueueName);
        });
    })
    .Build();

await host.RunAsync();

// Helper class for queue configuration
public record QueueConfiguration(string InputQueueName, string OutputQueueName);
