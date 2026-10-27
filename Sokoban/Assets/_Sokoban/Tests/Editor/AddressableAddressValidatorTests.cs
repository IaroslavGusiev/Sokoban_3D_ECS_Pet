using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sokoban.Editor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Sokoban.Tests.Editor
{
    public sealed class AddressableAddressValidatorTests
    {
        [Test]
        public void SavedAddresses_ResolveUniqueAssetsOfExpectedTypes() =>
            Assert.DoesNotThrow(AddressableAddressValidator.ValidateAll);

        [Test]
        public void MissingAddress_ReportsContextAndAddress()
        {
            var entries = Entries();
            string message = Assert.Throws<InvalidOperationException>(() =>
                AddressableAddressValidator.ValidateAddress("missing/view", typeof(GameObject), entries, "Player/Alternate")).Message;
            Assert.That(message, Does.Contain("Player/Alternate").And.Contain("missing/view").And.Contain("matches 0 assets"));
        }

        [Test]
        public void AmbiguousAddress_IsRejectedBeforeLoading()
        {
            var entries = Entries();
            AddressableAssetEntry player = entries.Single(entry => entry.address == "views/player");
            string message = Assert.Throws<InvalidOperationException>(() =>
                AddressableAddressValidator.ValidateAddress(player.address, typeof(GameObject), entries.Append(player), "Player")).Message;
            Assert.That(message, Does.Contain("matches 2 assets"));
        }

        [Test]
        public void WrongAssetType_IsRejected()
        {
            string message = Assert.Throws<InvalidOperationException>(() =>
                AddressableAddressValidator.ValidateAddress("views/player", typeof(TextAsset), Entries(), "level 0")).Message;
            Assert.That(message, Does.Contain("must reference TextAsset, found GameObject"));
        }

        [Test]
        public void ArrowWithoutTheRequiredFacade_IsRejected()
        {
            string message = Assert.Throws<InvalidOperationException>(() =>
                AddressableAddressValidator.ValidateHintArrow("views/box", Entries(), "HintArrow/Default")).Message;
            Assert.That(message, Does.Contain("HintArrow/Default").And.Contain("views/box").And.Contain("with HintArrow"));
        }

        private static List<AddressableAssetEntry> Entries()
        {
            var entries = new List<AddressableAssetEntry>();
            AddressableAssetSettingsDefaultObject.Settings.GetAllAssets(entries, false);
            return entries;
        }

        [Test]
        public void MissingConfigLabelEntry_IsRejected()
        {
            string message = Assert.Throws<InvalidOperationException>(() =>
                AddressableAddressValidator.ValidateConfigs(Entries().Where(entry => entry.address != "configs/gameplay"))).Message;
            Assert.That(message, Does.Contain("GameplaySettings").And.Contain("found 0"));
        }

        [Test]
        public void DuplicateConfigType_IsRejected()
        {
            var entries = Entries();
            string message = Assert.Throws<InvalidOperationException>(() =>
                AddressableAddressValidator.ValidateConfigs(entries.Append(entries.Single(entry => entry.address == "configs/gameplay")))).Message;
            Assert.That(message, Does.Contain("GameplaySettings").And.Contain("found 2"));
        }
    }
}
