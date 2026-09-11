namespace Autopatch.Server.Tests.Infrastructure.Collision;

/// <summary>
/// A plausible domain model whose simple name collides with <see cref="System.Threading.Monitor"/>.
/// </summary>
public sealed class Monitor : ObservableModel
{
    private string _name = string.Empty;

    public int Id { get; set; }

    public string Name { get => _name; set => Set(ref _name, value); }
}
