using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests
{
    /// <summary>
    /// Guards the patch-application style, which is the one thing the Logic tests cannot see.
    /// <para>
    /// The product assembly can compile, pass every test, load cleanly in BepInEx and still apply
    /// <b>no patches at all</b>, because whether a Harmony patch is applied is decided at runtime from
    /// where its attributes sit. 0.1.2 shipped exactly that: every patch class declaring its target on
    /// the <b>method</b> (<c>[HarmonyPatch(typeof(X), "Y")]</c> above the patch method) applied
    /// nothing, while the classes declaring it on the <b>class</b> (<c>[HarmonyPatch(typeof(X))]</c> on
    /// the class, <c>[HarmonyPatch("Y")]</c> on the method — the shape CraftFromContainers uses)
    /// applied. In game that produced no exception and no log line: the mod loaded, reported "0 failed"
    /// and did nothing.
    /// </para>
    /// <para>
    /// This fixture reproduces both placements against throwaway targets in the test assembly and
    /// asserts each one takes effect, so the style this project uses can never silently stop working
    /// again. It needs no game assemblies: the targets are plain managed types, and it patches only the
    /// test assembly.
    /// </para>
    /// <para>
    /// If the method-level assertion fails, the fix is not to delete the assertion — it is to move the
    /// target onto the class in the product's patch classes, exactly as CraftFromContainers does.
    /// </para>
    /// </summary>
    [TestFixture]
    public class PatchApplicationTests
    {
        /// <summary>Target for the method-level placement under test.</summary>
        public static class MethodLevelTarget
        {
            public static bool Run() { return false; }
        }

        /// <summary>Target for the class-level placement under test.</summary>
        public static class ClassLevelTarget
        {
            public static bool Run() { return false; }
        }

        /// <summary>Target for the bare-class-level placement under test.</summary>
        public static class BareClassTarget
        {
            public static bool Run() { return false; }
        }

        // Placement A - the target is on the METHOD. This is the shape every patch class in the
        // product used before the fix.
        [HarmonyPatch(typeof(MethodLevelTarget), "Run")]
        [HarmonyPostfix]
        private static void MethodLevelPostfix(ref bool __result)
        {
            __result = true;
        }

        // Placement B - the target is on the CLASS and the method name is on the method. This is the
        // shape CraftFromContainers uses, and the shape the product uses after the fix.
        [HarmonyPatch(typeof(ClassLevelTarget))]
        private static class ClassLevelPatch
        {
            [HarmonyPatch("Run")]
            [HarmonyPostfix]
            private static void Postfix(ref bool __result)
            {
                __result = true;
            }
        }

        // Placement C - a BARE class-level [HarmonyPatch] with the whole target on the method. If this
        // one applies, the minimal product fix is to annotate each patch class with a bare
        // [HarmonyPatch] and leave every existing method-level attribute exactly where it is.
        [HarmonyPatch]
        private static class BareClassPatch
        {
            [HarmonyPatch(typeof(BareClassTarget), "Run")]
            [HarmonyPostfix]
            private static void Postfix(ref bool __result)
            {
                __result = true;
            }
        }

        [Test]
        public void BothAttributePlacementsAreApplied()
        {
            var harmony = new Harmony("CraftFromDrawersAndChests.PatchApplicationProbe");

            // PatchAll(Assembly) is what Plugin.Awake calls, so the probe exercises the same entry
            // point rather than patching the two classes one at a time.
            harmony.PatchAll(typeof(PatchApplicationTests).Assembly);

            var patchedMethods = new List<string>();
            foreach (MethodBase patchedMethod in harmony.GetPatchedMethods())
                patchedMethods.Add(patchedMethod.DeclaringType.Name + "." + patchedMethod.Name);

            string patched = string.Join(", ", patchedMethods);

            // The two forms that ARE applied, and the forms the product relies on.
            Assert.That(ClassLevelTarget.Run(), Is.True,
                "A class-level [HarmonyPatch(typeof(X))] was not applied. Patched methods: " + patched);

            Assert.That(BareClassTarget.Run(), Is.True,
                "A bare class-level [HarmonyPatch] with a method-level target was not applied. This is " +
                "the form the product's patch classes use, so the whole mod would be inert. " +
                "Patched methods: " + patched);

            // The form that is silently ignored, which is the failure this fixture exists for. Asserted
            // as a negative on purpose: it pins the behaviour that was diagnosed and fixed. If this ever
            // starts passing, HarmonyX has begun honouring a method-level-only target, so the product's
            // bare [HarmonyPatch] annotations are then redundant - though still harmless. Revisit this
            // fixture at that point rather than deleting the assertion.
            Assert.That(MethodLevelTarget.Run(), Is.False,
                "HarmonyX now applies a method-level-only target. Patched methods: " + patched);
        }
    }
}
