using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace AdofaiEventStudio
{
    internal static class SmokeTest
    {
        private static int _failures;

        private static int Main(string[] args)
        {
            string levelPath = args.Length > 0
                ? args[0]
                : @"D:\adofai\testlevel\level.adofai";

            try
            {
                EventProject project = new EventProject();
                project.Load(levelPath);
                Assert(project.ActionCount == 19, "Expected 19 actions.");
                Assert(project.SettingsVersion == 19, "Expected settings.version 19.");
                Assert(project.Query(7, 7, "RecolorTrack").Count == 1, "RecolorTrack query failed.");
                Assert(project.Query(2, 2, "MoveDecorations").Count == 10, "MoveDecorations query failed.");

                int changed = project.SetField(3, 3, "SetFilter", "intensity", "42");
                Assert(changed == 1, "SetFilter field update count failed.");
                Assert(
                    project.Query(3, 3, "SetFilter")[0].Action["intensity"].Value<int>() == 42,
                    "SetFilter field update value failed.");
                Assert(project.Undo(), "Undo after field update failed.");
                Assert(
                    project.Query(3, 3, "SetFilter")[0].Action["intensity"].Value<int>() == 100,
                    "Undo did not restore intensity.");

                changed = project.ShiftFloors(3, 3, "SetFilter", 2);
                Assert(changed == 1, "Floor shift count failed.");
                Assert(project.Query(5, 5, "SetFilter").Count == 1, "Floor shift result failed.");
                Assert(project.Undo(), "Undo after floor shift failed.");

                int beforeClone = project.ActionCount;
                int created = project.CloneAtInterval(2, 20, 24, 2, "MoveDecorations");
                Assert(created == 30, "Clone count failed.");
                Assert(project.ActionCount == beforeClone + 30, "Clone did not append actions.");
                Assert(project.Undo(), "Undo after clone failed.");
                Assert(project.ActionCount == beforeClone, "Undo did not remove cloned actions.");

                string temporary = Path.Combine(
                    Path.GetTempPath(),
                    "AdofaiEventStudio-smoke-" + Guid.NewGuid().ToString("N") + ".adofai");
                try
                {
                    project.Save(temporary);
                    EventProject saved = new EventProject();
                    saved.Load(temporary);
                    Assert(saved.ActionCount == 19, "Saved file did not round-trip.");
                }
                finally
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                _failures++;
            }

            if (_failures == 0)
            {
                Console.WriteLine("Smoke tests passed.");
                return 0;
            }

            Console.Error.WriteLine("Smoke tests failed: " + _failures);
            return 1;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
