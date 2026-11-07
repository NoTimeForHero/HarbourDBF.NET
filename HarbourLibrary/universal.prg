
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
      xValue := hb_SToD(xValue)
    ELSEIF cFieldType == "T"
      xValue := hb_SToT(xValue)
    ENDIF
    FieldPut(nOrder, xValue)
  NEXT
  hResult := { "RESULT" => nLen }
RETURN hResult

FUNCTION INTERNAL_FORMAT_KEY(cData)
  LOCAL hData, xValue, aStep, cOp, nWidth
  hb_jsonDecode(cData, @hData)
  xValue := hData["value"]
  FOR EACH aStep IN hData["steps"]
    cOp := aStep[1]
    IF cOp == "STR"
      IF ValType(xValue) != "N"
        RETURN { "ERROR" => "STR requires a number" }
      ENDIF
      nWidth := aStep[2]
      xValue := Str(xValue, nWidth, 0)
      IF "*" $ xValue
        RETURN { "ERROR" => "STR width exceeded" }
      ENDIF
    ELSEIF cOp == "ALLTRIM"
      IF ValType(xValue) != "C"
        RETURN { "ERROR" => "ALLTRIM requires text" }
      ENDIF
      xValue := AllTrim(xValue)
    ELSEIF cOp == "PADR"
      IF ValType(xValue) != "C"
        RETURN { "ERROR" => "PADR requires text" }
      ENDIF
      nWidth := aStep[2]
      IF Len(xValue) > nWidth
        RETURN { "ERROR" => "PADR width exceeded" }
      ENDIF
      xValue := PadR(xValue, nWidth)
    ELSEIF cOp == "UPPER"
      IF ValType(xValue) != "C"
        RETURN { "ERROR" => "UPPER requires text" }
      ENDIF
      xValue := Upper(xValue)
    ELSE
      RETURN { "ERROR" => "Unknown key formatting operation" }
    ENDIF
  NEXT
RETURN { "RESULT" => xValue }

FUNCTION INTERNAL_SEARCH_BLOCK(cData, lValidateOnly)
  LOCAL hData, cBlock, bBlock, xResult
  hb_jsonDecode(cData, @hData)
  cBlock := hData["block"]
  bBlock := &cBlock
  IF ValType(bBlock) != "B"
    RETURN { "ERROR" => "SearchKey must be a Harbour block" }
  ENDIF
  IF lValidateOnly
    RETURN { "RESULT" => .T. }
  ENDIF
  xResult := hb_ExecFromArray(bBlock, hData["arguments"])
  IF ValType(xResult) != "C"
    RETURN { "ERROR" => "SearchKey must return a character key" }
  ENDIF
RETURN { "RESULT" => xResult }

FUNCTION DBF_PROCEDURE_CALL(cCommand, cData)
    LOCAL hResult
    IF cCommand == "LIST_FIELDS"
      hResult := INTERNAL_LIST_FIELDS()
    ELSEIF cCommand == "SET_VALUES"  
      hResult := INTERNAL_SET_VALUES(cData)
    ELSEIF cCommand == "FORMAT_KEY"
      hResult := INTERNAL_FORMAT_KEY(cData)
    ELSEIF cCommand == "VALIDATE_SEARCH_BLOCK"
      hResult := INTERNAL_SEARCH_BLOCK(cData, .T.)
    ELSEIF cCommand == "SEARCH_BLOCK"
      hResult := INTERNAL_SEARCH_BLOCK(cData, .F.)
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
