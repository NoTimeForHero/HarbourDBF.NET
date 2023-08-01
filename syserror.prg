#include "error.ch"
#include "common.ch"
#include "error.ch"
#include "fileio.ch"
#include "hbmemvar.ch"
#include "hbver.ch"

*-----------------------------------------------------------------------------*
FUNCTION DebugErrorMessage( oError )
    *-----------------------------------------------------------------------------*
       // start error message
       LOCAL cMessage := iif( oError:severity > ES_WARNING, "Error", "Warning" ) + " "
       LOCAL n
    
       // add subsystem name if available
       IF ISCHARACTER( oError:subsystem )
          cMessage += oError:subsystem()
       ELSE
          cMessage += "???"
       ENDIF
    
       // add subsystem's error code if available
       IF ISNUMBER( oError:subCode )
          cMessage += "/" + hb_ntos( oError:subCode )
       ELSE
          cMessage += "/???"
       ENDIF
    
       // add error description if available
       IF ISCHARACTER( oError:description )
          cMessage += " " + oError:description
       ENDIF
    
       // add either filename or operation
       DO CASE
       CASE !Empty( oError:filename )
          cMessage += ": " + oError:filename
       CASE !Empty( oError:operation )
          cMessage += ": " + oError:operation
       ENDCASE
    
       // add OS error code if available
       IF !Empty( oError:osCode )
          cMessage += CRLF
          cMessage += "OS Error: " + GetOSErrorDescription( oError:osCode )
       ENDIF
    
       IF ValType( oError:args ) == "A"
          cMessage += CRLF
          cMessage += "   Args:" + CRLF
          FOR n := 1 TO Len( oError:args )
             cMessage += ;
                "     [" + hb_ntos( n, 2 ) + "] = " + ValType( oError:args[ n ] ) + ;
                "   " + cValToChar( cValToChar( oError:args[ n ] ) ) + ;
                iif( ValType( oError:args[ n ] ) == "A", " length: " + ;
                hb_ntos( Len( oError:args[ n ] ) ), "" ) + iif( n < Len( oError:args ), CRLF, "" )
          NEXT
       ENDIF
    
    RETURN cMessage

    *-----------------------------------------------------------------------------*
