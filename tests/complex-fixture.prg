/* Generate and independently verify a new synthetic multi-tag CDX fixture. */
REQUEST DBFCDX, HB_CODEPAGE_UA866

PROCEDURE Main( cDir, cMode )
   LOCAL cDbf, cCdx, cLower, cUpper, cOther, oError, cStage := "uppercase"
   IF Empty( cDir ) .OR. ! hb_DirExists( cDir )
      OutErr( "Provide an existing output directory" + hb_eol() ); ErrorLevel( 2 ); RETURN
   ENDIF
   cDir := hb_DirSepAdd( cDir )
   cDbf := cDir + "complex.dbf"; cCdx := cDir + "complex.cdx"
   cLower := Chr(160)+Chr(171)+Chr(168)+Chr(225)+Chr(160)
   cUpper := Chr(128)+Chr(139)+Chr(136)+Chr(145)+Chr(128)
   cOther := Chr(129)+Chr(142)+Chr(144)+Chr(136)+Chr(145)
   hb_cdpSelect( "UA866" ); rddSetDefault( "DBFCDX" )
   SET AUTOPEN OFF
   ErrorBlock( {|e| Break( e ) } )
   BEGIN SEQUENCE
      Check( Upper( cLower ) == cUpper, "UA866 uppercase" )
      IF cMode == "verify"
         SET DELETED OFF
         dbUseArea( .T., "DBFCDX", cDbf, "COMPLEX", .F., .T., "UA866" )
         OrdListAdd( cCdx )
         Check( LastRec() == 5, "typed insert count" )
         OrdSetFocus( "VIEWNAME" )
         Check( dbSeek( Str(3,4,0)+PadR(cUpper,30) ) .AND. FIELD->ID == 5, "typed insert composite key" )
         Check( dbSeek( Str(1,4,0)+PadR(cOther,30) ) .AND. FIELD->ID == 1, "typed update composite key" )
         OrdSetFocus( "NAME" )
         Check( dbSeek( PadR(cOther,20) ) .AND. FIELD->ID == 1, "typed update uppercase key" )
         dbCloseArea()
         dbUseArea( .T., "DBFCDX", cDir + "numeric.dbf", "NUMERIC", .F., .T., "UA866" )
         Check( LastRec() == 4, "numeric record count" )
         dbGoTo( 1 ); Check( FIELD->AMOUNT == 999999999999.98 .AND. FIELD->RATE == 999999999.99998, "positive numeric boundary" )
         dbGoTo( 2 ); Check( FIELD->AMOUNT == -99999999999.98 .AND. FIELD->RATE == -99999999.99998, "negative numeric boundary" )
         dbGoTo( 4 ); Check( FIELD->AMOUNT == 0.01 .AND. FIELD->RATE == 0.00001, "small numeric values" )
         dbCloseArea()
         dbUseArea( .T., "DBFCDX", cDir + "wide.dbf", "WIDE", .F., .T., "UA866" )
         dbGoTo( 1 ); Check( FIELD->NUM == 9007199254740991, "safe integer boundary" )
         dbCloseArea()
         OutStd( "PASS: independently verified typed expression indexes" + hb_eol() )
      ELSE
      Check( ! hb_FileExists( cDbf ) .AND. ! hb_FileExists( cCdx ) .AND. ! hb_FileExists( cDir + "numeric.dbf" ) .AND. ! hb_FileExists( cDir + "wide.dbf" ), "refusing to overwrite fixture" )
      cStage := "create"
      dbCreate( cDbf, { { "ID", "N", 6, 0 }, { "NAME", "C", 20, 0 }, { "VIEW", "N", 4, 0 }, { "BIRTH", "D", 8, 0 } }, "DBFCDX" )
      dbUseArea( .T., "DBFCDX", cDbf, "COMPLEX", .F., .F., "UA866" )
      cStage := "records"
      APPEND BLANK; REPLACE ID WITH 1, NAME WITH cLower, VIEW WITH 1, BIRTH WITH hb_SToD("20000102")
      APPEND BLANK; REPLACE ID WITH 2, NAME WITH cUpper, VIEW WITH 1, BIRTH WITH hb_SToD("20000102")
      APPEND BLANK; REPLACE ID WITH 3, NAME WITH cOther, VIEW WITH 1
      APPEND BLANK; REPLACE ID WITH 4, NAME WITH cUpper, VIEW WITH 2
      cStage := "index id"
      INDEX ON ID TAG ID TO ( cCdx )
      cStage := "index name"
      dbGoTop()
      INDEX ON UPPER(PADR(ALLTRIM(NAME),20)) TAG NAME TO ( cCdx ) ADDITIVE
      cStage := "index composite"
      dbGoTop()
      INDEX ON STR(VIEW,4,0)+UPPER(PADR(NAME,30)) TAG VIEWNAME TO ( cCdx ) ADDITIVE
      cStage := "index category"
      dbGoTop()
      INDEX ON IIF(VIEW=1,'A','B')+UPPER(PADR(NAME,30)) TAG CATEGORY TO ( cCdx ) ADDITIVE
      cStage := "index date"
      dbGoTop()
      INDEX ON DTOS(BIRTH) TAG DATEKEY TO ( cCdx ) ADDITIVE
      dbGoTop()
      INDEX ON DTOS(BIRTH)+STR(VIEW,4,0) TAG DATEVIEW TO ( cCdx ) ADDITIVE
      cStage := "unsupported indexes"
      dbGoTop()
      INDEX ON ID TAG BADFOR TO ( cCdx ) FOR VIEW=1 ADDITIVE
      dbGoTop()
      INDEX ON ID TAG BADUNIQUE TO ( cCdx ) UNIQUE ADDITIVE
      dbGoTop()
      INDEX ON ID TAG BADDESC TO ( cCdx ) DESCENDING ADDITIVE
      dbCloseArea()
      dbUseArea( .T., "DBFCDX", cDbf, "COMPLEX", .F., .T., "UA866" )
      OrdListAdd( cCdx )
      Check( OrdCount() == 9, "multi-tag count" )
      OrdSetFocus( "NAME" ); Check( dbSeek( cUpper ) .AND. FIELD->ID == 1, "uppercase search" )
      OrdSetFocus( "VIEWNAME" ); Check( dbSeek( Str(1,4,0)+PadR(cUpper,30) ) .AND. FIELD->ID == 1, "composite search" )
      OrdSetFocus( "CATEGORY" ); Check( dbSeek( "A"+PadR(cUpper,30) ) .AND. FIELD->ID == 1, "category search" )
      OrdSetFocus( "DATEKEY" ); Check( dbSeek( "20000102" ) .AND. FIELD->ID == 1, "date key search" )
      dbCloseArea()
      dbCreate( cDir + "numeric.dbf", { { "ID", "N", 4, 0 }, { "AMOUNT", "N", 15, 2 }, { "RATE", "N", 15, 5 } }, "DBFCDX" )
      dbUseArea( .T., "DBFCDX", cDir + "numeric.dbf", "NUMERIC", .F., .F., "UA866" )
      APPEND BLANK; REPLACE ID WITH 1, AMOUNT WITH 999999999999.99, RATE WITH 999999999.99999
      APPEND BLANK; REPLACE ID WITH 2, AMOUNT WITH -99999999999.99, RATE WITH -99999999.99999
      APPEND BLANK; REPLACE ID WITH 3, AMOUNT WITH 0.01, RATE WITH 0.00001
      dbCloseArea()
      dbCreate( cDir + "wide.dbf", { { "NUM", "N", 20, 0 } }, "DBFCDX" )
      dbUseArea( .T., "DBFCDX", cDir + "wide.dbf", "WIDE", .F., .F., "UA866" )
      APPEND BLANK; REPLACE NUM WITH 9007199254740993
      dbCloseArea()
      OutStd( "PASS: generated and verified multi-tag fixture" + hb_eol() )
      ENDIF
   RECOVER USING oError
      OutErr( "Multi-tag fixture failed at " + cStage + ": " + oError:Description + " / " + oError:Operation + hb_eol() )
      ErrorLevel( 4 ); CLOSE ALL
   END SEQUENCE
RETURN

STATIC PROCEDURE Check( lCondition, cMessage )
   LOCAL oError
   IF ! lCondition
      oError := ErrorNew(); oError:Description := cMessage; Break( oError )
   ENDIF
RETURN
