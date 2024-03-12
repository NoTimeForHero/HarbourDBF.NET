
// Универсальный

FUNCTION INTERNAL_LIST_FIELDS()
  LOCAL nI, nLen 
  LOCAL aFields := {}
  LOCAL hLine
  nLen := FCount()
  FOR nI := 1 TO nLen
    hLine := hb_Hash()          
    hLine["Name"] := FieldName(nI)
    hLine["Type"] := FieldType(nI)
    hLine["Length"] := FieldLen(nI)
    hLine["Point"] := FieldDec(nI)
    AADD(aFields, hLine)
  NEXT
  hResult := { "RESULT" => aFields}
RETURN hResult 

FUNCTION INTERNAL_SET_VALUES(cData)
  LOCAL aRecords, nLen, nI, aRow
  LOCAL nOrder, xValue, cFieldType
  hb_jsonDecode(cData, @aRecords)
  nLen := LEN(aRecords)
  FOR nI := 1 TO nLen
    aRow := aRecords[nI]
    nOrder := aRow[1]
    xValue := aRow[2]
    cFieldType := aRow[3]
    IF cFieldType == "D"
      xValue := CtoD(xValue)
    ENDIF
    FieldPut(nOrder, xValue)
  NEXT
  hResult := { "RESULT" => nLen }
RETURN hResult

FUNCTION DBF_PROCEDURE_CALL(cCommand, cData)
    LOCAL hResult
    IF cCommand == "LIST_FIELDS"
      hResult := INTERNAL_LIST_FIELDS()
    ELSEIF cCommand == "SET_VALUES"  
      hResult := INTERNAL_SET_VALUES(cData)
    ELSE
      hResult := { "ERROR" => "Unknown command name: " + cCommand}
    ENDIF
RETURN hb_jsonEncode(hResult)

#pragma BEGINDUMP
#include <stdio.h>
#include <windows.h>
#include <hbvm.h>
#include <hbapi.h>
#include <hbapiitm.h>

HB_EXPORT void* _export DBF_PROCEDURE_CALL(const char* cCommand, const char* cData)
{
  PHB_ITEM pArg1 = hb_itemPutC( NULL, cCommand );
  PHB_ITEM pArg2 = hb_itemPutC( NULL, cData );
  PHB_ITEM pResult = hb_itemDoC( "DBF_PROCEDURE_CALL", 2, pArg1, pArg2);
  char * rawResult = (char*) hb_itemGetC(pResult);
  hb_itemRelease( pArg1 );
  hb_itemRelease( pArg2 );
  hb_itemRelease( pResult );
  return rawResult;
}

#pragma ENDDUMP