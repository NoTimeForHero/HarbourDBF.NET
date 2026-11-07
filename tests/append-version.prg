/* Probe Harbour's shared and exclusive APPEND behaviour on a disposable fixture copy. */
REQUEST DBFCDX, DBFFPT, HB_CODEPAGE_UA866

PROCEDURE Main( cDir, cMode )
   LOCAL nBefore, nAfter, lShared := cMode == "shared", oError
   IF Empty(cDir) .OR. !(cMode == "shared" .OR. cMode == "exclusive")
      OutErr("Expected copied fixture directory and mode" + hb_eol()); ErrorLevel(2); RETURN
   ENDIF
   cDir := hb_DirSepAdd(cDir)
   hb_cdpSelect("UA866"); rddSetDefault("DBFCDX")
   SET AUTOPEN OFF
   ErrorBlock({|e| Break(e)})
   BEGIN SEQUENCE
      dbUseArea(.T., "DBFCDX", cDir + "special.dbf", "PROBE", lShared, .F., "UA866")
      OrdListAdd(cDir + "special_id.cdx"); OrdListAdd(cDir + "special_time.cdx")
      dbGoBottom(); nBefore := FIELD->VMZ
      APPEND BLANK
      REPLACE FIELD->NAME WITH "FOUR", FIELD->STAMP WITH hb_SToT("20200102030405000")
      dbCommit(); nAfter := FIELD->VMZ
      IF nAfter != nBefore + iif(lShared, 2, 1)
         OutErr("Unexpected version progression: " + hb_ntos(nBefore) + " -> " + hb_ntos(nAfter) + hb_eol())
         ErrorLevel(4)
      ENDIF
      dbCloseArea()
   RECOVER USING oError
      OutErr("Append version probe failed: " + oError:Description + hb_eol())
      ErrorLevel(4); CLOSE ALL
   END SEQUENCE
RETURN
