# TQServer - A Terribly Questionable Server Project

Welcome to **TQServer**, an ill-advised, poorly implemented, and completely insecure shell server. It is a product of boredom and hatred; this code is not even close production-ready, and to be frank, has very few real use cases.

---

## ❓ **What is TQServer?**

TQServer is a "containerized shell" server that allows clients to connect, execute commands, and interact with a file system. It supports authentication, user roles, and various commands, but in a pretty roundabout, insecure, and generally terrible way.

By default, when it's started, it'll drop you into an interactive shell with the internal server, but the server can accept connections from other clients, too, and it fully supports most telnet clients, or even just netcat.

It’s perfect if you:
1. Want to learn about server security by exploiting its glaring vulnerabilities.
2. Need a service for a CTF challenge.
3. Enjoy reading horrific code for entertainment.

Tested on both Windows and Linux, equally shit on both of them!

---

## ⚠️ **Warnings**

- **DO NOT USE THIS IN PRODUCTION.**  
  This server is riddled with vulnerabilities and has horribly written spaghetti code.
- **ANYTHING RUNNING THIS SERVER SHOULD BE ISOLATED FROM THE INTERNET.**  
  The containerization is really ease to bypass, and this might as well just be a backdoor into your machine.
- **CONSIDER USING THIS FOR EDUCATIONAL PURPOSES ONLY.**  
  If you're setting up a CTF challenge and want a non-standard service for people to play with, this _might_ be fit.

---

## 🛠 **"Features"** 

- **TCP-Based Server**: Handles client connections and allows command execution.
- **Authentication**: Implemented using *plaintext passwords* stored in the configuration file.
- **File System Traversal**: Clients can browse the server's file system with a defined root directory.
- **Basic Command Support**: `clear`, `ls`, `cat`, and other common shell commands are built-in instead of relying on the OS.
- **Configurable Permissions**: You can edit CoreHandler.cs to change which commands require which permissions. 
- **User Scope System**: You can grant permissions like `Traverse`, `Read`, or even `Exec` to *Scopes*, and add those Scopes to *Users.*

---

## **Known Vulnerabilities**

- **Plaintext Passwords**: All user credentials are stored in plaintext in the config file.
- **Input Validation? What’s That?**: User input isn’t validated, making it trivial to inject malicious commands.
- **Directory Traversal**: The server tries to restrict file system access but this can probably be bypassed, using `exec` if nothing else.
- **Denial of Service**: No protections against malformed or excessive data.
---

## 🚀 **How to Run It**

### Prerequisites
- .NET Framework or .NET Core compatible runtime.

### Steps
1. Clone the repository.
2. Modify `acki.conf` to configure users, permissions, and server behavior.
3. Build and run the server:
   ```bash
   dotnet run --project TQServer
   ```
4. Connect a client:
   ```bash
   telnet localhost 11037
   ```

---

## **Recommended Use Cases**

1. **CTFs**: Set up this server and let participants exploit it for fun and prizes.
2. **Educational Demos**: Use it to show how vulnerabilities can arise in real-world systems.
3. **Code Reviews from Hell**: Share this code with your friends for laughs and lessons.
4. **Stress Testing Your Sanity**: Try to fix it (good luck).

---

## 📝 **Configuration**

Edit `acki.conf` to customize server behavior. 
- `RestrictToLocalhost`: Set this to **Yes** to only allow connections from 127.0.0.1
- `AllowAnonymous`: Let unauthenticated users access the server with a configurable "Anonymous Scope"
- Define users and their permissions:
  ```plaintext
  <Cody>
    Scope LocalAdmin
    Password password123
  ```

---

## 📜 **License**

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

---

**If you use this for literally anything, I'll be surprised but happy, so shoot me a mail at lenkodak@gmail.com to let me know what you did with it :D**

--- 