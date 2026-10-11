using System;
using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// Pins the Item Drawers reflection-contract resolution (Task 9, Ruling A). The resolution
    /// decision is pure, so it is exercised here with stub types standing in for the real
    /// <c>kg_ItemDrawers</c> assembly, which is never loaded in the test process. The stubs mirror
    /// the real API's shapes: a static <c>AllDrawers</c> property plus <c>AllDrawersInRange</c> on
    /// the preferred type, and a static <c>AllDrawers()</c> method on the fallback.
    /// </summary>
    [TestFixture]
    public class DrawerContractTests
    {
        /// <summary>
        /// Stands in for <c>UnityEngine.Vector3</c> in the range query's signature. The pure
        /// matcher compares parameter types by simple name (<c>"Vector3"</c>), and this test
        /// project deliberately references no UnityEngine, so the stub declares the name locally.
        /// </summary>
        private struct Vector3
        {
        }

        private static class StubItemDrawersApi
        {
            // A static property, like the real API.ItemDrawers_API.AllDrawers.
            public static object AllDrawers
            {
                get { return null; }
            }

            // Two parameters, like the real AllDrawersInRange(Vector3, float).
            public static object AllDrawersInRange(Vector3 pos, float range)
            {
                return null;
            }
        }

        private static class StubItemDrawersApiWithoutRange
        {
            public static object AllDrawers
            {
                get { return null; }
            }
        }

        private static class StubItemDrawersApiWithMismatchedRange
        {
            public static object AllDrawers
            {
                get { return null; }
            }

            // Same name and arity as the real range query, but the position parameter's type is
            // wrong. Binding this MethodInfo would throw at Invoke time, so the matcher must
            // reject it — otherwise the whole drawer path silently degrades to "no drawers".
            public static object AllDrawersInRange(object pos, float range)
            {
                return null;
            }
        }

        private static class StubClientSideV2
        {
            public static object AllDrawers()
            {
                return null;
            }
        }

        /// <summary>Builds a stub resolver that returns <paramref name="apiType"/> / <paramref name="v2Type"/> for the two qualified names.</summary>
        private static Func<string, Type> Lookup(Type apiType, Type v2Type)
        {
            return name =>
            {
                if (apiType != null && name == DrawerContract.ItemDrawersApiTypeName)
                    return apiType;
                if (v2Type != null && name == DrawerContract.ClientSideV2TypeName)
                    return v2Type;
                return null;
            };
        }

        [Test]
        public void NoTypesResolve_KindIsNoneAndNothingIsCached()
        {
            // Review Focus 5: Item Drawers absent — the soft-dependency fallback.
            DrawerContract contract = DrawerContract.Resolve(Lookup(null, null));

            Assert.That(contract.Kind, Is.EqualTo(DrawerContractKind.None));
            Assert.That(contract.IsAvailable, Is.False);
            Assert.That(contract.AllDrawersQuery, Is.Null);
            Assert.That(contract.RangeQuery, Is.Null);
        }

        [Test]
        public void ItemDrawersApiPresent_KindIsApiAndRangeQueryResolved()
        {
            DrawerContract contract = DrawerContract.Resolve(Lookup(typeof(StubItemDrawersApi), null));

            Assert.That(contract.Kind, Is.EqualTo(DrawerContractKind.ItemDrawersApi));
            Assert.That(contract.IsAvailable, Is.True);
            Assert.That(contract.RangeQuery, Is.Not.Null);
            Assert.That(contract.RangeQuery.Name, Is.EqualTo("AllDrawersInRange"));
            Assert.That(contract.RangeQuery.DeclaringType, Is.EqualTo(typeof(StubItemDrawersApi)));
            Assert.That(contract.RangeQuery.IsStatic, Is.True);
        }

        [Test]
        public void ItemDrawersApiPresent_AllDrawersQueryIsTheStaticGetter()
        {
            DrawerContract contract = DrawerContract.Resolve(Lookup(typeof(StubItemDrawersApi), null));

            // The real API exposes AllDrawers as a property, so the cached query must be its getter.
            Assert.That(contract.AllDrawersQuery, Is.Not.Null);
            Assert.That(contract.AllDrawersQuery.Name, Is.EqualTo("get_AllDrawers"));
            Assert.That(contract.AllDrawersQuery.DeclaringType, Is.EqualTo(typeof(StubItemDrawersApi)));
        }

        [Test]
        public void OnlyFallbackPresent_KindIsClientSideV2()
        {
            DrawerContract contract = DrawerContract.Resolve(Lookup(null, typeof(StubClientSideV2)));

            Assert.That(contract.Kind, Is.EqualTo(DrawerContractKind.ClientSideV2));
            Assert.That(contract.IsAvailable, Is.True);
            Assert.That(contract.AllDrawersQuery, Is.Not.Null);
            Assert.That(contract.AllDrawersQuery.Name, Is.EqualTo("AllDrawers"));
            Assert.That(contract.AllDrawersQuery.DeclaringType, Is.EqualTo(typeof(StubClientSideV2)));
            // The fallback has no native range query: the caller filters by distance itself.
            Assert.That(contract.RangeQuery, Is.Null);
        }

        [Test]
        public void BothContractsPresent_PrefersItemDrawersApi()
        {
            DrawerContract contract = DrawerContract.Resolve(
                Lookup(typeof(StubItemDrawersApi), typeof(StubClientSideV2)));

            Assert.That(contract.Kind, Is.EqualTo(DrawerContractKind.ItemDrawersApi));
        }

        [Test]
        public void ApiPresentButRangeMethodMissing_FallsBackToClientSideV2()
        {
            // Robustness against a partial rename: without the range query the API is not usable
            // as the preferred contract, so the fallback wins rather than half-working.
            DrawerContract contract = DrawerContract.Resolve(
                Lookup(typeof(StubItemDrawersApiWithoutRange), typeof(StubClientSideV2)));

            Assert.That(contract.Kind, Is.EqualTo(DrawerContractKind.ClientSideV2));
            Assert.That(contract.AllDrawersQuery.Name, Is.EqualTo("AllDrawers"));
        }

        [Test]
        public void ApiPresentButRangeMethodMissing_FallsBackToNoneWhenNoFallback()
        {
            DrawerContract contract = DrawerContract.Resolve(
                Lookup(typeof(StubItemDrawersApiWithoutRange), null));

            Assert.That(contract.Kind, Is.EqualTo(DrawerContractKind.None));
            Assert.That(contract.IsAvailable, Is.False);
        }

        [Test]
        public void ApiWithMismatchedRangeParameterTypes_FallsBackToClientSideV2()
        {
            // Robustness against a same-arity overload: the wrong MethodInfo must not bind, so
            // the preferred contract is rejected and the fallback is used instead.
            DrawerContract contract = DrawerContract.Resolve(
                Lookup(typeof(StubItemDrawersApiWithMismatchedRange), typeof(StubClientSideV2)));

            Assert.That(contract.Kind, Is.EqualTo(DrawerContractKind.ClientSideV2));
            Assert.That(contract.RangeQuery, Is.Null);
            Assert.That(contract.AllDrawersQuery.Name, Is.EqualTo("AllDrawers"));
        }

        [Test]
        public void ApiWithMismatchedRangeParameterTypes_FallsBackToNoneWhenNoFallback()
        {
            DrawerContract contract = DrawerContract.Resolve(
                Lookup(typeof(StubItemDrawersApiWithMismatchedRange), null));

            Assert.That(contract.Kind, Is.EqualTo(DrawerContractKind.None));
            Assert.That(contract.IsAvailable, Is.False);
        }

        [Test]
        public void NullResolver_KindIsNone()
        {
            DrawerContract contract = DrawerContract.Resolve(null);

            Assert.That(contract.Kind, Is.EqualTo(DrawerContractKind.None));
            Assert.That(contract.IsAvailable, Is.False);
        }

        [Test]
        public void ThrowingResolver_KindIsNoneAndDoesNotPropagate()
        {
            // Review Focus 5: a renamed/renamed-again API must degrade to chest-only, never throw.
            Func<string, Type> resolver = name => { throw new TypeLoadException(name); };

            DrawerContract contract = DrawerContract.Resolve(resolver);

            Assert.That(contract.Kind, Is.EqualTo(DrawerContractKind.None));
            Assert.That(contract.IsAvailable, Is.False);
        }
    }
}
