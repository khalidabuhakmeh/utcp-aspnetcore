namespace Utcp.AspNetCore.Models;

/// <summary>
/// Base type for UTCP authentication blocks. The concrete type determines the <c>auth_type</c> discriminator.
/// Values inside auth blocks are <c>${PLACEHOLDER}</c> variables that the consuming agent substitutes at call
/// time; this library never emits real secrets.
/// </summary>
public abstract class UtcpAuth
{
    /// <summary>JSON <c>auth_type</c>: discriminator telling the agent which auth scheme this block describes.</summary>
    public abstract string AuthType { get; }
}
