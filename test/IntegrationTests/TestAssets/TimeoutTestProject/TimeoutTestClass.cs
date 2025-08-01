﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TimeoutTestProject;

#pragma warning disable CS0618 // Type or member is obsolete
[TestClass]
public class TimeoutTestClass
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Timeout(TestTimeout.Infinite)]
    public void TimeoutTest_WhenUserCancelsTestContextToken_AbortTest()
    {
        TestContext.CancellationTokenSource.Cancel();
        Assert.Fail("Test should have been canceled");
    }

#if NETFRAMEWORK
    [TestMethod]
    [Timeout(TestTimeout.Infinite)]
    public void TimeoutTest_WhenUserCallsThreadAbort_AbortTest()
    {
        Thread.CurrentThread.Abort();
        Assert.Fail("Test should have been canceled");
    }
#endif

    [TestMethod]
    public void RegularTest_WhenUserCancelsTestContextToken_TestContinues()
    {
        TestContext.CancellationTokenSource.Cancel();
        Assert.Fail("Expected failure: test should not be canceled");
    }

    [TestMethod]
    [Timeout(1000)]
    public void TimeoutTest_WhenTimeoutReached_CancelsTestContextToken()
    {
        var longTask = new Thread(ExecuteLong);
        longTask.Start();
        longTask.Join();
    }

    [TestMethod]
    [Timeout(500)]
    public void TimeoutTest_WhenTimeoutReached_ForcesTestAbort() => Thread.Sleep(100_000);

    private void ExecuteLong()
    {
        try
        {
            File.Delete("TimeoutTestOutput.txt");
            Task.Delay(100_000).Wait(TestContext.CancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            File.WriteAllText("TimeoutTestOutput.txt", "Written from long running thread post termination");
        }
    }
}
