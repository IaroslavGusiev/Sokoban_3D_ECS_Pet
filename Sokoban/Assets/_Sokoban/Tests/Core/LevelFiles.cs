using System.IO;

namespace Sokoban.CoreTests
{
    internal static class LevelFiles
    {
        public static string DirectoryPath
        {
            get
            {
                for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory != null; directory = directory.Parent)
                {
                    string path = Path.Combine(directory.FullName, "Assets", "Levels");
                    if (Directory.Exists(path)) return path;

                    path = Path.Combine(directory.FullName, "Sokoban", "Assets", "Levels");
                    if (Directory.Exists(path)) return path;
                }

                throw new DirectoryNotFoundException("Run level tests from the Sokoban project or repository; Assets/Levels was not found.");
            }
        }
    }
}
