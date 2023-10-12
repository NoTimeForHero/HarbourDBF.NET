//#include "inkey.ch"
//#include "fileio.ch"
//#include "hbclass.ch"
//#include "common.ch"
#define CLRF CHR(13) + CHR(10)

REQUEST DBFCDX, DBFFPT

STATIC cLastError := NIL

// ВНИМАНИЕ!
// Многие функции вроде DbRLock могут не вызываться из C кода
// Поэтому лучше использовать дополнительную обёртку хоть и ценой производительности

FUNCTION DBF_GET_LAST_ERROR()
RETURN cLastError

FUNCTION DBF_USE(cFile, cAlias, lExclusive, cCodepage)
  LOCAL cRdd := RddSetDefault()
  cCodepage := IIF(LEN(cCodepage) == 0, "RU866", cCodepage)
  // ? HB_ValToExp({cRdd, cFile, cAlias, lExclusive, cCodepage})
  IF LEN(cFile) != 0
    DbUseArea(.T., cRdd, cFile, cAlias, !lExclusive, .F., cCodepage)
  ELSE
    USE
  ENDIF
RETURN NIL

FUNCTION DBF_CREATE(cName, cJson)
  LOCAL xStruct
  // ? "DBF_CREATE: ", cName, " ", cJson
  hb_jsonDecode(cJson, @xStruct)
  dbCreate(cName, xStruct)
  // ? "RAW: ", ValType(xStruct), " ", HB_ValToExp(xStruct)
RETURN NIL

FUNCTION DBF_GOTO(nIndex)
  DBGOTO(nIndex)
RETURN NIL

FUNCTION DBF_APPEND()
  APPEND BLANK
RETURN NIL

FUNCTION DBF_COMMIT()
  DbCommit()
RETURN NIL

FUNCTION DBF_CLOSE_AREA(cAlias)
  IF LEN(cAlias) > 0
    DbSelectArea(cAlias)
  ENDIF
  DbCloseArea()
RETURN NIL

FUNCTION DBF_GET_VALUES(cJson)
  LOCAL aFields, nI, nField
  LOCAL aResult := {}, cResult
  hb_jsonDecode(cJson, @aFields)
  FOR nI := 1 TO LEN(aFields)
    aFields[nI] := FIELDPOS(aFields[nI])
  NEXT
  FOR nI := 1 TO LEN(aFields)
    nField := aFields[nI]
    AADD(aResult, FIELDGET(nField))
    //AADD(aResult, {nField, FIELDGET(nField)})
  NEXT
  cResult := hb_jsonEncode(aResult)
RETURN cResult

FUNCTION DBF_GET_VALUES_RANGE(cJson, nBegin, nEnd)
  LOCAL aFields, nI, nRec, nField
  LOCAL aResult, cResult
  LOCAL aRow
  hb_jsonDecode(cJson, @aFields)
  DBGOTO(nBegin)
  aResult := {}
  FOR nI := 1 TO LEN(aFields)
    aFields[nI] := FIELDPOS(aFields[nI])
  NEXT
  FOR nRec := nBegin TO nEnd
    aRow := {}
    FOR nI := 1 TO LEN(aFields)
      nField := aFields[nI]
      AADD(aRow, FIELDGET(nField))
    NEXT
    AADD(aResult, aRow)
    SKIP 1
  NEXT
  cResult := hb_jsonEncode(aResult)
RETURN cResult

FUNCTION DBF_SET_VALUES(cJson)
  LOCAL aData, nI, aRecord, nField
  hb_jsonDecode(cJson, @aData)
  FOR nI := 1 TO LEN(aData)
    aRecord := aData[nI]
    nField := FIELDPOS(aRecord[1])
    FIELDPUT(nField, aRecord[2])
  NEXT
RETURN NIL

FUNCTION DBF_RECORDS(lCurrent)
  IF lCurrent
    RETURN RecNo()
  ELSE
    RETURN RecCount()
  ENDIF
RETURN NIL

FUNCTION DBF_SELECTAREA(cAlias)
  DbSelectArea(cAlias)
RETURN NIL

FUNCTION DBF_RECORD_LOCK(nRecord, lUnlock)
  nRecord := IIF(nRecord == -1, RecNo(), nRecord)
  IF lUnlock
    DbRUnlock(nRecord)
    RETURN .T.
  ELSE
    RETURN DbRLock(nRecord)
  ENDIF
RETURN NIL

FUNCTION DBF_INDEX_LOAD(cIndexFile)
  OrdListAdd(cIndexFile)
RETURN NIL

FUNCTION DBF_INDEX_SELECT(nOrder)
  OrdSetFocus(nOrder)
