
README:


INSTRUCTIONS:

 1. You must run Visual Studio/iri.exe as administrator to elevated privileges, otherwise the HTTP listener throws "Access is denied".

 2. "Sockets: an attempt was made to access a socket in a way forbbiden by it's access permissions.".
    javaw.exe uses this port (999). You can disable it by removing it by going to task manager --> Details --> End Task



EXPLANATIONS:

1.  I used SortedSet instead of TreeSet. It's not as fast but still sorted.
    Both C# SortedSet and Java TreeSet ignores duplicates.

2.	A. ConcurrentOrderedList: perhaps I should implement removeAll method   
	B. ConcurrentSkipListSet is faster and also uses a non-synchornized way to do stuff (ConcurrentOrderedList implementation is slower - but does the job) 
	C. objectsToRemove can be created in a different place and I can only use Clear...

3. Java HashSet default implemetation adds duplicates.
   C# HashSet default implementation ignores them.
   Here We used HashSet only with Transaction and Hash classes which overrides Equals & hashCode 
   So we are actually using them as C# default implementation handles them.
   
   Important to remember! If HashSet is used on a class that equals & hashCode are not overriden, we will have to 
   use a different data structure in C# to handle the addition of duplicates that Java HashSet has by default.

   To Handle it with the overrides to make sure the execution is the same in C# and Java!!!!!!!

4. Java HashMap "put" is different from C# dictionary add.
   HashMap replaces value if key exists while dictionary throws argument exception.



