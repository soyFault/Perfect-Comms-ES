using System;
using BepInEx.Configuration;

namespace VoiceChatPlugin.VoiceChat;

public abstract class OptionHolder
{
    /// <summary>
    /// Stable identity of the option ("Section.Key" for config-backed options, the composed
    /// "modId.optionKey" for third-party ones). Never localized; use it instead of <see cref="Label"/>
    /// whenever the option has to be recognized or compared.
    /// </summary>
    public string Id { get; protected init; } = "";
    // Text is resolved on read, so it follows the game language instead of freezing at plugin Load.
    internal Loc.LocText LabelText { get; init; } = Loc.LocText.Plain("");
    internal Loc.LocText HelpLoc { get; init; } = Loc.LocText.Plain("");
    public string Label => LabelText.Text;
    public string HelpText => HelpLoc.Text;
    public Func<bool>? Visible { get; init; }
    public bool IsVisible => Visible == null || Visible();
}

public sealed class ToggleHolder : OptionHolder
{
    private readonly ConfigEntry<bool> _entry;

    public ToggleHolder(
        ConfigFile cfg,
        string section,
        string key,
        string label,
        bool def)
        : this(cfg, section, key, label, def, "")
    {
    }

    public ToggleHolder(
        ConfigFile cfg,
        string section,
        string key,
        string label,
        bool def,
        string helpText)
        : this(cfg, section, key, Loc.LocText.Plain(label), def, Loc.LocText.Plain(helpText))
    {
    }

    internal ToggleHolder(
        ConfigFile cfg,
        string section,
        string key,
        Loc.LocText label,
        bool def,
        Loc.LocText helpText)
    {
        Id = section + "." + key;
        LabelText = label;
        HelpLoc = string.IsNullOrWhiteSpace(helpText.English)
            ? Loc.L("host.default_toggle_help", "Enables or disables this host voice rule for the lobby. The host's choice is synchronized to all players.")
            : helpText;
        _entry = cfg.Bind(section, key, def,
            ConfigHelp.D(HelpLoc));
    }

    public bool Value
    {
        get => _entry.Value;
        set => _entry.Value = value;
    }
}

public sealed class EnumHolder : OptionHolder
{
    private readonly ConfigEntry<int> _entry;
    private readonly Loc.LocText[] _labels;

    public Type EnumType { get; }

    /// <summary>Choice labels in the current language (allocates; use <see cref="LabelAt"/> per frame).</summary>
    public string[] Labels
    {
        get
        {
            var texts = new string[_labels.Length];
            for (int i = 0; i < texts.Length; i++) texts[i] = _labels[i].Text;
            return texts;
        }
    }

    public int LabelCount => _labels.Length;
    public string LabelAt(int index) => _labels[Math.Clamp(index, 0, _labels.Length - 1)].Text;

    public EnumHolder(
        ConfigFile cfg,
        string section,
        string key,
        string label,
        int def,
        Type enumType,
        string[] labels)
        : this(cfg, section, key, label, def, enumType, labels, "")
    {
    }

    public EnumHolder(
        ConfigFile cfg,
        string section,
        string key,
        string label,
        int def,
        Type enumType,
        string[] labels,
        string helpText)
        : this(cfg, section, key, Loc.LocText.Plain(label), def, enumType,
            Array.ConvertAll(labels, Loc.LocText.Plain), Loc.LocText.Plain(helpText))
    {
    }

    internal EnumHolder(
        ConfigFile cfg,
        string section,
        string key,
        Loc.LocText label,
        int def,
        Type enumType,
        Loc.LocText[] labels,
        Loc.LocText helpText)
    {
        Id = section + "." + key;
        LabelText = label;
        HelpLoc = string.IsNullOrWhiteSpace(helpText.English)
            ? Loc.L("host.default_enum_help", "Chooses the value for this host voice rule. The host's choice is synchronized to all players.")
            : helpText;
        EnumType = enumType;
        _labels = labels;
        _entry = cfg.Bind(section, key, def,
            ConfigHelp.D(HelpLoc));
    }

    public int Value
    {
        get => _entry.Value;
        set => _entry.Value = value;
    }
}

public sealed class NumberHolder : OptionHolder
{
    private readonly ConfigEntry<float> _entry;

    public float Min { get; }
    public float Max { get; }
    public float Step { get; }
    public string Format { get; }

    public NumberHolder(
        ConfigFile cfg,
        string section,
        string key,
        string label,
        float def,
        float min,
        float max,
        float step,
        string format)
        : this(cfg, section, key, label, def, min, max, step, format, "")
    {
    }

    public NumberHolder(
        ConfigFile cfg,
        string section,
        string key,
        string label,
        float def,
        float min,
        float max,
        float step,
        string format,
        string helpText)
        : this(cfg, section, key, Loc.LocText.Plain(label), def, min, max, step, format, Loc.LocText.Plain(helpText))
    {
    }

    internal NumberHolder(
        ConfigFile cfg,
        string section,
        string key,
        Loc.LocText label,
        float def,
        float min,
        float max,
        float step,
        string format,
        Loc.LocText helpText)
    {
        Id = section + "." + key;
        LabelText = label;
        HelpLoc = string.IsNullOrWhiteSpace(helpText.English)
            ? Loc.L("host.default_number_help", "Adjusts this host voice rule for the lobby. The host's value is synchronized to all players.")
            : helpText;
        Min = min;
        Max = max;
        Step = step;
        Format = format;
        _entry = cfg.Bind(section, key, def,
            ConfigHelp.D(HelpLoc, new AcceptableValueRange<float>(min, max)));
    }

    public float Value
    {
        get => _entry.Value;
        set => _entry.Value = Math.Clamp(value, Min, Max);
    }
}
