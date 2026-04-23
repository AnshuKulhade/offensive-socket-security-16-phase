# Phase 05 — Stream Desynchronization
## Architectural Insights & Remediation

## Executive Summary

TCP is commonly misunderstood as a message-based protocol. It is not. TCP provides only an ordered and reliable byte stream. Message boundaries, request ownership, parser state, and authentication scope are assumptions implemented by applications.

When systems disagree on those assumptions, desynchronization vulnerabilities emerge.

This phase studies three practical exploit classes:

1. Parser State Desynchronization  
2. Backend Response Queue Poisoning  
3. HTTP Request Smuggling (CL.TE)

---

## 🏛️ Core Principle

> TCP gives bytes.  
> Protocols define boundaries.  
> Parsers assign meaning.  
> Attackers exploit disagreement.

---

## Why Stream Desynchronization Exists

### Developer Assumptions
- One `Receive()` call equals one complete request.
- One request always maps to one response.
- Every network tier interprets boundaries identically.
- Authentication state naturally follows message boundaries.

### Reality
- `Receive()` returns arbitrary chunks based on buffering and timing.
- A single chunk may contain partial data or multiple merged messages.
- Different systems may use different framing rules.
- Responses may outlive the request that created them.
- Parser state often leaks across unintended boundaries.

### The Vulnerability Surface
The gap between developer assumptions and transport reality creates exploitable trust failures.

---

##  Attack Anatomy

### 1. Parser State Desynchronization

**Definition:**
A parser detects message separators correctly but shares internal state across multiple logical messages in the same stream window.

**Example:**
```text
AUTH:USER
CMD:EXPORT_USERS
```

The first message establishes authorization state.  
The second message incorrectly inherits it.

### Root Cause

- State scoped to the receive buffer  
- State not reset per message  
- Parsing performed before transaction isolation  

### Impact

- Privilege escalation  
- Logic bypass  
- Unauthorized command execution  
- State contamination  

### Defensive Rule

Reset parser and authentication state per logical message, never per raw socket read.

---

## 2. Backend Response Queue Poisoning

**Definition:**
A crafted request causes more backend responses than the intermediary expects, leaving residual responses queued on a shared keep-alive connection.

### Example Flow

Attacker sends 1 request  
Backend emits 2 responses  
Proxy consumes first response  
Second response remains queued  
Victim receives poisoned response later  

### Root Cause

- Broken request-response ownership mapping  
- Shared pooled connections  
- Incomplete response draining  

### Impact

- Cross-user data leakage  
- Session contamination  
- Response confusion  
- Sensitive data exposure  

### Defensive Rule

Enforce strict one-request-to-one-response mapping. Drain or close poisoned connections immediately.

---

## 3. HTTP Request Smuggling (CL.TE)

**Definition:**
The frontend gateway and backend service interpret HTTP request boundaries differently.

### Canonical Example

Frontend trusts Content-Length  
Backend trusts Transfer-Encoding  

Same bytes. Different boundaries.  
Hidden bytes become a second request.

Client → Frontend Proxy (CL) → Backend Service (TE)
                     

### Root Cause

- Parser disagreement across tiers  
- Ambiguous HTTP framing  
- Inconsistent RFC handling  

### Impact

- Hidden request execution  
- Internal route access  
- Validation bypass  
- Cache poisoning  
- Queue poisoning precursor  

### Defensive Rule

Normalize requests at the edge. Reject ambiguous `Content-Length` + `Transfer-Encoding` combinations. Use identical parsing logic across tiers.

---

## ⚖️ Comparison Matrix

| Attack Type | Core Failure | Primary Victim | Typical Outcome |
|---|---|---|---|
| Parser State Desync | State scope mismatch | Same requester | Privilege escalation |
| Response Queue Poisoning | Response ownership mismatch | Other users | Cross-user leakage |
| Request Smuggling | Boundary disagreement | Frontend/backend chain | Hidden backend execution |

---

##  Real Systems at Risk

These patterns are relevant to:

- Reverse proxies  
- Load balancers  
- API gateways  
- Microservice meshes  
- Custom TCP services  
- Legacy HTTP/1.1 chains  
- Shared backend connection pools  
- Mixed-vendor infrastructures  

---

## Detection Signals

### Network Indicators

- Unexpected fragmented reads  
- Multiple commands in one logical flow  
- Response count ≠ request count  
- Keep-alive reuse anomalies  
- Unexplained retries or resets  

### Application Indicators

- Users receiving another user’s data  
- Unexpected privileged actions  
- Missing gateway logs for backend activity  
- Intermittent authorization anomalies  
- Non-deterministic parser behavior  

---

## Why Raw Sockets Matter

Modern frameworks often abstract away:

- Buffering behavior  
- Connection reuse  
- Header normalization  
- Framing reconstruction  
- Parser edge cases  

Raw `System.Net.Sockets` exposes transport behavior directly, making desynchronization mechanics observable without framework interference.

---

## 🛡️ Modern Defenses

### Architectural Controls

**Protocol Normalization**  
Terminate and normalize traffic at the edge.

**Parser Parity**  
Ensure frontend and backend apply identical framing logic.

**Connection Isolation**  
Separate sensitive workloads from shared pooled channels.

**Safe Upgrades**  
Use protocols with explicit framing where possible.

### Code-Level Controls

- Accumulate complete frames before parsing  
- Validate completeness before execution  
- Scope variables strictly per transaction  
- Reset state per logical message  
- Reject ambiguity by default  
- Fail closed on malformed framing  

---

## Engineering Mindset Shift

Do not ask:

Did the request arrive?

Ask:

- Who decided where the request ended?  
- Which layer owns the response?  
- Which parser defined the boundary?  

Those answers determine security.

---

## 🏁 Final Insight

Parser too early   → Partial trust  
Parser too broadly → State bleed  
Parser disagrees   → Smuggling  
Response misowned  → Queue poisoning  

Together:

# Stream Desynchronization

Control protocol boundaries, or attackers will.
