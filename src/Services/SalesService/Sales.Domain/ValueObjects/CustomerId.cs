namespace Sales.Domain.ValueObjects;

public sealed record CustomerId
{
    public string Value { get; }

    private CustomerId(string value)
    {
        Value = value;
    }

    public static CustomerId Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CustomerId cannot be empty", nameof(value));

        if (value.Length > 200)
            throw new ArgumentException("CustomerId cannot exceed 200 characters", nameof(value));

        return new CustomerId(value.Trim());
    }

    public static implicit operator string(CustomerId customerId) => customerId.Value;

    public override string ToString() => Value;
}
