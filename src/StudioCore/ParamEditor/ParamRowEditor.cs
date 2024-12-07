using Andre.Formats;
using static Andre.Native.ImGuiBindings;
using SoulsFormats;
using StudioCore.Editor;
using StudioCore.Editor.MassEdit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;
using DotNext.Collections.Specialized;
using StudioCore.TextEditor;

namespace StudioCore.ParamEditor;

public class ParamRowEditor
{
    private readonly ParamEditorScreen _paramEditor;

    private Dictionary<string, PropertyInfo[]> _propCache = new();
    public ActionManager ContextActionManager;

    public ParamRowEditor(ActionManager manager, ParamEditorScreen paramEditorScreen)
    {
        ContextActionManager = manager;
        _paramEditor = paramEditorScreen;
    }

    private static void PropEditorParamRow_Header(bool isActiveView, ref string propSearchString)
    {
        if (propSearchString != null)
        {
            if (isActiveView && InputTracker.GetKeyDown(KeyBindings.Current.Param_SearchField))
            {
                EditorDecorations.ImGuiSetKeyboardFocusHere();
            }

            ImGui.InputText($"Search <{KeyBindings.Current.Param_SearchField.HintText}>", ref propSearchString,
                255);
            if (ImGui.IsItemEdited())
            {
                UICache.ClearCaches();
            }

            var resAutoCol = AutoFill.ColumnSearchBarAutoFill();
            if (resAutoCol != null)
            {
                propSearchString = resAutoCol;
                UICache.ClearCaches();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }
    }

    private void PropEditorParamRow_RowFields(ParamBank bank, Param.Row row, Param.Row vrow,
        List<(string, Param.Row)> auxRows, Param.Row crow, ref int imguiId, ParamEditorSelectionState selection)
    {
        ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(0.8f, 0.8f, 1.0f, 1.0f));
        PropertyInfo nameProp = row.GetType().GetProperty("Name");
        PropertyInfo idProp = row.GetType().GetProperty("ID");
        PropEditorPropInfoRow(bank, row, vrow, auxRows, crow, nameProp, "Name", ref imguiId, selection);
        PropEditorPropInfoRow(bank, row, vrow, auxRows, crow, idProp, "ID", ref imguiId, selection);
        ImGui.PopStyleColor(1);
        ImGui.Spacing();
    }

    private void PropEditorParamRow_PinnedFields(List<string> pinList, ParamBank bank, Param.Row row,
        Param.Row vrow, List<(string, Param.Row)> auxRows, Param.Row crow, List<(PseudoColumn, Param.Column)> cols,
        List<(PseudoColumn, Param.Column)> vcols, List<List<(PseudoColumn, Param.Column)>> auxCols, ref int imguiId,
        string activeParam, ParamEditorSelectionState selection)
    {
        List<string> pinnedFields = new List<string>(pinList);
        foreach (var field in pinnedFields)
        {
            List<(PseudoColumn, Param.Column)> matches =
                cols.Where((x, i) => x.Item2 != null && x.Item2.Def.InternalName == field).ToList();
            List<(PseudoColumn, Param.Column)> vmatches =
                vcols.Where((x, i) => x.Item2 != null && x.Item2.Def.InternalName == field).ToList();
            List<List<(PseudoColumn, Param.Column)>> auxMatches = auxCols.Select((aux, i) =>
                aux.Where((x, i) => x.Item2 != null && x.Item2.Def.InternalName == field).ToList()).ToList();
            for (var i = 0; i < matches.Count; i++)
            {
                PropEditorPropCellRow(bank,
                    row,
                    crow,
                    matches[i],
                    vrow,
                    vmatches.Count > i ? vmatches[i] : (PseudoColumn.None, null),
                    auxRows,
                    auxMatches.Select((x, j) => x.Count > i ? x[i] : (PseudoColumn.None, null)).ToList(),
                    OffsetTextOfColumn(matches[i].Item2),
                    ref imguiId, activeParam, true, selection);
            }
        }
    }

    private void PropEditorParamRow_MainFields(ParamMetaData meta, ParamBank bank, Param.Row row, Param.Row vrow,
        List<(string, Param.Row)> auxRows, Param.Row crow, List<(PseudoColumn, Param.Column)> cols,
        List<(PseudoColumn, Param.Column)> vcols, List<List<(PseudoColumn, Param.Column)>> auxCols, ref int imguiId,
        string activeParam, ParamEditorSelectionState selection)
    {
        List<string> fieldOrder = meta != null && meta.AlternateOrder != null && CFG.Current.Param_AllowFieldReorder
            ? meta.AlternateOrder
            : new List<string>();
        foreach (PARAMDEF.Field field in row.Def.Fields)
        {
            if (!fieldOrder.Contains(field.InternalName))
            {
                fieldOrder.Add(field.InternalName);
            }
        }

        var lastRowExists = false;
        foreach (var field in fieldOrder)
        {
            if (field.Equals("-") && lastRowExists)
            {
                EditorDecorations.ImguiTableSeparator();
                lastRowExists = false;
                continue;
            }

            List<(PseudoColumn, Param.Column)> matches =
                cols?.Where((x, i) => x.Item2 != null && x.Item2.Def.InternalName == field).ToList();
            List<(PseudoColumn, Param.Column)> vmatches =
                vcols?.Where((x, i) => x.Item2 != null && x.Item2.Def.InternalName == field).ToList();
            List<List<(PseudoColumn, Param.Column)>> auxMatches = auxCols?.Select((aux, i) =>
                aux.Where((x, i) => x.Item2 != null && x.Item2.Def.InternalName == field).ToList()).ToList();
            for (var i = 0; i < matches.Count; i++)
            {
                PropEditorPropCellRow(bank,
                    row,
                    crow,
                    matches[i],
                    vrow,
                    vmatches.Count > i ? vmatches[i] : (PseudoColumn.None, null),
                    auxRows,
                    auxMatches.Select((x, j) => x.Count > i ? x[i] : (PseudoColumn.None, null)).ToList(),
                    OffsetTextOfColumn(matches[i].Item2),
                    ref imguiId, activeParam, false, selection);
                lastRowExists = true;
            }
        }
    }

