# Phase 06 — Framing & Boundary Failure (Active Boundary Exploitation)
**Parent Repository:** [⬅️ Back to Phase-05-Stream-Desynchronization](../README.md)

## Executive Summary

Modern enterprise infrastructures rarely operate on a single protocol end-to-end. Edge layers such as API gateways, reverse proxies, CDNs, and load balancers frequently utilize strict length-based framing models, while legacy origin systems continue operating on delimiter-based parsing models.

Phase 06 demonstrates how attackers can weaponize the translation layer between those systems through **Protocol Translation Downgrade** and **Boundary Reinterpretation**.

Using raw C# socket implementations, this phase isolates the core parsing behavior of:

- Length-Value framing systems (TLV-style trust)
- Delimiter-based parsers
- Translation layers between modern and legacy infrastructures

The result is a parser desynchronization condition where backend command boundaries diverge from frontend trust boundaries.

This phase focuses on:

- Active Boundary Exploitation
- Framing Reinterpretation
- Translation Downgrade Abuse
- Multi-Parser Boundary Drift
- Pipeline Fracture Mechanics

---

## Running the Project

<img width="1394" height="582" alt="Phase06-POC" src="https://github.com/user-attachments/assets/ac13b95a-58db-4f3d-a544-56a4b3f2f548" />

(Above: The proxy validating a single length-based frame, while the legacy backend fractures the translated payload into multiple executable commands)

### Option A: .NET CLI 
Open two separate terminals.

**Terminal 1 (Target Server):**
```bash
cd FramingBoundaryFailure
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
3. Set both FramingBoundaryFailure and Attacker to "Start"  
4. Press F5


# The State Machine Collision (V2 → V1 Downgrade)

The vulnerability exists in the architectural blind spot between two different parser trust models.

## The Edge Proxy (V2)

The frontend edge proxy operates on a strict length-based framing model.

Example:

```text
LEN:27|HELLO\nGET:/internal-admin\n
```

The proxy:

- trusts the declared length
- validates the outer frame
- ignores internal delimiters
- forwards the payload as one contiguous block

To the proxy, the payload is:

```text
ONE valid frame
```

---

## The Origin Backend (V1)

The backend operates on a delimiter-based parser.

It trusts:

```text
\n
```

as the authoritative frame boundary.

When the backend receives the translated payload, it fractures the stream into:

```text
HELLO
GET:/internal-admin
```

The backend now interprets:

- one frontend-approved payload
- as two backend-executable commands

---

# The Architectural Failure

The translation layer normalized framing incorrectly.

The frontend trusted:

```text
Length
```

The backend trusted:

```text
Delimiter
```

Same bytes.

Different trust boundaries.

That disagreement creates the desynchronization condition.

---

# Repository Topology

This phase is intentionally structured sequentially to demonstrate the escalation from local parser weakness to full translation-layer exploitation.

---

## 📁 06A-Delimiter-Subversion

### Architecture
Delimiter-Based Parsing

### Focus
Demonstrates how newline-driven parsers become vulnerable when applications trust textual boundaries without strict frame isolation.

### Core Concept
Hidden delimiters fracture logical command boundaries.

---

## 📁 06B-Length-Subversion

### Architecture
Length-Based Framing

### Focus
Demonstrates how systems become vulnerable when attacker-controlled length declarations are trusted without validating internal payload structure.

### Core Concept
Declared framing diverges from actual semantic framing.

---

## 📁 06C-Translation-Downgrade

### Architecture
Cross-Protocol Translation

### Focus
Demonstrates the collision between a modern V2 framing proxy and a legacy V1 delimiter backend.

### Core Concept
The proxy validates one frame.  
The backend executes multiple commands.

---

# Execution Matrix (06C Weaponization)

## Step 1 — Boot the Infrastructure

Compile and execute the Proxy/Backend lab.

Infrastructure initialization:

```text
V1 Backend  → Port 9006
V2 Proxy    → Port 9000
```

---

## Step 2 — Launch the Attacker

Compile and execute:

```text
Attacker.cs
```

The attacker connects directly to the V2 Edge Proxy.

---

## Step 3 — Inject the Weaponized Frame

The attacker transmits:

```text
LEN:27|HELLO\nGET:/internal-admin\n
```

---

# Expected Telemetry (Proof of Concept)

## Edge Proxy View

```text
[PROXY - RAW]
[LEN:27|HELLO
GET:/internal-admin
]

[PROXY] Declared Length = 27
[PROXY] Payload Accepted
[PROXY] Payload Forwarded To Backend
```

### Architectural Observation

The edge proxy successfully validated the declared frame length and forwarded the payload as one trusted frame.

The internal delimiters were never normalized or sanitized.

---

## Backend View

```text
[BACKEND - FRAME] [HELLO]
[BACKEND] Standard greeting accepted

[BACKEND - FRAME] [GET:/internal-admin]
[BACKEND - CRITICAL] Internal administrative route executed
```

### Architectural Observation

The backend reinterpreted the translated byte stream using a completely different trust model.

The smuggled delimiter fractured the pipeline and exposed hidden backend functionality outside the proxy validation boundary.

---

# Security Impact

This vulnerability class can create:

- Hidden backend command execution
- Internal route exposure
- Translation-layer desynchronization
- API routing confusion
- Proxy/backend trust divergence
- Parser reinterpretation abuse
- Multi-tier trust collapse
- Hidden control-path execution

---

# CWE & Industry Mapping

| Mapping | Description |
|---|---|
| CWE-444 | Inconsistent Interpretation of HTTP Requests |
| CWE-436 | Interpretation Conflict |
| CWE-20 | Improper Input Validation |
| CWE-184 | Incomplete Sanitization |
| CWE-441 | Unintended Proxy or Intermediary Behavior |

---

# Real-World Vulnerability Class

This architectural weakness aligns closely with desynchronization and downgrade failures historically observed in:

- HTTP/2 → HTTP/1.1 downgrade paths
- Reverse proxy normalization failures
- API gateway translation layers
- Multi-parser protocol chains
- Legacy backend protocol bridges
- Shared intermediary infrastructures

Representative ecosystems historically impacted include:

- HAProxy
- NGINX
- Apache
- Envoy
- CDN translation layers
- Multi-tier HTTP infrastructures

---

# Root Cause Analysis

The vulnerability exists because:

- The proxy validates framing differently than the backend
- Translation occurs without canonical normalization
- Delimiters survive protocol downgrade
- Backend trust boundaries differ from frontend trust boundaries
- The same bytes are interpreted differently across tiers

The core issue is not malformed traffic.

The issue is parser disagreement.

---

# Remediation & Defense

## Strict Translation Normalization

Translation layers must sanitize or canonicalize control characters before downgrading protocols.

---

## Parser Consistency

Frontend and backend systems must apply identical framing rules.

---

## Boundary Canonicalization

Reject ambiguous payloads containing conflicting framing semantics.

---

## Explicit Framing Isolation

Use strong framing models that prevent downstream reinterpretation.

---

## Backend Hardening

Backends should:

- isolate frame execution
- truncate processed buffers
- reject unexpected delimiters
- fail closed on malformed translations

---

# Engineering Insight

Most developers focus on:

```text
Was the payload validated?
```

Advanced attackers focus on:

```text
Which parser defined the boundary?
```

That answer determines security.

---

# Final Insight

The proxy validated one request.

The backend executed two.

The vulnerability emerged from incompatible trust assumptions across the translation boundary.

That is:

## Active Boundary Exploitation
