/* Generate and independently verify a new synthetic multi-tag CDX fixture. */
REQUEST DBFCDX, HB_CODEPAGE_UA866

PROCEDURE Main( cDir )
   LOCAL cDbf, cCdx, cLower, cUpper, cOther, oError, cStage := "uppercase"
   IF Empty( cDir ) .OR. ! hb_DirExists( cDir )
      OutErr( "Provide an existing output directory" + hb_eol() ); ErrorLevel( 2 ); RETURN
   ENDIF
   cDir := hb_DirSepAdd( cDir )
   cDbf := cDir + "complex.dbf"; cCdx := cDir + "complex.cdx"
   IF hb_FileExists( cDbf ) .OR. hb_FileExists( cCdx )
      OutErr( "Refusing to overwrite fixture" + hb_eol() ); ErrorLevel( 2 ); RETURN
   ENDIF
   cLower := Chr(160)+Chr(171)+Chr(168)+Chr(225)+Chr(160)
   cUpper := Chr(128)+Chr(139)+Chr(136)+Chr(145)+Chr(128)
   cOther := Chr(129)+Chr(142)+Chr(144)+Chr(136)+Chr(145)
   hb_cdpSelect( "UA866" ); rddSetDefault( "DBFCDX" )
   SET AUTOPEN OFF
   ErrorBlock( {|e| Break( e ) } )
   BEGIN SEQUENCE
      Check( Upper( cLower ) == cUpper, "UA866 uppercase" )
      cStage := "create"
      dbCreate( cDbf, { { "ID", "N", 6, 0 }, { "NAME", "C", 20, 0 }, { "VIEW", "N", 4, 0 } }, "DBFCDX" )
      dbUseArea( .T., "DBFCDX", cDbf, "COMPLEX", .F., .F., "UA866" )
      cStage := "records"
      APPEND BLANK; REPLACE ID WITH 1, NAME WITH cLower, VIEW WITH 1
      APPEND BLANK; REPLACE ID WITH 2, NAME WITH cUpper, VIEW WITH 1
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
      dbCloseArea()
      dbUseArea( .T., "DBFCDX", cDbf, "COMPLEX", .F., .T., "UA866" )
      OrdListAdd( cCdx )
      Check( OrdCount() == 3, "multi-tag count" )
      OrdSetFocus( "NAME" ); Check( dbSeek( cUpper ) .AND. FIELD->ID == 1, "uppercase search" )
      OrdSetFocus( "VIEWNAME" ); Check( dbSeek( Str(1,4,0)+PadR(cUpper,30) ) .AND. FIELD->ID == 1, "composite search" )
      dbCloseArea()
      OutStd( "PASS: generated and verified multi-tag fixture" + hb_eol() )
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