RETURN NIL

FUNCTION DBF_INDEX_SEEK(nPosition, lSoftSeek, lFindLast)
  DbSeek(nPosition, lSoftSeek, lFindLast)
RETURN NIL

FUNCTION DBF_ORD_KEY_COUNT()
RETURN OrdKeyCount()

FUNCTION DBF_FOUND()
RETURN Found() 

FUNCTION DBF_EOF()
RETURN EOF() 

FUNCTION DBF_SKIP(lCount)
  DbSkip(lCount)
RETURN NIL

FUNCTION DBF_IS_DELETED()
RETURN Deleted()

FUNCTION ERROR_PROCEDURE(oErr)
  LOCAL nI, cText, cLine
  cText := DebugErrorMessage(oErr) + CLRF
  n := 1
  WHILE ! Empty( ProcName( ++n ) )
     cLine := "Called from " + ProcName( n ) + "(" + hb_ntos( ProcLine( n ) ) + ")" + ;
        iif( ProcLine( n ) > 0, " in module: " + ProcFile( n ), "" ) + CLRF
     cText += cLine
  ENDDO
  ? cText
  cLastError := cText
  Break(oErr)
RETURN NIL

FUNCTION INIT_LIBRARY()
  LOCAL bError := ErrorBlock( {|e| ERROR_PROCEDURE(e) } )
  ? "[HbDBF] Library initialized!" + CLRF
  REQUEST DBFNTX
  REQUEST DBFCDX
  RDDSETDEFAULT( "DBFCDX" )
  REQUEST HB_CODEPAGE_RU866
  REQUEST HB_LANG_RU866
  HB_LANGSELECT("RU866")
RETURN NIL

FUNCTION TEST244()
  LOCAL aData := DbRLockList(), nI
  //dbRLock(21)
  FOR nI := 1 TO 1
    ? HB_ValToExp(aData)
  NEXT
RETURN NIL

FUNCTION DBF_EVAL(cExecutable)
  LOCAL hData := &cExecutable
RETURN hb_jsonEncode(hData)

FUNCTION DBF_MAKE_INDEX(cAlias, cTag, cIndex, cFor)
  DbSelectArea(cAlias)
  INDEX ON &cIndex TAG cTag FOR &cFor TEMPORARY ADDITIVE 
RETURN NIL

#pragma BEGINDUMP
#include <stdio.h>
#include <windows.h>
#include <hbvm.h>
#include <hbapi.h>
#include <hbapiitm.h>

/*
 Заметки об использованных функциях

 NOTE: Функция возвращает размер объекта, а сам объект передаётся вторым аргументом по ссылке!
 HB_SIZE hb_jsonDecode( const char * szSource, PHB_ITEM pValue );

 NOTE: The caller should free the pointer if it's not NULL. [vszakats]
 char * hb_itemGetC( PHB_ITEM pItem )

 NOTE: Caller should not modify the buffer returned by this function. [vszakats]
 const char * hb_itemGetCPtr( PHB_ITEM pItem )

 hb_xfree(rawResult); // Очистка аллоцированной памяти
 hb_itemFreeC(rawResult); // Вызов hb_xfree с дополнительным логгингом
*/


BOOL WINAPI DllMain( HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved )       //This Works!
{
  HB_SYMBOL_UNUSED( hinstDLL );
  HB_SYMBOL_UNUSED( lpvReserved );
  switch( fdwReason )
  {
    case DLL_PROCESS_ATTACH:
      //printf("Attached to main!\n");
      //MessageBox( 0, "We're in DLLMain", "1", 0 );
      hb_vmInit( FALSE );
      hb_itemDoC( "INIT_LIBRARY", 0);
      break;
    case DLL_PROCESS_DETACH:
      hb_vmQuit();
      break;
  }
  return TRUE;
}

HB_EXPORT void* _export DBF_GOTO(long recordNumber)
{
  PHB_ITEM pRecord = hb_itemPutNL( NULL, recordNumber );
  hb_itemDoC( "DBF_GOTO", 1, pRecord);
  hb_itemRelease( pRecord );
  return NULL;
}

HB_EXPORT void* _export DBF_SKIP(long recordCount)
{
  PHB_ITEM pRecord = hb_itemPutNL( NULL, recordCount );
  hb_itemDoC( "DBF_SKIP", 1, pRecord);
  hb_itemRelease( pRecord );
  return NULL;
}

HB_EXPORT void* _export DBF_GET_LAST_ERROR()
{
  PHB_ITEM pValue = hb_itemDoC( "DBF_GET_LAST_ERROR", 0);
  char * rawResult = (char*) hb_itemGetC(pValue);
  return rawResult;
}

