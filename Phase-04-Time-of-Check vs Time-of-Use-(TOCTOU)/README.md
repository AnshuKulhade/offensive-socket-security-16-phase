# Phase 04 — TOCTOU: Defeating the OS File Lock

**Parent Repository:** [⬅️ Back to Main 16-Phase Series](../README.md)

Most tutorials describe Time-of-Check to Time-of-Use (TOCTOU) as a theoretical timing flaw. This lab treats it as a physical one. 

In this phase, we do not guess execution times. We weaponize C# to manipulate the Windows kernel, exhaust the physical SSD I/O, blind the RAM cache, and surgically inject a payload into a sub-second CPU hashing window.

---

## Overview

**Category:** TOCTOU (Time-of-Check to Time-of-Use) Race Condition  
**Pattern:** `Check` → `Lock Dropped (The Gap)` → `Use`

The vulnerability occurs when a system trusts a shared environment (the file system) to remain static. The server verifies a reference, drops its lock, and assumes that reference points to the same safe data milliseconds later. 

We exploit this by treating the OS lock not as a barrier, but as a high-speed sensor to track the exact microsecond the server drops its guard.

---

## The Physics of the Exploit (Why standard tutorials fail)

If you run a standard TOCTOU exploit on a modern NVMe SSD, it will fail. Modern operating systems cheat at I/O by caching recently accessed files in ultra-fast RAM (the Windows Standby List). The server reads the file in `<2 milliseconds`, making the race window impossible to hit.

To execute this exploit deterministically on a local machine, we must **force the gap** by manipulating the hardware:
1. **Cache-Busting:** We dynamically generate GUID filenames to blind the RAM cache.
2. **Hardware Exhaustion:** We pad the file with 300MB of physical junk to intentionally bog down the SSD, stretching the server's I/O read phase from 2ms to 300ms.


---

## Execution Timeline: The "Golden Window"

A standard race condition relies on luck. This framework relies on deterministic state-tracking. By profiling the server's hardware, we map the exact microsecond timeline of the vulnerability:
Based on the telemetry proof below, we can map the exact microsecond timeline of the vulnerability:

* **0ms [Check]** → Server validates file existence (T1).
* **405ms [Boundary]** → Physical SSD read (`File.ReadAllBytes`) completes. The OS file handle is released.
* **406ms [Swap]** → Attacker exception loop detects the dropped lock. The file is wiped and modified.
* **672ms [Use]** → CPU hashing finishes (267ms later). Server blindly re-reads the file (`File.ReadAllText`) and executes the poisoned content.

> **⏱️ A Note on Timing Variability:** The exact millisecond durations shown in the telemetry are not fixed. They fluctuate wildly based on your NVMe/SSD read speeds, current CPU load, and OS thread scheduling. However, the exploit is deterministic because it **does not guess time**. The attacker's polling loop simply waits for the OS to release the I/O lock, executing the swap regardless of whether the read phase takes 40ms or 4000ms.

---
## Attacker Architecture: The 4 Pillars

To reliably execute the exploit, the attacker script cannot just be a payload delivery mechanism. It acts as a custom observability framework interrogating the Windows Kernel:

1. **The Cache-Buster (GUIDs):** Generating `target_<random>.txt` forces the server to bypass the RAM Standby List and physically pull data from the SSD.
2. **The Hardware Anchor:** Writing 300MB of padding dictates the I/O read speed, widening the vulnerability window from microseconds to hundreds of milliseconds.
3. **The Polite Yield (`Task.Delay(30)`):** Yielding the thread prevents the attacker from locking the file *before* the server, which would cause a loud Denial of Service (DoS) crash instead of a silent execution bypass.
4. **The Exception Sensor (`catch(IOException)`):** We do not guess time. We weaponize the .NET `catch` block. The OS throwing an exception is our high-speed sensor confirming the server's lock is active. The exact millisecond the exceptions stop, the swap executes.

---

## Execution Timeline: The "Golden Window"

A standard race condition relies on luck. This framework relies on deterministic state-tracking. By profiling the server's hardware, we map the exact microsecond timeline of the vulnerability:

```text
0ms   [SAFE]       → TCP Trigger sent. Server executes File.Exists(path).
30ms  [THE WALL]   → Server executes File.ReadAllBytes(path) (I/O-bound, OS Lock Engaged).
300ms [BOUNDARY]   → Server finishes SSD read. OS Lock Drops.
301ms [THE SWAP]   → Attacker loop penetrates dropped lock, wiping data and injecting payload.
302ms [STALE DATA] → Server enters CPU to compute SHA256 Hash on safe data (blind to the swap).
500ms [EXPLOIT]    → Server executes File.ReadAllText(path) (Re-reads from disk, ingesting payload).
```

---


## Running the Project

### Phase-04-POC

