using System.ComponentModel;
using JetBrains.Annotations;

namespace osu.Plugin.LegacyExperience.Localisations;

/// <summary>
/// A list of language codes used in osu!stable, used for legacy experience plugin localisations.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public enum LegacyLanguageCodes
{
    [Description("en")]
    en,
    [Description("中文(简体)")]
    zh_CHS,
    [Description("中文(繁體)")]
    zh_CHT,
    [Description("日本語")]
    ja,
    [Description("한국어")]
    ko,
    [Description("Česky")]
    cs,
    [Description("Dansk")]
    da,
    [Description("Deutsch")]
    de,
    [Description("Esperanto")]
    eo,
    [Description("Español")]
    es,
    [Description("Français")]
    fr,
    [Description("Български")]
    bg,
    [Description("Ελληνικά")]
    el,
    [Description("Suomi")]
    fi,
    [Description("עברית")]
    he,
    [Description("Hrvatski")]
    hr,
    [Description("Bahasa Indonesia")]
    id,
    [Description("Italiano")]
    it,
    [Description("Magyar")]
    hu,
    [Description("Mongɣol kele")]
    mn,
    [Description("Bahasa Malaysia")]
    ms_MY,
    [Description("Nederlands")]
    nl,
    [Description("Norsk")]
    no,
    [Description("latviešu valoda")]
    lv,
    [Description("Polski")]
    pl,
    [Description("Português (Brasil)")]
    br,
    [Description("Português (Portugal)")]
    pt,
    [Description("Română (Romanian)")]
    ro,
    [Description("Русский")]
    ru,
    [Description("Slovenčina")]
    sk,
    [Description("Slovenščina")]
    sl,
    [Description("Svenska")]
    sv,
    [Description("ภาษาไทย")]
    th,
    [Description("Türkçe")]
    tr,
    [Description("Tiếng Việt")]
    vi_VN,
    [Description("Українська")]
    uk_UA,
}
