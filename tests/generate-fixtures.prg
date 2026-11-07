/* Maintainer tool: create only new, synthetic files in the supplied directory. */
REQUEST DBFCDX, DBFFPT, HB_CODEPAGE_UA866

PROCEDURE Main( cDir )
   LOCAL cName, n, oError
   LOCAL aNames := { "basic.dbf", "basic.fpt", "basic_id.cdx", "basic_name.cdx", ;
      "special.dbf", "special.fpt", "special_id.cdx", "special_time.cdx" }
   IF Empty( cDir ) .OR. ! hb_DirExists( cDir )
      OutErr( "Provide an existing output directory" + hb_eol() ); ErrorLevel( 2 ); RETURN
   ENDIF
   cDir := hb_DirSepAdd( cDir )
   FOR EACH cName IN aNames
      IF hb_FileExists( cDir + cName )
         OutErr( "Refusing to overwrite " + cDir + cName + hb_eol() ); ErrorLevel( 2 ); RETURN
      ENDIF
   NEXT
   hb_cdpSelect( "UA866" ); rddSetDefault( "DBFCDX" )
   SET AUTOPEN OFF
   ErrorBlock( {|e| Break( e ) } )
   BEGIN SEQUENCE
      dbCreate( cDir + "basic.dbf", { { "ID", "N", 6, 0 }, { "NAME", "C", 20, 0 }, ;
         { "BIRTH", "D", 8, 0 }, { "ACTIVE", "L", 1, 0 }, { "AMOUNT", "N", 10, 2 }, ;
         { "NOTE", "M", 10, 0 } }, "DBFCDX" )
      dbUseArea( .T., "DBFCDX", cDir + "basic.dbf", "BASIC", .F., .F., "UA866" )
      APPEND BLANK
      REPLACE ID WITH 1, NAME WITH "ALICE", BIRTH WITH hb_SToD( "19900102" ), ;
         ACTIVE WITH .T., AMOUNT WITH 12.50, NOTE WITH "First memo"
      APPEND BLANK
      REPLACE ID WITH 2, NAME WITH "BOB", BIRTH WITH hb_SToD( "19850506" ), ;
         ACTIVE WITH .F., AMOUNT WITH 7.25, NOTE WITH "Second memo"
      APPEND BLANK
      REPLACE ID WITH 3, NAME WITH "CAROL", BIRTH WITH hb_SToD( "20000101" ), ;
         ACTIVE WITH .T., AMOUNT WITH 0, NOTE WITH ""
      INDEX ON ID TAG ID TO ( cDir + "basic_id.cdx" )
      INDEX ON NAME TAG NAME TO ( cDir + "basic_name.cdx" )
      dbCloseArea()

      dbCreate( cDir + "special.dbf", { { "IDZ", "+", 4, 0 }, { "TSZ", "=", 8, 0 }, ;
         { "VMZ", "^", 8, 0 }, { "STAMP", "@", 8, 0 }, { "NAME", "C", 20, 0 } }, "DBFCDX" )
      dbUseArea( .T., "DBFCDX", cDir + "special.dbf", "SPECIAL", .F., .F., "UA866" )
      FOR n := 1 TO 3
         APPEND BLANK
         REPLACE NAME WITH "ITEM" + hb_ntos( n ), STAMP WITH hb_SToT( "2026010112000" + hb_ntos( n ) + "123" )
      NEXT
      INDEX ON IDZ TAG IDZ TO ( cDir + "special_id.cdx" )
      INDEX ON STAMP TAG STAMP TO ( cDir + "special_time.cdx" )
      dbCloseArea()
      OutStd( "Created two synthetic fixtures (three records each)." + hb_eol() )
   RECOVER USING oError
      OutErr( "Fixture generation failed: " + oError:Description + hb_eol() )
      ErrorLevel( 4 ); CLOSE ALL
   END SEQUENCE
RETURN
