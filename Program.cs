using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;

using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using System.Diagnostics;
using System.CodeDom.Compiler;

namespace CompilerAssemblyForCloneObject
{
    public class Program
    {
        public static void Main()
        {
            //Test performances
            Console.WriteLine("\n=========================\n");
            Console.WriteLine("Compare Performance\n");
            Console.WriteLine("Approach".PadRight(30) + "First Call".PadRight(20) + "Second Call".PadRight(20) + "100 Calls".PadRight(20));

            CheckPerformance(Program.CloneWithSystemTextJsonSerializer, "System.Text.Json Serializer");
            CheckPerformance(Program.CloneWithJsonNetSerializer, "Json.NET Serializer");
            CheckPerformance(Program.CloneWithBinarySerializer, "Binary Serializer");
            CheckPerformance(Program.CloneWithICloneable, "ICloneable");
            CheckPerformance(Program.CloneWithReflection, "Reflection");
            //CheckPerformance(Program.CloneWithExpression, "Expression");
            CheckPerformance("RuntimeCompiled");


            //Aproach 1: using Json Serializer
            //Json Serializer approach is a bit lighter then Binary Seriliazer and does not require Serializable attribute on all objects that you would like to copy
            CreateCloneAndTest(Program.CloneWithSystemTextJsonSerializer, "System.Text.Json Serializer");
            CreateCloneAndTest(Program.CloneWithJsonNetSerializer, "Json.NET Serializer");

            //Aproach 2: using Binary Serializer
            //When using Binary Serializer Customer class must be Serializable, aka have a Serializable attribute. As well as all properties in customer that you would like to deep-copy.
            //What is nice about it is that you don't have to add Cloning code to each object that you would like to serialize.  Just add Serialize attribute
            CreateCloneAndTest(Program.CloneWithBinarySerializer, "Binary Serializer");

            //Aproach 3: using IClonebale
            //ICloneable approach is to implement ICloneable.Clone() method on classes you would like to clone
            //It is shallow and not type safe, but interface is included in .NET
            //Internally it uses MemberWise for shallow copy
            CreateCloneAndTest(Program.CloneWithICloneable, "ICloneable");

            //Aproach 4: using Reflection
            //Clone with Reflection requires a parameterless constructor defined in all classes being cloned. So that Activator.CreateInstance would work
            CreateCloneAndTest(Program.CloneWithReflection, "Reflection");

            //CreateCloneAndTest(Program.CloneWithExpression, "Expression");

            //CloneObjectWithAssembly(null);

            Console.ReadLine();
        }

        public static void CheckPerformance(string approachName)
        {
            #region CheckPerformance
            Customer customer = CreateCustomer();
            var RuntimeCompiledClone = CloneWithRuntimeCompiler(customer);

            //First
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            _ = RuntimeCompiledClone.GetType().GetMethod("Clone").Invoke(RuntimeCompiledClone, new object[] { customer }) as Customer;

            stopwatch.Stop();
            var firstDuration = stopwatch.Elapsed;


            //Second
            stopwatch.Reset();
            stopwatch.Start();

            _ = RuntimeCompiledClone.GetType().GetMethod("Clone").Invoke(RuntimeCompiledClone, new object[] { customer }) as Customer;

            stopwatch.Stop();

            var secondDuration = stopwatch.Elapsed;

            //100
            stopwatch.Reset();

            stopwatch.Start();

            for (int i = 0; i <= 100; i++)
            {
                _ = RuntimeCompiledClone.GetType().GetMethod("Clone").Invoke(RuntimeCompiledClone, new object[] { customer }) as Customer;
            }

            stopwatch.Stop();

            var oneHundredDuration = stopwatch.Elapsed;
            Console.WriteLine(approachName.PadRight(30) + (firstDuration.TotalMilliseconds + "ms").PadRight(20) + (secondDuration.TotalMilliseconds + "ms").PadRight(20) + (oneHundredDuration.TotalMilliseconds + "ms").PadRight(20));
            #endregion CheckPerformance


            #region 檢視
            Console.WriteLine("\n=========================\n");
            Console.WriteLine("Approach: " + approachName + Environment.NewLine);

            var clone = RuntimeCompiledClone.GetType().GetMethod("Clone").Invoke(RuntimeCompiledClone, new object[] { customer }) as Customer;

            //Test
            if ((customer.LastName == clone.LastName) &&
                    (customer.Address.City == clone.Address.City) &&
                    (customer.Address.State.TwoLetterCode == clone.Address.State.TwoLetterCode))
            {
                Console.WriteLine("Result: Success");
            }
            else
            {
                Console.WriteLine("Result: Failed");
            }

            Console.WriteLine();
            Console.WriteLine("Clone: " + Environment.NewLine + JsonSerializer.Serialize(clone, new JsonSerializerOptions { WriteIndented = true }));
            #endregion 檢視
        }

