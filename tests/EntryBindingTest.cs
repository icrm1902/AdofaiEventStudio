using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

namespace AdofaiEventStudio
{
    internal static class EntryBindingTest
    {
        private static string _managedDirectory;
        private static string _ummDirectory;

        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: EntryBindingTest <mod dll> <managed directory>");
                return 2;
            }

            string modPath = Path.GetFullPath(args[0]);
            _managedDirectory = Path.GetFullPath(args[1]);
            _ummDirectory = Path.Combine(_managedDirectory, "UnityModManager");
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

            try
            {
                Assembly ummAssembly = Assembly.UnsafeLoadFrom(
                    Path.Combine(_ummDirectory, "UnityModManager.dll"));
                Assembly modAssembly = Assembly.UnsafeLoadFrom(modPath);

                Type entryType = ummAssembly.GetType(
                    "UnityModManagerNet.UnityModManager+ModEntry",
                    true);
                object modEntry = FormatterServices.GetUninitializedObject(entryType);

                MethodInfo load = modAssembly
                    .GetType("AdofaiEventStudio.Main", true)
                    .GetMethod("Load", BindingFlags.Public | BindingFlags.Static);

                object result = load.Invoke(null, new object[] { modEntry });
                Assert(result is bool && (bool)result, "Load did not return true.");

                AssertBound(entryType, modEntry, "OnToggle");
                AssertBound(entryType, modEntry, "OnGUI");
                AssertBound(entryType, modEntry, "OnSaveGUI");

                try
                {
                    MethodInfo propertyCountMethod = modAssembly
                        .GetType("AdofaiEventStudio.EditorIntegration", true)
                        .GetMethod(
                            "GetStudioPropertyCountForTest",
                            BindingFlags.NonPublic | BindingFlags.Static);
                    int propertyCount = (int)propertyCountMethod.Invoke(null, null);
                    Assert(propertyCount >= 0, "Studio property form was not constructed.");
                }
                catch (TargetInvocationException exception)
                {
                    if (!(exception.InnerException is TypeLoadException))
                        throw;
                    Console.WriteLine(
                        "Property form runtime construction skipped outside Unity Mono.");
                }
                Console.WriteLine("UMM entry binding test passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void AssertBound(Type entryType, object instance, string fieldName)
        {
            FieldInfo field = entryType.GetField(fieldName);
            if (field == null || field.GetValue(instance) == null)
                throw new InvalidOperationException("UMM callback was not bound: " + fieldName);
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs eventArgs)
        {
            string name = new AssemblyName(eventArgs.Name).Name;
            if (name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                return null;

            string[] candidates = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name + ".dll"),
                Path.Combine(_managedDirectory, name + ".dll"),
                Path.Combine(_ummDirectory, name + ".dll")
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                    return Assembly.UnsafeLoadFrom(candidate);
            }
            return null;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
