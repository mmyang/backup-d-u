// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2020 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors: Numidium
//
// Notes:
//

using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Localization;
using UnityEngine.SocialPlatforms;
using UnityEngine.Localization.Tables;
using DaggerfallWorkshop.Game;

namespace DaggerfallWorkshop.Utility
{
    /// <summary>
    /// Interface to a text provider.
    /// Provides common functionality for dealing with various text sources.
    /// </summary>
    public interface ITextProvider
    {
        /// <summary>
        /// Gets tokens from a TEXT.RSC record.
        /// </summary>
        /// <param name="id">Text resource ID.</param>
        /// <returns>Text resource tokens.</returns>
        TextFile.Token[] GetRSCTokens(int id);

        /// <summary>
        /// Gets tokens from a randomly selected subrecord.
        /// </summary>
        /// <param name="id">Text resource ID.</param>
        /// <param name="dfRand">Use Daggerfall rand() for random selection.</param>
        /// <returns>Text resource tokens.</returns>
        TextFile.Token[] GetRandomTokens(int id, bool dfRand = false);

        /// <summary>
        /// Creates a custom token array.
        /// </summary>
        /// <param name="formatting">Formatting of each line.</param>
        /// <param name="lines">All text lines.</param>
        /// <returns>Token array.</returns>
        TextFile.Token[] CreateTokens(TextFile.Formatting formatting, params string[] lines);

        /// <summary>
        /// Gets string from token array.
        /// </summary>
        /// <param name="id">Text resource ID.</param>
        /// <returns>String from text resource.</returns>
        string GetText(int id);

        /// <summary>
        /// Gets random string from separated token array.
        /// Example would be flavour text variants when finding dungeon exterior.
        /// </summary>
        /// <param name="id">Text resource ID.</param>
        /// <returns>String randomly selected from variants.</returns>
        string GetRandomText(int id);

        /// <summary>
        /// Gets name of weapon material type.
        /// </summary>
        /// <param name="material">Material type of weapon.</param>
        /// <returns>String for weapon material name.</returns>
        string GetWeaponMaterialName(WeaponMaterialTypes material);

        /// <summary>
        /// Gets name of armor material type.
        /// </summary>
        /// <param name="material">Material type of armor.</param>
        /// <returns>String for armor material name.</returns>
        string GetArmorMaterialName(ArmorMaterialTypes material);

        /// <summary>
        /// Gets text for skill name.
        /// </summary>
        /// <param name="skill">Skill.</param>
        /// <returns>Text for this skill.</returns>
        string GetSkillName(DFCareer.Skills skill);

        /// <summary>
        /// Gets text for stat name.
        /// </summary>
        /// <param name="stat">Stat.</param>
        /// <returns>Text for this stat.</returns>
        string GetStatName(DFCareer.Stats stat);

        /// <summary>
        /// Gets abbreviated text for stat name.
        /// </summary>
        /// <param name="stat">Stat.</param>
        /// <returns>Abbreviated text for this stat.</returns>
        string GetAbbreviatedStatName(DFCareer.Stats stat);

        /// <summary>
        /// Gets text resource ID of stat description.
        /// </summary>
        /// <param name="stat">Stat.</param>
        /// <returns>Text resource ID.</returns>
        int GetStatDescriptionTextID(DFCareer.Stats stat);

        /// <summary>
        /// Attempts to read a localized string from a named table collection.
        /// </summary>
        /// <param name="collection">Name of table collection.</param>
        /// <param name="id">ID of string to get.</param>
        /// <param name="result">Localized string result or null/empty.</param>
        /// <returns>True if string found, otherwise false.</returns>
        bool GetLocalizedString(string collection, string id, out string result);

        /// <summary>
        /// Enable or disable verbose localized string debug in player log.
        /// </summary>
        /// <param name="enable">True to enable, false to disable.</param>
        void EnableLocalizedStringDebug(bool enable);
    }

    /// <summary>
    /// Implementation of a text provider.
    /// Inherit from this class and override as needed.
    /// </summary>
    public abstract class TextProvider : ITextProvider
    {
        public static string defaultInternalStringsCollectionName = "Internal_Strings";

        public bool localizedStringDebug = false;

        TextFile rscFile = new TextFile();

        public TextProvider()
        {
        }

