using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using System.Threading.Tasks;

using System.Diagnostics;

namespace TestIntegration
{
    class Program
    {
        static void Main(string[] args)
        {
            string inputfile = "";
            string outputfilename = "";
            for(int i= 0; i<args.Length - 1; i++)
            {
                if (args[i] == "-i")
                {
                    try
                    {
                        inputfile = args[i + 1];
                    }
                    catch (IndexOutOfRangeException e)
                    {
                        System.Console.WriteLine("Please enter an ontology path");
                    }
                    
                }

                if (args[i] == "-o")
                {
                    try
                    {
                        outputfilename = args[i + 1];
                    }
                    catch (IndexOutOfRangeException e)
                    {
                        System.Console.WriteLine("Please enter a output filename");
                    }
                    
                }
            }

            if (outputfilename == "") outputfilename = "classification-result";
            if (inputfile == "" || inputfile == "-o") throw new System.ArgumentException("Please enter an ontology path");
            Stopwatch sp = new Stopwatch();
            System.Console.WriteLine("Start loading ontology......");
            sp.Start();
            //using our F# OWLReader
            FOWL.OWLReader.Reader reader = new FOWL.OWLReader.Reader(FOWL.OWLReader.token);
            List<string> ontology = reader.loadOntologyfromFile(inputfile);

            List<String> axioms = reader.getAxiom(ontology);
            List<String> classes = reader.getClass(ontology);
            List<String> properties = reader.getProperty(ontology);

            sp.Stop();
            System.Console.WriteLine("End loading ontology......");
            System.Console.WriteLine("Loading Time:" + sp.Elapsed.TotalSeconds + " s.");
            System.Console.WriteLine("Start Reasoner......");
            FOWL.OWLReasoner.Reason(classes, properties, axioms, outputfilename);
        }
    }
}
