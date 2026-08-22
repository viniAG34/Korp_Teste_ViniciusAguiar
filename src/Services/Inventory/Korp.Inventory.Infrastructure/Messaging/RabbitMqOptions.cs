using System.ComponentModel.DataAnnotations;

namespace Korp.Inventory.Infrastructure.Messaging;

public sealed class RabbitMqOptions : IValidatableObject
{
    public const string SectionName = "Messaging:RabbitMq";

    public bool Enabled { get; init; }
    public string Host { get; init; } = string.Empty;
    [Range(1, 65535)] public int Port { get; init; } = 5672;
    public string VirtualHost { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    [Range(1, 300)] public int RequestedHeartbeatSeconds { get; init; } = 30;
    [Range(1, 300)] public int NetworkRecoveryIntervalSeconds { get; init; } = 5;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled) yield break;
        if (string.IsNullOrWhiteSpace(Host)) yield return Required(nameof(Host));
        if (string.IsNullOrWhiteSpace(VirtualHost)) yield return Required(nameof(VirtualHost));
        if (string.IsNullOrWhiteSpace(Username)) yield return Required(nameof(Username));
        if (string.IsNullOrWhiteSpace(Password)) yield return Required(nameof(Password));
    }

    private static ValidationResult Required(string member) =>
        new($"Messaging option {member} is required.", [member]);
}
