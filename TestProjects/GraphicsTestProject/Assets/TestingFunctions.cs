#if !UNITY_RUNTIMETEST

using System;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine
{
    public class AssertFailureException : System.Exception
    {
        public AssertFailureException() {}
        public AssertFailureException(string message) : base(message) {}
    }

    public class Testing
    {
        public static void TestCompleted()
        {
            Debug.Log("Test passed!");
            StopEditor();
        }

        private static void StopEditor()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#endif
        }
    }

    public class Check
    {
        private static double dEpsilon = 0.000001f;
        private static float fEpsilon = 0.0001f;

        #region IsTrue/IsFalse

        public static void IsTrue(bool b)
        {
            IsTrue(b, String.Empty);
        }

        public static void IsTrue(bool b, string message)
        {
            if (!b)
            {
                PauseEditor();
                throw new AssertFailureException("IsTrue failed. Message: " + message);
            }
        }

        public static void IsFalse(bool b)
        {
            IsFalse(b, String.Empty);
        }

        public static void IsFalse(bool b, string message)
        {
            if (b)
            {
                PauseEditor();
                throw new AssertFailureException("IsFalse failed. Message: " + message);
            }
        }

        #endregion IsTrue/IsFalse

        #region Fail

        public static void Fail()
        {
            Fail(String.Empty);
        }

        public static void Fail(string message)
        {
            PauseEditor();
            throw new AssertFailureException(message);
        }

        #endregion Fail

        #region AreEqual/AreNotEqual

        public static void AreEqual(object expected, object actual)
        {
            AreEqual(expected, actual, String.Empty);
        }

        public static void AreEqual(object expected, object actual, string message)
        {
            if (!_AreEqual(expected, actual))
            {
                PauseEditor();
                throw new AssertFailureException("AreEqual failed. Expected: " + expected + ", was: " + actual + ". Message: " + message);
            }
        }

        public static void AreNotEqual(object expected, object actual)
        {
            AreNotEqual(expected, actual, String.Empty);
        }

        public static void AreNotEqual(object expected, object actual, string message)
        {
            if (_AreEqual(expected, actual))
            {
                PauseEditor();
                throw new AssertFailureException("AreNotEqual failed. Did not expect both expected and actual to be: " + expected + ". Message: " + message);
            }
        }

        #endregion AreEqual/AreNotEqual

        #region AreSequencesEqual

        public static void AreSequencesEqual(IEnumerable expected, IEnumerable actual, ObjectEqualityComparer comparer, string message)
        {
            if (expected == null)
            {
                PauseEditor();
                throw new AssertFailureException("AreSequencesEqual failed. Expected sequence is null. Message: " + message);
            }

            if (actual == null)
            {
                PauseEditor();
                throw new AssertFailureException("AreSequencesEqual failed. Actual sequence is null. Message: " + message);
            }

            if (ReferenceEquals(expected, actual))
            {
                return;
            }

            var expectedEnumerator = expected.GetEnumerator();
            var actualEnumerator = actual.GetEnumerator();
            var count = 0;
            bool expectedSequenceHasNextElement;
            bool actualSequenceHasNextElement;

            for (expectedSequenceHasNextElement = expectedEnumerator.MoveNext(),
                 actualSequenceHasNextElement = actualEnumerator.MoveNext();
                 expectedSequenceHasNextElement && actualSequenceHasNextElement;
                 expectedSequenceHasNextElement = expectedEnumerator.MoveNext(),
                 actualSequenceHasNextElement = actualEnumerator.MoveNext())
            {
                var expectedElement = expectedEnumerator.Current;
                var actualElement = actualEnumerator.Current;

                if (!_AreEqual(expectedElement, actualElement, comparer))
                {
                    PauseEditor();
                    throw new AssertFailureException(string.Format("AreSequencesEqual failed. Sequences differ at element with index {0}. Expected: {1}, actual: {2}. Message: {3}",
                        count, expectedElement ?? "null", actualElement ?? "null", message));
                }

                ++count;
            }

            if (expectedSequenceHasNextElement || actualSequenceHasNextElement)
            {
                PauseEditor();
                throw new AssertFailureException("AreSequencesEqual failed. Sequences have different lengths. Message: " + message);
            }
        }

        public static void AreSequencesEqual(IEnumerable expected, IEnumerable actual)
        {
            AreSequencesEqual(expected, actual, String.Empty);
        }

        public static void AreSequencesEqual(IEnumerable expected, IEnumerable actual,
            ObjectEqualityComparer comparer)
        {
            AreSequencesEqual(expected, actual, comparer, String.Empty);
        }

        public static void AreSequencesEqual(IEnumerable expected, IEnumerable actual, string message)
        {
            AreSequencesEqual(expected, actual, new ObjectEqualityComparer(), message);
        }

        #endregion AreSequencesEqual

        #region _AreEqual helper methods

        private static bool _AreEqual(object a, object b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;
            return a.Equals(b);
        }

        private static bool _AreEqual(object first, object second, ObjectEqualityComparer comparer)
        {
            return ReferenceEquals(first, second) || comparer.Equal(first, second);
        }

        #endregion _AreEqual helper methods

        #region IsNull/IsNotNull

        public static void IsNull(object o)
        {
            IsNull(o, String.Empty);
        }

        public static void IsNull(object o, string message)
        {
            if (o != null)
            {
                PauseEditor();
                throw new AssertFailureException("IsNull failed. Message: " + message);
            }
        }

        public static void IsNotNull(object o)
        {
            IsNotNull(o, String.Empty);
        }

        public static void IsNotNull(object o, string message)
        {
            if (o == null)
            {
                PauseEditor();
                throw new AssertFailureException("IsNotNull failed. Message: " + message);
            }
        }

        #endregion IsNull/IsNotNull

        #region Check double

        private static bool CheckAreDoubleEqual(double expected, double actual, double allowedRelativeError)
        {
            if (Math.Abs(expected) < dEpsilon && Math.Abs(actual) < dEpsilon
                || (Math.Abs(actual - expected) < dEpsilon))
                return true;
            else
            {
                var relativeError = Math.Abs((actual - expected) / expected);

                if (relativeError <= allowedRelativeError)
                    return true;
                else
                    return false;
            }
        }

        public static void AreDoublesEqual(double expected, double actual, double allowedRelativeError)
        {
            AreDoublesEqual(expected, actual, allowedRelativeError, String.Empty);
        }

        public static void AreDoublesEqual(double expected, double actual, double allowedRelativeError, string message)
        {
            if (CheckAreDoubleEqual(expected, actual, allowedRelativeError))
                return;

            PauseEditor();
            throw new AssertFailureException("AreDoublesEqual failed. Expected: " + expected + ", was: " + actual + ". Message: " + message);
        }

        #endregion Check double

        #region Check float

        private static bool CheckAreFloatEqual(float expected, float actual, float allowedRelativeError)
        {
            if (Math.Abs(expected) < fEpsilon && Math.Abs(actual) < fEpsilon
                || (Math.Abs(actual - expected) < fEpsilon))
                return true;
            else
            {
                var relativeError = Math.Abs((actual - expected) / expected);

                if (relativeError <= allowedRelativeError)
                    return true;
                else
                    return false;
            }
        }

        public static void AreFloatsEqual(float expected, float actual, float allowedRelativeError)
        {
            AreFloatsEqual(expected, actual, allowedRelativeError, String.Empty);
        }

        public static void AreFloatsEqual(float expected, float actual, float allowedRelativeError, string message)
        {
            if (CheckAreFloatEqual(expected, actual, allowedRelativeError))
                return;

            PauseEditor();
            throw new AssertFailureException("AreFloatsEqual failed. Expected: " + expected + ", was: " + actual + ". Message: " + message);
        }

        public static void IsFloatLess(float expected, float actual)
        {
            IsFloatLess(expected, actual, null);
        }

        public static void IsFloatLess(float expected, float actual, string message)
        {
            if (actual < expected)
                return;

            PauseEditor();
            throw new AssertFailureException("AreFloatsEqual failed. Expected: less than " + expected + ", was: " + actual + ". Message: " + message);
        }

        public static void IsFloatGreater(float expected, float actual)
        {
            IsFloatGreater(expected, actual, null);
        }

        public static void IsFloatGreater(float expected, float actual, string message)
        {
            if (actual > expected)
                return;

            PauseEditor();
            throw new AssertFailureException("AreFloatsEqual failed. Expected: greater than " + expected + ", was: " + actual + ". Message: " + message);
        }

        public static void IsFloatLessOrEqual(float expected, float actual, float allowedRelativeError)
        {
            IsFloatLessOrEqual(expected, actual, allowedRelativeError, null);
        }

        public static void IsFloatLessOrEqual(float expected, float actual, float allowedRelativeError, string message)
        {
            if (actual <= expected ||
                CheckAreFloatEqual(expected, actual, allowedRelativeError))
                return;

            PauseEditor();
            throw new AssertFailureException("AreFloatsEqual failed. Expected: less than or equal to " + expected + ", was: " + actual + ". Message: " + message);
        }

        public static void IsFloatGreaterOrEqual(float expected, float actual, float allowedRelativeError)
        {
            IsFloatGreaterOrEqual(expected, actual, allowedRelativeError, null);
        }

        public static void IsFloatGreaterOrEqual(float expected, float actual, float allowedRelativeError, string message)
        {
            if (actual >= expected ||
                CheckAreFloatEqual(expected, actual, allowedRelativeError))
                return;

            PauseEditor();
            throw new AssertFailureException("AreFloatsEqual failed. Expected: greater than or equal to " + expected + ", was: " + actual + ". Message: " + message);
        }

        #endregion Check float

        #region AreVectorsEqual

        public static void AreVectorsEqual(UnityEngine.Vector3 expected, UnityEngine.Vector3 actual, float allowedRelativeError)
        {
            AreVectorsEqual(expected, actual, allowedRelativeError, null);
        }

        public static void AreVectorsEqual(UnityEngine.Vector3 expected, UnityEngine.Vector3 actual, float allowedRelativeError, string message)
        {
            if (CheckAreFloatEqual(expected.x, actual.x, allowedRelativeError) &&
                CheckAreFloatEqual(expected.y, actual.y, allowedRelativeError) &&
                CheckAreFloatEqual(expected.z, actual.z, allowedRelativeError))
                return;

            PauseEditor();
            throw new AssertFailureException("AreVectorsEqual failed. Expected: " + expected.ToString("F5") + ", was: " + actual.ToString("F5") + ". Message: " + message);
        }

        public static void AreVectorsEqual(UnityEngine.Vector3 expected, UnityEngine.Vector3 actual, UnityEngine.Vector3 allowedRelativeError)
        {
            AreVectorsEqual(expected, actual, allowedRelativeError, null);
        }

        public static void AreVectorsEqual(UnityEngine.Vector3 expected, UnityEngine.Vector3 actual, UnityEngine.Vector3 allowedRelativeError, string message)
        {
            if (CheckAreFloatEqual(expected.x, actual.x, allowedRelativeError.x) &&
                CheckAreFloatEqual(expected.y, actual.y, allowedRelativeError.y) &&
                CheckAreFloatEqual(expected.z, actual.z, allowedRelativeError.z))
                return;

            PauseEditor();
            throw new AssertFailureException("AreVectorsEqual failed. Expected: " + expected.ToString("F5") + ", was: " + actual.ToString("F5") + ". Message: " + message);
        }

        public static void AreVectorsEqual(UnityEngine.Vector2 expected, UnityEngine.Vector2 actual, float allowedRelativeError)
        {
            AreVectorsEqual(expected, actual, allowedRelativeError, null);
        }

        public static void AreVectorsEqual(UnityEngine.Vector2 expected, UnityEngine.Vector2 actual, float allowedRelativeError, string message)
        {
            if (CheckAreFloatEqual(expected.x, actual.x, allowedRelativeError) &&
                CheckAreFloatEqual(expected.y, actual.y, allowedRelativeError))
                return;

            PauseEditor();
            throw new AssertFailureException("AreVectorsEqual failed. Expected: " + expected.ToString("F5") + ", was: " + actual.ToString("F5") + ". Message: " + message);
        }

        public static void AreVectorsEqual(UnityEngine.Vector2 expected, UnityEngine.Vector2 actual, UnityEngine.Vector2 allowedRelativeError)
        {
            AreVectorsEqual(expected, actual, allowedRelativeError, null);
        }

        public static void AreVectorsEqual(UnityEngine.Vector2 expected, UnityEngine.Vector2 actual, UnityEngine.Vector2 allowedRelativeError, string message)
        {
            if (CheckAreFloatEqual(expected.x, actual.x, allowedRelativeError.x) &&
                CheckAreFloatEqual(expected.y, actual.y, allowedRelativeError.y))
                return;

            PauseEditor();
            throw new AssertFailureException("AreVectorsEqual failed. Expected: " + expected.ToString("F5") + ", was: " + actual.ToString("F5") + ". Message: " + message);
        }

        #endregion AreVectorsEqual

        #region AreColorComponentsEqual

        public static void AreColorComponentsEqual(UnityEngine.Color expected, UnityEngine.Color actual, float allowedRelativeError)
        {
            AreColorComponentsEqual(expected, actual, allowedRelativeError, null);
        }

        public static void AreColorComponentsEqual(UnityEngine.Color expected, UnityEngine.Color actual, float allowedRelativeError, string message)
        {
            if (CheckAreFloatEqual(expected.r, actual.r, allowedRelativeError) &&
                CheckAreFloatEqual(expected.g, actual.g, allowedRelativeError) &&
                CheckAreFloatEqual(expected.b, actual.b, allowedRelativeError) &&
                CheckAreFloatEqual(expected.a, actual.a, allowedRelativeError))
                return;

            PauseEditor();
            throw new AssertFailureException("AreColorComponentsEqual failed. Expected: " + expected + ", was: " + actual + ". Message: " + message);
        }

        #endregion AreColorComponentsEqual

        private static void PauseEditor()
        {
#if UNITY_EDITOR
            EditorApplication.isPaused = true;
#endif
        }

        public class ObjectEqualityComparer
        {
            public virtual bool Equal(object first, object second)
            {
                return ReferenceEquals(first, second) || //Covers scenario when both objects are null
                    first != null && first.Equals(second);
            }
        }
    }
}

#endif
