using System;
using System.Collections.Generic;
using System.Linq;
using Sokoban.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Sokoban.Editor
{
    public static class AddressableAddressValidator
    {
        [MenuItem("Tools/Sokoban/Validate Addressable Addresses")]
        public static void ValidateAll()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("Create Addressables settings before validating addresses.");
            var entries = new List<AddressableAssetEntry>();
            settings.GetAllAssets(entries, false);
            ValidateConfigs(entries);
            foreach (string guid in AssetDatabase.FindAssets("t:ViewCatalog"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ViewCatalog catalog = AssetDatabase.LoadAssetAtPath<ViewCatalog>(path);
                foreach (ViewCatalog.KindEntry kind in catalog.Entries)
                foreach (ViewCatalog.SkinEntry skin in kind.Skins)
                {
                    ValidateAddress(skin.Address, typeof(GameObject), entries, $"{path}: {new ViewKey(kind.Kind, skin.Skin)}");
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:LevelList"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                LevelList levels = AssetDatabase.LoadAssetAtPath<LevelList>(path);
                for (int index = 0; index < levels.Count; index++)
                {
                    ValidateAddress(levels.Levels[index].Address, typeof(TextAsset), entries, $"{path}: level {index}");
                }
            }
        }

        public static void ValidateAddress(string address, Type expectedType, IEnumerable<AddressableAssetEntry> entries, string context)
        {
            if (string.IsNullOrWhiteSpace(address)) throw new InvalidOperationException($"{context}: empty address.");
            AddressableAssetEntry[] matches = entries.Where(entry => string.Equals(entry.address, address, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException($"{context}: address '{address}' matches {matches.Length} assets; expected exactly one.");
            }
            AddressableAssetEntry match = matches[0];
            Type actualType = AssetDatabase.GetMainAssetTypeAtPath(match.AssetPath);
            if (actualType == null || !expectedType.IsAssignableFrom(actualType))
            {
                throw new InvalidOperationException($"{context}: address '{address}' must reference {expectedType.Name}, found {actualType?.Name ?? "missing asset"}.");
            }
            BundledAssetGroupSchema schema = match.parentGroup.GetSchema<BundledAssetGroupSchema>();
            if (!match.parentGroup.IncludeInBuild || schema != null && !schema.IncludeAddressInCatalog)
            {
                throw new InvalidOperationException($"{context}: address '{address}' is excluded from the build or its catalog addresses are disabled.");
            }
        }

        public static void ValidateConfigs(IEnumerable<AddressableAssetEntry> entries)
        {
            AddressableAssetEntry[] all = entries.ToArray();
            foreach (Type type in new[] { typeof(GameplaySettings), typeof(LevelList), typeof(ViewCatalog) })
            {
                AddressableAssetEntry[] matches = all.Where(entry => entry.labels.Contains(StaticDataProvider.ConfigLabel)
                    && type.IsAssignableFrom(AssetDatabase.GetMainAssetTypeAtPath(entry.AssetPath))).ToArray();
                if (matches.Length != 1)
                    throw new InvalidOperationException($"Label '{StaticDataProvider.ConfigLabel}' must contain exactly one {type.Name}; found {matches.Length}.");
                AddressableAssetEntry match = matches[0];
                ValidateAddress(match.address, type, all, StaticDataProvider.ConfigLabel);
                BundledAssetGroupSchema schema = match.parentGroup.GetSchema<BundledAssetGroupSchema>();
                if (schema != null && !schema.IncludeLabelsInCatalog)
                    throw new InvalidOperationException($"{match.address}: config labels are excluded from the catalog.");
            }
        }
    }
}