<img width="1910" height="940" alt="Phase-04-POC" src="https://github.com/user-attachments/assets/6c0826d8-6492-48a2-ac24-88caa10b322e" />

### Option A: .NET CLI

Open two separate terminals.

**Terminal 1 (Target Server):**
```bash
cd ServerTOCTOU
dotnet run
```
**Terminal 2 (Attacker Client):**
```
cd ClientTOCTOU
dotnet run
```

## Using .NET Framework GUI
Set multiple startup projects in Visual Studio:

1. Right click solution → Set Startup Projects  
2. Choose "Multiple startup projects"  
3. Set both ServerListener and ClientSender to "Start"  
4. Press F5  

This runs both the vulnerable server and exploit client simultaneously.

---

## Core Concepts

### Check (T1)
Validation of a resource (e.g., file existence).

### Use (T2)
Later use of the same resource.

### Gap
The interval between T1 and T2 where the resource is not protected.

### Shared Mutable State
The file system is externally modifiable and cannot be assumed stable.

---

## Core Vulnerability — Reuse of Mutable Reference

The server validates a file path and later reuses the same path without ensuring that the underlying file has not changed.

This creates a race condition between validation and usage.

---

## Protocol / Execution Flow

```
TCP Trigger 
→ File.Exists(path) 
→ File.ReadAllBytes(path)   (I/O-bound, file locked)
→ SHA256 hashing            (CPU-bound, no lock)
→ File.ReadAllText(path)    (re-read, untrusted)
```

---

## Attacker Model

- Create a large file to enforce disk I/O latency  
- Send trigger to initiate server execution  
- Observe file lock behavior  
- Detect transition from I/O phase → CPU phase  
- Modify file during this transition  
- Server consumes modified data at T2  

---

## Observable Signals (Correct Metrics)

- Disk read latency (I/O phase duration)  
- CPU processing duration (hashing phase)  
- File lock availability transitions  
- Execution gap between first and second file access  

---

## Core Insight

The system assumes that a file path refers to stable data across multiple operations.

This is incorrect.

The exploit leverages inconsistency between two observations of the same resource.

---

## Important Clarification

**This is NOT:**
- A delay-based trick  
- A network-layer attack  
- A brute-force race  

**This IS:**
- A resource race condition (CWE-367)  
- A state inconsistency flaw  
- A system behavior exploitation pattern  

---

## The Architecture of the Flaw (Code Insight)

The vulnerability exists because the developer trusts a **mutable file path (a reference)** instead of a **stable memory buffer (a value)**. 

### The Vulnerable Pattern (The Double-Read)
```csharp
// T1 & T1.5: Validates and reads the file. OS Lock is temporarily engaged.
byte[] data = File.ReadAllBytes(path);

// CPU Phase: The OS Lock is dropped. The file on disk is completely exposed.
// The attacker swaps the file exactly here.
sha.ComputeHash(data);

// T2 (The Fatal Flaw): The system reaches back to the mutable hard drive.
// It blindly ingests the newly swapped payload instead of the data it just hashed.
string content = File.ReadAllText(path);
```

---

## The Universal Patch: Acquire → Check → Use

To permanently kill a TOCTOU vulnerability, you must sever the dependency on the shared environment. Once data is verified, you pull it into a private memory snapshot and never touch the hard drive again.

---



## The Secure Pattern (Private Memory Snapshot)

```
// 1. ACQUIRE: Read from the disk exactly ONCE into a private memory buffer.
byte[] data = File.ReadAllBytes(path);

// 2. CHECK: Validate the private memory snapshot, completely ignoring the file system.
using (var sha = SHA256.Create())
{
    sha.ComputeHash(data);
}

// 3. USE: Execute the exact bytes you just verified.
// The attacker can swap the file on disk a thousand times; the server no longer cares.
string content = Encoding.UTF8.GetString(data);
```

---

## Core Engineering Principle

> Never verify a reference. Verify the value.

---

## Important Clarifications & Observable Signals

### This is NOT:

-  A delay-based trick (Thread.Sleep guessing)

- A network-layer attack

- A brute-force race

### Observable Metrics:

- Disk read latency (I/O phase duration).

- CPU processing duration (Hashing phase).

- File lock availability transitions.

**The execution gap between the first and second file access.**

---

## The Invisible Breach (Conclusion)

The most dangerous aspect of a TOCTOU exploit is its silence. The system does not crash. No exceptions are thrown. Standard application logs will show a successful file validation followed by a successful execution.

The only evidence of the breach is the microsecond delta between the T1 and T2 timestamps.

This phase proves that race conditions are not theoretical timing anomalies; they are deterministic hardware vulnerabilities. By understanding the physical limits of the operating system's cache and disk I/O, an engineer can manufacture a vulnerability window and step right through it.
