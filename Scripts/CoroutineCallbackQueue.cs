using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

class CoroutineCallbackQueue
{
    private Queue<IEnumerator> coroutineQueue = new Queue<IEnumerator>();

    public Action<T> SetupCoroutineCallback<T>(Func<T, IEnumerator> cf)
    {
        return (t) =>
        {
            var handle = cf(t);
            coroutineQueue.Enqueue(handle);
        };
    }

    public Action<T, V> SetupCoroutineCallback<T, V>(Func<T, V, IEnumerator> cf)
    {
        return (t1, t2) =>
        {
            var handle = cf(t1, t2);
            coroutineQueue.Enqueue(handle);
        };
    }

    public Action<T1, T2, T3> SetupCoroutineCallback<T1, T2, T3>(Func<T1, T2, T3, IEnumerator> cf)
    {
        return (t1, t2, t3) =>
        {
            var handle = cf(t1, t2, t3);
            coroutineQueue.Enqueue(handle);
        };
    }

    public Action<T1, T2, T3, T4> SetupCoroutineCallback<T1, T2, T3, T4>(Func<T1, T2, T3, T4, IEnumerator> cf)
    {
        return (t1, t2, t3, t4) =>
        {
            var handle = cf(t1, t2, t3, t4);
            coroutineQueue.Enqueue(handle);
        };
    }

    public Action<T1, T2, T3, T4, T5> SetupCoroutineCallback<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5, IEnumerator> cf)
    {
        return (t1, t2, t3, t4, t5) =>
        {
            var handle = cf(t1, t2, t3, t4, t5);
            coroutineQueue.Enqueue(handle);
        };
    }

    public Action<T1, T2, T3, T4, T5, T6> SetupCoroutineCallback<T1, T2, T3, T4, T5, T6>(Func<T1, T2, T3, T4, T5, T6, IEnumerator> cf)
    {
        return (t1, t2, t3, t4, t5, t6) =>
        {
            var handle = cf(t1, t2, t3, t4, t5, t6);
            coroutineQueue.Enqueue(handle);
        };
    }

    public Action<T1, T2, T3, T4, T5, T6, T7> SetupCoroutineCallback<T1, T2, T3, T4, T5, T6, T7>(Func<T1, T2, T3, T4, T5, T6, T7, IEnumerator> cf)
    {
        return (t1, t2, t3, t4, t5, t6, t7) =>
        {
            var handle = cf(t1, t2, t3, t4, t5, t6, t7);
            coroutineQueue.Enqueue(handle);
        };
    }

    public Action<T1, T2, T3, T4, T5, T6, T7, T8> SetupCoroutineCallback<T1, T2, T3, T4, T5, T6, T7, T8>(Func<T1, T2, T3, T4, T5, T6, T7, T8, IEnumerator> cf)
    {
        return (t1, t2, t3, t4, t5, t6, t7, t8) =>
        {
            var handle = cf(t1, t2, t3, t4, t5, t6, t7, t8);
            coroutineQueue.Enqueue(handle);
        };
    }

    public Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> SetupCoroutineCallback<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, IEnumerator> cf)
    {
        return (t1, t2, t3, t4, t5, t6, t7, t8, t9) =>
        {
            var handle = cf(t1, t2, t3, t4, t5, t6, t7, t8, t9);
            coroutineQueue.Enqueue(handle);
        };
    }

    public bool Empty => coroutineQueue.Count <= 0;
    public IEnumerator Next => coroutineQueue.Dequeue();
}