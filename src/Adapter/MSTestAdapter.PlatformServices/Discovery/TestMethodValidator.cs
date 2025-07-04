﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;

/// <summary>
/// Determines if a method is a valid test method.
/// </summary>
[SuppressMessage("Performance", "CA1852: Seal internal types", Justification = "Overrides required for testability")]
internal class TestMethodValidator
{
    private readonly ReflectHelper _reflectHelper;
    private readonly bool _discoverInternals;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestMethodValidator"/> class.
    /// </summary>
    /// <param name="reflectHelper">An instance to reflection helper for type information.</param>
    /// <param name="discoverInternals">True to discover methods which are declared internal in addition to methods
    /// which are declared public.</param>
    internal TestMethodValidator(ReflectHelper reflectHelper, bool discoverInternals)
    {
        _reflectHelper = reflectHelper;
        _discoverInternals = discoverInternals;
    }

    /// <summary>
    /// Determines if a method is a valid test method.
    /// </summary>
    /// <param name="testMethodInfo"> The reflected method. </param>
    /// <param name="type"> The reflected type. </param>
    /// <param name="warnings"> Contains warnings if any, that need to be passed back to the caller. </param>
    /// <returns> Return true if a method is a valid test method. </returns>
    internal virtual bool IsValidTestMethod(MethodInfo testMethodInfo, Type type, ICollection<string> warnings)
    {
        // PERF: We are doing caching reflection here, meaning we will cache every method info in the
        // assembly, this is because when we discover and run we will repeatedly inspect all the methods in the assembly, and this
        // gives us a better performance.
        // It would be possible to use non-caching reflection here if we knew that we are only doing discovery that won't be followed by run,
        // but the difference is quite small, and we don't expect a huge amount of non-test methods in the assembly.
        //
        // Also skip all methods coming from object, because they cannot be tests.
        if (testMethodInfo.DeclaringType == typeof(object) || !_reflectHelper.IsAttributeDefined<TestMethodAttribute>(testMethodInfo, inherit: false))
        {
            return false;
        }

        bool isAccessible = testMethodInfo.IsPublic
            || (_discoverInternals && testMethodInfo.IsAssembly);

        // Todo: Decide whether parameter count matters.
        bool isValidTestMethod = isAccessible &&
                                 testMethodInfo is { IsAbstract: false, IsStatic: false } &&
                                 testMethodInfo.IsValidReturnType(_reflectHelper);

        if (!isValidTestMethod)
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorIncorrectTestMethodSignature, type.FullName, testMethodInfo.Name);
            warnings.Add(message);
            return false;
        }

        return true;
    }
}
