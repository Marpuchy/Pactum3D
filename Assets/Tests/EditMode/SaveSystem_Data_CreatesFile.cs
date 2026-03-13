using System.IO;
using NUnit.Framework;
using UnityEngine;


namespace Tests.EditMode
{
    public class SaveSystem_Data_CreatesFile
    {
        private struct TestState
        {
            public int Score;
        }

        [Test]
        public void SaveSystem_CreatesFile()
        {
            const string slot = "single_test_slot";

            var saveSystem = new SaveSystem.SaveSystem();

            string path = Path.Combine(
                Application.persistentDataPath,
                "saves",
                $"{slot}.save.json");

            if (File.Exists(path))
                File.Delete(path);

            var state = new TestState { Score = 42 };

            saveSystem.Save(slot, state);

            Assert.IsTrue(File.Exists(path));

            File.Delete(path);
        }
    }
}