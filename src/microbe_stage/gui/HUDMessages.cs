﻿using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
///   Shows message lines on screen that fade after some time to give the player some gameplay related messages
/// </summary>
public partial class HUDMessages : VBoxContainer
{
    /// <summary>
    ///   When this is true any new messages are added *above* existing messages. Otherwise, new messages appear below
    ///   existing messages.
    /// </summary>
    [Export]
    public bool OrderMessagesBottomUp;

#pragma warning disable CA2213
    [Export]
    public LabelSettings MessageFontSettings = null!;
#pragma warning restore CA2213

    [Export]
    public int MaxShownMessages = 4;

    [Export]
    public float MaxDisplayTimeBeforeForceFade = 60;

    /// <summary>
    ///   The alpha value that is reached by the midway point of a message
    /// </summary>
    [Export]
    public float MidwayFadeValue = 0.65f;

    private readonly List<(IHUDMessage Message, Label Displayer)> hudMessages = new();

    /*
    /// <summary>
    ///   Temporary memory used for some operations on <see cref="hudMessages"/>
    /// </summary>
    private readonly List<(IHUDMessage Message, Label Displayer)> tempMessages = new();*/

    private readonly Stack<LabelSettings> freeToUseLabelSettings = new();

    private Color messageShadowColour = new(0, 0, 0, 0.7f);
    private Color baseMessageColour = new(1, 1, 1);

    private string multipliedMessageTemplate = string.Empty;

    private double extraTime;

    public override void _Ready()
    {
        multipliedMessageTemplate = Localization.Translate("HUD_MESSAGE_MULTIPLE");

        if (MaxShownMessages < 1)
        {
            GD.PrintErr($"{nameof(MaxShownMessages)} needs to be at least one");
            MaxShownMessages = 1;
        }

        messageShadowColour = MessageFontSettings.ShadowColor;
        baseMessageColour = MessageFontSettings.FontColor;
        baseMessageColour.A = 1;
    }

    public override void _Process(double delta)
    {
        bool clean = false;

        if (extraTime > 0)
        {
            delta += extraTime;
            extraTime = 0;
        }

        var convertedDelta = (float)delta;

        foreach (var (message, displayer) in hudMessages)
        {
            message.TimeRemaining -= convertedDelta;

            if (message.TimeRemaining < 0)
            {
                OnDestroyLabel(displayer);

                clean = true;
                continue;
            }

            message.TotalDisplayedTime += convertedDelta;

            // Update fade
            // TODO: should different types of messages (more urgent?) have different colours
            var alpha = CalculateMessageAlpha(message.TimeRemaining, message.OriginalTimeRemaining);
            displayer.LabelSettings.FontColor = new Color(baseMessageColour, alpha);
            displayer.LabelSettings.ShadowColor = new Color(messageShadowColour, messageShadowColour.A * alpha);
        }

        if (clean)
            hudMessages.RemoveAll(t => t.Message.TimeRemaining < 0);
    }

    public void ShowMessage(IHUDMessage message)
    {
        // First combine to an existing message if possible
        foreach (var (existingMessage, existingLabel) in hudMessages)
        {
            if (existingMessage.IsSameMessage(message))
            {
                // Limit to only combining with not super long-lived messages. This ensures that all messages will
                // eventually disappear
                if (existingMessage.TotalDisplayedTime > MaxDisplayTimeBeforeForceFade)
                    continue;

                existingMessage.UpdateFromOtherMessage(message);
                existingLabel.Text = TextForMessage(existingMessage);

                // Reset the fade time for the message that appeared again
                existingMessage.TimeRemaining = existingMessage.OriginalTimeRemaining;

                return;
            }
        }

        // Can't combine, need to add a new label to display this
        var label = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            Text = TextForMessage(message),
            LabelSettings = GetLabelSettings(),
        };

        AddChild(label);

        if (OrderMessagesBottomUp)
        {
            MoveChild(label, 0);
        }

        message.OriginalTimeRemaining = TimeToFadeFromDuration(message.Duration);
        message.TimeRemaining = message.OriginalTimeRemaining;

        hudMessages.Add((message, label));

        // If there's too many messages, remove the one with the least time remaining
        while (hudMessages.Count > MaxShownMessages)
        {
            var toRemove = hudMessages.MinBy(m => m.Message.TimeRemaining);

            OnDestroyLabel(toRemove.Displayer);

            if (!hudMessages.Remove(toRemove))
                throw new Exception("Expected list item removal failed");
        }
    }

    public void ShowMessage(string simpleMessage, DisplayDuration duration = DisplayDuration.Normal)
    {
        ShowMessage(new SimpleHUDMessage(simpleMessage, duration));
    }

    /// <summary>
    ///   Causes extra time for fading to elapse
    /// </summary>
    public void PassExtraTime(float extraDelta)
    {
        extraTime += extraDelta;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var labelSetting in freeToUseLabelSettings)
            {
                labelSetting.Dispose();
            }

            freeToUseLabelSettings.Clear();
        }

        base.Dispose(disposing);
    }

    private static float TimeToFadeFromDuration(DisplayDuration duration)
    {
        return duration switch
        {
            DisplayDuration.Short => 2,
            DisplayDuration.Normal => 4,
            DisplayDuration.Long => 12,
            DisplayDuration.ExtraLong => 25,
            _ => throw new ArgumentOutOfRangeException(nameof(duration), duration, null),
        };
    }

    private void OnDestroyLabel(Label displayer)
    {
        // Store the label settings for later re-use
        freeToUseLabelSettings.Push(displayer.LabelSettings);

        displayer.Visible = false;

        // Don't let the previous label remove the label settings
        displayer.LabelSettings = null;
        displayer.QueueFree();
    }

    private LabelSettings GetLabelSettings()
    {
        if (freeToUseLabelSettings.TryPop(out var result))
            return result;

        var settings = (LabelSettings)MessageFontSettings.Duplicate(false);
        settings.FontColor = baseMessageColour;

        return settings;
    }

    private string TextForMessage(IHUDMessage message)
    {
        if (message.Multiplier < 2)
            return message.ToString();

        return multipliedMessageTemplate.FormatSafe(message.ToString(), message.Multiplier);
    }

    private float CalculateMessageAlpha(float timeLeft, float originalTime)
    {
        // First half fades slower, and then again fade fast
        float halfway = originalTime * 0.5f;
        if (timeLeft >= halfway)
        {
            return MidwayFadeValue + (1 - MidwayFadeValue) *
                (timeLeft - originalTime * 0.5f) / originalTime;
        }

        return MidwayFadeValue * (timeLeft / halfway);
    }
}
