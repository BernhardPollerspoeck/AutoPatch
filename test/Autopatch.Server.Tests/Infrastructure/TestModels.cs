using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Autopatch.Server.Services;

namespace Autopatch.Server.Tests.Infrastructure;

public abstract class ObservableModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<TValue>(ref TValue field, TValue value, [CallerMemberName] string? propertyName = null)
    {
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class TestItem : ObservableModel
{
    private string _name = string.Empty;
    private int _value;
    private string? _secret;
    private TestPosition? _position;

    public int Id { get; set; }

    public string Name { get => _name; set => Set(ref _name, value); }

    public int Value { get => _value; set => Set(ref _value, value); }

    public string? Secret { get => _secret; set => Set(ref _secret, value); }

    public TestPosition? Position { get => _position; set => Set(ref _position, value); }

    public static TestItem Create(int id, string? name = null) => new() { Id = id, Name = name ?? $"item-{id}" };

    public override string ToString() => $"#{Id} {Name}";
}

public sealed class TestPosition
{
    public double X { get; set; }

    public double Y { get; set; }
}

/// <summary>Collection type that is protected by a validator in the tests.</summary>
public sealed class SecureItem : ObservableModel
{
    private string _name = string.Empty;

    public int Id { get; set; }

    public string Name { get => _name; set => Set(ref _name, value); }
}

public sealed class RejectAllValidator<T> : ICollectionSubscriptionValidator<T> where T : class
{
    public Task<bool> ValidateSubscriptionAsync(ClaimsPrincipal? user, string? authString, string collectionKey)
        => Task.FromResult(false);
}

public sealed class AllowAllValidator<T> : ICollectionSubscriptionValidator<T> where T : class
{
    public Task<bool> ValidateSubscriptionAsync(ClaimsPrincipal? user, string? authString, string collectionKey)
        => Task.FromResult(true);
}

/// <summary>Accepts only the auth string "valid".</summary>
public sealed class AuthStringValidator<T> : ICollectionSubscriptionValidator<T> where T : class
{
    public Task<bool> ValidateSubscriptionAsync(ClaimsPrincipal? user, string? authString, string collectionKey)
        => Task.FromResult(authString == "valid");
}

public sealed class AuthenticatedUserValidator<T> : ICollectionSubscriptionValidator<T> where T : class
{
    public Task<bool> ValidateSubscriptionAsync(ClaimsPrincipal? user, string? authString, string collectionKey)
        => Task.FromResult(user?.Identity?.IsAuthenticated == true);
}
