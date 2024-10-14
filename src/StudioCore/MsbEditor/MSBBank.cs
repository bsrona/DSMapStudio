using Silk.NET.OpenGL;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace StudioCore.ParamEditor;

/// <summary>
///     Utilities for dealing with global paramdefs for a game
/// </summary>
public class MSBBank : DataBank
{

    /// <summary>
    ///     List of msb names
    /// </summary>
    private List<string> _msbIDs = new();

    /// <summary>
    ///     Mapping from msb path|name -> IMsb.
    /// </summary>
    private Dictionary<string, IMsb> _msbs = new();

    public MSBBank(Project project) : base(project, "MSBs")
    {

    }

    protected override void Load()
    {
        //move this functionality to msbbank
        _msbIDs = LoadFullMapList();
    }

    public override void Save()
    {
    }

    protected override IEnumerable<StudioResource> GetDependencies(Project project)
    {
        return [];
    }

    public IMsb GetMsb(GameType type, string mapId)
    {
        if (_msbs.TryGetValue(mapId, out IMsb msb))
        {
            return msb;
        }

        AssetDescription ad = Project.AssetLocator.GetMapMSB(mapId);
        if (ad.AssetPath == null)
        {
            return null;
        }
        string path = ad.AssetPath;

        if (type == GameType.DarkSoulsIII)
        {
            msb = MSB3.Read(path);
        }
        else if (type == GameType.Sekiro)
        {
            msb = MSBS.Read(path);
        }
        else if (type == GameType.EldenRing)
        {
            msb = MSBE.Read(path);
        }
        else if (type == GameType.ArmoredCoreVI)
        {
            msb = MSB_AC6.Read(path);
        }
        else if (type == GameType.DarkSoulsIISOTFS)
        {
            msb = MSB2.Read(path);
        }
        else if (type == GameType.Bloodborne)
        {
            msb = MSBB.Read(path);
        }
        else if (type == GameType.DemonsSouls)
        {
            msb = MSBD.Read(path);
        }
        else
        {
            msb = MSB1.Read(path);
        }
        _msbs[mapId] = msb;
        return msb;
    }


    /// <summary>
    ///     Gets the full list of maps in the game (excluding chalice dungeons). Basically if there's an msb for it,
    ///     it will be in this list.
    /// </summary>
    /// <returns></returns>
    public List<string> GetFullMapList()
    {
        if (_msbIDs == null)
            _msbIDs = LoadFullMapList();
        return _msbIDs;
    }
    private List<string> LoadFullMapList()
    {
        HashSet<string> mapSet = new();

        // DS2 has its own structure for msbs, where they are all inside individual folders
        if (Project.Type == GameType.DarkSoulsIISOTFS)
        {
            foreach (var map in Project.AssetLocator.GetAllAssets(@"map", [@"*.msb"], true, true))
            {
                mapSet.Add(Path.GetFileNameWithoutExtension(map));
            }
        }
        else
        {
            foreach (var msb in Project.AssetLocator.GetAllAssets(@"map\MapStudio\", [@"*.msb", @"*.msb.dcx"]))
            {
                mapSet.Add(ProjectAssetLocator.GetFileNameWithoutExtensions(msb));
            }
        }
        Regex mapRegex = new(@"^m\d{2}_\d{2}_\d{2}_\d{2}$");
        List<string> mapList = mapSet.Where(x => mapRegex.IsMatch(x)).ToList();
        mapList.Sort();
        return mapList;
    }
}
