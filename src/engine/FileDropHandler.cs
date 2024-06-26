﻿using System;
using Godot;

/// <summary>
///   Handles signal from Godot when files are dragged and dropped onto the game window
/// </summary>
[GodotAutoload]
public partial class FileDropHandler : Node
{
    public override void _Ready()
    {
        if (Engine.IsEditorHint())
            return;

        GetTree().Root.Connect(Window.SignalName.FilesDropped, new Callable(this, nameof(OnFilesDropped)));
    }

    private void OnFilesDropped(Variant files)
    {
        if (files.VariantType != Variant.Type.PackedStringArray)
        {
            GD.PrintErr("Unknown type of data in files dropped callback: " + files.VariantType);
            return;
        }

        foreach (var file in files.AsStringArray())
        {
            GD.Print("Detected file drop \"", file, "\"");
            HandleFileDrop(file);
        }
    }

    private void HandleFileDrop(string file)
    {
        // For now just the load save functionality is done, but in the future this might be extended to allow
        // other code to dynamically register listeners here

        if (file.EndsWith(Constants.SAVE_EXTENSION, StringComparison.InvariantCulture))
        {
            if (!file.StartsWith(Constants.EXPLICIT_PATH_PREFIX, StringComparison.InvariantCulture))
                file = Constants.EXPLICIT_PATH_PREFIX + file;

            // TODO: could add a dialog to ask if the save should be loaded now or copied to the saves folder
            HandleLoadSaveFromDrop(file);
        }
        else
        {
            GD.Print("Unknown file type to handle on drop");
        }
    }

    private void HandleLoadSaveFromDrop(string file)
    {
        GD.Print("Trying to load dropped save file...");

        // TODO: would be nice to have some kind of popup box showing the errors from here to the user
        try
        {
            var info = Save.LoadJustInfoFromSave(file);

            if (info.Type == SaveInformation.SaveType.Invalid)
            {
                GD.PrintErr("Given file (", file, ") is not a valid Thrive save");
                return;
            }

            if (SaveHelper.IsKnownIncompatible(info.ThriveVersion))
            {
                GD.Print("Dropped save file is known incompatible, not loading it");
                return;
            }

            if (info.ThriveVersion != Constants.Version)
            {
                // TODO: would be nice to show a confirmation dialog here
                GD.Print("The dropped save version is not exactly the same as current game version");
            }

            SaveHelper.LoadSave(file);
        }
        catch (Exception e)
        {
            GD.PrintErr("Exception while trying to load dropped save: ", e);
        }
    }
}
