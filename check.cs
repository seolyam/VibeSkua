using System;
using System.Reflection;
using Velopack;

class Program {
    static void Main() {
        var t = typeof(VelopackLocator);
        foreach (var c in t.GetConstructors()) {
            Console.WriteLine(c);
        }
        Console.WriteLine("---");
        var m = typeof(UpdateManager).GetConstructors();
        foreach (var c in m) {
            Console.WriteLine(c);
        }
    }
}
