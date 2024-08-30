using Andre.Formats;
using Microsoft.Extensions.Logging;
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
///     Utilities for dealing with global params for a game
/// </summary>
public partial class ParamBank : DataBank
{

    private Dictionary<string, HashSet<int>> _primaryDiffCache; //If param != primaryparam
    private Dictionary<string, HashSet<int>> _vanillaDiffCache; //If param != vanillaparam

    public IReadOnlyDictionary<string, HashSet<int>> VanillaDiffCache
    {
        get
        {
            if (IsLoading)
            {
                return null;
            }

            {
                if (VanillaBank == this)
                {
                    return null;
                }
            }
            return _vanillaDiffCache;
        }
    }

    public IReadOnlyDictionary<string, HashSet<int>> PrimaryDiffCache
    {
        get
        {
            if (IsLoading)
            {
                return null;
            }

            {
                if (PrimaryBank == this)
                {
                    return null;
                }
            }
            return _primaryDiffCache;
        }
    }

    public void ClearParamDiffCaches()
    {
        _vanillaDiffCache = new Dictionary<string, HashSet<int>>();
        _primaryDiffCache = new Dictionary<string, HashSet<int>>();
        foreach (var param in _params.Keys)
        {
            _vanillaDiffCache.Add(param, new HashSet<int>());
            _primaryDiffCache.Add(param, new HashSet<int>());
        }
    }

    public static void RefreshAllParamDiffCaches(bool checkAuxVanillaDiff)
    {
        PrimaryBank.RefreshParamDiffCaches(true);
        foreach (KeyValuePair<string, ParamBank> bank in AuxBanks)
        {
            bank.Value.RefreshParamDiffCaches(checkAuxVanillaDiff);
        }

        UICache.ClearCaches();
    }

    public void RefreshParamDiffCaches(bool checkVanillaDiff)
    {
        if (this != VanillaBank && checkVanillaDiff)
        {
            _vanillaDiffCache = GetParamDiff(VanillaBank);
        }

        if (this == VanillaBank && PrimaryBank._vanillaDiffCache != null)
        {
            _primaryDiffCache = PrimaryBank._vanillaDiffCache;
        }
        else if (this != PrimaryBank)
        {
            _primaryDiffCache = GetParamDiff(PrimaryBank);
        }

        UICache.ClearCaches();
    }

    private Dictionary<string, HashSet<int>> GetParamDiff(ParamBank otherBank)
    {
        if (IsLoading || otherBank == null || otherBank.IsLoading)
        {
            return null;
        }

        Dictionary<string, HashSet<int>> newCache = new();
        foreach (var param in _params.Keys)
        {
            HashSet<int> cache = new();
            newCache.Add(param, cache);
            Param p = _params[param];
            if (!otherBank._params.ContainsKey(param))
            {
                Console.WriteLine("Missing vanilla param " + param);
                continue;
            }

            Param.Row[] rows = _params[param].Rows.OrderBy(r => r.ID).ToArray();
            Param.Row[] vrows = otherBank._params[param].Rows.OrderBy(r => r.ID).ToArray();

            var vanillaIndex = 0;
            var lastID = -1;
            ReadOnlySpan<Param.Row> lastVanillaRows = default;
            for (var i = 0; i < rows.Length; i++)
            {
                var ID = rows[i].ID;
                if (ID == lastID)
                {
                    RefreshParamRowDiffCache(rows[i], lastVanillaRows, cache);
                }
                else
                {
                    lastID = ID;
                    while (vanillaIndex < vrows.Length && vrows[vanillaIndex].ID < ID)
                    {
                        vanillaIndex++;
                    }

                    if (vanillaIndex >= vrows.Length)
                    {
                        RefreshParamRowDiffCache(rows[i], Span<Param.Row>.Empty, cache);
                    }
                    else
                    {
                        var count = 0;
                        while (vanillaIndex + count < vrows.Length && vrows[vanillaIndex + count].ID == ID)
                        {
                            count++;
                        }

                        lastVanillaRows = new ReadOnlySpan<Param.Row>(vrows, vanillaIndex, count);
                        RefreshParamRowDiffCache(rows[i], lastVanillaRows, cache);
                        vanillaIndex += count;
                    }
                }
            }
        }

        return newCache;
    }

    private static void RefreshParamRowDiffCache(Param.Row row, ReadOnlySpan<Param.Row> otherBankRows,
        HashSet<int> cache)
    {
        if (IsChanged(row, otherBankRows))
        {
            cache.Add(row.ID);
        }
        else
        {
            cache.Remove(row.ID);
        }
    }

    public void RefreshParamRowDiffs(Param.Row row, string param)
    {
        if (param == null)
        {
            return;
        }

        if (VanillaBank.Params.ContainsKey(param) && VanillaDiffCache != null &&
            VanillaDiffCache.ContainsKey(param))
        {
            Param.Row[] otherBankRows = VanillaBank.Params[param].Rows.Where(cell => cell.ID == row.ID).ToArray();
            RefreshParamRowDiffCache(row, otherBankRows, VanillaDiffCache[param]);
        }

        if (this != PrimaryBank)
        {
            return;
        }

        foreach (ParamBank aux in AuxBanks.Values)
        {
            if (!aux.Params.ContainsKey(param) || aux.PrimaryDiffCache == null ||
                !aux.PrimaryDiffCache.ContainsKey(param))
            {
                continue; // Don't try for now
            }

            Param.Row[] otherBankRows = aux.Params[param].Rows.Where(cell => cell.ID == row.ID).ToArray();
            RefreshParamRowDiffCache(row, otherBankRows, aux.PrimaryDiffCache[param]);
        }
    }

    private static bool IsChanged(Param.Row row, ReadOnlySpan<Param.Row> vanillaRows)
    {
        //List<Param.Row> vanils = vanilla.Rows.Where(cell => cell.ID == row.ID).ToList();
        if (vanillaRows.Length == 0)
        {
            return true;
        }

        foreach (Param.Row vrow in vanillaRows)
        {
            if (row.RowMatches(vrow))
            {
                return false; //if we find a matching vanilla row
            }
        }

        return true;
    }

    public HashSet<int> GetVanillaDiffRows(string param)
    {
        IReadOnlyDictionary<string, HashSet<int>> allDiffs = VanillaDiffCache;
        if (allDiffs == null || !allDiffs.ContainsKey(param))
        {
            return EMPTYSET;
        }

        return allDiffs[param];
    }

    public HashSet<int> GetPrimaryDiffRows(string param)
    {
        IReadOnlyDictionary<string, HashSet<int>> allDiffs = PrimaryDiffCache;
        if (allDiffs == null || !allDiffs.ContainsKey(param))
        {
            return EMPTYSET;
        }

        return allDiffs[param];
    }
}
