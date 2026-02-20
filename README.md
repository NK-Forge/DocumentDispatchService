# DocumentDispatchService

A distributed document dispatch simulation service built with ASP.NET Core, EF Core, and a background worker.

This project demonstrates lease-based worker coordination, configurable concurrency, retry handling, and real-time operational visibility through a Razor-based Ops dashboard.

---

## Running the Application

### Recommended: Docker (Production Ready)

Docker Compose provides a turnkey environment including PostgreSQL.

1. Clone the repository
2. Copy environment template:

   cp .env.example .env

3. Start the stack:

   docker compose up -d --build

4. Access the application:

   http://localhost:8080/ops

Stop the stack:

   docker compose down

Reset database (destructive):

   docker compose down -v

---

### Alternative: Windows Self-Contained Release

A Windows self-contained executable is available in GitHub Releases.

1. Download the latest release asset
2. Ensure PostgreSQL is available (local install or remote instance)
3. Set the connection string via environment variable:

   ConnectionStrings__DefaultConnection

4. Run:

   DocumentDispatchService.exe

---

## Overview

DocumentDispatchService simulates a document delivery pipeline where:

- Dispatch requests are created and stored in a database
- A background worker claims jobs using a lease-based locking model
- Jobs are processed concurrently
- Retries and failures are handled deterministically
- Operational state is observable in real time

The goal of this project is to demonstrate backend system design concepts commonly used in production distributed systems.

---

## Architectural Concepts Demonstrated

- Lease-based job claiming with lock ownership
- Optimistic concurrency via atomic update conditions
- Configurable worker concurrency
- Retry and requeue behavior
- Failure thresholds
- In-flight tracking
- Prometheus metrics exposure
- Real-time operational dashboard
- Config-driven runtime behavior
- Activity stream instrumentation

---

## Operations Dashboard

Accessible at:

http://localhost:8080/ops

Features:

- Live snapshot metrics
- Adjustable recent dispatch view
- Toggle to show or hide completed items
- Auto-refresh with visible countdown
- Scrollable real-time table
- Activity feed showing worker events
- Demonstration job generation controls

Designed to resemble an internal operations console.

---

## Background Worker

The DispatchWorker:

- Claims jobs in batches
- Uses lease ownership fields LockOwner and LockedUntilUtc
- Renews leases periodically
- Processes jobs concurrently
- Simulates success and failure
- Requeues transient failures
- Permanently fails after retry threshold

Worker behavior is configurable via appsettings.json.

---

## Metrics

Prometheus metrics are exposed at:

/metrics

Includes:

- Dispatch claimed total
- Dispatch processed counts (completed, failed, requeued, lease lost)
- In-flight job count
- Processing duration histogram
- Lease renewal counts
- Worker error counts

---

## Demo Controls

The Ops dashboard includes demonstration controls:

- Create 1 to 50 jobs
- Live worker-driven mode
- Static mixed status mode
- Failed-only mode
- Clear all jobs with confirmation

This enables visual demonstration of worker coordination behavior.

---

## Tech Stack

- ASP.NET Core
- Razor Pages
- Entity Framework Core
- BackgroundService
- PostgreSQL
- Docker / Docker Compose
- Prometheus-net
- Config-driven runtime tuning

---

## Purpose

This project demonstrates:

- Backend system design
- Distributed processing concepts
- Concurrency management
- Observability practices
- Operational UI design
- Production-ready Docker packaging
- Clean feature branching and release workflows

It is intended as a portfolio and recruitment-ready backend demonstration.

---

## Author

Nevin Kadlec  
Software Developer focused on distributed systems, operational tooling, and scalable backend architecture.
