# TandyPrinter
This is the very beginnings of a printer application for the trs80gp Tandy emulator.

At this point, it is ONLY a listener; and specifically listens on localhost using port 1234.

Because these printers were largely line based, rather than spooler based like mainframes
and mini's each page is in it's own PDF file.  This may change in the future; but this seems
to be the best compromise at the moment.

To run this from the command line; use the following format

```
TandyPrinter ip_address_to_use listen_port
Eg: TandyPrinter 192.168.1.30 1234
```

# Building from source
**In Windows and Linux both you must have the dotnet 9 SDK installed.  Instructions for this can be found online.**

It is assumed you know how to use the `git clone` and `git pull` commands.  If not, read up on them.

**Building with Windows and linux (including the raspberry Pi)**

clone the repo into a directory, then change into that directory.

```dotnet publish --self-contained -c Release -o Publish```

**For Linux**

```dotnet publish --self-contained --runtime <your runtime> -c Release -o Publish```

for your runtime, you can use `linux-x64`, `linux-arm64` (raspberry pi)


This will build the code and place it inside the Publish subdirectory


Contributions are welcome to this project.  Everyone is free to participate without
regard to who they are or what their beliefs are.  If you understand VB.NET; you're 
welcome.
