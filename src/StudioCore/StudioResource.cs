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
using System.Reflection;

namespace StudioCore;

/// <summary>
///     Class that stores a collection of mapstudio data independent of a given project
/// </summary>
public abstract class StudioResource
{
    public GameType GameType;
    public readonly string nameForUI;

    public StudioResource(GameType gameType, string name)
    {
        GameType = gameType;
        nameForUI = name;
    }

    public bool IsLoaded { get; private set; }
    public bool IsLoading { get; private set; }
    public void Load(Project project)
    {
        IsLoading = true;
        List<TaskManager.LiveTask> tasks = new();
        foreach (StudioResource res in GetDependencies(project))
        {
            if ((!res.IsLoaded && !res.IsLoading) || project.Type != res.GameType)
            {
                TaskManager.LiveTask t = new TaskManager.LiveTask($@"Resource - Loading {res.nameForUI}({project?.Settings.ProjectName})", TaskManager.RequeueType.None, true, () => {
                    res.Load(project);
                });
                TaskManager.Run(t);
                tasks.Add(t);
            }
        }
        foreach (TaskManager.LiveTask t in tasks)
        {
            t.Task.Wait();
        }
        Load();
        IsLoading = false;
        IsLoaded = true;
    }
    protected abstract void Load();
    protected abstract IEnumerable<StudioResource> GetDependencies(Project project);
}
