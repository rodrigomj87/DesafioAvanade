namespace Inventory.Domain.Entities;

public sealed class ProcessedMessage
{
    private ProcessedMessage()
    {
        MessageId = string.Empty;
    }

    public ProcessedMessage(string messageId)
    {
        MessageId = messageId ?? throw new ArgumentNullException(nameof(messageId));
        ProcessedAt = DateTime.UtcNow;
    }

    public string MessageId { get; private set; }
    public DateTime ProcessedAt { get; private set; }
}
