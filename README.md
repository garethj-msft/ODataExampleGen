# ODataExampleGen
Generate example JSON packets for requests or responses for OData APIs.

Usage:

`dotnet ODataExampleGen -c[--csdl] someModel.csdl -u[--uri] <some_path_rooted_at_singleton_or_entitySet> -p[--propertyType] <navPropName1>:<concreteTypeNameToUse1> <navPropName2>:<concreteTypeNameToUse2> -e[--enumValue] <propName1>:<enumValue1> <propName2>:<enumValue2> -r[--primitiveValue] <propName1>:<propValue1> <propName2>:<propValue2> -b[--baseUri] https://graph.microsoft.com/beta` -d childNavProp

Example output:

```
{
  "id": "id1",
  "displayName": "A sample displayName",
  "recurrence": "hourly",
  "startDateTime": "2020-10-30T08:25:00.4673388Z",
  "endDateTime": "2020-10-30T08:25:00.4675242Z",
  "type": "grades",
  "apiFilter": {
    "@odata.type": "#myNamespace.powerSchoolApiFilter",
    "schoolIds": [
      "A sample of schoolIds",
      "Another sample of schoolIds"
    ],
    "schoolYears": [
      "A sample of schoolYears",
      "Another sample of schoolYears"
    ]
  },
  "runs@odata.bind": [
    "https://graph.microsoft.com/beta/external/dataFlowHub/runs/id2",
    "https://graph.microsoft.com/beta/external/dataFlowHub/runs/id3"
  ],
  "source@odata.bind": "https://graph.microsoft.com/beta/external/dataFlowHub/sources/id4"
}
```
