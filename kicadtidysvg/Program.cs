using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Diagnostics;
using System.Globalization;

namespace kicadtidysvg
{

    static class Program
    {

        static void Main(string[] args)
        {
            Console.WriteLine("INFO: This is kicadtidysvg, a tool to tidy up SVG PCB files produced by KICAD in order to use them with inkscape or gcode generators" + Environment.NewLine);
 
            //Check args
            if(args.Length < 1)
            {
                Console.WriteLine("IO:   Error, no input file specified");
                Environment.Exit(1);
            }
            else
            {
                if(!File.Exists(args[0]))
                {
                    Console.WriteLine("IO:   Error, input file does not exist");
                    Environment.Exit(1);
                }
            }

            string outputFile = args[0].Replace(".svg", "-tidy.svg");
            if (args.Length < 2) Console.WriteLine("IO:   Warning, no output file specified, inferring " + outputFile);
            else outputFile = args[2];

            double circleTres = 600;
            if (args.Length < 3) Console.WriteLine("IO:   Warning, no circle size threshold specified, using default " + circleTres);
            else
            {
                try { circleTres = double.Parse(args[2], CultureInfo.InvariantCulture); }
                catch (Exception ex) { Console.WriteLine("IO:   Warning, invalid circle size threshold specified, using default " + circleTres + ": " + ex.Message); }
            }

            //Read and parse input file
            XDocument xmlDoc = null;
            try
            {
                Console.WriteLine("IO:   Loading " + args[0]);
                xmlDoc = XDocument.Load(args[0]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("XML:  Error loading file: " + ex.Message);
                Environment.Exit(1);
            }
            XNamespace xmlns = xmlDoc.Root.GetDefaultNamespace();

            //Edit groups
            try
            {
                Console.WriteLine("XML:  Removing empty groups");
                xmlDoc.Root.Elements().Where(p => p.Name.LocalName == "g" && p.Elements().Count() == 0).Remove();
                Console.WriteLine("XML:  Grouping and unifying groups by style");
                Console.WriteLine("XML:  Converting circular line caps to actual circles");
                IEnumerable<XElement> newGroups = xmlDoc.Root.Elements()
                    .Where(p => p.Name.LocalName == "g")
                    .GroupBy(k => k.Attribute("style").Value, p => p.Elements())
                    .Select(p => (new XElement(xmlns + "g",
                        p.SelectMany(a => a), 
                        new XAttribute("style", p.Key))))
                    .ToList() //fetch
                    .Select(p => (double.Parse(p.Attribute("style").Value.ToStyleDictionary()["stroke-width"], CultureInfo.InvariantCulture) < circleTres)?
                        p : new XElement(xmlns + "g", 
                                new XAttribute("style", 
                                        p.Attribute("style").Value.ToStyleDictionary()
                                            .UpdateMany(new Dictionary<string, string>() {
                                                { "stroke-width", "0" }, { "fill-opacity", "1.0" } })
                                            .ToStyleString()),
                                    p.Elements()
                                        .Select(b => new XElement(xmlns + "circle",
                                            new XAttribute("cx", b.Attribute("d").Value.Split(' ')[0].Substring(1)),
                                            new XAttribute("cy", b.Attribute("d").Value.Split(' ')[1]),
                                            new XAttribute("r", double.Parse(p.Attribute("style").Value.ToStyleDictionary()["stroke-width"], CultureInfo.InvariantCulture) / 2.0)))));

                Console.WriteLine("XML:  Updating root");
                xmlDoc.Root.Elements().Where(p => p.Name.LocalName == "g").Remove();
                xmlDoc.Root.Add(newGroups);
            }
            catch (Exception ex)
            {
                Console.WriteLine("XML:  Error editing groups: " + ex.Message);
                Environment.Exit(1);
            }

            //Write new file
            try
            {
                Console.WriteLine("IO:   Writing new file to " + outputFile);
                xmlDoc.Save(outputFile);
            }
            catch(Exception ex)
            {
                Console.WriteLine("IO:   Error, cannot write file: " + ex.Message);
                Environment.Exit(1);
            }

            Console.WriteLine("INFO: Done, Thank you" + Environment.NewLine);
        }

        //Extension methods

        private static IDictionary<string, string> ToStyleDictionary(this string style)
        {
            return style.Split(';')
                .Select(p => p.Trim())
                .Where(p => p != "")
                .Select(p => p.Split(':'))
                .ToDictionary(k => k[0], p => p[1]);
        }

        private static string ToStyleString(this IDictionary<string, string> dict)
        {
            return dict
                .Select(p => p.Key + ":" + p.Value)
                .Aggregate((a, b) => a + ";" + b);
        }

        private static IDictionary<string, string> UpdateMany(this IDictionary<string, string> dict, IDictionary<string, string> delta)
        {
            return dict.Select(p =>
                (delta.ContainsKey(p.Key)) ?
                    new KeyValuePair<string, string>(p.Key, delta[p.Key]) : p)
                .ToDictionary(k => k.Key, p => p.Value);
        }

    }

}
