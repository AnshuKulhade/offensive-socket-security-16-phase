# 05B — Backend Response Queue Poisoning
**Parent Repository:** [⬅️ Back to Phase-05-Stream-Desynchronization](../README.md)

## Lab Architecture

This lab demonstrates how an attacker can strand a malicious response inside a shared Keep-Alive connection pool, causing the next legitimate user to receive the attacker's data.

This folder contains two C# files:
* **Server.cs:** Listens on port 9090. It simulates a vulnerable backend that processes multiple pipelined commands from a single TCP read and emits multiple HTTP responses without closing the connection.
* **Attacker.cs:** Simulates both an Attacker and a legitimate Victim routing through an API Gateway (Proxy). The proxy strictly reads only the bytes defined by the first response's `Content-Length`, leaving the second response poisoned in the queue.


## Running the Project

### Option A: .NET CLI 

Open two separate terminals.

**Terminal 1 (Target Server):**

```bash

cd ResponseQueuePoisoning

dotnet run

```

**Terminal 2 (Attacker Client):**

```bash

cd Attacker

dotnet run

```

## Using .NET Framework GUI

Set multiple startup projects in Visual Studio:

1. Right click solution → Set Startup Projects  

2. Choose "Multiple startup projects"  

3. Set both ServerListener and ClientSender to "Start"  

4. Press F5  


---

## The Vulnerability Flow

**The Execution Logic:**
1. The Attacker sends a smuggled request asking for an `ADMIN_SECRET`.
2. The Backend processes the request and writes TWO responses into the TCP pipe.
3. The Gateway reads the exact `Content-Length` of the first response and forwards it to the Attacker.
4. The `ADMIN_SECRET` response is left stranded in the OS network buffer.
5. A Victim sends a legitimate request (e.g., `GET /profile`) down the same Keep-Alive connection.
6. The Gateway pulls the stranded `ADMIN_SECRET` from the buffer and sends it to the Victim.

**Root Cause in Code:** The Gateway and the Backend lose 1:1 synchronization of requests to responses over a persistent shared socket.

---

## Execution Steps

1. Open a terminal and compile/run `Server.cs` to start the backend on `127.0.0.1:9090`.
2. Open a second terminal and compile/run `Attacker.cs`.
3. Watch the proxy output. You will see the Attacker receive standard user data, while the Victim accidentally receives the `ADMIN_SECRET`.

---
## Security Impact

> Cross-user data leakage
> Session contamination
> Unauthorized data disclosure
> Response confusion
> Multi-tenant boundary failure

---

## CWE Mapping

> CWE-444 Inconsistent Interpretation of HTTP Requests
> CWE-441 Unintended Proxy or Intermediary Behavior
> CWE-200 Exposure of Sensitive Information to an Unauthorized Actor
> CWE-284 Improper Access Control

---


## Proof of Concept
<img width="1325" height="739" alt="05B-POC" src="https://github.com/user-attachments/assets/7cdcc4c9-979f-40fa-a43f-801cea50fbd9" />