HB_EXPORT void* _export DBF_USE(const char* cDatabaseName, const char* cAlias, BOOL lExclusive, const char * cCodepage)
{
  PHB_ITEM pName = hb_itemPutC( NULL, cDatabaseName );
  PHB_ITEM pAlias = hb_itemPutC( NULL, cAlias );
  PHB_ITEM pCodepage = hb_itemPutC( NULL, cCodepage );
  PHB_ITEM pExclusive = hb_itemPutL( NULL, lExclusive );
  hb_itemDoC( "DBF_USE", 4, pName, pAlias, pExclusive, pCodepage);
  hb_itemRelease( pName );
  return NULL;
}

HB_EXPORT void* _export DBF_APPEND()
{
  hb_itemDoC( "DBF_APPEND", 0);
  return NULL;
}

HB_EXPORT void* _export DBF_COMMIT()
{
  hb_itemDoC( "DBF_COMMIT", 0);
  return NULL;
}

HB_EXPORT void* _export DBF_GET_VALUES(const char * cJson)
{
  PHB_ITEM pJson = hb_itemPutC( NULL, cJson );
  PHB_ITEM pResult = hb_itemDoC( "DBF_GET_VALUES", 1, pJson);
  char * rawResult = (char*) hb_itemGetC(pResult);
  hb_itemRelease( pJson );
  hb_itemRelease( pResult );
  return rawResult;
}

HB_EXPORT void* _export DBF_GET_VALUES_RANGE(const char * cJson, long nBegin, long nEnd)
{
  PHB_ITEM pArg1 = hb_itemPutC( NULL, cJson );
  PHB_ITEM pArg2 = hb_itemPutNL( NULL, nBegin );
  PHB_ITEM pArg3 = hb_itemPutNL( NULL, nEnd );
  PHB_ITEM pResult = hb_itemDoC( "DBF_GET_VALUES_RANGE", 3, pArg1, pArg2, pArg3);
  char * rawResult = (char*) hb_itemGetC(pResult);
  hb_itemRelease( pArg1 );
  hb_itemRelease( pArg2 );
  hb_itemRelease( pArg3 );
  hb_itemRelease( pResult );
  return rawResult;
}

HB_EXPORT void* _export DBF_SET_VALUES(const char * cJson)
{
  //MessageBox( 0, cJson, "1", 0 );  
  PHB_ITEM pJson = hb_itemPutC( NULL, cJson );
  hb_itemDoC( "DBF_SET_VALUES", 1, pJson);
  hb_itemRelease( pJson );
  return NULL;
}

HB_EXPORT void* _export DBF_CREATE(const char* cDatabaseName, const char* cJson)
{
  //printf("Create DBF: %s\n", cDatabaseName);
  //printf("Structure: %s\n", cJson);
  //MessageBox( 0, cJson, "1", 0 );
  PHB_ITEM pName = hb_itemPutC( NULL, cDatabaseName );
  PHB_ITEM pJson = hb_itemPutC( NULL, cJson );
  hb_itemDoC( "DBF_CREATE", 2, pName, pJson);
  hb_itemRelease( pName );
  hb_itemRelease( pJson );
  return NULL;
}

HB_EXPORT void* _export DBF_SELECT_AREA(const char* cAlias)
{
  PHB_ITEM pAlias = hb_itemPutC( NULL, cAlias );
  hb_itemDoC( "DBSELECTAREA", 1, pAlias);
  hb_itemRelease( pAlias );
  return NULL;
}

HB_EXPORT void* _export DBF_CLOSE_AREA(const char* cAlias)
{
  PHB_ITEM pAlias = hb_itemPutC( NULL, cAlias );
  hb_itemDoC( "DBF_CLOSE_AREA", 1, pAlias);
  hb_itemRelease( pAlias );
  return NULL;
}

HB_EXPORT HB_BOOL _export DBF_RECORD_LOCK(long recordNumber, BOOL isUnlock)
{
  PHB_ITEM pRecord = hb_itemPutNL( NULL, recordNumber );
  PHB_ITEM pUnlock = hb_itemPutL( NULL, isUnlock );
  PHB_ITEM pResult = hb_itemDoC( "DBF_RECORD_LOCK", 2, pRecord, pUnlock);
  HB_BOOL value = hb_itemGetL( pResult) ;
  hb_itemRelease( pRecord );
  hb_itemRelease( pUnlock );
  hb_itemRelease( pResult );
  return value;
}


