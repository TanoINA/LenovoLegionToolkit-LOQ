using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using LenovoLegionToolkit.Lib.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Tests;

[TestClass]
public class SettingsHardeningTests
{
    public sealed class PolymorphicStore
    {
        public object? Payload { get; set; }
    }

    private sealed class TestPolymorphicSettings(string filename) : AbstractSettings<PolymorphicStore>(filename)
    {
        public JsonSerializerSettings SerializerSettings => JsonSerializerSettings;
    }

    [TestMethod]
    public void SettingsBinder_AllowsToolkitPolymorphicTypes()
    {
        var settings = new TestPolymorphicSettings($"test_{Guid.NewGuid():N}.json");
        var original = new PolymorphicStore
        {
            Payload = new PowerModeAutomationPipelineTrigger(PowerModeState.Balance)
        };

        var json = JsonConvert.SerializeObject(original, settings.SerializerSettings);
        var roundTrip = JsonConvert.DeserializeObject<PolymorphicStore>(json, settings.SerializerSettings);

        Assert.IsNotNull(roundTrip);
        Assert.IsInstanceOfType<PowerModeAutomationPipelineTrigger>(roundTrip.Payload);
    }

    [TestMethod]
    public void SettingsBinder_RejectsDisallowedExternalGadgetTypes()
    {
        var settings = new TestPolymorphicSettings($"test_{Guid.NewGuid():N}.json");
        var maliciousJson = """
        {
          "Payload": {
            "$type": "System.Diagnostics.Process, System.Diagnostics.Process"
          }
        }
        """;

        Assert.ThrowsException<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<PolymorphicStore>(maliciousJson, settings.SerializerSettings));
    }

    [TestMethod]
    public void SettingsBinder_AllowsGenericCollectionsOfToolkitTypes()
    {
        var settings = new TestPolymorphicSettings($"test_{Guid.NewGuid():N}.json");
        var original = new PolymorphicStore
        {
            Payload = new List<IAutomationPipelineTrigger>
            {
                new ACAdapterConnectedAutomationPipelineTrigger()
            }
        };

        var json = JsonConvert.SerializeObject(original, settings.SerializerSettings);
        var roundTrip = JsonConvert.DeserializeObject<PolymorphicStore>(json, settings.SerializerSettings);

        Assert.IsNotNull(roundTrip);
        Assert.IsInstanceOfType<List<IAutomationPipelineTrigger>>(roundTrip.Payload);
    }
}
