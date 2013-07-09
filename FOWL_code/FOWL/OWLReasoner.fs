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

module OWLReasoner =
    type PropertyExpression (op:int, ?props:List<PropertyExpression>, ?prop_name: int) =
        member x.op = op
        member x.props = props
        member x.prop_name = prop_name

    type ClassExpression (op: int, complex: bool, ?cls_name: int, ?cls: List<ClassExpression>,?prop: PropertyExpression) = 
        member x.complex = complex
        member x.className = cls_name       
        member x.classes = cls
        member x.op = op
        member x.prop = prop

    type Axiom_General (op:int) =
        member x.op = op

    type ClassAxiom_SubclassOf (op:int,subcls: ClassExpression, supcls: ClassExpression) = 
        inherit Axiom_General(op)
        member x.op = op
        member x.sub = subcls
        member x.sup = supcls

    type ClassAxiom_SubclassOf_AND (op:int,subcls: Dictionary<int,int>, supcls: int) =
        inherit Axiom_General(op)
        member x.op = op
        member x.sub = subcls
        member x.sup = supcls

    type ClassAxiom_Equivalent (op:int,cls: List<ClassExpression>) =
        inherit Axiom_General(op)
        member x.op = op
        member x.classes = cls

    type ClassAxiom_Disjoint (op:int,cls: List<ClassExpression>) = 
        inherit Axiom_General(op)
        member x.op = op
        member x.classes = cls

    type PropertyAxiom_ObjectProperty(op:int,prop: PropertyExpression, cls: ClassExpression) =
        inherit Axiom_General(op)
        member x.op = op
        member x.prop = prop
        member c.cls = cls

    type PropertyAxiom_SubProperty (op:int,subprop: PropertyExpression, supprop: PropertyExpression) =
        inherit Axiom_General(op)
        member x.op = op
        member x.sub = subprop
        member x.sup = supprop  

    type Vertex (id:int) = 
        member x.id = id

    type Edge (r:int,fromVertex:int,toVertex:int) = 
        member x.r = r
        member x.fromVertex = fromVertex
        member x.toVertex = toVertex

    type DAG (vertices:Dictionary<int,int>,adjacencyList:Dictionary<int, Dictionary<int, int>>,inverseAdjacencyList:Dictionary<int, Dictionary<int, int>>) =
        member x.vertices = vertices
        member x.adjacencyList = adjacencyList
        member x.inverseAdjacencyList = inverseAdjacencyList

        member x.AddVertex (vertex:Vertex) =
            vertices.Add(vertex.id, 0);
            adjacencyList.Item(vertex.id) <- new Dictionary<int, int>();
            inverseAdjacencyList.Item(vertex.id) <- new Dictionary<int, int>();
        member x.AddEdge (from_vertex:Vertex,to_vertex:Vertex) =
            adjacencyList.Item(from_vertex.id).Add(to_vertex.id,0)
            inverseAdjacencyList.Item(to_vertex.id).Add(from_vertex.id,0)
        member x.HasVertex (vertex:int) =
            if vertices.ContainsKey(vertex) then
                true
            else
                false
        member x.HasEdge (from_vertex:int,to_vertex:int) =
            if adjacencyList.Item(from_vertex).ContainsKey(to_vertex) then
                true
            else
                false
        member x.getChildren (vertex:int) =
            let result, value =  adjacencyList.TryGetValue(vertex)
            if result then
                value.Keys 
            else
                value.Keys
        member x.getParent (vertex:int) = 
            let result, value =  inverseAdjacencyList.TryGetValue(vertex)
            if result then
                value.Keys 
            else
                value.Keys
        member x.GetEdgeCount =
            let mutable result = 0
            for pair in adjacencyList do
                result <- result + pair.Value.Count
            result
        member x.DepthFirstSearch (start_vertex:int) =
            let result = new Dictionary<int,int>()
            let unvisted_nodes = new HashSet<int>(vertices.Keys)
            unvisted_nodes.Remove(start_vertex) |> ignore
            let S = new Stack<int>()
            S.Push(start_vertex)
            while S.Count > 0 do
                let top = S.Pop()
                result.Add(top,top)
                for neighbor in x.getChildren(top) do
                    if unvisted_nodes.Remove(neighbor) then
                        S.Push(neighbor)
            result
        member x.BreathFirstSearch (start_vertex:int) =
            let result = new Dictionary<int,int>()
            let unvisted_nodes = new HashSet<int>(vertices.Keys)
            unvisted_nodes.Remove(start_vertex) |> ignore
            let Q = new Queue<int>()
            Q.Enqueue(start_vertex)
            while Q.Count > 0 do
                let head = Q.Dequeue()
                result.Add(head,head)
                for neighbor in x.getChildren(head) do
                    if unvisted_nodes.Remove(neighbor) then
                        Q.Enqueue(neighbor)
            result
        member x.BreathFirstSearchInverse (start_vertex:int) =
            let result = new Dictionary<int,int>()
            let unvisted_nodes = new HashSet<int>(vertices.Keys)
            unvisted_nodes.Remove(start_vertex) |> ignore
            let Q = new Queue<int>()
            Q.Enqueue(start_vertex)
            while Q.Count > 0 do
                let head = Q.Dequeue()
                result.Add(head,head)
                for neighbor in x.getParent(head) do
                    if unvisted_nodes.Remove(neighbor) then
                        Q.Enqueue(neighbor)
            result
        member x.getSuperConcepts (vertex:int) =
            let result, value =  adjacencyList.TryGetValue(vertex)
            if result then
                value.Keys 
            else
                value.Keys

    let Reason (lst_cls : ResizeArray<string>) (lst_prop : ResizeArray<string>) (logicalaxiom : ResizeArray<string>) (output:string) =

        printfn "Classes: %d" lst_cls.Count
        printfn "Properties: %d" lst_prop.Count
        printfn "Axioms: %d" logicalaxiom.Count
        let sw = new Stopwatch();
        let TOP = "owl:Thing"
        let BOT = "owl:Nothing"
        let TOP_ref = 1
        let BOT_ref = 0
        

        let SUBCLASS = "AX1"
        let EQUIVALENT = "AX2"
        let DISJOINT = "AX3"
        let SUBPROP = "AX4"
        let TRAN = "AX5"
        let PROPCHAIN = "AX6"
        let AND = "AX7"
        let SOME = "AX8"
        let PROPERTY_DOMAIN = "AX9"
        let PROPERTY_RANGE = "AX10"

        

        let SUBCLASS_value = "SubClassOf"
        let EQUIVALENT_value = "EquivalentClasses"
        let DISJOINT_value = "DisjointClasses"
        let SUBPROP_value = "SubObjectPropertyOf"
        let TRAN_value = "TransitiveObjectProperty"
        let PROPCHAIN_value = "ObjectPropertyChain"
        let AND_value = "ObjectIntersectionOf"
        let SOME_value = "ObjectSomeValuesFrom"
        let PROPERTY_DOMAIN_value = "ObjectPropertyDomain"
        let PROPERTY_RANGE_value = "ObjectPropertyRange"

        let axiom_keyword = Map.empty.Add(1,SUBCLASS_value).Add(2,EQUIVALENT_value).Add(3,DISJOINT_value).Add(4,SUBPROP_value).Add(5,TRAN_value).Add(6,PROPCHAIN_value).Add(7,AND_value).Add(8,SOME_value).Add(9,PROPERTY_DOMAIN_value).Add(10,PROPERTY_RANGE_value)

        let CLASSNAME_key = 0
        let PROPNAME_key = 0
        let SUBCLASS_key = 1
        let EQUIVALENT_key = 2
        let DISJOINT_key = 3
        let SUBPROP_key = 4
        let TRAN_key = 5
        let PROPCHAIN_key = 6
        let AND_key = 7
        let SOME_key = 8
        let PROPERTY_DOMAIN_key = 9
        let PROPERTY_RANGE_key = 10

        let freshClasses = new HashSet<int>()
        let freshRoles = new HashSet<int>()
        let rnd = new Random()
        let start_ran = 1000000
        let end_ran = 8000000
        let lst_thing = [(BOT_ref,BOT);(TOP_ref,TOP)]

        let start_time = System.DateTime.Now
        sw.Start()

        let la = List.ofSeq logicalaxiom
        let clist = [for i = 0 to lst_cls.Count - 1 do yield (i+2,lst_cls.Item(i))]
        let cMap = 
            let output = new Dictionary<int,string>(lst_cls.Count)
            for i = 0 to lst_cls.Count - 1 do
                output.Add(i+2,lst_cls.Item(i))
            output
        let reversed_cMap = 
            let output = new Dictionary<string,int>(lst_cls.Count)
            for i = 0 to lst_cls.Count - 1 do
                output.Add(lst_cls.Item(i),i+2)
            output
        
        let cMap_1 = Map.ofList (lst_thing @ clist)
        let plist = [for i = 0 to lst_prop.Count - 1 do yield (i+1,lst_prop.Item(i))]
        let pMap = 
            let output = new Dictionary<int,string>(lst_prop.Count)
            for i = 0 to lst_prop.Count - 1 do
                output.Add(i+1,lst_prop.Item(i))
            output
        let reversed_pMap = 
            let output = new Dictionary<string,int>(lst_prop.Count)
            for i = 0 to lst_prop.Count - 1 do
                output.Add(lst_prop.Item(i),i+1)
            output
        let pMap_1 = Map.ofList plist
        let getElement (input: string) = 
            let expression_op = input.Substring(0,input.IndexOf("("))
            let x1 = input.Substring(input.IndexOf("(")+1,input.LastIndexOf(")")-input.IndexOf("(")-1)
            let str_arr = x1.ToCharArray()
            let sb = new StringBuilder()
            let eles = new List<string>(5)
            eles.Add(expression_op)
            let mutable c = 0
            for s in str_arr do
                if s <> ' ' && s <> '(' && s <> ')' then
                    sb.Append(s) |> ignore
                elif s = '(' then
                    c <- c + 1
                    sb.Append(s) |> ignore
                elif s = ' ' && c > 0 then
                    sb.Append(s) |> ignore
                elif s = ')' then
                    c <- c - 1
                    sb.Append(s) |> ignore
                else
                    eles.Add(sb.ToString())
                    sb.Clear()  |> ignore
            eles.Add(sb.ToString())
            eles

        let translate (x: string) = //Convert classes and properties to integer
            let pattern1 = "<\S+>|owl:Thing|owl:Nothing|[a-zA-Z]*:[A-Za-z0-9-_]+"
            let leng = new Dictionary<string,int>()
            let mutable new_x = x
            let mat = Regex.Matches(x,pattern1)
            if mat.Count > 0 then
                for m in mat do
                    let mutable cls = m.Value                  
                    let result, value =  reversed_cMap.TryGetValue(cls)
                    if result then
                        let r = new Regex(cls, RegexOptions.IgnoreCase)
                        new_x <- r.Replace(new_x,value.ToString(),1)
                    else
                        let result, value =  reversed_pMap.TryGetValue(cls)
                        if result then
                            new_x <- new_x.Replace(cls,value.ToString())  
                new_x
            else
                new_x

        let printSuperclass (class_SET:List<ConcurrentDictionary<int,int>>) (role_SET:List<ConcurrentDictionary<int,ConcurrentDictionary<int, int>>>)  =
            let pattern = "\S*:" 
            let delimiter = ","
            if File.Exists("./" + output + ".csv") then
                File.Delete("./" + output + ".csv")
            let info = new FileStream("./" + output + ".csv",FileMode.OpenOrCreate)
            let sw = new StreamWriter(info);
            let mutable count = 0
            let mutable j = 0
            sw.AutoFlush <- true
            for i = 0 to class_SET.Count - 1 do
                let subcls = i + 2
                let result,value = cMap.TryGetValue(subcls)
                if result then
                    if class_SET.Item(i).Keys.Count = 2 then
                        if value.Contains(":") && not(value.Contains("http")) then
                            let mat = Regex.Matches(value,pattern)
                            if mat.Count > 0 then
                                sw.WriteLine(value.Replace(mat.Item(0).Value,"<Class IRI=\"#") + "\"/>" + delimiter + "<Class IRI=\"http://www.w3.org/2002/07/owl#Thing\"/>")
                            else
                                sw.WriteLine(value.Replace(":","<Class IRI=\"#") + "\"/>" + delimiter + "<Class IRI=\"http://www.w3.org/2002/07/owl#Thing\"/>")
                        else
                            sw.WriteLine(value.Replace("<","<Class IRI=\"").Replace(">","\"/>") + delimiter + "<Class IRI=\"http://www.w3.org/2002/07/owl#Thing\"/>")
                    else
                        for k in class_SET.Item(i).Keys do
                            if k = 1 then
                                if value.Contains(":") && not(value.Contains("http")) then
                                    let mat = Regex.Matches(value,pattern)
                                    if mat.Count > 0 then
                                        sw.WriteLine(value.Replace(mat.Item(0).Value,"<Class IRI=\"#") + "\"/>" + delimiter + "<Class IRI=\"http://www.w3.org/2002/07/owl#Thing\"/>")
                                    else
                                        sw.WriteLine(value.Replace(":","<Class IRI=\"#") + "\"/>" + delimiter + "<Class IRI=\"http://www.w3.org/2002/07/owl#Thing\"/>")
                                else
                                    sw.WriteLine(value.Replace("<","<Class IRI=\"").Replace(">","\"/>") + delimiter + "<Class IRI=\"http://www.w3.org/2002/07/owl#Thing\"/>")
                            elif k <> subcls && cMap.ContainsKey(k) then 
                                if not (class_SET.Item(k - 2).ContainsKey(subcls)) then
                                    if value.Contains(":") && not(value.Contains("http")) then
                                        let mat = Regex.Matches(value,pattern)
                                        if mat.Count > 0 then
                                            sw.WriteLine(value.Replace(mat.Item(0).Value,"<Class IRI=\"#") + "\"/>" + delimiter + cMap.Item(k).Replace(mat.Item(0).Value,"<Class IRI=\"#") + "\"/>")
                                        else
                                            sw.WriteLine(value.Replace(":","<Class IRI=\"#") + "\"/>" + delimiter + cMap.Item(k).Replace(":","<Class IRI=\"#") + "\"/>")
                                    else
                                        sw.WriteLine(value.Replace("<","<Class IRI=\"").Replace(">","\"/>") + delimiter + cMap.Item(k).Replace("<","<Class IRI=\"").Replace(">","\"/>"))

        let mergeElement (input: list<List<string>>)=
                let output_list = new List<string>(3)
                let sb =new StringBuilder()
                for i = 0 to input.Length - 1 do
                    let e = input.Item(i)
                    sb.Append(e.Item(0) + "(") |> ignore
                    for j = 1 to e.Count - 1 do
                        let s = e.Item(j)
                        if j <> e.Count - 1 then
                            sb.Append(s + " ") |> ignore
                        else
                            sb.Append(s + ")") |> ignore
                    output_list.Add(sb.ToString())   
                    sb.Clear() |> ignore      
                let output = Seq.toList output_list
                output

        let mergeAxiom_SUBCLASS (input: list<List<ClassAxiom_SubclassOf>>)=
            let output_list = new List<ClassAxiom_SubclassOf>(5)
            for i = 0 to input.Length - 1 do
                let e = input.Item(i)
                for j = 0 to e.Count - 1 do
                    let s = e.Item(j)
                    output_list.Add(s)      
            let output = Seq.toList output_list
            output

        let mergeAxiom_SUBRPOP (input: list<List<PropertyAxiom_SubProperty>>)=
            let output_list = new List<PropertyAxiom_SubProperty>(5)
            for i = 0 to input.Length - 1 do
                let e = input.Item(i)
                for j = 0 to e.Count - 1 do
                    let s = e.Item(j)
                    output_list.Add(s)      
            let output = Seq.toList output_list
            output

        let mergeAxiom_GENERAL (input: list<List<Axiom_General>>)=
            let output_list = new List<Axiom_General>(5)
            for i = 0 to input.Length - 1 do
                let e = input.Item(i)
                for j = 0 to e.Count - 1 do
                    let s = e.Item(j)
                    output_list.Add(s)      
            let output = Seq.toList output_list
            output

        // Start Filter
        let filter_EQUIVALENT (input: list<string>) =
                input |> List.filter (fun x -> x.Contains(EQUIVALENT_value))

        let filter_DISJOINT (input: list<string>) =
            input |> List.filter (fun x -> x.Contains(DISJOINT_value))

        let filter_PROPERTY_DOMAIN (input: list<string>) =
            input |> List.filter (fun x -> x.Contains(PROPERTY_DOMAIN_value))

        let filter_PROPERTY_RANGE (input: list<string>) =
            input |> List.filter (fun x -> x.Contains(PROPERTY_RANGE_value))

        let filter_GENERAL_GCI_STRING (input: list<string>) = 
            input |> List.filter (fun x -> x.Contains(SUBCLASS_value) && not (Regex.IsMatch(x,"(\S+\s\S+\s0)")))

        let filter_GCI_WITH_NOTHING (input: list<string>) = 
            input |> List.filter (fun x -> x.Contains(SUBCLASS_value) && Regex.IsMatch(x,"(\S+\s\S+\s0)"))

        let filter_COMPLEX_GCI_LEFT (input: list<ClassAxiom_SubclassOf>) = 
            input |> List.filter (fun x -> x.sub.complex = true)
        let filter_COMPLEX_GCI_RIGHT (input: list<ClassAxiom_SubclassOf>) = 
            input |> List.filter (fun x -> x.sub.op = CLASSNAME_key && (x.sup.complex = true || x.sup.op = AND_key))
        let filter_COMPLEX_GCI_BOTH (input: list<ClassAxiom_SubclassOf>) = 
            input |> List.filter (fun x -> x.sub.complex = true && x.sup.complex = true)
        let filter_COMPLEX_GCI_BOTH_2 (input: list<ClassAxiom_SubclassOf>) = 
            input |> List.filter (fun x -> x.sub.op <> CLASSNAME_key && x.sup.op <> CLASSNAME_key)
        let filter_GENERAL_GCI (input: list<ClassAxiom_SubclassOf>) = 
            input |> List.filter (fun x -> (x.sub.op = CLASSNAME_key && x.sup.op = CLASSNAME_key) || (x.sub.op = AND_key && x.sub.complex = false && x.sup.op = CLASSNAME_key) || (x.sub.op = CLASSNAME_key && x.sup.op = SOME_key && x.sup.complex = false) || (x.sub.op = SOME_key && x.sub.complex = false && x.sup.op = CLASSNAME_key))

        let filter_GENERAL_RI_STRING (input: list<string>) = 
            input |> List.filter (fun x -> x.Contains(SUBPROP_value))
        let filter_TRANSITIVE_ROLE (input: list<string>) = // NR1-4
            input |> List.filter (fun x -> x.Contains(TRAN_value))

        let filter_GENERAL_RI (input: list<PropertyAxiom_SubProperty>) = 
            input |> List.filter (fun x -> x.op = SUBPROP_key && x.sub.op = PROPNAME_key)
        let filter_CHAIN_RI (input: list<PropertyAxiom_SubProperty>) = 
            input |> List.filter (fun x -> x.sub.op = PROPCHAIN_key)

        // END Filter
 
        let rec parse_complex (input:string) =
            let input_eles = getElement input
            let op = input_eles.Item(0)
            let op_key = Map.findKey(fun key value -> value = op) axiom_keyword
            let mutable complex_value = false
            let All_clsexp = new List<ClassExpression>(5)
            if op = AND_value then
                for i =1 to input_eles.Count - 1 do
                    if not (input_eles.Item(i).Contains(AND_value) || input_eles.Item(i).Contains(SOME_value)) then
                        let cls_exp = new ClassExpression(op = CLASSNAME_key,complex = false, cls_name = Int32.Parse(input_eles.Item(i)))
                        All_clsexp.Add(cls_exp)
                    else
                        All_clsexp.Add(parse_complex(input_eles.Item(i)))
                        complex_value <- true
                let new_clsexp = new ClassExpression(op=op_key,complex = complex_value, cls = All_clsexp)
                new_clsexp
            elif op = SOME_value then
                let ori_cls = input_eles.Item(2)
                let role = input_eles.Item(1)
                let propexp = new PropertyExpression(op = PROPNAME_key,prop_name = Int32.Parse(role))
                if not (ori_cls.Contains(AND_value) || ori_cls.Contains(SOME_value)) then
                    let cls_exp = new ClassExpression(op = CLASSNAME_key,complex = false, cls_name = Int32.Parse(ori_cls))
                    All_clsexp.Add(cls_exp)
                else
                    All_clsexp.Add(parse_complex(ori_cls))
                    complex_value <- true
                let new_clsexp = new ClassExpression(op=op_key,complex = complex_value, cls = All_clsexp,prop = propexp)
                new_clsexp
            else
                let new_clsexp = new ClassExpression(op=0,complex = complex_value, cls_name = 0)
                new_clsexp

        let parseSubclass (input:string) = 
            let original_eles = getElement input
            let axiom_op = original_eles.Item(0)
            let op_key = Map.findKey(fun key value -> value = axiom_op) axiom_keyword
            let left = original_eles.Item(1)
            let right = original_eles.Item(2)
            let mutable left_clsexp = new ClassExpression(op = 0,complex = false)
            let mutable right_clsexp = new ClassExpression(op = 0,complex = false)
            if not (left.Contains(AND_value) || left.Contains(SOME_value)) then
                left_clsexp <- new ClassExpression(op = CLASSNAME_key,complex = false, cls_name = Int32.Parse(left))
            else
                left_clsexp <- parse_complex(left)

            if not (right.Contains(AND_value) || right.Contains(SOME_value)) then
                right_clsexp <- new ClassExpression(op = CLASSNAME_key,complex = false, cls_name = Int32.Parse(right))
            else
                right_clsexp <- parse_complex(right)

            let new_axiom = new ClassAxiom_SubclassOf(op_key,left_clsexp,right_clsexp)
            new_axiom

        let parseEquivalent (input:string) = 
            let original_eles = getElement input
            let Bindex = original_eles.IndexOf("")
            if Bindex <> -1 then
                original_eles.RemoveAt(Bindex) |> ignore
            let axiom_op = original_eles.Item(0)
            let lst_clsexp = new List<ClassExpression>(5)
            let op_key = Map.findKey(fun key value -> value = axiom_op) axiom_keyword
            let mutable temp_clsexp = new ClassExpression(op = 0,complex = false)
            for i = 1 to original_eles.Count - 1 do
                if not (original_eles.Item(i).Contains(AND_value) || original_eles.Item(i).Contains(SOME_value)) then
                    temp_clsexp <- new ClassExpression(op=CLASSNAME_key,complex = false, cls_name = Int32.Parse(original_eles.Item(i)))
                    lst_clsexp.Add(temp_clsexp)
                else
                    temp_clsexp <- parse_complex(original_eles.Item(i))
                    lst_clsexp.Add(temp_clsexp)
            let new_axiom = new ClassAxiom_Equivalent(op_key,lst_clsexp)
            new_axiom

        let parseObjectProperty (input:string) = 
            let original_eles = getElement input
            let axiom_op = original_eles.Item(0)
            let op_key = Map.findKey(fun key value -> value = axiom_op) axiom_keyword
            let left = original_eles.Item(1)
            let right = original_eles.Item(2)
            let propexp = new PropertyExpression(op = PROPNAME_key,prop_name = Int32.Parse(left))
            let mutable right_clsexp = new ClassExpression(op = 0,complex = false)
            if not (right.Contains(AND_value) || right.Contains(SOME_value)) then
                right_clsexp <- new ClassExpression(op=CLASSNAME_key,complex = false, cls_name = Int32.Parse(right))
            else
                right_clsexp <- parse_complex(right)
            let new_axiom = new PropertyAxiom_ObjectProperty(op_key,propexp,right_clsexp)
            new_axiom

        let parseTransitiveProperty (input:string) =
            let original_eles = getElement input
            let axiom_op = original_eles.Item(0)
            if axiom_op = TRAN_value then
                let op_key = Map.findKey(fun key value -> value = axiom_op) axiom_keyword
                let role = original_eles.Item(1)
                let propexp = new PropertyExpression(op = TRAN_key,prop_name = Int32.Parse(role))
                propexp
            else
                let propexp = new PropertyExpression(op = 0)
                propexp

        let parseSubproperty (input:string) =
            let original_eles = getElement input
            let axiom_op = original_eles.Item(0)
            let op_key = Map.findKey(fun key value -> value = axiom_op) axiom_keyword
            let left = original_eles.Item(1)
            let right = original_eles.Item(2)
            let mutable new_left = new PropertyExpression(op = 0)
            if left.Contains(PROPCHAIN_value) then
                let left_eles = getElement left
                let lst_propexp = new List<PropertyExpression>()
                for i = 1 to left_eles.Count - 1 do
                    let propexp = new PropertyExpression(op = PROPNAME_key,prop_name = Int32.Parse(left_eles.Item(i)))
                    lst_propexp.Add(propexp)
                new_left <- new PropertyExpression(op =PROPCHAIN_key,props = lst_propexp)
            else
                new_left <- new PropertyExpression(op = PROPNAME_key,prop_name = Int32.Parse(left))

            let new_right = new PropertyExpression(op = PROPNAME_key,prop_name = Int32.Parse(right))

            let new_axiom = new PropertyAxiom_SubProperty(SUBPROP_key,new_left,new_right)
            new_axiom

        // Start Normalization rule

        let NR1_1 (input:PropertyAxiom_ObjectProperty) = 
            if input.op = PROPERTY_DOMAIN_key then
                let TOP_classexp = new ClassExpression(op = CLASSNAME_key,complex = false, cls_name = TOP_ref)
                let List_clsexp = new List<ClassExpression>()
                List_clsexp.Add(TOP_classexp)
                let new_left = new ClassExpression(op = SOME_key,complex = false, cls = List_clsexp,prop = input.prop)
                let output = new ClassAxiom_SubclassOf(SUBCLASS_key,new_left,input.cls)
                output
            else
                let output = new ClassAxiom_SubclassOf(SUBCLASS_key,new ClassExpression(op=0,complex =false),new ClassExpression(op=0,complex =false))
                output

        let NR1_2 (input:PropertyAxiom_ObjectProperty) =
            let output = new List<Axiom_General>(5)
            if input.op = PROPERTY_RANGE_key then
                let afreshClass = lst_cls.Count + freshClasses.Count + 2
                freshClasses.Add(afreshClass) |> ignore
                let afresh_classexp = new ClassExpression(op = CLASSNAME_key,complex = false, cls_name = afreshClass)
                let faxiom = new PropertyAxiom_ObjectProperty(PROPERTY_RANGE_key,input.prop,afresh_classexp)
                let saxiom = new ClassAxiom_SubclassOf(SUBCLASS_key,afresh_classexp,input.cls)
                output.Add(faxiom)
                output.Add(saxiom)
                output
            else
                output

        let NR1_4 (input:PropertyExpression) =
            let propexp = new PropertyExpression(op = PROPNAME_key,prop_name = input.prop_name.Value)
            let List_propexp = new List<PropertyExpression>()
            List_propexp.Add(propexp)
            List_propexp.Add(propexp)
            let propexpchain = new PropertyExpression(op = PROPCHAIN_key, props = List_propexp)
            let output = new PropertyAxiom_SubProperty(SUBPROP_key,propexpchain,propexp)
            output

        let NR1_5 (input: ClassAxiom_Equivalent) =
            let output = new List<ClassAxiom_SubclassOf>()
            if input.op = EQUIVALENT_key then
                for i = 0 to input.classes.Count - 1 do
                    for j = 0 to input.classes.Count - 1 do
                        if not (input.classes.Item(i).Equals(input.classes.Item(j))) then
                            let new_axiom = new ClassAxiom_SubclassOf(SUBCLASS_key,input.classes.Item(i),input.classes.Item(j))
                            output.Add(new_axiom)
                output
            else
                output
                
        let NR1_7 (input: ClassAxiom_SubclassOf) =
            let BOT_clssexp = new ClassExpression(op = CLASSNAME_key,complex = false, cls_name = 0)
            let new_axiom = new ClassAxiom_SubclassOf(SUBCLASS_key,input.sub,BOT_clssexp)
            new_axiom

        let NR2_1 (input:PropertyAxiom_SubProperty) =
            let output = new List<PropertyAxiom_SubProperty>(5)
            let mutable old_role = input.sup
            if input.sub.op = PROPCHAIN_key && input.sub.props.Value.Count <> 2 then
                for i = input.sub.props.Value.Count - 1 downto 0 do
                    let afreshrole = lst_prop.Count + freshRoles.Count + 1
                    freshRoles.Add(afreshrole) |> ignore
                    let afresh_propexp = new PropertyExpression(op = PROPNAME_key,prop_name = afreshrole)
                    let lastrole = input.sub.props.Value.Item(i)
                    let List_propexp = new List<PropertyExpression>(5)
                    List_propexp.Add(afresh_propexp)
                    List_propexp.Add(lastrole)
                    let propchain = new PropertyExpression(op = PROPCHAIN_key, props = List_propexp)
                    let new_role_axiom = new PropertyAxiom_SubProperty(SUBPROP_key,propchain,old_role)
                    output.Add(new_role_axiom)
                    old_role <- afresh_propexp
                output
            else
                output.Add(input)
                output


        let NR2_2 (input: ClassAxiom_SubclassOf) =
            let output = new List<ClassAxiom_SubclassOf>(5)
            let mutable compl = false
            let main_lst_classexp = new List<ClassExpression>(5)
            let left = input.sub
            if left.op = AND_key then
                for i = 0 to left.classes.Value.Count - 1 do
                    let inner_classexp = left.classes.Value.Item(i) 
                    if not (inner_classexp.op = AND_key || inner_classexp.op = SOME_key) then
                        main_lst_classexp.Add(inner_classexp) |> ignore
                    else
                        let afreshClass = lst_cls.Count + freshClasses.Count + 2
                        freshClasses.Add(afreshClass) |> ignore
                        let afresh_classexp = new ClassExpression(op=CLASSNAME_key,complex = false, cls_name = afreshClass)
                        main_lst_classexp.Add(afresh_classexp)
                        let new_axiom = new ClassAxiom_SubclassOf(SUBCLASS_key,inner_classexp,afresh_classexp)
                        output.Add(new_axiom)
            let new_main_classexp = new ClassExpression(op = AND_key, complex = compl, cls = main_lst_classexp)
            let new_main_axiom = new ClassAxiom_SubclassOf(SUBCLASS_key,new_main_classexp,input.sup)
            output.Add(new_main_axiom)
            output
    
        let NR2_3 (input: ClassAxiom_SubclassOf) =
            let output = new List<ClassAxiom_SubclassOf>(5)
            let left = input.sub
            if left.op = SOME_key then
                let afreshClass = lst_cls.Count + freshClasses.Count + 2
                freshClasses.Add(afreshClass) |> ignore
                let afresh_classexp = new ClassExpression(op=CLASSNAME_key,complex = false, cls_name = afreshClass)
                let inner_classexp = left.classes.Value.Item(0) 
                let new_axiom = new ClassAxiom_SubclassOf(SUBCLASS_key,inner_classexp,afresh_classexp)
                let List_clsexp = new List<ClassExpression>(5)
                List_clsexp.Add(afresh_classexp)
                let new_classexp = new ClassExpression(op = SOME_key,complex = false, cls = List_clsexp, prop = left.prop.Value)
                let old_axiom = new ClassAxiom_SubclassOf(SUBCLASS_key,new_classexp,input.sup)
                output.Add(new_axiom)
                output.Add(old_axiom)
            output

        let NR3_1 (input: ClassAxiom_SubclassOf) =
            let output = new List<ClassAxiom_SubclassOf>(5)
            let left = input.sub
            let right = input.sup
            let afreshClass = lst_cls.Count + freshClasses.Count + 2
            freshClasses.Add(afreshClass) |> ignore
            let afresh_classexp = new ClassExpression(op=CLASSNAME_key,complex = false, cls_name = afreshClass)
            let new_axiom_left = new ClassAxiom_SubclassOf(SUBCLASS_key,left,afresh_classexp)
            let new_axiom_right = new ClassAxiom_SubclassOf(SUBCLASS_key,afresh_classexp,right)
            output.Add(new_axiom_left)
            output.Add(new_axiom_right)
            output

        let NR3_2 (input: ClassAxiom_SubclassOf) = 
            let output = new List<ClassAxiom_SubclassOf>(5)
            let right = input.sup
            if right.op = SOME_key then
                let afreshClass = lst_cls.Count + freshClasses.Count + 2
                freshClasses.Add(afreshClass) |> ignore
                let afresh_classexp = new ClassExpression(op=CLASSNAME_key,complex = false, cls_name = afreshClass)
                let inner_classexp = right.classes.Value.Item(0) 
                let List_clsexp = new List<ClassExpression>(5)
                List_clsexp.Add(afresh_classexp)
                let new_classexp = new ClassExpression(op = SOME_key,complex = false, cls = List_clsexp, prop = right.prop.Value)
                let new_axiom = new ClassAxiom_SubclassOf(SUBCLASS_key,input.sub,new_classexp)
                let old_axiom = new ClassAxiom_SubclassOf(SUBCLASS_key,afresh_classexp,inner_classexp)
                output.Add(new_axiom)
                output.Add(old_axiom)
            output

        let NR3_3 (input: ClassAxiom_SubclassOf) = 
            let output = new List<ClassAxiom_SubclassOf>(5)
            let right = input.sup
            if right.op = AND_key then
                for i = 0 to right.classes.Value.Count - 1 do
                    let new_axiom = new ClassAxiom_SubclassOf(SUBCLASS_key,input.sub,right.classes.Value.Item(i))
                    output.Add(new_axiom)
            output

        // End Normalization rule

        //Start recusive normalisation
        let check_complexclass_left (input: List<ClassAxiom_SubclassOf>) =
            let mutable flag = false
            for i =0 to input.Count - 1 do
                if input.Item(i).sub.complex then
                    flag <- true
            flag

        let check_complexclass_right (input: List<ClassAxiom_SubclassOf>) =
            let mutable flag = false
            for i = 0 to input.Count - 1 do
                if input.Item(i).sup.complex || input.Item(i).sup.op = AND_key then
                    flag <- true
            flag
            
        

        let normalisation_left (input: ClassAxiom_SubclassOf) =
            let rec continuous_normalise (listnewaxiom: List<ClassAxiom_SubclassOf>) = 
                let inner_output = new List<ClassAxiom_SubclassOf>(5)
                let mutable newaxioms = new List<ClassAxiom_SubclassOf>(5)
                for i = 0 to listnewaxiom.Count - 1 do
                    let axiom = listnewaxiom.Item(i)
                    let left_clsexp = axiom.sub
                    if left_clsexp.op = AND_key && left_clsexp.complex then
                        newaxioms <- NR2_2 axiom
                    elif left_clsexp.op = SOME_key && left_clsexp.complex then
                        newaxioms <- NR2_3 axiom
                    else
                        newaxioms.Add(axiom)
                    for j = 0 to newaxioms.Count - 1 do
                        inner_output.Add(newaxioms.Item(j))
                    newaxioms.Clear()

                if check_complexclass_left(inner_output) then
                    continuous_normalise(inner_output)
                else
                    inner_output

            if input.sub.op = AND_key then
                let first_normalised = NR2_2 input
                let answer = continuous_normalise(first_normalised)
                answer
            elif input.sub.op = SOME_key then
                let first_normalised = NR2_3 input
                let answer = continuous_normalise(first_normalised)
                answer
            else
                let answer = new List<ClassAxiom_SubclassOf>(5)
                answer.Add(input)
                answer

        let normalisation_right (input: ClassAxiom_SubclassOf) =
            let rec continuous_normalise (listnewaxiom: List<ClassAxiom_SubclassOf>) = 
                let inner_output = new List<ClassAxiom_SubclassOf>(5)
                let mutable newaxioms = new List<ClassAxiom_SubclassOf>(5)
                for i = 0 to listnewaxiom.Count - 1 do
                    let axiom = listnewaxiom.Item(i)
                    let right_clsexp = axiom.sup
                    if right_clsexp.op = AND_key then
                        newaxioms <- NR3_3 axiom
                    elif right_clsexp.op = SOME_key && right_clsexp.complex then
                        newaxioms <- NR3_2 axiom
                    else
                        newaxioms.Add(axiom)
                    for j = 0 to newaxioms.Count - 1 do
                        inner_output.Add(newaxioms.Item(j))
                    newaxioms.Clear()

                if check_complexclass_right(inner_output) then
                    continuous_normalise(inner_output)
                else
                    inner_output

            if input.sup.op = AND_key then
                let first_normalised = NR3_3 input
                let answer = continuous_normalise(first_normalised)
                answer
            elif input.sup.op = SOME_key then
                let first_normalised = NR3_2 input
                let answer = continuous_normalise(first_normalised)
                answer
            else
                let answer = new List<ClassAxiom_SubclassOf>(5)
                answer.Add(input)
                answer
        //End recusive normalisation
        let start_time_translating = new Stopwatch();
        start_time_translating.Start()
        let la1 = Async.Parallel [ for x in la -> async { return translate x } ]|> Async.RunSynchronously
        let la2 = List.ofArray la1
        start_time_translating.Stop()
        printfn "Translating Time: %A s." start_time_translating.Elapsed.TotalSeconds

        //START Parsing
        let ori_EQaxiom = filter_EQUIVALENT la2
        let ori_Domainaxiom = filter_PROPERTY_DOMAIN la2
        let ori_Rangeaxiom = filter_PROPERTY_RANGE la2
        let ori_Subaxiom_with_nothing = filter_GCI_WITH_NOTHING la2
        let ori_Subaxiom = filter_GENERAL_GCI_STRING la2
        let ori_Trasitiveaxiom = filter_TRANSITIVE_ROLE la2
        let ori_SubPropaxiom = filter_GENERAL_RI_STRING la2
        
        let start_time_parsing = new Stopwatch();
        start_time_parsing.Start()
        let parsed_EQaxiom = List.map parseEquivalent ori_EQaxiom 
        let parsed_Domainaxiom = List.map parseObjectProperty ori_Domainaxiom
        let parsed_Rangeaxiom = List.map parseObjectProperty ori_Rangeaxiom
        let parsed_Subaxiom = List.map parseSubclass ori_Subaxiom
        let parsed_Subaxiom_with_nothing = List.map parseSubclass ori_Subaxiom_with_nothing

        let parsed_Trasitiveaxiom = List.map parseTransitiveProperty ori_Trasitiveaxiom
        let parsed_SubPropaxiom = List.map parseSubproperty ori_SubPropaxiom

        let parsed_Subaxiom_with_complex_both = filter_COMPLEX_GCI_BOTH parsed_Subaxiom
        start_time_parsing.Stop()
        printfn "Parsing Time: %A s." start_time_parsing.Elapsed.TotalSeconds


        
        //START Normalisation
        let start_time_normalisation = new Stopwatch();
        start_time_normalisation.Start()
        //Start Apply NR1-1 to NR1-7
        let normalised_parsed_Domainaxiom = List.map NR1_1 parsed_Domainaxiom
        let normalised_parsed_Rangeaxiom = mergeAxiom_GENERAL (List.map NR1_2 parsed_Rangeaxiom)
        let normalised_parsed_Trasitiveaxiom = List.map NR1_4 parsed_Trasitiveaxiom
        let normalised_parsed_EQaxiom = mergeAxiom_SUBCLASS (List.map NR1_5 parsed_EQaxiom)
        let normalised_parsed_Subaxiom_with_nothing = List.map NR1_7 parsed_Subaxiom_with_nothing
        let normalised_parsed_Subaxiom_with_complex_both = mergeAxiom_SUBCLASS (List.map NR3_1 parsed_Subaxiom_with_complex_both)
        //End Apply NR1-1 to NR1-7

        let subclass_normalised_parsed_Rangeaxiom =
            let output = new List<ClassAxiom_SubclassOf>(5)
            for i = 0 to normalised_parsed_Rangeaxiom.Length - 1 do
                if normalised_parsed_Rangeaxiom.Item(i).op = SUBCLASS_key then
                    output.Add(normalised_parsed_Rangeaxiom.Item(i) :?> ClassAxiom_SubclassOf)
            (Seq.toList output)
        let new_normal = (filter_GENERAL_GCI parsed_Subaxiom) @ (filter_COMPLEX_GCI_RIGHT parsed_Subaxiom) @ (filter_COMPLEX_GCI_LEFT parsed_Subaxiom) @ normalised_parsed_Subaxiom_with_nothing @  normalised_parsed_EQaxiom @ normalised_parsed_Domainaxiom @ subclass_normalised_parsed_Rangeaxiom @ normalised_parsed_Subaxiom_with_complex_both
        let new_normal_RI = parsed_SubPropaxiom @ normalised_parsed_Trasitiveaxiom

        //Start Apply NR2-1 to NR2-3
        let axiom_with_chain = filter_CHAIN_RI new_normal_RI
        let new_axiom_with_chain = mergeAxiom_SUBRPOP (List.map NR2_1 axiom_with_chain)
        let axiom_complex_left = filter_COMPLEX_GCI_LEFT new_normal
        let new_axiom_complex_left = mergeAxiom_SUBCLASS (List.map normalisation_left axiom_complex_left)
        //End Apply NR2-1 to NR2-3

        let axiom_complex_right_1 = filter_COMPLEX_GCI_RIGHT new_normal
        let new_normal_2 = (filter_GENERAL_GCI new_normal) @ new_axiom_complex_left @ axiom_complex_right_1

        //Start Apply NR3-1 to NR3-3
        let axiom_with_complex_both = filter_COMPLEX_GCI_BOTH_2 new_normal_2
        let new_axiom_with_complex_both = mergeAxiom_SUBCLASS (List.map NR3_1 axiom_with_complex_both)
        let new_normal_3 = (filter_GENERAL_GCI new_normal_2) @ new_axiom_with_complex_both @ axiom_complex_right_1
        let axiom_complex_right = filter_COMPLEX_GCI_RIGHT new_normal_3
        let new_axiom_complex_right = mergeAxiom_SUBCLASS (List.map normalisation_right axiom_complex_right)
        //End Apply NR3-1 to NR3-3

        let final_normalised_axiom_GCI = (filter_GENERAL_GCI new_normal_3) @ new_axiom_complex_right
        let final_normalised_RI = (filter_GENERAL_RI new_normal_RI) @ new_axiom_with_chain

        let complex_both = filter_COMPLEX_GCI_BOTH_2 final_normalised_axiom_GCI
        let complex_right = filter_COMPLEX_GCI_RIGHT final_normalised_axiom_GCI
        let complex_left = filter_COMPLEX_GCI_LEFT final_normalised_axiom_GCI

        start_time_normalisation.Stop()
        printfn "Normalisation Time: %A s." start_time_normalisation.Elapsed.TotalSeconds
        //END Normalisation

        //Merge the original class map with a fresh class list
        let Class_SET_ref = [for i = 0 to lst_cls.Count - 1 do yield i+2] @ (Seq.toList freshClasses) //Index reference of Class sets

        //Merge the original property map with a fresh property list
        let Role_SET_ref = [for i = 0 to lst_prop.Count - 1 do yield i+1] @ (Seq.toList freshRoles) //Index reference of Role sets

        //Generate a class set
        let Class_SET =
            let output = new List<ConcurrentDictionary<int,int>>(30000)
            for i = 0 to Class_SET_ref.Length - 1 do 
                let set = new ConcurrentDictionary<int,int>()
                set.TryAdd(Class_SET_ref.Item(i),Class_SET_ref.Item(i)) |> ignore
                set.TryAdd(TOP_ref,TOP_ref) |> ignore
                output.Add(set)
            output


        //Generate a subclass set 
        let Subclass_SET = 
            let output = new Dictionary<int,ConcurrentDictionary<int,int>>(30000)
            for i = 0 to Class_SET_ref.Length - 1 do 
                let set = new ConcurrentDictionary<int,int>()
                set.TryAdd(Class_SET_ref.Item(i),Class_SET_ref.Item(i)) |> ignore
                output.Add(Class_SET_ref.Item(i),set)
            output

        //Generate a role set

        let Role_SET =
            let output = new List<ConcurrentDictionary<int,ConcurrentDictionary<int, int>>>(1000)
            for i = 0 to Role_SET_ref.Length - 1 do
                let tup = new ConcurrentDictionary<int,ConcurrentDictionary<int, int>>()
                output.Add(tup)
            output

        let superRole_SET =
            let output = new List<List<int>>(1000)
            for i = 0 to Role_SET_ref.Length - 1 do 
                let set = new List<int>()
                set.Add(Role_SET_ref.Item(i))
                output.Add(set)
            output

        let Class_digraph = new DAG(new Dictionary<int,int>(),new Dictionary<int, Dictionary<int, int>>(),new Dictionary<int, Dictionary<int, int>>())
        //START Completion Rules
        let CompletionRule_apply (lst_CR1:list<ClassAxiom_SubclassOf>) (lst_CR2:list<ClassAxiom_SubclassOf>) (lst_CR3:list<ClassAxiom_SubclassOf>) (lst_CR5:list<PropertyAxiom_SubProperty>) (lst_CR6:list<PropertyAxiom_SubProperty>) =
            
            let Class_SET_temp_CR3 = new ConcurrentDictionary<int,ConcurrentDictionary<int,int>>()
            let Class_SET_temp_CR1 = new ConcurrentDictionary<int,int>()
            let Role_SET_temp = new ConcurrentDictionary<int,int>()

            let map_filter_CR2 = new Dictionary<int,list<ClassAxiom_SubclassOf>>()
            let map_filter_CR3_class = new Dictionary<int,list<ClassAxiom_SubclassOf>>()
            let map_filter_CR3_role = new Dictionary<int,list<ClassAxiom_SubclassOf>>()
            let map_filter_CR5 = new Dictionary<int,list<PropertyAxiom_SubProperty>>()
            let map_filter_CR6 = new Dictionary<int,list<PropertyAxiom_SubProperty>>()

            let CS_modified = ref false
            let RS_modified = ref false
            let CR1_changed = ref false
            
            let CR1_modified (input:list<ClassAxiom_SubclassOf>) =
                for i = 0 to input.Length - 1 do
                    let left_class = input.Item(i).sub.className.Value
                    let right_class =  input.Item(i).sup.className.Value
                    if not (Class_digraph.HasVertex(left_class)) then
                        Class_digraph.AddVertex(new Vertex(left_class)) |> ignore
                    if not (Class_digraph.HasVertex(right_class)) then
                        Class_digraph.AddVertex(new Vertex(right_class)) |> ignore
                    if not (Class_digraph.HasEdge(left_class,right_class)) then
                        Class_digraph.AddEdge(new Vertex(left_class),new Vertex(right_class)) |> ignore
                        

            let convert_AND_Axiom (input:ClassAxiom_SubclassOf) =
                let extracted_axiom = new ClassAxiom_SubclassOf_AND(1,new Dictionary<int,int>(),input.sup.className.Value)
                for i = 0 to input.sub.classes.Value.Count - 1 do
                    let cls = input.sub.classes.Value.Item(i).className.Value
                    extracted_axiom.sub.Add(cls,cls)
                extracted_axiom
            
            let CR1_modified_and (input:ClassAxiom_SubclassOf_AND) = 
                let mutable flag = true
                let mutable apply = true
                let mutable sub = -1
                let mutable i = 0
                let mutable j = 0
                let mutable old_numberofParentNode = 0
                let mutable AllParent = new ConcurrentDictionary<int,int>()
                let mutable enum = input.sub.GetEnumerator()
                while apply && enum.MoveNext() do
                    let cls = enum.Current.Key
                    if Class_digraph.HasVertex(cls) then
                        let inside_AllParent = Subclass_SET.Item(cls)
                        let numberofParentNode = inside_AllParent.Count
                        if j = 0 then 
                            AllParent <- inside_AllParent
                            old_numberofParentNode <- numberofParentNode
                        if old_numberofParentNode > numberofParentNode then
                            AllParent <- inside_AllParent
                            old_numberofParentNode <- numberofParentNode
                        j <- j + 1
                    else
                        apply <- false
                j <- 0
                if apply then        
                    for k in AllParent.Keys do
                        let AllLeaf = Class_SET.Item(k - 2)
                        if not(AllLeaf.ContainsKey(input.sup)) then
                            flag <- true
                            let mutable inside_enum = input.sub.GetEnumerator()
                            while flag && inside_enum.MoveNext() do
                                if AllLeaf.ContainsKey(inside_enum.Current.Key) then
                                    i <- i + 1
                                else
                                    flag <- false
                            i <- 0
                        else
                            flag <- false
                        if flag then
                            let right_class =  input.sup
                            if k <> right_class then
                                if not (Class_digraph.HasVertex(right_class)) then
                                    Class_digraph.AddVertex(new Vertex(right_class)) |> ignore
                                if not (Class_digraph.HasEdge(k,right_class)) then
                                    Class_digraph.AddEdge(new Vertex(k),new Vertex(right_class)) |> ignore
                                 
                                    if Class_SET_temp_CR3.ContainsKey(k) then
                                        if not (Class_SET_temp_CR3.Item(k).ContainsKey(right_class) && not (Class_SET.Item(k - 2).ContainsKey(right_class))) then
                                            Class_SET_temp_CR3.Item(k).TryAdd(right_class,right_class) |> ignore
                                        if not (Subclass_SET.Item(right_class).ContainsKey(k)) then
                                            Subclass_SET.Item(right_class).TryAdd(k,k) |> ignore
                                        if not(Class_SET_temp_CR1.ContainsKey(right_class)) then
                                            Class_SET_temp_CR1.TryAdd(right_class,0) |> ignore
                                    else
                                        let new_dic = new ConcurrentDictionary<int,int>()
                                        new_dic.TryAdd(right_class,right_class) |> ignore
                                        Class_SET_temp_CR3.TryAdd(k,new_dic) |> ignore
                                        if not (Subclass_SET.Item(right_class).ContainsKey(k)) then
                                            Subclass_SET.Item(right_class).TryAdd(k,k) |> ignore
                                        if not(Class_SET_temp_CR1.ContainsKey(right_class)) then
                                            Class_SET_temp_CR1.TryAdd(right_class,0) |> ignore
                if Class_SET_temp_CR1.Count > 0 then
                    CR1_changed := true
                else
                    CR1_changed := false

                if Class_SET_temp_CR3.Count > 0 then
                    CS_modified := true
                else
                    CS_modified := false

            let CR1_1 (input:list<PropertyAxiom_SubProperty>) =
                let Role_graph = new DAG(new Dictionary<int,int>(),new Dictionary<int, Dictionary<int, int>>(),new Dictionary<int, Dictionary<int, int>>())
                for i = 0 to input.Length - 1 do
                    let left_propexp = input.Item(i).sub
                    let right_propexp = input.Item(i).sup
                    if left_propexp.op = PROPNAME_key then
                        let left_role = left_propexp.prop_name.Value
                        let right_role = right_propexp.prop_name.Value
                        if not (Role_graph.HasVertex(left_role)) then
                            Role_graph.AddVertex(new Vertex(left_role)) |> ignore
                        if not (Role_graph.HasVertex(right_role)) then
                            Role_graph.AddVertex(new Vertex(right_role)) |> ignore
                        if not (Role_graph.HasEdge(left_role,right_role)) then
                            Role_graph.AddEdge(new Vertex(left_role),new Vertex(right_role)) |> ignore
                Parallel.ForEach(Role_graph.vertices, (fun (vertex:KeyValuePair<int,int>) ->    let AllleafNode = Role_graph.BreathFirstSearch(vertex.Key)
                                                                                                for i in AllleafNode.Keys do
                                                                                                    if not (superRole_SET.Item(vertex.Key - 1).Contains(i)) then
                                                                                                        superRole_SET.Item(vertex.Key - 1).Add(i) |> ignore))
       
            let CR2 (input:ClassAxiom_SubclassOf) = 
                let left_classexp = input.sub
                let right_classexp = input.sup
                if right_classexp.op = SOME_key then
                    let left_class_name_id = left_classexp.className.Value
                    let role = right_classexp.prop.Value.prop_name.Value
                    let right_class_name_id = right_classexp.classes.Value.Item(0).className.Value

                    let left_cls_index = left_class_name_id
                    for j in Subclass_SET.Item(left_cls_index).Keys do 
                        let cls = j
                        let cls_index = cls
                        let index = role - 1
                        let result,value = Role_SET.Item(index).TryGetValue(cls)
                        if result then
                            if not (value.ContainsKey(right_class_name_id)) then
                                Role_SET.Item(index).Item(cls).TryAdd(right_class_name_id,right_class_name_id) |> ignore 
                                if not(Role_SET_temp.ContainsKey(index + 1)) then
                                    Role_SET_temp.TryAdd(index + 1,index + 1) |> ignore
                        else
                            let new_dic = new ConcurrentDictionary<int,int>()
                            new_dic.TryAdd(right_class_name_id,right_class_name_id) |> ignore
                            Role_SET.Item(index).TryAdd(cls,new_dic)  |> ignore
                            if not(Role_SET_temp.ContainsKey(index + 1)) then
                                Role_SET_temp.TryAdd(index + 1,index + 1)  |> ignore
                if Role_SET_temp.Count > 0 then
                    RS_modified := true
                else
                    RS_modified := false
                    
            let CR3 (input:ClassAxiom_SubclassOf) =
                let left_classexp = input.sub
                let right_classexp = input.sup
                if left_classexp.op = SOME_key then
                    let mutable class_in_SET = -1
                    let left_class_name_id = left_classexp.classes.Value.Item(0).className.Value
                    let role = left_classexp.prop.Value.prop_name.Value
                    let right_class_name_id = right_classexp.className.Value
                    let left_cls_index = left_class_name_id
                    let role_index = role - 1
                    if left_cls_index > 0  then
                        for k in Role_SET.Item(role_index).Keys do
                            if not (Class_SET.Item(k - 2).ContainsKey(right_class_name_id)) then
                                for i in Subclass_SET.Item(left_cls_index).Keys do
                                    if not (Class_SET.Item(k - 2).ContainsKey(right_class_name_id)) then
                                        class_in_SET <- i
                                        if Role_SET.Item(role_index).Item(k).ContainsKey(class_in_SET) then
                                            if Class_SET_temp_CR3.ContainsKey(k) then
                                                if not (Class_SET_temp_CR3.Item(k).ContainsKey(right_class_name_id)) && not (Class_SET.Item(k - 2).ContainsKey(right_class_name_id))  then
                                                    Class_SET_temp_CR3.Item(k).TryAdd(right_class_name_id,right_class_name_id) |> ignore
                                                    if not (Subclass_SET.Item(right_class_name_id).ContainsKey(k)) then
                                                        Subclass_SET.Item(right_class_name_id).TryAdd(k,k) |> ignore
                                                    if not (Class_digraph.HasVertex(k)) then
                                                        Class_digraph.AddVertex(new Vertex(k)) |> ignore
                                                    if not (Class_digraph.HasVertex(right_class_name_id)) then
                                                        Class_digraph.AddVertex(new Vertex(right_class_name_id)) |> ignore
                                                    if not (Class_digraph.HasEdge(k,right_class_name_id)) && k <> right_class_name_id then
                                                        Class_digraph.AddEdge(new Vertex(k),new Vertex(right_class_name_id)) |> ignore
                                            else
                                                if not (Class_SET.Item(k - 2).ContainsKey(right_class_name_id)) then
                                                    let new_dic = new ConcurrentDictionary<int,int>()
                                                    new_dic.TryAdd(right_class_name_id,right_class_name_id) |> ignore
                                                    Class_SET_temp_CR3.TryAdd(k,new_dic) |> ignore
                                                if not (Subclass_SET.Item(right_class_name_id).ContainsKey(k)) then
                                                    Subclass_SET.Item(right_class_name_id).TryAdd(k,k) |> ignore
                                                if not (Class_digraph.HasVertex(k)) then
                                                    Class_digraph.AddVertex(new Vertex(k)) |> ignore
                                                if not (Class_digraph.HasVertex(right_class_name_id)) then
                                                    Class_digraph.AddVertex(new Vertex(right_class_name_id)) |> ignore
                                                if not (Class_digraph.HasEdge(k,right_class_name_id)) && k <> right_class_name_id then
                                                    Class_digraph.AddEdge(new Vertex(k),new Vertex(right_class_name_id)) |> ignore
                if Class_SET_temp_CR3.Count > 0 then
                    CS_modified := true
                else
                    CS_modified := false
                    

            let CR5 (input:PropertyAxiom_SubProperty) =
                let left_propexp = input.sub
                let right_propexp = input.sup
                if left_propexp.op = PROPNAME_key then
                    let index_left_role = left_propexp.prop_name.Value - 1
                    let index_right_role = right_propexp.prop_name.Value - 1

                    for i in Role_SET.Item(index_left_role).Keys do
                        let result, value = Role_SET.Item(index_left_role).TryGetValue(i)
                        if Role_SET.Item(index_right_role).ContainsKey(i) then
                            for j in value.Keys do
                                if not (Role_SET.Item(index_right_role).Item(i).ContainsKey(j)) then
                                    Role_SET.Item(index_right_role).Item(i).TryAdd(j,j) |> ignore
                                    if not(Role_SET_temp.ContainsKey(index_right_role + 1)) then
                                        Role_SET_temp.TryAdd(index_right_role + 1,index_right_role + 1) |>ignore

                                    let lst_super_role_of_right = superRole_SET.Item(index_right_role)
                                    for k = 1 to lst_super_role_of_right.Count - 1 do
                                        if Role_SET.Item(lst_super_role_of_right.Item(k) - 1).ContainsKey(i) then
                                            if not (Role_SET.Item(lst_super_role_of_right.Item(k) - 1).Item(i).ContainsKey(j)) then
                                                Role_SET.Item(lst_super_role_of_right.Item(k) - 1).Item(i).TryAdd(j,j) |> ignore
                                                if not(Role_SET_temp.ContainsKey(lst_super_role_of_right.Item(k))) then
                                                    Role_SET_temp.TryAdd(lst_super_role_of_right.Item(k),lst_super_role_of_right.Item(k)) |>ignore
                                        else
                                            let new_value = new ConcurrentDictionary<int,int>()
                                            new_value.TryAdd(j,j) |>ignore
                                            Role_SET.Item(lst_super_role_of_right.Item(k) - 1).TryAdd(i,new_value) |> ignore
                                            if not(Role_SET_temp.ContainsKey(lst_super_role_of_right.Item(k))) then
                                                Role_SET_temp.TryAdd(lst_super_role_of_right.Item(k),lst_super_role_of_right.Item(k)) |>ignore

                        else
                            let new_value = new ConcurrentDictionary<int,int>(value)
                            Role_SET.Item(index_right_role).TryAdd(i,new_value) |> ignore
                            if not(Role_SET_temp.ContainsKey(index_right_role + 1)) then
                                Role_SET_temp.TryAdd(index_right_role + 1,index_right_role + 1) |>ignore

                            let lst_super_role_of_right = superRole_SET.Item(index_right_role)
                            for k = 1 to lst_super_role_of_right.Count - 1 do
                                if Role_SET.Item(lst_super_role_of_right.Item(k) - 1).ContainsKey(i) then
                                    for j in value.Keys do
                                        if not (Role_SET.Item(lst_super_role_of_right.Item(k) - 1).Item(i).ContainsKey(j)) then
                                            Role_SET.Item(lst_super_role_of_right.Item(k) - 1).Item(i).TryAdd(j,j) |> ignore
                                            if not(Role_SET_temp.ContainsKey(lst_super_role_of_right.Item(k))) then
                                                Role_SET_temp.TryAdd(lst_super_role_of_right.Item(k),lst_super_role_of_right.Item(k)) |>ignore
                                else
                                    let new_value = new ConcurrentDictionary<int,int>(value)
                                    Role_SET.Item(lst_super_role_of_right.Item(k) - 1).TryAdd(i,new_value) |> ignore
                                    if not(Role_SET_temp.ContainsKey(lst_super_role_of_right.Item(k))) then
                                        Role_SET_temp.TryAdd(lst_super_role_of_right.Item(k),lst_super_role_of_right.Item(k)) |>ignore

                if Role_SET_temp.Count > 0 then
                    RS_modified := true
                else
                    RS_modified := false

            let rec CR6 (input:PropertyAxiom_SubProperty) =
                let left_propexp = input.sub
                let right_propexp = input.sup
                if left_propexp.op = PROPCHAIN_key then
                    let left_role = left_propexp.props.Value.Item(0).prop_name.Value
                    let right_role = left_propexp.props.Value.Item(1).prop_name.Value
                    let index_right = right_propexp.prop_name.Value - 1
                    let index_left_role = left_role - 1
                    let index_right_role = right_role - 1
                    if index_left_role = index_right_role then
                        let digraph = new DAG(new Dictionary<int,int>(),new Dictionary<int, Dictionary<int, int>>(),new Dictionary<int, Dictionary<int, int>>())
                        for i in Role_SET.Item(index_left_role).Keys do
                            let class_1 = i
                            let result,value = Role_SET.Item(index_left_role).TryGetValue(class_1)
                            for j in value.Keys do
                                let class_2 = j
                                if not (digraph.HasVertex(class_1)) then
                                    digraph.AddVertex(new Vertex(class_1)) |> ignore
                                if not (digraph.HasVertex(class_2)) then
                                    digraph.AddVertex(new Vertex(class_2)) |> ignore
                                digraph.AddEdge(new Vertex(class_1),new Vertex(class_2)) |> ignore
                        for vertex in digraph.vertices do
                            let AllleafNode = digraph.DepthFirstSearch(vertex.Key)
                            for i in AllleafNode.Keys do
                                if i <> vertex.Key then
                                    if Role_SET.Item(index_right).ContainsKey(vertex.Key) then
                                        if not (Role_SET.Item(index_right).Item(vertex.Key).ContainsKey(i)) then
                                            Role_SET.Item(index_right).Item(vertex.Key).TryAdd(i,i) |> ignore
                                            if not(Role_SET_temp.ContainsKey(index_right + 1)) then
                                                Role_SET_temp.TryAdd(index_right + 1,index_right + 1) |>ignore
                                    else
                                        let new_dic = new ConcurrentDictionary<int,int>()
                                        new_dic.TryAdd(i,i) |> ignore
                                        Role_SET.Item(index_right).TryAdd(vertex.Key,new_dic) |> ignore
                                        if not(Role_SET_temp.ContainsKey(index_right + 1)) then
                                            Role_SET_temp.TryAdd(index_right + 1,index_right + 1) |> ignore
                     else
                        for i in Role_SET.Item(index_left_role).Keys do
                            let class_1 = i
                            let result,value = Role_SET.Item(index_left_role).TryGetValue(class_1)
                            for j in value.Keys do
                                let class_2 = j
                                let result_right_role,value_right_role = Role_SET.Item(index_right_role).TryGetValue(class_2)
                                if result_right_role then
                                    for k in value_right_role.Keys do
                                        let class_3 = k
                                        let result_right_hand,value_right_hand = Role_SET.Item(index_right).TryGetValue(class_1)
                                        if result_right_hand then
                                            if not (value_right_hand.ContainsKey(class_3)) then
                                                Role_SET.Item(index_right).Item(class_1).TryAdd(class_3,class_3) |> ignore
                                                if not(Role_SET_temp.ContainsKey(index_right + 1)) then
                                                    Role_SET_temp.TryAdd(index_right + 1,index_right + 1) |>ignore
                                                let lst_reApply_CR6 = lst_CR6 |> List.filter (fun x -> x.sub.props.Value.Item(0).prop_name.Value = right_propexp.prop_name.Value || x.sub.props.Value.Item(1).prop_name.Value = right_propexp.prop_name.Value)
                                                if lst_reApply_CR6.Length <> 0 then
                                                    List.map CR6 lst_reApply_CR6 |> ignore                                             
                if Role_SET_temp.Count > 0 then
                    RS_modified := true
                else
                    RS_modified := false

            let apply_CR1_1 = CR1_1 lst_CR5
             
            //Start Rule Application
            let start_time_CR1 = System.DateTime.Now
            let lst_CR1_normal = lst_CR1 |> List.filter (fun x -> x.sub.op = CLASSNAME_key)
            let lst_CR1_and = lst_CR1 |> List.filter (fun x -> x.sub.op = AND_key)
            let AND_Axiom = List.map convert_AND_Axiom lst_CR1_and
            let apply_CR1 = CR1_modified lst_CR1_normal
            let apply_CR1_and = List.map CR1_modified_and AND_Axiom
            CS_modified := false
            Class_SET_temp_CR3.Clear()
            Parallel.ForEach(Class_digraph.vertices, (fun (vertex:KeyValuePair<int,int>) ->     let AllleafNode = Class_digraph.BreathFirstSearch(vertex.Key)
                                                                                                for i in AllleafNode.Keys do
                                                                                                    if not (Class_SET.Item(vertex.Key - 2).ContainsKey(i)) then
                                                                                                        Class_SET.Item(vertex.Key - 2).TryAdd(i,i) |> ignore
                                                                                                    if not (Subclass_SET.Item(i).ContainsKey(vertex.Key)) then
                                                                                                        Subclass_SET.Item(i).TryAdd(vertex.Key,vertex.Key) |> ignore)) |> ignore

            let CR1_duration = System.DateTime.Now - start_time_CR1
            printfn "CR1 Time: %A s." CR1_duration.TotalSeconds

            let start_time_CR2 = System.DateTime.Now
            let apply_CR2 = List.map CR2 lst_CR2
            RS_modified := false
            Role_SET_temp.Clear()
            let CR2_duration = System.DateTime.Now - start_time_CR2
            printfn "CR2 Time: %A s." CR2_duration.TotalSeconds

            let start_time_CR3 = System.DateTime.Now
            if lst_CR3.Length <> 0 then
                List.map CR3 lst_CR3 |> ignore     
            let CR3_duration = System.DateTime.Now - start_time_CR3
            printfn "CR3 Time: %A s." CR3_duration.TotalSeconds

            let start_time_CR5 = System.DateTime.Now
            if lst_CR5.Length <> 0 then
                List.map CR5 lst_CR5 |> ignore
            let CR5_duration = System.DateTime.Now - start_time_CR5
            printfn "CR5 Time: %A s." CR5_duration.TotalSeconds

            let start_time_CR6 = System.DateTime.Now
            if lst_CR6.Length <> 0 then
                List.map CR6 lst_CR6 |> ignore
                
            let CR6_duration = System.DateTime.Now - start_time_CR6
            printfn "CR6 Time: %A s." CR6_duration.TotalSeconds

            while (!CS_modified = true && lst_CR3.Length <> 0) || (lst_CR2.Length <> 0 && !RS_modified = true)  do
                    if !CS_modified = true then
                        CS_modified := false
                        List.map CR1_modified_and AND_Axiom |> ignore
                        Parallel.ForEach(Class_SET_temp_CR3.Keys, (fun (k:int) ->   let AllParentNode = Class_digraph.BreathFirstSearchInverse(k)
                                                                                    for i in AllParentNode.Keys do
                                                                                        for cls in Class_SET_temp_CR3.Item(k).Keys do
                                                                                            if not(Class_SET.Item(i - 2).ContainsKey(cls)) then
                                                                                                Class_SET.Item(i - 2).TryAdd(cls,cls) |>ignore
                                                                                            if not(Subclass_SET.Item(cls).ContainsKey(i)) then
                                                                                                Subclass_SET.Item(cls).TryAdd(i,i) |>ignore)) |> ignore
                        
                        let Class_SET_temp = new ConcurrentDictionary<int,ConcurrentDictionary<int,int>>(Class_SET_temp_CR3)
                        Class_SET_temp_CR3.Clear()

                        let inside = new Dictionary<int,int>()
                        for i in Class_SET_temp.Keys do
                            for k in Class_SET_temp.Item(i).Keys do
                                if not(inside.ContainsKey(k)) then
                                    inside.Add(k,0)
                        for k in inside.Keys do
                            if map_filter_CR2.ContainsKey(k) then
                                if  map_filter_CR2.Item(k).Length <> 0 then
                                    List.map CR2 (map_filter_CR2.Item(k)) |> ignore
                            else
                                let lst_reApply_CR2 = lst_CR2 |> List.filter (fun x -> x.sub.className.Value = k)
                                map_filter_CR2.Add(k,lst_reApply_CR2)
                                List.map CR2 lst_reApply_CR2 |> ignore

                            if map_filter_CR3_class.ContainsKey(k) then
                                if map_filter_CR3_class.Item(k).Length <> 0 then
                                    List.map CR3 (map_filter_CR3_class.Item(k)) |> ignore
                            else
                                let lst_reApply_CR3 = lst_CR3 |> List.filter (fun x -> x.sub.classes.Value.Item(0).className.Value = k)
                                map_filter_CR3_class.Add(k,lst_reApply_CR3)
                                List.map CR3 lst_reApply_CR3 |> ignore
                        inside.Clear()

                    if !RS_modified = true then
                        RS_modified := false
                        if Class_SET_temp_CR3.Count > 0 then
                            Parallel.ForEach(Class_digraph.vertices, (fun (vertex:KeyValuePair<int,int>) ->     let AllleafNode = Class_digraph.BreathFirstSearch(vertex.Key)
                                                                                                                for i in AllleafNode.Keys do
                                                                                                                    if not (Class_SET.Item(vertex.Key - 2).ContainsKey(i)) then
                                                                                                                        Class_SET.Item(vertex.Key - 2).TryAdd(i,i) |> ignore
                                                                                                                    if not (Subclass_SET.Item(i).ContainsKey(vertex.Key)) then
                                                                                                                        Subclass_SET.Item(i).TryAdd(vertex.Key,vertex.Key) |> ignore)) |> ignore

                        let inside_Role_SET = new Dictionary<int,int>(Role_SET_temp)
                        Role_SET_temp.Clear()
                        for i in inside_Role_SET.Keys do
                            if map_filter_CR3_role.ContainsKey(i) then
                                if map_filter_CR3_role.Item(i).Length <> 0 then
                                    List.map CR3 (map_filter_CR3_role.Item(i)) |> ignore
                            else
                                let lst_reApply_CR3 = lst_CR3 |> List.filter (fun x -> x.sub.prop.Value.prop_name.Value = i)
                                map_filter_CR3_role.Add(i,lst_reApply_CR3)
                                List.map CR3 lst_reApply_CR3 |> ignore

                            if map_filter_CR5.ContainsKey(i) then
                                if map_filter_CR5.Item(i).Length <> 0 then
                                    List.map CR5 (map_filter_CR5.Item(i)) |> ignore
                            else
                                let lst_reApply_CR5 = lst_CR5 |> List.filter (fun x -> x.sub.prop_name.Value = i)
                                map_filter_CR5.Add(i,lst_reApply_CR5)
                                List.map CR5 lst_reApply_CR5 |> ignore

                            if map_filter_CR6.ContainsKey(i) then
                                if map_filter_CR6.Item(i).Length <> 0 then
                                    List.map CR6 (map_filter_CR6.Item(i)) |> ignore
                            else
                                let lst_reApply_CR6 = lst_CR6 |> List.filter (fun x -> x.sub.props.Value.Item(0).prop_name.Value = i || x.sub.props.Value.Item(1).prop_name.Value = i)
                                map_filter_CR6.Add(i,lst_reApply_CR6)
                                List.map CR6 lst_reApply_CR6 |> ignore
                        inside_Role_SET.Clear()

            let start_time_CR4 = System.DateTime.Now
            let CR4 = 
                if normalised_parsed_Subaxiom_with_nothing.Length <> 0 then
                    let mutable class_in_SET = -1
                    for j = 0 to Class_SET_ref.Length - 1 do
                        if Class_SET.Item(j).ContainsKey(BOT_ref) then
                            class_in_SET <- Class_SET_ref.Item(j)
                            for i = 0 to Role_SET_ref.Length - 1 do
                                for k in Role_SET.Item(i).Keys do
                                    if Role_SET.Item(i).Item(k).ContainsKey(class_in_SET) then
                                        if not (Class_SET.Item(k).ContainsKey(BOT_ref)) then
                                            Class_SET.Item(k).TryAdd(BOT_ref,BOT_ref) |> ignore

            let CR4_duration = System.DateTime.Now - start_time_CR4
            printfn "CR4 Time: %A s." CR4_duration.TotalSeconds        
        //END Completion Rules
        
        //START applying Completion Rules
        let start_time_classification = System.DateTime.Now
        let List_CR1 = final_normalised_axiom_GCI |> List.filter (fun x -> not (x.sub.op = SOME_key || x.sup.op = SOME_key))
        let List_CR2 = final_normalised_axiom_GCI |> List.filter (fun x -> x.sup.op = SOME_key)
        let List_CR3 = final_normalised_axiom_GCI |> List.filter (fun x -> x.sub.op = SOME_key)
        let List_CR5 = final_normalised_RI |> List.filter (fun x -> not (x.sub.op = PROPCHAIN_key))
        let List_CR6 = final_normalised_RI |> List.filter (fun x -> x.sub.op = PROPCHAIN_key)

        CompletionRule_apply List_CR1 List_CR2 List_CR3 List_CR5 List_CR6 |> ignore

        let Classification_duration = System.DateTime.Now - start_time_classification
        printfn "Classification Time: %A s." Classification_duration.TotalSeconds
        sw.Stop()
        printfn "Finish Classification....."
        let ts = sw.Elapsed
        printfn "Total Time: %A s." ts.TotalSeconds
        printfn "Start Print output................................."
        printSuperclass Class_SET Role_SET
        printfn "Finish Print output................................."
        Console.ReadLine() |> ignore