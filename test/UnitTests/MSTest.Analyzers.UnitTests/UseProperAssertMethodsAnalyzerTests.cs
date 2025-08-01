﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseProperAssertMethodsAnalyzer,
    MSTest.Analyzers.UseProperAssertMethodsFixer>;

namespace MSTest.Analyzers.Test;

// NOTE: tests in this class are intentionally not using the [|...|] markup syntax so that we test the arguments
[TestClass]
public sealed class UseProperAssertMethodsAnalyzerTests
{
    private const string SomeClassWithUserDefinedEqualityOperators = """
        public class SomeClass
        {
            public static bool operator ==(SomeClass x, SomeClass y) => true;
            public static bool operator !=(SomeClass x, SomeClass y) => false;
        }
        """;

    [TestMethod]
    public async Task WhenAssertIsTrueWithEqualsNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsTrue(x == null)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    Assert.IsNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNull' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNull", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenPointerTypesPassedToIsTrueOrIsFalseThenNoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public unsafe void MyTestMethod()
                {
                    {
                        byte* x = null;
                        delegate*<void> y = null;

                        // Assert.IsTrue: null comparisons
                        Assert.IsTrue(x is null);
                        Assert.IsTrue(y is null);
                        Assert.IsTrue(x == null);
                        Assert.IsTrue(y == null);
                        Assert.IsTrue(null == x);
                        Assert.IsTrue(null == y);

                        // Assert.IsTrue: not null comparisons
                        Assert.IsTrue(x is not null);
                        Assert.IsTrue(y is not null);
                        Assert.IsTrue(x != null);
                        Assert.IsTrue(y != null);
                        Assert.IsTrue(null != x);
                        Assert.IsTrue(null != y);

                        // Assert.IsTrue: two pointers equality comparisons
                        Assert.IsTrue(x == x);
                        Assert.IsTrue(x == y);
                        Assert.IsTrue(y == x);
                        Assert.IsTrue(y == y);

                        // Assert.IsTrue: two pointers inequality comparisons
                        Assert.IsTrue(x != x);
                        Assert.IsTrue(x != y);
                        Assert.IsTrue(y != x);
                        Assert.IsTrue(y != y);

                        // Assert.IsFalse: null comparisons
                        Assert.IsFalse(x is null);
                        Assert.IsFalse(y is null);
                        Assert.IsFalse(x == null);
                        Assert.IsFalse(y == null);
                        Assert.IsFalse(null == x);
                        Assert.IsFalse(null == y);
            
                        // Assert.IsFalse: not null comparisons
                        Assert.IsFalse(x is not null);
                        Assert.IsFalse(y is not null);
                        Assert.IsFalse(x != null);
                        Assert.IsFalse(y != null);
                        Assert.IsFalse(null != x);
                        Assert.IsFalse(null != y);
            
                        // Assert.IsFalse: two pointers equality comparisons
                        Assert.IsFalse(x == x);
                        Assert.IsFalse(x == y);
                        Assert.IsFalse(y == x);
                        Assert.IsFalse(y == y);
            
                        // Assert.IsFalse: two pointers inequality comparisons
                        Assert.IsFalse(x != x);
                        Assert.IsFalse(x != y);
                        Assert.IsFalse(y != x);
                        Assert.IsFalse(y != y);

                        // The following is to show that if we try to use IsNull or AreEqual, it will not work.
                        // If this behavior ever changed (either as a result of some language feature, or us adding extra overloads),
                        // then we should update the analyzer to start reporting diagnostic in the relevant cases and have the codefix handle it correctly
                        Assert.IsNull({|#0:x|});
                        Assert.IsNull({|#1:y|});
                        Assert.{|#2:AreEqual|}(null, x);
                        Assert.{|#3:AreEqual|}(null, y);
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                // /0/Test0.cs(15,27): error CS1503: Argument 1: cannot convert from 'byte*' to 'object?'
                DiagnosticResult.CompilerError("CS1503").WithLocation(0).WithArguments("1", "byte*", "object?"),
                // /0/Test0.cs(16,27): error CS1503: Argument 1: cannot convert from 'delegate*<void>' to 'object?'
                DiagnosticResult.CompilerError("CS1503").WithLocation(1).WithArguments("1", "delegate*<void>", "object?"),
                // /0/Test0.cs(17,20): error CS0306: The type 'byte*' may not be used as a type argument
                DiagnosticResult.CompilerError("CS0306").WithLocation(2).WithArguments("byte*"),
                // /0/Test0.cs(18,20): error CS0306: The type 'delegate*<void>' may not be used as a type argument
                DiagnosticResult.CompilerError("CS0306").WithLocation(3).WithArguments("delegate*<void>"),
            ],
            code);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithEqualsNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsTrue(x == null);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithIsNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsTrue(x is null)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    Assert.IsNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNull' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNull", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithIsNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    {|#0:Assert.IsTrue(x is null)|};
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        string fixedCode = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsNull(x);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNull' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNull", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithNotEqualsNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsTrue(x != null)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    Assert.IsNotNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotNull' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotNull", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithNotEqualsNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsTrue(x != null);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithIsNotNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsTrue(x is not null)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    Assert.IsNotNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotNull' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotNull", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithIsNotNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    {|#0:Assert.IsTrue(x is not null)|};
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        string fixedCode = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsNotNull(x);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotNull' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotNull", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithEqualsNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsFalse(x == null)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    Assert.IsNotNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotNull' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotNull", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithEqualsNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsFalse(x == null);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithIsNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsFalse(x is null)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    Assert.IsNotNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotNull' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotNull", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithIsNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    {|#0:Assert.IsFalse(x is null)|};
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        string fixedCode = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsNotNull(x);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotNull' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotNull", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithNotEqualsNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsFalse(x != null)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    Assert.IsNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNull' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNull", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithNotEqualsNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsFalse(x != null);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithIsNotNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsFalse(x is not null)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    Assert.IsNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNull' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNull", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithIsNotNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    {|#0:Assert.IsFalse(x is not null)|};
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        string fixedCode = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsNull(x);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNull' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNull", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueAndArgumentIsEquality()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    object y = new object();
                    {|#0:Assert.IsTrue(x == y)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    object y = new object();
                    Assert.AreEqual(y, x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.AreEqual' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreEqual", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueAndArgumentIsEqualityAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    SomeClass y = new SomeClass();
                    Assert.IsTrue(x == y);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueAndArgumentIsInequality()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    object y = new object();
                    {|#0:Assert.IsTrue(x != y)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    object y = new object();
                    Assert.AreNotEqual(y, x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.AreNotEqual' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreNotEqual", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueAndArgumentIsInequalityAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    SomeClass y = new SomeClass();
                    Assert.IsTrue(x != y);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseAndArgumentIsEquality()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    object y = new object();
                    {|#0:Assert.IsFalse(x == y)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    object y = new object();
                    Assert.AreNotEqual(y, x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.AreNotEqual' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreNotEqual", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseAndArgumentIsEqualityAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    SomeClass y = new SomeClass();
                    Assert.IsFalse(x == y);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseAndArgumentIsInequality()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    object y = new object();
                    {|#0:Assert.IsFalse(x != y)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    object y = new object();
                    Assert.AreEqual(y, x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.AreEqual' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreEqual", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseAndArgumentIsInequalityAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    SomeClass y = new SomeClass();
                    Assert.IsFalse(x != y);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualAndExpectedIsNull()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.AreEqual(null, x)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    Assert.IsNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNull' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNull", "AreEqual"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualAndExpectedIsNull()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.AreNotEqual(null, x)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    Assert.IsNotNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotNull' instead of 'Assert.AreNotEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotNull", "AreNotEqual"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualAndExpectedIsTrue()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.AreEqual(true, x)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    Assert.IsTrue((bool?)x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsTrue' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsTrue", "AreEqual"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualAndExpectedIsTrue_CastNotAddedWhenTypeIsBool()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    bool x = false;
                    {|#0:Assert.AreEqual(true, x)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    bool x = false;
                    Assert.IsTrue(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsTrue' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsTrue", "AreEqual"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualAndExpectedIsTrue_CastNotAddedWhenTypeIsNullableBool()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    bool? x = false;
                    {|#0:Assert.AreEqual(true, x)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    bool? x = false;
                    Assert.IsTrue(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsTrue' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsTrue", "AreEqual"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualAndExpectedIsTrue_CastShouldBeAddedWithParentheses()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Assert.AreEqual<object>(true, new C() + new C())|};
                }
            }

            public class C
            {
                public static object operator +(C c1, C c2)
                    => true;
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Assert.IsTrue((bool?)(new C() + new C()));
                }
            }

            public class C
            {
                public static object operator +(C c1, C c2)
                    => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsTrue' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsTrue", "AreEqual"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualAndExpectedIsTrue()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    // Note: Assert.IsFalse(x) has different semantics. So no diagnostic.
                    // We currently don't produce a diagnostic even if the type of 'x' is boolean.
                    // But we could special case that.
                    Assert.AreNotEqual(true, x);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualAndExpectedIsFalse()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.AreEqual(false, x)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    Assert.IsFalse((bool?)x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsFalse' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsFalse", "AreEqual"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualAndExpectedIsFalse()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    // Note: Assert.IsTrue(x) has different semantics. So no diagnostic.
                    // We currently don't produce a diagnostic even if the type of 'x' is boolean.
                    // But we could special case that.
                    Assert.AreNotEqual(false, x);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    #region New test cases for string methods

    [TestMethod]
    public async Task WhenAssertIsTrueWithStringStartsWith()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string myString = "Hello World";
                    {|#0:Assert.IsTrue(myString.StartsWith("Hello"))|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string myString = "Hello World";
                    Assert.StartsWith("Hello", myString);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.StartsWith' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("StartsWith", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithStringEndsWith()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string myString = "Hello World";
                    {|#0:Assert.IsTrue(myString.EndsWith("World"))|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string myString = "Hello World";
                    Assert.EndsWith("World", myString);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.EndsWith' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("EndsWith", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithStringContains()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string myString = "Hello World";
                    {|#0:Assert.IsTrue(myString.Contains("lo Wo"))|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string myString = "Hello World";
                    Assert.Contains("lo Wo", myString);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.Contains' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("Contains", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithStringStartsWith()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string myString = "Hello World";
                    Assert.IsFalse(myString.StartsWith("Hello"));
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithStringEndsWith()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string myString = "Hello World";
                    Assert.IsFalse(myString.EndsWith("World"));
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithStringContains()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string myString = "Hello World";
                    Assert.IsFalse(myString.Contains("test"));
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    #endregion

    #region New test cases for collection methods

    [TestMethod]
    public async Task WhenAssertIsTrueWithCollectionContains()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    {|#0:Assert.IsTrue(list.Contains(2))|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Assert.Contains(2, list);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.Contains' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("Contains", "IsTrue"),
            fixedCode);
    }

    #endregion

    #region New test cases for comparisons

    [TestMethod]
    public async Task WhenAssertIsTrueWithGreaterThanComparison()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int a = 5;
                    int b = 3;
                    {|#0:Assert.IsTrue(a > b)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int a = 5;
                    int b = 3;
                    Assert.IsGreaterThan(b, a);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsGreaterThan' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsGreaterThan", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithGreaterThanComparison()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int a = 5;
                    int b = 3;
                    {|#0:Assert.IsFalse(a > b)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int a = 5;
                    int b = 3;
                    Assert.IsLessThanOrEqualTo(b, a);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsLessThanOrEqualTo' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsLessThanOrEqualTo", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithEqualsComparison()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int a = 5;
                    int b = 5;
                    {|#0:Assert.IsTrue(a == b)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int a = 5;
                    int b = 5;
                    Assert.AreEqual(b, a);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.AreEqual' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreEqual", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithEqualsComparison()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int a = 5;
                    int b = 3;
                    {|#0:Assert.IsFalse(a == b)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int a = 5;
                    int b = 3;
                    Assert.AreNotEqual(b, a);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.AreNotEqual' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreNotEqual", "IsFalse"),
            fixedCode);
    }

    #endregion

    #region New test cases for collection count

    [TestMethod]
    public async Task WhenAssertAreEqualWithCollectionCountZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int>();
                    {|#0:Assert.AreEqual(0, list.Count)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int>();
                    Assert.IsEmpty(list);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsEmpty' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsEmpty", "AreEqual"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualWithCollectionCountNonZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    {|#0:Assert.AreEqual(3, list.Count)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Assert.HasCount(3, list);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.HasCount' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("HasCount", "AreEqual"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualWithCollectionCountZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int>();
                    Assert.AreNotEqual(0, list.Count);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualWithCollectionCountNonZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Assert.AreNotEqual(3, list.Count);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualWithArrayLength()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var array = new int[] { 1, 2, 3, 4, 5 };
                    Assert.AreNotEqual(5, array.Length);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualWithCollectionCountAndMessage_HasCount()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var myCollection = new List<int> { 1, 2 };
                    {|#0:Assert.AreEqual(2, myCollection.Count, "Wrong number of elements")|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var myCollection = new List<int> { 1, 2 };
                    Assert.HasCount(2, myCollection, "Wrong number of elements");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.HasCount' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("HasCount", "AreEqual"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualWithCollectionCountZeroAndMessage_IsEmpty()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int>();
                    {|#0:Assert.AreEqual(0, list.Count, "Collection should be empty")|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int>();
                    Assert.IsEmpty(list, "Collection should be empty");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsEmpty' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsEmpty", "AreEqual"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualWithCollectionCountAndMultipleParameters()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    {|#0:Assert.AreEqual(3, list.Count, "Wrong count: expected {0} but was {1}", 3, list.Count)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Assert.HasCount(3, list, "Wrong count: expected {0} but was {1}", 3, list.Count);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.HasCount' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("HasCount", "AreEqual"),
            fixedCode);
    }

    #endregion
}
