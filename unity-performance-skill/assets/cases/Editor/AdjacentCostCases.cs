using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Qin.UnityPerformance.Cases
{
    public sealed class AdjacentCostCases
    {
        private sealed class LogSink : ILogHandler
        {
            public int Received;
            public string LastMessage;

            public void LogFormat(LogType level, Object context, string format, params object[] args)
            {
                Received++;
                LastMessage = string.Format(format, args);
            }

            public void LogException(Exception exception, Object context)
            {
                throw exception;
            }
        }

        [Test]
        public void DisabledDiagnosticsSkipPerActorFormattingAndEnabledLogsStillArrive()
        {
            LogSink sink = new LogSink();
            Logger logger = new Logger(sink) { logEnabled = false };
            int formats = 0;
            for (int actor = 0; actor < 128; actor++)
                logger.Log(LogType.Log, Describe(actor, ref formats));
            Assert.That(formats, Is.EqualTo(128));
            Assert.That(sink.Received, Is.Zero);

            formats = 0;
            for (int actor = 0; actor < 128; actor++)
                if (logger.IsLogTypeAllowed(LogType.Log))
                    logger.Log(LogType.Log, Describe(actor, ref formats));
            Assert.That(formats, Is.Zero);
            logger.logEnabled = true;
            if (logger.IsLogTypeAllowed(LogType.Log))
                logger.Log(LogType.Log, Describe(127, ref formats));
            Assert.That(formats, Is.EqualTo(1));
            Assert.That(sink.Received, Is.EqualTo(1));
            Assert.That(sink.LastMessage, Is.EqualTo("actor 127"));
            TestContext.WriteLine("QIN_CASE Disabled formatting calls fell from 128 to 0. One enabled message reached the sink.");
        }

        private static string Describe(int actor, ref int formats)
        {
            formats++;
            return $"actor {actor}";
        }

        [Test]
        public void SharedSpriteMaterialSurvivesIndependentTintAndActorRetirement()
        {
            GameObject first = new GameObject("First sprite");
            GameObject second = new GameObject("Second sprite");
            Material shared = new Material(Shader.Find("Sprites/Default"));
            Material owned = null;
            try
            {
                SpriteRenderer a = first.AddComponent<SpriteRenderer>();
                SpriteRenderer b = second.AddComponent<SpriteRenderer>();
                a.sharedMaterial = shared;
                b.sharedMaterial = shared;

                // A per-actor material copy has its own lifetime, even for a cosmetic tint.
                owned = new Material(shared);
                a.sharedMaterial = owned;
                Assert.That(a.sharedMaterial, Is.Not.SameAs(b.sharedMaterial));
                a.sharedMaterial = shared;
                Object.DestroyImmediate(owned);

                a.color = Color.red;
                b.color = Color.green;
                Assert.That(a.color, Is.EqualTo(Color.red));
                Assert.That(b.color, Is.EqualTo(Color.green));
                Assert.That(a.sharedMaterial, Is.SameAs(b.sharedMaterial));
                Object.DestroyImmediate(first);
                Assert.That(b.sharedMaterial, Is.SameAs(shared));
                Assert.That(owned == null, Is.True);
                TestContext.WriteLine("QIN_CASE Two independently tinted sprites shared one material. Retiring one retained the other binding.");
            }
            finally
            {
                if (first != null) Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                if (owned != null) Object.DestroyImmediate(owned);
                Object.DestroyImmediate(shared);
            }
        }
    }
}