        public virtual TextFile.Token[] GetRSCTokens(int id)
        {
            if (localizedStringDebug && !string.IsNullOrEmpty(TextManager.Instance.textRSCCollection))
                Debug.LogFormat("Trying localized string using RSC collection '{0}'", TextManager.Instance.textRSCCollection);

            // First attempt to get string from localization
            string localizedString;
            if (GetLocalizedString(TextManager.Instance.textRSCCollection, id.ToString(), out localizedString))
                return DaggerfallStringTableImporter.ConvertStringToRSCTokens(localizedString);

            if (localizedStringDebug)
                Debug.Log("Failed to get localized string. Fallback to TEXT.RSC");

            if (!rscFile.IsLoaded)
                OpenTextRSCFile();

            byte[] buffer = rscFile.GetBytesById(id);
            if (buffer == null)
                return null;

            return TextFile.ReadTokens(ref buffer, 0, TextFile.Formatting.EndOfRecord);
        }

        public virtual TextFile.Token[] GetRandomTokens(int id, bool dfRand = false)
        {
            TextFile.Token[] sourceTokens = GetRSCTokens(id);

            // Build a list of token subrecords
            List<TextFile.Token> currentStream = new List<TextFile.Token>();
            List<TextFile.Token[]> tokenStreams = new List<TextFile.Token[]>();
            for (int i = 0; i < sourceTokens.Length; i++)
            {
                // If we're at end of subrecord then start a new stream
                if (sourceTokens[i].formatting == TextFile.Formatting.SubrecordSeparator)
                {
                    tokenStreams.Add(currentStream.ToArray());
                    currentStream.Clear();
                    continue;
                }

                // Otherwise keep adding to current stream
                currentStream.Add(sourceTokens[i]);
            }

            // Complete final stream
            tokenStreams.Add(currentStream.ToArray());

            // Select a random token stream
            int index = dfRand ? (int)(DFRandom.rand() % tokenStreams.Count) : UnityEngine.Random.Range(0, tokenStreams.Count);

            // Select the next to last item from the array if the length of the last one is zero
            index = (tokenStreams[index].Length == 0 ? index - 1 : index);

            return tokenStreams[index];
        }

        /// <summary>
        /// Gets string from token array.
        /// </summary>
        /// <param name="id">Text resource ID.</param>
        /// <returns>String from single text resource.</returns>
        public virtual string GetText(int id)
        {
            TextFile.Token[] tokens = GetRSCTokens(id);
            if (tokens == null || tokens.Length == 0)
                return string.Empty;

            return tokens[0].text;
        }

