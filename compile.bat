rem set PATH=%PATH%;C:\BCC58
del /Y test1.dll
C:\Harbour3\bin\hbmk2 main.hbp
rem C:\Harbour3\bin\hbmk2 -hbdynvm -gui main.prg -otest1
rem C:\MiniGUI\BATCH\hbmk2 -hbdynvm -trace main.prg -otest1 -gui
rem call C:\MiniGUI\BATCH\HBMK2 -hbdynvm -trace main.prg -otest1 -lminigui
copy /Y test1.dll Loader\Loader\bin\Debug\test1.dll