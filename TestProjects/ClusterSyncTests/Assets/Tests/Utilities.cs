using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.ClusterDisplay.Scripting;
using Unity.Collections;
using UnityEngine;

namespace Unity.ClusterDisplay.Tests
{
    public class SomeComponent : MonoBehaviour, IEquatable<SomeComponent>
    {
        public int myField;
        public float MyProperty { get; set; }

        public bool Equals(SomeComponent other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return myField == other.myField && MyProperty.Equals(other.MyProperty);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SomeComponent)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), myField, MyProperty);
        }

        public static bool operator ==(SomeComponent left, SomeComponent right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SomeComponent left, SomeComponent right)
        {
            return !Equals(left, right);
        }
    }

    public class TransformComparer : IEqualityComparer<Transform>
    {
        public bool Equals(Transform x, Transform y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.position.Equals(y.position) && x.rotation.Equals(y.rotation) && x.localScale.Equals(y.localScale);
        }

        public int GetHashCode(Transform obj)
        {
            return HashCode.Combine(obj.position, obj.rotation, obj.localScale);
        }
    }

    public class TransformMessageComparer : IEqualityComparer<TransformMessage>
    {
        float m_Epsilon;

        public TransformMessageComparer(float epsilon = 1e-5f)
        {
            m_Epsilon = epsilon;
        }

        public bool Equals(TransformMessage x, TransformMessage y)
        {
            var posApprox = Vector3.Distance(x.Position, y.Position) < m_Epsilon;
            var rotApprox = Quaternion.Angle(x.Rotation, y.Rotation) < m_Epsilon;
            var scaleApprox = Vector3.Distance(x.Scale, y.Scale) < m_Epsilon;
            return posApprox && rotApprox && scaleApprox;
        }

        public int GetHashCode(TransformMessage obj)
        {
            return HashCode.Combine(obj.Position, obj.Rotation, obj.Scale);
        }
    }

    static class Utilities
    {
        public static byte[] AllocRandomByteArray(int length)
        {
            var ret = new byte[length];
            for (int currentPosition = 0; currentPosition < length; currentPosition += 16)
            {
                var toCopy = Guid.NewGuid().ToByteArray().AsSpan(new Range(0, Math.Min(length - currentPosition, 16)));
                toCopy.CopyTo(new Span<byte>(ret, currentPosition, length - currentPosition));
            }
            return ret;
        }

        public static IEnumerator ToCoroutine(this Task task, float timeoutSeconds)
        {
            var elapsed = 0f;
            while (!task.IsCompleted && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.True(task.IsCompleted);
            Assert.DoesNotThrow(task.Wait);
        }

        public static CoroutineTask<T> ToCoroutine<T>(this ValueTask<T> task) => new(task);

        public static bool LoopUntil(Func<bool> pred, int maxRetries)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                if (pred())
                {
                    return true;
                }

                Thread.Sleep(100);
            }

            return false;
        }

        public static int AppendCustomData<T>(this NativeArray<T> array, int pos, T[] data) where T : struct
        {
            array.GetSubArray(pos, data.Length).CopyFrom(data);
            return pos + data.Length;
        }
    }

    /// <summary>
    /// Class for running an async <see cref="Task{TResult}"/> as a coroutine and keeping track of the
    /// return value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    class CoroutineTask<T>
    {
        public T Result =>
            m_Task.IsCompletedSuccessfully
                ? m_Task.Result
                : throw new InvalidOperationException("Task is was not complete, or it was faulted");

        ValueTask<T> m_Task;

        public bool IsSuccessful => m_Task.IsCompletedSuccessfully;

        public CoroutineTask(ValueTask<T> task) => m_Task = task;

        public IEnumerator WaitForCompletion(float timeoutSeconds)
        {
            var elapsed = 0f;
            while (!m_Task.IsCompleted && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