        public virtual string GetRandomText(int id)
        {
            // Collect text items
            List<string> textItems = new List<string>();
            TextFile.Token[] tokens = GetRSCTokens(id);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i].formatting == TextFile.Formatting.Text)
                    textItems.Add(tokens[i].text);
            }

            // Validate items
            if (textItems.Count == 0)
                return string.Empty;

            // Select random text item
            int index = UnityEngine.Random.Range(0, textItems.Count);

            return textItems[index];
        }

        public virtual TextFile.Token[] CreateTokens(TextFile.Formatting formatting, params string[] lines)
        {
            List<TextFile.Token> tokens = new List<TextFile.Token>();

            foreach(string line in lines)
            {
                tokens.Add(new TextFile.Token(TextFile.Formatting.Text, line));
                tokens.Add(new TextFile.Token(formatting));
            }

            tokens.Add(new TextFile.Token(TextFile.Formatting.EndOfRecord));

            return tokens.ToArray();
        }

        /// <summary>
        /// Attempts to read a localized string from a named table collection.
        /// </summary>
        /// <param name="collection">Name of table collection.</param>
        /// <param name="id">ID of string to get.</param>
        /// <param name="result">Localized string result or null/empty.</param>
        /// <returns>True if string found, otherwise false.</returns>
        public virtual bool GetLocalizedString(string collection, string id, out string result)
        {
            result = string.Empty;
            if (string.IsNullOrEmpty(collection))
                return false;

            StringTable table = null;
            var sd = LocalizationSettings.StringDatabase;
            var op = sd.GetTableAsync(collection);
            if (op.IsDone)
                table = op.Result;
            else
                op.Completed += (o) => table = o.Result;

            if (table != null)
            {
                var entry = table.GetEntry(id);
                if (entry != null)
                {
                    result = entry.GetLocalizedString();
                    if (localizedStringDebug)
                        Debug.LogFormat("Found localized string for locale {0}\n{1}", LocalizationSettings.SelectedLocale.name, result);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Enable or disable verbose localized string debug in player log.
        /// </summary>
        /// <param name="enable">True to enable, false to disable.</param>
        public void EnableLocalizedStringDebug(bool enable)
        {
            localizedStringDebug = enable;
        }

        public string GetWeaponMaterialName(WeaponMaterialTypes material)
        {
            switch(material)
            {
                case WeaponMaterialTypes.Iron:
                    return TextManager.Instance.GetLocalizedText("iron");
                case WeaponMaterialTypes.Steel:
                    return TextManager.Instance.GetLocalizedText("steel");
                case WeaponMaterialTypes.Silver:
                    return TextManager.Instance.GetLocalizedText("silver");
                case WeaponMaterialTypes.Elven:
                    return TextManager.Instance.GetLocalizedText("elven");
                case WeaponMaterialTypes.Dwarven:
                    return TextManager.Instance.GetLocalizedText("dwarven");
                case WeaponMaterialTypes.Mithril:
                    return TextManager.Instance.GetLocalizedText("mithril");
                case WeaponMaterialTypes.Adamantium:
                    return TextManager.Instance.GetLocalizedText("adamantium");
                case WeaponMaterialTypes.Ebony:
                    return TextManager.Instance.GetLocalizedText("ebony");
                case WeaponMaterialTypes.Orcish:
                    return TextManager.Instance.GetLocalizedText("orcish");
                case WeaponMaterialTypes.Daedric:
                    return TextManager.Instance.GetLocalizedText("daedric");
                default:
                    return string.Empty;
            }
        }

        public string GetArmorMaterialName(ArmorMaterialTypes material)
        {
            switch (material)
            {
                case ArmorMaterialTypes.Leather:
                    return TextManager.Instance.GetLocalizedText("leather");
                case ArmorMaterialTypes.Chain:
                case ArmorMaterialTypes.Chain2:
                    return TextManager.Instance.GetLocalizedText("chain");
                case ArmorMaterialTypes.Iron:
                    return TextManager.Instance.GetLocalizedText("iron");
                case ArmorMaterialTypes.Steel:
                    return TextManager.Instance.GetLocalizedText("steel");
                case ArmorMaterialTypes.Silver:
                    return TextManager.Instance.GetLocalizedText("silver");
                case ArmorMaterialTypes.Elven:
                    return TextManager.Instance.GetLocalizedText("elven");
                case ArmorMaterialTypes.Dwarven:
                    return TextManager.Instance.GetLocalizedText("dwarven");
                case ArmorMaterialTypes.Mithril:
                    return TextManager.Instance.GetLocalizedText("mithril");
                case ArmorMaterialTypes.Adamantium:
                    return TextManager.Instance.GetLocalizedText("adamantium");
                case ArmorMaterialTypes.Ebony:
                    return TextManager.Instance.GetLocalizedText("ebony");
                case ArmorMaterialTypes.Orcish:
                    return TextManager.Instance.GetLocalizedText("orcish");
                case ArmorMaterialTypes.Daedric:
                    return TextManager.Instance.GetLocalizedText("daedric");
            }

            // Standard material type value not found.
            // Try again using material value masked to material type only.
            // Some save editors will not write back correct material mask, so we must try to handle this if possible.
            // Clamping range so we don't end up in infinite loop.
            int value = (int)material >> 8;
            value = Mathf.Clamp(value, (int)ArmorMaterialTypes.Iron, (int)ArmorMaterialTypes.Daedric);
            return GetArmorMaterialName((ArmorMaterialTypes)value);
        }

        public string GetSkillName(DFCareer.Skills skill)
        {
            switch (skill)
            {
                case DFCareer.Skills.Medical:
                    return "Medical";
                case DFCareer.Skills.Etiquette:
                    return "Etiquette";
                case DFCareer.Skills.Streetwise:
                    return "Streetwise";
                case DFCareer.Skills.Jumping:
                    return "Jumping";
                case DFCareer.Skills.Orcish:
                    return "Orcish";
                case DFCareer.Skills.Harpy:
                    return "Harpy";
                case DFCareer.Skills.Giantish:
                    return "Giantish";
                case DFCareer.Skills.Dragonish:
                    return "Dragonish";
                case DFCareer.Skills.Nymph:
                    return "Nymph";
                case DFCareer.Skills.Daedric:
                    return "Daedric";
                case DFCareer.Skills.Spriggan:
                    return "Spriggan";
                case DFCareer.Skills.Centaurian:
                    return "Centaurian";
                case DFCareer.Skills.Impish:
                    return "Impish";
                case DFCareer.Skills.Lockpicking:
                    return "Lockpicking";
                case DFCareer.Skills.Mercantile:
                    return "Mercantile";
                case DFCareer.Skills.Pickpocket:
                    return "Pickpocket";
                case DFCareer.Skills.Stealth:
                    return "Stealth";
                case DFCareer.Skills.Swimming:
                    return "Swimming";
                case DFCareer.Skills.Climbing:
                    return "Climbing";
                case DFCareer.Skills.Backstabbing:
                    return "Backstabbing";
                case DFCareer.Skills.Dodging:
                    return "Dodging";
                case DFCareer.Skills.Running:
                    return "Running";
                case DFCareer.Skills.Destruction:
                    return "Destruction";
                case DFCareer.Skills.Restoration:
                    return "Restoration";
                case DFCareer.Skills.Illusion:
                    return "Illusion";
                case DFCareer.Skills.Alteration:
                    return "Alteration";
                case DFCareer.Skills.Thaumaturgy:
                    return "Thaumaturgy";
                case DFCareer.Skills.Mysticism:
                    return "Mysticism";
                case DFCareer.Skills.ShortBlade:
                    return "Short Blade";
                case DFCareer.Skills.LongBlade:
                    return "Long Blade";
                case DFCareer.Skills.HandToHand:
                    return "Hand-to-Hand";
                case DFCareer.Skills.Axe:
                    return "Axe";
                case DFCareer.Skills.BluntWeapon:
                    return "Blunt Weapon";
                case DFCareer.Skills.Archery:
                    return "Archery";
                case DFCareer.Skills.CriticalStrike:
                    return "Critical Strike";
                default:
                    return string.Empty;
            }
        }

        public string GetStatName(DFCareer.Stats stat)
        {
            switch (stat)
            {
                case DFCareer.Stats.Strength:
                    return "Strength";
                case DFCareer.Stats.Intelligence:
                    return "Intelligence";
                case DFCareer.Stats.Willpower:
                    return "Willpower";
                case DFCareer.Stats.Agility:
                    return "Agility";
                case DFCareer.Stats.Endurance:
                    return "Endurance";
                case DFCareer.Stats.Personality:
                    return "Personality";
                case DFCareer.Stats.Speed:
                    return "Speed";
                case DFCareer.Stats.Luck:
                    return "Luck";
                default:
                    return string.Empty;
            }
        }

        public string GetAbbreviatedStatName(DFCareer.Stats stat)
        {
            switch (stat)
            {
                case DFCareer.Stats.Strength:
                    return "STR";
                case DFCareer.Stats.Intelligence:
                    return "INT";
                case DFCareer.Stats.Willpower:
                    return "WIL";
                case DFCareer.Stats.Agility:
                    return "AGI";
                case DFCareer.Stats.Endurance:
                    return "END";
                case DFCareer.Stats.Personality:
                    return "PER";
                case DFCareer.Stats.Speed:
                    return "SPD";
                case DFCareer.Stats.Luck:
                    return "LUC";
                default:
                    return string.Empty;
            }
        }

        public int GetStatDescriptionTextID(DFCareer.Stats stat)
        {
            switch (stat)
            {
                case DFCareer.Stats.Strength:
                    return 0;
                case DFCareer.Stats.Intelligence:
                    return 1;
                case DFCareer.Stats.Willpower:
                    return 2;
                case DFCareer.Stats.Agility:
                    return 3;
                case DFCareer.Stats.Endurance:
                    return 4;
                case DFCareer.Stats.Personality:
                    return 5;
                case DFCareer.Stats.Speed:
                    return 6;
                case DFCareer.Stats.Luck:
                    return 7;
                default:
                    return -1;
            }
        }

        #region Protected Methods

        protected void OpenTextRSCFile()
        {
            rscFile.Load(DaggerfallUnity.Instance.Arena2Path, TextFile.Filename);
        }

        #endregion
    }
}
