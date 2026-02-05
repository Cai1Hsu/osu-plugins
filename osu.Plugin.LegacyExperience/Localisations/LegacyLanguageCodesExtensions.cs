using System.Globalization;
using osu.Game.Extensions;
using LazerLanguage = osu.Game.Localisation.Language;

namespace osu.Plugin.LegacyExperience.Localisations;

public static class LegacyLanguageCodesExtensions
{
    public static string ToLegacyCode(this LegacyLanguageCodes language)
    {
        return language switch
        {
            LegacyLanguageCodes.en => "en",
            LegacyLanguageCodes.zh_CHS => "zh-CHS",
            LegacyLanguageCodes.zh_CHT => "zh-CHT",
            LegacyLanguageCodes.ja => "ja",
            LegacyLanguageCodes.ko => "ko",
            LegacyLanguageCodes.cs => "cs",
            LegacyLanguageCodes.da => "da",
            LegacyLanguageCodes.de => "de",
            LegacyLanguageCodes.eo => "eo",
            LegacyLanguageCodes.es => "es",
            LegacyLanguageCodes.fr => "fr",
            LegacyLanguageCodes.bg => "bg",
            LegacyLanguageCodes.el => "el",
            LegacyLanguageCodes.fi => "fi",
            LegacyLanguageCodes.he => "he",
            LegacyLanguageCodes.hr => "hr",
            LegacyLanguageCodes.id => "id",
            LegacyLanguageCodes.it => "it",
            LegacyLanguageCodes.hu => "hu",
            LegacyLanguageCodes.mn => "mn",
            LegacyLanguageCodes.ms_MY => "ms-MY",
            LegacyLanguageCodes.nl => "nl",
            LegacyLanguageCodes.no => "no",
            LegacyLanguageCodes.lv => "lv",
            LegacyLanguageCodes.pl => "pl",
            LegacyLanguageCodes.br => "br",
            LegacyLanguageCodes.pt => "pt",
            LegacyLanguageCodes.ro => "ro",
            LegacyLanguageCodes.ru => "ru",
            LegacyLanguageCodes.sk => "sk",
            LegacyLanguageCodes.sl => "sl",
            LegacyLanguageCodes.sv => "sv",
            LegacyLanguageCodes.th => "th",
            LegacyLanguageCodes.tr => "tr",
            LegacyLanguageCodes.vi_VN => "vi-VN",
            LegacyLanguageCodes.uk_UA => "uk-UA",
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
        };
    }

    public static LegacyLanguageCodes ToLegacy(this LazerLanguage lang)
    {
        return lang switch
        {
            // LazerLanguage.be => LegacyLanguageCodes.be, // seems no correct mapping for this in osu!stable, skipping for now.
            LazerLanguage.bg => LegacyLanguageCodes.bg,
            // LazerLanguage.ca => LegacyLanguageCodes.ca, // seems no correct mapping for this in osu!stable, skipping for now.
            LazerLanguage.cs => LegacyLanguageCodes.cs,
            LazerLanguage.da => LegacyLanguageCodes.da,
            LazerLanguage.de => LegacyLanguageCodes.de,
            LazerLanguage.el => LegacyLanguageCodes.el,
            LazerLanguage.es => LegacyLanguageCodes.es,
            LazerLanguage.fi => LegacyLanguageCodes.fi,
            LazerLanguage.fr => LegacyLanguageCodes.fr,
            LazerLanguage.hr_hr => LegacyLanguageCodes.hr,
            LazerLanguage.hu => LegacyLanguageCodes.hu,
            LazerLanguage.id => LegacyLanguageCodes.id,
            LazerLanguage.it => LegacyLanguageCodes.it,
            LazerLanguage.ja => LegacyLanguageCodes.ja,
            LazerLanguage.ko => LegacyLanguageCodes.ko,
            // LazerLanguage.lt => LegacyLanguageCodes.lt, // same as above
            LazerLanguage.lv_lv => LegacyLanguageCodes.lv,
            LazerLanguage.ms_my => LegacyLanguageCodes.ms_MY,
            LazerLanguage.nl => LegacyLanguageCodes.nl,
            LazerLanguage.no => LegacyLanguageCodes.no,
            LazerLanguage.pl => LegacyLanguageCodes.pl,
            LazerLanguage.pt => LegacyLanguageCodes.pt,
            LazerLanguage.pt_br => LegacyLanguageCodes.br,
            LazerLanguage.ro => LegacyLanguageCodes.ro,
            LazerLanguage.ru => LegacyLanguageCodes.ru,
            LazerLanguage.sk => LegacyLanguageCodes.sk,
            LazerLanguage.sl => LegacyLanguageCodes.sl,
            // LazerLanguage.sr => LegacyLanguageCodes.sr, // same as above
            LazerLanguage.sv => LegacyLanguageCodes.sv,
            LazerLanguage.th => LegacyLanguageCodes.th,
            LazerLanguage.tr => LegacyLanguageCodes.tr,
            LazerLanguage.uk => LegacyLanguageCodes.uk_UA,
            LazerLanguage.vi => LegacyLanguageCodes.vi_VN,
            LazerLanguage.zh => LegacyLanguageCodes.zh_CHS,
            LazerLanguage.zh_hant => LegacyLanguageCodes.zh_CHT,
            LazerLanguage.en or
            _ => LegacyLanguageCodes.en
        };
    }

    private static readonly LazerLanguage[] lazerLanguages = Enum.GetValues<LazerLanguage>();

    public static string ToCultureCode(this LegacyLanguageCodes langCode)
    {
        var lazerLang = lazerLanguages.FirstOrDefault(l => l.ToLegacy() == langCode, LazerLanguage.en);
        return lazerLang.ToCultureCode();
    }

    public static CultureInfo GetEffectiveCultureInfo(this LegacyLanguageCodes langCode)
    {
        try
        {
            return new CultureInfo(langCode.ToLegacyCode());
        }
        catch (CultureNotFoundException)
        {
            // fallback to invariant culture if the culture is not found, to avoid crashing the game.
            return CultureInfo.InvariantCulture;
        }
    }
}
