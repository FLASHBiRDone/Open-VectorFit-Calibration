using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json.Linq;

// Small process boundary around the unchanged official Raw Accel 1.7.1 runtime.
class Bridge {
    static int Main(string[] args) {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        try {
            if (args.Length == 0) throw new ArgumentException("Missing command");
            if (args[0] == "default") { Console.Write(DriverConfig.GetDefault().ToJSON()); return 0; }
            if (args[0] == "validate" || args[0] == "apply") {
                var parsed = DriverConfig.Convert(File.ReadAllText(args[1]));
                if (parsed.Item2 != null) throw new Exception(parsed.Item2);
                if (args[0] == "validate") { Console.Write(parsed.Item1.ToJSON()); return 0; }
                VersionHelper.ValidOrThrow();
                parsed.Item1.Activate();
                Thread.Sleep(1250);
                var actual = DriverConfig.GetActive();
                if (!JToken.DeepEquals(JObject.Parse(parsed.Item1.ToJSON()), JObject.Parse(actual.ToJSON())))
                    throw new Exception("Driver readback differs from requested settings. Restore the backup.");
                Console.Write(actual.ToJSON()); return 0;
            }
            if (args[0] == "read") {
                VersionHelper.ValidOrThrow(); Console.Write(DriverConfig.GetActive().ToJSON()); return 0;
            }
            if (args[0] == "simulate") {
                var parsed = DriverConfig.Convert(File.ReadAllText(args[1]));
                if (parsed.Item2 != null) throw new Exception(parsed.Item2);
                var accel = new ManagedAccel(parsed.Item1.profiles[0]);
                for (int i=0; i<8; i++) {
                    double a = i*Math.PI/4;
                    int x=(int)Math.Round(1000*Math.Cos(a)), y=(int)Math.Round(1000*Math.Sin(a));
                    var p=accel.Accelerate(x,y,1,1);
                    Console.WriteLine(x+","+y+":"+p.Item1.ToString("R", System.Globalization.CultureInfo.InvariantCulture)+","+p.Item2.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                }
                return 0;
            }
            throw new ArgumentException("Unknown command");
        } catch (Exception e) { Console.Error.WriteLine(e.GetType().Name+": "+e.Message); return 2; }
    }
}
