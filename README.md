# Playstation 3 File Dumper
 Dumps Files Via webMAN MODs HTTP Server or FTP

![image](https://github.com/user-attachments/assets/84dca907-fe02-4af3-8abf-6594c4da6968)


Small application to help with dumping Playstation Home Cache from PS3 consoles. Can be used to dump other folders too. 

Usage:
- Enter PS3 IP
- Enter folder you want to Dump
- Click Check Connection to confirm IP is correct
- By default it will zip up whatever is dumped if it succeeds to dump ALL files without any error
- To skip the large MOSTLY empty RESERVED files in PS Home Cache toggle that setting on
- If your console has no HTTP server you can use FTP instead but really FTP client would be better,

Notes: 
- IF a file fails to dump it will retry that file 10 times (HTTP and FTP)
- HTTP is setup for 4 concurrent downloads - FTP just 1 concurrent download.
- Files dump into DUMPED folder next to the EXE, you will zipped dumps in there too after completion.


Work in progress - needs more testing

