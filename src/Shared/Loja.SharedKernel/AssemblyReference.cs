using System.Reflection;

namespace Loja.SharedKernel;

/// <summary>
/// Gives tests and DI registration a stable way to reach this assembly.
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
