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
            var errorQueueName = Environment.GetEnvironmentVariable("ErrorQueueName");

            if (string.IsNullOrEmpty(inputQueueName))
            {
                throw new InvalidOperationException("InputQueueName is not configured.");
            }

            if (string.IsNullOrEmpty(outputQueueName))
            {
                throw new InvalidOperationException("OutputQueueName is not configured.");
            }

            if (string.IsNullOrEmpty(errorQueueName))
            {
                throw new InvalidOperationException("ErrorQueueName is not configured.");
            }

            return new QueueConfiguration(inputQueueName, outputQueueName, errorQueueName);
        });
    })
    .Build();

await host.RunAsync();

// Helper class for queue configuration
public record QueueConfiguration(string InputQueueName, string OutputQueueName, string ErrorQueueName);
