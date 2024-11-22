using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace wednesday;

public class ProcessXML
{
    private readonly ILogger<ProcessXML> _logger;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly QueueConfiguration _queueConfig;

    public ProcessXML(ILogger<ProcessXML> logger, ServiceBusClient serviceBusClient, QueueConfiguration queueConfig)
    {
        _logger = logger;
        _serviceBusClient = serviceBusClient;
        _queueConfig = queueConfig;
    }

    [Function("ProcessXML")]
    public async Task Run(
        [ServiceBusTrigger("%InputQueueName%", Connection = "ServiceBusConnection")] string inputXml,
        FunctionContext context)
    {
        _logger.LogInformation("Received XML message: {Message}", inputXml);

        try
        {
            // Step 1: Validate the input XML against the XSD schema
            ValidateXml(inputXml);

            // Step 2: Transform namespaces in the XML
            XElement transformedXml = TransformNamespaces(XElement.Parse(inputXml));

            // Step 3: Publish the transformed XML to the output queue
            await PublishTransformedXml(transformedXml);

            _logger.LogInformation("Successfully processed and published transformed XML.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing the XML message. Sending to error queue.");

            // Send the failed message to the error queue
            await SendToErrorQueue(inputXml, ex.Message);

            // Suppress the exception to avoid dead-lettering
        }
    }

    private void ValidateXml(string xml)
    {
        // Ensure the path to the XSD file is correct
        string schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "record.xsd");
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException($"The XSD schema file was not found at: {schemaPath}");
        }

        XmlSchemaSet schemas = new();
        schemas.Add("http://example.com/record", schemaPath);

        XmlDocument xmlDocument = new();
        xmlDocument.LoadXml(xml);
        xmlDocument.Schemas.Add(schemas);

        xmlDocument.Validate((sender, args) =>
        {
            if (args.Severity == XmlSeverityType.Error)
            {
                throw new InvalidOperationException($"XML validation error: {args.Message}");
            }
        });

        _logger.LogInformation("XML validated successfully.");
    }

    private XElement TransformNamespaces(XElement inputXml)
    {
        XNamespace newNamespace = "http://example.com/new-namespace";

        // Recursive method to update namespaces
        void UpdateNamespace(XElement element)
        {
            element.Name = newNamespace + element.Name.LocalName;
            foreach (var child in element.Elements())
            {
                UpdateNamespace(child);
            }
        }

        XElement transformedXml = new(inputXml);
        UpdateNamespace(transformedXml);

        _logger.LogInformation("Transformed XML: {TransformedXml}", transformedXml);
        return transformedXml;
    }

    private async Task PublishTransformedXml(XElement transformedXml)
    {
        string xmlString = transformedXml.ToString(SaveOptions.DisableFormatting);

        ServiceBusSender sender = _serviceBusClient.CreateSender(_queueConfig.OutputQueueName);
        ServiceBusMessage message = new(xmlString);

        await sender.SendMessageAsync(message);

        _logger.LogInformation("Published message to queue: {QueueName}", _queueConfig.OutputQueueName);
    }

    private async Task SendToErrorQueue(string inputXml, string errorMessage)
    {
        ServiceBusSender errorSender = _serviceBusClient.CreateSender(_queueConfig.ErrorQueueName);

        // Add metadata to the error message for debugging
        ServiceBusMessage errorMessageToSend = new()
        {
            Body = BinaryData.FromString(inputXml),
            ApplicationProperties =
            {
                { "ErrorMessage", errorMessage },
                { "Timestamp", DateTime.UtcNow.ToString("o") }
            }
        };

        await errorSender.SendMessageAsync(errorMessageToSend);

        _logger.LogInformation("Sent message to error queue: {QueueName}", _queueConfig.ErrorQueueName);
    }
}