    private void FillPropertyRowEntry_Object<T>(ref PropertyRowEntry<T> e, ref int index, string property, T obj, Param.Row robj, T vobj, Param.Row vrobj, List<T> aobjs, List<Param.Row> arobjs, T cobj, Param.Row crobj) where T : class
    {
        e.index = index++;
        FillPropertyRowEntry_Basic_Reflection(ref e, property, obj, robj, vobj, vrobj, aobjs, arobjs, cobj, crobj);
        FillPropertyRowEntry_Diffs(ref e);
    }
    private void FillPropertyRowEntry_Basic_Reflection<T>(ref PropertyRowEntry<T> e, string property, T obj, Param.Row robj, T vobj, Param.Row vrobj, List<T> aobjs, List<Param.Row> arobjs, T cobj, Param.Row crobj) where T : class //Stupid stupid param rows funking my day up
    {
        e.isDummy = false;
        ref FieldInfoEntry f = ref e.field;
        f.displayText = property;
        f.internalName = property;
        f.proprow = typeof(T).GetProperty(property);
        f.propType = f.proprow.PropertyType;
        FillCellInfoEntry(ref e.cell, obj, robj, f.proprow);
        FillCellInfoEntry(ref e.vanilla, vobj, vrobj, f.proprow);
        e.aux = new CellInfoEntry<T>[aobjs.Count];
        for(int i=0; i<aobjs.Count; i++)
        {
            FillCellInfoEntry(ref e.aux[i], aobjs[i], arobjs[i], f.proprow);
        }
        FillCellInfoEntry(ref e.compare, cobj, crobj, f.proprow);
    }
    private void FillCellInfoEntry<T>(ref CellInfoEntry<T> c, T obj, Param.Row robj, PropertyInfo prop) where T : class
    {
        if (obj != null && robj != null)
        {
            c.obj = obj;
            c.row = robj;
            c.oldval = obj != null ? prop.GetValue(obj) : null; //Using reflection - sad and bad! Alternative? Delegate? inlineable function in a struct so it's fully reified at runtime?
        }
        else
        {
            c.isNull = true;
        }
    }
    private void FillPropertyRowEntry_Struct<T>(ref PropertyRowEntry<T> e, ref int index, string property, string name, Type type, (bool, T) obj, Param.Row robj, (bool, T) vobj, Param.Row vrobj, List<(bool, T)> aobjs, List<Param.Row> arobjs, (bool, T) cobj, Param.Row crobj) where T : struct
    {
        e.index = index++;
        FillPropertyRowEntry_InnerStruct_Reflection(ref e, property, name, type, obj, robj, vobj, vrobj, aobjs, arobjs, cobj, crobj);
        FillPropertyRowEntry_Diffs(ref e);
    }
    private void FillPropertyRowEntry_InnerStruct_Reflection<T>(ref PropertyRowEntry<T> e, string property, string name, Type type, (bool, T) obj, Param.Row robj, (bool, T) vobj, Param.Row vrobj, List<(bool, T)> aobjs, List<Param.Row> arobjs, (bool, T) cobj, Param.Row crobj) where T : struct //Stupid stupid param rows funking my day up
    {
        e.isDummy = false;
        ref FieldInfoEntry f = ref e.field;
        f.displayText = name;
        f.internalName = name;
        f.proprow = typeof(T).GetProperty(property);
        f.propType = type;
        FillCellInfoEntry(ref e.cell, obj, robj, f.proprow);
        FillCellInfoEntry(ref e.vanilla, vobj, vrobj, f.proprow);
        e.aux = new CellInfoEntry<T>[aobjs.Count];
        for(int i=0; i<aobjs.Count; i++)
        {
            FillCellInfoEntry(ref e.aux[i], aobjs[i], arobjs[i], f.proprow);
        }
        FillCellInfoEntry(ref e.compare, cobj, crobj, f.proprow);
    }
    private void FillCellInfoEntry<T>(ref CellInfoEntry<T> c, (bool, T) obj, Param.Row robj, PropertyInfo prop) where T : struct
    {
        if (!obj.Item1 && robj != null)
        {
            c.obj = obj.Item2;
            c.row = robj;
            c.oldval = prop.GetValue(obj.Item2); //Using reflection - sad and bad! Alternative? Delegate? inlineable function in a struct so it's fully reified at runtime?
        }
        else
        {
            c.isNull = true;
        }
    }
    private void FillPropertyRowEntry_Diffs<T>(ref PropertyRowEntry<T> e)
    {
        Type t = e.field.propType;
        ref FieldInfoEntry f = ref e.field;
        ref CellInfoEntry<T> c = ref e.cell;
        ref CellInfoEntry<T> v = ref e.vanilla;  
        c.diffVanilla = ParamUtils.IsValueDiff(ref c.oldval, ref v.oldval, t);
        v.conflictOrDiffPrimary = c.diffVanilla;
        for(int i=0; i<e.aux.Length; i++)
        {
            ref CellInfoEntry<T> a = ref e.aux[i];
            a.diffVanilla = ParamUtils.IsValueDiff(ref a.oldval, ref v.oldval, t);
            a.conflictOrDiffPrimary = ParamUtils.IsValueDiff(ref a.oldval, ref c.oldval, t);
        }
        ref CellInfoEntry<T> cmp = ref e.compare;
        cmp.diffVanilla = ParamUtils.IsValueDiff(ref cmp.oldval, ref v.oldval, t);
        cmp.conflictOrDiffPrimary = ParamUtils.IsValueDiff(ref cmp.oldval, ref c.oldval, t);

        c.conflictOrDiffPrimary = (c.diffVanilla ? 1 : 0) + e.aux.Count((a) => a.diffVanilla && a.conflictOrDiffPrimary) > 1; //Doesn't mark conflict if it matches primary - check this matches search behaviour
        //v.diffVanilla unused
    }
    private void FillMetaFromFieldMeta<T>(ref PropertyRowEntry<T> e, PARAMDEF.Field field)
    {
        if (FieldMetaData._FieldMetas.TryGetValue(field, out FieldMetaData meta))
        {
            ref FieldInfoEntry f = ref e.field;
            ref CellInfoEntry<T> c = ref e.cell;
            f.meta = meta;
            f.wiki = meta.Wiki;
            f.displayText = NameText(f.internalName, meta.AltName, f.col);
            
            (f.activeFmgRefText, f.inactiveFmgRefText) = FmgRefText(meta.FmgRef, c.row);
            f.isFMGRef = f.activeFmgRefText != null || f.inactiveFmgRefText != null;

            (f.activeParamRefText, f.inactiveParamRefText) = ParamRefText(meta.RefTypes, c.row);
            f.isParamRef = f.activeParamRefText != null || f.inactiveParamRefText != null;

            f.enumText = EnumText(meta.EnumType, c.row);
            f.isEnum = f.enumText != null;

            FillMetaFromFieldMeta(ref c, meta);
            FillMetaFromFieldMeta(ref e.vanilla, meta);
            for (int i=0; i<e.aux.Length; i++)
            {
                FillMetaFromFieldMeta(ref e.aux[i], meta);
            }
            FillMetaFromFieldMeta(ref e.compare, meta);
        }
    }
    private void FillMetaFromFieldMeta<T>(ref CellInfoEntry<T> c, FieldMetaData meta)
    {
        c.fmgRefText = FmgRefValues(meta.FmgRef, c.row, c.oldval);
        c.paramRefText = ParamRefValues(meta.RefTypes, c.row, c.oldval);
        c.enumText = EnumValue(meta.EnumType, c.row, c.oldval);
    }
    private string NameText(string internalName, string altName, Param.Column col)
    {
        if (CFG.Current.Param_MakeMetaNamesPrimary && !string.IsNullOrWhiteSpace(altName))
            (altName, internalName) = (internalName, altName);
        string offsetComponent = CFG.Current.Param_ShowFieldOffsets ? OffsetTextOfColumn(col) : "";
        string primaryNameComponent = internalName;
        string secondaryNameComponent = CFG.Current.Param_ShowSecondaryNames && !string.IsNullOrWhiteSpace(altName) ? $" ({altName})": "";
        
        return $"{offsetComponent}{primaryNameComponent}{secondaryNameComponent}";
    }
    private string EnumText(ParamEnum e, Param.Row context)
    {
        if (e == null)
            return null;
        return e?.name;
    }
    private string EnumValue(ParamEnum e, Param.Row context, object oldval)
    {
        if (e == null || oldval == null)
            return null;
        return e?.values.GetValueOrDefault(oldval.ToParamEditorString());
    }
    private (string, string) ParamRefText(List<ParamRef> paramRef, Param.Row context) //Modified from editordecorations. move elsewhere.
    {
        if (paramRef == null || paramRef.Count == 0)
            return (null, null);
        (List<ParamRef> activeRefs, List<ParamRef> inactiveRefs) = ActiveParamRefs(paramRef, context);
        return (string.Join(',', activeRefs.Select((r) => r.param)), string.Join(',', inactiveRefs.Select((r) => $@"!{r.param}")));
    }
    private string ParamRefValues(List<ParamRef> paramRef, Param.Row context, object oldval) //Modified from editordecorations. move elsewhere.
    {
        if (paramRef == null)
            return null;
        int val = 0;
        try
        {
            val = Convert.ToInt32(oldval);
        }
        catch (Exception e)
        {
            return null;
        }
        (List<ParamRef> activeRefs, List<ParamRef> inactiveRefs) = ActiveParamRefs(paramRef, context);
        //Nicked resolution code from editordecorations again. Seriously move this.
        var refs = activeRefs.Select(rf => {
            if (!Locator.ActiveProject.ParamBank.Params.TryGetValue(rf.param, out Param param))
                return null;
            var altval = val;
            var postfix = "";
            if (rf.offset != 0)
            {
                altval += rf.offset;
                postfix += rf.offset > 0 ? "+" + rf.offset : rf.offset;
            }
            ParamMetaData meta = ParamMetaData.Get(param.AppliedParamdef);
            if (meta != null && meta.Row0Dummy && altval == 0)
                return null;
            Param.Row r = param[altval];
            if (r == null && altval > 0 && meta != null)
            {
                if (meta.FixedOffset != 0)
                {
                    altval = val + meta.FixedOffset;
                    postfix += meta.FixedOffset > 0 ? "+" + meta.FixedOffset : meta.FixedOffset;
                }
                if (meta.OffsetSize > 0)
                {
                    altval = altval - (altval % meta.OffsetSize);
                    postfix += "+" + (val % meta.OffsetSize);
                }
                r = param[altval];
            }
            if (r == null)
                return null;
            if (string.IsNullOrWhiteSpace(r.Name))
                return "Unnamed Row" + postfix;
            return r.Name + postfix;
        }).Where(x => x != null);
        return string.Join(',', refs);
    }
    private (List<ParamRef>, List<ParamRef>) ActiveParamRefs(List<ParamRef> fmgRef, Param.Row context)
    {
        List<ParamRef> activeRefs = new();
        List<ParamRef> inactiveRefs = new();
        foreach (ParamRef r in fmgRef)
        {
            if (context == null || r.conditionField == null)
            {
                activeRefs.Add(r);
                continue;
            }
            Param.Cell? c = context?[r.conditionField];
            if (c==null || !c.HasValue)
            {
                inactiveRefs.Add(r);
                continue;
            }
            try
            {
                int value = Convert.ToInt32(c.Value.Value);
                if (r.conditionValue == value)
                    activeRefs.Add(r);
                else
                    inactiveRefs.Add(r);
            }
            catch (Exception e)
            {
                inactiveRefs.Add(r);
            }
        }
        return (activeRefs, inactiveRefs);
    }
    private (string, string) FmgRefText(List<FMGRef> fmgRef, Param.Row context) //Modified from editordecorations. move elsewhere.
    {
        if (fmgRef == null || fmgRef.Count == 0)
            return (null, null);
        (List<FMGRef> activeRefs, List<FMGRef> inactiveRefs) = ActiveFMGRefs(fmgRef, context);
        return (string.Join(',', activeRefs.Select((r) => r.fmg)), string.Join(',', inactiveRefs.Select((r) => $@"!{r.fmg}")));
    }
    //Reinventing editordecorations.resolvefmgrefs
    private string FmgRefValues(List<FMGRef> fmgRef, Param.Row context, object oldval) //Modified from editordecorations. move elsewhere.
    {
        if (fmgRef == null)
            return null;
        int val = 0;
        try
        {
            val = Convert.ToInt32(oldval);
        }
        catch (Exception e)
        {
            return null;
        }
        (List<FMGRef> activeRefs, List<FMGRef> inactiveRefs) = ActiveFMGRefs(fmgRef, context);
        var refs = activeRefs.Select(rf => Locator.ActiveProject.FMGBank.FmgInfoBank.FirstOrDefault(x => x.Name == rf.fmg))
            .Where(fmgi => fmgi != null)
            .Select(fmgi => {
                FMGEntryGroup group = Locator.ActiveProject.FMGBank.GenerateEntryGroup(val, fmgi);
                //TODO: add default stringifying to entrygroup
                if (group.Title != null) return group.Title.Text;
                if (group.Summary != null) return group.Summary.Text;
                if (group.Description != null) return group.Description.Text;
                if (group.TextBody != null) return group.TextBody.Text;
                if (group.ExtraText != null) return group.ExtraText.Text;
                return "";
            });
        return string.Join(',', refs);
    }
    private (List<FMGRef>, List<FMGRef>) ActiveFMGRefs(List<FMGRef> fmgRef, Param.Row context)
    {
        List<FMGRef> activeRefs = new();
        List<FMGRef> inactiveRefs = new();
        foreach (FMGRef r in fmgRef)
        {
            if (context == null || r.conditionField == null)
            {
                activeRefs.Add(r);
                continue;
            }
            Param.Cell? c = context?[r.conditionField];
            if (c==null || !c.HasValue)
            {
                inactiveRefs.Add(r);
                continue;
            }
            try
            {
                int value = Convert.ToInt32(c.Value.Value);
                if (r.conditionValue == value)
                    activeRefs.Add(r);
                else
                    inactiveRefs.Add(r);
            }
            catch (Exception e)
            {
                inactiveRefs.Add(r);
            }
        }
        return (activeRefs, inactiveRefs);
    }

