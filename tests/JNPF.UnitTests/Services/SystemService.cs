using JNPF.DependencyInjection;

namespace JNPF.UnitTests;

public class SystemService : ISystemService, ITransient
{
    public string GetName() => "JNPF";
}