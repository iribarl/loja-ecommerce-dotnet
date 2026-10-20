using System.Text.Json;
using Loja.SharedKernel;

namespace Loja.Outbox;

internal static class IntegrationEventSerializer
{
    public static string TypeName(IntegrationEvent integrationEvent)
    {
        var type = integrationEvent.GetType();
        return $"{type.FullName}, {type.Assembly.GetName().Name}";
    }

    public static string Serialize(IntegrationEvent integrationEvent) =>
        JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType());

    /// <summary>
    /// Rebuilds an event from the outbox. Only integration event types are accepted: the type name
    /// comes from the database, and instantiating arbitrary types from stored names is a classic hole.
    /// </summary>
    public static IntegrationEvent Deserialize(string typeName, string content)
    {
        var type = Type.GetType(typeName, throwOnError: false)
            ?? throw new InvalidOperationException($"Unknown integration event type '{typeName}'.");

        if (!typeof(IntegrationEvent).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"'{typeName}' is not an integration event.");
        }

        return (IntegrationEvent)(JsonSerializer.Deserialize(content, type)
            ?? throw new InvalidOperationException($"Empty content for '{typeName}'."));
    }
}
