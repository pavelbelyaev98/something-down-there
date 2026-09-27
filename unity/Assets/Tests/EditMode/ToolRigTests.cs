using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    public sealed class ToolRigTests
    {
        [TestCase("L01_Shaft__Wood", 1, 10)]
        [TestCase("L01-02_Blade__Steel", 1, 2)]
        [TestCase("L07-08_SpinBit__Steel", 7, 8)]
        [TestCase("L10_Nozzle__Paint", 10, 10)]
        public void StageNamesGiveTheirLevelRange(string name, int from, int to)
        {
            Assert.That(ToolRigPresenter.TryParseStage(name, out int first, out int last), Is.True);
            Assert.That((first, last), Is.EqualTo((from, to)));
        }

        [TestCase("Shaft")] [TestCase("L11_Extra__Steel")] [TestCase("L05-03_Backwards__Steel")] [TestCase("L1_Short__Steel")]
        public void MalformedStageNamesAreIgnored(string name) =>
            Assert.That(ToolRigPresenter.TryParseStage(name, out _, out _), Is.False);

        [Test]
        public void EveryLevelShowsADifferentMachineWithOneHeadAndTheDrillFromLevelSeven()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Content/ToolRig/Models/ToolRig.fbx");
            Assert.That(model, Is.Not.Null, "Run art/tool-rig/create_assets.py through Blender MCP.");
            var parts = model.GetComponentsInChildren<Transform>(true).Where(t => ToolRigPresenter.TryParseStage(t.name, out _, out _))
                .Select(t => { ToolRigPresenter.TryParseStage(t.name, out int from, out int to); return (t.name, from, to); }).ToArray();
            Assert.That(model.GetComponentsInChildren<MeshRenderer>(true).All(r => ToolRigPresenter.TryParseStage(r.name, out _, out _)), Is.True,
                "Every rig mesh follows the stage naming.");
            HashSet<string> previous = null;
            for (int level = 1; level <= EquipmentProgression.LevelCount; level++)
            {
                var shown = new HashSet<string>(parts.Where(p => level >= p.from && level <= p.to).Select(p => p.name));
                Assert.That(shown.Count(n => n.Contains("Blade") || n.Contains("HeadScoop")), Is.EqualTo(1), $"Level {level} has one head.");
                Assert.That(shown.Any(n => n.Contains("Spin")), Is.EqualTo(EquipmentProgression.UsesDrill(level)), $"Level {level} drill bit.");
                if (previous != null) Assert.That(shown.SetEquals(previous), Is.False, $"Level {level} looks like level {level - 1}.");
                previous = shown;
            }
        }
    }
}