HB_EXPORT long _export DBF_RECORDS(BOOL currentRecord)
{
  PHB_ITEM pCurrent = hb_itemPutL( NULL, currentRecord );
  PHB_ITEM pResult = hb_itemDoC( "DBF_RECORDS", 1, pCurrent);
  long nRecords = hb_itemGetNL(pResult);
  hb_itemRelease( pCurrent );
  hb_itemRelease( pResult );
  return nRecords;
}

HB_EXPORT void _export DBF_UNSAFE_FREE(char* target)
{
  hb_itemFreeC(target);
}

HB_EXPORT void _export DBF_INDEX_LOAD(char* cArg1)
{
  PHB_ITEM pArg1 = hb_itemPutC( NULL, cArg1 );
  hb_itemDoC( "DBF_INDEX_LOAD", 1, pArg1);
  hb_itemRelease( pArg1 );
}

HB_EXPORT void _export DBF_INDEX_SELECT(long cArg1)
{
  PHB_ITEM pArg1 = hb_itemPutNL( NULL, cArg1 );
  hb_itemDoC( "DBF_INDEX_SELECT", 1, pArg1);
  hb_itemRelease( pArg1 );
}

  HB_EXPORT void _export DBF_INDEX_SELECT_STR(char* cArg1)
{
  PHB_ITEM pArg1 = hb_itemPutC( NULL, cArg1 );
  hb_itemDoC( "DBF_INDEX_SELECT", 1, pArg1);
  hb_itemRelease( pArg1 );
}

HB_EXPORT HB_BOOL _export DBF_INDEX_SEEK(long arg1, HB_BOOL arg2, HB_BOOL arg3)
{
  PHB_ITEM pArg1 = hb_itemPutNL( NULL, arg1 );
  PHB_ITEM pArg2 = hb_itemPutL( NULL, arg2 );
  PHB_ITEM pArg3 = hb_itemPutL( NULL, arg3 );
  PHB_ITEM pResult = hb_itemDoC( "DBF_INDEX_SEEK", 3, pArg1, pArg2, pArg3);
  HB_BOOL value = hb_itemGetL( pResult) ;
  hb_itemRelease( pArg1 );
  hb_itemRelease( pArg2 );
  hb_itemRelease( pArg3 );
  hb_itemRelease( pResult );
  return value;
}

HB_EXPORT HB_BOOL _export DBF_FOUND()
{
  PHB_ITEM pResult = hb_itemDoC( "DBF_FOUND", 0);
  HB_BOOL value = hb_itemGetL( pResult) ;
  hb_itemRelease( pResult );
  return value;
}

HB_EXPORT HB_BOOL _export DBF_IS_DELETED()
{
  PHB_ITEM pResult = hb_itemDoC( "DBF_IS_DELETED", 0);
  HB_BOOL value = hb_itemGetL( pResult) ;
  hb_itemRelease( pResult );
  return value;
}

HB_EXPORT HB_BOOL _export DBF_EOF()
{
  PHB_ITEM pResult = hb_itemDoC( "DBF_EOF", 0);
  HB_BOOL value = hb_itemGetL( pResult) ;
  hb_itemRelease( pResult );
  return value;
}

HB_EXPORT long _export DBF_ORD_KEY_COUNT()
{
  PHB_ITEM pResult = hb_itemDoC( "DBF_ORD_KEY_COUNT", 0);
  long nRecords = hb_itemGetNL(pResult);
  hb_itemRelease( pResult );
  return nRecords;
}


HB_EXPORT void* _export DBF_EVAL(const char * cCode)
{
  PHB_ITEM pCode = hb_itemPutC( NULL, cCode );
  PHB_ITEM pResult = hb_itemDoC( "DBF_EVAL", 1, pCode);
  char * rawResult = (char*) hb_itemGetC(pResult);
  hb_itemRelease( pCode );
  hb_itemRelease( pResult );
  return rawResult;
}

HB_EXPORT void* _export DBF_MAKE_INDEX(const char * cAlias, const char * cTag, const char * cIndex, const char * cFor)
{
  PHB_ITEM pAlias = hb_itemPutC( NULL, cAlias );
    PHB_ITEM pTag = hb_itemPutC( NULL, cTag );
  PHB_ITEM pIndex = hb_itemPutC( NULL, cIndex );
  PHB_ITEM pFor = hb_itemPutC( NULL, cFor );
  hb_itemDoC( "DBF_MAKE_INDEX", 4, pAlias, pTag, pIndex, pFor );
  hb_itemRelease( pAlias );
  hb_itemRelease( pIndex );
  hb_itemRelease( pTag );
  hb_itemRelease( pFor );
  return NULL;
}

#pragma ENDDUMP