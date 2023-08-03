//#include "inkey.ch"
//#include "fileio.ch"
//#include "hbclass.ch"
//#include "common.ch"
#define CLRF CHR(13) + CHR(10)

REQUEST DBFCDX, DBFFPT

STATIC cLastError := NIL

FUNCTION DBF_GET_LAST_ERROR()
RETURN cLastError

FUNCTION DBF_USE(cFile, cAlias, lExclusive, cCodepage)
  LOCAL cRdd := RddSetDefault()
  cCodepage := IIF(LEN(cCodepage) == 0, "RU866", cCodepage)
  // ? HB_ValToExp({cRdd, cFile, cAlias, lExclusive, cCodepage})
  IF LEN(cFile) != 0
    DbUseArea(.F., cRdd, cFile, cAlias, !lExclusive, .F., cCodepage)
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

FUNCTION DBF_SET_VALUES(cJson)
  LOCAL aData, nI, aRecord, nField
  hb_jsonDecode(cJson, @aData)
  FOR nI := 1 TO LEN(aData)
    aRecord := aData[nI]
    nField := FIELDPOS(aRecord[1])
    FIELDPUT(nField, aRecord[2])
  NEXT
RETURN NIL

FUNCTION DBF_SELECTAREA(cAlias)
  DbSelectArea(cAlias)
RETURN NIL

// Обёртка потому что DbRLock не вызывается из C кода
FUNCTION DBF_RECORD_LOCK(nRecord, lUnlock)
  nRecord := IIF(nRecord == -1, RecNo(), nRecord)
  IF lUnlock
    DbRUnlock(nRecord)
  ELSE
    DbRLock(nRecord)
  ENDIF
RETURN NIL

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
RETURN NIL

FUNCTION TEST244()
  LOCAL aData := DbRLockList(), nI
  //dbRLock(21)
  FOR nI := 1 TO 1
    ? HB_ValToExp(aData)
  NEXT
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

HB_EXPORT void* _export DBF_GET_LAST_ERROR()
{
  PHB_ITEM pValue = hb_itemDoC( "DBF_GET_LAST_ERROR", 0);
  char * rawResult = (char*) hb_itemGetCPtr(pValue);
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

HB_EXPORT void* _export DBF_RECORD_LOCK(long recordNumber, BOOL isUnlock)
{
  PHB_ITEM pRecord = hb_itemPutNL( NULL, recordNumber );
  PHB_ITEM pUnlock = hb_itemPutL( NULL, isUnlock );
  hb_itemDoC( "DBF_RECORD_LOCK", 2, pRecord, pUnlock);
  hb_itemRelease( pRecord );
  hb_itemRelease( pUnlock );
  return NULL;
}


#pragma ENDDUMP