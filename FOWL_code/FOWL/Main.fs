﻿namespace FOWL

open System
open System.Text.RegularExpressions
open System.Collections.Generic
open System.Collections
open System.Collections.Specialized
open System.Collections.Concurrent
open System.Text

open System.IO
open System.Threading.Tasks
open System.Diagnostics
open System.Linq

module Main =
    [<EntryPoint>]
    let Run args = 
        let start_time_reader = new Stopwatch();
        let mutable inputfile = ""
        let mutable outputfilename = ""
        for i = 0 to args.Length - 1 do
            if args.[i] = "-i" then
                try
                    inputfile <- args.[i + 1]
                with
                    | :? System.IndexOutOfRangeException -> printfn "Please enter an ontology path"
            if args.[i] = "-o" then
                try
                    outputfilename <- args.[i + 1]
                with
                    | :? System.IndexOutOfRangeException -> printfn "Please enter an output filename"

        if outputfilename = "" then outputfilename <- "classification-result"
        if inputfile = "" || inputfile = "-o" then raise (System.ArgumentException("Please enter an ontology path"))
        printfn "Loading Ontology..............."
        start_time_reader.Start()
        let reader = new FOWL.OWLReader.Reader(FOWL.OWLReader.token)
        let ontology = reader.loadOntologyfromFile(inputfile)
        let classes = reader.getClass(ontology)
        let properties = reader.getProperty(ontology)
        let axioms = reader.getAxiom(ontology)
        start_time_reader.Stop()
        printfn "Loading Time: %A s." start_time_reader.Elapsed.TotalSeconds
        printfn "Start Reasoner..............."
        FOWL.OWLReasoner.Reason classes properties axioms outputfilename
        0