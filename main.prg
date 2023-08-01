//DLLTEST2.PRG
#pragma BEGINDUMP
#include <stdio.h>
#include <windows.h>
#include <hbvm.h>
#include <hbapi.h>
#include <hbapiitm.h>

/*
 Заметки об использованных функциях

 NOTE: The caller should free the pointer if it's not NULL. [vszakats]
 char * hb_itemGetC( PHB_ITEM pItem )

 NOTE: Caller should not modify the buffer returned by this function. [vszakats]
 const char * hb_itemGetCPtr( PHB_ITEM pItem )
*/


BOOL WINAPI DllMain( HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved )       //This Works!
{
  HB_SYMBOL_UNUSED( hinstDLL );
  HB_SYMBOL_UNUSED( lpvReserved );
  switch( fdwReason )
  {
    case DLL_PROCESS_ATTACH:
      //MessageBox( 0, "We're in DLLMain", "1", 0 );
      hb_vmInit( FALSE );
      break;
    case DLL_PROCESS_DETACH:
      hb_vmQuit();
      break;
  }
  return TRUE;
}

HB_EXPORT void * _export DBF_USE( const char * cDatabaseName)
{
  PHB_ITEM pName = hb_itemPutC( NULL, cDatabaseName );
  hb_itemDoC( "DBF_USE", 1, pName);
  return NULL;
}

HB_EXPORT void * _export DBF_CREATE( const char * cDatabaseName, const char * cJsonStructure)
{
  PHB_ITEM pName = hb_itemPutC( NULL, cDatabaseName );
  PHB_ITEM pJson = hb_itemPutC( NULL, cJsonStructure );
  hb_itemDoC( "DBF_CREATE", 2, pName, pJson);
  hb_itemRelease( pName );
  hb_itemRelease( pJson );
  return NULL;
}

HB_EXPORT char * _export C_TEST( const char * cProcName, const char * cText1 )
{
  //if (TRUE) return NULL;
  PHB_ITEM pResult;
  PHB_ITEM pItem1;
  PHB_ITEM pItem2;
  char *rawResult;

  pItem1 = hb_itemPutC( NULL, cProcName );
  pItem2 = hb_itemPutC( NULL, cText1 );
  pResult = hb_itemDoC( "TEST_COMBINE", 2, pItem1, pItem2 );

  // Способ 1: Получить указатель, но возможно строку соберёт GC...
  rawResult = hb_itemGetCPtr(pResult);

  // Способ 2: Сделать копию но потом понадобится вызывать высвобождение буфера!
  //rawResult = hb_itemGetC(pResult);
  //hb_itemFreeC(rawResult);
  //hb_xfree(rawResult); // Аналогично предыдущему но без DEBUG LOG-ов

  //hb_itemDoC( "TESTHB", 0, (PHB_ITEM *) 0);
  //MessageBox( 0, cProcName, "1", 0 );
  //MessageBox( 0, cText1, "1", 0 );
  //MessageBox( 0, rawResult, "1", 0 );
  hb_itemRelease( pItem1 );
  hb_itemRelease( pItem2 );
  hb_itemRelease( pResult );
  //return hb_itemGetC( pResult );
  return rawResult;
}
#pragma ENDDUMP

//#include "inkey.ch"
//#include "fileio.ch"
//#include "hbclass.ch"
//#include "common.ch"

FUNCTION DBF_USE(cAlias)
  USE (cAlias)
RETURN NIL

FUNCTION DBF_CREATE(cName, cStructure)
  LOCAL aStructure
  aStructure := hb_jsonDecode(cName)
  dbCreate(cName, aStructure)
RETURN NIL

function TEST_COMBINE(cArg1, cArg2)
RETURN "Combined: " + cArg1 + cArg2

//function TESTHB(cTest)
//set alternate to E:\EY\597harbour\dlltest\test1.dat
//set alternate to test1.dat
//set alternate on
//?
//? "Inside testhb()"
//?
//close alternate
//return NIL
