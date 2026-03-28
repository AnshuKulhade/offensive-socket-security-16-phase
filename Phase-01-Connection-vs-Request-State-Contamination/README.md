
# Phase 01 — Connection vs Request (State Contamination)

## Overview

This phase demonstrates a critical design flaw in socket-based systems:

> A connection is treated as an identity boundary.

This assumption leads to **state contamination**, where authentication persists across unintended scopes.

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

---

## How to Run

1. Open the solution file (.sln)
2. Run the Server project
3. Run the Client project

Then execute:

```
AUTH:secret
BECOME:guest
GET_DATA
```

---

## Notes

This implementation is intentionally vulnerable for research and learning purposes.
Not intended for production use.
