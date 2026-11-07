/* Independent read-only verification of temporary files written by LightTests. */
REQUEST DBFCDX, DBFFPT, HB_CODEPAGE_UA866

PROCEDURE Main( cRoot )
   LOCAL cDir, cNew := Chr(141)+Chr(142)+Chr(130)+Chr(128)+Chr(159)
   LOCAL cUpdated := Chr(136)+Chr(135)+Chr(140)+Chr(133)+Chr(141)+Chr(133)+Chr(141)+Chr(142)
   LOCAL oError
   IF Empty( cRoot )
      OutErr( "Expected temporary fixture directory" + hb_eol() ); ErrorLevel( 2 ); RETURN
   ENDIF
   hb_cdpSelect( "UA866" ); rddSetDefault( "DBFCDX" )
   SET AUTOPEN OFF
   SET DELETED OFF
   ErrorBlock( {|e| Break( e ) } )
   BEGIN SEQUENCE
      cDir := hb_DirSepAdd( cRoot ) + "typed-insert" + hb_ps()
      dbUseArea( .T., "DBFCDX", cDir + "basic.dbf", "CHECK", .F., .T., "UA866" )
      OrdListAdd( cDir + "basic_id.cdx" ); OrdListAdd( cDir + "basic_name.cdx" )
      Check( LastRec() == 4, "insert count" )
      OrdSetFocus( "ID" ); Check( dbSeek( 9 ) .AND. RecNo() == 4, "insert ID index" )
      Check( RTrim( FIELD->NAME ) == cNew .AND. FIELD->BIRTH == hb_SToD( "20000102" ) .AND. FIELD->AMOUNT == 42.75, "insert fields" )
      OrdSetFocus( "NAME" ); Check( dbSeek( PadR( cNew, 20 ) ) .AND. FIELD->ID == 9, "insert name index" )
      dbCloseArea()

      dbUseArea( .T., "DBFCDX", cDir + "special.dbf", "CHECK", .F., .T., "UA866" )
      OrdListAdd( cDir + "special_id.cdx" ); OrdListAdd( cDir + "special_time.cdx" )
      Check( LastRec() == 4, "special insert count" ); dbGoTo( 4 )
      Check( FIELD->IDZ != 999 .AND. FIELD->VMZ != 999 .AND. RTrim( FIELD->NAME ) == "FOUR", "generated insert fields" )
      OrdSetFocus( "IDZ" ); Check( dbSeek( FIELD->IDZ ) .AND. RecNo() == 4, "special ID index" )
      OrdSetFocus( "STAMP" ); Check( dbSeek( hb_SToT( "20200102030405000" ) ) .AND. RecNo() == 4, "special timestamp index" )
      dbCloseArea()

      cDir := hb_DirSepAdd( cRoot ) + "typed-update" + hb_ps()
      dbUseArea( .T., "DBFCDX", cDir + "basic.dbf", "CHECK", .F., .T., "UA866" )
      OrdListAdd( cDir + "basic_id.cdx" ); OrdListAdd( cDir + "basic_name.cdx" )
      OrdSetFocus( "NAME" ); Check( dbSeek( PadR( cUpdated, 20 ) ) .AND. RecNo() == 1, "updated name index" )
      Check( Deleted() .AND. FIELD->AMOUNT == 12.50 .AND. FIELD->NOTE == "First memo", "updated and unmapped fields" )
      dbCloseArea()

      cDir := hb_DirSepAdd( cRoot ) + "concurrent" + hb_ps()
      dbUseArea( .T., "DBFCDX", cDir + "basic.dbf", "CHECK", .F., .T., "UA866" )
      OrdListAdd( cDir + "basic_id.cdx" ); OrdListAdd( cDir + "basic_name.cdx" )
      OrdSetFocus( "ID" ); Check( dbSeek( 51 ) .AND. RecNo() == 1 .AND. RTrim(FIELD->NAME) == "ALICE", "concurrent updates and index" )
      dbCloseArea()
      OutStd( "PASS: independent Harbour typed writes and indexes" + hb_eol() )
   RECOVER USING oError
      OutErr( "Independent verification failed: " + oError:Description + hb_eol() )
      ErrorLevel( 4 ); CLOSE ALL
   END SEQUENCE
RETURN

STATIC PROCEDURE Check( lCondition, cMessage )
   LOCAL oError
   IF ! lCondition
      oError := ErrorNew(); oError:Description := cMessage; Break( oError )
   ENDIF
RETURN
