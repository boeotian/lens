using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class FunctionalTest : TestBase
    {
        [Test]
        public void CreateFunctionObjectFromName()
        {
            var src = @"
var fx = double::IsInfinity
fx (1.0 / 0)";

            Test(src, true, true);
        }

        [Test]
        public void Closure1()
        {
            var src = @"
var a = 0
var b = 2
var fx = x:int -> a = b * x
fx 3
a";
            Test(src, 6);
        }

        [Test]
        public void Closure2()
        {
            var src = @"
var result = 0
var x1 = 1
var fx1 = a:int ->
    x1 = x1 + 1
    var x2 = 1
    var fx2 = b:int ->
        x2 = x2 + 1
        result = x1 + x2 + b
    fx2 a
fx1 10
result";
            Test(src, 14);
        }

        [Test]
        public void ClosureError1()
        {
            var src = @"
fun doStuff:Func<int, int> (x:ref int) ->
    (y:int -> x + y)
";
            TestError(src, CompilerMessages.ClosureRef);
        }

        [Test]
        public void RefValueError1()
        {
            var src = @"
fun inc (x:ref int) ->
    x = x + 1

let value = 1
inc (ref value)
";
            TestError(src, CompilerMessages.ConstantByRef);
        }

        [Test]
        public void RefValueError2()
        {
            var src = @"
fun inc (x:ref int) ->
    x = x + 1

var value = 1
inc value
";
            TestError(src, CompilerMessages.ReferenceArgExpected);
        }

        [Test]
        public void RefValueError3()
        {
            var src = @"
fun doStuff (x:int) ->
    print x

var value = 1
doStuff (ref value)
";
            TestError(src, CompilerMessages.ReferenceArgUnexpected);
        }

        [Test]
        public void FunctionComposition1()
        {
            var src = @"
let add = (a:int b:int) -> a + b
let square = x:int -> x ** 2 as int
let inc = x:int -> x + 1

let asi = add :> square :> inc
asi 1 2
";

            Test(src, 10);
        }

        [Test]
        public void FunctionComposition2()
        {
            var src = @"
let invConcat = (x:string y:string) -> y + x
let invParse = invConcat :> int::Parse
invParse ""37"" ""13""
";

            Test(src, 1337);
        }

        [Test]
        public void FunctionComposition3()
        {
            var src = @"
fun invConcat:string (x:string y:string) -> y + x
let invParse = invConcat :> int::Parse
invParse ""37"" ""13""
";

            Test(src, 1337);
        }

        [Test]
        public void FunctionCompositionError1()
        {
            var src = @"
let fx1 = (x:string) -> x
let fx2 = (x:int) -> x
fx1 :> fx2
";

            TestError(src, CompilerMessages.DelegatesNotCombinable);
        }

        [Test]
        public void Wildcards1()
        {
            var src = @"
let fx1 = string::Format <_, object[]> as object
let fx2 = string::Join <string, _, _, _> as object
let fx3 = int::Parse <_> as object
new [fx1; fx2; fx3]
    |> Where x -> x <> null
    |> Count ()
";
            Test(src, 3);
        }

        [Test]
        public void WildcardsError1()
        {
            var src = @"int::Parse <_, _>";
            TestError(src, CompilerMessages.TypeMethodAmbiguous);
        }

        [Test]
        public void WildcardsError2()
        {
            var src = @"int::Parse <string, string>";
            TestError(src, CompilerMessages.TypeMethodNotFound);
        }

        [Test]
        public void PartialApplication1()
        {
            var src = @"
fun add:int (x:int y:int) -> x + y
let add2 = add 2 _
let add3 = add _ 3
(add2 1) + (add3 4)
";
            Test(src, 10);
        }

        [Test]
        public void PartialApplication2()
        {
            var src = @"
let prepend = string::Concat ""test:"" _
prepend ""hello""
";
            Test(src, "test:hello");
        }

        [Test]
        public void PartialApplication3()
        {
            var src = @"
fun add:int (x:int y:int z:int) -> x + y + z
let fx1 = add 1 _ _
let fx2 = fx1 2 _
fx2 3
";
            Test(src, 6);
        }

        [Test]
        public void PartialApplicationError1()
        {
            var src = @"
fun add:int (x:int y:int) -> x + y
add _
";
            TestError(src, CompilerMessages.FunctionNotFound);
        }

        [Test]
        public void PartialApplicationError2()
        {
            var src = @"
fun add:int (x:int y:int) -> x + y
add _ _ _
";
            TestError(src, CompilerMessages.FunctionNotFound);
        }

        [Test]
        public void ConstructorApplication1()
        {
            var src = @"
let repeater = new string (""a""[0]) _
new [repeater 2; repeater 3]
";
            Test(src, new[] {"aa", "aaa"});
        }

        [Test]
        public void ConstructorApplication2()
        {
            var src = @"
record Data
    Value : int
    Coeff : int

let dataFx = new Data _ 2
new [dataFx 1; dataFx 2; dataFx 3]
    |> Sum x:Data -> x.Value * x.Coeff
";
            Test(src, 12);
        }

        [Test]
        public void ConstructorApplicationError1()
        {
            var src = @"
record Data
    Value : int
    Coeff : int

new Data _";

            TestError(src, CompilerMessages.TypeConstructorNotFound);
        }

        [Test]
        public void ConstructorApplicationError2()
        {
            var src = @"
record Data
    Value : int
    Coeff : int

new Data _ _ _";

            TestError(src, CompilerMessages.TypeConstructorNotFound);
        }

        [Test]
        public void FunctionUnneededArgs()
        {
            var src = @"
fun sum:int (a:int b:int) -> a + b
fun sum:int (a:int b:int _:int) -> a + b
fun sum:int (a:int b:int _:int _:int) -> a + b

new [
    sum 1 2
    sum 1 2 3
    sum 1 2 3 4
]
";
            Test(src, new[] {3, 3, 3});
        }

        [Test]
        public void LambdaUnneededArgs()
        {
            var src = @"
var x : Func<int, int, int>
var y : Func<int, int, int>
x = (_ _) -> 1
y = (_ b) -> b + 1
new [x 1 2; y 1 2]
";
            Test(src, new[] {1, 3});
        }

        [Test]
        public void OverloadPartialApplicationError()
        {
            var src = "println _";
            TestError(src, CompilerMessages.FunctionInvocationAmbiguous);
        }
    }
}