using DotNext;
using Microsoft.Extensions.Logging;
using SoapstoneLib.Proto.Internal;
using SoulsFormats;
using StudioCore.Editor;
using StudioCore.MsbEditor;
using StudioCore.Platform;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StudioCore;

/// <summary>
///     Class that stores a collection of game data sourced from a single project
/// </summary>
public abstract class DataBank : StudioResource
{
    public Project Project;

    public DataBank(Project project, string nameForUI) : base(project.Settings.GameType, nameForUI)
    {
        Project = project;
    }
    protected abstract void Save();
}
