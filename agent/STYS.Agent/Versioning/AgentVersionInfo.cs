using System.Reflection;

namespace STYS.Agent.Versioning;

public static class AgentVersionInfo
{
    public static string Current => ResolveFromAssembly(typeof(AgentVersionInfo).Assembly);

    public static string ResolveFromAssembly(Assembly assembly)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
            return informationalVersion.Trim();

        return ResolveFromAssemblyVersion(assembly.GetName().Version);
    }

    public static string ResolveFromAssemblyVersion(Version? version)
    {
        if (version is null)
            return "unknown";

        var patch = version.Build >= 0 ? version.Build : 0;
        return $"{version.Major}.{version.Minor}.{patch}";
    }
}
