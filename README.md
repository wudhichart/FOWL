FOWL
====

An ontology reasoner for the .NET Framework which implements the completion-rule algorithm for the fragment ELHR+ of OWL 2 EL in F#.

Requirement
-------------------------------------------------------------------------
- .NET Framework 4.5

Instruction for Execution
-------------------------------------------------------------------------
For the ontology reasoner:
- There is an executable file called "FOWL.exe".
- Use command prompt to execute the ontology reasoner by command in the following form.
  "FOWL.exe -i [an ontology path] -o [a output filename]". 
  For example, <code>FOWL.exe -i "C://ontology/nci.owl" -o "classification-result"</code>
- The output is a subsumption relation of the ontology in .csv file.

Limitations
-------------------------------------------------------------------------
- The reasoner supports only functional syntax.
