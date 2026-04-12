using System;
using System.Linq;
using System.Reflection;

var asm = Assembly.LoadFrom(@"C:\Users\miken\.nuget\packages\aspire.hosting.keycloak\13.2.2-preview.1.26207.2\lib\net8.0\Aspire.Hosting.Keycloak.dll");
foreach (var t in asm.GetExportedTypes().OrderBy(t => t.FullName))
{
    Console.WriteLine("TYPE: " + t.FullName + " : " + t.BaseType?.Name);
    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        Console.WriteLine("  PROP: " + p.Name + " : " + p.PropertyType.Name);
}
