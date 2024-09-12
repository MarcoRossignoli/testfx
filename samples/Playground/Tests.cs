﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods

using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 0)]

namespace Playground;

[TestClass]
public class TestClass
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Test() => Assert.AreEqual(1, 2);

    [TestMethod]
    public async Task Test2() => await Task.Delay(TimeSpan.FromSeconds(10), TestContext.CancellationTokenSource.Token);

    [TestMethod]
    public void Test3()
    {
    }
}
