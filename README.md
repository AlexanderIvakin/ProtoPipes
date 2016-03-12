# ProtoPipes
Fooling around with Windows named pipes

A simple console application that runs in two modes:
* Server-mode launches a named pipe server that pushes random integers with a predefined interval
* Client-mode launches a named pipe client that reads the messages from the named pipe

Couple of interesting features:
* Server supports multiple clients
* Server supports reconnecting clients
* Client can selectively connect to the specified server via PID
