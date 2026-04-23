# Phase 05 — Stream Desynchronization
**Parent Repository:** [⬅️ Back to Phase-05-Stream-Desynchronization](../README.md)

## The Thesis

Developers often treat TCP and HTTP as discrete message-based protocols. They are not. They are continuous byte streams. When transport boundaries (what the operating system receives) differ from application boundaries (what the parser trusts), trust assumptions collapse.

This phase contains three raw C# socket labs demonstrating how stream parsing flaws can lead to state bleed, cross-user response poisoning, and intermediary request bypasses.

---

### 🗂️ Repository Structure
```text
Phase-05-Stream-Desynchronization/
│── README.md          (You are here — Overview & Execution matrix)
│── INSIGHTS.md        (Deep architectural theory and defense/remediation)
│
│── 05A-Parser-State-Desync/
│   ├── README.md      (Execution steps & POC screenshot)
│   └── Server.cs / Attacker.cs
│
│── 05B-Response-Queue-Poisoning/
│   ├── README.md      (Execution steps & POC screenshot)
│   └── Server.cs / Attacker.cs
│
│── 05C-Request-Smuggling/
│   ├── README.md      (Execution steps & POC screenshot)
│   └── Proxy.cs / Backend.cs / Attacker.cs
```

---

##  Attack Modules

### [05A: Parser State Desynchronization](./05A-Parser-Dsync)

**The Flaw:** A backend parser processes partial or merged TCP data but incorrectly scopes internal authorization or parser state to the receive buffer rather than the logical message boundary.

- **The Exploit:** An attacker places an unauthenticated command directly behind an authenticated one within the same receive buffer.
- **The Impact:** Privilege escalation, state bleed, logic bypass.

---

### [05B: Backend Response Queue Poisoning](./05B-Queue-Poisoning)

**The Flaw:** A backend desynchronization causes the server to generate more responses than the intermediary expects.

- **The Exploit:** An attacker intentionally leaves an unauthorized response stranded inside a shared keep-alive backend connection.
- **The Impact:** Cross-user data leakage, response confusion, session contamination.

---

### [05C: HTTP Request Smuggling (CL.TE)](./05C-CL-TE-Smuggling)

**The Flaw:** The frontend gateway and backend service interpret HTTP request boundaries differently due to conflicting headers (`Content-Length` vs `Transfer-Encoding`).

- **The Exploit:** An attacker carefully crafts payload boundaries so the gateway accepts one safe request while the backend processes two.
- **The Impact:** Hidden request execution, intermediary validation bypass, internal route access.

---

##  Desynchronization Matrix

While all three attacks abuse byte-stream trust assumptions, their mechanics and targets differ significantly.

| Attack Type | Core Flaw | Primary Target | Typical Impact |
| :--- | :--- | :--- | :--- |
| **Parser State Desync** | State scoped to receive buffer instead of logical message | Backend parser logic | Escalate the attacker’s own privileges |
| **Response Queue Poisoning** | Request/response ownership breaks on shared keep-alive channels | Other legitimate users | Cross-user data exposure |
| **Request Smuggling** | Frontend and backend disagree on boundaries | Intermediary validation layer | Hidden backend request execution |

---

##  Lab Architecture & Execution

These labs are written in raw C# using `System.Net.Sockets` to demonstrate protocol behavior directly, without modern frameworks masking parsing mistakes.

### To run any module

1. Open the target module folder.
2. Run the `Server`, `Backend`, or `Gateway` listener first.
3. Run the `Attacker` client.
4. Observe terminal output to see boundary trust failures in real time.

---

## Why This Matters

These issues appear in custom TCP services, reverse proxies, API gateways, pooled backend connections, and multi-tier systems where different layers parse the same stream differently.

---

## Disclaimer

These labs are provided for educational and defensive architecture research only. Test only systems you own or are explicitly authorized to assess.
