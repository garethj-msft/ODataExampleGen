ODataExampleGen tool.

Usage:

ODataExampleGen.exe -c[--csdl] someModel.csdl -u[--uri] <some_path_rooted_at_singleton_or_entitySet> -p[--propertyType] <navPropName1>:<concreteTypeNameToUse1> <navPropName2>:<concreteTypeNameToUse2> -e[--enumValue] <propName1>:<enumValue1> <propName2>:<enumValue2> -r[--primitiveValue] <propName1>:<propValue1> <propName2>:<propValue2> -b[--baseUri] https://graph.microsoft.com/beta