        public static object CloneWithRuntimeCompiler(Customer customer)
        {
            var sourceFile = string.Empty;

            sourceFile = @"
using CompilerAssemblyForCloneObject;

namespace Samples
{
   public class RuntimeCompiledClone
    {
            public static CompilerAssemblyForCloneObject.Program.Customer Clone(CompilerAssemblyForCloneObject.Program.Customer source){
                     var clone = new CompilerAssemblyForCloneObject.Program.Customer(){
                          Id = source.Id,
                          FirstName = source.FirstName + ""-Cloned"",
                          LastName = source.LastName,
                          Address = new CompilerAssemblyForCloneObject.Program.Address
                          {
                               Street = source.Address.Street,
                               City = source.Address.City,
                               State = new CompilerAssemblyForCloneObject.Program.State { Name = source.Address.State.Name, TwoLetterCode = source.Address.State.TwoLetterCode }
                          },
                     };

                     return clone;
            }
    }
}
";

            var provider = CodeDomProvider.CreateProvider("CSharp");

            CompilerParameters cp = new CompilerParameters();

            // Generate an executable instead of
            // a class library.
            cp.GenerateExecutable = false;

            // Set the assembly file name to generate.
            //cp.OutputAssembly = exeFile;

            // Generate debug information.
            cp.IncludeDebugInformation = true;

            // Add an assembly reference.
            cp.ReferencedAssemblies.Add("System.dll");
            cp.ReferencedAssemblies.Add("CompilerAssemblyForCloneObject.exe");

            // Save the assembly as a physical file.
            cp.GenerateInMemory = true;

            // Set the level at which the compiler
            // should start displaying warnings.
            cp.WarningLevel = 3;

            // Set whether to treat all warnings as errors.
            cp.TreatWarningsAsErrors = false;

            // Set compiler argument to optimize output.
            cp.CompilerOptions = "/optimize";

            // Set a temporary files collection.
            // The TempFileCollection stores the temporary files
            // generated during a build in the current directory,
            // and does not delete them after compilation.
            cp.TempFiles = new TempFileCollection(".", false);

            // Invoke compilation.
            CompilerResults cr = provider.CompileAssemblyFromSource(cp, sourceFile);

            if (cr.Errors.Count > 0)
            {
                throw new Exception("Compiler Error.");
            }
            else
            {
                return cr.CompiledAssembly.CreateInstance("Samples.RuntimeCompiledClone");
            }
        }

        public static object CloneObjectWithAssembly(object obj)
        {
            var sourceFile = string.Empty;

            sourceFile = @"
using CompilerAssemblyForCloneObject;

namespace Samples
{
   public class CompiledClone
    {
            public static CompilerAssemblyForCloneObject.Program.Customer Run(CompilerAssemblyForCloneObject.Program.Customer source){
                     var clone = new CompilerAssemblyForCloneObject.Program.Customer(){
                          Id = source.Id,
                          FirstName = source.FirstName + ""-Cloned"",
                          LastName = source.LastName,
                          Address = new CompilerAssemblyForCloneObject.Program.Address
                          {
                               Street = source.Address.Street,
                               City = source.Address.City,
                               State = new CompilerAssemblyForCloneObject.Program.State { Name = source.Address.State.Name, TwoLetterCode = source.Address.State.TwoLetterCode }
                          },
                     };

                     return clone;
            }
    }
}
";

