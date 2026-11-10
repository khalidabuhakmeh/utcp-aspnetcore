using JetBrains.Annotations;
using Utcp.AspNetCore.Models;

namespace Utcp.AspNetCore;

/// <summary>Post-processing wrapper around the final <see cref="UtcpManual" />, passed to Configure hooks.</summary>
[PublicAPI]
public sealed class UtcpManualContext(UtcpManual manual, IServiceProvider services)
{
    /// <summary>The manual being built; may be modified directly.</summary>
    public UtcpManual Manual { get; } = manual;

    /// <summary>The request-scoped service provider.</summary>
    public IServiceProvider Services { get; } = services;

    /// <summary>Adds a tool.</summary>
    /// <exception cref="InvalidOperationException">A tool with the same name already exists.</exception>
    public void Add(UtcpTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (Manual.Tools.Any(t => t.Name == tool.Name))
        {
            throw new InvalidOperationException($"Duplicate UTCP tool name '{tool.Name}'.");
        }

        Manual.Tools.Add(tool);
    }

    /// <summary>Removes the tool with the given name.</summary>
    /// <returns><see langword="true" /> if a tool was removed.</returns>
    public bool Remove(string toolName)
    {
        return Manual.Tools.RemoveAll(t => t.Name == toolName) > 0;
    }

    /// <summary>Replaces the existing tool that has the same name as <paramref name="tool" />.</summary>
    /// <exception cref="InvalidOperationException">No tool with that name exists.</exception>
    public void Replace(UtcpTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        var i = Manual.Tools.FindIndex(t => t.Name == tool.Name);
        if (i < 0)
        {
            throw new InvalidOperationException($"Cannot replace UTCP tool '{tool.Name}': no tool with that name exists.");
        }

        Manual.Tools[i] = tool;
    }
}
