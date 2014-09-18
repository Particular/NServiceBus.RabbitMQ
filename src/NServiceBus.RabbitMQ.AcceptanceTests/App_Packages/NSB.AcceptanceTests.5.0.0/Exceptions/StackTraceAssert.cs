using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace NServiceBus.AcceptanceTests.Exceptions
{
    static class StackTraceAssert
    {
// ReSharper disable once UnusedParameter.Global
        public static void StartsWith(string expected, string actual)
        {
            if (actual == null)
            {
                Assert.Fail();
            }
            else
            {
                var cleanStackTrace = CleanStackTrace(actual);
                try
                {
                    var expectedSt = expected.Replace("\r\n", "\n");
                    var cleanSt = cleanStackTrace.Replace("\r\n", "\n");

                    Assert.IsTrue(cleanSt.StartsWith(expectedSt), "Clean '{0}' should have started with '{1}'", cleanSt, expectedSt);
                }
                catch (Exception)
                {
                    Trace.WriteLine(cleanStackTrace);
                    throw;
                }
            }
        }
        static string CleanStackTrace(string stackTrace)
        {
            if (stackTrace== null)
            {
                return string.Empty;
            }
            using (var stringReader = new StringReader(stackTrace))
            {
                var stringBuilder = new StringBuilder();
                while (true)
                {
                    var line = stringReader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    stringBuilder.AppendLine(line.Split(new[]
                    {
                        " in "
                    }, StringSplitOptions.RemoveEmptyEntries).First().Trim());
                }
                return stringBuilder.ToString().Trim();
            }
        }
    }
}

