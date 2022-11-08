using System.Reflection;
using System.Runtime.Loader;

if (args.Length < 3)
{
    DisplayUsage();
    return 1;
}

Assembly assembly;
try
{
    AssemblyLoadContext loadContext = AssemblyLoadContext.Default;

    var assembliesPath = Path.GetDirectoryName(args[0]);
    loadContext.Resolving += (context, name) => {
        string assemblyPath = $"{assembliesPath}\\{name.Name}.dll";
        return context.LoadFromAssemblyPath(assemblyPath);
    };

    assembly = loadContext.LoadFromAssemblyPath(args[0]);
}
catch(Exception e)
{
    Console.WriteLine($"Failed to load {args[0]}: {e}");
    return 2;
}

Type classType;
try
{
    var typeWithNull = assembly.GetType(args[1]);
    if (typeWithNull == null)
    {
        Console.WriteLine($"Type {args[1]} not found.");
        return 3;
    }
    classType = typeWithNull;
}
catch(Exception e)
{
    Console.WriteLine($"Failed to get type {args[1]}: {e}");
    return 4;
}

int nbrStringParam = args.Length - 3;

MethodInfo methodInfo;
try
{
    var argTypes = new Type[nbrStringParam];
    for (int i = 0; i < nbrStringParam; ++i)
    {
        argTypes[i] = typeof(string);
    }

    var methodInfoWithNull = classType.GetMethod(args[2], argTypes);
    if (methodInfoWithNull == null)
    {
        Console.WriteLine($"Cannot find method named {args[2]} with {nbrStringParam} string parameters in type " +
            $"{args[1]}.");
        return 5;
    }
    methodInfo = methodInfoWithNull;
}
catch(Exception e)
{
    Console.WriteLine($"Failed to getting method {args[2]}: {e}");
    return 6;
}

var methodArgs = new object[nbrStringParam];
for (int i = 0; i < nbrStringParam; ++i)
{
    methodArgs[i] = args[i + 3];
}

if (methodInfo.ReturnParameter.ParameterType == typeof(int))
{
    return (int)methodInfo.Invoke(null, methodArgs)!;
}
else
{
    methodInfo.Invoke(null, methodArgs);
    return 0;
}

void DisplayUsage()
{
    Console.WriteLine("Executes a static method of an assembly (in its own process)");
    Console.WriteLine();
    Console.WriteLine("AssemblyRun AssemblyPath ClassName MethodName [String Arg] [String Arg] ...");
    Console.WriteLine();
    Console.WriteLine("  AssemblyPath Path to the assembly to load and execute.");
    Console.WriteLine("  ClassName    Fully qualified name (class name and namespace) of the class ");
    Console.WriteLine("               containing the static method.");
    Console.WriteLine("  MethodName   Name of the method to execute.");
    Console.WriteLine("  String Arg   All following arguments are strings to be passed to the static");
    Console.WriteLine("               method executed.");
}
