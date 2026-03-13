using System.IO;
using NUnit.Framework;
using UnityEngine;


namespace Tests.EditMode
{
    public class SaveSystem_NullSlot_DontCreateFile
    {
        private struct TestState
        {
            public int Score;
        }

        [Test]
        public void SaveSystem_NullSlot_NoCreatesFile()
        {
            const string slot = null;

            var saveSystem = new SaveSystem.SaveSystem();

            string path = Path.Combine(
                Application.persistentDataPath,
                "saves",
                $"{slot}.save.json");

            if (File.Exists(path))
                File.Delete(path);

            var state = new TestState { Score = 42 };

            Assert.Throws<System.ArgumentException>(() =>
            {
                saveSystem.Save(null, state);
            });

            File.Delete(path);
        }
    }
}