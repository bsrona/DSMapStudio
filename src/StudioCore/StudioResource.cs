using DotNext.Collections.Generic;
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
        if (IsLoaded || IsLoading)
            return;
        IsLoading = true;
        List<TaskManager.LiveTask> tasks = new();
        foreach (StudioResource res in GetDependencies(project))
        {
            if (!res.IsLoaded)
            {
                TaskManager.LiveTask t = new TaskManager.LiveTask(res.GetTaskName(), TaskManager.RequeueType.None, true, () => {
                    res.Load(project);
                });
                t = TaskManager.Run(t);
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

    public virtual string GetTaskName()
    {
        return $@"Resource - Loading {nameForUI}";
    }
    protected abstract void Load();
    protected abstract IEnumerable<StudioResource> GetDependencies(Project project);

    public static bool AreResourcesLoaded(IEnumerable<StudioResource> res)
    {
        foreach (StudioResource r in res)
        {
            if (!r.IsLoaded)
                return false;
        }
        return true;
    }
    public static void Load(Project project, IEnumerable<StudioResource> resources)
    {
        foreach (StudioResource res in resources)
        {
            if (!res.IsLoaded)
            {
                TaskManager.Run(new TaskManager.LiveTask(res.GetTaskName(), TaskManager.RequeueType.None, true, () => {
                    res.Load(project);
                }));
            }
        }
    }
}