            var provider = CodeDomProvider.CreateProvider("CSharp");

            CompilerParameters cp = new CompilerParameters();

            // Generate an executable instead of
            // a class library.
            cp.GenerateExecutable = false;

            // Set the assembly file name to generate.
            //cp.OutputAssembly = exeFile;

            // Generate debug information.
            cp.IncludeDebugInformation = true;

            // Add an assembly reference.
            cp.ReferencedAssemblies.Add("System.dll");
            cp.ReferencedAssemblies.Add("CompilerAssemblyForCloneObject.exe");

            // Save the assembly as a physical file.
            cp.GenerateInMemory = true;

            // Set the level at which the compiler
            // should start displaying warnings.
            cp.WarningLevel = 3;

            // Set whether to treat all warnings as errors.
            cp.TreatWarningsAsErrors = false;

            // Set compiler argument to optimize output.
            cp.CompilerOptions = "/optimize";

            // Set a temporary files collection.
            // The TempFileCollection stores the temporary files
            // generated during a build in the current directory,
            // and does not delete them after compilation.
            cp.TempFiles = new TempFileCollection(".", true);

            if (provider.Supports(GeneratorSupport.AssemblyAttributes))
            {
                // Specify the class that contains
                // the main method of the executable.
                cp.MainClass = "Samples.CompiledClone";
            }

            if (Directory.Exists("Resources"))
            {
                if (provider.Supports(GeneratorSupport.Resources))
                {
                    // Set the embedded resource file of the assembly.
                    // This is useful for culture-neutral resources,
                    // or default (fallback) resources.
                    cp.EmbeddedResources.Add("Resources\\Default.resources");

                    // Set the linked resource reference files of the assembly.
                    // These resources are included in separate assembly files,
                    // typically localized for a specific language and culture.
                    cp.LinkedResources.Add("Resources\\nb-no.resources");
                }
            }

            // Invoke compilation.
            CompilerResults cr = provider.CompileAssemblyFromSource(cp, sourceFile);

