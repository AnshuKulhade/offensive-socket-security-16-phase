# Phase 02 — Lifecycle as Control Flow (FIN vs RST)

---

## Overview

**Category:** Transaction Integrity Failure  
**Pattern:** Transport-State Coupling  

This phase demonstrates a critical failure where **application correctness is incorrectly tied to network lifecycle behavior**.

> A connection is treated as a reliability boundary.

This assumption leads to **state inconsistency**, where execution succeeds but confirmation fails — creating a **blind success condition**.

---

## Running the Project

Set multiple startup projects in Visual Studio:

1. Right click solution → Set Startup Projects  
2. Choose **Multiple startup projects**  
3. Set both **Sender** and **Server_Fin_Rst** to **Start**  
4. Press **F5**

This runs both the vulnerable server and exploit client simultaneously.

--- 

## Note

Unlike Phase 01, this phase does not require manual user input.

Both the server and client run automatically once started.  
To stop execution, use the debugger (Stop / Shift + F5).

---

## Core Insight

- Only a few socket options (e.g., Linger) directly impact lifecycle behavior → exploit surface  
- Execution success must be independent of response delivery  
- A socket is a transport channel, not a correctness guarantee  
- Connection lifecycle ≠ transaction lifecycle  
- Network failure does not imply operation failure  

---

## Vulnerable Behavior

### Flow


```
CONNECT → TRANSFER → Receive → COMMIT
↓
Connection closes (RST / early termination)
↓
ACK fails → Exception
```

Execution completes even when confirmation fails.

---

## Protocol Behavior

### FIN — Graceful Close


Client → Receive → COMMIT → ACK → FIN


- ✔ Execution completes  
- ✔ Response delivered  
- ✔ System consistent  

---

### RST — Abrupt Termination


Client → Receive → COMMIT → RST → ACK fails


- ❌ Execution completes  
- ❌ Response fails  
- ❌ Connection terminates unexpectedly  

---

## Core Vulnerability

The server performs:


Receive → COMMIT


Without ensuring:

- response delivery  
- lifecycle completion  

The system assumes:

> processing success = delivery success  

👉 This creates a **blind success condition**

---

## Exploit Flow

1. Send TRANSFER request  
2. Server Receive → COMMIT executes  
3. Client terminates connection (RST / early close)  
4. ACK fails, but state is already modified  

---

## Protocol Coding — Exploit Implementation

This demonstrates how a client can **break the response lifecycle while still triggering server-side execution**.

```csharp
var request = (HttpWebRequest)WebRequest.Create(url);
request.Method = "POST";

using (var stream = request.GetRequestStream())
{
    byte[] data = Encoding.UTF8.GetBytes("TRANSFER:amount=1000:to=attacker");
    stream.Write(data, 0, data.Length);

    // Send payload
    stream.Write(data, 0, data.Length);
    stream.Flush();

    // Immediately force RST — do not wait for server ACK
    // This is what creates the torn window
    request.Abort(); // triggers RST, not FIN
}
```
Payload reaches server → Receive completes → COMMIT executes
Connection closes before ACK → lifecycle breaks, execution remains

---

## Real-World Impact

This pattern exists in:

- Payment systems
- Distributed systems
- Queue processors
- APIs under unstable network conditions


## Impact
- Duplicate execution on retry
- Inconsistent client/server state
- Silent data corruption
- Financial integrity risks

---


## Error Surface (Attack Signals)
### Connection forcibly closed (RST during response write)
Receive ✔ → COMMIT ✔ → Send(ACK) ❌ → Exception
- Client terminates connection abruptly
- Server already committed state
- Failure occurs during response transmission

---

### Unable to write to transport (ACK failure after commit)
Receive ✔ → COMMIT ✔ → Write(ACK) ❌ → Socket closed
- Server writes to a closed socket
- State mutation succeeds, acknowledgment fails

### Request aborted (lifecycle terminated mid-operation)
Receive ? → COMMIT ? → Connection drop → Abort
- Execution outcome becomes uncertain

> ⚠️ Critical Risk — Race Condition Window

- COMMIT and RST may occur at nearly the same time
- Execution becomes non-deterministic
- State may be partially or fully applied

> Equivalent to torn write behavior in distributed systems

--- 

## Cross-Layer Insight

### Transport Layer
- TCP ensures delivery attempt, not correctness
- RST invalidates connection immediately

### Application Layer
- Commit must not depend on socket state
- Response delivery is not a correctness guarantee

### Observability
- Errors appear as network failures
- Successful state changes remain silent

> This creates logging blindness

--- 

## Race Condition Window
- Receive → COMMIT ? → RST
- Non-deterministic execution outcome
- Equivalent to torn write behavior

---

## Fix Strategy

The issue occurs because state mutation depends on connection lifecycle instead of controlled application logic.

## Correct Approach — Idempotent Atomic Commit
- Commit exactly once
- Ignore duplicate requests
- Do not rely on ACK or connection state

---

## Fixed Implementation
// Shared state
```
static object _sync = new object();
static HashSet<string> _txnCache = new HashSet<string>();
static decimal _accountBalance = 1000.00m;

static void Handle(Socket client)
{
    var buffer = new byte[1024];

    try
    {
        int bytesRead;
        try { bytesRead = client.Receive(buffer); }
        catch { return; }

        if (bytesRead <= 0) return;

        string payload = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

        if (!payload.StartsWith("TRANSFER:")) return;

        var parts = payload.Split(':');
        if (parts.Length < 4) return;

        string txnId = parts[1].Split('=')[1];
        decimal amount = decimal.Parse(parts[2].Split('=')[1]);

        lock (_sync)
        {
            if (_txnCache.Contains(txnId))
                return;

            _accountBalance += amount;
            _txnCache.Add(txnId);
        }

        try { client.Send(Encoding.UTF8.GetBytes("ACK")); }
        catch { }
    }
    finally
    {
        try { client.Close(); } catch { }
    }
}
```

## Fix Summary
- Removed transport-dependent commit logic — state no longer relies on ACK or connection lifecycle
- Added idempotency via transaction IDs — prevents duplicate execution and replay attacks
- Ensured atomic state updates using locking — eliminates race conditions under concurrency

----

## Detection Signals

- Execution count ≠ successful responses
- Repeated operations with failed acknowledgments
- Network errors correlated with state changes
- Abnormal RST timing during write operations

---

## Key Takeaway

> The system did not fail due to missing validation —
> it failed because correctness was applied at the wrong lifecycle.
```
TRANSFER → COMMIT → CONNECTION FAIL
```

> Transactions must be committed exactly once, independent of network reliability.

--- 

## Notes

This implementation is intentionally vulnerable for research and learning purposes.
Not intended for production use.