    public void PropEditorParamRowNew(ParamBank bank, Param.Row row, Param.Row vrow, List<(string, Param.Row)> auxRows, Param.Row crow, ref string propSearchString, string activeParam, bool isActiveView, ParamEditorSelectionState selection, bool limitHeight)
    {
        PropertyRowEntry<Param.Row>[] propertyRowsHeader = UICache.GetCached(_paramEditor, row, "fieldsHeader", () =>
        {
            PropertyRowEntry<Param.Row>[] rowFields = new PropertyRowEntry<Param.Row>[2];
            int index = 0;
            List<Param.Row> auxRowsF = auxRows.Select((x)=>x.Item2).ToList(); //Fiddle with input data a little
            FillPropertyRowEntry_Object(ref rowFields[0], ref index, "Name", row, row, vrow, vrow, auxRowsF, auxRowsF, crow, crow);
            FillPropertyRowEntry_Object(ref rowFields[1], ref index, "ID", row, row, vrow, vrow, auxRowsF, auxRowsF, crow, crow);
            return rowFields;
        });
        PropertyRowEntry<Param.Cell>[] propertyRowsPinned = [];//TODO
        var search = propSearchString;
        PropertyRowEntry<Param.Cell>[] propertyRows = UICache.GetCached(_paramEditor, row, "fields", () =>
        {
            List<Param.Column> cols = SearchEngine.cell.Search((activeParam, row), search, true, true).Where((x)=>x.Item1 == PseudoColumn.None).Select((x)=>x.Item2).ToList();
            //TODO Apply meta sorting and dummyrows here
            List<Param.Column> vcols = cols.Select((x, i) => (PseudoColumn.None, x).GetAs(ParamBank.VanillaBank.GetParamFromName(activeParam)).Item2).ToList();
            List<List<Param.Column>> auxCols = auxRows.Select((r, i) =>
                    cols.Select((c, j) => (PseudoColumn.None, c).GetAs(ResDirectory.CurrentGame.AuxProjects[r.Item1].ParamBank.GetParamFromName(activeParam)).Item2)
                        .ToList()).ToList();
            PropertyRowEntry<Param.Cell>[] rowFields = new PropertyRowEntry<Param.Cell>[cols.Count];
            int index = 2;
            for (int i=0; i<rowFields.Length; i++)
            {
                Param.Column col = cols[i];
                Param.Column vcol = vcols[i];
                List<Param.Column> acol = auxCols.Select((x) => x[i]).ToList();
                FillPropertyRowEntry_Struct(ref rowFields[i], ref index, "Value", col.Def.InternalName, (PseudoColumn.None, col).GetColumnType(), (row==null, row?[col]??default), row, (vrow==null, vrow?[col]??default), vrow, acol.Select((x)=>(auxRows[i].Item2==null, auxRows[i].Item2?[x]??default)).ToList(), auxRows.Select((x)=>x.Item2).ToList(), (crow==null, crow?[col]??default), crow);
                rowFields[i].field.col = col; //dirty but not generic (unless we subscribe harder to forcing param row into things which is bad)
                FillMetaFromFieldMeta(ref rowFields[i], col.Def);
            }
            return rowFields;
        });
        var showVanilla = CFG.Current.Param_ShowVanillaParams;
        var showParamCompare = auxRows.Count > 0;
        var showRowCompare = crow != null;
        var showColumnHeaders = showParamCompare;
        
        float fieldDataHeight = limitHeight ? ImGui.GetWindowHeight() * 3/5f : -1;
        if (ImGui.BeginChild("regularFieldData", new Vector2(-1, fieldDataHeight), ImGuiChildFlags.AlwaysAutoResize))
        {
            PropEditorParamRow_Header(isActiveView, ref propSearchString);
            var columnCount = 2;
            if (showVanilla)
            {
                columnCount++;
            }
            if (showParamCompare)
            {
                columnCount += auxRows.Count;
            }
            if (showRowCompare)
            {
                columnCount++;
            }

            if (EditorDecorations.ImGuiTableStdColumns("ParamFieldsT", columnCount, false))
            {                
                ImGui.TableSetupScrollFreeze(columnCount, (showColumnHeaders ? 1 : 0) + propertyRowsHeader.Length + (1 + propertyRowsPinned?.Length ?? 0));
                if (showColumnHeaders)
                {
                    if (ImGui.TableNextColumn())
                    {
                        ImGui.Text("Field");
                    }
                    if (ImGui.TableNextColumn())
                    {
                        ImGui.Text("Current");
                    }
                    if (showVanilla && ImGui.TableNextColumn())
                    {
                        ImGui.Text("Vanilla");
                    }
                    foreach ((var name, Param.Row r) in auxRows)
                    {
                        if (showParamCompare && ImGui.TableNextColumn())
                        {
                            ImGui.Text(name);
                        }
                    }
                    if (showRowCompare && ImGui.TableNextColumn())
                    {
                        ImGui.Text(@$"Row {crow.ID}");
                    }
                }

                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(0.8f, 0.8f, 1.0f, 1.0f));
                foreach (ref var field in propertyRowsHeader.AsSpan())
                {
                    PropEditorPropRow(bank, null, ref field, selection, true); //Weird nulling of activeparam to obscure pin options
                }
                ImGui.PopStyleColor(1);
                ImGui.Spacing();

                EditorDecorations.ImguiTableSeparator();
                if (propertyRowsPinned.Length > 0)
                {
                    foreach (ref var field in propertyRowsPinned.AsSpan())
                    {
                        PropEditorPropRow(bank, activeParam, ref field, selection, true);
                    }

                    EditorDecorations.ImguiTableSeparator();
                }

                var lastRowExists = false;
                foreach (ref var field in propertyRows.AsSpan())
                {
                    if (field.isDummy)
                    {
                        if (lastRowExists)
                        {
                            EditorDecorations.ImguiTableSeparator();
                            lastRowExists = false;
                        }
                        continue;
                    }
                    PropEditorPropRow(bank, activeParam, ref field, selection, false);
                    lastRowExists = true;
                }

                ImGui.EndTable();
            }
            ImGui.EndChild();
        }
    }
    public void PropEditorParamRow(ParamBank bank, Param.Row row, Param.Row vrow, List<(string, Param.Row)> auxRows,
        Param.Row crow, ref string propSearchString, string activeParam, bool isActiveView,
        ParamEditorSelectionState selection)
    {
        ParamMetaData meta = ParamMetaData.Get(row.Def);
        var imguiId = 0;
        var showParamCompare = auxRows.Count > 0;
        var showRowCompare = crow != null;
        float fieldDataHeight = meta.CalcCorrectDef != null || meta.SoulCostDef != null ? ImGui.GetWindowHeight() * 3/5f : -1;
        if (ImGui.BeginChild("regularFieldData", new Vector2(-1, fieldDataHeight), ImGuiChildFlags.AlwaysAutoResize))
        {
            PropEditorParamRow_Header(isActiveView, ref propSearchString);

            //ImGui.BeginChild("Param Fields");
            var columnCount = 2;
            if (CFG.Current.Param_ShowVanillaParams)
            {
                columnCount++;
            }

            if (showRowCompare)
            {
                columnCount++;
            }

            if (showParamCompare)
            {
                columnCount += auxRows.Count;
            }
            if (EditorDecorations.ImGuiTableStdColumns("ParamFieldsT", columnCount, false))
            {
                List<string> pinnedFields =
                    _paramEditor._projectSettings.PinnedFields.GetValueOrDefault(activeParam, null);

                ImGui.TableSetupScrollFreeze(columnCount, (showParamCompare ? 3 : 2) + (1 + pinnedFields?.Count ?? 0));
                if (showParamCompare)
                {
                    ImGui.TableNextColumn();
                    if (ImGui.TableNextColumn())
                    {
                        ImGui.Text("Current");
                    }

                    if (CFG.Current.Param_ShowVanillaParams && ImGui.TableNextColumn())
                    {
                        ImGui.Text("Vanilla");
                    }

                    foreach ((var name, Param.Row r) in auxRows)
                    {
                        if (ImGui.TableNextColumn())
                        {
                            ImGui.Text(name);
                        }
                    }
                }
                PropEditorParamRow_RowFields(bank, row, vrow, auxRows, crow, ref imguiId, selection);
                EditorDecorations.ImguiTableSeparator();

                var search = propSearchString;
                List<(PseudoColumn, Param.Column)> cols = UICache.GetCached(_paramEditor, row, "fieldFilter",
                    () => SearchEngine.cell.Search((activeParam, row), search, true, true).Select(x => (x.Item1, x.Item2)).ToList());
                List<(PseudoColumn, Param.Column)> vcols = UICache.GetCached(_paramEditor, vrow, "vFieldFilter",
                    () => cols.Select((x, i) => x.GetAs(ParamBank.VanillaBank.GetParamFromName(activeParam))).ToList());
                List<List<(PseudoColumn, Param.Column)>> auxCols = UICache.GetCached(_paramEditor, auxRows,
                    "auxFieldFilter",
                    () => auxRows.Select((r, i) =>
                        cols.Select((c, j) => c.GetAs(ResDirectory.CurrentGame.AuxProjects[r.Item1].ParamBank.GetParamFromName(activeParam)))
                            .ToList()).ToList());

                if (pinnedFields?.Count > 0)
                {
                    PropEditorParamRow_PinnedFields(pinnedFields, bank, row, vrow, auxRows, crow, cols, vcols, auxCols,
                        ref imguiId, activeParam, selection);
                    EditorDecorations.ImguiTableSeparator();
                }

                PropEditorParamRow_MainFields(meta, bank, row, vrow, auxRows, crow, cols, vcols, auxCols, ref imguiId,
                    activeParam, selection);
                ImGui.EndTable();
            }
            ImGui.EndChild();
        }

        if (meta.CalcCorrectDef != null || meta.SoulCostDef != null)
        {
            EditorDecorations.DrawCalcCorrectGraph(_paramEditor, meta, row);
        }
    }

    // Many parameter options, which may be simplified.
    private void PropEditorPropInfoRow(ParamBank bank, Param.Row row, Param.Row vrow,
        List<(string, Param.Row)> auxRows, Param.Row crow, PropertyInfo prop, string visualName, ref int imguiId,
        ParamEditorSelectionState selection)
    {
        PropEditorPropRow(
            bank,
            prop.GetValue(row),
            crow != null ? prop.GetValue(crow) : null,
            vrow != null ? prop.GetValue(vrow) : null,
            auxRows.Select((r, i) => r.Item2 != null ? prop.GetValue(r.Item2) : null).ToList(),
            ref imguiId,
            "header",
            visualName,
            null,
            prop.PropertyType,
            prop,
            null,
            row,
            null,
            false,
            null,
            selection);
    }

    private void PropEditorPropCellRow(ParamBank bank, Param.Row row, Param.Row crow,
        (PseudoColumn, Param.Column) col, Param.Row vrow, (PseudoColumn, Param.Column) vcol,
        List<(string, Param.Row)> auxRows, List<(PseudoColumn, Param.Column)> auxCols, string fieldOffset,
        ref int imguiId, string activeParam, bool isPinned, ParamEditorSelectionState selection)
    {
        PropEditorPropRow(
            bank,
            row.Get(col),
            crow?.Get(col),
            vcol.IsColumnValid() ? vrow?.Get(vcol) : null,
            auxRows.Select((r, i) => auxCols[i].IsColumnValid() ? r.Item2?.Get(auxCols[i]) : null).ToList(),
            ref imguiId,
            fieldOffset != null ? "0x" + fieldOffset : null, col.Item2.Def.InternalName,
            FieldMetaData.Get(col.Item2.Def),
            col.GetColumnType(),
            typeof(Param.Cell).GetProperty("Value"),
            row[col.Item2],
            row,
            activeParam,
            isPinned,
            col.Item2,
            selection);
    }
    private struct PropertyRowEntry<T>
    {
        internal int index;
        internal bool isDummy;
        internal FieldInfoEntry field;
        internal CellInfoEntry<T> cell;
        internal CellInfoEntry<T> vanilla;
        internal CellInfoEntry<T>[] aux;
        internal CellInfoEntry<T> compare;
    }
    private struct FieldInfoEntry
    {
        internal Param.Column? col;
        internal FieldMetaData meta;
        internal Type propType;
        internal PropertyInfo proprow;
        internal string displayText;
        internal string internalName;
        internal string wiki;
        internal bool isParamRef;
        internal string inactiveParamRefText;
        internal string activeParamRefText;
        internal bool isFMGRef;
        internal string inactiveFmgRefText;
        internal string activeFmgRefText;
        internal bool isEnum;
        internal string enumText;
        internal string extRefText;
        internal string virtualRef;
        internal bool displayBool;
        internal bool isRef;
    }
    private struct CellInfoEntry<T>
    {
        internal bool isNull;
        internal T obj;
        internal Param.Row row; //Still here for legacy reasons
        internal object oldval;
        internal string paramRefText;
        internal string fmgRefText;
        internal string enumText;
        internal bool diffVanilla;
        internal bool conflictOrDiffPrimary;
        internal bool matchDefault;
    }

    private void PropEditorPropRow<T>(ParamBank bank, string activeParam, ref PropertyRowEntry<T> entry, ParamEditorSelectionState selection, bool isPinned)
    {
        ImGui.PushID(entry.index);

        ref FieldInfoEntry field = ref entry.field;

        //ENTRY.FIELD
        if (ImGui.TableNextColumn())
        {
            ImGui.AlignTextToFramePadding();
            if (field.wiki != null)
            {
                string wiki = field.wiki; //Stupid pinning
                if (EditorDecorations.HelpIcon(field.internalName, ref wiki, true))
                {
                    field.meta.Wiki = wiki;
                }

                ImGui.SameLine();
            }
            else
            {
                ImGui.Text(" ");
                ImGui.SameLine();
            }

            ImGui.Selectable("", false, ImGuiSelectableFlags.AllowOverlap);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                ImGui.OpenPopup("ParamRowCommonMenu");
            }

            ImGui.SameLine();

            //Field name
            if (ParamEditorScreen.EditorMode && field.meta != null)
            {
                string altName = field.meta.AltName;
                ImGui.InputText("##editName", ref altName, 128);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    field.meta.AltName = altName == field.internalName ? null : altName;
                }
            }
            else
            {
                ImGui.TextUnformatted(field.displayText);
            }

            //Ref lines
            bool anyItem = false;
            ImGui.BeginGroup();
            //Generify decorations like these
            if (!CFG.Current.Param_HideReferenceRows && field.isParamRef)
            {
                ImGui.PushStyleVarVec2(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));
                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                ImGui.TextUnformatted(@"   <");
                ImGui.SameLine();
                ImGui.TextUnformatted(field.activeParamRefText);
                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
                ImGui.SameLine();
                ImGui.TextUnformatted(field.inactiveParamRefText);
                ImGui.PopStyleColor(1);
                ImGui.SameLine();
                ImGui.TextUnformatted(">");
                ImGui.PopStyleColor(1);
                ImGui.PopStyleVar(1);
                anyItem = true;
            }

            if (!CFG.Current.Param_HideReferenceRows && field.isFMGRef)
            {
                ImGui.PushStyleVarVec2(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));
                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                ImGui.TextUnformatted(@"   [");
                ImGui.SameLine();
                ImGui.TextUnformatted(field.activeFmgRefText);
                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
                ImGui.SameLine();
                ImGui.TextUnformatted(field.inactiveFmgRefText);
                ImGui.PopStyleColor(1);
                ImGui.SameLine();
                ImGui.TextUnformatted("]");
                ImGui.PopStyleColor(1);
                ImGui.PopStyleVar(1);
                anyItem = true;
            }

            if (!CFG.Current.Param_HideEnums && field.isEnum)
            {
                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                ImGui.TextUnformatted($@"   {field.enumText}");
                ImGui.PopStyleColor(1);
                anyItem = true;
            }

            ImGui.EndGroup();
            if (anyItem && ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                ImGui.OpenPopup("ParamRowCommonMenu");
            }
        }

        object newval = null;
        //ENTRY.CELL
        ref CellInfoEntry<T> cell = ref entry.cell;
        if (ImGui.TableNextColumn() && !cell.isNull)
        {
            if (cell.conflictOrDiffPrimary)
            {
                ImGui.PushStyleColorVec4(ImGuiCol.FrameBg, new Vector4(0.25f, 0.2f, 0.2f, 1.0f));
            }
            else if (cell.diffVanilla)
            {
                ImGui.PushStyleColorVec4(ImGuiCol.FrameBg, new Vector4(0.2f, 0.22f, 0.2f, 1.0f));
            }

            if (field.isRef)
            {
                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(1.0f, 0.5f, 1.0f, 1.0f));
            }
            else if (cell.matchDefault)
            {
                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(0.75f, 0.75f, 0.75f, 1.0f));
            }
            ParamEditorCommon.PropertyField(field.propType, cell.oldval, ref newval, field.displayBool);
            if (field.isRef || cell.matchDefault) //if diffVanilla, remove styling later
            {
                ImGui.PopStyleColor(1);
            }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                ImGui.OpenPopup("ParamRowCommonMenu");
            }

            ColumnReferences(ref field, ref cell, bank, true);

            if (cell.conflictOrDiffPrimary || cell.diffVanilla)
            {
                ImGui.PopStyleColor(1);
            }
        }

        if (cell.conflictOrDiffPrimary)
        {
            ImGui.PushStyleColorVec4(ImGuiCol.FrameBg, new Vector4(0.25f, 0.2f, 0.2f, 1.0f));
        }

        //VANILLA
        ref CellInfoEntry<T> vanilla = ref entry.vanilla;
        ImGui.PushStyleColorVec4(ImGuiCol.FrameBg, new Vector4(0.180f, 0.180f, 0.196f, 1.0f));
        ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(0.9f, 0.9f, 0.9f, 1.0f));

        if (CFG.Current.Param_ShowVanillaParams && ImGui.TableNextColumn() && !vanilla.isNull)
        {
            AdditionalColumnValue(ref field, ref vanilla, bank, @$"##colvalvanilla");
        }

        //AUX
        for (var i = 0; i < entry.aux.Length; i++)
        {
            ref CellInfoEntry<T> aux = ref entry.aux[i];
            if (ImGui.TableNextColumn() && !aux.isNull)
            {
                if (!cell.conflictOrDiffPrimary && aux.diffVanilla)
                {
                    ImGui.PushStyleColorVec4(ImGuiCol.FrameBg, new Vector4(0.2f, 0.2f, 0.35f, 1.0f));
                }
                AdditionalColumnValue(ref field, ref aux, bank, @$"##colval{i}");
                if (!cell.conflictOrDiffPrimary && aux.diffVanilla)
                {
                    ImGui.PopStyleColor(1);
                }
            }
        }
        if (cell.conflictOrDiffPrimary)
        {
            ImGui.PopStyleColor(1);
        }

        //COMPARE
        ref CellInfoEntry<T> compare = ref entry.compare;
        if (!compare.isNull && ImGui.TableNextColumn())
        {
            if (compare.conflictOrDiffPrimary)
            {
                ImGui.PushStyleColorVec4(ImGuiCol.FrameBg, new Vector4(0.2f, 0.2f, 0.35f, 1.0f));
            }
            AdditionalColumnValue(ref field, ref compare, bank, @$"##colvalcompRow");
            if (compare.conflictOrDiffPrimary)
            {
                ImGui.PopStyleColor(1);
            }
        }

        ImGui.PopStyleColor(2);

        if (ImGui.BeginPopup("ParamRowCommonMenu"))
        {
            PropertyRowNameContextMenuItems(bank, field.internalName, field.meta, activeParam, activeParam != null,
                isPinned, field.col, selection, field.propType, field.wiki, cell.oldval);
            PropertyRowValueContextMenuItems(bank, cell.row, field.internalName, field.meta?.VirtualRef, field.meta?.ExtRefs, cell.oldval, ref newval,
                field.meta?.RefTypes, field.meta?.FmgRef, field.meta?.EnumType);
            ImGui.EndPopup();
        }

        //Note here newval isn't passed in. This is because ParamEditorCommon actually caches it
        //This saves it from reversion if this edit is triggered before the imgui for this field is called
        var committed = ParamEditorCommon.UpdateProperty(ContextActionManager, cell.obj, field.proprow, cell.oldval);
        if (committed && ParamBank.VanillaBank.IsLoaded)
        {
            Locator.ActiveProject.ParamDiffBank.RefreshParamRowDiffs(cell.row, activeParam);
        }

        ImGui.PopID();
    }

    private void AdditionalColumnValue<T>(ref FieldInfoEntry field, ref CellInfoEntry<T> cell, ParamBank bank, string imguiElemName)
    {
        //Real case any more?
        if (cell.oldval == null)
        {
            ImGui.TextUnformatted("");
            return;
        }
        string value = cell.oldval.ToParamEditorString();
        ImGui.InputText(imguiElemName, ref value, 256, ImGuiInputTextFlags.ReadOnly);
        ColumnReferences(ref field, ref cell, bank, false);
    }

    private void ColumnReferences<T>(ref FieldInfoEntry field, ref CellInfoEntry<T> cell, ParamBank bank, bool allowClickForPopup)
    {
        ImGui.BeginGroup();
        bool anyItem = false;
        if (!CFG.Current.Param_HideReferenceRows && field.isParamRef)
        {
            if (!string.IsNullOrEmpty(cell.paramRefText)) //cache this work too? finetune cell.paramRefText to be nullable?
            {
                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(1.0f, 0.5f, 0.5f, 1.0f));
                ImGui.TextUnformatted(cell.paramRefText);
            }
            else
            {
                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                ImGui.TextUnformatted("___");
            }
            ImGui.PopStyleColor(1);
            anyItem = true;
        }

        if (!CFG.Current.Param_HideReferenceRows && field.isFMGRef)
        {
            if (!string.IsNullOrEmpty(cell.fmgRefText)) //cache this work too? finetune cell.fmgRefText to be nullable?
            {
                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(1.0f, 0.5f, 0.5f, 1.0f));
                ImGui.TextUnformatted(cell.fmgRefText);
            }
            else
            {
                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                ImGui.TextUnformatted("%null%");
            }
            ImGui.PopStyleColor(1);
            anyItem = true;
        }

        if (!CFG.Current.Param_HideEnums && field.isEnum)
        {
            if (cell.enumText != null)
            {
                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(1.0f, 0.5f, 0.5f, 1.0f));
                ImGui.TextUnformatted(cell.enumText);
            }
            else
            {
                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                ImGui.TextUnformatted("Not Enumerated");
            }
            ImGui.PopStyleColor(1);
            anyItem = true;
        }
        ImGui.EndGroup();
        if (anyItem)
        {
            //Todo avoid dereferencing these before determining a click has occurred
            EditorDecorations.ParamRefEnumQuickLink(bank, cell.oldval, field.meta?.RefTypes, cell.row, field.meta?.FmgRef, field.meta?.EnumType);
            if (allowClickForPopup && ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                ImGui.OpenPopup("ParamRowCommonMenu");
            }
        }
    }

    private void PropEditorPropRow(ParamBank bank, object oldval, object compareval, object vanillaval,
        List<object> auxVals, ref int imguiId, string fieldOffset, string internalName, FieldMetaData cellMeta,
        Type propType, PropertyInfo proprow, Param.Cell? nullableCell, Param.Row row, string activeParam,
        bool isPinned, Param.Column? col, ParamEditorSelectionState selection)
    {
        var Wiki = cellMeta?.Wiki;

        List<ParamRef> RefTypes = cellMeta?.RefTypes;
        List<FMGRef> FmgRef = cellMeta?.FmgRef;
        List<ExtRef> ExtRefs = cellMeta?.ExtRefs;
        var VirtualRef = cellMeta?.VirtualRef;

        ParamEnum Enum = cellMeta?.EnumType;
        var IsBool = cellMeta?.IsBool ?? false;

        var displayRefTypes = !CFG.Current.Param_HideReferenceRows && RefTypes != null;
        var displayFmgRef = !CFG.Current.Param_HideReferenceRows && FmgRef != null;
        var displayEnum = !CFG.Current.Param_HideEnums && Enum != null;

        object newval = null;

        ImGui.PushID(imguiId);
        if (ImGui.TableNextColumn())
        {
            ImGui.AlignTextToFramePadding();
            if (Wiki != null)
            {
                if (EditorDecorations.HelpIcon(internalName, ref Wiki, true))
                {
                    cellMeta.Wiki = Wiki;
                }

                ImGui.SameLine();
            }
            else
            {
                ImGui.Text(" ");
                ImGui.SameLine();
            }

            ImGui.Selectable("", false, ImGuiSelectableFlags.AllowOverlap);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                ImGui.OpenPopup("ParamRowCommonMenu");
            }

            ImGui.SameLine();

            PropertyRowName(fieldOffset, ref internalName, cellMeta);

            if (displayRefTypes || displayFmgRef || displayEnum)
            {
                ImGui.BeginGroup();
                if (displayRefTypes)
                {
                    EditorDecorations.ParamRefText(RefTypes, row);
                }

                if (displayFmgRef)
                {
                    EditorDecorations.FmgRefText(FmgRef, row);
                }

                if (displayEnum)
                {
                    EditorDecorations.EnumNameText(Enum);
                }

                ImGui.EndGroup();
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    ImGui.OpenPopup("ParamRowCommonMenu");
                }
            }
        }

        var diffVanilla = ParamUtils.IsValueDiff(ref oldval, ref vanillaval, propType);
        var diffCompare = ParamUtils.IsValueDiff(ref oldval, ref compareval, propType);
        List<bool> diffAuxVanilla =
            auxVals.Select((o, i) => ParamUtils.IsValueDiff(ref o, ref vanillaval, propType)).ToList();
        List<bool> diffAuxPrimaryAndVanilla = auxVals.Select((o, i) =>
            ParamUtils.IsValueDiff(ref o, ref oldval, propType) &&
            ParamUtils.IsValueDiff(ref o, ref vanillaval, propType)).ToList();
        var count = diffAuxPrimaryAndVanilla.Where(x => x).Count();
        var conflict = (diffVanilla ? 1 : 0) + diffAuxPrimaryAndVanilla.Where(x => x).Count() > 1;

        var matchDefault = nullableCell?.Def.Default != null && nullableCell.Value.Def.Default.Equals(oldval);
        var isRef = (CFG.Current.Param_HideReferenceRows == false && (RefTypes != null || FmgRef != null)) ||
                    (CFG.Current.Param_HideEnums == false && Enum != null) || VirtualRef != null ||
                    ExtRefs != null;

        if (ImGui.TableNextColumn())
        {
            if (conflict)
            {
                ImGui.PushStyleColorVec4(ImGuiCol.FrameBg, new Vector4(0.25f, 0.2f, 0.2f, 1.0f));
            }
            else if (diffVanilla)
            {
                ImGui.PushStyleColorVec4(ImGuiCol.FrameBg, new Vector4(0.2f, 0.22f, 0.2f, 1.0f));
            }

            if (isRef)
            {
                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(1.0f, 0.5f, 1.0f, 1.0f));
            }
            else if (matchDefault)
            {
                ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(0.75f, 0.75f, 0.75f, 1.0f));
            }

            // Property Editor UI
            ParamEditorCommon.PropertyField(propType, oldval, ref newval, IsBool);

            if (isRef || matchDefault) //if diffVanilla, remove styling later
            {
                ImGui.PopStyleColor(1);
            }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                ImGui.OpenPopup("ParamRowCommonMenu");
            }

            if (displayRefTypes || displayFmgRef || displayEnum)
            {
                ImGui.BeginGroup();
                if (displayRefTypes)
                {
                    EditorDecorations.ParamRefsSelectables(bank, RefTypes, row, oldval);
                }

                if (displayFmgRef)
                {
                    EditorDecorations.FmgRefSelectable(_paramEditor, FmgRef, row, oldval);
                }

                if (displayEnum)
                {
                    EditorDecorations.EnumValueText(Enum.values, oldval.ToString());
                }

                ImGui.EndGroup();
                EditorDecorations.ParamRefEnumQuickLink(bank, oldval, RefTypes, row, FmgRef, Enum);
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    ImGui.OpenPopup("ParamRowCommonMenu");
                }
            }

            if (conflict || diffVanilla)
            {
                ImGui.PopStyleColor(1);
            }
        }

        ImGui.PushStyleColorVec4(ImGuiCol.FrameBg, new Vector4(0.180f, 0.180f, 0.196f, 1.0f));
        ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(0.9f, 0.9f, 0.9f, 1.0f));
        if (conflict)
        {
            ImGui.PushStyleColorVec4(ImGuiCol.FrameBg, new Vector4(0.25f, 0.2f, 0.2f, 1.0f));
        }

        if (CFG.Current.Param_ShowVanillaParams && ImGui.TableNextColumn())
        {
            AdditionalColumnValue(vanillaval, propType, bank, RefTypes, FmgRef, row, Enum, "vanilla");
        }

        for (var i = 0; i < auxVals.Count; i++)
        {
            if (ImGui.TableNextColumn())
            {
                if (!conflict && diffAuxVanilla[i])
                {
                    ImGui.PushStyleColorVec4(ImGuiCol.FrameBg, new Vector4(0.2f, 0.2f, 0.35f, 1.0f));
                }

                AdditionalColumnValue(auxVals[i], propType, bank, RefTypes, FmgRef, row, Enum, i.ToString());
                if (!conflict && diffAuxVanilla[i])
                {
                    ImGui.PopStyleColor(1);
                }
            }
        }

        if (conflict)
        {
            ImGui.PopStyleColor(1);
        }

        if (compareval != null && ImGui.TableNextColumn())
        {
            if (diffCompare)
            {
                ImGui.PushStyleColorVec4(ImGuiCol.FrameBg, new Vector4(0.2f, 0.2f, 0.35f, 1.0f));
            }

            AdditionalColumnValue(compareval, propType, bank, RefTypes, FmgRef, row, Enum, "compRow");
            if (diffCompare)
            {
                ImGui.PopStyleColor(1);
            }
        }

        ImGui.PopStyleColor(2);
        if (ImGui.BeginPopup("ParamRowCommonMenu"))
        {
            PropertyRowNameContextMenuItems(bank, internalName, cellMeta, activeParam, activeParam != null,
                isPinned, col, selection, propType, Wiki, oldval);
            PropertyRowValueContextMenuItems(bank, row, internalName, VirtualRef, ExtRefs, oldval, ref newval,
                RefTypes, FmgRef, Enum);
            ImGui.EndPopup();
        }

        var committed = ParamEditorCommon.UpdateProperty(ContextActionManager,
            nullableCell != null ? nullableCell : row, proprow, oldval);
        if (committed && ParamBank.VanillaBank.IsLoaded)
        {
            Locator.ActiveProject.ParamDiffBank.RefreshParamRowDiffs(row, activeParam);
        }

        ImGui.PopID();
        imguiId++;
    }

    private void AdditionalColumnValue(object colVal, Type propType, ParamBank bank, List<ParamRef> RefTypes,
        List<FMGRef> FmgRef, Param.Row context, ParamEnum Enum, string imguiSuffix)
    {
        if (colVal == null)
        {
            ImGui.TextUnformatted("");
        }
        else
        {
            string value;
            if (propType == typeof(byte[]))
            {
                value = ParamUtils.Dummy8Write((byte[])colVal);
            }
            else
            {
                value = colVal.ToString();
            }

            ImGui.InputText("##colval" + imguiSuffix, ref value, 256, ImGuiInputTextFlags.ReadOnly);
            if (CFG.Current.Param_HideReferenceRows == false && RefTypes != null)
            {
                EditorDecorations.ParamRefsSelectables(bank, RefTypes, context, colVal);
            }

            if (CFG.Current.Param_HideReferenceRows == false && FmgRef != null)
            {
                EditorDecorations.FmgRefSelectable(_paramEditor, FmgRef, context, colVal);
            }

            if (CFG.Current.Param_HideEnums == false && Enum != null)
            {
                EditorDecorations.EnumValueText(Enum.values, colVal.ToString());
            }
        }
    }

    private static string OffsetTextOfColumn(Param.Column? col)
    {
        if (col == null)
        {
            return "";
        }

        if (col.Def.BitSize == -1)
        {
            return $"0x{col.GetByteOffset().ToString("x")} ";
        }

        var offS = col.GetBitOffset();
        if (col.Def.BitSize == 1)
        {
            return $"0x{col.GetByteOffset().ToString("x")} [{offS}] ";
        }

        return $"0x{col.GetByteOffset().ToString("x")} [{offS}-{offS + col.Def.BitSize - 1}] ";
    }

    private static void PropertyRowName(string fieldOffset, ref string internalName, FieldMetaData cellMeta)
    {
        var altName = cellMeta?.AltName;
        if (cellMeta != null && ParamEditorScreen.EditorMode)
        {
            var editName = !string.IsNullOrWhiteSpace(altName) ? altName : internalName;
            ImGui.InputText("##editName", ref editName, 128);
            if (editName.Equals(internalName) || editName.Equals(""))
            {
                cellMeta.AltName = null;
            }
            else
            {
                cellMeta.AltName = editName;
            }
        }
        else
        {
            var printedName = internalName;
            if (!string.IsNullOrWhiteSpace(altName))
            {
                if (CFG.Current.Param_MakeMetaNamesPrimary)
                {
                    printedName = altName;
                    if (CFG.Current.Param_ShowSecondaryNames)
                    {
                        printedName = $"{printedName} ({internalName})";
                    }
                }
                else if (CFG.Current.Param_ShowSecondaryNames)
                {
                    printedName = $"{printedName} ({altName})";
                }
            }

            if (fieldOffset != null && CFG.Current.Param_ShowFieldOffsets)
            {
                printedName = $"{fieldOffset} {printedName}";
            }

            ImGui.TextUnformatted(printedName);
        }
    }

    private void PropertyRowNameContextMenuItems(ParamBank bank, string internalName, FieldMetaData cellMeta,
        string activeParam, bool showPinOptions, bool isPinned, Param.Column col,
        ParamEditorSelectionState selection, Type propType, string Wiki, dynamic oldval)
    {
        var scale = MapStudioNew.GetUIScale();
        var altName = cellMeta?.AltName;

        ImGui.PushStyleVarVec2(ImGuiStyleVar.ItemSpacing, new Vector2(0f, 10f) * scale);

        if (col != null)
        {
            EditorDecorations.ImGui_DisplayPropertyInfo(propType, internalName, altName, col.Def.ArrayLength, col.Def.BitSize);
            if (Wiki != null)
            {
                ImGui.TextColored(new Vector4(.4f, .7f, 1f, 1f), $"{Wiki}");
            }
            else
            {
                ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 0.7f),
                    "Info regarding this field has not been written.");
            }
        }
        else
        {
            // Headers
            ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.4f, 1.0f), Utils.ImGuiEscape(internalName, "", true));
        }

        ImGui.Separator();

        if (showPinOptions)
        {
            if (ImGui.MenuItem(isPinned ? "Unpin " : "Pin " + internalName))
            {
                if (!_paramEditor._projectSettings.PinnedFields.ContainsKey(activeParam))
                {
                    _paramEditor._projectSettings.PinnedFields.Add(activeParam, new List<string>());
                }

                List<string> pinned = _paramEditor._projectSettings.PinnedFields[activeParam];
                if (isPinned)
                {
                    pinned.Remove(internalName);
                }
                else if (!pinned.Contains(internalName))
                {
                    pinned.Add(internalName);
                }
            }
            if (isPinned)
            {
                EditorDecorations.PinListReorderOptions(_paramEditor._projectSettings.PinnedFields[activeParam], internalName);
            }
            ImGui.Separator();
        }

        if (ImGui.MenuItem("Add to Searchbar"))
        {
            if (col != null)
            {
                EditorCommandQueue.AddCommand($@"param/search/prop {internalName.Replace(" ", "\\s")} ");
            }
            else
            {
                // Headers
                EditorCommandQueue.AddCommand($@"param/search/{internalName.Replace(" ", "\\s")} ");
            }
        }

        if (col != null && ImGui.MenuItem("Compare field"))
        {
            selection.SetCompareCol(col);
        }

        if (ImGui.Selectable("View value distribution in selected rows..."))
        {
            EditorCommandQueue.AddCommand($@"param/menu/distributionPopup/{internalName}");
        }

        if (ParamEditorScreen.EditorMode && cellMeta != null)
        {
            if (ImGui.BeginMenu("Add Reference"))
            {
                foreach (var p in bank.Params.Keys)
                {
                    if (ImGui.MenuItem(p + "##add" + p))
                    {
                        if (cellMeta.RefTypes == null)
                        {
                            cellMeta.RefTypes = new List<ParamRef>();
                        }

                        cellMeta.RefTypes.Add(new ParamRef(p));
                    }
                }

                ImGui.EndMenu();
            }

            if (cellMeta.RefTypes != null && ImGui.BeginMenu("Remove Reference"))
            {
                foreach (ParamRef p in cellMeta.RefTypes)
                {
                    if (ImGui.MenuItem(p.param + "##remove" + p.param))
                    {
                        cellMeta.RefTypes.Remove(p);
                        if (cellMeta.RefTypes.Count == 0)
                        {
                            cellMeta.RefTypes = null;
                        }

                        break;
                    }
                }

                ImGui.EndMenu();
            }

            if (ImGui.MenuItem(cellMeta.IsBool ? "Remove bool toggle" : "Add bool toggle"))
            {
                cellMeta.IsBool = !cellMeta.IsBool;
            }

            if (cellMeta.Wiki == null && ImGui.MenuItem("Add wiki..."))
            {
                cellMeta.Wiki = "Empty wiki...";
            }

            if (cellMeta.Wiki != null && ImGui.MenuItem("Remove wiki"))
            {
                cellMeta.Wiki = null;
            }
        }

        ImGui.PopStyleVar(1);
    }

    private void PropertyRowValueContextMenuItems(ParamBank bank, Param.Row row, string internalName,
        string VirtualRef, List<ExtRef> ExtRefs, dynamic oldval, ref object newval, List<ParamRef> RefTypes,
        List<FMGRef> FmgRef, ParamEnum Enum)
    {
        if (VirtualRef != null || ExtRefs != null)
        {
            ImGui.Separator();
            ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(1.0f, 0.75f, 1.0f, 1.0f));
            EditorDecorations.VirtualParamRefSelectables(bank, VirtualRef, oldval, row, internalName, ExtRefs,
                _paramEditor);
            ImGui.PopStyleColor(1);
        }

        if (RefTypes != null || FmgRef != null || Enum != null)
        {
            ImGui.Separator();
            ImGui.PushStyleColorVec4(ImGuiCol.Text, new Vector4(1.0f, 0.75f, 0.75f, 1.0f));
            if (EditorDecorations.ParamRefEnumContextMenuItems(bank, oldval, ref newval, RefTypes, row, FmgRef,
                    Enum, ContextActionManager))
            {
                ParamEditorCommon.SetLastPropertyManual(newval);
            }

            ImGui.PopStyleColor(1);
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Mass edit", ImGuiTreeNodeFlags.SpanFullWidth))
        {
            ImGui.Separator();
            if (ImGui.Selectable("Manually..."))
            {
                EditorCommandQueue.AddCommand(
                    $@"param/menu/massEditRegex/selection: {Regex.Escape(internalName)}: ");
            }

            if (ImGui.Selectable("Reset to vanilla..."))
            {
                EditorCommandQueue.AddCommand(
                    $@"param/menu/massEditRegex/selection && !added: {Regex.Escape(internalName)}: = vanilla;");
            }

            ImGui.Separator();
            var res = AutoFill.MassEditOpAutoFill();
            if (res != null)
            {
                EditorCommandQueue.AddCommand(
                    $@"param/menu/massEditRegex/selection: {Regex.Escape(internalName)}: " + res);
            }
        }
    }
}