            if (cr.Errors.Count > 0)
            {
                // Display compilation errors.
                Console.WriteLine("Errors building {0} into {1}",
                    sourceFile, cr.PathToAssembly);
                foreach (CompilerError ce in cr.Errors)
                {
                    Console.WriteLine("  {0}", ce.ToString());
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("Source {0} built into {1} successfully.",
                    sourceFile, cr.PathToAssembly);
                Console.WriteLine("{0} temporary files created during the compilation.",
                    cp.TempFiles.Count.ToString());

                var Class1 = cr.CompiledAssembly.CreateInstance("Samples.CompiledClone");

                //var ret = Class1.GetType().GetMethod("Run").Invoke(Class1, new object[] { 32 });

                Customer customer = CreateCustomer();

                var ret = Class1.GetType().GetMethod("Run").Invoke(Class1, new object[] { customer }) as Customer;

                Console.WriteLine("{0} -- {1}", ret.Id, ret.FirstName);
                Console.WriteLine();
            }

            // Return the results of compilation.
            if (cr.Errors.Count > 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }


        public static Customer CloneWithSystemTextJsonSerializer(Customer customer)
        {
            var clone = CloneGenericWithSystemTextJsonSerializer(customer);
            return clone;
        }


        public static T CloneGenericWithSystemTextJsonSerializer<T>(T source)
        {
            if (source == null)
                return default(T);

            var cloneJson = JsonSerializer.Serialize(source);
            var clone = JsonSerializer.Deserialize<T>(cloneJson);

            return clone;
        }

        public static Customer CloneWithJsonNetSerializer(Customer customer)
        {
            var clone = CloneGenericWithJsonNetSerializer(customer);
            return clone;
        }

        public static T CloneGenericWithJsonNetSerializer<T>(T source)
        {
            if (!typeof(T).IsSerializable)
                throw new ArgumentException("The type must be serializable.", nameof(source));

            if (source == null)
                return default(T);

            var cloneJson = Newtonsoft.Json.JsonConvert.SerializeObject(source);
            var clone = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(cloneJson);

            return clone;
        }

        public static Customer CloneWithBinarySerializer(Customer customer)
        {
            var clone = CloneGenericWithBinarySerializer(customer);
            return clone;
        }


        public static T CloneGenericWithBinarySerializer<T>(T source)
        {
            if (!typeof(T).IsSerializable)
                throw new ArgumentException("The type must be serializable.", nameof(source));

            if (source == null)
                return default(T);

            IFormatter formatter = new BinaryFormatter();
            Stream stream = new MemoryStream();
            using (stream)
            {
                formatter.Serialize(stream, source);
                stream.Seek(0, SeekOrigin.Begin);
                return (T)formatter.Deserialize(stream);
            }
        }


        public static Customer CloneWithICloneable(Customer customer)
        {
            var clone = (Customer)customer.Clone();
            return clone;
        }


        public static Customer CloneWithReflection(Customer customer)
        {
            var clone = (Customer)CloneObjectWithReflection(customer);
            return clone;
        }
        //public static Customer CloneWithExpression(Customer customer)
        //{
        //    var clone = (Customer)CloneHelper.Clone(customer);
        //    return clone;
        //}

        //Use Activator.CreateInstance and PropertyInfo to copy all data to clone clone all data
        public static object CloneObjectWithReflection(object obj)
        {
            Type type = obj.GetType();

            object clone = Activator.CreateInstance(type);

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var property in properties)
            {
                if (property.CanWrite)
                {
                    if (property.PropertyType.IsValueType || property.PropertyType.IsEnum || property.PropertyType.Equals(typeof(System.String)))
                    {
                        property.SetValue(clone, property.GetValue(obj, null), null);
                    }
                    else
                    {

                        var objPropertyValue = property.GetValue(obj, null);

                        if (objPropertyValue == null)
                            property.SetValue(clone, null, null);
                        else
                            property.SetValue(clone, CloneObjectWithReflection(objPropertyValue), null);
                    }
                }
            }

            return clone;
        }


        public static void CreateCloneAndTest(Func<Customer, Customer> cloneFunction, string approachName)
        {
            Console.WriteLine("\n=========================\n");
            Console.WriteLine("Approach: " + approachName + Environment.NewLine);

            var customer = CreateCustomer();
            var clone = cloneFunction(customer);

            //Test
            if ((customer.LastName == clone.LastName) &&
                    (customer.Address.City == clone.Address.City) &&
                    (customer.Address.State.TwoLetterCode == clone.Address.State.TwoLetterCode))
            {
                Console.WriteLine("Result: Success");
            }
            else
            {
                Console.WriteLine("Result: Failed");
            }

            Console.WriteLine();
            Console.WriteLine("Clone: " + Environment.NewLine + JsonSerializer.Serialize(clone, new JsonSerializerOptions { WriteIndented = true }));
        }



