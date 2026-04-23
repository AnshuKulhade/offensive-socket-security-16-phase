# 05A — Parser State Desynchronization
**Parent Repository:** [⬅️ Back to Phase-05-Stream-Desynchronization](../README.md)

## Lab Architecture

This lab demonstrates how a parser can correctly split multiple commands, but fail because its authorization state is scoped to the TCP receive buffer instead of each logical message. 

This folder contains two C# projects:
* **Parser_State_Dsync (The Server):** Listens on port 9004. It correctly splits incoming streams by `\n`, but incorrectly declares the `isAuthorized` boolean *outside* of the message parsing loop.
* **Attacker (The Client):** Connects to the server and injects a malicious payload containing an authenticated command followed immediately by a restricted administrative command.

---

## ⚙️ The Vulnerability Flow

**The Attacker Payload:**
```text
AUTH:GUEST\nCMD:EXPORT_USERS\n
The Server Execution Logic:

[PARSE] AUTH:GUEST

[AUTH] Guest access granted. (Sets isAuthorized = true)

[PARSE] CMD:EXPORT_USERS

[EXEC] Unauthorized user export executed! (Inherits the isAuthorized state from the previous loop iteration).
```

**Root Cause in Code:** The message boundaries reset inside the loop, but the authorization state does not.

---

## Security Impact
> Privilege escalation

> Authorization bypass

> Hidden command execution

> orkflow abuse


## CWE Mapping
> CWE-863 Incorrect Authorization

> CWE-284 Improper Access Control

> CWE-20 Improper Input Validation

---

## Execution Steps

1. Open `Parser_State_Desynchronization_Sol.sln` in Visual Studio.
2. Build the solution.
3. Run the **Parser_State_Dsync** project first to start the listener on `127.0.0.1:9004`.
4. Run the **Attacker** project to fire the weaponized payload.
5. Observe the server console outputting the successful execution of the smuggled command.

---

## Defensive Fix

### State Isolation

- Scope authorization state per logical message  
- Reset temporary flags after each command  

### Boundary Integrity

- Parse one complete command at a time  
- Use explicit framing (length-prefix or strict delimiter model)  

### Atomic Authorization

- Perform authorization check and command execution atomically in the same transaction scope  
- Never allow previous message state to satisfy current command validation

--- 

## 📸 Proof of Concept

<img width="1335" height="529" alt="05A-POC" src="https://github.com/user-attachments/assets/1f05e743-2129-4b71-a6b3-b6d2310d1fdd" />
