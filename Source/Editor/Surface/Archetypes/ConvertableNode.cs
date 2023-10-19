﻿using System;
using System.Linq;
using FlaxEditor.GUI;
using FlaxEditor.Scripting;
using FlaxEditor.Surface.Elements;
using FlaxEditor.Surface.Undo;
using FlaxEngine;

namespace FlaxEditor.Surface.Archetypes;

/// <summary>
/// 
/// </summary>
internal class ConvertableNode : SurfaceNode
{ 
    private ScriptType _type;
    private Func<object[], object> _convertFunction;
    
    /// <inheritdoc />
    public ConvertableNode(uint id, VisjectSurfaceContext context, NodeArchetype nodeArch, GroupArchetype groupArch, ScriptType type, Func<object[], object> convertFunction = null) 
    : base(id, context, nodeArch, groupArch)
    {
        _type = type;
        _convertFunction = convertFunction;
    }

    /// <inheritdoc />
    public override void OnShowSecondaryContextMenu(FlaxEditor.GUI.ContextMenu.ContextMenu menu, Float2 location)
    {
        base.OnShowSecondaryContextMenu(menu, location);
        
        menu.AddSeparator();
        menu.AddButton("Convert to Parameter", OnConvertToParameter);
    }

    private void OnConvertToParameter()
    {
        if(Surface.Owner is not IVisjectSurfaceWindow window)
            throw new Exception("Surface owner is not a Visject Surface Window");
        
        Asset asset = Surface.Owner.SurfaceAsset;
        if (asset == null || !asset.IsLoaded)
        {
            Editor.LogError("Asset is null or not loaded");
            return;   
        }

        // Add parameter to editor
        var paramIndex = Surface.Parameters.Count;
        object initValue = _convertFunction == null ? Values[0] : _convertFunction.Invoke(Values);
        var paramAction = new AddRemoveParamAction
        {
            Window = window,
            IsAdd = true,
            Name = Utilities.Utils.IncrementNameNumber("New parameter", x => OnParameterRenameValidate(null, x)),
            Type = _type,
            Index = paramIndex,
            InitValue = initValue,
        };
        paramAction.Do();

        var parameterGuid = Surface.Parameters[paramIndex].ID;
        
        bool undoEnabled = Surface.Undo.Enabled;
        Surface.Undo.Enabled = false;
        SurfaceNode node = Surface.Context.SpawnNode(6, 1, this.Location, new object[] {parameterGuid});
        Surface.Undo.Enabled = undoEnabled;

        if (node is not Parameters.SurfaceNodeParamsGet getNode)
            throw new Exception("Node is not a ParamsGet node!");
        
        // Recreate connections of constant node
        // Constant nodes and parameter nodes should have the same ports, so we can just iterate through the connections
        var boxes = GetBoxes();
        for (int i = 0;i < boxes.Count; i++)
        {
            Box box = boxes[i];
            if (!box.HasAnyConnection)
                continue;

            if (!getNode.TryGetBox(i, out Box paramBox))
                continue;
            
            // Iterating backwards, because the CreateConnection method deletes entries from the box connections when target box IsSingle is set to true
            for (int k = box.Connections.Count-1; k >= 0; k--)
            {
                Box connectedBox = box.Connections[k];
                paramBox.CreateConnection(connectedBox);
            }
        }
        
        var spawnNodeAction = new AddRemoveNodeAction(getNode, true);
        var removeConstantAction = new AddRemoveNodeAction(this, false);
        
        Surface.AddBatchedUndoAction(new MultiUndoAction(paramAction, spawnNodeAction, removeConstantAction));
        removeConstantAction.Do();
    }
    
    private bool OnParameterRenameValidate(RenamePopup popup, string value)
    {
        if(Surface.Owner is not IVisjectSurfaceWindow window)
            throw new Exception("Surface owner is not a Visject Surface Window");
        return !string.IsNullOrWhiteSpace(value) && window.VisjectSurface.Parameters.All(x => x.Name != value);
    }
}
