
# Phase 01 — Connection vs Request (State Contamination)

## Overview

**Category:** Authentication Boundary Failure (State Contamination)

This phase demonstrates a boundary failure in socket-based systems...

> A connection is treated as an identity boundary.

This assumption leads to **state contamination**, where authentication persists across unintended scopes.



## Running the Project

Set multiple startup projects in Visual Studio:

1. Right click solution → Set Startup Projects  
2. Choose "Multiple startup projects"  
3. Set both ServerListener and ClientSender to "Start"  
4. Press F5  

This runs both the vulnerable server and exploit client simultaneously.
   
---

## Core Insight

* Authentication must be **request-scoped**, not connection-scoped
* A socket is a **transport pipe**, not an identity boundary
* Connection persistence ≠ identity persistence

---

## Vulnerable Behavior

### Flow

```
CONNECT → AUTH:secret → isAuthenticated = true
↓
GET_DATA → SECRET_DATA ✔
↓
Connection closes → state cleared
```

This appears correct — until identity changes mid-connection.

---

## Exploit Flow — Identity Switch

```
CONNECT → AUTH:secret → isAuthenticated = true (admin)
↓
BECOME:guest → identity changes (auth NOT reset)
↓
GET_DATA → SECRET_DATA ❌ (unauthorized access)
```

### Client Input

```
AUTH:secret
BECOME:guest
GET_DATA
```

---

## Root Cause

* Authentication stored at **connection scope**
* No re-validation per request
* Identity change does not reset auth state

> The system trusts connection state as identity — which is incorrect.

---

## Vulnerability Class

* Connection-scoped authentication
* State persistence without revalidation
* Improper authentication boundary
* Privilege carry-over

---

## Real-World Impact

This pattern exists in real systems:

* WebSockets → long-lived auth reuse
* API connection pooling → identity bleed
* gRPC / TCP services → cross-request contamination

### Impact

* Unauthorized data access
* Privilege escalation
* Cross-user data leakage

---

## Error Surface (Attack Signals)

### Connection Refused

* Happens before Accept()
* Indicates backlog exhaustion
* Usable for DoS

### Connection Closed

* Happens during processing
* May cause partial execution
* Useful for state manipulation

---

## Cross-Layer Insight

### Threading

* Per-client state (correct): Task-scoped
* Shared/global state → global contamination risk

### Protocol Design

* HTTP/2: multiple streams over single TCP connection
* Connection-scoped auth → cross-stream privilege bleed

---

## Defensive Controls

* Validate authentication per request
* Do not bind auth to socket lifecycle
* Use tokens with expiry
* Enforce re-authentication on identity change

---

## Detection Signals

* Identity change without re-authentication
* Privileged actions without recent auth
* Auth count < action count per connection

---

## Key Takeaway

> The system did not fail due to missing authentication —
> it failed because authentication was applied at the wrong boundary.


```
AUTH:secret
BECOME:guest
GET_DATA
```

---

## Notes

This implementation is intentionally vulnerable for research and learning purposes.
Not intended for production use.
