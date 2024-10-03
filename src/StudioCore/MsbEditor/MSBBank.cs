using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StudioCore.ParamEditor;

/// <summary>
///     Utilities for dealing with global paramdefs for a game
/// </summary>
public class MSBBank : StudioResource
{

    /// <summary>
    ///     List of msb names
    /// </summary>
    private List<string> _msbIDs = new();

    /// <summary>
    ///     Mapping from msb name -> IMsb.
    /// </summary>
    private Dictionary<string, IMsb> _msbs = new();
    
    public MSBBank() : base(Locator.ActiveProject.Type, "MSBs")
    {
    }

    protected override void Load()
    {
    }

    protected override IEnumerable<StudioResource> GetDependencies(Project project)
    {
        return [];
    }
}
