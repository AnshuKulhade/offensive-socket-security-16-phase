# 05C — HTTP Request Smuggling (CL.TE Desynchronization)

**Parent Repository:** [⬅️ Back to Phase-05-Stream-Desynchronization](../README.md)

## Executive Summary

This lab demonstrates a classic parser desynchronization flaw where the frontend proxy trusts `Content-Length`, while the backend server prioritizes `Transfer-Encoding: chunked`.

Both systems inspect the same bytes, but define different request boundaries.

The result is a hidden second request reaching a protected backend route.

---

## Lab Architecture

```text
Client → Frontend Proxy (CL) → Backend Service (TE)
```

---

This folder contains one C# lab consisting of three components:

* **Frontend Proxy:** Listens on `127.0.0.1:9000`. Reads headers, parses `Content-Length`, slices the body strictly by byte count, and then forwards the request.
* **Backend Service:** Listens on `127.0.0.1:9001`. Prioritizes `Transfer-Encoding: chunked` and treats `0\r\n\r\n` as the absolute end of the request.
* **Attacker Client:** Sends a crafted, dual-header payload containing a hidden second request.

---

## The Vulnerability Flow

**The Attacker Payload:**
```http
POST / HTTP/1.1
Host: localhost
Content-Length: <full body size>
Transfer-Encoding: chunked

0

POST /admin HTTP/1.1
Host: localhost
Action: wipe_db
```

---

### Frontend View
* **One valid request**
* Body length satisfied by `Content-Length`
* Forward request downstream

### Backend View
* Chunk terminator reached at: `0\r\n\r\n`
* Request #1 ends here
* Remaining bytes become Request #2
* `POST /admin` executes

### Result
```text
[Backend] 200 OK - CRITICAL: Smuggled Admin Endpoint Executed
```
### Root Cause
* Frontend trusts Content-Length
* Backend trusts Transfer-Encoding
* Same bytes
* Different boundaries
---

## Security Impact

* Hidden request execution
* Internal route access
* Intermediary validation bypass
* Authentication bypass chains
* Cache poisoning precursor
* Response desynchronization precursor

---

## CWE Mapping

* **CWE-444** Inconsistent Interpretation of HTTP Requests
* **CWE-436** Interpretation Conflict
* **CWE-20** Improper Input Validation
* **CWE-284** Improper Access Control

---

## Real-World Research & CVE Class

This vulnerability class has severely impacted:
* Reverse proxies
* Load balancers
* CDNs
* API gateways
* Multi-tier HTTP/1.1 infrastructures

--- 

**Publicly Known Research:**
* PortSwigger HTTP Request Smuggling research (James Kettle / albinowax)
* CL.TE / TE.CL desync classes across major vendors

---

**Representative CVE Families:**
Multiple request smuggling issues across HAProxy, Apache, NGINX, F5, cloud edge platforms, and gateway products over time. *(Exact CVEs vary by parser implementation.)*

----

## Running the Project

### Option A — Visual Studio
> Open solution

> Build project

> Run application

> Backend listener starts on 9001

> Frontend proxy starts on 9000

> Attacker auto-executes payload

## Option B — CLI
```
dotnet run
```
---

## Proof of Concept
<img width="1235" height="662" alt="05C-POC" src="https://github.com/user-attachments/assets/be01e2cb-3719-4d38-a8d8-6fb9bc89c6de" />

---

## Detection Signals

* Requests containing both `Content-Length` and `Transfer-Encoding`
* Backend actions without matching gateway logs
* Unexpected internal route access
* Desync anomalies on Keep-Alive channels
* Cache or routing inconsistencies

## Defensive Fix

* **Parser Consistency:** Use identical parsing logic across frontend and backend. Normalize all requests before forwarding.
* **Header Validation:** Reject ambiguous requests containing both `Content-Length` and `Transfer-Encoding`. Remove unsupported transfer codings.
* **Boundary Enforcement:** Enforce one canonical request boundary. Immediately close connections on any framing ambiguity.
* **Platform Hardening:** Upgrade HTTP parsers regularly. Proactively test intermediary proxy chains for desynchronization behavior.

---

## Final Insight

> *Same bytes.*
> *Different parsers.*
> *Different trust decisions.*

**That is HTTP Request Smuggling.**