        public static void CheckPerformance(Func<Customer, Customer> cloneFunction, string approachName)
        {

            var customer = CreateCustomer();

            //First
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var clone = cloneFunction(customer);
            stopwatch.Stop();
            var firstDuration = stopwatch.Elapsed;


            //Second
            stopwatch.Reset();
            stopwatch.Start();

            clone = cloneFunction(customer);
            stopwatch.Stop();

            var secondDuration = stopwatch.Elapsed;

            //100
            stopwatch.Reset();

            stopwatch.Start();

            for (int i = 0; i <= 100; i++)
            {
                clone = cloneFunction(customer);
            }

            stopwatch.Stop();

            var oneHundredDuration = stopwatch.Elapsed;
            Console.WriteLine(approachName.PadRight(30) + (firstDuration.TotalMilliseconds + "ms").PadRight(20) + (secondDuration.TotalMilliseconds + "ms").PadRight(20) + (oneHundredDuration.TotalMilliseconds + "ms").PadRight(20));
        }
        //public static class CloneHelper
        //{
        //    private static Dictionary<Type, Func<object, object>> _method = new Dictionary<Type, Func<object, object>>();
        //    private static Dictionary<Type, LambdaExpression> _expression = new Dictionary<Type, LambdaExpression>();

        //    public static T Clone<T>(T target) where T : class, new()
        //    {
        //        var type = typeof(T);
        //        var method = _method.TryGetValue(type, out var clone) ? clone : CreateFactory<T>();
        //        return (T)method(target);
        //    }

        //    private static Func<object, object> CreateFactory<T>()
        //    {
        //        var method = (Func<T, T>)GetOrCreateClone(typeof(T)).Compile();
        //        _method.Add(typeof(T), result);
        //        return result;
        //        object result(object input) => method((T)input);
        //    }

        //    private static LambdaExpression GetOrCreateClone(Type type)
        //        => _expression.TryGetValue(type, out var exp) ? exp : CreateClone(type);

        //    private static LambdaExpression CreateClone(Type type)
        //    {
        //        var returnTarget = Expression.Label(type);
        //        var output = Expression.Variable(type, type.Name + "Output");
        //        var input = Expression.Parameter(type, type.Name + "Input");
        //        var result = Expression.Lambda(Expression.Block(
        //            type,
        //            new[] { output },
        //            type.GetProperties().Select<PropertyInfo, Expression>(prop =>
        //            {
        //                var propInput = Expression.Property(input, prop);
        //                var propOutput = Expression.Property(output, prop);
        //                var parameterInput = Expression.Parameter(prop.PropertyType);

        //                return Expression.Assign(
        //                    propOutput,
        //                    prop.PropertyType.IsValueType || prop.PropertyType == typeof(string)
        //                        ? (Expression)propInput
        //                        : Expression.Convert(
        //                            Expression.Invoke(
        //                                GetOrCreateClone(prop.PropertyType),
        //                                propInput),
        //                            prop.PropertyType));
        //            })
        //                .Prepend(Expression.Assign(output, Expression.New(type.GetConstructor(Type.EmptyTypes))))
        //                .Append(Expression.Label(returnTarget, output))), input);
        //        _expression.Add(type, result);
        //        return result;
        //    }
        //}

        public static Customer CreateCustomer()
        {
            return new Customer
            {
                Id = "Customer1",
                FirstName = "Johnny",
                LastName = "Five",
                Address = new Address
                {
                    Street = "197 Hume Ave",
                    City = "Astoria",
                    State = new State { Name = "Oregon", TwoLetterCode = "OR" }
                },
            };
        }

        [Serializable]
        public class Customer : ICloneable
        {
            public string Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public Address Address { get; set; }

            public object Clone()
            {
                var newCustomer = (Customer)this.MemberwiseClone();
                newCustomer.Address = (Address)this.Address.Clone();

                return newCustomer;
            }
        }

        [Serializable]
        public class Address : ICloneable
        {
            public string Street { get; set; }
            public string City { get; set; }
            public State State { get; set; }

            public object Clone()
            {
                var newAddress = (Address)this.MemberwiseClone();
                newAddress.State = (State)this.State.Clone();

                return newAddress;
            }
        }

        [Serializable]
        public class State : ICloneable
        {
            public string Name { get; set; }
            public string TwoLetterCode { get; set; }

            public object Clone()
            {
                var newState = (State)this.MemberwiseClone();
                return newState;
            }
        }
    }
}