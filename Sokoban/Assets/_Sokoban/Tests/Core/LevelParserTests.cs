using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Components;

namespace Sokoban.CoreTests
{
    public sealed class LevelParserTests
    {
        private const string Example = "#######\n#-----#\n#-.-.-#\n#-$-$-#\n#--@--#\n#-----#\n#######";

        [Test]
        public void Example_MapsTopRowToHighestY()
        {
            LevelDefinition level = LevelParser.Parse(Example);
            Assert.That(level.Width, Is.EqualTo(7));
            Assert.That(level.Height, Is.EqualTo(7));
            Assert.That(level.PlayerStart, Is.EqualTo(new GridPosition(3, 2)));
            Assert.That(level.Boxes, Is.EquivalentTo(new[] { new GridPosition(2, 3), new GridPosition(4, 3) }));
            Assert.That(level.Goals, Is.EquivalentTo(new[] { new GridPosition(2, 4), new GridPosition(4, 4) }));
            var walls = from y in Enumerable.Range(0, 7)
                        from x in Enumerable.Range(0, 7)
                        where x == 0 || x == 6 || y == 0 || y == 6
                        select new GridPosition(x, y);
            Assert.That(level.Walls, Is.EquivalentTo(walls));
        }

        [Test]
        public void Level01File_MatchesOriginalLevelData()
        {
            string text = File.ReadAllText(Path.Combine(LevelFiles.DirectoryPath, "level-01.txt"));
            LevelDefinition level = LevelParser.Parse(text);
            Assert.That(level.Width, Is.EqualTo(7));
            Assert.That(level.Height, Is.EqualTo(7));
            Assert.That(level.PlayerStart, Is.EqualTo(new GridPosition(3, 2)));
            Assert.That(level.Boxes, Is.EquivalentTo(new[] { new GridPosition(2, 3), new GridPosition(4, 3) }));
            Assert.That(level.Goals, Is.EquivalentTo(new[] { new GridPosition(2, 4), new GridPosition(4, 4) }));
            var walls = from y in Enumerable.Range(0, 7)
                        from x in Enumerable.Range(0, 7)
                        where x == 0 || x == 6 || y == 0 || y == 6
                        select new GridPosition(x, y);
            Assert.That(level.Walls, Is.EquivalentTo(walls));
        }

        [Test]
        public void CrLfAndTrailingEmptyLines_DoNotChangeCoordinates()
        {
            LevelDefinition level = LevelParser.Parse(Example.Replace("\n", "\r\n") + "\r\n\r\n");
            Assert.That(level.Height, Is.EqualTo(7));
            Assert.That(level.PlayerStart, Is.EqualTo(new GridPosition(3, 2)));
            Assert.That(level.Goals, Is.EquivalentTo(new[] { new GridPosition(2, 4), new GridPosition(4, 4) }));
        }

        [Test]
        public void CombinedSymbols_CountBothOccupantsAndGoals()
        {
            LevelDefinition level = LevelParser.Parse("#######\n#+*$--#\n#######");
            Assert.That(level.PlayerStart, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(level.Boxes, Is.EquivalentTo(new[] { new GridPosition(2, 1), new GridPosition(3, 1) }));
            Assert.That(level.Goals, Is.EquivalentTo(new[] { new GridPosition(1, 1), new GridPosition(2, 1) }));
        }

        [TestCase(null, "level:", "provide")]
        [TestCase("", "level:", "at least one row")]
        [TestCase("\r\n\r\n", "level:", "at least one row")]
        [TestCase("###\n\n###", "line 2:", "blank line")]
        [TestCase("\n###", "line 1:", "blank line")]
        [TestCase("####\n###", "line 2:", "4 cells")]
        [TestCase("###\n####", "line 2:", "3 cells")]
        [TestCase("###\n#x#\n###", "line 2:", "column 2")]
        [TestCase("###\n# #\n###", "line 2:", "no spaces")]
        [TestCase("###\n#\t#\n###", "line 2:", "contains '\\t'")]
        [TestCase("###\n#\r#\n###", "line 2:", "contains '\\r'")]
        [TestCase("#####\n#-$.#\n#####", "level:", "exactly one player")]
        [TestCase("######\n#@@$.#\n######", "level:", "exactly one player")]
        [TestCase("######\n#@+$.#\n######", "level:", "exactly one player")]
        [TestCase("#####\n#@-.#\n#####", "level:", "at least one box")]
        [TestCase("#####\n#@$-#\n#####", "level:", "same number")]
        [TestCase("######\n#@$..#\n######", "level:", "same number")]
        public void InvalidText_ExplainsWhatToFix(string text, string prefix, string explanation)
        {
            LevelFormatException exception = Assert.Throws<LevelFormatException>(() => LevelParser.Parse(text));
            Assert.That(exception.Message, Does.StartWith(prefix));
            Assert.That(exception.Message, Does.Contain(explanation));
        }

        [TestCase("##-##\n#@$.#\n#####")]
        [TestCase("#####\n#@$.#\n##-##")]
        [TestCase("#####\n-@$.#\n#####")]
        [TestCase("#####\n#@$.-\n#####")]
        [TestCase("@$.##\n#####")]
        public void PlayableAreaReachingAnyGridEdge_IsRejected(string text)
        {
            LevelFormatException exception = Assert.Throws<LevelFormatException>(() => LevelParser.Parse(text));
            Assert.That(exception.Message, Is.EqualTo("level: the playable area touches the grid edge; close it with walls (#) so the player and boxes cannot leave."));
        }

        [Test]
        public void WalledPlayableArea_StillParses()
        {
            LevelDefinition level = LevelParser.Parse("#####\n#@$.#\n#####");
            Assert.That(level.PlayerStart, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(level.Boxes, Is.EqualTo(new[] { new GridPosition(2, 1) }));
            Assert.That(level.Goals, Is.EqualTo(new[] { new GridPosition(3, 1) }));
        }

        [Test]
        public void AlreadySolvedLevel_IsRejected()
        {
            LevelFormatException exception = Assert.Throws<LevelFormatException>(() => LevelParser.Parse("#####\n#@*-#\n#####"));
            Assert.That(exception.Message, Is.EqualTo("level: the level is already solved; leave at least one box off its goal."));
        }

        [Test]
        public void DiagonallyAdjacentEdge_DoesNotOpenThePlayableArea()
        {
            LevelDefinition level = LevelParser.Parse("#####\n#@$.#\n####-");
            Assert.That(level.PlayerStart, Is.EqualTo(new GridPosition(1, 1)));
        }

        [Test]
        public void UnreachableGridEdge_DoesNotOpenThePlayableArea()
        {
            LevelDefinition level = LevelParser.Parse("-#####\n##@$.#\n######");
            Assert.That(level.Width, Is.EqualTo(6));
        }

        [TestCase('%', "%")]
        [TestCase(' ', " ")]
        [TestCase('\t', "\\t")]
        [TestCase('\r', "\\r")]
        public void UnsupportedCharacter_MessageIncludesWhatWasFound(char character, string display)
        {
            LevelFormatException exception = Assert.Throws<LevelFormatException>(() => LevelParser.Parse($"###\n#{character}#\n###"));
            Assert.That(exception.Message, Is.EqualTo($"line 2: column 2 contains '{display}'; use only # - @ $ . * + (no spaces)."));
        }
    }
}
