using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SODataCodeGenTests
{
    private class Balance<T> : ScriptableObject
    {
        public T amount;
    }

    private sealed class Position : Balance<Vector3> { }
    private sealed class Legacy : SOLocalData { public int points; }

    private sealed class StepCommand
    {
        public StepCommand(Fields callerTarget) { callerTarget.step += 2; }
    }

    private sealed class Fields : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField] private int balance;
        public volatile int step;
        [NonSerialized] public int transient;
        public int Balance => balance;

        public int Accept(int input)
        {
            try
            {
                if (input < 0) throw new ArgumentOutOfRangeException(nameof(input));
                return balance += input;
            }
            catch (ArgumentOutOfRangeException) { return step = 3; }
            finally { transient++; }
        }

        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() => balance = 0;
    }

    [Test]
    public void AssignmentRetainsExpressionExceptionAndVolatileSemantics()
    {
        Fields data = ScriptableObject.CreateInstance<Fields>();
        List<string> observed = new List<string>();
        Action<string> record = name => observed.Add(name);
        try
        {
            SODataWriteHooks.Bind(data, record);
            Assert.AreEqual(7, data.Accept(7));
            Assert.AreEqual(3, data.Accept(-1));
            Assert.AreEqual(2, data.transient);
            new StepCommand(data);
            Assert.AreEqual(5, data.step);
            data.OnAfterDeserialize();
            Assert.AreEqual(0, data.Balance);
            CollectionAssert.AreEqual(new[] { "balance", "step", "step" }, observed);
        }
        finally
        {
            SODataWriteHooks.Unbind(data, record);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void ConcreteDomainRetainsItsClosedInheritedValueTypeCodec()
    {
        const BindingFlags lookup = BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Static;
        MethodInfo register = typeof(Position).GetMethod("__SODataRegisterCodecs", lookup);
        Assert.IsNotNull(register);
        register.Invoke(null, null);
        Assert.IsTrue(SODataWire.TryGet(typeof(Vector3), out SODataWire.Codec codec));
        Position data = ScriptableObject.CreateInstance<Position>();
        int notifications = 0;
        Action<string> record = name => { if (name == "amount") notifications++; };
        try
        {
            SODataWriteHooks.Bind(data, record);
            Vector3 expected = new Vector3(1, -2, 0);
            data.amount = expected;
            Assert.AreEqual(1, notifications);
            Assert.AreEqual(expected, codec.Decode(codec.Encode(data.amount)));
        }
        finally
        {
            SODataWriteHooks.Unbind(data, record);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void ExplicitlyBoundLegacySubclassObservesOnlyItsOwnGameFields()
    {
        Legacy data = ScriptableObject.CreateInstance<Legacy>();
        List<string> observed = new List<string>();
        Action<string> record = name => observed.Add(name);
        try
        {
            data.points = 4;
            SODataWriteHooks.Bind(data, record);
            data.points += 3;
            SODataWriteHooks.Unbind(data, record);
            data.points++;
            Assert.AreEqual(8, data.points);
            CollectionAssert.AreEqual(new[] { "points" }, observed);
        }
        finally
        {
            SODataWriteHooks.Unbind(data, record);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void CallerWithOnlyADomainAssemblyReferenceStillReportsTheWrite()
    {
        PlainMoney data = ScriptableObject.CreateInstance<PlainMoney>();
        List<string> observed = new List<string>();
        Action<string> record = name => observed.Add(name);
        try
        {
            Assert.AreNotSame(typeof(PlainMoney).Assembly, typeof(SODataNoEngineCaller).Assembly);
            SODataWriteHooks.Bind(data, record);
            data.money = 37;
            SODataNoEngineCaller.Credit(data, 3);
            Assert.AreEqual(40, data.money);
            CollectionAssert.AreEqual(new[] { "money", "money" }, observed);
        }
        finally
        {
            SODataWriteHooks.Unbind(data, record);
            Object.DestroyImmediate(data);
        }
    }
}
