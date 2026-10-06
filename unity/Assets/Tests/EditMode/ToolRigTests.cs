using System.Linq;
using NUnit.Framework;
using SomethingDownThere.Editor;

namespace SomethingDownThere.Tests
{
    public sealed class ToolRigTests
    {
        [TestCase("L01-06_Blade__Western", 1, 6)]
        [TestCase("L07-12_Drill", 7, EquipmentProgression.LevelCount)]
        [TestCase("L10_Nozzle__Paint", 10, EquipmentProgression.LevelCount)]
        public void StageNamesGiveTheirLevelRange(string name, int from, int to)
        {
            Assert.That(ToolRigPresenter.TryParseStage(name, out int first, out int last), Is.True);
            Assert.That((first, last), Is.EqualTo((from, to)));
        }

        [TestCase("Shaft")] [TestCase("L13_Extra__Steel")] [TestCase("L05-03_Backwards__Steel")] [TestCase("L1_Short__Steel")]
        public void MalformedStageNamesAreIgnored(string name) =>
            Assert.That(ToolRigPresenter.TryParseStage(name, out _, out _), Is.False);

        [Test]
        public void ShovelLevelsShowTheShovelAndDrillLevelsTheDrill()
        {
            var parts = ToolRigSetup.PartNames.Select(name =>
            {
                Assert.That(ToolRigPresenter.TryParseStage(name, out int from, out int to), Is.True, name + " follows the stage naming.");
                return (name, from, to);
            }).ToArray();
            for (int level = 1; level <= EquipmentProgression.LevelCount; level++)
            {
                var shown = parts.Where(p => level >= p.from && level <= p.to).Select(p => p.name).ToArray();
                Assert.That(shown, EquipmentProgression.UsesDrill(level)
                    ? Is.EquivalentTo(new[] { ToolRigSetup.Drill, ToolRigSetup.DrillHead })
                    : Is.EquivalentTo(new[] { ToolRigSetup.ShovelBlade }), $"Level {level}.");
            }
            Assert.That(ToolRigSetup.DrillHead, Does.Contain("Spin"), "The drill's head turns.");
        }
    }
}
