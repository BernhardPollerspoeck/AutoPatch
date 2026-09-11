namespace Autopatch.Client.Tests.Infrastructure;

public sealed class Order
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Address? Address { get; set; }

    public int? NullableInt { get; set; }

    public double? NullableDouble { get; set; }

    public bool? NullableBool { get; set; }

    public DateTime? NullableDateTime { get; set; }

    public decimal? NullableDecimal { get; set; }

    public static Order Create(int id, string name) => new()
    {
        Id = id,
        Name = name,
        Address = new Address { Street = "Main Street 1" },
        NullableInt = 1,
        NullableDouble = 1.5,
        NullableBool = true,
        NullableDateTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        NullableDecimal = 2.5m,
    };
}

public sealed class Address
{
    public string Street { get; set; } = string.Empty;
}