FUNCTION GetOSErrorDescription( nError )
    *-----------------------------------------------------------------------------*
       LOCAL nPos
       STATIC aError := {}
    
       IF Len( aError ) == 0
          AAdd( aError, {  1, "Invalid function number                  " } )
          AAdd( aError, {  2, "File not found                           " } )
          AAdd( aError, {  3, "Path not found                           " } )
          AAdd( aError, {  4, "Too many open files (no handles left)    " } )
          AAdd( aError, {  5, "Access denied                            " } )
          AAdd( aError, {  6, "Invalid handle                           " } )
          AAdd( aError, {  7, "Memory control blocks destroyed          " } )
          AAdd( aError, {  8, "Insufficient memory                      " } )
          AAdd( aError, {  9, "Invalid memory block address             " } )
          AAdd( aError, { 10, "Invalid environment                      " } )
          AAdd( aError, { 11, "Invalid format                           " } )
          AAdd( aError, { 12, "Invalid access code                      " } )
          AAdd( aError, { 13, "Invalid data                             " } )
          AAdd( aError, { 15, "Invalid drive was specified              " } )
          AAdd( aError, { 16, "Attempt to remove the current directory  " } )
          AAdd( aError, { 17, "Not same device                          " } )
          AAdd( aError, { 18, "No more files                            " } )
          AAdd( aError, { 19, "Attempt to write to write-protected media" } )
          AAdd( aError, { 20, "Unknown unit                             " } )
          AAdd( aError, { 21, "Drive not ready                          " } )
          AAdd( aError, { 22, "Unknown command                          " } )
          AAdd( aError, { 23, "Data CRC error                           " } )
          AAdd( aError, { 24, "Bad request structure length             " } )
          AAdd( aError, { 25, "Seek error                               " } )
          AAdd( aError, { 26, "Unknown media type                       " } )
          AAdd( aError, { 27, "Sector not found                         " } )
          AAdd( aError, { 28, "Printer out of paper                     " } )
          AAdd( aError, { 29, "Write fault                              " } )
          AAdd( aError, { 30, "Read fault                               " } )
          AAdd( aError, { 31, "General failure                          " } )
          AAdd( aError, { 32, "Sharing violation                        " } )
          AAdd( aError, { 33, "Lock violation                           " } )
          AAdd( aError, { 34, "Invalid disk change                      " } )
          AAdd( aError, { 35, "FCB unavailable                          " } )
          AAdd( aError, { 36, "Sharing buffer overflow                  " } )
          AAdd( aError, { 38, "Unable to complete the operation         " } )
          AAdd( aError, { 50, "Network request not supported            " } )
          AAdd( aError, { 51, "Remote computer not listening            " } )
          AAdd( aError, { 52, "Duplicate name on network                " } )
          AAdd( aError, { 53, "Network path not found                   " } )
          AAdd( aError, { 54, "Network busy                             " } )
          AAdd( aError, { 55, "Network device no longer exists          " } )
          AAdd( aError, { 56, "NETBIOS command limit exceeded           " } )
          AAdd( aError, { 57, "System error, NETBIOS error              " } )
          AAdd( aError, { 58, "Incorrect response from network          " } )
          AAdd( aError, { 59, "Unexpected network error                 " } )
          AAdd( aError, { 60, "Incompatible remote adapter              " } )
          AAdd( aError, { 61, "Print queue full                         " } )
          AAdd( aError, { 62, "Not enough space for print file          " } )
          AAdd( aError, { 63, "Print file was cancelled                 " } )
          AAdd( aError, { 64, "Network name was denied                  " } )
          AAdd( aError, { 65, "Access denied                            " } )
          AAdd( aError, { 66, "Network device type incorrect            " } )
          AAdd( aError, { 67, "Network name not found                   " } )
          AAdd( aError, { 68, "Network name limit exceeded              " } )
          AAdd( aError, { 69, "NETBIOS session limit exceeded           " } )
          AAdd( aError, { 70, "Sharing temporarily paused               " } )
          AAdd( aError, { 71, "Network request not accepted             " } )
          AAdd( aError, { 72, "Print or disk redirection is paused      " } )
          AAdd( aError, { 80, "File exists                              " } )
          AAdd( aError, { 82, "Cannot make directory entry              " } )
          AAdd( aError, { 83, "Fail on INT 24                           " } )
          AAdd( aError, { 84, "Too many redirections                    " } )
          AAdd( aError, { 85, "Duplicate redirection                    " } )
          AAdd( aError, { 86, "Invalid password                         " } )
          AAdd( aError, { 87, "Invalid parameter                        " } )
          AAdd( aError, { 88, "Network data fault                       " } )
          AAdd( aError, { 89, "Function not supported by network        " } )
          AAdd( aError, { 90, "Required system component not installed  " } )
       ENDIF
    
       IF ( nPos := AScan( aError, {| x | x[ 1 ] == nError } ) ) > 0
          RETURN hb_ntos( aError[ nPos ][ 1 ] ) + "=" + Trim( aError[ nPos ][ 2 ] )
       ENDIF
    
    RETURN hb_ntos( nError ) + "=Unknown error"


*-----------------------------------------------------------------------------*
FUNCTION cValToChar( uVal )
    *-----------------------------------------------------------------------------*
    
       SWITCH ValType( uVal )
       CASE "C"
       CASE "M"
    
          RETURN uVal
    
       CASE "D"
    
    #ifdef __XHARBOUR__
          IF HasTimePart( uVal )
             RETURN iif( Year( uVal ) == 0, TToC( uVal, 2 ), TToC( uVal ) )
          ENDIF
    #endif
          RETURN DToC( uVal )
    
       CASE "T"
    
    #ifdef __XHARBOUR__
          RETURN iif( Year( uVal ) == 0, TToC( uVal, 2 ), TToC( uVal ) )
    #else
          RETURN iif( Year( uVal ) == 0, hb_TToC( uVal, '', Set( _SET_TIMEFORMAT ) ), hb_TToC( uVal ) )
    #endif
    
       CASE "L"
    
          RETURN iif( uVal, "T", "F" )
    
       CASE "N"
    
          RETURN hb_ntos( uVal )
    
       CASE "B"
    
          RETURN "{|| ... }"
    
       CASE "A"
    
          RETURN "{ ... }"
    
       CASE "O"
    
          RETURN uVal:ClassName()
    
       CASE "H"
    
          RETURN "{=>}"
    
       CASE "P"
    
    #ifdef __XHARBOUR__
          RETURN "0x" + NumToHex( uVal )
    #else
          RETURN "0x" + hb_NumToHex( uVal )
    #endif
    
//       DEFAULT    
//          RETURN ""
    
       ENDSWITCH
    
    RETURN NIL