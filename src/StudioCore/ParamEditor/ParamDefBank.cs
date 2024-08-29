using Andre.Formats;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.HighPerformance.Buffers;
using Octokit;
using SoulsFormats;
using StudioCore.Editor;
using StudioCore.Platform;
using StudioCore.TextEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StudioCore.ParamEditor;

/// <summary>
///     Utilities for dealing with global paramdefs for a game
/// </summary>
public class ParamDefBank : StudioResource
{
    /// <summary>
    ///     Mapping from path -> PARAMDEF for cache and and comparison purposes. TODO: check for paramdef comparisons and evaluate if the file/paramdef was actually the same.
    /// </summary>
    private static readonly Dictionary<string, PARAMDEF> _paramdefsCache = new();

    /// <summary>
    ///     Mapping from ParamType -> PARAMDEF.
    /// </summary>
    private Dictionary<string, PARAMDEF> _paramdefs = new();

    //TODO private this
    public Dictionary<PARAMDEF, ParamMetaData> ParamMetas = new();

    public bool IsMetaLoaded = false;


    /// <summary>
    ///     Mapping from Param filename -> Manual ParamType.
    ///     This is for params with no usable ParamType at some particular game version.
    ///     By convention, ParamTypes ending in "_TENTATIVE" do not have official data to reference.
    /// </summary>
    private Dictionary<string, string> _tentativeParamType;

    public ParamDefBank() : base(Locator.ActiveProject.Type, "Paramdefs")
    {
    }

    public Dictionary<string, PARAMDEF> GetParamDefs()
    {
        return _paramdefs;
    }

    public Dictionary<string, string> GetTentativeParamTypes()
    {
        return _tentativeParamType;
    }

    private List<(string, PARAMDEF)> LoadParamdefs()
    {
        _paramdefs = new Dictionary<string, PARAMDEF>();
        _tentativeParamType = new Dictionary<string, string>();
        var files = Locator.ActiveProject.AssetLocator.GetAllProjectFiles($@"Paramdex\{AssetUtils.GetGameIDForDir(Locator.ActiveProject.Type)}\Defs", ["*.xml"], true, false);
        List<(string, PARAMDEF)> defPairs = new();
        foreach (var f in files)
        {
            if (!_paramdefsCache.TryGetValue(f, out PARAMDEF pdef))
            {
                pdef = PARAMDEF.XmlDeserialize(f, true);
            } 
            _paramdefs.Add(pdef.ParamType, pdef);
            defPairs.Add((f, pdef));
        }

        var tentativeMappingPath = Locator.ActiveProject.AssetLocator.GetProjectFilePath($@"{Locator.ActiveProject.AssetLocator.GetParamdexDir()}\Defs\TentativeParamType.csv");
        if (File.Exists(tentativeMappingPath))
        {
            // No proper CSV library is used currently, and all CSV parsing is in the context of param files.
            // If a CSV library is introduced in DSMapStudio, use it here.
            foreach (var line in File.ReadAllLines(tentativeMappingPath).Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                {
                    throw new FormatException($"Malformed line in {tentativeMappingPath}: {line}");
                }

                _tentativeParamType[parts[0]] = parts[1];
            }
        }

        return defPairs;
    }
    public void LoadParamMeta(List<(string, PARAMDEF)> defPairs)
    {
        //This way of tying stuff together still sucks
        var mdir = Locator.ActiveProject.AssetLocator.GetProjectFilePath($@"{Locator.ActiveProject.AssetLocator.GetParamdexDir()}\Meta");
        foreach ((var f, PARAMDEF pdef) in defPairs)
        {
            var fName = f.Substring(f.LastIndexOf('\\') + 1);
            var md = ParamMetaData.XmlDeserialize($@"{mdir}\{fName}", pdef);
            ParamMetas.Add(pdef, md);
        }
    }

    private void LoadParams()
    {
        IsMetaLoaded = false;
        List<(string, PARAMDEF)> defPairs = LoadParamdefs();
        TaskManager.Run(new TaskManager.LiveTask("Param - Load Meta", TaskManager.RequeueType.WaitThenRequeue, false, () =>
        {
            LoadParamMeta(defPairs);
            IsMetaLoaded = true;
        }));
    }

    protected override void Load()
    {
        LoadParams();
    }

    protected override IEnumerable<StudioResource> GetDependencies(Project project)
    {
        return [];
    }
}
